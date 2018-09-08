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
        public const int BytesPerIndexRecordBlock = 512;

        // MULTI_SECTOR_HEADER
        public ulong LogFileSequenceNumber;
        public long RecordVBN; // Stored as unsigned, but is within the range of long
        public IndexHeader IndexHeader;
        public ushort UpdateSequenceNumber;
        // byte[] UpdateSequenceReplacementData
        public List<IndexEntry> IndexEntries = new List<IndexEntry>();

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

        public bool IsParentNode
        {
            get
            {
                return IndexHeader.IsParentNode;
            }
        }
    }
}
