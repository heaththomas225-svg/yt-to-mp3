using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// A track can be turned off completely with the neon light at the top left of its header: it goes quiet (nothing is played or processed, solo and the vocal sidechain ignore it),
// its header dims, and on the timeline its audio turns very translucent and shakes itself apart: an animated earthquake of broken strips, gaps and cracks.
partial class StudioForm
{
    // the power button of a track (top left of its header): a round glossy button with a power sign, lit in the track colour when on, dark red when off;
    // pressing it sends pulse rings out of it
    readonly Dictionary<Track, int> powerPulse = new Dictionary<Track, int>();
    const float PowerR = 11f;
    void PaintPowerButton(Graphics g, Track tr, float cx, float cy, bool hot)
    {
        bool on = !tr.Off; Color c = on ? Neon(tr.Color) : Color.FromArgb(255, 255, 70, 90); var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias; float R = PowerR;
        int t0; if (powerPulse.TryGetValue(tr, out t0)) { int age = unchecked(Environment.TickCount - t0); if (age > 900) powerPulse.Remove(tr); else { lastInput = DateTime.Now; for (int k = 0; k < 2; k++) { float u = Math.Max(0, (age - k * 220) / 680f); if (u <= 0 || u >= 1) continue; float rr = R + 4 + u * 30; using (var pen = new Pen(Color.FromArgb((int)(220 * (1 - u)), c), 2.6f * (1 - u) + 0.6f)) g.DrawEllipse(pen, cx - rr, cy - rr, rr * 2, rr * 2); } } }
        if (on || hot) using (var gp = new GraphicsPath()) { gp.AddEllipse(cx - R - 5, cy - R - 5, (R + 5) * 2, (R + 5) * 2); using (var pb = new PathGradientBrush(gp) { CenterColor = Color.FromArgb(on ? 120 : 70, c), SurroundColors = new[] { Color.FromArgb(0, c) } }) g.FillPath(pb, gp); }
        var face = new RectangleF(cx - R, cy - R, R * 2, R * 2); using (var lb = new LinearGradientBrush(face, on ? TM.C(255, 46, 30, 80) : TM.C(255, 40, 10, 16), on ? TM.C(255, 12, 6, 26) : TM.C(255, 14, 4, 8), 90f)) g.FillEllipse(lb, face);
        using (var hl = new LinearGradientBrush(new RectangleF(cx - R, cy - R, R * 2, R), Color.FromArgb(70, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f)) g.FillEllipse(hl, cx - R + 2, cy - R + 1, R * 2 - 4, R);
        using (var pen = new Pen(Color.FromArgb(hot ? 255 : 220, c), 2f)) g.DrawEllipse(pen, face);
        float s = R * 0.52f; using (var pen = new Pen(Color.FromArgb(on ? 255 : 230, on ? c : Color.FromArgb(255, 255, 120, 130)), 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) { g.DrawArc(pen, cx - s, cy - s + 1, s * 2, s * 2, -50, 280); g.DrawLine(pen, cx, cy - s - 2, cx, cy + 0.5f); }
        g.SmoothingMode = sm;
    }
    bool AnyTrackOff() { foreach (var t in eng.Tracks.ToArray()) if (t.Off) return true; return false; }
    void SetOff(Track tk, bool off) { foreach (var x in eng.Tracks.ToArray()) if (x == tk || x.Host == tk) x.Off = off; }
    // the light turns the whole row off (its own clip and every layer on it); an original that was unloaded after separation is read back first when it is turned on
    void ToggleOff(Track tk)
    {
        if (tk.Off && tk.Clip != null && !tk.Clip.Loaded) { RunBusy("Loading " + tk.Name + " back into memory...", true, p => tk.Clip.Reload(p, () => cancelFlag), ok => { if (ok) SetOff(tk, false); }); return; }
        bool v = !tk.Off; SetOff(tk, v); lastInput = DateTime.Now; powerPulse[tk] = Environment.TickCount; Toast(v ? tk.Name + " is OFF: it makes no sound. Press its power button to turn it on again." : tk.Name + " is on again.");
    }

    // the header of a track that is off: everything dimmed, and the light is a hollow ring that slowly breathes (press it to switch the track back on)
    void PaintHeaderOff(Graphics g, RectangleF r, Track tr)
    {
        { var swallow = Reg(r, () => Toast(tr.Name + " is OFF: press its power button (top left) to turn it on before you touch it.")); var light = Rc(r.X + 7, r.Y + 4, 26, 26); Reg(light, () => ToggleOff(tr)); }   // a track that is off cannot be touched: only its light answers
        using (var br = new SolidBrush(Color.FromArgb(140, 5, 3, 12))) g.FillRectangle(br, r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
        var c = Neon(tr.Color); float cx = r.X + 20, cy = r.Y + 17; double pulse = 0.5 + 0.5 * Math.Sin(Environment.TickCount / 420.0);
        PaintPowerButton(g, tr, cx, cy, Hot(Rc(r.X + 7, r.Y + 4, 26, 26)));   // OFF: the power button is dark red
        using (var pen = new Pen(Color.FromArgb(40 + (int)(90 * pulse), 255, 70, 90), 1.4f)) g.DrawEllipse(pen, cx - PowerR - 3, cy - PowerR - 3, (PowerR + 3) * 2, (PowerR + 3) * 2);   // and a ring that slowly breathes round it
    }

    // a number between -1 and 1 that is always the same for the same three numbers
    static double QRnd(long a, int b, int c) { unchecked { long h = a * 73856093L ^ b * 19349663L ^ c * 83492791L; h ^= h >> 13; h *= 0x5bd1e995L; h ^= h >> 15; return ((h & 0xFFFF) / 32767.5) - 1.0; } }

    // the picture of the clip (img, drawn at ix, iy) of a track that is off: a faint, glitching ghost of the audio (the colours split apart like a broken screen, strips jerk sideways, scan lines and blocks flicker),
    // lit by the colour of the track, with bolts of lightning that strike across it, branch, flash the light round them and spark where they land
    static System.Drawing.Imaging.ImageAttributes offFaint;
    void PaintQuake(Graphics g, Bitmap img, int ix, int iy, Track tr, RectangleF lane, RectangleF cr)
    {
        var area = RectangleF.Intersect(lane, new RectangleF(cr.X - 24, lane.Y, cr.Width + 48, lane.Height)); if (area.IsEmpty) return;
        if (Prefs.PerfMode)   // PERFORMANCE MODE: a track that is off has no glitch and no lightning: its waves are still there, very faint (5%), with a line that says why it cannot be edited
        {
            if (offFaint == null) { var cm = new System.Drawing.Imaging.ColorMatrix(); cm.Matrix33 = 0.05f; offFaint = new System.Drawing.Imaging.ImageAttributes(); offFaint.SetColorMatrix(cm); }
            var gp = g.Save(); g.SetClip(area, System.Drawing.Drawing2D.CombineMode.Intersect); g.DrawImage(img, new Rectangle(ix, iy, img.Width, img.Height), 0, 0, img.Width, img.Height, GraphicsUnit.Pixel, offFaint); g.Restore(gp);
            if (area.Width > 140) Txt(g, "THIS TRACK IS TURNED OFF: TURN IT BACK ON TO EDIT", 12, Color.FromArgb(215, 255, 255, 255), Rc(area.X, lane.Y, area.Width, lane.Height), StringAlignment.Center, true); return;
        }
        var gs = g.Save(); g.SetClip(lane, CombineMode.Intersect); lastInput = DateTime.Now;
        double now = Environment.TickCount / 1000.0; long step = (long)(now * 16); int seed = (tr.Name ?? "").GetHashCode() & 0xFFFF;
        Color tc = Neon(tr.Color), cyan = Color.FromArgb(255, 0, 229, 255), pink = Color.FromArgb(255, 255, 60, 220), bolt = MixC(cyan, tc, 0.25f);
        double rumble = 1.0 + 2.0 * Math.Abs(Math.Sin(now * 2.1 + seed)), burst = Math.Sin(now * 6.3 + seed * 0.37) > 0.82 ? 1.0 : 0.0;
        double flick = 0.65 + 0.35 * Math.Abs(Math.Sin(now * 27 + seed)); if (QRnd(step, 1, seed) > 0.7) flick *= 0.35;
        // the light: a wash of the track's colour from above that flickers, and a cool glow from below
        using (var br = new LinearGradientBrush(area, Color.FromArgb((int)(46 * flick), tc), Color.FromArgb(0, tc), 90f)) g.FillRectangle(br, area);
        using (var br = new LinearGradientBrush(area, Color.FromArgb(0, cyan), Color.FromArgb((int)(22 * flick), cyan), 90f)) g.FillRectangle(br, area);
        // the ghost of the audio: three copies, a red one, a cyan one and a pale one, a little apart (that is the glitch), each cut into strips that jerk sideways
        int bands = Math.Max(6, (int)(img.Height / 9f)); float bandH = img.Height / (float)bands; float split = 1.6f + (float)burst * 5f + (QRnd(step, 3, seed) > 0.8 ? 4f : 0f);
        Action<ColorMatrix, float> strips = (cm, xoff) =>
        {
            using (var ia = new ImageAttributes())
            {
                ia.SetColorMatrix(cm);
                for (int b = 0; b < bands; b++)
                {
                    if (QRnd(step, b, seed) > 0.88 && b > 0) continue;   // a strip is missing for a moment
                    double jitter = QRnd(step, b, seed + 1) * (rumble + burst * 10.0); if (QRnd(step / 2, b, seed + 2) > 0.92) jitter *= 4.0;   // now and then a strip is thrown far
                    float dy = (float)(QRnd(step + 5, b, seed + 3) * (0.6 + burst * 1.6)); float sy = b * bandH, sh = Math.Min(bandH + 1f, img.Height - sy); if (sh <= 0) continue;
                    g.DrawImage(img, new Rectangle(ix + (int)Math.Round(jitter + xoff), iy + (int)Math.Round(sy + dy), img.Width, (int)Math.Ceiling(sh)), 0, (int)sy, img.Width, (int)Math.Ceiling(sh), GraphicsUnit.Pixel, ia);
                }
            }
        };
        strips(new ColorMatrix { Matrix00 = 1.4f, Matrix33 = 0.34f, Matrix44 = 1 }, -split);   // red
        strips(new ColorMatrix { Matrix11 = 1.2f, Matrix22 = 1.4f, Matrix33 = 0.36f, Matrix44 = 1 }, split);   // cyan
        strips(new ColorMatrix { Matrix00 = 1, Matrix11 = 1, Matrix22 = 1, Matrix33 = (float)(0.16 + 0.10 * flick), Matrix44 = 1 }, 0);   // the pale one
        // scan lines that crawl, and blocks of colour that blink on and off
        using (var pen = new Pen(Color.FromArgb(52, 0, 0, 0), 1f)) for (float y = area.Y + (step % 3); y < area.Bottom; y += 3) g.DrawLine(pen, area.X, y, area.Right, y);
        for (int k = 0; k < 4; k++)
        {
            if (QRnd(step, 50 + k, seed) < 0.15) continue; float w = 22 + (float)(QRnd(step, 60 + k, seed) + 1) * 55, h = 2 + (float)(QRnd(step, 61 + k, seed) + 1) * 3.5f;
            float bx = area.X + (float)((QRnd(step, 70 + k, seed) + 1) / 2) * Math.Max(1f, area.Width - w), by = area.Y + (float)((QRnd(step, 80 + k, seed) + 1) / 2) * Math.Max(1f, area.Height - h);
            using (var br = new SolidBrush(Color.FromArgb(80, k % 2 == 0 ? cyan : pink))) g.FillRectangle(br, bx, by, w, h); using (var br = new SolidBrush(Color.FromArgb(150, 255, 255, 255))) g.FillRectangle(br, bx, by, Math.Min(w, 10f), h);
        }
        // the lightning
        var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias; int bolts = (int)Math.Max(2, Math.Min(7, cr.Width / 170f)); const int segs = 14;
        for (int k = 0; k < bolts; k++)
        {
            double clock = now * 1.7 + k * 0.41 + (seed & 255) / 255.0 * 3.0; long strike = (long)Math.Floor(clock); double t = clock - strike; if (t > 0.30) continue;
            float I = (float)(1.0 - t / 0.30); if (QRnd(strike, k, seed + 7) > 0.2 && t > 0.10 && t < 0.17) I *= 0.35f;   // a bolt flickers: dark for a blink, then strikes again
            float x = cr.X + 20 + (float)((QRnd(strike, k, seed + 8) + 1) / 2) * Math.Max(1f, cr.Width - 40); var pts = new PointF[segs + 1]; float walk = 0;
            for (int j = 0; j <= segs; j++) { walk = walk * 0.62f + (float)QRnd(strike, j + k * 31, seed + 9) * 15f; pts[j] = new PointF(x + walk, area.Y + area.Height * j / segs); }
            { var band = new RectangleF(x - 90, area.Y, 180, area.Height); using (var br = new LinearGradientBrush(band, Color.Transparent, Color.Transparent, 0f)) { var cb = new ColorBlend(3) { Colors = new[] { Color.FromArgb(0, bolt), Color.FromArgb((int)(60 * I), bolt), Color.FromArgb(0, bolt) }, Positions = new[] { 0f, 0.5f, 1f } }; br.InterpolationColors = cb; g.FillRectangle(br, band); } }   // the flash: the light round the bolt
            Action<PointF[], float> stroke = (p, f) =>
            {
                using (var pen = new Pen(Color.FromArgb((int)(46 * I * f), tc), 10f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLines(pen, p);
                using (var pen = new Pen(Color.FromArgb((int)(120 * I * f), bolt), 4f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLines(pen, p);
                using (var pen = new Pen(Color.FromArgb((int)(255 * I * f), 255, 250, 255), 1.4f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLines(pen, p);
            };
            stroke(pts, 1f);
            for (int br2 = 0; br2 < 2; br2++)   // branches that fork off and fade out
            {
                int j0 = 2 + (int)((QRnd(strike, k + 40 + br2, seed + 10) + 1) / 2 * (segs - 6)); float dir = QRnd(strike, k + 44 + br2, seed + 11) > 0 ? 1 : -1; var bp = new List<PointF> { pts[j0] }; float bx2 = pts[j0].X, by2 = pts[j0].Y;
                for (int q = 0; q < 5; q++) { bx2 += dir * (7 + (float)(QRnd(strike, q + br2 * 7, seed + 12) + 1) * 5); by2 += 7 + (float)(QRnd(strike, q + 9 + br2 * 7, seed + 13) + 1) * 4; if (by2 > area.Bottom) break; bp.Add(new PointF(bx2, by2)); }
                if (bp.Count > 1) stroke(bp.ToArray(), 0.75f);
            }
            var hit = pts[segs]; using (var br = new SolidBrush(Color.FromArgb((int)(70 * I), bolt))) g.FillEllipse(br, hit.X - 15, hit.Y - 9, 30, 12); using (var br = new SolidBrush(Color.FromArgb((int)(190 * I), 255, 255, 255))) g.FillEllipse(br, hit.X - 4, hit.Y - 3, 8, 5);   // where it lands: a glow and a few sparks
            for (int sp = 0; sp < 5; sp++) { float sx = hit.X + (float)QRnd(strike, sp, seed + 14) * 26 * (1 - I * 0.4f), sy2 = hit.Y - 2 - (float)(QRnd(strike, sp + 5, seed + 15) + 1) * 14 * (1.2f - I); using (var br = new SolidBrush(Color.FromArgb((int)(220 * I), sp % 2 == 0 ? cyan : Color.White))) g.FillRectangle(br, sx, sy2, 2f, 2f); }
        }
        g.SmoothingMode = sm; g.Restore(gs);
    }
}
