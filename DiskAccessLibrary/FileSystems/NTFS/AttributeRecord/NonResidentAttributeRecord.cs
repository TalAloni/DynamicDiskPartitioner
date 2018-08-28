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
    /// ATTRIBUTE_RECORD_HEADER: https://docs.microsoft.com/en-us/windows/desktop/DevNotes/attribute-record-header
    /// </summary>
    /// <remarks>
    /// The maximum NTFS file size is 2^64 bytes, so the number of file clusters can be represented using long.
    /// </remarks>
    public class NonResidentAttributeRecord : AttributeRecord
    {
        public const int HeaderLength = 0x40;

        public long LowestVCN;  // The lowest VCN covered by this attribute record, stored as unsigned, but is within the range of long, see note above.
        public long HighestVCN; // The highest VCN covered by this attribute record, stored as unsigned, but is within the range of long, see note above.
        // ushort mappingPairsOffset;
        public byte CompressionUnit;
        // 5 reserved bytes
        // ulong AllocatedLength;     // An even multiple of the cluster size (not valid if the LowestVCN member is nonzero).
        public ulong FileSize;        // The real size of a file with all of its runs combined (not valid if the LowestVCN member is nonzero).
        public ulong ValidDataLength; // Actual data written so far, (always less than or equal to the file size).
                                      // Data beyond ValidDataLength should be treated as 0 (not valid if the LowestVCN member is nonzero).
        // ulong TotalAllocated;      // Presented for the first file record of a compressed stream.

        private DataRunSequence m_dataRunSequence = new DataRunSequence();
        // Data run NULL terminator here
        // I've noticed that Windows Server 2003 puts 0x00 0x01 here for the $MFT FileRecord, seems to have no effect
        // (I've set it to 0x00 for the $MFT FileRecord in the MFT and the MFT mirror, and chkdsk did not report a problem.

        public NonResidentAttributeRecord(AttributeType attributeType, string name, ushort instance) : base(attributeType, name, false, instance)
        {
            m_dataRunSequence = new DataRunSequence();
            HighestVCN = -1; // This is the value that should be set when the attribute contain no data.
        }

        public NonResidentAttributeRecord(byte[] buffer, int offset) : base(buffer, offset)
        {
            LowestVCN = (long)LittleEndianConverter.ToUInt64(buffer, offset + 0x10);
            HighestVCN = (long)LittleEndianConverter.ToUInt64(buffer, offset + 0x18);
            ushort mappingPairsOffset = LittleEndianConverter.ToUInt16(buffer, offset + 0x20);
            CompressionUnit = ByteReader.ReadByte(buffer, offset + 0x22);
            ulong allocatedLength = LittleEndianConverter.ToUInt64(buffer, offset + 0x28);
            FileSize = LittleEndianConverter.ToUInt64(buffer, offset + 0x30);
            ValidDataLength = LittleEndianConverter.ToUInt64(buffer, offset + 0x38);

            int position = offset + mappingPairsOffset;
            while (position < offset + this.RecordLengthOnDisk)
            {
                DataRun run = new DataRun();
                int length = run.Read(buffer, position);
                position += length;

                // Length 1 means there was only a header byte (i.e. terminator)
                if (length == 1)
                {
                    break;
                }

                m_dataRunSequence.Add(run);
            }

            if ((HighestVCN - LowestVCN + 1) != m_dataRunSequence.DataClusterCount)
            {
                throw new InvalidDataException("Invalid non-resident attribute record");
            }
        }

        public override byte[] GetBytes(int bytesPerCluster)
        {
            int dataRunSequenceLength = m_dataRunSequence.RecordLength;
            ushort mappingPairsOffset = (ushort)(HeaderLength + Name.Length * 2);
            uint length = this.RecordLength;
            byte[] buffer = new byte[length];
            WriteHeader(buffer, HeaderLength);

            ulong allocatedLength = (ulong)(m_dataRunSequence.DataClusterCount * bytesPerCluster);
            ushort dataRunsOffset = (ushort)(HeaderLength + Name.Length * 2);

            LittleEndianWriter.WriteInt64(buffer, 0x10, LowestVCN);
            LittleEndianWriter.WriteInt64(buffer, 0x18, HighestVCN);
            LittleEndianWriter.WriteUInt16(buffer, 0x20, mappingPairsOffset);
            ByteWriter.WriteByte(buffer, 0x22, CompressionUnit);
            LittleEndianWriter.WriteUInt64(buffer, 0x28, allocatedLength);
            LittleEndianWriter.WriteUInt64(buffer, 0x30, FileSize);
            LittleEndianWriter.WriteUInt64(buffer, 0x38, ValidDataLength);

            int position = dataRunsOffset;
            foreach (DataRun run in m_dataRunSequence)
            { 
                byte[] runBytes = run.GetBytes();
                Array.Copy(runBytes, 0, buffer, position, runBytes.Length);
                position += runBytes.Length;
            }
            buffer[position] = 0; // Null termination

            return buffer;
        }

        /// <summary>
        /// This method should only be used for informational purposes.
        /// </summary>
        public KeyValuePairList<long, int> GetClustersInUse()
        {
            long clusterCount = HighestVCN - LowestVCN + 1;
            KeyValuePairList<long, int> sequence = m_dataRunSequence.TranslateToLCN(0, (int)clusterCount);
            return sequence;
        }

        /// <summary>
        /// When reading attributes, they may contain additional padding,
        /// so we should use RecordLengthOnDisk to advance the buffer position instead.
        /// </summary>
        public override uint RecordLength
        {
            get 
            {
                int dataRunSequenceLength = m_dataRunSequence.RecordLength;
                ushort mappingPairsOffset = (ushort)(HeaderLength + Name.Length * 2);
                uint length = (uint)(mappingPairsOffset + dataRunSequenceLength);
                // Each record is aligned to 8-byte boundary
                length = (uint)Math.Ceiling((double)length / 8) * 8;
                return length;
            }
        }

        public override ulong DataRealSize
        {
            get
            {
                return FileSize;
            }
        }

        public DataRunSequence DataRunSequence
        {
            get
            {
                return m_dataRunSequence;
            }
        }
        
        public long DataClusterCount
        {
            get
            {
                return HighestVCN - LowestVCN + 1;;
            }
        }
    }
}
