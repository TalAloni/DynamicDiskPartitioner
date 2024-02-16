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
using Utilities;

namespace DiskAccessLibrary.Tests.IntegrationTests.FileSystems.NTFS
{
    [TestClass]
    public class NTFSVolumeTests
    {
        private const long DiskSizeInBytes = 100 * 1024 * 1024;

        private VirtualHardDisk m_disk;

        [TestInitialize]
        public void Initialize()
        {
            string diskPath = $@"C:\VolumeTest_{Guid.NewGuid()}.vhd";
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
        public void TestMove(int bytesPerCluster)
        {
            Assert.IsTrue(VHDMountHelper.IsVHDMountInstalled(), "vhdmount.exe was not found! Please install Virtual Server 2005 R2 SP1 (select the VHDMount component)");

            string volumeLabel = "MoveTest_" + bytesPerCluster.ToString();
            m_disk.ExclusiveLock();
            Partition partition = NTFSFormatTests.CreatePrimaryPartition(m_disk);
            NTFSVolume volume = NTFSVolumeCreator.Format(partition, bytesPerCluster, volumeLabel);

            string directory1Name = "Directory1";
            string directory2Name = "Directory2";
            FileRecord directory1Record = volume.CreateFile(NTFSVolume.RootDirSegmentReference, directory1Name, true);
            FileRecord directory2Record = volume.CreateFile(NTFSVolume.RootDirSegmentReference, directory2Name, true);
            string fileNameBefore = "Test1.txt";
            string fileNameAfter = "Test2.txt";
            FileRecord fileRecord = volume.CreateFile(directory1Record.BaseSegmentReference, fileNameBefore, false);
            NTFSFile file = new NTFSFile(volume, fileRecord);
            byte[] fileData = System.Text.Encoding.ASCII.GetBytes("Test");
            file.Data.WriteBytes(0, fileData);
            volume.UpdateFileRecord(fileRecord);
            volume.MoveFile(fileRecord, directory2Record.BaseSegmentReference, fileNameAfter);
            m_disk.ReleaseLock();
            VHDMountHelper.MountVHD(m_disk.Path);
            string driveName = MountHelper.WaitForDriveToMount(volumeLabel);
            if (driveName == null)
            {
                throw new Exception("Timeout waiting for volume to mount");
            }
            bool isErrorFree = ChkdskHelper.Chkdsk(driveName);
            bool isFileDataMatchExpected = false;
            if (isErrorFree)
            {
                byte[] bytesRead = File.ReadAllBytes(driveName + directory2Name + "\\" + fileNameAfter);
                isFileDataMatchExpected = ByteUtils.AreByteArraysEqual(fileData, bytesRead);
            }

            VHDMountHelper.UnmountVHD(m_disk.Path);
            Assert.IsTrue(isErrorFree, "CHKDSK reported errors");
            Assert.IsTrue(isFileDataMatchExpected, "File data does not match test data");
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
        public void TestCreateFiles(int bytesPerCluster)
        {
            Assert.IsTrue(VHDMountHelper.IsVHDMountInstalled(), "vhdmount.exe was not found! Please install Virtual Server 2005 R2 SP1 (select the VHDMount component)");
            int numberOfFilesToCreate = 50000;
            string volumeLabel = "CreateFilesTest_" + bytesPerCluster.ToString();
            m_disk.ExclusiveLock();
            Partition partition = NTFSFormatTests.CreatePrimaryPartition(m_disk);
            NTFSVolume volume = NTFSVolumeCreator.Format(partition, bytesPerCluster, volumeLabel);
            string directoryName = "Directory";
            CreateFiles(volume, directoryName, numberOfFilesToCreate);
            m_disk.ReleaseLock();

            VHDMountHelper.MountVHD(m_disk.Path);
            string driveName = MountHelper.WaitForDriveToMount(volumeLabel);
            if (driveName == null)
            {
                throw new Exception("Timeout waiting for volume to mount");
            }
            bool isErrorFree = ChkdskHelper.Chkdsk(driveName);
            int numberOfFilesObservedByOS = -1;
            if (isErrorFree)
            {
                numberOfFilesObservedByOS = Directory.GetFiles(driveName + directoryName).Length;
            }
            VHDMountHelper.UnmountVHD(m_disk.Path);

            Assert.IsTrue(isErrorFree, "CHKDSK reported errors");
            Assert.AreEqual(numberOfFilesToCreate, numberOfFilesObservedByOS);
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
        public void TestDeleteFiles(int bytesPerCluster)
        {
            Assert.IsTrue(VHDMountHelper.IsVHDMountInstalled(), "vhdmount.exe was not found! Please install Virtual Server 2005 R2 SP1 (select the VHDMount component)");

            int count = 50000;
            string volumeLabel = "DeleteFilesTest_" + bytesPerCluster.ToString();
            m_disk.ExclusiveLock();
            Partition partition = NTFSFormatTests.CreatePrimaryPartition(m_disk);
            NTFSVolume volume = NTFSVolumeCreator.Format(partition, bytesPerCluster, volumeLabel);
            string directoryName = "Directory";
            CreateFiles(volume, directoryName, count);
            DeleteFiles(volume, directoryName, count);
            m_disk.ReleaseLock();
            VHDMountHelper.MountVHD(m_disk.Path);
            string driveName = MountHelper.WaitForDriveToMount(volumeLabel);
            if (driveName == null)
            {
                throw new Exception("Timeout waiting for volume to mount");
            }

            bool isErrorFree = ChkdskHelper.Chkdsk(driveName);
            int numberOfFilesObservedByOS = -1;
            if (isErrorFree)
            {
                numberOfFilesObservedByOS = Directory.GetFiles(driveName + directoryName).Length;
            }
            VHDMountHelper.UnmountVHD(m_disk.Path);

            Assert.IsTrue(isErrorFree, "CHKDSK reported errors");
            Assert.AreEqual(0, numberOfFilesObservedByOS);
        }

        private static void CreateFiles(NTFSVolume volume, string directoryName, int count)
        {
            FileRecord parentDirectoryRecord = volume.CreateFile(NTFSVolume.RootDirSegmentReference, directoryName, true);
            for (int index = 1; index <= count; index++)
            {
                string fileName = "File" + index.ToString("000000");
                FileRecord fileRecord = volume.CreateFile(parentDirectoryRecord.BaseSegmentReference, fileName, false);
            }
        }

        private static void DeleteFiles(NTFSVolume volume, string directoryName, int count)
        {
            for (int index = 1; index <= count; index++)
            {
                string fileName = "File" + index.ToString("000000");
                string filePath = "\\" + directoryName + "\\" + fileName;
                FileRecord fileRecord = volume.GetFileRecord(filePath);
                volume.DeleteFile(fileRecord);
            }
        }
    }
}
