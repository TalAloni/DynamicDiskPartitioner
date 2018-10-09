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
    /// DIRTY_PAGE_ENTRY
    /// This record structure is compatible with NTFSRestartRecord v0.0
    /// This structure is NOT compatible with NTFSRestartRecord v1.0
    /// </summary>
    public class DirtyPageEntry : RestartTableEntry
    {
        public const int FixedLengthV0 = 0x24;
        public const int FixedLengthV1 = 0x20;

        private uint m_majorVersion;

        // uint AllocatedOrNextFree;
        public uint TargetAttribute;
        public uint LengthOfTransfer;
        // uint LcnsToFollow;
        public uint Reserved; // v0.0 only
        public ulong VCN;
        public ulong OldestLsn;
        public List<long> LCNsForPage = new List<long>();

        public DirtyPageEntry(uint majorVersion)
        {
            m_majorVersion = majorVersion;
        }

        public DirtyPageEntry(byte[] buffer, int offset, uint majorVersion)
        {
            m_majorVersion = majorVersion;

            AllocatedOrNextFree = LittleEndianReader.ReadUInt32(buffer, ref offset);
            TargetAttribute = LittleEndianReader.ReadUInt32(buffer, ref offset);
            LengthOfTransfer = LittleEndianReader.ReadUInt32(buffer, ref offset);
            uint lcnsToFollow = LittleEndianReader.ReadUInt32(buffer, ref offset);
            if (majorVersion == 0)
            {
                Reserved = LittleEndianReader.ReadUInt32(buffer, ref offset);
            }
            VCN = LittleEndianReader.ReadUInt64(buffer, ref offset);
            OldestLsn = LittleEndianReader.ReadUInt64(buffer, ref offset);
            for (int index = 0; index < lcnsToFollow; index++)
            {
                long lcn = (long)LittleEndianReader.ReadUInt64(buffer, ref offset);
                LCNsForPage.Add(lcn);
            }
        }

        public override void WriteBytes(byte[] buffer, int offset)
        {
            LittleEndianWriter.WriteUInt32(buffer, ref offset, AllocatedOrNextFree);
            LittleEndianWriter.WriteUInt32(buffer, ref offset, TargetAttribute);
            LittleEndianWriter.WriteUInt32(buffer, ref offset, LengthOfTransfer);
            LittleEndianWriter.WriteUInt32(buffer, ref offset, (uint)LCNsForPage.Count);
            if (m_majorVersion == 0)
            {
                LittleEndianWriter.WriteUInt32(buffer, ref offset, Reserved);
            }
            LittleEndianWriter.WriteUInt64(buffer, ref offset, VCN);
            LittleEndianWriter.WriteUInt64(buffer, ref offset, OldestLsn);
            for (int index = 0; index < LCNsForPage.Count; index++)
            {
                LittleEndianWriter.WriteInt64(buffer, ref offset, LCNsForPage[index]);
            }
        }

        public override int Length
        {
            get
            {
                if (m_majorVersion == 0)
                {
                    return FixedLengthV0 + LCNsForPage.Count * 8;
                }
                else
                {
                    return FixedLengthV1 + LCNsForPage.Count * 8;
                }
            }
        }
    }
}
