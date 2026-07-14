using System;
using System.Collections.Generic;

namespace RFramework
{
    /// <summary>
    /// 事件模块。支持 struct 消息（栈分配零 GC）和 class 消息（引用传递）。
    /// </summary>
    internal sealed class EventModule : RFrameworkModule, IEventModule
    {
        /// <inheritdoc/>
        public event Action<RFrameworkException> OnError;

        /// <summary>
        /// 处理函数存储：Type → 该类型的所有处理函数链表。
        /// 使用 LinkedList 以 O(1) 增删，配合 CachedNodes 支持遍历中安全删除。
        /// </summary>
        private readonly Dictionary<Type, LinkedList<Delegate>> handlers;

        /// <summary>
        /// 异步事件队列，支持跨线程安全入队。
        /// 每个元素是封装了 HandleEvent 调用的 Action 闭包。
        /// FireAsync 低频率使用，闭包分配可接受。
        /// </summary>
        private readonly Queue<Action> asyncQueue;

        /// <summary>
        /// 异步队列线程安全锁。
        /// </summary>
        private readonly object queueLock;

        /// <summary>
        /// 缓存节点映射，key 为迭代令牌，value 为当前处理的链表节点下一个节点。
        /// 用于在遍历中安全删除/新增处理函数——仿 GF EventPool.CachedNodes 模式。
        /// </summary>
        private readonly Dictionary<int, LinkedListNode<Delegate>> cachedNodes;

        /// <summary>
        /// 迭代令牌计数器，每次 Dispatch 递增，保证嵌套事件不会冲突。
        /// </summary>
        private int iterationToken;

        /// <summary>
        /// 初始化事件模块的新实例。
        /// </summary>
        public EventModule()
        {
            handlers = new Dictionary<Type, LinkedList<Delegate>>();
            asyncQueue = new Queue<Action>();
            queueLock = new object();
            cachedNodes = new Dictionary<int, LinkedListNode<Delegate>>();
            iterationToken = 0;
        }

        /// <summary>
        /// 获取框架模块优先级。高优先级保证事件在处理其他系统逻辑（如 Procedure）前分发。
        /// </summary>
        internal override int Priority
        {
            get { return 7; }
        }

        /// <summary>
        /// 事件模块轮询。从异步队列中取出所有事件并在主线程分发。
        /// </summary>
        internal override void Update(float elapseSeconds, float realElapseSeconds)
        {
            // 持锁仅做出队，释放锁后再执行回调，避免持锁期间阻塞 FireAsync 入队或死锁
            List<Action> toInvoke = new List<Action>();
            lock (queueLock)
            {
                while (asyncQueue.Count > 0)
                {
                    toInvoke.Add(asyncQueue.Dequeue());
                }
            }

            Exception firstError = null;
            for (int i = 0; i < toInvoke.Count; i++)
            {
                try
                {
                    toInvoke[i].Invoke();
                }
                catch (Exception ex)
                {
                    // A bad asynchronous event must not prevent later queued
                    // events from reaching the main thread this frame.
                    if (firstError == null)
                    {
                        firstError = ex;
                    }
                }
            }

            if (firstError != null)
            {
                throw new RFrameworkException("EventModule: one or more asynchronous event handlers threw exception.", firstError);
            }
        }

        /// <summary>
        /// 关闭并清理事件模块，释放所有资源。
        /// </summary>
        internal override void Shutdown()
        {
            lock (queueLock)
            {
                asyncQueue.Clear();
            }

            handlers.Clear();
            cachedNodes.Clear();
            OnError = null;
        }

        /// <summary>
        /// 获取已注册的事件处理函数总数。
        /// </summary>
        public int HandlerCount
        {
            get
            {
                int count = 0;
                foreach (KeyValuePair<Type, LinkedList<Delegate>> kvp in handlers)
                {
                    count += kvp.Value.Count;
                }

                return count;
            }
        }

        /// <summary>
        /// 获取异步队列中待处理的事件数。
        /// </summary>
        public int AsyncEventCount
        {
            get
            {
                lock (queueLock)
                {
                    return asyncQueue.Count;
                }
            }
        }

        /// <summary>
        /// 获取指定类型事件的处理函数数量。
        /// </summary>
        public int Count<T>()
        {
            Type type = typeof(T);
            if (handlers.TryGetValue(type, out LinkedList<Delegate> list))
            {
                return list.Count;
            }

            return 0;
        }

        /// <summary>
        /// 检查指定事件类型是否存在处理函数。
        /// </summary>
        public bool Check<T>(Action<T> handler)
        {
            if (handler == null)
            {
                throw new RFrameworkException("Event handler is invalid.");
            }

            Type type = typeof(T);
            return handlers.TryGetValue(type, out LinkedList<Delegate> list) && list.Contains(handler);
        }

        /// <summary>
        /// 订阅事件。
        /// 允许同一处理函数多次订阅；若要防止重复，调用方自行 Check 后决定。
        /// </summary>
        public void Subscribe<T>(Action<T> handler)
        {
            if (handler == null)
            {
                throw new RFrameworkException("Event handler is invalid.");
            }

            Type type = typeof(T);
            if (!handlers.TryGetValue(type, out LinkedList<Delegate> list))
            {
                list = new LinkedList<Delegate>();
                handlers.Add(type, list);
            }

            list.AddLast(handler);
        }

        /// <summary>
        /// 取消订阅事件。
        /// 如果遍历正在进行中（同类型事件的 Dispatch 尚未结束），
        /// 通过 cachedNodes 更新当前迭代节点，确保迭代器不会访问已删除节点。
        /// </summary>
        public void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null)
            {
                throw new RFrameworkException("Event handler is invalid.");
            }

            Type type = typeof(T);
            if (!handlers.TryGetValue(type, out LinkedList<Delegate> list))
            {
                return;
            }

            // 如果正在遍历该类型的事件，更新 cachedNodes 中的 next 指针，
            // 确保 Dispatch 的 while 循环能跳过被删除节点。
            if (cachedNodes.Count > 0)
            {
                LinkedListNode<Delegate> node = list.First;
                while (node != null)
                {
                    if (node.Value == (Delegate)handler)
                    {
                        foreach (KeyValuePair<int, LinkedListNode<Delegate>> kvp in cachedNodes)
                        {
                            if (kvp.Value == node)
                            {
                                cachedNodes[kvp.Key] = node.Next;
                            }
                        }

                        break;
                    }

                    node = node.Next;
                }
            }

            list.Remove(handler);

            // 清理空链表以节省后续查找开销
            if (list.Count == 0)
            {
                handlers.Remove(type);
            }
        }

        /// <summary>
        /// 取消订阅事件（非泛型版本）。
        /// 逻辑与泛型版本一致，同样支持遍历中安全删除。
        /// </summary>
        public void Unsubscribe(Type eventType, Delegate handler)
        {
            if (handler == null)
            {
                throw new RFrameworkException("Event handler is invalid.");
            }

            if (!handlers.TryGetValue(eventType, out LinkedList<Delegate> list))
            {
                return;
            }

            if (cachedNodes.Count > 0)
            {
                LinkedListNode<Delegate> node = list.First;
                while (node != null)
                {
                    if (node.Value == handler)
                    {
                        foreach (KeyValuePair<int, LinkedListNode<Delegate>> kvp in cachedNodes)
                        {
                            if (kvp.Value == node)
                            {
                                cachedNodes[kvp.Key] = node.Next;
                            }
                        }

                        break;
                    }

                    node = node.Next;
                }
            }

            list.Remove(handler);

            if (list.Count == 0)
            {
                handlers.Remove(eventType);
            }
        }

        /// <summary>
        /// 同步立即分发事件。
        /// struct 消息在栈上传递，零分配零 GC；class 消息按引用传递。
        /// </summary>
        public void Fire<T>(T args)
        {
            Dispatch(args);
        }

        /// <inheritdoc/>
        public void FireSafely<T>(T args)
        {
            try
            {
                Dispatch(args);
            }
            catch (Exception ex)
            {
                RaiseError(new RFrameworkException(
                    $"EventModule: safe dispatch for '{typeof(T).Name}' failed.", ex));
            }
        }

        /// <summary>
        /// 异步分发事件（线程安全）。
        /// 事件进入队列，在下一帧 Update 中由主线程分发。
        /// 可从任意线程安全调用。
        /// </summary>
        public void FireAsync<T>(T args)
        {
            lock (queueLock)
            {
                asyncQueue.Enqueue(() => Dispatch(args));
            }
        }

        /// <summary>
        /// 创建事件组，用于批量管理订阅生命周期。
        /// </summary>
        public EventGroup CreateGroup()
        {
            return new EventGroup(this);
        }

        private void RaiseError(RFrameworkException error)
        {
            Action<RFrameworkException> handlersSnapshot = OnError;
            if (handlersSnapshot == null)
            {
                return;
            }

            Delegate[] callbacks = handlersSnapshot.GetInvocationList();
            for (int i = 0; i < callbacks.Length; i++)
            {
                try
                {
                    ((Action<RFrameworkException>)callbacks[i]).Invoke(error);
                }
                catch
                {
                    // 错误观察者不得破坏 FireSafely 的异常隔离契约。
                }
            }
        }

        /// <summary>
        /// 核心分发逻辑。遍历指定类型的所有处理函数并触发。
        /// 使用 CachedNodes 模式支持遍历中安全删除/新增处理函数。
        /// </summary>
        private void Dispatch<T>(T args)
        {
            Type type = typeof(T);

            if (!handlers.TryGetValue(type, out LinkedList<Delegate> list))
            {
                return;
            }

            int token = ++iterationToken;
            LinkedListNode<Delegate> node = list.First;
            Exception dispatchError = null;

            try
            {
                while (node != null)
                {
                    // 缓存当前节点下一个为 token 对应值，后续 Unsubscribe 可能修改它
                    cachedNodes[token] = node.Next;

                    try
                    {
                        ((Action<T>)node.Value)(args);
                    }
                    catch (Exception ex)
                    {
                        // 单个 handler 异常不应中断其他订阅者：暂存首个异常并继续派发
                        if (dispatchError == null)
                        {
                            dispatchError = ex;
                        }
                    }

                    // 从缓存取 next（可能已被 Unsubscribe 修改为 skip 被删节点）
                    if (!cachedNodes.TryGetValue(token, out node))
                    {
                        break;
                    }
                }
            }
            finally
            {
                // 确保无论是否异常都清理 token 条目，避免 cachedNodes 泄漏
                cachedNodes.Remove(token);
            }

            // 派发过程中有 handler 抛异常：按框架约定以 RFrameworkException 上报，
            // 由 Runtime 层（EventComponent.Fire / BaseComponent.Update）捕获并写日志。
            if (dispatchError != null)
            {
                throw new RFrameworkException(
                    Utility.Text.Format("EventModule: handler for [{0}] threw exception.", type.FullName),
                    dispatchError);
            }
        }
    }
}
