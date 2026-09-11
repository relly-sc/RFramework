namespace RFramework
{
    /// <summary>存档对象与字节数据之间的可替换序列化契约。</summary>
    public interface IStorageSerializer
    {
        /// <summary>序列化存档对象。</summary>
        byte[] Serialize<T>(T data);

        /// <summary>反序列化存档对象。</summary>
        T Deserialize<T>(byte[] data);
    }

    /// <summary>业务存档版本迁移契约。</summary>
    /// <typeparam name="T">存档数据类型。</typeparam>
    public interface IStorageMigration<T>
    {
        /// <summary>将旧版本数据迁移到目标版本。</summary>
        T Migrate(T data, int fromVersion, int targetVersion);
    }
}
