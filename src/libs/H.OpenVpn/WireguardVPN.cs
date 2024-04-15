using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using H.OpenVpn.Wireguard.Tunnel;
using H.OpenVpn.Wireguard.TunnelDll;

namespace H.OpenVpn;
public class WireguardVPN : BaseVPN
{
    private bool _isRequestDispose = false;
    private bool _isGettingTraffic = false;
    private VPNConnectionInfo _connectionInfo = null;

    #region Methods
    public override async Task StartAsync(VPNConnectionInfo connectionInfo)
    {
        VpnType = LibVpnType.Wireguard;

        _isRequestDispose = false;

        if (connectionInfo?.WireguardServiceInfo == null)
        {
            throw new ArgumentNullException();
        }

        _connectionInfo = connectionInfo ?? throw new ArgumentNullException();

        string serviceBinaryPath = Path.GetDirectoryName(_connectionInfo.WireguardServiceInfo.BinaryServicePath);

        string platform = Environment.Is64BitProcess ? "x64" : "x86";

        string sourceTunnelPathFile = Path.Combine(serviceBinaryPath, $"Wireguard/lib_{platform}/tunnel.dll");
        string sourceWireguardPathFile = Path.Combine(serviceBinaryPath, $"Wireguard/lib_{platform}/wireguard.dll");

        string targetTunnelPathFile = Path.Combine(serviceBinaryPath, "tunnel.dll");
        string targetWireguardPathFile = Path.Combine(serviceBinaryPath, "wireguard.dll");
        if (File.Exists(sourceTunnelPathFile) && !File.Exists(targetTunnelPathFile))
        {
            File.Copy(sourceTunnelPathFile, targetTunnelPathFile);
        }

        if (File.Exists(sourceWireguardPathFile) && !File.Exists(targetWireguardPathFile))
        {
            File.Copy(sourceWireguardPathFile, targetWireguardPathFile);
        }
#if DEBUG
        File.WriteAllText("C:\\f.txt", $"{File.Exists(targetTunnelPathFile)}\n{File.Exists(targetTunnelPathFile)}");

        Console.WriteLine(targetWireguardPathFile);
#endif
        ConfigPath = Path.Combine(Path.GetTempPath(), $"{connectionInfo.AdapterName}.conf");
        File.WriteAllText(ConfigPath, connectionInfo.ConfigContent);

        GetDnsServers(connectionInfo);

        await Task.Run(() => Service.Add(connectionInfo, ConfigPath, true));

        Task.Run(OnWireguardHandler);
    }

    private void GetDnsServers(VPNConnectionInfo connectionInfo)
    {
        string dnsPattern = @"DNS\s*=\s*([\d.]+)";
        MatchCollection matches = Regex.Matches(connectionInfo.ConfigContent, dnsPattern);

        foreach (Match match in matches)
        {
            var dnsServers = connectionInfo.WireguardServiceInfo.DnsServers;
            if (dnsServers == null)
            {
                dnsServers = new List<string>();
            }
            dnsServers.Add(match.Groups[1].Value);
        }
    }

    private async Task OnWireguardHandler()
    {
        const int timesTryToFindAdapter = 10;
        IntPtr handle = IntPtr.Zero;

        Driver.Adapter adapter;

        for (int i = 0; i < timesTryToFindAdapter; i++)
        {
            //handle = NativeMethods.openAdapter(_connectionInfo.AdapterName);
            //if (handle != IntPtr.Zero)
            //{
            //    break;
            //}

            try
            {
                adapter = Service.GetAdapter(_connectionInfo.AdapterName);
                if (adapter != null)
                {
                    break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception:" + ex.Message);
            }

            await Task.Delay(1000);
        }

        //if (handle == IntPtr.Zero)
        //{
        //    VpnState = VpnState.Failed;
        //    return;
        //}

        adapter = Service.GetAdapter(_connectionInfo.AdapterName);
        if (adapter == null)
        {
            VpnState = VpnState.Failed;
            return;
        }

        VpnState = VpnState.Connected;
        while (handle != IntPtr.Zero)
        {
            try
            {
                if (_isRequestDispose)
                {
                    adapter = null;
                    break;
                }

                _isGettingTraffic = true;

                ulong rx = 0, tx = 0;
                var config = adapter.GetConfiguration();
                foreach (var peer in config.Peers)
                {
                    rx += peer.RxBytes;
                    tx += peer.TxBytes;
                }

                OnBytesInOutCountChanged(new InOutBytes((long)rx, (long)tx));


                await Task.Delay(1000);

                handle = NativeMethods.openAdapter(_connectionInfo.AdapterName);
            }
            catch (Exception ex)
            {
                adapter = null;
                VpnState = VpnState.Exiting;
            }
            finally
            {
                _isGettingTraffic = false;
            }

        }
    }

    protected override void Dispose(bool disposing)
    {
        _isRequestDispose = true;

        try
        {
            while (_isGettingTraffic)
            {
                Thread.Sleep(500);
                continue;
            }
        }
        catch (Exception ex)
        {

        }

        try
        {
            if (_connectionInfo != null
                && _connectionInfo.WireguardServiceInfo != null)
            {
                Service.Remove(_connectionInfo.WireguardServiceInfo.ServiceName, true);
            }
        }
        catch (Exception)
        {
            // ignored
        }

        try
        {
            if (ConfigPath != null && File.Exists(ConfigPath))
            {
                File.Delete(ConfigPath);
                ConfigPath = null;
            }
        }
        catch (Exception)
        {
            // ignored
        }
    }
#endregion
}
