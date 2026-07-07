using System;
using System.Threading;
using System.Threading.Tasks;
using RFramework.Event;
using RFramework.Timer;

namespace RFramework.Network
{
    /// <summary>
    /// 网络模块接口。提供连接管理、心跳、自动重连、消息收发。
    /// 单连接模式，适配 XR/虚拟仿真常见场景。
    /// </summary>
    public interface INetworkModule
    {
        /// <summary>
        /// 是否已连接。
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 心跳间隔（秒）。0 表示不发送心跳。
        /// </summary>
        float HeartbeatInterval { get; set; }

        /// <summary>
        /// 是否启用自动重连。
        /// </summary>
        bool AutoReconnect { get; set; }

        /// <summary>
        /// 重连间隔（秒）。
        /// </summary>
        float ReconnectInterval { get; set; }

        /// <summary>
        /// 设置依赖模块引用。
        /// </summary>
        /// <param name="eventModule">事件模块，用于分发连接/断开/错误事件。</param>
        /// <param name="timerModule">计时器模块，用于心跳和重连。</param>
        void SetDependencies(IEventModule eventModule, ITimerModule timerModule);

        /// <summary>
        /// 设置网络辅助器。
        /// </summary>
        /// <param name="helper">网络辅助器实例。</param>
        void SetHelper(INetworkHelper helper);

        /// <summary>
        /// 异步连接服务器。
        /// </summary>
        /// <param name="ip">目标 IP 地址。</param>
        /// <param name="port">目标端口。</param>
        /// <param name="ct">取消令牌。</param>
        Task ConnectAsync(string ip, int port, CancellationToken ct = default);

        /// <summary>
        /// 断开连接。
        /// </summary>
        void Disconnect();

        /// <summary>
        /// 发送原始数据。
        /// </summary>
        /// <param name="msgId">消息 ID。</param>
        /// <param name="body">消息体字节数据。</param>
        void Send(int msgId, byte[] body);

        /// <summary>
        /// 注册消息处理器。
        /// </summary>
        /// <param name="msgId">消息 ID。</param>
        /// <param name="handler">处理函数。</param>
        void RegisterHandler(int msgId, Action<byte[]> handler);

        /// <summary>
        /// 注销消息处理器。
        /// </summary>
        /// <param name="msgId">消息 ID。</param>
        void UnregisterHandler(int msgId);
    }
}
