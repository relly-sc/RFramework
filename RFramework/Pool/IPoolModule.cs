using System;

namespace RFramework
{
    /// <summary>
    /// 对象池服务接口。通过委托注入行为，不强制继承基类。
    /// </summary>
    public interface IPoolModule
    {
        /// <summary>
        /// 获取当前管理的对象池数量。
        /// </summary>
        int PoolCount { get; }

        /// <summary>
        /// 创建对象池。
        /// </summary>
        /// <typeparam name="T">池中对象类型（必须是引用类型）。</typeparam>
        /// <param name="name">对象池名称，全局唯一。同名重复创建会抛出异常。</param>
        /// <param name="createFunc">对象工厂方法，Spawn 无可用对象时调用。</param>
        /// <param name="onSpawn">对象被取出时的回调，可选。</param>
        /// <param name="onUnspawn">对象被回收时的回调，可选。</param>
        /// <param name="onDestroy">对象被销毁（超出容量）时的回调，可选。</param>
        /// <param name="capacity">对象池容量上限，超出时 Unspawn 的对象会被直接销毁。默认 64。</param>
        /// <returns>创建的对象池实例。</returns>
        IObjectPool<T> CreatePool<T>(
            string name,
            Func<T> createFunc,
            Action<T> onSpawn = null,
            Action<T> onUnspawn = null,
            Action<T> onDestroy = null,
            int capacity = 64) where T : class;

        /// <summary>
        /// 销毁指定名称的对象池，释放其中所有对象。
        /// </summary>
        /// <param name="name">对象池名称。</param>
        /// <returns>是否成功销毁。名称不存在时返回 false。</returns>
        bool DestroyPool(string name);

        /// <summary>
        /// 获取指定名称的对象池。
        /// </summary>
        /// <typeparam name="T">池中对象类型。</typeparam>
        /// <param name="name">对象池名称。</param>
        /// <returns>对象池实例，不存在时返回 null。</returns>
        IObjectPool<T> GetPool<T>(string name) where T : class;

        /// <summary>
        /// 释放所有对象池中未使用的对象（available 队列中的对象将被销毁）。
        /// </summary>
        void ReleaseAllUnused();
    }
}
