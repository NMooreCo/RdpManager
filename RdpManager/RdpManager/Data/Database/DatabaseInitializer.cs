using System;
using System.Data.SQLite;

namespace RdpManager.Data.Database
{
    /// <summary>
    /// Initializes and manages database schema
    /// </summary>
    public class DatabaseInitializer
    {
        private readonly DatabaseConnection _db;

        public DatabaseInitializer()
        {
            _db = DatabaseConnection.Instance;
        }

        /// <summary>
        /// Initialize the database schema if it doesn't exist
        /// </summary>
        public void Initialize()
        {
            if (!_db.DatabaseExists)
            {
                CreateSchema();
            }
        }

        /// <summary>
        /// Create the complete database schema
        /// </summary>
        private void CreateSchema()
        {
            using var connection = _db.CreateConnection();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Create Computers table
                ExecuteNonQuery(connection, @"
                    CREATE TABLE IF NOT EXISTS Computers (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        FriendlyName TEXT NOT NULL,
                        MachineName TEXT NOT NULL,
                        Domain TEXT,
                        GroupPath TEXT,
                        IsFavorite INTEGER DEFAULT 0,
                        SortOrder INTEGER DEFAULT 0,
                        CreatedAt TEXT NOT NULL,
                        UpdatedAt TEXT NOT NULL,
                        LastConnectedAt TEXT,
                        LastDisconnectErrorCode INTEGER
                    )");

                // Create indices for Computers
                ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS idx_computers_machine ON Computers(MachineName)");
                ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS idx_computers_favorite ON Computers(IsFavorite)");
                ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS idx_computers_group ON Computers(GroupPath)");

                // Create ComputerRdpSettings table
                ExecuteNonQuery(connection, @"
                    CREATE TABLE IF NOT EXISTS ComputerRdpSettings (
                        ComputerId INTEGER PRIMARY KEY,
                        ScreenMode INTEGER DEFAULT 2,
                        ColorDepth INTEGER DEFAULT 32,
                        AudioMode INTEGER DEFAULT 0,
                        EnableClipboard INTEGER DEFAULT 1,
                        EnableDrives INTEGER DEFAULT 0,
                        CustomResolution TEXT,
                        EnableCompression INTEGER DEFAULT 1,
                        EnableDesktopComposition INTEGER DEFAULT 1,
                        EnableFontSmoothing INTEGER DEFAULT 1,
                        FOREIGN KEY (ComputerId) REFERENCES Computers(Id) ON DELETE CASCADE
                    )");

                // Create Tags table
                ExecuteNonQuery(connection, @"
                    CREATE TABLE IF NOT EXISTS Tags (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        Name TEXT NOT NULL UNIQUE,
                        Color TEXT DEFAULT '#9CA3AF',
                        CreatedAt TEXT NOT NULL
                    )");

                // Create ComputerTags junction table
                ExecuteNonQuery(connection, @"
                    CREATE TABLE IF NOT EXISTS ComputerTags (
                        ComputerId INTEGER NOT NULL,
                        TagId INTEGER NOT NULL,
                        PRIMARY KEY (ComputerId, TagId),
                        FOREIGN KEY (ComputerId) REFERENCES Computers(Id) ON DELETE CASCADE,
                        FOREIGN KEY (TagId) REFERENCES Tags(Id) ON DELETE CASCADE
                    )");

                // Create indices for ComputerTags
                ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS idx_computer_tags_computer ON ComputerTags(ComputerId)");
                ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS idx_computer_tags_tag ON ComputerTags(TagId)");

                // Create Groups table
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

                // Create indices for Groups
                ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS idx_groups_fullpath ON Groups(FullPath)");
                ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS idx_groups_parent ON Groups(ParentPath)");

                // Create ConnectionLogs table
                ExecuteNonQuery(connection, @"
                    CREATE TABLE IF NOT EXISTS ConnectionLogs (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ComputerId INTEGER NOT NULL,
                        ServerAddress TEXT NOT NULL,
                        FriendlyName TEXT,
                        Domain TEXT,
                        GroupPath TEXT,
                        StartTime TEXT NOT NULL,
                        EndTime TEXT,
                        DurationSeconds INTEGER,
                        ConnectionType TEXT DEFAULT 'Embedded',
                        Success INTEGER DEFAULT 1,
                        DisconnectReason TEXT,
                        ErrorCode INTEGER,
                        Notes TEXT,
                        IsActiveSession INTEGER DEFAULT 1,
                        FOREIGN KEY (ComputerId) REFERENCES Computers(Id) ON DELETE CASCADE
                    )");

                // Create indices for ConnectionLogs
                ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS idx_connection_logs_computer ON ConnectionLogs(ComputerId)");
                ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS idx_connection_logs_start_time ON ConnectionLogs(StartTime)");

                // Create Preferences table
                ExecuteNonQuery(connection, @"
                    CREATE TABLE IF NOT EXISTS Preferences (
                        Key TEXT PRIMARY KEY,
                        Value TEXT NOT NULL,
                        Type TEXT NOT NULL
                    )");

                // Create FileExplorerSessions table
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
                ExecuteNonQuery(connection, "CREATE INDEX IF NOT EXISTS idx_file_explorer_sessions_last_accessed ON FileExplorerSessions(LastAccessedAt)");

                // Create KubernetesClusters table
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

                // Create schema version table
                ExecuteNonQuery(connection, @"
                    CREATE TABLE IF NOT EXISTS SchemaVersion (
                        Version INTEGER PRIMARY KEY,
                        AppliedAt TEXT NOT NULL
                    )");

                // Insert initial schema version (version 6 includes Kubernetes clusters)
                ExecuteNonQuery(connection, $@"
                    INSERT INTO SchemaVersion (Version, AppliedAt)
                    VALUES (6, '{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}')");

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
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
