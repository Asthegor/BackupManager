using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using BackupManager.Models;

namespace BackupManager.Services
{
    public sealed class JobScheduler(List<BackupJob> jobs) : IDisposable
    {
        private readonly List<BackupJob> _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        private readonly Queue<BackupJob> _pendingJobs   = new();
        private readonly HashSet<BackupJob> _setPending  = [];
        private readonly CancellationTokenSource _cts    = new();

        // Verrou unique protégeant _pendingJobs, _setPending et _isRunningJob
        private readonly Lock _syncRoot = new();
        private bool _isRunningJob = false;

        private Task? _loopTask;

        // ── Events UI ────────────────────────────────────────────────────────
        /// <summary>Levé quand l'accessibilité de la destination d'un job change.</summary>
        public event Action<BackupJob, bool>?             DestinationAccessChanged;

        /// <summary>Levé à la fin (succès ou échec) d'une sauvegarde planifiée.</summary>
        public event Action<BackupJob, bool, string?>?    BackupCompleted;

        /// <summary>Levé à chaque fichier traité — progression d'une sauvegarde planifiée.</summary>
        public event Action<BackupJob, BackupProgress>?   BackupProgressChanged;

        /// <summary>Levé quand un statut quelconque change (pour rafraîchir l'icône tray).</summary>
        public event Action?                              AnyStatusChanged;

        // ── Démarrage ────────────────────────────────────────────────────────

        public void Start()
        {
            if (_loopTask != null)
                return;

            _loopTask = Task.Run(SchedulerLoop, _cts.Token);
        }

        // ── Boucle principale (thread pool) ──────────────────────────────────

        private async Task SchedulerLoop()
        {
            var lastAccess = new Dictionary<BackupJob, bool>();

            while (!_cts.IsCancellationRequested)
            {
                DateTime now = DateTime.Now;

                // ── Vérification de chaque job ────────────────────────────
                foreach (var job in _jobs)
                {
                    // Accès destination
                    bool access = job.DestinationAccessible;
                    if (!lastAccess.TryGetValue(job, out bool prev) || prev != access)
                    {
                        lastAccess[job] = access;
                        DestinationAccessChanged?.Invoke(job, access);
                        AnyStatusChanged?.Invoke();
                    }

                    if (!job.IsActive)
                        continue;

                    bool isTime =
                        now.Hour   == job.BackupTime.Hours &&
                        now.Minute == job.BackupTime.Minutes;

                    if (isTime)
                    {
                        // Planifier le job si pas déjà en file
                        lock (_syncRoot)
                        {
                            if (_setPending.Add(job))
                                _pendingJobs.Enqueue(job);
                        }
                    }
                }

                // ── Lancement du prochain job en attente ──────────────────
                BackupJob? nextJob = null;
                lock (_syncRoot)
                {
                    if (!_isRunningJob && _pendingJobs.Count > 0)
                    {
                        nextJob        = _pendingJobs.Dequeue();
                        _isRunningJob  = true;
                    }
                }

                if (nextJob != null)
                {
                    // Pas de await ici : on lance et on continue la boucle
                    _ = Task.Run(() => RunJobAsync(nextJob));
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), _cts.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        // ── Exécution d'un job (thread pool) ─────────────────────────────────

        private async Task RunJobAsync(BackupJob job)
        {
            try
            {
                // IProgress<T> sans SynchronizationContext capturé (on est sur le pool) :
                // on invoque directement l'event — l'abonné (MainForm) est responsable
                // de son propre BeginInvoke si nécessaire.
                var progress = new Progress<BackupProgress>(p =>
                    BackupProgressChanged?.Invoke(job, p));

                var (success, zipPath) = await BackupService.ExecuteBackupAsync(
                    job, progress, _cts.Token);

                if (success)
                    job.LastBackupDate = DateTime.Now;

                BackupCompleted?.Invoke(job, success, success ? zipPath : null);
            }
            catch (OperationCanceledException)
            {
                BackupCompleted?.Invoke(job, false, null);
            }
            catch
            {
                BackupCompleted?.Invoke(job, false, null);
            }
            finally
            {
                lock (_syncRoot)
                {
                    _setPending.Remove(job);
                    _isRunningJob = false;
                }
                AnyStatusChanged?.Invoke();
            }
        }

        // ── IDisposable ───────────────────────────────────────────────────────

        public void Dispose()
        {
            _cts.Cancel();
            try { _loopTask?.Wait(2000); }
            catch { /* ignore */ }
            _cts.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
