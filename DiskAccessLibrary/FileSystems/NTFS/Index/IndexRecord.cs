/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    public class IndexRecord
    {
        public const string ValidSignature = "INDX";
        public const int IndexHeaderOffset = 0x18;
        public const int UpdateSequenceArrayOffset = 0x28;
        public const int BytesPerIndexRecordBlock = 512;

        // MULTI_SECTOR_HEADER
        public ulong LogFileSequenceNumber;
        public long RecordVBN; // Stored as unsigned, but is within the range of long
        public IndexHeader IndexHeader;
        public ushort UpdateSequenceNumber;
        // byte[] UpdateSequenceReplacementData
        // Padding to align to 8-byte boundary
        public List<IndexEntry> IndexEntries;

        public IndexRecord()
        {
            IndexHeader = new IndexHeader();
            IndexEntries = new List<IndexEntry>();
        }

        public IndexRecord(byte[] buffer, int offset)
        {
            MultiSectorHeader multiSectorHeader = new MultiSectorHeader(buffer, offset + 0x00);
            if (multiSectorHeader.Signature != ValidSignature)
            {
                throw new InvalidDataException("Invalid INDX record signature");
            }
            LogFileSequenceNumber = LittleEndianConverter.ToUInt64(buffer, offset + 0x08);
            RecordVBN = (long)LittleEndianConverter.ToUInt64(buffer, offset + 0x10);
            IndexHeader = new IndexHeader(buffer, offset + 0x18);

            int position = offset + multiSectorHeader.UpdateSequenceArrayOffset;
            List<byte[]> updateSequenceReplacementData = MultiSectorHelper.ReadUpdateSequenceArray(buffer, position, multiSectorHeader.UpdateSequenceArraySize, out UpdateSequenceNumber);
            MultiSectorHelper.DecodeSegmentBuffer(buffer, offset, UpdateSequenceNumber, updateSequenceReplacementData);

            int entriesOffset = 0x18 + (int)IndexHeader.EntriesOffset;
            IndexEntries = IndexEntry.ReadIndexEntries(buffer, entriesOffset);
        }

        public byte[] GetBytes(int bytesPerIndexRecord)
        {
            int strideCount = bytesPerIndexRecord / MultiSectorHelper.BytesPerStride;
            ushort updateSequenceArraySize = (ushort)(1 + strideCount);
            MultiSectorHeader multiSectorHeader = new MultiSectorHeader(ValidSignature, UpdateSequenceArrayOffset, updateSequenceArraySize);

            int updateSequenceArrayPaddedLength = (int)Math.Ceiling((double)(updateSequenceArraySize * 2) / 8) * 8;

            IndexHeader.EntriesOffset = (uint)(IndexHeader.Length + updateSequenceArrayPaddedLength);
            IndexHeader.TotalLength = (uint)(IndexHeader.Length + updateSequenceArrayPaddedLength + IndexEntry.GetLength(IndexEntries));
            IndexHeader.AllocatedLength = (uint)(bytesPerIndexRecord - IndexHeaderOffset);

            byte[] buffer = new byte[bytesPerIndexRecord];
            multiSectorHeader.WriteBytes(buffer, 0x00);
            LittleEndianWriter.WriteUInt64(buffer, 0x08, LogFileSequenceNumber);
            LittleEndianWriter.WriteUInt64(buffer, 0x10, (ulong)RecordVBN);
            IndexHeader.WriteBytes(buffer, 0x18);

            IndexEntry.WriteIndexEntries(buffer, UpdateSequenceArrayOffset + updateSequenceArrayPaddedLength, IndexEntries);

            // Write UpdateSequenceNumber and UpdateSequenceReplacementData
            List<byte[]> updateSequenceReplacementData = MultiSectorHelper.EncodeSegmentBuffer(buffer, 0, bytesPerIndexRecord, UpdateSequenceNumber);
            MultiSectorHelper.WriteUpdateSequenceArray(buffer, UpdateSequenceArrayOffset, updateSequenceArraySize, UpdateSequenceNumber, updateSequenceReplacementData);
            return buffer;
        }

        public bool IsParentNode
        {
            get
            {
                return IndexHeader.IsParentNode;
            }
        }
    }
}
