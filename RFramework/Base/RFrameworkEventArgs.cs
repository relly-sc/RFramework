
using System;

namespace RFramework
{
    /// <summary>
    /// 框架中包含事件数据的类的基类。
    /// </summary>
    public abstract class RFrameworkEventArgs : EventArgs, IReference
    {
        /// <summary>
        /// 初始化框架中包含事件数据的类的新实例。
        /// </summary>
        public RFrameworkEventArgs()
        {

        }

        /// <summary>
        /// 清理引用。
        /// </summary>
        public abstract void Clear();
    }
}