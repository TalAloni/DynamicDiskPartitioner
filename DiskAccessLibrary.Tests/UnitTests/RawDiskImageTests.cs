/* Copyright (C) 2018-2024 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.IO;
using System.Threading;
using DiskAccessLibrary.Win32;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Utilities;

namespace DiskAccessLibrary.Tests.UnitTests
{
    [TestClass]
    public class RawDiskImageTests
    {
        private const long DiskSizeInBytes = 100 * 1024 * 1024;

        private RawDiskImage m_disk;

        [TestInitialize]
        public void Initialize()
        {
            string diskPath = $@"C:\RawDiskTest_{Guid.NewGuid()}.vhd";
            m_disk = RawDiskImage.Create(diskPath, DiskSizeInBytes);
        }

        [TestCleanup]
        public void Cleanup()
        {
            // File may still be in use for a brief period after vhdmount unmount
            Thread.Sleep(250);
            File.Delete(m_disk.Path);
        }

        [TestMethod]
        public void TestWriteRead()
        {
            m_disk.ExclusiveLock();
            for (long sectorIndex = 0; sectorIndex < m_disk.TotalSectors; sectorIndex += PhysicalDisk.MaximumDirectTransferSizeLBA)
            {
                long leftToWrite = m_disk.TotalSectors - sectorIndex;
                int sectorsToWrite = (int)Math.Min(leftToWrite, PhysicalDisk.MaximumDirectTransferSizeLBA);
                byte[] pattern = GetTestPattern(sectorIndex, sectorsToWrite, RawDiskImage.DefaultBytesPerSector);
                m_disk.WriteSectors(sectorIndex, pattern);
            }

            for (long sectorIndex = 0; sectorIndex < m_disk.TotalSectors; sectorIndex += PhysicalDisk.MaximumDirectTransferSizeLBA)
            {
                long leftToRead = m_disk.TotalSectors - sectorIndex;
                int sectorsToRead = (int)Math.Min(leftToRead, PhysicalDisk.MaximumDirectTransferSizeLBA);
                byte[] pattern = GetTestPattern(sectorIndex, sectorsToRead, RawDiskImage.DefaultBytesPerSector);
                byte[] sectorBytes = m_disk.ReadSectors(sectorIndex, sectorsToRead);
                if (!ByteUtils.AreByteArraysEqual(pattern, sectorBytes))
                {
                    throw new InvalidDataException("Test failed");
                }
            }

            m_disk.ReleaseLock();
        }

        [TestMethod]
        public void TestOverlappedWriteAndOverlappedRead()
        {
            m_disk.ExclusiveLock(true);
            int totalSectors = (int)Math.Min(m_disk.TotalSectors, int.MaxValue);
            Parallel.For(0, totalSectors, 1, 4, delegate (int sectorIndex)
            {
                byte[] pattern = GetTestPattern(sectorIndex, RawDiskImage.DefaultBytesPerSector);
                m_disk.WriteSectors(sectorIndex, pattern);
            });

            Parallel.For(0, totalSectors, 1, 4, delegate (int sectorIndex)
            {
                byte[] pattern = GetTestPattern(sectorIndex, RawDiskImage.DefaultBytesPerSector);
                byte[] sectorBytes = m_disk.ReadSector(sectorIndex);
                if (!ByteUtils.AreByteArraysEqual(pattern, sectorBytes))
                {
                    throw new InvalidDataException("Test failed");
                }
            });

            m_disk.ReleaseLock();
        }

        [TestMethod]
        public void TestSimultaneousWriteAndRead()
        {
            m_disk.ExclusiveLock(true);
            int totalSectors = (int)Math.Min(m_disk.TotalSectors, Int32.MaxValue);
            Parallel.For(0, totalSectors / 2, 1, 4, delegate (int sectorHint)
            {
                long sectorIndex = sectorHint * 2;
                byte[] pattern = GetTestPattern(sectorIndex, 2, RawDiskImage.DefaultBytesPerSector);
                m_disk.WriteSectors(sectorIndex, pattern);

                if (sectorIndex > 100)
                {
                    long sectorIndexToVerify = sectorIndex - 100;
                    byte[] expectedPattern = GetTestPattern(sectorIndexToVerify, 2, RawDiskImage.DefaultBytesPerSector);
                    byte[] sectorBytes = m_disk.ReadSectors(sectorIndexToVerify, 2);
                    if (!ByteUtils.AreByteArraysEqual(sectorBytes, expectedPattern))
                    {
                        throw new InvalidDataException("Test failed");
                    }
                }
            });

            m_disk.ReleaseLock();
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
