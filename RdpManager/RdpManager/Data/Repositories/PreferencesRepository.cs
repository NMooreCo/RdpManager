using System;
using System.Data.SQLite;

namespace RdpManager.Data.Repositories
{
    /// <summary>
    /// Repository for managing application preferences
    /// </summary>
    public class PreferencesRepository : BaseRepository
    {
        /// <summary>
        /// Get a string preference value
        /// </summary>
        public string? GetString(string key, string? defaultValue = null)
        {
            const string sql = "SELECT Value FROM Preferences WHERE Key = @Key";
            var value = ExecuteScalar<string>(sql, new SQLiteParameter("@Key", key));
            return value ?? defaultValue;
        }

        /// <summary>
        /// Get a boolean preference value
        /// </summary>
        public bool GetBool(string key, bool defaultValue = false)
        {
            var value = GetString(key);
            if (value == null) return defaultValue;
            return bool.TryParse(value, out var result) && result;
        }

        /// <summary>
        /// Get an integer preference value
        /// </summary>
        public int GetInt(string key, int defaultValue = 0)
        {
            var value = GetString(key);
            if (value == null) return defaultValue;
            return int.TryParse(value, out var result) ? result : defaultValue;
        }

        /// <summary>
        /// Set a preference value
        /// </summary>
        public void Set(string key, string value, string type = "string")
        {
            const string sql = @"
                INSERT OR REPLACE INTO Preferences (Key, Value, Type)
                VALUES (@Key, @Value, @Type)";

            ExecuteNonQuery(sql,
                new SQLiteParameter("@Key", key),
                new SQLiteParameter("@Value", value),
                new SQLiteParameter("@Type", type));
        }

        /// <summary>
        /// Set a boolean preference value
        /// </summary>
        public void SetBool(string key, bool value)
        {
            Set(key, value.ToString(), "bool");
        }

        /// <summary>
        /// Set an integer preference value
        /// </summary>
        public void SetInt(string key, int value)
        {
            Set(key, value.ToString(), "int");
        }

        /// <summary>
        /// Delete a preference
        /// </summary>
        public void Delete(string key)
        {
            const string sql = "DELETE FROM Preferences WHERE Key = @Key";
            ExecuteNonQuery(sql, new SQLiteParameter("@Key", key));
        }

        /// <summary>
        /// Check if a preference key exists
        /// </summary>
        public bool Exists(string key)
        {
            const string sql = "SELECT COUNT(*) FROM Preferences WHERE Key = @Key";
            var count = ExecuteScalar<long>(sql, new SQLiteParameter("@Key", key));
            return count > 0;
        }
    }
}
