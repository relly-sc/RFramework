namespace RFramework
{
    /// <summary>
    /// 资源辅助器的可选磁盘缓存能力。
    /// ResourceComponent 通过此接口向支持磁盘缓存的辅助器传递自动清理策略。
    /// </summary>
    public interface IResourceCacheHelper
    {
        /// <summary>
        /// 配置磁盘缓存自动清理策略。
        /// </summary>
        /// <param name="autoClearEnabled">是否在辅助器初始化期间自动检查并清理缓存。</param>
        /// <param name="maxCacheBytes">允许保留的最大缓存字节数。</param>
        void ConfigureCache(bool autoClearEnabled, long maxCacheBytes);

        /// <summary>
        /// 获取最近一次统计到的磁盘缓存字节数。
        /// 尚未统计或辅助器未初始化时返回 0。
        /// </summary>
        long CacheSizeBytes { get; }
    }
}
