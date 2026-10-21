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

        // ---------- 着色器 / 覆盖层缓存 ----------

        /// <summary>
        /// 清理 NVIDIA 着色器缓存目录。
        /// 只处理三个已知缓存路径，**不递归删除父目录**，也不碰用户文档等位置。
        /// 缓存删掉会自动重建，最多让游戏首次加载稍慢一点。
        /// </summary>
        public static string CleanGpuCache(out long freedBytes, out int fileCount)
        {
            freedBytes = 0;
            fileCount = 0;
            StringBuilder detail = new StringBuilder();

            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] targets = new string[]
            {
                Path.Combine(local, "NVIDIA", "DXCache"),
                Path.Combine(local, "NVIDIA", "GLCache"),
                Path.Combine(local, "NVIDIA Corporation", "NV_Cache"),
                Path.Combine(local, "Temp", "NVIDIA Corporation", "NV_Cache")
            };

            for (int i = 0; i < targets.Length; i++)
            {
                string dir = targets[i];
                try
                {
                    if (!Directory.Exists(dir)) continue;

                    long before = 0;
                    int n = 0;
                    string[] files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories);
                    for (int j = 0; j < files.Length; j++)
                    {
                        try
                        {
                            FileInfo fi = new FileInfo(files[j]);
                            before += fi.Length;
                            fi.Delete();
                            n++;
                        }
                        catch { /* 被占用的单个文件跳过，不影响整体 */ }
                    }
                    freedBytes += before;
                    fileCount += n;
                    detail.Append(Path.GetFileName(dir)).Append('=').Append(n).Append("个 ");
                }
                catch { }
            }

            return detail.ToString().Trim();
        }

        // ---------- NVIDIA App 覆盖层文件备份 ----------

        private static string[] OverlayDirs()
        {
            return new string[] {
                @"C:\Program Files\NVIDIA Corporation\NVIDIA App\www",
                @"C:\Program Files\NVIDIA Corporation\NVIDIA app\www",
                @"C:\Program Files\NVIDIA Corporation\NVIDIA App\osc",
                @"C:\Program Files\NVIDIA Corporation\NVIDIA app\osc",
                @"C:\Program Files (x86)\NVIDIA Corporation\NVIDIA App\www",
                @"C:\Program Files (x86)\NVIDIA Corporation\NVIDIA App\osc",
                @"D:\Program Files\NVIDIA Corporation\NVIDIA app\www",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                             @"NVIDIA Corporation\NVIDIA App\www")
            };
        }

        /// <summary>
        /// 打补丁前先给每个 main.*.js 存一份 .hxbak。
        ///
        /// 老版解锁会自己在补丁流程里建 .bak，但实测它备份的文件名与实际改写的
        /// 不是同一个（改了 2899f0d8…js，却建了 884630…js.bak），导致还原时
        /// 找不到对应备份、什么也恢复不了。这里用固定后缀 .hxbak 自己兜一层。
        /// </summary>
        public static int BackupOverlayFiles(Action<string, bool> log)
        {
            int count = 0;
            string[] dirs = OverlayDirs();
            for (int i = 0; i < dirs.Length; i++)
            {
                try
                {
                    if (!Directory.Exists(dirs[i])) continue;
                    string[] files = Directory.GetFiles(dirs[i], "main.*.js");
                    for (int j = 0; j < files.Length; j++)
                    {
                        string bak = files[j] + ".hxbak";
                        if (File.Exists(bak)) continue;      // 已有原始备份就不要覆盖
                        File.Copy(files[j], bak, false);
                        count++;
                        if (log != null) log("已备份 " + Path.GetFileName(files[j]) + " → .hxbak", false);
                    }
                }
                catch (Exception ex)
                {
                    if (log != null) log("备份失败: " + ex.Message, true);
                }
            }
            return count;
        }

        /// <summary>从 .hxbak 还原覆盖层文件；成功一个返回 1。</summary>
        public static int RestoreOverlayFiles(Action<string, bool> log)
        {
            int count = 0;
            string[] dirs = OverlayDirs();
            for (int i = 0; i < dirs.Length; i++)
            {
                try
                {
                    if (!Directory.Exists(dirs[i])) continue;
                    string[] baks = Directory.GetFiles(dirs[i], "main.*.js.hxbak");
                    for (int j = 0; j < baks.Length; j++)
                    {
                        string orig = baks[j].Substring(0, baks[j].Length - 6); // 去掉 .hxbak
                        try
                        {
                            // NVIDIA App 还在运行时文件被占，单次复制常失败（用户反馈要点两次）。
                            // 这里重试 3 次，每次间隔 700ms，第二次起先清只读属性。
                            bool done = false;
                            Exception last = null;
                            for (int attempt = 1; attempt <= 3 && !done; attempt++)
                            {
                                try
                                {
                                    if (File.Exists(orig)) File.SetAttributes(orig, FileAttributes.Normal);
                                    File.Copy(baks[j], orig, true);
                                    done = true;
                                }
                                catch (Exception ex)
                                {
                                    last = ex;
                                    System.Threading.Thread.Sleep(700);
                                }
                            }
                            if (!done) throw last;
                            File.Delete(baks[j]);
                            count++;
                            if (log != null) log("已还原 " + Path.GetFileName(orig), false);
                        }
                        catch (Exception ex)
                        {
                            if (log != null) log("还原 " + Path.GetFileName(orig) + " 失败: " + ex.Message, true);
                        }
                    }
                }
                catch { }
            }
            return count;
        }

        /// <summary>本地是否存在我们自己打的备份（界面用来判断能不能还原）。</summary>
        public static bool HasOverlayBackup()
        {
            string[] dirs = OverlayDirs();
            for (int i = 0; i < dirs.Length; i++)
            {
                try
                {
                    if (!Directory.Exists(dirs[i])) continue;
                    if (Directory.GetFiles(dirs[i], "main.*.js.hxbak").Length > 0) return true;
                }
                catch { }
            }
            return false;
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
