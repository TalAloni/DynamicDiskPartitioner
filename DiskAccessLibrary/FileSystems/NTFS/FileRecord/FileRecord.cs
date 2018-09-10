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
    /// A collection of base record segment and zero or more file record segments making up this file record.
    /// </summary>
    public class FileRecord
    {
        private List<FileRecordSegment> m_segments;
        private List<AttributeRecord> m_attributes;

        public FileRecord(FileRecordSegment segment)
        {
            m_segments = new List<FileRecordSegment>();
            m_segments.Add(segment);
        }

        public FileRecord(List<FileRecordSegment> segments)
        {
            m_segments = segments;
        }

        /// <remarks>
        /// https://blogs.technet.microsoft.com/askcore/2009/10/16/the-four-stages-of-ntfs-file-growth/
        /// </remarks>
        public void UpdateSegments(int maximumSegmentLength, int bytesPerSector, ushort minorNTFSVersion)
        {
            foreach (FileRecordSegment segment in m_segments)
            {
                segment.ImmediateAttributes.Clear();
            }

            int segmentLength = FileRecordSegment.GetFirstAttributeOffset(maximumSegmentLength, minorNTFSVersion);
            segmentLength += FileRecordSegment.EndMarkerLength;

            foreach (AttributeRecord attribute in this.Attributes)
            {
                segmentLength += (int)attribute.RecordLength;
            }

            if (segmentLength <= maximumSegmentLength)
            {
                // a single record segment is needed
                FileRecordSegment baseRecordSegment = m_segments[0];
                foreach (AttributeRecord attribute in this.Attributes)
                {
                    baseRecordSegment.ImmediateAttributes.Add(attribute);
                }

                // free the rest of the segments, if there are any
                for (int index = 1; index < m_segments.Count; index++)
                {
                    m_segments[index].IsInUse = false;
                }
            }
            else
            {
                // we have to check if we can make some data streams non-resident,
                // otherwise we have to use child segments and create an attribute list
                throw new NotImplementedException();
            }
        }

        public List<AttributeRecord> GetAssembledAttributes()
        {
            return FileRecordHelper.GetAssembledAttributes(m_segments);
        }

        public AttributeRecord CreateAttributeRecord(AttributeType type, string name)
        {
            AttributeRecord attribute = AttributeRecord.Create(type, name, m_segments[0].NextAttributeInstance);
            m_segments[0].NextAttributeInstance++;
            FileRecordHelper.InsertSorted(this.Attributes, attribute);
            return attribute;
        }

        public AttributeRecord GetAttributeRecord(AttributeType type, string name)
        {
            foreach (AttributeRecord attribute in this.Attributes)
            {
                if (attribute.AttributeType == type && attribute.Name == name)
                {
                    return attribute;
                }
            }

            return null;
        }

        public void RemoveAttributeRecord(AttributeType attributeType, string name)
        {
            for (int index = 0; index < Attributes.Count; index++)
            {
                if (Attributes[index].AttributeType == attributeType && Attributes[index].Name == name)
                {
                    Attributes.RemoveAt(index);
                    break;
                }
            }
        }

        public FileNameRecord GetFileNameRecord(FilenameNamespace filenameNamespace)
        {
            foreach (AttributeRecord attribute in this.Attributes)
            {
                if (attribute is FileNameAttributeRecord)
                {
                    FileNameRecord fileNameAttribute = ((FileNameAttributeRecord)attribute).Record;
                    if (fileNameAttribute.Namespace == filenameNamespace)
                    {
                        return fileNameAttribute;
                    }
                }
            }
            return null;
        }

        public List<FileRecordSegment> Segments
        {
            get
            {
                return m_segments;
            }
        }

        public List<AttributeRecord> Attributes
        {
            get
            {
                if (m_attributes == null)
                {
                    m_attributes = GetAssembledAttributes();
                }
                return m_attributes;
            }
        }

        public StandardInformationRecord StandardInformation
        {
            get
            {
                foreach (AttributeRecord attribute in this.Attributes)
                {
                    if (attribute is StandardInformationRecord)
                    {
                        return (StandardInformationRecord)attribute;
                    }
                }
                return null;
            }
        }

        public FileNameRecord LongFileNameRecord
        {
            get
            {
                FileNameRecord record = GetFileNameRecord(FilenameNamespace.Win32);
                if (record == null)
                {
                    record = GetFileNameRecord(FilenameNamespace.POSIX);
                }
                return record;
            }
        }

        // 8.3 filename
        public FileNameRecord ShortFileNameRecord
        {
            get
            {
                FileNameRecord record = GetFileNameRecord(FilenameNamespace.DOS);
                if (record == null)
                {
                    // Win32AndDOS means that both the Win32 and the DOS filenames are identical and hence have been saved in this single filename record.
                    record = GetFileNameRecord(FilenameNamespace.Win32AndDOS);
                }
                return record;
            }
        }

        public FileNameRecord FileNameRecord
        {
            get
            {
                FileNameRecord fileNameRecord = this.LongFileNameRecord;
                if (fileNameRecord == null)
                {
                    fileNameRecord = this.ShortFileNameRecord;
                }

                return fileNameRecord;
            }
        }

        /// <summary>
        /// Will return the long filename of the file
        /// </summary>
        public string FileName
        {
            get
            {
                FileNameRecord fileNameRecord = this.FileNameRecord;
                if (fileNameRecord != null)
                {
                    return fileNameRecord.FileName;
                }
                else
                {
                    return String.Empty;
                }
            }
        }

        public MftSegmentReference ParentDirectoryReference
        {
            get
            {
                FileNameRecord fileNameRecord = this.LongFileNameRecord;
                if (fileNameRecord == null)
                {
                    fileNameRecord = this.ShortFileNameRecord;
                }

                if (fileNameRecord != null)
                {
                    return fileNameRecord.ParentDirectory;
                }
                else
                {
                    return null;
                }
            }
        }

        public AttributeRecord DataRecord
        {
            get
            {
                return GetAttributeRecord(AttributeType.Data, String.Empty);
            }
        }

        public NonResidentAttributeRecord NonResidentDataRecord
        {
            get
            {
                AttributeRecord dataRecord = this.DataRecord;
                if (dataRecord is NonResidentAttributeRecord)
                {
                    return (NonResidentAttributeRecord)dataRecord;
                }
                else
                {
                    return null;
                }
            }
        }

        public AttributeRecord BitmapRecord
        {
            get
            {
                return GetAttributeRecord(AttributeType.Bitmap, String.Empty);
            }
        }

        public long BaseRecordSegmentNumber
        {
            get
            {
                return m_segments[0].MftSegmentNumber;
            }
        }

        public ushort BaseRecordSequenceNumber
        {
            get
            {
                return m_segments[0].SequenceNumber;
            }
        }

        public MftSegmentReference BaseRecordSegmentReference
        {
            get
            {
                return new MftSegmentReference(BaseRecordSegmentNumber, BaseRecordSequenceNumber);
            }
        }

        public bool IsInUse
        {
            get
            {
                return m_segments[0].IsInUse;
            }
        }

        public bool IsDirectory
        {
            get
            {
                return m_segments[0].IsDirectory;
            }
        }

        public int AttributesLengthOnDisk
        {
            get
            {
                int length = 0;
                foreach (AttributeRecord attribute in this.Attributes)
                {
                    length += (int)attribute.RecordLengthOnDisk;
                }
                return length;
            }
        }

        public bool IsMetaFile
        {
            get
            {
                return (this.BaseRecordSegmentNumber <= MasterFileTable.LastReservedMftSegmentNumber);
            }
        }
    }
}
