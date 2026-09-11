using System;
using System.Text;

namespace RFramework
{
    internal readonly struct ProtectedDataHeader
    {
        internal const byte CurrentVersion = DefaultDataProtector.FormatVersion;
        internal const byte Aes256CbcHmacSha256 = 1;
        internal const int FixedLength = 16;
        internal const int IvLength = 16;
        internal const int AuthenticationTagLength = 32;

        private const int MaximumKeyIdLength = 255;
        private static readonly byte[] Magic = { (byte)'U', (byte)'R', (byte)'P', (byte)'D' };
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        internal ProtectedDataHeader(
            ProtectedDataPayloadKind payloadKind,
            string keyId,
            int keyIdLength,
            int ciphertextLength,
            int ciphertextOffset,
            int tagOffset)
        {
            PayloadKind = payloadKind;
            KeyId = keyId;
            KeyIdLength = keyIdLength;
            CiphertextLength = ciphertextLength;
            CiphertextOffset = ciphertextOffset;
            TagOffset = tagOffset;
        }

        internal ProtectedDataPayloadKind PayloadKind { get; }

        internal string KeyId { get; }

        internal int KeyIdLength { get; }

        internal int CiphertextLength { get; }

        internal int CiphertextOffset { get; }

        internal int TagOffset { get; }

        internal static byte[] GetKeyIdBytes(string keyId)
        {
            if (string.IsNullOrWhiteSpace(keyId))
            {
                throw new RFrameworkException("Protected data key ID is invalid.");
            }

            byte[] bytes = StrictUtf8.GetBytes(keyId);
            if (bytes.Length > MaximumKeyIdLength)
            {
                throw new RFrameworkException(
                    $"Protected data key ID exceeds {MaximumKeyIdLength} UTF-8 bytes.");
            }

            return bytes;
        }

        internal static int Write(
            byte[] destination,
            ProtectedDataPayloadKind payloadKind,
            byte[] keyIdBytes,
            int ciphertextLength)
        {
            ValidatePayloadKind(payloadKind);
            Buffer.BlockCopy(Magic, 0, destination, 0, Magic.Length);
            destination[4] = CurrentVersion;
            destination[5] = Aes256CbcHmacSha256;
            destination[6] = (byte)payloadKind;
            destination[7] = 0;
            WriteUInt16(destination, 8, keyIdBytes.Length);
            WriteInt32(destination, 10, ciphertextLength);
            destination[14] = IvLength;
            destination[15] = AuthenticationTagLength;
            Buffer.BlockCopy(keyIdBytes, 0, destination, FixedLength, keyIdBytes.Length);
            return FixedLength + keyIdBytes.Length;
        }

        internal static ProtectedDataHeader Read(byte[] source)
        {
            if (source == null || source.Length < FixedLength + IvLength + AuthenticationTagLength + 16)
            {
                throw new RFrameworkException("Protected data is invalid or truncated.");
            }

            for (int i = 0; i < Magic.Length; i++)
            {
                if (source[i] != Magic[i])
                {
                    throw new RFrameworkException("Protected data magic is invalid.");
                }
            }

            if (source[4] != CurrentVersion)
            {
                throw new RFrameworkException(
                    $"Protected data version '{source[4]}' is not supported.");
            }

            if (source[5] != Aes256CbcHmacSha256)
            {
                throw new RFrameworkException(
                    $"Protected data algorithm '{source[5]}' is not supported.");
            }

            ProtectedDataPayloadKind payloadKind = (ProtectedDataPayloadKind)source[6];
            ValidatePayloadKind(payloadKind);
            if (source[7] != 0 || source[14] != IvLength ||
                source[15] != AuthenticationTagLength)
            {
                throw new RFrameworkException("Protected data header is invalid.");
            }

            int keyIdLength = ReadUInt16(source, 8);
            int ciphertextLength = ReadInt32(source, 10);
            if (keyIdLength <= 0 || keyIdLength > MaximumKeyIdLength ||
                ciphertextLength <= 0 || ciphertextLength % 16 != 0)
            {
                throw new RFrameworkException("Protected data lengths are invalid.");
            }

            long ciphertextOffset = (long)FixedLength + keyIdLength + IvLength;
            long tagOffset = ciphertextOffset + ciphertextLength;
            long expectedLength = tagOffset + AuthenticationTagLength;
            if (expectedLength != source.Length || ciphertextOffset > int.MaxValue ||
                tagOffset > int.MaxValue)
            {
                throw new RFrameworkException("Protected data length does not match its header.");
            }

            string keyId;
            try
            {
                keyId = StrictUtf8.GetString(source, FixedLength, keyIdLength);
            }
            catch (DecoderFallbackException exception)
            {
                throw new RFrameworkException("Protected data key ID is not valid UTF-8.", exception);
            }

            if (string.IsNullOrWhiteSpace(keyId))
            {
                throw new RFrameworkException("Protected data key ID is invalid.");
            }

            return new ProtectedDataHeader(
                payloadKind,
                keyId,
                keyIdLength,
                ciphertextLength,
                (int)ciphertextOffset,
                (int)tagOffset);
        }

        internal static bool HasMagic(byte[] source)
        {
            if (source == null || source.Length < Magic.Length) return false;
            for (int i = 0; i < Magic.Length; i++)
            {
                if (source[i] != Magic[i]) return false;
            }
            return true;
        }

        private static void ValidatePayloadKind(ProtectedDataPayloadKind payloadKind)
        {
            if (payloadKind < ProtectedDataPayloadKind.Custom ||
                payloadKind > ProtectedDataPayloadKind.Storage)
            {
                throw new RFrameworkException(
                    $"Protected data payload kind '{payloadKind}' is not supported.");
            }
        }

        private static int ReadUInt16(byte[] source, int offset)
        {
            return source[offset] | source[offset + 1] << 8;
        }

        private static int ReadInt32(byte[] source, int offset)
        {
            return source[offset] |
                   source[offset + 1] << 8 |
                   source[offset + 2] << 16 |
                   source[offset + 3] << 24;
        }

        private static void WriteUInt16(byte[] destination, int offset, int value)
        {
            destination[offset] = (byte)value;
            destination[offset + 1] = (byte)(value >> 8);
        }

        private static void WriteInt32(byte[] destination, int offset, int value)
        {
            destination[offset] = (byte)value;
            destination[offset + 1] = (byte)(value >> 8);
            destination[offset + 2] = (byte)(value >> 16);
            destination[offset + 3] = (byte)(value >> 24);
        }
    }
}
