namespace RFramework
{
    /// <summary>
    /// 实体加载上下文，记录异步加载中的实体信息。
    /// </summary>
    internal sealed class EntityLoadingInfo
    {
        /// <summary>
        /// 实体编号。
        /// </summary>
        public long EntityId { get; }

        /// <summary>
        /// 实体资源路径。
        /// </summary>
        public string AssetName { get; }

        /// <summary>
        /// 目标实体组。
        /// </summary>
        public EntityGroup Group { get; }

        /// <summary>
        /// 用户自定义数据。
        /// </summary>
        public object UserData { get; }

        /// <summary>
        /// 加载开始时间戳，用于计算加载持续时间。
        /// </summary>
        public readonly double StartTimestamp;

        /// <summary>
        /// 计算从开始到当前的加载耗时（秒）。
        /// </summary>
        public float ElapsedSeconds => (float)(System.DateTime.UtcNow.Ticks - StartTimestamp) / 10000000f;

        /// <summary>
        /// 初始化实体加载上下文。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <param name="assetName">实体资源路径。</param>
        /// <param name="group">目标实体组。</param>
        /// <param name="userData">用户自定义数据。</param>
        public EntityLoadingInfo(long entityId, string assetName, EntityGroup group, object userData)
        {
            EntityId = entityId;
            AssetName = assetName;
            Group = group;
            UserData = userData;
            StartTimestamp = System.DateTime.UtcNow.Ticks;
        }
    }
}
