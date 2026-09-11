namespace RFramework
{
    /// <summary>存档写入或删除结果。</summary>
    public sealed class StorageResult
    {
        internal StorageResult(
            bool succeeded,
            string slotName,
            int version,
            long size,
            StorageExceptionReason reason,
            string message)
        {
            Succeeded = succeeded;
            SlotName = slotName;
            Version = version;
            Size = size;
            Reason = reason;
            Message = message ?? string.Empty;
        }

        /// <summary>操作是否成功。</summary>
        public bool Succeeded { get; }
        /// <summary>槽位名称。</summary>
        public string SlotName { get; }
        /// <summary>业务存档版本。</summary>
        public int Version { get; }
        /// <summary>最终文件字节数。</summary>
        public long Size { get; }
        /// <summary>失败原因。</summary>
        public StorageExceptionReason Reason { get; }
        /// <summary>不包含密钥材料的结果说明。</summary>
        public string Message { get; }

        internal static StorageResult Success(string slotName, int version, long size)
        {
            return new StorageResult(true, slotName, version, size, StorageExceptionReason.None, string.Empty);
        }

        internal static StorageResult Failure(string slotName, StorageExceptionReason reason, string message)
        {
            return new StorageResult(false, slotName, 0, 0L, reason, message);
        }
    }
}
