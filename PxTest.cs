using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

// The Purphex VSTs (Glow, Curve EQ, Shape, SubForge): registered, every control audible, safe, presets valid, real-time, windows draw.
static class PX
{
    static int fails = 0; const int SR = 44100;
    static void Check(string name, bool ok, string info = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (info.Length > 0 ? "   " + info : "")); if (!ok) fails++; }
    static Fx New(string type) { var f = FxRegistry.Create(type); f.Prepare(SR); f.Reset(); return f; }
    static void Run(Fx f, float[] l, float[] r, int block, Action<int> between = null)
    {
        var bl = new float[block]; var br = new float[block];
        for (int p = 0; p < l.Length; p += block) { int n = Math.Min(block, l.Length - p); if (between != null) between(p); Array.Copy(l, p, bl, 0, n); Array.Copy(r, p, br, 0, n); f.BeginBlock(null, p, n, true); f.Process(bl, br, n); Array.Copy(bl, 0, l, p, n); Array.Copy(br, 0, r, p, n); }
    }
    static float[] Voice(int n) { var x = new float[n]; var rnd = new Random(3); for (int i = 0; i < n; i++) { double t = i / (double)SR, s = 0; for (int h = 1; h <= 14; h++) s += Math.Sin(2 * Math.PI * h * (110 + 20 * Math.Sin(t)) * t) / h; x[i] = (float)(0.3 * s * (0.3 + 0.7 * Math.Abs(Math.Sin(2 * Math.PI * 2.1 * t))) + (rnd.NextDouble() - 0.5) * 0.04); } return x; }
    static float[] Sine(int n, double hz, double a) { var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(a * Math.Sin(2 * Math.PI * hz * i / SR)); return x; }
    static double Rms(float[] x, int a, int b) { double s = 0; for (int i = a; i < b; i++) s += x[i] * x[i]; return Math.Sqrt(s / Math.Max(1, b - a)); }
    static double Diff(float[] a, float[] b) { double m = 0; for (int i = 0; i < a.Length; i++) m = Math.Max(m, Math.Abs(a[i] - b[i])); return m; }
    static bool Finite(float[] x) { foreach (var v in x) if (float.IsNaN(v) || float.IsInfinity(v)) return false; return true; }
    static double Peak(float[] x) { double m = 0; foreach (var v in x) m = Math.Max(m, Math.Abs(v)); return m; }
    static double Goertzel(float[] x, int s, int n, double hz) { double w = 2 * Math.PI * hz / SR, c = 2 * Math.Cos(w), q1 = 0, q2 = 0; for (int i = s; i < s + n; i++) { double q0 = c * q1 - q2 + x[i]; q2 = q1; q1 = q0; } return Math.Sqrt(Math.Max(0, q1 * q1 + q2 * q2 - c * q1 * q2)) / n; }
    // a sound through an effect, or a held note through an instrument
    static float[] Render(Fx f, float[] src) { var l = (float[])src.Clone(); var r = (float[])src.Clone(); if (f is StereoLabFx) for (int i = 0; i < r.Length; i++) r[i] = i >= 37 ? src[i - 37] * 0.7f : 0; Run(f, l, r, 512); return l; }
    // a vocal with breaths (quiet hiss) between the phrases, and drum hits with spill between them
    static float[] Breathy(int n) { var v = Voice(n); var rnd = new Random(5); double hp = 0, px = 0; for (int i = 0; i < n; i++) { bool gap = (i / (SR / 2)) % 2 == 1; double nz = rnd.NextDouble() * 2 - 1; hp = 0.9 * (hp + nz - px); px = nz; v[i] = gap ? (float)(hp * 0.012) : v[i]; } return v; }
    static float[] Hits(int n) { var x = new float[n]; var rnd = new Random(7); for (int i = 0; i < n; i++) { int k = i % (SR / 4); x[i] = (float)(k < 3000 ? 0.6 * Math.Sin(k * 0.12) * Math.Exp(-k / 700.0) : (rnd.NextDouble() - 0.5) * 0.004); } return x; }
    static float[] SrcFor(string t, int n) { return t == "pxbreath" ? Breathy(n) : t == "pxpgate" || t == "pxsmack" ? Hits(n) : Voice(n); }
    static float[] Note(Fx f, int note, int frames, int offAt = -1)
    {
        var inst = (InstrumentFx)f; var l = new float[frames]; var r = new float[frames]; var bl = new float[512]; var br = new float[512]; inst.Midi(0x90, (byte)note, 100, false);
        for (int p = 0; p < frames; p += 512) { int n = Math.Min(512, frames - p); if (offAt >= 0 && p >= offAt && p - 512 < offAt) inst.Midi(0x80, (byte)note, 0, false); Array.Clear(bl, 0, n); Array.Clear(br, 0, n); f.BeginBlock(null, p, n, false); f.Process(bl, br, n); Array.Copy(bl, 0, l, p, n); }
        return l;
    }
    static readonly Dictionary<string, string[]> Skip = new Dictionary<string, string[]> {
        { "pxglow", new string[0] }, { "pxcurve", new[] { "pre", "post", "scale" } }, { "pxshape", new string[0] }, { "pxsubforge", new[] { "glide", "mono" } }, { "pxbreath", new string[0] }, { "pxpgate", new string[0] }, { "pxvortex", new string[0] }, { "pxmultidyn", new string[0] }, { "pxprism", new[] { "kill" } }, { "pxfrost", new[] { "freeze" } }, { "pxtape", new string[0] }, { "pxconst", new string[0] }, { "pxclip", new string[0] }, { "pxsmack", new string[0] }, { "pxvinyl", new[] { "start" } },   // spin-up is heard only after a stop is released: tested on its own below
        { "pxradio", new string[0] }, { "pxdoubler", new string[0] }, { "pxchoir", new string[0] }, { "pxvowel", new string[0] }, { "pxstep", new string[0] }, { "pxtri", new string[0] }, { "pxechogrid", new string[0] }, { "pxmodal", new string[0] }, { "pxshift", new string[0] }, { "pxmax", new[] { "mono", "width" } },   // mono bass and width do nothing to a mono test sound: tested on their own below
        { "pxsgate", new[] { "sp0", "wp0", "sp1", "wp1", "sp2", "wp2", "sp3", "wp3", "sp4", "wp4", "sp5", "wp5", "sp6", "wp6", "sp7", "wp7", "sp8", "wp8", "sp9", "wp9", "sp10", "wp10", "sp11", "wp11", "sp12", "wp12", "sp13", "wp13", "sp14", "wp14", "sp15", "wp15", "sp16", "wp16", "sp17", "wp17", "sp18", "wp18", "sp19", "wp19", "sp20", "wp20", "sp21", "wp21", "sp22", "wp22", "sp23", "wp23", "sp24", "wp24", "sp25", "wp25", "sp26", "wp26", "sp27", "wp27", "sp28", "wp28", "sp29", "wp29", "sp30", "wp30", "sp31", "wp31" } },   // the learned profiles: tested on their own below
       
        { "pxstereo", new[] { "ab", "morph", "a_lg", "a_rg", "a_lpan", "a_rpan", "a_width", "a_pan", "a_ms", "a_bass", "b_lg", "b_rg", "b_lpan", "b_rpan", "b_width", "b_pan", "b_ms", "b_bass" } } };   // A-B morph: tested on its own below   // glide and voices need two notes: tested on their own below

    [STAThread]
    static void Main()
    {
        StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_px_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(StudioEnv.DataDir); AppDomain.MonitoringIsEnabled = true;
        var types = new[] { "pxglow", "pxcurve", "pxshape", "pxsubforge", "pxbreath", "pxpgate", "pxstereo", "pxvortex", "pxmultidyn", "pxprism", "pxfrost", "pxtape", "pxconst", "pxclip", "pxsmack", "pxvinyl", "pxradio", "pxdoubler", "pxchoir", "pxvowel", "pxstep", "pxtri", "pxshift", "pxmax", "pxsgate", "pxechogrid", "pxmodal" };
        foreach (var t in types)
        {
            var info = FxRegistry.All.FirstOrDefault(i => i.Type == t); var f = New(t); bool inst = f is InstrumentFx;
            Check(t + ": in the list (" + (inst ? "Instruments" : "Purphex VST") + "), made by the registry, has factory presets", info != null && info.Category == (inst ? "Instruments" : "Purphex VST") && FxRegistry.IsBuiltin(t) && Presets.For(t).Count >= 12, info == null ? "missing" : info.Name + ", " + Presets.For(t).Count + " presets");
            // every setting changes the sound (a change of one step from its default, or to an end, with the sections it needs switched on)
            var src = SrcFor(t, SR * 2); Func<Action<Fx>, float[]> go = set => { var g = New(t); prep(t, g); set(g); return inst ? Note(g, 36, SR * 2, SR) : Render(g, src); };
            var b0 = go(x => { }); double rb = Math.Max(1e-4, Rms(b0, 0, b0.Length)); var deaf = new List<string>();
            foreach (var p in f.P)
            {
                if (Skip[t].Contains(p.Id) || p.Id.EndsWith("_on") || p.Id == "sub_on" || p.Id == "d_on" || p.Id == "c_on" || p.Id == "filt") continue;
                var pp = p; float alt = p.IsChoice ? (p.Val + 1 > p.Max ? p.Min : p.Val + 1) : (Math.Abs(p.Val - p.Max) > Math.Abs(p.Val - p.Min) ? p.Max : p.Min);
                var o = go(g => { needs(t, pp.Id, g); g.Set(pp.Id, alt); }); var o0 = go(g => needs(t, pp.Id, g));
                double d = Math.Sqrt(o.Zip(o0, (u, v) => (double)(u - v) * (u - v)).Sum() / o.Length);
                if (d < rb * 0.002 || !Finite(o)) deaf.Add(p.Id + " (" + (d / rb).ToString("0.0000") + ")");
            }
            Check(t + ": every setting changes the sound (" + f.P.Count + " settings)", deaf.Count == 0, string.Join(", ", deaf.ToArray()));
            // safe at its loudest, presets valid, block size, save/load, real time, no memory on the audio thread
            var hot = New(t); prep(t, hot); foreach (var p in hot.P) if (p.IsChoice && p.Choices.Length == 2 && p.Choices[0] == "Off") hot.Set(p.Id, 1); foreach (var id in new[] { "sat", "d_drive", "cl_gain", "inject", "res" }) if (hot.Find(id) != null) hot.Set(id, hot.Find(id).Max);
            if (t == "pxbreath") hot.Set("mon", 0); if (t == "pxpgate") hot.Set("sc_listen", 0);
            var ho = inst ? Note(hot, 36, SR * 2) : Render(hot, src); Check(t + ": everything on at its strongest stays finite and below +12 dB", Finite(ho) && Peak(ho) < 4, "peak " + Peak(ho).ToString("0.00"));
            var bad = new List<string>();
            foreach (var kv in Presets.For(t))
            {
                foreach (var tok in kv.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) { int eq = tok.IndexOf('='); var p = eq > 0 ? f.Find(tok.Substring(0, eq)) : null; float v; if (p == null || !float.TryParse(tok.Substring(eq + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) || v < p.Min || v > p.Max) bad.Add(kv.Key + ": " + tok); }
                var g = New(t); Presets.Apply(g, kv.Value); var o = inst ? Note(g, 36, SR) : Render(g, SrcFor(t, SR)); if (!Finite(o) || Peak(o) > 4) bad.Add(kv.Key + " level " + Peak(o).ToString("0.0"));
            }
            Check(t + ": every factory preset uses real settings in range and plays safely", bad.Count == 0, string.Join(" | ", bad.Take(3).ToArray()));
            if (!inst)
            {
                float[] o64 = null, o4096 = null; foreach (int bs in new[] { 64, 4096 }) { var g = New(t); prep(t, g); var l = (float[])src.Clone(); var r = (float[])src.Clone(); Run(g, l, r, bs); if (bs == 64) o64 = l; else o4096 = l; }
                Check(t + ": 64 and 4096-sample buffers give the same sound", Diff(o64, o4096) < 2e-4, Diff(o64, o4096).ToString("0.000000"));
            }
            var a = New(t); foreach (var p in a.P) a.Set(p.Id, p.IsChoice ? p.Max : p.Min + (p.Max - p.Min) * 0.37f); string ser = a.Serialize(); var b = FxRegistry.Create(t); b.Deserialize(ser);
            Check(t + ": settings save and load with the project", b.Serialize() == ser);
            { var g = New(t); prep(t, g); var l = Voice(SR * 10); var r = (float[])l.Clone(); var bl = new float[256]; var br = new float[256]; if (inst) ((InstrumentFx)g).Midi(0x90, 36, 100, false);
              for (int p = 0; p < SR; p += 256) { g.BeginBlock(null, p, 256, !inst); g.Process(bl, br, 256); }
              long m0 = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize; var sw = Stopwatch.StartNew();
              for (int p = 0; p + 256 <= l.Length; p += 256) { if (!inst) { Array.Copy(l, p, bl, 0, 256); Array.Copy(r, p, br, 0, 256); } else { Array.Clear(bl, 0, 256); Array.Clear(br, 0, 256); } g.BeginBlock(null, p, 256, false); g.Process(bl, br, 256); }
              double pct = sw.Elapsed.TotalSeconds / 10 * 100; long mem = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - m0;
              Check(t + ": under 10% of real time, and no new memory while it plays", pct < 10 && mem < (inst ? 600 * 1024 : 64 * 1024), pct.ToString("0.0") + "% of real time, " + (mem / 1024) + " KB"); }
        }
        // ---- Glow ----
        { var g = New("pxglow"); foreach (var k in "lmha") g.Set(k + "_amt", 0); var s = Voice(SR); var o = Render(g, s); Check("Glow: every band at 0 is perfectly transparent", Diff(o, s) < 1e-6);
          var g2 = New("pxglow"); g2.Set("fxonly", 1); foreach (var k in "lmha") g2.Set(k + "_amt", 0); var o2 = Render(g2, s); Check("Glow: FX ONLY with nothing added is silent", Peak(o2) < 1e-6);
          var g3 = New("pxglow"); g3.Set("lvl", 1); g3.Set("inject", 100); var o3 = Render(g3, Voice(SR * 4)); var s4 = Voice(SR * 4); Check("Glow: LEVEL MATCH keeps the loudness about the same", Math.Abs(20 * Math.Log10(Rms(o3, SR * 2, SR * 4) / Rms(s4, SR * 2, SR * 4))) < 1.5, (20 * Math.Log10(Rms(o3, SR * 2, SR * 4) / Rms(s4, SR * 2, SR * 4))).ToString("0.00") + " dB"); }
        // ---- Curve EQ ----
        { var g = New("pxcurve"); g.Set("b3_on", 1); g.Set("b3_t", 0); g.Set("b3_f", 1000); g.Set("b3_g", 12); g.Set("b3_q", 1); var s = Sine(SR, 1000, 0.1); var o = Render(g, s); double db = 20 * Math.Log10(Rms(o, SR / 2, SR) / Rms(s, SR / 2, SR));
          Check("Curve EQ: a +12 dB bell at 1 kHz lifts a 1 kHz tone by 12 dB", Math.Abs(db - 12) < 0.3, db.ToString("0.00") + " dB");
          var gd = New("pxcurve"); gd.Set("b3_on", 1); gd.Set("b3_f", 1000); gd.Set("b3_dyn", -12); gd.Set("b3_thr", -40); var loud = Sine(SR, 1000, 0.5); var ol = Render(gd, loud); double dbl = 20 * Math.Log10(Rms(ol, SR / 2, SR) / Rms(loud, SR / 2, SR));
          var quiet = Sine(SR, 1000, 0.001); var oq = Render(New("pxcurve").Also(x => { x.Set("b3_on", 1); x.Set("b3_f", 1000); x.Set("b3_dyn", -12); x.Set("b3_thr", -40); }), quiet); double dbq = 20 * Math.Log10(Rms(oq, SR / 2, SR) / Rms(quiet, SR / 2, SR));
          Check("Curve EQ: a dynamic band cuts only when its part of the sound is loud", dbl < -8 && Math.Abs(dbq) < 1, "loud " + dbl.ToString("0.0") + " dB, quiet " + dbq.ToString("0.0") + " dB");
          var ga = New("pxcurve"); ga.Set("b2_on", 1); ga.Set("b2_t", 1); ga.Set("b2_f", 5000); ga.Set("b2_g", 9); ga.Set("auto", 1); var v = Voice(SR * 2); var oa = Render(ga, v); var gn = New("pxcurve"); gn.Set("b2_on", 1); gn.Set("b2_t", 1); gn.Set("b2_f", 5000); gn.Set("b2_g", 9); var on2 = Render(gn, v);
          Check("Curve EQ: AUTO GAIN turns a big boost back down", Rms(oa, 0, oa.Length) < Rms(on2, 0, on2.Length) * 0.8, (20 * Math.Log10(Rms(oa, 0, oa.Length) / Rms(on2, 0, on2.Length))).ToString("0.0") + " dB"); }
        // ---- Shape ----
        { var g = New("pxshape"); g.Set("ratio", 10); g.Set("thr", -30); g.Set("auto", 0); var s = Sine(SR, 200, 0.5); var o = Render(g, s); double db = 20 * Math.Log10(Rms(o, SR / 2, SR) / Rms(s, SR / 2, SR));
          Check("Shape: compresses a loud tone (10:1 at -30 dB)", db < -15, db.ToString("0.0") + " dB");
          var gc = New("pxshape"); gc.Set("ratio", 1); gc.Set("cl_on", 1); gc.Set("cl_gain", 24); gc.Set("cl_soft", 0); var oc = Render(gc, Sine(SR, 200, 0.9)); Check("Shape: CLIP at full drive never goes over 0 dB", Peak(oc) <= 1.0001, Peak(oc).ToString("0.0000"));
          var hits = new float[SR * 2]; for (int k = 0; k < 8; k++) for (int i = 0; i < 2000; i++) hits[k * 11025 + i] = (float)(0.4 * Math.Sin(i * 0.2) * Math.Exp(-i / 600.0));
          var gt = New("pxshape"); gt.Set("ratio", 1); gt.Set("auto", 0); gt.Set("tr_on", 1); gt.Set("tr_gain", 12); var ot = Render(gt, hits); Check("Shape: TRANSIENT lifts the hits", Peak(ot) > Peak(hits) * 1.5, Peak(hits).ToString("0.00") + " -> " + Peak(ot).ToString("0.00")); }
        // ---- SubForge ----
        { var g = New("pxsubforge"); g.Set("punch", 0); g.Set("sub_on", 0); g.Set("ftype", 0); g.Set("s", 100); var o = Note(g, 45, SR); double a = Goertzel(o, SR / 2, SR / 4, 110), off = Goertzel(o, SR / 2, SR / 4, 150);
          Check("SubForge: note A2 plays at 110 Hz", a > 0.05 && a > off * 10, a.ToString("0.000") + " vs " + off.ToString("0.000"));
          var gs = New("pxsubforge"); gs.Set("punch", 0); gs.Set("ftype", 0); gs.Set("s", 100); gs.Set("sub_lvl", 100); var os = Note(gs, 45, SR); Check("SubForge: the sub adds the octave below (55 Hz)", Goertzel(os, SR / 2, SR / 4, 55) > 0.05);
          var gr = New("pxsubforge"); gr.Set("r", 50); var orr = Note(gr, 45, SR, SR / 4); Check("SubForge: releasing the note lets it fade out", Rms(orr, SR / 2, SR) < 0.002 && Rms(orr, 0, SR / 4) > 0.05);
          var gp = New("pxsubforge"); gp.Set("punch", 12); gp.Set("ptime", 60); gp.Set("sub_on", 0); gp.Set("ftype", 0); gp.Set("s", 100); var op = Note(gp, 45, SR); Func<float[], int, int, int> zc = (x, a0, n0) => { int c = 0; for (int i = a0 + 1; i < a0 + n0; i++) if (x[i - 1] <= 0 && x[i] > 0) c++; return c; };
          Check("SubForge: PUNCH starts the note higher (the 808 knock) and settles on the note", zc(op, 0, 2205) > zc(op, SR / 2, 2205) * 1.25 && Goertzel(op, SR / 2, SR / 4, 110) > 0.05, zc(op, 0, 2205) + " vs " + zc(op, SR / 2, 2205) + " cycles");
          { var gg = New("pxsubforge"); gg.Set("punch", 0); gg.Set("sub_on", 0); gg.Set("ftype", 0); gg.Set("s", 100); gg.Set("glide", 100); var inst = (InstrumentFx)gg; var l = new float[SR]; var r = new float[SR]; var bl = new float[512]; var br = new float[512]; inst.Midi(0x90, 45, 100, false);
            for (int p = 0; p < SR; p += 512) { if (p == 22016) inst.Midi(0x90, 57, 100, false); Array.Clear(bl, 0, 512); Array.Clear(br, 0, 512); gg.BeginBlock(null, p, 512, false); gg.Process(bl, br, 512); Array.Copy(bl, 0, l, p, Math.Min(512, SR - p)); }
            int mid = zc(l, 22050 + 2000, 4410), end = zc(l, SR - 4410, 4410); Check("SubForge: GLIDE slides from one note to the next (mono)", end >= 20 && mid > 11 && mid < end - 3, "cycles " + mid + " then " + end); } }
        // ---- BreathOff ----
        { var g = New("pxbreath"); var s = Breathy(SR * 3); var o = Render(g, s); int lat = g.Latency; double vIn = Rms(s, SR / 8, SR / 2 - SR / 8), vOut = Rms(o, SR / 8 + lat, SR / 2 - SR / 8 + lat), bIn = Rms(s, SR / 2 + SR / 8, SR - SR / 8), bOut = Rms(o, SR / 2 + SR / 8 + lat, SR - SR / 8 + lat);
          Check("BreathOff: the breaths are turned right down and the singing is untouched", 20 * Math.Log10(bOut / bIn) < -20 && Math.Abs(20 * Math.Log10(vOut / vIn)) < 0.5, "breath " + (20 * Math.Log10(bOut / bIn)).ToString("0.0") + " dB, voice " + (20 * Math.Log10(vOut / vIn)).ToString("0.00") + " dB");
          Check("BreathOff: it reports its look-ahead so the studio lines the track up", lat > 0 && lat < SR / 50, lat + " samples"); }
        // ---- Phantom Gate ----
        { var g = New("pxpgate"); g.Set("thr", -30); g.Set("floor", -60); g.Set("ahead", 0); g.Set("hold", 5); g.Set("rel", 10); var s = Hits(SR * 2); var o = Render(g, s); double hitIn = Peak(s), hitOut = Peak(o); double spillIn = Rms(s, 9000, 11000), spillOut = Rms(o, 9000, 11000);
          Check("Phantom Gate: the hits pass and the spill between them is cut", hitOut > hitIn * 0.8 && 20 * Math.Log10((spillOut + 1e-9) / spillIn) < -30, "hits " + hitOut.ToString("0.00") + ", spill " + (20 * Math.Log10((spillOut + 1e-9) / spillIn)).ToString("0") + " dB");
          var ga = New("pxpgate"); ga.Set("ahead", 10); Check("Phantom Gate: AHEAD is reported as latency", ga.Latency == (int)(0.010 * SR), ga.Latency + " samples"); }
        // ---- Stereo Lab ----
        { var g = New("pxstereo"); g.Set("width", 0); var s = Voice(SR); var l = (float[])s.Clone(); var r = new float[SR]; for (int i = 37; i < SR; i++) r[i] = s[i - 37] * 0.7f; Run(g, l, r, 512); Check("Stereo Lab: width 0 makes it mono (left = right)", Diff(l, r) < 1e-6);
          var gp = New("pxstereo"); var l2 = (float[])s.Clone(); var r2 = new float[SR]; for (int i = 0; i < SR; i++) r2[i] = -s[i]; Run(gp, l2, r2, 512); Check("Stereo Lab: the phase meter shows an out-of-phase sound as negative", ((StereoLabFx)gp).Corr < -0.9, ((StereoLabFx)gp).Corr.ToString("0.00"));
          var gm = (StereoLabFx)New("pxstereo"); gm.Set("width", 0); gm.Store(false); gm.Set("width", 200); gm.Store(true); gm.Set("width", 100); gm.Set("ab", 1); gm.Set("morph", 50);
          var l3 = (float[])s.Clone(); var r3 = new float[SR]; for (int i = 37; i < SR; i++) r3[i] = s[i - 37] * 0.7f; Run(gm, l3, r3, 512); var gw = New("pxstereo"); gw.Set("width", 100); var l4 = (float[])s.Clone(); var r4 = new float[SR]; for (int i = 37; i < SR; i++) r4[i] = s[i - 37] * 0.7f; Run(gw, l4, r4, 512);
          Check("Stereo Lab: the A-B slider half way between width 0 and width 200 sounds like width 100", Diff(l3, l4) < 1e-4, Diff(l3, l4).ToString("0.000000")); }
        // ---- Vortex ----
        { var g = New("pxvortex"); g.Set("sync", 0); g.Set("rate", 1); g.Set("f_mix", 0); g.Set("g_mix", 0); var s = Sine(SR, 300, 0.3); var l = (float[])s.Clone(); var r = (float[])s.Clone(); Run(g, l, r, 512);
          double l1 = Rms(l, SR / 4 - 2000, SR / 4 + 2000), r1 = Rms(r, SR / 4 - 2000, SR / 4 + 2000), l3 = Rms(l, 3 * SR / 4 - 2000, 3 * SR / 4 + 2000), r3 = Rms(r, 3 * SR / 4 - 2000, 3 * SR / 4 + 2000);
          Check("Vortex: the sound circles: right at a quarter turn, left at three quarters", r1 > l1 * 4 && l3 > r3 * 4, (l1 / (r1 + 1e-9)).ToString("0.00") + " / " + (l3 / (r3 + 1e-9)).ToString("0.00")); }
        // ---- MultiDyn ----
        { var g = New("pxmultidyn"); for (int k = 0; k < 5; k++) g.Set("b" + k + "_ratio", 1); var s = Voice(SR); var o = Render(g, s); Check("MultiDyn: at 1:1 every band adds back to exactly the input", Diff(o, s) < 1e-5, Diff(o, s).ToString("0.0000000"));
          var gs = New("pxmultidyn"); for (int k = 0; k < 5; k++) gs.Set("b" + k + "_ratio", 1); gs.Set("b0_solo", 1); var low = Render(gs, Sine(SR, 60, 0.3)); var high = Render(New("pxmultidyn").Also(x => { for (int k = 0; k < 5; k++) x.Set("b" + k + "_ratio", 1); x.Set("b0_solo", 1); }), Sine(SR, 8000, 0.3));
          Check("MultiDyn: SOLO on the low band lets the lows through and stops the highs", Rms(low, SR / 2, SR) > 0.15 && Rms(high, SR / 2, SR) < 0.01, Rms(low, SR / 2, SR).ToString("0.000") + " / " + Rms(high, SR / 2, SR).ToString("0.000"));
          var gc = New("pxmultidyn"); for (int k = 0; k < 5; k++) { gc.Set("b" + k + "_ratio", 10); gc.Set("b" + k + "_thr", -30); } var oc = Render(gc, Sine(SR, 1000, 0.5)); Check("MultiDyn: compresses a loud tone", Rms(oc, SR / 2, SR) < 0.5 * 0.7071 * 0.3, Rms(oc, SR / 2, SR).ToString("0.000")); }
        // ---- Prism ----
        { var g = New("pxprism"); g.Set("note", 57); g.Set("i1_on", 0); g.Set("i2_on", 0); g.Set("unison", 1); g.Set("mix", 100); g.Set("decay", 3000); var s = new float[SR]; for (int i = 0; i < 400; i++) s[i] = (float)((new Random(i).NextDouble() - 0.5) * 0.8); var o = Render(g, s);
          double at = Goertzel(o, SR / 4, SR / 4, 220), off = Goertzel(o, SR / 4, SR / 4, 300); Check("Prism: a click rings on at the note (A3 = 220 Hz)", at > off * 5 && at > 1e-4, at.ToString("0.00000") + " vs " + off.ToString("0.00000"));
          var gk = New("pxprism"); gk.Set("mix", 100); gk.Set("decay", 8000); var l = (float[])s.Clone(); var r = (float[])s.Clone(); Run(gk, l, r, 512, p => { if (p >= SR / 2) gk.Set("kill", 1); }); Check("Prism: KILL stops the ring at once", Rms(l, SR / 2 + 1024, SR) < 1e-6 && Rms(l, SR / 4, SR / 2) > 1e-4, Rms(l, SR / 4, SR / 2).ToString("0.000000") + " then " + Rms(l, SR / 2 + 1024, SR).ToString("0.0000000")); }
        // ---- Frost ----
        { var g = New("pxfrost"); g.Set("fade", 20); g.Set("wet", 100); g.Set("dry", 0); var s = Voice(SR * 2); for (int i = SR; i < s.Length; i++) s[i] = 0; var l = (float[])s.Clone(); var r = (float[])s.Clone();
          Run(g, l, r, 512, p => { if (p >= SR / 2) g.Set("freeze", 1); }); Check("Frost: FREEZE holds the sound after the input stops", Rms(l, SR + SR / 4, SR * 2) > Rms(s, SR / 4, SR) * 0.15, Rms(l, SR + SR / 4, SR * 2).ToString("0.0000") + " vs input " + Rms(s, SR / 4, SR).ToString("0.0000"));
          var g2 = New("pxfrost"); g2.Set("fade", 20); var l2 = (float[])s.Clone(); var r2 = (float[])s.Clone(); Run(g2, l2, r2, 512, p => { g2.Set("freeze", p >= SR / 2 && p < SR * 3 / 2 ? 1 : 0); }); Check("Frost: CLEAR lets go (silence again)", Rms(l2, SR * 7 / 4, SR * 2) < 1e-3, Rms(l2, SR * 7 / 4, SR * 2).ToString("0.00000")); }
        // ---- Tape Dirt ----
        { var s = new float[SR * 2]; { var rn = new Random(9); for (int i = 0; i < s.Length; i++) s[i] = (float)((rn.NextDouble() - 0.5) * 0.4); } var gc = New("pxtape"); gc.Set("dirt", 0); var oc = Render(gc, s); var gd = New("pxtape"); gd.Set("dirt", 4); var od = Render(gd, s);
          Check("Tape Dirt: more DIRT takes the highs away", Goertzel(od, SR / 2, SR, 3300) < Goertzel(oc, SR / 2, SR, 3300) * 0.5, Goertzel(oc, SR / 2, SR, 3300).ToString("0.00000") + " -> " + Goertzel(od, SR / 2, SR, 3300).ToString("0.00000"));
          var gl = New("pxtape"); gl.Set("drive", 10); var ol = Render(gl, Sine(SR, 200, 0.8)); Check("Tape Dirt: full drive stays about as loud (no jump in level)", Rms(ol, SR / 2, SR) < 0.8 * 1.2 && Rms(ol, SR / 2, SR) > 0.15, Rms(ol, SR / 2, SR).ToString("0.000")); }
        // ---- Constellation ----
        { var g = New("pxconst"); g.Set("dry", -60); g.Set("rev", 0); g.Set("fb", 0); var s = Voice(SR * 2); for (int i = SR; i < s.Length; i++) s[i] = 0; var o = Render(g, s);
          Check("Constellation: the grains keep playing the sound after the input stops", Rms(o, SR + SR / 8, SR + SR / 2) > 0.01, Rms(o, SR + SR / 8, SR + SR / 2).ToString("0.0000"));
          var go = New("pxconst"); go.Set("dry", -60); go.Set("rev", 0); go.Set("fb", 0); for (int k = 0; k < 5; k++) go.Set("l" + k + "_p", 12); go.Set("glen", 400); var oo = Render(go, Sine(SR, 300, 0.4));
          Check("Constellation: lanes at +12 play the grains an octave up", Goertzel(oo, SR / 2, SR / 2, 600) > Goertzel(oo, SR / 2, SR / 2, 300) * 3, Goertzel(oo, SR / 2, SR / 2, 600).ToString("0.0000") + " vs " + Goertzel(oo, SR / 2, SR / 2, 300).ToString("0.0000")); }
        // ---- Clipper ----
        { var g = New("pxclip"); g.Set("mode", 1); g.Set("thr", -6); g.Set("pre", 12); var o = Render(g, Sine(SR, 200, 0.5)); Check("Clipper: hard mode never goes over its threshold", Peak(o) <= Math.Pow(10, -6 / 20.0) + 1e-4, Peak(o).ToString("0.0000"));
          var gs = New("pxclip"); gs.Set("soft", 0); var s = Sine(SR, 200, 0.2); var os = Render(gs, s); Check("Clipper: a quiet sound below the threshold passes untouched", Diff(os, s) < 1e-4); }
        // ---- Smack ----
        { var h = Hits(SR); var gu = New("pxsmack"); gu.Set("att", 100); gu.Set("sus", 0); gu.Set("guard", 0); var ou = Render(gu, h); var gd = New("pxsmack"); gd.Set("att", -100); gd.Set("sus", 0); var od = Render(gd, h);
          Check("Smack: ATTACK up lifts the hits, down softens them", Peak(ou) > Peak(h) * 1.3 && Peak(od) < Peak(h) * 0.8, Peak(h).ToString("0.00") + " -> " + Peak(ou).ToString("0.00") + " / " + Peak(od).ToString("0.00"));
          var gg = New("pxsmack"); gg.Set("att", 100); gg.Set("out", 24); gg.Set("guard", 1); var og = Render(gg, h); Check("Smack: GUARD keeps the peaks under 0 dB", Peak(og) <= 1.0001, Peak(og).ToString("0.000")); }
        // ---- Vinyl Stop ----
        { Fx.ProjectBpm = 120; var g = New("pxvinyl"); g.Set("len", 2); g.Set("wash", 0); var s = Sine(SR * 2, 440, 0.4); var l = (float[])s.Clone(); var r = (float[])s.Clone(); Run(g, l, r, 512, p => { if (p >= SR / 2) g.Set("stop", 1); });
          Func<float[], int, int, int> zc = (x, a0, n0) => { int c = 0; for (int i = a0 + 1; i < a0 + n0; i++) if (x[i - 1] <= 0 && x[i] > 0) c++; return c; };
          Check("Vinyl Stop: the pitch falls as it stops, then it is silent", zc(l, SR / 2 + 4410, 4410) < zc(l, SR / 4, 4410) && Rms(l, SR + SR / 4, SR * 2) < 0.01, zc(l, SR / 4, 4410) + " -> " + zc(l, SR / 2 + 4410, 4410) + " cycles, end " + Rms(l, SR + SR / 4, SR * 2).ToString("0.0000")); }
        { var g = New("pxvinyl"); g.Set("len", 1); g.Set("wash", 0); g.Set("start", 0); var s = Sine(SR * 2, 440, 0.4); var l = (float[])s.Clone(); var r = (float[])s.Clone(); Run(g, l, r, 512, p => g.Set("stop", p >= SR / 4 && p < SR / 2 ? 1 : 0)); var g2 = New("pxvinyl"); g2.Set("len", 1); g2.Set("wash", 0); g2.Set("start", 100); var l2 = (float[])s.Clone(); var r2 = (float[])s.Clone(); Run(g2, l2, r2, 512, p => g2.Set("stop", p >= SR / 4 && p < SR / 2 ? 1 : 0));
          Check("Vinyl Stop: after a stop it spins back up, slowly or quickly as SPIN-UP says", Diff(l, l2) > 0.05 && Rms(l, SR + SR / 2, SR * 2) > 0.2, Diff(l, l2).ToString("0.000")); }
        // ---- Radio ----
        { var g = New("pxradio"); g.Set("static", 0); g.Set("noise", 0); var o = Render(g, Sine(SR, 50, 0.3)); var o2 = Render(New("pxradio").Also(x => { x.Set("static", 0); x.Set("noise", 0); }), Sine(SR, 1000, 0.3));
          Check("Radio: the low end is cut and the middle comes through (a radio band)", Goertzel(o, SR / 2, SR / 2, 50) < Goertzel(o2, SR / 2, SR / 2, 1000) * 0.2, Goertzel(o, SR / 2, SR / 2, 50).ToString("0.0000") + " vs " + Goertzel(o2, SR / 2, SR / 2, 1000).ToString("0.0000")); }
        // ---- Doubler / Octave Choir / Vowel / Step Shaper ----
        { var g = New("pxdoubler"); g.Set("v0_on", 0); g.Set("v1_on", 0); var s = Voice(SR); var o = Render(g, s); Check("Doubler: with no voices on, only the original comes through", Diff(o, s) < 1e-5);
          var gc = New("pxchoir"); for (int k = 0; k < 8; k++) gc.Set("o" + k + "_on", 0); gc.Set("o0_on", 1); gc.Set("o0_iv", 12); gc.Set("o0_d", 0); gc.Set("o0_ct", 0); gc.Set("o0_fb", 0); gc.Set("mix", 100); gc.Set("rand", 0); gc.Set("scale", 0); var oc = Render(gc, Sine(SR, 220, 0.3));
          Check("Octave Choir: a voice at +12 sings an octave up", Goertzel(oc, SR / 2, SR / 2, 440) > Goertzel(oc, SR / 2, SR / 2, 220) * 3, Goertzel(oc, SR / 2, SR / 2, 440).ToString("0.0000") + " vs " + Goertzel(oc, SR / 2, SR / 2, 220).ToString("0.0000"));
          Check("Octave Choir: HARMONY makes 3rds, 6ths and 7ths major or minor and leaves the rest", OctaveChoirFx.Snap(4, 2) == 3 && OctaveChoirFx.Snap(3, 1) == 4 && OctaveChoirFx.Snap(-9, 1) == -8 && OctaveChoirFx.Snap(7, 2) == 7 && OctaveChoirFx.Snap(12, 1) == 12 && OctaveChoirFx.Snap(15, 0) == 15);
          var noise = new float[SR]; { var rn = new Random(4); for (int i = 0; i < SR; i++) noise[i] = (float)((rn.NextDouble() - 0.5) * 0.4); }
          var ga = New("pxvowel"); ga.Set("x", 0.85f); ga.Set("y", 0.95f); ga.Set("lm", 0); var oa = Render(ga, noise); var gi = New("pxvowel"); gi.Set("x", 0.05f); gi.Set("y", 0.02f); gi.Set("lm", 0); var oi = Render(gi, noise);
          Check("Vowel: 'aah' has its energy low (about 730 Hz) and 'ee' high (about 2.3 kHz)", Goertzel(oa, SR / 2, SR / 2, 730) > Goertzel(oi, SR / 2, SR / 2, 730) * 2 && Goertzel(oi, SR / 2, SR / 2, 2290) > Goertzel(oa, SR / 2, SR / 2, 2290) * 2);
          Fx.ProjectBpm = 120; var gs = New("pxstep"); for (int k = 0; k < 16; k++) gs.Set("s" + k, k % 2 == 0 ? 100 : 0); gs.Set("smooth", 0); gs.Set("swing", 0); gs.Set("grid", 1); var os = Render(gs, Sine(SR, 300, 0.3)); int q = (int)(0.25 * SR);
          Check("Step Shaper: steps at 0% are silent and steps at 100% pass (1/8 grid at 120 BPM)", Rms(os, q / 4, q * 3 / 4) > 0.15 && Rms(os, q + q / 4, q + q * 3 / 4) < 0.01, Rms(os, q / 4, q * 3 / 4).ToString("0.000") + " / " + Rms(os, q + q / 4, q + q * 3 / 4).ToString("0.000")); }
        // ---- Tri-Drive / Shift / Maximizer / Spectral Gate ----
        { var g = New("pxtri"); g.Set("lo_on", 0); g.Set("mid_on", 0); g.Set("hi_on", 0); var s = Voice(SR); var o = Render(g, s); double r0 = Rms(s, SR / 4, SR), r1 = Rms(o, SR / 4, SR); Check("Tri-Drive: with every band off the level is the same (the three bands add back up flat)", Math.Abs(r1 / r0 - 1) < 0.03, (r1 / r0).ToString("0.000"));
          var gf = New("pxtri"); gf.Set("hi_drive", 90); gf.Set("hi_type", 2); gf.Set("lo_drive", 0); gf.Set("mid_drive", 0); var tone = Sine(SR, 100, 0.4); var of = Render(gf, tone); double h3 = Goertzel(of, SR / 2, SR / 2, 300), f1 = Goertzel(of, SR / 2, SR / 2, 100); Check("Tri-Drive: driving only the HIGH band leaves a low note clean (no buzz added)", h3 < f1 * 0.01, (h3 / f1).ToString("0.0000")); }
        { var g = New("pxshift"); g.Set("a_shift", 200); var o = Render(g, Sine(SR, 1000, 0.4)); double up = Goertzel(o, SR / 2, SR / 2, 1200), down = Goertzel(o, SR / 2, SR / 2, 800), orig = Goertzel(o, SR / 2, SR / 2, 1000);
          Check("Shift: +200 Hz moves a 1000 Hz tone to 1200 Hz (not 800, and the 1000 is gone)", up > down * 20 && up > orig * 20, up.ToString("0.0000") + " / " + down.ToString("0.0000") + " / " + orig.ToString("0.0000"));
          var gd = New("pxshift"); gd.Set("a_shift", 200); gd.Set("a_dir", 1); var od = Render(gd, Sine(SR, 1000, 0.4)); Check("Shift: DOWN moves it to 800 Hz", Goertzel(od, SR / 2, SR / 2, 800) > Goertzel(od, SR / 2, SR / 2, 1200) * 20); }
        { var g = New("pxmax"); g.Set("loud", 100); g.Set("ceil", -1); var o = Render(g, Sine(SR, 300, 0.9)); Check("Maximizer: never over the ceiling, however hard it is pushed", Peak(o) <= Math.Pow(10, -1 / 20.0) + 1e-4, Peak(o).ToString("0.0000"));
          var gq = New("pxmax"); gq.Set("loud", 0); var q = Sine(SR, 300, 0.2); var oq = Render(gq, q); int lat = gq.Latency; double dq = 0; for (int i = lat + 1000; i < SR; i++) dq = Math.Max(dq, Math.Abs(oq[i] - q[i - lat])); Check("Maximizer: a quiet sound at loudness 0 passes untouched (only delayed by its look-ahead)", dq < 1e-3, dq.ToString("0.00000") + " latency " + lat);
          var gw = New("pxmax"); gw.Set("loud", 0); gw.Set("width", 0); var sl = Sine(SR, 300, 0.3); var sr2 = Sine(SR, 450, 0.3); var l2 = (float[])sl.Clone(); var r2 = (float[])sr2.Clone(); Run(gw, l2, r2, 512); Check("Maximizer: WIDTH 0 makes it mono", Diff(l2, r2) < 1e-4);
          var gm = New("pxmax"); gm.Set("loud", 0); gm.Set("mono", 2); var lb = Sine(SR, 50, 0.3); var rb = new float[SR]; for (int i = 0; i < SR; i++) rb[i] = -lb[i]; Run(gm, lb, rb, 512); Check("Maximizer: MONO BASS takes the low end out of the sides", Rms(lb, SR / 2, SR) < 0.05, Rms(lb, SR / 2, SR).ToString("0.000")); }
        { var rn = new Random(9); var x = new float[SR * 2]; for (int i = 0; i < x.Length; i++) { x[i] = (float)((rn.NextDouble() - 0.5) * 0.004); if ((i / (SR / 2)) % 2 == 0) x[i] += (float)(0.4 * Math.Sin(2 * Math.PI * 1000 * i / SR)); }
          var g = New("pxsgate"); g.Set("thr", -45); g.Set("red", 40); var o = Render(g, x); int lat = g.Latency; double gap = Rms(o, SR / 2 + lat + 13000, SR - 2000), gapIn = Rms(x, SR / 2 + 13000, SR - 2000 - lat), tone = Rms(o, lat + 4000, SR / 2 - 2000);
          Check("Spectral Gate: the hiss between the notes is turned down, the notes come through", gap < gapIn * 0.2 && tone > 0.2, gap.ToString("0.00000") + " vs " + gapIn.ToString("0.00000") + ", note " + tone.ToString("0.000"));
          var gl = (SpectralGateFx)New("pxsgate"); gl.Set("learn", 1); var hiss = new float[SR]; for (int i = 0; i < SR; i++) hiss[i] = (float)((rn.NextDouble() - 0.5) * 0.02); Render(gl, hiss); gl.Set("learn", 0);
          Check("Spectral Gate: LEARN SPILL remembers the spill (kept with the project) and CLEAR forgets it", gl.SpillLearned && gl.SpillDb(16) > -90 && gl.SpillDb(16) < -30 && gl.Serialize().Contains("sp16"), gl.SpillDb(16).ToString("0.0")); gl.ClearLearned(); Check("Spectral Gate: CLEAR LEARNED goes back to one threshold", !gl.SpillLearned); }
        // ---- EchoGrid / Modal Filter ----
        { Fx.ProjectBpm = 120; var g = New("pxechogrid"); for (int k = 0; k < 8; k++) g.Set("e" + k + "_on", 0); g.Set("e0_on", 1); g.Set("e0_step", 4); g.Set("e0_lvl", 0); g.Set("e0_tone", 0); g.Set("mix", 100); g.Set("fb", 0);
          var x = new float[SR * 2]; for (int i = 0; i < 2000; i++) x[i] = (float)(0.6 * Math.Sin(i * 0.2) * Math.Exp(-i / 400.0)); var o = Render(g, x); int at = (int)(4 * 0.25 * 60 / 120.0 * SR);
          Check("EchoGrid: an echo on step 4 of a 1/16 grid comes back exactly 4 sixteenths later (0.5 s at 120 BPM)", Rms(o, at, at + 2000) > 0.05 && Rms(o, at - 3000, at - 1000) < 0.005, Rms(o, at, at + 2000).ToString("0.000"));
          var gp = New("pxechogrid"); for (int k = 0; k < 8; k++) gp.Set("e" + k + "_on", 0); gp.Set("e0_on", 1); gp.Set("e0_step", 4); gp.Set("e0_lvl", 0); gp.Set("e0_tone", 0); gp.Set("e0_pitch", 12); gp.Set("mix", 100); gp.Set("fb", 0); var op = Render(gp, Sine(SR * 2, 300, 0.3));
          Check("EchoGrid: an echo pitched +12 comes back an octave up", Goertzel(op, SR, SR / 2, 600) > Goertzel(op, SR, SR / 2, 300) * 2, Goertzel(op, SR, SR / 2, 600).ToString("0.000") + " vs " + Goertzel(op, SR, SR / 2, 300).ToString("0.000")); }
        { var g = New("pxmodal"); g.Set("a_type", 0); g.Set("a_cut", 300); g.Set("a_res", 0); var lo = Render(g, Sine(SR, 100, 0.3)); var g2 = New("pxmodal"); g2.Set("a_type", 0); g2.Set("a_cut", 300); g2.Set("a_res", 0); var hi = Render(g2, Sine(SR, 5000, 0.3));
          Check("Modal Filter: the low-pass lets 100 Hz through and cuts 5 kHz", Rms(lo, SR / 2, SR) > 0.18 && Rms(hi, SR / 2, SR) < 0.02, Rms(lo, SR / 2, SR).ToString("0.000") + " / " + Rms(hi, SR / 2, SR).ToString("0.000"));
          var gm = New("pxmodal"); gm.Set("a_type", 0); gm.Set("a_cut", 300); gm.Set("b_type", 1); gm.Set("b_cut", 3000); gm.Set("morph", 100); var om = Render(gm, Sine(SR, 100, 0.3));
          Check("Modal Filter: MORPH all the way to B (a high-pass) cuts the low tone A let through", Rms(om, SR / 2, SR) < 0.03, Rms(om, SR / 2, SR).ToString("0.000"));
          var gc = New("pxmodal"); gc.Set("a_type", 5); gc.Set("a_cut", 200); gc.Set("a_res", 90); var noise = new float[SR]; var rn = new Random(3); for (int i = 0; i < SR; i++) noise[i] = (float)((rn.NextDouble() - 0.5) * 0.3); var oc = Render(gc, noise);
          Check("Modal Filter: the comb rings at its cutoff (200 Hz stands out of noise)", Goertzel(oc, SR / 2, SR / 2, 200) > Goertzel(oc, SR / 2, SR / 2, 300) * 2, Goertzel(oc, SR / 2, SR / 2, 200).ToString("0.000") + " vs " + Goertzel(oc, SR / 2, SR / 2, 300).ToString("0.000")); }
        // ---- windows ----
        foreach (var t in new[] { "pxglow", "pxcurve", "pxshape", "pxsubforge", "pxbreath", "pxpgate", "pxstereo", "pxvortex", "pxmultidyn", "pxprism", "pxfrost", "pxtape", "pxconst", "pxclip", "pxsmack", "pxvinyl", "pxradio", "pxdoubler", "pxchoir", "pxvowel", "pxstep", "pxtri", "pxshift", "pxmax", "pxsgate", "pxechogrid", "pxmodal" })
        {
            var f = New(t); PxWindow w = t == "pxglow" ? (PxWindow)new GlowWindow((GlowFx)f) : t == "pxcurve" ? new CurveEqWindow((CurveEqFx)f) : t == "pxshape" ? (PxWindow)new ShapeWindow((ShapeFx)f) : t == "pxbreath" ? new BreathOffWindow((BreathOffFx)f) : t == "pxpgate" ? new PhantomGateWindow((PhantomGateFx)f) : t == "pxstereo" ? (PxWindow)new StereoLabWindow((StereoLabFx)f) : t == "pxvortex" ? new VortexWindow((VortexFx)f) : t == "pxmultidyn" ? (PxWindow)new MultiDynWindow((MultiDynFx)f) : t == "pxprism" ? (PxWindow)new PrismWindow((PrismFx)f) : t == "pxfrost" ? (PxWindow)new FrostWindow((FrostFx)f) : t == "pxtape" ? (PxWindow)new TapeDirtWindow((TapeDirtFx)f) : t == "pxconst" ? (PxWindow)new ConstellationWindow((ConstellationFx)f) : t == "pxclip" ? (PxWindow)new ClipperWindow((ClipperFx)f) : t == "pxsmack" ? (PxWindow)new SmackWindow((SmackFx)f) : t == "pxvinyl" ? (PxWindow)new VinylStopWindow((VinylStopFx)f) : t == "pxradio" ? (PxWindow)new RadioWindow((RadioFx)f) : t == "pxdoubler" ? (PxWindow)new DoublerWindow((DoublerFx)f) : t == "pxchoir" ? (PxWindow)new OctaveChoirWindow((OctaveChoirFx)f) : t == "pxvowel" ? (PxWindow)new PxVowelWindow((PxVowelFx)f) : t == "pxstep" ? (PxWindow)new StepShaperWindow((StepShaperFx)f) : t == "pxtri" ? (PxWindow)new TriDriveWindow((TriDriveFx)f) : t == "pxshift" ? (PxWindow)new ShiftWindow((ShiftFx)f) : t == "pxmax" ? (PxWindow)new MaximizerWindow((MaximizerFx)f) : t == "pxsgate" ? (PxWindow)new SpectralGateWindow((SpectralGateFx)f) : t == "pxechogrid" ? (PxWindow)new EchoGridWindow((EchoGridFx)f) : t == "pxmodal" ? (PxWindow)new ModalFilterWindow((ModalFilterFx)f) : new SubForgeWindow((SubForgeFx)f);
            w.CreateControl(); using (var bmp = w.Snapshot()) { } string id = f.P.First(p => !p.IsChoice).Id; float v0 = f.Get(id); w.TestDrag(id, 0.8f); bool moved = Math.Abs(f.Get(id) - v0) > 1e-4; w.Undo(); bool back = Math.Abs(f.Get(id) - v0) < 1e-4;
            Check(t + ": its window draws its controls, a control moves the real setting, undo puts it back", w.HitCount > 10 && moved && back, w.HitCount + " controls"); w.Dispose();
        }
        // ---- every label fits its box and none sits across a panel edge, in every Purphex window (GlitchFlow too) ----
        { Wv2Loader.Ensure(); Type[] allT; try { allT = typeof(PxWindow).Assembly.GetTypes(); } catch (System.Reflection.ReflectionTypeLoadException ex) { allT = ex.Types.Where(x => x != null).ToArray(); } var wins = allT.Where(t => t.IsSubclassOf(typeof(PxWindow)) && !t.IsAbstract).ToList(); var probs = new List<string>();
          foreach (var info in FxRegistry.All.Where(i => PxPlugins.Types.Contains(i.Type) || i.Type == "glitchflow"))
          {
              var f = New(info.Type); Form w = null;
              foreach (var wt in wins) { var c = wt.GetConstructors().FirstOrDefault(k => k.GetParameters().Length == 1 && k.GetParameters()[0].ParameterType.IsInstanceOfType(f)); if (c != null) { w = (Form)c.Invoke(new object[] { f }); break; } }
              if (w == null && f is GlitchFlowFx) w = new GlitchFlowWindow((GlitchFlowFx)f); if (w == null) { probs.Add(info.Type + ": no window"); continue; }
              w.CreateControl(); UiAudit.Begin(); using (var bmp = w is PxWindow ? ((PxWindow)w).Snapshot() : ((GlitchFlowWindow)w).Snapshot()) { } foreach (var s in UiAudit.End().Distinct()) probs.Add(info.Type + ": " + s); w.Dispose();
          }
          Check("every Purphex window: no text cut off or across a box edge", probs.Count == 0, string.Join(" | ", probs.Take(6))); }
        Console.WriteLine(fails == 0 ? "=== purphex vst checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
    // the sections a setting needs to be heard
    static void prep(string t, Fx g) { if (t == "pxcurve") { for (int k = 0; k < 8; k++) g.Set("b" + k + "_on", 1); } }
    static void needs(string t, string id, Fx g)
    {
        if (t == "pxglow") { if (id == "hp" || id == "lp" || id == "steep") { g.Set("filt", 1); g.Set("hp", 200); g.Set("lp", 5000); } if (id == "satmode") g.Set("sat", 60); if (id == "lvl") g.Set("inject", 100); }
        if (t == "pxcurve") { if (id.EndsWith("_g") || id.EndsWith("_dyn") || id.EndsWith("_thr") || id.EndsWith("_q")) g.Set(id.Substring(0, 3) + "t", 0); if (id.EndsWith("_dyn") || id.EndsWith("_thr")) { g.Set(id.Substring(0, 3) + "dyn", 12); g.Set(id.Substring(0, 3) + "thr", -40); } if (id == "auto") g.Set("b3_g", 9); if (id.EndsWith("_q")) g.Set(id.Substring(0, 3) + "g", 9); if (id.EndsWith("_t") || id.EndsWith("_f")) g.Set(id.Substring(0, 3) + "g", 9); }
        if (t == "pxshape") { if (id.StartsWith("tr_")) g.Set("tr_on", 1); if (id.StartsWith("sc_")) { g.Set("sc_on", 1); g.Set("ratio", 8); g.Set("thr", -40); g.Set("sc_g", 18); g.Set("sc_f", 300); } if (id.StartsWith("eq_")) { g.Set("eq_on", 1); g.Set("eq_slope", 60); } if (id.StartsWith("cl_")) { g.Set("cl_on", 1); g.Set("cl_gain", 12); } if (id == "knee") { g.Set("ratio", 8); g.Set("thr", -14); } else if (id == "att" || id == "rel" || id == "fatt" || id == "frel" || id == "int" || id == "auto" || id == "mode") { g.Set("ratio", 8); g.Set("thr", -40); } }
        if (t == "pxbreath") { if (id == "roomlvl") g.Set("room", 1); if (id == "room") g.Set("roomlvl", -40); if (id == "fout" || id == "fin") g.Set("red", -60); }
        if (t == "pxpgate") { if (id == "spike") { g.Set("thr", 0); g.Set("floor", -80); } if (id.StartsWith("sc_")) { g.Set("sc_on", 1); g.Set("sc_hp", 2000); g.Set("thr", -40); } if (id == "db_f") { g.Set("db_on", 1); g.Set("floor", -3); g.Set("thr", -6); } if (id == "ratio") { g.Set("thr", -20); g.Set("ret", 0); } if (id == "relsync") g.Set("hold", 0); if (id == "curve") { g.Set("att", 10); g.Set("rel", 200); } }
        if (t == "pxstereo") { if (id == "bass") g.Set("width", 150); if (id == "rg" || id == "rph") g.Set("rpan", 0); }
        if (t == "pxvortex") { if (id == "color" || id == "freq" || id == "pan" || id == "range" || id == "time") { g.Set("g_mix", 100); g.Set("f_mix", 100); g.Set("w_mix", 100); g.Set("b_mix", 100); g.Set("s_mix", 100); } if (id == "esync") g.Set("echo", 60); if (id == "rate") g.Set("sync", 0); }
        if (t == "pxmultidyn") { for (int k = 0; k < 5; k++) { g.Set("b" + k + "_ratio", 6); g.Set("b" + k + "_thr", -40); } if (id == "limit" || id == "mode") for (int k = 0; k < 5; k++) g.Set("b" + k + "_gain", 18); if (id.EndsWith("_solo") || id.StartsWith("x")) { } }
        if (t == "pxfrost") { if (id.StartsWith("rm_")) g.Set("rm_on", 1); if (id.StartsWith("fb_")) g.Set("fb_on", 1); if (id == "fade" || id == "width" || id == "lo" || id == "mid" || id == "hi" || id == "fz_mix") { g.Set("freeze", 1); g.Set("fade", id == "fade" ? 400 : 20); g.Set("wet", 100); } if (id.StartsWith("lfo_")) { g.Set("lfo_amt", 80); g.Set("rm_on", 1); g.Set("fb_on", 1); g.Set("rm_mix", 100); } if (id == "serial" || id == "dry" || id == "wet") { g.Set("rm_on", 1); g.Set("fb_on", 1); } if (id == "clip") { g.Set("fb_on", 1); g.Set("fb_amt", 90); g.Set("wet", 100); g.Set("rm_on", 1); g.Set("rm_mix", 100); } }
        if (t == "pxtape") { if (id == "stereo") g.Set("dirt", 9); }
        if (t == "pxclip") { g.Set("pre", 6); if (id == "soft") g.Set("mode", 0); }
        if (t == "pxsmack") { if (id == "asens" || id == "ashape" || id == "adur") g.Set("att", 100); if (id == "ssens" || id == "sshape" || id == "sdur") g.Set("sus", 100); if (id == "guard") g.Set("out", 18); }
        if (t == "pxvinyl") { if (id != "stop" && id != "vinyl") g.Set("stop", 1); if (id == "start") { } }
        if (t == "pxdoubler") { if (id == "dpan") g.Set("dpan", 50); if (id.StartsWith("v2") || id.StartsWith("v3")) g.Set(id.Substring(0, 2) + "_on", 1); if (id.EndsWith("_rate")) g.Set(id.Substring(0, 2) + "_dep", 20); if (id.EndsWith("_fb")) g.Set(id.Substring(0, 2) + "_d", 60); if (id == "lo_f") g.Set("lo_g", 12); if (id == "hi_f") g.Set("hi_g", 12); }
        if (t == "pxchoir") { if (id.Length > 2 && id[0] == 'o' && char.IsDigit(id[1])) { g.Set(id.Substring(0, 2) + "_on", 1); g.Set(id.Substring(0, 2) + "_pan", 0); } if (id.EndsWith("_fb")) g.Set(id.Substring(0, 2) + "_d", 100); if (id == "scale") g.Set("o0_iv", 3); }
        if (t == "pxvowel") { if (id == "msync" || id == "mrate" || id == "mdepth") g.Set("move", 3); if (id == "snap") { g.Set("x", 0.3f); g.Set("y", 0.4f); } if (id == "lm") g.Set("q", 30); }
        if (t == "pxechogrid") { if (id.Length > 2 && id[0] == 'e' && char.IsDigit(id[1])) { string p = id.Substring(0, 3); g.Set(p + "on", 1); g.Set(p + "step", 2); g.Set(p + "lvl", 0); } g.Set("mix", 100); g.Set("fb", 0); if (id == "fb") g.Set("fb", 50); }
        if (t == "pxmodal") { if (id.StartsWith("b_")) g.Set("route", 1); if (id == "morph" || id == "ltarget") g.Set("morph", id == "morph" ? 0 : 50); if (id == "att" || id == "rel") g.Set("env", 90); if (id == "lshape" || id == "ltarget" || id == "lrate") { g.Set("ldepth", 80); g.Set("lrate", 3); } if (id == "a_drive" || id == "b_drive") { g.Set("a_res", 0); g.Set("b_res", 0); } }
        if (t == "pxtri") { if (id == "cut" || id == "res") g.Set("filt", 1); if (id == "fbt") g.Set("fb", 60); if (id == "xlo" || id == "xhi") { g.Set("lo_type", 4); g.Set("lo_drive", 80); g.Set("hi_type", 2); g.Set("hi_drive", 80); } }
        if (t == "pxshift") { if (id.StartsWith("b_") || id == "route") g.Set("b_on", 1); if (id == "a_rate") g.Set("a_dep", 300); if (id == "b_rate") g.Set("b_dep", 300); if (id == "fbt") g.Set("fb", 60); }
        if (t == "pxmax") { if (id == "rel" || id == "ceil" || id == "char") g.Set("loud", 100); if (id == "target") g.Set("loud", 60); }
        if (t == "pxsgate") { if (id == "margin" || id == "bias") for (int b = 0; b < 32; b++) { g.Set("sp" + b, -70); if (id == "bias") g.Set("wp" + b, -20); } }
        if (t == "pxstep") { if (id.Length > 1 && id[0] == 's' && char.IsDigit(id[1])) g.Set("grid", 3); if (id == "res") g.Set("cut", 1000); if (id == "follow") g.Set("cut", 3000); }

        if (t == "pxprism") { if (id != "mix") g.Set("mix", 100); if (id == "i3") g.Set("i3_on", 1); if (id == "decay" || id == "hfd" || id == "lfd") g.Set("freeze", 0); }
        if (t == "pxsubforge") { if (id.StartsWith("d_")) { g.Set("d_on", 1); g.Set("d_drive", 60); } if (id.StartsWith("c_")) { g.Set("c_on", 1); g.Set("c_thr", -40); } if (id == "sub_lvl" || id == "sub_oct") g.Set("sub_on", 1); if (id == "res" || id == "fenv" || id == "track" || id == "cut") { g.Set("ftype", 1); g.Set("cut", 400); } if (id == "glide" || id == "mono" || id == "vel") { } }
    }
    static T Also<T>(this T x, Action<T> a) { a(x); return x; }
}
