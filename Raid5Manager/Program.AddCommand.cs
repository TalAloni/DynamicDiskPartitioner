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
        public static void AddCommand(string[] args)
        {
            if (args.Length == 1)
            {
                HelpAdd();
                return;
            }

            if (m_selectedVolume == null)
            {
                Console.WriteLine("No volume has been selected.");
                return;
            }

            if (m_selectedVolume is DynamicVolume)
            {
                DiskGroupDatabase database = DiskGroupDatabase.ReadFromPhysicalDisks(((DynamicVolume)m_selectedVolume).DiskGroupGuid);
                if (database == null)
                {
                    Console.WriteLine("The selected volume is invalid");
                    return;
                }

                // We can make the code work on degraded volumes as well, but it's not a good practice anyway.
                if (!((DynamicVolume)m_selectedVolume).IsHealthy)
                {
                    Console.WriteLine("Volume is not healthy");
                    return;
                }
            }

            if (RAID5ManagerBootRecord.HasValidSignature(m_selectedVolume.ReadSector(0)))
            {
                Console.WriteLine("There is already an operation in progress");
                Console.WriteLine("Use the RESUME command to resume the operation");
                return;
            }

            if (!(m_selectedVolume is Raid5Volume))
            {
                Console.WriteLine("Disks can only be added to RAID-5 Volume.");
                return;
            }

            KeyValuePairList<string, string> parameters = ParseParameters(args, 1);
            if (!VerifyParameters(parameters, "disk") || !parameters.ContainsKey("disk"))
            {
                HelpAdd();
                return;
            }
            
            int physicalDiskIndex = Conversion.ToInt32(parameters.ValueOf("disk"), -1);
            PhysicalDisk targetDisk = null;
            if (physicalDiskIndex >= 0)
            {
                try
                {
                    targetDisk = new PhysicalDisk(physicalDiskIndex);
                }
                catch
                {
                    Console.WriteLine("Cannot access requested disk.");
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

            foreach (DiskExtent extent in ((DynamicVolume)m_selectedVolume).Extents)
            {
                if (extent.Disk is PhysicalDisk)
                {
                    if (targetDisk.PhysicalDiskIndex == ((PhysicalDisk)extent.Disk).PhysicalDiskIndex)
                    {
                        // Windows will report such configuration as 'Duplicate records', so we should prevent this
                        Console.WriteLine("The disk specified already contains an extent that belongs to the selected");
                        Console.WriteLine("volume.");
                        return;
                    }
                }
            }

            VolumeManagerDatabase targetDiskDatabase = VolumeManagerDatabase.ReadFromDisk(targetDynamicDisk);
            if (((DynamicVolume)m_selectedVolume).DiskGroupGuid != targetDiskDatabase.DiskGroupGuid)
            {
                Console.WriteLine("The disk specified does not belong to the same dynamic disk group");
                return;
            }

            Raid5Volume raid5Volume = (Raid5Volume)m_selectedVolume;
            DiskExtent newExtent = DynamicDiskExtentHelper.AllocateNewExtent(targetDynamicDisk, raid5Volume.ColumnSize);
            if (newExtent == null)
            {
                Console.WriteLine("The disk specified does not contain enough free space.");
                return;
            }

            // Lock disks and volumes
            Console.WriteLine("Locking disks and volumes");
            LockStatus status = LockManager.LockAllDynamicDisks(true);
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
                    LockManager.UnlockAllDisksAndVolumes();
                    return;
                }

                Console.WriteLine("Taking dynamic disks offline.");
                bool success = DiskOfflineHelper.OfflineAllDynamicDisks();
                if (!success)
                {
                    Console.WriteLine("Failed to take all dynamic disks offline!");
                    LockManager.UnlockAllDisksAndVolumes();
                    return;
                }
            }

            // Perform the operation
            Console.WriteLine("Starting the operation");
            Console.WriteLine("[This program was designed to recover from a power failure, but not from");
            Console.WriteLine("user intervention, Do not abort or close this program during operation!]");

            long bytesTotal = raid5Volume.Size;
            long bytesCopied = 0;
            Thread thread = new Thread(delegate()
            {
                List<DynamicDisk> disks = WindowsDynamicDiskHelper.GetPhysicalDynamicDisks();
                AddDiskToArrayHelper.AddDiskToRaid5Volume(disks, raid5Volume, newExtent, ref bytesCopied);
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
                LockManager.UnlockAllDisksAndVolumes();
            }
            else
            {
                OperatingSystemHelper.RestartLDMAndUnlockDisksAndVolumes();
            }

            // volume has been modified and must be reselected
            m_selectedVolume = WindowsVolumeHelper.GetVolumeByGuid(raid5Volume.VolumeGuid);
        }

        public static void HelpAdd()
        {
            Console.WriteLine();
            Console.WriteLine("    Expands the selected volume using an additional disk.");
            Console.WriteLine();
            Console.WriteLine("    Note: The filesystem residing on the volume will not be extended,");
            Console.WriteLine("          you can use \"EXTEND FILESYSTEM\" to extend the file system.");
            Console.WriteLine();
            Console.WriteLine("Syntax: ADD DISK=<N>");
        }
    }
}
