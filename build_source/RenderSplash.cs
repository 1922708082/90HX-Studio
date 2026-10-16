using System;
using System.IO;
using System.Text;
using System.Windows;
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

                int W = 360, H = 360;
                root.Width = W; root.Height = H;
                root.Measure(new Size(W, H));
                root.Arrange(new Rect(0, 0, W, H));
                root.UpdateLayout();

                // 四帧预览：分别对应动画的四个阶段（手动设定该时刻的视觉状态）
                // 阶段1：弹性涌现中 (t≈0.55s)
                SetState(window, 0.82, 0.55, 0.30, 0.90, 0, 0.90, 0.20, 0);
                Save(root, W, H, @"D:\app\build_source\splash_frame_entrance.png", "t=0.55s 弹性涌现中");

                // 阶段2：落定 + 光环外扩 (t≈1.20s)
                SetState(window, 1.00, 1.00, 0.85, 1.00, 0.45, 1.22, 0.55, 0);
                Save(root, W, H, @"D:\app\build_source\splash_frame_settle.png", "t=1.20s 落定光环外扩");

                // 阶段3：双层流光扫掠 (t≈2.10s)
                SetState(window, 1.00, 1.00, 0.85, 1.02, 0, 1.30, 0.60, 0.30);
                Save(root, W, H, @"D:\app\build_source\splash_frame_shine.png", "t=2.10s 双层流光扫掠");

                // 阶段4：常态极光呼吸 (t≈3.20s)
                SetState(window, 1.018, 1.00, 0.85, 1.05, 0, 1.42, 0, 0);
                Save(root, W, H, @"D:\app\build_source\render_splash_preview.png", "t=3.20s 常态极光呼吸");

                Console.WriteLine("[OK] 启动动画四帧预览渲染完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Render error: " + ex.ToString());
            }
        }

        // iconScale: 图标弹性缩放 | iconOpacity: 图标淡入 | auraOpacity: 光晕强度 | auraScale: 光晕缩放
        // ringOpacity: 落定环强度 | ringScale: 落定环外扩 | shine: 主流光 | shine2: 副流光
        private static void SetState(Window window, double iconScale, double iconOpacity,
            double auraOpacity, double auraScale, double ringOpacity, double ringScale,
            double shine, double shine2)
        {
            var sc = window.FindName("IconScale") as ScaleTransform;
            if (sc != null) { sc.ScaleX = iconScale; sc.ScaleY = iconScale; }

            var ico = window.FindName("IconContainer") as FrameworkElement;
            if (ico != null) ico.Opacity = iconOpacity;

            var aura = window.FindName("IconAura") as FrameworkElement;
            if (aura != null) aura.Opacity = auraOpacity;
            var asc = window.FindName("AuraScale") as ScaleTransform;
            if (asc != null) { asc.ScaleX = auraScale; asc.ScaleY = auraScale; }

            var ring = window.FindName("SettleRing") as FrameworkElement;
            if (ring != null) ring.Opacity = ringOpacity;
            var rsc = window.FindName("RingScale") as ScaleTransform;
            if (rsc != null) { rsc.ScaleX = ringScale; rsc.ScaleY = ringScale; }

            var sb1 = window.FindName("ShineBar") as FrameworkElement;
            if (sb1 != null) sb1.Opacity = shine;
            var sb2 = window.FindName("ShineBar2") as FrameworkElement;
            if (sb2 != null) sb2.Opacity = shine2;
        }

        private static void Save(FrameworkElement root, int W, int H, string outPng, string label)
        {
            root.UpdateLayout();
            var rtb = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(root);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            using (var fsOut = File.Create(outPng))
            {
                encoder.Save(fsOut);
            }
            Console.WriteLine("[OK] " + Path.GetFileName(outPng) + "  " + label);
        }
    }
}
