using System;

namespace RFramework
{
    /// <summary>
    /// 配置加载失败事件。
    /// 当 LoadConfig 失败时由 ConfigModule 分发。
    /// 事件是额外通知，不替代异常——调用方仍需通过 try/catch 处理加载失败。
    /// </summary>
    public readonly struct ConfigLoadFailedEvent
    {
        /// <summary>
        /// 配置行类型。
        /// </summary>
        public readonly Type ConfigType;

        /// <summary>
        /// 失败原因。
        /// </summary>
        public readonly string ErrorMessage;

        /// <summary>
        /// 初始化配置加载失败事件。
        /// </summary>
        /// <param name="configType">配置行类型。</param>
        /// <param name="errorMessage">失败原因。</param>
        public ConfigLoadFailedEvent(Type configType, string errorMessage)
        {
            ConfigType = configType;
            ErrorMessage = errorMessage;
        }
    }
}
