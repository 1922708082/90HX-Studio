using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CMP90HXToolbox
{
    /// <summary>
    /// 日志读取 / 导出 / 清空，以及系统快照的建立与删除。
    ///
    /// 之前这些命令前端都在发，宿主一律回 NOT_SUPPORTED：
    /// 日志页永远空白（连初始内容都拿不到），维护页的快照列表也没有来源。
    /// </summary>
    internal static class Diagnostics
    {
        private static string LogFile
        {
            get
            {
                try { return Path.Combine(NativeCore.GetLogsDirectory(), "webshell.log"); }
                catch { return null; }
            }
        }

        public static string BackupDir
        {
            get
            {
                try
                {
                    string d = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "CMP90HX_Toolbox", "backup");
                    if (!Directory.Exists(d)) Directory.CreateDirectory(d);
                    return d;
                }
                catch { return null; }
            }
        }

        // ---------- 日志 ----------

        /// <summary>读日志尾部若干行，解析成 {at, level, text} 列表。</summary>
        public static List<string[]> ReadLogTail(int limit, string levelFilter)
        {
            List<string[]> rows = new List<string[]>();
            try
            {
                string p = LogFile;
                if (string.IsNullOrEmpty(p) || !File.Exists(p)) return rows;

                // 日志不大（几十 KB），整体读入后取尾部即可，避免依赖外部命令
                string[] all = File.ReadAllLines(p, Encoding.UTF8);
                int take = limit <= 0 ? 100 : (limit > 500 ? 500 : limit);
                int start = Math.Max(0, all.Length - take);

                for (int i = start; i < all.Length; i++)
                {
                    string line = all[i];
                    if (string.IsNullOrEmpty(line)) continue;

                    string at = "", text = line, level = "info";
                    int rb = line.IndexOf(']');
                    if (line.Length > 1 && line[0] == '[' && rb > 1)
                    {
                        at = line.Substring(1, rb - 1);
                        text = line.Substring(rb + 1).Trim();
                    }
                    if (text.IndexOf("[WARN]", StringComparison.OrdinalIgnoreCase) >= 0) level = "warn";
                    else if (text.IndexOf("[ERR", StringComparison.OrdinalIgnoreCase) >= 0) level = "error";
                    else if (text.IndexOf("[DIAG]", StringComparison.OrdinalIgnoreCase) >= 0) level = "debug";

                    if (!string.IsNullOrEmpty(levelFilter) && levelFilter != "all"
                        && !string.Equals(levelFilter, level, StringComparison.OrdinalIgnoreCase)) continue;

                    rows.Add(new string[] { at, level, text });
                }
            }
            catch { }
            return rows;
        }

        /// <summary>返回日志全文，供复制到剪贴板。过长时只取尾部 2000 行。</summary>
        public static string ReadLogText()
        {
            try
            {
                string p = LogFile;
                if (string.IsNullOrEmpty(p) || !File.Exists(p)) return null;
                string[] all = File.ReadAllLines(p, Encoding.UTF8);
                int start = Math.Max(0, all.Length - 2000);
                StringBuilder sb = new StringBuilder(64 * 1024);
                for (int i = start; i < all.Length; i++) sb.AppendLine(all[i]);
                return sb.ToString();
            }
            catch { return null; }
        }

        /// <summary>把当前日志另存到指定路径。</summary>
        public static string ExportLog(string targetPath)
        {
            try
            {
                string src = LogFile;
                if (string.IsNullOrEmpty(src) || !File.Exists(src) || string.IsNullOrEmpty(targetPath)) return null;
                if (string.Equals(src, targetPath, StringComparison.OrdinalIgnoreCase)) return null;
                File.Copy(src, targetPath, true);
                return targetPath;
            }
            catch { return null; }
        }

        /// <summary>清空日志（截断，不删除文件，避免句柄失效）。</summary>
        public static bool ClearLog()
        {
            try
            {
                string p = LogFile;
                if (string.IsNullOrEmpty(p) || !File.Exists(p)) return true;
                File.WriteAllText(p, string.Empty, new UTF8Encoding(false));
                return true;
            }
            catch { return false; }
        }

        // ---------- 快照 ----------

        /// <summary>
        /// 建立快照：把显卡驱动类注册表的关键键值与本机显卡身份落盘。
        /// 名字形如 snap_20261021_173000_<label>.txt，id 即文件名。
        /// </summary>
        public static string CreateSnapshot(string label)
        {
            try
            {
                string dir = BackupDir;
                if (string.IsNullOrEmpty(dir)) return null;

                string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string safe = SanitizeFileName(string.IsNullOrEmpty(label) ? "手动快照" : label);
                if (safe.Length > 24) safe = safe.Substring(0, 24);
                string id = "snap_" + ts + "_" + safe + ".txt";
                string full = Path.Combine(dir, id);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# HX Studio 系统快照");
                sb.AppendLine("# id       : " + id);
                sb.AppendLine("# label    : " + label);
                sb.AppendLine("# created  : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine("# 说明     : 记录显卡驱动类注册表键值与当前身份，供人工核对/回滚参考");
                sb.AppendLine();

                try
                {
                    SystemStatus st = SystemDetector.DetectAll();
                    if (st != null)
                    {
                        sb.AppendLine("[GPU]");
                        sb.AppendLine("desc        = " + st.GpuDesc);
                        sb.AppendLine("gpuId       = " + st.GpuId);
                        sb.AppendLine("classSubKey = " + st.ClassSubKey);
                        sb.AppendLine("isSpoofed   = " + st.IsSpoofedTo3080);
                        sb.AppendLine("pcieMode    = " + st.PcieModeDesc);
                        sb.AppendLine("isUefi      = " + st.IsUefi);
                        sb.AppendLine("secureBoot  = " + st.SecureBootEnabled);
                        sb.AppendLine("driverVer   = " + st.DriverVersion);
                        sb.AppendLine();
                    }
                }
                catch { }

                // 驱动类注册表项
                const string classRoot = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
                try
                {
                    using (Microsoft.Win32.RegistryKey root =
                        Microsoft.Win32.Registry.LocalMachine.OpenSubKey(classRoot, false))
                    {
                        if (root != null)
                        {
                            sb.AppendLine("[REGISTRY]");
                            foreach (string sub in root.GetSubKeyNames())
                            {
                                if (sub.Length != 4) continue;
                                using (Microsoft.Win32.RegistryKey sk = root.OpenSubKey(sub, false))
                                {
                                    if (sk == null) continue;
                                    object desc = sk.GetValue("DriverDesc");
                                    if (desc == null) continue;
                                    sb.AppendLine(sub + ".DriverDesc       = " + desc);
                                    sb.AppendLine(sub + ".MatchingDeviceId = " + (sk.GetValue("MatchingDeviceId") ?? ""));
                                    sb.AppendLine(sub + ".InfPath          = " + (sk.GetValue("InfPath") ?? ""));
                                    sb.AppendLine();
                                }
                            }
                        }
                    }
                }
                catch { }

                File.WriteAllText(full, sb.ToString(), new UTF8Encoding(false));
                return id;
            }
            catch { return null; }
        }

        public static bool DeleteSnapshot(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id)) return false;
                // 只允许删备份目录下的文件名，禁止路径穿越
                if (id.IndexOf('\\') >= 0 || id.IndexOf('/') >= 0 || id.IndexOf("..") >= 0) return false;
                string dir = BackupDir;
                if (string.IsNullOrEmpty(dir)) return false;
                string full = Path.Combine(dir, id);
                if (!File.Exists(full)) return false;
                File.Delete(full);
                return true;
            }
            catch { return false; }
        }

        private static string SanitizeFileName(string s)
        {
            StringBuilder sb = new StringBuilder(s.Length);
            char[] bad = Path.GetInvalidFileNameChars();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                bool ok = true;
                for (int j = 0; j < bad.Length; j++) if (bad[j] == c) { ok = false; break; }
                sb.Append(ok ? c : '_');
            }
            return sb.ToString().Replace(' ', '_');
        }
    }
}
