using RFramework.Fsm;

namespace RFramework.Procedure
{
    /// <summary>
    /// 流程状态抽象基类。
    /// 继承自 <see cref="FsmStateBase"/>，为 Procedure 模块的状态提供类型标记。
    /// Procedure 状态和普通 FSM 状态的区别：Procedure 状态通过 <see cref="IProcedureModule"/>
    /// 的 Blackboard 共享数据，且切换路径由 <see cref="ProcedureModule"/> 统一编排。
    /// </summary>
    public abstract class ProcedureStateBase : FsmStateBase
    {
    }
}
