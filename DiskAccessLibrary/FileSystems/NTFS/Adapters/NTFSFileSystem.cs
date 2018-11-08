/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
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
    /// <summary>
    /// Adapter providing FileSystem implementation for NTFS (using NTFSVolume).
    /// </summary>
    public class NTFSFileSystem : FileSystem, IExtendableFileSystem
    {
        private NTFSVolume m_volume;
        private Dictionary<long, List<NTFSFileStream>> m_openStreams = new Dictionary<long, List<NTFSFileStream>>();

        public NTFSFileSystem(Volume volume)
        {
            m_volume = new NTFSVolume(volume);
        }

        public NTFSFileSystem(NTFSVolume volume)
        {
            m_volume = volume;
        }

        public override FileSystemEntry GetEntry(string path)
        {
            FileRecord record = m_volume.GetFileRecord(path);
            return ToFileSystemEntry(path, record);
        }

        public override FileSystemEntry CreateFile(string path)
        {
            string parentDirectoryName = Path.GetDirectoryName(path);
            string fileName = Path.GetFileName(path);
            FileRecord parentDirectoryRecord = m_volume.GetFileRecord(parentDirectoryName);
            FileRecord fileRecord = m_volume.CreateFile(parentDirectoryRecord.BaseSegmentReference, fileName, false);
            return ToFileSystemEntry(path, fileRecord);
        }

        public override FileSystemEntry CreateDirectory(string path)
        {
            string parentDirectoryName = Path.GetDirectoryName(path);
            string directoryName = Path.GetFileName(path);
            FileRecord parentDirectoryRecord = m_volume.GetFileRecord(parentDirectoryName);
            FileRecord directoryRecord = m_volume.CreateFile(parentDirectoryRecord.BaseSegmentReference, directoryName, true);
            return ToFileSystemEntry(path, directoryRecord);
        }

        public override void Move(string source, string destination)
        {
            FileRecord sourceFileRecord = m_volume.GetFileRecord(source);
            string destinationDirectory = Path.GetDirectoryName(destination);
            string destinationFileName = Path.GetFileName(destination);
            FileRecord destinationDirectoryFileRecord = m_volume.GetFileRecord(destinationDirectory);
            m_volume.MoveFile(sourceFileRecord, destinationDirectoryFileRecord.BaseSegmentReference, destinationFileName);
        }

        public override void Delete(string path)
        {
            FileRecord fileRecord = m_volume.GetFileRecord(path);
            m_volume.DeleteFile(fileRecord);
        }

        public override List<FileSystemEntry> ListEntriesInDirectory(string path)
        {
            FileRecord directoryRecord = m_volume.GetFileRecord(path);
            if (!directoryRecord.IsDirectory)
            {
                throw new InvalidPathException(String.Format("'{0}' is not a directory", path));
            }

            KeyValuePairList<MftSegmentReference, FileNameRecord> records = m_volume.GetFileNameRecordsInDirectory(directoryRecord.BaseSegmentReference);
            List<FileSystemEntry> result = new List<FileSystemEntry>();

            path = FileSystem.GetDirectoryPath(path);

            foreach (FileNameRecord record in records.Values)
            {
                string fullPath = path + record.FileName;
                FileSystemEntry entry = ToFileSystemEntry(fullPath, record);
                result.Add(entry);
            }
            return result;
        }

        public override Stream OpenFile(string path, FileMode mode, FileAccess access, FileShare share, FileOptions options)
        {
            FileRecord fileRecord = null;
            if (mode == FileMode.CreateNew || mode == FileMode.Create || mode == FileMode.OpenOrCreate)
            {
                bool fileExists = false;
                try
                {
                    fileRecord = m_volume.GetFileRecord(path);
                    fileExists = true;
                }
                catch (FileNotFoundException)
                {
                }
                catch (DirectoryNotFoundException)
                {
                }

                if (fileExists)
                {
                    if (mode == FileMode.CreateNew)
                    {
                        throw new AlreadyExistsException();
                    }
                    else if (mode == FileMode.Create)
                    {
                        mode = FileMode.Truncate;
                    }
                }
                else
                {
                    string directoryPath = Path.GetDirectoryName(path);
                    string fileName = Path.GetFileName(path);
                    FileRecord directoryRecord = m_volume.GetFileRecord(directoryPath);
                    fileRecord = m_volume.CreateFile(directoryRecord.BaseSegmentReference, fileName, false);
                }
            }
            else // Open, Truncate or Append
            {
                fileRecord = m_volume.GetFileRecord(path);
            }

            if (fileRecord.IsDirectory)
            {
                throw new UnauthorizedAccessException();
            }

            List<NTFSFileStream> openStreams;
            lock (m_openStreams)
            {
                if (m_openStreams.TryGetValue(fileRecord.BaseSegmentNumber, out openStreams))
                {
                    if ((access & FileAccess.Write) != 0)
                    {
                        // Currently we only support opening a file stream for write access if no other stream is opened for that file
                        throw new SharingViolationException();
                    }
                    else if ((access & FileAccess.Read) != 0)
                    {
                        foreach (NTFSFileStream openStream in openStreams)
                        {
                            if (openStream.CanWrite && ((share & FileShare.Write) == 0))
                            {
                                throw new SharingViolationException();
                            }
                        }
                    }
                }
                else
                {
                    openStreams = new List<NTFSFileStream>();
                    m_openStreams.Add(fileRecord.BaseSegmentNumber, openStreams);
                }
            }

            NTFSFile file = new NTFSFile(m_volume, fileRecord);
            NTFSFileStream stream = new NTFSFileStream(file, access);
            openStreams.Add(stream);
            stream.Closed += delegate(object sender, EventArgs e)
            {
                openStreams.Remove(stream);
                if (openStreams.Count == 0)
                {
                    lock (m_openStreams)
                    {
                        m_openStreams.Remove(fileRecord.BaseSegmentNumber);
                    }
                }
            };

            if (mode == FileMode.Truncate)
            {
                stream.SetLength(0);
            }
            else if (mode == FileMode.Append)
            {
                stream.Seek((long)file.Length, SeekOrigin.Begin);
            }
            return stream;
        }

        public override void SetAttributes(string path, bool? isHidden, bool? isReadonly, bool? isArchived)
        {
            FileRecord record = m_volume.GetFileRecord(path);
            if (isHidden.HasValue)
            {
                if (isHidden.Value)
                {
                    record.StandardInformation.FileAttributes |= FileAttributes.Hidden;
                }
                else
                {
                    record.StandardInformation.FileAttributes &= ~FileAttributes.Hidden;
                }
            }

            if (isReadonly.HasValue)
            {
                if (isReadonly.Value)
                {
                    record.StandardInformation.FileAttributes |= FileAttributes.Readonly;
                }
                else
                {
                    record.StandardInformation.FileAttributes &= ~FileAttributes.Readonly;
                }
            }

            if (isArchived.HasValue)
            {
                if (isArchived.Value)
                {
                    record.StandardInformation.FileAttributes |= FileAttributes.Archive;
                }
                else
                {
                    record.StandardInformation.FileAttributes &= ~FileAttributes.Archive;
                }
            }

            record.StandardInformation.MftModificationTime = DateTime.Now;
            m_volume.UpdateFileRecord(record);
        }

        public override void SetDates(string path, DateTime? creationDT, DateTime? lastWriteDT, DateTime? lastAccessDT)
        {
            FileRecord record = m_volume.GetFileRecord(path);
            if (creationDT.HasValue)
            {
                record.StandardInformation.CreationTime = creationDT.Value;
                record.FileNameRecord.CreationTime = creationDT.Value;
            }

            if (lastWriteDT.HasValue)
            {
                record.StandardInformation.ModificationTime = lastWriteDT.Value;
                record.FileNameRecord.ModificationTime = lastWriteDT.Value;
            }

            if (lastAccessDT.HasValue)
            {
                record.StandardInformation.LastAccessTime = lastAccessDT.Value;
                record.FileNameRecord.LastAccessTime = lastAccessDT.Value;
            }

            record.StandardInformation.MftModificationTime = DateTime.Now;
            record.FileNameRecord.MftModificationTime = DateTime.Now;
            m_volume.UpdateFileRecord(record);
        }

        public long GetMaximumSizeToExtend()
        {
            return m_volume.GetMaximumSizeToExtend();
        }

        public void Extend(long numberOfAdditionalSectors)
        {
            m_volume.Extend(numberOfAdditionalSectors);
        }

        public override string ToString()
        {
            return m_volume.ToString();
        }

        public override string Name
        {
            get
            {
                return "NTFS";
            }
        }

        public override long Size
        {
            get
            {
                return m_volume.Size;
            }
        }

        public override long FreeSpace
        {
            get
            {
                return m_volume.FreeSpace;
            }
        }

        public override bool SupportsNamedStreams
        {
            get
            {
                return false;
            }
        }

        public static FileSystemEntry ToFileSystemEntry(string path, FileRecord record)
        {
            ulong size = record.IsDirectory ? 0 : record.DataRecord.DataLength;
            FileAttributes attributes = record.StandardInformation.FileAttributes;
            bool isHidden = (attributes & FileAttributes.Hidden) > 0;
            bool isReadonly = (attributes & FileAttributes.Readonly) > 0;
            bool isArchived = (attributes & FileAttributes.Archive) > 0;
            return new FileSystemEntry(path, record.FileName, record.IsDirectory, size, record.FileNameRecord.CreationTime, record.FileNameRecord.ModificationTime, record.FileNameRecord.LastAccessTime, isHidden, isReadonly, isArchived);
        }

        public static FileSystemEntry ToFileSystemEntry(string path, FileNameRecord record)
        {
            ulong size = record.FileSize;
            bool isDirectory = record.IsDirectory;
            FileAttributes attributes = record.FileAttributes;
            bool isHidden = (attributes & FileAttributes.Hidden) > 0;
            bool isReadonly = (attributes & FileAttributes.Readonly) > 0;
            bool isArchived = (attributes & FileAttributes.Archive) > 0;
            return new FileSystemEntry(path, record.FileName, isDirectory, size, record.CreationTime, record.ModificationTime, record.LastAccessTime, isHidden, isReadonly, isArchived);
        }
    }
}
