using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace CMP90HXToolbox
{
    /// <summary>
    /// 版本更新检查。
    ///
    /// 更新源支持两种写法：
    ///   1) GitHub 仓库：https://github.com/{owner}/{repo}（可带 /releases/... 或尾部 /），
    ///      也可以直接写 owner/repo。直接读 api.github.com 的 releases/latest，
    ///      取 tag_name 当版本、assets 里的 .exe/.zip 当安装包，用户不必自己维护 JSON。
    ///      共享出口 IP 会被 API 限流（403）：这时降级为解析 github.com 的
    ///      /releases/latest 跳转与 tag 页 HTML（不消耗 API 配额），
    ///      但页面拿不到 sha256，结果会标 Unverified，由下载阶段改用直链 + PE 校验。
    ///   2) 自建清单：纯文本 6.3.0，或 JSON {"version":..,"url":..,"sha256":..}
    ///
    /// 地址来自 settings.ini 的 updateManifestUrl，留空用 AppSettings.DefaultUpdateRepo。
    /// 只做「查」，不做下载替换（下载在 UpdateInstaller，需 sha256 校验）。
    /// </summary>
    internal static class UpdateChecker
    {
        /// <summary>一次解析后的发布信息（GitHub Release 或自建清单）。</summary>
        public sealed class ReleaseInfo
        {
            public string Version;      // 去掉前导 v，如 5.0.0
            public string PackageUrl;   // 安装包直链（按需已加加速前缀）
            public string Sha256;       // 64 位小写，取不到为 null
            public string Notes;        // release body / 清单 notes
            public string Source;       // "github" / "manifest"

            /// <summary>
            /// true = 走了「release 页面解析」降级链路，页面上拿不到官方 sha256。
            /// 下载阶段据此改用「github.com 直链 + PE 头/体积校验」放行，而不是哈希校验。
            /// </summary>
            public bool Unverified;
        }

        /// <summary>最近一次 Fetch 失败的原因（给上层区分 404 / 403 / 网络异常）。</summary>
        public static string LastError { get; private set; }

        /// <summary>最近一次 HttpGet 的 HTTP 状态码；0 表示没拿到响应（超时 / 断网）。</summary>
        private static int _lastHttpStatus;

        /// <summary>只要版本号的老接口，保留给既有调用点。</summary>
        public static string FetchLatestVersion(string manifestUrl, Action<string, bool> log)
        {
            ReleaseInfo info = Fetch(manifestUrl, log);
            return info == null ? null : info.Version;
        }

        /// <summary>抓取并解析更新源；失败返回 null，原因见 LastError。</summary>
        public static ReleaseInfo Fetch(string manifestUrl, Action<string, bool> log)
        {
            LastError = null;
            string source = manifestUrl == null ? "" : manifestUrl.Trim();
            // 空地址 = 官方仓库：设置留空也能开箱即用
            if (source.Length == 0) source = AppSettings.DefaultUpdateRepo;

            string owner, repo;
            if (TryParseGitHubRepo(source, out owner, out repo))
                return FetchGitHub(owner, repo, log);

            return FetchManifest(source, log);
        }

        /// <summary>
        /// 识别 GitHub 仓库地址。接受：
        ///   https://github.com/owner/repo[/releases/...|/]（http / www 亦可）
        ///   owner/repo（恰好一段斜杠、不含空格）
        /// 命中返回 true 并输出 owner/repo；自建清单地址返回 false。
        /// </summary>
        public static bool TryParseGitHubRepo(string source, out string owner, out string repo)
        {
            owner = null;
            repo = null;
            if (string.IsNullOrEmpty(source)) return false;

            string s = source.Trim();
            if (s.Length == 0) return false;

            string[] heads = new string[]
            {
                "https://github.com/", "http://github.com/",
                "https://www.github.com/", "http://www.github.com/",
                "github.com/", "www.github.com/"
            };
            string rest = null;
            for (int i = 0; i < heads.Length; i++)
            {
                if (s.StartsWith(heads[i], StringComparison.OrdinalIgnoreCase))
                {
                    rest = s.Substring(heads[i].Length);
                    break;
                }
            }

            if (rest == null)
            {
                // 裸 owner/repo：恰好一段斜杠，且不能是带协议的地址
                if (s.IndexOf("://", StringComparison.Ordinal) >= 0) return false;
                if (s.IndexOf(' ') >= 0 || s.IndexOf('\t') >= 0 ||
                    s.IndexOf('\\') >= 0 || s.IndexOf(':') >= 0) return false;
                int slashes = 0;
                for (int i = 0; i < s.Length; i++) if (s[i] == '/') slashes++;
                if (slashes != 1) return false;
                rest = s;
            }

            // 去掉 ?query / #fragment
            int cut = rest.IndexOfAny(new char[] { '?', '#' });
            if (cut >= 0) rest = rest.Substring(0, cut);

            string[] parts = rest.Split('/');
            string a = null, b = null;
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (p.Length == 0) continue;
                if (a == null) a = p;
                else { b = p; break; }
            }
            if (a == null || b == null) return false;
            if (b.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                b = b.Substring(0, b.Length - 4);
            if (!IsRepoName(a) || !IsRepoName(b)) return false;

            owner = a;
            repo = b;
            return true;
        }

        // ==================== GitHub API ====================

        private static ReleaseInfo FetchGitHub(string owner, string repo, Action<string, bool> log)
        {
            // 注意：api.github.com 不能套加速前缀——那些镜像只代理 github.com，
            // 套上去会把接口地址拼成不存在的路径，反而全挂。
            string api = "https://api.github.com/repos/" + owner + "/" + repo + "/releases/latest";
            if (log != null) log("检查更新（GitHub 仓库 " + owner + "/" + repo + "）…", false);

            string body, err;
            if (!HttpGet(api, true, out body, out err))
            {
                // 403 / 429 = 出口 IP 被限流（共享 IP 常见），0 = 超时 / 连不上。
                // 这两种都不代表仓库或 Release 不存在，不能直接判死：
                // github.com 的 release 页面不走 API 配额，降级去解析页面。
                // 404 仍然按「仓库/Release 不存在」直接失败，不做降级。
                int code = _lastHttpStatus;
                if (code == 403 || code == 429 || code == 0)
                {
                    if (log != null)
                        log("GitHub API 不可用（" + (code == 0 ? "网络超时/连接失败" : code.ToString()) +
                            "），已降级为页面解析", true);
                    return FetchGitHubByPage(owner, repo, log);
                }

                LastError = err;
                if (log != null) log(err, true);
                return null;
            }

            string tag = ExtractString(body, "tag_name", 0);
            if (string.IsNullOrEmpty(tag))
            {
                LastError = "GitHub Release 数据缺少 tag_name，无法判断版本";
                if (log != null) log(LastError, true);
                return null;
            }

            AssetPick pick = PickAsset(body);
            if (pick == null)
            {
                LastError = "该 Release 没有任何 .exe / .zip 附件，无法下载";
                if (log != null) log(LastError, true);
                return null;
            }
            if (string.IsNullOrEmpty(pick.Url))
            {
                LastError = "Release 附件 " + pick.Name + " 缺少 browser_download_url";
                if (log != null) log(LastError, true);
                return null;
            }

            ReleaseInfo info = new ReleaseInfo();
            info.Version = tag.Trim().TrimStart('v', 'V');
            info.Notes = ExtractString(body, "body", 0);
            info.PackageUrl = WithProxy(pick.Url);
            info.Sha256 = NormalizeSha256(pick.Digest);
            info.Source = "github";

            if (log != null)
            {
                log("GitHub Release: v" + info.Version + " / 附件 " + pick.Name +
                    (info.Sha256 == null
                        ? "（附件没有 digest 摘要，下载阶段会拒绝）"
                        : "（sha256 " + info.Sha256.Substring(0, 12) + "…）"), false);
            }
            return info;
        }

        // ==================== GitHub 页面降级（API 限流时） ====================

        /// <summary>
        /// API 被限流（403/429）或超时后的降级链路：
        ///   1) GET /releases/latest，读 302 后的 ResponseUri 取 tag —— 不消耗 API 配额；
        ///   2) GET /releases/tag/{tag}（附件在懒加载片段里，见 TryFetchReleaseHtml），
        ///      从 HTML 里抓 /releases/download/ 直链。
        /// 页面没有 sha256，所以结果一律 Unverified=true，交给下载阶段做直连 + PE 校验。
        /// </summary>
        private static ReleaseInfo FetchGitHubByPage(string owner, string repo, Action<string, bool> log)
        {
            string tag, err;
            if (!TryResolveTagByRedirect(owner, repo, out tag, out err))
            {
                LastError = err;
                if (log != null) log(err, true);
                return null;
            }
            if (log != null) log("Release 页面跳转解析到标签: " + tag, false);

            string html;
            if (!TryFetchReleaseHtml(owner, repo, tag, out html, out err))
            {
                LastError = err;
                if (log != null) log(err, true);
                return null;
            }

            AssetPick pick = PickAssetFromHtml(html);
            if (pick == null || string.IsNullOrEmpty(pick.Url))
            {
                LastError = "该 Release 页面上没有找到 .exe 附件";
                if (log != null) log(LastError, true);
                return null;
            }

            ReleaseInfo info = new ReleaseInfo();
            info.Version = Uri.UnescapeDataString(tag).Trim().TrimStart('v', 'V');
            info.Notes = null;          // 页面降级不解析 changelog，宁缺勿错
            // 不套加速前缀：无摘要时只认 github.com 直链（下载阶段还会再校验一次主机）
            info.PackageUrl = pick.Url;
            info.Sha256 = null;
            info.Unverified = true;
            info.Source = "github";

            if (log != null)
                log("Release 页面解析到附件 " + pick.Name +
                    "（页面无 sha256，下载阶段按 github.com 直连 + PE 头校验放行）", true);
            return info;
        }

        /// <summary>GET /releases/latest 并跟随 302，从最终的 /releases/tag/{tag} 路径取 tag。</summary>
        private static bool TryResolveTagByRedirect(string owner, string repo, out string tag, out string error)
        {
            tag = null;
            error = null;
            string url = "https://github.com/" + owner + "/" + repo + "/releases/latest";

            try
            {
                EnsureTls();
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = "Mozilla/5.0 HX-Studio";
                req.Timeout = 20000;
                req.ReadWriteTimeout = 20000;
                req.AllowAutoRedirect = true;
                try
                {
                    string proxyDesc;
                    req.Proxy = DlssPack.ResolveProxy(out proxyDesc);
                }
                catch { }

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    Uri final = resp.ResponseUri;
                    if (final == null)
                    {
                        error = "无法读取 GitHub Release 页面跳转后的地址";
                        return false;
                    }

                    const string mark = "/releases/tag/";
                    string path = final.AbsolutePath;
                    int i = path.IndexOf(mark, StringComparison.OrdinalIgnoreCase);
                    if (i < 0)
                    {
                        error = "GitHub Release 页面没有跳转到 tag 页（" + final.AbsoluteUri + "），无法判断版本";
                        return false;
                    }

                    string seg = path.Substring(i + mark.Length).Trim('/');
                    if (seg.Length == 0)
                    {
                        error = "GitHub Release 页面跳转地址里没有 tag（" + final.AbsoluteUri + "）";
                        return false;
                    }
                    tag = seg;   // 保留 URL 里的原样（已百分号编码），拼页面地址时直接用
                    return true;
                }
            }
            catch (WebException wex)
            {
                HttpWebResponse hr = wex.Response as HttpWebResponse;
                if (hr != null)
                {
                    int code = (int)hr.StatusCode;
                    try { hr.Dispose(); } catch { }
                    error = code == 404
                        ? "仓库或 Release 不存在（404），请检查更新源地址或该仓库是否已发布 Release"
                        : "访问 GitHub Release 页面返回 HTTP " + code;
                    return false;
                }
                error = "访问 GitHub Release 页面失败：" + wex.Message;
                return false;
            }
            catch (Exception ex)
            {
                error = "访问 GitHub Release 页面失败：" + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 取 tag 页 HTML。GitHub 把附件列表懒加载在
        /// /releases/expanded_assets/{tag} 片段里，tag 页本身通常没有下载直链，
        /// 所以主页面匹配不到时再取那个片段。
        /// </summary>
        private static bool TryFetchReleaseHtml(string owner, string repo, string tag,
                                                out string html, out string error)
        {
            html = null;
            error = null;

            string page;
            if (!TryGetText("https://github.com/" + owner + "/" + repo + "/releases/tag/" + tag,
                            out page, out error))
                return false;

            page = page.Replace("&amp;", "&");
            if (PickAssetFromHtml(page) != null)
            {
                html = page;
                return true;
            }

            // 优先用页面自己给出的 include-fragment src（GitHub 改路径时也能跟着走）
            string frag = null;
            Match m = Regex.Match(page,
                "src=\"(https://github\\.com/[^\"]*/releases/expanded_assets/[^\"]+)\"",
                RegexOptions.IgnoreCase);
            if (m.Success) frag = m.Groups[1].Value;
            if (string.IsNullOrEmpty(frag))
                frag = "https://github.com/" + owner + "/" + repo + "/releases/expanded_assets/" + tag;

            string part;
            if (!TryGetText(frag, out part, out error)) return false;
            html = part.Replace("&amp;", "&");
            return true;   // 有没有附件交给调用方统一判断，才能给出统一措辞
        }

        /// <summary>
        /// 从 release 页 HTML 里挑附件直链：优先 .exe，其次 .zip。
        /// 页面里的 href 是相对路径（/owner/repo/releases/download/tag/file.exe），
        /// 统一补成 https://github.com 开头的绝对地址。
        /// </summary>
        private static AssetPick PickAssetFromHtml(string html)
        {
            if (string.IsNullOrEmpty(html)) return null;

            MatchCollection ms = Regex.Matches(html,
                "href=\"(/[^\"]*/releases/download/[^\"]+\\.(exe|zip))\"",
                RegexOptions.IgnoreCase);

            AssetPick exe = null, zip = null;
            for (int i = 0; i < ms.Count; i++)
            {
                string path = ms[i].Groups[1].Value;
                if (string.IsNullOrEmpty(path)) continue;

                AssetPick p = new AssetPick();
                p.Url = "https://github.com" + path;
                int slash = path.LastIndexOf('/');
                p.Name = slash >= 0 ? Uri.UnescapeDataString(path.Substring(slash + 1)) : path;

                string lower = path.ToLowerInvariant();
                if (lower.EndsWith(".exe", StringComparison.Ordinal))
                {
                    if (exe == null) exe = p;
                }
                else if (zip == null) zip = p;
            }
            return exe != null ? exe : zip;
        }

        /// <summary>取一个网页/片段文本，404 也翻译成「仓库或 Release 不存在」。</summary>
        private static bool TryGetText(string url, out string body, out string error)
        {
            body = null;
            error = null;
            try
            {
                EnsureTls();
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = "Mozilla/5.0 HX-Studio";
                req.Accept = "text/html,application/xhtml+xml";
                req.Timeout = 20000;
                req.ReadWriteTimeout = 20000;
                req.AllowAutoRedirect = true;
                try
                {
                    string proxyDesc;
                    req.Proxy = DlssPack.ResolveProxy(out proxyDesc);
                }
                catch { }

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (Stream s = resp.GetResponseStream())
                using (StreamReader reader = new StreamReader(s, Encoding.UTF8, true))
                {
                    body = reader.ReadToEnd();
                }
                return true;
            }
            catch (WebException wex)
            {
                HttpWebResponse hr = wex.Response as HttpWebResponse;
                if (hr != null)
                {
                    int code = (int)hr.StatusCode;
                    try { hr.Dispose(); } catch { }
                    error = code == 404
                        ? "仓库或 Release 不存在（404），请检查更新源地址或该仓库是否已发布 Release"
                        : "访问 GitHub 页面返回 HTTP " + code;
                    return false;
                }
                error = "访问 GitHub 页面失败：" + wex.Message;
                return false;
            }
            catch (Exception ex)
            {
                error = "访问 GitHub 页面失败：" + ex.Message;
                return false;
            }
        }

        private static void EnsureTls()
        {
            try
            {
                ServicePointManager.SecurityProtocol =
                    SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;
            }
            catch { }
        }

        /// <summary>GitHub 附件的 digest 形如 sha256:xxxx，去掉前缀并校验 64 位十六进制。</summary>
        private static string NormalizeSha256(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            s = s.Trim();
            if (s.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(7).Trim();
            if (s.Length != 64) return null;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex) return null;
            }
            return s.ToLowerInvariant();
        }

        private sealed class AssetPick
        {
            public string Name;
            public string Url;
            public string Digest;
        }

        /// <summary>
        /// 从 release JSON 里挑安装包：优先 .exe，其次 .zip，取第一个命中的。
        /// 只用字符串扫描，不引第三方 JSON 库。
        /// </summary>
        private static AssetPick PickAsset(string json)
        {
            int arr = FindValueStart(json, "assets", 0);
            if (arr < 0 || arr >= json.Length || json[arr] != '[') return null;
            int end = FindBracketEnd(json, arr, '[', ']');
            if (end < 0) return null;

            AssetPick exe = null, zip = null;
            int i = arr + 1;
            while (i < end)
            {
                if (json[i] == '{')
                {
                    int objEnd = FindBracketEnd(json, i, '{', '}');
                    if (objEnd < 0 || objEnd > end) break;
                    string obj = json.Substring(i + 1, objEnd - i - 1);
                    string name = ExtractString(obj, "name", 0);
                    if (!string.IsNullOrEmpty(name))
                    {
                        string lower = name.ToLowerInvariant();
                        if (lower.EndsWith(".exe", StringComparison.Ordinal))
                        {
                            if (exe == null) exe = MakePick(obj, name);
                        }
                        else if (lower.EndsWith(".zip", StringComparison.Ordinal))
                        {
                            if (zip == null) zip = MakePick(obj, name);
                        }
                    }
                    i = objEnd + 1;
                }
                else i++;
            }
            return exe != null ? exe : zip;
        }

        private static AssetPick MakePick(string obj, string name)
        {
            AssetPick p = new AssetPick();
            p.Name = name;
            p.Url = ExtractString(obj, "browser_download_url", 0);
            p.Digest = ExtractString(obj, "digest", 0);
            return p;
        }

        /// <summary>下载直链按全局加速前缀拼接（只对 github.com 生效，规则与 UpdateInstaller 一致）。</summary>
        private static string WithProxy(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;
            if (!url.StartsWith("https://github.com", StringComparison.OrdinalIgnoreCase)) return url;

            string prefix = null;
            try
            {
                AppSettings.Load();
                prefix = AppSettings.DownloadProxy;
            }
            catch { }
            if (string.IsNullOrEmpty(prefix)) return url;
            prefix = prefix.Trim();
            if (prefix.Length == 0) return url;

            return prefix.TrimEnd('/') + "/" + url;
        }

        // ==================== 自建清单 ====================

        private static ReleaseInfo FetchManifest(string url, Action<string, bool> log)
        {
            string body, err;
            if (!HttpGet(url, false, out body, out err))
            {
                LastError = err;
                if (log != null) log(err, true);
                return null;
            }
            if (string.IsNullOrEmpty(body))
            {
                LastError = "更新清单内容为空";
                if (log != null) log(LastError, true);
                return null;
            }
            body = body.Trim();
            // 记事本存出来的清单可能带 BOM，不剔掉会把 JSON 当成纯文本解析
            if (body.Length > 0 && body[0] == '\uFEFF') body = body.Substring(1).Trim();
            if (body.Length == 0)
            {
                LastError = "更新清单内容为空";
                if (log != null) log(LastError, true);
                return null;
            }

            ReleaseInfo info = new ReleaseInfo();
            info.Source = "manifest";

            if (body.StartsWith("{"))
            {
                info.Version = Clean(ExtractString(body, "version", 0));
                info.PackageUrl = Clean(ExtractString(body, "url", 0));
                info.Sha256 = NormalizeSha256(ExtractString(body, "sha256", 0));
                info.Notes = ExtractString(body, "notes", 0);
                if (string.IsNullOrEmpty(info.Version))
                {
                    LastError = "更新清单 JSON 缺少 version 字段";
                    if (log != null) log(LastError, true);
                    return null;
                }
                return info;
            }

            // 纯文本：取第一行
            int nl = body.IndexOf('\n');
            string line = (nl > 0 ? body.Substring(0, nl) : body).Trim();
            if (!Regex.IsMatch(line, @"^[vV]?\d+(\.\d+){1,3}$"))
            {
                LastError = "更新清单无法解析（既不是版本号，也不是合法 JSON）";
                if (log != null) log(LastError, true);
                return null;
            }
            info.Version = line.TrimStart('v', 'V');
            return info;
        }

        // ==================== HTTP ====================

        /// <summary>
        /// GET 一个文本资源。isApi=true 时按 GitHub API 带 Accept 头，
        /// 并把 404 / 403 翻译成用户看得懂的原因。
        /// </summary>
        private static bool HttpGet(string url, bool isApi, out string body, out string error)
        {
            body = null;
            error = null;
            _lastHttpStatus = 0;
            string what = isApi ? "GitHub 接口" : "更新清单";

            try
            {
                // .NET Framework 默认可能协商到 TLS1.0，GitHub 只收 1.2+
                try
                {
                    ServicePointManager.SecurityProtocol =
                        SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;
                }
                catch { }

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = "HX-Studio";
                if (isApi) req.Accept = "application/vnd.github+json";
                req.Timeout = 20000;
                req.ReadWriteTimeout = 20000;
                req.AllowAutoRedirect = true;
                try
                {
                    string proxyDesc;
                    req.Proxy = DlssPack.ResolveProxy(out proxyDesc);
                }
                catch { }

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (Stream s = resp.GetResponseStream())
                using (StreamReader reader = new StreamReader(s, Encoding.UTF8, true))
                {
                    body = reader.ReadToEnd();
                }
                return true;
            }
            catch (WebException wex)
            {
                HttpWebResponse hr = wex.Response as HttpWebResponse;
                if (hr != null)
                {
                    int code = (int)hr.StatusCode;
                    _lastHttpStatus = code;
                    try { hr.Dispose(); } catch { }

                    if (code == 404)
                        error = isApi
                            ? "仓库或 Release 不存在（404），请检查更新源地址或该仓库是否已发布 Release"
                            : "更新清单不存在（404），请检查地址是否可访问";
                    else if (code == 403)
                        error = isApi
                            ? "GitHub 接口限流，请稍后再试或配置加速前缀（403）"
                            : "更新清单拒绝访问（403），请检查地址或稍后再试";
                    else if (code == 401)
                        error = "GitHub 拒绝访问（401），该仓库可能需要登录";
                    else
                        error = what + "返回 HTTP " + code;
                    return false;
                }

                error = "访问" + what + "失败：" + wex.Message;
                return false;
            }
            catch (Exception ex)
            {
                error = "访问" + what + "失败：" + ex.Message;
                return false;
            }
        }

        // ==================== 轻量 JSON 扫描 ====================

        /// <summary>找到 "key" 之后第一个值的起始下标（跳过空白与冒号）。</summary>
        private static int FindValueStart(string json, string key, int from)
        {
            string pat = "\"" + key + "\"";
            int i = json.IndexOf(pat, from, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return -1;
            int colon = json.IndexOf(':', i + pat.Length);
            if (colon < 0) return -1;
            int p = colon + 1;
            while (p < json.Length && char.IsWhiteSpace(json[p])) p++;
            return p;
        }

        /// <summary>取字符串字段（自动反转义），没有/不是字符串返回 null。</summary>
        private static string ExtractString(string json, string key, int from)
        {
            int i = FindValueStart(json, key, from);
            if (i < 0 || i >= json.Length || json[i] != '"') return null;

            StringBuilder sb = new StringBuilder();
            int p = i + 1;
            for (; p < json.Length; p++)
            {
                char c = json[p];
                if (c == '\\' && p + 1 < json.Length)
                {
                    sb.Append(c);
                    sb.Append(json[++p]);
                    continue;
                }
                if (c == '"') return Unescape(sb.ToString());
                sb.Append(c);
            }
            return null;
        }

        private static string Unescape(string s)
        {
            if (s.IndexOf('\\') < 0) return s;

            StringBuilder sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c != '\\' || i + 1 >= s.Length)
                {
                    sb.Append(c);
                    continue;
                }
                char e = s[++i];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'u':
                        int code;
                        if (i + 4 < s.Length &&
                            int.TryParse(s.Substring(i + 1, 4), NumberStyles.HexNumber,
                                         CultureInfo.InvariantCulture, out code))
                        {
                            sb.Append((char)code);
                            i += 4;
                        }
                        else sb.Append('u');
                        break;
                    default: sb.Append(e); break;
                }
            }
            return sb.ToString();
        }

        /// <summary>从 start 处的开括号找到配对的闭括号，字符串里的括号不算数。</summary>
        private static int FindBracketEnd(string s, int start, char open, char close)
        {
            int depth = 0;
            bool inStr = false;
            for (int i = start; i < s.Length; i++)
            {
                char c = s[i];
                if (inStr)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == '"') inStr = false;
                    continue;
                }
                if (c == '"') { inStr = true; continue; }
                if (c == open) depth++;
                else if (c == close)
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        private static bool IsRepoName(string s)
        {
            return !string.IsNullOrEmpty(s) && Regex.IsMatch(s, @"^[A-Za-z0-9_.-]+$");
        }

        private static string Clean(string s)
        {
            return s == null ? null : s.Trim();
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
    }
}
