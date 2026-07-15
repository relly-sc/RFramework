using System.Collections.Generic;

namespace RFramework
{
    /// <summary>
    /// 本地化模块接口。负责语言包缓存、切换、查询和事件分发。
    /// 文件加载由 Runtime Component 通过 ResourceModule 编排。
    /// </summary>
    public interface ILocalizationModule
    {
        string CurrentLanguage { get; }

        IReadOnlyList<string> SupportedLanguages { get; }

        int LoadedLanguageCount { get; }

        void SetHelper(ILocalizationHelper helper);

        /// <summary>从 Helper 定义的字节格式解析并缓存一个语言包。</summary>
        void LoadLanguage(string language, byte[] bytes);

        /// <summary>从一个容器原子加载多种语言。</summary>
        void LoadLanguageBundle(byte[] bytes);

        /// <summary>从 JSON 字符串解析并缓存一个语言包。</summary>
        void LoadLanguageFromString(string language, string json);

        /// <summary>切换到已经加载的语言。</summary>
        void SwitchLanguage(string language);

        bool HasLanguage(string language);

        void UnloadLanguage(string language);

        string GetString(string key);

        string GetString(string key, params object[] args);

        bool HasString(string key);
    }
}
