using System;
using System.IO;
using System.Text;

namespace Utilities
{
    public class LittleEndianReader
    {
        public static short ReadInt16(byte[] buffer, ref int offset)
        {
            offset += 2;
            return LittleEndianConverter.ToInt16(buffer, offset - 2);
        }

        public static ushort ReadUInt16(byte[] buffer, ref int offset)
        {
            offset += 2;
            return LittleEndianConverter.ToUInt16(buffer, offset - 2);
        }

        public static int ReadInt32(byte[] buffer, ref int offset)
        {
            offset += 4;
            return LittleEndianConverter.ToInt32(buffer, offset - 4);
        }

        public static uint ReadUInt32(byte[] buffer, ref int offset)
        {
            offset += 4;
            return LittleEndianConverter.ToUInt32(buffer, offset - 4);
        }

        public static long ReadInt64(byte[] buffer, ref int offset)
        {
            offset += 8;
            return LittleEndianConverter.ToInt64(buffer, offset - 8);
        }

        public static ulong ReadUInt64(byte[] buffer, ref int offset)
        {
            offset += 8;
            return LittleEndianConverter.ToUInt64(buffer, offset - 8);
        }

        public static Guid ReadGuid(byte[] buffer, ref int offset)
        {
            offset += 16;
            return LittleEndianConverter.ToGuid(buffer, offset - 16);
        }

        public static short ReadInt16(Stream stream)
        {
            byte[] buffer = new byte[2];
            stream.Read(buffer, 0, 2);
            return LittleEndianConverter.ToInt16(buffer, 0);
        }

        public static ushort ReadUInt16(Stream stream)
        {
            byte[] buffer = new byte[2];
            stream.Read(buffer, 0, 2);
            return LittleEndianConverter.ToUInt16(buffer, 0);
        }

        public static int ReadInt32(Stream stream)
        {
            byte[] buffer = new byte[4];
            stream.Read(buffer, 0, 4);
            return LittleEndianConverter.ToInt32(buffer, 0);
        }

        public static uint ReadUInt32(Stream stream)
        {
            byte[] buffer = new byte[4];
            stream.Read(buffer, 0, 4);
            return LittleEndianConverter.ToUInt32(buffer, 0);
        }

        public static long ReadInt64(Stream stream)
        {
            byte[] buffer = new byte[8];
            stream.Read(buffer, 0, 8);
            return LittleEndianConverter.ToInt64(buffer, 0);
        }

        public static ulong ReadUInt64(Stream stream)
        {
            byte[] buffer = new byte[8];
            stream.Read(buffer, 0, 8);
            return LittleEndianConverter.ToUInt64(buffer, 0);
        }

        public static Guid ReadGuidBytes(Stream stream)
        {
            byte[] buffer = new byte[16];
            stream.Read(buffer, 0, 16);
            return LittleEndianConverter.ToGuid(buffer, 0);
        }
    }
}
