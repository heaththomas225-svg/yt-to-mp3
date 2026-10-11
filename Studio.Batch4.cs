using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

// BACK from every page, the animated Purphex logo behind Community and My Projects, neon sparks on the button you point at, STEREO / MONO / THIN TRACKS helpers and the
// "are you sure" questions for AUTO MIX, MIC and MIDI REC.
// the last guard before the sound card: peaks above full scale are turned down smoothly (a quick attack, a slow release) instead of being cut off flat
sealed class OutSafety
{
    float g = 1f; float[] need = new float[0], aheadBuf = new float[0]; const int Ahead = 40;
    public void Process(float[] l, float[] r, int n)
    {
        if (need.Length < n + Ahead) need = new float[n + Ahead];
        bool any = false; for (int k = 0; k < n; k++) { float a = l[k] < 0 ? -l[k] : l[k], b = r[k] < 0 ? -r[k] : r[k], pk = a > b ? a : b; float nd = pk > 0.98f ? 0.98f / pk : 1f; need[k] = nd; if (nd < 1f) any = true; }
        if (!any && g >= 0.9999f) { g = 1f; return; }   // nothing too loud: the signal is left exactly as it is
        for (int k = n; k < n + Ahead; k++) need[k] = 1f;
        // look 40 samples ahead so the gain is already down when the peak arrives (no chopping, no click)
        if (aheadBuf.Length < n) aheadBuf = new float[n]; var ahead = aheadBuf;
        for (int k = 0; k < n; k++) { float m = 1f; int e = Math.Min(n + Ahead - 1, k + Ahead); for (int j = k; j <= e; j++) if (need[j] < m) m = need[j]; ahead[k] = m; }
        for (int k = 0; k < n; k++) { float tg = ahead[k]; g += (tg - g) * (tg < g ? 0.12f : 0.00004f); if (g > 1f) g = 1f; l[k] *= g; r[k] *= g; }
    }
}

partial class StudioForm
{
    // ---------- navigation: BACK goes to the page you came from ----------
    int navFrom, navLast;
    void NavTrack() { if (tab != navLast) { navFrom = navLast; navLast = tab; } }
    void GoBack() { StopPreview(); int t = navFrom == tab ? 0 : navFrom; if (t < 0 || t > 4) t = 0; projBin = false; tab = t; }
    bool ShowBack { get { return tab != 0 || navFrom == 3 || navFrom == 4; } }

    // ---------- questions (a test answers for itself) ----------
    Func<string, bool> askHook;
    bool AskYes(string text, string title)
    {
        if (askHook != null) return askHook(text);
        return MessageBox.Show(this, text, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
    }

    // ---------- tracks: STEREO and MONO are two buttons; THIN TRACKS squeezes the lanes ----------
    void SetMono(Track t, bool mono)
    {
        if (t.Mono == mono) return; PushUndo(); t.Mono = mono;
        Toast(t.Name + " is now " + (mono ? "MONO: left and right play the same sound." : "STEREO: left and right are kept apart."));
    }
    int normalLaneH = 132;
    void ToggleThinTracks() { if (LaneH < 96) LaneH = normalLaneH; else { normalLaneH = LaneH; LaneH = 84; } }

    // ---------- sparks flying off the button you point at ----------
    readonly List<Spark> hSparks = new List<Spark>(); readonly Random hRnd = new Random(23); double hLast, hAcc; float hDt = 0.016f;
    void HoverTick() { double now = needleClock.Elapsed.TotalSeconds; hDt = (float)Math.Max(0.001, Math.Min(0.05, now - hLast)); hLast = now; }
    void HoverSparks(RectangleF r)
    {
        hAcc += 70 * hDt; lastInput = DateTime.Now; btnAnim = true; float per = 2 * (r.Width + r.Height);
        while (hAcc >= 1 && hSparks.Count < 260)
        {
            hAcc -= 1; float p = (float)hRnd.NextDouble() * per, x, y, nx, ny;
            if (p < r.Width) { x = r.X + p; y = r.Y; nx = 0; ny = -1; } else if (p < r.Width + r.Height) { x = r.Right; y = r.Y + (p - r.Width); nx = 1; ny = 0; }
            else if (p < 2 * r.Width + r.Height) { x = r.Right - (p - r.Width - r.Height); y = r.Bottom; nx = 0; ny = 1; } else { x = r.X; y = r.Bottom - (p - 2 * r.Width - r.Height); nx = -1; ny = 0; }
            float sp = 25 + (float)hRnd.NextDouble() * 55; float max = 0.4f + (float)hRnd.NextDouble() * 0.5f;
            hSparks.Add(new Spark { X = x, Y = y, VX = nx * sp + (float)(hRnd.NextDouble() - 0.5) * 30, VY = ny * sp - 25 - (float)hRnd.NextDouble() * 35, Max = max, Life = max, R = 1.1f + (float)hRnd.NextDouble() * 1.5f, C = SparkCols[hRnd.Next(SparkCols.Length)] });
        }
        if (hAcc > 4) hAcc = 0;
    }
    void PaintHoverSparks(Graphics g)
    {
        if (hSparks.Count == 0) return; float dt = hDt; var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        for (int i = hSparks.Count - 1; i >= 0; i--)
        {
            var s = hSparks[i]; s.VY += 200f * dt; s.X += s.VX * dt; s.Y += s.VY * dt; s.Life -= dt; if (s.Life <= 0) { hSparks.RemoveAt(i); continue; }
            float k = s.Life / s.Max; int a = (int)(255 * k);
            using (var br = new SolidBrush(Color.FromArgb(Math.Min(255, (int)(70 * k)), s.C))) g.FillEllipse(br, s.X - s.R * 2.2f, s.Y - s.R * 2.2f, s.R * 4.4f, s.R * 4.4f);
            using (var br = new SolidBrush(Color.FromArgb(a, s.C))) g.FillEllipse(br, s.X - s.R, s.Y - s.R, s.R * 2, s.R * 2);
        }
        g.SmoothingMode = sm; btnAnim = true;
    }

    Bitmap softLogo; int softLogoSide;
    Bitmap SoftLogo(int side)
    {   // the logo drawn once, faint, at the size needed (re-made only when the window size changes a lot)
        side = Math.Max(64, side / 16 * 16); if (softLogo != null && softLogoSide == side) return softLogo; var lg = AppLogo.Logo; if (lg == null) return null;
        float asp = lg.Width / (float)Math.Max(1, lg.Height); int w = asp >= 1 ? side : (int)(side * asp), h = asp >= 1 ? (int)(side / asp) : side; var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        using (var g2 = Graphics.FromImage(bmp)) using (var ia = new ImageAttributes()) { ia.SetColorMatrix(new ColorMatrix { Matrix33 = 0.22f }); g2.InterpolationMode = InterpolationMode.HighQualityBicubic; g2.DrawImage(lg, new Rectangle(0, 0, w, h), 0, 0, lg.Width, lg.Height, GraphicsUnit.Pixel, ia); }
        if (softLogo != null) softLogo.Dispose(); softLogo = bmp; softLogoSide = side; return bmp;
    }
    // ---------- the animated Purphex logo behind Community and My Projects (like the one on the website: a turning ring of light and a breathing glow) ----------
    void PaintLogoBackdrop(Graphics g, RectangleF area)
    {
        double t = needleClock.Elapsed.TotalSeconds; float d = Math.Min(Math.Min(area.Width * 0.7f, area.Height * 0.86f), 720f); if (d < 140) return; float cx = area.X + area.Width / 2, cy = area.Y + area.Height / 2; var gs = g.Save(); g.SetClip(area, CombineMode.Intersect);
        float breathe = 0.5f + 0.5f * (float)Math.Sin(t * 0.9);
        // the breathing ring and glow
        var ring = new RectangleF(cx - d / 2, cy - d / 2, d, d); float pad = d * 0.045f * (0.6f + 0.4f * breathe);
        using (var gp = new GraphicsPath()) { gp.AddEllipse(ring); using (var pg = new PathGradientBrush(gp) { CenterColor = Color.FromArgb(34, 124, 58, 237), SurroundColors = new[] { Color.FromArgb(0, 124, 58, 237) } }) g.FillEllipse(pg, ring); }
        using (var pen = new Pen(Color.FromArgb((int)(22 + 36 * breathe), 190, 120, 255), 1.4f)) g.DrawEllipse(pen, ring.X + pad, ring.Y + pad, ring.Width - 2 * pad, ring.Height - 2 * pad);
        // the turning arc: purple fading into cyan, like the website
        float ang = (float)(t * 40 % 360); using (var pen = new Pen(Color.FromArgb(150, 168, 85, 247), 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            for (int i = 0; i < 28; i++) { float k = i / 27f; pen.Color = Color.FromArgb((int)(40 + 120 * k), (int)(168 - 168 * k), (int)(85 + 144 * k), (int)(247 + 8 * k)); g.DrawArc(pen, ring, ang + i * 4f, 5f); }
        // the logo itself, soft so the cards stay easy to read
        var soft = SoftLogo((int)(d * 0.66f)); if (soft != null) g.DrawImageUnscaled(soft, (int)(cx - soft.Width / 2), (int)(cy - soft.Height / 2));
        g.Restore(gs); lastInput = DateTime.Now; btnAnim = true;
    }
}
