using System;

namespace RFramework
{
    /// <summary>
    /// 框架通用工具入口。
    /// </summary>
    public static partial class Utility
    {
        /// <summary>
        /// 提供可设置种子且支持并发调用的伪随机数功能。
        /// </summary>
        public static class Random
        {
            private static readonly object GeneratorLock = new object();
            private static System.Random generator = new System.Random(Environment.TickCount);

            /// <summary>
            /// 使用指定种子重置随机数序列。
            /// </summary>
            /// <param name="seed">随机数种子。</param>
            public static void SetSeed(int seed)
            {
                lock (GeneratorLock)
                {
                    generator = new System.Random(seed);
                }
            }

            /// <summary>
            /// 返回大于等于零且小于 <see cref="int.MaxValue"/> 的整数。
            /// </summary>
            /// <returns>生成的伪随机整数。</returns>
            public static int GetRandom()
            {
                lock (GeneratorLock)
                {
                    return generator.Next();
                }
            }

            /// <summary>
            /// 返回大于等于零且小于指定上界的整数。
            /// </summary>
            /// <param name="maxValue">不包含在结果内的上界。</param>
            /// <returns>生成的伪随机整数。</returns>
            public static int GetRandom(int maxValue)
            {
                lock (GeneratorLock)
                {
                    return generator.Next(maxValue);
                }
            }

            /// <summary>
            /// 返回大于等于下界且小于上界的整数。
            /// </summary>
            /// <param name="minValue">包含在结果内的下界。</param>
            /// <param name="maxValue">不包含在结果内的上界。</param>
            /// <returns>生成的伪随机整数。</returns>
            public static int GetRandom(int minValue, int maxValue)
            {
                lock (GeneratorLock)
                {
                    return generator.Next(minValue, maxValue);
                }
            }

            /// <summary>
            /// 返回大于等于零且小于一的双精度浮点数。
            /// </summary>
            /// <returns>生成的伪随机浮点数。</returns>
            public static double GetRandomDouble()
            {
                lock (GeneratorLock)
                {
                    return generator.NextDouble();
                }
            }

            /// <summary>
            /// 使用伪随机数据填充指定缓冲区。
            /// </summary>
            /// <param name="buffer">待填充的字节数组。</param>
            public static void GetRandomBytes(byte[] buffer)
            {
                if (buffer == null)
                {
                    throw new RFrameworkException("Random byte buffer is invalid.");
                }

                lock (GeneratorLock)
                {
                    generator.NextBytes(buffer);
                }
            }
        }
    }
}
