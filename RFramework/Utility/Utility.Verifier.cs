using System;
using System.Buffers;
using System.IO;

namespace RFramework
{
    /// <summary>
    /// 框架通用工具入口。
    /// </summary>
    public static partial class Utility
    {
        /// <summary>
        /// 数据完整性校验工具。
        /// </summary>
        public static class Verifier
        {
            private const uint Crc32Polynomial = 0xEDB88320u;
            private const uint Crc32InitialValue = uint.MaxValue;
            private const int StreamBufferSize = 8192;

            private static readonly uint[] Crc32Table = CreateCrc32Table();

        /// <summary>
        /// 计算完整字节数组的标准 CRC-32 校验值。
        /// </summary>
        /// <param name="bytes">待校验字节数组。</param>
        /// <returns>CRC-32 校验值。</returns>
        public static int GetCrc32(byte[] bytes)
            {
                if (bytes == null)
                {
                    throw new RFrameworkException("CRC32 source bytes cannot be null.");
                }

                return GetCrc32(bytes, 0, bytes.Length);
            }

        /// <summary>
        /// 计算字节数组指定片段的标准 CRC-32 校验值。
        /// </summary>
        /// <param name="bytes">待校验字节数组。</param>
        /// <param name="offset">起始偏移。</param>
        /// <param name="length">参与校验的字节数。</param>
        /// <returns>CRC-32 校验值。</returns>
        public static int GetCrc32(byte[] bytes, int offset, int length)
            {
                ValidateSegment(bytes, offset, length);
                uint state = UpdateCrc32(Crc32InitialValue, bytes, offset, length);
                return unchecked((int)~state);
            }

        /// <summary>
        /// 从流的当前位置读取到末尾并计算标准 CRC-32 校验值，不关闭输入流。
        /// </summary>
        /// <param name="stream">待校验输入流。</param>
        /// <returns>CRC-32 校验值。</returns>
        public static int GetCrc32(Stream stream)
            {
                if (stream == null)
                {
                    throw new RFrameworkException("CRC32 source stream cannot be null.");
                }

                byte[] buffer = ArrayPool<byte>.Shared.Rent(StreamBufferSize);
                uint state = Crc32InitialValue;
                try
                {
                    int count;
                    while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        state = UpdateCrc32(state, buffer, 0, count);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                return unchecked((int)~state);
            }

        /// <summary>
        /// 将 CRC32 数值转换为四字节大端序数组。
        /// </summary>
        /// <param name="crc32">CRC-32 校验值。</param>
        /// <returns>四字节大端序数组。</returns>
        public static byte[] GetCrc32Bytes(int crc32)
            {
                byte[] result = new byte[sizeof(int)];
                WriteBigEndian(crc32, result, 0);
                return result;
            }

        /// <summary>
        /// 将 CRC32 数值以大端序写入目标数组起始位置。
        /// </summary>
        /// <param name="crc32">CRC-32 校验值。</param>
        /// <param name="bytes">目标字节数组。</param>
        public static void GetCrc32Bytes(int crc32, byte[] bytes)
            {
                WriteBigEndian(crc32, bytes, 0);
            }

        /// <summary>
        /// 将 CRC32 数值以大端序写入目标数组指定位置。
        /// </summary>
        /// <param name="crc32">CRC-32 校验值。</param>
        /// <param name="bytes">目标字节数组。</param>
        /// <param name="offset">写入起始偏移。</param>
        public static void GetCrc32Bytes(int crc32, byte[] bytes, int offset)
            {
                WriteBigEndian(crc32, bytes, offset);
            }

            private static void WriteBigEndian(int crc32, byte[] destination, int offset)
            {
                if (destination == null)
                {
                    throw new RFrameworkException("CRC32 destination bytes cannot be null.");
                }

                if (offset < 0 || offset > destination.Length - sizeof(int))
                {
                    throw new RFrameworkException("CRC32 destination offset is outside the array.");
                }

                uint value = unchecked((uint)crc32);
                destination[offset] = (byte)(value >> 24);
                destination[offset + 1] = (byte)(value >> 16);
                destination[offset + 2] = (byte)(value >> 8);
                destination[offset + 3] = (byte)value;
            }

            private static void ValidateSegment(byte[] bytes, int offset, int length)
            {
                if (bytes == null)
                {
                    throw new RFrameworkException("CRC32 source bytes cannot be null.");
                }

                if (offset < 0 || length < 0 || offset > bytes.Length
                    || length > bytes.Length - offset)
                {
                    throw new RFrameworkException("CRC32 source range is outside the array.");
                }
            }

            private static uint UpdateCrc32(
                uint state, byte[] bytes, int offset, int length)
            {
                int end = offset + length;
                for (int index = offset; index < end; index++)
                {
                    int tableIndex = (byte)(state ^ bytes[index]);
                    state = Crc32Table[tableIndex] ^ (state >> 8);
                }

                return state;
            }

            private static uint[] CreateCrc32Table()
            {
                uint[] table = new uint[256];
                for (int index = 0; index < table.Length; index++)
                {
                    uint remainder = (uint)index;
                    for (int bit = 0; bit < 8; bit++)
                    {
                        remainder = (remainder & 1u) == 0u
                            ? remainder >> 1
                            : (remainder >> 1) ^ Crc32Polynomial;
                    }

                    table[index] = remainder;
                }

                return table;
            }
        }
    }
}
