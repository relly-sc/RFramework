using System.Collections.Generic;

namespace RFramework
{
    /// <summary>
    /// 网络模块核心实现。作为通道管理器，支持同时管理多个服务器连接。
    /// </summary>
    internal sealed class NetworkModule : RFrameworkModule, INetworkModule
    {
        /// <summary>
        /// 事件模块引用。
        /// </summary>
        private IEventModule eventModule;

        /// <summary>
        /// 计时器模块引用。
        /// </summary>
        private ITimerModule timerModule;

        /// <summary>
        /// 所有通道字典：名称 → 通道。
        /// </summary>
        private readonly Dictionary<string, NetworkChannel> channels = new Dictionary<string, NetworkChannel>();

        /// <summary>
        /// 所有通道列表（用于轮询 Update）。
        /// </summary>
        private readonly List<NetworkChannel> channelList = new List<NetworkChannel>();

        /// <summary>
        /// 默认通道（第一个创建的通道）。
        /// </summary>
        private NetworkChannel defaultChannel;

        /// <inheritdoc/>
        internal override int Order
        {
            get
            {
                return 0;
            }
        }

        // ====== 多通道管理 ======

        /// <inheritdoc/>
        public INetworkChannel CreateChannel(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new RFrameworkException("Channel name is invalid.");
            }

            if (channels.TryGetValue(name, out NetworkChannel existing))
            {
                return existing;
            }

            NetworkChannel channel = new NetworkChannel(name, eventModule, timerModule);
            channels[name] = channel;
            channelList.Add(channel);

            if (defaultChannel == null)
            {
                defaultChannel = channel;
            }

            return channel;
        }

        /// <inheritdoc/>
        public INetworkChannel GetChannel(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            channels.TryGetValue(name, out NetworkChannel channel);
            return channel;
        }

        /// <inheritdoc/>
        public bool HasChannel(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            return channels.ContainsKey(name);
        }

        /// <inheritdoc/>
        public bool RemoveChannel(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            if (channels.TryGetValue(name, out NetworkChannel channel))
            {
                channel.Shutdown();
                channels.Remove(name);
                channelList.Remove(channel);

                if (defaultChannel == channel)
                {
                    defaultChannel = channelList.Count > 0 ? channelList[0] : null;
                }

                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        public IReadOnlyList<INetworkChannel> GetAllChannels()
        {
            return channelList.AsReadOnly();
        }

        /// <inheritdoc/>
        public int ChannelCount
        {
            get { return channelList.Count; }
        }

        /// <inheritdoc/>
        public INetworkChannel DefaultChannel
        {
            get { return defaultChannel; }
        }

        // ====== 依赖注入 ======

        /// <inheritdoc/>
        public void SetDependencies(IEventModule eventModule, ITimerModule timerModule)
        {
            this.eventModule = eventModule;
            this.timerModule = timerModule;
        }

        // ====== 生命周期 ======

        /// <inheritdoc/>
        internal override void Tick(float elapseSeconds, float realElapseSeconds)
        {
            for (int i = 0; i < channelList.Count; i++)
            {
                channelList[i].Update(elapseSeconds, realElapseSeconds);
            }
        }

        /// <inheritdoc/>
        internal override void Stop()
        {
            foreach (NetworkChannel channel in channelList)
            {
                channel.Shutdown();
            }

            channels.Clear();
            channelList.Clear();
            defaultChannel = null;
        }

    }
}
