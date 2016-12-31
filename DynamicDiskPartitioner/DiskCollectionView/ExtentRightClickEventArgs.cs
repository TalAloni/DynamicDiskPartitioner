/* Copyright (C) 2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Drawing;
using DiskAccessLibrary;

namespace DynamicDiskPartitioner
{
    public class ExtentRightClickEventArgs : EventArgs
    {
        public DiskExtent Extent;
        public Volume Volume; // null for unallocated extents
        public Point Location;

        public ExtentRightClickEventArgs(DiskExtent extent, Volume volume, Point location)
        {
            Extent = extent;
            Volume = volume;
            Location = location;
        }
    }
}
