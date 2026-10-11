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
    // HOME goes to the main studio and forgets where you have been; BACK steps back through the pages you went through, one at a time
    int navLast; readonly List<int> navStack = new List<int>(); bool navPopping;
    void NavTrack()
    {
        if (tab == navLast) return;
        if (navPopping) navPopping = false; else if (navStack.Count == 0 || navStack[navStack.Count - 1] != navLast) { navStack.Add(navLast); if (navStack.Count > 24) navStack.RemoveAt(0); }
        navLast = tab;
    }
    void GoHome() { StopPreview(); projBin = false; navStack.Clear(); navPopping = false; tab = 0; navLast = 0; }
    void GoBack()
    {
        StopPreview(); projBin = false;
        while (navStack.Count > 0) { int t = navStack[navStack.Count - 1]; navStack.RemoveAt(navStack.Count - 1); if (t == tab || t < 0 || t > 5) continue; navPopping = true; tab = t; return; }
        GoHome();
    }
    // BACK only appears when it has somewhere to take you that is not home (HOME is its own button)
    bool ShowBack { get { for (int i = navStack.Count - 1; i >= 0; i--) { int t = navStack[i]; if (t == tab) continue; return t != 0; } return false; } }
    bool ShowHome { get { return tab != 0; } }

    // ---------- REVERT: whenever something is applied (a preset, a style, a theme, AUTO MASTERING...) a bar offers to put everything back as it was ----------
    Action revertAction; string revertText = ""; DateTime revertUntil;
    internal void OfferRevert(string what, Action undo)
    {
        if (what.Length > 40) what = what.Substring(0, 38) + "..";
        revertAction = undo; revertText = what; revertUntil = DateTime.Now.AddSeconds(60);
    }
    void DoRevert() { var a = revertAction; revertAction = null; if (a == null) return; try { a(); } catch (Exception ex) { Toast("Could not revert: " + ex.Message); return; } Toast("Reverted: " + revertText + " is back as it was."); }
    void PaintRevertBar(Graphics g)
    {
        if (revertAction == null) return; if (DateTime.Now > revertUntil) { revertAction = null; return; }
        float W = ClientSize.Width, H = ClientSize.Height, bw = Math.Min(W - 28, 470), bh = 42, by = H - (tab == 0 ? 56 : 10) - bh - 4; var bar = Rc(14, by, bw, bh);
        FillRR(g, Color.FromArgb(240, 18, 30, 52), bar, 10); StrokeRR(g, Color.FromArgb(200, NeonCyan), bar, 10, 1.3f);
        var rv = Rc(bar.Right - 128, bar.Y + 6, 90, 30); var cl = Rc(bar.Right - 32, bar.Y + 9, 24, 24);
        Txt(g, revertText.ToUpper(), 12, Ink, Rc(bar.X + 12, bar.Y, bar.Width - 164, bar.Height), StringAlignment.Near, true);
        Btn(g, rv, "REVERT", 3, () => DoRevert(), 12); Hint2(rv, "Put everything back exactly as it was before you pressed USE");
        Btn(g, cl, "x", 0, () => revertAction = null, 12); Hint2(cl, "Hide this");
    }

    // applies something to a track's plugin chain with undo (Ctrl+Z) and the REVERT bar
    void ChainApply(Track tr, string what, Action apply) { var snap = TakeSnapshot(); PushUndo(); apply(); OfferRevert(what + " on " + tr.Name, () => Restore(snap)); }

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
    readonly List<RectangleF> btnRects = new List<RectangleF>(); readonly Dictionary<string, RectangleF> btnMap = new Dictionary<string, RectangleF>(StringComparer.OrdinalIgnoreCase);
    // where a button is on screen right now (empty if it is not on this page): the guided tours and SHOW ME point at the real button
    internal Rectangle TargetOf(string label)
    {
        RectangleF r; if (!btnMap.TryGetValue(label, out r)) { r = RectangleF.Empty; foreach (var kv in btnMap) if (kv.Key.StartsWith(label, StringComparison.OrdinalIgnoreCase)) { r = kv.Value; break; } }
        if (r.IsEmpty) return Rectangle.Empty; return Rectangle.Round(RectangleF.Inflate(r, 1, 1));
    }
    // SHOW ME: finds the page the button lives on, goes there and points at it with the button's own help text
    internal bool ShowOnScreen(string label)
    {
        int keep = tab; var r = TargetOf(label);
        if (r.IsEmpty) { foreach (int t in new[] { 0, 1, 2, 3, 4 }) { if (t == keep) continue; tab = t; Refresh(); r = TargetOf(label); if (!r.IsEmpty) break; } }
        if (r.IsEmpty) { tab = keep; Toast("Could not find " + label + " on this screen. Open the page it belongs to first."); return false; }
        string txt; if (!Hints.TryGetValue(label, out txt)) { txt = "This is the " + label + " button."; foreach (var kv in Hints) if (label.StartsWith(kv.Key, StringComparison.OrdinalIgnoreCase) || kv.Key.StartsWith(label, StringComparison.OrdinalIgnoreCase)) { txt = kv.Value; break; } }
        string lb = label; Tour.Run(this, new List<TourStep> { new TourStep(() => TargetOf(lb), Win.Up(label), txt) }); return true;
    }
    readonly List<Spark> hSparks = new List<Spark>(); readonly Random hRnd = new Random(23); double hLast, hAcc; float hDt = 0.016f;
    void HoverTick() { double now = needleClock.Elapsed.TotalSeconds; hDt = (float)Math.Max(0.001, Math.Min(0.05, now - hLast)); hLast = now; }
    void HoverSparks(RectangleF r)
    {
        hAcc += 38 * hDt; lastInput = DateTime.Now; btnAnim = true; float per = 2 * (r.Width + r.Height);
        while (hAcc >= 1 && hSparks.Count < 260)
        {
            hAcc -= 1; float p = (float)hRnd.NextDouble() * per, x, y, nx, ny;
            if (p < r.Width) { x = r.X + p; y = r.Y; nx = 0; ny = -1; } else if (p < r.Width + r.Height) { x = r.Right; y = r.Y + (p - r.Width); nx = 1; ny = 0; }
            else if (p < 2 * r.Width + r.Height) { x = r.Right - (p - r.Width - r.Height); y = r.Bottom; nx = 0; ny = 1; } else { x = r.X; y = r.Bottom - (p - 2 * r.Width - r.Height); nx = -1; ny = 0; }
            float sp = 22 + (float)hRnd.NextDouble() * 40; float max = 0.35f + (float)hRnd.NextDouble() * 0.4f;
            hSparks.Add(new Spark { X = x, Y = y, VX = nx * sp + (float)(hRnd.NextDouble() - 0.5) * 30, VY = ny * sp - 25 - (float)hRnd.NextDouble() * 35, Max = max, Life = max, R = 0.9f + (float)hRnd.NextDouble() * 1.0f, C = SparkCols[hRnd.Next(SparkCols.Length)] });
        }
        if (hAcc > 4) hAcc = 0;
    }
    void PaintHoverSparks(Graphics g)
    {
        if (hSparks.Count == 0) return; float dt = hDt; var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        // the sparks stay UNDER every button: the buttons are cut out of the picture, so a spark can never hide one
        var gstate = g.Save(); using (var rgn = new Region(new RectangleF(0, 0, ClientSize.Width, ClientSize.Height))) { int cnt = 0; foreach (var br0 in btnRects) { rgn.Exclude(br0); if (++cnt > 160) break; } g.SetClip(rgn, CombineMode.Replace); }
        for (int i = hSparks.Count - 1; i >= 0; i--)
        {
            var s = hSparks[i]; s.VY += 200f * dt; s.X += s.VX * dt; s.Y += s.VY * dt; s.Life -= dt; if (s.Life <= 0) { hSparks.RemoveAt(i); continue; }
            float k = s.Life / s.Max; int a = (int)(170 * k);
            using (var br = new SolidBrush(Color.FromArgb(Math.Min(255, (int)(34 * k)), s.C))) g.FillEllipse(br, s.X - s.R * 2.0f, s.Y - s.R * 2.0f, s.R * 4.0f, s.R * 4.0f);
            using (var br = new SolidBrush(Color.FromArgb(a, s.C))) g.FillEllipse(br, s.X - s.R, s.Y - s.R, s.R * 2, s.R * 2);
        }
        g.Restore(gstate); g.SmoothingMode = sm; btnAnim = true;
    }

    // the glow, the faint logo and the inner ring never move, so they are drawn once into a picture and copied each frame (cheap); only the turning arc is drawn live
    Bitmap bdBmp; int bdSide; Pen bdPen = new Pen(Color.White, 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round }, bdRing = new Pen(Color.White, 1.4f);
    Bitmap BackdropBitmap(int d)
    {
        d = Math.Max(160, d / 16 * 16); if (bdBmp != null && bdSide == d) return bdBmp; var lg = AppLogo.Logo; var bmp = new Bitmap(d, d, PixelFormat.Format32bppPArgb);
        using (var g2 = Graphics.FromImage(bmp))
        {
            g2.SmoothingMode = SmoothingMode.AntiAlias;
            using (var gp = new GraphicsPath()) { gp.AddEllipse(0, 0, d - 1, d - 1); using (var pg = new PathGradientBrush(gp) { CenterColor = Color.FromArgb(38, 124, 58, 237), SurroundColors = new[] { Color.FromArgb(0, 124, 58, 237) } }) g2.FillEllipse(pg, 0, 0, d - 1, d - 1); }
            if (lg != null)
            {
                float side = d * 0.66f, asp = lg.Width / (float)Math.Max(1, lg.Height), w = asp >= 1 ? side : side * asp, h = asp >= 1 ? side / asp : side;
                using (var ia = new ImageAttributes()) { ia.SetColorMatrix(new ColorMatrix { Matrix33 = 0.24f }); g2.InterpolationMode = InterpolationMode.HighQualityBicubic; g2.DrawImage(lg, new Rectangle((int)((d - w) / 2), (int)((d - h) / 2), (int)w, (int)h), 0, 0, lg.Width, lg.Height, GraphicsUnit.Pixel, ia); }
            }
        }
        if (bdBmp != null) bdBmp.Dispose(); bdBmp = bmp; bdSide = d; return bmp;
    }
    // the animated Purphex logo behind Community and My Projects (like the one on the website: a turning ring of light and a soft breathing glow)
    void PaintLogoBackdrop(Graphics g, RectangleF area)
    {
        double t = needleClock.Elapsed.TotalSeconds; float d = Math.Min(Math.Min(area.Width * 0.7f, area.Height * 0.86f), 720f); if (d < 140) return; float cx = area.X + area.Width / 2, cy = area.Y + area.Height / 2; var gs = g.Save(); g.SetClip(area, CombineMode.Intersect);
        var bmp = BackdropBitmap((int)d); int bx = (int)(cx - bmp.Width / 2f), by = (int)(cy - bmp.Height / 2f); g.DrawImageUnscaled(bmp, bx, by); float side = bmp.Width - 2; var ring = new RectangleF(bx + 1, by + 1, side, side);
        float breathe = 0.5f + 0.5f * (float)Math.Sin(t * 0.9), pad = side * 0.045f * (0.6f + 0.4f * breathe); bdRing.Color = Color.FromArgb((int)(22 + 36 * breathe), NeonViolet); g.DrawEllipse(bdRing, ring.X + pad, ring.Y + pad, ring.Width - 2 * pad, ring.Height - 2 * pad);
        float ang = (float)(t * 40 % 360);
        for (int i = 0; i < 16; i++) { float k = i / 15f; bdPen.Color = Color.FromArgb((int)(40 + 120 * k), (int)(NeonViolet.R + (NeonCyan.R - NeonViolet.R) * k), (int)(NeonViolet.G + (NeonCyan.G - NeonViolet.G) * k), (int)(NeonViolet.B + (NeonCyan.B - NeonViolet.B) * k)); g.DrawArc(bdPen, ring, ang + i * 6f, 7f); }
        g.Restore(gs); lastInput = DateTime.Now; btnAnim = true;
    }
}
