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

        /* Index Record Header start*/
        // MULTI_SECTOR_HEADER
        public ulong LogFileSequenceNumber;
        public long RecordVCN; // Stored as unsigned, but is within the range of long
        /* Header */
        /* Index Header start */
        public uint EntriesOffset; // Relative to Index record header start offset
        public uint IndexLength;  // Including the Index record header
        public uint AllocatedLength; // Including the Index record header
        public bool HasChildren; // Level?
        // 3 zero bytes (padding)
        /* Index Header end */
        public ushort UpdateSequenceNumber;
        /* Index Record Header end*/

        public List<IndexNodeEntry> IndexEntries = new List<IndexNodeEntry>();
        public List<FileNameIndexEntry> FileNameEntries = new List<FileNameIndexEntry>();

        public IndexRecord(byte[] buffer, int offset)
        {
            MultiSectorHeader multiSectorHeader = new MultiSectorHeader(buffer, offset + 0x00);
            if (multiSectorHeader.Signature != ValidSignature)
            {
                throw new InvalidDataException("Invalid INDX record signature");
            }
            LogFileSequenceNumber = LittleEndianConverter.ToUInt64(buffer, offset + 0x08);
            RecordVCN = (long)LittleEndianConverter.ToUInt64(buffer, offset + 0x10);
            EntriesOffset = LittleEndianConverter.ToUInt32(buffer, offset + 0x18);
            IndexLength = LittleEndianConverter.ToUInt32(buffer, offset + 0x1C);
            AllocatedLength = LittleEndianConverter.ToUInt32(buffer, offset + 0x20);
            HasChildren = ByteReader.ReadByte(buffer, offset + 0x24) > 0;

            int position = offset + multiSectorHeader.UpdateSequenceArrayOffset;
            List<byte[]> updateSequenceReplacementData = MultiSectorHelper.ReadUpdateSequenceArray(buffer, position, multiSectorHeader.UpdateSequenceArraySize, out UpdateSequenceNumber);
            MultiSectorHelper.DecodeSegmentBuffer(buffer, offset, UpdateSequenceNumber, updateSequenceReplacementData);

            position = 0x18 + (int)EntriesOffset;
            if (HasChildren)
            {
                IndexNode node = new IndexNode(buffer, position);
                IndexEntries = node.Entries;
            }
            else
            {
                FileNameIndexLeafNode leaf = new FileNameIndexLeafNode(buffer, position);
                FileNameEntries = leaf.Entries;
            }
        }
    }
}
