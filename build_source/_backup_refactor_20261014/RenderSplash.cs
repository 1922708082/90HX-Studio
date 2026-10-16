using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RenderSplash
{
    public class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                string xamlPath = @"D:\app\build_source\src\SplashWindow.xaml";
                string content = File.ReadAllText(xamlPath, Encoding.UTF8);

                var window = (Window)XamlReader.Parse(content);
                var root = (FrameworkElement)window.Content;

                int width = 320;
                int height = 320;

                root.Width = width;
                root.Height = height;
                root.Measure(new Size(width, height));
                root.Arrange(new Rect(0, 0, width, height));
                root.UpdateLayout();

                var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(root);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                string outPng = @"D:\app\build_source\render_splash_preview.png";
                using (var fsOut = File.Create(outPng))
                {
                    encoder.Save(fsOut);
                }
                Console.WriteLine("[OK] 还原后的黑色图标启动动画预览图渲染成功: " + outPng);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Render error: " + ex.ToString());
            }
        }
    }
}
