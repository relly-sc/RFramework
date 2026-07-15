using System;
using System.Collections.Generic;

namespace RFramework
{
    /// <summary>
    /// 可一次解析多张配置表的可选辅助器接口。
    /// 未实现此接口的自定义 IConfigHelper 仍可继续使用单表加载 API。
    /// </summary>
    public interface IConfigBundleHelper
    {
        /// <summary>
        /// 解析配置容器，并按配置行类型返回已经合并的表对象。
        /// 同一行类型的多个分片必须在返回前完成去重和合并。
        /// </summary>
        /// <param name="bytes">配置容器原始字节。</param>
        /// <returns>配置行类型到表对象的只读映射。</returns>
        IReadOnlyDictionary<Type, object> ParseConfigBundle(byte[] bytes);
    }
}
