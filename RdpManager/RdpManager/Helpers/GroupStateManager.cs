using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using RdpManager.Data.Repositories;

namespace RdpManager.Helpers
{
    /// <summary>
    /// Manages group expansion state persistence
    /// </summary>
    public class GroupStateManager
    {
        private static GroupStateManager? _instance;
        private readonly PreferencesRepository _preferencesRepo;
        private Dictionary<string, bool> _expansionState;

        public static GroupStateManager Instance
        {
            get
            {
                _instance ??= new GroupStateManager();
                return _instance;
            }
        }

        private GroupStateManager()
        {
            _preferencesRepo = new PreferencesRepository();
            LoadState();
        }

        /// <summary>
        /// Load expansion state from database
        /// </summary>
        private void LoadState()
        {
            try
            {
                var json = _preferencesRepo.GetString("GroupExpansionState", "{}");
                _expansionState = JsonConvert.DeserializeObject<Dictionary<string, bool>>(json) ?? new Dictionary<string, bool>();
            }
            catch
            {
                _expansionState = new Dictionary<string, bool>();
            }
        }

        /// <summary>
        /// Get expansion state for a group path
        /// </summary>
        public bool GetExpansionState(string groupPath, bool defaultExpanded = true)
        {
            if (string.IsNullOrEmpty(groupPath))
                return defaultExpanded;

            var expanded = _expansionState.TryGetValue(groupPath, out var state) ? state : defaultExpanded;
            System.Diagnostics.Debug.WriteLine($"GetExpansionState({groupPath}) = {expanded} (default: {defaultExpanded})");
            return expanded;
        }

        /// <summary>
        /// Set expansion state for a group path
        /// </summary>
        public void SetExpansionState(string groupPath, bool isExpanded)
        {
            if (string.IsNullOrEmpty(groupPath))
                return;

            System.Diagnostics.Debug.WriteLine($"SetExpansionState({groupPath}) = {isExpanded}");
            _expansionState[groupPath] = isExpanded;
            SaveState();
        }

        /// <summary>
        /// Save expansion state to database
        /// </summary>
        private void SaveState()
        {
            try
            {
                var json = JsonConvert.SerializeObject(_expansionState);
                _preferencesRepo.Set("GroupExpansionState", json, "json");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving group expansion state: {ex.Message}");
            }
        }
    }
}
