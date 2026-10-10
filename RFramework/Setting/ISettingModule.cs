namespace RFramework
{
    /// <summary>跨会话保存少量应用偏好；不用于业务存档。</summary>
    public interface ISettingModule
    {
        string HelperName { get; }
        bool HasPendingChanges { get; }
        void SetHelper(ISettingHelper helper);
        bool HasKey(string key);
        void DeleteKey(string key);
        bool GetBool(string key, bool defaultValue = false);
        void SetBool(string key, bool value);
        int GetInt(string key, int defaultValue = 0);
        void SetInt(string key, int value);
        float GetFloat(string key, float defaultValue = 0f);
        void SetFloat(string key, float value);
        string GetString(string key, string defaultValue = "");
        void SetString(string key, string value);
        void Save();
    }
}
