using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RFramework.Event;
using RFramework.Resource;

namespace RFramework.Localization
{
    /// <summary>
    /// 本地化模块核心实现。
    /// 管理语言包字典（language → key → value），
    /// 支持运行时语言切换 + 占位符格式化 + 事件分发。
    /// </summary>
    internal sealed class LocalizationModule : RFrameworkModule, ILocalizationModule
    {
        /// <summary>
        /// 本地化辅助器引用。
        /// </summary>
        private ILocalizationHelper localizationHelper;

        /// <summary>
        /// 资源模块引用（预留）。
        /// </summary>
        private IResourceModule resourceModule;

        /// <summary>
        /// 事件模块引用。
        /// </summary>
        private IEventModule eventModule;

        /// <summary>
        /// 支持的语言代码列表。
        /// </summary>
        private readonly List<string> supportedLanguages = new List<string>();

        /// <summary>
        /// 语言包字典：language → （key → value）。
        /// </summary>
        private readonly Dictionary<string, Dictionary<string, string>> languageDicts = new Dictionary<string, Dictionary<string, string>>();

        /// <summary>
        /// 当前语言代码。
        /// </summary>
        private string currentLanguage;

        /// <inheritdoc/>
        public string CurrentLanguage
        {
            get { return currentLanguage; }
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> SupportedLanguages
        {
            get { return supportedLanguages; }
        }

        /// <inheritdoc/>
        public int LoadedLanguageCount
        {
            get { return languageDicts.Count; }
        }

        /// <inheritdoc/>
        internal override int Priority
        {
            get
            {
                return 40;
            }
        }

        /// <inheritdoc/>
        public void SetHelper(ILocalizationHelper helper)
        {
            localizationHelper = helper;
        }

        /// <inheritdoc/>
        public void SetDependencies(IResourceModule resourceModule, IEventModule eventModule)
        {
            this.resourceModule = resourceModule;
            this.eventModule = eventModule;
        }

        /// <inheritdoc/>
        public Task LoadLanguageAsync(string language)
        {
            if (string.IsNullOrEmpty(language))
            {
                throw new RFrameworkException("Language code is invalid.");
            }

            if (localizationHelper == null)
            {
                throw new RFrameworkException("Localization helper is not set.");
            }

            LoadLanguageInternal(language);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task SwitchLanguageAsync(string language)
        {
            if (string.IsNullOrEmpty(language))
            {
                throw new RFrameworkException("Language code is invalid.");
            }

            if (localizationHelper == null)
            {
                throw new RFrameworkException("Localization helper is not set.");
            }

            string previous = currentLanguage;

            // 加载目标语言包（如果还未加载）
            if (!languageDicts.ContainsKey(language))
            {
                LoadLanguageInternal(language);
            }

            // 切换当前语言
            currentLanguage = language;

            // 添加到支持列表（首次使用时自动注册）
            if (!supportedLanguages.Contains(language))
            {
                supportedLanguages.Add(language);
            }

            // 分发事件
            if (eventModule != null && previous != null && previous != language)
            {
                eventModule.FireSafely(new LanguageChangedEvent(previous, language));
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public void UnloadLanguage(string language)
        {
            if (languageDicts.TryGetValue(language, out Dictionary<string, string> dict))
            {
                localizationHelper?.UnloadLanguageDict(language);
                languageDicts.Remove(language);
                supportedLanguages.Remove(language);

                // 如果卸载的是当前语言，清除当前语言引用
                if (currentLanguage == language)
                {
                    currentLanguage = null;
                }
            }
        }

        /// <inheritdoc/>
        public string GetString(string key)
        {
            return GetStringInternal(key);
        }

        /// <inheritdoc/>
        public string GetString(string key, params object[] args)
        {
            string format = GetStringInternal(key);

            if (args != null && args.Length > 0)
            {
                try
                {
                    return string.Format(format, args);
                }
                catch (FormatException)
                {
                    return format;
                }
            }

            return format;
        }

        /// <inheritdoc/>
        public bool HasString(string key)
        {
            if (currentLanguage == null)
            {
                return false;
            }

            if (!languageDicts.TryGetValue(currentLanguage, out Dictionary<string, string> dict))
            {
                return false;
            }

            return dict.ContainsKey(key);
        }

        /// <inheritdoc/>
        internal override void Update(float elapseSeconds, float realElapseSeconds)
        {
        }

        /// <inheritdoc/>
        internal override void Shutdown()
        {
            // 卸载所有语言包
            if (localizationHelper != null)
            {
                foreach (string lang in languageDicts.Keys)
                {
                    localizationHelper.UnloadLanguageDict(lang);
                }
            }

            languageDicts.Clear();
            supportedLanguages.Clear();
            currentLanguage = null;
        }

        /// <summary>
        /// 内部加载语言包：调用 Helper 获取字典 → 缓存到 languageDicts。
        /// </summary>
        /// <param name="language">语言代码。</param>
        private void LoadLanguageInternal(string language)
        {
            Dictionary<string, string> dict = localizationHelper.LoadLanguageDict(language);
            if (dict == null)
            {
                throw new RFrameworkException($"Failed to load language '{language}'. Helper returned null.");
            }

            languageDicts[language] = dict;

            // 若尚无当前语言，自动设为第一个加载的语言
            if (currentLanguage == null)
            {
                currentLanguage = language;
            }

            if (!supportedLanguages.Contains(language))
            {
                supportedLanguages.Add(language);
            }
        }

        /// <summary>
        /// 内部查询：从当前语言字典中获取文本。key 不存在时返回 key 本身。
        /// </summary>
        /// <param name="key">文本 key。</param>
        /// <returns>本地化文本。</returns>
        private string GetStringInternal(string key)
        {
            if (currentLanguage == null)
            {
                return key;
            }

            if (!languageDicts.TryGetValue(currentLanguage, out Dictionary<string, string> dict))
            {
                return key;
            }

            if (!dict.TryGetValue(key, out string value))
            {
                return key;
            }

            return value;
        }
    }
}
