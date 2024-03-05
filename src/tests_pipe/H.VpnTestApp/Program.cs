using H.Pipes;
using H.VpnService.Models;

namespace H.VpnTestApp;

internal class Program
{
    static async Task Main(string[] args)
    {
        await using var client = new SingleConnectionPipeClient<string>("Quang");
        client.MessageReceived += (o, args) => Console.WriteLine("MessageReceived: " + args.Message);
        client.Disconnected += (o, args) => Console.WriteLine("Disconnected from server");
        client.Connected += (o, args) => Console.WriteLine("Connected to server");
        client.ExceptionOccurred += (o, args) => OnExceptionOccurred(args.Exception);

        await client.ConnectAsync();

        var m = new StopConnectionMethod
        {
            Method = VpnRpcMethods.StopConnection
        };
        await client.WriteAsync(m.ToString());

        //var config = File.ReadAllText("C:\\Users\\quang\\Downloads\\vpngate_public-vpn-253.opengw.net_tcp_443.ovpn");

        //var message = new StartConnectionMethod
        //{
        //    Id = 1,
        //    Method = VpnRpcMethods.StartConnection,
        //    OVpn = config,
        //    Password = "",
        //    Username = "",
        //    Proto = ""
        //};

        //await client.WriteAsync(message.ToString());

        await Task.Delay(Timeout.InfiniteTimeSpan);

        Console.ReadLine();
    }

    private static void OnExceptionOccurred(Exception exception)
    {
        
    }
}
