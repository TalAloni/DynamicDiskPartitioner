/* Copyright (C) 2014-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace DiskAccessLibrary.LogicalDiskManager
{
    public class DynamicDiskExtentHelper
    {
        public static int GetIndexOfExtentID(List<DynamicDiskExtent> extents, ulong extentID)
        {
            for (int index = 0; index < extents.Count; index++)
            {
                if (extents[index].ExtentID == extentID)
                {
                    return index;
                }
            }
            return -1;
        }

        public static DynamicDiskExtent GetByExtentID(List<DynamicDiskExtent> extents, ulong extentID)
        {
            int index = GetIndexOfExtentID(extents, extentID);
            if (index >= 0)
            {
                return extents[index];
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Support null disks
        /// </summary>
        public static DynamicDiskExtent GetDiskExtent(DynamicDisk dynamicDisk, ExtentRecord extentRecord)
        {
            long extentStartSector = GetExtentStartSector(dynamicDisk, extentRecord);
            long extentSize = (long)extentRecord.SizeLBA * PublicRegionHelper.BytesPerPublicRegionSector;
            Disk disk = null;
            Guid diskGuid = Guid.Empty;
            if (dynamicDisk != null)
            {
                disk = dynamicDisk.Disk;
                diskGuid = dynamicDisk.DiskGuid;
            }
            DynamicDiskExtent extent = new DynamicDiskExtent(disk, extentStartSector, extentSize, extentRecord.ExtentId);
            extent.Name = extentRecord.Name;
            extent.DiskGuid = diskGuid;
            return extent;
        }

        /// <summary>
        /// Support null disks
        /// </summary>
        public static long GetExtentStartSector(DynamicDisk disk, ExtentRecord extentRecord)
        {
            long publicRegionStartLBA = 0;
            int bytesPerDiskSector = DynamicColumn.DefaultBytesPerSector; // default for missing disks
            if (disk != null)
            {
                bytesPerDiskSector = disk.BytesPerSector;
                PrivateHeader privateHeader = disk.PrivateHeader;
                publicRegionStartLBA = (long)privateHeader.PublicRegionStartLBA;
            }
            return PublicRegionHelper.TranslateFromPublicRegionLBA((long)extentRecord.DiskOffsetLBA, publicRegionStartLBA, bytesPerDiskSector);
        }

        /// <param name="targetOffset">in bytes</param>
        public static bool IsMoveLocationValid(DynamicDisk disk, DynamicDiskExtent sourceExtent, long targetOffset)
        {
            List<DynamicDiskExtent> extents = GetDiskExtents(disk);
            // extents are sorted by first sector
            if (extents == null)
            {
                return false;
            }

            PrivateHeader privateHeader = disk.PrivateHeader;
            if (targetOffset % privateHeader.BytesPerSector > 0)
            {
                return false;
            }

            int index = GetIndexOfExtentID(extents, sourceExtent.ExtentID);
            extents.RemoveAt(index);

            long targetStartSector = targetOffset / disk.BytesPerSector;

            long publicRegionStartSector = (long)privateHeader.PublicRegionStartLBA;
            long startSector = publicRegionStartSector;
            long publicRegionSizeLBA = (long)privateHeader.PublicRegionSizeLBA;

            if (targetStartSector < publicRegionStartSector)
            {
                return false;
            }

            if (targetStartSector + sourceExtent.TotalSectors > publicRegionStartSector + publicRegionSizeLBA)
            {
                return false;
            }
            
            foreach (DynamicDiskExtent extent in extents)
            {
                long extentStartSector = extent.FirstSector;
                long extentEndSector = extent.FirstSector + extent.Size / disk.BytesPerSector - 1;
                if (extentStartSector >= targetStartSector &&
                    extentStartSector <= targetStartSector + sourceExtent.TotalSectors)
                {
                    // extent start within the requested region
                    return false;
                }

                if (extentEndSector >= targetStartSector &&
                    extentEndSector <= targetStartSector + sourceExtent.TotalSectors)
                {
                    // extent end within the requested region
                    return false;
                }
            }

            return true;
        }

        public static DiskExtent AllocateNewExtent(DynamicDisk disk, long allocationLength)
        {
            return AllocateNewExtent(disk, allocationLength, 0);
        }

        /// <param name="allocationLength">In bytes</param>
        /// <param name="alignInSectors">0 or 1 for no alignment</param>
        /// <returns>Allocated DiskExtent or null if there is not enough free disk space</returns>
        public static DiskExtent AllocateNewExtent(DynamicDisk disk, long allocationLength, long alignInSectors)
        {
            List<DiskExtent> unallocatedExtents = GetUnallocatedExtents(disk);
            if (unallocatedExtents == null)
            {
                return null;
            }

            for (int index = 0; index < unallocatedExtents.Count; index++)
            {
                DiskExtent extent = unallocatedExtents[index];
                if (alignInSectors > 1)
                {
                    extent = DiskExtentHelper.GetAlignedDiskExtent(extent, alignInSectors);
                }
                if (extent.Size >= allocationLength)
                {
                    return new DiskExtent(extent.Disk, extent.FirstSector, allocationLength);
                }
            }
            return null;
        }

        public static long GetMaxNewExtentLength(DynamicDisk disk)
        {
            return GetMaxNewExtentLength(disk, 0);
        }

        /// <returns>In bytes</returns>
        public static long GetMaxNewExtentLength(DynamicDisk disk, long alignInSectors)
        {
            List<DiskExtent> unallocatedExtents = GetUnallocatedExtents(disk);
            if (unallocatedExtents == null)
            {
                return -1;
            }

            long result = 0;
            for(int index = 0; index < unallocatedExtents.Count; index++)
            {
                DiskExtent extent = unallocatedExtents[index];
                if (alignInSectors > 1)
                {
                    extent = DiskExtentHelper.GetAlignedDiskExtent(extent, alignInSectors);
                }
                if (extent.Size > result)
                {
                    result = extent.Size;
                }
            }
            return result;
        }

        private static List<DiskExtent> GetUnallocatedExtents(DynamicDisk disk)
        {
            List<DynamicDiskExtent> extents = GetDiskExtents(disk);
            // extents are sorted by first sector
            if (extents == null)
            {
                return null;
            }
            List<DiskExtent> usedExtents = new List<DiskExtent>();
            foreach (DynamicDiskExtent usedExtent in usedExtents)
            {
                usedExtents.Add(usedExtent);
            }

            List<DiskExtent> result = new List<DiskExtent>();

            PrivateHeader privateHeader = disk.PrivateHeader;
            long publicRegionStartSector = (long)privateHeader.PublicRegionStartLBA;
            long publicRegionSize = (long)privateHeader.PublicRegionSizeLBA * disk.Disk.BytesPerSector;
            return DiskExtentsHelper.GetUnallocatedExtents(disk.Disk, publicRegionStartSector, publicRegionSize, usedExtents);
        }

        /// <summary>
        /// Sorted by first sector
        /// </summary>
        /// <returns>null if there was a problem reading extent information from disk</returns>
        public static List<DynamicDiskExtent> GetDiskExtents(DynamicDisk disk)
        {
            List<DynamicDiskExtent> result = new List<DynamicDiskExtent>();
            PrivateHeader privateHeader = disk.PrivateHeader;
            if (privateHeader != null)
            {
                VolumeManagerDatabase database = VolumeManagerDatabase.ReadFromDisk(disk);
                if (database != null)
                {
                    DiskRecord diskRecord = database.FindDiskByDiskGuid(privateHeader.DiskGuid);
                    List<ExtentRecord> extentRecords = database.FindExtentsByDiskID(diskRecord.DiskId);
                    foreach (ExtentRecord extentRecord in extentRecords)
                    {
                        DynamicDiskExtent extent = GetDiskExtent(disk, extentRecord);
                        result.Add(extent);
                    }
                    DynamicDiskExtentsHelper.SortExtentsByFirstSector(result);
                    return result;
                }
            }
            return null;
        }
    }
}
