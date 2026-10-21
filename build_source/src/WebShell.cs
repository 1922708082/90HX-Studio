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
                else if (action == "spoof.apply")
                {
                    string targetName = ExtractPayloadString(json, "targetName");
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
                else if (action == "logs.query")
                {
                    int limit = ExtractPayloadNumber(json, "limit");
                    string level = ExtractPayloadString(json, "level");
                    System.Collections.Generic.List<string[]> rows = Diagnostics.ReadLogTail(limit, level);
                    StringBuilder sb = new StringBuilder(8192);
                    sb.Append("{\"items\":[");
                    for (int i = 0; i < rows.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        sb.Append("{\"at\":").Append(HardwareBridge.Quote(rows[i][0]))
                          .Append(",\"level\":").Append(HardwareBridge.Quote(rows[i][1]))
                          .Append(",\"text\":").Append(HardwareBridge.Quote(rows[i][2])).Append('}');
                    }
                    sb.Append("]}");
                    ReplyDataJson(id, action, sb.ToString());
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
                    string rid = id;
                    Task.Run(new Action(delegate
                    {
                        string target = ShowSaveDialog("hx-studio-log.txt");
                        if (string.IsNullOrEmpty(target))
                        {
                            ReplyFail(rid, action, "CANCELLED", "已取消导出");
                            return;
                        }
                        string okPath = Diagnostics.ExportLog(target);
                        if (string.IsNullOrEmpty(okPath))
                        {
                            ReplyFail(rid, action, "INTERNAL", "导出失败，请检查目标路径权限");
                            return;
                        }
                        ReplyDataJson(rid, action, "{\"path\":" + HardwareBridge.Quote(okPath) + "}");
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
                else if (action == "settings.save")
                {
                    AppSettings.Load();
                    string proxy = ExtractPayloadString(json, "downloadProxy");
                    string upd = ExtractPayloadString(json, "updateManifestUrl");
                    if (proxy == null) proxy = "";
                    if (upd == null) upd = "";
                    proxy = proxy.Trim();
                    upd = upd.Trim();
                    if (proxy.Length > 0 && !proxy.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        ReplyFail(id, action, "INTERNAL", "加速前缀必须以 http(s):// 开头");
                        return;
                    }
                    AppSettings.DownloadProxy = proxy;
                    AppSettings.UpdateManifestUrl = upd;
                    AppSettings.Save();
                    LogInfo("下载源已保存: 前缀=" + (proxy.Length == 0 ? "直连" : proxy) +
                            " / 更新地址=" + (upd.Length == 0 ? "未配置" : upd));
                    PostToPage(HardwareBridge.SettingsStateEvt());
                }
                else if (action == "update.check")
                {
                    string cur = HardwareBridge.CurrentVersion();
                    AppSettings.Load();
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
                else if (action == "unlock.install")
                {
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

                        PostToPage(HardwareBridge.UnlockStateEvt());
                        if (ok) ReplyOk(rid, action);
                        else ReplyFail(rid, action, "NEED_REBOOT", "载荷安装未完成，详见日志");
                    }));
                    return;
                }
                else if (action == "unlock.rollback")
                {
                    LogInfo("回滚引导与解锁载荷…");
                    string rid = id;
                    Task.Run(new Action(delegate
                    {
                        bool ok = false;
                        try { ok = NativeCore.CleanAllPcieUnlocks(LogPower); }
                        catch (Exception ex) { LogCrash("回滚失败", ex); }

                        PostToPage(HardwareBridge.UnlockStateEvt());
                        if (ok) ReplyOk(rid, action);
                        else ReplyFail(rid, action, "INTERNAL", "回滚未完成，详见日志");
                    }));
                    return;
                }
                else if (action == "dlss.checkUpdate")
                {
                    LogInfo("检查 DLSS 5 资源包…");
                    string rid = id;
                    Task.Run(new Action(delegate
                    {
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

                        string mirror = ExtractPayloadString(json, "mirror");
                        LogInfo("开始下载 " + assetName + (string.IsNullOrEmpty(mirror) ? "（自动选源）" : "（源: " + mirror + "）"));
                        bool ok = DlssPack.DownloadToLegacyDir(url, tag, mirror, LogPower);
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
                    NativeCore.DlssPreflightResult r =
                        NativeCore.PreflightDlssDeploy(gameDir, proxyName, reFw);
                    int failed = 0;
                    for (int i = 0; i < r.Items.Count; i++)
                        if (!r.Items[i].Ok) failed++;
                    LogInfo("预检: " + (r.Passed ? "通过" : failed + " 项未通过"));
                    ReplyDataJson(id, action, "{\"passed\":" + (r.Passed ? "true" : "false") +
                                  ",\"failedCount\":" + failed + "}");
                    return;
                }
                else if (action == "dlss.deploy")
                {
                    string gameDir = ExtractPayloadString(json, "gameDir");
                    if (string.IsNullOrEmpty(gameDir))
                    {
                        ReplyFail(id, action, "CHECKSUM_MISMATCH", "请先选择游戏目录");
                        return;
                    }
                    string proxyName = ExtractPayloadString(json, "proxyName");
                    bool reFw = json.IndexOf("\"injectReFramework\":true") >= 0;
                    string opt = ExtractPayloadString(json, "optLevel");
                    string maxFrames = ExtractPayloadString(json, "maxFrames");
                    string engine = ExtractPayloadString(json, "engine");
                    string preset = ExtractPayloadString(json, "preset");
                    string srMode = ExtractPayloadString(json, "srMode");
                    LogInfo("部署 DLSS → " + gameDir);
                    string rid = id;
                    Task.Run(new Action(delegate
                    {
                        bool ok = NativeCore.InjectDlssG(gameDir, opt, maxFrames, reFw,
                                                          proxyName, engine, preset, srMode, LogPower);
                        PostToPage(HardwareBridge.DlssStateEvt());
                        if (ok) ReplyOk(rid, action);
                        else ReplyFail(rid, action, "FILE_LOCKED", "部署失败，详见日志");
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
                        try { games = GameScanner.Scan(drive); }
                        catch (Exception ex) { LogCrash("扫描游戏目录失败", ex); }
                        if (games == null) games = new System.Collections.Generic.List<GameScanner.GameEntry>();

                        for (int i = 0; i < games.Count && i < 60; i++)
                            LogInfo("  发现 " + games[i].Source + " 游戏: " + games[i].Name + " → " + games[i].Path);

                        LogInfo("扫描完成: " + games.Count + " 个");
                        PostToPage(HardwareBridge.DualStateEvt());
                        ReplyDataJson(rid, action, "{\"count\":" + games.Count + "}");
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
                        ReplyDataJson(rid, action, "{\"freedBytes\":" + freed + ",\"fileCount\":" + count + "}");
                    }));
                    return;
                }
                else if (action == "unlock.scan")
                {
                    LogInfo("重扫引导状态…");
                    Task.Run(new Action(delegate
                    {
                        PostToPage(HardwareBridge.UnlockStateEvt());
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

        /// <summary>带自定义 data 的应答（data 已是合法 JSON 片段）。</summary>
        private void ReplyDataJson(string id, string action, string dataJson)
        {
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

        /// <summary>把字符串列表拼成 JSON 数组。</summary>
        private static string JsonArray(System.Collections.Generic.List<string> items)
        {
            StringBuilder sb = new StringBuilder(256);
            sb.Append('[');
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(HardwareBridge.Quote(items[i]));
            }
            sb.Append(']');
            return sb.ToString();
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
                    if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return null;
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
