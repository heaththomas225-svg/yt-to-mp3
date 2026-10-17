using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ======================================================================
//  PURPHEX REFERENCE: put a finished song you like next to your own mix. LISTEN flips between your mix and the reference at the same place
//  in the song; LEVEL MATCH plays them equally loud (so louder never sounds "better"); the picture compares both spectrums, and the meters
//  their loudness and width. MONO checks both in mono, FOCUS listens to the lows, mids or highs of both. The reference is a file you load.
// ======================================================================
class ReferenceFx : SyncFx
{
    public static readonly string[] Listen = { "My mix", "Reference" }, Focus = { "Full", "Lows", "Mids", "Highs" }, Sync = { "Follow the song", "From its start" };
    float[] refL, refR; public string RefPath = "", RefName = ""; public volatile string RefError = ""; public volatile bool Loading;
    readonly KW kIn = new KW(), kRef = new KW(); double msIn, msRef, corrIn, corrRef, gainMatch = 1, refPlay; double bl1, bl2, br1, br2, bh1, bh2, bk1, bk2; long refPos;
    public readonly PxScope ScopeIn = new PxScope(4096), ScopeRef = new PxScope(4096); public volatile float LoudIn = -70, LoudRef = -70, WidthIn, WidthRef, Match;
    int listen, focus, sync; bool match, mono; double trim, outG;
    public ReferenceFx()
    {
        Type = "pxref"; Title = "Purphex Reference"; Category = "Purphex VST"; Blurb = "Compare your mix with a reference song: level matched, spectrum, loudness and width";
        AddC("listen", "Listen", Listen, 0); AddC("match", "Level match", new[] { "Off", "On" }, 1); AddP("trim", "Reference trim", -24, 12, 0, "dB"); AddC("sync", "Reference plays", Sync, 0);
        AddC("mono", "Mono check", new[] { "Off", "On" }, 0); AddC("focus", "Focus", Focus, 0); AddP("out", "Output", -24, 12, 0, "dB");
    }
    // K-weighting (the loudness filter of LUFS): a high shelf around 1.5 kHz and a high-pass at 38 Hz
    sealed class KW
    {
        double a1, a2, b0, b1, b2, c1, c2, d0, d1, d2, x1, x2, y1, y2, u1, u2, v1, v2; int sr;
        public void Set(int s) { if (s == sr) return; sr = s; double f0 = 1681.97, G = 3.99984, Q = 0.7071752, K = Math.Tan(Math.PI * f0 / s), Vh = Math.Pow(10, G / 20), Vb = Math.Pow(Vh, 0.4996667741545416), a0 = 1 + K / Q + K * K; b0 = (Vh + Vb * K / Q + K * K) / a0; b1 = 2 * (K * K - Vh) / a0; b2 = (Vh - Vb * K / Q + K * K) / a0; a1 = 2 * (K * K - 1) / a0; a2 = (1 - K / Q + K * K) / a0; double f1 = 38.135, Q1 = 0.5003270373, K1 = Math.Tan(Math.PI * f1 / s), a01 = 1 + K1 / Q1 + K1 * K1; d0 = 1 / a01; d1 = -2 / a01; d2 = 1 / a01; c1 = 2 * (K1 * K1 - 1) / a01; c2 = (1 - K1 / Q1 + K1 * K1) / a01; }
        public void Reset() { x1 = x2 = y1 = y2 = u1 = u2 = v1 = v2 = 0; }
        public double Tick(double x) { double y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2; x2 = x1; x1 = x; y2 = y1; y1 = y; double z = d0 * y + d1 * u1 + d2 * u2 - c1 * v1 - c2 * v2; u2 = u1; u1 = y; v2 = v1; v1 = z; return z; }
    }
    public void SetReference(float[] l, float[] r, string name) { refL = l; refR = r ?? l; RefName = name ?? ""; refPos = 0; }
    public bool HasRef { get { return refL != null && refL.Length > 0; } }
    public void LoadFile(string path, Action done)
    {
        Loading = true; RefError = ""; var th = new System.Threading.Thread(() => { try { var c = AudioClip.FromFile(path); RefPath = path; SetReference(c.L, c.R, System.IO.Path.GetFileNameWithoutExtension(path)); } catch (Exception ex) { RefError = ex.Message; } Loading = false; if (done != null) done(); }) { IsBackground = true, Name = "px-ref-load" }; th.Start();
    }
    public override string Serialize() { return base.Serialize() + (RefPath.Length > 0 ? ";refpath=" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(RefPath)) : ""); }
    public override void Deserialize(string s)
    {
        base.Deserialize(s); if (s == null) return; string tag = s.Contains(";refpath=") ? ";refpath=" : (s.StartsWith("refpath=") ? "refpath=" : null); if (tag == null) return;
        int j = s.IndexOf(tag) + tag.Length; int k = s.IndexOf(';', j); string b64 = k < 0 ? s.Substring(j) : s.Substring(j, k - j); try { string p = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(b64)); if (p != RefPath && System.IO.File.Exists(p)) { RefPath = p; LoadFile(p, null); } else RefPath = p; } catch { }
    }
    public override void Reset() { base.Reset(); kIn.Reset(); kRef.Reset(); msIn = msRef = 0; refPlay = 0; bl1 = bl2 = br1 = br2 = bh1 = bh2 = bk1 = bk2 = 0; }
    protected override void Update() { listen = (int)Get("listen"); match = Get("match") >= 0.5f; trim = Math.Pow(10, Get("trim") / 20); sync = (int)Get("sync"); mono = Get("mono") >= 0.5f; focus = (int)Get("focus"); outG = Math.Pow(10, Get("out") / 20); kIn.Set(SR); kRef.Set(SR); }
    // FOCUS: only the lows (< 200 Hz), the mids (200 Hz - 3 kHz) or the highs (> 3 kHz), on both
    double Band(double x, ref double s1, ref double s2, ref double t1, ref double t2)
    {
        if (focus == 0) return x; double cl = 1 - Math.Exp(-2 * Math.PI * 200 / SR), ch = 1 - Math.Exp(-2 * Math.PI * 3000 / SR);
        s1 += (x - s1) * cl; s2 += (s1 - s2) * cl; t1 += (x - t1) * ch; t2 += (t1 - t2) * ch; double low = s2, below3k = t2;
        return focus == 1 ? low : focus == 2 ? below3k - low : x - below3k;
    }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); bool has = HasRef; double wLoud = 1 - Math.Exp(-1.0 / (3.0 * SR)), wC = 1 - Math.Exp(-1.0 / (0.5 * SR));
        long start = sync == 0 ? (tr ? bp : freeCnt) : refPos; float[] rl = refL, rr = refR;
        for (int i = 0; i < n; i++)
        {
            double xl = l[i], xr = r[i], kl = kIn.Tick((xl + xr) * 0.5); msIn += (kl * kl * 2 - msIn) * wLoud; corrIn += (xl * xr - corrIn) * wC;
            double ql = 0, qr = 0;
            if (has) { long p = start + i; if (sync == 0 && rl.Length > 0) p %= rl.Length; else if (p >= rl.Length) p %= rl.Length; if (p < 0) p = 0; ql = rl[p] * trim; qr = rr[p] * trim; double kr = kRef.Tick((ql + qr) * 0.5); msRef += (kr * kr * 2 - msRef) * wLoud; corrRef += (ql * qr - corrRef) * wC; }
            ScopeIn.Push((float)((xl + xr) * 0.5)); ScopeRef.Push((float)((ql + qr) * 0.5));
            double ol = xl, or = xr;
            if (listen == 1 && has) { double gm = match && msRef > 1e-9 ? Math.Sqrt(msIn / msRef) : 1; gm = Math.Min(8, Math.Max(0.05, gm)); gainMatch += (gm - gainMatch) * 0.0005; ol = ql * gainMatch; or = qr * gainMatch; }
            if (mono) { double m = (ol + or) * 0.5; ol = or = m; }
            if (focus > 0) { ol = Band(ol, ref bl1, ref bl2, ref bh1, ref bh2); or = Band(or, ref br1, ref br2, ref bk1, ref bk2); }
            l[i] = (float)(ol * outG); r[i] = (float)(or * outG);
        }
        if (sync == 1 && has) refPos = (refPos + n) % Math.Max(1, rl.Length);
        LoudIn = (float)(-0.691 + 10 * Math.Log10(msIn + 1e-12)); LoudRef = has ? (float)(-0.691 + 10 * Math.Log10(msRef + 1e-12)) : -70; Match = (float)(20 * Math.Log10(Math.Max(1e-6, gainMatch)));
        WidthIn = (float)corrIn; WidthRef = (float)corrRef; EndBlock(n);
    }
}

class ReferenceWindow : PxWindow
{
    readonly ReferenceFx e;
    public ReferenceWindow(ReferenceFx f) : base(f, "Reference", new Size(960, 640)) { e = f; Accent = Gold; }
    protected override string SubTitle { get { return "your mix next to a song you like"; } }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        // the reference file
        var top = new RectangleF(a.X, a.Y, a.Width, 60); Panel(g, top, "", Gold);
        Button(g, new RectangleF(top.X + 14, top.Y + 14, 200, 32), e.HasRef ? "CHANGE REFERENCE..." : "LOAD A REFERENCE...", !e.HasRef, Gold, () => { using (var d = new OpenFileDialog { Title = "Pick a reference song", Filter = "Audio|*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg;*.opus;*.aiff;*.aif|All files|*.*" }) if (d.ShowDialog(this) == DialogResult.OK) e.LoadFile(d.FileName, null); }, "A finished song to compare your mix with", 11);
        Txt(g, e.Loading ? "LOADING..." : e.RefError.Length > 0 ? "Could not load it: " + e.RefError : e.HasRef ? e.RefName : "No reference yet: load a song you would like yours to sound like.", 12, e.RefError.Length > 0 ? Pink : e.HasRef ? Ink : Dim, new RectangleF(top.X + 230, top.Y + 8, top.Width - 250, 44), StringAlignment.Near, e.HasRef);
        // the two spectrums
        var sp = new RectangleF(a.X, top.Bottom + 10, a.Width, 250); Fill(g, Well, sp, 10); float x0 = sp.X + 12, w = sp.Width - 24, y0 = sp.Y + 12, h = sp.Height - 24;
        using (var pen = new Pen(Color.FromArgb(28, 255, 255, 255))) foreach (var hz in new[] { 100.0, 1000, 10000 }) { float x = x0 + (float)(Math.Log(hz / 20) / Math.Log(1000)) * w; g.DrawLine(pen, x, y0, x, y0 + h); }
        Action<PxScope, Color> draw = (sc, c) => { var db = sc.Spectrum(4096); var pts = new PointF[120]; for (int i = 0; i < 120; i++) { double hz = 20 * Math.Pow(1000, i / 119.0); int k = Math.Max(1, Math.Min(db.Length - 1, (int)(hz * 4096 / 44100))); double v = 0; int c0 = 0; for (int q = Math.Max(1, k - 2); q <= Math.Min(db.Length - 1, k + 2); q++) { v += db[q]; c0++; } v /= c0; float y = y0 + h - (float)Math.Max(0, Math.Min(1, (v + 90) / 84)) * h; pts[i] = new PointF(x0 + i / 119f * w, y); } using (var pen = new Pen(c, 2f)) g.DrawLines(pen, pts); };
        draw(e.ScopeIn, Color.FromArgb(220, Cyan)); if (e.HasRef) draw(e.ScopeRef, Color.FromArgb(220, Gold));
        Txt(g, "MY MIX", 11, Cyan, new RectangleF(x0 + 6, y0 + 2, 90, 16), StringAlignment.Near, true); Txt(g, "REFERENCE", 11, Gold, new RectangleF(x0 + 96, y0 + 2, 120, 16), StringAlignment.Near, true);
        // loudness and width, and the controls
        var bot = new RectangleF(a.X, sp.Bottom + 10, a.Width, a.Bottom - sp.Bottom - 10); Panel(g, bot, "", Violet); float cy = bot.Y + bot.Height / 2;
        Txt(g, "LOUDNESS (LUFS, LAST 3 S)", 10, Dim, new RectangleF(bot.X + 16, bot.Y + 10, 240, 14), StringAlignment.Near, true);
        Txt(g, e.LoudIn.ToString("0.0"), 26, Cyan, new RectangleF(bot.X + 16, bot.Y + 28, 110, 34), StringAlignment.Near, true); Txt(g, e.HasRef ? e.LoudRef.ToString("0.0") : "--", 26, Gold, new RectangleF(bot.X + 130, bot.Y + 28, 110, 34), StringAlignment.Near, true);
        Txt(g, "WIDTH (CORRELATION)", 10, Dim, new RectangleF(bot.X + 16, bot.Y + 72, 240, 14), StringAlignment.Near, true);
        Txt(g, "mix " + Corr(e.WidthIn) + "    ref " + (e.HasRef ? Corr(e.WidthRef) : "--"), 12, Ink, new RectangleF(bot.X + 16, bot.Y + 88, 260, 18), StringAlignment.Near, false);
        Txt(g, "LISTEN", 10, Dim, new RectangleF(bot.X + 300, bot.Y + 10, 200, 14), StringAlignment.Near, true); Segments(g, new RectangleF(bot.X + 300, bot.Y + 28, 260, 30), "listen", Gold, 11);
        Txt(g, "FOCUS", 10, Dim, new RectangleF(bot.X + 300, bot.Y + 68, 200, 14), StringAlignment.Near, true); Segments(g, new RectangleF(bot.X + 300, bot.Y + 86, 260, 26), "focus", Cyan, 10);
        Txt(g, "LEVEL MATCH", 10, Dim, new RectangleF(bot.X + 590, bot.Y + 10, 130, 14), StringAlignment.Near, true); Toggle(g, new RectangleF(bot.X + 590, bot.Y + 28, 110, 30), "match", Lime);
        Txt(g, "MONO", 10, Dim, new RectangleF(bot.X + 590, bot.Y + 68, 130, 14), StringAlignment.Near, true); Toggle(g, new RectangleF(bot.X + 590, bot.Y + 86, 110, 26), "mono", Pink);
        Knob(g, bot.Right - 160, cy - 4, 20, "trim", "REF TRIM", Gold); Knob(g, bot.Right - 70, cy - 4, 20, "out", "OUTPUT", Ink);
        Choice(g, new RectangleF(bot.X + 300, bot.Bottom - 34, 260, 24), "sync", Violet, 10);
        if (e.HasRef && e.Get("match") >= 0.5f) Txt(g, "matched by " + (e.Match >= 0 ? "+" : "") + e.Match.ToString("0.0") + " dB", 10, Lime, new RectangleF(bot.X + 590, bot.Bottom - 30, 200, 16), StringAlignment.Near, false);
    }
    static string Corr(float c) { return c > 0.9 ? "narrow" : c > 0.5 ? "medium" : c > 0.1 ? "wide" : "very wide"; }
}

// ======================================================================
//  PURPHEX PATHFINDER: a reverse delay. Every echo comes back backwards (or forwards, or backwards bouncing left and right), in time with
//  the song. PITCH moves the echoes up or down, FEEDBACK lets them build, the filters and DIFFUSION blur them into a wash, DUCK keeps them
//  out of the way while you sing, WIDTH spreads them.
// ======================================================================
class PathfinderFx : SyncFx
{
    public static readonly string[] Times = { "1/16", "1/8", "1/4", "1/2", "1 bar" }, Dirs = { "Reverse", "Forward", "Reverse ping-pong" }; static readonly double[] TB = { 0.25, 0.5, 1, 2, 4 };
    readonly float[] bufL = new float[1 << 19], bufR = new float[1 << 19]; int w; readonly PxShifter shL = new PxShifter(16384), shR = new PxShifter(16384);
    readonly float[][] ap = new float[8][]; readonly int[] apW = new int[8]; static readonly int[] ApLen = { 347, 541, 769, 1031, 379, 577, 811, 1103 };
    double fbL, fbR, lpL, lpR, hpL, hpR, env; long cnt;
    double tBeats, fb, ratio, lpC, hpC, diff, width, duck, mixW, outG; int dir;
    public volatile float ShowPhase;
    public PathfinderFx()
    {
        Type = "pxpath"; Title = "Purphex Pathfinder"; Category = "Purphex VST"; Blurb = "Reverse delay in time with the song: pitch, feedback, filters, diffusion and ducking";
        AddC("time", "Time", Times, 2); AddC("dir", "Direction", Dirs, 0); AddP("fb", "Feedback", 0, 95, 35, "%"); AddP("pitch", "Pitch", -12, 12, 0, "st");
        AddP("lp", "High cut", 500, 20000, 12000, "Hz"); AddP("hp", "Low cut", 20, 2000, 120, "Hz"); AddP("diff", "Diffusion", 0, 100, 20, "%"); AddP("width", "Width", 0, 100, 60, "%");
        AddP("duck", "Duck", 0, 100, 0, "%"); AddP("mix", "Mix", 0, 100, 40, "%"); AddP("out", "Output", -24, 12, 0, "dB");
        for (int i = 0; i < 8; i++) ap[i] = new float[ApLen[i]];
    }
    public override void Reset() { base.Reset(); Array.Clear(bufL, 0, bufL.Length); Array.Clear(bufR, 0, bufR.Length); w = 0; shL.Reset(); shR.Reset(); foreach (var a in ap) Array.Clear(a, 0, a.Length); Array.Clear(apW, 0, 8); fbL = fbR = lpL = lpR = hpL = hpR = env = 0; cnt = 0; }
    protected override void Update()
    {
        tBeats = TB[(int)Get("time")]; dir = (int)Get("dir"); fb = Get("fb") / 100.0; ratio = Math.Pow(2, Get("pitch") / 12.0); lpC = 1 - Math.Exp(-2 * Math.PI * Get("lp") / SR); hpC = 1 - Math.Exp(-2 * Math.PI * Get("hp") / SR);
        diff = Get("diff") / 100.0; width = Get("width") / 100.0; duck = Get("duck") / 100.0; mixW = Get("mix") / 100.0; outG = Math.Pow(10, Get("out") / 20);
    }
    double Ap(int k, double x, double g) { var b = ap[k]; int i = apW[k]; double d = b[i]; double y = -g * x + d; b[i] = (float)(x + g * y); apW[k] = (i + 1) % b.Length; return y; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); int L = bufL.Length; double T = Math.Max(256, Math.Min(L / 3, 60.0 / Math.Max(20, Fx.Bpm) * SR * tBeats)); double envA = 1 - Math.Exp(-1.0 / (0.005 * SR)), envR = 1 - Math.Exp(-1.0 / (0.25 * SR));
        for (int i = 0; i < n; i++, cnt++)
        {
            double xl = l[i], xr = r[i]; double a = Math.Max(Math.Abs(xl), Math.Abs(xr)); env += (a - env) * (a > env ? envA : envR);
            bufL[w] = (float)(xl + Math.Tanh(fbL * fb)); bufR[w] = (float)(xr + Math.Tanh(fbR * fb));
            // two readers half a segment apart, each faded in and out with a sine squared, read the last segment backwards (or forwards)
            double yl = 0, yr = 0; long segIdx = 0;
            for (int k = 0; k < 2; k++)
            {
                double ph = ((cnt + k * T / 2) % T) / T; double win = Math.Sin(Math.PI * ph); win *= win; double back = dir == 1 ? T : 2 * ph * T + 1; double rp = w - back; if (rp < 0) rp += L; int i0 = (int)rp; double fr = rp - i0; int i1 = (i0 + 1) & (L - 1);
                double vl = bufL[i0] + (bufL[i1] - bufL[i0]) * fr, vr = bufR[i0] + (bufR[i1] - bufR[i0]) * fr;
                if (dir == 2) { long sg = (long)((cnt + k * T / 2) / T); double pan = (sg & 1) == 0 ? 0.15 : 0.85; double m = (vl + vr) * 0.5; vl = m * Math.Cos(pan * Math.PI / 2) * 1.414; vr = m * Math.Sin(pan * Math.PI / 2) * 1.414; if (k == 0) segIdx = sg; }
                yl += vl * win; yr += vr * win;
            }
            if (ratio != 1) { yl = shL.Tick(yl, ratio, 0, SR); yr = shR.Tick(yr, ratio, 0, SR); }
            lpL += (yl - lpL) * lpC; lpR += (yr - lpR) * lpC; hpL += (lpL - hpL) * hpC; hpR += (lpR - hpR) * hpC; yl = lpL - hpL; yr = lpR - hpR;
            if (diff > 0) { double g = 0.3 + diff * 0.45, dl = yl, dr = yr; for (int k = 0; k < 4; k++) { dl = Ap(k, dl, g); dr = Ap(k + 4, dr, g); } yl += (dl - yl) * diff; yr += (dr - yr) * diff; }
            fbL = yl; fbR = yr;
            double m2 = (yl + yr) * 0.5, s2 = (yl - yr) * 0.5 * width * 1.6; yl = m2 + s2; yr = m2 - s2;
            double dk = 1 - duck * Math.Min(1, env * 4); w = (w + 1) & (L - 1);
            l[i] = (float)((xl * (1 - mixW) + yl * mixW * dk) * outG); r[i] = (float)((xr * (1 - mixW) + yr * mixW * dk) * outG);
        }
        ShowPhase = (float)((cnt % (long)T) / T); EndBlock(n);
    }
}

class PathfinderWindow : PxWindow
{
    readonly PathfinderFx e;
    public PathfinderWindow(PathfinderFx f) : base(f, "Pathfinder", new Size(900, 560)) { e = f; Accent = Violet; }
    protected override string SubTitle { get { return "a reverse delay"; } }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        // the picture: echoes running backwards, getting quieter with the feedback
        var pic = new RectangleF(a.X, a.Y, a.Width, 170); Fill(g, Well, pic, 10); double fbv = e.Get("fb") / 100.0; int dir = (int)e.Get("dir");
        for (int k = 0; k < 7; k++) { float x = pic.X + 40 + k * (pic.Width - 80) / 7f, lv = (float)Math.Pow(Math.Max(0.05, fbv), k), hh = (pic.Height - 40) * lv; var r = new RectangleF(x, pic.Y + pic.Height / 2 - hh / 2, (pic.Width - 80) / 7f - 14, hh);
            using (var br = new LinearGradientBrush(new RectangleF(r.X, r.Y, Math.Max(1, r.Width), Math.Max(1, r.Height)), dir == 1 ? Color.FromArgb(230, Violet) : Color.FromArgb(40, Violet), dir == 1 ? Color.FromArgb(40, Violet) : Color.FromArgb(230, Violet), 0f)) g.FillRectangle(br, r); }
        Txt(g, PathfinderFx.Dirs[dir].ToUpper(), 12, Ink, new RectangleF(pic.X + 14, pic.Y + 8, 300, 16), StringAlignment.Near, true);
        float ph = e.ShowPhase; using (var pen = new Pen(Color.FromArgb(160, Cyan), 2)) g.DrawLine(pen, pic.X + 40 + ph * (pic.Width - 80), pic.Y + 10, pic.X + 40 + ph * (pic.Width - 80), pic.Bottom - 10);
        // time and direction, then the knobs
        var p1 = new RectangleF(a.X, pic.Bottom + 10, 260, a.Bottom - pic.Bottom - 10); Panel(g, p1, "", Violet);
        Txt(g, "TIME", 10, Dim, new RectangleF(p1.X + 16, p1.Y + 12, 100, 14), StringAlignment.Near, true); Segments(g, new RectangleF(p1.X + 16, p1.Y + 30, p1.Width - 32, 28), "time", Violet, 10);
        Txt(g, "DIRECTION", 10, Dim, new RectangleF(p1.X + 16, p1.Y + 72, 140, 14), StringAlignment.Near, true); Choice(g, new RectangleF(p1.X + 16, p1.Y + 90, p1.Width - 32, 26), "dir", Cyan, 10.5f);
        Knob(g, p1.X + p1.Width / 2, p1.Y + 200, 34, "fb", "FEEDBACK", Gold, true);
        var p2 = new RectangleF(p1.Right + 10, p1.Y, a.Right - p1.Right - 10, p1.Height); Panel(g, p2, "", Cyan); float kw = p2.Width / 4f, r1 = p2.Y + p2.Height * 0.3f, r2 = p2.Y + p2.Height * 0.72f;
        Knob(g, p2.X + kw * 0.5f, r1, 22, "pitch", "PITCH", Pink); Knob(g, p2.X + kw * 1.5f, r1, 22, "diff", "DIFFUSION", Cyan); Knob(g, p2.X + kw * 2.5f, r1, 22, "lp", "HIGH CUT", Cyan); Knob(g, p2.X + kw * 3.5f, r1, 22, "hp", "LOW CUT", Cyan);
        Knob(g, p2.X + kw * 0.5f, r2, 22, "width", "WIDTH", Violet); Knob(g, p2.X + kw * 1.5f, r2, 22, "duck", "DUCK", Lime); Knob(g, p2.X + kw * 2.5f, r2, 22, "mix", "MIX", Ink); Knob(g, p2.X + kw * 3.5f, r2, 22, "out", "OUTPUT", Ink);
    }
}

// ======================================================================
//  PURPHEX MODULAR: four slots in a row, each one a module you pick (drive, fold, crush, rectify, low-pass, high-pass, band-pass, comb, ring,
//  tremolo, downsample) with its own AMOUNT. Each slot can be moved by the LFO or by the ENVELOPE (how loud the sound is) by its own MOD amount.
// ======================================================================
class ModularFx : Fx
{
    public const int N = 4; public static readonly string[] Mods = { "Off", "Drive", "Fold", "Crush", "Rectify", "Low-pass", "High-pass", "Band-pass", "Comb", "Ring", "Tremolo", "Downsample" }, Srcs = { "LFO", "Envelope" }, Shapes = { "Sine", "Triangle", "Square", "Random" };
    sealed class Slot { public int type; public double amt, mod; public int src; public double s1L, s2L, s1R, s2R, holdL, holdR; public int hc; public readonly float[] cL = new float[4096], cR = new float[4096]; public int cw; public double ringPh; public void Reset() { s1L = s2L = s1R = s2R = holdL = holdR = 0; hc = 0; Array.Clear(cL, 0, cL.Length); Array.Clear(cR, 0, cR.Length); cw = 0; ringPh = 0; } }
    readonly Slot[] slots = new Slot[N]; double inG, lfoRate, sens, mixW, outG, env, lfoPh, rnd; int shape; uint seed = 11;
    public volatile float ShowLfo, ShowEnv;
    public ModularFx()
    {
        Type = "pxmodular"; Title = "Purphex Modular"; Category = "Purphex VST"; Blurb = "Four slots of drive, fold, crush, filters, comb, ring and more, each moved by an LFO or envelope";
        int[] dt = { 1, 5, 0, 0 }; float[] da = { 40, 50, 50, 50 };
        for (int k = 0; k < N; k++) { string s = "s" + k + "_"; AddC(s + "type", "Slot " + (k + 1) + " module", Mods, dt[k]); AddP(s + "amt", "Slot " + (k + 1) + " amount", 0, 100, da[k], "%"); AddP(s + "mod", "Slot " + (k + 1) + " mod", -100, 100, 0, "%"); AddC(s + "src", "Slot " + (k + 1) + " moved by", Srcs, 0); slots[k] = new Slot(); }
        AddP("in", "Input", -24, 24, 0, "dB"); AddP("lrate", "LFO rate", 0.05f, 20, 1.5f, "Hz"); AddC("lshape", "LFO shape", Shapes, 0); AddP("sens", "Envelope sensitivity", 0, 100, 50, "%"); AddP("mix", "Mix", 0, 100, 100, "%"); AddP("out", "Output", -24, 12, 0, "dB");
    }
    public override void Reset() { base.Reset(); foreach (var s in slots) s.Reset(); env = 0; lfoPh = 0; seed = 11; rnd = 0; }
    protected override void Update()
    {
        for (int k = 0; k < N; k++) { string s = "s" + k + "_"; var sl = slots[k]; sl.type = (int)Get(s + "type"); sl.amt = Get(s + "amt") / 100.0; sl.mod = Get(s + "mod") / 100.0; sl.src = (int)Get(s + "src"); }
        inG = Math.Pow(10, Get("in") / 20); lfoRate = Get("lrate"); shape = (int)Get("lshape"); sens = Get("sens") / 100.0; mixW = Get("mix") / 100.0; outG = Math.Pow(10, Get("out") / 20);
    }
    double Lfo() { double p = lfoPh; switch (shape) { case 1: return 1 - 4 * Math.Abs(p - 0.5); case 2: return p < 0.5 ? 1 : -1; case 3: return rnd; default: return Math.Sin(2 * Math.PI * p); } }
    double Svf(double x, double fc, double res, ref double s1, ref double s2, int mode) { double g = Math.Tan(Math.PI * Math.Min(SR * 0.45, Math.Max(20, fc)) / SR), k = 2 - 1.9 * res; double hp = (x - (k + g) * s1 - s2) / (1 + g * (g + k)); double v1 = g * hp, bp = v1 + s1; s1 = bp + v1; double v2 = g * bp, lp = v2 + s2; s2 = lp + v2; return mode == 0 ? lp : mode == 1 ? hp : bp * k; }
    double Run(Slot s, double x, double a, bool left)
    {
        switch (s.type)
        {
            case 1: { double gg = 1 + a * 20; return Math.Tanh(x * gg) / Math.Tanh(gg) * (1 - a * 0.25); }
            case 2: { double gg = 1 + a * 8, y = x * gg; for (int q = 0; q < 6 && Math.Abs(y) > 1; q++) y = y > 1 ? 2 - y : -2 - y; return y * 0.8; }
            case 3: { int hold = 1 + (int)(a * 24); if (left) { if (++s.hc >= hold) { s.hc = 0; double st = Math.Pow(2, -(16 - a * 13)); s.holdL = Math.Round(x / st) * st; s.holdR = double.NaN; } return s.holdL; } else { if (double.IsNaN(s.holdR) || s.hc == 0) { double st = Math.Pow(2, -(16 - a * 13)); s.holdR = Math.Round(x / st) * st; } return s.holdR; } }
            case 4: { double rect = Math.Abs(x); return (x * (1 - a) + rect * a * 1.4 - a * 0.3) ; }
            case 5: return left ? Svf(x, 20 * Math.Pow(1000, 1 - a), 0.5, ref s.s1L, ref s.s2L, 0) : Svf(x, 20 * Math.Pow(1000, 1 - a), 0.5, ref s.s1R, ref s.s2R, 0);
            case 6: return left ? Svf(x, 20 * Math.Pow(500, a), 0.5, ref s.s1L, ref s.s2L, 1) : Svf(x, 20 * Math.Pow(500, a), 0.5, ref s.s1R, ref s.s2R, 1);
            case 7: return left ? Svf(x, 80 * Math.Pow(150, a), 0.75, ref s.s1L, ref s.s2L, 2) : Svf(x, 80 * Math.Pow(150, a), 0.75, ref s.s1R, ref s.s2R, 2);
            case 8: { var buf = left ? s.cL : s.cR; double d = Math.Max(2, Math.Min(buf.Length - 2, SR / (60 * Math.Pow(30, a)))); double rp = s.cw - d; if (rp < 0) rp += buf.Length; int i0 = (int)rp; double fr = rp - i0; double v = buf[i0] + (buf[(i0 + 1) % buf.Length] - buf[i0]) * fr; double y = x + v * 0.85; buf[s.cw] = (float)Math.Tanh(y); if (!left) s.cw = (s.cw + 1) % buf.Length; return y * 0.6; }
            case 9: { if (left) s.ringPh += (30 + a * 1500) / SR; if (s.ringPh >= 1) s.ringPh -= 1; return x * (1 - a) + x * Math.Sin(2 * Math.PI * s.ringPh) * a; }
            case 10: { if (left) s.ringPh += (0.5 + a * 15) / SR; if (s.ringPh >= 1) s.ringPh -= 1; return x * (1 - a * (0.5 + 0.5 * Math.Sin(2 * Math.PI * s.ringPh))); }
            case 11: { int hold = 1 + (int)(a * 40); if (left) { if (++s.hc >= hold) { s.hc = 0; s.holdL = x; s.holdR = double.NaN; } return s.holdL; } else { if (double.IsNaN(s.holdR) || s.hc == 0) s.holdR = x; return s.holdR; } }
            default: return x;
        }
    }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); double envA = 1 - Math.Exp(-1.0 / (0.003 * SR)), envR = 1 - Math.Exp(-1.0 / (0.15 * SR));
        for (int i = 0; i < n; i++)
        {
            double xl = l[i] * inG, xr = r[i] * inG, a = Math.Max(Math.Abs(xl), Math.Abs(xr)); env += (a - env) * (a > env ? envA : envR);
            lfoPh += lfoRate / SR; if (lfoPh >= 1) { lfoPh -= 1; seed = seed * 1664525 + 1013904223; rnd = (seed >> 8) / 8388608.0 - 1; }
            double lf = Lfo(), ev = Math.Min(1, env * (1 + sens * 8)); double yl = xl, yr = xr;
            for (int k = 0; k < N; k++) { var s = slots[k]; if (s.type == 0) continue; double am = Math.Max(0, Math.Min(1, s.amt + s.mod * (s.src == 0 ? lf * 0.5 : ev - 0.3))); yl = Run(s, yl, am, true); yr = Run(s, yr, am, false); }
            l[i] = (float)((l[i] * (1 - mixW) + yl * mixW) * outG); r[i] = (float)((r[i] * (1 - mixW) + yr * mixW) * outG);
        }
        ShowLfo = (float)Lfo(); ShowEnv = (float)Math.Min(1, env * (1 + sens * 8));
    }
}

class ModularWindow : PxWindow
{
    readonly ModularFx e; static readonly Color[] Col = { Color.FromArgb(255, 255, 80, 196), Color.FromArgb(255, 60, 225, 255), Color.FromArgb(255, 255, 205, 80), Color.FromArgb(255, 120, 240, 140) };
    public ModularWindow(ModularFx f) : base(f, "Modular", new Size(980, 600)) { e = f; Accent = Lime; }
    protected override string SubTitle { get { return "four slots, your chain"; } }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        float sw = (a.Width - 30) / 4f, sh = a.Height - 170;
        for (int k = 0; k < ModularFx.N; k++)
        {
            string s = "s" + k + "_"; var r = new RectangleF(a.X + k * (sw + 10), a.Y, sw, sh); Panel(g, r, "SLOT " + (k + 1), Col[k]);
            Choice(g, new RectangleF(r.X + 14, r.Y + 34, r.Width - 28, 26), s + "type", Col[k], 10.5f);
            Knob(g, r.X + r.Width / 2, r.Y + 118, 32, s + "amt", "AMOUNT", Col[k], true);
            Knob(g, r.X + r.Width / 2, r.Y + 210, 20, s + "mod", "MOD", Col[k]);
            Choice(g, new RectangleF(r.X + 14, r.Bottom - 40, r.Width - 28, 24), s + "src", Col[k], 10);
            if (k < 3) using (var pen = new Pen(Color.FromArgb(150, 255, 255, 255), 2f) { EndCap = LineCap.ArrowAnchor }) g.DrawLine(pen, r.Right - 2, r.Y + 47, r.Right + 10, r.Y + 47);
        }
        var bot = new RectangleF(a.X, a.Y + sh + 10, a.Width, a.Bottom - a.Y - sh - 10); Panel(g, bot, "", Violet); float cy = bot.Y + bot.Height / 2 - 4;
        Knob(g, bot.X + 60, cy, 20, "in", "INPUT", Ink);
        Txt(g, "LFO", 10, Dim, new RectangleF(bot.X + 130, bot.Y + 8, 100, 14), StringAlignment.Near, true); Knob(g, bot.X + 170, cy, 20, "lrate", "RATE", Gold); Choice(g, new RectangleF(bot.X + 220, cy - 12, 130, 24), "lshape", Gold, 10);
        Meter(g, new RectangleF(bot.X + 365, bot.Y + 16, 14, bot.Height - 46), (float)(Math.Abs(e.ShowLfo) * 0.9 + 0.01), "LFO");
        Txt(g, "ENVELOPE", 10, Dim, new RectangleF(bot.X + 410, bot.Y + 8, 120, 14), StringAlignment.Near, true); Knob(g, bot.X + 450, cy, 20, "sens", "SENSITIVITY", Lime); Meter(g, new RectangleF(bot.X + 510, bot.Y + 16, 14, bot.Height - 46), e.ShowEnv + 0.001f, "ENV");
        Knob(g, bot.Right - 160, cy, 20, "mix", "MIX", Ink); Knob(g, bot.Right - 70, cy, 20, "out", "OUTPUT", Ink);
    }
}

// ======================================================================
//  PURPHEX DREAMSCAPE: a huge ambient space. SIZE and DECAY (up to 30 seconds), DAMP darkens the tail, SHIMMER feeds an octave (or a fifth)
//  up back into it so it rises, BLOOM lets it swell in slowly, MOD makes it drift, FREEZE holds the sound forever while you play over it.
// ======================================================================
class DreamscapeFx : Fx
{
    public static readonly string[] Shifts = { "+12 (octave up)", "+7 (fifth up)", "+19 (octave + fifth)", "-12 (octave down)" }; static readonly double[] SR_ = { 12, 7, 19, -12 };
    const int NL = 8; static readonly int[] Base = { 1557, 1617, 1491, 1422, 1277, 1356, 1188, 1116 };
    readonly float[][] line = new float[NL][]; readonly int[] lw = new int[NL]; readonly double[] lpS = new double[NL], len = new double[NL], modPh = new double[NL];
    readonly float[][] pre = new float[4][]; readonly int[] pw = new int[4]; static readonly int[] PreLen = { 142, 379, 107, 277 };
    readonly PxShifter shim = new PxShifter(16384); double hpL, hpR, lpL, lpR, bloomEnv, fastEnv;
    double size, decay, damp, shimmer, shift, mod, bloom, lowC, highC, width, mixW, outG; bool freeze;
    public volatile float ShowLevel; readonly double[] outsB = new double[NL], mixB = new double[NL];
    public DreamscapeFx()
    {
        Type = "pxdream"; Title = "Purphex Dreamscape"; Category = "Purphex VST"; Blurb = "A huge ambient space with shimmer, freeze, bloom and drift";
        AddP("size", "Size", 10, 100, 70, "%"); AddP("decay", "Decay", 0.5f, 30, 8, "s"); AddP("damp", "Damp", 0, 100, 40, "%"); AddP("shimmer", "Shimmer", 0, 100, 25, "%"); AddC("shift", "Shimmer pitch", Shifts, 0);
        AddP("mod", "Drift", 0, 100, 35, "%"); AddP("bloom", "Bloom", 0, 100, 30, "%"); AddC("freeze", "Freeze", new[] { "Off", "On" }, 0);
        AddP("lowcut", "Low cut", 20, 1000, 150, "Hz"); AddP("highcut", "High cut", 1000, 20000, 9000, "Hz"); AddP("width", "Width", 0, 100, 100, "%"); AddP("mix", "Mix", 0, 100, 40, "%"); AddP("out", "Output", -24, 12, 0, "dB");
        for (int i = 0; i < NL; i++) line[i] = new float[8192]; for (int i = 0; i < 4; i++) pre[i] = new float[PreLen[i] * 3];
    }
    public override void Reset() { base.Reset(); foreach (var b in line) Array.Clear(b, 0, b.Length); foreach (var b in pre) Array.Clear(b, 0, b.Length); Array.Clear(lw, 0, NL); Array.Clear(pw, 0, 4); Array.Clear(lpS, 0, NL); shim.Reset(); hpL = hpR = lpL = lpR = bloomEnv = fastEnv = 0; for (int i = 0; i < NL; i++) modPh[i] = i / (double)NL; }
    protected override void Update()
    {
        size = Get("size") / 100.0; decay = Get("decay"); damp = Get("damp") / 100.0; shimmer = Get("shimmer") / 100.0; shift = Math.Pow(2, SR_[(int)Get("shift")] / 12.0); mod = Get("mod") / 100.0; bloom = Get("bloom") / 100.0; freeze = Get("freeze") >= 0.5f;
        lowC = 1 - Math.Exp(-2 * Math.PI * Get("lowcut") / SR); highC = 1 - Math.Exp(-2 * Math.PI * Get("highcut") / SR); width = Get("width") / 100.0; mixW = Get("mix") / 100.0; outG = Math.Pow(10, Get("out") / 20);
        double k = SR / 44100.0; for (int i = 0; i < NL; i++) len[i] = Math.Min(line[i].Length - 64, Base[i] * (0.35 + size * 2.4) * k);
    }
    double Pre(int k, double x, double g) { var b = pre[k]; int L = PreLen[k] * (1 + (int)(size * 2)); if (L >= b.Length) L = b.Length - 1; int i = pw[k] % L; double d = b[i]; double y = -g * x + d; b[i] = (float)(x + g * y); pw[k] = (i + 1) % L; return y; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); double meanLen = 0; for (int i = 0; i < NL; i++) meanLen += len[i]; meanLen /= NL; double g = freeze ? 1.0 : Math.Pow(10, -3 * meanLen / SR / Math.Max(0.1, decay)); double dampC = freeze ? 1 : 1 - damp * 0.85;   /* frozen: no damping and no drift, so the loop keeps every bit of the sound */ double bloomA = 1 - Math.Exp(-1.0 / ((0.02 + bloom * 1.5) * SR)), fastA = 1 - Math.Exp(-1.0 / (0.005 * SR)), relA = 1 - Math.Exp(-1.0 / (0.3 * SR));
        var outs = outsB; double lvl = 0;
        for (int i = 0; i < n; i++)
        {
            double xl = l[i], xr = r[i], inp = freeze ? 0 : (xl + xr) * 0.5;
            // BLOOM: the sound going into the space fades in slowly at the start of each phrase (a slow follower of the level against a quick one)
            double ai = Math.Abs(inp); fastEnv += (ai - fastEnv) * (ai > fastEnv ? fastA : relA); bloomEnv += (ai - bloomEnv) * (ai > bloomEnv ? bloomA : relA); double x = bloom > 0 ? inp * Math.Min(1, bloomEnv / (fastEnv + 1e-6)) : inp; for (int k = 0; k < 4; k++) x = Pre(k, x, 0.65);
            // read the lines (each drifts a little), damp them, mix them with a Hadamard matrix and write them back
            for (int k = 0; k < NL; k++) { modPh[k] += (0.1 + k * 0.037) / SR; if (modPh[k] >= 1) modPh[k] -= 1; double d = freeze ? Math.Round(len[k]) : len[k] + Math.Sin(2 * Math.PI * modPh[k]) * mod * 24; var b = line[k]; double rp = lw[k] - d; if (rp < 0) rp += b.Length; int i0 = (int)rp; double fr = rp - i0; double v = b[i0] + (b[(i0 + 1) % b.Length] - b[i0]) * fr; lpS[k] += (v - lpS[k]) * dampC; outs[k] = lpS[k]; }
            double s0 = outs[0] + outs[1], d0 = outs[0] - outs[1], s1 = outs[2] + outs[3], d1 = outs[2] - outs[3], s2 = outs[4] + outs[5], d2 = outs[4] - outs[5], s3 = outs[6] + outs[7], d3 = outs[6] - outs[7];
            double e0 = s0 + s1, e1 = d0 + d1, e2 = s0 - s1, e3 = d0 - d1, e4 = s2 + s3, e5 = d2 + d3, e6 = s2 - s3, e7 = d2 - d3; double hn = 0.35355339059;
            var mixd = mixB; mixd[0] = (e0 + e4) * hn; mixd[1] = (e1 + e5) * hn; mixd[2] = (e2 + e6) * hn; mixd[3] = (e3 + e7) * hn; mixd[4] = (e0 - e4) * hn; mixd[5] = (e1 - e5) * hn; mixd[6] = (e2 - e6) * hn; mixd[7] = (e3 - e7) * hn;
            double sh = shimmer > 0 ? shim.Tick((outs[0] + outs[3] + outs[5]) * 0.33, shift, 0, SR) * shimmer : 0;
            for (int k = 0; k < NL; k++) { var b = line[k]; b[lw[k]] = (float)Math.Max(-4, Math.Min(4, x * 0.5 + (mixd[k] + (k < 2 ? sh : 0)) * g)); lw[k] = (lw[k] + 1) % b.Length; }
            double wl = (outs[0] + outs[2] + outs[4] + outs[6]) * 0.5, wr = (outs[1] + outs[3] + outs[5] + outs[7]) * 0.5; double m = (wl + wr) * 0.5, s = (wl - wr) * 0.5 * width; wl = m + s; wr = m - s;
            lpL += (wl - lpL) * highC; lpR += (wr - lpR) * highC; hpL += (lpL - hpL) * lowC; hpR += (lpR - hpR) * lowC; wl = lpL - hpL; wr = lpR - hpR; lvl = Math.Max(lvl, Math.Abs(wl));
            l[i] = (float)((xl * (1 - mixW) + wl * mixW) * outG); r[i] = (float)((xr * (1 - mixW) + wr * mixW) * outG);
        }
        ShowLevel = (float)lvl;
    }
}

class DreamscapeWindow : PxWindow
{
    readonly DreamscapeFx e; readonly float[] hist = new float[160]; int hw;
    public DreamscapeWindow(DreamscapeFx f) : base(f, "Dreamscape", new Size(940, 600)) { e = f; Accent = Cyan; }
    protected override string SubTitle { get { return "a huge ambient space"; } }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        // the tail: how much of the space is ringing, scrolling by
        hist[hw] = e.ShowLevel; hw = (hw + 1) % hist.Length; var pic = new RectangleF(a.X, a.Y, a.Width, 160); Fill(g, Well, pic, 10); bool fz = e.Get("freeze") >= 0.5f;
        for (int i = 0; i < hist.Length; i++) { float v = Math.Min(1, hist[(hw + i) % hist.Length] * 2.5f), x = pic.X + 10 + i * (pic.Width - 20) / hist.Length, hh = (pic.Height - 30) * v; using (var b = new SolidBrush(Color.FromArgb(60 + (int)(150 * v), fz ? Gold : Cyan))) g.FillRectangle(b, x, pic.Bottom - 10 - hh, (pic.Width - 20) / hist.Length - 1, hh); }
        Txt(g, fz ? "FROZEN" : "THE SPACE", 12, fz ? Gold : Ink, new RectangleF(pic.X + 14, pic.Y + 8, 200, 16), StringAlignment.Near, true);
        var p1 = new RectangleF(a.X, pic.Bottom + 10, a.Width * 0.5f - 5, a.Bottom - pic.Bottom - 10); Panel(g, p1, "SPACE", Cyan); float k1 = p1.Width / 4;
        Knob(g, p1.X + k1 * 0.5f, p1.Y + 110, 26, "size", "SIZE", Cyan, true); Knob(g, p1.X + k1 * 1.5f, p1.Y + 110, 26, "decay", "DECAY", Cyan, true); Knob(g, p1.X + k1 * 2.5f, p1.Y + 110, 22, "damp", "DAMP", Cyan); Knob(g, p1.X + k1 * 3.5f, p1.Y + 110, 22, "bloom", "BLOOM", Lime);
        Knob(g, p1.X + k1 * 0.5f, p1.Y + 240, 20, "mod", "DRIFT", Violet); Knob(g, p1.X + k1 * 1.5f, p1.Y + 240, 20, "lowcut", "LOW CUT", Cyan); Knob(g, p1.X + k1 * 2.5f, p1.Y + 240, 20, "highcut", "HIGH CUT", Cyan); Knob(g, p1.X + k1 * 3.5f, p1.Y + 240, 20, "width", "WIDTH", Violet);
        var p2 = new RectangleF(p1.Right + 10, p1.Y, a.Right - p1.Right - 10, p1.Height); Panel(g, p2, "SHIMMER AND FREEZE", Pink);
        Knob(g, p2.X + p2.Width * 0.25f, p2.Y + 110, 30, "shimmer", "SHIMMER", Pink, true); Choice(g, new RectangleF(p2.X + p2.Width * 0.5f, p2.Y + 70, p2.Width * 0.45f, 26), "shift", Pink, 10);
        Txt(g, "FREEZE", 10, Dim, new RectangleF(p2.X + p2.Width * 0.5f, p2.Y + 110, 140, 14), StringAlignment.Near, true); Toggle(g, new RectangleF(p2.X + p2.Width * 0.5f, p2.Y + 128, p2.Width * 0.45f, 36), "freeze", Gold, "FROZEN", "FREEZE");
        Knob(g, p2.X + p2.Width * 0.3f, p2.Y + 240, 20, "mix", "MIX", Ink); Knob(g, p2.X + p2.Width * 0.7f, p2.Y + 240, 20, "out", "OUTPUT", Ink);
    }
}

// ======================================================================
//  PURPHEX MORPH: blends this track with another one, by their spectrums. MORPH crossfades what the two sound like (not just their volume);
//  VOCODER puts the shape of the other track (a voice) on this one (a synth, a pad); GATE lets through only the parts of this track that are
//  also in the other one. SOURCE picks the other track (the vocal track to start). FORMANT moves the voice shape up or down.
// ======================================================================
class MorphFx : Fx
{
    public static readonly string[] Modes = { "Morph", "Vocoder", "Gate" };
    const int NF = 1024, HOP = 256; readonly double[] win = new double[NF];
    sealed class Ch { public readonly float[] inBuf = new float[NF], modBuf = new float[NF], outAcc = new float[NF * 2]; public readonly double[] re = new double[NF], im = new double[NF], mre = new double[NF], mim = new double[NF], mSm = new double[NF / 2 + 1], cSm = new double[NF / 2 + 1]; public readonly float[] dry = new float[NF]; public int dw; public void Reset() { Array.Clear(inBuf, 0, NF); Array.Clear(modBuf, 0, NF); Array.Clear(outAcc, 0, outAcc.Length); Array.Clear(mSm, 0, mSm.Length); Array.Clear(cSm, 0, cSm.Length); Array.Clear(dry, 0, NF); dw = 0; } }
    readonly Ch cl = new Ch(), cr = new Ch(); int fill; readonly double[] envTmp = new double[NF / 2 + 1], envTmp2 = new double[NF / 2 + 1]; long modCnt;
    int mode; double amt, smooth, attC, relC, sens, formant, mixW, outG; public float[] TestMod;   // (a test can feed its own source)
    public volatile float ShowMod; public volatile bool NoSource = true;
    public override bool WantsSidechain { get { return true; } }
    public override int SidechainTrack { get { return (int)Get("src"); } }
    public override int Latency { get { return NF; } }
    public MorphFx()
    {
        Type = "pxmorph"; Title = "Purphex Morph"; Category = "Purphex VST"; Blurb = "Morph or vocode this track with another one (pick any track as the source)";
        AddP("src", "Source track", 0, 32, 0, ""); AddC("mode", "Mode", Modes, 0); AddP("amt", "Amount", 0, 100, 60, "%"); AddP("smooth", "Smoothing", 0, 100, 40, "%");
        AddP("att", "Attack", 1, 200, 10, "ms"); AddP("rel", "Release", 10, 1000, 120, "ms"); AddP("sens", "Sensitivity", -80, -10, -50, "dB"); AddP("formant", "Formant", -12, 12, 0, "st");
        AddP("mix", "Mix", 0, 100, 100, "%"); AddP("out", "Output", -24, 12, 0, "dB");
        for (int i = 0; i < NF; i++) win[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / NF);
    }
    public override void Reset() { base.Reset(); cl.Reset(); cr.Reset(); fill = 0; modCnt = 0; }
    protected override void Update()
    {
        mode = (int)Get("mode"); amt = Get("amt") / 100.0; smooth = Get("smooth") / 100.0; double hopT = HOP / (double)SR; attC = 1 - Math.Exp(-hopT / (Get("att") * 0.001)); relC = 1 - Math.Exp(-hopT / (Get("rel") * 0.001));
        sens = Math.Pow(10, Get("sens") / 20); formant = Math.Pow(2, Get("formant") / 12.0); mixW = Get("mix") / 100.0; outG = Math.Pow(10, Get("out") / 20);
    }
    void Frame(Ch c)
    {
        for (int i = 0; i < NF; i++) { c.re[i] = c.inBuf[i] * win[i]; c.im[i] = 0; c.mre[i] = c.modBuf[i] * win[i]; c.mim[i] = 0; }
        Fft.Transform(c.re, c.im, false); Fft.Transform(c.mre, c.mim, false); int H = NF / 2; double norm = NF / 4.0;
        for (int k = 0; k <= H; k++) { double mm = Math.Sqrt(c.mre[k] * c.mre[k] + c.mim[k] * c.mim[k]) / norm; c.mSm[k] += (mm - c.mSm[k]) * (mm > c.mSm[k] ? attC : relC); double cm = Math.Sqrt(c.re[k] * c.re[k] + c.im[k] * c.im[k]) / norm; c.cSm[k] += (cm - c.cSm[k]) * 0.5; }
        // the spectral envelopes (smoothed across frequency, and the source one moved by FORMANT)
        int rad = 1 + (int)(smooth * 24);
        for (int k = 0; k <= H; k++) { double s = 0, s2 = 0; int c0 = 0; double src = k / formant; int ks = (int)src; for (int q = -rad; q <= rad; q++) { int j = ks + q, j2 = k + q; if (j >= 0 && j <= H) { s += c.mSm[j]; c0++; } if (j2 >= 0 && j2 <= H) s2 += c.cSm[j2]; } envTmp[k] = c0 > 0 ? s / c0 : 0; envTmp2[k] = s2 / (2 * rad + 1); }
        for (int k = 0; k <= H; k++)
        {
            double cm = Math.Sqrt(c.re[k] * c.re[k] + c.im[k] * c.im[k]) / norm + 1e-12, g = 1;
            if (mode == 0) { double mmag = c.mSm[k]; double target = Math.Exp((1 - amt) * Math.Log(cm) + amt * Math.Log(mmag + 1e-9)); g = target / cm; }
            else if (mode == 1) { double ratio = envTmp[k] / (envTmp2[k] + 1e-6); g = 1 - amt + amt * Math.Min(30, ratio * 0.8); }
            else { g = envTmp[k] > sens ? 1 : 1 - amt; }
            g = Math.Min(40, g); c.re[k] *= g; c.im[k] *= g; if (k > 0 && k < H) { c.re[NF - k] = c.re[k]; c.im[NF - k] = -c.im[k]; }
        }
        Fft.Transform(c.re, c.im, true); const double sc = 1.0 / 1.5;   // (the inverse already divides by N; Hann times Hann at 75% overlap adds up to 1.5)
        for (int i = 0; i < NF; i++) c.outAcc[i] += (float)(c.re[i] * win[i] * sc);
    }
    // one hop: drop the hop of output just played, add the new frame, slide the input along
    void Advance(Ch c) { Array.Copy(c.outAcc, HOP, c.outAcc, 0, c.outAcc.Length - HOP); Array.Clear(c.outAcc, c.outAcc.Length - HOP, HOP); Frame(c); Array.Copy(c.inBuf, HOP, c.inBuf, 0, NF - HOP); Array.Copy(c.modBuf, HOP, c.modBuf, 0, NF - HOP); }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); float[] ml = ScL, mr = ScR; NoSource = ml == null && TestMod == null; double peak = 0;
        for (int i = 0; i < n; i++)
        {
            double sl = 0, sr = 0; if (ml != null) { sl = ml[i]; sr = mr != null ? mr[i] : sl; } else if (TestMod != null) { sl = sr = TestMod[modCnt % TestMod.Length]; } modCnt++; peak = Math.Max(peak, Math.Abs(sl));
            cl.inBuf[NF - HOP + fill] = l[i]; cr.inBuf[NF - HOP + fill] = r[i]; cl.modBuf[NF - HOP + fill] = (float)sl; cr.modBuf[NF - HOP + fill] = (float)sr;
            double yl = cl.outAcc[fill], yr = cr.outAcc[fill]; double dl = cl.dry[cl.dw], dr = cr.dry[cr.dw]; cl.dry[cl.dw] = l[i]; cr.dry[cr.dw] = r[i]; cl.dw = (cl.dw + 1) % NF; cr.dw = (cr.dw + 1) % NF;   // the dry signal, delayed by exactly the latency
            if (NoSource) { yl = dl; yr = dr; }
            l[i] = (float)((dl * (1 - mixW) + yl * mixW) * outG); r[i] = (float)((dr * (1 - mixW) + yr * mixW) * outG);
            if (++fill == HOP)
            {
                fill = 0; Advance(cl); Advance(cr);
            }
        }
        ShowMod = (float)peak;
    }
}

class MorphWindow : PxWindow
{
    readonly MorphFx e;
    public MorphWindow(MorphFx f) : base(f, "Morph", new Size(920, 580)) { e = f; Accent = Pink; }
    protected override string SubTitle { get { return "this track, shaped by another"; } }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        // the source: any track of the project
        var top = new RectangleF(a.X, a.Y, a.Width, 150); Panel(g, top, "SOURCE (THE OTHER TRACK)", Pink);
        var eng = StudioEngine.Current; var rows = new List<string>(); if (eng != null) lock (eng.Sync) foreach (var t in eng.Tracks) if (t.Host == null) rows.Add(t.Name);
        int cur = (int)e.Get("src"); float bx = top.X + 16, by = top.Y + 34; var vr = new RectangleF(bx, by, 150, 28); Button(g, vr, "VOCAL TRACK", cur == 0, Pink, () => { Mark(); e.Set("src", 0); }, "Use the project's vocal track (the default)", 10.5f); bx += 158;
        for (int i = 0; i < rows.Count && i < 32; i++) { string nm = (i + 1) + "  " + rows[i]; float w = Math.Min(190, 30 + nm.Length * 7.2f); if (bx + w > top.Right - 16) { bx = top.X + 16; by += 34; if (by > top.Bottom - 34) break; } var r = new RectangleF(bx, by, w, 28); int ii = i + 1; Button(g, r, nm, cur == ii, Cyan, () => { Mark(); e.Set("src", ii); }, "Use track " + (i + 1) + " as the source", 10); bx += w + 8; }
        if (rows.Count == 0) Txt(g, "Add the plugin to a track in a project: the tracks show here.", 11, Dim, new RectangleF(top.X + 180, top.Y + 34, top.Width - 200, 28), StringAlignment.Near, false);
        Txt(g, e.NoSource ? "No sound from the source yet (play the song)" : "Source level " + (20 * Math.Log10(Math.Max(1e-5, e.ShowMod))).ToString("0") + " dB", 10, e.NoSource ? Dim : Lime, new RectangleF(top.X + 16, top.Bottom - 22, 400, 14), StringAlignment.Near, false);
        var p1 = new RectangleF(a.X, top.Bottom + 10, a.Width * 0.55f - 5, a.Bottom - top.Bottom - 10); Panel(g, p1, "", Pink);
        Txt(g, "MODE", 10, Dim, new RectangleF(p1.X + 16, p1.Y + 12, 100, 14), StringAlignment.Near, true); Segments(g, new RectangleF(p1.X + 16, p1.Y + 30, p1.Width - 32, 30), "mode", Pink, 11);
        Knob(g, p1.X + p1.Width * 0.25f, p1.Y + 160, 34, "amt", "AMOUNT", Pink, true); Knob(g, p1.X + p1.Width * 0.6f, p1.Y + 150, 22, "smooth", "SMOOTHING", Violet); Knob(g, p1.X + p1.Width * 0.85f, p1.Y + 150, 22, "formant", "FORMANT", Gold);
        Knob(g, p1.X + p1.Width * 0.6f, p1.Y + 250, 18, "att", "ATTACK", Violet); Knob(g, p1.X + p1.Width * 0.85f, p1.Y + 250, 18, "rel", "RELEASE", Violet);
        var p2 = new RectangleF(p1.Right + 10, p1.Y, a.Right - p1.Right - 10, p1.Height); Panel(g, p2, "", Violet);
        Knob(g, p2.X + p2.Width * 0.5f, p2.Y + 90, 22, "sens", "GATE SENSITIVITY", Lime); Knob(g, p2.X + p2.Width * 0.3f, p2.Y + 230, 20, "mix", "MIX", Ink); Knob(g, p2.X + p2.Width * 0.7f, p2.Y + 230, 20, "out", "OUTPUT", Ink);
    }
}
