using System;
using System.ComponentModel;
using System.Windows;

namespace RdpManager.Models
{
    public enum RdpConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting,
        Failed
    }

    public class RdpSession : INotifyPropertyChanged
    {
        private string _friendlyName = string.Empty;
        private string _serverAddress = string.Empty;
        private string? _domain;
        private string? _group;
        private RdpConnectionState _connectionState = RdpConnectionState.Disconnected;
        private DateTime? _connectedAt;
        private TimeSpan _duration = TimeSpan.Zero;
        private string? _errorMessage;
        private bool _isPopedOut = false;
        private Window? _popoutWindow = null;

        public Guid SessionId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// Computer ID for database logging (0 for servers)
        /// </summary>
        public long ComputerId { get; set; }

        /// <summary>
        /// Connection log ID from database
        /// </summary>
        public long ConnectionLogId { get; set; }

        /// <summary>
        /// Disconnect error code from FreeRDP
        /// </summary>
        public int? DisconnectErrorCode { get; set; }

        public string FriendlyName
        {
            get => _friendlyName;
            set { _friendlyName = value; OnPropertyChanged(nameof(FriendlyName)); OnPropertyChanged(nameof(DisplayName)); }
        }

        public string ServerAddress
        {
            get => _serverAddress;
            set { _serverAddress = value; OnPropertyChanged(nameof(ServerAddress)); }
        }

        public string? Domain
        {
            get => _domain;
            set { _domain = value; OnPropertyChanged(nameof(Domain)); }
        }

        public string? Group
        {
            get => _group;
            set { _group = value; OnPropertyChanged(nameof(Group)); }
        }

        public RdpConnectionState ConnectionState
        {
            get => _connectionState;
            set { _connectionState = value; OnPropertyChanged(nameof(ConnectionState)); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusColor)); }
        }

        public DateTime? ConnectedAt
        {
            get => _connectedAt;
            set { _connectedAt = value; OnPropertyChanged(nameof(ConnectedAt)); }
        }

        public TimeSpan Duration
        {
            get => _duration;
            set { _duration = value; OnPropertyChanged(nameof(Duration)); OnPropertyChanged(nameof(DurationText)); }
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(nameof(ErrorMessage)); }
        }

        public bool IsPopedOut
        {
            get => _isPopedOut;
            set { _isPopedOut = value; OnPropertyChanged(nameof(IsPopedOut)); }
        }

        public Window? PopoutWindow
        {
            get => _popoutWindow;
            set { _popoutWindow = value; OnPropertyChanged(nameof(PopoutWindow)); }
        }

        // UI Helper Properties
        public string DisplayName => !string.IsNullOrEmpty(FriendlyName) ? FriendlyName : ServerAddress;

        public string StatusText => ConnectionState switch
        {
            RdpConnectionState.Connecting => "Connecting...",
            RdpConnectionState.Connected => "Connected",
            RdpConnectionState.Reconnecting => "Reconnecting...",
            RdpConnectionState.Failed => "Connection Failed",
            _ => "Disconnected"
        };

        public string StatusColor => ConnectionState switch
        {
            RdpConnectionState.Connecting => "#FFA500", // Orange
            RdpConnectionState.Connected => "#4CAF50", // Green
            RdpConnectionState.Reconnecting => "#FF9800", // Amber
            RdpConnectionState.Failed => "#F44336", // Red
            _ => "#9E9E9E" // Gray
        };

        public string DurationText
        {
            get
            {
                if (_duration.TotalSeconds < 1) return "00:00";
                if (_duration.TotalHours >= 1)
                    return $"{(int)_duration.TotalHours:D2}:{_duration.Minutes:D2}:{_duration.Seconds:D2}";
                return $"{_duration.Minutes:D2}:{_duration.Seconds:D2}";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
