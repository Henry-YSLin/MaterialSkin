using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MaterialSkin
{
    namespace ControlRenderExtension
    {
        public static class ControlHelper
        {
            #region Redraw Suspend/Resume
            [DllImport("user32.dll", EntryPoint = "SendMessageA", ExactSpelling = true, CharSet = CharSet.Ansi, SetLastError = true)]
            private static extern int SendMessage(IntPtr hwnd, int wMsg, int wParam, int lParam);
            private const int WM_SETREDRAW = 0xB;

            public static void SuspendDrawing(this Control target)
            {
                SendMessage(target.Handle, WM_SETREDRAW, 0, 0);
            }

            public static void ResumeDrawing(this Control target) { ResumeDrawing(target, true); }
            public static void ResumeDrawing(this Control target, bool redraw)
            {
                SendMessage(target.Handle, WM_SETREDRAW, 1, 0);

                if (redraw)
                {
                    target.Refresh();
                }
            }
            #endregion
        }

        public static class ShadowHelper
        {
            public static void Old_DrawChildShadow(this IMaterialControl target, Graphics g)
            {
                Control panel = (Control)target;
                foreach (IMaterialControl p in panel.Controls.OfType<IMaterialControl>())
                {
                    Control p2 = (Control)p;
                    GraphicsPath GP = new GraphicsPath();
                    GP.AddRectangle(new Rectangle(p2.Left, p2.Top, p2.Width, p2.Height));
                    g.TranslateTransform(p.Depth, p.Depth);
                    for (int i = p.Depth; i >= 1; i--)
                    {
                        g.TranslateTransform(-1f, -1f);                // <== shadow vector!
                        using (Pen pen = new Pen(Color.FromArgb((int)((255-MaterialSkinManager.SHADOW_COLOR.A) * (1 - i / (float)p.Depth)), MaterialSkinManager.SHADOW_COLOR.RemoveAlpha()), 1.75f))
                        {
                            g.DrawPath(pen, GP);
                        }
                    }
                    g.ResetTransform();
                }
            }

            public static void DrawChildShadow(this IMaterialControl target, Graphics g)
            {
                if (!MaterialSkinManager.SoftShadow) return;
                Control panel = (Control)target;
                foreach (IMaterialControl p in panel.Controls.OfType<IMaterialControl>())
                {
                    Control p2 = (Control)p;
                    if (p2.Visible != false && p.Depth!=0)
                    {
                        if (p.Shadow == null) {
                            Bitmap sBMP = new Bitmap(p2.Width + p.Depth * 18, p2.Height + p.Depth * 18);
                            Graphics sg = Graphics.FromImage(sBMP);
                            Color shadow = Color.FromArgb(MaterialSkinManager.SHADOW_COLOR.A - p.Depth , MaterialSkinManager.SHADOW_COLOR);
                            Color softShadow = Color.FromArgb(Math.Max(10, MaterialSkinManager.SOFT_SHADOW_COLOR.A - p.Depth * 3 / 2), MaterialSkinManager.SOFT_SHADOW_COLOR);
                            //if (p.ShadowShape != null)
                            //{//NOT IMPLEMENTED
                            //    PointF midPt = new Point(p2.Left + p2.Width / 2, p2.Top + p2.Height / 2);
                            //    GraphicsPath gp = (GraphicsPath)p.ShadowShape.Clone();
                            //    gp.ScaleAroundPivot(1.5f, 1.5f, midPt.X, midPt.Y);
                            //    sg.FillPath(new SolidBrush(softShadow), gp);
                            //    GraphicsPath gp2 = (GraphicsPath)p.ShadowShape.Clone();
                            //    gp2.ScaleAroundPivot(1.5f, 1.5f, midPt.X, midPt.Y);
                            //    sg.FillPath(new SolidBrush(shadow), gp2);
                            //}
                            //else
                            //{
                                sg.FillRectangle(new SolidBrush(softShadow), p.Depth * 9 - p.Depth * 3 / 2, p.Depth * 9 - p.Depth * 3 / 2, p2.Width + p.Depth * 3, p2.Height + p.Depth * 3);
                                sg.FillRectangle(new SolidBrush(shadow), p.Depth * 9 + p.Depth * 3 / 8 + 4, p.Depth * 9 + p.Depth / 2 + 5, p2.Width - p.Depth * 3 / 4 - 8, p2.Height + p.Depth / 2 - 7);
                            //}
                            sg.Flush(FlushIntention.Sync);
                            sBMP = sBMP.Blur(p.Depth + 1);
                            p.Shadow = sBMP;
                        }
                        g.DrawImage(p.Shadow, p2.Left - p.Depth * 9, p2.Top - p.Depth * 9);
                    }
                }
            }

            private static void ScaleAroundPivot(this GraphicsPath gp, 
                float scalex, float scaley, float px, float py)
            {
                Matrix m = new Matrix();
                m.Translate(-px, -py, MatrixOrder.Append);
                m.Scale(scalex, scaley, MatrixOrder.Append);
                m.Translate(px, py, MatrixOrder.Append);
                gp.Transform(m);
            }
        }

        public class GaussianBlur
        {
            public GaussianBlur()
            {
                Blur = (source, target, width, height, radius) =>
                {
                    var bxs = CalculateBoxes(radius, 3);
                    BoxBlur(source, target, width, height, (bxs[0] - 1) / 2);
                    BoxBlur(target, source, width, height, (bxs[1] - 1) / 2);
                    BoxBlur(source, target, width, height, (bxs[2] - 1) / 2);
                };

                CalculateBoxes = (sigma, n) =>
                {
                    var limits = IdealWidth(sigma, n);

                    var lower = limits[0];
                    var upper = limits[1];

                    var ideal = (12 * sigma * sigma - n * lower * lower - 4 * n * lower - 3 * n) / (-4 * lower - 4);

                    var m = Math.Round(ideal);

                    var sizes = new int[n];

                    for (int i = 0; i < n; i++)
                    {
                        sizes[i] = i < m ? lower : upper;
                    }

                    return sizes;
                };

                IdealWidth = (sigma, n) =>
                {
                    var ideal = Math.Sqrt((12 * sigma * sigma / n) + 1);

                    var limits = new int[2];
                    limits[0] = (int)Math.Floor(ideal);
                    if (limits[0] % 2 == 0) limits[0]--;
                    limits[1] = limits[0] + 2;

                    return limits;
                };

                BoxBlur = (source, target, width, height, radius) =>
                {
                    for (var i = 0; i < source.Length; i++) target[i] = source[i];

                    HorizontalBlur(target, source, width, height, radius);
                    TotalBlur(source, target, width, height, radius);
                };

                HorizontalBlur = (source, target, width, height, radius) =>
                {
                    var iarr = 1 / Convert.ToDouble(radius + radius + 1);

                    for (var i = 0; i < height; i++)
                    {
                        var ti = i * width;
                        var li = ti;
                        var ri = ti + radius;

                        var fv = source[ti];
                        var lv = source[ti + width - 1];

                        var val = (radius + 1) * fv;

                        for (var j = 0; j < radius; j++) val += source[ti + j];
                        for (var j = 0; j <= radius; j++) { val += source[ri++] - fv; target[ti++] = (byte)Math.Round(val * iarr); }
                        for (var j = radius + 1; j < width - radius; j++) { val += source[ri++] - source[li++]; target[ti++] = (byte)Math.Round(val * iarr); }
                        for (var j = width - radius; j < width; j++) { val += lv - source[li++]; target[ti++] = (byte)Math.Round(val * iarr); }
                    }
                };

                TotalBlur = (source, target, width, height, radius) =>
                {
                    var iarr = 1 / Convert.ToDouble(radius + radius + 1);

                    for (var i = 0; i < width; i++)
                    {
                        var ti = i;
                        var li = ti;
                        var ri = ti + (width * radius);

                        var fv = source[ti];
                        var lv = source[ti + width * (height - 1)];
                        var val = (radius + 1) * fv;

                        for (var j = 0; j < radius; j++)
                        {
                            val += source[ti + j * width];
                        }

                        for (var j = 0; j <= radius; j++)
                        {
                            val += source[ri] - fv;
                            target[ti] = (byte)Math.Round(val * iarr);
                            ri += width;
                            ti += width;
                        }

                        for (var j = radius + 1; j < height - radius; j++) { val += source[ri] - source[li]; target[ti] = (byte)Math.Round(val * iarr); li += width; ri += width; ti += width; }

                        for (var j = height - radius; j < height; j++) { val += lv - source[li]; target[ti] = (byte)Math.Round(val * iarr); li += width; ti += width; }
                    }
                };
            }

            public Action<byte[], byte[], int, int, int> Blur;

            private readonly Action<byte[], byte[], int, int, int> BoxBlur;

            private readonly Action<byte[], byte[], int, int, int> HorizontalBlur;

            private readonly Action<byte[], byte[], int, int, int> TotalBlur;

            private readonly Func<double, int, int[]> CalculateBoxes;

            private readonly Func<double, int, int[]> IdealWidth;

        }
        public static class BitmapExtensions
        {
            public static List<Color> Flatten(this Bitmap bitmap)
            {
                var size = bitmap.Size;

                var width = size.Width;
                var height = size.Height;

                var colors = new List<Color>();

                for (var i = 0; i < height; i++)
                {
                    for (var j = 0; j < width; j++)
                    {
                        colors.Add(bitmap.GetPixel(j, i));
                    }
                }

                return colors;
            }

            public static int Width(this Bitmap bitmap)
            {
                return bitmap.Size.Width;
            }

            public static int Height(this Bitmap bitmap)
            {
                return bitmap.Size.Height;
            }

            public static Bitmap Blur(this Bitmap bitmap, int radius)
            {
                var width = bitmap.Width();
                var height = bitmap.Height();

                var channels = bitmap.Flatten();

                var red = channels.Blur(c => c.Red(), width, height, radius);
                var green = channels.Blur(c => c.Green(), width, height, radius);
                var blue = channels.Blur(c => c.Blue(), width, height, radius);
                var alpha = channels.Blur(c => c.Alpha(), width, height, radius);

                var result = new Bitmap(width, height);

                var index = 0;
                for (var i = 0; i < height; i++)
                {
                    for (var j = 0; j < width; j++)
                    {
                        result.SetPixel(j, i, Color.FromArgb(alpha[index], red[index], green[index], blue[index]));
                        ++index;
                    }
                }

                return result;
            }
        }

        public static class ColourExtensions
        {

            public static byte[] Blur(this List<Color> channels, Func<List<Color>, byte[]> selector, int width, int height, int radius)
            {
                var blurrer = new GaussianBlur();

                var colour = selector(channels);

                var target = channels.Empty();

                blurrer.Blur(colour, target, width, height, radius);

                return target;
            }

            public static byte[] Empty(this List<Color> colours)
            {
                return colours.Select(c => new byte()).ToArray();
            }

            public static byte[] Red(this List<Color> colours)
            {
                return colours.Select(c => c.R).ToArray();
            }

            public static byte[] Green(this List<Color> colours)
            {
                return colours.Select(c => c.G).ToArray();
            }

            public static byte[] Blue(this List<Color> colours)
            {
                return colours.Select(c => c.B).ToArray();
            }

            public static byte[] Alpha(this List<Color> colours)
            {
                return colours.Select(c => c.A).ToArray();
            }
        }
    }

    static class DrawHelper
    {
        public static GraphicsPath CreateRoundRect(float x, float y, float width, float height, float radius)
        {
            GraphicsPath gp = new GraphicsPath();
            gp.AddLine(x + radius, y, x + width - (radius * 2), y);
            gp.AddArc(x + width - (radius * 2), y, radius * 2, radius * 2, 270, 90);
            gp.AddLine(x + width, y + radius, x + width, y + height - (radius * 2));
            gp.AddArc(x + width - (radius * 2), y + height - (radius * 2), radius * 2, radius * 2, 0, 90);
            gp.AddLine(x + width - (radius * 2), y + height, x + radius, y + height);
            gp.AddArc(x, y + height - (radius * 2), radius * 2, radius * 2, 90, 90);
            gp.AddLine(x, y + height - (radius * 2), x, y + radius);
            gp.AddArc(x, y, radius * 2, radius * 2, 180, 90);
            gp.CloseFigure();
            return gp;
        }

        public static GraphicsPath CreateUpRoundRect(float x, float y, float width, float height, float radius)
        {
            GraphicsPath gp = new GraphicsPath();

            gp.AddLine(x + radius, y, x + width - (radius * 2), y);
            gp.AddArc(x + width - (radius * 2), y, radius * 2, radius * 2, 270, 90);

            gp.AddLine(x + width, y + radius, x + width, y + height - (radius * 2) + 1);
            gp.AddArc(x + width - (radius * 2), y + height - (radius * 2), radius * 2, 2, 0, 90);

            gp.AddLine(x + width, y + height, x + radius, y + height);
            gp.AddArc(x, y + height - (radius * 2) + 1, radius * 2, 1, 90, 90);

            gp.AddLine(x, y + height, x, y + radius);
            gp.AddArc(x, y, radius * 2, radius * 2, 180, 90);

            gp.CloseFigure();
            return gp;
        }

        public static GraphicsPath CreateLeftRoundRect(float x, float y, float width, float height, float radius)
        {
            GraphicsPath gp = new GraphicsPath();
            gp.AddLine(x + radius, y, x + width - (radius * 2), y);
            gp.AddArc(x + width - (radius * 2), y, radius * 2, radius * 2, 270, 90);

            gp.AddLine(x + width, y + 0, x + width, y + height);
            gp.AddArc(x + width - (radius * 2), y + height - (1), radius * 2, 1, 0, 90);

            gp.AddLine(x + width - (radius * 2), y + height, x + radius, y + height);
            gp.AddArc(x, y + height - (radius * 2), radius * 2, radius * 2, 90, 90);

            gp.AddLine(x, y + height - (radius * 2), x, y + radius);
            gp.AddArc(x, y, radius * 2, radius * 2, 180, 90);

            gp.CloseFigure();
            return gp;
        }

        public static GraphicsPath CreateRoundRect(Rectangle rect, float radius)
        {
            return CreateRoundRect(rect.X, rect.Y, rect.Width, rect.Height, radius);
        }

        public static Color BlendColor(Color backgroundColor, Color frontColor, double blend)
        {
            double ratio = blend / 255d;
            double invRatio = 1d - ratio;
            int a = (int)((backgroundColor.A * invRatio) + (frontColor.A * ratio));
            int r = (int)((backgroundColor.R * invRatio) + (frontColor.R * ratio));
            int g = (int)((backgroundColor.G * invRatio) + (frontColor.G * ratio));
            int b = (int)((backgroundColor.B * invRatio) + (frontColor.B * ratio));
            return Color.FromArgb(a, r, g, b);
        }

        public static Color BlendColor(Color backgroundColor, Color frontColor)
        {
            return BlendColor(backgroundColor, frontColor, frontColor.A);
        }
    }
}
