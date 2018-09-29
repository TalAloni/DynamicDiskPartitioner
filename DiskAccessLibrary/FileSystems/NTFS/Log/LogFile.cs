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
    public class LogFile : NTFSFile
    {
        private LogRestartPage m_restartPage;

        public LogFile(NTFSVolume volume) : base(volume, MasterFileTable.LogSegmentReference)
        {
        }

        public LogRestartPage ReadRestartPage()
        {
            byte[] pageBytes = ReadData(0, Volume.BytesPerSector);
            uint logPageSize = LogRestartPage.GetLogPageSize(pageBytes, 0);
            int bytesToRead = (int)logPageSize - pageBytes.Length;
            if (bytesToRead > 0)
            {
                byte[] temp = ReadData((ulong)pageBytes.Length, bytesToRead);
                pageBytes = ByteUtils.Concatenate(pageBytes, temp);
            }
            m_restartPage = new LogRestartPage(pageBytes, 0);
            return m_restartPage;
        }

        public LogRecord ReadCurrentRecord()
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }
            return ReadRecord(m_restartPage.LogRestartArea.CurrentLsn);
        }

        public LogRecord ReadRecord(ulong lsn)
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            ulong pageOffsetInFile = LSNToPageOffsetInFile(lsn);
            int recordOffsetInPage = LSNToRecordOffsetInPage(lsn);
            LogRecordPage page = ReadPage(pageOffsetInFile);
            return page.ReadRecord(recordOffsetInPage, m_restartPage.LogRestartArea.LogPageDataOffset);
        }

        private LogRecordPage ReadPage(ulong pageOffset)
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            byte[] pageBytes = ReadData(pageOffset, (int)m_restartPage.LogPageSize);
            return new LogRecordPage(pageBytes, 0, m_restartPage.LogRestartArea.LogPageDataOffset);
        }

        private ulong LSNToPageOffsetInFile(ulong lsn)
        {
            int seqNumberBits = (int)m_restartPage.LogRestartArea.SeqNumberBits;
            ulong fileOffset = (lsn << seqNumberBits) >> (seqNumberBits - 3);
            return fileOffset & ~(m_restartPage.LogPageSize - 1);
        }

        private int LSNToRecordOffsetInPage(ulong lsn)
        {
            if (m_restartPage == null)
            {
                m_restartPage = ReadRestartPage();
            }

            return (int)((lsn << 3) & (m_restartPage.LogPageSize - 1));
        }
    }
}
