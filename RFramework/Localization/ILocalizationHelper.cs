using System.Collections.Generic;

namespace RFramework.Localization
{
    /// <summary>
    /// 本地化辅助器接口。负责语言包的实际加载与卸载，
    /// 将具体数据格式（Luban 表 / CSV / JSON / Unity Package）封装在实现中。
    /// </summary>
    public interface ILocalizationHelper
    {
        /// <summary>
        /// 加载指定语言的语言包数据。
        /// </summary>
        /// <param name="language">语言代码，如 "zh-CN"、"en-US"。</param>
        /// <returns>key → 模板字符串 的字典。key 未找到时返回 null。</returns>
        Dictionary<string, string> LoadLanguageDict(string language);

        /// <summary>
        /// 卸载指定语言的语言包数据。
        /// </summary>
        /// <param name="language">语言代码。</param>
        void UnloadLanguageDict(string language);
    }
}
