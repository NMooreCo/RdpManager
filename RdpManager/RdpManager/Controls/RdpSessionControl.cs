using System;
using System.Windows;
using System.Windows.Forms.Integration;
using System.Windows.Threading;
using RdpManager.Models;
using RoyalApps.Community.FreeRdp.WinForms;

namespace RdpManager.Controls
{
    public class RdpSessionControl : System.Windows.Controls.UserControl, IDisposable
    {
        private WindowsFormsHost? _host;
        private FreeRdpControl? _rdpControl;
        private DispatcherTimer? _durationTimer;
        private DateTime? _connectedEventTime;
        private bool _disposed = false;

        public RdpSession? Session { get; set; }

        public event EventHandler? Connected;
        public event EventHandler? Disconnected;
        public event EventHandler<string>? ConnectionFailed;

        public RdpSessionControl()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            try
            {
                // Remove any default UserControl styling and fix WindowsFormsHost rendering in maximized borderless windows
                this.Background = System.Windows.Media.Brushes.Black;
                this.BorderBrush = null;
                this.BorderThickness = new System.Windows.Thickness(0);
                this.Padding = new System.Windows.Thickness(0);
                this.Margin = new System.Windows.Thickness(0);
                this.ClipToBounds = true;  // Prevent white border artifacts
                this.UseLayoutRounding = true;  // Fix subpixel rendering
                this.SnapsToDevicePixels = true;  // Align to device pixels

                // Create the WindowsFormsHost
                _host = new WindowsFormsHost
                {
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                    VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                    Margin = new System.Windows.Thickness(0),
                    Padding = new System.Windows.Thickness(0),
                    Background = System.Windows.Media.Brushes.Black,
                    ClipToBounds = true,  // Clip any overflow
                    UseLayoutRounding = true,  // Fix subpixel rendering
                    SnapsToDevicePixels = true  // Align to device pixels
                };

                // Create the FreeRDP control
                _rdpControl = new FreeRdpControl
                {
                    Dock = System.Windows.Forms.DockStyle.Fill,
                    Margin = new System.Windows.Forms.Padding(0),
                    BackColor = System.Drawing.Color.Black
                };

                // Wire up events
                _rdpControl.Connected += RdpControl_Connected;
                _rdpControl.Disconnected += RdpControl_Disconnected;
                _rdpControl.CertificateError += RdpControl_CertificateError;
                _rdpControl.VerifyCredentials += RdpControl_VerifyCredentials;

                // Handle window resize to update RDP display
                this.SizeChanged += RdpSessionControl_SizeChanged;
                _rdpControl.Resize += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"FreeRdpControl resized to: {_rdpControl.Width}x{_rdpControl.Height}");
                };

                // Host the control
                _host.Child = _rdpControl;

                // Set the content of this UserControl (wrap in border to ensure no white space)
                var container = new System.Windows.Controls.Border
                {
                    Background = System.Windows.Media.Brushes.Black,
                    BorderThickness = new System.Windows.Thickness(0),
                    Padding = new System.Windows.Thickness(0),
                    Margin = new System.Windows.Thickness(0),
                    ClipToBounds = true,  // Clip any overflow from WindowsFormsHost
                    UseLayoutRounding = true,  // Fix subpixel rendering
                    SnapsToDevicePixels = true,  // Align to device pixels
                    Child = _host
                };
                this.Content = container;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to initialize RDP control: {ex.Message}\n\n{ex.StackTrace}",
                    "RDP Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void Connect(string server, string username, string password, string? domain = null)
        {
            if (_rdpControl == null || Session == null) return;

            try
            {
                // Update session state
                Session.ConnectionState = RdpConnectionState.Connecting;
                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] STATE → Connecting");

                // Log connection attempt
                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Attempting RDP connection to: {server}");
                System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Username: {username}, Domain: {domain ?? "(none)"}");

                // Parse username - if it contains domain\username, split it
                string actualUsername = username;
                string actualDomain = domain ?? string.Empty;

                if (username.Contains('\\') && string.IsNullOrEmpty(domain))
                {
                    var parts = username.Split('\\');
                    actualDomain = parts[0];
                    actualUsername = parts[1];
                    System.Diagnostics.Debug.WriteLine($"Split username: Domain={actualDomain}, User={actualUsername}");
                }

                // Configure FreeRDP connection via Configuration property
                _rdpControl.Configuration.Server = server;
                _rdpControl.Configuration.Username = actualUsername;
                _rdpControl.Configuration.Password = password;

                // Only set domain if we have one
                if (!string.IsNullOrEmpty(actualDomain))
                {
                    _rdpControl.Configuration.Domain = actualDomain;
                }

                // Desktop size - Set to 0 for automatic sizing based on container size
                // "When both equal zero, the remote desktop size is determined by the container size"
                _rdpControl.Configuration.DesktopWidth = 0;
                _rdpControl.Configuration.DesktopHeight = 0;

                // Display settings
                _rdpControl.Configuration.ColorDepth = (RoyalApps.Community.FreeRdp.WinForms.Configuration.BitsPerPixel)32;

                // Enable automatic scaling
                _rdpControl.Configuration.AutoScaling = true;

                // IMPORTANT: Enable SmartReconnect for dynamic resizing!
                // "When enabled, the connection will automatically be closed and re-opened
                // to adapt to the new desktop size if the container dimensions change"
                _rdpControl.Configuration.SmartReconnect = true;

                System.Diagnostics.Debug.WriteLine($"Configuration: AutoScaling=true, SmartReconnect=true, Desktop=0x0 (auto)");

                // Connect
                System.Diagnostics.Debug.WriteLine("Calling FreeRDP Connect()...");
                _rdpControl.Connect();
            }
            catch (Exception ex)
            {
                Session.ConnectionState = RdpConnectionState.Failed;
                Session.ErrorMessage = $"Exception during connect: {ex.Message}\n\n{ex.StackTrace}";
                System.Diagnostics.Debug.WriteLine($"Connection exception: {ex}");
                ConnectionFailed?.Invoke(this, ex.Message);
            }
        }

        public void Disconnect()
        {
            try
            {
                _rdpControl?.Disconnect();
                StopDurationTimer();
            }
            catch { }
        }

        private void RdpControl_Connected(object? sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] FreeRDP Connected event fired!");
            _connectedEventTime = DateTime.Now;

            // Delay setting Connected state to verify it's a real connection
            // FreeRDP sometimes fires Connected prematurely before errors occur
            Task.Delay(500).ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    // Only set Connected if we're still connected after delay
                    // (haven't received Disconnected event)
                    if (Session != null && _connectedEventTime.HasValue &&
                        (DateTime.Now - _connectedEventTime.Value).TotalMilliseconds < 600)
                    {
                        System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] STATE → Connected (verified)");
                        Session.ConnectionState = RdpConnectionState.Connected;
                        Session.ConnectedAt = DateTime.Now;
                        StartDurationTimer();
                        Connected?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Connected event ignored (disconnected too quickly)");
                    }
                });
            });
        }

        private void RdpControl_Disconnected(object? sender, DisconnectEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] FreeRDP Disconnected event fired!");

            // Clear connected event time to prevent delayed Connected state change
            _connectedEventTime = null;

            Dispatcher.Invoke(() =>
            {
                if (Session != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] STATE → Disconnected");
                    Session.ConnectionState = RdpConnectionState.Disconnected;

                    // Capture error message and code if available
                    string errorMsg = e.ErrorMessage ?? "Connection closed";
                    Session.ErrorMessage = errorMsg;

                    // Try to get error code from DisconnectEventArgs
                    int? errorCode = null;
                    try
                    {
                        var errorCodeProp = e.GetType().GetProperty("ErrorCode");
                        if (errorCodeProp != null)
                        {
                            errorCode = (int?)errorCodeProp.GetValue(e);
                            Session.DisconnectErrorCode = errorCode;
                        }
                    }
                    catch { }

                    System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Error: {errorMsg} (Code: {errorCode})");
                    StopDurationTimer();

                    // Log connection end to database
                    if (Session.ConnectionLogId > 0)
                    {
                        try
                        {
                            var connectionLogRepo = new RdpManager.Data.Repositories.ConnectionLogRepository();
                            bool success = Session.ConnectionState == RdpConnectionState.Connected || errorMsg == "Connection closed";
                            connectionLogRepo.EndConnection(Session.ConnectionLogId, success, errorMsg, errorCode);

                            // Update computer's last disconnect error code
                            if (Session.ComputerId > 0)
                            {
                                var computerRepo = new RdpManager.Data.Repositories.ComputerRepository();
                                computerRepo.UpdateLastConnection(Session.ComputerId, errorCode);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error logging connection end: {ex.Message}");
                        }
                    }
                }
                Disconnected?.Invoke(this, EventArgs.Empty);
            });
        }

        private void RdpControl_CertificateError(object? sender, CertificateErrorEventArgs e)
        {
            // Auto-accept certificate errors (already set IgnoreCertificate = true, but handle event anyway)
            e.Continue();
        }

        private void RdpControl_VerifyCredentials(object? sender, VerifyCredentialsEventArgs e)
        {
            // Credential verification failed
            Dispatcher.Invoke(() =>
            {
                if (Session != null)
                {
                    Session.ConnectionState = RdpConnectionState.Failed;
                    Session.ErrorMessage = "Authentication failed - invalid credentials";
                    ConnectionFailed?.Invoke(this, "Invalid username or password");
                }
            });
        }

        private void RdpSessionControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // SmartReconnect handles this automatically - just log for debugging
            if (Session?.ConnectionState == RdpConnectionState.Connected)
            {
                System.Diagnostics.Debug.WriteLine($"UserControl resized: {e.NewSize.Width}x{e.NewSize.Height} - SmartReconnect will handle it");
            }
        }

        private void StartDurationTimer()
        {
            _durationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _durationTimer.Tick += DurationTimer_Tick;
            _durationTimer.Start();
        }

        private void StopDurationTimer()
        {
            if (_durationTimer != null)
            {
                _durationTimer.Stop();
                _durationTimer.Tick -= DurationTimer_Tick;
                _durationTimer = null;
            }
        }

        private void DurationTimer_Tick(object? sender, EventArgs e)
        {
            if (Session != null && Session.ConnectedAt.HasValue)
            {
                Session.Duration = DateTime.Now - Session.ConnectedAt.Value;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    StopDurationTimer();

                    if (_rdpControl != null)
                    {
                        try
                        {
                            // Unsubscribe from events
                            _rdpControl.Connected -= RdpControl_Connected;
                            _rdpControl.Disconnected -= RdpControl_Disconnected;
                            _rdpControl.CertificateError -= RdpControl_CertificateError;
                            _rdpControl.VerifyCredentials -= RdpControl_VerifyCredentials;

                            // Disconnect if connected
                            _rdpControl.Disconnect();

                            // Dispose the control
                            _rdpControl.Dispose();
                            _rdpControl = null;
                        }
                        catch { }
                    }

                    if (_host != null)
                    {
                        _host.Dispose();
                        _host = null;
                    }
                }

                _disposed = true;
            }
        }

        ~RdpSessionControl()
        {
            Dispose(false);
        }
    }
}
