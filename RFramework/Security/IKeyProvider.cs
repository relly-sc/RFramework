namespace RFramework
{
    /// <summary>
    /// 为受保护数据提供指定版本的密钥副本。
    /// </summary>
    public interface IKeyProvider
    {
        /// <summary>
        /// 获取指定标识对应的密钥副本。
        /// </summary>
        /// <param name="keyId">密钥标识。</param>
        /// <param name="key">
        /// 获取成功时返回由调用方负责清零的密钥副本；获取失败时返回 <see langword="null"/>。
        /// </param>
        /// <returns>找到密钥时返回 <see langword="true"/>。</returns>
        bool TryGetKey(string keyId, out byte[] key);
    }
}
