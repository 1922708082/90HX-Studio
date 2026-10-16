using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace CMP90HXInstaller
{
    public class RenderInstaller
    {
        [STAThread]
        public static void Main()
        {
            var form = new SetupForm();
            form.ShowInTaskbar = false;
            form.CreateControl();

            // Set size and layout
            var bmp = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));

            string outPath = @"D:\app\build_source\render_installer.png";
            bmp.Save(outPath, ImageFormat.Png);
            Console.WriteLine("Rendered installer to " + outPath);
        }
    }
}
