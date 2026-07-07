namespace RFramework.Localization
{
    /// <summary>
    /// 语言切换事件。由 LocalizationModule 在 SwitchLanguageAsync 成功后分发。
    /// </summary>
    public readonly struct LanguageChangedEvent
    {
        /// <summary>
        /// 切换前的语言代码。
        /// </summary>
        public readonly string PreviousLanguage;

        /// <summary>
        /// 切换后的语言代码。
        /// </summary>
        public readonly string CurrentLanguage;

        /// <summary>
        /// 构造语言切换事件。
        /// </summary>
        /// <param name="previousLanguage">切换前的语言代码。</param>
        /// <param name="currentLanguage">切换后的语言代码。</param>
        public LanguageChangedEvent(string previousLanguage, string currentLanguage)
        {
            PreviousLanguage = previousLanguage;
            CurrentLanguage = currentLanguage;
        }
    }
}
