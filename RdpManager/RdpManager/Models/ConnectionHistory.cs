using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace RdpManager.Models
{
    public class ConnectionHistoryEntry
    {
        public string Name { get; set; }
        public string MachineName { get; set; }
        public string Domain { get; set; }
        public string Group { get; set; }
        public DateTime LastConnected { get; set; }
        public int ConnectionCount { get; set; }
        public bool IsFavorite { get; set; }
        
        [JsonIgnore]
        public string DisplayName => !string.IsNullOrEmpty(Domain) ? $"{Name} ({Domain})" : Name;
        
        [JsonIgnore]
        public string FullAddress => !string.IsNullOrEmpty(Domain) ? $"{MachineName}.{Domain}" : MachineName;
    }
    
    public class ConnectionHistory
    {
        private const int MaxRecentConnections = 10;
        
        public List<ConnectionHistoryEntry> Entries { get; set; } = new List<ConnectionHistoryEntry>();
        
        public void AddConnection(string name, string machineName, string domain = null, string group = null)
        {
            var existing = Entries.FirstOrDefault(e => 
                e.MachineName.Equals(machineName, StringComparison.OrdinalIgnoreCase) &&
                (e.Domain ?? "").Equals(domain ?? "", StringComparison.OrdinalIgnoreCase));
            
            if (existing != null)
            {
                existing.LastConnected = DateTime.Now;
                existing.ConnectionCount++;
                existing.Name = name; // Update name in case it changed
                existing.Group = group; // Update group in case it changed
            }
            else
            {
                Entries.Add(new ConnectionHistoryEntry
                {
                    Name = name,
                    MachineName = machineName,
                    Domain = domain,
                    Group = group,
                    LastConnected = DateTime.Now,
                    ConnectionCount = 1,
                    IsFavorite = false
                });
            }
            
            // Keep only the most recent connections (unless they're favorites)
            var nonFavorites = Entries.Where(e => !e.IsFavorite).OrderByDescending(e => e.LastConnected).ToList();
            if (nonFavorites.Count > MaxRecentConnections)
            {
                foreach (var entry in nonFavorites.Skip(MaxRecentConnections))
                {
                    Entries.Remove(entry);
                }
            }
        }
        
        public void ToggleFavorite(string machineName, string domain = null)
        {
            var entry = Entries.FirstOrDefault(e => 
                e.MachineName.Equals(machineName, StringComparison.OrdinalIgnoreCase) &&
                (e.Domain ?? "").Equals(domain ?? "", StringComparison.OrdinalIgnoreCase));
            
            if (entry != null)
            {
                entry.IsFavorite = !entry.IsFavorite;
            }
        }
        
        public List<ConnectionHistoryEntry> GetRecentConnections(int count = 10)
        {
            return Entries
                .OrderByDescending(e => e.LastConnected)
                .Take(count)
                .ToList();
        }
        
        public List<ConnectionHistoryEntry> GetFavorites()
        {
            return Entries
                .Where(e => e.IsFavorite)
                .OrderBy(e => e.Name)
                .ToList();
        }
    }
}