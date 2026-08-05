namespace RFramework
{
    /// <summary>
    /// 框架支持的日志级别。
    /// </summary>
    public enum LogLevel : byte
    {
        /// <summary>普通运行信息。</summary>
        Info = 0,

        /// <summary>可恢复问题或降级提示。</summary>
        Warning = 1,

        /// <summary>需要处理的功能错误。</summary>
        Error = 2
    }
}
