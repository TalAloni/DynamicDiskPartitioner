/* Copyright (C) 2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace DiskAccessLibrary.Tests
{
    public class VHDMountHelper
    {
        private static readonly string VHDMountExecutablePath = @"C:\Program Files\Microsoft Virtual Server\Vhdmount\vhdmount.exe";

        public static bool IsVHDMountInstalled()
        {
            return File.Exists(VHDMountExecutablePath);
        }

        public static void MountVHD(string path)
        {
            if (!IsVHDMountInstalled())
            {
                throw new FileNotFoundException("vhdmount.exe was not found, please install Virtual Server 2005 R2 SP1 (select the VHDMount component)");
            }

            string arguments = String.Format("/p /f \"{0}\"", path);
            Process process = Process.Start(VHDMountExecutablePath, arguments);
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new Exception("Failed to mount the VHD");
            }
        }

        public static void UnmountVHD(string path)
        {
            string arguments = String.Format("/u \"{0}\"", path);
            int count = 0;
            // Sometimes a volume needs some time before it can be dismounted successfully
            while (count < 3)
            {
                Process process = Process.Start(VHDMountExecutablePath, arguments);
                process.WaitForExit();
                if (process.ExitCode == 0)
                {
                    return;
                }

                count++;
            }

            throw new Exception("Failed to unmount the VHD");
        }
    }
}
