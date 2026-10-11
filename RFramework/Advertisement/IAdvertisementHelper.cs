using System;

namespace RFramework
{
    /// <summary>
    /// 广告平台适配器接口。具体平台 SDK 由可选扩展实现，核心不依赖厂商程序集。
    /// </summary>
    public interface IAdvertisementHelper
    {
        string HelperName { get; }
        bool IsSupported { get; }
        bool IsInitialized { get; }
        void Initialize();
        void Load(AdvertisementType type, string placementId);
        bool IsReady(AdvertisementType type, string placementId);
        bool Show(AdvertisementType type, string placementId, Action<AdvertisementResult> callback);
        void Hide(AdvertisementType type, string placementId);
    }
}
