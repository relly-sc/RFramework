using System;

namespace RFramework
{
    /// <summary>
    /// 场景加载失败事件。
    /// 当 LoadSceneAsync 失败时由 ResourceModule 分发。
    /// 事件是额外通知，不替代异常——调用方仍需通过 try/catch 处理加载失败。
    /// </summary>
    public readonly struct SceneLoadFailedEvent
    {
        /// <summary>场景资源路径</summary>
        public readonly string Location;

        /// <summary>失败原因</summary>
        public readonly string ErrorMessage;

        /// <summary>
        /// 初始化场景加载失败事件。
        /// </summary>
        /// <param name="location">场景资源路径。</param>
        /// <param name="errorMessage">失败原因。</param>
        public SceneLoadFailedEvent(string location, string errorMessage)
        {
            Location = location;
            ErrorMessage = errorMessage;
        }
    }
}
