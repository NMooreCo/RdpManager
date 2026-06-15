using System;
using System.ComponentModel;

namespace RdpManager.Models
{
    public class ComputerEntry : INotifyPropertyChanged
    {
        private string _machineName;
        private string _friendlyName;
        private string _group;
        private string _domain;
        private bool _isFavorite;

        /// <summary>
        /// Database ID (0 if not yet persisted)
        /// </summary>
        public long Id { get; set; }

        public string MachineName
        {
            get => _machineName;
            set { _machineName = value; OnPropertyChanged(nameof(MachineName)); }
        }

        public string FriendlyName
        {
            get => _friendlyName;
            set { _friendlyName = value; OnPropertyChanged(nameof(FriendlyName)); }
        }

        public string Group
        {
            get => _group;
            set { _group = value; OnPropertyChanged(nameof(Group)); }
        }

        public string Domain
        {
            get => _domain;
            set { _domain = value; OnPropertyChanged(nameof(Domain)); }
        }

        public bool IsFavorite
        {
            get => _isFavorite;
            set { _isFavorite = value; OnPropertyChanged(nameof(IsFavorite)); }
        }

        /// <summary>
        /// Sort order within the group (0-based)
        /// </summary>
        public int SortOrder { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastConnectedAt { get; set; }
        public int? LastDisconnectErrorCode { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
