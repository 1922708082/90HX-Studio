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
        /* 后端单飞锁。
           前端超时只是「别让用户再点」，两个窗口各点一次照样会并发：
           引导组并发 = 两串 bcdedit 交错（可以写坏 BCD）；
           下载组并发 = 两个线程写同一个 .part（文件损坏）。
           锁在收到该组的 ReplyOk/ReplyFail 时释放。 */
        private static int _lockPcie;      // unlock.install / unlock.rollback
        private static int _lockDlss;      // dlss.deploy / dlss.remove / dlss.checkUpdate
        private static int _lockUpdate;    // update.download / update.apply
        private static string _lockPcieToken = "";
        private static string _lockPcieOwner = "";
        private static DateTime _lockPcieAt = DateTime.MinValue;
        private static string _lockDlssToken = "";
        private static string _lockDlssOwner = "";
        private static DateTime _lockDlssAt = DateTime.MinValue;
        private static string _lockUpdateToken = "";
        private static string _lockUpdateOwner = "";
        private static DateTime _lockUpdateAt = DateTime.MinValue;

        private static int GroupOf(string action)
        {
            if (action == null) return 0;
            // factory.reset 和 unlock.install/rollback 同组：都会改 BCD/ESP，
            // 两边并发执行等于两串 bcdedit 交错，可以把 BCD 写坏。
            if (action == "unlock.install" || action == "unlock.rollback" || action == "factory.reset") return 1;
            if (action == "dlss.deploy" || action == "dlss.remove" || action == "dlss.checkUpdate") return 2;
            if (action == "update.download" || action == "update.apply") return 3;
            return 0;
        }

        /// <summary>尝试取组锁；已被占用则返回 false。</summary>
        private static bool TryEnterGroup(string action, string id)
        {
            int g = GroupOf(action);
            if (g == 1)
            {
                if (System.Threading.Interlocked.CompareExchange(ref _lockPcie, 1, 0) != 0) return false;
                _lockPcieToken = id; _lockPcieOwner = action; _lockPcieAt = DateTime.UtcNow; return true;
            }
            if (g == 2)
            {
                if (System.Threading.Interlocked.CompareExchange(ref _lockDlss, 1, 0) != 0) return false;
                _lockDlssToken = id; _lockDlssOwner = action; _lockDlssAt = DateTime.UtcNow; return true;
            }
            if (g == 3)
            {
                if (System.Threading.Interlocked.CompareExchange(ref _lockUpdate, 1, 0) != 0) return false;
                _lockUpdateToken = id; _lockUpdateOwner = action; _lockUpdateAt = DateTime.UtcNow; return true;
            }
            return true;
        }

        /// <summary>BUSY 时告诉用户「谁在跑、跑了多久」，避免一句「请稍等」被当成敷衍。</summary>
        private static string BusyMessage(string action)
        {
            int g = GroupOf(action);
            string owner = ""; DateTime at = DateTime.MinValue;
            if (g == 1) { owner = _lockPcieOwner; at = _lockPcieAt; }
            else if (g == 2) { owner = _lockDlssOwner; at = _lockDlssAt; }
            else if (g == 3) { owner = _lockUpdateOwner; at = _lockUpdateAt; }
            int secs = at == DateTime.MinValue ? 0 : (int)(DateTime.UtcNow - at).TotalSeconds;
            string who = string.IsNullOrEmpty(owner) ? "同类操作" : owner;
            return who + " 仍在执行（已 " + secs + " 秒），完成前不能重复触发。";
        }

        /// <summary>回执时释放组锁。任何操作最终都会回执，所以这里释放是可靠的。</summary>
        /// <summary>
        /// 只释放「自己持有的那把锁」。
        /// 按 action 直接清 0 是错的：BUSY 分支也会走到这里，
        /// 于是「提示忙」反而把正在运行那一方的锁放掉，第三次点击就真并发了。
        /// 用命令 id 做 token，id 不匹配（含 BUSY、迟到回执）一律不放。
        /// </summary>
        private static void ExitGroup(string action, string id)
        {
            int g = GroupOf(action);
            if (g == 1)
            {
                if (!string.Equals(_lockPcieToken, id, StringComparison.Ordinal)) return;
                _lockPcieToken = ""; _lockPcieOwner = "";
                System.Threading.Interlocked.Exchange(ref _lockPcie, 0);
            }
            else if (g == 2)
            {
                if (!string.Equals(_lockDlssToken, id, StringComparison.Ordinal)) return;
                _lockDlssToken = ""; _lockDlssOwner = "";
                System.Threading.Interlocked.Exchange(ref _lockDlss, 0);
            }
            else if (g == 3)
            {
                if (!string.Equals(_lockUpdateToken, id, StringComparison.Ordinal)) return;
                _lockUpdateToken = ""; _lockUpdateOwner = "";
                System.Threading.Interlocked.Exchange(ref _lockUpdate, 0);
            }
        }

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
        private string _theme = "light";   // 启动后由 AppSettings 覆盖

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

            // 单控制器原则 + 设置恢复：暂停后台任务，并把上次的档位重新应用上
            Task.Run(new Action(delegate
            {
                NativeCore.SuspendPowerSaverTask(LogPower);
                try
                {
                    AppSettings.Load();
                    if (!string.IsNullOrEmpty(AppSettings.Preset) && AppSettings.Preset != "none")
                    {
                        LogInfo("恢复上次档位: " + AppSettings.Preset + " / " + AppSettings.IdleSeconds + "s");
                        PowerPresets.Apply(AppSettings.Preset, AppSettings.IdleSeconds, LogPower);
                        PostToPage(HardwareBridge.PowerStateEvt());
                    }
                }
                catch (Exception ex) { LogCrash("恢复上次档位失败", ex); }
            }));
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
            // 提到 try 外层：兜底 catch 里也要能拿到 id/action 才能回执，
            // 否则一条消息处理炸了，前端那个按钮就永久 disabled + is-loading。
            string id = null;
            string action = null;
            try
            {
                if (!string.Equals(e.Source, StartUrl, StringComparison.OrdinalIgnoreCase)) return;

                string json = e.WebMessageAsJson;
                if (string.IsNullOrEmpty(json)) return;

                action = ExtractJsonString(json, "action");
                id = ExtractJsonString(json, "id");
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
                else if (action == "spoof.apply")
                {
                    string targetName = ExtractPayloadString(json, "targetName");
                    // 型号只能来自内置目录：这一串会被写进注册表 DriverDesc，
                    // 任意字符串等于把别人的驱动描述改乱，且无从校验。
                    // （proxyName 在 NativeCore 里已有 IsKnownProxyName 兜底，型号之前没有。）
                    bool modelKnown = false;
                    try
                    {
                        System.Collections.Generic.List<SpoofCatalog.Group> groups = SpoofCatalog.All();
                        for (int gi = 0; gi < groups.Count; gi++)
                        {
                            string[] ms = groups[gi].Models;
                            for (int mi = 0; mi < ms.Length; mi++)
                            {
                                if (string.Equals(ms[mi], targetName, StringComparison.OrdinalIgnoreCase))
                                {
                                    modelKnown = true; break;
                                }
                            }
                            if (modelKnown) break;
                        }
                    }
                    catch { }
                    if (!modelKnown)
                    {
                        ReplyFail(id, action, "VALIDATION_FAILED",
                                  "目标型号不在内置目录中，已拒绝执行：" + (targetName ?? "（空）"));
                        return;
                    }
                    if (string.IsNullOrEmpty(targetName)) targetName = "NVIDIA GeForce RTX 3080";
                    string rid = id;
                    string tgt = targetName;
                    LogInfo("应用伪装 → " + tgt);
                    Task.Run(new Action(delegate
                    {
                        bool ok = false;
                        try { ok = NativeCore.ApplyGpuRenameAsync(tgt, LogPower).Result; }
                        catch (Exception ex) { LogCrash("应用伪装失败", ex); }

                        if (ok)
                        {
                            LogInfo("伪装完成，回读注册表…");
                            ReplyOk(rid, action);
                        }
                        else
                        {
                            ReplyFail(rid, action, "DRIVER_REJECT", "伪装失败，详情见日志（旧版备份位于 ProgramData\\CMP90HX_Toolbox）");
                        }
                        // 名称变了，重新推一遍状态让界面同步
                        PostToPage(HardwareBridge.SpoofStateEvt());
                        PostToPage(HardwareBridge.DeviceSnapshotEvt());
                        // 伪装后必须重推显卡列表：列表里的 isSpoofed 是构建时的快照，
                        // 不重推界面就一直显示「伪装:否」，用户会以为没生效。
                        PostToPage(HardwareBridge.GpuListEvt());
                    }));
                    return;
                }
                else if (action == "spoof.restore")
                {
                    string rid = id;
                    LogInfo("还原型号名…");
                    Task.Run(new Action(delegate
                    {
                        bool ok = false;
                        try { ok = NativeCore.RestoreGpuTo90hxAsync(LogPower).Result; }
                        catch (Exception ex) { LogCrash("还原型号名失败", ex); }

                        if (ok) ReplyOk(rid, action);
                        else ReplyFail(rid, action, "DRIVER_REJECT", "还原失败，详情见日志");

                        PostToPage(HardwareBridge.SpoofStateEvt());
                        PostToPage(HardwareBridge.DeviceSnapshotEvt());
                        // 还原出厂名后必须重推显卡列表：列表里的 isSpoofed
                        // 是构建时快照，不重推会一直显示「伪装:是」。
                        PostToPage(HardwareBridge.GpuListEvt());
                    }));
                    return;
                }
                else if (action == "theme.set")
                {
                    string t = ExtractPayloadString(json, "theme");
                    _theme = (t == "dark") ? "dark" : "light";
                    AppSettings.Load();
                    AppSettings.SetTheme(_theme);
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
                        bool okE = false;
                        try { okE = PowerPresets.Apply("adaptive", secs <= 0 ? 8 : secs, LogPower); }
                        catch (Exception exE) { LogCrash("启用节能失败", exE); }
                        PostToPage(HardwareBridge.PowerStateEvt());
                        // 驱动拒绝写功耗墙时必须如实报失败，否则界面回「已启用」而频率没变
                        if (okE) ReplyOk(rid, action);
                        else ReplyFail(rid, action, "DRIVER_REJECT", "启用节能失败，驱动未接受设置，详见日志");
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
                else if (action == "logs.query")
                {
                    int limit = ExtractPayloadNumber(json, "limit");
                    int offset = ExtractPayloadNumber(json, "offset");
                    string level = ExtractPayloadString(json, "level");
                    if (limit <= 0) limit = 200;
                    if (offset < 0) offset = 0;
                    string rid = id;

                    // 必须离开 UI 线程：ReadLogTail 会整文件读入内存，
                    // 日志到几十万行时同步执行会把界面冻住，并且触发前端 15 秒超时，
                    // 超时后的迟到回执还会把同一批日志再追加一遍。
                    Task.Run(new Action(delegate
                    {
                        try
                        {
                            // 读尾部 offset+limit 行，再切出这一页——
                            // 原来完全忽略 offset，「加载更多」只是把最近 N 行重复插一遍。
                            System.Collections.Generic.List<string[]> tail =
                                Diagnostics.ReadLogTail(offset + limit, level);
                            int total = tail.Count;
                            int end = Math.Max(0, total - offset);
                            int start = Math.Max(0, end - limit);

                            StringBuilder sb = new StringBuilder(8192);
                            sb.Append("{\"offset\":").Append(offset)
                              .Append(",\"total\":").Append(total)
                              .Append(",\"items\":[");
                            for (int i = start; i < end; i++)
                            {
                                if (i > start) sb.Append(',');
                                sb.Append("{\"at\":").Append(HardwareBridge.Quote(tail[i][0]))
                                  .Append(",\"level\":").Append(HardwareBridge.Quote(tail[i][1]))
                                  .Append(",\"text\":").Append(HardwareBridge.Quote(tail[i][2])).Append('}');
                            }
                            sb.Append("]}");
                            // 记一行「本轮取的是哪一段」，用来证明 offset 真的生效：
                            // 只看界面行数会被前端去重骗过，只有这个区间能说明取到了更早的记录。
                            LogInfo("[日志] offset=" + offset + " limit=" + limit +
                                    " → 取第 " + start + "~" + (end - 1) + " 行 / 共 " + total + " 行" +
                                    (offset > 0 && start == 0 ? "（已到最早）" : ""));
                            ReplyDataJson(rid, action, sb.ToString());
                        }
                        catch (Exception ex)
                        {
                            LogCrash("读取日志失败", ex);
                            ReplyFail(rid, action, "INTERNAL", "读取日志失败：" + ex.Message);
                        }
                    }));
                    return;
                }
                else if (action == "logs.copy")
                {
                    string text = Diagnostics.ReadLogText();
                    if (string.IsNullOrEmpty(text))
                    {
                        ReplyFail(id, action, "INTERNAL", "没有可复制的日志内容");
                        return;
                    }
                    string rid = id;
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        try
                        {
                            System.Windows.Forms.Clipboard.SetText(text);
                            LogInfo("日志已复制到剪贴板（" + text.Length + " 字符）");
                            ReplyOk(rid, action);
                        }
                        catch (Exception ex)
                        {
                            ReplyFail(rid, action, "INTERNAL", "复制失败：" + ex.Message);
                        }
                    }));
                    return;
                }
                else if (action == "logs.export")
                {
                    // SaveFileDialog 是 WinForms 控件，必须在 STA 的 UI 线程上开。
                    // 原来放在 Task.Run（线程池 MTA）里 → ThreadStateException 被
                    // ShowSaveDialog 的 catch 吞掉 → 返回 null → 永远报「已取消导出」，
                    // 导出功能 100% 失效，还伪装成用户主动取消。
                    string target = ShowSaveDialog("hx-studio-log.txt");
                    if (string.IsNullOrEmpty(target))
                    {
                        ReplyFail(id, action, "CANCELLED", "已取消导出");
                        return;
                    }
                    string rid = id;
                    Task.Run(new Action(delegate
                    {
                        string okPath = Diagnostics.ExportLog(target);
                        if (string.IsNullOrEmpty(okPath))
                        {
                            ReplyFail(rid, action, "INTERNAL", "导出失败，请检查目标路径权限");
                            return;
                        }
                        // 带上 message，前端才会 toast 出「已导出到 X」；
                        // 原来只回 path，用户点完没有任何反馈。
                        ReplyDataJson(rid, action,
                            "{\"path\":" + HardwareBridge.Quote(okPath) +
                            ",\"message\":" + HardwareBridge.Quote("日志已导出：" + okPath) + "}");
                    }));
                    return;
                }
                else if (action == "logs.clear")
                {
                    if (!Diagnostics.ClearLog())
                    {
                        ReplyFail(id, action, "INTERNAL", "清空日志失败，文件可能被占用");
                        return;
                    }
                    LogInfo("日志已清空");
                }
                else if (action == "snapshot.create")
                {
                    string label = ExtractPayloadString(json, "label");
                    string rid = id;
                    Task.Run(new Action(delegate
                    {
                        string sid = Diagnostics.CreateSnapshot(label);
                        if (string.IsNullOrEmpty(sid))
                        {
                            ReplyFail(rid, action, "INTERNAL", "创建快照失败，请检查 ProgramData 写入权限");
                            return;
                        }
                        LogInfo("已创建系统快照: " + sid);
                        PostToPage(HardwareBridge.SnapshotsEvt());
                        ReplyDataJson(rid, action, "{\"snapshotId\":" + HardwareBridge.Quote(sid) + "}");
                    }));
                    return;
                }
                else if (action == "snapshot.delete")
                {
                    string snapId = ExtractPayloadString(json, "snapshotId");
                    if (!Diagnostics.DeleteSnapshot(snapId))
                    {
                        ReplyFail(id, action, "FILE_LOCKED", "快照不存在或无法删除");
                        return;
                    }
                    LogInfo("已删除系统快照: " + snapId);
                    PostToPage(HardwareBridge.SnapshotsEvt());
                }
                else if (action == "snapshot.restore")
                {
                    string snapId = JsonUnescape(ExtractPayloadString(json, "snapshotId"));
                    if (string.IsNullOrEmpty(snapId))
                    {
                        ReplyFail(id, action, "VALIDATION_FAILED", "缺少 snapshotId");
                        return;
                    }
                    string rid = id;
                    LogInfo("回滚快照: " + snapId);
                    // 要写 HKLM 驱动类键并逐项回读，不能占 UI 线程
                    Task.Run(new Action(delegate
                    {
                        Diagnostics.SnapshotRestoreResult r = null;
                        try { r = Diagnostics.RestoreSnapshot(snapId, LogPower); }
                        catch (Exception ex) { LogCrash("回滚快照失败", ex); }

                        if (r == null || !r.Ok)
                        {
                            string why = r == null ? "回滚未返回结果" : (r.Message ?? "未知原因");
                            LogInfo("回滚快照失败: " + why);
                            ReplyFail(rid, action, r == null ? "INTERNAL" : (r.Code ?? "INTERNAL"), why);
                            return;
                        }

                        LogInfo(r.Message);
                        // 驱动类描述变了：型号名/显卡列表必须重推，否则界面还显示旧名字
                        PostToPage(HardwareBridge.SpoofStateEvt());
                        PostToPage(HardwareBridge.DeviceSnapshotEvt());
                        PostToPage(HardwareBridge.GpuListEvt());
                        ReplyDataJson(rid, action, "{\"message\":" + HardwareBridge.Quote(r.Message) + "}");
                    }));
                    return;
                }
                else if (action == "settings.save")
                {
                    AppSettings.Load();
                    // 关键：只更新本次真正提交的字段。
                    // 之前 null 一律当空串写回，而前端只提交下载源，
                    // 于是每次保存都会把已配置的「更新清单地址」清空，
                    // 导致 update.download 永远报「未配置更新源」。
                    string proxy = ExtractPayloadString(json, "downloadProxy");
                    string upd = ExtractPayloadString(json, "updateManifestUrl");

                    if (proxy != null)
                    {
                        proxy = proxy.Trim();
                        if (proxy.Length > 0 && !proxy.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        {
                            ReplyFail(id, action, "INTERNAL", "加速前缀必须以 http(s):// 开头");
                            return;
                        }
                        AppSettings.DownloadProxy = proxy;
                    }
                    if (upd != null)
                    {
                        upd = upd.Trim();
                        if (upd.Length > 0 && !upd.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        {
                            ReplyFail(id, action, "INTERNAL", "更新清单地址必须以 http(s):// 开头");
                            return;
                        }
                        AppSettings.UpdateManifestUrl = upd;
                    }
                    if (!AppSettings.Save())
                    {
                        ReplyFail(id, action, "INTERNAL", "设置写入失败：" + AppSettings.LastError);
                        return;
                    }
                    string pdesc;
                    DlssPack.ResolveProxy(out pdesc);
                    LogInfo("设置已保存: 前缀=" + (string.IsNullOrEmpty(AppSettings.DownloadProxy) ? "自动" : AppSettings.DownloadProxy) +
                            " / 清单=" + (string.IsNullOrEmpty(AppSettings.UpdateManifestUrl) ? "未配置" : AppSettings.UpdateManifestUrl) +
                            " / 代理=" + pdesc);
                    PostToPage(HardwareBridge.SettingsStateEvt());
                }
                else if (action == "update.check")
                {
                    string cur = HardwareBridge.CurrentVersion();
                    AppSettings.Reload();   // 强制重读，避免运行时改了配置却读到旧缓存
                    string url = AppSettings.UpdateManifestUrl;
                    if (string.IsNullOrEmpty(url))
                    {
                        LogInfo("更新检查: v" + cur + "，未配置更新地址");
                        ReplyDataJson(id, action, "{\"currentVersion\":" + HardwareBridge.Quote(cur) +
                            ",\"latestVersion\":" + HardwareBridge.Quote(cur) +
                            ",\"hasUpdate\":false,\"configured\":false" +
                            ",\"message\":\"未配置更新地址，请在上方「下载源设置」里填写\"}");
                        return;
                    }
                    string rid = id;
                    Task.Run(new Action(delegate
                    {
                        string latest = UpdateChecker.FetchLatestVersion(url, LogPower);
                        if (string.IsNullOrEmpty(latest))
                        {
                            ReplyFail(rid, action, "NETWORK", "无法获取更新信息");
                            return;
                        }
                        bool hasNew = UpdateChecker.IsNewer(latest, cur);
                        LogInfo("更新检查: 当前 v" + cur + " / 最新 v" + latest + (hasNew ? "（有更新）" : "（已最新）"));
                        PostToPage(HardwareBridge.UpdateStateEvt(cur, latest, hasNew));
                        ReplyDataJson(rid, action, "{\"currentVersion\":" + HardwareBridge.Quote(cur) +
                            ",\"latestVersion\":" + HardwareBridge.Quote(latest) +
                            ",\"hasUpdate\":" + (hasNew ? "true" : "false") +
                            ",\"configured\":true}");
                    }));
                    return;
                }
                else if (action == "update.download")
                {
                    if (!TryEnterGroup(action, id))
                    {
                        ReplyFail(id, action, "BUSY", BusyMessage(action));
                        return;
                    }
                    string rid = id;
                    LogInfo("开始下载更新包…");
                    // 安装包动辄上百 MB，下载绝不能占 UI 线程——历史事故里同步执行直接闪退
                    Task.Run(new Action(delegate
                    {
                        UpdateInstaller.UpdateResult r = null;
                        try
                        {
                            r = UpdateInstaller.Download(LogPower, delegate (int pct, long read, long total)
                            {
                                // 进度按 prg 推给页面：percent=-1 表示无 Content-Length，显示不确定进度
                                string detail = total > 0
                                    ? (read / 1048576) + "/" + (total / 1048576) + " MB"
                                    : (read / 1048576) + " MB";
                                PostToPage("{\"kind\":\"prg\",\"id\":\"" + (rid == null ? "" : rid) +
                                           "\",\"action\":\"update.download\",\"payload\":{\"percent\":" + pct +
                                           ",\"readBytes\":" + read + ",\"totalBytes\":" + total +
                                           ",\"stage\":\"正在下载更新包\",\"detail\":" +
                                           HardwareBridge.Quote(detail) + "}}");
                            });
                        }
                        catch (Exception ex)
                        {
                            LogCrash("下载更新包异常", ex);
                            ReplyFail(rid, action, "INTERNAL", "下载更新包异常：" + ex.Message);
                            return;
                        }

                        if (r == null || !r.Ok)
                        {
                            LogInfo("更新包下载失败: " + (r == null ? "未返回结果" : r.Message));
                            ReplyFail(rid, action, r == null ? "INTERNAL" : r.Code,
                                      r == null ? "下载更新包未返回结果" : r.Message);
                            return;
                        }

                        // 应用按钮受前端 verified 严格门禁：不推这条 evt，按钮会一直是灰的
                        string cur = HardwareBridge.CurrentVersion();
                        PostToPage("{\"kind\":\"evt\",\"action\":\"update.state\",\"payload\":{" +
                                   "\"currentVersion\":" + HardwareBridge.Quote(cur) +
                                   ",\"latestVersion\":" + HardwareBridge.Quote(r.Version) +
                                   ",\"hasUpdate\":" + (UpdateChecker.IsNewer(r.Version, cur) ? "true" : "false") +
                                   ",\"verified\":true" +
                                   ",\"assetName\":" + HardwareBridge.Quote(r.AssetName) +
                                   ",\"sha256\":" + HardwareBridge.Quote(r.Sha256) +
                                   ",\"releaseNotes\":\"\"}}");
                        ReplyDataJson(rid, action, "{\"version\":" + HardwareBridge.Quote(r.Version) +
                                      ",\"package\":" + HardwareBridge.Quote(r.PackagePath) +
                                      ",\"sha256\":" + HardwareBridge.Quote(r.Sha256) + "}");
                        LogInfo("更新包就绪: " + r.PackagePath);
                    }));
                    return;
                }
                else if (action == "update.apply")
                {
                    if (!TryEnterGroup(action, id))
                    {
                        ReplyFail(id, action, "BUSY", BusyMessage(action));
                        return;
                    }
                    string rid = id;
                    LogInfo("准备应用更新…");
                    // 备份 exe、写脚本、起 cmd 都不能占 UI 线程
                    Task.Run(new Action(delegate
                    {
                        UpdateInstaller.UpdateResult r = null;
                        try { r = UpdateInstaller.Apply(LogPower); }
                        catch (Exception ex)
                        {
                            LogCrash("应用更新异常", ex);
                            ReplyFail(rid, action, "INTERNAL", "应用更新异常：" + ex.Message);
                            return;
                        }

                        if (r == null || !r.Ok)
                        {
                            LogInfo("应用更新失败: " + (r == null ? "未返回结果" : r.Message));
                            ReplyFail(rid, action, r == null ? "INTERNAL" : r.Code,
                                      r == null ? "应用更新未返回结果" : r.Message);
                            return;
                        }

                        ReplyDataJson(rid, action, "{\"restarting\":true,\"version\":" +
                                      HardwareBridge.Quote(r.Version) + "}");
                        // 回执要先让前端把 loading 放掉再退出；
                        // 立刻 Shutdown 会把这条消息掐死在 WebView2 的发送队列里。
                        try { System.Threading.Thread.Sleep(800); } catch { }
                        try
                        {
                            Dispatcher.BeginInvoke(new Action(delegate
                            {
                                try { Application.Current.Shutdown(); }
                                catch (Exception ex) { LogCrash("退出主程序失败", ex); }
                            }));
                        }
                        catch (Exception ex) { LogCrash("调度退出失败", ex); }
                    }));
                    return;
                }
                else if (action == "unlock.install")
                {
                    if (!TryEnterGroup(action, id))
                    {
                        ReplyFail(id, action, "BUSY", BusyMessage(action));
                        return;
                    }
                    // 必须先短路再放开 BCD 读取：通道 C 目前没接线，
                    // 落到下面会被当成 Mode1 去改本机 {bootmgr}。
                    string chGuard = ExtractPayloadString(json, "channel");
                    if (string.Equals(chGuard, "C", StringComparison.OrdinalIgnoreCase))
                    {
                        ReplyFail(id, action, "NOT_SUPPORTED",
                                  "U 盘引导通道尚未接入，请改用通道 A（UEFI 直启）或 B（Shell 链启）");
                        return;
                    }
                    NativeCore.BcdReadAllowed = true;
                    // channel: A=UEFI 直启(Mode1) / B=UEFI Shell 链启(Mode2)
                    string ch = ExtractPayloadString(json, "channel");
                    string mode = ExtractPayloadString(json, "mode");
                    bool useMode2 = string.Equals(ch, "B", StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(mode, "2", StringComparison.OrdinalIgnoreCase)
                                    || string.Equals(mode, "Mode2", StringComparison.OrdinalIgnoreCase);
                    LogInfo("安装解锁载荷: " + (useMode2 ? "Mode2 UEFI Shell" : "Mode1 UEFI 直启"));
                    string rid = id;
                    Task.Run(new Action(delegate
                    {
                        bool ok;
                        try
                        {
                            ok = useMode2
                                ? NativeCore.InstallPcieMode2Async(LogPower).Result
                                : NativeCore.InstallPcieMode1Async(LogPower).Result;
                        }
                        catch (Exception ex) { LogCrash("安装解锁载荷失败", ex); ok = false; }
                        // 只在成功时记账。
                        // 失败时写 0 是错的：失败可能发生在预清理就中止（此时机器仍处于
                        // 已解锁状态），写 0 会把「已解锁」翻成「未解锁」，比不写更糟。
                        // 开机路径会用 BCD 回读自我校正，不写也能恢复正确值。
                        if (ok) { try { NativeCore.MarkPcieUnlocked(true); } catch { } }

                        PostToPage(HardwareBridge.UnlockStateEvt());
                        PostToPage(HardwareBridge.GpuListEvt());   // PCIe 标志变了，列表要跟着刷新
                        if (ok)
                        {
                            // 带 message：这项操作的关键是「冷启动才生效」，
                            // 普通「操作完成」的绿字不够，用户会直接重启（热重启不生效）。
                            ReplyDataJson(rid, action,
                                "{\"message\":" + HardwareBridge.Quote(
                                    "载荷已写入。请完全关机（断电 10 秒以上）再开机生效，热重启不生效。") + "}");
                        }
                        // 原来用 NEED_REBOOT，而前端码表里它的标签是
                        // 「操作已提交，需重启系统生效」——成功语气，与「安装未完成」矛盾。
                        else ReplyFail(rid, action, "INTERNAL", "载荷安装未完成，详见日志");
                    }));
                    return;
                }
                else if (action == "unlock.rollback")
                {
                    if (!TryEnterGroup(action, id))
                    {
                        ReplyFail(id, action, "BUSY", BusyMessage(action));
                        return;
                    }
                    NativeCore.BcdReadAllowed = true;
                    LogInfo("回滚引导与解锁载荷…");
                    string rid = id;
                    Task.Run(new Action(delegate
                    {
                        bool ok = false;
                        try { ok = NativeCore.CleanAllPcieUnlocks(LogPower); }
                        catch (Exception ex) { LogCrash("回滚失败", ex); }
                        // 还原成功后清除状态：开机不读 BCD 也能显示「未解锁」
                        // 同样只在成功时写：回滚失败时机器可能仍是解锁状态，
                        // 写 0 会造成「显示未解锁、实际仍解锁」的反向误导。
                        if (ok) { try { NativeCore.MarkPcieUnlocked(false); } catch { } }

                        PostToPage(HardwareBridge.UnlockStateEvt());
                        PostToPage(HardwareBridge.GpuListEvt());   // PCIe 标志变了，列表要跟着刷新
                        if (ok) ReplyOk(rid, action);
                        else ReplyFail(rid, action, "INTERNAL", "回滚未完成，详见日志");
                    }));
                    return;
                }
                else if (action == "factory.reset")
                {
                    // 与 unlock.install / unlock.rollback 同组：都会改 BCD 与 ESP，
                    // 并发执行等于两串 bcdedit 交错，可以把 BCD 写坏。
                    if (!TryEnterGroup(action, id))
                    {
                        ReplyFail(id, action, "BUSY", BusyMessage(action));
                        return;
                    }
                    // confirmText 由前端二次确认弹窗填写，必须是完整大写 RESET
                    string confirm = ExtractPayloadString(json, "confirmText");
                    if (!string.Equals(confirm, "RESET", StringComparison.Ordinal))
                    {
                        ReplyFail(id, action, "VALIDATION_FAILED", "需要输入大写 RESET 确认");
                        return;
                    }

                    NativeCore.BcdReadAllowed = true;   // 下面要读/改 BCD
                    LogInfo("===== 开始恢复出厂设置 =====");
                    string rid = id;
                    Task.Run(new Action(delegate
                    {
                        ResetReport rep = new ResetReport();

                        // ---- 步骤 1：还原显卡型号名 ----
                        LogInfo("[恢复出厂 1/4] 还原显卡型号名…");
                        try
                        {
                            bool okName = NativeCore.RestoreGpuTo90hxAsync(LogPower).Result;
                            // 回读校验：该方法内部多处 catch{}，返回值不足以证明真的改了。
                            string desc = NativeCore.ReadGpuDriverDesc();
                            if (!okName)
                                rep.Fail("步骤1 显卡型号: 还原调用返回失败（详见上面的日志）");
                            else if (string.IsNullOrEmpty(desc))
                                rep.Fail("步骤1 显卡型号: 无法回读驱动类 DriverDesc，不能确认已还原");
                            else if (desc.IndexOf("CMP", StringComparison.OrdinalIgnoreCase) < 0)
                                rep.Fail("步骤1 显卡型号: 回读仍是 " + desc);
                            else
                                rep.Pass("步骤1 显卡型号已还原为 " + desc);
                        }
                        catch (Exception ex)
                        {
                            LogCrash("恢复出厂-还原显卡型号失败", ex);
                            rep.Fail("步骤1 显卡型号: " + ex.Message);
                        }

                        // ---- 步骤 2：清理全部 PCIe 引导载荷 ----
                        LogInfo("[恢复出厂 2/4] 清理全部 PCIe 引导载荷…");
                        try
                        {
                            bool okBcd = NativeCore.CleanAllPcieUnlocks(LogPower);
                            if (okBcd)
                            {
                                // 记账写 0：开机不读 BCD 也不会把「已解锁」显示出来
                                try { NativeCore.MarkPcieUnlocked(false); } catch { }
                                rep.Pass("步骤2 PCIe 引导载荷已清理（BCD 与 ESP 恢复官方状态）");
                            }
                            else
                            {
                                rep.Fail("步骤2 PCIe 引导载荷: 清理未完成（bcdedit 或 ESP 操作失败，详见上面的日志）");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogCrash("恢复出厂-清理引导载荷失败", ex);
                            rep.Fail("步骤2 PCIe 引导载荷: " + ex.Message);
                        }

                        // ---- 步骤 3：清理状态文件 ----
                        LogInfo("[恢复出厂 3/4] 清理本地状态文件…");
                        try
                        {
                            string[] files = new string[]
                            {
                                Path.Combine(NativeCore.GetConfigDirectory(), "pcie-unlocked.txt"),
                                Path.Combine(NativeCore.GetConfigDirectory(), "compat-state.txt"),
                                // 实际生效的解锁状态文件在 data\state 下，两处都要清
                                Path.Combine(NativeCore.GetDataDirectory(), "state", "pcie-unlocked.txt")
                            };
                            StringBuilder deleted = new StringBuilder();
                            StringBuilder delErr = new StringBuilder();
                            for (int i = 0; i < files.Length; i++)
                            {
                                string f = files[i];
                                try
                                {
                                    if (!File.Exists(f)) continue;
                                    File.Delete(f);
                                    // 两处可能都有 pcie-unlocked.txt，同一个名字只报一次
                                    string fn = Path.GetFileName(f);
                                    if (deleted.ToString().IndexOf(fn, StringComparison.Ordinal) < 0)
                                        deleted.Append(fn).Append(' ');
                                }
                                catch (Exception exDel)
                                {
                                    delErr.Append(Path.GetFileName(f)).Append(": ").Append(exDel.Message).Append("；");
                                }
                            }
                            if (delErr.Length > 0)
                                rep.Fail("步骤3 状态文件: 删除失败 " + delErr.ToString().TrimEnd('；'));
                            else
                                rep.Pass("步骤3 状态文件已清理（" +
                                         (deleted.Length > 0 ? "删除 " + deleted.ToString().Trim() : "本来就没有残留文件") + "）");
                        }
                        catch (Exception ex)
                        {
                            LogCrash("恢复出厂-清理状态文件失败", ex);
                            rep.Fail("步骤3 状态文件: " + ex.Message);
                        }

                        // ---- 步骤 4：删除省电计划任务 ----
                        LogInfo("[恢复出厂 4/4] 移除省电计划任务…");
                        try
                        {
                            if (!NativeCore.IsPowerSaverTaskInstalled())
                            {
                                rep.Pass("步骤4 省电计划任务: 本来就没安装，跳过");
                            }
                            else
                            {
                                NativeCore.UninstallPowerSaverTask(LogPower);
                                // 该方法的 schtasks 兜底分支无论成败都 return true，
                                // 必须重新查询确认，不能信返回值。
                                if (NativeCore.IsPowerSaverTaskInstalled())
                                    rep.Fail("步骤4 省电计划任务: 删除后仍能查询到该任务");
                                else
                                    rep.Pass("步骤4 省电计划任务已移除");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogCrash("恢复出厂-移除省电任务失败", ex);
                            rep.Fail("步骤4 省电计划任务: " + ex.Message);
                        }

                        // 型号名/引导/功耗状态都变过，整页重推
                        PostToPage(HardwareBridge.SpoofStateEvt());
                        PostToPage(HardwareBridge.DeviceSnapshotEvt());
                        PostToPage(HardwareBridge.GpuListEvt());
                        PostToPage(HardwareBridge.UnlockStateEvt());
                        PostToPage(HardwareBridge.PowerStateEvt());

                        string summary = rep.Summary();
                        if (rep.Failed.Count == 0)
                        {
                            LogInfo(summary);
                            ReplyDataJson(rid, action,
                                "{\"ok\":true,\"successCount\":" + rep.Ok.Count +
                                ",\"failedCount\":0,\"message\":" + HardwareBridge.Quote(summary) + "}");
                        }
                        else
                        {
                            // 还有步骤失败时绝不能回「重置成功」——
                            // 用户会以为机器已经回到出厂状态，实际没有。
                            LogInfo(summary);
                            ReplyFail(rid, action, "INTERNAL", summary);
                        }
                    }));
                    return;
                }
                else if (action == "dlss.checkUpdate")
                {
                    if (!TryEnterGroup(action, id))
                    {
                        ReplyFail(id, action, "BUSY", BusyMessage(action));
                        return;
                    }
                    LogInfo("检查 DLSS 5 资源包…");
                    string rid = id;
                    Task.Run(new Action(delegate
                    {
                        string pdesc;
                        DlssPack.ResolveProxy(out pdesc);
                        LogInfo("代理: " + pdesc);
                        string tag, assetName;
                        string url = DlssPack.ResolveLatestAsset(out tag, out assetName, LogPower);
                        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(tag))
                        {
                            ReplyFail(rid, action, "NETWORK", "解析最新版本失败");
                            return;
                        }
                        LogInfo("最新版本: " + tag + "（资产: " + assetName + "）");

                        string dir = NativeCore.GetDlssUnlockedDirectory();
                        string zip = Path.Combine(dir, "dlss-unlocked-standalone-" + tag + ".zip");
                        // 本地有这个文件不等于它完好：实测拿到过 3KB 的假 zip。
                        // 无效则删掉重新下载，避免一直卡在「已是该版本」。
                        if (File.Exists(zip) && !DlssPack.IsValidZip(zip))
                        {
                            long badLen = 0;
                            try { badLen = new FileInfo(zip).Length; } catch { }
                            try { File.Delete(zip); } catch { }
                            LogInfo("本地包无效（" + badLen + " 字节），已删除并重新下载");
                        }
                        if (File.Exists(zip))
                        {
                            string v = NativeCore.ValidateDlssUnlockedPackage(LogPower);
                            LogInfo("本地已是该版本（" + v + "）");
                            PostToPage(HardwareBridge.DlssStateEvt());
                            ReplyDataJson(rid, action, "{\"tag\":" + HardwareBridge.Quote(tag) + ",\"downloaded\":true}");
                            return;
                        }

                        // 下载进度实时推给界面
                        DlssPack.OnProgress = delegate (int pct, long read, long total)
                        {
                            PostToPage("{\"kind\":\"evt\",\"action\":\"dlss.download\",\"payload\":{\"percent\":" +
                                       pct + ",\"readBytes\":" + read + ",\"totalBytes\":" + total + "}}");
                        };
                        string mirror = ExtractPayloadString(json, "mirror");
                        LogInfo("开始下载 " + assetName + (string.IsNullOrEmpty(mirror) ? "（自动选源）" : "（源: " + mirror + "）"));
                        bool ok = DlssPack.DownloadToLegacyDir(url, tag, mirror, LogPower);
                        DlssPack.OnProgress = null;
                        string validation = ok ? NativeCore.ValidateDlssUnlockedPackage(LogPower) : "下载失败";
                        LogInfo("资源包: " + validation);
                        PostToPage(HardwareBridge.DlssStateEvt());
                        if (ok) ReplyDataJson(rid, action, "{\"tag\":" + HardwareBridge.Quote(tag) + ",\"downloaded\":true}");
                        else ReplyFail(rid, action, "NETWORK", "资源包下载失败");
                    }));
                    return;
                }
                else if (action == "dlss.preflight")
                {
                    string gameDir = ExtractPayloadString(json, "gameDir");
                    string proxyName = ExtractPayloadString(json, "proxyName");
                    bool reFw = json.IndexOf("\"injectReFramework\":true") >= 0;
                    string rid = id;

                    // 必须离开 UI 线程：上次同步执行（含全进程枚举）直接把界面卡死闪退
                    Task.Run(new Action(delegate
                    {
                        try
                        {
                            NativeCore.DlssPreflightResult r =
                                NativeCore.PreflightDlssDeploy(gameDir, proxyName, reFw);
                            if (r == null || r.Items == null)
                            {
                                ReplyFail(rid, action, "INTERNAL", "预检未返回结果");
                                return;
                            }
                            int failed = 0;
                            StringBuilder sb = new StringBuilder(400);
                            sb.Append("[");
                            for (int i = 0; i < r.Items.Count; i++)
                            {
                                NativeCore.DlssCheckItem it = r.Items[i];
                                if (!it.Ok) failed++;
                                if (i > 0) sb.Append(',');
                                sb.Append("{\"name\":").Append(HardwareBridge.Quote(it.Name))
                                  .Append(",\"ok\":").Append(it.Ok ? "true" : "false")
                                  .Append(",\"blocking\":").Append(it.Blocking ? "true" : "false")
                                  .Append(",\"detail\":").Append(HardwareBridge.Quote(it.Detail)).Append('}');
                            }
                            sb.Append("]");
                            LogInfo("预检: " + (failingText(failed)) + "（共 " + r.Items.Count + " 项）");
                            ReplyDataJson(rid, action, "{\"passed\":" + (r.Passed ? "true" : "false") +
                                          ",\"failedCount\":" + failed + ",\"items\":" + sb.ToString() + "}");
                        }
                        catch (Exception ex)
                        {
                            LogCrash("预检异常", ex);
                            ReplyFail(rid, action, "INTERNAL", "预检出错: " + ex.Message);
                        }
                    }));
                    return;
                }
                else if (action == "dlss.deploy")
                {
                    if (!TryEnterGroup(action, id))
                    {
                        ReplyFail(id, action, "BUSY", BusyMessage(action));
                        return;
                    }
                    string gameDir = ExtractPayloadString(json, "gameDir");
                    if (string.IsNullOrEmpty(gameDir))
                    {
                        ReplyFail(id, action, "CHECKSUM_MISMATCH", "请先选择游戏目录");
                        return;
                    }
                    // 勾选项决定部署哪些组件；缺省时只装内置帧生成
                    bool doFrame = json.IndexOf("\"frame\":false") < 0;
                    bool doUnlock = json.IndexOf("\"unlock\":true") >= 0;
                    if (!doFrame && !doUnlock)
                    {
                        ReplyFail(id, action, "CHECKSUM_MISMATCH", "请至少勾选一个组件");
                        return;
                    }

                    string proxyName = ExtractPayloadString(json, "proxyName");
                    bool reFw = json.IndexOf("\"injectReFramework\":true") >= 0;
                    string opt = ExtractPayloadString(json, "optLevel");
                    string maxFrames = ExtractPayloadString(json, "maxFrames");
                    string engine = ExtractPayloadString(json, "engine");
                    string preset = ExtractPayloadString(json, "preset");
                    string srMode = ExtractPayloadString(json, "srMode");

                    LogInfo("部署 → " + gameDir + "（" +
                            (doFrame ? "帧生成" : "") + (doFrame && doUnlock ? "+" : "") +
                            (doUnlock ? "DLSS解锁" : "") + "）");
                    string rid = id;
                    Task.Run(new Action(delegate
                    {
                        bool ok = true;
                        if (doFrame)
                        {
                            bool f = false;
                            try { f = NativeCore.InjectDlssG(gameDir, opt, maxFrames, reFw, proxyName, engine, preset, srMode, LogPower); }
                            catch (Exception ex) { LogCrash("部署帧生成失败", ex); }
                            if (!f) ok = false;
                        }
                        if (ok && doUnlock)
                        {
                            bool u = false;
                            try { u = NativeCore.DeployDlssUnlockedToGame(gameDir, LogPower); }
                            catch (Exception ex) { LogCrash("部署 DLSS 解锁失败", ex); }
                            if (!u) ok = false;
                        }
                        PostToPage(HardwareBridge.DlssStateEvt());
                        if (ok) ReplyOk(rid, action);
                        else ReplyFail(rid, action, "FILE_LOCKED", "部署未全部成功，详见日志");
                    }));
                    return;
                }
                else if (action == "dlss.remove")
                {
                    if (!TryEnterGroup(action, id))
                    {
                        ReplyFail(id, action, "BUSY", BusyMessage(action));
                        return;
                    }
                    string gd = ExtractPayloadString(json, "gameDir");
                    if (string.IsNullOrEmpty(gd))
                    {
                        ReplyFail(id, action, "CHECKSUM_MISMATCH", "请先选择游戏目录");
                        return;
                    }
                    LogInfo("移除补丁 → " + gd);
                    string rid = id;
                    Task.Run(new Action(delegate
                    {
                        bool ok = false;
                        try { ok = NativeCore.RemoveDlssG(gd, LogPower); }
                        catch (Exception ex) { LogCrash("移除帧生成失败", ex); }

                        // DLSS5（OptiScaler 那一套）没有内置移除函数，
                        // 这里按已知文件名把它们改成 .hxbak 备份，避免硬删。
                        try
                        {
                            string[] unlockFiles = new string[] {
                                "dxgi.dll", "OptiScaler.ini", "nvngx.dll_dlssnr.dll",
                                "renodx-dlss5.addon64", "nvngx_dlss.dll", "nvngx_dlssg.dll"
                            };
                            int moved = 0;
                            for (int i = 0; i < unlockFiles.Length; i++)
                            {
                                string p = Path.Combine(gd, unlockFiles[i]);
                                if (!File.Exists(p)) continue;
                                try
                                {
                                    string bak = p + ".hxbak";
                                    if (File.Exists(bak))
                                    {
                                        // 已经有备份了：说明 p 是「打过补丁的文件」，bak 才是原件。
                                        // 绝对不能删 bak 再 Move —— 那等于用补丁文件覆盖用户原件，
                                        // 反复移除几次原始 DLL 就永久没了。
                                        string extra = p + ".hxbak.new";
                                        try { if (File.Exists(extra)) File.Delete(extra); } catch { }
                                        File.Move(p, extra);
                                        moved++;
                                        LogPower("已移出补丁文件 " + unlockFiles[i] +
                                                 "（原件备份 .hxbak 保留不动）", false);
                                    }
                                    else
                                    {
                                        File.Move(p, bak);
                                        moved++;
                                        LogPower("已备份 DLSS5 文件: " + unlockFiles[i] + " → .hxbak", false);
                                    }
                                }
                                catch (Exception ex2) { LogPower("备份 " + unlockFiles[i] + " 失败: " + ex2.Message, true); }
                            }
                            if (moved == 0) LogPower("未发现 DLSS5 文件（可能未部署过）", false);
                        }
                        catch (Exception ex3) { LogCrash("清理 DLSS5 文件失败", ex3); }
                        PostToPage(HardwareBridge.DlssStateEvt());
                        if (ok) ReplyOk(rid, action);
                        else ReplyFail(rid, action, "FILE_LOCKED", "移除未完成，详见日志");
                    }));
                    return;
                }
                else if (action == "unlock.selectCard")
                {
                    // 只是切换界面上的显卡卡片，属于前端选择态；
                    // 但命令仍要应答，否则会掉进「尚未接入新版界面」兜底。
                    string cardSel = ExtractPayloadString(json, "card");
                    LogInfo("选择显卡: " + (string.IsNullOrEmpty(cardSel) ? "90HX" : cardSel));
                    PostToPage(HardwareBridge.UnlockStateEvt());
                    // 必须回执：页面的 sendCmd 收到同 id 的 res 才会解除按钮的
                    // disabled + is-loading 状态，否则整个界面会永远卡在等待。
                    ReplyOk(id, action);
                    return;
                }
                else if (action == "unlock.toolkit")
                {
                    // 40HX 走外置工具：先解压 toolkit，再启动对应 exe
                    string card = ExtractPayloadString(json, "card");
                    string op = ExtractPayloadString(json, "op");
                    if (string.IsNullOrEmpty(card)) card = "40HX";
                    if (string.IsNullOrEmpty(op)) op = "check";

                    string key = string.Equals(card, "50HX", StringComparison.OrdinalIgnoreCase) ? "50hx" : "40hx";
                    string exeName = string.Equals(op, "install", StringComparison.OrdinalIgnoreCase) ? "40HXInstaller.exe"
                                   : string.Equals(op, "uninstall", StringComparison.OrdinalIgnoreCase) ? "40HXUninstaller.exe"
                                   : "40HXCheck.exe";

                    if (key == "50hx")
                    {
                        ReplyFail(id, action, "NOT_SUPPORTED", "50HX 暂未验证解锁");
                        return;
                    }

                    string rid = id;
                    Task.Run(new Action(delegate
                    {
                        try
                        {
                            string dir = NativeCore.EnsureToolkitExtracted("40hx");
                            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                            {
                                ReplyFail(rid, action, "INTERNAL", "工具解压失败");
                                return;
                            }
                            string exe = Path.Combine(dir, exeName);
                            if (!File.Exists(exe))
                            {
                                LogInfo("工具不存在: " + exeName + "，目录: " + dir);
                                // 只回文件名等于没说：用户需要知道去哪找、为什么没有
                                ReplyFail(rid, action, "FILE_NOT_FOUND",
                                          "未找到 " + exeName + "（应为 " + dir + "\\" + exeName +
                                          "）。可能是内置资源包缺失或不完整。");
                                return;
                            }

                            LogInfo("启动外置工具: " + exeName + "（需要管理员权限，会弹 UAC）");
                            System.Diagnostics.ProcessStartInfo psi =
                                new System.Diagnostics.ProcessStartInfo(exe);
                            psi.WorkingDirectory = dir;
                            psi.UseShellExecute = true;
                            psi.Verb = "runas";     // 安装器要写驱动/引导，必须提权
                            System.Diagnostics.Process.Start(psi);
                            ReplyOk(rid, action);
                        }
                        catch (Exception ex)
                        {
                            LogCrash("启动外置工具失败", ex);
                            ReplyFail(rid, action, "INTERNAL", ex.Message);
                        }
                    }));
                    return;
                }
                else if (action == "dual.scanGames")
                {
                    string drive = ExtractPayloadString(json, "drive");
                    string scope = string.IsNullOrEmpty(drive) ? "全部磁盘 + Steam/Epic" : drive;
                    LogInfo("扫描游戏: " + scope);
                    string rid = id;
                    Task.Run(new Action(delegate
                    {
                        System.Collections.Generic.List<GameScanner.GameEntry> games = null;
                        string scanErr = null;
                        try { games = GameScanner.Scan(drive); }
                        catch (Exception ex)
                        {
                            scanErr = ex.Message;
                            LogCrash("扫描游戏目录失败", ex);
                        }

                        // 扫描抛异常时绝不能回「扫描完成：0 个」——
                        // 用户会以为机器上真的没装游戏，而实际是盘掉线/权限不足。
                        if (games == null)
                        {
                            // 失败路径不推 DualStateEvt：它会去枚举注册表「图形首选项」，
                            // 而这次扫描根本没成功，刷新状态纯属浪费。
                            ReplyFail(rid, action, "INTERNAL",
                                      "扫描失败：" + (scanErr ?? "未知原因") + "（请检查磁盘是否在线、是否有读取权限）");
                            return;
                        }

                        for (int i = 0; i < games.Count && i < 60; i++)
                            LogInfo("  发现 " + games[i].Source + " 游戏: " + games[i].Name + " → " + games[i].Path);

                        LogInfo("扫描完成: " + games.Count + " 个");
                        PostToPage(HardwareBridge.DualStateEvt());
                        string scanMsg = games.Count > 0
                            ? "扫描完成：发现 " + games.Count + " 个游戏（绑定功能尚未接入）"
                            : "扫描完成：未发现游戏（可能未安装，或安装路径不在扫描范围内）";
                        ReplyDataJson(rid, action, "{\"count\":" + games.Count +
                                ",\"message\":" + HardwareBridge.Quote(scanMsg) + "}");
                    }));
                    return;
                }
                else if (action == "dual.bind" || action == "dual.unbind")
                {
                    bool isBind = (action == "dual.bind");
                    // 前端用 postMessage(object)，WebMessageAsJson 给出的是原始 JSON 文本：
                    // Windows 路径里的 \ 在文本里是 \\，不还原就会把双反斜杠当成路径本体
                    // 写进注册表值名（界面回显也会变成双斜杠）。
                    string exePath = JsonUnescape(ExtractPayloadString(json, "exePath"));
                    if (string.IsNullOrEmpty(exePath) || exePath.Trim().Length == 0)
                    {
                        ReplyFail(id, action, "VALIDATION_FAILED",
                                  isBind ? "请提供要绑定的程序路径（exePath）" : "请提供要解绑的程序路径（exePath）");
                        return;
                    }
                    string rid = id;
                    Task.Run(new Action(delegate
                    {
                        NativeCore.GpuBindingResult r = null;
                        try
                        {
                            r = isBind
                                ? NativeCore.BindAppToHighPerformanceGpu(exePath, LogPower)
                                : NativeCore.UnbindAppFromGpuPreference(exePath, LogPower);
                        }
                        catch (Exception ex) { LogCrash(isBind ? "绑定高性能 GPU 失败" : "解除 GPU 绑定失败", ex); }

                        if (r == null || !r.Ok)
                        {
                            string why = r == null ? "未返回结果" : (r.Error ?? "未知原因");
                            LogInfo((isBind ? "绑定" : "解绑") + "失败: " + why);
                            // 错误码由 NativeCore 给出：路径/文件问题 VALIDATION_FAILED，权限 NO_ADMIN，其余 INTERNAL
                            string code = (r != null && !string.IsNullOrEmpty(r.Code)) ? r.Code : "INTERNAL";
                            ReplyFail(rid, action, code, (isBind ? "绑定失败：" : "解绑失败：") + why);
                            return;
                        }

                        // 名单与计数必须同源：页面列表读的是 dual.state 里的 bindings，
                        // 只回 count 不推事件会出现「共 3 款」+「暂无绑定游戏」自相矛盾。
                        PostToPage(HardwareBridge.DualStateEvt());

                        string name = SafeFileName(exePath);
                        string msg;
                        if (isBind)
                        {
                            msg = (r.Existed ? "已更新 " : "已绑定 ") + name +
                                  " → 高性能 GPU（GpuPreference=2），当前共 " + r.Count + " 个绑定";
                        }
                        else if (r.Existed)
                        {
                            msg = "已解除 " + name + " 的绑定，当前共 " + r.Count + " 个绑定";
                        }
                        else
                        {
                            msg = name + " 本来就没有绑定高性能 GPU（当前共 " + r.Count + " 个绑定）";
                        }
                        if (r.OtherUserFailures > 0)
                            msg += "；另有 " + r.OtherUserFailures + " 个其它用户配置" +
                                   (isBind ? "未能写入" : "无法访问、未做处理") + "（不影响当前用户）";
                        LogInfo(msg);
                        ReplyDataJson(rid, action,
                            "{\"count\":" + r.Count + ",\"message\":" + HardwareBridge.Quote(msg) + "}");
                    }));
                    return;
                }
                else if (action == "overlay.unlock")
                {
                    LogInfo("解锁覆盖层…");
                    string rid = id;
                    Task.Run(new Action(delegate
                    {
                        // 硬性前置：没有备份就不许打补丁。
                        // 之前就是备份名对不上还照样改，结果改完无法还原。
                        int backed = Diagnostics.BackupOverlayFiles(LogPower);
                        if (backed == 0 && !Diagnostics.HasOverlayBackup())
                        {
                            LogInfo("未找到可备份的覆盖层文件，已中止解锁");
                            ReplyFail(rid, action, "FILE_LOCKED", "找不到 NVIDIA App 前端文件，未做任何修改");
                            return;
                        }
                        bool ok = false;
                        try { ok = NativeCore.UnlockNvAppOverlayAsync(LogPower).Result; }
                        catch (Exception ex) { LogCrash("解锁覆盖层失败", ex); }

                        if (ok) ReplyOk(rid, action);
                        else ReplyFail(rid, action, "NOT_SUPPORTED", "覆盖层解锁失败，详见日志（可能未安装 NVIDIA App）");
                        PostToPage(HardwareBridge.OverlayStateEvt()); // overlay.state
                    }));
                    return;
                }
                else if (action == "overlay.restore")
                {
                    LogInfo("还原覆盖层…");
                    string rid = id;
                    Task.Run(new Action(delegate
                    {
                        int mine = Diagnostics.RestoreOverlayFiles(LogPower);
                        if (mine > 0)
                        {
                            LogInfo("已从 .hxbak 还原 " + mine + " 个文件");
                            PostToPage(HardwareBridge.OverlayStateEvt());
                            ReplyOk(rid, action);
                            return;
                        }
                        LogInfo("没有 .hxbak 备份，走官方备份还原…");
                        bool ok = false;
                        try { ok = NativeCore.RestoreNvAppOverlayAsync(LogPower).Result; }
                        catch (Exception ex) { LogCrash("还原覆盖层失败", ex); }

                        if (ok) ReplyOk(rid, action);
                        else ReplyFail(rid, action, "FILE_LOCKED", "还原失败，NVIDIA App 可能正在运行");
                        PostToPage(HardwareBridge.OverlayStateEvt());
                    }));
                    return;
                }
                else if (action == "overlay.cleanCache")
                {
                    string rid = id;
                    Task.Run(new Action(delegate
                    {
                        long freed = 0;
                        int count = 0;
                        string detail = Diagnostics.CleanGpuCache(out freed, out count);
                        LogInfo("已清理 GPU 缓存: " + count + " 个文件 / " + (freed / 1024.0 / 1024.0).ToString("0.0") + " MB （" + detail + "）");
                        ReplyDataJson(rid, action, "{\"freedBytes\":" + freed + ",\"fileCount\":" + count +
                            ",\"message\":" + HardwareBridge.Quote(
                                "已清理 " + count + " 个文件，释放 " + (freed / 1048576.0).ToString("0.0") + " MB") + "}");
                    }));
                    return;
                }
                else if (action == "unlock.scan")
                {
                    // 用户显式重扫时才允许读 BCD（调用 bcdedit 会有控制台闪窗）
                    NativeCore.BcdReadAllowed = true;
                    LogInfo("重扫引导状态…");
                    Task.Run(new Action(delegate
                    {
                        PostToPage(HardwareBridge.UnlockStateEvt());
                        PostToPage(HardwareBridge.GpuListEvt());   // PCIe 标志变了，列表要跟着刷新
                        ReplyOk(id, action);
                    }));
                    return;
                }
                else if (action == "app.diag")
                {
                    // 页面诊断：把页面侧真实状态原样写进日志，用于定位“界面看不到数据”
                    LogInfo("[DIAG] " + json);
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
                else if (action == "unlock.toolkit")
                {
                    // 兜底再判一次：主链上的分支若被前面的 return 挡住，
                    // 这里仍能处理，避免误报「尚未接入新版界面」。
                    string card2 = ExtractPayloadString(json, "card");
                    string op2 = ExtractPayloadString(json, "op");
                    if (string.IsNullOrEmpty(op2)) op2 = "check";
                    if (string.Equals(card2, "50HX", StringComparison.OrdinalIgnoreCase))
                    {
                        ReplyFail(id, action, "NOT_SUPPORTED", "50HX 暂未验证解锁");
                        return;
                    }
                    string rid3 = id;
                    Task.Run(new Action(delegate
                    {
                        try
                        {
                            string dir3 = NativeCore.EnsureToolkitExtracted("40hx");
                            string exe3 = string.Equals(op2, "install", StringComparison.OrdinalIgnoreCase)
                                ? "40HXInstaller.exe"
                                : (string.Equals(op2, "uninstall", StringComparison.OrdinalIgnoreCase)
                                    ? "40HXUninstaller.exe" : "40HXCheck.exe");
                            string full3 = Path.Combine(dir3, exe3);
                            if (!File.Exists(full3))
                            {
                                LogInfo("工具不存在: " + exe3 + "，目录: " + dir3);
                                ReplyFail(rid3, action, "FILE_LOCKED", "未找到 " + exe3);
                                return;
                            }
                            LogInfo("启动外置工具: " + exe3 + "（会弹 UAC）");
                            System.Diagnostics.ProcessStartInfo psi3 =
                                new System.Diagnostics.ProcessStartInfo(full3);
                            psi3.WorkingDirectory = dir3;
                            psi3.UseShellExecute = true;
                            psi3.Verb = "runas";
                            System.Diagnostics.Process.Start(psi3);
                            ReplyOk(rid3, action);
                        }
                        catch (Exception ex)
                        {
                            LogCrash("启动外置工具失败", ex);
                            ReplyFail(rid3, action, "INTERNAL", ex.Message);
                        }
                    }));
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
                // 兜底回执：宁可让用户看到一个明确的错误，也不要让按钮静默卡死。
                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(action))
                {
                    try { ReplyFail(id, action, "INTERNAL", ex.Message); } catch { }
                }
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
            ExitGroup(action, id);
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
            ExitGroup(action, id);
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
            // 与 ReplyOk / ReplyFail / ReplyDataJson 一致：任何回执都必须释放组锁。
            // 这里之前漏了——只要将来有组内命令走这条路，锁就会永久占住。
            ExitGroup(action, id);
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

        /// <summary>带自定义 data 的应答（data 已是合法 JSON 片段）。</summary>
        private void ReplyDataJson(string id, string action, string dataJson)
        {
            // 必须在这里也释放组锁：下载类命令成功时走的正是 ReplyDataJson，
            // 漏掉它会导致 update.download 成功后 update.apply 永远 BUSY。
            ExitGroup(action, id);
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(delegate { ReplyDataJson(id, action, dataJson); }));
                return;
            }
            try
            {
                if (_webView == null || _webView.CoreWebView2 == null) return;
                StringBuilder sb = new StringBuilder(512);
                sb.Append("{\"kind\":\"res\",\"id\":\"").Append(id == null ? string.Empty : id)
                  .Append("\",\"action\":\"").Append(action)
                  .Append("\",\"payload\":{\"ok\":true,\"data\":")
                  .Append(dataJson).Append("}}");
                _webView.CoreWebView2.PostWebMessageAsJson(sb.ToString());
            }
            catch (Exception ex)
            {
                LogCrash("回复数据消息出错", ex);
            }
        }

        private static string failingText(int failed)
        {
            return failed == 0 ? "全部通过" : (failed + " 项未通过");
        }

        /// <summary>
        /// 出厂重置的逐步结果。成功/失败分开记，
        /// 最后统一拼成「成功 N 步 / 失败 M 步 + 明细」，
        /// 避免各步骤自己回一条半截消息、用户看不到全貌。
        /// </summary>
        private sealed class ResetReport
        {
            public readonly System.Collections.Generic.List<string> Ok =
                new System.Collections.Generic.List<string>();
            public readonly System.Collections.Generic.List<string> Failed =
                new System.Collections.Generic.List<string>();

            public void Pass(string detail) { Ok.Add(detail); }
            public void Fail(string detail) { Failed.Add(detail); }

            public string Summary()
            {
                StringBuilder sb = new StringBuilder(320);
                sb.Append("恢复出厂结果：成功 ").Append(Ok.Count)
                  .Append(" 步，失败 ").Append(Failed.Count).Append(" 步。");
                for (int i = 0; i < Ok.Count; i++)
                    sb.Append(" [成功] ").Append(Ok[i]).Append('；');
                for (int i = 0; i < Failed.Count; i++)
                    sb.Append(" [失败] ").Append(Failed[i]).Append('；');
                return sb.ToString().TrimEnd('；');
            }
        }

        /// <summary>
        /// 还原 JSON 字符串字面量里的转义。
        /// WebMessageAsJson 给出的是原始 JSON 文本，Windows 路径里的反斜杠在文本里是 "\\"；
        /// 不还原就把双反斜杠当成路径本体（写进注册表值名后会一直带着双斜杠）。
        /// </summary>
        private static string JsonUnescape(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('\\') < 0) return s;

            StringBuilder sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c != '\\' || i + 1 >= s.Length)
                {
                    sb.Append(c);
                    continue;
                }
                char n = s[++i];
                switch (n)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        int code;
                        if (i + 4 < s.Length &&
                            int.TryParse(s.Substring(i + 1, 4),
                                System.Globalization.NumberStyles.HexNumber,
                                System.Globalization.CultureInfo.InvariantCulture, out code))
                        {
                            sb.Append((char)code);
                            i += 4;
                        }
                        else sb.Append('u');
                        break;
                    default: sb.Append(n); break;
                }
            }
            return sb.ToString();
        }

        /// <summary>取文件名用于提示；路径异常时退回原文，不让拼消息把整个操作搞挂。</summary>
        private static string SafeFileName(string path)
        {
            try { return Path.GetFileName(path); }
            catch { return path; }
        }

        private string ShowSaveDialog(string defaultName)
        {
            try
            {
                using (System.Windows.Forms.SaveFileDialog dlg = new System.Windows.Forms.SaveFileDialog())
                {
                    dlg.Title = "导出日志";
                    dlg.FileName = defaultName;
                    dlg.Filter = "文本文件 (*.txt)|*.txt|日志文件 (*.log)|*.log|所有文件 (*.*)|*.*";
                    if (dlg.ShowDialog(OwnerForm) != System.Windows.Forms.DialogResult.OK) return null;
                    return dlg.FileName;
                }
            }
            catch (Exception ex)
            {
                LogCrash("打开保存对话框失败", ex);
                return null;
            }
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

        /// <summary>
        /// 原生对话框的 owner 窗口。
        /// 不指定 owner 时对话框可能出现在主窗口之后，WPF 视觉上完全没变化，
        /// 用户会判定"卡死了"；指定后对话框始终挂在主窗口上方、任务栏归属正确。
        /// </summary>
        private System.Windows.Forms.IWin32Window OwnerForm
        {
            get
            {
                try
                {
                    IntPtr h = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                    return h == IntPtr.Zero ? null : new Win32Owner(h);
                }
                catch { return null; }
            }
        }

        private sealed class Win32Owner : System.Windows.Forms.IWin32Window
        {
            private readonly IntPtr _h;
            public Win32Owner(IntPtr h) { _h = h; }
            public IntPtr Handle { get { return _h; } }
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
                        return (dlg.ShowDialog(OwnerForm) == System.Windows.Forms.DialogResult.OK) ? dlg.FileName : null;
                    }
                }
                using (System.Windows.Forms.FolderBrowserDialog dlg2 = new System.Windows.Forms.FolderBrowserDialog())
                {
                    dlg2.Description = "选择目录";
                    return (dlg2.ShowDialog(OwnerForm) == System.Windows.Forms.DialogResult.OK) ? dlg2.SelectedPath : null;
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
