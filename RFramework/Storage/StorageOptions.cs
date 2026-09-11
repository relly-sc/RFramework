namespace RFramework
{
    /// <summary>存档数据保护模式。</summary>
    public enum StorageProtectionMode
    {
        /// <summary>不加密。</summary>
        None = 0,

        /// <summary>加密并认证完整性。</summary>
        EncryptedAndAuthenticated = 1
    }

    /// <summary>存档压缩模式。</summary>
    public enum StorageCompressionMode
    {
        /// <summary>不压缩。</summary>
        None = 0,

        /// <summary>使用 GZip 压缩。</summary>
        GZip = 1
    }

    /// <summary>控制一次存档保存或加载的行为。</summary>
    public sealed class StorageOptions
    {
        /// <summary>当前业务存档版本。</summary>
        public int Version { get; set; } = 1;

        /// <summary>数据保护模式，默认不加密。</summary>
        public StorageProtectionMode ProtectionMode { get; set; } = StorageProtectionMode.None;

        /// <summary>加密时使用的密钥标识。</summary>
        public string KeyId { get; set; } = "SaveKey";

        /// <summary>压缩模式，默认不压缩。</summary>
        public StorageCompressionMode CompressionMode { get; set; } = StorageCompressionMode.None;

        /// <summary>覆盖正式存档时是否保留上一份备份。</summary>
        public bool CreateBackup { get; set; } = true;

        /// <summary>主存档加载失败时是否尝试读取备份。</summary>
        public bool RecoverFromBackup { get; set; } = true;
    }
}
