using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RFramework.Event;
using RFramework.Resource;

namespace RFramework.Scene
{
    /// <summary>
    /// 场景模块核心实现。以 GF SceneManager 为蓝本，
    /// 提供场景状态追踪、防并发加载/卸载、事件分发。
    /// 实际加载委托 IResourceModule。
    /// </summary>
    internal sealed class SceneModule : RFrameworkModule, ISceneModule
    {
        /// <summary>
        /// 已加载的场景名称列表。
        /// </summary>
        private readonly List<string> loadedSceneNames = new List<string>();

        /// <summary>
        /// 正在加载中的场景名称列表。
        /// </summary>
        private readonly List<string> loadingSceneNames = new List<string>();

        /// <summary>
        /// 正在卸载中的场景名称列表。
        /// </summary>
        private readonly List<string> unloadingSceneNames = new List<string>();

        /// <summary>
        /// 资源模块引用，用于场景加载/卸载。
        /// </summary>
        private IResourceModule resourceModule;

        /// <summary>
        /// 事件模块引用，用于分发场景事件。
        /// </summary>
        private IEventModule eventModule;

        /// <summary>
        /// 获取框架模块优先级。
        /// SceneModule Priority=15，在 Resource(20) 之后。
        /// </summary>
        internal override int Priority
        {
            get
            {
                return 15;
            }
        }

        /// <summary>
        /// 获取当前主场景名称（最后一个以 Single 模式加载的场景）。
        /// </summary>
        public string CurrentSceneName { get; private set; }

        /// <summary>
        /// 获取已加载的场景数量。
        /// </summary>
        public int LoadedSceneCount => loadedSceneNames.Count;

        /// <summary>
        /// 获取正在加载的场景数量。
        /// </summary>
        public int LoadingSceneCount => loadingSceneNames.Count;

        /// <summary>
        /// 设置依赖模块引用（由 SceneComponent 在 Awake 中注入）。
        /// </summary>
        /// <param name="resourceModule">资源模块。</param>
        /// <param name="eventModule">事件模块。</param>
        public void SetDependencies(IResourceModule resourceModule, IEventModule eventModule)
        {
            this.resourceModule = resourceModule;
            this.eventModule = eventModule;
        }

        /// <summary>
        /// 异步加载场景。同一场景不可重复加载（需先 UnloadSceneAsync）。
        /// </summary>
        /// <param name="assetName">场景资源路径。</param>
        /// <param name="sceneMode">加载模式：0=Single 替换当前场景，1=Additive 叠加到当前场景（与 UnityEngine.SceneManagement.LoadSceneMode 值一致）。</param>
        /// <param name="activateOnLoad">是否加载完成后立即激活。</param>
        /// <param name="priority">加载优先级。</param>
        /// <param name="onProgress">进度回调（0~1），可为 null。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <param name="ct">取消令牌。</param>
        public async Task LoadSceneAsync(string assetName, int sceneMode = 0,
            bool activateOnLoad = true, uint priority = 0, IProgress<float> onProgress = null,
            object userData = null, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(assetName))
            {
                throw new RFrameworkException("Scene asset name is invalid.");
            }

            if (resourceModule == null)
            {
                throw new RFrameworkException("Resource module is not set.");
            }

            // Unity scene operations cannot be safely rolled back after they
            // have started. Cancellation is therefore an admission check,
            // performed before this module mutates its loading-state ledger.
            ct.ThrowIfCancellationRequested();

            if (loadedSceneNames.Contains(assetName))
            {
                throw new RFrameworkException($"Scene '{assetName}' is already loaded.");
            }

            if (loadingSceneNames.Contains(assetName))
            {
                throw new RFrameworkException($"Scene '{assetName}' is already loading.");
            }

            if (unloadingSceneNames.Contains(assetName))
            {
                throw new RFrameworkException($"Scene '{assetName}' is unloading.");
            }

            double startTimestamp = DateTime.UtcNow.Ticks;
            loadingSceneNames.Add(assetName);

            try
            {
                await resourceModule.LoadSceneAsync(assetName, sceneMode, activateOnLoad, priority, onProgress);

                loadingSceneNames.Remove(assetName);
                loadedSceneNames.Add(assetName);

                // Single 模式替换当前主场景
                if (sceneMode == 0)
                {
                    string previousScene = CurrentSceneName;
                    CurrentSceneName = assetName;

                    // 移除被替换的旧场景
                    if (previousScene != null)
                    {
                        loadedSceneNames.Remove(previousScene);
                    }
                }

                float duration = (float)(DateTime.UtcNow.Ticks - startTimestamp) / 10000000f;

                // 分发成功事件
                if (eventModule != null)
                {
                    eventModule.Fire(new LoadSceneSuccessEvent(assetName, duration, userData));
                }
            }
            catch (Exception ex)
            {
                loadingSceneNames.Remove(assetName);

                // 分发失败事件
                if (eventModule != null)
                {
                    eventModule.Fire(new LoadSceneFailureEvent(assetName, ex.Message, userData));
                }

                throw;
            }
        }

        /// <summary>
        /// 异步卸载场景。场景必须已加载且未在卸载中。
        /// </summary>
        /// <param name="assetName">场景资源路径。</param>
        /// <param name="userData">用户自定义数据。</param>
        public async Task UnloadSceneAsync(string assetName, object userData = null)
        {
            if (string.IsNullOrEmpty(assetName))
            {
                throw new RFrameworkException("Scene asset name is invalid.");
            }

            if (resourceModule == null)
            {
                throw new RFrameworkException("Resource module is not set.");
            }

            if (unloadingSceneNames.Contains(assetName))
            {
                throw new RFrameworkException($"Scene '{assetName}' is already unloading.");
            }

            if (loadingSceneNames.Contains(assetName))
            {
                throw new RFrameworkException($"Scene '{assetName}' is loading.");
            }

            if (!loadedSceneNames.Contains(assetName))
            {
                throw new RFrameworkException($"Scene '{assetName}' is not loaded.");
            }

            unloadingSceneNames.Add(assetName);

            try
            {
                await resourceModule.UnloadSceneAsync(assetName);

                unloadingSceneNames.Remove(assetName);
                loadedSceneNames.Remove(assetName);

                // 如果卸载的是当前主场景，清空引用
                if (CurrentSceneName == assetName)
                {
                    CurrentSceneName = null;
                }

                // 分发卸载成功事件
                if (eventModule != null)
                {
                    eventModule.Fire(new UnloadSceneSuccessEvent(assetName, userData));
                }
            }
            catch (Exception)
            {
                unloadingSceneNames.Remove(assetName);
                throw;
            }
        }

        /// <summary>
        /// 判断场景是否已加载。
        /// </summary>
        /// <param name="assetName">场景资源路径。</param>
        /// <returns>是否已加载。</returns>
        public bool IsLoaded(string assetName)
        {
            return loadedSceneNames.Contains(assetName);
        }

        /// <summary>
        /// 判断场景是否正在加载中。
        /// </summary>
        /// <param name="assetName">场景资源路径。</param>
        /// <returns>是否正在加载。</returns>
        public bool IsLoading(string assetName)
        {
            return loadingSceneNames.Contains(assetName);
        }

        /// <summary>
        /// 判断场景是否正在卸载中。
        /// </summary>
        /// <param name="assetName">场景资源路径。</param>
        /// <returns>是否正在卸载。</returns>
        public bool IsUnloading(string assetName)
        {
            return unloadingSceneNames.Contains(assetName);
        }

        /// <inheritdoc cref="ISceneModule.GetLoadedSceneNames()"/>
        public string[] GetLoadedSceneNames()
        {
            return loadedSceneNames.ToArray();
        }

        /// <inheritdoc cref="ISceneModule.GetLoadedSceneNames(List{string})"/>
        public void GetLoadedSceneNames(List<string> results)
        {
            if (results == null)
            {
                throw new RFrameworkException("Results is invalid.");
            }

            results.Clear();
            for (int i = 0; i < loadedSceneNames.Count; i++)
            {
                results.Add(loadedSceneNames[i]);
            }
        }

        /// <inheritdoc cref="ISceneModule.GetLoadingSceneNames()"/>
        public string[] GetLoadingSceneNames()
        {
            return loadingSceneNames.ToArray();
        }

        /// <inheritdoc cref="ISceneModule.GetLoadingSceneNames(List{string})"/>
        public void GetLoadingSceneNames(List<string> results)
        {
            if (results == null)
            {
                throw new RFrameworkException("Results is invalid.");
            }

            results.Clear();
            for (int i = 0; i < loadingSceneNames.Count; i++)
            {
                results.Add(loadingSceneNames[i]);
            }
        }

        /// <inheritdoc cref="ISceneModule.GetUnloadingSceneNames()"/>
        public string[] GetUnloadingSceneNames()
        {
            return unloadingSceneNames.ToArray();
        }

        /// <inheritdoc cref="ISceneModule.GetUnloadingSceneNames(List{string})"/>
        public void GetUnloadingSceneNames(List<string> results)
        {
            if (results == null)
            {
                throw new RFrameworkException("Results is invalid.");
            }

            results.Clear();
            for (int i = 0; i < unloadingSceneNames.Count; i++)
            {
                results.Add(unloadingSceneNames[i]);
            }
        }

        /// <summary>
        /// 模块轮询更新。当前无需每帧操作（场景加载/卸载由 IResourceModule 异步驱动）。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间。</param>
        /// <param name="realElapseSeconds">实际流逝时间。</param>
        internal override void Update(float elapseSeconds, float realElapseSeconds)
        {
        }

        /// <summary>
        /// 模块关闭。卸载所有已加载场景并清理状态。
        /// </summary>
        internal override void Shutdown()
        {
            // 关闭阶段跳过异步卸载：本模块优先级(15)低于 Resource(20)，
            // 若在此 fire-and-forget 触发卸载，会与 ResourceModule 的 Shutdown/资源销毁
            // 形成并发竞态，且底层辅助器可能在卸载完成前即被销毁导致异常被吞。
            // 因此仅清理场景簿记，底层场景句柄由 ResourceModule.Destroy 统一回收。
            loadedSceneNames.Clear();
            loadingSceneNames.Clear();
            unloadingSceneNames.Clear();
            CurrentSceneName = null;
        }
    }
}
