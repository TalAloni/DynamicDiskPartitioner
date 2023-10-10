/* Copyright (C) 2014-2023 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.IO;
using Utilities;

namespace DiskAccessLibrary.VMDK
{
    public class SparseExtentHeader
    {
        private const string ValidSignature = "KDMV";

        public const int Length = 512;
        public const int NumberOfGrainTableEntriesPerGrainTable = 512; // Each grain directory entry points to a level 1 page called grain table

        public string Signature; // MagicNumber
        public uint Version;
        public SparseExtentHeaderFlags Flags;
        public ulong Capacity; // Should be a multiple of the grain size
        public ulong GrainSize; // Expressed in sectors
        public ulong DescriptorOffset; // Expressed in sectors
        public ulong DescriptorSize; // Expressed in sectors
        public uint NumGTEsPerGT; // The number of entries in a grain table, must be 512 according to VMDK specs
        public ulong RedundantGDOffset; // Expressed in sectors
        public ulong GDOffset; // Expressed in sectors
        public ulong OverHead; // The number of sectors occupied by the metadata (Header, descriptor, grain directory & grain tables)
        public bool UncleanShutdown; // Stored as byte 
        public char SingleEndLineChar;
        public char NonEndLineChar;
        public char DoubleEndLineChar1;
        public char DoubleEndLineChar2;
        public SparseExtentCompression CompressionAlgirithm;

        public SparseExtentHeader(ulong totalSectors, ulong grainSize, ulong descriptorSizeInSectors)
        {
            if (totalSectors % grainSize != 0)
            {
                throw new ArgumentException("totalSectors should be a multiple of grainSize");
            }

            Signature = ValidSignature;
            Version = 1;
            Flags = SparseExtentHeaderFlags.ValidNewLineDetectionTest | SparseExtentHeaderFlags.HasRedundantGrainTable;
            Capacity = totalSectors;
            GrainSize = grainSize;
            DescriptorOffset = 1;
            DescriptorSize = descriptorSizeInSectors;
            RedundantGDOffset = 1 + DescriptorSize;
            GDOffset = 1 + DescriptorSize;
            NumGTEsPerGT = NumberOfGrainTableEntriesPerGrainTable; 
            SingleEndLineChar = '\n';
            NonEndLineChar = ' ';
            DoubleEndLineChar1 = '\r';
            DoubleEndLineChar2 = '\n';
            CompressionAlgirithm = SparseExtentCompression.None;
        }

        public SparseExtentHeader(byte[] buffer)
        {
            Signature = ByteReader.ReadAnsiString(buffer, 0x00, 4);
            if (!String.Equals(Signature, ValidSignature))
            {
                throw new InvalidDataException("Sparse extent header signature is invalid");
            }
            Version = LittleEndianConverter.ToUInt32(buffer, 0x04);
            Flags = (SparseExtentHeaderFlags)LittleEndianConverter.ToUInt32(buffer, 0x08);
            Capacity = LittleEndianConverter.ToUInt64(buffer, 0x0C);
            GrainSize = LittleEndianConverter.ToUInt64(buffer, 0x14);
            DescriptorOffset = LittleEndianConverter.ToUInt64(buffer, 0x1C);
            DescriptorSize = LittleEndianConverter.ToUInt64(buffer, 0x24);
            NumGTEsPerGT = LittleEndianConverter.ToUInt32(buffer, 0x2C);
            RedundantGDOffset = LittleEndianConverter.ToUInt64(buffer, 0x30);
            GDOffset = LittleEndianConverter.ToUInt64(buffer, 0x38);
            OverHead = LittleEndianConverter.ToUInt64(buffer, 0x40);
            UncleanShutdown = ByteReader.ReadByte(buffer, 0x48) == 1;
            SingleEndLineChar = (char)ByteReader.ReadByte(buffer, 0x49);
            NonEndLineChar = (char)ByteReader.ReadByte(buffer, 0x4A);
            DoubleEndLineChar1 = (char)ByteReader.ReadByte(buffer, 0x4B);
            DoubleEndLineChar2 = (char)ByteReader.ReadByte(buffer, 0x4C);
            CompressionAlgirithm = (SparseExtentCompression)LittleEndianConverter.ToUInt16(buffer, 0x4D);
        }

        public byte[] GetBytes()
        {
            byte[] buffer = new byte[Length];
            ByteWriter.WriteAnsiString(buffer, 0, ValidSignature);
            LittleEndianWriter.WriteUInt32(buffer, 0x04, Version);
            LittleEndianWriter.WriteUInt32(buffer, 0x08, (uint)Flags);
            LittleEndianWriter.WriteUInt64(buffer, 0x0C, Capacity);
            LittleEndianWriter.WriteUInt64(buffer, 0x14, GrainSize);
            LittleEndianWriter.WriteUInt64(buffer, 0x1C, DescriptorOffset);
            LittleEndianWriter.WriteUInt64(buffer, 0x24, DescriptorSize);
            LittleEndianWriter.WriteUInt32(buffer, 0x2C, NumGTEsPerGT);
            LittleEndianWriter.WriteUInt64(buffer, 0x30, RedundantGDOffset);
            LittleEndianWriter.WriteUInt64(buffer, 0x38, GDOffset);
            LittleEndianWriter.WriteUInt64(buffer, 0x40, OverHead);
            ByteWriter.WriteByte(buffer, 0x48, Convert.ToByte(UncleanShutdown));
            ByteWriter.WriteByte(buffer, 0x49, (byte)SingleEndLineChar);
            ByteWriter.WriteByte(buffer, 0x4A, (byte)NonEndLineChar);
            ByteWriter.WriteByte(buffer, 0x4B, (byte)DoubleEndLineChar1);
            ByteWriter.WriteByte(buffer, 0x4C, (byte)DoubleEndLineChar2);
            LittleEndianWriter.WriteUInt16(buffer, 0x4D, (ushort)CompressionAlgirithm);

            return buffer;
        }

        public bool IsSupported
        {
            get
            {
                return (Version == 1);
            }
        }

        public bool HasRedundantGrainTable
        {
            get
            {
                return (Flags & SparseExtentHeaderFlags.HasRedundantGrainTable) > 0;
            }
        }
    }
}
