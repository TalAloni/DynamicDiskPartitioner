/* Copyright (C) 2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using DiskAccessLibrary;
using DiskAccessLibrary.LogicalDiskManager;
using Utilities;

namespace DynamicDiskPartitioner
{
    public class DiskHelper
    {
        public static List<DiskExtent> GetUnallocatedExtents(List<PhysicalDisk> disks)
        {
            List<Disk> temp = new List<Disk>();
            foreach (PhysicalDisk disk in disks)
            {
                temp.Add(disk);
            }
            return GetUnallocatedExtents(temp);
        }

        public static List<DiskExtent> GetUnallocatedExtents(List<Disk> disks)
        {
            List<DiskExtent> extents = new List<DiskExtent>();
            foreach (Disk disk in disks)
            {
                if (DynamicDisk.IsDynamicDisk(disk))
                {
                    DynamicDisk dynamicDisk = DynamicDisk.ReadFromDisk(disk);
                    extents.AddRange(DynamicDiskHelper.GetUnallocatedExtents(dynamicDisk));
                }
                else
                {
                    extents.AddRange(BasicDiskHelper.GetUnallocatedExtents(disk));
                }
            }
            return extents;
        }

        public static int IndexOfDisk(List<DynamicDisk> dynamicDisks, Disk disk)
        {
            for (int index = 0; index < dynamicDisks.Count; index++)
            {
                if (dynamicDisks[index].Disk == disk)
                {
                    return index;
                }
            }
            return -1;
        }
    }
}
