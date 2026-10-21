using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace CMP90HXToolbox
{
    /// <summary>
    /// DLSS / 帧生成 资源包：**运行时从 GitHub 拉取，不打进 exe**。
    ///
    /// 设计取舍：
    /// · 三个 DLL 加起来 110MB 左右，塞进 exe 会让安装包膨胀一倍以上，
    ///   而且换版本就得重发整个软件。改成按需下载，用户只下自己需要的。
    /// · 存放位置固定在 ProgramData 数据目录，卸载软件不影响、重装可复用。
    /// · 下载源做成可配置（国内直连 raw.githubusercontent 常常很慢），
    ///   默认官方，可在 settings.ini 里换成镜像前缀。
    /// </summary>
    internal static class DlssPack
    {
        /// <summary>一个资源文件的定义。</summary>
        public sealed class PackFile
        {
            public string Name;        // 落盘文件名
            public string Url;         // 下载地址
            public string Label;       // 界面显示用
            public long ExpectSize;    // 期望字节数，用来判断是否需要重下

            public PackFile(string name, string url, string label, long expectSize)
            {
                Name = name; Url = url; Label = label; ExpectSize = expectSize;
            }
        }

        private const string OfficialBase =
            "https://raw.githubusercontent.com/NVIDIA/DLSS/main/lib/Windows_x86_64/rel/";

        public static string DataDir
        {
            get
            {
                try
                {
                    string d = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "CMP90HX_Toolbox", "dlss");
                    if (!Directory.Exists(d)) Directory.CreateDirectory(d);
                    return d;
                }
                catch { return null; }
            }
        }

        /// <summary>资源清单。基址可在 settings.ini 用 dlssBaseUrl 覆盖（换镜像）。</summary>
        public static List<PackFile> Files()
        {
            string baseUrl = OfficialBase;
            try
            {
                AppSettings.Load();
                if (!string.IsNullOrEmpty(AppSettings.DlssBaseUrl))
                    baseUrl = AppSettings.DlssBaseUrl;
            }
            catch { }

            List<PackFile> list = new List<PackFile>();
            list.Add(new PackFile("nvngx_dlss.dll", baseUrl + "nvngx_dlss.dll", "DLSS 超分辨率", 58956912));
            list.Add(new PackFile("nvngx_dlssg.dll", baseUrl + "nvngx_dlssg.dll", "DLSS 帧生成", 7506544));
            list.Add(new PackFile("nvngx_dlssd.dll", baseUrl + "nvngx_dlssd.dll", "DLSS 光追重建", 48343664));
            return list;
        }

        /// <summary>本地是否已有该文件（存在且大小大于 0）。</summary>
        public static bool Exists(string name)
        {
            try
            {
                string dir = DataDir;
                if (string.IsNullOrEmpty(dir)) return false;
                string p = Path.Combine(dir, name);
                return File.Exists(p) && new FileInfo(p).Length > 0;
            }
            catch { return false; }
        }

        /// <summary>
        /// 检查并补齐资源：缺失或大小不符的才下载，已有且一致的跳过。
        /// 返回每个文件的处理结果文字，供日志与界面显示。
        /// </summary>
        public static List<string> CheckAndDownload(Action<string, bool> log)
        {
            List<string> report = new List<string>();
            try
            {
                // .NET Framework 默认可能是 TLS1.0，GitHub 只收 1.2+
                try
                {
                    ServicePointManager.SecurityProtocol =
                        SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;
                }
                catch { }

                string dir = DataDir;
                if (string.IsNullOrEmpty(dir))
                {
                    report.Add("数据目录不可用");
                    return report;
                }

                List<PackFile> files = Files();
                for (int i = 0; i < files.Count; i++)
                {
                    PackFile f = files[i];
                    string target = Path.Combine(dir, f.Name);

                    long localLen = 0;
                    try { if (File.Exists(target)) localLen = new FileInfo(target).Length; } catch { }

                    if (localLen > 0 && (f.ExpectSize <= 0 || localLen == f.ExpectSize))
                    {
                        report.Add(f.Label + " 已是当前版本（" + Mb(localLen) + "）");
                        continue;
                    }

                    if (log != null) log("正在下载 " + f.Label + "（" + Mb(f.ExpectSize) + "）…", false);
                    bool ok = Download(f.Url, target, log);
                    if (ok)
                    {
                        long now = 0;
                        try { now = new FileInfo(target).Length; } catch { }
                        report.Add(f.Label + " 下载完成（" + Mb(now) + "）");
                    }
                    else
                    {
                        report.Add(f.Label + " 下载失败，可稍后重试或改用镜像");
                    }
                }
            }
            catch (Exception ex)
            {
                report.Add("资源检查异常: " + ex.Message);
            }
            return report;
        }

        /// <summary>把数据目录里的资源复制到游戏目录；目标已存在则先备份为 .hxbak。</summary>
        public static List<string> Deploy(string gameDir, Action<string, bool> log)
        {
            List<string> report = new List<string>();
            try
            {
                if (string.IsNullOrEmpty(gameDir) || !Directory.Exists(gameDir))
                {
                    report.Add("游戏目录不存在: " + gameDir);
                    return report;
                }
                string dir = DataDir;
                if (string.IsNullOrEmpty(dir))
                {
                    report.Add("数据目录不可用");
                    return report;
                }

                List<PackFile> files = Files();
                int deployed = 0;
                for (int i = 0; i < files.Count; i++)
                {
                    PackFile f = files[i];
                    string src = Path.Combine(dir, f.Name);
                    if (!File.Exists(src)) continue;   // 没下载的跳过，不报错

                    string dst = Path.Combine(gameDir, f.Name);
                    try
                    {
                        if (File.Exists(dst))
                        {
                            string bak = dst + ".hxbak";
                            if (!File.Exists(bak)) File.Copy(dst, bak, false);
                        }
                        File.Copy(src, dst, true);
                        deployed++;
                        if (log != null) log("已部署 " + f.Label + " → " + gameDir, false);
                    }
                    catch (Exception ex)
                    {
                        report.Add(f.Label + " 部署失败: " + ex.Message);
                    }
                }
                report.Insert(0, "共部署 " + deployed + " 个文件");
            }
            catch (Exception ex)
            {
                report.Add("部署异常: " + ex.Message);
            }
            return report;
        }

        /// <summary>
        /// 生成 dlss.state 的 payload。
        /// 下载与部署已全部复用 NativeCore 的老实现（ShyVortex/dlss-unlocked），
        /// 这里只负责把「本地包在不在、是否完整」如实报给界面。
        /// </summary>
        public static string StateJson()
        {
            bool downloaded = false;
            string tag = "";
            string validation = "未下载";
            int fileCount = 0;
            long totalBytes = 0;

            try
            {
                string dir = NativeCore.GetDlssUnlockedDirectory();
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                {
                    string[] zips = Directory.GetFiles(dir, "dlss-unlocked-standalone-*.zip");
                    if (zips.Length > 0)
                    {
                        string zip = zips[0];
                        for (int i = 1; i < zips.Length; i++)
                            if (File.GetLastWriteTimeUtc(zips[i]) > File.GetLastWriteTimeUtc(zip)) zip = zips[i];
                        downloaded = true;
                        totalBytes = new FileInfo(zip).Length;
                        tag = Path.GetFileName(zip)
                                .Replace("dlss-unlocked-standalone-", "").Replace(".zip", "");
                        validation = NativeCore.ValidateDlssUnlockedPackage(null);
                    }
                }
            }
            catch { }

            StringBuilder sb = new StringBuilder(500);
            sb.Append("{\"games\":[],\"deployed\":[],\"preflightOk\":true,");
            sb.Append("\"downloaded\":").Append(downloaded ? "true" : "false").Append(',');
            sb.Append("\"tag\":\"").Append(tag).Append("\",");
            sb.Append("\"validation\":\"").Append(validation).Append("\",");
            sb.Append("\"totalBytes\":").Append(totalBytes).Append(',');
            sb.Append("\"fileCount\":").Append(fileCount).Append(',');
            sb.Append("\"packReady\":").Append(downloaded ? "true" : "false");
            sb.Append(",\"mirrors\":").Append(HardwareBridge.DownloadSourcesJson());
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// 通过 GitHub API 解析最新 release 的 standalone 资产真实地址与文件名。
        ///
        /// 之前靠拼文件名（dlss-unlocked-standalone-<tag>.zip）去下，实测 404——
        /// 上游资产命名并不固定。改为查 API 拿 browser_download_url，不再猜。
        /// </summary>
        public static string ResolveLatestAsset(out string tag, out string assetName, Action<string, bool> log)
        {
            tag = null;
            assetName = null;
            try
            {
                try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { }

                // 不用 api.github.com —— 实测该接口对本机返回 403（限流/拦截）。
                // 改为读 /releases/latest 的 302 Location 取 tag，再拼资产名。
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(
                    "https://github.com/ShyVortex/dlss-unlocked/releases/latest");
                req.AllowAutoRedirect = false;
                req.UserAgent = "Mozilla/5.0 HX-Studio";
                req.Timeout = 20000;
                string proxyDesc;
                req.Proxy = ResolveProxy(out proxyDesc);

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    string loc = resp.Headers["Location"];
                    if (!string.IsNullOrEmpty(loc))
                    {
                        int p = loc.LastIndexOf("/tag/", StringComparison.OrdinalIgnoreCase);
                        if (p >= 0) tag = loc.Substring(p + 5).Trim();
                    }
                }

                if (string.IsNullOrEmpty(tag))
                {
                    if (log != null) log("解析最新版本失败（未取到 tag）", true);
                    return null;
                }

                assetName = "dlss-unlocked-standalone-" + tag + ".zip";
                return "https://github.com/ShyVortex/dlss-unlocked/releases/download/"
                       + tag + "/" + assetName;
            }
            catch (Exception ex)
            {
                if (log != null) log("解析最新版本失败: " + ex.Message, true);
                return null;
            }
        }

        /// <summary>
        /// 下载资源包到老版数据目录（dlss_unlocked），文件名统一成
        /// dlss-unlocked-standalone-<tag>.zip，保证老版校验与注入逻辑能识别。
        /// 流式写入以驱动进度回调（WebClient.DownloadFile 不触发进度）。
        /// </summary>
        public static bool DownloadToLegacyDir(string url, string tag, Action<string, bool> log)
        {
            return DownloadToLegacyDir(url, tag, null, log);
        }

        public static bool DownloadToLegacyDir(string url, string tag, string preferred, Action<string, bool> log)
        {
            try
            {
                string dir = NativeCore.GetDlssUnlockedDirectory();
                if (string.IsNullOrEmpty(dir)) return false;
                Directory.CreateDirectory(dir);

                string fileName = "dlss-unlocked-standalone-" + tag + ".zip";
                string dest = Path.Combine(dir, fileName);
                string tmp = dest + ".part";   // 断点续传：保留未完成的 .part

                // 候选源：用户自配前缀（若有）→ 内置几个公共加速 → 直连。
                // 实测不同机器能通的镜像不一样（本机只有 ghproxy.net 与直连通），
                // 所以必须逐个试并记录每个的失败原因，而不是写死一个。
                string[] mirrors = BuildCandidates(preferred);
                bool anyOk = false;
                string lastErr = "";
                for (int ci = 0; ci < mirrors.Length; ci++)
                {
                    string prefix = mirrors[ci];
                    string label = string.IsNullOrEmpty(prefix) ? "直连" : prefix.Replace("https://", "").TrimEnd('/');
                    try
                    {
                        if (log != null) log("尝试 " + label + " …", false);
                        DownloadOnce(prefix + url, tmp, log);
                        anyOk = true;
                        if (log != null) log("下载成功（" + label + "）", false);
                        break;
                    }
                    catch (Exception ex)
                    {
                        lastErr = ex.Message;
                        if (log != null) log(label + " 失败: " + ex.Message, true);
                        try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                    }
                }
                if (!anyOk)
                {
                    long keep = 0;
                    try { if (File.Exists(tmp)) keep = new FileInfo(tmp).Length; } catch { }
                    if (log != null) log("所有下载源均失败，最后一个错误: " + lastErr +
                                         "（已保留 " + (keep / 1048576) + " MB，下次续传）", true);
                    return false;   // 保留 .part，下次从断点继续
                }

                // 镜像常返回 HTML 错误页或半截数据（实测拿到过 3KB 的假 zip）。
                // 落盘前先验真，不合法就当这次失败。
                if (!IsValidZip(tmp))
                {
                    long bad = 0;
                    try { bad = new FileInfo(tmp).Length; } catch { }
                    if (log != null) log("下载到的文件不是有效 zip（" + bad + " 字节），已丢弃", true);
                    try { File.Delete(tmp); } catch { }
                    return false;
                }
                if (!File.Exists(tmp) || new FileInfo(tmp).Length <= 0) return false;

                if (File.Exists(dest)) File.Delete(dest);
                File.Move(tmp, dest);
                return true;
            }
            catch (Exception ex)
            {
                if (log != null) log("下载失败: " + ex.Message, true);
                return false;
            }
        }

        /// <summary>
        /// 构造下载候选前缀列表。
        /// 顺序：用户自配 → 内置公共加速 → 直连（空前缀）。
        /// 内置列表按实测可用性排序，但每台机器网络环境不同，仍逐个尝试。
        /// </summary>
        private static string[] BuildCandidates(string preferred)
        {
            // 用户在 DLSS 页选定了具体源：DIRECT=只直连；其余=该源优先，失败回落直连
            if (!string.IsNullOrEmpty(preferred))
            {
                if (string.Equals(preferred, "DIRECT", StringComparison.OrdinalIgnoreCase))
                    return new string[] { "" };
                return new string[] { preferred.TrimEnd('/') + "/", "" };
            }
            return BuildCandidates();
        }

        private static string[] BuildCandidates()
        {
            System.Collections.Generic.List<string> list = new System.Collections.Generic.List<string>();

            try
            {
                AppSettings.Load();
                if (!string.IsNullOrEmpty(AppSettings.DownloadProxy))
                    list.Add(AppSettings.DownloadProxy.TrimEnd('/') + "/");
            }
            catch { }

            string[] builtin = new string[] {
                "https://ghproxy.net/",
                "https://ghfast.top/",
                "https://gh-proxy.com/",
                "https://hub.gitmirror.com/"
            };
            for (int i = 0; i < builtin.Length; i++)
                if (!list.Contains(builtin[i])) list.Add(builtin[i]);

            list.Add("");   // 直连垫底
            return list.ToArray();
        }

        /// <summary>单通道下载到临时文件，流式读取以驱动进度。</summary>
        private static void DownloadOnce(string url, string tmp, Action<string, bool> log)
        {
            long existing = 0;
            try { if (File.Exists(tmp)) existing = new FileInfo(tmp).Length; } catch { }

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "Mozilla/5.0 HX-Studio";
            req.Timeout = 30000;
            // 15 秒没有任何数据就抛异常：镜像常在传到一半时静默掐断，
            // 原来的 60 秒表现就是「卡住不动」。
            req.ReadWriteTimeout = 15000;
            req.AllowAutoRedirect = true;
            string proxyDesc;
            req.Proxy = ResolveProxy(out proxyDesc);
            if (existing > 0) req.AddRange(existing);      // 断点续传

            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            {
                bool partial = resp.StatusCode == HttpStatusCode.PartialContent;
                long total = resp.ContentLength;

                // 206 时 ContentLength 是剩余部分，总量要从 Content-Range 取
                if (partial)
                {
                    string cr = resp.Headers["Content-Range"];   // bytes 100-999/1000
                    if (!string.IsNullOrEmpty(cr))
                    {
                        int slash = cr.LastIndexOf('/');
                        long t;
                        if (slash > 0 && long.TryParse(cr.Substring(slash + 1), out t)) total = t;
                    }
                }
                else if (existing > 0)
                {
                    // 服务端不支持断点：只能从头来，丢掉已有进度
                    existing = 0;
                    try { File.Delete(tmp); } catch { }
                }

                long read = existing;
                int lastPct = -1;
                byte[] buf = new byte[131072];

                if (existing > 0 && log != null)
                    log("从断点续传（已有 " + (existing / 1048576) + " MB）", false);

                using (Stream ns = resp.GetResponseStream())
                using (FileStream fs = new FileStream(tmp, existing > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write))
                {
                    int n;
                    while ((n = ns.Read(buf, 0, buf.Length)) > 0)
                    {
                        fs.Write(buf, 0, n);
                        read += n;
                        if (total > 0)
                        {
                            int pct = (int)(read * 100 / total);
                            if (pct != lastPct && pct % 5 == 0)
                            {
                                lastPct = pct;
                                if (log != null) log("下载 " + pct + "%（" + (read / 1024 / 1024) + "/" + (total / 1024 / 1024) + " MB）", false);
                            }
                            try { if (OnProgress != null) OnProgress(pct, read, total); } catch { }
                        }
                    }
                }

                // 读到的字节少于总量 = 被中途掐断，报错换源；.part 保留以便续传
                if (total > 0 && read < total)
                    throw new IOException("传输中断（" + (read / 1048576) + "/" + (total / 1048576) + " MB）");
            }
        }

        /// <summary>判断文件是不是能打开的 zip（镜像可能返回 HTML 错误页或半截数据）。</summary>
        /// <summary>
        /// 取当前该用的代理。
        /// system = 跟随系统（.NET 默认行为，读 IE/WinINET 设置）；
        /// manual = 用户填的地址；direct = 完全不走代理。
        /// 实测很关键：系统代理是 TUN 模式时 .NET 未必识别，需要手动指定。
        /// </summary>
        public static IWebProxy ResolveProxy(out string desc)
        {
            desc = "直连";
            try
            {
                AppSettings.Load();
                string mode = (AppSettings.ProxyMode ?? "system").Trim().ToLowerInvariant();

                if (mode == "direct")
                {
                    desc = "直连(已禁用代理)";
                    return null;
                }
                if (mode == "manual" && !string.IsNullOrEmpty(AppSettings.ProxyAddress))
                {
                    desc = "手动 " + AppSettings.ProxyAddress;
                    return new WebProxy(AppSettings.ProxyAddress.Trim());
                }

                IWebProxy sys = WebRequest.DefaultWebProxy;
                if (sys != null)
                {
                    try
                    {
                        Uri probe = new Uri("https://github.com");
                        Uri via = sys.GetProxy(probe);
                        if (via != null && via.Host != probe.Host)
                        {
                            desc = "系统 " + via.Host + ":" + via.Port;
                            return sys;
                        }
                    }
                    catch { }
                }
                desc = "系统(未检测到代理，将直连)";
                return sys;
            }
            catch { return null; }
        }

        /// <summary>下载进度回调：percent(0-100), 已读字节, 总字节。宿主用它向前端推事件。</summary>
        public static Action<int, long, long> OnProgress;

        public static bool IsValidZip(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;
                if (new FileInfo(path).Length < 1024 * 1024) return false;   // 小于 1MB 必是错的
                using (FileStream probe = File.OpenRead(path))
                using (System.IO.Compression.ZipArchive za =
                       new System.IO.Compression.ZipArchive(probe, System.IO.Compression.ZipArchiveMode.Read))
                {
                    return za.Entries.Count > 0;
                }
            }
            catch { return false; }
        }

        private static string ExtractJson(string json, string key)
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

        // ---------- 内部 ----------

        private static bool Download(string url, string target, Action<string, bool> log)
        {
            string tmp = target + ".part";
            try
            {
                if (File.Exists(tmp)) File.Delete(tmp);
                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "HX-Studio");
                    wc.DownloadFile(url, tmp);
                }
                long len = 0;
                try { len = new FileInfo(tmp).Length; } catch { }
                if (len <= 0) { try { File.Delete(tmp); } catch { } return false; }

                if (File.Exists(target)) File.Delete(target);
                File.Move(tmp, target);
                return true;
            }
            catch (Exception ex)
            {
                if (log != null) log("下载失败: " + ex.Message, true);
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                return false;
            }
        }

        private static string Mb(long bytes)
        {
            return (bytes / 1024.0 / 1024.0).ToString("0.0") + " MB";
        }
    }
}
