using System;
using System.Collections.Generic;
using System.Text;
using DiskAccessLibrary;
using DiskAccessLibrary.FileSystems;
using DiskAccessLibrary.LogicalDiskManager;
using Utilities;

namespace Raid5Manager
{
    public partial class Program
    {
        public static void DetailCommand(string[] args)
        {
            if (args.Length == 1)
            {
                HelpDetail();
                return;
            }
            else if (args.Length > 2)
            {
                Console.WriteLine("Too many arguments.");
                HelpDetail();
                return;
            }

            switch (args[1].ToLower())
            {
                case "disk":
                    {
                        Console.WriteLine();
                        if (m_selectedDisk != null)
                        {
                            if (m_selectedDisk is PhysicalDisk)
                            {
                                PhysicalDisk disk = (PhysicalDisk)m_selectedDisk;
                                Console.WriteLine(disk.Description);
                            }
                            Console.WriteLine("Size: {0} bytes", m_selectedDisk.Size.ToString("###,###,###,###,##0"));
                            if (m_selectedDisk is PhysicalDisk)
                            {
                                PhysicalDisk disk = (PhysicalDisk)m_selectedDisk;
                                Console.WriteLine("Geometry: Cylinders: {0}, Heads: {1}, Sectors Per Track: {2}", disk.Cylinders, disk.TracksPerCylinder, disk.SectorsPerTrack);
                            }
                            else if (m_selectedDisk is DiskImage)
                            {
                                DiskImage disk = (DiskImage)m_selectedDisk;
                                Console.WriteLine("Disk image path: {0}", disk.Path);
                            }
                            Console.WriteLine();

                            MasterBootRecord mbr = MasterBootRecord.ReadFromDisk(m_selectedDisk);
                            if (mbr != null)
                            {
                                Console.WriteLine("Partitioning scheme: " + (mbr.IsGPTBasedDisk ? "GPT" : "MBR"));
                            }
                            DynamicDisk dynamicDisk = DynamicDisk.ReadFromDisk(m_selectedDisk);
                            Console.WriteLine("Disk type: " + ((dynamicDisk != null) ? "Dynamic Disk" : "Basic Disk"));
                            if (dynamicDisk != null)
                            {
                                Console.WriteLine("Disk group: " + dynamicDisk.PrivateHeader.DiskGroupGuidString);
                                Console.WriteLine();
                                Console.WriteLine("Public region start sector: " + dynamicDisk.PrivateHeader.PublicRegionStartLBA);
                                Console.WriteLine("Public region size (sectors): " + dynamicDisk.PrivateHeader.PublicRegionSizeLBA);
                                Console.WriteLine();
                                Console.WriteLine("Private region start sector: " + dynamicDisk.PrivateHeader.PrivateRegionStartLBA);
                                Console.WriteLine("Private region size (sectors): " + dynamicDisk.PrivateHeader.PrivateRegionSizeLBA);
                            }
                        }
                        else
                        {
                            Console.WriteLine("No disk has been selected.");
                        }
                        break;
                    }
                case "volume":
                case "partition":
                    {
                        Console.WriteLine();
                        if (m_selectedVolume != null)
                        {
                            Console.WriteLine("Volume size: {0} bytes", m_selectedVolume.Size.ToString("###,###,###,###,##0"));
                            if (m_selectedVolume is GPTPartition)
                            {
                                Console.WriteLine("Partition name: {0}", ((GPTPartition)m_selectedVolume).PartitionName);
                            }

                            Guid? windowsVolumeGuid = WindowsVolumeHelper.GetWindowsVolumeGuid(m_selectedVolume);
                            if (windowsVolumeGuid.HasValue)
                            {
                                List<string> mountPoints = WindowsVolumeManager.GetMountPoints(windowsVolumeGuid.Value);
                                foreach (string volumePath in mountPoints)
                                {
                                    Console.WriteLine("Volume path: {0}", volumePath);
                                }
                                bool isMounted = WindowsVolumeManager.IsMounted(windowsVolumeGuid.Value);
                                Console.WriteLine("Mounted: {0}", isMounted);
                            }
                        }
                        else
                        {
                            Console.WriteLine("No volume has been selected.");
                        }
                        break;
                    }
                case "filesystem":
                    {
                        Console.WriteLine();
                        if (m_selectedVolume != null)
                        {
                            FileSystem fileSystem = FileSystemHelper.ReadFileSystem(m_selectedVolume);
                            if (fileSystem != null)
                            {
                                Console.WriteLine("File system: " + fileSystem.Name);
                                Console.Write(fileSystem.ToString()); // FileSystem.ToString() already appended \r\n at the end, no need for WriteLine()
                            }
                        }
                        else
                        {
                            Console.WriteLine("No volume has been selected.");
                        }
                        break;
                    }
                default:
                    Console.WriteLine("Invalid argument.");
                    HelpDetail();
                    break;
            }
        }

        public static void HelpDetail()
        {
            Console.WriteLine();
            Console.WriteLine("    Provides details about a selected object.");
            Console.WriteLine();
            Console.WriteLine("Syntax: DETAIL DISK       - Display selected disk details.");
            Console.WriteLine("        DETAIL VOLUME     - Display selected volume details.");
            Console.WriteLine("        DETAIL FILESYSTEM - Display details of the filesystem on the selected");
            Console.WriteLine("                            volume.");
        }
    }
}
