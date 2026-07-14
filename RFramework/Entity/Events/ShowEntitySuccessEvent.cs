namespace RFramework
{
    /// <summary>
    /// 显示实体成功事件。
    /// 当 ShowEntityAsync 完成时由 EntityModule 通过 IEventModule 分发。
    /// 事件是额外通知——调用方已通过 Task 返回值获取 IEntity，此事件用于全局监听。
    /// </summary>
    public readonly struct ShowEntitySuccessEvent
    {
        /// <summary>
        /// 显示成功的实体。
        /// </summary>
        public readonly IEntity Entity;

        /// <summary>
        /// 从开始加载到完成的时间（秒）。
        /// </summary>
        public readonly float Duration;

        /// <summary>
        /// 用户自定义数据。
        /// </summary>
        public readonly object UserData;

        /// <summary>
        /// 初始化显示实体成功事件。
        /// </summary>
        /// <param name="entity">显示成功的实体。</param>
        /// <param name="duration">加载持续时间。</param>
        /// <param name="userData">用户自定义数据。</param>
        public ShowEntitySuccessEvent(IEntity entity, float duration, object userData)
        {
            Entity = entity;
            Duration = duration;
            UserData = userData;
        }
    }
}
