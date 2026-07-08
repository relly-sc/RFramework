using System;

namespace RFramework.Audio
{
    /// <summary>
    /// 音频辅助器接口，封装引擎特定的音频加载、播放与回调操作。
    /// AudioModule 不直接依赖 Unity AudioSource，全部通过此接口委托给 Runtime 层实现。
    /// </summary>
    public interface IAudioHelper
    {
        /// <summary>
        /// 加载音频资源。
        /// </summary>
        /// <param name="assetName">音频资源路径。</param>
        /// <returns>加载的音频资源（Unity 层为 AudioClip）。</returns>
        object LoadAudioAsset(string assetName);

        /// <summary>
        /// 释放音频资源。
        /// </summary>
        /// <param name="audioAsset">音频资源对象。</param>
        void ReleaseAudioAsset(object audioAsset);

        /// <summary>
        /// 播放 BGM。Helper 负责 AudioSource 设置、淡入和完成回调协程。
        /// </summary>
        /// <param name="audioAsset">音频资源（AudioClip）。</param>
        /// <param name="volume">最终音量（已乘类别音量 + 静音判定）。</param>
        /// <param name="loop">是否循环。</param>
        /// <param name="fadeInSeconds">淡入时长（秒），0 为立即。</param>
        /// <param name="completeDelaySeconds">播放完毕后延迟多少秒再触发回调。</param>
        /// <param name="onComplete">播放完毕 + 延迟后的回调。Stop 后不会被触发。</param>
        void PlayBgm(object audioAsset, float volume, bool loop, float fadeInSeconds,
            float completeDelaySeconds, Action onComplete);

        /// <summary>
        /// 停止 BGM，同时取消当前 BGM 的完成回调协程。
        /// </summary>
        /// <param name="fadeOutSeconds">淡出时长（秒），0 为立即停止。</param>
        void StopBgm(float fadeOutSeconds);

        /// <summary>
        /// 暂停 BGM。
        /// </summary>
        void PauseBgm();

        /// <summary>
        /// 恢复 BGM。
        /// </summary>
        void ResumeBgm();

        /// <summary>
        /// 直接设置 BGM AudioSource 音量（静音切换时使用）。
        /// </summary>
        /// <param name="volume">音量值。</param>
        void SetBgmVolume(float volume);

        /// <summary>
        /// 播放 SFX。Helper 负责分配 AudioSource 和完成回调协程。
        /// </summary>
        /// <param name="handleId">模块分配的句柄 ID，用于后续单独停止。</param>
        /// <param name="audioAsset">音频资源（AudioClip）。</param>
        /// <param name="volume">最终音量。</param>
        /// <param name="completeDelaySeconds">播放完毕后延迟多少秒再触发回调。</param>
        /// <param name="onComplete">播放完毕 + 延迟后的回调。Stop 后不会被触发。</param>
        void PlaySfx(int handleId, object audioAsset, float volume,
            float completeDelaySeconds, Action onComplete);

        /// <summary>
        /// 按句柄 ID 停止指定 SFX，并取消其完成回调协程。
        /// </summary>
        /// <param name="handleId">句柄 ID。</param>
        void StopSfx(int handleId);

        /// <summary>
        /// 停止所有 SFX，并取消所有完成回调协程。
        /// </summary>
        void StopAllSfx();

        /// <summary>
        /// 播放 UI 音效（无回调，即时播放）。
        /// </summary>
        /// <param name="audioAsset">音频资源（AudioClip）。</param>
        /// <param name="volume">最终音量。</param>
        void PlayUI(object audioAsset, float volume);
    }
}
