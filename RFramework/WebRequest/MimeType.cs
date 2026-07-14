namespace RFramework
{
    /// <summary>
    /// MIME 内容类型枚举。
    /// 对应 HTTP Content-Type 请求头，模块内部自动映射为标准字符串值。
    /// </summary>
    public enum MimeType
    {
        /// <summary>application/json</summary>
        Json,

        /// <summary>application/xml</summary>
        Xml,

        /// <summary>application/x-www-form-urlencoded（表单提交）</summary>
        FormUrlEncoded,

        /// <summary>text/plain</summary>
        TextPlain,

        /// <summary>application/octet-stream（二进制流）</summary>
        OctetStream
    }
}
