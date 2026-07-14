using System.Collections.Generic;

namespace RFramework
{
    /// <summary>
    /// 本地化数据解析辅助器。只负责把原始数据解析为语言字典，不负责加载资源。
    /// 字节数据可以是 JSON、框架二进制协议或用户自定义格式。
    /// </summary>
    public interface ILocalizationHelper
    {
        /// <summary>
        /// 从原始字节解析指定语言的语言包。
        /// </summary>
        Dictionary<string, string> ParseLanguage(string language, byte[] bytes);

        /// <summary>
        /// 从 JSON 字符串解析指定语言的语言包。
        /// 不支持 JSON 的自定义 Helper 可明确抛出 NotSupportedException。
        /// </summary>
        Dictionary<string, string> ParseLanguageFromString(string language, string json);

        /// <summary>
        /// 释放已解析的语言包。默认字典实现通常只需清空字典。
        /// </summary>
        void ReleaseLanguage(string language, Dictionary<string, string> languageDict);
    }
}
