using System;
using System.Runtime.Serialization;

namespace RFramework
{
    /// <summary>
    /// 表示框架检测到的配置、状态或生命周期错误。
    /// </summary>
    [Serializable]
    public sealed class RFrameworkException : Exception
    {
        /// <summary>
        /// 创建不带详细信息的框架异常。
        /// </summary>
        public RFrameworkException()
        {
        }

        /// <summary>
        /// 使用错误信息创建框架异常。
        /// </summary>
        /// <param name="message">错误信息。</param>
        public RFrameworkException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// 使用错误信息和原始异常创建框架异常。
        /// </summary>
        /// <param name="message">错误信息。</param>
        /// <param name="innerException">导致当前错误的原始异常。</param>
        public RFrameworkException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        private RFrameworkException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
