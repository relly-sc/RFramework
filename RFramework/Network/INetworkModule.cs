using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RFramework.Event;
using RFramework.Timer;

namespace RFramework.Network
{
    /// <summary>
    /// 网络模块接口。通道管理器，支持同时连接多个服务器。
    /// 保留默认通道的单连接 API 以向后兼容。
    /// </summary>
    /// <remarks>
    /// 多服务器示例：
    /// <code>
    /// var login = networkModule.CreateChannel("Login");
    /// login.RegisterHandler(1001, OnLoginResponse);
    /// await login.ConnectAsync("127.0.0.1", 9001);
    ///
    /// var chat = networkModule.CreateChannel("Chat");
    /// await chat.ConnectAsync("127.0.0.1", 9002);
    ///
    /// chat.Send(2001, chatMsgBytes);
    /// </code>
    /// 单服务器（兼容旧代码）：
    /// <code>
    /// networkModule.HeartbeatInterval = 10f;
    /// await networkModule.ConnectAsync("127.0.0.1", 9000);
    /// networkModule.Send(1001, bytes);
    /// </code>
    /// </remarks>
    public interface INetworkModule
    {
        // ====== 多通道管理 ======

        /// <summary>
        /// 创建网络通道。同名通道会返回已有实例。
        /// </summary>
        /// <param name="name">通道名称（如 "Login"、"Chat"），全局唯一。</param>
        /// <returns>通道实例。</returns>
        INetworkChannel CreateChannel(string name);

        /// <summary>
        /// 获取已存在的通道。
        /// </summary>
        /// <param name="name">通道名称。</param>
        /// <returns>通道实例，不存在时返回 null。</returns>
        INetworkChannel GetChannel(string name);

        /// <summary>
        /// 是否存在指定名称的通道。
        /// </summary>
        /// <param name="name">通道名称。</param>
        bool HasChannel(string name);

        /// <summary>
        /// 移除并关闭通道。
        /// </summary>
        /// <param name="name">通道名称。</param>
        /// <returns>是否找到并移除成功。</returns>
        bool RemoveChannel(string name);

        /// <summary>
        /// 获取所有通道列表（只读）。
        /// </summary>
        IReadOnlyList<INetworkChannel> GetAllChannels();

        /// <summary>
        /// 获取默认通道（第一个创建的通道）。
        /// 未创建任何通道时返回 null。
        /// </summary>
        INetworkChannel DefaultChannel { get; }

        // ====== 依赖注入 ======

        /// <summary>
        /// 设置依赖模块引用。
        /// </summary>
        /// <param name="eventModule">事件模块，用于分发连接/断开/错误事件。</param>
        /// <param name="timerModule">计时器模块，用于心跳和重连。</param>
        void SetDependencies(IEventModule eventModule, ITimerModule timerModule);

        // ====== 向后兼容：默认通道单连接 API ======

        /// <summary>
        /// 默认通道是否已连接。
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 默认通道心跳间隔（秒）。0 表示不发送心跳。
        /// </summary>
        float HeartbeatInterval { get; set; }

        /// <summary>
        /// 默认通道是否启用自动重连。
        /// </summary>
        bool AutoReconnect { get; set; }

        /// <summary>
        /// 默认通道重连间隔（秒）。
        /// </summary>
        float ReconnectInterval { get; set; }

        /// <summary>
        /// 设置默认通道的网络辅助器。
        /// </summary>
        /// <param name="helper">网络辅助器实例。</param>
        void SetHelper(INetworkHelper helper);

        /// <summary>
        /// 默认通道异步连接服务器。
        /// </summary>
        /// <param name="ip">目标 IP 地址。</param>
        /// <param name="port">目标端口。</param>
        /// <param name="ct">取消令牌。</param>
        Task ConnectAsync(string ip, int port, CancellationToken ct = default);

        /// <summary>
        /// 默认通道断开连接。
        /// </summary>
        void Disconnect();

        /// <summary>
        /// 默认通道发送原始数据。
        /// </summary>
        /// <param name="msgId">消息 ID。</param>
        /// <param name="body">消息体字节数据。</param>
        void Send(int msgId, byte[] body);

        /// <summary>
        /// 默认通道注册消息处理器。
        /// </summary>
        /// <param name="msgId">消息 ID。</param>
        /// <param name="handler">处理函数。</param>
        void RegisterHandler(int msgId, Action<byte[]> handler);

        /// <summary>
        /// 默认通道注销消息处理器。
        /// </summary>
        /// <param name="msgId">消息 ID。</param>
        void UnregisterHandler(int msgId);
    }
}
