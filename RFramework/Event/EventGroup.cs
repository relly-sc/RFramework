using System;
using System.Collections.Generic;

namespace RFramework.Event
{
    /// <summary>
    /// 事件组，Dispose() 一键取消组内所有事件订阅。
    /// </summary>
    public sealed class EventGroup : IDisposable
    {
        private IEventModule eventModule;
        private readonly List<(Type type, Delegate handler)> entries;

        /// <summary>
        /// 初始化事件组的新实例。
        /// </summary>
        /// <param name="eventModule">关联的事件模块。</param>
        internal EventGroup(IEventModule eventModule)
        {
            this.eventModule = eventModule;
            entries = new List<(Type, Delegate)>();
        }

        /// <summary>
        /// 订阅事件，同时将处理函数加入组内以便批量退订。
        /// </summary>
        /// <typeparam name="T">事件消息类型。</typeparam>
        /// <param name="handler">事件处理函数。</param>
        public void Subscribe<T>(Action<T> handler)
        {
            if (eventModule == null)
            {
                throw new RFrameworkException("EventGroup already disposed.");
            }

            eventModule.Subscribe(handler);
            entries.Add((typeof(T), handler));
        }

        /// <summary>
        /// 取消组内所有事件订阅并释放资源。
        /// 可安全重复调用。
        /// </summary>
        public void Dispose()
        {
            if (eventModule == null)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                (Type type, Delegate handler) = entries[i];
                Unsubscribe(type, handler);
            }

            entries.Clear();
            eventModule = null;
        }

        /// <summary>
        /// 直接调用非泛型 Unsubscribe，零反射。
        /// </summary>
        private void Unsubscribe(Type type, Delegate handler)
        {
            eventModule?.Unsubscribe(type, handler);
        }
    }
}
