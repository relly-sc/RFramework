namespace RFramework.Network
{
    /// <summary>
    /// 网络连接成功事件。包含通道名称，用于区分不同服务器的连接。
    /// </summary>
    public readonly struct NetworkConnectedEvent
    {
        /// <summary>
        /// 通道名称（如 "Login"、"Chat"）。
        /// </summary>
        public readonly string ChannelName;

        /// <summary>
        /// 构造网络连接成功事件。
        /// </summary>
        /// <param name="channelName">通道名称。</param>
        public NetworkConnectedEvent(string channelName)
        {
            ChannelName = channelName;
        }
    }
}
