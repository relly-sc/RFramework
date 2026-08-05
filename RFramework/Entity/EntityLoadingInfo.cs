using System;
using System.Diagnostics;
using System.Threading;

namespace RFramework
{
    /// <summary>
    /// 单次实体加载请求及其独立取消边界。
    /// </summary>
    internal sealed class EntityLoadingInfo : IDisposable
    {
        private readonly long startedAt = Stopwatch.GetTimestamp();
        private readonly CancellationTokenSource cancellation;

        /// <summary>
        /// 创建实体加载请求，并组合调用方与框架关闭令牌。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <param name="assetName">实体资源地址。</param>
        /// <param name="group">目标实体组。</param>
        /// <param name="userData">业务自定义数据。</param>
        /// <param name="callerToken">调用方取消令牌。</param>
        /// <param name="shutdownToken">框架关闭取消令牌。</param>
        public EntityLoadingInfo(long entityId, string assetName, EntityGroup group, object userData,
            CancellationToken callerToken, CancellationToken shutdownToken)
        {
            EntityId = entityId;
            AssetName = assetName;
            Group = group;
            UserData = userData;
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(callerToken, shutdownToken);
        }

        /// <summary>获取实体编号。</summary>
        public long EntityId { get; }

        /// <summary>获取实体资源地址。</summary>
        public string AssetName { get; }

        /// <summary>获取目标实体组。</summary>
        public EntityGroup Group { get; }

        /// <summary>获取业务自定义数据。</summary>
        public object UserData { get; }

        /// <summary>获取本次加载请求的组合取消令牌。</summary>
        public CancellationToken Token => cancellation.Token;

        /// <summary>获取请求已经经过的真实时间。</summary>
        public float ElapsedSeconds =>
            (float)(Stopwatch.GetTimestamp() - startedAt) / Stopwatch.Frequency;

        /// <summary>取消本次加载请求。</summary>
        public void Cancel()
        {
            cancellation.Cancel();
        }

        /// <summary>释放本次请求持有的取消令牌资源。</summary>
        public void Dispose()
        {
            cancellation.Dispose();
        }
    }
}
