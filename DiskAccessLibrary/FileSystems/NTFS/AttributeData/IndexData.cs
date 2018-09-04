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
    public class IndexData
    {
        private NTFSVolume m_volume;
        private FileRecord m_fileRecord;
        private AttributeType m_indexedAttributeType; // Type of the attribute being indexed
        private string m_indexName;
        private IndexRootRecord m_rootRecord;

        public IndexData(NTFSVolume volume, FileRecord fileRecord, AttributeType indexedAttributeType)
        {
            m_volume = volume;
            m_fileRecord = fileRecord;
            m_indexedAttributeType = indexedAttributeType;
            m_indexName = "$I" + ((uint)indexedAttributeType).ToString("X");
            m_rootRecord = (IndexRootRecord)m_fileRecord.GetAttributeRecord(AttributeType.IndexRoot, m_indexName);
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
                IndexAllocationRecord indexAllocationRecord = (IndexAllocationRecord)m_fileRecord.GetAttributeRecord(AttributeType.IndexAllocation, m_indexName);
                NonResidentAttributeData indexAllocationData = new NonResidentAttributeData(m_volume, indexAllocationRecord);
                List<IndexEntry> parents = new List<IndexEntry>(m_rootRecord.IndexEntries);

                while (parents.Count > 0)
                {
                    IndexEntry parent = parents[0];
                    parents.RemoveAt(0);
                    byte[] recordBytes = indexAllocationData.ReadClusters(parent.SubnodeVCN, m_rootRecord.ClustersPerIndexRecord);
                    IndexRecord record = new IndexRecord(recordBytes, 0);
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
    }
}
