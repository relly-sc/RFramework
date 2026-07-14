using System.Collections.Generic;

namespace RFramework
{
    /// <summary>
    /// 请求数据结构体（纯 C#，零 Unity 依赖）。
    /// 封装一次 HTTP 请求所需的所有参数，传递给 <see cref="IWebRequestHelper.SendAsync"/> 执行。
    /// </summary>
    public class WebRequestData
    {
        /// <summary>
        /// 请求 URL（可包含 query string）。
        /// </summary>
        public string Url;

        /// <summary>
        /// HTTP 请求方法。
        /// </summary>
        public HttpMethod Method;

        /// <summary>
        /// 自定义请求头（键值对，可以为 null）。
        /// 注意：Content-Type 由 Body 参数和 MimeType 自动设置，无需手动添加。
        /// </summary>
        public Dictionary<string, string> Headers;

        /// <summary>
        /// 请求体字节数据（GET/HEAD 等无请求体的方法为 null）。
        /// </summary>
        public byte[] Body;

        /// <summary>
        /// Content-Type 头值（如 "application/json"），有 Body 时必填。
        /// </summary>
        public string ContentType;

        /// <summary>
        /// 超时毫秒数（0 表示无超时限制）。
        /// </summary>
        public int TimeoutMs;

        /// <summary>
        /// 请求标签，用于分组管理和批量取消。
        /// </summary>
        public string Tag;

        /// <summary>
        /// 请求优先级（越大越优先，仅在有并发限制时生效）。
        /// </summary>
        public uint Priority;
    }
}
