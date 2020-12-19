/* Copyright (C) 2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace DiskAccessLibrary.Tests
{
    public class ChkdskHelper
    {
        private static readonly string ChkdskExecutablePath = "chkdsk.exe";

        /// <returns>true if no errors were found</returns>
        public static bool Chkdsk(string driveName)
        {
            if (driveName.EndsWith("\\"))
            {
                driveName = driveName.Substring(0, 2);
            }

            Process process = Process.Start(ChkdskExecutablePath, driveName);
            process.WaitForExit();
            // CHKDSK exit codes:
            // 0 - No errors were found.
            // 1 - Errors were found and fixed.
            // 2 - Performed disk cleanup (such as garbage collection) or did not perform cleanup because /f was not specified.
            // 3 - Could not check the disk, errors could not be fixed, or errors were not fixed because /f was not specified.

            // Note: Exit code 2 reported by CHKDSK effectively means that no errors were found.
            // I have ran CHKDSK on two identical copies of the same freshly formatted volume and observed it returning 0 in one case and 2 in another after the initial mount.
            return (process.ExitCode == 0 || process.ExitCode == 2);
        }
    }
}
