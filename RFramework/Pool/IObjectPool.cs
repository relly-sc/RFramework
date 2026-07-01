using System;

namespace RFramework.Pool
{
    /// <summary>
    /// 对象池接口，表示单个类型的对象池实例。
    /// </summary>
    /// <typeparam name="T">池中对象类型。</typeparam>
    public interface IObjectPool<T> where T : class
    {
        /// <summary>
        /// 获取对象池名称。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 获取池中对象类型。
        /// </summary>
        Type ObjectType { get; }

        /// <summary>
        /// 获取池中对象总数（已取出 + 可获取）。
        /// </summary>
        int TotalCount { get; }

        /// <summary>
        /// 获取可获取（未被取出）的对象数量。
        /// </summary>
        int AvailableCount { get; }

        /// <summary>
        /// 获取当前已被取出的活跃对象数量。
        /// </summary>
        int ActiveCount { get; }

        /// <summary>
        /// 从池中获取一个对象。
        /// 若无可用对象则调用 createFunc 创建新对象。
        /// </summary>
        /// <returns>获取到的对象。</returns>
        T Spawn();

        /// <summary>
        /// 回收对象到池中。
        /// 若池容量已满则直接销毁该对象（调用 onDestroy）。
        /// </summary>
        /// <param name="obj">要回收的对象。</param>
        void Unspawn(T obj);

        /// <summary>
        /// 预热对象池，预先创建指定数量的对象并放入可用队列。
        /// </summary>
        /// <param name="count">预热数量。</param>
        void Prewarm(int count);

        /// <summary>
        /// 释放所有未使用的对象（销毁 available 队列中的全部对象）。
        /// </summary>
        void ReleaseUnused();

        /// <summary>
        /// 清空对象池，销毁所有对象（包括活跃对象）。
        /// </summary>
        void Clear();
    }
}
