using System; using System.Drawing; using System.Drawing.Drawing2D; using System.Drawing.Imaging;
static class P
{
    static GraphicsPath RR(RectangleF r, float rad) { var p = new GraphicsPath(); float d = rad * 2; p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p; }
    static void Button(Graphics g, RectangleF r, string text, Color a, Color b)
    {
        for (int i = 4; i >= 1; i--) using (var gp = RR(RectangleF.Inflate(r, i * 2, i * 2), 10 + i * 2)) using (var pen = new Pen(Color.FromArgb(28, a), 4)) g.DrawPath(pen, gp);
        using (var p = RR(r, 10)) { using (var br = new LinearGradientBrush(r, a, b, 0f)) g.FillPath(br, p); using (var pen = new Pen(Color.FromArgb(230, 255, 255, 255), 2.2f)) g.DrawPath(pen, p); }
        using (var f = new Font("Segoe UI", 14, FontStyle.Bold, GraphicsUnit.Pixel)) using (var sb = new SolidBrush(Color.White)) using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center }) g.DrawString(text, f, sb, r, sf);
    }
    static int Main(string[] a)
    {
        var bm = new Bitmap(900, 420); using (var g = Graphics.FromImage(bm))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.FromArgb(12, 6, 26));
            using (var br = new LinearGradientBrush(new RectangleF(0, 0, 900, 420), Color.FromArgb(40, 14, 70), Color.FromArgb(10, 4, 22), 90f)) g.FillRectangle(br, 0, 0, 900, 420);
            Button(g, new RectangleF(30, 30, 170, 46), "ADD MUSIC", Color.FromArgb(150, 90, 255), Color.FromArgb(40, 160, 140));
            Button(g, new RectangleF(220, 30, 230, 46), "SPLIT VOCALS + BEAT", Color.FromArgb(70, 90, 255), Color.FromArgb(40, 70, 200));
            Button(g, new RectangleF(470, 30, 150, 46), "AUTO MIX", Color.FromArgb(200, 60, 230), Color.FromArgb(120, 40, 220));
            // a waveform lane
            var lane = new RectangleF(30, 110, 840, 120); using (var p = RR(lane, 8)) { using (var br = new SolidBrush(Color.FromArgb(70, 120, 60, 220))) g.FillPath(br, p); using (var pen = new Pen(Color.FromArgb(200, 170, 110, 255), 2)) g.DrawPath(pen, p); }
            var pts = new PointF[400]; for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(40 + i * 2.05f, 170 + (float)(Math.Sin(i * 0.21) * Math.Sin(i * 0.013) * 44)); using (var pen = new Pen(Color.FromArgb(255, 90, 200, 255), 1.6f)) g.DrawLines(pen, pts);
            using (var f = new Font("Segoe UI", 12, FontStyle.Bold, GraphicsUnit.Pixel)) using (var sb = new SolidBrush(Color.White)) g.DrawString("Drums", f, sb, 42, 116);
            // meters, a knob (arc), a glow dot, a radial (path gradient) glow
            for (int i = 0; i < 12; i++) using (var br = new SolidBrush(i > 9 ? Color.Red : (i > 7 ? Color.Gold : Color.LimeGreen))) g.FillRectangle(br, 40 + i * 14, 380 - 10, 11, 8);
            using (var pen = new Pen(Color.FromArgb(255, 200, 120, 255), 6) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(pen, 300, 260, 110, 110, 135, 200);
            using (var gp = new GraphicsPath()) { gp.AddEllipse(520, 250, 160, 160); using (var pb = new PathGradientBrush(gp) { CenterColor = Color.FromArgb(230, 230, 90, 255), SurroundColors = new[] { Color.FromArgb(0, 30, 10, 60) } }) g.FillPath(pb, gp); }
            using (var cb = new LinearGradientBrush(new RectangleF(700, 260, 170, 30), Color.Black, Color.White, 0f)) { cb.InterpolationColors = new ColorBlend(3) { Colors = new[] { Color.Red, Color.Lime, Color.Blue }, Positions = new[] { 0f, 0.5f, 1f } }; g.FillRectangle(cb, 700, 260, 170, 30); }
            using (var f = new Font("Segoe UI", 11, FontStyle.Regular, GraphicsUnit.Pixel)) using (var sb = new SolidBrush(Color.FromArgb(200, 220, 200, 255))) g.DrawString("A long hint line that has to wrap inside its box neatly, like the studio's hint strip does.", f, sb, new RectangleF(700, 300, 170, 80));
        }
        bm.Save(a.Length > 0 ? a[0] : "gfx-check.png", ImageFormat.Png); Console.WriteLine("saved"); return 0;
    }
}
