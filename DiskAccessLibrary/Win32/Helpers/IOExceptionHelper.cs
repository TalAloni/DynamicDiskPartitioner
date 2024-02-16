/* Copyright (C) 2018-2024 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;
using System.IO;
using System.Reflection;

namespace DiskAccessLibrary
{
    public class IOExceptionHelper
    {
        public static ushort GetWin32ErrorCode(IOException ex)
        {
            int hResult = GetExceptionHResult(ex);
            // The Win32 error code is stored in the 16 first bits of the value
            return (ushort)(hResult & 0x0000FFFF);
        }

        public static int GetExceptionHResult(IOException ex)
        {
            PropertyInfo hResult = ex.GetType().GetProperty("HResult", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            return (int)hResult.GetValue(ex, null);
        }

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

        /// <param name="win32ErrorCode">The Win32 error code associated with the exception to throw</param>
        public static void ThrowIOError(int win32ErrorCode, string defaultMessage)
        {
            if (win32ErrorCode == (int)Win32Error.ERROR_ACCESS_DENIED)
            {
                // UnauthorizedAccessException will be thrown if stream was opened only for writing or if a user is not an administrator
                throw new UnauthorizedAccessException(defaultMessage);
            }
            else if (win32ErrorCode == (int)Win32Error.ERROR_SHARING_VIOLATION)
            {
                throw new SharingViolationException(defaultMessage);
            }
            else if (win32ErrorCode == (int)Win32Error.ERROR_SECTOR_NOT_FOUND)
            {
                string message = defaultMessage + " The sector does not exist.";
                int hresult = GetHResultFromWin32Error((Win32Error)win32ErrorCode);
                throw new IOException(message, hresult);
            }
            else if (win32ErrorCode == (int)Win32Error.ERROR_CRC)
            {
                string message = defaultMessage + " Data Error (Cyclic Redundancy Check).";
                throw new CyclicRedundancyCheckException(message);
            }
            else if (win32ErrorCode == (int)Win32Error.ERROR_NO_SYSTEM_RESOURCES)
            {
                throw new OutOfMemoryException();
            }
            else
            {
                string message = defaultMessage + String.Format(" Win32 Error: {0}", win32ErrorCode);
                int hresult = GetHResultFromWin32Error((Win32Error)win32ErrorCode);
                throw new IOException(message, hresult);
            }
        }
    }
}
