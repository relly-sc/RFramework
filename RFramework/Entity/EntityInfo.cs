using System.Collections.Generic;

namespace RFramework
{
    /// <summary>
    /// 实体模块内部账本。父子关系以账本为准，IEntity 上的关系用于对外展示。
    /// </summary>
    internal sealed class EntityInfo
    {
        private readonly List<EntityInfo> children = new List<EntityInfo>();

        /// <summary>
        /// 创建实体账本记录。
        /// </summary>
        /// <param name="entity">实体实例。</param>
        /// <param name="group">实体所属组。</param>
        /// <param name="isExternal">是否由场景或业务外部注册。</param>
        public EntityInfo(IEntity entity, EntityGroup group, bool isExternal)
        {
            Entity = entity;
            Group = group;
            IsExternal = isExternal;
        }

        /// <summary>获取实体实例。</summary>
        public IEntity Entity { get; }

        /// <summary>获取实体所属组。</summary>
        public EntityGroup Group { get; }

        /// <summary>获取实体是否由外部注册。</summary>
        public bool IsExternal { get; }

        /// <summary>获取或设置实体是否正在执行显示或隐藏转换。</summary>
        public bool IsTransitioning { get; set; }

        /// <summary>获取或设置父实体账本。</summary>
        public EntityInfo Parent { get; set; }

        /// <summary>获取只读子实体账本集合。</summary>
        public IReadOnlyList<EntityInfo> Children => children;

        /// <summary>
        /// 添加子实体账本。
        /// </summary>
        /// <param name="child">待添加的子实体账本。</param>
        public void AddChild(EntityInfo child)
        {
            if (!children.Contains(child))
            {
                children.Add(child);
            }
        }

        /// <summary>
        /// 移除子实体账本。
        /// </summary>
        /// <param name="child">待移除的子实体账本。</param>
        public void RemoveChild(EntityInfo child)
        {
            children.Remove(child);
        }

        /// <summary>
        /// 创建当前子实体账本快照。
        /// </summary>
        /// <returns>不受后续关系变化影响的数组快照。</returns>
        public EntityInfo[] GetChildrenSnapshot()
        {
            return children.ToArray();
        }
    }
}
