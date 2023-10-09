/* Copyright (C) 2014-2023 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using DiskAccessLibrary.VMDK;

namespace DiskAccessLibrary
{
    public partial class VirtualMachineDisk
    {
        public static VirtualMachineDisk CreateMonolithicFlat(string path, long size)
        {
            string directory = System.IO.Path.GetDirectoryName(path);
            string extentFileName = System.IO.Path.GetFileNameWithoutExtension(path) + "-flat.vmdk";
            string extentPath = System.IO.Path.Combine(directory, extentFileName);
            RawDiskImage.Create(extentPath, size);

            VirtualMachineDiskDescriptor descriptor = VirtualMachineDiskDescriptor.CreateDescriptor(VirtualMachineDiskType.MonolithicFlat, size);

            VirtualMachineDiskExtentEntry extentEntry = new VirtualMachineDiskExtentEntry();
            extentEntry.ReadAccess = true;
            extentEntry.WriteAccess = true;
            extentEntry.SizeInSectors = size / BytesPerDiskSector;
            extentEntry.ExtentType = ExtentType.Flat;
            extentEntry.FileName = extentFileName;
            extentEntry.Offset = 0;
            descriptor.ExtentEntries.Add(extentEntry);
            descriptor.SaveToFile(path);

            return new VirtualMachineDisk(path);
        }

        // https://kb.vmware.com/s/article/1026266
        public static void GetDiskGeometry(long totalSectors, out byte heads, out byte sectorsPerTrack, out long cylinders)
        {
            if (totalSectors * BytesPerDiskSector < 1073741824) // < 1 GB
            {
                heads = 64;
                sectorsPerTrack = 32;
            }
            else if (totalSectors * BytesPerDiskSector < 2147483648) // < 2 GB
            {
                heads = 128;
                sectorsPerTrack = 32;
            }
            else
            {
                heads = 255;
                sectorsPerTrack = 63;
            }
            cylinders = totalSectors / (heads * sectorsPerTrack);
        }
    }
}
