/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.IO;
using System.Collections.Generic;
using Utilities;
using DiskAccessLibrary.VHD;

namespace DiskAccessLibrary
{
    public partial class VirtualHardDisk
    {
        private byte[] ReadSectorsFromDynamicDisk(long sectorIndex, int sectorCount)
        {
            byte[] buffer = new byte[sectorCount * BytesPerDiskSector];
            int sectorOffset = 0;
            while (sectorOffset < sectorCount)
            {
                uint blockIndex = (uint)((sectorIndex + sectorOffset) * BytesPerDiskSector / m_dynamicHeader.BlockSize);
                int sectorOffsetInBlock = (int)(((sectorIndex + sectorOffset) * BytesPerDiskSector % m_dynamicHeader.BlockSize) / BytesPerDiskSector);
                int sectorsInBlock = (int)(m_dynamicHeader.BlockSize / BytesPerDiskSector);
                int sectorsRemainingInBlock = sectorsInBlock - sectorOffsetInBlock;
                int sectorsToRead = Math.Min(sectorCount - sectorOffset, sectorsRemainingInBlock);

                uint blockStartSector;
                if (m_blockAllocationTable.IsBlockInUse(blockIndex, out blockStartSector))
                {
                    // Each data block has a sector bitmap preceding the data, the bitmap is padded to a 512-byte sector boundary.
                    int blockBitmapSectorCount = (int)Math.Ceiling((double)sectorsInBlock / (BytesPerDiskSector * 8));
                    // "All sectors within a block whose corresponding bits in the bitmap are zero must contain 512 bytes of zero on disk"
                    byte[] temp = m_file.ReadSectors(blockStartSector + blockBitmapSectorCount + sectorOffsetInBlock, sectorsToRead);
                    ByteWriter.WriteBytes(buffer, sectorOffset * BytesPerDiskSector, temp);
                }
                sectorOffset += sectorsToRead;
            }
            return buffer;
        }

        private void WriteSectorsToDynamicDisk(long sectorIndex, byte[] data)
        {
            throw new NotImplementedException();
        }
    }
}
