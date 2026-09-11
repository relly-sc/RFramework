namespace RFramework
{
    /// <summary>持久化安装级密钥材料的存储边界。</summary>
    public interface IKeyStore
    {
        /// <summary>读取指定密钥；成功时返回由调用方负责清零的副本。</summary>
        bool TryRead(string keyId, out byte[] key);

        /// <summary>持久化指定密钥。实现不得保留调用方传入数组的引用。</summary>
        void Write(string keyId, byte[] key);

        /// <summary>删除指定密钥。</summary>
        bool Delete(string keyId);
    }
}
