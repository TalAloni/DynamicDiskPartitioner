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
    /// This class is used to read and update the volume bitmap (the $Bitmap metafile).
    /// Each bit in this file represents a cluster, extra bits are always set to 1.
    /// </summary>
    public class VolumeBitmap : NTFSFile
    {
        NTFSVolume m_volume;
        private long m_bufferedClusterVCN = -1;
        private byte[] m_bufferedCluster;
        private bool m_isBufferDirty; // if set to true, we need to write the buffer

        public VolumeBitmap(NTFSVolume volume) : base(volume, MasterFileTable.BitmapSegmentNumber)
        {
            m_volume = volume;
        }

        private byte[] GetBitmapCluster(long bitmapClusterVCN)
        {
            if (m_bufferedClusterVCN != bitmapClusterVCN)
            {
                if (m_isBufferDirty)
                {
                    FlushBuffer();
                }
                m_bufferedClusterVCN = bitmapClusterVCN;
                // VolumeBitmap data record is always non-resident (the NTFS v5.1 driver does not support a VolumeBitmap having a resident data record)
                m_bufferedCluster = FileRecord.NonResidentDataRecord.ReadDataCluster(Volume, bitmapClusterVCN);
            }
            return m_bufferedCluster;
        }
        
        private void FlushBuffer()
        {
            FileRecord.NonResidentDataRecord.WriteDataClusters(Volume, m_bufferedClusterVCN, m_bufferedCluster);
            m_isBufferDirty = false;
        }

        /// <param name="lcn">Logical cluster number</param>
        private bool IsClusterFree(long lcn)
        {
            long bitmapClusterVCN = (lcn / 8) / Volume.BytesPerCluster;
            byte[] bitmap = GetBitmapCluster(bitmapClusterVCN);

            int lcnByteOffset = (int)(((long)(lcn / 8)) % Volume.BytesPerCluster);
            int lcnBitIndex = (int)(lcn % 8);
            bool isInUse = ((bitmap[lcnByteOffset] >> lcnBitIndex) & 0x01) != 0;
            return !isInUse;
        }

        private void UpdateClusterStatus(long lcn, bool isUsed)
        {
            long bitmapClusterVCN = lcn / (8 * Volume.BytesPerCluster);

            // bitmap will reference m_bufferedCluster
            byte[] bitmap = GetBitmapCluster(bitmapClusterVCN);
            
            long clusterIndexInBitmap = lcn % (8 * Volume.BytesPerCluster);
            UpdateClusterStatus(bitmap, clusterIndexInBitmap, true);
            
            m_isBufferDirty = true;
        }

        public KeyValuePairList<long, long> AllocateClusters(long desiredStartLCN, long numberOfClusters)
        {
            KeyValuePairList<long, long> freeClusterRunList = FindClustersToAllocate(desiredStartLCN, numberOfClusters);
            // mark the clusters as used in the volume bitmap
            for (int index = 0; index < freeClusterRunList.Count; index++)
            {
                long runStartLCN = freeClusterRunList[index].Key;
                long runLength = freeClusterRunList[index].Value;
                for (uint position = 0; position < runLength; position++)
                {
                    UpdateClusterStatus(runStartLCN + position, true);
                }
            }
            if (m_isBufferDirty)
            {
                FlushBuffer();
            }
            return freeClusterRunList;
        }

        /// <summary>
        /// Return list of free cluster runs, key is cluster LCN, value is run length
        /// </summary>
        private KeyValuePairList<long, long> FindClustersToAllocate(long desiredStartLCN, long numberOfClusters)
        {
            KeyValuePairList<long, long> result = new KeyValuePairList<long, long>();

            long leftToAllocate;
            long endLCN = m_volume.Size / m_volume.BytesPerCluster - 1;
            KeyValuePairList<long, long> segment = FindClustersToAllocate(desiredStartLCN, endLCN, numberOfClusters, out leftToAllocate);
            result.AddRange(segment);

            if (leftToAllocate > 0 && desiredStartLCN > 0)
            {
                segment = FindClustersToAllocate(0, desiredStartLCN - 1, leftToAllocate, out leftToAllocate);
                result.AddRange(segment);
            }

            if (leftToAllocate > 0)
            {
                throw new Exception("Could not allocate clusters. Not enough free disk space");
            }

            return result;
        }

        /// <param name="clustersToAllocate">Number of clusters to allocate</param>
        private KeyValuePairList<long, long> FindClustersToAllocate(long startLCN, long endLCN, long clustersToAllocate, out long leftToAllocate)
        {
            KeyValuePairList<long, long> result = new KeyValuePairList<long, long>();
            leftToAllocate = clustersToAllocate;

            long runStartLCN = 0; // temporary
            long runLength = 0;
            long nextLCN = startLCN;
            while (nextLCN <= endLCN && leftToAllocate > 0)
            {
                if (IsClusterFree(nextLCN))
                {
                    if (runLength == 0)
                    {
                        runStartLCN = nextLCN;
                        runLength = 1;
                    }
                    else
                    {
                        runLength++;
                    }
                    leftToAllocate--;
                }
                else
                {
                    if (runLength > 0)
                    {
                        result.Add(runStartLCN, runLength);
                        runLength = 0;
                        // add this run
                    }
                }
                nextLCN++;
            }

            // add the last run
            if (runLength > 0)
            {
                result.Add(runStartLCN, runLength);
            }

            return result;
        }

        private byte CountNumberOfFreeClusters(byte bitmap)
        {
            byte result = 0;
            for (int bitIndex = 0; bitIndex < 8; bitIndex++)
            {
                bool isFree = ((bitmap >> bitIndex) & 0x01) == 0;
                if (isFree)
                {
                    result++;
                }
            }
            return result;
        }

        // Each bit in the $Bitmap file represents a cluster.
        // The size of the $Bitmap file is always a multiple of 8 bytes, extra bits are always set to 1.
        public long CountNumberOfFreeClusters()
        {
            int transferSizeInClusters = Settings.MaximumTransferSizeLBA / m_volume.SectorsPerCluster;
            ulong endLCN = (ulong)(m_volume.Size / m_volume.BytesPerCluster) - 1;
            long lastClusterVCN = ((long)(endLCN / 8)) / Volume.BytesPerCluster;
            
            long result = 0;

            // build lookup table
            byte[] lookup = new byte[256];
            for (int index = 0; index < 256; index++)
            {
                lookup[index] = CountNumberOfFreeClusters((byte)index);
            }

            // extra bits will be marked as used, so no need for special treatment
            for (long bitmapVcn = 0; bitmapVcn < lastClusterVCN; bitmapVcn += transferSizeInClusters)
            {
                byte[] bitmap = FileRecord.NonResidentDataRecord.ReadDataClusters(Volume, bitmapVcn, transferSizeInClusters);
                for (int index = 0; index < bitmap.Length; index++)
                {
                    result += lookup[bitmap[index]];
                }
            }

            return result;
        }

        public static void UpdateClusterStatus(byte[] bitmap, long clusterIndexInBitmap, bool isUsed)
        {
            long byteOffset = clusterIndexInBitmap / 8;
            int bitOffset = (int)(clusterIndexInBitmap % 8);
            if (isUsed)
            {
                bitmap[byteOffset] |= (byte)(0x01 << bitOffset);
            }
            else
            {
                bitmap[byteOffset] &= (byte)(~(0x01 << bitOffset));
            }
        }
    }
}
