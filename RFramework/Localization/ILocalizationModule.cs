using System.Collections.Generic;
using System.Threading.Tasks;
using RFramework.Event;
using RFramework.Resource;

namespace RFramework.Localization
{
    /// <summary>
    /// 本地化模块接口。
    /// 提供语言包加载/切换、文本查询、格式化占位符替换。
    /// 通过 ILocalizationHelper 桥接具体数据源（Luban/CSV/JSON）。
    /// </summary>
    public interface ILocalizationModule
    {
        /// <summary>
        /// 当前语言代码，如 "zh-CN"。
        /// </summary>
        string CurrentLanguage { get; }

        /// <summary>
        /// 支持的语言代码列表。
        /// </summary>
        IReadOnlyList<string> SupportedLanguages { get; }

        /// <summary>
        /// 已加载的语言数量。
        /// </summary>
        int LoadedLanguageCount { get; }

        /// <summary>
        /// 设置依赖模块引用。
        /// </summary>
        /// <param name="resourceModule">资源模块（预留）。</param>
        /// <param name="eventModule">事件模块，用于分发 LanguageChangedEvent。</param>
        void SetDependencies(IResourceModule resourceModule, IEventModule eventModule);

        /// <summary>
        /// 设置本地化辅助器。
        /// </summary>
        /// <param name="helper">辅助器实例。</param>
        void SetHelper(ILocalizationHelper helper);

        /// <summary>
        /// 异步加载语言包（不切换，仅加载数据到内存）。
        /// </summary>
        /// <param name="language">语言代码。</param>
        Task LoadLanguageAsync(string language);

        /// <summary>
        /// 异步切换语言：自动卸载旧语言包 → 加载新语言包 → 分发 LanguageChangedEvent。
        /// </summary>
        /// <param name="language">目标语言代码。</param>
        Task SwitchLanguageAsync(string language);

        /// <summary>
        /// 卸载指定语言包数据。
        /// </summary>
        /// <param name="language">语言代码。</param>
        void UnloadLanguage(string language);

        /// <summary>
        /// 获取当前语言的本地化文本。
        /// </summary>
        /// <param name="key">文本 key。</param>
        /// <returns>本地化后的文本。key 不存在时返回 key 本身。</returns>
        string GetString(string key);

        /// <summary>
        /// 获取当前语言的本地化文本，并用参数替换占位符（{0}、{1}...）。
        /// </summary>
        /// <param name="key">文本 key。</param>
        /// <param name="args">占位符替换参数。</param>
        /// <returns>格式化后的文本。key 不存在时返回 key 本身。</returns>
        string GetString(string key, params object[] args);

        /// <summary>
        /// 检查指定 key 是否存在。
        /// </summary>
        /// <param name="key">文本 key。</param>
        bool HasString(string key);
    }
}
