using System;
using System.Threading;
using System.Threading.Tasks;

namespace RFramework
{
    /// <summary>
    /// 网络通道接口。每个通道代表与一个服务器的独立连接，
    /// 拥有独立的 Helper、心跳、重连和消息处理器。
    /// </summary>
    /// <remarks>
    /// 典型用法：
    /// <code>
    /// var loginChannel = networkModule.CreateChannel("Login");
    /// loginChannel.HeartbeatInterval = 10f;
    /// loginChannel.RegisterHandler(1001, OnLoginResponse);
    /// await loginChannel.ConnectAsync("127.0.0.1", 9001);
    ///
    /// var chatChannel = networkModule.CreateChannel("Chat");
    /// await chatChannel.ConnectAsync("127.0.0.1", 9002);
    /// </code>
    /// </remarks>
    public interface INetworkChannel
    {
        /// <summary>
        /// 通道名称，全局唯一。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 是否已连接。
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 心跳间隔（秒）。设为 0 或负数表示不发送心跳。
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
        /// 当前连接的 IP 地址。
        /// </summary>
        string CurrentIP { get; }

        /// <summary>
        /// 当前连接的端口。
        /// </summary>
        int CurrentPort { get; }

        /// <summary>
        /// 设置网络辅助器。必须在 ConnectAsync 之前调用。
        /// </summary>
        /// <param name="helper">网络辅助器实例。</param>
        void SetHelper(INetworkHelper helper);

        /// <summary>
        /// 异步连接服务器。
        /// </summary>
        /// <param name="ip">目标 IP 地址。</param>
        /// <param name="port">目标端口。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>连接成功的 Task。</returns>
        Task ConnectAsync(string ip, int port, CancellationToken ct = default);

        /// <summary>
        /// 断开连接（不再自动重连）。
        /// </summary>
        void Disconnect();

        /// <summary>
        /// 发送消息。
        /// </summary>
        /// <param name="msgId">消息 ID。</param>
        /// <param name="body">消息体字节数据。</param>
        void Send(int msgId, byte[] body);

        /// <summary>
        /// 注册消息处理器。
        /// </summary>
        /// <param name="msgId">消息 ID。</param>
        /// <param name="handler">处理函数，参数为消息体字节数据。</param>
        void RegisterHandler(int msgId, Action<byte[]> handler);

        /// <summary>
        /// 注销消息处理器。
        /// </summary>
        /// <param name="msgId">消息 ID。</param>
        void UnregisterHandler(int msgId);

        /// <summary>
        /// 每帧轮询，驱动 Helper.Update()。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间。</param>
        /// <param name="realElapseSeconds">真实流逝时间。</param>
        void Update(float elapseSeconds, float realElapseSeconds);

        /// <summary>
        /// 关闭通道，释放所有资源。
        /// </summary>
        void Shutdown();
    }
}
