using System;
using System.Collections.Generic;

namespace RFramework.Config
{
    /// <summary>
    /// 配置辅助器接口。
    /// 封装所有配置解析相关的引擎特定操作（如 Luban 反序列化）。
    /// 由 Runtime 层实现，ConfigModule 通过此接口与第三方配置工具解耦。
    /// </summary>
    public interface IConfigHelper
    {
        /// <summary>
        /// 获取配置行类型对应的表类型。
        /// 例如 ItemConfig → TbItem（Luban 生成的表类）。
        /// 实现方可通过命名约定、属性标记或注册表来建立映射。
        /// </summary>
        /// <param name="rowType">配置行类型（如 typeof(ItemConfig)）。</param>
        /// <returns>对应的表类型（如 typeof(TbItem)）。</returns>
        Type GetTableType(Type rowType);

        /// <summary>
        /// 解析配置原始字节为强类型配置表对象。
        /// 由 Runtime 层使用 Luban 的 ByteBuf 反序列化实现。
        /// </summary>
        /// <param name="tableType">表类型（由 GetTableType 返回）。</param>
        /// <param name="bytes">原始字节数据。</param>
        /// <returns>解析后的配置表对象。</returns>
        object ParseConfig(Type tableType, byte[] bytes);

        /// <summary>
        /// 从已解析的配置表中获取指定 ID 的单条配置行。
        /// </summary>
        /// <typeparam name="T">配置行类型。</typeparam>
        /// <param name="parsedTable">ParseConfig 返回的配置表对象。</param>
        /// <param name="id">配置行 ID。</param>
        /// <returns>配置行实例，不存在时返回 null。</returns>
        T GetConfig<T>(object parsedTable, int id) where T : class;

        /// <summary>
        /// 检查已解析的配置表中是否包含指定 ID。
        /// </summary>
        /// <param name="parsedTable">ParseConfig 返回的配置表对象。</param>
        /// <param name="id">配置行 ID。</param>
        /// <returns>存在返回 true，否则返回 false。</returns>
        bool ContainsConfig(object parsedTable, int id);

        /// <summary>
        /// 获取已解析配置表中的所有配置行。
        /// </summary>
        /// <typeparam name="T">配置行类型。</typeparam>
        /// <param name="parsedTable">ParseConfig 返回的配置表对象。</param>
        /// <returns>配置行只读列表。</returns>
        IReadOnlyList<T> GetAllConfigs<T>(object parsedTable) where T : class;

        /// <summary>
        /// 释放已解析的配置表。
        /// 调用后该表不应再被访问。
        /// </summary>
        /// <param name="parsedTable">ParseConfig 返回的配置表对象。</param>
        void ReleaseConfig(object parsedTable);
    }
}
