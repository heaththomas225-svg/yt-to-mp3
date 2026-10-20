using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ======================================================================
//  PURPHEX PRISM: makes any sound ring at musical notes. A NOTE (from the knob or the keyboard in the window) and up to three more INTERVALS
//  each get a bank of tuned resonators (UNISON copies, spread by DETUNE), so drums, noise or a voice turn into chords.
//  HARMONICS picks all, only even or only odd overtones; DECAY is how long they ring; HF and LF DAMP darken or thin the ring;
//  BLUR smears the sound before it rings; SHIFT moves everything by semitones and FINE by cents; FREEZE holds the ring forever,
//  KILL stops it at once. GATE ignores quiet input, PAN spreads the copies, LIMITER keeps the level safe, INPUT / OUTPUT and DRY / WET.
// ======================================================================
class PrismFx : Fx
{
    public static readonly string[] Notes = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
    const int MaxU = 5, MaxN = 4, Len = 4096;
    readonly float[][] comb = new float[MaxN * MaxU][]; readonly int[] cw = new int[MaxN * MaxU]; readonly double[] lpS = new double[MaxN * MaxU], hpS = new double[MaxN * MaxU], hpX = new double[MaxN * MaxU];
    readonly double[] ap = new double[8]; readonly float[][] apb = new float[4][]; readonly int[] apw = new int[4]; static readonly int[] ApLen = { 142, 107, 379, 277 };
    double inG = 1, outG = 1, mixS = 0.33, limG = 1, gateEnv; bool killed;
    public readonly PxScope Scope = new PxScope(8192); public volatile float OutPk;
    readonly double[] dly = new double[MaxN * MaxU], pan = new double[MaxN * MaxU]; int nActive;
    public PrismFx()
    {
        Type = "pxprism"; Title = "Purphex Prism"; Category = "Purphex VST"; Blurb = "Spectral resonator: any sound rings at notes and chords";
        AddP("note", "Note", 24, 84, 35, ""); AddC("i1_on", "Interval 1", new[] { "Off", "On" }, 1); AddP("i1", "Interval 1", -24, 24, 3, "st"); AddC("i2_on", "Interval 2", new[] { "Off", "On" }, 1); AddP("i2", "Interval 2", -24, 24, 7, "st"); AddC("i3_on", "Interval 3", new[] { "Off", "On" }, 0); AddP("i3", "Interval 3", -24, 24, 10, "st");
        AddP("detune", "Detune", 0, 100, 18, "%"); AddP("unison", "Unison", 1, 5, 3, ""); AddP("fine", "Fine", -100, 100, 0, "ct"); AddP("shift", "Shift", -24, 24, 0, "st");
        AddC("harm", "Harmonics", new[] { "All", "Even", "Odd" }, 0); AddP("decay", "Decay", 20, 8000, 628, "ms"); AddP("hfd", "HF damp", 500, 20000, 12000, "Hz"); AddP("lfd", "LF damp", 10, 1000, 10, "Hz"); AddP("blur", "Blur", 0, 100, 0, "%");
        AddC("freeze", "Freeze", new[] { "Off", "On" }, 0); AddC("kill", "Kill", new[] { "Off", "On" }, 0); AddP("gate", "Gate", -90, -20, -90, "dB"); AddP("pan", "Pan spread", 0, 100, 50, "%"); AddC("limit", "Limiter", new[] { "Off", "On" }, 1);
        AddP("in", "Input", -24, 24, 0, "dB"); AddP("out", "Output", -24, 24, 0, "dB"); AddP("mix", "Dry/Wet", 0, 100, 33, "%");
        for (int i = 0; i < comb.Length; i++) comb[i] = new float[Len]; for (int i = 0; i < 4; i++) apb[i] = new float[ApLen[i] * 3];
    }
    public override void Reset() { base.Reset(); for (int i = 0; i < comb.Length; i++) { Array.Clear(comb[i], 0, Len); cw[i] = 0; lpS[i] = hpS[i] = hpX[i] = 0; } for (int i = 0; i < 4; i++) { Array.Clear(apb[i], 0, apb[i].Length); apw[i] = 0; } limG = 1; gateEnv = 0; }
    protected override void Update()
    {
        int u = (int)Math.Round(Get("unison")); int harm = (int)Get("harm"); double baseNote = Get("note") + Get("shift") + Get("fine") / 100.0, det = Get("detune") / 100.0 * 0.4, ps = Get("pan") / 100.0;
        double[] iv = { 0, Get("i1_on") >= 0.5f ? Get("i1") : double.NaN, Get("i2_on") >= 0.5f ? Get("i2") : double.NaN, Get("i3_on") >= 0.5f ? Get("i3") : double.NaN };
        nActive = 0;
        for (int n = 0; n < MaxN; n++)
        {
            if (double.IsNaN(iv[n])) continue;
            for (int k = 0; k < u; k++)
            {
                double spread = u == 1 ? 0 : (k / (double)(u - 1) - 0.5) * 2, note = baseNote + iv[n] + spread * det, hz = 440 * Math.Pow(2, (note - 69) / 12.0);
                double period = SR / hz; if (harm == 2) period *= 0.5; else if (harm == 1) period *= 0.5;   // odd: half the period with negative feedback; even: half the period
                int i = nActive++; dly[i] = Math.Max(2, Math.Min(Len - 2, period)); pan[i] = u == 1 ? 0.5 : 0.5 + spread * ps * 0.5;
            }
        }
    }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        int harm = (int)Get("harm"); bool freeze = Get("freeze") >= 0.5f, kill = Get("kill") >= 0.5f, limit = Get("limit") >= 0.5f;
        if (kill && !killed) { for (int i = 0; i < comb.Length; i++) { Array.Clear(comb[i], 0, Len); lpS[i] = hpS[i] = hpX[i] = 0; } limG = 1; } killed = kill;
        double decayS = Get("decay") * 0.001, hfA = 1 - Math.Exp(-2 * Math.PI * Math.Min(Get("hfd"), SR * 0.45) / SR), lfA = Math.Exp(-2 * Math.PI * Get("lfd") / SR), blur = Get("blur") / 100.0;
        double gateLin = Math.Pow(10, Get("gate") / 20), inT = Math.Pow(10, Get("in") / 20), outT = Math.Pow(10, Get("out") / 20), mixT = Get("mix") / 100.0, gs = 1 - Math.Exp(-1.0 / (0.01 * SR));
        float pk = 0; double norm = 0.35 / Math.Max(1, Math.Sqrt(nActive));
        for (int i = 0; i < n; i++)
        {
            inG += (inT - inG) * gs; outG += (outT - outG) * gs; mixS += (mixT - mixS) * gs;
            double xl = l[i], xr = r[i], x = (xl + xr) * 0.5 * inG; gateEnv = Math.Max(Math.Abs(x), gateEnv * 0.9995); if (gateEnv < gateLin) x = 0;
            if (blur > 0) { for (int k = 0; k < 4; k++) { var b = apb[k]; int len = (int)(ApLen[k] * SR / 44100.0); int w = apw[k] % len; double bo = b[w], y = -x * 0.6 + bo; b[w] = (float)(x + bo * 0.6); apw[k] = w + 1; x = x + (y - x) * blur; } }
            if (freeze || kill) x = 0;
            double ol = 0, or = 0;
            for (int c = 0; c < nActive; c++)
            {
                var buf = comb[c]; double d = dly[c]; double fb = freeze ? 0.9999 : Math.Pow(0.001, d / SR / Math.Max(0.02, decayS)); if (harm == 2) fb = -fb;
                double rp = cw[c] - d; if (rp < 0) rp += Len; int i0 = (int)rp, i1 = (i0 + 1) % Len; double fr = rp - i0, o = buf[i0] + (buf[i1] - buf[i0]) * fr;
                lpS[c] += (o - lpS[c]) * hfA; hpS[c] = lfA * (hpS[c] + lpS[c] - hpX[c]); hpX[c] = lpS[c]; double v = hpS[c];
                buf[cw[c]] = (float)(x + v * fb); cw[c] = (cw[c] + 1) % Len;
                ol += v * (1 - pan[c]) * 2 * norm; or += v * pan[c] * 2 * norm;
            }
            if (limit) { double p = Math.Max(Math.Abs(ol), Math.Abs(or)) * limG; if (p > 0.9) limG = 0.9 / Math.Max(Math.Abs(ol), Math.Abs(or)); else limG = Math.Min(1, limG + (1 - limG) * 0.0002); ol *= limG; or *= limG; }
            double yl = (xl + (ol - xl) * mixS) * outG, yr = (xr + (or - xr) * mixS) * outG;
            if (double.IsNaN(yl) || double.IsNaN(yr) || double.IsInfinity(yl) || double.IsInfinity(yr)) { Reset(); yl = xl; yr = xr; }
            l[i] = (float)yl; r[i] = (float)yr; pk = (float)Math.Max(pk, Math.Max(Math.Abs(yl), Math.Abs(yr))); Scope.Push((float)((ol + or) * 0.5));
        }
        OutPk = Math.Max(OutPk * 0.85f, pk);
    }
}

#if !PURPHEX_CORE
class PrismWindow : PxWindow
{
    readonly PrismFx p;
    public PrismWindow(PrismFx f) : base(f, "Prism", new Size(1000, 700)) { p = f; Accent = Pink; }
    protected override string SubTitle { get { return "spectral resonator"; } }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        // the rainbow spectrum of the ring
        var sp = new RectangleF(a.X + 230, a.Y + 6, a.Width - 460, 230); Fill(g, Color.FromArgb(12, 8, 26), sp, 10);
        { var db = p.Scope.Spectrum(4096); for (float x = sp.X + 2; x < sp.Right - 2; x += 3) { double f = 40 * Math.Pow(300, (x - sp.X) / sp.Width); int bin = (int)(f * 4096 / p.SR); if (bin < 1 || bin >= db.Length) continue; float h = (float)Math.Max(0, Math.Min(1, (db[bin] + 90) / 90)) * (sp.Height - 8);
          float hue = (x - sp.X) / sp.Width; using (var pen = new Pen(HueColor(hue), 2)) g.DrawLine(pen, x, sp.Bottom - 4, x, sp.Bottom - 4 - h); } }
        // voicing on the left
        var vc = new RectangleF(a.X, a.Y, 220, 400); Panel(g, vc, "VOICING", Pink);
        Knob(g, vc.X + 110, vc.Y + 66, 34, "note", "NOTE", Pink, true); Txt(g, PrismFx.Notes[(int)Math.Round(fx.Get("note")) % 12] + ((int)Math.Round(fx.Get("note")) / 12 - 1), 13, Gold, new RectangleF(vc.X + 60, vc.Y + 140, 100, 16), StringAlignment.Center, true);
        for (int k = 1; k <= 3; k++) { float y = vc.Y + 170 + (k - 1) * 75; Toggle(g, new RectangleF(vc.X + 16, y + 10, 50, 22), "i" + k + "_on", Pink); Knob(g, vc.X + 130, y + 22, 20, "i" + k, "INTERVAL " + k, Pink); }
        // the main knobs
        var mk = new RectangleF(a.X + 230, sp.Bottom + 10, a.Width - 460, 160); Panel(g, mk, "", Violet);
        string[] ks = { "detune", "unison", "fine", "shift", "blur" }, ls = { "DETUNE", "UNISON", "FINE", "SHIFT", "BLUR" }; Color[] cs = { Cyan, Cyan, Cyan, Gold, Violet }; float kw = mk.Width / 5;
        for (int i = 0; i < 5; i++) Knob(g, mk.X + kw * i + kw / 2, mk.Y + 56, 26, ks[i], ls[i], cs[i]);
        Txt(g, "HARMONICS", 10, Dim, new RectangleF(mk.X + 16, mk.Bottom - 34, 80, 24), StringAlignment.Near, true); Segments(g, new RectangleF(mk.X + 100, mk.Bottom - 34, 200, 24), "harm", Gold, 10);
        // the right column: decay, damping, freeze, kill, levels
        var rc = new RectangleF(a.Right - 220, a.Y, 220, 400); Panel(g, rc, "", Cyan);
        Knob(g, rc.X + 60, rc.Y + 60, 22, "hfd", "HF DAMP", Cyan); Knob(g, rc.X + 160, rc.Y + 60, 22, "lfd", "LF DAMP", Cyan); Knob(g, rc.X + 110, rc.Y + 160, 32, "decay", "DECAY", Cyan, true);
        Toggle(g, new RectangleF(rc.X + 20, rc.Y + 240, 85, 28), "freeze", Cyan, "FROZEN", "FREEZE"); Toggle(g, new RectangleF(rc.X + 115, rc.Y + 240, 85, 28), "kill", Pink, "KILLED", "KILL");
        Knob(g, rc.X + 60, rc.Y + 320, 18, "gate", "GATE", Cyan); Knob(g, rc.X + 160, rc.Y + 320, 18, "pan", "PAN", Cyan); Toggle(g, new RectangleF(rc.X + 70, rc.Y + 370, 80, 22), "limit", Gold, "LIMITER", "LIMITER");
        HSlider(g, new RectangleF(a.X + 230, mk.Bottom + 26, 200, 8), "in", "INPUT", Violet); HSlider(g, new RectangleF(a.X + 450, mk.Bottom + 26, 200, 8), "out", "OUTPUT", Violet); HSlider(g, new RectangleF(a.X + 670, mk.Bottom + 26, 100, 8), "mix", "DRY / WET", Pink);
        // the keyboard: click a key to set the note
        var kb = new RectangleF(a.X, a.Bottom - 120, a.Width, 110); int whites = 35; float ww = kb.Width / whites; int[] wn = { 0, 2, 4, 5, 7, 9, 11 }; int root = (int)Math.Round(fx.Get("note"));
        for (int i = 0; i < whites; i++) { int note = 24 + 12 * (i / 7) + wn[i % 7]; var kr = new RectangleF(kb.X + i * ww, kb.Y, ww - 1.5f, kb.Height); Fill(g, Lit(note, root) ? HueColor((note - 24) / 60f) : Color.FromArgb(232, 228, 240), kr, 3); int nn = note; Reg(kr, () => { Mark(); fx.Set("note", nn); }, "Set the note to " + PrismFx.Notes[note % 12] + (note / 12 - 1)); }
        for (int i = 0; i < whites; i++) { int k = i % 7; if (k == 2 || k == 6) continue; int note = 24 + 12 * (i / 7) + wn[k] + 1; var kr = new RectangleF(kb.X + i * ww + ww * 0.65f, kb.Y, ww * 0.7f, kb.Height * 0.6f); Fill(g, Lit(note, root) ? HueColor((note - 24) / 60f) : Color.FromArgb(28, 22, 46), kr, 2); int nn = note; Reg(kr, () => { Mark(); fx.Set("note", nn); }, "Set the note to " + PrismFx.Notes[note % 12] + (note / 12 - 1)); }
    }
    bool Lit(int note, int root) { if (note == root) return true; for (int k = 1; k <= 3; k++) if (fx.Get("i" + k + "_on") >= 0.5f && note == root + (int)Math.Round(fx.Get("i" + k))) return true; return false; }
    static Color HueColor(float h) { h = Math.Max(0, Math.Min(1, h)) * 300; int i = (int)(h / 60) % 6; float f = h / 60 - (int)(h / 60); int q = (int)(255 * (1 - f)), t = (int)(255 * f); switch (i) { case 0: return Color.FromArgb(255, t, 60); case 1: return Color.FromArgb(q, 255, 60); case 2: return Color.FromArgb(60, 255, t); case 3: return Color.FromArgb(60, q, 255); default: return Color.FromArgb(t, 60, 255); } }
}
#endif
