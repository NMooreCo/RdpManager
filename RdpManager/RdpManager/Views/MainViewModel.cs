using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RdpManager.Models;
using Application = System.Windows.Application;

namespace RdpManager.Views
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<Models.TreeNode> _rootNodes;
        private ObservableCollection<Models.TreeNode> _rootNodesServers;
        private ObservableCollection<Models.TreeNode> _rootNodesRecent;
        private ObservableCollection<Models.TreeNode> _rootNodesFavorites;
        private ObservableCollection<ComputerEntry> _computerEntries;
        private ObservableCollection<Server> _serverEntries;
        private ConnectionHistory _connectionHistory;
        private ObservableCollection<RdpSession> _activeSessions;
        private ObservableCollection<FileExplorerSession> _fileExplorerSessions;
        private ObservableCollection<NavigationItem> _navigationItems;
        private object _selectedItem;
        private object _selectedServerItem;
        private readonly HttpClient _httpClient;
        private string _filterText;
        private bool _useEmbeddedRdp = true; // Default to embedded RDP
        private string? _serverLoadError;
        private const string ConnectionHistoryFile = "connectionHistory.json";
        // Optional API endpoint for fetching a server list. Empty by default; set it in
        // Settings (persisted to the Preferences table). When empty, the Servers tab stays
        // empty and no network call is made. The endpoint is expected to return JSON matching
        // ServerResponse (a "Servers" array). See README for the schema.
        private const string DefaultServerEndpoint = "";

        public MainViewModel()
        {
            _httpClient = new HttpClient();
            _activeSessions = new ObservableCollection<RdpSession>();
            _fileExplorerSessions = new ObservableCollection<FileExplorerSession>();
            _navigationItems = new ObservableCollection<NavigationItem>();

            InitializeNavigation();
            LoadPreferences();
            LoadConnectionHistory();
            LoadEntries();
            LoadFileExplorerSessions();
            BuildComputerEntriesTree();
            BuildRecentConnectionsTree();
            BuildFavoritesTree();
            _ = SetupServersAsync(); // Start the async method without awaiting

            // Subscribe to session changes to update navigation
            _activeSessions.CollectionChanged += ActiveSessions_CollectionChanged;
            _fileExplorerSessions.CollectionChanged += FileExplorerSessions_CollectionChanged;
        }

        public ObservableCollection<ComputerEntry> ComputerEntries
        {
            get => _computerEntries;
            set { _computerEntries = value; OnPropertyChanged(nameof(ComputerEntries)); }
        }

        public ObservableCollection<Server> ServerEntries
        {
            get => _serverEntries;
            set { _serverEntries = value; OnPropertyChanged(nameof(ServerEntries)); }
        }

        public ObservableCollection<Models.TreeNode> RootNodes
        {
            get => _rootNodes;
            set { _rootNodes = value; OnPropertyChanged(nameof(RootNodes)); }
        }

        public ObservableCollection<Models.TreeNode> RootNodesServers
        {
            get => _rootNodesServers;
            set { _rootNodesServers = value; OnPropertyChanged(nameof(RootNodesServers)); }
        }
        
        public ObservableCollection<Models.TreeNode> RootNodesRecent
        {
            get => _rootNodesRecent;
            set { _rootNodesRecent = value; OnPropertyChanged(nameof(RootNodesRecent)); }
        }
        
        public ObservableCollection<Models.TreeNode> RootNodesFavorites
        {
            get => _rootNodesFavorites;
            set { _rootNodesFavorites = value; OnPropertyChanged(nameof(RootNodesFavorites)); }
        }

        public ObservableCollection<RdpSession> ActiveSessions
        {
            get => _activeSessions;
            set { _activeSessions = value; OnPropertyChanged(nameof(ActiveSessions)); }
        }

        public ObservableCollection<FileExplorerSession> FileExplorerSessions
        {
            get => _fileExplorerSessions;
            set { _fileExplorerSessions = value; OnPropertyChanged(nameof(FileExplorerSessions)); }
        }

        public ObservableCollection<NavigationItem> NavigationItems
        {
            get => _navigationItems;
            set { _navigationItems = value; OnPropertyChanged(nameof(NavigationItems)); }
        }

        public ConnectionHistory ConnectionHistory
        {
            get => _connectionHistory;
            set { _connectionHistory = value; OnPropertyChanged(nameof(ConnectionHistory)); }
        }

        public object SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value; OnPropertyChanged(nameof(SelectedItem));
            }
        }

        public object SelectedServerItem
        {
            get => _selectedServerItem;
            set
            {
                _selectedServerItem = value; OnPropertyChanged(nameof(SelectedServerItem));
            }
        }

        public string FilterText
        {
            get => _filterText;
            set
            {
                _filterText = value;
                OnPropertyChanged(nameof(FilterText));
                ApplyFilter();
            }
        }

        public bool UseEmbeddedRdp
        {
            get => _useEmbeddedRdp;
            set
            {
                _useEmbeddedRdp = value;
                OnPropertyChanged(nameof(UseEmbeddedRdp));
                SavePreferences();
            }
        }

        public string ServerEndpoint
        {
            get
            {
                try
                {
                    var prefsRepo = new RdpManager.Data.Repositories.PreferencesRepository();
                    return prefsRepo.GetString("ServerEndpoint", DefaultServerEndpoint) ?? DefaultServerEndpoint;
                }
                catch
                {
                    return DefaultServerEndpoint;
                }
            }
            set
            {
                try
                {
                    var prefsRepo = new RdpManager.Data.Repositories.PreferencesRepository();
                    prefsRepo.Set("ServerEndpoint", value ?? string.Empty);
                    OnPropertyChanged(nameof(ServerEndpoint));
                }
                catch
                {
                    // Silently fail if we can't save
                }
            }
        }

        public string? ServerLoadError
        {
            get => _serverLoadError;
            private set
            {
                _serverLoadError = value;
                OnPropertyChanged(nameof(ServerLoadError));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private void LoadEntries()
        {
            try
            {
                var computerRepo = new RdpManager.Data.Repositories.ComputerRepository();
                var computers = computerRepo.GetAll();
                ComputerEntries = new ObservableCollection<ComputerEntry>(computers);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading computers from database: {ex.Message}");
                ComputerEntries = new ObservableCollection<ComputerEntry>();
            }
        }

        public bool ServersSetup = false;
        private async Task SetupServersAsync()
        {
            await LoadServersAsync();

            // Ensure UI updates happen on the UI thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    BuildServerEntriesTree();
                    ApplyFilter();
                }
                catch
                {

                }
                finally
                {
                    ServersSetup = true;
                }
            });
        }

        public async Task LoadServersAsync()
        {
            var serverEndpoint = ServerEndpoint;

            // No endpoint configured: leave the Servers tab empty and skip the network call.
            if (string.IsNullOrWhiteSpace(serverEndpoint))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ServerLoadError = null;
                    ServerEntries = new ObservableCollection<Server>();
                });
                return;
            }

            try
            {
                Application.Current.Dispatcher.Invoke(() => ServerLoadError = null);

                var response = await _httpClient.GetStringAsync(serverEndpoint);
                var servers = JsonConvert.DeserializeObject<ServerResponse>(response)?.Servers
                              ?? new List<Server>();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    ServerEntries = new ObservableCollection<Server>(servers);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading servers: {ex.Message}");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ServerLoadError = $"Failed to load servers from {serverEndpoint}: {ex.Message}";
                });
            }
        }

        public async Task ReloadServersAsync()
        {
            await SetupServersAsync();
        }

        public void BuildComputerEntriesTree()
        {
            RootNodes = new ObservableCollection<Models.TreeNode>();
            if (ComputerEntries == null)
            {
                return;
            }

            var groupStateManager = RdpManager.Helpers.GroupStateManager.Instance;
            var groupRepo = new RdpManager.Data.Repositories.GroupRepository();

            // Get all groups to respect their sort order
            var allGroups = groupRepo.GetAll();

            // Sort computers by group path, then sort order, then friendly name
            foreach (var entry in ComputerEntries.OrderBy(d => d.Group).ThenBy(d => d.SortOrder).ThenBy(d => d.FriendlyName).ToList())
            {
                var groupPath = string.IsNullOrEmpty(entry.Group) ? Array.Empty<string>() : entry.Group.Split('/');
                ObservableCollection<Models.TreeNode> currentGroup = RootNodes;
                var currentPath = new List<string>();

                // Build group hierarchy
                foreach (var groupName in groupPath)
                {
                    currentPath.Add(groupName);
                    var fullPath = string.Join("/", currentPath);

                    var existingNode = currentGroup.FirstOrDefault(n => n.Name == groupName && n.ComputerEntry == null);
                    if (existingNode == null)
                    {
                        // Load expansion state from database (default to expanded)
                        bool isExpanded = groupStateManager.GetExpansionState(fullPath, defaultExpanded: true);

                        // Get or create group in database
                        var groupData = allGroups.FirstOrDefault(g => g.FullPath == fullPath);
                        if (groupData == null)
                        {
                            // Group doesn't exist in database - create it
                            var parentPath = currentPath.Count > 1 ? string.Join("/", currentPath.Take(currentPath.Count - 1)) : null;
                            var newGroup = new RdpManager.Data.Models.Group
                            {
                                FullPath = fullPath,
                                ParentPath = parentPath,
                                Name = groupName,
                                SortOrder = 0, // Will be updated if needed
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            };
                            newGroup.Id = groupRepo.Insert(newGroup);
                            allGroups.Add(newGroup);
                            groupData = newGroup;
                            System.Diagnostics.Debug.WriteLine($"Created missing group in database: {fullPath}");
                        }

                        var sortOrder = groupData.SortOrder;

                        existingNode = new Models.TreeNode
                        {
                            Name = groupName,
                            GroupPath = fullPath,
                            IsExpanded = isExpanded
                        };

                        // Insert in correct position based on sort order from database
                        // Find where to insert this group among other groups (not computers)
                        var groupNodes = currentGroup.Where(n => n.ComputerEntry == null).ToList();

                        // Find insert position by comparing sort orders
                        var insertIndex = 0;
                        for (int i = 0; i < groupNodes.Count; i++)
                        {
                            var existingGroupPath = groupNodes[i].GroupPath;
                            var existingGroupData = allGroups.FirstOrDefault(g => g.FullPath == existingGroupPath);
                            var existingSortOrder = existingGroupData?.SortOrder ?? 999;

                            if (sortOrder >= existingSortOrder)
                            {
                                insertIndex = currentGroup.IndexOf(groupNodes[i]) + 1;
                            }
                            else
                            {
                                break;
                            }
                        }

                        currentGroup.Insert(insertIndex, existingNode);
                    }
                    currentGroup = existingNode.Children;
                }

                var entryNode = new Models.TreeNode
                {
                    Name = entry.FriendlyName,
                    ComputerEntry = entry,
                    IsFavorite = IsFavorite(entry.MachineName, entry.Domain)
                };
                currentGroup.Add(entryNode);
            }
        }

        public void BuildServerEntriesTree()
        {
            RootNodesServers = new ObservableCollection<Models.TreeNode>();
            if (!ServerEntries.Any())
            {
                return;
            }

            foreach (var server in ServerEntries.Where(s => s.Os.Equals("windows", StringComparison.OrdinalIgnoreCase)).OrderBy(s => s.ServerName).ToList())
            {
                AddServerToTree(server);
            }
        }

        private void AddServerToTree(Server server)
        {
            var groupPath = new[] { server.Domain, server.Application, server.Site };
            ObservableCollection<Models.TreeNode> currentGroup = RootNodesServers;

            foreach (var groupName in groupPath)
            {
                var groupValue = groupName ?? "Unknown";
                var existingNode = currentGroup.FirstOrDefault(n => n.Name == groupValue && n.Server == null);
                if (existingNode == null)
                {
                    existingNode = new Models.TreeNode { Name = groupValue, IsExpanded = false };
                    currentGroup.Add(existingNode);
                }
                currentGroup = existingNode.Children;
            }

            var serverNode = new Models.TreeNode 
            { 
                Name = server.ServerName, 
                Server = server,
                IsFavorite = IsFavorite(server.ServerName, server.Domain)
            };
            currentGroup.Add(serverNode);
        }

        private void ApplyFilter()
        {
            if (RootNodesServers == null) return;

            foreach (var node in RootNodesServers)
            {
                ApplyFilterToNode(node);
            }
        }

        private bool ApplyFilterToNode(Models.TreeNode node)
        {
            bool isVisible = false;

            if (node.Children.Any())
            {
                bool anyChildVisible = false;
                foreach (var child in node.Children)
                {
                    if (ApplyFilterToNode(child))
                    {
                        anyChildVisible = true;
                    }
                }
                node.IsVisible = anyChildVisible;
                node.IsExpanded = anyChildVisible;
                isVisible = anyChildVisible;
            }
            else
            {
                if (node.Server != null)
                {
                    var filter = FilterText?.ToLowerInvariant();
                    bool matches = string.IsNullOrWhiteSpace(filter) || node.Server.GetType().GetProperties()
                        .Any(p => p.GetValue(node.Server)?.ToString()?.ToLowerInvariant().Contains(filter) == true);
                    node.IsVisible = matches;
                    isVisible = matches;
                }
                else
                {
                    node.IsVisible = true;
                    isVisible = true;
                }
            }

            return isVisible;
        }

        public void AddEntry(ComputerEntry newEntry)
        {
            try
            {
                var computerRepo = new RdpManager.Data.Repositories.ComputerRepository();
                newEntry.Id = computerRepo.Insert(newEntry);
                ComputerEntries?.Add(newEntry);

                // Add to existing tree instead of rebuilding
                AddEntryToTree(newEntry);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding computer to database: {ex.Message}");
            }
        }

        public void RemoveEntry(ComputerEntry entry)
        {
            if (ComputerEntries != null && entry != null)
            {
                // Find the entry by database ID (unique identifier)
                var entryToRemove = ComputerEntries.FirstOrDefault(c => c.Id == entry.Id);

                if (entryToRemove != null)
                {
                    try
                    {
                        var computerRepo = new RdpManager.Data.Repositories.ComputerRepository();
                        computerRepo.Delete(entryToRemove.Id);
                        ComputerEntries.Remove(entryToRemove);

                        // Remove from existing tree instead of rebuilding
                        RemoveEntryFromTree(entryToRemove);

                        System.Diagnostics.Debug.WriteLine($"Removed computer: {entryToRemove.FriendlyName} (Id: {entryToRemove.Id})");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error removing computer from database: {ex.Message}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"ERROR: Could not find entry with Id={entry.Id} in ComputerEntries collection");
                }
            }
        }

        public void AddEntryToTree(ComputerEntry entry)
        {
            if (RootNodes == null) return;

            var groupPath = string.IsNullOrEmpty(entry.Group) ? Array.Empty<string>() : entry.Group.Split('/');
            ObservableCollection<Models.TreeNode> currentGroup = RootNodes;
            var currentPath = new List<string>();
            var groupStateManager = RdpManager.Helpers.GroupStateManager.Instance;

            // Navigate/create group structure
            foreach (var groupName in groupPath)
            {
                currentPath.Add(groupName);
                var fullPath = string.Join("/", currentPath);

                var existingNode = currentGroup.FirstOrDefault(n => n.Name == groupName && n.ComputerEntry == null);
                if (existingNode == null)
                {
                    // Load expansion state from database (default to expanded)
                    bool isExpanded = groupStateManager.GetExpansionState(fullPath, defaultExpanded: true);

                    existingNode = new Models.TreeNode
                    {
                        Name = groupName,
                        GroupPath = fullPath,
                        IsExpanded = isExpanded
                    };

                    // Insert in sorted order
                    int insertIndex = 0;
                    while (insertIndex < currentGroup.Count &&
                           currentGroup[insertIndex].ComputerEntry == null &&
                           string.Compare(currentGroup[insertIndex].Name, groupName, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        insertIndex++;
                    }
                    currentGroup.Insert(insertIndex, existingNode);
                }
                currentGroup = existingNode.Children;
            }

            // Add the computer entry node in sorted order
            var newNode = new Models.TreeNode
            {
                Name = entry.FriendlyName,
                ComputerEntry = entry,
                IsFavorite = entry.IsFavorite
            };

            int computerIndex = 0;
            while (computerIndex < currentGroup.Count &&
                   string.Compare(currentGroup[computerIndex].Name, entry.FriendlyName, StringComparison.OrdinalIgnoreCase) < 0)
            {
                computerIndex++;
            }
            currentGroup.Insert(computerIndex, newNode);
        }

        public void RemoveEntryFromTree(ComputerEntry entry)
        {
            if (RootNodes == null) return;

            var groupPath = string.IsNullOrEmpty(entry.Group) ? Array.Empty<string>() : entry.Group.Split('/');
            ObservableCollection<Models.TreeNode> currentGroup = RootNodes;
            var groupNodes = new List<(ObservableCollection<Models.TreeNode>, Models.TreeNode)>();

            // Navigate to the group containing the entry
            foreach (var groupName in groupPath)
            {
                var groupNode = currentGroup.FirstOrDefault(n => n.Name == groupName && n.ComputerEntry == null);
                if (groupNode == null) return; // Group not found

                groupNodes.Add((currentGroup, groupNode));
                currentGroup = groupNode.Children;
            }

            // Find and remove the entry node by unique database ID
            var entryNode = currentGroup.FirstOrDefault(n =>
                n.ComputerEntry != null &&
                n.ComputerEntry.Id == entry.Id);

            if (entryNode != null)
            {
                System.Diagnostics.Debug.WriteLine($"Removing from tree: {entryNode.ComputerEntry.FriendlyName} (Id: {entry.Id})");
                currentGroup.Remove(entryNode);

                // Clean up empty parent groups
                for (int i = groupNodes.Count - 1; i >= 0; i--)
                {
                    var (parentCollection, groupNode) = groupNodes[i];
                    if (groupNode.Children.Count == 0)
                    {
                        parentCollection.Remove(groupNode);
                    }
                    else
                    {
                        break; // Stop if we find a non-empty group
                    }
                }
            }
        }

        public void RebuildGroupTree()
        {
            BuildComputerEntriesTree();
        }
        
        private void LoadConnectionHistory()
        {
            if (File.Exists(ConnectionHistoryFile))
            {
                try
                {
                    var json = File.ReadAllText(ConnectionHistoryFile);
                    _connectionHistory = JsonConvert.DeserializeObject<ConnectionHistory>(json) ?? new ConnectionHistory();
                }
                catch
                {
                    _connectionHistory = new ConnectionHistory();
                }
            }
            else
            {
                _connectionHistory = new ConnectionHistory();
            }
        }
        
        public void SaveConnectionHistory()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_connectionHistory, Formatting.Indented);
                File.WriteAllText(ConnectionHistoryFile, json);
            }
            catch
            {
                // Silently fail if we can't save history
            }
        }

        private void LoadPreferences()
        {
            try
            {
                var prefsRepo = new RdpManager.Data.Repositories.PreferencesRepository();
                _useEmbeddedRdp = prefsRepo.GetBool("UseEmbeddedRdp", true);
            }
            catch
            {
                // Use default value (true)
            }
        }

        private void SavePreferences()
        {
            try
            {
                var prefsRepo = new RdpManager.Data.Repositories.PreferencesRepository();
                prefsRepo.SetBool("UseEmbeddedRdp", _useEmbeddedRdp);
            }
            catch
            {
                // Silently fail if we can't save
            }
        }
        
        public void AddConnectionToHistory(string name, string machineName, string domain = null, string group = null)
        {
            _connectionHistory.AddConnection(name, machineName, domain, group);
            SaveConnectionHistory();
            BuildRecentConnectionsTree();
            BuildFavoritesTree();
        }
        
        public void ToggleFavorite(string machineName, string domain = null, string friendlyName = null, string group = null)
        {
            // Ensure the entry exists in history before toggling
            if (!_connectionHistory.Entries.Any(e => 
                e.MachineName.Equals(machineName, StringComparison.OrdinalIgnoreCase) &&
                (e.Domain ?? "").Equals(domain ?? "", StringComparison.OrdinalIgnoreCase)))
            {
                // Add entry if it doesn't exist - look up full details from ComputerEntries if not provided
                if (string.IsNullOrEmpty(friendlyName) || string.IsNullOrEmpty(group))
                {
                    var computerEntry = ComputerEntries?.FirstOrDefault(c => 
                        c.MachineName.Equals(machineName, StringComparison.OrdinalIgnoreCase) &&
                        (c.Domain ?? "").Equals(domain ?? "", StringComparison.OrdinalIgnoreCase));
                    
                    if (computerEntry != null)
                    {
                        friendlyName = computerEntry.FriendlyName;
                        group = computerEntry.Group;
                    }
                    else
                    {
                        // For servers, look in ServerEntries
                        var serverEntry = ServerEntries?.FirstOrDefault(s =>
                            s.ServerName.Equals(machineName, StringComparison.OrdinalIgnoreCase) &&
                            (s.Domain ?? "").Equals(domain ?? "", StringComparison.OrdinalIgnoreCase));
                        
                        if (serverEntry != null)
                        {
                            friendlyName = serverEntry.ServerName;
                            group = serverEntry.Application;
                        }
                    }
                }
                
                // Use provided or found friendly name, fallback to machine name
                _connectionHistory.AddConnection(friendlyName ?? machineName, machineName, domain, group);
            }
            
            _connectionHistory.ToggleFavorite(machineName, domain);
            SaveConnectionHistory();

            // Also update the computer entry's IsFavorite flag in the database
            try
            {
                var computerEntry = ComputerEntries?.FirstOrDefault(c =>
                    c.MachineName.Equals(machineName, StringComparison.OrdinalIgnoreCase) &&
                    (c.Domain ?? "").Equals(domain ?? "", StringComparison.OrdinalIgnoreCase));

                if (computerEntry != null && computerEntry.Id > 0)
                {
                    var computerRepo = new RdpManager.Data.Repositories.ComputerRepository();
                    computerRepo.ToggleFavorite(computerEntry.Id);

                    // Update the in-memory copy
                    computerEntry.IsFavorite = !computerEntry.IsFavorite;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error toggling favorite in database: {ex.Message}");
            }

            BuildRecentConnectionsTree();
            BuildFavoritesTree();
        }
        
        public void BuildRecentConnectionsTree()
        {
            var nodes = new ObservableCollection<Models.TreeNode>();

            try
            {
                // Get recent connections from database (unique by ComputerId, most recent first)
                var connectionLogRepo = new RdpManager.Data.Repositories.ConnectionLogRepository();
                var recentLogs = connectionLogRepo.GetRecent(50);

                // Group by ComputerId and find the most recent COMPLETED connection for each
                var computerGroups = recentLogs
                    .Where(log => log.ComputerId > 0)
                    .GroupBy(log => log.ComputerId)
                    .Select(g => new
                    {
                        ComputerId = g.Key,
                        MostRecentLog = g.OrderByDescending(log => log.StartTime).First(),
                        // Get most recent completed connection (with EndTime) for the timestamp
                        MostRecentCompleted = g.Where(log => log.EndTime.HasValue)
                                                .OrderByDescending(log => log.StartTime)
                                                .FirstOrDefault()
                    })
                    .OrderByDescending(x => x.MostRecentLog.StartTime)
                    .Take(10) // Show last 10 unique computers
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"Building Recent tree with {computerGroups.Count} entries from database (from {recentLogs.Count} total logs)");

                foreach (var group in computerGroups)
                {
                    var log = group.MostRecentLog;

                    // Get the actual computer entry for full data
                    var computerEntry = ComputerEntries?.FirstOrDefault(c => c.Id == log.ComputerId);
                    bool isFavorite = computerEntry?.IsFavorite ?? false;

                    // Use computer's LastConnectedAt from database (most reliable)
                    DateTime timeToShow;
                    if (computerEntry?.LastConnectedAt != null)
                    {
                        timeToShow = computerEntry.LastConnectedAt.Value;
                        System.Diagnostics.Debug.WriteLine($"  {log.FriendlyName}: Using LastConnectedAt = {timeToShow}");
                    }
                    else if (group.MostRecentCompleted != null)
                    {
                        timeToShow = group.MostRecentCompleted.StartTime;
                        System.Diagnostics.Debug.WriteLine($"  {log.FriendlyName}: Using MostRecentCompleted = {timeToShow}");
                    }
                    else
                    {
                        timeToShow = log.StartTime;
                        System.Diagnostics.Debug.WriteLine($"  {log.FriendlyName}: Using log.StartTime = {timeToShow}");
                    }

                    var timeAgo = GetTimeAgo(timeToShow);

                    nodes.Add(new Models.TreeNode
                    {
                        Name = log.FriendlyName ?? log.ServerAddress, // Just the friendly name for display
                        TimeAgo = timeAgo, // Time stored separately for template
                        ComputerEntry = computerEntry ?? new ComputerEntry
                        {
                            Id = log.ComputerId,
                            FriendlyName = log.FriendlyName ?? log.ServerAddress,
                            MachineName = log.ServerAddress.Split('.')[0],
                            Domain = log.Domain ?? string.Empty,
                            Group = log.GroupPath ?? string.Empty
                        },
                        IsVisible = true,
                        IsFavorite = isFavorite
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error building Recent tree from database: {ex.Message}");
            }

            RootNodesRecent = nodes;
        }
        
        private string GetTimeAgo(DateTime dateTime)
        {
            var now = DateTime.Now;
            var timeSpan = now - dateTime;

            System.Diagnostics.Debug.WriteLine($"    GetTimeAgo: now={now:yyyy-MM-dd HH:mm:ss}, dateTime={dateTime:yyyy-MM-dd HH:mm:ss}, diff={timeSpan.TotalMinutes:F1} min");

            if (timeSpan.TotalSeconds < 60)
                return "just now";
            if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes} min ago";
            if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours} hours ago";
            if (timeSpan.TotalDays < 7)
                return $"{(int)timeSpan.TotalDays} days ago";

            return dateTime.ToString("MMM d");
        }
        
        public void BuildFavoritesTree()
        {
            var favorites = _connectionHistory?.GetFavorites() ?? new List<ConnectionHistoryEntry>();
            var nodes = new ObservableCollection<Models.TreeNode>();
            
            foreach (var entry in favorites)
            {
                // Build display string with friendly name, group, and computer details
                var parts = new List<string>();
                parts.Add(entry.Name); // Friendly name
                
                if (!string.IsNullOrEmpty(entry.Group))
                    parts.Add($"[{entry.Group}]");
                
                var computerPart = !string.IsNullOrEmpty(entry.Domain) 
                    ? $"{entry.MachineName}.{entry.Domain}" 
                    : entry.MachineName;
                parts.Add($"({computerPart})");
                
                var displayName = string.Join(" ", parts);
                    
                nodes.Add(new Models.TreeNode
                {
                    Name = displayName,
                    ComputerEntry = new ComputerEntry
                    {
                        FriendlyName = entry.Name,
                        MachineName = entry.MachineName,
                        Domain = entry.Domain,
                        Group = entry.Group
                    },
                    IsVisible = true,
                    IsFavorite = true
                });
            }
            
            RootNodesFavorites = nodes;
        }
        
        public bool IsFavorite(string machineName, string domain = null)
        {
            var entry = _connectionHistory?.Entries?.FirstOrDefault(e =>
                e.MachineName.Equals(machineName, StringComparison.OrdinalIgnoreCase) &&
                (e.Domain ?? "").Equals(domain ?? "", StringComparison.OrdinalIgnoreCase));

            return entry?.IsFavorite ?? false;
        }

        private void InitializeNavigation()
        {
            _navigationItems.Add(new NavigationItem
            {
                Type = NavigationItemType.Section,
                Name = "Computers",
                SectionIndex = 0,
                IconData = "M4,6H20V16H4M20,18A2,2 0 0,0 22,16V6C22,4.89 21.1,4 20,4H4C2.89,4 2,4.89 2,6V16A2,2 0 0,0 4,18H0V20H24V18H20Z"
            });

            _navigationItems.Add(new NavigationItem
            {
                Type = NavigationItemType.Section,
                Name = "Servers",
                SectionIndex = 1,
                IconData = "M4,1C2.89,1 2,1.89 2,3V7C2,8.11 2.89,9 4,9H1V11H13V9H10C11.11,9 12,8.11 12,7V3C12,1.89 11.11,1 10,1H4M4,3H10V7H4V3M14,13C12.89,13 12,13.89 12,15V19C12,20.11 12.89,21 14,21H11V23H23V21H20C21.11,21 22,20.11 22,19V15C22,13.89 21.11,13 20,13H14M14,15H20V19H14V15Z"
            });

            _navigationItems.Add(new NavigationItem
            {
                Type = NavigationItemType.Section,
                Name = "Kubernetes",
                SectionIndex = 2,
                IconData = "M13.95,13.5H13.72C13.69,13.3 13.64,13.1 13.56,12.91L13.74,12.73C13.87,12.6 13.87,12.39 13.74,12.26L13.27,11.79C13.14,11.66 12.93,11.66 12.8,11.79L12.62,11.97C12.43,11.89 12.23,11.84 12.03,11.81V11.58C12.03,11.4 11.88,11.25 11.7,11.25H11.03C10.85,11.25 10.7,11.4 10.7,11.58V11.81C10.5,11.84 10.3,11.89 10.11,11.97L9.93,11.79C9.8,11.66 9.59,11.66 9.46,11.79L8.99,12.26C8.86,12.39 8.86,12.6 8.99,12.73L9.17,12.91C9.09,13.1 9.04,13.3 9.01,13.5H8.78C8.6,13.5 8.45,13.65 8.45,13.83V14.5C8.45,14.68 8.6,14.83 8.78,14.83H9.01C9.04,15.03 9.09,15.23 9.17,15.42L8.99,15.6C8.86,15.73 8.86,15.94 8.99,16.07L9.46,16.54C9.59,16.67 9.8,16.67 9.93,16.54L10.11,16.36C10.3,16.44 10.5,16.49 10.7,16.52V16.75C10.7,16.93 10.85,17.08 11.03,17.08H11.7C11.88,17.08 12.03,16.93 12.03,16.75V16.52C12.23,16.49 12.43,16.44 12.62,16.36L12.8,16.54C12.93,16.67 13.14,16.67 13.27,16.54L13.74,16.07C13.87,15.94 13.87,15.73 13.74,15.6L13.56,15.42C13.64,15.23 13.69,15.03 13.72,14.83H13.95C14.13,14.83 14.28,14.68 14.28,14.5V13.83C14.28,13.65 14.13,13.5 13.95,13.5M11.37,15.5A1.33,1.33 0 0,1 10.04,14.17A1.33,1.33 0 0,1 11.37,12.84A1.33,1.33 0 0,1 12.7,14.17A1.33,1.33 0 0,1 11.37,15.5M11.37,4.21L16.66,8.08L15.27,8.58L11.37,5.77L7.46,8.58L6.08,8.08L11.37,4.21M3.69,11.04L4.87,10.58L5.45,11.62L4.19,11.81L3.69,11.04M5.06,17.41L4.46,16.47L5.73,16.29L5.93,17.51L5.06,17.41M11.37,20.03L10.5,19.26L11.37,18.19L12.24,19.26L11.37,20.03M17.68,17.41L16.81,17.51L17,16.29L18.28,16.47L17.68,17.41M19.04,11.04L18.55,11.81L17.29,11.62L17.87,10.58L19.04,11.04Z"
            });

            _navigationItems.Add(new NavigationItem
            {
                Type = NavigationItemType.Section,
                Name = "Recent",
                SectionIndex = 3,
                IconData = "M13,3A9,9 0 0,0 4,12H1L4.89,15.89L4.96,16.03L9,12H6A7,7 0 0,1 13,5A7,7 0 0,1 20,12A7,7 0 0,1 13,19C11.07,19 9.32,18.21 8.06,16.94L6.64,18.36C8.27,20 10.5,21 13,21A9,9 0 0,0 22,12A9,9 0 0,0 13,3Z"
            });

            _navigationItems.Add(new NavigationItem
            {
                Type = NavigationItemType.Section,
                Name = "Favorites",
                SectionIndex = 4,
                IconData = "M12,17.27L18.18,21L16.54,13.97L22,9.24L14.81,8.62L12,2L9.19,8.62L2,9.24L7.45,13.97L5.82,21L12,17.27Z"
            });

            _navigationItems.Add(new NavigationItem
            {
                Type = NavigationItemType.Section,
                Name = "Active Sessions",
                SectionIndex = 5,
                IconData = "M21,16V4H3V16H21M21,2A2,2 0 0,1 23,4V16A2,2 0 0,1 21,18H14V20H16V22H8V20H10V18H3C1.89,18 1,17.1 1,16V4C1,2.89 1.89,2 3,2H21M5,6H14V11H5V6M15,6H19V8H15V6M19,9V14H15V9H19M5,12H9V14H5V12M10,12H14V14H10V12Z",
                IsVisible = false,  // Hidden by default (no sessions)
                IsEnabled = false   // Not clickable
            });
        }

        private void ActiveSessions_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            // Find the Active Sessions section header
            var activeSessionsHeader = _navigationItems.FirstOrDefault(n => n.SectionIndex == 5);
            int activeSectionIndex = _navigationItems.IndexOf(activeSessionsHeader ?? _navigationItems.Last());

            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                // Add new session items after Active Sessions section
                foreach (RdpSession session in e.NewItems)
                {
                    var navItem = new NavigationItem
                    {
                        Type = NavigationItemType.Session,
                        Name = session.DisplayName,
                        Session = session,
                        StatusText = session.StatusText,
                        StatusColor = session.StatusColor
                    };

                    // Subscribe to session property changes to update navigation item
                    session.PropertyChanged += (s, args) =>
                    {
                        if (args.PropertyName == nameof(RdpSession.StatusText))
                            navItem.StatusText = session.StatusText;
                        if (args.PropertyName == nameof(RdpSession.StatusColor))
                            navItem.StatusColor = session.StatusColor;
                        if (args.PropertyName == nameof(RdpSession.DisplayName))
                            navItem.Name = session.DisplayName;
                    };

                    _navigationItems.Insert(activeSectionIndex + 1, navItem);
                }
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove && e.OldItems != null)
            {
                // Remove session items
                foreach (RdpSession session in e.OldItems)
                {
                    var navItem = _navigationItems.FirstOrDefault(n => n.Session?.SessionId == session.SessionId);
                    if (navItem != null)
                    {
                        _navigationItems.Remove(navItem);
                    }
                }
            }

            // Update Active Sessions header visibility based on whether there are any sessions
            if (activeSessionsHeader != null)
            {
                activeSessionsHeader.IsVisible = _activeSessions.Count > 0;
            }
        }

        #region Drag-Drop Handlers

        /// <summary>
        /// Handle drag-drop operation for computers tree
        /// </summary>
        public void HandleDragDrop(Models.TreeNode sourceNode, Models.TreeNode targetNode, RdpManager.Helpers.DropPosition position)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== VIEWMODEL HandleDragDrop called ===");
                System.Diagnostics.Debug.WriteLine($"Source: {sourceNode.Name} (GroupPath: {sourceNode.GroupPath}, IsLeaf: {sourceNode.IsLeaf})");
                System.Diagnostics.Debug.WriteLine($"Target: {targetNode.Name} (GroupPath: {targetNode.GroupPath}, IsLeaf: {targetNode.IsLeaf})");
                System.Diagnostics.Debug.WriteLine($"Position: {position}");

                var dragDropHelper = new RdpManager.Helpers.DragDropHelper();
                var result = dragDropHelper.ExecuteDrop(sourceNode, targetNode, position);

                System.Diagnostics.Debug.WriteLine($"Drop result: Success={result.Success}, RequiresTreeRebuild={result.RequiresTreeRebuild}");

                if (result.Success)
                {
                    // Reload computer entries from database
                    var computerRepo = new RdpManager.Data.Repositories.ComputerRepository();
                    var computers = computerRepo.GetAll();
                    ComputerEntries = new ObservableCollection<Models.ComputerEntry>(computers);

                    System.Diagnostics.Debug.WriteLine($"Reloaded {computers.Count} computers from database");

                    // Rebuild tree to reflect new ordering
                    if (result.RequiresTreeRebuild)
                    {
                        System.Diagnostics.Debug.WriteLine("Rebuilding tree...");
                        BuildComputerEntriesTree();
                        System.Diagnostics.Debug.WriteLine("Tree rebuilt successfully");
                    }

                    System.Diagnostics.Debug.WriteLine("=== Drag-drop operation completed successfully ===");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Drag-drop operation failed: {result.ErrorMessage}");
                    System.Windows.MessageBox.Show(
                        $"Unable to complete drag-drop operation:\n\n{result.ErrorMessage}",
                        "Drag-Drop Failed",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in drag-drop handler: {ex.Message}\n{ex.StackTrace}");
                System.Windows.MessageBox.Show(
                    $"An error occurred during drag-drop:\n\n{ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        #endregion

        #region File Explorer Session Management

        /// <summary>
        /// Load File Explorer sessions from database
        /// </summary>
        private void LoadFileExplorerSessions()
        {
            try
            {
                var fileExplorerRepo = new RdpManager.Data.Repositories.FileExplorerSessionRepository();
                var sessions = fileExplorerRepo.GetAll();

                System.Diagnostics.Debug.WriteLine($"Loaded {sessions.Count} File Explorer sessions from database");

                foreach (var session in sessions)
                {
                    _fileExplorerSessions.Add(session);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading File Explorer sessions from database: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle File Explorer sessions collection changed event
        /// </summary>
        private void FileExplorerSessions_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[FileExplorerSessions] Collection changed: Action={e.Action}");

            // Find the File Explorer header navigation item (if it exists)
            var fileExplorerHeader = _navigationItems.FirstOrDefault(n => n.SectionIndex == 6);

            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                // Session added - create File Explorer header if it doesn't exist
                if (fileExplorerHeader == null && _fileExplorerSessions.Count == 1)
                {
                    System.Diagnostics.Debug.WriteLine("[FileExplorerSessions] Adding File Explorer navigation section");

                    var fileExplorerNav = new NavigationItem
                    {
                        Type = NavigationItemType.Section,
                        Name = "File Explorer",
                        IconData = "M10,4H4C2.89,4 2,4.89 2,6V18A2,2 0 0,0 4,20H20A2,2 0 0,0 22,18V8C22,6.89 21.1,6 20,6H12L10,4Z",
                        SectionIndex = 6,
                        IsVisible = true,
                        IsEnabled = true
                    };

                    // Insert after Active Sessions (index 5)
                    int insertIndex = _navigationItems.Count;
                    for (int i = 0; i < _navigationItems.Count; i++)
                    {
                        if (_navigationItems[i].SectionIndex > 6)
                        {
                            insertIndex = i;
                            break;
                        }
                    }

                    _navigationItems.Insert(insertIndex, fileExplorerNav);
                }

                // Save new sessions to database
                foreach (FileExplorerSession session in e.NewItems)
                {
                    try
                    {
                        var repo = new RdpManager.Data.Repositories.FileExplorerSessionRepository();
                        repo.Insert(session);
                        System.Diagnostics.Debug.WriteLine($"[FileExplorerSessions] Saved new session to database: {session.DisplayName}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[FileExplorerSessions] Error saving session: {ex.Message}");
                    }
                }
            }
            else if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Remove && e.OldItems != null)
            {
                // Session removed - remove File Explorer header if no more sessions
                if (_fileExplorerSessions.Count == 0 && fileExplorerHeader != null)
                {
                    System.Diagnostics.Debug.WriteLine("[FileExplorerSessions] Removing File Explorer navigation section");
                    _navigationItems.Remove(fileExplorerHeader);
                }
            }

            // Update File Explorer header visibility
            if (fileExplorerHeader != null)
            {
                fileExplorerHeader.IsVisible = _fileExplorerSessions.Count > 0;
            }
        }

        #endregion
    }
}
