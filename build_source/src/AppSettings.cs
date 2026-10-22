using System;
using System.IO;
using System.Text;

namespace CMP90HXToolbox
{
    /// <summary>
    /// 本地设置持久化。
    ///
    /// 之前所有状态都只在内存里，关掉软件再打开就回到默认——
    /// 用户选了哪一档、空闲阈值几秒、白天/夜间主题，全部丢失。
    ///
    /// 存成极简 key=value 文本，落在 ProgramData 下（与日志同目录），
    /// 不引第三方依赖，也不写入注册表。
    /// </summary>
    internal static class AppSettings
    {
        private static readonly object _gate = new object();
        private static bool _loaded;

        public static string Preset = "none";
        public static int IdleSeconds = 8;
        public static string Theme = "light";
        /// <summary>DLSS 资源包下载基址（留空用官方 raw，可换国内镜像）。</summary>
        public static string DlssBaseUrl = "";
        /// <summary>更新清单地址（留空则只提示当前版本，不联网）。</summary>
        public static string UpdateManifestUrl = "";
        /// <summary>全局加速前缀（作用于 GitHub 下载），留空则直连。</summary>
        public static string DownloadProxy = "";
        /// <summary>代理模式：system=跟随系统 / manual=手动地址 / direct=不使用代理</summary>
        public static string ProxyMode = "system";
        /// <summary>手动代理地址，如 http://127.0.0.1:7890</summary>
        public static string ProxyAddress = "";

        private static string Dir
        {
            get
            {
                try
                {
                    string d = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "CMP90HX_Toolbox");
                    return d;
                }
                catch { return null; }
            }
        }

        private static string FilePath
        {
            get
            {
                string d = Dir;
                return string.IsNullOrEmpty(d) ? null : Path.Combine(d, "settings.ini");
            }
        }

        public static void Load()
        {
            lock (_gate)
            {
                if (_loaded) return;
                _loaded = true;
                try
                {
                    string p = FilePath;
                    if (string.IsNullOrEmpty(p) || !File.Exists(p)) return;

                    string[] lines = File.ReadAllLines(p, Encoding.UTF8);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i].Trim();
                        if (line.Length == 0 || line[0] == '#') continue;
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        string k = line.Substring(0, eq).Trim();
                        string v = line.Substring(eq + 1).Trim();

                        if (string.Equals(k, "preset", StringComparison.OrdinalIgnoreCase)) Preset = v;
                        else if (string.Equals(k, "idleSeconds", StringComparison.OrdinalIgnoreCase))
                        {
                            int n;
                            if (int.TryParse(v, out n)) IdleSeconds = n < 1 ? 1 : n;
                        }
                        else if (string.Equals(k, "theme", StringComparison.OrdinalIgnoreCase)) Theme = v;
                        else if (string.Equals(k, "dlssBaseUrl", StringComparison.OrdinalIgnoreCase)) DlssBaseUrl = v;
                        else if (string.Equals(k, "updateManifestUrl", StringComparison.OrdinalIgnoreCase)) UpdateManifestUrl = v;
                        else if (string.Equals(k, "downloadProxy", StringComparison.OrdinalIgnoreCase)) DownloadProxy = v;
                        else if (string.Equals(k, "proxyMode", StringComparison.OrdinalIgnoreCase)) ProxyMode = v;
                        else if (string.Equals(k, "proxyAddress", StringComparison.OrdinalIgnoreCase)) ProxyAddress = v;
                    }
                }
                catch { }
            }
        }

        /// <summary>最近一次写设置失败的原因（给界面展示，不要静默吞掉）。</summary>
        public static string LastError { get; private set; }

        /// <summary>
        /// 写入设置，返回是否成功。
        /// 原来是 void + catch{}：磁盘只读、ACL 拒绝时静默失败，
        /// 调用方照样回「设置已保存」，重启后全回默认。
        /// </summary>
        public static bool Save()
        {
            lock (_gate)
            {
                try
                {
                    string p = FilePath;
                    if (string.IsNullOrEmpty(p)) { LastError = "设置文件路径不可用"; return false; }
                    string d = Dir;
                    if (!string.IsNullOrEmpty(d) && !Directory.Exists(d)) Directory.CreateDirectory(d);

                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("# HX Studio 用户设置（程序自动维护）");
                    sb.AppendLine("preset=" + (Preset ?? "none"));
                    sb.AppendLine("idleSeconds=" + IdleSeconds);
                    sb.AppendLine("theme=" + (Theme ?? "light"));
                    sb.AppendLine("dlssBaseUrl=" + (DlssBaseUrl ?? ""));
                    sb.AppendLine("updateManifestUrl=" + (UpdateManifestUrl ?? ""));
                    sb.AppendLine("downloadProxy=" + (DownloadProxy ?? ""));
                    sb.AppendLine("proxyMode=" + (ProxyMode ?? "system"));
                    sb.AppendLine("proxyAddress=" + (ProxyAddress ?? ""));
                    File.WriteAllText(p, sb.ToString(), new UTF8Encoding(false));
                    LastError = null;
                    return true;
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    return false;
                }
            }
        }

        public static void SetPreset(string preset, int idleSeconds)
        {
            Load();
            Preset = string.IsNullOrEmpty(preset) ? "none" : preset;
            if (idleSeconds > 0) IdleSeconds = idleSeconds;
            Save();
        }

        public static void SetTheme(string theme)
        {
            Load();
            Theme = string.IsNullOrEmpty(theme) ? "light" : theme;
            Save();
        }
    }
}
