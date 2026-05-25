
using System;
using System.Collections.Generic;

namespace RFramework
{
    /// <summary>
    /// 框架入口
    /// </summary>
    public static class RFrameworkModuleEntry
    {
        private static readonly RFrameworkLinkedList<RFrameworkModule> s_FrameworkModules = new RFrameworkLinkedList<RFrameworkModule>();


        /// <summary>
        /// 所有框架模块轮询。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间，以秒为单位。</param>
        /// <param name="realElapseSeconds">真实流逝时间，以秒为单位。</param>
        public static void Update(float elapseSeconds, float realElapseSeconds)
        {
            foreach (RFrameworkModule module in s_FrameworkModules)
            {
                module.Update(elapseSeconds, realElapseSeconds);
            }
        }

        /// <summary>
        /// 关闭并清理所有框架模块。
        /// </summary>
        public static void Shutdown()
        {
            for (LinkedListNode<RFrameworkModule> current = s_FrameworkModules.Last; current != null; current = current.Previous)
            {
                current.Value.Shutdown();
            }

            s_FrameworkModules.Clear();
            ReferencePool.ClearAll();
            Utility.Marshal.FreeCachedHGlobal();
            RFrameworkLog.SetLogHelper(null);
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

            if (!interfaceType.FullName.StartsWith("Framework.", StringComparison.Ordinal))
            {
                throw new RFrameworkException(Utility.Text.Format("You must get a UnityFramework module, but '{0}' is not.", interfaceType.FullName));
            }

            string moduleName = Utility.Text.Format("{0}.{1}", interfaceType.Namespace, interfaceType.Name.Substring(1));
            Type moduleType = Type.GetType(moduleName);
            if (moduleType == null)
            {
                throw new RFrameworkException(Utility.Text.Format("Can not find UnityFramework module type '{0}'.", moduleName));
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
            foreach (RFrameworkModule module in s_FrameworkModules)
            {
                if (module.GetType() == moduleType)
                {
                    return module;
                }
            }

            return CreateModule(moduleType);
        }

        /// <summary>
        /// 创建框架模块。
        /// </summary>
        /// <param name="moduleType">要创建的框架模块类型。</param>
        /// <returns>要创建的框架模块。</returns>
        private static RFrameworkModule CreateModule(Type moduleType)
        {
            RFrameworkModule module = (RFrameworkModule)Activator.CreateInstance(moduleType);
            if (module == null)
            {
                throw new RFrameworkException(Utility.Text.Format("Can not create module '{0}'.", moduleType.FullName));
            }

            LinkedListNode<RFrameworkModule> current = s_FrameworkModules.First;
            while (current != null)
            {
                if (module.Priority > current.Value.Priority)
                {
                    break;
                }

                current = current.Next;
            }

            if (current != null)
            {
                s_FrameworkModules.AddBefore(current, module);
            }
            else
            {
                s_FrameworkModules.AddLast(module);
            }

            return module;
        }
    }
}