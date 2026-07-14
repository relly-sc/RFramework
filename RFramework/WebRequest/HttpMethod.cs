namespace RFramework
{
    /// <summary>
    /// HTTP 请求方法枚举。
    /// 覆盖 RESTful API 常用的六种语义操作。
    /// </summary>
    public enum HttpMethod
    {
        /// <summary>GET：请求指定资源，仅用于检索数据。</summary>
        Get,

        /// <summary>POST：向指定资源提交数据以进行处理（如创建新资源）。</summary>
        Post,

        /// <summary>PUT：替换指定资源的所有当前表示。</summary>
        Put,

        /// <summary>DELETE：删除指定资源。</summary>
        Delete,

        /// <summary>HEAD：与 GET 相同，但只返回响应头不返回正文。</summary>
        Head,

        /// <summary>PATCH：对资源进行部分修改。</summary>
        Patch
    }
}
