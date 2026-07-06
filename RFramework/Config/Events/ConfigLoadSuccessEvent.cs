using System;

namespace RFramework.Config
{
    /// <summary>
    /// 配置加载成功事件。
    /// 当 LoadConfig 成功完成时由 ConfigModule 分发。
    /// </summary>
    public readonly struct ConfigLoadSuccessEvent
    {
        /// <summary>
        /// 配置行类型。
        /// </summary>
        public readonly Type ConfigType;

        /// <summary>
        /// 加载的配置行数量。
        /// </summary>
        public readonly int RowCount;

        /// <summary>
        /// 初始化配置加载成功事件。
        /// </summary>
        /// <param name="configType">配置行类型。</param>
        /// <param name="rowCount">加载的配置行数量。</param>
        public ConfigLoadSuccessEvent(Type configType, int rowCount)
        {
            ConfigType = configType;
            RowCount = rowCount;
        }
    }
}
