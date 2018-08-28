/* Copyright (C) 2014-2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    public class MasterFileTable
    {
        public const int LastReservedMftSegmentNumber = 23; // 12-23 are reserved for additional metafiles
        
        public const long MasterFileTableSegmentNumber = 0;
        public const long MftMirrorSegmentNumber = 1;
        public const long LogFileSegmentNumber = 2;
        public const long VolumeSegmentNumber = 3;
        public const long AttrDefSegmentNumber = 4;
        public const long RootDirSegmentNumber = 5;
        public const long BitmapSegmentNumber = 6;
        public const long BootSegmentNumber = 7;
        public const long BadClusSegmentNumber = 8;
        public const long SecureSegmentNumber = 9;
        public const long UpCaseSegmentNumber = 10;
        public const long ExtendSegmentNumber = 11;
        // The $Extend Metafile is simply a directory index that contains information on where to locate the last four metafiles ($ObjId, $Quota, $Reparse and $UsnJrnl)
        public readonly int AttributeDataLengthToMakeNonResident;

        public NTFSVolume m_volume;
        private bool m_useMftMirror;
        
        private FileRecord m_mftRecord;

        public MasterFileTable(NTFSVolume volume, bool useMftMirror)
        {
            m_volume = volume;
            m_useMftMirror = useMftMirror;

            m_mftRecord = ReadMftRecord();
            AttributeDataLengthToMakeNonResident = m_volume.BytesPerFileRecordSegment * 5 / 16; // We immitate the NTFS v5.1 driver
        }

        private FileRecord ReadMftRecord()
        {
            NTFSBootRecord bootRecord = m_volume.BootRecord;

            if (bootRecord != null)
            {
                long mftStartLCN;
                if (m_useMftMirror)
                {
                    mftStartLCN = (long)bootRecord.MftMirrorStartLCN;
                }
                else
                {
                    mftStartLCN = (long)bootRecord.MftStartLCN;
                }
                
                FileRecordSegment mftRecordSegment = GetRecordSegmentOfMasterFileTable(mftStartLCN, MasterFileTableSegmentNumber);
                if (!mftRecordSegment.IsBaseFileRecord)
                {
                    throw new InvalidDataException("Invalid MFT record, not a base record");
                }

                AttributeRecord attributeListRecord = mftRecordSegment.GetImmediateAttributeRecord(AttributeType.AttributeList);
                if (attributeListRecord == null)
                {
                    return new FileRecord(m_volume, mftRecordSegment);
                }
                else
                {
                    // I have never personally seen an MFT with an attribute list
                    AttributeList attributeList = new AttributeList(m_volume, attributeListRecord);
                    List<MftSegmentReference> references = attributeList.GetSegmentReferenceList();
                    int baseSegmentIndex = MftSegmentReference.IndexOfSegmentNumber(references, MasterFileTableSegmentNumber);

                    if (baseSegmentIndex >= 0)
                    {
                        references.RemoveAt(baseSegmentIndex);
                    }

                    List<FileRecordSegment> recordSegments = new List<FileRecordSegment>();
                    // we want the base record segment first
                    recordSegments.Add(mftRecordSegment);

                    foreach (MftSegmentReference reference in references)
                    {
                        FileRecordSegment segment = GetRecordSegmentOfMasterFileTable(mftStartLCN, reference);
                        if (segment != null)
                        {
                            recordSegments.Add(segment);
                        }
                        else
                        {
                            throw new InvalidDataException("Invalid MFT record, missing segment");
                        }
                    }
                    return new FileRecord(m_volume, recordSegments);
                }
            }
            else
            {
                return null;
            }
        }

        private FileRecordSegment GetRecordSegmentOfMasterFileTable(long mftStartLCN, MftSegmentReference reference)
        {
            FileRecordSegment result = GetRecordSegmentOfMasterFileTable(mftStartLCN, reference.SegmentNumber);
            if (result.SequenceNumber != reference.SequenceNumber)
            {
                // The file record segment has been modified, and an older version has been requested
                return null;
            }
            return result;
        }

        /// <summary>
        /// We can't use GetFileRecordSegment before strapping the MFT
        /// </summary>
        private FileRecordSegment GetRecordSegmentOfMasterFileTable(long mftStartLCN, long segmentNumber)
        {
            long sectorIndex = mftStartLCN * m_volume.SectorsPerCluster + segmentNumber * m_volume.SectorsPerFileRecordSegment;
            byte[] bytes = m_volume.ReadSectors(sectorIndex, m_volume.SectorsPerFileRecordSegment);
            FileRecordSegment result = new FileRecordSegment(bytes, 0, m_volume.BytesPerSector, MasterFileTableSegmentNumber);
            return result;
        }

        public FileRecordSegment GetFileRecordSegment(MftSegmentReference reference)
        {
            FileRecordSegment result = GetFileRecordSegment(reference.SegmentNumber);
            if (result.SequenceNumber != reference.SequenceNumber)
            {
                // The file record segment has been modified, and an older version has been requested
                return null;
            }
            return result;
        }

        private FileRecordSegment GetFileRecordSegment(long segmentNumber)
        { 
            NTFSBootRecord bootRecord = m_volume.BootRecord;

            // Note: File record always start at the beginning of a sector
            // Note: Record can span multiple clusters, or alternatively, several records can be stored in the same cluster
            long firstSectorIndex = segmentNumber * m_volume.SectorsPerFileRecordSegment;
            byte[] segmentBytes = m_mftRecord.Data.ReadSectors(firstSectorIndex, m_volume.SectorsPerFileRecordSegment);

            if (FileRecordSegment.ContainsFileRecordSegment(segmentBytes))
            {
                FileRecordSegment recordSegment = new FileRecordSegment(segmentBytes, m_volume.BootRecord.BytesPerSector, segmentNumber);
                return recordSegment;
            }
            else
            {
                return null;
            }
        }

        public FileRecord GetFileRecord(MftSegmentReference reference)
        {
            FileRecord result = GetFileRecord(reference.SegmentNumber);
            if (result != null)
            {
                if (result.SequenceNumber != reference.SequenceNumber)
                {
                    // The file record segment has been modified, and an older version has been requested
                    return null;
                }
            }
            return result;
        }

        public FileRecord GetFileRecord(long baseSegmentNumber)
        {
            FileRecordSegment baseRecordSegment = GetFileRecordSegment(baseSegmentNumber);
            if (baseRecordSegment != null && baseRecordSegment.IsBaseFileRecord)
            {
                AttributeRecord attributeListRecord = baseRecordSegment.GetImmediateAttributeRecord(AttributeType.AttributeList);
                if (attributeListRecord == null)
                {
                    return new FileRecord(m_volume, baseRecordSegment);
                }
                else
                {
                    // The attribute list contains entries for every attribute the record has (excluding the attribute list),
                    // including attributes that reside within the base record segment.
                    AttributeList attributeList = new AttributeList(m_volume, attributeListRecord);
                    List<MftSegmentReference> references = attributeList.GetSegmentReferenceList();
                    int baseSegmentIndex = MftSegmentReference.IndexOfSegmentNumber(references, baseSegmentNumber);
                    
                    if (baseSegmentIndex >= 0)
                    {
                        references.RemoveAt(baseSegmentIndex);
                    }

                    List<FileRecordSegment> recordSegments = new List<FileRecordSegment>();
                    // we want the base record segment first
                    recordSegments.Add(baseRecordSegment);

                    foreach (MftSegmentReference reference in references)
                    {
                        FileRecordSegment segment = GetFileRecordSegment(reference);
                        if (segment != null)
                        {
                            recordSegments.Add(segment);
                        }
                        else
                        {
                            // record is invalid
                            return null;
                        }
                    }
                    return new FileRecord(m_volume, recordSegments);
                }
            }
            else
            {
                return null;
            }
        }

        public FileRecord GetMftRecord()
        {
            return m_mftRecord;
        }

        public FileRecord GetVolumeRecord()
        {
            return GetFileRecord(VolumeSegmentNumber);
        }

        public FileRecord GetBitmapRecord()
        {
            return GetFileRecord(BitmapSegmentNumber);
        }

        public void UpdateFileRecord(FileRecord record)
        {
            record.UpdateSegments(m_volume.BytesPerFileRecordSegment, m_volume.BytesPerSector, m_volume.MinorVersion);
            
            foreach (FileRecordSegment segment in record.Segments)
            {
                if (segment.MftSegmentNumber >= 0)
                {
                    UpdateFileRecordSegment(segment);
                }
                else
                {
                    // new segment, we must allocate space for it
                    throw new NotImplementedException();
                }
            }
        }

        public void UpdateFileRecordSegment(FileRecordSegment recordSegment)
        {
            long segmentNumber = recordSegment.MftSegmentNumber;
            long firstSectorIndex = segmentNumber * m_volume.SectorsPerFileRecordSegment;
            byte[] recordSegmentBytes = recordSegment.GetBytes(m_volume.BytesPerFileRecordSegment, m_volume.BytesPerCluster, m_volume.MinorVersion);

            m_mftRecord.Data.WriteSectors(firstSectorIndex, recordSegmentBytes);
        }

        // In NTFS v3.1 the FileRecord's self reference SegmentNumber is 32 bits,
        // but the MftSegmentReference's SegmentNumber is 48 bits.
        public long GetNumberOfUsableSegments()
        {
            return (long)(m_mftRecord.NonResidentDataRecord.FileSize / (uint)m_volume.BytesPerFileRecordSegment);
        }
    }
}
