using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RFramework.Event;
using RFramework.Pool;
using RFramework.Resource;

namespace RFramework.Entity
{
    /// <summary>
    /// 实体模块核心实现。
    /// 管理游戏实体的完整生命周期，包括创建、显示、隐藏、回收、父子附加。
    /// 资源加载委托 IResourceModule，对象池复用 IPoolModule。
    /// </summary>
    internal sealed class EntityModule : RFrameworkModule, IEntityModule
    {
        /// <summary>
        /// 实体辅助器实例。
        /// </summary>
        private IEntityHelper entityHelper;

        /// <summary>
        /// 所有已加载实体的索引（entityId → EntityInfo）。
        /// </summary>
        private readonly Dictionary<long, EntityInfo> entityInfos = new Dictionary<long, EntityInfo>();

        /// <summary>
        /// 所有实体组（groupName → EntityGroup）。
        /// </summary>
        private readonly Dictionary<string, EntityGroup> entityGroups = new Dictionary<string, EntityGroup>();

        /// <summary>
        /// 正在异步加载中的实体（entityId → EntityLoadingInfo）。
        /// </summary>
        private readonly Dictionary<long, EntityLoadingInfo> entitiesBeingLoaded = new Dictionary<long, EntityLoadingInfo>();

        /// <summary>
        /// 加载中但已被请求取消的实体编号集合。
        /// </summary>
        private readonly HashSet<long> entitiesToReleaseOnLoad = new HashSet<long>();

        /// <summary>
        /// 等待下一帧回收的实体队列（延迟回收机制）。
        /// </summary>
        private readonly Queue<EntityInfo> recycleQueue = new Queue<EntityInfo>();

        /// <summary>
        /// 活跃实体到其 EntityInstanceObject 的映射（用于 Hide 时归还对象池）。
        /// </summary>
        private readonly Dictionary<long, EntityInstanceObject> activeInstanceObjects = new Dictionary<long, EntityInstanceObject>();

        /// <summary>
        /// 框架关闭标记，用于 OnHide 的 isShutdown 参数。
        /// </summary>
        private bool isShutdown;

        /// <summary>
        /// 资源模块引用，用于异步加载实体 Prefab 资源。
        /// </summary>
        private IResourceModule resourceModule;

        /// <summary>
        /// 事件模块引用，用于分发实体生命周期事件。
        /// </summary>
        private IEventModule eventModule;

        /// <summary>
        /// 对象池模块引用，用于实体组创建专属对象池。
        /// </summary>
        private IPoolModule poolModule;

        /// <summary>
        /// 获取当前已加载的实体数量。
        /// </summary>
        public int EntityCount => entityInfos.Count;

        /// <summary>
        /// 获取当前实体组数量。
        /// </summary>
        public int EntityGroupCount => entityGroups.Count;

        /// <summary>
        /// 获取框架模块优先级。
        /// EntityModule 依赖 ResourceModule（Priority=20）加载资源，
        /// 因此 Priority=25 确保 Resource 先 Update、Entity 后 Update。
        /// </summary>
        internal override int Priority
        {
            get
            {
                return 25;
            }
        }

        /// <summary>
        /// 设置实体辅助器（必须在首次 ShowEntity 之前调用）。
        /// </summary>
        /// <param name="helper">实体辅助器实例。</param>
        public void SetHelper(IEntityHelper helper)
        {
            entityHelper = helper;
        }

        /// <summary>
        /// 设置依赖模块引用（由 EntityComponent 在 Awake 中注入）。
        /// </summary>
        /// <param name="resourceModule">资源模块。</param>
        /// <param name="eventModule">事件模块。</param>
        /// <param name="poolModule">对象池模块。</param>
        public void SetDependencies(IResourceModule resourceModule, IEventModule eventModule, IPoolModule poolModule)
        {
            this.resourceModule = resourceModule;
            this.eventModule = eventModule;
            this.poolModule = poolModule;
        }

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
        public IEntityGroup CreateEntityGroup(string name, float autoReleaseInterval, int capacity,
            float expireTime, int priority, IEntityGroupHelper groupHelper = null)
        {
            if (entityGroups.ContainsKey(name))
            {
                throw new RFrameworkException($"Already exists entity group '{name}'.");
            }

            EntityGroup group = new EntityGroup(name, autoReleaseInterval, capacity, expireTime,
                priority, groupHelper);
            entityGroups.Add(name, group);
            return group;
        }

        /// <summary>
        /// 销毁指定名称的实体组，释放其中所有实体。
        /// </summary>
        /// <param name="name">实体组名称。</param>
        /// <returns>是否成功销毁。</returns>
        public bool DestroyEntityGroup(string name)
        {
            if (!entityGroups.TryGetValue(name, out EntityGroup group))
            {
                return false;
            }

            // 先隐藏组内所有实体
            IEntity[] allEntities = group.GetAllEntities();
            for (int i = 0; i < allEntities.Length; i++)
            {
                HideEntity(allEntities[i].Id);
            }

            entityGroups.Remove(name);
            group.Destroy();
            return true;
        }

        /// <summary>
        /// 判断是否包含指定名称的实体组。
        /// </summary>
        /// <param name="name">实体组名称。</param>
        /// <returns>是否包含。</returns>
        public bool HasEntityGroup(string name)
        {
            return entityGroups.ContainsKey(name);
        }

        /// <summary>
        /// 获取指定名称的实体组。
        /// </summary>
        /// <param name="name">实体组名称。</param>
        /// <returns>实体组实例，不存在时返回 null。</returns>
        public IEntityGroup GetEntityGroup(string name)
        {
            if (entityGroups.TryGetValue(name, out EntityGroup group))
            {
                return group;
            }

            return null;
        }

        /// <summary>
        /// 获取所有实体组。
        /// </summary>
        /// <returns>实体组数组。</returns>
        public IEntityGroup[] GetAllEntityGroups()
        {
            List<IEntityGroup> results = new List<IEntityGroup>();
            GetAllEntityGroups(results);
            return results.ToArray();
        }

        /// <summary>
        /// 获取所有实体组，填充到结果列表。
        /// </summary>
        /// <param name="results">结果列表。</param>
        public void GetAllEntityGroups(List<IEntityGroup> results)
        {
            if (results == null)
            {
                throw new RFrameworkException("Results is invalid.");
            }

            results.Clear();
            foreach (KeyValuePair<string, EntityGroup> kv in entityGroups)
            {
                results.Add(kv.Value);
            }
        }

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
        public async Task<IEntity> ShowEntityAsync(long entityId, string assetName, string groupName,
            uint priority = 0, object userData = null, CancellationToken ct = default)
        {
            if (entityHelper == null)
            {
                throw new RFrameworkException("Entity helper is not set.");
            }

            if (resourceModule == null)
            {
                throw new RFrameworkException("Resource module is not set.");
            }

            if (HasEntity(entityId))
            {
                throw new RFrameworkException($"Entity id '{entityId}' is already exist.");
            }

            if (IsLoadingEntity(entityId))
            {
                throw new RFrameworkException($"Entity id '{entityId}' is already loading.");
            }

            EntityGroup group = GetEntityGroupInternal(groupName);

            // 尝试从对象池获取
            EntityInstanceObject instanceObject = group.SpawnEntityInstanceObject(assetName);
            if (instanceObject != null)
            {
                try
                {
                    // 对象池命中，直接显示
                    IEntity entity = InternalShowEntity(entityId, assetName, group, instanceObject.Target,
                        false, 0f, userData);
                    activeInstanceObjects.Add(entityId, instanceObject);
                    return entity;
                }
                catch
                {
                    CleanupFailedEntity(entityId, group);
                    instanceObject.Release();
                    throw;
                }
            }

            // 对象池未命中，需要异步加载资源
            EntityLoadingInfo loadingInfo = new EntityLoadingInfo(entityId, assetName, group, userData);
            entitiesBeingLoaded.Add(entityId, loadingInfo);

            object entityAsset = null;
            instanceObject = null;
            try
            {
                // 通过 IResourceModule 加载 Prefab 资源
                entityAsset = await resourceModule.LoadAssetAsync<object>(assetName, priority, ct);

                // 加载完成后检查取消 / 模块关闭 / 手动隐藏标记，
                // 避免迟到的加载在已关闭（或正在关闭）的模块中复活实体
                if (ct.IsCancellationRequested || isShutdown || entitiesToReleaseOnLoad.Contains(entityId))
                {
                    entitiesToReleaseOnLoad.Remove(entityId);
                    entitiesBeingLoaded.Remove(entityId);
                    resourceModule.UnloadAsset<object>(assetName);
                    entityAsset = null;
                    throw new OperationCanceledException(
                        $"Entity '{entityId}' loading was cancelled or module is shutting down.");
                }

                entitiesBeingLoaded.Remove(entityId);

                // 实例化并注册到对象池
                object entityInstance = entityHelper.InstantiateEntity(entityAsset);
                instanceObject = new EntityInstanceObject(assetName, entityAsset, entityInstance, entityHelper,
                    resourceModule);
                group.RegisterEntityInstanceObject(instanceObject, true);

                // 内部显示流程
                IEntity entity = InternalShowEntity(entityId, assetName, group, entityInstance,
                    true, (float)loadingInfo.ElapsedSeconds, userData);
                activeInstanceObjects.Add(entityId, instanceObject);
                instanceObject = null; // Ownership transfers to activeInstanceObjects.
                return entity;
            }
            catch (Exception ex)
            {
                entitiesBeingLoaded.Remove(entityId);
                CleanupFailedEntity(entityId, group);
                if (instanceObject != null)
                {
                    instanceObject.Release();
                }
                else if (entityAsset != null)
                {
                    resourceModule.UnloadAsset<object>(assetName);
                }

                // 分发失败事件
                if (eventModule != null)
                {
                    eventModule.Fire(new ShowEntityFailureEvent(entityId, assetName,
                        ex.Message, userData));
                }

                throw;
            }
        }

        /// <summary>
        /// 内部显示实体流程：创建 IEntity → OnInit → OnShow → 分发成功事件。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <param name="assetName">实体资源路径。</param>
        /// <param name="group">实体组。</param>
        /// <param name="entityInstance">实体实例对象。</param>
        /// <param name="isNewInstance">是否为新创建的实例。</param>
        /// <param name="duration">加载持续时间。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>显示完成的实体。</returns>
        private IEntity InternalShowEntity(long entityId, string assetName, EntityGroup group,
            object entityInstance, bool isNewInstance, float duration, object userData)
        {
            IEntity entity = entityHelper.CreateEntity(entityInstance, group, userData);

            EntityInfo entityInfo = new EntityInfo(entity);
            entityInfos.Add(entityId, entityInfo);

            // 生命周期：WillInit → OnInit → Inited
            entityInfo.Status = EntityStatus.WillInit;
            entity.OnInit(entityId, assetName, group, isNewInstance, userData);
            entityInfo.Status = EntityStatus.Inited;

            // 加入实体组
            group.AddEntity(entity);

            // 生命周期：WillShow → OnShow → Showed
            entityInfo.Status = EntityStatus.WillShow;
            entity.OnShow(userData);
            entityInfo.Status = EntityStatus.Showed;

            // 分发成功事件
            if (eventModule != null)
            {
                eventModule.Fire(new ShowEntitySuccessEvent(entity, duration, userData));
            }

            return entity;
        }

        /// <summary>
        /// 隐藏指定编号的实体。实体将进入对象池等待复用或被销毁。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void HideEntity(long entityId, object userData = null)
        {
            // 正在加载中：标记取消
            if (IsLoadingEntity(entityId))
            {
                entitiesToReleaseOnLoad.Add(entityId);
                return;
            }

            EntityInfo entityInfo = GetEntityInfo(entityId);
            if (entityInfo == null)
            {
                throw new RFrameworkException($"Entity id '{entityId}' is not exist.");
            }

            InternalHideEntity(entityInfo, userData);
        }

        /// <summary>
        /// 内部隐藏实体流程：递归隐藏子实体 → 解除父挂接 → OnHide → 加入回收队列。
        /// </summary>
        /// <param name="entityInfo">实体信息。</param>
        /// <param name="userData">用户自定义数据。</param>
        private void InternalHideEntity(EntityInfo entityInfo, object userData)
        {
            // 递归隐藏所有子实体
            while (entityInfo.ChildEntityCount > 0)
            {
                IEntity childEntity = entityInfo.GetChildEntity();
                if (childEntity != null)
                {
                    HideEntity(childEntity.Id, userData);
                }
            }

            // 防止重复隐藏
            if (entityInfo.Status == EntityStatus.Hidden)
            {
                return;
            }

            IEntity entity = entityInfo.Entity;

            // 解除与父实体的挂接
            if (entityInfo.Parent != null)
            {
                DetachEntity(entity.Id, userData);
            }

            // 生命周期：WillHide → OnHide → Hidden
            entityInfo.Status = EntityStatus.WillHide;
            entity.OnHide(isShutdown, userData);
            entityInfo.Status = EntityStatus.Hidden;

            // 从实体组移除
            EntityGroup group = GetEntityGroupInternal(entity.Group.Name);
            group.RemoveEntity(entity);

            // 从全局字典移除
            entityInfos.Remove(entity.Id);

            // 分发隐藏完成事件
            if (eventModule != null)
            {
                eventModule.Fire(new HideEntityCompleteEvent(entity.Id, entity.AssetName,
                    entity.Group.Name, userData));
            }

            // 加入回收队列（延迟到下一帧）
            recycleQueue.Enqueue(entityInfo);
        }

        /// <summary>
        /// 隐藏所有已加载的实体。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        public void HideAllLoadedEntities(object userData = null)
        {
            long[] entityIds = new long[entityInfos.Count];
            int index = 0;
            foreach (KeyValuePair<long, EntityInfo> kv in entityInfos)
            {
                entityIds[index++] = kv.Key;
            }

            for (int i = 0; i < entityIds.Length; i++)
            {
                HideEntity(entityIds[i], userData);
            }
        }

        /// <summary>
        /// 隐藏所有正在加载中的实体（取消异步加载）。
        /// </summary>
        public void HideAllLoadingEntities()
        {
            foreach (KeyValuePair<long, EntityLoadingInfo> kv in entitiesBeingLoaded)
            {
                entitiesToReleaseOnLoad.Add(kv.Key);
            }

            entitiesBeingLoaded.Clear();
        }

        /// <summary>
        /// 将子实体附加到父实体。子实体的 Handle 将跟随父实体层级。
        /// </summary>
        /// <param name="childEntityId">子实体编号。</param>
        /// <param name="parentEntityId">父实体编号。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void AttachEntity(long childEntityId, long parentEntityId, object userData = null)
        {
            if (childEntityId == parentEntityId)
            {
                throw new RFrameworkException("Cannot attach entity to itself.");
            }

            EntityInfo childInfo = GetEntityInfo(childEntityId);
            if (childInfo == null)
            {
                throw new RFrameworkException($"Child entity id '{childEntityId}' is not exist.");
            }

            EntityInfo parentInfo = GetEntityInfo(parentEntityId);
            if (parentInfo == null)
            {
                throw new RFrameworkException($"Parent entity id '{parentEntityId}' is not exist.");
            }

            // 先解除旧的父子关系
            if (childInfo.Parent != null)
            {
                DetachEntity(childEntityId, userData);
            }

            // 建立新的父子关系
            IEntity childEntity = childInfo.Entity;
            IEntity parentEntity = parentInfo.Entity;
            childInfo.Parent = parentEntity;
            parentInfo.AddChildEntity(childEntity);

            // 双向回调
            parentEntity.OnAttached(childEntity, userData);
            childEntity.OnAttachTo(parentEntity, userData);
        }

        /// <summary>
        /// 将子实体从父实体解除。子实体 Handle 将脱离父实体层级。
        /// </summary>
        /// <param name="childEntityId">子实体编号。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void DetachEntity(long childEntityId, object userData = null)
        {
            EntityInfo childInfo = GetEntityInfo(childEntityId);
            if (childInfo == null)
            {
                return;
            }

            IEntity parentEntity = childInfo.Parent;
            if (parentEntity == null)
            {
                return;
            }

            EntityInfo parentInfo = GetEntityInfo(parentEntity.Id);
            IEntity childEntity = childInfo.Entity;

            // 解除关系
            childInfo.Parent = null;
            if (parentInfo != null)
            {
                parentInfo.RemoveChildEntity(childEntity);
            }

            // 双向回调
            parentEntity.OnDetached(childEntity, userData);
            childEntity.OnDetachFrom(parentEntity, userData);
        }

        /// <summary>
        /// 解除父实体下的所有子实体。
        /// </summary>
        /// <param name="parentEntityId">父实体编号。</param>
        /// <param name="userData">用户自定义数据。</param>
        public void DetachChildEntities(long parentEntityId, object userData = null)
        {
            EntityInfo parentInfo = GetEntityInfo(parentEntityId);
            if (parentInfo == null)
            {
                return;
            }

            while (parentInfo.ChildEntityCount > 0)
            {
                IEntity childEntity = parentInfo.GetChildEntity();
                if (childEntity != null)
                {
                    DetachEntity(childEntity.Id, userData);
                }
            }
        }

        /// <summary>
        /// 判断是否包含指定编号的实体。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <returns>是否包含。</returns>
        public bool HasEntity(long entityId)
        {
            return entityInfos.ContainsKey(entityId);
        }

        /// <summary>
        /// 获取指定编号的实体。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <returns>实体实例，不存在时返回 null。</returns>
        public IEntity GetEntity(long entityId)
        {
            EntityInfo entityInfo = GetEntityInfo(entityId);
            return entityInfo != null ? entityInfo.Entity : null;
        }

        /// <summary>
        /// 获取所有已加载的实体。
        /// </summary>
        /// <returns>实体数组。</returns>
        public IEntity[] GetAllLoadedEntities()
        {
            List<IEntity> results = new List<IEntity>();
            GetAllLoadedEntities(results);
            return results.ToArray();
        }

        /// <summary>
        /// 获取所有已加载的实体，填充到结果列表。
        /// </summary>
        /// <param name="results">结果列表。</param>
        public void GetAllLoadedEntities(List<IEntity> results)
        {
            if (results == null)
            {
                throw new RFrameworkException("Results is invalid.");
            }

            results.Clear();
            foreach (KeyValuePair<long, EntityInfo> kv in entityInfos)
            {
                results.Add(kv.Value.Entity);
            }
        }

        /// <summary>
        /// 获取所有正在加载中的实体编号。
        /// </summary>
        /// <returns>实体编号数组。</returns>
        public long[] GetAllLoadingEntityIds()
        {
            long[] ids = new long[entitiesBeingLoaded.Count];
            int index = 0;
            foreach (KeyValuePair<long, EntityLoadingInfo> kv in entitiesBeingLoaded)
            {
                ids[index++] = kv.Key;
            }

            return ids;
        }

        /// <summary>
        /// 判断指定编号的实体是否正在加载中。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <returns>是否正在加载。</returns>
        public bool IsLoadingEntity(long entityId)
        {
            return entitiesBeingLoaded.ContainsKey(entityId);
        }

        /// <summary>
        /// 判断实体是否有效（未被回收）。
        /// </summary>
        /// <param name="entity">实体实例。</param>
        /// <returns>是否有效。</returns>
        public bool IsValidEntity(IEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            return HasEntity(entity.Id);
        }

        /// <summary>
        /// 获取子实体的父实体。
        /// </summary>
        /// <param name="childEntityId">子实体编号。</param>
        /// <returns>父实体实例，不存在时返回 null。</returns>
        public IEntity GetParentEntity(long childEntityId)
        {
            EntityInfo childInfo = GetEntityInfo(childEntityId);
            return childInfo != null ? childInfo.Parent : null;
        }

        /// <summary>
        /// 获取父实体的子实体数量。
        /// </summary>
        /// <param name="parentEntityId">父实体编号。</param>
        /// <returns>子实体数量。</returns>
        public int GetChildEntityCount(long parentEntityId)
        {
            EntityInfo parentInfo = GetEntityInfo(parentEntityId);
            return parentInfo != null ? parentInfo.ChildEntityCount : 0;
        }

        /// <summary>
        /// 获取父实体的子实体列表。
        /// </summary>
        /// <param name="parentEntityId">父实体编号。</param>
        /// <returns>子实体列表。</returns>
        public IReadOnlyList<IEntity> GetChildEntities(long parentEntityId)
        {
            EntityInfo parentInfo = GetEntityInfo(parentEntityId);
            return parentInfo != null ? parentInfo.Children : (IReadOnlyList<IEntity>)Array.Empty<IEntity>();
        }

        /// <summary>
        /// 模块轮询更新。处理延迟回收队列并驱动所有实体组的 Update。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间。</param>
        /// <param name="realElapseSeconds">实际流逝时间。</param>
        internal override void Update(float elapseSeconds, float realElapseSeconds)
        {
            // 处理延迟回收队列
            while (recycleQueue.Count > 0)
            {
                EntityInfo entityInfo = recycleQueue.Dequeue();
                IEntity entity = entityInfo.Entity;
                long entityId = entity.Id;

                // 生命周期：WillRecycle → OnRecycle → Recycled
                entityInfo.Status = EntityStatus.WillRecycle;
                entity.OnRecycle();
                entityInfo.Status = EntityStatus.Recycled;

                // 将 instanceObject 归还对象池（委托给 EntityGroup 处理容量检查）
                if (activeInstanceObjects.TryGetValue(entityId, out EntityInstanceObject instanceObject))
                {
                    activeInstanceObjects.Remove(entityId);
                    EntityGroup group = GetEntityGroupInternal(entity.Group.Name);
                    group.UnspawnEntityInstance(instanceObject);
                }
            }

            // 驱动所有实体组的 Update（含定时过期释放）
            foreach (KeyValuePair<string, EntityGroup> kv in entityGroups)
            {
                kv.Value.Update(elapseSeconds, realElapseSeconds);
            }
        }

        /// <summary>
        /// 模块关闭。隐藏所有实体并标记为 shutdown 状态。
        /// </summary>
        internal override void Shutdown()
        {
            isShutdown = true;
            HideAllLoadedEntities();
            HideAllLoadingEntities();

            // 立即处理回收队列（不等下一帧）
            while (recycleQueue.Count > 0)
            {
                EntityInfo entityInfo = recycleQueue.Dequeue();
                entityInfo.Status = EntityStatus.WillRecycle;
                entityInfo.Entity.OnRecycle();
                entityInfo.Status = EntityStatus.Recycled;
                if (activeInstanceObjects.TryGetValue(entityInfo.Entity.Id, out EntityInstanceObject instanceObject))
                {
                    activeInstanceObjects.Remove(entityInfo.Entity.Id);
                    instanceObject.Release();
                }
            }

            // 销毁所有实体组
            foreach (KeyValuePair<string, EntityGroup> kv in entityGroups)
            {
                kv.Value.Destroy();
            }

            entityGroups.Clear();
            entityInfos.Clear();
            entitiesBeingLoaded.Clear();
            entitiesToReleaseOnLoad.Clear();
            recycleQueue.Clear();
            activeInstanceObjects.Clear();
            // 注意：isShutdown 保持为 true，使关闭后迟到的加载能检测到模块已关闭而取消，
            // 避免在已销毁的模块中复活实体。
        }

        /// <summary>
        /// 获取内部 EntityInfo 实例。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <returns>EntityInfo 实例，不存在时返回 null。</returns>
        private EntityInfo GetEntityInfo(long entityId)
        {
            if (entityInfos.TryGetValue(entityId, out EntityInfo info))
            {
                return info;
            }

            return null;
        }

        /// <summary>
        /// 获取内部 EntityGroup 实例。
        /// </summary>
        /// <param name="groupName">实体组名称。</param>
        /// <returns>EntityGroup 实例。</returns>
        private EntityGroup GetEntityGroupInternal(string groupName)
        {
            if (entityGroups.TryGetValue(groupName, out EntityGroup group))
            {
                return group;
            }

            throw new RFrameworkException($"Entity group '{groupName}' is not exist.");
        }

        private void CleanupFailedEntity(long entityId, EntityGroup group)
        {
            if (!entityInfos.TryGetValue(entityId, out EntityInfo entityInfo))
            {
                return;
            }

            entityInfos.Remove(entityId);
            try
            {
                group.RemoveEntity(entityInfo.Entity);
            }
            catch
            {
                // Preserve the original creation exception.
            }
        }
    }
}
