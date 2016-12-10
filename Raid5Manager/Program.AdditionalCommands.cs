using System;
using System.Collections.Generic;
using System.Text;
using DiskAccessLibrary;
using DiskAccessLibrary.LogicalDiskManager;
using Utilities;

namespace Raid5Manager
{
    public partial class Program
    {
        public static void LDMCommand(string[] args)
        {
            if (args.Length >= 2)
            {
                switch (args[1].ToLower())
                {
                    case "stop":
                        OperatingSystemHelper.StopLogicalDiskManagerServices();
                        break;
                    case "start":
                        OperatingSystemHelper.StartLogicalDiskManagerServices(false);
                        break;
                    case "restart":
                        OperatingSystemHelper.RestartLogicalDiskManagerServices();
                        break;
                    default:
                        Console.WriteLine("Invalid argument.");
                        break;
                }
            }
            else
            {
                Console.WriteLine("LDM STOP");
                Console.WriteLine("LDM START");
                Console.WriteLine("LDM RESTART");
            }
        }

        public static void LockCommand(string[] args)
        {
            if (args.Length > 1)
            {
                switch (args[1].ToLower())
                {
                    case "disk":
                        {
                            if (m_selectedDisk != null)
                            {
                                if (m_selectedDisk is PhysicalDisk)
                                {
                                    bool success = ((PhysicalDisk)m_selectedDisk).ExclusiveLock();
                                    if (success)
                                    {
                                        Console.WriteLine("Disk has been locked.");
                                    }
                                    else
                                    {
                                        Console.WriteLine("Failed to lock the disk.");
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine("No disk has been selected.");
                            }
                            break;
                        }
                    case "volume":
                        {
                            Guid? windowsVolumeGuid = WindowsVolumeHelper.GetWindowsVolumeGuid(m_selectedVolume);
                            if (windowsVolumeGuid.HasValue)
                            {
                                bool success = WindowsVolumeManager.ExclusiveLock(windowsVolumeGuid.Value);
                                if (success)
                                {
                                    Console.WriteLine("Volume has been locked");
                                }
                                else
                                {
                                    Console.WriteLine("Failed to lock the volume");
                                }
                            }
                            break;
                        }
                    default:
                        {
                            Console.WriteLine("Invalid argument.");
                            break;
                        }
                }
            }
            else
            {
                Console.WriteLine("Invalid argument.");
            }
        }

        public static void UnlockCommand(string[] args)
        {
            if (args.Length > 1)
            {
                switch (args[1].ToLower())
                {
                    case "disk":
                        {
                            if (m_selectedDisk != null)
                            {
                                if (m_selectedDisk is PhysicalDisk)
                                {
                                    bool success = ((PhysicalDisk)m_selectedDisk).ReleaseLock();
                                    if (success)
                                    {
                                        Console.WriteLine("Disk has been unlocked.");
                                    }
                                    else
                                    {
                                        Console.WriteLine("Failed to unlock the disk.");
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine("No disk has been selected.");
                            }
                            break;
                        }
                    case "volume":
                        {
                            Guid? windowsVolumeGuid = WindowsVolumeHelper.GetWindowsVolumeGuid(m_selectedVolume);
                            if (windowsVolumeGuid.HasValue)
                            {
                                bool success = WindowsVolumeManager.ReleaseLock(windowsVolumeGuid.Value);
                                if (success)
                                {
                                    Console.WriteLine("Volume has been unlocked.");
                                }
                                else
                                {
                                    Console.WriteLine("Failed to unlock the volume.");
                                }
                            }
                            break;
                        }
                    default:
                        {
                            Console.WriteLine("Invalid argument.");
                            break;
                        }
                }
            }
            else
            {
                Console.WriteLine("Invalid argument.");
            }
        }
    }
}