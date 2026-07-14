namespace RFramework
{
    /// <summary>
    /// 场景加载失败事件。由 SceneModule 在 LoadSceneAsync 失败时分发。
    /// </summary>
    public readonly struct LoadSceneFailureEvent
    {
        /// <summary>
        /// 场景资源路径。
        /// </summary>
        public readonly string AssetName;

        /// <summary>
        /// 错误信息。
        /// </summary>
        public readonly string ErrorMessage;

        /// <summary>
        /// 用户自定义数据。
        /// </summary>
        public readonly object UserData;

        /// <summary>
        /// 初始化场景加载失败事件。
        /// </summary>
        /// <param name="assetName">场景资源路径。</param>
        /// <param name="errorMessage">错误信息。</param>
        /// <param name="userData">用户自定义数据。</param>
        public LoadSceneFailureEvent(string assetName, string errorMessage, object userData)
        {
            AssetName = assetName;
            ErrorMessage = errorMessage;
            UserData = userData;
        }
    }
}
