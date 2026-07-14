namespace RFramework
{
    /// <summary>
    /// 显示实体失败事件。
    /// 当 ShowEntityAsync 失败时由 EntityModule 通过 IEventModule 分发。
    /// 事件是额外通知，不替代异常——调用方仍需通过 Task 异常处理加载失败。
    /// </summary>
    public readonly struct ShowEntityFailureEvent
    {
        /// <summary>
        /// 失败的实体编号。
        /// </summary>
        public readonly long EntityId;

        /// <summary>
        /// 实体资源路径。
        /// </summary>
        public readonly string AssetName;

        /// <summary>
        /// 失败原因。
        /// </summary>
        public readonly string ErrorMessage;

        /// <summary>
        /// 用户自定义数据。
        /// </summary>
        public readonly object UserData;

        /// <summary>
        /// 初始化显示实体失败事件。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <param name="assetName">实体资源路径。</param>
        /// <param name="errorMessage">失败原因。</param>
        /// <param name="userData">用户自定义数据。</param>
        public ShowEntityFailureEvent(long entityId, string assetName, string errorMessage, object userData)
        {
            EntityId = entityId;
            AssetName = assetName;
            ErrorMessage = errorMessage;
            UserData = userData;
        }
    }
}
