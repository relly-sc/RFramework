using System.Collections.Generic;

namespace RFramework
{
    /// <summary>
    /// 可由实体模块托管的运行时对象。实现负责保存 OnInit 分配的身份和关系。
    /// </summary>
    public interface IEntity
    {
        /// <summary>获取实体编号。</summary>
        long Id { get; }

        /// <summary>获取实体当前生命周期状态。</summary>
        EntityStatus Status { get; }

        /// <summary>获取实体资源地址或外部登记名称。</summary>
        string AssetName { get; }

        /// <summary>获取实体对应的引擎对象。</summary>
        object Handle { get; }

        /// <summary>获取实体所属组。</summary>
        IEntityGroup Group { get; }

        /// <summary>获取父实体。</summary>
        IEntity Parent { get; }

        /// <summary>获取只读子实体集合。</summary>
        IReadOnlyList<IEntity> Children { get; }

        /// <summary>为一次显示周期分配身份。</summary>
        /// <param name="entityId">实体编号。</param>
        /// <param name="assetName">实体资源地址或外部登记名称。</param>
        /// <param name="group">实体所属组。</param>
        /// <param name="isNewInstance">是否为刚创建而非缓存复用的实例。</param>
        /// <param name="userData">业务自定义数据。</param>
        void OnInit(long entityId, string assetName, IEntityGroup group, bool isNewInstance,
            object userData);

        /// <summary>结束一次显示周期并清空运行时状态。</summary>
        void OnRecycle();

        /// <summary>实体进入活跃状态。</summary>
        /// <param name="userData">业务自定义数据。</param>
        void OnShow(object userData);

        /// <summary>实体离开活跃状态。</summary>
        /// <param name="isShutdown">是否因框架关闭而隐藏。</param>
        /// <param name="userData">业务自定义数据。</param>
        void OnHide(bool isShutdown, object userData);

        /// <summary>当前实体成为父节点后的通知。</summary>
        /// <param name="childEntity">新挂接的子实体。</param>
        /// <param name="userData">业务自定义数据。</param>
        void OnAttached(IEntity childEntity, object userData);

        /// <summary>当前实体失去子节点后的通知。</summary>
        /// <param name="childEntity">已脱离的子实体。</param>
        /// <param name="userData">业务自定义数据。</param>
        void OnDetached(IEntity childEntity, object userData);

        /// <summary>当前实体被挂到父节点后的通知。</summary>
        /// <param name="parentEntity">新的父实体。</param>
        /// <param name="userData">业务自定义数据。</param>
        void OnAttachTo(IEntity parentEntity, object userData);

        /// <summary>当前实体脱离父节点后的通知。</summary>
        /// <param name="parentEntity">原父实体。</param>
        /// <param name="userData">业务自定义数据。</param>
        void OnDetachFrom(IEntity parentEntity, object userData);

        /// <summary>实体组逐帧驱动入口。</summary>
        /// <param name="elapseSeconds">受时间缩放影响的帧间隔。</param>
        /// <param name="realElapseSeconds">不受时间缩放影响的帧间隔。</param>
        void OnUpdate(float elapseSeconds, float realElapseSeconds);
    }
}
