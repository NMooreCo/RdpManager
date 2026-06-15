using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using RdpManager.Data.Repositories;
using RdpManager.Models;

namespace RdpManager.Data.Migration
{
    /// <summary>
    /// Migrates data from JSON files to SQLite database
    /// </summary>
    public class JsonMigrator
    {
        private readonly ComputerRepository _computerRepo;
        private readonly ConnectionLogRepository _connectionLogRepo;
        private readonly PreferencesRepository _preferencesRepo;
        private readonly string _appDirectory;

        public JsonMigrator()
        {
            _computerRepo = new ComputerRepository();
            _connectionLogRepo = new ConnectionLogRepository();
            _preferencesRepo = new PreferencesRepository();
            _appDirectory = AppDomain.CurrentDomain.BaseDirectory;
        }

        /// <summary>
        /// Perform complete migration from JSON to SQLite
        /// </summary>
        public MigrationResult Migrate()
        {
            var result = new MigrationResult();

            try
            {
                // Migrate computers
                var computersPath = Path.Combine(_appDirectory, "computers.json");
                if (File.Exists(computersPath))
                {
                    result.ComputersMigrated = MigrateComputers(computersPath);
                }

                // Migrate connection history
                var historyPath = Path.Combine(_appDirectory, "connectionHistory.json");
                if (File.Exists(historyPath))
                {
                    result.ConnectionHistoryMigrated = MigrateConnectionHistory(historyPath);
                }

                // Migrate preferences
                var preferencesPath = Path.Combine(_appDirectory, "preferences.json");
                if (File.Exists(preferencesPath))
                {
                    result.PreferencesMigrated = MigratePreferences(preferencesPath);
                }

                // Backup JSON files
                BackupJsonFiles();

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Migrate computers.json to Computers table
        /// </summary>
        private int MigrateComputers(string filePath)
        {
            var json = File.ReadAllText(filePath);
            var computers = JsonConvert.DeserializeObject<List<ComputerEntry>>(json);

            if (computers == null || computers.Count == 0)
                return 0;

            int count = 0;
            foreach (var computer in computers)
            {
                // Set default values if not present
                if (computer.FriendlyName == null)
                    computer.FriendlyName = computer.MachineName;

                _computerRepo.Insert(computer);
                count++;
            }

            return count;
        }

        /// <summary>
        /// Migrate connectionHistory.json to ConnectionLogs table
        /// </summary>
        private int MigrateConnectionHistory(string filePath)
        {
            var json = File.ReadAllText(filePath);
            var history = JsonConvert.DeserializeObject<ConnectionHistory>(json);

            if (history?.Entries == null || history.Entries.Count == 0)
                return 0;

            int count = 0;
            foreach (var entry in history.Entries)
            {
                // Find or create the computer entry
                var computers = _computerRepo.GetAll();
                var computer = computers.Find(c =>
                    c.MachineName.Equals(entry.MachineName, StringComparison.OrdinalIgnoreCase) &&
                    (c.Domain ?? "").Equals(entry.Domain ?? "", StringComparison.OrdinalIgnoreCase));

                if (computer == null)
                {
                    // Create a new computer entry from history
                    computer = new ComputerEntry
                    {
                        MachineName = entry.MachineName,
                        FriendlyName = entry.Name,
                        Domain = entry.Domain,
                        Group = entry.Group,
                        IsFavorite = entry.IsFavorite
                    };
                    computer.Id = _computerRepo.Insert(computer);
                }
                else if (entry.IsFavorite && !computer.IsFavorite)
                {
                    // Update favorite status
                    _computerRepo.ToggleFavorite(computer.Id);
                }

                // Create basic connection log entries (we don't have detailed info from old format)
                for (int i = 0; i < entry.ConnectionCount; i++)
                {
                    var logId = _connectionLogRepo.StartConnection(
                        computer.Id,
                        "Unknown",
                        entry.MachineName,
                        entry.Name,  // ConnectionHistoryEntry uses "Name" not "FriendlyName"
                        entry.Domain,
                        entry.Group);
                    // End it immediately with the last connected timestamp
                    _connectionLogRepo.EndConnection(logId, true, "Migrated from JSON", null);
                    count++;
                }

                // Update last connected timestamp
                if (entry.LastConnected != default)
                {
                    _computerRepo.UpdateLastConnection(computer.Id, null);
                }
            }

            return count;
        }

        /// <summary>
        /// Migrate preferences.json to Preferences table
        /// </summary>
        private int MigratePreferences(string filePath)
        {
            var json = File.ReadAllText(filePath);
            var preferences = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

            if (preferences == null || preferences.Count == 0)
                return 0;

            int count = 0;
            foreach (var kvp in preferences)
            {
                var valueType = kvp.Value?.GetType().Name ?? "string";
                _preferencesRepo.Set(kvp.Key, kvp.Value?.ToString() ?? "", valueType);
                count++;
            }

            return count;
        }

        /// <summary>
        /// Backup JSON files by renaming them to .json.backup
        /// </summary>
        private void BackupJsonFiles()
        {
            var files = new[] { "computers.json", "connectionHistory.json", "preferences.json" };

            foreach (var file in files)
            {
                var filePath = Path.Combine(_appDirectory, file);
                if (File.Exists(filePath))
                {
                    var backupPath = filePath + ".backup";

                    // If backup already exists, delete it
                    if (File.Exists(backupPath))
                        File.Delete(backupPath);

                    File.Move(filePath, backupPath);
                }
            }
        }
    }

    /// <summary>
    /// Result of JSON migration operation
    /// </summary>
    public class MigrationResult
    {
        public bool Success { get; set; }
        public int ComputersMigrated { get; set; }
        public int ConnectionHistoryMigrated { get; set; }
        public int PreferencesMigrated { get; set; }
        public string? ErrorMessage { get; set; }

        public int TotalItemsMigrated => ComputersMigrated + ConnectionHistoryMigrated + PreferencesMigrated;
    }
}
