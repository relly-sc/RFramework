
namespace RFramework
{
    /// <summary>
    /// 框架日志类
    /// </summary>
    public static partial class RFrameworkLog
    {
        private static ILogHelper logHelper;

        /// <summary>
        /// 设置游戏框架日志辅助器。
        /// </summary>
        /// <param name="_logHelper">要设置的游戏框架日志辅助器。</param>
        public static void SetLogHelper(ILogHelper _logHelper)
        {
            RFrameworkLog.logHelper = _logHelper;
        }

        /// <summary>
        /// 打印信息级别日志，用于记录程序正常运行日志信息。
        /// </summary>
        /// <param name="message">日志内容</param>
        public static void Info(object message)
        {
            if (logHelper == null)
            {
                return;
            }

            logHelper.Log(RFrameworkLogLevel.Info, message);
        }

        /// <summary>
        /// 打印信息级别日志，用于记录程序正常运行日志信息。
        /// </summary>
        /// <param name="message">日志内容</param>
        public static void Info(string message)
        {
            if (logHelper == null)
            {
                return;
            }

            logHelper.Log(RFrameworkLogLevel.Info, message);
        }

        /// <summary>
        /// 打印信息级别日志，用于记录程序正常运行日志信息。
        /// </summary>
        /// <param name="format">日志格式。</param>
        /// <param name="args">日志参数。</param>
        public static void Info(string format, params object[] args)
        {
            if (logHelper == null)
            {
                return;
            }

            logHelper.Log(RFrameworkLogLevel.Info, Utility.Text.Format(format, args));
        }

        /// <summary>
        /// 打印警告级别日志，建议在发生局部功能逻辑错误，但尚不会导致游戏崩溃或异常时使用。
        /// </summary>
        /// <param name="message">日志内容。</param>
        public static void Warning(object message)
        {
            if (logHelper == null)
            {
                return;
            }

            logHelper.Log(RFrameworkLogLevel.Warning, message);
        }

        /// <summary>
        /// 打印警告级别日志，建议在发生局部功能逻辑错误，但尚不会导致游戏崩溃或异常时使用。
        /// </summary>
        /// <param name="message">日志内容。</param>
        public static void Warning(string message)
        {
            if (logHelper == null)
            {
                return;
            }

            logHelper.Log(RFrameworkLogLevel.Warning, message);
        }

        /// <summary>
        /// 打印警告级别日志，建议在发生局部功能逻辑错误，但尚不会导致游戏崩溃或异常时使用。
        /// </summary>
        /// <param name="format">日志格式。</param>
        /// <param name="args">日志参数。</param>
        public static void Warning(string format, params object[] args)
        {
            if (logHelper == null)
            {
                return;
            }

            logHelper.Log(RFrameworkLogLevel.Warning, Utility.Text.Format(format, args));
        }

        /// <summary>
        /// 打印错误级别日志，建议在发生功能逻辑错误，但尚不会导致游戏崩溃或异常时使用。
        /// </summary>
        /// <param name="message">日志内容。</param>
        public static void Error(object message)
        {
            if (logHelper == null)
            {
                return;
            }

            logHelper.Log(RFrameworkLogLevel.Error, message);
        }

        /// <summary>
        /// 打印错误级别日志，建议在发生功能逻辑错误，但尚不会导致游戏崩溃或异常时使用。
        /// </summary>
        /// <param name="message">日志内容。</param>
        public static void Error(string message)
        {
            if (logHelper == null)
            {
                return;
            }

            logHelper.Log(RFrameworkLogLevel.Error, message);
        }

        /// <summary>
        /// 打印错误级别日志，建议在发生功能逻辑错误，但尚不会导致游戏崩溃或异常时使用。
        /// </summary>
        /// <param name="format">日志格式。</param>
        /// <param name="args">日志参数。</param>
        public static void Error(string format, params object[] args)
        {
            if (logHelper == null)
            {
                return;
            }

            logHelper.Log(RFrameworkLogLevel.Error, Utility.Text.Format(format, args));
        }
    }
}