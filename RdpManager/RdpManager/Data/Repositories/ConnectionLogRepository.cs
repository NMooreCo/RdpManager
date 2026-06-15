using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using RdpManager.Data.Models;

namespace RdpManager.Data.Repositories
{
    /// <summary>
    /// Repository for managing connection logs
    /// </summary>
    public class ConnectionLogRepository : BaseRepository
    {
        /// <summary>
        /// Start a new connection log entry with session details
        /// </summary>
        public long StartConnection(long computerId, string connectionType, string serverAddress, string? friendlyName, string? domain, string? groupPath)
        {
            const string sql = @"
                INSERT INTO ConnectionLogs (ComputerId, ServerAddress, FriendlyName, Domain, GroupPath, StartTime, ConnectionType, Success, IsActiveSession)
                VALUES (@ComputerId, @ServerAddress, @FriendlyName, @Domain, @GroupPath, @StartTime, @ConnectionType, 1, 1)";

            using var connection = Db.CreateConnection();
            using var cmd = new SQLiteCommand(sql, connection);

            cmd.Parameters.AddWithValue("@ComputerId", computerId);
            cmd.Parameters.AddWithValue("@ServerAddress", serverAddress);
            cmd.Parameters.AddWithValue("@FriendlyName", friendlyName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Domain", domain ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@GroupPath", groupPath ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@StartTime", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("@ConnectionType", connectionType);

            cmd.ExecuteNonQuery();
            var logId = GetLastInsertRowId(connection);

            System.Diagnostics.Debug.WriteLine($"Created active session log #{logId} for {friendlyName ?? serverAddress}");

            return logId;
        }

        /// <summary>
        /// End a connection log entry
        /// </summary>
        public void EndConnection(long logId, bool success, string? disconnectReason, int? errorCode)
        {
            const string sql = @"
                UPDATE ConnectionLogs
                SET EndTime = @EndTime,
                    DurationSeconds = (julianday(@EndTime) - julianday(StartTime)) * 86400,
                    Success = @Success,
                    DisconnectReason = @DisconnectReason,
                    ErrorCode = @ErrorCode
                WHERE Id = @Id";

            ExecuteNonQuery(sql,
                new SQLiteParameter("@EndTime", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")),
                new SQLiteParameter("@Success", success ? 1 : 0),
                new SQLiteParameter("@DisconnectReason", disconnectReason ?? (object)DBNull.Value),
                new SQLiteParameter("@ErrorCode", errorCode ?? (object)DBNull.Value),
                new SQLiteParameter("@Id", logId));
        }

        /// <summary>
        /// Get active sessions (IsActiveSession = 1) for session restoration
        /// </summary>
        public List<ConnectionLog> GetActiveSessions()
        {
            const string sql = @"
                SELECT Id, ComputerId, ServerAddress, FriendlyName, Domain, GroupPath,
                       StartTime, EndTime, DurationSeconds,
                       ConnectionType, Success, DisconnectReason, ErrorCode, Notes, IsActiveSession
                FROM ConnectionLogs
                WHERE IsActiveSession = 1
                ORDER BY StartTime DESC";

            return ExecuteQuery(sql, MapFromReader);
        }

        /// <summary>
        /// Mark a session as closed (removes from Active Sessions view)
        /// </summary>
        public void CloseSession(long logId)
        {
            const string sql = @"
                UPDATE ConnectionLogs
                SET IsActiveSession = 0
                WHERE Id = @Id";

            ExecuteNonQuery(sql, new SQLiteParameter("@Id", logId));
        }

        /// <summary>
        /// Get recent connections (last N entries)
        /// </summary>
        public List<ConnectionLog> GetRecent(int limit = 50)
        {
            const string sql = @"
                SELECT Id, ComputerId, ServerAddress, FriendlyName, Domain, GroupPath,
                       StartTime, EndTime, DurationSeconds,
                       ConnectionType, Success, DisconnectReason, ErrorCode, Notes, IsActiveSession
                FROM ConnectionLogs
                ORDER BY StartTime DESC
                LIMIT @Limit";

            return ExecuteQuery(sql, MapFromReader, new SQLiteParameter("@Limit", limit));
        }

        /// <summary>
        /// Get connection logs for a specific computer
        /// </summary>
        public List<ConnectionLog> GetByComputer(long computerId, int limit = 50)
        {
            const string sql = @"
                SELECT Id, ComputerId, ServerAddress, FriendlyName, Domain, GroupPath,
                       StartTime, EndTime, DurationSeconds,
                       ConnectionType, Success, DisconnectReason, ErrorCode, Notes, IsActiveSession
                FROM ConnectionLogs
                WHERE ComputerId = @ComputerId
                ORDER BY StartTime DESC
                LIMIT @Limit";

            return ExecuteQuery(sql, MapFromReader,
                new SQLiteParameter("@ComputerId", computerId),
                new SQLiteParameter("@Limit", limit));
        }

        /// <summary>
        /// Get connection statistics for a computer
        /// </summary>
        public ConnectionStatistics GetStatistics(long computerId)
        {
            const string sql = @"
                SELECT
                    COUNT(*) as TotalConnections,
                    SUM(CASE WHEN Success = 1 THEN 1 ELSE 0 END) as SuccessfulConnections,
                    AVG(DurationSeconds) as AverageDurationSeconds,
                    MAX(StartTime) as LastConnectionTime
                FROM ConnectionLogs
                WHERE ComputerId = @ComputerId";

            using var connection = Db.CreateConnection();
            using var cmd = new SQLiteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@ComputerId", computerId);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return new ConnectionStatistics
                {
                    TotalConnections = reader.GetInt32(0),
                    SuccessfulConnections = reader.GetInt32(1),
                    AverageDurationSeconds = reader.IsDBNull(2) ? 0 : reader.GetDouble(2),
                    LastConnectionTime = GetNullableDateTime(reader, "LastConnectionTime")
                };
            }

            return new ConnectionStatistics();
        }

        /// <summary>
        /// Map database row to ConnectionLog object
        /// </summary>
        private ConnectionLog MapFromReader(IDataReader reader)
        {
            return new ConnectionLog
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")),
                ComputerId = reader.GetInt64(reader.GetOrdinal("ComputerId")),
                ServerAddress = GetString(reader, "ServerAddress") ?? string.Empty,
                FriendlyName = GetString(reader, "FriendlyName"),
                Domain = GetString(reader, "Domain"),
                GroupPath = GetString(reader, "GroupPath"),
                StartTime = DateTime.Parse(reader.GetString(reader.GetOrdinal("StartTime"))),
                EndTime = GetNullableDateTime(reader, "EndTime"),
                DurationSeconds = GetNullableInt(reader, "DurationSeconds"),
                ConnectionType = reader.GetString(reader.GetOrdinal("ConnectionType")),
                Success = GetBool(reader, "Success"),
                DisconnectReason = GetString(reader, "DisconnectReason"),
                ErrorCode = GetNullableInt(reader, "ErrorCode"),
                Notes = GetString(reader, "Notes"),
                IsActiveSession = GetBool(reader, "IsActiveSession")
            };
        }
    }

    /// <summary>
    /// Connection statistics for a computer
    /// </summary>
    public class ConnectionStatistics
    {
        public int TotalConnections { get; set; }
        public int SuccessfulConnections { get; set; }
        public double AverageDurationSeconds { get; set; }
        public DateTime? LastConnectionTime { get; set; }

        public double SuccessRate => TotalConnections > 0 ? (double)SuccessfulConnections / TotalConnections * 100 : 0;
    }
}
