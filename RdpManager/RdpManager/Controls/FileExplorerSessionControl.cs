using System;
using System.Collections.Generic;
using System.Windows;
using RdpManager.Models;

namespace RdpManager.Controls
{
    /// <summary>
    /// WPF UserControl that hosts a custom file explorer control to display files and folders
    /// </summary>
    public class FileExplorerSessionControl : System.Windows.Controls.UserControl, IDisposable
    {
        private FileExplorerContentControl? _explorerControl;
        private List<string> _navigationHistory = new List<string>();
        private int _currentHistoryIndex = -1;
        private bool _disposed = false;
        private bool _isNavigatingHistory = false;

        public FileExplorerSession? Session { get; set; }

        public event EventHandler? NavigationCompleted;
        public event EventHandler<string>? NavigationFailed;
        public event EventHandler? HistoryChanged;

        public bool CanGoBack => _currentHistoryIndex > 0;
        public bool CanGoForward => _currentHistoryIndex < _navigationHistory.Count - 1;
        public string CurrentPath => _explorerControl?.CurrentPath ?? string.Empty;

        public FileExplorerSessionControl()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            try
            {
                // Configure UserControl styling
                this.Background = System.Windows.Media.Brushes.Transparent;
                this.BorderBrush = null;
                this.BorderThickness = new Thickness(0);
                this.Padding = new Thickness(0);
                this.Margin = new Thickness(0);
                this.ClipToBounds = true;
                this.UseLayoutRounding = true;
                this.SnapsToDevicePixels = true;

                // Create the custom file explorer control
                _explorerControl = new FileExplorerContentControl();

                // Wire up events
                _explorerControl.PathChanged += ExplorerControl_PathChanged;
                _explorerControl.NavigationFailed += ExplorerControl_NavigationFailed;

                // Set the content of this UserControl
                var container = new System.Windows.Controls.Border
                {
                    Background = System.Windows.Media.Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0),
                    Margin = new Thickness(0),
                    ClipToBounds = true,
                    UseLayoutRounding = true,
                    SnapsToDevicePixels = true,
                    Child = _explorerControl
                };
                this.Content = container;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to initialize File Explorer control: {ex.Message}\n\n{ex.StackTrace}",
                    "File Explorer Initialization Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Navigate to a folder path (UNC or local)
        /// </summary>
        public void Navigate(string folderPath)
        {
            if (_explorerControl == null || string.IsNullOrWhiteSpace(folderPath))
                return;

            try
            {
                System.Diagnostics.Debug.WriteLine($"[FileExplorer] Navigating to: {folderPath}");

                // Update session
                if (Session != null)
                {
                    Session.FolderPath = folderPath;
                    Session.LastAccessedAt = DateTime.Now;
                }

                // Navigate the explorer control
                _explorerControl.NavigateToPath(folderPath);

                // Add to navigation history (unless we're navigating via history)
                if (!_isNavigatingHistory)
                {
                    // Remove any forward history if we're not at the end
                    if (_currentHistoryIndex < _navigationHistory.Count - 1)
                    {
                        _navigationHistory.RemoveRange(_currentHistoryIndex + 1, _navigationHistory.Count - _currentHistoryIndex - 1);
                    }

                    // Add new path to history
                    _navigationHistory.Add(folderPath);
                    _currentHistoryIndex = _navigationHistory.Count - 1;

                    HistoryChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileExplorer] Navigation failed: {ex.Message}");
                NavigationFailed?.Invoke(this, $"Failed to navigate to {folderPath}: {ex.Message}");

                System.Windows.MessageBox.Show($"Failed to open folder: {folderPath}\n\nError: {ex.Message}",
                    "Navigation Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Navigate back in history
        /// </summary>
        public void GoBack()
        {
            if (CanGoBack)
            {
                _isNavigatingHistory = true;
                _currentHistoryIndex--;
                _explorerControl?.NavigateToPath(_navigationHistory[_currentHistoryIndex]);
                _isNavigatingHistory = false;
                HistoryChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Navigate forward in history
        /// </summary>
        public void GoForward()
        {
            if (CanGoForward)
            {
                _isNavigatingHistory = true;
                _currentHistoryIndex++;
                _explorerControl?.NavigateToPath(_navigationHistory[_currentHistoryIndex]);
                _isNavigatingHistory = false;
                HistoryChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Navigate up to parent folder
        /// </summary>
        public void GoUp()
        {
            if (_explorerControl == null) return;

            string currentPath = _explorerControl.CurrentPath;
            if (string.IsNullOrEmpty(currentPath)) return;

            try
            {
                var directory = new System.IO.DirectoryInfo(currentPath);
                if (directory.Parent != null)
                {
                    Navigate(directory.Parent.FullName);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileExplorer] Go up failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Refresh the current view
        /// </summary>
        public void Refresh()
        {
            _explorerControl?.Refresh();
        }

        private void ExplorerControl_PathChanged(object? sender, string newPath)
        {
            System.Diagnostics.Debug.WriteLine($"[FileExplorer] Path changed to: {newPath}");
            NavigationCompleted?.Invoke(this, EventArgs.Empty);

            // Update session
            if (Session != null)
            {
                Session.FolderPath = newPath;
                Session.LastAccessedAt = DateTime.Now;
            }
        }

        private void ExplorerControl_NavigationFailed(object? sender, string error)
        {
            NavigationFailed?.Invoke(this, error);
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                if (_explorerControl != null)
                {
                    _explorerControl.PathChanged -= ExplorerControl_PathChanged;
                    _explorerControl.NavigationFailed -= ExplorerControl_NavigationFailed;
                    _explorerControl = null;
                }

                _disposed = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileExplorer] Error disposing: {ex.Message}");
            }
        }
    }
}
