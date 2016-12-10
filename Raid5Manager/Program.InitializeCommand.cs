using System;
using System.Collections.Generic;
using System.Text;
using DiskAccessLibrary;
using Utilities;

namespace Raid5Manager
{
    public partial class Program
    {
        public static void InitializeCommand(string[] args)
        {
            if (m_selectedDisk == null)
            {
                Console.WriteLine("No disk has been selected.");
                return;
            }

            if (args.Length >= 2)
            {
                if (args[1].ToLower() == "gpt")
                {
                    KeyValuePairList<string, string> parameters = ParseParameters(args, 2);
                    InitializeGPT(parameters);
                }
                else
                {
                    Console.WriteLine("Invalid argument.");
                    HelpInitialize();
                }
            }
            else
            {
                Console.WriteLine("Invalid number of argument.");
                HelpInitialize();
            }
        }

        public static void InitializeGPT(KeyValuePairList<string, string> parameters)
        {
            if (!VerifyParameters(parameters, "align", "msr"))
            {
                Console.WriteLine();
                Console.WriteLine("Invalid parameter.");
                HelpInitialize();
                return;
            }

            MasterBootRecord mbr = MasterBootRecord.ReadFromDisk(m_selectedDisk);
            if (mbr != null)
            {
                if (mbr.IsGPTBasedDisk)
                {
                    Console.WriteLine("The selected disk already uses the GPT partitioning scheme.");
                    return;
                }
                else
                {
                    for (int index = 0; index < mbr.PartitionTable.Length; index++)
                    {
                        PartitionTableEntry entry = mbr.PartitionTable[index];
                        if (entry.SectorCountLBA > 0)
                        {
                            Console.WriteLine("You must delete existing MBR partitions before initializing the disk as GPT.");
                            return;
                        }
                    }
                }
            }

            long alignInSectors = 0;
            if (parameters.ContainsKey("align"))
            {
                long requestedAlignInKB = Conversion.ToInt64(parameters.ValueOf("align"), 0);
                long alignInBytes = requestedAlignInKB * 1024;
                if (requestedAlignInKB <= 0 || alignInBytes % m_selectedDisk.BytesPerSector > 0)
                {
                    Console.WriteLine("Invalid ALIGN parameter (must be specified in KB and be multipe of bytes per");
                    Console.WriteLine("sector).");
                    return;
                }
                alignInSectors = alignInBytes / m_selectedDisk.BytesPerSector;
            }

            long firstUsableLBA = 34;
            if (alignInSectors > 0)
            {
                firstUsableLBA = (long)Math.Ceiling((double)34 / alignInSectors) * alignInSectors;
            }

            long requestedReservedInMB = 0;
            if (parameters.ContainsKey("msr"))
            {
                requestedReservedInMB = Conversion.ToInt64(parameters.ValueOf("msr"), 0);
                long bytesAvailable = (m_selectedDisk.TotalSectors - firstUsableLBA) * m_selectedDisk.BytesPerSector;
                if ((requestedReservedInMB < 1) || (requestedReservedInMB * 1024 * 1024 > bytesAvailable))
                {
                    Console.WriteLine("Invalid MSR parameter (must be specified in MB).");
                    return;
                }
            }

            if (m_selectedDisk is PhysicalDisk)
            {
                bool success = ((PhysicalDisk)m_selectedDisk).ExclusiveLock();
                if (!success)
                {
                    Console.WriteLine("Failed to lock the disk.");
                    return;
                }
            }

            long reservedPartitionSizeLBA = requestedReservedInMB * 1024 * 1024 / m_selectedDisk.BytesPerSector;
            GuidPartitionTable.InitializeDisk(m_selectedDisk, firstUsableLBA, reservedPartitionSizeLBA);

            if (m_selectedDisk is PhysicalDisk)
            {
                ((PhysicalDisk)m_selectedDisk).ReleaseLock();
                ((PhysicalDisk)m_selectedDisk).UpdateProperties();
            }
            Console.WriteLine("Operation completed.");
        }

        public static void HelpInitialize()
        {
            Console.WriteLine();
            Console.WriteLine("    Initializes a new disk.");
            Console.WriteLine();
            Console.WriteLine("Syntax: INITIALIZE GPT [ALIGN=<N>] [MSR=<N>]");
            Console.WriteLine();
            Console.WriteLine("    ALIGN=<N>   The first partition start offset will be a multiple of <N>.");
            Console.WriteLine("                Must be specified in kilobytes (KB), and without any suffixes.");
            Console.WriteLine();
            Console.WriteLine("    MSR=<N>     The size of the Microsoft Reserved partition in megabytes (MB).");
            Console.WriteLine();
        }
    }
}
