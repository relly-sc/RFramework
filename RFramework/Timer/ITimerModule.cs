namespace RFramework.Timer
{
    /// <summary>
    /// 计时器模块接口。管理所有活跃计时器的生命周期和 Update 驱动。
    /// </summary>
    public interface ITimerModule
    {
        /// <summary>
        /// 获取当前活跃的计时器数量。
        /// </summary>
        int TimerCount { get; }

        /// <summary>
        /// 注册一个由工厂方法创建的计时器，模块接管其 Update 驱动。
        /// </summary>
        /// <param name="timer">要注册的计时器，通过 Timer.CreateXxx() 创建。</param>
        void RegisterTimer(Timer timer);

        /// <summary>
        /// 取消指定计时器（标记为已结束，下一帧清理）。
        /// </summary>
        /// <param name="timer">要取消的计时器。</param>
        void CancelTimer(Timer timer);

        /// <summary>
        /// 取消并移除所有活跃计时器。
        /// </summary>
        void CancelAllTimers();
    }
}
