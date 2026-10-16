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
                var root = (UIElement)window.Content;

                var tc = (TabControl)window.FindName("MainTabControl");
                if (tc != null)
                {
                    tc.SelectedIndex = 3; // 选中第 3 个 Tab: 全系解锁矩阵
                }

                var txtSummary = (TextBlock)window.FindName("TxtMiningGpuSummary");
                if (txtSummary != null)
                {
                    txtSummary.Text = "CMP 90HX + CMP 40HX (共 2 张矿卡混合已就绪)";
                }

                int width = 1080;
                int height = 760;

                root.Measure(new Size(width, height));
                root.Arrange(new Rect(0, 0, width, height));
                root.UpdateLayout();

                var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(root);

                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(rtb));
                string outPath = @"D:\app\build_source\render_mining_matrix.png";
                using (var fs = File.Create(outPath))
                {
                    enc.Save(fs);
                }
                Console.WriteLine("[OK] Render saved to " + outPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERR] " + ex.ToString());
            }
        }
    }
}
