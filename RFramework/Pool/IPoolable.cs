namespace RFramework.Pool
{
    /// <summary>
    /// 可池化对象接口。
    /// 实现此接口的对象在 Spawn / Unspawn 时，
    /// 对象池会自动调用对应方法，无需额外传递 onSpawn / onUnspawn 回调。
    /// </summary>
    /// <remarks>
    /// 这是可选的便利接口。不实现此接口时，通过 CreatePool 的 onSpawn/onUnspawn 委托完成相同功能。
    /// </remarks>
    public interface IPoolable
    {
        /// <summary>
        /// 对象被从池中取出时调用。
        /// </summary>
        void OnSpawn();

        /// <summary>
        /// 对象被回收到池中时调用。
        /// </summary>
        void OnUnspawn();
    }
}
