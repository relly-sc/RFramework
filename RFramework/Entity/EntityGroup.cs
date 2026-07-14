using System;
using System.Collections.Generic;

namespace RFramework
{
    /// <summary>
    /// 实体组实现，管理同类型实体的集合及其专属对象池。
    /// 内部使用 Dictionary&lt;assetName, List&lt;EntityInstanceObject&gt;&gt; 按资源名分组缓存，
    /// 提供 O(1) 的 Spawn 查找（优于 GF 的遍历查找）。
    /// 支持 Capacity 容量上限、ExpireTime 过期回收、AutoReleaseInterval 定时释放。
    /// </summary>
    internal sealed class EntityGroup : IEntityGroup
    {
        /// <summary>
        /// 实体组名称。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 实体组中当前活跃的实体数量。
        /// </summary>
        public int EntityCount => entities.Count;

        /// <summary>
        /// 对象池自动释放间隔（秒）。每隔此秒数触发一次过期对象扫描。
        /// </summary>
        public float AutoReleaseInterval { get; set; }

        /// <summary>
        /// 对象池容量上限。池满时新归还的对象直接 Release 销毁。
        /// </summary>
        public int Capacity { get; set; }

        /// <summary>
        /// 对象池中对象过期时间（秒）。超过此时间未被使用的对象将被释放。
        /// </summary>
        public float ExpireTime { get; set; }

        /// <summary>
        /// 对象池优先级。
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// 实体组辅助器。
        /// </summary>
        public IEntityGroupHelper Helper { get; }

        /// <summary>
        /// 实体组内活跃实体的有序列表。
        /// </summary>
        private readonly List<IEntity> entities = new List<IEntity>();

        /// <summary>
        /// 实体实例对象池（按 assetName 索引）。
        /// 每个 assetName 对应一个独立的 List，取出取最后一个（最近归还的）。
        /// </summary>
        private readonly Dictionary<string, List<EntityInstanceObject>> instancePools = new Dictionary<string, List<EntityInstanceObject>>();

        /// <summary>
        /// 遍历缓存节点索引，用于在 OnUpdate 回调中安全迭代。
        /// </summary>
        private int cachedEntityIndex = -1;

        /// <summary>
        /// 距离上次自动释放的累计时间（秒）。
        /// </summary>
        private float autoReleaseTimer;

        /// <summary>
        /// 初始化实体组。
        /// </summary>
        /// <param name="name">实体组名称。</param>
        /// <param name="autoReleaseInterval">自动释放间隔。</param>
        /// <param name="capacity">对象池容量。</param>
        /// <param name="expireTime">对象过期时间。</param>
        /// <param name="priority">优先级。</param>
        /// <param name="helper">实体组辅助器。</param>
        public EntityGroup(string name, float autoReleaseInterval, int capacity, float expireTime,
            int priority, IEntityGroupHelper helper)
        {
            Name = name;
            AutoReleaseInterval = autoReleaseInterval;
            Capacity = capacity;
            ExpireTime = expireTime;
            Priority = priority;
            Helper = helper;
        }

        /// <summary>
        /// 判断实体组中是否包含指定编号的实体。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <returns>是否包含。</returns>
        public bool HasEntity(long entityId)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].Id == entityId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 判断实体组中是否包含指定资源路径的实体。
        /// </summary>
        /// <param name="assetName">实体资源路径。</param>
        /// <returns>是否包含。</returns>
        public bool HasEntity(string assetName)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].AssetName == assetName)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取指定编号的实体。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <returns>实体实例。</returns>
        public IEntity GetEntity(long entityId)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].Id == entityId)
                {
                    return entities[i];
                }
            }

            return null;
        }

        /// <summary>
        /// 获取指定资源路径的实体。
        /// </summary>
        /// <param name="assetName">实体资源路径。</param>
        /// <returns>实体实例。</returns>
        public IEntity GetEntity(string assetName)
        {
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].AssetName == assetName)
                {
                    return entities[i];
                }
            }

            return null;
        }

        /// <summary>
        /// 获取指定资源路径的所有实体。
        /// </summary>
        /// <param name="assetName">实体资源路径。</param>
        /// <returns>实体数组。</returns>
        public IEntity[] GetEntities(string assetName)
        {
            List<IEntity> results = new List<IEntity>();
            GetEntities(assetName, results);
            return results.ToArray();
        }

        /// <summary>
        /// 获取指定资源路径的所有实体，填充到结果列表。
        /// </summary>
        /// <param name="assetName">实体资源路径。</param>
        /// <param name="results">结果列表。</param>
        public void GetEntities(string assetName, List<IEntity> results)
        {
            if (results == null)
            {
                throw new RFrameworkException("Results is invalid.");
            }

            results.Clear();
            for (int i = 0; i < entities.Count; i++)
            {
                if (entities[i].AssetName == assetName)
                {
                    results.Add(entities[i]);
                }
            }
        }

        /// <summary>
        /// 获取实体组中所有活跃实体。
        /// </summary>
        /// <returns>实体数组。</returns>
        public IEntity[] GetAllEntities()
        {
            return entities.ToArray();
        }

        /// <summary>
        /// 获取实体组中所有活跃实体，填充到结果列表。
        /// </summary>
        /// <param name="results">结果列表。</param>
        public void GetAllEntities(List<IEntity> results)
        {
            if (results == null)
            {
                throw new RFrameworkException("Results is invalid.");
            }

            results.Clear();
            for (int i = 0; i < entities.Count; i++)
            {
                results.Add(entities[i]);
            }
        }

        /// <summary>
        /// 添加实体到实体组。
        /// </summary>
        /// <param name="entity">实体实例。</param>
        public void AddEntity(IEntity entity)
        {
            if (entities.Contains(entity))
            {
                throw new RFrameworkException($"Entity '{entity.Id}' is already in group '{Name}'.");
            }

            entities.Add(entity);
        }

        /// <summary>
        /// 从实体组移除实体。
        /// </summary>
        /// <param name="entity">实体实例。</param>
        public void RemoveEntity(IEntity entity)
        {
            if (!entities.Contains(entity))
            {
                return;
            }

            // 安全移除：如果正在遍历中，跳过到下一个
            int index = entities.IndexOf(entity);
            if (index == cachedEntityIndex)
            {
                cachedEntityIndex = index - 1;
            }

            entities.Remove(entity);
        }

        /// <summary>
        /// 驱动实体组内所有实体的 OnUpdate 回调。
        /// 使用 cachedEntityIndex 保证遍历中删除实体的安全性。
        /// 同时按 AutoReleaseInterval 定时触发过期对象释放。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间。</param>
        /// <param name="realElapseSeconds">实际流逝时间。</param>
        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            for (int i = 0; i < entities.Count;)
            {
                cachedEntityIndex = i;
                IEntity entity = entities[i];
                entity.OnUpdate(elapseSeconds, realElapseSeconds);

                // OnUpdate may hide/remove the current entity. Advance from
                // the entity's current position so the element shifted into
                // this slot is still updated this frame.
                int currentIndex = entities.IndexOf(entity);
                i = currentIndex >= 0 ? currentIndex + 1 : i;
            }

            cachedEntityIndex = -1;

            // 定时触发过期对象释放
            if (AutoReleaseInterval > 0f)
            {
                autoReleaseTimer += realElapseSeconds;
                if (autoReleaseTimer >= AutoReleaseInterval)
                {
                    autoReleaseTimer = 0f;
                    ReleaseExpiredInstances();
                }
            }
        }

        /// <summary>
        /// 从对象池中取出一个可复用的实体实例。
        /// 取出时更新 LastUseTimestamp。
        /// </summary>
        /// <param name="assetName">实体资源路径。</param>
        /// <returns>可复用的实例对象，池中无可用对象时返回 null。</returns>
        public EntityInstanceObject SpawnEntityInstanceObject(string assetName)
        {
            if (!instancePools.TryGetValue(assetName, out List<EntityInstanceObject> pool) || pool.Count <= 0)
            {
                return null;
            }

            // 取最后一个（最近归还的）
            EntityInstanceObject instanceObject = pool[pool.Count - 1];
            pool.RemoveAt(pool.Count - 1);
            instanceObject.LastUseTimestamp = System.DateTime.UtcNow.Ticks;
            return instanceObject;
        }

        /// <summary>
        /// 注册实体实例到对象池（首次创建后注册）。
        /// </summary>
        /// <param name="instanceObject">实体实例对象。</param>
        /// <param name="spawned">是否已被取出（首次创建时为 true，表示当前正在使用中，不入池）。</param>
        public void RegisterEntityInstanceObject(EntityInstanceObject instanceObject, bool spawned)
        {
            if (!instancePools.TryGetValue(instanceObject.AssetName, out List<EntityInstanceObject> pool))
            {
                pool = new List<EntityInstanceObject>();
                instancePools.Add(instanceObject.AssetName, pool);
            }

            if (!spawned)
            {
                // 归还入池：更新最后使用时间
                instanceObject.LastUseTimestamp = System.DateTime.UtcNow.Ticks;
                pool.Add(instanceObject);
            }
        }

        /// <summary>
        /// 将实体实例归还对象池（HideEntity 回收后由 EntityModule 调用）。
        /// 池满时直接 Release 销毁。
        /// </summary>
        /// <param name="instanceObject">要归还的实例对象。</param>
        public void UnspawnEntityInstance(EntityInstanceObject instanceObject)
        {
            if (instanceObject == null)
            {
                return;
            }

            // 更新最后使用时间
            instanceObject.LastUseTimestamp = System.DateTime.UtcNow.Ticks;

            if (!instancePools.TryGetValue(instanceObject.AssetName, out List<EntityInstanceObject> pool))
            {
                pool = new List<EntityInstanceObject>();
                instancePools.Add(instanceObject.AssetName, pool);
            }

            // 容量检查：池满时直接销毁
            if (Capacity > 0 && pool.Count >= Capacity)
            {
                instanceObject.Release();
                return;
            }

            pool.Add(instanceObject);
        }

        /// <summary>
        /// 释放所有过期的池中实例。
        /// 遍历所有 assetName 的池，移除超过 ExpireTime 未被使用的对象。
        /// </summary>
        private void ReleaseExpiredInstances()
        {
            if (ExpireTime <= 0f)
            {
                return;
            }

            foreach (KeyValuePair<string, List<EntityInstanceObject>> kv in instancePools)
            {
                List<EntityInstanceObject> pool = kv.Value;
                for (int i = pool.Count - 1; i >= 0; i--)
                {
                    if (pool[i].IsExpired(ExpireTime))
                    {
                        pool[i].Release();
                        pool.RemoveAt(i);
                    }
                }
            }
        }

        /// <summary>
        /// 销毁实体组，释放所有对象池实例。
        /// </summary>
        public void Destroy()
        {
            // 释放所有池中实例
            foreach (KeyValuePair<string, List<EntityInstanceObject>> kv in instancePools)
            {
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    kv.Value[i].Release();
                }
            }

            instancePools.Clear();
            entities.Clear();
            autoReleaseTimer = 0f;
        }
    }
}
