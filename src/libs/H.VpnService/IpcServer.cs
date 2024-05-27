using System.Runtime.Versioning;
using H.Pipes;
using H.Pipes.AccessControl;
using Newtonsoft.Json;
using H.VpnService.Models;
using System.Diagnostics;
using H.Vpn;

#pragma warning disable CS8604 // Possible null reference argument.

namespace H.VpnService;

[SupportedOSPlatform("windows")]
public class IpcServer : IAsyncDisposable
{
    #region Properties
    public static readonly string PipeName = "62de9f67-34cd-47c5-9f1b-b347089f7ff8";
    private IPipeServer<string> PipeServer { get; } = new SingleConnectionPipeServer<string>(PipeName);

    #endregion

    #region Events

    public event EventHandler<Exception>? ExceptionOccurred;

    public event EventHandler<RpcMethod>? MethodCalled;
    public event EventHandler<string>? ClientConnected;
    public event EventHandler<string>? ClientDisconnected;
    public event EventHandler<string>? MessageReceived;

    public event EventHandler<RpcResponse>? ResponseSent;
    public event EventHandler<string>? MessageSent;

    public event EventHandler<StartConnectionMethod>? StartConnectionMethodCalled;
    public event EventHandler<StopConnectionMethod>? StopConnectionMethodCalled;
    public event EventHandler<RequestStatusMethod>? RequestStatusMethodCalled;
    public event EventHandler<RequestOptionsMethod>? RequestOptionsMethodCalled;
    public event EventHandler<RequestVersionMethod>? RequestVersionMethodCalled;
    public event EventHandler<ChangeFirewallSettingsMethod>? ChangeFirewallSettingsMethodCalled;
    public event EventHandler<DisableFirewallMethod>? DisableFirewallMethodCalled;
    public event EventHandler<RpcMethod>? SignOutMethodCalled;

    private void OnMethodCalled(RpcMethod value)
    {
        MethodCalled?.Invoke(this, value);
    }

    private void OnResponseSent(RpcResponse value)
    {
        ResponseSent?.Invoke(this, value);
    }

    private void OnExceptionOccurred(Exception value)
    {
        ExceptionOccurred?.Invoke(this, value);
    }

    private void OnClientConnected(string value)
    {
        ClientConnected?.Invoke(this, value);
    }

    private void OnClientDisconnected(string value)
    {
        ClientDisconnected?.Invoke(this, value);
    }

    private void OnMessageReceived(string value)
    {
        MessageReceived?.Invoke(this, value);
    }

    private void OnMessageSent(string value)
    {
        MessageSent?.Invoke(this, value);
    }

    private void OnStartConnectionMethodCalled(StartConnectionMethod value)
    {
        StartConnectionMethodCalled?.Invoke(this, value);
    }

    private void OnStopConnectionMethodCalled(StopConnectionMethod value)
    {
        StopConnectionMethodCalled?.Invoke(this, value);
    }

    private void OnRequestStatusMethodCalled(RequestStatusMethod value)
    {
        RequestStatusMethodCalled?.Invoke(this, value);
    }

    private void OnRequestOptionsMethodCalled(RequestOptionsMethod value)
    {
        RequestOptionsMethodCalled?.Invoke(this, value);
    }

    private void OnRequestVersionMethodCalled(RequestVersionMethod value)
    {
        RequestVersionMethodCalled?.Invoke(this, value);
    }

    private void OnChangeFirewallSettingsMethodCalled(ChangeFirewallSettingsMethod value)
    {
        ChangeFirewallSettingsMethodCalled?.Invoke(this, value);
    }

    private void OnDisableFirewallMethodCalled(DisableFirewallMethod value)
    {
        DisableFirewallMethodCalled?.Invoke(this, value);
    }

    private void OnSignOutMethodCalled(RpcMethod value)
    {
        SignOutMethodCalled?.Invoke(this, value);
    }

    #endregion

    #region Methods

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        PipeServer.AllowUsersReadWrite();

        PipeServer.ExceptionOccurred += (_, args) => OnExceptionOccurred(args.Exception);
        PipeServer.ClientConnected += (_, args) => OnClientConnected("Pipe client connected");
        PipeServer.ClientDisconnected += (_, args) => OnClientDisconnected("Pipe client connected");
        PipeServer.MessageReceived += (_, args) =>
        {
            try
            {
                OnMessageReceived(args.Message);

                var json = args.Message;
                var method = JsonConvert.DeserializeObject<RpcMethod>(json);

                OnMethodCalled(method);
                switch (method.Method)
                {
                    case VpnRpcMethods.StartConnection:
                        var startConnection = JsonConvert.DeserializeObject<StartConnectionMethod>(json);
                        OnStartConnectionMethodCalled(startConnection);
                        break;

                    case VpnRpcMethods.StopConnection:
                        var stopConnection = JsonConvert.DeserializeObject<StopConnectionMethod>(json);
                        OnStopConnectionMethodCalled(stopConnection);
                        break;

                    case VpnRpcMethods.RequestStatus:
                        var requestStatus = JsonConvert.DeserializeObject<RequestStatusMethod>(json);
                        OnRequestStatusMethodCalled(requestStatus);
                        break;

                    case VpnRpcMethods.RequestOptions:
                        var requestOptions = JsonConvert.DeserializeObject<RequestOptionsMethod>(json);
                        OnRequestOptionsMethodCalled(requestOptions);
                        break;

                    case VpnRpcMethods.RequestVersion:
                        var requestVersion = JsonConvert.DeserializeObject<RequestVersionMethod>(json);
                        OnRequestVersionMethodCalled(requestVersion);
                        break;

                    case VpnRpcMethods.ChangeFirewallSettings:
                        var changeFirewallSettings = JsonConvert.DeserializeObject<ChangeFirewallSettingsMethod>(json);
                        OnChangeFirewallSettingsMethodCalled(changeFirewallSettings);
                        break;

                    case VpnRpcMethods.DisableFirewall:
                        var disableFirewall = JsonConvert.DeserializeObject<DisableFirewallMethod>(json);
                        OnDisableFirewallMethodCalled(disableFirewall);
                        break;

                    case VpnRpcMethods.SignOut:
                        OnSignOutMethodCalled(method);
                        break;
                }
            }
            catch (Exception exception)
            {
                OnExceptionOccurred(exception);
            }
        };

        await PipeServer.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteAsync(RpcResponse response, CancellationToken cancellationToken = default)
    {
        var json = JsonConvert.SerializeObject(new[] {response});

        await PipeServer.WriteAsync(json, cancellationToken).ConfigureAwait(false);

        OnMessageSent(json);
        OnResponseSent(response);
    }

    public async Task SendLogAsync(string text, CancellationToken cancellationToken = default)
    {
        await WriteAsync(new LogResponse
        {
            Text = text,
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task SendTrafficStatsAsync(long bytesIn, long bytesOut, CancellationToken cancellationToken = default)
    {
        await WriteAsync(new StatsResponse
        {
            BytesIn = bytesIn,
            BytesOut = bytesOut,
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task SendOptionsAsync(
        int id, 
        bool allowLan, 
        bool isKillSwitchEnabled,
        VpnStatus vpnStatus,
        CancellationToken cancellationToken = default)
    {
        await WriteAsync(new OptionsResponse
        {
            Id = id,
            AllowLan = allowLan,
            IsKillSwitchEnabled = isKillSwitchEnabled,
            Status = vpnStatus,
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task SendVersionAsync(
        int id,
        Version version,
        CancellationToken cancellationToken = default)
    {
        await WriteAsync(new VersionResponse
        {
            Id = id,
            Name = "H.VpnService",
            Identifier = "com.H.VpnService.v1.desktop.service",
            Description = $"H.VpnService service for VPN connections (v{version})",
            Version = $"v{version}",
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask StopAsync()
    {
        await DisposeAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        await PipeServer.DisposeAsync().ConfigureAwait(false);
        
        GC.SuppressFinalize(this);
    }

    #endregion
}
