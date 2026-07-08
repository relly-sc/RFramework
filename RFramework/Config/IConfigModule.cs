using System.Collections.Generic;

namespace RFramework.Config
{
    /// <summary>
    /// 配置模块接口。
    /// 负责配置表的内存缓存和查询，不涉及资源加载（加载由 Runtime ConfigComponent 编排）。
    /// 通过 IConfigHelper 桥接与 Luban 等配置工具解耦。
    /// </summary>
    public interface IConfigModule
    {
        /// <summary>
        /// 设置配置辅助器（必须在加载任何配置前调用）。
        /// 辅助器封装了所有引擎/工具特定的配置解析操作，由 Runtime 层实现。
        /// </summary>
        /// <param name="helper">配置辅助器实例。</param>
        void SetHelper(IConfigHelper helper);

        /// <summary>
        /// 从字节数据加载配置表。T 为 Luban 生成的配置行类型（如 ItemConfig）。
        /// 内部通过 IConfigHelper.GetTableType 映射行类型→表类型，然后解析并缓存。
        /// 重复加载相同类型会覆盖旧数据。
        /// </summary>
        /// <typeparam name="T">配置行类型。</typeparam>
        /// <param name="configBytes">配置原始字节数据。</param>
        void LoadConfig<T>(byte[] configBytes) where T : class;

        /// <summary>
        /// 从 JSON 字符串加载配置表。适用于运行时动态生成配置、编辑器预览等场景。
        /// JSON 格式应为配置行数组：[{"Id":1,...},{"Id":2,...}]。
        /// 重复加载相同类型会覆盖旧数据。
        /// </summary>
        /// <typeparam name="T">配置行类型。</typeparam>
        /// <param name="json">JSON 字符串。</param>
        void LoadConfigFromString<T>(string json) where T : class;

        /// <summary>
        /// 卸载指定类型的配置表。
        /// </summary>
        /// <typeparam name="T">配置行类型。</typeparam>
        void UnloadConfig<T>() where T : class;

        /// <summary>
        /// 卸载所有已加载的配置表。
        /// </summary>
        void UnloadAllConfigs();

        /// <summary>
        /// 检查指定类型的配置表是否已加载。
        /// </summary>
        /// <typeparam name="T">配置行类型。</typeparam>
        /// <returns>已加载返回 true，否则返回 false。</returns>
        bool HasConfig<T>() where T : class;

        /// <summary>
        /// 从已加载的配置表中获取指定 ID 的配置行。
        /// </summary>
        /// <typeparam name="T">配置行类型。</typeparam>
        /// <param name="id">配置行 ID。</param>
        /// <returns>配置行实例，不存在时返回 null。</returns>
        T GetConfig<T>(int id) where T : class;

        /// <summary>
        /// 检查已加载的配置表中是否包含指定 ID 的配置行。
        /// </summary>
        /// <typeparam name="T">配置行类型。</typeparam>
        /// <param name="id">配置行 ID。</param>
        /// <returns>存在返回 true，否则返回 false。</returns>
        bool HasConfigRow<T>(int id) where T : class;

        /// <summary>
        /// 获取已加载配置表中的所有配置行。
        /// </summary>
        /// <typeparam name="T">配置行类型。</typeparam>
        /// <returns>配置行只读列表。</returns>
        IReadOnlyList<T> GetAllConfigs<T>() where T : class;

        /// <summary>
        /// 获取当前已加载的配置表数量。
        /// </summary>
        int ConfigCount { get; }
    }
}
