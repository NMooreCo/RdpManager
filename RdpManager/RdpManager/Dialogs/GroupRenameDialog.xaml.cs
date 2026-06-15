using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using MessageBox = System.Windows.MessageBox;
using UserControl = System.Windows.Controls.UserControl;

namespace RdpManager.Dialogs
{
    public partial class GroupRenameDialog : UserControl
    {
        private readonly string _originalGroupName;

        public GroupRenameDialog(string currentGroupName)
        {
            InitializeComponent();
            _originalGroupName = currentGroupName;
            GroupNameTextBox.Text = currentGroupName;
            GroupNameTextBox.SelectAll();
            GroupNameTextBox.Focus();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var newName = GroupNameTextBox.Text?.Trim();

            if (string.IsNullOrEmpty(newName))
            {
                MessageBox.Show("Group name cannot be empty.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (newName.Contains("/"))
            {
                MessageBox.Show("Group name cannot contain '/' character.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Return the new name to the caller
            DialogHost.CloseDialogCommand.Execute(newName, this);
        }
    }
}
