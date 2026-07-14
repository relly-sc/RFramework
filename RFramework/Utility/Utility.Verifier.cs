
using System;
using System.IO;

namespace RFramework
{
    public static partial class Utility
    {
        /// <summary>
        /// 校验相关的实用函数。
        /// </summary>
        public static partial class Verifier
        {
            /// <summary>
            /// CRC32 计算的缓存字节数组长度。
            /// </summary>
            private const int CachedBytesLength = 0x1000;
            private static readonly object s_SyncRoot = new object();
            /// <summary>
            /// CRC32 计算使用的缓存字节数组。
            /// </summary>
            private static readonly byte[] s_CachedBytes = new byte[CachedBytesLength];
            /// <summary>
            /// CRC32 算法实例。
            /// </summary>
            private static readonly Crc32 s_Algorithm = new Crc32();

            /// <summary>
            /// 计算二进制流的 CRC32。
            /// </summary>
            /// <param name="bytes">指定的二进制流。</param>
            /// <returns>计算后的 CRC32。</returns>
            public static int GetCrc32(byte[] bytes)
            {
                if (bytes == null)
                {
                    throw new RFrameworkException("Bytes is invalid.");
                }

                return GetCrc32(bytes, 0, bytes.Length);
            }

            /// <summary>
            /// 计算二进制流的 CRC32。
            /// </summary>
            /// <param name="bytes">指定的二进制流。</param>
            /// <param name="offset">二进制流的偏移。</param>
            /// <param name="length">二进制流的长度。</param>
            /// <returns>计算后的 CRC32。</returns>
            public static int GetCrc32(byte[] bytes, int offset, int length)
            {
                if (bytes == null)
                {
                    throw new RFrameworkException("Bytes is invalid.");
                }

                if (offset < 0 || length < 0 || offset > bytes.Length - length)
                {
                    throw new RFrameworkException("Offset or length is invalid.");
                }

                lock (s_SyncRoot)
                {
                    try
                    {
                        s_Algorithm.HashCore(bytes, offset, length);
                        return (int)s_Algorithm.HashFinal();
                    }
                    finally
                    {
                        s_Algorithm.Initialize();
                    }
                }
            }

            /// <summary>
            /// 计算二进制流的 CRC32。
            /// </summary>
            /// <param name="stream">指定的二进制流。</param>
            /// <returns>计算后的 CRC32。</returns>
            public static int GetCrc32(Stream stream)
            {
                if (stream == null)
                {
                    throw new RFrameworkException("Stream is invalid.");
                }

                lock (s_SyncRoot)
                {
                    try
                    {
                        while (true)
                        {
                            int bytesRead = stream.Read(s_CachedBytes, 0, CachedBytesLength);
                            if (bytesRead <= 0)
                            {
                                break;
                            }

                            s_Algorithm.HashCore(s_CachedBytes, 0, bytesRead);
                        }

                        return (int)s_Algorithm.HashFinal();
                    }
                    finally
                    {
                        s_Algorithm.Initialize();
                        Array.Clear(s_CachedBytes, 0, CachedBytesLength);
                    }
                }
            }

            /// <summary>
            /// 获取 CRC32 数值的二进制数组。
            /// </summary>
            /// <param name="crc32">CRC32 数值。</param>
            /// <returns>CRC32 数值的二进制数组。</returns>
            public static byte[] GetCrc32Bytes(int crc32)
            {
                return new byte[]
                {
                    (byte)((crc32 >> 24) & 0xff),
                    (byte)((crc32 >> 16) & 0xff),
                    (byte)((crc32 >> 8) & 0xff),
                    (byte)(crc32 & 0xff)
                };
            }

            /// <summary>
            /// 获取 CRC32 数值的二进制数组。
            /// </summary>
            /// <param name="crc32">CRC32 数值。</param>
            /// <param name="bytes">要存放结果的数组。</param>
            public static void GetCrc32Bytes(int crc32, byte[] bytes)
            {
                GetCrc32Bytes(crc32, bytes, 0);
            }

            /// <summary>
            /// 获取 CRC32 数值的二进制数组。
            /// </summary>
            /// <param name="crc32">CRC32 数值。</param>
            /// <param name="bytes">要存放结果的数组。</param>
            /// <param name="offset">CRC32 数值的二进制数组在结果数组内的起始位置。</param>
            public static void GetCrc32Bytes(int crc32, byte[] bytes, int offset)
            {
                if (bytes == null)
                {
                    throw new RFrameworkException("Result is invalid.");
                }

                if (offset < 0 || offset > bytes.Length - sizeof(int))
                {
                    throw new RFrameworkException("Offset or length is invalid.");
                }

                bytes[offset] = (byte)((crc32 >> 24) & 0xff);
                bytes[offset + 1] = (byte)((crc32 >> 16) & 0xff);
                bytes[offset + 2] = (byte)((crc32 >> 8) & 0xff);
                bytes[offset + 3] = (byte)(crc32 & 0xff);
            }

        }
    }
}
