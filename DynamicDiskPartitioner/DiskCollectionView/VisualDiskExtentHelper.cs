/* Copyright (C) 2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace DynamicDiskPartitioner
{
    public class VisualDiskExtentHelper
    {
        public static List<VisualDiskExtent> GetFiltered(List<VisualDiskExtent> extents, int visualDiskIndex)
        {
            List<VisualDiskExtent> result = new List<VisualDiskExtent>();
            foreach (VisualDiskExtent extent in extents)
            {
                if (extent.VisualDiskIndex == visualDiskIndex)
                {
                    result.Add(extent);
                }
            }
            return result;
        }

        /// <summary>
        /// Sort (in-place) extents by first sector
        /// </summary>
        public static void SortExtentsByFirstSector(List<VisualDiskExtent> extents)
        {
            SortedList<long, VisualDiskExtent> list = new SortedList<long, VisualDiskExtent>();
            foreach (VisualDiskExtent extent in extents)
            {
                list.Add(extent.Extent.FirstSector, extent);
            }

            extents.Clear();
            extents.AddRange(list.Values);
        }

        public static List<int> DistributeWidth(List<VisualDiskExtent> extents, int diskWidth)
        {
            long maxExtentSize = 0;
            foreach (VisualDiskExtent extent in extents)
            {
                if (extent.Extent.Size > maxExtentSize)
                {
                    maxExtentSize = extent.Extent.Size;
                }
            }
            List<int> widthEntries = new List<int>();
            int sumWidth = 0;
            foreach (VisualDiskExtent extent in extents)
            {
                int rawExtentWidth = (int)VisualDiskHelper.Scale(extent.Extent.Size, maxExtentSize, diskWidth);
                sumWidth += rawExtentWidth;
                widthEntries.Add(rawExtentWidth);
            }
            double factor = (double)diskWidth / sumWidth;
            for (int index = 0; index < widthEntries.Count; index++)
            {
                widthEntries[index] = (int)Math.Round((widthEntries[index] * factor));
            }
            return widthEntries;
        }
    }
}
