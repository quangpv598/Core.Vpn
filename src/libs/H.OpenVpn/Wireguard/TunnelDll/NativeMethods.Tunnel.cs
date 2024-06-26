﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;

namespace H.OpenVpn.Wireguard.TunnelDll;
public static class NativeMethods
{
    private const string WIREGUARD_X64_PATH = "Wireguard/lib_x64/wireguard.dll";
    private const string WIREGUARD_X86_PATH = "Wireguard/lib_x86/wireguard.dll";

    private const string TUNNEL_X64_PATH = "Wireguard/lib_x64/tunnel.dll";
    private const string TUNNEL_X86_PATH = "Wireguard/lib_x86/tunnel.dll";

    #region X64
    [DllImport("Wireguard/lib_x64/wireguard.dll", EntryPoint = "WireGuardOpenAdapter", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    private static extern IntPtr openAdapter_x64([MarshalAs(UnmanagedType.LPWStr)] string name);

    [DllImport("Wireguard/lib_x64/wireguard.dll", EntryPoint = "WireGuardCloseAdapter", CallingConvention = CallingConvention.StdCall)]
    private static extern void freeAdapter_x64(IntPtr adapter);

    [DllImport("Wireguard/lib_x64/wireguard.dll", EntryPoint = "WireGuardGetConfiguration", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    private static extern bool getConfiguration_x64(IntPtr adapter, byte[] iface, ref UInt32 bytes);

    [DllImport("Wireguard/lib_x64/tunnel.dll", EntryPoint = "WireGuardGenerateKeypair", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool WireGuardGenerateKeypair_x64(byte[] publicKey, byte[] privateKey);

    [DllImport("Wireguard/lib_x64/tunnel.dll", EntryPoint = "WireGuardTunnelService", CallingConvention = CallingConvention.Cdecl)]
    public static extern bool Run_x64([MarshalAs(UnmanagedType.LPWStr)] string configFile);
    #endregion

    #region X86
    [DllImport("Wireguard/lib_x86/wireguard.dll", EntryPoint = "WireGuardOpenAdapter", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    private static extern IntPtr openAdapter_x86([MarshalAs(UnmanagedType.LPWStr)] string name);

    [DllImport("Wireguard/lib_x86/wireguard.dll", EntryPoint = "WireGuardCloseAdapter", CallingConvention = CallingConvention.StdCall)]
    private static extern void freeAdapter_x86(IntPtr adapter);

    [DllImport("Wireguard/lib_x86/wireguard.dll", EntryPoint = "WireGuardGetConfiguration", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    private static extern bool getConfiguration_x86(IntPtr adapter, byte[] iface, ref UInt32 bytes);

    [DllImport("Wireguard/lib_x86/tunnel.dll", EntryPoint = "WireGuardGenerateKeypair", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool WireGuardGenerateKeypair_x86(byte[] publicKey, byte[] privateKey);

    [DllImport("Wireguard/lib_x86/tunnel.dll", EntryPoint = "WireGuardTunnelService", CallingConvention = CallingConvention.Cdecl)]
    public static extern bool Run_x86([MarshalAs(UnmanagedType.LPWStr)] string configFile);
    #endregion


    public static IntPtr openAdapter(string name)
    {
        return Environment.Is64BitOperatingSystem ? openAdapter_x64(name)
            : openAdapter_x86(name);
    }

    public static void freeAdapter(IntPtr adapter)
    {
        if (Environment.Is64BitOperatingSystem)
        {
            freeAdapter_x64(adapter);
        }
        else
        {
            freeAdapter_x86(adapter);
        }
    }

    public static bool getConfiguration(IntPtr adapter, byte[] iface, ref UInt32 bytes)
    {
        return Environment.Is64BitOperatingSystem ? getConfiguration_x64(adapter, iface, ref bytes)
            : getConfiguration_x86(adapter, iface, ref bytes);
    }

    public static bool WireGuardGenerateKeypair(byte[] publicKey, byte[] privateKey)
    {
        return Environment.Is64BitOperatingSystem ? WireGuardGenerateKeypair_x64(publicKey, privateKey)
            : WireGuardGenerateKeypair_x86(publicKey, privateKey);
    }

    public static bool Run(string configFile)
    {
        return Environment.Is64BitOperatingSystem ? Run_x64(configFile)
           : Run_x86(configFile);
    }


    //private const string WIREGUARD_PATH = "wireguard.dll";

    //private const string TUNNEL_PATH = "tunnel.dll";

    //#region GENERAL
    //[DllImport("wireguard.dll", EntryPoint = "WireGuardOpenAdapter", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    //public static extern IntPtr openAdapter([MarshalAs(UnmanagedType.LPWStr)] string name);

    //[DllImport("wireguard.dll", EntryPoint = "WireGuardCloseAdapter", CallingConvention = CallingConvention.StdCall)]
    //public static extern void freeAdapter(IntPtr adapter);

    //[DllImport("wireguard.dll", EntryPoint = "WireGuardGetConfiguration", CallingConvention = CallingConvention.StdCall, SetLastError = true)]
    //public static extern bool getConfiguration(IntPtr adapter, byte[] iface, ref UInt32 bytes);

    //[DllImport("tunnel.dll", EntryPoint = "WireGuardGenerateKeypair", CallingConvention = CallingConvention.Cdecl)]
    //public static extern bool WireGuardGenerateKeypair(byte[] publicKey, byte[] privateKey);

    //[DllImport("tunnel.dll", EntryPoint = "WireGuardTunnelService", CallingConvention = CallingConvention.Cdecl)]
    //public static extern bool Run([MarshalAs(UnmanagedType.LPWStr)] string configFile);
    //#endregion
}
