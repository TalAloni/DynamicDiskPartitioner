/* Copyright (C) 2014-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using DiskAccessLibrary;
using DiskAccessLibrary.FileSystems;
using DiskAccessLibrary.LogicalDiskManager;
using Utilities;

namespace Raid5Manager
{
    public enum ExtendFileSystemResult
    {
        Success,
        NonOperationalVolume,
        UnsupportedFileSystem,
        CannotLockDisk,
        CannotLockVolume,
        CannotDismountVolume,
        OneOrMoreDisksAreOfflineOrReadonly,
        CannotTakeDiskOffline,
    }

    public class ExtendFileSystemHelper
    {
        /// <param name="diskGroup">Can be set to null when volume is partition</param>
        public static ExtendFileSystemResult ExtendFileSystem(List<DynamicDisk> diskGroup, Volume volume, long numberOfAdditionalSectors)
        {
            Guid? windowsVolumeGuid = WindowsVolumeHelper.GetWindowsVolumeGuid(volume);
            bool isMounted = windowsVolumeGuid.HasValue && WindowsVolumeManager.IsMounted(windowsVolumeGuid.Value);
            if (isMounted)
            {
                return ExtendMountedFileSystem(volume, windowsVolumeGuid.Value, numberOfAdditionalSectors);
            }
            else
            {
                if (volume is Partition)
                {
                    return ExtendUnmountedFileSystem((Partition)volume, numberOfAdditionalSectors);
                }
                else
                {
                    return ExtendUnmountedFileSystem(diskGroup, (DynamicVolume)volume, numberOfAdditionalSectors);
                }
            }
        }

        private static ExtendFileSystemResult ExtendUnmountedFileSystem(Partition volume, long numberOfAdditionalSectors)
        {
            IExtendableFileSystem fileSystem = FileSystemHelper.ReadFileSystem(volume) as IExtendableFileSystem;
            // Windows Vista / 7 enforce various limitations on direct write operations to volumes and disks.
            // Basic disks are not needed to be taken offline for direct write operations within volume region. Only dynamic disks have to.
            if (volume.Disk is PhysicalDisk && Environment.OSVersion.Version.Major >= 6)
            {
                bool isReadOnly;
                bool isOnline = ((PhysicalDisk)volume.Disk).GetOnlineStatus(out isReadOnly);
                if (!isOnline || isReadOnly)
                {
                    return ExtendFileSystemResult.OneOrMoreDisksAreOfflineOrReadonly;
                }

                LockStatus status = LockHelper.LockBasicDiskAndVolumesOrNone(((PhysicalDisk)volume.Disk));
                if (status == LockStatus.CannotLockDisk)
                {
                    return ExtendFileSystemResult.CannotLockDisk;
                }
                else if (status == LockStatus.CannotLockVolume)
                {
                    return ExtendFileSystemResult.CannotLockVolume;
                }

                bool success = ((PhysicalDisk)volume.Disk).SetOnlineStatus(false);
                if (!success)
                {
                    return ExtendFileSystemResult.CannotTakeDiskOffline;
                }
            }

            fileSystem.Extend(numberOfAdditionalSectors);

            if (volume.Disk is PhysicalDisk && (Environment.OSVersion.Version.Major >= 6))
            {
                bool success = ((PhysicalDisk)volume.Disk).SetOnlineStatus(true);
                LockHelper.UnlockBasicDiskAndVolumes((PhysicalDisk)volume.Disk);
            }

            return ExtendFileSystemResult.Success;
        }

        private static ExtendFileSystemResult ExtendUnmountedFileSystem(List<DynamicDisk> diskGroup, DynamicVolume volume, long numberOfAdditionalSectors)
        {
            if (!volume.IsOperational)
            {
                return ExtendFileSystemResult.NonOperationalVolume;
            }

            IExtendableFileSystem fileSystem = FileSystemHelper.ReadFileSystem(volume) as IExtendableFileSystem;
            // Windows Vista / 7 enforce various limitations on direct write operations to volumes and disks.
            // Basic disks are not needed to be taken offline for direct write operations within volume region. Only dynamic disks have to.
            if (Environment.OSVersion.Version.Major >= 6)
            {
                // Lock disks and volumes
                Console.WriteLine("Locking disks and volumes");
                DiskGroupLockResult lockResult = DiskGroupHelper.LockDiskGroup(diskGroup);
                if (lockResult == DiskGroupLockResult.CannotLockDisk)
                {
                    return ExtendFileSystemResult.CannotLockDisk;
                }
                else if (lockResult == DiskGroupLockResult.CannotLockVolume)
                {
                    return ExtendFileSystemResult.CannotLockVolume;
                }
                else if (lockResult == DiskGroupLockResult.CannotTakeDiskOffline)
                {
                    return ExtendFileSystemResult.CannotTakeDiskOffline;
                }
                else if (lockResult == DiskGroupLockResult.OneOrMoreDisksAreOfflineOrReadonly)
                {
                    return ExtendFileSystemResult.OneOrMoreDisksAreOfflineOrReadonly;
                }
            }
            fileSystem.Extend(numberOfAdditionalSectors);

            if (Environment.OSVersion.Version.Major >= 6)
            {
                DiskGroupHelper.UnlockDiskGroup(diskGroup);
            }

            return ExtendFileSystemResult.Success;
        }

        private static ExtendFileSystemResult ExtendMountedFileSystem(Volume volume, Guid windowsVolumeGuid, long numberOfAdditionalSectors)
        {
            // Windows Vista / 7 enforce various limitations on direct write operations to volumes and disks.
            // We either have to take the disk(s) offline or use the OS volume handle for write operations.
            // http://msdn.microsoft.com/en-us/library/ff551353%28v=vs.85%29.aspx
            OperatingSystemVolume osVolume = new OperatingSystemVolume(windowsVolumeGuid, volume.BytesPerSector, volume.Size);
            IExtendableFileSystem fileSystem = FileSystemHelper.ReadFileSystem(osVolume) as IExtendableFileSystem;
            if (fileSystem == null)
            {
                return ExtendFileSystemResult.UnsupportedFileSystem;
            }

            bool success = WindowsVolumeManager.ExclusiveLock(windowsVolumeGuid, FileAccess.ReadWrite);
            if (!success)
            {
                return ExtendFileSystemResult.CannotLockVolume;
            }

            // Dismounting the volume will make sure the OS have the correct filesystem size. (locking the volume is not enough)
            success = WindowsVolumeManager.DismountVolume(windowsVolumeGuid);
            if (!success)
            {
                return ExtendFileSystemResult.CannotDismountVolume;
            }

            fileSystem.Extend(numberOfAdditionalSectors);

            WindowsVolumeManager.ReleaseLock(windowsVolumeGuid);
            return ExtendFileSystemResult.Success;
        }
    }
}
