using System.Collections.ObjectModel;
using System.ComponentModel;

namespace RdpManager.Models
{
    public class GroupNode : INotifyPropertyChanged
    {
        private string _name;
        private ObservableCollection<object> _items = new ObservableCollection<object>();
        private bool _isExpanded = true; // Default to expanded
        public GroupNode(bool isExpanded)
        {
            _isExpanded = isExpanded;
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(nameof(Name)); }
        }

        public ObservableCollection<object> Items
        {
            get => _items;
            set { _items = value; OnPropertyChanged(nameof(Items)); }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(nameof(IsExpanded)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
