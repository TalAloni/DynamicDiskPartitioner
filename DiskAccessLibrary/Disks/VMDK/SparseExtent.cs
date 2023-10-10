/* Copyright (C) 2014-2023 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace DiskAccessLibrary.VMDK
{
    public partial class SparseExtent : DiskImage
    {
        private RawDiskImage m_file;
        private bool m_isReadOnly;
        private SparseExtentHeader m_header;
        private VirtualMachineDiskDescriptor m_descriptor;
        private uint? m_grainTableArrayStartSector;
        private uint? m_redundantGrainTableArrayStartSector;

        public SparseExtent(string path) : this(path, false)
        {
        }

        public SparseExtent(string path, bool isReadOnly) : base(path)
        {
            m_file = new RawDiskImage(path, VirtualMachineDisk.BytesPerDiskSector, isReadOnly);
            m_isReadOnly = isReadOnly;
            byte[] headerBytes = m_file.ReadSector(0);
            m_header = new SparseExtentHeader(headerBytes);
            if (!m_header.IsSupported)
            {
                throw new NotSupportedException("Sparse extent header version is not supported");
            }

            if (m_header.CompressionAlgirithm != SparseExtentCompression.None)
            {
                throw new NotSupportedException("Sparse extent compression is not supported");
            }

            if (m_header.DescriptorOffset > 0)
            {
                byte[] descriptorBytes = m_file.ReadSectors((long)m_header.DescriptorOffset, (int)m_header.DescriptorSize);
                string text = ASCIIEncoding.ASCII.GetString(descriptorBytes);
                List<string> lines = VirtualMachineDiskDescriptor.GetLines(text);
                m_descriptor = new VirtualMachineDiskDescriptor(lines);
            }
        }

        public override bool ExclusiveLock()
        {
            return m_file.ExclusiveLock();
        }

        public override bool ExclusiveLock(bool useOverlappedIO)
        {
            return m_file.ExclusiveLock(useOverlappedIO);
        }

        public override bool ReleaseLock()
        {
            return m_file.ReleaseLock();
        }

        private KeyValuePairList<long, int> MapSectors(long sectorIndex, int sectorCount, bool allocateUnmappedSectors)
        {
            if (m_grainTableArrayStartSector == null)
            {
                ulong grainTableOffset = m_header.GDOffset;
                byte[] grainDirectoryBytes = m_file.ReadSectors((long)grainTableOffset, 1);
                // We assume that the grain table array is consecutive and do not bother reading the entire grain directory
                m_grainTableArrayStartSector = LittleEndianConverter.ToUInt32(grainDirectoryBytes, 0);
            }

            long grainIndex = sectorIndex / (long)m_header.GrainSize;
            int grainTableEntriesPerSector = BytesPerSector / 4;
            long grainSectorIndexInTableArray = grainIndex / grainTableEntriesPerSector;
            int grainIndexInBuffer = (int)grainIndex % grainTableEntriesPerSector;
            int sectorsToReadFromTable = 1 + (int)Math.Ceiling((double)(sectorCount - (grainTableEntriesPerSector - grainIndexInBuffer)) / grainTableEntriesPerSector);
            byte[] grainTableBuffer = m_file.ReadSectors(m_grainTableArrayStartSector.Value + grainSectorIndexInTableArray, sectorsToReadFromTable);

            long sectorIndexInGrain = sectorIndex % (long)m_header.GrainSize;

            KeyValuePairList<long, int> result = new KeyValuePairList<long, int>();
            uint grainOffset = LittleEndianConverter.ToUInt32(grainTableBuffer, grainIndexInBuffer * 4);
            bool updateGrainTableArrays = false;
            if (grainOffset == 0 && allocateUnmappedSectors)
            {
                grainOffset = AllocateGrain();
                LittleEndianWriter.WriteUInt32(grainTableBuffer, grainIndexInBuffer * 4, grainOffset);
                updateGrainTableArrays = true;
            }
            grainOffset += (uint)sectorIndexInGrain;
            int sectorsLeft = sectorCount;
            int sectorsProcessedInGrain = (int)Math.Min(sectorsLeft, (long)m_header.GrainSize - sectorIndexInGrain);
            result.Add(grainOffset, sectorsProcessedInGrain);
            sectorsLeft -= sectorsProcessedInGrain;

            while (sectorsLeft > 0)
            {
                grainIndexInBuffer++;
                grainOffset = LittleEndianConverter.ToUInt32(grainTableBuffer, grainIndexInBuffer * 4);
                if (grainOffset == 0 && allocateUnmappedSectors)
                {
                    grainOffset = AllocateGrain();
                    LittleEndianWriter.WriteUInt32(grainTableBuffer, grainIndexInBuffer * 4, grainOffset);
                    updateGrainTableArrays = true;
                }
                sectorsProcessedInGrain = (int)Math.Min(sectorsLeft, (long)m_header.GrainSize);
                long lastSectorIndex = result[result.Count - 1].Key;
                int lastSectorCount = result[result.Count - 1].Value;
                if (lastSectorIndex + lastSectorCount == grainOffset)
                {
                    result[result.Count - 1] = new KeyValuePair<long, int>(lastSectorIndex, lastSectorCount + sectorsProcessedInGrain);
                }
                else
                {
                    result.Add(grainOffset, sectorsProcessedInGrain);
                }
                sectorsLeft -= sectorsProcessedInGrain;
            }

            // Allocate unallocated grains
            if (updateGrainTableArrays)
            {
                m_file.WriteSectors(m_grainTableArrayStartSector.Value + grainSectorIndexInTableArray, grainTableBuffer);

                if (m_header.HasRedundantGrainTable)
                {
                    if (m_redundantGrainTableArrayStartSector == null)
                    {
                        ulong redundantGrainTableOffset = m_header.RedundantGDOffset;
                        byte[] redundantGrainDirectoryBytes = m_file.ReadSectors((long)redundantGrainTableOffset, 1);
                        // We assume that the grain table array is consecutive and do not bother reading the entire grain directory
                        m_redundantGrainTableArrayStartSector = LittleEndianConverter.ToUInt32(redundantGrainDirectoryBytes, 0);
                    }
                    m_file.WriteSectors(m_redundantGrainTableArrayStartSector.Value + grainSectorIndexInTableArray, grainTableBuffer);
                }
            }

            return result;
        }

        private uint AllocateGrain()
        {
            uint grainOffet = (uint)m_file.TotalSectors;
            m_file.Extend((long)m_header.GrainSize * BytesPerSector);
            return grainOffet;
        }

        public override byte[] ReadSectors(long sectorIndex, int sectorCount)
        {
            CheckBoundaries(sectorIndex, sectorCount);
            byte[] result = new byte[sectorCount * this.BytesPerSector];
            int offset = 0;
            KeyValuePairList<long, int> map = MapSectors(sectorIndex, sectorCount, false);
            foreach (KeyValuePair<long, int> entry in map)
            {
                byte[] temp;
                if (entry.Key == 0) // 0 means that the grain is not yet allocated
                {
                    temp = new byte[entry.Value * this.BytesPerSector];
                }
                else
                {
                    temp = m_file.ReadSectors(entry.Key, entry.Value);
                }
                Array.Copy(temp, 0, result, offset, temp.Length);
                offset += temp.Length;
            }

            return result;
        }

        public override void WriteSectors(long sectorIndex, byte[] data)
        {
            if (m_isReadOnly)
            {
                throw new UnauthorizedAccessException("Attempted to perform write on a readonly disk");
            }

            int sectorCount = data.Length / BytesPerSector;
            CheckBoundaries(sectorIndex, sectorCount);

            KeyValuePairList<long, int> map = MapSectors(sectorIndex, sectorCount, true);
            int offset = 0;
            foreach (KeyValuePair<long, int> entry in map)
            {
                byte[] temp = new byte[entry.Value * BytesPerSector];
                Array.Copy(data, offset, temp, 0, temp.Length);
                m_file.WriteSectors(entry.Key, temp);
                offset += temp.Length;
            }
        }

        public override void Extend(long numberOfAdditionalBytes)
        {
            throw new NotImplementedException("The method or operation is not implemented.");
        }

        public override int BytesPerSector
        {
            get
            {
                return VirtualMachineDisk.BytesPerDiskSector;
            }
        }

        public override long Size
        {
            get
            {
                return (long)(m_header.Capacity * (ulong)this.BytesPerSector);
            }
        }

        public VirtualMachineDiskDescriptor Descriptor
        {
            get
            {
                return m_descriptor;
            }
        }
    }
}
