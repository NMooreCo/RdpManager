using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using RdpManager.Data.Models;

namespace RdpManager.Data.Repositories
{
    /// <summary>
    /// Repository for managing group hierarchy and ordering
    /// </summary>
    public class GroupRepository : BaseRepository
    {
        /// <summary>
        /// Get all groups ordered by parent path and sort order
        /// </summary>
        public List<Group> GetAll()
        {
            var sql = @"
                SELECT Id, FullPath, ParentPath, Name, SortOrder, CreatedAt, UpdatedAt
                FROM Groups
                ORDER BY ParentPath NULLS FIRST, SortOrder, Name";

            return ExecuteQuery(sql, MapGroup);
        }

        /// <summary>
        /// Get a group by its full path
        /// </summary>
        public Group? GetByPath(string fullPath)
        {
            var sql = @"
                SELECT Id, FullPath, ParentPath, Name, SortOrder, CreatedAt, UpdatedAt
                FROM Groups
                WHERE FullPath = @FullPath";

            return ExecuteQuerySingle(sql, MapGroup, new SQLiteParameter("@FullPath", fullPath));
        }

        /// <summary>
        /// Get immediate children of a parent group
        /// </summary>
        public List<Group> GetChildren(string? parentPath)
        {
            var sql = parentPath == null
                ? @"SELECT Id, FullPath, ParentPath, Name, SortOrder, CreatedAt, UpdatedAt
                    FROM Groups
                    WHERE ParentPath IS NULL
                    ORDER BY SortOrder, Name"
                : @"SELECT Id, FullPath, ParentPath, Name, SortOrder, CreatedAt, UpdatedAt
                    FROM Groups
                    WHERE ParentPath = @ParentPath
                    ORDER BY SortOrder, Name";

            return parentPath == null
                ? ExecuteQuery(sql, MapGroup)
                : ExecuteQuery(sql, MapGroup, new SQLiteParameter("@ParentPath", parentPath));
        }

        /// <summary>
        /// Insert a new group
        /// </summary>
        public long Insert(Group group)
        {
            var sql = @"
                INSERT INTO Groups (FullPath, ParentPath, Name, SortOrder, CreatedAt, UpdatedAt)
                VALUES (@FullPath, @ParentPath, @Name, @SortOrder, @CreatedAt, @UpdatedAt)";

            using var connection = Db.CreateConnection();
            using var cmd = new SQLiteCommand(sql, connection);

            cmd.Parameters.AddWithValue("@FullPath", group.FullPath);
            cmd.Parameters.AddWithValue("@ParentPath", (object?)group.ParentPath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@Name", group.Name);
            cmd.Parameters.AddWithValue("@SortOrder", group.SortOrder);
            cmd.Parameters.AddWithValue("@CreatedAt", group.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            cmd.Parameters.AddWithValue("@UpdatedAt", group.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"));

            cmd.ExecuteNonQuery();
            return GetLastInsertRowId(connection);
        }

        /// <summary>
        /// Update an existing group
        /// </summary>
        public void Update(Group group)
        {
            var sql = @"
                UPDATE Groups
                SET FullPath = @FullPath,
                    ParentPath = @ParentPath,
                    Name = @Name,
                    SortOrder = @SortOrder,
                    UpdatedAt = @UpdatedAt
                WHERE Id = @Id";

            ExecuteNonQuery(sql,
                new SQLiteParameter("@Id", group.Id),
                new SQLiteParameter("@FullPath", group.FullPath),
                new SQLiteParameter("@ParentPath", (object?)group.ParentPath ?? DBNull.Value),
                new SQLiteParameter("@Name", group.Name),
                new SQLiteParameter("@SortOrder", group.SortOrder),
                new SQLiteParameter("@UpdatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"))
            );
        }

        /// <summary>
        /// Delete a group (and all its children will cascade if needed)
        /// </summary>
        public void Delete(long id)
        {
            var sql = "DELETE FROM Groups WHERE Id = @Id";
            ExecuteNonQuery(sql, new SQLiteParameter("@Id", id));
        }

        /// <summary>
        /// Delete a group by path
        /// </summary>
        public void DeleteByPath(string fullPath)
        {
            var sql = "DELETE FROM Groups WHERE FullPath = @FullPath";
            ExecuteNonQuery(sql, new SQLiteParameter("@FullPath", fullPath));
        }

        /// <summary>
        /// Bulk update sort orders for multiple groups
        /// </summary>
        public void UpdateSortOrders(List<(long id, int sortOrder)> updates)
        {
            using var connection = Db.CreateConnection();
            using var transaction = connection.BeginTransaction();

            try
            {
                var sql = "UPDATE Groups SET SortOrder = @SortOrder, UpdatedAt = @UpdatedAt WHERE Id = @Id";

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
        /// Move a group to a new parent (updates all child group paths recursively)
        /// </summary>
        public void MoveGroup(string oldPath, string newParentPath, int newSortOrder)
        {
            using var connection = Db.CreateConnection();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Get the group being moved
                var group = GetByPath(oldPath);
                if (group == null) return;

                // Calculate new path
                var groupName = group.Name;
                var newFullPath = string.IsNullOrEmpty(newParentPath)
                    ? groupName
                    : $"{newParentPath}/{groupName}";

                // Update the group's path and parent
                var updateSql = @"
                    UPDATE Groups
                    SET FullPath = @NewPath,
                        ParentPath = @NewParent,
                        SortOrder = @SortOrder,
                        UpdatedAt = @UpdatedAt
                    WHERE FullPath = @OldPath";

                using (var cmd = new SQLiteCommand(updateSql, connection))
                {
                    cmd.Parameters.AddWithValue("@NewPath", newFullPath);
                    cmd.Parameters.AddWithValue("@NewParent", (object?)newParentPath ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@SortOrder", newSortOrder);
                    cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                    cmd.Parameters.AddWithValue("@OldPath", oldPath);
                    cmd.ExecuteNonQuery();
                }

                // Update all child groups (recursively update their paths)
                var childrenSql = "SELECT FullPath FROM Groups WHERE FullPath LIKE @Pattern";
                var pattern = $"{oldPath}/%";

                using var childCmd = new SQLiteCommand(childrenSql, connection);
                childCmd.Parameters.AddWithValue("@Pattern", pattern);

                var childPaths = new List<string>();
                using (var reader = childCmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        childPaths.Add(reader.GetString(0));
                    }
                }

                // Update each child's path
                foreach (var childPath in childPaths)
                {
                    var relativePath = childPath.Substring(oldPath.Length + 1); // +1 for the "/"
                    var newChildPath = $"{newFullPath}/{relativePath}";

                    // Determine new parent path
                    var lastSlash = newChildPath.LastIndexOf('/');
                    var newChildParent = lastSlash > 0 ? newChildPath.Substring(0, lastSlash) : null;

                    var updateChildSql = @"
                        UPDATE Groups
                        SET FullPath = @NewPath,
                            ParentPath = @NewParent,
                            UpdatedAt = @UpdatedAt
                        WHERE FullPath = @OldPath";

                    using var updateCmd = new SQLiteCommand(updateChildSql, connection);
                    updateCmd.Parameters.AddWithValue("@NewPath", newChildPath);
                    updateCmd.Parameters.AddWithValue("@NewParent", (object?)newChildParent ?? DBNull.Value);
                    updateCmd.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));
                    updateCmd.Parameters.AddWithValue("@OldPath", childPath);
                    updateCmd.ExecuteNonQuery();
                }

                // Also need to update computers in this group and all child groups
                var computerRepo = new ComputerRepository();
                computerRepo.UpdateGroupPaths(oldPath, newFullPath, connection);

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        /// <summary>
        /// Map database reader to Group object
        /// </summary>
        private Group MapGroup(IDataReader reader)
        {
            return new Group
            {
                Id = reader.GetInt64(0),
                FullPath = reader.GetString(1),
                ParentPath = reader.IsDBNull(2) ? null : reader.GetString(2),
                Name = reader.GetString(3),
                SortOrder = reader.GetInt32(4),
                CreatedAt = DateTime.Parse(reader.GetString(5)),
                UpdatedAt = DateTime.Parse(reader.GetString(6))
            };
        }
    }
}
