using System;

namespace RFramework
{
    /// <summary>受保护数值被篡改时发布的事件。</summary>
    public readonly struct MemoryTamperEvent
    {
        /// <summary>发生篡改的值类型全名。</summary>
        public string ValueTypeName { get; }

        /// <summary>本次值实例的检测次数。</summary>
        public int DetectionCount { get; }

        /// <summary>创建篡改事件。</summary>
        public MemoryTamperEvent(string valueTypeName, int detectionCount)
        {
            ValueTypeName = valueTypeName;
            DetectionCount = detectionCount;
        }
    }

    internal static class ProtectedValueUtility
    {
        private const int IntCheckSalt = unchecked((int)0x9E3779B9);
        private const long LongCheckSalt = unchecked((long)0x9E3779B97F4A7C15L);

        public static int NextMask()
        {
            int mask = Utility.Random.GetRandom();
            return mask == 0 ? 1 : mask;
        }

        public static long NextLongMask()
        {
            long high = (long)(uint)Utility.Random.GetRandom();
            long low = (uint)Utility.Random.GetRandom();
            long mask = (high << 32) | low;
            return mask == 0 ? 1L : mask;
        }

        public static int IntCheck(int value, int mask)
        {
            return unchecked(value ^ (mask * IntCheckSalt));
        }

        public static long LongCheck(long value, long mask)
        {
            return unchecked(value ^ (mask * LongCheckSalt));
        }

        public static void ReportTamper(string typeName, ref bool reported)
        {
            if (reported)
            {
                return;
            }

            reported = true;
            try
            {
                RFrameworkModuleHost.Get<IEventModule>().FireSafely(
                    new MemoryTamperEvent(typeName, 1));
            }
            catch (Exception)
            {
                // Security signals must not prevent a value read or framework shutdown.
            }
        }

        public static int FloatBits(float value)
        {
            return BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
        }

        public static float FloatFromBits(int bits)
        {
            return BitConverter.ToSingle(BitConverter.GetBytes(bits), 0);
        }
    }

    /// <summary>使用实例随机掩码和校验值保存整数。</summary>
    [Serializable]
    public struct ProtectedInt : IEquatable<ProtectedInt>
    {
        private int maskedValue;
        private int checkValue;
        private int mask;
        private bool tamperReported;

        /// <summary>创建受保护整数。</summary>
        public ProtectedInt(int value)
        {
            mask = ProtectedValueUtility.NextMask();
            maskedValue = value ^ mask;
            checkValue = ProtectedValueUtility.IntCheck(value, mask);
            tamperReported = false;
        }

        /// <summary>读取明文值；检测到篡改时返回默认值并发布事件。</summary>
        public int Value => Decode();

        /// <summary>显式转换为整数。</summary>
        public static explicit operator int(ProtectedInt value) => value.Decode();

        /// <summary>从整数创建受保护值。</summary>
        public static implicit operator ProtectedInt(int value) => new ProtectedInt(value);

        /// <summary>整数加法。</summary>
        public static ProtectedInt operator +(ProtectedInt left, ProtectedInt right) =>
            new ProtectedInt(left.Decode() + right.Decode());

        /// <summary>整数减法。</summary>
        public static ProtectedInt operator -(ProtectedInt left, ProtectedInt right) =>
            new ProtectedInt(left.Decode() - right.Decode());

        /// <inheritdoc/>
        public bool Equals(ProtectedInt other) => Decode() == other.Decode();

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is ProtectedInt other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => Decode();

        /// <summary>比较两个受保护整数。</summary>
        public static bool operator ==(ProtectedInt left, ProtectedInt right) => left.Equals(right);

        /// <summary>比较两个受保护整数是否不同。</summary>
        public static bool operator !=(ProtectedInt left, ProtectedInt right) => !left.Equals(right);

        private int Decode()
        {
            int value = maskedValue ^ mask;
            if (ProtectedValueUtility.IntCheck(value, mask) != checkValue)
            {
                ProtectedValueUtility.ReportTamper(typeof(ProtectedInt).FullName, ref tamperReported);
                return 0;
            }

            return value;
        }
    }

    /// <summary>使用实例随机掩码和校验值保存长整数。</summary>
    [Serializable]
    public struct ProtectedLong : IEquatable<ProtectedLong>
    {
        private long maskedValue;
        private long checkValue;
        private long mask;
        private bool tamperReported;

        /// <summary>创建受保护长整数。</summary>
        public ProtectedLong(long value)
        {
            mask = ProtectedValueUtility.NextLongMask();
            maskedValue = value ^ mask;
            checkValue = ProtectedValueUtility.LongCheck(value, mask);
            tamperReported = false;
        }

        /// <summary>读取明文值；检测到篡改时返回默认值并发布事件。</summary>
        public long Value => Decode();

        /// <summary>显式转换为长整数。</summary>
        public static explicit operator long(ProtectedLong value) => value.Decode();

        /// <summary>从长整数创建受保护值。</summary>
        public static implicit operator ProtectedLong(long value) => new ProtectedLong(value);

        /// <summary>长整数加法。</summary>
        public static ProtectedLong operator +(ProtectedLong left, ProtectedLong right) =>
            new ProtectedLong(left.Decode() + right.Decode());

        /// <summary>长整数减法。</summary>
        public static ProtectedLong operator -(ProtectedLong left, ProtectedLong right) =>
            new ProtectedLong(left.Decode() - right.Decode());

        /// <inheritdoc/>
        public bool Equals(ProtectedLong other) => Decode() == other.Decode();

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is ProtectedLong other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => Decode().GetHashCode();

        /// <summary>比较两个受保护长整数。</summary>
        public static bool operator ==(ProtectedLong left, ProtectedLong right) => left.Equals(right);

        /// <summary>比较两个受保护长整数是否不同。</summary>
        public static bool operator !=(ProtectedLong left, ProtectedLong right) => !left.Equals(right);

        private long Decode()
        {
            long value = maskedValue ^ mask;
            if (ProtectedValueUtility.LongCheck(value, mask) != checkValue)
            {
                ProtectedValueUtility.ReportTamper(typeof(ProtectedLong).FullName, ref tamperReported);
                return 0L;
            }

            return value;
        }
    }

    /// <summary>按 IEEE 754 位模式保存受保护浮点数。</summary>
    [Serializable]
    public struct ProtectedFloat : IEquatable<ProtectedFloat>
    {
        private int maskedValue;
        private int checkValue;
        private int mask;
        private bool tamperReported;

        /// <summary>创建受保护浮点数。</summary>
        public ProtectedFloat(float value)
        {
            int bits = ProtectedValueUtility.FloatBits(value);
            mask = ProtectedValueUtility.NextMask();
            maskedValue = bits ^ mask;
            checkValue = ProtectedValueUtility.IntCheck(bits, mask);
            tamperReported = false;
        }

        /// <summary>读取明文值；检测到篡改时返回默认值并发布事件。</summary>
        public float Value => Decode();

        /// <summary>显式转换为浮点数。</summary>
        public static explicit operator float(ProtectedFloat value) => value.Decode();

        /// <summary>从浮点数创建受保护值。</summary>
        public static implicit operator ProtectedFloat(float value) => new ProtectedFloat(value);

        /// <summary>浮点数加法。</summary>
        public static ProtectedFloat operator +(ProtectedFloat left, ProtectedFloat right) =>
            new ProtectedFloat(left.Decode() + right.Decode());

        /// <summary>浮点数减法。</summary>
        public static ProtectedFloat operator -(ProtectedFloat left, ProtectedFloat right) =>
            new ProtectedFloat(left.Decode() - right.Decode());

        /// <inheritdoc/>
        public bool Equals(ProtectedFloat other) =>
            ProtectedValueUtility.FloatBits(Decode()) == ProtectedValueUtility.FloatBits(other.Decode());

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is ProtectedFloat other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => ProtectedValueUtility.FloatBits(Decode());

        /// <summary>比较两个受保护浮点数的位模式。</summary>
        public static bool operator ==(ProtectedFloat left, ProtectedFloat right) => left.Equals(right);

        /// <summary>比较两个受保护浮点数的位模式是否不同。</summary>
        public static bool operator !=(ProtectedFloat left, ProtectedFloat right) => !left.Equals(right);

        private float Decode()
        {
            int bits = maskedValue ^ mask;
            if (ProtectedValueUtility.IntCheck(bits, mask) != checkValue)
            {
                ProtectedValueUtility.ReportTamper(typeof(ProtectedFloat).FullName, ref tamperReported);
                return 0f;
            }

            return ProtectedValueUtility.FloatFromBits(bits);
        }
    }

    /// <summary>使用实例随机掩码和校验值保存布尔值。</summary>
    [Serializable]
    public struct ProtectedBool : IEquatable<ProtectedBool>
    {
        private int maskedValue;
        private int checkValue;
        private int mask;
        private bool tamperReported;

        /// <summary>创建受保护布尔值。</summary>
        public ProtectedBool(bool value)
        {
            int bits = value ? 1 : 0;
            mask = ProtectedValueUtility.NextMask();
            maskedValue = bits ^ mask;
            checkValue = ProtectedValueUtility.IntCheck(bits, mask);
            tamperReported = false;
        }

        /// <summary>读取明文值；检测到篡改时返回默认值并发布事件。</summary>
        public bool Value => Decode();

        /// <summary>显式转换为布尔值。</summary>
        public static explicit operator bool(ProtectedBool value) => value.Decode();

        /// <summary>从布尔值创建受保护值。</summary>
        public static implicit operator ProtectedBool(bool value) => new ProtectedBool(value);

        /// <inheritdoc/>
        public bool Equals(ProtectedBool other) => Decode() == other.Decode();

        /// <inheritdoc/>
        public override bool Equals(object obj) => obj is ProtectedBool other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode() => Decode().GetHashCode();

        /// <summary>比较两个受保护布尔值。</summary>
        public static bool operator ==(ProtectedBool left, ProtectedBool right) => left.Equals(right);

        /// <summary>比较两个受保护布尔值是否不同。</summary>
        public static bool operator !=(ProtectedBool left, ProtectedBool right) => !left.Equals(right);

        private bool Decode()
        {
            int bits = maskedValue ^ mask;
            if ((bits != 0 && bits != 1)
                || ProtectedValueUtility.IntCheck(bits, mask) != checkValue)
            {
                ProtectedValueUtility.ReportTamper(typeof(ProtectedBool).FullName, ref tamperReported);
                return false;
            }

            return bits == 1;
        }
    }
}
