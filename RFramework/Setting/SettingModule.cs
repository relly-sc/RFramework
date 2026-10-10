namespace RFramework
{
    internal sealed class SettingModule : RFrameworkModule, ISettingModule
    {
        private ISettingHelper helper;
        private bool dirty;

        internal override int Order => 25;
        public string HelperName => helper?.GetType().Name ?? "None";
        public bool HasPendingChanges => dirty;

        public void SetHelper(ISettingHelper value)
        {
            if (value == null) throw new RFrameworkException("Setting helper is invalid.");
            if (ReferenceEquals(helper, value)) return;
            Save();
            helper = value;
        }

        public bool HasKey(string key) => RequireHelper().HasKey(ValidateKey(key));

        public void DeleteKey(string key)
        {
            ISettingHelper current = RequireHelper();
            key = ValidateKey(key);
            if (!current.HasKey(key)) return;
            current.DeleteKey(key);
            dirty = true;
        }

        public bool GetBool(string key, bool defaultValue = false) =>
            GetInt(key, defaultValue ? 1 : 0) != 0;

        public void SetBool(string key, bool value) => SetInt(key, value ? 1 : 0);

        public int GetInt(string key, int defaultValue = 0) =>
            RequireHelper().GetInt(ValidateKey(key), defaultValue);

        public void SetInt(string key, int value)
        {
            RequireHelper().SetInt(ValidateKey(key), value);
            dirty = true;
        }

        public float GetFloat(string key, float defaultValue = 0f) =>
            RequireHelper().GetFloat(ValidateKey(key), defaultValue);

        public void SetFloat(string key, float value)
        {
            RequireHelper().SetFloat(ValidateKey(key), value);
            dirty = true;
        }

        public string GetString(string key, string defaultValue = "") =>
            RequireHelper().GetString(ValidateKey(key), defaultValue);

        public void SetString(string key, string value)
        {
            if (value == null) throw new RFrameworkException("Setting value is invalid.");
            RequireHelper().SetString(ValidateKey(key), value);
            dirty = true;
        }

        public void Save()
        {
            if (!dirty) return;
            RequireHelper().Save();
            dirty = false;
        }

        internal override void Tick(float deltaTime, float unscaledDeltaTime) { }

        internal override void Stop()
        {
            try { Save(); }
            finally
            {
                helper = null;
                dirty = false;
            }
        }

        private ISettingHelper RequireHelper() => helper
            ?? throw new RFrameworkException("Setting helper has not been configured.");

        private static string ValidateKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new RFrameworkException("Setting key is invalid.");
            return key;
        }
    }
}
