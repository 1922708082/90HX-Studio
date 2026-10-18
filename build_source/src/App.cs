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

        private Border _bannerAdminWarning;
        private Button _btnRestartAsAdmin;
        private TextBlock _txtTopGpuModel;
        private TextBlock _txtTopPcieStatus;
        private TextBlock _txtTopOverlayStatus;
        private TextBlock _txtTopSensors;
        private Button _btnTopRefresh;
        private Button _btnTopAutoOptimize;

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
        private TextBlock _txtHwMonitor;
        private TextBlock _txtHwDisk;
        private TextBlock _txtHwAudio;
        private TextBlock _txtHwNetwork;

        private ComboBox _cmbSpoofTargetCard;
        private Button _btnSpoofAllGpus;
        private ComboBox _cmbSpoofModel;
        private Button _btnApplySpoof;
        private Button _btnRestoreGpuName;
        private TextBlock _txtClassSubKey;
        private TextBlock _txtCurrentDriverDesc;
        private TextBlock _txtCurrentGpuMatchId;
        private TextBlock _txtDirectXDesc;
        private Button _btnRenameTo3080;
        private Button _btnRestore90hx;
        private Button _btnQuickRestartDriver;

        // —— v4 双栏工作台 / 驾驶舱新增控件（右栏日志、仪表环、快捷动作）——
        private TextBox _txtSideLogUnlock;
        private TextBox _txtSideLogSpoof;
        private TextBox _txtSideLogDlss;
        private System.Windows.Shapes.Ellipse _ringUnlockTemp;
        private System.Windows.Shapes.Ellipse _ringUnlockPower;
        private TextBlock _txtRingUnlockTempVal;
        private TextBlock _txtRingUnlockPowerVal;
        private System.Windows.Shapes.Ellipse _ringDashTemp;
        private System.Windows.Shapes.Ellipse _ringDashPower;
        private TextBlock _txtRingDashTempVal;
        private TextBlock _txtRingDashPowerVal;
        private TextBlock _txtDashPcie;
        private TextBlock _txtDashOverlay;
        private Button _btnDashRename;
        private bool _dashIdleSaveOn;
        private Button _btnDashUnlock;
        private Button _btnDashRestart;

        private TextBlock _txtNvAppPath;
        private TextBlock _txtOverlayPatchState;
        private TextBlock _txtOverlayBakState;
        private Button _btnUnlockOverlay;
        private Button _btnRestoreOverlay;
        private Button _btnCleanCefAndRestart;

        private TextBlock _txtPcieCurrentMode;
        private TextBlock _txtPcieBcdHealth;
        private TextBlock _txtSecureBootState;
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
        private RadioButton _radioGpu90hx;
        private RadioButton _radioGpu40hx;
        private RadioButton _radioGpu50hx;
        private RadioButton _radioGpu30hx;
        private Button _btnApplyNewUnlock;
        private Button _btnRepairBootMenu;
        private Button _btnRestoreBcdBackup;
        private Border _cardNewUnlock;
        private Border _cardOldUnlock;
        private Button _btnApplyOldUnlock;
        private RadioButton _radioChannelA;
        private RadioButton _radioChannelB;
        private RadioButton _radioChannelC;
        private RadioButton _radioChannelD;
        private FrameworkElement _panelUsbPicker;
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

        private TextBox _txtDlssGameDir;
        private Button _btnBrowseGameDir;
        private TextBlock _txtDlssStatusInDir;
        private ComboBox _cmbOptimizedLevel;
        private ComboBox _cmbMaxFramesLevel;
        private ComboBox _cmbProxyDll;
        private TextBlock _txtProxyHint;
        private ComboBox _cmbFgEngine;
        private ComboBox _cmbFgPreset;
        private ComboBox _cmbSrMode;
        private Border _borderPreflight;
        private StackPanel _pnlPreflight;
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
        private TextBlock _txtReFwVersion;
        private Button _btnCheckReFw;
        private Button _btnPullReFw;
        private ProgressBar _progReFw;
        private TextBlock _txtReFwStatus;

        private TextBlock _txtSidebarVersion;
        private TextBlock _txtAppVerCurrent;
        private TextBlock _txtAppVerLatest;
        private Border _bdAppStatus;
        private TextBlock _txtAppStatusBadge;
        private Button _btnCheckAppUpdate;
        private Button _btnApplyAppUpdate;
        private ProgressBar _progAppUpdate;
        private TextBlock _txtAppUpdateStatus;
        private NativeCore.AppUpdateInfo _pendingUpdate;
        private bool _autoUpdateCheckStarted;

        private Button _btnToolRestartDriver;
        private Button _btnToolToggleHags;
        private TextBlock _txtToolHagsStatus;
        private Button _btnToolEnableUltimatePower;
        private Button _btnToolDevMgmt;
        private Button _btnToolDxDiag;
        private Button _btnToolNvCpl;
        private Button _btnToolGpuDiag;
        private Button _btnToolOpenDataDir;

        private Image _imgWechatPay;
        private Image _imgAlipay;

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

        /// <summary>
        /// 清掉历史版本更新留在程序目录的备份文件。
        /// 自更新已改为把备份放系统临时目录，这里只处理旧版残留，避免桌面上越更越多文件。
        /// </summary>
        private static void CleanupUpdateLeftovers()
        {
            try
            {
                string exePath = NativeCore.GetCurrentExePath();
                if (string.IsNullOrEmpty(exePath)) return;

                string dir = Path.GetDirectoryName(exePath);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

                string baseName = Path.GetFileNameWithoutExtension(exePath);
                string[] patterns = new string[]
                {
                    baseName + ".exe.old-update",
                    baseName + ".old-update"
                };
                for (int i = 0; i < patterns.Length; i++)
                {
                    string f = Path.Combine(dir, patterns[i]);
                    if (File.Exists(f))
                    {
                        try { File.Delete(f); } catch { }
                    }
                }
            }
            catch { }
        }

        private static void ActivateExistingInstance()
        {
            try
            {
                IntPtr targetHwnd = IntPtr.Zero;
                Process current = Process.GetCurrentProcess();

                foreach (var p in Process.GetProcessesByName(current.ProcessName))
                {
                    if (p.Id != current.Id && p.MainWindowHandle != IntPtr.Zero)
                    {
                        targetHwnd = p.MainWindowHandle;
                        break;
                    }
                }

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

        /// <summary>
        /// 后台省电进程：不创建任何窗口，只按空闲/负载动态限制 P-State。
        /// 由主程序以 --powersave 拉起，主程序关闭后仍继续运行，直到被显式停止或重启。
        /// </summary>
        private static void RunPowerSaverBackground()
        {
            Mutex mutex = null;
            bool created = false;
            try { mutex = new Mutex(true, "CMP90HX_Toolbox_PowerSaver_Mutex", out created); }
            catch { created = true; }

            if (!created) return;

            try
            {
                string dir = NativeCore.GetDataDirectory();
                if (!string.IsNullOrEmpty(dir))
                {
                    Directory.CreateDirectory(dir);
                    File.WriteAllText(Path.Combine(dir, "powersaver.pid"),
                        Process.GetCurrentProcess().Id.ToString());
                }
            }
            catch { }

            Action<string, bool> silent = delegate { };
            NativeCore.LoadDynamicClockParams(silent);
            NativeCore.StartAutoIdleSave(8, silent);

            try
            {
                System.Windows.Threading.Dispatcher.Run();
            }
            catch { }
            finally
            {
                try { NativeCore.StopAutoIdleSave(silent); } catch { }
                try
                {
                    string pidFile = Path.Combine(NativeCore.GetDataDirectory(), "powersaver.pid");
                    if (File.Exists(pidFile)) File.Delete(pidFile);
                }
                catch { }
            }
        }

        [STAThread]
        public static void Main(string[] args)
        {
            // 后台省电模式：由主程序以 --powersave 拉起，不开窗口，只跑空闲监控
            if (args != null && args.Length > 0 && args[0] == "--powersave")
            {
                RunPowerSaverBackground();
                return;
            }

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
                    CleanupUpdateLeftovers();
                    var app = new App();
                    app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

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

        private void ShowStartupPrank()
        {
            try
            {
                var overlay = new StartupAnimWindow();
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
                            LogAudit("启动动画播放完毕。90HX Studio 已就绪。");
                        }
                    }
                    catch
                    {

                    }
                };
                overlay.Show();
            }
            catch
            {

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

            _navItems = new RadioButton[] {
                (RadioButton)_window.FindName("NavItemHardware"),
                (RadioButton)_window.FindName("NavItemSpoof"),
                (RadioButton)_window.FindName("NavItemOverlay"),
                (RadioButton)_window.FindName("NavItemMatrix"),
                (RadioButton)_window.FindName("NavItemUnlock50"),
                (RadioButton)_window.FindName("NavItemUnlock40"),
                (RadioButton)_window.FindName("NavItemDualGpu"),
                (RadioButton)_window.FindName("NavItemDlss"),
                (RadioButton)_window.FindName("NavItemPower"),
                (RadioButton)_window.FindName("NavItemLogs"),
                (RadioButton)_window.FindName("NavItemSponsor"),
                (RadioButton)_window.FindName("NavItemUpdate"),
            };
            _pageHosts = new Grid[] {
                (Grid)_window.FindName("PageHardware"),
                (Grid)_window.FindName("PageSpoof"),
                (Grid)_window.FindName("PageOverlay"),
                (Grid)_window.FindName("PageMatrix"),
                (Grid)_window.FindName("PageUnlock50"),
                (Grid)_window.FindName("PageUnlock40"),
                (Grid)_window.FindName("PageDualGpu"),
                (Grid)_window.FindName("PageDlss"),
                (Grid)_window.FindName("PagePower"),
                (Grid)_window.FindName("PageLogs"),
                (Grid)_window.FindName("PageSponsor"),
                (Grid)_window.FindName("PageUpdate"),
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
            _txtHwMonitor = (TextBlock)_window.FindName("TxtHwMonitor");
            _txtHwDisk = (TextBlock)_window.FindName("TxtHwDisk");
            _txtHwAudio = (TextBlock)_window.FindName("TxtHwAudio");
            _txtHwNetwork = (TextBlock)_window.FindName("TxtHwNetwork");

            _cmbSpoofTargetCard = (ComboBox)_window.FindName("CmbSpoofTargetCard");
            _btnSpoofAllGpus = (Button)_window.FindName("BtnSpoofAllGpus");

            _cmbSpoofModel = (ComboBox)_window.FindName("CmbSpoofModel");
            _btnApplySpoof = (Button)_window.FindName("BtnApplySpoof");
            _btnRestoreGpuName = (Button)_window.FindName("BtnRestoreGpuName");
            if (_cmbSpoofModel != null)
            {
                _cmbSpoofModel.Items.Clear();
                for (int i = 0; i < NativeCore.SpoofTargetModels.Length; i++)
                    _cmbSpoofModel.Items.Add(NativeCore.SpoofTargetModels[i]);
                _cmbSpoofModel.SelectedIndex = 0;
            }

            _txtClassSubKey = (TextBlock)_window.FindName("TxtClassSubKey");
            _txtCurrentDriverDesc = (TextBlock)_window.FindName("TxtCurrentDriverDesc");
            _txtCurrentGpuMatchId = (TextBlock)_window.FindName("TxtCurrentGpuMatchId");
            _txtDirectXDesc = (TextBlock)_window.FindName("TxtDirectXDesc");
            _btnRenameTo3080 = (Button)_window.FindName("BtnRenameTo3080");
            _btnRestore90hx = (Button)_window.FindName("BtnRestore90hx");
            _btnQuickRestartDriver = (Button)_window.FindName("BtnQuickRestartDriver");

            _txtSideLogUnlock = (TextBox)_window.FindName("TxtSideLogUnlock");
            _txtSideLogSpoof = (TextBox)_window.FindName("TxtSideLogSpoof");
            _txtSideLogDlss = (TextBox)_window.FindName("TxtSideLogDlss");
            _ringUnlockTemp = (System.Windows.Shapes.Ellipse)_window.FindName("RingUnlockTemp");
            _ringUnlockPower = (System.Windows.Shapes.Ellipse)_window.FindName("RingUnlockPower");
            _txtRingUnlockTempVal = (TextBlock)_window.FindName("TxtRingUnlockTempVal");
            _txtRingUnlockPowerVal = (TextBlock)_window.FindName("TxtRingUnlockPowerVal");
            _ringDashTemp = (System.Windows.Shapes.Ellipse)_window.FindName("RingDashTemp");
            _ringDashPower = (System.Windows.Shapes.Ellipse)_window.FindName("RingDashPower");
            _txtRingDashTempVal = (TextBlock)_window.FindName("TxtRingDashTempVal");
            _txtRingDashPowerVal = (TextBlock)_window.FindName("TxtRingDashPowerVal");
            _txtDashPcie = (TextBlock)_window.FindName("TxtDashPcie");
            _txtDashOverlay = (TextBlock)_window.FindName("TxtDashOverlay");
            _btnDashRename = (Button)_window.FindName("BtnDashRename");
            _btnDashUnlock = (Button)_window.FindName("BtnDashUnlock");
            _btnDashRestart = (Button)_window.FindName("BtnDashRestart");

            // 首页快捷动作：此前这三个按钮只取了引用、没绑事件，点了没反应
            if (_btnDashRename != null)
            {
                _btnDashRename.Click += async (s, e) =>
                {
                    if (!EnsureAdminPrivilege()) return;
                    if (ModernMessageBox.Show(
                        "将把当前显卡显示名称改为：\nNVIDIA GeForce RTX 5090\n\n只改显示名称，不改 PCI ID，驱动匹配不受影响。\n\n是否继续？",
                        "一键伪装", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

                    _btnDashRename.IsEnabled = false;
                    bool ok = await NativeCore.ApplyGpuRenameAsync("NVIDIA GeForce RTX 5090", LogCallback);
                    _btnDashRename.IsEnabled = true;
                    RefreshSystemStatus();
                    if (ok) ModernMessageBox.Show("已伪装为 RTX 5090。\n到设备管理器禁用再启用显卡即可生效。", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                };
            }

            if (_btnDashUnlock != null)
            {
                if (NativeCore.IsPowerSaverTaskInstalled())
                    _btnDashUnlock.Content = "🔋 关闭系统省电";

                _btnDashUnlock.Click += (s, e) =>
                {
                    if (!EnsureAdminPrivilege()) return;

                    if (!NativeCore.IsPowerSaverTaskInstalled())
                    {
                        if (ModernMessageBox.Show(
                            "将注册开机自启的系统级省电：\n\n" +
                            "· 以 SYSTEM 权限运行，不需要用户登录\n" +
                            "· 连续空闲 60 秒 → 压到 P8\n" +
                            "· 检测到 GPU 占用 > 3% → 立刻恢复性能\n" +
                            "· 无窗口，不占任务栏\n\n" +
                            "是否继续？",
                            "系统级省电", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

                        if (NativeCore.InstallPowerSaverTask(LogCallback))
                        {
                            _btnDashUnlock.Content = "🔋 关闭系统省电";
                            NativeCore.RunPowerSaverTaskNow(LogCallback);
                            ModernMessageBox.Show(
                                "已注册并启动。\n\n以后每次开机自动生效，不需要打开软件。\n想关掉再点一次这个按钮。",
                                "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            ModernMessageBox.Show("注册失败，详情见日志。", "失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
                    else
                    {
                        if (ModernMessageBox.Show("将移除系统级省电任务，是否继续？", "移除", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

                        NativeCore.UninstallPowerSaverTask(LogCallback);
                        _btnDashUnlock.Content = "🔋 系统级省电";
                        ModernMessageBox.Show(
                            "已移除。\n\n频率锁可能还留在驱动里，点【软重启显卡驱动】可彻底恢复。",
                            "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    RefreshSensors();
                };
            }

            if (_btnDashRestart != null)
            {
                _btnDashRestart.Click += async (s, e) =>
                {
                    if (!EnsureAdminPrivilege()) return;
                    _btnDashRestart.IsEnabled = false;
                    await NativeCore.SoftRestartGpuDriverAsync(LogCallback);
                    _btnDashRestart.IsEnabled = true;
                    RefreshSystemStatus();
                };
            }

            // 系统级省电已合并到上面的「🔋 系统级省电」按钮，不再单独占一个入口

            _txtNvAppPath = (TextBlock)_window.FindName("TxtNvAppPath");
            _txtOverlayPatchState = (TextBlock)_window.FindName("TxtOverlayPatchState");
            _txtOverlayBakState = (TextBlock)_window.FindName("TxtOverlayBakState");
            _btnUnlockOverlay = (Button)_window.FindName("BtnUnlockOverlay");
            _btnRestoreOverlay = (Button)_window.FindName("BtnRestoreOverlay");
            _btnCleanCefAndRestart = (Button)_window.FindName("BtnCleanCefAndRestart");

            _txtPcieCurrentMode = (TextBlock)_window.FindName("TxtPcieCurrentMode");
            _txtPcieBcdHealth = (TextBlock)_window.FindName("TxtPcieBcdHealth");
            _txtSecureBootState = (TextBlock)_window.FindName("TxtSecureBootState");
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
            _radioGpu90hx = (RadioButton)_window.FindName("RadioGpu90hx");
            _radioGpu40hx = (RadioButton)_window.FindName("RadioGpu40hx");
            _radioGpu50hx = (RadioButton)_window.FindName("RadioGpu50hx");
            _radioGpu30hx = (RadioButton)_window.FindName("RadioGpu30hx");
            _btnApplyNewUnlock = (Button)_window.FindName("BtnApplyNewUnlock");
            _btnRepairBootMenu = (Button)_window.FindName("BtnRepairBootMenu");
            _btnRestoreBcdBackup = (Button)_window.FindName("BtnRestoreBcdBackup");
            _cardNewUnlock = (Border)_window.FindName("CardNewUnlock");
            _cardOldUnlock = (Border)_window.FindName("CardOldUnlock");
            _btnApplyOldUnlock = (Button)_window.FindName("BtnApplyOldUnlock");
            _radioChannelA = (RadioButton)_window.FindName("RadioChannelA");
            _radioChannelB = (RadioButton)_window.FindName("RadioChannelB");
            _radioChannelC = (RadioButton)_window.FindName("RadioChannelC");
            _radioChannelD = (RadioButton)_window.FindName("RadioChannelD");
            _panelUsbPicker = (FrameworkElement)_window.FindName("PanelUsbPicker");
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
            _cmbProxyDll = (ComboBox)_window.FindName("CmbProxyDll");
            _txtProxyHint = (TextBlock)_window.FindName("TxtProxyHint");
            _cmbFgEngine = (ComboBox)_window.FindName("CmbFgEngine");
            _cmbFgPreset = (ComboBox)_window.FindName("CmbFgPreset");
            _cmbSrMode = (ComboBox)_window.FindName("CmbSrMode");
            _borderPreflight = (Border)_window.FindName("BorderPreflight");
            _pnlPreflight = (StackPanel)_window.FindName("PnlPreflight");
            LoadProxyCandidates();
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
            _txtReFwVersion = (TextBlock)_window.FindName("TxtReFwVersion");
            _btnCheckReFw = (Button)_window.FindName("BtnCheckReFw");
            _btnPullReFw = (Button)_window.FindName("BtnPullReFw");
            _progReFw = (ProgressBar)_window.FindName("ProgReFw");
            _txtReFwStatus = (TextBlock)_window.FindName("TxtReFwStatus");
            if (_txtReFwVersion != null) _txtReFwVersion.Text = ShortVersion(NativeCore.GetLocalReFrameworkVersion());

            _txtSidebarVersion = (TextBlock)_window.FindName("TxtSidebarVersion");
            _txtAppVerCurrent = (TextBlock)_window.FindName("TxtAppVerCurrent");
            _txtAppVerLatest = (TextBlock)_window.FindName("TxtAppVerLatest");
            _bdAppStatus = (Border)_window.FindName("BdAppStatus");
            _txtAppStatusBadge = (TextBlock)_window.FindName("TxtAppStatusBadge");
            _btnCheckAppUpdate = (Button)_window.FindName("BtnCheckAppUpdate");
            _btnApplyAppUpdate = (Button)_window.FindName("BtnApplyAppUpdate");
            _progAppUpdate = (ProgressBar)_window.FindName("ProgAppUpdate");
            _txtAppUpdateStatus = (TextBlock)_window.FindName("TxtAppUpdateStatus");

            string curVer = NativeCore.GetCurrentAppVersion();
            if (_txtAppVerCurrent != null) _txtAppVerCurrent.Text = "v" + curVer;
            if (_txtSidebarVersion != null) _txtSidebarVersion.Text = "v" + curVer;

            TextBlock aboutVer = _window.FindName("TxtAboutVersion") as TextBlock;
            if (aboutVer != null) aboutVer.Text = "v" + curVer;

            WireLinkButton("BtnLinkRepo");
            WireLinkButton("BtnLinkCmp90");
            WireLinkButton("BtnLinkDlss");
            WireLinkButton("BtnLinkRefw");
            WireLinkButton("BtnLinkCmp40");
            WireLinkButton("BtnLinkCmp50");
            WireLinkButton("BtnDownloadRtDriver");
            WireLinkButton("BtnOpenDriverFolder");

            WireUnlockPage("Unlock50", "50hx", "50HX", "50HX");
            WireUnlockPage("Unlock40", "40hx", "40HX", "40HX");

            // ===== 软件更新页：打开数据目录 + 全局下载线路 =====
            Button btnOpenDataDir = _window.FindName("BtnOpenDataDir") as Button;
            if (btnOpenDataDir != null)
            {
                btnOpenDataDir.Click += (s, e) =>
                {
                    try
                    {
                        string dir = NativeCore.GetDataDirectory();
                        if (string.IsNullOrEmpty(dir)) return;
                        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                        Process.Start("explorer.exe", "\"" + dir + "\"");
                        LogAudit("已打开数据目录: " + dir);
                    }
                    catch (Exception ex)
                    {
                        LogCallback("打开数据目录失败: " + ex.Message, true);
                    }
                };
            }

            ComboBox cmbGlobalMirror = _window.FindName("CmbGlobalMirror") as ComboBox;
            if (cmbGlobalMirror != null)
            {
                cmbGlobalMirror.Items.Clear();
                var gMirrors = NativeCore.GetDlssMirrors();
                for (int i = 0; i < gMirrors.Count; i++) cmbGlobalMirror.Items.Add(gMirrors[i].Name);
                if (cmbGlobalMirror.Items.Count > 0) cmbGlobalMirror.SelectedIndex = 0;

                // 全局线路是唯一入口，DLSS 页那个下拉已隐藏，只作为参数来源同步
                cmbGlobalMirror.SelectionChanged += (s, e) =>
                {
                    int idx = cmbGlobalMirror.SelectedIndex;
                    if (idx < 0) return;
                    if (_cmbDlssMirrorSource != null && idx < _cmbDlssMirrorSource.Items.Count)
                        _cmbDlssMirrorSource.SelectedIndex = idx;
                };
            }

            Button aboutCheck = _window.FindName("BtnAboutCheckUpdate") as Button;
            if (aboutCheck != null)
            {
                aboutCheck.Click += (s, e) =>
                {
                    // 跳到「软件更新」页（导航索引 11）
                    if (_navItems != null && _navItems.Length > 11 && _navItems[11] != null)
                        _navItems[11].IsChecked = true;
                    else
                        SwitchPage(11, true);
                };
            }

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

            if (_navItems.Length > 0 && _navItems[0] != null) _navItems[0].IsChecked = true;
            else SwitchPage(0, false);

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

            if (_btnTopRefresh != null)
                _btnTopRefresh.Click += (s, e) =>
            {
                RefreshSystemStatus();
                RefreshSensors();
                LogAudit("系统硬件与配置状态已刷新。");
            };

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

            if (_btnRefreshSensorsOnce != null)
                _btnRefreshSensorsOnce.Click += (s, e) => RefreshSystemStatus();

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

            if (_btnApplySpoof != null)
            {
                _btnApplySpoof.Click += async (s, e) =>
                {
                    if (!EnsureAdminPrivilege()) return;

                    string model = Convert.ToString(_cmbSpoofModel != null ? _cmbSpoofModel.SelectedItem : "");
                    if (string.IsNullOrEmpty(model))
                    {
                        ModernMessageBox.Show("请先在【伪装为】下拉框里选一个目标型号。", "未选择型号", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (ModernMessageBox.Show(
                        "将把显卡显示名称改为：\n" + model + "\n\n" +
                        "只改显示名称，不改 PCI ID，驱动匹配不受影响。\n" +
                        "修改后到设备管理器禁用再启用一次显卡即可看到变化。\n\n是否继续？",
                        "确认伪装型号", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                        return;

                    _btnApplySpoof.IsEnabled = false;
                    bool ok = await NativeCore.ApplyGpuRenameAsync(model, LogCallback);
                    _btnApplySpoof.IsEnabled = true;
                    RefreshSystemStatus();
                    if (ok) ModernMessageBox.Show("已将显示名称改为 " + model + "\n\n到设备管理器禁用再启用显卡即可生效。", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                };
            }

            if (_btnRestoreGpuName != null)
            {
                _btnRestoreGpuName.Click += async (s, e) =>
                {
                    if (!EnsureAdminPrivilege()) return;
                    if (ModernMessageBox.Show("将把显卡显示名称恢复为 NVIDIA CMP 90HX，是否继续？", "恢复原名", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                        return;

                    _btnRestoreGpuName.IsEnabled = false;
                    bool ok = await NativeCore.RestoreGpuTo90hxAsync(LogCallback);
                    _btnRestoreGpuName.IsEnabled = true;
                    RefreshSystemStatus();
                    if (ok) ModernMessageBox.Show("已恢复原始名称。", "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                };
            }

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

            // 驾驶舱快捷动作已在 LoadControlRefs 里各自绑定，这里不再做二次转发，
            // 否则一次点击会触发两遍业务逻辑（待机省电曾被重复触发到一键解锁上）。
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

            if (_btnRefreshUsbDrives != null) _btnRefreshUsbDrives.Click += (s, e) => LoadUsbDrives();

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

            if (_btnRescanMiningGpus != null)
            {
                _btnRescanMiningGpus.Click += (s, e) => RefreshMiningGpuList();
            }

            if (_radioGpu90hx != null) _radioGpu90hx.Checked += (s, e) => UpdateGpuModelHint();
            if (_radioGpu40hx != null) _radioGpu40hx.Checked += (s, e) => UpdateGpuModelHint();
            if (_radioGpu50hx != null) _radioGpu50hx.Checked += (s, e) => UpdateGpuModelHint();
            if (_radioGpu30hx != null) _radioGpu30hx.Checked += (s, e) => UpdateGpuModelHint();
            UpdateGpuModelHint();

            if (_radioChannelA != null) _radioChannelA.Checked += (s, e) => UpdateUsbPickerVisibility();
            if (_radioChannelB != null) _radioChannelB.Checked += (s, e) => UpdateUsbPickerVisibility();
            if (_radioChannelC != null) _radioChannelC.Checked += (s, e) => UpdateUsbPickerVisibility();
            if (_radioChannelD != null) _radioChannelD.Checked += (s, e) => UpdateUsbPickerVisibility();
            UpdateUsbPickerVisibility();

            if (_btnApplyNewUnlock != null)
            {
                _btnApplyNewUnlock.Click += async (s, e) =>
                {
                    await RunUnlockFlow(true);
                };
            }

            if (_btnApplyOldUnlock != null)
            {
                _btnApplyOldUnlock.Click += async (s, e) =>
                {
                    await RunUnlockFlow(false);
                };
            }

            if (_btnRepairBootMenu != null)
            {
                _btnRepairBootMenu.Click += async (s, e) =>
                {
                    if (ModernMessageBox.Show("【修复开机系统选择框】\n\n将执行：\n• 导出当前 BCD 备份（可还原）\n• 删除本工具生成的重复启动项\n• 还原 Windows 启动管理器路径与启动顺序\n\n是否继续？", "确认修复引导菜单", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                        return;
                    _btnRepairBootMenu.IsEnabled = false;
                    bool ok = await NativeCore.RepairBootMenuAsync(LogCallback);
                    _btnRepairBootMenu.IsEnabled = true;
                    if (ok) ModernMessageBox.Show("引导菜单修复完成！\n\n请重启电脑验证：开机应直接进入 Windows，不再弹出系统选择框。", "修复完成", MessageBoxButton.OK, MessageBoxImage.Information);
                };
            }

            if (_btnRestoreBcdBackup != null)
            {
                _btnRestoreBcdBackup.Click += async (s, e) =>
                {
                    if (ModernMessageBox.Show("将从最近的 BCD 备份还原引导配置。\n\n建议仅在开机异常时使用。是否继续？", "确认还原引导", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                        return;
                    _btnRestoreBcdBackup.IsEnabled = false;
                    bool ok = await System.Threading.Tasks.Task.Factory.StartNew(() => NativeCore.RestoreBcdFromBackup(LogCallback));
                    _btnRestoreBcdBackup.IsEnabled = true;
                    if (ok) ModernMessageBox.Show("引导配置已还原，请重启验证。", "还原完成", MessageBoxButton.OK, MessageBoxImage.Information);
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

            _btnBrowseGameDir.Click += (s, e) =>
            {
                using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
                {
                    fbd.Description = "请选择 3A 游戏的主程序目录 (包含 exe 的文件夹)";
                    if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        _txtDlssGameDir.Text = fbd.SelectedPath;
                        CheckDlssInDir(fbd.SelectedPath);
                        bool reChk = _chkInjectReFramework != null && _chkInjectReFramework.IsChecked == true;
                        RenderPreflight(NativeCore.PreflightDlssDeploy(fbd.SelectedPath, GetSelectedProxyName(), reChk));
                    }
                }
            };

            _btnInjectDlss.Click += (s, e) =>
            {
                string dir = _txtDlssGameDir.Text.Trim();
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                {
                    ModernMessageBox.Show("请先选择有效的目标游戏程序目录！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                bool injectRe = _chkInjectReFramework != null && _chkInjectReFramework.IsChecked == true;
                string proxy = GetSelectedProxyName();
                string opt = TakePrefix(GetComboText(_cmbOptimizedLevel), "1");
                string maxF = GetSelectedMaxFrames();
                string engine = TakePrefix(GetComboText(_cmbFgEngine), "DLSS");
                string preset = TakePrefix(GetComboText(_cmbFgPreset), "Auto");
                string srMode = TakePrefix(GetComboText(_cmbSrMode), "Off");

                // 体检先行：源运行库结构、内核组件、第三方残留、代理冲突都在落盘前确认
                NativeCore.DlssPreflightResult pf = NativeCore.PreflightDlssDeploy(dir, proxy, injectRe);
                RenderPreflight(pf);

                if (pf == null || !pf.Passed)
                {
                    string blockText = BuildPreflightText(pf, true);
                    ModernMessageBox.Show("部署体检未通过，已中止注入：\n\n" + blockText, "体检未通过", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                bool hasIni = File.Exists(Path.Combine(dir, "dlssg_sm86.ini"));
                bool hasProxy = File.Exists(Path.Combine(dir, proxy));
                bool hasMarker = File.Exists(Path.Combine(dir, ".dlssg_proxy"));
                if ((hasProxy && hasIni) || hasMarker)
                {
                    var confirm = ModernMessageBox.Show("【防重复注入检测】\n检测到该游戏目录下已经存在 DLSS-G 补丁（代理: " + proxy + "）！\n\n是否确认使用当前参数重新覆盖替换？", "补丁已存在", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (confirm != MessageBoxResult.Yes) return;
                }

                bool ok = NativeCore.InjectDlssG(dir, opt, maxF, injectRe, proxy, engine, preset, srMode, LogCallback);
                CheckDlssInDir(dir);
                if (ok)
                {
                    ModernMessageBox.Show("DLSS 3 补丁注入成功！\n\n代理: " + proxy + "\n倍率上限: " + maxF + "\n引擎/预设: " + engine + " / " + preset + "\n\n启动游戏并在画质设置中开启【帧生成】即可。" + (injectRe ? "\n(已附带注入 RE 框架代理 dinput8.dll)" : ""), "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            };

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

            // ===== DLSS 5 神经渲染（DLSS Unlocked 社区解锁）=====
            {
                Button btnD5Check = _window.FindName("BtnDlss5Check") as Button;
                Button btnD5Download = _window.FindName("BtnDlss5Download") as Button;
                Button btnD5Deploy = _window.FindName("BtnDlss5Deploy") as Button;
                TextBlock txtD5Ver = _window.FindName("TxtDlss5Version") as TextBlock;
                TextBlock txtD5Latest = _window.FindName("TxtDlss5Latest") as TextBlock;
                ProgressBar progD5 = _window.FindName("ProgDlss5") as ProgressBar;

                if (txtD5Ver != null) txtD5Ver.Text = NativeCore.GetDlssUnlockedLocalVersion();

                if (btnD5Check != null)
                {
                    btnD5Check.Click += (s, e) =>
                    {
                        btnD5Check.IsEnabled = false;
                        if (txtD5Latest != null) txtD5Latest.Text = "检查中…";
                        Task.Run(delegate
                        {
                            string tag = NativeCore.CheckDlssUnlockedLatestTag(LogCallback);
                            _window.Dispatcher.BeginInvoke(new Action(delegate
                            {
                                btnD5Check.IsEnabled = true;
                                if (txtD5Latest != null)
                                    txtD5Latest.Text = string.IsNullOrEmpty(tag) ? "检查失败，请换线路重试" : ("最新 " + tag);
                                if (!string.IsNullOrEmpty(tag))
                                {
                                    string local = NativeCore.GetDlssUnlockedLocalVersion();
                                    if (txtD5Ver != null) txtD5Ver.Text = local;
                                    if (local == tag)
                                        LogCallback("DLSS 5 神经渲染已是最新版本 " + tag, false);
                                    else
                                        LogCallback("DLSS 5 神经渲染：本地 " + local + "，最新 " + tag, false);
                                }
                            }));
                        });
                    };
                }

                if (btnD5Download != null)
                {
                    btnD5Download.Click += async (s, e) =>
                    {
                        if (!EnsureAdminPrivilege()) return;

                        btnD5Download.IsEnabled = false;
                        if (progD5 != null) { progD5.Visibility = Visibility.Visible; progD5.Value = 0; }
                        if (txtD5Latest != null) txtD5Latest.Text = "获取版本号…";

                        string tag = null;
                        await Task.Run(delegate { tag = NativeCore.CheckDlssUnlockedLatestTag(LogCallback); });

                        if (string.IsNullOrEmpty(tag))
                        {
                            btnD5Download.IsEnabled = true;
                            if (progD5 != null) progD5.Visibility = Visibility.Collapsed;
                            if (txtD5Latest != null) txtD5Latest.Text = "获取版本失败";
                            ModernMessageBox.Show("无法获取最新版本号，请检查网络后重试。", "下载失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        if (txtD5Latest != null) txtD5Latest.Text = tag;

                        bool ok = await NativeCore.DownloadDlssUnlockedAsync(tag, delegate(int pct, string txt)
                        {
                            _window.Dispatcher.BeginInvoke(new Action(delegate
                            {
                                if (progD5 != null) progD5.Value = pct;
                                if (txtD5Latest != null && !string.IsNullOrEmpty(txt)) txtD5Latest.Text = pct + "%  " + txt;
                            }));
                        }, LogCallback);

                        btnD5Download.IsEnabled = true;
                        if (progD5 != null) progD5.Visibility = Visibility.Collapsed;
                        if (txtD5Ver != null) txtD5Ver.Text = NativeCore.GetDlssUnlockedLocalVersion();

                        if (ok)
                        {
                            string vres = NativeCore.ValidateDlssUnlockedPackage(LogCallback);
                            if (txtD5Ver != null) txtD5Ver.Text = "已下载 · " + vres;

                            ModernMessageBox.Show(
                                "下载完成，包校验：" + vres + "\n\n" +
                                "⚠ 下载得到的是压缩包，不能直接放进游戏目录。\n\n" +
                                "正确用法：\n" +
                                "1. 把游戏主程序 exe 所在目录填到上方【目标游戏主程序目录】\n" +
                                "2. 点【部署到游戏】，程序会自动解压并放到正确位置\n" +
                                "3. 进游戏后按 Home 打开面板，开启 DLSS Neural Rendering",
                                "下载完成", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            ModernMessageBox.Show("下载未完成，详情见右侧日志。", "下载失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    };
                }

                if (btnD5Deploy != null)
                {
                    btnD5Deploy.Click += (s, e) =>
                    {
                        if (!EnsureAdminPrivilege()) return;

                        string dir = Convert.ToString(_txtDlssGameDir != null ? _txtDlssGameDir.Text : "").Trim().Trim('"');
                        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                        {
                            ModernMessageBox.Show("请先在上方【目标游戏主程序目录】填入游戏 exe 所在目录。", "需要游戏目录", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        if (ModernMessageBox.Show(
                            "即将把 DLSS 5 神经渲染部署到：\n" + dir + "\n\n" +
                            "· 原有同名文件会备份为 .dlss5.bak\n" +
                            "· 代理 DLL 只能存在一个，若目录里已有其它帧生成补丁请先移除\n" +
                            "· 仅限单机游戏，网游会被反作弊判定异常\n\n是否继续？",
                            "确认部署", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                            return;

                        bool ok = NativeCore.DeployDlssUnlockedToGame(dir, LogCallback);
                        if (txtD5Ver != null) txtD5Ver.Text = NativeCore.GetDlssUnlockedLocalVersion();

                        if (ok)
                        {
                            ModernMessageBox.Show("部署完成。\n\n进游戏后按 Home 打开 OptiScaler 面板，开启 DLSS Neural Rendering。",
                                "完成", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        else
                        {
                            ModernMessageBox.Show("部署未完成，详情见右侧日志。", "部署失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    };
                }
            }

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

                        // 新版运行库可能带回 alternatives\ 代理，刷新下拉框并重跑体检
                        LoadProxyCandidates();
                        if (!string.IsNullOrEmpty(gameDir))
                        {
                            CheckDlssInDir(gameDir);
                            bool reChk2 = _chkInjectReFramework != null && _chkInjectReFramework.IsChecked == true;
                            RenderPreflight(NativeCore.PreflightDlssDeploy(gameDir, GetSelectedProxyName(), reChk2));
                        }

                        ModernMessageBox.Show(tip, "更新成功", MessageBoxButton.OK, MessageBoxImage.Information);
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

            if (_btnCheckReFw != null)
            {
                _btnCheckReFw.Click += async (s, e) =>
                {
                    _btnCheckReFw.IsEnabled = false;
                    if (_txtReFwStatus != null)
                    {
                        _txtReFwStatus.Text = "正在查询 REFramework 最新版本...";
                        _txtReFwStatus.Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                    }

                    NativeCore.ReFwCheckResult res = await NativeCore.CheckReFrameworkUpdateAsync(LogCallback);
                    _btnCheckReFw.IsEnabled = true;

                    // 本地记录的是 commit 哈希，远端是完整 tag；确认一致时展示可读版本名，避免一串哈希看不懂
                    if (_txtReFwVersion != null)
                    {
                        _txtReFwVersion.Text = (res.Success && !res.HasUpdate)
                            ? ShortVersion(res.RemoteVersion)
                            : ShortVersion(res.LocalVersion);
                    }

                    if (res.Success)
                    {
                        if (_txtReFwStatus != null)
                        {
                            if (res.HasUpdate)
                            {
                                _txtReFwStatus.Text = "发现新版: " + res.RemoteVersion + "（当前: " + ShortVersion(res.LocalVersion) + "），点击【一键更新】升级";
                                _txtReFwStatus.Foreground = new SolidColorBrush(Color.FromRgb(217, 119, 6));
                            }
                            else
                            {
                                _txtReFwStatus.Text = "已是最新版本: " + ShortVersion(res.LocalVersion);
                                _txtReFwStatus.Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74));
                            }
                        }
                    }
                    else if (_txtReFwStatus != null)
                    {
                        _txtReFwStatus.Text = "检测失败: " + res.ErrorMessage;
                        _txtReFwStatus.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                    }
                };
            }

            if (_btnPullReFw != null)
            {
                _btnPullReFw.Click += async (s, e) =>
                {
                    _btnPullReFw.IsEnabled = false;
                    if (_progReFw != null) { _progReFw.Visibility = Visibility.Visible; _progReFw.Value = 0; }
                    if (_txtReFwStatus != null)
                    {
                        _txtReFwStatus.Text = "正在下载 REFramework...";
                        _txtReFwStatus.Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235));
                    }

                    string tag = null;
                    NativeCore.ReFwCheckResult chk = await NativeCore.CheckReFrameworkUpdateAsync(LogCallback);
                    if (chk.Success) tag = chk.Tag;

                    NativeCore.ReFwPullResult res = await NativeCore.PullReFrameworkAsync(tag, delegate(int p)
                    {
                        Dispatcher.Invoke(new Action(() => { if (_progReFw != null) _progReFw.Value = p; }));
                    }, LogCallback);

                    _btnPullReFw.IsEnabled = true;
                    if (_progReFw != null) _progReFw.Visibility = Visibility.Collapsed;

                    if (res.Success)
                    {
                        if (_txtReFwVersion != null) _txtReFwVersion.Text = ShortVersion(res.NewVersion);
                        if (_txtReFwStatus != null)
                        {
                            _txtReFwStatus.Text = "已更新到 " + res.NewVersion;
                            _txtReFwStatus.Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74));
                        }
                        ModernMessageBox.Show("REFramework 已更新到 " + res.NewVersion + "\n\n下次勾选「同时注入 RE 框架代理」时会使用新版。", "更新成功", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else if (_txtReFwStatus != null)
                    {
                        _txtReFwStatus.Text = "更新失败: " + res.Message;
                        _txtReFwStatus.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                    }
                };
            }

            if (_btnCheckAppUpdate != null)
            {
                _btnCheckAppUpdate.Click += async (s, e) =>
                {
                    await RunAppUpdateCheck(true);
                };
            }

            if (_btnApplyAppUpdate != null)
            {
                _btnApplyAppUpdate.Click += async (s, e) =>
                {
                    await RunApplyAppUpdate();
                };
            }

            StartAutoUpdateCheck();

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

            _btnClearLogs.Click += (s, e) =>
            {
                _txtLogBox.Clear();
                if (_txtSideLogUnlock != null) _txtSideLogUnlock.Clear();
                if (_txtSideLogSpoof != null) _txtSideLogSpoof.Clear();
                if (_txtSideLogDlss != null) _txtSideLogDlss.Clear();
            };
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

                        double tempFrac = Math.Max(0.0, Math.Min(1.0, snap.Temperature / 90.0));
                        UpdateRingArc(_ringUnlockTemp, tempFrac);
                        UpdateRingArc(_ringDashTemp, tempFrac);
                        if (_txtRingUnlockTempVal != null) _txtRingUnlockTempVal.Text = snap.Temperature + "°";
                        if (_txtRingDashTempVal != null) _txtRingDashTempVal.Text = snap.Temperature + "°";

                        if (_txtSensFan != null) _txtSensFan.Text = snap.FanPercent + " %";
                        if (_barSensFan != null) _barSensFan.Value = snap.FanPercent;

                        if (_txtSensPower != null) _txtSensPower.Text = snap.PowerDraw.ToString("F1") + " W / " + snap.PowerLimit.ToString("F0") + " W";

                        double dashPowerMax = snap.PowerLimit > 0 ? snap.PowerLimit : 250.0;
                        double powerFrac = Math.Max(0.0, Math.Min(1.0, snap.PowerDraw / dashPowerMax));
                        UpdateRingArc(_ringUnlockPower, powerFrac);
                        UpdateRingArc(_ringDashPower, powerFrac);
                        if (_txtRingUnlockPowerVal != null) _txtRingUnlockPowerVal.Text = ((int)snap.PowerDraw) + "W";
                        if (_txtRingDashPowerVal != null) _txtRingDashPowerVal.Text = ((int)snap.PowerDraw) + "W";
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
                        if (_txtDashPcie != null) _txtDashPcie.Text = snap.PcieGen == "2" ? "Gen2 x" + snap.PcieWidth + " · 已解锁" : "Gen" + snap.PcieGen + ".0 x" + snap.PcieWidth;
                    });
                }
            }
            catch { }
        }

        private static void UpdateRingArc(System.Windows.Shapes.Ellipse arc, double frac)
        {
            if (arc == null) return;
            if (frac < 0) frac = 0;
            if (frac > 1) frac = 1;
            double th = arc.StrokeThickness;
            if (th <= 0) return;
            double size = arc.ActualWidth;
            if (size <= 0 || double.IsNaN(size)) size = arc.Width;
            if (size <= 0 || double.IsNaN(size)) return;
            double r = (size - th) / 2.0;
            if (r <= 0) return;
            double circUnits = (2.0 * Math.PI * r) / th;
            arc.StrokeDashArray = new DoubleCollection(new double[] { Math.Max(0.8, circUnits * frac), circUnits * 2.0 });
        }

        private void RefreshSystemStatus()
        {
            var status = SystemDetector.DetectAll();
            if (_txtDlssLocalVersion != null) _txtDlssLocalVersion.Text = "v" + NativeCore.GetLocalDlssVersion();

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
            if (_txtDashOverlay != null) _txtDashOverlay.Text = status.OverlayPatched ? "已激活 · 免白名单" : "未激活 · 官方锁定";
            _txtTopOverlayStatus.Foreground = status.OverlayPatched ? new SolidColorBrush(Color.FromRgb(22, 163, 74)) : new SolidColorBrush(Color.FromRgb(100, 116, 139));

            _txtClassSubKey.Text = status.ClassSubKey;
            _txtCurrentDriverDesc.Text = status.GpuDesc;
            _txtCurrentGpuMatchId.Text = status.GpuId;
            _txtDirectXDesc.Text = status.IsSpoofedTo3080 ? "NVIDIA GeForce RTX 3080 (已同步)" : "NVIDIA CMP 90HX";

            _txtNvAppPath.Text = string.IsNullOrEmpty(status.NvAppPath) ? "未安装 NVIDIA App" : status.NvAppPath;
            _txtOverlayPatchState.Text = status.OverlayPatched ? "已成功注入免检补丁 (ShadowPlay 白名单已解开)" : "未注入补丁 (受官方硬件白名单限制)";
            _txtOverlayPatchState.Foreground = status.OverlayPatched ? new SolidColorBrush(Color.FromRgb(22, 163, 74)) : new SolidColorBrush(Color.FromRgb(217, 119, 6));
            _txtOverlayBakState.Text = status.OverlayBakExists ? "main.*.js.bak (官方备份存在，随时可一键还原)" : "未检测到备份";

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
                if (_txtHwMonitor != null) _txtHwMonitor.Text = hw.DisplayDevices;
                if (_txtHwDisk != null) _txtHwDisk.Text = hw.DiskDevices;
                if (_txtHwAudio != null) _txtHwAudio.Text = hw.AudioDevices;
                if (_txtHwNetwork != null) _txtHwNetwork.Text = hw.NetworkDevices;

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

                bool hasMiningGpu = (_cachedMiningGpus != null && _cachedMiningGpus.Count > 0);

                if (_bannerNo90hxWarning != null)
                {
                    _bannerNo90hxWarning.Visibility = hasMiningGpu ? Visibility.Collapsed : Visibility.Visible;
                }
                if (_card90hxStatusInfo != null)
                {
                    _card90hxStatusInfo.Opacity = hasMiningGpu ? 1.0 : 0.85;
                }
                if (_cardNewUnlock != null) _cardNewUnlock.Opacity = hasMiningGpu ? 1.0 : 0.85;
                if (_cardOldUnlock != null) _cardOldUnlock.Opacity = hasMiningGpu ? 1.0 : 0.85;
                if (_btnApplyNewUnlock != null) _btnApplyNewUnlock.IsEnabled = hasMiningGpu;
                if (_btnApplyOldUnlock != null) _btnApplyOldUnlock.IsEnabled = hasMiningGpu;

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

        private async Task<NativeCore.AppUpdateInfo> RunAppUpdateCheck(bool interactive)
        {
            if (_btnCheckAppUpdate != null) _btnCheckAppUpdate.IsEnabled = false;
            if (interactive && _txtAppUpdateStatus != null)
            {
                _txtAppUpdateStatus.Text = "正在检查更新...";
                _txtAppUpdateStatus.Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235));
            }

            NativeCore.AppUpdateInfo info = await NativeCore.CheckAppUpdateAsync(LogCallback);

            if (_btnCheckAppUpdate != null) _btnCheckAppUpdate.IsEnabled = true;

            if (info != null && info.Success)
            {
                if (_txtAppVerLatest != null) _txtAppVerLatest.Text = "最新版本: v" + info.LatestVersion;

                if (_bdAppStatus != null && _txtAppStatusBadge != null)
                {
                    if (info.HasUpdate)
                    {
                        _bdAppStatus.Background = new SolidColorBrush(Color.FromRgb(254, 243, 199));
                        _txtAppStatusBadge.Text = "有新版本";
                        _txtAppStatusBadge.Foreground = new SolidColorBrush(Color.FromRgb(180, 83, 9));
                    }
                    else
                    {
                        _bdAppStatus.Background = new SolidColorBrush(Color.FromRgb(220, 252, 231));
                        _txtAppStatusBadge.Text = "已是最新";
                        _txtAppStatusBadge.Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74));
                    }
                }

                // 已经是最新版时不该出现"立即更新"，否则逻辑上说不通
                if (_btnApplyAppUpdate != null)
                    _btnApplyAppUpdate.Visibility = info.HasUpdate ? Visibility.Visible : Visibility.Collapsed;

                if (info.HasUpdate)
                {
                    _pendingUpdate = info;
                    if (_txtAppUpdateStatus != null)
                    {
                        _txtAppUpdateStatus.Text = "发现新版本 v" + info.LatestVersion + "，点击【立即更新并重启】升级";
                        _txtAppUpdateStatus.Foreground = new SolidColorBrush(Color.FromRgb(217, 119, 6));
                    }
                }
                else
                {
                    _pendingUpdate = null;
                    if (_txtAppUpdateStatus != null)
                    {
                        _txtAppUpdateStatus.Text = "已是最新版本 v" + info.CurrentVersion;
                        _txtAppUpdateStatus.Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74));
                    }
                }
            }
            else if (interactive)
            {
                if (_txtAppUpdateStatus != null)
                {
                    _txtAppUpdateStatus.Text = "检查失败: " + (info == null ? "未知错误" : info.ErrorMessage);
                    _txtAppUpdateStatus.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                }
                if (_bdAppStatus != null && _txtAppStatusBadge != null)
                {
                    _bdAppStatus.Background = new SolidColorBrush(Color.FromRgb(254, 226, 226));
                    _txtAppStatusBadge.Text = "检查失败";
                    _txtAppStatusBadge.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                }
            }

            return info;
        }

        private async Task RunApplyAppUpdate()
        {
            NativeCore.AppUpdateInfo info = _pendingUpdate;
            if (info == null || !info.HasUpdate)
            {
                info = await RunAppUpdateCheck(true);
                if (info == null || !info.HasUpdate)
                {
                    ModernMessageBox.Show("当前已是最新版本，无需更新。", "无需更新", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }

            if (_btnApplyAppUpdate != null) _btnApplyAppUpdate.IsEnabled = false;
            if (_progAppUpdate != null) { _progAppUpdate.Visibility = Visibility.Visible; _progAppUpdate.Value = 0; }
            if (_txtAppUpdateStatus != null)
            {
                _txtAppUpdateStatus.Text = "正在下载 v" + info.LatestVersion + "...";
                _txtAppUpdateStatus.Foreground = new SolidColorBrush(Color.FromRgb(37, 99, 235));
            }

            NativeCore.AppUpdateResult res = await NativeCore.DownloadAppUpdateAsync(info.DownloadUrl, delegate(int p)
            {
                Dispatcher.Invoke(new Action(() => { if (_progAppUpdate != null) _progAppUpdate.Value = p; }));
            }, LogCallback);

            if (res == null || !res.Success)
            {
                if (_btnApplyAppUpdate != null) _btnApplyAppUpdate.IsEnabled = true;
                if (_progAppUpdate != null) _progAppUpdate.Visibility = Visibility.Collapsed;

                string msg = res == null ? "未知错误" : res.Message;
                if (_txtAppUpdateStatus != null)
                {
                    _txtAppUpdateStatus.Text = "下载失败: " + msg;
                    _txtAppUpdateStatus.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                }
                ModernMessageBox.Show("更新包下载失败：\n" + msg, "更新失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_txtAppUpdateStatus != null)
            {
                _txtAppUpdateStatus.Text = "下载完成，等待确认替换";
                _txtAppUpdateStatus.Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74));
            }

            string sizeText = (res.SizeBytes / 1048576.0).ToString("0.0");
            var confirm = ModernMessageBox.Show(
                "更新包已下载完成（" + sizeText + " MB）\n\n" +
                "确认后程序会关闭，自动替换为 v" + info.LatestVersion + " 并重新打开。\n" +
                "替换前旧版会备份为 .old-update，替换失败可回滚。",
                "准备更新", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
            {
                if (_btnApplyAppUpdate != null) _btnApplyAppUpdate.IsEnabled = true;
                return;
            }

            bool started = NativeCore.ApplyAppUpdate(res.FilePath, LogCallback);
            if (started)
            {
                Shutdown();
            }
            else
            {
                if (_btnApplyAppUpdate != null) _btnApplyAppUpdate.IsEnabled = true;
                ModernMessageBox.Show("无法启动替换脚本。\n可手动把程序目录下的 .update.exe 改名为原文件名后运行。", "更新未启动", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>启动后延迟几秒检测一次；失败静默，避免没网时每次开都弹错误。</summary>
        private void StartAutoUpdateCheck()
        {
            if (_autoUpdateCheckStarted) return;
            _autoUpdateCheckStarted = true;

            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(4);
            timer.Tick += async (s, e) =>
            {
                timer.Stop();
                try
                {
                    NativeCore.AppUpdateInfo info = await RunAppUpdateCheck(false);
                    if (info != null && info.Success && info.HasUpdate)
                    {
                        var r = ModernMessageBox.Show(
                            "发现新版本 v" + info.LatestVersion + "\n\n" +
                            "当前版本: v" + info.CurrentVersion + "\n\n" +
                            "是否现在下载并更新？更新时程序会自动关闭，完成后自动重新打开。",
                            "发现更新", MessageBoxButton.YesNo, MessageBoxImage.Question);

                        if (r == MessageBoxResult.Yes)
                        {
                            await RunApplyAppUpdate();
                        }
                    }
                }
                catch { }
            };
            timer.Start();
        }

        private void LoadProxyCandidates()
        {
            if (_cmbProxyDll == null) return;
            _cmbProxyDll.Items.Clear();
            List<string> names = NativeCore.GetAvailableProxyNames();
            for (int i = 0; i < names.Count; i++) _cmbProxyDll.Items.Add(names[i]);
            if (_cmbProxyDll.Items.Count > 0) _cmbProxyDll.SelectedIndex = 0;

            if (_txtProxyHint != null)
            {
                _txtProxyHint.Text = names.Count > 1
                    ? "共 " + names.Count + " 个可用代理"
                    : "仅 version.dll 可用，换别的需先补 alternatives";
            }
        }

        private string GetComboText(ComboBox cmb)
        {
            if (cmb == null || cmb.SelectedItem == null) return "";
            ComboBoxItem item = cmb.SelectedItem as ComboBoxItem;
            if (item != null) return item.Content == null ? "" : item.Content.ToString();
            return cmb.SelectedItem.ToString();
        }

        private string TakePrefix(string text, string fallback)
        {
            if (string.IsNullOrEmpty(text)) return fallback;
            int i = text.IndexOf(':');
            if (i <= 0) return fallback;
            string p = text.Substring(0, i).Trim();
            return p.Length == 0 ? fallback : p;
        }

        /// <summary>绑定 40HX / 50HX 解锁页的检测与工具按钮。</summary>
        private void WireUnlockPage(string key, string toolkit, string exePrefix, string modelMatch)
        {
            Button detect = _window.FindName("Btn" + key + "Detect") as Button;
            Button install = _window.FindName("Btn" + key + "Install") as Button;
            Button check = _window.FindName("Btn" + key + "Check") as Button;
            Button uninstall = _window.FindName("Btn" + key + "Uninstall") as Button;
            Button folder = _window.FindName("Btn" + key + "Folder") as Button;
            Border dot = _window.FindName("DotDot" + key) as Border;
            TextBlock dev = _window.FindName("Txt" + key + "Device") as TextBlock;
            TextBlock detail = _window.FindName("Txt" + key + "Detail") as TextBlock;
            TextBlock status = _window.FindName("Txt" + key + "Status") as TextBlock;

            Action refresh = delegate
            {
                try
                {
                    NativeCore.InstalledMiningGpu found = null;
                    var gpus = NativeCore.DetectAllMiningGpus();
                    if (gpus != null)
                    {
                        for (int i = 0; i < gpus.Count; i++)
                        {
                            string m = gpus[i].DetectedModel == null ? "" : gpus[i].DetectedModel;
                            if (m.IndexOf(modelMatch, StringComparison.OrdinalIgnoreCase) >= 0) { found = gpus[i]; break; }
                        }
                    }

                    if (found != null)
                    {
                        if (dot != null) dot.Background = new SolidColorBrush(Color.FromRgb(22, 163, 74));
                        if (dev != null) dev.Text = "已检测到 " + found.DetectedModel;
                        if (detail != null) detail.Text = "槽位 " + (found.SlotIndex + 1) + " · " + (found.CurrentDesc == null ? "" : found.CurrentDesc);
                    }
                    else
                    {
                        if (dot != null) dot.Background = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                        if (dev != null) dev.Text = "未检测到 " + modelMatch + " 显卡";
                        if (detail != null) detail.Text = "插好卡后点右侧重新检测";
                    }
                }
                catch { }
            };

            if (detect != null) detect.Click += (s, e) => { refresh(); };
            if (install != null)
            {
                install.Click += (s, e) =>
                {
                    NativeCore.LaunchToolkitApp(toolkit, exePrefix + "Installer.exe", LogCallback);
                    if (status != null) status.Text = "状态: 已启动安装器，请按提示操作";
                };
            }
            if (check != null)
            {
                check.Click += (s, e) =>
                {
                    NativeCore.LaunchToolkitApp(toolkit, exePrefix + "Check.exe", LogCallback);
                    if (status != null) status.Text = "状态: 已启动检查器（只读诊断）";
                };
            }
            if (uninstall != null)
            {
                uninstall.Click += (s, e) =>
                {
                    NativeCore.LaunchToolkitApp(toolkit, exePrefix + "Uninstaller.exe", LogCallback);
                    if (status != null) status.Text = "状态: 已启动卸载程序";
                };
            }
            if (folder != null)
            {
                folder.Click += (s, e) =>
                {
                    NativeCore.OpenToolkitFolder(toolkit, LogCallback);
                    if (status != null) status.Text = "状态: 已打开工具目录";
                };
            }

            refresh();
        }

        /// <summary>把按钮的 Tag 当 URL，点击时用默认浏览器打开。</summary>
        private void WireLinkButton(string name)
        {
            Button b = _window.FindName(name) as Button;
            if (b == null) return;
            b.Click += (s, e) =>
            {
                string url = Convert.ToString(b.Tag);
                if (string.IsNullOrEmpty(url)) return;
                try { Process.Start(url); } catch { }
            };
        }

        /// <summary>版本号过长时截断展示：内置 revision 是 40 位 commit，直接显示会撑破右侧布局。</summary>
        private string ShortVersion(string v)
        {
            if (string.IsNullOrEmpty(v)) return "未知";
            if (v.Length == 40) return "rev-" + v.Substring(0, 8);
            if (v.Length <= 22) return v;
            return v.Substring(0, 20) + "...";
        }

        private string GetSelectedProxyName()
        {
            string t = GetComboText(_cmbProxyDll);
            if (!NativeCore.IsKnownProxyName(t)) return "version.dll";
            return t;
        }

        /// <summary>
        /// 上游语义：MaxGeneratedFrames = 倍率 - 1，合法区间 1-5。
        /// 之前直接取选项首字符，导致 4X 写入 4、6X 写入 6 均越界，倍率选择实际失效。
        /// </summary>
        private string GetSelectedMaxFrames()
        {
            string t = GetComboText(_cmbMaxFramesLevel);
            if (string.IsNullOrEmpty(t)) return "3";
            if (t.StartsWith("2X")) return "1";
            if (t.StartsWith("3X")) return "2";
            if (t.StartsWith("4X")) return "3";
            if (t.StartsWith("5X")) return "4";
            if (t.StartsWith("6X")) return "5";
            return "3";
        }

        private void RenderPreflight(NativeCore.DlssPreflightResult pf)
        {
            if (_borderPreflight == null || _pnlPreflight == null) return;
            _pnlPreflight.Children.Clear();

            if (pf == null || pf.Items == null || pf.Items.Count == 0)
            {
                _borderPreflight.Visibility = Visibility.Collapsed;
                return;
            }

            for (int i = 0; i < pf.Items.Count; i++)
            {
                NativeCore.DlssCheckItem it = pf.Items[i];

                StackPanel row = new StackPanel();
                row.Orientation = Orientation.Horizontal;
                row.Margin = new Thickness(0, 2, 0, 2);

                TextBlock dot = new TextBlock();
                dot.Text = "●";
                dot.FontSize = 9;
                dot.VerticalAlignment = VerticalAlignment.Center;
                dot.Margin = new Thickness(0, 0, 6, 0);
                if (it.Ok) dot.Foreground = new SolidColorBrush(Color.FromRgb(22, 163, 74));
                else if (it.Blocking) dot.Foreground = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                else dot.Foreground = new SolidColorBrush(Color.FromRgb(217, 119, 6));

                TextBlock name = new TextBlock();
                name.Text = it.Name + ": ";
                name.FontSize = 11;
                name.Foreground = new SolidColorBrush(Color.FromRgb(55, 65, 81));

                TextBlock detail = new TextBlock();
                detail.Text = it.Detail;
                detail.FontSize = 11;
                detail.TextWrapping = TextWrapping.Wrap;
                detail.Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128));

                row.Children.Add(dot);
                row.Children.Add(name);
                row.Children.Add(detail);
                _pnlPreflight.Children.Add(row);
            }

            _borderPreflight.Visibility = Visibility.Visible;
        }

        private string BuildPreflightText(NativeCore.DlssPreflightResult pf, bool onlyBlocking)
        {
            if (pf == null || pf.Items == null) return "体检数据不可用";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < pf.Items.Count; i++)
            {
                NativeCore.DlssCheckItem it = pf.Items[i];
                if (onlyBlocking && (it.Ok || !it.Blocking)) continue;
                if (it.Ok) sb.AppendLine("[通过] " + it.Name + ": " + it.Detail);
                else if (it.Blocking) sb.AppendLine("[阻断] " + it.Name + ": " + it.Detail);
                else sb.AppendLine("[注意] " + it.Name + ": " + it.Detail);
            }
            return sb.ToString();
        }

        private void CheckDlssInDir(string dir)
        {
            if (!Directory.Exists(dir)) return;

            bool ini = File.Exists(Path.Combine(dir, "dlssg_sm86.ini"));
            string marker = Path.Combine(dir, ".dlssg_proxy");
            string usedProxy = null;
            if (File.Exists(marker))
            {
                try { usedProxy = File.ReadAllText(marker).Trim(); }
                catch { usedProxy = null; }
            }
            if (!NativeCore.IsKnownProxyName(usedProxy)) usedProxy = "version.dll";

            string proxyPath = Path.Combine(dir, usedProxy);
            bool proxyOk = File.Exists(proxyPath);
            bool re = File.Exists(Path.Combine(dir, "dinput8.dll"));
            bool sub = Directory.Exists(Path.Combine(dir, "dlssg_sm86"));

            bool legacy = false;
            if (proxyOk)
            {
                try { legacy = new FileInfo(proxyPath).Length >= 10L * 1024L * 1024L; }
                catch { legacy = false; }
            }

            if (proxyOk && ini)
            {
                string status = "已检测到 DLSS-G 补丁 (代理 " + usedProxy + " + dlssg_sm86.ini 运行中)";
                if (re && !string.Equals(usedProxy, "dinput8.dll", StringComparison.OrdinalIgnoreCase)) status += " [含 RE 框架支持]";
                if (legacy) status += " [单体版，运行库已内嵌]";
                else if (sub) status += " [0.4.0 内核完整]";
                else status += " [警告：缺少 dlssg_sm86 内核目录，0.4.0 下无法生效]";

                _txtDlssStatusInDir.Text = status;
                _txtDlssStatusInDir.Foreground = (legacy || sub)
                    ? new SolidColorBrush(Color.FromRgb(22, 163, 74))
                    : new SolidColorBrush(Color.FromRgb(217, 119, 6));

                if (re && _chkInjectReFramework != null && !string.Equals(usedProxy, "dinput8.dll", StringComparison.OrdinalIgnoreCase))
                    _chkInjectReFramework.IsChecked = true;
            }
            else
            {
                _txtDlssStatusInDir.Text = "该目录尚未注入 DLSS-G 补丁";
                _txtDlssStatusInDir.Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139));
            }
        }

        private string GetSelectedGpuModel()
        {
            if (_radioGpu40hx != null && _radioGpu40hx.IsChecked == true) return "40HX";
            if (_radioGpu50hx != null && _radioGpu50hx.IsChecked == true) return "50HX";
            if (_radioGpu30hx != null && _radioGpu30hx.IsChecked == true) return "30HX";
            return "90HX";
        }

        private void UpdateGpuModelHint()
        {
            try
            {
                if (_txtMiningGpuSummary == null) return;
                string model = GetSelectedGpuModel();
                string arch = model == "40HX" ? "TU106 · 8GB" : (model == "50HX" ? "TU102 · 10GB" : (model == "30HX" ? "TU116 · 6GB" : "GA102 · 10GB"));
                _txtMiningGpuSummary.Text = "已选 " + model + " (" + arch + ")";
            }
            catch { }
        }

        /// <summary>
        /// 统一解锁流程：新版 = NVPermissiveEFI（图形+算力+光追，启动更快）；
        /// 旧版 = 单文件 BOOTX64.EFI 固化（图形+算力，兼容性更好）。
        /// </summary>
        private async System.Threading.Tasks.Task RunUnlockFlow(bool newVersion)
        {
            var curStatus = SystemDetector.DetectAll();
            if (!curStatus.NvAppInstalled && curStatus.GpuDesc.IndexOf("CMP", StringComparison.OrdinalIgnoreCase) < 0)
            {
                ModernMessageBox.Show("【未检测到矿卡】\n系统未识别到 CMP 矿卡设备，请确认显卡安装与驱动状态。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var gpus = NativeCore.DetectAllMiningGpus();
            string model = GetSelectedGpuModel();
            if (gpus.Count == 0)
            {
                if (ModernMessageBox.Show("未自动检测到矿卡设备。是否仍要按【" + model + "】强制部署解锁引导？", "继续执行", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;
            }

            int channel = GetSelectedChannel();
            string channelName = channel == 0 ? "现代 UEFI 直启" : (channel == 1 ? "老主板 Shell" : (channel == 2 ? "老卡驱动 Hook" : "U 盘直启"));
            string usbDrive = null;
            if (channel == 3)
            {
                string sel = Convert.ToString(_cmbUsbDrives != null ? _cmbUsbDrives.SelectedItem : "");
                if (string.IsNullOrEmpty(sel) || sel.Length < 2 || sel.IndexOf("未检测", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    ModernMessageBox.Show("请先在【通道 D · U 盘直启】中选择一个 U 盘。", "需要选择 U 盘", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                usbDrive = sel.Substring(0, 2);
            }

            string verName = newVersion ? "新版 (NVPermissiveEFI · 图形+算力+光追)" : "旧版 (单文件 EFI 固化 · 图形+算力)";
            string body = newVersion
                ? "• 图形 / 算力 / 光追 一并解锁\n• 独立启动项部署（不动 Windows 引导，失败自动兜底）\n• 解锁文件已签名，Secure Boot 可关闭或注册后使用\n• 光追需配合 616.92 魔改驱动\n• 通道：" + channelName + "\n• 目标型号：" + model
                : "• 图形 + 算力 一起解锁（不含光追）\n• 单文件 BOOTX64.EFI 固化，兼容性更好\n• 通道：" + channelName + "\n• 目标型号：" + model;

            if (ModernMessageBox.Show("即将执行【" + verName + "】：\n\n" + body + "\n\n是否确认立即执行？", "确认执行解锁", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            var btn = newVersion ? _btnApplyNewUnlock : _btnApplyOldUnlock;
            if (btn != null) btn.IsEnabled = false;
            try
            {
                LogAudit(">>> 开始执行 " + verName + " (通道: " + channelName + ", 型号: " + model + ")");

                bool ok;
                if (newVersion)
                {
                    ok = await NativeCore.ApplyNvPermissiveUnlockAsync(gpus, channel, usbDrive, LogCallback);
                }
                else
                {
                    ok = await NativeCore.ApplyHybridMultiGpuUnlockAsync(gpus, true, true, channel, LogCallback);
                }

                RefreshSystemStatus();
                RefreshMiningGpuList();

                if (ok)
                {
                    string tip = newVersion
                        ? "新版解锁部署完成！已创建独立解锁启动项（Windows 引导保持原样）。\n\n重启后将自动执行解锁并进入系统；开机看到 \"Press ESC in N seconds...\" 倒计时请勿按 ESC。\n\n注意：光追需要同时安装 616.92 魔改驱动。"
                        : "旧版解锁部署完成！重启后即可获得 图形 + 算力。";
                    ModernMessageBox.Show(tip, "执行成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    ModernMessageBox.Show("解锁未完全成功，请查看左侧【审计日志】中的具体错误信息。", "执行结果", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            finally
            {
                if (btn != null) btn.IsEnabled = true;
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
            string line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg;
            if (_txtLogBox != null)
            {
                _txtLogBox.AppendText(line + "\r\n");
                _txtLogBox.ScrollToEnd();
            }
            if (_txtSideLogUnlock != null)
            {
                _txtSideLogUnlock.AppendText(line + "\r\n");
                _txtSideLogUnlock.ScrollToEnd();
            }
            if (_txtSideLogSpoof != null)
            {
                _txtSideLogSpoof.AppendText(line + "\r\n");
                _txtSideLogSpoof.ScrollToEnd();
            }
            if (_txtSideLogDlss != null)
            {
                _txtSideLogDlss.AppendText(line + "\r\n");
                _txtSideLogDlss.ScrollToEnd();
            }
        }
    }

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
