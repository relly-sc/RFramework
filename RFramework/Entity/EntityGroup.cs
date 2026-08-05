using System;
using System.Collections.Generic;

namespace RFramework
{
    /// <summary>
    /// 实体组实现。活跃实体按编号索引，隐藏后的框架实体按资源地址缓存。
    /// </summary>
    internal sealed class EntityGroup : IEntityGroup
    {
        private readonly Dictionary<long, IEntity> entities = new Dictionary<long, IEntity>();
        private readonly Dictionary<string, List<EntityInstanceObject>> cache =
            new Dictionary<string, List<EntityInstanceObject>>(StringComparer.Ordinal);
        private readonly List<IEntity> updateSnapshot = new List<IEntity>();
        private float autoReleaseInterval;
        private float expireTime;
        private int capacity;
        private int cachedEntityCount;
        private float clock;
        private float releaseTimer;

        /// <summary>
        /// 创建实体组。
        /// </summary>
        /// <param name="name">实体组名称。</param>
        /// <param name="autoReleaseInterval">缓存自动释放检查间隔。</param>
        /// <param name="capacity">整个实体组允许缓存的实例上限；零表示不限制。</param>
        /// <param name="expireTime">缓存实例过期时间；零表示不过期。</param>
        public EntityGroup(string name, float autoReleaseInterval, int capacity, float expireTime)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new RFrameworkException("Entity group name is invalid.");
            }

            Name = name.Trim();
            AutoReleaseInterval = autoReleaseInterval;
            Capacity = capacity;
            ExpireTime = expireTime;
        }

        /// <inheritdoc/>
        public string Name { get; }

        /// <inheritdoc/>
        public int EntityCount => entities.Count;

        /// <inheritdoc/>
        public int CachedEntityCount => cachedEntityCount;

        /// <inheritdoc/>
        public float AutoReleaseInterval
        {
            get => autoReleaseInterval;
            set
            {
                if (value < 0f)
                {
                    throw new RFrameworkException("Entity group auto release interval cannot be negative.");
                }

                autoReleaseInterval = value;
            }
        }

        /// <inheritdoc/>
        public int Capacity
        {
            get => capacity;
            set
            {
                if (value < 0)
                {
                    throw new RFrameworkException("Entity group capacity cannot be negative.");
                }

                capacity = value;
            }
        }

        /// <inheritdoc/>
        public float ExpireTime
        {
            get => expireTime;
            set
            {
                if (value < 0f)
                {
                    throw new RFrameworkException("Entity group expire time cannot be negative.");
                }

                expireTime = value;
            }
        }

        /// <inheritdoc/>
        public bool HasEntity(long entityId)
        {
            return entities.ContainsKey(entityId);
        }

        /// <inheritdoc/>
        public bool HasEntity(string assetName)
        {
            return GetEntity(assetName) != null;
        }

        /// <inheritdoc/>
        public IEntity GetEntity(long entityId)
        {
            return entities.TryGetValue(entityId, out IEntity entity) ? entity : null;
        }

        /// <inheritdoc/>
        public IEntity GetEntity(string assetName)
        {
            foreach (IEntity entity in entities.Values)
            {
                if (string.Equals(entity.AssetName, assetName, StringComparison.Ordinal))
                {
                    return entity;
                }
            }

            return null;
        }

        /// <inheritdoc/>
        public IEntity[] GetEntities(string assetName)
        {
            List<IEntity> results = new List<IEntity>();
            GetEntities(assetName, results);
            return results.ToArray();
        }

        /// <inheritdoc/>
        public void GetEntities(string assetName, List<IEntity> results)
        {
            if (results == null)
            {
                throw new RFrameworkException("Results list is invalid.");
            }

            results.Clear();
            foreach (IEntity entity in entities.Values)
            {
                if (string.Equals(entity.AssetName, assetName, StringComparison.Ordinal))
                {
                    results.Add(entity);
                }
            }
        }

        /// <inheritdoc/>
        public IEntity[] GetAllEntities()
        {
            IEntity[] results = new IEntity[entities.Count];
            entities.Values.CopyTo(results, 0);
            return results;
        }

        /// <inheritdoc/>
        public void GetAllEntities(List<IEntity> results)
        {
            if (results == null)
            {
                throw new RFrameworkException("Results list is invalid.");
            }

            results.Clear();
            results.AddRange(entities.Values);
        }

        /// <summary>
        /// 将活跃实体加入组内索引。
        /// </summary>
        /// <param name="entity">待加入的实体。</param>
        public void AddEntity(IEntity entity)
        {
            if (entity == null)
            {
                throw new RFrameworkException("Entity is invalid.");
            }

            if (entities.ContainsKey(entity.Id))
            {
                throw new RFrameworkException($"Entity '{entity.Id}' already belongs to group '{Name}'.");
            }

            entities.Add(entity.Id, entity);
        }

        /// <summary>
        /// 从组内索引移除指定实体。
        /// </summary>
        /// <param name="entity">待移除的实体。</param>
        public void RemoveEntity(IEntity entity)
        {
            if (entity != null && entities.TryGetValue(entity.Id, out IEntity current)
                && ReferenceEquals(current, entity))
            {
                entities.Remove(entity.Id);
            }
        }

        /// <summary>
        /// 取出指定资源地址最近缓存的实体实例。
        /// </summary>
        /// <param name="assetName">实体资源地址。</param>
        /// <returns>缓存实例；不存在时返回 null。</returns>
        public EntityInstanceObject TakeCached(string assetName)
        {
            if (!cache.TryGetValue(assetName, out List<EntityInstanceObject> bucket)
                || bucket.Count == 0)
            {
                return null;
            }

            int index = bucket.Count - 1;
            EntityInstanceObject instance = bucket[index];
            bucket.RemoveAt(index);
            cachedEntityCount--;
            if (bucket.Count == 0)
            {
                cache.Remove(assetName);
            }

            return instance;
        }

        /// <summary>
        /// 将隐藏后的实体实例归还组缓存；达到容量时立即释放。
        /// </summary>
        /// <param name="instance">待缓存的实体实例。</param>
        public void ReturnCached(EntityInstanceObject instance)
        {
            if (instance == null)
            {
                return;
            }

            if (Capacity > 0 && cachedEntityCount >= Capacity)
            {
                instance.Release();
                return;
            }

            if (!cache.TryGetValue(instance.AssetName, out List<EntityInstanceObject> bucket))
            {
                bucket = new List<EntityInstanceObject>();
                cache.Add(instance.AssetName, bucket);
            }

            instance.LastUsedAt = clock;
            bucket.Add(instance);
            cachedEntityCount++;
        }

        /// <summary>
        /// 更新组内活跃实体并释放过期缓存。
        /// </summary>
        /// <param name="elapseSeconds">受时间缩放影响的帧间隔。</param>
        /// <param name="realElapseSeconds">不受时间缩放影响的帧间隔。</param>
        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            List<Exception> failures = null;
            updateSnapshot.Clear();
            updateSnapshot.AddRange(entities.Values);
            for (int i = 0; i < updateSnapshot.Count; i++)
            {
                IEntity entity = updateSnapshot[i];
                if (!entities.TryGetValue(entity.Id, out IEntity current) || !ReferenceEquals(current, entity))
                {
                    continue;
                }

                try
                {
                    entity.OnUpdate(elapseSeconds, realElapseSeconds);
                }
                catch (Exception ex)
                {
                    (failures ??= new List<Exception>()).Add(ex);
                }
            }

            clock += Math.Max(0f, realElapseSeconds);
            if (AutoReleaseInterval > 0f)
            {
                releaseTimer += Math.Max(0f, realElapseSeconds);
                if (releaseTimer >= AutoReleaseInterval)
                {
                    releaseTimer = 0f;
                    ReleaseExpired(ref failures);
                }
            }

            if (failures != null)
            {
                throw new RFrameworkException(
                    $"Entity group '{Name}' update encountered {failures.Count} error(s).",
                    new AggregateException(failures));
            }
        }

        /// <summary>
        /// 清空实体索引并释放所有缓存实例。
        /// </summary>
        public void Destroy()
        {
            List<Exception> failures = null;
            foreach (List<EntityInstanceObject> bucket in cache.Values)
            {
                for (int i = 0; i < bucket.Count; i++)
                {
                    TryRelease(bucket[i], ref failures);
                }
            }

            cache.Clear();
            entities.Clear();
            updateSnapshot.Clear();
            cachedEntityCount = 0;

            if (failures != null)
            {
                throw new RFrameworkException(
                    $"Entity group '{Name}' cleanup encountered {failures.Count} error(s).",
                    new AggregateException(failures));
            }
        }

        private void ReleaseExpired(ref List<Exception> failures)
        {
            if (ExpireTime <= 0f)
            {
                return;
            }

            List<string> emptyBuckets = null;
            foreach (KeyValuePair<string, List<EntityInstanceObject>> pair in cache)
            {
                List<EntityInstanceObject> bucket = pair.Value;
                for (int i = bucket.Count - 1; i >= 0; i--)
                {
                    if (clock - bucket[i].LastUsedAt < ExpireTime)
                    {
                        continue;
                    }

                    EntityInstanceObject instance = bucket[i];
                    bucket.RemoveAt(i);
                    cachedEntityCount--;
                    TryRelease(instance, ref failures);
                }

                if (bucket.Count == 0)
                {
                    (emptyBuckets ??= new List<string>()).Add(pair.Key);
                }
            }

            if (emptyBuckets == null)
            {
                return;
            }

            for (int i = 0; i < emptyBuckets.Count; i++)
            {
                cache.Remove(emptyBuckets[i]);
            }
        }

        private static void TryRelease(EntityInstanceObject instance, ref List<Exception> failures)
        {
            try
            {
                instance.Release();
            }
            catch (Exception ex)
            {
                (failures ??= new List<Exception>()).Add(ex);
            }
        }
    }
}
