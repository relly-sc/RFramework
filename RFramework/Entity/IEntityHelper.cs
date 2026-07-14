namespace RFramework
{
    /// <summary>
    /// 实体辅助器接口，封装所有引擎特定的实体操作。
    /// Library 层通过此接口与 Runtime 层解耦，不同引擎只需实现对应 Helper。
    /// </summary>
    public interface IEntityHelper
    {
        /// <summary>
        /// 使用加载好的资源实例化一个实体对象。
        /// </summary>
        /// <param name="entityAsset">已加载的实体资源（Unity 层为 GameObject prefab）。</param>
        /// <returns>实例化后的对象（Unity 层为 GameObject 实例）。</returns>
        object InstantiateEntity(object entityAsset);

        /// <summary>
        /// 为实例化对象创建 IEntity 包装，并关联到实体组。
        /// </summary>
        /// <param name="entityInstance">实例化后的对象。</param>
        /// <param name="group">目标实体组。</param>
        /// <param name="userData">用户自定义数据。</param>
        /// <returns>IEntity 包装实例。</returns>
        IEntity CreateEntity(object entityInstance, IEntityGroup group, object userData);

        /// <summary>
        /// 释放实体实例和资源。
        /// </summary>
        /// <param name="entityAsset">实体资源对象。</param>
        /// <param name="entityInstance">实体实例对象。</param>
        void ReleaseEntity(object entityAsset, object entityInstance);
    }
}
