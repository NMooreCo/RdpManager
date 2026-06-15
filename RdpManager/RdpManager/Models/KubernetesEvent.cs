using System;

namespace RdpManager.Models
{
    public class KubernetesEvent
    {
        public string Type { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Object { get; set; } = string.Empty;
        public DateTime? Timestamp { get; set; }
        public int Count { get; set; }

        public string TypeColor => Type switch
        {
            "Warning" => "#FF9800",
            "Normal" => "#4CAF50",
            _ => "#9E9E9E"
        };

        public string LastSeenText => Timestamp?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown";
    }
}
