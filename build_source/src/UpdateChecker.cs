using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace CMP90HXToolbox
{
    /// <summary>
    /// 版本更新检查。
    ///
    /// 更新源地址不写死——存在 settings.ini 的 updateManifestUrl。
    /// 清单支持两种格式，谁方便用谁：
    ///   纯文本：6.3.0
    ///   JSON  ：{"version":"6.3.0","notes":"...","url":"..."}
    ///
    /// 只做「查」，不做下载替换（update.apply 是另一条独立链路，需验签）。
    /// </summary>
    internal static class UpdateChecker
    {
        public static string FetchLatestVersion(string manifestUrl, Action<string, bool> log)
        {
            try
            {
                try
                {
                    ServicePointManager.SecurityProtocol =
                        SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;
                }
                catch { }

                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "HX-Studio");
                    wc.Encoding = Encoding.UTF8;
                    // 清单地址也走全局加速前缀（地址本身不是 GitHub 时不会被加前缀，
                    // 由用户填完整地址即可；这里仅在前缀存在且地址是 https://github.com 时生效）
                    AppSettings.Load();
                    string target = manifestUrl;
                    string proxy = AppSettings.DownloadProxy;
                    if (!string.IsNullOrEmpty(proxy) &&
                        manifestUrl.StartsWith("https://github.com", StringComparison.OrdinalIgnoreCase))
                        target = proxy + manifestUrl;

                    string body = wc.DownloadString(target);
                    if (string.IsNullOrEmpty(body)) return null;
                    body = body.Trim();

                    if (body.StartsWith("{"))
                    {
                        string v = ExtractJson(body, "version");
                        return string.IsNullOrEmpty(v) ? null : v.Trim();
                    }
                    // 纯文本：取第一行
                    int nl = body.IndexOf('\n');
                    string line = (nl > 0 ? body.Substring(0, nl) : body).Trim();
                    return Regex.IsMatch(line, @"^\d+(\.\d+){1,3}$") ? line : null;
                }
            }
            catch (Exception ex)
            {
                if (log != null) log("获取更新信息失败: " + ex.Message, true);
                return null;
            }
        }

        /// <summary>a 是否比 b 新（逐段数字比较，缺位补 0）。</summary>
        public static bool IsNewer(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return false;
            if (string.IsNullOrEmpty(b)) return true;
            string[] pa = a.Trim().TrimStart('v', 'V').Split('.');
            string[] pb = b.Trim().TrimStart('v', 'V').Split('.');
            int n = Math.Max(pa.Length, pb.Length);
            for (int i = 0; i < n; i++)
            {
                int va = i < pa.Length ? ParseInt(pa[i]) : 0;
                int vb = i < pb.Length ? ParseInt(pb[i]) : 0;
                if (va > vb) return true;
                if (va < vb) return false;
            }
            return false;
        }

        private static int ParseInt(string s)
        {
            int v;
            return int.TryParse(s.Trim(), out v) ? v : 0;
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
    }
}
