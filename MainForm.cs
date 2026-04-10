using BackupManager.Models;
using BackupManager.Services;
using BackupManager.UI;
using BackupManager.Utils;

namespace BackupManager
{
    public partial class MainForm : Form
    {
        // ── Données ──────────────────────────────────────────────────────────
        private readonly List<BackupJob>    _jobs;
        private readonly BackupRepository   _repository;
        private readonly JobScheduler       _scheduler;

        // ── Contrôles UI ─────────────────────────────────────────────────────
        private readonly FlowLayoutPanel    _jobsList;
        private readonly Button             _btnAdd;
        private readonly Button             _btnEdit;
        private readonly Button             _btnDelete;
        private readonly Button             _btnLaunch;
        private readonly Button             _btnLaunchAll;

        // Barre de statut en bas de fenêtre
        private readonly StatusStrip            _statusBar;
        private readonly ToolStripStatusLabel   _statusLabel;
        private readonly ToolStripProgressBar   _progressBar;
        private readonly ToolStripStatusLabel   _cancelLink;

        // Tray
        private readonly NotifyIcon         _tray;
        private readonly ContextMenuStrip   _trayMenu;

        // Annulation de la sauvegarde manuelle en cours
        private CancellationTokenSource? _backupCts;

        // ── Propriété ────────────────────────────────────────────────────────

        /// <summary>Retourne une copie triée des jobs — utiliser _jobs pour les mutations.</summary>
        public List<BackupJob> Jobs => [.. _jobs.OrderBy(j => j.BackupTime)];

        // ── Constructeur ─────────────────────────────────────────────────────

        public MainForm()
        {
            _repository = new BackupRepository();
            _jobs       = [.. _repository.Load()];

            // ── Fenêtre ──────────────────────────────────────────────────
            Text            = "BackupManager";
            StartPosition   = FormStartPosition.CenterScreen;
            Size            = new Size(720, 480);
            MinimumSize     = new Size(600, 400);
            Icon            = IconHelper.Logo;

            // ── Bandeau actions ──────────────────────────────────────────
            var header  = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Color.WhiteSmoke, Padding = new Padding(8) };
            _btnAdd     = new Button { Text = "Ajouter",    Width = 90, Height = 28, Location = new Point(8,   10) };
            _btnEdit    = new Button { Text = "Modifier",   Width = 90, Height = 28, Location = new Point(106, 10) };
            _btnDelete  = new Button { Text = "Supprimer",  Width = 90, Height = 28, Location = new Point(204, 10) };
            _btnLaunch    = new Button { Text = "Lancer",      Width = 90,  Height = 28, Location = new Point(302, 10) };
            _btnLaunchAll = new Button { Text = "Lancer tous", Width = 100, Height = 28, Location = new Point(400, 10) };
            header.Controls.AddRange([_btnAdd, _btnEdit, _btnDelete, _btnLaunch, _btnLaunchAll]);

            // ── Barre de statut ──────────────────────────────────────────
            _statusBar  = new StatusStrip { Dock = DockStyle.Bottom, SizingGrip = false };

            _statusLabel = new ToolStripStatusLabel("Prêt")
            {
                Spring    = true,          // occupe tout l'espace libre à gauche
                TextAlign = ContentAlignment.MiddleLeft
            };

            _progressBar = new ToolStripProgressBar
            {
                Width   = 200,
                Minimum = 0,
                Maximum = 100,
                Visible = false
            };

            // "✕ Annuler" : affiché uniquement pendant une sauvegarde manuelle
            _cancelLink = new ToolStripStatusLabel("✕ Annuler")
            {
                IsLink    = true,
                LinkColor = Color.Crimson,
                Visible   = false
            };
            _cancelLink.Click += (s, e) => _backupCts?.Cancel();

            _statusBar.Items.AddRange([_statusLabel, _progressBar, _cancelLink]);

            // ── Liste des jobs ───────────────────────────────────────────
            _jobsList = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents  = false,
                AutoScroll    = true,
                BackColor     = Color.White
            };

            // Ordre d'ajout : header en haut, statusBar en bas, jobsList au milieu
            Controls.Add(_jobsList);
            Controls.Add(header);
            Controls.Add(_statusBar);

            _jobsList.BringToFront();

            // ── Tray ─────────────────────────────────────────────────────
            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add(new ToolStripMenuItem("Ouvrir", null, (s, e) => RestoreFromTray()));
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(new ToolStripMenuItem("Quitter", null, (s, e) =>
            {
                _scheduler?.Dispose();
                _tray!.Visible = false;
                Application.Exit();
            }));

            _tray = new NotifyIcon
            {
                Visible          = true,
                Text             = "BackupManager",
                Icon             = IconHelper.TrayGreen,
                ContextMenuStrip = _trayMenu
            };
            _tray.DoubleClick += (s, e) => RestoreFromTray();

            // ── Events boutons ───────────────────────────────────────────
            _btnAdd.Click    += (s, e) => AddJob();
            _btnEdit.Click   += (s, e) => EditSelectedJob();
            _btnDelete.Click += (s, e) => DeleteSelectedJob();
            _btnLaunch.Click    += async (s, e) => await LaunchSelectedJobAsync();
            _btnLaunchAll.Click += async (s, e) => await LaunchAllJobsAsync();

            // ── Affichage initial ────────────────────────────────────────
            ReloadJobPanels();

            // ── Scheduler ───────────────────────────────────────────────
            _scheduler = new JobScheduler(_jobs);
            _scheduler.DestinationAccessChanged += Scheduler_DestinationAccessChanged;
            _scheduler.BackupCompleted          += Scheduler_BackupCompleted;
            _scheduler.BackupProgressChanged    += Scheduler_BackupProgressChanged;
            _scheduler.AnyStatusChanged         += UpdateTrayIcon;
            _scheduler.Start();

            // ── Fermeture ────────────────────────────────────────────────
            FormClosing += MainForm_FormClosing;
            Resize      += MainForm_Resize!;
        }

        // ── Helpers UI ───────────────────────────────────────────────────────

        private void ReloadJobPanels()
        {
            _jobsList.SuspendLayout();
            _jobsList.Controls.Clear();

            foreach (var job in Jobs)
            {
                var panel = new JobPanel(job)
                {
                    Width = _jobsList.ClientSize.Width - SystemInformation.VerticalScrollBarWidth
                };
                panel.SelectedChanged += Panel_SelectedChanged!;
                _jobsList.Controls.Add(panel);
            }

            _jobsList.ResumeLayout();
            UpdateTrayIcon();
            AdjustJobPanelsWidth();
        }

        private void Panel_SelectedChanged(object sender, EventArgs e)
        {
            if (sender is JobPanel selectedPanel && selectedPanel.Selected)
            {
                foreach (JobPanel panel in _jobsList.Controls.OfType<JobPanel>())
                {
                    if (panel != selectedPanel && panel.Selected)
                        panel.ToggleSelection();
                }
            }
        }

        private JobPanel? GetSelectedPanel()
            => _jobsList.Controls.OfType<JobPanel>().FirstOrDefault(p => p.Selected);

        private JobPanel? GetPanelForJob(BackupJob job)
            => _jobsList.Controls.OfType<JobPanel>().FirstOrDefault(p => ReferenceEquals(p.Job, job));

        // ── CRUD jobs ────────────────────────────────────────────────────────

        private void AddJob()
        {
            using var dlg = new JobEditForm();
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _jobs.Add(dlg.Result);     // mutation sur _jobs, pas Jobs (qui est une copie)
                ReloadJobPanels();
                _repository.Save(Jobs);
            }
        }

        private void EditSelectedJob()
        {
            var selected = GetSelectedPanel();
            if (selected == null)
            {
                MessageBox.Show(this, "Sélectionne un job dans la liste.", "Info",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dlg = new JobEditForm(selected.Job);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                selected.RefreshStatus();
                selected.Invalidate();
                ReloadJobPanels();
                UpdateTrayIcon();
                _repository.Save(Jobs);
            }
        }

        private void DeleteSelectedJob()
        {
            var selected = GetSelectedPanel();
            if (selected == null)
            {
                MessageBox.Show(this, "Sélectionne un job à supprimer.", "Info",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(this,
                    $"Supprimer le job '{selected.Job.Name}' ?", "Confirmation",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                // ✅ Correction : mutation sur _jobs (liste réelle), pas Jobs (copie triée)
                _jobs.Remove(selected.Job);
                ReloadJobPanels();
                _repository.Save(Jobs);
            }
        }

        // ── Lancement manuel (async) ─────────────────────────────────────────

        private async Task LaunchSelectedJobAsync()
        {
            var selected = GetSelectedPanel();
            if (selected == null)
            {
                MessageBox.Show(this, "Sélectionne un job à lancer.", "Info",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _backupCts = new CancellationTokenSource();

            // Progress<T> est créé sur le thread UI : ses callbacks sont automatiquement
            // dispatchés sur le thread UI via le SynchronizationContext capturé ici.
            var progress = new Progress<BackupProgress>(p =>
            {
                _progressBar.Maximum = p.FilesTotal;
                _progressBar.Value   = Math.Min(p.FilesProcessed, p.FilesTotal);
                _statusLabel.Text    = $"Sauvegarde : {p.CurrentFile}  ({p.Percent} %)";
                selected.SetRunning(true, p.Percent);
            });

            SetBackupUIState(isRunning: true);

            try
            {
                var (success, zipPath) = await BackupService.ExecuteBackupAsync(
                    selected.Job, progress, _backupCts.Token);

                if (success)
                {
                    selected.Job.LastCreatedFile = zipPath;
                    selected.Job.LastBackupDate  = DateTime.Now;
                    selected.RefreshStatus();
                    _repository.Save(Jobs);
                    _statusLabel.Text = $"✔ Sauvegarde terminée : {Path.GetFileName(zipPath)}";
                }
                else
                {
                    _statusLabel.Text = "Sauvegarde annulée ou échec (destination inaccessible ?).";
                }
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Erreur : {ex.Message}";
            }
            finally
            {
                _backupCts.Dispose();
                _backupCts = null;
                selected.SetRunning(false);
                SetBackupUIState(isRunning: false);
            }
        }

        /// <summary>
        /// Lance séquentiellement tous les jobs actifs dont la destination est accessible.
        /// La progression globale (job N/total) est affichée dans la StatusStrip.
        /// Un seul CancellationTokenSource couvre l'ensemble de la session.
        /// </summary>
        private async Task LaunchAllJobsAsync()
        {
            var jobsToRun = Jobs
                .Where(j => j.IsActive && j.DestinationAccessible)
                .ToList();

            if (jobsToRun.Count == 0)
            {
                MessageBox.Show(this,
                    "Aucun job actif avec une destination accessible.",
                    "Lancer tous", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _backupCts = new CancellationTokenSource();
            SetBackupUIState(isRunning: true);

            int total     = jobsToRun.Count;
            int succeeded = 0;
            int failed    = 0;

            try
            {
                for (int i = 0; i < total; i++)
                {
                    if (_backupCts.Token.IsCancellationRequested)
                        break;

                    var job   = jobsToRun[i];
                    var panel = GetPanelForJob(job);

                    // Progression globale dans le label, progression fichier dans la bar
                    _statusLabel.Text    = $"Job {i + 1}/{total} : {job.Name}";
                    _progressBar.Maximum = 100;
                    _progressBar.Value   = 0;

                    var progress = new Progress<BackupProgress>(p =>
                    {
                        _progressBar.Value = Math.Min(p.Percent, 100);
                        _statusLabel.Text  = $"Job {i + 1}/{total} – {job.Name} : {p.CurrentFile}  ({p.Percent} %)";
                        panel?.SetRunning(true, p.Percent);
                    });

                    try
                    {
                        var (success, zipPath) = await BackupService.ExecuteBackupAsync(
                            job, progress, _backupCts.Token);

                        if (success)
                        {
                            job.LastCreatedFile = zipPath;
                            job.LastBackupDate  = DateTime.Now;
                            succeeded++;
                        }
                        else
                        {
                            failed++;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        failed++;
                    }
                    finally
                    {
                        panel?.SetRunning(false);
                        panel?.RefreshStatus();
                    }
                }

                // Bilan
                bool cancelled = _backupCts.Token.IsCancellationRequested;
                _statusLabel.Text = cancelled
                    ? $"Annulé — {succeeded}/{total} sauvegarde(s) effectuée(s)."
                    : failed == 0
                        ? $"✔ {succeeded}/{total} sauvegarde(s) effectuée(s)."
                        : $"⚠ {succeeded} OK, {failed} échec(s) sur {total} jobs.";

                _repository.Save(Jobs);
            }
            finally
            {
                // Capturer le bilan AVANT que SetBackupUIState réinitialise le label
                string bilan = _statusLabel?.Text ?? string.Empty;

                _backupCts.Dispose();
                _backupCts = null;
                SetBackupUIState(isRunning: false);

                // Restaurer le bilan (SetBackupUIState remet "Prêt", on l'écrase)
                _statusLabel?.Text = bilan;
            }
        }

        /// <summary>
        /// Verrouille ou déverrouille les boutons pendant une sauvegarde manuelle.
        /// N'affecte PAS les sauvegardes planifiées (transparentes pour l'utilisateur).
        /// </summary>
        private void SetBackupUIState(bool isRunning)
        {
            _btnAdd.Enabled       = !isRunning;
            _btnEdit.Enabled      = !isRunning;
            _btnDelete.Enabled    = !isRunning;
            _btnLaunch.Enabled    = !isRunning;
            _btnLaunchAll.Enabled = !isRunning;

            _progressBar.Visible = isRunning;
            _cancelLink.Visible  = isRunning;

            if (!isRunning)
            {
                _progressBar.Value = 0;
                _statusLabel.Text  = "Prêt";
            }
        }

        // ── Tray & fermeture ─────────────────────────────────────────────────

        private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideToTray();
            }
            _repository.Save(Jobs);
        }

        private void HideToTray()
        {
            Hide();
            ShowInTaskbar = false;
            _tray.Visible = true;
            _tray.ShowBalloonTip(2000, "BackupManager",
                "L'application continue en arrière-plan.", ToolTipIcon.Info);
        }

        private void RestoreFromTray()
        {
            Show();
            WindowState   = FormWindowState.Normal;
            ShowInTaskbar = true;
            Activate();
        }

        private void UpdateTrayIcon()
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(UpdateTrayIcon); return; }

            int active = Jobs.Count(j => j.IsActive);
            int ok     = Jobs.Count(j => j.IsActive && j.DestinationAccessible);

            if (active == 0)
            {
                _tray.Icon = IconHelper.TrayGreen;
                _tray.Text = "BackupManager – aucun job actif";
            }
            else if (ok == 0)
            {
                _tray.Icon = IconHelper.TrayRed;
                _tray.Text = "BackupManager – aucun job sauvegardable";
            }
            else if (ok == active)
            {
                _tray.Icon = IconHelper.TrayGreen;
                _tray.Text = "BackupManager – tous les jobs sauvegardables";
            }
            else
            {
                _tray.Icon = IconHelper.TrayOrange;
                _tray.Text = "BackupManager – partiel";
            }
        }

        // ── Events scheduler → thread UI ─────────────────────────────────────

        private void Scheduler_DestinationAccessChanged(BackupJob job, bool access)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(() => Scheduler_DestinationAccessChanged(job, access)); return; }

            GetPanelForJob(job)?.RefreshStatus();
        }

        private void Scheduler_BackupCompleted(BackupJob job, bool success, string? zipPath)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(() => Scheduler_BackupCompleted(job, success, zipPath)); return; }

            string title = success ? "Sauvegarde effectuée" : "Échec de la sauvegarde";
            string body  = success
                ? $"Job : {job.Name}\nFichier : {zipPath}"
                : $"Job : {job.Name}";

            _tray.ShowBalloonTip(4000, title, body, success ? ToolTipIcon.Info : ToolTipIcon.Error);

            var panel = GetPanelForJob(job);
            panel?.SetRunning(false);
            panel?.RefreshStatus();
        }

        private void Scheduler_BackupProgressChanged(BackupJob job, BackupProgress p)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(() => Scheduler_BackupProgressChanged(job, p)); return; }

            // Mise à jour légère dans la barre de statut (sans bloquer les boutons)
            _statusLabel.Text = $"[Planifié] {job.Name} : {p.CurrentFile}  ({p.Percent} %)";
            GetPanelForJob(job)?.SetRunning(true, p.Percent);
        }

        // ── Redimensionnement ────────────────────────────────────────────────

        private void MainForm_Resize(object sender, EventArgs e) => AdjustJobPanelsWidth();

        private void AdjustJobPanelsWidth()
        {
            int available = _jobsList.ClientSize.Width - SystemInformation.VerticalScrollBarWidth / 2;
            if (_jobsList.VerticalScroll.Visible)
                available -= SystemInformation.VerticalScrollBarWidth / 2;

            foreach (JobPanel panel in _jobsList.Controls.OfType<JobPanel>())
                panel.Width = available;
        }
    }
}
