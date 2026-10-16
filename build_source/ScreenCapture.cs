using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace ScreenCapture
{
    public class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                int w = Screen.PrimaryScreen.Bounds.Width;
                int h = Screen.PrimaryScreen.Bounds.Height;
                using (Bitmap bmp = new Bitmap(w, h))
                {
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(0, 0, 0, 0, new Size(w, h));
                    }
                    string outPath = @"D:\app\build_source\screen_tuba.png";
                    bmp.Save(outPath, ImageFormat.Png);
                    Console.WriteLine("[OK] 屏幕截图成功: " + outPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Capture error: " + ex.Message);
            }
        }
    }
}
