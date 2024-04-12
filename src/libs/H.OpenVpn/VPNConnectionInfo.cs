using System;
using System.Collections.Generic;
using System.Text;

namespace H.OpenVpn;

public enum LibVpnType
{
    OpenVPN,
    Wireguard
}

public class WireguardServiceInfo
{
    public string ServiceName { get; set; }
    public string ServiceDescription { get; set; }
    public string BinaryServicePath { get; set; }
    public List<string> DnsServers { get; set; } = new List<string>();
}
public class OpenVPNServiceInfo
{
    public string UserName { get; set; }
    public string Password { get; set; }
    public string Protocol { get; set; }
    public string BinaryServicePath { get; set; }
}

public class VPNConnectionInfo
{
    public LibVpnType Type {  get; set; }
    public string ConfigContent { get; set; }
    public string AdapterName { get; set; }
    public WireguardServiceInfo WireguardServiceInfo { get; set; }
    public OpenVPNServiceInfo OpenVPNServiceInfo { get; set; }
    public bool IsUseMultiNode { get; set; }
    public int EntryCountryId { get; set; }
    public int EntryCityId { get; set; }
    public int CountryId { get; set; }
    public int CityId { get; set; }
    public string LocalCountryCode { get; set; }

}
