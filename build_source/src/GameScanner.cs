using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace CMP90HXToolbox
{
    /// <summary>
    /// 本机游戏目录扫描（只读）。
    ///
    /// 用途：双卡绑定页需要知道机器上装了哪些游戏、在哪个目录，
    /// 才能把指定程序绑定到 90HX 做渲染。
    ///
    /// 只读 VDF/注册表，不写入任何东西，扫描不到就返回空列表，不猜。
    /// </summary>
    internal static class GameScanner
    {
        public sealed class GameEntry
        {
            public string Name;
            public string Path;
            public string Exe;
            public string Source;   // Steam / Epic / 其他
        }

        /// <summary>枚举本机磁盘（固定盘优先），供界面选择要扫描的盘符。</summary>
        public static List<string[]> ListDrives()
        {
            List<string[]> list = new List<string[]>();
            try
            {
                DriveInfo[] all = DriveInfo.GetDrives();
                for (int i = 0; i < all.Length; i++)
                {
                    try
                    {
                        DriveInfo d = all[i];
                        if (!d.IsReady) continue;
                        if (d.DriveType != DriveType.Fixed && d.DriveType != DriveType.Removable) continue;
                        string label = "";
                        try { label = d.VolumeLabel; } catch { }
                        list.Add(new string[] {
                            d.Name,                                  // 如 D:\
                            d.DriveType == DriveType.Removable ? "可移动" : "本地",
                            label,
                            (d.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0).ToString("0")   // 剩余 GB
                        });
                    }
                    catch { }
                }
            }
            catch { }
            return list;
        }

        /// <summary>
        /// 按盘符扫描游戏目录。
        /// 策略：只往下走 3 层，跳过系统目录；某个目录内存在 20MB 以上的 exe 就算候选。
        /// 全程只读，并有目录数上限，避免大容量盘扫太久。
        /// </summary>
        public static void ScanDrive(string root, List<GameEntry> list, HashSet<string> seen)
        {
            if (string.IsNullOrEmpty(root)) return;
            try
            {
                if (!Directory.Exists(root)) return;

                int budget = 4000;   // 最多访问 4000 个目录
                WalkForGames(root, 0, 3, list, seen, ref budget);
            }
            catch { }
        }

        private static void WalkForGames(string dir, int depth, int maxDepth,
                                         List<GameEntry> list, HashSet<string> seen, ref int budget)
        {
            if (budget <= 0 || depth > maxDepth) return;
            budget--;

            string[] subs;
            try { subs = Directory.GetDirectories(dir); }
            catch { return; }

            for (int i = 0; i < subs.Length; i++)
            {
                if (budget <= 0) return;
                string sub = subs[i];
                string name = Path.GetFileName(sub);

                // 跳过系统/缓存类目录，省时间也避免误判
                if (name.Equals("Windows", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("$Recycle.Bin", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("ProgramData", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("node_modules", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("AppData", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("WindowsApps", StringComparison.OrdinalIgnoreCase))
                    continue;

                bool looksLikeGame = HasBigExe(sub);
                if (looksLikeGame)
                {
                    AddGame(list, seen, name, sub, "磁盘");
                    // 命中的目录不再往下挖，避免把一个游戏拆成几十条
                    continue;
                }
                WalkForGames(sub, depth + 1, maxDepth, list, seen, ref budget);
            }
        }

        /// <summary>目录下（两层内）是否有 20MB 以上的可执行文件。</summary>
        private static bool HasBigExe(string dir)
        {
            try
            {
                string[] exes = Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories);
                int n = 0;
                for (int i = 0; i < exes.Length && n < 60; i++, n++)
                {
                    try
                    {
                        FileInfo fi = new FileInfo(exes[i]);
                        if (fi.Length < 20L * 1024 * 1024) continue;
                        string lower = fi.Name.ToLowerInvariant();
                        if (lower.Contains("unins") || lower.Contains("setup") ||
                            lower.Contains("vcredist") || lower.Contains("dxsetup") ||
                            lower.Contains("update") || lower.Contains("install"))
                            continue;
                        return true;
                    }
                    catch { }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// 扫描本机游戏。
        /// drive 为空时：Steam/Epic 注册表扫描 + 所有固定盘浅层扫描；
        /// drive 指定时：只扫该盘（Steam/Epic 仅在其库位于该盘时才命中，无副作用）。
        /// </summary>
        public static List<GameEntry> Scan(string drive)
        {
            List<GameEntry> list = new List<GameEntry>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool singleDrive = !string.IsNullOrEmpty(drive);

            if (!singleDrive) ScanLaunchers(list, seen);

            if (singleDrive)
            {
                ScanDrive(drive, list, seen);
            }
            else
            {
                // 全部磁盘：逐盘浅扫
                List<string[]> drives = ListDrives();
                for (int i = 0; i < drives.Count; i++)
                    ScanDrive(drives[i][0], list, seen);
            }

            list.Sort(delegate(GameEntry a, GameEntry b)
            {
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return list;
        }

        /// <summary>扫描 Steam 库（含所有附加库目录）与常见启动器目录。</summary>
        public static List<GameEntry> Scan()
        {
            return Scan(null);
        }

        private static void ScanLaunchers(List<GameEntry> list, HashSet<string> seen)
        {
            // 1) Steam：从注册表找安装路径，再读 libraryfolders.vdf 拿所有库
            try
            {
                string steamRoot = null;
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    if (k != null) steamRoot = k.GetValue("SteamPath") as string;
                }
                if (string.IsNullOrEmpty(steamRoot))
                {
                    using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
                    {
                        if (k != null) steamRoot = k.GetValue("InstallPath") as string;
                    }
                }

                if (!string.IsNullOrEmpty(steamRoot))
                {
                    steamRoot = steamRoot.Replace('/', '\\');
                    List<string> libraries = new List<string>();
                    libraries.Add(steamRoot);

                    string vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
                    if (File.Exists(vdf))
                    {
                        foreach (string line in File.ReadAllLines(vdf))
                        {
                            string p = ExtractVdfPath(line);
                            if (!string.IsNullOrEmpty(p)) libraries.Add(p.Replace("\\\\", "\\"));
                        }
                    }

                    for (int i = 0; i < libraries.Count; i++)
                        ScanSteamLibrary(libraries[i], list, seen);
                }
            }
            catch { }

            // 2) Epic：只列 manifest 里的安装目录（不解析 JSON，纯找目录）
            try
            {
                string epic = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "Epic", "EpicGamesLauncher", "Data", "Manifests");
                if (Directory.Exists(epic))
                {
                    foreach (string f in Directory.GetFiles(epic, "*.item"))
                    {
                        // manifest 是 JSON，抓 "InstallLocation":"..." 即可
                        string text = File.ReadAllText(f, Encoding.UTF8);
                        string loc = ExtractJsonString(text, "InstallLocation");
                        string name = ExtractJsonString(text, "DisplayName");
                        if (!string.IsNullOrEmpty(loc) && Directory.Exists(loc))
                            AddGame(list, seen, name, loc, "Epic");
                    }
                }
            }
            catch { }
        }

        private static void ScanSteamLibrary(string libraryRoot, List<GameEntry> list, HashSet<string> seen)
        {
            try
            {
                string common = Path.Combine(libraryRoot, "steamapps", "common");
                if (!Directory.Exists(common)) return;

                string[] dirs = Directory.GetDirectories(common);
                for (int i = 0; i < dirs.Length; i++)
                {
                    string dir = dirs[i];
                    string name = Path.GetFileName(dir);
                    AddGame(list, seen, name, dir, "Steam");
                }
            }
            catch { }
        }

        private static void AddGame(List<GameEntry> list, HashSet<string> seen,
                                    string name, string dir, string source)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            if (!seen.Add(dir)) return;

            string exe = FindMainExe(dir);
            list.Add(new GameEntry
            {
                Name = string.IsNullOrEmpty(name) ? Path.GetFileName(dir) : name,
                Path = dir,
                Exe = exe ?? "",
                Source = source
            });
        }

        /// <summary>在游戏目录里找体积最大的 exe（通常就是主程序）。</summary>
        private static string FindMainExe(string dir)
        {
            try
            {
                string[] exes = Directory.GetFiles(dir, "*.exe", SearchOption.AllDirectories);
                string best = null;
                long bestLen = 0;
                int scanned = 0;
                for (int i = 0; i < exes.Length && scanned < 400; i++, scanned++)
                {
                    try
                    {
                        FileInfo fi = new FileInfo(exes[i]);
                        string lower = fi.Name.ToLowerInvariant();
                        // 排除常见非主程序
                        if (lower.Contains("unins") || lower.Contains("setup") ||
                            lower.Contains("crash") || lower.Contains("vcredist") ||
                            lower.Contains("dxsetup") || lower.Contains("launcher_helper"))
                            continue;
                        if (fi.Length > bestLen) { bestLen = fi.Length; best = fi.FullName; }
                    }
                    catch { }
                }
                return best;
            }
            catch { return null; }
        }

        private static string ExtractVdfPath(string line)
        {
            // 形如：  "path"		"D:\\SteamLibrary"
            int q1 = line.IndexOf('"');
            if (q1 < 0) return null;
            int q2 = line.IndexOf('"', q1 + 1);
            if (q2 < 0) return null;
            string key = line.Substring(q1 + 1, q2 - q1 - 1);
            if (!string.Equals(key, "path", StringComparison.OrdinalIgnoreCase)) return null;
            int q3 = line.IndexOf('"', q2 + 1);
            if (q3 < 0) return null;
            int q4 = line.IndexOf('"', q3 + 1);
            if (q4 < 0) return null;
            return line.Substring(q3 + 1, q4 - q3 - 1);
        }

        private static string ExtractJsonString(string json, string key)
        {
            string pat = "\"" + key + "\"";
            int i = json.IndexOf(pat, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return null;
            i = json.IndexOf(':', i);
            if (i < 0) return null;
            int q1 = json.IndexOf('"', i);
            if (q1 < 0) return null;
            int q2 = json.IndexOf('"', q1 + 1);
            if (q2 < 0) return null;
            return json.Substring(q1 + 1, q2 - q1 - 1);
        }
    }
}
