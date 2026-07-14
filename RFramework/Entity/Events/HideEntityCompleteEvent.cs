namespace RFramework
{
    /// <summary>
    /// 隐藏实体完成事件。
    /// 当 HideEntity 完成时由 EntityModule 通过 IEventModule 分发。
    /// </summary>
    public readonly struct HideEntityCompleteEvent
    {
        /// <summary>
        /// 隐藏完成的实体编号。
        /// </summary>
        public readonly long EntityId;

        /// <summary>
        /// 实体资源路径。
        /// </summary>
        public readonly string AssetName;

        /// <summary>
        /// 实体所属组名称。
        /// </summary>
        public readonly string GroupName;

        /// <summary>
        /// 用户自定义数据。
        /// </summary>
        public readonly object UserData;

        /// <summary>
        /// 初始化隐藏实体完成事件。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <param name="assetName">实体资源路径。</param>
        /// <param name="groupName">实体所属组名称。</param>
        /// <param name="userData">用户自定义数据。</param>
        public HideEntityCompleteEvent(long entityId, string assetName, string groupName, object userData)
        {
            EntityId = entityId;
            AssetName = assetName;
            GroupName = groupName;
            UserData = userData;
        }
    }
}
