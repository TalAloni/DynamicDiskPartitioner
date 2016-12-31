/* Copyright (C) 2012-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace DynamicDiskPartitioner
{
    public class FormattingHelper
    {
        public static string GetStandardSizeString(long value)
        {
            string[] suffixes = { " B", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB" };
            int suffixIndex = 0;
            while (value > 9999)
            {
                value = value / 1024;
                suffixIndex++;
            }

            if (suffixIndex < suffixes.Length)
            {
                return String.Format("{0} {1}", value.ToString(), suffixes[suffixIndex]);
            }
            else
            {
                return "> 9999 EB";
            }
        }

        public static string GetCompactSizeString(long value)
        {
            string[] suffixes = { " B", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB" };
            int suffixIndex = 0;
            while (value >= 1024)
            {
                value = value / 1024;
                suffixIndex++;
            }

            if (suffixIndex < suffixes.Length)
            {
                return String.Format("{0} {1}", value.ToString(), suffixes[suffixIndex]);
            }
            else
            {
                return "> ZB";
            }
        }
    }
}
