using Newtonsoft.Json;

namespace H.VpnService.Models
{
    public enum VpnRpcMethods
    {
        Others,
        StartConnection,
        StopConnection,
        RequestStatus,
        RequestOptions,
        RequestVersion,
        ChangeFirewallSettings,
        DisableFirewall,
        SignOut
    }

    /// <summary>
    /// rpc method
    /// </summary>
    public class RpcMethod
    {
        [JsonProperty("id", Required = Required.Always)]
        public int Id { get; set; }

        [JsonProperty("method", Required = Required.Always)]
        public VpnRpcMethods? Method { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
