using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using RdpManager.Data.Database;
using RdpManager.Data.Models;
using RdpManager.Data.Repositories;

namespace RdpManager.Data.Migration
{
    /// <summary>
    /// Handles database schema migrations for version upgrades
    /// </summary>
    public class SchemaMigrator
    {
        private readonly DatabaseConnection _db;

        public SchemaMigrator()
        {
            _db = DatabaseConnection.Instance;
        }

        /// <summary>
        /// Run all pending migrations to bring database to current schema version
        /// </summary>
        public void Migrate()
        {
            var currentVersion = GetSchemaVersion();
            const int targetVersion = 6; // Current schema version

            System.Diagnostics.Debug.WriteLine($"Database schema version: {currentVersion}, Target: {targetVersion}");

            if (currentVersion < targetVersion)
            {
                System.Diagnostics.Debug.WriteLine($"Running schema migrations from v{currentVersion} to v{targetVersion}...");

                if (currentVersion < 2)
                {
                    MigrateToVersion2();
                }

                if (currentVersion < 3)
                {
                    MigrateToVersion3();
                }

                if (currentVersion < 4)
                {
                    MigrateToVersion4();
                }

                if (currentVersion < 5)
                {
                    MigrateToVersion5();
                }

                if (currentVersion < 6)
                {
                    MigrateToVersion6();
                }
            }
        }

        /// <summary>
        /// Get current schema version from database
        /// </summary>
        private int GetSchemaVersion()
        {
            try
            {
                using var connection = _db.CreateConnection();
                const string sql = "SELECT MAX(Version) FROM SchemaVersion";
                using var cmd = new SQLiteCommand(sql, connection);
                var result = cmd.ExecuteScalar();

                return result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
            }
            catch
            {
                // Schema version table doesn't exist - this is a new database
                return 0;
            }
        }

        /// <summary>
        /// Migrate from version 1 to version 2
        /// Adds Groups table and SortOrder column to Computers
        /// </summary>
        private void MigrateToVersion2()
        {
            System.Diagnostics.Debug.WriteLine("Migrating schema to version 2...");

            using var connection = _db.CreateConnection();
            using var transaction = connection.BeginTransaction();

            try
            {
                // 1. Add SortOrder column to Computers table
                System.Diagnostics.Debug.WriteLine("  - Adding SortOrder column to Computers table");
                ExecuteNonQuery(connection, "ALTER TABLE Computers ADD COLUMN SortOrder INTEGER DEFAULT 0");

                // 2. Create Groups table
                System.Diagnostics.Debug.WriteLine("  - Creating Groups table");
                ExecuteNonQuery(connection, @"
                    CREATE TABLE IF NOT EXISTS Groups (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        FullPath TEXT NOT NULL UNIQUE,
                        ParentPath TEXT,
                        Name TEXT NOT NULL,
                        SortOrder INTEGER DEFAULT 0,
                        CreatedAt TEXT NOT NULL,
                        UpdatedAt TEXT NOT NULL
                    )");

                // 3. Create indices for Groups
                ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS idx_groups_fullpath ON Groups(FullPath)");
                ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS idx_groups_parent ON Groups(ParentPath)");

                // 4. Populate Groups table from existing computer GroupPath values
                System.Diagnostics.Debug.WriteLine("  - Populating Groups table from existing data");
                var groups = ExtractGroupsFromComputers(connection);
                InsertGroups(connection, groups);

                // 5. Update schema version
                System.Diagnostics.Debug.WriteLine("  - Updating schema version to 2");
                ExecuteNonQuery(connection, $@"
                    INSERT INTO SchemaVersion (Version, AppliedAt)
                    VALUES (2, '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}')");

                transaction.Commit();
                System.Diagnostics.Debug.WriteLine("Schema migration to version 2 completed successfully");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                System.Diagnostics.Debug.WriteLine($"Schema migration failed: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Extract unique group paths from existing computers
        /// </summary>
        private List<Group> ExtractGroupsFromComputers(SQLiteConnection connection)
        {
            var groupPaths = new HashSet<string>();
            var sql = "SELECT DISTINCT GroupPath FROM Computers WHERE GroupPath IS NOT NULL AND GroupPath != ''";

            using var cmd = new SQLiteCommand(sql, connection);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var groupPath = reader.GetString(0);
                if (!string.IsNullOrEmpty(groupPath))
                {
                    // Add this path and all parent paths
                    var parts = groupPath.Split('/');
                    var currentPath = "";

                    foreach (var part in parts)
                    {
                        currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}/{part}";
                        groupPaths.Add(currentPath);
                    }
                }
            }

            // Convert to Group objects with proper hierarchy
            var groups = new List<Group>();
            var sortedPaths = groupPaths.OrderBy(p => p).ToList();

            for (int i = 0; i < sortedPaths.Count; i++)
            {
                var fullPath = sortedPaths[i];
                var lastSlash = fullPath.LastIndexOf('/');
                var parentPath = lastSlash > 0 ? fullPath.Substring(0, lastSlash) : null;
                var name = lastSlash > 0 ? fullPath.Substring(lastSlash + 1) : fullPath;

                // Calculate sort order (siblings get sequential order)
                var siblingCount = groups.Count(g => g.ParentPath == parentPath && g.Name.CompareTo(name) < 0);

                groups.Add(new Group
                {
                    FullPath = fullPath,
                    ParentPath = parentPath,
                    Name = name,
                    SortOrder = siblingCount,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            System.Diagnostics.Debug.WriteLine($"  - Extracted {groups.Count} unique groups from computer entries");
            return groups;
        }

        /// <summary>
        /// Insert groups into the database
        /// </summary>
        private void InsertGroups(SQLiteConnection connection, List<Group> groups)
        {
            const string sql = @"
                INSERT INTO Groups (FullPath, ParentPath, Name, SortOrder, CreatedAt, UpdatedAt)
                VALUES (@FullPath, @ParentPath, @Name, @SortOrder, @CreatedAt, @UpdatedAt)";

            foreach (var group in groups)
            {
                using var cmd = new SQLiteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@FullPath", group.FullPath);
                cmd.Parameters.AddWithValue("@ParentPath", (object?)group.ParentPath ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Name", group.Name);
                cmd.Parameters.AddWithValue("@SortOrder", group.SortOrder);
                cmd.Parameters.AddWithValue("@CreatedAt", group.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.Parameters.AddWithValue("@UpdatedAt", group.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Migrate from version 2 to version 3
        /// Adds session persistence fields to ConnectionLogs
        /// </summary>
        private void MigrateToVersion3()
        {
            System.Diagnostics.Debug.WriteLine("Migrating schema to version 3...");

            using var connection = _db.CreateConnection();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Add session info columns to ConnectionLogs
                System.Diagnostics.Debug.WriteLine("  - Adding session fields to ConnectionLogs table");
                ExecuteNonQuery(connection, "ALTER TABLE ConnectionLogs ADD COLUMN ServerAddress TEXT");
                ExecuteNonQuery(connection, "ALTER TABLE ConnectionLogs ADD COLUMN FriendlyName TEXT");
                ExecuteNonQuery(connection, "ALTER TABLE ConnectionLogs ADD COLUMN Domain TEXT");
                ExecuteNonQuery(connection, "ALTER TABLE ConnectionLogs ADD COLUMN GroupPath TEXT");

                // Update schema version
                System.Diagnostics.Debug.WriteLine("  - Updating schema version to 3");
                ExecuteNonQuery(connection, $@"
                    INSERT INTO SchemaVersion (Version, AppliedAt)
                    VALUES (3, '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}')");

                transaction.Commit();
                System.Diagnostics.Debug.WriteLine("Schema migration to version 3 completed successfully");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                System.Diagnostics.Debug.WriteLine($"Schema migration failed: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Migrate from version 3 to version 4
        /// Adds IsActiveSession flag to track open session panels
        /// </summary>
        private void MigrateToVersion4()
        {
            System.Diagnostics.Debug.WriteLine("Migrating schema to version 4...");

            using var connection = _db.CreateConnection();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Add IsActiveSession column
                System.Diagnostics.Debug.WriteLine("  - Adding IsActiveSession column to ConnectionLogs table");
                ExecuteNonQuery(connection, "ALTER TABLE ConnectionLogs ADD COLUMN IsActiveSession INTEGER DEFAULT 0");

                // Set IsActiveSession = 0 for all existing rows (DEFAULT only affects new rows)
                System.Diagnostics.Debug.WriteLine("  - Setting IsActiveSession = 0 for existing rows");
                ExecuteNonQuery(connection, "UPDATE ConnectionLogs SET IsActiveSession = 0 WHERE IsActiveSession IS NULL");

                // Update schema version
                System.Diagnostics.Debug.WriteLine("  - Updating schema version to 4");
                ExecuteNonQuery(connection, $@"
                    INSERT INTO SchemaVersion (Version, AppliedAt)
                    VALUES (4, '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}')");

                transaction.Commit();
                System.Diagnostics.Debug.WriteLine("Schema migration to version 4 completed successfully");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                System.Diagnostics.Debug.WriteLine($"Schema migration failed: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Migrate from version 4 to version 5
        /// Adds FileExplorerSessions table for embedded file explorer
        /// </summary>
        private void MigrateToVersion5()
        {
            System.Diagnostics.Debug.WriteLine("Migrating schema to version 5...");

            using var connection = _db.CreateConnection();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Create FileExplorerSessions table
                System.Diagnostics.Debug.WriteLine("  - Creating FileExplorerSessions table");
                ExecuteNonQuery(connection, @"
                    CREATE TABLE IF NOT EXISTS FileExplorerSessions (
                        SessionId TEXT PRIMARY KEY,
                        FolderPath TEXT NOT NULL,
                        DisplayName TEXT,
                        CreatedAt TEXT NOT NULL,
                        LastAccessedAt TEXT,
                        SortOrder INTEGER DEFAULT 0
                    )");

                // Create index for FileExplorerSessions
                System.Diagnostics.Debug.WriteLine("  - Creating index on FileExplorerSessions");
                ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS idx_file_explorer_sessions_last_accessed ON FileExplorerSessions(LastAccessedAt)");

                // Update schema version
                System.Diagnostics.Debug.WriteLine("  - Updating schema version to 5");
                ExecuteNonQuery(connection, $@"
                    INSERT INTO SchemaVersion (Version, AppliedAt)
                    VALUES (5, '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}')");

                transaction.Commit();
                System.Diagnostics.Debug.WriteLine("Schema migration to version 5 completed successfully");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                System.Diagnostics.Debug.WriteLine($"Schema migration failed: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Migrate from version 5 to version 6
        /// Adds KubernetesClusters table for Kubernetes pod management
        /// </summary>
        private void MigrateToVersion6()
        {
            System.Diagnostics.Debug.WriteLine("Migrating schema to version 6...");

            using var connection = _db.CreateConnection();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Create KubernetesClusters table
                System.Diagnostics.Debug.WriteLine("  - Creating KubernetesClusters table");
                ExecuteNonQuery(connection, @"
                    CREATE TABLE IF NOT EXISTS KubernetesClusters (
                        ClusterId TEXT PRIMARY KEY,
                        DisplayName TEXT NOT NULL,
                        ProjectId TEXT NOT NULL,
                        ClusterName TEXT NOT NULL,
                        Region TEXT NOT NULL,
                        DefaultNamespace TEXT NOT NULL DEFAULT 'default',
                        ProxyAddress TEXT,
                        ClearProxyBeforeAuth INTEGER DEFAULT 1,
                        CreatedAt TEXT NOT NULL,
                        SortOrder INTEGER DEFAULT 0
                    )");

                // Update schema version
                System.Diagnostics.Debug.WriteLine("  - Updating schema version to 6");
                ExecuteNonQuery(connection, $@"
                    INSERT INTO SchemaVersion (Version, AppliedAt)
                    VALUES (6, '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}')");

                transaction.Commit();
                System.Diagnostics.Debug.WriteLine("Schema migration to version 6 completed successfully");
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                System.Diagnostics.Debug.WriteLine($"Schema migration failed: {ex.Message}\n{ex.StackTrace}");
                throw;
            }
        }

        private void ExecuteNonQuery(SQLiteConnection connection, string sql)
        {
            using var cmd = new SQLiteCommand(sql, connection);
            cmd.ExecuteNonQuery();
        }
    }
}
