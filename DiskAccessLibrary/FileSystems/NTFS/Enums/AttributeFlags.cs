using System;

namespace DiskAccessLibrary.FileSystems.NTFS
{
    /// <remarks>
    /// https://docs.microsoft.com/en-us/windows/desktop/DevNotes/attribute-record-header
    /// </remarks>
    [Flags]
    public enum AttributeFlags : ushort
    {
        Encrypted = 0x4000,       // ATTRIBUTE_FLAG_ENCRYPTED
        Sparse = 0x8000,          // ATTRIBUTE_FLAG_SPARSE
        CompressionMask = 0x00FF, // ATTRIBUTE_FLAG_COMPRESSION_MASK
    }
}
