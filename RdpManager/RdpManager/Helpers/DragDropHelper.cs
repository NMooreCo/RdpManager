using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RdpManager.Data.Repositories;

namespace RdpManager.Helpers
{
    /// <summary>
    /// Defines where a dragged item will be dropped relative to the target
    /// </summary>
    public enum DropPosition
    {
        Before,  // Insert before the target item
        After,   // Insert after the target item
        Into     // Insert as a child of the target (for groups)
    }

    /// <summary>
    /// Result of a drag-drop operation
    /// </summary>
    public class DragDropResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public bool RequiresTreeRebuild { get; set; }
    }

    /// <summary>
    /// Handles drag-drop logic for TreeView items
    /// </summary>
    public class DragDropHelper
    {
        private readonly ComputerRepository _computerRepo;
        private readonly GroupRepository _groupRepo;

        public DragDropHelper()
        {
            _computerRepo = new ComputerRepository();
            _groupRepo = new GroupRepository();
        }

        /// <summary>
        /// Calculate drop position based on mouse position relative to target item
        /// </summary>
        public DropPosition CalculateDropPosition(System.Windows.Point mousePosition, TreeViewItem targetItem, RdpManager.Models.TreeNode targetNode)
        {
            var itemHeight = targetItem.ActualHeight;
            var relativeY = mousePosition.Y;

            System.Diagnostics.Debug.WriteLine($"  CalculateDropPosition: relativeY={relativeY:F1}, itemHeight={itemHeight:F1}, ratio={relativeY/itemHeight:F2}");

            // If target is a group (not a leaf), check if dropping INTO it
            if (!targetNode.IsLeaf)
            {
                // Top 35% = before
                // Middle 30% = into (smaller zone to avoid accidental nesting)
                // Bottom 35% = after
                if (relativeY < itemHeight * 0.35)
                {
                    System.Diagnostics.Debug.WriteLine($"  → Before (top 35%)");
                    return DropPosition.Before;
                }
                else if (relativeY > itemHeight * 0.65)
                {
                    System.Diagnostics.Debug.WriteLine($"  → After (bottom 35%)");
                    return DropPosition.After;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"  → Into (middle 30%)");
                    return DropPosition.Into;
                }
            }
            else
            {
                // For leaf items (computers), only before/after
                if (relativeY < itemHeight * 0.5)
                {
                    System.Diagnostics.Debug.WriteLine($"  → Before (top half)");
                    return DropPosition.Before;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"  → After (bottom half)");
                    return DropPosition.After;
                }
            }
        }

        /// <summary>
        /// Execute a drag-drop operation
        /// </summary>
        public DragDropResult ExecuteDrop(RdpManager.Models.TreeNode sourceNode, RdpManager.Models.TreeNode targetNode, DropPosition position)
        {
            try
            {
                // Determine operation type
                bool sourceIsComputer = sourceNode.ComputerEntry != null;
                bool targetIsComputer = targetNode.ComputerEntry != null;

                if (sourceIsComputer && targetIsComputer)
                {
                    return MoveComputerRelativeToComputer(sourceNode.ComputerEntry!, targetNode.ComputerEntry!, position);
                }
                else if (sourceIsComputer && !targetIsComputer)
                {
                    return MoveComputerRelativeToGroup(sourceNode.ComputerEntry!, targetNode, position);
                }
                else if (!sourceIsComputer && !targetIsComputer)
                {
                    return MoveGroupRelativeToGroup(sourceNode, targetNode, position);
                }
                else
                {
                    // Moving group relative to computer - move group to same level as computer
                    return MoveGroupRelativeToComputer(sourceNode, targetNode, position);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Drag-drop error: {ex.Message}\n{ex.StackTrace}");
                return new DragDropResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Move a computer relative to another computer (same or different group)
        /// </summary>
        private DragDropResult MoveComputerRelativeToComputer(RdpManager.Models.ComputerEntry source, RdpManager.Models.ComputerEntry target, DropPosition position)
        {
            System.Diagnostics.Debug.WriteLine($"Moving computer '{source.FriendlyName}' {position} computer '{target.FriendlyName}'");

            var targetGroup = target.Group ?? string.Empty;
            bool sameGroup = source.Group == target.Group;

            if (sameGroup)
            {
                // Reordering within same group
                var computers = _computerRepo.GetAll()
                    .Where(c => c.Group == targetGroup)
                    .OrderBy(c => c.SortOrder)
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"  Found {computers.Count} computers in group '{targetGroup}'");

                // Remove source from list by Id (not reference equality)
                computers.RemoveAll(c => c.Id == source.Id);

                // Find target index by Id
                var targetIndex = computers.FindIndex(c => c.Id == target.Id);
                if (targetIndex < 0)
                {
                    System.Diagnostics.Debug.WriteLine($"  ERROR: Target computer (Id={target.Id}) not found in list!");
                    return new DragDropResult { Success = false, ErrorMessage = "Target computer not found" };
                }

                System.Diagnostics.Debug.WriteLine($"  Target index: {targetIndex}, Insert position: {(position == DropPosition.Before ? "Before" : "After")}");

                // Insert at new position
                var insertIndex = position == DropPosition.Before ? targetIndex : targetIndex + 1;
                computers.Insert(insertIndex, source);

                // Update sort orders
                var updates = computers.Select((c, i) => (c.Id, i)).ToList();
                _computerRepo.UpdateSortOrders(updates);

                System.Diagnostics.Debug.WriteLine($"  Updated {updates.Count} computer sort orders");

                return new DragDropResult { Success = true, RequiresTreeRebuild = true };
            }
            else
            {
                // Moving to different group
                var targetComputers = _computerRepo.GetAll()
                    .Where(c => c.Group == targetGroup)
                    .OrderBy(c => c.SortOrder)
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"  Moving to different group. Target group has {targetComputers.Count} computers");

                // Calculate new sort order by finding target by Id
                var targetIndex = targetComputers.FindIndex(c => c.Id == target.Id);
                if (targetIndex < 0)
                {
                    System.Diagnostics.Debug.WriteLine($"  ERROR: Target computer (Id={target.Id}) not found in target group!");
                    return new DragDropResult { Success = false, ErrorMessage = "Target computer not found in target group" };
                }

                var newSortOrder = position == DropPosition.Before ? targetIndex : targetIndex + 1;

                System.Diagnostics.Debug.WriteLine($"  Target index: {targetIndex}, New sort order: {newSortOrder}");

                // Move the computer
                _computerRepo.MoveToGroup(source.Id, targetGroup, newSortOrder);

                // Recalculate sort orders in both groups
                RecalculateSortOrders(source.Group);
                RecalculateSortOrders(targetGroup);

                return new DragDropResult { Success = true, RequiresTreeRebuild = true };
            }
        }

        /// <summary>
        /// Move a computer relative to a group
        /// </summary>
        private DragDropResult MoveComputerRelativeToGroup(RdpManager.Models.ComputerEntry source, RdpManager.Models.TreeNode targetGroupNode, DropPosition position)
        {
            var targetGroupPath = targetGroupNode.GroupPath ?? string.Empty;
            System.Diagnostics.Debug.WriteLine($"Moving computer '{source.FriendlyName}' {position} group '{targetGroupPath}'");

            if (position == DropPosition.Into)
            {
                // Move to end of group
                var groupComputers = _computerRepo.GetAll()
                    .Where(c => c.Group == targetGroupPath)
                    .ToList();

                var maxSortOrder = groupComputers.Any() ? groupComputers.Max(c => c.SortOrder) + 1 : 0;
                _computerRepo.MoveToGroup(source.Id, targetGroupPath, maxSortOrder);

                RecalculateSortOrders(source.Group);
                return new DragDropResult { Success = true, RequiresTreeRebuild = true };
            }
            else
            {
                // Before/After group - move to parent group at that position
                var group = _groupRepo.GetByPath(targetGroupPath);
                if (group == null)
                {
                    return new DragDropResult { Success = false, ErrorMessage = "Target group not found" };
                }

                var parentPath = group.ParentPath ?? string.Empty;

                // Get all items in parent (computers and groups)
                var parentComputers = _computerRepo.GetAll()
                    .Where(c => c.Group == parentPath)
                    .OrderBy(c => c.SortOrder)
                    .ToList();

                // This is complex - for now, just move to end of parent group
                var maxSortOrder = parentComputers.Any() ? parentComputers.Max(c => c.SortOrder) + 1 : 0;
                _computerRepo.MoveToGroup(source.Id, parentPath, maxSortOrder);

                RecalculateSortOrders(source.Group);
                RecalculateSortOrders(parentPath);

                return new DragDropResult { Success = true, RequiresTreeRebuild = true };
            }
        }

        /// <summary>
        /// Move a group relative to another group
        /// </summary>
        private DragDropResult MoveGroupRelativeToGroup(RdpManager.Models.TreeNode sourceGroupNode, RdpManager.Models.TreeNode targetGroupNode, DropPosition position)
        {
            var sourceGroupPath = sourceGroupNode.GroupPath ?? string.Empty;
            var targetGroupPath = targetGroupNode.GroupPath ?? string.Empty;

            System.Diagnostics.Debug.WriteLine($"Moving group '{sourceGroupPath}' {position} group '{targetGroupPath}'");

            // Prevent moving a group into itself or its descendants
            if (targetGroupPath.StartsWith(sourceGroupPath + "/"))
            {
                return new DragDropResult
                {
                    Success = false,
                    ErrorMessage = "Cannot move a group into its own descendant"
                };
            }

            if (position == DropPosition.Into)
            {
                // Nesting: move source group to become child of target group
                var sourceGroup = _groupRepo.GetByPath(sourceGroupPath);
                if (sourceGroup == null)
                {
                    return new DragDropResult { Success = false, ErrorMessage = "Source group not found" };
                }

                // Get max sort order of target's children
                var targetChildren = _groupRepo.GetChildren(targetGroupPath);
                var maxSortOrder = targetChildren.Any() ? targetChildren.Max(g => g.SortOrder) + 1 : 0;

                _groupRepo.MoveGroup(sourceGroupPath, targetGroupPath, maxSortOrder);

                return new DragDropResult { Success = true, RequiresTreeRebuild = true };
            }
            else
            {
                // Before/After: move to become sibling of target group (at target's parent level)
                var targetGroup = _groupRepo.GetByPath(targetGroupPath);
                if (targetGroup == null)
                {
                    return new DragDropResult { Success = false, ErrorMessage = "Target group not found" };
                }

                var sourceGroup = _groupRepo.GetByPath(sourceGroupPath);
                if (sourceGroup == null)
                {
                    return new DragDropResult { Success = false, ErrorMessage = "Source group not found" };
                }

                // Target's parent becomes the new parent for source
                var newParentPath = targetGroup.ParentPath;

                System.Diagnostics.Debug.WriteLine($"  Moving '{sourceGroup.Name}' to become sibling of '{targetGroup.Name}' (parent: '{newParentPath ?? "ROOT"}')");

                // Get all siblings at the target's level
                var siblings = _groupRepo.GetChildren(newParentPath).OrderBy(g => g.SortOrder).ToList();

                System.Diagnostics.Debug.WriteLine($"  Found {siblings.Count} siblings with parent '{newParentPath ?? "ROOT"}'");
                foreach (var sib in siblings)
                {
                    System.Diagnostics.Debug.WriteLine($"    - {sib.Name} (SortOrder: {sib.SortOrder})");
                }

                // Remove source from siblings if it's already at this level
                siblings.RemoveAll(g => g.FullPath == sourceGroupPath);

                // Find target index in the list WITHOUT source
                var targetIndex = siblings.FindIndex(g => g.FullPath == targetGroupPath);
                if (targetIndex < 0)
                {
                    return new DragDropResult { Success = false, ErrorMessage = "Target group not found in parent" };
                }

                // Calculate insertion position
                var insertPosition = position == DropPosition.Before ? targetIndex : targetIndex + 1;

                // If source needs to change parent (un-nesting), use MoveGroup to update paths
                if (sourceGroup.ParentPath != newParentPath)
                {
                    System.Diagnostics.Debug.WriteLine($"  Source parent is different - using MoveGroup to update paths");
                    _groupRepo.MoveGroup(sourceGroupPath, newParentPath ?? string.Empty, insertPosition);

                    // Reload siblings after move
                    siblings = _groupRepo.GetChildren(newParentPath).OrderBy(g => g.SortOrder).ToList();
                }
                else
                {
                    // Same parent, just reordering - insert at new position
                    siblings.Insert(insertPosition, sourceGroup);
                }

                System.Diagnostics.Debug.WriteLine($"  After reorder:");
                for (int i = 0; i < siblings.Count; i++)
                {
                    System.Diagnostics.Debug.WriteLine($"    [{i}] {siblings[i].Name}");
                }

                // Update all siblings' sort orders sequentially
                for (int i = 0; i < siblings.Count; i++)
                {
                    siblings[i].SortOrder = i;
                    _groupRepo.Update(siblings[i]);
                }

                System.Diagnostics.Debug.WriteLine($"  Updated sort orders in database");

                return new DragDropResult { Success = true, RequiresTreeRebuild = true };
            }
        }

        /// <summary>
        /// Move a group relative to a computer (makes group sibling of computer at same level)
        /// </summary>
        private DragDropResult MoveGroupRelativeToComputer(RdpManager.Models.TreeNode sourceGroupNode, RdpManager.Models.TreeNode targetComputerNode, DropPosition position)
        {
            var sourceGroupPath = sourceGroupNode.GroupPath ?? string.Empty;
            var targetComputer = targetComputerNode.ComputerEntry;
            if (targetComputer == null)
            {
                return new DragDropResult { Success = false, ErrorMessage = "Target computer not found" };
            }

            var targetParentPath = targetComputer.Group ?? string.Empty;

            System.Diagnostics.Debug.WriteLine($"Moving group '{sourceGroupPath}' {position} computer '{targetComputer.FriendlyName}'");
            System.Diagnostics.Debug.WriteLine($"  Group will move to same level as computer (parent: '{targetParentPath}')");

            var sourceGroup = _groupRepo.GetByPath(sourceGroupPath);
            if (sourceGroup == null)
            {
                return new DragDropResult { Success = false, ErrorMessage = "Source group not found" };
            }

            // Get all sibling groups at the target level
            var siblings = _groupRepo.GetChildren(string.IsNullOrEmpty(targetParentPath) ? null : targetParentPath)
                .OrderBy(g => g.SortOrder)
                .ToList();

            // Remove source if already at this level
            siblings.RemoveAll(g => g.FullPath == sourceGroupPath);

            // Add source to end of groups (groups are displayed before computers)
            var newSortOrder = siblings.Count;
            siblings.Add(sourceGroup);

            System.Diagnostics.Debug.WriteLine($"  Moving group to position {newSortOrder} among {siblings.Count} sibling groups");

            // If source needs to change parent, use MoveGroup to update paths
            if (sourceGroup.ParentPath != (string.IsNullOrEmpty(targetParentPath) ? null : targetParentPath))
            {
                _groupRepo.MoveGroup(sourceGroupPath, targetParentPath, newSortOrder);

                // Reload siblings after move
                siblings = _groupRepo.GetChildren(string.IsNullOrEmpty(targetParentPath) ? null : targetParentPath)
                    .OrderBy(g => g.SortOrder)
                    .ToList();
            }

            // Update all siblings' sort orders sequentially
            for (int i = 0; i < siblings.Count; i++)
            {
                siblings[i].SortOrder = i;
                _groupRepo.Update(siblings[i]);
            }

            return new DragDropResult { Success = true, RequiresTreeRebuild = true };
        }

        /// <summary>
        /// Recalculate sort orders for all computers in a group to ensure sequential ordering
        /// </summary>
        private void RecalculateSortOrders(string? groupPath)
        {
            var computers = _computerRepo.GetAll()
                .Where(c => c.Group == (groupPath ?? string.Empty))
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.FriendlyName)
                .ToList();

            var updates = computers.Select((c, index) => (c.Id, index)).ToList();
            if (updates.Any())
            {
                _computerRepo.UpdateSortOrders(updates);
            }
        }

        /// <summary>
        /// Recalculate sort orders for all groups with the same parent
        /// </summary>
        private void RecalculateGroupSortOrders(string? parentPath)
        {
            var groups = _groupRepo.GetChildren(parentPath)
                .OrderBy(g => g.SortOrder)
                .ThenBy(g => g.Name)
                .ToList();

            var updates = groups.Select((g, index) => (g.Id, index)).ToList();
            if (updates.Any())
            {
                _groupRepo.UpdateSortOrders(updates);
            }
        }
    }
}
