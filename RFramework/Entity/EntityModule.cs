using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RFramework
{
    /// <summary>
    /// 实体模块。统一管理加载请求、活跃实体、父子关系和隐藏后的实例缓存。
    /// </summary>
    internal sealed class EntityModule : RFrameworkModule, IEntityModule
    {
        private readonly Dictionary<long, EntityInfo> entities = new Dictionary<long, EntityInfo>();
        private readonly Dictionary<string, EntityGroup> groups =
            new Dictionary<string, EntityGroup>(StringComparer.Ordinal);
        private readonly Dictionary<long, EntityLoadingInfo> loading =
            new Dictionary<long, EntityLoadingInfo>();
        private readonly Dictionary<EntityInfo, EntityInstanceObject> ownedInstances =
            new Dictionary<EntityInfo, EntityInstanceObject>();
        private readonly Queue<EntityInfo> recycleQueue = new Queue<EntityInfo>();
        private readonly CancellationTokenSource shutdownCancellation = new CancellationTokenSource();

        private IEntityHelper helper;
        private IResourceModule resourceModule;
        private IEventModule eventModule;
        private bool isShutdown;

        /// <inheritdoc/>
        internal override int Order => 25;

        /// <inheritdoc/>
        public int EntityCount => entities.Count;

        /// <inheritdoc/>
        public int LoadingEntityCount => loading.Count;

        /// <inheritdoc/>
        public int EntityGroupCount => groups.Count;

        /// <inheritdoc/>
        public void SetHelper(IEntityHelper helper)
        {
            this.helper = helper ?? throw new RFrameworkException("Entity helper is invalid.");
        }

        /// <inheritdoc/>
        public void SetDependencies(IResourceModule resourceModule, IEventModule eventModule)
        {
            this.resourceModule = resourceModule;
            this.eventModule = eventModule;
        }

        /// <inheritdoc/>
        public IEntityGroup CreateEntityGroup(string name, float autoReleaseInterval, int capacity,
            float expireTime)
        {
            EnsureRunning();
            EntityGroup group = new EntityGroup(name, autoReleaseInterval, capacity, expireTime);
            if (groups.ContainsKey(group.Name))
            {
                throw new RFrameworkException($"Entity group '{group.Name}' already exists.");
            }

            groups.Add(group.Name, group);
            return group;
        }

        /// <inheritdoc/>
        public bool DestroyEntityGroup(string name)
        {
            EnsureRunning();
            if (!groups.TryGetValue(name, out EntityGroup group))
            {
                return false;
            }

            foreach (EntityInfo info in entities.Values)
            {
                if (ReferenceEquals(info.Group, group) && info.IsTransitioning)
                {
                    throw new RFrameworkException(
                        $"Entity group '{name}' is busy with a lifecycle callback.");
                }
            }

            List<EntityLoadingInfo> groupLoads = null;
            foreach (EntityLoadingInfo request in loading.Values)
            {
                if (ReferenceEquals(request.Group, group))
                {
                    (groupLoads ??= new List<EntityLoadingInfo>()).Add(request);
                }
            }

            if (groupLoads != null)
            {
                for (int i = 0; i < groupLoads.Count; i++)
                {
                    groupLoads[i].Cancel();
                }
            }

            List<Exception> failures = null;
            IEntity[] snapshot = group.GetAllEntities();
            for (int i = 0; i < snapshot.Length; i++)
            {
                TryHide(snapshot[i].Id, null, ref failures);
            }

            ProcessRecycleQueue(false, ref failures);
            groups.Remove(name);
            TryRun(group.Destroy, ref failures);
            ThrowFailures($"Destroy entity group '{name}'", failures);
            return true;
        }

        /// <inheritdoc/>
        public bool HasEntityGroup(string name)
        {
            return !string.IsNullOrEmpty(name) && groups.ContainsKey(name);
        }

        /// <inheritdoc/>
        public IEntityGroup GetEntityGroup(string name)
        {
            return !string.IsNullOrEmpty(name) && groups.TryGetValue(name, out EntityGroup group)
                ? group
                : null;
        }

        /// <inheritdoc/>
        public IEntityGroup[] GetAllEntityGroups()
        {
            IEntityGroup[] results = new IEntityGroup[groups.Count];
            int index = 0;
            foreach (EntityGroup group in groups.Values)
            {
                results[index++] = group;
            }

            return results;
        }

        /// <inheritdoc/>
        public void GetAllEntityGroups(List<IEntityGroup> results)
        {
            if (results == null)
            {
                throw new RFrameworkException("Results list is invalid.");
            }

            results.Clear();
            foreach (EntityGroup group in groups.Values)
            {
                results.Add(group);
            }
        }

        /// <inheritdoc/>
        public async Task<IEntity> ShowEntityAsync(long entityId, string assetName, string groupName,
            uint priority = 0, object userData = null, CancellationToken ct = default)
        {
            EnsureCanCreate(entityId, assetName);
            if (helper == null)
            {
                throw new RFrameworkException("Entity helper is not set.");
            }

            if (resourceModule == null)
            {
                throw new RFrameworkException("Resource module is not set.");
            }

            EntityGroup group = GetRequiredGroup(groupName);
            EntityInstanceObject cached = group.TakeCached(assetName);
            if (cached != null)
            {
                try
                {
                    return ActivateOwnedEntity(entityId, assetName, group, cached, false, 0f, userData);
                }
                catch (Exception ex)
                {
                    TryReleaseAfterFailure(cached, ref ex);
                    PublishFailure(entityId, assetName, ex, userData);
                    throw new RFrameworkException($"Failed to show entity '{entityId}'.", ex);
                }
            }

            EntityLoadingInfo request = new EntityLoadingInfo(entityId, assetName, group, userData,
                ct, shutdownCancellation.Token);
            loading.Add(entityId, request);
            object asset = null;
            EntityInstanceObject instance = null;
            try
            {
                asset = await resourceModule.LoadAssetAsync<object>(assetName, priority, request.Token);
                request.Token.ThrowIfCancellationRequested();
                EnsureRunning();

                object target = helper.InstantiateEntity(asset);
                if (target == null)
                {
                    throw new RFrameworkException($"Entity helper failed to instantiate '{assetName}'.");
                }

                instance = new EntityInstanceObject(assetName, asset, target, helper, resourceModule);
                asset = null;
                IEntity entity = ActivateOwnedEntity(entityId, assetName, group, instance, true,
                    request.ElapsedSeconds, userData);
                instance = null;
                return entity;
            }
            catch (Exception ex)
            {
                if (instance != null)
                {
                    TryReleaseAfterFailure(instance, ref ex);
                }
                else if (asset != null)
                {
                    TryUnloadAfterFailure(assetName, ref ex);
                }

                PublishFailure(entityId, assetName, ex, userData);
                throw new RFrameworkException($"Failed to show entity '{entityId}'.", ex);
            }
            finally
            {
                if (loading.TryGetValue(entityId, out EntityLoadingInfo current)
                    && ReferenceEquals(current, request))
                {
                    loading.Remove(entityId);
                }

                request.Dispose();
            }
        }

        /// <inheritdoc/>
        public IEntity RegisterEntity(long entityId, string assetName, string groupName, IEntity entity,
            object userData = null)
        {
            EnsureCanCreate(entityId, assetName);
            if (entity == null)
            {
                throw new RFrameworkException("Entity is invalid.");
            }

            foreach (EntityInfo info in entities.Values)
            {
                if (ReferenceEquals(info.Entity, entity))
                {
                    throw new RFrameworkException("Entity instance is already registered.");
                }
            }

            EntityGroup group = GetRequiredGroup(groupName);
            try
            {
                return ActivateEntity(entityId, assetName, group, entity, true, null, true, 0f, userData);
            }
            catch (Exception ex)
            {
                PublishFailure(entityId, assetName, ex, userData);
                throw;
            }
        }

        /// <inheritdoc/>
        public void UnregisterEntity(long entityId, object userData = null)
        {
            if (entities.TryGetValue(entityId, out EntityInfo info) && info.IsExternal)
            {
                HideEntity(entityId, userData);
            }
        }

        /// <inheritdoc/>
        public void HideEntity(long entityId, object userData = null)
        {
            if (loading.TryGetValue(entityId, out EntityLoadingInfo request))
            {
                request.Cancel();
                return;
            }

            if (!entities.TryGetValue(entityId, out EntityInfo info))
            {
                throw new RFrameworkException($"Entity '{entityId}' does not exist.");
            }

            List<Exception> failures = null;
            Hide(info, userData, ref failures);
            ThrowFailures($"Hide entity '{entityId}'", failures);
        }

        /// <inheritdoc/>
        public void HideAllLoadedEntities(object userData = null)
        {
            long[] ids = new long[entities.Count];
            entities.Keys.CopyTo(ids, 0);
            List<Exception> failures = null;
            for (int i = 0; i < ids.Length; i++)
            {
                TryHide(ids[i], userData, ref failures);
            }

            ThrowFailures("Hide all entities", failures);
        }

        /// <inheritdoc/>
        public void HideAllLoadingEntities()
        {
            EntityLoadingInfo[] requests = new EntityLoadingInfo[loading.Count];
            loading.Values.CopyTo(requests, 0);
            for (int i = 0; i < requests.Length; i++)
            {
                requests[i].Cancel();
            }
        }

        /// <inheritdoc/>
        public void AttachEntity(long childEntityId, long parentEntityId, object userData = null)
        {
            if (childEntityId == parentEntityId)
            {
                throw new RFrameworkException("An entity cannot be attached to itself.");
            }

            EntityInfo child = GetRequiredEntity(childEntityId);
            EntityInfo parent = GetRequiredEntity(parentEntityId);
            EnsureStable(child);
            EnsureStable(parent);

            for (EntityInfo cursor = parent; cursor != null; cursor = cursor.Parent)
            {
                if (ReferenceEquals(cursor, child))
                {
                    throw new RFrameworkException("Entity attachment would create a parent cycle.");
                }
            }

            if (ReferenceEquals(child.Parent, parent))
            {
                return;
            }

            List<Exception> failures = null;
            if (child.Parent != null)
            {
                Detach(child, userData, ref failures);
            }

            child.Parent = parent;
            parent.AddChild(child);
            child.IsTransitioning = true;
            parent.IsTransitioning = true;
            try
            {
                TryRun(() => child.Entity.OnAttachTo(parent.Entity, userData), ref failures);
                TryRun(() => parent.Entity.OnAttached(child.Entity, userData), ref failures);
            }
            finally
            {
                child.IsTransitioning = false;
                parent.IsTransitioning = false;
            }

            ThrowFailures($"Attach entity '{childEntityId}' to '{parentEntityId}'", failures);
        }

        /// <inheritdoc/>
        public void DetachEntity(long childEntityId, object userData = null)
        {
            if (!entities.TryGetValue(childEntityId, out EntityInfo child) || child.Parent == null)
            {
                return;
            }

            List<Exception> failures = null;
            Detach(child, userData, ref failures);
            ThrowFailures($"Detach entity '{childEntityId}'", failures);
        }

        /// <inheritdoc/>
        public void DetachChildEntities(long parentEntityId, object userData = null)
        {
            if (!entities.TryGetValue(parentEntityId, out EntityInfo parent))
            {
                return;
            }

            EntityInfo[] children = parent.GetChildrenSnapshot();
            List<Exception> failures = null;
            for (int i = 0; i < children.Length; i++)
            {
                Detach(children[i], userData, ref failures);
            }

            ThrowFailures($"Detach children of entity '{parentEntityId}'", failures);
        }

        /// <inheritdoc/>
        public bool HasEntity(long entityId)
        {
            return entities.ContainsKey(entityId);
        }

        /// <inheritdoc/>
        public IEntity GetEntity(long entityId)
        {
            return entities.TryGetValue(entityId, out EntityInfo info) ? info.Entity : null;
        }

        /// <inheritdoc/>
        public IEntity[] GetAllLoadedEntities()
        {
            IEntity[] results = new IEntity[entities.Count];
            int index = 0;
            foreach (EntityInfo info in entities.Values)
            {
                results[index++] = info.Entity;
            }

            return results;
        }

        /// <inheritdoc/>
        public void GetAllLoadedEntities(List<IEntity> results)
        {
            if (results == null)
            {
                throw new RFrameworkException("Results list is invalid.");
            }

            results.Clear();
            foreach (EntityInfo info in entities.Values)
            {
                results.Add(info.Entity);
            }
        }

        /// <inheritdoc/>
        public long[] GetAllLoadingEntityIds()
        {
            long[] results = new long[loading.Count];
            loading.Keys.CopyTo(results, 0);
            return results;
        }

        /// <inheritdoc/>
        public bool IsLoadingEntity(long entityId)
        {
            return loading.ContainsKey(entityId);
        }

        /// <inheritdoc/>
        public bool IsValidEntity(IEntity entity)
        {
            return entity != null && entities.TryGetValue(entity.Id, out EntityInfo info)
                && ReferenceEquals(info.Entity, entity);
        }

        /// <inheritdoc/>
        public IEntity GetParentEntity(long childEntityId)
        {
            return entities.TryGetValue(childEntityId, out EntityInfo child)
                ? child.Parent?.Entity
                : null;
        }

        /// <inheritdoc/>
        public int GetChildEntityCount(long parentEntityId)
        {
            return entities.TryGetValue(parentEntityId, out EntityInfo parent)
                ? parent.Children.Count
                : 0;
        }

        /// <inheritdoc/>
        public IReadOnlyList<IEntity> GetChildEntities(long parentEntityId)
        {
            if (!entities.TryGetValue(parentEntityId, out EntityInfo parent))
            {
                return Array.Empty<IEntity>();
            }

            IEntity[] results = new IEntity[parent.Children.Count];
            for (int i = 0; i < results.Length; i++)
            {
                results[i] = parent.Children[i].Entity;
            }

            return results;
        }

        /// <inheritdoc/>
        internal override void Tick(float elapseSeconds, float realElapseSeconds)
        {
            List<Exception> failures = null;
            ProcessRecycleQueue(false, ref failures);
            EntityGroup[] snapshot = new EntityGroup[groups.Count];
            groups.Values.CopyTo(snapshot, 0);
            for (int i = 0; i < snapshot.Length; i++)
            {
                TryRun(() => snapshot[i].Update(elapseSeconds, realElapseSeconds), ref failures);
            }

            ThrowFailures("Entity module update", failures);
        }

        /// <inheritdoc/>
        internal override void Stop()
        {
            if (isShutdown)
            {
                return;
            }

            isShutdown = true;
            shutdownCancellation.Cancel();
            HideAllLoadingEntities();
            List<Exception> failures = null;

            long[] ids = new long[entities.Count];
            entities.Keys.CopyTo(ids, 0);
            for (int i = 0; i < ids.Length; i++)
            {
                TryHide(ids[i], null, ref failures);
            }

            ProcessRecycleQueue(true, ref failures);

            EntityInfo[] residualEntities = new EntityInfo[entities.Count];
            entities.Values.CopyTo(residualEntities, 0);
            for (int i = 0; i < residualEntities.Length; i++)
            {
                EntityInfo info = residualEntities[i];
                TryRun(info.Entity.OnRecycle, ref failures);
                if (ownedInstances.TryGetValue(info, out EntityInstanceObject instance))
                {
                    ownedInstances.Remove(info);
                    TryRun(instance.Release, ref failures);
                }
            }

            EntityInstanceObject[] orphanedInstances = new EntityInstanceObject[ownedInstances.Count];
            ownedInstances.Values.CopyTo(orphanedInstances, 0);
            for (int i = 0; i < orphanedInstances.Length; i++)
            {
                TryRun(orphanedInstances[i].Release, ref failures);
            }

            foreach (EntityGroup group in groups.Values)
            {
                TryRun(group.Destroy, ref failures);
            }

            groups.Clear();
            entities.Clear();
            ownedInstances.Clear();
            recycleQueue.Clear();
            ThrowFailures("Entity module shutdown", failures);
        }

        private IEntity ActivateOwnedEntity(long entityId, string assetName, EntityGroup group,
            EntityInstanceObject instance, bool isNewInstance, float duration, object userData)
        {
            IEntity entity = helper.CreateEntity(instance.Target, group, userData);
            if (entity == null)
            {
                throw new RFrameworkException($"Entity helper failed to wrap '{assetName}'.");
            }

            return ActivateEntity(entityId, assetName, group, entity, false, instance,
                isNewInstance, duration, userData);
        }

        private IEntity ActivateEntity(long entityId, string assetName, EntityGroup group, IEntity entity,
            bool isExternal, EntityInstanceObject instance, bool isNewInstance, float duration,
            object userData)
        {
            EntityInfo info = new EntityInfo(entity, group, isExternal) { IsTransitioning = true };
            bool addedToGroup = false;
            bool initialized = false;
            entities.Add(entityId, info);
            if (instance != null)
            {
                ownedInstances.Add(info, instance);
            }

            try
            {
                initialized = true;
                entity.OnInit(entityId, assetName, group, isNewInstance, userData);
                if (entity.Id != entityId || !ReferenceEquals(entity.Group, group))
                {
                    throw new RFrameworkException("Entity did not retain the identity assigned during OnInit.");
                }

                group.AddEntity(entity);
                addedToGroup = true;
                entity.OnShow(userData);
                info.IsTransitioning = false;
                eventModule?.FireSafely(new ShowEntitySuccessEvent(entity, duration, userData));
                return entity;
            }
            catch (Exception ex)
            {
                info.IsTransitioning = false;
                if (addedToGroup)
                {
                    group.RemoveEntity(entity);
                }

                entities.Remove(entityId);
                ownedInstances.Remove(info);
                if (initialized)
                {
                    try
                    {
                        entity.OnRecycle();
                    }
                    catch (Exception recycleError)
                    {
                        ex = new AggregateException(ex, recycleError);
                    }
                }

                throw new RFrameworkException($"Failed to activate entity '{entityId}'.", ex);
            }
        }

        private void Hide(EntityInfo info, object userData, ref List<Exception> failures)
        {
            if (info.IsTransitioning)
            {
                throw new RFrameworkException(
                    $"Entity '{info.Entity.Id}' cannot be hidden during another lifecycle callback.");
            }

            info.IsTransitioning = true;
            EntityInfo[] children = info.GetChildrenSnapshot();
            for (int i = 0; i < children.Length; i++)
            {
                try
                {
                    Hide(children[i], userData, ref failures);
                }
                catch (Exception ex)
                {
                    (failures ??= new List<Exception>()).Add(ex);
                }
            }

            if (info.Parent != null)
            {
                Detach(info, userData, ref failures);
            }

            long entityId = info.Entity.Id;
            string assetName = info.Entity.AssetName;
            TryRun(() => info.Entity.OnHide(isShutdown, userData), ref failures);
            info.Group.RemoveEntity(info.Entity);
            entities.Remove(entityId);
            info.IsTransitioning = false;
            eventModule?.FireSafely(new HideEntityCompleteEvent(
                entityId, assetName, info.Group.Name, userData));

            if (info.IsExternal)
            {
                TryRun(info.Entity.OnRecycle, ref failures);
            }
            else
            {
                recycleQueue.Enqueue(info);
            }
        }

        private void Detach(EntityInfo child, object userData, ref List<Exception> failures)
        {
            EntityInfo parent = child.Parent;
            if (parent == null)
            {
                return;
            }

            child.Parent = null;
            parent.RemoveChild(child);
            bool childWasTransitioning = child.IsTransitioning;
            bool parentWasTransitioning = parent.IsTransitioning;
            child.IsTransitioning = true;
            parent.IsTransitioning = true;
            try
            {
                TryRun(() => child.Entity.OnDetachFrom(parent.Entity, userData), ref failures);
                TryRun(() => parent.Entity.OnDetached(child.Entity, userData), ref failures);
            }
            finally
            {
                child.IsTransitioning = childWasTransitioning;
                parent.IsTransitioning = parentWasTransitioning;
            }
        }

        private void ProcessRecycleQueue(bool releaseInstances, ref List<Exception> failures)
        {
            while (recycleQueue.Count > 0)
            {
                EntityInfo info = recycleQueue.Dequeue();
                TryRun(info.Entity.OnRecycle, ref failures);
                if (!ownedInstances.TryGetValue(info, out EntityInstanceObject instance))
                {
                    continue;
                }

                ownedInstances.Remove(info);
                if (releaseInstances)
                {
                    TryRun(instance.Release, ref failures);
                }
                else
                {
                    TryRun(() => info.Group.ReturnCached(instance), ref failures);
                }
            }
        }

        private void TryHide(long entityId, object userData, ref List<Exception> failures)
        {
            if (!entities.TryGetValue(entityId, out EntityInfo info))
            {
                return;
            }

            try
            {
                Hide(info, userData, ref failures);
            }
            catch (Exception ex)
            {
                (failures ??= new List<Exception>()).Add(ex);
            }
        }

        private void EnsureCanCreate(long entityId, string assetName)
        {
            EnsureRunning();
            if (entityId == 0)
            {
                throw new RFrameworkException("Entity id cannot be zero.");
            }

            if (string.IsNullOrWhiteSpace(assetName))
            {
                throw new RFrameworkException("Entity asset name is invalid.");
            }

            if (entities.ContainsKey(entityId) || loading.ContainsKey(entityId))
            {
                throw new RFrameworkException($"Entity id '{entityId}' is already in use.");
            }
        }

        private void EnsureRunning()
        {
            if (isShutdown)
            {
                throw new RFrameworkException("Entity module is shutdown.");
            }
        }

        private void EnsureStable(EntityInfo info)
        {
            if (info.IsTransitioning)
            {
                throw new RFrameworkException($"Entity '{info.Entity.Id}' is changing lifecycle state.");
            }
        }

        private EntityGroup GetRequiredGroup(string name)
        {
            if (string.IsNullOrEmpty(name) || !groups.TryGetValue(name, out EntityGroup group))
            {
                throw new RFrameworkException($"Entity group '{name}' does not exist.");
            }

            return group;
        }

        private EntityInfo GetRequiredEntity(long entityId)
        {
            if (!entities.TryGetValue(entityId, out EntityInfo info))
            {
                throw new RFrameworkException($"Entity '{entityId}' does not exist.");
            }

            return info;
        }

        private void PublishFailure(long entityId, string assetName, Exception ex, object userData)
        {
            eventModule?.FireSafely(new ShowEntityFailureEvent(entityId, assetName, ex.Message, userData));
        }

        private void TryUnloadAfterFailure(string assetName, ref Exception failure)
        {
            try
            {
                resourceModule.UnloadAsset<object>(assetName);
            }
            catch (Exception cleanupError)
            {
                failure = new AggregateException(failure, cleanupError);
            }
        }

        private static void TryReleaseAfterFailure(EntityInstanceObject instance, ref Exception failure)
        {
            try
            {
                instance.Release();
            }
            catch (Exception cleanupError)
            {
                failure = new AggregateException(failure, cleanupError);
            }
        }

        private static void TryRun(Action action, ref List<Exception> failures)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                (failures ??= new List<Exception>()).Add(ex);
            }
        }

        private static void ThrowFailures(string operation, List<Exception> failures)
        {
            if (failures != null)
            {
                throw new RFrameworkException(
                    $"{operation} encountered {failures.Count} error(s).",
                    new AggregateException(failures));
            }
        }
    }
}
