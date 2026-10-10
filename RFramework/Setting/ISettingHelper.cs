namespace RFramework
{
    /// <summary>为简单应用设置提供平台键值存储。</summary>
    public interface ISettingHelper
    {
        bool HasKey(string key);
        void DeleteKey(string key);
        int GetInt(string key, int defaultValue);
        void SetInt(string key, int value);
        float GetFloat(string key, float defaultValue);
        void SetFloat(string key, float value);
        string GetString(string key, string defaultValue);
        void SetString(string key, string value);
        void Save();
    }
}
