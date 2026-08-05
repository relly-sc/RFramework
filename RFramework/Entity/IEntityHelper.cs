namespace RFramework
{
    /// <summary>
    /// Library 与引擎对象之间的实体适配边界。
    /// </summary>
    public interface IEntityHelper
    {
        /// <summary>从已加载资源创建引擎实例。</summary>
        /// <param name="entityAsset">实体资源对象。</param>
        /// <returns>创建的引擎实例。</returns>
        object InstantiateEntity(object entityAsset);

        /// <summary>取得实例对应的 IEntity 包装。</summary>
        /// <param name="entityInstance">引擎实例。</param>
        /// <param name="group">实体所属组。</param>
        /// <param name="userData">业务自定义数据。</param>
        /// <returns>实体包装器。</returns>
        IEntity CreateEntity(object entityInstance, IEntityGroup group, object userData);

        /// <summary>销毁引擎实例；资源引用由 EntityModule 归还。</summary>
        /// <param name="entityAsset">实体资源对象。</param>
        /// <param name="entityInstance">待销毁的引擎实例。</param>
        void ReleaseEntity(object entityAsset, object entityInstance);
    }
}
