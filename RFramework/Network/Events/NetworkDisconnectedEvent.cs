namespace RFramework.Network
{
    /// <summary>
    /// 网络断开事件。包含通道名称，用于区分不同服务器的断开。
    /// </summary>
    public readonly struct NetworkDisconnectedEvent
    {
        /// <summary>
        /// 通道名称（如 "Login"、"Chat"）。
        /// </summary>
        public readonly string ChannelName;

        /// <summary>
        /// 构造网络断开事件。
        /// </summary>
        /// <param name="channelName">通道名称。</param>
        public NetworkDisconnectedEvent(string channelName)
        {
            ChannelName = channelName;
        }
    }
}
