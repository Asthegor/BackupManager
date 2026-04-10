using BackupManager.Models;

using System.Text.Json;
using System.Text.Json.Serialization;

namespace BackupManager.Services
{
    /// <summary>
    /// Charge et sauvegarde la liste des tâches dans un fichier JSON.
    /// </summary>
    public sealed class BackupRepository
    {
        public string ConfigPath { get; }

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            // .NET 7/8 gère DateOnly/TimeOnly nativement ; ce converter garde le format HH:mm
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// Utilise par défaut %AppData%\LACOMBE Dominique\BackupManager\backupConfig.json
        /// </summary>
        public BackupRepository(string? configPath = null)
        {
            if (string.IsNullOrWhiteSpace(configPath))
            {
                var baseDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "LACOMBE Dominique\\BackupManager");
                Directory.CreateDirectory(baseDir);
                ConfigPath = Path.Combine(baseDir, "backupConfig.json");
            }
            else
            {
                var dir = Path.GetDirectoryName(configPath)!;
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                ConfigPath = configPath!;
            }
        }

        public IReadOnlyList<BackupJob> Load()
        {
            if (!File.Exists(ConfigPath))
                return [];

            var json = File.ReadAllText(ConfigPath);
            var data = JsonSerializer.Deserialize<List<BackupJob>>(json, _jsonOptions);
            return data ?? [];
        }

        public void Save(IEnumerable<BackupJob> jobs)
        {
            var json = JsonSerializer.Serialize(jobs, _jsonOptions);
            File.WriteAllText(ConfigPath, json);
        }
    }
}
