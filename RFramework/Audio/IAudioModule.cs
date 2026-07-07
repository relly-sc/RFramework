using System;
using RFramework.Event;
using RFramework.Pool;
using RFramework.Resource;

namespace RFramework.Audio
{
    /// <summary>
    /// 音频模块接口。提供 BGM/SFX/UI 三组内置音轨，
    /// 音量独立控制，BGM 淡入淡出，SFX AudioSource 池并发播放。
    /// </summary>
    public interface IAudioModule
    {
        /// <summary>
        /// BGM 音量（0~1）。
        /// </summary>
        float BgmVolume { get; set; }

        /// <summary>
        /// SFX 音量（0~1）。
        /// </summary>
        float SfxVolume { get; set; }

        /// <summary>
        /// UI 音效音量（0~1）。
        /// </summary>
        float UIVolume { get; set; }

        /// <summary>
        /// 全局静音。
        /// </summary>
        bool Muted { get; set; }

        /// <summary>
        /// 设置依赖模块引用。
        /// </summary>
        /// <param name="resourceModule">资源模块，用于加载音频资源。</param>
        /// <param name="eventModule">事件模块（预留）。</param>
        /// <param name="poolModule">对象池模块（预留）。</param>
        void SetDependencies(IResourceModule resourceModule, IEventModule eventModule, IPoolModule poolModule);

        /// <summary>
        /// 设置音频辅助器。
        /// </summary>
        /// <param name="helper">音频辅助器实例。</param>
        void SetHelper(IAudioHelper helper);

        /// <summary>
        /// 播放 BGM。返回句柄，可通过句柄停止或取消回调。
        /// </summary>
        /// <param name="assetName">音频资源路径。</param>
        /// <param name="volume">音量倍率（0~1）。</param>
        /// <param name="loop">是否循环。</param>
        /// <param name="fadeInSeconds">淡入时长（秒），0 为立即。</param>
        /// <param name="completeDelaySeconds">播放完毕后延迟多少秒再触发回调。</param>
        /// <param name="onComplete">播放完毕 + 延迟后的回调。Stop 后不会被触发。</param>
        AudioHandle PlayBgm(string assetName, float volume = 1f, bool loop = true,
            float fadeInSeconds = 0f, float completeDelaySeconds = 0f, Action onComplete = null);

        /// <summary>
        /// 停止 BGM。
        /// </summary>
        /// <param name="fadeOutSeconds">淡出时长（秒），0 为立即。</param>
        void StopBgm(float fadeOutSeconds = 0f);

        /// <summary>
        /// 暂停 BGM。
        /// </summary>
        void PauseBgm();

        /// <summary>
        /// 恢复 BGM。
        /// </summary>
        void ResumeBgm();

        /// <summary>
        /// 播放 SFX。返回句柄，可通过句柄单独停止或取消回调。
        /// </summary>
        /// <param name="assetName">音频资源路径。</param>
        /// <param name="volume">音量倍率（0~1）。</param>
        /// <param name="completeDelaySeconds">播放完毕后延迟多少秒再触发回调。</param>
        /// <param name="onComplete">播放完毕 + 延迟后的回调。Stop 后不会被触发。</param>
        AudioHandle PlaySfx(string assetName, float volume = 1f,
            float completeDelaySeconds = 0f, Action onComplete = null);

        /// <summary>
        /// 停止所有 SFX（同时取消所有 SFX 的回调）。
        /// </summary>
        void StopAllSfx();

        /// <summary>
        /// 播放 UI 音效。
        /// </summary>
        /// <param name="assetName">音频资源路径。</param>
        /// <param name="volume">音量倍率（0~1）。</param>
        void PlayUI(string assetName, float volume = 1f);

        /// <summary>
        /// 停止所有音频。
        /// </summary>
        void StopAll();
    }
}
