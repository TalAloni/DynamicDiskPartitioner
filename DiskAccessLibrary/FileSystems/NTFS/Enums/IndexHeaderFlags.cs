using System;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    [Flags]
    public enum IndexHeaderFlags : byte
    {
        LargeIndex = 0x01, // INDEX_NODE, denotes the presence of IndexAllocation record
    }
}
