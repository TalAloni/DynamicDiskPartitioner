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

namespace Raid5Manager
{
    public class FormattingHelper
    {
        public static long ParseStandardSizeString(string value)
        {
            if (value.ToUpper().EndsWith("TB"))
            {
                return (long)1024 * 1024 * 1024 * 1024 * Conversion.ToInt64(value.Substring(0, value.Length - 2), -1);
            }
            else if (value.ToUpper().EndsWith("GB"))
            {
                return 1024 * 1024 * 1024 * Conversion.ToInt64(value.Substring(0, value.Length - 2), -1);
            }
            else if (value.ToUpper().EndsWith("MB"))
            {
                return 1024 * 1024 * Conversion.ToInt64(value.Substring(0, value.Length - 2), -1);
            }
            else if (value.ToUpper().EndsWith("KB"))
            {
                return 1024 * Conversion.ToInt64(value.Substring(0, value.Length - 2), -1);
            }
            if (value.ToUpper().EndsWith("B"))
            {
                return Conversion.ToInt64(value.Substring(0, value.Length - 1), -1);
            }
            else
            {
                return Conversion.ToInt64(value, -1);
            }
        }

        public static string GetStandardSizeString(long value)
        {
            string[] suffixes = { " B", "KB", "MB", "GB", "TB", "PB", "EB" };
            int suffixIndex = 0;
            while (value > 9999)
            {
                value = value / 1024;
                suffixIndex++;
            }

            if (suffixIndex < suffixes.Length)
            {
                string FourCharacterValue = value.ToString();
                while (FourCharacterValue.Length < 4)
                {
                    FourCharacterValue = " " + FourCharacterValue;
                }
                return String.Format("{0} {1}", FourCharacterValue, suffixes[suffixIndex]);
            }
            else
            {
                return "> 9999 EB";
            }
        }
    }
}
