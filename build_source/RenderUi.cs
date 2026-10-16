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
                string outPng = @"D:\app\build_source\rendered_ui.png";

                string content = File.ReadAllText(xamlPath, Encoding.UTF8);
                var window = (Window)XamlReader.Parse(content);

                var root = (UIElement)window.Content;

                // 注入测试数据
                var hGpu = (TextBlock)window.FindName("TxtHeaderGpuName");
                if (hGpu != null) hGpu.Text = "NVIDIA GeForce RTX 3080";

                var hId = (TextBlock)window.FindName("TxtHeaderGpuId");
                if (hId != null) hId.Text = @"PCI\VEN_10DE&DEV_220D";

                var hOver = (TextBlock)window.FindName("TxtHeaderOverlay");
                if (hOver != null) hOver.Text = "已解锁 (Alt+R 可用)";

                var hPcie = (TextBlock)window.FindName("TxtHeaderPcie");
                if (hPcie != null) hPcie.Text = "Gen2 x16 就绪";

                int width = 860;
                int height = 620;

                root.Measure(new Size(width, height));
                root.Arrange(new Rect(0, 0, width, height));
                root.UpdateLayout();

                var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(root);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                using (var fs = File.Create(outPng))
                {
                    encoder.Save(fs);
                }

                Console.WriteLine("Successfully rendered UI to " + outPng + " (" + new FileInfo(outPng).Length + " bytes)");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Render error: " + ex.ToString());
            }
        }
    }
}
