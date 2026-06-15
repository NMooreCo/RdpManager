using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RdpManager.Models;

namespace RdpManager.Views
{
    public partial class FavoritesView : System.Windows.Controls.UserControl
    {
        public event EventHandler<Models.TreeNode>? ConnectRequested;

        public FavoritesView()
        {
            InitializeComponent();
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (FavoritesTreeView.SelectedItem is Models.TreeNode node)
            {
                ConnectRequested?.Invoke(this, node);
            }
        }

        private void FavoritesTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (FavoritesTreeView.SelectedItem is Models.TreeNode node && node.ComputerEntry != null)
            {
                ConnectRequested?.Invoke(this, node);
            }
        }
    }
}
