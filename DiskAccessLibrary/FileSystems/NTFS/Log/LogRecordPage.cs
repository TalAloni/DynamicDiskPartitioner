/* Copyright (C) 2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    /// <remarks>
    /// This record should be read according to the version number specified in LogRestartPage.
    /// v1.0 use the LFS_UNPACKED_RECORD_PAGE structure, which is not being used in later versions.
    /// </remarks>
    public class LogRecordPage
    {
        private const string ValidSignature = "RCRD";
        private const int UpdateSequenceArrayOffset = 0x28;

        /* Start of LFS_RECORD_PAGE_HEADER */
        // MULTI_SECTOR_HEADER
        public ulong LastLsnOrFileOffset; // Last LSN that starts on this page for regular log pages, FileOffset for tail copies (indicates the location in the file where the page should be placed)
        public LogRecordPageFlags Flags;
        public ushort PageCount; // Number of pages written as part of the IO transfer. a MultiPage record is likely to be written in two separate IO transfers (since the last page may have room for more records that will be written in a later transfer)
        public ushort PagePosition; // One-based
        /* Start of LFS_PACKED_RECORD_PAGE */
        public ushort NextRecordOffset; // The offset of the free space in the page, if the last record has MultiPage flag set this value is not incremented and will point to the start of the record.
        // ushort WordAlign
        // uint DWordAlign
        public ulong LastEndLsn; // Last LSN that ends on this page
        public ushort UpdateSequenceNumber; // a.k.a. USN
        // byte[] UpdateSequenceReplacementData
        /* End of LFS_PACKED_RECORD_PAGE */
        /* End of LFS_RECORD_PAGE_HEADER */
        public byte[] Data;

        public LogRecordPage()
        {
            Data = new byte[0];
        }

        public LogRecordPage(byte[] pageBytes, int dataOffset)
        {
            MultiSectorHeader multiSectorHeader = new MultiSectorHeader(pageBytes, 0x00);
            if (multiSectorHeader.Signature != ValidSignature)
            {
                throw new InvalidDataException("Invalid RCRD record signature");
            }
            LastLsnOrFileOffset = LittleEndianConverter.ToUInt64(pageBytes, 0x08);
            Flags = (LogRecordPageFlags)LittleEndianConverter.ToUInt32(pageBytes, 0x10);
            PageCount = LittleEndianConverter.ToUInt16(pageBytes, 0x14);
            PagePosition = LittleEndianConverter.ToUInt16(pageBytes, 0x16);
            NextRecordOffset = LittleEndianConverter.ToUInt16(pageBytes, 0x18);
            LastEndLsn = LittleEndianConverter.ToUInt64(pageBytes, 0x20);
            int position = multiSectorHeader.UpdateSequenceArrayOffset;
            List<byte[]> updateSequenceReplacementData = MultiSectorHelper.ReadUpdateSequenceArray(pageBytes, position, multiSectorHeader.UpdateSequenceArraySize, out UpdateSequenceNumber);
            MultiSectorHelper.DecodeSegmentBuffer(pageBytes, 0, UpdateSequenceNumber, updateSequenceReplacementData);
            Data = ByteReader.ReadBytes(pageBytes, dataOffset, pageBytes.Length - dataOffset);
        }

        public byte[] GetBytes(int bytesPerLogPage, int dataOffset)
        {
            int strideCount = bytesPerLogPage / MultiSectorHelper.BytesPerStride;
            ushort updateSequenceArraySize = (ushort)(1 + strideCount);
            MultiSectorHeader multiSectorHeader = new MultiSectorHeader(ValidSignature, UpdateSequenceArrayOffset, updateSequenceArraySize);

            byte[] buffer = new byte[bytesPerLogPage];
            multiSectorHeader.WriteBytes(buffer, 0);
            LittleEndianWriter.WriteUInt64(buffer, 0x08, LastLsnOrFileOffset);
            LittleEndianWriter.WriteUInt32(buffer, 0x10, (uint)Flags);
            LittleEndianWriter.WriteUInt16(buffer, 0x14, PageCount);
            LittleEndianWriter.WriteUInt16(buffer, 0x16, PagePosition);
            LittleEndianWriter.WriteUInt16(buffer, 0x18, NextRecordOffset);
            LittleEndianWriter.WriteUInt64(buffer, 0x20, LastEndLsn);
            ByteWriter.WriteBytes(buffer, dataOffset, Data);

            // Write UpdateSequenceNumber and UpdateSequenceReplacementData
            List<byte[]> updateSequenceReplacementData = MultiSectorHelper.EncodeSegmentBuffer(buffer, 0, bytesPerLogPage, UpdateSequenceNumber);
            MultiSectorHelper.WriteUpdateSequenceArray(buffer, UpdateSequenceArrayOffset, updateSequenceArraySize, UpdateSequenceNumber, updateSequenceReplacementData);
            return buffer;
        }

        public LogRecord ReadRecord(int recordOffset, int dataOffset)
        {
            return new LogRecord(Data, recordOffset - dataOffset);
        }

        public byte[] ReadBytes(int recordOffset, int recordLength, int dataOffset)
        {
            return ByteReader.ReadBytes(Data, recordOffset - dataOffset, recordLength);
        }

        public bool HasRecordEnd
        {
            get
            {
                return (Flags & LogRecordPageFlags.RecordEnd) != 0;
            }
            set
            {
                if (value)
                {
                    Flags |= LogRecordPageFlags.RecordEnd;
                }
                else
                {
                    Flags &= ~LogRecordPageFlags.RecordEnd;
                }
            }
        }

        public static int GetDataOffset(int bytesPerLogPage)
        {
            int strideCount = bytesPerLogPage / MultiSectorHelper.BytesPerStride;
            ushort updateSequenceArraySize = (ushort)(1 + strideCount);
            return (int)Math.Ceiling((double)(UpdateSequenceArrayOffset + updateSequenceArraySize * 2) / 8) * 8;
        }
    }
}
