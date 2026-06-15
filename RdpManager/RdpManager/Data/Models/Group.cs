using System;

namespace RdpManager.Data.Models
{
    /// <summary>
    /// Represents a group in the hierarchical computer organization
    /// </summary>
    public class Group
    {
        /// <summary>
        /// Database ID
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Full path of the group (e.g., "PPS/Prod/Specialty")
        /// </summary>
        public string FullPath { get; set; } = string.Empty;

        /// <summary>
        /// Parent group path (e.g., "PPS/Prod" for "PPS/Prod/Specialty")
        /// Null for root-level groups
        /// </summary>
        public string? ParentPath { get; set; }

        /// <summary>
        /// Display name of this group level (e.g., "Specialty" from "PPS/Prod/Specialty")
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Sort order within the parent group (0-based)
        /// </summary>
        public int SortOrder { get; set; }

        /// <summary>
        /// When this group was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When this group was last modified
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
