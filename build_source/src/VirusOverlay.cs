using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace CMP90HXToolbox
{

    public class VirusOverlay : Window
    {

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public POINT ptMinPosition;
            public POINT ptMaxPosition;
            public RECT rcNormalPosition;
        }

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int SW_SHOWMINIMIZED = 2;

        public event Action PrankCompleted;

        private readonly List<Rect> _desktopIconRects = new List<Rect>();
        private readonly List<Rect> _windowRects = new List<Rect>();
        private readonly List<Rect> _fakeWindowBase = new List<Rect>();

        private Canvas _chaosLayer;
        private Border _promptBox;
        private Image _noiseImage;
        private WriteableBitmap _noiseBitmap;
        private readonly int[] _noisePixels;
        private readonly int _noiseW = 200, _noiseH = 120;

        private readonly List<TextBlock> _rainColumns = new List<TextBlock>();
        private readonly List<List<string>> _rainLines = new List<List<string>>();
        private readonly List<TextBlock> _fakeWinTitles = new List<TextBlock>();
        private readonly List<Border> _fakeWindows = new List<Border>();
        private readonly List<Border> _glitchBands = new List<Border>();
        private readonly List<TextBlock> _errorLabels = new List<TextBlock>();
        private readonly List<double> _fakeWinPhase = new List<double>();

        private readonly DispatcherTimer _animTimer;
        private readonly DispatcherTimer _hitTimer;
        private readonly DispatcherTimer _autoCloseTimer;
        private readonly Random _rnd = new Random();
        private Matrix _toDip = Matrix.Identity;
        private double _elapsed;
        private bool _closing;
        private bool _promptReady;
        private int _frame;

        private static readonly char[] RainChars =
            "ｱｲｳｴｵｶｷｸｹｺｻｼｽｾｿﾀﾁﾂﾃﾄﾅﾆﾇﾈﾉﾊﾋﾌﾍﾎﾏﾐﾑﾒﾓﾔﾕﾖﾗﾘﾙﾚﾛﾜﾝ0123456789#$%&@*+=<>/\\|_"
                .ToCharArray();

        private static readonly string[] FakeTitles = new string[] {
            "system32.exe", "kernel_guard.sys", "SEC2_UNLOCK.bin", "nvlddmkm.sys",
            "PLM_BREAK.dll", "GA102_FW.img", "bootx64.efi", "WPR2_HANDLER"
        };

        private static readonly string[] FakeErrors = new string[] {
            "ERR 0x0000090HX", "SEC2 FALCON FAULT", "PLM LOCK BREACHED", "WPR2 RANGE OPEN",
            "MEMORY INTEGRITY FAIL", "KERNEL PANIC 0x1E", "BAR0 UNREACHABLE"
        };

        public VirusOverlay()
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            Topmost = true;

            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;
            Title = "90HX Studio";

            _noisePixels = new int[_noiseW * _noiseH];

            SourceInitialized += (s, e) =>
            {
                var src = PresentationSource.FromVisual(this);
                if (src != null && src.CompositionTarget != null)
                {
                    _toDip = src.CompositionTarget.TransformFromDevice;
                }
                ApplyClickThrough();
            };

            BuildUi();
            BuildPromptBox();

            _animTimer = new DispatcherTimer();
            _animTimer.Interval = TimeSpan.FromMilliseconds(180);
            _animTimer.Tick += OnAnimTick;

            _hitTimer = new DispatcherTimer();
            _hitTimer.Interval = TimeSpan.FromMilliseconds(60);
            _hitTimer.Tick += OnHitTick;

            _autoCloseTimer = new DispatcherTimer();
            _autoCloseTimer.Interval = TimeSpan.FromSeconds(26);
            _autoCloseTimer.Tick += (s, e) =>
            {
                _autoCloseTimer.Stop();
                FinishPrank();
            };

            Loaded += (s, e) =>
            {
                _animTimer.Start();
                _hitTimer.Start();
                _autoCloseTimer.Start();
                BuildGarbage();
                StartGathering();
            };
        }

        #region 只读信息采集（唯一用途：计算渲染坐标）

        private void GatherReadOnlyInfo()
        {

            try
            {
                foreach (var p in Process.GetProcesses())
                {
                    IntPtr h = p.MainWindowHandle;
                    if (h == IntPtr.Zero) continue;
                    if (!IsWindowVisible(h)) continue;

                    RECT r;
                    if (!GetWindowRect(h, out r)) continue;
                    int w = r.Right - r.Left, ht = r.Bottom - r.Top;
                    if (w < 160 || ht < 100) continue;
                    if (w >= SystemParameters.VirtualScreenWidth * 0.98 &&
                        ht >= SystemParameters.VirtualScreenHeight * 0.98) continue;

                    var wp = new WINDOWPLACEMENT();
                    wp.length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
                    bool gotPlacement = GetWindowPlacement(h, ref wp);

                    RECT use = r;
                    if (gotPlacement && wp.showCmd == SW_SHOWMINIMIZED)
                    {
                        use = wp.rcNormalPosition;
                        if (use.Right - use.Left < 160) continue;
                    }

                    var device = new Rect(use.Left, use.Top, use.Right - use.Left, use.Bottom - use.Top);
                    _windowRects.Add(Rect.Transform(device, _toDip));
                    if (_windowRects.Count >= 16) break;
                }
            }
            catch { }

            try
            {
                IntPtr listHwnd = FindDesktopListView();
                if (listHwnd != IntPtr.Zero)
                {
                    var el = System.Windows.Automation.AutomationElement.FromHandle(listHwnd);
                    if (el != null)
                    {
                        var items = el.FindAll(
                            System.Windows.Automation.TreeScope.Children,
                            System.Windows.Automation.Condition.TrueCondition);
                        foreach (System.Windows.Automation.AutomationElement item in items)
                        {
                            var rr = item.Current.BoundingRectangle;
                            if (rr.Width <= 0 || rr.Height <= 0) continue;
                            _desktopIconRects.Add(Rect.Transform(new Rect(rr.X, rr.Y, rr.Width, rr.Height), _toDip));
                            if (_desktopIconRects.Count >= 200) break;
                        }
                    }
                }
            }
            catch { }
        }

        private static IntPtr FindDesktopListView()
        {
            try
            {
                IntPtr progman = FindWindow("Progman", null);
                if (progman != IntPtr.Zero)
                {
                    IntPtr lv = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (lv != IntPtr.Zero)
                    {
                        IntPtr list = FindWindowEx(lv, IntPtr.Zero, "SysListView32", null);
                        if (list != IntPtr.Zero) return list;
                    }
                }

                IntPtr found = IntPtr.Zero;
                List<IntPtr> workers = new List<IntPtr>();
                EnumWindows((h, l) =>
                {
                    var sb = new StringBuilder(256);
                    GetClassName(h, sb, sb.Capacity);
                    if (sb.ToString() == "WorkerW") workers.Add(h);
                    return true;
                }, IntPtr.Zero);

                foreach (var w in workers)
                {
                    IntPtr dv = FindWindowEx(w, IntPtr.Zero, "SHELLDLL_DefView", null);
                    if (dv != IntPtr.Zero)
                    {
                        IntPtr list = FindWindowEx(dv, IntPtr.Zero, "SysListView32", null);
                        if (list != IntPtr.Zero) { found = list; break; }
                    }
                }
                return found;
            }
            catch { return IntPtr.Zero; }
        }

        private void StartGathering()
        {
            var worker = new System.Threading.Thread(() =>
            {
                try
                {
                    var t = new System.Threading.Thread(GatherReadOnlyInfo) { IsBackground = true };
                    t.Start();
                    t.Join(2500);
                }
                catch { }

                Dispatcher.BeginInvoke((Action)(() =>
                {
                    try { BuildChaos(); }
                    catch { }
                }));
            });
            worker.IsBackground = true;
            worker.Start();
        }

        #endregion

        #region UI 构建

        private void BuildUi()
        {

            var root = new Grid();
            root.Background = null;
            Content = root;

            var scrim = new Border();
            scrim.Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x02, 0x04, 0x08));
            scrim.IsHitTestVisible = false;
            root.Children.Add(scrim);

            _chaosLayer = new Canvas();
            _chaosLayer.IsHitTestVisible = false;
            _chaosLayer.ClipToBounds = true;
            root.Children.Add(_chaosLayer);

            _noiseBitmap = new WriteableBitmap(_noiseW, _noiseH, 96, 96, PixelFormats.Bgra32, null);
            _noiseImage = new Image();
            _noiseImage.Source = _noiseBitmap;
            _noiseImage.Stretch = Stretch.Fill;
            _noiseImage.Opacity = 0.10;
            _noiseImage.IsHitTestVisible = false;
            _noiseImage.Width = Width;
            _noiseImage.Height = Height;
            root.Children.Add(_noiseImage);

            var scan = new Border();
            scan.IsHitTestVisible = false;
            var tile = new DrawingBrush();
            var dg = new DrawingGroup();
            dg.Children.Add(new GeometryDrawing(
                new SolidColorBrush(Color.FromArgb(0x40, 0x00, 0x00, 0x00)), null,
                new RectangleGeometry(new Rect(0, 0, 1, 2))));
            tile.Drawing = dg;
            tile.TileMode = TileMode.Tile;
            tile.Viewport = new Rect(0, 0, 4, 4);
            tile.ViewportUnits = BrushMappingMode.Absolute;
            scan.Background = tile;
            root.Children.Add(scan);

            var beam = new Border();
            beam.Height = 60;
            beam.VerticalAlignment = VerticalAlignment.Top;
            beam.IsHitTestVisible = false;
            var beamBrush = new LinearGradientBrush();
            beamBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0x66, 0xFF, 0xCC), 0));
            beamBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0x1E, 0x66, 0xFF, 0xCC), 0.5));
            beamBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0x66, 0xFF, 0xCC), 1));
            beam.Background = beamBrush;
            _chaosLayer.Children.Add(beam);
            var beamAnim = new Storyboard();
            var beamSlide = new DoubleAnimation(0, Height, new Duration(TimeSpan.FromSeconds(2.6)));
            beamSlide.RepeatBehavior = RepeatBehavior.Forever;
            Storyboard.SetTarget(beamSlide, beam);
            Storyboard.SetTargetProperty(beamSlide, new PropertyPath("(0)", Canvas.TopProperty));
            beamAnim.Children.Add(beamSlide);
            beamAnim.Begin();
        }

        private void BuildGarbage()
        {
            double colWidth = Math.Max(150, Width / 16.0);
            int cols = (int)Math.Max(6, Width / colWidth);
            int linesPerCol = (int)Math.Max(12, Height / 26.0);

            for (int c = 0; c < cols; c++)
            {
                var lines = new List<string>();
                for (int i = 0; i < linesPerCol; i++) lines.Add(RandomRainLine(14 + _rnd.Next(20)));
                _rainLines.Add(lines);

                var tb = new TextBlock();
                tb.FontFamily = new FontFamily("Consolas");
                tb.FontSize = 15;
                tb.LineHeight = 22;
                tb.Foreground = new SolidColorBrush(Color.FromRgb(0x35, 0xC8, 0x6A));
                tb.Width = colWidth;
                tb.Height = Height;
                tb.IsHitTestVisible = false;
                tb.TextWrapping = TextWrapping.NoWrap;
                tb.CacheMode = new BitmapCache();
                Canvas.SetLeft(tb, c * colWidth);
                Canvas.SetTop(tb, 0);
                tb.Text = string.Join("\n", lines.ToArray());
                _chaosLayer.Children.Add(tb);
                _rainColumns.Add(tb);
            }

            for (int i = 0; i < 4; i++)
            {
                var tb = new TextBlock();
                tb.Text = FakeErrors[_rnd.Next(FakeErrors.Length)];
                tb.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x3B, 0x30));
                tb.FontFamily = new FontFamily("Consolas");
                tb.FontSize = 22;
                tb.FontWeight = FontWeights.Bold;
                tb.IsHitTestVisible = false;
                tb.CacheMode = new BitmapCache();
                _chaosLayer.Children.Add(tb);
                _errorLabels.Add(tb);
            }
        }

        private void BuildChaos()
        {

            foreach (var ir in _desktopIconRects)
            {
                var ghost = new Border();
                ghost.Width = Math.Max(12, ir.Width);
                ghost.Height = Math.Max(12, ir.Height);
                ghost.Background = new SolidColorBrush(Color.FromArgb(0x55, 0x40, 0xA0, 0xFF));
                ghost.BorderBrush = new SolidColorBrush(Color.FromArgb(0xAA, 0x90, 0xD0, 0xFF));
                ghost.BorderThickness = new Thickness(1);
                ghost.IsHitTestVisible = false;
                Canvas.SetLeft(ghost, ir.X);
                Canvas.SetTop(ghost, ir.Y);
                _chaosLayer.Children.Add(ghost);
            }

            int fakeCount = Math.Min(10, Math.Max(5, _windowRects.Count));
            for (int i = 0; i < fakeCount; i++)
            {
                Rect baseRect;
                if (_windowRects.Count > 0)
                {
                    baseRect = _windowRects[i % _windowRects.Count];
                }
                else
                {
                    double w = Width * (0.24 + _rnd.NextDouble() * 0.28);
                    double h = Height * (0.18 + _rnd.NextDouble() * 0.22);
                    baseRect = new Rect(_rnd.NextDouble() * Math.Max(1, Width - w), _rnd.NextDouble() * Math.Max(1, Height - h), w, h);
                }
                _fakeWindowBase.Add(baseRect);

                var win = new Border();
                win.Width = baseRect.Width;
                win.Height = baseRect.Height;
                win.BorderBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0xC8, 0xFF));
                win.BorderThickness = new Thickness(1.5);
                win.Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x06, 0x0E, 0x18));
                win.IsHitTestVisible = false;
                win.CacheMode = new BitmapCache();

                var grid = new Grid();
                grid.IsHitTestVisible = false;
                var r0 = new RowDefinition(); r0.Height = new GridLength(26);
                var r1 = new RowDefinition(); r1.Height = new GridLength(1, GridUnitType.Star);
                grid.RowDefinitions.Add(r0);
                grid.RowDefinitions.Add(r1);

                var bar = new Border();
                bar.Background = new LinearGradientBrush(
                    Color.FromRgb(0x0E, 0x3A, 0x66), Color.FromRgb(0x08, 0x1C, 0x30),
                    new Point(0, 0), new Point(1, 1));
                bar.IsHitTestVisible = false;
                var barText = new TextBlock();
                barText.Text = FakeTitles[_rnd.Next(FakeTitles.Length)];
                barText.Foreground = new SolidColorBrush(Color.FromRgb(0xAE, 0xE4, 0xFF));
                barText.FontFamily = new FontFamily("Consolas");
                barText.FontSize = 11;
                barText.Margin = new Thickness(8, 5, 0, 0);
                barText.IsHitTestVisible = false;
                bar.Child = barText;
                Grid.SetRow(bar, 0);
                grid.Children.Add(bar);
                _fakeWinTitles.Add(barText);

                var body = new StackPanel();
                body.Margin = new Thickness(8, 7, 8, 0);
                body.IsHitTestVisible = false;
                int lines = 3 + _rnd.Next(3);
                for (int l = 0; l < lines; l++)
                {
                    var tb = new TextBlock();
                    tb.Text = RandomRainLine(20 + _rnd.Next(30));
                    tb.Foreground = new SolidColorBrush(Color.FromRgb(0x5A, 0xE0, 0x9A));
                    tb.FontFamily = new FontFamily("Consolas");
                    tb.FontSize = 10.5;
                    tb.IsHitTestVisible = false;
                    body.Children.Add(tb);
                }
                var prog = new ProgressBar();
                prog.Height = 8;
                prog.Margin = new Thickness(0, 8, 0, 0);
                prog.Foreground = new SolidColorBrush(Color.FromRgb(0x4A, 0xC8, 0xFF));
                prog.Background = new SolidColorBrush(Color.FromArgb(0x55, 0x20, 0x40, 0x60));
                prog.BorderThickness = new Thickness(0);
                prog.IsHitTestVisible = false;
                prog.Value = _rnd.Next(10, 95);
                body.Children.Add(prog);
                Grid.SetRow(body, 1);
                grid.Children.Add(body);

                win.Child = grid;
                win.RenderTransformOrigin = new Point(0.5, 0.5);
                var tg = new TransformGroup();
                var st = new ScaleTransform(1, 1);
                var rot = new RotateTransform(0);
                tg.Children.Add(st);
                tg.Children.Add(rot);
                win.RenderTransform = tg;
                _chaosLayer.Children.Add(win);
                _fakeWindows.Add(win);
                _fakeWinPhase.Add(_rnd.NextDouble() * Math.PI * 2);
            }

            for (int i = 0; i < 4; i++)
            {
                var band = new Border();
                band.Height = 14 + _rnd.Next(40);
                band.Width = Width;
                band.Background = new SolidColorBrush(Color.FromArgb(0x30, 0x90, 0xE0, 0xFF));
                band.IsHitTestVisible = false;
                _chaosLayer.Children.Add(band);
                _glitchBands.Add(band);
            }
        }

        private static string RandomRainLine(int len)
        {
            var sb = new StringBuilder(len);
            for (int i = 0; i < len; i++) sb.Append(RainChars[_rndShared.Next(RainChars.Length)]);
            return sb.ToString();
        }

        private static readonly Random _rndShared = new Random();

        #endregion

        #region 中央提示框（唯一可交互元素）

        private void BuildPromptBox()
        {
            _promptBox = new Border();
            _promptBox.Width = 560;
            _promptBox.VerticalAlignment = VerticalAlignment.Center;
            _promptBox.HorizontalAlignment = HorizontalAlignment.Center;
            _promptBox.Background = new SolidColorBrush(Color.FromRgb(0x16, 0x18, 0x1E));
            _promptBox.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x3B, 0x30));
            _promptBox.BorderThickness = new Thickness(2);
            _promptBox.Padding = new Thickness(30, 26, 30, 26);
            _promptBox.IsHitTestVisible = true;
            _promptBox.Opacity = 0;

            var sh = new System.Windows.Media.Effects.DropShadowEffect();
            sh.Color = Colors.Black;
            sh.BlurRadius = 30;
            sh.ShadowDepth = 0;
            sh.Opacity = 0.8;
            _promptBox.Effect = sh;

            var panel = new StackPanel();

            var head = new StackPanel();
            head.Orientation = Orientation.Horizontal;
            head.Margin = new Thickness(0, 0, 0, 16);
            var warn = new Border();
            warn.Width = 38; warn.Height = 38;
            warn.CornerRadius = new CornerRadius(19);
            warn.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0x3B, 0x30));
            warn.Margin = new Thickness(0, 0, 14, 0);
            var wt = new TextBlock();
            wt.Text = "!";
            wt.FontSize = 23;
            wt.FontWeight = FontWeights.Bold;
            wt.Foreground = Brushes.White;
            wt.HorizontalAlignment = HorizontalAlignment.Center;
            wt.VerticalAlignment = VerticalAlignment.Center;
            warn.Child = wt;
            head.Children.Add(warn);
            var tb1 = new TextBlock();
            tb1.Text = "Windows 安全中心 · 紧急通知";
            tb1.FontSize = 16;
            tb1.FontWeight = FontWeights.Bold;
            tb1.Foreground = Brushes.White;
            var tb2 = new TextBlock();
            tb2.Text = "威胁编号 90HX-EGG-2026 · 纯视觉演示";
            tb2.FontSize = 11;
            tb2.Foreground = new SolidColorBrush(Color.FromRgb(0x9A, 0xA2, 0xB0));
            tb2.Margin = new Thickness(0, 3, 0, 0);
            var tstack = new StackPanel();
            tstack.Children.Add(tb1);
            tstack.Children.Add(tb2);
            head.Children.Add(tstack);
            panel.Children.Add(head);

            var main = new TextBlock();
            main.Text = "恭喜你，你的电脑没有中毒";
            main.FontSize = 27;
            main.FontWeight = FontWeights.Bold;
            main.Foreground = new SolidColorBrush(Color.FromRgb(0x30, 0xD1, 0x58));
            main.HorizontalAlignment = HorizontalAlignment.Center;
            main.Margin = new Thickness(0, 4, 0, 12);
            panel.Children.Add(main);

            var d1 = new TextBlock();
            d1.Text = "这是 90HX Studio 的开机彩蛋。刚才的全屏乱码、错位窗口与故障特效";
            d1.FontSize = 12;
            d1.Foreground = new SolidColorBrush(Color.FromRgb(0xC8, 0xCE, 0xD8));
            d1.TextAlignment = TextAlignment.Center;
            panel.Children.Add(d1);

            var d2 = new TextBlock();
            d2.Text = "全部由本程序绘制，未修改 / 移动 / 关闭你桌面上的任何图标与窗口。";
            d2.FontSize = 12;
            d2.Foreground = new SolidColorBrush(Color.FromRgb(0xC8, 0xCE, 0xD8));
            d2.TextAlignment = TextAlignment.Center;
            panel.Children.Add(d2);

            var d3 = new TextBlock();
            d3.Text = "（约 20 秒后也会自动关闭，你随时可以离开）";
            d3.FontSize = 10.5;
            d3.Foreground = new SolidColorBrush(Color.FromRgb(0x7A, 0x82, 0x90));
            d3.TextAlignment = TextAlignment.Center;
            d3.Margin = new Thickness(0, 4, 0, 0);
            panel.Children.Add(d3);

            var btn = new Button();
            btn.Content = "知道了，进入软件";
            btn.Width = 230;
            btn.Height = 40;
            btn.Margin = new Thickness(0, 20, 0, 2);
            btn.HorizontalAlignment = HorizontalAlignment.Center;
            btn.FontSize = 13.5;
            btn.FontWeight = FontWeights.SemiBold;
            btn.Foreground = Brushes.White;
            btn.Cursor = System.Windows.Input.Cursors.Hand;
            btn.Background = new SolidColorBrush(Color.FromRgb(0x00, 0x71, 0xE3));
            btn.BorderThickness = new Thickness(0);
            btn.Click += (s, e) => FinishPrank();
            panel.Children.Add(btn);

            _promptBox.Child = panel;

            var rootGrid = (Grid)Content;
            rootGrid.Children.Add(_promptBox);

            var popTimer = new DispatcherTimer();
            popTimer.Interval = TimeSpan.FromSeconds(7.0);
            popTimer.Tick += (s, e) =>
            {
                popTimer.Stop();
                _promptReady = true;
                var fade = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(300)));
                fade.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
                _promptBox.BeginAnimation(UIElement.OpacityProperty, fade);
            };
            popTimer.Start();
        }

        private void FinishPrank()
        {
            if (_closing) return;
            _closing = true;
            _animTimer.Stop();
            _hitTimer.Stop();
            _autoCloseTimer.Stop();

            var fade = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(240)));
            fade.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn };
            fade.Completed += (s2, e2) =>
            {
                try { Close(); } catch { }
                if (PrankCompleted != null) PrankCompleted();
            };
            BeginAnimation(UIElement.OpacityProperty, fade);
        }

        #endregion

        #region 键鼠穿透控制（轮询方式，可靠）

        private void OnHitTick(object sender, EventArgs e)
        {
            try
            {
                if (_closing || !_promptReady) return;

                POINT cur;
                if (!GetCursorPos(out cur)) return;

                var tl = _promptBox.PointToScreen(new Point(0, 0));
                double pad = 14;
                bool inside = cur.X >= tl.X - pad && cur.X <= tl.X + _promptBox.ActualWidth + pad &&
                              cur.Y >= tl.Y - pad && cur.Y <= tl.Y + _promptBox.ActualHeight + pad;

                if (inside) ClearClickThrough();
                else ApplyClickThrough();
            }
            catch { }
        }

        private void ApplyClickThrough()
        {
            try
            {
                IntPtr h = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (h == IntPtr.Zero) return;
                int ex = GetWindowLong(h, GWL_EXSTYLE);
                if ((ex & WS_EX_TRANSPARENT) == 0)
                {
                    SetWindowLong(h, GWL_EXSTYLE, ex | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE);
                }
            }
            catch { }
        }

        private void ClearClickThrough()
        {
            try
            {
                IntPtr h = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (h == IntPtr.Zero) return;
                int ex = GetWindowLong(h, GWL_EXSTYLE);
                if ((ex & WS_EX_TRANSPARENT) != 0)
                {
                    SetWindowLong(h, GWL_EXSTYLE, (ex & ~WS_EX_TRANSPARENT) | WS_EX_NOACTIVATE);
                }
            }
            catch { }
        }

        #endregion

        #region 动画驱动（低频 + 分批更新，保证不占满 CPU）

        private void OnAnimTick(object sender, EventArgs e)
        {
            _elapsed += 0.18;
            _frame++;

            switch (_frame % 4)
            {
                case 0: UpdateNoise(); break;
                case 1: UpdateRain(); break;
                case 2: UpdateFakeWindows(); break;
                case 3: UpdateGlitch(); UpdateErrorLabels(); break;
            }
        }

        private void UpdateNoise()
        {
            try
            {
                for (int i = 0; i < _noisePixels.Length; i++)
                {
                    int v = _rnd.Next(0, 190);
                    int r = v, g = v, b = v;
                    int k = _rnd.Next(200);
                    if (k < 2) { r = 40; g = 220; b = 110; }
                    else if (k < 4) { r = 230; g = 50; b = 50; }
                    _noisePixels[i] = unchecked((int)(0xFF000000 | ((uint)r << 16) | ((uint)g << 8) | (uint)b));
                }
                _noiseBitmap.WritePixels(new Int32Rect(0, 0, _noiseW, _noiseH), _noisePixels, _noiseW * 4, 0);
            }
            catch { }
        }

        private void UpdateRain()
        {
            try
            {
                for (int c = 0; c < _rainColumns.Count; c++)
                {
                    var lines = _rainLines[c];

                    lines.RemoveAt(lines.Count - 1);
                    lines.Insert(0, RandomRainLine(14 + _rnd.Next(20)));

                    var col = _rainColumns[c];
                    if (_rnd.NextDouble() < 0.06)
                    {
                        col.Foreground = new SolidColorBrush(Color.FromRgb(0xB8, 0xFF, 0xD0));
                    }
                    else
                    {
                        col.Foreground = new SolidColorBrush(Color.FromRgb(0x35, 0xC8, 0x6A));
                    }
                    col.Text = string.Join("\n", lines.ToArray());
                }
            }
            catch { }
        }

        private void UpdateFakeWindows()
        {
            for (int i = 0; i < _fakeWindows.Count; i++)
            {
                var win = _fakeWindows[i];
                var baseRect = _fakeWindowBase[i];
                double phase = _fakeWinPhase[i];

                double jx = Math.Sin(_elapsed * 2.1 + phase) * 22 + (_rnd.NextDouble() - 0.5) * 26;
                double jy = Math.Cos(_elapsed * 1.7 + phase) * 16 + (_rnd.NextDouble() - 0.5) * 20;
                double scale = 0.95 + Math.Sin(_elapsed * 2.6 + phase) * 0.05;

                Canvas.SetLeft(win, baseRect.X + jx);
                Canvas.SetTop(win, baseRect.Y + jy);

                var tg = win.RenderTransform as TransformGroup;
                if (tg != null && tg.Children.Count >= 2)
                {
                    var st = tg.Children[0] as ScaleTransform;
                    var rot = tg.Children[1] as RotateTransform;
                    if (st != null) { st.ScaleX = scale; st.ScaleY = scale; }
                    if (rot != null) { rot.Angle = Math.Sin(_elapsed * 1.2 + phase) * 1.4; }
                }

                win.Opacity = _rnd.NextDouble() < 0.08 ? 0.2 : (0.8 + _rnd.NextDouble() * 0.2);

                if (_rnd.NextDouble() < 0.08 && i < _fakeWinTitles.Count)
                {
                    _fakeWinTitles[i].Text = FakeTitles[_rnd.Next(FakeTitles.Length)];
                }
            }
        }

        private void UpdateGlitch()
        {
            for (int i = 0; i < _glitchBands.Count; i++)
            {
                var band = _glitchBands[i];
                Canvas.SetLeft(band, (_rnd.NextDouble() - 0.5) * 120);
                Canvas.SetTop(band, _rnd.NextDouble() * (Height - band.Height));
                band.Opacity = _rnd.NextDouble() < 0.4 ? 0 : (0.12 + _rnd.NextDouble() * 0.3);
            }
        }

        private void UpdateErrorLabels()
        {
            foreach (var tb in _errorLabels)
            {
                Canvas.SetLeft(tb, _rnd.NextDouble() * Math.Max(1, Width - 320));
                Canvas.SetTop(tb, _rnd.NextDouble() * Math.Max(1, Height - 80));
                tb.Opacity = _rnd.NextDouble() < 0.4 ? 0 : (0.65 + _rnd.NextDouble() * 0.35);
                if (_rnd.NextDouble() < 0.2)
                {
                    tb.Text = FakeErrors[_rnd.Next(FakeErrors.Length)];
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// 启动动画 v2「数字跃动」：00 → 90 仪表式滚动 + 进度条打满 + HX · STUDIO 浮现 + 蓝光定格，
    /// 约 2.6 秒后淡出进入主界面（点击任意处可提前跳过）。纯视觉动画，不动系统任何设置。
    /// 注：原 VirusOverlay（假病毒彩蛋）类保留未删，仅不再被启动流程调用。
    /// </summary>
    public class StartupAnimWindow : Window
    {
        public event Action PrankCompleted;

        private const double TotalMs = 2600.0;
        private const double BarFullWidth = 156.0;

        private readonly Stopwatch _sw = new Stopwatch();
        private readonly SolidColorBrush _numBaseBrush = new SolidColorBrush(Color.FromRgb(0xED, 0xF2, 0xF7));
        private readonly SolidColorBrush _numLitBrush = new SolidColorBrush(Color.FromRgb(0xDF, 0xF3, 0xFF));
        private readonly SolidColorBrush _barTrackBrush = new SolidColorBrush(Color.FromRgb(0x22, 0x24, 0x2B));
        private readonly SolidColorBrush _barFillBrush = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7));
        private readonly SolidColorBrush _suffixBrush = new SolidColorBrush(Color.FromRgb(0x4F, 0xC3, 0xF7));
        private readonly SolidColorBrush _footBrush = new SolidColorBrush(Color.FromRgb(0x5A, 0x5E, 0x68));

        private TextBlock _numText;
        private Border _barFill;
        private StackPanel _suffixPanel;
        private TextBlock _footText;
        private System.Windows.Media.Effects.DropShadowEffect _numGlow;
        private Border _card;
        private ScaleTransform _cardScale;
        private bool _finishing;
        private bool _started;

        public StartupAnimWindow()
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            ShowInTaskbar = false;
            Topmost = true;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Width = 540;
            Height = 420;
            Background = Brushes.Transparent;
            Title = "90HX Studio";

            BuildUi();

            Loaded += (s, e) =>
            {
                if (!_started)
                {
                    _started = true;

                    if (_card != null)
                    {
                        var ease = new CubicEase();
                        ease.EasingMode = EasingMode.EaseOut;
                        var fi = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromMilliseconds(240)));
                        fi.EasingFunction = ease;
                        var fs = new DoubleAnimation(0.95, 1.0, new Duration(TimeSpan.FromMilliseconds(300)));
                        fs.EasingFunction = ease;
                        _card.BeginAnimation(OpacityProperty, fi);
                        if (_cardScale != null)
                        {
                            _cardScale.BeginAnimation(ScaleTransform.ScaleXProperty, fs);
                            _cardScale.BeginAnimation(ScaleTransform.ScaleYProperty, fs);
                        }
                    }

                    _sw.Start();
                    System.Windows.Media.CompositionTarget.Rendering += OnFrame;
                }
            };
            MouseDown += (s, e) => Finish();
        }

        private static double Clamp01(double v)
        {
            if (v < 0) return 0;
            if (v > 1) return 1;
            return v;
        }

        private void BuildUi()
        {
            var root = new Grid();

            _card = new Border();
            _card.Width = 460;
            _card.Height = 330;
            _card.CornerRadius = new CornerRadius(26);
            _card.Background = new SolidColorBrush(Color.FromArgb(0xCE, 0x0B, 0x0C, 0x0F));
            _card.BorderBrush = new SolidColorBrush(Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF));
            _card.BorderThickness = new Thickness(1);
            _card.HorizontalAlignment = HorizontalAlignment.Center;
            _card.VerticalAlignment = VerticalAlignment.Center;
            _card.RenderTransformOrigin = new Point(0.5, 0.5);
            _cardScale = new ScaleTransform(1.0, 1.0);
            _card.RenderTransform = _cardScale;
            _card.Opacity = 0.0;
            var cardShadow = new System.Windows.Media.Effects.DropShadowEffect();
            cardShadow.Color = Colors.Black;
            cardShadow.BlurRadius = 46;
            cardShadow.ShadowDepth = 0;
            cardShadow.Opacity = 0.55;
            _card.Effect = cardShadow;

            var cardGrid = new Grid();

            var panel = new StackPanel();
            panel.HorizontalAlignment = HorizontalAlignment.Center;
            panel.VerticalAlignment = VerticalAlignment.Center;

            _numText = new TextBlock();
            _numText.Text = "00";
            _numText.FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI, sans-serif");
            _numText.FontSize = 96;
            _numText.FontWeight = FontWeights.ExtraBold;
            _numText.Foreground = _numBaseBrush;
            _numText.HorizontalAlignment = HorizontalAlignment.Center;

            _numGlow = new System.Windows.Media.Effects.DropShadowEffect();
            _numGlow.Color = Color.FromRgb(0x4F, 0xC3, 0xF7);
            _numGlow.BlurRadius = 36;
            _numGlow.ShadowDepth = 0;
            _numGlow.Opacity = 0.0;
            _numText.Effect = _numGlow;

            var barTrack = new Border();
            barTrack.Width = BarFullWidth;
            barTrack.Height = 4;
            barTrack.CornerRadius = new CornerRadius(2);
            barTrack.Background = _barTrackBrush;
            barTrack.HorizontalAlignment = HorizontalAlignment.Center;
            barTrack.Margin = new Thickness(0, 30, 0, 0);

            _barFill = new Border();
            _barFill.Width = 0;
            _barFill.Height = 4;
            _barFill.CornerRadius = new CornerRadius(2);
            _barFill.Background = _barFillBrush;
            _barFill.HorizontalAlignment = HorizontalAlignment.Left;
            barTrack.Child = _barFill;

            _suffixPanel = new StackPanel();
            _suffixPanel.HorizontalAlignment = HorizontalAlignment.Center;
            _suffixPanel.Margin = new Thickness(0, 22, 0, 0);
            _suffixPanel.Opacity = 0.0;
            _suffixPanel.RenderTransform = new TranslateTransform(0, 10);

            var suffix = new TextBlock();
            suffix.Text = "HX  ·  STUDIO";
            suffix.FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI, sans-serif");
            suffix.FontSize = 16;
            suffix.FontWeight = FontWeights.SemiBold;
            suffix.Foreground = _suffixBrush;
            _suffixPanel.Children.Add(suffix);

            _footText = new TextBlock();
            _footText.Text = "GA102 旗舰解锁控制台";
            _footText.FontFamily = new FontFamily("Microsoft YaHei UI, Microsoft YaHei, Segoe UI");
            _footText.FontSize = 11.5;
            _footText.Foreground = _footBrush;
            _footText.HorizontalAlignment = HorizontalAlignment.Center;
            _footText.Margin = new Thickness(0, 14, 0, 0);
            _footText.Opacity = 0.0;

            panel.Children.Add(_numText);
            panel.Children.Add(barTrack);
            panel.Children.Add(_suffixPanel);
            panel.Children.Add(_footText);

            var gloss = new Border();
            gloss.CornerRadius = new CornerRadius(25, 25, 0, 0);
            gloss.Height = 96;
            gloss.VerticalAlignment = VerticalAlignment.Top;
            gloss.IsHitTestVisible = false;
            var glossBrush = new LinearGradientBrush();
            glossBrush.StartPoint = new Point(0, 0);
            glossBrush.EndPoint = new Point(0, 1);
            glossBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF), 0.0));
            glossBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF), 1.0));
            gloss.Background = glossBrush;

            cardGrid.Children.Add(panel);
            cardGrid.Children.Add(gloss);
            _card.Child = cardGrid;
            root.Children.Add(_card);

            Content = root;
        }

        private void OnFrame(object sender, EventArgs e)
        {
            if (_finishing) return;

            double p = _sw.Elapsed.TotalMilliseconds / TotalMs;

            double q1 = Clamp01(p / 0.52);
            double eased = 1.0 - Math.Pow(1.0 - q1, 3.0);
            int val = (int)Math.Round(eased * 90.0);
            if (val > 90) val = 90;
            _numText.Text = val.ToString("00");
            _barFill.Width = BarFullWidth * eased;

            double q2 = Clamp01((p - 0.52) / 0.24);
            if (q2 > 0)
            {
                _numGlow.Opacity = 0.95 * q2;
                _numText.Foreground = _numLitBrush;
                _suffixPanel.Opacity = q2;
                _footText.Opacity = 0.9 * q2;
                var tr = _suffixPanel.RenderTransform as TranslateTransform;
                if (tr != null) tr.Y = 10.0 * (1.0 - q2);
            }

            double q3 = Clamp01((p - 0.88) / 0.12);
            if (q3 > 0) Opacity = 1.0 - q3;

            if (p >= 1.0) Finish();
        }

        private void Finish()
        {
            if (_finishing) return;
            _finishing = true;

            try
            {
                System.Windows.Media.CompositionTarget.Rendering -= OnFrame;
                _sw.Stop();
            }
            catch { }

            var fade = new DoubleAnimation(Opacity, 0.0, new Duration(TimeSpan.FromMilliseconds(340)));
            fade.Completed += (s, e) =>
            {
                try { Close(); }
                catch { }
                if (PrankCompleted != null)
                {
                    try { PrankCompleted(); }
                    catch { }
                }
            };
            BeginAnimation(OpacityProperty, fade);
        }
    }
}
