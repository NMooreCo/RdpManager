using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Application = System.Windows.Application;
using MaterialDesignThemes.Wpf;
using RdpManager.Models;

namespace RdpManager.Dialogs
{
    public partial class AddEditComputerDialog : System.Windows.Controls.UserControl
    {
        private ComputerEntry _editingEntry;

#if NETFRAMEWORK
        public AddEditComputerDialog(ComputerEntry editEntry = null)
#else
        public AddEditComputerDialog(ComputerEntry? editEntry = null)
#endif
        {
            InitializeComponent();
            _editingEntry = editEntry;

            if (_editingEntry != null)
            {
                // Edit mode
                TitleText.Text = "Edit Computer";
                MachineNameTextBox.Text = _editingEntry.MachineName;
                FriendlyNameTextBox.Text = _editingEntry.FriendlyName;
                DomainTextBox.Text = _editingEntry.Domain;
                GroupComboBox.Text = _editingEntry.Group;
            }

            LoadGroups();
        }

        private void LoadGroups()
        {
            // Load existing groups from the current computer entries
            var viewModel = Application.Current.MainWindow?.DataContext as Views.MainViewModel;
            if (viewModel?.ComputerEntries != null)
            {
                var groups = viewModel.ComputerEntries
                    .Where(c => !string.IsNullOrWhiteSpace(c.Group))
                    .Select(c => c.Group)
                    .Distinct()
                    .OrderBy(g => g);

                foreach (var group in groups)
                {
                    GroupComboBox.Items.Add(group);
                }
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(MachineNameTextBox.Text))
            {
                ShowValidationError("Machine name is required");
                return;
            }

            if (string.IsNullOrWhiteSpace(FriendlyNameTextBox.Text))
            {
                ShowValidationError("Friendly name is required");
                return;
            }

            // Create or update the computer entry
            var entry = new ComputerEntry
            {
                MachineName = MachineNameTextBox.Text.Trim(),
                FriendlyName = FriendlyNameTextBox.Text.Trim(),
                Domain = DomainTextBox.Text?.Trim() ?? string.Empty,
                Group = GroupComboBox.Text?.Trim() ?? string.Empty
            };

            // Close dialog with the entry as result
            DialogHost.CloseDialogCommand.Execute(entry, this);
        }

        private void ShowValidationError(string message)
        {
            // You could show a snackbar or inline validation here
            // For now, we'll just focus the first empty field
            if (string.IsNullOrWhiteSpace(MachineNameTextBox.Text))
                MachineNameTextBox.Focus();
            else if (string.IsNullOrWhiteSpace(FriendlyNameTextBox.Text))
                FriendlyNameTextBox.Focus();
        }
    }
}