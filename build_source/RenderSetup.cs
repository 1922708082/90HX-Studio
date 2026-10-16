using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RenderSetup
{
    public class Program
    {
        [STAThread]
        public static void Main()
        {
            string xamlPath = @"D:\app\build_source\src\SetupWindow.xaml";
            string content = File.ReadAllText(xamlPath, Encoding.UTF8);

            var win = (Window)XamlReader.Parse(content);
            var root = (FrameworkElement)win.Content;

            int width = 580;
            int height = 410;

            root.Width = width;
            root.Height = height;
            root.Measure(new Size(width, height));
            root.Arrange(new Rect(0, 0, width, height));
            root.UpdateLayout();

            var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(root);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            string outPng = @"D:\app\build_source\render_installer_wpf.png";
            using (var fsOut = File.Create(outPng))
            {
                encoder.Save(fsOut);
            }
            Console.WriteLine("Rendered WPF installer to " + outPng);
        }
    }
}
