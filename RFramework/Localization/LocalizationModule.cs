using System;
using System.Collections.Generic;

namespace RFramework
{
    /// <summary>
    /// 本地化模块核心实现。管理每语言一个字典，不参与资源加载和具体格式解析。
    /// </summary>
    internal sealed class LocalizationModule : RFrameworkModule, ILocalizationModule
    {
        private readonly List<string> supportedLanguages = new List<string>();
        private readonly Dictionary<string, Dictionary<string, string>> languageDicts =
            new Dictionary<string, Dictionary<string, string>>();

        private ILocalizationHelper localizationHelper;
        private IEventModule eventModule;
        private string currentLanguage;

        public string CurrentLanguage => currentLanguage;

        public IReadOnlyList<string> SupportedLanguages => supportedLanguages;

        public int LoadedLanguageCount => languageDicts.Count;

        internal override int Priority => 40;

        public void SetHelper(ILocalizationHelper helper)
        {
            if (helper == null)
            {
                throw new RFrameworkException("Localization helper is invalid.");
            }

            if (languageDicts.Count > 0 && !ReferenceEquals(localizationHelper, helper))
            {
                throw new RFrameworkException(
                    "Localization helper cannot be replaced while language packs are loaded. "
                    + "Unload all language packs first.");
            }

            localizationHelper = helper;
        }

        public void LoadLanguage(string language, byte[] bytes)
        {
            ValidateLanguage(language);
            EnsureHelper();
            if (bytes == null || bytes.Length == 0)
            {
                throw new RFrameworkException($"Language data for '{language}' is empty.");
            }

            Dictionary<string, string> parsed = localizationHelper.ParseLanguage(language, bytes);
            ReplaceLanguage(language, parsed);
        }

        public void LoadLanguageBundle(byte[] bytes)
        {
            EnsureHelper();
            if (!(localizationHelper is ILocalizationBundleHelper bundleHelper))
            {
                throw new RFrameworkException(
                    $"Localization helper '{localizationHelper.GetType().FullName}' "
                    + "does not support language bundles.");
            }

            if (bytes == null || bytes.Length == 0)
            {
                throw new RFrameworkException("Localization bundle data is empty.");
            }

            IReadOnlyDictionary<string, Dictionary<string, string>> parsed =
                bundleHelper.ParseLanguageBundle(bytes);
            if (parsed == null || parsed.Count == 0)
            {
                throw new RFrameworkException(
                    "Localization helper returned an empty language bundle.");
            }

            Dictionary<string, Dictionary<string, string>> oldLanguages =
                new Dictionary<string, Dictionary<string, string>>();
            List<string> oldSupported = new List<string>(supportedLanguages);
            List<string> committed = new List<string>(parsed.Count);
            try
            {
                foreach (KeyValuePair<string, Dictionary<string, string>> pair in parsed)
                {
                    ValidateLanguage(pair.Key);
                    if (pair.Value == null || pair.Value.Count == 0)
                    {
                        throw new RFrameworkException(
                            $"Language bundle contains no entries for '{pair.Key}'.");
                    }

                    if (languageDicts.TryGetValue(
                        pair.Key, out Dictionary<string, string> oldLanguage))
                    {
                        oldLanguages.Add(pair.Key, oldLanguage);
                    }
                }

                foreach (KeyValuePair<string, Dictionary<string, string>> pair in parsed)
                {
                    languageDicts[pair.Key] = pair.Value;
                    if (!supportedLanguages.Contains(pair.Key))
                    {
                        supportedLanguages.Add(pair.Key);
                    }

                    committed.Add(pair.Key);
                }
            }
            catch
            {
                for (int i = 0; i < committed.Count; i++)
                {
                    string language = committed[i];
                    if (oldLanguages.TryGetValue(
                        language, out Dictionary<string, string> oldLanguage))
                    {
                        languageDicts[language] = oldLanguage;
                    }
                    else
                    {
                        languageDicts.Remove(language);
                    }
                }

                supportedLanguages.Clear();
                supportedLanguages.AddRange(oldSupported);
                foreach (KeyValuePair<string, Dictionary<string, string>> pair in parsed)
                {
                    if (!oldLanguages.TryGetValue(
                        pair.Key, out Dictionary<string, string> oldLanguage)
                        || !ReferenceEquals(oldLanguage, pair.Value))
                    {
                        try { localizationHelper.ReleaseLanguage(pair.Key, pair.Value); }
                        catch { }
                    }
                }

                throw;
            }

            foreach (KeyValuePair<string, Dictionary<string, string>> pair in oldLanguages)
            {
                if (!ReferenceEquals(pair.Value, parsed[pair.Key]))
                {
                    try { localizationHelper.ReleaseLanguage(pair.Key, pair.Value); }
                    catch { }
                }
            }
        }

        public void LoadLanguageFromString(string language, string json)
        {
            ValidateLanguage(language);
            EnsureHelper();
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new RFrameworkException($"Language JSON for '{language}' is empty.");
            }

            Dictionary<string, string> parsed = localizationHelper.ParseLanguageFromString(language, json);
            ReplaceLanguage(language, parsed);
        }

        public void SwitchLanguage(string language)
        {
            ValidateLanguage(language);
            if (!languageDicts.ContainsKey(language))
            {
                throw new RFrameworkException(
                    $"Language '{language}' is not loaded. Load it before switching.");
            }

            string previous = currentLanguage;
            currentLanguage = language;

            IEventModule evt = GetEventModule();
            if (evt != null && previous != null && previous != language)
            {
                evt.FireSafely(new LanguageChangedEvent(previous, language));
            }
        }

        public bool HasLanguage(string language)
        {
            return !string.IsNullOrEmpty(language) && languageDicts.ContainsKey(language);
        }

        public void UnloadLanguage(string language)
        {
            if (string.IsNullOrEmpty(language)
                || !languageDicts.TryGetValue(language, out Dictionary<string, string> dict))
            {
                return;
            }

            Exception releaseError = null;
            try
            {
                localizationHelper?.ReleaseLanguage(language, dict);
            }
            catch (Exception ex)
            {
                releaseError = ex;
            }
            finally
            {
                languageDicts.Remove(language);
                supportedLanguages.Remove(language);
                if (currentLanguage == language)
                {
                    currentLanguage = null;
                }
            }

            if (releaseError != null)
            {
                throw new RFrameworkException(
                    $"Failed to release language '{language}'.", releaseError);
            }
        }

        public string GetString(string key)
        {
            return GetStringInternal(key);
        }

        public string GetString(string key, params object[] args)
        {
            string format = GetStringInternal(key);
            if (format == null || args == null || args.Length == 0)
            {
                return format;
            }

            try
            {
                return string.Format(format, args);
            }
            catch (FormatException)
            {
                return format;
            }
        }

        public bool HasString(string key)
        {
            return key != null
                && currentLanguage != null
                && languageDicts.TryGetValue(currentLanguage, out Dictionary<string, string> dict)
                && dict.ContainsKey(key);
        }

        internal override void Update(float elapseSeconds, float realElapseSeconds)
        {
        }

        internal override void Shutdown()
        {
            if (localizationHelper != null)
            {
                foreach (KeyValuePair<string, Dictionary<string, string>> pair in languageDicts)
                {
                    try
                    {
                        localizationHelper.ReleaseLanguage(pair.Key, pair.Value);
                    }
                    catch
                    {
                        // 关闭阶段逐项尽力清理，单个 Helper 异常不能阻断框架关闭。
                    }
                }
            }

            languageDicts.Clear();
            supportedLanguages.Clear();
            currentLanguage = null;
            localizationHelper = null;
            eventModule = null;
        }

        private void ReplaceLanguage(string language, Dictionary<string, string> parsed)
        {
            if (parsed == null || parsed.Count == 0)
            {
                throw new RFrameworkException(
                    $"Failed to load language '{language}'. Helper returned no entries.");
            }

            bool hadOld = languageDicts.TryGetValue(language, out Dictionary<string, string> old);
            languageDicts[language] = parsed;
            if (!supportedLanguages.Contains(language))
            {
                supportedLanguages.Add(language);
            }

            if (hadOld && !ReferenceEquals(old, parsed))
            {
                try
                {
                    localizationHelper.ReleaseLanguage(language, old);
                }
                catch
                {
                    // 新语言包已成功提交，旧包释放失败不应回滚有效数据。
                }
            }
        }

        private static void ValidateLanguage(string language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                throw new RFrameworkException("Language code is invalid.");
            }
        }

        private void EnsureHelper()
        {
            if (localizationHelper == null)
            {
                throw new RFrameworkException("Localization helper is not set.");
            }
        }

        private IEventModule GetEventModule()
        {
            if (eventModule != null)
            {
                return eventModule;
            }

            try
            {
                eventModule = RFrameworkModuleEntry.GetModule<IEventModule>();
            }
            catch
            {
                return null;
            }

            return eventModule;
        }

        private string GetStringInternal(string key)
        {
            if (key == null || currentLanguage == null)
            {
                return key;
            }

            return languageDicts.TryGetValue(currentLanguage, out Dictionary<string, string> dict)
                && dict.TryGetValue(key, out string value)
                ? value
                : key;
        }
    }
}
