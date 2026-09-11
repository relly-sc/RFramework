using System;
using System.Text;

namespace RFramework
{
    /// <summary>配置数据保护模式。</summary>
    public enum ConfigProtectionMode
    {
        /// <summary>直接解析原始数据。</summary>
        None = 0,

        /// <summary>先验证完整性并解密，再解析配置。</summary>
        EncryptedAndAuthenticated = 1
    }

    /// <summary>配置数据的结构类型。</summary>
    public enum ConfigPayloadType
    {
        /// <summary>单张配置表。</summary>
        Single = 0,

        /// <summary>多表配置容器。</summary>
        Bundle = 1
    }

    /// <summary>解密后交给 Config Helper 的负载格式。</summary>
    public enum ConfigPayloadFormat
    {
        /// <summary>项目自定义格式。</summary>
        Custom = 0,

        /// <summary>JSON 单表或多表格式。</summary>
        Json = 1,

        /// <summary>框架单表二进制格式。</summary>
        BinarySingleTable = 2,

        /// <summary>框架多表二进制容器格式。</summary>
        BinaryTableBundle = 3
    }

    /// <summary>
    /// 描述一次配置保护操作。资源位置、结构类型和负载格式共同参与认证，防止密文被错位使用。
    /// </summary>
    public readonly struct ConfigProtectionContext
    {
        public ConfigProtectionContext(
            ConfigProtectionMode mode,
            string source,
            ConfigPayloadType payloadType,
            ConfigPayloadFormat payloadFormat)
        {
            Mode = mode;
            Source = source;
            PayloadType = payloadType;
            PayloadFormat = payloadFormat;
        }

        /// <summary>保护模式。</summary>
        public ConfigProtectionMode Mode { get; }

        /// <summary>稳定的资源位置或调用方提供的数据标识。</summary>
        public string Source { get; }

        /// <summary>单表或多表容器。</summary>
        public ConfigPayloadType PayloadType { get; }

        /// <summary>解密后的负载格式。</summary>
        public ConfigPayloadFormat PayloadFormat { get; }

        /// <summary>按当前上下文保护配置数据；未启用时直接返回原数组。</summary>
        public byte[] Protect(IDataProtector protector, byte[] plaintext, string keyId)
        {
            if (Mode == ConfigProtectionMode.None)
            {
                return plaintext;
            }

            Validate(protector);
            return protector.Protect(
                plaintext,
                ProtectedDataPayloadKind.Config,
                keyId,
                CreateAssociatedData());
        }

        /// <summary>按当前上下文还原配置数据；未启用时直接返回原数组。</summary>
        public byte[] Unprotect(IDataProtector protector, byte[] protectedData)
        {
            if (Mode == ConfigProtectionMode.None)
            {
                return protectedData;
            }

            Validate(protector);
            return protector.Unprotect(
                protectedData,
                ProtectedDataPayloadKind.Config,
                CreateAssociatedData());
        }

        private void Validate(IDataProtector protector)
        {
            if (Mode != ConfigProtectionMode.EncryptedAndAuthenticated)
            {
                throw new RFrameworkException($"Unsupported config protection mode '{Mode}'.");
            }

            if (protector == null)
            {
                throw new RFrameworkException(
                    "Config data protector is not configured. Call SetDataProtector first.");
            }

            if (string.IsNullOrEmpty(Source))
            {
                throw new RFrameworkException(
                    "Protected config source cannot be null or empty.");
            }

            if (PayloadType == ConfigPayloadType.Single
                && PayloadFormat == ConfigPayloadFormat.BinaryTableBundle)
            {
                throw new RFrameworkException(
                    "The binary table bundle format cannot be used for a single table.");
            }

            if (PayloadType == ConfigPayloadType.Bundle
                && PayloadFormat == ConfigPayloadFormat.BinarySingleTable)
            {
                throw new RFrameworkException(
                    "The binary single-table format cannot be used for a table bundle.");
            }
        }

        private byte[] CreateAssociatedData()
        {
            string encodedSource = Convert.ToBase64String(Encoding.UTF8.GetBytes(Source));
            string value = $"URFCONFIG|1|{(int)PayloadType}|{(int)PayloadFormat}|{encodedSource}";
            return Encoding.UTF8.GetBytes(value);
        }
    }
}
