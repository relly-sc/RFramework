namespace RFramework.Resource
{
    /// <summary>
    /// 资源运行模式
    /// </summary>
    public enum ResourcePlayMode
    {
        /// <summary>
        /// 编辑器模拟模式：开发阶段使用编辑器内资源，无需构建资源包。
        /// </summary>
        EditorSimulate = 0,

        /// <summary>
        /// 离线模式：资源全部内置在本地包体中，不连接远程服务器。
        /// </summary>
        Offline = 1,

        /// <summary>
        /// 联机模式：内置资源 + 远程 CDN 更新 + 本地缓存
        /// </summary>
        Host = 2
    }

    /// <summary>
    /// 场景加载模式。与常见引擎的加载模式值保持一致，Runtime 辅助器负责映射到底层引擎。
    /// </summary>
    public enum SceneLoadMode
    {
        /// <summary>替换当前场景</summary>
        Single = 0,

        /// <summary>叠加到当前场景</summary>
        Additive = 1
    }
}
