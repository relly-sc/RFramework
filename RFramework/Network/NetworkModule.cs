using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RFramework.Event;
using RFramework.Timer;

namespace RFramework.Network
{
    /// <summary>
    /// 网络模块核心实现。作为通道管理器，支持同时管理多个服务器连接。
    /// 保留默认通道的单连接 API 以向后兼容。
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
        internal override int Priority
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

        // ====== 向后兼容：默认通道代理 ======

        /// <inheritdoc/>
        public bool IsConnected
        {
            get { return defaultChannel != null && defaultChannel.IsConnected; }
        }

        /// <inheritdoc/>
        public float HeartbeatInterval
        {
            get { return defaultChannel != null ? defaultChannel.HeartbeatInterval : 0f; }
            set
            {
                if (defaultChannel != null)
                {
                    defaultChannel.HeartbeatInterval = value;
                }
            }
        }

        /// <inheritdoc/>
        public bool AutoReconnect
        {
            get { return defaultChannel != null && defaultChannel.AutoReconnect; }
            set
            {
                if (defaultChannel != null)
                {
                    defaultChannel.AutoReconnect = value;
                }
            }
        }

        /// <inheritdoc/>
        public float ReconnectInterval
        {
            get { return defaultChannel != null ? defaultChannel.ReconnectInterval : 0f; }
            set
            {
                if (defaultChannel != null)
                {
                    defaultChannel.ReconnectInterval = value;
                }
            }
        }

        /// <inheritdoc/>
        public void SetHelper(INetworkHelper helper)
        {
            EnsureDefaultChannel();
            defaultChannel.SetHelper(helper);
        }

        /// <inheritdoc/>
        public Task ConnectAsync(string ip, int port, System.Threading.CancellationToken ct = default)
        {
            EnsureDefaultChannel();
            return defaultChannel.ConnectAsync(ip, port, ct);
        }

        /// <inheritdoc/>
        public void Disconnect()
        {
            defaultChannel?.Disconnect();
        }

        /// <inheritdoc/>
        public void Send(int msgId, byte[] body)
        {
            if (defaultChannel == null || !defaultChannel.IsConnected)
            {
                throw new RFrameworkException("Default channel is not connected. Cannot send message.");
            }

            defaultChannel.Send(msgId, body);
        }

        /// <inheritdoc/>
        public void RegisterHandler(int msgId, Action<byte[]> handler)
        {
            EnsureDefaultChannel();
            defaultChannel.RegisterHandler(msgId, handler);
        }

        /// <inheritdoc/>
        public void UnregisterHandler(int msgId)
        {
            defaultChannel?.UnregisterHandler(msgId);
        }

        // ====== 生命周期 ======

        /// <inheritdoc/>
        internal override void Update(float elapseSeconds, float realElapseSeconds)
        {
            for (int i = 0; i < channelList.Count; i++)
            {
                channelList[i].Update(elapseSeconds, realElapseSeconds);
            }
        }

        /// <inheritdoc/>
        internal override void Shutdown()
        {
            foreach (NetworkChannel channel in channelList)
            {
                channel.Shutdown();
            }

            channels.Clear();
            channelList.Clear();
            defaultChannel = null;
        }

        // ====== 内部方法 ======

        /// <summary>
        /// 确保默认通道存在。如果尚未创建任何通道，自动创建名称为 "Default" 的默认通道。
        /// </summary>
        private void EnsureDefaultChannel()
        {
            if (defaultChannel == null)
            {
                CreateChannel("Default");
            }
        }
    }
}
