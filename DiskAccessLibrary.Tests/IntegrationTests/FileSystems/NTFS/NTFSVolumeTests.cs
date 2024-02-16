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
using DiskAccessLibrary.FileSystems.NTFS;
using Utilities;

namespace DiskAccessLibrary.Tests
{
    public class NTFSVolumeTests
    {
        public static void Test(string path, long size)
        {
            TestMove(path, size);
            TestCreateAndDeleteFiles(path, size, 50000);
        }

        private static void TestMove(string path, long size)
        {
            int bytesPerCluster = 512;
            while (bytesPerCluster <= 65536)
            {
                string volumeLabel = "MoveTest_" + bytesPerCluster.ToString();
                VirtualHardDisk disk = VirtualHardDisk.CreateFixedDisk(path, size);
                disk.ExclusiveLock();
                Partition partition = NTFSFormatTests.CreatePrimaryPartition(disk);
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
                byte[] bytesRead = File.ReadAllBytes(driveName + directory2Name + "\\" + fileNameAfter);
                if (!ByteUtils.AreByteArraysEqual(fileData, bytesRead))
                {
                    throw new InvalidDataException("Test failed");
                }
                VHDMountHelper.UnmountVHD(path);
                File.Delete(path);

                bytesPerCluster = bytesPerCluster * 2;
            }
        }

        private static void TestCreateAndDeleteFiles(string path, long size, int count)
        {
            int bytesPerCluster = 512;
            while (bytesPerCluster <= 65536)
            {
                string volumeLabel = "CreateFilesTest_" + bytesPerCluster.ToString();
                VirtualHardDisk disk = VirtualHardDisk.CreateFixedDisk(path, size);
                disk.ExclusiveLock();
                Partition partition = NTFSFormatTests.CreatePrimaryPartition(disk);
                NTFSVolume volume = NTFSVolumeCreator.Format(partition, bytesPerCluster, volumeLabel);
                string directoryName = "Directory";
                CreateFiles(volume, directoryName, count);
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

                if (count != Directory.GetFiles(driveName + directoryName).Length)
                {
                    throw new InvalidDataException("Test failed");
                }
                VHDMountHelper.UnmountVHD(path);
                disk.ExclusiveLock();
                volume = new NTFSVolume(partition);
                DeleteFiles(volume, directoryName, count);
                disk.ReleaseLock();
                VHDMountHelper.MountVHD(path);
                isErrorFree = ChkdskHelper.Chkdsk(driveName);
                if (!isErrorFree)
                {
                    throw new InvalidDataException("CHKDSK reported errors");
                }

                if (Directory.GetFiles(driveName + directoryName).Length > 0)
                {
                    throw new InvalidDataException("Test failed");
                }
                VHDMountHelper.UnmountVHD(path);
                File.Delete(path);

                bytesPerCluster = bytesPerCluster * 2;
            }
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
