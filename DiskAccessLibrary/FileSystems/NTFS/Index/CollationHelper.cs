/* Copyright (C) 2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    public class CollationHelper
    {
        public static int Compare(byte[] key1, byte[] key2, CollationRule collationRule)
        {
            switch(collationRule)
            {
                case CollationRule.Filename:
                    {
                        string str1 = FileNameRecord.ReadFileName(key1, 0);
                        string str2 = FileNameRecord.ReadFileName(key2, 0);
                        return String.Compare(str1, str2, true);
                    }
                case CollationRule.UnicodeString:
                    {
                        string str1 = Encoding.Unicode.GetString(key1);
                        string str2 = Encoding.Unicode.GetString(key2);
                        return String.Compare(str1, str2, true);
                    }
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
