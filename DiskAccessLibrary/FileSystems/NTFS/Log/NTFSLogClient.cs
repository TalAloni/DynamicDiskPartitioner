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
        private const string ClientName = "NTFS";

        private LogFile m_logFile;
        private int m_clientIndex;
        private uint m_majorVersion; // For write purposes only
        private uint m_minorVersion; // For write purposes only
        private ulong m_lastClientLsn = 0;
        private ulong m_lastLsnToUndo = 0;
        private KeyValuePairList<MftSegmentReference, AttributeRecord> m_openAttributes = new KeyValuePairList<MftSegmentReference, AttributeRecord>();

        public NTFSLogClient(LogFile logFile)
        {
            m_logFile = logFile;
            m_clientIndex = m_logFile.FindClientIndex(ClientName);
            if (m_clientIndex == -1)
            {
                throw new InvalidDataException("NTFS Client was not found");
            }
            ulong lastClientRestartLsn = m_logFile.GetClientRecord(m_clientIndex).ClientRestartLsn;
            m_lastClientLsn = lastClientRestartLsn;
            NTFSRestartRecord currentRestartRecord = ReadRestartRecord(lastClientRestartLsn);
            m_majorVersion = currentRestartRecord.MajorVersion;
            m_minorVersion = currentRestartRecord.MinorVersion;
        }

        public NTFSRestartRecord ReadCurrentRestartRecord()
        {
            ulong clientRestartLsn = m_logFile.GetClientRecord(m_clientIndex).ClientRestartLsn;
            return ReadRestartRecord(clientRestartLsn);
        }

        public List<AttributeNameEntry> ReadCurrentAttributeNamesTable()
        {
            NTFSRestartRecord restartRecord = ReadCurrentRestartRecord();
            return ReadAttributeNamesTable(restartRecord);
        }

        public List<OpenAttributeEntry> ReadCurrentOpenAttributeTable()
        {
            NTFSRestartRecord restartRecord = ReadCurrentRestartRecord();
            return ReadOpenAttributeTable(restartRecord);
        }

        public List<DirtyPageEntry> ReadCurrentDirtyPageTable()
        {
            NTFSRestartRecord restartRecord = ReadCurrentRestartRecord();
            return ReadDirtyPageTable(restartRecord);
        }

        public List<TransactionEntry> ReadCurrentTransactionTable()
        {
            NTFSRestartRecord restartRecord = ReadCurrentRestartRecord();
            return ReadTransactionTable(restartRecord);
        }

        public NTFSRestartRecord ReadRestartRecord(ulong clientRestartLsn)
        {
            LogRecord record = m_logFile.ReadRecord(clientRestartLsn);
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

        public List<AttributeNameEntry> ReadAttributeNamesTable(NTFSRestartRecord restartRecord)
        {
            ulong attributeNamesLsn = restartRecord.AttributeNamesLsn;
            if (attributeNamesLsn != 0)
            {
                NTFSLogRecord record = ReadLogRecord(attributeNamesLsn);
                if (record.RedoOperation != NTFSLogOperation.AttributeNamesDump)
                {
                    string message = String.Format("Restart record AttributeNamesLsn points to a record with RedoOperation {0}", record.RedoOperation);
                    throw new InvalidDataException(message);
                }

                if (restartRecord.AttributeNamesLength != record.RedoData.Length)
                {
                    throw new InvalidDataException("Open attribute table length does not match restart record");
                }

                return AttributeNameEntry.ReadTable(record.RedoData);
            }
            else
            {
                return null;
            }
        }

        public List<OpenAttributeEntry> ReadOpenAttributeTable(NTFSRestartRecord restartRecord)
        {
            ulong openAttributeTableLsn = restartRecord.OpenAttributeTableLsn;
            if (openAttributeTableLsn != 0)
            {
                NTFSLogRecord record = ReadLogRecord(openAttributeTableLsn);
                if (record.RedoOperation != NTFSLogOperation.OpenAttributeTableDump)
                {
                    string message = String.Format("Restart record OpenAttributeTableLsn points to a record with RedoOperation {0}", record.RedoOperation);
                    throw new InvalidDataException(message);
                }

                if (restartRecord.OpenAttributeTableLength != record.RedoData.Length)
                {
                    throw new InvalidDataException("Open attribute table length does not match restart record");
                }

                return RestartTableHelper.ReadTable<OpenAttributeEntry>(record.RedoData, restartRecord.MajorVersion);
            }
            else
            {
                return null;
            }
        }

        public List<DirtyPageEntry> ReadDirtyPageTable(NTFSRestartRecord restartRecord)
        {
            ulong dirtyPageTableLsn = restartRecord.DirtyPageTableLsn;
            if (dirtyPageTableLsn != 0)
            {
                NTFSLogRecord record = ReadLogRecord(dirtyPageTableLsn);
                if (record.RedoOperation != NTFSLogOperation.DirtyPageTableDump)
                {
                    string message = String.Format("Restart record DirtyPageTableLsn points to a record with RedoOperation {0}", record.RedoOperation);
                    throw new InvalidDataException(message);
                }

                if (restartRecord.DirtyPageTableLength != record.RedoData.Length)
                {
                    throw new InvalidDataException("Dirty page table length does not match restart record");
                }

                return RestartTableHelper.ReadTable<DirtyPageEntry>(record.RedoData, restartRecord.MajorVersion);
            }
            else
            {
                return null;
            }
        }

        public List<TransactionEntry> ReadTransactionTable(NTFSRestartRecord restartRecord)
        {
            ulong transactionTableLsn = restartRecord.TransactionTableLsn;
            if (transactionTableLsn != 0)
            {
                NTFSLogRecord record = ReadLogRecord(transactionTableLsn);
                if (record.RedoOperation != NTFSLogOperation.TransactionTableDump)
                {
                    string message = String.Format("Restart record TransactionTableLsn points to a record with RedoOperation {0}", record.RedoOperation);
                    throw new InvalidDataException(message);
                }

                if (restartRecord.TransactionTableLength != record.RedoData.Length)
                {
                    throw new InvalidDataException("Transcation table length does not match restart record");
                }

                return RestartTableHelper.ReadTable<TransactionEntry>(record.RedoData, restartRecord.MajorVersion);
            }
            else
            {
                return null;
            }
        }

        public NTFSLogRecord ReadLogRecord(ulong lsn)
        {
            LogRecord record = m_logFile.ReadRecord(lsn);
            if (record.RecordType == LogRecordType.ClientRecord)
            {
                return new NTFSLogRecord(record.Data);
            }
            else
            {
                return null;
            }
        }

        public void WriteRestartRecord(ushort majorNTFSVersion, bool isClean)
        {
            NTFSRestartRecord previousRestartRecord = ReadCurrentRestartRecord();
            MftSegmentReference usnJournal = previousRestartRecord.UsnJournal;
            ulong previousRestartRecordLsn = previousRestartRecord.PreviousRestartRecordLsn;
            LogRecord restartRecord = WriteRestartRecord(m_lastClientLsn, previousRestartRecordLsn, usnJournal, majorNTFSVersion, isClean);
        }

        private LogRecord WriteRestartRecord(ulong startOfCheckpointLsn, ulong previousRestartRecordLsn, MftSegmentReference usnJournal, ushort majorNTFSVersion, bool isClean)
        {
            NTFSRestartRecord restartRecord = new NTFSRestartRecord(m_majorVersion, m_minorVersion);
            restartRecord.StartOfCheckpointLsn = startOfCheckpointLsn;
            restartRecord.PreviousRestartRecordLsn = previousRestartRecordLsn;
            if (isClean)
            {
                m_openAttributes.Clear(); // FIXME: we should find a more appropriate way to clear the open attribute table
            }
            else if (m_openAttributes.Count > 0)
            {
                byte[] openAttributeTableBytes = GetOpenAttributeTableBytes();
                m_lastClientLsn = 0;
                m_lastLsnToUndo = 0;
                LogRecord openAttributeTableRecord = WriteLogRecord(null, null, NTFSLogOperation.OpenAttributeTableDump, openAttributeTableBytes, NTFSLogOperation.Noop, new byte[0], 0, RestartTableHeader.Length);
                restartRecord.OpenAttributeTableLsn = openAttributeTableRecord.ThisLsn;
                restartRecord.OpenAttributeTableLength = (uint)openAttributeTableBytes.Length;
            }
            restartRecord.BytesPerCluster = (uint)Volume.BytesPerCluster;
            restartRecord.UsnJournal = usnJournal;
            byte[] clientData = restartRecord.GetBytes(majorNTFSVersion);
            LogRecord result = m_logFile.WriteRecord(m_clientIndex, LogRecordType.ClientRestart, 0, 0, 0, clientData);
            m_lastClientLsn = result.ThisLsn;
            m_lastLsnToUndo = 0;
            LogClientRecord clientRecord = m_logFile.GetClientRecord(m_clientIndex);
            clientRecord.OldestLsn = startOfCheckpointLsn;
            clientRecord.ClientRestartLsn = result.ThisLsn;
            m_logFile.WriteRestartPage(isClean);
            return result;
        }

        public LogRecord WriteForgetTransactionRecord(uint transactionID)
        {
            NTFSLogRecord ntfsLogRecord = new NTFSLogRecord();
            ntfsLogRecord.RedoOperation = NTFSLogOperation.ForgetTransaction;
            ntfsLogRecord.UndoOperation = NTFSLogOperation.CompensationLogRecord;
            return WriteLogRecord(ntfsLogRecord, transactionID);
        }

        public LogRecord WriteLogRecord(MftSegmentReference fileReference, AttributeRecord attributeRecord, NTFSLogOperation redoOperation, byte[] redoData, NTFSLogOperation undoOperation, byte[] undoData, ulong streamOffset, uint transactionID)
        {
            int openAttributeOffset = 0;
            if (fileReference != null)
            {
                int openAttributeIndex = IndexOfOpenAttribute(fileReference, attributeRecord.AttributeType);
                if (openAttributeIndex == -1)
                {
                    openAttributeIndex = AddToOpenAttributeTable(fileReference, attributeRecord);
                    openAttributeOffset = OpenAttributeIndexToOffset(openAttributeIndex);
                    OpenAttributeEntry entry = new OpenAttributeEntry(m_majorVersion);
                    entry.AllocatedOrNextFree = RestartTableEntry.RestartEntryAllocated;
                    entry.AttributeOffset = (uint)(RestartTableHeader.Length + openAttributeIndex * 0x28); //(uint)OpenAttributeIndexToOffset(index);
                    entry.FileReference = fileReference;
                    entry.LsnOfOpenRecord = m_lastClientLsn;
                    entry.AttributeTypeCode = attributeRecord.AttributeType;
                    byte[] openData = entry.GetBytes();
                    if (attributeRecord.Name != String.Empty)
                    {
                        throw new NotImplementedException();
                    }
                    LogRecord openAttributeRecord = WriteLogRecord(openAttributeOffset, NTFSLogOperation.OpenNonresidentAttribute, openData, NTFSLogOperation.Noop, new byte[0], 0, 0, new List<long>(), transactionID);
                }
                else
                {
                    openAttributeOffset = OpenAttributeIndexToOffset(openAttributeIndex);
                }
            }

            List<long> lcnList = new List<long>();
            if (attributeRecord is NonResidentAttributeRecord)
            {
                long targetVCN = (long)(streamOffset / (uint)Volume.BytesPerCluster);
                long lcn = ((NonResidentAttributeRecord)attributeRecord).DataRunSequence.GetDataClusterLCN(targetVCN);
                lcnList.Add(lcn);
            }

            return WriteLogRecord(openAttributeOffset, redoOperation, redoData, undoOperation, undoData, 0, streamOffset, lcnList, transactionID);
        }

        private LogRecord WriteLogRecord(int openAttributeOffset, NTFSLogOperation redoOperation, byte[] redoData, NTFSLogOperation undoOperation, byte[] undoData, int attributeOffset, ulong streamOffset, List<long> lcnList, uint transactionID)
        {
            NTFSLogRecord ntfsLogRecord = new NTFSLogRecord();
            ntfsLogRecord.TargetAttributeOffset = (ushort)openAttributeOffset;
            ntfsLogRecord.RedoOperation = redoOperation;
            ntfsLogRecord.RedoData = redoData;
            ntfsLogRecord.UndoOperation = undoOperation;
            ntfsLogRecord.UndoData = undoData;
            ntfsLogRecord.TargetVCN = (long)(streamOffset / (uint)Volume.BytesPerCluster);
            ntfsLogRecord.LCNsForPage.AddRange(lcnList);
            int offsetInCluster = (int)(streamOffset % (uint)Volume.BytesPerCluster);
            ntfsLogRecord.AttributeOffset = (ushort)attributeOffset;
            ntfsLogRecord.ClusterBlockOffset = (ushort)(offsetInCluster / NTFSLogRecord.BytesPerLogBlock);
            return WriteLogRecord(ntfsLogRecord, transactionID);
        }

        private LogRecord WriteLogRecord(NTFSLogRecord ntfsLogRecord, uint transactionID)
        {
            LogClientRecord clientRecord = m_logFile.GetClientRecord(m_clientIndex);
            byte[] clientData = ntfsLogRecord.GetBytes();
            LogRecord result = m_logFile.WriteRecord(m_clientIndex, LogRecordType.ClientRecord, m_lastClientLsn, m_lastLsnToUndo, transactionID, clientData);
            m_lastClientLsn = result.ThisLsn;
            m_lastLsnToUndo = result.ThisLsn;
            return result;
        }

        /// <returns>Index in open attribute table</returns>
        private int AddToOpenAttributeTable(MftSegmentReference fileReference, AttributeRecord attributeRecord)
        {
            int openAttributeIndex = m_openAttributes.Count;
            m_openAttributes.Add(fileReference, attributeRecord);
            return openAttributeIndex;
        }

        private byte[] GetOpenAttributeTableBytes()
        {
            List<OpenAttributeEntry> openAttributeTable = new List<OpenAttributeEntry>();
            for (int index = 0; index < m_openAttributes.Count; index++)
            {
                KeyValuePair<MftSegmentReference, AttributeRecord> openAttribute = m_openAttributes[index];
                OpenAttributeEntry entry = new OpenAttributeEntry(m_majorVersion);
                entry.AllocatedOrNextFree = RestartTableEntry.RestartEntryAllocated;
                entry.AttributeOffset = (uint)(RestartTableHeader.Length + index * 0x28); //(uint)OpenAttributeIndexToOffset(index);
                entry.FileReference = openAttribute.Key;
                entry.LsnOfOpenRecord = 0; // FIXME
                entry.AttributeTypeCode = openAttribute.Value.AttributeType;
                openAttributeTable.Add(entry);
            }
            return RestartTableHelper.GetTableBytes<OpenAttributeEntry>(openAttributeTable);
        }

        private int OpenAttributeIndexToOffset(int openAttributeIndex)
        {
            int entryLength = (m_majorVersion == 0) ? OpenAttributeEntry.LengthV0 : OpenAttributeEntry.LengthV1;
            return RestartTableHeader.Length + openAttributeIndex * entryLength;
        }

        private int IndexOfOpenAttribute(MftSegmentReference fileReference, AttributeType attributeType)
        {
            for (int index = 0; index < m_openAttributes.Count; index++)
            {
                // FIXME: Take attribute name into account as well
                if (m_openAttributes[index].Key == fileReference && m_openAttributes[index].Value.AttributeType == attributeType)
                {
                    return index;
                }
            }
            return -1;
        }

        public NTFSVolume Volume
        {
            get
            {
                return m_logFile.Volume;
            }
        }
    }
}
