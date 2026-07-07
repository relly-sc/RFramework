using System;
using System.Collections.Generic;
using RFramework.Event;
using RFramework.Pool;
using RFramework.Resource;

namespace RFramework.Audio
{
    /// <summary>
    /// 音频模块核心实现。内置 BGM/SFX/UI 三组音轨，
    /// BGM 单实例 + 淡入淡出，SFX AudioSource 池并发，UI 单实例即时。
    /// 每个音效返回 AudioHandle，支持单独停止和回调取消。
    /// </summary>
    internal sealed class AudioModule : RFrameworkModule, IAudioModule
    {
        /// <summary>
        /// 下一个句柄 ID。
        /// </summary>
        private int nextHandleId = 1;

        /// <summary>
        /// 活跃句柄（Id → 延迟秒数 + 回调信息）。
        /// </summary>
        private readonly Dictionary<int, HandleCallbackInfo> activeHandles = new Dictionary<int, HandleCallbackInfo>();

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
        /// BGM 句柄 ID（内部追踪，用于匹配 AudioSource 和取消回调）。
        /// </summary>
        private int bgmHandleId;

        /// <summary>
        /// BGM 句柄 ID。Runtime 层用于匹配当前 BGM 的 AudioSource。
        /// </summary>
        public int BgmHandleId
        {
            get { return bgmHandleId; }
        }

        /// <summary>
        /// BGM 音量（0~1）。
        /// </summary>
        public float BgmVolume { get; set; } = 1f;

        /// <summary>
        /// SFX 音量（0~1）。
        /// </summary>
        public float SfxVolume { get; set; } = 1f;

        /// <summary>
        /// UI 音效音量（0~1）。
        /// </summary>
        public float UIVolume { get; set; } = 1f;

        /// <summary>
        /// 全局静音开关。设为 true 时所有音频静音，false 恢复。
        /// </summary>
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

        /// <summary>
        /// 句柄回调信息。
        /// </summary>
        internal struct HandleCallbackInfo
        {
            /// <summary>
            /// 播放完毕后延迟多少秒再触发回调。
            /// </summary>
            public float DelaySeconds;

            /// <summary>
            /// 延迟后执行的回调委托。
            /// </summary>
            public Action Callback;

            /// <summary>
            /// 是否为 BGM 句柄（false 则为 SFX 句柄）。
            /// </summary>
            public bool IsBgm;
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

            // 取消旧 BGM 回调
            CancelHandle(bgmHandleId);

            // 停止旧 BGM
            if (currentBgmAssetName != null)
            {
                StopBgmInternal();
            }

            object audioAsset = LoadAudioAsset(assetName);
            currentBgmAssetName = assetName;
            currentBgmVolume = volume;
            bgmPaused = false;

            int handleId = CreateHandle(completeDelaySeconds, onComplete, true);
            bgmHandleId = handleId;

            PlayBgmNative(audioAsset, GetFinalVolume(volume, BgmVolume), loop, fadeInSeconds);
            StartCallbackCoroutine(handleId);
            return new AudioHandle(handleId, this);
        }

        /// <inheritdoc/>
        public void StopBgm(float fadeOutSeconds = 0f)
        {
            CancelHandle(bgmHandleId);
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
            PauseBgmNative();
        }

        /// <inheritdoc/>
        public void ResumeBgm()
        {
            if (currentBgmAssetName == null || !bgmPaused)
            {
                return;
            }

            bgmPaused = false;
            ResumeBgmNative();
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
            int handleId = CreateHandle(completeDelaySeconds, onComplete, false);

            PlaySfxNative(handleId, audioAsset, GetFinalVolume(volume, SfxVolume));
            StartCallbackCoroutine(handleId);
            return new AudioHandle(handleId, this);
        }

        /// <inheritdoc/>
        public void StopAllSfx()
        {
            // 取消所有非 BGM 句柄的回调
            CancelAllNonBgmHandles();
            StopAllSfxNative();
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
            PlayUINative(audioAsset, GetFinalVolume(volume, UIVolume));
        }

        /// <inheritdoc/>
        public void StopAll()
        {
            CancelAllHandles();
            StopBgmInternal();
            StopAllSfxNative();
        }

        // ====== 句柄管理 ======

        /// <summary>
        /// 创建句柄并记录回调信息。
        /// </summary>
        /// <param name="delaySeconds">播放完毕后延迟多少秒触发回调。</param>
        /// <param name="callback">回调委托，null 表示不需要回调。</param>
        /// <param name="isBgm">是否为 BGM 句柄。</param>
        /// <returns>句柄 ID。</returns>
        private int CreateHandle(float delaySeconds, Action callback, bool isBgm)
        {
            int id = nextHandleId++;

            if (callback != null)
            {
                activeHandles.Add(id, new HandleCallbackInfo
                {
                    DelaySeconds = delaySeconds,
                    Callback = callback,
                    IsBgm = isBgm
                });
            }

            return id;
        }

        /// <summary>
        /// 内部停止句柄（由 AudioHandle.Stop() 触发）。
        /// 同时取消回调并停止对应 AudioSource。
        /// </summary>
        /// <param name="handleId">句柄 ID。</param>
        internal void StopHandleInternal(int handleId)
        {
            CancelHandle(handleId);

            if (handleId == bgmHandleId)
            {
                StopBgmInternal();
            }
            else
            {
                StopSfxByHandleIdNative(handleId);
            }
        }

        /// <summary>
        /// 取消句柄的回调和协程（不停止 AudioSource）。
        /// </summary>
        /// <param name="handleId">句柄 ID。</param>
        private void CancelHandle(int handleId)
        {
            if (activeHandles.TryGetValue(handleId, out HandleCallbackInfo info))
            {
                CancelCallbackCoroutine(handleId);
                activeHandles.Remove(handleId);
            }

            if (handleId == bgmHandleId)
            {
                bgmHandleId = 0;
            }
        }

        /// <summary>
        /// 取消所有非 BGM 句柄（StopAllSfx 时使用）。
        /// </summary>
        private void CancelAllNonBgmHandles()
        {
            List<int> toRemove = new List<int>();
            foreach (KeyValuePair<int, HandleCallbackInfo> kv in activeHandles)
            {
                if (!kv.Value.IsBgm)
                {
                    CancelCallbackCoroutine(kv.Key);
                    toRemove.Add(kv.Key);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                activeHandles.Remove(toRemove[i]);
            }
        }

        /// <summary>
        /// 取消所有句柄（StopAll 时使用）。
        /// </summary>
        private void CancelAllHandles()
        {
            foreach (KeyValuePair<int, HandleCallbackInfo> kv in activeHandles)
            {
                CancelCallbackCoroutine(kv.Key);
            }

            activeHandles.Clear();
            bgmHandleId = 0;
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

            object audioAsset = audioHelper.LoadAudioAsset(assetName);
            loadedAudioAssets.Add(assetName, audioAsset);
            return audioAsset;
        }

        /// <summary>
        /// 应用静音状态到当前 BGM（静音时直接设音量为 0，取消静音时恢复）。
        /// </summary>
        private void ApplyMuteState()
        {
            if (currentBgmAssetName != null)
            {
                SetBgmVolumeNative(muted ? 0f : GetFinalVolume(currentBgmVolume, BgmVolume));
            }
        }

        /// <summary>
        /// 内部停止 BGM（停止 AudioSource + 清除资源路径 + 重置暂停状态）。
        /// </summary>
        /// <param name="fadeOutSeconds">淡出时长（秒），0 为立即停止。</param>
        private void StopBgmInternal(float fadeOutSeconds = 0f)
        {
            StopBgmNative(fadeOutSeconds);
            currentBgmAssetName = null;
            bgmPaused = false;
        }

        // ====== Runtime 层钩子（由 AudioComponent 绑定，连接 Library 层逻辑和 Unity AudioSource） ======

        /// <summary>
        /// BGM 播放钩子。参数：AudioClip, 音量, 循环, 淡入秒数。
        /// </summary>
        public Action<object, float, bool, float> OnPlayBgm;

        /// <summary>
        /// BGM 停止钩子。参数：淡出秒数。
        /// </summary>
        public Action<float> OnStopBgm;

        /// <summary>
        /// BGM 暂停钩子。
        /// </summary>
        public Action OnPauseNative;

        /// <summary>
        /// BGM 恢复钩子。
        /// </summary>
        public Action OnResumeNative;

        /// <summary>
        /// SFX 播放钩子。参数：句柄 ID, AudioClip, 音量。
        /// </summary>
        public Action<int, object, float> OnPlaySfx;

        /// <summary>
        /// 停止所有 SFX 钩子。
        /// </summary>
        public Action OnStopAllSfx;

        /// <summary>
        /// UI 音效播放钩子。参数：AudioClip, 音量。
        /// </summary>
        public Action<object, float> OnPlayUI;

        /// <summary>
        /// 设置 BGM AudioSource 音量钩子。参数：音量值。
        /// </summary>
        public Action<float> OnSetBgmVolume;

        /// <summary>
        /// 启动回调协程钩子。参数：句柄 ID, 延迟秒数, 回调委托。
        /// </summary>
        public Action<int, float, Action> OnStartCallback;

        /// <summary>
        /// 取消回调协程钩子。参数：句柄 ID。
        /// </summary>
        public Action<int> OnCancelCallback;

        /// <summary>
        /// 按句柄 ID 停止 SFX 钩子。参数：句柄 ID。
        /// </summary>
        public Action<int> OnStopSfxById;

        // ====== 钩子调用方法 ======

        /// <summary>
        /// 通过钩子触发 Runtime 层 BGM 播放。
        /// </summary>
        /// <param name="audioAsset">AudioClip 对象。</param>
        /// <param name="volume">最终音量。</param>
        /// <param name="loop">是否循环。</param>
        /// <param name="fadeIn">淡入时长。</param>
        private void PlayBgmNative(object audioAsset, float volume, bool loop, float fadeIn)
        {
            OnPlayBgm?.Invoke(audioAsset, volume, loop, fadeIn);
        }

        /// <summary>
        /// 通过钩子触发 Runtime 层 BGM 停止。
        /// </summary>
        /// <param name="fadeOut">淡出时长。</param>
        private void StopBgmNative(float fadeOut)
        {
            OnStopBgm?.Invoke(fadeOut);
        }

        /// <summary>
        /// 通过钩子触发 Runtime 层 BGM 暂停。
        /// </summary>
        private void PauseBgmNative()
        {
            OnPauseNative?.Invoke();
        }

        /// <summary>
        /// 通过钩子触发 Runtime 层 BGM 恢复。
        /// </summary>
        private void ResumeBgmNative()
        {
            OnResumeNative?.Invoke();
        }

        /// <summary>
        /// 通过钩子触发 Runtime 层 SFX 播放。
        /// </summary>
        /// <param name="handleId">句柄 ID（用于后续单独停止）。</param>
        /// <param name="audioAsset">AudioClip 对象。</param>
        /// <param name="volume">最终音量。</param>
        private void PlaySfxNative(int handleId, object audioAsset, float volume)
        {
            OnPlaySfx?.Invoke(handleId, audioAsset, volume);
        }

        /// <summary>
        /// 通过钩子触发 Runtime 层停止所有 SFX。
        /// </summary>
        private void StopAllSfxNative()
        {
            OnStopAllSfx?.Invoke();
        }

        /// <summary>
        /// 通过钩子触发 Runtime 层按句柄 ID 停止指定 SFX。
        /// </summary>
        /// <param name="handleId">句柄 ID。</param>
        private void StopSfxByHandleIdNative(int handleId)
        {
            OnStopSfxById?.Invoke(handleId);
        }

        /// <summary>
        /// 通过钩子触发 Runtime 层 UI 音效播放。
        /// </summary>
        /// <param name="audioAsset">AudioClip 对象。</param>
        /// <param name="volume">最终音量。</param>
        private void PlayUINative(object audioAsset, float volume)
        {
            OnPlayUI?.Invoke(audioAsset, volume);
        }

        /// <summary>
        /// 通过钩子设置 BGM AudioSource 音量（静音切换时使用）。
        /// </summary>
        /// <param name="volume">音量值。</param>
        private void SetBgmVolumeNative(float volume)
        {
            OnSetBgmVolume?.Invoke(volume);
        }

        /// <summary>
        /// 通过钩子启动回调协程（等音频播完 → 延迟 → 执行回调）。
        /// </summary>
        /// <param name="handleId">句柄 ID。</param>
        private void StartCallbackCoroutine(int handleId)
        {
            if (activeHandles.TryGetValue(handleId, out HandleCallbackInfo info) && info.Callback != null)
            {
                OnStartCallback?.Invoke(handleId, info.DelaySeconds, info.Callback);
            }
        }

        /// <summary>
        /// 通过钩子取消回调协程。
        /// </summary>
        /// <param name="handleId">句柄 ID。</param>
        private void CancelCallbackCoroutine(int handleId)
        {
            OnCancelCallback?.Invoke(handleId);
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
            if (audioHelper != null)
            {
                foreach (KeyValuePair<string, object> kv in loadedAudioAssets)
                {
                    audioHelper.ReleaseAudioAsset(kv.Value);
                }
            }

            loadedAudioAssets.Clear();
            currentBgmAssetName = null;
        }
    }
}
