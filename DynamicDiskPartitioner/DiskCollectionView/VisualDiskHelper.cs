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

namespace DynamicDiskPartitioner
{
    public class VisualDiskHelper
    {
        public static List<VisualDiskExtent> GetVisualExtents(List<PhysicalDisk> disks)
        {
            List<Disk> temp = new List<Disk>();
            foreach (PhysicalDisk disk in disks)
            {
                temp.Add(disk);
            }
            return GetVisualExtents(temp);
        }

        public static List<VisualDiskExtent> GetVisualExtents(List<Disk> disks)
        {
            List<Volume> volumes = VolumeHelper.GetVolumes(disks);
            List<VisualDiskExtent> extents = new List<VisualDiskExtent>();
            foreach (Volume volume in volumes)
            {
                if (volume is MirroredVolume)
                {
                    List<DynamicVolume> components = ((MirroredVolume)volume).Components;
                    foreach (Volume component in components)
                    {
                        foreach (DiskExtent extent in component.Extents)
                        {
                            int diskIndex = disks.IndexOf(extent.Disk);
                            extents.Add(new VisualDiskExtent(diskIndex, extent, volume));
                        }
                    }
                }
                else
                {
                    foreach (DiskExtent extent in volume.Extents)
                    {
                        int diskIndex = disks.IndexOf(extent.Disk);
                        extents.Add(new VisualDiskExtent(diskIndex, extent, volume));
                    }
                }
            }

            List<DiskExtent> unallocatedExtents = DiskHelper.GetUnallocatedExtents(disks);
            foreach (DiskExtent extent in unallocatedExtents)
            {
                int diskIndex = disks.IndexOf(extent.Disk);
                extents.Add(new VisualDiskExtent(diskIndex, extent, null));
            }

            return extents;
        }

        public static double Scale(long value, long maxInputValue, long maxOutputValue)
        {
            return Scale(value, maxInputValue, maxOutputValue, 3);
        }

        public static double Scale(long value, long maxInputValue, long maxOutputValue, double scaleFactor)
        {
            if (value <= 1)
                return 0; // log is undefined for 0, log(1) = 0
            return maxOutputValue * Math.Pow(Math.Log(value), scaleFactor) / Math.Pow(Math.Log(maxInputValue), scaleFactor);
        }
    }
}
