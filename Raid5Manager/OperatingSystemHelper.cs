using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Text;
using DiskAccessLibrary;
using DiskAccessLibrary.LogicalDiskManager;
using Utilities;

namespace Raid5Manager
{
    public class OperatingSystemHelper
    {
        public static void RestartLDMAndUnlockDisksAndVolumes()
        {
            // We stop LDM before unlocking the disk (not sure if it's necessary to do so before unlocking the disks, but better safe than sorry)
            Console.WriteLine("Restarting Logical Disk Manager.");
            StopLogicalDiskManagerServices();

            LockManager.UnlockAllDisksAndVolumes();

            StartLogicalDiskManagerServices(false);
            Console.WriteLine("To re-enable the disk group, you must do one of the following:");
            Console.WriteLine("- Restart your computer (RECOMMENDED)");
            Console.WriteLine("- Go to Disk Management and reactivate the disk group (NOT RECOMMENDED*)");

            Console.WriteLine();
            Console.WriteLine("* When reactivation is performed without restart, the on-disk update log will");
            Console.WriteLine("  be overwritten by old entries. This may cause a problem if the log will be");
            Console.WriteLine("  needed for recovery purposes.");
            // Note: the VMDB header will be left untarnished, only the last KLOG commit entry will be rolled back,
            // I'm not really sure if this can cause problems and to what extent, but better safe than sorry.

            Console.WriteLine();
            Console.WriteLine("WARNING: You must not use the volumes until performing one of these steps.");
            // Note: because the kernel still thinks the volume has its old size / extent offsets
        }

        public static void RestartLogicalDiskManagerServices()
        {
            StopLogicalDiskManagerServices();
            StartLogicalDiskManagerServices(false);
        }

        /// <summary>
        /// When launching Disk Management / Diskpart, Windows will start these services in the following order:
        /// vds
        /// dmserver
        /// dmadmin  (depend on dmserver)
        /// 
        /// When exiting Disk Management / Diskpart, vds and dmadmin will be stopped.
        /// </summary>
        public static void StartLogicalDiskManagerServices(bool startManagementServices)
        {
            if (startManagementServices)
            {
                StartService("vds", 30000);
            }
            StopService("dmserver", 30000);
            if (startManagementServices)
            {
                StartService("dmadmin", 30000); // this will start dmserver if it hasn't already been started
            }
        }

        public static void StopLogicalDiskManagerServices()
        {
            StopService("vds", 30000);
            StopService("dmadmin", 30000);
            StopService("dmserver", 30000); // this will stop dmadmin if it hasn't already been stopped
        }

        public static bool HasService(string serviceName)
        {
            ServiceController[] services = ServiceController.GetServices();
            foreach (ServiceController service in services)
            {
                if (service.ServiceName.Equals(serviceName, StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public static void StopService(string serviceName, int timeoutMilliseconds)
        {
            if (HasService(serviceName))
            {
                ServiceController service = new ServiceController(serviceName);
                if (service.Status == ServiceControllerStatus.Running)
                {
                    try
                    {
                        service.Stop();
                        TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);
                        service.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Failed to stop '{0}'", serviceName);
                    }
                }
            }
            else
            {
                Console.WriteLine("Service '{0}' was not found", serviceName);
            }
        }

        public static void StartService(string serviceName, int timeoutMilliseconds)
        {
            if (HasService(serviceName))
            {
                ServiceController service = new ServiceController(serviceName);
                if (service.Status == ServiceControllerStatus.Stopped)
                {
                    try
                    {
                        service.Start();
                        TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);
                        service.WaitForStatus(ServiceControllerStatus.Running, timeout);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine("Failed to start '{0}'", serviceName);
                    }
                }
            }
            else
            {
                Console.WriteLine("Service '{0}' was not found", serviceName);
            }
        }
    }
}
