using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace RdpManager.Controls
{
    /// <summary>
    /// Custom WPF file explorer control that matches the app theme
    /// </summary>
    public class FileExplorerContentControl : System.Windows.Controls.UserControl
    {
        private System.Windows.Controls.ListView? _listView;
        private ObservableCollection<FileSystemItem> _items;
        private string _currentPath = string.Empty;

        public event EventHandler<string>? PathChanged;
        public event EventHandler<string>? NavigationFailed;

        public string CurrentPath => _currentPath;

        public FileExplorerContentControl()
        {
            _items = new ObservableCollection<FileSystemItem>();
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            // Try to get theme resources, fallback to default colors if not available
            var backgroundColor = TryFindResource("App.Background.Primary") as System.Windows.Media.Brush
                ?? System.Windows.Media.Brushes.White;
            var foregroundColor = TryFindResource("App.Text.Primary") as System.Windows.Media.Brush
                ?? System.Windows.Media.Brushes.Black;

            // Create ListView for files and folders
            _listView = new System.Windows.Controls.ListView
            {
                ItemsSource = _items,
                Background = backgroundColor,
                Foreground = foregroundColor,
                BorderThickness = new Thickness(0),
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Stretch,
                VerticalContentAlignment = System.Windows.VerticalAlignment.Stretch,
                Margin = new Thickness(0),
                Padding = new Thickness(8)
            };

            // Create GridView with columns
            var gridView = new GridView
            {
                AllowsColumnReorder = true
            };

            // Name column with icon
            var nameColumn = new GridViewColumn
            {
                Header = "Name",
                Width = 300,
                DisplayMemberBinding = new System.Windows.Data.Binding("Name")
            };
            gridView.Columns.Add(nameColumn);

            // Type column
            var typeColumn = new GridViewColumn
            {
                Header = "Type",
                Width = 120,
                DisplayMemberBinding = new System.Windows.Data.Binding("Type")
            };
            gridView.Columns.Add(typeColumn);

            // Size column
            var sizeColumn = new GridViewColumn
            {
                Header = "Size",
                Width = 100,
                DisplayMemberBinding = new System.Windows.Data.Binding("SizeDisplay")
            };
            gridView.Columns.Add(sizeColumn);

            // Modified column
            var modifiedColumn = new GridViewColumn
            {
                Header = "Date Modified",
                Width = 150,
                DisplayMemberBinding = new System.Windows.Data.Binding("ModifiedDisplay")
            };
            gridView.Columns.Add(modifiedColumn);

            _listView.View = gridView;

            // Handle double-click for folder navigation
            _listView.MouseDoubleClick += ListView_MouseDoubleClick;

            // Wrap in a border for better visibility
            var border = new System.Windows.Controls.Border
            {
                Background = backgroundColor,
                Child = _listView,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                VerticalAlignment = System.Windows.VerticalAlignment.Stretch
            };

            // Set as content
            this.Content = border;
        }

        public void NavigateToPath(string path)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[FileExplorer] NavigateToPath called with: {path}");

                _currentPath = path;
                _items.Clear();

                // Don't use Directory.Exists for UNC paths - it's unreliable with network shares
                // Instead, try to create DirectoryInfo and access it directly
                DirectoryInfo directory;
                try
                {
                    directory = new DirectoryInfo(path);
                    System.Diagnostics.Debug.WriteLine($"[FileExplorer] DirectoryInfo created for: {directory.FullName}");
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Invalid path: {ex.Message}";
                    System.Diagnostics.Debug.WriteLine($"[FileExplorer] {errorMsg}");
                    NavigationFailed?.Invoke(this, errorMsg);

                    System.Windows.MessageBox.Show(
                        $"Cannot access folder:\n{path}\n\n{ex.Message}",
                        "Invalid Path",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                int folderCount = 0;
                int fileCount = 0;
                bool hasError = false;

                // Add folders first
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[FileExplorer] Calling GetDirectories()...");
                    var directories = directory.GetDirectories();
                    System.Diagnostics.Debug.WriteLine($"[FileExplorer] Found {directories.Length} directories");

                    foreach (var dir in directories.OrderBy(d => d.Name))
                    {
                        _items.Add(new FileSystemItem
                        {
                            Name = dir.Name,
                            FullPath = dir.FullName,
                            Type = "File folder",
                            Size = 0,
                            Modified = dir.LastWriteTime,
                            IsDirectory = true
                        });
                        folderCount++;
                    }
                    System.Diagnostics.Debug.WriteLine($"[FileExplorer] Added {folderCount} folders to ObservableCollection");
                }
                catch (UnauthorizedAccessException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[FileExplorer] Access denied to folders: {ex.Message}");
                    hasError = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[FileExplorer] Error reading folders: {ex.GetType().Name} - {ex.Message}");
                    hasError = true;
                    throw; // Re-throw to outer catch
                }

                // Add files
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[FileExplorer] Calling GetFiles()...");
                    var files = directory.GetFiles();
                    System.Diagnostics.Debug.WriteLine($"[FileExplorer] Found {files.Length} files");

                    foreach (var file in files.OrderBy(f => f.Name))
                    {
                        _items.Add(new FileSystemItem
                        {
                            Name = file.Name,
                            FullPath = file.FullName,
                            Type = GetFileType(file.Extension),
                            Size = file.Length,
                            Modified = file.LastWriteTime,
                            IsDirectory = false
                        });
                        fileCount++;
                    }
                    System.Diagnostics.Debug.WriteLine($"[FileExplorer] Added {fileCount} files to ObservableCollection");
                }
                catch (UnauthorizedAccessException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[FileExplorer] Access denied to files: {ex.Message}");
                    hasError = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[FileExplorer] Error reading files: {ex.GetType().Name} - {ex.Message}");
                    hasError = true;
                    throw; // Re-throw to outer catch
                }

                System.Diagnostics.Debug.WriteLine($"[FileExplorer] SUCCESS: Loaded {folderCount} folders and {fileCount} files from {path}");
                System.Diagnostics.Debug.WriteLine($"[FileExplorer] Total items in ObservableCollection: {_items.Count}");

                PathChanged?.Invoke(this, path);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[FileExplorer] Navigation failed: {ex.Message}\n{ex.StackTrace}");
                NavigationFailed?.Invoke(this, $"Failed to navigate: {ex.Message}");

                System.Windows.MessageBox.Show(
                    $"Failed to open folder:\n{path}\n\nError: {ex.Message}",
                    "Navigation Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_listView?.SelectedItem is FileSystemItem item && item.IsDirectory)
            {
                // Navigate to the selected folder
                NavigateToPath(item.FullPath);
            }
        }

        private string GetFileType(string extension)
        {
            if (string.IsNullOrEmpty(extension))
                return "File";

            return extension.TrimStart('.').ToUpperInvariant() + " File";
        }

        public void Refresh()
        {
            if (!string.IsNullOrEmpty(_currentPath))
            {
                NavigateToPath(_currentPath);
            }
        }
    }

    /// <summary>
    /// Represents a file or folder in the file system
    /// </summary>
    public class FileSystemItem : INotifyPropertyChanged
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime Modified { get; set; }
        public bool IsDirectory { get; set; }

        public string SizeDisplay
        {
            get
            {
                if (IsDirectory) return "";
                if (Size < 1024) return $"{Size} bytes";
                if (Size < 1024 * 1024) return $"{Size / 1024:N0} KB";
                if (Size < 1024 * 1024 * 1024) return $"{Size / (1024 * 1024):N0} MB";
                return $"{Size / (1024 * 1024 * 1024):N2} GB";
            }
        }

        public string ModifiedDisplay => Modified.ToString("g");

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
