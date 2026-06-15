using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using RdpManager.Data.Database;

namespace RdpManager.Data.Repositories
{
    /// <summary>
    /// Base class for repositories with common ADO.NET helper methods
    /// </summary>
    public abstract class BaseRepository
    {
        protected readonly DatabaseConnection Db;

        protected BaseRepository()
        {
            Db = DatabaseConnection.Instance;
        }

        /// <summary>
        /// Execute a query and map results to a list of objects
        /// </summary>
        protected List<T> ExecuteQuery<T>(string sql, Func<IDataReader, T> mapper, params SQLiteParameter[] parameters)
        {
            var results = new List<T>();

            using var connection = Db.CreateConnection();
            using var cmd = new SQLiteCommand(sql, connection);
            cmd.Parameters.AddRange(parameters);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(mapper(reader));
            }

            return results;
        }

        /// <summary>
        /// Execute a query and return a single result or null
        /// </summary>
        protected T? ExecuteQuerySingle<T>(string sql, Func<IDataReader, T> mapper, params SQLiteParameter[] parameters) where T : class
        {
            using var connection = Db.CreateConnection();
            using var cmd = new SQLiteCommand(sql, connection);
            cmd.Parameters.AddRange(parameters);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return mapper(reader);
            }

            return null;
        }

        /// <summary>
        /// Execute a non-query command and return rows affected
        /// </summary>
        protected int ExecuteNonQuery(string sql, params SQLiteParameter[] parameters)
        {
            using var connection = Db.CreateConnection();
            using var cmd = new SQLiteCommand(sql, connection);
            cmd.Parameters.AddRange(parameters);
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Execute a scalar query and return the result
        /// </summary>
        protected T? ExecuteScalar<T>(string sql, params SQLiteParameter[] parameters)
        {
            using var connection = Db.CreateConnection();
            using var cmd = new SQLiteCommand(sql, connection);
            cmd.Parameters.AddRange(parameters);

            var result = cmd.ExecuteScalar();
            if (result == null || result == DBNull.Value)
                return default;

            return (T)Convert.ChangeType(result, typeof(T));
        }

        /// <summary>
        /// Get the last inserted row ID
        /// </summary>
        protected long GetLastInsertRowId(SQLiteConnection connection)
        {
            using var cmd = new SQLiteCommand("SELECT last_insert_rowid()", connection);
            return (long)cmd.ExecuteScalar()!;
        }

        /// <summary>
        /// Safely get a string value from a data reader
        /// </summary>
        protected string? GetString(IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }

        /// <summary>
        /// Safely get an int value from a data reader (handles REAL/INTEGER conversions)
        /// </summary>
        protected int? GetNullableInt(IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal))
                return null;

            // SQLite might return INTEGER or REAL - handle both
            var value = reader.GetValue(ordinal);
            if (value is long longValue)
                return (int)longValue;
            if (value is int intValue)
                return intValue;
            if (value is double doubleValue)
                return (int)Math.Round(doubleValue);

            // Try converting from string as fallback
            return Convert.ToInt32(value);
        }

        /// <summary>
        /// Safely get a DateTime value from a data reader (assumes UTC stored in database)
        /// </summary>
        protected DateTime? GetNullableDateTime(IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal))
                return null;

            var value = reader.GetString(ordinal);
            // Parse as UTC and convert to local time
            var utcTime = DateTime.Parse(value, null, System.Globalization.DateTimeStyles.AssumeUniversal);
            return utcTime.ToLocalTime();
        }

        /// <summary>
        /// Safely get a bool value from a data reader (SQLite stores as INTEGER)
        /// </summary>
        protected bool GetBool(IDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal))
                return false;

            // SQLite might return INTEGER or REAL - handle both
            var value = reader.GetValue(ordinal);
            if (value is long longValue)
                return longValue == 1;
            if (value is int intValue)
                return intValue == 1;
            if (value is double doubleValue)
                return Math.Abs(doubleValue - 1.0) < 0.001;
            if (value is bool boolValue)
                return boolValue;

            // Try converting from string as fallback
            return Convert.ToInt32(value) == 1;
        }
    }
}
