namespace RFramework.UI
{
    public readonly struct OpenUIFormFailureEvent
    {
        public readonly string AssetName;
        public readonly string ErrorMessage;
        public readonly object UserData;

        public OpenUIFormFailureEvent(string assetName, string errorMessage, object userData)
        {
            AssetName = assetName;
            ErrorMessage = errorMessage;
            UserData = userData;
        }
    }
}
