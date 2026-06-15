using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using RdpManager.Data.Models;

namespace RdpManager.Data.Repositories
{
    /// <summary>
    /// Repository for managing tags and computer-tag associations
    /// </summary>
    public class TagRepository : BaseRepository
    {
        /// <summary>
        /// Get all tags
        /// </summary>
        public List<Tag> GetAll()
        {
            const string sql = @"
                SELECT Id, Name, Color, CreatedAt
                FROM Tags
                ORDER BY Name";

            return ExecuteQuery(sql, MapFromReader);
        }

        /// <summary>
        /// Get tags for a specific computer
        /// </summary>
        public List<Tag> GetByComputer(long computerId)
        {
            const string sql = @"
                SELECT t.Id, t.Name, t.Color, t.CreatedAt
                FROM Tags t
                INNER JOIN ComputerTags ct ON t.Id = ct.TagId
                WHERE ct.ComputerId = @ComputerId
                ORDER BY t.Name";

            return ExecuteQuery(sql, MapFromReader, new SQLiteParameter("@ComputerId", computerId));
        }

        /// <summary>
        /// Create a new tag
        /// </summary>
        public long Insert(Tag tag)
        {
            const string sql = @"
                INSERT INTO Tags (Name, Color, CreatedAt)
                VALUES (@Name, @Color, @CreatedAt)";

            using var connection = Db.CreateConnection();
            using var cmd = new SQLiteCommand(sql, connection);

            cmd.Parameters.AddWithValue("@Name", tag.Name);
            cmd.Parameters.AddWithValue("@Color", tag.Color);
            cmd.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"));

            cmd.ExecuteNonQuery();
            return GetLastInsertRowId(connection);
        }

        /// <summary>
        /// Update a tag
        /// </summary>
        public void Update(Tag tag)
        {
            const string sql = @"
                UPDATE Tags
                SET Name = @Name, Color = @Color
                WHERE Id = @Id";

            ExecuteNonQuery(sql,
                new SQLiteParameter("@Name", tag.Name),
                new SQLiteParameter("@Color", tag.Color),
                new SQLiteParameter("@Id", tag.Id));
        }

        /// <summary>
        /// Delete a tag
        /// </summary>
        public void Delete(long id)
        {
            const string sql = "DELETE FROM Tags WHERE Id = @Id";
            ExecuteNonQuery(sql, new SQLiteParameter("@Id", id));
        }

        /// <summary>
        /// Add a tag to a computer
        /// </summary>
        public void AddTagToComputer(long computerId, long tagId)
        {
            const string sql = @"
                INSERT OR IGNORE INTO ComputerTags (ComputerId, TagId)
                VALUES (@ComputerId, @TagId)";

            ExecuteNonQuery(sql,
                new SQLiteParameter("@ComputerId", computerId),
                new SQLiteParameter("@TagId", tagId));
        }

        /// <summary>
        /// Remove a tag from a computer
        /// </summary>
        public void RemoveTagFromComputer(long computerId, long tagId)
        {
            const string sql = @"
                DELETE FROM ComputerTags
                WHERE ComputerId = @ComputerId AND TagId = @TagId";

            ExecuteNonQuery(sql,
                new SQLiteParameter("@ComputerId", computerId),
                new SQLiteParameter("@TagId", tagId));
        }

        /// <summary>
        /// Get all computers with a specific tag
        /// </summary>
        public List<long> GetComputerIdsByTag(long tagId)
        {
            const string sql = @"
                SELECT ComputerId
                FROM ComputerTags
                WHERE TagId = @TagId";

            var ids = new List<long>();
            using var connection = Db.CreateConnection();
            using var cmd = new SQLiteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@TagId", tagId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                ids.Add(reader.GetInt64(0));
            }

            return ids;
        }

        /// <summary>
        /// Map database row to Tag object
        /// </summary>
        private Tag MapFromReader(IDataReader reader)
        {
            return new Tag
            {
                Id = reader.GetInt64(reader.GetOrdinal("Id")),
                Name = reader.GetString(reader.GetOrdinal("Name")),
                Color = reader.GetString(reader.GetOrdinal("Color")),
                CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("CreatedAt")))
            };
        }
    }
}
