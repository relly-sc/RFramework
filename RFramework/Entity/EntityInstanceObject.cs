namespace RFramework.Entity
{
    /// <summary>
    /// 实体实例对象，包装一个实例化的实体及其原始资源引用。
    /// 用于对象池管理：Spawn 时取出，Unspawn 时归还，Release 时真正销毁。
    /// </summary>
    internal sealed class EntityInstanceObject
    {
        /// <summary>
        /// 实体资源路径（用作对象池的查找键）。
        /// </summary>
        public string AssetName { get; }

        /// <summary>
        /// 原始实体资源对象（Unity 层为 GameObject prefab）。
        /// </summary>
        public object EntityAsset { get; }

        /// <summary>
        /// 实例化后的对象（Unity 层为 GameObject 实例）。
        /// </summary>
        public object Target { get; }

        /// <summary>
        /// 最后一次被使用的时间戳（UtcTicks）。
        /// Spawn 和 Register 时更新，用于对象池过期判定。
        /// </summary>
        public double LastUseTimestamp { get; set; }

        /// <summary>
        /// 实体辅助器引用，用于 Release 时调用 ReleaseEntity。
        /// </summary>
        private readonly IEntityHelper entityHelper;

        /// <summary>
        /// 初始化实体实例对象。
        /// </summary>
        /// <param name="assetName">实体资源路径。</param>
        /// <param name="entityAsset">原始资源对象。</param>
        /// <param name="target">实例化后的对象。</param>
        /// <param name="entityHelper">实体辅助器。</param>
        public EntityInstanceObject(string assetName, object entityAsset, object target, IEntityHelper entityHelper)
        {
            AssetName = assetName;
            EntityAsset = entityAsset;
            Target = target;
            this.entityHelper = entityHelper;
            LastUseTimestamp = System.DateTime.UtcNow.Ticks;
        }

        /// <summary>
        /// 判断此实例对象是否已超过指定过期时间。
        /// </summary>
        /// <param name="expireTime">过期时间（秒）。</param>
        /// <returns>是否已过期。</returns>
        public bool IsExpired(float expireTime)
        {
            double elapsedSeconds = (System.DateTime.UtcNow.Ticks - LastUseTimestamp) / 10000000d;
            return elapsedSeconds >= expireTime;
        }

        /// <summary>
        /// 释放实体实例（真正销毁 GameObject + UnloadAsset）。
        /// 由 EntityGroup.Destroy 或对象池容量溢出时调用。
        /// </summary>
        public void Release()
        {
            if (entityHelper != null)
            {
                entityHelper.ReleaseEntity(EntityAsset, Target);
            }
        }
    }
}
