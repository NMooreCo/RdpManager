using System;
using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using RdpManager.Models;
using RdpManager.Services;

namespace RdpManager.Dialogs
{
    public partial class AddEditKubernetesClusterDialog : System.Windows.Controls.UserControl
    {
        private readonly KubernetesCluster? _editingCluster;
        private readonly KubernetesService _kubernetesService = new();

        public AddEditKubernetesClusterDialog(KubernetesCluster? editCluster = null)
        {
            InitializeComponent();
            _editingCluster = editCluster;

            if (_editingCluster != null)
            {
                TitleText.Text = "Edit Kubernetes Cluster";
                DisplayNameTextBox.Text = _editingCluster.DisplayName;
                ProjectIdTextBox.Text = _editingCluster.ProjectId;
                ClusterNameTextBox.Text = _editingCluster.ClusterName;
                RegionTextBox.Text = _editingCluster.Region;
                DefaultNamespaceTextBox.Text = _editingCluster.DefaultNamespace;
                ProxyAddressTextBox.Text = _editingCluster.ProxyAddress ?? "";
                ClearProxyBeforeAuthCheckBox.IsChecked = _editingCluster.ClearProxyBeforeAuth;
            }
            else
            {
                DefaultNamespaceTextBox.Text = "default";
            }
        }

        private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            var cluster = BuildClusterFromForm();
            if (cluster == null) return;

            TestConnectionButton.IsEnabled = false;
            TestConnectionButton.Content = "TESTING...";
            TestOutputTextBox.Visibility = Visibility.Visible;
            TestOutputTextBox.Text = "Authenticating to cluster...";

            try
            {
                var (success, output, error) = await _kubernetesService.AuthenticateClusterAsync(cluster);

                if (success)
                {
                    TestOutputTextBox.Text = $"SUCCESS\n{output}";
                    if (!string.IsNullOrEmpty(error))
                        TestOutputTextBox.Text += $"\n{error}";

                    // Try to get namespaces to further verify
                    var (namespaces, nsError) = await _kubernetesService.GetNamespacesAsync(cluster);
                    if (namespaces.Count > 0)
                    {
                        TestOutputTextBox.Text += $"\n\nFound {namespaces.Count} namespaces: {string.Join(", ", namespaces)}";
                    }
                    else if (!string.IsNullOrEmpty(nsError))
                    {
                        TestOutputTextBox.Text += $"\n\nWarning - could not list namespaces: {nsError}";
                    }
                }
                else
                {
                    TestOutputTextBox.Text = $"FAILED\n{error}";
                    if (!string.IsNullOrEmpty(output))
                        TestOutputTextBox.Text += $"\n{output}";
                }
            }
            catch (Exception ex)
            {
                TestOutputTextBox.Text = $"ERROR: {ex.Message}";
            }
            finally
            {
                TestConnectionButton.IsEnabled = true;
                TestConnectionButton.Content = "TEST CONNECTION";
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var cluster = BuildClusterFromForm();
            if (cluster == null) return;

            // Preserve original ID and creation date if editing
            if (_editingCluster != null)
            {
                cluster.ClusterId = _editingCluster.ClusterId;
                cluster.CreatedAt = _editingCluster.CreatedAt;
                cluster.SortOrder = _editingCluster.SortOrder;
            }

            DialogHost.CloseDialogCommand.Execute(cluster, this);
        }

        private KubernetesCluster? BuildClusterFromForm()
        {
            if (string.IsNullOrWhiteSpace(DisplayNameTextBox.Text))
            {
                DisplayNameTextBox.Focus();
                return null;
            }
            if (string.IsNullOrWhiteSpace(ProjectIdTextBox.Text))
            {
                ProjectIdTextBox.Focus();
                return null;
            }
            if (string.IsNullOrWhiteSpace(ClusterNameTextBox.Text))
            {
                ClusterNameTextBox.Focus();
                return null;
            }
            if (string.IsNullOrWhiteSpace(RegionTextBox.Text))
            {
                RegionTextBox.Focus();
                return null;
            }

            return new KubernetesCluster
            {
                DisplayName = DisplayNameTextBox.Text.Trim(),
                ProjectId = ProjectIdTextBox.Text.Trim(),
                ClusterName = ClusterNameTextBox.Text.Trim(),
                Region = RegionTextBox.Text.Trim(),
                DefaultNamespace = string.IsNullOrWhiteSpace(DefaultNamespaceTextBox.Text) ? "default" : DefaultNamespaceTextBox.Text.Trim(),
                ProxyAddress = string.IsNullOrWhiteSpace(ProxyAddressTextBox.Text) ? null : ProxyAddressTextBox.Text.Trim(),
                ClearProxyBeforeAuth = ClearProxyBeforeAuthCheckBox.IsChecked == true
            };
        }
    }
}
