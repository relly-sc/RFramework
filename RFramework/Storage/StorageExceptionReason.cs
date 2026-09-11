namespace RFramework
{
    /// <summary>存档操作失败原因。</summary>
    public enum StorageExceptionReason
    {
        /// <summary>没有错误。</summary>
        None = 0,
        /// <summary>参数无效。</summary>
        InvalidArgument = 1,
        /// <summary>存档不存在。</summary>
        NotFound = 2,
        /// <summary>所需密钥不可用。</summary>
        KeyUnavailable = 3,
        /// <summary>认证失败，数据、上下文或密钥不匹配。</summary>
        AuthenticationFailed = 4,
        /// <summary>存档封装格式损坏或不受支持。</summary>
        FormatInvalid = 5,
        /// <summary>序列化或反序列化失败。</summary>
        SerializationFailed = 6,
        /// <summary>业务版本迁移失败或缺少迁移器。</summary>
        MigrationFailed = 7,
        /// <summary>文件系统操作失败。</summary>
        IoFailure = 8,
        /// <summary>操作已取消。</summary>
        Cancelled = 9,
        /// <summary>未分类错误。</summary>
        Unknown = 10,
        /// <summary>读取选项中的保护方式与存档实际保护方式不一致。</summary>
        ProtectionModeMismatch = 11,
        /// <summary>读取选项中的压缩方式与存档实际压缩方式不一致。</summary>
        CompressionModeMismatch = 12
    }
}
