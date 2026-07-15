using System.Collections.Generic;

namespace RFramework
{
    /// <summary>
    /// 可一次解析多种语言的可选本地化辅助器接口。
    /// </summary>
    public interface ILocalizationBundleHelper
    {
        /// <summary>解析多语言容器，返回语言代码到语言字典的映射。</summary>
        IReadOnlyDictionary<string, Dictionary<string, string>> ParseLanguageBundle(
            byte[] bytes);
    }
}
