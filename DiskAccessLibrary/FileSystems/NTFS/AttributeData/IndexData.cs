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
    public partial class IndexData
    {
        private NTFSVolume m_volume;
        private FileRecord m_fileRecord;
        private AttributeType m_indexedAttributeType; // Type of the attribute being indexed
        private string m_indexName;
        private IndexRootRecord m_rootRecord;
        private IndexAllocationRecord m_indexAllocationRecord;
        private NonResidentAttributeData m_indexAllocationData;
        private AttributeRecord m_bitmapRecord;
        private BitmapData m_bitmapData;

        public IndexData(NTFSVolume volume, FileRecord fileRecord, AttributeType indexedAttributeType)
        {
            m_volume = volume;
            m_fileRecord = fileRecord;
            m_indexedAttributeType = indexedAttributeType;
            m_indexName = IndexHelper.GetIndexName(indexedAttributeType);
            m_rootRecord = (IndexRootRecord)m_fileRecord.GetAttributeRecord(AttributeType.IndexRoot, m_indexName);
            if (m_rootRecord.IsParentNode)
            {
                m_indexAllocationRecord = (IndexAllocationRecord)m_fileRecord.GetAttributeRecord(AttributeType.IndexAllocation, m_indexName);
                if (m_indexAllocationRecord == null)
                {
                    throw new InvalidDataException("Missing Index Allocation Record");
                }
                m_indexAllocationData = new NonResidentAttributeData(m_volume, m_indexAllocationRecord);
                m_bitmapRecord = m_fileRecord.GetAttributeRecord(AttributeType.Bitmap, m_indexName);
                if (m_bitmapRecord == null)
                {
                    throw new InvalidDataException("Missing Index Bitmap Record");
                }
                long numberOfUsableBits = (long)(m_indexAllocationRecord.DataLength / m_rootRecord.BytesPerIndexRecord);
                m_bitmapData = new BitmapData(m_volume, m_fileRecord, m_bitmapRecord, numberOfUsableBits);
            }
        }

        public KeyValuePair<MftSegmentReference, byte[]>? FindEntry(byte[] key)
        {
            if (!m_rootRecord.IsParentNode)
            {
                int index = CollationHelper.FindIndexInLeafNode(m_rootRecord.IndexEntries, key, m_rootRecord.CollationRule);
                if (index >= 0)
                {
                    IndexEntry entry = m_rootRecord.IndexEntries[index];
                    return new KeyValuePair<MftSegmentReference, byte[]>(entry.FileReference, entry.Key);
                }
                else
                {
                    return null;
                }
            }
            else
            {
                List<IndexEntry> entries = m_rootRecord.IndexEntries;
                bool isParentNode = true;
                int index;
                while (isParentNode)
                {
                    index = CollationHelper.FindIndexInParentNode(entries, key, m_rootRecord.CollationRule);
                    IndexEntry entry = entries[index];
                    if (!entry.IsLastEntry && CollationHelper.Compare(entry.Key, key, m_rootRecord.CollationRule) == 0)
                    {
                        return new KeyValuePair<MftSegmentReference, byte[]>(entry.FileReference, entry.Key);
                    }
                    else
                    {
                        long subnodeVBN = entry.SubnodeVBN;
                        IndexRecord indexRecord = ReadIndexRecord(subnodeVBN);
                        entries = indexRecord.IndexEntries;
                        isParentNode = indexRecord.IsParentNode;
                    }
                }

                index = CollationHelper.FindIndexInLeafNode(entries, key, m_rootRecord.CollationRule);
                if (index >= 0)
                {
                    IndexEntry entry = entries[index];
                    return new KeyValuePair<MftSegmentReference, byte[]>(entry.FileReference, entry.Key);
                }
                else
                {
                    return null;
                }
            }
        }

        public KeyValuePairList<MftSegmentReference, byte[]> GetAllEntries()
        {
            KeyValuePairList<MftSegmentReference, byte[]> result = new KeyValuePairList<MftSegmentReference, byte[]>();
            if (!m_rootRecord.IsParentNode)
            {
                foreach (IndexEntry entry in m_rootRecord.IndexEntries)
                {
                    result.Add(entry.FileReference, entry.Key);
                }
            }
            else
            {
                List<IndexEntry> parents = new List<IndexEntry>(m_rootRecord.IndexEntries);

                while (parents.Count > 0)
                {
                    IndexEntry parent = parents[0];
                    parents.RemoveAt(0);
                    IndexRecord record = ReadIndexRecord(parent.SubnodeVBN);
                    if (record.IsParentNode)
                    {
                        parents.InsertRange(0, record.IndexEntries);
                    }
                    else
                    {
                        foreach (IndexEntry entry in record.IndexEntries)
                        {
                            result.Add(entry.FileReference, entry.Key);
                        }
                    }


                    if (!parent.IsLastEntry)
                    {
                        // Some of the tree data in NTFS is contained in non-leaf keys
                        result.Add(parent.FileReference, parent.Key);
                    }
                }
            }
            return result;
        }

        private IndexRecord ReadIndexRecord(long subnodeVBN)
        {
            long sectorIndex = ConvertToSectorIndex(subnodeVBN);
            byte[] recordBytes = m_indexAllocationData.ReadSectors(sectorIndex, this.SectorsPerIndexRecord);
            IndexRecord record = new IndexRecord(recordBytes, 0);
            return record;
        }

        private long ConvertToSectorIndex(long recordVBN)
        {
            if (m_rootRecord.BytesPerIndexRecord >= m_volume.BytesPerCluster)
            {
                // The VBN is a VCN so we need to translate to sector index
                return recordVBN * m_volume.SectorsPerCluster;
            }
            else
            {
                return recordVBN * IndexRecord.BytesPerIndexRecordBlock / m_volume.BytesPerSector;
            }
        }

        private int SectorsPerIndexRecord
        {
            get
            {
                return (int)m_rootRecord.BytesPerIndexRecord / m_volume.BytesPerSector;
            }
        }
    }
}
