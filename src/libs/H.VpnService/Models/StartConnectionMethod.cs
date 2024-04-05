using H.OpenVpn;
using Newtonsoft.Json;

namespace H.VpnService.Models
{
    /// <summary>
    /// start VPN connection
    /// </summary>
    public class StartConnectionMethod : RpcMethod
    {
        [JsonProperty("vpnType", Required = Required.Always)]
        public LibVpnType VpnType { get; set; }

        [JsonProperty("adapter", Required = Required.Always)]
        public string? AdapterName { get; set; }

        [JsonProperty("config", Required = Required.Always)]
        public string? ConfigContent { get; set; }


        #region For OpenVPN

        [JsonProperty("username")]
        public string? Username { get; set; }

        [JsonProperty("password")]
        public string? Password { get; set; }

        [JsonProperty("protocol")]
        public string? Protocol { get; set; }
        #endregion

        #region For Wireguard

        [JsonProperty("shortServiceName")]
        public string? ShortServiceName { get; set; }

        [JsonProperty("serviceDescription")]
        public string? ServiceDescription { get; set; }

        [JsonProperty("binaryServicePath")]
        public string? BinaryServicePath { get; set; }

        [JsonProperty("dnsServers")]
        public List<string> DnsServers { get; set; } = new List<string>();

        #endregion

    }
}
