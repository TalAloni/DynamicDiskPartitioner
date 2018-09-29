/* Copyright (C) 2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.IO;

namespace DiskAccessLibrary
{
    public class IOExceptionHelper
    {
        public static int GetHResultFromWin32Error(Win32Error error)
        {
            if (error == Win32Error.ERROR_SUCCESS)
            {
                return 0;
            }
            else
            {
                return (int)(0x80070000 | (ushort)error);
            }
        }

        /// <param name="errorCode">The Win32 error code associated with this exception</param>
        internal static void ThrowIOError(int errorCode, string defaultMessage)
        {
            if (errorCode == (int)Win32Error.ERROR_ACCESS_DENIED)
            {
                // UnauthorizedAccessException will be thrown if stream was opened only for writing or if a user is not an administrator
                throw new UnauthorizedAccessException(defaultMessage);
            }
            else if (errorCode == (int)Win32Error.ERROR_SHARING_VIOLATION)
            {
                throw new SharingViolationException(defaultMessage);
            }
            else if (errorCode == (int)Win32Error.ERROR_SECTOR_NOT_FOUND)
            {
                string message = defaultMessage + " The sector does not exist.";
                throw new IOException(message, (int)Win32Error.ERROR_SECTOR_NOT_FOUND);
            }
            else if (errorCode == (int)Win32Error.ERROR_CRC)
            {
                string message = defaultMessage + " Data Error (Cyclic Redundancy Check).";
                throw new CyclicRedundancyCheckException(message);
            }
            else if (errorCode == (int)Win32Error.ERROR_NO_SYSTEM_RESOURCES)
            {
                throw new OutOfMemoryException();
            }
            else
            {
                string message = defaultMessage + String.Format(" Win32 Error: {0}", errorCode);
                throw new IOException(message, errorCode);
            }
        }
    }
}
