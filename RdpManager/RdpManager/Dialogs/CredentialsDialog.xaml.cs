using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using RdpManager.Models;

namespace RdpManager.Dialogs
{
    public partial class CredentialsDialog : System.Windows.Controls.UserControl
    {
        private Views.MainViewModel? _viewModel;

        public CredentialsDialog(Views.MainViewModel? viewModel = null)
        {
            InitializeComponent();
            _viewModel = viewModel;

            // Load existing credentials if available
            var creds = MainController.LoadCredentials("RdpManagerCredentials");
            if (creds != null)
            {
                UsernameTextBox.Text = creds.UserName;
            }

            // Load preference
            if (_viewModel != null)
            {
                UseEmbeddedRdpCheckBox.IsChecked = _viewModel.UseEmbeddedRdp;
                ServerEndpointTextBox.Text = _viewModel.ServerEndpoint;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Save credentials only when both fields are provided. The password box is
            // never pre-populated, so leaving it blank means "don't change credentials"
            // rather than blocking the rest of the settings from saving.
            if (!string.IsNullOrWhiteSpace(UsernameTextBox.Text) &&
                !string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                SaveCredentials();
            }

            // Save preferences
            if (_viewModel != null)
            {
                if (UseEmbeddedRdpCheckBox.IsChecked.HasValue)
                {
                    _viewModel.UseEmbeddedRdp = UseEmbeddedRdpCheckBox.IsChecked.Value;
                }

                var newEndpoint = ServerEndpointTextBox.Text?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(newEndpoint) && newEndpoint != _viewModel.ServerEndpoint)
                {
                    _viewModel.ServerEndpoint = newEndpoint;
                    _ = _viewModel.ReloadServersAsync();
                }
            }

            // Close dialog with success
            DialogHost.CloseDialogCommand.Execute(true, this);
        }

        private void SaveCredentials()
        {
            using (var cred = new Credential
            {
                Target = "RdpManagerCredentials",
                Username = UsernameTextBox.Text,
                Password = PasswordBox.Password,
                Type = CredentialType.Generic,
                PersistanceType = PersistanceType.LocalComputer
            })
            {
                cred.Save();
            }
        }
    }
}