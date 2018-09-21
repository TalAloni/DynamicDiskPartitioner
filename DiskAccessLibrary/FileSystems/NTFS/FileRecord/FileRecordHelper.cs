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
    public class FileRecordHelper
    {
        /// <remarks>
        /// Only non-resident attributes can be fragmented.
        /// References:
        /// https://docs.microsoft.com/en-us/previous-versions/windows/it-pro/windows-2000-server/cc976808(v=technet.10)
        /// https://blogs.technet.microsoft.com/askcore/2009/10/16/the-four-stages-of-ntfs-file-growth/
        /// </remarks>
        public static List<AttributeRecord> GetAssembledAttributes(List<FileRecordSegment> segments)
        {
            List<AttributeRecord> result = new List<AttributeRecord>();
            // If two non-resident attributes have the same AttributeType and Name, then we need to assemble them back together.
            // Additional fragments immediately follow after the initial fragment.
            AttributeType currentAttributeType = AttributeType.None;
            string currentAttributeName = String.Empty;
            List<NonResidentAttributeRecord> fragments = new List<NonResidentAttributeRecord>();
            foreach (FileRecordSegment segment in segments)
            {
                foreach (AttributeRecord attribute in segment.ImmediateAttributes)
                {
                    if (attribute.AttributeType == AttributeType.AttributeList)
                    {
                        continue;
                    }

                    bool additionalFragment = (attribute is NonResidentAttributeRecord) && (fragments.Count > 0) &&
                                              (attribute.AttributeType == currentAttributeType) && (attribute.Name == currentAttributeName);

                    if (!additionalFragment && fragments.Count > 0)
                    {
                        NonResidentAttributeRecord assembledAttribute = AssembleFragments(fragments, segments[0].NextAttributeInstance);
                        segments[0].NextAttributeInstance++;
                        result.Add(assembledAttribute);
                        fragments.Clear();
                    }

                    if (attribute is ResidentAttributeRecord)
                    {
                        result.Add(attribute);
                    }
                    else
                    {
                        fragments.Add((NonResidentAttributeRecord)attribute);
                        if (!additionalFragment)
                        {
                            currentAttributeType = attribute.AttributeType;
                            currentAttributeName = attribute.Name;
                        }
                    }
                }
            }

            if (fragments.Count > 0)
            {
                NonResidentAttributeRecord assembledAttribute = AssembleFragments(fragments, segments[0].NextAttributeInstance);
                segments[0].NextAttributeInstance++;
                result.Add(assembledAttribute);
            }

            return result;
        }

        private static NonResidentAttributeRecord AssembleFragments(List<NonResidentAttributeRecord> attributeFragments, ushort nextAttributeInstance)
        {
            // Attribute fragments are written to disk sorted by LowestVCN
            NonResidentAttributeRecord firstFragment = attributeFragments[0];
            if (firstFragment.LowestVCN != 0)
            {
                string message = String.Format("Attribute fragments must be sorted. Attribute type: {0}", firstFragment.AttributeType);
                throw new InvalidDataException(message);
            }

            NonResidentAttributeRecord attribute = new NonResidentAttributeRecord(firstFragment.AttributeType, firstFragment.Name, nextAttributeInstance);
            attribute.Flags = firstFragment.Flags;
            attribute.LowestVCN = 0;
            attribute.HighestVCN = -1;
            attribute.CompressionUnit = firstFragment.CompressionUnit;
            attribute.AllocatedLength = firstFragment.AllocatedLength;
            attribute.FileSize = firstFragment.FileSize;
            attribute.ValidDataLength = firstFragment.ValidDataLength;

            foreach(NonResidentAttributeRecord attributeFragment in attributeFragments)
            {
                if (attributeFragment.LowestVCN == attribute.HighestVCN + 1)
                {
                    // The DataRunSequence of each NonResidentDataRecord fragment starts at absolute LCN,
                    // We need to convert it to relative offset before adding it to the base DataRunSequence
                    long runLength = attributeFragment.DataRunSequence[0].RunLength;
                    long absoluteOffset = attributeFragment.DataRunSequence[0].RunOffset;
                    long previousLCN = attribute.DataRunSequence.LastDataRunStartLCN;
                    long relativeOffset = absoluteOffset - previousLCN;

                    int runIndex = attribute.DataRunSequence.Count;
                    attribute.DataRunSequence.AddRange(attributeFragment.DataRunSequence);
                    attribute.DataRunSequence[runIndex] = new DataRun(runLength, relativeOffset);
                    attribute.HighestVCN = attributeFragment.HighestVCN;
                }
                else
                {
                    throw new InvalidDataException("Invalid attribute fragments order");
                }
            }

            return attribute;
        }

        public static void InsertSorted(List<AttributeRecord> attributes, AttributeRecord attribute)
        {
            int insertIndex = SortedList<AttributeRecord>.FindIndexForSortedInsert(attributes, CompareAttributeTypes, attribute);
            attributes.Insert(insertIndex, attribute);
        }

        private static int CompareAttributeTypes(AttributeRecord attribute1, AttributeRecord attribute2)
        {
            return attribute1.AttributeType.CompareTo(attribute2.AttributeType);
        }
    }
}
