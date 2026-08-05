using System;
using System.Globalization;

namespace RFramework
{
    /// <summary>
    /// Library 层日志桥接入口。具体输出由 Runtime 安装的 <see cref="ILogSink"/> 提供。
    /// </summary>
    public static class RFrameworkLog
    {
        private static readonly object Gate = new object();
        private static ILogSink sink;

        /// <summary>
        /// 获取当前是否已安装日志接收器。
        /// </summary>
        public static bool IsInitialized
        {
            get
            {
                lock (Gate)
                {
                    return sink != null;
                }
            }
        }

        /// <summary>
        /// 安装日志接收器，并释放之前的接收器。
        /// </summary>
        /// <param name="value">新的日志接收器。</param>
        public static void SetSink(ILogSink value)
        {
            if (value == null)
            {
                throw new RFrameworkException("Log sink cannot be null.");
            }

            ILogSink previous;
            lock (Gate)
            {
                previous = sink;
                sink = value;
            }

            if (!ReferenceEquals(previous, value))
            {
                DisposeSink(previous);
            }
        }

        /// <summary>
        /// 移除并释放当前日志接收器。
        /// </summary>
        public static void Clear()
        {
            ILogSink previous;
            lock (Gate)
            {
                previous = sink;
                sink = null;
            }

            DisposeSink(previous);
        }

        /// <summary>
        /// 写入一条日志；未安装接收器时抛出框架异常。
        /// </summary>
        /// <param name="level">日志级别。</param>
        /// <param name="message">日志内容。</param>
        public static void Write(LogLevel level, object message)
        {
            GetRequiredSink().Write(level, message?.ToString() ?? "null");
        }

        /// <summary>
        /// 格式化并写入一条日志；未安装接收器时抛出框架异常。
        /// </summary>
        /// <param name="level">日志级别。</param>
        /// <param name="format">复合格式字符串。</param>
        /// <param name="args">格式化参数。</param>
        public static void Write(LogLevel level, string format, params object[] args)
        {
            if (format == null)
            {
                throw new RFrameworkException("Log format cannot be null.");
            }

            GetRequiredSink().Write(
                level, string.Format(CultureInfo.InvariantCulture, format, args));
        }

        /// <summary>
        /// 尝试写入日志。框架尚未启动、已经关闭或接收器失败时返回 false。
        /// </summary>
        /// <param name="level">日志级别。</param>
        /// <param name="message">日志内容。</param>
        /// <returns>成功交给接收器时返回 true。</returns>
        public static bool TryWrite(LogLevel level, object message)
        {
            return TryWriteCore(level, message?.ToString() ?? "null");
        }

        /// <summary>
        /// 尝试格式化并写入日志。
        /// </summary>
        /// <param name="level">日志级别。</param>
        /// <param name="format">复合格式字符串。</param>
        /// <param name="args">格式化参数。</param>
        /// <returns>成功交给接收器时返回 true。</returns>
        public static bool TryWrite(LogLevel level, string format, params object[] args)
        {
            if (format == null)
            {
                return false;
            }

            string message;
            try
            {
                message = string.Format(CultureInfo.InvariantCulture, format, args);
            }
            catch (FormatException)
            {
                return false;
            }

            return TryWriteCore(level, message);
        }

        private static ILogSink GetRequiredSink()
        {
            lock (Gate)
            {
                return sink ?? throw new RFrameworkException(
                    "No log sink is installed. Initialize the framework before writing logs.");
            }
        }

        private static bool TryWriteCore(LogLevel level, string message)
        {
            ILogSink current;
            lock (Gate)
            {
                current = sink;
            }

            if (current == null)
            {
                return false;
            }

            try
            {
                current.Write(level, message);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void DisposeSink(ILogSink value)
        {
            if (value == null)
            {
                return;
            }

            try
            {
                value.Dispose();
            }
            catch
            {
                // 日志后端关闭失败不能阻止框架继续替换或清理全局接收器。
            }
        }
    }
}
