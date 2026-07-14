using System;
using System.Collections.Generic;

namespace RFramework
{
    /// <summary>
    /// FSM 内部基类，提供 FsmModule 可调用的非泛型 Update 和 Shutdown 入口。
    /// </summary>
    internal abstract class FsmBase : IFsm
    {
        /// <inheritdoc cref="IFsm.CurrentStateType"/>
        public abstract Type CurrentStateType { get; }

        /// <inheritdoc cref="IFsm.CurrentState"/>
        public abstract IFsmState CurrentState { get; }

        /// <inheritdoc cref="IFsm.CurrentStateTime"/>
        public abstract float CurrentStateTime { get; }

        /// <inheritdoc cref="IFsm.Start{TState}"/>
        public abstract void Start<TState>() where TState : IFsmState;

        /// <inheritdoc cref="IFsm.ChangeState{TState}"/>
        public abstract void ChangeState<TState>() where TState : IFsmState;

        /// <inheritdoc cref="IFsm.HasState{TState}"/>
        public abstract bool HasState<TState>() where TState : IFsmState;

        /// <inheritdoc cref="IFsm.GetState{TState}"/>
        public abstract TState GetState<TState>() where TState : IFsmState;

        /// <summary>
        /// 内部更新入口，由 FsmModule 轮询调用。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间，以秒为单位。</param>
        /// <param name="realElapseSeconds">真实流逝时间，以秒为单位。</param>
        internal abstract void Update(float elapseSeconds, float realElapseSeconds);

        /// <summary>
        /// 内部关闭入口，由 FsmModule 销毁时调用。
        /// </summary>
        internal abstract void Shutdown();
    }

    /// <summary>
    /// 有限状态机实现。
    /// 管理一系列 <see cref="IFsmState"/>，保证同一时刻只有一个状态活跃。
    /// </summary>
    /// <typeparam name="TOwner">状态机拥有者类型。</typeparam>
    internal sealed class Fsm<TOwner> : FsmBase where TOwner : class
    {
        /// <summary>
        /// 状态字典：Type → 状态实例。
        /// </summary>
        private readonly Dictionary<Type, IFsmState> states;

        /// <summary>
        /// 状态机拥有者。
        /// </summary>
        private readonly TOwner owner;

        /// <summary>
        /// 当前活跃状态。
        /// </summary>
        private IFsmState currentState;

        /// <summary>
        /// 当前状态已运行时间。
        /// </summary>
        private float currentStateTime;

        /// <summary>
        /// 是否已经被销毁。
        /// </summary>
        private bool isDestroyed;

        /// <summary>
        /// 是否正处于状态切换中（OnLeave/OnEnter 执行期间）。
        /// 用于防止状态生命周期方法内嵌套调用 ChangeState/Start 导致的重入递归。
        /// </summary>
        private bool isTransitioning;

        /// <summary>
        /// Whether a lifecycle callback failed. A synchronous FSM cannot safely
        /// roll back arbitrary state side effects, so it stops after a failed
        /// transition instead of continuing from an indeterminate state.
        /// </summary>
        private bool isFaulted;

        /// <summary>
        /// 初始化有限状态机的新实例。
        /// </summary>
        /// <param name="owner">状态机拥有者。</param>
        /// <param name="states">状态列表，每个类型的实例仅保留一个。</param>
        public Fsm(TOwner owner, IFsmState[] states)
        {
            if (owner == null)
            {
                throw new RFrameworkException("Fsm owner is invalid.");
            }

            if (states == null || states.Length < 1)
            {
                throw new RFrameworkException("Fsm states is invalid.");
            }

            this.owner = owner;
            this.states = new Dictionary<Type, IFsmState>();
            currentState = null;
            currentStateTime = 0f;
            isDestroyed = false;
            isFaulted = false;

            for (int i = 0; i < states.Length; i++)
            {
                IFsmState state = states[i];
                if (state == null)
                {
                    throw new RFrameworkException("Fsm state is invalid.");
                }

                Type stateType = state.GetType();
                if (this.states.ContainsKey(stateType))
                {
                    throw new RFrameworkException(
                        Utility.Text.Format("Fsm state '{0}' is already exist.", stateType.FullName));
                }

                this.states.Add(stateType, state);
                state.OnInit();
            }
        }

        /// <summary>
        /// 获取状态机拥有者。
        /// </summary>
        public TOwner Owner
        {
            get { return owner; }
        }

        /// <inheritdoc/>
        public override Type CurrentStateType
        {
            get
            {
                if (currentState == null)
                {
                    return null;
                }

                return currentState.GetType();
            }
        }

        /// <inheritdoc/>
        public override IFsmState CurrentState
        {
            get { return currentState; }
        }

        /// <inheritdoc/>
        public override float CurrentStateTime
        {
            get { return currentStateTime; }
        }

        /// <inheritdoc/>
        public override void Start<TState>()
        {
            if (isDestroyed)
            {
                throw new RFrameworkException("Fsm is destroyed.");
            }

            if (isFaulted)
            {
                throw new RFrameworkException("Fsm is faulted after a lifecycle exception and can not be restarted.");
            }

            if (currentState != null)
            {
                throw new RFrameworkException("Fsm is already started.");
            }

            if (isTransitioning)
            {
                throw new RFrameworkException("Fsm is in transition, reentrant Start is not allowed.");
            }

            Type stateType = typeof(TState);
            IFsmState state = GetStateInternal(stateType);
            currentState = state;
            currentStateTime = 0f;
            isTransitioning = true;
            try
            {
                state.OnEnter();
            }
            catch
            {
                currentState = null;
                currentStateTime = 0f;
                isFaulted = true;
                throw;
            }
            finally
            {
                isTransitioning = false;
            }
        }

        /// <inheritdoc/>
        public override void ChangeState<TState>()
        {
            if (isDestroyed)
            {
                throw new RFrameworkException("Fsm is destroyed.");
            }

            if (isFaulted)
            {
                throw new RFrameworkException("Fsm is faulted after a lifecycle exception and can not change state.");
            }

            if (isTransitioning)
            {
                throw new RFrameworkException("Fsm is in transition, reentrant ChangeState is not allowed.");
            }

            Type stateType = typeof(TState);
            if (!states.TryGetValue(stateType, out IFsmState targetState))
            {
                throw new RFrameworkException(
                    Utility.Text.Format("Fsm can not change state to '{0}' which is not exist.", stateType.FullName));
            }

            // 同一状态则忽略
            if (currentState != null && currentState.GetType() == stateType)
            {
                return;
            }

            isTransitioning = true;
            try
            {
                // 离开当前状态
                if (currentState != null)
                {
                    currentState.OnLeave(false);
                }

                currentState = targetState;
                currentStateTime = 0f;
                targetState.OnEnter();
            }
            catch
            {
                currentState = null;
                currentStateTime = 0f;
                isFaulted = true;
                throw;
            }
            finally
            {
                isTransitioning = false;
            }
        }

        /// <inheritdoc/>
        public override bool HasState<TState>()
        {
            Type stateType = typeof(TState);
            return states.ContainsKey(stateType);
        }

        /// <inheritdoc/>
        public override TState GetState<TState>()
        {
            Type stateType = typeof(TState);
            return (TState)GetStateInternal(stateType);
        }

        /// <inheritdoc/>
        internal override void Update(float elapseSeconds, float realElapseSeconds)
        {
            if (isDestroyed || isFaulted || currentState == null)
            {
                return;
            }

            try
            {
                currentStateTime += realElapseSeconds;
                currentState.OnUpdate(elapseSeconds, realElapseSeconds);
            }
            catch
            {
                currentState = null;
                currentStateTime = 0f;
                isFaulted = true;
                throw;
            }
        }

        /// <inheritdoc/>
        internal override void Shutdown()
        {
            if (isDestroyed)
            {
                return;
            }

            List<Exception> errors = null;

            // 离开当前状态：包裹 isTransitioning 守卫，与 Start/ChangeState 保持一致，
            // 既防止重入，也确保异常不会跳过 finally 复位而中止后续清理。
            isTransitioning = true;
            try
            {
                if (currentState != null)
                {
                    currentState.OnLeave(true);
                }
            }
            catch (Exception ex)
            {
                (errors ?? (errors = new List<Exception>())).Add(ex);
            }
            finally
            {
                isTransitioning = false;
            }

            // 销毁所有状态：单个状态异常不应中止其余状态的清理（尽力清理）
            foreach (KeyValuePair<Type, IFsmState> kvp in states)
            {
                try
                {
                    kvp.Value.OnDestroy();
                }
                catch (Exception ex)
                {
                    (errors ?? (errors = new List<Exception>())).Add(ex);
                }
            }

            states.Clear();
            currentState = null;
            isDestroyed = true;

            // 收集到异常时抛出内联根因（保留首个原始异常作为 inner），
            // 便于上层模块级异常聚合保留真实堆栈。
            if (errors != null)
            {
                Exception first = errors[0];
                throw new RFrameworkException(
                    Utility.Text.Format("Fsm shutdown encountered {0} state error(s).", errors.Count), first);
            }
        }

        /// <summary>
        /// 获取指定类型的状态实例，不存在时抛出异常。
        /// </summary>
        /// <param name="stateType">状态类型。</param>
        /// <returns>状态实例。</returns>
        private IFsmState GetStateInternal(Type stateType)
        {
            if (states.TryGetValue(stateType, out IFsmState state))
            {
                return state;
            }

            throw new RFrameworkException(
                Utility.Text.Format("Fsm can not find state type '{0}'.", stateType.FullName));
        }
    }
}
