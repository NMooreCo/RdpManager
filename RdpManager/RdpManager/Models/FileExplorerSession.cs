using System;
using System.ComponentModel;
using System.IO;

namespace RdpManager.Models
{
    /// <summary>
    /// Represents an open File Explorer session
    /// </summary>
    public class FileExplorerSession : INotifyPropertyChanged
    {
        private string _folderPath = string.Empty;
        private string? _displayName;
        private DateTime _createdAt = DateTime.Now;
        private DateTime? _lastAccessedAt;
        private int _sortOrder;

        public Guid SessionId { get; set; } = Guid.NewGuid();

        public string FolderPath
        {
            get => _folderPath;
            set
            {
                _folderPath = value;
                OnPropertyChanged(nameof(FolderPath));
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(FolderIcon));
            }
        }

        public string? DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(_displayName))
                    return _displayName;

                // Generate display name from folder path
                return GetFriendlyName(_folderPath);
            }
            set
            {
                _displayName = value;
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set
            {
                _createdAt = value;
                OnPropertyChanged(nameof(CreatedAt));
            }
        }

        public DateTime? LastAccessedAt
        {
            get => _lastAccessedAt;
            set
            {
                _lastAccessedAt = value;
                OnPropertyChanged(nameof(LastAccessedAt));
            }
        }

        public int SortOrder
        {
            get => _sortOrder;
            set
            {
                _sortOrder = value;
                OnPropertyChanged(nameof(SortOrder));
            }
        }

        /// <summary>
        /// UI Helper - Get icon path for folder type
        /// </summary>
        public string FolderIcon => IsNetworkPath ? "FolderNetworkOutline" : "FolderOutline";

        /// <summary>
        /// UI Helper - Check if path is a network path (UNC)
        /// </summary>
        public bool IsNetworkPath => _folderPath.StartsWith("\\\\") || _folderPath.StartsWith("//");

        /// <summary>
        /// Get a friendly display name from the folder path
        /// </summary>
        private string GetFriendlyName(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                return "Explorer";

            try
            {
                // For UNC paths like \\server\share
                if (folderPath.StartsWith("\\\\") || folderPath.StartsWith("//"))
                {
                    var parts = folderPath.TrimStart('\\', '/').Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0)
                    {
                        // Show server name or server\share
                        return parts.Length > 1 ? $"\\\\{parts[0]}\\{parts[1]}" : $"\\\\{parts[0]}";
                    }
                }

                // For local paths, show the last folder name or drive letter
                if (Path.IsPathRooted(folderPath))
                {
                    var dirInfo = new DirectoryInfo(folderPath);
                    if (dirInfo.Parent == null)
                    {
                        // Root drive like C:\
                        return folderPath.TrimEnd('\\');
                    }
                    return dirInfo.Name;
                }

                return folderPath;
            }
            catch
            {
                // If parsing fails, return the raw path
                return folderPath;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
