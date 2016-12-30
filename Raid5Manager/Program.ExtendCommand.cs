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
                if (m_selectedVolume is StripedVolume)
                {
                    numberOfAdditionalExtentSectors -= numberOfAdditionalExtentSectors % ((StripedVolume)m_selectedVolume).SectorsPerStripe;
                }
                if (m_selectedVolume is Raid5Volume)
                { 
                    numberOfAdditionalExtentSectors -= numberOfAdditionalExtentSectors % ((Raid5Volume)m_selectedVolume).SectorsPerStripe;
                }

                if (m_selectedVolume is DynamicVolume)
                {
                    Guid diskGroupGuid = ((DynamicVolume)m_selectedVolume).DiskGroupGuid;
                    // Lock disks and volumes
                    Console.WriteLine("Locking disks and volumes");
                    List<DynamicDisk> disksToLock = WindowsDynamicDiskHelper.GetPhysicalDynamicDisks(diskGroupGuid);
                    DiskGroupLockResult lockResult = DiskGroupHelper.LockDiskGroup(disksToLock);
                    if (lockResult == DiskGroupLockResult.CannotLockDisk)
                    {
                        Console.WriteLine("Unable to lock all disks!");
                    }
                    else if (lockResult == DiskGroupLockResult.CannotLockVolume)
                    {
                        Console.WriteLine("Unable to lock all volumes!");
                    }
                    else if (lockResult == DiskGroupLockResult.OneOrMoreDisksAreOfflineOrReadonly)
                    {
                        Console.WriteLine("Error: One or more dynamic disks are offline or set to readonly.");
                    }
                    else if (lockResult == DiskGroupLockResult.CannotTakeDiskOffline)
                    {
                        Console.WriteLine("Failed to take all dynamic disks offline!");
                    }
                    else if (lockResult == DiskGroupLockResult.Success)
                    {

                        DiskGroupDatabase database = DiskGroupDatabase.ReadFromPhysicalDisks(diskGroupGuid);
                        ExtendHelper.ExtendDynamicVolume((DynamicVolume)m_selectedVolume, numberOfAdditionalExtentSectors, database);
                        Console.WriteLine("Operation completed.");

                        DiskGroupHelper.UnlockDiskGroup(disksToLock);

                        // volume has been modified and must be reselected
                        m_selectedVolume = WindowsVolumeHelper.GetVolumeByGuid(((DynamicVolume)m_selectedVolume).VolumeGuid);
                    }
                }
                else if (m_selectedVolume is Partition)
                {
                    Partition partition = (Partition)m_selectedVolume;
                    if (partition.Disk is PhysicalDisk)
                    {
                        if (Environment.OSVersion.Version.Major >= 6)
                        {
                            Console.WriteLine("Taking the disk offline.");
                            bool success = ((PhysicalDisk)partition.Disk).SetOnlineStatus(false, false);
                            if (!success)
                            {
                                Console.WriteLine("Failed to take the disk offline.");
                                return;
                            }
                        }
                        else
                        {
                            Console.WriteLine("Locking disk");
                            bool success = ((PhysicalDisk)partition.Disk).ExclusiveLock();
                            if (!success)
                            {
                                Console.WriteLine("Failed to lock the disk.");
                                return;
                            }
                        }
                    }

                    ExtendHelper.ExtendPartition((Partition)m_selectedVolume, numberOfAdditionalExtentSectors);
                    Console.WriteLine("Operation completed.");
                    if (partition.Disk is PhysicalDisk)
                    {
                        if (Environment.OSVersion.Version.Major >= 6)
                        {
                            Console.WriteLine("Taking the disk online.");
                            ((PhysicalDisk)partition.Disk).SetOnlineStatus(true, false);
                        }
                        else
                        {
                            ((PhysicalDisk)partition.Disk).ReleaseLock();
                        }
                        ((PhysicalDisk)partition.Disk).UpdateProperties();
                    }
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

                Guid? windowsVolumeGuid = WindowsVolumeHelper.GetWindowsVolumeGuid(m_selectedVolume);
                if (windowsVolumeGuid.HasValue)
                {
                    if (m_selectedVolume is DynamicVolume && !((DynamicVolume)m_selectedVolume).IsOperational)
                    {
                        Console.WriteLine("Error: non-operational volume!");
                        return;
                    }
                    // We must call FSCTL_IS_VOLUME_MOUNTED before calling FSCTL_LOCK_VOLUME (The NTFS file system treats a locked volume as a dismounted volume)
                    bool isMounted = WindowsVolumeManager.IsMounted(windowsVolumeGuid.Value);
                    // Lock volume
                    Console.WriteLine("Locking volume");
                    // Windows XP / 2003: It's acceptable to request a volume handle with just FileAccess.Read when locking a volume.
                    // Windows Vista / 7: We MUST request a volume handle with just FileAccess.Read when locking a volume on a basic disk.
                    // http://stackoverflow.com/questions/8694713/createfile-direct-write-operation-to-raw-disk-access-is-denied-vista-win7
                    // Windows Vista / 7: We MUST request a volume handle with FileAccess.ReadWrite when locking dynamic volumes.
                    FileAccess fileAccess = (m_selectedVolume is DynamicVolume) ? FileAccess.ReadWrite : FileAccess.Read;
                    bool success = WindowsVolumeManager.ExclusiveLock(windowsVolumeGuid.Value, fileAccess);
                    if (!success)
                    {
                        Console.WriteLine("Unable to lock the volume!");
                        return;
                    }
                    
                    if (isMounted)
                    {
                        // Dismounting the volume will make sure the OS have the correct filesystem size. (locking the volume is not enough)
                        success = WindowsVolumeManager.DismountVolume(windowsVolumeGuid.Value);
                        if (!success)
                        {
                            Console.WriteLine("Unable to dismount the volume!");
                            return;
                        }
                    }
                }

                // Windows Vista / 7 enforce various limitations on direct write operations to volumes and disks.
                // I tried performing the steps necessary, but could only get them to work with basic disks,
                // so in the case of dynamic volume we take the disk(s) offline.
                // http://msdn.microsoft.com/en-us/library/ff551353%28v=vs.85%29.aspx
                if (Environment.OSVersion.Version.Major >= 6 && m_selectedVolume is DynamicVolume)
                {
                    Guid diskGroupGuid = ((DynamicVolume)m_selectedVolume).DiskGroupGuid;
                    if (!DiskOfflineHelper.IsDiskGroupOnlineAndWritable(diskGroupGuid))
                    {
                        Console.WriteLine("Error: One or more dynamic disks are offline or set to readonly.");
                        return;
                    }

                    Console.WriteLine("Taking dynamic disks offline.");
                    bool success = DiskOfflineHelper.OfflineDiskGroup(diskGroupGuid);
                    if (!success)
                    {
                        Console.WriteLine("Failed to take all dynamic disks offline!");
                        return;
                    }
                }

                ((IExtendableFileSystem)fileSystem).Extend(numberOfAdditionalSectors);
                Console.WriteLine("Operation completed.");

                if (windowsVolumeGuid.HasValue)
                {
                    WindowsVolumeManager.ReleaseLock(windowsVolumeGuid.Value);
                }

                if (Environment.OSVersion.Version.Major >= 6 && m_selectedVolume is DynamicVolume)
                {
                    Console.WriteLine("Taking dynamic disks online.");
                    DiskOfflineHelper.OnlineDiskGroup(((DynamicVolume)m_selectedVolume).DiskGroupGuid);
                }
            }
        }
    }
}
