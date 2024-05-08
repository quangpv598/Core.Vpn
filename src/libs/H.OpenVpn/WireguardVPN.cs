using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Resources;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using H.OpenVpn.Utilities;
using H.OpenVpn.Wireguard;
using H.OpenVpn.Wireguard.Config;
using H.OpenVpn.Wireguard.Tunnel;
using H.OpenVpn.Wireguard.TunnelDll;
using static H.OpenVpn.Wireguard.Native.WireguardBoosterExports;

namespace H.OpenVpn;
public class WireguardVPN : BaseVPN
{
    private bool _isRequestDispose = false;
    private bool _isGettingTraffic = false;
    private VPNConnectionInfo _connectionInfo = null;

    //private readonly BackgroundWorker _tunnelConnectionWorker;
    //private readonly BackgroundWorker _tunnelStateWorker;

    ///**
    // * @brief The manager that handles the Wireguard connections.
    // */
    //private readonly WireSockManager _wiresock;

    public WireguardVPN()
    {
        //_tunnelConnectionWorker = InitializeTunnelConnectionWorker();
        //_tunnelStateWorker = InitTunnelStateWorker();

        //// Create a new WireSockManager instance, attached to the logging control
        //_wiresock = new WireSockManager(OnWireSockLogMessage);
    }


    ///// <summary>
    /////     Initialize a <see cref="T:BackgroundWorker" /> which retrieves tunnel connecting / connecting state and updates it
    /////     in the UI
    ///// </summary>
    ///// <returns>
    /////     <see cref="T:BackgroundWorker" />
    ///// </returns>
    //private BackgroundWorker InitializeTunnelConnectionWorker()
    //{
    //    var worker = new BackgroundWorker
    //    {
    //        WorkerSupportsCancellation = true,
    //        WorkerReportsProgress = true
    //    };

    //    worker.DoWork += (s, e) =>
    //    {
    //        do
    //        {
    //            Thread.Sleep(500);
    //            worker.ReportProgress(0, _wiresock.Connected);
    //        } while (!worker.CancellationPending && !_wiresock.Connected);
    //    };

    //    worker.ProgressChanged += (s, e) =>
    //    {
    //        if ((bool)e.UserState)
    //        {
    //            if (!_tunnelStateWorker.IsBusy)
    //                _tunnelStateWorker.RunWorkerAsync();
    //        }
    //    };

    //    return worker;
    //}

    ///// <summary>
    /////     Initialize a <see cref="T:BackgroundWorker" /> which retrieves the connected tunnel state and updates it in the UI
    ///// </summary>
    ///// <returns>
    /////     <see cref="T:BackgroundWorker" />
    ///// </returns>
    //private BackgroundWorker InitTunnelStateWorker()
    //{
    //    var worker = new BackgroundWorker
    //    {
    //        WorkerSupportsCancellation = true,
    //        WorkerReportsProgress = true
    //    };

    //    worker.DoWork += (s, e) =>
    //    {
    //        while (!worker.CancellationPending)
    //        {
    //            Thread.Sleep(1000);

    //            if (!_wiresock.Connected) continue;

    //            var stats = _wiresock.GetState();
    //            worker.ReportProgress(0, stats);
    //        }
    //    };

    //    worker.ProgressChanged += (s, e) =>
    //    {
    //        if (!(e.UserState is WgbStats stats)) return;

    //        OnBytesInOutCountChanged(new InOutBytes((long)stats.rx_bytes, (long)stats.tx_bytes));

    //    };

    //    return worker;
    //}

    //private void OnWireSockLogMessage(WireSockManager.LogMessage logMessage)
    //{

    //}


    #region Methods
    public override async Task StartAsync(VPNConnectionInfo connectionInfo)
    {
        Debug.WriteLine($"Connect to {connectionInfo.CountryId}");
        _isRequestDispose = false;

        VpnType = LibVpnType.Wireguard;

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

        //try
        //{
        //    if (_wiresock.Connect(ConfigPath))
        //    {
        //        VpnState = VpnState.Connected;
        //        if (!_tunnelConnectionWorker.IsBusy)
        //            _tunnelConnectionWorker.RunWorkerAsync();
        //    }
        //}
        //catch (Exception ex)
        //{
        //    VpnState = VpnState.Failed;
        //}

        GetServerInfoAsync(connectionInfo);

        await Task.Run(() => Service.Add(connectionInfo, ConfigPath, true));

        Task.Run(() => OnWireguardHandler(connectionInfo.IsUseKillSwitch));
    }

    private async Task GetServerInfoAsync(VPNConnectionInfo connectionInfo)
    {
        Debug.WriteLine($"Start track to {connectionInfo.CountryId}");
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

        string patternEndpoint = @"Endpoint\s*=\s*([\d.]+)";
        Match matchEndpoint = Regex.Match(connectionInfo.ConfigContent, patternEndpoint);
        if (matchEndpoint.Success)
        {
            string endpointAddress = matchEndpoint.Groups[1].Value;
            RemoteIpAddress = endpointAddress;
        }
    }

    private async Task OnWireguardHandler(bool isKillSwitch)
    {
        const int timesTryToFindAdapter = 30;
        IntPtr handle = IntPtr.Zero;

        Driver.Adapter adapter;

        for (int i = 0; i < timesTryToFindAdapter; i++)
        {
            handle = NativeMethods.openAdapter(_connectionInfo.AdapterName);
            if (handle != IntPtr.Zero)
            {
                break;
            }

            await Task.Delay(1000);
        }

        if (handle == IntPtr.Zero)
        {
            VpnState = VpnState.Failed;
            return;
        }

        adapter = Service.GetAdapter(_connectionInfo.AdapterName);
        if (adapter == null)
        {
            VpnState = VpnState.Failed;
            return;
        }

        Debug.WriteLine($"Connected: {_connectionInfo.CountryId}");

        VpnState = VpnState.Connected;

        int countIsOnline = 0;
        const int maxCountIsOnline = 5;

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

                var isOnline = await NetworkUtilities.IsAppOnline(isKillSwitch);

                if (_isRequestDispose)
                {
                    adapter = null;
                    break;
                }

                if (isOnline)
                {
                    countIsOnline = 0;
                }
                else
                {
                    countIsOnline++;
                }

                if (countIsOnline >= maxCountIsOnline)
                {
                    countIsOnline = 0;
                    VpnState = VpnState.Reconnecting;
                    Task.Run(() => Dispose(true));
                    break;
                }

                handle = NativeMethods.openAdapter(_connectionInfo.AdapterName);
            }
            catch (Exception ex)
            {
                adapter = null;
                VpnState = VpnState.Exiting;
                break;
            }
            finally
            {
                _isGettingTraffic = false;

                await Task.Delay(1000);

            }

        }

        adapter = null;
    }

    protected override void Dispose(bool disposing)
    {
        _isRequestDispose = true;

        Debug.WriteLine("Dispose");

        try
        {
            while (_isGettingTraffic)
            {
                Task.Delay(100).Wait();
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

        //try
        //{
        //    _tunnelStateWorker.CancelAsync();
        //    _wiresock.Disconnect();
        //}
        //catch (Exception ex)
        //{

        //}

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
