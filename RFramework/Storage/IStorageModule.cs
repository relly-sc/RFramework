using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RFramework
{
    /// <summary>提供多槽位、原子提交、备份恢复和可选数据保护的存档模块。</summary>
    public interface IStorageModule
    {
        /// <summary>设置平台文件系统辅助器。</summary>
        void SetHelper(IStorageHelper helper);
        /// <summary>设置存档序列化器。</summary>
        void SetSerializer(IStorageSerializer serializer);
        /// <summary>注入或移除数据保护器。</summary>
        void SetDataProtector(IDataProtector protector);

        /// <summary>保存指定槽位。</summary>
        Task<StorageResult> SaveAsync<T>(
            string slotName, T data, StorageOptions options = null,
            CancellationToken ct = default(CancellationToken));

        /// <summary>加载指定槽位，并按需迁移旧业务版本。</summary>
        Task<StorageLoadResult<T>> LoadAsync<T>(
            string slotName, StorageOptions options = null,
            IStorageMigration<T> migration = null,
            CancellationToken ct = default(CancellationToken));

        /// <summary>删除指定槽位及其备份。</summary>
        Task<StorageResult> DeleteAsync(
            string slotName, CancellationToken ct = default(CancellationToken));

        /// <summary>检查指定槽位是否存在。</summary>
        Task<bool> ExistsAsync(
            string slotName, CancellationToken ct = default(CancellationToken));

        /// <summary>列举现有槽位。</summary>
        Task<IReadOnlyList<StorageSlotInfo>> GetSlotsAsync(
            CancellationToken ct = default(CancellationToken));
    }
}
