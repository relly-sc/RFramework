using System.Collections.Generic;

namespace RFramework
{
    /// <summary>
    /// 一组活跃实体及其隐藏实例缓存的只读入口和容量参数。
    /// </summary>
    public interface IEntityGroup
    {
        /// <summary>获取实体组名称。</summary>
        string Name { get; }

        /// <summary>获取组内活跃实体数量。</summary>
        int EntityCount { get; }

        /// <summary>获取组内缓存实体实例数量。</summary>
        int CachedEntityCount { get; }

        /// <summary>过期缓存扫描间隔；0 表示不自动扫描。</summary>
        float AutoReleaseInterval { get; set; }

        /// <summary>整个组允许缓存的实例上限；0 表示不限制。</summary>
        int Capacity { get; set; }

        /// <summary>缓存实例的闲置过期秒数；0 表示不过期。</summary>
        float ExpireTime { get; set; }

        /// <summary>检查组内是否存在指定编号的活跃实体。</summary>
        /// <param name="entityId">实体编号。</param>
        /// <returns>是否存在。</returns>
        bool HasEntity(long entityId);

        /// <summary>检查组内是否存在使用指定资源的活跃实体。</summary>
        /// <param name="assetName">实体资源地址。</param>
        /// <returns>是否存在。</returns>
        bool HasEntity(string assetName);

        /// <summary>获取指定编号的活跃实体。</summary>
        /// <param name="entityId">实体编号。</param>
        /// <returns>实体；不存在时返回 null。</returns>
        IEntity GetEntity(long entityId);

        /// <summary>获取首个使用指定资源的活跃实体。</summary>
        /// <param name="assetName">实体资源地址。</param>
        /// <returns>实体；不存在时返回 null。</returns>
        IEntity GetEntity(string assetName);

        /// <summary>获取所有使用指定资源的活跃实体。</summary>
        /// <param name="assetName">实体资源地址。</param>
        /// <returns>实体数组。</returns>
        IEntity[] GetEntities(string assetName);

        /// <summary>将所有使用指定资源的活跃实体写入列表。</summary>
        /// <param name="assetName">实体资源地址。</param>
        /// <param name="results">接收结果的列表；写入前会清空。</param>
        void GetEntities(string assetName, List<IEntity> results);

        /// <summary>获取组内所有活跃实体。</summary>
        /// <returns>实体数组。</returns>
        IEntity[] GetAllEntities();

        /// <summary>将组内所有活跃实体写入列表。</summary>
        /// <param name="results">接收结果的列表；写入前会清空。</param>
        void GetAllEntities(List<IEntity> results);
    }
}
