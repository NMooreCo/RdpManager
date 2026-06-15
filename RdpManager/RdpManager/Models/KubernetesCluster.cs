using System;
using System.ComponentModel;

namespace RdpManager.Models
{
    public class KubernetesCluster : INotifyPropertyChanged
    {
        private string _displayName = string.Empty;
        private string _projectId = string.Empty;
        private string _clusterName = string.Empty;
        private string _region = string.Empty;
        private string _defaultNamespace = "default";
        private string? _proxyAddress;
        private bool _clearProxyBeforeAuth = true;
        private int _sortOrder;
        private bool _isAuthenticated;
        private DateTime? _lastAuthenticatedAt;

        public Guid ClusterId { get; set; } = Guid.NewGuid();

        public string DisplayName
        {
            get => _displayName;
            set { _displayName = value; OnPropertyChanged(nameof(DisplayName)); }
        }

        public string ProjectId
        {
            get => _projectId;
            set { _projectId = value; OnPropertyChanged(nameof(ProjectId)); }
        }

        public string ClusterName
        {
            get => _clusterName;
            set { _clusterName = value; OnPropertyChanged(nameof(ClusterName)); }
        }

        public string Region
        {
            get => _region;
            set { _region = value; OnPropertyChanged(nameof(Region)); }
        }

        public string DefaultNamespace
        {
            get => _defaultNamespace;
            set { _defaultNamespace = value; OnPropertyChanged(nameof(DefaultNamespace)); }
        }

        public string? ProxyAddress
        {
            get => _proxyAddress;
            set { _proxyAddress = value; OnPropertyChanged(nameof(ProxyAddress)); OnPropertyChanged(nameof(HasProxy)); }
        }

        public bool ClearProxyBeforeAuth
        {
            get => _clearProxyBeforeAuth;
            set { _clearProxyBeforeAuth = value; OnPropertyChanged(nameof(ClearProxyBeforeAuth)); }
        }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int SortOrder
        {
            get => _sortOrder;
            set { _sortOrder = value; OnPropertyChanged(nameof(SortOrder)); }
        }

        // Runtime-only (not persisted)
        public bool IsAuthenticated
        {
            get => _isAuthenticated;
            set { _isAuthenticated = value; OnPropertyChanged(nameof(IsAuthenticated)); OnPropertyChanged(nameof(AuthStatusText)); OnPropertyChanged(nameof(AuthStatusColor)); }
        }

        public DateTime? LastAuthenticatedAt
        {
            get => _lastAuthenticatedAt;
            set { _lastAuthenticatedAt = value; OnPropertyChanged(nameof(LastAuthenticatedAt)); }
        }

        // UI Helpers
        public bool HasProxy => !string.IsNullOrEmpty(_proxyAddress);

        public string AuthStatusText => IsAuthenticated ? "Authenticated" : "Not Authenticated";

        public string AuthStatusColor => IsAuthenticated ? "#4CAF50" : "#9E9E9E";

        public string ContextName => $"gke_{ProjectId}_{Region}_{ClusterName}";

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
