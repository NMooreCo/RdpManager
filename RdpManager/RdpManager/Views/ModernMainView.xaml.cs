using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using MaterialDesignThemes.Wpf;
using MaterialDesignColors;
using RdpManager.Models;
using RdpManager.Dialogs;
using System.Threading.Tasks;
using System.Linq;
using RdpManager.Commands;
using System.Windows.Forms;
using System.Drawing;
using RdpManager.Views;
using RdpManager.Windows;

namespace RdpManager
{
    public partial class ModernMainView : BorderlessWindowBase
    {
        private Views.MainViewModel _viewModel;
        private DateTime _lastCredentialUpdate;
        private NotifyIcon? _notifyIcon;
        private bool _hasShownMinimizeTip = false;
        private ActiveSessionsView? _activeSessionsView;
        private ComputersView? _computersView;
        private ServersView? _serversView;
        private RecentView? _recentView;
        private FavoritesView? _favoritesView;
        private FileExplorerView? _fileExplorerView;
        private KubernetesView? _kubernetesView;
        private GamesView? _gamesView; // 🎮 Easter egg
        private bool _isSidebarCollapsed = false;

        public ModernMainView()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== ModernMainView Constructor Start ===");

                InitializeComponent();
                System.Diagnostics.Debug.WriteLine("InitializeComponent completed");

                _viewModel = new Views.MainViewModel();
                System.Diagnostics.Debug.WriteLine($"MainViewModel created. NavigationItems count: {_viewModel.NavigationItems.Count}");

                //MainSnackbar.MessageQueue = _snackbarMessageQueue;

                InitializeCommands();
                DataContext = _viewModel;
                System.Diagnostics.Debug.WriteLine("DataContext set");

                // Set theme icon based on current theme
                UpdateThemeIcon();

                UpdateCredentialStatus();
                InitializeSystemTray();

                // Set initial navigation selection AFTER everything is initialized
                Loaded += (s, e) =>
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine("Loaded event firing...");
                        if (NavigationListBox.SelectedIndex < 0)
                        {
                            System.Diagnostics.Debug.WriteLine("Setting SelectedIndex to 0");
                            NavigationListBox.SelectedIndex = 0;
                        }

                        // Set initial window corners
                        UpdateWindowCorners();

                        // Restore previous sessions (show as disconnected)
                        RestorePreviousSessions();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"ERROR in Loaded event: {ex}");
                        System.Windows.MessageBox.Show($"Loaded event error: {ex.Message}\n\n{ex.StackTrace}", "Error");
                    }
                };

                System.Diagnostics.Debug.WriteLine("=== ModernMainView Constructor Complete ===");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FATAL ERROR in ModernMainView constructor: {ex}");
                System.Windows.MessageBox.Show($"Initialization error: {ex.Message}\n\n{ex.StackTrace}", "Fatal Error");
                throw;
            }
        }

        // Expose the ViewModel as a property for binding
        public Views.MainViewModel ViewModel => _viewModel;

        // Commands for keyboard shortcuts
        public ICommand AddCommand { get; private set; } = null!;
        public ICommand EditCommand { get; private set; } = null!;
        public ICommand RemoveCommand { get; private set; } = null!;
        public ICommand ConnectCommand { get; private set; } = null!;
        public ICommand FocusSearchCommand { get; private set; } = null!;
        public ICommand UpdateCredentialsCommand { get; private set; } = null!;
        public ICommand ToggleThemeCommand { get; private set; } = null!;
        public ICommand ImportCommand { get; private set; } = null!;
        public ICommand ExportCommand { get; private set; } = null!;
        public ICommand ClearSearchCommand { get; private set; } = null!;

        private void InitializeCommands()
        {
            AddCommand = new RelayCommand(() => AddButton_Click(null, null));
            EditCommand = new RelayCommand(() => EditButton_Click(null, null));
            RemoveCommand = new RelayCommand(() => RemoveButton_Click(null, null));
            ConnectCommand = new RelayCommand(() => ConnectButton_Click(null, null));
            FocusSearchCommand = new RelayCommand(() => { /* Search moved to tab content */ });
            UpdateCredentialsCommand = new RelayCommand(() => QuickUpdateCredentials_Click(null, null));
            ToggleThemeCommand = new RelayCommand(() => { /* Theme toggle removed in professional UI */ });
            ImportCommand = new RelayCommand(() => ImportButton_Click(null, null));
            ExportCommand = new RelayCommand(() => ExportButton_Click(null, null));
            ClearSearchCommand = new RelayCommand(() => { /* Search moved to tab content */ });
        }

        private void InitializeSystemTray()
        {
            Task.Run(() =>
            {
                System.Threading.Thread.Sleep(500);
                this.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        _notifyIcon = new NotifyIcon
                        {
                            Icon = new Icon("icon.ico"),
                            Visible = true,
                            Text = "RDP Manager",
                            ContextMenuStrip = CreateSystemTrayMenu()
                        };
                        _notifyIcon.DoubleClick += (s, e) =>
                        {
                            Show();
                            WindowState = WindowState.Normal;
                            Activate();
                        };
                    }
                    catch { }
                });
            });
        }

        private System.Windows.Forms.ContextMenuStrip CreateSystemTrayMenu()
        {
            var menu = new System.Windows.Forms.ContextMenuStrip();

            // Open/Restore
            var openItem = new System.Windows.Forms.ToolStripMenuItem("Open", null, (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                    Topmost = true;
                    Topmost = false;
                    Focus();
                });
            });
            menu.Items.Add(openItem);

            // Update Credentials
            var updateCredentialsItem = new System.Windows.Forms.ToolStripMenuItem("Update Credentials", null, (s, e) =>
            {
                Dispatcher.Invoke(() => QuickUpdateCredentials_Click(null, null));
            });
            menu.Items.Add(updateCredentialsItem);

            // Separator
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

            // Close/Exit
            var exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit", null, (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (_notifyIcon != null)
                    {
                        _notifyIcon.Visible = false;
                    }
                    System.Windows.Application.Current.Shutdown();
                });
            });
            menu.Items.Add(exitItem);

            return menu;
        }

        private System.Windows.Controls.TreeView? _defaultTreeView;

        private void UpdateCredentialStatus()
        {
            var creds = MainController.LoadCredentials("RdpManagerCredentials");
            if (creds != null)
            {
                _lastCredentialUpdate = DateTime.Now;
            }
        }

        private void ThemeToggle_Click(object? sender, RoutedEventArgs? e)
        {
            var themeManager = RdpManager.Helpers.ThemeManager.Instance;
            themeManager.ToggleTheme();
            UpdateThemeIcon();
            // _snackbarMessageQueue.Enqueue($"Switched to {(themeManager.IsDarkMode() ? "Dark" : "Light")} mode");
        }

        private void UpdateThemeIcon()
        {
            var themeManager = RdpManager.Helpers.ThemeManager.Instance;

            if (themeManager.IsDarkMode())
            {
                // Dark mode - show sun icon (click to go light)
                ThemeIcon.Data = Geometry.Parse("M12,7A5,5 0 0,1 17,12A5,5 0 0,1 12,17A5,5 0 0,1 7,12A5,5 0 0,1 12,7M12,9A3,3 0 0,0 9,12A3,3 0 0,0 12,15A3,3 0 0,0 15,12A3,3 0 0,0 12,9M12,2L14.39,5.42C13.65,5.15 12.84,5 12,5C11.16,5 10.35,5.15 9.61,5.42L12,2M3.34,7L7.5,6.65C6.9,7.16 6.36,7.78 5.94,8.5C5.5,9.24 5.25,10 5.11,10.79L3.34,7M3.36,17L5.12,13.23C5.26,14 5.53,14.78 5.95,15.5C6.37,16.24 6.91,16.86 7.5,17.37L3.36,17M20.65,7L18.88,10.79C18.74,10 18.47,9.23 18.05,8.5C17.63,7.78 17.1,7.15 16.5,6.64L20.65,7M20.64,17L16.5,17.36C17.09,16.85 17.62,16.22 18.04,15.5C18.46,14.77 18.73,14 18.87,13.21L20.64,17M12,22L9.59,18.56C10.33,18.83 11.14,19 12,19C12.82,19 13.63,18.83 14.37,18.56L12,22Z");
            }
            else
            {
                // Light mode - show moon icon (click to go dark)
                ThemeIcon.Data = Geometry.Parse("M17.75,4.09L15.22,6.03L16.13,9.09L13.5,7.28L10.87,9.09L11.78,6.03L9.25,4.09L12.44,4L13.5,1L14.56,4L17.75,4.09M21.25,11L19.61,12.25L20.2,14.23L18.5,13.06L16.8,14.23L17.39,12.25L15.75,11L17.81,10.95L18.5,9L19.19,10.95L21.25,11M18.97,15.95C19.8,15.87 20.69,17.05 20.16,17.8C19.84,18.25 19.5,18.67 19.08,19.07C15.17,23 8.84,23 4.94,19.07C1.03,15.17 1.03,8.83 4.94,4.93C5.34,4.53 5.76,4.17 6.21,3.85C6.96,3.32 8.14,4.21 8.06,5.04C7.79,7.9 8.75,10.87 10.95,13.06C13.14,15.26 16.1,16.22 18.97,15.95M17.33,17.97C14.5,17.81 11.7,16.64 9.53,14.5C7.36,12.31 6.2,9.5 6.04,6.68C3.23,9.82 3.34,14.64 6.35,17.66C9.37,20.67 14.19,20.78 17.33,17.97Z");
            }
        }

        private async void QuickUpdateCredentials_Click(object? sender, RoutedEventArgs? e)
        {
            await ShowCredentialsDialog();
        }

        private async Task ShowCredentialsDialog()
        {
            try
            {
                var dialog = new CredentialsDialog(_viewModel);
                var result = await DialogHost.Show(dialog, "RootDialogHost");

                if (result is bool b && b)
                {
                    UpdateCredentialStatus();
                    // _snackbarMessageQueue.Enqueue("Credentials updated successfully");
                }
            }
            catch (InvalidOperationException)
            {
                // DialogHost not ready yet - show as a regular window instead
                var dialog = new CredentialsDialog(_viewModel);
                var window = new Window
                {
                    Content = dialog,
                    SizeToContent = SizeToContent.WidthAndHeight,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    Title = "Update Credentials"
                };

                if (window.ShowDialog() == true)
                {
                    UpdateCredentialStatus();
                    // _snackbarMessageQueue.Enqueue("Credentials updated successfully");
                }
            }
        }

        private async void AddButton_Click(object? sender, RoutedEventArgs? e)
        {
            try
            {
                var dialog = new AddEditComputerDialog();
                var result = await DialogHost.Show(dialog, "RootDialogHost");

                if (result is ComputerEntry entry)
                {
                    _viewModel.AddEntry(entry);
                    // _snackbarMessageQueue.Enqueue($"Added {entry.FriendlyName}");

                    // No need to refresh view - RootNodes ObservableCollection automatically updates the UI
                }
            }
            catch (InvalidOperationException)
            {
                // Fallback if DialogHost not ready
                // _snackbarMessageQueue.Enqueue("Dialog system not ready. Please try again.");
            }
        }

        private async void DuplicateComputer(ComputerEntry originalEntry)
        {
            try
            {
                // Create a copy with modified name
                var duplicateEntry = new ComputerEntry
                {
                    MachineName = originalEntry.MachineName,
                    FriendlyName = originalEntry.FriendlyName + " - Copy",
                    Domain = originalEntry.Domain,
                    Group = originalEntry.Group
                };

                var dialog = new AddEditComputerDialog(duplicateEntry);
                var result = await DialogHost.Show(dialog, "RootDialogHost");

                if (result is ComputerEntry entry)
                {
                    _viewModel.AddEntry(entry);
                    // _snackbarMessageQueue.Enqueue($"Duplicated {entry.FriendlyName}");
                }
            }
            catch (InvalidOperationException)
            {
                // _snackbarMessageQueue.Enqueue("Dialog system not ready. Please try again.");
            }
        }

        private async void RenameGroup(Models.TreeNode groupNode)
        {
            try
            {
                if (string.IsNullOrEmpty(groupNode.GroupPath))
                    return;

                var groupRepo = new RdpManager.Data.Repositories.GroupRepository();
                var group = groupRepo.GetByPath(groupNode.GroupPath);
                if (group == null)
                {
                    // _snackbarMessageQueue.Enqueue("Group not found in database");
                    return;
                }

                var dialog = new RdpManager.Dialogs.GroupRenameDialog(group.Name);
                var result = await DialogHost.Show(dialog, "RootDialogHost");

                if (result is string newName && !string.IsNullOrEmpty(newName) && newName != group.Name)
                {
                    System.Diagnostics.Debug.WriteLine($"Renaming group '{group.Name}' to '{newName}'");

                    var oldPath = group.FullPath;
                    var newPath = string.IsNullOrEmpty(group.ParentPath) ? newName : $"{group.ParentPath}/{newName}";

                    System.Diagnostics.Debug.WriteLine($"  Old path: {oldPath}");
                    System.Diagnostics.Debug.WriteLine($"  New path: {newPath}");

                    // Update group in database
                    group.Name = newName;
                    group.FullPath = newPath;
                    group.UpdatedAt = DateTime.UtcNow;
                    groupRepo.Update(group);

                    // Update all child groups' paths
                    var allGroups = groupRepo.GetAll();
                    var childGroups = allGroups.Where(g => g.FullPath.StartsWith(oldPath + "/")).ToList();

                    System.Diagnostics.Debug.WriteLine($"  Updating {childGroups.Count} child groups");
                    foreach (var childGroup in childGroups)
                    {
                        var oldChildPath = childGroup.FullPath;
                        var newChildPath = newPath + oldChildPath.Substring(oldPath.Length);
                        childGroup.FullPath = newChildPath;

                        // Update parent path if it was the renamed group
                        if (childGroup.ParentPath == oldPath)
                        {
                            childGroup.ParentPath = newPath;
                        }

                        childGroup.UpdatedAt = DateTime.UtcNow;
                        groupRepo.Update(childGroup);
                    }

                    // Update all computers in this group and child groups
                    var computerRepo = new RdpManager.Data.Repositories.ComputerRepository();
                    var allComputers = computerRepo.GetAll();
                    var affectedComputers = allComputers.Where(c =>
                        c.Group == oldPath || c.Group?.StartsWith(oldPath + "/") == true).ToList();

                    System.Diagnostics.Debug.WriteLine($"  Updating {affectedComputers.Count} computers");
                    foreach (var computer in affectedComputers)
                    {
                        if (computer.Group == oldPath)
                        {
                            computer.Group = newPath;
                        }
                        else if (computer.Group?.StartsWith(oldPath + "/") == true)
                        {
                            computer.Group = newPath + computer.Group.Substring(oldPath.Length);
                        }
                        computer.UpdatedAt = DateTime.UtcNow;
                        computerRepo.Update(computer);
                    }

                    // Reload and rebuild tree
                    var computers = computerRepo.GetAll();
                    _viewModel.ComputerEntries = new System.Collections.ObjectModel.ObservableCollection<ComputerEntry>(computers);
                    _viewModel.BuildComputerEntriesTree();

                    // _snackbarMessageQueue.Enqueue($"Renamed group to '{newName}'");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error renaming group: {ex.Message}\n{ex.StackTrace}");
                // _snackbarMessageQueue.Enqueue($"Error renaming group: {ex.Message}");
            }
        }

        private void ConnectAllInGroup(Models.TreeNode groupNode)
        {
            try
            {
                // Get all direct child computers (not nested in sub-groups)
                var directComputers = groupNode.Children
                    .Where(child => child.ComputerEntry != null)
                    .Select(child => child.ComputerEntry!)
                    .ToList();

                if (!directComputers.Any())
                {
                    // _snackbarMessageQueue.Enqueue("No computers in this group");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"Connecting to {directComputers.Count} computers in group '{groupNode.Name}'");

                // Connect to each computer
                foreach (var computer in directComputers)
                {
                    var serverAddress = computer.MachineName;
                    if (!string.IsNullOrEmpty(computer.Domain))
                    {
                        serverAddress = $"{computer.MachineName}.{computer.Domain}";
                    }

                    ConnectEmbedded(serverAddress, computer.FriendlyName, computer.Domain, computer.Id, computer.Group);
                }

                // Switch to Active Sessions tab
                NavigationListBox.SelectedIndex = 4;

                // _snackbarMessageQueue.Enqueue($"Connecting to {directComputers.Count} computers");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error connecting to group: {ex.Message}\n{ex.StackTrace}");
                // _snackbarMessageQueue.Enqueue($"Error: {ex.Message}");
            }
        }

        private void EnsureActiveSessionsViewInitialized()
        {
            if (_activeSessionsView == null)
            {
                _activeSessionsView = new ActiveSessionsView();

                // Wire up session reconnecting event to refresh Recent view
                _activeSessionsView.SessionReconnecting += (s, session) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[SessionReconnecting EVENT] Session reconnecting: {session.FriendlyName}");

                    // The database is automatically updated when connection starts (new ConnectionLog entry created)
                    // Just rebuild the Recent tree to reflect the new connection
                    _viewModel.BuildRecentConnectionsTree();
                    System.Diagnostics.Debug.WriteLine($"  ✓ Rebuilt Recent connections tree from database");
                };
            }
        }

        private void EnsureFileExplorerViewInitialized()
        {
            if (_fileExplorerView == null)
            {
                _fileExplorerView = new FileExplorerView();

                // Wire up events
                _fileExplorerView.SessionClosed += (s, session) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[FileExplorer] Session closed: {session.DisplayName}");

                    // Remove from ViewModel collection
                    var sessionToRemove = _viewModel.FileExplorerSessions.FirstOrDefault(s => s.SessionId == session.SessionId);
                    if (sessionToRemove != null)
                    {
                        _viewModel.FileExplorerSessions.Remove(sessionToRemove);
                    }
                };

                _fileExplorerView.AllSessionsClosed += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine("[FileExplorer] All sessions closed - switching to Computers view");

                    // Switch to Computers view
                    NavigationListBox.SelectedIndex = 0;
                };
            }
        }

        private void ShowGamesTab()
        {
            System.Diagnostics.Debug.WriteLine("🎮 Showing secret Games tab!");

            // Create games view if needed
            if (_gamesView == null)
            {
                _gamesView = new GamesView();
                _gamesView.RequestClose += (s, e) =>
                {
                    System.Diagnostics.Debug.WriteLine("🚪 Games tab requested close");
                    // Remove games nav item and go back to Computers
                    var gamesNavItem = _viewModel.NavigationItems.FirstOrDefault(n => n.IsTemporary && n.SectionIndex == 99);
                    if (gamesNavItem != null)
                    {
                        _viewModel.NavigationItems.Remove(gamesNavItem);
                    }
                    NavigationListBox.SelectedIndex = 0; // Back to Computers
                };
            }

            // Add temporary navigation item
            var existing = _viewModel.NavigationItems.FirstOrDefault(n => n.SectionIndex == 99);
            if (existing == null)
            {
                var gamesNavItem = new NavigationItem
                {
                    Type = NavigationItemType.Section,
                    Name = "🎮 Games",
                    IconData = "M12,2A2,2 0 0,1 14,4V4.28L20,8V16H4V8L10,4.28V4A2,2 0 0,1 12,2M14,6V12H18V10L14,8V6M4,18V20A2,2 0 0,0 6,22H18A2,2 0 0,0 20,20V18H4Z",
                    SectionIndex = 99,
                    IsTemporary = true,
                    IsVisible = true
                };

                _viewModel.NavigationItems.Add(gamesNavItem);
                System.Diagnostics.Debug.WriteLine("  Added Games navigation item");
            }

            // Select it
            var gamesNav = _viewModel.NavigationItems.FirstOrDefault(n => n.SectionIndex == 99);
            if (gamesNav != null)
            {
                NavigationListBox.SelectedItem = gamesNav;
            }
        }

        private void CloseSession(RdpSession session)
        {
            System.Diagnostics.Debug.WriteLine($"CloseSession called for: {session.FriendlyName}");

            // Mark session as closed in database
            if (session.ConnectionLogId > 0)
            {
                try
                {
                    var connectionLogRepo = new RdpManager.Data.Repositories.ConnectionLogRepository();
                    connectionLogRepo.CloseSession(session.ConnectionLogId);
                    System.Diagnostics.Debug.WriteLine($"  Marked session #{session.ConnectionLogId} as closed in database");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"  Error marking session as closed: {ex.Message}");
                }
            }

            // Remove from ActiveSessionsView
            _activeSessionsView?.RemoveSession(session);

            // Remove from ViewModel collection
            _viewModel.ActiveSessions.Remove(session);

            System.Diagnostics.Debug.WriteLine($"  Session removed from Active Sessions");
        }

        private void RestorePreviousSessions()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== RestorePreviousSessions called ===");

                var connectionLogRepo = new RdpManager.Data.Repositories.ConnectionLogRepository();
                var activeSessions = connectionLogRepo.GetActiveSessions();

                System.Diagnostics.Debug.WriteLine($"Found {activeSessions.Count} active session(s) in database");

                if (activeSessions.Count > 0)
                {
                    foreach (var log in activeSessions)
                    {
                        System.Diagnostics.Debug.WriteLine($"  Log #{log.Id}: {log.FriendlyName ?? log.ServerAddress} (ComputerId: {log.ComputerId}, EndTime: {log.EndTime})");

                        // Create a session - always restore as Disconnected (user can reconnect manually)
                        var session = new RdpSession
                        {
                            ServerAddress = log.ServerAddress,
                            FriendlyName = log.FriendlyName ?? log.ServerAddress,
                            Domain = log.Domain,
                            Group = log.GroupPath,
                            ComputerId = log.ComputerId,
                            ConnectionLogId = log.Id,
                            ConnectionState = RdpConnectionState.Disconnected
                        };

                        // Add to ViewModel's collection
                        _viewModel.ActiveSessions.Add(session);

                        System.Diagnostics.Debug.WriteLine($"    → Added to ActiveSessions as {session.ConnectionState}");
                    }

                    System.Diagnostics.Debug.WriteLine($"ActiveSessions now has {_viewModel.ActiveSessions.Count} session(s)");

                    // NOTE: Don't call ShowSession() here - it would auto-connect all of them!
                    // Sessions will be shown when user clicks on them in the navigation

                    System.Diagnostics.Debug.WriteLine($"=== Restored {activeSessions.Count} previous session(s) successfully ===");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("No active sessions found");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR restoring sessions: {ex.Message}\n{ex.StackTrace}");
                System.Windows.MessageBox.Show($"Error restoring sessions: {ex.Message}", "Session Restore Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void EditButton_Click(object? sender, RoutedEventArgs? e)
        {
            // Try to get selected item from active view
            Models.TreeNode? selectedNode = null;

            // Check if we have a selected item in the ViewModel
            if (_viewModel.SelectedItem is Models.TreeNode vmNode)
            {
                selectedNode = vmNode;
            }
            // Otherwise try to get from the current TreeView
            else if (ContentFrame.Content is System.Windows.Controls.TreeView treeView)
            {
                selectedNode = treeView.SelectedItem as Models.TreeNode;
            }

            if (selectedNode != null && selectedNode.ComputerEntry != null)
            {
                try
                {
                    var dialog = new AddEditComputerDialog(selectedNode.ComputerEntry);
                    var result = await DialogHost.Show(dialog, "RootDialogHost");

                    if (result is ComputerEntry entry)
                    {
                        // Update the entry in-place to preserve the database ID
                        var existingEntry = selectedNode.ComputerEntry;
                        bool groupChanged = existingEntry.Group != entry.Group;
                        bool nameChanged = existingEntry.FriendlyName != entry.FriendlyName;

                        // Store old values before updating
                        var oldGroup = existingEntry.Group;

                        existingEntry.FriendlyName = entry.FriendlyName;
                        existingEntry.MachineName = entry.MachineName;
                        existingEntry.Domain = entry.Domain;
                        existingEntry.Group = entry.Group;
                        existingEntry.UpdatedAt = DateTime.UtcNow;

                        SaveEntries();

                        if (groupChanged || nameChanged)
                        {
                            // Need to reposition the node - remove from old location with old group
                            var tempEntry = new ComputerEntry
                            {
                                MachineName = existingEntry.MachineName,
                                Group = oldGroup
                            };
                            _viewModel.RemoveEntryFromTree(tempEntry);
                            _viewModel.AddEntryToTree(existingEntry);
                        }

                        // _snackbarMessageQueue.Enqueue($"Updated {entry.FriendlyName}");
                    }
                }
                catch (InvalidOperationException)
                {
                    // _snackbarMessageQueue.Enqueue("Dialog system not ready. Please try again.");
                }
            }
            else
            {
                // _snackbarMessageQueue.Enqueue("Please select a computer entry to edit");
            }
        }

        private async void RemoveButton_Click(object? sender, RoutedEventArgs? e)
        {
            // Try to get selected item from active view
            Models.TreeNode? selectedNode = null;

            // Check if we have a selected item in the ViewModel
            if (_viewModel.SelectedItem is Models.TreeNode vmNode)
            {
                selectedNode = vmNode;
            }
            // Otherwise try to get from the current TreeView
            else if (ContentFrame.Content is System.Windows.Controls.TreeView treeView)
            {
                selectedNode = treeView.SelectedItem as Models.TreeNode;
            }

            if (selectedNode != null && selectedNode.ComputerEntry != null)
            {
                try
                {
                    var dialog = new ConfirmDialog($"Remove {selectedNode.ComputerEntry.FriendlyName}?",
                        "This action cannot be undone.");
                    var result = await DialogHost.Show(dialog, "RootDialogHost");

                    // DialogHost returns "True" as string when confirmed
                    if (result?.ToString() == "True" || result is bool b && b)
                    {
                        var friendlyName = selectedNode.ComputerEntry.FriendlyName;

                        // Use the ViewModel's RemoveEntry method which handles the removal properly
                        _viewModel.RemoveEntry(selectedNode.ComputerEntry);
                        _viewModel.SelectedItem = null; // Clear selection
                        // _snackbarMessageQueue.Enqueue($"Removed {friendlyName}");

                        // No need to replace views - RemoveEntry rebuilds tree and triggers UI refresh
                    }
                }
                catch (InvalidOperationException)
                {
                    // Fallback to direct removal with confirmation
                    if (System.Windows.MessageBox.Show($"Remove {selectedNode.ComputerEntry.FriendlyName}?\n\nThis action cannot be undone.",
                        "Confirm Remove", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        // Use the ViewModel's RemoveEntry method which handles the removal properly
                        _viewModel.RemoveEntry(selectedNode.ComputerEntry);
                        SaveEntries();
                        _viewModel.SelectedItem = null; // Clear selection
                        // _snackbarMessageQueue.Enqueue($"Removed {selectedNode.ComputerEntry.FriendlyName}");

                        // Refresh the current view
                        switch (NavigationListBox.SelectedIndex)
                        {
                            case 0: // Computers
                                _defaultTreeView = CreateTreeView(_viewModel.RootNodes, true);
                                ContentFrame.Content = _defaultTreeView;
                                break;
                            case 3: // Recent
                                ContentFrame.Content = CreateTreeView(_viewModel.RootNodesRecent, true);
                                break;
                            case 4: // Favorites
                                ContentFrame.Content = CreateTreeView(_viewModel.RootNodesFavorites, true);
                                break;
                        }
                    }
                }
            }
            else
            {
                // _snackbarMessageQueue.Enqueue("Please select a computer entry to remove");
            }
        }

        private void ConnectButton_Click(object? sender, RoutedEventArgs? e)
        {
            if (_viewModel.SelectedItem is Models.TreeNode node)
            {
                if (node.ComputerEntry != null)
                {
                    if (_viewModel.UseEmbeddedRdp)
                    {
                        ConnectEmbedded(node.ComputerEntry.MachineName, node.ComputerEntry.FriendlyName, node.ComputerEntry.Domain, node.ComputerEntry.Id);
                    }
                    else
                    {
                        MainController.ConnectToMachine(node.ComputerEntry.MachineName, node.ComputerEntry.FriendlyName);

                        // Log external connection start
                        if (node.ComputerEntry.Id > 0)
                        {
                            try
                            {
                                var connectionLogRepo = new RdpManager.Data.Repositories.ConnectionLogRepository();
                                var serverAddr = node.ComputerEntry.MachineName;
                                if (!string.IsNullOrEmpty(node.ComputerEntry.Domain))
                                    serverAddr = $"{node.ComputerEntry.MachineName}.{node.ComputerEntry.Domain}";

                                connectionLogRepo.StartConnection(
                                    node.ComputerEntry.Id,
                                    "External",
                                    serverAddr,
                                    node.ComputerEntry.FriendlyName,
                                    node.ComputerEntry.Domain,
                                    node.ComputerEntry.Group);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error logging external connection: {ex.Message}");
                            }
                        }
                    }
                    // _snackbarMessageQueue.Enqueue($"Connecting to {node.ComputerEntry.FriendlyName}");

                    // Note: Recent connections will be updated from database when session disconnects or is closed
                }
                else if (node.Server != null)
                {
                    string address = !string.IsNullOrEmpty(node.Server.Domain) ? $"{node.Server.ServerName}.{node.Server.Domain}" : node.Server.ServerName;
                    if (_viewModel.UseEmbeddedRdp)
                    {
                        ConnectEmbedded(address, node.Server.ServerName, node.Server.Domain);
                    }
                    else
                    {
                        ConnectToServer(node.Server);
                    }
                    _viewModel.AddConnectionToHistory(node.Server.ServerName, node.Server.ServerName, node.Server.Domain, node.Server.Application);
                    // _snackbarMessageQueue.Enqueue($"Connecting to {node.Server.ServerName}");

                    // Refresh Recent view if currently showing
                    if (NavigationListBox.SelectedIndex == 2)
                    {
                        ContentFrame.Content = CreateTreeView(_viewModel.RootNodesRecent, true);
                    }
                }
            }
            else
            {
                // _snackbarMessageQueue.Enqueue("Please select an entry to connect");
            }
        }

        private void ConnectEmbedded(string serverAddress, string friendlyName, string? domain, long computerId = 0, string? groupPath = null)
        {
            // Check if session already exists for this server
            var existingSession = _viewModel.ActiveSessions.FirstOrDefault(
                s => s.ServerAddress.Equals(serverAddress, StringComparison.OrdinalIgnoreCase));

            if (existingSession != null)
            {
                // Focus existing session instead of creating duplicate
                System.Diagnostics.Debug.WriteLine($"Session already exists for {serverAddress}, switching to it");

                // Create view if needed and wire up events
                EnsureActiveSessionsViewInitialized();

                // Show the existing session
                _activeSessionsView.ShowSession(existingSession);

                // Find and select the session's navigation item
                var sessionNavItem = _viewModel.NavigationItems.FirstOrDefault(
                    n => n.Session?.SessionId == existingSession.SessionId);

                if (sessionNavItem != null)
                {
                    NavigationListBox.SelectedItem = sessionNavItem;
                }

                // _snackbarMessageQueue.Enqueue($"Switched to existing session: {existingSession.DisplayName}");
                return;
            }

            // Create the Active Sessions view if it doesn't exist and wire up events
            EnsureActiveSessionsViewInitialized();

            // Create a new RDP session
            var session = new RdpSession
            {
                ServerAddress = serverAddress,
                FriendlyName = friendlyName,
                Domain = domain,
                Group = groupPath ?? (_viewModel.SelectedItem is Models.TreeNode node ?
                        (node.ComputerEntry?.Group ?? node.Server?.Application) : null),
                ComputerId = computerId
            };

            // Start connection logging if this is a computer (not server)
            if (computerId > 0)
            {
                try
                {
                    var connectionLogRepo = new RdpManager.Data.Repositories.ConnectionLogRepository();
                    session.ConnectionLogId = connectionLogRepo.StartConnection(
                        computerId,
                        "Embedded",
                        serverAddress,
                        friendlyName,
                        domain,
                        session.Group);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error starting connection log: {ex.Message}");
                }
            }

            // Add to ViewModel's collection
            _viewModel.ActiveSessions.Add(session);

            // Show the session in the view
            _activeSessionsView.ShowSession(session);

            // Set ContentFrame and remove borders for fullscreen RDP
            ContentFrame.Content = _activeSessionsView;
            SetFullScreenRdpMode();

            // Switch to Active Sessions tab - select the newly added session
            var newSessionNavItem = _viewModel.NavigationItems.FirstOrDefault(
                n => n.Session?.SessionId == session.SessionId);

            if (newSessionNavItem != null)
            {
                NavigationListBox.SelectedItem = newSessionNavItem;
            }
        }

        private void QuickConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is Models.TreeNode node)
            {
                if (node.ComputerEntry != null)
                {
                    if (_viewModel.UseEmbeddedRdp)
                    {
                        ConnectEmbedded(node.ComputerEntry.MachineName, node.ComputerEntry.FriendlyName, node.ComputerEntry.Domain, node.ComputerEntry.Id);
                    }
                    else
                    {
                        MainController.ConnectToMachine(node.ComputerEntry.MachineName, node.ComputerEntry.FriendlyName);

                        // Log external connection start
                        if (node.ComputerEntry.Id > 0)
                        {
                            try
                            {
                                var connectionLogRepo = new RdpManager.Data.Repositories.ConnectionLogRepository();
                                var serverAddr = node.ComputerEntry.MachineName;
                                if (!string.IsNullOrEmpty(node.ComputerEntry.Domain))
                                    serverAddr = $"{node.ComputerEntry.MachineName}.{node.ComputerEntry.Domain}";

                                connectionLogRepo.StartConnection(
                                    node.ComputerEntry.Id,
                                    "External",
                                    serverAddr,
                                    node.ComputerEntry.FriendlyName,
                                    node.ComputerEntry.Domain,
                                    node.ComputerEntry.Group);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error logging external connection: {ex.Message}");
                            }
                        }
                    }
                    // _snackbarMessageQueue.Enqueue($"Connecting to {node.ComputerEntry.FriendlyName}");

                    // Note: Recent connections will be updated from database when session disconnects or is closed
                }
                else if (node.Server != null)
                {
                    string address = !string.IsNullOrEmpty(node.Server.Domain) ? $"{node.Server.ServerName}.{node.Server.Domain}" : node.Server.ServerName;
                    if (_viewModel.UseEmbeddedRdp)
                    {
                        ConnectEmbedded(address, node.Server.ServerName, node.Server.Domain);
                    }
                    else
                    {
                        ConnectToServer(node.Server);
                    }
                    _viewModel.AddConnectionToHistory(node.Server.ServerName, node.Server.ServerName, node.Server.Domain, node.Server.Application);
                    // _snackbarMessageQueue.Enqueue($"Connecting to {node.Server.ServerName}");

                    // Refresh Recent view if currently showing
                    if (NavigationListBox.SelectedIndex == 2)
                    {
                        ContentFrame.Content = CreateTreeView(_viewModel.RootNodesRecent, true);
                    }
                }
            }
        }

        private void FavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is Models.TreeNode node)
            {
                if (node.ComputerEntry != null)
                {
                    _viewModel.ToggleFavorite(node.ComputerEntry.MachineName, node.ComputerEntry.Domain,
                        node.ComputerEntry.FriendlyName, node.ComputerEntry.Group);
                    bool isFav = _viewModel.IsFavorite(node.ComputerEntry.MachineName, node.ComputerEntry.Domain);
                    node.IsFavorite = isFav; // Update the node's favorite status
                    // _snackbarMessageQueue.Enqueue(isFav ? $"Added {node.ComputerEntry.FriendlyName} to favorites" : $"Removed {node.ComputerEntry.FriendlyName} from favorites");

                    // Rebuild trees to update UI
                    _viewModel.BuildComputerEntriesTree();
                    _viewModel.BuildRecentConnectionsTree();
                    _viewModel.BuildFavoritesTree();

                    // If we're on the Favorites view and unfavorited, refresh the view
                    if (NavigationListBox.SelectedIndex == 3 && !isFav)
                    {
                        ContentFrame.Content = CreateTreeView(_viewModel.RootNodesFavorites, true);
                    }
                    // If we're on the Recent view, refresh it too
                    else if (NavigationListBox.SelectedIndex == 2)
                    {
                        ContentFrame.Content = CreateTreeView(_viewModel.RootNodesRecent, true);
                    }
                }
                else if (node.Server != null)
                {
                    _viewModel.ToggleFavorite(node.Server.ServerName, node.Server.Domain,
                        node.Server.ServerName, node.Server.Application);
                    bool isFav = _viewModel.IsFavorite(node.Server.ServerName, node.Server.Domain);
                    node.IsFavorite = isFav; // Update the node's favorite status
                    // _snackbarMessageQueue.Enqueue(isFav ? $"Added {node.Server.ServerName} to favorites" : $"Removed {node.Server.ServerName} from favorites");

                    // Rebuild trees to update UI
                    _viewModel.BuildServerEntriesTree();
                    _viewModel.BuildRecentConnectionsTree();
                    _viewModel.BuildFavoritesTree();

                    // If we're on the Favorites view and unfavorited, refresh the view
                    if (NavigationListBox.SelectedIndex == 3 && !isFav)
                    {
                        ContentFrame.Content = CreateTreeView(_viewModel.RootNodesFavorites, true);
                    }
                    // If we're on the Recent view, refresh it too
                    else if (NavigationListBox.SelectedIndex == 2)
                    {
                        ContentFrame.Content = CreateTreeView(_viewModel.RootNodesRecent, true);
                    }
                }
            }
        }

        private void ConnectToServer(Server server)
        {
            string address = !string.IsNullOrEmpty(server.Domain) ? $"{server.ServerName}.{server.Domain}" : server.ServerName;
            MainController.ConnectToMachine(address);
        }

        private void NavigationListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"NavigationListBox_SelectionChanged fired. SelectedItem type: {NavigationListBox.SelectedItem?.GetType().Name}");

                if (NavigationListBox.SelectedItem is not NavigationItem navItem)
                {
                    System.Diagnostics.Debug.WriteLine("SelectedItem is not NavigationItem, returning");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"NavigationItem: {navItem.Name}, SectionIndex: {navItem.SectionIndex}");

                // Check if viewModel is initialized
                if (_viewModel == null)
                {
                    System.Diagnostics.Debug.WriteLine("ViewModel is null");
                    return;
                }
                //if (_snackbarMessageQueue == null)
                //{
                //    System.Diagnostics.Debug.WriteLine("SnackbarMessageQueue is null");
                //    return;
                //}

            // Remove temporary navigation items when switching away (except to the temp item itself)
            if (!navItem.IsTemporary)
            {
                var tempItems = _viewModel.NavigationItems.Where(n => n.IsTemporary).ToList();
                foreach (var tempItem in tempItems)
                {
                    System.Diagnostics.Debug.WriteLine($"Removing temporary nav item: {tempItem.Name}");
                    _viewModel.NavigationItems.Remove(tempItem);
                }
            }

            // Handle session selection
            if (navItem.IsSession && navItem.Session != null)
            {
                // Show this specific session in ActiveSessionsView
                EnsureActiveSessionsViewInitialized();

                // Only auto-connect if session is in Connecting/Connected state
                // Disconnected/Failed sessions require user to manually reconnect
                bool shouldAutoConnect = navItem.Session.ConnectionState == RdpConnectionState.Connecting ||
                                        navItem.Session.ConnectionState == RdpConnectionState.Connected ||
                                        navItem.Session.ConnectionState == RdpConnectionState.Reconnecting;

                _activeSessionsView.ShowSession(navItem.Session, autoConnect: shouldAutoConnect);
                ContentFrame.Content = _activeSessionsView;
                return;
            }

            // Adjust content borders based on what we're showing
            bool isActiveSession = navItem.IsSession || navItem.SectionIndex == 5;

            if (isActiveSession)
            {
                SetFullScreenRdpMode();
            }
            else
            {
                SetNormalViewMode();
            }

            // Handle section selection
            switch (navItem.SectionIndex)
            {
                case 0: // Computers
                    if (_computersView == null)
                    {
                        _computersView = new ComputersView();
                        _computersView.DataContext = _viewModel;

                        // Wire up events
                        _computersView.AddRequested += (s, e) => AddButton_Click(null, null);
                        _computersView.EditRequested += (s, node) => { _viewModel.SelectedItem = node; EditButton_Click(null, null); };
                        _computersView.RemoveRequested += (s, node) => { _viewModel.SelectedItem = node; RemoveButton_Click(null, null); };
                        _computersView.DuplicateRequested += (s, entry) => DuplicateComputer(entry);
                        _computersView.RenameGroupRequested += (s, node) => RenameGroup(node);
                        _computersView.ConnectAllInGroupRequested += (s, node) => ConnectAllInGroup(node);
                        _computersView.ConnectRequested += (s, node) => HandleConnect(node);
                        _computersView.ImportRequested += (s, e) => ImportButton_Click(null, null);
                        _computersView.ExportRequested += (s, e) => ExportButton_Click(null, null);
                        _computersView.FavoriteToggled += (s, node) => HandleFavoriteToggle(node);
                        _computersView.ExploreInAppRequested += (s, node) => HandleExploreInApp(node);
                        _computersView.GamesActivated += (s, e) => ShowGamesTab(); // 🎮 Easter egg
                    }
                    ContentFrame.Content = _computersView;
                    break;

                case 1: // Servers
                    if (_serversView == null)
                    {
                        _serversView = new ServersView();
                        _serversView.DataContext = _viewModel;

                        // Wire up events
                        _serversView.ConnectRequested += (s, node) => HandleConnect(node);
                        _serversView.SearchTextChanged += (s, text) => { _viewModel.FilterText = text; };
                    }
                    ContentFrame.Content = _serversView;
                    break;

                case 2: // Kubernetes
                    if (_kubernetesView == null)
                    {
                        _kubernetesView = new KubernetesView();
                    }
                    ContentFrame.Content = _kubernetesView;
                    SetNormalViewMode();
                    break;

                case 3: // Recent
                    // Reload computer entries from database to get fresh LastConnectedAt times
                    var computerRepo = new RdpManager.Data.Repositories.ComputerRepository();
                    var computers = computerRepo.GetAll();
                    _viewModel.ComputerEntries = new System.Collections.ObjectModel.ObservableCollection<ComputerEntry>(computers);

                    // Always rebuild Recent from database when switching to this tab (to show latest)
                    _viewModel.BuildRecentConnectionsTree();

                    if (_recentView == null)
                    {
                        _recentView = new RecentView();
                        _recentView.ConnectRequested += (s, node) => HandleConnect(node);
                    }
                    _recentView.DataContext = _viewModel.RootNodesRecent;
                    ContentFrame.Content = _recentView;
                    break;

                case 4: // Favorites
                    if (_favoritesView == null)
                    {
                        _favoritesView = new FavoritesView();
                        _favoritesView.ConnectRequested += (s, node) => HandleConnect(node);
                    }
                    _favoritesView.DataContext = _viewModel.RootNodesFavorites;
                    ContentFrame.Content = _favoritesView;
                    break;

                case 5: // Active Sessions
                    EnsureActiveSessionsViewInitialized();
                    ContentFrame.Content = _activeSessionsView;
                    break;

                case 6: // File Explorer
                    EnsureFileExplorerViewInitialized();
                    ContentFrame.Content = _fileExplorerView;
                    SetNormalViewMode();
                    break;

                case 99: // 🎮 Games (Easter egg)
                    if (_gamesView == null)
                    {
                        _gamesView = new GamesView();
                        _gamesView.RequestClose += (s, e) =>
                        {
                            System.Diagnostics.Debug.WriteLine("🚪 Games tab requested close");
                            // Remove games nav item and go back to Computers
                            var gamesNavItem = _viewModel.NavigationItems.FirstOrDefault(n => n.IsTemporary && n.SectionIndex == 99);
                            if (gamesNavItem != null)
                            {
                                _viewModel.NavigationItems.Remove(gamesNavItem);
                            }
                            NavigationListBox.SelectedIndex = 0; // Back to Computers
                        };
                    }
                    ContentFrame.Content = _gamesView;
                    System.Diagnostics.Debug.WriteLine("🎮 Games view activated!");
                    break;
            }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EXCEPTION in NavigationListBox_SelectionChanged: {ex}");
                System.Windows.MessageBox.Show($"Navigation error: {ex.Message}\n\n{ex.StackTrace}", "Navigation Error");
            }
        }

        private void HandleConnect(Models.TreeNode node)
        {
            _viewModel.SelectedItem = node;
            ConnectButton_Click(null, null);
        }

        private void HandleFavoriteToggle(Models.TreeNode node)
        {
            if (node.ComputerEntry != null)
            {
                _viewModel.ToggleFavorite(node.ComputerEntry.MachineName, node.ComputerEntry.Domain,
                    node.ComputerEntry.FriendlyName, node.ComputerEntry.Group);
                bool isFav = _viewModel.IsFavorite(node.ComputerEntry.MachineName, node.ComputerEntry.Domain);

                // Update the TreeNode's IsFavorite - this will automatically update the UI via INotifyPropertyChanged
                node.IsFavorite = isFav;

                // _snackbarMessageQueue.Enqueue(isFav ? $"Added to favorites" : $"Removed from favorites");

                // Only rebuild Favorites tree (Computers tree doesn't need rebuild - icon updates automatically)
                _viewModel.BuildFavoritesTree();

                // Only refresh Favorites view if currently showing it
                if (_favoritesView != null && NavigationListBox.SelectedIndex == 3)
                {
                    _favoritesView.DataContext = _viewModel.RootNodesFavorites;
                }
            }
        }

        private void HandleExploreInApp(Models.TreeNode node)
        {
            if (node.ComputerEntry != null)
            {
                var computerEntry = node.ComputerEntry;
                string uncPath;

                // Build UNC path (try with domain first)
                if (!string.IsNullOrEmpty(computerEntry.Domain))
                {
                    uncPath = $"\\\\{computerEntry.MachineName}.{computerEntry.Domain}";
                }
                else
                {
                    uncPath = $"\\\\{computerEntry.MachineName}";
                }

                // Create new File Explorer session
                var session = new FileExplorerSession
                {
                    FolderPath = uncPath,
                    DisplayName = computerEntry.FriendlyName ?? computerEntry.MachineName,
                    CreatedAt = DateTime.Now,
                    LastAccessedAt = DateTime.Now
                };

                System.Diagnostics.Debug.WriteLine($"[ExploreInApp] Creating File Explorer session for: {uncPath}");

                // Add to ViewModel (will trigger database save and navigation item creation)
                _viewModel.FileExplorerSessions.Add(session);

                // Initialize and show File Explorer view
                EnsureFileExplorerViewInitialized();
                _fileExplorerView?.ShowSession(session);

                // Switch to File Explorer section
                var fileExplorerNavItem = _viewModel.NavigationItems.FirstOrDefault(n => n.SectionIndex == 6);
                if (fileExplorerNavItem != null)
                {
                    NavigationListBox.SelectedItem = fileExplorerNavItem;
                }

                // _snackbarMessageQueue.Enqueue($"Opened {computerEntry.FriendlyName ?? computerEntry.MachineName} in File Explorer");
            }
        }

        private System.Windows.Controls.TreeView CreateTreeView(System.Collections.ObjectModel.ObservableCollection<Models.TreeNode> source, bool isComputers)
        {
            var treeView = new System.Windows.Controls.TreeView
            {
                ItemsSource = source
            };

            // Apply the same template as the main TreeView
            treeView.Resources = ComputersTreeView.Resources;
            treeView.ItemContainerStyle = ComputersTreeView.ItemContainerStyle;

            // Update SelectedItem when selection changes
            treeView.SelectedItemChanged += (s, e) =>
            {
                _viewModel.SelectedItem = e.NewValue;
            };

            if (isComputers)
            {
                treeView.MouseDoubleClick += (s, e) =>
                {
                    if (GetSelectedNode(treeView) is Models.TreeNode node && node.ComputerEntry != null)
                    {
                        if (_viewModel.UseEmbeddedRdp)
                        {
                            ConnectEmbedded(node.ComputerEntry.MachineName, node.ComputerEntry.FriendlyName, node.ComputerEntry.Domain, node.ComputerEntry.Id);
                        }
                        else
                        {
                            MainController.ConnectToMachine(node.ComputerEntry.MachineName, node.ComputerEntry.FriendlyName);

                            // Log external connection start
                            if (node.ComputerEntry.Id > 0)
                            {
                                try
                                {
                                    var connectionLogRepo = new RdpManager.Data.Repositories.ConnectionLogRepository();
                                    var serverAddr = node.ComputerEntry.MachineName;
                                    if (!string.IsNullOrEmpty(node.ComputerEntry.Domain))
                                        serverAddr = $"{node.ComputerEntry.MachineName}.{node.ComputerEntry.Domain}";

                                    connectionLogRepo.StartConnection(
                                        node.ComputerEntry.Id,
                                        "External",
                                        serverAddr,
                                        node.ComputerEntry.FriendlyName,
                                        node.ComputerEntry.Domain,
                                        node.ComputerEntry.Group);
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Error logging external connection: {ex.Message}");
                                }
                            }
                        }
                        _viewModel.AddConnectionToHistory(node.ComputerEntry.FriendlyName, node.ComputerEntry.MachineName, node.ComputerEntry.Domain, node.ComputerEntry.Group);

                        // Refresh Recent view if currently showing
                        if (NavigationListBox.SelectedIndex == 2)
                        {
                            ContentFrame.Content = CreateTreeView(_viewModel.RootNodesRecent, true);
                        }
                    }
                };
            }
            else
            {
                treeView.MouseDoubleClick += (s, e) =>
                {
                    if (GetSelectedNode(treeView) is Models.TreeNode node && node.Server != null)
                    {
                        string address = !string.IsNullOrEmpty(node.Server.Domain) ? $"{node.Server.ServerName}.{node.Server.Domain}" : node.Server.ServerName;
                        if (_viewModel.UseEmbeddedRdp)
                        {
                            ConnectEmbedded(address, node.Server.ServerName, node.Server.Domain);
                        }
                        else
                        {
                            ConnectToServer(node.Server);
                        }
                        _viewModel.AddConnectionToHistory(node.Server.ServerName, node.Server.ServerName, node.Server.Domain, node.Server.Application);

                        // Refresh Recent view if currently showing
                        if (NavigationListBox.SelectedIndex == 2)
                        {
                            ContentFrame.Content = CreateTreeView(_viewModel.RootNodesRecent, true);
                        }
                    }
                };
            }

            return treeView;
        }

        private Models.TreeNode? GetSelectedNode(System.Windows.Controls.TreeView treeView)
        {
            return treeView.SelectedItem as Models.TreeNode;
        }

        private async void ImportButton_Click(object? sender, RoutedEventArgs? e)
        {
            var openFileDialog = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                Title = "Import Computer Entries"
            };

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                try
                {
                    var json = System.IO.File.ReadAllText(openFileDialog.FileName);
                    var imported = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Collections.Generic.List<ComputerEntry>>(json);

                    if (imported != null && imported.Count > 0)
                    {
                        foreach (var entry in imported)
                        {
                            if (!_viewModel.ComputerEntries.Any(ce => ce.MachineName.Equals(entry.MachineName, StringComparison.OrdinalIgnoreCase)))
                            {
                                _viewModel.ComputerEntries.Add(entry);
                            }
                        }

                        _viewModel.BuildComputerEntriesTree();
                        SaveEntries();
                        // _snackbarMessageQueue.Enqueue($"Imported {imported.Count} entries successfully");
                    }
                }
                catch (Exception ex)
                {
                    // _snackbarMessageQueue.Enqueue($"Import failed: {ex.Message}");
                }
            }
        }

        private async void ExportButton_Click(object? sender, RoutedEventArgs? e)
        {
            var saveFileDialog = new System.Windows.Forms.SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|CSV files (*.csv)|*.csv",
                Title = "Export Computer Entries",
                FileName = $"rdp_entries_{DateTime.Now:yyyyMMdd}.json"
            };

            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                try
                {
                    if (saveFileDialog.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    {
                        // Export as CSV
                        var csv = "MachineName,FriendlyName,Group\n" +
                            string.Join("\n", _viewModel.ComputerEntries.Select(en =>
                                $"\"{en.MachineName}\",\"{en.FriendlyName}\",\"{en.Group}\""));
                        System.IO.File.WriteAllText(saveFileDialog.FileName, csv);
                    }
                    else
                    {
                        // Export as JSON
                        var json = Newtonsoft.Json.JsonConvert.SerializeObject(_viewModel.ComputerEntries, Newtonsoft.Json.Formatting.Indented);
                        System.IO.File.WriteAllText(saveFileDialog.FileName, json);
                    }

                    // _snackbarMessageQueue.Enqueue($"Exported {_viewModel.ComputerEntries.Count} entries successfully");
                }
                catch (Exception ex)
                {
                    // _snackbarMessageQueue.Enqueue($"Export failed: {ex.Message}");
                }
            }
        }

        private void SaveEntries()
        {
            try
            {
                // Save to database instead of JSON
                var computerRepo = new RdpManager.Data.Repositories.ComputerRepository();
                foreach (var entry in _viewModel.ComputerEntries)
                {
                    if (entry.Id > 0)
                    {
                        // Update existing entry
                        computerRepo.Update(entry);
                    }
                    else
                    {
                        // Insert new entry (shouldn't happen often as AddEntry handles this)
                        entry.Id = computerRepo.Insert(entry);
                    }
                }
            }
            catch (Exception ex)
            {
                // _snackbarMessageQueue.Enqueue($"Error saving: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error saving computers to database: {ex.Message}");
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Only hide the window if the notify icon is active
            if (_notifyIcon != null && _notifyIcon.Visible)
            {
                e.Cancel = true;
                this.Hide();

                // Show a balloon tip on first minimize
                if (!_hasShownMinimizeTip)
                {
                    _notifyIcon.BalloonTipTitle = "RDP Manager";
                    _notifyIcon.BalloonTipText = "Application minimized to system tray. Double-click to restore.";
                    _notifyIcon.ShowBalloonTip(2000);
                    _hasShownMinimizeTip = true;
                }
            }
        }

        // Removed: Window no longer hides to tray on minimize (only on close)
        // private void Window_StateChanged(object sender, EventArgs e)
        // {
        //     if (this.WindowState == WindowState.Minimized && _notifyIcon != null)
        //     {
        //         this.Hide();
        //     }
        // }

        private void NavigationListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Find the double-clicked navigation item
            var item = e.OriginalSource as FrameworkElement;
            while (item != null && !(item is ListBoxItem))
            {
                item = System.Windows.Media.VisualTreeHelper.GetParent(item) as FrameworkElement;
            }

            if (item is ListBoxItem listBoxItem && listBoxItem.DataContext is NavigationItem navItem)
            {
                // If double-clicking on a disconnected session, reconnect it
                if (navItem.IsSession && navItem.Session != null &&
                    navItem.Session.ConnectionState == RdpConnectionState.Disconnected)
                {
                    System.Diagnostics.Debug.WriteLine($"Double-click reconnect: {navItem.Session.DisplayName}");

                    // Show the session (which will reconnect it)
                    if (_activeSessionsView != null)
                    {
                        _activeSessionsView.ShowSession(navItem.Session);
                    }
                }
            }
        }

        private void CloseSessionNav_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is NavigationItem navItem && navItem.Session != null)
            {
                bool shouldClose = true;

                // Only confirm if session is actively connected
                if (navItem.Session.ConnectionState == RdpConnectionState.Connected)
                {
                    var result = System.Windows.MessageBox.Show(
                        $"Are you sure you want to disconnect from {navItem.Name} without signing off?\n\nThis will immediately end your remote session.",
                        "Disconnect Session",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    shouldClose = result == MessageBoxResult.Yes;
                }
                // Disconnected sessions close immediately without confirmation

                if (shouldClose)
                {
                    // Mark session as closed in database (so it won't restore on next startup)
                    if (navItem.Session.ConnectionLogId > 0)
                    {
                        try
                        {
                            var connectionLogRepo = new RdpManager.Data.Repositories.ConnectionLogRepository();
                            connectionLogRepo.CloseSession(navItem.Session.ConnectionLogId);
                            System.Diagnostics.Debug.WriteLine($"Marked session #{navItem.Session.ConnectionLogId} as closed in database");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error marking session as closed: {ex.Message}");
                        }
                    }

                    // Remove from ActiveSessionsView if it exists
                    _activeSessionsView?.RemoveSession(navItem.Session);

                    // Remove from ViewModel collection (will trigger navigation update)
                    _viewModel.ActiveSessions.Remove(navItem.Session);
                }

                // Prevent selection change
                e.Handled = true;
            }
        }

        // Helper methods for content border management
        private void SetFullScreenRdpMode()
        {
            ContentGrid.Background = System.Windows.Media.Brushes.Black;  // Set parent grid to black
            ContentOuterBorder.Margin = new Thickness(0);
            ContentOuterBorder.Background = System.Windows.Media.Brushes.Black;  // Set black to hide any gaps
            ContentInnerBorder.BorderThickness = new Thickness(0);
            ContentInnerBorder.CornerRadius = new CornerRadius(0);
            ContentInnerBorder.Background = System.Windows.Media.Brushes.Black;  // Changed from Transparent to Black
        }

        private void SetNormalViewMode()
        {
            ContentGrid.Background = System.Windows.Media.Brushes.Transparent;  // Restore transparent
            ContentOuterBorder.Margin = new Thickness(5);
            ContentOuterBorder.Background = System.Windows.Media.Brushes.Transparent;  // Restore transparent
            ContentInnerBorder.BorderThickness = new Thickness(1);
            ContentInnerBorder.CornerRadius = new CornerRadius(8);
            ContentInnerBorder.SetResourceReference(Border.BackgroundProperty, "App.Background.Surface");
        }

        // Pop-out / Pop-in handlers
        private void ContextMenu_PopOut(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is NavigationItem navItem && navItem.Session != null)
            {
                var session = navItem.Session;

                // Don't pop-out if already popped out
                if (session.IsPopedOut)
                {
                    // _snackbarMessageQueue.Enqueue("Session is already popped out");
                    return;
                }

                try
                {
                    System.Diagnostics.Debug.WriteLine($"Popping out session: {session.DisplayName}");

                    // Get the RDP control from ActiveSessionsView
                    var rdpControl = _activeSessionsView?.GetSessionControl(session.SessionId);
                    if (rdpControl == null)
                    {
                        // _snackbarMessageQueue.Enqueue("Session control not found");
                        return;
                    }

                    // Create pop-out window with close handler
                    var popoutWindow = new RdpManager.Windows.PopoutWindow(session, PopInSession, CloseSession);

                    // Remove control from ActiveSessionsView
                    if (_activeSessionsView != null && _activeSessionsView.RdpContentHost != null)
                    {
                        _activeSessionsView.RdpContentHost.Content = null;
                    }

                    // Add control to pop-out window
                    popoutWindow.RdpContentHost.Content = rdpControl;

                    // Mark session as popped out
                    session.IsPopedOut = true;
                    session.PopoutWindow = popoutWindow;

                    // Show the window
                    popoutWindow.Show();

                    // Show popped-out message in main window if this session is selected
                    if (_activeSessionsView != null && navItem == NavigationListBox.SelectedItem)
                    {
                        _activeSessionsView.ShowPoppedOutMessage(session);
                    }

                    // _snackbarMessageQueue.Enqueue($"Popped out: {session.DisplayName}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error popping out session: {ex}");
                    // _snackbarMessageQueue.Enqueue($"Error: {ex.Message}");
                }
            }
        }

        private void PopInSession(RdpSession session)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"Popping in session: {session.DisplayName}");

                if (session.PopoutWindow == null)
                {
                    System.Diagnostics.Debug.WriteLine("PopoutWindow is null, cannot pop-in");
                    return;
                }

                // Get the RDP control from pop-out window
                var popoutWindow = session.PopoutWindow as RdpManager.Windows.PopoutWindow;
                var rdpControl = popoutWindow?.RdpContentHost?.Content as RdpManager.Controls.RdpSessionControl;

                if (rdpControl == null)
                {
                    System.Diagnostics.Debug.WriteLine("RDP control not found in pop-out window");
                    return;
                }

                // Remove from pop-out window
                popoutWindow.RdpContentHost.Content = null;

                // Add back to main window's ActiveSessionsView
                if (_activeSessionsView != null)
                {
                    _activeSessionsView.RdpContentHost.Content = rdpControl;
                    _activeSessionsView.NoSelectionPanel.Visibility = Visibility.Collapsed;
                    _activeSessionsView.PoppedOutPanel.Visibility = Visibility.Collapsed;
                }

                // Mark session as not popped out
                session.IsPopedOut = false;
                session.PopoutWindow = null;

                // If this session is currently selected in navigation, refresh the view
                var selectedNavItem = NavigationListBox.SelectedItem as NavigationItem;
                if (selectedNavItem?.Session?.SessionId == session.SessionId && _activeSessionsView != null)
                {
                    ContentFrame.Content = _activeSessionsView;
                    SetFullScreenRdpMode();
                }

                System.Diagnostics.Debug.WriteLine($"Successfully popped in: {session.DisplayName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error popping in session: {ex}");
                System.Windows.MessageBox.Show($"Error popping in session: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
