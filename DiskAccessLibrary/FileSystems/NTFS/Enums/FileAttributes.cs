using System;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    [Flags]
    public enum FileAttributes : uint
    {
        Readonly = 0x00000001,    // FILE_ATTRIBUTE_READONLY
        Hidden = 0x00000002,      // FILE_ATTRIBUTE_HIDDEN
        System = 0x00000004,      // FILE_ATTRIBUTE_SYSTEM
        Directory = 0x00000010,   // FILE_ATTRIBUTE_DIRECTORY
        Archive = 0x00000020,     // FILE_ATTRIBUTE_ARCHIVE
        Normal = 0x00000080,      // FILE_ATTRIBUTE_NORMAL
        Temporary = 0x00000100,   // FILE_ATTRIBUTE_TEMPORARY
        Reserved0 = 0x00000200,   // FILE_ATTRIBUTE_RESERVED0
        Reserved1 = 0x00000400,   // FILE_ATTRIBUTE_RESERVED1
        Compressed = 0x00000800,  // FILE_ATTRIBUTE_COMPRESSED
        Offline = 0x00001000,     // FILE_ATTRIBUTE_OFFLINE
        PropertySet = 0x00002000, // FILE_ATTRIBUTE_PROPERTY_SET
    }
}
