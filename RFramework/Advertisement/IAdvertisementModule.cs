using System;

namespace RFramework
{
    /// <summary>统一广告入口。</summary>
    public interface IAdvertisementModule
    {
        string HelperName { get; }
        bool IsSupported { get; }
        bool IsInitialized { get; }
        void SetHelper(IAdvertisementHelper helper);
        void Initialize();
        void Load(AdvertisementType type, string placementId);
        bool IsReady(AdvertisementType type, string placementId);
        bool Show(AdvertisementType type, string placementId, Action<AdvertisementResult> callback = null);
        void Hide(AdvertisementType type, string placementId);
    }
}
