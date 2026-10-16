using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RenderUi
{
    public class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                string xamlPath = @"D:\app\build_source\src\MainWindow.xaml";
                string content = File.ReadAllText(xamlPath, Encoding.UTF8);

                string[] tabNames = new string[] {
                    "tab0_gpu",
                    "tab1_overlay",
                    "tab2_pcie",
                    "tab3_dlss",
                    "tab4_tools",
                    "tab5_logs"
                };

                for (int i = 0; i < 6; i++)
                {
                    var window = (Window)XamlReader.Parse(content);
                    var root = (UIElement)window.Content;

                    var hGpu = (TextBlock)window.FindName("TxtHeaderGpuName");
                    if (hGpu != null) hGpu.Text = "NVIDIA GeForce RTX 3080";

                    var hId = (TextBlock)window.FindName("TxtHeaderGpuId");
                    if (hId != null) hId.Text = @"PCI\VEN_10DE&DEV_220D";

                    var hOver = (TextBlock)window.FindName("TxtHeaderOverlay");
                    if (hOver != null) hOver.Text = "已解锁 (Alt+R 可用)";

                    var hPcie = (TextBlock)window.FindName("TxtHeaderPcie");
                    if (hPcie != null) hPcie.Text = "Gen2 x16 就绪";

                    var tMatchId = (TextBlock)window.FindName("TxtGpuMatchId");
                    if (tMatchId != null) tMatchId.Text = @"PCI\VEN_10DE&DEV_220D";

                    var dlssDir = (TextBox)window.FindName("TxtDlssGameDir");
                    if (dlssDir != null) dlssDir.Text = @"D:\Games\BlackMythWukong\b1\Binaries\Win64";

                    var dlssStatus = (TextBlock)window.FindName("TxtDlssStatusInDir");
                    if (dlssStatus != null) dlssStatus.Text = "已就绪 (可注入 version.dll 与 dlssg_sm86.ini)";

                    var logs = (TextBox)window.FindName("TxtLogConsole");
                    if (logs != null)
                    {
                        logs.Text = "[21:00:12] [√] 硬件检测完成: 识别为 NVIDIA GeForce RTX 3080\r\n" +
                                    "[21:00:13] [√] PCIe 速率状态: Gen2 x16 开机解锁已就绪 (NVPermissiveCompat)\r\n" +
                                    "[21:00:14] [√] NVIDIA App 状态: main.1875e13aec4671dc.js 补丁已应用\r\n" +
                                    "[21:00:15] [√] 核心加密容器: AES-256 安全载荷校验通过\r\n" +
                                    "[21:00:16] [√] 系统驱动类与设备管理器名称一致\r\n";
                    }

                    var tabs = (TabControl)window.FindName("MainTabControl");
                    if (tabs != null)
                    {
                        tabs.SelectedIndex = i;
                    }

                    int width = 880;
                    int height = 640;

                    root.Measure(new Size(width, height));
                    root.Arrange(new Rect(0, 0, width, height));
                    root.UpdateLayout();

                    var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                    rtb.Render(root);

                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(rtb));

                    string outPng = @"D:\app\build_source\render_" + tabNames[i] + ".png";
                    using (var fs = File.Create(outPng))
                    {
                        encoder.Save(fs);
                    }
                    Console.WriteLine("Rendered: " + outPng);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Render error: " + ex.ToString());
            }
        }
    }
}
