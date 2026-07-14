namespace RFramework
{
    /// <summary>
    /// 对象池内部基接口（非泛型），供 PoolModule 统一管理不同泛型的对象池。
    /// 外部不可见。
    /// </summary>
    internal interface IObjectPoolBase
    {
        /// <summary>
        /// 清空对象池。
        /// </summary>
        void Clear();

        /// <summary>
        /// 释放未使用对象。
        /// </summary>
        void ReleaseUnused();
    }
}
