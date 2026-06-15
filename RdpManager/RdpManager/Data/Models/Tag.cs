using System;

namespace RdpManager.Data.Models
{
    /// <summary>
    /// Represents a tag that can be applied to computer entries
    /// </summary>
    public class Tag
    {
        /// <summary>
        /// Unique identifier for the tag
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Tag name (e.g., "Production", "Development", "Database Server")
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Hex color for the tag (e.g., "#FF5722")
        /// </summary>
        public string Color { get; set; } = "#9CA3AF";

        /// <summary>
        /// When the tag was created
        /// </summary>
        public DateTime CreatedAt { get; set; }
    }
}
