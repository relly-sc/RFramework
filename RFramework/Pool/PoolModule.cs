using System;
using System.Collections.Generic;

namespace RFramework.Pool
{
    /// <summary>
    /// 对象池模块。通过泛型 + 可选参数 + 委托注入实现零侵入池化。
    /// </summary>
    internal sealed class PoolModule : RFrameworkModule, IPoolModule
    {
        private readonly Dictionary<string, IObjectPoolBase> pools;

        /// <summary>
        /// 初始化对象池服务的新实例。
        /// </summary>
        public PoolModule()
        {
            pools = new Dictionary<string, IObjectPoolBase>();
        }

        /// <summary>
        /// 获取框架模块优先级。
        /// </summary>
        internal override int Priority
        {
            get { return 5; }
        }

        /// <summary>
        /// 对象池服务轮询。
        /// 对象池本身不需要每帧更新——Spawn/Unspawn 是事件驱动的。
        /// </summary>
        internal override void Update(float elapseSeconds, float realElapseSeconds)
        {
        }

        /// <summary>
        /// 关闭并清理对象池服务，销毁所有对象池。
        /// </summary>
        internal override void Shutdown()
        {
            List<IObjectPoolBase> poolsToClear;
            lock (pools)
            {
                poolsToClear = new List<IObjectPoolBase>(pools.Values);
                pools.Clear();
            }

            for (int i = 0; i < poolsToClear.Count; i++)
            {
                poolsToClear[i].Clear();
            }
        }

        /// <summary>
        /// 获取当前管理的对象池数量。
        /// </summary>
        public int PoolCount
        {
            get
            {
                lock (pools)
                {
                    return pools.Count;
                }
            }
        }

        /// <summary>
        /// 创建对象池。
        /// </summary>
        public IObjectPool<T> CreatePool<T>(
            string name,
            Func<T> createFunc,
            Action<T> onSpawn = null,
            Action<T> onUnspawn = null,
            Action<T> onDestroy = null,
            int capacity = 64) where T : class
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new RFrameworkException("Object pool name is invalid.");
            }

            if (createFunc == null)
            {
                throw new RFrameworkException("Create function is invalid.");
            }

            if (capacity <= 0)
            {
                throw new RFrameworkException("Object pool capacity must be greater than 0.");
            }

            lock (pools)
            {
                if (pools.ContainsKey(name))
                {
                    throw new RFrameworkException(Utility.Text.Format("Object pool '{0}' already exists.", name));
                }

                ObjectPool<T> pool = new ObjectPool<T>(name, createFunc, onSpawn, onUnspawn, onDestroy, capacity);
                pools.Add(name, pool);
                return pool;
            }
        }

        /// <summary>
        /// 销毁指定名称的对象池。
        /// </summary>
        public bool DestroyPool(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new RFrameworkException("Object pool name is invalid.");
            }

            IObjectPoolBase poolToClear = null;
            lock (pools)
            {
                if (pools.TryGetValue(name, out IObjectPoolBase pool))
                {
                    pools.Remove(name);
                    poolToClear = pool;
                }
            }

            if (poolToClear == null)
            {
                return false;
            }

            // Invoke pool callbacks after releasing the registry lock. They
            // are user code and may create or destroy other pools.
            poolToClear.Clear();
            return true;
        }

        /// <summary>
        /// 获取指定名称的对象池。
        /// </summary>
        public IObjectPool<T> GetPool<T>(string name) where T : class
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new RFrameworkException("Object pool name is invalid.");
            }

            lock (pools)
            {
                if (pools.TryGetValue(name, out IObjectPoolBase pool))
                {
                    return pool as IObjectPool<T>;
                }
            }

            return null;
        }

        /// <summary>
        /// 释放所有对象池中未使用的对象。
        /// </summary>
        public void ReleaseAllUnused()
        {
            List<IObjectPoolBase> poolsToRelease;
            lock (pools)
            {
                poolsToRelease = new List<IObjectPoolBase>(pools.Values);
            }

            for (int i = 0; i < poolsToRelease.Count; i++)
            {
                poolsToRelease[i].ReleaseUnused();
            }
        }
    }
}
