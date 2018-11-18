/* Copyright (C) 2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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

namespace DiskAccessLibraryTests
{
    public class NTFSLogTests
    {
        public static void Test(string path, long size)
        {
            TestRedo(path, size);
        }

        public static void TestRedo(string path, long size)
        {
            int bytesPerCluster = 512;
            while (bytesPerCluster <= 65536)
            {
                string volumeLabel = "RedoTest_" + bytesPerCluster.ToString();
                string fileName = "Test.txt";
                byte[] fileData = System.Text.Encoding.ASCII.GetBytes("Redone");
                CreateVolumeWithPendingFileCreation(path, size, bytesPerCluster, volumeLabel, fileName, fileData);

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
                byte[] bytesRead = File.ReadAllBytes(driveName + fileName);
                if (!ByteUtils.AreByteArraysEqual(fileData, bytesRead))
                {
                    throw new InvalidDataException("Test failed");
                }
                VHDMountHelper.UnmountVHD(path);
                File.Delete(path);

                bytesPerCluster = bytesPerCluster * 2;
            }
        }

        public static void CreateVolumeWithPendingFileCreation(string path, long size, int bytesPerCluster, string volumeLabel, string fileName, byte[] fileData)
        {
            VirtualHardDisk disk = VirtualHardDisk.CreateFixedDisk(path, size);
            disk.ExclusiveLock();
            NTFSVolume volume = NTFSFormatTests.CreateAndFormatPrimaryPartition(disk, bytesPerCluster, volumeLabel);
            //NTFSVolume volume = NTFSFormatTests.CreateVHDAndFormatPrimaryPartition(disk, 4096, volumeLabel);
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
