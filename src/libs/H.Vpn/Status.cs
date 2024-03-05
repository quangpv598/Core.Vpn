namespace H.Vpn;

public enum VpnStatus
{
    Connecting,
    Connected,
    Disconnecting,
    Disconnected,
    Failed
}

public class ServiceStatus
{
    public VpnStatus Status { get; set; }
    public string? SubStatus { get; set; } = string.Empty;
    public bool IsReconnecting { get; set; }
    public string? LastErrorMessage { get; set; }
    public string? LastErrorCode { get; set; }
    public string? LocalInterfaceAddress { get; set; }
    public string? RemoteIpdAddress { get; set; }
    public string? RemoteIpPort { get; set; }
    public DateTime ConnectionStartDate { get; set; }
    public long BytesIn { get; set; }
    public long BytesOut { get; set; }
    public string? Version { get; set; }
}
