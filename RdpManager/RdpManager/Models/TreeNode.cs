using System.Collections.ObjectModel;
using System.ComponentModel;

namespace RdpManager.Models
{
    public class TreeNode : INotifyPropertyChanged
    {
        private string _name;
        private ObservableCollection<TreeNode> _children;
        private bool _isExpanded;
        private bool _isVisible = true;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        public ObservableCollection<TreeNode> Children
        {
            get => _children;
            set { _children = value; OnPropertyChanged(nameof(Children)); }
        }

        /// <summary>
        /// Full group path (e.g., "PPS/Prod/Specialty") for state persistence
        /// </summary>
        public string? GroupPath { get; set; }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));

                    // Save expansion state if this is a group node (not a leaf)
                    if (!IsLeaf && !string.IsNullOrEmpty(GroupPath))
                    {
                        try
                        {
                            RdpManager.Helpers.GroupStateManager.Instance.SetExpansionState(GroupPath, value);
                        }
                        catch { }
                    }
                }
            }
        }

        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(nameof(IsVisible)); }
        }

        private bool _isFavorite;
        
        public ComputerEntry ComputerEntry { get; set; }
        public Server Server { get; set; }
        
        public bool IsFavorite 
        { 
            get => _isFavorite;
            set { _isFavorite = value; OnPropertyChanged(nameof(IsFavorite)); }
        }

        // UI Enhancement Properties
        public bool IsLeaf => ComputerEntry != null || Server != null;
        public string IconKind
        {
            get
            {
                if (ComputerEntry != null) return "Monitor";
                if (Server != null) return "Server";
                return Children.Count > 0 ? "FolderOpen" : "Folder";
            }
        }

        // Drag-Drop State Properties
        private bool _isDragging;
        public bool IsDragging
        {
            get => _isDragging;
            set { _isDragging = value; OnPropertyChanged(nameof(IsDragging)); }
        }

        private bool _isDropTarget;
        public bool IsDropTarget
        {
            get => _isDropTarget;
            set { _isDropTarget = value; OnPropertyChanged(nameof(IsDropTarget)); }
        }

        // Display Properties for Recent/Favorites
        public string? TimeAgo { get; set; }

        public TreeNode()
        {
            Children = new ObservableCollection<TreeNode>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
