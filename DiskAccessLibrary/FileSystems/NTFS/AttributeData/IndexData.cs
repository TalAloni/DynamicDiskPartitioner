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
    public class IndexData
    {
        private NTFSVolume m_volume;
        private FileRecord m_fileRecord;
        private AttributeType m_indexedAttributeType; // Type of the attribute being indexed
        private string m_indexName;
        private IndexRootRecord m_rootRecord;
        private NonResidentAttributeRecord m_indexAllocationRecord;
        private NonResidentAttributeData m_indexAllocationData;

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
            }
        }

        public KeyValuePairList<MftSegmentReference, FileNameRecord> GetAllFileNameRecords()
        {
            KeyValuePairList<MftSegmentReference, FileNameRecord> result = new KeyValuePairList<MftSegmentReference, FileNameRecord>();
            KeyValuePairList<MftSegmentReference, byte[]> entries = GetAllEntries();
            foreach (KeyValuePair<MftSegmentReference, byte[]> entry in entries)
            {
                FileNameRecord fileNameRecord = new FileNameRecord(entry.Value, 0);
                result.Add(entry.Key, fileNameRecord);
            }

            return result;
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

        public IndexRecord ReadIndexRecord(long subnodeVBN)
        {
            if (m_rootRecord.BytesPerIndexRecord >= m_volume.BytesPerCluster)
            {
                // The VBN is a VCN so we need to translate to sector number
                subnodeVBN *= m_volume.SectorsPerCluster;
            }
            else
            {
                subnodeVBN = subnodeVBN * IndexRecord.BytesPerIndexRecordBlock / m_volume.BytesPerSector;
            }
            byte[] recordBytes = m_indexAllocationData.ReadSectors(subnodeVBN, this.SectorsPerIndexRecord);
            IndexRecord record = new IndexRecord(recordBytes, 0);
            return record;
        }

        public int SectorsPerIndexRecord
        {
            get
            {
                return (int)m_rootRecord.BytesPerIndexRecord / m_volume.BytesPerSector;
            }
        }
    }
}
