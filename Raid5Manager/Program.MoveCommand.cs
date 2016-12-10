using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using DiskAccessLibrary;
using DiskAccessLibrary.LogicalDiskManager;
using Utilities;

namespace Raid5Manager
{
    public partial class Program
    {
        public static void MoveCommand(string[] args)
        {
            if (args.Length == 1)
            {
                HelpMove();
                return;
            }

            if (m_selectedVolume == null)
            {
                Console.WriteLine("No volume has been selected.");
                return;
            }

            if (!(m_selectedVolume is DynamicVolume))
            {
                Console.WriteLine("Only dynamic volumes are currently supported");
                return;
            }

            DynamicVolume dynamicVolume = (DynamicVolume)m_selectedVolume;
            if (!dynamicVolume.IsOperational)
            {
                Console.WriteLine("Some disks are missing or currently locked");
                return;
            }

            bool isBootVolume;
            if (RetainHelper.IsVolumeRetained((DynamicVolume)m_selectedVolume, out isBootVolume))
            {
                Console.WriteLine("WARNING: You're trying to move a retained volume (volume that has a partition");
                Console.WriteLine("         associated with it).");
                Console.WriteLine("         If an operating system is present on this volume, a reconfiguration");
                Console.WriteLine("         might be necessary before you could boot it successfully.");
                Console.WriteLine("         This operation is currently not supported.");
                return;
            }

            if (RAID5ManagerBootRecord.HasValidSignature(m_selectedVolume.ReadSector(0)))
            {
                Console.WriteLine("There is already an operation in progress");
                Console.WriteLine("Use the RESUME command to resume the operation");
                return;
            }

            KeyValuePairList<string, string> parameters = ParseParameters(args, 1);
            if (!VerifyParameters(parameters, "extent", "offset", "disk"))
            {
                Console.WriteLine("Invalid parameter.");
                return;
            }
            DynamicDiskExtent sourceExtent = GetExtent(dynamicVolume, parameters);
            if (sourceExtent == null)
            {
                return;
            }

            if (!parameters.ContainsKey("offset") && !parameters.ContainsKey("disk"))
            {
                Console.WriteLine("Error: You must specify either offset or disk.");
                return;
            }

            if (parameters.ContainsKey("offset") && parameters.ContainsKey("disk"))
            {
                Console.WriteLine("Invalid combination of parameters.");
                return;
            }

            bool isSameDisk = parameters.ContainsKey("offset");
            DiskExtent relocatedExtent;

            if (isSameDisk)
            {
                long offset = ParseStandardSizeString(parameters.ValueOf("offset"));
                DynamicDisk dynamicDisk = DynamicDisk.ReadFromDisk(sourceExtent.Disk);
                if (!DynamicDiskExtentHelper.IsMoveLocationValid(dynamicDisk, sourceExtent, offset))
                {
                    Console.WriteLine("Error: Invalid offset specified.");
                    Console.WriteLine("The following conditions must be met:");
                    Console.WriteLine("1. The destination must reside inside the data portion of the disk.");
                    Console.WriteLine("2. The destination must not be used by any other extents.");
                    Console.WriteLine("3. The offset must be aligned to sector size.");
                    return;
                }
                long targetFirstSector = offset / sourceExtent.Disk.BytesPerSector;
                relocatedExtent = new DiskExtent(sourceExtent.Disk, targetFirstSector, sourceExtent.Size);

                if (targetFirstSector == sourceExtent.FirstSector)
                {
                    Console.WriteLine("Source and destination are the same.");
                    return;
                }
            }
            else
            {
                int sourceDiskIndex = ((PhysicalDisk)sourceExtent.Disk).PhysicalDiskIndex;
                int targetDiskIndex = Conversion.ToInt32(parameters.ValueOf("disk"), -1);
                if (targetDiskIndex == -1)
                {
                    Console.WriteLine("Error: Invalid disk was specified.");
                    return;
                }

                if (targetDiskIndex == sourceDiskIndex)
                {
                    Console.WriteLine("Error: Disk specified is the same as the source disk.");
                    return;
                }

                DynamicDisk dynamicDisk = DynamicDisk.ReadFromDisk(new PhysicalDisk(targetDiskIndex));
                if (dynamicDisk == null)
                {
                    Console.WriteLine("Error: Disk specified is not a dynamic disk.");
                    return;
                }
                relocatedExtent = DynamicDiskExtentHelper.AllocateNewExtent(dynamicDisk, sourceExtent.Size);
                if (relocatedExtent == null)
                {
                    Console.WriteLine("Disk {0} does not contain enough free space.", targetDiskIndex);
                    return;
                }
            }

            // Lock disks and volumes
            Console.WriteLine("Locking disks and volumes");
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

            // Perform the operation
            Console.WriteLine("Starting the operation");
            if (isSameDisk)
            {
                Console.WriteLine("[This program was designed to recover from a power failure, but not from");
                Console.WriteLine("user intervention, Do not abort or close this program during operation!]");
            }

            long bytesTotal = sourceExtent.Size;
            long bytesCopied = 0;
            Thread thread = new Thread(delegate()
            {
                List<DynamicDisk> disks = WindowsDynamicDiskHelper.GetPhysicalDynamicDisks();
                if (isSameDisk)
                {
                    MoveExtentHelper.MoveExtentWithinSameDisk(disks, dynamicVolume, sourceExtent, relocatedExtent, ref bytesCopied);
                }
                else
                {
                    MoveExtentHelper.MoveExtentToAnotherDisk(disks, dynamicVolume, sourceExtent, relocatedExtent, ref bytesCopied);
                }
            });
            thread.Start();

            while (thread.IsAlive)
            {
                Thread.Sleep(1000);
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.Write("Committed: {0} / {1}", GetStandardSizeString(bytesCopied), GetStandardSizeString(bytesTotal));
            }

            Console.WriteLine();
            Console.WriteLine();
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
            // volume has been modified and must be reselected
            m_selectedVolume = WindowsVolumeHelper.GetVolumeByGuid(dynamicVolume.VolumeGuid);
        }

        public static DynamicDiskExtent GetExtent(DynamicVolume volume, KeyValuePairList<string, string> parameters)
        {
            if (parameters.ContainsKey("extent"))
            {
                int extentIndex = Conversion.ToInt32(parameters.ValueOf("extent"), -1);
                if (extentIndex >= 0 && extentIndex < volume.DynamicExtents.Count)
                {
                    return volume.DynamicExtents[extentIndex];
                }
                else
                {
                    Console.WriteLine("Error: Invalid extent number.");
                }
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("Error: Extent number was not specified.");
            }

            return null;
        }

        public static void HelpMove()
        {
            Console.WriteLine();
            Console.WriteLine("    Move extent within the disk or to another disk.");
            Console.WriteLine();
            Console.WriteLine("Syntax: MOVE EXTENT=<N> OFFSET=<N>");
            Console.WriteLine("        MOVE EXTENT=<N> DISK=<N>");
            Console.WriteLine();
            Console.WriteLine("    OFFSET=<N>  Absolute number of bytes from the start of the disk.");
        }
    }
}
