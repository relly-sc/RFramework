namespace RFramework
{
    /// <summary>
    /// 有限状态机状态接口。
    /// 所有状态（Procedure、Entity AI、UI 流程等）均实现此接口，
    /// 由 <see cref="IFsm"/> 驱动生命周期回调。
    /// </summary>
    public interface IFsmState
    {
        /// <summary>
        /// 状态初始化，在加入 FSM 时调用一次。
        /// </summary>
        void OnInit();

        /// <summary>
        /// 状态进入时调用，每次切换到该状态时触发。
        /// </summary>
        void OnEnter();

        /// <summary>
        /// 状态每帧轮询。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间，以秒为单位。</param>
        /// <param name="realElapseSeconds">真实流逝时间，以秒为单位。</param>
        void OnUpdate(float elapseSeconds, float realElapseSeconds);

        /// <summary>
        /// 状态离开时调用，切换或停止 FSM 时触发。
        /// </summary>
        /// <param name="isShutdown">是否为 FSM 关闭导致的离开。</param>
        void OnLeave(bool isShutdown);

        /// <summary>
        /// 状态销毁时调用，FSM 销毁时触发一次。
        /// </summary>
        void OnDestroy();
    }
}
