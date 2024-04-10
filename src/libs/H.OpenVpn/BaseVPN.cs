using H.OpenVpn.Utilities;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Channels;

namespace H.OpenVpn;
public abstract class BaseVPN : IDisposable
{
    #region Properties

    public string? ConfigPath { get; set; }

    private VpnState _vpnState = VpnState.Inactive;
    public VpnState VpnState
    {
        get => _vpnState;
        set
        {
            _vpnState = value;
            OnStateChanged(value);
        }
    }

    private long _bytesInCount;
    public long BytesInCount
    {
        get => _bytesInCount;
        set
        {
            _bytesInCount = value;
            OnBytesInCountChanged(value);
        }
    }

    private long _bytesOutCount;
    public long BytesOutCount
    {
        get => _bytesOutCount;
        set
        {
            _bytesOutCount = value;
            OnBytesOutCountChanged(value);
        }
    }

    public string LocalInterfaceAddress { get; set; } = string.Empty;
    public string RemoteIpAddress { get; set; } = string.Empty;
    public string RemoteIpPort { get; set; } = string.Empty;

    public bool IsUseMultiNode { get; set; }
    public int EntryCountryId { get; set; }
    public int EntryCityId { get; set; }
    public int CountryId { get; set; }
    public int CityId { get; set; }
    public LibVpnType VpnType { get; set; }
    #endregion

    #region Events

    public event EventHandler<Exception>? ExceptionOccurred;

    public event EventHandler<VpnState>? StateChanged;
    public event EventHandler<State>? InternalStateObtained;
    public event EventHandler<long>? BytesInCountChanged;
    public event EventHandler<long>? BytesOutCountChanged;
    public event EventHandler<InOutBytes>? BytesInOutCountChanged;
    public event EventHandler<string>? LogObtained;

    protected void OnExceptionOccurred(Exception value)
    {
        ExceptionOccurred?.Invoke(this, value);
    }

    protected void OnStateChanged(VpnState value)
    {
        StateChanged?.Invoke(this, value);
    }

    protected void OnInternalStateObtained(State value)
    {
        InternalStateObtained?.Invoke(this, value);
    }

    protected void OnBytesInCountChanged(long value)
    {
        BytesInCountChanged?.Invoke(this, value);
    }

    protected void OnBytesOutCountChanged(long value)
    {
        BytesOutCountChanged?.Invoke(this, value);
    }

    protected void OnBytesInOutCountChanged(InOutBytes value)
    {
        BytesInOutCountChanged?.Invoke(this, value);
    }

    protected void OnLogObtained(string value)
    {
        LogObtained?.Invoke(this, value);
    }

    #endregion

    #region Methods

    public abstract Task StartAsync(VPNConnectionInfo connectionInfo);

    public void Stop()
    {
        Dispose();
    }

    protected abstract void Dispose(bool disposing);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    #endregion
}
