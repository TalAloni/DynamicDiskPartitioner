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
    public class DiskLabelHelper
    {
        public static string GetDiskLabel(Disk disk, int visualDiskIndex)
        {
            int diskNumber;
            if (disk is PhysicalDisk)
            {
                diskNumber = ((PhysicalDisk)disk).PhysicalDiskIndex;
            }
            else
            {
                diskNumber = visualDiskIndex;
            }
            string diskType = GetDiskType(disk);

            return String.Format("Disk {0}\n{1}\n{2}", diskNumber, diskType, FormattingHelper.GetStandardSizeString(disk.Size));
        }

        private static string GetDiskType(Disk disk)
        {
            MasterBootRecord mbr = MasterBootRecord.ReadFromDisk(disk);
            if (DynamicDisk.IsDynamicDisk(disk))
            {
                return "Dynamic";
            }
            else
            {
                if (mbr == null)
                {
                    return "Uninitialized";
                }
                else if (!mbr.IsGPTBasedDisk)
                {
                    return "MBR";
                }
                else
                {
                    return "GPT";
                }
            }
        }

        public static string GetExtentLabel(Volume volume, DiskExtent extent, int width)
        {
            StringBuilder builder = new StringBuilder();
            if (volume != null)
            {
                Guid? volumeGuid = WindowsVolumeHelper.GetWindowsVolumeGuid(volume);
                if (volumeGuid.HasValue)
                {
                    List<string> mountPoints = WindowsVolumeManager.GetMountPoints(volumeGuid.Value);
                    if (mountPoints.Count > 0)
                    {
                        builder.AppendLine(mountPoints[0]);
                    }
                }
            }
            long size = extent.Size;
            if (width <= 60)
            {
                builder.AppendLine(FormattingHelper.GetCompactSizeString(extent.Size));
            }
            else
            {
                builder.AppendLine(FormattingHelper.GetStandardSizeString(extent.Size));
            }
            if (volume != null)
            {
                string statusString = VolumeHelper.GetVolumeStatusString(volume);
                builder.AppendLine(statusString);
            }

            return builder.ToString();
        }
    }
}
