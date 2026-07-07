using System.Collections.Generic;

namespace RFramework.Entity
{
    /// <summary>
    /// 实体组接口，管理同类型实体的集合及其专属对象池。
    /// </summary>
    public interface IEntityGroup
    {
        /// <summary>
        /// 获取实体组名称。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 获取实体组中当前活跃的实体数量。
        /// </summary>
        int EntityCount { get; }

        /// <summary>
        /// 获取或设置实体组对象池的自动释放间隔（秒）。
        /// </summary>
        float AutoReleaseInterval { get; set; }

        /// <summary>
        /// 获取或设置实体组对象池的容量上限。
        /// </summary>
        int Capacity { get; set; }

        /// <summary>
        /// 获取或设置实体组对象池中对象的过期时间（秒）。
        /// </summary>
        float ExpireTime { get; set; }

        /// <summary>
        /// 获取或设置实体组对象池的优先级。
        /// </summary>
        int Priority { get; set; }

        /// <summary>
        /// 判断实体组中是否包含指定编号的实体。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <returns>是否包含。</returns>
        bool HasEntity(long entityId);

        /// <summary>
        /// 判断实体组中是否包含指定资源路径的实体。
        /// </summary>
        /// <param name="assetName">实体资源路径。</param>
        /// <returns>是否包含。</returns>
        bool HasEntity(string assetName);

        /// <summary>
        /// 获取指定编号的实体。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <returns>实体实例，不存在时返回 null。</returns>
        IEntity GetEntity(long entityId);

        /// <summary>
        /// 获取指定资源路径的实体。
        /// </summary>
        /// <param name="assetName">实体资源路径。</param>
        /// <returns>实体实例，不存在时返回 null。</returns>
        IEntity GetEntity(string assetName);

        /// <summary>
        /// 获取指定资源路径的所有实体。
        /// </summary>
        /// <param name="assetName">实体资源路径。</param>
        /// <returns>实体数组。</returns>
        IEntity[] GetEntities(string assetName);

        /// <summary>
        /// 获取指定资源路径的所有实体，填充到结果列表。
        /// </summary>
        /// <param name="assetName">实体资源路径。</param>
        /// <param name="results">结果列表。</param>
        void GetEntities(string assetName, List<IEntity> results);

        /// <summary>
        /// 获取实体组中所有活跃实体。
        /// </summary>
        /// <returns>实体数组。</returns>
        IEntity[] GetAllEntities();

        /// <summary>
        /// 获取实体组中所有活跃实体，填充到结果列表。
        /// </summary>
        /// <param name="results">结果列表。</param>
        void GetAllEntities(List<IEntity> results);
    }
}
