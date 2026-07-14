using System.Collections.Generic;

namespace RFramework
{
    /// <summary>
    /// 实体信息，记录实体的运行时状态、父子关系等元数据。
    /// 仅在 EntityModule 内部使用，不暴露给外部。
    /// </summary>
    internal sealed class EntityInfo
    {
        /// <summary>
        /// 实体实例引用。
        /// </summary>
        public IEntity Entity { get; }

        /// <summary>
        /// 实体当前状态。
        /// </summary>
        public EntityStatus Status { get; set; }

        /// <summary>
        /// 父实体引用。
        /// </summary>
        public IEntity Parent { get; set; }

        /// <summary>
        /// 子实体列表。
        /// </summary>
        private readonly List<IEntity> children = new List<IEntity>();

        /// <summary>
        /// 获取子实体数量。
        /// </summary>
        public int ChildEntityCount => children.Count;

        /// <summary>
        /// 获取子实体的只读列表视图。
        /// </summary>
        public IReadOnlyList<IEntity> Children => children;

        /// <summary>
        /// 初始化实体信息。
        /// </summary>
        /// <param name="entity">实体实例。</param>
        public EntityInfo(IEntity entity)
        {
            Entity = entity;
            Status = EntityStatus.WillInit;
        }

        /// <summary>
        /// 获取第一个子实体。
        /// </summary>
        /// <returns>第一个子实体，无子实体时返回 null。</returns>
        public IEntity GetChildEntity()
        {
            if (children.Count <= 0)
            {
                return null;
            }

            return children[0];
        }

        /// <summary>
        /// 添加子实体。
        /// </summary>
        /// <param name="child">子实体实例。</param>
        public void AddChildEntity(IEntity child)
        {
            if (!children.Contains(child))
            {
                children.Add(child);
            }
        }

        /// <summary>
        /// 移除子实体。
        /// </summary>
        /// <param name="child">子实体实例。</param>
        public void RemoveChildEntity(IEntity child)
        {
            if (children.Contains(child))
            {
                children.Remove(child);
            }
        }
    }
}
