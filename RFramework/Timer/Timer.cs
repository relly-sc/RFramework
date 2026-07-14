using System;

namespace RFramework
{
    /// <summary>
    /// 计时器句柄。基于 (延迟, 间隔, 时长, 次数) 四参数模型，
    /// 覆盖一次性/重复/限时/限次等所有计时场景。
    /// 计时器由 TimerModule 驱动 Update，用户通过句柄控制暂停/恢复/取消/重置。
    /// </summary>
    public sealed class Timer
    {
        private readonly float intervalTime;
        private readonly float durationTime;
        private readonly long maxTriggerCount;
        private readonly Action callback;

        // 运行时状态（Reset 时归零）
        private float delayTimer;
        private float durationTimer;
        private float intervalTimer;
        private long triggerCount;

        /// <summary>
        /// 延迟时间（首次触发前的等待秒数）。
        /// </summary>
        public float DelayTime { get; private set; }

        /// <summary>
        /// 计时器是否已结束（自然到期或被取消）。
        /// </summary>
        public bool IsOver { get; private set; }

        /// <summary>
        /// 计时器是否已暂停。
        /// </summary>
        public bool IsPause { get; private set; }

        /// <summary>
        /// 是否忽略 Time.timeScale（使用 realtimeSinceStartup 计时）。
        /// </summary>
        internal bool IgnorTimescale { get; private set; }

        /// <summary>
        /// 延迟剩余时间（已结束时返回 0）。
        /// </summary>
        public float Remaining
        {
            get
            {
                if (IsOver)
                {
                    return 0f;
                }

                return Math.Max(0f, DelayTime - delayTimer);
            }
        }

        /// <summary>
        /// 创建计时器实例（内部构造，由 TimerModule 注册）。
        /// </summary>
        /// <param name="delay">延迟时间（秒），> 0 表示延迟后触发，= 0 表示立即触发。</param>
        /// <param name="interval">间隔时间（秒），> 0 表示重复，-1 表示只触发一次。</param>
        /// <param name="duration">总运行时长（秒），-1 表示无限。</param>
        /// <param name="maxTriggerCount">最大触发次数，-1 表示无限，1 表示单次。</param>
        /// <param name="callback">触发回调。</param>
        /// <param name="ignorTimescale">是否忽略 Time.timeScale。</param>
        internal Timer(float delay, float interval, float duration, long maxTriggerCount, Action callback, bool ignorTimescale)
        {
            DelayTime = delay;
            intervalTime = interval;
            durationTime = duration;
            this.maxTriggerCount = maxTriggerCount;
            this.callback = callback;
            IgnorTimescale = ignorTimescale;
        }

        // ====== 静态工厂方法 ======

        /// <summary>
        /// 创建一次性延迟计时器（延迟后触发一次即结束）。
        /// </summary>
        public static Timer CreateOnce(float delay, Action callback, bool ignorTimescale = true)
        {
            return new Timer(delay, -1f, -1f, 1, callback, ignorTimescale);
        }

        /// <summary>
        /// 创建永久重复计时器（延迟后每隔 interval 秒触发，无限循环）。
        /// </summary>
        public static Timer CreateRepeat(float delay, float interval, Action callback, bool ignorTimescale = true)
        {
            return new Timer(delay, interval, -1f, -1, callback, ignorTimescale);
        }

        /// <summary>
        /// 创建限时重复计时器（延迟后每隔 interval 秒触发，持续 duration 秒后结束）。
        /// </summary>
        public static Timer CreateRepeat(float delay, float interval, float duration, Action callback, bool ignorTimescale = true)
        {
            return new Timer(delay, interval, duration, -1, callback, ignorTimescale);
        }

        /// <summary>
        /// 创建限次重复计时器（延迟后每隔 interval 秒触发，最多触发 maxTriggerCount 次后结束）。
        /// </summary>
        public static Timer CreateRepeat(float delay, float interval, long maxTriggerCount, Action callback, bool ignorTimescale = true)
        {
            return new Timer(delay, interval, -1f, maxTriggerCount, callback, ignorTimescale);
        }

        /// <summary>
        /// 创建限时持续计时器（在 duration 秒内持续触发，适用于需要每帧回调的场景）。
        /// </summary>
        public static Timer CreateDuration(float delay, float duration, Action callback, bool ignorTimescale = true)
        {
            return new Timer(delay, -1f, duration, -1, callback, ignorTimescale);
        }

        /// <summary>
        /// 创建永久持续计时器（延迟后每帧触发，永不结束，需手动 Cancel）。
        /// </summary>
        public static Timer CreateForever(float delay, Action callback, bool ignorTimescale = true)
        {
            return new Timer(delay, -1f, -1f, -1, callback, ignorTimescale);
        }

        // ====== 控制方法 ======

        /// <summary>
        /// 暂停计时器。
        /// </summary>
        public void Pause()
        {
            IsPause = true;
        }

        /// <summary>
        /// 恢复计时器。
        /// </summary>
        public void Resume()
        {
            IsPause = false;
        }

        /// <summary>
        /// 取消计时器（标记为已结束，下一帧由 TimerModule 清理）。
        /// </summary>
        public void Cancel()
        {
            IsOver = true;
        }

        /// <summary>
        /// 重置计时器到初始状态，可重新使用。
        /// </summary>
        public void Reset()
        {
            delayTimer = 0f;
            durationTimer = 0f;
            intervalTimer = 0f;
            triggerCount = 0;
            IsOver = false;
            IsPause = false;
        }

        // ====== 内部方法（TimerModule 调用） ======

        /// <summary>
        /// 更新计时器，返回本帧是否触发回调。
        /// </summary>
        /// <param name="deltaTime">时间增量（由 TimerModule 根据 ignorTimescale 选择）。</param>
        /// <returns>本帧是否应触发回调。</returns>
        internal bool Update(float deltaTime)
        {
            if (IsOver || IsPause)
            {
                return false;
            }

            // 延迟阶段：累计等待直到超过 DelayTime
            delayTimer += deltaTime;
            if (delayTimer < DelayTime)
            {
                return false;
            }

            // 间隔和时长计时
            if (intervalTime > 0f)
            {
                intervalTimer += deltaTime;
            }

            if (durationTime > 0f)
            {
                durationTimer += deltaTime;
            }

            // 间隔检查：未到间隔时间则等待
            if (intervalTime > 0f)
            {
                if (intervalTimer < intervalTime)
                {
                    return false;
                }

                intervalTimer = 0f;
            }

            // 时长结束检查
            if (durationTime > 0f)
            {
                if (durationTimer >= durationTime)
                {
                    IsOver = true;
                }
            }

            // 次数结束检查
            if (maxTriggerCount > 0)
            {
                triggerCount++;
                if (triggerCount >= maxTriggerCount)
                {
                    IsOver = true;
                }
            }

            return true;
        }

        /// <summary>
        /// 触发回调（由 TimerModule 在 Update 返回 true 后调用）。
        /// </summary>
        internal void InvokeCallback()
        {
            callback?.Invoke();
        }
    }
}
