namespace RFramework
{
    /// <summary>
    /// 标识受保护数据的业务用途。
    /// </summary>
    public enum ProtectedDataPayloadKind : byte
    {
        /// <summary>
        /// 项目自定义数据。
        /// </summary>
        Custom = 0,

        /// <summary>
        /// 配置数据。
        /// </summary>
        Config = 1,

        /// <summary>
        /// 存档数据。
        /// </summary>
        Storage = 2
    }

    /// <summary>
    /// 提供带版本、密钥轮换和完整性认证的数据保护能力。
    /// </summary>
    public interface IDataProtector
    {
        /// <summary>
        /// 保护指定数据。
        /// </summary>
        /// <param name="plaintext">待保护数据。</param>
        /// <param name="payloadKind">数据用途。</param>
        /// <param name="keyId">本次使用的密钥标识。</param>
        /// <param name="associatedData">参与认证但不写入结果的调用上下文，可为空。</param>
        /// <returns>包含格式头、随机 IV、密文和认证标签的数据。</returns>
        byte[] Protect(
            byte[] plaintext,
            ProtectedDataPayloadKind payloadKind,
            string keyId,
            byte[] associatedData = null);

        /// <summary>
        /// 认证并还原指定数据。
        /// </summary>
        /// <param name="protectedData">待还原的受保护数据。</param>
        /// <param name="expectedPayloadKind">调用方预期的数据用途。</param>
        /// <param name="associatedData">保护时使用的相同调用上下文，可为空。</param>
        /// <returns>还原后的原始数据。</returns>
        byte[] Unprotect(
            byte[] protectedData,
            ProtectedDataPayloadKind expectedPayloadKind,
            byte[] associatedData = null);
    }
}
