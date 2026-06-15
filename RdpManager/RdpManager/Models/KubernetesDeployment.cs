namespace RdpManager.Models
{
    public class KubernetesDeployment
    {
        public string Name { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public int Replicas { get; set; }
        public int ReadyReplicas { get; set; }
        public int AvailableReplicas { get; set; }
        public int UpdatedReplicas { get; set; }
        public string Age { get; set; } = string.Empty;
        public string Strategy { get; set; } = string.Empty;
        public KubernetesCluster? Cluster { get; set; }

        public string ReadyDisplay => $"{ReadyReplicas}/{Replicas}";
        public string DisplayName => $"{Name} ({Namespace})";
    }
}
