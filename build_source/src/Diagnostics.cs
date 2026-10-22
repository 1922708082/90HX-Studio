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

        private const string GpuClassRoot =
            @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

        /// <summary>带这段的快照才记录了可写回的键值；旧快照只有 [GPU]/[REGISTRY] 的人读信息。</summary>
        private const string RollbackSection = "[ROLLBACK]";

        /// <summary>
        /// 可回滚的字符串值白名单。
        /// 快照文件是磁盘上的纯文本，回滚时按文件内容写注册表——
        /// 不设白名单就等于「谁能改这个 txt 谁就能改显卡驱动类的任意值」。
        /// </summary>
        private static readonly string[] RollbackStringValues = new string[]
        {
            "DriverDesc",
            "HardwareInformation.AdapterString",
            "HardwareInformation.ChipType"
        };

        /// <summary>可回滚的 DWORD 值白名单（伪装时被写入的那几个开关）。</summary>
        private static readonly string[] RollbackDwordValues = new string[]
        {
            "AdapterType",
            "EnableMsHybrid",
            "RMDisableTcc",
            "D3DMappingLayerEnable"
        };

        /// <summary>
        /// 建立快照：把显卡驱动类注册表的关键键值与本机显卡身份落盘。
        /// 名字形如 snap_20261021_173000_<label>.txt，id 即文件名。
        ///
        /// schema 2 起额外写 [ROLLBACK] 段（可被 snapshot.restore 写回的原始值）。
        /// 旧快照（schema 1）没有该段，读取时仍能识别，但回滚会明确失败——见 RestoreSnapshot。
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
                sb.AppendLine("# schema   : 2");
                sb.AppendLine("# id       : " + id);
                sb.AppendLine("# label    : " + label);
                sb.AppendLine("# created  : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine("# 说明     : 记录显卡驱动类注册表键值与当前身份；[ROLLBACK] 段可用 snapshot.restore 写回");
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

                // 驱动类注册表项：人读段与可回滚段在同一次遍历里生成，
                // 避免两次读取之间状态变化导致两段内容不一致。
                StringBuilder human = new StringBuilder();
                StringBuilder rollback = new StringBuilder();
                try
                {
                    using (Microsoft.Win32.RegistryKey root =
                        Microsoft.Win32.Registry.LocalMachine.OpenSubKey(GpuClassRoot, false))
                    {
                        if (root != null)
                        {
                            foreach (string sub in root.GetSubKeyNames())
                            {
                                if (sub.Length != 4) continue;
                                using (Microsoft.Win32.RegistryKey sk = root.OpenSubKey(sub, false))
                                {
                                    if (sk == null) continue;
                                    object desc = sk.GetValue("DriverDesc");
                                    if (desc == null) continue;
                                    human.AppendLine(sub + ".DriverDesc       = " + desc);
                                    human.AppendLine(sub + ".MatchingDeviceId = " + (sk.GetValue("MatchingDeviceId") ?? ""));
                                    human.AppendLine(sub + ".InfPath          = " + (sk.GetValue("InfPath") ?? ""));
                                    human.AppendLine();

                                    // 读不到的值（权限/异常）不写进去：
                                    // 写了空编码等于制造一条永远回滚失败的记录。
                                    for (int i = 0; i < RollbackStringValues.Length; i++)
                                    {
                                        string enc = EncodeRegValue(sk, RollbackStringValues[i]);
                                        if (enc != null)
                                            rollback.AppendLine(sub + "." + RollbackStringValues[i] + " = " + enc);
                                    }
                                    for (int i = 0; i < RollbackDwordValues.Length; i++)
                                    {
                                        string enc = EncodeRegValue(sk, RollbackDwordValues[i]);
                                        if (enc != null)
                                            rollback.AppendLine(sub + "." + RollbackDwordValues[i] + " = " + enc);
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }

                if (human.Length > 0)
                {
                    sb.AppendLine("[REGISTRY]");
                    sb.Append(human);
                }

                sb.AppendLine(RollbackSection);
                sb.AppendLine("# 格式: 子键.值名 = S:字符串 / D:数字 / -（快照时不存在）");
                sb.AppendLine("# 只接受白名单值名，由 snapshot.restore 写回");
                sb.Append(rollback);

                File.WriteAllText(full, sb.ToString(), new UTF8Encoding(false));
                return id;
            }
            catch { return null; }
        }

        /// <summary>把注册表值编码成快照里的一行：S:字符串 / D:数字 / -（不存在）。读不到返回 null。</summary>
        private static string EncodeRegValue(Microsoft.Win32.RegistryKey sk, string name)
        {
            try
            {
                object v = sk.GetValue(name, null, Microsoft.Win32.RegistryValueOptions.DoNotExpandEnvironmentNames);
                if (v == null) return "-";

                if (v is int)
                    return "D:" + ((int)v).ToString(System.Globalization.CultureInfo.InvariantCulture);

                string s = Convert.ToString(v);
                if (s == null) return "-";
                // 一行一个键值，换行会把文件结构写坏
                s = s.Replace("\r", " ").Replace("\n", " ");
                return "S:" + s;
            }
            catch { return null; }
        }

        private static bool IsRollbackValueName(string name)
        {
            for (int i = 0; i < RollbackStringValues.Length; i++)
                if (string.Equals(name, RollbackStringValues[i], StringComparison.OrdinalIgnoreCase)) return true;
            for (int i = 0; i < RollbackDwordValues.Length; i++)
                if (string.Equals(name, RollbackDwordValues[i], StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>
        /// 解析 [ROLLBACK] 段，返回 {子键, 值名, 编码} 列表。
        /// hasSection 单独返回：用来区分「旧快照（没有这段）」和「新快照但没记到东西」，
        /// 两者给用户的解释不一样。
        /// </summary>
        private static List<string[]> ParseRollbackEntries(string[] lines, out bool hasSection)
        {
            hasSection = false;
            List<string[]> entries = new List<string[]>();
            if (lines == null) return entries;

            bool inSection = false;
            for (int li = 0; li < lines.Length; li++)
            {
                string raw = lines[li];
                if (raw == null) continue;
                string t = raw.Trim();
                if (t.Length == 0) continue;
                if (t[0] == '#') continue;

                if (t[0] == '[')
                {
                    inSection = string.Equals(t, RollbackSection, StringComparison.OrdinalIgnoreCase);
                    if (inSection) hasSection = true;
                    continue;
                }
                if (!inSection) continue;

                // 子键（4 位数字）.值名 = 编码
                int dot = t.IndexOf('.');
                if (dot != 4) continue;
                string sub = t.Substring(0, 4);
                bool subOk = true;
                for (int i = 0; i < 4; i++) if (!char.IsDigit(sub[i])) { subOk = false; break; }
                if (!subOk) continue;

                int sep = t.IndexOf(" = ", StringComparison.Ordinal);
                if (sep <= dot) continue;
                string name = t.Substring(dot + 1, sep - dot - 1).Trim();
                if (!IsRollbackValueName(name)) continue;      // 白名单外的值名一律忽略

                entries.Add(new string[] { sub, name, t.Substring(sep + 3).Trim() });
            }
            return entries;
        }

        /// <summary>该快照是否带有可回滚数据（旧格式只有人读信息，返回 false）。</summary>
        public static bool IsRestorable(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id)) return false;
                if (id.IndexOf('\\') >= 0 || id.IndexOf('/') >= 0 || id.IndexOf("..") >= 0) return false;
                string dir = BackupDir;
                if (string.IsNullOrEmpty(dir)) return false;
                string full = Path.Combine(dir, id);
                if (!File.Exists(full)) return false;

                bool hasSection;
                List<string[]> entries = ParseRollbackEntries(File.ReadAllLines(full, Encoding.UTF8), out hasSection);
                return hasSection && entries.Count > 0;
            }
            catch { return false; }
        }

        /// <summary>回滚快照的结果。Ok=false 时 Code/Message 直接回给前端，绝不返回假成功。</summary>
        public sealed class SnapshotRestoreResult
        {
            public bool Ok;
            public string Code;
            public string Message;
        }

        /// <summary>
        /// 把快照里记录的显卡驱动类键值写回注册表，并逐项回读校验。
        ///
        /// 旧快照（schema 1）只记了 desc / gpuId / classSubKey 这些人读信息，
        /// 没有任何「当时的值是多少」的数据，写不回去——必须明确失败，
        /// 否则就是最坏的一种假成功：用户以为回滚了，实际一个字节都没改。
        /// </summary>
        public static SnapshotRestoreResult RestoreSnapshot(string id, Action<string, bool> log)
        {
            SnapshotRestoreResult r = new SnapshotRestoreResult();

            if (string.IsNullOrEmpty(id))
            {
                r.Code = "VALIDATION_FAILED";
                r.Message = "缺少快照 ID";
                return r;
            }
            // 只允许备份目录下的文件名，禁止路径穿越
            if (id.IndexOf('\\') >= 0 || id.IndexOf('/') >= 0 || id.IndexOf("..") >= 0)
            {
                r.Code = "VALIDATION_FAILED";
                r.Message = "快照 ID 非法：" + id;
                return r;
            }

            string dir = BackupDir;
            if (string.IsNullOrEmpty(dir))
            {
                r.Code = "INTERNAL";
                r.Message = "无法定位快照目录（请检查 ProgramData 写入权限）";
                return r;
            }

            string full = Path.Combine(dir, id);
            if (!File.Exists(full))
            {
                r.Code = "FILE_NOT_FOUND";
                r.Message = "找不到快照文件：" + id;
                return r;
            }

            string[] lines;
            try { lines = File.ReadAllLines(full, Encoding.UTF8); }
            catch (Exception ex)
            {
                r.Code = "FILE_LOCKED";
                r.Message = "读取快照失败：" + ex.Message;
                return r;
            }

            bool hasSection;
            List<string[]> entries = ParseRollbackEntries(lines, out hasSection);

            if (!hasSection)
            {
                r.Code = "VALIDATION_FAILED";
                r.Message = "该快照只记录了展示信息，无法用于回滚，请创建新快照";
                return r;
            }
            if (entries.Count == 0)
            {
                r.Code = "VALIDATION_FAILED";
                r.Message = "该快照没有记录可回滚的注册表键值（创建时可能未检测到显卡），请重新创建快照";
                return r;
            }

            List<string> done = new List<string>();
            List<string> failed = new List<string>();

            try
            {
                using (Microsoft.Win32.RegistryKey root =
                    Microsoft.Win32.Registry.LocalMachine.OpenSubKey(GpuClassRoot, true))
                {
                    if (root == null)
                    {
                        r.Code = "INTERNAL";
                        r.Message = "无法以写入方式打开显卡驱动类注册表项（需要管理员权限）";
                        return r;
                    }

                    for (int i = 0; i < entries.Count; i++)
                    {
                        string sub = entries[i][0];
                        string name = entries[i][1];
                        string enc = entries[i][2];
                        string label = sub + "." + name;

                        try
                        {
                            using (Microsoft.Win32.RegistryKey sk = root.OpenSubKey(sub, true))
                            {
                                if (sk == null)
                                {
                                    failed.Add(label + "：驱动类子键 " + sub + " 不存在");
                                    continue;
                                }

                                // 只写仍是显卡项的驱动类子键：重装驱动后子键号可能被别的设备复用，
                                // 盲目按旧编号写会把 DriverDesc 塞进不相干的驱动项。
                                if (Convert.ToString(sk.GetValue("DriverDesc") ?? "").Length == 0)
                                {
                                    failed.Add(label + "：驱动类子键 " + sub + " 已不是显卡项（DriverDesc 为空），未写入");
                                    continue;
                                }

                                ApplyRegValue(sk, name, enc);

                                // 回读校验：写进去的值必须与快照记录一致
                                string actual = EncodeRegValue(sk, name);
                                if (!string.Equals(actual, enc, StringComparison.Ordinal))
                                {
                                    failed.Add(label + "：回读不符（期望 " + enc + "，实际 " + (actual ?? "读不到") + "）");
                                    continue;
                                }
                                done.Add(label);
                                if (log != null) log("已回滚 " + label + " = " + enc, false);
                            }
                        }
                        catch (Exception exOne)
                        {
                            failed.Add(label + "：" + exOne.Message);
                        }
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                r.Code = "INTERNAL";
                r.Message = "注册表写入被拒绝（需要管理员权限）：" + ex.Message;
                return r;
            }
            catch (Exception ex)
            {
                r.Code = "INTERNAL";
                r.Message = "回滚注册表失败：" + ex.Message + "（已成功 " + done.Count + " 项）";
                return r;
            }

            if (failed.Count > 0)
            {
                r.Code = "INTERNAL";
                StringBuilder sb = new StringBuilder(320);
                sb.Append("回滚未完成：成功 ").Append(done.Count).Append(" 项，失败 ").Append(failed.Count).Append(" 项（");
                for (int i = 0; i < failed.Count; i++)
                {
                    if (i > 0) sb.Append('；');
                    sb.Append(failed[i]);
                }
                sb.Append('）');
                r.Message = sb.ToString();
                return r;
            }

            r.Ok = true;
            r.Message = "已回滚 " + done.Count + " 项显卡注册表键值到快照记录的状态（含显卡名称），快照: " + id;
            return r;
        }

        /// <summary>按快照里的编码写/删一个注册表值。编码非法直接抛，由调用方计入失败。</summary>
        private static void ApplyRegValue(Microsoft.Win32.RegistryKey sk, string name, string encoded)
        {
            if (encoded == "-")
            {
                sk.DeleteValue(name, false);
                return;
            }
            if (encoded.StartsWith("S:", StringComparison.Ordinal))
            {
                sk.SetValue(name, encoded.Substring(2), Microsoft.Win32.RegistryValueKind.String);
                return;
            }
            if (encoded.StartsWith("D:", StringComparison.Ordinal))
            {
                int n;
                if (!int.TryParse(encoded.Substring(2),
                        System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture, out n))
                    throw new FormatException("快照里的 DWORD 值无法解析: " + encoded);
                sk.SetValue(name, n, Microsoft.Win32.RegistryValueKind.DWord);
                return;
            }
            throw new FormatException("无法识别的值编码: " + encoded);
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
