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
    public partial class LogFile : NTFSFile
    {
        private LogRestartPage m_restartPage;
        private LogRecordPage m_firstTailPage;
        private LogRecordPage m_secondTailPage;

        public LogFile(NTFSVolume volume) : base(volume, MasterFileTable.LogSegmentReference)
        {
        }

        private LogRestartPage ReadRestartPage()
        {
            byte[] pageBytes = ReadData(0, Volume.BytesPerSector);
            uint systemPageSize = LogRestartPage.GetSystemPageSize(pageBytes, 0);
            int bytesToRead = (int)systemPageSize - pageBytes.Length;
            if (bytesToRead > 0)
            {
                byte[] temp = ReadData((ulong)pageBytes.Length, bytesToRead);
                pageBytes = ByteUtils.Concatenate(pageBytes, temp);
            }
            MultiSectorHelper.RevertUsaProtection(pageBytes, 0);
            m_restartPage = new LogRestartPage(pageBytes, 0);
            return m_restartPage;
        }

        public int FindClientIndex(string clientName)
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            for (int index = 0; index < m_restartPage.LogRestartArea.LogClientArray.Count; index++)
            {
                if (String.Equals(m_restartPage.LogRestartArea.LogClientArray[index].ClientName, clientName, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }
            return -1;
        }

        public LogClientRecord GetClientRecord(int clientIndex)
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            return m_restartPage.LogRestartArea.LogClientArray[clientIndex];
        }

        public bool IsLogClean()
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            if (m_restartPage.LogRestartArea.IsInUse)
            {
                // If the log file is not in use than it must be clean.
                return true;
            }
            else if (m_restartPage.LogRestartArea.IsClean)
            {
                // If the clean bit is set than the log file must be clean.
                return true;
            }
            else
            {
                // The volume has not been shutdown cleanly.
                // It's possible that the log is clean if the volume was completely idle for at least five seconds preceding the unclean shutdown.
                // Currently, we skip the analysis and assume that's not the case.
                return false;
            }
        }

        public LogRecord ReadRecord(ulong lsn)
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            ulong pageOffsetInFile = LsnToPageOffsetInFile(lsn);
            int recordOffsetInPage = LsnToRecordOffsetInPage(lsn);
            LogRecordPage page = ReadPage(pageOffsetInFile);
            int dataOffset = m_restartPage.LogRestartArea.LogPageDataOffset;
            LogRecord record = page.ReadRecord(recordOffsetInPage, dataOffset);
            if (record.ThisLsn != lsn)
            {
                throw new InvalidDataException("LogRecord Lsn does not match expected value");
            }
            if (record.IsMultiPageRecord)
            {
                int recordLength = (int)(LogRecord.HeaderLength + record.ClientDataLength);
                int bytesRemaining = recordLength - (LogRecord.HeaderLength + record.Data.Length);
                while (bytesRemaining > 0)
                {
                    pageOffsetInFile += m_restartPage.LogPageSize;
                    if (pageOffsetInFile == m_restartPage.LogRestartArea.FileSize)
                    {
                        pageOffsetInFile = m_restartPage.SystemPageSize * 2 + m_restartPage.LogPageSize * 2;
                    }
                    page = ReadPage(pageOffsetInFile);
                    int bytesToRead = Math.Min((int)m_restartPage.LogPageSize - dataOffset, bytesRemaining);
                    record.Data = ByteUtils.Concatenate(record.Data, page.ReadBytes(dataOffset, bytesToRead, dataOffset));
                    bytesRemaining -= bytesToRead;
                }
            }
            return record;
        }

        private LogRecordPage ReadPage(ulong pageOffset)
        {
            if (m_firstTailPage == null || m_secondTailPage == null)
            {
                m_firstTailPage = ReadPageFromFile(m_restartPage.SystemPageSize * 2);
                m_secondTailPage = ReadPageFromFile(m_restartPage.SystemPageSize * 2 + m_restartPage.LogPageSize);
            }

            LogRecordPage tailPage = null;
            if (pageOffset == m_firstTailPage.LastLsnOrFileOffset)
            {
                tailPage = m_firstTailPage;
            }

            if (pageOffset == m_secondTailPage.LastLsnOrFileOffset)
            {
                if (tailPage == null || m_secondTailPage.LastEndLsn >= m_firstTailPage.LastEndLsn)
                {
                    tailPage = m_secondTailPage;
                }
            }

            LogRecordPage page = ReadPageFromFile(pageOffset);
            if (tailPage != null && tailPage.LastEndLsn >= page.LastLsnOrFileOffset)
            {
                return tailPage;
            }
            else
            {
                return page;
            }
        }

        private LogRecordPage ReadPageFromFile(ulong pageOffset)
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            byte[] pageBytes = ReadData(pageOffset, (int)m_restartPage.LogPageSize);
            MultiSectorHelper.RevertUsaProtection(pageBytes, 0);
            return new LogRecordPage(pageBytes, m_restartPage.LogRestartArea.LogPageDataOffset);
        }

        private void WritePage(ulong pageOffset, LogRecordPage page)
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            byte[] pageBytes = page.GetBytes((int)m_restartPage.LogPageSize, m_restartPage.LogRestartArea.LogPageDataOffset, true);
            WriteData(pageOffset, pageBytes);
        }

        private ulong LsnToPageOffsetInFile(ulong lsn)
        {
            int seqNumberBits = (int)m_restartPage.LogRestartArea.SeqNumberBits;
            ulong fileOffset = (lsn << seqNumberBits) >> (seqNumberBits - 3);
            return fileOffset & ~(m_restartPage.LogPageSize - 1);
        }

        private int LsnToRecordOffsetInPage(ulong lsn)
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            return (int)((lsn << 3) & (m_restartPage.LogPageSize - 1));
        }

        private ulong CalculateNextLsn(ulong lsn, int recordLength)
        {
            int recordOffsetInPage = LsnToRecordOffsetInPage(lsn);
            int bytesToSkip = recordLength;
            int nextRecordOffsetInPage = recordOffsetInPage + recordLength;
            if (nextRecordOffsetInPage >= m_restartPage.LogPageSize)
            {
                int recordBytesInFirstPage = (int)m_restartPage.LogPageSize - recordOffsetInPage;
                int bytesRemaining = recordLength - recordBytesInFirstPage;
                int bytesAvailableInPage = (int)m_restartPage.LogPageSize - (int)m_restartPage.LogRestartArea.LogPageDataOffset;
                int middlePageCount = bytesRemaining / bytesAvailableInPage;
                int recordBytesInLastPage = bytesRemaining % bytesAvailableInPage;
                bytesToSkip = recordBytesInFirstPage + middlePageCount * (int)m_restartPage.LogPageSize + m_restartPage.LogRestartArea.LogPageDataOffset + recordBytesInLastPage;
                nextRecordOffsetInPage = (recordOffsetInPage + bytesToSkip) % (int)m_restartPage.LogPageSize;
            }

            int bytesRemainingInPage = (int)m_restartPage.LogPageSize - nextRecordOffsetInPage;
            if (bytesRemainingInPage < m_restartPage.LogRestartArea.RecordHeaderLength)
            {
                bytesToSkip += bytesRemainingInPage + m_restartPage.LogRestartArea.LogPageDataOffset;
            }

            return lsn + ((uint)bytesToSkip >> 3);
        }
    }
}
