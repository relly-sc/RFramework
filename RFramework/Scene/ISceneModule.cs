using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RFramework.Event;
using RFramework.Resource;

namespace RFramework.Scene
{
    /// <summary>
    /// 场景模块接口，管理 Unity 场景的加载、卸载和状态追踪。
    /// 实际加载委托 IResourceModule，本模块是纯管理层（不建 ISceneHelper）。
    /// 异步 API 统一使用 Task，Library 层零第三方依赖。
    /// </summary>
    /// <remarks>
    /// 与 GF 的差异：
    /// - 异步：Task 替代 GF 回调模式
    /// - 事件：IEventModule.Fire 替代 C# event
    /// - 依赖注入：SetDependencies 合并注入替代多 setter
    /// - 不建 ISceneHelper：场景加载已在 IResourceModule 中实现
    /// </remarks>
    public interface ISceneModule
    {
        /// <summary>
        /// 获取当前主场景名称。
        /// </summary>
        string CurrentSceneName { get; }

        /// <summary>
        /// 获取已加载的场景数量。
        /// </summary>
        int LoadedSceneCount { get; }

        /// <summary>
        /// 获取正在加载的场景数量。
        /// </summary>
        int LoadingSceneCount { get; }

        /// <summary>
        /// 设置依赖模块引用（由 SceneComponent 在 Awake 中注入）。
        /// 必须在首次 LoadSceneAsync 之前调用。
        /// </summary>
        /// <param name="resourceModule">资源模块，用于场景加载/卸载。</param>
        /// <param name="eventModule">事件模块，用于分发场景事件。</param>
        void SetDependencies(IResourceModule resourceModule, IEventModule eventModule);

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
        Task LoadSceneAsync(string assetName, int sceneMode = 0,
            bool activateOnLoad = true, uint priority = 0, IProgress<float> onProgress = null,
            object userData = null, CancellationToken ct = default);

        /// <summary>
        /// 异步卸载场景。场景必须已加载且未在卸载中。
        /// </summary>
        /// <param name="assetName">场景资源路径。</param>
        /// <param name="userData">用户自定义数据。</param>
        Task UnloadSceneAsync(string assetName, object userData = null);

        /// <summary>
        /// 判断场景是否已加载。
        /// </summary>
        /// <param name="assetName">场景资源路径。</param>
        /// <returns>是否已加载。</returns>
        bool IsLoaded(string assetName);

        /// <summary>
        /// 判断场景是否正在加载中。
        /// </summary>
        /// <param name="assetName">场景资源路径。</param>
        /// <returns>是否正在加载。</returns>
        bool IsLoading(string assetName);

        /// <summary>
        /// 判断场景是否正在卸载中。
        /// </summary>
        /// <param name="assetName">场景资源路径。</param>
        /// <returns>是否正在卸载。</returns>
        bool IsUnloading(string assetName);

        /// <summary>
        /// 获取所有已加载的场景名称。
        /// </summary>
        /// <returns>场景名称数组。</returns>
        string[] GetLoadedSceneNames();

        /// <summary>
        /// 获取所有已加载的场景名称，填充到结果列表。
        /// </summary>
        /// <param name="results">结果列表。</param>
        void GetLoadedSceneNames(List<string> results);

        /// <summary>
        /// 获取所有正在加载中的场景名称。
        /// </summary>
        /// <returns>场景名称数组。</returns>
        string[] GetLoadingSceneNames();

        /// <summary>
        /// 获取所有正在加载中的场景名称，填充到结果列表。
        /// </summary>
        /// <param name="results">结果列表。</param>
        void GetLoadingSceneNames(List<string> results);

        /// <summary>
        /// 获取所有正在卸载中的场景名称。
        /// </summary>
        /// <returns>场景名称数组。</returns>
        string[] GetUnloadingSceneNames();

        /// <summary>
        /// 获取所有正在卸载中的场景名称，填充到结果列表。
        /// </summary>
        /// <param name="results">结果列表。</param>
        void GetUnloadingSceneNames(List<string> results);
    }
}
