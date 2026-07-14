namespace RFramework
{
    /// <summary>
    /// 有限状态机模块接口。
    /// 管理多个 <see cref="IFsm"/> 实例的创建、销毁和 Update 驱动。
    /// </summary>
    public interface IFsmModule
    {
        /// <summary>
        /// 获取当前活跃的 FSM 实例总数。
        /// </summary>
        int FsmCount { get; }

        /// <summary>
        /// 创建一个 FSM 实例。
        /// </summary>
        /// <typeparam name="TOwner">状态机拥有者类型。</typeparam>
        /// <param name="owner">状态机拥有者实例。</param>
        /// <param name="states">该 FSM 包含的所有状态列表。</param>
        /// <returns>创建的 FSM 实例。</returns>
        IFsm CreateFsm<TOwner>(TOwner owner, params IFsmState[] states) where TOwner : class;

        /// <summary>
        /// 销毁指定 FSM 实例。
        /// 会依次调用当前状态的 OnLeave(true) 和所有状态的 OnDestroy()。
        /// </summary>
        /// <param name="fsm">要销毁的 FSM 实例。</param>
        /// <returns>成功销毁返回 true，FSM 不存在返回 false。</returns>
        bool DestroyFsm(IFsm fsm);
    }
}
