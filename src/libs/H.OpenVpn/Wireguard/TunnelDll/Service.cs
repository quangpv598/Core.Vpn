/* SPDX-License-Identifier: MIT
 *
 * Copyright (C) 2019-2022 WireGuard LLC. All Rights Reserved.
 */

using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;

namespace H.OpenVpn.Wireguard.Tunnel
{
    public class Service
    {
        public static Driver.Adapter GetAdapter(string adapterName)
        {
            return new Driver.Adapter(adapterName);
        }

        public static void Add(VPNConnectionInfo connectionInfo, string configFile, bool ephemeral)
        {
            var shortName = connectionInfo.WireguardServiceInfo.ServiceName;
            var longName = connectionInfo.WireguardServiceInfo.ServiceDescription;
            var exeName = connectionInfo.WireguardServiceInfo.BinaryServicePath;

            string dnsParams = string.Empty;
            if (connectionInfo.WireguardServiceInfo.DnsServers != null)
            {
                foreach(string server in connectionInfo.WireguardServiceInfo.DnsServers)
                {
                    dnsParams += server + " ";
                }

                dnsParams = dnsParams.Trim();
            }

            var pathAndArgs = string.Format("\"{0}\" /config \"{1}\" /dns {2}", exeName, configFile, dnsParams);
            var scm = Win32.OpenSCManager(null, null, Win32.ScmAccessRights.AllAccess);
            if (scm == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                var service = Win32.OpenService(scm, shortName, Win32.ServiceAccessRights.AllAccess);
                if (service != IntPtr.Zero)
                {
                    Win32.CloseServiceHandle(service);
                    Remove(shortName, true);
                }
                service = Win32.CreateService(scm, shortName, longName, Win32.ServiceAccessRights.AllAccess, Win32.ServiceType.Win32OwnProcess, Win32.ServiceStartType.Demand, Win32.ServiceError.Normal, pathAndArgs, null, IntPtr.Zero, "Nsi\0TcpIp\0", null, null);
                if (service == IntPtr.Zero)
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                try
                {
                    var sidType = Win32.ServiceSidType.Unrestricted;
                    if (!Win32.ChangeServiceConfig2(service, Win32.ServiceConfigType.SidInfo, ref sidType))
                        throw new Win32Exception(Marshal.GetLastWin32Error());

                    var description = new Win32.ServiceDescription { lpDescription = connectionInfo.WireguardServiceInfo.ServiceDescription };
                    if (!Win32.ChangeServiceConfig2(service, Win32.ServiceConfigType.Description, ref description))
                        throw new Win32Exception(Marshal.GetLastWin32Error());

                    if (!Win32.StartService(service, 0, null))
                        throw new Win32Exception(Marshal.GetLastWin32Error());

                    if (ephemeral && !Win32.DeleteService(service))
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                finally
                {
                    Win32.CloseServiceHandle(service);
                }
            }
            finally
            {
                Win32.CloseServiceHandle(scm);
            }
        }

        public static void Remove(string serviceName, bool waitForStop)
        {
            var shortName = serviceName;

            var scm = Win32.OpenSCManager(null, null, Win32.ScmAccessRights.AllAccess);
            if (scm == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                var service = Win32.OpenService(scm, shortName, Win32.ServiceAccessRights.AllAccess);
                if (service == IntPtr.Zero)
                    return;
                try
                {
                    var serviceStatus = new Win32.ServiceStatus();
                    Win32.ControlService(service, Win32.ServiceControl.Stop, serviceStatus);

                    for (int i = 0; waitForStop && i < 180 && Win32.QueryServiceStatus(service, serviceStatus) && serviceStatus.dwCurrentState != Win32.ServiceState.Stopped; ++i)
                        Thread.Sleep(1000);

                    if (!Win32.DeleteService(service) && Marshal.GetLastWin32Error() != 0x00000430)
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                finally
                {
                    Win32.CloseServiceHandle(service);
                }
            }
            finally
            {
                Win32.CloseServiceHandle(scm);
            }
        }
    }
}
