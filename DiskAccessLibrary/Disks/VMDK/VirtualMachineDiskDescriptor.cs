/* Copyright (C) 2014-2023 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utilities;

namespace DiskAccessLibrary.VMDK
{
    public class VirtualMachineDiskDescriptor
    {
        public int Version;
        public uint ContentID;
        public uint ParentContentID;
        public VirtualMachineDiskType DiskType;
        public List<VirtualMachineDiskExtentEntry> ExtentEntries;
        public string Adapter;
        public long Cylinders;
        public int TracksPerCylinder; // heads
        public int SectorsPerTrack;

        protected VirtualMachineDiskDescriptor()
        {
            Version = 1;
            ContentID = (uint)new Random().Next();
            ParentContentID = 0xFFFFFFFF;
            ExtentEntries = new List<VirtualMachineDiskExtentEntry>();
        }

        public VirtualMachineDiskDescriptor(List<string> lines)
        {
            ParseDescriptor(lines);
        }

        private void ParseDescriptor(List<string> lines)
        {
            ExtentEntries = new List<VirtualMachineDiskExtentEntry>();

            foreach (string line in lines)
            {
                if (line.StartsWith("version", StringComparison.InvariantCultureIgnoreCase))
                {
                    string value = line.Substring(line.IndexOf('=') + 1).Trim();
                    Version = Conversion.ToInt32(value);
                }
                else if (line.StartsWith("CID", StringComparison.InvariantCultureIgnoreCase))
                {
                    string value = line.Substring(line.IndexOf('=') + 1).Trim();
                    ContentID = UInt32.Parse(value, System.Globalization.NumberStyles.HexNumber);
                }
                else if (line.StartsWith("ParentCID", StringComparison.InvariantCultureIgnoreCase))
                {
                    string value = line.Substring(line.IndexOf('=') + 1).Trim();
                    ParentContentID = UInt32.Parse(value, System.Globalization.NumberStyles.HexNumber);
                }
                else if (line.StartsWith("createType", StringComparison.InvariantCultureIgnoreCase))
                {
                    string value = line.Substring(line.IndexOf('=') + 1).Trim();
                    value = QuotedStringUtils.Unquote(value);
                    DiskType = GetFromString(value);
                }
                else if (line.StartsWith("RW", StringComparison.InvariantCultureIgnoreCase) ||
                         line.StartsWith("RDONLY", StringComparison.InvariantCultureIgnoreCase) ||
                         line.StartsWith("NOACCESS", StringComparison.InvariantCultureIgnoreCase))
                {
                    VirtualMachineDiskExtentEntry entry = VirtualMachineDiskExtentEntry.ParseEntry(line);
                    ExtentEntries.Add(entry);
                }
                else if (line.StartsWith("ddb.adapterType", StringComparison.InvariantCultureIgnoreCase))
                {
                    string value = line.Substring(line.IndexOf('=') + 1).Trim();
                    value = QuotedStringUtils.Unquote(value);
                    Adapter = value;
                }
                else if (line.StartsWith("ddb.geometry.sectors", StringComparison.InvariantCultureIgnoreCase))
                {
                    string value = line.Substring(line.IndexOf('=') + 1).Trim();
                    value = QuotedStringUtils.Unquote(value);
                    SectorsPerTrack = Conversion.ToInt32(value);
                }
                else if (line.StartsWith("ddb.geometry.heads", StringComparison.InvariantCultureIgnoreCase))
                {
                    string value = line.Substring(line.IndexOf('=') + 1).Trim();
                    value = QuotedStringUtils.Unquote(value);
                    TracksPerCylinder = Conversion.ToInt32(value);
                }
                else if (line.StartsWith("ddb.geometry.cylinders", StringComparison.InvariantCultureIgnoreCase))
                {
                    string value = line.Substring(line.IndexOf('=') + 1).Trim();
                    value = QuotedStringUtils.Unquote(value);
                    Cylinders = Conversion.ToInt64(value);
                }
            }
        }

        public void UpdateExtentEntries(List<string> lines)
        {
            int startIndex = -1;
            // Remove previous extent entries
            for (int index = 0; index < lines.Count; index++)
            {
                string line = lines[index];
                if (line.StartsWith("RW", StringComparison.InvariantCultureIgnoreCase) ||
                    line.StartsWith("RDONLY", StringComparison.InvariantCultureIgnoreCase) ||
                    line.StartsWith("NOACCESS", StringComparison.InvariantCultureIgnoreCase))
                {
                    if (startIndex == -1)
                    {
                        startIndex = index;
                    }
                    lines.RemoveAt(index);
                    index--;
                }
            }

            if (startIndex >= 0)
            {
                foreach (VirtualMachineDiskExtentEntry entry in ExtentEntries)
                {
                    lines.Insert(startIndex, entry.GetEntryLine());
                    startIndex++;
                }
            }
        }

        public static VirtualMachineDiskDescriptor ReadFromFile(string descriptorPath)
        {
            List<string> lines = ReadASCIITextLines(descriptorPath);
            if (lines == null)
            {
                return null;
            }

            return new VirtualMachineDiskDescriptor(lines);
        }

        public string GetDescriptorText()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendFormat("# Disk DescriptorFile\n");
            builder.AppendFormat("version={0}\n", Version);
            builder.AppendFormat("encoding=\"{0}\"\n", "windows-1252");
            builder.AppendFormat("CID={0}\n", ContentID.ToString("x8"));
            builder.AppendFormat("parentCID={0}\n", ParentContentID.ToString("x8"));
            builder.AppendFormat("createType=\"{0}\"\n", GetVirtualMachineDiskTypeString(DiskType));

            builder.Append("\n");
            builder.Append("# Extent description\n");
            foreach (VirtualMachineDiskExtentEntry entry in ExtentEntries)
            {
                builder.AppendLine(entry.GetEntryLine());
            }

            builder.Append("\n");
            builder.Append("# The Disk Data Base \n");
            builder.Append("#DDB\n");
            builder.Append("\n");
            builder.AppendFormat("ddb.adapterType = \"{0}\"\n", Adapter);
            builder.AppendFormat("ddb.geometry.cylinders = \"{0}\"\n", Cylinders);
            builder.AppendFormat("ddb.geometry.heads = \"{0}\"\n", TracksPerCylinder);
            builder.AppendFormat("ddb.geometry.sectors = \"{0}\"\n", SectorsPerTrack);

            return builder.ToString();
        }

        public void SaveToFile(string path)
        {
            string text = GetDescriptorText();
            File.WriteAllText(path, text, Encoding.ASCII);
        }

        private static VirtualMachineDiskType GetFromString(string createType)
        {
            switch (createType.ToLower())
            {
                case "custom":
                    return VirtualMachineDiskType.Custom;
                case "monolithicsparse":
                    return VirtualMachineDiskType.MonolithicSparse;
                case "monolithicflat":
                    return VirtualMachineDiskType.MonolithicFlat;
                case "2Gbmaxextentsparse":
                    return VirtualMachineDiskType.TwoGbMaxExtentSparse;
                case "2Gbmaxextentflat":
                    return VirtualMachineDiskType.TwoGbMaxExtentFlat;
                case "fulldevice":
                    return VirtualMachineDiskType.FullDevice;
                case "partitioneddevice":
                    return VirtualMachineDiskType.PartitionedDevice;
                case "vmfspreallocated":
                    return VirtualMachineDiskType.VmfsPreallocated;
                case "vmfseagerzeroedthick":
                    return VirtualMachineDiskType.VmfsEagerZeroedThick;
                case "vmfsthin":
                    return VirtualMachineDiskType.VmfsThin;
                case "vmfssparse":
                    return VirtualMachineDiskType.VmfsSparse;
                case "vmfsrdm":
                    return VirtualMachineDiskType.VmfsRDM;
                case "vmfsRDMP":
                    return VirtualMachineDiskType.VmfsRDMP;
                case "vmfsraw":
                    return VirtualMachineDiskType.VmfsRaw;
                case "streamoptimized":
                    return VirtualMachineDiskType.StreamOptimized;
                default:
                    return VirtualMachineDiskType.Custom;
            }
        }

        private static string GetVirtualMachineDiskTypeString(VirtualMachineDiskType diskType)
        {
            switch (diskType)
            {
                case VirtualMachineDiskType.Custom:
                    return "custom";
                case VirtualMachineDiskType.MonolithicSparse:
                    return "monolithicSparse";
                case VirtualMachineDiskType.MonolithicFlat:
                    return "monolithicFlat";
                case VirtualMachineDiskType.TwoGbMaxExtentSparse:
                    return "twoGbMaxExtentSparse";
                case VirtualMachineDiskType.TwoGbMaxExtentFlat:
                    return "twoGbMaxExtentFlat";
                case VirtualMachineDiskType.FullDevice:
                    return "fullDevice";
                case VirtualMachineDiskType.PartitionedDevice:
                    return "partitionedDevice";
                case VirtualMachineDiskType.VmfsPreallocated:
                    return "vmfsPreallocated";
                case VirtualMachineDiskType.VmfsEagerZeroedThick:
                    return "vmfsEagerZeroedThick";
                case VirtualMachineDiskType.VmfsThin:
                    return "vmfsThin";
                case VirtualMachineDiskType.VmfsSparse:
                    return "vmfsSparse";
                case VirtualMachineDiskType.VmfsRDM:
                    return "vmfsRDM";
                case VirtualMachineDiskType.VmfsRDMP:
                    return "vmfsRDMP";
                case VirtualMachineDiskType.VmfsRaw:
                    return "vmfsRaw";
                case VirtualMachineDiskType.StreamOptimized:
                    return "streamOptimized";
                default:
                    return "custom";
            }
        }

        public static List<string> ReadASCIITextLines(string path)
        {
            string text = ReadASCIIText(path);

            if (text == null)
            {
                return null;
            }

            return GetLines(text);
        }

        public static List<string> GetLines(string text)
        {
            List<string> result = new List<string>();
            StringReader reader = new StringReader(text);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                result.Add(line);
            }

            return result;
        }

        public static string ReadASCIIText(string path)
        {
            StringBuilder builder = new StringBuilder();
            FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read);
            int bufferSize = 0x1000; // default FileStream buffer size
            byte[] buffer = new byte[bufferSize];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            string temp = ASCIIEncoding.ASCII.GetString(buffer, 0, bytesRead);
            while (bytesRead > 0)
            {
                foreach (char c in temp)
                {
                    if (char.IsControl(c) && c != '\r' && c != '\n')
                    {
                        stream.Close();
                        return null;
                    }
                }
                builder.Append(temp);

                if (bytesRead == bufferSize)
                {
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                    temp = ASCIIEncoding.ASCII.GetString(buffer, 0, bytesRead);
                }
                else
                {
                    break;
                }
            }
            stream.Close();
            return builder.ToString();
        }

        public static VirtualMachineDiskDescriptor CreateMonolithicFlatDescriptor(long size)
        {
            VirtualMachineDiskDescriptor descriptor = new VirtualMachineDiskDescriptor();
            descriptor.DiskType = VirtualMachineDiskType.MonolithicFlat;
            descriptor.Adapter = "lsilogic";
            byte heads;
            byte sectorsPerTrack;
            ushort cylinders;
            VirtualHardDisk.GetDiskGeometry((ulong)size / VirtualMachineDisk.BytesPerDiskSector, out heads, out sectorsPerTrack, out cylinders);
            descriptor.Cylinders = cylinders;
            descriptor.TracksPerCylinder = heads;
            descriptor.SectorsPerTrack = sectorsPerTrack;
            return descriptor;
        }
    }
}
