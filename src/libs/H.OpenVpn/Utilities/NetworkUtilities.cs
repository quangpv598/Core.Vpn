using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace H.OpenVpn.Utilities;

internal static class NetworkUtilities
{
    #region Methods

    public static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }


    public static async Task<bool> IsAppOnline(bool isUseKillSwitch)
    {
        try
        {
            if (isUseKillSwitch)
            {
                return IsConnectedToInternet() && await PingWithHttpClient();
            }
            else
            {
                return IsConnectedToInternet() && await PingAsync();
            }
        }
        catch (Exception ex)
        {
            return IsConnectedToInternet() && await PingAsync();
        }
    }

    public static async Task<bool> PingWithHttpClient()
    {
        try
        {
            HttpClient httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(5);
            var res = await httpClient.GetAsync("https://checkip.amazonaws.com/");
            res.EnsureSuccessStatusCode();
            return res.IsSuccessStatusCode;
        }
        catch(Exception ex)
        {
            return false;
        }
    }

    public static async Task<bool> PingAsync()
    {
        try
        {
            Ping pingSender = new Ping();
            PingReply reply = await pingSender.SendPingAsync("8.8.8.8");

            return reply.Status == IPStatus.Success;

        }
        catch
        {
            return false;
        }
    }

    [DllImport("wininet.dll")]
    private extern static bool InternetGetConnectedState(out int Description, int ReservedValue);
    public static bool IsConnectedToInternet()
    {
        int Desc;
        bool hasInternet = InternetGetConnectedState(out Desc, 0);

        if (!hasInternet)
        {
            return false;
        }
        return true;
    }

    #endregion
}
