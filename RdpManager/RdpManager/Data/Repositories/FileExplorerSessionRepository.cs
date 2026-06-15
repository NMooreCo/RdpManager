using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using RdpManager.Models;

namespace RdpManager.Data.Repositories
{
    /// <summary>
    /// Repository for managing File Explorer sessions
    /// </summary>
    public class FileExplorerSessionRepository : BaseRepository
    {
        /// <summary>
        /// Get all File Explorer sessions ordered by last accessed time
        /// </summary>
        public List<FileExplorerSession> GetAll()
        {
            const string sql = @"
                SELECT SessionId, FolderPath, DisplayName, CreatedAt, LastAccessedAt, SortOrder
                FROM FileExplorerSessions
                ORDER BY SortOrder, LastAccessedAt DESC";

            return ExecuteQuery(sql, MapFromReader);
        }

        /// <summary>
        /// Get a File Explorer session by ID
        /// </summary>
        public FileExplorerSession? GetById(Guid sessionId)
        {
            const string sql = @"
                SELECT SessionId, FolderPath, DisplayName, CreatedAt, LastAccessedAt, SortOrder
                FROM FileExplorerSessions
                WHERE SessionId = @SessionId";

            return ExecuteQuerySingle(sql, MapFromReader, new SQLiteParameter("@SessionId", sessionId.ToString()));
        }

        /// <summary>
        /// Insert a new File Explorer session
        /// </summary>
        public void Insert(FileExplorerSession session)
        {
            const string sql = @"
                INSERT INTO FileExplorerSessions (SessionId, FolderPath, DisplayName, CreatedAt, LastAccessedAt, SortOrder)
                VALUES (@SessionId, @FolderPath, @DisplayName, @CreatedAt, @LastAccessedAt, @SortOrder)";

            ExecuteNonQuery(sql,
                new SQLiteParameter("@SessionId", session.SessionId.ToString()),
                new SQLiteParameter("@FolderPath", session.FolderPath),
                new SQLiteParameter("@DisplayName", session.DisplayName ?? (object)DBNull.Value),
                new SQLiteParameter("@CreatedAt", session.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")),
                new SQLiteParameter("@LastAccessedAt", session.LastAccessedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)DBNull.Value),
                new SQLiteParameter("@SortOrder", session.SortOrder));
        }

        /// <summary>
        /// Update an existing File Explorer session
        /// </summary>
        public void Update(FileExplorerSession session)
        {
            const string sql = @"
                UPDATE FileExplorerSessions
                SET FolderPath = @FolderPath,
                    DisplayName = @DisplayName,
                    LastAccessedAt = @LastAccessedAt,
                    SortOrder = @SortOrder
                WHERE SessionId = @SessionId";

            ExecuteNonQuery(sql,
                new SQLiteParameter("@SessionId", session.SessionId.ToString()),
                new SQLiteParameter("@FolderPath", session.FolderPath),
                new SQLiteParameter("@DisplayName", session.DisplayName ?? (object)DBNull.Value),
                new SQLiteParameter("@LastAccessedAt", session.LastAccessedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)DBNull.Value),
                new SQLiteParameter("@SortOrder", session.SortOrder));
        }

        /// <summary>
        /// Update the last accessed time for a session
        /// </summary>
        public void UpdateLastAccessed(Guid sessionId)
        {
            const string sql = @"
                UPDATE FileExplorerSessions
                SET LastAccessedAt = @LastAccessedAt
                WHERE SessionId = @SessionId";

            ExecuteNonQuery(sql,
                new SQLiteParameter("@SessionId", sessionId.ToString()),
                new SQLiteParameter("@LastAccessedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")));
        }

        /// <summary>
        /// Delete a File Explorer session
        /// </summary>
        public void Delete(Guid sessionId)
        {
            const string sql = "DELETE FROM FileExplorerSessions WHERE SessionId = @SessionId";
            ExecuteNonQuery(sql, new SQLiteParameter("@SessionId", sessionId.ToString()));
        }

        /// <summary>
        /// Delete all File Explorer sessions
        /// </summary>
        public void DeleteAll()
        {
            const string sql = "DELETE FROM FileExplorerSessions";
            ExecuteNonQuery(sql);
        }

        /// <summary>
        /// Map a data reader row to a FileExplorerSession object
        /// </summary>
        private FileExplorerSession MapFromReader(IDataReader reader)
        {
            return new FileExplorerSession
            {
                SessionId = Guid.Parse(reader.GetString(reader.GetOrdinal("SessionId"))),
                FolderPath = reader.GetString(reader.GetOrdinal("FolderPath")),
                DisplayName = GetString(reader, "DisplayName"),
                CreatedAt = DateTime.Parse(GetString(reader, "CreatedAt") ?? DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")),
                LastAccessedAt = GetNullableDateTime(reader, "LastAccessedAt"),
                SortOrder = reader.GetInt32(reader.GetOrdinal("SortOrder"))
            };
        }
    }
}
