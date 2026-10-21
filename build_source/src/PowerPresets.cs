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
        /// <summary>显存最低档。本卡支持的显存频率只有 405 与 9501 两档，
        /// 不把它压下来，核心降到 210MHz 也还有 80W 左右（实测）。</summary>
        public const int SleepMemClock = 405;
        public const int FullMemClock = 9501;
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
        /// <summary>当前生效的空闲阈值（秒）。界面靠它回显，不能写死。</summary>
        public static int IdleSeconds { get { lock (_gate) return _idleSeconds; } }
        public static bool PowerLimitWritable { get { return ProbePowerLimit(); } }

        /// <summary>应用某一档。</summary>
        public static bool Apply(string preset, int idleSeconds, Action<string, bool> log)
        {
            if (string.IsNullOrEmpty(preset)) preset = "adaptive";
            preset = preset.ToLowerInvariant();

            StopWatchdog();
            bool ok = true;
            LastError = "";

            // 只有「智能动态」需要后台托管；其余档位是静态状态，把任务清掉，
            // 免得后台进程和进程内守护抢控制权（实测会让卡在负载下也拉不回频率）。
            if (preset != "adaptive") RemoveBackgroundTask(log);

            if (preset == "sleep")
            {
                // 顺序很重要：先平滑降核心频率，再把显存压到最低档，最后压 P-State 与功耗墙。
                // 显存不降下来，核心降到 210MHz 也还有 80W 左右（实测 9501MHz 时）。
                RampClockDown(SleepCoreClock, log);
                Smi("-lmc " + SleepMemClock + "," + SleepMemClock, log, "显存压到 " + SleepMemClock + "MHz");
                TryPstate(8, log);
                TryRelaxPowerLimit(log);
                ok = true;
            }
            else if (preset == "turbo")
            {
                TryPstate(0, log);
                // 实测：-rgc 清不掉锁频；-lgc 0,x 的最小值 0 非法会被忽略。
                // 唯一被验证能生效的写法是显式给一个合法区间：下限=硬件地板 210，上限=2100。
                ok = Smi("-lgc " + SleepCoreClock + "," + TurboClockCeiling, log,
                         "频率区间设为 " + SleepCoreClock + "~" + TurboClockCeiling + "MHz");
                TryRaisePowerLimit(TurboWatts, log);
                // 显存锁到出厂满速 9501（默认值；用户手动降过，这里拉回满血）
                Smi("-lmc 9501,9501", log, "显存频率锁定 9501MHz");
            }
            else
            {
                preset = "adaptive";
                TryPstate(0, log);
                UnlockClocks(log);
                // 恢复出厂显存频率（本卡默认满速就是 9501）
                Smi("-lmc " + FullMemClock + "," + FullMemClock, log, "显存恢复 " + FullMemClock + "MHz");
                TryRestoreDefaultPowerLimit(log);
                StartWatchdog(idleSeconds, log);
                // 用户要求程序退出后仍能后台调节：装好计划任务，
                // 但程序在运行期间立刻暂停它，交给进程内守护独占
                try
                {
                    if (!NativeCore.IsPowerSaverTaskInstalled())
                    {
                        if (log != null) log("正在注册后台省电计划任务…", false);
                        NativeCore.InstallPowerSaverTask(log);
                        if (log != null)
                            log(NativeCore.IsPowerSaverTaskInstalled()
                                ? "计划任务注册成功（运行期间暂停，退出后接管）"
                                : "计划任务注册失败，请查看日志", false);
                    }
                    NativeCore.SuspendPowerSaverTask(null);
                }
                catch (Exception ex)
                {
                    if (log != null) log("注册计划任务异常: " + ex.Message, true);
                }
            }

            lock (_gate)
            {
                _current = preset;
                _lastSwitch = DateTime.Now.ToString("HH:mm:ss");
                _throttled = false;
            }
            AppSettings.SetPreset(preset, _idleSeconds);
            if (log != null) log("已切换到预设：" + preset, false);

            // 应用后回读一次真实频率，写进日志——不再靠猜
            System.Threading.Tasks.Task.Run(new Action(delegate
            {
                try
                {
                    System.Threading.Thread.Sleep(1500);
                    NativeCore.GpuSensorSnapshot s = NativeCore.QueryGpuSensors();
                    if (s != null && log != null)
                        log("回读：频率 " + s.GraphicsClockMHz + "MHz / " + s.PState +
                            " / 功耗 " + s.PowerDraw.ToString("0.#") + "W", false);
                }
                catch { }
            }));

            return ok;
        }

        /// <summary>
        /// 移除后台省电计划任务。
        /// 规则：只有「智能动态」需要后台托管；静音/满血/关闭守护都是
        /// 明确的静态状态，不需要后台进程，留着反而会和进程内守护抢控制权。
        /// </summary>
        private static void RemoveBackgroundTask(Action<string, bool> log)
        {
            try
            {
                if (!NativeCore.IsPowerSaverTaskInstalled()) return;
                NativeCore.UninstallPowerSaverTask(log);
                if (log != null) log("后台省电计划任务已移除（当前档位不需要）", false);
            }
            catch (Exception ex)
            {
                if (log != null) log("移除后台任务异常: " + ex.Message, true);
            }
        }

        /// <summary>彻底解除所有限制。</summary>
        public static void StopAll(Action<string, bool> log)
        {
            StopWatchdog();
            // 关闭守护 = 完全不管了，后台任务也没必要留着
            RemoveBackgroundTask(log);
            TryPstate(0, log);
            UnlockClocks(log);
            Smi("-rmc", log, "恢复显存调度");
            Smi("-lmc " + FullMemClock + "," + FullMemClock, log, "显存恢复 " + FullMemClock + "MHz");
            // 关闭守护必须恢复出厂功耗墙。原来这里误用了 TryRelaxPowerLimit（12W），
            // 结果「关闭守护」= 把功耗墙压到 12W，频率自然被锁在 210MHz。
            TryRestoreDefaultPowerLimit(log);
            lock (_gate)
            {
                _current = "none";
                _throttled = false;
                _lastSwitch = DateTime.Now.ToString("HH:mm:ss");
            }
            AppSettings.SetPreset("none", _idleSeconds);
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

        /// <summary>
        /// 真正解除锁频。实测本卡 -rgc 无效（返回成功但频率不动），
        /// 必须显式写入合法区间 -lgc 210,2100 才能放开。
        /// </summary>
        private static bool UnlockClocks(Action<string, bool> log)
        {
            return Smi("-lgc " + SleepCoreClock + "," + TurboClockCeiling, log,
                       "频率区间恢复 " + SleepCoreClock + "~" + TurboClockCeiling + "MHz");
        }

        private static bool LockCore(int mhz, Action<string, bool> log)
        {
            return Smi("-lgc " + mhz + "," + mhz, log, "核心锁频 " + mhz + "MHz");
        }

        /// <summary>
        /// 平滑降频：把当前频率分步压到目标值，而不是一步跳到地板。
        /// 硬切会让功耗瞬间掉到十几瓦、又被驱动拉回去，看起来就是「闪来闪去」。
        /// </summary>
        private static void RampClockDown(int targetMhz, Action<string, bool> log)
        {
            int current = targetMhz;
            try
            {
                NativeCore.GpuSensorSnapshot s = NativeCore.QueryGpuSensors();
                if (s != null && s.GraphicsClockMHz > 0) current = s.GraphicsClockMHz;
            }
            catch { }

            if (current <= targetMhz + 50)
            {
                Smi("-lgc " + targetMhz + "," + targetMhz, log, "核心锁频 " + targetMhz + "MHz");
                return;
            }

            const int steps = 6;
            for (int i = 1; i <= steps; i++)
            {
                int v = current - (current - targetMhz) * i / steps;
                Smi("-lgc " + v + "," + v, null, null);   // 中间步骤静默，不刷日志
                try { System.Threading.Thread.Sleep(260); } catch { }
            }
            if (log != null) log("核心频率已从 " + current + "MHz 平滑降到 " + targetMhz + "MHz", false);
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
                // 下限只留 1 秒，不再强制抬到 5 秒——用户设 1 就该是 1
                _idleSeconds = idleSeconds < 1 ? 1 : idleSeconds;
                _lastBusy = DateTime.Now;
                // 轮询 1 秒，否则设 1~2 秒的阈值根本来不及触发
                _watch = new Timer(WatchTick, log, 1000, 1000);
            }
            if (log != null) log("省电守护已启动：空闲 " + _idleSeconds + " 秒压到 210MHz/P8/显存405，有负载立即恢复", false);
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

                // 判定「忙」只看利用率和明显偏高的功耗。
                // 原来的 60W 阈值有致命问题：显存还在 9501 时，空闲功耗就有 80W 左右，
                // 会永远被判成「有负载」，于是永远不下压——形成死循环。
                bool busy = s.GpuUtilPercent >= 10 || s.PowerDraw >= 150;
                if (busy) _lastBusy = DateTime.Now;

                bool shouldThrottle = !busy && (DateTime.Now - _lastBusy).TotalSeconds >= _idleSeconds;

                if (shouldThrottle && !_throttled)
                {
                    LockCore(SleepCoreClock, log);
                    Smi("-lmc " + SleepMemClock + "," + SleepMemClock, log, "显存压到 " + SleepMemClock + "MHz");
                    TryPstate(8, log);
                    lock (_gate) { _throttled = true; _lastSwitch = DateTime.Now.ToString("HH:mm:ss"); }
                    if (log != null) log("空闲已压到 " + SleepCoreClock + "MHz / 显存 " + SleepMemClock + "MHz / P8", false);
                }
                else if (!shouldThrottle && _throttled)
                {
                    TryPstate(0, log);
                    UnlockClocks(log);
                    Smi("-lmc " + FullMemClock + "," + FullMemClock, log, "显存恢复 " + FullMemClock + "MHz");
                    lock (_gate) { _throttled = false; _lastSwitch = DateTime.Now.ToString("HH:mm:ss"); }
                    if (log != null) log("检测到负载，频率已放开", false);
                }
            }
            catch { }
        }
    }
}
