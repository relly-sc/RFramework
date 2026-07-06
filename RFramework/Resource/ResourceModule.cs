using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RFramework;
using RFramework.Event;

namespace RFramework.Resource
{
    /// <summary>
    /// 资源模块核心实现。
    /// 负责引用计数管理、并发加载去重、延迟释放调度。
    /// 所有引擎特定资源操作委托给 IResourceHelper，保持 Library 层纯 C#。
    /// </summary>
    internal sealed class ResourceModule : RFrameworkModule, IResourceModule
    {
        /// <summary>
        /// 获取框架模块优先级。
        /// 高于 Timer(10)，确保资源加载在计时器之后初始化。
        /// </summary>
        internal override int Priority
        {
            get { return 20; }
        }

        // ==================== 依赖注入 ====================

        /// <summary>资源辅助器，封装所有引擎特定资源操作</summary>
        private IResourceHelper helper;

        /// <summary>事件模块引用，用于分发加载失败事件。惰性获取，可能为 null（事件模块未就绪时）。</summary>
        private IEventModule eventModule;

        // ==================== 配置 ====================

        /// <summary>资源运行模式</summary>
        private ResourcePlayMode playMode = ResourcePlayMode.EditorSimulate;

        /// <summary>默认 CDN 地址（Host 模式使用）</summary>
        private string defaultHostServer;

        /// <summary>备用 CDN 地址</summary>
        private string fallbackHostServer;

        /// <summary>资源包裹名称</summary>
        private string packageName = "DefaultPackage";

        /// <summary>是否已完成初始化</summary>
        private bool isInitialized;

        // ==================== 资源缓存（引用计数） ====================

        /// <summary>已加载资源缓存：location → CachedAsset</summary>
        private readonly Dictionary<string, CachedAsset> loadedAssets = new Dictionary<string, CachedAsset>();

        /// <summary>正在加载中的资源并发去重：location → TCS 列表</summary>
        private readonly Dictionary<string, List<TaskCompletionSource<CachedAsset>>> pendingLoads =
            new Dictionary<string, List<TaskCompletionSource<CachedAsset>>>();

        /// <summary>已加载的场景 location 集合</summary>
        private readonly HashSet<string> loadedScenes = new HashSet<string>();

        /// <summary>待回收资源队列（UnloadAsset 后引用计数为 0 的资源）</summary>
        private readonly Queue<CachedAsset> releaseQueue = new Queue<CachedAsset>();

        // ==================== 内部类型 ====================

        /// <summary>
        /// 已加载资源的内部缓存对象。追踪引用计数，但不持有底层句柄（句柄由 IResourceHelper 管理）。
        /// </summary>
        private sealed class CachedAsset
        {
            /// <summary>资源路径</summary>
            public string Location;

            /// <summary>加载的资源对象</summary>
            public object Asset;

            /// <summary>资源类型</summary>
            public Type AssetType;

            /// <summary>引用计数</summary>
            public int RefCount;

            /// <summary>是否可以被释放（引用计数为 0）</summary>
            public bool CanRelease
            {
                get { return RefCount <= 0; }
            }
        }

        // ==================== 配置方法 ====================

        /// <summary>
        /// 设置资源辅助器（必须在 InitializeAsync 之前调用）。
        /// </summary>
        /// <param name="helper">资源辅助器实例，由 Runtime 层创建并注入。</param>
        public void SetHelper(IResourceHelper helper)
        {
            this.helper = helper ?? throw new RFrameworkException("Resource helper is invalid.");
        }

        /// <summary>
        /// 设置资源运行模式（必须在 InitializeAsync 之前调用）。
        /// </summary>
        /// <param name="playMode">资源运行模式。</param>
        public void SetPlayMode(ResourcePlayMode playMode)
        {
            if (isInitialized)
            {
                throw new InvalidOperationException("ResourceModule: Cannot change play mode after initialization.");
            }

            this.playMode = playMode;
        }

        /// <summary>
        /// 设置远程资源服务器地址（Host 模式必须）。
        /// </summary>
        /// <param name="defaultHostServer">默认 CDN 地址。</param>
        /// <param name="fallbackHostServer">备用 CDN 地址。</param>
        public void SetRemoteServiceUrl(string defaultHostServer, string fallbackHostServer)
        {
            if (isInitialized)
            {
                throw new InvalidOperationException(
                    "ResourceModule: Cannot change remote service URL after initialization.");
            }

            this.defaultHostServer = defaultHostServer;
            this.fallbackHostServer = fallbackHostServer;
        }

        /// <summary>
        /// 设置资源包裹名称（必须在 InitializeAsync 之前调用，默认 "DefaultPackage"）。
        /// </summary>
        /// <param name="packageName">资源包裹名称。</param>
        public void SetPackageName(string packageName)
        {
            if (isInitialized)
            {
                throw new InvalidOperationException(
                    "ResourceModule: Cannot change package name after initialization.");
            }

            this.packageName = packageName ?? "DefaultPackage";
        }

        // ==================== 初始化 ====================

        /// <summary>
        /// 初始化资源系统。调用前必须先设置 PlayMode、RemoteServiceUrl 和 Helper。
        /// 异步等待资源系统初始化、资源包裹创建及文件系统挂载完成。
        /// </summary>
        public async Task InitializeAsync()
        {
            if (isInitialized)
            {
                return;
            }

            EnsureHelper();

            await helper.InitializeAsync(packageName, playMode, defaultHostServer, fallbackHostServer);
            isInitialized = true;
        }

        // ==================== 资源加载 ====================

        /// <summary>
        /// 异步加载资源。同一资源并发请求自动去重（第二个请求等待第一个完成）。
        /// 返回的资源通过引用计数管理——每调用一次 LoadAssetAsync 计数 +1。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="location">资源路径。</param>
        /// <param name="priority">加载优先级（越大越优先）。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>加载的资源对象。</returns>
        public async Task<T> LoadAssetAsync<T>(string location, uint priority = 0,
            CancellationToken ct = default) where T : class
        {
            EnsureInitialized();

            if (string.IsNullOrEmpty(location))
            {
                throw new ArgumentNullException(nameof(location));
            }

            // 1. 检查缓存命中
            if (loadedAssets.TryGetValue(location, out CachedAsset cached))
            {
                cached.RefCount++;
                return cached.Asset as T;
            }

            // 2. 并发去重：如果同一资源正在加载中，等待已有加载完成
            if (pendingLoads.TryGetValue(location, out List<TaskCompletionSource<CachedAsset>> tcsList))
            {
                TaskCompletionSource<CachedAsset> waitTcs = new TaskCompletionSource<CachedAsset>();

                // 注册取消回调：ct 被取消时自动取消等待 TCS
                using (CancellationTokenRegistration ctr = ct.CanBeCanceled
                    ? ct.Register(() => waitTcs.TrySetCanceled())
                    : default)
                {
                    tcsList.Add(waitTcs);

                    try
                    {
                        CachedAsset result = await waitTcs.Task;
                        result.RefCount++;
                        return result.Asset as T;
                    }
                    catch (OperationCanceledException)
                    {
                        tcsList.Remove(waitTcs);
                        throw;
                    }
                }
            }

            // 3. 新建加载任务
            List<TaskCompletionSource<CachedAsset>> newTcsList =
                new List<TaskCompletionSource<CachedAsset>>();
            pendingLoads[location] = newTcsList;

            try
            {
                object asset = await helper.LoadAssetAsync(location, typeof(T), priority);

                if (asset != null)
                {
                    CachedAsset newCached = new CachedAsset
                    {
                        Location = location,
                        Asset = asset,
                        AssetType = typeof(T),
                        RefCount = 1 // 初始引用计数为 1（当前调用者）
                    };

                    loadedAssets[location] = newCached;

                    // 通知所有等待者
                    foreach (TaskCompletionSource<CachedAsset> tcs in newTcsList)
                    {
                        tcs.TrySetResult(newCached);
                    }

                    return newCached.Asset as T;
                }

                // 加载失败：分发事件通知 + 抛异常
                FireLoadFailedEvent<T>(location,
                    $"LoadAssetAsync<{typeof(T).Name}> failed: {location}");

                foreach (TaskCompletionSource<CachedAsset> tcs in newTcsList)
                {
                    tcs.TrySetException(new RFrameworkException(
                        $"LoadAssetAsync<{typeof(T).Name}> failed: {location}"));
                }

                throw new RFrameworkException($"LoadAssetAsync<{typeof(T).Name}> failed: {location}");
            }
            catch (Exception ex)
            {
                // 分发失败事件（仅非取消异常；取消异常由 catch(OperationCanceledException) 处理）
                if (!(ex is OperationCanceledException))
                {
                    FireLoadFailedEvent<T>(location, ex.Message);
                }

                foreach (TaskCompletionSource<CachedAsset> tcs in newTcsList)
                {
                    tcs.TrySetCanceled();
                }

                throw;
            }
            finally
            {
                pendingLoads.Remove(location);
            }
        }

        /// <summary>
        /// 同步加载资源。由 Runtime 辅助器内部阻塞等待异步完成。
        /// 仅在确实需要同步加载时使用，不建议在热更层调用。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="location">资源路径。</param>
        /// <returns>加载的资源对象。</returns>
        public T LoadAssetSync<T>(string location) where T : class
        {
            EnsureInitialized();

            if (string.IsNullOrEmpty(location))
            {
                throw new ArgumentNullException(nameof(location));
            }

            // 检查缓存
            if (loadedAssets.TryGetValue(location, out CachedAsset cached))
            {
                cached.RefCount++;
                return cached.Asset as T;
            }

            object asset = helper.LoadAssetSync(location, typeof(T));

            if (asset == null)
            {
                FireLoadFailedEvent<T>(location, $"LoadAssetSync<{typeof(T).Name}> failed: {location}");
                throw new RFrameworkException(
                    $"LoadAssetSync<{typeof(T).Name}> failed: {location}");
            }

            CachedAsset newCached = new CachedAsset
            {
                Location = location,
                Asset = asset,
                AssetType = typeof(T),
                RefCount = 1
            };

            loadedAssets[location] = newCached;
            return newCached.Asset as T;
        }

        // ==================== 场景加载 ====================

        /// <summary>
        /// 异步加载场景。
        /// </summary>
        /// <param name="location">场景资源路径。</param>
        /// <param name="sceneMode">场景加载模式。</param>
        /// <param name="activateOnLoad">是否加载完成后立即激活。</param>
        /// <param name="priority">加载优先级。</param>
        public async Task LoadSceneAsync(string location, SceneLoadMode sceneMode = SceneLoadMode.Single,
            bool activateOnLoad = true, uint priority = 0)
        {
            EnsureInitialized();

            if (string.IsNullOrEmpty(location))
            {
                throw new ArgumentNullException(nameof(location));
            }

            try
            {
                await helper.LoadSceneAsync(location, sceneMode, activateOnLoad, priority);
                loadedScenes.Add(location);
            }
            catch (Exception ex)
            {
                SceneLoadFailedEvent failedEvent = new SceneLoadFailedEvent(location, ex.Message);
                GetEventModule()?.Fire(failedEvent);
                throw;
            }
        }

        /// <summary>
        /// 异步卸载场景。
        /// </summary>
        /// <param name="location">场景资源路径。</param>
        public async Task UnloadSceneAsync(string location)
        {
            EnsureInitialized();

            await helper.UnloadSceneAsync(location);
            loadedScenes.Remove(location);
        }

        // ==================== 资源卸载 ====================

        /// <summary>
        /// 卸载资源，引用计数 -1。当计数归零时加入待回收队列，下一帧由 Update 释放。
        /// </summary>
        /// <param name="asset">LoadAssetAsync / LoadAssetSync 返回的资源对象。</param>
        public void UnloadAsset(object asset)
        {
            if (asset == null)
            {
                return;
            }

            // 查找对应的 CachedAsset
            foreach (KeyValuePair<string, CachedAsset> kv in loadedAssets)
            {
                if (kv.Value.Asset == asset)
                {
                    kv.Value.RefCount--;

                    if (kv.Value.RefCount <= 0)
                    {
                        releaseQueue.Enqueue(kv.Value);
                        loadedAssets.Remove(kv.Key);
                    }

                    return;
                }
            }
        }

        /// <summary>
        /// 立即释放所有引用计数为 0 的资源。
        /// 同时处理待回收队列中的资源。
        /// </summary>
        public void UnloadUnusedAssets()
        {
            // 收集所有引用计数为 0 的资源
            List<CachedAsset> toRelease = new List<CachedAsset>();
            foreach (KeyValuePair<string, CachedAsset> kv in loadedAssets)
            {
                if (kv.Value.CanRelease)
                {
                    toRelease.Add(kv.Value);
                }
            }

            foreach (CachedAsset asset in toRelease)
            {
                loadedAssets.Remove(asset.Location);
                ReleaseCachedAsset(asset);
            }

            // 同时处理待回收队列
            while (releaseQueue.Count > 0)
            {
                CachedAsset asset = releaseQueue.Dequeue();
                if (asset.CanRelease)
                {
                    ReleaseCachedAsset(asset);
                }
            }
        }

        // ==================== 查询 ====================

        /// <summary>
        /// 检查资源是否存在且可用。
        /// </summary>
        /// <param name="location">资源路径。</param>
        /// <returns>存在返回 true，否则返回 false。</returns>
        public bool HasAsset(string location)
        {
            EnsureInitialized();
            return loadedAssets.ContainsKey(location) || helper.IsLocationValid(location);
        }

        /// <summary>
        /// 获取指定资源的下载大小（字节），用于判断是否需要下载。
        /// </summary>
        /// <param name="location">资源路径。</param>
        /// <returns>下载大小（字节）。</returns>
        public long GetDownloadSize(string location)
        {
            EnsureInitialized();
            return helper.GetDownloadSize(location);
        }

        /// <summary>
        /// 获取当前已加载资源的数量。
        /// </summary>
        public int LoadedAssetCount
        {
            get { return loadedAssets.Count; }
        }

        /// <summary>
        /// 获取待加载资源数量（正在异步加载中的资源数）。
        /// </summary>
        public int LoadingAssetCount
        {
            get { return pendingLoads.Count; }
        }

        // ==================== RFrameworkModule 生命周期 ====================

        /// <summary>
        /// 每帧轮询，处理待回收资源的释放。
        /// 每帧最多释放 5 个，避免尖峰。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间。</param>
        /// <param name="realElapseSeconds">真实流逝时间。</param>
        internal override void Update(float elapseSeconds, float realElapseSeconds)
        {
            // 处理待回收队列——每帧最多释放 5 个，避免尖峰
            int maxReleasePerFrame = 5;
            while (releaseQueue.Count > 0 && maxReleasePerFrame > 0)
            {
                CachedAsset asset = releaseQueue.Dequeue();
                if (asset.CanRelease)
                {
                    ReleaseCachedAsset(asset);
                }

                maxReleasePerFrame--;
            }
        }

        /// <summary>
        /// 关闭并清理资源模块。释放所有缓存资源，销毁底层资源系统。
        /// </summary>
        internal override void Shutdown()
        {
            // 释放所有缓存的资源句柄
            foreach (KeyValuePair<string, CachedAsset> kv in loadedAssets)
            {
                helper?.ReleaseAsset(kv.Key);
            }

            loadedAssets.Clear();
            pendingLoads.Clear();
            releaseQueue.Clear();
            loadedScenes.Clear();

            // 销毁底层资源系统（释放所有场景句柄、移除资源包裹并销毁辅助器）。
            helper?.Destroy();
            isInitialized = false;
        }

        // ==================== 私有方法 ====================

        /// <summary>
        /// 检查模块是否已初始化，未初始化时抛出异常。
        /// </summary>
        private void EnsureInitialized()
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException(
                    "ResourceModule: Not initialized. Call InitializeAsync() first.");
            }
        }

        /// <summary>
        /// 检查辅助器是否已设置，未设置时抛出异常。
        /// </summary>
        private void EnsureHelper()
        {
            if (helper == null)
            {
                throw new RFrameworkException("ResourceModule: Helper not set. Call SetHelper() before InitializeAsync.");
            }
        }

        /// <summary>
        /// 释放缓存的资源对象，通知辅助器释放底层资源并清空引用。
        /// </summary>
        /// <param name="asset">要释放的缓存资源。</param>
        private void ReleaseCachedAsset(CachedAsset asset)
        {
            if (asset == null)
            {
                return;
            }

            loadedAssets.Remove(asset.Location);
            helper?.ReleaseAsset(asset.Location);

            asset.Asset = null;
        }

        /// <summary>
        /// 惰性获取事件模块引用。
        /// 通过 RFrameworkModuleEntry.GetModule 获取，若事件模块尚未就绪则返回 null。
        /// </summary>
        /// <returns>事件模块实例，未就绪时为 null。</returns>
        private IEventModule GetEventModule()
        {
            if (eventModule == null)
            {
                try
                {
                    eventModule = RFrameworkModuleEntry.GetModule<IEventModule>();
                }
                catch
                {
                    // 事件模块未就绪时静默处理，不影响核心加载流程
                    return null;
                }
            }

            return eventModule;
        }

        /// <summary>
        /// 分发资源加载失败事件。
        /// 事件模块未就绪时静默跳过。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="location">资源路径。</param>
        /// <param name="errorMessage">失败原因。</param>
        private void FireLoadFailedEvent<T>(string location, string errorMessage)
        {
            IEventModule evt = GetEventModule();
            if (evt == null)
            {
                return;
            }

            ResourceLoadFailedEvent failedEvent = new ResourceLoadFailedEvent(location, typeof(T), errorMessage);
            evt.Fire(failedEvent);
        }
    }
}
