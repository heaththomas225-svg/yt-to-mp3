using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;

// =====================================================================================================
//  THE EFFECT DISPLAY in a plugin window: what the effect really does, drawn from its own settings (it moves as you turn a knob), with the
//  live sound on top while the song plays. A reverb shows its tail (pre-delay, the early reflections, how long it rings and how dark it gets),
//  a delay its echoes, a compressor or limiter its curve (where it starts, how hard it squeezes, and the sound going through it), an EQ or
//  filter its frequency curve, a drive its shaping curve and the wave it makes, a chorus / pitch effect its movement, the rest the level.
// =====================================================================================================
partial class StudioForm
{
    static readonly Color ScCyan = Color.FromArgb(255, 61, 217, 255), ScPink = Color.FromArgb(255, 255, 92, 200), ScViolet = Color.FromArgb(255, 176, 92, 255), ScGold = Color.FromArgb(255, 255, 206, 84);
    static readonly Color ScGrid = Color.FromArgb(34, 190, 170, 255), ScGridHi = Color.FromArgb(70, 190, 170, 255), ScLabel = Color.FromArgb(255, 150, 136, 185);

    // a setting found by its id, or else by a word in its name
    static Param ScP(Fx f, params string[] keys)
    {
        foreach (var k in keys) foreach (var p in f.P) if (p.Id == k && !p.IsChoice) return p;
        foreach (var k in keys) foreach (var p in f.P) if (!p.IsChoice && p.Name.ToLowerInvariant().Contains(k)) return p;
        return null;
    }
    static float ScFrac(Param p, float d) { return p == null || p.Max <= p.Min ? d : (p.Val - p.Min) / (p.Max - p.Min); }
    static float ScPct(Param p, float d) { if (p == null) return d; return p.Unit == "%" ? p.Val / 100f : ScFrac(p, d); }
    static string ScNum(double v, string f) { return v.ToString(f, CultureInfo.InvariantCulture); }

    internal void PaintFxScope(Graphics g, RectangleF b, Fx f)
    {
        if (b.Width < 40 || b.Height < 30) return;
        var gs = g.Save(); var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var p = RR(b, 10)) { using (var br = new LinearGradientBrush(b, Color.FromArgb(255, 7, 4, 20), Color.FromArgb(255, 17, 8, 38), 90f)) g.FillPath(br, p); g.SetClip(p, CombineMode.Intersect); }
        float lvl = VizLevel(f); float[] hist; fxvHist.TryGetValue(f, out hist); if (hist == null) hist = new float[48];
        var plot = Rc(b.X + 12, b.Y + 22, b.Width - 24, b.Height - 22 - 18);
        string title, info;
        string ty = (f.Type + " " + f.Title).ToLowerInvariant();
        if (ty.Contains("8d")) ScOrbit(g, plot, f, lvl, out title, out info);
        else if (ty.Contains("pitch corr") || ty.Contains("autotune") || ty.Contains("auto-tune") || f.Type == "pitch") ScSnap(g, plot, f, lvl, out title, out info);
        else if (ty.Contains("widen") || ty.Contains("imager") || ty.Contains("stereo")) ScField(g, plot, f, lvl, out title, out info);
        else if (ty.Contains("noise") || ty.Contains("denois")) ScNoise(g, plot, f, lvl, out title, out info);
        else if (ty.Contains("excit") || ty.Contains("harmonic")) ScHarm(g, plot, f, lvl, out title, out info);
        else switch (VizOf(f))
        {
            case VizKind.Ripples: ScReverb(g, plot, f, lvl, out title, out info); break;
            case VizKind.Echo: ScDelay(g, plot, f, lvl, out title, out info); break;
            case VizKind.Pump: ScDynamics(g, plot, f, lvl, out title, out info); break;
            case VizKind.Curve: ScEq(g, plot, f, lvl, hist, out title, out info); break;
            case VizKind.Crunch: ScDrive(g, plot, f, lvl, out title, out info); break;
            case VizKind.Bend: ScMod(g, plot, f, lvl, out title, out info); break;
            default: ScLevel(g, plot, f, lvl, hist, out title, out info); break;
        }
        Txt(g, title, 9, ScViolet, Rc(b.X + 12, b.Y + 4, b.Width * 0.5f, 16), StringAlignment.Near, true);
        Txt(g, info, 9, ScCyan, Rc(b.X + b.Width * 0.3f, b.Y + 4, b.Width * 0.7f - 12, 16), StringAlignment.Far, true);
        if (f.Bypass) { using (var dim = new SolidBrush(Color.FromArgb(150, 6, 3, 16))) g.FillRectangle(dim, b); Txt(g, "OFF (BYPASSED)", 11, ScLabel, b, StringAlignment.Center, true); }
        g.Restore(gs); g.SmoothingMode = sm;
        using (var p = RR(b, 10)) using (var pen = new Pen(Color.FromArgb(200, ScViolet), 1.3f)) g.DrawPath(pen, p);
        if (eng.Playing && !UiTokens.ReducedMotion) lastInput = DateTime.Now;   // (keeps it moving while the song plays)
    }

    // ---- drawing helpers: a glowing line, a soft fill under it, grid lines with labels ----
    static void ScGlow(Graphics g, PointF[] pts, Color c, float w)
    {
        if (pts.Length < 2) return;
        using (var p1 = new Pen(Color.FromArgb(36, c), w + 6) { LineJoin = LineJoin.Round }) g.DrawLines(p1, pts);
        using (var p2 = new Pen(Color.FromArgb(90, c), w + 2.5f) { LineJoin = LineJoin.Round }) g.DrawLines(p2, pts);
        using (var p3 = new Pen(c, w) { LineJoin = LineJoin.Round }) g.DrawLines(p3, pts);
    }
    static void ScFill(Graphics g, PointF[] pts, float baseY, RectangleF r, Color c, int a)
    {
        if (pts.Length < 2) return; var poly = new List<PointF>(pts); poly.Add(new PointF(pts[pts.Length - 1].X, baseY)); poly.Add(new PointF(pts[0].X, baseY));
        float top = r.Y, bot = r.Bottom; if (Math.Abs(bot - top) < 1) bot = top + 1;
        using (var br = new LinearGradientBrush(new RectangleF(r.X, top - 1, r.Width, bot - top + 2), Color.FromArgb(a, c), Color.FromArgb(4, c), 90f)) g.FillPolygon(br, poly.ToArray());
    }
    void ScHLine(Graphics g, RectangleF r, float y, string label, bool hi)
    {
        using (var pen = new Pen(hi ? ScGridHi : ScGrid, 1f)) g.DrawLine(pen, r.X, y, r.Right, y);
        if (label != null) Txt(g, label, 7.5f, ScLabel, Rc(r.X + 2, y - 12, 60, 11), StringAlignment.Near, false);
    }
    void ScVLine(Graphics g, RectangleF r, float x, string label, bool hi)
    {
        using (var pen = new Pen(hi ? ScGridHi : ScGrid, 1f)) g.DrawLine(pen, x, r.Y, x, r.Bottom);
        if (label != null) Txt(g, label, 7.5f, ScLabel, Rc(x - 30, r.Bottom + 2, 60, 12), StringAlignment.Center, false);
    }
    static float ScHash(int i) { unchecked { uint h = (uint)i * 2654435761u; h ^= h >> 13; h *= 1274126177u; h ^= h >> 16; return (h & 0xFFFF) / 65535f; } }
    static double ScNice(double v) { double[] st = { 0.05, 0.1, 0.2, 0.25, 0.5, 1, 2, 2.5, 5, 10 }; foreach (var s in st) if (v / s <= 6) return s; return 10; }
    double ScPhase { get { return UiTokens.ReducedMotion ? 0 : (Environment.TickCount % 1000000) / 1000.0; } }

    // ---- reverb: its tail ----
    void ScReverb(Graphics g, RectangleF r, Fx f, float lvl, out string title, out string info)
    {
        var pDec = ScP(f, "decay", "time", "length"); var pSize = ScP(f, "size", "room", "revsize"); double rt;
        if (pDec != null && pDec.Unit == "s") rt = pDec.Val; else if (pDec != null && pDec.Unit == "ms") rt = pDec.Val / 1000.0; else rt = 0.3 + 5.7 * ScPct(pDec ?? pSize, 0.5f);
        rt = Math.Max(0.1, rt); double pre = Math.Max(0, (ScP(f, "pre") ?? new Param("", "", 0, 0, 0, "ms")).Val / 1000.0); float damp = ScPct(ScP(f, "damp"), 0.4f), mix = ScPct(ScP(f, "mix", "wet"), 0.35f);
        double span = pre + rt * 1.15; double step = ScNice(span); span = Math.Ceiling(span / step) * step;
        Func<double, float> X = t => r.X + (float)(t / span * r.Width); float cy = r.Y + r.Height / 2, hh = r.Height / 2 - 2;
        for (double t = 0; t <= span + 1e-6; t += step) ScVLine(g, r, X(t), (t == 0 ? "0" : ScNum(t, t < 1 ? "0.##" : "0.#")) + (t == 0 ? "" : " s"), t == 0);
        ScHLine(g, r, cy, null, true);
        // the dry hit
        using (var pen = new Pen(Color.FromArgb(230, 255, 255, 255), 2f)) g.DrawLine(pen, X(0), cy - hh * 0.95f, X(0), cy + hh * 0.95f);
        // the early reflections, then the tail (60 dB down at the decay time); darker and smoother as damping goes up
        float wk = 0.35f + 0.65f * mix; var env = new List<PointF>(); var envB = new List<PointF>();
        for (int i = 0; i < 9; i++) { double t = pre + 0.004 + ScHash(i + 7) * 0.07; float a = hh * wk * (0.95f - i * 0.06f); float x = X(t); using (var pen = new Pen(Color.FromArgb(220, ScCyan), 1.6f)) g.DrawLine(pen, x, cy - a, x, cy + a); }
        float t0x = X(pre + 0.02); int n = 0;
        for (float x = t0x; x <= r.Right; x += 1.5f, n++)
        {
            double t = (x - r.X) / r.Width * span, k = (t - pre) / rt; if (k < 0) continue; double e = Math.Pow(10, -3 * k) ; float ed = (float)Math.Max(0, 1 - k);   // (drawn on a dB scale: the line reaches the middle at the decay time)
            float build = (float)Math.Min(1, (t - pre) / 0.035); float a = hh * wk * (float)Math.Max(0, Math.Exp(-3.2 * k) - 0.04 * k) * (0.35f + 0.65f * build); float noise = 0.55f + 0.45f * (damp > 0.7f ? ScHash(n / 3) : ScHash(n)); float yy = a * noise;
            Color c = FvMix(ScCyan, damp > 0.5f ? ScViolet : ScPink, (float)Math.Min(1, k * (0.5 + damp * 1.5)));
            using (var pen = new Pen(Color.FromArgb(Math.Max(30, (int)(200 * (0.35 + 0.65 * ed))), c), 1.4f)) g.DrawLine(pen, x, cy - yy, x, cy + yy);
            env.Add(new PointF(x, cy - a)); envB.Add(new PointF(x, cy + a)); if (ed <= 0 && k > 1.2) break;
        }
        ScGlow(g, env.ToArray(), ScCyan, 1.6f); ScGlow(g, envB.ToArray(), ScViolet, 1.2f);
        if (pre > 0.002) { float px = X(pre); using (var pen = new Pen(Color.FromArgb(160, ScGold), 1f) { DashStyle = DashStyle.Dash }) g.DrawLine(pen, px, r.Y, px, r.Bottom); }
        // the live sound: a light sweeping along the tail at real speed, as bright as the reverb is working
        if (eng.Playing && lvl > 0.01f) { double ph = ScPhase % span; float sx = X(ph); using (var br = new LinearGradientBrush(new RectangleF(sx - 40, r.Y, 41, r.Height), Color.FromArgb(0, ScCyan), Color.FromArgb((int)(40 + 140 * lvl), ScCyan), 0f)) g.FillRectangle(br, sx - 40, r.Y, 40, r.Height); using (var pen = new Pen(Color.FromArgb((int)(120 + 135 * lvl), Color.White), 1.4f)) g.DrawLine(pen, sx, r.Y, sx, r.Bottom); }
        title = "REVERB TAIL"; info = (pre > 0.0005 ? "PRE-DELAY " + ScNum(pre * 1000, "0") + " ms  \u00B7  " : "") + "DECAY " + ScNum(rt, rt < 10 ? "0.0" : "0") + " s  \u00B7  MIX " + ScNum(mix * 100, "0") + "%";
    }

    // ---- delay: its echoes ----
    double ScDelayTime(Fx f)
    {
        var pt = ScP(f, "time", "delay", "fbt"); if (pt != null && pt.Unit == "ms" && pt.Val > 0) { var sync = ScChoice(f, "sync", "note", "division", "rate"); if (sync == null || sync.Text.ToLowerInvariant().Contains("free") || sync.Text.ToLowerInvariant().Contains("off")) return pt.Val / 1000.0; }
        var ch = ScChoice(f, "note", "division", "sync", "rate", "time"); if (ch != null) { double s = ScNoteSec(ch.Text); if (s > 0) return s; }
        return pt != null && pt.Unit == "ms" ? Math.Max(0.01, pt.Val / 1000.0) : 60.0 / Fx.ProjectBpm / 2;
    }
    static Param ScChoice(Fx f, params string[] keys) { foreach (var k in keys) foreach (var p in f.P) if (p.IsChoice && (p.Id == k || p.Name.ToLowerInvariant().Contains(k))) return p; return null; }
    static double ScNoteSec(string t)
    {
        t = t.ToLowerInvariant(); var m = System.Text.RegularExpressions.Regex.Match(t, @"1\s*/\s*(\d+)"); if (!m.Success) return 0; double beats = 4.0 / int.Parse(m.Groups[1].Value);
        if (t.Contains("dot") || t.EndsWith("d") || t.Contains(".")) beats *= 1.5; if (t.Contains("trip") || t.EndsWith("t")) beats *= 2.0 / 3; return beats * 60.0 / Fx.ProjectBpm;
    }
    void ScDelay(Graphics g, RectangleF r, Fx f, float lvl, out string title, out string info)
    {
        double dt = ScDelayTime(f); float fb = ScPct(ScP(f, "fb", "feedback"), 0.4f), mix = ScPct(ScP(f, "mix", "wet", "echo"), 0.35f); bool ping = (f.Type + f.Title).ToLowerInvariant().Contains("ping") || ScChoice(f, "ping", "mode") != null && ScChoice(f, "ping", "mode").Text.ToLowerInvariant().Contains("ping");
        int reps = 1; { double a = 1; while (reps < 16 && a > 0.04) { a *= Math.Max(0.01, fb); reps++; } }
        double span = dt * (reps + 0.6); double step = ScNice(span); span = Math.Ceiling(span / step) * step; Func<double, float> X = t => r.X + (float)(t / span * r.Width); float cy = r.Y + r.Height / 2, hh = r.Height / 2 - 2;
        for (double t = 0; t <= span + 1e-6; t += step) ScVLine(g, r, X(t), t == 0 ? "0" : (span < 2 ? ScNum(t * 1000, "0") + " ms" : ScNum(t, "0.#") + " s"), t == 0);
        ScHLine(g, r, cy, null, true);
        Action<float, float, Color, bool> burst = (x, a, c, up) => { var pts = new List<PointF>(); float w = Math.Max(10, Math.Min(36, (float)(dt / span * r.Width) * 0.7f)); for (float i = 0; i <= w; i += 1f) { float env = (float)Math.Exp(-i / (w * 0.35f)); float s = (float)Math.Sin(i * 1.1f) * env; pts.Add(new PointF(x + i, cy - s * a * (up ? 1 : -1))); } ScGlow(g, pts.ToArray(), c, 1.3f); using (var pen = new Pen(Color.FromArgb(120, c), 1f)) g.DrawLine(pen, x, cy - a, x, cy + a); };
        burst(X(0), hh * 0.95f, Color.White, true);
        var tops = new List<PointF>(); double amp = Math.Max(0.15, mix);
        for (int i = 1; i < reps; i++) { float a = (float)(hh * 0.92 * amp); var c = ping ? (i % 2 == 1 ? ScCyan : ScPink) : FvMix(ScCyan, ScViolet, i / (float)reps); burst(X(dt * i), a, c, !ping || i % 2 == 1); tops.Add(new PointF(X(dt * i), cy - a)); amp *= Math.Max(0.01, fb); }
        if (tops.Count > 1) using (var pen = new Pen(Color.FromArgb(110, ScGold), 1f) { DashStyle = DashStyle.Dot }) g.DrawLines(pen, tops.ToArray());
        if (eng.Playing && lvl > 0.01f) { double ph = ScPhase % span; float sx = X(ph); using (var pen = new Pen(Color.FromArgb((int)(100 + 155 * lvl), Color.White), 1.4f)) g.DrawLine(pen, sx, r.Y, sx, r.Bottom); }
        title = ping ? "PING-PONG ECHOES" : "ECHOES"; info = "TIME " + (dt < 2 ? ScNum(dt * 1000, "0") + " ms" : ScNum(dt, "0.0") + " s") + "  \u00B7  FEEDBACK " + ScNum(fb * 100, "0") + "%  \u00B7  " + Math.Max(0, reps - 1) + " REPEATS";
    }

    // ---- compressor / limiter / gate: its curve ----
    void ScDynamics(Graphics g, RectangleF r, Fx f, float lvl, out string title, out string info)
    {
        string t = (f.Type + " " + f.Title).ToLowerInvariant(); bool gate = t.Contains("gate"), limit = t.Contains("limit") || t.Contains("clip") || t.Contains("loud");
        var pThr = ScP(f, "thr", "threshold", "ceiling", "gate", "floor"); float thr = pThr != null && pThr.Unit == "dB" ? pThr.Val : (pThr != null ? -40 * ScFrac(pThr, 0.5f) : -18);
        var pRat = ScP(f, "ratio"); float ratio = pRat != null ? Math.Max(1, pRat.Val) : (limit ? 50 : (gate ? 1 : 4)); float knee = Math.Max(0, (ScP(f, "knee") ?? new Param("", "", 0, 0, limit ? 0 : 3, "dB")).Val);
        float lo = -60, hi = 0; var sq = r; float side = Math.Min(r.Width - 90, r.Height); if (side > 40) sq = Rc(r.X + 22, r.Y, side, r.Height);
        Func<float, float> X = db => sq.X + (db - lo) / (hi - lo) * sq.Width; Func<float, float> Y = db => sq.Bottom - (db - lo) / (hi - lo) * sq.Height;
        for (int d = -60; d <= 0; d += 12) { ScVLine(g, sq, X(d), d % 24 == 0 ? d.ToString() : null, d == 0); ScHLine(g, sq, Y(d), null, d == 0); if (d % 24 == 0 && d < 0) Txt(g, d.ToString(), 7.5f, ScLabel, Rc(sq.X - 24, Y(d) - 6, 22, 12), StringAlignment.Far, false); }
        using (var pen = new Pen(Color.FromArgb(70, 255, 255, 255), 1f) { DashStyle = DashStyle.Dash }) g.DrawLine(pen, X(lo), Y(lo), X(hi), Y(hi));
        Func<float, float> curve = x =>
        {
            if (gate) { if (x >= thr) return x; return Math.Max(lo - 10, thr + (x - thr) * 4); }
            float over = x - thr; if (knee > 0 && Math.Abs(over) <= knee / 2) { float k = over + knee / 2; return x + (1 / ratio - 1) * k * k / (2 * knee); }
            return over <= 0 ? x : thr + over / ratio;
        };
        var pts = new List<PointF>(); for (float x = lo; x <= hi + 0.01f; x += 0.5f) pts.Add(new PointF(X(x), Math.Min(sq.Bottom, Y(curve(x)))));
        ScFill(g, pts.ToArray(), sq.Bottom, sq, ScViolet, 70); ScGlow(g, pts.ToArray(), ScCyan, 2f);
        using (var pen = new Pen(Color.FromArgb(150, ScGold), 1f) { DashStyle = DashStyle.Dash }) g.DrawLine(pen, X(thr), sq.Y, X(thr), sq.Bottom);
        // the sound going through it right now: a dot on the curve, and how much it is squeezed
        if (eng.Playing && f.OutLvl > 0.0005f) { float outDb = (float)(20 * Math.Log10(Math.Max(1e-5, f.OutLvl))) + 3, inDb = Math.Max(lo, Math.Min(hi, outDb + f.GR)); float dx = X(inDb), dy = Y(curve(inDb)); using (var gl = new SolidBrush(Color.FromArgb(70, ScPink))) g.FillEllipse(gl, dx - 9, dy - 9, 18, 18); using (var dot = new SolidBrush(Color.White)) g.FillEllipse(dot, dx - 3.5f, dy - 3.5f, 7, 7); }
        if (sq.Right + 120 < r.Right)
        {   // how much it squeezed over the last few seconds, hanging from the top (the newest on the right)
            var hr = Rc(sq.Right + 26, r.Y, r.Right - 64 - sq.Right - 26, r.Height); float[] gh; if (!scGr.TryGetValue(f, out gh)) { if (scGr.Count > 200) { scGr.Clear(); scGrT.Clear(); } gh = new float[120]; scGr[f] = gh; }
            int nowT = Environment.TickCount, lastT; scGrT.TryGetValue(f, out lastT); if (nowT - lastT >= 33) { scGrT[f] = nowT; Array.Copy(gh, 1, gh, 0, gh.Length - 1); gh[gh.Length - 1] = eng.Playing ? f.GR : 0; }
            for (int d = 0; d <= 24; d += 6) { float yy = hr.Y + hr.Height * d / 24f; ScHLine(g, hr, yy, d == 0 ? null : "-" + d, d == 0); }
            var gp = new List<PointF>(); for (int i = 0; i < gh.Length; i++) gp.Add(new PointF(hr.X + hr.Width * i / (gh.Length - 1), hr.Y + hr.Height * Math.Min(24, gh[i]) / 24f));
            var fillP = new List<PointF>(gp); fillP.Add(new PointF(hr.Right, hr.Y)); fillP.Add(new PointF(hr.X, hr.Y)); using (var br = new LinearGradientBrush(new RectangleF(hr.X, hr.Y - 1, hr.Width, hr.Height + 2), Color.FromArgb(110, ScPink), Color.FromArgb(10, ScPink), 90f)) g.FillPolygon(br, fillP.ToArray());
            ScGlow(g, gp.ToArray(), ScPink, 1.4f); Txt(g, "SQUEEZE OVER TIME", 7.5f, ScLabel, Rc(hr.X, hr.Bottom - 12, hr.Width, 11), StringAlignment.Center, true);
        }
        if (sq.Right + 50 < r.Right)
        {   // the gain reduction meter, hanging from the top
            var gm = Rc(r.Right - 44, r.Y, 18, r.Height); FillRR(g, Color.FromArgb(255, 10, 5, 26), gm, 5); using (var pen = new Pen(Color.FromArgb(90, ScViolet), 1f)) g.DrawRectangle(pen, gm.X, gm.Y, gm.Width, gm.Height);
            float gr = Math.Min(24, f.GR); float gh = gm.Height * gr / 24f; if (gh > 0.5f) using (var br = new LinearGradientBrush(new RectangleF(gm.X, gm.Y, gm.Width, Math.Max(1, gh)), ScPink, ScGold, 90f)) g.FillRectangle(br, gm.X + 2, gm.Y + 1, gm.Width - 4, gh);
            for (int d = 6; d < 24; d += 6) { float yy = gm.Y + gm.Height * d / 24f; using (var pen = new Pen(Color.FromArgb(60, 255, 255, 255), 1f)) g.DrawLine(pen, gm.X, yy, gm.Right, yy); }
            Txt(g, "GR", 7.5f, ScLabel, Rc(gm.Right + 2, gm.Y, 30, 11), StringAlignment.Near, true); Txt(g, gr > 0.05f ? "-" + ScNum(gr, "0.0") : "0", 8, ScGold, Rc(gm.Right + 1, gm.Bottom - 12, 30, 12), StringAlignment.Near, true);
        }
        title = gate ? "GATE CURVE" : (limit ? "LIMITER CURVE" : "COMPRESSION CURVE"); info = "THRESHOLD " + ScNum(thr, "0.#") + " dB" + (gate ? "" : "  \u00B7  RATIO " + (ratio >= 30 ? "BRICKWALL" : ScNum(ratio, "0.#") + ":1"));
    }

    readonly Dictionary<Fx, float[]> scGr = new Dictionary<Fx, float[]>(); readonly Dictionary<Fx, int> scGrT = new Dictionary<Fx, int>();

    // ---- 8D: the sound going round your head ----
    void ScOrbit(Graphics g, RectangleF r, Fx f, float lvl, out string title, out string info)
    {
        var pRate = ScP(f, "rate", "speed"); float depth = ScPct(ScP(f, "depth", "width", "amount"), 0.7f); float cx = r.X + r.Width / 2, cy = r.Y + r.Height / 2, ry = r.Height / 2 - 4, rx = Math.Min(r.Width / 2 - 30, ry * 2.2f);
        for (int i = 1; i <= 3; i++) using (var pen = new Pen(i == 3 ? ScGridHi : ScGrid, 1f)) g.DrawEllipse(pen, cx - rx * i / 3, cy - ry * i / 3, rx * 2 * i / 3, ry * 2 * i / 3);
        Txt(g, "L", 9, ScLabel, Rc(cx - rx - 22, cy - 7, 16, 14), StringAlignment.Center, true); Txt(g, "R", 9, ScLabel, Rc(cx + rx + 6, cy - 7, 16, 14), StringAlignment.Center, true); Txt(g, "FRONT", 7.5f, ScLabel, Rc(cx - 30, cy + ry - 12, 60, 11), StringAlignment.Center, true); Txt(g, "BEHIND", 7.5f, ScLabel, Rc(cx - 30, r.Y, 60, 11), StringAlignment.Center, true);
        using (var head = new SolidBrush(Color.FromArgb(200, 40, 24, 70))) g.FillEllipse(head, cx - 14, cy - 16, 28, 32); using (var pen = new Pen(Color.FromArgb(200, ScViolet), 1.4f)) g.DrawEllipse(pen, cx - 14, cy - 16, 28, 32);
        double hz = pRate == null ? 0.2 : (pRate.Unit == "Hz" ? pRate.Val : 0.05 + 1.5 * ScFrac(pRate, 0.2f)); double a = (eng.Playing ? ScPhase : 0.7) * hz * Math.PI * 2; float orx = rx * (0.4f + 0.6f * depth), ory = ry * (0.4f + 0.6f * depth);
        var trail = new List<PointF>(); for (int i = 40; i >= 0; i--) { double aa = a - i * 0.05; trail.Add(new PointF(cx + (float)Math.Sin(aa) * orx, cy + (float)Math.Cos(aa) * ory)); } ScGlow(g, trail.ToArray(), ScCyan, 1.4f);
        float sx = trail[trail.Count - 1].X, sy = trail[trail.Count - 1].Y; using (var gl = new SolidBrush(Color.FromArgb(90, ScPink))) g.FillEllipse(gl, sx - 11, sy - 11, 22, 22); using (var dot = new SolidBrush(Color.White)) g.FillEllipse(dot, sx - 4.5f, sy - 4.5f, 9, 9);
        title = "AROUND YOUR HEAD"; info = (pRate != null ? "SPEED " + pRate.Text + "  \u00B7  " : "") + "WIDTH " + ScNum(depth * 100, "0") + "%";
    }

    // ---- pitch correction: the sung pitch (faint) pulled onto the notes of the scale (bright); a faster speed snaps harder ----
    void ScSnap(Graphics g, RectangleF r, Fx f, float lvl, out string title, out string info)
    {
        var pSp = ScP(f, "speed", "retune", "rate"); float hard = pSp == null ? 0.7f : 1 - ScFrac(pSp, 0.3f); var pAm = ScP(f, "amount", "strength"); float amt = ScPct(pAm, 1f);
        int notes = 6; float rowH = r.Height / notes; for (int i = 0; i <= notes; i++) ScHLine(g, r, r.Y + i * rowH, null, false);
        double ph = eng.Playing ? ScPhase * 0.6 : 0; var sung = new List<PointF>(); var out1 = new List<PointF>(); double held = 3;
        for (float x = 0; x <= r.Width; x += 1.5f)
        {
            double t = x / r.Width * 4 + ph; double p = 3 + 1.6 * Math.Sin(t * 1.3) + 0.6 * Math.Sin(t * 3.7) + 0.18 * Math.Sin(t * 23);   // a sung line: slides, vibrato and wobble
            double target = Math.Round(p); held += (target - held) * (0.04 + 0.6 * hard * hard); double o = p + (held - p) * amt;
            sung.Add(new PointF(r.X + x, r.Bottom - (float)(p / notes * r.Height))); out1.Add(new PointF(r.X + x, r.Bottom - (float)(o / notes * r.Height)));
        }
        using (var pen = new Pen(Color.FromArgb(90, 255, 255, 255), 1.2f)) g.DrawLines(pen, sung.ToArray()); ScGlow(g, out1.ToArray(), ScCyan, 2f);
        title = "PITCH CORRECTION"; info = (pSp != null ? "SPEED " + pSp.Text + "  \u00B7  " : "") + (hard > 0.8f ? "HARD (ROBOTIC)" : hard > 0.45f ? "MEDIUM" : "NATURAL");
    }

    // ---- stereo imager: how wide the sound is ----
    void ScField(Graphics g, RectangleF r, Fx f, float lvl, out string title, out string info)
    {
        var pw = ScP(f, "width", "stereo", "spread"); float w = pw == null ? 1 : (pw.Unit == "%" ? pw.Val / 100f : ScFrac(pw, 0.5f) * 2); float cx = r.X + r.Width / 2, by = r.Bottom - 2, R0 = Math.Min(r.Height - 6, r.Width / 2 - 20);
        for (int i = 1; i <= 3; i++) using (var pen = new Pen(i == 3 ? ScGridHi : ScGrid, 1f)) g.DrawArc(pen, cx - R0 * i / 3, by - R0 * i / 3, R0 * 2 * i / 3, R0 * 2 * i / 3, 180, 180);
        foreach (int deg in new[] { 45, 90, 135 }) { double a = Math.PI * deg / 180; using (var pen = new Pen(ScGrid, 1f)) g.DrawLine(pen, cx, by, cx - (float)Math.Cos(a) * R0, by - (float)Math.Sin(a) * R0); }
        Txt(g, "L", 9, ScLabel, Rc(cx - R0 - 16, by - 14, 14, 14), StringAlignment.Center, true); Txt(g, "R", 9, ScLabel, Rc(cx + R0 + 2, by - 14, 14, 14), StringAlignment.Center, true); Txt(g, "MONO", 7.5f, ScLabel, Rc(cx - 20, by - R0 - 12, 40, 11), StringAlignment.Center, true);
        float spread = (float)(Math.Min(2, Math.Max(0, w)) * 45); float pulse = eng.Playing ? 0.85f + 0.15f * Math.Min(1, lvl * 2) : 1f;
        using (var path = new GraphicsPath()) { path.AddPie(cx - R0 * pulse, by - R0 * pulse, R0 * 2 * pulse, R0 * 2 * pulse, 270 - spread, Math.Max(1, spread * 2)); using (var br = new PathGradientBrush(path) { CenterPoint = new PointF(cx, by), CenterColor = Color.FromArgb(180, ScCyan), SurroundColors = new[] { Color.FromArgb(30, ScViolet) } }) g.FillPath(br, path); using (var pen = new Pen(Color.FromArgb(220, ScCyan), 1.6f)) g.DrawPath(pen, path); }
        title = "STEREO FIELD"; info = "WIDTH " + ScNum(w * 100, "0") + "%" + (w < 0.05 ? "  \u00B7  MONO" : w > 1.05 ? "  \u00B7  EXTRA WIDE" : "");
    }

    // ---- noise reduction: the hiss under the line is taken away ----
    void ScNoise(Graphics g, RectangleF r, Fx f, float lvl, out string title, out string info)
    {
        var pr = ScP(f, "red", "reduction", "strength", "amount"); float amt = pr == null ? 0.5f : (pr.Unit == "dB" ? Math.Min(1, Math.Abs(pr.Val) / 30f) : ScPct(pr, 0.5f)); var pf = ScP(f, "floor", "thr", "threshold", "sens");
        float floorY = r.Bottom - r.Height * (0.25f + 0.2f * (pf == null ? 0.5f : ScFrac(pf, 0.5f)));
        double fl = Math.Log10(20), fh = Math.Log10(20000); foreach (var hz in new[] { 100, 1000, 10000 }) ScVLine(g, r, r.X + (float)((Math.Log10(hz) - fl) / (fh - fl) * r.Width), hz == 1000 ? "1k" : hz == 10000 ? "10k" : "100", false);
        double ph = eng.Playing ? ScPhase * 8 : 0; var before = new List<PointF>(); var after = new List<PointF>();
        for (float x = 0; x <= r.Width; x += 2)
        {
            double t = x / r.Width; float voice = (float)(Math.Exp(-Math.Pow((t - 0.42) / 0.16, 2)) * 0.75 + Math.Exp(-Math.Pow((t - 0.62) / 0.08, 2)) * 0.35); float hiss = 0.12f + 0.1f * ScHash((int)(x / 2) + (int)ph * 97);
            float yB = r.Bottom - r.Height * (voice + hiss * 0.9f); float yA = yB > floorY ? yB + (r.Bottom - yB) * amt : r.Bottom - r.Height * voice;
            before.Add(new PointF(r.X + x, yB)); after.Add(new PointF(r.X + x, Math.Max(yB, yA)));
        }
        using (var br = new SolidBrush(Color.FromArgb(40, ScPink))) g.FillRectangle(br, r.X, floorY, r.Width, r.Bottom - floorY); using (var pen = new Pen(Color.FromArgb(170, ScGold), 1f) { DashStyle = DashStyle.Dash }) g.DrawLine(pen, r.X, floorY, r.Right, floorY);
        Txt(g, "NOISE FLOOR", 7.5f, ScGold, Rc(r.Right - 80, floorY - 12, 78, 11), StringAlignment.Far, true);
        using (var pen = new Pen(Color.FromArgb(90, 255, 255, 255), 1f)) g.DrawLines(pen, before.ToArray()); ScFill(g, after.ToArray(), r.Bottom, r, ScCyan, 70); ScGlow(g, after.ToArray(), ScCyan, 1.6f);
        title = "NOISE REMOVED"; info = pr != null ? Win.Up(pr.Name) + " " + pr.Text : "";
    }

    // ---- exciter: new harmonics on top of the sound ----
    void ScHarm(Graphics g, RectangleF r, Fx f, float lvl, out string title, out string info)
    {
        var pa = ScP(f, "amount", "drive", "mix", "excite"); float amt = ScPct(pa, 0.4f); var pfq = ScP(f, "freq", "tune", "from"); int n = 12; float bw = r.Width / n;
        for (int i = 1; i < 4; i++) ScHLine(g, r, r.Y + r.Height * i / 4, null, false);
        for (int i = 0; i < n; i++)
        {
            float orig = (float)Math.Max(0.05, 0.9 * Math.Pow(0.7, i)); float added = i == 0 ? 0 : (float)(amt * 0.55 * Math.Pow(0.88, i)) * (eng.Playing ? 0.8f + 0.2f * ScHash(i + (int)(ScPhase * 12)) : 1f);
            float x = r.X + i * bw + bw * 0.2f, w = bw * 0.6f; float h1 = r.Height * orig, h2 = r.Height * Math.Min(1 - orig, added);
            using (var p = RR(Rc(x, r.Bottom - h1, w, h1), 3)) using (var br = new SolidBrush(Color.FromArgb(150, ScViolet))) g.FillPath(br, p);
            if (h2 > 1) using (var p = RR(Rc(x, r.Bottom - h1 - h2, w, h2), 3)) { using (var br = new LinearGradientBrush(Rc(x, r.Bottom - h1 - h2 - 1, w, h2 + 2), ScGold, ScPink, 90f)) g.FillPath(br, p); }
            Txt(g, i == 0 ? "1st" : (i + 1).ToString(), 7.5f, ScLabel, Rc(x - 4, r.Bottom + 2, w + 8, 11), StringAlignment.Center, false);
        }
        title = "HARMONICS ADDED"; info = (pa != null ? Win.Up(pa.Name) + " " + pa.Text : "") + (pfq != null ? "  \u00B7  FROM " + pfq.Text : "");
    }

    // ---- EQ / filter: its frequency curve ----
    void ScEq(Graphics g, RectangleF r, Fx f, float lvl, float[] hist, out string title, out string info)
    {
        double fl = Math.Log10(20), fh = Math.Log10(20000); Func<double, float> X = hz => r.X + (float)((Math.Log10(hz) - fl) / (fh - fl) * r.Width); const float range = 18; float cy = r.Y + r.Height / 2;
        Func<double, float> Y = db => cy - (float)(Math.Max(-range, Math.Min(range, db)) / range * (r.Height / 2 - 2));
        foreach (var hz in new[] { 50, 100, 200, 500, 1000, 2000, 5000, 10000 }) ScVLine(g, r, X(hz), hz == 100 ? "100" : hz == 1000 ? "1k" : hz == 10000 ? "10k" : null, hz == 1000);
        foreach (var db in new[] { -12, -6, 0, 6, 12 }) ScHLine(g, r, Y(db), db == 0 ? null : (db > 0 ? "+" : "") + db, db == 0);
        // the parts of the curve from the settings: low cut, high cut, and the boosts / cuts by name (bass, mid, presence, air, tilt...)
        var parts = new List<Func<double, double>>(); var used = new List<string>();
        foreach (var p in f.P)
        {
            if (p.IsChoice) continue; string nm = (p.Id + " " + p.Name).ToLowerInvariant();
            if (p.Unit == "Hz" && (nm.Contains("low cut") || p.Id == "lc" || p.Id == "hp" || p.Id == "lowcut" || nm.Contains("high-pass") || nm.Contains("highpass"))) { if (p.Val > p.Min + 0.5f) { double fc = p.Val; parts.Add(hz => { double w = hz / fc; return 20 * Math.Log10(w * w / Math.Sqrt((1 - w * w) * (1 - w * w) + w * w * 2)); }); used.Add("LOW CUT " + ScHz(fc)); } continue; }
            if (p.Unit == "Hz" && (nm.Contains("high cut") || p.Id == "hc" || p.Id == "lp" || nm.Contains("low-pass") || nm.Contains("lowpass"))) { if (p.Val < p.Max - 0.5f) { double fc = p.Val; parts.Add(hz => { double w = hz / fc; return 20 * Math.Log10(1 / Math.Sqrt((1 - w * w) * (1 - w * w) + w * w * 2)); }); used.Add("HIGH CUT " + ScHz(fc)); } continue; }
            if (p.Unit == "Hz" && (nm.Contains("cutoff") || p.Id == "cut" || p.Id == "freq")) { double fc = p.Val; var res = ScP(f, "res", "q", "reson"); double q = 0.7 + 6 * ScPct(res, 0); bool hpMode = (ScChoice(f, "type", "mode") != null && ScChoice(f, "type", "mode").Text.ToLowerInvariant().Contains("high")); parts.Add(hz => { double w = hz / fc; double num = hpMode ? w * w : 1; return 20 * Math.Log10(num / Math.Sqrt((1 - w * w) * (1 - w * w) + (w / q) * (w / q))); }); used.Add((hpMode ? "HIGH-PASS " : "CUTOFF ") + ScHz(fc)); continue; }
            if (p.Unit != "dB" || p.Id == "out" || p.Id == "in" || nm.Contains("output") || nm.Contains("input") || nm.Contains("level")) continue;
            double gdb = p.Val; if (Math.Abs(gdb) < 0.05) continue;
            if (nm.Contains("low") || nm.Contains("bass") || nm.Contains("warm") || nm.Contains("body")) { double fc = ScNear(f, p, 120); parts.Add(hz => gdb / (1 + Math.Pow(hz / fc, 2))); }
            else if (nm.Contains("air")) { double fc = ScNear(f, p, 12000); parts.Add(hz => gdb / (1 + Math.Pow(fc / hz, 2))); }
            else if (nm.Contains("high") || nm.Contains("treble") || nm.Contains("bright") || nm.Contains("top")) { double fc = ScNear(f, p, 6000); parts.Add(hz => gdb / (1 + Math.Pow(fc / hz, 2))); }
            else if (nm.Contains("tilt")) { parts.Add(hz => gdb * Math.Max(-1, Math.Min(1, Math.Log10(hz / 1000) / 1.3))); }
            else if (nm.Contains("presence") || nm.Contains("mid") || nm.Contains("clarity") || nm.Contains("gain") || nm.Contains("tone")) { double fc = ScNear(f, p, nm.Contains("presence") ? 4000 : 1000); double q = 0.9; double A = Math.Pow(10, gdb / 40); parts.Add(hz => { double w = hz / fc; double a1 = (1 - w * w); double num = a1 * a1 + Math.Pow(w * A / q, 2), den = a1 * a1 + Math.Pow(w / (A * q), 2); return 10 * Math.Log10(num / den); }); }
            else continue;
            used.Add(Win.Up(p.Name) + " " + (gdb > 0 ? "+" : "") + ScNum(gdb, "0.#"));
        }
        var pts = new List<PointF>(); for (float x = r.X; x <= r.Right; x += 2) { double hz = Math.Pow(10, fl + (x - r.X) / r.Width * (fh - fl)); double db = 0; foreach (var pf in parts) db += pf(hz); pts.Add(new PointF(x, Y(db))); }
        // the live sound: its level, as a soft glow under the curve
        if (eng.Playing && lvl > 0.01f) { int n = hist.Length; float bw = r.Width / n; for (int i = 0; i < n; i++) { float v = hist[i]; if (v < 0.01f) continue; float hh = r.Height * 0.45f * v * (0.6f + 0.4f * ScHash(i + (int)(ScPhase * 25))); using (var br = new SolidBrush(Color.FromArgb((int)(30 + 60 * v), ScViolet))) g.FillRectangle(br, r.X + i * bw, r.Bottom - hh, bw - 1, hh); } }
        ScFill(g, pts.ToArray(), cy, r, ScCyan, 70); ScGlow(g, pts.ToArray(), ScCyan, 2f);
        title = "FREQUENCY CURVE"; info = used.Count == 0 ? "FLAT" : string.Join("  \u00B7  ", used.GetRange(0, Math.Min(3, used.Count)).ToArray());
    }
    static string ScHz(double hz) { return hz >= 1000 ? ScNum(hz / 1000, hz >= 10000 ? "0" : "0.#") + " kHz" : ScNum(hz, "0") + " Hz"; }
    // the frequency setting that goes with a boost (the Hz knob just before or after it), or a usual one
    static double ScNear(Fx f, Param gain, double def) { int i = f.P.IndexOf(gain); foreach (int j in new[] { i - 1, i + 1 }) if (j >= 0 && j < f.P.Count && f.P[j].Unit == "Hz" && !f.P[j].IsChoice) return f.P[j].Val; return def; }

    // ---- drive / distortion / crush: the shaping curve and the wave it makes ----
    void ScDrive(Graphics g, RectangleF r, Fx f, float lvl, out string title, out string info)
    {
        var pd = ScP(f, "drive", "amount", "sat", "grit", "dist"); float k = pd == null ? 4 : (pd.Unit == "dB" ? (float)Math.Pow(10, pd.Val / 20) : 1 + 14 * ScFrac(pd, 0.3f));
        var pb = ScP(f, "bits", "bit", "crush"); int levels = 0; if (pb != null) { if (pb.Unit == "" || pb.Name.ToLowerInvariant().Contains("bit")) { int bits = (int)Math.Round(pb.Val); if (bits >= 1 && bits <= 16 && pb.Max <= 24) levels = 1 << Math.Max(1, bits - 1); else levels = (int)(2 + 30 * (1 - ScFrac(pb, 0.5f))); } }
        Func<double, double> shape = x => { double y = Math.Tanh(k * x) / Math.Tanh(k); if (levels > 0 && levels < 4096) y = Math.Round(y * levels) / levels; return y; };
        var sq = Rc(r.X + 4, r.Y, Math.Min(r.Height, r.Width * 0.38f), r.Height); float cx = sq.X + sq.Width / 2, cy = sq.Y + sq.Height / 2;
        ScVLine(g, sq, cx, null, true); ScHLine(g, sq, cy, null, true); ScVLine(g, sq, sq.X, null, false); ScVLine(g, sq, sq.Right, null, false); ScHLine(g, sq, sq.Y, null, false); ScHLine(g, sq, sq.Bottom, null, false);
        using (var pen = new Pen(Color.FromArgb(70, 255, 255, 255), 1f) { DashStyle = DashStyle.Dash }) g.DrawLine(pen, sq.X, sq.Bottom, sq.Right, sq.Y);
        var pts = new List<PointF>(); for (float i = 0; i <= sq.Width; i += 1) { double x = i / sq.Width * 2 - 1; pts.Add(new PointF(sq.X + i, cy - (float)shape(x) * (sq.Height / 2 - 2))); } ScGlow(g, pts.ToArray(), ScPink, 2f);
        Txt(g, "IN", 7.5f, ScLabel, Rc(sq.Right - 20, cy + 2, 20, 11), StringAlignment.Far, false); Txt(g, "OUT", 7.5f, ScLabel, Rc(cx + 3, sq.Y, 30, 11), StringAlignment.Near, false);
        // the wave: a clean sine (faint) and what comes out (bright); it moves while the song plays
        var wv = Rc(sq.Right + 18, r.Y, r.Right - sq.Right - 18, r.Height); float wy = wv.Y + wv.Height / 2, amp = wv.Height / 2 - 3; ScHLine(g, wv, wy, null, true);
        double ph = eng.Playing ? ScPhase * 2.5 : 0; float drive = eng.Playing ? 0.55f + 0.45f * Math.Min(1, lvl * 1.5f) : 1f; var clean = new List<PointF>(); var dirty = new List<PointF>();
        for (float i = 0; i <= wv.Width; i += 1.5f) { double x = Math.Sin(i / wv.Width * Math.PI * 4 + ph) * drive; clean.Add(new PointF(wv.X + i, wy - (float)x * amp)); dirty.Add(new PointF(wv.X + i, wy - (float)shape(x) * amp)); }
        using (var pen = new Pen(Color.FromArgb(80, 255, 255, 255), 1f)) g.DrawLines(pen, clean.ToArray()); ScFill(g, dirty.ToArray(), wy, wv, ScPink, 50); ScGlow(g, dirty.ToArray(), ScGold, 1.6f);
        title = levels > 0 ? "CRUSH CURVE" : "DRIVE CURVE"; info = (pd != null ? "DRIVE " + pd.Text : "") + (levels > 0 ? (pd != null ? "  \u00B7  " : "") + levels * 2 + " STEPS" : "");
    }

    // ---- chorus / flanger / phaser / vibrato / pitch: the movement ----
    void ScMod(Graphics g, RectangleF r, Fx f, float lvl, out string title, out string info)
    {
        var pSt = ScP(f, "shift", "pitch", "semi"); var pRate = ScP(f, "rate", "speed"); float cy = r.Y + r.Height / 2, amp = r.Height / 2 - 3; double ph = eng.Playing ? ScPhase : 0;
        ScHLine(g, r, cy, null, true); for (int i = 1; i < 4; i++) ScVLine(g, r, r.X + r.Width * i / 4, null, false);
        if (pSt != null && pSt.Unit == "st" && pRate == null)
        {   // pitch: the wave as it goes in (faint) and as it comes out, higher or lower
            double st = pSt.Val, ratio = Math.Pow(2, st / 12); var a = new List<PointF>(); var b2 = new List<PointF>();
            for (float i = 0; i <= r.Width; i += 1.5f) { double t = i / r.Width; a.Add(new PointF(r.X + i, cy - (float)Math.Sin(t * Math.PI * 6 + ph * 3) * amp * 0.8f)); b2.Add(new PointF(r.X + i, cy - (float)Math.Sin(t * Math.PI * 6 * ratio + ph * 3 * ratio) * amp * 0.8f)); }
            using (var pen = new Pen(Color.FromArgb(80, 255, 255, 255), 1f)) g.DrawLines(pen, a.ToArray()); ScGlow(g, b2.ToArray(), ScCyan, 1.8f);
            title = "PITCH"; info = (st > 0 ? "+" : "") + ScNum(st, "0.#") + " SEMITONES" + (Math.Abs(st) >= 12 ? "  \u00B7  " + ScNum(Math.Abs(st) / 12, "0.#") + " OCTAVE" + (Math.Abs(st) >= 24 ? "S" : "") : ""); return;
        }
        double hz = pRate == null ? 0.8 : (pRate.Unit == "Hz" ? pRate.Val : 0.1 + 8 * ScFrac(pRate, 0.2f)); float depth = ScPct(ScP(f, "depth", "amount", "wob"), 0.5f);
        double span = Math.Max(1, Math.Min(8, 3 / Math.Max(0.05, hz))); var lfo = new List<PointF>();
        for (float i = 0; i <= r.Width; i += 1.5f) { double t = i / r.Width * span; lfo.Add(new PointF(r.X + i, cy - (float)Math.Sin((t + ph) * hz * Math.PI * 2) * amp * (0.15f + 0.85f * depth))); }
        ScFill(g, lfo.ToArray(), cy, r, ScViolet, 60); ScGlow(g, lfo.ToArray(), ScCyan, 1.8f);
        if (eng.Playing && lvl > 0.01f) { float sx = r.X + r.Width * 0.5f; double v = Math.Sin((span * 0.5 + ph) * hz * Math.PI * 2) * (0.15f + 0.85f * depth); float sy = cy - (float)v * amp; using (var gl = new SolidBrush(Color.FromArgb(80, ScPink))) g.FillEllipse(gl, sx - 8, sy - 8, 16, 16); using (var dot = new SolidBrush(Color.White)) g.FillEllipse(dot, sx - 3, sy - 3, 6, 6); }
        title = "MOVEMENT"; info = (pRate != null ? "RATE " + (pRate.Unit == "Hz" ? ScNum(hz, "0.00") + " Hz" : pRate.Text) + "  \u00B7  " : "") + "DEPTH " + ScNum(depth * 100, "0") + "%";
    }

    // ---- anything else: the level coming out, as a moving wave ----
    void ScLevel(Graphics g, RectangleF r, Fx f, float lvl, float[] hist, out string title, out string info)
    {
        float cy = r.Y + r.Height / 2, amp = r.Height / 2 - 2; ScHLine(g, r, cy, null, true); for (int i = 1; i < 6; i++) ScVLine(g, r, r.X + r.Width * i / 6, null, false);
        int n = hist.Length; var top = new List<PointF>(); var bot = new List<PointF>();
        for (int i = 0; i < n; i++) { float v = hist[i]; float x = r.X + r.Width * i / (n - 1); float a = amp * Math.Min(1, v) * (0.75f + 0.25f * ScHash(i * 3 + 1)); top.Add(new PointF(x, cy - a)); bot.Add(new PointF(x, cy + a)); }
        ScFill(g, top.ToArray(), cy, Rc(r.X, r.Y, r.Width, r.Height / 2), ScCyan, 90); ScFill(g, bot.ToArray(), cy, Rc(r.X, cy, r.Width, r.Height / 2), ScViolet, 90);
        ScGlow(g, top.ToArray(), ScCyan, 1.6f); ScGlow(g, bot.ToArray(), ScViolet, 1.2f);
        if (!eng.Playing) Txt(g, "PLAY TO SEE THE SOUND COMING OUT", 9, ScLabel, Rc(r.X, cy - 26, r.Width, 18), StringAlignment.Center, true);
        double db = f.OutLvl > 0.00001f ? 20 * Math.Log10(f.OutLvl) : -90; title = "OUTPUT"; info = eng.Playing && db > -80 ? ScNum(db, "0.0") + " dB" : "";
    }
}
