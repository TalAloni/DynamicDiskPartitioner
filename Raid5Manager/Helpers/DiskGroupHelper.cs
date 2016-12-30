/* Copyright (C) 2014-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using DiskAccessLibrary;
using DiskAccessLibrary.LogicalDiskManager;

namespace Raid5Manager
{
    public enum DiskGroupLockResult
    {
        Success,
        CannotLockDisk,
        CannotLockVolume,
        OneOrMoreDisksAreOfflineOrReadonly,
        CannotTakeDiskOffline,
    }

    public class DiskGroupHelper
    {
        public static DiskGroupLockResult LockDiskGroup(List<DynamicDisk> diskGroup)
        {
            // Lock disks and volumes
            LockStatus status = LockManager.LockDynamicDiskGroup(diskGroup, true);
            if (status != LockStatus.Success)
            {
                if (status == LockStatus.CannotLockDisk)
                {
                    return DiskGroupLockResult.CannotLockDisk;
                }
                else
                {
                    return DiskGroupLockResult.CannotLockVolume;
                }
            }

            if (Environment.OSVersion.Version.Major >= 6)
            {
                if (!DiskOfflineHelper.IsDiskGroupOnlineAndWritable(diskGroup))
                {
                    LockManager.UnlockAllDisksAndVolumes();
                    return DiskGroupLockResult.OneOrMoreDisksAreOfflineOrReadonly;
                }

                Console.WriteLine("Taking dynamic disks offline.");
                bool success = DiskOfflineHelper.OfflineAllOrNone(diskGroup);
                if (!success)
                {
                    LockManager.UnlockAllDisksAndVolumes();
                    return DiskGroupLockResult.CannotTakeDiskOffline;
                }
            }

            return DiskGroupLockResult.Success;
        }

        public static void UnlockDiskGroup(List<DynamicDisk> diskGroup)
        {
            if (Environment.OSVersion.Version.Major >= 6)
            {
                Console.WriteLine("Taking dynamic disks online.");
                DiskOfflineHelper.OnlineAll(diskGroup);
                LockManager.UnlockAllDisksAndVolumes();
            }
            else
            {
                OperatingSystemHelper.RestartLDMAndUnlockDisksAndVolumes();
            }
        }
    }
}
