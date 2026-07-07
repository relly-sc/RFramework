namespace RFramework.UI
{
    /// <summary>
    /// UI 打开成功事件。由 UIModule 在 OpenUIFormAsync 完成后分发。
    /// </summary>
    public readonly struct OpenUIFormSuccessEvent
    {
        public readonly string AssetName;
        public readonly IUIForm UIForm;
        public readonly float Duration;
        public readonly object UserData;

        public OpenUIFormSuccessEvent(string assetName, IUIForm uiForm, float duration, object userData)
        {
            AssetName = assetName;
            UIForm = uiForm;
            Duration = duration;
            UserData = userData;
        }
    }
}
