using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
static class Bench3T
{
    static void T(string n, int it, Action a) { a(); var sw = Stopwatch.StartNew(); for (int i = 0; i < it; i++) a(); Console.WriteLine(string.Format("{0,-62} {1,8:0.000} ms", n, sw.Elapsed.TotalMilliseconds / it)); }
    static void Main()
    {
        using (var dst = new Bitmap(1920, 1080, System.Drawing.Imaging.PixelFormat.Format32bppArgb)) using (var g = Graphics.FromImage(dst))
        {
            float cx = 960, cy = 540;
            Func<int, float, float, float, PointF[]> quad = (i, r0, len, wd) => { double an = i * Math.PI * 2 / 96; float ca = (float)Math.Cos(an), sa = (float)Math.Sin(an); float nx = -sa * wd / 2, ny = ca * wd / 2; return new[] { new PointF(cx + ca * r0 + nx, cy + sa * r0 + ny), new PointF(cx + ca * (r0 + len) + nx, cy + sa * (r0 + len) + ny), new PointF(cx + ca * (r0 + len) - nx, cy + sa * (r0 + len) - ny), new PointF(cx + ca * r0 - nx, cy + sa * r0 - ny) }; };
            using (var br = new SolidBrush(Color.FromArgb(220, 255, 0, 255)))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias; T("96 FillPolygon AA len 200 w 5 (full-res, Argb dest)", 10, () => { for (int i = 0; i < 96; i++) g.FillPolygon(br, quad(i, 300, 200, 5)); });
                T("96 FillPolygon AA len 60 w 5", 10, () => { for (int i = 0; i < 96; i++) g.FillPolygon(br, quad(i, 300, 60, 5)); });
                g.SmoothingMode = SmoothingMode.None; T("96 FillPolygon noAA len 200 w 5", 10, () => { for (int i = 0; i < 96; i++) g.FillPolygon(br, quad(i, 300, 200, 5)); });
                using (var pen = new Pen(Color.FromArgb(220, 255, 0, 255), 5f)) { T("96 DrawLine noAA len 200 w 5", 10, () => { for (int i = 0; i < 96; i++) { var q = quad(i, 300, 200, 0); g.DrawLine(pen, q[0], q[1]); } }); g.SmoothingMode = SmoothingMode.AntiAlias; T("96 DrawLine AA len 200 w5", 10, () => { for (int i = 0; i < 96; i++) { var q = quad(i, 300, 200, 0); g.DrawLine(pen, q[0], q[1]); } }); }
                g.SmoothingMode = SmoothingMode.AntiAlias; var sm = new Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppPArgb); using (var sg = Graphics.FromImage(sm)) sg.FillEllipse(Brushes.White, 1, 1, 14, 14);
                T("300 sprite DrawImage 16x16 unscaled", 10, () => { for (int i = 0; i < 300; i++) g.DrawImageUnscaled(sm, 100 + i * 3, 100); });
                T("300 FillEllipse AA r3", 10, () => { for (int i = 0; i < 300; i++) g.FillEllipse(br, 100 + i * 3, 100, 6, 6); });
                g.SmoothingMode = SmoothingMode.None; T("300 FillRectangle 3x3 noAA alpha", 10, () => { for (int i = 0; i < 300; i++) g.FillRectangle(br, 100 + i * 3, 100, 3, 3); });
                g.SmoothingMode = SmoothingMode.AntiAlias;
                T("5 DrawEllipse AA r 300 w2", 10, () => { using (var pen = new Pen(Color.FromArgb(120, 0, 255, 255), 2f)) for (int i = 0; i < 5; i++) g.DrawEllipse(pen, cx - 300 - i * 20, cy - 300 - i * 20, 600 + i * 40, 600 + i * 40); });
                T("lock glow sprite build 600x600 per-pixel", 3, () => { var b = new Bitmap(600, 600, System.Drawing.Imaging.PixelFormat.Format32bppPArgb); var d = b.LockBits(new Rectangle(0, 0, 600, 600), System.Drawing.Imaging.ImageLockMode.WriteOnly, b.PixelFormat); var arr = new int[600 * 600]; for (int y = 0; y < 600; y++) for (int x = 0; x < 600; x++) { float dx = x - 300, dy = y - 300; float r = (float)Math.Sqrt(dx * dx + dy * dy) / 300f; float a = Math.Max(0, 1 - r); a = a * a * 0.4f; int al = (int)(a * 255); arr[y * 600 + x] = (al << 24) | ((200 * al / 255) << 16) | ((60 * al / 255) << 8) | (255 * al / 255); } System.Runtime.InteropServices.Marshal.Copy(arr, 0, d.Scan0, arr.Length); b.UnlockBits(d); b.Dispose(); });
            }
        }
    }
}
