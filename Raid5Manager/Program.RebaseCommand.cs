using System;
using System.Collections.Generic;
using System.Text;
using DiskAccessLibrary;
using Utilities;

namespace Raid5Manager
{
    public partial class Program
    {
        public static void RebaseCommand(string[] args)
        {
            if (m_selectedDisk == null)
            {
                Console.WriteLine("No disk has been selected.");
                return;
            }

            MasterBootRecord mbr = MasterBootRecord.ReadFromDisk(m_selectedDisk);
            if (mbr != null)
            {
                if (mbr.IsGPTBasedDisk)
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
                    GuidPartitionTable.RebaseDisk(m_selectedDisk, mbr);
                    if (m_selectedDisk is PhysicalDisk)
                    {
                        ((PhysicalDisk)m_selectedDisk).ReleaseLock();
                        ((PhysicalDisk)m_selectedDisk).UpdateProperties();
                    }
                    Console.WriteLine("Operation completed.");
                }
                else
                {
                    Console.WriteLine("Disk does not contain GUID partition table.");
                }
            }
            else
            {
                Console.WriteLine("Invalid MBR.");
            }
        }

        public static void HelpRebase()
        {
            Console.WriteLine();
            Console.WriteLine("    Rewrite the primary and secondary GPT to the correct locations at the");
            Console.WriteLine("    beginning and end of the disk.");
            Console.WriteLine();
            Console.WriteLine("Syntax: REBASE");
            Console.WriteLine();
        }
    }
}
