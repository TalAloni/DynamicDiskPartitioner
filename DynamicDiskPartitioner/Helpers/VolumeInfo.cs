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
using DiskAccessLibrary.FileSystems;
using DiskAccessLibrary.FileSystems.Abstractions;
using DiskAccessLibrary.LogicalDiskManager;
using Utilities;

namespace DynamicDiskPartitioner
{
    public class VolumeInfo
    {
        public static string GetVolumeInformation(Volume volume)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendFormat("Volume size: {0} bytes\n", volume.Size.ToString("###,###,###,###,##0"));
            builder.AppendFormat("Volume type: {0}\n", VolumeHelper.GetVolumeTypeString(volume));
            if (volume is GPTPartition)
            {
                builder.AppendFormat("Partition name: {0}\n", ((GPTPartition)volume).PartitionName);
            }
            else if (volume is DynamicVolume)
            {
                builder.AppendFormat("Volume name: {0}\n", ((DynamicVolume)volume).Name);
                builder.AppendFormat("Volume status: {0}\n", VolumeHelper.GetVolumeStatusString(volume));
            }
            
            Guid? windowsVolumeGuid = WindowsVolumeHelper.GetWindowsVolumeGuid(volume);
            if (windowsVolumeGuid.HasValue)
            {
                List<string> mountPoints = WindowsVolumeManager.GetMountPoints(windowsVolumeGuid.Value);
                foreach (string volumePath in mountPoints)
                {
                    builder.AppendFormat("Volume path: {0}\n", volumePath);
                }
                bool isMounted = WindowsVolumeManager.IsMounted(windowsVolumeGuid.Value);
                builder.AppendFormat("Mounted: {0}\n", isMounted);
            }
            builder.AppendLine();

            if (volume is MirroredVolume)
            {
                builder.AppendLine("Extents:");
                List<DynamicVolume> components = ((MirroredVolume)volume).Components;
                for (int componentIndex = 0; componentIndex < components.Count; componentIndex++)
                {
                    if (componentIndex != 0)
                    {
                        builder.AppendLine();
                    }
                    DynamicVolume component = components[componentIndex];
                    builder.AppendFormat("Component {0}:\n", componentIndex);
                    builder.Append(GetExtentsInformation(component));
                }
            }
            else if (volume is DynamicVolume)
            {
                builder.AppendLine("Extents:");
                builder.Append(GetExtentsInformation((DynamicVolume)volume));
            }
            else if (volume is Partition)
            {
                Partition partition = (Partition)volume;
                long partitionOffset = partition.FirstSector * partition.BytesPerSector;
                string partitionOffsetString = FormattingHelper.GetStandardSizeString(partitionOffset);
                builder.AppendFormat("Partiton Offset: {0}, Start Sector: {1}\n", partitionOffsetString, partition.FirstSector);
            }

            return builder.ToString();
        }

        public static string GetExtentsInformation(DynamicVolume volume)
        {
            List<DynamicDiskExtent> extents = volume.DynamicExtents;
            StringBuilder builder = new StringBuilder();
            for(int extentIndex = 0; extentIndex < extents.Count; extentIndex++)
            {
                DynamicDiskExtent extent = extents[extentIndex];
                string extentOffsetString;
                string diskIDString = String.Empty;
                if (extent.Disk != null)
                {
                    long extentOffset = extent.FirstSector * extent.Disk.BytesPerSector;
                    extentOffsetString = FormattingHelper.GetStandardSizeString(extentOffset);
                    VolumeManagerDatabase database = VolumeManagerDatabase.ReadFromDisk(extent.Disk);
                    if (database != null)
                    {
                        ExtentRecord extentRecord = database.FindExtentByExtentID(extent.ExtentID);
                        if (extentRecord != null)
                        {
                            diskIDString = extentRecord.DiskId.ToString();
                        }
                    }
                }
                else
                {
                    extentOffsetString = "N/A";
                }

                string extentSizeString = FormattingHelper.GetStandardSizeString(extent.Size);
                builder.AppendFormat("Extent {0}, ID: {1}, Name: {2}, Size: {3}, Disk ID: {4}, Offset: {5}, Start Sector: {6}\n",
                                      extentIndex, extent.ExtentID, extent.Name, extentSizeString, diskIDString, extentOffsetString, extent.FirstSector);
            }
            return builder.ToString();
        }

        public static string GetFileSystemInformation(Volume volume)
        {
            StringBuilder builder = new StringBuilder();
            FileSystem fileSystem = FileSystemHelper.ReadFileSystem(volume, true);
            if (fileSystem != null)
            {
                builder.AppendFormat("File system: {0}\n", fileSystem.Name);
                builder.Append(fileSystem.ToString()); // FileSystem.ToString() already appended newline at the end, no need for AppendLine()
            }
            else
            {
                builder.AppendLine("File system: Unsupported");
            }
            return builder.ToString();
        }
    }
}
