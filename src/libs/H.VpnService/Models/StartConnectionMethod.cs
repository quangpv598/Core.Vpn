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

        [JsonProperty("openVPNBinaryServicePath")]
        public string? OpenVPNBinaryServicePath { get; set; }

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

        #region Others
        [JsonProperty("isUseKillSwitch")]
        public bool IsUseKillSwitch { get; set; }

        [JsonProperty("isUseMultiNode")]
        public bool IsUseMultiNode { get; set; }

        [JsonProperty("entryCountryId")]
        public int EntryCountryId { get; set; }

        [JsonProperty("entryCityId")]
        public int EntryCityId { get; set; }

        [JsonProperty("countryId")]
        public int CountryId { get; set; }

        [JsonProperty("cityId")]
        public int CityId { get; set; }

        [JsonProperty("localCountryCode")]
        public string LocalCountryCode { get; set; }
        #endregion

    }
}
