/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    /// <remarks>
    /// IndexRoot attribute is always resident.
    /// </remarks>
    public class IndexRootRecord : ResidentAttributeRecord
    {
        public const string FileNameIndexName = "$I30";
 
        public AttributeType IndexedAttributeType; // FileName for directories
        public CollationRule CollationRule;
        public uint IndexAllocationEntryLength; // in bytes
        public byte ClustersPerIndexRecord;
        // 3 zero bytes
        public IndexHeader IndexHeader;
        // Index node
        
        public List<IndexNodeEntry> IndexEntries = new List<IndexNodeEntry>();
        public List<FileNameIndexEntry> FileNameEntries = new List<FileNameIndexEntry>();

        public IndexRootRecord(byte[] buffer, int offset) : base(buffer, offset)
        {
            IndexedAttributeType = (AttributeType)LittleEndianConverter.ToUInt32(this.Data, 0x00);
            CollationRule = (CollationRule)LittleEndianConverter.ToUInt32(this.Data, 0x04);
            IndexAllocationEntryLength = LittleEndianConverter.ToUInt32(this.Data, 0x08);
            ClustersPerIndexRecord = ByteReader.ReadByte(this.Data, 0x0C);
            // 3 zero bytes (padding to 8-byte boundary)
            IndexHeader = new IndexHeader(this.Data, 0x10);
            
            if (Name == FileNameIndexName)
            {
                int position = 0x10 + (int)IndexHeader.EntriesOffset;
                if (IsLargeIndex)
                {
                    IndexNode node = new IndexNode(this.Data, position);
                    IndexEntries = node.Entries;
                }
                else
                {
                    FileNameIndexLeafNode leaf = new FileNameIndexLeafNode(this.Data, position);
                    FileNameEntries = leaf.Entries;
                }
            }
        }

        public KeyValuePairList<MftSegmentReference, FileNameRecord> GetSmallIndexEntries()
        {
            if (IsLargeIndex)
            {
                throw new ArgumentException("Not a small index");
            }

            KeyValuePairList<MftSegmentReference, FileNameRecord> result = new KeyValuePairList<MftSegmentReference, FileNameRecord>();

            foreach (FileNameIndexEntry entry in FileNameEntries)
            {
                result.Add(entry.FileReference, entry.Record);
            }
            return result;
        }

        public bool IsLargeIndex
        {
            get
            {
                return (IndexHeader.IndexFlags & IndexHeaderFlags.LargeIndex) > 0;
            }
        }
    }
}
