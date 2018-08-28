using System;
using System.Collections.Generic;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    /// <summary>
    /// This class is used to read and modify the data of a non-resident attribute
    /// </summary>
    public class NonResidentAttributeData
    {
        private NTFSVolume m_volume;
        private NonResidentAttributeRecord m_record;

        public NonResidentAttributeData(NTFSVolume volume, NonResidentAttributeRecord attributeRecord)
        {
            m_volume = volume;
            m_record = attributeRecord;
        }

        /// <param name="clusterVCN">Cluster index</param>
        public byte[] ReadCluster(long clusterVCN)
        {
            return ReadClusters(clusterVCN, 1);
        }

        /// <param name="count">Maximum number of clusters to read</param>
        public byte[] ReadClusters(long firstClusterVCN, int count)
        {
            long lastClusterVcnToRead = firstClusterVCN + count - 1;
            if (firstClusterVCN < LowestVCN || firstClusterVCN > HighestVCN)
            {
                string message = String.Format("Cluster VCN {0}-{1} is not within the valid range ({2}-{3})", firstClusterVCN, lastClusterVcnToRead, LowestVCN, HighestVCN);
                throw new ArgumentOutOfRangeException(message);
            }

            if (lastClusterVcnToRead > HighestVCN)
            {
                lastClusterVcnToRead = HighestVCN;
            }

            byte[] result = new byte[count * m_volume.BytesPerCluster];
            ulong firstBytePosition = (ulong)firstClusterVCN * (uint)m_volume.BytesPerCluster;
            if (firstBytePosition < ValidDataLength)
            {
                KeyValuePairList<long, int> sequence = m_record.DataRunSequence.TranslateToLCN(firstClusterVCN - LowestVCN, count);
                long bytesRead = 0;
                foreach (KeyValuePair<long, int> run in sequence)
                {
                    byte[] clusters = m_volume.ReadClusters(run.Key, run.Value);
                    Array.Copy(clusters, 0, result, bytesRead, clusters.Length);
                    bytesRead += clusters.Length;
                }
            }

            int numberOfBytesToReturn = count * m_volume.BytesPerCluster;
            if (lastClusterVcnToRead == (long)HighestVCN)
            {
                numberOfBytesToReturn = (int)(FileSize - (ulong)firstClusterVCN * (uint)m_volume.BytesPerCluster);
                // If the last cluster is only partially used or we have been asked to read clusters beyond the last cluster, trim result.
                // (Either of those cases could only be true if we have just read the last cluster).
                if (numberOfBytesToReturn < result.Length)
                {
                    byte[] resultTrimmed = new byte[numberOfBytesToReturn];
                    Array.Copy(result, resultTrimmed, numberOfBytesToReturn);
                    result = resultTrimmed;
                }
            }

            if (firstBytePosition < ValidDataLength && ValidDataLength < firstBytePosition + (uint)numberOfBytesToReturn)
            {
                // Zero-out bytes outside ValidDataLength
                int numberOfValidBytesInResult = (int)(ValidDataLength - firstBytePosition);
                ByteWriter.WriteBytes(result, numberOfValidBytesInResult, new byte[result.Length - numberOfValidBytesInResult]);
            }

            return result;
        }

        public void WriteClusters(long firstClusterVCN, byte[] data)
        {
            int count;
            long lastClusterVcnToWrite;

            if (data.Length % m_volume.BytesPerCluster > 0)
            {
                int paddedLength = (int)Math.Ceiling((double)data.Length / m_volume.BytesPerCluster) * m_volume.BytesPerCluster;
                // last cluster could be partial, we must zero-fill it before write
                count = paddedLength / m_volume.BytesPerCluster;
                lastClusterVcnToWrite = firstClusterVCN + count - 1;
                if (lastClusterVcnToWrite == HighestVCN)
                {
                    byte[] paddedData = new byte[paddedLength];
                    Array.Copy(data, paddedData, data.Length);
                    data = paddedData;
                }
                else
                {
                    // only the last cluster can be partial
                    throw new ArgumentException("Cannot write partial cluster");
                }
            }
            else
            {
                count = data.Length / m_volume.BytesPerCluster;
                lastClusterVcnToWrite = firstClusterVCN + count - 1;
            }

            if (firstClusterVCN < LowestVCN || lastClusterVcnToWrite > HighestVCN)
            {
                string message = String.Format("Cluster VCN {0}-{1} is not within the valid range ({2}-{3})", firstClusterVCN, firstClusterVCN + count, LowestVCN, HighestVCN);
                throw new ArgumentOutOfRangeException(message);
            }

            ulong firstBytePosition = (ulong)firstClusterVCN * (uint)m_volume.BytesPerCluster;
            if (firstBytePosition > ValidDataLength)
            {
                // We need to zero-fill all the the bytes up to ValidDataLength
                long firstClusterToFillVCN = (long)(ValidDataLength / (uint)m_volume.BytesPerCluster);
                long clusterCount = firstClusterVCN - firstClusterToFillVCN;
                int transferSizeInClusters = Settings.MaximumTransferSizeLBA / m_volume.SectorsPerCluster;
                for (long clusterToFillVCN = firstClusterToFillVCN; clusterToFillVCN < firstClusterVCN; clusterToFillVCN += transferSizeInClusters)
                {
                    int clustersToWrite = (int)Math.Min(transferSizeInClusters, firstClusterVCN - firstClusterToFillVCN);
                    byte[] fillData = new byte[clustersToWrite * m_volume.BytesPerCluster];
                    if (clusterToFillVCN == firstClusterToFillVCN)
                    {
                        int bytesToRetain = (int)(ValidDataLength % (uint)m_volume.BytesPerCluster);
                        if (bytesToRetain > 0)
                        {
                            byte[] dataToRetain = ReadCluster(firstClusterToFillVCN);
                            Array.Copy(dataToRetain, 0, fillData, 0, bytesToRetain);
                        }
                    }
                    WriteClusters(clusterToFillVCN, fillData);
                }
            }

            KeyValuePairList<long, int> sequence = m_record.DataRunSequence.TranslateToLCN(firstClusterVCN, count);
            long bytesWritten = 0;
            foreach (KeyValuePair<long, int> run in sequence)
            {
                byte[] clusters = new byte[run.Value * m_volume.BytesPerCluster];
                Array.Copy(data, bytesWritten, clusters, 0, clusters.Length);
                m_volume.WriteClusters(run.Key, clusters);
                bytesWritten += clusters.Length;
            }

            if (firstBytePosition + (uint)data.Length > ValidDataLength)
            {
                m_record.ValidDataLength = firstBytePosition + (uint)data.Length;
            }
        }

        public byte[] ReadSectors(long firstSectorIndex, int count)
        {
            long firstClusterVcn = firstSectorIndex / m_volume.SectorsPerCluster;
            int sectorsToSkip = (int)(firstSectorIndex % m_volume.SectorsPerCluster);

            int clustersToRead = (int)Math.Ceiling((double)(count + sectorsToSkip) / m_volume.SectorsPerCluster);

            byte[] clusters = ReadClusters(firstClusterVcn, clustersToRead);
            byte[] result = new byte[count * m_volume.BytesPerSector];
            Array.Copy(clusters, sectorsToSkip * m_volume.BytesPerSector, result, 0, result.Length);
            return result;
        }

        public void WriteSectors(long firstSectorIndex, byte[] data)
        {
            int count = data.Length / m_volume.BytesPerSector;
            long firstClusterVcn = firstSectorIndex / m_volume.SectorsPerCluster;
            int sectorsToSkip = (int)(firstSectorIndex % m_volume.SectorsPerCluster);

            int clustersToRead = (int)Math.Ceiling((double)(count + sectorsToSkip) / m_volume.SectorsPerCluster);
            byte[] clusters = ReadClusters(firstClusterVcn, clustersToRead);
            Array.Copy(data, 0, clusters, sectorsToSkip * m_volume.BytesPerSector, data.Length);
            WriteClusters(firstClusterVcn, clusters);
        }

        public void Extend(ulong additionalLengthInBytes)
        {
            long numberOfClusters = (long)Math.Ceiling((double)FileSize / m_volume.BytesPerCluster);
            int freeBytesInLastCluster = (int)(numberOfClusters * m_volume.BytesPerCluster - (long)FileSize);

            if (additionalLengthInBytes > (uint)freeBytesInLastCluster)
            {
                ulong bytesToAllocate = additionalLengthInBytes - (uint)freeBytesInLastCluster;

                long clustersToAllocate = (long)Math.Ceiling((double)bytesToAllocate / m_volume.BytesPerCluster);
                if (clustersToAllocate > m_volume.NumberOfFreeClusters)
                {
                    throw new DiskFullException();
                }
                AllocateAdditionalClusters(clustersToAllocate);
            }

            m_record.FileSize += additionalLengthInBytes;
        }

        private void AllocateAdditionalClusters(long clustersToAllocate)
        {
            KeyValuePairList<long, long> freeClusterRunList;
            DataRunSequence dataRuns = m_record.DataRunSequence;
            if (dataRuns.Count == 0)
            {
                freeClusterRunList = m_volume.AllocateClusters(clustersToAllocate);
            }
            else
            {
                long desiredStartLCN = dataRuns.DataLastLCN + 1;
                freeClusterRunList = m_volume.AllocateClusters(desiredStartLCN, clustersToAllocate);

                long firstRunStartLCN = freeClusterRunList[0].Key;
                long firstRunLength = freeClusterRunList[0].Value;
                if (firstRunStartLCN == desiredStartLCN)
                {
                    // Merge with last run
                    DataRun lastRun = dataRuns[dataRuns.Count - 1];
                    lastRun.RunLength += firstRunLength;
                    m_record.HighestVCN += (long)firstRunLength;
                    freeClusterRunList.RemoveAt(0);
                }
            }

            for (int index = 0; index < freeClusterRunList.Count; index++)
            {
                long runStartLCN = freeClusterRunList[index].Key;
                long runLength = freeClusterRunList[index].Value;

                DataRun run = new DataRun();
                long previousLCN = m_record.DataRunSequence.LastDataRunStartLCN;
                run.RunOffset = runStartLCN - previousLCN;
                run.RunLength = runLength;
                m_record.HighestVCN += runLength;
                m_record.DataRunSequence.Add(run);
            }
        }

        public long LowestVCN
        {
            get
            {
                return m_record.LowestVCN;
            }
        }

        public long HighestVCN
        {
            get
            {
                return m_record.HighestVCN;
            }
        }

        public ulong FileSize
        {
            get
            {
                return m_record.FileSize;
            }
        }

        public ulong ValidDataLength
        {
            get
            {
                return m_record.ValidDataLength;
            }
        }

        public NonResidentAttributeRecord AttributeRecord
        {
            get
            {
                return m_record;
            }
        }
    }
}
