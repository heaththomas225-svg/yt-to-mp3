using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
static class Bench2T
{
    static void T(string n, int it, Action a) { a(); var sw = Stopwatch.StartNew(); for (int i = 0; i < it; i++) a(); Console.WriteLine(string.Format("{0,-62} {1,8:0.000} ms", n, sw.Elapsed.TotalMilliseconds / it)); }
    static void Main()
    {
        using (var dst = new Bitmap(1920, 1080, System.Drawing.Imaging.PixelFormat.Format32bppPArgb)) using (var g = Graphics.FromImage(dst))
        using (var half = new Bitmap(960, 540, System.Drawing.Imaging.PixelFormat.Format32bppPArgb)) using (var quarter = new Bitmap(480, 270, System.Drawing.Imaging.PixelFormat.Format32bppPArgb)) using (var hg = Graphics.FromImage(half))
        {
            g.CompositingMode = CompositingMode.SourceCopy;
            foreach (var mode in new[] { InterpolationMode.NearestNeighbor, InterpolationMode.Bilinear, InterpolationMode.HighQualityBilinear })
            {
                g.InterpolationMode = mode;
                T("half->full 1920x1080 SourceCopy " + mode, 10, () => g.DrawImage(half, new Rectangle(0, 0, 1920, 1080), 0, 0, 960, 540, GraphicsUnit.Pixel));
                T("quarter->full SourceCopy " + mode, 10, () => g.DrawImage(quarter, new Rectangle(0, 0, 1920, 1080), 0, 0, 480, 270, GraphicsUnit.Pixel));
            }
            g.CompositingMode = CompositingMode.SourceOver; g.InterpolationMode = InterpolationMode.Bilinear;
            T("half->full SourceOver Bilinear", 10, () => g.DrawImage(half, new Rectangle(0, 0, 1920, 1080), 0, 0, 960, 540, GraphicsUnit.Pixel));
            hg.SmoothingMode = SmoothingMode.AntiAlias; using (var b = new SolidBrush(Color.FromArgb(40, 0, 0, 0))) T("fade half-res rect alpha fill", 20, () => hg.FillRectangle(b, 0, 0, 960, 540));
            hg.InterpolationMode = InterpolationMode.Bilinear; hg.CompositingMode = CompositingMode.SourceCopy; T("feedback zoom half->half SourceCopy bilinear", 10, () => hg.DrawImage(half, new Rectangle(-10, -6, 980, 552), 0, 0, 960, 540, GraphicsUnit.Pixel));
            hg.CompositingMode = CompositingMode.SourceOver;
            using (var pen = new Pen(Color.FromArgb(120, 255, 0, 255), 5f)) T("96 lines thick on half-res AA", 10, () => { for (int i = 0; i < 96; i++) hg.DrawLine(pen, 480 + i, 270, 480 + i * 3, 100); });
            using (var br = new SolidBrush(Color.FromArgb(200, 255, 0, 255))) T("96 quads FillPolygon on half-res AA", 10, () => { for (int i = 0; i < 96; i++) hg.FillPolygon(br, new[] { new PointF(480 + i, 270), new PointF(484 + i, 270), new PointF(484 + i * 3, 100), new PointF(480 + i * 3, 100) }); });
            using (var br = new SolidBrush(Color.FromArgb(200, 255, 0, 255))) T("1 path 96 quads FillPath half-res AA", 10, () => { using (var gp = new GraphicsPath()) { for (int i = 0; i < 96; i++) gp.AddPolygon(new[] { new PointF(480 + i, 270), new PointF(484 + i, 270), new PointF(484 + i * 3, 100), new PointF(480 + i * 3, 100) }); hg.FillPath(br, gp); } });
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var br = new SolidBrush(Color.FromArgb(200, 255, 0, 255))) T("1 path 96 quads FillPath FULL-res AA", 10, () => { using (var gp = new GraphicsPath()) { for (int i = 0; i < 96; i++) gp.AddPolygon(new[] { new PointF(900 + i, 540), new PointF(904 + i, 540), new PointF(904 + i * 3, 200), new PointF(900 + i * 3, 200) }); g.FillPath(br, gp); } });
            using (var pen = new Pen(Color.FromArgb(200, 255, 0, 255), 2f)) T("DrawClosedCurve 180pts full AA", 10, () => { var pts = new PointF[180]; for (int i = 0; i < 180; i++) pts[i] = new PointF(960 + (float)Math.Cos(i / 28.6) * 300, 540 + (float)Math.Sin(i / 28.6) * 300); g.DrawClosedCurve(pen, pts); });
            using (var pen = new Pen(Color.FromArgb(200, 255, 0, 255), 2f)) T("DrawPolygon 180pts full AA", 10, () => { var pts = new PointF[180]; for (int i = 0; i < 180; i++) pts[i] = new PointF(960 + (float)Math.Cos(i / 28.6) * 300, 540 + (float)Math.Sin(i / 28.6) * 300); g.DrawPolygon(pen, pts); });
        }
    }
}
