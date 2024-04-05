using H.Firewall;
using H.IpHlpApi;
using H.OpenVpn;
using System.Net;
using System.Reflection;
using System.Runtime.Versioning;
using H.Wfp;

namespace H.Vpn;

[SupportedOSPlatform("windows6.0.6000")]
public class HVpn : IDisposable
{
    #region Properties

    public HFirewall Firewall { get; } = new HFirewall();
    public BaseVPN CurrentVpnInstance { get; set; }
    public ServiceStatus Status { get; } = new ServiceStatus
    {
        Status = VpnStatus.Disconnected,
    };
    public FirewallSettings FirewallSettings { get; private set; } = new();
    public string VpnIp { get; private set; } = string.Empty;

    #endregion

    #region Events

    public event EventHandler<Exception>? ExceptionOccurred;
    public event EventHandler<string>? LogReceived;

    public event EventHandler<ServiceStatus>? StatusChanged;
    public event EventHandler<(long bytesIn, long bytesOut)>? TrafficStatsChanged;

    private void OnExceptionOccurred(Exception value)
    {
        OnLogReceived($"Exception: {value}");

        ExceptionOccurred?.Invoke(this, value);
    }

    private void OnLogReceived(string value)
    {
        LogReceived?.Invoke(this, value);
    }

    private void OnStatusChanged()
    {
        StatusChanged?.Invoke(this, Status);
    }

    private void OnTrafficStatsChanged((long bytesIn, long bytesOut) value)
    {
        TrafficStatsChanged?.Invoke(this, value);
    }

    #endregion

    #region Methods

    public void ChangeFirewallSettings(FirewallSettings settings, string vpnIp)
    {
        settings = settings ?? throw new ArgumentNullException(nameof(settings));

        if (!string.IsNullOrWhiteSpace(VpnIp))
        {
            try
            {
                NetworkMethods.RemoveSplitTunnelRoutes(IPAddress.Parse(VpnIp));
            }
            catch (Exception exception)
            {
                OnLogReceived($"{exception}");
            }
        }

        if (Firewall.IsEnabled)
        {
            if (FirewallSettings.Equals(settings) &&
                VpnIp == vpnIp)
            {
                return;
            }

            Dispose();
        }

        if (!settings.EnableKillSwitch &&
            settings.SplitTunnelingMode == SplitTunnelingMode.Off)
        {
            return;
        }

        if (settings.SplitTunnelingMode != SplitTunnelingMode.Off)
        {
            try
            {
                //StartServiceIfNotRunning("STDriver");
            }
            catch (Exception)
            {
                settings.SplitTunnelingMode = SplitTunnelingMode.Off;
            }
        }

        StartFirewall(settings, vpnIp);

        if (!string.IsNullOrWhiteSpace(VpnIp) &&
            VpnIp != vpnIp)
        {
            try
            {
                NetworkMethods.RemoveSplitTunnelRoutes(IPAddress.Parse(VpnIp));
            }
            catch (Exception exception)
            {
                OnLogReceived($"{exception}");
            }
        }

        if (!string.IsNullOrWhiteSpace(vpnIp))
        {
            try
            {
                NetworkMethods.AddSplitTunnelRoutes(IPAddress.Parse(vpnIp));
            }
            catch (Exception exception)
            {
                OnLogReceived($"{exception}");
            }
        }

        FirewallSettings = settings;
        VpnIp = vpnIp;
    }

    public static string GetServiceProcessPath()
    {
        var path = Assembly.GetEntryAssembly()?.Location ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("This method only works when running exe files.");
        }

        return path;
    }

    public static string GetServiceProcessDirectory()
    {
        return Path.GetDirectoryName(GetServiceProcessPath()) ?? string.Empty;
    }

    public void StartFirewall(FirewallSettings settings, string vpnIp)
    {
        Firewall.Start();
        Firewall.RunTransaction(handle =>
        {
            var (providerKey, subLayerKey) = handle.RegisterKeys();
            if (settings.EnableKillSwitch)
            {
                // H.Wfp-Service.exe
                handle.AddAppId(ActionType.Permit, providerKey, subLayerKey, GetServiceProcessPath(), 15);

                //Permit app
                byte weight = 0;
                foreach (string path in settings.PermitAppPath)
                {
                    handle.AddAppId(ActionType.Permit, providerKey, subLayerKey, path, weight);
                    weight++;
                }

                if (settings.AllowLan)
                {
                    handle.PermitLan(providerKey, subLayerKey, 12);
                }

                handle.PermitDns(providerKey, subLayerKey, 11, 10, settings.PrimaryDns, settings.SecondaryDns);
                handle.PermitIKEv2(providerKey, subLayerKey, 9);
                // Permit Tap Adapter
                handle.PermitNetworkInterface(providerKey, subLayerKey, 2, NetworkMethods.FindTapAdapterLuid(settings.AdapterTunDescription));
                handle.PermitLocalhost(providerKey, subLayerKey, 1);

                // Block everything not allowed explicitly
                handle.BlockAll(providerKey, subLayerKey, 0);
            }

            switch (settings.SplitTunnelingMode)
            {
                case SplitTunnelingMode.AllowSelectedApps:
                    {
                        //Firewall.EnableSplitTunnelingOnlyForSelectedApps(
                        //    providerKey,
                        //    subLayerKey,
                        //    8,
                        //    IPAddress.Parse(settings.LocalIp),
                        //    IPAddress.Parse(vpnIp),
                        //    settings.SplitTunnelingApps.ToArray());
                        break;
                    }

                case SplitTunnelingMode.DisallowSelectedApps:
                    {
                        //Firewall.EnableSplitTunnelingExcludeSelectedApps(
                        //    providerKey,
                        //    subLayerKey,
                        //    8,
                        //    IPAddress.Parse(settings.LocalIp),
                        //    IPAddress.Parse(vpnIp),
                        //    settings.SplitTunnelingApps.ToArray());
                        break;
                    }
            }
        });
    }

    public void StopFirewall()
    {
        Firewall.Stop();
        FirewallSettings.EnableKillSwitch = false;
        FirewallSettings.AllowLan = false;
    }

    public async Task StartVpnAsync(VPNConnectionInfo connectionInfo,
        CancellationToken cancellationToken = default)
    {
        if (connectionInfo == null)
        {
            return;
        }
        if (CurrentVpnInstance != null)
        {
            CurrentVpnInstance.Dispose();
        }

        CurrentVpnInstance = connectionInfo.Type == LibVpnType.Wireguard ? new WireguardVPN() : new HOpenVpn();
        CurrentVpnInstance.ExceptionOccurred += (_, exception) => OnExceptionOccurred(exception);
        CurrentVpnInstance.StateChanged += async (_, state) =>
        {
            try
            {
                switch (state)
                {
                    case VpnState.Restarting:
                    case VpnState.DisconnectingToReconnect:
                        Status.IsReconnecting = true;
                        break;

                    case VpnState.Connected:
                    case VpnState.Disconnecting:
                        Status.IsReconnecting = false;
                        break;
                }

                var subStatus = $"{state:G}".ToLowerInvariant();
                switch (state)
                {
                    case VpnState.Reconnecting:
                        Status.Status = VpnStatus.Reconnecting;
                        Status.SubStatus = subStatus;

                        OnStatusChanged();
                        break;

                    case VpnState.Preparing:
                    case VpnState.Started:
                    case VpnState.Initialized:
                    case VpnState.Restarting:
                    case VpnState.Connecting:
                        Status.Status = VpnStatus.Connecting;
                        Status.SubStatus = subStatus;

                        OnStatusChanged();
                        break;

                    case VpnState.Connected:

                        if (CurrentVpnInstance is HOpenVpn instance)
                        {
                            await instance.SubscribeByteCountAsync().ConfigureAwait(false);
                        }

                        Status.Status = VpnStatus.Connected;
                        Status.ConnectionStartDate = DateTime.UtcNow;
                        Status.LocalInterfaceAddress = CurrentVpnInstance.LocalInterfaceAddress;
                        Status.RemoteIpdAddress = CurrentVpnInstance.RemoteIpAddress;
                        Status.RemoteIpPort = CurrentVpnInstance.RemoteIpPort;

                        OnStatusChanged();
                        break;

                    case VpnState.Disconnecting:
                    case VpnState.DisconnectingToReconnect:
                    case VpnState.Exiting:
                        Status.Status = VpnStatus.Disconnecting;
                        Status.SubStatus = subStatus;

                        OnStatusChanged();
                        break;

                    case VpnState.Inactive:
                        Status.Status = VpnStatus.Disconnected;

                        OnStatusChanged();
                        break;

                    case VpnState.Failed:
                        Status.Status = VpnStatus.Failed;
                        //Status.LastErrorCode = code;
                        //Status.LastErrorMessage = message ?? "Unexpected error";

                        OnStatusChanged();

                        Status.Status = VpnStatus.Disconnected;

                        OnStatusChanged();
                        break;
                }
            }
            catch (Exception exception)
            {
                OnExceptionOccurred(exception);
            }
        };
        CurrentVpnInstance.InternalStateObtained += (_, state) =>
        {
            OnLogReceived($@"OpenVPN internal state obtained: 
                            Name: {state.Name},
                            Description: {state.Description},
                            LocalIp: {state.LocalIp},
                            RemoteIp: {state.RemoteIp},
                            Time: {state.Time:T}");
        };
        CurrentVpnInstance.BytesInOutCountChanged += (_, bytesInOut) =>
        {
            OnLogReceived($"OpenVPN BytesInOutCount: {bytesInOut.BytesIn}-{bytesInOut.BytesOut}");
            OnTrafficStatsChanged((bytesInOut.BytesIn, bytesInOut.BytesOut));
        };
        CurrentVpnInstance.LogObtained += (_, message) =>
        {
            OnLogReceived($"OpenVPN Log: {message}");
        };

        if (CurrentVpnInstance is HOpenVpn instance)
        {
            instance.ConsoleLineReceived += (_, message) =>
            {
                OnLogReceived($"OpenVPN Console Received: {message}");
            };
            instance.ManagementLineReceived += (_, message) =>
            {
                OnLogReceived($"OpenVPN Management Received: {message}");
            };
            instance.ConsoleLineSent += (_, message) =>
            {
                OnLogReceived($"OpenVPN Console Sent: {message}");
            };
            instance.ManagementLineSent += (_, message) =>
            {
                OnLogReceived($"OpenVPN Management Sent: {message}");
            };
        }

        CurrentVpnInstance.StartAsync(connectionInfo);


        if (CurrentVpnInstance is HOpenVpn openVpnInstance)
        {
            if (!string.IsNullOrEmpty(connectionInfo?.OpenVPNServiceInfo?.UserName) && !string.IsNullOrEmpty(connectionInfo?.OpenVPNServiceInfo?.UserName))
            {
                await openVpnInstance.WaitAuthenticationAsync(cancellationToken).ConfigureAwait(false);
            }

            await openVpnInstance.SubscribeStateAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task StopVpnAsync(CancellationToken cancellationToken = default)
    {
        if (CurrentVpnInstance == null)
        {
            return;
        }

        //isReconnecting = false
        switch (CurrentVpnInstance.VpnState)
        {
            case VpnState.Preparing:
            case VpnState.Started:
            case VpnState.Initialized:
                CurrentVpnInstance.VpnState = VpnState.Exiting;
                break;

            case VpnState.Connecting:
            case VpnState.Restarting:
            case VpnState.Connected:
                CurrentVpnInstance.VpnState = VpnState.Disconnecting;

                if (CurrentVpnInstance is HOpenVpn instance)
                {
                    await instance.SendSignalAsync(Signal.SIGTERM, cancellationToken).ConfigureAwait(false);

                    instance.WaitForExit(TimeSpan.FromSeconds(5));
                }

                CurrentVpnInstance.VpnState = VpnState.Inactive;
                break;

            // Force change event.
            case VpnState.DisconnectingToReconnect:
            case VpnState.Exiting:
            case VpnState.Inactive:
            case VpnState.Failed:
                CurrentVpnInstance.VpnState = CurrentVpnInstance.VpnState;
                break;
        }

        CurrentVpnInstance.Dispose();
    }

    public static Version GetVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version ?? new Version();
    }

    public void Stop()
    {
        Dispose();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (CurrentVpnInstance != null)
            {
                CurrentVpnInstance.Dispose();
            }

            Firewall.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}
