using System;
using System.Windows;
using RdpManager.Models;
using MaterialDesignThemes.Wpf;
using RdpManager.Dialogs;
using MessageBox = System.Windows.MessageBox;

namespace RdpManager.Windows
{
    public partial class PopoutWindow : BorderlessWindowBase
    {
        private readonly RdpSession _session;
        private readonly Action<RdpSession> _onPopIn;
        private readonly Action<RdpSession>? _onClose;
        private SnackbarMessageQueue? _snackbarMessageQueue;

        // Expose session for XAML data binding
        public RdpSession Session => _session;

        public PopoutWindow(RdpSession session, Action<RdpSession> onPopIn, Action<RdpSession>? onClose = null)
        {
            InitializeComponent();

            _session = session;
            _onPopIn = onPopIn;
            _onClose = onClose;

            // Set window title
            Title = $"{session.FriendlyName} - RDP Session";

            // Handle window closing - pop session back in
            Closing += PopoutWindow_Closing;
        }

        private void ReconnectButton_Click(object? sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Reconnect button clicked for session: {_session.FriendlyName}");

            // Get the RDP control from the content host
            var rdpControl = RdpContentHost.Content as RdpManager.Controls.RdpSessionControl;
            if (rdpControl != null)
            {
                // Load credentials and reconnect
                var creds = MainController.LoadCredentials("RdpManagerCredentials");
                if (creds != null)
                {
                    System.Diagnostics.Debug.WriteLine($"  Reconnecting with credentials...");
                    rdpControl.Connect(_session.ServerAddress, creds.UserName, creds.Password, _session.Domain);
                }
                else
                {
                    MessageBox.Show("Please set your username and password in the settings.",
                        "Credentials Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"  ERROR: RDP control not found in popout window");
            }
        }

        private void PopoutWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Only pop-in if we're actually closing (not if already popped in)
            if (_session.IsPopedOut)
            {
                System.Diagnostics.Debug.WriteLine($"PopoutWindow closing - popping session back in: {_session.DisplayName}");
                _onPopIn?.Invoke(_session);
            }
        }

        private void CloseButton_Click(object? sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Close button clicked for session: {_session.FriendlyName}, State: {_session.ConnectionState}");

            bool shouldClose = true;

            // Show confirmation if session is connected
            if (_session.ConnectionState == RdpConnectionState.Connected ||
                _session.ConnectionState == RdpConnectionState.Connecting ||
                _session.ConnectionState == RdpConnectionState.Reconnecting)
            {
                var result = MessageBox.Show(
                    $"Close active connection to {_session.DisplayName}?\n\nThe session will be disconnected and removed from Active Sessions.",
                    "Confirm Close",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                shouldClose = result == MessageBoxResult.Yes;
                System.Diagnostics.Debug.WriteLine($"  User confirmation: {(shouldClose ? "Yes" : "No")}");
            }

            if (shouldClose)
            {
                // Disconnect the RDP session
                var rdpControl = RdpContentHost.Content as RdpManager.Controls.RdpSessionControl;
                if (rdpControl != null)
                {
                    System.Diagnostics.Debug.WriteLine($"  Disconnecting RDP control...");
                    rdpControl.Disconnect();
                }

                // Mark as not popped out and remove from Active Sessions
                _session.IsPopedOut = false;

                // Call the close handler to remove from Active Sessions
                System.Diagnostics.Debug.WriteLine($"  Calling onClose handler to remove from Active Sessions");
                _onClose?.Invoke(_session);

                // Close window
                System.Diagnostics.Debug.WriteLine($"  Closing popout window");
                this.Close();
            }
        }

        private void PopInButton_Click(object? sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Pop-in button clicked for session: {_session.DisplayName}");

            // Call the pop-in handler
            _onPopIn?.Invoke(_session);

            // Close the window (but don't trigger pop-in again in Closing event)
            _session.IsPopedOut = false; // Prevent Closing event from triggering pop-in again
            this.Close();
        }
    }
}
