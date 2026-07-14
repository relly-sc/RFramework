namespace RFramework
{
    /// <summary>
    /// 场景卸载成功事件。由 SceneModule 在 UnloadSceneAsync 完成后分发。
    /// </summary>
    public readonly struct UnloadSceneSuccessEvent
    {
        /// <summary>
        /// 场景资源路径。
        /// </summary>
        public readonly string AssetName;

        /// <summary>
        /// 用户自定义数据。
        /// </summary>
        public readonly object UserData;

        /// <summary>
        /// 初始化场景卸载成功事件。
        /// </summary>
        /// <param name="assetName">场景资源路径。</param>
        /// <param name="userData">用户自定义数据。</param>
        public UnloadSceneSuccessEvent(string assetName, object userData)
        {
            AssetName = assetName;
            UserData = userData;
        }
    }
}
