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
    /// <remarks>
    /// IndexAllocation attribute is always non-resident.
    /// </remarks>
    public class IndexAllocationRecord : NonResidentAttributeRecord
    {
        public IndexAllocationRecord(byte[] buffer, int offset) : base(buffer, offset)
        {
        }

        public KeyValuePairList<MftSegmentReference, FileNameRecord> GetAllEntries(NTFSVolume volume, IndexRootRecord rootRecord)
        {
            KeyValuePairList<MftSegmentReference, FileNameRecord> result = new KeyValuePairList<MftSegmentReference, FileNameRecord>();
            List<IndexNodeEntry> parents = new List<IndexNodeEntry>(rootRecord.IndexEntries);
            List<IndexRecord> leaves = new List<IndexRecord>();
            NonResidentAttributeData attributeData = new NonResidentAttributeData(volume, this);

            int parentIndex = 0;
            while (parentIndex < parents.Count)
            {
                IndexNodeEntry parent = parents[parentIndex];
                byte[] clusters = attributeData.ReadClusters(parent.SubnodeVCN, rootRecord.ClustersPerIndexRecord);
                IndexRecord record = new IndexRecord(clusters, 0);
                if (record.IsParentNode)
                {
                    foreach (IndexNodeEntry node in record.IndexEntries)
                    {
                        parents.Add(node);
                    }
                }
                else
                {
                    leaves.Add(record);
                }

                parentIndex++;
            }

            foreach (IndexNodeEntry node in parents)
            {
                if (!node.IsLastEntry)
                {
                    // Some of the tree data in NTFS is contained in non-leaf keys
                    FileNameRecord parentRecord = new FileNameRecord(node.Key, 0);
                    result.Add(node.SegmentReference, parentRecord);
                }
            }

            foreach (IndexRecord record in leaves)
            {
                foreach (FileNameIndexEntry entry in record.FileNameEntries)
                {
                    result.Add(entry.FileReference, entry.Record);
                }
            }

            result.Sort(Compare);

            return result;
        }

        public static int Compare(KeyValuePair<MftSegmentReference, FileNameRecord> entryA, KeyValuePair<MftSegmentReference, FileNameRecord> entryB)
        {
            return String.Compare(entryA.Value.FileName, entryB.Value.FileName);
        }
    }
}
