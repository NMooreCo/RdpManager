using System.Collections.Generic;
using Newtonsoft.Json;

namespace RdpManager.Models
{
    public class ServerResponse
    {
        [JsonProperty("servers")]
        public List<Server> Servers { get; set; }
    }
}
