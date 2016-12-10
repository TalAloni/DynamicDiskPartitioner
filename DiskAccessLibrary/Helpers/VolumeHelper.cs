/* Copyright (C) 2014 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using DiskAccessLibrary.LogicalDiskManager;
using Utilities;

namespace DiskAccessLibrary
{
    public partial class VolumeHelper
    {
        public static List<Volume> GetVolumes(List<Disk> disks)
        {
            List<Volume> result = new List<Volume>();
            List<DynamicDisk> dynamicDisks = new List<DynamicDisk>();

            // Get partitions:
            foreach (Disk disk in disks)
            {
                if (!DynamicDisk.IsDynamicDisk(disk))
                {
                    List<Partition> partitions = BasicDiskHelper.GetPartitions(disk);
                    foreach (Partition partition in partitions)
                    {
                        result.Add(partition);
                    }
                }
                else
                {
                    dynamicDisks.Add(DynamicDisk.ReadFromDisk(disk));
                }
            }

            // Get dynamic volumes
            List<DynamicVolume> dynamicVolumes = DynamicVolumeHelper.GetDynamicVolumes(dynamicDisks);
            foreach (DynamicVolume volume in dynamicVolumes)
            {
                result.Add(volume);
            }

            return result;
        }
    }
}
