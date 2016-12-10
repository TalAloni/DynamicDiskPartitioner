using System;
using System.Collections.Generic;
using System.Text;
using DiskAccessLibrary;
using Utilities;

namespace Raid5Manager
{
    public class TestHelper
    {
        public static void WriteTestPattern(Volume volume, ref long sectorsWritten)
        {
            int transferSize = Settings.MaximumTransferSizeLBA;
            sectorsWritten = 0;
            for (long sectorIndex = 0; sectorIndex < volume.TotalSectors; sectorIndex += transferSize)
            {
                int sectorsToWrite = (int)Math.Min(transferSize, volume.TotalSectors - sectorIndex);
                // Clear allocations from previous iteration
                GC.Collect();
                GC.WaitForPendingFinalizers();
                byte[] pattern = GetTestPattern(sectorIndex, sectorsToWrite, volume.BytesPerSector);
                if (sectorIndex == 0)
                {
                    // the volume could be extended (we only want to check the original sectors)
                    BigEndianWriter.WriteUInt64(pattern, 8, (ulong)volume.TotalSectors);
                }
                volume.WriteSectors(sectorIndex, pattern);
                sectorsWritten += sectorsToWrite;
            }
        }

        public static void WriteTestPattern(Disk disk, ref long sectorsWritten)
        {
            int transferSize = Settings.MaximumTransferSizeLBA;
            sectorsWritten = 0;
            for (long sectorIndex = 0; sectorIndex < disk.TotalSectors; sectorIndex += transferSize)
            {
                int sectorsToWrite = (int)Math.Min(transferSize, disk.TotalSectors - sectorIndex);
                // Clear allocations from previous iteration
                GC.Collect();
                GC.WaitForPendingFinalizers();
                byte[] pattern = GetTestPattern(sectorIndex, sectorsToWrite, disk.BytesPerSector);
                if (sectorIndex == 0)
                {
                    // the volume could be extended (we only want to check the original sectors)
                    BigEndianWriter.WriteUInt64(pattern, 8, (ulong)disk.TotalSectors);
                }
                disk.WriteSectors(sectorIndex, pattern);
                sectorsWritten += sectorsToWrite;
            }
        }

        /// <returns>List of sectors that failed verification</returns>
        public static List<long> VerifyTestPattern(Volume volume, ref long sectorsRead)
        {
            int transferSize = Settings.MaximumTransferSizeLBA;
            sectorsRead = 0;
            List<long> failedSectorList = new List<long>();
            long totalSectors = volume.TotalSectors;
            for (long sectorIndex = 0; sectorIndex < totalSectors; sectorIndex += transferSize)
            {
                int sectorsToRead = (int)Math.Min(transferSize, totalSectors - sectorIndex);
                // Clear allocations from previous iteration
                GC.Collect();
                GC.WaitForPendingFinalizers();
                byte[] data = volume.ReadSectors(sectorIndex, sectorsToRead);
                for (int offset = 0; offset < sectorsToRead; offset++)
                {
                    byte[] sectorBytes = ByteReader.ReadBytes(data, (int)(offset * volume.BytesPerSector), volume.BytesPerSector);
                    byte[] expected = GetTestPattern(sectorIndex + offset, volume.BytesPerSector);

                    if (sectorIndex + offset == 0)
                    {
                        long sectorNumber = (long)BigEndianConverter.ToUInt64(sectorBytes, 0);
                        long sectorCount = (long)BigEndianConverter.ToUInt64(sectorBytes, 8);
                        if (sectorNumber == 0 && sectorCount > 0)
                        {
                            totalSectors = sectorCount;
                            BigEndianWriter.WriteUInt64(sectorBytes, 8, 0);
                        }
                    }

                    if (!ByteUtils.AreByteArraysEqual(sectorBytes, expected))
                    {
                        failedSectorList.Add(sectorIndex + offset);
                    }
                }

                sectorsRead += sectorsToRead;
            }
            return failedSectorList;
        }

        /// <returns>List of sectors that failed verification</returns>
        public static List<long> VerifyTestPattern(Disk disk, ref long sectorsRead)
        {
            int transferSize = Settings.MaximumTransferSizeLBA;
            sectorsRead = 0;
            List<long> failedSectorList = new List<long>();
            long totalSectors = disk.TotalSectors;
            for (long sectorIndex = 0; sectorIndex < totalSectors; sectorIndex += transferSize)
            {
                int sectorsToRead = (int)Math.Min(transferSize, totalSectors - sectorIndex);
                // Clear allocations from previous iteration
                GC.Collect();
                GC.WaitForPendingFinalizers();
                byte[] data = disk.ReadSectors(sectorIndex, sectorsToRead);
                for (int offset = 0; offset < sectorsToRead; offset++)
                {
                    byte[] sectorBytes = ByteReader.ReadBytes(data, (int)(offset * disk.BytesPerSector), disk.BytesPerSector);
                    byte[] expected = GetTestPattern(sectorIndex + offset, disk.BytesPerSector);

                    if (sectorIndex + offset == 0)
                    {
                        long sectorNumber = (long)BigEndianConverter.ToUInt64(sectorBytes, 0);
                        long sectorCount = (long)BigEndianConverter.ToUInt64(sectorBytes, 8);
                        if (sectorNumber == 0 && sectorCount > 0)
                        {
                            totalSectors = sectorCount;
                            BigEndianWriter.WriteUInt64(sectorBytes, 8, 0);
                        }
                    }

                    if (!ByteUtils.AreByteArraysEqual(sectorBytes, expected))
                    {
                        failedSectorList.Add(sectorIndex + offset);
                    }
                }

                sectorsRead += sectorsToRead;
            }
            return failedSectorList;
        }

        private static byte[] GetTestPattern(long sectorIndex, int sectorCount, int bytesPerSector)
        {
            byte[] pattern = new byte[sectorCount * bytesPerSector];
            for (int sectorOffset = 0; sectorOffset < sectorCount; sectorOffset++)
            {
                for (int offsetInSector = 0; offsetInSector <= bytesPerSector - 8; offsetInSector += 8)
                {
                    BigEndianWriter.WriteInt64(pattern, sectorOffset * bytesPerSector + offsetInSector, sectorIndex + sectorOffset);
                }
            }
            return pattern;
        }

        private static byte[] GetTestPattern(long sectorIndex, int bytesPerSector)
        {
            byte[] pattern = new byte[bytesPerSector];
            for (int offset = 0; offset <= bytesPerSector - 8; offset += 8)
            {
                BigEndianWriter.WriteInt64(pattern, offset, sectorIndex);
            }
            return pattern;
        }
    }
}
