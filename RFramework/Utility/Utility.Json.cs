using System;
using System.Threading;

namespace RFramework
{
    /// <summary>
    /// 框架通用工具入口。
    /// </summary>
    public static partial class Utility
    {
        /// <summary>
        /// 通过可替换 Helper 提供 JSON 序列化与反序列化。
        /// </summary>
        public static class Json
        {
            private static IJsonHelper helper;

            /// <summary>
            /// JSON 转换器契约。
            /// </summary>
            public interface IJsonHelper
            {
                /// <summary>将对象转换为 JSON。</summary>
                /// <param name="value">待序列化对象。</param>
                /// <returns>JSON 字符串。</returns>
                string ToJson(object value);

                /// <summary>将 JSON 转换为指定泛型类型。</summary>
                /// <typeparam name="T">目标类型。</typeparam>
                /// <param name="json">JSON 字符串。</param>
                /// <returns>反序列化结果。</returns>
                T ToObject<T>(string json);

                /// <summary>将 JSON 转换为指定运行时类型。</summary>
                /// <param name="targetType">目标运行时类型。</param>
                /// <param name="json">JSON 字符串。</param>
                /// <returns>反序列化结果。</returns>
                object ToObject(Type targetType, string json);
            }

            /// <summary>
            /// 设置全局 JSON Helper。
            /// </summary>
            /// <param name="value">有效的 JSON Helper。</param>
            public static void SetJsonHelper(IJsonHelper value)
            {
                if (value == null)
                {
                    throw new RFrameworkException("JSON helper cannot be null.");
                }

                Volatile.Write(ref helper, value);
            }

        /// <summary>
        /// 将对象转换为 JSON。
        /// </summary>
        /// <param name="value">待序列化对象。</param>
        /// <returns>JSON 字符串。</returns>
        public static string ToJson(object value)
            {
                try
                {
                    return GetHelper().ToJson(value);
                }
                catch (RFrameworkException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw new RFrameworkException("JSON serialization failed.", exception);
                }
            }

        /// <summary>
        /// 将 JSON 转换为指定泛型类型。
        /// </summary>
        /// <typeparam name="T">目标类型。</typeparam>
        /// <param name="json">JSON 字符串。</param>
        /// <returns>反序列化结果。</returns>
        public static T ToObject<T>(string json)
            {
                try
                {
                    return GetHelper().ToObject<T>(json);
                }
                catch (RFrameworkException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw new RFrameworkException(
                        $"JSON deserialization to '{typeof(T).FullName}' failed.", exception);
                }
            }

        /// <summary>
        /// 将 JSON 转换为指定运行时类型。
        /// </summary>
        /// <param name="targetType">目标运行时类型。</param>
        /// <param name="json">JSON 字符串。</param>
        /// <returns>反序列化结果。</returns>
        public static object ToObject(Type targetType, string json)
            {
                if (targetType == null)
                {
                    throw new RFrameworkException("JSON target type cannot be null.");
                }

                try
                {
                    return GetHelper().ToObject(targetType, json);
                }
                catch (RFrameworkException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw new RFrameworkException(
                        $"JSON deserialization to '{targetType.FullName}' failed.", exception);
                }
            }

            private static IJsonHelper GetHelper()
            {
                return Volatile.Read(ref helper) ?? throw new RFrameworkException(
                    "No JSON helper is installed. Initialize the framework before using JSON.");
            }
        }
    }
}
