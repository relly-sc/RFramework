using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RFramework
{
    /// <summary>隔离平台文件系统的存档读写契约。</summary>
    public interface IStorageHelper
    {
        /// <summary>原子写入正式文件，并按需保留上一份备份。</summary>
        Task WriteAtomicAsync(string slotName, byte[] data, bool createBackup, CancellationToken ct);

        /// <summary>读取正式文件或备份文件。</summary>
        Task<byte[]> ReadAsync(string slotName, bool backup, CancellationToken ct);

        /// <summary>删除正式文件和备份。</summary>
        Task DeleteAsync(string slotName, CancellationToken ct);

        /// <summary>检查正式文件是否存在。</summary>
        Task<bool> ExistsAsync(string slotName, CancellationToken ct);

        /// <summary>列举现有正式存档。</summary>
        Task<IReadOnlyList<StorageSlotInfo>> GetSlotsAsync(CancellationToken ct);
    }
}
