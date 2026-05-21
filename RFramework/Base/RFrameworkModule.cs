
namespace RFramework
{
    /// <summary>
    /// 框架模块抽象类
    /// </summary>
    internal abstract class RFrameworkModule
    {
        /// <summary>
        /// 获取游戏框架模块优先级。
        /// </summary>
        /// <remarks>优先级较高的模块会优先轮询，并且关闭操作会后进行。</remarks>
        internal virtual int Priority
        {
            get
            {
                return 0;
            }
        }

        /// <summary>
        /// 框架模块轮询。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间，以秒为单位。</param>
        /// <param name="realElapseSeconds">真实流逝时间，以秒为单位。</param>
        internal abstract void Update(float elapseSeconds, float realElapseSeconds);

        /// <summary>
        /// 关闭并清理框架模块。
        /// </summary>
        internal abstract void Shutdown();
    }
}