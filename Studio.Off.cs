using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// A track can be turned off completely with the neon light at the top left of its header: it goes quiet (nothing is played or processed, solo and the vocal sidechain ignore it),
// its header dims, and on the timeline its audio turns very translucent and shakes itself apart: an animated earthquake of broken strips, gaps and cracks.
partial class StudioForm
{
    bool AnyTrackOff() { foreach (var t in eng.Tracks.ToArray()) if (t.Off) return true; return false; }
    void SetOff(Track tk, bool off) { foreach (var x in eng.Tracks.ToArray()) if (x == tk || x.Host == tk) x.Off = off; }
    // the light turns the whole row off (its own clip and every layer on it); an original that was unloaded after separation is read back first when it is turned on
    void ToggleOff(Track tk)
    {
        if (tk.Off && tk.Clip != null && !tk.Clip.Loaded) { RunBusy("Loading " + tk.Name + " back into memory...", true, p => tk.Clip.Reload(p, () => cancelFlag), ok => { if (ok) SetOff(tk, false); }); return; }
        bool v = !tk.Off; SetOff(tk, v); lastInput = DateTime.Now; Toast(v ? tk.Name + " is OFF: it makes no sound. Click its light to turn it on again." : tk.Name + " is on again.");
    }

    // the header of a track that is off: everything dimmed, and the light is a hollow ring that slowly breathes (press it to switch the track back on)
    void PaintHeaderOff(Graphics g, RectangleF r, Track tr)
    {
        using (var br = new SolidBrush(Color.FromArgb(140, 5, 3, 12))) g.FillRectangle(br, r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
        var c = Neon(tr.Color); float cx = r.X + 20, cy = r.Y + 17; double pulse = 0.5 + 0.5 * Math.Sin(Environment.TickCount / 420.0);
        using (var br = new SolidBrush(Color.FromArgb(235, 8, 4, 18))) g.FillEllipse(br, cx - 7.5f, cy - 7.5f, 15, 15);
        using (var pen = new Pen(Color.FromArgb(90 + (int)(120 * pulse), c), 1.8f)) g.DrawEllipse(pen, cx - 6, cy - 6, 12, 12);
    }

    // a number between -1 and 1 that is always the same for the same three numbers
    static double QRnd(long a, int b, int c) { unchecked { long h = a * 73856093L ^ b * 19349663L ^ c * 83492791L; h ^= h >> 13; h *= 0x5bd1e995L; h ^= h >> 15; return ((h & 0xFFFF) / 32767.5) - 1.0; } }

    // the picture of the clip (img, drawn at ix, iy) very translucent and broken into strips that jerk sideways like an earthquake, with gaps and cracks across it
    void PaintQuake(Graphics g, Bitmap img, int ix, int iy, Track tr, RectangleF lane, RectangleF cr)
    {
        var area = RectangleF.Intersect(lane, new RectangleF(cr.X - 24, lane.Y, cr.Width + 48, lane.Height)); if (area.IsEmpty) return;
        var gs = g.Save(); g.SetClip(lane, CombineMode.Intersect);
        double now = Environment.TickCount / 1000.0; long step = (long)(now * 15); int seed = (tr.Name ?? "").GetHashCode() & 0xFFFF;
        double rumble = 2.2 + 3.4 * Math.Abs(Math.Sin(now * 2.1 + seed)), burst = Math.Sin(now * 6.3 + seed * 0.37) > 0.80 ? 1.0 : 0.0;   // a steady rumble and now and then a violent jolt
        int bands = Math.Max(5, (int)(img.Height / 13f)); float bandH = img.Height / (float)bands;
        using (var ia = new ImageAttributes())
        {
            ia.SetColorMatrix(new ColorMatrix { Matrix00 = 1, Matrix11 = 1, Matrix22 = 1, Matrix33 = 0.30f, Matrix44 = 1 });
            for (int b = 0; b < bands; b++)
            {
                if (QRnd(step, b, seed) > 0.78 && b > 0) continue;   // a strip missing for a moment: the picture is broken open
                double jitter = QRnd(step, b, seed + 1) * (rumble + burst * 14.0); if (QRnd(step / 2, b, seed + 2) > 0.86) jitter *= 3.5;   // some strips are thrown much further
                float dy = (float)(QRnd(step + 5, b, seed + 3) * (1.0 + burst * 2.0)); float sy = b * bandH, sh = Math.Min(bandH + 1f, img.Height - sy); if (sh <= 0) continue;
                g.DrawImage(img, new Rectangle(ix + (int)Math.Round(jitter), iy + (int)Math.Round(sy + dy), img.Width, (int)Math.Ceiling(sh)), 0, (int)sy, img.Width, (int)Math.Ceiling(sh), GraphicsUnit.Pixel, ia);
            }
        }
        // cracks: jagged lines from the top to the bottom of the clip that flicker
        var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias; int cracks = (int)Math.Max(2, Math.Min(6, cr.Width / 160f));
        for (int k = 0; k < cracks; k++)
        {
            double fx = 0.12 + (k + 0.5 * (QRnd(seed, k, 9) + 1) * 0.5) / cracks * 0.80; float x = cr.X + (float)(fx * cr.Width); var pts = new List<PointF>(); int segs = 7;
            for (int j = 0; j <= segs; j++) pts.Add(new PointF(x + (float)(QRnd(step / 3, j + k * 11, seed + 4) * (7 + burst * 10)), area.Y + area.Height * j / segs));
            using (var pen = new Pen(Color.FromArgb(46, Neon(tr.Color)), 5f)) g.DrawLines(pen, pts.ToArray());
            using (var pen = new Pen(Color.FromArgb(150 + (int)(70 * burst), 255, 220, 245), 1.1f)) g.DrawLines(pen, pts.ToArray());
        }
        g.SmoothingMode = sm; g.Restore(gs);
    }
}
