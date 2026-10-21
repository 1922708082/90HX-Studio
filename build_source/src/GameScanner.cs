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

        /// <summary>扫描 Steam 库（含所有附加库目录）与常见启动器目录。</summary>
        public static List<GameEntry> Scan()
        {
            List<GameEntry> list = new List<GameEntry>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

            list.Sort(delegate(GameEntry a, GameEntry b)
            {
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            return list;
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
