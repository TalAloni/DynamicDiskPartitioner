using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utilities;
using DiskAccessLibrary;
using DiskAccessLibrary.FileSystems;
using DiskAccessLibrary.LogicalDiskManager;

namespace Raid5Manager
{
    public partial class Program
    {
        public static void ExtendCommand(string[] args)
        {
            if (m_selectedVolume is DynamicVolume)
            {
                DiskGroupDatabase database = DiskGroupDatabase.ReadFromPhysicalDisks(((DynamicVolume)m_selectedVolume).DiskGroupGuid);
                if (database == null)
                {
                    Console.WriteLine("The selected volume is invalid");
                    return;
                }

                if (!((DynamicVolume)m_selectedVolume).IsHealthy)
                {
                    Console.WriteLine("Volume is not healthy.");
                    return;
                }
            }

            if (m_selectedVolume != null)
            {
                KeyValuePairList<string, string> parameters = ParseParameters(args, 1);
                if (!VerifyParameters(parameters, "querymax", "size", "filesystem", "volume"))
                {
                    Console.WriteLine();
                    Console.WriteLine("Invalid parameter.");
                    HelpExtend();
                    return;
                }

                if (parameters.ContainsKey("filesystem"))
                {
                    ExtendFileSystem(parameters);
                }
                else // extend the volume (container)
                {
                    ExtendVolume(parameters);
                }
            }
            else
            {
                Console.WriteLine("No volume has been selected.");
            }
        }

        public static void HelpExtend()
        {
            Console.WriteLine();
            Console.WriteLine("    EXTEND [VOLUME]   - Extend the selected volume / partition, the filesystem");
            Console.WriteLine("                        will not be extended.");
            Console.WriteLine("    EXTEND FILESYSTEM - Extend the file system on the selected volume.");
            Console.WriteLine();
            Console.WriteLine("Syntax: EXTEND [QUERYMAX] [SIZE=<N>]");
            Console.WriteLine("        EXTEND VOLUME [QUERYMAX] [SIZE=<N>]");
            Console.WriteLine("        EXTEND FILESYSTEM [QUERYMAX] [SIZE=<N>]");
            Console.WriteLine();
            Console.WriteLine("    QUERYMAX    Returns the maximum size that can be added to each column.");
            Console.WriteLine();
            Console.WriteLine("    SIZE=<N>    The size to add to each column, in megabytes (MB).");
            Console.WriteLine();
        }

        public static void ExtendVolume(KeyValuePairList<string, string> parameters)
        {
            long numberOfAvailablelExtentBytes = ExtendHelper.GetMaximumSizeToExtendVolume(m_selectedVolume);
            if (parameters.ContainsKey("querymax"))
            {
                Console.Write("Max extend: {0}", FormattingHelper.GetStandardSizeString(numberOfAvailablelExtentBytes));
                if (m_selectedVolume is Raid5Volume || m_selectedVolume is StripedVolume)
                {
                    Console.Write(" (Per column)");
                }
                Console.WriteLine();
            }
            else
            {
                if (parameters.ContainsKey("size"))
                {
                    long requestedSizeInMB = Conversion.ToInt64(parameters.ValueOf("size"), 0);
                    if (requestedSizeInMB <= 0)
                    {
                        Console.WriteLine("Invalid size (must be specified in MB).");
                        return;
                    }

                    long requestedSizeInBytes = requestedSizeInMB * 1024 * 1024;
                    if (requestedSizeInBytes <= numberOfAvailablelExtentBytes)
                    {
                        numberOfAvailablelExtentBytes = requestedSizeInBytes;
                    }
                    else
                    {
                        Console.WriteLine("Invalid size, the extent does not have enough free space following the volume");
                        return;
                    }
                }
                else if (numberOfAvailablelExtentBytes == 0)
                {
                    Console.WriteLine("There is no space available after the volume.");
                    return;
                }

                long numberOfAdditionalExtentSectors = numberOfAvailablelExtentBytes / m_selectedVolume.BytesPerSector;
                if (m_selectedVolume is DynamicVolume)
                {
                    DynamicVolume dynamicVolume = ((DynamicVolume)m_selectedVolume);
                    List<DynamicDisk> diskGroup = WindowsDynamicDiskHelper.GetPhysicalDynamicDisks(dynamicVolume.DiskGroupGuid);
                    // Lock disks and volumes
                    Console.WriteLine("Locking disks and volumes");
                    DiskGroupLockResult result = ExtendVolumeHelper.ExtendDynamicVolume(diskGroup, dynamicVolume, numberOfAdditionalExtentSectors);
                    if (result == DiskGroupLockResult.CannotLockDisk)
                    {
                        Console.WriteLine("Unable to lock all disks!");
                    }
                    else if (result == DiskGroupLockResult.CannotLockVolume)
                    {
                        Console.WriteLine("Unable to lock all volumes!");
                    }
                    else if (result == DiskGroupLockResult.OneOrMoreDisksAreOfflineOrReadonly)
                    {
                        Console.WriteLine("Error: One or more dynamic disks are offline or set to readonly.");
                    }
                    else if (result == DiskGroupLockResult.CannotTakeDiskOffline)
                    {
                        Console.WriteLine("Failed to take all dynamic disks offline!");
                    }
                    else if (result == DiskGroupLockResult.Success)
                    {
                        Console.WriteLine("Operation completed.");
                        // volume has been modified and must be reselected
                        m_selectedVolume = WindowsVolumeHelper.GetVolumeByGuid(((DynamicVolume)m_selectedVolume).VolumeGuid);
                    }
                }
                else if (m_selectedVolume is Partition)
                {
                    Partition partition = (Partition)m_selectedVolume;
                    DiskGroupLockResult result = ExtendVolumeHelper.ExtendPartition(partition, numberOfAdditionalExtentSectors);
                    if (result == DiskGroupLockResult.CannotLockDisk)
                    {
                        Console.WriteLine("Unable to lock all disks!");
                    }
                    else if (result == DiskGroupLockResult.CannotLockVolume)
                    {
                        Console.WriteLine("Unable to lock all volumes!");
                    }
                    else if (result == DiskGroupLockResult.OneOrMoreDisksAreOfflineOrReadonly)
                    {
                        Console.WriteLine("Error: One or more dynamic disks are offline or set to readonly.");
                    }
                    else if (result == DiskGroupLockResult.CannotTakeDiskOffline)
                    {
                        Console.WriteLine("Failed to take all dynamic disks offline!");
                    }
                    else if (result == DiskGroupLockResult.Success)
                    {
                        Console.WriteLine("Operation completed.");
                        // volume has been modified and must be reselected
                        if (partition.Disk is PhysicalDisk)
                        {
                            Guid? windowsVolumeGuid = WindowsVolumeHelper.GetWindowsVolumeGuid(m_selectedVolume);
                            m_selectedVolume = WindowsVolumeHelper.GetVolumeByGuid(windowsVolumeGuid.Value);
                        }
                        else
                        {
                            m_selectedVolume = BasicDiskHelper.GetPartitionByStartOffset(partition.Disk, partition.FirstSector);
                        }
                    }
                }
            }
        }

        public static void ExtendFileSystem(KeyValuePairList<string, string> parameters)
        {
            if (!VerifyParameters(parameters, "filesystem", "querymax", "size"))
            {
                Console.WriteLine("Invalid parameter.");
                return;
            }

            IFileSystem fileSystem = FileSystemHelper.ReadFileSystem(m_selectedVolume);
            if (!(fileSystem is IExtendableFileSystem))
            {
                Console.WriteLine("Filsystem is not supported for this operation.");
                return;
            }

            long numberOfAvailableBytes = ((IExtendableFileSystem)fileSystem).GetMaximumSizeToExtend();
            if (parameters.ContainsKey("querymax"))
            {
                Console.WriteLine("Max extend: {0}", FormattingHelper.GetStandardSizeString(numberOfAvailableBytes));
            }
            else
            {
                if (parameters.ContainsKey("size"))
                {
                    long requestedSizeInMB = Conversion.ToInt64(parameters.ValueOf("size"), 0);
                    if (requestedSizeInMB <= 0)
                    {
                        Console.WriteLine("Invalid size (must be specified in MB).");
                        return;
                    }

                    long requestedSizeInBytes = requestedSizeInMB * 1024 * 1024;
                    if (requestedSizeInBytes <= numberOfAvailableBytes)
                    {
                        numberOfAvailableBytes = requestedSizeInBytes;
                    }
                    else
                    {
                        Console.WriteLine("Invalid size, the volume does not have free space following the filesystem.");
                        return;
                    }
                }
                else if (numberOfAvailableBytes == 0)
                {
                    Console.WriteLine("There is no space available after the volume.");
                    return;
                }

                long numberOfAdditionalSectors = numberOfAvailableBytes / m_selectedVolume.BytesPerSector;
                List<DynamicDisk> diskGroup = null;
                if (m_selectedVolume is DynamicVolume)
                {
                    DynamicVolume dynamicVolume = (DynamicVolume)m_selectedVolume;
                    diskGroup = WindowsDynamicDiskHelper.GetPhysicalDynamicDisks(dynamicVolume.DiskGroupGuid);
                }
                ExtendFileSystemResult result = ExtendFileSystemHelper.ExtendFileSystem(diskGroup, m_selectedVolume, numberOfAdditionalSectors);
                if (result == ExtendFileSystemResult.UnsupportedFileSystem)
                {
                    Console.WriteLine("Unsupported File System");
                }
                else if (result == ExtendFileSystemResult.NonOperationalVolume)
                {
                    Console.WriteLine("Error: non-operational volume!");
                }
                else if (result == ExtendFileSystemResult.CannotLockDisk)
                {
                    Console.WriteLine("Unable to lock all disks!");
                }
                else if (result == ExtendFileSystemResult.CannotLockVolume)
                {
                    Console.WriteLine("Unable to lock all volumes!");
                }
                else if (result == ExtendFileSystemResult.CannotDismountVolume)
                {
                    Console.WriteLine("Unable to dismount the volume!");
                }
                else if (result == ExtendFileSystemResult.OneOrMoreDisksAreOfflineOrReadonly)
                {
                    Console.WriteLine("Error: One or more dynamic disks are offline or set to readonly.");
                }
                else if (result == ExtendFileSystemResult.CannotTakeDiskOffline)
                {
                    Console.WriteLine("Failed to take all dynamic disks offline!");
                }
                else if (result == ExtendFileSystemResult.Success)
                {
                    Console.WriteLine("Operation completed.");
                }
            }
        }
    }
}
