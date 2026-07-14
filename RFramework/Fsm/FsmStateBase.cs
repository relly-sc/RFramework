namespace RFramework
{
    /// <summary>
    /// 有限状态机状态抽象基类。
    /// 为 <see cref="IFsmState"/> 的五个生命周期方法提供默认空实现，
    /// 子类只需重写实际需要的方法。
    /// </summary>
    public abstract class FsmStateBase : IFsmState
    {
        /// <inheritdoc cref="IFsmState.OnInit"/>
        public virtual void OnInit()
        {
        }

        /// <inheritdoc cref="IFsmState.OnEnter"/>
        public virtual void OnEnter()
        {
        }

        /// <inheritdoc cref="IFsmState.OnUpdate"/>
        public virtual void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
        }

        /// <inheritdoc cref="IFsmState.OnLeave"/>
        public virtual void OnLeave(bool isShutdown)
        {
        }

        /// <inheritdoc cref="IFsmState.OnDestroy"/>
        public virtual void OnDestroy()
        {
        }
    }
}
