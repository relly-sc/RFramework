using System;
using System.Security.Cryptography;
using System.Text;

namespace RFramework
{
    /// <summary>
    /// 使用 AES-256-CBC 与 HMAC-SHA256 实现默认数据保护。
    /// </summary>
    public sealed class DefaultDataProtector : IDataProtector
    {
        /// <summary>当前受保护数据封装版本。</summary>
        public const byte FormatVersion = 1;

        private static readonly byte[] HkdfSalt =
            Encoding.ASCII.GetBytes("UnityRFramework.ProtectedData.v1");
        private static readonly byte[] EncryptionInfo = Encoding.ASCII.GetBytes("encryption");
        private static readonly byte[] AuthenticationInfo = Encoding.ASCII.GetBytes("authentication");

        private readonly IKeyProvider keyProvider;

        /// <summary>
        /// 创建默认数据保护器。
        /// </summary>
        /// <param name="keyProvider">密钥提供器。</param>
        public DefaultDataProtector(IKeyProvider keyProvider)
        {
            this.keyProvider = keyProvider ??
                throw new RFrameworkException("Key provider is invalid.");
        }

        /// <inheritdoc />
        public byte[] Protect(
            byte[] plaintext,
            ProtectedDataPayloadKind payloadKind,
            string keyId,
            byte[] associatedData = null)
        {
            if (plaintext == null)
            {
                throw new RFrameworkException("Plaintext is invalid.");
            }

            byte[] keyIdBytes = ProtectedDataHeader.GetKeyIdBytes(keyId);
            byte[] masterKey = GetKey(keyId);
            byte[] encryptionKey = null;
            byte[] authenticationKey = null;

            try
            {
                encryptionKey = DeriveKey(masterKey, EncryptionInfo);
                authenticationKey = DeriveKey(masterKey, AuthenticationInfo);

                byte[] iv;
                byte[] ciphertext;
                using (Aes aes = Aes.Create())
                {
                    aes.KeySize = 256;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = encryptionKey;
                    aes.GenerateIV();
                    iv = aes.IV;

                    using (ICryptoTransform encryptor = aes.CreateEncryptor())
                    {
                        ciphertext = encryptor.TransformFinalBlock(
                            plaintext, 0, plaintext.Length);
                    }
                }

                int payloadLength = checked(
                    ProtectedDataHeader.FixedLength + keyIdBytes.Length +
                    iv.Length + ciphertext.Length);
                byte[] result = new byte[checked(
                    payloadLength + ProtectedDataHeader.AuthenticationTagLength)];
                int ivOffset = ProtectedDataHeader.Write(
                    result,
                    payloadKind,
                    keyIdBytes,
                    ciphertext.Length);
                Buffer.BlockCopy(iv, 0, result, ivOffset, iv.Length);
                Buffer.BlockCopy(
                    ciphertext,
                    0,
                    result,
                    ivOffset + iv.Length,
                    ciphertext.Length);

                byte[] tag = ComputeTag(
                    result,
                    payloadLength,
                    authenticationKey,
                    associatedData);
                Buffer.BlockCopy(tag, 0, result, payloadLength, tag.Length);
                Array.Clear(tag, 0, tag.Length);
                return result;
            }
            catch (OverflowException exception)
            {
                throw new RFrameworkException("Protected data is too large.", exception);
            }
            catch (CryptographicException exception)
            {
                throw new RFrameworkException("Data protection failed.", exception);
            }
            finally
            {
                Clear(masterKey);
                Clear(encryptionKey);
                Clear(authenticationKey);
            }
        }

        /// <inheritdoc />
        public byte[] Unprotect(
            byte[] protectedData,
            ProtectedDataPayloadKind expectedPayloadKind,
            byte[] associatedData = null)
        {
            ProtectedDataHeader header = ProtectedDataHeader.Read(protectedData);
            byte[] masterKey = GetKey(header.KeyId);
            byte[] encryptionKey = null;
            byte[] authenticationKey = null;
            byte[] expectedTag = null;

            try
            {
                encryptionKey = DeriveKey(masterKey, EncryptionInfo);
                authenticationKey = DeriveKey(masterKey, AuthenticationInfo);
                expectedTag = ComputeTag(
                    protectedData,
                    header.TagOffset,
                    authenticationKey,
                    associatedData);
                if (!Utility.Encryption.TagsMatch(
                        expectedTag,
                        protectedData,
                        header.TagOffset))
                {
                    throw new RFrameworkException(
                        "Protected data authentication failed. The key, context or data is invalid.");
                }

                if (header.PayloadKind != expectedPayloadKind)
                {
                    throw new RFrameworkException(
                        $"Protected data payload kind '{header.PayloadKind}' does not match " +
                        $"expected kind '{expectedPayloadKind}'.");
                }

                int ivOffset = ProtectedDataHeader.FixedLength + header.KeyIdLength;
                byte[] iv = new byte[ProtectedDataHeader.IvLength];
                Buffer.BlockCopy(protectedData, ivOffset, iv, 0, iv.Length);

                using (Aes aes = Aes.Create())
                {
                    aes.KeySize = 256;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = encryptionKey;
                    aes.IV = iv;

                    using (ICryptoTransform decryptor = aes.CreateDecryptor())
                    {
                        return decryptor.TransformFinalBlock(
                            protectedData,
                            header.CiphertextOffset,
                            header.CiphertextLength);
                    }
                }
            }
            catch (RFrameworkException)
            {
                throw;
            }
            catch (CryptographicException exception)
            {
                throw new RFrameworkException("Protected data decryption failed.", exception);
            }
            finally
            {
                Clear(masterKey);
                Clear(encryptionKey);
                Clear(authenticationKey);
                Clear(expectedTag);
            }
        }

        private byte[] GetKey(string keyId)
        {
            if (!keyProvider.TryGetKey(keyId, out byte[] key) || key == null)
            {
                throw new RFrameworkException(
                    $"Protected data key '{keyId}' is unavailable.");
            }

            try
            {
                Utility.Encryption.ValidateKey(key);
                return key;
            }
            catch
            {
                Clear(key);
                throw;
            }
        }

        private static byte[] DeriveKey(byte[] masterKey, byte[] info)
        {
            byte[] pseudorandomKey;
            using (HMACSHA256 extract = new HMACSHA256(HkdfSalt))
            {
                pseudorandomKey = extract.ComputeHash(masterKey);
            }

            try
            {
                byte[] input = new byte[info.Length + 1];
                Buffer.BlockCopy(info, 0, input, 0, info.Length);
                input[input.Length - 1] = 1;
                try
                {
                    using (HMACSHA256 expand = new HMACSHA256(pseudorandomKey))
                    {
                        return expand.ComputeHash(input);
                    }
                }
                finally
                {
                    Array.Clear(input, 0, input.Length);
                }
            }
            finally
            {
                Array.Clear(pseudorandomKey, 0, pseudorandomKey.Length);
            }
        }

        private static byte[] ComputeTag(
            byte[] data,
            int dataLength,
            byte[] authenticationKey,
            byte[] associatedData)
        {
            int contextLength = associatedData?.Length ?? 0;
            byte[] input = new byte[checked(dataLength + 4 + contextLength)];
            Buffer.BlockCopy(data, 0, input, 0, dataLength);
            input[dataLength] = (byte)contextLength;
            input[dataLength + 1] = (byte)(contextLength >> 8);
            input[dataLength + 2] = (byte)(contextLength >> 16);
            input[dataLength + 3] = (byte)(contextLength >> 24);
            if (contextLength > 0)
            {
                Buffer.BlockCopy(associatedData, 0, input, dataLength + 4, contextLength);
            }

            try
            {
                using (HMACSHA256 hmac = new HMACSHA256(authenticationKey))
                {
                    return hmac.ComputeHash(input);
                }
            }
            finally
            {
                Array.Clear(input, 0, input.Length);
            }
        }

        private static void Clear(byte[] bytes)
        {
            if (bytes != null)
            {
                Array.Clear(bytes, 0, bytes.Length);
            }
        }
    }
}
