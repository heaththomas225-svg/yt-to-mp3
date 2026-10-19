using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

// =====================================================================================================
//  PLUGIN VISUALISERS: every plugin slot on the mixer and every plugin window shows a little live picture of how much that plugin is
//  doing to the sound, in the Purphex look. The engine measures it (Fx.Act: how much the sound coming out differs from the sound going
//  in, Fx.OutLvl: how loud it comes out, Fx.GR: gain reduction), and the picture fits the plugin: ripples for reverbs, fading repeats for
//  delays, a pumping meter for compressors and limiters, a moving curve for EQs and filters, a crunchy stepped wave for distortion and
//  bit crushers, a bending wave for pitch and voice plugins, and bars for the rest. Bypassed plugins sit still. Moving parts are calmer in
//  Performance mode or with Windows animations off.
// =====================================================================================================
partial class StudioForm
{
    // the Purphex colours: always the brand violet, magenta and cyan, whatever the theme
    internal static readonly Color PxViolet = Color.FromArgb(255, 140, 92, 255), PxMagenta = Color.FromArgb(255, 236, 72, 200), PxCyan = Color.FromArgb(255, 92, 220, 255);
    // the colours of the pictures and slots: the Purphex design inside plugin and VST windows (TM.Brand is on there), the theme everywhere else
    Color VzV { get { return TM.Brand ? PxViolet : NeonViolet; } }
    Color VzP { get { return TM.Brand ? PxMagenta : NeonPink; } }
    Color VzC { get { return TM.Brand ? PxCyan : NeonCyan; } }
    enum VizKind { Ripples, Echo, Pump, Curve, Crunch, Bend, Bars }
    internal static string VizName(Fx f) { return VizOf(f).ToString(); }
    static VizKind VizOf(Fx f)
    {
        string t = (f.Type + " " + f.Title).ToLowerInvariant();
        Func<string[], bool> has = ws => { foreach (var w in ws) if (t.Contains(w)) return true; return false; };
        if (has(new[] { "reverb", "verb", "hall", "plate", "room", "space", "shimmer", "ambien" })) return VizKind.Ripples;
        if (has(new[] { "delay", "echo", "ping", "tape" })) return VizKind.Echo;
        if (has(new[] { "comp", "limit", "gate", "duck", "deess", "de-ess", "clip", "glue", "pump", "sidechain", "level", "loud" })) return VizKind.Pump;
        if (has(new[] { "eq", "filter", "clarity", "tilt", "bright", "warm", "air", "hp", "lp", "band", "tone" })) return VizKind.Curve;
        if (has(new[] { "dist", "crush", "bit", "sat", "drive", "fuzz", "tele", "phone", "radio", "lofi", "lo-fi", "grit", "demon" })) return VizKind.Crunch;
        if (has(new[] { "pitch", "tune", "voice", "formant", "harm", "chorus", "flang", "phase", "vibr", "wobble", "octave" })) return VizKind.Bend;
        return VizKind.Bars;
    }
    // the shown level follows the plugin smoothly (fast up, slow down) and keeps a short history for the moving pictures
    readonly Dictionary<Fx, float> fxvLvl = new Dictionary<Fx, float>(); readonly Dictionary<Fx, float[]> fxvHist = new Dictionary<Fx, float[]>(); readonly Dictionary<Fx, int> fxvTick = new Dictionary<Fx, int>();
    internal float VizLevel(Fx f)
    {
        VizKind k = VizOf(f); float target = f.Bypass || !eng.Playing ? 0f : (k == VizKind.Pump ? Math.Min(1f, f.GR / 10f + f.Act * 0.5f) : Math.Min(1f, f.Act));
        if (k == VizKind.Bars && !f.Bypass && eng.Playing) target = Math.Max(target, Math.Min(1f, f.OutLvl * 3f));
        float cur; fxvLvl.TryGetValue(f, out cur); cur += (target - cur) * (target > cur ? 0.5f : 0.08f); if (cur < 0.002f) cur = 0; fxvLvl[f] = cur;
        float[] h; if (!fxvHist.TryGetValue(f, out h)) { h = new float[48]; fxvHist[f] = h; }
        int now = Environment.TickCount, last; fxvTick.TryGetValue(f, out last);
        if (now - last >= 40) { fxvTick[f] = now; Array.Copy(h, 1, h, 0, h.Length - 1); h[h.Length - 1] = cur; }
        if (fxvLvl.Count > 400) { fxvLvl.Clear(); fxvHist.Clear(); fxvTick.Clear(); }   // (removed plugins don't pile up)
        return cur;
    }
    internal bool VizActive { get { return eng.Playing; } }

    // the picture, drawn inside r (a slot or a window's strip); big = the plugin window's larger version
    internal void PaintFxViz(Graphics g, RectangleF r, Fx f, bool big)
    {
        if (r.Width < 8 || r.Height < 6) return;
        float lvl = VizLevel(f); float[] hist; fxvHist.TryGetValue(f, out hist); if (hist == null) hist = new float[48];
        bool calm = UiTokens.ReducedMotion; double ph = calm ? 0.3 : (Environment.TickCount % 100000) / 1000.0;
        Color c1 = VzV, c2 = VzP, c3 = VzC; int baseA = f.Bypass ? 40 : 90;
        var gs = g.Save(); g.SetClip(r, CombineMode.Intersect); var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        float cx = r.X + r.Width * (big ? 0.5f : 0.78f), cy = r.Y + r.Height / 2;
        switch (VizOf(f))
        {
            case VizKind.Ripples:   // rings spreading out like a room answering
                { float maxR = big ? r.Width * 0.5f : r.Width * 0.45f; int rings = big ? 5 : 3;
                  for (int i = 0; i < rings; i++) { float k = (float)(((ph * 0.45) + i / (double)rings) % 1.0); float rad = 3 + k * maxR; int a = (int)((baseA + 150 * lvl) * (1 - k)); if (a < 6) continue;
                    using (var pen = new Pen(Color.FromArgb(Math.Min(255, a), i % 2 == 0 ? c3 : c2), big ? 2f : 1.3f)) g.DrawEllipse(pen, cx - rad, cy - rad * 0.55f, rad * 2, rad * 1.1f); }
                  using (var b = new SolidBrush(Color.FromArgb(Math.Min(255, 80 + (int)(170 * lvl)), c2))) g.FillEllipse(b, cx - 2.5f, cy - 2.5f, 5, 5); }
                break;
            case VizKind.Echo:   // the repeats: each one smaller than the last
                { int reps = big ? 8 : 5; float gap = (r.Width * (big ? 0.9f : 0.5f)) / reps, x0 = big ? r.X + r.Width * 0.05f : r.Right - r.Width * 0.5f - 4; float shift = (float)((ph * 0.8) % 1.0) * gap;
                  for (int i = 0; i < reps; i++) { float k = (float)Math.Pow(0.72, i); float hh = (r.Height - 4) * k * (0.25f + 0.75f * lvl); float x = x0 + i * gap + (calm ? 0 : shift * 0.3f); int a = (int)((baseA + 140 * lvl) * k);
                    using (var b = new SolidBrush(Color.FromArgb(Math.Min(255, a), i == 0 ? c2 : c3))) using (var p = RR(Rc(x, cy - hh / 2, Math.Max(2, gap * 0.4f), Math.Max(2, hh)), 1.5f)) g.FillPath(b, p); } }
                break;
            case VizKind.Pump:   // gain reduction hanging from the top, as it pumps
                { int n = hist.Length; float bw = (big ? r.Width : r.Width * 0.5f) / n, x0 = big ? r.X : r.Right - r.Width * 0.5f;
                  for (int i = 0; i < n; i++) { float v = hist[i]; if (v <= 0.01f) continue; float hh = (r.Height - 2) * v; using (var b = new SolidBrush(Color.FromArgb(Math.Min(255, 60 + (int)(180 * v)), FvMix(c2, Warn, v)))) g.FillRectangle(b, x0 + i * bw, r.Y + 1, Math.Max(1, bw - 0.6f), hh); }
                  if (big && f.GR > 0.1f) Txt(g, "-" + f.GR.ToString("0.0") + " dB", 10, Ink, Rc(r.X + 6, r.Bottom - 18, 90, 16), StringAlignment.Near, true); }
                break;
            case VizKind.Curve:   // a curve that swells with how much the sound is reshaped
                { float amp = (r.Height / 2 - 2) * (0.15f + 0.85f * lvl); var pts = new List<PointF>(); float x0 = big ? r.X : r.X + r.Width * 0.5f, w = big ? r.Width : r.Width * 0.5f;
                  for (float x = 0; x <= w; x += 2) pts.Add(new PointF(x0 + x, cy - amp * (float)Math.Sin(x / w * Math.PI * 2 * (big ? 2.5 : 1.5) + ph * 2.2)));
                  if (pts.Count > 1) using (var pen = new Pen(Color.FromArgb(Math.Min(255, baseA + (int)(160 * lvl)), c3), big ? 2.2f : 1.5f)) g.DrawLines(pen, pts.ToArray());
                  if (pts.Count > 1) { pts.Add(new PointF(x0 + w, r.Bottom)); pts.Add(new PointF(x0, r.Bottom)); using (var b = new LinearGradientBrush(r, Color.FromArgb((int)(60 * lvl) + 10, c1), Color.FromArgb(0, c1), 90f)) g.FillPolygon(b, pts.ToArray()); } }
                break;
            case VizKind.Crunch:   // a wave cut into steps (the crunch)
                { float amp = (r.Height / 2 - 2) * (0.25f + 0.75f * lvl); float step = big ? 7 : 5; int levels = Math.Max(2, (int)(7 - 5 * lvl)); var pts = new List<PointF>(); float x0 = big ? r.X : r.X + r.Width * 0.5f, w = big ? r.Width : r.Width * 0.5f;
                  for (float x = 0; x <= w; x += step) { double s = Math.Sin(x / w * Math.PI * (big ? 5 : 3) + ph * 3); float q = (float)(Math.Round(s * levels) / levels); float yy = cy - amp * q; pts.Add(new PointF(x0 + x, yy)); pts.Add(new PointF(x0 + x + step, yy)); }
                  if (pts.Count > 1) using (var pen = new Pen(Color.FromArgb(Math.Min(255, baseA + (int)(160 * lvl)), FvMix(c2, Warn, lvl)), big ? 2f : 1.4f)) g.DrawLines(pen, pts.ToArray()); }
                break;
            case VizKind.Bend:   // a wave whose pitch wobbles
                { float amp = (r.Height / 2 - 2) * (0.2f + 0.8f * lvl); var pts = new List<PointF>(); float x0 = big ? r.X : r.X + r.Width * 0.5f, w = big ? r.Width : r.Width * 0.5f;
                  for (float x = 0; x <= w; x += 2) { double fr = (big ? 4 : 2.5) * (1 + 0.35 * Math.Sin(ph * 1.7 + x / w * 3)); pts.Add(new PointF(x0 + x, cy - amp * (float)Math.Sin(x / w * Math.PI * 2 * fr + ph * 2))); }
                  if (pts.Count > 1) using (var pen = new Pen(Color.FromArgb(Math.Min(255, baseA + (int)(160 * lvl)), c1), big ? 2.2f : 1.5f)) g.DrawLines(pen, pts.ToArray());
                  if (pts.Count > 1) using (var pen = new Pen(Color.FromArgb(Math.Min(255, (baseA + (int)(160 * lvl)) / 2), c3), 1f)) { var p2 = pts.ConvertAll(p => new PointF(p.X, cy + (cy - p.Y) * 0.6f)); g.DrawLines(pen, p2.ToArray()); } }
                break;
            default:   // level bars, the newest on the right
                { int n = big ? hist.Length : 12; float bw = (big ? r.Width : r.Width * 0.5f) / n, x0 = big ? r.X : r.Right - r.Width * 0.5f;
                  for (int i = 0; i < n; i++) { float v = hist[hist.Length - n + i]; float hh = Math.Max(1.5f, (r.Height - 3) * v); using (var b = new SolidBrush(Color.FromArgb(Math.Min(255, baseA + (int)(150 * v)), FvMix(c1, c2, i / (float)n)))) g.FillRectangle(b, x0 + i * bw, r.Bottom - 1.5f - hh, Math.Max(1, bw - 1), hh); } }
                break;
        }
        g.SmoothingMode = sm; g.Restore(gs);
    }
    static Color FvMix(Color a, Color b, float k) { k = Math.Max(0, Math.Min(1, k)); return Color.FromArgb(255, (int)(a.R + (b.R - a.R) * k), (int)(a.G + (b.G - a.G) * k), (int)(a.B + (b.B - a.B) * k)); }

    // ---- the Purphex look for the plugin windows: the whole window (background, frame, buttons, sliders, writing) in the brand colours, whatever the theme ----
    class LookSave { public Color Ink, DimC, NV, NC, NP, NL, Ac, Ac2, Vi, Btn, Ed, Pn, Hot, Idle; public bool Custom; }
    LookSave BeginBrand()
    {
        var s = new LookSave { Ink = Ink, DimC = DimC, NV = NeonViolet, NC = NeonCyan, NP = NeonPink, NL = NeonLime, Ac = Accent, Ac2 = Accent2, Vi = Violet, Btn = BtnC, Ed = Edge, Pn = Panel, Hot = Win.NeonHot, Idle = Win.NeonIdle, Custom = UiTokens.Custom };
        var t = Themes.All[0]; TM.Brand = true; UiTokens.Custom = false;
        Ink = Color.FromArgb(255, 240, 232, 255); DimC = Color.FromArgb(255, 165, 148, 190); NeonViolet = t.Violet; NeonCyan = t.Cyan; NeonPink = t.Pink; NeonLime = t.Lime; Accent = t.Accent; Accent2 = t.Accent2; Violet = t.Btn; BtnC = t.BtnC;
        Edge = Color.FromArgb(120, t.EdgeC); Panel = Color.FromArgb(235, t.Panel); Win.NeonHot = t.Cyan; Win.NeonIdle = t.Violet; themeKeyAt = Environment.TickCount ^ 0x5A5A5A;   // (the button pictures are looked up for these colours)
        return s;
    }
    void EndBrand(LookSave s)
    {
        Ink = s.Ink; DimC = s.DimC; NeonViolet = s.NV; NeonCyan = s.NC; NeonPink = s.NP; NeonLime = s.NL; Accent = s.Ac; Accent2 = s.Ac2; Violet = s.Vi; BtnC = s.Btn; Edge = s.Ed; Panel = s.Pn; Win.NeonHot = s.Hot; Win.NeonIdle = s.Idle;
        UiTokens.Custom = s.Custom; TM.Brand = false; themeKeyAt = Environment.TickCount ^ 0x3C3C3C;
    }
    internal bool BrandOn { get { return TM.Brand; } }

    // a plugin slot in the Purphex look: a deep violet-to-magenta glass bar with a neon edge that lights up with the plugin's work
    internal void PaintPxSlot(Graphics g, RectangleF row, Fx f, bool open)
    {
        float lvl; fxvLvl.TryGetValue(f, out lvl);
        Color edge = f.Bypass ? DimC : (open ? UiTokens.Sel(VzC) : FvMix(VzV, VzP, lvl));
        using (var p = RR(row, 6))
        {
            using (var b = new LinearGradientBrush(row, f.Bypass ? TM.C(170, 34, 26, 46) : TM.C(230, 52, 18, 110), f.Bypass ? TM.C(170, 26, 20, 36) : TM.C(230, 120, 20, 112), 0f)) g.FillPath(b, p);
            using (var b = new LinearGradientBrush(row, Color.FromArgb(60, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f)) g.FillRectangle(b, row.X, row.Y, row.Width, row.Height * 0.45f);   // the glass shine on top
        }
        if (!f.Bypass && lvl > 0.05f) Glow(g, edge, row, 6, 0.25f + 0.75f * lvl, 3);
        StrokeRR(g, Color.FromArgb(f.Bypass ? 90 : 220, edge), row, 6, open ? 1.6f : 1.1f);
        using (var b = new SolidBrush(Color.FromArgb(f.Bypass ? 70 : 230, edge))) g.FillRectangle(b, row.X + 1.5f, row.Y + 4, 2, row.Height - 8);   // the neon tick on the left
    }
}
