using System;
using System.Threading;

namespace CMP90HXToolbox
{
    /// <summary>
    /// 三档功耗预设。
    ///
    /// 实测结论（CMP 90HX / 驱动 616.92）：
    ///   nvidia-smi -pl 任何值都返回 "Changing power management limit is not
    ///   supported in current scope" —— 该卡 BIOS 把功耗墙锁死了，管理员也没用。
    ///   可用的是：-lgc 锁频、-rgc 解锁、NVAPI P-State 上限。
    ///
    /// 因此三档改为「以频率和 P-State 为主」，功耗墙只在驱动允许时才用，
    /// 不允许就完全跳过，绝不因此报错。
    ///
    ///   sleep    锁 210MHz + 压 P8        → 实测十几瓦
    ///   adaptive 解锁频率 + P-State 自动；空闲 N 秒锁 210/P8，有负载立刻解锁
    ///   turbo    频率区间放开到 0~2100MHz + 解除 P-State 上限
    /// </summary>
    internal static class PowerPresets
    {
        public const int SleepCoreClock = 210;
        public const int TurboClockCeiling = 2100;
        public const int AdaptiveWatts = 250;   // 仅用于展示，实际受硬件限制
        /// <summary>满血档的功耗墙。本卡实测物理峰值就在 290~300W，
        /// 写 320W（128%）虽然能生效，但实际拉不到那么多，按 300W 更贴合。</summary>
        public const int TurboWatts = 300;
        /// <summary>
        /// 展示用功耗。**禁止再出现 0**：
        /// 本卡 min_limit 报 0 只是占位值，真写进去会把卡永久钳在 0W 预算，
        /// 之后 -pl 一律返回 not supported，只能重启或走 NVAPI 才能恢复。
        /// </summary>
        public const int SleepWatts = 12;
        public const int SleepFallbackWatts = 12;

        private static string _current = "none";
        private static Timer _watch;
        private static DateTime _lastBusy = DateTime.Now;
        private static bool _throttled;
        private static int _idleSeconds = 8;
        private static string _lastSwitch = "";
        private static readonly object _gate = new object();

        /// <summary>功耗墙是否可写。首次探测后缓存，避免每次都白试一遍。</summary>
        private static bool? _powerLimitWritable;

        public static string LastError = "";

        public static string Current { get { lock (_gate) return _current; } }
        public static bool WatchdogRunning { get { lock (_gate) return _watch != null; } }
        public static bool Throttled { get { lock (_gate) return _throttled; } }
        public static string LastSwitch { get { lock (_gate) return _lastSwitch; } }
        public static bool PowerLimitWritable { get { return ProbePowerLimit(); } }

        /// <summary>应用某一档。</summary>
        public static bool Apply(string preset, int idleSeconds, Action<string, bool> log)
        {
            if (string.IsNullOrEmpty(preset)) preset = "adaptive";
            preset = preset.ToLowerInvariant();

            StopWatchdog();
            bool ok = true;
            LastError = "";

            if (preset == "sleep")
            {
                ok = LockCore(SleepCoreClock, log);
                TryPstate(8, log);
                TryRelaxPowerLimit(log);
            }
            else if (preset == "turbo")
            {
                TryPstate(0, log);
                // 关键：必须用 -rgc 解除锁频。
                // 之前用 -lgc 0,2100 把最小值写成 0，参数非法，驱动保留了旧的 210 锁，
                // 结果功耗墙到了 320W 但频率仍卡在 210MHz。
                ok = Smi("-rgc", log, "解除核心锁频");
                TryRaisePowerLimit(TurboWatts, log);
            }
            else
            {
                preset = "adaptive";
                TryPstate(0, log);
                Smi("-rgc", log, "解除核心锁频");
                // 智能动态是「常态全速」，功耗墙要回到默认，不能沿用静音档的 12W
                TryRestoreDefaultPowerLimit(log);
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

        /// <summary>彻底解除所有限制。</summary>
        public static void StopAll(Action<string, bool> log)
        {
            StopWatchdog();
            TryPstate(0, log);
            Smi("-rgc", log, "解除核心锁频");
            Smi("-rmc", log, "恢复显存调度");
            TryRelaxPowerLimit(log);
            lock (_gate)
            {
                _current = "none";
                _throttled = false;
                _lastSwitch = DateTime.Now.ToString("HH:mm:ss");
            }
            if (log != null) log("已关闭省电守护，频率与功耗恢复驱动默认", false);
        }

        /// <summary>
        /// 功耗墙是否可写。
        /// 只做「读」判断，绝不为了探测而写值——早期版本拿 -pl 250 当探针，
        /// 再叠加 0W 的写入，会把卡钳死在 0W 预算里且无法恢复。
        /// 这里改为：第一次真实写入失败后再缓存结果。
        /// </summary>
        public static bool ProbePowerLimit()
        {
            if (_powerLimitWritable.HasValue) return _powerLimitWritable.Value;
            return true; // 尚未验证过，交给第一次真实调用去发现
        }

        private static void MarkPowerLimitUnwritable(string reason)
        {
            _powerLimitWritable = false;
        }

        private static void TryRelaxPowerLimit(Action<string, bool> log)
        {
            if (!ProbePowerLimit())
            {
                if (log != null) log("本卡驱动不支持修改功耗墙，仅使用频率与 P-State 控制", false);
                return;
            }
            SetPowerLimit(SleepFallbackWatts, log);
        }

        private static void TryRaisePowerLimit(int watts, Action<string, bool> log)
        {
            if (!ProbePowerLimit())
            {
                if (log != null) log("本卡驱动不支持修改功耗墙，靠放开频率区间来提速", false);
                return;
            }
            SetPowerLimit(watts, log);
        }

        // ---------- 内部 ----------

        private static bool LockCore(int mhz, Action<string, bool> log)
        {
            return Smi("-lgc " + mhz + "," + mhz, log, "核心锁频 " + mhz + "MHz");
        }

        private static bool SetPowerLimit(int watts, Action<string, bool> log)
        {
            // 安全底线：绝不允许写入 8W 以下。本卡 min_limit 报 0 是占位值，
            // 写 0 会把卡永久钳在 0W 预算（实测 -pl 0 之后 450MHz / 99W 且无法改回）。
            if (watts < 8)
            {
                if (log != null) log("拒绝写入 " + watts + "W：低于安全下限 8W", true);
                return false;
            }
            return Smi("-pl " + watts, log, "功耗墙 " + watts + "W");
        }

        /// <summary>恢复出厂默认功耗墙，用于关闭守护。</summary>
        private static void TryRestoreDefaultPowerLimit(Action<string, bool> log)
        {
            if (!ProbePowerLimit()) return;
            int def = AdaptiveWatts;
            try
            {
                NativeCore.GpuPowerRange r = NativeCore.QueryGpuPowerRange();
                if (r != null && r.DefaultLimit >= 8) def = (int)Math.Round(r.DefaultLimit);
            }
            catch { }
            SetPowerLimit(def, log);
        }

        private static bool Smi(string args, Action<string, bool> log, string desc)
        {
            try
            {
                string outp = NativeCore.RunNvidiaSmiCommand(args);
                string t = outp == null ? "" : outp.Trim();
                bool bad = t.Length == 0
                           || t.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0
                           || t.IndexOf("not supported", StringComparison.OrdinalIgnoreCase) >= 0
                           || t.IndexOf("insufficient", StringComparison.OrdinalIgnoreCase) >= 0
                           || t.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0
                           || t.IndexOf("out of range", StringComparison.OrdinalIgnoreCase) >= 0;
                if (bad)
                {
                    LastError = desc + " → " + (t.Length == 0 ? "无输出" : t);
                    if (log != null) log("设置失败 " + LastError, true);
                    return false;
                }
                if (log != null) log("已设置 " + desc, false);
                return true;
            }
            catch (Exception ex)
            {
                LastError = desc + " 异常: " + ex.Message;
                if (log != null) log(LastError, true);
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
            if (log != null) log("省电守护已启动：空闲 " + _idleSeconds + " 秒压到 210MHz/P8，有负载立即恢复", false);
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
                    LockCore(SleepCoreClock, log);
                    TryPstate(8, log);
                    lock (_gate) { _throttled = true; _lastSwitch = DateTime.Now.ToString("HH:mm:ss"); }
                    if (log != null) log("空闲已压到 " + SleepCoreClock + "MHz / P8", false);
                }
                else if (!shouldThrottle && _throttled)
                {
                    TryPstate(0, log);
                    Smi("-rgc", log, "解除锁频");
                    lock (_gate) { _throttled = false; _lastSwitch = DateTime.Now.ToString("HH:mm:ss"); }
                    if (log != null) log("检测到负载，频率已放开", false);
                }
            }
            catch { }
        }
    }
}
