using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
static class Bench4T
{
    static void T(string n, int it, Action a) { a(); var sw = Stopwatch.StartNew(); for (int i = 0; i < it; i++) a(); Console.WriteLine(string.Format("{0,-62} {1,8:0.000} ms", n, sw.Elapsed.TotalMilliseconds / it)); }
    static void Main()
    {
        using (var dst = new Bitmap(1920, 1080, System.Drawing.Imaging.PixelFormat.Format32bppPArgb)) using (var g = Graphics.FromImage(dst)) using (var src = new Bitmap(1920, 1010, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            T("plain DrawImageUnscaled", 10, () => g.DrawImageUnscaled(src, 0, 70));
            T("plain DrawImage(src, x, y, w, h)", 10, () => g.DrawImage(src, 0, 70, 1920, 1010));
            T("plain DrawImage(src, destRect, srcRect, Pixel)", 10, () => g.DrawImage(src, new Rectangle(0, 70, 1920, 1010), 0, 0, 1920, 1010, GraphicsUnit.Pixel));
            g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            T("AA + cleartype DrawImageUnscaled", 10, () => g.DrawImageUnscaled(src, 0, 70));
            g.SetClip(new RectangleF(0, 70, 1920, 1010), CombineMode.Intersect);
            T("with SetClip rect: DrawImageUnscaled", 10, () => g.DrawImageUnscaled(src, 0, 70));
            T("with SetClip rect: DrawImage destRect srcRect Pixel", 10, () => g.DrawImage(src, new Rectangle(0, 70, 1920, 1010), 0, 0, 1920, 1010, GraphicsUnit.Pixel));
            g.ResetClip(); g.SetClip(new RectangleF(0, 70, 1920, 1010));
            T("SetClip replace: DrawImage destRect Pixel", 10, () => g.DrawImage(src, new Rectangle(0, 70, 1920, 1010), 0, 0, 1920, 1010, GraphicsUnit.Pixel));
            g.CompositingMode = CompositingMode.SourceOver; T("SourceOver DrawImage destRect Pixel clipped", 10, () => g.DrawImage(src, new Rectangle(0, 70, 1920, 1010), 0, 0, 1920, 1010, GraphicsUnit.Pixel));
            var gs = g.Save(); g.CompositingMode = CompositingMode.SourceCopy; T("after Save, SourceCopy clipped", 10, () => g.DrawImage(src, new Rectangle(0, 70, 1920, 1010), 0, 0, 1920, 1010, GraphicsUnit.Pixel));
        }
    }
}
