using System.Diagnostics;

using BackupManager.Models;
using BackupManager.Utils;

namespace BackupManager.UI
{
    public partial class JobPanel : UserControl
    {
        private readonly BackupJob _job;
        private bool _selected;

        // Contrôles
        private readonly PictureBox  _statusIcon;
        private readonly Label       _nameLabel;
        private readonly PictureBox  _openFolderIcon;
        private readonly ProgressBar _progressBar;   // visible uniquement pendant la sauvegarde

        public BackupJob Job => _job;

        public JobPanel(BackupJob job)
        {
            _job      = job ?? throw new ArgumentNullException(nameof(job));
            _selected = false;

            Height      = 50;
            BorderStyle = BorderStyle.FixedSingle;
            Margin      = new Padding(2);
            Padding     = new Padding(6);

            // ── Icône statut ─────────────────────────────────────────────
            _statusIcon = new PictureBox
            {
                Size     = new Size(24, 24),
                Location = new Point(8, 13),
                SizeMode = PictureBoxSizeMode.Zoom,
                Cursor   = Cursors.Hand
            };
            _statusIcon.Click += (s, e) => ToggleSelection();

            // ── Nom + heure ──────────────────────────────────────────────
            _nameLabel = new Label
            {
                Location = new Point(40, 15),
                AutoSize = true,
                Font     = new Font("Segoe UI", 10, FontStyle.Regular)
            };
            _nameLabel.Click += (s, e) => ToggleSelection();

            // ── Ouvrir dossier ───────────────────────────────────────────
            _openFolderIcon = new PictureBox
            {
                Size     = new Size(40, 40),
                SizeMode = PictureBoxSizeMode.Zoom,
                Cursor   = Cursors.Hand,
                Image    = IconHelper.OpenFolder
            };
            _openFolderIcon.Click += (s, e) =>
            {
                if (Directory.Exists(_job.DestinationDirectory))
                    Process.Start("explorer.exe", _job.DestinationDirectory);
            };

            // ── Barre de progression (masquée par défaut) ────────────────
            _progressBar = new ProgressBar
            {
                Height  = 4,
                Minimum = 0,
                Maximum = 100,
                Value   = 0,
                Style   = ProgressBarStyle.Continuous,
                Visible = false
            };

            Controls.AddRange([_statusIcon, _nameLabel, _openFolderIcon, _progressBar]);

            RefreshStatus();

            Resize += JobPanel_Resize!;
            Click  += (s, e) => ToggleSelection();
        }

        // ── Layout ───────────────────────────────────────────────────────────

        private void JobPanel_Resize(object sender, EventArgs e)
        {
            int paddingRight = 10;
            _openFolderIcon.Left = ClientSize.Width - paddingRight - _openFolderIcon.Width;
            _openFolderIcon.Top  = (Height - _openFolderIcon.Height) / 2;

            _nameLabel.MaximumSize = new Size(_openFolderIcon.Left - 48, 0);

            // La barre de progression longe le bas du panel
            _progressBar.Location = new Point(0, Height - _progressBar.Height - 1);
            _progressBar.Width    = ClientSize.Width;
        }

        // ── État "en cours de sauvegarde" ────────────────────────────────────

        /// <summary>
        /// Active ou désactive l'affichage de la progression sur ce panel.
        /// Appelé depuis MainForm via les events du scheduler ou de BackupService.
        /// </summary>
        public void SetRunning(bool running, int percent = 0)
        {
            _progressBar.Visible = running;
            if (running)
                _progressBar.Value = Math.Clamp(percent, 0, 100);
            else
                _progressBar.Value = 0;
        }

        // ── Rafraîchissement du statut ───────────────────────────────────────

        public void RefreshStatus()
        {
            string derniere = _job.LastBackupDate.HasValue
                ? _job.LastBackupDate.Value.ToString("yyyy-MM-dd HH:mm")
                : "jamais";

            _nameLabel.Text = $"[{_job.BackupTime:hh\\hmm}] {_job.Name}  (dernière exécution : {derniere})";

            bool ok = _job.IsActive && Directory.Exists(_job.DestinationDirectory);

            _statusIcon.Image         = ok ? IconHelper.FolderOk : IconHelper.FolderError;
            _openFolderIcon.Enabled   = ok;
            _openFolderIcon.Cursor    = ok ? Cursors.Hand : Cursors.Default;
            _openFolderIcon.Image     = ok ? IconHelper.OpenFolder
                                           : IconHelper.ToGrayScale(IconHelper.OpenFolder);

            // Replacer les contrôles après changement de texte
            JobPanel_Resize(this, EventArgs.Empty);
        }

        // ── Sélection ────────────────────────────────────────────────────────

        public event EventHandler? SelectedChanged;

        public bool Selected
        {
            get => _selected;
            private set
            {
                if (_selected == value) return;
                _selected  = value;
                BackColor  = _selected ? Color.LightBlue : Color.White;
                SelectedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void ToggleSelection() => Selected = !Selected;
    }
}
