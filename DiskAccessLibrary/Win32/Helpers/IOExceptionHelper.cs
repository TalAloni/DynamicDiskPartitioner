/* Copyright (C) 2018 Tal Aloni <tal.aloni.il@gmail.com>. All rights reserved.
 * 
 * You can redistribute this program and/or modify it under the terms of
 * the GNU Lesser Public License as published by the Free Software Foundation,
 * either version 3 of the License, or (at your option) any later version.
 */
using System;

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
    }
}
