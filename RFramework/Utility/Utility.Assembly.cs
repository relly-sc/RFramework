
using System;

namespace RFramework
{
    /// <summary>
    /// 框架通用工具入口。
    /// </summary>
    public static partial class Utility
    {
        /// <summary>
        /// 程序集相关的实用函数。
        /// </summary>
        public static class Assembly
        {
            /// <summary>
            /// 按完整类型名查找当前应用域中已加载的类型。
            /// </summary>
            /// <param name="typeName">包含命名空间的完整类型名，或程序集限定名。</param>
            /// <returns>找到的类型；不存在时返回 null。</returns>
            public static Type GetType(string typeName)
            {
                if (string.IsNullOrWhiteSpace(typeName))
                {
                    throw new RFrameworkException("Type name cannot be empty.");
                }

                Type resolvedType = Type.GetType(typeName, false);
                if (resolvedType != null)
                {
                    return resolvedType;
                }

                foreach (System.Reflection.Assembly loadedAssembly
                    in AppDomain.CurrentDomain.GetAssemblies())
                {
                    resolvedType = loadedAssembly.GetType(typeName, false, false);
                    if (resolvedType != null)
                    {
                        return resolvedType;
                    }
                }

                return null;
            }
        }
    }
}
