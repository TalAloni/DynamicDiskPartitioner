/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    // This is the record itself (the data that is contained in the attribute / index key)
    public class FileNameRecord
    {
        public const int FixedLength = 0x42;

        public MftSegmentReference ParentDirectory;
        public DateTime CreationTime;
        public DateTime ModificationTime;
        public DateTime MftModificationTime;
        public DateTime LastAccessTime;
        public ulong AllocatedSize; // of the file
        public ulong FileSize; // of the file
        public FileAttributes FileAttributes;
        public ushort PackedEASize;
        // ushort Reserved;
        // byte FileNameLength
        public FilenameNamespace Namespace; // Type of filename (e.g. 8.3, long filename etc.)
        public string FileName;

        public FileNameRecord(byte[] buffer, int offset)
        {
            ParentDirectory = new MftSegmentReference(buffer, offset + 0x00);
            CreationTime = StandardInformationRecord.ReadDateTime(buffer, offset + 0x08);
            ModificationTime = StandardInformationRecord.ReadDateTime(buffer, offset + 0x10);
            MftModificationTime = StandardInformationRecord.ReadDateTime(buffer, offset + 0x18);
            LastAccessTime = StandardInformationRecord.ReadDateTime(buffer, offset + 0x20);
            AllocatedSize = LittleEndianConverter.ToUInt64(buffer, offset + 0x28);
            FileSize = LittleEndianConverter.ToUInt64(buffer, offset + 0x30);
            FileAttributes = (FileAttributes)LittleEndianConverter.ToUInt32(buffer, offset + 0x38);
            PackedEASize = LittleEndianConverter.ToUInt16(buffer, offset + 0x3C);
            byte fileNameLength = ByteReader.ReadByte(buffer, offset + 0x40);
            Namespace = (FilenameNamespace)ByteReader.ReadByte(buffer, offset + 0x41);
            FileName = Encoding.Unicode.GetString(buffer, offset + 0x42, fileNameLength * 2);
        }

        public byte[] GetBytes()
        {
            byte[] buffer = new byte[this.Length];

            ParentDirectory.WriteBytes(buffer, 0x00);
            StandardInformationRecord.WriteDateTime(buffer, 0x08, CreationTime);
            StandardInformationRecord.WriteDateTime(buffer, 0x10, ModificationTime);
            StandardInformationRecord.WriteDateTime(buffer, 0x18, MftModificationTime);
            StandardInformationRecord.WriteDateTime(buffer, 0x20, LastAccessTime);
            LittleEndianWriter.WriteUInt64(buffer, 0x28, AllocatedSize);
            LittleEndianWriter.WriteUInt64(buffer, 0x30, FileSize);
            LittleEndianWriter.WriteUInt32(buffer, 0x38, (uint)FileAttributes);
            LittleEndianWriter.WriteUInt16(buffer, 0x3C, PackedEASize);
            ByteWriter.WriteByte(buffer, 0x40, (byte)FileName.Length);
            ByteWriter.WriteByte(buffer, 0x41, (byte)Namespace);
            ByteWriter.WriteBytes(buffer, 0x42, Encoding.Unicode.GetBytes(FileName));

            return buffer;
        }

        public int Length
        {
            get
            {
                return FixedLength + FileName.Length * 2;
            }
        }

        public static string ReadFileName(byte[] buffer, int offset)
        {
            byte fileNameLength = ByteReader.ReadByte(buffer, offset + 0x40);
            return Encoding.Unicode.GetString(buffer, offset + 0x42, fileNameLength * 2);
        }
    }
}
