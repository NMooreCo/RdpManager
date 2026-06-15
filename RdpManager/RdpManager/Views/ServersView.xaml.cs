using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RdpManager.Models;

namespace RdpManager.Views
{
    public partial class ServersView : System.Windows.Controls.UserControl
    {
        public event EventHandler<Models.TreeNode>? ConnectRequested;
        public event EventHandler<string>? SearchTextChanged;

        public ServersView()
        {
            InitializeComponent();
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (ServersTreeView.SelectedItem is Models.TreeNode node)
            {
                ConnectRequested?.Invoke(this, node);
            }
        }

        private void ServersTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // Selection changed
        }

        private void ServersTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ServersTreeView.SelectedItem is Models.TreeNode node && node.Server != null)
            {
                ConnectRequested?.Invoke(this, node);
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchTextChanged?.Invoke(this, SearchBox.Text);
        }
    }
}
