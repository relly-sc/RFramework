
using System;
using System.Collections.Generic;
using System.Text;

namespace RFramework
{
    /// <summary>
    /// 框架模块入口，管理所有 RFrameworkModule 的生命周期。
    /// </summary>
    public static class RFrameworkModuleEntry
    {
        private static readonly LinkedList<RFrameworkModule> frameworkModules = new LinkedList<RFrameworkModule>();

        /// <summary>
        /// 所有框架模块轮询。
        /// 按 Priority 降序遍历：优先级高的模块先执行 Update。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间，以秒为单位。</param>
        /// <param name="realElapseSeconds">真实流逝时间，以秒为单位。</param>
        public static void Update(float elapseSeconds, float realElapseSeconds)
        {
            // 逐模块隔离：单个模块 Update 抛异常不影响其余模块继续轮询；收集后统一上报
            List<Exception> errors = null;
            for (LinkedListNode<RFrameworkModule> node = frameworkModules.Last; node != null; node = node.Previous)
            {
                try
                {
                    node.Value.Update(elapseSeconds, realElapseSeconds);
                }
                catch (Exception ex)
                {
                    (errors ??= new List<Exception>()).Add(ex);
                }
            }

            if (errors != null)
            {
                ThrowAggregatedErrors("update", errors);
            }
        }

        /// <summary>
        /// 关闭并清理所有框架模块。
        /// 按 Priority 升序遍历：优先级低的模块先释放。
        /// </summary>
        public static void Shutdown()
        {
            List<Exception> errors = null;
            for (LinkedListNode<RFrameworkModule> node = frameworkModules.First; node != null; node = node.Next)
            {
                try
                {
                    node.Value.Shutdown();
                }
                catch (Exception ex)
                {
                    // 单个模块关闭异常不影响其余模块继续释放；收集后统一上报
                    (errors ??= new List<Exception>()).Add(ex);
                }
            }

            frameworkModules.Clear();
            Utility.Marshal.FreeCachedHGlobal();

            // 聚合异常必须在 SetLogHelper(null) 之前抛出，
            // 确保 BaseComponent.OnDestroy 经 Log 输出时日志辅助器仍有效
            if (errors != null)
            {
                ThrowAggregatedErrors("shutdown", errors);
            }

            RFrameworkLog.SetLogHelper(null);
        }

        /// <summary>
        /// 将多个模块异常聚合并内联各自的完整文本（含堆栈），便于上层定位根因。
        /// 首个异常作为包装异常的根因（inner）传出，避免丢失原始异常信息。
        /// </summary>
        /// <param name="phase">发生阶段（"update" 或 "shutdown"）。</param>
        /// <param name="errors">收集到的模块异常列表。</param>
        private static void ThrowAggregatedErrors(string phase, List<Exception> errors)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Utility.Text.Format(
                "Framework {0} encountered {1} module error(s):{2}", phase, errors.Count, Environment.NewLine));
            for (int i = 0; i < errors.Count; i++)
            {
                sb.Append(Utility.Text.Format(
                    "[Error {0}] {1}{2}", i + 1, errors[i].ToString(), Environment.NewLine));
            }

            throw new RFrameworkException(sb.ToString(), errors[0]);
        }

        /// <summary>
        /// 获取框架模块。
        /// </summary>
        /// <typeparam name="T">要获取的框架模块类型。</typeparam>
        /// <returns>要获取的框架模块。</returns>
        /// <remarks>如果要获取的框架模块不存在，则自动创建该框架模块。</remarks>
        public static T GetModule<T>() where T : class
        {
            Type interfaceType = typeof(T);
            if (!interfaceType.IsInterface)
            {
                throw new RFrameworkException(Utility.Text.Format("You must get module by interface, but '{0}' is not.", interfaceType.FullName));
            }

            if (!interfaceType.FullName.StartsWith("RFramework.", StringComparison.Ordinal))
            {
                throw new RFrameworkException(Utility.Text.Format("You must get a RFramework module, but '{0}' is not.", interfaceType.FullName));
            }

            string moduleName = Utility.Text.Format("{0}.{1}", interfaceType.Namespace, interfaceType.Name.Substring(1));
            Type moduleType = Type.GetType(moduleName);
            if (moduleType == null)
            {
                throw new RFrameworkException(Utility.Text.Format("Can not find RFramework module type '{0}'.", moduleName));
            }

            return GetModule(moduleType) as T;
        }

        /// <summary>
        /// 获取框架模块。
        /// </summary>
        /// <param name="moduleType">要获取的框架模块类型。</param>
        /// <returns>要获取的框架模块。</returns>
        /// <remarks>如果要获取的框架模块不存在，则自动创建该框架模块。</remarks>
        private static RFrameworkModule GetModule(Type moduleType)
        {
            for (LinkedListNode<RFrameworkModule> node = frameworkModules.First; node != null; node = node.Next)
            {
                if (node.Value.GetType() == moduleType)
                {
                    return node.Value;
                }
            }

            return CreateModule(moduleType);
        }

        /// <summary>
        /// 创建框架模块，并按 Priority 插入链表保持升序。
        /// 遍历链表找到第一个 Priority 大于当前模块的节点，插入其前面；
        /// 若没有更大者则追加到末尾。
        /// 插入即有序，无需额外 Sort()。
        /// </summary>
        /// <param name="moduleType">要创建的框架模块类型。</param>
        /// <returns>创建的框架模块实例。</returns>
        private static RFrameworkModule CreateModule(Type moduleType)
        {
            RFrameworkModule module = (RFrameworkModule)Activator.CreateInstance(moduleType);
            if (module == null)
            {
                throw new RFrameworkException(Utility.Text.Format("Can not create module '{0}'.", moduleType.FullName));
            }

            // 按 Priority 升序插入：遍历找到第一个 Priority 更大的节点，插到它前面
            LinkedListNode<RFrameworkModule> current = frameworkModules.First;
            while (current != null)
            {
                if (current.Value.Priority > module.Priority)
                {
                    frameworkModules.AddBefore(current, module);
                    return module;
                }

                current = current.Next;
            }

            // 没找到更大的，追加到末尾
            frameworkModules.AddLast(module);
            return module;
        }
    }
}
