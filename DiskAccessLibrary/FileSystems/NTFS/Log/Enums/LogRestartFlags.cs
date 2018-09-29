using System;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    [Flags]
    public enum LogRestartFlags : ushort
    {
        RestartSinglePageIO = 0x0001, // RESTART_SINGLE_PAGE_IO
    }
}
