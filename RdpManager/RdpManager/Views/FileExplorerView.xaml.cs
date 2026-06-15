using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RdpManager.Models;
using RdpManager.Controls;
using RdpManager.Data.Repositories;

namespace RdpManager.Views
{
    public partial class FileExplorerView : System.Windows.Controls.UserControl
    {
        private Dictionary<Guid, FileExplorerSessionControl> _sessionControls = new Dictionary<Guid, FileExplorerSessionControl>();
        private Dictionary<Guid, Border> _tabControls = new Dictionary<Guid, Border>();
        private FileExplorerSession? _currentSession;
        private FileExplorerSessionRepository? _repository;

        public event EventHandler<FileExplorerSession>? SessionClosed;
        public event EventHandler? AllSessionsClosed;

        public FileExplorerView()
        {
            InitializeComponent();
            _repository = new FileExplorerSessionRepository();

            // Wire up navigation bar events
            BackButton.Click += (s, e) => NavigateBack();
            ForwardButton.Click += (s, e) => NavigateForward();
            UpButton.Click += (s, e) => NavigateUp();
            RefreshButton.Click += (s, e) => RefreshCurrent();
            PathTextBox.KeyDown += PathTextBox_KeyDown;
        }

        /// <summary>
        /// Show a specific File Explorer session
        /// </summary>
        public void ShowSession(FileExplorerSession session)
        {
            _currentSession = session;

            // Get or create the control for this session
            if (!_sessionControls.ContainsKey(session.SessionId))
            {
                // Create new control
                var control = new FileExplorerSessionControl
                {
                    Session = session
                };

                // Wire up events
                control.NavigationCompleted += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[FileExplorer] Navigation completed for: {session.DisplayName}");
                    UpdateNavigationBar();
                };

                control.NavigationFailed += (s, error) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[FileExplorer] Navigation failed: {error}");
                };

                control.HistoryChanged += (s, e) =>
                {
                    UpdateNavigationBar();
                };

                _sessionControls[session.SessionId] = control;

                // Create tab
                CreateTabForSession(session);

                // Navigate to the folder
                control.Navigate(session.FolderPath);
            }

            // Get the control (existing or newly created)
            var sessionControl = _sessionControls[session.SessionId];

            // Show the control
            ExplorerContentHost.Content = sessionControl;
            EmptyStatePanel.Visibility = Visibility.Collapsed;
            NavigationBar.Visibility = Visibility.Visible;

            // Update last accessed time
            session.LastAccessedAt = DateTime.Now;
            try
            {
                _repository?.UpdateLastAccessed(session.SessionId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileExplorer] Failed to update last accessed: {ex.Message}");
            }

            // Highlight the active tab
            UpdateTabHighlights(session.SessionId);

            // Focus the control
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    sessionControl.Focus();
                    sessionControl.UpdateLayout();
                }
                catch { }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Create a tab UI element for a session
        /// </summary>
        private void CreateTabForSession(FileExplorerSession session)
        {
            var tab = new Border
            {
                Style = (Style)FindResource("ExplorerTabStyle"),
                Tag = session.SessionId
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Tab content (folder name)
            var textBlock = new TextBlock
            {
                Text = session.DisplayName,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (System.Windows.Media.Brush)FindResource("App.Text.Primary"),
                FontSize = 13,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(textBlock, 0);
            grid.Children.Add(textBlock);

            // Close button
            var closeButton = new System.Windows.Controls.Button
            {
                Content = "×",
                Width = 20,
                Height = 20,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(0),
                Background = System.Windows.Media.Brushes.Transparent,
                BorderBrush = System.Windows.Media.Brushes.Transparent,
                Foreground = (System.Windows.Media.Brush)FindResource("App.Text.Secondary"),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            closeButton.Click += (s, e) =>
            {
                e.Handled = true;
                CloseSession(session);
            };
            Grid.SetColumn(closeButton, 1);
            grid.Children.Add(closeButton);

            tab.Child = grid;

            // Tab click handler
            tab.MouseLeftButtonDown += (s, e) =>
            {
                ShowSession(session);
            };

            // Add to panel
            TabPanel.Children.Add(tab);
            _tabControls[session.SessionId] = tab;
        }

        /// <summary>
        /// Update tab visual states to highlight the active tab
        /// </summary>
        private void UpdateTabHighlights(Guid activeSessionId)
        {
            foreach (var kvp in _tabControls)
            {
                bool isActive = kvp.Key == activeSessionId;

                // Update tab style
                kvp.Value.Style = (Style)FindResource(isActive ? "ActiveExplorerTabStyle" : "ExplorerTabStyle");

                // Update text color based on active state
                if (kvp.Value.Child is Grid grid && grid.Children.Count > 0)
                {
                    // First child is the TextBlock
                    if (grid.Children[0] is TextBlock textBlock)
                    {
                        textBlock.Foreground = (System.Windows.Media.Brush)FindResource(
                            isActive ? "App.Primary.Foreground" : "App.Text.Primary");
                    }

                    // Second child is the close button
                    if (grid.Children.Count > 1 && grid.Children[1] is System.Windows.Controls.Button button)
                    {
                        button.Foreground = (System.Windows.Media.Brush)FindResource(
                            isActive ? "App.Primary.Foreground" : "App.Text.Secondary");
                    }
                }
            }
        }

        /// <summary>
        /// Close a File Explorer session
        /// </summary>
        public void CloseSession(FileExplorerSession session)
        {
            if (_sessionControls.ContainsKey(session.SessionId))
            {
                var control = _sessionControls[session.SessionId];

                // Dispose control
                try
                {
                    control.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[FileExplorer] Error disposing control: {ex.Message}");
                }

                _sessionControls.Remove(session.SessionId);
            }

            // Remove tab
            if (_tabControls.ContainsKey(session.SessionId))
            {
                TabPanel.Children.Remove(_tabControls[session.SessionId]);
                _tabControls.Remove(session.SessionId);
            }

            // Delete from database
            try
            {
                _repository?.Delete(session.SessionId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileExplorer] Failed to delete session from database: {ex.Message}");
            }

            // Raise event
            SessionClosed?.Invoke(this, session);

            // If this was the current session, show another one or empty state
            if (_currentSession?.SessionId == session.SessionId)
            {
                if (_sessionControls.Count > 0)
                {
                    // Show the first available session
                    var nextSession = _sessionControls.Keys.First();
                    var nextSessionModel = new FileExplorerSession { SessionId = nextSession };
                    ShowSession(nextSessionModel);
                }
                else
                {
                    // No more sessions - show empty state
                    ClearSelection();
                    AllSessionsClosed?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Get a session control by ID
        /// </summary>
        public FileExplorerSessionControl? GetSessionControl(Guid sessionId)
        {
            return _sessionControls.ContainsKey(sessionId) ? _sessionControls[sessionId] : null;
        }

        /// <summary>
        /// Clear the selection and show empty state
        /// </summary>
        public void ClearSelection()
        {
            _currentSession = null;
            ExplorerContentHost.Content = null;
            EmptyStatePanel.Visibility = Visibility.Visible;
            NavigationBar.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Close all File Explorer sessions
        /// </summary>
        public void CloseAllSessions()
        {
            var sessions = _sessionControls.Keys.ToList();
            foreach (var sessionId in sessions)
            {
                var session = new FileExplorerSession { SessionId = sessionId };
                CloseSession(session);
            }
        }

        /// <summary>
        /// Check if there are any open sessions
        /// </summary>
        public bool HasOpenSessions => _sessionControls.Count > 0;

        /// <summary>
        /// Get count of open sessions
        /// </summary>
        public int SessionCount => _sessionControls.Count;

        #region Navigation Bar Methods

        private void NavigateBack()
        {
            var control = GetCurrentSessionControl();
            control?.GoBack();
        }

        private void NavigateForward()
        {
            var control = GetCurrentSessionControl();
            control?.GoForward();
        }

        private void NavigateUp()
        {
            var control = GetCurrentSessionControl();
            control?.GoUp();
        }

        private void RefreshCurrent()
        {
            var control = GetCurrentSessionControl();
            control?.Refresh();
        }

        private void PathTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                var newPath = PathTextBox.Text;
                if (!string.IsNullOrWhiteSpace(newPath))
                {
                    var control = GetCurrentSessionControl();
                    control?.Navigate(newPath);
                }
            }
        }

        private void UpdateNavigationBar()
        {
            var control = GetCurrentSessionControl();
            if (control != null)
            {
                // Update path textbox
                PathTextBox.Text = control.CurrentPath;

                // Update button states
                BackButton.IsEnabled = control.CanGoBack;
                ForwardButton.IsEnabled = control.CanGoForward;
            }
        }

        private FileExplorerSessionControl? GetCurrentSessionControl()
        {
            if (_currentSession != null && _sessionControls.ContainsKey(_currentSession.SessionId))
            {
                return _sessionControls[_currentSession.SessionId];
            }
            return null;
        }

        #endregion
    }
}
