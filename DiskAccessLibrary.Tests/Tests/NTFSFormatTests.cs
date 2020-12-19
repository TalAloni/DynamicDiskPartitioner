/* Copyright (C) 2018-2019 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
using DiskAccessLibrary.FileSystems.NTFS;
using Utilities;

namespace DiskAccessLibrary.Tests
{
    public class NTFSFormatTests
    {
        public static void Test(string path, long size)
        {
            int bytesPerCluster = 512;
            while (bytesPerCluster <= 65536)
            {
                string volumeLabel = "FormatTest_" + bytesPerCluster.ToString();
                VirtualHardDisk disk = VirtualHardDisk.CreateFixedDisk(path, size);
                disk.ExclusiveLock();
                Partition partition = CreatePrimaryPartition(disk);
                NTFSVolumeCreator.Format(partition, bytesPerCluster, volumeLabel);
                disk.ReleaseLock();
                VHDMountHelper.MountVHD(path);
                string driveName = MountHelper.WaitForDriveToMount(volumeLabel);
                if (driveName == null)
                {
                    throw new Exception("Timeout waiting for volume to mount");
                }
                bool isErrorFree = ChkdskHelper.Chkdsk(driveName);
                if (!isErrorFree)
                {
                    throw new InvalidDataException("CHKDSK reported errors");
                }
                VHDMountHelper.UnmountVHD(path);
                File.Delete(path);

                bytesPerCluster = bytesPerCluster * 2;
            }
        }

        public static Partition CreatePrimaryPartition(Disk disk)
        {
            MasterBootRecord mbr = new MasterBootRecord();
            mbr.DiskSignature = (uint)new Random().Next(Int32.MaxValue);
            mbr.PartitionTable[0].PartitionTypeName = PartitionTypeName.Primary;
            mbr.PartitionTable[0].FirstSectorLBA = 63;
            mbr.PartitionTable[0].SectorCountLBA = (uint)Math.Min(disk.TotalSectors - 63, UInt32.MaxValue);
            MasterBootRecord.WriteToDisk(disk, mbr);
            return BasicDiskHelper.GetPartitions(disk)[0];
        }
    }
}
