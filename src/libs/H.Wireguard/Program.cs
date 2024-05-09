﻿using System.Diagnostics;
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

            try
            {
                var dnsServers = new List<string>();

                //string ipv4Pattern = @"^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$";

                //foreach (string ip in new List<string>(args).Skip(3))
                //{
                //    if (Regex.IsMatch(ip, ipv4Pattern))
                //    {
                //        dnsServers.Add(ip);
                //    }
                //}

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


                File.WriteAllText("C:\\c.txt", string.Join(',', args));

                Run(configPath);
            }
            catch (Exception ex)
            {
                File.WriteAllText("C:\\b.txt", ex.ToString());
            }

        }
    }
}
