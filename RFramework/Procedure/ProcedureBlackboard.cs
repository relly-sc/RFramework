using System.Collections.Generic;

namespace RFramework.Procedure
{
    /// <summary>
    /// 流程黑板，用于跨流程状态共享数据。
    /// 例：在 ProcedureLaunch 中解析命令行参数存入 Blackboard，
    /// 后续 ProcedureCheckVersion / ProcedureLogin 直接读取。
    /// </summary>
    public sealed class ProcedureBlackboard
    {
        /// <summary>
        /// 数据存储字典。
        /// </summary>
        private readonly Dictionary<string, object> data;

        /// <summary>
        /// 初始化黑板的新实例。
        /// </summary>
        public ProcedureBlackboard()
        {
            data = new Dictionary<string, object>();
        }

        /// <summary>
        /// 设置黑板值。
        /// </summary>
        /// <typeparam name="T">值的类型。</typeparam>
        /// <param name="key">键，重复键会覆盖旧值。</param>
        /// <param name="value">值。</param>
        public void Set<T>(string key, T value)
        {
            data[key] = value;
        }

        /// <summary>
        /// 获取黑板值。
        /// </summary>
        /// <typeparam name="T">值的类型。</typeparam>
        /// <param name="key">键。</param>
        /// <returns>值，键不存在时返回 default(T)。</returns>
        public T Get<T>(string key)
        {
            if (data.TryGetValue(key, out object value))
            {
                return (T)value;
            }

            return default;
        }

        /// <summary>
        /// 检查黑板中是否包含指定键。
        /// </summary>
        /// <param name="key">键。</param>
        /// <returns>如果包含则返回 true。</returns>
        public bool Has(string key)
        {
            return data.ContainsKey(key);
        }

        /// <summary>
        /// 移除指定键的值。
        /// </summary>
        /// <param name="key">键。</param>
        /// <returns>成功移除返回 true，键不存在返回 false。</returns>
        public bool Remove(string key)
        {
            return data.Remove(key);
        }

        /// <summary>
        /// 清空黑板中的所有数据。
        /// </summary>
        public void Clear()
        {
            data.Clear();
        }
    }
}
