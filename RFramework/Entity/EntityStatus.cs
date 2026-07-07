namespace RFramework.Entity
{
    /// <summary>
    /// 实体状态枚举，描述实体在其生命周期中的当前阶段。
    /// </summary>
    public enum EntityStatus : byte
    {
        /// <summary>
        /// 未知状态，实体尚未初始化。
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// 即将初始化，实体正在准备 OnInit 调用。
        /// </summary>
        WillInit = 1,

        /// <summary>
        /// 已初始化，OnInit 已完成。
        /// </summary>
        Inited = 2,

        /// <summary>
        /// 即将显示，实体正在准备 OnShow 调用。
        /// </summary>
        WillShow = 3,

        /// <summary>
        /// 已显示，OnShow 已完成，实体可见且活跃。
        /// </summary>
        Showed = 4,

        /// <summary>
        /// 即将隐藏，实体正在准备 OnHide 调用。
        /// </summary>
        WillHide = 5,

        /// <summary>
        /// 已隐藏，OnHide 已完成，实体不可见。
        /// </summary>
        Hidden = 6,

        /// <summary>
        /// 即将回收，实体正在准备 OnRecycle 调用。
        /// </summary>
        WillRecycle = 7,

        /// <summary>
        /// 已回收，OnRecycle 已完成，实体已归还对象池或已销毁。
        /// </summary>
        Recycled = 8
    }
}
