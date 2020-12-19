/* Copyright (C) 2016-2020 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
using DiskAccessLibrary.Win32;

namespace DynamicDiskPartitioner
{
    public class DiskInfo
    {
        public static string GetDiskInformation(Disk disk)
        {
            StringBuilder builder = new StringBuilder();
            if (disk is PhysicalDisk)
            {
                PhysicalDisk physicalDisk = (PhysicalDisk)disk;
                builder.AppendLine(physicalDisk.Description);
                builder.AppendLine("S/N: " + physicalDisk.SerialNumber);
                builder.AppendLine();
            }
            builder.AppendFormat("Size: {0} bytes\n", disk.Size.ToString("###,###,###,###,##0"));
            builder.AppendFormat("Bytes per sector (logical): {0}\n", disk.BytesPerSector);
            if (disk is PhysicalDisk)
            {
                PhysicalDisk physicalDisk = (PhysicalDisk)disk;
                builder.AppendFormat("Geometry: Cylinders: {0}, Heads: {1}, Sectors Per Track: {2}\n", physicalDisk.Cylinders, physicalDisk.TracksPerCylinder, physicalDisk.SectorsPerTrack);
            }
            else if (disk is DiskImage)
            {
                DiskImage diskImage = (DiskImage)disk;
                builder.AppendFormat("Disk image path: {0}\n", diskImage.Path);
            }
            builder.AppendLine();

            MasterBootRecord mbr = MasterBootRecord.ReadFromDisk(disk);
            if (mbr != null)
            {
                builder.AppendFormat("Partitioning scheme: {0}\n", (mbr.IsGPTBasedDisk ? "GPT" : "MBR"));
            }
            DynamicDisk dynamicDisk = DynamicDisk.ReadFromDisk(disk);
            builder.AppendFormat("Disk type: {0}\n", ((dynamicDisk != null) ? "Dynamic Disk" : "Basic Disk"));
            if (dynamicDisk != null)
            {
                VolumeManagerDatabase database = VolumeManagerDatabase.ReadFromDisk(dynamicDisk);
                if (database != null)
                {
                    DiskRecord diskRecord = database.FindDiskByDiskGuid(dynamicDisk.PrivateHeader.DiskGuid);
                    if (diskRecord != null)
                    {
                        builder.AppendLine("Disk ID: " + diskRecord.DiskId);
                    }
                }
                builder.AppendLine("Disk GUID: " + dynamicDisk.PrivateHeader.DiskGuid);
                builder.AppendLine("Disk Group GUID: " + dynamicDisk.PrivateHeader.DiskGroupGuidString);
                builder.AppendLine();
                builder.AppendLine("Public region start sector: " + dynamicDisk.PrivateHeader.PublicRegionStartLBA);
                builder.AppendLine("Public region size (sectors): " + dynamicDisk.PrivateHeader.PublicRegionSizeLBA);
                builder.AppendLine();
                builder.AppendLine("Private region start sector: " + dynamicDisk.PrivateHeader.PrivateRegionStartLBA);
                builder.AppendLine("Private region size (sectors): " + dynamicDisk.PrivateHeader.PrivateRegionSizeLBA);
            }
            return builder.ToString();
        }
    }
}
