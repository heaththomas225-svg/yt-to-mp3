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
    static float[] Render(Fx f, float[] src) { var l = (float[])src.Clone(); var r = (float[])src.Clone(); Run(f, l, r, 512); return l; }
    static float[] Note(Fx f, int note, int frames, int offAt = -1)
    {
        var inst = (InstrumentFx)f; var l = new float[frames]; var r = new float[frames]; var bl = new float[512]; var br = new float[512]; inst.Midi(0x90, (byte)note, 100, false);
        for (int p = 0; p < frames; p += 512) { int n = Math.Min(512, frames - p); if (offAt >= 0 && p >= offAt && p - 512 < offAt) inst.Midi(0x80, (byte)note, 0, false); Array.Clear(bl, 0, n); Array.Clear(br, 0, n); f.BeginBlock(null, p, n, false); f.Process(bl, br, n); Array.Copy(bl, 0, l, p, n); }
        return l;
    }
    static readonly Dictionary<string, string[]> Skip = new Dictionary<string, string[]> {
        { "pxglow", new string[0] }, { "pxcurve", new[] { "pre", "post", "scale" } }, { "pxshape", new string[0] }, { "pxsubforge", new[] { "glide", "mono" } } };   // glide and voices need two notes: tested on their own below

    [STAThread]
    static void Main()
    {
        StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_px_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(StudioEnv.DataDir); AppDomain.MonitoringIsEnabled = true;
        var types = new[] { "pxglow", "pxcurve", "pxshape", "pxsubforge" };
        foreach (var t in types)
        {
            var info = FxRegistry.All.FirstOrDefault(i => i.Type == t); var f = New(t); bool inst = f is InstrumentFx;
            Check(t + ": in the list (" + (inst ? "Instruments" : "Purphex VST") + "), made by the registry, has factory presets", info != null && info.Category == (inst ? "Instruments" : "Purphex VST") && FxRegistry.IsBuiltin(t) && Presets.For(t).Count >= 12, info == null ? "missing" : info.Name + ", " + Presets.For(t).Count + " presets");
            // every setting changes the sound (a change of one step from its default, or to an end, with the sections it needs switched on)
            var src = Voice(SR * 2); Func<Action<Fx>, float[]> go = set => { var g = New(t); prep(t, g); set(g); return inst ? Note(g, 36, SR * 2, SR) : Render(g, src); };
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
            var ho = inst ? Note(hot, 36, SR * 2) : Render(hot, src); Check(t + ": everything on at its strongest stays finite and below +12 dB", Finite(ho) && Peak(ho) < 4, "peak " + Peak(ho).ToString("0.00"));
            var bad = new List<string>();
            foreach (var kv in Presets.For(t))
            {
                foreach (var tok in kv.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) { int eq = tok.IndexOf('='); var p = eq > 0 ? f.Find(tok.Substring(0, eq)) : null; float v; if (p == null || !float.TryParse(tok.Substring(eq + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) || v < p.Min || v > p.Max) bad.Add(kv.Key + ": " + tok); }
                var g = New(t); Presets.Apply(g, kv.Value); var o = inst ? Note(g, 36, SR) : Render(g, Voice(SR)); if (!Finite(o) || Peak(o) > 4) bad.Add(kv.Key + " level " + Peak(o).ToString("0.0"));
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
        // ---- windows ----
        foreach (var t in new[] { "pxglow", "pxcurve", "pxshape", "pxsubforge" })
        {
            var f = New(t); PxWindow w = t == "pxglow" ? (PxWindow)new GlowWindow((GlowFx)f) : t == "pxcurve" ? new CurveEqWindow((CurveEqFx)f) : t == "pxshape" ? (PxWindow)new ShapeWindow((ShapeFx)f) : new SubForgeWindow((SubForgeFx)f);
            w.CreateControl(); using (var bmp = w.Snapshot()) { } string id = f.P.First(p => !p.IsChoice).Id; float v0 = f.Get(id); w.TestDrag(id, 0.8f); bool moved = Math.Abs(f.Get(id) - v0) > 1e-4; w.Undo(); bool back = Math.Abs(f.Get(id) - v0) < 1e-4;
            Check(t + ": its window draws its controls, a control moves the real setting, undo puts it back", w.HitCount > 15 && moved && back, w.HitCount + " controls"); w.Dispose();
        }
        Console.WriteLine(fails == 0 ? "=== purphex vst checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
    // the sections a setting needs to be heard
    static void prep(string t, Fx g) { if (t == "pxcurve") { for (int k = 0; k < 8; k++) g.Set("b" + k + "_on", 1); } }
    static void needs(string t, string id, Fx g)
    {
        if (t == "pxglow") { if (id == "hp" || id == "lp" || id == "steep") { g.Set("filt", 1); g.Set("hp", 200); g.Set("lp", 5000); } if (id == "satmode") g.Set("sat", 60); if (id == "lvl") g.Set("inject", 100); }
        if (t == "pxcurve") { if (id.EndsWith("_g") || id.EndsWith("_dyn") || id.EndsWith("_thr") || id.EndsWith("_q")) g.Set(id.Substring(0, 3) + "t", 0); if (id.EndsWith("_dyn") || id.EndsWith("_thr")) { g.Set(id.Substring(0, 3) + "dyn", 12); g.Set(id.Substring(0, 3) + "thr", -40); } if (id == "auto") g.Set("b3_g", 9); if (id.EndsWith("_q")) g.Set(id.Substring(0, 3) + "g", 9); if (id.EndsWith("_t") || id.EndsWith("_f")) g.Set(id.Substring(0, 3) + "g", 9); }
        if (t == "pxshape") { if (id.StartsWith("tr_")) g.Set("tr_on", 1); if (id.StartsWith("sc_")) { g.Set("sc_on", 1); g.Set("ratio", 8); g.Set("thr", -40); g.Set("sc_g", 18); g.Set("sc_f", 300); } if (id.StartsWith("eq_")) { g.Set("eq_on", 1); g.Set("eq_slope", 60); } if (id.StartsWith("cl_")) { g.Set("cl_on", 1); g.Set("cl_gain", 12); } if (id == "knee") { g.Set("ratio", 8); g.Set("thr", -14); } else if (id == "att" || id == "rel" || id == "fatt" || id == "frel" || id == "int" || id == "auto" || id == "mode") { g.Set("ratio", 8); g.Set("thr", -40); } }
        if (t == "pxsubforge") { if (id.StartsWith("d_")) { g.Set("d_on", 1); g.Set("d_drive", 60); } if (id.StartsWith("c_")) { g.Set("c_on", 1); g.Set("c_thr", -40); } if (id == "sub_lvl" || id == "sub_oct") g.Set("sub_on", 1); if (id == "res" || id == "fenv" || id == "track" || id == "cut") { g.Set("ftype", 1); g.Set("cut", 400); } if (id == "glide" || id == "mono" || id == "vel") { } }
    }
    static T Also<T>(this T x, Action<T> a) { a(x); return x; }
}
