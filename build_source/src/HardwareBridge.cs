using System;
using System.Globalization;
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
