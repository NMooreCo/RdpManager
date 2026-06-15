using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using RdpManager.Controls;
using RdpManager.Helpers;

namespace RdpManager.Views
{
    public partial class ComputersView : System.Windows.Controls.UserControl
    {
        public event EventHandler<RdpManager.Models.ComputerEntry>? AddRequested;
        public event EventHandler<RdpManager.Models.TreeNode>? EditRequested;
        public event EventHandler<RdpManager.Models.TreeNode>? RemoveRequested;
        public event EventHandler<RdpManager.Models.TreeNode>? ConnectRequested;
        public event EventHandler<RdpManager.Models.ComputerEntry>? DuplicateRequested;
        public event EventHandler<RdpManager.Models.TreeNode>? RenameGroupRequested;
        public event EventHandler<RdpManager.Models.TreeNode>? ConnectAllInGroupRequested;
        public event EventHandler? ImportRequested;
        public event EventHandler? ExportRequested;
        public event EventHandler<RdpManager.Models.TreeNode>? FavoriteToggled;
        public event EventHandler<RdpManager.Models.TreeNode>? ExploreInAppRequested;
        public event EventHandler? GamesActivated; // Easter egg

        // Drag-drop state
        private System.Windows.Point? _dragStartPoint;
        private RdpManager.Models.TreeNode? _draggedNode;
        private InsertionAdorner? _insertionAdorner;

        public ComputersView()
        {
            InitializeComponent();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            AddRequested?.Invoke(this, null!);
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (ComputersTreeView.SelectedItem is RdpManager.Models.TreeNode node)
            {
                EditRequested?.Invoke(this, node);
            }
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            if (ComputersTreeView.SelectedItem is RdpManager.Models.TreeNode node)
            {
                RemoveRequested?.Invoke(this, node);
            }
        }

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (ComputersTreeView.SelectedItem is RdpManager.Models.TreeNode node)
            {
                ConnectRequested?.Invoke(this, node);
            }
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            ImportRequested?.Invoke(this, EventArgs.Empty);
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            ExportRequested?.Invoke(this, EventArgs.Empty);
        }

        private void FavoriteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is RdpManager.Models.TreeNode node)
            {
                FavoriteToggled?.Invoke(this, node);
            }
        }

        private void ComputersTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // Selection changed - could update button states here
        }

        private void ComputersTreeView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ComputersTreeView.SelectedItem is RdpManager.Models.TreeNode node)
            {
                // 🎮 Easter egg: Double-clicking "GAMES" group unlocks secret games tab!
                if (node.ComputerEntry == null &&
                    node.Name.Equals("GAMES", StringComparison.OrdinalIgnoreCase))
                {
                    System.Diagnostics.Debug.WriteLine("🎮 Easter egg triggered! GAMES group double-clicked");
                    GamesActivated?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                    return;
                }

                // Normal computer connection
                if (node.ComputerEntry != null)
                {
                    ConnectRequested?.Invoke(this, node);
                }
            }
        }

        private void Border_ContextMenuOpening(object sender, System.Windows.Controls.ContextMenuEventArgs e)
        {
            // Select the item when right-clicking to make it visually clear which item is targeted
            if (sender is FrameworkElement element && element.DataContext is RdpManager.Models.TreeNode node)
            {
                // Find and select the TreeViewItem for this node
                var treeViewItem = FindTreeViewItem(ComputersTreeView, node);
                if (treeViewItem != null)
                {
                    treeViewItem.IsSelected = true;
                    treeViewItem.Focus();
                }

                // If this is a group, check if it has direct child computers for "Connect All"
                if (!node.IsLeaf && sender is Border border && border.ContextMenu != null)
                {
                    var connectAllItem = FindMenuItemByHeader(border.ContextMenu, "Connect All");
                    if (connectAllItem != null)
                    {
                        // Enable Connect All only if there are direct child computers (not nested groups)
                        var hasDirectComputers = node.Children.Any(child => child.ComputerEntry != null);
                        connectAllItem.IsEnabled = hasDirectComputers;
                    }
                }
            }
        }

        private MenuItem? FindMenuItemByHeader(ContextMenu menu, string header)
        {
            foreach (var item in menu.Items)
            {
                if (item is MenuItem menuItem && menuItem.Header?.ToString() == header)
                    return menuItem;
            }
            return null;
        }

        private TreeViewItem? FindTreeViewItem(ItemsControl container, RdpManager.Models.TreeNode node)
        {
            if (container == null) return null;

            // Check direct children
            foreach (var item in container.Items)
            {
                var treeViewItem = container.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                if (treeViewItem != null)
                {
                    if (treeViewItem.DataContext == node)
                        return treeViewItem;

                    // Search recursively in children
                    var childItem = FindTreeViewItem(treeViewItem, node);
                    if (childItem != null)
                        return childItem;
                }
            }

            return null;
        }

        private void ContextMenu_Connect(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is RdpManager.Models.TreeNode node)
            {
                ConnectRequested?.Invoke(this, node);
            }
        }

        private void ContextMenu_CopyMachineName(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is RdpManager.Models.TreeNode node && node.ComputerEntry != null)
            {
                var machineName = node.ComputerEntry.MachineName;
                if (!string.IsNullOrEmpty(machineName))
                {
                    System.Windows.Clipboard.SetText(machineName);
                }
            }
        }

        private void ContextMenu_OpenFileExplorer(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is RdpManager.Models.TreeNode node && node.ComputerEntry != null)
            {
                var computerEntry = node.ComputerEntry;
                string uncPath;

                // Try with domain first if available
                if (!string.IsNullOrEmpty(computerEntry.Domain))
                {
                    uncPath = $"\\\\{computerEntry.MachineName}.{computerEntry.Domain}";
                }
                else
                {
                    uncPath = $"\\\\{computerEntry.MachineName}";
                }

                try
                {
                    System.Diagnostics.Debug.WriteLine($"[OpenFileExplorer] Opening external explorer to: {uncPath}");
                    System.Diagnostics.Process.Start("explorer.exe", uncPath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[OpenFileExplorer] Failed with domain, trying without: {ex.Message}");

                    // Fallback to simple machine name if domain version fails
                    try
                    {
                        uncPath = $"\\\\{computerEntry.MachineName}";
                        System.Diagnostics.Process.Start("explorer.exe", uncPath);
                    }
                    catch (Exception ex2)
                    {
                        System.Windows.MessageBox.Show(
                            $"Failed to open File Explorer for {computerEntry.MachineName}.\n\nError: {ex2.Message}",
                            "File Explorer Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }
            }
        }

        private void ContextMenu_ExploreInApp(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is RdpManager.Models.TreeNode node && node.ComputerEntry != null)
            {
                ExploreInAppRequested?.Invoke(this, node);
            }
        }

        private void ContextMenu_Edit(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is RdpManager.Models.TreeNode node && node.ComputerEntry != null)
            {
                EditRequested?.Invoke(this, node);
            }
        }

        private void ContextMenu_Duplicate(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is RdpManager.Models.TreeNode node && node.ComputerEntry != null)
            {
                DuplicateRequested?.Invoke(this, node.ComputerEntry);
            }
        }

        private void ContextMenu_Remove(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is RdpManager.Models.TreeNode node && node.ComputerEntry != null)
            {
                RemoveRequested?.Invoke(this, node);
            }
        }

        private void ContextMenu_RenameGroup(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is RdpManager.Models.TreeNode node && !node.IsLeaf)
            {
                RenameGroupRequested?.Invoke(this, node);
            }
        }

        private void ContextMenu_ConnectAll(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.DataContext is RdpManager.Models.TreeNode node && !node.IsLeaf)
            {
                ConnectAllInGroupRequested?.Invoke(this, node);
            }
        }

        #region Drag-Drop Event Handlers

        public void TreeViewItem_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is TreeViewItem item && item.DataContext is RdpManager.Models.TreeNode node)
            {
                _dragStartPoint = e.GetPosition(null);
                _draggedNode = node;
            }
        }

        public void TreeViewItem_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Pressed && _dragStartPoint.HasValue && _draggedNode != null)
            {
                var mousePos = e.GetPosition(null);
                var diff = _dragStartPoint.Value - mousePos;

                // Check if drag threshold exceeded
                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    // Start drag operation
                    var data = new System.Windows.DataObject("TreeNode", _draggedNode);
                    System.Windows.DragDrop.DoDragDrop((DependencyObject)sender, data, System.Windows.DragDropEffects.Move);

                    // Clear drag state
                    _dragStartPoint = null;
                    _draggedNode = null;
                }
            }
        }

        public void TreeViewItem_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            try
            {
                // Remove previous adorner
                RemoveInsertionAdorner();

                if (!e.Data.GetDataPresent("TreeNode"))
                {
                    e.Effects = System.Windows.DragDropEffects.None;
                    return;
                }

                var sourceNode = e.Data.GetData("TreeNode") as RdpManager.Models.TreeNode;
                if (sourceNode == null || sender is not TreeViewItem targetItem || targetItem.DataContext is not RdpManager.Models.TreeNode targetNode)
                {
                    e.Effects = System.Windows.DragDropEffects.None;
                    return;
                }

                // Don't allow dropping on self
                if (sourceNode == targetNode)
                {
                    e.Effects = System.Windows.DragDropEffects.None;
                    return;
                }

                // Calculate drop position
                var mousePos = e.GetPosition(targetItem);
                var dragDropHelper = new DragDropHelper();
                var dropPosition = dragDropHelper.CalculateDropPosition(mousePos, targetItem, targetNode);

                // Show insertion adorner
                var adornerLayer = AdornerLayer.GetAdornerLayer(targetItem);
                if (adornerLayer != null)
                {
                    bool isTop = dropPosition == DropPosition.Before;
                    bool isInto = dropPosition == DropPosition.Into;
                    _insertionAdorner = new InsertionAdorner(targetItem, isTop, isInto);
                    adornerLayer.Add(_insertionAdorner);
                }

                e.Effects = System.Windows.DragDropEffects.Move;
                e.Handled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DragOver error: {ex.Message}");
                e.Effects = System.Windows.DragDropEffects.None;
            }
        }

        public void TreeViewItem_Drop(object sender, System.Windows.DragEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("=== DROP EVENT FIRED ===");

                // Remove adorner
                RemoveInsertionAdorner();

                if (!e.Data.GetDataPresent("TreeNode"))
                {
                    System.Diagnostics.Debug.WriteLine("Drop cancelled: TreeNode data not present");
                    return;
                }

                var sourceNode = e.Data.GetData("TreeNode") as RdpManager.Models.TreeNode;
                if (sourceNode == null || sender is not TreeViewItem targetItem || targetItem.DataContext is not RdpManager.Models.TreeNode targetNode)
                {
                    System.Diagnostics.Debug.WriteLine("Drop cancelled: Invalid source or target");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"Source: {sourceNode.Name}, Target: {targetNode.Name}");

                // Don't allow dropping on self
                if (sourceNode == targetNode)
                {
                    System.Diagnostics.Debug.WriteLine("Drop cancelled: Source == Target");
                    return;
                }

                // Calculate drop position
                var mousePos = e.GetPosition(targetItem);
                var dragDropHelper = new DragDropHelper();
                var dropPosition = dragDropHelper.CalculateDropPosition(mousePos, targetItem, targetNode);

                System.Diagnostics.Debug.WriteLine($"Drop position: {dropPosition}");

                // Handle the drop via ViewModel
                if (DataContext is RdpManager.Views.MainViewModel viewModel)
                {
                    System.Diagnostics.Debug.WriteLine("Calling ViewModel.HandleDragDrop...");
                    viewModel.HandleDragDrop(sourceNode, targetNode, dropPosition);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: DataContext is not MainViewModel!");
                }

                e.Handled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Drop error: {ex.Message}\n{ex.StackTrace}");
                System.Windows.MessageBox.Show(
                    $"Error during drop operation:\n\n{ex.Message}",
                    "Drop Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        public void TreeViewItem_DragLeave(object sender, System.Windows.DragEventArgs e)
        {
            RemoveInsertionAdorner();
        }

        private void RemoveInsertionAdorner()
        {
            if (_insertionAdorner != null)
            {
                var adornerLayer = AdornerLayer.GetAdornerLayer(_insertionAdorner.AdornedElement);
                adornerLayer?.Remove(_insertionAdorner);
                _insertionAdorner = null;
            }
        }

        #endregion
    }
}
