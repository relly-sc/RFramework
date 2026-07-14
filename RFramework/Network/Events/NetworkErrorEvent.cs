namespace RFramework
{
    /// <summary>
    /// 网络错误事件。包含通道名称和错误描述信息。
    /// </summary>
    public readonly struct NetworkErrorEvent
    {
        /// <summary>
        /// 通道名称（如 "Login"、"Chat"）。
        /// </summary>
        public readonly string ChannelName;

        /// <summary>
        /// 错误描述。
        /// </summary>
        public readonly string Message;

        /// <summary>
        /// 构造网络错误事件。
        /// </summary>
        /// <param name="channelName">通道名称。</param>
        /// <param name="message">错误描述。</param>
        public NetworkErrorEvent(string channelName, string message)
        {
            ChannelName = channelName;
            Message = message;
        }
    }
}
