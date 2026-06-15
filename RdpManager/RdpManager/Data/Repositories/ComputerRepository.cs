using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using RdpManager.Models;

namespace RdpManager.Data.Repositories
{
    /// <summary>
    /// Repository for managing computer entries in the database
    /// </summary>
    public class ComputerRepository : BaseRepository
    {
        /// <summary>
        /// Get all computer entries ordered by group path and sort order
        /// </summary>
        public List<ComputerEntry> GetAll()
        {
            const string sql = @"
                SELECT Id, MachineName, FriendlyName, Domain, GroupPath, IsFavorite,
                       SortOrder, CreatedAt, UpdatedAt, LastConnectedAt, LastDisconnectErrorCode
                FROM Computers
                ORDER BY GroupPath, SortOrder, FriendlyName";

            return ExecuteQuery(sql, MapFromReader);
        }

        /// <summary>
        /// Get a computer by ID
        /// </summary>
        public ComputerEntry? GetById(long id)
        {
            const string sql = @"
                SELECT Id, MachineName, FriendlyName, Domain, GroupPath, IsFavorite,
                       SortOrder, CreatedAt, UpdatedAt, LastConnectedAt, LastDisconnectErrorCode
                FROM Computers
                WHERE Id = @Id";

            return ExecuteQuerySingle(sql, MapFromReader, new SQLiteParameter("@Id", id));
        }

        /// <summary>
        /// Get favorites ordered by their sort order
        /// </summary>
        public List<ComputerEntry> GetFavorites()
        {
            const string sql = @"
                SELECT Id, MachineName, FriendlyName, Domain, GroupPath, IsFavorite,
                       SortOrder, CreatedAt, UpdatedAt, LastConnectedAt, LastDisconnectErrorCode
                FROM Computers
                WHERE IsFavorite = 1
                ORDER BY GroupPath, SortOrder, FriendlyName";

            return ExecuteQuery(sql, MapFromReader);
        }

        /// <summary>
        /// Insert a new computer entry
        /// </summary>
        public long Insert(ComputerEntry computer)
        {
            const string sql = @"
                INSERT INTO Computers (MachineName, FriendlyName, Domain, GroupPath, IsFavorite, SortOrder, CreatedAt, UpdatedAt)
                VALUES (@MachineName, @FriendlyName, @Domain, @GroupPath, @IsFavorite, @SortOrder, @CreatedAt, @UpdatedAt)";

            using var connection = Db.CreateConnection();
            using var cmd = new SQLiteCommand(sql, connection);

            cmd.Parameters.AddWithValue("@MachineName", computer.MachineName);
            cmd.Parameters.AddWithValue("@FriendlyName", computer.FriendlyName);
            cmd.Parameters.AddWithValue("@Domain", computer.Domain ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@GroupPath", computer.Group ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@IsFavorite", computer.IsFavorite ? 1 : 0);
            cmd.Parameters.AddWithValue("@SortOrder", computer.SortOrder);
            cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

            cmd.ExecuteNonQuery();
            return GetLastInsertRowId(connection);
        }

        /// <summary>
        /// Update an existing computer entry
        /// </summary>
        public void Update(ComputerEntry computer)
        {
            const string sql = @"
                UPDATE Computers
                SET MachineName = @MachineName,
                    FriendlyName = @FriendlyName,
                    Domain = @Domain,
                    GroupPath = @GroupPath,
                    IsFavorite = @IsFavorite,
                    SortOrder = @SortOrder,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id";

            ExecuteNonQuery(sql,
                new SQLiteParameter("@MachineName", computer.MachineName),
                new SQLiteParameter("@FriendlyName", computer.FriendlyName),
                new SQLiteParameter("@Domain", computer.Domain ?? (object)DBNull.Value),
                new SQLiteParameter("@GroupPath", computer.Group ?? (object)DBNull.Value),
                new SQLiteParameter("@IsFavorite", computer.IsFavorite ? 1 : 0),
                new SQLiteParameter("@SortOrder", computer.SortOrder),
                new SQLiteParameter("@UpdatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")),
                new SQLiteParameter("@Id", computer.Id));
        }

        /// <summary>
        /// Delete a computer entry
        /// </summary>
        public void Delete(long id)
        {
            const string sql = "DELETE FROM Computers WHERE Id = @Id";
            ExecuteNonQuery(sql, new SQLiteParameter("@Id", id));
        }

        /// <summary>
        /// Update last connected timestamp and error code
        /// </summary>
        public void UpdateLastConnection(long id, int? errorCode)
        {
            const string sql = @"
                UPDATE Computers
                SET LastConnectedAt = @LastConnectedAt,
                    LastDisconnectErrorCode = @ErrorCode
                WHERE Id = @Id";

            ExecuteNonQuery(sql,
                new SQLiteParameter("@LastConnectedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")),
                new SQLiteParameter("@ErrorCode", errorCode ?? (object)DBNull.Value),
                new SQLiteParameter("@Id", id));
        }

        /// <summary>
        /// Toggle favorite status
        /// </summary>
        public void ToggleFavorite(long id)
        {
            const string sql = @"
                UPDATE Computers
                SET IsFavorite = NOT IsFavorite,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id";

            ExecuteNonQuery(sql,
                new SQLiteParameter("@UpdatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")),
                new SQLiteParameter("@Id", id));
        }

        /// <summary>
        /// Bulk update sort orders for multiple computers
        /// </summary>
        public void UpdateSortOrders(List<(long id, int sortOrder)> updates)
        {
            using var connection = Db.CreateConnection();
            using var transaction = connection.BeginTransaction();

            try
            {
                const string sql = "UPDATE Computers SET SortOrder = @SortOrder, UpdatedAt = @UpdatedAt WHERE Id = @Id";

                foreach (var (id, sortOrder) in updates)
                {
                    using var cmd = new SQLiteCommand(sql, connection);
                    cmd.Parameters.AddWithValue("@Id", id);
                    cmd.Parameters.AddWithValue("@SortOrder", sortOrder);
                    cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Update group paths for all computers when a group is moved
        /// Called from GroupRepository.MoveGroup with existing connection/transaction
        /// </summary>
        public void UpdateGroupPaths(string oldGroupPath, string newGroupPath, SQLiteConnection connection)
        {
            // Update computers in the moved group
            var updateSql = @"
                UPDATE Computers
                SET GroupPath = @NewGroupPath,
                    UpdatedAt = @UpdatedAt
                WHERE GroupPath = @OldGroupPath";

            using var cmd = new SQLiteCommand(updateSql, connection);
            cmd.Parameters.AddWithValue("@NewGroupPath", newGroupPath);
            cmd.Parameters.AddWithValue("@OldGroupPath", oldGroupPath);
            cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.ExecuteNonQuery();

            // Update computers in all child groups
            var childSql = @"
                UPDATE Computers
                SET GroupPath = @NewGroupPath || SUBSTR(GroupPath, @OldLength + 1),
                    UpdatedAt = @UpdatedAt
                WHERE GroupPath LIKE @Pattern";

            using var childCmd = new SQLiteCommand(childSql, connection);
            var pattern = $"{oldGroupPath}/%";
            childCmd.Parameters.AddWithValue("@NewGroupPath", newGroupPath);
            childCmd.Parameters.AddWithValue("@OldLength", oldGroupPath.Length);
            childCmd.Parameters.AddWithValue("@Pattern", pattern);
            childCmd.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
            childCmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Move a computer to a different group and position
        /// </summary>
        public void MoveToGroup(long computerId, string newGroupPath, int newSortOrder)
        {
            const string sql = @"
                UPDATE Computers
                SET GroupPath = @GroupPath,
                    SortOrder = @SortOrder,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id";

            ExecuteNonQuery(sql,
                new SQLiteParameter("@GroupPath", newGroupPath ?? (object)DBNull.Value),
                new SQLiteParameter("@SortOrder", newSortOrder),
                new SQLiteParameter("@UpdatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")),
                new SQLiteParameter("@Id", computerId));
        }

        /// <summary>
        /// Map database row to ComputerEntry object
        /// </summary>
        private ComputerEntry MapFromReader(IDataReader reader)
        {
            return new ComputerEntry
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")),
                MachineName = reader.GetString(reader.GetOrdinal("MachineName")),
                FriendlyName = reader.GetString(reader.GetOrdinal("FriendlyName")),
                Domain = GetString(reader, "Domain") ?? string.Empty,
                Group = GetString(reader, "GroupPath") ?? string.Empty,
                IsFavorite = GetBool(reader, "IsFavorite"),
                SortOrder = reader.GetInt32(reader.GetOrdinal("SortOrder")),
                CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")), null, System.Globalization.DateTimeStyles.AssumeUniversal).ToLocalTime(),
                UpdatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("UpdatedAt")), null, System.Globalization.DateTimeStyles.AssumeUniversal).ToLocalTime(),
                LastConnectedAt = GetNullableDateTime(reader, "LastConnectedAt"),
                LastDisconnectErrorCode = GetNullableInt(reader, "LastDisconnectErrorCode")
            };
        }
    }
}
