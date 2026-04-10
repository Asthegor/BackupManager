namespace BackupManager.Models
{
    /// <summary>
    /// Représente l'état instantané de progression d'une sauvegarde.
    /// Utilisé comme paramètre générique de IProgress&lt;T&gt; entre le worker et le thread UI.
    /// Type immuable : toutes les propriétés sont en lecture seule.
    /// </summary>
    public sealed class BackupProgress(int filesProcessed, int filesTotal, string currentFile, string jobName = "")
    {
        /// <summary>Nombre de fichiers déjà compressés.</summary>
        public int FilesProcessed { get; } = filesProcessed;

        /// <summary>Nombre total de fichiers à compresser.</summary>
        public int FilesTotal { get; } = filesTotal;

        /// <summary>Nom court du fichier en cours de traitement.</summary>
        public string CurrentFile { get; } = currentFile;

        /// <summary>
        /// Pourcentage d'avancement (0-100).
        /// Retourne 0 si FilesTotal vaut 0 pour éviter une division par zéro.
        /// </summary>
        public int Percent => FilesTotal > 0 ? (int)((FilesProcessed / (double)FilesTotal) * 100) : 0;

        /// <summary>Nom du job concerné, pour les mises à jour UI multi-jobs.</summary>
        public string JobName { get; } = jobName;
    }
}