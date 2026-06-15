using System.Collections.Generic;

namespace RdpManager.Models
{
    public class KubernetesPod
    {
        public string Name { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Ready { get; set; } = string.Empty;
        public int Restarts { get; set; }
        public string Age { get; set; } = string.Empty;
        public string Node { get; set; } = string.Empty;
        public string IP { get; set; } = string.Empty;
        public List<string> Containers { get; set; } = new();
        public KubernetesCluster? Cluster { get; set; }

        public string DisplayName => $"{Name} ({Namespace})";

        public string StatusColor => Status switch
        {
            "Running" => "#4CAF50",
            "Pending" => "#FFA500",
            "Succeeded" => "#2196F3",
            "Failed" => "#F44336",
            "CrashLoopBackOff" => "#F44336",
            "ImagePullBackOff" => "#FF9800",
            "ErrImagePull" => "#FF9800",
            "Terminating" => "#9E9E9E",
            _ => "#9E9E9E"
        };
    }
}
