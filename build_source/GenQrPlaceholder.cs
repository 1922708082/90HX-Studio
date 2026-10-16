using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace GenerateQrPlaceholder
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreatePlaceholder(@"D:\app\build_source\assets\wechat_pay.png", "微信赞赏码", Color.FromArgb(0x07, 0xC1, 0x60));
            CreatePlaceholder(@"D:\app\build_source\assets\alipay.png", "支付宝赞助码", Color.FromArgb(0x16, 0x77, 0xFF));
            Console.WriteLine("[OK] 赞赏码占位图生成完毕");
        }

        private static void CreatePlaceholder(string path, string title, Color brandColor)
        {
            int w = 240;
            int h = 240;
            using (Bitmap bmp = new Bitmap(w, h))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.Clear(Color.FromArgb(248, 249, 250));

                // 虚线边框
                using (Pen pen = new Pen(brandColor, 2))
                {
                    pen.DashStyle = DashStyle.Dash;
                    g.DrawRectangle(pen, 12, 12, w - 24, h - 24);
                }

                // 品牌色圆形底色
                using (Brush b = new SolidBrush(brandColor))
                {
                    g.FillEllipse(b, w / 2 - 28, 48, 56, 56);
                }

                // 中心二维码图标
                using (Font fontIco = new Font("Segoe UI Symbol", 22, FontStyle.Bold))
                using (Brush bWhite = new SolidBrush(Color.White))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    g.DrawString("❖", fontIco, bWhite, new RectangleF(w / 2 - 28, 48, 56, 56), sf);
                }

                // 标题文字
                using (Font font = new Font("Microsoft YaHei", 12, FontStyle.Bold))
                using (Brush bText = new SolidBrush(Color.FromArgb(29, 29, 31)))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center };
                    g.DrawString(title, font, bText, new PointF(w / 2, 126), sf);
                }

                // 提示文字
                using (Font fontSub = new Font("Microsoft YaHei", 9.5f))
                using (Brush bSub = new SolidBrush(Color.FromArgb(134, 134, 139)))
                {
                    StringFormat sf = new StringFormat { Alignment = StringAlignment.Center };
                    g.DrawString("待放入收款二维码图片", fontSub, bSub, new PointF(w / 2, 156), sf);
                    g.DrawString("(直接替换此文件即可)", fontSub, bSub, new PointF(w / 2, 176), sf);
                }

                bmp.Save(path, ImageFormat.Png);
            }
        }
    }
}
