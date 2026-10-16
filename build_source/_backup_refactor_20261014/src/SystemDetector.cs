using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace CMP90HXToolbox
{
    public class GpuDeviceInfo
    {
        public string Name { get; set; }
        public string PciId { get; set; }
        public string DriverVersion { get; set; }
        public string MemorySize { get; set; }
        public string RoleTag { get; set; }
        public bool IsMiningCard { get; set; }
    }

    public class MachineHardwareProfile
    {
        public string ModelName { get; set; }
        public string Uptime { get; set; }
        public string CpuName { get; set; }
        public string CpuCores { get; set; }
        public string CpuClock { get; set; }
        public string MotherboardModel { get; set; }
        public string BiosVersion { get; set; }
        public string TotalMemory { get; set; }
        public string OsVersion { get; set; }
        public string UefiStatus { get; set; }
        public string SecureBootStatus { get; set; }
        public string HagsStatus { get; set; }
        public List<GpuDeviceInfo> GpuList { get; set; }
        public string DisplayDevices { get; set; }
        public string DiskDevices { get; set; }
        public string AudioDevices { get; set; }
        public string NetworkDevices { get; set; }

        public MachineHardwareProfile()
        {
            GpuList = new List<GpuDeviceInfo>();
            ModelName = "ASUS System Product Name";
            DisplayDevices = "主屏 [27.0\" 2560 x 1440 144Hz/180Hz]";
            DiskDevices = "致态 YMTC PC41Q-1TB-B (953.9GB) · 高速 NVMe";
            AudioDevices = "AMD High Definition Audio / Realtek Audio";
            NetworkDevices = "Realtek 2.5GbE LAN · MediaTek Wi-Fi 6";
        }
    }

    public class SystemStatus
    {
        public string GpuDesc { get; set; }
        public string GpuId { get; set; }
        public string ClassSubKey { get; set; }
        public bool IsSpoofedTo3080 { get; set; }
        public bool NvAppInstalled { get; set; }
        public string NvAppPath { get; set; }
        public bool OverlayPatched { get; set; }
        public bool OverlayBakExists { get; set; }
        public NativeCore.PcieUnlockMode PcieMode { get; set; }
        public string PcieModeDesc { get; set; }
        public bool IsUefi { get; set; }
        public bool SecureBootEnabled { get; set; }
        public string DriverVersion { get; set; }
        public bool IsHagsEnabled { get; set; }
        public NativeCore.GpuSensorSnapshot Sensors { get; set; }
        public MachineHardwareProfile Hardware { get; set; }
    }

    public static class SystemDetector
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX()
            {
                this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        public static SystemStatus DetectAll()
        {
            var status = new SystemStatus
            {
                GpuDesc = "未检测到 90HX 显卡",
                GpuId = "未知",
                ClassSubKey = "0001",
                IsSpoofedTo3080 = false,
                NvAppInstalled = false,
                NvAppPath = "",
                OverlayPatched = false,
                OverlayBakExists = false,
                PcieMode = NativeCore.PcieUnlockMode.None,
                PcieModeDesc = "检测中...",
                IsUefi = true,
                SecureBootEnabled = false,
                DriverVersion = "未知",
                IsHagsEnabled = false,
                Sensors = new NativeCore.GpuSensorSnapshot(),
                Hardware = new MachineHardwareProfile()
            };

            DetectGpu(status);
            DetectOverlay(status);
            DetectPcie(status);
            DetectSystemSecurity(status);
            DetectSensors(status);
            DetectFullHardware(status);

            return status;
        }

        public static void DetectFullHardware(SystemStatus status)
        {
            var hw = status.Hardware;

            // 0. 系统运行时间计算
            try
            {
                TimeSpan span = TimeSpan.FromMilliseconds(Environment.TickCount);
                hw.Uptime = string.Format("{0}天{1}小时{2}分钟{3}秒", span.Days, span.Hours, span.Minutes, span.Seconds);
            }
            catch
            {
                hw.Uptime = "0天2小时15分钟";
            }

            // 1. CPU 处理器检测
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                {
                    if (k != null)
                    {
                        hw.CpuName = Convert.ToString(k.GetValue("ProcessorNameString") ?? "未知处理器").Trim();
                        int mhz = Convert.ToInt32(k.GetValue("~MHz") ?? 0);
                        hw.CpuClock = mhz > 0 ? (mhz / 1000.0).ToString("0.0") + " GHz" : "标准主频";
                    }
                }
                hw.CpuCores = Environment.ProcessorCount + " 逻辑处理器";
            }
            catch
            {
                hw.CpuName = "通用 x64 处理器";
                hw.CpuCores = Environment.ProcessorCount + " 线程";
                hw.CpuClock = "";
            }

            // 2. 主板与 BIOS
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\BIOS"))
                {
                    if (k != null)
                    {
                        string mfg = Convert.ToString(k.GetValue("BaseBoardManufacturer") ?? "");
                        string prod = Convert.ToString(k.GetValue("BaseBoardProduct") ?? "");
                        hw.MotherboardModel = (mfg + " " + prod).Trim();
                        if (string.IsNullOrEmpty(hw.MotherboardModel)) hw.MotherboardModel = "标准 UEFI 主板";

                        string bVer = Convert.ToString(k.GetValue("BIOSVersion") ?? "");
                        string bDate = Convert.ToString(k.GetValue("BIOSReleaseDate") ?? "");
                        hw.BiosVersion = bVer + (string.IsNullOrEmpty(bDate) ? "" : " (" + bDate + ")");
                    }
                }
            }
            catch
            {
                hw.MotherboardModel = "标准主板";
                hw.BiosVersion = "UEFI 固件";
            }

            // 3. 内存容量
            try
            {
                var mem = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(mem))
                {
                    double totalGb = mem.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                    hw.TotalMemory = Math.Round(totalGb, 1) + " GB (物理内存)";
                }
                else
                {
                    hw.TotalMemory = "16.0 GB";
                }
            }
            catch
            {
                hw.TotalMemory = "16.0 GB";
            }

            // 4. 操作系统与安全状态
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (k != null)
                    {
                        string prod = Convert.ToString(k.GetValue("ProductName") ?? "");
                        string build = Convert.ToString(k.GetValue("CurrentBuild") ?? "");
                        string displayVer = Convert.ToString(k.GetValue("DisplayVersion") ?? "");
                        hw.OsVersion = string.Format("{0} {1} (Build {2})", prod, displayVer, build).Trim();
                    }
                }
            }
            catch
            {
                hw.OsVersion = Environment.OSVersion.VersionString;
            }

            hw.UefiStatus = status.IsUefi ? "UEFI 原生引导" : "Legacy 传统引导";
            hw.SecureBootStatus = status.SecureBootEnabled ? "已开启 (需进 BIOS 关闭)" : "已关闭 (良好)";
            hw.HagsStatus = status.IsHagsEnabled ? "已开启 (推荐)" : "已关闭";

            // 5. 枚举全机显卡资产
            hw.GpuList.Clear();
            try
            {
                const string classRoot = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
                using (var key = Registry.LocalMachine.OpenSubKey(classRoot))
                {
                    if (key != null)
                    {
                        foreach (var sub in key.GetSubKeyNames())
                        {
                            if (sub.Length != 4) continue;
                            using (var sk = key.OpenSubKey(sub))
                            {
                                if (sk == null) continue;
                                string matchId = Convert.ToString(sk.GetValue("MatchingDeviceId") ?? "");
                                string desc = Convert.ToString(sk.GetValue("DriverDesc") ?? "");
                                string drvVer = Convert.ToString(sk.GetValue("DriverVersion") ?? "");
                                if (string.IsNullOrEmpty(desc)) continue;

                                bool isMining = desc.IndexOf("CMP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                matchId.IndexOf("220D", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                matchId.IndexOf("1F0B", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                matchId.IndexOf("1E09", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                desc.IndexOf("3080", StringComparison.OrdinalIgnoreCase) >= 0;

                                string role = isMining ? "高性能 3D 渲染 / 满血算力核心" : "主板视频信号输出卡 (接显示器)";
                                string vram = "10 GB";
                                if (matchId.IndexOf("1F0B", StringComparison.OrdinalIgnoreCase) >= 0) vram = "8 GB";
                                else if (desc.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0 || desc.IndexOf("Radeon", StringComparison.OrdinalIgnoreCase) >= 0 && !isMining) vram = "共享显存";

                                hw.GpuList.Add(new GpuDeviceInfo
                                {
                                    Name = desc,
                                    PciId = matchId,
                                    DriverVersion = drvVer,
                                    MemorySize = vram,
                                    RoleTag = role,
                                    IsMiningCard = isMining
                                });
                            }
                        }
                    }
                }
            }
            catch { }

            if (hw.GpuList.Count == 0)
            {
                hw.GpuList.Add(new GpuDeviceInfo
                {
                    Name = status.GpuDesc,
                    PciId = status.GpuId,
                    DriverVersion = status.DriverVersion,
                    MemorySize = "10 GB GDDR6X",
                    RoleTag = "高性能 3D 渲染核心",
                    IsMiningCard = true
                });
            }
        }

        private static void DetectGpu(SystemStatus status)
        {
            try
            {
                const string classRoot = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
                using (var key = Registry.LocalMachine.OpenSubKey(classRoot))
                {
                    if (key != null)
                    {
                        foreach (var subName in key.GetSubKeyNames())
                        {
                            if (subName.Length != 4) continue;
                            using (var subKey = key.OpenSubKey(subName))
                            {
                                if (subKey == null) continue;
                                var matchId = Convert.ToString(subKey.GetValue("MatchingDeviceId") ?? "");
                                var desc = Convert.ToString(subKey.GetValue("DriverDesc") ?? "");
                                var drvVer = Convert.ToString(subKey.GetValue("DriverVersion") ?? "");

                                if (matchId.IndexOf("220D", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    desc.IndexOf("90HX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    (desc.IndexOf("3080", StringComparison.OrdinalIgnoreCase) >= 0 && matchId.IndexOf("10DE", StringComparison.OrdinalIgnoreCase) >= 0))
                                {
                                    status.GpuDesc = desc;
                                    status.GpuId = matchId;
                                    status.ClassSubKey = subName;
                                    status.DriverVersion = drvVer;
                                    if (desc.IndexOf("3080", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        status.IsSpoofedTo3080 = true;
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private static void DetectOverlay(SystemStatus status)
        {
            try
            {
                string[] candidates = new string[]
                {
                    @"C:\Program Files\NVIDIA Corporation\NVIDIA app\www",
                    @"C:\Program Files\NVIDIA Corporation\NVIDIA App\www",
                    @"D:\Program Files\NVIDIA Corporation\NVIDIA app\www"
                };

                string targetWww = null;
                foreach (var p in candidates)
                {
                    if (Directory.Exists(p))
                    {
                        targetWww = p;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(targetWww))
                {
                    status.NvAppInstalled = true;
                    status.NvAppPath = targetWww;

                    var files = Directory.GetFiles(targetWww, "main.*.js");
                    foreach (var f in files)
                    {
                        if (f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)) continue;
                        
                        var content = File.ReadAllText(f);
                        if (content.Contains("const ge=!0") && content.Contains("overallState:\"true\""))
                        {
                            status.OverlayPatched = true;
                        }
                        
                        if (File.Exists(f + ".bak"))
                        {
                            status.OverlayBakExists = true;
                        }
                        break;
                    }
                }
            }
            catch { }
        }

        private static void DetectPcie(SystemStatus status)
        {
            try
            {
                string desc;
                status.PcieMode = NativeCore.DetectCurrentPcieUnlockMode(out desc);
                status.PcieModeDesc = desc;
            }
            catch
            {
                status.PcieModeDesc = "未检测到引导项";
            }
        }

        private static void DetectSystemSecurity(SystemStatus status)
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\SecureBoot\State"))
                {
                    if (key != null)
                    {
                        int secVal = Convert.ToInt32(key.GetValue("UEFISecureBootEnabled") ?? 0);
                        status.SecureBootEnabled = (secVal == 1);
                    }
                }
            }
            catch { }

            try
            {
                status.IsHagsEnabled = NativeCore.IsHagsEnabled();
            }
            catch { }
        }

        public static void DetectSensors(SystemStatus status)
        {
            try
            {
                status.Sensors = NativeCore.QueryGpuSensors();
            }
            catch { }
        }
    }
}
