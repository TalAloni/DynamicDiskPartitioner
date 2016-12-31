/* Copyright (C) 2016 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Drawing;
using DiskAccessLibrary;
using DiskAccessLibrary.LogicalDiskManager;

namespace DynamicDiskPartitioner
{
    public class DiskStyling
    {
        public static Brush GetVolumeBrush(Volume volume)
        {
            if (volume is Partition)
            {
                return Brushes.Navy;
            }
            else if (volume is SimpleVolume || volume is SpannedVolume)
            {
                return Brushes.Olive;
            }
            else if (volume is MirroredVolume)
            {
                return Brushes.Maroon;
            }
            else if (volume is StripedVolume)
            {
                return Brushes.Teal;
            }
            else if (volume is Raid5Volume)
            {
                return Brushes.Cyan;
            }
            else
            {
                return Brushes.Black;
            }
        }
    }
}
