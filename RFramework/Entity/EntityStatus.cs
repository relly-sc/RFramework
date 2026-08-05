namespace RFramework
{
    /// <summary>
    /// IEntity 对外报告的生命周期阶段。
    /// </summary>
    public enum EntityStatus : byte
    {
        Unknown = 0,
        WillInit = 1,
        Inited = 2,
        WillShow = 3,
        Showed = 4,
        WillHide = 5,
        Hidden = 6,
        WillRecycle = 7,
        Recycled = 8
    }
}
