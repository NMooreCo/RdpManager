using System;
using System.Data.SQLite;
using System.IO;

namespace RdpManager.Data.Database
{
    /// <summary>
    /// Manages SQLite database connection for RDP Manager
    /// </summary>
    public class DatabaseConnection
    {
        private static DatabaseConnection? _instance;
        private readonly string _connectionString;
        private readonly string _databasePath;

        /// <summary>
        /// Get the singleton instance of DatabaseConnection
        /// </summary>
        public static DatabaseConnection Instance
        {
            get
            {
                _instance ??= new DatabaseConnection();
                return _instance;
            }
        }

        private DatabaseConnection()
        {
            // Store database in the same directory as the executable
            var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _databasePath = Path.Combine(appDirectory, "rdpmanager.db");

            _connectionString = $"Data Source={_databasePath};Version=3;";
        }

        /// <summary>
        /// Get the path to the database file
        /// </summary>
        public string DatabasePath => _databasePath;

        /// <summary>
        /// Check if the database file exists
        /// </summary>
        public bool DatabaseExists => File.Exists(_databasePath);

        /// <summary>
        /// Create a new SQLite connection
        /// </summary>
        public SQLiteConnection CreateConnection()
        {
            var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            // Enable foreign keys
            using (var cmd = new SQLiteCommand("PRAGMA foreign_keys = ON;", connection))
            {
                cmd.ExecuteNonQuery();
            }

            return connection;
        }

        /// <summary>
        /// Execute a non-query SQL command (INSERT, UPDATE, DELETE)
        /// </summary>
        public int ExecuteNonQuery(string sql, params SQLiteParameter[] parameters)
        {
            using var connection = CreateConnection();
            using var cmd = new SQLiteCommand(sql, connection);
            cmd.Parameters.AddRange(parameters);
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Execute a scalar SQL command (returns single value)
        /// </summary>
        public object? ExecuteScalar(string sql, params SQLiteParameter[] parameters)
        {
            using var connection = CreateConnection();
            using var cmd = new SQLiteCommand(sql, connection);
            cmd.Parameters.AddRange(parameters);
            return cmd.ExecuteScalar();
        }
    }
}
