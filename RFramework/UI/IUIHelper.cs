namespace RFramework.UI
{
    /// <summary>
    /// UI 辅助器接口，封装引擎特定的 UI 创建与销毁操作。
    /// Library 层通过此接口与 Runtime 层解耦。
    /// </summary>
    public interface IUIHelper
    {
        /// <summary>
        /// 使用加载好的 UI 资源实例化 UI 对象。
        /// </summary>
        /// <param name="uiAsset">已加载的 UI 资源（Unity 层为 GameObject prefab）。</param>
        /// <returns>实例化后的对象（Unity 层为 GameObject 实例）。</returns>
        object InstantiateUI(object uiAsset);

        /// <summary>
        /// 为实例化对象创建 IUIForm 包装组件。
        /// </summary>
        /// <param name="uiInstance">实例化后的 UI 对象。</param>
        /// <param name="assetName">UI 资源路径。</param>
        /// <param name="windowLayer">窗口层级。</param>
        /// <param name="fullScreen">是否全屏。</param>
        /// <returns>IUIForm 包装实例。</returns>
        IUIForm CreateUIForm(object uiInstance, string assetName, int windowLayer, bool fullScreen);

        /// <summary>
        /// 释放 UI 实例对象。
        /// </summary>
        /// <param name="uiInstance">UI 实例对象。</param>
        void ReleaseUI(object uiInstance);
    }
}
