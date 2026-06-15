using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using RdpManager.Data.Repositories;
using RdpManager.Dialogs;
using RdpManager.Models;
using RdpManager.Services;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Clipboard = System.Windows.Clipboard;
using FontFamily = System.Windows.Media.FontFamily;
using Orientation = System.Windows.Controls.Orientation;
using TextBox = System.Windows.Controls.TextBox;

namespace RdpManager.Views
{
    public partial class KubernetesView : System.Windows.Controls.UserControl
    {
        private readonly KubernetesService _kubernetesService = new();
        private readonly KubernetesClusterRepository _clusterRepo = new();
        private ObservableCollection<KubernetesCluster> _clusters = new();
        private KubernetesCluster? _selectedCluster;
        private string _selectedNamespace = "";
        private string _selectedResourceType = "";
        private readonly Dictionary<Guid, CancellationTokenSource> _logStreams = new();
        private readonly Dictionary<Guid, Process> _portForwards = new();
        private readonly Dictionary<Guid, Process> _terminalProcesses = new();

        public KubernetesView()
        {
            InitializeComponent();
            LoadClusters();
        }

        #region Cluster Management

        private void LoadClusters()
        {
            try
            {
                var clusters = _clusterRepo.GetAll();
                _clusters = new ObservableCollection<KubernetesCluster>(clusters);
                RebuildClusterTree();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading clusters: {ex.Message}");
            }
        }

        private void RebuildClusterTree()
        {
            ClusterTreeView.Items.Clear();

            foreach (var cluster in _clusters)
            {
                var clusterItem = new TreeViewItem
                {
                    Header = CreateClusterHeader(cluster),
                    Tag = cluster,
                    IsExpanded = true
                };

                // Add resource type nodes with themed icons
                var resourceTypes = new[]
                {
                    ("Pods", "M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2Z"),
                    ("Deployments", "M19,12V13.5A4,4 0 0,1 23,17.5C23,18.32 22.75,19.08 22.33,19.71L21.24,18.62C21.41,18.28 21.5,17.9 21.5,17.5A2.5,2.5 0 0,0 19,15V16.5L16.75,14.25L19,12M19,23V21.5A4,4 0 0,1 15,17.5C15,16.68 15.25,15.92 15.67,15.29L16.76,16.38C16.59,16.72 16.5,17.1 16.5,17.5A2.5,2.5 0 0,0 19,20V18.5L21.25,20.75L19,23M10,2H14A2,2 0 0,1 16,4V6H20A2,2 0 0,1 22,8V13.53A5.99,5.99 0 0,0 13.06,22H4A2,2 0 0,1 2,20V8A2,2 0 0,1 4,6H8V4A2,2 0 0,1 10,2M14,6V4H10V6H14Z"),
                    ("Events", "M14,2H6A2,2 0 0,0 4,4V20A2,2 0 0,0 6,22H18A2,2 0 0,0 20,20V8L14,2M18,20H6V4H13V9H18V20Z")
                };

                foreach (var (name, iconPath) in resourceTypes)
                {
                    var rtItem = new TreeViewItem
                    {
                        Tag = new { Cluster = cluster, ResourceType = name }
                    };
                    rtItem.Header = CreateResourceTypeHeader(name, iconPath);
                    clusterItem.Items.Add(rtItem);
                }

                clusterItem.ContextMenu = CreateClusterContextMenu(cluster);
                ClusterTreeView.Items.Add(clusterItem);
            }
        }

        private StackPanel CreateClusterHeader(KubernetesCluster cluster)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };

            var statusDot = new System.Windows.Shapes.Ellipse
            {
                Width = 8, Height = 8,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            void UpdateColor()
            {
                statusDot.Fill = new SolidColorBrush(
                    cluster.IsAuthenticated
                        ? (Color)ColorConverter.ConvertFromString("#4CAF50")
                        : (Color)ColorConverter.ConvertFromString("#9E9E9E"));
            }
            UpdateColor();
            cluster.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(KubernetesCluster.IsAuthenticated))
                    Dispatcher.Invoke(UpdateColor);
            };

            panel.Children.Add(statusDot);

            var nameBlock = new TextBlock
            {
                Text = cluster.DisplayName,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13
            };
            nameBlock.SetResourceReference(TextBlock.ForegroundProperty, "App.Text.Primary");
            panel.Children.Add(nameBlock);

            return panel;
        }

        private StackPanel CreateResourceTypeHeader(string name, string iconPath)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };

            var icon = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(iconPath),
                Width = 12, Height = 12,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 0, 6, 0)
            };
            icon.SetResourceReference(System.Windows.Shapes.Path.FillProperty, "App.Text.Secondary");
            panel.Children.Add(icon);

            var text = new TextBlock { Text = name, FontSize = 13 };
            text.SetResourceReference(TextBlock.ForegroundProperty, "App.Text.Primary");
            panel.Children.Add(text);

            return panel;
        }

        private ContextMenu CreateClusterContextMenu(KubernetesCluster cluster)
        {
            var menu = new ContextMenu();
            menu.SetResourceReference(ContextMenu.StyleProperty, "CompactContextMenu");

            var authItem = new MenuItem { Header = "Authenticate" };
            authItem.SetResourceReference(MenuItem.StyleProperty, "CompactMenuItem");
            authItem.Click += async (s, e) => await AuthenticateCluster(cluster);
            menu.Items.Add(authItem);

            var terminalItem = new MenuItem { Header = "Open Terminal" };
            terminalItem.SetResourceReference(MenuItem.StyleProperty, "CompactMenuItem");
            terminalItem.Click += (s, e) => OpenClusterTerminal(cluster);
            menu.Items.Add(terminalItem);

            menu.Items.Add(new Separator());

            var editItem = new MenuItem { Header = "Edit Cluster" };
            editItem.SetResourceReference(MenuItem.StyleProperty, "CompactMenuItem");
            editItem.Click += async (s, e) => await EditCluster(cluster);
            menu.Items.Add(editItem);

            var deleteItem = new MenuItem { Header = "Remove Cluster" };
            deleteItem.SetResourceReference(MenuItem.StyleProperty, "CompactMenuItem");
            deleteItem.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F44336"));
            deleteItem.Click += async (s, e) => await DeleteCluster(cluster);
            menu.Items.Add(deleteItem);

            return menu;
        }

        private async void AddClusterButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AddEditKubernetesClusterDialog();
            var result = await DialogHost.Show(dialog, "RootDialogHost");

            if (result is KubernetesCluster cluster)
            {
                _clusterRepo.Insert(cluster);
                _clusters.Add(cluster);
                RebuildClusterTree();
            }
        }

        private async Task EditCluster(KubernetesCluster cluster)
        {
            var dialog = new AddEditKubernetesClusterDialog(cluster);
            var result = await DialogHost.Show(dialog, "RootDialogHost");

            if (result is KubernetesCluster updated)
            {
                _clusterRepo.Update(updated);
                var idx = _clusters.IndexOf(cluster);
                if (idx >= 0)
                {
                    _clusters[idx] = updated;
                }
                RebuildClusterTree();
            }
        }

        private async Task DeleteCluster(KubernetesCluster cluster)
        {
            var confirmDialog = new ConfirmDialog(
                "Remove Cluster",
                $"Are you sure you want to remove '{cluster.DisplayName}'?");
            var result = await DialogHost.Show(confirmDialog, "RootDialogHost");

            if (result is true)
            {
                _clusterRepo.Delete(cluster.ClusterId);
                _clusters.Remove(cluster);
                RebuildClusterTree();
            }
        }

        private async Task AuthenticateCluster(KubernetesCluster cluster)
        {
            StatusMessage.Text = $"Authenticating to {cluster.DisplayName}...";
            StatusMessage.Visibility = Visibility.Visible;

            var (success, output, error) = await _kubernetesService.AuthenticateClusterAsync(cluster);

            // Detect expired/missing gcloud credentials and offer to re-login
            if (!success && NeedsGcloudLogin(error))
            {
                var confirmDialog = new ConfirmDialog(
                    "Google Cloud Login Required",
                    "Your gcloud credentials have expired or are missing.\n\nRun 'gcloud auth login' to re-authenticate? This will open your browser.");
                var result = await DialogHost.Show(confirmDialog, "RootDialogHost");

                if (result is true)
                {
                    StatusMessage.Text = "Waiting for gcloud auth login (check your browser)...";

                    var (loginSuccess, loginError) = await _kubernetesService.GcloudAuthLoginAsync();
                    if (loginSuccess)
                    {
                        // Retry cluster authentication after login
                        StatusMessage.Text = $"Re-authenticating to {cluster.DisplayName}...";
                        (success, output, error) = await _kubernetesService.AuthenticateClusterAsync(cluster);
                    }
                    else
                    {
                        StatusMessage.Text = $"gcloud auth login failed: {loginError}";
                        ShowInBottomPanel("Login Error", loginError);
                        return;
                    }
                }
            }

            if (success)
            {
                cluster.IsAuthenticated = true;
                cluster.LastAuthenticatedAt = DateTime.Now;
                StatusMessage.Text = $"Authenticated to {cluster.DisplayName}";

                var (namespaces, nsError) = await _kubernetesService.GetNamespacesAsync(cluster);
                if (namespaces.Count > 0)
                {
                    NamespaceComboBox.Items.Clear();
                    foreach (var ns in namespaces)
                        NamespaceComboBox.Items.Add(ns);

                    var defaultNs = namespaces.Contains(cluster.DefaultNamespace)
                        ? cluster.DefaultNamespace : namespaces.First();
                    NamespaceComboBox.SelectedItem = defaultNs;
                    NamespaceComboBox.Visibility = Visibility.Visible;
                }
            }
            else
            {
                cluster.IsAuthenticated = false;
                StatusMessage.Text = $"Authentication failed: {error}";
                ShowInBottomPanel("Auth Error", error);
            }
        }

        private static bool NeedsGcloudLogin(string error)
        {
            if (string.IsNullOrEmpty(error)) return false;
            var lower = error.ToLowerInvariant();
            return lower.Contains("obtain new credentials") ||
                   lower.Contains("reauth") ||
                   lower.Contains("refresh token") ||
                   lower.Contains("token has been expired") ||
                   lower.Contains("token has been revoked") ||
                   lower.Contains("invalid_grant") ||
                   lower.Contains("login is required") ||
                   lower.Contains("not logged in") ||
                   lower.Contains("credentials are not valid") ||
                   lower.Contains("gcloud auth login");
        }

        #endregion

        #region Navigation & Resource Loading

        private void ClusterTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem item)
            {
                if (item.Tag is KubernetesCluster cluster)
                {
                    _selectedCluster = cluster;
                    _selectedResourceType = "";
                    ResourceTypeLabel.Text = cluster.DisplayName;
                    HideAllGrids();
                    StatusMessage.Text = "Select a resource type (Pods, Deployments, Events)";
                    StatusMessage.Visibility = Visibility.Visible;

                    if (!cluster.IsAuthenticated)
                    {
                        StatusMessage.Text = "Right-click the cluster to authenticate first";
                    }
                }
                else if (item.Tag != null)
                {
                    dynamic tag = item.Tag;
                    _selectedCluster = tag.Cluster;
                    _selectedResourceType = tag.ResourceType;
                    ResourceTypeLabel.Text = $"{_selectedCluster.DisplayName} - {_selectedResourceType}";

                    if (!_selectedCluster.IsAuthenticated)
                    {
                        StatusMessage.Text = "Authenticate to the cluster first (right-click cluster)";
                        StatusMessage.Visibility = Visibility.Visible;
                        return;
                    }

                    NamespaceComboBox.Visibility = Visibility.Visible;
                    ResourceSearchBox.Visibility = Visibility.Visible;
                    _ = LoadResourcesAsync();
                }
            }
        }

        private async void NamespaceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (NamespaceComboBox.SelectedItem is string ns)
            {
                _selectedNamespace = ns;
                if (!string.IsNullOrEmpty(_selectedResourceType))
                    await LoadResourcesAsync();
            }
        }

        private async Task LoadResourcesAsync()
        {
            if (_selectedCluster == null || string.IsNullOrEmpty(_selectedNamespace))
                return;

            HideAllGrids();
            StatusMessage.Text = $"Loading {_selectedResourceType}...";
            StatusMessage.Visibility = Visibility.Visible;

            switch (_selectedResourceType)
            {
                case "Pods":
                    await LoadPodsAsync();
                    break;
                case "Deployments":
                    await LoadDeploymentsAsync();
                    break;
                case "Events":
                    await LoadEventsAsync();
                    break;
            }
        }

        private async Task LoadPodsAsync()
        {
            var (pods, error) = await _kubernetesService.GetPodsAsync(_selectedCluster!, _selectedNamespace);

            if (!string.IsNullOrEmpty(error))
            {
                StatusMessage.Text = $"Error: {error}";
                return;
            }

            var (resources, _) = await _kubernetesService.GetTopPodsAsync(_selectedCluster!, _selectedNamespace);

            PodsDataGrid.ItemsSource = pods;
            PodsDataGrid.Visibility = Visibility.Visible;
            StatusMessage.Visibility = Visibility.Collapsed;
        }

        private async Task LoadDeploymentsAsync()
        {
            var (deployments, error) = await _kubernetesService.GetDeploymentsAsync(_selectedCluster!, _selectedNamespace);

            if (!string.IsNullOrEmpty(error))
            {
                StatusMessage.Text = $"Error: {error}";
                return;
            }

            DeploymentsDataGrid.ItemsSource = deployments;
            DeploymentsDataGrid.Visibility = Visibility.Visible;
            StatusMessage.Visibility = Visibility.Collapsed;
        }

        private async Task LoadEventsAsync()
        {
            var (events, error) = await _kubernetesService.GetEventsAsync(_selectedCluster!, _selectedNamespace);

            if (!string.IsNullOrEmpty(error))
            {
                StatusMessage.Text = $"Error: {error}";
                return;
            }

            EventsDataGrid.ItemsSource = events;
            EventsDataGrid.Visibility = Visibility.Visible;
            StatusMessage.Visibility = Visibility.Collapsed;
        }

        private void HideAllGrids()
        {
            PodsDataGrid.Visibility = Visibility.Collapsed;
            DeploymentsDataGrid.Visibility = Visibility.Collapsed;
            EventsDataGrid.Visibility = Visibility.Collapsed;
            DescribeTextBox.Visibility = Visibility.Collapsed;
        }

        #endregion

        #region Pod Actions

        private void PodsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (PodsDataGrid.SelectedItem is KubernetesPod pod)
                ViewPodLogs(pod, streaming: true);
        }

        private void ViewLogsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (PodsDataGrid.SelectedItem is KubernetesPod pod)
                ViewPodLogs(pod, streaming: true);
        }

        private void ViewPreviousLogsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (PodsDataGrid.SelectedItem is KubernetesPod pod)
                ViewPreviousLogs(pod);
        }

        private void OpenTerminalMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (PodsDataGrid.SelectedItem is KubernetesPod pod)
                OpenTerminal(pod);
        }

        private async void DescribePodMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (PodsDataGrid.SelectedItem is KubernetesPod pod && _selectedCluster != null)
            {
                var (description, error) = await _kubernetesService.DescribePodAsync(_selectedCluster, pod);
                if (!string.IsNullOrEmpty(error))
                    ShowInBottomPanel($"Error - {pod.Name}", error);
                else
                    ShowInBottomPanel($"Describe: {pod.Name}", description);
            }
        }

        private async void PortForwardMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (PodsDataGrid.SelectedItem is KubernetesPod pod && _selectedCluster != null)
            {
                var process = _kubernetesService.StartPortForward(_selectedCluster, pod, 8080, 8080);
                if (process != null)
                {
                    var id = Guid.NewGuid();
                    _portForwards[id] = process;
                    ShowInBottomPanel($"Port Forward: {pod.Name}", $"Forwarding localhost:8080 -> {pod.Name}:8080\nClose this tab to stop forwarding.");
                }
            }
        }

        private void CopyPodNameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (PodsDataGrid.SelectedItem is KubernetesPod pod)
                Clipboard.SetText(pod.Name);
        }

        private async void DeletePodMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (PodsDataGrid.SelectedItem is KubernetesPod pod && _selectedCluster != null)
            {
                var confirmDialog = new ConfirmDialog(
                    "Delete Pod",
                    $"Are you sure you want to delete pod '{pod.Name}'?\nKubernetes will recreate it if managed by a deployment.");
                var result = await DialogHost.Show(confirmDialog, "RootDialogHost");

                if (result is true)
                {
                    var (success, output, error) = await _kubernetesService.DeletePodAsync(_selectedCluster, pod);
                    if (success)
                        await LoadPodsAsync();
                    else
                        ShowInBottomPanel("Delete Error", error);
                }
            }
        }

        private void ViewPodLogs(KubernetesPod pod, bool streaming)
        {
            if (_selectedCluster == null) return;

            var tabId = Guid.NewGuid();
            var logTextBox = CreateThemedTextBox();

            var tabHeader = streaming ? $"Logs: {pod.Name}" : $"Logs (static): {pod.Name}";
            AddBottomTab(tabHeader, logTextBox, tabId);

            if (streaming)
            {
                var cts = new CancellationTokenSource();
                _logStreams[tabId] = cts;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _kubernetesService.StreamPodLogsAsync(_selectedCluster, pod, null, line =>
                        {
                            Dispatcher.BeginInvoke(() =>
                            {
                                logTextBox.AppendText(line + "\n");
                                logTextBox.ScrollToEnd();
                            });
                        }, cts.Token);
                    }
                    catch (OperationCanceledException) { }
                    catch (Exception ex)
                    {
                        Dispatcher.BeginInvoke(() =>
                        {
                            logTextBox.AppendText($"\n--- Stream error: {ex.Message} ---\n");
                        });
                    }
                });
            }
            else
            {
                _ = Task.Run(async () =>
                {
                    var (logs, error) = await _kubernetesService.GetPodLogsAsync(_selectedCluster, pod, null);
                    Dispatcher.BeginInvoke(() =>
                    {
                        logTextBox.Text = string.IsNullOrEmpty(error) ? logs : $"Error: {error}";
                    });
                });
            }
        }

        private void ViewPreviousLogs(KubernetesPod pod)
        {
            if (_selectedCluster == null) return;

            var logTextBox = CreateThemedTextBox();
            AddBottomTab($"Prev Logs: {pod.Name}", logTextBox);

            var cluster = _selectedCluster;
            _ = Task.Run(async () =>
            {
                var (logs, error) = await _kubernetesService.GetPreviousPodLogsAsync(cluster, pod, null);
                Dispatcher.BeginInvoke(() =>
                {
                    logTextBox.Text = string.IsNullOrEmpty(error) ? logs : $"Error: {error}";
                });
            });
        }

        private void OpenTerminal(KubernetesPod pod)
        {
            if (_selectedCluster == null) return;

            try
            {
                var process = _kubernetesService.StartPodExecProcess(_selectedCluster, pod);
                if (process == null)
                {
                    ShowInBottomPanel("Terminal Error", "Failed to start kubectl exec process.");
                    return;
                }

                var tabId = Guid.NewGuid();
                _terminalProcesses[tabId] = process;

                var terminalTextBox = new TextBox
                {
                    IsReadOnly = false,
                    AcceptsReturn = false,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(8)
                };
                terminalTextBox.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "App.Background.Surface");
                terminalTextBox.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "App.Text.Primary");
                terminalTextBox.SetResourceReference(TextBox.CaretBrushProperty, "App.Text.Primary");

                // Track where user input starts (after all output)
                int inputStartIndex = 0;

                void AppendOutput(string text)
                {
                    terminalTextBox.AppendText(text);
                    terminalTextBox.CaretIndex = terminalTextBox.Text.Length;
                    terminalTextBox.ScrollToEnd();
                    inputStartIndex = terminalTextBox.Text.Length;
                }

                // Handle Enter key to send input to the process
                terminalTextBox.PreviewKeyDown += (s, e) =>
                {
                    if (e.Key == Key.Enter)
                    {
                        e.Handled = true;
                        var currentText = terminalTextBox.Text;
                        var userInput = currentText.Substring(inputStartIndex);
                        try
                        {
                            process.StandardInput.WriteLine(userInput);
                            process.StandardInput.Flush();
                        }
                        catch { }
                        terminalTextBox.AppendText("\n");
                        inputStartIndex = terminalTextBox.Text.Length;
                    }
                    // Prevent editing/deleting output text
                    else if (e.Key == Key.Back || e.Key == Key.Delete)
                    {
                        if (terminalTextBox.CaretIndex <= inputStartIndex &&
                            terminalTextBox.SelectionLength == 0)
                        {
                            e.Handled = true;
                        }
                    }
                };

                // Prevent caret from moving into output area for editing
                terminalTextBox.PreviewTextInput += (s, e) =>
                {
                    if (terminalTextBox.CaretIndex < inputStartIndex)
                    {
                        terminalTextBox.CaretIndex = terminalTextBox.Text.Length;
                    }
                };

                // Read stdout async
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var buffer = new char[1024];
                        int bytesRead;
                        while ((bytesRead = await process.StandardOutput.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            var text = new string(buffer, 0, bytesRead);
                            Dispatcher.BeginInvoke(() => AppendOutput(text));
                        }
                    }
                    catch { }
                    finally
                    {
                        Dispatcher.BeginInvoke(() =>
                            AppendOutput("\n--- Session ended ---\n"));
                    }
                });

                // Read stderr async
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var buffer = new char[1024];
                        int bytesRead;
                        while ((bytesRead = await process.StandardError.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            var text = new string(buffer, 0, bytesRead);
                            Dispatcher.BeginInvoke(() => AppendOutput(text));
                        }
                    }
                    catch { }
                });

                AddBottomTab($"Terminal: {pod.Name}", terminalTextBox, tabId);
                terminalTextBox.Focus();

                // Set a visible prompt and show a connection banner
                try
                {
                    process.StandardInput.WriteLine($"export PS1='{pod.Name}$ '");
                    process.StandardInput.WriteLine($"echo '--- Connected to {pod.Name} ({pod.Namespace}) ---'");
                    process.StandardInput.Flush();
                }
                catch { }
            }
            catch (Exception ex)
            {
                ShowInBottomPanel("Terminal Error", $"Failed to open terminal: {ex.Message}");
            }
        }

        private void OpenClusterTerminal(KubernetesCluster cluster)
        {
            try
            {
                var kubeconfigPath = KubernetesService.GetClusterKubeconfigPath(cluster);

                var psi = new ProcessStartInfo("cmd.exe")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = true
                };

                psi.Environment["KUBECONFIG"] = kubeconfigPath;
                if (!string.IsNullOrEmpty(cluster.ProxyAddress))
                {
                    psi.Environment["HTTPS_PROXY"] = cluster.ProxyAddress;
                    psi.Environment["https_proxy"] = cluster.ProxyAddress;
                    psi.Environment["HTTP_PROXY"] = cluster.ProxyAddress;
                    psi.Environment["http_proxy"] = cluster.ProxyAddress;
                }

                var process = new Process { StartInfo = psi };
                process.Start();

                var tabId = Guid.NewGuid();
                _terminalProcesses[tabId] = process;

                var terminalTextBox = new TextBox
                {
                    IsReadOnly = false,
                    AcceptsReturn = false,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 12,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(8)
                };
                terminalTextBox.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "App.Background.Surface");
                terminalTextBox.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "App.Text.Primary");
                terminalTextBox.SetResourceReference(TextBox.CaretBrushProperty, "App.Text.Primary");

                int inputStartIndex = 0;

                void AppendOutput(string text)
                {
                    terminalTextBox.AppendText(text);
                    terminalTextBox.CaretIndex = terminalTextBox.Text.Length;
                    terminalTextBox.ScrollToEnd();
                    inputStartIndex = terminalTextBox.Text.Length;
                }

                terminalTextBox.PreviewKeyDown += (s, e) =>
                {
                    if (e.Key == Key.Enter)
                    {
                        e.Handled = true;
                        var userInput = terminalTextBox.Text.Substring(inputStartIndex);
                        try
                        {
                            process.StandardInput.WriteLine(userInput);
                            process.StandardInput.Flush();
                        }
                        catch { }
                        terminalTextBox.AppendText("\n");
                        inputStartIndex = terminalTextBox.Text.Length;
                    }
                    else if (e.Key == Key.Back || e.Key == Key.Delete)
                    {
                        if (terminalTextBox.CaretIndex <= inputStartIndex &&
                            terminalTextBox.SelectionLength == 0)
                        {
                            e.Handled = true;
                        }
                    }
                };

                terminalTextBox.PreviewTextInput += (s, e) =>
                {
                    if (terminalTextBox.CaretIndex < inputStartIndex)
                        terminalTextBox.CaretIndex = terminalTextBox.Text.Length;
                };

                // Read stdout
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var buffer = new char[1024];
                        int bytesRead;
                        while ((bytesRead = await process.StandardOutput.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            var text = new string(buffer, 0, bytesRead);
                            Dispatcher.BeginInvoke(() => AppendOutput(text));
                        }
                    }
                    catch { }
                    finally
                    {
                        Dispatcher.BeginInvoke(() => AppendOutput("\n--- Session ended ---\n"));
                    }
                });

                // Read stderr
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var buffer = new char[1024];
                        int bytesRead;
                        while ((bytesRead = await process.StandardError.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            var text = new string(buffer, 0, bytesRead);
                            Dispatcher.BeginInvoke(() => AppendOutput(text));
                        }
                    }
                    catch { }
                });

                AddBottomTab($"kubectl: {cluster.DisplayName}", terminalTextBox, tabId);
                terminalTextBox.Focus();

                // Suppress the default cmd banner and show our own
                try
                {
                    process.StandardInput.WriteLine("@echo off");
                    process.StandardInput.WriteLine($"echo --- Cluster Terminal: {cluster.DisplayName} ---");
                    process.StandardInput.WriteLine($"echo KUBECONFIG={kubeconfigPath}");
                    process.StandardInput.WriteLine("echo.");
                    process.StandardInput.WriteLine("prompt $G ");
                    process.StandardInput.Flush();
                }
                catch { }
            }
            catch (Exception ex)
            {
                ShowInBottomPanel("Terminal Error", $"Failed to open cluster terminal: {ex.Message}");
            }
        }

        #endregion

        #region Deployment Actions

        private async void ScaleDeploymentMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DeploymentsDataGrid.SelectedItem is KubernetesDeployment deployment && _selectedCluster != null)
            {
                var inputDialog = new GroupRenameDialog(deployment.Replicas.ToString());
                var result = await DialogHost.Show(inputDialog, "RootDialogHost");

                if (result is string replicaStr && int.TryParse(replicaStr, out int replicas))
                {
                    var confirmDialog = new ConfirmDialog(
                        "Scale Deployment",
                        $"Scale '{deployment.Name}' from {deployment.Replicas} to {replicas} replicas?");
                    var confirmed = await DialogHost.Show(confirmDialog, "RootDialogHost");

                    if (confirmed is true)
                    {
                        var (success, output, error) = await _kubernetesService.ScaleDeploymentAsync(
                            _selectedCluster, _selectedNamespace, deployment.Name, replicas);

                        if (success)
                            await LoadDeploymentsAsync();
                        else
                            ShowInBottomPanel("Scale Error", error);
                    }
                }
            }
        }

        private async void RestartDeploymentMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DeploymentsDataGrid.SelectedItem is KubernetesDeployment deployment && _selectedCluster != null)
            {
                var confirmDialog = new ConfirmDialog(
                    "Restart Deployment",
                    $"Are you sure you want to restart '{deployment.Name}'?");
                var result = await DialogHost.Show(confirmDialog, "RootDialogHost");

                if (result is true)
                {
                    var (success, output, error) = await _kubernetesService.RestartDeploymentAsync(
                        _selectedCluster, _selectedNamespace, deployment.Name);

                    ShowInBottomPanel(success ? "Restart" : "Restart Error",
                        success ? output : error);
                }
            }
        }

        private async void RolloutStatusMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DeploymentsDataGrid.SelectedItem is KubernetesDeployment deployment && _selectedCluster != null)
            {
                var (status, error) = await _kubernetesService.GetRolloutStatusAsync(
                    _selectedCluster, _selectedNamespace, deployment.Name);
                ShowInBottomPanel($"Rollout Status: {deployment.Name}",
                    string.IsNullOrEmpty(error) ? status : error);
            }
        }

        private async void RolloutHistoryMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (DeploymentsDataGrid.SelectedItem is KubernetesDeployment deployment && _selectedCluster != null)
            {
                var (history, error) = await _kubernetesService.GetRolloutHistoryAsync(
                    _selectedCluster, _selectedNamespace, deployment.Name);
                ShowInBottomPanel($"Rollout History: {deployment.Name}",
                    string.IsNullOrEmpty(error) ? history : error);
            }
        }

        #endregion

        #region Toolbar Actions

        private async void RefreshAllButton_Click(object sender, RoutedEventArgs e)
        {
            LoadClusters();
        }

        private async void RefreshResourcesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_selectedResourceType))
                await LoadResourcesAsync();
        }

        private void ResourceSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var filter = ResourceSearchBox.Text.ToLowerInvariant();

            if (PodsDataGrid.Visibility == Visibility.Visible && PodsDataGrid.ItemsSource is List<KubernetesPod> pods)
            {
                if (string.IsNullOrEmpty(filter))
                    PodsDataGrid.ItemsSource = pods;
                else
                    PodsDataGrid.ItemsSource = pods.Where(p =>
                        p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        p.Status.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
            }
        }

        #endregion

        #region Bottom Panel

        private void ShowInBottomPanel(string title, string content)
        {
            var textBox = CreateThemedTextBox();
            textBox.Text = content;
            AddBottomTab(title, textBox);
        }

        private void AddBottomTab(string header, UIElement content, Guid? tabId = null)
        {
            BottomPanel.Visibility = Visibility.Visible;
            BottomSplitter.Visibility = Visibility.Visible;

            var grid = (Grid)BottomPanel.Parent;
            if (grid.RowDefinitions[2].Height.Value < 200)
                grid.RowDefinitions[2].Height = new GridLength(250);

            // Create a themed tab header
            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
            var headerText = new TextBlock { Text = header, FontSize = 12 };
            headerText.SetResourceReference(TextBlock.ForegroundProperty, "App.Text.Primary");
            headerPanel.Children.Add(headerText);

            var tabItem = new TabItem
            {
                Header = headerPanel,
                Content = content,
                Tag = tabId
            };
            tabItem.SetResourceReference(TabItem.BackgroundProperty, "App.Background.Surface");
            tabItem.SetResourceReference(TabItem.ForegroundProperty, "App.Text.Primary");

            BottomTabControl.Items.Add(tabItem);
            BottomTabControl.SelectedItem = tabItem;
        }

        private void CloseBottomPanelButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (TabItem tab in BottomTabControl.Items)
            {
                if (tab.Tag is Guid id)
                {
                    if (_logStreams.TryGetValue(id, out var cts))
                    {
                        cts.Cancel();
                        _logStreams.Remove(id);
                    }
                    if (_terminalProcesses.TryGetValue(id, out var proc))
                    {
                        try { if (!proc.HasExited) proc.Kill(); } catch { }
                        _terminalProcesses.Remove(id);
                    }
                }
            }

            BottomTabControl.Items.Clear();
            BottomPanel.Visibility = Visibility.Collapsed;
            BottomSplitter.Visibility = Visibility.Collapsed;

            var grid = (Grid)BottomPanel.Parent;
            grid.RowDefinitions[2].Height = new GridLength(0);
        }

        #endregion

        private TextBox CreateThemedTextBox()
        {
            var textBox = new TextBox
            {
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8)
            };
            textBox.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "App.Background.Surface");
            textBox.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "App.Text.Primary");
            textBox.SetResourceReference(System.Windows.Controls.Control.BorderBrushProperty, "App.Border.Divider");
            textBox.SetResourceReference(TextBox.CaretBrushProperty, "App.Text.Primary");
            return textBox;
        }

        public void Cleanup()
        {
            foreach (var cts in _logStreams.Values)
                cts.Cancel();
            _logStreams.Clear();

            foreach (var process in _portForwards.Values)
            {
                try { if (!process.HasExited) process.Kill(); } catch { }
            }
            _portForwards.Clear();

            foreach (var process in _terminalProcesses.Values)
            {
                try { if (!process.HasExited) process.Kill(); } catch { }
            }
            _terminalProcesses.Clear();
        }
    }
}
