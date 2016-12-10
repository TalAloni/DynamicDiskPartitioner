using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DiskAccessLibrary;
using DiskAccessLibrary.LogicalDiskManager;
using Utilities;

namespace Raid5Manager
{
    public partial class Program
    {
        public static void CreateCommand(string[] args)
        {
            if (args.Length < 3)
            {
                HelpCreate();
                return;
            }

            if (args[1].ToLower() == "volume")
            {
                switch (args[2].ToLower())
                {
                    case "simple":
                        {
                            KeyValuePairList<string, string> parameters = ParseParameters(args, 3);
                            CreateSimpleVolume(parameters);
                            break;
                        }
                    case "raid":
                        {
                            KeyValuePairList<string, string> parameters = ParseParameters(args, 3);
                            CreateRaid5Volume(parameters);
                            break;
                        }
                    default:
                        Console.WriteLine("Invalid volume type.");
                        break;
                }
            }
            else
            {
                Console.WriteLine("Invalid argument.");
                HelpCreate();
            }
        }

        public static void CreateSimpleVolume(KeyValuePairList<string, string> parameters)
        {
            if (!VerifyParameters(parameters, "disk", "size", "align"))
            {
                Console.WriteLine();
                Console.WriteLine("Invalid parameter.");
                HelpCreate();
                return;
            }

            if (!parameters.ContainsKey("disk"))
            {
                Console.WriteLine("The DISK parameter was not specified.");
                return;
            }

            int diskIndex = Conversion.ToInt32(parameters.ValueOf("disk"), -1);
            PhysicalDisk disk = null;
            if (diskIndex >= 0)
            {
                try
                {
                    disk = new PhysicalDisk(diskIndex);
                }
                catch
                {
                    Console.WriteLine("Cannot access one of the requested disks.");
                    return;
                }
            }
            else
            {
                Console.WriteLine("Invalid disk specified.");
                return;
            }

            DynamicDisk dynamicDisk = DynamicDisk.ReadFromDisk(disk);
            if (dynamicDisk == null)
            {
                Console.WriteLine("The disk specified is not a dynamic disk.");
                return;
            }

            Guid diskGroupGuid = dynamicDisk.PrivateHeader.DiskGroupGuid;

            long alignInSectors = 0;
            if (parameters.ContainsKey("align"))
            {
                long requestedAlignInKB = Conversion.ToInt64(parameters.ValueOf("align"), 0);
                long alignInBytes = requestedAlignInKB * 1024;
                if (requestedAlignInKB <= 0 || alignInBytes % disk.BytesPerSector > 0)
                {
                    Console.WriteLine("Invalid ALIGN parameter (must be specified in KB and be multipe of bytes per");
                    Console.WriteLine("sector).");
                    return;
                }
                alignInSectors = alignInBytes / disk.BytesPerSector;
            }

            long sizeInBytes;
            if (parameters.ContainsKey("size"))
            {
                long requestedSizeInMB = Conversion.ToInt64(parameters.ValueOf("size"), 0);
                sizeInBytes = requestedSizeInMB * 1024 * 1024;
                if (requestedSizeInMB <= 0)
                {
                    Console.WriteLine("Invalid size (must be specified in MB).");
                    return;
                }
            }
            else // size was not specified
            {
                sizeInBytes = DynamicDiskExtentHelper.GetMaxNewExtentLength(dynamicDisk, alignInSectors);

                if (sizeInBytes < 1024 * 1024)
                {
                    Console.WriteLine("Not enough free space on selected disks.");
                    return;
                }
            }

            DiskExtent extent = DynamicDiskExtentHelper.AllocateNewExtent(dynamicDisk, sizeInBytes, alignInSectors);
            if (extent == null)
            {
                Console.WriteLine("Disk {0} does not contain enough free space.", diskIndex);
                return;
            }

            // Lock disks and volumes
            Console.WriteLine("Locking disks and volumes");
            // We want to lock the volumes as well or otherwise dmio will report the following error:
            // "The system failed to flush data to the transaction log. Corruption may occur."
            LockStatus status = LockHelper.LockAllDynamicDisks(true);
            if (status != LockStatus.Success)
            {
                if (status == LockStatus.CannotLockDisk)
                {
                    Console.WriteLine("Unable to lock all disks!");
                }
                else if (status == LockStatus.CannotLockVolume)
                {
                    Console.WriteLine("Unable to lock all volumes!");
                }
                return;
            }

            if (Environment.OSVersion.Version.Major >= 6)
            {
                if (!DiskOfflineHelper.AreDynamicDisksOnlineAndWriteable())
                {
                    Console.WriteLine("Error: One or more dynamic disks are offline or set to readonly.");
                    LockHelper.UnlockAllDisksAndVolumes();
                    return;
                }

                Console.WriteLine("Taking dynamic disks offline.");
                bool success = DiskOfflineHelper.OfflineAllDynamicDisks();
                if (!success)
                {
                    Console.WriteLine("Failed to take all dynamic disks offline!");
                    LockHelper.UnlockAllDisksAndVolumes();
                    return;
                }
            }

            DiskGroupDatabase database = DiskGroupDatabase.ReadFromPhysicalDisks(diskGroupGuid);
            VolumeManagerDatabaseHelper.CreateSimpleVolume(database, extent);

            Console.WriteLine("Operation completed.");

            if (Environment.OSVersion.Version.Major >= 6)
            {
                Console.WriteLine("Taking dynamic disks online.");
                DiskOfflineHelper.OnlineAllDynamicDisks();
                LockHelper.UnlockAllDisksAndVolumes();
            }
            else
            {
                OperatingSystemHelper.RestartLDMAndUnlockDisksAndVolumes();
            }
        }

        public static void CreateRaid5Volume(KeyValuePairList<string, string> parameters)
        {
            if (!VerifyParameters(parameters, "disk", "degraded", "size", "align"))
            {
                Console.WriteLine();
                Console.WriteLine("Invalid parameter.");
                HelpCreate();
                return;
            }

            if (!parameters.ContainsKey("disk"))
            {
                Console.WriteLine("The DISK parameter was not specified.");
                return;
            }

            List<int> disks = ToInt32List(new List<string>(parameters.ValueOf("disk").Split(',')));
            if (disks == null)
            {
                Console.WriteLine("Invalid DISK parameter.");
                return;
            }

            bool isDegraded = parameters.ContainsKey("degraded");
            if (disks.Count < 2 || disks.Count < 3 && !isDegraded)
            {
                Console.WriteLine("Invalid number of disks.");
                return;
            }

            List<DynamicDisk> dynamicDisks = new List<DynamicDisk>();
            foreach (int physicalDiskIndex in disks)
            {
                PhysicalDisk targetDisk = null;
                if (physicalDiskIndex >= 0)
                {
                    try
                    {
                        targetDisk = new PhysicalDisk(physicalDiskIndex);
                    }
                    catch
                    {
                        Console.WriteLine("Cannot access one of the requested disks.");
                        return;
                    }
                }
                else
                {
                    Console.WriteLine("Invalid disk specified.");
                    return;
                }

                DynamicDisk targetDynamicDisk = DynamicDisk.ReadFromDisk(targetDisk);
                if (targetDynamicDisk == null)
                {
                    Console.WriteLine("The disk specified is not a dynamic disk.");
                    return;
                }
                dynamicDisks.Add(targetDynamicDisk);
            }

            Guid diskGroupGuid = dynamicDisks[0].PrivateHeader.DiskGroupGuid;
            for(int index = 1; index < dynamicDisks.Count; index++)
            {
                if (dynamicDisks[index].PrivateHeader.DiskGroupGuid != diskGroupGuid)
                {
                    Console.WriteLine("The disks must be members of the same disk group.");
                    return;
                }
            }

            long alignInBytes = 0;
            if (parameters.ContainsKey("align"))
            {
                long requestedAlignInKB = Conversion.ToInt64(parameters.ValueOf("align"), 0);
                alignInBytes = requestedAlignInKB * 1024;
                bool isAlignSizeMultipleOfBytesPerSector = true;
                foreach (DynamicDisk disk in dynamicDisks)
                {
                    if (alignInBytes % disk.BytesPerSector > 0)
                    {
                        isAlignSizeMultipleOfBytesPerSector = false;
                    }
                }

                if (requestedAlignInKB <= 0 || !isAlignSizeMultipleOfBytesPerSector)
                {
                    Console.WriteLine("Invalid align size (must be specified in KB and be multipe of bytes per sector).");
                    return;
                }
            }

            long sizeInBytes;
            if (parameters.ContainsKey("size"))
            {
                long requestedSizeInMB = Conversion.ToInt64(parameters.ValueOf("size"), 0);
                sizeInBytes = requestedSizeInMB * 1024 * 1024;
                if (requestedSizeInMB <= 0)
                {
                    Console.WriteLine("Invalid size (must be specified in MB).");
                    return;
                }
            }
            else // size was not specified
            {
                sizeInBytes = Int64.MaxValue;
                foreach (DynamicDisk disk in dynamicDisks)
                {
                    long alignInSectors = alignInBytes / disk.BytesPerSector;
                    long freeSpaceInExtent = DynamicDiskExtentHelper.GetMaxNewExtentLength(disk, alignInSectors);
                    if (freeSpaceInExtent < sizeInBytes)
                    {
                        sizeInBytes = freeSpaceInExtent;
                    }
                }
                // We want sizeInBytes to be a multiple of MB, because it must be a multiple of stripe size
                sizeInBytes = (sizeInBytes / (1024 * 1024)) * (1024 * 1024);

                if (sizeInBytes < 1024 * 1024)
                {
                    Console.WriteLine("Not enough free space on selected disks.");
                    return;
                }
            }

            List<DiskExtent> extents = new List<DiskExtent>();

            foreach(DynamicDisk disk in dynamicDisks)
            {
                long alignInSectors = alignInBytes / disk.BytesPerSector;
                DiskExtent extent = DynamicDiskExtentHelper.AllocateNewExtent(disk, sizeInBytes, alignInSectors);
                if (extent == null)
                {
                    Console.WriteLine("Disk {0} does not contain enough free space.", ((PhysicalDisk)disk.Disk).PhysicalDiskIndex);
                    return;
                }
                extents.Add(extent);
            }

            // Lock disks and volumes
            Console.WriteLine("Locking disks and volumes");
            // We want to lock the volumes as well or otherwise dmio will report the following error:
            // "The system failed to flush data to the transaction log. Corruption may occur."
            LockStatus status = LockHelper.LockAllDynamicDisks(true);
            if (status != LockStatus.Success)
            {
                if (status == LockStatus.CannotLockDisk)
                {
                    Console.WriteLine("Unable to lock all disks!");
                }
                else if (status == LockStatus.CannotLockVolume)
                {
                    Console.WriteLine("Unable to lock all volumes!");
                }
                return;
            }

            if (Environment.OSVersion.Version.Major >= 6)
            {
                if (!DiskOfflineHelper.AreDynamicDisksOnlineAndWriteable())
                {
                    Console.WriteLine("Error: One or more dynamic disks are offline or set to readonly.");
                    LockHelper.UnlockAllDisksAndVolumes();
                    return;
                }

                Console.WriteLine("Taking dynamic disks offline.");
                bool success = DiskOfflineHelper.OfflineAllDynamicDisks();
                if (!success)
                {
                    Console.WriteLine("Failed to take all dynamic disks offline!");
                    LockHelper.UnlockAllDisksAndVolumes();
                    return;
                }
            }

            DiskGroupDatabase database = DiskGroupDatabase.ReadFromPhysicalDisks(diskGroupGuid);
            VolumeManagerDatabaseHelper.CreateRAID5Volume(database, extents, isDegraded);

            Console.WriteLine("Operation completed.");
            if (Environment.OSVersion.Version.Major >= 6)
            {
                Console.WriteLine("Taking dynamic disks online.");
                DiskOfflineHelper.OnlineAllDynamicDisks();
                LockHelper.UnlockAllDisksAndVolumes();
            }
            else
            {
                OperatingSystemHelper.RestartLDMAndUnlockDisksAndVolumes();
            }
        }

        public static void HelpCreate()
        {
            Console.WriteLine();
            Console.WriteLine("    Creates a new volume.");
            Console.WriteLine();
            Console.WriteLine("Syntax: CREATE VOLUME SIMPLE DISK=<N> [SIZE=<N>] [ALIGN=<N>]");
            Console.WriteLine("        CREATE VOLUME RAID DISK=<N>,<N>[,<N>,..] [DEGRADED] [SIZE=<N>]");
            Console.WriteLine("           [ALIGN=<N>]");
            Console.WriteLine();
            Console.WriteLine("    RAID        Software RAID-5 volume.");
            Console.WriteLine();
            Console.WriteLine("    SIZE=<N>    The size of each extent in megabytes (MB).");
            Console.WriteLine();
            Console.WriteLine("    ALIGN=<N>   Each extent start offset will be a multiple of <N>.");
            Console.WriteLine("                Must be specified in kilobytes (KB), and without any suffixes.");
        }

        public static List<int> ToInt32List(List<string> list)
        {
            List<int> result = new List<int>();
            foreach (string entry in list)
            {
                int value;
                try
                {
                    value = Convert.ToInt32(entry);
                }
                catch
                {
                    return null;
                }
                result.Add(value);
            }
            return result;
        }
    }
}
