using System;

namespace RFramework
{
    /// <summary>一个持久化存档槽位的文件元数据。</summary>
    public sealed class StorageSlotInfo
    {
        /// <summary>创建槽位信息。</summary>
        public StorageSlotInfo(string slotName, long size, DateTime lastWriteTimeUtc, bool hasBackup)
        {
            SlotName = slotName;
            Size = size;
            LastWriteTimeUtc = lastWriteTimeUtc;
            HasBackup = hasBackup;
        }

        /// <summary>槽位名称。</summary>
        public string SlotName { get; }
        /// <summary>正式文件字节数。</summary>
        public long Size { get; }
        /// <summary>最后写入时间（UTC）。</summary>
        public DateTime LastWriteTimeUtc { get; }
        /// <summary>是否存在备份文件。</summary>
        public bool HasBackup { get; }
    }
}
