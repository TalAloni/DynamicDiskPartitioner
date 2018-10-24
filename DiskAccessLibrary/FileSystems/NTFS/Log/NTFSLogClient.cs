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
        private class OpenAttribute
        {
            public MftSegmentReference FileReference;
            public AttributeType AttributeType;
            public string AttributeName;
            public ulong LsnOfOpenRecord;

            public OpenAttribute(MftSegmentReference fileReference, AttributeType attributeType, string attributeName, ulong lsnOfOpenRecord)
            {
                FileReference = fileReference;
                AttributeType = attributeType;
                AttributeName = attributeName;
                LsnOfOpenRecord = lsnOfOpenRecord;
            }
        }

        private class Transaction
        {
            public ulong LastLsnToUndo;
            public ulong OldestLsn;

            public Transaction()
            {
            }
        }

        private const string ClientName = "NTFS";

        private LogFile m_logFile;
        private int m_clientIndex;
        private uint m_majorVersion; // For write purposes only
        private uint m_minorVersion; // For write purposes only
        private ulong m_lastClientLsn = 0;
        private List<OpenAttribute> m_openAttributes = new List<OpenAttribute>();
        private List<Transaction> m_transactions = new List<Transaction>();

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
            LfsRecord record = m_logFile.ReadRecord(clientRestartLsn);
            if (record.RecordType == LfsRecordType.ClientRestart)
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
            LfsRecord record = m_logFile.ReadRecord(lsn);
            if (record.RecordType == LfsRecordType.ClientRecord)
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
            LfsRecord restartRecord = WriteRestartRecord(previousRestartRecordLsn, usnJournal, majorNTFSVersion, isClean);
        }

        private LfsRecord WriteRestartRecord(ulong previousRestartRecordLsn, MftSegmentReference usnJournal, ushort majorNTFSVersion, bool isClean)
        {
            NTFSRestartRecord restartRecord = new NTFSRestartRecord(m_majorVersion, m_minorVersion);
            restartRecord.StartOfCheckpointLsn = m_lastClientLsn;
            restartRecord.PreviousRestartRecordLsn = previousRestartRecordLsn;
            if (isClean)
            {
                if (m_transactions.Count > 0)
                {
                    throw new InvalidOperationException("All TransactionIDs must be deallocated before writing a clean restart record");
                }
                m_openAttributes.Clear(); // FIXME: we should find a more appropriate way to clear the open attribute table
            }
            else if (m_openAttributes.Count > 0)
            {
                byte[] openAttributeTableBytes = GetOpenAttributeTableBytes();
                m_lastClientLsn = 0;
                uint transactionID = RestartTableHeader.Length; // This record must have a valid transactionID
                LfsRecord openAttributeTableRecord = WriteLogRecord(null, null, NTFSLogOperation.OpenAttributeTableDump, openAttributeTableBytes, NTFSLogOperation.Noop, new byte[0], 0, transactionID);
                restartRecord.OpenAttributeTableLsn = openAttributeTableRecord.ThisLsn;
                restartRecord.OpenAttributeTableLength = (uint)openAttributeTableBytes.Length;
            }
            restartRecord.BytesPerCluster = (uint)Volume.BytesPerCluster;
            restartRecord.UsnJournal = usnJournal;
            byte[] clientData = restartRecord.GetBytes(majorNTFSVersion);
            LfsRecord result = m_logFile.WriteRecord(m_clientIndex, LfsRecordType.ClientRestart, 0, 0, 0, clientData);
            m_lastClientLsn = result.ThisLsn;
            LfsClientRecord clientRecord = m_logFile.GetClientRecord(m_clientIndex);
            if (isClean)
            {
                clientRecord.OldestLsn = restartRecord.StartOfCheckpointLsn;
            }
            else
            {
                ulong oldestLsn = restartRecord.StartOfCheckpointLsn;
                foreach (Transaction transaction in m_transactions)
                {
                    if (transaction.OldestLsn != 0 && transaction.OldestLsn < oldestLsn)
                    {
                        oldestLsn = transaction.OldestLsn;
                    }
                }
                clientRecord.OldestLsn = oldestLsn;
            }
            clientRecord.ClientRestartLsn = result.ThisLsn;
            m_logFile.WriteRestartPage(isClean);
            return result;
        }

        /// <summary>
        /// Write ForgetTransaction record and deallocate the transactionID.
        /// </summary>
        public LfsRecord WriteForgetTransactionRecord(uint transactionID)
        {
            NTFSLogRecord ntfsLogRecord = new NTFSLogRecord();
            ntfsLogRecord.RedoOperation = NTFSLogOperation.ForgetTransaction;
            ntfsLogRecord.UndoOperation = NTFSLogOperation.CompensationLogRecord;
            LfsRecord result = WriteLogRecord(ntfsLogRecord, transactionID);
            DeallocateTransactionID(transactionID);
            return result;
        }

        public LfsRecord WriteLogRecord(MftSegmentReference fileReference, AttributeRecord attributeRecord, NTFSLogOperation redoOperation, byte[] redoData, NTFSLogOperation undoOperation, byte[] undoData, ulong streamOffset, uint transactionID)
        {
            int openAttributeOffset = 0;
            if (fileReference != null)
            {
                int openAttributeIndex = IndexOfOpenAttribute(fileReference, attributeRecord.AttributeType, attributeRecord.Name);
                if (openAttributeIndex == -1)
                {
                    openAttributeIndex = AddToOpenAttributeTable(fileReference, attributeRecord.AttributeType, attributeRecord.Name, m_lastClientLsn);
                    openAttributeOffset = OpenAttributeIndexToOffset(openAttributeIndex);
                    OpenAttributeEntry entry = new OpenAttributeEntry(m_majorVersion);
                    entry.AllocatedOrNextFree = RestartTableEntry.RestartEntryAllocated;
                    // Note: NTFS v5.1 driver calulates AttributeOffset using entry length of 0x28, the reason is unclear but we're immitating this.
                    entry.AttributeOffset = (uint)(RestartTableHeader.Length + openAttributeIndex * OpenAttributeEntry.LengthV1);
                    entry.FileReference = fileReference;
                    entry.LsnOfOpenRecord = m_lastClientLsn;
                    entry.AttributeTypeCode = attributeRecord.AttributeType;
                    byte[] openData = entry.GetBytes();
                    if (attributeRecord.Name != String.Empty)
                    {
                        throw new NotImplementedException();
                    }
                    LfsRecord openAttributeRecord = WriteLogRecord(openAttributeOffset, NTFSLogOperation.OpenNonResidentAttribute, openData, NTFSLogOperation.Noop, new byte[0], 0, 0, new List<long>(), transactionID);
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

        private LfsRecord WriteLogRecord(int openAttributeOffset, NTFSLogOperation redoOperation, byte[] redoData, NTFSLogOperation undoOperation, byte[] undoData, int attributeOffset, ulong streamOffset, List<long> lcnList, uint transactionID)
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

        private LfsRecord WriteLogRecord(NTFSLogRecord ntfsLogRecord, uint transactionID)
        {
            LfsClientRecord clientRecord = m_logFile.GetClientRecord(m_clientIndex);
            byte[] clientData = ntfsLogRecord.GetBytes();
            int transactionIndex = IndexOfTransaction(transactionID);
            ulong lastLsnToUndo = m_transactions[transactionIndex].LastLsnToUndo;
            LfsRecord result = m_logFile.WriteRecord(m_clientIndex, LfsRecordType.ClientRecord, m_lastClientLsn, lastLsnToUndo, transactionID, clientData);
            m_lastClientLsn = result.ThisLsn;
            m_transactions[transactionIndex].LastLsnToUndo = result.ThisLsn;
            if (m_transactions[transactionIndex].OldestLsn == 0)
            {
                m_transactions[transactionIndex].OldestLsn = result.ThisLsn;
            }
            return result;
        }

        /// <returns>Index in open attribute table</returns>
        private int AddToOpenAttributeTable(MftSegmentReference fileReference, AttributeType attributeType, string attributeName, ulong lsnOfOpenRecord)
        {
            int openAttributeIndex = m_openAttributes.Count;
            m_openAttributes.Add(new OpenAttribute(fileReference, attributeType, attributeName, lsnOfOpenRecord));
            return openAttributeIndex;
        }

        private byte[] GetOpenAttributeTableBytes()
        {
            List<OpenAttributeEntry> openAttributeTable = new List<OpenAttributeEntry>();
            for (int index = 0; index < m_openAttributes.Count; index++)
            {
                OpenAttribute openAttribute = m_openAttributes[index];
                OpenAttributeEntry entry = new OpenAttributeEntry(m_majorVersion);
                entry.AllocatedOrNextFree = RestartTableEntry.RestartEntryAllocated;
                // Note: NTFS v5.1 driver calulates AttributeOffset using entry length of 0x28, the reason is unclear but we're immitating this.
                entry.AttributeOffset = (uint)(RestartTableHeader.Length + index * OpenAttributeEntry.LengthV1);
                entry.FileReference = openAttribute.FileReference;
                entry.LsnOfOpenRecord = openAttribute.LsnOfOpenRecord;
                entry.AttributeTypeCode = openAttribute.AttributeType;
                if (openAttribute.AttributeName != String.Empty)
                {
                    throw new NotImplementedException();
                }
                openAttributeTable.Add(entry);
            }
            return RestartTableHelper.GetTableBytes<OpenAttributeEntry>(openAttributeTable);
        }

        private int OpenAttributeIndexToOffset(int openAttributeIndex)
        {
            int entryLength = (m_majorVersion == 0) ? OpenAttributeEntry.LengthV0 : OpenAttributeEntry.LengthV1;
            return RestartTableHeader.Length + openAttributeIndex * entryLength;
        }

        private int IndexOfOpenAttribute(MftSegmentReference fileReference, AttributeType attributeType, string attributeName)
        {
            for (int index = 0; index < m_openAttributes.Count; index++)
            {
                if (m_openAttributes[index].FileReference == fileReference &&
                    m_openAttributes[index].AttributeType == attributeType &&
                    String.Equals(m_openAttributes[index].AttributeName, attributeName, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }
            return -1;
        }

        public uint AllocateTransactionID()
        {
            int transactionIndex = m_transactions.Count;
            m_transactions.Add(new Transaction());
            return TransactionIndexToOffset(transactionIndex);
        }

        private void DeallocateTransactionID(uint transactionID)
        {
            int transactionIndex = IndexOfTransaction(transactionID);
            // A more recent transaction (with a bigger transaction index) might still be active,
            // so we set the transaction to null and trim the list when possible.
            m_transactions[transactionIndex] = null;
            for (int index = m_transactions.Count - 1; index >= 0; index--)
            {
                if (m_transactions[index] == null)
                {
                    m_transactions.RemoveAt(index);
                }
            }
        }

        private uint TransactionIndexToOffset(int transactionIndex)
        {
            return RestartTableHeader.Length + (uint)transactionIndex * TransactionEntry.EntryLength;
        }

        private int IndexOfTransaction(uint transactionID)
        {
            return (int)((transactionID - RestartTableHeader.Length) / TransactionEntry.EntryLength);
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
