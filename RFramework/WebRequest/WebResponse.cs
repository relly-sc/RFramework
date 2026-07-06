using System.Collections.Generic;
using System.Text;

namespace RFramework.WebRequest
{
    /// <summary>
    /// HTTP 响应结构体（纯 C# 数据类，零 Unity 依赖）。
    /// 包含状态码、响应头、响应体以及错误分类信息。
    /// </summary>
    public class WebResponse
    {
        /// <summary>
        /// HTTP 状态码（200、404、500 等）。
        /// </summary>
        public int StatusCode { get; }

        /// <summary>
        /// 响应 Content-Type 头值。
        /// </summary>
        public string ContentType { get; }

        /// <summary>
        /// 响应头字典（键为小写规范化的头名称）。
        /// </summary>
        public Dictionary<string, string> Headers { get; }

        /// <summary>
        /// 响应体原始字节数据。
        /// </summary>
        public byte[] Data { get; }

        /// <summary>
        /// 响应体长度（字节）。
        /// </summary>
        public long ContentLength { get; }

        /// <summary>
        /// 错误分类（None 表示请求成功）。
        /// </summary>
        public WebRequestError Error { get; }

        /// <summary>
        /// 错误描述信息（仅 Error != None 时有效）。
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// 响应体文本缓存（首次访问 Text 属性时由 UTF-8 解码并缓存，避免重复解码）。
        /// </summary>
        private string cachedText;

        /// <summary>
        /// 初始化 HTTP 响应的新实例。
        /// </summary>
        /// <param name="statusCode">HTTP 状态码。</param>
        /// <param name="contentType">Content-Type 头值，无时为 null。</param>
        /// <param name="headers">响应头字典，无时传入空字典。</param>
        /// <param name="data">响应体字节数据，无时传入 null。</param>
        /// <param name="error">错误分类。</param>
        /// <param name="errorMessage">错误描述，无错误时为 null。</param>
        public WebResponse(int statusCode, string contentType, Dictionary<string, string> headers,
            byte[] data, WebRequestError error = WebRequestError.None, string errorMessage = null)
        {
            StatusCode = statusCode;
            ContentType = contentType;
            Headers = headers ?? new Dictionary<string, string>();
            Data = data;
            ContentLength = data != null ? data.LongLength : 0L;
            Error = error;
            ErrorMessage = errorMessage;
            cachedText = null;
        }

        /// <summary>
        /// 获取请求是否成功（状态码 2xx 且无网络层错误）。
        /// </summary>
        public bool IsSuccess
        {
            get { return Error == WebRequestError.None && StatusCode >= 200 && StatusCode < 300; }
        }

        /// <summary>
        /// 获取响应体文本（UTF-8 解码，首次调用时缓存结果）。
        /// </summary>
        public string Text
        {
            get
            {
                if (cachedText == null && Data != null)
                {
                    cachedText = Encoding.UTF8.GetString(Data);
                }

                return cachedText ?? string.Empty;
            }
        }

        /// <summary>
        /// 获取指定响应头的第一个值，不存在时返回 null。
        /// </summary>
        /// <param name="name">头名称（大小写不敏感）。</param>
        /// <returns>头值，不存在时返回 null。</returns>
        public string GetHeader(string name)
        {
            if (string.IsNullOrEmpty(name) || Headers == null)
            {
                return null;
            }

            string key = name.ToLowerInvariant();
            Headers.TryGetValue(key, out string value);
            return value;
        }
    }
}
