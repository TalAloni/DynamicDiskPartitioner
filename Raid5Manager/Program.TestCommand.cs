using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using DiskAccessLibrary.LogicalDiskManager;
using DiskAccessLibrary;
using Utilities;

namespace Raid5Manager
{
    public partial class Program
    {
        public static void TestCommand(string[] args)
        {
            if (args.Length >= 3 && args.Length <= 5)
            {
                switch (args[1].ToLower())
                {
                    case "volume":
                        {
                            if (m_selectedVolume == null)
                            {
                                Console.WriteLine("No volume has been selected.");
                                return;
                            }

                            if (args[2].ToLower() == "write")
                            {
                                KeyValuePairList<string, string> parameters = ParseParameters(args, 3);

                                Guid? windowsVolumeGuid = WindowsVolumeHelper.GetWindowsVolumeGuid(m_selectedVolume);
                                if (!windowsVolumeGuid.HasValue)
                                {
                                    Console.WriteLine("Error: Cannot obtain volume GUID");
                                    return;
                                }
                                // Windows XP / 2003: It's acceptable to request a volume handle with just FileAccess.Read when locking a volume.
                                // Windows Vista / 7: We MUST request a volume handle with just FileAccess.Read when locking a volume on a basic disk.
                                // Windows Vista / 7: We MUST request a volume handle with FileAccess.ReadWrite when locking dynamic volumes.
                                FileAccess fileAccess = (m_selectedVolume is DynamicVolume) ? FileAccess.ReadWrite : FileAccess.Read;
                                bool success = WindowsVolumeManager.ExclusiveLockIfMounted(windowsVolumeGuid.Value, fileAccess);
                                if (!success)
                                {
                                    Console.WriteLine("Unable to lock volume.");
                                    return;
                                }
                                else
                                {
                                    if (!parameters.ContainsKey("winapi"))
                                    {
                                        success = WindowsVolumeManager.DismountVolume(windowsVolumeGuid.Value);
                                    }
                                }

                                Console.Write("Are you sure that you want to destroy the volume? ");
                                string input = Console.ReadLine();
                                if (input == "y")
                                {
                                    Volume volume;
                                    if (parameters.ContainsKey("winapi"))
                                    {
                                        volume = new OperatingSystemVolume(windowsVolumeGuid.Value, m_selectedVolume.BytesPerSector, m_selectedVolume.Size);
                                    }
                                    else
                                    {
                                        volume = m_selectedVolume;                                            
                                    }

                                    long sectorsWritten = 0;
                                    Thread thread = new Thread(delegate()
                                    {
                                        TestHelper.WriteTestPattern(volume, ref sectorsWritten);
                                    });
                                    thread.Start();

                                    while (thread.IsAlive)
                                    {
                                        Thread.Sleep(1000);
                                        Console.SetCursorPosition(0, Console.CursorTop);
                                        Console.Write("Progress: {0:##0.0}%", ((double)sectorsWritten / volume.TotalSectors) * 100);
                                    }

                                    Console.WriteLine();
                                    Console.WriteLine();
                                    Console.WriteLine("Operation completed.");
                                }

                                if (windowsVolumeGuid.HasValue)
                                {
                                    WindowsVolumeManager.ReleaseLock(windowsVolumeGuid.Value);
                                }
                            }
                            else if (args[2].ToLower() == "verify")
                            {
                                KeyValuePairList<string, string> parameters = ParseParameters(args, 3);
                                Volume volume;
                                if (parameters.ContainsKey("winapi"))
                                {
                                    Guid? windowsVolumeGuid = WindowsVolumeHelper.GetWindowsVolumeGuid(m_selectedVolume);
                                    volume = new OperatingSystemVolume(windowsVolumeGuid.Value, m_selectedVolume.BytesPerSector, m_selectedVolume.Size);
                                }
                                else
                                {
                                    volume = m_selectedVolume;
                                }

                                if (parameters.ContainsKey("random"))
                                {
                                    VerifyTestPatternRandom(m_selectedVolume);
                                }
                                else
                                {
                                    long sectorsRead = 0;
                                    List<long> failedSectorList = null;
                                    Thread thread = new Thread(delegate()
                                    {
                                        failedSectorList = TestHelper.VerifyTestPattern(volume, ref sectorsRead);
                                    });
                                    thread.Start();

                                    while (thread.IsAlive)
                                    {
                                        Thread.Sleep(1000);
                                        Console.SetCursorPosition(0, Console.CursorTop);
                                        Console.Write("Progress: {0:##0.0}%", ((double)sectorsRead / volume.TotalSectors) * 100);
                                    }

                                    Console.WriteLine();
                                    Console.WriteLine();
                                    if (failedSectorList.Count == 0)
                                    {
                                        Console.WriteLine("Verification completed.");
                                    }
                                    else
                                    {
                                        Console.WriteLine("{0} sectors failed verification.", failedSectorList.Count);
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine("Invalid argument.");
                                HelpTest();
                            }
                            break;
                        }
                    case "disk":
                        {
                            if (m_selectedDisk == null)
                            {
                                Console.WriteLine("No disk has been selected.");
                                return;
                            }

                            if (args[2].ToLower() == "write")
                            {
                                Console.Write("Are you sure that you want to destroy the disk? ");
                                string input = Console.ReadLine();
                                if (input == "y")
                                {
                                    if (m_selectedDisk is PhysicalDisk)
                                    {
                                        bool success = ((PhysicalDisk)m_selectedDisk).ExclusiveLock();
                                        if (!success)
                                        {
                                            Console.WriteLine("Failed to lock the disk.");
                                            return;
                                        }
                                    }
                                    long sectorsWritten = 0;
                                    Thread thread = new Thread(delegate()
                                    {
                                        TestHelper.WriteTestPattern(m_selectedDisk, ref sectorsWritten);
                                    });
                                    thread.Start();

                                    while (thread.IsAlive)
                                    {
                                        Thread.Sleep(1000);
                                        Console.SetCursorPosition(0, Console.CursorTop);
                                        Console.Write("Progress: {0:##0.0}%", ((double)sectorsWritten / m_selectedDisk.TotalSectors) * 100);
                                    }

                                    if (m_selectedDisk is PhysicalDisk)
                                    {
                                        ((PhysicalDisk)m_selectedDisk).ReleaseLock();
                                        ((PhysicalDisk)m_selectedDisk).UpdateProperties();
                                    }
                                    Console.WriteLine();
                                    Console.WriteLine();
                                    Console.WriteLine("Operation completed.");
                                }
                            }
                            else if (args[2].ToLower() == "verify")
                            {
                                long sectorsRead = 0;
                                List<long> failedSectorList = null;
                                Thread thread = new Thread(delegate()
                                {
                                    failedSectorList = TestHelper.VerifyTestPattern(m_selectedDisk, ref sectorsRead);
                                });
                                thread.Start();

                                while (thread.IsAlive)
                                {
                                    Thread.Sleep(1000);
                                    Console.SetCursorPosition(0, Console.CursorTop);
                                    Console.Write("Progress: {0:##0.0}%", ((double)sectorsRead / m_selectedDisk.TotalSectors) * 100);
                                }

                                Console.WriteLine();
                                Console.WriteLine();
                                if (failedSectorList.Count == 0)
                                {
                                    Console.WriteLine("Verification completed.");
                                }
                                else
                                {
                                    Console.WriteLine("{0} sectors failed verification.", failedSectorList.Count);
                                }
                            }
                            else
                            {
                                Console.WriteLine("Invalid argument.");
                                HelpTest();
                            }
                            break;
                        }
                    default:
                        {
                            Console.WriteLine("Invalid argument.");
                            HelpTest();
                            break;
                        }
                }
            }
            else
            {
                Console.WriteLine("Invalid number of arguments.");
                HelpTest();
            }
        }

        private static void VerifyTestPatternRandom(Volume volume)
        {
            // volume will be read at random positions (this will test Striped / RAID-5 volume implementations)
            int transferSize = Settings.MaximumTransferSizeLBA;
            byte[] bootRecord = volume.ReadSectors(0, 1);
            long totalSectors = (long)BigEndianConverter.ToUInt64(bootRecord, 8);
            int numberOfPositions = 100;
            Random random = new Random();
            for (int index = 0; index < numberOfPositions; index++)
            {
                int sectorIndex = random.Next((int)totalSectors);

                int sectorsToRead = (int)Math.Min(transferSize, totalSectors - sectorIndex);
                byte[] buffer = volume.ReadSectors(sectorIndex, sectorsToRead);
                for (int position = 0; position < sectorsToRead; position++)
                {
                    long number = (long)BigEndianConverter.ToUInt64(buffer, position * volume.BytesPerSector);

                    long shouldBe = sectorIndex + position;
                    if (number != shouldBe)
                    {
                        Console.WriteLine("Error at sector {0}", sectorIndex + position);
                    }
                }
            }
            Console.WriteLine("{0} positions have been verified", numberOfPositions);
        }

        public static void HelpTest()
        {
            Console.WriteLine();
            Console.WriteLine("TEST DISK WRITE                      - Write test pattern to selected disk.");
            Console.WriteLine("TEST DISK VERIFY                     - Verify test pattern on selected disk.");
            Console.WriteLine("TEST VOLUME WRITE [WINAPI]           - Write test pattern to selected volume.");
            Console.WriteLine("TEST VOLUME VERIFY [WINAPI] [RANDOM] - Verify test pattern on selected volume.");
        }
    }
}
