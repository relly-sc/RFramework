using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RFramework
{
    /// <summary>
    /// 实体生命周期服务。动态实体由模块持有实例与资源，外部实体仅登记生命周期。
    /// </summary>
    public interface IEntityModule
    {
        /// <summary>获取已显示或已登记的活跃实体数量。</summary>
        int EntityCount { get; }

        /// <summary>获取正在异步加载的实体请求数量。</summary>
        int LoadingEntityCount { get; }

        /// <summary>获取实体组数量。</summary>
        int EntityGroupCount { get; }

        /// <summary>设置引擎实体适配器。</summary>
        /// <param name="helper">实体 Helper。</param>
        void SetHelper(IEntityHelper helper);

        /// <summary>设置实体加载与生命周期事件依赖。</summary>
        /// <param name="resourceModule">资源模块。</param>
        /// <param name="eventModule">事件模块。</param>
        void SetDependencies(IResourceModule resourceModule, IEventModule eventModule);

        /// <summary>创建实体组。容量是整个组的缓存上限。</summary>
        /// <param name="name">实体组名称。</param>
        /// <param name="autoReleaseInterval">缓存自动释放检查间隔。</param>
        /// <param name="capacity">整个组的缓存实例上限；零表示不限制。</param>
        /// <param name="expireTime">缓存实例过期时间；零表示不过期。</param>
        /// <returns>创建的实体组。</returns>
        IEntityGroup CreateEntityGroup(string name, float autoReleaseInterval, int capacity,
            float expireTime);

        /// <summary>销毁指定实体组及其缓存。</summary>
        /// <param name="name">实体组名称。</param>
        /// <returns>是否找到并销毁了实体组。</returns>
        bool DestroyEntityGroup(string name);

        /// <summary>检查指定实体组是否存在。</summary>
        /// <param name="name">实体组名称。</param>
        /// <returns>是否存在。</returns>
        bool HasEntityGroup(string name);

        /// <summary>获取指定实体组。</summary>
        /// <param name="name">实体组名称。</param>
        /// <returns>实体组；不存在时返回 null。</returns>
        IEntityGroup GetEntityGroup(string name);

        /// <summary>获取所有实体组。</summary>
        /// <returns>实体组数组。</returns>
        IEntityGroup[] GetAllEntityGroups();

        /// <summary>将所有实体组写入列表。</summary>
        /// <param name="results">接收结果的列表；写入前会清空。</param>
        void GetAllEntityGroups(List<IEntityGroup> results);

        /// <summary>加载或复用实例，完成初始化并显示实体。</summary>
        /// <param name="entityId">实体编号。</param>
        /// <param name="assetName">实体资源地址。</param>
        /// <param name="groupName">目标实体组名称。</param>
        /// <param name="priority">资源加载优先级。</param>
        /// <param name="userData">业务自定义数据。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>显示完成的实体。</returns>
        Task<IEntity> ShowEntityAsync(long entityId, string assetName, string groupName,
            uint priority = 0, object userData = null, CancellationToken ct = default);

        /// <summary>登记由场景或业务持有的实体实例。</summary>
        /// <param name="entityId">实体编号。</param>
        /// <param name="assetName">实体名称或资源标识。</param>
        /// <param name="groupName">目标实体组名称。</param>
        /// <param name="entity">待登记实体。</param>
        /// <param name="userData">业务自定义数据。</param>
        /// <returns>完成初始化和显示的实体。</returns>
        IEntity RegisterEntity(long entityId, string assetName, string groupName, IEntity entity,
            object userData = null);

        /// <summary>注销外部实体；模块不会销毁其 Handle。</summary>
        /// <param name="entityId">实体编号。</param>
        /// <param name="userData">业务自定义数据。</param>
        void UnregisterEntity(long entityId, object userData = null);

        /// <summary>隐藏活跃实体；若仍在加载则取消该请求。</summary>
        /// <param name="entityId">实体编号。</param>
        /// <param name="userData">业务自定义数据。</param>
        void HideEntity(long entityId, object userData = null);

        /// <summary>隐藏所有活跃实体。</summary>
        /// <param name="userData">业务自定义数据。</param>
        void HideAllLoadedEntities(object userData = null);

        /// <summary>取消所有正在加载的实体请求。</summary>
        void HideAllLoadingEntities();

        /// <summary>建立父子关系；形成环时抛出异常。</summary>
        /// <param name="childEntityId">子实体编号。</param>
        /// <param name="parentEntityId">父实体编号。</param>
        /// <param name="userData">业务自定义数据。</param>
        void AttachEntity(long childEntityId, long parentEntityId, object userData = null);

        /// <summary>解除指定子实体与父实体的关系。</summary>
        /// <param name="childEntityId">子实体编号。</param>
        /// <param name="userData">业务自定义数据。</param>
        void DetachEntity(long childEntityId, object userData = null);

        /// <summary>解除指定父实体的全部子实体关系。</summary>
        /// <param name="parentEntityId">父实体编号。</param>
        /// <param name="userData">业务自定义数据。</param>
        void DetachChildEntities(long parentEntityId, object userData = null);

        /// <summary>检查指定编号的活跃实体是否存在。</summary>
        /// <param name="entityId">实体编号。</param>
        /// <returns>是否存在。</returns>
        bool HasEntity(long entityId);

        /// <summary>获取指定编号的活跃实体。</summary>
        /// <param name="entityId">实体编号。</param>
        /// <returns>实体；不存在时返回 null。</returns>
        IEntity GetEntity(long entityId);

        /// <summary>获取所有活跃实体。</summary>
        /// <returns>实体数组。</returns>
        IEntity[] GetAllLoadedEntities();

        /// <summary>将所有活跃实体写入列表。</summary>
        /// <param name="results">接收结果的列表；写入前会清空。</param>
        void GetAllLoadedEntities(List<IEntity> results);

        /// <summary>获取所有正在加载的实体编号。</summary>
        /// <returns>实体编号数组。</returns>
        long[] GetAllLoadingEntityIds();

        /// <summary>检查指定实体是否正在加载。</summary>
        /// <param name="entityId">实体编号。</param>
        /// <returns>是否正在加载。</returns>
        bool IsLoadingEntity(long entityId);

        /// <summary>检查实体是否仍由模块作为当前活跃实例管理。</summary>
        /// <param name="entity">待检查实体。</param>
        /// <returns>是否有效。</returns>
        bool IsValidEntity(IEntity entity);

        /// <summary>获取指定子实体的父实体。</summary>
        /// <param name="childEntityId">子实体编号。</param>
        /// <returns>父实体；不存在时返回 null。</returns>
        IEntity GetParentEntity(long childEntityId);

        /// <summary>获取指定父实体的直接子实体数量。</summary>
        /// <param name="parentEntityId">父实体编号。</param>
        /// <returns>直接子实体数量。</returns>
        int GetChildEntityCount(long parentEntityId);

        /// <summary>获取指定父实体的直接子实体只读快照。</summary>
        /// <param name="parentEntityId">父实体编号。</param>
        /// <returns>子实体只读集合。</returns>
        IReadOnlyList<IEntity> GetChildEntities(long parentEntityId);
    }
}
