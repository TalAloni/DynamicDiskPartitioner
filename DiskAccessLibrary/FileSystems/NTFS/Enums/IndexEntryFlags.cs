using System;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    [Flags]
    public enum IndexEntryFlags : ushort
    {
        PointsToSubnode = 0x01, // INDEX_ENTRY_NODE
        LastEntryInNode = 0x02, // INDEX_ENTRY_END
    }
}
