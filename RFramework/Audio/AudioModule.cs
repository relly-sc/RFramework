using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace RFramework
{
    /// <summary>
    /// 音频模块核心实现。内置 BGM/SFX/UI 三组音轨，
    /// BGM 单实例 + 淡入淡出，SFX 并发，UI 即时。
    /// 每个音效返回 AudioHandle，支持单独停止。
    /// 引擎特定操作（AudioSource 播放、回调协程）全部委托给 IAudioHelper。
    /// </summary>
    internal sealed class AudioModule : RFrameworkModule, IAudioModule
    {
        /// <summary>
        /// 下一个句柄 ID。
        /// </summary>
        private int nextHandleId = 1;

        /// <summary>
        /// 音频辅助器引用。
        /// </summary>
        private IAudioHelper audioHelper;

        /// <summary>
        /// 资源模块引用。
        /// </summary>
        private IResourceModule resourceModule;

        /// <summary>
        /// 事件模块引用（预留）。
        /// </summary>
        private IEventModule eventModule;

        /// <summary>
        /// 对象池模块引用（预留）。
        /// </summary>
        private IPoolModule poolModule;

        /// <summary>
        /// 已加载的音频资源缓存（assetName → AudioClip）。
        /// </summary>
        private readonly Dictionary<string, object> loadedAudioAssets = new Dictionary<string, object>();

        /// <summary>
        /// 当前 BGM 资源路径。
        /// </summary>
        private string currentBgmAssetName;

        /// <summary>
        /// 当前 BGM 音量倍率（用于静音恢复时重新计算）。
        /// </summary>
        private float currentBgmVolume = 1f;

        /// <summary>
        /// BGM 是否处于暂停状态。
        /// </summary>
        private bool bgmPaused;

        /// <summary>
        /// 全局静音状态。
        /// </summary>
        private bool muted;

        /// <summary>
        /// 当前 BGM 句柄 ID（内部追踪，用于区分 BGM/SFX 停止）。
        /// </summary>
        private int bgmHandleId;

        /// <inheritdoc/>
        public float BgmVolume { get; set; } = 1f;

        /// <inheritdoc/>
        public float SfxVolume { get; set; } = 1f;

        /// <inheritdoc/>
        public float UIVolume { get; set; } = 1f;

        /// <inheritdoc/>
        public bool Muted
        {
            get { return muted; }
            set
            {
                muted = value;
                ApplyMuteState();
            }
        }

        /// <inheritdoc/>
        internal override int Priority
        {
            get
            {
                return 35;
            }
        }

        /// <inheritdoc/>
        public void SetHelper(IAudioHelper helper)
        {
            audioHelper = helper;
        }

        /// <inheritdoc/>
        public void SetDependencies(IResourceModule resourceModule, IEventModule eventModule, IPoolModule poolModule)
        {
            this.resourceModule = resourceModule;
            this.eventModule = eventModule;
            this.poolModule = poolModule;
        }

        // ====== BGM ======

        /// <inheritdoc/>
        public AudioHandle PlayBgm(string assetName, float volume = 1f, bool loop = true,
            float fadeInSeconds = 0f, float completeDelaySeconds = 0f, Action onComplete = null)
        {
            if (string.IsNullOrEmpty(assetName))
            {
                throw new RFrameworkException("BGM asset name is invalid.");
            }

            if (audioHelper == null)
            {
                throw new RFrameworkException("Audio helper is not set.");
            }

            // 停止旧 BGM（同时取消旧完成回调）
            if (currentBgmAssetName != null)
            {
                StopBgmInternal();
            }

            object audioAsset = LoadAudioAsset(assetName);
            currentBgmAssetName = assetName;
            currentBgmVolume = volume;
            bgmPaused = false;

            int handleId = nextHandleId++;
            bgmHandleId = handleId;

            audioHelper.PlayBgm(audioAsset, GetFinalVolume(volume, BgmVolume), loop,
                fadeInSeconds, completeDelaySeconds, onComplete);
            return new AudioHandle(handleId, this);
        }

        /// <inheritdoc/>
        public async Task<AudioHandle> PlayBgmAsync(string assetName, float volume = 1f,
            bool loop = true, float fadeInSeconds = 0f, float completeDelaySeconds = 0f,
            Action onComplete = null, CancellationToken ct = default)
        {
            ValidateAudioRequest(assetName, "BGM");
            object audioAsset = await LoadAudioAssetAsync(assetName, ct);
            ct.ThrowIfCancellationRequested();

            if (currentBgmAssetName != null)
            {
                StopBgmInternal();
            }

            currentBgmAssetName = assetName;
            currentBgmVolume = volume;
            bgmPaused = false;
            int handleId = nextHandleId++;
            bgmHandleId = handleId;
            audioHelper.PlayBgm(audioAsset, GetFinalVolume(volume, BgmVolume), loop,
                fadeInSeconds, completeDelaySeconds, onComplete);
            return new AudioHandle(handleId, this);
        }

        /// <inheritdoc/>
        public void StopBgm(float fadeOutSeconds = 0f)
        {
            if (currentBgmAssetName == null)
            {
                return;
            }

            StopBgmInternal(fadeOutSeconds);
        }

        /// <inheritdoc/>
        public void PauseBgm()
        {
            if (currentBgmAssetName == null)
            {
                return;
            }

            bgmPaused = true;
            audioHelper.PauseBgm();
        }

        /// <inheritdoc/>
        public void ResumeBgm()
        {
            if (currentBgmAssetName == null || !bgmPaused)
            {
                return;
            }

            bgmPaused = false;
            audioHelper.ResumeBgm();
        }

        // ====== SFX ======

        /// <inheritdoc/>
        public AudioHandle PlaySfx(string assetName, float volume = 1f,
            float completeDelaySeconds = 0f, Action onComplete = null)
        {
            if (string.IsNullOrEmpty(assetName))
            {
                throw new RFrameworkException("SFX asset name is invalid.");
            }

            if (audioHelper == null)
            {
                throw new RFrameworkException("Audio helper is not set.");
            }

            object audioAsset = LoadAudioAsset(assetName);
            int handleId = nextHandleId++;

            audioHelper.PlaySfx(handleId, audioAsset, GetFinalVolume(volume, SfxVolume),
                completeDelaySeconds, onComplete);
            return new AudioHandle(handleId, this);
        }

        /// <inheritdoc/>
        public async Task<AudioHandle> PlaySfxAsync(string assetName, float volume = 1f,
            float completeDelaySeconds = 0f, Action onComplete = null,
            CancellationToken ct = default)
        {
            ValidateAudioRequest(assetName, "SFX");
            object audioAsset = await LoadAudioAssetAsync(assetName, ct);
            ct.ThrowIfCancellationRequested();
            int handleId = nextHandleId++;
            audioHelper.PlaySfx(handleId, audioAsset, GetFinalVolume(volume, SfxVolume),
                completeDelaySeconds, onComplete);
            return new AudioHandle(handleId, this);
        }

        /// <inheritdoc/>
        public void StopAllSfx()
        {
            audioHelper?.StopAllSfx();
        }

        // ====== UI ======

        /// <inheritdoc/>
        public void PlayUI(string assetName, float volume = 1f)
        {
            if (string.IsNullOrEmpty(assetName))
            {
                throw new RFrameworkException("UI asset name is invalid.");
            }

            if (audioHelper == null)
            {
                throw new RFrameworkException("Audio helper is not set.");
            }

            object audioAsset = LoadAudioAsset(assetName);
            audioHelper.PlayUI(audioAsset, GetFinalVolume(volume, UIVolume));
        }

        /// <inheritdoc/>
        public async Task PlayUIAsync(string assetName, float volume = 1f,
            CancellationToken ct = default)
        {
            ValidateAudioRequest(assetName, "UI");
            object audioAsset = await LoadAudioAssetAsync(assetName, ct);
            ct.ThrowIfCancellationRequested();
            audioHelper.PlayUI(audioAsset, GetFinalVolume(volume, UIVolume));
        }

        /// <inheritdoc/>
        public void StopAll()
        {
            if (audioHelper == null)
            {
                currentBgmAssetName = null;
                bgmPaused = false;
                bgmHandleId = 0;
                return;
            }

            StopBgmInternal();
            audioHelper.StopAllSfx();
        }

        /// <inheritdoc/>
        public void ClearCache()
        {
            StopAll();
            UnloadAllAssets();
        }

        // ====== 句柄管理 ======

        /// <summary>
        /// 内部停止句柄（由 AudioHandle.Stop() 触发）。
        /// 停止指定音效并取消其完成回调。
        /// </summary>
        /// <param name="handleId">句柄 ID。</param>
        internal void StopHandleInternal(int handleId)
        {
            if (handleId == bgmHandleId)
            {
                StopBgmInternal();
            }
            else
            {
                audioHelper?.StopSfx(handleId);
            }
        }

        /// <summary>
        /// 计算最终音量：音量倍率 × 分类音量，限制在 0~1，静音状态直接返回 0。
        /// </summary>
        /// <param name="volumeMultiplier">播放时传入的音量倍率。</param>
        /// <param name="categoryVolume">分类音量（BgmVolume / SfxVolume / UIVolume）。</param>
        /// <returns>最终音量值。</returns>
        private float GetFinalVolume(float volumeMultiplier, float categoryVolume)
        {
            if (muted)
            {
                return 0f;
            }

            return Math.Clamp(volumeMultiplier * categoryVolume, 0f, 1f);
        }

        /// <summary>
        /// 加载音频资源（优先从缓存获取，缓存未命中则通过辅助器加载）。
        /// </summary>
        /// <param name="assetName">音频资源路径。</param>
        /// <returns>音频资源对象（Unity 层为 AudioClip）。</returns>
        private object LoadAudioAsset(string assetName)
        {
            if (loadedAudioAssets.TryGetValue(assetName, out object asset))
            {
                return asset;
            }

            if (resourceModule == null)
            {
                throw new RFrameworkException("Resource module is not set.");
            }

            object audioAsset = resourceModule.LoadAssetSync<object>(assetName);
            if (audioAsset == null)
            {
                throw new RFrameworkException($"Audio asset '{assetName}' could not be loaded.");
            }

            loadedAudioAssets.Add(assetName, audioAsset);
            return audioAsset;
        }

        /// <summary>
        /// 异步加载音频资源。并发完成时只保留一份模块缓存，多余引用立即归还 ResourceModule。
        /// </summary>
        /// <param name="assetName">音频资源路径。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>音频资源对象。</returns>
        private async Task<object> LoadAudioAssetAsync(
            string assetName, CancellationToken ct)
        {
            if (loadedAudioAssets.TryGetValue(assetName, out object cached))
            {
                return cached;
            }

            if (resourceModule == null)
            {
                throw new RFrameworkException("Resource module is not set.");
            }

            object audioAsset = await resourceModule.LoadAssetAsync<object>(
                assetName, 0, ct);
            if (audioAsset == null)
            {
                throw new RFrameworkException(
                    $"Audio asset '{assetName}' could not be loaded.");
            }

            if (loadedAudioAssets.TryGetValue(assetName, out object existing))
            {
                resourceModule.UnloadAsset<object>(assetName);
                return existing;
            }

            loadedAudioAssets.Add(assetName, audioAsset);
            return audioAsset;
        }

        private void ValidateAudioRequest(string assetName, string category)
        {
            if (string.IsNullOrEmpty(assetName))
            {
                throw new RFrameworkException($"{category} asset name is invalid.");
            }

            if (audioHelper == null)
            {
                throw new RFrameworkException("Audio helper is not set.");
            }
        }

        /// <summary>
        /// 应用静音状态到当前 BGM（静音时直接设音量为 0，取消静音时恢复）。
        /// </summary>
        private void ApplyMuteState()
        {
            if (currentBgmAssetName != null)
            {
                audioHelper.SetBgmVolume(muted ? 0f : GetFinalVolume(currentBgmVolume, BgmVolume));
            }
        }

        /// <summary>
        /// 内部停止 BGM（停止 AudioSource + 清除资源路径 + 重置暂停/句柄状态）。
        /// </summary>
        /// <param name="fadeOutSeconds">淡出时长（秒），0 为立即停止。</param>
        private void StopBgmInternal(float fadeOutSeconds = 0f)
        {
            audioHelper.StopBgm(fadeOutSeconds);
            currentBgmAssetName = null;
            bgmPaused = false;
            bgmHandleId = 0;
        }

        /// <inheritdoc/>
        internal override void Update(float elapseSeconds, float realElapseSeconds)
        {
        }

        /// <inheritdoc/>
        internal override void Shutdown()
        {
            StopAll();
            UnloadAllAssets();
        }

        /// <summary>
        /// 卸载所有已缓存的音频资源。
        /// </summary>
        private void UnloadAllAssets()
        {
            if (resourceModule != null)
            {
                foreach (KeyValuePair<string, object> kv in loadedAudioAssets)
                {
                    resourceModule.UnloadAsset<object>(kv.Key);
                }
            }

            loadedAudioAssets.Clear();
            currentBgmAssetName = null;
        }
    }
}
