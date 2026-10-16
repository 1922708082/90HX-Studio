using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

namespace IconGenerator
{
    public class Program
    {
        public static void Main(string[] args)
        {
            int[] sizes = new int[] { 256, 128, 64, 48, 32, 16 };
            Bitmap[] bitmaps = new Bitmap[sizes.Length];

            for (int i = 0; i < sizes.Length; i++)
            {
                int s = sizes[i];
                Bitmap bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                    g.Clear(Color.Transparent);

                    // 圆角矩形外边距 (根据尺寸缩放留出微小呼吸感)
                    float pad = s >= 64 ? s * 0.04f : 0;
                    float w = s - pad * 2;
                    float h = s - pad * 2;
                    float r = w * 0.25f; // 圆角弧度

                    using (GraphicsPath path = GetRoundedRect(pad, pad, w, h, r))
                    {
                        // 1. 深色微弥散阴影 (在 256/128/64 下绘制)
                        if (s >= 48)
                        {
                            using (GraphicsPath shadowPath = GetRoundedRect(pad, pad + s * 0.03f, w, h, r))
                            using (PathGradientBrush shadowBrush = new PathGradientBrush(shadowPath))
                            {
                                shadowBrush.CenterColor = Color.FromArgb(40, 0, 0, 0);
                                shadowBrush.SurroundColors = new Color[] { Color.FromArgb(0, 0, 0, 0) };
                                g.FillPath(shadowBrush, shadowPath);
                            }
                        }

                        // 2. 主体黑灰质感渐变 (135度，从 #1D1D1F 到 #3A3A3C)
                        using (LinearGradientBrush grad = new LinearGradientBrush(
                            new PointF(pad, pad),
                            new PointF(pad + w, pad + h),
                            Color.FromArgb(255, 0x1D, 0x1D, 0x1F),
                            Color.FromArgb(255, 0x3A, 0x3A, 0x3C)))
                        {
                            g.FillPath(grad, path);
                        }

                        // 3. 顶部微光边缘线 (增强高级立体感)
                        using (Pen pen = new Pen(Color.FromArgb(50, 255, 255, 255), Math.Max(1f, s * 0.015f)))
                        {
                            g.DrawPath(pen, path);
                        }

                        // 4. 白色加粗居中文字 "90"
                        string text = "90";
                        float fontSize = w * 0.50f;
                        Font font = null;
                        string[] fontFamilies = new string[] { "Segoe UI", "Arial", "Microsoft YaHei", "sans-serif" };
                        foreach (var fn in fontFamilies)
                        {
                            try
                            {
                                font = new Font(fn, fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
                                break;
                            }
                            catch { }
                        }
                        if (font == null) font = new Font(FontFamily.GenericSansSerif, fontSize, FontStyle.Bold, GraphicsUnit.Pixel);

                        using (font)
                        {
                            StringFormat sf = new StringFormat
                            {
                                Alignment = StringAlignment.Center,
                                LineAlignment = StringAlignment.Center
                            };

                            // 微微向下偏移以适应视觉居中
                            RectangleF textRect = new RectangleF(pad, pad + (s * 0.01f), w, h);
                            using (Brush textBrush = new SolidBrush(Color.FromArgb(255, 255, 255, 255)))
                            {
                                g.DrawString(text, font, textBrush, textRect, sf);
                            }
                        }
                    }
                }
                bitmaps[i] = bmp;
            }

            string outIco = @"D:\app\app.ico";
            SaveAsIcon(bitmaps, outIco);
            Console.WriteLine("[OK] 图标生成成功: " + outIco);

            // 同时输出一张高清 png 供预览
            bitmaps[0].Save(@"D:\app\build_source\icon_preview_90.png", ImageFormat.Png);
            Console.WriteLine("[OK] 预览图保存成功: D:\\app\\build_source\\icon_preview_90.png");

            foreach (var b in bitmaps) b.Dispose();
        }

        private static GraphicsPath GetRoundedRect(float x, float y, float width, float height, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(x, y, d, d, 180, 90);
            path.AddArc(x + width - d, y, d, d, 270, 90);
            path.AddArc(x + width - d, y + height - d, d, d, 0, 90);
            path.AddArc(x, y + height - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void SaveAsIcon(Bitmap[] bitmaps, string outputPath)
        {
            using (FileStream fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                // ICONDIR header
                bw.Write((short)0); // Reserved
                bw.Write((short)1); // Type 1 = ICO
                bw.Write((short)bitmaps.Length); // Image count

                int offset = 6 + (16 * bitmaps.Length);
                byte[][] pngBytes = new byte[bitmaps.Length][];

                for (int i = 0; i < bitmaps.Length; i++)
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        bitmaps[i].Save(ms, ImageFormat.Png);
                        pngBytes[i] = ms.ToArray();
                    }

                    int bWidth = bitmaps[i].Width >= 256 ? 0 : bitmaps[i].Width;
                    int bHeight = bitmaps[i].Height >= 256 ? 0 : bitmaps[i].Height;

                    bw.Write((byte)bWidth);
                    bw.Write((byte)bHeight);
                    bw.Write((byte)0); // Color count
                    bw.Write((byte)0); // Reserved
                    bw.Write((short)1); // Color planes
                    bw.Write((short)32); // Bits per pixel
                    bw.Write((int)pngBytes[i].Length); // Bytes in resource
                    bw.Write((int)offset); // Offset to image

                    offset += pngBytes[i].Length;
                }

                for (int i = 0; i < bitmaps.Length; i++)
                {
                    bw.Write(pngBytes[i]);
                }
            }
        }
    }
}
