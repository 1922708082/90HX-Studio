using System;
using System.Threading;

namespace CMP90HXToolbox
{
    /// <summary>
    /// 三档功耗预设。
    ///
    /// 说明：Windows 下 nvidia-smi 只能设功耗墙（-pl）与锁频区间（-lgc），
    /// 没有真正的频率偏移（offset）超频接口（那需要 NVAPI 的 Pstates20 写入）。
    /// 所以「满血狂暴」的自动超频 = 功耗墙抬到 320W + 解除锁频 + 把频率上限
    /// 抬到本卡支持的最高值，让 Boost 自己跑满。这是本平台能做到的超频形态。
    ///
    /// 三档：
    ///   sleep    严格限功耗到十几瓦 + 锁最低核心频率（P8），几乎不发热
    ///   adaptive 功耗墙 250W；空闲超过 N 秒自动压到十几瓦，有负载立刻恢复
    ///   turbo    功耗墙 320W + 解除锁频 + 频率上限放开到卡的最大值
    /// </summary>
    internal static class PowerPresets
    {
        public const int SleepWatts = 0;          // 功耗墙直接压到 0W（本卡 min_limit = 0）
        public const int SleepFallbackWatts = 12; // 若驱动拒绝 0W 则回退到 12W
        public const int AdaptiveWatts = 250;
        public const int TurboWatts = 320;
        public const int SleepCoreClock = 210;
        public const int TurboClockCeiling = 2100;

        private static string _current = "none";
        private static Timer _watch;
        private static DateTime _lastBusy = DateTime.Now;
        private static bool _throttled;
        private static int _idleSeconds = 8;
        private static string _lastSwitch = "";
        private static readonly object _gate = new object();

        public static string Current { get { lock (_gate) return _current; } }
        public static bool WatchdogRunning { get { lock (_gate) return _watch != null; } }
        public static bool Throttled { get { lock (_gate) return _throttled; } }
        public static string LastSwitch { get { lock (_gate) return _lastSwitch; } }

        /// <summary>应用某一档。返回 false 表示关键命令被驱动拒绝。</summary>
        public static bool Apply(string preset, int idleSeconds, Action<string, bool> log)
        {
            if (string.IsNullOrEmpty(preset)) preset = "adaptive";
            preset = preset.ToLowerInvariant();

            StopWatchdog();
            bool ok = true;

            if (preset == "sleep")
            {
                ApplySleepLimit(log);
                TryPstate(8, log);
            }
            else if (preset == "turbo")
            {
                TryPstate(0, log);
                ok &= SetPowerLimit(TurboWatts, log);
                // 放开频率区间：上限拉到本卡最高，等同「自动超频」
                ok &= Smi("-lgc 0," + TurboClockCeiling, log, "频率上限放至 " + TurboClockCeiling + "MHz");
            }
            else
            {
                preset = "adaptive";
                TryPstate(0, log);
                Smi("-rgc", log, "解除核心锁频");
                ok &= SetPowerLimit(AdaptiveWatts, log);
                StartWatchdog(idleSeconds, log);
            }

            lock (_gate)
            {
                _current = preset;
                _lastSwitch = DateTime.Now.ToString("HH:mm:ss");
                _throttled = false;
            }
            if (log != null) log("已切换到预设：" + preset, false);
            return ok;
        }

        /// <summary>彻底解除所有限制，回到驱动默认功耗墙。</summary>
        public static void StopAll(Action<string, bool> log)
        {
            StopWatchdog();
            TryPstate(0, log);
            Smi("-rac", log, "重置应用时钟");
            Smi("-rgc", log, "解除核心锁频");
            Smi("-rmc", log, "恢复显存调度");
            int def = 250;
            try
            {
                NativeCore.GpuPowerRange r = NativeCore.QueryGpuPowerRange();
                if (r != null && r.DefaultLimit > 0) def = (int)Math.Round(r.DefaultLimit);
            }
            catch { }
            SetPowerLimit(def, log);
            lock (_gate)
            {
                _current = "none";
                _throttled = false;
                _lastSwitch = DateTime.Now.ToString("HH:mm:ss");
            }
            if (log != null) log("已关闭省电守护，恢复默认功耗墙 " + def + "W", false);
        }

        // ---------- 内部 ----------

        private static void ApplySleepLimit(Action<string, bool> log)
        {
            // 先试 0W：本卡 Min Power Limit = 0，理论上允许。
            // 驱动若拒绝（部分卡会报 out of range），自动回退 12W。
            if (!SetPowerLimit(SleepWatts, log))
            {
                if (log != null) log("驱动拒绝 0W，回退到 " + SleepFallbackWatts + "W", true);
                SetPowerLimit(SleepFallbackWatts, log);
            }
            LockCore(SleepCoreClock, log);
        }

        private static bool SetPowerLimit(int watts, Action<string, bool> log)
        {
            return Smi("-pl " + watts, log, "功耗墙 " + watts + "W");
        }

        private static bool LockCore(int mhz, Action<string, bool> log)
        {
            return Smi("-lgc " + mhz + "," + mhz, log, "核心锁频 " + mhz + "MHz");
        }

        private static bool Smi(string args, Action<string, bool> log, string desc)
        {
            try
            {
                string outp = NativeCore.RunNvidiaSmiCommand(args);
                bool ok = outp != null && outp.IndexOf("error", StringComparison.OrdinalIgnoreCase) < 0
                          && outp.IndexOf("not supported", StringComparison.OrdinalIgnoreCase) < 0
                          && outp.IndexOf("insufficient", StringComparison.OrdinalIgnoreCase) < 0;
                if (log != null) log((ok ? "已设置 " : "设置失败 ") + desc, !ok);
                return ok;
            }
            catch (Exception ex)
            {
                if (log != null) log(desc + " 执行异常: " + ex.Message, true);
                return false;
            }
        }

        private static void TryPstate(int ceiling, Action<string, bool> log)
        {
            try { NativeCore.SetPstateCeiling(ceiling, log); }
            catch { }
        }

        private static void StartWatchdog(int idleSeconds, Action<string, bool> log)
        {
            lock (_gate)
            {
                _idleSeconds = idleSeconds < 5 ? 5 : idleSeconds;
                _lastBusy = DateTime.Now;
                _watch = new Timer(WatchTick, log, 2000, 2000);
            }
            if (log != null) log("省电守护已启动：空闲 " + _idleSeconds + " 秒压到 " + SleepWatts + "W，负载即恢复", false);
        }

        private static void StopWatchdog()
        {
            lock (_gate)
            {
                if (_watch != null)
                {
                    try { _watch.Dispose(); } catch { }
                    _watch = null;
                }
            }
        }

        private static void WatchTick(object state)
        {
            Action<string, bool> log = state as Action<string, bool>;
            try
            {
                NativeCore.GpuSensorSnapshot s = NativeCore.QueryGpuSensors();
                if (s == null) return;

                bool busy = s.GpuUtilPercent >= 15 || s.PowerDraw >= 60;
                if (busy) _lastBusy = DateTime.Now;

                bool shouldThrottle = !busy && (DateTime.Now - _lastBusy).TotalSeconds >= _idleSeconds;

                if (shouldThrottle && !_throttled)
                {
                    ApplySleepLimit(log);
                    TryPstate(8, log);
                    lock (_gate) { _throttled = true; _lastSwitch = DateTime.Now.ToString("HH:mm:ss"); }
                    if (log != null) log("空闲已压到 " + SleepWatts + "W", false);
                }
                else if (!shouldThrottle && _throttled)
                {
                    TryPstate(0, log);
                    Smi("-rgc", log, "解除锁频");
                    SetPowerLimit(AdaptiveWatts, log);
                    lock (_gate) { _throttled = false; _lastSwitch = DateTime.Now.ToString("HH:mm:ss"); }
                    if (log != null) log("检测到负载，恢复 " + AdaptiveWatts + "W", false);
                }
            }
            catch { }
        }
    }
}
