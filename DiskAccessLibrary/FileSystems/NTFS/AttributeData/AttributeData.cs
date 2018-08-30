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
    /// <summary>
    /// This class provides a unified interface to access the data of an AttributeRecord.
    /// </summary>
    /// <remarks>
    /// The $Data attribute can be either resident or non-resident.
    /// </remarks>
    public class AttributeData
    {
        private NTFSVolume m_volume;
        private FileRecord m_fileRecord;
        private AttributeRecord m_attributeRecord;

        public AttributeData(NTFSVolume volume, FileRecord fileRecord, AttributeRecord attributeRecord)
        {
            m_volume = volume;
            m_fileRecord = fileRecord;
            m_attributeRecord = attributeRecord;
        }

        public byte[] ReadBytes(ulong offset, int length)
        {
            if (offset >= this.RealSize)
            {
                return new byte[0];
            }
            long clusterVCN = (long)(offset / (uint)m_volume.BytesPerCluster);
            int offsetInCluster = (int)(offset % (uint)m_volume.BytesPerCluster);
            int clusterCount = (int)Math.Ceiling((double)(offsetInCluster + length) / m_volume.BytesPerCluster);
            byte[] clustersBytes = ReadClusters(clusterVCN, clusterCount);
            int readLength = clustersBytes.Length - offsetInCluster;
            if (readLength < length)
            {
                length = readLength;
            }
            byte[] result = new byte[length];
            Array.Copy(clustersBytes, offsetInCluster, result, 0, length);
            return result;
        }

        public void WriteBytes(ulong offset, byte[] data)
        {
            ulong currentSize = this.RealSize;
            if (offset + (uint)data.Length > currentSize)
            {
                // Data needs to be extended
                ulong additionalLength = offset + (uint)data.Length - currentSize;
                Extend(additionalLength);
            }

            int position = 0;
            long clusterVCN = (long)(offset / (uint)m_volume.BytesPerCluster);
            int offsetInCluster = (int)(offset % (uint)m_volume.BytesPerCluster);
            if (offsetInCluster > 0)
            {
                int bytesLeftInCluster = m_volume.BytesPerCluster - offsetInCluster;
                int numberOfBytesToCopy = Math.Min(bytesLeftInCluster, data.Length);
                // Note: it's safe to send 'bytes' to ModifyCluster(), because it will ignore additional bytes after the first cluster
                ModifyCluster(clusterVCN, offsetInCluster, data);
                position += numberOfBytesToCopy;
                clusterVCN++;
            }

            while (position < data.Length)
            {
                int bytesLeft = data.Length - position;
                int numberOfBytesToCopy = Math.Min(m_volume.BytesPerCluster, bytesLeft);
                byte[] clusterBytes = new byte[numberOfBytesToCopy];
                Array.Copy(data, position, clusterBytes, 0, numberOfBytesToCopy);
                if (numberOfBytesToCopy < m_volume.BytesPerCluster)
                {
                    ModifyCluster(clusterVCN, 0, clusterBytes);
                }
                else
                {
                    WriteCluster(clusterVCN, clusterBytes);
                }
                clusterVCN++;
                position += clusterBytes.Length;
            }
            // ValidDataLength might have changed
            m_volume.MasterFileTable.UpdateFileRecord(m_fileRecord);
        }

        /// <summary>
        /// Will read cluster and then modify the given bytes
        /// </summary>
        private void ModifyCluster(long clusterVCN, int offsetInCluster, byte[] data)
        {
            int bytesLeftInCluster = m_volume.BytesPerCluster - offsetInCluster;
            int numberOfBytesToCopy = Math.Min(bytesLeftInCluster, data.Length);

            byte[] clusterBytes = ReadCluster(clusterVCN);
            // last cluster could be partial
            if (clusterBytes.Length < offsetInCluster + numberOfBytesToCopy)
            {
                byte[] temp = new byte[offsetInCluster + numberOfBytesToCopy];
                Array.Copy(clusterBytes, temp, clusterBytes.Length);
                clusterBytes = temp;
            }

            Array.Copy(data, 0, clusterBytes, offsetInCluster, numberOfBytesToCopy);
            WriteCluster(clusterVCN, clusterBytes);
        }

        public byte[] ReadCluster(long clusterVCN)
        {
            return ReadClusters(clusterVCN, 1);
        }

        public byte[] ReadClusters(long clusterVCN, int count)
        {
            if (m_attributeRecord is NonResidentAttributeRecord)
            {
                NonResidentAttributeData attributeData = new NonResidentAttributeData(m_volume, (NonResidentAttributeRecord)m_attributeRecord);
                return attributeData.ReadClusters(clusterVCN, count);
            }
            else
            {
                long numberOfClusters = (long)Math.Ceiling((double)((ResidentAttributeRecord)m_attributeRecord).Data.Length / m_volume.BytesPerCluster);
                long highestVCN = Math.Max(numberOfClusters - 1, 0);
                if (clusterVCN < 0 || clusterVCN > highestVCN)
                {
                    throw new ArgumentOutOfRangeException("Cluster VCN is not within the valid range");
                }

                long offset = clusterVCN * m_volume.BytesPerCluster;
                int bytesToRead;
                // last cluster could be partial
                if (clusterVCN + count < numberOfClusters)
                {
                    bytesToRead = count * m_volume.BytesPerCluster;   
                }
                else
                {
                    bytesToRead = (int)(((ResidentAttributeRecord)m_attributeRecord).Data.Length - offset);
                }
                byte[] data = new byte[bytesToRead];
                Array.Copy(((ResidentAttributeRecord)m_attributeRecord).Data, offset, data, 0, bytesToRead);
                return data;
            }
        }

        public void WriteCluster(long clusterVCN, byte[] data)
        {
            WriteClusters(clusterVCN, data);
        }

        public void WriteClusters(long clusterVCN, byte[] data)
        {
            if (m_attributeRecord is NonResidentAttributeRecord)
            {
                NonResidentAttributeData attributeData = new NonResidentAttributeData(m_volume, (NonResidentAttributeRecord)m_attributeRecord);
                attributeData.WriteClusters(clusterVCN, data);
            }
            else
            {
                int numberOfClusters = (int)Math.Ceiling((double)((ResidentAttributeRecord)m_attributeRecord).Data.Length / m_volume.BytesPerCluster);
                int count = (int)Math.Ceiling((double)data.Length / m_volume.BytesPerCluster);
                long highestVCN = Math.Max(numberOfClusters - 1, 0);
                if (clusterVCN < 0 || clusterVCN > highestVCN)
                {
                    throw new ArgumentOutOfRangeException("Cluster VCN is not within the valid range");
                }

                long offset = clusterVCN * m_volume.BytesPerCluster;
                Array.Copy(data, 0, ((ResidentAttributeRecord)m_attributeRecord).Data, offset, data.Length);
            }
        }

        public byte[] ReadSectors(long firstSectorIndex, int count)
        {
            if (m_attributeRecord is NonResidentAttributeRecord)
            {
                NonResidentAttributeData attributeData = new NonResidentAttributeData(m_volume, (NonResidentAttributeRecord)m_attributeRecord);
                return attributeData.ReadSectors(firstSectorIndex, count);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public void WriteSectors(long firstSectorIndex, byte[] data)
        {
            if (m_attributeRecord is NonResidentAttributeRecord)
            {
                NonResidentAttributeData attributeData = new NonResidentAttributeData(m_volume, (NonResidentAttributeRecord)m_attributeRecord);
                attributeData.WriteSectors(firstSectorIndex, data);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public void Extend(ulong additionalLengthInBytes)
        {
            ulong currentSize = this.RealSize;
            if (m_attributeRecord is NonResidentAttributeRecord)
            {
                NonResidentAttributeData attributeData = new NonResidentAttributeData(m_volume, (NonResidentAttributeRecord)m_attributeRecord);
                attributeData.Extend(additionalLengthInBytes);
            }
            else
            {
                byte[] data = ((ResidentAttributeRecord)m_attributeRecord).Data;
                ulong finalDataLength = (uint)data.Length + additionalLengthInBytes;
                if (finalDataLength >= (ulong)m_volume.MasterFileTable.AttributeDataLengthToMakeNonResident)
                {
                    // Convert the attribute to non-resident
                    long clustersToAllocate = (long)Math.Ceiling((double)finalDataLength / m_volume.BytesPerCluster);
                    if (clustersToAllocate > m_volume.NumberOfFreeClusters)
                    {
                        throw new DiskFullException();
                    }
                    m_fileRecord.RemoveAttributeRecord(m_attributeRecord.AttributeType, m_attributeRecord.Name);
                    NonResidentAttributeRecord attributeRecord = new NonResidentAttributeRecord(m_attributeRecord.AttributeType, m_attributeRecord.Name, m_attributeRecord.Instance);
                    NonResidentAttributeData attributeData = new NonResidentAttributeData(m_volume, attributeRecord);
                    attributeData.Extend(finalDataLength);
                    attributeData.WriteClusters(0, data);
                    m_attributeRecord = attributeRecord;
                    m_fileRecord.Attributes.Add(m_attributeRecord);
                }
                else
                {
                    int currentLength = data.Length;
                    byte[] temp = new byte[currentLength + (int)additionalLengthInBytes];
                    Array.Copy(data, temp, data.Length);
                    ((ResidentAttributeRecord)m_attributeRecord).Data = temp;
                }
            }
            m_volume.MasterFileTable.UpdateFileRecord(m_fileRecord);
        }

        public ulong AllocatedSize
        {
            get
            {
                if (m_attributeRecord is NonResidentAttributeRecord)
                {
                    return (ulong)(((NonResidentAttributeRecord)m_attributeRecord).DataClusterCount * m_volume.BytesPerCluster);
                }
                else
                {
                    return (ulong)((ResidentAttributeRecord)m_attributeRecord).Data.Length;
                }
            }
        }

        public ulong RealSize
        {
            get
            {
                if (m_attributeRecord is NonResidentAttributeRecord)
                {
                    return ((NonResidentAttributeRecord)m_attributeRecord).FileSize;
                }
                else
                {
                    return (ulong)((ResidentAttributeRecord)m_attributeRecord).Data.Length;
                }
            }
        }

        public long ClusterCount
        {
            get
            {
                if (m_attributeRecord is NonResidentAttributeRecord)
                {
                    return ((NonResidentAttributeRecord)m_attributeRecord).DataClusterCount;
                }
                else
                {
                    // A resident AttributeRecord could span several clusters
                    int dataLength = ((ResidentAttributeRecord)m_attributeRecord).Data.Length;
                    return (long)Math.Ceiling((double)dataLength / m_volume.BytesPerCluster);
                }
            }
        }

        public AttributeRecord AttributeRecord
        {
            get
            {
                return m_attributeRecord;
            }
        }
    }
}
