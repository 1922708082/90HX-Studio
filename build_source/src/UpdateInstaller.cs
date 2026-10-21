using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CMP90HXToolbox
{
    /// <summary>
    /// 软件自更新：下载安装包 → 校验 SHA-256 → 生成替换脚本并重启。
    ///
    /// 为什么覆盖 exe 要交给外部 .cmd 而不是 C# 自己干：
    /// 正在运行的 exe 被系统以「映射映像」方式锁着，任何进程都写不进去。
    /// 只能起一个独立脚本，等主进程退出后由它动手；主程序只负责退出。
    ///
    /// 两条硬规矩，别为了「先跑通」改掉：
    /// 1. 不关闭 TLS 证书校验（不要碰 ServicePointManager.ServerCertificateValidationCallback）；
    /// 2. 不跳过哈希校验。清单没给合法 sha256、或算出来对不上，
    ///    就地删除文件并失败——宁可让用户重下，也不能把来路不明的 exe 换到主程序位置。
    /// </summary>
    internal static class UpdateInstaller
    {
        /// <summary>一次下载/应用的执行结果，WebShell 直接转成前端 res，不在此处拼 JSON。</summary>
        /// <summary>
        /// 是否已有通过 SHA-256 校验、等待应用的安装包。
        /// 状态事件据此点亮「应用更新」按钮——否则 StubStates 每次刷新
        /// 都会重推 verified:false，把刚下载好的状态打回禁用。
        /// </summary>
        public static bool HasVerifiedPackage
        {
            get
            {
                try { return !string.IsNullOrEmpty(_pendingPackage) && File.Exists(_pendingPackage); }
                catch { return false; }
            }
        }

        /// <summary>待应用安装包的文件名，供界面展示。</summary>
        public static string VerifiedPackageName
        {
            get
            {
                try { return HasVerifiedPackage ? Path.GetFileName(_pendingPackage) : ""; }
                catch { return ""; }
            }
        }

        public sealed class UpdateResult
        {
            public bool Ok;
            public string Code;         // 失败错误码，与前端 ERROR_CODE_LABELS 对齐
            public string Message;      // 中文说明，直接展示给用户
            public string Version;      // 清单里的版本号
            public string PackagePath;  // 已通过校验的安装包绝对路径
            public string AssetName;    // 安装包文件名（界面展示）
            public string Sha256;       // 规整成小写的期望哈希
        }

        // 已校验的包只记在内存：程序重启后这个文件可能已被删掉或替换，
        // 与其让它变成「下次开机还能点应用」的暗坑，不如要求重新下载一次。
        private static string _pendingPackage;
        private static string _pendingVersion;
        private static string _pendingSha;

        /// <summary>更新包目录：ProgramData\CMP90HX_Toolbox\update。</summary>
        public static string UpdateDir
        {
            get
            {
                try
                {
                    string root = NativeCore.GetDataDirectory();
                    if (string.IsNullOrEmpty(root)) return null;
                    string d = Path.Combine(root, "update");
                    if (!Directory.Exists(d)) Directory.CreateDirectory(d);
                    return d;
                }
                catch (Exception ex)
                {
                    LogLine("获取更新目录失败: " + ex.Message, true);
                    return null;
                }
            }
        }

        // ==================== 下载 ====================

        /// <summary>
        /// 下载更新包。progress 回传 (percent, 已读字节, 总字节)，
        /// percent 为 -1 表示服务端没给 Content-Length（前端显示为不确定进度）。
        /// </summary>
        public static UpdateResult Download(Action<string, bool> log, Action<int, long, long> progress)
        {
            UpdateResult r = new UpdateResult();
            // 每次下载都作废上一次的结果，避免「新包下载失败，旧包被当成新包应用」
            _pendingPackage = null;
            _pendingVersion = null;
            _pendingSha = null;

            try
            {
                AppSettings.Load();
                string manifestUrl = AppSettings.UpdateManifestUrl == null
                    ? "" : AppSettings.UpdateManifestUrl.Trim();
                if (manifestUrl.Length == 0)
                    return Fail(r, "NETWORK", "未配置更新源，请先在「下载源设置」里填写更新清单地址");

                string fetchErr;
                ManifestInfo mi = FetchManifest(manifestUrl, log, out fetchErr);
                if (mi == null)
                    return Fail(r, "NETWORK", string.IsNullOrEmpty(fetchErr) ? "无法获取更新信息" : fetchErr);

                r.Version = mi.Version;
                if (string.IsNullOrEmpty(mi.Version))
                    return Fail(r, "NETWORK", "更新清单无法解析（既不是版本号，也不是合法 JSON）");

                // 纯文本清单只有版本号：检查更新够用，但没法定位安装包，
                // 这时必须说清楚缺什么，而不是含糊地报「下载失败」。
                if (string.IsNullOrEmpty(mi.PackageUrl))
                    return Fail(r, "NETWORK", "更新清单缺少 url 字段，无法定位安装包");
                if (!IsHttpUrl(mi.PackageUrl))
                    return Fail(r, "NETWORK", "更新清单里的 url 必须是完整的 http:// 或 https:// 地址");

                string expectSha = NormalizeSha256(mi.Sha256);
                if (expectSha == null)
                    return Fail(r, "CHECKSUM_MISMATCH",
                        "更新清单缺少合法的 sha256（需 64 位十六进制），为防止装到被篡改的包已拒绝下载");

                string cur = HardwareBridge.CurrentVersion();
                if (log != null)
                {
                    log("更新清单: 当前 v" + cur + " / 最新 v" + mi.Version +
                        (UpdateChecker.IsNewer(mi.Version, cur) ? "（有新版本）" : "（不比当前新，仍按清单下载）"), false);
                }

                string dir = UpdateDir;
                if (string.IsNullOrEmpty(dir))
                    return Fail(r, "INTERNAL", "更新目录不可用，请检查 ProgramData 写入权限");

                string dest = Path.Combine(dir, SafeFileName(mi.PackageUrl, mi.Version));
                string part = dest + ".part";
                try { if (File.Exists(part)) File.Delete(part); }
                catch (Exception ex) { if (log != null) log("清理上次残留失败（继续）: " + ex.Message, true); }

                // 候选源：有加速前缀且是 GitHub 地址时先走前缀，失败回落直连。
                // 两条路都只认「传输成功」，内容是否可信统一交给后面的 SHA-256 判定。
                string[] urls = Candidates(mi.PackageUrl);
                string lastErr = null;
                long gotBytes = 0;
                bool transferred = false;
                for (int i = 0; i < urls.Length; i++)
                {
                    try
                    {
                        if (log != null)
                            log("开始下载更新包（" + (i == 0 && urls.Length > 1 ? "加速前缀" : "直连") + "）: " + urls[i], false);
                        DownloadOnce(urls[i], part, progress, out gotBytes);
                        transferred = true;
                        break;
                    }
                    catch (Exception ex)
                    {
                        lastErr = ex.Message;
                        if (log != null) log("下载通道失败: " + ex.Message, true);
                        try { if (File.Exists(part)) File.Delete(part); } catch { }
                    }
                }
                if (!transferred)
                    return Fail(r, "NETWORK", "更新包下载失败" + (string.IsNullOrEmpty(lastErr) ? "" : "：" + lastErr));

                // 校验：这是唯一决定「能不能装」的地方，失败必须删文件
                string actual = Sha256Hex(part, log);
                if (actual == null || !string.Equals(actual, expectSha, StringComparison.OrdinalIgnoreCase))
                {
                    long badLen = 0;
                    try { if (File.Exists(part)) badLen = new FileInfo(part).Length; } catch { }
                    try { if (File.Exists(part)) File.Delete(part); } catch { }
                    if (log != null)
                        log("SHA-256 不匹配，已删除该文件（期望 " + expectSha + " / 实际 " +
                            (actual == null ? "无法读取" : actual) + "，" + badLen + " 字节）", true);
                    return Fail(r, "CHECKSUM_MISMATCH", "SHA-256 校验不通过，安装包已删除，拒绝安装");
                }

                try
                {
                    if (File.Exists(dest)) File.Delete(dest);
                    File.Move(part, dest);
                }
                catch (Exception ex)
                {
                    return Fail(r, "FILE_LOCKED", "保存安装包失败：" + ex.Message);
                }

                _pendingPackage = dest;
                _pendingVersion = mi.Version;
                _pendingSha = expectSha;

                r.Ok = true;
                r.PackagePath = dest;
                r.AssetName = Path.GetFileName(dest);
                r.Sha256 = expectSha;
                r.Message = "更新包下载完成，SHA-256 校验通过";
                if (log != null) log(r.Message + "（" + Mb(gotBytes) + "）→ " + dest, false);
                return r;
            }
            catch (Exception ex)
            {
                if (log != null) log("下载更新包异常: " + ex.Message, true);
                return Fail(r, "INTERNAL", "下载更新包异常：" + ex.Message);
            }
        }

        private sealed class ManifestInfo
        {
            public string Version;
            public string PackageUrl;
            public string Sha256;
        }

        private static ManifestInfo FetchManifest(string manifestUrl, Action<string, bool> log, out string error)
        {
            error = null;
            string[] urls = Candidates(manifestUrl);
            string lastErr = null;
            for (int i = 0; i < urls.Length; i++)
            {
                string body = FetchText(urls[i], log, out lastErr);
                if (body == null) continue;
                ManifestInfo mi = ParseManifest(body);
                if (mi != null) return mi;
                lastErr = "更新清单内容无法解析（可能被镜像替换成了错误页）";
                if (log != null) log(lastErr + ": " + urls[i], true);
            }
            error = string.IsNullOrEmpty(lastErr) ? "无法获取更新信息" : lastErr;
            return null;
        }

        /// <summary>清单解析：JSON 取 version/url/sha256；纯文本只取第一行版本号。</summary>
        private static ManifestInfo ParseManifest(string body)
        {
            if (string.IsNullOrEmpty(body)) return null;
            body = body.Trim();
            // 记事本存出来的清单可能带 BOM，不剔掉会把 JSON 当成纯文本解析
            if (body.Length > 0 && body[0] == '\uFEFF') body = body.Substring(1).Trim();
            if (body.Length == 0) return null;

            ManifestInfo mi = new ManifestInfo();
            if (body[0] == '{')
            {
                mi.Version = Clean(ExtractJsonValue(body, "version"));
                mi.PackageUrl = Clean(ExtractJsonValue(body, "url"));
                mi.Sha256 = Clean(ExtractJsonValue(body, "sha256"));
                return mi;
            }

            int nl = body.IndexOf('\n');
            string line = (nl > 0 ? body.Substring(0, nl) : body).Trim();
            if (!Regex.IsMatch(line, @"^[vV]?\d+(\.\d+){1,3}$")) return null;
            mi.Version = line.TrimStart('v', 'V');
            return mi;
        }

        private static string ExtractJsonValue(string json, string key)
        {
            string pat = "\"" + key + "\"";
            int i = json.IndexOf(pat, StringComparison.OrdinalIgnoreCase);
            if (i < 0) return null;
            i = json.IndexOf(':', i + pat.Length);
            if (i < 0) return null;
            i++;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length) return null;

            if (json[i] == '"')
            {
                // 清单里的 url / 哈希不会带转义字符，遇到下一个引号即视为结束
                int end = json.IndexOf('"', i + 1);
                if (end < 0) return null;
                return json.Substring(i + 1, end - i - 1);
            }
            int start = i;
            while (i < json.Length && json[i] != ',' && json[i] != '}' &&
                   json[i] != '\r' && json[i] != '\n') i++;
            return json.Substring(start, i - start).Trim();
        }

        private static string FetchText(string url, Action<string, bool> log, out string error)
        {
            error = null;
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
                req.UserAgent = "Mozilla/5.0 HX-Studio";
                req.Timeout = 20000;
                req.ReadWriteTimeout = 20000;
                req.AllowAutoRedirect = true;
                string proxyDesc;
                req.Proxy = DlssPack.ResolveProxy(out proxyDesc);

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (Stream s = resp.GetResponseStream())
                using (StreamReader reader = new StreamReader(s, Encoding.UTF8, true))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                error = "获取更新清单失败：" + ex.Message;
                if (log != null) log(error, true);
                return null;
            }
        }

        /// <summary>
        /// 单通道流式下载到 .part。流式是为了拿得到进度；
        /// ReadWriteTimeout 设短一些，镜像静默掐断时才能尽快换下一个源。
        /// </summary>
        private static void DownloadOnce(string url, string part, Action<int, long, long> progress,
                                         out long totalBytes)
        {
            totalBytes = 0;
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "Mozilla/5.0 HX-Studio";
            req.Timeout = 30000;
            req.ReadWriteTimeout = 30000;
            req.AllowAutoRedirect = true;
            string proxyDesc;
            req.Proxy = DlssPack.ResolveProxy(out proxyDesc);

            long total = 0;
            long read = 0;
            int lastPct = -5;   // 让 0% 也能推一次，用户点了就有反馈
            byte[] buf = new byte[131072];

            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            {
                total = resp.ContentLength;
                totalBytes = total < 0 ? 0 : total;

                using (Stream ns = resp.GetResponseStream())
                using (FileStream fs = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    int n;
                    while ((n = ns.Read(buf, 0, buf.Length)) > 0)
                    {
                        fs.Write(buf, 0, n);
                        read += n;

                        if (total > 0)
                        {
                            int pct = (int)(read * 100 / total);
                            if (pct > 100) pct = 100;
                            if (pct >= lastPct + 5 || pct == 100)
                            {
                                lastPct = pct;
                                if (progress != null)
                                {
                                    try { progress(pct, read, total); } catch { }
                                }
                            }
                        }
                        else if (progress != null)
                        {
                            // 无 Content-Length：每 4MB 报一次「进行中」，percent=-1
                            long before = (read - n) / 4194304;
                            long now = read / 4194304;
                            if (now != before)
                            {
                                try { progress(-1, read, 0); } catch { }
                            }
                        }
                    }
                }
            }

            if (read <= 0) throw new IOException("服务端返回空内容");
            if (total > 0 && read != total)
                throw new IOException("传输中断（" + Mb(read) + "/" + Mb(total) + "）");
        }

        // ==================== 应用 ====================

        /// <summary>
        /// 备份当前 exe → 生成替换脚本 → 启动脚本。主程序退出由调用方负责。
        /// 失败时保留已下载的包，用户可重试。
        /// </summary>
        public static UpdateResult Apply(Action<string, bool> log)
        {
            UpdateResult r = new UpdateResult();
            try
            {
                if (string.IsNullOrEmpty(_pendingPackage) || !File.Exists(_pendingPackage))
                    return Fail(r, "FILE_LOCKED", "请先下载更新包");

                string exe = CurrentExePath(log);
                if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
                    return Fail(r, "INTERNAL", "无法定位当前程序文件，已中止更新");

                // 只允许替换自己目录里的包：将来若有人改了调用方，
                // 也不能靠一个任意路径把主程序换成别的 exe。
                string dir = UpdateDir;
                bool inside = false;
                try
                {
                    inside = string.Equals(
                        Path.GetDirectoryName(Path.GetFullPath(_pendingPackage)),
                        Path.GetFullPath(dir), StringComparison.OrdinalIgnoreCase);
                }
                catch { }
                if (!inside)
                    return Fail(r, "INTERNAL", "安装包不在更新目录内，已中止（安全检查）");

                // 落地后又被人换过的包同样不能放行：这里再算一次哈希
                string again = Sha256Hex(_pendingPackage, log);
                if (again == null || !string.Equals(again, _pendingSha, StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(_pendingPackage); } catch { }
                    _pendingPackage = null;
                    return Fail(r, "CHECKSUM_MISMATCH", "安装包在下载后被改动（SHA-256 不一致），已删除，请重新下载");
                }

                // 没有备份就绝不覆盖：回滚完全依赖这个文件
                string bak = exe + ".bak";
                try
                {
                    if (File.Exists(bak)) File.Delete(bak);
                    File.Copy(exe, bak, true);
                    if (log != null) log("已备份当前程序 → " + bak, false);
                }
                catch (Exception ex)
                {
                    return Fail(r, "FILE_LOCKED", "备份当前程序失败，已中止更新：" + ex.Message);
                }

                string script;
                try
                {
                    script = WriteScript(_pendingPackage, exe, bak);
                    if (log != null) log("已生成更新脚本 → " + script, false);
                }
                catch (Exception ex)
                {
                    return Fail(r, "INTERNAL", "生成更新脚本失败：" + ex.Message);
                }

                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo();
                    // 用绝对路径调 cmd，避免 PATH 里被塞了同名程序
                    psi.FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe");
                    psi.Arguments = "/c \"" + script + "\"";
                    // 关键：不弹黑窗。UseShellExecute=false 才能配合 CreateNoWindow
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    psi.WindowStyle = ProcessWindowStyle.Hidden;
                    psi.WorkingDirectory = Path.GetDirectoryName(script);
                    Process proc = Process.Start(psi);
                    if (proc == null) return Fail(r, "INTERNAL", "更新脚本启动失败（进程未创建）");

                    // 脚本正常时会一直等着主进程退出（≥1 秒），所以 400ms 后还活着才算真起来了。
                    // 立刻退出说明脚本根本没跑起来（路径/权限/被杀软拦），
                    // 这种情况绝不能关主程序——否则用户看到的是「软件自己没了」。
                    System.Threading.Thread.Sleep(400);
                    bool scriptAlive = false;
                    try { scriptAlive = !proc.HasExited; }
                    catch (Exception ex) { if (log != null) log("检查更新脚本状态失败（按已启动处理）: " + ex.Message, true); scriptAlive = true; }
                    try { proc.Dispose(); } catch { }   // 脚本自己跑，句柄不用留
                    if (!scriptAlive)
                        return Fail(r, "INTERNAL", "更新脚本启动后立即退出，已取消本次更新（脚本保留在临时目录，可手工排查）");
                }
                catch (Exception ex)
                {
                    return Fail(r, "INTERNAL", "启动更新脚本失败：" + ex.Message);
                }

                r.Ok = true;
                r.PackagePath = _pendingPackage;
                r.Version = _pendingVersion;
                r.Sha256 = _pendingSha;
                r.AssetName = Path.GetFileName(_pendingPackage);
                r.Message = "更新脚本已启动，程序将在退出后自动替换并重启";
                if (log != null) log(r.Message, false);

                // 只能应用一次：脚本已经把包拷走了，再点一次没有意义
                _pendingPackage = null;
                return r;
            }
            catch (Exception ex)
            {
                if (log != null) log("应用更新异常: " + ex.Message, true);
                return Fail(r, "INTERNAL", "应用更新异常：" + ex.Message);
            }
        }

        /// <summary>
        /// 生成替换脚本。步骤固定为：
        /// 等主进程退出 → 覆盖 exe → 启动 → 启动失败则用 .bak 还原 → 删除自身。
        /// 回滚写在脚本里而不是 C# 里，因为回滚发生时主程序已经退出，C# 代码跑不到了。
        ///
        /// 覆盖采用「先 copy 成 .new 再 move 改名」：同目录改名是原子的，
        /// 中途断电/杀软拦截不会留下一个被截断的主程序。
        /// </summary>
        private static string WriteScript(string package, string exe, string bak)
        {
            string dir = UpdateDir;
            if (string.IsNullOrEmpty(dir)) throw new InvalidOperationException("更新目录不可用");

            // 脚本放临时目录：它只活几秒，不该在 ProgramData 里越积越多。
            // 例外：%TEMP% 带中文（C:\Users\张三\...）时不是纯 ASCII，
            // cmd 按 OEM 码页读批处理容易跑偏，这种情况退回 ProgramData。
            string scriptDir = Path.GetTempPath();
            if (!IsAsciiPath(scriptDir)) scriptDir = dir;

            string exeDir = Path.GetDirectoryName(exe);
            string img = Path.GetFileName(exe);
            int pid = 0;
            try { pid = Process.GetCurrentProcess().Id; }
            catch (Exception ex) { LogLine("取当前进程号失败，改用映像名等待: " + ex.Message, true); }

            string waitLine = pid > 0
                ? "tasklist /FI \"PID eq " + pid + "\" /NH 2>nul | find /I \"%IMG%\" >nul"
                : "tasklist /FI \"IMAGENAME eq %IMG%\" /NH 2>nul | find /I \"%IMG%\" >nul";

            StringBuilder sb = new StringBuilder(4096);
            sb.AppendLine("@echo off");
            // 注意 rem 行里不能出现 > —— cmd 会把它当重定向，凭空建一个文件出来
            sb.AppendLine("rem HX Studio self-update script, generated by UpdateInstaller.");
            sb.AppendLine("rem steps: wait for old process, swap exe, start, rollback on failure, delete self");
            sb.AppendLine("setlocal");
            // 开头先停 1 秒：调用方会用「脚本是否在 400ms 内退出」判断它有没有真跑起来，
            // 这段等待保证任何真正的动作都发生在那个判断之后，避免误判成没起来。
            sb.AppendLine("ping -n 2 127.0.0.1 >nul");
            sb.AppendLine("set \"NEW=" + Q(package) + "\"");
            sb.AppendLine("set \"EXE=" + Q(exe) + "\"");
            sb.AppendLine("set \"BAK=" + Q(bak) + "\"");
            sb.AppendLine("set \"EXEDIR=" + Q(exeDir) + "\"");
            sb.AppendLine("set \"IMG=" + Q(img) + "\"");
            sb.AppendLine("");

            // 1) 等主进程退出。最多等约 60 秒，超时也往下走
            //    （后面覆盖本身还有重试，进程晚一点退也能接上）。
            sb.AppendLine("rem [1] wait for the main process to exit (max ~60s)");
            sb.AppendLine("set /a WAITN=0");
            sb.AppendLine(":waitloop");
            sb.AppendLine(waitLine);
            sb.AppendLine("if errorlevel 1 goto :swap");
            sb.AppendLine("set /a WAITN+=1");
            sb.AppendLine("if %WAITN% GEQ 60 goto :swap");
            sb.AppendLine("ping -n 2 127.0.0.1 >nul");
            sb.AppendLine("goto :waitloop");
            sb.AppendLine("");

            // 2) 覆盖。copy 可能被杀软/占用拖住，重试 30 次
            sb.AppendLine("rem [2] replace exe: copy to .new then atomic rename");
            sb.AppendLine("set /a TRYN=0");
            sb.AppendLine(":swaploop");
            sb.AppendLine("copy /y \"%NEW%\" \"%EXE%.new\" >nul 2>nul");
            sb.AppendLine("if errorlevel 1 goto :swapretry");
            sb.AppendLine("move /y \"%EXE%.new\" \"%EXE%\" >nul 2>nul");
            sb.AppendLine("if not errorlevel 1 goto :launch");
            sb.AppendLine(":swapretry");
            sb.AppendLine("set /a TRYN+=1");
            sb.AppendLine("if %TRYN% GEQ 30 goto :rollback");
            sb.AppendLine("ping -n 2 127.0.0.1 >nul");
            sb.AppendLine("goto :swaploop");
            sb.AppendLine("");

            // 3) 启动并确认进程真的起来了：起不来说明新包是坏的
            sb.AppendLine("rem [3] start new exe and verify it stays alive");
            sb.AppendLine(":launch");
            sb.AppendLine("cd /d \"%EXEDIR%\"");
            sb.AppendLine("start \"\" \"%EXE%\"");
            sb.AppendLine("ping -n 7 127.0.0.1 >nul");
            sb.AppendLine("tasklist /FI \"IMAGENAME eq %IMG%\" /NH 2>nul | find /I \"%IMG%\" >nul");
            sb.AppendLine("if errorlevel 1 goto :rollback");
            sb.AppendLine("goto :done");
            sb.AppendLine("");

            // 4) 回滚：覆盖失败或新程序起不来，都用备份还原并重新拉起
            sb.AppendLine("rem [4] rollback from .bak");
            sb.AppendLine(":rollback");
            sb.AppendLine("set /a RBN=0");
            sb.AppendLine(":rbloop");
            sb.AppendLine("copy /y \"%BAK%\" \"%EXE%.new\" >nul 2>nul");
            sb.AppendLine("if errorlevel 1 goto :rbretry");
            sb.AppendLine("move /y \"%EXE%.new\" \"%EXE%\" >nul 2>nul");
            sb.AppendLine("if not errorlevel 1 goto :rbstart");
            sb.AppendLine(":rbretry");
            sb.AppendLine("set /a RBN+=1");
            sb.AppendLine("if %RBN% GEQ 15 goto :done");
            sb.AppendLine("ping -n 2 127.0.0.1 >nul");
            sb.AppendLine("goto :rbloop");
            sb.AppendLine(":rbstart");
            sb.AppendLine("cd /d \"%EXEDIR%\"");
            sb.AppendLine("start \"\" \"%EXE%\"");
            sb.AppendLine("");

            // 5) 清理：临时 .new 与脚本自身（del 必须放最后，删掉后 cmd 读不到后续行）
            sb.AppendLine(":done");
            sb.AppendLine("del /f /q \"%EXE%.new\" >nul 2>nul");
            sb.AppendLine("del /f /q \"%~f0\" >nul 2>nul");

            string script = Path.Combine(scriptDir, "hx-update-" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".cmd");
            // cmd.exe 按系统 ANSI 码页读批处理，这里必须跟着用 Encoding.Default；
            // 写成 UTF-8 会让带中文的安装路径变成乱码，脚本直接跑偏。
            File.WriteAllText(script, sb.ToString(), Encoding.Default);
            return script;
        }

        /// <summary>批处理里 % 是变量展开符，路径里真出现必须写成 %%。</summary>
        private static string Q(string path)
        {
            if (path == null) return "";
            return path.Replace("%", "%%");
        }

        private static bool IsAsciiPath(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            for (int i = 0; i < s.Length; i++)
                if (s[i] > 127) return false;
            return true;
        }

        // ==================== 工具 ====================

        private static string CurrentExePath(Action<string, bool> log)
        {
            try
            {
                Process p = Process.GetCurrentProcess();
                if (p.MainModule != null && !string.IsNullOrEmpty(p.MainModule.FileName))
                    return p.MainModule.FileName;
            }
            catch (Exception ex)
            {
                if (log != null) log("读取主模块路径失败，改用备用方式: " + ex.Message, true);
            }
            try
            {
                Assembly asm = Assembly.GetEntryAssembly();
                if (asm != null && !string.IsNullOrEmpty(asm.Location)) return asm.Location;
            }
            catch (Exception ex)
            {
                if (log != null) log("读取入口程序集路径失败: " + ex.Message, true);
            }
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                if (args != null && args.Length > 0) return args[0];
            }
            catch (Exception ex)
            {
                if (log != null) log("读取命令行路径失败: " + ex.Message, true);
            }
            return null;
        }

        /// <summary>候选下载地址：有加速前缀且目标是 GitHub 时先走前缀，再回落直连。</summary>
        private static string[] Candidates(string url)
        {
            string prefix = null;
            try
            {
                AppSettings.Load();
                prefix = AppSettings.DownloadProxy;
            }
            catch (Exception ex)
            {
                LogLine("读取加速前缀失败: " + ex.Message, true);
            }

            if (!string.IsNullOrEmpty(prefix) &&
                url.StartsWith("https://github.com", StringComparison.OrdinalIgnoreCase))
            {
                string p = prefix.Trim();
                if (p.Length > 0)
                {
                    p = p.TrimEnd('/') + "/";
                    return new string[] { p + url, url };
                }
            }
            return new string[] { url };
        }

        private static bool IsHttpUrl(string url)
        {
            return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>从下载地址里取文件名；取不到就用版本号兜底，绝不把 URL 当文件名。</summary>
        private static string SafeFileName(string url, string version)
        {
            try
            {
                string pathPart = url;
                int q = pathPart.IndexOf('?');
                if (q >= 0) pathPart = pathPart.Substring(0, q);
                int slash = pathPart.LastIndexOf('/');
                string name = slash >= 0 && slash < pathPart.Length - 1
                    ? pathPart.Substring(slash + 1) : null;
                if (!string.IsNullOrEmpty(name))
                {
                    name = Uri.UnescapeDataString(name);
                    char[] bad = Path.GetInvalidFileNameChars();
                    for (int i = 0; i < bad.Length; i++) name = name.Replace(bad[i], '_');
                    if (name.Length > 0 && name.IndexOf('.') > 0) return name;
                }
            }
            catch (Exception ex)
            {
                LogLine("解析安装包文件名失败，改用版本号命名: " + ex.Message, true);
            }
            return "hx-update-" + (string.IsNullOrEmpty(version) ? "latest" : version) + ".exe";
        }

        private static string Sha256Hex(string path, Action<string, bool> log)
        {
            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (SHA256 sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(fs);
                    StringBuilder sb = new StringBuilder(hash.Length * 2);
                    for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
                    return sb.ToString();
                }
            }
            catch (Exception ex)
            {
                if (log != null) log("计算 SHA-256 失败: " + ex.Message, true);
                return null;
            }
        }

        /// <summary>规整哈希；非法（长度/字符集不对）返回 null，调用方据此拒绝下载。</summary>
        private static string NormalizeSha256(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            s = s.Trim();
            if (s.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) s = s.Substring(7).Trim();
            if (s.Length != 64) return null;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex) return null;
            }
            return s.ToLowerInvariant();
        }

        private static string Clean(string s)
        {
            return s == null ? null : s.Trim();
        }

        private static string Mb(long bytes)
        {
            return (bytes / 1024.0 / 1024.0).ToString("0.0") + " MB";
        }

        private static UpdateResult Fail(UpdateResult r, string code, string message)
        {
            r.Ok = false;
            r.Code = code;
            r.Message = message;
            return r;
        }

        /// <summary>本类被 WebShell 调用时通常已传入日志回调；这里只兜没有回调的内部场景。</summary>
        private static void LogLine(string msg, bool isError)
        {
            try
            {
                File.AppendAllText(
                    Path.Combine(NativeCore.GetLogsDirectory(), "update.log"),
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " +
                    (isError ? "[WARN] " : "") + msg + Environment.NewLine,
                    Encoding.UTF8);
            }
            catch { }
        }
    }
}
