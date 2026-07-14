using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RFramework.Event;
using RFramework.Pool;
using RFramework.Resource;

namespace RFramework.UI
{
    /// <summary>
    /// UI 模块核心实现。融合 GF UIManager 和 UniWindow 的设计：
    /// - 单一窗口栈 + 层级排序（参考 UniWindow）
    /// - FullScreen 自动隐藏被覆盖窗口
    /// - 对象池复用 UI 实例
    /// - Task 异步加载
    /// </summary>
    internal sealed class UIModule : RFrameworkModule, IUIModule
    {
        /// <summary>
        /// UI 辅助器。
        /// </summary>
        private IUIHelper uiHelper;

        /// <summary>
        /// 资源模块引用。
        /// </summary>
        private IResourceModule resourceModule;

        /// <summary>
        /// 事件模块引用。
        /// </summary>
        private IEventModule eventModule;

        /// <summary>
        /// 对象池模块引用。
        /// </summary>
        private IPoolModule poolModule;

        /// <summary>
        /// 窗口栈（按层级排序存储）。
        /// </summary>
        private readonly List<IUIForm> windowStack = new List<IUIForm>();

        /// <summary>
        /// 已打开 UI 的索引（assetName → IUIForm）。
        /// </summary>
        private readonly Dictionary<string, IUIForm> uiForms = new Dictionary<string, IUIForm>();

        // Keep the ResourceModule reference alongside the instantiated form. The form handle alone
        // is insufficient to return the asset reference when the form is closed.
        private readonly Dictionary<string, object> uiAssets = new Dictionary<string, object>();

        private readonly HashSet<IUIForm> pausedUIForms = new HashSet<IUIForm>();

        /// <summary>
        /// 正在加载中的 UI 资源路径集合。
        /// </summary>
        private readonly HashSet<string> loadingUIForms = new HashSet<string>();

        /// <summary>
        /// 正在关闭（加载中被取消）的 UI 资源路径集合。
        /// CloseUIForm 在 UI 仍加载时记录，OpenUIFormAsync 加载完成后据此放弃打开。
        /// </summary>
        private readonly HashSet<string> abortedUIForms = new HashSet<string>();

        /// <summary>
        /// 模块是否已关闭。关闭后加载完成的 UI 不再打开。
        /// </summary>
        private bool isShutdown;

        /// <summary>
        /// 获取框架模块优先级。
        /// UIModule Priority=30，在 Entity(25) 之后。
        /// </summary>
        internal override int Priority
        {
            get
            {
                return 30;
            }
        }

        /// <summary>
        /// 获取当前打开的 UI 数量。
        /// </summary>
        public int UIFormCount => uiForms.Count;

        /// <summary>
        /// 设置 UI 辅助器。
        /// </summary>
        public void SetHelper(IUIHelper helper)
        {
            uiHelper = helper;
        }

        /// <summary>
        /// 设置依赖模块引用。
        /// </summary>
        public void SetDependencies(IResourceModule resourceModule, IEventModule eventModule, IPoolModule poolModule)
        {
            this.resourceModule = resourceModule;
            this.eventModule = eventModule;
            this.poolModule = poolModule;
        }

        /// <summary>
        /// 异步打开 UI。
        /// </summary>
        public async Task<IUIForm> OpenUIFormAsync(string assetName, int windowLayer = 0,
            bool fullScreen = false, uint priority = 0, object userData = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(assetName))
            {
                throw new RFrameworkException("UI asset name is invalid.");
            }

            if (uiHelper == null)
            {
                throw new RFrameworkException("UI helper is not set.");
            }

            if (resourceModule == null)
            {
                throw new RFrameworkException("Resource module is not set.");
            }

            if (uiForms.ContainsKey(assetName))
            {
                throw new RFrameworkException($"UI form '{assetName}' is already opened.");
            }

            if (loadingUIForms.Contains(assetName))
            {
                throw new RFrameworkException($"UI form '{assetName}' is already loading.");
            }

            double startTimestamp = DateTime.UtcNow.Ticks;
            loadingUIForms.Add(assetName);

            object uiAsset = null;
            object uiInstance = null;
            IUIForm uiForm = null;
            try
            {
                // 通过 IResourceModule 加载 UI Prefab
                uiAsset = await resourceModule.LoadAssetAsync<object>(assetName, priority, ct);

                loadingUIForms.Remove(assetName);

                // 加载完成后再次校验：取消令牌触发 / 模块已关闭 / 加载期间被 CloseUIForm 取消。
                // 任一成立则放弃打开，并释放已加载但未使用的资源引用计数，避免泄漏。
                if (ct.IsCancellationRequested || isShutdown || abortedUIForms.Remove(assetName))
                {
                    resourceModule.UnloadAsset<object>(assetName);
                    uiAsset = null;
                    throw new OperationCanceledException();
                }

                // 实例化 UI 对象
                uiInstance = uiHelper.InstantiateUI(uiAsset);
                uiForm = uiHelper.CreateUIForm(uiInstance, assetName, windowLayer, fullScreen);
                if (uiForm == null)
                {
                    throw new RFrameworkException($"UI helper failed to create UI form '{assetName}'.");
                }

                // 生命周期：OnInit → OnOpen
                uiForm.OnInit(userData);
                uiForms.Add(assetName, uiForm);
                uiAssets.Add(assetName, uiAsset);

                // 按层级插入窗口栈
                InsertToStack(uiForm);

                uiForm.OnOpen(userData);

                // 全屏判定：隐藏被覆盖的窗口
                ApplyFullScreenVisibility();

                float duration = (float)(DateTime.UtcNow.Ticks - startTimestamp) / 10000000f;

                if (eventModule != null)
                {
                    eventModule.Fire(new OpenUIFormSuccessEvent(assetName, uiForm, duration, userData));
                }

                return uiForm;
            }
            catch (Exception ex)
            {
                if (uiForm != null)
                {
                    uiForms.Remove(assetName);
                    windowStack.Remove(uiForm);
                    pausedUIForms.Remove(uiForm);
                }

                uiAssets.Remove(assetName);

                if (uiInstance != null)
                {
                    uiHelper?.ReleaseUI(uiInstance);
                }

                if (uiAsset != null)
                {
                    resourceModule?.UnloadAsset<object>(assetName);
                }

                loadingUIForms.Remove(assetName);
                abortedUIForms.Remove(assetName);

                // 取消（含加载中关闭）属正常流程，不视为失败事件
                if (!(ex is OperationCanceledException) && eventModule != null)
                {
                    eventModule.Fire(new OpenUIFormFailureEvent(assetName, ex.Message, userData));
                }

                throw;
            }
        }

        /// <summary>
        /// 关闭 UI。
        /// </summary>
        public void CloseUIForm(string assetName, object userData = null)
        {
            // 若 UI 仍在加载中：标记取消，待加载完成后由 OpenUIFormAsync 放弃打开
            if (loadingUIForms.Contains(assetName))
            {
                abortedUIForms.Add(assetName);
                return;
            }

            if (!uiForms.TryGetValue(assetName, out IUIForm uiForm))
            {
                return;
            }

            // 生命周期：OnClose
            uiForm.OnClose(userData);

            // 从字典和栈中移除
            uiForms.Remove(assetName);
            windowStack.Remove(uiForm);

            // 获取实例对象归还对象池或销毁
            object uiInstance = uiForm.Handle;
            uiHelper.ReleaseUI(uiInstance);
            pausedUIForms.Remove(uiForm);
            if (uiAssets.TryGetValue(assetName, out object uiAsset))
            {
                uiAssets.Remove(assetName);
                resourceModule.UnloadAsset<object>(assetName);
            }

            // 恢复被覆盖窗口的可见性
            ApplyFullScreenVisibility();

            if (eventModule != null)
            {
                eventModule.Fire(new CloseUIFormCompleteEvent(assetName, userData));
            }
        }

        /// <summary>
        /// 关闭所有已打开的 UI。
        /// </summary>
        public void CloseAllUIForms(object userData = null)
        {
            // Keep loading entries until their continuations observe this
            // marker. Clearing the set would allow a late load to reopen UI
            // after this bulk-close operation has completed.
            foreach (string assetName in loadingUIForms)
            {
                abortedUIForms.Add(assetName);
            }

            // 倒序关闭（从栈顶开始）
            for (int i = windowStack.Count - 1; i >= 0; i--)
            {
                IUIForm uiForm = windowStack[i];
                uiForm.OnClose(userData);
                uiHelper.ReleaseUI(uiForm.Handle);
                pausedUIForms.Remove(uiForm);
            }

            foreach (KeyValuePair<string, object> uiAsset in uiAssets)
            {
                resourceModule.UnloadAsset<object>(uiAsset.Key);
            }

            windowStack.Clear();
            uiForms.Clear();
            uiAssets.Clear();
        }

        /// <summary>
        /// 判断 UI 是否已打开。
        /// </summary>
        public bool HasUIForm(string assetName)
        {
            return uiForms.ContainsKey(assetName);
        }

        /// <summary>
        /// 获取 UI 表单。
        /// </summary>
        public IUIForm GetUIForm(string assetName)
        {
            if (uiForms.TryGetValue(assetName, out IUIForm uiForm))
            {
                return uiForm;
            }

            return null;
        }

        /// <summary>
        /// 获取所有已打开的 UI。
        /// </summary>
        public IUIForm[] GetAllUIForms()
        {
            IUIForm[] results = new IUIForm[windowStack.Count];
            for (int i = 0; i < windowStack.Count; i++)
            {
                results[i] = windowStack[i];
            }

            return results;
        }

        /// <summary>
        /// 获取所有正在加载中的 UI 资源路径。
        /// </summary>
        public string[] GetAllLoadingUIFormAssetNames()
        {
            string[] results = new string[loadingUIForms.Count];
            int index = 0;
            foreach (string assetName in loadingUIForms)
            {
                results[index++] = assetName;
            }

            return results;
        }

        /// <summary>
        /// 判断 UI 是否正在加载中。
        /// </summary>
        public bool IsLoadingUIForm(string assetName)
        {
            return loadingUIForms.Contains(assetName);
        }

        /// <summary>
        /// 按层级将窗口插入栈中的正确位置。
        /// 同层窗口按插入顺序排列（后插入的在后面，渲染在更上层）。
        /// </summary>
        private void InsertToStack(IUIForm uiForm)
        {
            int targetLayer = uiForm.WindowLayer;
            int insertIndex = windowStack.Count;

            // 找到同层或更高层的插入位置
            for (int i = 0; i < windowStack.Count; i++)
            {
                if (windowStack[i].WindowLayer > targetLayer)
                {
                    insertIndex = i;
                    break;
                }
            }

            windowStack.Insert(insertIndex, uiForm);
        }

        /// <summary>
        /// 应用全屏窗口可见性规则（参考 UniWindow.OnSetWindowVisible）。
        /// 从栈顶往下扫描，遇到 FullScreen 窗口后将其余窗口设为暂停。
        /// </summary>
        private void ApplyFullScreenVisibility()
        {
            bool shouldPause = false;

            for (int i = windowStack.Count - 1; i >= 0; i--)
            {
                IUIForm uiForm = windowStack[i];

                if (uiForm.FullScreen && !shouldPause)
                {
                    // 第一个全屏窗口：自身可见，后续窗口暂停
                    shouldPause = true;
                    continue;
                }

                if (shouldPause)
                {
                    // 被全屏窗口覆盖：暂停
                    if (uiForm.IsOpened && pausedUIForms.Add(uiForm))
                    {
                        uiForm.OnPause();
                    }
                }
                else if (pausedUIForms.Remove(uiForm))
                {
                    // Only resume forms that were actually paused by this module.
                    uiForm.OnResume();
                }
            }
        }

        /// <summary>
        /// 模块轮询更新。驱动所有已打开 UI 的 OnUpdate。
        /// </summary>
        internal override void Update(float elapseSeconds, float realElapseSeconds)
        {
            for (int i = 0; i < windowStack.Count; i++)
            {
                windowStack[i].OnUpdate(elapseSeconds, realElapseSeconds);
            }
        }

        /// <summary>
        /// 模块关闭。关闭所有 UI 并清理状态。
        /// </summary>
        internal override void Shutdown()
        {
            isShutdown = true;
            CloseAllUIForms();
            windowStack.Clear();
            uiForms.Clear();
            uiAssets.Clear();
            pausedUIForms.Clear();
            loadingUIForms.Clear();
            abortedUIForms.Clear();
        }
    }
}
