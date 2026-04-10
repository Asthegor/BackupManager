using System.IO;
using System.IO.Compression;
using System.Threading;

using BackupManager.Models;

namespace BackupManager.Utils
{
    public static class ZipHelper
    {
        /// <summary>
        /// Crée un ZIP de sauvegarde pour le job donné.
        /// </summary>
        /// <param name="job">Le job à sauvegarder.</param>
        /// <param name="targetFile">Chemin complet du fichier ZIP à créer.</param>
        /// <param name="progress">
        ///   Optionnel. Reçoit un <see cref="BackupProgress"/> après chaque fichier traité.
        ///   Progress&lt;T&gt; dispatche automatiquement sur le thread UI.
        /// </param>
        /// <param name="ct">
        ///   Optionnel. Permet d'annuler l'opération proprement.
        ///   Si annulé, le fichier ZIP partiel est supprimé.
        /// </param>
        public static void CreateBackupZip(
            BackupJob job,
            string targetFile,
            IProgress<BackupProgress>? progress = null,
            CancellationToken ct = default)
        {
            if (!Directory.Exists(job.SourceDirectory))
                throw new DirectoryNotFoundException(job.SourceDirectory);

            // --- 1. Collecter tous les fichiers non exclus AVANT de créer le ZIP ---
            // Cela permet de connaître le total pour la barre de progression.
            var filesToAdd = CollectFiles(job);
            int total = filesToAdd.Count;

            // --- 2. Créer le ZIP ---
            try
            {
                using var zip = ZipFile.Open(targetFile, ZipArchiveMode.Create);

                for (int i = 0; i < total; i++)
                {
                    // Vérifier l'annulation avant chaque fichier
                    ct.ThrowIfCancellationRequested();

                    string file = filesToAdd[i];
                    string rel  = Path.GetRelativePath(job.SourceDirectory, file);

                    zip.CreateEntryFromFile(file, rel, CompressionLevel.Optimal);

                    // Signaler la progression après chaque fichier traité
                    progress?.Report(new BackupProgress(
                        filesProcessed: i + 1,
                        filesTotal:     total,
                        currentFile:    Path.GetFileName(file),
                        jobName:        job.Name));
                }
            }
            catch
            {
                // Nettoyage : supprimer le ZIP partiel si annulation ou erreur
                if (File.Exists(targetFile))
                {
                    try { File.Delete(targetFile); }
                    catch { /* ignorer l'échec de suppression */ }
                }
                throw; // propager l'exception (OperationCanceledException ou autre)
            }
        }

        /// <summary>
        /// Collecte la liste des fichiers à inclure dans le ZIP (hors exclusions).
        /// </summary>
        private static List<string> CollectFiles(BackupJob job)
        {
            var result = new List<string>();

            foreach (string file in Directory.GetFiles(job.SourceDirectory, "*", SearchOption.AllDirectories))
            {
                string rel    = Path.GetRelativePath(job.SourceDirectory, file);
                string relDir = Path.GetDirectoryName(rel) ?? string.Empty;

                bool excluded = false;
                foreach (string excl in job.ExcludedDirectories)
                {
                    string normExcl   = excl.Replace('\\', '/').Trim().TrimStart('/', '\\');
                    string normRelDir = relDir.Replace('\\', '/');

                    if (normRelDir.StartsWith(normExcl, StringComparison.OrdinalIgnoreCase))
                    {
                        excluded = true;
                        break;
                    }
                }

                if (!excluded)
                    result.Add(file);
            }

            return result;
        }
    }
}
