namespace RFramework.WebRequest
{
    /// <summary>
    /// Multipart 表单字段（纯 C#，零 Unity 依赖）。
    /// 每个字段可以是文本值或二进制文件，用于 multipart/form-data 上传。
    /// 文本字段：<see cref="Value"/> 有效而 <see cref="Data"/> 为 null。
    /// 文件字段：<see cref="Data"/> 有效而 <see cref="Value"/> 为 null。
    /// </summary>
    public class MultipartField
    {
        /// <summary>
        /// 字段名称（对应表单的 name 属性）。
        /// </summary>
        public string Name;

        /// <summary>
        /// 文件名（仅文件字段有效，文本字段为 null）。
        /// 此值出现在 multipart Content-Disposition 的 filename 参数中。
        /// </summary>
        public string FileName;

        /// <summary>
        /// 子类型 MIME（仅文件字段有效，如 "image/png"、"application/pdf"）。
        /// 此值出现在 multipart 部件内嵌的 Content-Type 头中。
        /// </summary>
        public string ContentType;

        /// <summary>
        /// 二进制数据（仅文件字段有效，文本字段为 null）。
        /// </summary>
        public byte[] Data;

        /// <summary>
        /// 文本值（仅文本字段有效，文件字段为 null）。
        /// </summary>
        public string Value;

        /// <summary>
        /// 获取该字段是否为文件字段。
        /// </summary>
        public bool IsFile
        {
            get { return Data != null && !string.IsNullOrEmpty(FileName); }
        }

        /// <summary>
        /// 创建一个文本表单字段。
        /// </summary>
        /// <param name="name">字段名称。</param>
        /// <param name="value">文本值。</param>
        public static MultipartField CreateText(string name, string value)
        {
            return new MultipartField { Name = name, Value = value };
        }

        /// <summary>
        /// 创建一个文件表单字段。
        /// </summary>
        /// <param name="name">字段名称。</param>
        /// <param name="fileName">文件名。</param>
        /// <param name="data">文件二进制数据。</param>
        /// <param name="contentType">文件 MIME 类型。</param>
        public static MultipartField CreateFile(string name, string fileName, byte[] data, string contentType = "application/octet-stream")
        {
            return new MultipartField
            {
                Name = name,
                FileName = fileName,
                Data = data,
                ContentType = contentType
            };
        }
    }
}
