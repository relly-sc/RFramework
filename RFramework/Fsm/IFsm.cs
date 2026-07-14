using System;

namespace RFramework
{
    /// <summary>
    /// 有限状态机实例接口。
    /// 管理一系列 <see cref="IFsmState"/> 的生命周期，保证同一时刻只有一个状态活跃。
    /// </summary>
    public interface IFsm
    {
        /// <summary>
        /// 获取当前活跃状态的 Type。
        /// </summary>
        Type CurrentStateType { get; }

        /// <summary>
        /// 获取当前活跃状态实例。
        /// </summary>
        IFsmState CurrentState { get; }

        /// <summary>
        /// 获取当前状态已运行的持续时间（秒）。
        /// </summary>
        float CurrentStateTime { get; }

        /// <summary>
        /// 启动状态机，进入指定初始状态。
        /// 只能在 FSM 尚未启动时调用一次。
        /// </summary>
        /// <typeparam name="TState">初始状态类型，必须实现 <see cref="IFsmState"/>。</typeparam>
        void Start<TState>() where TState : IFsmState;

        /// <summary>
        /// 切换到指定状态。
        /// 离开当前状态 → 进入目标状态，若目标状态已是当前状态则忽略。
        /// </summary>
        /// <typeparam name="TState">目标状态类型，必须实现 <see cref="IFsmState"/>。</typeparam>
        void ChangeState<TState>() where TState : IFsmState;

        /// <summary>
        /// 检查是否包含指定类型的状态。
        /// </summary>
        /// <typeparam name="TState">要检查的状态类型。</typeparam>
        /// <returns>如果包含则返回 true。</returns>
        bool HasState<TState>() where TState : IFsmState;

        /// <summary>
        /// 获取指定类型的状态实例。
        /// </summary>
        /// <typeparam name="TState">要获取的状态类型。</typeparam>
        /// <returns>状态实例，不存在时返回 null。</returns>
        TState GetState<TState>() where TState : IFsmState;
    }
}
