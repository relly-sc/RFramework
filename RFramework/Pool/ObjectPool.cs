using System;
using System.Collections.Generic;

namespace RFramework.Pool
{
    /// <summary>
    /// 泛型对象池实现。
    /// 使用 Stack 管理可用对象，HashSet 追踪活跃对象，
    /// 通过委托注入创建/回收/销毁行为。
    /// </summary>
    /// <typeparam name="T">池中对象类型，必须是引用类型。</typeparam>
    internal sealed class ObjectPool<T> : IObjectPool<T>, IObjectPoolBase where T : class
    {
        private readonly string name;
        private readonly Stack<T> available;
        private readonly HashSet<T> active;
        private readonly Func<T> createFunc;
        private readonly Action<T> onSpawn;
        private readonly Action<T> onUnspawn;
        private readonly Action<T> onDestroy;
        private readonly int capacity;
        private readonly bool autoPoolable;

        /// <summary>
        /// 初始化对象池的新实例。
        /// </summary>
        /// <param name="name">对象池名称。</param>
        /// <param name="createFunc">对象工厂。</param>
        /// <param name="onSpawn">取出回调。</param>
        /// <param name="onUnspawn">回收回调。</param>
        /// <param name="onDestroy">销毁回调。</param>
        /// <param name="capacity">容量上限。</param>
        public ObjectPool(string name, Func<T> createFunc, Action<T> onSpawn, Action<T> onUnspawn, Action<T> onDestroy, int capacity)
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

            this.name = name;
            this.createFunc = createFunc;
            this.onSpawn = onSpawn;
            this.onUnspawn = onUnspawn;
            this.onDestroy = onDestroy;
            this.capacity = capacity;

            available = new Stack<T>(capacity);
            active = new HashSet<T>();
            autoPoolable = typeof(IPoolable).IsAssignableFrom(typeof(T));
        }

        /// <summary>
        /// 获取对象池名称。
        /// </summary>
        public string Name
        {
            get { return name; }
        }

        /// <summary>
        /// 获取对象类型。
        /// </summary>
        public Type ObjectType
        {
            get { return typeof(T); }
        }

        /// <summary>
        /// 获取池中对象总数。
        /// </summary>
        public int TotalCount
        {
            get { return available.Count + active.Count; }
        }

        /// <summary>
        /// 获取可获取的对象数量。
        /// </summary>
        public int AvailableCount
        {
            get { return available.Count; }
        }

        /// <summary>
        /// 获取活跃对象数量。
        /// </summary>
        public int ActiveCount
        {
            get { return active.Count; }
        }

        /// <summary>
        /// 从池中获取对象。
        /// </summary>
        /// <returns>获取到的对象。</returns>
        public T Spawn()
        {
            T obj;
            if (available.Count > 0)
            {
                obj = available.Pop();
            }
            else
            {
                obj = createFunc();
                if (obj == null)
                {
                    throw new RFrameworkException(Utility.Text.Format("Object pool '{0}' create function returned null.", name));
                }
            }

            active.Add(obj);
            InvokeSpawn(obj);
            return obj;
        }

        /// <summary>
        /// 回收对象到池中。
        /// </summary>
        /// <param name="obj">要回收的对象。</param>
        public void Unspawn(T obj)
        {
            if (obj == null)
            {
                throw new RFrameworkException("Can not unspawn null object.");
            }

            if (!active.Remove(obj))
            {
                return;
            }

            InvokeUnspawn(obj);

            if (available.Count + active.Count >= capacity)
            {
                // 容量已满，直接销毁
                DestroyObject(obj);
                return;
            }

            available.Push(obj);
        }

        /// <summary>
        /// 预热对象池。
        /// </summary>
        /// <param name="count">预热数量。</param>
        public void Prewarm(int count)
        {
            if (count <= 0)
            {
                return;
            }

            int createCount = count - available.Count;
            for (int i = 0; i < createCount; i++)
            {
                T obj = createFunc();
                if (obj == null)
                {
                    throw new RFrameworkException(Utility.Text.Format("Object pool '{0}' create function returned null during prewarm.", name));
                }

                InvokeUnspawn(obj);
                available.Push(obj);
            }
        }

        /// <summary>
        /// 释放所有未使用的对象。
        /// </summary>
        public void ReleaseUnused()
        {
            while (available.Count > 0)
            {
                T obj = available.Pop();
                DestroyObject(obj);
            }
        }

        /// <summary>
        /// 清空对象池，销毁所有对象。
        /// </summary>
        public void Clear()
        {
            // 先销毁活跃对象
            foreach (T obj in active)
            {
                DestroyObject(obj);
            }

            active.Clear();

            // 再销毁可用对象
            ReleaseUnused();
        }

        /// <summary>
        /// 获取对象名（调试用）。
        /// </summary>
        public override string ToString()
        {
            return Utility.Text.Format("ObjectPool<{0}>({1}) Total:{2} Available:{3} Active:{4}",
                typeof(T).Name, name, TotalCount, available.Count, active.Count);
        }

        /// <summary>
        /// 调用 Spawn 回调。
        /// 优先使用 autoPoolable（IPoolable 接口），其次使用 onSpawn 委托。
        /// </summary>
        private void InvokeSpawn(T obj)
        {
            if (autoPoolable)
            {
                ((IPoolable)obj).OnSpawn();
            }

            if (onSpawn != null)
            {
                onSpawn(obj);
            }
        }

        /// <summary>
        /// 调用 Unspawn 回调。
        /// 优先使用 autoPoolable（IPoolable 接口），其次使用 onUnspawn 委托。
        /// </summary>
        private void InvokeUnspawn(T obj)
        {
            if (autoPoolable)
            {
                ((IPoolable)obj).OnUnspawn();
            }

            if (onUnspawn != null)
            {
                onUnspawn(obj);
            }
        }

        /// <summary>
        /// 销毁对象。
        /// </summary>
        private void DestroyObject(T obj)
        {
            if (onDestroy != null)
            {
                onDestroy(obj);
            }
        }
    }
}
