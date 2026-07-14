namespace RFramework
{
    /// <summary>
    /// UI 表单接口，描述一个 UI 面板的状态和生命周期。
    /// Library 层用 object 替代 GameObject，由 Runtime 层 Helper 强转。
    /// </summary>
    public interface IUIForm
    {
        /// <summary>
        /// 获取 UI 资源路径。
        /// </summary>
        string AssetName { get; }

        /// <summary>
        /// 获取 UI 表单实例对象（Unity 层为 GameObject）。
        /// </summary>
        object Handle { get; }

        /// <summary>
        /// 获取窗口层级（数值越大越靠前）。
        /// </summary>
        int WindowLayer { get; }

        /// <summary>
        /// 获取是否为全屏窗口（全屏时自动隐藏被覆盖窗口）。
        /// </summary>
        bool FullScreen { get; }

        /// <summary>
        /// 获取 UI 是否已打开。
        /// </summary>
        bool IsOpened { get; }

        /// <summary>
        /// UI 初始化回调。资源实例化后首次创建时调用。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        void OnInit(object userData);

        /// <summary>
        /// UI 打开回调。每次显示时调用。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        void OnOpen(object userData);

        /// <summary>
        /// UI 暂停回调。被其他全屏 UI 覆盖时调用。
        /// </summary>
        void OnPause();

        /// <summary>
        /// UI 恢复回调。覆盖的 UI 关闭后恢复时调用。
        /// </summary>
        void OnResume();

        /// <summary>
        /// UI 关闭回调。关闭时调用。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        void OnClose(object userData);

        /// <summary>
        /// UI 轮询回调。每帧调用。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间。</param>
        /// <param name="realElapseSeconds">实际流逝时间。</param>
        void OnUpdate(float elapseSeconds, float realElapseSeconds);
    }
}
