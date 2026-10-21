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

        /// <summary>生成 dlss.state 的 payload（本地资源实况）。</summary>
        public static string StateJson()
        {
            StringBuilder sb = new StringBuilder(700);
            sb.Append("{\"games\":[],\"deployed\":[],\"preflightOk\":");
            bool anyReady = false;
            sb.Append("true,\"pack\":[");

            try
            {
                List<PackFile> files = Files();
                string dir = DataDir;
                for (int i = 0; i < files.Count; i++)
                {
                    PackFile f = files[i];
                    long len = 0;
                    try
                    {
                        string p = Path.Combine(dir, f.Name);
                        if (File.Exists(p)) len = new FileInfo(p).Length;
                    }
                    catch { }
                    if (len > 0) anyReady = true;

                    if (i > 0) sb.Append(',');
                    sb.Append("{\"name\":\"").Append(f.Name)
                      .Append("\",\"label\":\"").Append(f.Label)
                      .Append("\",\"installed\":").Append(len > 0 ? "true" : "false")
                      .Append(",\"sizeBytes\":").Append(len).Append('}');
                }
            }
            catch { }

            sb.Append("],\"packReady\":").Append(anyReady ? "true" : "false");
            sb.Append("}");
            return sb.ToString();
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
