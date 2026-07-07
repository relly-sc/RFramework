using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RFramework.Event;
using RFramework.Pool;
using RFramework.Resource;

namespace RFramework.Entity
{
    /// <summary>
    /// 实体模块接口，管理游戏实体的完整生命周期。
    /// 提供实体创建/显示/隐藏/回收、实体组管理、父子附加等能力。
    /// 异步 API 统一使用 Task，Library 层零第三方依赖。
    /// </summary>
    public interface IEntityModule
    {
        /// <summary>
        /// 设置实体辅助器（必须在首次 ShowEntity 之前调用）。
        /// </summary>
        /// <param name="helper">实体辅助器实例。</param>
        void SetHelper(IEntityHelper helper);

        /// <summary>
        /// 设置依赖模块引用（由 EntityComponent 在 Awake 中注入）。
        /// 必须在首次 ShowEntity 之前调用。
        /// </summary>
        /// <param name="resourceModule">资源模块，用于异步加载实体 Prefab。</param>
        /// <param name="eventModule">事件模块，用于分发实体生命周期事件。</param>
        /// <param name="poolModule">对象池模块，用于实体组创建专属对象池。</param>
        void SetDependencies(IResourceModule resourceModule, IEventModule eventModule, IPoolModule poolModule);

        /// <summary>
        /// 获取当前已加载的实体数量。
        /// </summary>
        int EntityCount { get; }

        /// <summary>
        /// 获取当前实体组数量。
        /// </summary>
        int EntityGroupCount { get; }

        /// <summary>
        /// 创建实体组。每个实体组拥有独立的对象池配置。
        /// </summary>
        /// <param name="name">实体组名称，全局唯一。</param>
        /// <param name="autoReleaseInterval">对象池自动释放间隔（秒）。</param>
        /// <param name="capacity">对象池容量上限。</param>
        /// <param name="expireTime">对象池中对象过期时间（秒）。</param>
        /// <param name="priority">对象池优先级。</param>
        /// <param name="groupHelper">实体组辅助器，可选。</param>
        /// <returns>创建的实体组实例。</returns>
        IEntityGroup CreateEntityGroup(string name, float autoReleaseInterval, int capacity,
            float expireTime, int priority, IEntityGroupHelper groupHelper = null);

        /// <summary>
        /// 销毁指定名称的实体组，释放其中所有实体。
        /// </summary>
        /// <param name="name">实体组名称。</param>
        /// <returns>是否成功销毁。</returns>
        bool DestroyEntityGroup(string name);

        /// <summary>
        /// 判断是否包含指定名称的实体组。
        /// </summary>
        /// <param name="name">实体组名称。</param>
        /// <returns>是否包含。</returns>
        bool HasEntityGroup(string name);

        /// <summary>
        /// 获取指定名称的实体组。
        /// </summary>
        /// <param name="name">实体组名称。</param>
        /// <returns>实体组实例，不存在时返回 null。</returns>
        IEntityGroup GetEntityGroup(string name);

        /// <summary>
        /// 获取所有实体组。
        /// </summary>
        /// <returns>实体组数组。</returns>
        IEntityGroup[] GetAllEntityGroups();

        /// <summary>
        /// 获取所有实体组，填充到结果列表。
        /// </summary>
        /// <param name="results">结果列表。</param>
        void GetAllEntityGroups(List<IEntityGroup> results);

        /// <summary>
        /// 异步显示实体。加载资源、实例化对象、调用 OnShow。
        /// 同一 entityId 不可重复显示（需先 Hide）。
        /// </summary>
        /// <param name="entityId">实体编号，由调用方分配。</param>
        /// <param name="assetName">实体资源路径。</param>
        /// <param name="groupName">目标实体组名称。</param>
        /// <param name="priority">加载优先级。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>显示完成的实体实例。</returns>
        Task<IEntity> ShowEntityAsync(long entityId, string assetName, string groupName,
            uint priority = 0, object userData = null, CancellationToken ct = default);

        /// <summary>
        /// 隐藏指定编号的实体。实体将进入对象池等待复用或被销毁。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <param name="userData">用户自定义数据。</param>
        void HideEntity(long entityId, object userData = null);

        /// <summary>
        /// 隐藏所有已加载的实体。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        void HideAllLoadedEntities(object userData = null);

        /// <summary>
        /// 隐藏所有正在加载中的实体（取消异步加载）。
        /// </summary>
        void HideAllLoadingEntities();

        /// <summary>
        /// 将子实体附加到父实体。子实体的 Handle 将跟随父实体层级。
        /// </summary>
        /// <param name="childEntityId">子实体编号。</param>
        /// <param name="parentEntityId">父实体编号。</param>
        /// <param name="userData">用户自定义数据。</param>
        void AttachEntity(long childEntityId, long parentEntityId, object userData = null);

        /// <summary>
        /// 将子实体从父实体解除。子实体 Handle 将脱离父实体层级。
        /// </summary>
        /// <param name="childEntityId">子实体编号。</param>
        /// <param name="userData">用户自定义数据。</param>
        void DetachEntity(long childEntityId, object userData = null);

        /// <summary>
        /// 解除父实体下的所有子实体。
        /// </summary>
        /// <param name="parentEntityId">父实体编号。</param>
        /// <param name="userData">用户自定义数据。</param>
        void DetachChildEntities(long parentEntityId, object userData = null);

        /// <summary>
        /// 判断是否包含指定编号的实体。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <returns>是否包含。</returns>
        bool HasEntity(long entityId);

        /// <summary>
        /// 获取指定编号的实体。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <returns>实体实例，不存在时返回 null。</returns>
        IEntity GetEntity(long entityId);

        /// <summary>
        /// 获取所有已加载的实体。
        /// </summary>
        /// <returns>实体数组。</returns>
        IEntity[] GetAllLoadedEntities();

        /// <summary>
        /// 获取所有已加载的实体，填充到结果列表。
        /// </summary>
        /// <param name="results">结果列表。</param>
        void GetAllLoadedEntities(List<IEntity> results);

        /// <summary>
        /// 获取所有正在加载中的实体编号。
        /// </summary>
        /// <returns>实体编号数组。</returns>
        long[] GetAllLoadingEntityIds();

        /// <summary>
        /// 判断指定编号的实体是否正在加载中。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <returns>是否正在加载。</returns>
        bool IsLoadingEntity(long entityId);

        /// <summary>
        /// 判断实体是否有效（未被回收）。
        /// </summary>
        /// <param name="entity">实体实例。</param>
        /// <returns>是否有效。</returns>
        bool IsValidEntity(IEntity entity);

        /// <summary>
        /// 获取子实体的父实体。
        /// </summary>
        /// <param name="childEntityId">子实体编号。</param>
        /// <returns>父实体实例，不存在时返回 null。</returns>
        IEntity GetParentEntity(long childEntityId);

        /// <summary>
        /// 获取父实体的子实体数量。
        /// </summary>
        /// <param name="parentEntityId">父实体编号。</param>
        /// <returns>子实体数量。</returns>
        int GetChildEntityCount(long parentEntityId);

        /// <summary>
        /// 获取父实体的子实体列表。
        /// </summary>
        /// <param name="parentEntityId">父实体编号。</param>
        /// <returns>子实体列表。</returns>
        IReadOnlyList<IEntity> GetChildEntities(long parentEntityId);

    }
}
