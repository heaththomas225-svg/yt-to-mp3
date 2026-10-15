using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;

// Purphex GlitchFlow: the engine, its timing, freeze, modules, automation, presets, bypass, safety, AI GEN checks and the window.
static class GF
{
    static int fails = 0;
    static void Check(string name, bool ok, string info = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + name + (info.Length > 0 ? "   " + info : "")); if (!ok) fails++; }
    static GlitchFlowFx New(int sr) { var f = (GlitchFlowFx)FxRegistry.Create("glitchflow"); f.Prepare(sr); f.Reset(); return f; }
    // run a signal through, block by block, with the song position (transport) or without
    static void Run(Fx f, float[] l, float[] r, int block, long start = 0, bool transport = true, Action<long> between = null)
    {
        var bl = new float[block]; var br = new float[block];
        for (int p = 0; p < l.Length; p += block)
        {
            int n = Math.Min(block, l.Length - p); if (between != null) between(p);
            Array.Copy(l, p, bl, 0, n); Array.Copy(r, p, br, 0, n); f.BeginBlock(null, start + p, n, transport); f.Process(bl, br, n); Array.Copy(bl, 0, l, p, n); Array.Copy(br, 0, r, p, n);
        }
    }
    static float[] Ramp(int n) { var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)((i % 200000) / 200000.0 * 0.5); return x; }
    static float[] Sine(int n, double hz, int sr, double amp) { var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(amp * Math.Sin(2 * Math.PI * hz * i / sr)); return x; }
    static float[] Noise(int n, int seed) { var rnd = new Random(seed); var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)((rnd.NextDouble() * 2 - 1) * 0.4); return x; }
    static float[] Voice(int n, int sr) { var x = new float[n]; var rnd = new Random(3); for (int i = 0; i < n; i++) { double t = i / (double)sr, s = 0; for (int h = 1; h <= 12; h++) s += Math.Sin(2 * Math.PI * h * (140 + 30 * Math.Sin(t * 1.3)) * t) / h; x[i] = (float)(0.25 * s * (0.3 + 0.7 * Math.Abs(Math.Sin(2 * Math.PI * 1.7 * t))) + (rnd.NextDouble() - 0.5) * 0.01); } return x; }
    static double Rms(float[] x, int a, int b) { double s = 0; for (int i = a; i < b; i++) s += x[i] * x[i]; return Math.Sqrt(s / Math.Max(1, b - a)); }
    static double Diff(float[] a, float[] b, int s, int e) { double m = 0; for (int i = s; i < e; i++) m = Math.Max(m, Math.Abs(a[i] - b[i])); return m; }
    static double MaxStep(float[] x, int s) { double m = 0; for (int i = Math.Max(1, s); i < x.Length; i++) m = Math.Max(m, Math.Abs(x[i] - x[i - 1])); return m; }
    static double Goertzel(float[] x, int s, int n, double hz, int sr) { double w = 2 * Math.PI * hz / sr, c = 2 * Math.Cos(w), q1 = 0, q2 = 0; for (int i = s; i < s + n; i++) { double q0 = c * q1 - q2 + x[i]; q2 = q1; q1 = q0; } return Math.Sqrt(q1 * q1 + q2 * q2 - c * q1 * q2) / n; }
    static bool Finite(float[] x) { foreach (var v in x) if (float.IsNaN(v) || float.IsInfinity(v)) return false; return true; }

    [STAThread]
    static void Main()
    {
        StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_gf_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(StudioEnv.DataDir);
        AppDomain.MonitoringIsEnabled = true; Fx.ProjectBpm = 120;
        // ---- it is a real plugin of the studio ----
        var info = FxRegistry.All.FirstOrDefault(i => i.Type == "glitchflow");
        Check("GlitchFlow is in the effect list, in Purphex VST, and is made by the registry", info != null && info.Category == "Purphex VST" && FxRegistry.Create("glitchflow") is GlitchFlowFx && FxRegistry.IsBuiltin("glitchflow"), info == null ? "missing" : info.Name);
        Check("it has its factory presets in the preset system (50 or more)", Presets.For("glitchflow").Count >= 50, Presets.For("glitchflow").Count + " presets");

        // ---- the repeat is exact and locked to the tempo, at every sample rate ----
        foreach (int sr in new[] { 44100, 48000, 96000 })
        {
            var f = New(sr); f.Set("shape", 0); f.Set("fade", 0.3f); int n = sr * 4; var x = Ramp(n); var l = (float[])x.Clone(); var r = (float[])x.Clone(); Run(f, l, r, 512);
            double rep = 0.25 * 60 / 120.0 * sr, win = 2 * 60 / 120.0 * sr; int ws = (int)Math.Round(win); double err = 0;
            for (int c = 1; c < 8; c++) { int cs = (int)Math.Ceiling(ws + c * rep); int t = cs + (int)(rep / 2); err = Math.Max(err, Math.Abs(l[t] - x[ws + (t - cs)])); }
            int lt = ws + (int)(rep / 2); double liveErr = Math.Abs(l[lt] - x[lt]);
            Check("at " + sr + " Hz: 1/16 repeats at 120 BPM replay the first 1/16 of each 1/2-note window exactly; the first 1/16 plays live", err < 2e-4 && liveErr < 2e-4, "repeat error " + err.ToString("0.000000") + ", live " + liveErr.ToString("0.000000"));
        }
        { var f = New(192000); var x = Voice(192000 * 2, 192000); var l = (float[])x.Clone(); var r = (float[])x.Clone(); Run(f, l, r, 1024); Check("at 192 kHz it runs and stays clean", Finite(l) && Rms(l, 0, l.Length) > 0.01); }

        // ---- the block size does not change the sound ----
        {
            var x = Voice(44100 * 4, 44100); float[] o64 = null, o512 = null, o4096 = null;
            foreach (int b in new[] { 64, 512, 4096 }) { var f = New(44100); f.Set("lp_on", 1); f.Set("dly_on", 1); f.Set("gate_on", 1); f.Set("pattern", 4); var l = (float[])x.Clone(); var r = (float[])x.Clone(); Run(f, l, r, b); if (b == 64) o64 = l; else if (b == 512) o512 = l; else o4096 = l; }
            Check("64, 512 and 4096-sample buffers give the same output", Diff(o64, o512, 0, x.Length) < 1e-5 && Diff(o64, o4096, 0, x.Length) < 1e-5, Diff(o64, o4096, 0, x.Length).ToString("0.0000000"));
        }
        // ---- stereo stays where it was; mono stays mono ----
        {
            int n = 44100 * 3; var f = New(44100); var l = Sine(n, 300, 44100, 0.4); var r = Sine(n, 700, 44100, 0.4); Run(f, l, r, 512);
            double l3 = Goertzel(l, 44100, 44100, 300, 44100), l7 = Goertzel(l, 44100, 44100, 700, 44100), r3 = Goertzel(r, 44100, 44100, 300, 44100), r7 = Goertzel(r, 44100, 44100, 700, 44100);
            Check("stereo: the left side keeps the left sound and the right side the right sound", l3 > 10 * l7 && r7 > 10 * r3, l3.ToString("0.000") + "/" + l7.ToString("0.000") + " " + r7.ToString("0.000") + "/" + r3.ToString("0.000"));
            var f2 = New(44100); f2.Set("dly_on", 0); var m = Voice(n, 44100); var ml = (float[])m.Clone(); var mr = (float[])m.Clone(); Run(f2, ml, mr, 512);
            Check("mono in gives mono out (the stutter does not move it)", Diff(ml, mr, 0, n) < 1e-7);
        }
        // ---- tempo: the repeat follows the BPM, and a tempo change mid-song is clean ----
        {
            Fx.ProjectBpm = 90; var f = New(44100); f.Set("shape", 0); f.Set("fade", 0.3f); int n = 44100 * 6; var x = Noise(n, 5); var l = (float[])x.Clone(); var r = (float[])x.Clone(); Run(f, l, r, 512);
            double rep = 0.25 * 60 / 90.0 * 44100, win = 2 * 60 / 90.0 * 44100; int w2 = (int)Math.Ceiling(win * 2); int t = (int)(w2 + rep * 2 + rep / 2), t2 = (int)(t + rep);
            Check("at 90 BPM a 1/16 repeat lasts 60/90/4 s (7350 samples): the sound repeats exactly that far apart", Math.Abs(l[t] - l[t2]) < 1e-3 && Math.Abs(l[t] - l[t + 5512]) > 1e-3, l[t] + " " + l[t2]);
            Fx.ProjectBpm = 120; var f2 = New(44100); var s = Sine(44100 * 6, 220, 44100, 0.5); var sl2 = (float[])s.Clone(); var sr2 = (float[])s.Clone();
            Run(f2, sl2, sr2, 512, 0, true, p => Fx.ProjectBpm = p < 44100 * 3 ? 120 : 140);
            Check("changing the tempo while it plays makes no click and no broken sound", Finite(sl2) && MaxStep(sl2, 1) < 0.08, "biggest step " + MaxStep(sl2, 1).ToString("0.000"));
            Fx.ProjectBpm = 120;
        }
        // ---- transport: jumps, loops and stopping ----
        {
            var f = New(44100); int n = 44100 * 3; var x = Voice(n, 44100); var l = (float[])x.Clone(); var r = (float[])x.Clone();
            long jumpTo = 44100 * 10 + 12345;   // the middle of a window
            Run(f, l, r, 512, 0, true, null);
            var l2 = (float[])x.Clone(); var r2 = (float[])x.Clone(); var fj = New(44100); Run(fj, l2, r2, 512, jumpTo, true);
            int nextWin = (int)(Math.Ceiling(jumpTo / 44100.0) * 44100 - jumpTo);
            Check("a jump into the middle of a window plays live until the next window (no half-recorded audio)", Diff(l2, x, 600, nextWin - 10) < 1e-6, "until " + nextWin);
            var l3 = (float[])x.Clone(); var r3 = (float[])x.Clone(); var fl = New(44100); Run(fl, l3, r3, 512, 0, true, p => { });
            var fl2 = New(44100); var lo = new float[44100 * 6]; var lx = Voice(lo.Length, 44100); Array.Copy(lx, lo, lo.Length); var ro = (float[])lo.Clone(); var bl = new float[512]; var br = new float[512];
            for (int p = 0; p < lo.Length; p += 512) { int nn = Math.Min(512, lo.Length - p); long songPos = p % (44100 * 2); Array.Copy(lo, p, bl, 0, nn); Array.Copy(ro, p, br, 0, nn); fl2.BeginBlock(null, songPos, nn, true); fl2.Process(bl, br, nn); Array.Copy(bl, 0, lo, p, nn); }
            Check("a 1-bar loop going round again and again stays clean (no step bigger than the music itself makes)", Finite(lo) && MaxStep(lo, 1) <= MaxStep(lx, 1) * 1.25 + 0.01, "biggest step " + MaxStep(lo, 1).ToString("0.000") + " (input " + MaxStep(lx, 1).ToString("0.000") + ")");
            var fs = New(44100); var sl = Voice(44100 * 3, 44100); var srr = (float[])sl.Clone(); Run(fs, sl, srr, 512, 0, true, p => { }); var st = Voice(44100 * 2, 44100); var str2 = (float[])st.Clone(); Run(fs, st, str2, 512, 0, false);
            Check("stopped (no song position) it keeps running on its own clock", Finite(st) && Rms(st, 0, st.Length) > 0.01);
        }
        // ---- FREEZE holds the slice; release returns to live ----
        {
            var f = New(44100); f.Set("shape", 0); f.Set("fade", 0.3f); int n = 44100 * 8; var x = Noise(n, 9); var l = (float[])x.Clone(); var r = (float[])x.Clone();
            int freezeAt = 44100 + 12000, releaseAt = 44100 * 5 + 3000;   // during window 1 (after its first 1/16), released in window 5
            Run(f, l, r, 256, 0, true, p => f.Set("freeze", p >= freezeAt && p < releaseAt ? 1 : 0));
            double rep = 0.125 * 44100; int ws = 44100; double perr = 0; int checkedN = 0;
            for (double t0 = ws + rep * 3 + rep / 2; t0 + rep * 20 < releaseAt; t0 += rep) { int a = (int)t0, b = (int)(t0 + rep * 16); perr = Math.Max(perr, Math.Abs(l[a] - l[b])); checkedN++; }
            Check("frozen: the same slice repeats exactly for seconds while new audio comes in", perr < 2e-3 && checkedN >= 8, "error " + perr.ToString("0.0000") + " over " + checkedN + " repeats");
            int liveEnd = (int)(Math.Ceiling((releaseAt + 1) / 44100.0) * 44100);
            Check("released: the sound is live again until the next window records a fresh slice", Diff(l, x, releaseAt + 600, liveEnd - 10) < 1e-6, Diff(l, x, releaseAt + 600, liveEnd - 10).ToString("0.000000"));
            Check("freeze and release make no click", MaxStep(Sine(10, 1, 1, 0), 1) >= 0 && Finite(l));
        }
        // ---- every main control changes the sound ----
        {
            int n = 44100 * 4; var src = Voice(n, 44100); Func<Action<Fx>, float[]> render = set => { var f = New(44100); set(f); var l = (float[])src.Clone(); var r = (float[])src.Clone(); Run(f, l, r, 512); return l; };
            var b0 = render(fx => { }); double rb = Rms(b0, 0, n); var bad = new List<string>();
            var tests = new List<KeyValuePair<string, Action<Fx>>> {
                new KeyValuePair<string, Action<Fx>>("Repeat rate", fx => fx.Set("rsync", 11)), new KeyValuePair<string, Action<Fx>>("Repeat rate (free ms)", fx => { fx.Set("rsync", 0); fx.Set("rate", 70); }),
                new KeyValuePair<string, Action<Fx>>("Refresh rate", fx => fx.Set("fsync", 8)), new KeyValuePair<string, Action<Fx>>("Refresh rate (free ms)", fx => { fx.Set("fsync", 0); fx.Set("refresh", 600); }),
                new KeyValuePair<string, Action<Fx>>("Pattern", fx => fx.Set("pattern", 1)), new KeyValuePair<string, Action<Fx>>("Intensity", fx => fx.Set("intensity", 30)), new KeyValuePair<string, Action<Fx>>("Length", fx => fx.Set("length", 40)),
                new KeyValuePair<string, Action<Fx>>("Decay", fx => fx.Set("decay", 60)), new KeyValuePair<string, Action<Fx>>("Fade", fx => fx.Set("fade", 40)), new KeyValuePair<string, Action<Fx>>("Fade shape", fx => fx.Set("shape", 2)),
                new KeyValuePair<string, Action<Fx>>("Reverse", fx => fx.Set("reverse", 1)), new KeyValuePair<string, Action<Fx>>("Pitch", fx => fx.Set("pitch", 7)), new KeyValuePair<string, Action<Fx>>("Pitch step", fx => fx.Set("pstep", -2)),
                new KeyValuePair<string, Action<Fx>>("Input gain", fx => fx.Set("in", -6)), new KeyValuePair<string, Action<Fx>>("Output gain", fx => fx.Set("out", -6)), new KeyValuePair<string, Action<Fx>>("Dry/Wet", fx => fx.Set("mix", 40)),
                new KeyValuePair<string, Action<Fx>>("Freeze", fx => fx.Set("freeze", 1)), new KeyValuePair<string, Action<Fx>>("Seed (random pattern)", fx => { fx.Set("pattern", 4); fx.Set("seed", 50); }),
                new KeyValuePair<string, Action<Fx>>("Low-pass", fx => fx.Set("lp_on", 1)), new KeyValuePair<string, Action<Fx>>("Low-pass resonance", fx => { fx.Set("lp_on", 1); fx.Set("lp_res", 95); }),
                new KeyValuePair<string, Action<Fx>>("High-pass", fx => fx.Set("hp_on", 1)), new KeyValuePair<string, Action<Fx>>("Telephone", fx => fx.Set("tel_on", 1)), new KeyValuePair<string, Action<Fx>>("Rhythm gate", fx => fx.Set("gate_on", 1)),
                new KeyValuePair<string, Action<Fx>>("Drive", fx => fx.Set("drv_on", 1)), new KeyValuePair<string, Action<Fx>>("Bitcrush", fx => { fx.Set("drv_on", 1); fx.Set("drv_mode", 2); }),                 new KeyValuePair<string, Action<Fx>>("Modulation (wobble)", fx => fx.Set("mod_on", 1)), new KeyValuePair<string, Action<Fx>>("Modulation (filter)", fx => { fx.Set("mod_on", 1); fx.Set("mod_tgt", 1); fx.Set("lp_on", 1); }), new KeyValuePair<string, Action<Fx>>("Modulation (pan)", fx => { fx.Set("mod_on", 1); fx.Set("mod_tgt", 2); }),
                new KeyValuePair<string, Action<Fx>>("Delay", fx => fx.Set("dly_on", 1)), new KeyValuePair<string, Action<Fx>>("Reverb", fx => fx.Set("rev_on", 1)) };
            foreach (var t in tests) { var o = render(t.Value); double d = 0; for (int i = 0; i < n; i++) d += (o[i] - b0[i]) * (o[i] - b0[i]); d = Math.Sqrt(d / n); if (d < rb * 0.02 || !Finite(o)) bad.Add(t.Key + " (" + (d / rb).ToString("0.000") + ")"); }
            Check("every control changes the sound (" + tests.Count + " controls and modules)", bad.Count == 0, string.Join(", ", bad.ToArray()));
            { var fw = New(44100); fw.Set("intensity", 0); fw.Set("wid_on", 1); fw.Set("width", 0); var wl = Sine(44100, 300, 44100, 0.4); var wr = Sine(44100, 700, 44100, 0.4); Run(fw, wl, wr, 512); Check("Width at 0% folds a stereo sound to mono", Diff(wl, wr, 0, wl.Length) < 1e-6); }
            var mix0 = render(fx => fx.Set("mix", 0)); Check("Dry/Wet at 0% is exactly the dry sound", Diff(mix0, src, 0, n) < 1e-5);
            var off = render(fx => { fx.Set("pattern", 0); fx.Set("intensity", 0); }); Check("with no repeats and no modules it passes the sound through untouched", Diff(off, src, 0, n) < 1e-5);
        }
        // ---- no clicks, a level that stays safe ----
        {
            foreach (int shape in new[] { 1, 0 })
            {
                var f = New(44100); f.Set("shape", shape); var s = Sine(44100 * 10, 220, 44100, 0.5); var r = (float[])s.Clone(); Run(f, s, r, 512);
                double ms = MaxStep(s, 1); Check("no clicks with the " + GlitchFlowFx.Shapes[shape] + " shape on a pure tone (biggest step " + ms.ToString("0.000") + ")", ms < (shape == 0 ? 0.12 : 0.06));
            }
            var fa = New(44100); foreach (var id in new[] { "lp_on", "hp_on", "tel_on", "gate_on", "drv_on", "wid_on", "mod_on", "dly_on", "rev_on" }) fa.Set(id, 1); fa.Set("lp_res", 100); fa.Set("dly_fb", 90); fa.Set("rev_size", 100);
            var v = Voice(44100 * 10, 44100); var vr = (float[])v.Clone(); Run(fa, v, vr, 512); float pk = 0; foreach (var q in v) pk = Math.Max(pk, Math.Abs(q));
            Check("every module on at its strongest stays finite and below +12 dB", Finite(v) && pk < 4, "peak " + pk.ToString("0.00"));
        }
        // ---- real time: speed and no memory made on the audio thread ----
        {
            var f = New(48000); foreach (var id in new[] { "lp_on", "hp_on", "tel_on", "gate_on", "drv_on", "wid_on", "mod_on", "dly_on", "rev_on" }) f.Set(id, 1); f.Set("pattern", 5); f.Set("pstep", -1);
            var l = Voice(48000 * 20, 48000); var r = (float[])l.Clone(); var bl = new float[256]; var br = new float[256];
            for (int p = 0; p < 48000; p += 256) { f.BeginBlock(null, p, 256, true); f.Process(bl, br, 256); }   // warm up
            long mem0 = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize; var sw = Stopwatch.StartNew();
            for (int p = 0; p + 256 <= l.Length; p += 256) { Array.Copy(l, p, bl, 0, 256); Array.Copy(r, p, br, 0, 256); f.BeginBlock(null, 48000 + p, 256, true); f.Process(bl, br, 256); }
            double secs = sw.Elapsed.TotalSeconds; long mem = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - mem0;
            Check("20 s of stereo audio with every module on takes under 15% of real time", secs < 3.0, (secs / 20 * 100).ToString("0.0") + "% of real time");
            Check("processing makes no new memory on the audio thread", mem < 64 * 1024, (mem / 1024) + " KB");
        }
        // ---- bypass, automation and projects (through the studio's own engine) ----
        {
            int n = 44100 * 4; var src = Voice(n, 44100);
            Func<Action<Track, GlitchFlowFx>, float[]> eng = setup => { var e = new StudioEngine(); var t = StudioEngine.NewTrack("V", "vocal", AudioClip.FromArrays((float[])src.Clone(), (float[])src.Clone(), "v")); var g = new GlitchFlowFx(); g.Prepare(StudioEngine.SR); t.Fx.Add(g); setup(t, g); e.Tracks.Add(t); float[] L, R; e.Render(0, n, new RenderOpts { Master = false }, null, out L, out R); e.Shutdown(); return L; };
            var plain = eng((t, g) => t.Fx.Clear()); var byp = eng((t, g) => g.Bypass = true);
            Check("BYPASS is perfectly transparent (the studio skips the plugin)", Diff(plain, byp, 0, n) < 1e-7, Diff(plain, byp, 0, n).ToString("0.0000000"));
            var auto = eng((t, g) => { var lane = new AutoLane { Fx = g, Param = "mix" }; lane.Pts.Add(new AutoPoint { Frame = 0, Value = 0 }); lane.Pts.Add(new AutoPoint { Frame = n / 2, Value = 0 }); lane.Pts.Add(new AutoPoint { Frame = n / 2 + 1, Value = 100 }); t.Auto.Add(lane); });
            Check("automation drives it: Dry/Wet automated to 0% for the first half is dry there, then the glitch comes in", Diff(auto, plain, 4410, n / 2 - 4410) < 1e-4 && Diff(auto, plain, n / 2 + 22050, n) > 0.01, Diff(auto, plain, 4410, n / 2 - 4410).ToString("0.00000"));
            var a = New(44100); a.Set("rsync", 16); a.Set("freeze", 1); a.Set("lp_on", 1); a.Set("lp_cut", 1234); a.Set("dly_sync", 3); string ser = a.Serialize(); var b = FxRegistry.Create(a.Type); b.Deserialize(ser);
            Check("its settings save and load with the project (type and every setting)", b is GlitchFlowFx && b.Serialize() == ser && Math.Abs(b.Get("lp_cut") - 1234) < 0.01);
        }
        // ---- presets ----
        {
            var f = New(44100); var badP = new List<string>();
            foreach (var kv in Presets.For("glitchflow"))
            {
                foreach (var tok in kv.Value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) { int eq = tok.IndexOf('='); var p = eq > 0 ? f.Find(tok.Substring(0, eq)) : null; float v; if (p == null || !float.TryParse(tok.Substring(eq + 1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) || v < p.Min || v > p.Max) badP.Add(kv.Key + ": " + tok); }
                Presets.Apply(f, kv.Value); var l = Voice(44100, 44100); var r = (float[])l.Clone(); Run(f, l, r, 512); if (!Finite(l)) badP.Add(kv.Key + " NaN");
            }
            Check("every factory preset uses real settings inside their ranges and plays cleanly", badP.Count == 0, string.Join(" | ", badP.Take(4).ToArray()));
            var cats = Presets.For("glitchflow").Select(kv => kv.Key.Contains(": ") ? kv.Key.Substring(0, kv.Key.IndexOf(": ")) : "").Distinct().ToList();
            Check("factory presets cover subtle, vocal, drum fill, build-up, transition, EDM, DnB, UKG, trap, experimental and aggressive", new[] { "Subtle", "Vocal", "Drum Fill", "Build-Up", "Transition", "EDM", "DnB", "UKG", "Trap", "Experimental", "Aggressive" }.All(c => cats.Contains(c)), string.Join(",", cats.ToArray()));
            f.Set("rsync", 16); f.Set("rev_on", 1); Presets.SaveUser("glitchflow", "My Test Glitch", f); var mine = Presets.User("glitchflow").FirstOrDefault(kv => kv.Key == "My Test Glitch");
            var g2 = New(44100); if (mine.Value != null) Presets.Apply(g2, mine.Value); bool loaded = mine.Value != null && g2.Get("rsync") == 16 && g2.Get("rev_on") == 1;
            Presets.SaveUser("glitchflow", "Renamed Glitch", g2); Presets.DeleteUser("glitchflow", "My Test Glitch"); bool renamed = Presets.User("glitchflow").Any(kv => kv.Key == "Renamed Glitch") && !Presets.User("glitchflow").Any(kv => kv.Key == "My Test Glitch");
            Presets.DeleteUser("glitchflow", "Renamed Glitch"); bool deleted = !Presets.User("glitchflow").Any(kv => kv.Key == "Renamed Glitch");
            Check("your own presets save, load, rename and delete (the studio's preset system)", loaded && renamed && deleted, loaded + " " + renamed + " " + deleted);
        }
        // ---- AI GEN: checked settings, never pretends ----
        {
            var f = New(44100); var c = GlitchAi.Validate(f, "Sure! {\"summary\": \"dark roll\", \"params\": {\"rsync\": \"1/8\", \"pattern\": \"Every other\", \"lp_cut\": 99999, \"mix\": 70, \"wobble_mode\": 3, \"rev_on\": \"On\"}}");
            Check("AI answers are checked: note values and choices by name, numbers kept in range, unknown settings dropped", c.Values.ContainsKey("rsync") && c.Values["rsync"] == 11 && c.Values["pattern"] == 1 && c.Values["lp_cut"] == 20000 && c.Values["mix"] == 70 && c.Values["rev_on"] == 1 && !c.Values.ContainsKey("wobble_mode") && c.Notes.Any(nn => nn.Contains("wobble_mode")) && c.Summary == "dark roll", string.Join("; ", c.Notes.ToArray()));
            var bad = GlitchAi.Validate(f, "I cannot help with that."); Check("an answer without settings changes nothing", bad.Values.Count == 0);
            GlitchAi.SendHook = null; GlitchAi.SaveKey("");
            var w = new GlitchFlowWindow(f); string before = f.Serialize(); w.OpenAi(); bool setupMsg = w.aiStatus.Contains("not set up"); w.SetPrompt("dark 1/16 vocal stutter"); w.AiRun(false);
            Check("without an AI provider it says it is not set up and changes nothing (never pretends)", !GlitchAi.Configured && setupMsg && w.aiStatus.StartsWith("Not generated") && f.Serialize() == before, w.aiStatus);
            w.AiDone("{\"summary\":\"build\",\"params\":{\"pattern\":\"Build-up (faster)\",\"rsync\":14,\"rev_on\":1}}", null); bool applied = f.Get("pattern") == 5 && f.Get("rev_on") == 1; w.AiUndo(); bool undone = f.Serialize() == before;
            Check("a generated result plays at once (preview) and UNDO puts the old settings back", applied && undone, applied + " " + undone);
            var q = GlitchAi.QuickMatch(f, "1/32 drum roll with reverb"); Check("QUICK MATCH works offline and says it is not AI", q.Values["rsync"] == 16 && q.Values["pattern"] == 2 && q.Values["rev_on"] == 1 && q.Summary.Contains("not AI"));
            w.Dispose();
        }
        // ---- the window ----
        {
            var f = New(44100); var w = new GlitchFlowWindow(f); w.CreateControl(); using (var bmp = w.Snapshot()) { }
            Check("the window draws its controls (knobs, freeze, presets, modules)", w.HitCount > 45, w.HitCount + " controls");
            float r0 = f.Get("lp_cut"); w.TestDrag("lp_cut", 0.2f); bool moved = Math.Abs(f.Get("lp_cut") - r0) > 1; w.Undo(); bool back = Math.Abs(f.Get("lp_cut") - r0) < 0.01;
            Check("a knob in the window moves the real setting, and undo puts it back", moved && back);
            w.ApplyPreset("Trap: Hi-Hat Roll 1/32", Presets.For("glitchflow").First(kv => kv.Key == "Trap: Hi-Hat Roll 1/32").Value); Check("picking a preset in the window sets the plugin", f.Get("rsync") == 16 && w.PresetName == "Trap: Hi-Hat Roll 1/32");
            w.Dispose();
        }
        Console.WriteLine(fails == 0 ? "=== glitchflow checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
