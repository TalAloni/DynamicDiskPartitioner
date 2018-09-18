/* Copyright (C) 2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    /// <summary>
    /// This class is used to read and update the bitmap of a Bitmap attribute (e.g. the MFT bitmap where each bit represents a FileRecord).
    /// Windows extends the MFT bitmap in multiple of 8 bytes, so the number of bits may be greater than the record count.
    /// </summary>
    /// <remarks>
    /// The Bitmap attribute can be either resident or non-resident.
    /// </remarks>
    public class BitmapData : AttributeData
    {
        private const int ExtendGranularity = 8; // The number of bytes added to the bitmap when extending it, MUST be multiple of 8.

        private long m_searchStartIndex = 0;
        private long m_numberOfUsableBits;

        public BitmapData(NTFSVolume volume, FileRecord fileRecord, AttributeRecord attributeRecord, long numberOfUsableBits) : base(volume, fileRecord, attributeRecord)
        {
            m_numberOfUsableBits = numberOfUsableBits;
        }

        /// <remarks>
        /// .
        /// </remarks>
        /// <returns>Record index</returns>
        public long? AllocateRecord()
        {
            long? recordIndex = AllocateRecord(m_searchStartIndex);
            if (recordIndex.HasValue)
            {
                m_searchStartIndex = recordIndex.Value + 1;
            }
            else
            {
                recordIndex = AllocateRecord(0, m_searchStartIndex - 1);
                if (recordIndex.HasValue)
                {
                    m_searchStartIndex = recordIndex.Value + 1;
                }
            }
            return recordIndex;
        }

        /// <returns>Record index</returns>
        public long? AllocateRecord(long searchStartIndex)
        {
            return AllocateRecord(searchStartIndex, m_numberOfUsableBits - 1);
        }

        /// <returns>Record index</returns>
        public long? AllocateRecord(long searchStartIndex, long searchEndIndex)
        {
            long bufferedVCN = -1;
            byte[] bufferedClusterBytes = null;

            for (long index = searchStartIndex; index <= searchEndIndex; index++)
            {
                long currentVCN = index / (Volume.BytesPerCluster * 8);
                if (currentVCN != bufferedVCN)
                {
                    bufferedClusterBytes = ReadCluster(currentVCN);
                    bufferedVCN = currentVCN;
                }

                int bitOffsetInBitmap = (int)(index % (Volume.BytesPerCluster * 8));
                if (IsBitClear(bufferedClusterBytes, bitOffsetInBitmap))
                {
                    SetBit(bufferedClusterBytes, bitOffsetInBitmap);
                    WriteCluster(currentVCN, bufferedClusterBytes);
                    return index;
                }
            }

            return null;
        }

        public void DeallocateRecord(long recordIndex)
        {
            long currentVCN = recordIndex / (Volume.BytesPerCluster * 8);
            int bitOffsetInBitmap = (int)(recordIndex % (Volume.BytesPerCluster * 8));
            byte[] bitmap = ReadCluster(currentVCN);
            ClearBit(bitmap, bitOffsetInBitmap);
            WriteCluster(currentVCN, bitmap);
        }

        public void ExtendBitmap(long numberOfBits)
        {
            long numberOfUnusedBits = (long)(this.Length * 8 - (ulong)m_numberOfUsableBits);
            if (numberOfBits > numberOfUnusedBits)
            {
                long additionalBits = numberOfBits - numberOfUnusedBits;
                ulong additionalBytes = (ulong)Math.Ceiling((double)additionalBits / (ExtendGranularity * 8)) * 8;
                this.Extend(additionalBytes);
            }
            m_numberOfUsableBits += numberOfBits;
        }

        public void TruncateBitmap(long newLengthInBits)
        {
            m_numberOfUsableBits = newLengthInBits;
            ulong newLengthInBytes = (ulong)Math.Ceiling((double)newLengthInBits / (ExtendGranularity * 8)) * 8;
            this.Truncate(newLengthInBytes);
        }

        private static bool IsBitClear(byte[] bitmap, int bitOffsetInBitmap)
        {
            int byteOffset = bitOffsetInBitmap / 8;
            int bitOffsetInByte = bitOffsetInBitmap % 8;
            bool isInUse = ((bitmap[byteOffset] >> bitOffsetInByte) & 0x01) != 0;
            return !isInUse;
        }

        private static void SetBit(byte[] bitmap, int bitOffsetInBitmap)
        {
            int byteOffset = bitOffsetInBitmap / 8;
            int bitOffsetInByte = bitOffsetInBitmap % 8;
            bitmap[byteOffset] |= (byte)(0x01 << bitOffsetInByte);
        }

        private static void ClearBit(byte[] bitmap, int bitOffsetInBitmap)
        {
            int byteOffset = bitOffsetInBitmap / 8;
            int bitOffsetInByte = bitOffsetInBitmap % 8;
            bitmap[byteOffset] &= (byte)(~(0x01 << bitOffsetInByte));
        }

        public long NumberOfUsableBits
        {
            get
            {
                return m_numberOfUsableBits;
            }
        }
    }
}
