using System;
using System.Collections.Generic;

namespace RFramework.Fsm
{
    /// <summary>
    /// 有限状态机模块。
    /// 管理所有 FSM 实例的生命周期，统一轮询驱动。
    /// </summary>
    internal sealed class FsmModule : RFrameworkModule, IFsmModule
    {
        /// <summary>
        /// FSM 实例列表（使用 FsmBase 非泛型基类统一管理）。
        /// </summary>
        private readonly List<FsmBase> fsms;

        /// <summary>
        /// 待添加的 FSM 列表（遍历中安全添加）。
        /// </summary>
        private readonly List<FsmBase> toAdd;

        /// <summary>
        /// 待移除的 FSM 列表（遍历中安全移除）。
        /// </summary>
        private readonly List<FsmBase> toRemove;

        /// <summary>
        /// 初始化有限状态机模块的新实例。
        /// </summary>
        public FsmModule()
        {
            fsms = new List<FsmBase>();
            toAdd = new List<FsmBase>();
            toRemove = new List<FsmBase>();
        }

        /// <summary>
        /// 获取框架模块优先级。
        /// 优先级 4：低于 Event（7）、Timer（6），确保 FSM 状态切换在事件分发之后执行。
        /// </summary>
        internal override int Priority
        {
            get { return 4; }
        }

        /// <inheritdoc/>
        public int FsmCount
        {
            get { return fsms.Count; }
        }

        /// <inheritdoc/>
        public IFsm CreateFsm<TOwner>(TOwner owner, params IFsmState[] states) where TOwner : class
        {
            Fsm<TOwner> fsm = new Fsm<TOwner>(owner, states);
            toAdd.Add(fsm);
            return fsm;
        }

        /// <inheritdoc/>
        public bool DestroyFsm(IFsm fsm)
        {
            if (fsm == null)
            {
                return false;
            }

            FsmBase fsmBase = fsm as FsmBase;
            if (fsmBase == null)
            {
                return false;
            }

            if (!fsms.Contains(fsmBase) && !toAdd.Contains(fsmBase))
            {
                return false;
            }

            toRemove.Add(fsmBase);
            return true;
        }

        /// <summary>
        /// 模块轮询。统一驱动所有 FSM 实例的当前状态 Update。
        /// 使用双缓冲模式保证遍历中 FSM 的创建/销毁安全。
        /// </summary>
        internal override void Update(float elapseSeconds, float realElapseSeconds)
        {
            List<Exception> errors = null;

            // 先处理待添加
            if (toAdd.Count > 0)
            {
                for (int i = 0; i < toAdd.Count; i++)
                {
                    fsms.Add(toAdd[i]);
                }

                toAdd.Clear();
            }

            // 轮询所有活跃 FSM
            for (int i = 0; i < fsms.Count; i++)
            {
                if (!toRemove.Contains(fsms[i]))
                {
                    try
                    {
                        fsms[i].Update(elapseSeconds, realElapseSeconds);
                    }
                    catch (Exception ex)
                    {
                        (errors ??= new List<Exception>()).Add(ex);
                    }
                }
            }

            // 清理待移除
            if (toRemove.Count > 0)
            {
                for (int i = 0; i < toRemove.Count; i++)
                {
                    try
                    {
                        toRemove[i].Shutdown();
                    }
                    catch (Exception ex)
                    {
                        (errors ??= new List<Exception>()).Add(ex);
                    }
                    finally
                    {
                        fsms.Remove(toRemove[i]);
                    }
                }

                toRemove.Clear();
            }

            if (errors != null)
            {
                throw new RFrameworkException(
                    $"FsmModule update encountered {errors.Count} FSM error(s).",
                    new AggregateException(errors));
            }
        }

        /// <summary>
        /// 关闭并清理 FSM 模块，销毁所有 FSM 实例。
        /// </summary>
        internal override void Shutdown()
        {
            HashSet<FsmBase> allFsms = new HashSet<FsmBase>(fsms);
            for (int i = 0; i < toAdd.Count; i++)
            {
                allFsms.Add(toAdd[i]);
            }

            toAdd.Clear();
            toRemove.Clear();

            List<Exception> errors = null;
            try
            {
                foreach (FsmBase fsm in allFsms)
                {
                    try
                    {
                        fsm.Shutdown();
                    }
                    catch (Exception ex)
                    {
                        (errors ??= new List<Exception>()).Add(ex);
                    }
                }
            }
            finally
            {
                fsms.Clear();
            }

            if (errors != null)
            {
                throw new RFrameworkException(
                    $"FsmModule shutdown encountered {errors.Count} FSM error(s).",
                    new AggregateException(errors));
            }
        }
    }
}
