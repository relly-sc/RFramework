using System;

namespace RFramework
{
    /// <summary>
    /// 资源加载失败事件。
    /// 当 LoadAssetAsync / LoadAssetSync 失败时由 ResourceModule 分发。
    /// 事件是额外通知，不替代异常——调用方仍需通过 try/catch 或 Task 异常处理加载失败。
    /// </summary>
    public readonly struct ResourceLoadFailedEvent
    {
        /// <summary>资源路径</summary>
        public readonly string Location;

        /// <summary>请求的资源类型</summary>
        public readonly Type AssetType;

        /// <summary>失败原因</summary>
        public readonly string ErrorMessage;

        /// <summary>
        /// 初始化资源加载失败事件。
        /// </summary>
        /// <param name="location">资源路径。</param>
        /// <param name="assetType">请求的资源类型。</param>
        /// <param name="errorMessage">失败原因。</param>
        public ResourceLoadFailedEvent(string location, Type assetType, string errorMessage)
        {
            Location = location;
            AssetType = assetType;
            ErrorMessage = errorMessage;
        }
    }
}
