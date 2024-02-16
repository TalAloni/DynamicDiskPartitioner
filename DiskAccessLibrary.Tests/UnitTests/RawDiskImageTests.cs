/* Copyright (C) 2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using DiskAccessLibrary;
using DiskAccessLibrary.Win32;
using Utilities;

namespace DiskAccessLibrary.Tests
{
    public class RawDiskImageTests
    {
        public static void Test(string path, long size)
        {
            TestWriteRead(path, size);
            File.Delete(path);
            TestOverlappedWriteAndOverlappedRead(path, size);
            File.Delete(path);
            TestSimultaneousWriteAndRead(path, size);
            File.Delete(path);
        }

        public static void TestWriteRead(string path, long size)
        {
            RawDiskImage disk = RawDiskImage.Create(path, size);
            disk.ExclusiveLock();
            for (long sectorIndex = 0; sectorIndex < disk.TotalSectors; sectorIndex += PhysicalDisk.MaximumDirectTransferSizeLBA)
            {
                long leftToWrite = disk.TotalSectors - sectorIndex;
                int sectorsToWrite = (int)Math.Min(leftToWrite, PhysicalDisk.MaximumDirectTransferSizeLBA);
                byte[] pattern = GetTestPattern(sectorIndex, sectorsToWrite, RawDiskImage.DefaultBytesPerSector);
                disk.WriteSectors(sectorIndex, pattern);
            }

            for (long sectorIndex = 0; sectorIndex < disk.TotalSectors; sectorIndex += PhysicalDisk.MaximumDirectTransferSizeLBA)
            {
                long leftToRead = disk.TotalSectors - sectorIndex;
                int sectorsToRead = (int)Math.Min(leftToRead, PhysicalDisk.MaximumDirectTransferSizeLBA);
                byte[] pattern = GetTestPattern(sectorIndex, sectorsToRead, RawDiskImage.DefaultBytesPerSector);
                byte[] sectorBytes = disk.ReadSectors(sectorIndex, sectorsToRead);
                if (!ByteUtils.AreByteArraysEqual(pattern, sectorBytes))
                {
                    throw new InvalidDataException("Test failed");
                }
            }

            disk.ReleaseLock();
        }

        public static void TestOverlappedWriteAndOverlappedRead(string path, long size)
        {
            RawDiskImage disk = RawDiskImage.Create(path, size);
            disk.ExclusiveLock(true);
            int totalSectors = (int)Math.Min(disk.TotalSectors, Int32.MaxValue);
            Parallel.For(0, totalSectors, 1, 4, delegate(int sectorIndex)
            {
                byte[] pattern = GetTestPattern(sectorIndex, RawDiskImage.DefaultBytesPerSector);
                disk.WriteSectors(sectorIndex, pattern);
            });

            Parallel.For(0, totalSectors, 1, 4, delegate(int sectorIndex)
            {
                byte[] pattern = GetTestPattern(sectorIndex, RawDiskImage.DefaultBytesPerSector);
                byte[] sectorBytes = disk.ReadSector(sectorIndex);
                if (!ByteUtils.AreByteArraysEqual(pattern, sectorBytes))
                {
                    throw new InvalidDataException("Test failed");
                }
            });

            disk.ReleaseLock();
        }

        public static void TestSimultaneousWriteAndRead(string path, long size)
        {
            RawDiskImage disk = RawDiskImage.Create(path, size);
            disk.ExclusiveLock(true);
            int totalSectors = (int)Math.Min(disk.TotalSectors, Int32.MaxValue);
            Parallel.For(0, totalSectors / 2, 1, 4, delegate(int sectorHint)
            {
                long sectorIndex = sectorHint * 2;
                byte[] pattern = GetTestPattern(sectorIndex, 2, RawDiskImage.DefaultBytesPerSector);
                disk.WriteSectors(sectorIndex, pattern);

                if (sectorIndex > 100)
                {
                    long sectorIndexToVerify = sectorIndex - 100;
                    byte[] expectedPattern = GetTestPattern(sectorIndexToVerify, 2, RawDiskImage.DefaultBytesPerSector);
                    byte[] sectorBytes = disk.ReadSectors(sectorIndexToVerify, 2);
                    if (!ByteUtils.AreByteArraysEqual(sectorBytes, expectedPattern))
                    {
                        throw new InvalidDataException("Test failed");
                    }
                }
            });

            disk.ReleaseLock();
        }

        private static byte[] GetTestPattern(long sectorIndex, int bytesPerSector)
        {
            return GetTestPattern(sectorIndex, 1, bytesPerSector);
        }

        private static byte[] GetTestPattern(long sectorIndex, int sectorCount, int bytesPerSector)
        {
            byte[] buffer = new byte[sectorCount * bytesPerSector];
            for (int sectorOffset = 0; sectorOffset < sectorCount; sectorOffset++)
            {
                byte[] pattern = BigEndianConverter.GetBytes(sectorIndex + sectorOffset);
                for (int offsetInSector = 0; offsetInSector <= bytesPerSector - 8; offsetInSector += 8)
                {
                    Array.Copy(pattern, 0, buffer, sectorOffset * bytesPerSector + offsetInSector, 8);
                }
            }
            return buffer;
        }
    }
}
