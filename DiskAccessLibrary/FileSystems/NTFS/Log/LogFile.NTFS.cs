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
    public partial class LogFile
    {
        private const string NTFSClientName = "NTFS";
        private int? m_ntfsClientIndex;

        public NTFSRestartRecord ReadNTFSRestartRecord()
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            ulong clientRestartLsn = m_restartPage.LogRestartArea.LogClientArray[NTFSClientIndex].ClientRestartLsn;
            LogRecord record = ReadRecord(clientRestartLsn);
            if (record.RecordType == LogRecordType.ClientRestart)
            {
                return new NTFSRestartRecord(record.Data);
            }
            else
            {
                string message = String.Format("Log restart area points to a record with RecordType {0}", record.RecordType);
                throw new InvalidDataException(message);
            }
        }

        public NTFSLogRecord ReadNTFSLogRecord(ulong lsn)
        {
            LogRecord record = ReadRecord(lsn);
            if (record.RecordType == LogRecordType.ClientRecord)
            {
                return new NTFSLogRecord(record.Data);
            }
            else
            {
                return null;

            }
        }

        private int NTFSClientIndex
        {
            get
            {
                if (m_ntfsClientIndex == null)
                {
                    m_ntfsClientIndex = FindClientIndex(NTFSClientName);
                    if (m_ntfsClientIndex == -1)
                    {
                        throw new InvalidDataException("NTFS Client was not found");
                    }
                }
                return m_ntfsClientIndex.Value;
            }
        }
    }
}
