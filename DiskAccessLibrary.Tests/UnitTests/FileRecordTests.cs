using System;
using System.Collections.Generic;
using DiskAccessLibrary.FileSystems.NTFS;
using Utilities;

namespace DiskAccessLibrary.Tests
{
    public class FileRecordTests
    {
        public static void Tests()
        {
            AttributeCloneTest();
        }

        /// <summary>
        /// This test checks that attributes are deep cloned when assembled from FileRecordSegment list and sliced into FileRecordSegment list.
        /// </summary>
        public static void AttributeCloneTest()
        {
            byte minorNTFSVersion = 1;
            int bytesPerFileRecordSegment = 1024;
            FileRecordSegment baseSegment = new FileRecordSegment(30, 1);
            FileRecordSegment segment2 = new FileRecordSegment(31, 1, baseSegment.SegmentReference);
            FileNameRecord fileNameRecord = new FileNameRecord(NTFSVolume.RootDirSegmentReference, "john.txt", false, DateTime.Now);
            FileNameAttributeRecord fileNameAttribute = new FileNameAttributeRecord(String.Empty);
            fileNameAttribute.Record = fileNameRecord;
            NonResidentAttributeRecord dataRecord = new NonResidentAttributeRecord(AttributeType.Data, String.Empty);
            baseSegment.ImmediateAttributes.Add(fileNameAttribute);
            baseSegment.ImmediateAttributes.Add(dataRecord);
            dataRecord.DataRunSequence.Add(new DataRun(5, 0));
            byte[] segmentBytesBefore = baseSegment.GetBytes(bytesPerFileRecordSegment, minorNTFSVersion, false);

            List<FileRecordSegment> segments = new List<FileRecordSegment>();
            segments.Add(baseSegment);
            segments.Add(segment2);
            FileRecord fileRecord = new FileRecord(segments);
            ((NonResidentAttributeRecord)fileRecord.DataRecord).DataRunSequence[0].RunLength = 8;
            fileRecord.FileNameRecord.ParentDirectory = new MftSegmentReference(8, 8);
            fileRecord.FileNameRecord.FileName = "d";
            byte[] segmentBytesAfter = baseSegment.GetBytes(bytesPerFileRecordSegment, minorNTFSVersion, false);
            if (!ByteUtils.AreByteArraysEqual(segmentBytesBefore, segmentBytesAfter))
            {
                throw new Exception("Test failed");
            }

            fileRecord.UpdateSegments(1024, 1);
            byte[] segmentBytesBefore2 = fileRecord.Segments[0].GetBytes(bytesPerFileRecordSegment, minorNTFSVersion, false);
            ((NonResidentAttributeRecord)fileRecord.DataRecord).DataRunSequence[0].RunLength = 10;
            fileRecord.FileNameRecord.FileName = "x";
            byte[] segmentBytesAfter2 = fileRecord.Segments[0].GetBytes(bytesPerFileRecordSegment, minorNTFSVersion, false);
            if (!ByteUtils.AreByteArraysEqual(segmentBytesBefore2, segmentBytesAfter2))
            {
                throw new Exception("Test failed");
            }
        }
    }
}
