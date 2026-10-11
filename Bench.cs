using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
static class BenchT
{
    static void T(string n, int it, Action a) { a(); var sw = Stopwatch.StartNew(); for (int i = 0; i < it; i++) a(); Console.WriteLine(string.Format("{0,-58} {1,8:0.000} ms", n, sw.Elapsed.TotalMilliseconds / it)); }
    static void Main()
    {
        using (var bm = new Bitmap(1920, 1080, System.Drawing.Imaging.PixelFormat.Format32bppPArgb)) using (var g = Graphics.FromImage(bm))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; var r = new RectangleF(100, 100, 1500, 800);
            Func<RectangleF, float, GraphicsPath> rr = (q, rad) => { var p = new GraphicsPath(); float d = rad * 2; p.AddArc(q.X, q.Y, d, d, 180, 90); p.AddArc(q.Right - d, q.Y, d, d, 270, 90); p.AddArc(q.Right - d, q.Bottom - d, d, d, 0, 90); p.AddArc(q.X, q.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p; };
            using (var b = new SolidBrush(Color.FromArgb(235, 18, 10, 34))) using (var bo = new SolidBrush(Color.FromArgb(255, 18, 10, 34)))
            {
                T("FillPath rrect alpha 1500x800 AA", 20, () => { using (var p = rr(r, 12)) g.FillPath(b, p); });
                T("FillPath rrect opaque 1500x800 AA", 20, () => { using (var p = rr(r, 12)) g.FillPath(bo, p); });
                g.SmoothingMode = SmoothingMode.None;
                T("FillPath rrect alpha no AA", 20, () => { using (var p = rr(r, 12)) g.FillPath(b, p); });
                T("FillRectangle alpha 1500x800", 20, () => g.FillRectangle(b, r));
                T("FillRectangle opaque 1500x800", 20, () => g.FillRectangle(bo, r));
                g.CompositingMode = CompositingMode.SourceCopy; T("FillRectangle opaque SourceCopy 1500x800", 20, () => g.FillRectangle(bo, r)); g.CompositingMode = CompositingMode.SourceOver;
                using (var src = new Bitmap(1920, 1080, System.Drawing.Imaging.PixelFormat.Format32bppPArgb)) { T("DrawImage full 1920x1080 SourceOver", 20, () => g.DrawImage(src, 0, 0, 1920, 1080)); g.CompositingMode = CompositingMode.SourceCopy; T("DrawImage full SourceCopy", 20, () => g.DrawImage(src, 0, 0, 1920, 1080)); g.CompositingMode = CompositingMode.SourceOver;
                  var rect = new Rectangle(0, 0, 1920, 1080); T("DrawImage(src, rect, rect, Pixel)", 20, () => g.DrawImage(src, rect, 0, 0, 1920, 1080, GraphicsUnit.Pixel)); }
                g.SmoothingMode = SmoothingMode.AntiAlias; var sm = new RectangleF(100, 100, 120, 36);
                T("small rrect fill AA x100", 10, () => { for (int i = 0; i < 100; i++) using (var p = rr(sm, 8)) g.FillPath(b, p); });
                T("small rrect stroke AA x100", 10, () => { using (var pen = new Pen(Color.FromArgb(200, 255, 255, 255), 1)) for (int i = 0; i < 100; i++) using (var p = rr(sm, 8)) g.DrawPath(pen, p); });
                T("glow(3 strokes) small x100", 10, () => { for (int k = 3; k >= 1; k--) using (var pen = new Pen(Color.FromArgb(40, 255, 0, 255), k * 2f)) for (int i = 0; i < 100; i++) using (var p = rr(sm, 8)) g.DrawPath(pen, p); });
                T("DrawLine AA 1000 lines w3.6", 10, () => { using (var pen = new Pen(Color.White, 3.6f)) for (int i = 0; i < 1000; i++) g.DrawLine(pen, 500 + i % 50, 300, 700 + i % 100, 600); });
                g.SmoothingMode = SmoothingMode.None; T("DrawLine noAA 1000 lines w3.6", 10, () => { using (var pen = new Pen(Color.White, 3.6f)) for (int i = 0; i < 1000; i++) g.DrawLine(pen, 500 + i % 50, 300, 700 + i % 100, 600); }); g.SmoothingMode = SmoothingMode.AntiAlias;
                T("PathGradient ellipse r=450 fill", 10, () => { using (var gp = new GraphicsPath()) { gp.AddEllipse(500, 100, 900, 900); using (var pg = new PathGradientBrush(gp) { CenterColor = Color.FromArgb(100, 150, 90, 255), SurroundColors = new[] { Color.FromArgb(0, 150, 90, 255) } }) g.FillEllipse(pg, 500, 100, 900, 900); } });
                T("DrawString x100", 10, () => { using (var f = new Font("Bahnschrift", 14, FontStyle.Bold)) using (var fm = new StringFormat()) for (int i = 0; i < 100; i++) g.DrawString("HELLO WORLD", f, Brushes.White, 10, 10, fm); });
            }
        }
    }
}
