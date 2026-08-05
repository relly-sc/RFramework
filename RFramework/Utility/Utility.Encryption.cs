using System;
using System.Security.Cryptography;

namespace RFramework
{
    /// <summary>
    /// 框架通用工具入口。
    /// </summary>
    public static partial class Utility
    {
        /// <summary>
        /// 提供带完整性校验的对称加密功能。
        /// </summary>
        public static class Encryption
        {
            /// <summary>
            /// 加密密钥要求的字节长度。
            /// </summary>
            public const int KeyLength = 32;

            private const byte FormatVersion = 1;
            private const int IvLength = 16;
            private const int AuthenticationTagLength = 32;
            private const int HeaderLength = 1 + IvLength;
            private const int MinimumEncryptedLength = HeaderLength + 16 + AuthenticationTagLength;

            /// <summary>
            /// 创建适用于本工具的 256 位随机密钥。
            /// </summary>
            /// <returns>长度为 32 字节的随机密钥。</returns>
            public static byte[] CreateKey()
            {
                byte[] key = new byte[KeyLength];
                using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
                {
                    generator.GetBytes(key);
                }

                return key;
            }

            /// <summary>
            /// 使用 256 位密钥加密数据，并附加完整性校验信息。
            /// </summary>
            /// <param name="plaintext">待加密数据。</param>
            /// <param name="key">由 <see cref="CreateKey"/> 创建的 32 字节密钥。</param>
            /// <returns>包含格式版本、随机 IV、密文和认证标签的数据。</returns>
            public static byte[] Encrypt(byte[] plaintext, byte[] key)
            {
                if (plaintext == null)
                {
                    throw new RFrameworkException("Plaintext is invalid.");
                }

                ValidateKey(key);
                byte[] encryptionKey = DeriveKey(key, 1);
                byte[] authenticationKey = DeriveKey(key, 2);

                try
                {
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

                    byte[] payload = new byte[HeaderLength + ciphertext.Length];
                    payload[0] = FormatVersion;
                    Buffer.BlockCopy(iv, 0, payload, 1, IvLength);
                    Buffer.BlockCopy(ciphertext, 0, payload, HeaderLength, ciphertext.Length);

                    byte[] tag;
                    using (HMACSHA256 hmac = new HMACSHA256(authenticationKey))
                    {
                        tag = hmac.ComputeHash(payload);
                    }

                    byte[] result = new byte[payload.Length + tag.Length];
                    Buffer.BlockCopy(payload, 0, result, 0, payload.Length);
                    Buffer.BlockCopy(tag, 0, result, payload.Length, tag.Length);
                    return result;
                }
                catch (CryptographicException exception)
                {
                    throw new RFrameworkException("Encryption failed.", exception);
                }
                finally
                {
                    Array.Clear(encryptionKey, 0, encryptionKey.Length);
                    Array.Clear(authenticationKey, 0, authenticationKey.Length);
                }
            }

            /// <summary>
            /// 校验并解密由 <see cref="Encrypt"/> 生成的数据。
            /// </summary>
            /// <param name="encryptedData">待解密数据。</param>
            /// <param name="key">加密时使用的 32 字节密钥。</param>
            /// <returns>解密后的原始数据。</returns>
            public static byte[] Decrypt(byte[] encryptedData, byte[] key)
            {
                if (encryptedData == null || encryptedData.Length < MinimumEncryptedLength)
                {
                    throw new RFrameworkException("Encrypted data is invalid.");
                }

                if (encryptedData[0] != FormatVersion)
                {
                    throw new RFrameworkException(
                        $"Encryption format version '{encryptedData[0]}' is not supported.");
                }

                ValidateKey(key);
                byte[] encryptionKey = DeriveKey(key, 1);
                byte[] authenticationKey = DeriveKey(key, 2);

                try
                {
                    int tagOffset = encryptedData.Length - AuthenticationTagLength;
                    byte[] expectedTag;
                    using (HMACSHA256 hmac = new HMACSHA256(authenticationKey))
                    {
                        expectedTag = hmac.ComputeHash(encryptedData, 0, tagOffset);
                    }

                    if (!TagsMatch(expectedTag, encryptedData, tagOffset))
                    {
                        throw new RFrameworkException(
                            "Encrypted data authentication failed. The key is wrong or the data was modified.");
                    }

                    byte[] iv = new byte[IvLength];
                    Buffer.BlockCopy(encryptedData, 1, iv, 0, iv.Length);
                    int ciphertextLength = tagOffset - HeaderLength;

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
                                encryptedData, HeaderLength, ciphertextLength);
                        }
                    }
                }
                catch (RFrameworkException)
                {
                    throw;
                }
                catch (CryptographicException exception)
                {
                    throw new RFrameworkException("Decryption failed.", exception);
                }
                finally
                {
                    Array.Clear(encryptionKey, 0, encryptionKey.Length);
                    Array.Clear(authenticationKey, 0, authenticationKey.Length);
                }
            }

            private static void ValidateKey(byte[] key)
            {
                if (key == null || key.Length != KeyLength)
                {
                    throw new RFrameworkException(
                        $"Encryption key must contain exactly {KeyLength} bytes.");
                }
            }

            private static byte[] DeriveKey(byte[] key, byte purpose)
            {
                byte[] input = new byte[key.Length + 1];
                input[0] = purpose;
                Buffer.BlockCopy(key, 0, input, 1, key.Length);

                try
                {
                    using (SHA256 hash = SHA256.Create())
                    {
                        return hash.ComputeHash(input);
                    }
                }
                finally
                {
                    Array.Clear(input, 0, input.Length);
                }
            }

            private static bool TagsMatch(byte[] expectedTag, byte[] data, int tagOffset)
            {
                int difference = 0;
                for (int i = 0; i < expectedTag.Length; i++)
                {
                    difference |= expectedTag[i] ^ data[tagOffset + i];
                }

                return difference == 0;
            }
        }
    }
}
