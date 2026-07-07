using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RFramework.Event;
using RFramework.Timer;

namespace RFramework.Network
{
    /// <summary>
    /// 网络模块核心实现。管理单连接的生命周期：
    /// 连接 → 心跳保活 → 断线自动重连 → 消息分发。
    /// </summary>
    internal sealed class NetworkModule : RFrameworkModule, INetworkModule
    {
        /// <summary>
        /// 网络辅助器引用。
        /// </summary>
        private INetworkHelper networkHelper;

        /// <summary>
        /// 事件模块引用。
        /// </summary>
        private IEventModule eventModule;

        /// <summary>
        /// 计时器模块引用。
        /// </summary>
        private ITimerModule timerModule;

        /// <summary>
        /// 连接状态。
        /// </summary>
        private bool isConnected;

        /// <summary>
        /// 是否正在尝试重连（防止重复重连）。
        /// </summary>
        private bool isReconnecting;

        /// <summary>
        /// 连接过程中被主动断开（不再重连）。
        /// </summary>
        private bool disposed;

        /// <summary>
        /// 心跳间隔（秒）。
        /// </summary>
        private float heartbeatInterval = 5f;

        /// <summary>
        /// 是否启用自动重连。
        /// </summary>
        private bool autoReconnect = true;

        /// <summary>
        /// 重连间隔（秒）。
        /// </summary>
        private float reconnectInterval = 3f;

        /// <summary>
        /// 当前 IP 和端口（用于重连）。
        /// </summary>
        private string currentIP;

        /// <summary>
        /// 当前端口。
        /// </summary>
        private int currentPort;

        /// <summary>
        /// 消息处理器：msgId → 处理函数。
        /// </summary>
        private readonly Dictionary<int, Action<byte[]>> messageHandlers = new Dictionary<int, Action<byte[]>>();

        /// <summary>
        /// 心跳计时器引用。
        /// </summary>
        private RFramework.Timer.Timer heartbeatTimer;

        /// <summary>
        /// 重连计时器引用。
        /// </summary>
        private RFramework.Timer.Timer reconnectTimer;

        /// <inheritdoc/>
        public bool IsConnected
        {
            get { return isConnected; }
        }

        /// <inheritdoc/>
        public float HeartbeatInterval
        {
            get { return heartbeatInterval; }
            set { heartbeatInterval = value; }
        }

        /// <inheritdoc/>
        public bool AutoReconnect
        {
            get { return autoReconnect; }
            set { autoReconnect = value; }
        }

        /// <inheritdoc/>
        public float ReconnectInterval
        {
            get { return reconnectInterval; }
            set { reconnectInterval = value; }
        }

        /// <inheritdoc/>
        internal override int Priority
        {
            get
            {
                return 45;
            }
        }

        /// <inheritdoc/>
        public void SetHelper(INetworkHelper helper)
        {
            if (networkHelper != null)
            {
                UnbindHelper();
            }

            networkHelper = helper;

            if (networkHelper != null)
            {
                BindHelper();
            }
        }

        /// <inheritdoc/>
        public void SetDependencies(IEventModule eventModule, ITimerModule timerModule)
        {
            this.eventModule = eventModule;
            this.timerModule = timerModule;
        }

        /// <inheritdoc/>
        public Task ConnectAsync(string ip, int port, System.Threading.CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(ip))
            {
                throw new RFrameworkException("IP address is invalid.");
            }

            if (networkHelper == null)
            {
                throw new RFrameworkException("Network helper is not set.");
            }

            if (isConnected)
            {
                return Task.CompletedTask;
            }

            currentIP = ip;
            currentPort = port;
            disposed = false;

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            // 连接成功
            networkHelper.OnConnected = () =>
            {
                isConnected = true;
                StartHeartbeat();
                eventModule?.Fire(new NetworkConnectedEvent());
                tcs.TrySetResult(true);
            };

            // 连接失败（断开回调在连接未成功时也视为失败）
            networkHelper.OnDisconnected = () =>
            {
                if (!isConnected)
                {
                    tcs.TrySetException(new RFrameworkException($"Failed to connect to {ip}:{port}."));
                    return;
                }

                HandleDisconnected();
            };

            // 错误回调
            networkHelper.OnError = (msg) =>
            {
                if (!isConnected)
                {
                    tcs.TrySetException(new RFrameworkException($"Connect error: {msg}"));
                }
                else
                {
                    eventModule?.Fire(new NetworkErrorEvent(msg));
                }
            };

            ct.Register(() => tcs.TrySetCanceled());

            try
            {
                networkHelper.Connect(ip, port);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(new RFrameworkException($"Connect exception: {ex.Message}"));
            }

            return tcs.Task;
        }

        /// <inheritdoc/>
        public void Disconnect()
        {
            disposed = true;
            StopHeartbeat();
            if (reconnectTimer != null)
            {
                timerModule?.CancelTimer(reconnectTimer);
                reconnectTimer = null;
            }
            isReconnecting = false;

            if (networkHelper != null && isConnected)
            {
                networkHelper.Disconnect();
            }

            isConnected = false;
        }

        /// <inheritdoc/>
        public void Send(int msgId, byte[] body)
        {
            if (!isConnected || networkHelper == null)
            {
                throw new RFrameworkException("Not connected. Cannot send message.");
            }

            networkHelper.Send(body);
        }

        /// <inheritdoc/>
        public void RegisterHandler(int msgId, Action<byte[]> handler)
        {
            messageHandlers[msgId] = handler;
        }

        /// <inheritdoc/>
        public void UnregisterHandler(int msgId)
        {
            messageHandlers.Remove(msgId);
        }

        /// <inheritdoc/>
        internal override void Update(float elapseSeconds, float realElapseSeconds)
        {
            networkHelper?.Update();
        }

        /// <inheritdoc/>
        internal override void Shutdown()
        {
            StopHeartbeat();
            if (reconnectTimer != null)
            {
                timerModule?.CancelTimer(reconnectTimer);
                reconnectTimer = null;
            }
            Disconnect();
            UnbindHelper();
            messageHandlers.Clear();
        }

        /// <summary>
        /// 绑定 Helper 的回调和收包事件。
        /// </summary>
        private void BindHelper()
        {
            networkHelper.OnReceive = HandleReceive;
        }

        /// <summary>
        /// 解绑 Helper 所有回调。
        /// </summary>
        private void UnbindHelper()
        {
            if (networkHelper != null)
            {
                networkHelper.OnReceive = null;
                networkHelper.OnConnected = null;
                networkHelper.OnDisconnected = null;
                networkHelper.OnError = null;
            }
        }

        /// <summary>
        /// 处理收到的消息：按 msgId 路由到注册的处理器。
        /// </summary>
        /// <param name="msgId">消息 ID。</param>
        /// <param name="body">消息体。</param>
        private void HandleReceive(int msgId, byte[] body)
        {
            if (messageHandlers.TryGetValue(msgId, out Action<byte[]> handler))
            {
                handler(body);
            }
        }

        /// <summary>
        /// 处理断开连接：启动自动重连（如果启用）。
        /// </summary>
        private void HandleDisconnected()
        {
            isConnected = false;
            StopHeartbeat();
            eventModule?.Fire(new NetworkDisconnectedEvent());

            if (autoReconnect && !disposed && !isReconnecting)
            {
                StartReconnect();
            }
        }

        /// <summary>
        /// 启动心跳计时器。
        /// </summary>
        private void StartHeartbeat()
        {
            if (heartbeatInterval <= 0f || timerModule == null)
            {
                return;
            }

            heartbeatTimer = RFramework.Timer.Timer.CreateRepeat(
                0f,
                heartbeatInterval,
                () =>
                {
                    if (isConnected)
                    {
                        networkHelper?.Send(Array.Empty<byte>());
                    }
                });
            timerModule.RegisterTimer(heartbeatTimer);
        }

        /// <summary>
        /// 停止心跳计时器。
        /// </summary>
        private void StopHeartbeat()
        {
            if (heartbeatTimer != null)
            {
                timerModule?.CancelTimer(heartbeatTimer);
                heartbeatTimer = null;
            }
        }

        /// <summary>
        /// 启动自动重连。
        /// </summary>
        private void StartReconnect()
        {
            isReconnecting = true;

            reconnectTimer = RFramework.Timer.Timer.CreateOnce(
                reconnectInterval,
                async () =>
                {
                    if (disposed)
                    {
                        isReconnecting = false;
                        return;
                    }

                    try
                    {
                        await ConnectAsync(currentIP, currentPort);
                        isReconnecting = false;
                    }
                    catch
                    {
                        // 重连失败，再次尝试
                        StartReconnect();
                    }
                });
            timerModule?.RegisterTimer(reconnectTimer);
        }
    }
}
