using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using H.Pipes;
using H.VpnService;

namespace H.VpnServiceTestApp;

internal class Program
{
    static async Task Main(string[] args)
    {
        HVpnService hVpnService = new HVpnService();

        hVpnService.LogReceived += HVpnService_LogReceived;
        hVpnService.ExceptionOccurred += HVpnService_ExceptionOccurred;

        await hVpnService.StartAsync();
        await Task.Delay(Timeout.InfiniteTimeSpan);

        //await using var server = new SingleConnectionPipeServer<string>("Quang");
        //server.ClientConnected += async (o, args) =>
        //{
        //    Console.WriteLine($"Client {args.Connection.PipeName} is now connected!");

        //    await args.Connection.WriteAsync("Welcome!");
        //};
        //server.ClientDisconnected += (o, args) =>
        //{
        //    Console.WriteLine($"Client {args.Connection.PipeName} disconnected");
        //};
        //server.MessageReceived += (sender, args) =>
        //{
        //    Console.WriteLine($"Client {args.Connection.PipeName} says: {args.Message}");
        //};
        //server.ExceptionOccurred += (o, args) => OnExceptionOccurred(args.Exception);

        //await server.StartAsync();

        //await Task.Delay(Timeout.InfiniteTimeSpan);

        Console.ReadLine(); Console.ReadLine();
    }

    private static void HVpnService_ExceptionOccurred(object? sender, Exception e)
    {
        Console.WriteLine(e.Message);
    }

    private static void OnExceptionOccurred(Exception exception)
    {
        Console.WriteLine("Welcome!");
    }

    private static void HVpnService_LogReceived(object? sender, string e)
    {
        Console.WriteLine(e);
    }
}
