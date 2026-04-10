using System.Threading;
using System.Threading.Tasks;

using BackupManager.Models;
using BackupManager.Utils;

namespace BackupManager.Services
{
    public static class BackupService
    {
        /// <summary>
        /// Version synchrone — conservée pour usage interne du JobScheduler
        /// qui tourne déjà dans un Task.Run.
        /// </summary>
        public static bool ExecuteBackup(BackupJob job, out string? createdFile)
        {
            createdFile = null;

            if (!job.DestinationAccessible)
                return false;

            Directory.CreateDirectory(job.DestinationDirectory);

            string targetFile = BuildTargetPath(job);

            ZipHelper.CreateBackupZip(job, targetFile);
            createdFile         = targetFile;
            job.LastBackupDate  = DateTime.Now;
            return true;
        }

        /// <summary>
        /// Version asynchrone — à utiliser depuis le thread UI (MainForm).
        /// N'bloque jamais le thread appelant.
        /// </summary>
        /// <param name="job">Le job à exécuter.</param>
        /// <param name="progress">
        ///   Optionnel. Progress&lt;BackupProgress&gt; créé sur le thread UI :
        ///   les callbacks seront automatiquement dispatché sur le thread UI.
        /// </param>
        /// <param name="ct">Optionnel. Permet l'annulation propre.</param>
        /// <returns>
        ///   (Success: true, CreatedFile: chemin) si OK.
        ///   (Success: false, CreatedFile: null) si destination inaccessible ou annulé.
        /// </returns>
        public static async Task<(bool Success, string? CreatedFile)> ExecuteBackupAsync(
            BackupJob job,
            IProgress<BackupProgress>? progress = null,
            CancellationToken ct = default)
        {
            if (!job.DestinationAccessible)
                return (false, null);

            Directory.CreateDirectory(job.DestinationDirectory);

            string targetFile = BuildTargetPath(job);

            try
            {
                // Task.Run délègue le travail CPU-bound sur un thread du pool,
                // libérant le thread UI immédiatement.
                await Task.Run(
                    () => ZipHelper.CreateBackupZip(job, targetFile, progress, ct),
                    ct);

                job.LastBackupDate = DateTime.Now;
                return (true, targetFile);
            }
            catch (OperationCanceledException)
            {
                // Annulation propre : pas de fichier créé (ZipHelper nettoie le partiel)
                return (false, null);
            }
            // Les autres exceptions (IOException, etc.) remontent à l'appelant
        }

        // --- Helpers ---

        private static string BuildTargetPath(BackupJob job)
        {
            string fileNameSafe = string.Join("_", job.Name.Split(Path.GetInvalidFileNameChars()));
            if (string.IsNullOrWhiteSpace(fileNameSafe))
                fileNameSafe = "backup";

            string fileName = $"{fileNameSafe}_{DateTime.Now:yyyyMMdd_HHmm}.zip";
            return Path.Combine(job.DestinationDirectory, fileName);
        }
    }
}
