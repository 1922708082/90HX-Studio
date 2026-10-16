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

                var window = (Window)XamlReader.Parse(content);
                var root = (FrameworkElement)window.Content;

                // 顶部状态徽章
                var txtTopGpu = (TextBlock)window.FindName("TxtTopGpuModel");
                if (txtTopGpu != null) txtTopGpu.Text = "NVIDIA CMP 90HX";

                var txtTopPcie = (TextBlock)window.FindName("TxtTopPcieStatus");
                if (txtTopPcie != null) txtTopPcie.Text = "PCIe Gen2 x16 (全速)";

                var txtTopOverlay = (TextBlock)window.FindName("TxtTopOverlayStatus");
                if (txtTopOverlay != null) txtTopOverlay.Text = "免白名单就绪";

                var txtTopSens = (TextBlock)window.FindName("TxtTopSensors");
                if (txtTopSens != null) txtTopSens.Text = "48 ℃ · 82.9 W";

                // 解锁矩阵页数据
                var lstMining = (ListBox)window.FindName("LstMiningGpus");
                if (lstMining != null)
                {
                    lstMining.Items.Add("[插槽 1] NVIDIA CMP 90HX (GA102 · 10GB GDDR6X) ➔ NVIDIA GeForce RTX 3080 [双轨完全就绪]");
                    lstMining.SelectedIndex = 0;
                }

                var txtMiningSum = (TextBlock)window.FindName("TxtMiningGpuSummary");
                if (txtMiningSum != null) txtMiningSum.Text = "共识别 1 张 CMP 90HX (GA102)";

                // 选中通道 D (U盘直启)，让 U 盘选择器显示
                var rD = (RadioButton)window.FindName("RadioChannelD");
                if (rD != null) rD.IsChecked = true;

                var cmb = (ComboBox)window.FindName("CmbUsbDrives");
                if (cmb != null)
                {
                    cmb.Items.Add("E: (KINGSTON - FAT32)");
                    cmb.SelectedIndex = 0;
                }

                // 日志
                var log = (TextBox)window.FindName("TxtLogBox");
                if (log != null)
                {
                    log.Text =
                        "[22:15:01] 90HX Studio 已就绪。\r\n" +
                        "[22:15:02] 检测到 NVIDIA CMP 90HX (10de:220d)，PCIe Gen2 x16。\r\n" +
                        "[22:15:03] 已识别可移动 U 盘: E: (KINGSTON - FAT32)。\r\n" +
                        "[22:15:05] 可点击【让 U 盘优先启动】一键配置开机优先解锁。\r\n";
                }

                // 切到解锁矩阵页 (索引 3)
                var tabs = (TabControl)window.FindName("MainTabControl");
                if (tabs != null) tabs.SelectedIndex = 3;

                int width = 1080;
                int height = 760;

                root.Width = width;
                root.Height = height;
                root.Measure(new Size(width, height));
                root.Arrange(new Rect(0, 0, width, height));
                root.UpdateLayout();

                var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(root);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                string outPng = @"D:\app\build_source\render_unlock_tab.png";
                using (var fsOut = File.Create(outPng))
                {
                    encoder.Save(fsOut);
                }
                Console.WriteLine("[OK] 解锁矩阵页渲染成功: " + outPng);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Render error: " + ex.ToString());
            }
        }
    }
}
