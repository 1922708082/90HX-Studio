using System.Reflection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CMP90HXToolbox
{
    public class App : Application
    {
        private Window _window;
        // 图吧工具箱风格导航：左侧导航项 + 右侧页面容器 + 底部状态栏
        private RadioButton[] _navItems;
        private Grid[] _pageHosts;
        private TextBlock _txtBarCpu;
        private TextBlock _txtBarGpu;
        private TextBlock _txtBarRam;
        private TextBlock _txtBarOs;
        private TextBlock _txtBarClock;
        private DispatcherTimer _clockTimer;
        private TextBox _txtLogBox;
        private DispatcherTimer _sensorTimer;

        // 顶部控件
        private Border _bannerAdminWarning;
        private Button _btnRestartAsAdmin;
        private TextBlock _txtTopGpuModel;
        private TextBlock _txtTopPcieStatus;
        private TextBlock _txtTopOverlayStatus;
        private TextBlock _txtTopSensors;
        private Button _btnTopRefresh;
        private Button _btnTopAutoOptimize;

        // 硬件监控控件
        private TextBlock _txtSensTemp;
        private ProgressBar _barSensTemp;
        private TextBlock _txtSensFan;
        private ProgressBar _barSensFan;
        private TextBlock _txtSensPower;
        private ProgressBar _barSensPower;
        private TextBlock _txtSensVram;
        private ProgressBar _barSensVram;
        private TextBlock _txtSensUtil;
        private ProgressBar _barSensUtil;
        private TextBlock _txtSensClocks;
        private TextBlock _txtSensPstate;
        private TextBlock _txtSensPcieLink;
        private CheckBox _chkAutoRefreshSensors;
        private Button _btnRefreshSensorsOnce;

        // 全机硬件资产检测控件 (图吧风格)
        private TextBlock _txtHwCpuName;
        private TextBlock _txtHwCpuCores;
        private TextBlock _txtHwCpuClock;
        private ListBox _lstAllGpuDevices;
        private TextBlock _txtHwGpuCount;
        private TextBlock _txtHwMotherboard;
        private TextBlock _txtHwBiosVer;
        private TextBlock _txtHwUefiState;
        private TextBlock _txtHwSecureBoot;
        private TextBlock _txtHwHags;
        private TextBlock _txtHwMemory;
        private TextBlock _txtHwOsVer;

        // 显卡型号控件 (支持全系多卡切换)
        private ComboBox _cmbSpoofTargetCard;
        private Button _btnSpoofAllGpus;
        private TextBlock _txtClassSubKey;
        private TextBlock _txtCurrentDriverDesc;
        private TextBlock _txtCurrentGpuMatchId;
        private TextBlock _txtDirectXDesc;
        private Button _btnRenameTo3080;
        private Button _btnRestore90hx;
        private Button _btnQuickRestartDriver;

        // 游戏覆盖控件
        private TextBlock _txtNvAppPath;
        private TextBlock _txtOverlayPatchState;
        private TextBlock _txtOverlayBakState;
        private Button _btnUnlockOverlay;
        private Button _btnRestoreOverlay;
        private Button _btnCleanCefAndRestart;

        // PCIe 多模式解锁控件 与 全系矿卡混合解锁控件
        private TextBlock _txtPcieCurrentMode;
        private TextBlock _txtPcieBcdHealth;
        private TextBlock _txtSecureBootState;
        private RadioButton _radioMode1;
        private RadioButton _radioMode2;
        private RadioButton _radioMode3;
        private ComboBox _cmbUsbDrives;
        private Button _btnRefreshUsbDrives;
        private Button _btnUsbSetFirst;
        private Button _btnApplySelectedMode;
        private Button _btnRescanMiningGpus;
        private ListBox _lstMiningGpus;
        private TextBlock _txtMiningGpuSummary;
        private CheckBox _chkUnlockGraphics;
        private CheckBox _chkUnlockCompute;
        private Border _bannerNo90hxWarning;
        private Border _card90hxStatusInfo;
        private Grid _card90hxUnlockOptions;
        private Border _cardUnlockGraphics;
        private Border _cardUnlockCompute;
        private RadioButton _radioChannelA;
        private RadioButton _radioChannelB;
        private RadioButton _radioChannelC;
        private RadioButton _radioChannelD;
        private StackPanel _panelUsbPicker;
        private Button _btnApplyHybridUnlock;
        private Button _btnApplyComputeOnly;
        private Button _btnRun40hxInstaller;
        private Button _btnRun40hxCheck;
        private Button _btnRun30hxSetup;
        private Button _btnRun30hxReBar;
        private Button _btnOpenToolkitFolder;
        private Button _btnDeploy90hxCompute;
        private Button _btnDeploy90hxFull;
        private List<NativeCore.InstalledMiningGpu> _cachedMiningGpus;
        private Button _btnRollbackPcieOfficial;
        private Button _btnFixBcdConflicts;

        // 双卡调用方案控件
        private TextBlock _txtDualGpuRenderCard;
        private TextBlock _txtDualGpuDisplayCard;
        private TextBlock _txtDualGpuHagsState;
        private TextBlock _txtBoundGamesSummary;
        private Button _btnDualGpuAutoScan;
        private Button _btnDualGpuBrowseAdd;
        private Button _btnDualGpuAddFromRunning;
        private Button _btnDualGpuRemoveSelected;
        private Button _btnDualGpuClearAll;
        private ListBox _lstBoundGames;
        private Button _btnDualGpuSetDxGlobal;
        private Button _btnDualGpuSetOpenGlIcd;
        private Button _btnDualGpuSoftRestartDwm;
        private Button _btnDualGpuOpenWinGraphics;

        // DLSS 控件
        private TextBox _txtDlssGameDir;
        private Button _btnBrowseGameDir;
        private TextBlock _txtDlssStatusInDir;
        private ComboBox _cmbOptimizedLevel;
        private ComboBox _cmbMaxFramesLevel;
        private CheckBox _chkInjectReFramework;
        private Button _btnInjectDlss;
        private Button _btnRemoveDlss;
        private TextBlock _txtDlssLocalVersion;
        private ComboBox _cmbDlssMirrorSource;
        private Button _btnCheckDlssUpdate;
        private Button _btnDownloadDlssUpdate;
        private ProgressBar _progDlssDownload;
        private TextBlock _txtDlssUpdateStatus;
        private Button _btnOpenDlssRepo;

        // 系统调试工具控件
        private Button _btnToolRestartDriver;
        private Button _btnToolToggleHags;
        private TextBlock _txtToolHagsStatus;
        private Button _btnToolEnableUltimatePower;
        private Button _btnToolDevMgmt;
        private Button _btnToolDxDiag;
        private Button _btnToolNvCpl;
        private Button _btnToolGpuDiag;
        private Button _btnToolOpenDataDir;

        // 赞助图片控件
        private Image _imgWechatPay;
        private Image _imgAlipay;

        // 日志控件
        private Button _btnClearLogs;
        private Button _btnCopyLogs;

        #region 单实例唤醒与前台激活 Win32 API

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;

        private static void ActivateExistingInstance()
        {
            try
            {
                IntPtr targetHwnd = IntPtr.Zero;
                Process current = Process.GetCurrentProcess();

                // 1. 从同名进程获取主窗口句柄
                foreach (var p in Process.GetProcessesByName(current.ProcessName))
                {
                    if (p.Id != current.Id && p.MainWindowHandle != IntPtr.Zero)
                    {
                        targetHwnd = p.MainWindowHandle;
                        break;
                    }
                }

                // 2. 如果主窗口句柄尚未挂载或因进程名不同，按窗口标题特征查找
                if (targetHwnd == IntPtr.Zero)
                {
                    EnumWindows((hWnd, lParam) =>
                    {
                        var sb = new StringBuilder(256);
                        GetWindowText(hWnd, sb, sb.Capacity);
                        string title = sb.ToString();
                        if (title.StartsWith("CMP 90HX 控制中心") || title.StartsWith("CMP90HX"))
                        {
                            targetHwnd = hWnd;
                            return false;
                        }
                        return true;
                    }, IntPtr.Zero);
                }

                // 3. 唤醒并置顶已有窗口
                if (targetHwnd != IntPtr.Zero)
                {
                    if (IsIconic(targetHwnd))
                    {
                        ShowWindow(targetHwnd, SW_RESTORE);
                    }
                    else
                    {
                        ShowWindow(targetHwnd, SW_SHOW);
                    }
                    SetForegroundWindow(targetHwnd);
                }
            }
            catch { }
        }

        #endregion

        [STAThread]
        public static void Main()
        {
            try
            {
                bool isNewInstance = false;
                Mutex mutex = null;
                try
                {
                    mutex = new Mutex(true, "CMP90HX_Toolbox_SingleInstance_App_Mutex", out isNewInstance);
                }
                catch
                {
                    isNewInstance = true;
                }

                if (!isNewInstance && mutex != null)
                {
                    ActivateExistingInstance();
                    return;
                }

                try
                {
                    var app = new App();
                    app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

                    // ===== 全局异常兜底：任何未处理异常都记录日志并阻止进程静默终止 =====
                    // 背景：WPF 中 Storyboard/动画在 Dispatcher 线程抛出的未处理异常会直接杀死进程，
                    //       且不会被 Main 的 try-catch 捕获（表现为"双击闪退，无任何提示"）。
                    app.DispatcherUnhandledException += (s, e) =>
                    {
                        try
                        {
                            File.AppendAllText(Path.Combine(NativeCore.GetLogsDirectory(), "crash.log"),
                                "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] DispatcherUnhandledException: " + e.Exception.ToString() + "\r\n");
                        }
                        catch { }
                        e.Handled = true;
                    };
                    AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                    {
                        try
                        {
                            File.AppendAllText(Path.Combine(NativeCore.GetLogsDirectory(), "crash.log"),
                                "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] AppDomainUnhandledException: " + e.ExceptionObject.ToString() + "\r\n");
                        }
                        catch { }
                    };

                    app.InitializeComponent();
                    app.Run();
                }
                catch (Exception ex)
                {
                    try
                    {
                        File.AppendAllText(Path.Combine(NativeCore.GetLogsDirectory(), "crash.log"),
                            "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] Main crash: " + ex.ToString() + "\r\n");
                    }
                    catch { }
                    ModernMessageBox.Show("程序启动严重错误:\n" + ex.Message + "\n\n详细日志已保存至数据目录:\n" + Path.Combine(NativeCore.GetLogsDirectory(), "crash.log"), "90HX Studio", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    if (mutex != null)
                    {
                        try { mutex.ReleaseMutex(); } catch { }
                        try { mutex.Close(); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show("进程启动致命异常: " + ex.Message, "90HX Studio", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Window LoadSplashWindow()
        {
            try
            {
                Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SplashWindow.xaml");
                if (stream == null)
                {
                    string[] candidates = new string[] {
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"build_source\src\SplashWindow.xaml"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SplashWindow.xaml"),
                        @"D:\app\build_source\src\SplashWindow.xaml"
                    };
                    foreach (var c in candidates)
                    {
                        if (File.Exists(c))
                        {
                            stream = File.OpenRead(c);
                            break;
                        }
                    }
                }
                if (stream != null)
                {
                    using (stream)
                    {
                        return (Window)XamlReader.Load(stream);
                    }
                }
            }
            catch { }
            return null;
        }

        public void InitializeComponent()
        {
            try
            {
                Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MainWindow.xaml");

                if (stream == null)
                {
                    string[] candidates = new string[] {
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"build_source\src\MainWindow.xaml"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MainWindow.xaml"),
                        @"D:\app\build_source\src\MainWindow.xaml"
                    };

                    foreach (var c in candidates)
                    {
                        if (File.Exists(c))
                        {
                            stream = File.OpenRead(c);
                            break;
                        }
                    }
                }

                if (stream == null)
                {
                    throw new FileNotFoundException("未找到主界面 XAML 资源 (MainWindow.xaml)，请确认程序完整性。");
                }

                using (stream)
                {
                    _window = (Window)XamlReader.Load(stream);
                }

                BindControls();
                InitEvents();
                InitNavigation();

                CheckAdminPrivilege();
                RefreshSystemStatus();
                UpdateStatusBar();
                InitSensorTimer();

                // 播放开机彩蛋（全屏「病毒爆发」视觉覆盖层，严格只读、非破坏性）
                ShowStartupPrank();
            }
            catch (Exception ex)
            {
                try
                {
                    File.AppendAllText(Path.Combine(NativeCore.GetLogsDirectory(), "crash.log"),
                        "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] InitializeComponent crash: " + ex.ToString() + "\r\n");
                }
                catch { }
                ModernMessageBox.Show("界面加载失败: " + ex.ToString(), "严重错误", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        /// <summary>
        /// 开机彩蛋：全屏「病毒爆发」视觉覆盖层。
        /// 严格只读（仅采集桌面图标与系统窗口坐标用于渲染）、非破坏性（不修改/移动/关闭任何真实对象）、
        /// 默认不捕获键鼠（仅中央提示框可交互）。用户关闭提示框后进入主界面。
        /// 彩蛋本身出现任何异常都直接降级进入主界面，绝不影响程序可用性。
        /// </summary>
        private void ShowStartupPrank()
        {
            try
            {
                var overlay = new VirusOverlay();
                overlay.PrankCompleted += () =>
                {
                    try
                    {
                        if (_window != null)
                        {
                            MainWindow = _window;
                            ShutdownMode = ShutdownMode.OnMainWindowClose;
                            _window.Show();
                            _window.Activate();
                            RefreshSensors();
                            LogAudit("开机彩蛋播放完毕（纯视觉动画，未对系统做任何修改）。90HX Studio 已就绪。");
                        }
                    }
                    catch
                    {
                        // 显示主窗口失败时静默降级（已由全局异常处理器记录日志）
                    }
                };
                overlay.Show();
            }
            catch
            {
                // 彩蛋异常：静默降级进入主界面
                // 降级：跳过彩蛋直接进主界面
                if (_window != null)
                {
                    MainWindow = _window;
                    ShutdownMode = ShutdownMode.OnMainWindowClose;
                    _window.Show();
                    RefreshSensors();
                    LogAudit("90HX Studio 已就绪。所有底层服务与安全防呆引擎已初始化完成。");
                }
            }
        }

        private void BindControls()
        {
            _bannerAdminWarning = (Border)_window.FindName("BannerAdminWarning");
            _btnRestartAsAdmin = (Button)_window.FindName("BtnRestartAsAdmin");
            _txtTopGpuModel = (TextBlock)_window.FindName("TxtTopGpuModel");
            _txtTopPcieStatus = (TextBlock)_window.FindName("TxtTopPcieStatus");
            _txtTopOverlayStatus = (TextBlock)_window.FindName("TxtTopOverlayStatus");
            _txtTopSensors = (TextBlock)_window.FindName("TxtTopSensors");
            _btnTopRefresh = (Button)_window.FindName("BtnTopRefresh");
            _btnTopAutoOptimize = (Button)_window.FindName("BtnTopAutoOptimize");

            // 图吧工具箱风格：左侧导航 + 页面容器 + 底部硬件状态栏
            _navItems = new RadioButton[] {
                (RadioButton)_window.FindName("NavItemHardware"),
                (RadioButton)_window.FindName("NavItemSpoof"),
                (RadioButton)_window.FindName("NavItemOverlay"),
                (RadioButton)_window.FindName("NavItemMatrix"),
                (RadioButton)_window.FindName("NavItemDualGpu"),
                (RadioButton)_window.FindName("NavItemDlss"),
                (RadioButton)_window.FindName("NavItemPower"),
                (RadioButton)_window.FindName("NavItemLogs"),
                (RadioButton)_window.FindName("NavItemSponsor"),
            };
            _pageHosts = new Grid[] {
                (Grid)_window.FindName("PageHardware"),
                (Grid)_window.FindName("PageSpoof"),
                (Grid)_window.FindName("PageOverlay"),
                (Grid)_window.FindName("PageMatrix"),
                (Grid)_window.FindName("PageDualGpu"),
                (Grid)_window.FindName("PageDlss"),
                (Grid)_window.FindName("PagePower"),
                (Grid)_window.FindName("PageLogs"),
                (Grid)_window.FindName("PageSponsor"),
            };
            _txtBarCpu = (TextBlock)_window.FindName("TxtBarCpu");
            _txtBarGpu = (TextBlock)_window.FindName("TxtBarGpu");
            _txtBarRam = (TextBlock)_window.FindName("TxtBarRam");
            _txtBarOs = (TextBlock)_window.FindName("TxtBarOs");
            _txtBarClock = (TextBlock)_window.FindName("TxtBarClock");

            _txtSensTemp = (TextBlock)_window.FindName("TxtSensTemp");
            _barSensTemp = (ProgressBar)_window.FindName("BarSensTemp");
            _txtSensFan = (TextBlock)_window.FindName("TxtSensFan");
            _barSensFan = (ProgressBar)_window.FindName("BarSensFan");
            _txtSensPower = (TextBlock)_window.FindName("TxtSensPower");
            _barSensPower = (ProgressBar)_window.FindName("BarSensPower");
            _txtSensVram = (TextBlock)_window.FindName("TxtSensVram");
            _barSensVram = (ProgressBar)_window.FindName("BarSensVram");
            _txtSensUtil = (TextBlock)_window.FindName("TxtSensUtil");
            _barSensUtil = (ProgressBar)_window.FindName("BarSensUtil");
            _txtSensClocks = (TextBlock)_window.FindName("TxtSensClocks");
            _txtSensPstate = (TextBlock)_window.FindName("TxtSensPstate");
            _txtSensPcieLink = (TextBlock)_window.FindName("TxtSensPcieLink");
            _chkAutoRefreshSensors = (CheckBox)_window.FindName("ChkAutoRefreshSensors");
            _btnRefreshSensorsOnce = (Button)_window.FindName("BtnRefreshSensorsOnce");

            // 全机硬件资产控件
            _txtHwCpuName = (TextBlock)_window.FindName("TxtHwCpuName");
            _txtHwCpuCores = (TextBlock)_window.FindName("TxtHwCpuCores");
            _txtHwCpuClock = (TextBlock)_window.FindName("TxtHwCpuClock");
            _lstAllGpuDevices = (ListBox)_window.FindName("LstAllGpuDevices");
            _txtHwGpuCount = (TextBlock)_window.FindName("TxtHwGpuCount");
            _txtHwMotherboard = (TextBlock)_window.FindName("TxtHwMotherboard");
            _txtHwBiosVer = (TextBlock)_window.FindName("TxtHwBiosVer");
            _txtHwUefiState = (TextBlock)_window.FindName("TxtHwUefiState");
            _txtHwSecureBoot = (TextBlock)_window.FindName("TxtHwSecureBoot");
            _txtHwHags = (TextBlock)_window.FindName("TxtHwHags");
            _txtHwMemory = (TextBlock)_window.FindName("TxtHwMemory");
            _txtHwOsVer = (TextBlock)_window.FindName("TxtHwOsVer");

            // 显卡型号多卡控件
            _cmbSpoofTargetCard = (ComboBox)_window.FindName("CmbSpoofTargetCard");
            _btnSpoofAllGpus = (Button)_window.FindName("BtnSpoofAllGpus");

            _txtClassSubKey = (TextBlock)_window.FindName("TxtClassSubKey");
            _txtCurrentDriverDesc = (TextBlock)_window.FindName("TxtCurrentDriverDesc");
            _txtCurrentGpuMatchId = (TextBlock)_window.FindName("TxtCurrentGpuMatchId");
            _txtDirectXDesc = (TextBlock)_window.FindName("TxtDirectXDesc");
            _btnRenameTo3080 = (Button)_window.FindName("BtnRenameTo3080");
            _btnRestore90hx = (Button)_window.FindName("BtnRestore90hx");
            _btnQuickRestartDriver = (Button)_window.FindName("BtnQuickRestartDriver");

            _txtNvAppPath = (TextBlock)_window.FindName("TxtNvAppPath");
            _txtOverlayPatchState = (TextBlock)_window.FindName("TxtOverlayPatchState");
            _txtOverlayBakState = (TextBlock)_window.FindName("TxtOverlayBakState");
            _btnUnlockOverlay = (Button)_window.FindName("BtnUnlockOverlay");
            _btnRestoreOverlay = (Button)_window.FindName("BtnRestoreOverlay");
            _btnCleanCefAndRestart = (Button)_window.FindName("BtnCleanCefAndRestart");

            _txtPcieCurrentMode = (TextBlock)_window.FindName("TxtPcieCurrentMode");
            _txtPcieBcdHealth = (TextBlock)_window.FindName("TxtPcieBcdHealth");
            _txtSecureBootState = (TextBlock)_window.FindName("TxtSecureBootState");
            _radioMode1 = (RadioButton)_window.FindName("RadioMode1");
            _radioMode2 = (RadioButton)_window.FindName("RadioMode2");
            _radioMode3 = (RadioButton)_window.FindName("RadioMode3");
            _cmbUsbDrives = (ComboBox)_window.FindName("CmbUsbDrives");
            _btnRefreshUsbDrives = (Button)_window.FindName("BtnRefreshUsbDrives");
            _btnUsbSetFirst = (Button)_window.FindName("BtnUsbSetFirst");
            _btnApplySelectedMode = (Button)_window.FindName("BtnApplySelectedMode");
            _btnRollbackPcieOfficial = (Button)_window.FindName("BtnRollbackPcieOfficial");
            _btnFixBcdConflicts = (Button)_window.FindName("BtnFixBcdConflicts");

            _btnRescanMiningGpus = (Button)_window.FindName("BtnRescanMiningGpus");
            _lstMiningGpus = (ListBox)_window.FindName("LstMiningGpus");
            _txtMiningGpuSummary = (TextBlock)_window.FindName("TxtMiningGpuSummary");
            _chkUnlockGraphics = (CheckBox)_window.FindName("ChkUnlockGraphics");
            _chkUnlockCompute = (CheckBox)_window.FindName("ChkUnlockCompute");
            _bannerNo90hxWarning = (Border)_window.FindName("BannerNo90hxWarning");
            _card90hxStatusInfo = (Border)_window.FindName("Card90hxStatusInfo");
            _card90hxUnlockOptions = (Grid)_window.FindName("Card90hxUnlockOptions");
            _cardUnlockGraphics = (Border)_window.FindName("CardUnlockGraphics");
            _cardUnlockCompute = (Border)_window.FindName("CardUnlockCompute");
            _radioChannelA = (RadioButton)_window.FindName("RadioChannelA");
            _radioChannelB = (RadioButton)_window.FindName("RadioChannelB");
            _radioChannelC = (RadioButton)_window.FindName("RadioChannelC");
            _radioChannelD = (RadioButton)_window.FindName("RadioChannelD");
            _panelUsbPicker = (StackPanel)_window.FindName("PanelUsbPicker");
            _btnApplyHybridUnlock = (Button)_window.FindName("BtnApplyHybridUnlock");
            _btnApplyComputeOnly = (Button)_window.FindName("BtnApplyComputeOnly");
            _btnRun40hxInstaller = (Button)_window.FindName("BtnRun40hxInstaller");
            _btnRun40hxCheck = (Button)_window.FindName("BtnRun40hxCheck");
            _btnRun30hxSetup = (Button)_window.FindName("BtnRun30hxSetup");
            _btnRun30hxReBar = (Button)_window.FindName("BtnRun30hxReBar");
            _btnOpenToolkitFolder = (Button)_window.FindName("BtnOpenToolkitFolder");
            _btnDeploy90hxCompute = (Button)_window.FindName("BtnDeploy90hxCompute");
            _btnDeploy90hxFull = (Button)_window.FindName("BtnDeploy90hxFull");

            _txtDualGpuRenderCard = (TextBlock)_window.FindName("TxtDualGpuRenderCard");
            _txtDualGpuDisplayCard = (TextBlock)_window.FindName("TxtDualGpuDisplayCard");
            _txtDualGpuHagsState = (TextBlock)_window.FindName("TxtDualGpuHagsState");
            _txtBoundGamesSummary = (TextBlock)_window.FindName("TxtBoundGamesSummary");
            _btnDualGpuAutoScan = (Button)_window.FindName("BtnDualGpuAutoScan");
            _btnDualGpuBrowseAdd = (Button)_window.FindName("BtnDualGpuBrowseAdd");
            _btnDualGpuAddFromRunning = (Button)_window.FindName("BtnDualGpuAddFromRunning");
            _btnDualGpuRemoveSelected = (Button)_window.FindName("BtnDualGpuRemoveSelected");
            _btnDualGpuClearAll = (Button)_window.FindName("BtnDualGpuClearAll");
            _lstBoundGames = (ListBox)_window.FindName("LstBoundGames");
            _btnDualGpuSetDxGlobal = (Button)_window.FindName("BtnDualGpuSetDxGlobal");
            _btnDualGpuSetOpenGlIcd = (Button)_window.FindName("BtnDualGpuSetOpenGlIcd");
            _btnDualGpuSoftRestartDwm = (Button)_window.FindName("BtnDualGpuSoftRestartDwm");
            _btnDualGpuOpenWinGraphics = (Button)_window.FindName("BtnDualGpuOpenWinGraphics");

            _txtDlssGameDir = (TextBox)_window.FindName("TxtDlssGameDir");
            _btnBrowseGameDir = (Button)_window.FindName("BtnBrowseGameDir");
            _txtDlssStatusInDir = (TextBlock)_window.FindName("TxtDlssStatusInDir");
            _cmbOptimizedLevel = (ComboBox)_window.FindName("CmbOptimizedLevel");
            _cmbMaxFramesLevel = (ComboBox)_window.FindName("CmbMaxFramesLevel");
            _chkInjectReFramework = (CheckBox)_window.FindName("ChkInjectReFramework");
            _btnInjectDlss = (Button)_window.FindName("BtnInjectDlss");
            _btnRemoveDlss = (Button)_window.FindName("BtnRemoveDlss");

            _txtDlssLocalVersion = (TextBlock)_window.FindName("TxtDlssLocalVersion");
            _cmbDlssMirrorSource = (ComboBox)_window.FindName("CmbDlssMirrorSource");
            if (_cmbDlssMirrorSource != null)
            {
                _cmbDlssMirrorSource.Items.Clear();
                var mirrors = NativeCore.GetDlssMirrors();
                foreach (var m in mirrors)
                {
                    _cmbDlssMirrorSource.Items.Add(m.Name);
                }
                if (_cmbDlssMirrorSource.Items.Count > 0)
                {
                    _cmbDlssMirrorSource.SelectedIndex = 0;
                }
            }
            _btnCheckDlssUpdate = (Button)_window.FindName("BtnCheckDlssUpdate");
            _btnDownloadDlssUpdate = (Button)_window.FindName("BtnDownloadDlssUpdate");
            _progDlssDownload = (ProgressBar)_window.FindName("ProgDlssDownload");
            _txtDlssUpdateStatus = (TextBlock)_window.FindName("TxtDlssUpdateStatus");
            _btnOpenDlssRepo = (Button)_window.FindName("BtnOpenDlssRepo");

            _btnToolRestartDriver = (Button)_window.FindName("BtnToolRestartDriver");
            _btnToolToggleHags = (Button)_window.FindName("BtnToolToggleHags");
            _txtToolHagsStatus = (TextBlock)_window.FindName("TxtToolHagsStatus");
            _btnToolEnableUltimatePower = (Button)_window.FindName("BtnToolEnableUltimatePower");
            _btnToolDevMgmt = (Button)_window.FindName("BtnToolDevMgmt");
            _btnToolDxDiag = (Button)_window.FindName("BtnToolDxDiag");
            _btnToolNvCpl = (Button)_window.FindName("BtnToolNvCpl");
            _btnToolGpuDiag = (Button)_window.FindName("BtnToolGpuDiag");
            _btnToolOpenDataDir = (Button)_window.FindName("BtnToolOpenDataDir");

            _txtLogBox = (TextBox)_window.FindName("TxtLogBox");
            _btnClearLogs = (Button)_window.FindName("BtnClearLogs");
            _btnCopyLogs = (Button)_window.FindName("BtnCopyLogs");

            _imgWechatPay = (Image)_window.FindName("ImgWechatPay");
            _imgAlipay = (Image)_window.FindName("ImgAlipay");

            LoadEmbeddedSponsorImages();
        }

        private void LoadEmbeddedSponsorImages()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                if (_imgWechatPay != null)
                {
                    Stream s = asm.GetManifestResourceStream("wechat_pay.png");
                    if (s == null)
                    {
                        string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"build_source\assets\wechat_pay.png");
                        if (File.Exists(localPath)) s = File.OpenRead(localPath);
                    }
                    if (s != null)
                    {
                        var ms = new MemoryStream();
                        s.CopyTo(ms);
                        ms.Position = 0;
                        s.Dispose();

                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.StreamSource = ms;
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.EndInit();
                        bi.Freeze();
                        _imgWechatPay.Source = bi;
                    }
                }
                if (_imgAlipay != null)
                {
                    Stream s = asm.GetManifestResourceStream("alipay.png");
                    if (s == null)
                    {
                        string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"build_source\assets\alipay.png");
                        if (File.Exists(localPath)) s = File.OpenRead(localPath);
                    }
                    if (s != null)
                    {
                        var ms = new MemoryStream();
                        s.CopyTo(ms);
                        ms.Position = 0;
                        s.Dispose();

                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.StreamSource = ms;
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.EndInit();
                        bi.Freeze();
                        _imgAlipay.Source = bi;
                    }
                }
            }
            catch (Exception ex)
            {
                LogAudit("加载赞助二维码异常: " + ex.Message);
            }
        }

        /// <summary>
        /// 页面导航（图吧工具箱风格）：点击左侧导航切换右侧页面，
        /// 带平滑淡入 + 阻尼微上浮转场，并同步底部硬件状态栏与时钟。
        /// </summary>
        private void InitNavigation()
        {
            if (_navItems == null || _pageHosts == null) return;

            for (int i = 0; i < _navItems.Length; i++)
            {
                int idx = i;
                var nav = _navItems[i];
                if (nav == null) continue;
                nav.Checked += (s, e) => SwitchPage(idx, true);
            }

            // 默认打开硬件信息页
            if (_navItems.Length > 0 && _navItems[0] != null) _navItems[0].IsChecked = true;
            else SwitchPage(0, false);

            // 底部时钟
            _clockTimer = new DispatcherTimer();
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += (s, e) =>
            {
                if (_txtBarClock != null) _txtBarClock.Text = DateTime.Now.ToString("HH:mm:ss");
            };
            _clockTimer.Start();
        }

        private void SwitchPage(int index, bool animate)
        {
            if (_pageHosts == null || index < 0 || index >= _pageHosts.Length) return;

            for (int i = 0; i < _pageHosts.Length; i++)
            {
                var page = _pageHosts[i];
                if (page == null) continue;

                if (i == index)
                {
                    page.Visibility = Visibility.Visible;
                    if (animate)
                    {
                        page.Opacity = 0.0;
                        var tt = new TranslateTransform(0, 10);
                        page.RenderTransform = tt;

                        var fadeAnim = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(240)))
                        {
                            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                        };
                        var slideAnim = new DoubleAnimation(10.0, 0.0, new Duration(TimeSpan.FromMilliseconds(300)))
                        {
                            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                        };

                        page.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
                        tt.BeginAnimation(TranslateTransform.YProperty, slideAnim);
                    }
                    else
                    {
                        page.Opacity = 1.0;
                        page.RenderTransform = null;
                    }
                }
                else
                {
                    page.Visibility = Visibility.Collapsed;
                }
            }
        }

        /// <summary>
        /// 填充底部硬件摘要状态栏（CPU / 显卡 / 内存 / 系统）。
        /// </summary>
        private void UpdateStatusBar()
        {
            try
            {
                var status = SystemDetector.DetectAll();
                var hw = status.Hardware;
                if (_txtBarCpu != null) _txtBarCpu.Text = hw != null ? hw.CpuName : "未知";
                if (_txtBarGpu != null) _txtBarGpu.Text = status.GpuDesc;
                if (_txtBarRam != null) _txtBarRam.Text = hw != null ? hw.TotalMemory : "未知";
                if (_txtBarOs != null) _txtBarOs.Text = hw != null ? hw.OsVersion : "未知";
                if (_txtBarClock != null) _txtBarClock.Text = DateTime.Now.ToString("HH:mm:ss");
            }
            catch { }
        }

        private void InitEvents()
        {
            // 提权
            if (_btnRestartAsAdmin != null)
            {
                _btnRestartAsAdmin.Click += (s, e) =>
                {
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = Process.GetCurrentProcess().MainModule.FileName,
                            UseShellExecute = true,
                            Verb = "runas"
                        };
                        Process.Start(psi);
                        Application.Current.Shutdown();
                    }
                    catch { }
                };
            }

            // 顶部
            if (_btnTopRefresh != null)
                _btnTopRefresh.Click += (s, e) =>
            {
                RefreshSystemStatus();
                RefreshSensors();
                LogAudit("系统硬件与配置状态已刷新。");
            };

            // 一键最优配置（带全面防重复检测）
            if (_btnTopAutoOptimize != null)
                _btnTopAutoOptimize.Click += async (s, e) =>
            {
                var curStatus = SystemDetector.DetectAll();
                bool needRename = !curStatus.IsSpoofedTo3080;
                bool needOverlay = !curStatus.OverlayPatched;
                bool needPcie = curStatus.PcieMode != NativeCore.PcieUnlockMode.Mode1_DirectBoot;

                if (!needRename && !needOverlay && !needPcie)
                {
                    ModernMessageBox.Show("【防重复配置提示】\n检测到当前系统已处于全部最优配置状态：\n✓ 显卡已深度伪装为 RTX 3080\n✓ NVIDIA App 游戏覆盖免检已激活\n✓ PCIe 模式 1 极速直启双保险引导已部署\n\n无需重复执行优化序列！若需刷新硬件状态，请直接重启电脑。", "已达最优状态", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                string plan = "一键最优配置将根据当前硬件环境执行以下调优序列：\n";
                plan += (needRename ? "1. 【执行】显卡型号深度伪装为 RTX 3080\n" : "1. 【跳过】显卡型号已是 RTX 3080\n");
                plan += (needOverlay ? "2. 【执行】解除 NVIDIA App 游戏覆盖白名单限制\n" : "2. 【跳过】NVIDIA App 覆盖免检已处于激活状态\n");
                plan += (needPcie ? "3. 【执行】安装模式 1 (直启双保险) PCIe Gen2\n" : "3. 【跳过】PCIe 模式 1 引导已处于安装就绪状态\n");
                plan += "\n是否立即开始执行？";

                if (ModernMessageBox.Show(plan, "一键最优配置", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;

                _btnTopAutoOptimize.IsEnabled = false;
                LogAudit(">>> 开始执行最优硬件调优配置序列...");

                if (needRename)
                {
                    await NativeCore.ApplyGpuRenameTo3080Async(LogCallback);
                }
                if (needOverlay)
                {
                    await NativeCore.UnlockNvAppOverlayAsync(LogCallback);
                }
                if (needPcie)
                {
                    await NativeCore.InstallPcieMode1Async(LogCallback);
                }

                RefreshSystemStatus();
                RefreshSensors();
                _btnTopAutoOptimize.IsEnabled = true;
                ModernMessageBox.Show("一键最优配置执行完毕！\n请记得将主机【完全关机】断电 10 秒后重新开机生效！", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
            };

            // 传感器与全机资产刷新
            if (_btnRefreshSensorsOnce != null)
                _btnRefreshSensorsOnce.Click += (s, e) => RefreshSystemStatus();

            // 多卡伪装选择器联动
            if (_cmbSpoofTargetCard != null)
            {
                _cmbSpoofTargetCard.SelectionChanged += (s, e) =>
                {
                    int idx = _cmbSpoofTargetCard.SelectedIndex;
                    var gpus = NativeCore.DetectAllMiningGpus();
                    if (idx >= 0 && idx < gpus.Count)
                    {
                        var g = gpus[idx];
                        _txtClassSubKey.Text = g.ClassSubKey;
                        _txtCurrentDriverDesc.Text = g.CurrentDesc;
                        _txtCurrentGpuMatchId.Text = g.DevInstanceId;
                        _txtDirectXDesc.Text = g.TargetGeForceName + " (" + g.ChipArchitecture + ")";
                        if (_btnRenameTo3080 != null)
                        {
                            _btnRenameTo3080.Content = "一键伪装当前卡 ➔ " + g.TargetGeForceName;
                        }
                    }
                };
            }

            // 一键全机多卡全部伪装 (混合矩阵)
            if (_btnSpoofAllGpus != null)
            {
                _btnSpoofAllGpus.Click += async (s, e) =>
                {
                    if (!EnsureAdminPrivilege()) return;
                    var gpus = NativeCore.DetectAllMiningGpus();
                    _btnSpoofAllGpus.IsEnabled = false;
                    bool ok = await NativeCore.ApplyHybridMultiGpuUnlockAsync(gpus, true, false, LogCallback);
                    _btnSpoofAllGpus.IsEnabled = true;
                    RefreshSystemStatus();
                    if (ok) ModernMessageBox.Show("全机所有矿卡已全部完成型号与图形深度伪装！\n设备管理器与图形系统已就绪。", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                };
            }

            // 显卡型号伪装（针对当前选中的矿卡执行）
            if (_btnRenameTo3080 != null)
                _btnRenameTo3080.Click += async (s, e) =>
            {
                if (!EnsureAdminPrivilege()) return;
                int selIdx = _cmbSpoofTargetCard != null ? _cmbSpoofTargetCard.SelectedIndex : 0;
                var gpus = NativeCore.DetectAllMiningGpus();
                var targetGpu = (selIdx >= 0 && selIdx < gpus.Count) ? gpus[selIdx] : (gpus.Count > 0 ? gpus[0] : null);

                if (targetGpu == null)
                {
                    ModernMessageBox.Show("未检测到有效矿卡设备！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _btnRenameTo3080.IsEnabled = false;
                bool ok = await NativeCore.ApplySingleGpuGraphicsUnlockAsync(targetGpu, LogCallback);
                await NativeCore.SoftRestartGpuDriverAsync(LogCallback);
                _btnRenameTo3080.IsEnabled = true;
                RefreshSystemStatus();
                if (ok) ModernMessageBox.Show(string.Format("已成功将 [{0}] 伪装为 {1}！\n设备管理器与显示服务已即时刷新。", targetGpu.DetectedModel, targetGpu.TargetGeForceName), "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            };

            // 恢复显卡出厂原名（针对当前选中的矿卡）
            if (_btnRestore90hx != null)
                _btnRestore90hx.Click += async (s, e) =>
            {
                if (!EnsureAdminPrivilege()) return;
                int selIdx = _cmbSpoofTargetCard != null ? _cmbSpoofTargetCard.SelectedIndex : 0;
                var gpus = NativeCore.DetectAllMiningGpus();
                var targetGpu = (selIdx >= 0 && selIdx < gpus.Count) ? gpus[selIdx] : (gpus.Count > 0 ? gpus[0] : null);

                if (targetGpu == null) return;

                _btnRestore90hx.IsEnabled = false;
                bool ok = await NativeCore.RestoreMiningGpuAsync(targetGpu, LogCallback);
                await NativeCore.SoftRestartGpuDriverAsync(LogCallback);
                _btnRestore90hx.IsEnabled = true;
                RefreshSystemStatus();
                if (ok) ModernMessageBox.Show(string.Format("已恢复 [{0}] 为出厂官方名称！\n设备管理器与显示服务已即时刷新。", targetGpu.DetectedModel), "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            };

            if (_btnQuickRestartDriver != null)
                _btnQuickRestartDriver.Click += async (s, e) =>
                {
                    if (!EnsureAdminPrivilege()) return;
                    _btnQuickRestartDriver.IsEnabled = false;
                    await NativeCore.SoftRestartGpuDriverAsync(LogCallback);
                    _btnQuickRestartDriver.IsEnabled = true;
                    RefreshSystemStatus();
                    RefreshSensors();
                };

            // 游戏覆盖解锁（防重复检测）
            if (_btnUnlockOverlay != null)
                _btnUnlockOverlay.Click += async (s, e) =>
            {
                var curStatus = SystemDetector.DetectAll();
                if (!curStatus.NvAppInstalled)
                {
                    ModernMessageBox.Show("【未检测到 NVIDIA App】\n系统未找到 NVIDIA App 安装目录！请先安装官方 NVIDIA App 后再执行解锁。", "未安装", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (curStatus.OverlayPatched)
                {
                    var confirm = ModernMessageBox.Show("【防重复注入检测】\nNVIDIA App 游戏覆盖免检补丁已成功激活生效！\n(ShadowPlay 录屏与 Alt+R 实时帧率悬浮窗已可用)\n\n无需重复安装。是否仍要强制重新覆写补丁？", "已处于激活状态", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (confirm != MessageBoxResult.Yes) return;
                }

                _btnUnlockOverlay.IsEnabled = false;
                bool ok = await NativeCore.UnlockNvAppOverlayAsync(LogCallback);
                _btnUnlockOverlay.IsEnabled = true;
                RefreshSystemStatus();
                if (ok) ModernMessageBox.Show("游戏覆盖解锁成功！现在可使用 Alt+R 呼出实时性能悬浮窗，Alt+Z 呼出录屏控制台。", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            };

            // 游戏覆盖还原官方（防重复检测）
            if (_btnRestoreOverlay != null)
                _btnRestoreOverlay.Click += async (s, e) =>
            {
                var curStatus = SystemDetector.DetectAll();
                if (!curStatus.NvAppInstalled)
                {
                    ModernMessageBox.Show("【未安装 NVIDIA App】\n系统未找到 NVIDIA App，无需还原！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                if (!curStatus.OverlayPatched && !curStatus.OverlayBakExists)
                {
                    ModernMessageBox.Show("【无需重复还原】\n当前 NVIDIA App 本来就是官方原生纯净版本，未检测到任何修改或补丁文件，无需重复还原！", "官方纯净版", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                _btnRestoreOverlay.IsEnabled = false;
                bool ok = await NativeCore.RestoreNvAppOverlayAsync(LogCallback);
                _btnRestoreOverlay.IsEnabled = true;
                RefreshSystemStatus();
                if (ok) ModernMessageBox.Show("已恢复为官方原始未修改版本！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            };

            if (_btnCleanCefAndRestart != null)
                _btnCleanCefAndRestart.Click += (s, e) =>
                {
                    NativeCore.CleanCefCacheInternal(LogCallback);
                    LogAudit("正在重新启动 NVIDIA App...");
                    string lnk = @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\NVIDIA Corporation\NVIDIA App.lnk";
                    if (File.Exists(lnk)) Process.Start(lnk);
                };

            // 90HX 专属解锁
            if (_btnRefreshUsbDrives != null) _btnRefreshUsbDrives.Click += (s, e) => LoadUsbDrives();

            // ⚡ 让 U 盘优先启动 (解决"设置了U盘第一启动项仍进Windows"的问题)
            if (_btnUsbSetFirst != null)
            {
                _btnUsbSetFirst.Click += async (s, e) =>
                {
                    if (!EnsureAdminPrivilege()) return;
                    string sel = Convert.ToString(_cmbUsbDrives != null ? _cmbUsbDrives.SelectedItem : "");
                    if (string.IsNullOrEmpty(sel) || sel.Length < 2 || sel.IndexOf("未检测", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ModernMessageBox.Show("请先在上方下拉框中选中一个可移动 U 盘！\n\n建议使用 FAT32 格式的小容量 U 盘。", "未选择 U 盘", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    string drive = sel.Substring(0, 2);

                    var confirm = ModernMessageBox.Show(
                        "即将把 U 盘 (" + drive + ") 配置为开机第一启动项：\n\n" +
                        "1. 为该 U 盘创建独立的 UEFI 固件启动项并置顶\n" +
                        "2. 关闭 Windows 快速启动 (关键，避免混合关机跳过启动顺序)\n" +
                        "3. 打开 5 秒启动菜单作为兜底\n\n" +
                        "此操作不写入本机硬盘系统，安全可撤销。是否继续？",
                        "配置 U 盘优先启动", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (confirm != MessageBoxResult.Yes) return;

                    _btnUsbSetFirst.IsEnabled = false;
                    bool ok = await NativeCore.SetUsbFirstBootAsync(drive + "\\", LogCallback);
                    _btnUsbSetFirst.IsEnabled = true;

                    if (ok)
                    {
                        ModernMessageBox.Show(
                            "配置完成！下次开机将优先从 U 盘启动并自动解锁。\n\n" +
                            "【若仍进 Windows，请检查 BIOS】\n" +
                            "1. 启动项要选带【UEFI:】前缀的 U 盘条目\n" +
                            "2. 关闭 BIOS 的 Fast Boot（快速启动）\n" +
                            "3. 部分主板需在【Hard Drive BBS Priorities】里也把 U 盘调第一\n\n" +
                            "【最省心方案】改用「安装到本机硬盘」，完全免 U 盘、不受 BIOS 启动顺序影响。",
                            "配置完成", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        ModernMessageBox.Show("配置失败，请查看下方日志。\n\n建议直接使用「安装到本机硬盘」方式，更稳妥。", "配置失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                };
            }

            // PCIe 彻底还原官方（防重复检测）
            if (_btnRollbackPcieOfficial != null)
            {
                _btnRollbackPcieOfficial.Click += async (s, e) =>
                {
                    var curStatus = SystemDetector.DetectAll();
                    if (curStatus.PcieMode == NativeCore.PcieUnlockMode.None)
                    {
                        ModernMessageBox.Show("【无需重复还原】\n当前系统引导环境本来就是微软官方原生纯净状态，未检测到任何 90HX 解锁项或 ESP 补丁文件！", "原生纯净状态", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    if (ModernMessageBox.Show("确定要彻底还原为微软官方标准引导吗？\n将自动删除所有 90HX 引导项与 ESP 解锁文件。", "确认还原", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                        return;

                    _btnRollbackPcieOfficial.IsEnabled = false;
                    bool ok = await NativeCore.RollbackPcieUnlockAsync(LogCallback);
                    _btnRollbackPcieOfficial.IsEnabled = true;
                    RefreshSystemStatus();
                    if (ok) ModernMessageBox.Show("系统引导已 100% 恢复官方原生状态，无任何残留！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                };
            }

            if (_btnFixBcdConflicts != null)
            {
                _btnFixBcdConflicts.Click += (s, e) =>
                {
                    LogAudit("正在执行 BCD 引导健康自检与孤立项修复...");
                    bool ok = NativeCore.CleanAllPcieUnlocks(LogCallback);
                    RefreshSystemStatus();
                    RefreshMiningGpuList();
                    if (ok) ModernMessageBox.Show("BCD 引导环境已自检并修复完毕，系统引导已恢复纯净！", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                };
            }

            // 全系矿卡多卡扫描与混合矩阵解锁事件
            if (_btnRescanMiningGpus != null)
            {
                _btnRescanMiningGpus.Click += (s, e) => RefreshMiningGpuList();
            }

            // 解锁卡片整卡可点击：点卡片即选中对应单选，并高亮选中态
            if (_cardUnlockGraphics != null)
            {
                _cardUnlockGraphics.MouseLeftButtonUp += (s, e) => { if (_radioMode1 != null) _radioMode1.IsChecked = true; };
            }
            if (_cardUnlockCompute != null)
            {
                _cardUnlockCompute.MouseLeftButtonUp += (s, e) => { if (_radioMode2 != null) _radioMode2.IsChecked = true; };
            }
            if (_radioMode1 != null)
            {
                _radioMode1.Checked += (s, e) => UpdateUnlockCardHighlight();
                _radioMode1.Unchecked += (s, e) => UpdateUnlockCardHighlight();
            }
            if (_radioMode2 != null)
            {
                _radioMode2.Checked += (s, e) => UpdateUnlockCardHighlight();
                _radioMode2.Unchecked += (s, e) => UpdateUnlockCardHighlight();
            }
            UpdateUnlockCardHighlight();

            // 安装通道选择联动：选 U 盘直启时展开盘符选择器
            if (_radioChannelA != null) _radioChannelA.Checked += (s, e) => UpdateUsbPickerVisibility();
            if (_radioChannelB != null) _radioChannelB.Checked += (s, e) => UpdateUsbPickerVisibility();
            if (_radioChannelC != null) _radioChannelC.Checked += (s, e) => UpdateUsbPickerVisibility();
            if (_radioChannelD != null) _radioChannelD.Checked += (s, e) => UpdateUsbPickerVisibility();
            UpdateUsbPickerVisibility();

            if (_btnApplyHybridUnlock != null)
            {
                _btnApplyHybridUnlock.Click += async (s, e) =>
                {
                    var curStatus = SystemDetector.DetectAll();
                    // 依据上方两个框的二选一选择分流执行 (左: 图形与算力双轨解锁, 右: 固件级算力满血解锁)
                    bool isComputeOnly = (_radioMode2 != null && _radioMode2.IsChecked == true);
                    int channel = GetSelectedChannel();
                    string channelName = channel == 0 ? "现代 UEFI 直启" : (channel == 1 ? "老主板 Shell" : (channel == 2 ? "老卡驱动 Hook" : "U 盘直启"));

                    var gpus = NativeCore.DetectAllMiningGpus();
                    if (gpus == null || gpus.Count == 0)
                    {
                        ModernMessageBox.Show("未检测到 CMP 矿卡硬件，无法执行解锁！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // 通道 D：免安装 U 盘直启 (写入 FAT32 U 盘，不碰本机系统引导)
                    if (channel == 3)
                    {
                        string sel = Convert.ToString(_cmbUsbDrives != null ? _cmbUsbDrives.SelectedItem : "");
                        if (string.IsNullOrEmpty(sel) || sel.Length < 2 || sel.IndexOf("未检测", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            ModernMessageBox.Show("请先插入 FAT32 格式的可移动 U 盘，并在列表中选择盘符后重试！", "需要选择 U 盘", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        string driveLetter = sel.Substring(0, 2);

                        if (isComputeOnly)
                        {
                            // 写入 WildFlash1st 纯算力固件 U 盘
                            if (ModernMessageBox.Show("即将把 90HX 固件级【纯算力】解锁引导器写入 U 盘 (" + driveLetter + ")：\n\n• 打开被固件锁死的算力 (SEC2/PLM)，llama 推理提速约 16 倍\n• 需关闭主板 Secure Boot\n• 解锁为易失性：每次冷启动需从该 U 盘启动一次\n• 零系统侵入，不破坏电脑硬盘引导\n\n是否确认写入？", "确认制作固件级算力解锁 U 盘", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                                return;

                            _btnApplyHybridUnlock.IsEnabled = false;
                            LogAudit(">>> 正在制作 WildFlash1st 固件级纯算力解锁 U 盘 (" + driveLetter + ")...");
                            bool ok = await NativeCore.Create90hxFirmwareUsbAsync(driveLetter, true, LogCallback);
                            // 为本机所有矿卡预置驱动层 P0 锁定
                            foreach (var g in gpus)
                            {
                                await NativeCore.ApplySingleGpuComputeUnlockAsync(g, LogCallback);
                            }
                            await NativeCore.SoftRestartGpuDriverAsync(LogCallback);
                            _btnApplyHybridUnlock.IsEnabled = true;
                            RefreshSystemStatus();
                            RefreshMiningGpuList();

                            if (ok) ModernMessageBox.Show("【固件级纯算力解锁 U 盘制作完成】已写入 " + driveLetter + "\n\n开机按快捷键选该 U 盘启动，即可突破 90HX 算力锁，llama 大模型推理提速 16 倍！", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            // 写入图形+算力完整 U 盘
                            if (ModernMessageBox.Show("即将把 90HX 固件级【图形 + 算力完整】解锁引导器写入 U 盘 (" + driveLetter + ")：\n\n• 打开算力 + 图形渲染 + PCIe 速率提升\n• 需关闭主板 Secure Boot\n• 零系统侵入，即插即用\n\n是否确认写入？", "确认制作图形+算力完整 U 盘", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                                return;

                            _btnApplyHybridUnlock.IsEnabled = false;
                            LogAudit(">>> 正在制作图形 + 算力完整解锁 U 盘 (" + driveLetter + ")...");
                            bool ok = await NativeCore.Create90hxFirmwareUsbAsync(driveLetter, false, LogCallback);
                            _btnApplyHybridUnlock.IsEnabled = true;
                            RefreshSystemStatus();
                            RefreshMiningGpuList();

                            if (ok) ModernMessageBox.Show("【图形 + 算力完整解锁 U 盘制作完成】已写入 " + driveLetter + "\n\n开机从该 U 盘启动即可完整解锁！", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        return;
                    }

                    // 本机部署模式 (通道 A、B、C)
                    if (!isComputeOnly)
                    {
                        // 模式一：图形与算力双轨解锁 (需引导重启生效)
                        if (curStatus.PcieMode == NativeCore.PcieUnlockMode.Mode1_DirectBoot && curStatus.IsSpoofedTo3080)
                        {
                            var confirm = ModernMessageBox.Show("【防重复防呆提示】\n检测到当前矿卡已经处于【图形 + 算力完整解锁】状态：\n\n✓ PCIe 直启双保险固件已部署\n✓ 显卡已完成型号伪装\n\n无需重复解锁！是否仍要强制重新覆写并重载驱动？", "已处于完整解锁状态", MessageBoxButton.YesNo, MessageBoxImage.Question);
                            if (confirm != MessageBoxResult.Yes) return;
                        }

                        if (ModernMessageBox.Show(string.Format("即将为检测到的 {0} 张矿卡部署【图形 + 算力双轨解锁】：\n\n• 安装通道：{1}\n• 解锁 PCIe Gen2 x16 全速总线\n• 深度伪装为对应 GeForce 架构 (90HX→RTX3080 / 40HX→2060S / 50HX→2080Ti)\n• 激活 WDDM 3A 游戏光栅渲染与核显跨卡输出\n• 强制锁定 P0 满血算力\n\n是否确认立即执行？", gpus.Count, channelName), "确认执行全系完整解锁", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                            return;

                        _btnApplyHybridUnlock.IsEnabled = false;
                        LogAudit(">>> 开始执行全系矿卡图形 + 算力完整解锁 (通道: " + channelName + ")...");

                        await NativeCore.ApplyHybridMultiGpuUnlockAsync(gpus, true, true, channel, LogCallback);

                        _btnApplyHybridUnlock.IsEnabled = true;
                        RefreshSystemStatus();
                        RefreshMiningGpuList();

                        ModernMessageBox.Show("【全系图形 + 算力完整解锁已成功写入】\n\n✓ 引导通道「" + channelName + "」已部署\n✓ 各矿卡已按架构深度伪装为对应 GeForce 型号\n✓ 核心 P0 满载频率已激活\n\n重要：请将主机【完全关机】断电 10 秒后重新开机，使 UEFI 总线改写生效！", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        // 模式二：固件级算力满血解锁 (WildFlash1st 核心，llama 提速约 16 倍)
                        if (curStatus.PcieMode != NativeCore.PcieUnlockMode.None && channel == 0)
                        {
                            var confirm = ModernMessageBox.Show("【运行模式切换提示】\n检测到系统此前已配置了其他引导固件。\n\n即将自动切换部署为【WildFlash1st 固件级纯算力满血直启】。\n是否确认覆盖并切换？", "模式切换确认", MessageBoxButton.YesNo, MessageBoxImage.Question);
                            if (confirm != MessageBoxResult.Yes) return;
                        }

                        if (ModernMessageBox.Show(string.Format("即将为检测到的 {0} 张矿卡部署【固件级算力满血解锁 (WildFlash1st)】：\n\n• 安装通道：{1}\n• 硬件级击穿 SEC2/PLM 固件算力锁，llama 大模型推理提速 16 倍\n• 强制锁定 P0 满血计算频率，彻底解除 Compute Throttle 节流熔断\n• 专为 AI 纯算力定制，开机速度极快秒进系统\n\n是否确认立即执行？", gpus.Count, channelName), "确认执行固件级算力解锁", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                            return;

                        _btnApplyHybridUnlock.IsEnabled = false;
                        LogAudit(">>> 开始执行全系矿卡固件级算力满血解锁 (通道: " + channelName + ")...");

                        bool ok = await NativeCore.ApplyComputeFirmwareMultiGpuUnlockAsync(gpus, channel, null, LogCallback);
                        _btnApplyHybridUnlock.IsEnabled = true;

                        RefreshSystemStatus();
                        RefreshMiningGpuList();

                        if (ok)
                        {
                            ModernMessageBox.Show(string.Format("【固件级算力满血解锁已成功部署】\n\n✓ 引导通道「{0}」已部署就绪\n✓ WildFlash1st 核心：击穿 SEC2/PLM 固件硬件算力锁\n✓ llama 大模型推理实测提速约 16 倍 (230 → 3700 t/s)\n✓ 核心锁定 P0 满血频率，解除计算节流熔断\n\n重要：请将主机【完全关机】断电 10 秒后重新开机，使硬件固件接管生效！", channelName), "算力解锁成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                };
            }

            if (_btnRun40hxInstaller != null)
            {
                _btnRun40hxInstaller.Click += (s, e) =>
                {
                    string target = @"D:\app\build_source\mining_unlock_toolkit\40hx_release\40HXInstaller.exe";
                    if (File.Exists(target))
                    {
                        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
                    }
                    else
                    {
                        ModernMessageBox.Show("40HX 官方安装器文件未找到，请确认 toolkit 文件完整性。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                };
            }

            if (_btnRun40hxCheck != null)
            {
                _btnRun40hxCheck.Click += (s, e) =>
                {
                    string target = @"D:\app\build_source\mining_unlock_toolkit\40hx_release\40HXCheck.exe";
                    if (File.Exists(target))
                    {
                        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
                    }
                    else
                    {
                        ModernMessageBox.Show("40HX 官方诊断工具未找到，请确认 toolkit 文件完整性。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                };
            }

            if (_btnRun30hxSetup != null)
            {
                _btnRun30hxSetup.Click += (s, e) =>
                {
                    string target = @"D:\app\build_source\mining_unlock_toolkit\30hx_gen2\Setup_CMP30HX_WindowsAIO.bat";
                    if (File.Exists(target))
                    {
                        Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + target + "\"") { UseShellExecute = true, Verb = "runas" });
                    }
                    else
                    {
                        ModernMessageBox.Show("30HX 官方自动化脚本未找到，请确认 toolkit 目录完整性。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                };
            }

            if (_btnRun30hxReBar != null)
            {
                _btnRun30hxReBar.Click += (s, e) =>
                {
                    string target = @"D:\app\build_source\mining_unlock_toolkit\30hx_gen2\Setup_ReBar_CMP30HX.bat";
                    if (File.Exists(target))
                    {
                        Process.Start(new ProcessStartInfo("cmd.exe", "/c \"" + target + "\"") { UseShellExecute = true, Verb = "runas" });
                    }
                    else
                    {
                        ModernMessageBox.Show("30HX ReBar 脚本未找到，请确认 toolkit 目录完整性。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                };
            }

            if (_btnOpenToolkitFolder != null)
            {
                _btnOpenToolkitFolder.Click += (s, e) =>
                {
                    string dir = @"D:\app\build_source\mining_unlock_toolkit";
                    if (Directory.Exists(dir))
                    {
                        Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
                    }
                };
            }

            // 90HX 固件级算力解锁 (WildFlash1st 开源，写入 U 盘引导)
            if (_btnDeploy90hxCompute != null)
            {
                _btnDeploy90hxCompute.Click += async (s, e) =>
                {
                    string sel = Convert.ToString(_cmbUsbDrives != null ? _cmbUsbDrives.SelectedItem : "");
                    if (string.IsNullOrEmpty(sel) || sel.Length < 2 || sel.IndexOf("未检测", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ModernMessageBox.Show("请先在上方【通道 D · U盘直启】中选中一个 FAT32 格式的 U 盘盘符！", "需要选择 U 盘", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    string drive = sel.Substring(0, 2);
                    if (ModernMessageBox.Show("即将把 90HX 固件级【纯算力】解锁引导器写入 U 盘 (" + drive + ")：\n\n• 打开被固件锁死的算力，llama 推理提速约 16 倍\n• 需关闭主板 Secure Boot\n• 解锁为易失性：每次冷启动需从该 U 盘启动一次\n\n是否确认写入？", "确认制作固件级算力解锁 U 盘", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                        return;
                    _btnDeploy90hxCompute.IsEnabled = false;
                    bool ok = await NativeCore.Create90hxFirmwareUsbAsync(drive, true, LogCallback);
                    _btnDeploy90hxCompute.IsEnabled = true;
                    if (ok) ModernMessageBox.Show("【固件级纯算力解锁 U 盘制作完成】已写入 " + drive + "\n\n开机从该 U 盘启动即可解锁 90HX 算力！", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                };
            }
            if (_btnDeploy90hxFull != null)
            {
                _btnDeploy90hxFull.Click += async (s, e) =>
                {
                    string sel = Convert.ToString(_cmbUsbDrives != null ? _cmbUsbDrives.SelectedItem : "");
                    if (string.IsNullOrEmpty(sel) || sel.Length < 2 || sel.IndexOf("未检测", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ModernMessageBox.Show("请先在上方【通道 D · U盘直启】中选中一个 FAT32 格式的 U 盘盘符！", "需要选择 U 盘", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    string drive = sel.Substring(0, 2);
                    if (ModernMessageBox.Show("即将把 90HX 固件级【图形+算力完整】解锁引导器写入 U 盘 (" + drive + ")：\n\n• 打开算力 + 图形渲染 + PCIe 提升\n• 需关闭主板 Secure Boot\n• 解锁为易失性：每次冷启动需从该 U 盘启动一次\n\n是否确认写入？", "确认制作固件级完整解锁 U 盘", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                        return;
                    _btnDeploy90hxFull.IsEnabled = false;
                    bool ok = await NativeCore.Create90hxFirmwareUsbAsync(drive, false, LogCallback);
                    _btnDeploy90hxFull.IsEnabled = true;
                    if (ok) ModernMessageBox.Show("【固件级图形+算力完整解锁 U 盘制作完成】已写入 " + drive + "\n\n开机从该 U 盘启动即可解锁！", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                };
            }

            // 双卡调用方案事件
            _btnDualGpuAutoScan.Click += async (s, e) =>
            {
                _btnDualGpuAutoScan.IsEnabled = false;
                int added = await NativeCore.AutoScanAndBindCommonGamesAsync(LogCallback);
                _btnDualGpuAutoScan.IsEnabled = true;
                RefreshDualGpuStatus();
                ModernMessageBox.Show("全盘扫描完成！共已配置 " + added + " 款游戏独占 90HX 高性能渲染！", "扫描完成", MessageBoxButton.OK, MessageBoxImage.Information);
            };

            _btnDualGpuBrowseAdd.Click += (s, e) =>
            {
                using (var ofd = new System.Windows.Forms.OpenFileDialog())
                {
                    ofd.Filter = "游戏可执行文件 (*.exe)|*.exe|所有文件 (*.*)|*.*";
                    ofd.Title = "请选择需要通过 90HX 渲染的游戏主程序 exe";
                    if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        bool ok = NativeCore.AddAppToGpuPreference(ofd.FileName, LogCallback);
                        RefreshDualGpuStatus();
                        if (ok) ModernMessageBox.Show("已将 " + Path.GetFileName(ofd.FileName) + " 成功绑定至 90HX 高性能渲染！", "添加成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            };

            _btnDualGpuAddFromRunning.Click += (s, e) =>
            {
                var procs = NativeCore.GetRunningWindowProcesses();
                if (procs.Count == 0)
                {
                    ModernMessageBox.Show("当前未检测到带有窗口的前台游戏或应用程序进程！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var winSelect = new Window
                {
                    Title = "从当前运行程序中选择游戏",
                    Width = 500,
                    Height = 400,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = _window,
                    Background = new SolidColorBrush(Color.FromRgb(24, 27, 32)),
                    Foreground = Brushes.White
                };

                var sp = new StackPanel { Margin = new Thickness(14) };
                sp.Children.Add(new TextBlock { Text = "选择您正在运行的游戏，一键绑定至 90HX 渲染:", Margin = new Thickness(0, 0, 0, 10), Foreground = new SolidColorBrush(Color.FromRgb(71, 85, 105)) });

                var lb = new ListBox { Height = 260, Margin = new Thickness(0, 0, 0, 12), Background = Brushes.White, Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)), BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)) };
                foreach (var p in procs) lb.Items.Add(p);
                if (lb.Items.Count > 0) lb.SelectedIndex = 0;
                sp.Children.Add(lb);

                var btnConfirm = new Button { Content = "立即绑定至 90HX 渲染", Height = 32, Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
                btnConfirm.Click += (bs, be) =>
                {
                    var sel = lb.SelectedItem as NativeCore.RunningProcessItem;
                    if (sel != null && !string.IsNullOrEmpty(sel.ExePath))
                    {
                        NativeCore.AddAppToGpuPreference(sel.ExePath, LogCallback);
                        RefreshDualGpuStatus();
                        winSelect.Close();
                        ModernMessageBox.Show("已成功绑定: " + sel.ProcessName, "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                };
                sp.Children.Add(btnConfirm);
                winSelect.Content = sp;
                winSelect.ShowDialog();
            };

            _btnDualGpuRemoveSelected.Click += (s, e) =>
            {
                var sel = _lstBoundGames.SelectedItem as string;
                if (!string.IsNullOrEmpty(sel) && !sel.StartsWith("暂未绑定游戏"))
                {
                    NativeCore.RemoveAppFromGpuPreference(sel, LogCallback);
                    RefreshDualGpuStatus();
                }
                else
                {
                    ModernMessageBox.Show("请先在列表中选中需要移除的游戏项目！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };

            // 清空列表（防空重复清空）
            _btnDualGpuClearAll.Click += (s, e) =>
            {
                var list = NativeCore.GetConfiguredGpuPreferenceApps();
                if (list.Count == 0)
                {
                    ModernMessageBox.Show("【列表已为空】\n当前没有配置任何 GPU 偏好程序，无需清空！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (ModernMessageBox.Show("确定要清空所有已绑定的 " + list.Count + " 款游戏配置吗？", "确认清空", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;

                foreach (var item in list)
                {
                    NativeCore.RemoveAppFromGpuPreference(item, null);
                }
                RefreshDualGpuStatus();
                LogAudit("已清空所有通过控制中心配置的 GPU 偏好列表。");
                ModernMessageBox.Show("已清空所有 GPU 偏好列表！", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
            };

            _btnDualGpuSoftRestartDwm.Click += async (s, e) =>
            {
                await NativeCore.SoftRestartGpuDriverAsync(LogCallback);
                RefreshSensors();
            };

            // 全局 DirectX 锁定 90HX（防重复检测）
            if (_btnDualGpuSetDxGlobal != null)
            {
                _btnDualGpuSetDxGlobal.Click += (s, e) =>
                {
                    if (NativeCore.IsDirectXGlobalDefaultGpuApplied())
                    {
                        var confirm = ModernMessageBox.Show("【防重复配置提示】\n当前系统全局 DirectX 默认首选显卡已锁定为 90HX！\n\n所有 3D 程序与游戏启动时已默认使用 90HX 渲染。是否重新写入注册表？", "已生效", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (confirm != MessageBoxResult.Yes) return;
                    }

                    bool ok = NativeCore.ApplyDirectXGlobalDefaultGpu(LogCallback);
                    if (ok)
                    {
                        ModernMessageBox.Show("已成功将系统全局 DirectX 默认首选显卡锁定为 90HX！\n全局游戏在启动时将直接调度 90HX 进行满血 3D 渲染，无需逐个配置。", "设置成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                };
            }

            // OpenGL ICD 锁定 90HX（防重复检测）
            if (_btnDualGpuSetOpenGlIcd != null)
            {
                _btnDualGpuSetOpenGlIcd.Click += (s, e) =>
                {
                    if (NativeCore.IsOpenGlIcdApplied())
                    {
                        var confirm = ModernMessageBox.Show("【防重复配置提示】\n当前系统 OpenGL ICD 驱动已锁定至 NVIDIA 90HX (nvoglv64.dll)！\n\n是否重新确认并覆盖写入？", "已生效", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        if (confirm != MessageBoxResult.Yes) return;
                    }

                    bool ok = NativeCore.ApplyOpenGLIcdTo90hx(LogCallback);
                    if (ok)
                    {
                        ModernMessageBox.Show("已成功将系统 OpenGL ICD 驱动锁定至 90HX！\n模拟器 (如 Yuzu/Ryujinx) 及专业软件将强制使用 90HX 进行硬件光栅化与渲染。", "配置成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                };
            }

            _btnDualGpuOpenWinGraphics.Click += (s, e) =>
            {
                NativeCore.EnableWindowsAdvancedGpuSettings(LogCallback);
            };

            // DLSS 游戏目录选择
            _btnBrowseGameDir.Click += (s, e) =>
            {
                using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
                {
                    fbd.Description = "请选择 3A 游戏的主程序目录 (包含 exe 的文件夹)";
                    if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        _txtDlssGameDir.Text = fbd.SelectedPath;
                        CheckDlssInDir(fbd.SelectedPath);
                    }
                }
            };

            // DLSS 3 注入（防重复检测）
            _btnInjectDlss.Click += (s, e) =>
            {
                string dir = _txtDlssGameDir.Text.Trim();
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                {
                    ModernMessageBox.Show("请先选择有效的目标游戏程序目录！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                bool hasVersionDll = File.Exists(Path.Combine(dir, "version.dll"));
                bool hasIni = File.Exists(Path.Combine(dir, "dlssg_sm86.ini"));
                if (hasVersionDll && hasIni)
                {
                    var confirm = ModernMessageBox.Show("【防重复注入检测】\n检测到该游戏目录下已经存在 DLSS-G 补丁核心文件 (version.dll + dlssg_sm86.ini)！\n\n是否确认使用当前参数重新覆盖替换？", "补丁已存在", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (confirm != MessageBoxResult.Yes) return;
                }

                string opt = "1";
                if (_cmbOptimizedLevel.SelectedIndex >= 0)
                {
                    string t = ((ComboBoxItem)_cmbOptimizedLevel.SelectedItem).Content.ToString();
                    opt = t.Substring(0, 1);
                }
                string maxF = "4";
                if (_cmbMaxFramesLevel.SelectedIndex >= 0)
                {
                    string t = ((ComboBoxItem)_cmbMaxFramesLevel.SelectedItem).Content.ToString();
                    maxF = t.Substring(0, 1);
                }
                bool injectRe = _chkInjectReFramework != null && _chkInjectReFramework.IsChecked == true;

                bool ok = NativeCore.InjectDlssG(dir, opt, maxF, injectRe, LogCallback);
                CheckDlssInDir(dir);
                if (ok) ModernMessageBox.Show("DLSS 3 补丁注入成功！启动游戏并在画质设置中开启【帧生成】即可。" + (injectRe ? "\n(已附带注入 RE 框架代理 dinput8.dll)" : ""), "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            };

            // DLSS 补丁清除（防空重复清除）
            _btnRemoveDlss.Click += (s, e) =>
            {
                string dir = _txtDlssGameDir.Text.Trim();
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                {
                    ModernMessageBox.Show("请先选择目标游戏目录！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                bool hasVersionDll = File.Exists(Path.Combine(dir, "version.dll"));
                bool hasIni = File.Exists(Path.Combine(dir, "dlssg_sm86.ini"));
                bool hasRe = File.Exists(Path.Combine(dir, "dinput8.dll"));

                if (!hasVersionDll && !hasIni && !hasRe)
                {
                    ModernMessageBox.Show("【无需清理】\n该游戏目录下未检测到任何 DLSS-G 补丁或相关组件，目录已是干净状态！", "已是纯净状态", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                bool ok = NativeCore.RemoveDlssG(dir, LogCallback);
                CheckDlssInDir(dir);
                if (ok) ModernMessageBox.Show("已从游戏目录清除 DLSS 补丁及相关组件！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            };

            if (_btnCheckDlssUpdate != null)
            {
                _btnCheckDlssUpdate.Click += async (s, e) =>
                {
                    _btnCheckDlssUpdate.IsEnabled = false;
                    _txtDlssUpdateStatus.Text = "正在连接所选镜像检索最新版本...";
                    _txtDlssUpdateStatus.Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235));

                    int idx = _cmbDlssMirrorSource != null ? _cmbDlssMirrorSource.SelectedIndex : 0;
                    var res = await NativeCore.CheckDlssUpdateAsync(idx, LogCallback);
                    _btnCheckDlssUpdate.IsEnabled = true;

                    if (res.Success)
                    {
                        if (_txtDlssLocalVersion != null) _txtDlssLocalVersion.Text = "v" + res.LocalVersion;
                        if (res.HasUpdate)
                        {
                            _txtDlssUpdateStatus.Text = "发现新版本: v" + res.RemoteVersion + " (当前: v" + res.LocalVersion + ")，点击【一键拉取并替换】即可升级！";
                            _txtDlssUpdateStatus.Foreground = new SolidColorBrush(Color.FromRgb(217, 119, 6));
                        }
                        else
                        {
                            _txtDlssUpdateStatus.Text = "当前运行库已是最新版本 (v" + res.LocalVersion + ")，来自: " + res.MirrorUsed;
                            _txtDlssUpdateStatus.Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74));
                        }
                    }
                    else
                    {
                        _txtDlssUpdateStatus.Text = "检测提示: " + res.ErrorMessage;
                        _txtDlssUpdateStatus.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                    }
                };
            }

            if (_btnDownloadDlssUpdate != null)
            {
                _btnDownloadDlssUpdate.Click += async (s, e) =>
                {
                    _btnDownloadDlssUpdate.IsEnabled = false;
                    if (_progDlssDownload != null)
                    {
                        _progDlssDownload.Visibility = Visibility.Visible;
                        _progDlssDownload.Value = 10;
                    }
                    _txtDlssUpdateStatus.Text = "正在从所选高速镜像源下载最新 DLSS 运行库...";
                    _txtDlssUpdateStatus.Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235));

                    int idx = _cmbDlssMirrorSource != null ? _cmbDlssMirrorSource.SelectedIndex : 0;
                    string gameDir = _txtDlssGameDir != null ? _txtDlssGameDir.Text.Trim() : "";

                    var res = await NativeCore.PullAndReplaceDlssAsync(idx, gameDir, p =>
                    {
                        Dispatcher.Invoke(new Action(() => { if (_progDlssDownload != null) _progDlssDownload.Value = p; }));
                    }, LogCallback);

                    _btnDownloadDlssUpdate.IsEnabled = true;
                    if (_progDlssDownload != null) _progDlssDownload.Visibility = Visibility.Collapsed;

                    if (res.Success)
                    {
                        if (_txtDlssLocalVersion != null) _txtDlssLocalVersion.Text = "v" + res.NewVersion;
                        string tip = "已成功更新 DLSS 运行库至 " + res.NewVersion;
                        if (res.GameDirUpdated) tip += "，并已直接同步替换游戏目录中的补丁！";
                        _txtDlssUpdateStatus.Text = tip;
                        _txtDlssUpdateStatus.Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74));
                        ModernMessageBox.Show(tip, "更新成功", MessageBoxButton.OK, MessageBoxImage.Information);
                        if (!string.IsNullOrEmpty(gameDir)) CheckDlssInDir(gameDir);
                    }
                    else
                    {
                        _txtDlssUpdateStatus.Text = "更新失败: " + res.Message;
                        _txtDlssUpdateStatus.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                    }
                };
            }

            if (_btnOpenDlssRepo != null)
            {
                _btnOpenDlssRepo.Click += (s, e) =>
                {
                    try { Process.Start("https://github.com/sdli1995/dlssg_for_sm86"); } catch { }
                };
            }

            // 系统调试工具
            _btnToolRestartDriver.Click += async (s, e) =>
            {
                await NativeCore.SoftRestartGpuDriverAsync(LogCallback);
                RefreshSystemStatus();
                RefreshSensors();
            };

            _btnToolToggleHags.Click += (s, e) =>
            {
                bool cur = NativeCore.IsHagsEnabled();
                NativeCore.SetHagsEnabled(!cur, LogCallback);
                RefreshSystemStatus();
            };

            _btnToolEnableUltimatePower.Click += (s, e) =>
            {
                NativeCore.EnableUltimatePerformancePowerPlan(LogCallback);
            };

            _btnToolDevMgmt.Click += (s, e) => Process.Start("devmgmt.msc");
            _btnToolDxDiag.Click += (s, e) => Process.Start("dxdiag.exe");
            if (_btnToolGpuDiag != null)
            {
                _btnToolGpuDiag.Click += (s, e) =>
                {
                    RefreshSensors();
                    ModernMessageBox.Show("GPU 硬件总线诊断完毕！已刷新当前链路状态与传感器数据。", "GPU 总线核查", MessageBoxButton.OK, MessageBoxImage.Information);
                };
            }
                        if (_btnToolOpenDataDir != null)
            {
                _btnToolOpenDataDir.Click += (s, e) =>
                {
                    try
                    {
                        string dataDir = NativeCore.GetDataDirectory();
                        Process.Start("explorer.exe", "\"" + dataDir + "\"");
                    }
                    catch (Exception exDir)
                    {
                        LogAudit("打开数据目录提示: " + exDir.Message);
                    }
                };
            }

            _btnToolNvCpl.Click += (s, e) =>
            {
                try
                {
                    Process.Start("nvcplui.exe");
                }
                catch
                {
                    string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"NVIDIA Corporation\Control Panel Client\nvcplui.exe");
                    if (File.Exists(path)) Process.Start(path);
                    else LogAudit("未找到 NVIDIA 原生控制面板程序。");
                }
            };

            // 日志
            _btnClearLogs.Click += (s, e) => _txtLogBox.Clear();
            _btnCopyLogs.Click += (s, e) =>
            {
                if (!string.IsNullOrEmpty(_txtLogBox.Text))
                {
                    Clipboard.SetText(_txtLogBox.Text);
                    ModernMessageBox.Show("日志已复制到剪贴板！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };

            LoadUsbDrives();

            if (_window != null)
            {
                _window.Closed += (s, e) =>
                {
                    try
                    {
                        if (_sensorTimer != null)
                        {
                            _sensorTimer.Stop();
                            _sensorTimer = null;
                        }
                    }
                    catch { }
                    Application.Current.Shutdown();
                };
            }
        }

        private void CheckAdminPrivilege()
        {
            bool admin = NativeCore.IsAdmin();
            if (!admin && _bannerAdminWarning != null)
            {
                _bannerAdminWarning.Visibility = Visibility.Visible;
            }
        }

        private bool EnsureAdminPrivilege()
        {
            if (NativeCore.IsAdmin()) return true;
            var res = ModernMessageBox.Show("修改显卡型号与驱动配置需要系统管理员权限，是否立即以管理员身份重启软件？", "权限提示", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = Process.GetCurrentProcess().MainModule.FileName,
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    Process.Start(psi);
                    Application.Current.Shutdown();
                }
                catch { }
            }
            return false;
        }

        private void InitSensorTimer()
        {
            _sensorTimer = new DispatcherTimer();
            _sensorTimer.Interval = TimeSpan.FromSeconds(1.5);
            _sensorTimer.Tick += (s, e) =>
            {
                if (_chkAutoRefreshSensors != null && _chkAutoRefreshSensors.IsChecked == true)
                {
                    RefreshSensors();
                }
            };
            _sensorTimer.Start();
        }

        private async void RefreshSensors()
        {
            try
            {
                var snap = await Task.Run(() => NativeCore.QueryGpuSensors());
                if (snap != null && snap.IsValid && _window != null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (_txtTopSensors != null) _txtTopSensors.Text = snap.Temperature + " ℃ | " + snap.PowerDraw.ToString("F1") + " W";
                        if (_txtSensTemp != null) _txtSensTemp.Text = snap.Temperature + " ℃";
                        if (_barSensTemp != null) _barSensTemp.Value = snap.Temperature;

                        if (_txtSensFan != null) _txtSensFan.Text = snap.FanPercent + " %";
                        if (_barSensFan != null) _barSensFan.Value = snap.FanPercent;

                        if (_txtSensPower != null) _txtSensPower.Text = snap.PowerDraw.ToString("F1") + " W / " + snap.PowerLimit.ToString("F0") + " W";
                        if (_barSensPower != null)
                        {
                            _barSensPower.Maximum = snap.PowerLimit > 0 ? snap.PowerLimit : 250;
                            _barSensPower.Value = snap.PowerDraw;
                        }

                        if (_txtSensVram != null) _txtSensVram.Text = snap.MemoryUsedMB + " MB / " + snap.MemoryTotalMB + " MB";
                        if (_barSensVram != null)
                        {
                            _barSensVram.Maximum = snap.MemoryTotalMB > 0 ? snap.MemoryTotalMB : 10240;
                            _barSensVram.Value = snap.MemoryUsedMB;
                        }

                        if (_txtSensUtil != null) _txtSensUtil.Text = snap.GpuUtilPercent + " %";
                        if (_barSensUtil != null) _barSensUtil.Value = snap.GpuUtilPercent;

                        if (_txtSensClocks != null) _txtSensClocks.Text = snap.GraphicsClockMHz + " MHz / " + snap.MemoryClockMHz + " MHz";
                        if (_txtSensPstate != null) _txtSensPstate.Text = "P-State 状态: " + snap.PState;

                        if (_txtSensPcieLink != null) _txtSensPcieLink.Text = "当前链路状态: " + snap.PcieFormatted;
                        if (_txtTopPcieStatus != null) _txtTopPcieStatus.Text = snap.PcieGen == "2" ? "PCIe x" + snap.PcieWidth + " 2.0 (已解锁)" : "PCIe x" + snap.PcieWidth + " " + snap.PcieGen + ".0";
                    });
                }
            }
            catch { }
        }

        private void RefreshSystemStatus()
        {
            var status = SystemDetector.DetectAll();
            if (_txtDlssLocalVersion != null) _txtDlssLocalVersion.Text = "v" + NativeCore.GetLocalDlssVersion();

            // 顶部状态
            if (status.IsSpoofedTo3080)
            {
                _txtTopGpuModel.Text = "RTX 3080 (已伪装)";
                _txtTopGpuModel.Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74));
            }
            else
            {
                _txtTopGpuModel.Text = status.GpuDesc;
                _txtTopGpuModel.Foreground = new SolidColorBrush(Color.FromRgb(217, 119, 6));
            }

            _txtTopOverlayStatus.Text = status.OverlayPatched ? "已激活 (免检)" : "未激活 / 官方锁定";
            _txtTopOverlayStatus.Foreground = status.OverlayPatched ? new SolidColorBrush(Color.FromRgb(22, 163, 74)) : new SolidColorBrush(Color.FromRgb(100, 116, 139));

            // 型号伪装
            _txtClassSubKey.Text = status.ClassSubKey;
            _txtCurrentDriverDesc.Text = status.GpuDesc;
            _txtCurrentGpuMatchId.Text = status.GpuId;
            _txtDirectXDesc.Text = status.IsSpoofedTo3080 ? "NVIDIA GeForce RTX 3080 (已同步)" : "NVIDIA CMP 90HX";

            // 游戏覆盖
            _txtNvAppPath.Text = string.IsNullOrEmpty(status.NvAppPath) ? "未安装 NVIDIA App" : status.NvAppPath;
            _txtOverlayPatchState.Text = status.OverlayPatched ? "已成功注入免检补丁 (ShadowPlay 白名单已解开)" : "未注入补丁 (受官方硬件白名单限制)";
            _txtOverlayPatchState.Foreground = status.OverlayPatched ? new SolidColorBrush(Color.FromRgb(22, 163, 74)) : new SolidColorBrush(Color.FromRgb(217, 119, 6));
            _txtOverlayBakState.Text = status.OverlayBakExists ? "main.*.js.bak (官方备份存在，随时可一键还原)" : "未检测到备份";

            // PCIe 引导模式
            _txtPcieCurrentMode.Text = status.PcieModeDesc;
            if (status.PcieMode == NativeCore.PcieUnlockMode.Mode1_DirectBoot)
            {
                _txtPcieCurrentMode.Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74));
                _txtPcieBcdHealth.Text = "健康 (已配置 Windows Boot Manager 自动兜底)";
                _txtPcieBcdHealth.Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74));
            }
            else if (status.PcieMode == NativeCore.PcieUnlockMode.Mode2_UefiShell)
            {
                _txtPcieCurrentMode.Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                _txtPcieBcdHealth.Text = "健康 (Shell.efi 引导项活跃)";
            }
            else if (status.PcieMode == NativeCore.PcieUnlockMode.DirtyState)
            {
                _txtPcieCurrentMode.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                _txtPcieBcdHealth.Text = "检测到历史残留引导项，建议点击【BCD 引导健康自检与修复】";
                _txtPcieBcdHealth.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38));
            }
            else
            {
                _txtPcieCurrentMode.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                _txtPcieBcdHealth.Text = "官方原生干净状态 (未安装 PCIe 解锁)";
            }

            _txtSecureBootState.Text = status.SecureBootEnabled ? "已开启 (Enabled) - 警告：会导致自定义 EFI 固件无法加载，请进 BIOS 关闭！" : "已关闭 (Disabled) - 正常，允许自定义 EFI 加载";
            _txtSecureBootState.Foreground = status.SecureBootEnabled ? new SolidColorBrush(Color.FromRgb(220, 38, 38)) : new SolidColorBrush(Color.FromRgb(22, 163, 74));

            // 系统调试工具状态
            _txtToolHagsStatus.Text = "状态: " + (status.IsHagsEnabled ? "已开启 (推荐)" : "已关闭");
            _txtToolHagsStatus.Foreground = status.IsHagsEnabled ? new SolidColorBrush(Color.FromRgb(22, 163, 74)) : new SolidColorBrush(Color.FromRgb(100, 116, 139));

            RefreshDualGpuStatus();
            RefreshMiningGpuList();
            RefreshFullHardwareDisplay(status);
            UpdateStatusBar();
        }

        private void RefreshFullHardwareDisplay(SystemStatus status)
        {
            try
            {
                var hw = status.Hardware;
                if (hw == null) return;

                if (_txtHwCpuName != null) _txtHwCpuName.Text = hw.CpuName;
                if (_txtHwCpuCores != null) _txtHwCpuCores.Text = hw.CpuCores;
                if (_txtHwCpuClock != null) _txtHwCpuClock.Text = hw.CpuClock;

                if (_lstAllGpuDevices != null)
                {
                    _lstAllGpuDevices.Items.Clear();
                    foreach (var g in hw.GpuList)
                    {
                        string line = string.Format("{0} [{1}] · {2} · 驱动: {3}", g.Name, g.MemorySize, g.RoleTag, g.DriverVersion);
                        _lstAllGpuDevices.Items.Add(line);
                    }
                    if (_lstAllGpuDevices.Items.Count > 0) _lstAllGpuDevices.SelectedIndex = 0;
                }

                if (_txtHwGpuCount != null)
                {
                    _txtHwGpuCount.Text = string.Format("已识别 {0} 块显卡资产", hw.GpuList.Count);
                }

                if (_txtHwMotherboard != null) _txtHwMotherboard.Text = hw.MotherboardModel;
                if (_txtHwBiosVer != null) _txtHwBiosVer.Text = hw.BiosVersion;
                if (_txtHwUefiState != null) _txtHwUefiState.Text = hw.UefiStatus;
                if (_txtHwSecureBoot != null) _txtHwSecureBoot.Text = "Secure Boot: " + hw.SecureBootStatus;
                if (_txtHwHags != null) _txtHwHags.Text = "HAGS 加速: " + hw.HagsStatus;
                if (_txtHwMemory != null) _txtHwMemory.Text = hw.TotalMemory;
                if (_txtHwOsVer != null) _txtHwOsVer.Text = hw.OsVersion;

                // 刷新多卡伪装下拉选择器
                if (_cmbSpoofTargetCard != null)
                {
                    int oldSel = _cmbSpoofTargetCard.SelectedIndex;
                    _cmbSpoofTargetCard.Items.Clear();
                    var gpus = NativeCore.DetectAllMiningGpus();
                    foreach (var g in gpus)
                    {
                        _cmbSpoofTargetCard.Items.Add(string.Format("[槽位 {0}] {1} ➔ 伪装为: {2}", g.SlotIndex + 1, g.DetectedModel, g.TargetGeForceName));
                    }
                    if (_cmbSpoofTargetCard.Items.Count > 0)
                    {
                        _cmbSpoofTargetCard.SelectedIndex = (oldSel >= 0 && oldSel < _cmbSpoofTargetCard.Items.Count) ? oldSel : 0;
                    }
                }
            }
            catch { }
        }

        private void RefreshMiningGpuList()
        {
            try
            {
                _cachedMiningGpus = NativeCore.DetectAllMiningGpus();
                if (_lstMiningGpus != null)
                {
                    _lstMiningGpus.Items.Clear();
                    foreach (var g in _cachedMiningGpus)
                    {
                        _lstMiningGpus.Items.Add(g.DisplaySummary);
                    }
                    if (_lstMiningGpus.Items.Count > 0)
                    {
                        _lstMiningGpus.SelectedIndex = 0;
                    }
                }

                // 检测是否真实存在 CMP 矿卡 (30/40/50/90HX 全系)
                bool hasMiningGpu = (_cachedMiningGpus != null && _cachedMiningGpus.Count > 0);

                // 智能感知：检测到矿卡则完整呈现解锁卡片，未检测到则显示温和提示
                if (_bannerNo90hxWarning != null)
                {
                    _bannerNo90hxWarning.Visibility = hasMiningGpu ? Visibility.Collapsed : Visibility.Visible;
                }
                if (_card90hxStatusInfo != null)
                {
                    _card90hxStatusInfo.Opacity = hasMiningGpu ? 1.0 : 0.85;
                }
                if (_card90hxUnlockOptions != null)
                {
                    _card90hxUnlockOptions.Visibility = hasMiningGpu ? Visibility.Visible : Visibility.Collapsed;
                }
                if (_btnApplyHybridUnlock != null)
                {
                    _btnApplyHybridUnlock.IsEnabled = hasMiningGpu;
                }

                if (_txtMiningGpuSummary != null)
                {
                    if (hasMiningGpu)
                    {
                        if (_cachedMiningGpus.Count == 1)
                        {
                            var g = _cachedMiningGpus[0];
                            _txtMiningGpuSummary.Text = g.DetectedModel + " (" + g.VramCapacity + ") · " + g.ChipArchitecture + " 已就绪";
                        }
                        else
                        {
                            var names = _cachedMiningGpus.Select(x => x.DetectedModel).ToList();
                            _txtMiningGpuSummary.Text = string.Join(" + ", names) + " (共 " + _cachedMiningGpus.Count + " 张矿卡混合已就绪)";
                        }
                    }
                    else
                    {
                        _txtMiningGpuSummary.Text = "未检测到 CMP 矿卡硬件";
                    }
                }
            }
            catch { }
        }

        private void UpdateUnlockCardHighlight()
        {
            try
            {
                bool g = (_radioMode1 != null && _radioMode1.IsChecked == true);
                bool c = (_radioMode2 != null && _radioMode2.IsChecked == true);

                if (_cardUnlockGraphics != null)
                {
                    _cardUnlockGraphics.BorderBrush = new SolidColorBrush(Color.FromRgb(g ? (byte)0 : (byte)198, g ? (byte)113 : (byte)224, g ? (byte)227 : (byte)250));
                    _cardUnlockGraphics.BorderThickness = g ? new Thickness(2) : new Thickness(1);
                }
                if (_cardUnlockCompute != null)
                {
                    _cardUnlockCompute.BorderBrush = new SolidColorBrush(Color.FromRgb(c ? (byte)168 : (byte)233, c ? (byte)85 : (byte)213, c ? (byte)247 : (byte)255));
                    _cardUnlockCompute.BorderThickness = c ? new Thickness(2) : new Thickness(1);
                }
            }
            catch { }
        }

        private void UpdateUsbPickerVisibility()
        {
            bool usb = (_radioChannelD != null && _radioChannelD.IsChecked == true);
            if (_panelUsbPicker != null)
            {
                _panelUsbPicker.Visibility = usb ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private int GetSelectedChannel()
        {
            if (_radioChannelB != null && _radioChannelB.IsChecked == true) return 1;
            if (_radioChannelC != null && _radioChannelC.IsChecked == true) return 2;
            if (_radioChannelD != null && _radioChannelD.IsChecked == true) return 3;
            return 0;
        }

        private void RefreshDualGpuStatus()
        {
            try
            {
                var topo = NativeCore.DetectDualGpuTopology();
                if (_txtDualGpuRenderCard != null)
                {
                    _txtDualGpuRenderCard.Text = topo.HasRenderGpu ? topo.RenderGpuName + " · 3D 渲染核心 (GA102)" : "未检测到 90HX";
                }
                if (_txtDualGpuDisplayCard != null)
                {
                    _txtDualGpuDisplayCard.Text = topo.HasDisplayGpu ? topo.DisplayGpuName + " · 视频信号输出卡 (已连显示器)" : "未检测到核显/亮机卡";
                }
                if (_txtDualGpuHagsState != null)
                {
                    _txtDualGpuHagsState.Text = "跨卡加速: " + (topo.HagsActive ? "硬件加速 GPU 计划 (HAGS) 已就绪，CASO 极速拷贝" : "建议开启 HAGS 以降低跨卡延迟");
                    _txtDualGpuHagsState.Foreground = topo.HagsActive ? new SolidColorBrush(Color.FromRgb(22, 163, 74)) : new SolidColorBrush(Color.FromRgb(217, 119, 6));
                }

                var apps = NativeCore.GetConfiguredGpuPreferenceApps();
                if (_txtBoundGamesSummary != null)
                {
                    _txtBoundGamesSummary.Text = "已绑定 " + apps.Count + " 款游戏/程序";
                }
                if (_lstBoundGames != null)
                {
                    _lstBoundGames.Items.Clear();
                    foreach (var a in apps)
                    {
                        _lstBoundGames.Items.Add(a);
                    }
                    if (_lstBoundGames.Items.Count == 0)
                    {
                        _lstBoundGames.Items.Add("暂未绑定游戏。点击上方【一键全盘扫描并绑定常见游戏】或【浏览添加】");
                    }
                }
            }
            catch { }
        }

        private void LoadUsbDrives()
        {
            _cmbUsbDrives.Items.Clear();
            var drives = NativeCore.GetAvailableRemovableDrives();
            foreach (var d in drives)
            {
                _cmbUsbDrives.Items.Add(d);
            }
            if (_cmbUsbDrives.Items.Count > 0)
            {
                _cmbUsbDrives.SelectedIndex = 0;
            }
            else
            {
                _cmbUsbDrives.Items.Add("未检测到可移动 U 盘 (请插入 FAT32 U 盘后刷新)");
                _cmbUsbDrives.SelectedIndex = 0;
            }
        }

        private void CheckDlssInDir(string dir)
        {
            if (Directory.Exists(dir))
            {
                bool dll = File.Exists(Path.Combine(dir, "version.dll"));
                bool ini = File.Exists(Path.Combine(dir, "dlssg_sm86.ini"));
                bool re = File.Exists(Path.Combine(dir, "dinput8.dll"));
                if (dll && ini)
                {
                    string status = "已检测到 DLSS-G 补丁 (version.dll + dlssg_sm86.ini 正常运行中)";
                    if (re) status += " [含 RE 框架支持]";
                    _txtDlssStatusInDir.Text = status;
                    _txtDlssStatusInDir.Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74));
                    if (re && _chkInjectReFramework != null) _chkInjectReFramework.IsChecked = true;
                }
                else
                {
                    _txtDlssStatusInDir.Text = "该目录尚未注入 DLSS-G 补丁";
                    _txtDlssStatusInDir.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
                }
            }
        }

        private void LogCallback(string msg, bool isError)
        {
            Dispatcher.Invoke(() =>
            {
                LogAudit((isError ? "[ERROR] " : "[INFO] ") + msg);
            });
        }

        private void LogAudit(string msg)
        {
            if (_txtLogBox != null)
            {
                string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg;
                _txtLogBox.AppendText(line + "\r\n");
                _txtLogBox.ScrollToEnd();
            }
        }
    }

    /// <summary>
    /// 现代极简提示框 (替代系统原生 MessageBox，与 Clean Studio 风格统一)
    /// </summary>
    public static class ModernMessageBox
    {
        public static MessageBoxResult Show(string text, string caption = "90HX Studio", MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
        {
            MessageBoxResult result = MessageBoxResult.None;

            string glyph;
            Color iconColor;
            switch (icon)
            {
                case MessageBoxImage.Warning: glyph = "!"; iconColor = Color.FromRgb(245, 158, 11); break;
                case MessageBoxImage.Error: glyph = "✕"; iconColor = Color.FromRgb(239, 68, 68); break;
                case MessageBoxImage.Question: glyph = "?"; iconColor = Color.FromRgb(0, 113, 227); break;
                default: glyph = "i"; iconColor = Color.FromRgb(0, 113, 227); break;
            }

            var win = new Window
            {
                Title = caption,
                Width = 448,
                SizeToContent = SizeToContent.Height,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ShowInTaskbar = false
            };

            var card = new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(229, 229, 234)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(18),
                Effect = new DropShadowEffect { BlurRadius = 26, ShadowDepth = 7, Opacity = 0.22, Color = Color.FromRgb(0, 0, 0) }
            };

            var root = new StackPanel { Margin = new Thickness(24, 22, 24, 22) };

            var header = new StackPanel { Orientation = Orientation.Horizontal };
            var iconBox = new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(Color.FromArgb(24, iconColor.R, iconColor.G, iconColor.B)),
                Margin = new Thickness(0, 0, 12, 0)
            };
            iconBox.Child = new TextBlock
            {
                Text = glyph,
                Foreground = new SolidColorBrush(iconColor),
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            header.Children.Add(iconBox);
            header.Children.Add(new TextBlock
            {
                Text = caption,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(29, 29, 31)),
                VerticalAlignment = VerticalAlignment.Center
            });

            var msg = new TextBlock
            {
                Text = text,
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(60, 60, 67)),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 19,
                Margin = new Thickness(0, 14, 0, 18)
            };

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            if (button == MessageBoxButton.YesNo || button == MessageBoxButton.YesNoCancel)
            {
                var noBtn = MakeButton("取消", false);
                noBtn.Click += (s, e) => { result = MessageBoxResult.No; win.Close(); };
                var yesBtn = MakeButton("确定", true);
                yesBtn.Click += (s, e) => { result = MessageBoxResult.Yes; win.Close(); };
                btnRow.Children.Add(noBtn);
                btnRow.Children.Add(yesBtn);
            }
            else
            {
                var okBtn = MakeButton("确定", true);
                okBtn.Click += (s, e) => { result = MessageBoxResult.OK; win.Close(); };
                btnRow.Children.Add(okBtn);
            }

            root.Children.Add(header);
            root.Children.Add(msg);
            root.Children.Add(btnRow);
            card.Child = root;
            win.Content = card;

            try
            {
                win.ShowDialog();
                return result;
            }
            catch
            {
                // 保险：自定义弹窗万一失败，回退系统原生框，绝不闪退
                return MessageBox.Show(text, caption, button, icon);
            }
        }

        private static Button MakeButton(string text, bool primary)
        {
            string template =
                "<ControlTemplate TargetType='Button' xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>" +
                "<Border x:Name='bd' Background='" + (primary ? "#0071E3" : "#F2F2F7") + "' CornerRadius='17' Padding='20,0,20,0'>" +
                "<ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>" +
                "</Border>" +
                "<ControlTemplate.Triggers>" +
                "<Trigger Property='IsMouseOver' Value='True'><Setter TargetName='bd' Property='Opacity' Value='0.86'/></Trigger>" +
                "<Trigger Property='IsPressed' Value='True'><Setter TargetName='bd' Property='Opacity' Value='0.72'/></Trigger>" +
                "</ControlTemplate.Triggers>" +
                "</ControlTemplate>";
            var btn = new Button
            {
                Template = (ControlTemplate)XamlReader.Parse(template),
                Content = text,
                Foreground = primary ? Brushes.White : new SolidColorBrush(Color.FromRgb(29, 29, 31)),
                Height = 34,
                MinWidth = 88,
                Margin = new Thickness(6, 0, 0, 0),
                Cursor = Cursors.Hand,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold
            };
            return btn;
        }
    }
}
