namespace RFramework.Network
{
    /// <summary>
    /// 网络错误事件。包含错误描述信息。
    /// </summary>
    public readonly struct NetworkErrorEvent
    {
        /// <summary>
        /// 错误描述。
        /// </summary>
        public readonly string Message;

        /// <summary>
        /// 构造网络错误事件。
        /// </summary>
        /// <param name="message">错误描述。</param>
        public NetworkErrorEvent(string message)
        {
            Message = message;
        }
    }
}
