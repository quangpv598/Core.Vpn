using System.Diagnostics;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using H.Firewall;

namespace H.Wireguard;

internal class Program
{
    [DllImport("Wireguard/lib_x64/tunnel.dll", EntryPoint = "WireGuardTunnelService", CallingConvention = CallingConvention.Cdecl)]
    public static extern bool Run_x64([MarshalAs(UnmanagedType.LPWStr)] string configFile);

    [DllImport("Wireguard/lib_x86/tunnel.dll", EntryPoint = "WireGuardTunnelService", CallingConvention = CallingConvention.Cdecl)]
    public static extern bool Run_x86([MarshalAs(UnmanagedType.LPWStr)] string configFile);

    public static bool Run(string configFile)
    {
        return Environment.Is64BitOperatingSystem ? Run_x64(configFile)
           : Run_x86(configFile);
    }

    static HFirewall _firewall = new HFirewall();

    static void Main(string[] args)
    {
        File.WriteAllText("C:\\a.txt", string.Join(',', args));

        if (args.Length >= 3 && args[0] == "/config" && args[2] == "/dns")
        {
            string configPath = args[1];

            try
            {
                var dnsServers = new List<string>();

                string ipv4Pattern = @"^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$";

                foreach (string ip in new List<string>(args).Skip(3))
                {
                    if (Regex.IsMatch(ip, ipv4Pattern))
                    {
                        dnsServers.Add(ip);
                    }
                }

                _firewall.Start();
                _firewall.RunTransaction((handle) =>
                {
                    var (providerKey, subLayerKey) = handle.RegisterKeys();
                    handle.PermitDns(providerKey, subLayerKey, 11, 10, dnsServers.ToArray());
                });

                Run_x64(configPath);
            }
            catch (Exception ex)
            {
                File.WriteAllText("C:\\b.txt", ex.ToString());
            }

        }
    }
}
