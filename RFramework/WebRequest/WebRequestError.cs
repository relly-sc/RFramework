namespace RFramework
{
    /// <summary>
    /// 请求错误分类枚举。
    /// 借鉴 BestHTTP 的 5 级错误分类并扩展，区分连接层、协议层和用户操作。
    /// </summary>
    public enum WebRequestError
    {
        /// <summary>无错误，请求成功完成。</summary>
        None,

        /// <summary>连接超时：在指定时间内未能与服务器建立连接。</summary>
        ConnectionTimeout,

        /// <summary>请求处理超时：连接已建立但响应未在指定时间内返回。</summary>
        RequestTimeout,

        /// <summary>用户主动取消（通过 CancellationToken 或 CancelAllByTag）。</summary>
        Aborted,

        /// <summary>网络层错误：DNS 解析失败、连接被拒绝、TLS 握手失败等。</summary>
        NetworkError,

        /// <summary>HTTP 协议层错误：服务器返回 4xx/5xx 状态码，但响应体可能仍有内容。</summary>
        HttpError,

        /// <summary>未知错误：无法归类到上述类型的异常。</summary>
        Unknown
    }
}
