/* Copyright (C) 2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace DiskAccessLibrary.Tests
{
    public class MountHelper
    {
        public static string WaitForDriveToMount(string volumeLabel)
        {
            return WaitForDriveToMount(volumeLabel, 30);
        }

        public static string WaitForDriveToMount(string volumeLabel, int timeout)
        {
            int count = 0;
            while (count < timeout)
            {
                string driveName = GetDriveByVolumeLabel(volumeLabel);
                if (driveName != null)
                {
                    return driveName;
                }
                Thread.Sleep(1000);
                count++;
            }

            return null;
        }

        public static string GetDriveByVolumeLabel(string volumeLabel)
        {
            DriveInfo[] drives = DriveInfo.GetDrives();
            foreach (DriveInfo drive in drives)
            {
                if (drive.DriveType == DriveType.Fixed && drive.VolumeLabel == volumeLabel)
                {
                    return drive.Name;
                }
            }

            return null;
        }
    }
}
