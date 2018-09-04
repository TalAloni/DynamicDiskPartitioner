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
    public class FileRecord // A collection of base record segment and zero or more file record segments making up this file record
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
            return GetAssembledAttributes(m_segments);
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

        /// <summary>
        /// Segment number of base record
        /// </summary>
        public long MftSegmentNumber
        {
            get
            {
                return m_segments[0].MftSegmentNumber;
            }
        }

        /// <summary>
        /// Sequence number of base record
        /// </summary>
        public ushort SequenceNumber
        {
            get
            {
                return m_segments[0].SequenceNumber;
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
                return (this.MftSegmentNumber <= MasterFileTable.LastReservedMftSegmentNumber);
            }
        }

        /// <remarks>
        /// Only non-resident attributes can be fragmented.
        /// References:
        /// https://docs.microsoft.com/en-us/previous-versions/windows/it-pro/windows-2000-server/cc976808(v=technet.10)
        /// https://blogs.technet.microsoft.com/askcore/2009/10/16/the-four-stages-of-ntfs-file-growth/
        /// </remarks>
        public static List<AttributeRecord> GetAssembledAttributes(List<FileRecordSegment> segments)
        {
            List<AttributeRecord> result = new List<AttributeRecord>();
            // We need to assemble fragmented attributes (if there are any),
            // if two attributes have the same AttributeType and Name, then we need to assemble them back together.
            Dictionary<KeyValuePair<AttributeType, string>, List<NonResidentAttributeRecord>> fragments = new Dictionary<KeyValuePair<AttributeType, string>, List<NonResidentAttributeRecord>>();
            foreach (FileRecordSegment segment in segments)
            {
                foreach (AttributeRecord attribute in segment.ImmediateAttributes)
                {
                    if (attribute is ResidentAttributeRecord)
                    {
                        result.Add(attribute);
                    }
                    else
                    {
                        KeyValuePair<AttributeType, string> key = new KeyValuePair<AttributeType, string>(attribute.AttributeType, attribute.Name);
                        if (fragments.ContainsKey(key))
                        {
                            fragments[key].Add((NonResidentAttributeRecord)attribute);
                        }
                        else
                        {
                            List<NonResidentAttributeRecord> attributeFragments = new List<NonResidentAttributeRecord>();
                            attributeFragments.Add((NonResidentAttributeRecord)attribute);
                            fragments.Add(key, attributeFragments);
                        }
                    }
                }
            }

            // assemble all non-resident attributes
            foreach (List<NonResidentAttributeRecord> attributeFragments in fragments.Values)
            {
                // we assume attribute fragments are written to disk sorted by LowestVCN
                NonResidentAttributeRecord baseAttribute = attributeFragments[0];
                if (baseAttribute.LowestVCN != 0)
                {
                    string message = String.Format("Attribute fragments must be sorted, MftSegmentNumber: {0}, attribute type: {1}",
                                                   segments[0].MftSegmentNumber, baseAttribute.AttributeType);
                    throw new InvalidDataException(message);
                }

                if (baseAttribute.DataRunSequence.DataClusterCount != baseAttribute.HighestVCN + 1)
                {
                    string message = String.Format("Cannot properly assemble data run sequence 0, expected length: {0}, sequence length: {1}",
                                                   baseAttribute.HighestVCN + 1, baseAttribute.DataRunSequence.DataClusterCount);
                    throw new InvalidDataException(message);
                }

                for (int index = 1; index < attributeFragments.Count; index++)
                {
                    NonResidentAttributeRecord attributeFragment = attributeFragments[index];
                    if (attributeFragment.LowestVCN == baseAttribute.HighestVCN + 1)
                    {
                        // The DataRunSequence of each additional file record segment starts at absolute LCN,
                        // so we need to convert it to relative offset before adding it to the base DataRunSequence
                        long absoluteOffset = attributeFragment.DataRunSequence[0].RunOffset;
                        long previousLCN = baseAttribute.DataRunSequence.LastDataRunStartLCN;
                        long relativeOffset = absoluteOffset - previousLCN;
                        attributeFragment.DataRunSequence[0].RunOffset = relativeOffset;

                        baseAttribute.DataRunSequence.AddRange(attributeFragment.DataRunSequence);
                        baseAttribute.HighestVCN = attributeFragment.HighestVCN;

                        if (baseAttribute.DataRunSequence.DataClusterCount != baseAttribute.HighestVCN + 1)
                        {
                            string message = String.Format("Cannot properly assemble data run sequence, expected length: {0}, sequence length: {1}",
                                                           baseAttribute.HighestVCN + 1, baseAttribute.DataRunSequence.DataClusterCount);
                            throw new InvalidDataException(message);
                        }
                    }
                    else
                    {
                        throw new InvalidDataException("Invalid attribute fragments order");
                    }
                }

                result.Add(baseAttribute);
            }

            return result;
        }
    }
}
