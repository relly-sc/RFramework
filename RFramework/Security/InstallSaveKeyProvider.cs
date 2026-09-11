using System;
using System.Security.Cryptography;

namespace RFramework
{
    /// <summary>管理每次安装生成的存档密钥，不用于发布内容解密。</summary>
    public sealed class InstallSaveKeyProvider : IKeyProvider
    {
        /// <summary>默认存档密钥标识。</summary>
        public const string DefaultKeyId = "SaveKey";

        /// <summary>AES-256 主密钥长度。</summary>
        public const int KeySize = 32;

        private readonly IKeyStore keyStore;
        private readonly string keyIdPrefix;
        private readonly object syncRoot = new object();

        /// <summary>创建安装级存档密钥提供器。</summary>
        public InstallSaveKeyProvider(
            IKeyStore keyStore,
            string keyIdPrefix = DefaultKeyId)
        {
            this.keyStore = keyStore ?? throw new ArgumentNullException(nameof(keyStore));
            this.keyIdPrefix = string.IsNullOrWhiteSpace(keyIdPrefix)
                ? throw new ArgumentException("Save key ID prefix is invalid.", nameof(keyIdPrefix))
                : keyIdPrefix.Trim();
        }

        /// <summary>确保当前存档密钥存在；新建时返回 true。</summary>
        public bool EnsureKey(string keyId = DefaultKeyId)
        {
            ValidateManagedKeyId(keyId);
            lock (syncRoot)
            {
                if (TryGetKeyCore(keyId, out byte[] existing))
                {
                    Array.Clear(existing, 0, existing.Length);
                    return false;
                }

                byte[] generated = new byte[KeySize];
                try
                {
                    using (RandomNumberGenerator random = RandomNumberGenerator.Create())
                    {
                        random.GetBytes(generated);
                    }

                    keyStore.Write(keyId, generated);
                    return true;
                }
                finally
                {
                    Array.Clear(generated, 0, generated.Length);
                }
            }
        }

        /// <summary>创建新的轮换密钥，旧密钥继续保留。</summary>
        public bool RotateKey(string newKeyId)
        {
            return EnsureKey(newKeyId);
        }

        /// <summary>删除已完成迁移的旧密钥。</summary>
        public bool DeleteKey(string keyId)
        {
            ValidateManagedKeyId(keyId);
            lock (syncRoot)
            {
                return keyStore.Delete(keyId);
            }
        }

        /// <inheritdoc />
        public bool TryGetKey(string keyId, out byte[] key)
        {
            if (!IsManagedKeyId(keyId))
            {
                key = null;
                return false;
            }

            lock (syncRoot)
            {
                return TryGetKeyCore(keyId, out key);
            }
        }

        private bool TryGetKeyCore(string keyId, out byte[] key)
        {
            if (!keyStore.TryRead(keyId, out key) || key == null)
            {
                key = null;
                return false;
            }

            if (key.Length == KeySize)
            {
                return true;
            }

            Array.Clear(key, 0, key.Length);
            key = null;
            throw new RFrameworkException(
                $"Stored save key '{keyId}' has an invalid length.");
        }

        private void ValidateManagedKeyId(string keyId)
        {
            if (!IsManagedKeyId(keyId))
            {
                throw new RFrameworkException(
                    $"Save key ID must be '{keyIdPrefix}' or use its version namespace.");
            }
        }

        private bool IsManagedKeyId(string keyId)
        {
            return string.Equals(keyId, keyIdPrefix, StringComparison.Ordinal)
                || (!string.IsNullOrEmpty(keyId)
                    && keyId.StartsWith(keyIdPrefix + ".", StringComparison.Ordinal));
        }
    }
}
