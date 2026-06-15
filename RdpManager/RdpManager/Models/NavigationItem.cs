using System.ComponentModel;

namespace RdpManager.Models
{
    public enum NavigationItemType
    {
        Section,  // Main navigation sections (Computers, Servers, etc.)
        Session   // Active RDP session
    }

    public class NavigationItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _statusText = string.Empty;
        private string _statusColor = "#9E9E9E";
        private bool _isVisible = true;
        private bool _isEnabled = true;

        public NavigationItemType Type { get; set; }
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        public string IconData { get; set; } = string.Empty;
        public int SectionIndex { get; set; } // 0=Computers, 1=Servers, 2=Recent, 3=Favorites, 4=ActiveSessions

        // For session items
        public RdpSession? Session { get; set; }

        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(nameof(IsVisible)); }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(nameof(IsEnabled)); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(nameof(StatusText)); }
        }

        public string StatusColor
        {
            get => _statusColor;
            set { _statusColor = value; OnPropertyChanged(nameof(StatusColor)); }
        }

        public bool IsSection => Type == NavigationItemType.Section;
        public bool IsSession => Type == NavigationItemType.Session;

        /// <summary>
        /// Temporary navigation items (like easter eggs) that should be removed when switching away
        /// </summary>
        public bool IsTemporary { get; set; } = false;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
