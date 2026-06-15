namespace RdpManager.Models
{
    public class KubernetesResource
    {
        public string PodName { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string CpuUsage { get; set; } = string.Empty;
        public string MemoryUsage { get; set; } = string.Empty;
    }
}
