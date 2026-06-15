using RdpManager.Models;
using System.Windows;

namespace RdpManager
{
    public partial class AddEditWindow : Window
    {
        public ComputerEntry ComputerEntry { get; set; }

        public AddEditWindow()
        {
            InitializeComponent();
        }

        public AddEditWindow(ComputerEntry entry) : this()
        {
            ComputerEntry = entry;
            MachineNameTextBox.Text = entry.MachineName;
            FriendlyNameTextBox.Text = entry.FriendlyName;
            GroupNameTextBox.Text = entry.Group;
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            ComputerEntry = new ComputerEntry
            {
                MachineName = MachineNameTextBox.Text,
                FriendlyName = FriendlyNameTextBox.Text,
                Group = GroupNameTextBox.Text
            };
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
