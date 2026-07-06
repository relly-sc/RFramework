using System.Collections.Generic;

namespace RFramework.Timer
{
    /// <summary>
    /// 计时器模块。管理所有活跃计时器的 Update 驱动和生命周期清理，
    /// 使用待注册队列（双缓冲模式）避免在遍历中修改集合。
    /// </summary>
    internal sealed class TimerModule : RFrameworkModule, ITimerModule
    {
        private readonly List<Timer> timers;
        private readonly List<Timer> pendingTimers;

        /// <summary>
        /// 初始化计时器模块的新实例。
        /// </summary>
        public TimerModule()
        {
            timers = new List<Timer>();
            pendingTimers = new List<Timer>();
        }

        /// <summary>
        /// 获取框架模块优先级。
        /// 高优先级确保计时器在事件系统（Priority 7）之前更新，
        /// 使计时器回调中发布的异步事件能在同帧被事件模块分发。
        /// </summary>
        internal override int Priority
        {
            get { return 10; }
        }

        /// <summary>
        /// 计时器模块轮询。驱动所有活跃计时器，触发到期回调，清理已结束的计时器。
        /// 倒序遍历以支持回调中安全删除当前计时器之后的元素。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间（受 timeScale 影响）。</param>
        /// <param name="realElapseSeconds">真实流逝时间（不受 timeScale 影响）。</param>
        internal override void Update(float elapseSeconds, float realElapseSeconds)
        {
            // 合并本帧新注册的计时器（双缓冲，避免遍历中修改）
            if (pendingTimers.Count > 0)
            {
                timers.AddRange(pendingTimers);
                pendingTimers.Clear();
            }

            // 倒序遍历：支持回调中取消当前或后续计时器
            for (int i = timers.Count - 1; i >= 0 && i < timers.Count; i--)
            {
                Timer timer = timers[i];

                // 已结束的计时器跳过（将在末尾统一清理）
                if (timer.IsOver)
                {
                    continue;
                }

                // 根据忽略 Timescale 选择时间增量
                float deltaTime = timer.IgnorTimescale ? realElapseSeconds : elapseSeconds;

                if (timer.Update(deltaTime))
                {
                    timer.InvokeCallback();
                }
            }

            // 清理已结束的计时器（倒序 RemoveAt 为 O(1) 尾部移动）
            for (int i = timers.Count - 1; i >= 0; i--)
            {
                if (timers[i].IsOver)
                {
                    timers.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 关闭并清理计时器模块。
        /// </summary>
        internal override void Shutdown()
        {
            timers.Clear();
            pendingTimers.Clear();
        }

        /// <summary>
        /// 获取当前活跃的计时器数量（含待注册队列）。
        /// </summary>
        public int TimerCount
        {
            get { return timers.Count + pendingTimers.Count; }
        }

        /// <summary>
        /// 注册计时器，模块接管其 Update 驱动。
        /// 计时器在下一帧 Update 时合并到活跃列表。
        /// </summary>
        /// <param name="timer">要注册的计时器，为 null 时抛出异常。</param>
        public void RegisterTimer(Timer timer)
        {
            if (timer == null)
            {
                throw new RFrameworkException("Timer is invalid.");
            }

            pendingTimers.Add(timer);
        }

        /// <summary>
        /// 取消指定计时器。
        /// </summary>
        /// <param name="timer">要取消的计时器，为 null 时不操作。</param>
        public void CancelTimer(Timer timer)
        {
            if (timer != null)
            {
                timer.Cancel();
            }
        }

        /// <summary>
        /// 取消并移除所有活跃和待注册的计时器。
        /// </summary>
        public void CancelAllTimers()
        {
            for (int i = timers.Count - 1; i >= 0; i--)
            {
                timers[i].Cancel();
            }

            for (int i = pendingTimers.Count - 1; i >= 0; i--)
            {
                pendingTimers[i].Cancel();
            }
        }
    }
}
