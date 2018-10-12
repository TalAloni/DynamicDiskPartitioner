/* Copyright (C) 2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
    public partial class LogFile
    {
        public LogRecord FindNextRecord(LogRecord record, int clientIndex)
        {
            do
            {
                ulong nextLsn = CalculateNextLsn(record.ThisLsn, record.Length);
                try
                {
                    record = ReadRecord(nextLsn);
                }
                catch
                {
                    return null;
                }

                ushort clientSeqNumber = m_restartPage.LogRestartArea.LogClientArray[clientIndex].SeqNumber;
                if (record.ClientIndex == clientIndex && record.ClientSeqNumber == clientSeqNumber)
                {
                    return record;
                }
            }
            while (true);
        }

        public List<LogRecord> FindNextRecords(ulong lsn, int clientIndex)
        {
            LogRecord record = ReadRecord(lsn);
            List<LogRecord> result = new List<LogRecord>();
            do
            {
                record = FindNextRecord(record, clientIndex);
                if (record != null)
                {
                    result.Add(record);
                }
            }
            while (record != null);
            return result;
        }
    }
}
