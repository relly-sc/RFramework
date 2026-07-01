
namespace RFramework
{
    /// <summary>
    /// 框架日志等级
    /// </summary>
    public enum RFrameworkLogLevel : byte
    {

        /// <summary>
        /// 信息。
        /// </summary>
        Info = 0,

        /// <summary>
        /// 警告。
        /// </summary>
        Warning,

        /// <summary>
        /// 断言。
        /// </summary>
        Assert,

        /// <summary>
        /// 错误。
        /// </summary>
        Error,

        /// <summary>
        /// 异常。
        /// </summary>
        Exception
    }
}