using System;
using System.Collections.Generic;
using System.Text;
using DiskAccessLibrary;
using Utilities;

namespace Raid5Manager
{
    public partial class Program
    {
        public static void OfflineCommand(string[] args)
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
                                    if (Environment.OSVersion.Version.Major >= 6)
                                    {
                                        bool isOnline = ((PhysicalDisk)m_selectedDisk).GetOnlineStatus();
                                        if (isOnline)
                                        {
                                            bool success = ((PhysicalDisk)m_selectedDisk).SetOnlineStatus(false, true);
                                            if (success)
                                            {
                                                Console.WriteLine("Disk has been taken offline.");
                                            }
                                            else
                                            {
                                                Console.WriteLine("Failed to take the disk offline.");
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine("Disk is already offline.");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("This command is only supported on Windows Vista and later.");
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine("No disk has been selected.");
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

        public static void OnlineCommand(string[] args)
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
                                    if (Environment.OSVersion.Version.Major >= 6)
                                    {
                                        bool isOnline = ((PhysicalDisk)m_selectedDisk).GetOnlineStatus();
                                        if (!isOnline)
                                        {
                                            bool success = ((PhysicalDisk)m_selectedDisk).SetOnlineStatus(true, true);
                                            if (success)
                                            {
                                                Console.WriteLine("Disk has been taken online.");
                                            }
                                            else
                                            {
                                                Console.WriteLine("Failed to take the disk online.");
                                            }
                                        }
                                        else
                                        {
                                            Console.WriteLine("Disk is already online.");
                                        }
                                    }
                                    else
                                    {
                                        Console.WriteLine("This command is only supported on Windows Vista and later.");
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine("No disk has been selected.");
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

        public static void HelpOffline()
        {
            Console.WriteLine();
            Console.WriteLine("    Takes a disk offline. (Requires Windows Vista and above)");
            Console.WriteLine();
            Console.WriteLine("Syntax: OFFLINE");
        }

        public static void HelpOnline()
        {
            Console.WriteLine();
            Console.WriteLine("    Brings an offline disk online. (Requires Windows Vista and above)");
            Console.WriteLine();
            Console.WriteLine("Syntax: ONLINE");
        }
    }
}
