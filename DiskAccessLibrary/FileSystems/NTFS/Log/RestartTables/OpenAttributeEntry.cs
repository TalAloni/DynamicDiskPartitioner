/* Copyright (C) 2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
    /// <summary>
    /// OPEN_ATTRIBUTE_ENTRY
    /// This record structure is compatible with NTFSRestartRecord v0.0
    /// This structure is NOT compatible with NTFSRestartRecord v1.0
    /// </summary>
    public class OpenAttributeEntry : RestartTableEntry
    {
        public const int LengthV0 = 0x2C;
        public const int LengthV1 = 0x28;

        private const int FileReferenceOffsetV0 = 0x08;
        private const int LsnOfOpenRecordOffsetV0 = 0x10;
        private const int AttributeNamePresentOffsetV0 = 0x19;
        private const int AttributeTypeCodeOffsetV0 = 0x1C;
        private const int BytesPerIndexBufferOffsetV0 = 0x28;
        private const int BytesPerIndexBufferOffsetV1 = 0x04;
        private const int AttributeTypeCodeOffsetV1 = 0x08;
        private const int FileReferenceOffsetV1 = 0x10;
        private const int AttributeNamePresentOffsetV1 = 0x14;
        private const int LsnOfOpenRecordOffsetV1 = 0x18;
        
        private uint m_majorVersion;

        // uint AllocatedOrNextFree;
        public uint AttributeOffset; // v0.0: Self reference - Offset of the attribute in the open attribute table
        public MftSegmentReference FileReference;
        public ulong LsnOfOpenRecord;
        public bool DirtyPagesSeen;
        public bool AttributeNamePresent;
        // 2 reserved bytes
        public AttributeType AttributeTypeCode;
        public string AttributeName; // UNICODE_STRING
        public uint BytesPerIndexBuffer;

        public OpenAttributeEntry(uint majorVersion)
        {
            m_majorVersion = majorVersion;
        }

        public OpenAttributeEntry(byte[] buffer, int offset, uint majorVersion)
        {
            m_majorVersion = majorVersion;

            int fileReferenceOffset = (m_majorVersion == 0) ? FileReferenceOffsetV0 : FileReferenceOffsetV1;
            int lsnOfOpenRecordOffset = (m_majorVersion == 0) ? LsnOfOpenRecordOffsetV0 : LsnOfOpenRecordOffsetV1;
            int attributeNamePresentOffset = (m_majorVersion == 0) ? AttributeNamePresentOffsetV0 : AttributeNamePresentOffsetV1;
            int attributeTypeCodeOffset = (m_majorVersion == 0) ? AttributeTypeCodeOffsetV0 : AttributeTypeCodeOffsetV1;
            int bytesPerIndexBufferOffset = (m_majorVersion == 0) ? BytesPerIndexBufferOffsetV0 : BytesPerIndexBufferOffsetV1;

            AllocatedOrNextFree = LittleEndianConverter.ToUInt32(buffer, offset + 0x00);
            if (majorVersion == 0)
            {
                AttributeOffset = LittleEndianConverter.ToUInt32(buffer, offset + 0x04);
            }
            FileReference = new MftSegmentReference(buffer, offset + fileReferenceOffset);
            if (m_majorVersion > 0)
            {
                // v1.0 doubles down on the assumption that SegmentNumber is implemented as UInt32, and uses one of the upper two bytes for AttributeNamePresent.
                FileReference.SegmentNumber &= 0xFFFFFFFF;
            }
            LsnOfOpenRecord = LittleEndianConverter.ToUInt64(buffer, offset + lsnOfOpenRecordOffset);
            if (m_majorVersion == 0)
            {
                DirtyPagesSeen = Convert.ToBoolean(ByteReader.ReadByte(buffer, offset + 0x18));
            }
            AttributeNamePresent = Convert.ToBoolean(ByteReader.ReadByte(buffer, offset + attributeNamePresentOffset));
            AttributeTypeCode = (AttributeType)LittleEndianConverter.ToUInt32(buffer, offset + attributeTypeCodeOffset);
            if (AttributeNamePresent)
            {
                // IIUC located at 0x20 at both v0.0 and v1.0
                ushort length = LittleEndianConverter.ToUInt16(buffer, offset + 0x20);
                ushort maximumLength = LittleEndianConverter.ToUInt16(buffer, offset + 0x22);
                uint pointerToString = LittleEndianConverter.ToUInt32(buffer, offset + 0x24);
                throw new NotImplementedException();
            }

            if (AttributeTypeCode == AttributeType.IndexAllocation)
            {
                BytesPerIndexBuffer = LittleEndianConverter.ToUInt32(buffer, offset + bytesPerIndexBufferOffset);
            }
        }

        public override void WriteBytes(byte[] buffer, int offset)
        {
            int fileReferenceOffset = (m_majorVersion == 0) ? FileReferenceOffsetV0 : FileReferenceOffsetV1;
            int lsnOfOpenRecordOffset = (m_majorVersion == 0) ? LsnOfOpenRecordOffsetV0 : LsnOfOpenRecordOffsetV1;
            int attributeNamePresentOffset = (m_majorVersion == 0) ? AttributeNamePresentOffsetV0 : AttributeNamePresentOffsetV1;
            int attributeTypeCodeOffset = (m_majorVersion == 0) ? AttributeTypeCodeOffsetV0 : AttributeTypeCodeOffsetV1;
            int bytesPerIndexBufferOffset = (m_majorVersion == 0) ? BytesPerIndexBufferOffsetV0 : BytesPerIndexBufferOffsetV1;
            
            LittleEndianWriter.WriteUInt32(buffer, offset + 0x00, AllocatedOrNextFree);
            if (m_majorVersion == 0)
            {
                LittleEndianWriter.WriteUInt32(buffer, offset + 0x04, AttributeOffset);
            }
            FileReference.WriteBytes(buffer, offset + fileReferenceOffset);
            LittleEndianWriter.WriteUInt64(buffer, offset + lsnOfOpenRecordOffset, LsnOfOpenRecord);
            if (m_majorVersion == 0)
            {
                ByteWriter.WriteByte(buffer, offset + 0x18, Convert.ToByte(DirtyPagesSeen));
            }
            ByteWriter.WriteByte(buffer, offset + attributeNamePresentOffset, Convert.ToByte(AttributeNamePresent));
            LittleEndianWriter.WriteUInt32(buffer, offset + attributeTypeCodeOffset, (uint)AttributeTypeCode);
            if (AttributeNamePresent)
            {
                throw new NotImplementedException();
            }

            if (AttributeTypeCode == AttributeType.IndexAllocation)
            {
                LittleEndianWriter.WriteUInt32(buffer, offset + bytesPerIndexBufferOffset, BytesPerIndexBuffer);
            }
        }

        public override int Length
        {
            get
            {
                if (m_majorVersion == 0)
                {
                    return LengthV0;
                }
                else
                {
                    return LengthV1;
                }
            }
        }
    }
}
