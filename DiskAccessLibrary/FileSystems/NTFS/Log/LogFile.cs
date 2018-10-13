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

        public LogFile(NTFSVolume volume) : base(volume, MasterFileTable.LogSegmentReference)
        {
            if (!IsLogClean())
            {
                RepairLogFile();
            }
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

        private void WriteRestartPage(LogRestartPage restartPage)
        {
            byte[] pageBytes = restartPage.GetBytes((int)restartPage.SystemPageSize, true);
            WriteData(0, pageBytes);
            WriteData(restartPage.SystemPageSize, pageBytes);
        }

        public bool IsLogClean()
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            if (!m_restartPage.LogRestartArea.IsInUse)
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
            if (record.Length < LogRecord.HeaderLength)
            {
                throw new InvalidDataException("LogRecord length is invalid");
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

        /// <summary>
        /// This method will repair the log file by copying the tail copies back to their correct location.
        /// If necessary, the restart area will be updated to reflect CurrentLsn and LastLsnDataLength.
        /// </summary>
        private void RepairLogFile()
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            LogRecordPage firstTailPage = null;
            LogRecordPage secondTailPage = null;
            try
            {
                firstTailPage = ReadPageFromFile(m_restartPage.SystemPageSize * 2);
            }
            catch (InvalidDataException)
            {
            }

            try
            {
                secondTailPage = ReadPageFromFile(m_restartPage.SystemPageSize * 2 + m_restartPage.LogPageSize);
            }
            catch (InvalidDataException)
            {
            }

            // Find the most recent tail copy
            LogRecordPage tailPage = null;
            if (firstTailPage != null)
            {
                tailPage = firstTailPage;
            }

            if (tailPage == null || (secondTailPage != null && secondTailPage.LastEndLsn > firstTailPage.LastEndLsn))
            {
                tailPage = secondTailPage;
            }

            if (tailPage != null)
            {
                LogRecordPage page = null;
                try
                {
                    page = ReadPageFromFile(tailPage.LastLsnOrFileOffset);
                }
                catch (InvalidDataException)
                {
                }

                if (page == null || tailPage.LastEndLsn > page.LastLsnOrFileOffset)
                {
                    ulong pageOffsetInFile = tailPage.LastLsnOrFileOffset;
                    tailPage.LastLsnOrFileOffset = tailPage.LastEndLsn;
                    WritePage(pageOffsetInFile, tailPage);

                    if (tailPage.LastEndLsn > m_restartPage.LogRestartArea.CurrentLsn)
                    {
                        m_restartPage.LogRestartArea.CurrentLsn = tailPage.LastEndLsn;
                        int recordOffsetInPage = LsnToRecordOffsetInPage(tailPage.LastEndLsn);
                        LogRecord record = tailPage.ReadRecord(recordOffsetInPage, m_restartPage.LogRestartArea.LogPageDataOffset);
                        m_restartPage.LogRestartArea.LastLsnDataLength = (uint)record.Length;
                        WriteRestartPage(m_restartPage);
                    }
                }
            }
        }

        // Placeholder method that will implement caching in the future
        private LogRecordPage ReadPage(ulong pageOffset)
        {
            return ReadPageFromFile(pageOffset);
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
