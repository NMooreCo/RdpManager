using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using RdpManager.Models;
using RdpManager.Controls;

namespace RdpManager.Views
{
    public partial class ActiveSessionsView : System.Windows.Controls.UserControl
    {
        private Dictionary<Guid, RdpSessionControl> _sessionControls = new Dictionary<Guid, RdpSessionControl>();
        private RdpSession? _currentSession;
        private RdpSession? _poppedOutSession;

        public ActiveSessionsView()
        {
            InitializeComponent();
        }

        public void ShowSession(RdpSession session, bool autoConnect = true)
        {
            _currentSession = session;

            // Check if session is popped out
            if (session.IsPopedOut)
            {
                ShowPoppedOutMessage(session);
                return;
            }

            // Get or create the RDP control for this session
            if (!_sessionControls.ContainsKey(session.SessionId))
            {
                // Create new control for this session
                var control = new RdpSessionControl
                {
                    Session = session
                };

                // Wire up events
                control.Connected += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"Session {session.DisplayName} connected");
                };

                control.Disconnected += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"Session {session.DisplayName} disconnected");
                    // Session stays in list - no auto-removal
                };

                control.ConnectionFailed += (s, error) =>
                {
                    System.Diagnostics.Debug.WriteLine($"Session {session.DisplayName} failed: {error}");
                };

                _sessionControls[session.SessionId] = control;
            }

            // Get the control (existing or newly created)
            var sessionControl = _sessionControls[session.SessionId];

            // Only auto-connect if requested (not for restored sessions)
            if (autoConnect &&
                session.ConnectionState != RdpConnectionState.Connected &&
                session.ConnectionState != RdpConnectionState.Connecting)
            {
                System.Diagnostics.Debug.WriteLine($"Auto-connecting to {session.DisplayName}...");
                ConnectSession(session, sessionControl);
            }
            else if (!autoConnect)
            {
                System.Diagnostics.Debug.WriteLine($"Showing {session.DisplayName} WITHOUT auto-connect");
            }

            // Show the control
            RdpContentHost.Content = sessionControl;
            NoSelectionPanel.Visibility = Visibility.Collapsed;
            PoppedOutPanel.Visibility = Visibility.Collapsed;

            // Activate and focus the control to prevent lag/sleep
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    sessionControl.Focus();
                    sessionControl.UpdateLayout();
                    System.Diagnostics.Debug.WriteLine($"Activated session: {session.DisplayName}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Activation error: {ex.Message}");
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        public void ShowPoppedOutMessage(RdpSession session)
        {
            _poppedOutSession = session;

            // Hide RDP content and no selection panel
            RdpContentHost.Content = null;
            NoSelectionPanel.Visibility = Visibility.Collapsed;

            // Show popped out message
            PoppedOutPanel.Visibility = Visibility.Visible;
            PoppedOutSessionName.Text = session.DisplayName;
        }

        public RdpSessionControl? GetSessionControl(Guid sessionId)
        {
            return _sessionControls.ContainsKey(sessionId) ? _sessionControls[sessionId] : null;
        }

        public void ClearSelection()
        {
            _currentSession = null;
            RdpContentHost.Content = null;
            NoSelectionPanel.Visibility = Visibility.Visible;
            PoppedOutPanel.Visibility = Visibility.Collapsed;
        }

        private void SwitchToWindow_Click(object sender, RoutedEventArgs e)
        {
            if (_poppedOutSession?.PopoutWindow != null)
            {
                try
                {
                    // Bring pop-out window to front
                    _poppedOutSession.PopoutWindow.Activate();
                    _poppedOutSession.PopoutWindow.Focus();

                    // Restore if minimized
                    if (_poppedOutSession.PopoutWindow.WindowState == WindowState.Minimized)
                    {
                        _poppedOutSession.PopoutWindow.WindowState = WindowState.Normal;
                    }

                    System.Diagnostics.Debug.WriteLine($"Switched to pop-out window for: {_poppedOutSession.DisplayName}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error switching to window: {ex.Message}");
                }
            }
        }

        public void RemoveSession(RdpSession session)
        {
            if (_sessionControls.ContainsKey(session.SessionId))
            {
                var control = _sessionControls[session.SessionId];

                // Disconnect and dispose
                try
                {
                    control.Disconnect();
                    control.Dispose();
                }
                catch { }

                _sessionControls.Remove(session.SessionId);

                // If this was the current session, clear the display
                if (_currentSession?.SessionId == session.SessionId)
                {
                    ClearSelection();
                }
            }
        }

        public void RemoveAllSessions()
        {
            foreach (var kvp in _sessionControls)
            {
                try
                {
                    kvp.Value.Disconnect();
                    kvp.Value.Dispose();
                }
                catch { }
            }

            _sessionControls.Clear();
            ClearSelection();
        }

        public event EventHandler<RdpSession>? SessionReconnecting;

        private void ConnectSession(RdpSession session, RdpSessionControl control)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[ConnectSession] Connecting session: {session.FriendlyName}");

                // Create a new connection log entry for this reconnection attempt
                if (session.ComputerId > 0)
                {
                    try
                    {
                        var connectionLogRepo = new RdpManager.Data.Repositories.ConnectionLogRepository();

                        // Close the old log entry if it exists
                        if (session.ConnectionLogId > 0)
                        {
                            connectionLogRepo.CloseSession(session.ConnectionLogId);
                        }

                        // Create new log entry for this reconnection
                        session.ConnectionLogId = connectionLogRepo.StartConnection(
                            session.ComputerId,
                            "Embedded",
                            session.ServerAddress,
                            session.FriendlyName,
                            session.Domain,
                            session.Group);

                        System.Diagnostics.Debug.WriteLine($"  Created new connection log #{session.ConnectionLogId}");

                        // Notify that session is reconnecting (so Recent view can refresh)
                        SessionReconnecting?.Invoke(this, session);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"  Error creating connection log: {ex.Message}");
                    }
                }

                var creds = MainController.LoadCredentials("RdpManagerCredentials");
                if (creds == null)
                {
                    System.Windows.MessageBox.Show("Please set your username and password in the settings.",
                        "Credentials Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    session.ConnectionState = RdpConnectionState.Failed;
                    session.ErrorMessage = "No credentials configured";
                    return;
                }

                // Delay connection to allow control to be properly sized
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    control.Connect(
                        session.ServerAddress,
                        creds.UserName,
                        creds.Password,
                        session.Domain
                    );
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error connecting to {session.DisplayName}:\n\n{ex.Message}",
                    "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
                session.ConnectionState = RdpConnectionState.Failed;
                session.ErrorMessage = ex.Message;
            }
        }
    }
}
