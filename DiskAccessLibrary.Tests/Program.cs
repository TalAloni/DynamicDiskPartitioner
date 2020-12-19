/* Copyright (C) 2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Text;

namespace DiskAccessLibrary.Tests
{
    public class Program
    {
        private static void Main(string[] args)
        {
            string path;
            if (args.Length == 0)
            {
                path = @"C:\Temp.vhd";
            }
            else
            {
                path = args[0];
                if (!path.ToLower().EndsWith(".vhd"))
                {
                    Console.WriteLine("vhdmount.exe requires a .vhd extension");
                    return;
                }
            }

            FileRecordTests.Tests();
            long size = 100 * 1024 * 1024;
            RawDiskImageTests.Test(path, size);
            if (!VHDMountHelper.IsVHDMountInstalled())
            {
                Console.WriteLine("vhdmount.exe was not found!");
                Console.WriteLine("Please install Virtual Server 2005 R2 SP1 (select the VHDMount component)");
                return;
            }

            NTFSFormatTests.Test(path, size);
            NTFSLogTests.Test(path, size);
            NTFSVolumeTests.Test(path, size);
        }
    }
}
