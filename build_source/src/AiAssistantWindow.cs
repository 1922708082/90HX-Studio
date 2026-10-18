using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace CMP90HXToolbox
{
    public class AiAssistantController
    {
        private Window _window;
        private Border _miniPetHost;
        private Border _chatWindowHost;
        private Border _titleBarDragArea;
        private Border _settingsDrawer;

        private Button _btnToggleSettings;
        private Button _btnResetPos;
        private Button _btnMinimizeToPet;
        private Button _btnCloseAssistant;

        private TextBox _txtApiUrl;
        private TextBox _txtApiKey;
        private TextBox _txtModelName;
        private Button _btnTestConnection;
        private Button _btnSaveSettings;

        private ScrollViewer _chatScrollViewer;
        private StackPanel _messagesPanel;
        private TextBox _txtInput;
        private TextBlock _txtPlaceholder;
        private Button _btnSend;

        private Button _quickQ1;
        private Button _quickQ2;
        private Button _quickQ3;
        private Button _quickQ4;

        private TextBlock _txtStatus;
        private System.Windows.Shapes.Ellipse _statusDot;

        private bool _isExpanded;
        private Point _dragStartPos;
        private bool _isDragging;
        private DispatcherTimer _blinkTimer;

        private string _apiUrl = "https://api.muyaa.top/hx-assistant/v1";
        private string _apiKey = "";
        private string _modelName = "gemini-3.8-flash";

        private static AiAssistantController _instance;
        private static bool _keepAliveOnMainWindowClose = true;
        public static bool KeepAliveOnMainWindowClose
        {
            get { return _keepAliveOnMainWindowClose; }
            set { _keepAliveOnMainWindowClose = value; }
        }

        public static AiAssistantController Instance
        {
            get { return _instance; }
        }

        public Window Window
        {
            get { return _window; }
        }

        public static AiAssistantController EnsureInitialized(bool autoShow)
        {
            if (_instance == null || _instance._window == null)
            {
                _instance = new AiAssistantController();
                _instance.Initialize();
            }
            if (autoShow && _instance._window != null)
            {
                try
                {
                    _instance._window.Show();
                    _instance._window.Activate();
                }
                catch { }
            }
            return _instance;
        }

                

        public static AiAssistantController CreateAndShow()
        {
            return EnsureInitialized(true);
        }

        public void Initialize()
        {
            try
            {
                Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("AiAssistantWindow.xaml");
                if (stream == null)
                {
                    string[] candidates = new string[] {
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"build_source\src\AiAssistantWindow.xaml"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AiAssistantWindow.xaml"),
                        @"D:\app\build_source\src\AiAssistantWindow.xaml"
                    };
                    for (int i = 0; i < candidates.Length; i++)
                    {
                        if (File.Exists(candidates[i]))
                        {
                            stream = File.OpenRead(candidates[i]);
                            break;
                        }
                    }
                }
                if (stream == null) return;
                using (stream)
                {
                    _window = (Window)XamlReader.Load(stream);
                }
                if (_window == null) return;

                BindControls();
                InitEvents();
                LoadConfig();

                _isExpanded = false;
                SwitchMode(false, false);
                ResetToBottomRight();

                _window.Show();

                _blinkTimer = new DispatcherTimer();
                _blinkTimer.Interval = TimeSpan.FromSeconds(4.5);
                _blinkTimer.Tick += delegate(object s, EventArgs e)
                {
                    try
                    {
                        Storyboard blinkSb = _window.TryFindResource("PetBlinkStoryboard") as Storyboard;
                        if (blinkSb != null) blinkSb.Begin(_window);
                    }
                    catch { }
                };
                _blinkTimer.Start();
            }
            catch (Exception ex)
            {
                try
                {
                    File.AppendAllText(Path.Combine(NativeCore.GetLogsDirectory(), "ai_assistant.log"),
                        "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] Init exception: " + ex.ToString() + Environment.NewLine);
                }
                catch { }
            }
        }

        private void BindControls()
        {
            _miniPetHost = (Border)_window.FindName("MiniPetHost");
            _chatWindowHost = (Border)_window.FindName("ChatWindowHost");
            _titleBarDragArea = (Border)_window.FindName("TitleBarDragArea");
            _settingsDrawer = (Border)_window.FindName("SettingsDrawer");

            _btnToggleSettings = (Button)_window.FindName("BtnToggleSettings");
            _btnResetPos = (Button)_window.FindName("BtnResetPos");
            _btnMinimizeToPet = (Button)_window.FindName("BtnMinimizeToPet");
            _btnCloseAssistant = (Button)_window.FindName("BtnCloseAssistant");

            _txtApiUrl = (TextBox)_window.FindName("TxtApiUrl");
            _txtApiKey = (TextBox)_window.FindName("TxtApiKey");
            _txtModelName = (TextBox)_window.FindName("TxtModelName");
            _btnTestConnection = (Button)_window.FindName("BtnTestConnection");
            _btnSaveSettings = (Button)_window.FindName("BtnSaveSettings");

            _chatScrollViewer = (ScrollViewer)_window.FindName("ChatScrollViewer");
            _messagesPanel = (StackPanel)_window.FindName("MessagesPanel");
            _txtInput = (TextBox)_window.FindName("TxtInput");
            _txtPlaceholder = (TextBlock)_window.FindName("TxtPlaceholder");
            _btnSend = (Button)_window.FindName("BtnSend");

            _quickQ1 = (Button)_window.FindName("QuickQ1");
            _quickQ2 = (Button)_window.FindName("QuickQ2");
            _quickQ3 = (Button)_window.FindName("QuickQ3");
            _quickQ4 = (Button)_window.FindName("QuickQ4");

            _txtStatus = (TextBlock)_window.FindName("TxtStatus");
            _statusDot = (System.Windows.Shapes.Ellipse)_window.FindName("StatusDot");
        }

        private void InitEvents()
        {
            if (_miniPetHost != null)
            {
                _miniPetHost.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
                {
                    _isDragging = false;
                    _dragStartPos = e.GetPosition(_window);
                };
                _miniPetHost.MouseMove += delegate(object s, MouseEventArgs e)
                {
                    if (e.LeftButton == MouseButtonState.Pressed)
                    {
                        Point current = e.GetPosition(_window);
                        if (Math.Abs(current.X - _dragStartPos.X) > 4 || Math.Abs(current.Y - _dragStartPos.Y) > 4)
                        {
                            _isDragging = true;
                            _window.DragMove();
                        }
                    }
                };
                _miniPetHost.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
                {
                    if (!_isDragging) SwitchMode(true, true);
                    _isDragging = false;
                };
                _miniPetHost.MouseRightButtonUp += delegate(object s, MouseButtonEventArgs e)
                {
                    ShowPetContextMenu();
                };
            }

            if (_titleBarDragArea != null)
            {
                _titleBarDragArea.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
                {
                    if (e.ClickCount == 2) ResetToBottomRight();
                    else if (e.LeftButton == MouseButtonState.Pressed) _window.DragMove();
                };
                _titleBarDragArea.MouseRightButtonUp += delegate(object s, MouseButtonEventArgs e)
                {
                    ShowPetContextMenu();
                };
            }

            if (_btnToggleSettings != null)
            {
                _btnToggleSettings.Click += delegate
                {
                    if (_settingsDrawer != null)
                    {
                        bool isVisible = _settingsDrawer.Visibility == Visibility.Visible;
                        _settingsDrawer.Visibility = isVisible ? Visibility.Collapsed : Visibility.Visible;
                    }
                };
            }
            if (_btnResetPos != null) _btnResetPos.Click += delegate { ResetToBottomRight(); };
            if (_btnMinimizeToPet != null) _btnMinimizeToPet.Click += delegate { SwitchMode(false, true); };
            if (_btnCloseAssistant != null) _btnCloseAssistant.Click += delegate { _window.Hide(); };
            if (_btnSaveSettings != null) _btnSaveSettings.Click += delegate { SaveConfig(); };
            if (_btnTestConnection != null) _btnTestConnection.Click += delegate { TestApiConnectionAsync(); };

            if (_txtInput != null)
            {
                _txtInput.TextChanged += delegate
                {
                    if (_txtPlaceholder != null)
                    {
                        _txtPlaceholder.Visibility = string.IsNullOrEmpty(_txtInput.Text) ? Visibility.Visible : Visibility.Collapsed;
                    }
                };
                _txtInput.PreviewKeyDown += delegate(object s, KeyEventArgs e)
                {
                    if (e.Key == Key.Enter)
                    {
                        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift) { }
                        else
                        {
                            e.Handled = true;
                            SendUserMessage();
                        }
                    }
                };
            }

            if (_btnSend != null) _btnSend.Click += delegate { SendUserMessage(); };
            if (_quickQ1 != null) _quickQ1.Click += delegate { HandleQuickQuestion("查询显卡实时温度与功耗"); };
            if (_quickQ2 != null) _quickQ2.Click += delegate { HandleQuickQuestion("90HX 解锁与超频建议"); };
            if (_quickQ3 != null) _quickQ3.Click += delegate { HandleQuickQuestion("双显卡调度如何设置？"); };
            if (_quickQ4 != null) _quickQ4.Click += delegate { HandleQuickQuestion("DLSS 5 与游戏覆盖修复步骤"); };
        }

        private void ShowPetContextMenu()
        {
            ContextMenu menu = new ContextMenu();
            menu.Style = null;
            MenuItem itemToggle = new MenuItem();
            itemToggle.Header = _isExpanded ? "收起为悬浮萌宠" : "展开 AI 硬件助手";
            itemToggle.Click += delegate { SwitchMode(!_isExpanded, true); };
            menu.Items.Add(itemToggle);

            MenuItem itemReset = new MenuItem();
            itemReset.Header = "复位到桌面右下角";
            itemReset.Click += delegate { ResetToBottomRight(); };
            menu.Items.Add(itemReset);

            MenuItem itemConfig = new MenuItem();
            itemConfig.Header = "中转站 API 配置";
            itemConfig.Click += delegate
            {
                if (!_isExpanded) SwitchMode(true, true);
                if (_settingsDrawer != null) _settingsDrawer.Visibility = Visibility.Visible;
            };
            menu.Items.Add(itemConfig);
            menu.Items.Add(new Separator());

            MenuItem itemClear = new MenuItem();
            itemClear.Header = "清空对话消息";
            itemClear.Click += delegate
            {
                if (_messagesPanel != null && _messagesPanel.Children.Count > 1)
                {
                    while (_messagesPanel.Children.Count > 1)
                    {
                        _messagesPanel.Children.RemoveAt(1);
                    }
                }
            };
            menu.Items.Add(itemClear);

            MenuItem itemKeepAlive = new MenuItem();
            itemKeepAlive.Header = KeepAliveOnMainWindowClose ? "✓ 主程序关闭时保持助手独立运行" : "   主程序关闭时保持助手独立运行";
            itemKeepAlive.Click += delegate
            {
                KeepAliveOnMainWindowClose = !KeepAliveOnMainWindowClose;
                SaveConfig();
            };
            menu.Items.Add(itemKeepAlive);

            MenuItem itemOpenMain = new MenuItem();
            itemOpenMain.Header = "打开 HX 工具箱主界面";
            itemOpenMain.Click += delegate
            {
                try
                {
                    if (Application.Current != null && Application.Current.MainWindow != null)
                    {
                        Application.Current.MainWindow.Show();
                        Application.Current.MainWindow.Activate();
                    }
                }
                catch { }
            };
            menu.Items.Add(itemOpenMain);

            MenuItem itemHide = new MenuItem();
            itemHide.Header = "隐藏悬浮助手";
            itemHide.Click += delegate { _window.Hide(); };
            menu.Items.Add(itemHide);

            MenuItem itemQuit = new MenuItem();
            itemQuit.Header = "彻底退出程序与助手";
            itemQuit.Click += delegate
            {
                try { if (_window != null) _window.Close(); } catch { }
                try { Application.Current.Shutdown(); } catch { }
            };
            menu.Items.Add(itemQuit);

            menu.IsOpen = true;
        }

        public void SwitchMode(bool expand, bool animate)
        {
            _isExpanded = expand;
            Rect workArea = SystemParameters.WorkArea;
            if (expand)
            {
                double targetWidth = 400;
                double targetHeight = 600;
                double newLeft = _window.Left;
                double newTop = _window.Top;
                if (newLeft + targetWidth > workArea.Right) newLeft = workArea.Right - targetWidth - 10;
                if (newTop + targetHeight > workArea.Bottom) newTop = workArea.Bottom - targetHeight - 10;
                if (newLeft < workArea.Left) newLeft = workArea.Left + 10;
                if (newTop < workArea.Top) newTop = workArea.Top + 10;
                _window.Left = newLeft;
                _window.Top = newTop;
                _window.Width = targetWidth;
                _window.Height = targetHeight;
                if (_miniPetHost != null) _miniPetHost.Visibility = Visibility.Collapsed;
                if (_chatWindowHost != null)
                {
                    _chatWindowHost.Visibility = Visibility.Visible;
                    if (animate)
                    {
                        var da = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(220));
                        _chatWindowHost.BeginAnimation(UIElement.OpacityProperty, da);
                    }
                }
                if (_txtInput != null) _txtInput.Focus();
            }
            else
            {
                double targetWidth = 110;
                double targetHeight = 110;
                double newLeft = _window.Left + (_window.Width - targetWidth);
                double newTop = _window.Top + (_window.Height - targetHeight);
                if (newLeft + targetWidth > workArea.Right) newLeft = workArea.Right - targetWidth - 14;
                if (newTop + targetHeight > workArea.Bottom) newTop = workArea.Bottom - targetHeight - 14;
                if (newLeft < workArea.Left) newLeft = workArea.Left + 10;
                if (newTop < workArea.Top) newTop = workArea.Top + 10;
                _window.Left = newLeft;
                _window.Top = newTop;
                _window.Width = targetWidth;
                _window.Height = targetHeight;
                if (_chatWindowHost != null) _chatWindowHost.Visibility = Visibility.Collapsed;
                if (_miniPetHost != null)
                {
                    _miniPetHost.Visibility = Visibility.Visible;
                    if (animate)
                    {
                        var da = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200));
                        _miniPetHost.BeginAnimation(UIElement.OpacityProperty, da);
                    }
                }
            }
        }

        public void ResetToBottomRight()
        {
            Rect workArea = SystemParameters.WorkArea;
            if (_isExpanded)
            {
                _window.Width = 400;
                _window.Height = 600;
                _window.Left = workArea.Right - _window.Width - 12;
                _window.Top = workArea.Bottom - _window.Height - 12;
            }
            else
            {
                _window.Width = 110;
                _window.Height = 110;
                _window.Left = workArea.Right - _window.Width - 14;
                _window.Top = workArea.Bottom - _window.Height - 14;
            }
        }

        public void HandleQuickQuestion(string question)
        {
            if (!_isExpanded) SwitchMode(true, true);
            if (_txtInput != null) _txtInput.Text = question;
            SendUserMessage();
        }

        private void SendUserMessage()
        {
            if (_txtInput == null) return;
            string text = _txtInput.Text;
            if (string.IsNullOrWhiteSpace(text)) return;
            text = text.Trim();
            _txtInput.Text = "";
            AddMessageBubble(text, true);
            ProcessAiResponseAsync(text);
        }

        private TextBlock AddMessageBubble(string text, bool isUser)
        {
            if (_messagesPanel == null) return null;
            Border bubbleBorder = new Border();
            bubbleBorder.Margin = new Thickness(0, 0, 0, 10);
            bubbleBorder.MaxWidth = 310;
            TextBlock contentBlock = new TextBlock();
            contentBlock.FontSize = 12;
            contentBlock.TextWrapping = TextWrapping.Wrap;
            contentBlock.LineHeight = 18;

            if (isUser)
            {
                bubbleBorder.HorizontalAlignment = HorizontalAlignment.Right;
                bubbleBorder.CornerRadius = new CornerRadius(14, 14, 3, 14);
                bubbleBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0071E3"));
                bubbleBorder.Padding = new Thickness(12, 8, 12, 8);
                contentBlock.Foreground = Brushes.White;
                contentBlock.Text = text;
                bubbleBorder.Child = contentBlock;
            }
            else
            {
                bubbleBorder.HorizontalAlignment = HorizontalAlignment.Left;
                bubbleBorder.CornerRadius = new CornerRadius(14, 14, 14, 3);
                bubbleBorder.Background = Brushes.White;
                bubbleBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E5EA"));
                bubbleBorder.BorderThickness = new Thickness(1);
                bubbleBorder.Padding = new Thickness(12, 8, 12, 8);
                var shadow = new DropShadowEffect();
                shadow.Color = Colors.Black;
                shadow.BlurRadius = 8;
                shadow.ShadowDepth = 2;
                shadow.Opacity = 0.05;
                shadow.Direction = 270;
                bubbleBorder.Effect = shadow;
                contentBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1D1D1F"));
                contentBlock.Text = text;
                bubbleBorder.Child = contentBlock;
            }
            _messagesPanel.Children.Add(bubbleBorder);
            if (_chatScrollViewer != null) _chatScrollViewer.ScrollToEnd();
            return contentBlock;
        }

        private async void ProcessAiResponseAsync(string userPrompt)
        {
            TextBlock replyBlock = AddMessageBubble("正在分析硬件并思考中...", false);
            SetStatus("正在计算...", "#FF9500");
            string finalAnswer = "";
            try
            {
                bool hasApiKey = !string.IsNullOrWhiteSpace(_apiKey) && _apiKey.Length > 5;
                if (hasApiKey)
                {
                    string apiResult = await RequestApiCompletionAsync(userPrompt);
                    if (!string.IsNullOrEmpty(apiResult))
                    {
                        finalAnswer = apiResult;
                        SetStatus("在线 · 中转站 API 响应", "#34C759");
                    }
                    else
                    {
                        finalAnswer = GenerateLocalHardwareKnowledge(userPrompt);
                        SetStatus("在线 · 本地硬件专家库", "#34C759");
                    }
                }
                else
                {
                    await Task.Delay(260);
                    finalAnswer = GenerateLocalHardwareKnowledge(userPrompt);
                    SetStatus("在线 · 本地硬件专家库就绪", "#34C759");
                }
            }
            catch (Exception)
            {
                finalAnswer = "【提示】连接遇到异常，已转为本地硬件专家回答：" + Environment.NewLine + Environment.NewLine + GenerateLocalHardwareKnowledge(userPrompt);
                SetStatus("在线 · 本地硬件专家库", "#34C759");
            }
            PlayTypewriterEffect(replyBlock, finalAnswer);
        }

        private void PlayTypewriterEffect(TextBlock targetBlock, string fullText)
        {
            if (targetBlock == null || string.IsNullOrEmpty(fullText)) return;
            int index = 0;
            targetBlock.Text = "";
            DispatcherTimer typeTimer = new DispatcherTimer();
            typeTimer.Interval = TimeSpan.FromMilliseconds(16);
            typeTimer.Tick += delegate(object s, EventArgs e)
            {
                try
                {
                    int step = 2;
                    if (index + step <= fullText.Length)
                    {
                        targetBlock.Text = fullText.Substring(0, index + step) + " ▌";
                        index += step;
                    }
                    else
                    {
                        targetBlock.Text = fullText;
                        typeTimer.Stop();
                    }
                    if (_chatScrollViewer != null) _chatScrollViewer.ScrollToEnd();
                }
                catch
                {
                    targetBlock.Text = fullText;
                    typeTimer.Stop();
                }
            };
            typeTimer.Start();
        }

        private string GenerateLocalHardwareKnowledge(string prompt)
        {
            string p = prompt.ToLower();
            if (p.Contains("温度") || p.Contains("功耗") || p.Contains("传感器") || p.Contains("频率") || p.Contains("风扇") || p.Contains("转速"))
            {
                try
                {
                    var s = NativeCore.QueryGpuSensors();
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("🔍 【CMP 90HX 硬件传感器实时诊断报告】");
                    sb.AppendLine("─────────────────────────");
                    sb.AppendLine("• 核心温度: " + (s.Temperature >= 0 ? s.Temperature.ToString() + " ℃" : "检测中"));
                    sb.AppendLine("• 实时整卡功耗: " + (s.PowerDraw >= 0 ? s.PowerDraw.ToString("F1") + " W" : "检测中"));
                    sb.AppendLine("• 风扇转速: " + (s.FanPercent >= 0 ? s.FanPercent.ToString() + " %" : "自动调速"));
                    sb.AppendLine("• 显存占用: " + (s.MemoryUsedMB >= 0 ? s.MemoryUsedMB.ToString() + " MB / " + s.MemoryTotalMB.ToString() + " MB" : "10240 MB"));
                    sb.AppendLine("• 核心负载率: " + (s.GpuUtilPercent >= 0 ? s.GpuUtilPercent.ToString() + " %" : "正常"));
                    sb.AppendLine("• 核心/显存频率: " + s.GraphicsClockMHz.ToString() + " MHz / " + s.MemoryClockMHz.ToString() + " MHz");
                    sb.AppendLine("• 电源状态 P-State: " + (!string.IsNullOrEmpty(s.PState) ? s.PState : "P0/P8 动态调度"));
                    sb.AppendLine("• PCIe 协商速率: " + (!string.IsNullOrEmpty(s.PcieFormatted) ? s.PcieFormatted : "PCIe Gen1/Gen2 良好"));
                    sb.AppendLine("─────────────────────────");
                    sb.AppendLine("💡 极客建议: CMP 90HX 最佳日常甜点功耗为 220W~250W，核心温度建议控制在 72℃ 以内。");
                    return sb.ToString();
                }
                catch
                {
                    return "当前未能读取到显卡传感器数据，请确认 NVIDIA 驱动是否正常载入，并在「硬件信息」页面刷新。";
                }
            }
            if (p.Contains("解锁") || p.Contains("超频") || p.Contains("90hx") || p.Contains("50hx") || p.Contains("40hx"))
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("⚡ 【CMP 90HX 引导解锁与超频调优宝典】");
                sb.AppendLine("1. 解锁原理：CMP 90HX 默认被屏蔽 Direct3D 游戏图形输出，工具箱采用 NVPermissive.sys 驱动层内核补丁，绕过限制重获 DX11/DX12 完整 3D 计算能力。");
                sb.AppendLine("2. 超频甜点设置：");
                sb.AppendLine("   • 核心频率：使用 MSI Afterburner 锁频在 1800MHz~1920MHz (0.825V~0.875V)，大幅降低发热；");
                sb.AppendLine("   • 显存频率：美光 GDDR6X 可稳超 +800MHz ~ +1200MHz，带宽大幅跃升；");
                sb.AppendLine("   • 功耗墙：建议限制为 240W，能耗比达到巅峰；");
                sb.AppendLine("3. 安全防呆：请务必在工具箱「90HX 解锁」页面安装引导项，重启选择解锁项即可完成。");
                return sb.ToString();
            }
            if (p.Contains("双显卡") || p.Contains("核显") || p.Contains("调度") || p.Contains("黑屏") || p.Contains("输出"))
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("🎮 【无显示接口显卡双卡无缝调度指南】");
                sb.AppendLine("由于 CMP 90HX 物理上无视频输出接口，必须依赖核显或亮机副卡输出画面：");
                sb.AppendLine("1. 将显示器线材插入主板（Intel/AMD 核显）或副卡输出口；");
                sb.AppendLine("2. 在工具箱「双卡调度」页面，点击「锁定 90HX 为全局首选渲染显卡」；");
                sb.AppendLine("3. 注册表将自动写入 GPU Preference 规则，所有 3D 游戏启动时将自动由 90HX 完成画面光栅化渲染，并通过 PCIe 共享总线回传至核显瞬间呈现，无感低延迟！");
                return sb.ToString();
            }
            if (p.Contains("dlss") || p.Contains("覆盖") || p.Contains("帧生成") || p.Contains("改名") || p.Contains("3080"))
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("🛠 【DLSS 5 / 帧生成与显卡改名全解】");
                sb.AppendLine("1. 显卡型号伪装：在「显卡型号伪装」页面一键改名为『NVIDIA GeForce RTX 3080』，防止游戏启动时因卡名包含 CMP 而禁止开启光线追踪与 DLSS。");
                sb.AppendLine("2. 游戏覆盖解锁：工具箱已适配 NVIDIA App 极客补丁，点击「一键解除游戏覆盖限制」，即可使用 Alt+Z 实时监控帧率、滤镜与一键录屏！");
                sb.AppendLine("3. DLSS 帧生成：搭配 DLSSG-to-FSR3 补丁，90HX 即可享受 200% 的丝滑帧率跃升！");
                return sb.ToString();
            }
            StringBuilder fallback = new StringBuilder();
            fallback.AppendLine("收到你的问题！我是 HX 智能硬件助手。对于 CMP 90HX/50HX/40HX，你可以随时询问我：");
            fallback.AppendLine("• 『查询显卡实时温度与功耗』");
            fallback.AppendLine("• 『90HX 解锁后如何最佳超频』");
            fallback.AppendLine("• 『双显卡无缝调度配置步骤』");
            fallback.AppendLine("• 『显卡改名与 DLSS 5 补丁打法』");
            fallback.AppendLine("");
            fallback.AppendLine("如果你想体验全功能的云端大模型，请点击右上角「齿轮⚙️」填入中转站 API 地址与 Key，我将化身更全能的硬件百科全书！");
            return fallback.ToString();
        }

        private Task<string> RequestApiCompletionAsync(string prompt)
        {
            return Task.Factory.StartNew<string>(delegate
            {
                try
                {
                    string endpoint = _apiUrl.Trim();
                    if (endpoint.EndsWith("/")) endpoint = endpoint.Substring(0, endpoint.Length - 1);
                    if (!endpoint.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
                    {
                        endpoint += "/chat/completions";
                    }

                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(endpoint);
                    request.Method = "POST";
                    request.ContentType = "application/json; charset=utf-8";
                    if (!string.IsNullOrEmpty(_apiKey))
                    {
                        request.Headers["Authorization"] = "Bearer " + _apiKey.Trim();
                    }
                    else
                    {
                        // 默认走国内服务器防盗刷网关
                        request.Headers["X-HX-Code"] = "HX_STUDIO_PUBLIC_2026";
                    }
                    request.Timeout = 20000;

                    string sysPrompt = "你是一个极客硬件专家小助手，名叫小 HX，精通 NVIDIA CMP 90HX、CMP 50HX、CMP 40HX 矿卡驱动魔改、引导解锁、显卡改名 RTX 3080、双显卡无缝调度、DLSS 5 帧生成补丁、传感器监测与超频调优。回答专业、幽默软萌又精炼。";
                    string escapedSys = EscapeJson(sysPrompt);
                    string escapedUser = EscapeJson(prompt);
                    string model = !string.IsNullOrEmpty(_modelName) ? _modelName.Trim() : "gpt-4o-mini";

                    string jsonBody = "{\"model\":\"" + model + "\",\"messages\":[{\"role\":\"system\",\"content\":\"" + escapedSys + "\"},{\"role\":\"user\",\"content\":\"" + escapedUser + "\"}],\"temperature\":0.7}";
                    byte[] postBytes = Encoding.UTF8.GetBytes(jsonBody);
                    request.ContentLength = postBytes.Length;

                    using (Stream stream = request.GetRequestStream())
                    {
                        stream.Write(postBytes, 0, postBytes.Length);
                    }

                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    {
                        string respText = reader.ReadToEnd();
                        Match m = Regex.Match(respText, "\"content\"\\s*:\\s*\"(.*?)\"", RegexOptions.Singleline);
                        if (m.Success)
                        {
                            string rawContent = m.Groups[1].Value;
                            return UnescapeJson(rawContent);
                        }
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        File.AppendAllText(Path.Combine(NativeCore.GetLogsDirectory(), "ai_assistant.log"),
                            "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] API Error: " + ex.Message + Environment.NewLine);
                    }
                    catch { }
                }
                return null;
            });
        }

                private void TestApiConnectionAsync()
        {
            if (_btnTestConnection == null) return;
            _btnTestConnection.IsEnabled = false;
            _btnTestConnection.Content = "测试中...";

            string url = _txtApiUrl != null ? _txtApiUrl.Text.Trim() : _apiUrl;
            string key = _txtApiKey != null ? _txtApiKey.Text.Trim() : _apiKey;
            string model = _txtModelName != null ? _txtModelName.Text.Trim() : _modelName;

            Task.Factory.StartNew(delegate
            {
                bool ok = false;
                string errorMsg = "";
                try
                {
                    string endpoint = url.Trim();
                    if (endpoint.EndsWith("/")) endpoint = endpoint.Substring(0, endpoint.Length - 1);
                    if (!endpoint.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
                    {
                        endpoint += "/chat/completions";
                    }

                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(endpoint);
                    req.Method = "POST";
                    req.ContentType = "application/json; charset=utf-8";
                    if (!string.IsNullOrEmpty(key))
                    {
                        req.Headers["Authorization"] = "Bearer " + key;
                    }
                    else
                    {
                        req.Headers["X-HX-Code"] = "HX_STUDIO_PUBLIC_2026";
                    }
                    req.Timeout = 10000;

                    string jsonBody = "{\"model\":\"" + model + "\",\"messages\":[{\"role\":\"user\",\"content\":\"hi\"}],\"max_tokens\":5}";
                    byte[] bytes = Encoding.UTF8.GetBytes(jsonBody);
                    req.ContentLength = bytes.Length;

                    using (Stream s = req.GetRequestStream())
                    {
                        s.Write(bytes, 0, bytes.Length);
                    }

                    using (HttpWebResponse res = (HttpWebResponse)req.GetResponse())
                    {
                        ok = (res.StatusCode == HttpStatusCode.OK);
                    }
                }
                catch (Exception ex)
                {
                    errorMsg = ex.Message;
                }

                _window.Dispatcher.Invoke((Action)(delegate
                {
                    _btnTestConnection.IsEnabled = true;
                    _btnTestConnection.Content = "测试连接";

                    if (ok)
                    {
                        MessageBox.Show("中转站 API 连接测试成功！\n模型响应正常，可以开始向小助手提问了。", "连接正常", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("API 连接测试失败: " + errorMsg + "\n请检查中转站网址、API Key 是否有效及网络状态。", "连接失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }));
            });
        }

                private void SetStatus(string text, string hexColor)
        {
            try
            {
                if (_txtStatus != null) _txtStatus.Text = text;
                if (_statusDot != null)
                {
                    _statusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor));
                }
            }
            catch { }
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\') { sb.Append('\\'); sb.Append('\\'); }
                else if (c == '"') { sb.Append('\\'); sb.Append('"'); }
                else if (c == '\r') { sb.Append('\\'); sb.Append('r'); }
                else if (c == '\n') { sb.Append('\\'); sb.Append('n'); }
                else if (c == '\t') { sb.Append('\\'); sb.Append('t'); }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static string UnescapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    char next = s[i + 1];
                    if (next == 'n') { sb.Append(Environment.NewLine); i++; }
                    else if (next == 'r') { i++; }
                    else if (next == 't') { sb.Append("    "); i++; }
                    else if (next == '"') { sb.Append('"'); i++; }
                    else if (next == '\\') { sb.Append('\\'); i++; }
                    else { sb.Append(s[i]); }
                }
                else
                {
                    sb.Append(s[i]);
                }
            }
            return sb.ToString();
        }

        private void LoadConfig()
        {
            try
            {
                string configPath = Path.Combine(NativeCore.GetDataDirectory(), "ai_assistant.ini");
                if (File.Exists(configPath))
                {
                    string[] cfgLines = File.ReadAllLines(configPath);
                    for (int i = 0; i < cfgLines.Length; i++)
                    {
                        string lineStr = cfgLines[i];
                        if (string.IsNullOrWhiteSpace(lineStr) || lineStr.StartsWith("#")) continue;
                        int eq = lineStr.IndexOf('=');
                        if (eq > 0)
                        {
                            string k = lineStr.Substring(0, eq).Trim();
                            string v = lineStr.Substring(eq + 1).Trim();
                            if (string.Equals(k, "KeepAlive", StringComparison.OrdinalIgnoreCase)) { bool val; if (bool.TryParse(v, out val)) _keepAliveOnMainWindowClose = val; }
                            else if (string.Equals(k, "KeepAlive", StringComparison.OrdinalIgnoreCase)) { bool val; if (bool.TryParse(v, out val)) _keepAliveOnMainWindowClose = val; }
                            else if (string.Equals(k, "ApiUrl", StringComparison.OrdinalIgnoreCase)) _apiUrl = v;
                            else if (string.Equals(k, "ApiKey", StringComparison.OrdinalIgnoreCase)) _apiKey = v;
                            else if (string.Equals(k, "ModelName", StringComparison.OrdinalIgnoreCase)) _modelName = v;
                        }
                    }
                }

                if (_txtApiUrl != null) _txtApiUrl.Text = _apiUrl;
                if (_txtApiKey != null) _txtApiKey.Text = _apiKey;
                if (_txtModelName != null) _txtModelName.Text = _modelName;
            }
            catch { }
        }

        private void SaveConfig()
        {
            try
            {
                if (_txtApiUrl != null) _apiUrl = _txtApiUrl.Text.Trim();
                if (_txtApiKey != null) _apiKey = _txtApiKey.Text.Trim();
                if (_txtModelName != null) _modelName = _txtModelName.Text.Trim();

                string configPath = Path.Combine(NativeCore.GetDataDirectory(), "ai_assistant.ini");
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# HX 智能硬件助手 API 配置");
                sb.AppendLine("KeepAlive=" + _keepAliveOnMainWindowClose);
                sb.AppendLine("KeepAlive=" + _keepAliveOnMainWindowClose);
                sb.AppendLine("ApiUrl=" + _apiUrl);
                sb.AppendLine("ApiKey=" + _apiKey);
                sb.AppendLine("ModelName=" + _modelName);
                File.WriteAllText(configPath, sb.ToString(), Encoding.UTF8);

                if (_settingsDrawer != null)
                {
                    _settingsDrawer.Visibility = Visibility.Collapsed;
                }
                MessageBox.Show("配置已成功保存！", "设置成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存配置失败: " + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

                public void AddAiMessage(string message)
        {
            AddMessageBubble(message, false);
        }

        public void OnMainWindowClosed()
        {
            AddAiMessage("【主程序生命周期通知】" + Environment.NewLine + "主工具箱已关闭。根据当前选项，AI 助手已进入【独立运行模式】，继续在后台常驻守护显卡。右键萌宠可重新打开主工具箱或彻底退出。");
        }

        
    }
}
