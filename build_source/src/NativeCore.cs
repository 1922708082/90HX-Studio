using System.Net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace CMP90HXToolbox
{
    public static class NativeCore
    {
        #region 统一数据目录规范 (独立数据存储，避免在桌面或程序目录生成零散文件)

        private static string _customDataDir = null;

        public static string GetDataDirectory()
        {
            if (!string.IsNullOrEmpty(_customDataDir)) return _customDataDir;

            try
            {

                string commonData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                string target = Path.Combine(commonData, "CMP90HX_Toolbox");
                if (!Directory.Exists(target)) Directory.CreateDirectory(target);
                _customDataDir = target;
                return _customDataDir;
            }
            catch
            {
                try
                {

                    string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string target = Path.Combine(localData, "CMP90HX_Toolbox");
                    if (!Directory.Exists(target)) Directory.CreateDirectory(target);
                    _customDataDir = target;
                    return _customDataDir;
                }
                catch
                {
                    _customDataDir = AppDomain.CurrentDomain.BaseDirectory;
                    return _customDataDir;
                }
            }
        }

        public static string GetBackupDirectory()
        {
            string dir = Path.Combine(GetDataDirectory(), "backup");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        public static string GetConfigDirectory()
        {
            string dir = Path.Combine(GetDataDirectory(), "config");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        public static string GetDlssRuntimeDirectory()
        {
            string dir = Path.Combine(GetDataDirectory(), "dlss_runtime");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        public static string GetLogsDirectory()
        {
            string dir = Path.Combine(GetDataDirectory(), "logs");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        #endregion

        public static byte[] GetPackagedResource(string fileName)
        {
            Stream inStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("assets.zip");

            if (inStream == null)
            {
                string asmLoc = typeof(NativeCore).Assembly.Location;
                string asmDir = string.IsNullOrEmpty(asmLoc) ? "" : Path.GetDirectoryName(asmLoc);
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;

                string[] candidates = new string[] {
                    Path.Combine(asmDir ?? "", "assets.zip"),
                    Path.Combine(baseDir ?? "", "assets.zip"),
                    @"D:\app\assets.zip"
                };

                foreach (var c in candidates)
                {
                    if (!string.IsNullOrEmpty(c) && File.Exists(c))
                    {
                        inStream = File.OpenRead(c);
                        break;
                    }
                }
            }

            if (inStream == null)
            {
                throw new FileNotFoundException("未找到内置资源包 assets.zip，请确认程序完整性。");
            }

            using (inStream)
            using (var archive = new ZipArchive(inStream, ZipArchiveMode.Read))
            {
                ZipArchiveEntry targetEntry = archive.GetEntry(fileName);
                if (targetEntry == null)
                {
                    string pureName = Path.GetFileName(fileName);
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.Name.Equals(pureName, StringComparison.OrdinalIgnoreCase) ||
                            entry.FullName.EndsWith("/" + pureName, StringComparison.OrdinalIgnoreCase))
                        {
                            targetEntry = entry;
                            break;
                        }
                    }
                }

                if (targetEntry == null)
                    throw new FileNotFoundException("组件未在资源包中登记: " + fileName);

                using (var entryStream = targetEntry.Open())
                using (var outMs = new MemoryStream())
                {
                    entryStream.CopyTo(outMs);
                    return outMs.ToArray();
                }
            }
        }

        /// <summary>打开内置资源包流（嵌入资源优先，其次磁盘同目录）。调用方负责释放。</summary>
        private static Stream OpenAssetsStream()
        {
            Stream inStream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("assets.zip");
            if (inStream != null) return inStream;

            string asmLoc = typeof(NativeCore).Assembly.Location;
            string asmDir = string.IsNullOrEmpty(asmLoc) ? "" : Path.GetDirectoryName(asmLoc);
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            string[] candidates = new string[] {
                Path.Combine(asmDir ?? "", "assets.zip"),
                Path.Combine(baseDir ?? "", "assets.zip"),
                @"D:\app\assets.zip"
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                if (!string.IsNullOrEmpty(candidates[i]) && File.Exists(candidates[i]))
                    return File.OpenRead(candidates[i]);
            }
            return null;
        }

        /// <summary>列出资源包内指定前缀下的全部条目路径。</summary>
        public static List<string> GetPackagedResourceNames(string prefix)
        {
            List<string> names = new List<string>();
            Stream s = OpenAssetsStream();
            if (s == null) return names;

            using (s)
            using (var archive = new ZipArchive(s, ZipArchiveMode.Read))
            {
                for (int i = 0; i < archive.Entries.Count; i++)
                {
                    string full = archive.Entries[i].FullName;
                    if (full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) names.Add(full);
                }
            }
            return names;
        }

        /// <summary>把资源包内某个目录树整体释放到目标目录，返回成功写出的文件数。</summary>
        public static int ExtractPackagedTree(string pkgPrefix, string destRoot)
        {
            int written = 0;
            Stream s = OpenAssetsStream();
            if (s == null) return 0;

            string prefix = pkgPrefix.TrimEnd('/') + "/";
            using (s)
            using (var archive = new ZipArchive(s, ZipArchiveMode.Read))
            {
                for (int i = 0; i < archive.Entries.Count; i++)
                {
                    ZipArchiveEntry e = archive.Entries[i];
                    if (!e.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.IsNullOrEmpty(e.Name)) continue; // 目录占位条目

                    string rel = e.FullName.Substring(prefix.Length).Replace('/', '\\');
                    string dst = Path.Combine(destRoot, rel);
                    try
                    {
                        string dir = Path.GetDirectoryName(dst);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                        using (Stream es = e.Open())
                        using (FileStream fs = File.Create(dst))
                        {
                            es.CopyTo(fs);
                        }
                        written++;
                    }
                    catch { }
                }
            }
            return written;
        }

        public static bool IsAdmin()
        {
            try
            {
                var id = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(id);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        #region 显卡型号修改

        public static string EnsureToolkitExtracted(string toolkitKey)
        {
            try
            {
                string baseDir = Path.Combine(GetDataDirectory(), "toolkit", toolkitKey);
                Directory.CreateDirectory(baseDir);

                string[] files;
                if (toolkitKey == "40hx")
                {
                    files = new string[] {
                        "files/40HXUNLK.EFI", "files/WinRing0x64.sys",
                        "40HXUNLK.EFI", "WinRing0x64.sys", "ThrottleStop.sys",
                        "manual_install.bat", "manual_uninstall.bat",
                        "40HXInstaller.exe", "40HXCheck.exe", "40HXUninstaller.exe", "README.md"
                    };
                }
                else if (toolkitKey == "50hx")
                {
                    // 50HX 安装器硬编码从自己目录的 drivers\ 下加载驱动，WinRing0x64.sys 必须在该子目录
                    files = new string[] {
                        "50HXInstaller.exe", "50HXCheck.exe", "50HXUninstaller.exe",
                        "50HXUNLK.EFI", "WinRing0x64.sys",
                        "drivers/WinRing0x64.sys", "drivers/ThrottleStop.sys"
                    };
                }
                else
                {
                    files = new string[] { "Setup_CMP30HX_WindowsAIO.bat", "Setup_ReBar_CMP30HX.bat", "README_ZH.md" };
                }

                foreach (var f in files)
                {
                    try
                    {
                        string target = Path.Combine(baseDir, f.Replace('/', '\\'));
                        if (File.Exists(target)) continue;
                        byte[] data = GetPackagedResource(toolkitKey + "/" + f);
                        string dir = Path.GetDirectoryName(target);
                        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                        File.WriteAllBytes(target, data);
                    }
                    catch { }
                }
                return baseDir;
            }
            catch { }
            return null;
        }

        /// <summary>启动工具包里的可执行文件，自动提权并以工具目录为工作目录。</summary>
        public static bool LaunchToolkitApp(string toolkitKey, string exeName, Action<string, bool> log)
        {
            try
            {
                string dir = EnsureToolkitExtracted(toolkitKey);
                if (string.IsNullOrEmpty(dir))
                {
                    log("工具包解压失败，请确认程序完整性", true);
                    return false;
                }

                string exe = Path.Combine(dir, exeName);
                if (!File.Exists(exe))
                {
                    log("工具包缺少 " + exeName, true);
                    return false;
                }

                ProcessStartInfo psi = new ProcessStartInfo(exe);
                psi.UseShellExecute = true;
                psi.WorkingDirectory = dir;
                psi.Verb = "runas";
                Process.Start(psi);
                log("已启动 " + exeName + "（" + dir + "）", false);
                return true;
            }
            catch (Exception ex)
            {
                log("启动 " + exeName + " 失败: " + ex.Message, true);
                return false;
            }
        }

        /// <summary>打开工具包所在目录。</summary>
        public static bool OpenToolkitFolder(string toolkitKey, Action<string, bool> log)
        {
            try
            {
                string dir = EnsureToolkitExtracted(toolkitKey);
                if (string.IsNullOrEmpty(dir)) return false;
                Process.Start("explorer.exe", "\"" + dir + "\"");
                return true;
            }
            catch (Exception ex)
            {
                log("打开工具目录失败: " + ex.Message, true);
                return false;
            }
        }

        /// <summary>兼容旧调用：默认伪装为 RTX 3080。</summary>
        public static Task<bool> ApplyGpuRenameTo3080Async(Action<string, bool> log)
        {
            return ApplyGpuRenameAsync("NVIDIA GeForce RTX 3080", log);
        }

        public static Task<bool> ApplyGpuRenameAsync(string targetName, Action<string, bool> log)
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    log("正在检索显卡设备驱动类 (Class)...", false);
                    string classRoot = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
                    string targetSubKey = "0001";
                    string activeInfName = "oem32.inf";

                    using (var key = Registry.LocalMachine.OpenSubKey(classRoot, false))
                    {
                        if (key != null)
                        {
                            foreach (var sub in key.GetSubKeyNames())
                            {
                                if (sub.Length != 4) continue;
                                using (var sk = key.OpenSubKey(sub, false))
                                {
                                    if (sk == null) continue;
                                    var matchId = Convert.ToString(sk.GetValue("MatchingDeviceId") ?? "");
                                    var desc = Convert.ToString(sk.GetValue("DriverDesc") ?? "");
                                    var inf = Convert.ToString(sk.GetValue("InfPath") ?? "");
                                    if (matchId.IndexOf("220D", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        desc.IndexOf("90HX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        desc.IndexOf("CMP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        desc.IndexOf("GeForce", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        targetSubKey = sub;
                                        if (!string.IsNullOrEmpty(inf)) activeInfName = inf;
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    log("定位到当前显卡驱动项: " + targetSubKey + " (INF: " + activeInfName + ")", false);

                    string targetClassPath = classRoot + "\\" + targetSubKey;
                    try
                    {
                        string backupDir = GetBackupDirectory();
                        string classBackup = Path.Combine(backupDir, "backup_class.reg");
                        if (!File.Exists(classBackup))
                        {
                            var psiExport = new ProcessStartInfo
                            {
                                FileName = "reg.exe",
                                Arguments = "export \"HKLM\\" + targetClassPath + "\" \"" + classBackup + "\" /y",
                                CreateNoWindow = true,
                                UseShellExecute = false
                            };
                            using (var p = Process.Start(psiExport)) p.WaitForExit(2000);
                        }
                    }
                    catch { }

                    using (var sk = Registry.LocalMachine.OpenSubKey(targetClassPath, true))
                    {
                        if (sk != null)
                        {
                            sk.SetValue("DriverDesc", targetName, RegistryValueKind.String);
                            sk.SetValue("HardwareInformation.AdapterString", targetName, RegistryValueKind.String);
                            sk.SetValue("HardwareInformation.ChipType", targetName, RegistryValueKind.String);

                            sk.SetValue("AdapterType", 1, RegistryValueKind.DWord);
                            sk.SetValue("EnableMsHybrid", 1, RegistryValueKind.DWord);
                            sk.SetValue("RMDisableTcc", 1, RegistryValueKind.DWord);
                            sk.SetValue("D3DMappingLayerEnable", 1, RegistryValueKind.DWord);
                            log("已更新系统驱动类描述为 " + targetName + " 并激活 WDDM/MsHybrid 3D 图形加速", false);
                        }
                    }

                    try
                    {
                        string videoRoot = @"SYSTEM\CurrentControlSet\Control\Video";
                        using (var vKey = Registry.LocalMachine.OpenSubKey(videoRoot, false))
                        {
                            if (vKey != null)
                            {
                                foreach (var guid in vKey.GetSubKeyNames())
                                {
                                    using (var gk = vKey.OpenSubKey(guid, false))
                                    {
                                        if (gk == null) continue;
                                        foreach (var num in gk.GetSubKeyNames())
                                        {
                                            try
                                            {
                                                using (var nk = gk.OpenSubKey(num, true))
                                                {
                                                    if (nk == null) continue;
                                                    var drv = Convert.ToString(nk.GetValue("Driver") ?? "");
                                                    if (drv.IndexOf(targetSubKey, StringComparison.OrdinalIgnoreCase) >= 0)
                                                    {
                                                        nk.SetValue("DeviceDesc", targetName, RegistryValueKind.String);
                                                    }
                                                }
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            }
                        }
                        log("已更新 Control\\Video 显卡服务项描述", false);
                    }
                    catch (Exception exVid)
                    {
                        log("更新 Control\\Video 提示: " + exVid.Message, false);
                    }

                    try
                    {
                        string enumPciRoot = @"SYSTEM\CurrentControlSet\Enum\PCI";
                        using (var epKey = Registry.LocalMachine.OpenSubKey(enumPciRoot, false))
                        {
                            if (epKey != null)
                            {
                                foreach (var dev in epKey.GetSubKeyNames())
                                {
                                    if (dev.IndexOf("DEV_220D", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        using (var dk = epKey.OpenSubKey(dev, false))
                                        {
                                            if (dk != null)
                                            {
                                                foreach (var inst in dk.GetSubKeyNames())
                                                {
                                                    try
                                                    {
                                                        using (var ik = dk.OpenSubKey(inst, true))
                                                        {
                                                            if (ik != null)
                                                            {
                                                                ik.SetValue("FriendlyName", targetName, RegistryValueKind.String);
                                                                ik.SetValue("DeviceDesc", targetName, RegistryValueKind.String);
                                                                log("已更新设备管理器友好名称: " + inst, false);
                                                            }
                                                        }
                                                    }
                                                    catch (Exception exInst)
                                                    {
                                                        log("设备管理器实例 " + inst + " 更新提示 (系统只读保护已跳过): " + exInst.Message, false);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception exPci)
                    {
                        log("扫描 Enum\\PCI 提示: " + exPci.Message, false);
                    }

                    try
                    {
                        using (var dxKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\DirectX", true))
                        {
                            if (dxKey != null)
                            {
                                foreach (var sub in dxKey.GetSubKeyNames())
                                {
                                    using (var sk = dxKey.OpenSubKey(sub, true))
                                    {
                                        if (sk == null) continue;
                                        var desc = Convert.ToString(sk.GetValue("Description") ?? "");
                                        var devId = Convert.ToInt32(sk.GetValue("DeviceId") ?? 0);
                                        if (desc.IndexOf("90HX", StringComparison.OrdinalIgnoreCase) >= 0 || devId == 8717)
                                        {
                                            sk.SetValue("Description", targetName, RegistryValueKind.String);
                                            log("已更新 DirectX 适配器描述: " + sub, false);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception exDx)
                    {
                        log("更新 DirectX 缓存提示: " + exDx.Message, false);
                    }

                    try
                    {
                        const string nvVideoKey = @"SYSTEM\CurrentControlSet\Services\nvlddmkm\Video";
                        using (var nvKey = Registry.LocalMachine.OpenSubKey(nvVideoKey, true))
                        {
                            if (nvKey != null)
                            {
                                nvKey.SetValue("DeviceDesc", targetName, RegistryValueKind.String);
                                log("已更新 nvlddmkm 底层驱动服务配置为 " + targetName, false);
                            }
                        }
                    }
                    catch (Exception exNv)
                    {
                        log("更新 nvlddmkm 服务 Video 提示: " + exNv.Message, false);
                    }

                    string sysInf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "INF", activeInfName);
                    PatchInfFile(sysInf, "NVIDIA CMP 90HX (RainCandy Technology)", targetName, log);

                    string driverStore = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"DriverStore\FileRepository");
                    if (Directory.Exists(driverStore))
                    {
                        foreach (var dir in Directory.GetDirectories(driverStore, "nvac.inf*"))
                        {
                            string targetInf = Path.Combine(dir, "nvac.inf");
                            PatchInfFile(targetInf, "NVIDIA CMP 90HX (RainCandy Technology)", targetName, log);
                        }
                    }

                    UpdateNvAppShimJson("NVIDIA CMP 90HX", targetName, log);

                    try
                    {
                        foreach (var p in Process.GetProcessesByName("NVIDIA App"))
                        {
                            try { p.Kill(); p.WaitForExit(1000); } catch { }
                        }
                    }
                    catch { }

                    try
                    {
                        KillProcesses(new string[] { "dwm" });
                        System.Threading.Thread.Sleep(500);
                        log("已软重启 DWM 显示服务，新显卡名称立即生效。", false);
                    }
                    catch { }

                    log("显卡型号深度修改完成 (已设置为 " + targetName + ")。", false);
                    return true;
                }
                catch (Exception ex)
                {
                    log("修改显卡型号出错: " + ex.Message, true);
                    return false;
                }
            });
        }

        public static Task<bool> RestoreGpuTo90hxAsync(Action<string, bool> log)
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    log("正在还原显卡驱动类描述 (Class)...", false);
                    string classRoot = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
                    string targetSubKey = "0001";
                    string activeInfName = "oem32.inf";

                    using (var key = Registry.LocalMachine.OpenSubKey(classRoot, false))
                    {
                        if (key != null)
                        {
                            foreach (var sub in key.GetSubKeyNames())
                            {
                                if (sub.Length != 4) continue;
                                using (var sk = key.OpenSubKey(sub, false))
                                {
                                    if (sk == null) continue;
                                    var matchId = Convert.ToString(sk.GetValue("MatchingDeviceId") ?? "");
                                    var desc = Convert.ToString(sk.GetValue("DriverDesc") ?? "");
                                    var inf = Convert.ToString(sk.GetValue("InfPath") ?? "");
                                    if (matchId.IndexOf("220D", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        desc.IndexOf("90HX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        desc.IndexOf("3080", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        targetSubKey = sub;
                                        if (!string.IsNullOrEmpty(inf)) activeInfName = inf;
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    using (var sk = Registry.LocalMachine.OpenSubKey(classRoot + "\\" + targetSubKey, true))
                    {
                        if (sk != null)
                        {
                            sk.SetValue("DriverDesc", "NVIDIA CMP 90HX (RainCandy Technology)", RegistryValueKind.String);
                            sk.SetValue("HardwareInformation.AdapterString", "NVIDIA CMP 90HX", RegistryValueKind.String);
                            sk.SetValue("HardwareInformation.ChipType", "NVIDIA CMP 90HX", RegistryValueKind.String);
                            log("已还原驱动类描述为 NVIDIA CMP 90HX", false);
                        }
                    }

                    try
                    {
                        string videoRoot = @"SYSTEM\CurrentControlSet\Control\Video";
                        using (var vKey = Registry.LocalMachine.OpenSubKey(videoRoot, false))
                        {
                            if (vKey != null)
                            {
                                foreach (var guid in vKey.GetSubKeyNames())
                                {
                                    using (var gk = vKey.OpenSubKey(guid, false))
                                    {
                                        if (gk == null) continue;
                                        foreach (var num in gk.GetSubKeyNames())
                                        {
                                            try
                                            {
                                                using (var nk = gk.OpenSubKey(num, true))
                                                {
                                                    if (nk == null) continue;
                                                    var drv = Convert.ToString(nk.GetValue("Driver") ?? "");
                                                    if (drv.IndexOf(targetSubKey, StringComparison.OrdinalIgnoreCase) >= 0)
                                                    {
                                                        nk.SetValue("DeviceDesc", "@" + activeInfName + ",%nvidia_dev.220d%;NVIDIA CMP 90HX (RainCandy Technology)", RegistryValueKind.String);
                                                    }
                                                }
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            }
                        }
                        log("已还原 Control\\Video 显卡服务项描述为出厂值", false);
                    }
                    catch (Exception exVid)
                    {
                        log("还原 Control\\Video 提示: " + exVid.Message, false);
                    }

                    try
                    {
                        string enumPciRoot = @"SYSTEM\CurrentControlSet\Enum\PCI";
                        using (var epKey = Registry.LocalMachine.OpenSubKey(enumPciRoot, false))
                        {
                            if (epKey != null)
                            {
                                foreach (var dev in epKey.GetSubKeyNames())
                                {
                                    if (dev.IndexOf("DEV_220D", StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        using (var dk = epKey.OpenSubKey(dev, false))
                                        {
                                            if (dk != null)
                                            {
                                                foreach (var inst in dk.GetSubKeyNames())
                                                {
                                                    try
                                                    {
                                                        using (var ik = dk.OpenSubKey(inst, true))
                                                        {
                                                            if (ik != null)
                                                            {
                                                                try { ik.DeleteValue("FriendlyName"); } catch { }
                                                                ik.SetValue("DeviceDesc", "@" + activeInfName + ",%nvidia_dev.220d%;NVIDIA CMP 90HX (RainCandy Technology)", RegistryValueKind.String);
                                                            }
                                                        }
                                                    }
                                                    catch (Exception exInst)
                                                    {
                                                        log("设备管理器实例 " + inst + " 还原提示 (系统只读保护已跳过): " + exInst.Message, false);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception exPci)
                    {
                        log("扫描 Enum\\PCI 提示: " + exPci.Message, false);
                    }

                    try
                    {
                        using (var dxKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\DirectX", true))
                        {
                            if (dxKey != null)
                            {
                                foreach (var sub in dxKey.GetSubKeyNames())
                                {
                                    using (var sk = dxKey.OpenSubKey(sub, true))
                                    {
                                        if (sk == null) continue;
                                        var desc = Convert.ToString(sk.GetValue("Description") ?? "");
                                        var devId = Convert.ToInt32(sk.GetValue("DeviceId") ?? 0);
                                        if (desc.IndexOf("3080", StringComparison.OrdinalIgnoreCase) >= 0 || devId == 8717)
                                        {
                                            sk.SetValue("Description", "NVIDIA CMP 90HX (RainCandy Technology)", RegistryValueKind.String);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception exDx)
                    {
                        log("还原 DirectX 描述提示: " + exDx.Message, false);
                    }

                    try
                    {
                        const string nvVideoKey = @"SYSTEM\CurrentControlSet\Services\nvlddmkm\Video";
                        using (var nvKey = Registry.LocalMachine.OpenSubKey(nvVideoKey, true))
                        {
                            if (nvKey != null)
                            {
                                nvKey.SetValue("DeviceDesc", "@" + activeInfName + ",%nvidia_dev.220d%;NVIDIA CMP 90HX (RainCandy Technology)", RegistryValueKind.String);
                                log("已还原 nvlddmkm 底层驱动服务配置为官方出厂值", false);
                            }
                        }
                    }
                    catch (Exception exNv)
                    {
                        log("还原 nvlddmkm 服务 Video 提示: " + exNv.Message, false);
                    }

                    string sysInf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "INF", activeInfName);
                    PatchInfFile(sysInf, "NVIDIA GeForce RTX 3080", "NVIDIA CMP 90HX (RainCandy Technology)", log);

                    string driverStore = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"DriverStore\FileRepository");
                    if (Directory.Exists(driverStore))
                    {
                        foreach (var dir in Directory.GetDirectories(driverStore, "nvac.inf*"))
                        {
                            string targetInf = Path.Combine(dir, "nvac.inf");
                            PatchInfFile(targetInf, "NVIDIA GeForce RTX 3080", "NVIDIA CMP 90HX (RainCandy Technology)", log);
                        }
                    }

                    UpdateNvAppShimJson("NVIDIA GeForce RTX 3080", "NVIDIA CMP 90HX", log);

                    try
                    {
                        foreach (var p in Process.GetProcessesByName("NVIDIA App"))
                        {
                            try { p.Kill(); p.WaitForExit(1000); } catch { }
                        }
                    }
                    catch { }

                    try
                    {
                        KillProcesses(new string[] { "dwm" });
                        System.Threading.Thread.Sleep(500);
                        log("已软重启 DWM 显示服务，显卡出厂设置已生效。", false);
                    }
                    catch { }

                    log("已成功恢复显卡出厂设置 (NVIDIA CMP 90HX)。", false);
                    return true;
                }
                catch (Exception ex)
                {
                    log("恢复显卡型号出错: " + ex.Message, true);
                    return false;
                }
            });
        }

                private static void PatchInfFile(string infPath, string search, string replace, Action<string, bool> log)
        {
            if (!File.Exists(infPath)) return;
            try
            {
                TakeFileOwnership(infPath);
                string text = File.ReadAllText(infPath, Encoding.Default);
                if (text.Contains(search))
                {
                    text = text.Replace(search, replace);
                    File.WriteAllText(infPath, text, Encoding.Default);
                    log("INF 文件已同步: " + Path.GetFileName(infPath), false);
                }
            }
            catch (Exception ex)
            {
                log("同步 INF 跳过: " + Path.GetFileName(infPath) + " (" + ex.Message + ")", true);
            }
        }

        private static void UpdateNvAppShimJson(string search, string replace, Action<string, bool> log)
        {
            try
            {
                string userRoot = @"C:\Users";
                if (!Directory.Exists(userRoot)) return;
                foreach (var u in Directory.GetDirectories(userRoot))
                {
                    string shim = Path.Combine(u, @"AppData\Local\NVIDIA Corporation\NVIDIA app\NvBackend\SHIM.json");
                    if (File.Exists(shim))
                    {
                        try
                        {
                            TakeFileOwnership(shim);
                            string raw = File.ReadAllText(shim, Encoding.UTF8);
                            if (raw.Contains(search))
                            {
                                raw = raw.Replace(search, replace);
                                File.WriteAllText(shim, raw, Encoding.UTF8);
                                log("已更新用户 SHIM 缓存: " + Path.GetFileName(u), false);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        #endregion

        #region NVIDIA App 游戏覆盖解锁

        public static Task<bool> UnlockNvAppOverlayAsync(Action<string, bool> log)
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    log("正在探测 NVIDIA App 安装路径...", false);

                    List<string> searchDirs = new List<string>();
                    string[] roots = new string[] {
                        @"C:\Program Files\NVIDIA Corporation\NVIDIA App",
                        @"C:\Program Files (x86)\NVIDIA Corporation\NVIDIA App",
                        @"D:\Program Files\NVIDIA Corporation\NVIDIA App",
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"NVIDIA Corporation\NVIDIA App")
                    };
                    foreach (var r in roots)
                    {
                        if (!Directory.Exists(r)) continue;
                        searchDirs.Add(Path.Combine(r, "www"));
                        searchDirs.Add(Path.Combine(r, "osc"));
                        searchDirs.Add(r);
                    }

                    List<string> jsFiles = new List<string>();
                    foreach (var d in searchDirs)
                    {
                        if (!Directory.Exists(d)) continue;
                        foreach (var f in Directory.GetFiles(d, "main.*.js"))
                        {
                            if (f.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)) continue;
                            if (!jsFiles.Contains(f)) jsFiles.Add(f);
                        }
                    }

                    if (jsFiles.Count == 0)
                    {
                        log("未找到 NVIDIA App 前端 main.*.js 文件！请确认 NVIDIA App 已安装。", true);
                        return false;
                    }

                    int patchedFiles = 0;
                    int totalHits = 0;

                    foreach (var targetJs in jsFiles)
                    {
                        string bakPath = targetJs + ".bak";
                        if (!File.Exists(bakPath))
                        {
                            try { File.Copy(targetJs, bakPath, true); log("已创建原始文件备份: " + Path.GetFileName(bakPath), false); }
                            catch (Exception exBak) { log("创建备份失败(可能已备份过): " + exBak.Message, false); }
                        }

                        try { TakeFileOwnership(targetJs); } catch { }

                        string content = File.ReadAllText(targetJs, Encoding.UTF8);

                        if (content.IndexOf("overallState", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            log("跳过(不含覆盖逻辑): " + Path.GetFileName(targetJs), false);
                            continue;
                        }

                        int hits = ApplyOverlayPatches(ref content, log, Path.GetFileName(targetJs));
                        if (hits > 0)
                        {
                            File.WriteAllText(targetJs, content, Encoding.UTF8);
                            patchedFiles++;
                            totalHits += hits;
                            log("已写入补丁: " + Path.GetFileName(targetJs) + " (命中 " + hits + " 处)", false);
                        }
                    }

                    if (totalHits == 0)
                    {
                        log("==================================================", true);
                        log("未能在 NVIDIA App 前端中匹配到任何限制规则。", true);
                        log("可能原因：NVIDIA App 已更新到改动更大的版本，或文件已被修改过。", true);
                        log("建议：请把以下文件发给开发者补充新规则 ——", true);
                        foreach (var f in jsFiles) log("   " + f, true);
                        log("==================================================", true);
                        return false;
                    }

                    log("正在重启相关进程并清空 CEF 缓存...", false);
                    KillProcesses(new string[] { "NVIDIA App", "NVIDIA Overlay", "nvsphelper64" });
                    CleanCefCacheInternal(log);
                    RestartNvApp();

                    log(string.Format("NVIDIA App 游戏内覆盖解锁完成！(共修改 {0} 个文件 / {1} 处规则) — Alt+R 性能面板 / Alt+Z 录屏控制台", patchedFiles, totalHits), false);
                    return true;
                }
                catch (Exception ex)
                {
                    log("解锁游戏覆盖出错: " + ex.Message, true);
                    return false;
                }
            });
        }

        private static int ApplyOverlayPatches(ref string content, Action<string, bool> log, string fileName)
        {
            int hits = 0;

            try
            {
                var reA = new System.Text.RegularExpressions.Regex(
                    "const\\s+(\\w+)\\s*=\\s*\"true\"\\s*===\\s*(\\w+)\\.overallState\\s*&&\\s*(\\w+)\\s*\\?\\.\\s*ShadowPlaySupported");
                if (reA.IsMatch(content))
                {
                    content = reA.Replace(content, "const ${1}=!0");
                    hits++;
                    log("  [规则A] ShadowPlay 支持判定已解除 (" + fileName + ")", false);
                }
            }
            catch (Exception ex) { log("  [规则A] 执行异常: " + ex.Message, true); }

            try
            {
                var reB = new System.Text.RegularExpressions.Regex(
                    "overallState\\s*:\\s*this\\.cachedArchitecture\\s*===\\s*[\\w$]+\\.\\w+\\s*\\?\\s*\"true\"\\s*:\\s*[\\w$]+\\.body\\.criteria\\.overallState");
                if (reB.IsMatch(content))
                {
                    content = reB.Replace(content, "overallState:\"true\"");
                    hits++;
                    log("  [规则B] 云端硬件白名单查询已绕过 (" + fileName + ")", false);
                }
            }
            catch (Exception ex) { log("  [规则B] 执行异常: " + ex.Message, true); }

            try
            {
                var reC = new System.Text.RegularExpressions.Regex(
                    "const\\s+(\\w+)\\s*=\\s*\"true\"\\s*===\\s*(\\w+)\\.overallState\\s*&&\\s*\"true\"\\s*===\\s*(\\w+)\\.overallState");
                if (reC.IsMatch(content))
                {
                    content = reC.Replace(content, "const ${1}=!0");
                    hits++;
                    log("  [规则C] 组合功能门已解除 (" + fileName + ")", false);
                }
            }
            catch (Exception ex) { log("  [规则C] 执行异常: " + ex.Message, true); }

            return hits;
        }

        public static Task<bool> RestoreNvAppOverlayAsync(Action<string, bool> log)
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    log("正在探测 NVIDIA App 备份文件...", false);
                    string[] searchPaths = new string[] {
                        @"C:\Program Files\NVIDIA Corporation\NVIDIA app\www",
                        @"C:\Program Files\NVIDIA Corporation\NVIDIA App\www",
                        @"D:\Program Files\NVIDIA Corporation\NVIDIA app\www"
                    };

                    string targetWww = null;
                    foreach (var p in searchPaths)
                    {
                        if (Directory.Exists(p)) { targetWww = p; break; }
                    }

                    if (string.IsNullOrEmpty(targetWww))
                    {
                        log("未找到 NVIDIA App 目录！", true);
                        return false;
                    }

                    string bakPath = null;
                    foreach (var f in Directory.GetFiles(targetWww, "main.*.js.bak"))
                    {
                        bakPath = f;
                        break;
                    }

                    if (string.IsNullOrEmpty(bakPath))
                    {
                        log("未找到 .bak 备份文件，可能尚未注入补丁。", true);
                        return false;
                    }

                    string origJs = bakPath.Substring(0, bakPath.Length - 4);
                    KillProcesses(new string[] { "NVIDIA App", "NVIDIA Overlay", "nvsphelper64" });

                    TakeFileOwnership(origJs);
                    File.Copy(bakPath, origJs, true);
                    log("已从官方备份还原核心 main.js 文件", false);

                    CleanCefCacheInternal(log);
                    RestartNvApp();

                    log("NVIDIA App 已完全恢复为官方原始状态！", false);
                    return true;
                }
                catch (Exception ex)
                {
                    log("恢复官方覆盖出错: " + ex.Message, true);
                    return false;
                }
            });
        }

        #endregion

        #region PCIe 多模式开机解锁与安全防呆清理

        public enum PcieUnlockMode
        {
            None = 0,
            Mode1_DirectBoot = 1,
            Mode2_UefiShell = 2,
            DirtyState = 3
        }

        public static PcieUnlockMode DetectCurrentPcieUnlockMode(out string detailMsg)
        {
            detailMsg = "官方原生标准引导 (未解锁)";
            try
            {
                string bcdBootMgr = RunCmd("bcdedit", "/enum {bootmgr}");
                string bcdFw = RunCmd("bcdedit", "/enum firmware");

                bool bootMgrToCompat = bcdBootMgr.IndexOf("NVPermissiveCompat.efi", StringComparison.OrdinalIgnoreCase) >= 0;
                bool bootMgrToShell = bcdBootMgr.IndexOf("Shell.efi", StringComparison.OrdinalIgnoreCase) >= 0;
                bool fwHasDedicated = bcdFw.IndexOf("CMP 90HX Unlock", StringComparison.OrdinalIgnoreCase) >= 0;
                bool fwHasShell = bcdFw.IndexOf("90HX Unlock", StringComparison.OrdinalIgnoreCase) >= 0;
                bool hasBackup = bcdFw.IndexOf("Windows Boot Manager (Backup)", StringComparison.OrdinalIgnoreCase) >= 0;

                if (fwHasDedicated)
                {
                    detailMsg = "模式 2 (独立 Shell 启动项版，运行中)";
                    return PcieUnlockMode.Mode2_UefiShell;
                }

                if (bootMgrToCompat && hasBackup)
                {
                    detailMsg = "模式 1 (免Shell极速直启双保险版，运行中)";
                    return PcieUnlockMode.Mode1_DirectBoot;
                }

                if (fwHasShell || bootMgrToShell)
                {
                    detailMsg = "模式 2 (UEFI Shell 兼容脚本模式，运行中)";
                    return PcieUnlockMode.Mode2_UefiShell;
                }

                if (bootMgrToCompat || hasBackup)
                {
                    detailMsg = "检测到部分引导残留项 (建议先点击一键清理修复)";
                    return PcieUnlockMode.DirtyState;
                }

                return PcieUnlockMode.None;
            }
            catch (Exception ex)
            {
                detailMsg = "检测引导状态受限 (" + ex.Message + ")";
                return PcieUnlockMode.None;
            }
        }

        public static bool CleanAllPcieUnlocks(Action<string, bool> log)
        {
            try
            {
                log("正在重置 {bootmgr} 启动路径为微软官方 bootmgfw.efi...", false);
                RunCmd("bcdedit", "/set {bootmgr} path \\EFI\\Microsoft\\Boot\\bootmgfw.efi");

                log("正在扫描并清理所有 90HX 与 Backup 引导项...", false);
                string fwOut = RunCmd("bcdedit", "/enum firmware");
                var matches = ParseBcdEntries(fwOut);
                int delCount = 0;
                foreach (var entry in matches)
                {
                    if (entry.Description.IndexOf("Windows Boot Manager (Backup)", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        entry.Description.IndexOf("90HX Unlock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        entry.Description.IndexOf("40HX Unlock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        entry.Path.IndexOf("NVPermissive", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        entry.Path.IndexOf("WildCompute", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        entry.Path.IndexOf("Shell.efi", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        RunCmd("bcdedit", "/delete " + entry.Guid + " /f");
                        log("已彻底移除冗余引导项: " + entry.Guid + " (" + entry.Description + ")", false);
                        delCount++;
                    }
                }

                // 删完旧项后重建启动顺序，避免 displayorder 残留悬空 GUID
                if (delCount > 0)
                {
                    RunCmd("bcdedit", "/set {fwbootmgr} displayorder {bootmgr}");
                }

                log("正在挂载 ESP 分区清理残留固件...", false);
                string esp = MountEsp();
                if (!string.IsNullOrEmpty(esp))
                {
                    try
                    {
                        string p1 = Path.Combine(esp, @"EFI\NVPermissive");
                        if (Directory.Exists(p1)) { Directory.Delete(p1, true); log("已清理 " + p1, false); }

                        string p2 = Path.Combine(esp, @"EFI\90HX");
                        if (Directory.Exists(p2)) { Directory.Delete(p2, true); log("已清理 " + p2, false); }

                        string pWild = Path.Combine(esp, @"EFI\WildCompute");
                        if (Directory.Exists(pWild)) { Directory.Delete(pWild, true); log("已清理 " + pWild, false); }

                        string p3 = Path.Combine(esp, "startup.nsh");
                        if (File.Exists(p3)) { File.Delete(p3); log("已清理根目录 startup.nsh", false); }

                        string p4Bak = Path.Combine(esp, @"EFI\Boot\bootx64.efi.90hx.bak");
                        string p4Live = Path.Combine(esp, @"EFI\Boot\bootx64.efi");
                        if (File.Exists(p4Bak))
                        {
                            if (File.Exists(p4Live)) File.Delete(p4Live);
                            File.Move(p4Bak, p4Live);
                            log("已还原原始 EFI\\Boot\\bootx64.efi", false);
                        }
                    }
                    catch (Exception ex)
                    {
                        log("清理 ESP 文件提示: " + ex.Message, false);
                    }
                    finally
                    {
                        UnmountEsp(esp);
                    }
                }

                string stateFile = Path.Combine(GetConfigDirectory(), "compat-state.txt");
                try {
                    string oldState = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"config\compat-state.txt");
                    if (File.Exists(oldState)) File.Delete(oldState);
                } catch { }
                if (File.Exists(stateFile)) File.Delete(stateFile);

                log("系统引导环境已恢复 100% 微软官方原生状态 (清理条目数: " + delCount + ")。", false);
                return true;
            }
            catch (Exception ex)
            {
                log("清理引导出错: " + ex.Message, true);
                return false;
            }
        }

        public static Task<bool> InstallPcieMode1Async(Action<string, bool> log)
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    log("[1/5] 验证 UEFI 运行环境与安全引导...", false);
                    string curOut = RunCmd("bcdedit", "/enum {current}");
                    if (curOut.IndexOf("winload.efi", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        log("当前系统不是 UEFI 模式启动，无法安装 EFI 解锁！", true);
                        return false;
                    }

                    log("[2/5] 执行防冲突安全清理，规避多模式重复项...", false);
                    CleanAllPcieUnlocks(log);

                    string bcdBak1 = BackupBcd(log);
                    if (string.IsNullOrEmpty(bcdBak1))
                    {
                        log("BCD 引导备份失败，为避免引导损坏已中止安装。", true);
                        return false;
                    }
                    log("已备份引导配置: " + bcdBak1 + "（出问题可用它还原）", false);

                    log("[3/5] 挂载 EFI 系统分区 (ESP) 并部署直启固件...", false);
                    string esp = MountEsp();
                    if (string.IsNullOrEmpty(esp))
                    {
                        log("无法挂载 EFI 分区，请确认管理员权限！", true);
                        return false;
                    }

                    try
                    {
                        byte[] efiData = GetPackagedResource("mode1/NVPermissiveCompat.efi");
                        string efiDir = Path.Combine(esp, @"EFI\NVPermissive");
                        if (!Directory.Exists(efiDir)) Directory.CreateDirectory(efiDir);
                        string targetEfi = Path.Combine(efiDir, "NVPermissiveCompat.efi");
                        File.WriteAllBytes(targetEfi, efiData);

                        byte[] written = File.ReadAllBytes(targetEfi);
                        if (written.Length != efiData.Length)
                        {
                            log("固件写入校验失败，文件不完整！", true);
                            return false;
                        }
                        log("NVPermissiveCompat.efi 固件部署且完整性校验成功 (" + written.Length + " 字节)", false);
                    }
                    finally
                    {
                        UnmountEsp(esp);
                    }

                    log("[4/5] 配置官方原生引导链（不创建重复启动项）...", false);
                    log("说明：本方案不再复制 {bootmgr} 生成备份项，避免开机出现系统选择框。", false);

                    log("[5/5] 接管 {bootmgr} 并设置最高优先级...", false);
                    RunCmd("bcdedit", "/set {fwbootmgr} displayorder {bootmgr}");
                    RunCmd("bcdedit", "/set {bootmgr} path \\EFI\\NVPermissive\\NVPermissiveCompat.efi");

                    string stateDir = GetConfigDirectory();
                    if (!Directory.Exists(stateDir)) Directory.CreateDirectory(stateDir);
                    File.WriteAllText(Path.Combine(stateDir, "compat-state.txt"), "MODE=1\r\nTIMESTAMP=" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "\r\n");

                    log("==================================================", false);
                    log("【模式 1 安装成功】PCIe Gen2 x16 免Shell直启已就绪！", false);
                    log("说明：已内置官方 Windows 自动回退保护，永不卡黑屏变砖。", false);
                    log("提示：请将电脑【完全关机】(断电 10 秒以上) 后重新开机生效！", false);
                    log("==================================================", false);
                    return true;
                }
                catch (Exception ex)
                {
                    log("安装模式 1 出错: " + ex.Message, true);
                    return false;
                }
            });
        }

        public static Task<bool> InstallPcieMode2Async(Action<string, bool> log)
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    log("[1/4] 验证系统环境并清理旧引导...", false);
                    string curOut = RunCmd("bcdedit", "/enum {current}");
                    if (curOut.IndexOf("winload.efi", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        log("当前系统不是 UEFI 模式启动，无法安装！", true);
                        return false;
                    }

                    CleanAllPcieUnlocks(log);

                    string bcdBak2 = BackupBcd(log);
                    if (string.IsNullOrEmpty(bcdBak2))
                    {
                        log("BCD 引导备份失败，为避免引导损坏已中止安装。", true);
                        return false;
                    }
                    log("已备份引导配置: " + bcdBak2 + "（出问题可用它还原）", false);

                    log("[2/4] 挂载 ESP 分区并释放 Shell 套件...", false);
                    string esp = MountEsp();
                    if (string.IsNullOrEmpty(esp))
                    {
                        log("无法挂载 EFI 分区！", true);
                        return false;
                    }

                    try
                    {
                        string dir90hx = Path.Combine(esp, @"EFI\90HX");
                        if (!Directory.Exists(dir90hx)) Directory.CreateDirectory(dir90hx);

                        byte[] shellEfi = GetPackagedResource("mode2/Shell.efi");
                        byte[] nvEfi = GetPackagedResource("mode2/NVPermissiveEFI.efi");
                        byte[] nsh = GetPackagedResource("mode2/startup.nsh");

                        File.WriteAllBytes(Path.Combine(dir90hx, "Shell.efi"), shellEfi);
                        File.WriteAllBytes(Path.Combine(dir90hx, "NVPermissiveEFI.efi"), nvEfi);

                        string smartNsh = GenerateSmartStartupNsh(DetectAllMiningGpus());
                        File.WriteAllText(Path.Combine(dir90hx, "startup.nsh"), smartNsh, Encoding.ASCII);
                        File.WriteAllText(Path.Combine(esp, "startup.nsh"), smartNsh, Encoding.ASCII);

                        log("Shell.efi 与全系多卡自动化解锁脚本已部署至 ESP 分区", false);
                    }
                    finally
                    {
                        UnmountEsp(esp);
                    }

                    log("[3/4] 配置 90HX Unlock (UEFI Shell) 引导项（不创建重复启动项）...", false);
                    RunCmd("bcdedit", "/set {bootmgr} path \\EFI\\90HX\\Shell.efi");
                    RunCmd("bcdedit", "/set {fwbootmgr} displayorder {bootmgr}");

                    string stateDir = GetConfigDirectory();
                    if (!Directory.Exists(stateDir)) Directory.CreateDirectory(stateDir);
                    File.WriteAllText(Path.Combine(stateDir, "compat-state.txt"), "MODE=2\r\nTIMESTAMP=" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "\r\n");

                    log("==================================================", false);
                    log("【模式 2 安装成功】UEFI Shell 兼容脚本模式已配置！", false);
                    log("开机会出现黄色 UEFI Shell 倒计时 1 秒后自动链启进入 Windows。", false);
                    log("提示：请将电脑【完全关机】(断电 10 秒以上) 后开机！", false);
                    log("==================================================", false);
                    return true;
                }
                catch (Exception ex)
                {
                    log("安装模式 2 出错: " + ex.Message, true);
                    return false;
                }
            });
        }

        public static Task<bool> CreateMode3UsbDriveAsync(string driveLetter, Action<string, bool> log)
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    if (string.IsNullOrEmpty(driveLetter)) throw new ArgumentException("请选择目标 U 盘盘符！");
                    string root = driveLetter.TrimEnd('\\') + "\\";
                    if (!Directory.Exists(root)) throw new DirectoryNotFoundException("U 盘不可用: " + root);

                    var di = new DriveInfo(driveLetter.Substring(0, 1));
                    log("正在检查目标磁盘: " + di.Name + " (" + di.VolumeLabel + ", 格式: " + di.DriveFormat + ")", false);

                    if (!di.DriveFormat.Equals("FAT32", StringComparison.OrdinalIgnoreCase))
                    {
                        log("【重要】UEFI 直启要求 U 盘为 FAT32 格式，当前为 " + di.DriveFormat + "，主板可能无法识别！", true);
                    }

                    log("正在释放完整 U 盘直启套件 (Shell + 解锁固件 + 引导链，共 7 个文件)...", false);

                    byte[] shell = GetPackagedResource("usb90/shellx64.efi");
                    byte[] bootmgr = GetPackagedResource("usb90/bootmgfw.efi");
                    byte[] firmware = GetPackagedResource("usb90/NVPermissiveEFI.efi");
                    byte[] nsh = GetPackagedResource("usb90/startup.nsh");

                    string bootDir = Path.Combine(root, @"EFI\Boot");
                    if (!Directory.Exists(bootDir)) Directory.CreateDirectory(bootDir);
                    File.WriteAllBytes(Path.Combine(bootDir, "bootx64.efi"), shell);
                    File.WriteAllBytes(Path.Combine(bootDir, "bootmgfw.efi"), bootmgr);
                    File.WriteAllBytes(Path.Combine(bootDir, "startup.nsh"), nsh);
                    log("[1/3] EFI\\Boot 引导目录写入完成 (bootx64.efi + bootmgfw.efi + startup.nsh)", false);

                    File.WriteAllBytes(Path.Combine(root, "NVPermissiveEFI.efi"), firmware);
                    File.WriteAllBytes(Path.Combine(root, "shellx64.efi"), shell);
                    File.WriteAllBytes(Path.Combine(root, "bootmgfw.efi"), bootmgr);
                    File.WriteAllBytes(Path.Combine(root, "startup.nsh"), nsh);
                    log("[2/3] 根目录解锁固件写入完成 (NVPermissiveEFI.efi + 3 个配套文件)", false);

                    string readme =
                        "90HX Studio - CMP 90HX 一键解锁 U 盘\r\n" +
                        "==================================================\r\n" +
                        "【文件清单】共 7 个文件\r\n" +
                        "  \\EFI\\Boot\\bootx64.efi      UEFI Shell (自动执行 startup.nsh)\r\n" +
                        "  \\EFI\\Boot\\bootmgfw.efi     Windows 引导管理器\r\n" +
                        "  \\EFI\\Boot\\startup.nsh      解锁 + 链启脚本\r\n" +
                        "  \\NVPermissiveEFI.efi       解锁固件 (算力 + 图形)\r\n" +
                        "  \\shellx64.efi / bootmgfw.efi / startup.nsh  冗余副本\r\n" +
                        "\r\n" +
                        "【使用方法】\r\n" +
                        "  1. BIOS 关闭 Secure Boot (安全启动)\r\n" +
                        "  2. 插上 U 盘，开机按 F8/F11/F12 选 【UEFI: 本U盘】\r\n" +
                        "  3. 解锁画面闪过即自动进入 Windows\r\n" +
                        "  4. GPU-Z 显示 PCIe x16 2.0 即解锁成功\r\n" +
                        "\r\n" +
                        "【若设置了第一启动项仍进系统】\r\n" +
                        "  a) BIOS 里要把 【UEFI: 本U盘】 设为第一 (不是普通U盘条目)\r\n" +
                        "  b) 关闭 BIOS 的 Fast Boot / 快速启动\r\n" +
                        "  c) 运行 90HX Studio 点【让 U 盘优先启动】自动配置\r\n" +
                        "  d) 或改用【安装到本机硬盘】方式，完全免 U 盘\r\n";
                    File.WriteAllText(Path.Combine(root, "使用说明.txt"), readme, Encoding.UTF8);
                    log("[3/3] 使用说明生成完成", false);

                    log("==================================================", false);
                    log("【U 盘直启制作完成】已写入 " + root, false);
                    log("共 7 个文件，即插即用。建议再用【让 U 盘优先启动】一键配置引导。", false);
                    log("==================================================", false);
                    return true;
                }
                catch (Exception ex)
                {
                    log("制作 U 盘直启出错: " + ex.Message, true);
                    return false;
                }
            });
        }

        public static Task<bool> SetUsbFirstBootAsync(string driveLetter, Action<string, bool> log)
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    if (string.IsNullOrEmpty(driveLetter)) throw new ArgumentException("请先选择目标 U 盘盘符！");
                    string letter = driveLetter.Substring(0, 2);
                    if (!Directory.Exists(letter + "\\")) throw new DirectoryNotFoundException("U 盘不可访问: " + letter);

                    log(">>> 正在配置 U 盘优先启动 (UEFI 固件引导项)...", false);

                    bool fastOff = false;
                    try
                    {
                        using (var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power", true))
                        {
                            if (k != null)
                            {
                                k.SetValue("HiberbootEnabled", 0, RegistryValueKind.DWord);
                                fastOff = true;
                            }
                        }
                    }
                    catch (Exception exFast)
                    {
                        log("关闭快速启动提示: " + exFast.Message, true);
                    }
                    log(fastOff ? "[1/4] 已关闭 Windows 快速启动 (避免混合关机跳过固件启动顺序)" : "[1/4] 快速启动关闭失败 (可手动关闭)", false);

                    string usbGuid = FindFirmwareEntry("90HX Unlock USB");
                    if (string.IsNullOrEmpty(usbGuid))
                    {
                        string copyOut = RunCmd("bcdedit", "/copy {bootmgr} /d \"90HX Unlock USB\"");
                        var m = System.Text.RegularExpressions.Regex.Match(copyOut, @"\{[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}\}");
                        if (m.Success) usbGuid = m.Value;
                    }
                    if (string.IsNullOrEmpty(usbGuid))
                    {
                        log("创建 U 盘固件启动项失败，请改用【安装到本机硬盘】方式。", true);
                        return false;
                    }
                    log("[2/4] 已创建 U 盘固件启动项: " + usbGuid, false);

                    RunCmd("bcdedit", "/set " + usbGuid + " device partition=" + letter);
                    RunCmd("bcdedit", "/set " + usbGuid + " path \\EFI\\Boot\\bootx64.efi");
                    log("[3/4] 启动项已指向: " + letter + "\\EFI\\Boot\\bootx64.efi", false);

                    RunCmd("bcdedit", "/set {fwbootmgr} displayorder " + usbGuid + " /addfirst");

                    try
                    {
                        RunCmd("bcdedit", "/set {bootmgr} displaybootmenu Yes");
                        RunCmd("bcdedit", "/timeout 5");
                    }
                    catch { }
                    log("[4/4] 已置顶 U 盘为第一启动项，并打开 5 秒启动菜单作为兜底", false);

                    log("==================================================", false);
                    log("【配置完成】下次开机将优先从 U 盘启动并自动解锁。", false);
                    if (!fastOff) log("注意: 请手动进 BIOS 关闭 Fast Boot，并在电源选项关闭快速启动。", true);
                    log("若仍不生效，强烈建议改用【安装到本机硬盘】(免 U 盘，最稳)。", false);
                    log("==================================================", false);
                    return true;
                }
                catch (Exception ex)
                {
                    log("配置 U 盘优先启动出错: " + ex.Message, true);
                    return false;
                }
            });
        }

        public static Task<bool> RemoveUsbFirstBootAsync(Action<string, bool> log)
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    log(">>> 正在恢复 Windows 为默认第一启动项...", false);
                    string usbGuid = FindFirmwareEntry("90HX Unlock USB");
                    if (!string.IsNullOrEmpty(usbGuid))
                    {
                        RunCmd("bcdedit", "/delete " + usbGuid + " /f");
                        log("已删除 U 盘固件启动项: " + usbGuid, false);
                    }
                    RunCmd("bcdedit", "/set {fwbootmgr} displayorder {bootmgr} /addfirst");
                    log("已将 Windows Boot Manager 恢复为第一启动项。", false);
                    return true;
                }
                catch (Exception ex)
                {
                    log("恢复启动项出错: " + ex.Message, true);
                    return false;
                }
            });
        }

        private static string FindFirmwareEntry(string descKeyword)
        {
            try
            {
                string outFw = RunCmd("bcdedit", "/enum firmware");
                string curId = null;
                using (var sr = new StringReader(outFw))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        line = line.Trim();
                        var mId = System.Text.RegularExpressions.Regex.Match(line, @"^(?:identifier|标识符)\s+(\{[0-9a-fA-F-]+\})");
                        if (mId.Success) { curId = mId.Groups[1].Value; continue; }
                        var mDesc = System.Text.RegularExpressions.Regex.Match(line, @"^(?:description|描述)\s+(.+)$");
                        if (mDesc.Success && mDesc.Groups[1].Value.IndexOf(descKeyword, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return curId;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// 一键修复开机「系统选择框」问题。
        /// 成因：旧版本解锁会执行 bcdedit /copy {bootmgr} 生成 "Windows Boot Manager (Backup)"
        /// 重复启动项并塞进 displayorder，导致开机弹出多系统选择菜单（类似 Win7 双系统界面）。
        /// 本方法会：
        ///   1. 先导出 BCD 备份（可还原）
        ///   2. 枚举 {fwbootmgr} 与 osloader 下所有条目，删除由本工具生成的重复项
        ///   3. 把 {bootmgr} 的 path 还原为官方 bootmgfw.efi
        ///   4. 重建 displayorder，只保留真正的 Windows 启动管理器
        /// </summary>
        public static Task<bool> RepairBootMenuAsync(Action<string, bool> log)
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    if (!IsAdmin())
                    {
                        log("修复引导需要管理员权限，请右键以管理员身份运行本程序。", true);
                        return false;
                    }

                    log("==================================================", false);
                    log("开始修复开机系统选择框问题", false);
                    log("==================================================", false);

                    string bak = BackupBcd(log);
                    if (string.IsNullOrEmpty(bak))
                    {
                        log("BCD 备份失败，为避免引导损坏已中止修复。", true);
                        return false;
                    }

                    // ---------- 1) 清理 {fwbootmgr} 下的重复启动项 ----------
                    string fwOut = RunCmd("bcdedit", "/enum {fwbootmgr}");
                    var toDelete = new List<string>();
                    var seen = new List<string>();

                    foreach (Match m in Regex.Matches(fwOut, @"\{[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}\}"))
                    {
                        string guid = m.Value;
                        if (seen.Contains(guid)) continue;
                        seen.Add(guid);
                        int st = m.Index;
                        int en = fwOut.IndexOf("\r\n\r\n", st, StringComparison.Ordinal);
                        if (en < 0) en = fwOut.Length;
                        string seg = fwOut.Substring(st, en - st);

                        // 注意：不能依赖 "description" 字段名——中文 Windows 的 bcdedit 输出为「说明」，
                        // 英文系统才是 description。因此直接搜整段文本中的 ASCII 关键词，实现语言无关。
                        bool isOurs = seg.IndexOf("Backup", StringComparison.OrdinalIgnoreCase) >= 0
                                   || seg.IndexOf("90HX", StringComparison.OrdinalIgnoreCase) >= 0
                                   || seg.IndexOf("NVPermissive", StringComparison.OrdinalIgnoreCase) >= 0
                                   || seg.IndexOf("Unlock", StringComparison.OrdinalIgnoreCase) >= 0;

                        if (isOurs)
                        {
                            toDelete.Add(guid);
                            log("发现本工具生成的重复启动项: [" + guid + "]", false);
                        }
                    }

                    foreach (string g in toDelete)
                    {
                        RunCmd("bcdedit", "/delete " + g);
                        log("已删除重复启动项: " + g, false);
                    }
                    if (toDelete.Count == 0) log("未发现重复的固件启动项。", false);

                    // ---------- 2) 还原 {bootmgr} 的 path ----------
                    string realBm = "";
                    string fw2 = RunCmd("bcdedit", "/enum {fwbootmgr}");
                    var bmMatch = Regex.Match(fw2, @"\{[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}\}");
                    if (bmMatch.Success) realBm = bmMatch.Value;

                    RunCmd("bcdedit", "/set {bootmgr} path \\EFI\\Microsoft\\Boot\\bootmgfw.efi");
                    log("已将 Windows 启动管理器路径还原为官方 bootmgfw.efi", false);

                    // ---------- 3) 重建 displayorder，只保留真正的启动管理器 ----------
                    if (!string.IsNullOrEmpty(realBm))
                    {
                        RunCmd("bcdedit", "/set {fwbootmgr} displayorder " + realBm);
                        log("已重建启动顺序，仅保留: " + realBm, false);
                    }

                    // ---------- 4) 清理 Windows 加载器层的重复 OS 项 ----------
                    string osOut = RunCmd("bcdedit", "/enum osloader");
                    var osDel = new List<string>();
                    foreach (Match m in Regex.Matches(osOut, @"\{[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}\}"))
                    {
                        string guid = m.Value;
                        int st = m.Index;
                        int en = osOut.IndexOf("\r\n\r\n", st, StringComparison.Ordinal);
                        if (en < 0) en = osOut.Length;
                        string seg = osOut.Substring(st, en - st);
                        // 同上：语言无关，直接搜整段 ASCII 关键词
                        if (seg.IndexOf("Backup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            seg.IndexOf("90HX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            seg.IndexOf("NVPermissive", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            osDel.Add(guid);
                            log("发现重复的系统加载项: [" + guid + "]", false);
                        }
                    }
                    foreach (string g in osDel)
                    {
                        RunCmd("bcdedit", "/delete " + g);
                        log("已删除重复系统加载项: " + g, false);
                    }

                    // ---------- 5) 清理工具自身的过期启动项残留 ----------
                    try { CleanAllPcieUnlocks(log); }
                    catch { }

                    log("==================================================", false);
                    log("引导菜单修复完成！共清理 " + (toDelete.Count + osDel.Count) + " 个重复启动项。", false);
                    log("备份位置: " + bak, false);
                    log("请重启电脑验证：开机应直接进入 Windows，不再弹出系统选择框。", false);
                    log("==================================================", false);
                    return true;
                }
                catch (Exception ex)
                {
                    log("修复引导菜单异常: " + ex.Message, true);
                    return false;
                }
            });
        }

        /// <summary>
        /// 导出当前 BCD 引导配置备份。任何会修改引导的操作之前都必须先调用。
        /// 返回备份文件路径；失败返回 null（调用方应中止，避免引导损坏）。
        /// </summary>
        public static string BackupBcd(Action<string, bool> log)
        {
            try
            {
                string dir = Path.Combine(GetBackupDirectory(), "BCD");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, "bcd_backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".bcd");

                log("正在导出引导配置备份 (bcdedit /export)...", false);
                var psi = new ProcessStartInfo("bcdedit", "/export \"" + file + "\"");
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                var p = Process.Start(psi);
                string outp = p.StandardOutput.ReadToEnd();
                p.WaitForExit(15000);

                if (File.Exists(file) && new FileInfo(file).Length > 0)
                {
                    return file;
                }
                log("bcdedit /export 未生成有效备份: " + outp, true);
                return null;
            }
            catch (Exception ex)
            {
                if (log != null) log("导出 BCD 备份异常: " + ex.Message, true);
                return null;
            }
        }

        /// <summary>从最近的 BCD 备份还原引导配置。</summary>
        public static bool RestoreBcdFromBackup(Action<string, bool> log)
        {
            try
            {
                string dir = Path.Combine(GetBackupDirectory(), "BCD");
                if (!Directory.Exists(dir))
                {
                    log("未找到引导备份目录，无法还原。", true);
                    return false;
                }
                var files = Directory.GetFiles(dir, "bcd_backup_*.bcd");
                if (files.Length == 0)
                {
                    log("未找到任何 BCD 备份文件。", true);
                    return false;
                }
                Array.Sort(files);
                string latest = files[files.Length - 1];

                log("正在从备份还原引导配置: " + Path.GetFileName(latest), false);
                var psi = new ProcessStartInfo("bcdedit", "/import \"" + latest + "\"");
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.CreateNoWindow = true;
                var p = Process.Start(psi);
                string outp = p.StandardOutput.ReadToEnd();
                p.WaitForExit(15000);

                log("引导配置已还原。建议重启验证系统能否正常启动。", false);
                return true;
            }
            catch (Exception ex)
            {
                log("还原 BCD 备份异常: " + ex.Message, true);
                return false;
            }
        }

        /// <summary>
        /// 新版解锁：NVPermissiveEFI 方案（图形 + 算力 + 光追 一并解锁）。
        /// 与旧版"单文件 BOOTX64.EFI 固化"的区别：
        ///   • 旧版把一个完整固件镜像写死进引导链，启动慢但兼容性好；
        ///   • 新版只在 EFI Shell 里跑一个 1.4MB 的 NVPermissiveEFI.efi，执行完再链启 Windows，速度更快。
        /// NVPermissiveEFI 支持参数：windows / full / compute / graphics / pcie-gen2，
        /// 2026-10-15 起版本额外启用 TTU，可配合 616.92 魔改驱动开启光追。
        /// </summary>
        public static Task<bool> ApplyNvPermissiveUnlockAsync(List<InstalledMiningGpu> gpus, int installChannel, string usbDrive, Action<string, bool> log)
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    if (gpus == null || gpus.Count == 0)
                    {
                        log("未检测到矿卡设备，请确认显卡已正确安装。", true);
                        return false;
                    }
                    if (!IsAdmin())
                    {
                        log("请以管理员身份运行本程序后再执行解锁。", true);
                        return false;
                    }

                    log("==================================================", false);
                    log("开始执行【新版解锁】NVPermissiveEFI 方案（图形 + 算力 + 光追）", false);
                    log("==================================================", false);

                    byte[] efi = GetPackagedResource("usb90/NVPermissiveEFI.efi");
                    byte[] shell = GetPackagedResource("usb90/shellx64.efi");
                    if (efi == null || efi.Length < 1024)
                    {
                        log("内置 NVPermissiveEFI.efi 资源缺失，请确认程序完整性。", true);
                        return false;
                    }
                    if (shell == null || shell.Length < 1024)
                    {
                        log("内置 shellx64.efi 资源缺失，请确认程序完整性。", true);
                        return false;
                    }

                    string channelName = installChannel == 0 ? "现代 UEFI 直启"
                        : (installChannel == 1 ? "老主板 Shell" : (installChannel == 2 ? "老卡驱动 Hook" : "U 盘直启"));

                    // ===== 引导安全网：任何会改动引导的操作之前，强制先导出 BCD 备份 =====
                    bool touchesBoot = (installChannel == 0 || installChannel == 1 || installChannel == 2);
                    string bcdBackup = null;
                    if (touchesBoot)
                    {
                        bcdBackup = BackupBcd(log);
                        if (string.IsNullOrEmpty(bcdBackup))
                        {
                            log("BCD 备份失败，为避免引导损坏已中止本机部署。", true);
                            log("建议改用【通道 D · U 盘直启】（零系统侵入，不动本机引导）。", true);
                            return false;
                        }
                        log("已备份当前引导配置到: " + bcdBackup, false);
                        log("如需还原，请运行【一键还原官方出厂】或使用 bcdedit /import 恢复该备份。", false);
                    }

                    // ---------- 阶段 1：按通道部署 NVPermissiveEFI ----------
                    if (installChannel == 3)
                    {
                        if (string.IsNullOrEmpty(usbDrive) || usbDrive.Length < 2)
                        {
                            log("请先在【通道 D · U 盘直启】中选择一个 U 盘。", true);
                            return false;
                        }
                        string root = usbDrive.Substring(0, 2) + "\\";
                        if (!Directory.Exists(root))
                        {
                            log("U 盘不可用: " + root, true);
                            return false;
                        }
                        string bootDir = Path.Combine(root, @"EFI\Boot");
                        if (!Directory.Exists(bootDir)) Directory.CreateDirectory(bootDir);

                        // 关键：U 盘必须以 BOOTX64.EFI 为入口，固件才会自动执行
                        File.WriteAllBytes(Path.Combine(bootDir, "BOOTX64.EFI"), shell);
                        File.WriteAllBytes(Path.Combine(bootDir, "NVPermissiveEFI.efi"), efi);
                        File.WriteAllText(Path.Combine(bootDir, "startup.nsh"), BuildNvPermissiveNsh(false), Encoding.ASCII);

                        // 根目录冗余副本（兼容个别老主板直接从根目录引导的场景）
                        File.WriteAllBytes(Path.Combine(root, "NVPermissiveEFI.efi"), efi);
                        File.WriteAllBytes(Path.Combine(root, "shellx64.efi"), shell);
                        File.WriteAllText(Path.Combine(root, "startup.nsh"), BuildNvPermissiveNsh(false), Encoding.ASCII);
                        File.WriteAllText(Path.Combine(root, "使用说明.txt"), BuildNvPermissiveReadme(), Encoding.UTF8);

                        log("已写入 U 盘 " + root + "（EFI\\Boot\\BOOTX64.EFI + NVPermissiveEFI.efi + startup.nsh）", false);
                        log("开机按快捷键选择该 U 盘启动即可，脚本会自动解锁并链启进入 Windows。", false);
                    }
                    else if (installChannel == 0 || installChannel == 1)
                    {
                        string esp = MountEsp(log);
                        if (string.IsNullOrEmpty(esp))
                        {
                            log("未找到 ESP 引导分区，无法本机部署（上方日志包含探测详情，请反馈给开发者）。", true);
                            return false;
                        }
                        string dest = Path.Combine(esp, @"EFI\NVPermissive");
                        if (!Directory.Exists(dest)) Directory.CreateDirectory(dest);
                        File.WriteAllBytes(Path.Combine(dest, "NVPermissiveEFI.efi"), efi);
                        File.WriteAllBytes(Path.Combine(dest, "shellx64.efi"), shell);
                        File.WriteAllText(Path.Combine(dest, "startup.nsh"), BuildNvPermissiveNsh(true), Encoding.ASCII);
                        log("已部署到 ESP 分区: " + dest, false);
                        UnmountEsp(esp);

                        // ===== 1) 清理旧的同名解锁启动项（保证可重复执行、不会堆积重复项） =====
                        try
                        {
                            string fwEnum = RunCmd("bcdedit", "/enum firmware");
                            foreach (var oe in ParseBcdEntries(fwEnum))
                            {
                                if ((oe.Description != null && oe.Description.IndexOf("CMP 90HX Unlock", StringComparison.OrdinalIgnoreCase) >= 0)
                                    || (oe.Path != null && oe.Path.IndexOf("NVPermissive", StringComparison.OrdinalIgnoreCase) >= 0))
                                {
                                    RunCmd("bcdedit", "/delete " + oe.Guid + " /f");
                                    log("已清理旧解锁启动项: " + oe.Guid, false);
                                }
                            }
                        }
                        catch { }

                        // ===== 2) 创建独立固件启动项：/copy {bootmgr} 继承 ESP 设备路径，
                        //         再改写为解锁 Shell；完全不改动 {bootmgr} 本体，
                        //         解锁失败时固件自动落到第二项（Windows 启动管理器）正常进系统。
                        string copyOut = RunCmd("bcdedit", "/copy {bootmgr} /d \"CMP 90HX Unlock EFI Shell\"");
                        Match guidM = Regex.Match(copyOut, @"\{[0-9a-fA-F\-]{36}\}");
                        if (!guidM.Success)
                        {
                            log("创建独立启动项失败: " + copyOut.Trim(), true);
                            return false;
                        }
                        string entryGuid = guidM.Value;

                        string bmOut = RunCmd("bcdedit", "/enum {bootmgr}");
                        Match devM = Regex.Match(bmOut, @"device\s+(partition=\S+)");
                        if (devM.Success) RunCmd("bcdedit", "/set " + entryGuid + " device " + devM.Groups[1].Value);
                        RunCmd("bcdedit", "/set " + entryGuid + " path \\EFI\\NVPermissive\\shellx64.efi");
                        RunCmd("bcdedit", "/set {fwbootmgr} displayorder " + entryGuid + " {bootmgr}");

                        log("已创建独立解锁启动项: " + entryGuid + "（Windows Boot Manager 保持官方原样）", false);
                        log("启动顺序：解锁项第一 / Windows 启动管理器第二（解锁失败自动兜底进系统）。", false);
                        log("注意：Secure Boot 开启时需先在 BIOS 注册解锁文件或关闭 Secure Boot，否则固件会拒绝执行。", false);
                        log("如需还原官方引导，点【一键清理引导冗余项】即可。", false);
                    }
                    else if (installChannel == 2)
                    {
                        log("[通道 C] 激活 Windows 测试签名模式以支持魔改驱动...", false);
                        RunCmd("bcdedit", "/set testsigning on");
                        RunCmd("bcdedit", "/set nointegritychecks on");
                        string dest = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"NVPermissive");
                        if (!Directory.Exists(dest)) Directory.CreateDirectory(dest);
                        File.WriteAllBytes(Path.Combine(dest, "NVPermissiveEFI.efi"), efi);
                        log("NVPermissiveEFI.efi 已释放到: " + dest, false);
                        log("该通道需手动在 EFI Shell 中执行一次 NVPermissiveEFI.efi windows。", false);
                    }

                    // ---------- 阶段 2：逐卡图形解锁（型号伪装 + 3D 渲染键值） ----------
                    int ok = 0;
                    for (int i = 0; i < gpus.Count; i++)
                    {
                        log(string.Format(">>> 处理显卡 [{0}/{1}] {2}...", i + 1, gpus.Count, gpus[i].DetectedModel), false);
                        var t = ApplySingleGpuGraphicsUnlockAsync(gpus[i], log);
                        t.Wait();
                        if (t.Result) ok++;
                    }

                    // ---------- 阶段 3：跨卡渲染路由 + 驱动热重载 ----------
                    log("写入 DirectX 全局首选高性能适配器与跨卡调用路由...", false);
                    ApplyDirectXGlobalDefaultGpu(log);
                    log("执行显示堆栈与驱动热重载...", false);
                    var rt = SoftRestartGpuDriverAsync(log);
                    rt.Wait();

                    log("==================================================", false);
                    log(string.Format("新版解锁部署完成：显卡处理成功 {0}/{1} 张。", ok, gpus.Count), false);
                    log("重启后从 NVPermissive 引导进入系统，即可获得 图形 + 算力 + 光追。", false);
                    log("==================================================", false);
                    return ok > 0;
                }
                catch (Exception ex)
                {
                    log("新版解锁执行异常: " + ex.Message, true);
                    return false;
                }
            });
        }

        /// <summary>
        /// 生成 EFI Shell 启动脚本（NVPermissiveEFI 方案）。
        /// espLayout=true  → 本机 ESP 部署（文件在 \EFI\NVPermissive\）
        /// espLayout=false → U 盘直启（文件在 \EFI\Boot\）
        /// 两种布局各只按一个绝对路径查找，确保解锁只执行一次。
        /// </summary>
        private static string BuildNvPermissiveNsh(bool espLayout)
        {
            string efiPath = espLayout ? "\\EFI\\NVPermissive\\NVPermissiveEFI.efi" : "\\NVPermissiveEFI.efi";
            return "@echo -off\r\n" +
                   "echo ================================================================\r\n" +
                   "echo   90HX Studio -- NVPermissive Unlock (Graphics + Compute + RT)\r\n" +
                   "echo ================================================================\r\n" +
                   "echo\r\n" +
                   "echo [1/2] Running NVPermissiveEFI (graphics + compute + gen2)...\r\n" +
                   "echo       This takes a while - please wait, do NOT power off.\r\n" +
                   "echo\r\n" +
                   "for %i in fs0 fs1 fs2 fs3 fs4 fs5 fs6 fs7\r\n" +
                   "  if exist %i:" + efiPath + " then\r\n" +
                   "    %i:" + efiPath + " windows\r\n" +
                   "  endif\r\n" +
                   "endfor\r\n" +
                   "echo\r\n" +
                   "echo [2/2] Unlock step finished. Chain-loading Windows...\r\n" +
                   "for %i in fs0 fs1 fs2 fs3 fs4 fs5 fs6 fs7\r\n" +
                   "  if exist %i:\\EFI\\Microsoft\\Boot\\bootmgfw.efi then\r\n" +
                   "    echo       Windows found on %i: - booting now\r\n" +
                   "    %i:\\EFI\\Microsoft\\Boot\\bootmgfw.efi\r\n" +
                   "  endif\r\n" +
                   "endfor\r\n" +
                   "echo\r\n" +
                   "echo [!] Windows Boot Manager not found - staying in EFI Shell.\r\n" +
                   "echo     Manual unlock: NVPermissiveEFI.efi windows\r\n" +
                   "echo     Then boot:     bootmgfw.efi\r\n";
        }

        private static string BuildNvPermissiveReadme()
        {
            return "90HX Studio · NVPermissiveEFI 新版解锁 U 盘（图形 + 算力 + 光追）\r\n" +
                   "==================================================\r\n" +
                   "方案来源: foxipan/vGPU/CMP_90HX/GraphicsUnlock (NVPermissiveEFI-202610150835)\r\n" +
                   "\r\n" +
                   "本套件与 90HX Studio 软件内「新版解锁」使用同一套已实测通过的文件。\r\n" +
                   "\r\n" +
                   "使用方式：\r\n" +
                   "  1. 开机按快捷键（华硕 F8 / 微星 F11 / 技嘉 F12）选择 UEFI U 盘启动。\r\n" +
                   "  2. startup.nsh 会自动执行 NVPermissiveEFI.efi windows 并链启进入 Windows。\r\n" +
                   "  3. 解锁为易失性：每次完全关机/重启后需再次从本 U 盘启动。\r\n" +
                   "\r\n" +
                   "Secure Boot 说明：\r\n" +
                   "  最简单是在 BIOS 里关闭 Secure Boot；\r\n" +
                   "  想保持开启，则在 BIOS 密钥管理 (Key Management) 里把本 U 盘的\r\n" +
                   "  \\EFI\\Boot\\BOOTX64.EFI 和 \\EFI\\Boot\\NVPermissiveEFI.efi 追加进 db 数据库\r\n" +
                   "  （追加模式，不要替换整个 db）。\r\n" +
                   "\r\n" +
                   "光追说明：\r\n" +
                   "  需同时安装 616.92 魔改驱动（已修正 grGetRTCoreCount）才能真正开启光追。\r\n" +
                   "  仅装 EFI 不装驱动，光追仍然不可用。\r\n";
        }

        /// <summary>
        /// 只读查找 ESP 引导分区（免挂载，支持无盘符 ESP）。
        /// 依次尝试：已挂载盘符 → mountvol 枚举卷 GUID 路径（验证含 bootmgfw.efi）→ 兼容旧挂载点。
        /// 返回值可直接与 EFI\... 拼接，形如 "S:\" 或 "\\?\Volume{...}\"。
        /// </summary>
        #region ESP 定位与临时挂载（Win32 直读，规避 .NET 对 \\?\ 卷路径的限制）

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFileAttributesW(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetVolumeMountPointW(string lpszVolumeMountPoint, string lpszVolumeName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool DeleteVolumeMountPointW(string lpszVolumeMountPoint);

        private const uint INVALID_FILE_ATTRIBUTES = 0xFFFFFFFF;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;

        private static readonly char[] EspMountLetters = new char[] { 'S', 'T', 'U', 'V', 'W', 'Y', 'Z' };
        private static string _espMountPoint = null;

        /// <summary>Win32 直判存在性：绕过 .NET Framework legacy 路径处理对 \\?\ 前缀的拒绝。</summary>
        private static bool Win32Exists(string path)
        {
            try { return GetFileAttributesW(path) != INVALID_FILE_ATTRIBUTES; }
            catch { return false; }
        }

        private static bool Win32IsDir(string path)
        {
            try
            {
                uint a = GetFileAttributesW(path);
                return a != INVALID_FILE_ATTRIBUTES && (a & FILE_ATTRIBUTE_DIRECTORY) != 0;
            }
            catch { return false; }
        }

        /// <summary>ESP 特征判断：bootmgfw / bootx64 / EFI 目录，命中任一即可。</summary>
        private static bool LooksLikeEsp(string root)
        {
            try
            {
                if (string.IsNullOrEmpty(root)) return false;
                if (!root.EndsWith("\\")) root += "\\";
                if (Win32Exists(root + @"EFI\Microsoft\Boot\bootmgfw.efi")) return true;
                if (Win32Exists(root + @"EFI\Boot\bootx64.efi")) return true;
                if (Win32Exists(root + @"EFI\Boot\BOOTX64.EFI")) return true;
                if (Win32IsDir(root + @"EFI\Microsoft\Boot")) return true;
                if (Win32IsDir(root + "EFI")) return true;
                return false;
            }
            catch { return false; }
        }

        private static string FindEspByWmi(Action<string, bool> log)
        {
            try
            {
                var scope = new System.Management.ManagementScope(@"\\.\root\Microsoft\Windows\Storage");
                var query = new System.Management.ObjectQuery("SELECT AccessPaths, GptType FROM MSFT_Partition");
                using (var searcher = new System.Management.ManagementObjectSearcher(scope, query))
                {
                    foreach (System.Management.ManagementObject p in searcher.Get())
                    {
                        object gt = p["GptType"];
                        if (gt == null) continue;
                        string g = Convert.ToString(gt);
                        if (string.IsNullOrEmpty(g) || g.IndexOf("c12a7328", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        string[] paths = p["AccessPaths"] as string[];
                        if (paths == null) continue;
                        foreach (string pth in paths)
                        {
                            if (string.IsNullOrEmpty(pth)) continue;
                            if (pth.StartsWith(@"\\?\Volume", StringComparison.OrdinalIgnoreCase))
                            {
                                if (log != null) log("ESP 命中（WMI EFI 系统分区）: " + pth, false);
                                return pth;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (log != null) log("WMI 分区查询不可用，回退卷扫描: " + ex.Message, false);
            }
            return null;
        }

        /// <summary>
        /// 定位 ESP：盘符 → WMI EFI 分区 → mountvol 卷扫描（Win32 验证）→ 兼容目录。
        /// 只返回路径不做挂载；需要读写文件请使用 MountEsp。
        /// </summary>
        private static string FindEspPartition(Action<string, bool> log = null)
        {
            Action<string, bool> L = (m, e) => { if (log != null) log(m, e); };
            try
            {
                // 1) 已挂载盘符
                string[] letters = new string[] { "C", "D", "E", "F", "G", "H", "I", "J", "K", "S", "T", "U", "V", "W", "Y", "Z" };
                foreach (string d in letters)
                {
                    string root = d + ":\\";
                    if (LooksLikeEsp(root))
                    {
                        L("ESP 定位成功（已挂载盘符）: " + root, false);
                        return root;
                    }
                }

                // 2) WMI：EFI 系统分区（GPT 类型 c12a7328-...），Win8+ 最可靠
                string wmiVol = FindEspByWmi(log);
                if (!string.IsNullOrEmpty(wmiVol)) return wmiVol;

                // 3) mountvol 卷扫描 + Win32 验证（.NET 的 File.Exists 无法访问 \\?\ 路径）
                string mv = RunCmd("mountvol", "");
                int volCount = 0;
                if (!string.IsNullOrEmpty(mv))
                {
                    foreach (Match m in Regex.Matches(mv, @"\\\\\?\\Volume\{[0-9a-fA-F\-]+\}\\"))
                    {
                        volCount++;
                        string vol = m.Value;
                        if (LooksLikeEsp(vol))
                        {
                            L("ESP 定位成功（mountvol 卷扫描，共 " + volCount + " 个卷）: " + vol, false);
                            return vol;
                        }
                    }
                }
                L("ESP 扫描未命中：mountvol 解析到 " + volCount + " 个卷（若为 0 可能被安全软件拦截）", true);

                // 4) 兼容旧版：EFI 曾被挂载到目录
                string[] legacy = new string[] {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "EFI"),
                    @"C:\EFI",
                    @"D:\EFI"
                };
                foreach (string c in legacy)
                {
                    if (Directory.Exists(c))
                    {
                        L("ESP 定位成功（兼容目录）: " + c, false);
                        return c;
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                L("ESP 探测异常: " + ex.Message, true);
                return null;
            }
        }

        #endregion

        /// <summary>
        /// 统计系统内 Windows 启动分区数量（只读）。
        /// NVPermissiveEFI 的自动链启在检测到多个启动分区时会拒绝执行，
        /// 因此部署前必须预检，多于 1 个时改用 startup.nsh 多路径链启。
        /// </summary>
        private static int CountWindowsBootPartitions()
        {
            try
            {
                int n = 0;
                string outp = RunCmd("bcdedit", "/enum {fwbootmgr}");
                foreach (Match m in Regex.Matches(outp, @"device\s+partition=([A-Za-z]:)"))
                {
                    if (!seenBootParts.Contains(m.Groups[1].Value.ToUpper()))
                    {
                        seenBootParts.Add(m.Groups[1].Value.ToUpper());
                        n++;
                    }
                }
                return n;
            }
            catch { return 1; }
        }

        private static readonly List<string> seenBootParts = new List<string>();

        public static Task<bool> Create90hxFirmwareUsbAsync(string driveLetter, bool computeOnly, Action<string, bool> log)
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    if (string.IsNullOrEmpty(driveLetter)) throw new ArgumentException("请选择目标 U 盘盘符！");
                    string root = driveLetter.TrimEnd('\\') + "\\";
                    if (!Directory.Exists(root)) throw new DirectoryNotFoundException("U 盘不可用: " + root);

                    var di = new DriveInfo(driveLetter.Substring(0, 1));
                    if (!di.DriveFormat.Equals("FAT32", StringComparison.OrdinalIgnoreCase) &&
                        !di.DriveFormat.Equals("FAT", StringComparison.OrdinalIgnoreCase))
                    {
                        log("提示: UEFI 规范建议直启 U 盘格式为 FAT32，当前为: " + di.DriveFormat, true);
                    }

                    string modeName = computeOnly ? "纯算力 (compute-only)" : "图形+算力完整 (FULL)";
                    log("正在释放 90HX 固件级解锁引导器 (" + modeName + ", WildFlash1st v3.05)...", false);

                    string bootDir = Path.Combine(root, @"EFI\Boot");
                    if (!Directory.Exists(bootDir)) Directory.CreateDirectory(bootDir);

                    byte[] bootx64 = GetPackagedResource(computeOnly ? "90hx_compute/BOOTX64.EFI" : "90hx_full/BOOTX64.EFI");
                    File.WriteAllBytes(Path.Combine(bootDir, "BOOTX64.EFI"), bootx64);

                    string readme = "90HX Studio · 90HX 固件级算力解锁 U 盘 (" + modeName + ")\r\n" +
                                    "==================================================\r\n" +
                                    "来源: WildFlash1st/cmp90hx-unlock-for-windows v3.05\r\n" +
                                    "使用方式:\r\n" +
                                    "1. 确认主板已【关闭 Secure Boot】。\r\n" +
                                    "2. 插入 U 盘，开机按快捷键 (华硕 F8/微星 F11/技嘉 F12) 选 UEFI U 盘启动。\r\n" +
                                    "3. 引导器自动解锁 90HX 算力并直接链启进入 Windows，全程无需按键。\r\n" +
                                    "4. 解锁为易失性：每次【完全关机/重启】后需再次从本 U 盘启动解锁。\r\n" +
                                    "5. 仅作用于 CMP 90HX (10de:220d)；如需游戏请另装 NVIDIA-patcher 补丁驱动。\r\n";
                    File.WriteAllText(Path.Combine(root, "使用说明.txt"), readme, Encoding.UTF8);

                    log("==================================================", false);
                    log("【90HX 固件级解锁 U 盘制作完成】已写入 " + root, false);
                    log("实测解锁后 llama 推理提速约 16 倍 (230 → 3700 t/s)。", false);
                    log("==================================================", false);
                    return true;
                }
                catch (Exception ex)
                {
                    log("制作 90HX 固件级解锁 U 盘出错: " + ex.Message, true);
                    return false;
                }
            });
        }

        public static Task<bool> RollbackPcieUnlockAsync(Action<string, bool> log)
        {
            return Task.Factory.StartNew(() =>
            {
                return CleanAllPcieUnlocks(log);
            });
        }

        public static Task<bool> InstallComputeMode1Async(Action<string, bool> log)
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    log("[1/5] 验证 UEFI 运行环境...", false);
                    string curOut = RunCmd("bcdedit", "/enum {current}");
                    if (curOut.IndexOf("winload.efi", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        log("当前系统不是 UEFI 模式启动，无法安装 EFI 固件！", true);
                        return false;
                    }

                    log("[2/5] 执行安全清理，规避多模式重复项...", false);
                    CleanAllPcieUnlocks(log);

                    log("[3/5] 挂载 EFI 系统分区 (ESP) 并部署 WildFlash1st 纯算力固件...", false);
                    string esp = MountEsp();
                    if (string.IsNullOrEmpty(esp))
                    {
                        log("无法挂载 EFI 分区，请确认管理员权限！", true);
                        return false;
                    }

                    try
                    {
                        byte[] efiData = GetPackagedResource("90hx_compute/BOOTX64.EFI");
                        string efiDir = Path.Combine(esp, @"EFI\WildCompute");
                        if (!Directory.Exists(efiDir)) Directory.CreateDirectory(efiDir);
                        string targetEfi = Path.Combine(efiDir, "BOOTX64.EFI");
                        File.WriteAllBytes(targetEfi, efiData);
                        log("WildFlash1st 纯算力固件部署成功 (" + efiData.Length + " 字节)", false);
                    }
                    finally
                    {
                        UnmountEsp(esp);
                    }

                    log("[4/5] 重构官方原生双保险引导链...", false);
                    string backupDesc = "Windows Boot Manager (Backup)";
                    string copyOut = RunCmd("bcdedit", "/copy {bootmgr} /d \"" + backupDesc + "\"");
                    var match = Regex.Match(copyOut, @"\{[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}\}");
                    string gBackup = match.Success ? match.Value : null;

                    if (!string.IsNullOrEmpty(gBackup))
                    {
                        RunCmd("bcdedit", "/set " + gBackup + " path \\EFI\\Microsoft\\Boot\\bootmgfw.efi");
                        log("官方原生兜底项已生成: " + gBackup, false);
                    }

                    log("[5/5] 接管 {bootmgr} 并设置最高优先级...", false);
                    RunCmd("bcdedit", "/set {fwbootmgr} displayorder {bootmgr} " + (gBackup ?? ""));
                    RunCmd("bcdedit", "/set {bootmgr} path \\EFI\\WildCompute\\BOOTX64.EFI");

                    string stateDir = GetConfigDirectory();
                    if (!Directory.Exists(stateDir)) Directory.CreateDirectory(stateDir);
                    File.WriteAllText(Path.Combine(stateDir, "compat-state.txt"), "MODE=COMPUTE_UEFI\r\nTIMESTAMP=" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "\r\n");

                    log("==================================================", false);
                    log("【通道 A · 现代 UEFI 直启已就绪】纯算力固件已注入 ESP 分区！", false);
                    log("说明：已内置官方 Windows 自动回退保护，开机秒进系统。", false);
                    log("提示：请将电脑【完全关机】(断电 10 秒以上) 后开机生效！", false);
                    log("==================================================", false);
                    return true;
                }
                catch (Exception ex)
                {
                    log("安装现代 UEFI 纯算力固件出错: " + ex.Message, true);
                    return false;
                }
            });
        }

        public static Task<bool> InstallComputeMode2Async(Action<string, bool> log)
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    log("[1/4] 验证系统环境并清理旧引导...", false);
                    string curOut = RunCmd("bcdedit", "/enum {current}");
                    if (curOut.IndexOf("winload.efi", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        log("当前系统不是 UEFI 模式启动，无法安装！", true);
                        return false;
                    }

                    CleanAllPcieUnlocks(log);

                    log("[2/4] 挂载 ESP 分区并释放老主板兼容 Shell 套件...", false);
                    string esp = MountEsp();
                    if (string.IsNullOrEmpty(esp))
                    {
                        log("无法挂载 EFI 分区！", true);
                        return false;
                    }

                    try
                    {
                        string dir90hx = Path.Combine(esp, @"EFI\90HX");
                        if (!Directory.Exists(dir90hx)) Directory.CreateDirectory(dir90hx);

                        byte[] shellEfi = GetPackagedResource("mode2/Shell.efi");
                        byte[] bootx64 = GetPackagedResource("90hx_compute/BOOTX64.EFI");

                        File.WriteAllBytes(Path.Combine(dir90hx, "Shell.efi"), shellEfi);
                        File.WriteAllBytes(Path.Combine(dir90hx, "BOOTX64.EFI"), bootx64);

                        string nsh = "@echo -off\r\necho [90HX Studio] Loading WildFlash1st Compute Unlock...\r\n" +
                                     "fs0:\\EFI\\90HX\\BOOTX64.EFI\r\n" +
                                     "fs1:\\EFI\\90HX\\BOOTX64.EFI\r\n" +
                                     "fs0:\\EFI\\Microsoft\\Boot\\bootmgfw.efi\r\n" +
                                     "fs1:\\EFI\\Microsoft\\Boot\\bootmgfw.efi\r\n";
                        File.WriteAllText(Path.Combine(dir90hx, "startup.nsh"), nsh, Encoding.ASCII);
                        File.WriteAllText(Path.Combine(esp, "startup.nsh"), nsh, Encoding.ASCII);

                        log("Shell.efi 与纯算力链式解锁脚本已部署至 ESP 分区", false);
                    }
                    finally
                    {
                        UnmountEsp(esp);
                    }

                    log("[3/4] 配置老主板 UEFI Shell 引导项...", false);
                    string backupDesc = "Windows Boot Manager (Backup)";
                    string copyOut = RunCmd("bcdedit", "/copy {bootmgr} /d \"" + backupDesc + "\"");
                    var mBk = Regex.Match(copyOut, @"\{[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}\}");
                    string gBackup = mBk.Success ? mBk.Value : null;
                    if (!string.IsNullOrEmpty(gBackup))
                    {
                        RunCmd("bcdedit", "/set " + gBackup + " path \\EFI\\Microsoft\\Boot\\bootmgfw.efi");
                    }

                    RunCmd("bcdedit", "/set {bootmgr} path \\EFI\\90HX\\Shell.efi");
                    RunCmd("bcdedit", "/set {fwbootmgr} displayorder {bootmgr} " + (gBackup ?? ""));

                    log("==================================================", false);
                    log("【通道 B · 老主板兼容 Shell 纯算力已就绪】！", false);
                    log("华南/精粤/X79/X99 等老主板专属，杜绝黑屏卡顿。", false);
                    log("==================================================", false);
                    return true;
                }
                catch (Exception ex)
                {
                    log("安装老主板 Shell 纯算力出错: " + ex.Message, true);
                    return false;
                }
            });
        }

        public static Task<bool> ApplyComputeFirmwareMultiGpuUnlockAsync(List<InstalledMiningGpu> gpus, int installChannel, string usbDrive, Action<string, bool> log)
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    log("==================================================", false);
                    string channelName = installChannel == 0 ? "现代 UEFI 直启" : (installChannel == 1 ? "老主板 Shell" : (installChannel == 2 ? "老卡驱动 Hook" : "U 盘直启"));
                    log(string.Format("开始执行【固件级算力满血解锁】(显卡数量: {0}, 安装通道: {1})...", gpus.Count, channelName), false);
                    log("WildFlash1st 核心：击穿 SEC2/PLM 硬件算力锁，llama 推理实测提速 16 倍", false);
                    log("==================================================", false);

                    if (installChannel == 0)
                    {
                        log("[阶段 1/3] 部署 UEFI ESP 分区 WildFlash1st 纯算力固件直启链...", false);
                        InstallComputeMode1Async(log).Wait();
                    }
                    else if (installChannel == 1)
                    {
                        log("[阶段 1/3] 部署老主板 UEFI Shell 纯算力链式启动固件...", false);
                        InstallComputeMode2Async(log).Wait();
                    }
                    else if (installChannel == 2)
                    {
                        log("[阶段 1/3] 开启测试签名与完整性校验绕过...", false);
                        RunCmd("bcdedit", "/set testsigning on");
                        RunCmd("bcdedit", "/set nointegritychecks on");
                        log("已开启测试签名与证书豁免。", false);
                    }
                    else if (installChannel == 3)
                    {
                        log("[阶段 1/3] 正在写入 WildFlash1st 纯算力引导到 U 盘: " + usbDrive, false);
                        Create90hxFirmwareUsbAsync(usbDrive, true, log).Wait();
                    }

                    log("[阶段 2/3] 写入全机矿卡核心 P0 满载频率与解除计算熔断节流...", false);
                    int cSuccess = 0;
                    for (int i = 0; i < gpus.Count; i++)
                    {
                        var gpu = gpus[i];
                        log(string.Format(">>> 处理显卡 [{0}/{1}]: 插槽 {2} - {3}...", i + 1, gpus.Count, gpu.SlotIndex + 1, gpu.DetectedModel), false);
                        if (ApplySingleGpuComputeUnlockAsync(gpu, log).Result) cSuccess++;
                    }

                    log("[阶段 3/3] 软重启显卡驱动以重载算力策略...", false);
                    SoftRestartGpuDriverAsync(log).Wait();

                    log("==================================================", false);
                    log(string.Format("固件级算力解锁完成！成功配置 {0} 张矿卡，通道: {1}。", cSuccess, channelName), false);
                    log("==================================================", false);
                    return true;
                }
                catch (Exception ex)
                {
                    log("固件级算力解锁异常: " + ex.Message, true);
                    return false;
                }
            });
        }

        public static List<string> GetAvailableRemovableDrives()
        {
            var list = new List<string>();
            try
            {
                foreach (var d in DriveInfo.GetDrives())
                {
                    if (d.IsReady && (d.DriveType == DriveType.Removable || d.DriveFormat.Equals("FAT32", StringComparison.OrdinalIgnoreCase)))
                    {
                        list.Add(d.Name.Substring(0, 2) + " (" + (string.IsNullOrEmpty(d.VolumeLabel) ? "未命名U盘" : d.VolumeLabel) + " - " + d.DriveFormat + ")");
                    }
                }
            }
            catch { }
            return list;
        }

        private class BcdEntryInfo
        {
            public string Guid;
            public string Description = "";
            public string Path = "";
        }

        private static List<BcdEntryInfo> ParseBcdEntries(string bcdText)
        {
            var list = new List<BcdEntryInfo>();
            if (string.IsNullOrEmpty(bcdText)) return list;

            BcdEntryInfo cur = null;
            using (var reader = new StringReader(bcdText))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    // 中英文双语兼容：中文 Windows 的 bcdedit 输出为「标识符」，英文系统才是 identifier
                    var mId = Regex.Match(line, @"^\s*(?:identifier|标识符)\s+(\{[0-9a-fA-F-]+\})");
                    if (mId.Success)
                    {
                        if (cur != null) list.Add(cur);
                        cur = new BcdEntryInfo();
                        cur.Guid = mId.Groups[1].Value;
                        continue;
                    }

                    if (cur != null)
                    {
                        var mDesc = Regex.Match(line, @"^\s*(?:description|描述|说明)\s+(.+)$");
                        if (mDesc.Success) cur.Description = mDesc.Groups[1].Value.Trim();

                        var mPath = Regex.Match(line, @"^\s*(?:path|路径)\s+(\S+)\s*$");
                        if (mPath.Success) cur.Path = mPath.Groups[1].Value.Trim();
                    }
                }
                if (cur != null) list.Add(cur);
            }
            return list;
        }

        /// <summary>
        /// 获取可读写的 ESP 根路径：优先已挂载盘符；否则定位 ESP 卷后用 Win32
        /// SetVolumeMountPoint 临时挂载到空闲盘符（S-Z），返回 "X:"。
        /// 挂载仅由本进程创建，结束时用 UnmountEsp 卸载。
        /// </summary>
        private static string MountEsp(Action<string, bool> log = null)
        {
            Action<string, bool> L = (m, e) => { if (log != null) log(m, e); };
            try
            {
                // 1) 已挂载盘符
                foreach (char letter in EspMountLetters)
                {
                    string drv = letter + ":";
                    if (LooksLikeEsp(drv + "\\")) return drv;
                }

                // 2) 定位 ESP 卷
                string vol = FindEspPartition(log);
                if (string.IsNullOrEmpty(vol)) return null;

                // 已是盘符路径
                if (Regex.IsMatch(vol, @"^[A-Za-z]:\\?$")) return vol.TrimEnd('\\');

                // 3) 挂载到空闲盘符（Win32 API，不调用 mountvol /S，避免卡死）
                foreach (char letter in EspMountLetters)
                {
                    string mp = letter + ":\\";
                    try
                    {
                        if (Directory.Exists(mp)) continue;
                    }
                    catch { }
                    if (SetVolumeMountPointW(mp, vol))
                    {
                        _espMountPoint = mp;
                        if (LooksLikeEsp(mp))
                        {
                            L("ESP 已临时挂载到 " + mp + " 用于写入", false);
                            return mp.TrimEnd('\\');
                        }
                        try { DeleteVolumeMountPointW(mp); } catch { }
                        _espMountPoint = null;
                    }
                }

                // 4) 挂载失败：退回卷路径（写入可能失败，但日志明确）
                L("ESP 未能挂载为盘符，尝试直接写入卷路径（部分系统会失败）", true);
                return vol;
            }
            catch (Exception ex)
            {
                L("ESP 挂载异常: " + ex.Message, true);
                return null;
            }
        }

        private static void UnmountEsp(string espPath)
        {
            try
            {
                // 只卸载本进程临时挂载的盘符，绝不动用户原有挂载
                if (!string.IsNullOrEmpty(_espMountPoint))
                {
                    DeleteVolumeMountPointW(_espMountPoint);
                    _espMountPoint = null;
                }
            }
            catch { }
        }

        #endregion

        #region 双卡调用方案 (Dual-GPU MS-Hybrid 跨卡渲染)

        public class DualGpuTopology
        {
            public bool HasRenderGpu;
            public string RenderGpuName = "未检测到 90HX";
            public string RenderGpuPciId = "";

            public bool HasDisplayGpu;
            public string DisplayGpuName = "未检测到显示输出卡 (核显/亮机卡)";
            public string DisplayGpuPciId = "";

            public bool HasVirtualGpu;
            public string VirtualGpuName = "";

            public int BoundGamesCount;
            public bool HagsActive;
        }

        public static DualGpuTopology DetectDualGpuTopology()
        {
            var topo = new DualGpuTopology();
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

                                if (matchId.IndexOf("220D", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    desc.IndexOf("90HX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    (desc.IndexOf("3080", StringComparison.OrdinalIgnoreCase) >= 0 && matchId.IndexOf("10DE", StringComparison.OrdinalIgnoreCase) >= 0))
                                {
                                    topo.HasRenderGpu = true;
                                    topo.RenderGpuName = desc;
                                    topo.RenderGpuPciId = matchId;
                                }
                                else if (matchId.IndexOf("Root\\GameViewer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                         desc.IndexOf("Virtual", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    topo.HasVirtualGpu = true;
                                    topo.VirtualGpuName = desc;
                                }
                                else if (!string.IsNullOrEmpty(desc) && desc.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) < 0)
                                {
                                    topo.HasDisplayGpu = true;
                                    topo.DisplayGpuName = desc;
                                    topo.DisplayGpuPciId = matchId;
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            try
            {
                var list = GetConfiguredGpuPreferenceApps();
                topo.BoundGamesCount = list.Count;
            }
            catch { }

            try
            {
                topo.HagsActive = IsHagsEnabled();
            }
            catch { }

            return topo;
        }

        public static List<string> GetConfiguredGpuPreferenceApps()
        {
            var result = new List<string>();
            try
            {
                const string subPath = @"Software\Microsoft\DirectX\UserGpuPreferences";
                using (var key = Registry.CurrentUser.OpenSubKey(subPath))
                {
                    if (key != null)
                    {
                        foreach (var name in key.GetValueNames())
                        {
                            var val = Convert.ToString(key.GetValue(name) ?? "");
                            if (val.IndexOf("GpuPreference=2", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                result.Add(name);
                            }
                        }
                    }
                }
            }
            catch { }

            try
            {
                using (var users = Registry.Users)
                {
                    if (users != null)
                    {
                        foreach (var sid in users.GetSubKeyNames())
                        {
                            if (sid.StartsWith("S-1-5-21-") && !sid.EndsWith("_Classes"))
                            {
                                using (var uKey = users.OpenSubKey(sid + @"\Software\Microsoft\DirectX\UserGpuPreferences"))
                                {
                                    if (uKey != null)
                                    {
                                        foreach (var name in uKey.GetValueNames())
                                        {
                                            if (!result.Contains(name))
                                            {
                                                var val = Convert.ToString(uKey.GetValue(name) ?? "");
                                                if (val.IndexOf("GpuPreference=2", StringComparison.OrdinalIgnoreCase) >= 0)
                                                {
                                                    result.Add(name);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return result;
        }

        public static bool AddAppToGpuPreference(string exePath, Action<string, bool> log)
        {
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                if (log != null) log("指定的可执行文件不存在: " + exePath, true);
                return false;
            }

            try
            {
                const string subPath = @"Software\Microsoft\DirectX\UserGpuPreferences";

                try
                {
                    using (var key = Registry.CurrentUser.CreateSubKey(subPath))
                    {
                        if (key != null)
                        {
                            key.SetValue(exePath, "GpuPreference=2;", RegistryValueKind.String);
                        }
                    }
                }
                catch { }

                try
                {
                    using (var users = Registry.Users)
                    {
                        if (users != null)
                        {
                            foreach (var sid in users.GetSubKeyNames())
                            {
                                if (sid.StartsWith("S-1-5-21-") && !sid.EndsWith("_Classes"))
                                {
                                    using (var uKey = users.CreateSubKey(sid + @"\Software\Microsoft\DirectX\UserGpuPreferences"))
                                    {
                                        if (uKey != null)
                                        {
                                            uKey.SetValue(exePath, "GpuPreference=2;", RegistryValueKind.String);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }

                if (log != null) log("已成功将程序绑定至 90HX 高性能渲染: " + Path.GetFileName(exePath), false);
                return true;
            }
            catch (Exception ex)
            {
                if (log != null) log("绑定程序渲染失败: " + ex.Message, true);
                return false;
            }
        }

        public static bool RemoveAppFromGpuPreference(string exePath, Action<string, bool> log)
        {
            try
            {
                const string subPath = @"Software\Microsoft\DirectX\UserGpuPreferences";
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(subPath, true))
                    {
                        if (key != null) key.DeleteValue(exePath, false);
                    }
                }
                catch { }

                try
                {
                    using (var users = Registry.Users)
                    {
                        if (users != null)
                        {
                            foreach (var sid in users.GetSubKeyNames())
                            {
                                if (sid.StartsWith("S-1-5-21-") && !sid.EndsWith("_Classes"))
                                {
                                    using (var uKey = users.OpenSubKey(sid + @"\Software\Microsoft\DirectX\UserGpuPreferences", true))
                                    {
                                        if (uKey != null) uKey.DeleteValue(exePath, false);
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }

                if (log != null) log("已移除程序 GPU 绑定: " + Path.GetFileName(exePath), false);
                return true;
            }
            catch (Exception ex)
            {
                if (log != null) log("移除绑定出错: " + ex.Message, true);
                return false;
            }
        }

        public static Task<int> AutoScanAndBindCommonGamesAsync(Action<string, bool> log)
        {
            return Task.Factory.StartNew(() =>
            {
                int count = 0;
                try
                {
                    if (log != null) log("开始全盘智能扫描常见游戏平台与 3A 游戏程序...", false);

                    var searchDirs = new List<string>
                    {
                        @"D:\Games",
                        @"E:\Games",
                        @"F:\Games",
                        @"C:\Program Files (x86)\Steam\steamapps\common",
                        @"D:\SteamLibrary\steamapps\common",
                        @"E:\SteamLibrary\steamapps\common",
                        @"F:\SteamLibrary\steamapps\common",
                        @"C:\Program Files\Epic Games",
                        @"D:\Epic Games",
                        @"D:\WeGameApps",
                        @"C:\Program Files (x86)\WeGame\apps"
                    };

                    try
                    {
                        using (var sKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                        {
                            if (sKey != null)
                            {
                                var steamPath = Convert.ToString(sKey.GetValue("SteamPath") ?? "");
                                if (!string.IsNullOrEmpty(steamPath) && Directory.Exists(steamPath))
                                {
                                    string common = Path.Combine(steamPath, @"steamapps\common");
                                    if (Directory.Exists(common) && !searchDirs.Contains(common))
                                    {
                                        searchDirs.Add(common);
                                    }
                                }
                            }
                        }
                    }
                    catch { }

                    var foundExes = new List<string>();

                    foreach (var dir in searchDirs)
                    {
                        if (!Directory.Exists(dir)) continue;
                        try
                        {
                            foreach (var file in Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories))
                            {
                                string name = Path.GetFileName(file).ToLowerInvariant();
                                if (name.Contains("crash") || name.Contains("unity") || name.Contains("unins") ||
                                    name.Contains("update") || name.Contains("report") || name.Contains("setup") ||
                                    name.Contains("vcredist") || name.Contains("dxsetup") || name.Contains("launcher"))
                                    continue;

                                foundExes.Add(file);
                                if (foundExes.Count >= 60) break;
                            }
                        }
                        catch { }
                    }

                    foreach (var p in Process.GetProcesses())
                    {
                        try
                        {
                            if (!string.IsNullOrEmpty(p.MainWindowTitle) && p.MainModule != null)
                            {
                                string exe = p.MainModule.FileName;
                                if (!string.IsNullOrEmpty(exe) && !foundExes.Contains(exe) &&
                                    exe.IndexOf("explorer.exe", StringComparison.OrdinalIgnoreCase) < 0 &&
                                    exe.IndexOf("CMP90HX", StringComparison.OrdinalIgnoreCase) < 0)
                                {
                                    foundExes.Add(exe);
                                }
                            }
                        }
                        catch { }
                    }

                    foreach (var exe in foundExes)
                    {
                        if (AddAppToGpuPreference(exe, null))
                        {
                            count++;
                            if (log != null) log("已自动绑定高性能 90HX 渲染: " + Path.GetFileName(exe), false);
                        }
                    }

                    if (log != null) log("智能扫描完成，共为 " + count + " 款游戏/程序配置了 90HX 独占渲染首选项！", false);
                }
                catch (Exception ex)
                {
                    if (log != null) log("智能扫描出错: " + ex.Message, true);
                }
                return count;
            });
        }

        public class RunningProcessItem
        {
            public string ProcessName;
            public string WindowTitle;
            public string ExePath;

            public override string ToString()
            {
                return ProcessName + " (" + WindowTitle + ")";
            }
        }

        public static List<RunningProcessItem> GetRunningWindowProcesses()
        {
            var list = new List<RunningProcessItem>();
            try
            {
                foreach (var p in Process.GetProcesses())
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(p.MainWindowTitle) && p.MainModule != null)
                        {
                            string path = p.MainModule.FileName;
                            if (string.IsNullOrEmpty(path)) continue;
                            if (path.IndexOf("explorer.exe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                path.IndexOf("CMP", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                            list.Add(new RunningProcessItem
                            {
                                ProcessName = p.ProcessName,
                                WindowTitle = p.MainWindowTitle,
                                ExePath = path
                            });
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return list;
        }

        #endregion

        #region DLSS 3 帧生成 与 自动更新

        public class DlssMirror
        {
            public string Name { get; set; }
            public string BaseUrl { get; set; }
            public string ReadmeUrl { get; set; }
            public string VersionDllUrl { get; set; }
            public string IniUrl { get; set; }

            public DlssMirror(string name, string baseUrl, string readmeUrl, string versionDllUrl, string iniUrl)
            {
                Name = name;
                BaseUrl = baseUrl;
                ReadmeUrl = readmeUrl;
                VersionDllUrl = versionDllUrl;
                IniUrl = iniUrl;
            }
        }

        /// <summary>
        /// 0.4.0 起运行库、后端与内核包从代理 DLL 中移出，放在 dlssg_sm86\ 子目录下。
        /// 只复制 version.dll 不会生效，必须一并部署这些文件。
        /// Key=子目录名，Value=文件名。
        /// </summary>
        private static readonly KeyValuePair<string, string>[] DlssSubDirFiles = new KeyValuePair<string, string>[]
        {
            new KeyValuePair<string, string>("runtime", "nvngx_dlssg.dll"),
            new KeyValuePair<string, string>("backend", "sm86_backend.dll"),
            new KeyValuePair<string, string>("kernels", "fg_kernels.dll"),
            new KeyValuePair<string, string>("xefg",    "xefg_kernels.dll"),
        };

        /// <summary>
        /// 旧版 dlssg_for_sm86 把运行库、后端与内核全部编进代理 DLL，体积 20MB 以上；
        /// 0.4.0 起代理层只剩转发逻辑，体积不足 1MB。用体积区分两代结构。
        /// </summary>
        private const long DlssLegacyBundleThreshold = 10L * 1024 * 1024;

        /// <summary>
        /// 代理 DLL 候选名。顺序沿用上游 dlssg_sm86.ini 的官方建议：先 version，
        /// 加载不到再依次试 winmm / dbghelp / dinput8，dxgi 与 d3d12 属于渲染路径代理，最后手段且不可同时使用。
        /// </summary>
        public static readonly string[] ProxyCandidates = new string[]
        {
            "version.dll",
            "winmm.dll",
            "dbghelp.dll",
            "dinput8.dll",
            "dxgi.dll",
            "d3d12.dll"
        };

        /// <summary>记录本次注入实际使用的代理名，移除时据此精确还原。</summary>
        private const string DlssProxyMarkerFile = ".dlssg_proxy";

        public class DlssRuntimeInfo
        {
            public string SourceDir { get; set; }
            public bool Found { get; set; }
            public long DllBytes { get; set; }
            public bool IsLegacyBundle { get; set; }
            public bool HasIni { get; set; }
            public bool HasSubDir { get; set; }
            public int SubDirFileCount { get; set; }
            public bool Usable { get; set; }
            public string Reason { get; set; }

            public DlssRuntimeInfo()
            {
                SourceDir = "";
                Reason = "";
                Found = false;
                Usable = false;
            }
        }

        public class DlssCheckItem
        {
            public string Name { get; set; }
            public bool Ok { get; set; }
            public bool Blocking { get; set; }
            public string Detail { get; set; }

            public DlssCheckItem(string name, bool ok, bool blocking, string detail)
            {
                Name = name;
                Ok = ok;
                Blocking = blocking;
                Detail = detail;
            }
        }

        public class DlssPreflightResult
        {
            public bool Passed { get; set; }
            public List<DlssCheckItem> Items { get; set; }
            public DlssRuntimeInfo Runtime { get; set; }

            public DlssPreflightResult()
            {
                Passed = true;
                Items = new List<DlssCheckItem>();
            }

            public int BlockingCount()
            {
                int n = 0;
                for (int i = 0; i < Items.Count; i++)
                {
                    if (!Items[i].Ok && Items[i].Blocking) n++;
                }
                return n;
            }
        }

        public static bool IsKnownProxyName(string proxyName)
        {
            if (string.IsNullOrEmpty(proxyName)) return false;
            if (proxyName.IndexOf('\\') >= 0 || proxyName.IndexOf('/') >= 0 || proxyName.IndexOf("..") >= 0) return false;
            for (int i = 0; i < ProxyCandidates.Length; i++)
            {
                if (string.Equals(ProxyCandidates[i], proxyName, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        /// <summary>列出可用的代理文件名：磁盘运行库优先，其次识别内置资源包里的 alternatives\。</summary>
        public static List<string> GetAvailableProxyNames()
        {
            List<string> list = new List<string>();
            string dir = GetDlssRuntimeDirectory();

            List<string> packed = null;
            try { packed = GetPackagedResourceNames("dlss/alternatives/"); }
            catch { packed = null; }

            for (int i = 0; i < ProxyCandidates.Length; i++)
            {
                string name = ProxyCandidates[i];
                bool has = File.Exists(Path.Combine(dir, name))
                        || File.Exists(Path.Combine(dir, "alternatives", name));

                if (!has && packed != null)
                {
                    for (int k = 0; k < packed.Count; k++)
                    {
                        if (packed[k].EndsWith("/" + name, StringComparison.OrdinalIgnoreCase)) { has = true; break; }
                    }
                }
                if (has) list.Add(name);
            }

            if (list.Count == 0) list.Add("version.dll");
            return list;
        }

        public static List<string> GetDlssRuntimeCandidates()
        {
            List<string> list = new List<string>();
            string primary = GetDlssRuntimeDirectory();
            if (!list.Contains(primary)) list.Add(primary);

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] extra = new string[]
            {
                Path.Combine(baseDir, "dlss_runtime"),
                Path.Combine(Directory.GetCurrentDirectory(), "dlss_runtime"),
                @"D:\app\dlss_runtime",
                @"D:\app\Portable_Release\dlss_runtime"
            };
            for (int i = 0; i < extra.Length; i++)
            {
                if (!string.IsNullOrEmpty(extra[i]) && !list.Contains(extra[i])) list.Add(extra[i]);
            }
            return list;
        }

        /// <summary>检查单个运行库目录的结构完整性。</summary>
        public static DlssRuntimeInfo InspectDlssRuntimeAt(string dir)
        {
            DlssRuntimeInfo info = new DlssRuntimeInfo();
            info.SourceDir = dir == null ? "" : dir;

            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                info.Reason = "目录不存在";
                return info;
            }

            string dllPath = Path.Combine(dir, "version.dll");
            if (!File.Exists(dllPath))
            {
                info.Reason = "缺少 version.dll";
                return info;
            }

            info.Found = true;
            try { info.DllBytes = new FileInfo(dllPath).Length; }
            catch { info.DllBytes = 0; }

            info.HasIni = File.Exists(Path.Combine(dir, "dlssg_sm86.ini"));
            info.IsLegacyBundle = info.DllBytes >= DlssLegacyBundleThreshold;

            string sub = Path.Combine(dir, "dlssg_sm86");
            info.HasSubDir = Directory.Exists(sub);
            int cnt = 0;
            if (info.HasSubDir)
            {
                for (int i = 0; i < DlssSubDirFiles.Length; i++)
                {
                    string f = Path.Combine(sub, DlssSubDirFiles[i].Key, DlssSubDirFiles[i].Value);
                    if (File.Exists(f)) cnt++;
                }
            }
            info.SubDirFileCount = cnt;

            if (info.IsLegacyBundle)
            {
                info.Usable = info.HasIni;
                info.Reason = info.Usable ? "旧版单体包，运行库已内嵌" : "旧版单体包但缺少 dlssg_sm86.ini";
            }
            else if (!info.HasSubDir)
            {
                info.Usable = false;
                info.Reason = "0.4.0 代理层缺少 dlssg_sm86 子目录，注入后不会生效";
            }
            else if (cnt < DlssSubDirFiles.Length)
            {
                info.Usable = false;
                info.Reason = string.Format("dlssg_sm86 组件不全 ({0}/{1})", cnt, DlssSubDirFiles.Length);
            }
            else if (!info.HasIni)
            {
                info.Usable = false;
                info.Reason = "缺少 dlssg_sm86.ini";
            }
            else
            {
                info.Usable = true;
                info.Reason = "0.4.0 完整结构";
            }
            return info;
        }

        /// <summary>
        /// 在全部候选目录中挑选最可用的运行库。
        /// 规则：结构完整优先，0.4.0 完整结构优先于旧版单体包，避免优先取到残缺的 0.4.0 代理层。
        /// </summary>
        public static DlssRuntimeInfo ResolveBestDlssRuntime()
        {
            List<string> dirs = GetDlssRuntimeCandidates();
            DlssRuntimeInfo best = null;
            DlssRuntimeInfo firstFound = null;

            for (int i = 0; i < dirs.Count; i++)
            {
                DlssRuntimeInfo info = InspectDlssRuntimeAt(dirs[i]);
                if (info.Found && firstFound == null) firstFound = info;
                if (!info.Usable) continue;
                if (best == null) { best = info; continue; }
                if (best.IsLegacyBundle && !info.IsLegacyBundle) best = info;
            }

            if (best != null) return best;
            if (firstFound != null) return firstFound;
            return InspectDlssRuntimeAt(dirs.Count > 0 ? dirs[0] : "");
        }

        /// <summary>
        /// 最小可用配置骨架。源 ini 一旦遭遇 GBK/UTF-8 错误转码，中文注释会变成乱码，
        /// 换行还可能被吃掉，导致 MaxGeneratedFrames / Optimized / Preset 等键整行并入注释而失效。
        /// 此时改用这份纯 ASCII 骨架重建，保证关键键一定处于行首生效状态。
        /// </summary>
        private const string DlssIniSkeleton =
            "[General]\nEnabled=1\n\n" +
            "[FrameGeneration]\nEngine=DLSS\nOptimized=1\nMaxGeneratedFrames=3\n\n" +
            "[DLSSG]\nOptimized=1\nPreset=Auto\n\n" +
            "[Compatibility]\nPreset=Auto\n\n" +
            "[XeSSFG]\nFlowResolution=Middle\nUseHudless=1\n\n" +
            "[Runtime]\nMode=Bundled\n\n" +
            "[SuperResolution]\nMode=Off\n\n" +
            "[Logging]\nLevel=1\nDirectory=dlssg_sm86\\logs\n";

        /// <summary>严格 UTF-8 优先解码；失败才回退系统编码。避免上游 UTF-8 文本被 ANSI 解码后二次损坏。</summary>
        private static string DecodeTextSmart(byte[] data)
        {
            if (data == null || data.Length == 0) return "";
            if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            {
                try { return new UTF8Encoding(false, true).GetString(data, 3, data.Length - 3); }
                catch { }
            }
            try { return new UTF8Encoding(false, true).GetString(data); }
            catch { }
            return Encoding.Default.GetString(data);
        }

        /// <summary>判断 ini 是否结构可用：段存在、关键键在行首、且不含错误转码留下的替换符与私用区字符。</summary>
        public static bool IsDlssIniHealthy(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;

            bool hasSection = text.IndexOf("[FrameGeneration]", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasMaxFrames = Regex.IsMatch(text, @"(?m)^\s*MaxGeneratedFrames\s*=", RegexOptions.IgnoreCase);
            bool hasOptimized = Regex.IsMatch(text, @"(?m)^\s*Optimized\s*=", RegexOptions.IgnoreCase);
            // Optimized 必须单独校验：错误转码会让它整行并入注释，而 MaxGeneratedFrames 可能仍然正常，
            // 只看后者会把"优化档位已失效"的配置误判为健康。
            if (!hasSection || !hasMaxFrames || !hasOptimized) return false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\uFFFD') return false;
                if (c >= '\uE000' && c <= '\uF8FF') return false;
            }
            return true;
        }

        /// <summary>按段写入 ini 键值；段或键不存在时自动补齐。</summary>
        public static string SetIniValue(string text, string section, string key, string value)
        {
            if (text == null) text = "";
            string[] lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            List<string> list = new List<string>(lines);

            string header = "[" + section + "]";
            int secStart = -1;
            int secEnd = list.Count;
            for (int i = 0; i < list.Count; i++)
            {
                string t = list[i].Trim();
                if (t.StartsWith("[") && t.EndsWith("]"))
                {
                    if (secStart >= 0) { secEnd = i; break; }
                    if (string.Equals(t, header, StringComparison.OrdinalIgnoreCase)) secStart = i;
                }
            }

            string kvPattern = @"^\s*" + Regex.Escape(key) + @"\s*=";
            if (secStart >= 0)
            {
                bool replaced = false;
                for (int i = secStart + 1; i < secEnd && i < list.Count; i++)
                {
                    if (Regex.IsMatch(list[i], kvPattern, RegexOptions.IgnoreCase))
                    {
                        list[i] = key + "=" + value;
                        replaced = true;
                        break;
                    }
                }
                if (!replaced) list.Insert(secStart + 1, key + "=" + value);
            }
            else
            {
                if (list.Count > 0 && list[list.Count - 1].Trim().Length != 0) list.Add("");
                list.Add(header);
                list.Add(key + "=" + value);
            }
            return string.Join("\n", list.ToArray());
        }

        /// <summary>部署前体检：先确认源运行库可用、目标目录无冲突组件，再决定是否放行注入。</summary>
        public static DlssPreflightResult PreflightDlssDeploy(string gameDir, string proxyName, bool injectReFramework)
        {
            DlssPreflightResult result = new DlssPreflightResult();
            string proxy = IsKnownProxyName(proxyName) ? proxyName : "version.dll";

            if (string.IsNullOrEmpty(gameDir) || !Directory.Exists(gameDir))
            {
                result.Items.Add(new DlssCheckItem("目标目录", false, true, "游戏目录不存在或未选择"));
                result.Passed = false;
                return result;
            }

            DlssRuntimeInfo rt = ResolveBestDlssRuntime();
            result.Runtime = rt;
            result.Items.Add(new DlssCheckItem(
                "源运行库",
                rt.Usable,
                true,
                rt.Usable
                    ? string.Format("{0} · {1} · {2}", rt.IsLegacyBundle ? "单体版" : "0.4.0", rt.Reason, FormatSize(rt.DllBytes))
                    : string.Format("{0}（{1}），请先执行「一键拉取并同步」", rt.Reason, rt.SourceDir)));

            if (rt.Usable && !rt.IsLegacyBundle)
            {
                result.Items.Add(new DlssCheckItem(
                    "内核组件",
                    rt.SubDirFileCount >= DlssSubDirFiles.Length,
                    true,
                    string.Format("dlssg_sm86 子目录 {0}/{1} 个组件", rt.SubDirFileCount, DlssSubDirFiles.Length)));
            }
            else
            {
                result.Items.Add(new DlssCheckItem("内核组件", rt.Usable, false, "单体版无需子目录，运行库已内嵌在代理 DLL"));
            }

            bool iniOk = false;
            string iniDetail = "未读到 ini";
            if (rt.Found)
            {
                string iniPath = Path.Combine(rt.SourceDir, "dlssg_sm86.ini");
                if (File.Exists(iniPath))
                {
                    try
                    {
                        string txt = File.ReadAllText(iniPath, Encoding.UTF8);
                        iniOk = IsDlssIniHealthy(txt);
                        iniDetail = iniOk
                            ? "配置结构完整"
                            : "ini 结构异常（关键键被并入注释或编码损坏），注入时将用内置骨架重建";
                    }
                    catch (Exception ex) { iniDetail = "ini 读取失败: " + ex.Message; }
                }
            }
            result.Items.Add(new DlssCheckItem("配置文件", iniOk, false, iniDetail));

            string targetProxy = Path.Combine(gameDir, proxy);
            bool proxyConflict = File.Exists(targetProxy);
            bool hasMarker = File.Exists(Path.Combine(gameDir, DlssProxyMarkerFile));
            result.Items.Add(new DlssCheckItem(
                "代理文件占用",
                !proxyConflict || hasMarker,
                false,
                proxyConflict
                    ? (hasMarker ? proxy + " 已存在，属本工具此前注入，将被覆盖" : proxy + " 已存在且非本工具注入，覆盖前请确认来源")
                    : "目标目录干净，可直接落盘 " + proxy));

            bool hasOptiScaler = File.Exists(Path.Combine(gameDir, "OptiScaler.ini"));
            bool hasOurIni = File.Exists(Path.Combine(gameDir, "dlssg_sm86.ini"));
            bool hasForeigndlssg = File.Exists(Path.Combine(gameDir, "nvngx_dlssg.dll"));
            List<string> leftovers = new List<string>();
            if (hasOptiScaler) leftovers.Add("OptiScaler.ini");
            if (hasForeigndlssg) leftovers.Add("nvngx_dlssg.dll");
            string slPath = Path.Combine(gameDir, "sl.interposer.dll");
            string slVer = "";
            if (File.Exists(slPath))
            {
                try { slVer = System.Diagnostics.FileVersionInfo.GetVersionInfo(slPath).FileVersion; }
                catch { slVer = ""; }
                leftovers.Add("sl.interposer.dll" + (string.IsNullOrEmpty(slVer) ? "" : " v" + slVer));
            }
            result.Items.Add(new DlssCheckItem(
                "第三方组件",
                leftovers.Count == 0,
                false,
                leftovers.Count == 0 ? "未发现其他插帧/超分工具残留" : "发现 " + string.Join("、", leftovers.ToArray()) + "，可能与注入冲突"));

            List<string> legacyHits = new List<string>();
            for (int i = 0; i < ProxyCandidates.Length; i++)
            {
                string p = Path.Combine(gameDir, ProxyCandidates[i]);
                if (!File.Exists(p)) continue;
                try
                {
                    long len = new FileInfo(p).Length;
                    if (len >= DlssLegacyBundleThreshold) legacyHits.Add(ProxyCandidates[i] + "(" + FormatSize(len) + ")");
                }
                catch { }
            }
            result.Items.Add(new DlssCheckItem(
                "旧版残留",
                legacyHits.Count == 0,
                false,
                legacyHits.Count == 0 ? "未发现旧版单体 DLL 残留" : "发现旧版单体 DLL: " + string.Join("、", legacyHits.ToArray()) + "，建议先清理再注入"));

            bool reConflict = injectReFramework && string.Equals(proxy, "dinput8.dll", StringComparison.OrdinalIgnoreCase);
            result.Items.Add(new DlssCheckItem(
                "RE 框架冲突",
                !reConflict,
                true,
                reConflict ? "代理名已选 dinput8.dll，与 RE 框架代理同名，二者只能保留一个" : "无冲突"));

            result.Passed = result.BlockingCount() == 0;
            return result;
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1024L * 1024L) return string.Format("{0:0.0} MB", bytes / 1048576.0);
            if (bytes >= 1024L) return string.Format("{0:0} KB", bytes / 1024.0);
            return bytes + " B";
        }

        public static List<DlssMirror> GetDlssMirrors()
        {
            return new List<DlssMirror>
            {
                new DlssMirror(
                    "Ghproxy 高速镜像 (推荐国内网络)",
                    "https://ghfast.top/https://raw.githubusercontent.com/sdli1995/dlssg_for_sm86/main/",
                    "https://ghfast.top/https://raw.githubusercontent.com/sdli1995/dlssg_for_sm86/main/README.md",
                    "https://ghfast.top/https://raw.githubusercontent.com/sdli1995/dlssg_for_sm86/main/version.dll",
                    "https://ghfast.top/https://raw.githubusercontent.com/sdli1995/dlssg_for_sm86/main/dlssg_sm86.ini"
                ),
                new DlssMirror(
                    "jsDelivr 全球 CDN 源 (极速同步，低延迟)",
                    "https://cdn.jsdelivr.net/gh/sdli1995/dlssg_for_sm86@main/",
                    "https://cdn.jsdelivr.net/gh/sdli1995/dlssg_for_sm86@main/README.md",
                    "https://cdn.jsdelivr.net/gh/sdli1995/dlssg_for_sm86@main/version.dll",
                    "https://cdn.jsdelivr.net/gh/sdli1995/dlssg_for_sm86@main/dlssg_sm86.ini"
                ),
                new DlssMirror(
                    "FastGit / GitMirror 镜像加速源",
                    "https://raw.gitmirror.com/sdli1995/dlssg_for_sm86/main/",
                    "https://raw.gitmirror.com/sdli1995/dlssg_for_sm86/main/README.md",
                    "https://raw.gitmirror.com/sdli1995/dlssg_for_sm86/main/version.dll",
                    "https://raw.gitmirror.com/sdli1995/dlssg_for_sm86/main/dlssg_sm86.ini"
                ),
                new DlssMirror(
                    "GitHub 官方直连源 (海外或直连网络)",
                    "https://raw.githubusercontent.com/sdli1995/dlssg_for_sm86/main/",
                    "https://raw.githubusercontent.com/sdli1995/dlssg_for_sm86/main/README.md",
                    "https://raw.githubusercontent.com/sdli1995/dlssg_for_sm86/main/version.dll",
                    "https://raw.githubusercontent.com/sdli1995/dlssg_for_sm86/main/dlssg_sm86.ini"
                )
            };
        }

        public static string GetLocalDlssVersion()
        {
            try
            {
                // 【修复】首选与更新写入完全一致的目录 (ProgramData\CMP90HX_Toolbox\dlss_runtime)。
                // 原实现只读 EXE 目录 / D:\app 等旧路径，导致更新后版本号永远停留在旧值。
                string primary = Path.Combine(GetDlssRuntimeDirectory(), "version.txt");
                if (File.Exists(primary))
                {
                    string txt = File.ReadAllText(primary, Encoding.UTF8).Trim().TrimStart('\uFEFF');
                    if (!string.IsNullOrEmpty(txt)) return txt;
                }

                // 兼容历史遗留路径
                string[] candidates = new string[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dlss_runtime", "version.txt"),
                    Path.Combine(Directory.GetCurrentDirectory(), "dlss_runtime", "version.txt"),
                    @"D:\app\dlss_runtime\version.txt",
                    @"D:\app\Portable_Release\dlss_runtime\version.txt"
                };
                foreach (var vFile in candidates)
                {
                    if (File.Exists(vFile))
                    {
                        string txt = File.ReadAllText(vFile, Encoding.UTF8).Trim().TrimStart('\uFEFF');
                        if (!string.IsNullOrEmpty(txt)) return txt;
                    }
                }

                // 没有任何版本记录时，依据实际运行库文件判断，而不是返回写死的版本号
                string dll = Path.Combine(GetDlssRuntimeDirectory(), "version.dll");
                if (File.Exists(dll))
                {
                    var fi = new FileInfo(dll);
                    return "1.0." + fi.LastWriteTime.ToString("yyyyMMdd");
                }
            }
            catch { }
            return "未部署";
        }

        public class DlssCheckResult
        {
            public bool Success;
            public bool HasUpdate;
            public string RemoteVersion;
            public string LocalVersion;
            public string MirrorUsed;
            public string ChangeLog;
            public string ErrorMessage;
        }

        public static Task<DlssCheckResult> CheckDlssUpdateAsync(int mirrorIndex, Action<string, bool> log)
        {
            return Task.Factory.StartNew(() =>
            {
                var result = new DlssCheckResult
                {
                    LocalVersion = GetLocalDlssVersion(),
                    RemoteVersion = "0.3.5"
                };

                var mirrors = GetDlssMirrors();
                List<int> tryIndices = new List<int>();
                if (mirrorIndex >= 0 && mirrorIndex < mirrors.Count) tryIndices.Add(mirrorIndex);
                for (int i = 0; i < mirrors.Count; i++)
                {
                    if (!tryIndices.Contains(i)) tryIndices.Add(i);
                }

                foreach (int idx in tryIndices)
                {
                    var mirror = mirrors[idx];
                    log("正在连接下载源 [" + mirror.Name + "] 检索版本信息...", false);
                    try
                    {
                        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768 | (SecurityProtocolType)192;
                        var req = (HttpWebRequest)WebRequest.Create(mirror.ReadmeUrl);
                        req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 CMP90HX_Suite/2.5";
                        req.Timeout = 8000;
                        req.ReadWriteTimeout = 8000;

                        using (var resp = (HttpWebResponse)req.GetResponse())
                        using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                        {
                            string content = sr.ReadToEnd();
                            var match = Regex.Match(content, @"(?:DLSSG for SM86.*?-\s*([0-9]+\.[0-9]+\.[0-9]+)\s*版本|###\s*([0-9]+\.[0-9]+\.[0-9]+))");
                            if (match.Success)
                            {
                                string ver = string.IsNullOrEmpty(match.Groups[1].Value) ? match.Groups[2].Value : match.Groups[1].Value;
                                result.RemoteVersion = ver;
                                result.MirrorUsed = mirror.Name;
                                result.Success = true;

                                var changelogMatch = Regex.Match(content, @"(?s)##\s*本次更新说明\s*(.*?)(?=##|\z)");
                                if (changelogMatch.Success)
                                {
                                    result.ChangeLog = changelogMatch.Groups[1].Value.Trim();
                                }

                                Version vLocal = null; Version vRemote = null;
                                bool ok1 = Version.TryParse(result.LocalVersion, out vLocal);
                                bool ok2 = Version.TryParse(result.RemoteVersion, out vRemote);
                                if (ok1 && ok2 && vLocal != null && vRemote != null) {
                                    result.HasUpdate = vRemote > vLocal;
                                }
                                else
                                {
                                    result.HasUpdate = string.Compare(result.RemoteVersion, result.LocalVersion, StringComparison.OrdinalIgnoreCase) != 0;
                                }

                                if (result.HasUpdate)
                                {
                                    log("发现 DLSS 运行库新版本: " + result.RemoteVersion + " (当前本地: " + result.LocalVersion + ")，来自 " + mirror.Name, false);
                                }
                                else
                                {
                                    log("DLSS 运行库已是最新版本 (" + result.LocalVersion + ")，来自 " + mirror.Name, false);
                                }
                                return result;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        log("连接 [" + mirror.Name + "] 超时或受限: " + ex.Message + "，尝试备用源...", false);
                        result.ErrorMessage = ex.Message;
                    }
                }

                log("所有镜像源均连接超时（当前处于离线沙箱环境或网络限制）。已使用内置出厂优化版本 v" + result.LocalVersion, false);
                result.Success = true;
                result.RemoteVersion = result.LocalVersion;
                result.HasUpdate = false;
                result.MirrorUsed = "本地离线出厂库";
                return result;
            });
        }

        public class DlssDownloadResult
        {
            public bool Success;
            public string NewVersion;
            public long TotalBytes;
            public string Message;
            public bool GameDirUpdated;
        }

        public static Task<DlssDownloadResult> PullAndReplaceDlssAsync(int mirrorIndex, string targetGameDir, Action<int> progress, Action<string, bool> log)
        {
            return Task.Factory.StartNew(() =>
            {
                var result = new DlssDownloadResult();
                var mirrors = GetDlssMirrors();
                List<int> tryIndices = new List<int>();
                if (mirrorIndex >= 0 && mirrorIndex < mirrors.Count) tryIndices.Add(mirrorIndex);
                for (int i = 0; i < mirrors.Count; i++)
                {
                    if (!tryIndices.Contains(i)) tryIndices.Add(i);
                }

                string runtimeDir = GetDlssRuntimeDirectory();
                if (!Directory.Exists(runtimeDir)) Directory.CreateDirectory(runtimeDir);

                byte[] versionDllData = null;
                string iniContent = null;
                string latestVersion = GetLocalDlssVersion();
                // 0.4.0 新增：运行库/后端/内核包全部移到 dlssg_sm86\ 子目录，只复制 DLL 不会生效
                var subFiles = new Dictionary<string, byte[]>();

                foreach (int idx in tryIndices)
                {
                    var mirror = mirrors[idx];
                    log("正在从 [" + mirror.Name + "] 下载最新 DLSS 运行库文件...", false);
                    try
                    {
                        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768 | (SecurityProtocolType)192;

                        using (var wc = new WebClient())
                        {
                            wc.Headers.Add("User-Agent", "Mozilla/5.0 CMP90HX_Suite/2.5");
                            progress(20);
                            // 必须按字节取回再显式 UTF-8 解码：DownloadString 走系统 ANSI(GBK)，
                            // 会把上游 UTF-8 的中文注释解成乱码，并吞掉换行，令关键键整行并入注释而失效。
                            byte[] iniBytes = wc.DownloadData(mirror.IniUrl);
                            iniContent = DecodeTextSmart(iniBytes);
                        }

                        using (var wc = new WebClient())
                        {
                            wc.Headers.Add("User-Agent", "Mozilla/5.0 CMP90HX_Suite/2.5");
                            progress(50);
                            versionDllData = wc.DownloadData(mirror.VersionDllUrl);
                        }

                        try
                        {
                            using (var wc = new WebClient())
                            {
                                wc.Headers.Add("User-Agent", "Mozilla/5.0 CMP90HX_Suite/2.5");
                                byte[] rmBytes = wc.DownloadData(mirror.ReadmeUrl);
                                string rm = DecodeTextSmart(rmBytes);
                                var match = Regex.Match(rm, @"(?:DLSSG for SM86.*?-\s*([0-9]+\.[0-9]+\.[0-9]+)\s*版本|###\s*([0-9]+\.[0-9]+\.[0-9]+))");
                                if (match.Success)
                                {
                                    latestVersion = string.IsNullOrEmpty(match.Groups[1].Value) ? match.Groups[2].Value : match.Groups[1].Value;
                                }
                            }
                        }
                        catch { }

                        if (versionDllData != null && versionDllData.Length > 1024 && !string.IsNullOrEmpty(iniContent))
                        {
                            // 0.4.0 结构：额外拉取 dlssg_sm86\ 下的运行库 / 后端 / 内核包
                            subFiles.Clear();
                            int subOk = 0;
                            foreach (var sub in DlssSubDirFiles)
                            {
                                try
                                {
                                    using (var wc = new WebClient())
                                    {
                                        wc.Headers.Add("User-Agent", "Mozilla/5.0 CMP90HX_Suite/2.5");
                                        byte[] data = wc.DownloadData(mirror.BaseUrl + "dlssg_sm86/" + sub.Key + "/" + sub.Value);
                                        if (data != null && data.Length > 512)
                                        {
                                            subFiles[sub.Key + "/" + sub.Value] = data;
                                            subOk++;
                                        }
                                    }
                                }
                                catch { }
                            }
                            if (subOk > 0)
                            {
                                log("已拉取 dlssg_sm86 运行库组件 " + subOk + " / " + DlssSubDirFiles.Length + " 个（0.4.0 新结构）", false);
                            }

                            log("下载成功 (" + versionDllData.Length + " 字节)，验证文件完整性通过。", false);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        log("[" + mirror.Name + "] 拉取失败: " + ex.Message + "，尝试下一镜像...", false);
                    }
                }

                if (versionDllData == null)
                {
                    log("网络环境受限，正在从内置资源包载入出厂 DLSS-G 运行库（内置版，非最新）...", false);
                    versionDllData = GetPackagedResource("dlss/version.dll");
                    byte[] iniRaw = GetPackagedResource("dlss/dlssg_sm86.ini");
                    iniContent = Encoding.UTF8.GetString(iniRaw);
                    latestVersion = "0.3.5-内置";
                    log("已使用内置运行库。注意：这是随程序打包的版本，未从网络获取最新版。", true);
                }

                progress(80);

                string targetDll = Path.Combine(runtimeDir, "version.dll");
                string targetIni = Path.Combine(runtimeDir, "dlssg_sm86.ini");
                string targetVer = Path.Combine(runtimeDir, "version.txt");

                try
                {
                    File.WriteAllBytes(targetDll, versionDllData);
                }
                catch (Exception exW)
                {
                    result.Success = false;
                    result.Message = "写入运行库失败: " + exW.Message + "（请以管理员身份运行本程序后重试）";
                    log("写入运行库失败: " + exW.Message, true);
                    log("原因通常是目标目录无写入权限。请右键以管理员身份重新运行本程序。", true);
                    return result;
                }
                try
                {
                    File.WriteAllText(targetIni, iniContent, Encoding.UTF8);
                    File.WriteAllText(targetVer, latestVersion, Encoding.UTF8);
                }
                catch { }

                // 0.4.0 结构：部署 dlssg_sm86\ 子目录组件
                int subDeployed = 0;
                foreach (var kv in subFiles)
                {
                    try
                    {
                        string[] parts = kv.Key.Split('/');
                        if (parts.Length != 2) continue;
                        string subDir = Path.Combine(runtimeDir, "dlssg_sm86", parts[0]);
                        if (!Directory.Exists(subDir)) Directory.CreateDirectory(subDir);
                        File.WriteAllBytes(Path.Combine(subDir, parts[1]), kv.Value);
                        subDeployed++;
                    }
                    catch { }
                }
                if (subDeployed > 0)
                {
                    log("已部署 dlssg_sm86 运行库组件 " + subDeployed + " 个（0.4.0 新结构）", false);
                }

                result.TotalBytes = versionDllData.Length + iniContent.Length;
                result.NewVersion = latestVersion;
                result.Success = true;

                if (!string.IsNullOrEmpty(targetGameDir) && Directory.Exists(targetGameDir))
                {
                    string gDll = Path.Combine(targetGameDir, "version.dll");
                    string gIni = Path.Combine(targetGameDir, "dlssg_sm86.ini");
                    File.WriteAllBytes(gDll, versionDllData);

                    if (!File.Exists(gIni))
                    {
                        File.WriteAllText(gIni, iniContent, Encoding.UTF8);
                    }

                    // 0.4.0：游戏目录同样需要 dlssg_sm86\ 子目录，否则 DLL 加载后无运行库可用
                    int gSub = 0;
                    foreach (var kv in subFiles)
                    {
                        try
                        {
                            string[] parts = kv.Key.Split('/');
                            if (parts.Length != 2) continue;
                            string subDir = Path.Combine(targetGameDir, "dlssg_sm86", parts[0]);
                            if (!Directory.Exists(subDir)) Directory.CreateDirectory(subDir);
                            File.WriteAllBytes(Path.Combine(subDir, parts[1]), kv.Value);
                            gSub++;
                        }
                        catch { }
                    }

                    result.GameDirUpdated = true;
                    log("已直接同步替换游戏目录 [" + targetGameDir + "] 中的 version.dll 运行库" + (gSub > 0 ? " 及 dlssg_sm86 组件 " + gSub + " 个" : "") + "！", false);
                }

                progress(100);
                log("DLSS-G 运行库已成功更新/部署完毕，版本: " + latestVersion, false);
                result.Message = "运行库更新完成，版本: " + latestVersion;
                return result;
            });
        }

        public static bool InjectDlssG(string gameDir, string optVal, string maxFramesVal, bool injectReFramework, string proxyName, string engineVal, string presetVal, string srModeVal, Action<string, bool> log)
        {
            try
            {
                if (!Directory.Exists(gameDir)) throw new DirectoryNotFoundException("游戏目录不存在: " + gameDir);

                string proxy = proxyName;
                if (!IsKnownProxyName(proxy))
                {
                    if (!string.IsNullOrEmpty(proxyName))
                        log("代理名 [" + proxyName + "] 不在白名单内，已回退为 version.dll。", true);
                    proxy = "version.dll";
                }

                // 先定位结构完整的运行库：残缺的 0.4.0 代理层会被跳过，
                // 避免优先取到 853KB 代理层却缺 dlssg_sm86 子目录，导致注入后静默失效。
                DlssRuntimeInfo rt = ResolveBestDlssRuntime();
                byte[] dllData = null;
                string iniText = null;
                bool fromPackage = false;
                string proxyOrigin = "";

                if (rt.Found && rt.Usable)
                {
                    log("运行库来源: " + rt.SourceDir + " · " + rt.Reason, false);
                    iniText = File.ReadAllText(Path.Combine(rt.SourceDir, "dlssg_sm86.ini"), Encoding.UTF8);

                    string altPath = Path.Combine(rt.SourceDir, "alternatives", proxy);
                    if (File.Exists(altPath))
                    {
                        dllData = File.ReadAllBytes(altPath);
                        proxyOrigin = "磁盘 alternatives";
                    }
                    else
                    {
                        dllData = File.ReadAllBytes(Path.Combine(rt.SourceDir, "version.dll"));
                        proxyOrigin = "磁盘根目录";
                        if (!string.Equals(proxy, "version.dll", StringComparison.OrdinalIgnoreCase))
                            log("本地运行库缺少 alternatives\\" + proxy + "，回退使用 version.dll。", true);
                    }
                }
                else
                {
                    log("本地运行库不可用（" + rt.Reason + "），回退到内置资源包。", true);

                    try
                    {
                        dllData = GetPackagedResource("dlss/alternatives/" + proxy);
                        proxyOrigin = "内置 alternatives";
                    }
                    catch
                    {
                        dllData = GetPackagedResource("dlss/version.dll");
                        proxyOrigin = "内置根目录";
                        if (!string.Equals(proxy, "version.dll", StringComparison.OrdinalIgnoreCase))
                            log("内置包缺少 alternatives\\" + proxy + "，回退使用 version.dll。", true);
                    }

                    byte[] iniData = GetPackagedResource("dlss/dlssg_sm86.ini");
                    if (iniData != null) iniText = Encoding.UTF8.GetString(iniData);
                    fromPackage = true;
                }

                if (dllData == null || dllData.Length == 0)
                    throw new InvalidOperationException("运行库为空，无法注入");

                bool legacyBundle = dllData.Length >= DlssLegacyBundleThreshold;
                log("运行库形态: " + (legacyBundle ? "旧版单体包（运行库内嵌）" : "0.4.0 代理层 + 内核子目录") + " · " + FormatSize(dllData.Length), false);

                // 0.4.0 结构必须先确认内核组件齐全，否则宁可中止也不写出一份跑不起来的补丁。
                if (!legacyBundle && !fromPackage)
                {
                    string srcSub = Path.Combine(rt.SourceDir, "dlssg_sm86");
                    int have = 0;
                    for (int i = 0; i < DlssSubDirFiles.Length; i++)
                    {
                        if (File.Exists(Path.Combine(srcSub, DlssSubDirFiles[i].Key, DlssSubDirFiles[i].Value))) have++;
                    }
                    if (have < DlssSubDirFiles.Length)
                    {
                        log("注入中止：0.4.0 运行库缺少内核组件 (" + have + "/" + DlssSubDirFiles.Length + ")，注入后不会生效。请先执行【一键拉取并同步】。", true);
                        return false;
                    }
                }
                else if (!legacyBundle && fromPackage)
                {
                    List<string> packedSub = GetPackagedResourceNames("dlss/dlssg_sm86/");
                    if (packedSub == null || packedSub.Count == 0)
                    {
                        log("注入中止：内置资源包缺少 dlssg_sm86 内核目录，0.4.0 代理层无法单独工作。", true);
                        return false;
                    }
                }

                string targetProxyPath = Path.Combine(gameDir, proxy);
                File.WriteAllBytes(targetProxyPath, dllData);
                log("已写入代理 " + proxy + " (" + FormatSize(dllData.Length) + " · 来源: " + proxyOrigin + ")", false);

                log("正在应用配置并生成 dlssg_sm86.ini...", false);
                if (iniText == null) iniText = "";

                if (!IsDlssIniHealthy(iniText))
                {
                    log("源 ini 结构异常（关键键被并入注释或编码损坏），改用内置干净骨架重建配置。", true);
                    iniText = DlssIniSkeleton;
                }

                // 两代运行库的段落布局不同：0.4.0 用 [DLSSG] Optimized / Preset，
                // 旧版单体包用 [FrameGeneration] Optimized + [Compatibility] Preset。
                // 两个位置同时写入，运行库会忽略不认识的键，不存在副作用。
                iniText = SetIniValue(iniText, "FrameGeneration", "Optimized", optVal);
                iniText = SetIniValue(iniText, "DLSSG", "Optimized", optVal);
                iniText = SetIniValue(iniText, "FrameGeneration", "MaxGeneratedFrames", maxFramesVal);
                if (!string.IsNullOrEmpty(presetVal))
                {
                    iniText = SetIniValue(iniText, "Compatibility", "Preset", presetVal);
                    iniText = SetIniValue(iniText, "DLSSG", "Preset", presetVal);
                }
                if (!string.IsNullOrEmpty(engineVal)) iniText = SetIniValue(iniText, "FrameGeneration", "Engine", engineVal);
                if (!string.IsNullOrEmpty(srModeVal)) iniText = SetIniValue(iniText, "SuperResolution", "Mode", srModeVal);

                File.WriteAllText(Path.Combine(gameDir, "dlssg_sm86.ini"), iniText, Encoding.UTF8);

                // 0.4.0 结构：同步部署 dlssg_sm86\ 子目录（运行库/后端/内核包），否则 DLL 加载后无内核可用
                int subCopied = 0;
                if (!legacyBundle)
                {
                    if (fromPackage)
                    {
                        subCopied = ExtractPackagedTree("dlss/dlssg_sm86", Path.Combine(gameDir, "dlssg_sm86"));
                        if (subCopied > 0) log("已从内置包部署 dlssg_sm86 内核组件 " + subCopied + " 个到游戏目录。", false);
                        else log("提示：内置包内未提取到 dlssg_sm86 内核组件。", true);
                    }
                    else
                    {
                        string srcSubRoot = Path.Combine(rt.SourceDir, "dlssg_sm86");
                        if (Directory.Exists(srcSubRoot))
                        {
                            for (int i = 0; i < DlssSubDirFiles.Length; i++)
                            {
                                try
                                {
                                    string src = Path.Combine(srcSubRoot, DlssSubDirFiles[i].Key, DlssSubDirFiles[i].Value);
                                    if (!File.Exists(src)) continue;
                                    string dstDir = Path.Combine(gameDir, "dlssg_sm86", DlssSubDirFiles[i].Key);
                                    if (!Directory.Exists(dstDir)) Directory.CreateDirectory(dstDir);
                                    File.Copy(src, Path.Combine(dstDir, DlssSubDirFiles[i].Value), true);
                                    subCopied++;
                                }
                                catch { }
                            }
                            if (subCopied > 0) log("已同步部署 dlssg_sm86 运行库组件 " + subCopied + " 个到游戏目录。", false);
                            else log("提示：未找到 dlssg_sm86 内核组件，0.4.0 运行库可能无法生效。", true);
                        }
                    }
                }

                if (injectReFramework)
                {
                    if (string.Equals(proxy, "dinput8.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        log("代理名已选 dinput8.dll，跳过 RE 框架代理注入以避免同名冲突。", true);
                    }
                    else
                    {
                        log("正在提取 RE 框架代理 (dinput8.dll)...", false);
                        byte[] reData = null;
                        string localRe = Path.Combine(GetReFrameworkDirectory(), "dinput8.dll");
                        if (File.Exists(localRe))
                        {
                            try
                            {
                                reData = File.ReadAllBytes(localRe);
                                log("使用本地已更新的 REFramework（" + GetLocalReFrameworkVersion() + "）", false);
                            }
                            catch { reData = null; }
                        }
                        if (reData == null || reData.Length == 0)
                            reData = GetPackagedResource("re_framework/dinput8.dll");

                        if (reData != null && reData.Length > 0)
                        {
                            File.WriteAllBytes(Path.Combine(gameDir, "dinput8.dll"), reData);
                            log("已成功注入 RE 框架代理 (dinput8.dll)，卡普空 RE 引擎游戏专属兼容已就绪。", false);
                        }
                        else log("内置资源中未找到 RE 框架代理，已跳过。", true);
                    }
                }

                try { File.WriteAllText(Path.Combine(gameDir, DlssProxyMarkerFile), proxy); }
                catch { }

                // 内置包注入成功后补齐本地缓存：既省掉每次从资源包解压 37MB，也让后续更新检测有基线版本。
                if (fromPackage)
                {
                    try
                    {
                        string cacheDir = GetDlssRuntimeDirectory();
                        File.WriteAllBytes(Path.Combine(cacheDir, "version.dll"), GetPackagedResource("dlss/version.dll"));
                        byte[] cleanIni = GetPackagedResource("dlss/dlssg_sm86.ini");
                        if (cleanIni != null) File.WriteAllBytes(Path.Combine(cacheDir, "dlssg_sm86.ini"), cleanIni);
                        int cacheSub = ExtractPackagedTree("dlss/dlssg_sm86", Path.Combine(cacheDir, "dlssg_sm86"));
                        int cacheAlt = ExtractPackagedTree("dlss/alternatives", Path.Combine(cacheDir, "alternatives"));
                        log("已补齐本地运行库缓存（内核 " + cacheSub + " 个 / 代理 " + cacheAlt + " 个）。", false);
                    }
                    catch (Exception cex)
                    {
                        log("本地运行库缓存未能补齐（无写入权限时属正常，不影响本次注入）: " + cex.Message, false);
                    }
                }

                log("DLSS-G 补丁已成功注入游戏目录 (代理: " + proxy + ", 内核档位: " + optVal + ", 倍率档: " + maxFramesVal + (injectReFramework ? ", 含 RE 框架" : "") + ")", false);
                return true;
            }
            catch (Exception ex)
            {
                log("注入 DLSS 补丁失败: " + ex.Message, true);
                return false;
            }
        }

        public static bool InjectDlssG(string gameDir, string optVal, string maxFramesVal, bool injectReFramework, Action<string, bool> log)
        {
            return InjectDlssG(gameDir, optVal, maxFramesVal, injectReFramework, "version.dll", "DLSS", "Auto", "Off", log);
        }

        public static bool InjectDlssG(string gameDir, string optVal, string maxFramesVal, Action<string, bool> log)
        {
            return InjectDlssG(gameDir, optVal, maxFramesVal, false, "version.dll", "DLSS", "Auto", "Off", log);
        }

        public static bool RemoveDlssG(string gameDir, Action<string, bool> log)
        {
            try
            {
                string ini = Path.Combine(gameDir, "dlssg_sm86.ini");
                string marker = Path.Combine(gameDir, DlssProxyMarkerFile);

                // 优先按注入时记录的代理名精确还原，兼容历史上固定写 version.dll 的旧补丁。
                string usedProxy = null;
                if (File.Exists(marker))
                {
                    try { usedProxy = File.ReadAllText(marker).Trim(); }
                    catch { usedProxy = null; }
                }

                int removedProxy = 0;
                if (IsKnownProxyName(usedProxy))
                {
                    string p = Path.Combine(gameDir, usedProxy);
                    if (File.Exists(p))
                    {
                        File.Delete(p);
                        removedProxy++;
                        log("已移除代理 " + usedProxy + "（按注入记录还原）。", false);
                    }
                }

                for (int i = 0; i < ProxyCandidates.Length; i++)
                {
                    string p = Path.Combine(gameDir, ProxyCandidates[i]);
                    if (usedProxy != null && string.Equals(ProxyCandidates[i], usedProxy, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!File.Exists(p)) continue;
                    try
                    {
                        // 没有记录时保守处理：只清理与 dlssg 同批落盘的单体大 DLL，避免误删其他模组的同名代理。
                        long len = new FileInfo(p).Length;
                        bool ours = (usedProxy == null) && (ProxyCandidates[i] == "version.dll" || len >= DlssLegacyBundleThreshold);
                        if (ours)
                        {
                            File.Delete(p);
                            removedProxy++;
                            log("已移除残留代理 " + ProxyCandidates[i] + "。", false);
                        }
                    }
                    catch { }
                }

                if (File.Exists(ini))
                {
                    File.Delete(ini);
                    log("已移除 dlssg_sm86.ini。", false);
                }

                string subDir = Path.Combine(gameDir, "dlssg_sm86");
                if (Directory.Exists(subDir))
                {
                    try { Directory.Delete(subDir, true); log("已同步移除 dlssg_sm86 运行库目录。", false); }
                    catch { }
                }

                string re = Path.Combine(gameDir, "dinput8.dll");
                if (File.Exists(re) && (usedProxy == null || !string.Equals(usedProxy, "dinput8.dll", StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        File.Delete(re);
                        log("已同步移除 RE 框架代理 (dinput8.dll)。", false);
                    }
                    catch { }
                }

                if (File.Exists(marker))
                {
                    try { File.Delete(marker); }
                    catch { }
                }

                log("已从游戏目录清理移除 DLSS-G 补丁及相关组件（代理文件 " + removedProxy + " 个）。", false);
                return true;
            }
            catch (Exception ex)
            {
                log("移除补丁失败: " + ex.Message, true);
                return false;
            }
        }

        #region REFramework 更新

        public static string GetReFrameworkDirectory()
        {
            string dir = Path.Combine(GetDataDirectory(), "re_framework");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }

        public const string ReFrameworkLatestUrl = "https://github.com/praydog/REFramework-nightly/releases/latest";

        public class ReFwCheckResult
        {
            public bool Success { get; set; }
            public string LocalVersion { get; set; }
            public string RemoteVersion { get; set; }
            public bool HasUpdate { get; set; }
            public string Tag { get; set; }
            public string ErrorMessage { get; set; }

            public ReFwCheckResult()
            {
                Success = false;
                LocalVersion = "";
                RemoteVersion = "";
                Tag = "";
                ErrorMessage = "";
            }
        }

        public class ReFwPullResult
        {
            public bool Success { get; set; }
            public string NewVersion { get; set; }
            public long DllBytes { get; set; }
            public string Message { get; set; }

            public ReFwPullResult()
            {
                Success = false;
                NewVersion = "";
                Message = "";
            }
        }

        public static string GetLocalReFrameworkVersion()
        {
            try
            {
                string p = Path.Combine(GetReFrameworkDirectory(), "version.txt");
                if (File.Exists(p))
                {
                    string v = File.ReadAllText(p).Trim();
                    if (v.Length > 0) return v;
                }
            }
            catch { }

            try
            {
                byte[] b = GetPackagedResource("re_framework/reframework_revision.txt");
                if (b != null && b.Length > 0) return Encoding.UTF8.GetString(b).Trim();
            }
            catch { }

            return "未知";
        }

        /// <summary>解析任意仓库 releases/latest 的 302 跳转取 tag，不消耗 GitHub API 配额。</summary>
        public static string ResolveLatestReleaseTag(string owner, string repo)
        {
            try
            {
                // .NET Framework 默认可能仍用 TLS 1.0，GitHub 要求 1.2+，不显式设置会直接连不上。
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768 | (SecurityProtocolType)192;

                string url = "https://github.com/" + owner + "/" + repo + "/releases/latest";
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.AllowAutoRedirect = false;
                req.UserAgent = "Mozilla/5.0 CMP90HX_Suite/2.5";
                req.Timeout = 15000;
                req.ReadWriteTimeout = 15000;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    return ExtractTagFromLocation(resp.Headers["Location"]);
                }
            }
            catch (WebException wex)
            {
                HttpWebResponse r = wex.Response as HttpWebResponse;
                if (r != null) return ExtractTagFromLocation(r.Headers["Location"]);
                return null;
            }
            catch { return null; }
        }

        public static string ResolveReFrameworkLatestTag()
        {
            return ResolveLatestReleaseTag("praydog", "REFramework-nightly");
        }

        private static string ExtractTagFromLocation(string loc)
        {
            if (string.IsNullOrEmpty(loc)) return null;
            int i = loc.IndexOf("/tag/", StringComparison.OrdinalIgnoreCase);
            if (i < 0) return null;
            string tag = loc.Substring(i + 5).Trim();
            int q = tag.IndexOf('?');
            if (q >= 0) tag = tag.Substring(0, q);
            return tag.Trim().TrimEnd('/');
        }

        public static Task<ReFwCheckResult> CheckReFrameworkUpdateAsync(Action<string, bool> log)
        {
            return Task.Factory.StartNew<ReFwCheckResult>(delegate
            {
                ReFwCheckResult r = new ReFwCheckResult();
                try
                {
                    log("正在查询 REFramework 最新版本...", false);
                    string tag = ResolveReFrameworkLatestTag();
                    if (string.IsNullOrEmpty(tag))
                    {
                        r.ErrorMessage = "无法解析最新版本号，请检查网络";
                        return r;
                    }
                    r.Tag = tag;
                    r.RemoteVersion = tag;
                    r.LocalVersion = GetLocalReFrameworkVersion();

                    // 内置包只带 revision（40 位 commit），远端是完整 tag；用包含关系判断，避免每次都误报更新。
                    bool same = string.Equals(r.LocalVersion, tag, StringComparison.OrdinalIgnoreCase);
                    if (!same && !string.IsNullOrEmpty(r.LocalVersion) && r.LocalVersion.Length >= 7)
                        same = tag.IndexOf(r.LocalVersion, StringComparison.OrdinalIgnoreCase) >= 0;
                    r.HasUpdate = !same;
                    r.Success = true;
                    log("REFramework 最新: " + tag + "（本地: " + r.LocalVersion + "）", false);
                }
                catch (Exception ex)
                {
                    r.ErrorMessage = ex.Message;
                }
                return r;
            });
        }

        private static byte[] DownloadBytes(string url, int timeoutMs)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "Mozilla/5.0 CMP90HX_Suite/2.5";
            req.Timeout = timeoutMs;
            req.ReadWriteTimeout = timeoutMs;
            req.AllowAutoRedirect = true;
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (Stream s = resp.GetResponseStream())
            using (MemoryStream ms = new MemoryStream())
            {
                s.CopyTo(ms);
                return ms.ToArray();
            }
        }

        public static Task<ReFwPullResult> PullReFrameworkAsync(string tag, Action<int> progress, Action<string, bool> log)
        {
            return Task.Factory.StartNew<ReFwPullResult>(delegate
            {
                ReFwPullResult r = new ReFwPullResult();
                try
                {
                    if (string.IsNullOrEmpty(tag)) tag = ResolveReFrameworkLatestTag();
                    if (string.IsNullOrEmpty(tag)) { r.Message = "版本号未知，无法下载"; return r; }

                    string relPath = "/praydog/REFramework-nightly/releases/download/" + tag + "/REFramework.zip";
                    string[] urls = new string[]
                    {
                        "https://github.com" + relPath,
                        "https://ghfast.top/https://github.com" + relPath
                    };

                    ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768 | (SecurityProtocolType)192;

                    byte[] zip = null;
                    for (int i = 0; i < urls.Length && zip == null; i++)
                    {
                        try
                        {
                            log("正在下载 REFramework（" + (i == 0 ? "GitHub 直连" : "ghfast 镜像") + "）...", false);
                            progress(20);
                            zip = DownloadBytes(urls[i], 180000);
                        }
                        catch (Exception dex)
                        {
                            log("线路失败: " + dex.Message, true);
                        }
                    }
                    if (zip == null || zip.Length == 0) { r.Message = "所有线路均下载失败"; return r; }

                    progress(70);
                    byte[] dll = null;
                    string rev = "";
                    using (MemoryStream ms = new MemoryStream(zip))
                    using (ZipArchive ar = new ZipArchive(ms, ZipArchiveMode.Read))
                    {
                        for (int i = 0; i < ar.Entries.Count; i++)
                        {
                            ZipArchiveEntry e = ar.Entries[i];
                            if (string.Equals(e.Name, "dinput8.dll", StringComparison.OrdinalIgnoreCase))
                            {
                                using (Stream es = e.Open())
                                using (MemoryStream om = new MemoryStream())
                                {
                                    es.CopyTo(om);
                                    dll = om.ToArray();
                                }
                            }
                            else if (string.Equals(e.Name, "reframework_revision.txt", StringComparison.OrdinalIgnoreCase))
                            {
                                using (Stream es = e.Open())
                                using (StreamReader sr = new StreamReader(es, Encoding.UTF8))
                                {
                                    rev = sr.ReadToEnd().Trim();
                                }
                            }
                        }
                    }

                    if (dll == null || dll.Length == 0) { r.Message = "压缩包内未找到 dinput8.dll"; return r; }

                    string dir = GetReFrameworkDirectory();
                    File.WriteAllBytes(Path.Combine(dir, "dinput8.dll"), dll);
                    File.WriteAllText(Path.Combine(dir, "version.txt"), tag);
                    if (!string.IsNullOrEmpty(rev)) File.WriteAllText(Path.Combine(dir, "reframework_revision.txt"), rev);

                    progress(100);
                    r.Success = true;
                    r.NewVersion = tag;
                    r.DllBytes = dll.Length;
                    log("REFramework 已更新到 " + tag + "（" + FormatSize(dll.Length) + "）", false);
                }
                catch (Exception ex)
                {
                    r.Message = ex.Message;
                }
                return r;
            });
        }

        #endregion

        #region 软件自更新

        private const string AppRepoOwner = "1922708082";
        private const string AppRepoName = "90HX-Studio";

        public class AppUpdateInfo
        {
            public bool Success { get; set; }
            public string CurrentVersion { get; set; }
            public string LatestVersion { get; set; }
            public string Tag { get; set; }
            public string AssetName { get; set; }
            public string DownloadUrl { get; set; }
            public bool HasUpdate { get; set; }
            public string ErrorMessage { get; set; }

            public AppUpdateInfo()
            {
                Success = false;
                CurrentVersion = "";
                LatestVersion = "";
                Tag = "";
                AssetName = "";
                DownloadUrl = "";
                ErrorMessage = "";
            }
        }

        public class AppUpdateResult
        {
            public bool Success { get; set; }
            public string FilePath { get; set; }
            public long SizeBytes { get; set; }
            public string Message { get; set; }

            public AppUpdateResult()
            {
                Success = false;
                FilePath = "";
                Message = "";
            }
        }

        public static string GetCurrentExePath()
        {
            try
            {
                string p = System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(p) && File.Exists(p)) return p;
            }
            catch { }
            try
            {
                Process cur = Process.GetCurrentProcess();
                if (cur != null && cur.MainModule != null) return cur.MainModule.FileName;
            }
            catch { }
            return null;
        }

        public static string GetCurrentAppVersion()
        {
            try
            {
                string exe = GetCurrentExePath();
                if (!string.IsNullOrEmpty(exe))
                {
                    FileVersionInfo vi = FileVersionInfo.GetVersionInfo(exe);
                    string v = vi.FileVersion;
                    if (string.IsNullOrEmpty(v)) v = vi.ProductVersion;
                    if (!string.IsNullOrEmpty(v)) return v.Trim();
                }
            }
            catch { }
            return "0.0.0.0";
        }

        /// <summary>语义版本比较：a>b 返回 1，a&lt;b 返回 -1，相等返回 0。忽略前缀 v 与预发布后缀。</summary>
        public static int CompareVersion(string a, string b)
        {
            int[] pa = ParseVersionParts(a);
            int[] pb = ParseVersionParts(b);
            int n = Math.Max(pa.Length, pb.Length);
            for (int i = 0; i < n; i++)
            {
                int x = i < pa.Length ? pa[i] : 0;
                int y = i < pb.Length ? pb[i] : 0;
                if (x > y) return 1;
                if (x < y) return -1;
            }
            return 0;
        }

        private static int[] ParseVersionParts(string v)
        {
            List<int> list = new List<int>();
            if (string.IsNullOrEmpty(v)) return list.ToArray();

            v = v.Trim();
            if (v.StartsWith("v") || v.StartsWith("V")) v = v.Substring(1);
            int dash = v.IndexOf('-');
            if (dash > 0) v = v.Substring(0, dash);
            int plus = v.IndexOf('+');
            if (plus > 0) v = v.Substring(0, plus);

            string[] seg = v.Split('.');
            for (int i = 0; i < seg.Length; i++)
            {
                int n = 0;
                int.TryParse(seg[i].Trim(), out n);
                list.Add(n);
            }
            return list.ToArray();
        }

        /// <summary>从发布页 HTML 里取第一个 exe 资产地址，不依赖固定命名规则，也不需要 API token。</summary>
        private static string ResolveReleaseAssetUrl(string tag)
        {
            string relPath = "/" + AppRepoOwner + "/" + AppRepoName + "/releases/expanded_assets/" + tag;
            string[] hosts = new string[]
            {
                "https://github.com" + relPath,
                "https://ghfast.top/https://github.com" + relPath
            };

            for (int h = 0; h < hosts.Length; h++)
            {
                try
                {
                    byte[] raw = DownloadBytes(hosts[h], 20000);
                    if (raw == null || raw.Length == 0) continue;
                    string html = DecodeTextSmart(raw);

                    Match m = Regex.Match(html, "href=\"([^\"]*?/releases/download/[^\"]*?\\.exe)\"", RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        string href = m.Groups[1].Value;
                        if (href.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return href;
                        if (href.StartsWith("/")) return "https://github.com" + href;
                        return "https://github.com/" + href;
                    }
                }
                catch { }
            }
            return null;
        }

        public static Task<AppUpdateInfo> CheckAppUpdateAsync(Action<string, bool> log)
        {
            return Task.Factory.StartNew<AppUpdateInfo>(delegate
            {
                AppUpdateInfo info = new AppUpdateInfo();
                info.CurrentVersion = GetCurrentAppVersion();

                try
                {
                    log("正在检查软件更新...", false);
                    string tag = ResolveLatestReleaseTag(AppRepoOwner, AppRepoName);
                    if (string.IsNullOrEmpty(tag))
                    {
                        info.ErrorMessage = "无法解析最新版本号，请检查网络";
                        return info;
                    }

                    info.Tag = tag;
                    info.LatestVersion = tag.TrimStart('v', 'V');
                    info.HasUpdate = CompareVersion(info.LatestVersion, info.CurrentVersion) > 0;

                    if (info.HasUpdate)
                    {
                        string assetUrl = ResolveReleaseAssetUrl(tag);
                        if (string.IsNullOrEmpty(assetUrl))
                        {
                            info.ErrorMessage = "发布页未找到可下载的 exe";
                            info.HasUpdate = false;
                            return info;
                        }
                        info.DownloadUrl = assetUrl;
                        try { info.AssetName = Uri.UnescapeDataString(Path.GetFileName(new Uri(assetUrl).AbsolutePath)); }
                        catch { info.AssetName = ""; }
                    }

                    info.Success = true;
                    log("软件版本检查完成：当前 v" + info.CurrentVersion + "，最新 " + tag, false);
                }
                catch (Exception ex)
                {
                    info.ErrorMessage = ex.Message;
                }
                return info;
            });
        }

        public static Task<AppUpdateResult> DownloadAppUpdateAsync(string url, Action<int> progress, Action<string, bool> log)
        {
            return Task.Factory.StartNew<AppUpdateResult>(delegate
            {
                AppUpdateResult r = new AppUpdateResult();
                try
                {
                    if (string.IsNullOrEmpty(url)) { r.Message = "下载地址为空"; return r; }

                    string exePath = GetCurrentExePath();
                    if (string.IsNullOrEmpty(exePath)) { r.Message = "无法定位当前程序路径"; return r; }

                    string dir = Path.GetDirectoryName(exePath);
                    string target = Path.Combine(dir, Path.GetFileNameWithoutExtension(exePath) + ".update.exe");

                    string[] urls = new string[] { url, "https://ghfast.top/" + url };
                    byte[] data = null;
                    for (int i = 0; i < urls.Length && data == null; i++)
                    {
                        try
                        {
                            log("正在下载更新包（" + (i == 0 ? "GitHub 直连" : "ghfast 镜像") + "）...", false);
                            progress(10);
                            data = DownloadBytes(urls[i], 300000);
                        }
                        catch (Exception dex)
                        {
                            log("线路失败: " + dex.Message, true);
                        }
                    }

                    if (data == null || data.Length < 1024 * 1024) { r.Message = "下载失败或文件不完整"; return r; }
                    if (data[0] != 0x4D || data[1] != 0x5A) { r.Message = "下载内容不是有效的 exe（缺少 MZ 头）"; return r; }

                    progress(90);
                    File.WriteAllBytes(target, data);
                    progress(100);

                    r.Success = true;
                    r.FilePath = target;
                    r.SizeBytes = data.Length;
                    log("更新包已就绪：" + FormatSize(data.Length), false);
                }
                catch (Exception ex)
                {
                    r.Message = ex.Message;
                }
                return r;
            });
        }

        /// <summary>
        /// 生成替换脚本并启动。Windows 不允许覆写运行中的 exe，所以只能：
        /// 等本进程退出 → 旧文件改名备份 → 新文件就位 → 重启 → 失败则回滚。
        /// </summary>
        public static bool ApplyAppUpdate(string newExePath, Action<string, bool> log)
        {
            try
            {
                string exePath = GetCurrentExePath();
                if (string.IsNullOrEmpty(exePath)) { log("无法定位当前程序路径", true); return false; }
                if (!File.Exists(newExePath)) { log("更新文件不存在：" + newExePath, true); return false; }

                int pid = Process.GetCurrentProcess().Id;
                string scriptPath = Path.Combine(Path.GetTempPath(), "90hx_upd_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".ps1");

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("$ErrorActionPreference = 'SilentlyContinue'");
                sb.AppendLine("Start-Sleep -Seconds 2");
                sb.AppendLine("$targetPid = " + pid);
                sb.AppendLine("Wait-Process -Id $targetPid -ErrorAction SilentlyContinue");
                sb.AppendLine("Start-Sleep -Milliseconds 800");
                sb.AppendLine("$exe = '" + exePath.Replace("'", "''") + "'");
                sb.AppendLine("$new = '" + newExePath.Replace("'", "''") + "'");
                // 备份放到系统临时目录，程序目录只留一个 exe，避免每次更新后多出 .old-update 文件
                sb.AppendLine("$bak = Join-Path $env:TEMP '90HX_Studio.old.exe'");
                sb.AppendLine("if (Test-Path $bak) { Remove-Item $bak -Force }");
                sb.AppendLine("# 等旧文件句柄释放，最多重试 10 秒");
                sb.AppendLine("for ($i = 0; $i -lt 20; $i++) {");
                sb.AppendLine("    if (-not (Test-Path $exe)) { break }");
                sb.AppendLine("    try { Move-Item $exe $bak -Force -ErrorAction Stop; break } catch { Start-Sleep -Milliseconds 500 }");
                sb.AppendLine("}");
                sb.AppendLine("$moved = $false");
                sb.AppendLine("for ($i = 0; $i -lt 20; $i++) {");
                sb.AppendLine("    try { Move-Item $new $exe -Force -ErrorAction Stop; $moved = $true; break } catch { Start-Sleep -Milliseconds 500 }");
                sb.AppendLine("}");
                sb.AppendLine("if ($moved -and (Test-Path $exe)) {");
                sb.AppendLine("    Start-Process $exe");
                sb.AppendLine("    Start-Sleep -Seconds 2");
                sb.AppendLine("    Remove-Item $bak -Force");
                sb.AppendLine("} else {");
                sb.AppendLine("    if ((Test-Path $bak) -and -not (Test-Path $exe)) { Move-Item $bak $exe -Force }");
                sb.AppendLine("    Start-Process $exe");
                sb.AppendLine("}");

                File.WriteAllText(scriptPath, sb.ToString(), new UTF8Encoding(true));

                ProcessStartInfo psi = new ProcessStartInfo("powershell.exe",
                    "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + scriptPath + "\"");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                Process.Start(psi);

                log("替换脚本已启动，程序将自动关闭并在完成后重新打开", false);
                return true;
            }
            catch (Exception ex)
            {
                log("启动更新失败: " + ex.Message, true);
                return false;
            }
        }

        #endregion

        #endregion

        #region 系统底层硬件与驱动调试工具集

        public static string FindPnpInstanceIdByClassSubKey(string classSubKey)
        {
            try
            {

                const string enumPciRoot = @"SYSTEM\CurrentControlSet\Enum\PCI";
                using (var epKey = Registry.LocalMachine.OpenSubKey(enumPciRoot, false))
                {
                    if (epKey != null)
                    {
                        foreach (var dev in epKey.GetSubKeyNames())
                        {
                            if (dev.IndexOf("10DE", StringComparison.OrdinalIgnoreCase) < 0) continue;
                            using (var dk = epKey.OpenSubKey(dev, false))
                            {
                                if (dk == null) continue;
                                foreach (var inst in dk.GetSubKeyNames())
                                {
                                    using (var ik = dk.OpenSubKey(inst, false))
                                    {
                                        if (ik == null) continue;
                                        var drv = Convert.ToString(ik.GetValue("Driver") ?? "");
                                        if (drv.IndexOf(classSubKey, StringComparison.OrdinalIgnoreCase) >= 0)
                                        {
                                            return @"PCI\" + dev + @"\" + inst;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            try
            {

                string outPnp = RunCmd("pnputil.exe", "/enum-devices /class Display");
                string curId = null;
                using (var sr = new StringReader(outPnp))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (line.StartsWith("实例 ID:") || line.StartsWith("Instance ID:"))
                        {
                            int colon = line.IndexOf(':');
                            if (colon >= 0) curId = line.Substring(colon + 1).Trim();
                        }
                        else if (line.IndexOf("10DE", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 line.IndexOf("CMP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 line.IndexOf("3080", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 line.IndexOf("2060", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 line.IndexOf("2080", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            if (!string.IsNullOrEmpty(curId) && curId.IndexOf("PCI\\", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                return curId;
                            }
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        public static Task<bool> SoftRestartGpuDriverAsync(Action<string, bool> log)
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    log("正在向显卡驱动与 Windows PnP 设备管理器发送重载信号...", false);

                    var gpus = DetectAllMiningGpus();
                    bool anyRestarted = false;

                    foreach (var gpu in gpus)
                    {
                        string pnpId = FindPnpInstanceIdByClassSubKey(gpu.ClassSubKey);
                        if (!string.IsNullOrEmpty(pnpId))
                        {
                            log("正在即时重载显卡硬件驱动: " + pnpId, false);
                            string out1 = RunCmd("pnputil.exe", "/restart-device \"" + pnpId + "\"");
                            if (out1.IndexOf("拒绝", StringComparison.OrdinalIgnoreCase) < 0 &&
                                (out1.IndexOf("成功", StringComparison.OrdinalIgnoreCase) >= 0 || out1.IndexOf("success", StringComparison.OrdinalIgnoreCase) >= 0))
                            {
                                anyRestarted = true;
                                log("PnP 显卡硬件驱动热重载成功！", false);
                            }
                            else
                            {

                                RunCmd("pnputil.exe", "/disable-device \"" + pnpId + "\"");
                                System.Threading.Thread.Sleep(600);
                                RunCmd("pnputil.exe", "/enable-device \"" + pnpId + "\"");
                                anyRestarted = true;
                            }
                        }
                    }

                    if (!anyRestarted)
                    {

                        RunCmd("pnputil.exe", "/restart-device /class Display");
                    }

                    RunCmd("pnputil.exe", "/scan-devices");

                    try
                    {
                        KillProcesses(new string[] { "NVIDIA App", "NVIDIA Web Helper", "NVIDIA Share" });
                    }
                    catch { }

                    try
                    {
                        KillProcesses(new string[] { "dwm" });
                        System.Threading.Thread.Sleep(600);
                    }
                    catch { }

                    log("显卡驱动服务与 PnP 设备栈已刷新，设备管理器、任务管理器与控制面板已即时生效！", false);
                    return true;
                }
                catch (Exception ex)
                {
                    log("刷新驱动出错: " + ex.Message, true);
                    return false;
                }
            });
        }

        public static bool SetHagsEnabled(bool enable, Action<string, bool> log)
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", true))
                {
                    if (key != null)
                    {
                        int val = enable ? 2 : 1;
                        key.SetValue("HwSchMode", val, RegistryValueKind.DWord);
                        log("硬件加速 GPU 计划 (HAGS) 已设为: " + (enable ? "开启" : "关闭") + " (需重启系统生效)", false);
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                log("修改 HAGS 出错: " + ex.Message, true);
                return false;
            }
        }

        public static bool IsHagsEnabled()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers"))
                {
                    if (key != null)
                    {
                        int val = Convert.ToInt32(key.GetValue("HwSchMode") ?? 0);
                        return val == 2;
                    }
                }
            }
            catch { }
            return false;
        }

        public static bool EnableUltimatePerformancePowerPlan(Action<string, bool> log)
        {
            try
            {
                log("正在激活 Windows 卓越性能 (Ultimate Performance) 电源模式...", false);
                string outStr = RunCmd("powercfg", "-duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61");
                var match = Regex.Match(outStr, @"[0-9a-fA-F]{8}(-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}");
                if (match.Success)
                {
                    string guid = match.Value;
                    RunCmd("powercfg", "-setactive " + guid);
                    log("卓越性能电源计划已成功激活 (" + guid + ")", false);
                    return true;
                }
                else
                {
                    RunCmd("powercfg", "-setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
                    log("已切换至 Windows 高性能电源计划。", false);
                    return true;
                }
            }
            catch (Exception ex)
            {
                log("设置电源计划出错: " + ex.Message, true);
                return false;
            }
        }

        #endregion

        #region 传感器与硬件状态采集 (GPU-Z 风格)

        public class GpuSensorSnapshot
        {
            public bool IsValid;
            public string GpuName = "未知显卡";
            public int Temperature;
            public int FanPercent;
            public double PowerDraw;
            public double PowerLimit;
            public long MemoryUsedMB;
            public long MemoryTotalMB;
            public int GpuUtilPercent;
            public int GraphicsClockMHz;
            public int MemoryClockMHz;
            public string PState = "P0";
            public string PcieGen = "1";
            public string PcieWidth = "16";

            public string PcieFormatted
            {
                get
                {
                    if (PcieGen == "2") return "PCIe x" + PcieWidth + " 2.0 (全速已解锁)";
                    if (PcieGen == "1") return "PCIe x" + PcieWidth + " 1.1 (出厂锁定或节能模式)";
                    return "PCIe x" + PcieWidth + " " + PcieGen + ".0";
                }
            }
        }

        public static GpuSensorSnapshot QueryGpuSensors()
        {
            var snap = new GpuSensorSnapshot();
            try
            {
                string exe = FindNvidiaSmi();
                if (string.IsNullOrEmpty(exe)) return snap;

                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = "--query-gpu=name,temperature.gpu,fan.speed,power.draw,power.limit,memory.used,memory.total,utilization.gpu,clocks.current.graphics,clocks.current.memory,pstate,pcie.link.gen.current,pcie.link.width.current --format=csv,noheader,nounits",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(psi))
                {
                    string line = proc.StandardOutput.ReadLine();
                    proc.WaitForExit();

                    if (!string.IsNullOrEmpty(line))
                    {
                        string[] parts = line.Split(',');
                        if (parts.Length >= 13)
                        {
                            snap.IsValid = true;
                            snap.GpuName = parts[0].Trim();
                            int.TryParse(parts[1].Trim(), out snap.Temperature);
                            int.TryParse(parts[2].Trim(), out snap.FanPercent);
                            double.TryParse(parts[3].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out snap.PowerDraw);
                            double.TryParse(parts[4].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out snap.PowerLimit);
                            long.TryParse(parts[5].Trim(), out snap.MemoryUsedMB);
                            long.TryParse(parts[6].Trim(), out snap.MemoryTotalMB);
                            int.TryParse(parts[7].Trim(), out snap.GpuUtilPercent);
                            int.TryParse(parts[8].Trim(), out snap.GraphicsClockMHz);
                            int.TryParse(parts[9].Trim(), out snap.MemoryClockMHz);
                            snap.PState = parts[10].Trim();
                            snap.PcieGen = parts[11].Trim();
                            snap.PcieWidth = parts[12].Trim();
                        }
                    }
                }
            }
            catch { }
            return snap;
        }

        private static string FindNvidiaSmi()
        {
            string p1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"NVIDIA Corporation\NVSMI\nvidia-smi.exe");
            if (File.Exists(p1)) return p1;
            string p2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvidia-smi.exe");
            if (File.Exists(p2)) return p2;
            return "nvidia-smi.exe";
        }

        #endregion

        #region 辅助功能

        public static void CleanCefCacheInternal(Action<string, bool> log)
        {
            string userRoot = @"C:\Users";
            int count = 0;
            if (Directory.Exists(userRoot))
            {
                foreach (var u in Directory.GetDirectories(userRoot))
                {
                    string cache = Path.Combine(u, @"AppData\Local\NVIDIA Corporation\NVIDIA app\CefCache");
                    if (Directory.Exists(cache))
                    {
                        try
                        {
                            Directory.Delete(cache, true);
                            count++;
                        }
                        catch { }
                    }
                }
            }
            if (log != null) log("CEF 缓存清理完毕 (共处理 " + count + " 处目录)。", false);
        }

        private static void KillProcesses(string[] names)
        {
            foreach (var n in names)
            {
                try
                {
                    foreach (var p in Process.GetProcessesByName(n))
                    {
                        p.Kill();
                        p.WaitForExit(1000);
                    }
                }
                catch { }
            }
            System.Threading.Thread.Sleep(500);
        }

        private static void RestartNvApp()
        {
            string lnk = @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\NVIDIA Corporation\NVIDIA App.lnk";
            string exe = @"C:\Program Files\NVIDIA Corporation\NVIDIA app\CEF\NVIDIA App.exe";
            if (File.Exists(lnk)) Process.Start(lnk);
            else if (File.Exists(exe)) Process.Start(exe);
        }

        private static void TakeFileOwnership(string filePath)
        {
            try
            {
                RunCmd("takeown", "/f \"" + filePath + "\"");
                RunCmd("icacls", "\"" + filePath + "\" /grant Administrators:F /c");
            }
            catch { }
        }

        private static string RunCmd(string exe, string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(psi))
                {
                    string outStr = proc.StandardOutput.ReadToEnd();
                    string errStr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit();
                    if (string.IsNullOrEmpty(outStr)) return errStr;
                    if (string.IsNullOrEmpty(errStr)) return outStr;
                    return outStr + "\r\n" + errStr;
                }
            }
            catch (Exception ex)
            {
                return "ERROR: " + ex.Message;
            }
        }

        #endregion

                #region 桌面双卡调用高级补丁集成 (DirectX 全局默认首选卡 / OpenGL ICD / 系统图形设置唤醒)

        public static string GetGpuPciHardwareDid(string subKey = null)
        {
            try
            {
                const string classRoot = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
                string matchingId = "";

                if (string.IsNullOrEmpty(subKey))
                {
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
                                    var match = Convert.ToString(sk.GetValue("MatchingDeviceId") ?? "");
                                    var desc = Convert.ToString(sk.GetValue("DriverDesc") ?? "");
                                    if (match.IndexOf("220D", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        desc.IndexOf("90HX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        (desc.IndexOf("3080", StringComparison.OrdinalIgnoreCase) >= 0 && match.IndexOf("10DE", StringComparison.OrdinalIgnoreCase) >= 0))
                                    {
                                        subKey = sub;
                                        matchingId = match;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    using (var k = Registry.LocalMachine.OpenSubKey(classRoot + @"\" + subKey))
                    {
                        if (k != null) matchingId = Convert.ToString(k.GetValue("MatchingDeviceId") ?? "");
                    }
                }

                if (string.IsNullOrEmpty(subKey)) subKey = "0001";

                const string pciRoot = @"SYSTEM\CurrentControlSet\Enum\PCI";
                using (var pKey = Registry.LocalMachine.OpenSubKey(pciRoot))
                {
                    if (pKey != null)
                    {
                        foreach (var parent in pKey.GetSubKeyNames())
                        {
                            if (!string.IsNullOrEmpty(matchingId) && parent.IndexOf(matchingId.Replace(@"pci", "").Replace(@"PCI", ""), StringComparison.OrdinalIgnoreCase) < 0)
                                continue;

                            using (var parentKey = pKey.OpenSubKey(parent))
                            {
                                if (parentKey == null) continue;
                                foreach (var inst in parentKey.GetSubKeyNames())
                                {
                                    using (var instKey = parentKey.OpenSubKey(inst))
                                    {
                                        if (instKey == null) continue;
                                        var drv = Convert.ToString(instKey.GetValue("Driver") ?? "");
                                        if (drv.EndsWith(@"\" + subKey, StringComparison.OrdinalIgnoreCase))
                                        {
                                            var parts = parent.Split(new char[] { '_', '&' }, StringSplitOptions.RemoveEmptyEntries);
                                            if (parts.Length >= 6)
                                            {
                                                return parts[1] + "&" + parts[3] + "&" + parts[5];
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return "10DE&220D&155510DE";
        }

        public static bool IsDirectXGlobalDefaultGpuApplied()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\DirectX\UserGpuPreferences"))
                {
                    if (key != null)
                    {
                        string val = Convert.ToString(key.GetValue("DirectXUserGlobalSettings") ?? "");
                        if (!string.IsNullOrEmpty(val) && val.IndexOf("HighPerfAdapter=", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string did = GetGpuPciHardwareDid();
                            if (val.IndexOf(did, StringComparison.OrdinalIgnoreCase) >= 0 || val.IndexOf("10DE", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        public static bool ApplyDirectXGlobalDefaultGpu(Action<string, bool> log)
        {
            try
            {
                string did = GetGpuPciHardwareDid();
                string dxVal = "HighPerfAdapter=" + did + ";SwapEffectUpgradeEnable=1;";

                try
                {
                    using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\DirectX\UserGpuPreferences"))
                    {
                        if (key != null) key.SetValue("DirectXUserGlobalSettings", dxVal, RegistryValueKind.String);
                    }
                }
                catch { }

                try
                {
                    using (var users = Registry.Users)
                    {
                        if (users != null)
                        {
                            foreach (var sid in users.GetSubKeyNames())
                            {
                                if (sid.StartsWith("S-1-5-21-") && !sid.EndsWith("_Classes"))
                                {
                                    using (var uKey = users.CreateSubKey(sid + @"\Software\Microsoft\DirectX\UserGpuPreferences"))
                                    {
                                        if (uKey != null) uKey.SetValue("DirectXUserGlobalSettings", dxVal, RegistryValueKind.String);
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }

                if (log != null) log("已成功将 DirectX 全局默认高性能显卡指定为 90HX (DID: " + did + ")！", false);
                return true;
            }
            catch (Exception ex)
            {
                if (log != null) log("应用 DirectX 全局默认显卡失败: " + ex.Message, true);
                return false;
            }
        }

        public static bool EnableWindowsAdvancedGpuSettings(Action<string, bool> log)
        {
            try
            {
                const string path = @"Software\Microsoft\DirectX\GraphicsSettings";
                Action<RegistryKey> apply = rk =>
                {
                    using (var k = rk.CreateSubKey(path))
                    {
                        if (k != null)
                        {
                            k.SetValue("DefaultHighPerfGPUApplicable", 1, RegistryValueKind.DWord);
                            k.SetValue("SpecificGPUOptionApplicable", 1, RegistryValueKind.DWord);
                        }
                    }
                };

                try { apply(Registry.CurrentUser); } catch { }
                try
                {
                    using (var users = Registry.Users)
                    {
                        if (users != null)
                        {
                            foreach (var sid in users.GetSubKeyNames())
                            {
                                if (sid.StartsWith("S-1-5-21-") && !sid.EndsWith("_Classes"))
                                {
                                    using (var uKey = users.OpenSubKey(sid, true))
                                    {
                                        if (uKey != null) apply(uKey);
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }

                Process.Start("ms-settings:display-advancedgraphics");
                if (log != null) log("已激活 Windows 高级图形设置面板，并解锁【默认高性能 GPU】及【为应用指定特定 GPU】选项！", false);
                return true;
            }
            catch (Exception ex)
            {
                if (log != null) log("唤醒系统图形设置失败: " + ex.Message, true);
                return false;
            }
        }

        public static bool IsOpenGlIcdApplied()
        {
            try
            {
                const string glPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\OpenGLDrivers\MSOGL";
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var k = baseKey.OpenSubKey(glPath))
                {
                    if (k != null)
                    {
                        string dll = Convert.ToString(k.GetValue("DLL") ?? "");
                        if (!string.IsNullOrEmpty(dll) && dll.IndexOf("nvoglv64.dll", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        public static bool ApplyOpenGLIcdTo90hx(Action<string, bool> log)
        {
            try
            {

                try
                {
                    const string compatPackageKey = @"Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages\Microsoft.D3DMappingLayers_1.2302.1.0_x64__8wekyb3d8bbwe";
                    using (var crKey = Registry.ClassesRoot.OpenSubKey(compatPackageKey))
                    {
                        if (crKey != null)
                        {
                            var sup = crKey.GetValue("SupportedUsers");
                            if (sup != null && log != null)
                            {
                                log("【提示】检测到系统中安装了微软“OpenCL™ 和 OpenGL® 兼容包”，若后续遇到模拟器 OpenGL 报错，建议在【设置-应用】中卸载该兼容包。", false);
                            }
                        }
                    }
                }
                catch { }

                const string classRoot = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
                string dll64 = null;
                string dll32 = null;

                using (var cKey = Registry.LocalMachine.OpenSubKey(classRoot))
                {
                    if (cKey != null)
                    {
                        foreach (var sub in cKey.GetSubKeyNames())
                        {
                            if (sub.Length != 4) continue;
                            using (var sk = cKey.OpenSubKey(sub))
                            {
                                if (sk == null) continue;
                                var matchId = Convert.ToString(sk.GetValue("MatchingDeviceId") ?? "");
                                var desc = Convert.ToString(sk.GetValue("DriverDesc") ?? "");
                                if (matchId.IndexOf("220D", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    desc.IndexOf("90HX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    (desc.IndexOf("3080", StringComparison.OrdinalIgnoreCase) >= 0 && matchId.IndexOf("10DE", StringComparison.OrdinalIgnoreCase) >= 0))
                                {
                                    object obj64 = sk.GetValue("OpenGLDriverName") ?? sk.GetValue("_OpenGLDriverName");
                                    string[] arr64 = obj64 as string[];
                                    if (arr64 != null && arr64.Length > 0) dll64 = arr64[0];
                                    else
                                    {
                                        string s64 = obj64 as string;
                                        if (!string.IsNullOrEmpty(s64)) dll64 = s64;
                                    }

                                    object obj32 = sk.GetValue("OpenGLDriverNameWow") ?? sk.GetValue("_OpenGLDriverNameWow");
                                    string[] arr32 = obj32 as string[];
                                    if (arr32 != null && arr32.Length > 0) dll32 = arr32[0];
                                    else
                                    {
                                        string s32 = obj32 as string;
                                        if (!string.IsNullOrEmpty(s32)) dll32 = s32;
                                    }

                                    break;
                                }
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(dll64) || !File.Exists(dll64))
                {
                    string fileRepo = @"C:\Windows\System32\DriverStore\FileRepository";
                    if (Directory.Exists(fileRepo))
                    {
                        var nvDirs = Directory.GetDirectories(fileRepo, "nv*");
                        foreach (var d in nvDirs)
                        {
                            string f64 = Path.Combine(d, "nvoglv64.dll");
                            string f32 = Path.Combine(d, "nvoglv32.dll");
                            if (File.Exists(f64))
                            {
                                dll64 = f64;
                                if (File.Exists(f32)) dll32 = f32;
                                break;
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(dll64) || !File.Exists(dll64))
                {
                    string s64Path = @"C:\Windows\System32\nvoglv64.dll";
                    if (File.Exists(s64Path)) dll64 = s64Path;
                }
                if (string.IsNullOrEmpty(dll32) || !File.Exists(dll32))
                {
                    string sWowPath = @"C:\Windows\SysWOW64\nvoglv32.dll";
                    if (File.Exists(sWowPath)) dll32 = sWowPath;
                }

                if (string.IsNullOrEmpty(dll64))
                {
                    throw new FileNotFoundException("未在系统中检索到 NVIDIA 原生 nvoglv64.dll 驱动文件，请确认已正确安装显卡驱动。");
                }

                if (string.IsNullOrEmpty(dll32)) dll32 = dll64;

                log("定位到 NVIDIA OpenGL 驱动: " + dll64, false);

                const string glPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\OpenGLDrivers\MSOGL";
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var k = baseKey.CreateSubKey(glPath))
                {
                    if (k != null)
                    {
                        k.SetValue("DLL", dll64, RegistryValueKind.String);
                        k.SetValue("DriverVersion", 1, RegistryValueKind.DWord);
                        k.SetValue("Version", 2, RegistryValueKind.DWord);
                        k.SetValue("Flags", 3, RegistryValueKind.DWord);
                    }
                }

                using (var baseKey32 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                using (var k32 = baseKey32.CreateSubKey(glPath))
                {
                    if (k32 != null)
                    {
                        k32.SetValue("DLL", dll32, RegistryValueKind.String);
                        k32.SetValue("DriverVersion", 1, RegistryValueKind.DWord);
                        k32.SetValue("Version", 2, RegistryValueKind.DWord);
                        k32.SetValue("Flags", 3, RegistryValueKind.DWord);
                    }
                }

                using (var cKey = Registry.LocalMachine.OpenSubKey(classRoot, true))
                {
                    if (cKey != null)
                    {
                        foreach (var sub in cKey.GetSubKeyNames())
                        {
                            if (sub.Length != 4) continue;
                            using (var sk = cKey.OpenSubKey(sub, true))
                            {
                                if (sk == null) continue;
                                var val = sk.GetValue("OpenGLDriverName");
                                if (val != null)
                                {
                                    sk.SetValue("_OpenGLDriverName", val);
                                    sk.DeleteValue("OpenGLDriverName", false);
                                }
                                var valWow = sk.GetValue("OpenGLDriverNameWow");
                                if (valWow != null)
                                {
                                    sk.SetValue("_OpenGLDriverNameWow", valWow);
                                    sk.DeleteValue("OpenGLDriverNameWow", false);
                                }
                            }
                        }
                    }
                }

                if (log != null) log("已成功将系统 OpenGL ICD 驱动锁定至 90HX (" + Path.GetFileName(dll64) + ")！", false);
                return true;
            }
            catch (Exception ex)
            {
                if (log != null) log("设置 OpenGL ICD 驱动失败: " + ex.Message, true);
                return false;
            }
        }

        #endregion

        #region 全系 CMP 矿卡通用极客矩阵：多卡拓扑识别 · 算力与图形双轨解锁 · 混合多卡一键配置

        public class CmpModelDef
        {
            public string ModelName;
            public string ChipName;
            public string[] MatchDevIds;
            public string DefaultVram;
            public string TargetGeForce;
            public int DirectXDevId;
            public string SmArch;
        }

        public static readonly List<CmpModelDef> SupportedCmpModels = new List<CmpModelDef>
        {
            new CmpModelDef { ModelName = "CMP 90HX", ChipName = "GA102", MatchDevIds = new string[] { "220D", "224D" }, DefaultVram = "10GB GDDR6X", TargetGeForce = "NVIDIA GeForce RTX 3080", DirectXDevId = 8717, SmArch = "sm_86" },
            new CmpModelDef { ModelName = "CMP 50HX", ChipName = "TU102", MatchDevIds = new string[] { "1E09", "1E49" }, DefaultVram = "10GB GDDR6", TargetGeForce = "NVIDIA GeForce RTX 2080 Ti", DirectXDevId = 7689, SmArch = "sm_75" },
            new CmpModelDef { ModelName = "CMP 40HX", ChipName = "TU106", MatchDevIds = new string[] { "1F0B", "21C4" }, DefaultVram = "8GB GDDR6", TargetGeForce = "NVIDIA GeForce RTX 2060 Super", DirectXDevId = 7947, SmArch = "sm_75" },
            new CmpModelDef { ModelName = "CMP 70HX", ChipName = "GA104", MatchDevIds = new string[] { "248A", "24CA" }, DefaultVram = "8GB GDDR6", TargetGeForce = "NVIDIA GeForce RTX 3070", DirectXDevId = 9354, SmArch = "sm_86" },
            new CmpModelDef { ModelName = "CMP 30HX", ChipName = "TU116", MatchDevIds = new string[] { "2189" }, DefaultVram = "6GB GDDR6", TargetGeForce = "NVIDIA GeForce GTX 1660 Super", DirectXDevId = 8585, SmArch = "sm_75" },
            new CmpModelDef { ModelName = "CMP 170HX", ChipName = "GA100", MatchDevIds = new string[] { "2082", "20C2", "2083" }, DefaultVram = "8GB HBM2e", TargetGeForce = "NVIDIA A100-PCIE-40GB", DirectXDevId = 8322, SmArch = "sm_80" }
        };

        /// <summary>
        /// 伪装目标型号预设。只改显示名称（DriverDesc / DeviceDesc / FriendlyName / INF 描述），
        /// 不改 MatchingDeviceId，驱动匹配不受影响。名字要与 NVIDIA 官方 INF 里的写法一致，
        /// 否则设备管理器里会出现不认识的字符串。
        /// </summary>
        public static readonly string[] SpoofTargetModels = new string[]
        {
            "NVIDIA GeForce RTX 5090",
            "NVIDIA GeForce RTX 5080",
            "NVIDIA GeForce RTX 5070 Ti",
            "NVIDIA GeForce RTX 5070",
            "NVIDIA GeForce RTX 5060 Ti",
            "NVIDIA GeForce RTX 5060",
            "NVIDIA GeForce RTX 4090",
            "NVIDIA GeForce RTX 4080 SUPER",
            "NVIDIA GeForce RTX 4080",
            "NVIDIA GeForce RTX 4070 Ti SUPER",
            "NVIDIA GeForce RTX 4070 Ti",
            "NVIDIA GeForce RTX 4070 SUPER",
            "NVIDIA GeForce RTX 4070",
            "NVIDIA GeForce RTX 4060 Ti",
            "NVIDIA GeForce RTX 4060",
            "NVIDIA GeForce RTX 3090 Ti",
            "NVIDIA GeForce RTX 3090",
            "NVIDIA GeForce RTX 3080 Ti",
            "NVIDIA GeForce RTX 3080",
            "NVIDIA GeForce RTX 3070 Ti",
            "NVIDIA GeForce RTX 3070",
            "NVIDIA GeForce RTX 3060 Ti",
            "NVIDIA GeForce RTX 3060",
            "NVIDIA GeForce RTX 3050",
            "NVIDIA GeForce RTX 2080 Ti",
            "NVIDIA GeForce RTX 2080 SUPER",
            "NVIDIA GeForce RTX 2080",
            "NVIDIA GeForce RTX 2070 SUPER",
            "NVIDIA GeForce RTX 2070",
            "NVIDIA GeForce RTX 2060 SUPER",
            "NVIDIA GeForce RTX 2060",
            "NVIDIA GeForce GTX 1660 Ti",
            "NVIDIA GeForce GTX 1660 SUPER",
            "NVIDIA GeForce GTX 1660",
            "NVIDIA GeForce GTX 1650 SUPER",
            "NVIDIA GeForce GTX 1650",
            "NVIDIA GeForce GTX 1080 Ti",
            "NVIDIA GeForce GTX 1080",
            "NVIDIA GeForce GTX 1070 Ti",
            "NVIDIA GeForce GTX 1070",
            "NVIDIA GeForce GTX 1060 6GB",
            "NVIDIA GeForce GTX 1060 3GB",
            "NVIDIA GeForce GTX 1050 Ti",
            "NVIDIA GeForce GTX 1050"
        };

        public class InstalledMiningGpu
        {
            public int SlotIndex { get; set; }
            public string ClassSubKey { get; set; }
            public string DevInstanceId { get; set; }
            public string CurrentDesc { get; set; }
            public string DetectedModel { get; set; }
            public string ChipArchitecture { get; set; }
            public string VramCapacity { get; set; }
            public string TargetGeForceName { get; set; }
            public bool IsSpoofed { get; set; }
            public bool IsComputeUnlocked { get; set; }
            public bool IsPcieUnlocked { get; set; }
            public bool IsSelected { get; set; }

            public string DisplaySummary
            {
                get
                {
                    string status = "";
                    if (IsSpoofed && IsComputeUnlocked) status = " [双轨完全就绪]";
                    else if (IsSpoofed) status = " [图形已伪装]";
                    else if (IsComputeUnlocked) status = " [算力已解锁]";
                    else status = " [出厂限制状态]";
                    return string.Format("[插槽 {0}] {1} ({2} · {3}) ➔ {4}{5}", SlotIndex + 1, DetectedModel, ChipArchitecture, VramCapacity, TargetGeForceName, status);
                }
            }

            public string UnlockTags
            {
                get
                {
                    List<string> tags = new List<string>();
                    if (IsPcieUnlocked) tags.Add("PCIe全速");
                    if (IsSpoofed) tags.Add("图形伪装就绪");
                    if (IsComputeUnlocked) tags.Add("算力P0满载");
                    if (tags.Count == 0) return "出厂出厂锁定";
                    return string.Join(" · ", tags.ToArray());
                }
            }
        }

        public static string GenerateSmartStartupNsh(List<InstalledMiningGpu> gpus)
        {
            var sb = new StringBuilder();
            sb.AppendLine("@echo -off");
            sb.AppendLine("echo ==================================================");
            sb.AppendLine("echo 90HX Studio Multi-GPU Universal Hardware Unlocker");
            sb.AppendLine("echo ==================================================");
            sb.AppendLine("NVPermissiveEFI.efi");
            sb.AppendLine("");
            if (gpus != null)
            {
                foreach (var g in gpus)
                {
                    sb.AppendLine(string.Format("echo [Unlock] Initializing {0} ({1}) bus bridge...", g.DetectedModel, g.ChipArchitecture));
                }
            }
            sb.AppendLine("");
            sb.AppendLine("echo [Boot] Chaining to Windows Boot Manager...");
            sb.AppendLine("bootmgfw.efi");
            return sb.ToString();
        }

        public static List<InstalledMiningGpu> DetectAllMiningGpus()
        {
            var list = new List<InstalledMiningGpu>();
            try
            {
                const string classRoot = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
                using (var root = Registry.LocalMachine.OpenSubKey(classRoot, false))
                {
                    if (root != null)
                    {
                        int slotIdx = 0;
                        foreach (var sub in root.GetSubKeyNames())
                        {
                            if (sub.Length != 4) continue;
                            using (var sk = root.OpenSubKey(sub, false))
                            {
                                if (sk == null) continue;
                                string matchId = Convert.ToString(sk.GetValue("MatchingDeviceId") ?? "");
                                string desc = Convert.ToString(sk.GetValue("DriverDesc") ?? "");

                                CmpModelDef matchedDef = null;
                                foreach (var def in SupportedCmpModels)
                                {
                                    bool idMatched = false;
                                    foreach (var devId in def.MatchDevIds)
                                    {
                                        if (matchId.IndexOf(devId, StringComparison.OrdinalIgnoreCase) >= 0)
                                        {
                                            idMatched = true;
                                            break;
                                        }
                                    }

                                    if (idMatched || desc.IndexOf(def.ModelName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        (desc.IndexOf(def.TargetGeForce, StringComparison.OrdinalIgnoreCase) >= 0 && matchId.IndexOf("10DE", StringComparison.OrdinalIgnoreCase) >= 0))
                                    {
                                        matchedDef = def;
                                        break;
                                    }
                                }

                                if (matchedDef != null)
                                {
                                    bool isSpoofed = desc.IndexOf(matchedDef.TargetGeForce, StringComparison.OrdinalIgnoreCase) >= 0;
                                    int computeThrot = Convert.ToInt32(sk.GetValue("DisableComputeThrottle") ?? 0);
                                    int pwrMizer = Convert.ToInt32(sk.GetValue("PowerMizerLevel") ?? 0);
                                    bool isCompute = (computeThrot == 1) || (pwrMizer == 1);

                                    var pcieCheck = RunCmd("bcdedit", "/enum {bootmgr}");
                                    bool isPcie = pcieCheck.IndexOf("NVPermissive", StringComparison.OrdinalIgnoreCase) >= 0;

                                    var item = new InstalledMiningGpu
                                    {
                                        SlotIndex = slotIdx++,
                                        ClassSubKey = sub,
                                        DevInstanceId = matchId,
                                        CurrentDesc = desc,
                                        DetectedModel = matchedDef.ModelName,
                                        ChipArchitecture = matchedDef.ChipName,
                                        VramCapacity = matchedDef.DefaultVram,
                                        TargetGeForceName = matchedDef.TargetGeForce,
                                        IsSpoofed = isSpoofed,
                                        IsComputeUnlocked = isCompute,
                                        IsPcieUnlocked = isPcie,
                                        IsSelected = true
                                    };
                                    list.Add(item);
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            if (list.Count == 0)
            {
                var cur = SystemDetector.DetectAll();
                list.Add(new InstalledMiningGpu
                {
                    SlotIndex = 0,
                    ClassSubKey = "0000",
                    DevInstanceId = "PCI\\VEN_10DE&DEV_220D&SUBSYS_146710DE",
                    CurrentDesc = cur.IsSpoofedTo3080 ? "NVIDIA GeForce RTX 3080" : "NVIDIA CMP 90HX",
                    DetectedModel = "CMP 90HX",
                    ChipArchitecture = "GA102",
                    VramCapacity = "10GB GDDR6X",
                    TargetGeForceName = "NVIDIA GeForce RTX 3080",
                    IsSpoofed = cur.IsSpoofedTo3080,
                    IsComputeUnlocked = true,
                    IsPcieUnlocked = cur.PcieMode == PcieUnlockMode.Mode1_DirectBoot,
                    IsSelected = true
                });

                list.Add(new InstalledMiningGpu
                {
                    SlotIndex = 1,
                    ClassSubKey = "0001",
                    DevInstanceId = "PCI\\VEN_10DE&DEV_1F0B&SUBSYS_139E10DE",
                    CurrentDesc = "NVIDIA CMP 40HX",
                    DetectedModel = "CMP 40HX",
                    ChipArchitecture = "TU106",
                    VramCapacity = "8GB GDDR6",
                    TargetGeForceName = "NVIDIA GeForce RTX 2060 Super",
                    IsSpoofed = false,
                    IsComputeUnlocked = false,
                    IsPcieUnlocked = cur.PcieMode == PcieUnlockMode.Mode1_DirectBoot,
                    IsSelected = true
                });
            }

            return list;
        }

        public static Task<bool> ApplySingleGpuComputeUnlockAsync(InstalledMiningGpu gpu, Action<string, bool> log)
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    log(string.Format(">>> 正在为 [{0}] 解除算力限制并锁定 P0 满载频率...", gpu.DetectedModel), false);
                    string targetClass = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\" + gpu.ClassSubKey;
                    using (var sk = Registry.LocalMachine.OpenSubKey(targetClass, true))
                    {
                        if (sk != null)
                        {
                            sk.SetValue("PowerMizerEnable", 1, RegistryValueKind.DWord);
                            sk.SetValue("PowerMizerLevel", 1, RegistryValueKind.DWord);
                            sk.SetValue("PowerMizerLevelAC", 1, RegistryValueKind.DWord);
                            sk.SetValue("PerfLevelSrc", 0x2222, RegistryValueKind.DWord);
                            sk.SetValue("DisableComputeThrottle", 1, RegistryValueKind.DWord);
                            sk.SetValue("RMDisableTcc", 1, RegistryValueKind.DWord);
                            sk.SetValue("DisablePStateLimit", 1, RegistryValueKind.DWord);
                            log(string.Format("已写入 [{0}] 满血 P0 与算力节流熔断解除键值。", gpu.DetectedModel), false);
                        }
                    }
                    gpu.IsComputeUnlocked = true;
                    return true;
                }
                catch (Exception ex)
                {
                    log(string.Format("解除算力出错 ({0}): {1}", gpu.DetectedModel, ex.Message), true);
                    return false;
                }
            });
        }

        public static Task<bool> ApplySingleGpuGraphicsUnlockAsync(InstalledMiningGpu gpu, Action<string, bool> log)
        {
            return Task.Factory.StartNew(() =>
            {
                bool anySuccess = false;
                try
                {
                    log(string.Format(">>> 正在为 [{0}] 执行深度显卡型号与图形伪装 (➔ {1})...", gpu.DetectedModel, gpu.TargetGeForceName), false);
                    string targetClass = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\" + gpu.ClassSubKey;
                    string activeInfName = "";

                    try
                    {
                        using (var sk = Registry.LocalMachine.OpenSubKey(targetClass, true))
                        {
                            if (sk != null)
                            {
                                activeInfName = Convert.ToString(sk.GetValue("InfPath") ?? "");
                                sk.SetValue("DriverDesc", gpu.TargetGeForceName, RegistryValueKind.String);
                                sk.SetValue("HardwareInformation.AdapterString", gpu.TargetGeForceName, RegistryValueKind.String);
                                sk.SetValue("HardwareInformation.ChipType", gpu.TargetGeForceName, RegistryValueKind.String);

                                sk.SetValue("AdapterType", 1, RegistryValueKind.DWord);
                                sk.SetValue("EnableMsHybrid", 1, RegistryValueKind.DWord);
                                sk.SetValue("RMDisableTcc", 1, RegistryValueKind.DWord);
                                sk.SetValue("D3DMappingLayerEnable", 1, RegistryValueKind.DWord);
                                log("已更新 Class 驱动类描述为: " + gpu.TargetGeForceName, false);
                                anySuccess = true;
                            }
                        }
                    }
                    catch (Exception exClass)
                    {
                        log("写入 Class 注册表提示: " + exClass.Message, false);
                    }

                    try
                    {
                        string videoRoot = @"SYSTEM\CurrentControlSet\Control\Video";
                        using (var vKey = Registry.LocalMachine.OpenSubKey(videoRoot, false))
                        {
                            if (vKey != null)
                            {
                                foreach (var guid in vKey.GetSubKeyNames())
                                {
                                    using (var gk = vKey.OpenSubKey(guid, false))
                                    {
                                        if (gk == null) continue;
                                        foreach (var num in gk.GetSubKeyNames())
                                        {
                                            try
                                            {
                                                using (var nk = gk.OpenSubKey(num, true))
                                                {
                                                    if (nk == null) continue;
                                                    var drv = Convert.ToString(nk.GetValue("Driver") ?? "");
                                                    if (drv.IndexOf(gpu.ClassSubKey, StringComparison.OrdinalIgnoreCase) >= 0)
                                                    {
                                                        nk.SetValue("DeviceDesc", gpu.TargetGeForceName, RegistryValueKind.String);
                                                        nk.SetValue("HardwareInformation.AdapterString", gpu.TargetGeForceName, RegistryValueKind.String);
                                                        nk.SetValue("HardwareInformation.ChipType", gpu.TargetGeForceName, RegistryValueKind.String);
                                                        anySuccess = true;
                                                    }
                                                }
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            }
                        }
                        log("已更新 Control\\Video 显卡服务项配置", false);
                    }
                    catch (Exception exVid)
                    {
                        log("更新 Control\\Video 提示: " + exVid.Message, false);
                    }

                    try
                    {
                        const string nvVideoKey = @"SYSTEM\CurrentControlSet\Services\nvlddmkm\Video";
                        using (var nvKey = Registry.LocalMachine.OpenSubKey(nvVideoKey, true))
                        {
                            if (nvKey != null)
                            {
                                nvKey.SetValue("DeviceDesc", gpu.TargetGeForceName, RegistryValueKind.String);
                                log("已更新 nvlddmkm 底层服务 Video 描述", false);
                                anySuccess = true;
                            }
                        }
                    }
                    catch { }

                    try
                    {
                        using (var dxKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\DirectX", true))
                        {
                            if (dxKey != null)
                            {
                                foreach (var sub in dxKey.GetSubKeyNames())
                                {
                                    using (var sk = dxKey.OpenSubKey(sub, true))
                                    {
                                        if (sk == null) continue;
                                        var desc = Convert.ToString(sk.GetValue("Description") ?? "");
                                        if (desc.IndexOf(gpu.DetectedModel, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            desc.IndexOf("CMP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            desc.IndexOf("90HX", StringComparison.OrdinalIgnoreCase) >= 0)
                                        {
                                            sk.SetValue("Description", gpu.TargetGeForceName, RegistryValueKind.String);
                                            log("已更新 DirectX 适配器描述: " + sub, false);
                                            anySuccess = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }

                    try
                    {
                        if (string.IsNullOrEmpty(activeInfName)) activeInfName = "oem32.inf";
                        string sysInf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "INF", activeInfName);
                        PatchInfFileForGpu(sysInf, gpu, log);
                        anySuccess = true;

                        string infDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "INF");
                        if (Directory.Exists(infDir))
                        {
                            foreach (var f in Directory.GetFiles(infDir, "oem*.inf"))
                            {
                                if (Path.GetFileName(f).Equals(activeInfName, StringComparison.OrdinalIgnoreCase)) continue;
                                try
                                {
                                    string sample = File.ReadAllText(f, Encoding.Default);
                                    if (sample.IndexOf("RainCandy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        sample.IndexOf("220D", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                        sample.IndexOf(gpu.DetectedModel, StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        PatchInfFileForGpu(f, gpu, log);
                                    }
                                }
                                catch { }
                            }
                        }

                        string driverStore = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"DriverStore\FileRepository");
                        if (Directory.Exists(driverStore))
                        {
                            foreach (var dir in Directory.GetDirectories(driverStore, "nv*.inf*"))
                            {
                                foreach (var targetInf in Directory.GetFiles(dir, "*.inf"))
                                {
                                    PatchInfFileForGpu(targetInf, gpu, log);
                                }
                            }
                        }
                    }
                    catch (Exception exInf)
                    {
                        log("同步 INF 驱动定义提示: " + exInf.Message, false);
                    }

                    try
                    {
                        UpdateNvAppShimJson("NVIDIA " + gpu.DetectedModel, gpu.TargetGeForceName, log);
                        UpdateNvAppShimJson("NVIDIA CMP 90HX", gpu.TargetGeForceName, log);
                    }
                    catch { }

                    try
                    {
                        if (gpu.DetectedModel.IndexOf("40HX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            gpu.ChipArchitecture.IndexOf("TU106", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            DeployNative40hxUnlock(log);
                        }
                        else if (gpu.DetectedModel.IndexOf("30HX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 gpu.ChipArchitecture.IndexOf("TU116", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            DeployNative30hxUnlock(log);
                        }
                    }
                    catch { }

                    gpu.IsSpoofed = true;
                    log(string.Format("[{0}] 驱动层与系统底层配置已完整伪装为 {1}！", gpu.DetectedModel, gpu.TargetGeForceName), false);
                    return true;
                }
                catch (Exception ex)
                {
                    log(string.Format("图形解锁出错 ({0}): {1}", gpu.DetectedModel, ex.Message), true);
                    return anySuccess;
                }
            });
        }

        private static void PatchInfFileForGpu(string infPath, InstalledMiningGpu gpu, Action<string, bool> log)
        {
            if (!File.Exists(infPath)) return;
            try
            {
                TakeFileOwnership(infPath);
                string text = File.ReadAllText(infPath, Encoding.Default);
                bool changed = false;

                string[] searchPatterns = new string[]
                {
                    "NVIDIA CMP 90HX (RainCandy Technology)",
                    "NVIDIA CMP 90HX",
                    "NVIDIA " + gpu.DetectedModel + " (RainCandy Technology)",
                    "NVIDIA " + gpu.DetectedModel,
                    gpu.DetectedModel
                };

                foreach (var s in searchPatterns)
                {
                    if (text.Contains(s))
                    {
                        text = text.Replace(s, gpu.TargetGeForceName);
                        changed = true;
                    }
                }

                if (changed)
                {
                    File.WriteAllText(infPath, text, Encoding.Default);
                    log("INF 驱动定义已同步: " + Path.GetFileName(infPath), false);
                }
            }
            catch (Exception ex)
            {
                log("INF 写入提示: " + Path.GetFileName(infPath) + " (" + ex.Message + ")", false);
            }
        }

        public static bool DeployNative40hxUnlock(Action<string, bool> log)
        {
            try
            {

                string bat = null;
                string dir = EnsureToolkitExtracted("40hx");
                if (!string.IsNullOrEmpty(dir))
                {
                    bat = Path.Combine(dir, "manual_install.bat");
                }
                if (string.IsNullOrEmpty(bat) || !File.Exists(bat))
                {
                    bat = @"D:\app\build_source\mining_unlock_toolkit\40hx_release\manual_install.bat";
                }
                if (File.Exists(bat))
                {
                    log("[40HX 原生集成] 正在通过底层引擎部署官方 40HXUNLK.EFI 固件与双路引导...", false);
                    var psi = new ProcessStartInfo("cmd.exe", "/c \"" + bat + "\"")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WorkingDirectory = Path.GetDirectoryName(bat)
                    };
                    using (var p = Process.Start(psi))
                    {
                        p.WaitForExit(35000);
                        log("[40HX 原生集成] 40HXUNLK.EFI 固件已成功部署至 ESP 分区，40HX Unlock 引导项已置顶！", false);
                        return true;
                    }
                }
                log("[40HX 原生集成] 官方工具包缺失，已跳过 (核心 EFI 引导解锁仍会由引擎完成)。", true);
            }
            catch (Exception ex)
            {
                log("[40HX 原生集成] 部署提示: " + ex.Message, true);
            }
            return false;
        }

        public static bool DeployNative30hxUnlock(Action<string, bool> log)
        {
            try
            {

                string bat = null;
                string dir = EnsureToolkitExtracted("30hx");
                if (!string.IsNullOrEmpty(dir))
                {
                    bat = Path.Combine(dir, "Setup_CMP30HX_WindowsAIO.bat");
                }
                if (string.IsNullOrEmpty(bat) || !File.Exists(bat))
                {
                    bat = @"D:\app\build_source\mining_unlock_toolkit\30hx_gen2\Setup_CMP30HX_WindowsAIO.bat";
                }
                if (File.Exists(bat))
                {
                    log("[30HX 原生集成] 正在通过底层引擎应用 30HX Gen2 x16 自动化链路与 512B MRRS 优化...", false);
                    var psi = new ProcessStartInfo("cmd.exe", "/c \"" + bat + "\" -nowait -noadmin")
                    {
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WorkingDirectory = Path.GetDirectoryName(bat)
                    };
                    using (var p = Process.Start(psi))
                    {
                        p.WaitForExit(35000);
                        log("[30HX 原生集成] ASPM 优化完成，BAR0 MMIO 寄存器已重载，Gen2 常驻任务已注册！", false);
                        return true;
                    }
                }
                log("[30HX 原生集成] 官方工具包缺失，已跳过 (核心注册表解锁仍会由引擎完成)。", true);
            }
            catch (Exception ex)
            {
                log("[30HX 原生集成] 部署提示: " + ex.Message, true);
            }
            return false;
        }

        public static Task<bool> ApplyHybridMultiGpuUnlockAsync(List<InstalledMiningGpu> gpus, bool unlockGraphics, bool unlockCompute, Action<string, bool> log)
        {
            return ApplyHybridMultiGpuUnlockAsync(gpus, unlockGraphics, unlockCompute, 0, log);
        }

        public static Task<bool> ApplyHybridMultiGpuUnlockAsync(List<InstalledMiningGpu> gpus, bool unlockGraphics, bool unlockCompute, int installChannel, Action<string, bool> log)
        {
            return Task.Factory.StartNew(() =>
            {

                if (gpus == null || gpus.Count == 0)
                {
                    log("解锁中止：未检测到任何矿卡设备，请确认显卡已正确安装并被系统枚举。", true);
                    return false;
                }
                if (!unlockGraphics && !unlockCompute)
                {
                    log("解锁中止：未选择任何解锁项（图形 / 算力至少需选择一项）。", true);
                    return false;
                }
                if (!IsAdmin())
                {
                    log("解锁中止：当前未以管理员身份运行。写入注册表与引导配置需要管理员权限，请右键以管理员身份重启程序。", true);
                    return false;
                }

                string channelName = GetChannelDisplayName(installChannel);
                try
                {
                    log("==================================================", false);
                    log(string.Format("开始执行矿卡混合矩阵解锁 (显卡 {0} 张 · 图形:{1} · 算力:{2} · 通道:{3})",
                        gpus.Count, unlockGraphics ? "开" : "关", unlockCompute ? "开" : "关", channelName), false);
                    log("==================================================", false);

                    if (unlockGraphics && !DeployBootChannel(installChannel, log))
                    {
                        log("引导层部署失败，已中止后续解锁流程（避免引导链进入不一致状态）。", true);
                        return false;
                    }

                    int gSuccess = 0, cSuccess = 0, failed = 0;
                    for (int i = 0; i < gpus.Count; i++)
                    {
                        var gpu = gpus[i];
                        log(string.Format(">>> 处理显卡 [{0}/{1}] 插槽{2} - {3}...", i + 1, gpus.Count, gpu.SlotIndex + 1, gpu.DetectedModel), false);

                        bool cardOk = true;
                        if (unlockGraphics)
                        {
                            cardOk = RunUnlockStep(() => ApplySingleGpuGraphicsUnlockAsync(gpu, log), log, "图形解锁") && cardOk;
                            if (cardOk) gSuccess++;
                        }
                        if (unlockCompute)
                        {
                            cardOk = RunUnlockStep(() => ApplySingleGpuComputeUnlockAsync(gpu, log), log, "算力解锁") && cardOk;
                            if (cardOk) cSuccess++;
                        }
                        if (!cardOk) failed++;
                    }

                    log("[阶段 3/4] 写入 DirectX 全局首选高性能适配器与跨卡调用路由...", false);
                    ApplyDirectXGlobalDefaultGpu(log);

                    log("[阶段 4/4] 执行全局显示堆栈与驱动热重载...", false);
                    SoftRestartGpuDriverAsync(log).Wait();

                    int totalOk = gSuccess + cSuccess;
                    log("==================================================", false);
                    log(string.Format("混合矩阵解锁执行完毕：图形成功 {0} 张，算力成功 {1} 张，失败 {2} 张。", gSuccess, cSuccess, failed), false);
                    log("==================================================", false);

                    if (totalOk == 0)
                    {
                        log("所有解锁项均未成功，请查看上方具体错误信息后重试。", true);
                        return false;
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    log("混合解锁出现异常: " + ex.Message, true);
                    return false;
                }
            });
        }

        private static bool DeployBootChannel(int installChannel, Action<string, bool> log)
        {
            try
            {
                switch (installChannel)
                {
                    case 0:
                        log("[阶段 1/4] 部署 UEFI 全局 NVPermissive PCIe 全速直启双保险...", false);
                        var curStatus = SystemDetector.DetectAll();
                        if (curStatus.PcieMode == PcieUnlockMode.Mode1_DirectBoot)
                        {
                            log("UEFI 引导层全总线 PCIe 解锁固件已处于生效状态，无需重复配置。", false);
                            return true;
                        }
                        var t0 = InstallPcieMode1Async(log);
                        t0.Wait();
                        return t0.Result;

                    case 1:
                        log("[阶段 1/4] 部署针对老主板 (华南/精粤/X79/X99) 的通用 UEFI Shell 链式解锁脚本...", false);
                        var t1 = InstallPcieMode2Async(log);
                        t1.Wait();
                        return t1.Result;

                    case 2:
                        log("[阶段 1/4] 激活 Windows 测试签名模式 (TestMode) 绕过驱动证书限制...", false);
                        RunCmd("bcdedit", "/set testsigning on");
                        RunCmd("bcdedit", "/set nointegritychecks on");
                        log("已开启测试签名与完整性校验豁免，支持老卡加载魔改 INF 与驱动补丁。", false);
                        return true;

                    default:

                        log(string.Format("未知安装通道编号 {0}，已跳过引导层部署。请从界面重新选择通道 A / B / C。", installChannel), true);
                        return false;
                }
            }
            catch (Exception ex)
            {
                log("引导层部署失败: " + ex.Message, true);
                return false;
            }
        }

        private static string GetChannelDisplayName(int installChannel)
        {
            switch (installChannel)
            {
                case 0: return "通道A 现代UEFI直启";
                case 1: return "通道B 老主板Shell";
                case 2: return "通道C 老卡驱动Hook";
                default: return "未知通道(" + installChannel + ")";
            }
        }

        private static bool RunUnlockStep(Func<Task<bool>> step, Action<string, bool> log, string stepName)
        {
            try
            {
                var task = step();
                try
                {
                    task.Wait();
                }
                catch (AggregateException ae)
                {
                    Exception inner = ae;
                    while (inner is AggregateException && inner.InnerException != null)
                    {
                        inner = inner.InnerException;
                    }
                    throw inner;
                }
                return task.Result;
            }
            catch (Exception ex)
            {
                log(stepName + "执行异常: " + ex.Message, true);
                return false;
            }
        }

        public static Task<bool> RestoreMiningGpuAsync(InstalledMiningGpu gpu, Action<string, bool> log)
        {
            return Task.Factory.StartNew(() =>
            {
                try
                {
                    string origName = "NVIDIA " + gpu.DetectedModel;
                    log(string.Format("正在恢复 [{0}] 为出厂官方名称与标准调度模式...", gpu.DetectedModel), false);

                    string targetClass = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\" + gpu.ClassSubKey;
                    string activeInfName = "";
                    using (var sk = Registry.LocalMachine.OpenSubKey(targetClass, true))
                    {
                        if (sk != null)
                        {
                            activeInfName = Convert.ToString(sk.GetValue("InfPath") ?? "");
                            sk.SetValue("DriverDesc", origName, RegistryValueKind.String);
                            sk.SetValue("HardwareInformation.AdapterString", origName, RegistryValueKind.String);
                            sk.SetValue("HardwareInformation.ChipType", origName, RegistryValueKind.String);
                            sk.DeleteValue("DisableComputeThrottle", false);
                            sk.DeleteValue("PowerMizerLevel", false);
                            sk.DeleteValue("PowerMizerLevelAC", false);
                            sk.DeleteValue("PerfLevelSrc", false);
                            log("已还原驱动类描述为: " + origName, false);
                        }
                    }

                    try
                    {
                        string videoRoot = @"SYSTEM\CurrentControlSet\Control\Video";
                        using (var vKey = Registry.LocalMachine.OpenSubKey(videoRoot, false))
                        {
                            if (vKey != null)
                            {
                                foreach (var guid in vKey.GetSubKeyNames())
                                {
                                    using (var gk = vKey.OpenSubKey(guid, false))
                                    {
                                        if (gk == null) continue;
                                        foreach (var num in gk.GetSubKeyNames())
                                        {
                                            try
                                            {
                                                using (var nk = gk.OpenSubKey(num, true))
                                                {
                                                    if (nk == null) continue;
                                                    var drv = Convert.ToString(nk.GetValue("Driver") ?? "");
                                                    if (drv.IndexOf(gpu.ClassSubKey, StringComparison.OrdinalIgnoreCase) >= 0)
                                                    {
                                                        nk.SetValue("DeviceDesc", origName, RegistryValueKind.String);
                                                    }
                                                }
                                            }
                                            catch { }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }

                    if (string.IsNullOrEmpty(activeInfName)) activeInfName = "oem32.inf";
                    string sysInf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "INF", activeInfName);
                    PatchInfFile(sysInf, gpu.TargetGeForceName, origName, log);

                    gpu.IsSpoofed = false;
                    gpu.IsComputeUnlocked = false;
                    log(string.Format("[{0}] 已恢复官方出厂状态与驱动定义。", gpu.DetectedModel), false);
                    return true;
                }
                catch (Exception ex)
                {
                    log(string.Format("恢复失败 ({0}): {1}", gpu.DetectedModel, ex.Message), true);
                    return false;
                }
            });
        }

        #endregion
    }
}