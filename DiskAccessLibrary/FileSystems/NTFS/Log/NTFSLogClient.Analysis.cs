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
    public partial class NTFSLogClient
    {
        public LogRecord FindNextRecord(LogRecord record)
        {
            return m_logFile.FindNextRecord(record, m_clientIndex);
        }

        public List<LogRecord> FindRecordsFollowingCurrentCheckpoint()
        {
            NTFSRestartRecord restartRecord = ReadCurrentRestartRecord();
            return FindRecordsFollowingCheckpoint(restartRecord);
        }

        public List<LogRecord> FindRecordsFollowingCheckpoint(NTFSRestartRecord restartRecord)
        {
            return m_logFile.FindNextRecords(restartRecord.StartOfCheckpointLsn, m_clientIndex);
        }

        public List<NTFSLogRecord> FindRecordsToRedo()
        {
            NTFSRestartRecord restartRecord = ReadCurrentRestartRecord();
            List<DirtyPageEntry> dirtyPageTable = ReadDirtyPageTable(restartRecord);
            ulong redoLsn = FindRedoLsn(dirtyPageTable);
            LogRecord firstRecord = m_logFile.ReadRecord(redoLsn);
            List<LogRecord> records = m_logFile.FindNextRecords(redoLsn, m_clientIndex);
            records.Insert(0, firstRecord);

            List<NTFSLogRecord> result = new List<NTFSLogRecord>();
            foreach (LogRecord record in records)
            {
                if (record.RecordType == LogRecordType.ClientRecord)
                {
                    NTFSLogRecord clientRecord = new NTFSLogRecord(record.Data);
                    switch (clientRecord.RedoOperation)
                    {
                        case NTFSLogOperation.Noop:
                        case NTFSLogOperation.OpenAttributeTableDump:
                        case NTFSLogOperation.AttributeNamesDump:
                        case NTFSLogOperation.DirtyPageTableDump:
                        case NTFSLogOperation.TransactionTableDump:
                            {
                                continue;
                            }
                        default:
                            {
                                result.Add(clientRecord);
                                break;
                            }
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Analysis pass:
        /// - NTFS scans forward in log file from beginning of last checkpoint
        /// - Updates transaction/dirty page tables it copied in memory
        /// - NTFS scans tables for oldest update record of a non-committed transactions.
        /// </summary>
        private ulong FindRedoLsn(List<DirtyPageEntry> dirtyPageTable)
        {
            List<LogRecord> records = FindRecordsFollowingCurrentCheckpoint();
            ulong redoLsn = records[0].ThisLsn;

            if (dirtyPageTable != null)
            {
                foreach (DirtyPageEntry entry in dirtyPageTable)
                {
                    if (entry.OldestLsn < redoLsn)
                    {
                        redoLsn = entry.OldestLsn;
                    }
                }
            }

            return redoLsn;
        }
    }
}
