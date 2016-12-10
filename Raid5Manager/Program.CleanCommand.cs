using System;
using System.Collections.Generic;
using System.Text;
using DiskAccessLibrary;
using Utilities;

namespace Raid5Manager
{
    public partial class Program
    {
        public static void CleanCommand(string[] args)
        {
            if (m_selectedDisk != null)
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
                    for (int index = 0; index < 63; index++)
                    {
                        byte[] bytes = new byte[m_selectedDisk.BytesPerSector];
                        m_selectedDisk.WriteSectors(index, bytes);
                    }
                    if (m_selectedDisk is PhysicalDisk)
                    {
                        ((PhysicalDisk)m_selectedDisk).ReleaseLock();
                        ((PhysicalDisk)m_selectedDisk).UpdateProperties();
                    }
                    Console.WriteLine("Clean completed.");
                }
            }
            else
            {
                Console.WriteLine("No disk has been selected");
            }
        }

        public static void HelpClean()
        {
            Console.WriteLine();
            Console.WriteLine("    Removes partitioning data from the disk.");
            Console.WriteLine();
            Console.WriteLine("Synatx: CLEAN");
        }
    }
}
