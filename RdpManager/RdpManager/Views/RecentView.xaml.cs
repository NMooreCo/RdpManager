using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RdpManager.Models;

namespace RdpManager.Views
{
    public partial class RecentView : System.Windows.Controls.UserControl
    {
        public event EventHandler<Models.TreeNode>? ConnectRequested;

        public RecentView()
        {
            InitializeComponent();
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (RecentTreeView.SelectedItem is Models.TreeNode node)
            {
                ConnectRequested?.Invoke(this, node);
            }
        }

        private void RecentTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (RecentTreeView.SelectedItem is Models.TreeNode node && node.ComputerEntry != null)
            {
                ConnectRequested?.Invoke(this, node);
            }
        }
    }
}
