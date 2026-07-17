namespace RFramework
{
    /// <summary>
    /// 流程模块实现。
    /// 通过 <see cref="IFsmModule"/> 管理一条专属的游戏流程 FSM 实例，
    /// 提供 Blackboard 跨状态共享数据和 Procedure 级别的快捷查询。
    /// </summary>
    internal sealed class ProcedureModule : RFrameworkModule, IProcedureModule
    {
        /// <summary>
        /// FSM 模块引用，用于创建和销毁流程 FSM。
        /// </summary>
        private IFsmModule fsmModule;

        /// <summary>
        /// 流程专属的 FSM 实例。
        /// </summary>
        private IFsm procedureFsm;

        /// <summary>
        /// 跨状态数据黑板。
        /// </summary>
        private ProcedureBlackboard blackboard;

        /// <summary>
        /// 是否已初始化。
        /// </summary>
        private bool isInitialized;

        /// <summary>
        /// 已注册流程状态数量。
        /// </summary>
        private int procedureCount;

        /// <summary>
        /// 初始化流程模块的新实例。
        /// </summary>
        public ProcedureModule()
        {
            fsmModule = null;
            procedureFsm = null;
            blackboard = new ProcedureBlackboard();
            isInitialized = false;
            procedureCount = 0;
        }

        /// <summary>
        /// 获取框架模块优先级。
        /// 优先级 3：低于 FsmModule（4），确保 FSM 状态切换的 Update 在此模块之前执行。
        /// </summary>
        internal override int Priority
        {
            get { return 3; }
        }

        /// <inheritdoc/>
        public ProcedureBlackboard Blackboard
        {
            get { return blackboard; }
        }

        /// <inheritdoc/>
        public ProcedureStateBase CurrentProcedure
        {
            get { return procedureFsm != null ? procedureFsm.CurrentState as ProcedureStateBase : null; }
        }

        /// <inheritdoc/>
        public float CurrentProcedureTime
        {
            get { return procedureFsm != null ? procedureFsm.CurrentStateTime : 0f; }
        }

        /// <inheritdoc/>
        public int ProcedureCount
        {
            get { return procedureCount; }
        }

        /// <inheritdoc/>
        public void Initialize(params ProcedureStateBase[] procedures)
        {
            if (isInitialized)
            {
                throw new RFrameworkException("ProcedureModule is already initialized.");
            }

            if (procedures == null || procedures.Length < 1)
            {
                throw new RFrameworkException("Procedure states are invalid.");
            }

            fsmModule = RFrameworkModuleEntry.GetModule<IFsmModule>();

            // 将 ProcedureStateBase[] 转换为 IFsmState[]
            IFsmState[] states = new IFsmState[procedures.Length];
            for (int i = 0; i < procedures.Length; i++)
            {
                states[i] = procedures[i];
            }

            procedureFsm = fsmModule.CreateFsm(this, states);
            procedureCount = procedures.Length;
            isInitialized = true;
        }

        /// <inheritdoc/>
        public void StartProcedure<T>() where T : ProcedureStateBase
        {
            if (!isInitialized)
            {
                throw new RFrameworkException("ProcedureModule is not initialized. Call Initialize() first.");
            }

            procedureFsm.Start<T>();
        }

        /// <inheritdoc/>
        public void ChangeProcedure<T>() where T : ProcedureStateBase
        {
            if (!isInitialized)
            {
                throw new RFrameworkException("ProcedureModule is not initialized. Call Initialize() first.");
            }

            procedureFsm.ChangeState<T>();
        }

        /// <inheritdoc/>
        public T GetProcedure<T>() where T : ProcedureStateBase
        {
            if (procedureFsm == null)
            {
                return null;
            }

            return procedureFsm.GetState<T>();
        }

        /// <inheritdoc/>
        public bool HasProcedure<T>() where T : ProcedureStateBase
        {
            if (procedureFsm == null)
            {
                return false;
            }

            return procedureFsm.HasState<T>();
        }

        /// <summary>
        /// 模块轮询。无操作——FSM 的 Update 由 FsmModule 统一驱动。
        /// </summary>
        internal override void Update(float elapseSeconds, float realElapseSeconds)
        {
        }

        /// <summary>
        /// 关闭并清理流程模块。
        /// </summary>
        internal override void Shutdown()
        {
            if (procedureFsm != null)
            {
                fsmModule.DestroyFsm(procedureFsm);
                procedureFsm = null;
            }

            blackboard.Clear();
            procedureCount = 0;
            isInitialized = false;
        }
    }
}
