namespace BackupManager.Models
{
    public class BackupJob
    {
        public string Name { get; set; } = "";
        public string SourceDirectory { get; set; } = "";
        public string DestinationDirectory { get; set; } = "";
        public TimeSpan BackupTime { get; set; }
        public bool IsActive { get; set; }
        public DateTime? LastBackupDate { get; set; }
        public List<string> ExcludedDirectories { get; set; } = [];
        public string? LastCreatedFile { get; set; }
        public bool DestinationAccessible =>
            System.IO.Directory.Exists(DestinationDirectory);
    }
}
