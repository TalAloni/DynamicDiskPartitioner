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
        internal const int LastReservedMftSegmentNumber = 15; // 12-15 are reserved for additional metafiles
        private const int ExtendGranularity = 16; // The number of records added to the MFT when extending it, MUST be multiple of 8

        private const long MasterFileTableSegmentNumber = 0;
        private const long MftMirrorSegmentNumber = 1;
        private const long LogFileSegmentNumber = 2;
        private const long VolumeSegmentNumber = 3;
        private const long AttrDefSegmentNumber = 4;
        internal const long RootDirSegmentNumber = 5;
        internal const long BitmapSegmentNumber = 6;
        private const long BootSegmentNumber = 7;
        private const long BadClusSegmentNumber = 8;
        private const long SecureSegmentNumber = 9;
        private const long UpCaseSegmentNumber = 10;
        private const long ExtendSegmentNumber = 11;
        // The $Extend Metafile is simply a directory index that contains information on where to locate the last four metafiles ($ObjId, $Quota, $Reparse and $UsnJrnl)
        internal readonly int AttributeRecordLengthToMakeNonResident;
        public readonly MftSegmentReference RootDirSegmentReference = new MftSegmentReference(RootDirSegmentNumber, (ushort)RootDirSegmentNumber);

        private NTFSVolume m_volume;
        private FileRecord m_mftRecord;
        private NTFSFile m_mftFile;

        public MasterFileTable(NTFSVolume volume) : this(volume, false, false)
        {
        }

        /// <param name="useMftMirror">Strap the MFT using the MFT mirror</param>
        public MasterFileTable(NTFSVolume volume, bool useMftMirror) : this(volume, useMftMirror, false)
        {
        }

        /// <param name="useMftMirror">Strap the MFT using the MFT mirror</param>
        public MasterFileTable(NTFSVolume volume, bool useMftMirror, bool manageMftMirror)
        {
            m_volume = volume;
            m_mftRecord = ReadMftRecord(useMftMirror, manageMftMirror);
            m_mftFile = new NTFSFile(m_volume, m_mftRecord);
            AttributeRecordLengthToMakeNonResident = m_volume.BytesPerFileRecordSegment * 5 / 16; // We immitate the NTFS v5.1 driver
        }

        private FileRecord ReadMftRecord(bool useMftMirror, bool readMftMirror)
        {
            NTFSBootRecord bootRecord = m_volume.BootRecord;

            if (bootRecord != null)
            {
                long mftStartLCN = useMftMirror ? (long)bootRecord.MftMirrorStartLCN : (long)bootRecord.MftStartLCN;
                long mftSegmentNumber = readMftMirror ? MftMirrorSegmentNumber : MasterFileTableSegmentNumber;
                FileRecordSegment mftRecordSegment = ReadFileRecordSegment(mftStartLCN, mftSegmentNumber);
                if (!mftRecordSegment.IsBaseFileRecord)
                {
                    throw new InvalidDataException("Invalid MFT record, not a base record");
                }

                AttributeRecord attributeListRecord = mftRecordSegment.GetImmediateAttributeRecord(AttributeType.AttributeList);
                if (attributeListRecord == null)
                {
                    return new FileRecord(mftRecordSegment);
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
                        FileRecordSegment segment = ReadFileRecordSegment(mftStartLCN, reference);
                        if (segment != null)
                        {
                            recordSegments.Add(segment);
                        }
                        else
                        {
                            throw new InvalidDataException("Invalid MFT record, missing segment");
                        }
                    }
                    return new FileRecord(recordSegments);
                }
            }
            else
            {
                return null;
            }
        }

        private FileRecordSegment ReadFileRecordSegment(long mftStartLCN, MftSegmentReference reference)
        {
            FileRecordSegment result = ReadFileRecordSegment(mftStartLCN, reference.SegmentNumber);
            if (result.SequenceNumber != reference.SequenceNumber)
            {
                // The file record segment has been freed and reallocated, and an obsolete version is being requested
                return null;
            }
            return result;
        }

        /// <summary>
        /// This method is used to read the record segment of the MFT itself.
        /// Only after strapping the MFT we can use GetFileRecordSegment which relies on the MFT file record.
        /// </summary>
        private FileRecordSegment ReadFileRecordSegment(long mftStartLCN, long segmentNumber)
        {
            long sectorIndex = mftStartLCN * m_volume.SectorsPerCluster + segmentNumber * m_volume.SectorsPerFileRecordSegment;
            byte[] bytes = m_volume.ReadSectors(sectorIndex, m_volume.SectorsPerFileRecordSegment);
            FileRecordSegment result = new FileRecordSegment(bytes, 0, m_volume.BytesPerSector, segmentNumber);
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
            byte[] segmentBytes = m_mftFile.Data.ReadSectors(firstSectorIndex, m_volume.SectorsPerFileRecordSegment);

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

        public FileRecord GetFileRecord(MftSegmentReference fileFeference)
        {
            FileRecord result = GetFileRecord(fileFeference.SegmentNumber);
            if (result != null)
            {
                if (result.BaseRecordSequenceNumber != fileFeference.SequenceNumber)
                {
                    // The file record segment has been freed and reallocated, and an obsolete version is being requested
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
                    return new FileRecord(baseRecordSegment);
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
                    return new FileRecord(recordSegments);
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
                if (segment.SegmentNumber >= 0)
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
            long segmentNumber = recordSegment.SegmentNumber;
            long firstSectorIndex = segmentNumber * m_volume.SectorsPerFileRecordSegment;
            byte[] recordSegmentBytes = recordSegment.GetBytes(m_volume.BytesPerFileRecordSegment, m_volume.BytesPerCluster, m_volume.MinorVersion);

            m_mftFile.Data.WriteSectors(firstSectorIndex, recordSegmentBytes);
        }

        public void Extend()
        {
            ulong additionalDataLength = (ulong)(m_volume.BytesPerFileRecordSegment * ExtendGranularity);
            ulong additionalBitmapLength = ExtendGranularity / 8;
            // We calculate the maximum possible number of free cluster required
            long numberOfClustersRequiredForData = (long)Math.Ceiling((double)additionalDataLength / m_volume.BytesPerCluster);
            long numberOfClustersRequiredForBitmap = (long)Math.Ceiling((double)additionalBitmapLength / m_volume.BytesPerCluster);
            if (numberOfClustersRequiredForData + numberOfClustersRequiredForBitmap > m_volume.NumberOfFreeClusters)
            {
                throw new DiskFullException();
            }

            // We have to extend the bitmap first because one of the constructor parameters is the size of the data
            if (additionalBitmapLength > 0)
            {
                m_mftFile.Bitmap.ExtendBitmap(ExtendGranularity);
            }
            // MFT Data: ValidDataLength must be equal to FileSize
            ulong currentDataLength = m_mftFile.Data.Length;
            m_mftFile.WriteData(currentDataLength, new byte[additionalDataLength]);

            // The NTFS v5.1 driver does not bother updating the FileNameRecord
            m_mftRecord.FileNameRecord.AllocatedLength = m_mftFile.Data.AllocatedLength;
            m_mftRecord.FileNameRecord.FileSize = m_mftFile.Data.Length;
            UpdateFileRecord(m_mftRecord);

            // Update the MFT mirror
            MasterFileTable mftMirror = new MasterFileTable(m_volume, true, true);
            FileRecord mftRecordFromMirror = mftMirror.GetFileRecord(MasterFileTableSegmentNumber);
            mftRecordFromMirror.RemoveAttributeRecord(AttributeType.Data, String.Empty);
            mftRecordFromMirror.RemoveAttributeRecord(AttributeType.Bitmap, String.Empty);
            mftRecordFromMirror.Attributes.Add(m_mftFile.Data.AttributeRecord);
            mftRecordFromMirror.Attributes.Add(m_mftFile.Bitmap.AttributeRecord);
            mftRecordFromMirror.FileNameRecord.AllocatedLength = m_mftFile.Data.AllocatedLength;
            mftRecordFromMirror.FileNameRecord.FileSize = m_mftFile.Data.Length;
            mftMirror.UpdateFileRecord(mftRecordFromMirror);
        }

        // In NTFS v3.1 the FileRecord's self reference SegmentNumber is 32 bits,
        // but the MftSegmentReference's SegmentNumber is 48 bits.
        public long GetNumberOfUsableSegments()
        {
            return (long)(m_mftRecord.NonResidentDataRecord.FileSize / (uint)m_volume.BytesPerFileRecordSegment);
        }
    }
}
