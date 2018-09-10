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
                    bool additionalFragment = (attribute is NonResidentAttributeRecord) && (fragments.Count > 0) &&
                                              (attribute.AttributeType == currentAttributeType) && (attribute.Name == currentAttributeName);

                    if (!additionalFragment && fragments.Count > 0)
                    {
                        NonResidentAttributeRecord assembledAttribute = AssembleFragments(fragments);
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
                NonResidentAttributeRecord assembledAttribute = AssembleFragments(fragments);
                result.Add(assembledAttribute);
            }

            return result;
        }

        private static NonResidentAttributeRecord AssembleFragments(List<NonResidentAttributeRecord> attributeFragments)
        {
            // Attribute fragments are written to disk sorted by LowestVCN
            NonResidentAttributeRecord baseAttribute = attributeFragments[0];
            if (baseAttribute.LowestVCN != 0)
            {
                string message = String.Format("Attribute fragments must be sorted. Attribute type: {0}", baseAttribute.AttributeType);
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

            return baseAttribute;
        }
    }
}
