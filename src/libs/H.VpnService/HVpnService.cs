﻿using System.Runtime.Versioning;
using H.Firewall;
using H.OpenVpn.Wireguard.Tunnel;
using H.Vpn;
using H.VpnService.Models;

namespace H.VpnService
{
    [SupportedOSPlatform("windows6.0.6000")]
    public class HVpnService : IAsyncDisposable
    {
        #region Properties

        private IpcServer IpcServer { get; } = new IpcServer();
        private HVpn Vpn { get; } = new HVpn();

        #endregion

        #region Events

        public event EventHandler<Exception>? ExceptionOccurred;
        public event EventHandler<string>? LogReceived;

        private void OnExceptionOccurred(Exception value)
        {
            OnLogReceived($"Exception: {value}");

            ExceptionOccurred?.Invoke(this, value);
        }

        private void OnLogReceived(string value)
        {
            LogReceived?.Invoke(this, value);
        }

        #endregion

        #region Constructors

        public HVpnService()
        {
            Vpn.LogReceived += (_, message) => OnLogReceived(message);
            Vpn.ExceptionOccurred += (_, exception) => OnExceptionOccurred(exception);
            Vpn.StatusChanged += async (_, args) =>
            {
                try
                {
                    await IpcServer.WriteAsync(new StatusResponse
                    {
                        Status = args,
                    }).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    OnExceptionOccurred(exception);
                }
            };
            Vpn.TrafficStatsChanged += async (_, args) =>
            {
                try
                {
                    await IpcServer.SendTrafficStatsAsync(args.bytesIn, args.bytesOut).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    OnExceptionOccurred(exception);
                }
            };
        }

        #endregion

        #region Methods

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                Service.Remove("SolarVPN Wireguard Service", false);
            }
            catch (Exception exception)
            {

            }

            OnLogReceived("Starting...");

            IpcServer.ExceptionOccurred += (_, exception) => OnExceptionOccurred(exception);
            IpcServer.ClientConnected += (_, args) => OnLogReceived("IPC client connected");
            IpcServer.ClientDisconnected += (_, args) => OnLogReceived("IPC client disconnected");
            IpcServer.MethodCalled += (_, method) => OnLogReceived($"IPC method called: {method.Id} {method.Method}");
            IpcServer.MessageReceived += (_, message) => OnLogReceived($"IPC message received: {message}");
            IpcServer.MessageSent += (_, message) => OnLogReceived($"IPC message sent: {message}");
            IpcServer.ResponseSent += (_, response) => OnLogReceived($"IPC response sent: {response.Id} {response.Response}");
            IpcServer.StartConnectionMethodCalled += async (_, method) =>
            {
                try
                {
                    await Vpn.StartVpnAsync(new OpenVpn.VPNConnectionInfo
                    {
                        Type = method.VpnType,
                        AdapterName = method.AdapterName,
                        ConfigContent = method.ConfigContent,
                        OpenVPNServiceInfo = new OpenVpn.OpenVPNServiceInfo
                        {
                            UserName = method.Username,
                            Password = method.Password,
                            BinaryServicePath = method.OpenVPNBinaryServicePath
                        },
                        WireguardServiceInfo = new OpenVpn.WireguardServiceInfo
                        {
                            ServiceName = method.ShortServiceName,
                            ServiceDescription = method.ServiceDescription,
                            BinaryServicePath = method.BinaryServicePath,
                            DnsServers = method.DnsServers,
                        },
                        IsUseMultiNode = method.IsUseMultiNode,
                        EntryCountryId = method.EntryCountryId,
                        EntryCityId = method.EntryCityId,
                        CountryId = method.CountryId,
                        CityId = method.CityId,
                        IsUseKillSwitch = method.IsUseKillSwitch
                    }).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    OnExceptionOccurred(exception);
                }
            };
            IpcServer.StopConnectionMethodCalled += async (_, method) =>
            {
                try
                {
                    await Vpn.StopVpnAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    OnExceptionOccurred(exception);
                }
            };
            IpcServer.RequestStatusMethodCalled += async (_, method) =>
            {
                try
                {
                    await IpcServer.WriteAsync(new StatusResponse
                    {
                        Id = method.Id,
                        Status = Vpn.Status,
                    }).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    OnExceptionOccurred(exception);
                }
            };
            IpcServer.RequestOptionsMethodCalled += async (_, method) =>
            {
                try
                {
                    await IpcServer.SendOptionsAsync(
                        method.Id,
                        Vpn.FirewallSettings.AllowLan,
                        Vpn.FirewallSettings.EnableKillSwitch,
                        Vpn.Status.Status).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    OnExceptionOccurred(exception);
                }
            };
            IpcServer.RequestVersionMethodCalled += async (_, method) =>
            {
                try
                {
                    await IpcServer.SendVersionAsync(method.Id, HVpn.GetVersion()).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    OnExceptionOccurred(exception);
                }
            };
            IpcServer.ChangeFirewallSettingsMethodCalled += (_, method) =>
            {
                try
                {
                    Vpn.ChangeFirewallSettings(new FirewallSettings
                    {
                        EnableFirewallOnStart = true,
                        AllowLan = method.AllowLan,
                        EnableKillSwitch = method.EnableKillSwitch,
                        LocalIp = method.LocalIp ?? string.Empty,
                        PrimaryDns = method.PrimaryDns ?? string.Empty,
                        SecondaryDns = method.SecondaryDns ?? string.Empty,
                        SplitTunnelingApps = method.SplitTunnelingApps ?? new List<string>(),
                        SplitTunnelingMode = method.SplitTunnelingMode,
                        PermitAppPath = method.PermitAppsPath ?? new List<string>(),
                        AdapterTunDescription = method.AdapterTunDescription ?? string.Empty,
                    }, method.VpnIp ?? string.Empty);
                }
                catch (Exception exception)
                {
                    OnExceptionOccurred(exception);
                }
            };
            IpcServer.DisableFirewallMethodCalled += (_, method) =>
            {
                try
                {
                    Vpn.StopFirewall();
                }
                catch (Exception exception)
                {
                    OnExceptionOccurred(exception);
                }
            };

            await IpcServer.StartAsync(cancellationToken).ConfigureAwait(false);

            OnLogReceived("Started");
        }

        public async ValueTask StopAsync()
        {
            await DisposeAsync().ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            await IpcServer.DisposeAsync().ConfigureAwait(false);
            Vpn.Dispose();
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
