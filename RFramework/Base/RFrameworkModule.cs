namespace RFramework
{
    /// <summary>
    /// 由框架统一驱动和停止的模块基类。
    /// </summary>
    internal abstract class RFrameworkModule
    {
        /// <summary>
        /// 获取调度顺序。数值较大的模块优先执行 Tick，较晚执行 Stop。
        /// </summary>
        internal virtual int Order => 0;

        /// <summary>
        /// 执行一次模块轮询。
        /// </summary>
        /// <param name="deltaTime">受时间缩放影响的帧间隔。</param>
        /// <param name="unscaledDeltaTime">不受时间缩放影响的帧间隔。</param>
        internal abstract void Tick(float deltaTime, float unscaledDeltaTime);

        /// <summary>
        /// 停止模块并释放其持有的资源。
        /// </summary>
        internal abstract void Stop();
    }
}
