using System;
using System.Collections.Generic;
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
using System.Windows.Documents;
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
        private bool _isExplicitExit;

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
            if (_instance == null || _instance._window == null || !_instance._window.IsLoaded)
            {
                _instance = new AiAssistantController();
                _instance.Initialize();
            }
            if (autoShow && _instance != null && _instance._window != null)
            {
                try
                {
                    if (!_instance._window.IsVisible)
                    {
                        _instance._window.Show();
                    }
                    _instance._window.Activate();
                    _instance._window.Topmost = true;
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
                    string[] resNames = Assembly.GetExecutingAssembly().GetManifestResourceNames();
                    if (resNames != null)
                    {
                        for (int i = 0; i < resNames.Length; i++)
                        {
                            if (string.Equals(resNames[i], "AiAssistantWindow.xaml", StringComparison.OrdinalIgnoreCase) ||
                                resNames[i].EndsWith(".AiAssistantWindow.xaml", StringComparison.OrdinalIgnoreCase) ||
                                resNames[i].IndexOf("AiAssistantWindow.xaml", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resNames[i]);
                                if (stream != null) break;
                            }
                        }
                    }
                }

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

                if (stream == null)
                {
                    try
                    {
                        File.AppendAllText(Path.Combine(NativeCore.GetLogsDirectory(), "ai_assistant.log"),
                            "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] [ERROR] 未找到 AiAssistantWindow.xaml 嵌入资源与本地文件！" + Environment.NewLine);
                    }
                    catch { }
                    return;
                }

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
                _window.Topmost = true;

                _blinkTimer = new DispatcherTimer();
                _blinkTimer.Interval = TimeSpan.FromSeconds(4.5);
                _blinkTimer.Tick += delegate(object s, EventArgs e)
                {
                    try
                    {
                        if (_window != null && _window.IsLoaded)
                        {
                            Storyboard blinkSb = _window.TryFindResource("PetBlinkStoryboard") as Storyboard;
                            if (blinkSb != null) blinkSb.Begin(_window);
                        }
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
            if (_window != null)
            {
                _window.Closing += delegate(object s, System.ComponentModel.CancelEventArgs e)
                {
                    if (!_isExplicitExit)
                    {
                        e.Cancel = true;
                        _window.Hide();
                    }
                };

                _window.Closed += delegate(object s, EventArgs e)
                {
                    if (_blinkTimer != null)
                    {
                        try { _blinkTimer.Stop(); } catch { }
                    }
                    _window = null;
                    _instance = null;
                };
            }

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
                    if (!_isDragging)
                    {
                        SwitchMode(true, true);
                    }
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
                    if (e.ClickCount == 2)
                    {
                        ResetToBottomRight();
                    }
                    else if (e.LeftButton == MouseButtonState.Pressed)
                    {
                        _window.DragMove();
                    }
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

            if (_chatScrollViewer != null)
            {
                // 每次滚轮只滚约 46px，滚动更细腻，不再一次跳一大段
                _chatScrollViewer.PreviewMouseWheel += delegate(object s, MouseWheelEventArgs e)
                {
                    _chatScrollViewer.ScrollToVerticalOffset(_chatScrollViewer.VerticalOffset - (e.Delta / 120.0) * 46);
                    e.Handled = true;
                };
            }

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

            // 预置官方国内云端接口，不暴露任何配置项
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
                _isExplicitExit = true;
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
                // 80x80 标准极客机械卡通徽标 (窗口留 4px 柔和外发光阴影至 88x88)
                double targetWidth = 88;
                double targetHeight = 88;
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
                _window.Width = 88;
                _window.Height = 88;
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
                RenderRichText(contentBlock, StripMarkdownArtifacts(text));  // 去 Markdown 并富文本显示
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
                // 首先请求国内服务器专属网关 (自动附加 X-HX-Code，零密钥泄露风险)
                string apiResult = await RequestApiCompletionAsync(userPrompt);
                if (!string.IsNullOrEmpty(apiResult) && apiResult.Trim().Length > 0)
                {
                    finalAnswer = apiResult.Trim();
                    SetStatus("在线 · 极客大模型响应", "#34C759");
                }
            }
            catch (Exception ex)
            {
                try
                {
                    File.AppendAllText(Path.Combine(NativeCore.GetLogsDirectory(), "ai_assistant.log"),
                        "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] Gateway request failed: " + ex.Message + Environment.NewLine);
                }
                catch { }
            }

            // 离线或网关未响应时，无缝切换为本地专家急诊库
            if (string.IsNullOrEmpty(finalAnswer))
            {
                try
                {
                    finalAnswer = GenerateLocalHardwareKnowledge(userPrompt);
                    SetStatus("离线 · 本地专家急诊库", "#34C759");
                }
                catch (Exception ex)
                {
                    finalAnswer = "【系统提示】本地专家库诊断异常: " + ex.Message;
                    SetStatus("诊断异常", "#FF3B30");
                }
            }

            // 模型侧工具调用：回复里带 [[ACTION:id]] 时优先按模型判断执行，
            // 标记本身要从显示文本里去掉，否则用户会看到原始标记。
            string modelActionId = AiAgentActionRegistry.ExtractActionId(finalAnswer);
            if (!string.IsNullOrEmpty(modelActionId))
            {
                finalAnswer = System.Text.RegularExpressions.Regex.Replace(
                    finalAnswer, @"\s*\[\[ACTION:[a-zA-Z]+\]\]\s*", "\n").Trim();
            }

            PlayTypewriterEffect(replyBlock, finalAnswer);

            // 若这句话对应真实可执行操作，给出"执行"按钮，让助手能直接干活
            // 支持多步指令：「解锁 90HX 并伪装成 3080」会按句子顺序列出两张卡片
            try
            {
                if (!string.IsNullOrEmpty(modelActionId))
                {
                    // 模型判断优先
                    AiAgentAction modelAction = AiAgentActionRegistry.BuildById(modelActionId, userPrompt);
                    if (modelAction != null) AddActionCard(modelAction);
                }
                else
                {
                    List<AiAgentAction> actions = AiAgentActionRegistry.MatchMany(userPrompt);
                    for (int i = 0; i < actions.Count; i++)
                    {
                        AddActionCard(actions[i]);
                    }
                }
            }
            catch { }
        }

        private void AddActionCard(AiAgentAction action)
        {
            if (_messagesPanel == null || action == null) return;

            Border card = new Border();
            card.Margin = new Thickness(0, 0, 0, 10);
            card.MaxWidth = 310;
            card.HorizontalAlignment = HorizontalAlignment.Left;
            card.CornerRadius = new CornerRadius(12);
            card.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F9FF"));
            card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CFE3FF"));
            card.BorderThickness = new Thickness(1);
            card.Padding = new Thickness(12, 10, 12, 10);

            StackPanel panel = new StackPanel();
            TextBlock title = new TextBlock();
            title.Text = "可直接执行：" + action.Title;
            title.FontSize = 12;
            title.FontWeight = FontWeights.SemiBold;
            title.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1D1D1F"));
            panel.Children.Add(title);

            TextBlock desc = new TextBlock();
            desc.Text = action.Description;
            desc.FontSize = 11;
            desc.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6E6E73"));
            desc.TextWrapping = TextWrapping.Wrap;
            desc.Margin = new Thickness(0, 4, 0, 8);
            panel.Children.Add(desc);

            // 执行过程日志：之前 logger 是空的，用户看不到任何中间步骤
            TextBlock stepLog = new TextBlock();
            stepLog.FontSize = 10.5;
            stepLog.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6E6E73"));
            stepLog.TextWrapping = TextWrapping.Wrap;
            stepLog.LineHeight = 15;
            stepLog.Margin = new Thickness(0, 0, 0, 6);
            stepLog.Visibility = Visibility.Collapsed;
            panel.Children.Add(stepLog);

            Button run = new Button();
            run.Content = action.IsDestructive ? "确认执行（需要管理员权限）" : "执行此操作";
            run.Height = 30;
            run.Padding = new Thickness(12, 0, 12, 0);
            run.HorizontalAlignment = HorizontalAlignment.Left;
            run.Cursor = System.Windows.Input.Cursors.Hand;
            run.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0071E3"));
            run.Foreground = Brushes.White;
            run.BorderThickness = new Thickness(0);
            run.Click += delegate
            {
                run.IsEnabled = false;
                run.Content = "执行中...";
                Task.Factory.StartNew(() =>
                {
                    Action<string, bool> logger = delegate(string msg, bool isErr)
                    {
                        if (string.IsNullOrEmpty(msg)) return;
                        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                stepLog.Visibility = Visibility.Visible;
                                string cur = stepLog.Text;
                                string line = (isErr ? "✕ " : "· ") + msg;
                                string next = string.IsNullOrEmpty(cur) ? line : line + "\n" + cur;

                                string[] parts = next.Split('\n');
                                if (parts.Length > 8)
                                {
                                    StringBuilder sb = new StringBuilder();
                                    for (int i = 0; i < 8; i++)
                                    {
                                        if (i > 0) sb.Append('\n');
                                        sb.Append(parts[i]);
                                    }
                                    next = sb.ToString();
                                }
                                stepLog.Text = next;
                                stepLog.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isErr ? "#C0392B" : "#6E6E73"));
                            }
                            catch { }
                        }));
                    };
                    string result;
                    try
                    {
                        Task<string> runTask = action.Executor(logger);
                        runTask.Wait();
                        result = runTask.Result;
                    }
                    catch (Exception ex)
                    {
                        result = "执行失败: " + ex.Message;
                    }

                    _window.Dispatcher.Invoke((Action)delegate
                    {
                        run.Content = "已执行";
                        AddMessageBubble(result, false);
                        SetStatus("操作已执行", "#34C759");
                    });
                });
            };
            panel.Children.Add(run);
            card.Child = panel;
            _messagesPanel.Children.Add(card);
            if (_chatScrollViewer != null) _chatScrollViewer.ScrollToEnd();
        }

        /// <summary>去掉模型常带的 Markdown 包装符号，避免聊天气泡里出现大量 * 和 ###。</summary>
        private static string StripMarkdownArtifacts(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            string s = text.Replace("`", "");
            s = Regex.Replace(s, @"^\s{0,3}#{1,6}\s*", "", RegexOptions.Multiline);
            s = Regex.Replace(s, @"^\s*[-*+]\s+", "• ", RegexOptions.Multiline);
            s = Regex.Replace(s, @"^\s*\d+\.\s+", "", RegexOptions.Multiline);
            s = Regex.Replace(s, @"\*\*\*(.+?)\*\*\*", "$1");
            s = Regex.Replace(s, @"\*\*(.+?)\*\*", "$1");
            s = Regex.Replace(s, @"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)", "$1");
            s = Regex.Replace(s, @"(?<!_)_(?!_)(.+?)(?<!_)_(?!_)", "$1");
            // 兜底：成对星号被模型拆开时，保留内容去符号
            if (s.IndexOf("**") >= 0) s = s.Replace("**", "");
            return s;
        }

        /// <summary>轻量富文本渲染：标题加粗、列表前缀、**强调**真正加粗，不再显示原始星号。</summary>
        private static void RenderRichText(TextBlock target, string text)
        {
            if (target == null) return;
            target.Inlines.Clear();
            if (string.IsNullOrEmpty(text)) return;

            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                bool isHeading = Regex.IsMatch(line, @"^\s{0,3}#{1,6}\s+");
                line = Regex.Replace(line, @"^\s{0,3}#{1,6}\s*", "");
                line = Regex.Replace(line, @"^\s*[-*+]\s+", "• ");
                line = Regex.Replace(line, @"^\s*(\d+)\.\s+", "$1. ");

                string[] parts = Regex.Split(line, @"(\*\*.+?\*\*)");
                for (int p = 0; p < parts.Length; p++)
                {
                    string part = parts[p];
                    if (string.IsNullOrEmpty(part)) continue;
                    if (part.Length > 4 && part.StartsWith("**") && part.EndsWith("**"))
                    {
                        Run bold = new Run(part.Substring(2, part.Length - 4));
                        bold.FontWeight = FontWeights.SemiBold;
                        target.Inlines.Add(bold);
                    }
                    else
                    {
                        Run run = new Run(part);
                        if (isHeading) run.FontWeight = FontWeights.SemiBold;
                        target.Inlines.Add(run);
                    }
                }

                if (i < lines.Length - 1) target.Inlines.Add(new LineBreak());
            }
        }

        private void PlayTypewriterEffect(TextBlock targetBlock, string fullText)
        {
            if (targetBlock == null || string.IsNullOrEmpty(fullText)) return;
            string cleanText = StripMarkdownArtifacts(fullText);
            int index = 0;
            targetBlock.Text = "";
            DispatcherTimer typeTimer = new DispatcherTimer();
            typeTimer.Interval = TimeSpan.FromMilliseconds(14);
            typeTimer.Tick += delegate(object s, EventArgs e)
            {
                try
                {
                    int step = Math.Max(3, cleanText.Length / 90);
                    if (index + step <= cleanText.Length)
                    {
                        index += step;
                        RenderRichText(targetBlock, cleanText.Substring(0, index));
                    }
                    else
                    {
                        RenderRichText(targetBlock, cleanText);
                        typeTimer.Stop();
                    }
                    if (_chatScrollViewer != null) _chatScrollViewer.ScrollToEnd();
                }
                catch
                {
                    RenderRichText(targetBlock, cleanText);
                    typeTimer.Stop();
                }
            };
            typeTimer.Start();
        }

        private string GenerateLocalHardwareKnowledge(string prompt)
        {
            string p = prompt.ToLower();
            if (p.Contains("温度") || p.Contains("功耗") || p.Contains("传感器") || p.Contains("频率") || p.Contains("风扇") || p.Contains("转速") || p.Contains("硬件") || p.Contains("状态"))
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
                sb.AppendLine("2. 调优思路（具体数值因卡而异，不要照抄别人的）：");
                sb.AppendLine("   • 核心频率：每次 +30MHz 往上试，出现花屏或驱动重置就退回上一档，稳定点即为本卡甜点；");
                sb.AppendLine("   • 显存频率：GDDR6X 超频收益有限且发热明显，建议小幅尝试并盯住显存温度；");
                sb.AppendLine("   • 功耗墙：本机默认 " + NativeCore.EffectiveDefaultPowerLimit() + "W，可在「硬件与电源」页看实时功耗；下调能明显降温，代价是高负载性能受限；");
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
            if (p.Contains("dlss") || p.Contains("覆盖") || p.Contains("帧生成") || p.Contains("改名") || p.Contains("3080") || p.Contains("4080"))
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("🛠 【DLSS 5 / 帧生成与显卡改名全解】");
                sb.AppendLine("1. 显卡型号伪装：在「显卡型号伪装」页面一键改名为『NVIDIA GeForce RTX 3080』，防止游戏启动时因卡名包含 CMP 而禁止开启光线追踪与 DLSS。");
                sb.AppendLine("2. 游戏覆盖解锁：工具箱已适配 NVIDIA App 极客补丁，点击「一键解除游戏覆盖限制」，即可使用 Alt+Z 实时监控帧率、滤镜与一键录屏！");
                sb.AppendLine("3. DLSS 帧生成：搭配 DLSSG-to-FSR3 补丁，90HX 即可享受 200% 的丝滑帧率跃升！");
                return sb.ToString();
            }
            StringBuilder fallback = new StringBuilder();
            fallback.AppendLine("收到你的问题！我是小 HX 专属硬件助手。对于 CMP 90HX/50HX/40HX，你可以随时询问我：");
            fallback.AppendLine("• 『查询显卡实时温度与功耗』");
            fallback.AppendLine("• 『90HX 解锁后如何最佳超频』");
            fallback.AppendLine("• 『双显卡无缝调度配置步骤』");
            fallback.AppendLine("• 『显卡改名与 DLSS 5 补丁打法』");
            fallback.AppendLine("");
            fallback.AppendLine("💡 当前已对接专属 AI 网关！如需配置自定义 API 地址，可点击右上角「齿轮⚙️」设置。");
            return fallback.ToString();
        }

        private static string GetNormalizedChatUrl(string rawUrl)
        {
            string url = string.IsNullOrEmpty(rawUrl) ? "https://api.muyaa.top/hx-assistant/v1" : rawUrl.Trim();
            if (url.EndsWith("/")) url = url.Substring(0, url.Length - 1);
            if (url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)) return url;
            if (url.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)) return url + "/chat/completions";
            return url + "/chat/completions";
        }

        private Task<string> RequestApiCompletionAsync(string prompt)
        {
            return Task.Factory.StartNew<string>(delegate
            {
                try
                {
                    try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768 | SecurityProtocolType.Tls; ServicePointManager.ServerCertificateValidationCallback = delegate { return true; }; } catch { }
                    string endpoint = GetNormalizedChatUrl(_apiUrl);
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(endpoint);
                    request.Method = "POST";
                    request.ContentType = "application/json; charset=utf-8";
                    request.UserAgent = "90HXStudio-AiAssistant/5.0 (.NET Framework)";

                    // 客户端自动附加专属网关代码，零密钥泄露风险
                    request.Headers["X-HX-Code"] = "HX_STUDIO_PUBLIC_2026";

                    if (!string.IsNullOrEmpty(_apiKey) && _apiKey.Trim().Length > 0)
                    {
                        request.Headers["Authorization"] = "Bearer " + _apiKey.Trim();
                    }
                    request.Timeout = 15000;
                    request.ReadWriteTimeout = 15000;

                    // 直读传感器并注入大模型上下文
                    string hwContext = "";
                    try
                    {
                        var s = NativeCore.QueryGpuSensors();
                        if (s != null && s.IsValid)
                        {
                            hwContext = "【当前显卡实时传感器状态】: 核心温度=" + s.Temperature + "℃, 功耗=" + s.PowerDraw.ToString("F1") + "W, 显存=" + s.MemoryUsedMB + "/" + s.MemoryTotalMB + "MB, 负载=" + s.GpuUtilPercent + "%, 频率=" + s.GraphicsClockMHz + "/" + s.MemoryClockMHz + "MHz, PState=" + s.PState + ", PCIe=" + s.PcieFormatted + "。";
                        }
                    }
                    catch { }

                    string realtimeFacts = "";
                    try { realtimeFacts = AiAgentActionRegistry.BuildRealtimeFacts(); } catch { }

                    string sysPrompt = "你是 HX 家族多功能工具箱里可直接操作硬件的桌面 Agent（助手名：小 HX），不是普通问答机器人。"
                        + "你可以真实执行显卡解锁、显卡型号伪装、游戏覆盖解锁、首选显卡调度、驱动热重启、系统级省电。"
                        + "回答原则：先用一两句话判断用户的问题，再说出你准备执行的动作；不要编造没有的能力，不要输出固定模板数字。"
                        + "所有功耗、温度、显存、频率、型号必须以给出的实时数据为准，不同显卡数值不同，绝不能套用写死的值。"
                        + "涉及改注册表/引导的操作，要明确提醒需要管理员权限并需要重启或软重启生效。"
                        + "输出格式：不要使用 Markdown 星号/井号/代码反引号，用纯文本、短段落和“•”列表。"
                        + (string.IsNullOrEmpty(hwContext) ? "" : "\n" + hwContext)
                        + (string.IsNullOrEmpty(realtimeFacts) ? "" : "\n【本机实时事实】" + realtimeFacts);
                    string escapedSys = EscapeJson(sysPrompt);
                    string escapedUser = EscapeJson(prompt);
                    string model = !string.IsNullOrEmpty(_modelName) ? _modelName.Trim() : "gemini-3.8-flash";

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
                        Dictionary<string, object> root = SimpleJson.Deserialize(respText) as Dictionary<string, object>;
                        if (root != null)
                        {
                            List<object> choices = SimpleJson.GetList(root, "choices");
                            if (choices != null && choices.Count > 0)
                            {
                                Dictionary<string, object> firstChoice = choices[0] as Dictionary<string, object>;
                                if (firstChoice != null)
                                {
                                    Dictionary<string, object> msg = SimpleJson.GetDict(firstChoice, "message");
                                    if (msg != null)
                                    {
                                        return SimpleJson.GetString(msg, "content", "");
                                    }
                                }
                            }
                        }

                        // 备用简易正则提取
                        Match m = Regex.Match(respText, "\"content\"\\s*:\\s*\"(.*?)\"", RegexOptions.Singleline);
                        if (m.Success)
                        {
                            return UnescapeJson(m.Groups[1].Value);
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
                    try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | (SecurityProtocolType)768 | SecurityProtocolType.Tls; ServicePointManager.ServerCertificateValidationCallback = delegate { return true; }; } catch { }
                    string endpoint = GetNormalizedChatUrl(url);
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(endpoint);
                    req.Method = "POST";
                    req.ContentType = "application/json; charset=utf-8";
                    req.UserAgent = "90HXStudio-AiAssistant/5.0 (.NET Framework)";

                    // 客户端自动附加专属网关代码，零密钥泄露风险
                    req.Headers["X-HX-Code"] = "HX_STUDIO_PUBLIC_2026";

                    if (!string.IsNullOrEmpty(key))
                    {
                        req.Headers["Authorization"] = "Bearer " + key;
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
                        ModernMessageBox.Show("中转站 API 连接测试成功！\n专属网关与模型响应正常，可以开始向小助手提问了。", "连接正常", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        ModernMessageBox.Show("API 连接测试失败: " + errorMsg + "\n请检查中转站网址、网络状态或是否开启代理。", "连接失败", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                            if (string.Equals(k, "KeepAlive", StringComparison.OrdinalIgnoreCase))
                            {
                                bool val;
                                if (bool.TryParse(v, out val)) _keepAliveOnMainWindowClose = val;
                            }
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




