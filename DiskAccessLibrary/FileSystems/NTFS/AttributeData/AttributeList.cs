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
    /// <remarks>
    /// 1. A file can only have one attribute list and the $ATTRIBUTE_LIST record must reside in the base record segment
    /// 2. AttributeList data is not necessarily resident.
    /// 3. AttributeList can point to both resident and non-resident records.
    /// </remarks>
    /// http://blogs.technet.com/b/askcore/archive/2009/10/16/the-four-stages-of-ntfs-file-growth.aspx
    public class AttributeList : AttributeData
    {
        public List<AttributeListEntry> Attributes = new List<AttributeListEntry>();

        public AttributeList(NTFSVolume volume, AttributeRecord attributeRecord) : base(volume, null, attributeRecord)
        {
            byte[] data = ReadClusters(0, (int)ClusterCount);

            int position = 0;
            while (position < data.Length)
            {
                AttributeListEntry entry = new AttributeListEntry(data, position);
                Attributes.Add(entry);
                position += entry.Length;
            }
        }

        /// <summary>
        /// Return list containing the segment reference to all of the segments that are listed in this attribute list
        /// </summary>
        public List<MftSegmentReference> GetSegmentReferenceList()
        {
            List<MftSegmentReference> result = new List<MftSegmentReference>();
            foreach (AttributeListEntry entry in Attributes)
            {
                if (!MftSegmentReference.ContainsSegmentNumber(result, entry.SegmentReference.SegmentNumber))
                {
                    result.Add(entry.SegmentReference);
                }
            }
            return result;
        }
    }
}
