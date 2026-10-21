using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace CMP90HXToolbox
{
    /// <summary>
    /// HX Studio 6.x 的 WebView2 宿主窗口。
    ///
    /// 设计约束（对应迁移方案 V2.2 第 4 章）：
    /// 1. 无边框 + WindowChrome，靠 app-region: drag 实现标题栏拖动；
    /// 2. HTML 从内存流直出，零磁盘落盘，避免 ProgramData 被篡改写导致提权；
    /// 3. WebView2Loader.dll 按进程位数从嵌入资源解压，再用 SetLoaderDllFolderPath 指定；
    /// 4. 外部导航一律拦截，交由系统浏览器打开；
    /// 5. 桥接只走 WebMessage（JSON 文本），不注入 HostObject。
    ///
    /// 当前阶段（P3 冒烟）：只接通窗口控制与渲染，业务数据推送留到 P4。
    /// 因此页面会在 800ms 后被自身的 MOCK 看门狗接管，显示演示数据。
    /// </summary>
    internal sealed class WebShellWindow : Window
    {
        private const string StartUrl = "https://hx.local/index.html";
        private const string HostOrigin = "https://hx.local";

        private WebView2 _webView;
        private CoreWebView2Environment _env;
        private static byte[] _htmlCache;

        public WebShellWindow()
        {
            Title = "HX家族多功能工具箱";
            Width = 1280;
            Height = 800;
            MinWidth = 1160;
            MinHeight = 660;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            AllowsTransparency = false;   // 必须 false：HwndHost 无法在分层窗口里渲染
            Background = new SolidColorBrush(Color.FromRgb(7, 9, 14));

            WindowChrome chrome = new WindowChrome();
            chrome.CaptionHeight = 0;
            chrome.ResizeBorderThickness = new Thickness(6);
            chrome.GlassFrameThickness = new Thickness(0);
            chrome.CornerRadius = new CornerRadius(0);
            chrome.UseAeroCaptionButtons = false;
            WindowChrome.SetWindowChrome(this, chrome);

            _webView = new WebView2();
            _webView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 7, 9, 14);
            _webView.HorizontalAlignment = HorizontalAlignment.Stretch;
            _webView.VerticalAlignment = VerticalAlignment.Stretch;
            Content = _webView;

            Loaded += OnWindowLoaded;
            Closed += OnWindowClosed;
        }

        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await StartAsync();
            }
            catch (Exception ex)
            {
                LogCrash("WebShell 启动失败", ex);
                ModernMessageBox.Show(
                    "新界面启动失败，将返回旧界面。\n\n原因：" + ex.Message +
                    "\n\n如需排查，请查看日志目录下的 crash.log。",
                    "HX Studio 6.x", MessageBoxButton.OK, MessageBoxImage.Warning);
                try { Close(); } catch { }
            }
        }

        private void OnWindowClosed(object sender, EventArgs e)
        {
            try
            {
                if (_sensorTimer != null) _sensorTimer.Stop();
            }
            catch { }
            // 交还控制权：程序退出后由后台计划任务继续做动态调节
            try
            {
                if (NativeCore.IsPowerSaverTaskInstalled())
                    NativeCore.ResumePowerSaverTask(LogPower);
            }
            catch { }
            try
            {
                if (_webView != null) _webView.Dispose();
            }
            catch { }
        }

        // ---------- 窗口圆角（Windows 11）----------

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DwmwaWindowCornerPreference = 33;
        private const int DwmwcpRound = 2;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            // 无边框窗口默认是直角。让系统给窗口本身上圆角，
            // 这样内容可以铺满窗口、四边不留空隙，同时外轮廓仍是圆角。
            try
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;
                int pref = DwmwcpRound;
                DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref pref, sizeof(int));
            }
            catch { }
        }

        private async Task StartAsync()
        {
            // 1. 加载器必须在任何 CoreWebView2Environment API 之前指定
            string loaderDir = ExtractLoader();
            CoreWebView2Environment.SetLoaderDllFolderPath(loaderDir);

            // 2. 运行时探测（缺失会在这里抛）
            string runtimeVersion = CoreWebView2Environment.GetAvailableBrowserVersionString();
            LogInfo("WebView2 Runtime: " + runtimeVersion);

            // 3. 环境（独立用户数据目录，避免污染系统盘默认位置）
            string udf = Path.Combine(NativeCore.GetDataDirectory(), "webview2_udf");
            if (!Directory.Exists(udf)) Directory.CreateDirectory(udf);
            CoreWebView2EnvironmentOptions options = new CoreWebView2EnvironmentOptions();
            _env = await CoreWebView2Environment.CreateAsync(null, udf, options);

            // 4. 控件初始化
            await _webView.EnsureCoreWebView2Async(_env);
            CoreWebView2 core = _webView.CoreWebView2;

            // 5. 安全基线（全部关闭）
            CoreWebView2Settings st = core.Settings;
            st.AreDevToolsEnabled = false;
            st.AreDefaultContextMenusEnabled = false;
            st.AreBrowserAcceleratorKeysEnabled = false;
            st.IsStatusBarEnabled = false;
            st.IsZoomControlEnabled = false;
            st.IsPasswordAutosaveEnabled = false;
            st.IsGeneralAutofillEnabled = false;
            try { st.IsNonClientRegionSupportEnabled = true; } catch { }

            // 6. 事件
            core.WebMessageReceived += OnWebMessageReceived;
            core.NavigationStarting += OnNavigationStarting;
            core.NewWindowRequested += OnNewWindowRequested;
            core.ProcessFailed += OnProcessFailed;

            // 握手必须在页面启动后 800ms 内到达一次，否则页面判定「无宿主」转 MOCK 模式，
            // 之后所有命令都不再发往宿主（窗口三键会因此完全失灵）。
            core.DOMContentLoaded += OnDomContentLoaded;
            core.NavigationCompleted += OnNavigationCompleted;

            // 7. 资源路由：只放行 hx.local，其余一律 403
            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += OnWebResourceRequested;

            core.Navigate(StartUrl);
            LogInfo("已导航到 " + StartUrl);
        }

        // ---------- 资源 ----------

        private static byte[] GetHtmlBytes()
        {
            if (_htmlCache != null) return _htmlCache;
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("index.html"))
            {
                if (s == null) throw new FileNotFoundException("内置资源 index.html 缺失，程序包不完整。");
                using (MemoryStream ms = new MemoryStream())
                {
                    s.CopyTo(ms);
                    _htmlCache = ms.ToArray();
                }
            }
            return _htmlCache;
        }

        private static string ExtractLoader()
        {
            string arch = IntPtr.Size == 8 ? "x64" : "x86";
            string resName = IntPtr.Size == 8 ? "wv2loader64.dll" : "wv2loader32.dll";
            string dir = Path.Combine(NativeCore.GetDataDirectory(), "webview2loader", arch);
            string file = Path.Combine(dir, "WebView2Loader.dll");

            if (File.Exists(file) && new FileInfo(file).Length > 0) return dir;

            Directory.CreateDirectory(dir);
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resName))
            {
                if (s == null) throw new FileNotFoundException("内置 WebView2Loader 资源缺失：" + resName);
                using (FileStream fs = new FileStream(file, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    s.CopyTo(fs);
                }
            }
            LogInfo("已解压 WebView2Loader -> " + file);
            return dir;
        }

        private void OnWebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            try
            {
                if (e.Request.Uri.StartsWith(StartUrl, StringComparison.OrdinalIgnoreCase))
                {
                    MemoryStream ms = new MemoryStream(GetHtmlBytes(), false);
                    e.Response = _env.CreateWebResourceResponse(
                        ms, 200, "OK",
                        "Content-Type: text/html; charset=utf-8\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff");
                    return;
                }

                // 任何其他请求（含外链、子资源）一律阻断——本界面是单文件，不该有别的请求
                e.Response = _env.CreateWebResourceResponse(
                    null, 403, "Forbidden", "Content-Type: text/plain; charset=utf-8");
            }
            catch { }
        }

        // ---------- 导航守卫 ----------

        private void OnNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (e.Uri != null && e.Uri.StartsWith(HostOrigin, StringComparison.OrdinalIgnoreCase))
            {
                // 重新加载时允许重新握手
                _handshakeSent = false;
                return;
            }

            e.Cancel = true;
            try
            {
                if (e.Uri != null && (e.Uri.StartsWith("http://") || e.Uri.StartsWith("https://")))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = e.Uri,
                        UseShellExecute = true
                    });
                }
            }
            catch { }
        }

        private void OnNewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;
            try
            {
                if (e.Uri != null && (e.Uri.StartsWith("http://") || e.Uri.StartsWith("https://")))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = e.Uri,
                        UseShellExecute = true
                    });
                }
            }
            catch { }
        }

        private void OnProcessFailed(object sender, CoreWebView2ProcessFailedEventArgs e)
        {
            LogInfo("WebView2 进程异常: " + e.ProcessFailedKind);
            if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.BrowserProcessExited)
            {
                try
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        try { _webView.CoreWebView2.Reload(); } catch { }
                    }));
                }
                catch { }
            }
        }

        // ---------- 数据推送 ----------

        private DispatcherTimer _sensorTimer;
        private bool _handshakeSent;
        private string _theme = "light";

        /// <summary>
        /// 握手时机尽量早：DOMContentLoaded 早于页面里的 800ms 看门狗，
        /// 才能让页面认为「有宿主」，命令才会真正发到 C#。
        /// </summary>
        private void OnDomContentLoaded(object sender, CoreWebView2DOMContentLoadedEventArgs e)
        {
            PushHandshake();
        }

        private void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess)
            {
                LogInfo("页面导航失败: " + e.WebErrorStatus);
                return;
            }
            PushHandshake();

            // SystemDetector.DetectAll 会走 WMI，比较慢，放后台线程，避免卡界面
            Task.Run(new Action(delegate
            {
                string[] events;
                try { events = HardwareBridge.AllInitialEvents(); }
                catch (Exception ex) { LogCrash("构建首屏状态失败", ex); return; }
                for (int i = 0; i < events.Length; i++) PostToPage(events[i]);
            }));

            StartSensorTimer();
        }

        private void PushHandshake()
        {
            if (_handshakeSent) return;
            _handshakeSent = true;
            PostToPage(HardwareBridge.ThemeEvt(_theme));
            PostToPage(HardwareBridge.VersionEvt());
            LogInfo("已发送握手事件 theme.apply / app.version");

            // 单控制器原则：程序接管时先把后台计划任务按下去，
            // 否则两边同时锁频/解锁会互相覆盖（实测会让卡在负载下也拉不回频率）
            Task.Run(new Action(delegate { NativeCore.SuspendPowerSaverTask(LogPower); }));
        }

        private void StartSensorTimer()
        {
            if (_sensorTimer == null)
            {
                _sensorTimer = new DispatcherTimer();
                _sensorTimer.Interval = TimeSpan.FromMilliseconds(1500);
                _sensorTimer.Tick += delegate
                {
                    Task.Run(new Action(delegate
                    {
                        string json;
                        try { json = HardwareBridge.SensorsTickEvt(); }
                        catch { return; }
                        PostToPage(json);
                    }));
                };
            }
            _sensorTimer.Start();
        }

        private void PostToPage(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                if (Dispatcher.CheckAccess()) DoPost(json);
                else Dispatcher.BeginInvoke(new Action(delegate { DoPost(json); }));
            }
            catch (Exception ex)
            {
                LogCrash("推送数据失败", ex);
            }
        }

        private void DoPost(string json)
        {
            try
            {
                if (_webView != null && _webView.CoreWebView2 != null)
                    _webView.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch { }
        }

        // ---------- 消息桥接 ----------

        private void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                if (!string.Equals(e.Source, StartUrl, StringComparison.OrdinalIgnoreCase)) return;

                string json = e.WebMessageAsJson;
                if (string.IsNullOrEmpty(json)) return;

                string action = ExtractJsonString(json, "action");
                string id = ExtractJsonString(json, "id");
                if (string.IsNullOrEmpty(action)) return;

                // P3 阶段只处理窗口控制；业务 action 留到 P4 的 ActionRouter
                if (action == "window.min")
                {
                    WindowState = WindowState.Minimized;
                }
                else if (action == "window.max")
                {
                    WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
                }
                else if (action == "window.close")
                {
                    Close();
                }
                else if (action == "device.refresh" || action == "app.init")
                {
                    // 页面主动要数据：重新采集并推一次快照
                    Task.Run(new Action(delegate
                    {
                        string[] events;
                        try { events = HardwareBridge.AllInitialEvents(); }
                        catch { return; }
                        for (int i = 0; i < events.Length; i++) PostToPage(events[i]);
                    }));
                }
                else if (action == "app.ready")
                {
                    // 首帧就绪信号，仅记录
                }
                else if (action == "theme.set")
                {
                    string t = ExtractPayloadString(json, "theme");
                    _theme = (t == "dark") ? "dark" : "light";
                }
                else if (action == "power.setPreset")
                {
                    string preset = ExtractPayloadString(json, "preset");
                    int secs = ExtractPayloadNumber(json, "idleSeconds");
                    string rid = id;
                    // 平滑降频是分步执行的，必须放后台，否则会把界面卡住 1.5 秒
                    Task.Run(new Action(delegate
                    {
                        // 未带 idleSeconds 时沿用当前设定，不能重置成 8
                        int useSecs = secs <= 0 ? PowerPresets.IdleSeconds : secs;
                        bool ok = PowerPresets.Apply(preset, useSecs, LogPower);
                        if (ok) ReplyOk(rid, action);
                        else ReplyFail(rid, action, "DRIVER_REJECT", "设置失败：" + (PowerPresets.LastError ?? ""));
                        PostToPage(HardwareBridge.PowerStateEvt());
                    }));
                    return;
                }
                else if (action == "power.enable")
                {
                    int secs = ExtractPayloadNumber(json, "idleSeconds");
                    string rid = id;
                    Task.Run(new Action(delegate
                    {
                        PowerPresets.Apply("adaptive", secs <= 0 ? 8 : secs, LogPower);
                        ReplyOk(rid, action);
                        PostToPage(HardwareBridge.PowerStateEvt());
                    }));
                    return;
                }
                else if (action == "power.disable")
                {
                    string rid = id;
                    Task.Run(new Action(delegate
                    {
                        PowerPresets.StopAll(LogPower);
                        ReplyOk(rid, action);
                        PostToPage(HardwareBridge.PowerStateEvt());
                    }));
                    return;
                }
                else if (action == "dialog.browse")
                {
                    string kind = ExtractPayloadString(json, "kind");
                    string picked = ShowBrowseDialog(kind);
                    if (!string.IsNullOrEmpty(picked))
                    {
                        ReplyWithPath(id, action, picked);
                        return;
                    }
                    ReplyFail(id, action, "CANCELLED", "已取消选择");
                    return;
                }
                else
                {
                    // 关键：未接入的动作绝不假装成功。
                    // 否则用户看到「操作成功」，实际什么都没发生——比报错更危险。
                    ReplyFail(id, action, "NOT_SUPPORTED", "该功能尚未接入新版界面，请暂用旧界面（--legacy）");
                    return;
                }

                // 必须回 res：页面 sendCmd 时会把触发按钮置 disabled + is-loading，
                // 只有收到同 id 的 res 才恢复。不回的话按钮点一次就永久卡死。
                ReplyOk(id, action);
            }
            catch (Exception ex)
            {
                LogCrash("处理网页消息失败", ex);
            }
        }

        /// <summary>从单层 JSON 里取字符串字段（足够应付 {kind,id,action,payload} 这层）。</summary>
        private static string ExtractJsonString(string json, string key)
        {
            string pat = "\"" + key + "\":\"";
            int i = json.IndexOf(pat, StringComparison.Ordinal);
            if (i < 0) return null;
            i += pat.Length;
            int j = json.IndexOf('"', i);
            if (j < 0) return null;
            return json.Substring(i, j - i);
        }

        private void ReplyOk(string id, string action)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(delegate { ReplyOk(id, action); }));
                return;
            }
            try
            {
                if (_webView == null || _webView.CoreWebView2 == null) return;

                StringBuilder sb = new StringBuilder(160);
                sb.Append("{\"kind\":\"res\",\"id\":\"").Append(id == null ? string.Empty : id)
                  .Append("\",\"action\":\"").Append(action)
                  .Append("\",\"payload\":{\"ok\":true,\"data\":null}}");
                _webView.CoreWebView2.PostWebMessageAsJson(sb.ToString());
            }
            catch (Exception ex)
            {
                LogCrash("回复网页消息失败", ex);
            }
        }

        /// <summary>失败应答。页面会弹「[错误码] 原因」并把对应页面置为错误态。</summary>
        private void ReplyFail(string id, string action, string code, string message)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(delegate { ReplyFail(id, action, code, message); }));
                return;
            }
            try
            {
                if (_webView == null || _webView.CoreWebView2 == null) return;

                StringBuilder sb = new StringBuilder(220);
                sb.Append("{\"kind\":\"res\",\"id\":\"").Append(id == null ? string.Empty : id)
                  .Append("\",\"action\":\"").Append(action)
                  .Append("\",\"payload\":{\"ok\":false,\"data\":null,\"error\":{\"code\":\"").Append(code)
                  .Append("\",\"message\":").Append(HardwareBridge.Quote(message)).Append("}}}");
                _webView.CoreWebView2.PostWebMessageAsJson(sb.ToString());
            }
            catch (Exception ex)
            {
                LogCrash("回复失败消息出错", ex);
            }
        }

        private void ReplyWithPath(string id, string action, string path)
        {
            try
            {
                if (_webView == null || _webView.CoreWebView2 == null) return;

                StringBuilder sb = new StringBuilder(320);
                sb.Append("{\"kind\":\"res\",\"id\":\"").Append(id == null ? string.Empty : id)
                  .Append("\",\"action\":\"").Append(action)
                  .Append("\",\"payload\":{\"ok\":true,\"data\":{\"path\":")
                  .Append(HardwareBridge.Quote(path)).Append("}}}");
                _webView.CoreWebView2.PostWebMessageAsJson(sb.ToString());
            }
            catch (Exception ex)
            {
                LogCrash("回复路径出错", ex);
            }
        }

        /// <summary>只在 payload 段里找字段，避免命中信封自身的 "kind":"cmd"。</summary>
        private static string ExtractPayloadString(string json, string key)
        {
            int p = json.IndexOf("\"payload\"", StringComparison.Ordinal);
            if (p < 0) return null;
            string pat = "\"" + key + "\":\"";
            int i = json.IndexOf(pat, p, StringComparison.Ordinal);
            if (i < 0) return null;
            i += pat.Length;
            int j = json.IndexOf('"', i);
            if (j < 0) return null;
            return json.Substring(i, j - i);
        }

        /// <summary>只在 payload 段里找数字字段。</summary>
        private static int ExtractPayloadNumber(string json, string key)
        {
            int p = json.IndexOf("\"payload\"", StringComparison.Ordinal);
            if (p < 0) return 0;
            string pat = "\"" + key + "\":";
            int i = json.IndexOf(pat, p, StringComparison.Ordinal);
            if (i < 0) return 0;
            i += pat.Length;
            while (i < json.Length && (json[i] == ' ' || json[i] == '"')) i++;
            int start = i;
            while (i < json.Length && (char.IsDigit(json[i]) || json[i] == '.')) i++;
            if (i <= start) return 0;
            double v;
            if (double.TryParse(json.Substring(start, i - start),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out v))
                return (int)Math.Round(v);
            return 0;
        }

        private static void LogPower(string message, bool isError)
        {
            LogInfo((isError ? "[WARN] " : "") + message);
        }

        private string ShowBrowseDialog(string kind)
        {
            try
            {
                if (string.Equals(kind, "file", StringComparison.OrdinalIgnoreCase))
                {
                    using (System.Windows.Forms.OpenFileDialog dlg = new System.Windows.Forms.OpenFileDialog())
                    {
                        dlg.Title = "选择游戏主程序";
                        dlg.Filter = "可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*";
                        dlg.CheckFileExists = true;
                        return (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK) ? dlg.FileName : null;
                    }
                }
                using (System.Windows.Forms.FolderBrowserDialog dlg2 = new System.Windows.Forms.FolderBrowserDialog())
                {
                    dlg2.Description = "选择目录";
                    return (dlg2.ShowDialog() == System.Windows.Forms.DialogResult.OK) ? dlg2.SelectedPath : null;
                }
            }
            catch (Exception ex)
            {
                LogCrash("打开文件选择框失败", ex);
                return null;
            }
        }

        // ---------- 日志 ----------

        private static void LogInfo(string msg)
        {
            try
            {
                File.AppendAllText(
                    Path.Combine(NativeCore.GetLogsDirectory(), "webshell.log"),
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " + msg + Environment.NewLine,
                    Encoding.UTF8);
            }
            catch { }
        }

        private static void LogCrash(string title, Exception ex)
        {
            try
            {
                File.AppendAllText(
                    Path.Combine(NativeCore.GetLogsDirectory(), "crash.log"),
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + title + ": " + ex + Environment.NewLine,
                    Encoding.UTF8);
            }
            catch { }
        }
    }
}
