using Newtonsoft.Json;

namespace RdpManager.Models
{
    public class Server
    {
        [JsonProperty("server_id")]
        public int ServerId { get; set; }

        [JsonProperty("site")]
        public string Site { get; set; }

        [JsonProperty("serverName")]
        public string ServerName { get; set; }

        [JsonProperty("domain")]
        public string Domain { get; set; }

        [JsonProperty("abbr")]
        public string Abbr { get; set; }

        [JsonProperty("ipAddress")]
        public string IpAddress { get; set; }

        [JsonProperty("application")]
        public string Application { get; set; }

        [JsonProperty("ram")]
        public string Ram { get; set; }

        [JsonProperty("cpus")]
        public string Cpus { get; set; }

        [JsonProperty("os")]
        public string Os { get; set; }

        [JsonProperty("osversion")]
        public string OsVersion { get; set; }

        [JsonProperty("notes")]
        public string Notes { get; set; }

        public string DisplayName => $"{ServerName}.{Domain}";
    }
}
