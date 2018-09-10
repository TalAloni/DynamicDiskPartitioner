/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    public class NTFSFile
    {
        private NTFSVolume m_volume;
        private FileRecord m_fileRecord;
        private AttributeData m_data;
        private BitmapData m_bitmap;

        public NTFSFile(NTFSVolume volume, long baseSegmentNumber)
        {
            m_volume = volume;
            m_fileRecord = m_volume.MasterFileTable.GetFileRecord(baseSegmentNumber);
        }

        public NTFSFile(NTFSVolume volume, FileRecord fileRecord)
        {
            m_volume = volume;
            m_fileRecord = fileRecord;
        }

        public byte[] ReadData(ulong offset, int length)
        {
            return this.Data.ReadBytes(offset, length);
        }

        public void WriteData(ulong offset, byte[] data)
        {
            this.Data.WriteBytes(offset, data);
        }

        public void SetLength(ulong newLengthInBytes)
        {
            if (newLengthInBytes > this.Data.Length)
            {
                ulong additionalLengthInBytes = newLengthInBytes - this.Data.Length;
                this.Data.Extend(additionalLengthInBytes);
            }
            else if (newLengthInBytes < this.Data.Length)
            {
                this.Data.Truncate(newLengthInBytes);
            }
            else
            {
                return;
            }

            if (m_fileRecord.LongFileNameRecord != null)
            {
                m_fileRecord.LongFileNameRecord.AllocatedLength = this.Data.AllocatedLength;
                m_fileRecord.LongFileNameRecord.FileSize = this.Data.Length;
            }
            if (m_fileRecord.ShortFileNameRecord != null)
            {
                m_fileRecord.ShortFileNameRecord.AllocatedLength = this.Data.AllocatedLength;
                m_fileRecord.ShortFileNameRecord.FileSize = this.Data.Length;
            }
            // Note that directory indexes are not being updated ATM
            m_volume.MasterFileTable.UpdateFileRecord(m_fileRecord);
        }

        public AttributeData Data
        {
            get
            {
                if (m_data == null)
                {
                    AttributeRecord record = m_fileRecord.DataRecord;
                    if (record != null)
                    {
                        m_data = new AttributeData(m_volume, m_fileRecord, record);
                    }
                }
                return m_data;
            }
        }

        public BitmapData Bitmap
        {
            get
            {
                if (m_bitmap == null)
                {
                    AttributeRecord record = m_fileRecord.BitmapRecord;
                    if (record != null)
                    {
                        long numberOfUsableBits = (long)(Data.Length / (uint)m_volume.BytesPerFileRecordSegment);
                        m_bitmap = new BitmapData(m_volume, m_fileRecord, record, numberOfUsableBits);
                    }
                }
                return m_bitmap;
            }
        }

        public NTFSVolume Volume
        {
            get
            {
                return m_volume;
            }
        }

        public FileRecord FileRecord
        {
            get
            {
                return m_fileRecord;
            }
        }

        public ulong Length
        {
            get
            {
                return this.Data.Length;
            }
        }
    }
}
