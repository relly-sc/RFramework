using System;

namespace RFramework
{
    internal sealed class AdvertisementModule : RFrameworkModule, IAdvertisementModule
    {
        private IAdvertisementHelper helper;

        internal override int Order => 36;
        public string HelperName => helper?.HelperName ?? "None";
        public bool IsSupported => helper != null && helper.IsSupported;
        public bool IsInitialized => helper != null && helper.IsInitialized;

        public void SetHelper(IAdvertisementHelper value)
        {
            if (value == null)
            {
                throw new RFrameworkException("Advertisement helper is invalid.");
            }

            if (!ReferenceEquals(helper, value))
            {
                helper = value;
            }
        }

        public void Initialize()
        {
            RequireHelper().Initialize();
        }

        public void Load(AdvertisementType type, string placementId)
        {
            RequirePlacement(placementId);
            RequireHelper().Load(type, placementId);
        }

        public bool IsReady(AdvertisementType type, string placementId)
        {
            RequirePlacement(placementId);
            return RequireHelper().IsReady(type, placementId);
        }

        public bool Show(AdvertisementType type, string placementId,
            Action<AdvertisementResult> callback = null)
        {
            RequirePlacement(placementId);
            return RequireHelper().Show(type, placementId, callback);
        }

        public void Hide(AdvertisementType type, string placementId)
        {
            RequirePlacement(placementId);
            RequireHelper().Hide(type, placementId);
        }

        internal override void Tick(float deltaTime, float unscaledDeltaTime) { }

        internal override void Stop()
        {
            helper = null;
        }

        private IAdvertisementHelper RequireHelper()
        {
            return helper ?? throw new RFrameworkException(
                "Advertisement helper has not been configured.");
        }

        private static void RequirePlacement(string placementId)
        {
            if (string.IsNullOrWhiteSpace(placementId))
            {
                throw new RFrameworkException("Advertisement placement id is invalid.");
            }
        }
    }
}
