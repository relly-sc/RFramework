namespace RFramework.Scene
{
    /// <summary>
    /// 场景加载成功事件。由 SceneModule 在 LoadSceneAsync 完成后分发。
    /// </summary>
    public readonly struct LoadSceneSuccessEvent
    {
        /// <summary>
        /// 场景资源路径。
        /// </summary>
        public readonly string AssetName;

        /// <summary>
        /// 加载持续时间（秒）。
        /// </summary>
        public readonly float Duration;

        /// <summary>
        /// 用户自定义数据。
        /// </summary>
        public readonly object UserData;

        /// <summary>
        /// 初始化场景加载成功事件。
        /// </summary>
        /// <param name="assetName">场景资源路径。</param>
        /// <param name="duration">加载持续时间。</param>
        /// <param name="userData">用户自定义数据。</param>
        public LoadSceneSuccessEvent(string assetName, float duration, object userData)
        {
            AssetName = assetName;
            Duration = duration;
            UserData = userData;
        }
    }
}
