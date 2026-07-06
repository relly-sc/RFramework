
namespace RFramework
{
    public static partial class Utility
    {
        public static partial class Verifier
        {
            /// <summary>
            /// CRC32 算法。
            /// </summary>
            private sealed class Crc32
            {
                /// <summary>
                /// CRC32 查表长度。
                /// </summary>
                private const int TableLength = 256;
                /// <summary>
                /// 默认 CRC32 生成多项式。
                /// </summary>
                private const uint DefaultPolynomial = 0xedb88320;
                /// <summary>
                /// 默认 CRC32 初始种子值。
                /// </summary>
                private const uint DefaultSeed = 0xffffffff;

                /// <summary>
                /// 当前实例使用的种子值。
                /// </summary>
                private readonly uint m_Seed;
                /// <summary>
                /// CRC32 查找表。
                /// </summary>
                private readonly uint[] m_Table;
                /// <summary>
                /// 当前计算的 CRC32 哈希值。
                /// </summary>
                private uint m_Hash;

                public Crc32()
                    : this(DefaultPolynomial, DefaultSeed)
                {
                }

                public Crc32(uint polynomial, uint seed)
                {
                    m_Seed = seed;
                    m_Table = InitializeTable(polynomial);
                    m_Hash = seed;
                }

                public void Initialize()
                {
                    m_Hash = m_Seed;
                }

                public void HashCore(byte[] bytes, int offset, int length)
                {
                    m_Hash = CalculateHash(m_Table, m_Hash, bytes, offset, length);
                }

                public uint HashFinal()
                {
                    return ~m_Hash;
                }

                /// <summary>
                /// 使用查找表计算 CRC32 哈希值。
                /// </summary>
                /// <param name="table">CRC32 查找表。</param>
                /// <param name="value">初始哈希值。</param>
                /// <param name="bytes">输入字节数组。</param>
                /// <param name="offset">字节偏移。</param>
                /// <param name="length">计算长度。</param>
                /// <returns>计算后的哈希值。</returns>
                private static uint CalculateHash(uint[] table, uint value, byte[] bytes, int offset, int length)
                {
                    int last = offset + length;
                    for (int i = offset; i < last; i++)
                    {
                        unchecked
                        {
                            value = (value >> 8) ^ table[bytes[i] ^ value & 0xff];
                        }
                    }

                    return value;
                }

                /// <summary>
                /// 根据多项式初始化 CRC32 查找表。
                /// </summary>
                /// <param name="polynomial">CRC32 生成多项式。</param>
                /// <returns>初始化后的查找表。</returns>
                private static uint[] InitializeTable(uint polynomial)
                {
                    uint[] table = new uint[TableLength];
                    for (int i = 0; i < TableLength; i++)
                    {
                        uint entry = (uint)i;
                        for (int j = 0; j < 8; j++)
                        {
                            if ((entry & 1) == 1)
                            {
                                entry = (entry >> 1) ^ polynomial;
                            }
                            else
                            {
                                entry >>= 1;
                            }
                        }

                        table[i] = entry;
                    }

                    return table;
                }
            }
        }
    }
}
