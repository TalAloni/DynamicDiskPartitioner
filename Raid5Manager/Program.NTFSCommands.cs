using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DiskAccessLibrary.FileSystems;
using DiskAccessLibrary.FileSystems.NTFS;
using DiskAccessLibrary.LogicalDiskManager;
using Utilities;

namespace Raid5Manager
{
    public partial class Program
    {
        public static void InfoCommand(string[] args)
        {
            if (m_selectedVolume != null)
            {
                if (m_selectedVolume is DynamicVolume)
                {
                    if (!((DynamicVolume)m_selectedVolume).IsOperational)
                    {
                        Console.WriteLine("Volume is not operational");
                        return;
                    }
                }

                NTFSVolume ntfsVolume = new NTFSVolume(m_selectedVolume);
                if (ntfsVolume.IsValidAndSupported)
                {
                    string path = SelectedDirectory + args[1];
                    FileRecord record = ntfsVolume.GetFileRecord(path);
                    if (record != null)
                    {
                        foreach (AttributeRecord attributeRecord in record.Attributes)
                        {
                            Console.WriteLine("Attribute ID: {0}", attributeRecord.AttributeID);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Invalid NTFS partition.");
                }
            }
        }
    }
}
