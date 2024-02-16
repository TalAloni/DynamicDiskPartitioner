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
    public class NTFSLogTests
    {
        private const long DiskSizeInBytes = 100 * 1024 * 1024;

        private VirtualHardDisk m_disk;

        [TestInitialize]
        public void Initialize()
        {
            string diskPath = $@"C:\LogRedoTest_{Guid.NewGuid()}.vhd";
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
        public void WhenRedoRecordPresent_ChkdskReportNoErrors(int bytesPerCluster)
        {
            Assert.IsTrue(VHDMountHelper.IsVHDMountInstalled(), "vhdmount.exe was not found! Please install Virtual Server 2005 R2 SP1 (select the VHDMount component)");

            string volumeLabel = "RedoTest_" + bytesPerCluster.ToString();
            string fileName = "Test.txt";
            byte[] fileData = System.Text.Encoding.ASCII.GetBytes("Redone");
            CreateVolumeWithPendingFileCreation(m_disk, bytesPerCluster, volumeLabel, fileName, fileData);

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
            byte[] bytesRead = File.ReadAllBytes(driveName + fileName);
            if (!ByteUtils.AreByteArraysEqual(fileData, bytesRead))
            {
                throw new InvalidDataException("Test failed");
            }
            VHDMountHelper.UnmountVHD(m_disk.Path);
        }

        public static void CreateVolumeWithPendingFileCreation(VirtualHardDisk disk, int bytesPerCluster, string volumeLabel, string fileName, byte[] fileData)
        {
            disk.ExclusiveLock();
            Partition partition = NTFSFormatTests.CreatePrimaryPartition(disk);
            NTFSVolume volume = NTFSVolumeCreator.Format(partition, bytesPerCluster, volumeLabel);
            long segmentNumber = MasterFileTable.FirstUserSegmentNumber;
            FileNameRecord fileNameRecord = new FileNameRecord(MasterFileTable.RootDirSegmentReference, fileName, false, DateTime.Now);
            FileRecordSegment fileRecordSegment = CreateFileRecordSegment(segmentNumber, fileNameRecord, fileData);

            ulong dataStreamOffset = (ulong)(segmentNumber * volume.BytesPerFileRecordSegment);
            byte[] redoData = fileRecordSegment.GetBytes(volume.BytesPerFileRecordSegment, volume.MinorVersion, false);
            MftSegmentReference mftFileReference = new MftSegmentReference(0, 1);
            AttributeRecord mftDataRecord = volume.GetFileRecord(mftFileReference).GetAttributeRecord(AttributeType.Data, String.Empty);
            AttributeRecord mftBitmapRecord = volume.GetFileRecord(mftFileReference).GetAttributeRecord(AttributeType.Bitmap, String.Empty);
            uint transactionID = volume.LogClient.AllocateTransactionID();
            volume.LogClient.WriteLogRecord(mftFileReference, mftDataRecord, dataStreamOffset, volume.BytesPerFileRecordSegment, NTFSLogOperation.InitializeFileRecordSegment, redoData, NTFSLogOperation.Noop, new byte[0], transactionID);
            long bitmapVCN = segmentNumber / (volume.BytesPerCluster * 8);
            int bitOffsetInCluster = (int)(segmentNumber % (volume.BytesPerCluster * 8));
            BitmapRange bitmapRange = new BitmapRange((uint)bitOffsetInCluster, 1);
            ulong bitmapStreamOffset = (ulong)(bitmapVCN * volume.BytesPerCluster);
            volume.LogClient.WriteLogRecord(mftFileReference, mftBitmapRecord, bitmapStreamOffset, volume.BytesPerCluster, NTFSLogOperation.SetBitsInNonResidentBitMap, bitmapRange.GetBytes(), NTFSLogOperation.Noop, new byte[0], transactionID);

            FileRecord parentDirectoryRecord = volume.GetFileRecord(MasterFileTable.RootDirSegmentReference);
            IndexData parentDirectoryIndex = new IndexData(volume, parentDirectoryRecord, AttributeType.FileName);
            byte[] fileNameRecordBytes = fileNameRecord.GetBytes();
            long leafRecordVBN = 0;
            IndexRecord leafRecord = parentDirectoryIndex.ReadIndexRecord(leafRecordVBN);
            ulong indexAllocationOffset = (ulong)parentDirectoryIndex.ConvertToDataOffset(leafRecordVBN);
            int insertIndex = CollationHelper.FindIndexForSortedInsert(leafRecord.IndexEntries, fileNameRecordBytes, CollationRule.Filename);
            int insertOffset = leafRecord.GetEntryOffset(volume.BytesPerIndexRecord, insertIndex);

            AttributeRecord rootDirIndexAllocation = volume.GetFileRecord(MasterFileTable.RootDirSegmentReference).GetAttributeRecord(AttributeType.IndexAllocation, IndexHelper.GetIndexName(AttributeType.FileName));
            IndexEntry indexEntry = new IndexEntry(fileRecordSegment.SegmentReference, fileNameRecord.GetBytes());
            volume.LogClient.WriteLogRecord(MasterFileTable.RootDirSegmentReference, rootDirIndexAllocation, indexAllocationOffset, volume.BytesPerIndexRecord, 0, insertOffset, NTFSLogOperation.AddIndexEntryToAllocationBuffer, indexEntry.GetBytes(), NTFSLogOperation.Noop, new byte[0], transactionID, false);

            volume.LogClient.WriteForgetTransactionRecord(transactionID, true);
            disk.ReleaseLock();
        }

        private static FileRecordSegment CreateFileRecordSegment(long segmentNumber, FileNameRecord fileNameRecord, byte[] fileData)
        {
            fileNameRecord.AllocatedLength = (uint)fileData.Length;
            fileNameRecord.FileSize = (uint)fileData.Length;
            FileRecordSegment fileRecordSegment = new FileRecordSegment(segmentNumber, 1);
            fileRecordSegment.IsInUse = true;
            fileRecordSegment.ReferenceCount = 1;
            StandardInformationRecord standardInformation = (StandardInformationRecord)fileRecordSegment.CreateAttributeRecord(AttributeType.StandardInformation, String.Empty);
            standardInformation.CreationTime = fileNameRecord.CreationTime;
            standardInformation.ModificationTime = fileNameRecord.ModificationTime;
            standardInformation.MftModificationTime = fileNameRecord.MftModificationTime;
            standardInformation.LastAccessTime = fileNameRecord.LastAccessTime;
            FileNameAttributeRecord fileNameAttribute = (FileNameAttributeRecord)fileRecordSegment.CreateAttributeRecord(AttributeType.FileName, String.Empty);
            fileNameAttribute.IsIndexed = true;
            fileNameAttribute.Record = fileNameRecord;
            ResidentAttributeRecord dataRecord = (ResidentAttributeRecord)fileRecordSegment.CreateAttributeRecord(AttributeType.Data, String.Empty);
            dataRecord.Data = fileData;

            return fileRecordSegment;
        }
    }
}
