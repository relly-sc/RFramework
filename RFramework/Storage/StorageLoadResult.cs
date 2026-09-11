namespace RFramework
{
    /// <summary>存档加载结果。</summary>
    /// <typeparam name="T">存档数据类型。</typeparam>
    public sealed class StorageLoadResult<T>
    {
        internal StorageLoadResult(
            bool succeeded,
            string slotName,
            T data,
            int version,
            bool recoveredFromBackup,
            StorageExceptionReason reason,
            string message)
        {
            Succeeded = succeeded;
            SlotName = slotName;
            Data = data;
            Version = version;
            RecoveredFromBackup = recoveredFromBackup;
            Reason = reason;
            Message = message ?? string.Empty;
        }

        /// <summary>操作是否成功。</summary>
        public bool Succeeded { get; }
        /// <summary>槽位名称。</summary>
        public string SlotName { get; }
        /// <summary>加载并迁移后的数据；失败时为默认值。</summary>
        public T Data { get; }
        /// <summary>返回数据对应的业务版本。</summary>
        public int Version { get; }
        /// <summary>数据是否来自备份。</summary>
        public bool RecoveredFromBackup { get; }
        /// <summary>失败原因。</summary>
        public StorageExceptionReason Reason { get; }
        /// <summary>不包含密钥材料的结果说明。</summary>
        public string Message { get; }

        internal static StorageLoadResult<T> Success(
            string slotName, T data, int version, bool recoveredFromBackup)
        {
            return new StorageLoadResult<T>(
                true, slotName, data, version, recoveredFromBackup,
                StorageExceptionReason.None, string.Empty);
        }

        internal static StorageLoadResult<T> Failure(
            string slotName, StorageExceptionReason reason, string message)
        {
            return new StorageLoadResult<T>(
                false, slotName, default(T), 0, false, reason, message);
        }
    }
}
