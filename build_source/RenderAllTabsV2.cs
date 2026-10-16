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
        private static readonly string[] PageNames = new string[] {
            "PageHardware", "PageSpoof", "PageOverlay", "PageMatrix",
            "PageDualGpu", "PageDlss", "PagePower", "PageLogs", "PageSponsor"
        };
        private static readonly string[] NavNames = new string[] {
            "NavItemHardware", "NavItemSpoof", "NavItemOverlay", "NavItemMatrix",
            "NavItemDualGpu", "NavItemDlss", "NavItemPower", "NavItemLogs", "NavItemSponsor"
        };

        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                string xamlPath = @"D:\app\build_source\src\MainWindow.xaml";
                string content = File.ReadAllText(xamlPath, Encoding.UTF8);

                var window = (Window)XamlReader.Parse(content);
                var root = (FrameworkElement)window.Content;

                // ---------- 填充演示数据 ----------
                var txtTopGpu = (TextBlock)window.FindName("TxtTopGpuModel");
                if (txtTopGpu != null) txtTopGpu.Text = "NVIDIA CMP 90HX";
                var txtTopPcie = (TextBlock)window.FindName("TxtTopPcieStatus");
                if (txtTopPcie != null) txtTopPcie.Text = "PCIe Gen2 x16 (全速)";
                var txtTopOverlay = (TextBlock)window.FindName("TxtTopOverlayStatus");
                if (txtTopOverlay != null) txtTopOverlay.Text = "免白名单就绪";
                var txtTopSens = (TextBlock)window.FindName("TxtTopSensors");
                if (txtTopSens != null) txtTopSens.Text = "48 ℃ · 82.9 W";

                // 底部状态栏
                var barCpu = (TextBlock)window.FindName("TxtBarCpu");
                if (barCpu != null) barCpu.Text = "Intel Core i5-12490F";
                var barGpu = (TextBlock)window.FindName("TxtBarGpu");
                if (barGpu != null) barGpu.Text = "NVIDIA CMP 90HX";
                var barRam = (TextBlock)window.FindName("TxtBarRam");
                if (barRam != null) barRam.Text = "32.0 GB DDR4";
                var barOs = (TextBlock)window.FindName("TxtBarOs");
                if (barOs != null) barOs.Text = "Windows 11 专业版 64位";
                var barClock = (TextBlock)window.FindName("TxtBarClock");
                if (barClock != null) barClock.Text = DateTime.Now.ToString("HH:mm:ss");

                // 解锁矩阵页数据
                var lstMining = (ListBox)window.FindName("LstMiningGpus");
                if (lstMining != null)
                {
                    lstMining.Items.Add("[插槽 1] NVIDIA CMP 90HX (GA102 · 10GB GDDR6X) ➔ NVIDIA GeForce RTX 3080 [双轨完全就绪]");
                    lstMining.SelectedIndex = 0;
                }
                var txtMiningSum = (TextBlock)window.FindName("TxtMiningGpuSummary");
                if (txtMiningSum != null) txtMiningSum.Text = "共识别 1 张 CMP 90HX (GA102)";

                var rD = (RadioButton)window.FindName("RadioChannelD");
                if (rD != null) rD.IsChecked = true;

                var cmb = (ComboBox)window.FindName("CmbUsbDrives");
                if (cmb != null)
                {
                    cmb.Items.Add("E: (KINGSTON - FAT32)");
                    cmb.SelectedIndex = 0;
                }

                var log = (TextBox)window.FindName("TxtLogBox");
                if (log != null)
                {
                    log.Text =
                        "[22:15:01] 90HX Studio 已就绪（100% 开源 · 无加密）。\r\n" +
                        "[22:15:02] 检测到 NVIDIA CMP 90HX (10de:220d)，PCIe Gen2 x16。\r\n" +
                        "[22:15:03] 已识别可移动 U 盘: E: (KINGSTON - FAT32)。\r\n" +
                        "[22:15:05] 资源包 assets.zip 明文内嵌，可随时解包查验。\r\n";
                }

                int width = 1180;
                int height = 780;

                // ---------- 渲染：整窗默认页（硬件信息） ----------
                RenderPage(window, root, -1, width, height, @"D:\app\build_source\render_main.png");

                // ---------- 渲染：全系解锁矩阵 ----------
                RenderPage(window, root, 3, width, height, @"D:\app\build_source\render_matrix.png");

                // ---------- 渲染：显卡型号伪装 ----------
                RenderPage(window, root, 1, width, height, @"D:\app\build_source\render_spoof.png");

                // ---------- 渲染：审计日志 ----------
                RenderPage(window, root, 7, width, height, @"D:\app\build_source\render_logs.png");

                Console.WriteLine("[OK] 全部页面渲染完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Render error: " + ex.ToString());
            }
        }

        private static void RenderPage(Window window, FrameworkElement root, int pageIndex, int width, int height, string outPng)
        {
            for (int i = 0; i < PageNames.Length; i++)
            {
                var page = (FrameworkElement)window.FindName(PageNames[i]);
                if (page == null) continue;
                page.Visibility = (pageIndex < 0 ? i == 0 : i == pageIndex) ? Visibility.Visible : Visibility.Collapsed;
            }
            for (int i = 0; i < NavNames.Length; i++)
            {
                var nav = (RadioButton)window.FindName(NavNames[i]);
                if (nav == null) continue;
                nav.IsChecked = (pageIndex < 0 ? i == 0 : i == pageIndex);
            }

            root.Width = width;
            root.Height = height;
            root.Measure(new Size(width, height));
            root.Arrange(new Rect(0, 0, width, height));
            root.UpdateLayout();

            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(root);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using (var fsOut = File.Create(outPng))
            {
                encoder.Save(fsOut);
            }
            Console.WriteLine("[OK] " + Path.GetFileName(outPng) + "  (page=" + (pageIndex < 0 ? "default/硬件信息" : PageNames[pageIndex]) + ")");
        }
    }
}
