using System.Diagnostics;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using H.Firewall;

namespace H.Wireguard;

internal class Program
{
    //[DllImport("Wireguard/lib_x64/tunnel.dll", EntryPoint = "WireGuardTunnelService", CallingConvention = CallingConvention.Cdecl)]
    //public static extern bool Run_x64([MarshalAs(UnmanagedType.LPWStr)] string configFile);

    //[DllImport("Wireguard/lib_x86/tunnel.dll", EntryPoint = "WireGuardTunnelService", CallingConvention = CallingConvention.Cdecl)]
    //public static extern bool Run_x86([MarshalAs(UnmanagedType.LPWStr)] string configFile);

    //public static bool Run(string configFile)
    //{
    //    return Environment.Is64BitOperatingSystem ? Run_x64(configFile)
    //       : Run_x86(configFile);
    //}

    [DllImport("tunnel.dll", EntryPoint = "WireGuardTunnelService", CallingConvention = CallingConvention.Cdecl)]
    public static extern bool Run([MarshalAs(UnmanagedType.LPWStr)] string configFile);


    static HFirewall _firewall = new HFirewall();

    static void Main(string[] args)
    {
        if (args.Length >= 2 && args[0] == "/config")
        {
            string configPath = args[1];
            var dnsServers = new List<string>();

            string dnsPattern = @"DNS\s*=\s*([\d.]+)";
            MatchCollection matches = Regex.Matches(File.ReadAllText(configPath), dnsPattern);

            foreach (Match match in matches)
            {
                dnsServers.Add(match.Groups[1].Value);
            }

            _firewall.Start();
            _firewall.RunTransaction((handle) =>
            {
                var (providerKey, subLayerKey) = handle.RegisterKeys();
                handle.PermitDns(providerKey, subLayerKey, 11, 10, dnsServers.ToArray());
            });

            Run(configPath);

        }
    }
}
