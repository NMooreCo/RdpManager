using System;

namespace RdpManager.Data.Models
{
    /// <summary>
    /// Represents a detailed connection log entry for RDP sessions
    /// </summary>
    public class ConnectionLog
    {
        /// <summary>
        /// Unique identifier for the connection log entry
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Reference to the computer that was connected to
        /// </summary>
        public long ComputerId { get; set; }

        /// <summary>
        /// Server address (for session restoration)
        /// </summary>
        public string ServerAddress { get; set; } = string.Empty;

        /// <summary>
        /// Friendly name (for session restoration)
        /// </summary>
        public string? FriendlyName { get; set; }

        /// <summary>
        /// Domain (for session restoration)
        /// </summary>
        public string? Domain { get; set; }

        /// <summary>
        /// Group path (for session restoration)
        /// </summary>
        public string? GroupPath { get; set; }

        /// <summary>
        /// Timestamp when the connection was initiated
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// Timestamp when the connection ended (null if still active)
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Duration of the connection in seconds (null if still active)
        /// </summary>
        public int? DurationSeconds { get; set; }

        /// <summary>
        /// Type of connection: "Embedded" or "External"
        /// </summary>
        public string ConnectionType { get; set; } = "Embedded";

        /// <summary>
        /// Whether the connection was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Human-readable disconnect reason
        /// </summary>
        public string? DisconnectReason { get; set; }

        /// <summary>
        /// Error code from RDP disconnect event
        /// </summary>
        public int? ErrorCode { get; set; }

        /// <summary>
        /// Additional notes or error details
        /// </summary>
        public string? Notes { get; set; }

        /// <summary>
        /// Whether this session should be shown in Active Sessions view
        /// True = show in Active Sessions, False = closed by user
        /// </summary>
        public bool IsActiveSession { get; set; } = true;
    }
}
