namespace RFramework
{
    /// <summary>
    /// 流程模块接口。
    /// 通过 <see cref="IFsmModule"/> 管理一条游戏流程的 FSM 实例，
    /// 提供 Blackboard 跨状态共享数据和 Procedure 级别的快捷查询。
    /// </summary>
    public interface IProcedureModule
    {
        /// <summary>
        /// 获取跨状态数据黑板。
        /// </summary>
        ProcedureBlackboard Blackboard { get; }

        /// <summary>
        /// 获取当前运行中的流程状态。
        /// </summary>
        ProcedureStateBase CurrentProcedure { get; }

        /// <summary>
        /// 获取当前流程状态已运行的持续时间（秒）。
        /// </summary>
        float CurrentProcedureTime { get; }

        /// <summary>
        /// 初始化流程模块并注册所有流程状态。
        /// 必须在 StartProcedure 之前调用。
        /// </summary>
        /// <param name="procedures">所有流程状态实例列表。</param>
        void Initialize(params ProcedureStateBase[] procedures);

        /// <summary>
        /// 启动流程，进入指定初始状态。
        /// 只能调用一次，在 Initialize 之后调用。
        /// </summary>
        /// <typeparam name="T">初始流程状态类型，必须继承 <see cref="ProcedureStateBase"/>。</typeparam>
        void StartProcedure<T>() where T : ProcedureStateBase;

        /// <summary>
        /// 切换到指定流程状态。
        /// </summary>
        /// <typeparam name="T">目标流程状态类型。</typeparam>
        void ChangeProcedure<T>() where T : ProcedureStateBase;

        /// <summary>
        /// 获取指定类型的流程状态实例。
        /// </summary>
        /// <typeparam name="T">要获取的流程状态类型。</typeparam>
        /// <returns>状态实例，不存在时返回 null。</returns>
        T GetProcedure<T>() where T : ProcedureStateBase;

        /// <summary>
        /// 检查是否包含指定类型的流程状态。
        /// </summary>
        /// <typeparam name="T">要检查的流程状态类型。</typeparam>
        /// <returns>如果包含则返回 true。</returns>
        bool HasProcedure<T>() where T : ProcedureStateBase;
    }
}
