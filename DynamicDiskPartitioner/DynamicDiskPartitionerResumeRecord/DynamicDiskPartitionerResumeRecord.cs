/* Copyright (C) 2014-2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace DynamicDiskPartitioner
{
    public enum DynamicDiskPartitionerOperation : ushort
    {
        //MoveExtent = 0x0100, // MoveExtent v0
        MoveExtent = 0x0101, // MoveExtent v1
        AddDiskToArray = 0x0200,
    }

    public abstract class DynamicDiskPartitionerResumeRecord
    {
        public const int Length = 512;
        public const string ValidSignature = "DDSKPART";

        public string Signature = ValidSignature;
        public byte RecordRevision = 1; // Must be 1
        protected DynamicDiskPartitionerOperation Operation; // 2 bytes
        // reserved 5 bytes

        public DynamicDiskPartitionerResumeRecord()
        { 

        }

        public DynamicDiskPartitionerResumeRecord(byte[] buffer)
        {
            Signature = ByteReader.ReadAnsiString(buffer, 0, 8);
            RecordRevision = ByteReader.ReadByte(buffer, 8);
            Operation = (DynamicDiskPartitionerOperation)BigEndianConverter.ToUInt16(buffer, 9);

            ReadOperationParameters(buffer, 16);
        }

        protected abstract void ReadOperationParameters(byte[] buffer, int offset);

        public byte[] GetBytes(int bytesPerSector)
        {
            byte[] buffer = new byte[bytesPerSector];
            ByteWriter.WriteAnsiString(buffer, 0, Signature, 8);
            ByteWriter.WriteByte(buffer, 8, RecordRevision);
            BigEndianWriter.WriteUInt16(buffer, 9, (ushort)Operation);

            WriteOperationParameters(buffer, 16);

            return buffer;
        }

        protected abstract void WriteOperationParameters(byte[] buffer, int offset);

        public bool IsValid
        {
            get
            {
                return this.Signature == ValidSignature;
            }
        }

        public static DynamicDiskPartitionerResumeRecord FromBytes(byte[] buffer)
        {
            string signature = ByteReader.ReadAnsiString(buffer, 0, 8);
            byte recordRevision = ByteReader.ReadByte(buffer, 8);
            DynamicDiskPartitionerOperation operation = (DynamicDiskPartitionerOperation)BigEndianConverter.ToUInt16(buffer, 9);
            if (signature == ValidSignature && recordRevision == 1)
            {
                if (operation == DynamicDiskPartitionerOperation.AddDiskToArray)
                {
                    return new AddDiskOperationResumeRecord(buffer);
                }
                else if (operation == DynamicDiskPartitionerOperation.MoveExtent)
                {
                    return new MoveExtentOperationResumeRecord(buffer);
                }
            }
            return null;
        }

        public static bool HasValidSignature(byte[] buffer)
        {
            string signature = ByteReader.ReadAnsiString(buffer, 0, 8);
            return (signature == ValidSignature);
        }
    }
}
