using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using RFramework.Event;
using RFramework.Timer;

namespace RFramework.Network
{
    /// <summary>
    /// 网络通道实现。管理单个服务器连接的完整生命周期：
    /// 连接 → 心跳保活 → 断线自动重连 → 消息分发。
    /// </summary>
    /// <remarks>
    /// 通道不是 RFrameworkModule，而是由 NetworkModule 创建和管理的普通对象。
    /// 每个通道拥有独立的 Helper、心跳、重连和消息处理器。
    /// </remarks>
    internal sealed class NetworkChannel : INetworkChannel
    {
        /// <summary>
        /// 网络辅助器引用。
        /// </summary>
        private INetworkHelper networkHelper;

        /// <summary>
        /// 事件模块引用。
        /// </summary>
        private readonly IEventModule eventModule;

        /// <summary>
        /// 计时器模块引用。
        /// </summary>
        private readonly ITimerModule timerModule;

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
        /// 消息处理器：msgId → 处理函数。
        /// </summary>
        private readonly Dictionary<int, Action<byte[]>> messageHandlers = new Dictionary<int, Action<byte[]>>();

        /// <summary>
        /// 回调马歇尔队列。接收线程/线程池触发的 OnConnected/OnDisconnected/OnError
        /// 先入队，由主线程 Update 排空后执行，避免跨线程污染 Event/Timer 等主线程模块。
        /// </summary>
        private readonly ConcurrentQueue<Action> callbackQueue = new ConcurrentQueue<Action>();

        /// <summary>
        /// 心跳计时器引用。
        /// </summary>
        private Timer.Timer heartbeatTimer;

        /// <summary>
        /// 重连计时器引用。
        /// </summary>
        private Timer.Timer reconnectTimer;

        /// <summary>
        /// 构造网络通道。
        /// </summary>
        /// <param name="name">通道名称（如 "Login"、"Chat"）。</param>
        /// <param name="eventModule">事件模块，用于分发连接/断开/错误事件。</param>
        /// <param name="timerModule">计时器模块，用于心跳和重连。</param>
        public NetworkChannel(string name, IEventModule eventModule, ITimerModule timerModule)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new RFrameworkException("Channel name is invalid.");
            }

            Name = name;
            this.eventModule = eventModule;
            this.timerModule = timerModule;
        }

        /// <inheritdoc/>
        public string Name { get; }

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
        public string CurrentIP { get; private set; }

        /// <inheritdoc/>
        public int CurrentPort { get; private set; }

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
        public Task ConnectAsync(string ip, int port, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(ip))
            {
                throw new RFrameworkException("IP address is invalid.");
            }

            if (networkHelper == null)
            {
                throw new RFrameworkException(string.Format("Network helper is not set for channel '{0}'.", Name));
            }

            if (isConnected)
            {
                return Task.CompletedTask;
            }

            CurrentIP = ip;
            CurrentPort = port;
            disposed = false;

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

            // 连接成功（统一马歇尔到主线程执行，避免跨线程触碰 Event/Timer）
            networkHelper.OnConnected = () =>
            {
                callbackQueue.Enqueue(() =>
                {
                    isConnected = true;
                    StartHeartbeat();
                    eventModule?.Fire(new NetworkConnectedEvent(Name));
                    tcs.TrySetResult(true);
                });
            };

            // 连接断开
            networkHelper.OnDisconnected = () =>
            {
                callbackQueue.Enqueue(() =>
                {
                    if (!isConnected)
                    {
                        tcs.TrySetException(new RFrameworkException(
                            string.Format("Failed to connect to {0}:{1} [{2}].", ip, port, Name)));
                        return;
                    }

                    HandleDisconnected();
                });
            };

            // 错误回调
            networkHelper.OnError = (msg) =>
            {
                callbackQueue.Enqueue(() =>
                {
                    if (!isConnected)
                    {
                        tcs.TrySetException(new RFrameworkException(string.Format("Connect error [{0}]: {1}", Name, msg)));
                    }
                    else
                    {
                        eventModule?.Fire(new NetworkErrorEvent(Name, msg));
                    }
                });
            };

            ct.Register(() => tcs.TrySetCanceled());

            try
            {
                networkHelper.Connect(ip, port);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(new RFrameworkException(
                    string.Format("Connect exception [{0}]: {1}", Name, ex.Message)));
            }

            return tcs.Task;
        }

        /// <inheritdoc/>
        public void Disconnect()
        {
            disposed = true;
            StopHeartbeat();
            CancelReconnect();
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
                throw new RFrameworkException(
                    string.Format("Channel '{0}' is not connected. Cannot send message.", Name));
            }

            networkHelper.Send(msgId, body);
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
        public void Update(float elapseSeconds, float realElapseSeconds)
        {
            networkHelper?.Update();

            // 主线程排空回调队列（OnConnected/OnDisconnected/OnError 在此执行）
            while (callbackQueue.TryDequeue(out Action callback))
            {
                callback();
            }
        }

        /// <inheritdoc/>
        public void Shutdown()
        {
            StopHeartbeat();
            CancelReconnect();
            Disconnect();
            UnbindHelper();
            messageHandlers.Clear();

            // 清空未执行的马歇尔回调，避免关闭后误触发
            while (callbackQueue.TryDequeue(out _))
            {
            }
        }

        // ====== 内部方法 ======

        /// <summary>
        /// 绑定 Helper 的收包回调。
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
        /// 处理断开连接：分发事件，启动自动重连（如果启用）。
        /// </summary>
        private void HandleDisconnected()
        {
            isConnected = false;
            StopHeartbeat();
            eventModule?.Fire(new NetworkDisconnectedEvent(Name));

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

            heartbeatTimer = Timer.Timer.CreateRepeat(
                0f,
                heartbeatInterval,
                () =>
                {
                    if (isConnected)
                    {
                        // 心跳：msgId 0 + 空 body，各 Helper 按自身帧协议封装
                        networkHelper?.Send(0, Array.Empty<byte>());
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
        /// 取消重连计时器。
        /// </summary>
        private void CancelReconnect()
        {
            if (reconnectTimer != null)
            {
                timerModule?.CancelTimer(reconnectTimer);
                reconnectTimer = null;
            }
        }

        /// <summary>
        /// 启动自动重连。
        /// </summary>
        private void StartReconnect()
        {
            isReconnecting = true;

            reconnectTimer = Timer.Timer.CreateOnce(
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
                        await ConnectAsync(CurrentIP, CurrentPort);
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
