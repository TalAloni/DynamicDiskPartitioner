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
    public class ExtendVolumeHelper
    {
        public static DiskGroupLockResult ExtendPartition(Partition volume, long numberOfAdditionalExtentSectors)
        {
            if (volume.Disk is PhysicalDisk)
            {
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    bool isReadOnly;
                    bool isOnline = ((PhysicalDisk)volume.Disk).GetOnlineStatus(out isReadOnly);
                    if (!isOnline || isReadOnly)
                    {
                        return DiskGroupLockResult.OneOrMoreDisksAreOfflineOrReadonly;
                    }
                }

                LockStatus status = LockHelper.LockBasicDiskAndVolumesOrNone(((PhysicalDisk)volume.Disk));
                if (status == LockStatus.CannotLockDisk)
                {
                    return DiskGroupLockResult.CannotLockDisk;
                }
                else if (status == LockStatus.CannotLockVolume)
                {
                    return DiskGroupLockResult.CannotLockVolume;
                }

                if (Environment.OSVersion.Version.Major >= 6)
                {
                    bool success = ((PhysicalDisk)volume.Disk).SetOnlineStatus(false);
                    if (!success)
                    {
                        return DiskGroupLockResult.CannotTakeDiskOffline;
                    }
                }
            }

            ExtendHelper.ExtendPartition(volume, numberOfAdditionalExtentSectors);

            if (volume.Disk is PhysicalDisk)
            {
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    bool success = ((PhysicalDisk)volume.Disk).SetOnlineStatus(true);
                }
                LockHelper.UnlockBasicDiskAndVolumes((PhysicalDisk)volume.Disk);
                ((PhysicalDisk)volume.Disk).UpdateProperties();
            }

            return DiskGroupLockResult.Success;
        }

        public static DiskGroupLockResult ExtendDynamicVolume(List<DynamicDisk> diskGroup, DynamicVolume volume, long numberOfAdditionalExtentSectors)
        {
            if (volume is StripedVolume)
            {
                numberOfAdditionalExtentSectors -= numberOfAdditionalExtentSectors % ((StripedVolume)volume).SectorsPerStripe;
            }
            if (volume is Raid5Volume)
            {
                numberOfAdditionalExtentSectors -= numberOfAdditionalExtentSectors % ((Raid5Volume)volume).SectorsPerStripe;
            }

            DiskGroupLockResult result = DiskGroupHelper.LockDiskGroup(diskGroup);
            if (result != DiskGroupLockResult.Success)
            {
                return result;
            }

            DiskGroupDatabase database = DiskGroupDatabase.ReadFromDisks(diskGroup, volume.DiskGroupGuid);
            ExtendHelper.ExtendDynamicVolume((DynamicVolume)volume, numberOfAdditionalExtentSectors, database);

            DiskGroupHelper.UnlockDiskGroup(diskGroup);

            return DiskGroupLockResult.Success;
        }
    }
}
