using System;

namespace RFramework
{
    /// <summary>
    /// 将一个实体实例与其资源引用绑定，确保最终只释放一次。
    /// </summary>
    internal sealed class EntityInstanceObject
    {
        private readonly IEntityHelper helper;
        private readonly IResourceModule resourceModule;
        private bool released;

        /// <summary>
        /// 创建实体实例资源绑定。
        /// </summary>
        /// <param name="assetName">实体资源地址。</param>
        /// <param name="asset">资源对象。</param>
        /// <param name="target">实例对象。</param>
        /// <param name="helper">实体 Helper。</param>
        /// <param name="resourceModule">资源模块。</param>
        public EntityInstanceObject(string assetName, object asset, object target, IEntityHelper helper,
            IResourceModule resourceModule)
        {
            AssetName = assetName;
            Asset = asset;
            Target = target;
            this.helper = helper;
            this.resourceModule = resourceModule;
        }

        /// <summary>获取实体资源地址。</summary>
        public string AssetName { get; }

        /// <summary>获取资源对象。</summary>
        public object Asset { get; }

        /// <summary>获取实例对象。</summary>
        public object Target { get; }

        /// <summary>获取或设置实例最近归还缓存的时间。</summary>
        public float LastUsedAt { get; set; }

        /// <summary>
        /// 释放实体实例及其资源引用；重复调用不会再次释放。
        /// </summary>
        public void Release()
        {
            if (released)
            {
                return;
            }

            released = true;
            Exception failure = null;
            try
            {
                helper.ReleaseEntity(Asset, Target);
            }
            catch (Exception ex)
            {
                failure = ex;
            }

            try
            {
                resourceModule.UnloadAsset<object>(AssetName);
            }
            catch (Exception ex)
            {
                failure = failure == null ? ex : new AggregateException(failure, ex);
            }

            if (failure != null)
            {
                throw new RFrameworkException($"Failed to release entity asset '{AssetName}'.", failure);
            }
        }
    }
}
