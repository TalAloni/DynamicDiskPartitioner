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
    public class VisualDiskExtent
    {
        public int VisualDiskIndex;
        public DiskExtent Extent;
        public Volume Volume; // set to null for unallocated extents

        public VisualDiskExtent(int visualDiskIndex, DiskExtent extent, Volume volume)
        {
            VisualDiskIndex = visualDiskIndex;
            Extent = extent;
            Volume = volume;
        }
    }
}
