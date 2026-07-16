namespace RFramework
{
    /// <summary>
    /// 提供资源物理地址的可选能力接口。
    /// 适用于 VideoPlayer 等需要直接消费文件路径或 URL、而不是加载 UnityEngine.Object 的场景。
    /// </summary>
    public interface IResourceUrlProvider
    {
        /// <summary>
        /// 将逻辑资源位置转换为可直接访问的物理路径或 URL。
        /// </summary>
        /// <param name="location">资源辅助器可识别的逻辑位置。</param>
        /// <returns>目标平台可访问的文件路径或 URL。</returns>
        string GetAssetUrl(string location);
    }
}
