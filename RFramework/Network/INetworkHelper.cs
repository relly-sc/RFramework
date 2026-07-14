namespace RFramework
{
    /// <summary>
    /// 网络辅助器接口。负责底层传输实现（TCP/WebSocket/KCP），
    /// 以及粘包编解码。由 Expansion 层提供具体实现。
    /// </summary>
    public interface INetworkHelper
    {
        /// <summary>
        /// 建立连接。
        /// </summary>
        /// <param name="ip">目标 IP 地址。</param>
        /// <param name="port">目标端口。</param>
        void Connect(string ip, int port);

        /// <summary>
        /// 断开连接。
        /// </summary>
        void Disconnect();

        /// <summary>
        /// 发送消息。由具体 Helper 按各自帧协议封装后发出：
        /// TCP = [4B 包长][4B msgId][body]，UDP/WebSocket = [4B msgId][body]。
        /// 帧协议属于各 Helper（见框架设计文档），通道只透传 msgId 与 body。
        /// </summary>
        /// <param name="msgId">消息 ID。</param>
        /// <param name="body">消息体字节数据（可为 null 或空数组）。</param>
        void Send(int msgId, byte[] body);

        /// <summary>
        /// 每帧轮询，检查收包并解码。收到完整包时触发 OnReceive 回调。
        /// </summary>
        void Update();

        /// <summary>
        /// 是否已连接。
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 收包回调。参数：(msgId, body)。
        /// 由 NetworkModule 绑定，Helper 收到完整包后调用。
        /// </summary>
        System.Action<int, byte[]> OnReceive { get; set; }

        /// <summary>
        /// 连接成功回调。由 NetworkModule 绑定。
        /// </summary>
        System.Action OnConnected { get; set; }

        /// <summary>
        /// 连接断开回调。由 NetworkModule 绑定。
        /// </summary>
        System.Action OnDisconnected { get; set; }

        /// <summary>
        /// 错误回调。参数：(错误信息)。
        /// 由 NetworkModule 绑定。
        /// </summary>
        System.Action<string> OnError { get; set; }
    }
}
