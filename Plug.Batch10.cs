using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// ======================================================================
//  The wavetables of Purphex Current: eight tables of 32 frames, each frame a single cycle built from harmonics, made once and shared.
//  Every frame is kept at seven levels of brightness (fewer harmonics for higher notes) so high notes never alias.
// ======================================================================
static class PxTables
{
    public static readonly string[] Names = { "Basic (sine to saw to square)", "Pulse width", "Vowels", "Harmonic sweep", "Digital", "Metallic", "Organ drawbars", "Hollow" };
    public const int Frames = 32, Size = 1024, Levels = 7;
    static float[][][][] tabs;   // [table][frame][level][sample]
    static readonly object lk = new object();
    public static float[][][][] Get() { if (tabs != null) return tabs; lock (lk) { if (tabs == null) tabs = Build(); } return tabs; }
    static double Amp(int t, double p, int h)
    {
        switch (t)
        {
            case 0: { double saw = 1.0 / h, sq = h % 2 == 1 ? 1.0 / h : 0, sine = h == 1 ? 1 : 0; return p < 0.5 ? sine + (saw - sine) * (p * 2) : saw + (sq - saw) * ((p - 0.5) * 2); }
            case 1: { double w = 0.5 - p * 0.45; return 2.0 / (Math.PI * h) * Math.Sin(Math.PI * h * w); }
            case 2: { double[][] F = { new[] { 800.0, 1150 }, new[] { 400.0, 1600 }, new[] { 350.0, 2300 }, new[] { 450.0, 800 }, new[] { 325.0, 700 } }; double q = p * 4; int a = (int)Math.Min(3, q); double fr = q - a; double f1 = F[a][0] + (F[a + 1][0] - F[a][0]) * fr, f2 = F[a][1] + (F[a + 1][1] - F[a][1]) * fr; double hz = h * 110; return (Math.Exp(-Math.Pow((hz - f1) / 120, 2)) + 0.7 * Math.Exp(-Math.Pow((hz - f2) / 180, 2)) + 0.05) / Math.Sqrt(h); }
            case 3: { double c = 1 + p * 40; return Math.Exp(-Math.Pow((h - c) / (2 + p * 6), 2)) / Math.Sqrt(h) + (h == 1 ? 0.3 : 0); }
            case 4: { int bits = 1 + (int)(p * 7); return ((h * 2654435761u >> (32 - bits)) & 1) == 1 ? 1.0 / Math.Sqrt(h) : 0.1 / h; }
            case 5: { double ratio = 1 + p * 2.5; double best = 1e9; for (int k = 1; k < 12; k++) best = Math.Min(best, Math.Abs(h - Math.Pow(k, ratio * 0.5 + 0.5))); return Math.Exp(-best * 2) / Math.Sqrt(h); }
            case 6: { int[] bars = { 1, 2, 3, 4, 6, 8, 10, 12, 16 }; int on = 1 + (int)(p * 8); for (int k = 0; k < on && k < bars.Length; k++) if (bars[k] == h) return 1.0 / (1 + k * 0.3); return 0; }
            default: { double odd = h % 2 == 1 ? 1.0 / h : 0, even = h % 2 == 0 ? 1.0 / h : 0; return odd * (1 - p) + even * p + (h == 1 ? 0.2 : 0); }
        }
    }
    static float[][][][] Build()
    {
        var all = new float[Names.Length][][][]; var re = new double[Size]; var im = new double[Size];
        for (int t = 0; t < Names.Length; t++)
        {
            all[t] = new float[Frames][][];
            for (int f = 0; f < Frames; f++)
            {
                all[t][f] = new float[Levels][]; double p = f / (double)(Frames - 1);
                for (int lv = 0; lv < Levels; lv++)
                {
                    int maxH = Math.Max(1, (Size / 2 - 1) >> lv); Array.Clear(re, 0, Size); Array.Clear(im, 0, Size);
                    for (int h = 1; h <= maxH; h++) { double a = Amp(t, p, h); if (a == 0) continue; im[h] = -a * 0.5; im[Size - h] = a * 0.5; }   // sines
                    Fft.Transform(re, im, true); double pk = 1e-9; for (int i = 0; i < Size; i++) pk = Math.Max(pk, Math.Abs(re[i]));
                    var w = all[t][f][lv] = new float[Size + 1]; for (int i = 0; i < Size; i++) w[i] = (float)(re[i] / pk * 0.9); w[Size] = w[0];
                }
            }
        }
        return all;
    }
    // one sample of a table at a frame position (0..1) and phase (0..1), at the right brightness for the pitch
    public static double Read(float[][][] tab, double pos, double ph, double dt)
    {
        int lv = 0; double hmax = (Size / 2 - 1); while (lv < Levels - 1 && dt * hmax > 0.45) { hmax /= 2; lv++; }
        double fp = Math.Max(0, Math.Min(Frames - 1.0001, pos * (Frames - 1))); int f0 = (int)fp; double ff = fp - f0; double x = ph * Size; int i0 = (int)x; double fr = x - i0; if (i0 >= Size) i0 = Size - 1;
        var a = tab[f0][lv]; var b = tab[f0 + 1][lv]; double va = a[i0] + (a[i0 + 1] - a[i0]) * fr, vb = b[i0] + (b[i0 + 1] - b[i0]) * fr; return va + (vb - va) * ff;
    }
}

// ======================================================================
//  PURPHEX CURRENT: a wavetable synth. Two oscillators, each with a TABLE and a POSITION that morphs through it, OCTAVE, SEMI, UNISON
//  (up to 7 voices), DETUNE and SPREAD; a SUB and NOISE; a FILTER (low-pass, high-pass, band-pass, notch) with its own envelope; the AMP
//  envelope; a second envelope and an LFO that move whatever you route them to in the three MOD slots.
// ======================================================================
class CurrentFx : InstrumentFx
{
    public static readonly string[] FTypes = { "Low-pass", "High-pass", "Band-pass", "Notch" }, MSrc = { "Off", "Mod envelope", "LFO", "Velocity", "Note" }, MDst = { "Position A", "Position B", "Cutoff", "Pitch", "Level A", "Level B", "Detune", "Resonance" }, LShapes = { "Sine", "Triangle", "Saw", "Square", "Random" };
    class Voice { public bool On, Rel; public int Note; public double Vel, Cur, Target, Env, Env2, S1L, S2L, S1R, S2R; public int St, St2; public long Age; public readonly double[] phA = new double[7], phB = new double[7]; public double subPh; }
    readonly Voice[] voices = new Voice[8]; List<KeyValuePair<int, MidiEv>> pending; readonly List<int> held = new List<int>(); long age; double lfoPh, lfoRnd; uint seed = 3;
    public readonly PxScope Scope = new PxScope(4096); public volatile float ShowLfo, ShowEnv2;
    public CurrentFx()
    {
        Type = "pxcurrent"; Title = "Purphex Current"; Category = "Instruments"; Blurb = "Wavetable synth: two morphing oscillators, unison, filter, LFO and mod slots";
        foreach (var o in new[] { "a", "b" }) { bool A = o == "a"; AddC(o + "_tab", "Osc " + o.ToUpper() + " table", PxTables.Names, A ? 0 : 2); AddP(o + "_pos", "Osc " + o.ToUpper() + " position", 0, 100, A ? 40 : 20, "%"); AddP(o + "_oct", "Osc " + o.ToUpper() + " octave", -3, 3, A ? 0 : -1, ""); AddP(o + "_semi", "Osc " + o.ToUpper() + " semitones", -12, 12, 0, "st");
            AddP(o + "_lvl", "Osc " + o.ToUpper() + " level", 0, 100, A ? 80 : 50, "%"); AddP(o + "_uni", "Osc " + o.ToUpper() + " unison", 1, 7, A ? 3 : 1, ""); AddP(o + "_det", "Osc " + o.ToUpper() + " detune", 0, 100, 25, "%"); AddP(o + "_spr", "Osc " + o.ToUpper() + " spread", 0, 100, 70, "%"); }
        AddP("sub", "Sub", 0, 100, 0, "%"); AddP("noise", "Noise", 0, 100, 0, "%");
        AddC("ftype", "Filter", FTypes, 0); AddP("cut", "Cutoff", 20, 20000, 6000, "Hz"); AddP("res", "Resonance", 0, 100, 20, "%"); AddP("fenv", "Filter envelope", -100, 100, 30, "%"); AddP("drive", "Filter drive", 0, 100, 0, "%"); AddP("track", "Key tracking", 0, 100, 50, "%");
        AddP("a", "Attack", 0.5f, 5000, 4, "ms"); AddP("d", "Decay", 5, 10000, 600, "ms"); AddP("s", "Sustain", 0, 100, 70, "%"); AddP("r", "Release", 5, 10000, 400, "ms");
        AddP("a2", "Mod attack", 0.5f, 5000, 10, "ms"); AddP("d2", "Mod decay", 5, 10000, 800, "ms"); AddP("s2", "Mod sustain", 0, 100, 0, "%"); AddP("r2", "Mod release", 5, 10000, 400, "ms");
        AddP("lrate", "LFO rate", 0.05f, 20, 2, "Hz"); AddC("lshape", "LFO shape", LShapes, 0);
        for (int k = 0; k < 3; k++) { AddC("m" + k + "_src", "Mod " + (k + 1) + " source", MSrc, k == 0 ? 1 : k == 1 ? 2 : 0); AddC("m" + k + "_dst", "Mod " + (k + 1) + " target", MDst, k == 0 ? 2 : k == 1 ? 0 : 3); AddP("m" + k + "_amt", "Mod " + (k + 1) + " amount", -100, 100, 0, "%"); }
        AddP("glide", "Glide", 0, 1000, 0, "ms"); AddC("mono", "Voices", new[] { "Poly", "Mono (slides)" }, 0); AddP("level", "Level", -24, 12, -4, "dB");
        for (int i = 0; i < voices.Length; i++) voices[i] = new Voice(); PxTables.Get();
    }
    public override bool IsInstrument { get { return true; } }
#if !PURPHEX_CORE
    public override void ShowWindow(IWin32Window owner) { PxWindow.Show(this, owner, () => new CurrentWindow(this)); }
#else
    public override void ShowWindow(IWin32Window owner) { }
#endif
    public override void CloseWindow() { }
    public override string WindowLabel { get { return "Open Purphex Current"; } }
    public override void Reset() { base.Reset(); foreach (var v in voices) { v.On = false; v.Env = v.Env2 = 0; v.S1L = v.S2L = v.S1R = v.S2R = 0; } held.Clear(); lfoPh = 0; }
    protected override void OnEvents(List<KeyValuePair<int, MidiEv>> ev) { pending = ev; }
    bool Mono { get { return Get("mono") >= 0.5f; } }
    void VOn(int note, int vel)
    {
        held.Remove(note); held.Add(note); Voice v; if (Mono) v = voices[0]; else { v = null; foreach (var x in voices) if (!x.On) { v = x; break; } if (v == null) { v = voices[0]; foreach (var x in voices) if (x.Age < v.Age) v = x; } }
        bool slide = Mono && v.On && !v.Rel && Get("glide") > 0; v.Note = note; v.Target = note; v.Vel = vel / 127.0; v.Age = ++age; v.Rel = false;
        if (!slide) { v.Cur = note; v.St = 1; v.St2 = 1; if (!v.On) { v.Env = v.Env2 = 0; v.S1L = v.S2L = v.S1R = v.S2R = 0; for (int u = 0; u < 7; u++) { v.phA[u] = (u * 0.137) % 1; v.phB[u] = (u * 0.291) % 1; } } }
        v.On = true;
    }
    void VOff(int note)
    {
        held.Remove(note); if (Mono) { var v = voices[0]; if (v.On && v.Note == note) { if (held.Count > 0) { v.Note = held[held.Count - 1]; v.Target = v.Note; } else v.Rel = true; } return; }
        foreach (var v in voices) if (v.On && !v.Rel && v.Note == note) v.Rel = true;
    }
    void Event(MidiEv e) { int st = e.S & 0xF0; if (st == 0x90 && e.D2 > 0) VOn(e.D1, e.D2); else if (st == 0x80 || (st == 0x90 && e.D2 == 0)) VOff(e.D1); else if (st == 0xB0 && (e.D1 == 123 || e.D1 == 120)) { foreach (var v in voices) v.Rel = true; held.Clear(); if (e.D1 == 120) foreach (var v in voices) { v.On = false; v.Env = 0; } } }
    double Lfo() { double p = lfoPh; switch ((int)Get("lshape")) { case 1: return 1 - 4 * Math.Abs(p - 0.5); case 2: return 2 * p - 1; case 3: return p < 0.5 ? 1 : -1; case 4: return lfoRnd; default: return Math.Sin(2 * Math.PI * p); } }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); var ev = pending; pending = null; int ei = 0; var T = PxTables.Get();
        var tA = T[(int)Get("a_tab")]; var tB = T[(int)Get("b_tab")]; double posA0 = Get("a_pos") / 100, posB0 = Get("b_pos") / 100, lvA0 = Get("a_lvl") / 100, lvB0 = Get("b_lvl") / 100; int uA = (int)Math.Round(Get("a_uni")), uB = (int)Math.Round(Get("b_uni"));
        double detA = Get("a_det") / 100, detB = Get("b_det") / 100, sprA = Get("a_spr") / 100, sprB = Get("b_spr") / 100, offA = Get("a_oct") * 12 + Get("a_semi"), offB = Get("b_oct") * 12 + Get("b_semi"), subL = Get("sub") / 100, noiseL = Get("noise") / 100;
        int ft = (int)Get("ftype"); double cut0 = Get("cut"), res0 = Get("res") / 100, fenv = Get("fenv") / 100, drive = 1 + Get("drive") / 100 * 8, track = Get("track") / 100;
        double aS = Math.Max(1, Get("a") * 0.001 * SR), dC = Math.Exp(-4.6 / Math.Max(1, Get("d") * 0.001 * SR)), sus = Get("s") / 100, rC = Math.Exp(-4.6 / Math.Max(1, Get("r") * 0.001 * SR));
        double aS2 = Math.Max(1, Get("a2") * 0.001 * SR), dC2 = Math.Exp(-4.6 / Math.Max(1, Get("d2") * 0.001 * SR)), sus2 = Get("s2") / 100, rC2 = Math.Exp(-4.6 / Math.Max(1, Get("r2") * 0.001 * SR));
        double lrate = Get("lrate"), glideC = Get("glide") <= 0 ? 0 : Math.Exp(-1.0 / (Get("glide") * 0.001 * SR)), level = Math.Pow(10, Get("level") / 20);
        int[] ms = { (int)Get("m0_src"), (int)Get("m1_src"), (int)Get("m2_src") }, md = { (int)Get("m0_dst"), (int)Get("m1_dst"), (int)Get("m2_dst") }; double[] ma = { Get("m0_amt") / 100, Get("m1_amt") / 100, Get("m2_amt") / 100 };
        for (int i = 0; i < n; i++)
        {
            while (ev != null && ei < ev.Count && ev[ei].Key <= i) { Event(ev[ei].Value); ei++; }
            lfoPh += lrate / SR; if (lfoPh >= 1) { lfoPh -= 1; seed = seed * 1664525 + 1013904223; lfoRnd = (seed >> 8) / 8388608.0 - 1; } double lf = Lfo(); double sl = 0, sr = 0;
            foreach (var v in voices)
            {
                if (!v.On) continue;
                if (v.Rel) { v.Env *= rC; v.Env2 *= rC2; if (v.Env < 0.0003) { v.On = false; v.Env = 0; continue; } }
                else { if (v.St == 1) { v.Env += 1.0 / aS; if (v.Env >= 1) { v.Env = 1; v.St = 2; } } else v.Env = sus + (v.Env - sus) * dC; if (v.St2 == 1) { v.Env2 += 1.0 / aS2; if (v.Env2 >= 1) { v.Env2 = 1; v.St2 = 2; } } else v.Env2 = sus2 + (v.Env2 - sus2) * dC2; }
                if (glideC > 0) v.Cur = v.Target + (v.Cur - v.Target) * glideC; else v.Cur = v.Target;
                // the three mod slots
                double mPosA = 0, mPosB = 0, mCut = 0, mPitch = 0, mLvA = 0, mLvB = 0, mDet = 0, mRes = 0;
                for (int k = 0; k < 3; k++) { if (ms[k] == 0 || ma[k] == 0) continue; double src = ms[k] == 1 ? v.Env2 : ms[k] == 2 ? lf : ms[k] == 3 ? v.Vel : (v.Cur - 60) / 24.0; double m = src * ma[k]; switch (md[k]) { case 0: mPosA += m; break; case 1: mPosB += m; break; case 2: mCut += m * 5; break; case 3: mPitch += m * 12; break; case 4: mLvA += m; break; case 5: mLvB += m; break; case 6: mDet += m; break; case 7: mRes += m; break; } }
                double note = v.Cur + mPitch, sumL = 0, sumR = 0;
                for (int o = 0; o < 2; o++)
                {
                    int U = o == 0 ? uA : uB; double lv = Math.Max(0, Math.Min(1, (o == 0 ? lvA0 + mLvA : lvB0 + mLvB))); if (lv <= 0) continue; var tab = o == 0 ? tA : tB; var ph = o == 0 ? v.phA : v.phB; double pos = Math.Max(0, Math.Min(1, (o == 0 ? posA0 + mPosA : posB0 + mPosB)));
                    double det = Math.Max(0, Math.Min(1, (o == 0 ? detA : detB) + mDet)), spr = o == 0 ? sprA : sprB, baseHz = 440 * Math.Pow(2, (note + (o == 0 ? offA : offB) - 69) / 12.0), g = lv / Math.Sqrt(U);
                    for (int u = 0; u < U; u++) { double off = U == 1 ? 0 : (u / (double)(U - 1) - 0.5) * 2; double hz = baseHz * Math.Pow(2, off * det * 0.5 / 12), dt = Math.Min(0.45, hz / SR); ph[u] += dt; if (ph[u] >= 1) ph[u] -= 1; double s = PxTables.Read(tab, pos, ph[u], dt) * g; double pan = 0.5 + off * spr * 0.5; sumL += s * Math.Cos(pan * Math.PI / 2) * 1.414; sumR += s * Math.Sin(pan * Math.PI / 2) * 1.414; }
                }
                if (subL > 0) { double hz = 440 * Math.Pow(2, (note - 12 - 69) / 12.0); v.subPh += hz / SR; if (v.subPh >= 1) v.subPh -= 1; double s = Math.Sin(2 * Math.PI * v.subPh) * subL; sumL += s; sumR += s; }
                if (noiseL > 0) { seed = seed * 1664525 + 1013904223; double nz = ((seed >> 8) / 8388608.0 - 1) * noiseL * 0.5; sumL += nz; sumR += nz; }
                // the filter (one per side), moved by its envelope, the note and the mod slots
                double fc = Math.Max(20, Math.Min(SR * 0.45, cut0 * Math.Pow(2, fenv * 6 * v.Env + mCut) * Math.Pow(Math.Pow(2, (note - 60) / 12.0), track))), gg = Math.Tan(Math.PI * fc / SR), res = Math.Max(0, Math.Min(0.98, res0 + mRes)), kk = 2 - 1.95 * res;
                sumL = Svf(Math.Tanh(sumL * drive) / Math.Tanh(drive), gg, kk, ref v.S1L, ref v.S2L, ft); sumR = Svf(Math.Tanh(sumR * drive) / Math.Tanh(drive), gg, kk, ref v.S1R, ref v.S2R, ft);
                double amp = v.Env * (0.5 + 0.5 * v.Vel) * 0.5; sl += sumL * amp; sr += sumR * amp; ShowEnv2 = (float)v.Env2;
            }
            sl *= level; sr *= level; if (double.IsNaN(sl + sr) || double.IsInfinity(sl + sr)) { Reset(); sl = sr = 0; }
            l[i] = (float)sl; r[i] = (float)sr; Scope.Push((float)((sl + sr) * 0.5));
        }
        while (ev != null && ei < ev.Count) { Event(ev[ei].Value); ei++; }
        ShowLfo = (float)Lfo();
    }
    static double Svf(double x, double g, double k, ref double s1, ref double s2, int t) { double hp = (x - (k + g) * s1 - s2) / (1 + g * (g + k)); double v1 = g * hp, bp = v1 + s1; s1 = bp + v1; double v2 = g * bp, lp = v2 + s2; s2 = lp + v2; return t == 1 ? hp : t == 2 ? bp * k : t == 3 ? lp + hp : lp; }
}

#if !PURPHEX_CORE
abstract class PxInstWindow : PxWindow
{
    readonly InstrumentFx inst; int octave = 3; readonly HashSet<int> down = new HashSet<int>();
    protected PxInstWindow(InstrumentFx f, string name, Size size) : base(f, name, size) { inst = f; }
    protected void Keys(Graphics g, RectangleF r)
    {
        var kb = new RectangleF(r.X, r.Y, r.Width - 76, r.Height); int whites = 14; float ww = kb.Width / whites; int[] wn = { 0, 2, 4, 5, 7, 9, 11 };
        for (int i = 0; i < whites; i++)
        {
            int note = 12 * (octave + 1) + 12 * (i / 7) + wn[i % 7]; var kr = new RectangleF(kb.X + i * ww, kb.Y, ww - 2, kb.Height); bool on = down.Contains(note);
            Fill(g, on ? Accent : Color.FromArgb(230, 228, 240), kr, 4); int nn = note; var h = Reg(kr, () => Play(nn), "Click to play"); h.Up = () => Stop(nn); h.DragAt = pt => { };
            if (i % 7 == 0) Txt(g, "C" + (octave + i / 7), 9, Color.FromArgb(80, 70, 110), new RectangleF(kr.X, kr.Bottom - 15, kr.Width, 13), StringAlignment.Center, true);
        }
        for (int i = 0; i < whites; i++)
        {
            int k = i % 7; if (k == 2 || k == 6) continue; int note = 12 * (octave + 1) + 12 * (i / 7) + wn[k] + 1; var kr = new RectangleF(kb.X + i * ww + ww * 0.65f, kb.Y, ww * 0.7f, kb.Height * 0.6f); bool on = down.Contains(note);
            Fill(g, on ? Accent : Color.FromArgb(30, 22, 50), kr, 3); int nn = note; var h = Reg(kr, () => Play(nn), "Click to play"); h.Up = () => Stop(nn); h.DragAt = pt => { };
        }
        float bx = kb.Right + 8, bh = (r.Height - 8) / 2;
        Button(g, new RectangleF(bx, r.Y, 68, bh), "OCT +", false, Accent, () => octave = Math.Min(6, octave + 1), "Keyboard an octave higher", 10);
        Button(g, new RectangleF(bx, r.Y + bh + 8, 68, bh), "OCT -", false, Accent, () => octave = Math.Max(0, octave - 1), "Keyboard an octave lower", 10);
    }
    void Play(int n) { if (down.Add(n)) inst.NoteOn(n, 100); }
    void Stop(int n) { if (down.Remove(n)) inst.NoteOff(n); }
    protected override void OnFormClosed(FormClosedEventArgs e) { foreach (var n in new List<int>(down)) inst.NoteOff(n); down.Clear(); base.OnFormClosed(e); }
}
#endif

#if !PURPHEX_CORE
class CurrentWindow : PxInstWindow
{
    readonly CurrentFx e;
    public CurrentWindow(CurrentFx f) : base(f, "Current", new Size(1040, 760)) { e = f; Accent = Cyan; }
    protected override string SubTitle { get { return "wavetable synth"; } }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        float ow = (a.Width - 10) / 2; var T = PxTables.Get();
        for (int o = 0; o < 2; o++)
        {
            string p = o == 0 ? "a_" : "b_"; Color c = o == 0 ? Cyan : Pink; var r = new RectangleF(a.X + o * (ow + 10), a.Y, ow, 320); Panel(g, r, "OSC " + (o == 0 ? "A" : "B"), c);
            Choice(g, new RectangleF(r.X + 14, r.Y + 32, r.Width - 28, 24), p + "tab", c, 10);
            // the wave at its position, drawn from the real table
            var wv = new RectangleF(r.X + 14, r.Y + 64, r.Width - 28, 80); Fill(g, Well, wv, 8); var tab = T[(int)e.Get(p + "tab")]; double pos = e.Get(p + "pos") / 100; var pts = new PointF[120];
            for (int i = 0; i < 120; i++) { double v = PxTables.Read(tab, pos, i / 120.0, 0.0001); pts[i] = new PointF(wv.X + 6 + i * (wv.Width - 12) / 119f, wv.Y + wv.Height / 2 - (float)v * (wv.Height / 2 - 6)); } using (var pen = new Pen(c, 2f)) g.DrawLines(pen, pts);
            float kw = r.Width / 4, ky = r.Y + 184, ky2 = r.Y + 266;
            Knob(g, r.X + kw * 0.5f, ky, 20, p + "pos", "POSITION", c); Knob(g, r.X + kw * 1.5f, ky, 18, p + "lvl", "LEVEL", c); Knob(g, r.X + kw * 2.5f, ky, 18, p + "oct", "OCTAVE", c); Knob(g, r.X + kw * 3.5f, ky, 18, p + "semi", "SEMI", c);
            Knob(g, r.X + kw * 0.5f, ky2, 16, p + "uni", "UNISON", c); Knob(g, r.X + kw * 1.5f, ky2, 16, p + "det", "DETUNE", c); Knob(g, r.X + kw * 2.5f, ky2, 16, p + "spr", "SPREAD", c);
        }
        var fr = new RectangleF(a.X, a.Y + 330, 330, 220); Panel(g, fr, "FILTER", Violet); Choice(g, new RectangleF(fr.X + 14, fr.Y + 32, 150, 24), "ftype", Violet, 10);
        Knob(g, fr.X + 55, fr.Y + 98, 22, "cut", "CUTOFF", Violet); Knob(g, fr.X + 135, fr.Y + 98, 18, "res", "RES", Violet); Knob(g, fr.X + 210, fr.Y + 98, 18, "fenv", "ENV", Violet); Knob(g, fr.X + 283, fr.Y + 98, 18, "drive", "DRIVE", Violet);
        Knob(g, fr.X + 55, fr.Y + 172, 14, "track", "KEY TRACK", Violet); Knob(g, fr.X + 135, fr.Y + 172, 14, "sub", "SUB", Gold); Knob(g, fr.X + 210, fr.Y + 172, 14, "noise", "NOISE", Gold);
        var en = new RectangleF(fr.Right + 10, fr.Y, 330, 220); Panel(g, en, "AMP AND MOD ENVELOPES", Lime); float ex = en.Width / 4;
        string[] e1 = { "a", "d", "s", "r" }, e2 = { "a2", "d2", "s2", "r2" }, lb = { "A", "D", "S", "R" };
        for (int k = 0; k < 4; k++) { Knob(g, en.X + ex * (k + 0.5f), en.Y + 76, 16, e1[k], lb[k], Lime); Knob(g, en.X + ex * (k + 0.5f), en.Y + 162, 16, e2[k], "MOD " + lb[k], Gold); }
        var mo = new RectangleF(en.Right + 10, fr.Y, a.Right - en.Right - 10, 220); Panel(g, mo, "LFO AND MOD SLOTS", Gold);
        Knob(g, mo.X + 36, mo.Y + 52, 14, "lrate", "RATE", Gold); Choice(g, new RectangleF(mo.X + 70, mo.Y + 40, mo.Width - 84, 22), "lshape", Gold, 9.5f);
        float cw = (mo.Width - 104) / 2;
        for (int k = 0; k < 3; k++) { float y = mo.Y + 112 + k * 32; Choice(g, new RectangleF(mo.X + 12, y, cw, 22), "m" + k + "_src", Gold, 9); Choice(g, new RectangleF(mo.X + 16 + cw, y, cw, 22), "m" + k + "_dst", Cyan, 9); HSlider(g, new RectangleF(mo.Right - 80, y + 12, 68, 8), "m" + k + "_amt", "", Gold); }
        var bot = new RectangleF(a.X, fr.Bottom + 10, a.Width, a.Bottom - fr.Bottom - 10); Panel(g, bot, "", Cyan); float by = bot.Y + bot.Height / 2 - 6;
        Knob(g, bot.X + 60, by, 16, "glide", "GLIDE", Cyan); Choice(g, new RectangleF(bot.X + 110, by - 12, 150, 24), "mono", Cyan, 10); Knob(g, bot.Right - 70, by, 18, "level", "LEVEL", Ink);
        Keys(g, new RectangleF(bot.X + 280, bot.Y + 10, bot.Width - 400, bot.Height - 20));
    }
}
#endif

// ======================================================================
//  PURPHEX INFERNO: a growl and wobble bass. OSC (saw, square, supersaw or FM) with a second oscillator for a reese (DETUNE), a GROWL filter
//  (low-pass, band-pass or vowels) wobbled by an LFO in time with the song, and HEAT: distortion (tube, hard, fold, crush) after the filter.
// ======================================================================
class InfernoFx : InstrumentFx
{
    public static readonly string[] Waves = { "Saw", "Square", "Supersaw", "FM" }, Filters = { "Low-pass", "Band-pass", "Vowel (growl)" }, Rates = { "1/1", "1/2", "1/4", "1/8", "1/16", "1/4 triplet", "1/8 triplet" }, Heat = { "Tube", "Hard", "Fold", "Crush" };
    static readonly double[] RB = { 4, 2, 1, 0.5, 0.25, 2.0 / 3, 1.0 / 3 };
    class Voice { public bool On, Rel; public int Note; public double Vel, Cur, Target, Env, S1, S2, F1a, F1b, F2a, F2b, fmPh, ph2; public readonly double[] ph = new double[7]; public int St; public long Age; }
    readonly Voice[] voices = new Voice[4]; List<KeyValuePair<int, MidiEv>> pending; readonly List<int> held = new List<int>(); long age; double hold, lp; int hc; long freeCnt; long bpos; bool trans;
    public readonly PxScope Scope = new PxScope(4096); public volatile float ShowWob;
    public InfernoFx()
    {
        Type = "pxinferno"; Title = "Purphex Inferno"; Category = "Instruments"; Blurb = "Growl and wobble bass: reese, growl filter wobbled in time, and HEAT distortion";
        AddC("wave", "Wave", Waves, 0); AddP("oct", "Octave", -3, 2, -1, ""); AddP("fm", "FM amount", 0, 100, 40, "%"); AddP("det", "Reese detune", 0, 100, 30, "%"); AddP("osc2", "Second oscillator", 0, 100, 60, "%");
        AddC("ftype", "Growl filter", Filters, 2); AddP("cut", "Cutoff", 40, 12000, 900, "Hz"); AddP("res", "Resonance", 0, 100, 55, "%"); AddC("rate", "Wobble rate", Rates, 3); AddP("wob", "Wobble depth", 0, 100, 70, "%"); AddP("fenv", "Filter envelope", 0, 100, 20, "%");
        AddC("heat", "Heat type", Heat, 0); AddP("drive", "Heat", 0, 100, 45, "%"); AddP("hmix", "Heat mix", 0, 100, 100, "%"); AddP("width", "Width", 0, 100, 40, "%");
        AddP("a", "Attack", 0.5f, 2000, 3, "ms"); AddP("r", "Release", 5, 3000, 120, "ms"); AddP("glide", "Glide", 0, 500, 40, "ms"); AddP("level", "Level", -24, 12, -6, "dB");
        for (int i = 0; i < voices.Length; i++) voices[i] = new Voice();
    }
    public override bool IsInstrument { get { return true; } }
#if !PURPHEX_CORE
    public override void ShowWindow(IWin32Window owner) { PxWindow.Show(this, owner, () => new InfernoWindow(this)); }
#else
    public override void ShowWindow(IWin32Window owner) { }
#endif
    public override void CloseWindow() { }
    public override string WindowLabel { get { return "Open Purphex Inferno"; } }
    public override void Reset() { base.Reset(); foreach (var v in voices) { v.On = false; v.Env = 0; v.S1 = v.S2 = v.F1a = v.F1b = v.F2a = v.F2b = 0; } held.Clear(); hold = lp = 0; hc = 0; }
    protected override void OnEvents(List<KeyValuePair<int, MidiEv>> ev) { pending = ev; }
    protected override void OnBlock(long pos, int n, bool transport) { bpos = pos; trans = transport; }
    void VOn(int note, int vel) { held.Remove(note); held.Add(note); var v = voices[0]; bool slide = v.On && !v.Rel && Get("glide") > 0; v.Note = note; v.Target = note; v.Vel = vel / 127.0; v.Rel = false; if (!slide) { v.Cur = note; v.St = 1; if (!v.On) { v.Env = 0; v.S1 = v.S2 = v.F1a = v.F1b = v.F2a = v.F2b = 0; } } v.On = true; }
    void VOff(int note) { held.Remove(note); var v = voices[0]; if (v.On && v.Note == note) { if (held.Count > 0) { v.Note = held[held.Count - 1]; v.Target = v.Note; } else v.Rel = true; } }
    void Event(MidiEv e) { int st = e.S & 0xF0; if (st == 0x90 && e.D2 > 0) VOn(e.D1, e.D2); else if (st == 0x80 || (st == 0x90 && e.D2 == 0)) VOff(e.D1); else if (st == 0xB0 && (e.D1 == 123 || e.D1 == 120)) { foreach (var v in voices) v.Rel = true; held.Clear(); if (e.D1 == 120) foreach (var v in voices) { v.On = false; v.Env = 0; } } }
    static double Blep(double t, double dt) { if (t < dt) { t /= dt; return t + t - t * t - 1; } if (t > 1 - dt) { t = (t - 1) / dt; return t * t + t + t + 1; } return 0; }
    static double Band(double x, double g, double k, ref double s1, ref double s2) { double hp = (x - (k + g) * s1 - s2) / (1 + g * (g + k)); double v1 = g * hp, bp = v1 + s1; s1 = bp + v1; double v2 = g * bp, lp = v2 + s2; s2 = lp + v2; return bp; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); var ev = pending; pending = null; int ei = 0; int wave = (int)Get("wave"), ft = (int)Get("ftype"), heat = (int)Get("heat"); double oct = Get("oct") * 12, fm = Get("fm") / 100, det = Get("det") / 100, o2 = Get("osc2") / 100, cut = Get("cut"), res = Get("res") / 100, wob = Get("wob") / 100, fenv = Get("fenv") / 100;
        double drive = 1 + Get("drive") / 100 * 25, hmix = Get("hmix") / 100, width = Get("width") / 100, aS = Math.Max(1, Get("a") * 0.001 * SR), rC = Math.Exp(-4.6 / Math.Max(1, Get("r") * 0.001 * SR)), glideC = Get("glide") <= 0 ? 0 : Math.Exp(-1.0 / (Get("glide") * 0.001 * SR)), level = Math.Pow(10, Get("level") / 20);
        double beats = RB[(int)Get("rate")]; double spb = 60.0 / Math.Max(20, Fx.Bpm) * SR; double wobLast = 0;
        for (int i = 0; i < n; i++)
        {
            while (ev != null && ei < ev.Count && ev[ei].Key <= i) { Event(ev[ei].Value); ei++; }
            long sp = trans ? bpos + i : freeCnt + i; double wph = (sp / spb / beats) % 1.0, wv = 0.5 - 0.5 * Math.Cos(2 * Math.PI * wph); wobLast = wv; double outL = 0, outR = 0;
            foreach (var v in voices)
            {
                if (!v.On) continue; if (v.Rel) { v.Env *= rC; if (v.Env < 0.0003) { v.On = false; continue; } } else if (v.St == 1) { v.Env += 1.0 / aS; if (v.Env >= 1) { v.Env = 1; v.St = 2; } }
                if (glideC > 0) v.Cur = v.Target + (v.Cur - v.Target) * glideC; else v.Cur = v.Target;
                double hz = 440 * Math.Pow(2, (v.Cur + oct - 69) / 12.0), dt = Math.Min(0.45, hz / SR), s = 0;
                int U = wave == 2 ? 7 : 1;
                for (int u = 0; u < U; u++)
                {
                    double off = U == 1 ? 0 : (u / 6.0 - 0.5) * 2, d2 = dt * Math.Pow(2, off * 0.3 / 12); v.ph[u] += d2; if (v.ph[u] >= 1) v.ph[u] -= 1; double p = v.ph[u], o;
                    if (wave == 3) { v.fmPh += dt * 2; if (v.fmPh >= 1) v.fmPh -= 1; o = Math.Sin(2 * Math.PI * p + fm * 6 * Math.Sin(2 * Math.PI * v.fmPh)); }
                    else if (wave == 1) { o = (p < 0.5 ? 1 : -1) + Blep(p, d2); double t2 = p + 0.5; if (t2 >= 1) t2 -= 1; o -= Blep(t2, d2); }
                    else o = 2 * p - 1 - Blep(p, d2);
                    s += o / Math.Sqrt(U);
                }
                if (o2 > 0) { double d3 = dt * Math.Pow(2, det * 0.6 / 12); v.ph2 += d3; if (v.ph2 >= 1) v.ph2 -= 1; s += (2 * v.ph2 - 1 - Blep(v.ph2, d3)) * o2; }
                // the growl: cutoff wobbled in time with the song
                double fc = Math.Max(40, Math.Min(SR * 0.45, cut * Math.Pow(2, wob * 4 * (wv - 0.5) + fenv * 4 * v.Env))), g = Math.Tan(Math.PI * fc / SR), k = 2 - 1.9 * res, y;
                if (ft == 2) { double vow = Math.Max(0, Math.Min(1, wv)); double f1 = 300 + vow * 500, f2 = 900 + vow * 1300; double g1 = Math.Tan(Math.PI * Math.Min(SR * 0.45, f1 * fc / 900) / SR), g2 = Math.Tan(Math.PI * Math.Min(SR * 0.45, f2 * fc / 900) / SR), kk = 2 - 1.8 * res; y = (Band(s, g1, kk, ref v.F1a, ref v.F1b) + 0.8 * Band(s, g2, kk, ref v.F2a, ref v.F2b)) * kk * 1.4; }
                else { double hp = (s - (k + g) * v.S1 - v.S2) / (1 + g * (g + k)); double v1 = g * hp, bp = v1 + v.S1; v.S1 = bp + v1; double v2 = g * bp, lpv = v2 + v.S2; v.S2 = lpv + v2; y = ft == 1 ? bp * k : lpv; }
                // HEAT
                double x = y * drive, h;
                switch (heat) { case 1: h = Math.Max(-1, Math.Min(1, x)); break; case 2: h = Math.Sin(x * 0.8); break; case 3: { if (++hc >= 2 + (int)(drive / 4)) { hc = 0; hold = Math.Round(Math.Tanh(x) * 12) / 12; } h = hold; break; } default: h = Math.Tanh(x); break; }
                h /= Math.Sqrt(drive) * 0.5 + 0.5; lp += (h - lp) * 0.6; double wet = y + (lp - y) * hmix, amp = v.Env * (0.6 + 0.4 * v.Vel) * 0.55;
                double sideAmt = width * Math.Sin(2 * Math.PI * (v.ph2 * 0.5 + 0.25)) * 0.2; outL += wet * amp * (1 + sideAmt); outR += wet * amp * (1 - sideAmt);
            }
            outL *= level; outR *= level; if (double.IsNaN(outL + outR) || double.IsInfinity(outL + outR)) { Reset(); outL = outR = 0; }
            l[i] = (float)outL; r[i] = (float)outR; Scope.Push((float)((outL + outR) * 0.5));
        }
        while (ev != null && ei < ev.Count) { Event(ev[ei].Value); ei++; }
        freeCnt += n; ShowWob = (float)wobLast;
    }
}

#if !PURPHEX_CORE
class InfernoWindow : PxInstWindow
{
    readonly InfernoFx e;
    public InfernoWindow(InfernoFx f) : base(f, "Inferno", new Size(960, 640)) { e = f; Accent = Orange; }
    protected override string SubTitle { get { return "growl and wobble bass"; } }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        var sc = new RectangleF(a.X, a.Y, a.Width, 130); Fill(g, Well, sc, 10); var buf = e.Scope.Buf; int w = e.Scope.W; var sp = new PointF[300]; for (int i = 0; i < 300; i++) { float v = buf[(w - 1200 + i * 4 + buf.Length * 2) % buf.Length]; sp[i] = new PointF(sc.X + 8 + i * (sc.Width - 16) / 299f, sc.Y + sc.Height / 2 - Math.Max(-1, Math.Min(1, v * 1.5f)) * (sc.Height / 2 - 8)); }
        using (var pen = new Pen(Color.FromArgb(220, Orange), 2f)) g.DrawLines(pen, sp); var wb = new RectangleF(sc.Right - 30, sc.Y + 10, 14, sc.Height - 20); Fill(g, Ctl, wb, 4); Fill(g, Pink, new RectangleF(wb.X, wb.Bottom - wb.Height * e.ShowWob, wb.Width, wb.Height * e.ShowWob), 4);
        float pw = (a.Width - 20) / 3, py = sc.Bottom + 10, ph = 290;
        var p1 = new RectangleF(a.X, py, pw, ph); Panel(g, p1, "OSCILLATOR", Orange); Choice(g, new RectangleF(p1.X + 14, p1.Y + 32, p1.Width - 28, 24), "wave", Orange, 10.5f);
        Knob(g, p1.X + p1.Width * 0.25f, p1.Y + 120, 22, "oct", "OCTAVE", Orange); Knob(g, p1.X + p1.Width * 0.75f, p1.Y + 120, 22, "fm", "FM", Orange); Knob(g, p1.X + p1.Width * 0.25f, p1.Y + 220, 22, "osc2", "OSC 2", Gold); Knob(g, p1.X + p1.Width * 0.75f, p1.Y + 220, 22, "det", "REESE", Gold);
        var p2 = new RectangleF(p1.Right + 10, py, pw, ph); Panel(g, p2, "GROWL", Pink); Choice(g, new RectangleF(p2.X + 14, p2.Y + 32, p2.Width / 2 - 20, 24), "ftype", Pink, 10.5f); Choice(g, new RectangleF(p2.X + p2.Width / 2 + 6, p2.Y + 32, p2.Width / 2 - 20, 24), "rate", Violet, 10.5f);
        Knob(g, p2.X + p2.Width * 0.25f, p2.Y + 120, 22, "cut", "CUTOFF", Pink); Knob(g, p2.X + p2.Width * 0.75f, p2.Y + 120, 22, "res", "RES", Pink); Knob(g, p2.X + p2.Width * 0.25f, p2.Y + 220, 22, "wob", "WOBBLE", Violet); Knob(g, p2.X + p2.Width * 0.75f, p2.Y + 220, 22, "fenv", "ENVELOPE", Pink);
        var p3 = new RectangleF(p2.Right + 10, py, pw, ph); Panel(g, p3, "HEAT", Gold); Choice(g, new RectangleF(p3.X + 14, p3.Y + 32, p3.Width - 28, 24), "heat", Gold, 10.5f);
        Knob(g, p3.X + p3.Width / 2, p3.Y + 130, 34, "drive", "HEAT", Gold, true); Knob(g, p3.X + p3.Width * 0.25f, p3.Y + 240, 18, "hmix", "MIX", Gold); Knob(g, p3.X + p3.Width * 0.75f, p3.Y + 240, 18, "width", "WIDTH", Violet);
        var bot = new RectangleF(a.X, py + ph + 10, a.Width, a.Bottom - py - ph - 10); Panel(g, bot, "", Violet); float by = bot.Y + bot.Height / 2 - 6;
        Knob(g, bot.X + 70, by, 18, "a", "ATTACK", Lime); Knob(g, bot.X + 160, by, 18, "r", "RELEASE", Lime); Knob(g, bot.X + 250, by, 18, "glide", "GLIDE", Cyan); Knob(g, bot.Right - 70, by, 18, "level", "LEVEL", Ink);
        Keys(g, new RectangleF(bot.X + 310, bot.Y + 10, bot.Width - 430, bot.Height - 20));
    }
}
#endif

// ======================================================================
//  PURPHEX CHORDPAD: pick a KEY and SCALE and eight pads give you the chords of that key. Play one key and a whole chord plays (one-finger
//  chords): the note you play picks the chord of that step of the scale. CHORDS (triads, 7ths, 9ths), VOICING, INVERSION, OCTAVE and STRUM
//  shape them. A built-in SOUND (pad, keys or pluck) plays them, and WRITE TO TRACK puts a progression of pads on the timeline as real notes.
// ======================================================================
class ChordPadFx : InstrumentFx
{
    public static readonly string[] Keys = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" }, Scales = { "Major", "Minor", "Dorian", "Mixolydian", "Harmonic minor", "Phrygian" }, Kinds = { "Triads", "7ths", "9ths", "Sus2", "Power (5ths)" }, Voicings = { "Close", "Open", "Spread", "Drop 2" }, Sounds = { "Warm pad", "Keys", "Pluck" };
    static readonly int[][] ScaleSteps = { new[] { 0, 2, 4, 5, 7, 9, 11 }, new[] { 0, 2, 3, 5, 7, 8, 10 }, new[] { 0, 2, 3, 5, 7, 9, 10 }, new[] { 0, 2, 4, 5, 7, 9, 10 }, new[] { 0, 2, 3, 5, 7, 8, 11 }, new[] { 0, 1, 3, 5, 7, 8, 10 } };
    public static readonly string[] ProgNames = { "I - V - vi - IV (pop)", "vi - IV - I - V (emotional)", "i - VI - III - VII (trap minor)", "ii - V - I - vi (soul)", "I - vi - IV - V (classic)", "i - iv - v - i (dark)" }; public static readonly int[][] Progs = { new[] { 0, 4, 5, 3 }, new[] { 5, 3, 0, 4 }, new[] { 0, 5, 2, 6 }, new[] { 1, 4, 0, 5 }, new[] { 0, 5, 3, 4 }, new[] { 0, 3, 4, 0 } };
    class Voice { public bool On, Rel; public int Note; public double Vel, Env, Ph1, Ph2, Ph3, S1, S2; public long Start; public int Delay; public readonly double[] ks = new double[2048]; public int kw; public double kLen; }
    readonly Voice[] voices = new Voice[24]; List<KeyValuePair<int, MidiEv>> pending; readonly Dictionary<int, int[]> playing = new Dictionary<int, int[]>(); uint seed = 9; long clock;
    public volatile int ShowPad = -1; public readonly PxScope Scope = new PxScope(4096);
    public ChordPadFx()
    {
        Type = "pxchord"; Title = "Purphex ChordPad"; Category = "Instruments"; Blurb = "Chords of any key on eight pads: one finger plays a chord, voicings, strum, write to track";
        AddC("key", "Key", Keys, 0); AddC("scale", "Scale", Scales, 0); AddC("kind", "Chords", Kinds, 0); AddC("voicing", "Voicing", Voicings, 0); AddP("inv", "Inversion", 0, 3, 0, ""); AddP("oct", "Octave", -2, 2, 0, ""); AddP("strum", "Strum", 0, 200, 0, "ms"); AddC("bass", "Bass note", new[] { "Off", "On" }, 1);
        AddC("sound", "Sound", Sounds, 0); AddP("bright", "Brightness", 0, 100, 50, "%"); AddP("att", "Attack", 1, 3000, 40, "ms"); AddP("rel", "Release", 10, 5000, 600, "ms"); AddC("prog", "Progression", ProgNames, 0); AddP("level", "Level", -24, 12, -6, "dB");
        for (int i = 0; i < voices.Length; i++) voices[i] = new Voice();
    }
    public override bool IsInstrument { get { return true; } }
#if !PURPHEX_CORE
    public override void ShowWindow(IWin32Window owner) { PxWindow.Show(this, owner, () => new ChordPadWindow(this)); }
#else
    public override void ShowWindow(IWin32Window owner) { }
#endif
    public override void CloseWindow() { }
    public override string WindowLabel { get { return "Open Purphex ChordPad"; } }
    public override void Reset() { base.Reset(); foreach (var v in voices) { v.On = false; v.Env = 0; } playing.Clear(); }
    protected override void OnEvents(List<KeyValuePair<int, MidiEv>> ev) { pending = ev; }
    // the notes of the chord on step "deg" (0..6) of the scale
    public int[] Chord(int deg)
    {
        int key = (int)Get("key"), kind = (int)Get("kind"), voicing = (int)Get("voicing"), inv = (int)Math.Round(Get("inv")); var sc = ScaleSteps[(int)Get("scale")]; int root = 48 + key + (int)Math.Round(Get("oct")) * 12;
        Func<int, int> step = s => { int o = s / 7; return root + sc[((s % 7) + 7) % 7] + o * 12; };
        var ns = new List<int>(); int[] stacks = kind == 1 ? new[] { 0, 2, 4, 6 } : kind == 2 ? new[] { 0, 2, 4, 6, 8 } : kind == 3 ? new[] { 0, 1, 4 } : kind == 4 ? new[] { 0, 4, 7 } : new[] { 0, 2, 4 };
        foreach (int s in stacks) ns.Add(kind == 4 && s == 7 ? step(deg) + 12 : step(deg + s));
        for (int i = 0; i < inv && i < ns.Count - 1; i++) { ns[i] += 12; } ns.Sort();
        if (voicing == 1 && ns.Count >= 3) { ns[1] += 12; ns.Sort(); } else if (voicing == 2) { for (int i = 1; i < ns.Count; i += 2) ns[i] += 12; ns.Sort(); } else if (voicing == 3 && ns.Count >= 3) { ns[ns.Count - 2] -= 12; ns.Sort(); }
        if (Get("bass") >= 0.5f) ns.Insert(0, step(deg) - 12);
        return ns.ToArray();
    }
    public string ChordName(int deg)
    {
        var ns = Chord(deg); int key = (int)Get("key"); var sc = ScaleSteps[(int)Get("scale")]; int rootPc = (key + sc[deg]) % 12; int third = (sc[(deg + 2) % 7] - sc[deg] + 12) % 12, fifth = (sc[(deg + 4) % 7] - sc[deg] + 12) % 12; int kind = (int)Get("kind");
        string q = kind == 3 ? "sus2" : kind == 4 ? "5" : third == 3 && fifth == 6 ? "dim" : third == 3 ? "m" : fifth == 8 ? "aug" : ""; if (kind == 1 || kind == 2) { int sev = (sc[(deg + 6) % 7] - sc[deg] + 12) % 12; q += sev == 11 && q == "" ? "maj7" : "7"; if (kind == 2) q = q.Replace("7", "9"); }
        return Keys[rootPc] + q;
    }
    public static readonly string[] Roman = { "I", "II", "III", "IV", "V", "VI", "VII" };
    // a pad pressed in the window (also with the computer keyboard): degree 0..6, or 7 = the first chord an octave up
    static readonly int[] White = { 0, 2, 4, 5, 7, 9, 11 };
    public void Pad(int deg, bool down) { int note = 60 + White[deg % 7] + (deg >= 7 ? 12 : 0); if (down) NoteOn(note, 100); else NoteOff(note); }
    void Start(int note, int vel)
    {
        int pc = note % 12, deg = Array.IndexOf(White, pc); if (deg < 0) deg = Array.IndexOf(White, pc - 1);   // the white keys are the steps of the scale (C = I, D = II...); a black key plays the step below it
        int shift = Math.Max(-12, Math.Min(12, (note / 12 - 5) * 12));   // from middle C up; an octave higher plays the chords an octave higher
        var ns = Chord(deg); for (int i = 0; i < ns.Length; i++) ns[i] += shift; playing[note] = ns; ShowPad = deg; double strum = Get("strum") * 0.001 * SR;
        for (int i = 0; i < ns.Length; i++) { Voice v = null; foreach (var x in voices) if (!x.On) { v = x; break; } if (v == null) { v = voices[0]; foreach (var x in voices) if (x.Start < v.Start) v = x; } v.On = true; v.Rel = false; v.Note = ns[i]; v.Vel = vel / 127.0; v.Env = 0; v.Start = ++clock; v.Delay = (int)(strum * i); v.Ph1 = v.Ph2 = v.Ph3 = 0; v.S1 = v.S2 = 0;
            double hz = 440 * Math.Pow(2, (v.Note - 69) / 12.0); v.kLen = Math.Min(2047, SR / hz); for (int k = 0; k < 2048; k++) { seed = seed * 1664525 + 1013904223; v.ks[k] = ((seed >> 8) / 8388608.0 - 1); } v.kw = 0; }
    }
    void Stop(int note) { int[] ns; if (!playing.TryGetValue(note, out ns)) return; playing.Remove(note); foreach (var v in voices) if (v.On && !v.Rel && Array.IndexOf(ns, v.Note) >= 0) v.Rel = true; if (playing.Count == 0) ShowPad = -1; }
    void Event(MidiEv e) { int st = e.S & 0xF0; if (st == 0x90 && e.D2 > 0) Start(e.D1, e.D2); else if (st == 0x80 || (st == 0x90 && e.D2 == 0)) Stop(e.D1); else if (st == 0xB0 && (e.D1 == 123 || e.D1 == 120)) { foreach (var v in voices) v.Rel = true; playing.Clear(); ShowPad = -1; if (e.D1 == 120) foreach (var v in voices) { v.On = false; v.Env = 0; } } }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); var ev = pending; pending = null; int ei = 0; int sound = (int)Get("sound"); double bright = Get("bright") / 100, aS = Math.Max(1, Get("att") * 0.001 * SR), rC = Math.Exp(-4.6 / Math.Max(1, Get("rel") * 0.001 * SR)), level = Math.Pow(10, Get("level") / 20);
        double fc = 300 * Math.Pow(40, bright), gF = Math.Tan(Math.PI * Math.Min(SR * 0.45, fc) / SR), kF = 1.2; double keysDecay = Math.Exp(-1.0 / (1.2 * SR));
        for (int i = 0; i < n; i++)
        {
            while (ev != null && ei < ev.Count && ev[ei].Key <= i) { Event(ev[ei].Value); ei++; }
            double sl = 0, sr = 0;
            foreach (var v in voices)
            {
                if (!v.On) continue; if (v.Delay > 0) { v.Delay--; continue; }
                if (v.Rel) { v.Env *= rC; if (v.Env < 0.0003) { v.On = false; continue; } } else { if (sound == 0) { v.Env += (1 - v.Env) / aS * 3; } else if (v.Env < 1 && v.Start > 0) { v.Env = Math.Min(1, v.Env + 1.0 / Math.Max(1, aS * 0.05)); if (v.Env >= 1) v.Start = -v.Start; } else v.Env *= sound == 1 ? keysDecay : 1; }
                double hz = 440 * Math.Pow(2, (v.Note - 69) / 12.0), dt = hz / SR, s;
                if (sound == 0) { v.Ph1 += dt; v.Ph2 += dt * 1.004; v.Ph3 += dt * 0.996; if (v.Ph1 >= 1) v.Ph1 -= 1; if (v.Ph2 >= 1) v.Ph2 -= 1; if (v.Ph3 >= 1) v.Ph3 -= 1; s = ((2 * v.Ph1 - 1) + (2 * v.Ph2 - 1) + (2 * v.Ph3 - 1)) / 3; }
                else if (sound == 1) { v.Ph1 += dt; if (v.Ph1 >= 1) v.Ph1 -= 1; double p = 2 * Math.PI * v.Ph1; s = Math.Sin(p) + 0.5 * Math.Sin(2 * p) * v.Env + 0.25 * Math.Sin(3 * p) * v.Env * v.Env + 0.12 * Math.Sin(5 * p) * v.Env * v.Env; }
                else { int L = Math.Max(2, (int)v.kLen), i0 = v.kw % L, i1 = (v.kw + 1) % L; double y = (v.ks[i0] + v.ks[i1]) * 0.5 * (0.98 + bright * 0.019); v.ks[i0] = y; v.kw = i1; s = y; }
                double hp = (s - (kF + gF) * v.S1 - v.S2) / (1 + gF * (gF + kF)); double v1 = gF * hp, bp = v1 + v.S1; v.S1 = bp + v1; double v2 = gF * bp, lp = v2 + v.S2; v.S2 = lp + v2; s = sound == 2 ? s * 0.6 + lp * 0.4 : lp;
                double amp = v.Env * (0.5 + 0.5 * v.Vel) * 0.22, pan = 0.5 + ((v.Note % 7) - 3) * 0.06; sl += s * amp * Math.Cos(pan * Math.PI / 2) * 1.414; sr += s * amp * Math.Sin(pan * Math.PI / 2) * 1.414;
            }
            sl *= level; sr *= level; if (double.IsNaN(sl + sr) || double.IsInfinity(sl + sr)) { Reset(); sl = sr = 0; }
            l[i] = (float)sl; r[i] = (float)sr; Scope.Push((float)((sl + sr) * 0.5));
        }
        while (ev != null && ei < ev.Count) { Event(ev[ei].Value); ei++; }
    }
    // WRITE TO TRACK: the chosen progression, one chord a bar from the start of the song, as real notes on this track
    public int WriteToTrack(Track t, long startFrame, int bars)
    {
        if (t == null) return 0; var prog = Progs[(int)Get("prog")]; double barFrames = 60.0 / Math.Max(20, Fx.Bpm) * StudioEngine.SR * 4; int count = 0;
        lock (t.Midi)
        {
            for (int b = 0; b < bars; b++) { int deg = prog[b % prog.Length]; int note = 60 + White[deg]; long f0 = startFrame + (long)(b * barFrames), f1 = startFrame + (long)((b + 1) * barFrames) - 64; t.Midi.Add(new MidiEv { Frame = f0, S = 0x90, D1 = (byte)note, D2 = 100 }); t.Midi.Add(new MidiEv { Frame = f1, S = 0x80, D1 = (byte)note, D2 = 0 }); count++; }
            t.Midi.Sort((a, b2) => a.Frame.CompareTo(b2.Frame));
        }
        return count;
    }
}

#if !PURPHEX_CORE
class ChordPadWindow : PxWindow
{
    readonly ChordPadFx e; int held = -1;
    public ChordPadWindow(ChordPadFx f) : base(f, "ChordPad", new Size(1000, 640)) { e = f; Accent = Violet; }
    protected override string SubTitle { get { return "chords of any key"; } }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        var top = new RectangleF(a.X, a.Y, a.Width, 70); Panel(g, top, "", Violet);
        Txt(g, "KEY", 10, Dim, new RectangleF(top.X + 14, top.Y + 8, 60, 14), StringAlignment.Near, true); Choice(g, new RectangleF(top.X + 14, top.Y + 26, 80, 26), "key", Violet, 11);
        Txt(g, "SCALE", 10, Dim, new RectangleF(top.X + 104, top.Y + 8, 100, 14), StringAlignment.Near, true); Choice(g, new RectangleF(top.X + 104, top.Y + 26, 160, 26), "scale", Violet, 11);
        Txt(g, "CHORDS", 10, Dim, new RectangleF(top.X + 274, top.Y + 8, 100, 14), StringAlignment.Near, true); Choice(g, new RectangleF(top.X + 274, top.Y + 26, 140, 26), "kind", Cyan, 11);
        Txt(g, "VOICING", 10, Dim, new RectangleF(top.X + 424, top.Y + 8, 100, 14), StringAlignment.Near, true); Choice(g, new RectangleF(top.X + 424, top.Y + 26, 120, 26), "voicing", Cyan, 11);
        Txt(g, "SOUND", 10, Dim, new RectangleF(top.X + 554, top.Y + 8, 100, 14), StringAlignment.Near, true); Choice(g, new RectangleF(top.X + 554, top.Y + 26, 130, 26), "sound", Gold, 11);
        Txt(g, "BASS NOTE", 10, Dim, new RectangleF(top.X + 694, top.Y + 8, 100, 14), StringAlignment.Near, true); Toggle(g, new RectangleF(top.X + 694, top.Y + 26, 90, 26), "bass", Lime);
        // the eight pads: the chords of the key (click and hold; the white keys from middle C play them too)
        float pw = (a.Width - 70) / 8f, py = top.Bottom + 12, ph = 170;
        for (int k = 0; k < 8; k++)
        {
            int deg = k % 7; var r = new RectangleF(a.X + k * (pw + 10), py, pw, ph); bool lit = e.ShowPad == deg && (k < 7 || held == 7) || held == k; Color c = k == 7 ? Gold : Violet;
            Fill(g, lit ? Color.FromArgb(255, c) : Ctl, r, 12); Stroke(g, lit ? Color.White : Color.FromArgb(160, c), r, 12, 1.5f);
            Txt(g, k == 7 ? "I (8va)" : ChordPadFx.Roman[deg], 11, lit ? Color.Black : Dim, new RectangleF(r.X, r.Y + 14, r.Width, 16), StringAlignment.Center, true);
            Txt(g, e.ChordName(deg), 20, lit ? Color.Black : Ink, new RectangleF(r.X, r.Y + 60, r.Width, 34), StringAlignment.Center, true);
            int kk = k; var h = Reg(r, () => { held = kk; e.Pad(kk, true); }, "Play " + e.ChordName(deg) + " (hold)"); h.DragAt = p => { }; h.Up = () => { if (held >= 0) e.Pad(held, false); held = -1; };
        }
        var p1 = new RectangleF(a.X, py + ph + 12, a.Width * 0.55f, a.Bottom - py - ph - 12); Panel(g, p1, "SHAPE AND SOUND", Cyan); float kw = p1.Width / 4;
        Knob(g, p1.X + kw * 0.5f, p1.Y + 90, 20, "inv", "INVERSION", Cyan); Knob(g, p1.X + kw * 1.5f, p1.Y + 90, 20, "oct", "OCTAVE", Cyan); Knob(g, p1.X + kw * 2.5f, p1.Y + 90, 20, "strum", "STRUM", Gold); Knob(g, p1.X + kw * 3.5f, p1.Y + 90, 20, "bright", "BRIGHTNESS", Gold);
        Knob(g, p1.X + kw * 0.5f, p1.Y + 200, 18, "att", "ATTACK", Lime); Knob(g, p1.X + kw * 1.5f, p1.Y + 200, 18, "rel", "RELEASE", Lime); Knob(g, p1.X + kw * 3.5f, p1.Y + 200, 18, "level", "LEVEL", Ink);
        var p2 = new RectangleF(p1.Right + 10, p1.Y, a.Right - p1.Right - 10, p1.Height); Panel(g, p2, "PROGRESSION", Gold);
        Choice(g, new RectangleF(p2.X + 14, p2.Y + 34, p2.Width - 28, 26), "prog", Gold, 10.5f); var prog = ChordPadFx.Progs[(int)e.Get("prog")]; string names = ""; foreach (int d in prog) names += (names.Length > 0 ? "  -  " : "") + e.ChordName(d);
        Txt(g, names, 15, Ink, new RectangleF(p2.X + 14, p2.Y + 72, p2.Width - 28, 26), StringAlignment.Center, true);
        Button(g, new RectangleF(p2.X + 14, p2.Y + 116, p2.Width - 28, 38), "WRITE 8 BARS TO THE TRACK", true, Gold, () => { var t = e.Owner; if (t == null) { MessageBox.Show(this, "Put ChordPad on a track first."); return; } int c = e.WriteToTrack(t, 0, 8); MessageBox.Show(this, c + " chords written to the track from the start of the song, one a bar. Play the song to hear them (they are notes you can edit)."); }, "Put this progression on the track as real notes (one chord a bar, 8 bars, from the start)", 11);
        Txt(g, "Tip: the white keys from middle C play the pads (C = I, D = II...).", 10, Dim, new RectangleF(p2.X + 14, p2.Bottom - 30, p2.Width - 28, 16), StringAlignment.Center, false);
    }
}
#endif
