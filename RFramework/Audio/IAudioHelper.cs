namespace RFramework.Audio
{
    /// <summary>
    /// 音频辅助器接口，封装引擎特定的音频加载与释放操作。
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
    }
}
