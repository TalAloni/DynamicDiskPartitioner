/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
        public const int FixedLength = 32;
 
        public AttributeType IndexedAttributeType; // FileName for directories
        public CollationRule CollationRule;
        public uint IndexAllocationEntryLength; // in bytes
        public byte ClustersPerIndexRecord;
        // 3 zero bytes
        public IndexHeader IndexHeader;
        public List<IndexEntry> IndexEntries = new List<IndexEntry>();
        
        public IndexRootRecord(byte[] buffer, int offset) : base(buffer, offset)
        {
            IndexedAttributeType = (AttributeType)LittleEndianConverter.ToUInt32(this.Data, 0x00);
            CollationRule = (CollationRule)LittleEndianConverter.ToUInt32(this.Data, 0x04);
            IndexAllocationEntryLength = LittleEndianConverter.ToUInt32(this.Data, 0x08);
            ClustersPerIndexRecord = ByteReader.ReadByte(this.Data, 0x0C);
            // 3 zero bytes (padding to 8-byte boundary)
            IndexHeader = new IndexHeader(this.Data, 0x10);

            int entriesOffset = 0x10 + (int)IndexHeader.EntriesOffset;
            IndexEntries = IndexEntry.ReadIndexEntries(this.Data, entriesOffset);
        }

        public override byte[] GetBytes(int bytesPerCluster)
        {
            int dataLength = FixedLength + IndexEntry.GetLength(IndexEntries);
            this.Data = new byte[dataLength];
            LittleEndianWriter.WriteUInt32(this.Data, 0x00, (uint)IndexedAttributeType);
            LittleEndianWriter.WriteUInt32(this.Data, 0x04, (uint)CollationRule);
            LittleEndianWriter.WriteUInt32(this.Data, 0x08, (uint)IndexAllocationEntryLength);
            ByteWriter.WriteByte(this.Data, 0x0C, ClustersPerIndexRecord);
            IndexHeader.WriteBytes(this.Data, 0x10);
            IndexEntry.WriteIndexEntries(this.Data, 0x20, IndexEntries);
            return base.GetBytes(bytesPerCluster);
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
