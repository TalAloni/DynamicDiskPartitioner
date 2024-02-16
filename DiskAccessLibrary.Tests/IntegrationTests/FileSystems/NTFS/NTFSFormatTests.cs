/* Copyright (C) 2018-2024 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.IO;
using DiskAccessLibrary.FileSystems.NTFS;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DiskAccessLibrary.Tests.IntegrationTests.FileSystems.NTFS
{
    [TestClass]
    public class NTFSFormatTests
    {
        private const long DiskSizeInBytes = 100 * 1024 * 1024;
        
        private VirtualHardDisk m_disk;

        [TestInitialize]
        public void Initialize()
        {
            string diskPath = $@"C:\FormatTest_{Guid.NewGuid()}.vhd";
            m_disk = VirtualHardDisk.CreateFixedDisk(diskPath, DiskSizeInBytes);
        }

        [TestCleanup]
        public void Cleanup()
        {
            File.Delete(m_disk.Path);
        }

        [DataTestMethod]
        [DataRow(512)]
        [DataRow(1024)]
        [DataRow(2048)]
        [DataRow(4096)]
        [DataRow(8192)]
        [DataRow(16384)]
        [DataRow(32768)]
        [DataRow(65536)]
        public void WhenVolumeIsFormatted_ChkdskReportNoErrors(int bytesPerCluster)
        {
            Assert.IsTrue(VHDMountHelper.IsVHDMountInstalled(), "vhdmount.exe was not found! Please install Virtual Server 2005 R2 SP1 (select the VHDMount component)");

            string volumeLabel = "FormatTest_" + bytesPerCluster.ToString();
            m_disk.ExclusiveLock();
            Partition partition = CreatePrimaryPartition(m_disk);
            NTFSVolumeCreator.Format(partition, bytesPerCluster, volumeLabel);
            m_disk.ReleaseLock();
            VHDMountHelper.MountVHD(m_disk.Path);
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
            VHDMountHelper.UnmountVHD(m_disk.Path);
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
