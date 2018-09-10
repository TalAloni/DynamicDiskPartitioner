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
    /// <summary>
    /// FILE_RECORD_SEGMENT_HEADER: https://msdn.microsoft.com/de-de/windows/desktop/bb470124
    /// </summary>
    /// <remarks>
    /// Attributes MUST be ordered by increasing attribute type code when written to disk.
    /// </remarks>
    public class FileRecordSegment
    {
        public const string ValidSignature = "FILE";
        public const int EndMarkerLength = 4;
        public const int NTFS30UpdateSequenceArrayOffset = 0x2A; // NTFS v3.0 and earlier (up to Windows 2000)
        public const int NTFS31UpdateSequenceArrayOffset = 0x30; // NTFS v3.1 and later   (XP and later)

        /* Start of header */
        // MULTI_SECTOR_HEADER
        public ulong LogFileSequenceNumber;
        public ushort SequenceNumber; // This value is incremented each time that a file record segment is freed
        public ushort ReferenceCount;
        // ushort FirstAttributeOffset;
        private FileRecordFlags m_flags;
        // uint SegmentLength; // FirstFreeByte
        // uint SegmentAllocatedLength; // BytesAvailable
        private MftSegmentReference m_baseFileRecordSegment; // If this is the base file record, the value is 0
        public ushort NextAttributeInstance; // Starting from 0
        // 2 bytes padding
        public uint MftSegmentNumberOnDisk; // Self-reference, NTFS v3.0+
        public ushort UpdateSequenceNumber; // a.k.a. USN
        // byte[] UpdateSequenceReplacementData
        /* End of header */
        private List<AttributeRecord> m_immediateAttributes = new List<AttributeRecord>(); // Attribute records that are stored in the base file record

        private long m_mftSegmentNumber; // We use our own segment number to support NTFS v3.0 (note that MftSegmentNumberOnDisk is UInt32, which is another reason to avoid it)

        public FileRecordSegment(long segmentNumber) : this(segmentNumber, new MftSegmentReference(0, 0))
        {
        }

        public FileRecordSegment(long segmentNumber, MftSegmentReference baseFileRecordSegment)
        {
            m_mftSegmentNumber = segmentNumber;
            m_baseFileRecordSegment = baseFileRecordSegment;
        }

        public FileRecordSegment(byte[] buffer, int bytesPerSector, long segmentNumber) : this(buffer, 0, bytesPerSector, segmentNumber)
        {
        }

        public FileRecordSegment(byte[] buffer, int offset, int bytesPerSector, long segmentNumber)
        {
            MultiSectorHeader multiSectorHeader = new MultiSectorHeader(buffer, offset + 0x00);
            if (multiSectorHeader.Signature != ValidSignature)
            {
                throw new InvalidDataException("Invalid FILE record signature");
            }
            LogFileSequenceNumber = LittleEndianConverter.ToUInt64(buffer, offset + 0x08);
            SequenceNumber = LittleEndianConverter.ToUInt16(buffer, offset + 0x10);
            ReferenceCount = LittleEndianConverter.ToUInt16(buffer, offset + 0x12);
            ushort firstAttributeOffset = LittleEndianConverter.ToUInt16(buffer, offset + 0x14);
            m_flags = (FileRecordFlags)LittleEndianConverter.ToUInt16(buffer, offset + 0x16);
            uint segmentLength = LittleEndianConverter.ToUInt32(buffer, offset + 0x18);
            uint segmentAllocatedLength = LittleEndianConverter.ToUInt32(buffer, offset + 0x1C);
            m_baseFileRecordSegment = new MftSegmentReference(buffer, offset + 0x20);
            NextAttributeInstance = LittleEndianConverter.ToUInt16(buffer, offset + 0x28);
            // 2 bytes padding
            MftSegmentNumberOnDisk = LittleEndianConverter.ToUInt32(buffer, offset + 0x2C);

            int position = offset + multiSectorHeader.UpdateSequenceArrayOffset;
            List<byte[]> updateSequenceReplacementData = MultiSectorHelper.ReadUpdateSequenceArray(buffer, position, multiSectorHeader.UpdateSequenceArraySize, out UpdateSequenceNumber);
            MultiSectorHelper.DecodeSegmentBuffer(buffer, offset, UpdateSequenceNumber, updateSequenceReplacementData);

            // Read attributes
            position = offset + firstAttributeOffset;
            while (!IsEndMarker(buffer, position))
            {
                AttributeRecord attribute = AttributeRecord.FromBytes(buffer, position);
                
                m_immediateAttributes.Add(attribute);
                position += (int)attribute.RecordLengthOnDisk;
                if (position > buffer.Length)
                {
                    throw new InvalidDataException("Invalid attribute length");
                }
            }

            m_mftSegmentNumber = segmentNumber;
        }

        /// <param name="segmentLength">This refers to the maximum length of FileRecord as defined in the Volume's BootRecord</param>
        public byte[] GetBytes(int bytesPerFileRecordSegment, int bytesPerCluster, ushort minorNTFSVersion)
        {
            int strideCount = bytesPerFileRecordSegment / MultiSectorHelper.BytesPerStride;
            ushort updateSequenceArraySize = (ushort)(1 + strideCount);

            ushort updateSequenceArrayOffset;
            if (minorNTFSVersion == 0)
            {
                updateSequenceArrayOffset = NTFS30UpdateSequenceArrayOffset;
            }
            else
            {
                updateSequenceArrayOffset = NTFS31UpdateSequenceArrayOffset;
            }

            MultiSectorHeader multiSectorHeader = new MultiSectorHeader(ValidSignature, updateSequenceArrayOffset, updateSequenceArraySize);
            ushort firstAttributeOffset = GetFirstAttributeOffset(bytesPerFileRecordSegment, minorNTFSVersion);

            byte[] buffer = new byte[bytesPerFileRecordSegment];
            multiSectorHeader.WriteBytes(buffer, 0x00);
            LittleEndianWriter.WriteUInt64(buffer, 0x08, LogFileSequenceNumber);
            LittleEndianWriter.WriteUInt16(buffer, 0x10, SequenceNumber);
            LittleEndianWriter.WriteUInt16(buffer, 0x12, ReferenceCount);
            LittleEndianWriter.WriteUInt16(buffer, 0x14, firstAttributeOffset);
            LittleEndianWriter.WriteUInt16(buffer, 0x16, (ushort)m_flags);

            LittleEndianWriter.WriteInt32(buffer, 0x1C, bytesPerFileRecordSegment);
            m_baseFileRecordSegment.WriteBytes(buffer, 0x20);
            LittleEndianWriter.WriteUInt16(buffer, 0x28, NextAttributeInstance);
            if (minorNTFSVersion == 1)
            {
                LittleEndianWriter.WriteUInt32(buffer, 0x2C, MftSegmentNumberOnDisk);
            }

            // write attributes
            int position = firstAttributeOffset;
            foreach (AttributeRecord attribute in m_immediateAttributes)
            {
                byte[] attributeBytes = attribute.GetBytes(bytesPerCluster);
                ByteWriter.WriteBytes(buffer, position, attributeBytes);
                position += attributeBytes.Length;
            }

            byte[] marker = GetEndMarker();
            ByteWriter.WriteBytes(buffer, position, marker);
            position += marker.Length;
            position += 4; // record (length) is aligned to 8-byte boundary

            uint segmentLength = (uint)position;
            LittleEndianWriter.WriteUInt32(buffer, 0x18, segmentLength);

            // Write UpdateSequenceNumber and UpdateSequenceReplacementData
            List<byte[]> updateSequenceReplacementData = MultiSectorHelper.EncodeSegmentBuffer(buffer, 0, bytesPerFileRecordSegment, UpdateSequenceNumber);
            MultiSectorHelper.WriteUpdateSequenceArray(buffer, updateSequenceArrayOffset, updateSequenceArraySize, UpdateSequenceNumber, updateSequenceReplacementData);
            return buffer;
        }

        public AttributeRecord GetImmediateAttributeRecord(AttributeType type)
        {
            foreach (AttributeRecord attribute in m_immediateAttributes)
            {
                if (attribute.AttributeType == type)
                {
                    return attribute;
                }
            }

            return null;
        }

        /// <summary>
        /// Indicates that the file / directory wasn't deleted
        /// </summary>
        public bool IsInUse
        {
            get
            {
                return (m_flags & FileRecordFlags.InUse) != 0;
            }
            set
            {
                if (value)
                {
                    m_flags |= FileRecordFlags.InUse;
                }
                else
                {
                    m_flags &= ~FileRecordFlags.InUse;
                }
            }
        }

        public bool IsDirectory
        {
            get
            {
                return (m_flags & FileRecordFlags.IsDirectory) != 0;
            }
            set
            {
                if (value)
                {
                    m_flags |= FileRecordFlags.IsDirectory;
                }
                else
                {
                    m_flags &= FileRecordFlags.IsDirectory;
                }
            }
        }
        
        public List<AttributeRecord> ImmediateAttributes
        {
            get
            {
                return m_immediateAttributes;
            }
        }

        /// <remarks>
        /// If this is the base file record, the BaseFileRecordSegment value is 0.
        /// https://docs.microsoft.com/en-us/windows/desktop/DevNotes/file-record-segment-header
        /// </remarks>
        public bool IsBaseFileRecord
        {
            get
            {
                return (m_baseFileRecordSegment.SegmentNumber == 0 && m_baseFileRecordSegment.SequenceNumber == 0);
            }
        }

        public bool HasAttributeList
        {
            get
            {
                AttributeRecord attributeList = GetImmediateAttributeRecord(AttributeType.AttributeList);
                return (attributeList != null);
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is FileRecordSegment)
            {
                return ((FileRecordSegment)obj).MftSegmentNumber == MftSegmentNumber;
            }
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return MftSegmentNumber.GetHashCode();
        }

        public long MftSegmentNumber
        {
            get
            {
                return m_mftSegmentNumber;
            }
        }

        public static bool IsEndMarker(byte[] buffer, int offset)
        {
            uint type = LittleEndianConverter.ToUInt32(buffer, offset + 0x00);
            return (type == 0xFFFFFFFF);
        }

        /// <summary>
        /// Get file record end marker
        /// </summary>
        public static byte[] GetEndMarker()
        {
            byte[] buffer = new byte[4];
            LittleEndianWriter.WriteUInt32(buffer, 0, 0xFFFFFFFF);
            return buffer;
        }

        public static ushort GetFirstAttributeOffset(int segmentLength, ushort minorNTFSVersion)
        {
            int strideCount = segmentLength / MultiSectorHelper.BytesPerStride;
            ushort updateSequenceArraySize = (ushort)(1 + strideCount);

            ushort updateSequenceArrayOffset;
            if (minorNTFSVersion == 0)
            {
                updateSequenceArrayOffset = NTFS30UpdateSequenceArrayOffset;
            }
            else
            {
                updateSequenceArrayOffset = NTFS31UpdateSequenceArrayOffset;
            }

            // Aligned to 8 byte boundary
            // Note: I had an issue with 4 byte boundary under Windows 7 using disk with 2048 bytes per sector.
            //       Windows used an 8 byte boundary.
            ushort firstAttributeOffset = (ushort)(Math.Ceiling((double)(updateSequenceArrayOffset + updateSequenceArraySize * 2) / 8) * 8);
            return firstAttributeOffset;
        }

        public static bool ContainsFileRecordSegment(byte[] recordBytes)
        {
            return ContainsFileRecordSegment(recordBytes, 0);
        }

        public static bool ContainsFileRecordSegment(byte[] recordBytes, int offset)
        {
            string fileSignature = ByteReader.ReadAnsiString(recordBytes, offset, 4);
            return (fileSignature == ValidSignature);
        }

        public static bool ContainsMftSegmentNumber(List<FileRecordSegment> list, long mftSegmentNumber)
        {
            foreach (FileRecordSegment segment in list)
            {
                if (segment.MftSegmentNumber == mftSegmentNumber)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
