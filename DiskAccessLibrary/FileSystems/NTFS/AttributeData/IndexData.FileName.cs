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
    }
}
