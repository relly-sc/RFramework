using System;

namespace RFramework.Event
{
    /// <summary>
    /// 事件模块接口。提供类型安全、零 GC 的发布-订阅事件系统。
    /// </summary>
    public interface IEventModule
    {
        /// <summary>
        /// 获取已注册的事件处理函数总数。
        /// </summary>
        int HandlerCount { get; }

        /// <summary>
        /// 获取异步队列中待处理的事件数。
        /// </summary>
        int AsyncEventCount { get; }

        /// <summary>
        /// 获取指定类型事件的处理函数数量。
        /// </summary>
        /// <typeparam name="T">事件消息类型。</typeparam>
        /// <returns>处理函数数量。</returns>
        int Count<T>();

        /// <summary>
        /// 检查指定事件类型是否存在处理函数。
        /// </summary>
        /// <typeparam name="T">事件消息类型。</typeparam>
        /// <param name="handler">要检查的事件处理函数。</param>
        /// <returns>是否存在该处理函数。</returns>
        bool Check<T>(Action<T> handler);

        /// <summary>
        /// 订阅事件。
        /// 允许同一处理函数多次订阅（去重由调用方自行负责）。
        /// </summary>
        /// <typeparam name="T">事件消息类型。</typeparam>
        /// <param name="handler">事件处理函数。</param>
        void Subscribe<T>(Action<T> handler);

        /// <summary>
        /// 取消订阅事件。
        /// </summary>
        /// <typeparam name="T">事件消息类型。</typeparam>
        /// <param name="handler">要移除的事件处理函数。</param>
        void Unsubscribe<T>(Action<T> handler);

        /// <summary>
        /// 取消订阅事件（非泛型版本，供 EventGroup 等内部组件使用）。
        /// 推荐使用泛型版本 <see cref="Unsubscribe{T}"/> 以获得编译期类型安全。
        /// </summary>
        /// <param name="eventType">事件消息类型。</param>
        /// <param name="handler">要移除的事件处理函数。</param>
        void Unsubscribe(Type eventType, Delegate handler);

        /// <summary>
        /// 同步立即分发事件。
        /// 事件在当前调用栈中直接触发所有已注册的处理函数。
        /// struct 消息栈分配零 GC，高频调用适用。
        /// </summary>
        /// <typeparam name="T">事件消息类型。</typeparam>
        /// <param name="args">事件消息实例（struct 为栈拷贝，class 为引用传递）。</param>
        void Fire<T>(T args);

        /// <summary>
        /// 异步分发事件（线程安全）。
        /// 事件进入线程安全队列，在下一帧的 Update 中由主线程统一分发。
        /// 支持从任意线程安全调用。
        /// </summary>
        /// <typeparam name="T">事件消息类型。</typeparam>
        /// <param name="args">事件消息实例。</param>
        void FireAsync<T>(T args);

        /// <summary>
        /// 创建事件组，用于批量管理订阅生命周期。
        /// 调用 EventGroup.Dispose() 时自动取消该组内所有订阅。
        /// </summary>
        /// <returns>新创建的事件组。</returns>
        EventGroup CreateGroup();
    }
}
