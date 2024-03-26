namespace H.OpenVpn;

public enum VpnState
{
    Inactive,
    Preparing,
    Started,
    Initialized,
    Connecting,
    Reconnecting,
    Restarting,
    Connected,
    Disconnecting,
    DisconnectingToReconnect,
    Exiting,
    Failed,
}