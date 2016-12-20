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
        public static void ResumeCommand(string[] args)
        {
            if (m_selectedVolume == null)
            {
                Console.WriteLine("No volume has been selected.");
                return;
            }

            if (m_selectedVolume is DynamicVolume)
            {
                if (!((DynamicVolume)m_selectedVolume).IsOperational)
                {
                    Console.WriteLine("Volume is not operational.");
                    return;
                }
            }

            byte[] bootRecord = m_selectedVolume.ReadSector(0);
            if (!RAID5ManagerBootRecord.HasValidSignature(bootRecord))
            {
                Console.WriteLine("Nothing to resume (resume boot record is not present).");
                return;
            }
            RAID5ManagerBootRecord resumeRecord = RAID5ManagerBootRecord.FromBytes(bootRecord);
            if (resumeRecord == null)
            {
                Console.WriteLine("Resume boot record version is not supported.");
                return;
            }
            
            if (resumeRecord is AddDiskOperationBootRecord)
            {
                // the RAID-5 volume was temporarily converted to striped volume
                if (m_selectedVolume is StripedVolume)
                {
                    StripedVolume stripedVolume = (StripedVolume)m_selectedVolume;

                    // Lock disks and volumes
                    Console.WriteLine("Locking disks and volumes");
                    LockStatus status = LockManager.LockDynamicDiskGroup(stripedVolume.DiskGroupGuid, true);
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
                        if (!DiskOfflineHelper.IsDiskGroupOnlineAndWritable(stripedVolume.DiskGroupGuid))
                        {
                            Console.WriteLine("Error: One or more dynamic disks are offline or set to readonly.");
                            LockManager.UnlockAllDisksAndVolumes();
                            return;
                        }

                        Console.WriteLine("Taking dynamic disks offline.");
                        bool success = DiskOfflineHelper.OfflineDiskGroup(stripedVolume.DiskGroupGuid);
                        if (!success)
                        {
                            Console.WriteLine("Failed to take all dynamic disks offline!");
                            LockManager.UnlockAllDisksAndVolumes();
                            return;
                        }
                    }

                    // Perform the operation
                    Console.WriteLine("Resuming the operation");

                    long bytesTotal = stripedVolume.Size / stripedVolume.NumberOfColumns * (stripedVolume.NumberOfColumns - 2);
                    long bytesCopied = 0;
                    Thread thread = new Thread(delegate()
                    {
                        List<DynamicDisk> diskGroup = WindowsDynamicDiskHelper.GetPhysicalDynamicDisks(stripedVolume.DiskGroupGuid);
                        DiskGroupDatabase database = DiskGroupDatabase.ReadFromDisks(diskGroup, stripedVolume.DiskGroupGuid);
                        AddDiskToArrayHelper.ResumeAddDiskToRaid5Volume(database, stripedVolume, (AddDiskOperationBootRecord)resumeRecord, ref bytesCopied);
                    });
                    thread.Start();

                    while (thread.IsAlive)
                    {
                        Thread.Sleep(1000);
                        Console.SetCursorPosition(0, Console.CursorTop);
                        Console.Write("Committed: {0} / {1}", FormattingHelper.GetStandardSizeString(bytesCopied), FormattingHelper.GetStandardSizeString(bytesTotal));
                    }

                    Console.WriteLine();
                    Console.WriteLine();
                    Console.WriteLine("Operation completed.");

                    if (Environment.OSVersion.Version.Major >= 6)
                    {
                        Console.WriteLine("Taking dynamic disks online.");
                        DiskOfflineHelper.OnlineDiskGroup(stripedVolume.DiskGroupGuid);
                        LockManager.UnlockAllDisksAndVolumes();
                    }
                    else
                    {
                        OperatingSystemHelper.RestartLDMAndUnlockDisksAndVolumes();
                    }

                    // volume has been modified and must be reselected
                    m_selectedVolume = WindowsVolumeHelper.GetVolumeByGuid(stripedVolume.VolumeGuid);
                }
            }
            else if (resumeRecord is MoveExtentOperationBootRecord)
            {
                if (m_selectedVolume is DynamicVolume)
                {
                    DynamicVolume dynamicVolume = (DynamicVolume)m_selectedVolume;

                    // Lock disks and volumes
                    Console.WriteLine("Locking disks and volumes");
                    LockStatus status = LockManager.LockDynamicDiskGroup(dynamicVolume.DiskGroupGuid, true);
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
                        if (!DiskOfflineHelper.IsDiskGroupOnlineAndWritable(dynamicVolume.DiskGroupGuid))
                        {
                            Console.WriteLine("Error: One or more dynamic disks are offline or set to readonly.");
                            LockManager.UnlockAllDisksAndVolumes();
                            return;
                        }

                        Console.WriteLine("Taking dynamic disks offline.");
                        bool success = DiskOfflineHelper.OfflineDiskGroup(dynamicVolume.DiskGroupGuid);
                        if (!success)
                        {
                            Console.WriteLine("Failed to take all dynamic disks offline!");
                            LockManager.UnlockAllDisksAndVolumes();
                            return;
                        }
                    }

                    // Perform the operation
                    Console.WriteLine("Resuming the operation");

                    int extentIndex = DynamicDiskExtentHelper.GetIndexOfExtentID(dynamicVolume.DynamicExtents, ((MoveExtentOperationBootRecord)resumeRecord).ExtentID);
                    DynamicDiskExtent sourceExtent = dynamicVolume.DynamicExtents[extentIndex];

                    long bytesTotal = sourceExtent.Size;
                    long bytesCopied = 0;
                    Thread thread = new Thread(delegate()
                    {
                        List<DynamicDisk> disks = WindowsDynamicDiskHelper.GetPhysicalDynamicDisks(dynamicVolume.DiskGroupGuid);
                        MoveExtentHelper.ResumeMoveExtent(disks, dynamicVolume, (MoveExtentOperationBootRecord)resumeRecord, ref bytesCopied);
                    });
                    thread.Start();

                    while (thread.IsAlive)
                    {
                        Thread.Sleep(1000);
                        Console.SetCursorPosition(0, Console.CursorTop);
                        Console.Write("Committed: {0} / {1}", FormattingHelper.GetStandardSizeString(bytesCopied), FormattingHelper.GetStandardSizeString(bytesTotal));
                    }

                    Console.WriteLine();
                    Console.WriteLine();
                    Console.WriteLine("Operation completed.");

                    if (Environment.OSVersion.Version.Major >= 6)
                    {
                        Console.WriteLine("Taking dynamic disks online.");
                        DiskOfflineHelper.OnlineDiskGroup(dynamicVolume.DiskGroupGuid);
                        LockManager.UnlockAllDisksAndVolumes();
                    }
                    else
                    {
                        OperatingSystemHelper.RestartLDMAndUnlockDisksAndVolumes();
                    }

                    // volume has been modified and must be reselected
                    m_selectedVolume = WindowsVolumeHelper.GetVolumeByGuid(dynamicVolume.VolumeGuid);
                }
            }
            else
            {
                Console.WriteLine("Resume boot record operation is not supported.");
                return;
            }
        }

        public static void HelpResume()
        {
            Console.WriteLine();
            Console.WriteLine("    Resume the operation.");
            Console.WriteLine();
            Console.WriteLine("Syntax: RESUME");
            Console.WriteLine();
        }
    }
}
