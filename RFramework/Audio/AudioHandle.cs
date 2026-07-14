using System;

namespace RFramework
{
    /// <summary>
    /// 音效句柄（轻量值类型，通过内部 ID 追踪单个播放中的音效）。
    /// 用于单独停止某个音效，或者取消其回调。
    /// </summary>
    public readonly struct AudioHandle
    {
        /// <summary>
        /// 内部追踪 ID。0 表示无效句柄。
        /// </summary>
        internal readonly int Id;

        /// <summary>
        /// 所属的 AudioModule 引用，用于调用内部停止方法。
        /// </summary>
        internal readonly AudioModule Module;

        /// <summary>
        /// 内部构造函数（由 AudioModule 调用）。
        /// </summary>
        /// <param name="id">句柄追踪 ID。</param>
        /// <param name="module">所属音频模块。</param>
        internal AudioHandle(int id, AudioModule module)
        {
            Id = id;
            Module = module;
        }

        /// <summary>
        /// 是否有效。
        /// </summary>
        public bool IsValid => Id > 0 && Module != null;

        /// <summary>
        /// 停止该音效，并取消其回调。
        /// </summary>
        public void Stop()
        {
            if (IsValid)
            {
                Module.StopHandleInternal(Id);
            }
        }
    }
}
