using System.Collections.Generic;

namespace RFramework
{
    /// <summary>
    /// 实体接口，描述一个游戏实体的完整生命周期和状态。
    /// Library 层用 object 替代 GameObject，由 Runtime 层 Helper 强转。
    /// </summary>
    public interface IEntity
    {
        /// <summary>
        /// 获取实体编号。
        /// </summary>
        long Id { get; }

        /// <summary>
        /// 获取实体当前状态。
        /// </summary>
        EntityStatus Status { get; }

        /// <summary>
        /// 获取实体名称。框架加载的实体使用资源路径，外部实体使用登记名称。
        /// </summary>
        string AssetName { get; }

        /// <summary>
        /// 获取实体实例对象（Unity 层为 GameObject，Library 层用 object 替代）。
        /// </summary>
        object Handle { get; }

        /// <summary>
        /// 获取实体所属的实体组。
        /// </summary>
        IEntityGroup Group { get; }

        /// <summary>
        /// 获取父实体。
        /// </summary>
        IEntity Parent { get; }

        /// <summary>
        /// 获取子实体列表。
        /// </summary>
        IReadOnlyList<IEntity> Children { get; }

        /// <summary>
        /// 实体初始化回调，在实体首次创建或从对象池取出时调用。
        /// </summary>
        /// <param name="entityId">实体编号。</param>
        /// <param name="assetName">实体资源路径。</param>
        /// <param name="group">实体所属组。</param>
        /// <param name="isNewInstance">是否为新创建的实例（非对象池复用）。</param>
        /// <param name="userData">用户自定义数据。</param>
        void OnInit(long entityId, string assetName, IEntityGroup group, bool isNewInstance, object userData);

        /// <summary>
        /// 实体回收回调，在实体被归还对象池或销毁时调用。
        /// </summary>
        void OnRecycle();

        /// <summary>
        /// 实体显示回调，在实体可见时调用。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        void OnShow(object userData);

        /// <summary>
        /// 实体隐藏回调，在实体不可见时调用。
        /// </summary>
        /// <param name="isShutdown">是否为框架关闭时的隐藏。</param>
        /// <param name="userData">用户自定义数据。</param>
        void OnHide(bool isShutdown, object userData);

        /// <summary>
        /// 子实体附加回调，作为父实体被调用。
        /// </summary>
        /// <param name="childEntity">被附加的子实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        void OnAttached(IEntity childEntity, object userData);

        /// <summary>
        /// 子实体解除回调，作为父实体被调用。
        /// </summary>
        /// <param name="childEntity">被解除的子实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        void OnDetached(IEntity childEntity, object userData);

        /// <summary>
        /// 附加到父实体回调，作为子实体被调用。
        /// </summary>
        /// <param name="parentEntity">目标父实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        void OnAttachTo(IEntity parentEntity, object userData);

        /// <summary>
        /// 从父实体解除回调，作为子实体被调用。
        /// </summary>
        /// <param name="parentEntity">原父实体。</param>
        /// <param name="userData">用户自定义数据。</param>
        void OnDetachFrom(IEntity parentEntity, object userData);

        /// <summary>
        /// 实体轮询回调，每帧调用。
        /// </summary>
        /// <param name="elapseSeconds">逻辑流逝时间（受 timeScale 影响）。</param>
        /// <param name="realElapseSeconds">实际流逝时间。</param>
        void OnUpdate(float elapseSeconds, float realElapseSeconds);
    }
}
