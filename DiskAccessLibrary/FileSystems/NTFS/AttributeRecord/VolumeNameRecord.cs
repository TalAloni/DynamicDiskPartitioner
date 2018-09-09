/* Copyright (C) 2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;
using Utilities;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    /// <remarks>
    /// VolumeName attribute is always resident.
    /// </remarks>
    public class VolumeNameRecord : ResidentAttributeRecord
    {
        public string VolumeName;

        public VolumeNameRecord(string name, ushort instance) : base(AttributeType.VolumeName, name, instance)
        {
            VolumeName = String.Empty;
        }

        public VolumeNameRecord(byte[] buffer, int offset) : base(buffer, offset)
        {
            VolumeName = Encoding.Unicode.GetString(this.Data);
        }

        public override byte[] GetBytes(int bytesPerCluster)
        {
            this.Data = Encoding.Unicode.GetBytes(VolumeName);

            return base.GetBytes(bytesPerCluster);
        }

        public override ulong DataLength
        {
            get
            {
                return (ulong)VolumeName.Length * 2;
            }
        }
    }
}
