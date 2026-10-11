using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// The needle (playhead): a glowing neon beam with a bright head, and tiny neon sparks that spit off it like fat in a hot pan
// while the music plays or while you drag it. The ruler strip it is dragged from has its own neon border, so you can see where to grab it.
partial class StudioForm
{
    sealed class Spark { public float X, Y, VX, VY, Life, Max, R; public Color C; }
    readonly List<Spark> sparks = new List<Spark>(); readonly Random sparkRnd = new Random(11); readonly System.Diagnostics.Stopwatch needleClock = System.Diagnostics.Stopwatch.StartNew();
    double needleLast, sparkAcc; bool needleDrag;
    static Color[] SparkCols = { Color.FromArgb(255, 70, 225), Color.FromArgb(0, 229, 255), Color.FromArgb(255, 255, 255), Color.FromArgb(190, 130, 255), Color.FromArgb(255, 70, 225), Color.FromArgb(0, 229, 255) };

    // the strip at the top of the tracks: a neon border (it lights up under the mouse and while you drag)
    void PaintRulerNeon(Graphics g, RectangleF ruler)
    {
        bool hot = Hot(ruler) || needleDrag; var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias; var r = RectangleF.Inflate(ruler, -1, -1);
        using (var path = RR(r, 6))
        {
            for (int i = 3; i >= 1; i--) using (var pen = new Pen(Color.FromArgb((hot ? 62 : 34) / i + 4, NeonPink), i * 3.2f)) g.DrawPath(pen, path);
            using (var br = new LinearGradientBrush(r, Color.FromArgb(hot ? 255 : 215, 0, 229, 255), Color.FromArgb(hot ? 255 : 215, 255, 70, 225), 0f)) using (var pen = new Pen(br, hot ? 2f : 1.5f)) g.DrawPath(pen, path);
        }
        g.SmoothingMode = sm;
    }

    // the beam, its head and the sparks
    void PaintNeedle(Graphics g, float px, float top, float bottom, float rulerH)
    {
        double now = needleClock.Elapsed.TotalSeconds, dt = Math.Min(0.05, Math.Max(0, now - needleLast)); needleLast = now;
        bool playing = eng.Playing, active = playing || needleDrag; float peak = playing ? Math.Min(1f, Math.Max(eng.MasterPeakL, eng.MasterPeakR) * 1.6f) : 0f; double pulse = 0.5 + 0.5 * Math.Sin(now * 7.0);
        var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias; float boost = 1f + peak * 0.9f + (float)pulse * 0.12f;
        // the beam: a wide soft pink glow, a cyan fringe, a hot pink body and a white core
        float[] gw = { 30, 20, 13, 8 }; int[] ga = { 12, 22, 40, 74 };
        for (int i = 0; i < gw.Length; i++) using (var pen = new Pen(Color.FromArgb(Math.Min(255, (int)(ga[i] * boost)), NeonPink), gw[i] * (0.9f + 0.25f * peak))) { pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; g.DrawLine(pen, px, top, px, bottom); }
        using (var pen = new Pen(Color.FromArgb(110, 0, 229, 255), 1f)) { g.DrawLine(pen, px - 2.5f, top + 14, px - 2.5f, bottom); g.DrawLine(pen, px + 2.5f, top + 14, px + 2.5f, bottom); }
        using (var pen = new Pen(Color.FromArgb(215, NeonPink), 3.4f)) g.DrawLine(pen, px, top, px, bottom);
        using (var pen = new Pen(Color.FromArgb(250, 255, 245, 255), 1.7f)) g.DrawLine(pen, px, top, px, bottom);
        // the head
        float hy = top + 7;
        foreach (var rr in new[] { 25f, 18f, 12f }) using (var b = new SolidBrush(Color.FromArgb((int)(Math.Min(255, (rr == 12 ? 62 : (rr == 18 ? 34 : 18)) * boost)), NeonPink))) g.FillEllipse(b, px - rr, hy - rr, rr * 2, rr * 2);
        var head = new[] { new PointF(px - 9.5f, top + 1), new PointF(px + 9.5f, top + 1), new PointF(px, top + 16) };
        using (var b = new SolidBrush(Color.White)) g.FillPolygon(b, head); using (var pen = new Pen(Color.FromArgb(255, NeonPink), 1.8f) { LineJoin = LineJoin.Round }) g.DrawPolygon(pen, head);
        // sparks: small neon balls that pop off the beam, arc up and out, and fall away
        if (active)
        {
            double rate = playing ? 34 + peak * 60 : 55; sparkAcc += rate * dt;
            while (sparkAcc >= 1 && sparks.Count < 90)
            {
                sparkAcc -= 1; float u = (float)sparkRnd.NextDouble(); float y = top + 10 + u * u * Math.Max(40, bottom - top - 10) * 0.85f; float side = sparkRnd.Next(2) == 0 ? -1 : 1; float sp = 45 + (float)sparkRnd.NextDouble() * 150;
                sparks.Add(new Spark { X = px + (float)(sparkRnd.NextDouble() - 0.5) * 3f, Y = y, VX = side * sp * (0.4f + (float)sparkRnd.NextDouble() * 0.6f), VY = -(35 + (float)sparkRnd.NextDouble() * 150), Max = 0.55f + (float)sparkRnd.NextDouble() * 0.7f, R = 2.0f + (float)sparkRnd.NextDouble() * 1.7f, C = SparkCols[sparkRnd.Next(SparkCols.Length)] });
                sparks[sparks.Count - 1].Life = sparks[sparks.Count - 1].Max;
            }
        }
        else sparkAcc = 0;
        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            var s = sparks[i]; s.VY += 340f * (float)dt; s.X += s.VX * (float)dt; s.Y += s.VY * (float)dt; s.Life -= (float)dt;
            if (s.Life <= 0 || s.Y > bottom + 30 || s.Y < top - 40) { sparks.RemoveAt(i); continue; }
            float a = Math.Max(0, Math.Min(1, s.Life / s.Max)); float rad = s.R * (0.55f + 0.45f * a);
            using (var b = new SolidBrush(Color.FromArgb((int)(70 * a), s.C))) g.FillEllipse(b, s.X - rad * 2.6f, s.Y - rad * 2.6f, rad * 5.2f, rad * 5.2f);
            using (var b = new SolidBrush(Color.FromArgb((int)(235 * a), s.C))) g.FillEllipse(b, s.X - rad, s.Y - rad, rad * 2, rad * 2);
            using (var b = new SolidBrush(Color.FromArgb((int)(255 * a), 255, 255, 255))) g.FillEllipse(b, s.X - rad * 0.42f, s.Y - rad * 0.42f, rad * 0.84f, rad * 0.84f);
        }
        if (sparks.Count > 0) lastInput = DateTime.Now;   // keep drawing until the last spark has faded
        g.SmoothingMode = sm;
    }
}
