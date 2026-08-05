using System.Collections.Generic;

namespace RFramework
{
    /// <summary>
    /// 网络模块接口。通道管理器，支持同时连接多个服务器。
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
        /// 获取当前网络通道数量。
        /// </summary>
        int ChannelCount { get; }

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

    }
}
