namespace RFramework
{
    public readonly struct CloseUIFormCompleteEvent
    {
        public readonly string AssetName;
        public readonly object UserData;

        public CloseUIFormCompleteEvent(string assetName, object userData)
        {
            AssetName = assetName;
            UserData = userData;
        }
    }
}
