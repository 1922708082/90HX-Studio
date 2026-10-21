using System;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace CMP90HXToolbox
{
    /// <summary>
    /// 宿主 → 页面的数据桥。
    ///
    /// 只负责把 NativeCore / SystemDetector 的实测值拼成页面约定的 JSON 信封，
    /// 不做任何业务判断，也不碰 UI。
    ///
    /// 信封格式（与 index.html handleIncomingEnvelope 对齐）：
    ///   { "kind":"evt", "action":"device.snapshot", "payload":{...} }
    /// </summary>
    internal static class HardwareBridge
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// 握手事件。必须在页面启动后 800ms 内送达一次，
        /// 否则页面会判定「无宿主」并切到 MOCK 模式，之后所有命令都不再发往宿主。
        /// </summary>
        public static string ThemeEvt(string theme)
        {
            return "{\"kind\":\"evt\",\"action\":\"theme.apply\",\"payload\":{\"theme\":\"" +
                   Esc(string.IsNullOrEmpty(theme) ? "light" : theme) + "\"}}";
        }

        /// <summary>版本号事件。左上角角标由这里驱动，页面里不再写死。</summary>
        public static string VersionEvt()
        {
            string v = "0.0.0";
            try
            {
                Version ver = Assembly.GetExecutingAssembly().GetName().Version;
                if (ver != null) v = ver.Major + "." + ver.Minor + "." + ver.Build;
            }
            catch { }
            return "{\"kind\":\"evt\",\"action\":\"app.version\",\"payload\":{\"version\":\"" +
                   Esc(v) + "\"}}";
        }

        /// <summary>显卡槽位列表。设备页的「显卡列表」表格靠它填充。</summary>
        public static string GpuListEvt()
        {
            System.Collections.Generic.List<NativeCore.InstalledMiningGpu> list = null;
            try { list = NativeCore.DetectAllMiningGpus(); }
            catch { }
            if (list == null) list = new System.Collections.Generic.List<NativeCore.InstalledMiningGpu>();

            StringBuilder sb = new StringBuilder(600);
            sb.Append("{\"kind\":\"evt\",\"action\":\"gpu.list\",\"payload\":{\"gpus\":[");
            for (int i = 0; i < list.Count; i++)
            {
                NativeCore.InstalledMiningGpu g = list[i];
                if (i > 0) sb.Append(',');
                sb.Append("{\"slot\":").Append(g.SlotIndex).Append(',');
                sb.Append("\"detectedModel\":\"").Append(Esc(g.DetectedModel)).Append("\",");
                sb.Append("\"chip\":\"").Append(Esc(g.ChipArchitecture)).Append("\",");
                sb.Append("\"vram\":\"").Append(Esc(g.VramCapacity)).Append("\",");
                sb.Append("\"currentDesc\":\"").Append(Esc(g.CurrentDesc)).Append("\",");
                sb.Append("\"targetName\":\"").Append(Esc(g.TargetGeForceName)).Append("\",");
                sb.Append("\"isSpoofed\":").Append(Bool(g.IsSpoofed)).Append("}");
            }
            sb.Append("]}}");
            return sb.ToString();
        }

        /// <summary>双卡拓扑状态。用真实检测结果，不再占位。</summary>
        public static string DualStateEvt()
        {
            NativeCore.DualGpuTopology t = null;
            try { t = NativeCore.DetectDualGpuTopology(); }
            catch { }
            if (t == null) t = new NativeCore.DualGpuTopology();

            StringBuilder sb = new StringBuilder(600);
            sb.Append("{\"kind\":\"evt\",\"action\":\"dual.state\",\"payload\":{");
            sb.Append("\"detected\":").Append(Bool(t.HasRenderGpu && t.HasDisplayGpu)).Append(',');
            sb.Append("\"hagsActive\":").Append(Bool(t.HagsActive)).Append(',');
            sb.Append("\"boundGames\":").Append(t.BoundGamesCount).Append(',');
            sb.Append("\"render\":{\"present\":").Append(Bool(t.HasRenderGpu))
              .Append(",\"name\":").Append(Quote(t.RenderGpuName))
              .Append(",\"pciId\":").Append(Quote(t.RenderGpuPciId)).Append("},");
            sb.Append("\"display\":{\"present\":").Append(Bool(t.HasDisplayGpu))
              .Append(",\"name\":").Append(Quote(t.DisplayGpuName))
              .Append(",\"pciId\":").Append(Quote(t.DisplayGpuPciId)).Append("},");
            sb.Append("\"virtual\":{\"present\":").Append(Bool(t.HasVirtualGpu))
              .Append(",\"name\":").Append(Quote(t.VirtualGpuName)).Append("},");
            sb.Append("\"gpus\":[],\"bindings\":[]");
            sb.Append("}}");
            return sb.ToString();
        }

        /// <summary>覆盖层状态（NVIDIA App 补丁）。单独成方法，解锁/还原后可直接重推。</summary>
        public static string OverlayStateEvt()
        {
            SystemStatus st = null;
            try { st = SystemDetector.DetectAll(); }
            catch { }
            if (st == null) st = new SystemStatus();

            StringBuilder sb = new StringBuilder(400);
            sb.Append("{\"kind\":\"evt\",\"action\":\"overlay.state\",\"payload\":{");
            sb.Append("\"patched\":").Append(Bool(st.OverlayPatched)).Append(",");
            sb.Append("\"backupExists\":").Append(Bool(st.OverlayBakExists)).Append(",");
            sb.Append("\"nvAppInstalled\":").Append(Bool(st.NvAppInstalled)).Append(",");
            sb.Append("\"nvAppPath\":").Append(Quote(st.NvAppPath));
            sb.Append("}}");
            return sb.ToString();
        }

        /// <summary>其余页面：先推可用的真实字段，缺失的留空让页面走空态，
        /// 避免整页停在「等待宿主同步数据」。</summary>
        public static string[] StubStates()
        {
            SystemStatus st = null;
            try { st = SystemDetector.DetectAll(); }
            catch { }
            if (st == null) st = new SystemStatus();

            System.Collections.Generic.List<string> outList = new System.Collections.Generic.List<string>();
            StringBuilder sb = new StringBuilder(1200);

            // 覆盖层状态改用 OverlayStateEvt()，解锁/还原后可单独重推
            outList.Add(OverlayStateEvt());

            // DLSS：尚未扫描过游戏目录，给空列表（诚实空态，不编造）
            sb.Append("{\"kind\":\"evt\",\"action\":\"dlss.state\",\"payload\":{");
            sb.Append("\"games\":[],\"deployed\":[],\"preflightOk\":").Append(Bool(false));
            sb.Append("}}");
            outList.Add(sb.ToString());
            sb = new StringBuilder(600);

            // 双卡拓扑改用 DualStateEvt()，扫描后可单独重推
            outList.Add(DualStateEvt());

            // 快照列表改由 SnapshotsEvt() 统一提供（创建/删除后可直接复用）
            outList.Add(sb.ToString());
            sb = new StringBuilder(400);

            // 更新状态：本机版本，无可用更新（未接更新源前如实报告）
            sb.Append("{\"kind\":\"evt\",\"action\":\"update.state\",\"payload\":{");
            sb.Append("\"currentVersion\":").Append(Quote(CurrentVersion())).Append(",");
            sb.Append("\"latestVersion\":").Append(Quote(CurrentVersion())).Append(",");
            sb.Append("\"hasUpdate\":false,\"verified\":false,\"releaseNotes\":\"\"");
            sb.Append("}}");
            outList.Add(sb.ToString());

            return outList.ToArray();
        }

        public static string CurrentVersion()
        {
            try
            {
                Version v = Assembly.GetExecutingAssembly().GetName().Version;
                if (v != null) return v.Major + "." + v.Minor + "." + v.Build;
            }
            catch { }
            return "0.0.0";
        }

        /// <summary>系统维护页的快照列表（扫描备份目录，时间倒序）。</summary>
        public static string SnapshotsEvt()
        {
            StringBuilder sb = new StringBuilder(1200);
            sb.Append("{\"kind\":\"evt\",\"action\":\"maintenance.snapshots\",\"payload\":{\"items\":[");
            try
            {
                string dir = Diagnostics.BackupDir;
                if (!string.IsNullOrEmpty(dir) && System.IO.Directory.Exists(dir))
                {
                    System.IO.FileInfo[] files = new System.IO.DirectoryInfo(dir).GetFiles();
                    System.Array.Sort(files, delegate(System.IO.FileInfo a, System.IO.FileInfo b)
                    {
                        return b.LastWriteTime.CompareTo(a.LastWriteTime);
                    });
                    int n = 0;
                    for (int i = 0; i < files.Length && n < 40; i++)
                    {
                        System.IO.FileInfo fi = files[i];
                        bool isSnap = fi.Name.StartsWith("snap_", StringComparison.OrdinalIgnoreCase);
                        if (n > 0) sb.Append(',');
                        sb.Append("{\"id\":").Append(Quote(fi.Name));
                        sb.Append(",\"label\":").Append(Quote(fi.Name));
                        sb.Append(",\"createdAt\":").Append(Quote(fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")));
                        sb.Append(",\"sizeBytes\":").Append(fi.Length);
                        sb.Append(",\"kind\":").Append(Quote(isSnap ? "manual" : "auto"));
                        // 回滚能力尚未接入，如实标注，避免界面给出不该有的承诺
                        sb.Append(",\"restorable\":").Append(Bool(false)).Append('}');
                        n++;
                    }
                }
            }
            catch { }
            sb.Append("]}}");
            return sb.ToString();
        }

        /// <summary>一次性构建全部首屏状态（快照 + 显卡列表 + 三个页面状态）。</summary>
        public static string[] AllInitialEvents()
        {
            System.Collections.Generic.List<string> list = new System.Collections.Generic.List<string>();
            try { list.Add(DeviceSnapshotEvt()); } catch { }
            try { list.Add(GpuListEvt()); } catch { }
            try { list.Add(SpoofStateEvt()); } catch { }
            try { list.Add(PowerStateEvt()); } catch { }
            try { list.Add(UnlockStateEvt()); } catch { }
            // 型号目录由 C# 单一持有，前端只负责渲染
            try { list.Add(SpoofCatalog.ToJson()); } catch { }
            try { list.Add(SnapshotsEvt()); } catch { }
            try
            {
                string[] stubs = StubStates();
                for (int i = 0; i < stubs.Length; i++)
                    if (!string.IsNullOrEmpty(stubs[i])) list.Add(stubs[i]);
            }
            catch { }
            return list.ToArray();
        }

        /// <summary>JSON 字符串字面量（带引号），供宿主侧拼装应答用。</summary>
        public static string Quote(string s)
        {
            return "\"" + Esc(s) + "\"";
        }

        /// <summary>伪装页状态：与显卡列表同源，外加当前伪装目标。</summary>
        public static string SpoofStateEvt()
        {
            // 直接自建数组，不再从 GpuListEvt 的 JSON 里切字符串
            // （切错一位整条事件就废，页面会一直停在「等待同步」）
            StringBuilder sb = new StringBuilder(500);
            sb.Append("{\"kind\":\"evt\",\"action\":\"spoof.state\",\"payload\":{\"gpus\":[");
            try
            {
                System.Collections.Generic.List<NativeCore.InstalledMiningGpu> gpus =
                    NativeCore.DetectAllMiningGpus();
                if (gpus != null)
                {
                    for (int i = 0; i < gpus.Count; i++)
                    {
                        NativeCore.InstalledMiningGpu g = gpus[i];
                        if (i > 0) sb.Append(',');
                        sb.Append("{\"slot\":").Append(g.SlotIndex).Append(',');
                        sb.Append("\"detectedModel\":\"").Append(Esc(g.DetectedModel)).Append("\",");
                        sb.Append("\"chip\":\"").Append(Esc(g.ChipArchitecture)).Append("\",");
                        sb.Append("\"vram\":\"").Append(Esc(g.VramCapacity)).Append("\",");
                        sb.Append("\"currentDesc\":\"").Append(Esc(g.CurrentDesc)).Append("\",");
                        sb.Append("\"targetName\":\"").Append(Esc(g.TargetGeForceName)).Append("\",");
                        sb.Append("\"isSpoofed\":").Append(Bool(g.IsSpoofed)).Append("}");
                    }
                }
            }
            catch { }
            sb.Append("]}}");
            return sb.ToString();
        }

        /// <summary>节能页状态：守护进程 / 任务 / 功耗墙，全部取实测值。</summary>
        public static string PowerStateEvt()
        {
            NativeCore.GpuPowerRange range = null;
            try { range = NativeCore.QueryGpuPowerRange(); }
            catch { }
            if (range == null) range = new NativeCore.GpuPowerRange();

            NativeCore.GpuSensorSnapshot s = null;
            try { s = NativeCore.QueryGpuSensors(); }
            catch { }
            if (s == null) s = new NativeCore.GpuSensorSnapshot();

            bool running = false, saveActive = false, taskInstalled = false;
            try { running = NativeCore.IsPowerSaverRunning(); } catch { }
            try { saveActive = NativeCore.IsGpuIdleSaveActive(); } catch { }
            try { taskInstalled = NativeCore.IsPowerSaverTaskInstalled(); } catch { }

            StringBuilder sb = new StringBuilder(360);
            sb.Append("{\"kind\":\"evt\",\"action\":\"power.state\",\"payload\":{");
            sb.Append("\"enabled\":").Append(Bool(PowerPresets.Current != "none" || running)).Append(",");
            sb.Append("\"running\":").Append(Bool(running || PowerPresets.WatchdogRunning)).Append(",");
            sb.Append("\"preset\":").Append(Quote(PowerPresets.Current == "none" ? "manual" : PowerPresets.Current)).Append(",");
            sb.Append("\"idleSeconds\":").Append(PowerPresets.IdleSeconds).Append(",");
            sb.Append("\"currentPstate\":\"").Append(Esc(PowerPresets.Throttled ? "P8" : s.PState)).Append("\",");
            sb.Append("\"powerRange\":{\"min\":").Append(PowerPresets.SleepWatts)
              .Append(",\"default\":").Append(F1(range.DefaultLimit))
              .Append(",\"max\":").Append(PowerPresets.TurboWatts)
              .Append(",\"current\":").Append(F1(range.CurrentLimit)).Append("},");
            sb.Append("\"taskInstalled\":").Append(Bool(taskInstalled)).Append(",");
            sb.Append("\"lastTrigger\":").Append(Quote(PowerPresets.LastSwitch));
            sb.Append("}}");
            return sb.ToString();
        }

        /// <summary>固件解锁页状态：引导模式、UEFI/SecureBoot、通道可用性。</summary>
        public static string UnlockStateEvt()
        {
            SystemStatus st = null;
            try { st = SystemDetector.DetectAll(); }
            catch { }
            if (st == null) st = new SystemStatus();

            bool unlocked = false;
            try { unlocked = st.PcieMode != NativeCore.PcieUnlockMode.None; } catch { }
            string modeLabel = string.IsNullOrEmpty(st.PcieModeDesc) ? "未检测到引导状态" : st.PcieModeDesc;

            StringBuilder sb = new StringBuilder(520);
            sb.Append("{\"kind\":\"evt\",\"action\":\"unlock.state\",\"payload\":{");
            sb.Append("\"mode\":\"").Append(unlocked ? "Mode1_DirectBoot" : "Mode0_Locked").Append("\",");
            sb.Append("\"modeLabel\":").Append(Quote(modeLabel)).Append(",");
            sb.Append("\"detail\":").Append(Quote(unlocked ? "已注入引导载荷" : "使用官方引导，未注入")).Append(",");
            sb.Append("\"hasBackup\":false,");
            sb.Append("\"isUefi\":").Append(Bool(st.IsUefi)).Append(",");
            sb.Append("\"secureBoot\":").Append(Bool(st.SecureBootEnabled)).Append(",");
            sb.Append("\"espMounted\":false,");
            sb.Append("\"channels\":[");
            sb.Append("{\"id\":\"A\",\"label\":\"UEFI 直启\",\"available\":").Append(Bool(st.IsUefi)).Append(",\"installed\":").Append(Bool(unlocked)).Append("},");
            sb.Append("{\"id\":\"B\",\"label\":\"UEFI Shell 链启\",\"available\":").Append(Bool(st.IsUefi)).Append(",\"installed\":false},");
            sb.Append("{\"id\":\"C\",\"label\":\"U 盘引导\",\"available\":true,\"installed\":false},");
            sb.Append("{\"id\":\"D\",\"label\":\"恢复官方引导\",\"available\":true,\"installed\":false}");
            sb.Append("],\"usbDrives\":[]");
            sb.Append("}}");
            return sb.ToString();
        }

        /// <summary>设备快照：显卡身份、传感器初值、系统信息、环境标志。</summary>
        public static string DeviceSnapshotEvt()
        {
            SystemStatus st = null;
            try { st = SystemDetector.DetectAll(); }
            catch { }
            if (st == null) st = new SystemStatus();

            NativeCore.GpuSensorSnapshot s = st.Sensors;
            if (s == null) s = new NativeCore.GpuSensorSnapshot();

            MachineHardwareProfile hw = st.Hardware;
            if (hw == null) hw = new MachineHardwareProfile();

            NativeCore.GpuTempThresholds th = null;
            try { th = NativeCore.QueryGpuTempThresholds(); }
            catch { }
            if (th == null) th = new NativeCore.GpuTempThresholds();

            bool isAdmin = false;
            bool powerSaverRunning = false;
            try { isAdmin = NativeCore.IsAdmin(); }
            catch { }
            try { powerSaverRunning = NativeCore.IsPowerSaverRunning(); }
            catch { }

            StringBuilder sb = new StringBuilder(1400);
            sb.Append("{\"kind\":\"evt\",\"action\":\"device.snapshot\",\"payload\":{");

            // ---- gpu ----
            sb.Append("\"gpu\":{");
            sb.Append("\"name\":\"").Append(Esc(st.GpuDesc)).Append("\",");
            sb.Append("\"model\":\"").Append(Esc(st.GpuDesc)).Append("\",");
            sb.Append("\"chip\":\"").Append(Esc(InferChip(st.GpuDesc))).Append("\",");
            sb.Append("\"deviceId\":\"").Append(Esc(st.GpuId)).Append("\",");
            sb.Append("\"vram\":\"").Append(Esc(VramText(s.MemoryTotalMB))).Append("\",");
            sb.Append("\"driverVersion\":\"").Append(Esc(st.DriverVersion)).Append("\",");
            sb.Append("\"bus\":\"\",");
            sb.Append("\"pcieGen\":\"").Append(Esc(s.PcieGen)).Append("\",");
            sb.Append("\"pcieWidth\":\"").Append(Esc(s.PcieWidth)).Append("\",");
            sb.Append("\"pcieFormatted\":\"").Append(Esc(s.PcieFormatted)).Append("\",");
            sb.Append("\"isSpoofed\":").Append(Bool(st.IsSpoofedTo3080)).Append(",");
            sb.Append("\"displayName\":\"").Append(Esc(st.GpuDesc)).Append("\"");
            sb.Append("},");

            // ---- sensors ----
            sb.Append("\"sensors\":{");
            sb.Append("\"valid\":").Append(Bool(s.IsValid)).Append(",");
            sb.Append("\"temp\":").Append(Num(s.Temperature)).Append(",");
            sb.Append("\"fanPercent\":").Append(Num(s.FanPercent)).Append(",");
            sb.Append("\"powerDraw\":").Append(F1(s.PowerDraw)).Append(",");
            sb.Append("\"powerLimit\":").Append(F1(s.PowerLimit)).Append(",");
            sb.Append("\"powerMinLimit\":").Append(F1(s.PowerMinLimit)).Append(",");
            sb.Append("\"powerDefaultLimit\":").Append(F1(s.PowerDefaultLimit)).Append(",");
            sb.Append("\"powerMaxLimit\":").Append(F1(s.PowerMaxLimit)).Append(",");
            sb.Append("\"memUsedMB\":").Append(Num(s.MemoryUsedMB)).Append(",");
            sb.Append("\"memTotalMB\":").Append(Num(s.MemoryTotalMB)).Append(",");
            sb.Append("\"utilPercent\":").Append(Num(s.GpuUtilPercent)).Append(",");
            sb.Append("\"coreClock\":").Append(Num(s.GraphicsClockMHz)).Append(",");
            sb.Append("\"coreClockMax\":").Append(Num(s.GraphicsClockMaxMHz)).Append(",");
            sb.Append("\"memClock\":").Append(Num(s.MemoryClockMHz)).Append(",");
            sb.Append("\"memClockMax\":").Append(Num(s.MemoryClockMaxMHz)).Append(",");
            sb.Append("\"pstate\":\"").Append(Esc(s.PState)).Append("\",");
            sb.Append("\"tempSlowdownC\":").Append(Num(th.SlowdownC)).Append(",");
            sb.Append("\"tempShutdownC\":").Append(Num(th.MaxC));
            sb.Append("},");

            // ---- system ----
            sb.Append("\"system\":{");
            sb.Append("\"cpuName\":\"").Append(Esc(hw.CpuName)).Append("\",");
            sb.Append("\"cpuCores\":\"").Append(Esc(hw.CpuCores)).Append("\",");
            sb.Append("\"cpuClock\":\"").Append(Esc(hw.CpuClock)).Append("\",");
            sb.Append("\"motherboard\":\"").Append(Esc(hw.MotherboardModel)).Append("\",");
            sb.Append("\"bios\":\"").Append(Esc(hw.BiosVersion)).Append("\",");
            sb.Append("\"memory\":\"").Append(Esc(hw.TotalMemory)).Append("\",");
            sb.Append("\"os\":\"").Append(Esc(hw.OsVersion)).Append("\",");
            sb.Append("\"uefi\":").Append(Bool(st.IsUefi)).Append(",");
            sb.Append("\"secureBoot\":").Append(Bool(st.SecureBootEnabled)).Append(",");
            sb.Append("\"hags\":").Append(Bool(st.IsHagsEnabled)).Append(",");
            sb.Append("\"uptime\":\"").Append(Esc(hw.Uptime)).Append("\"");
            sb.Append("},");

            // ---- flags ----
            sb.Append("\"flags\":{");
            sb.Append("\"isAdmin\":").Append(Bool(isAdmin)).Append(",");
            sb.Append("\"nvAppInstalled\":").Append(Bool(st.NvAppInstalled)).Append(",");
            sb.Append("\"isPowerSaverRunning\":").Append(Bool(powerSaverRunning));
            sb.Append("}");

            sb.Append("}}");
            return sb.ToString();
        }

        /// <summary>传感器增量推送（1.5s 一次）。页面会把它合并进已有 sensors 对象。</summary>
        public static string SensorsTickEvt()
        {
            NativeCore.GpuSensorSnapshot s = null;
            try { s = NativeCore.QueryGpuSensors(); }
            catch { }
            if (s == null) s = new NativeCore.GpuSensorSnapshot();

            StringBuilder sb = new StringBuilder(420);
            sb.Append("{\"kind\":\"evt\",\"action\":\"sensors.tick\",\"payload\":{");
            sb.Append("\"temp\":").Append(Num(s.Temperature)).Append(",");
            sb.Append("\"fanPercent\":").Append(Num(s.FanPercent)).Append(",");
            sb.Append("\"powerDraw\":").Append(F1(s.PowerDraw)).Append(",");
            sb.Append("\"powerLimit\":").Append(F1(s.PowerLimit)).Append(",");
            sb.Append("\"memUsedMB\":").Append(Num(s.MemoryUsedMB)).Append(",");
            sb.Append("\"memTotalMB\":").Append(Num(s.MemoryTotalMB)).Append(",");
            sb.Append("\"utilPercent\":").Append(Num(s.GpuUtilPercent)).Append(",");
            sb.Append("\"coreClock\":").Append(Num(s.GraphicsClockMHz)).Append(",");
            sb.Append("\"coreClockMax\":").Append(Num(s.GraphicsClockMaxMHz)).Append(",");
            sb.Append("\"memClock\":").Append(Num(s.MemoryClockMHz)).Append(",");
            sb.Append("\"memClockMax\":").Append(Num(s.MemoryClockMaxMHz)).Append(",");
            sb.Append("\"pstate\":\"").Append(Esc(s.PState)).Append("\",");
            sb.Append("\"valid\":").Append(Bool(s.IsValid));
            sb.Append("}}");
            return sb.ToString();
        }

        // ---------- 小工具 ----------

        private static string Bool(bool v)
        {
            return v ? "true" : "false";
        }

        private static string Num(long v)
        {
            return v.ToString(Inv);
        }

        private static string Num(int v)
        {
            return v.ToString(Inv);
        }

        private static string F1(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "0";
            return v.ToString("0.#", Inv);
        }

        private static string VramText(long totalMB)
        {
            if (totalMB <= 0) return "";
            double gb = totalMB / 1024.0;
            return gb.ToString("0.#", Inv) + "GB";
        }

        /// <summary>从型号名推断芯片代号，用于首页标签；推断不出就留空。</summary>
        private static string InferChip(string gpuDesc)
        {
            if (string.IsNullOrEmpty(gpuDesc)) return "";
            string d = gpuDesc.ToUpperInvariant();
            if (d.IndexOf("90HX") >= 0) return "GA102";
            if (d.IndexOf("70HX") >= 0) return "GA104";
            if (d.IndexOf("50HX") >= 0) return "TU102";
            if (d.IndexOf("40HX") >= 0) return "TU106";
            if (d.IndexOf("30HX") >= 0) return "TU116";
            return "";
        }

        /// <summary>JSON 字符串转义。设备名里可能出现引号或反斜杠。</summary>
        private static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder(s.Length + 8);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append(' ');
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
