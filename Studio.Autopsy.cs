using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;

// =====================================================================================================
//  INSTRUMENT AUTOPSY (a tool window; Tools > Instrument autopsy): takes a track's sound apart into five parts you can hear on their
//  own and use as new tracks: the TRANSIENT (the hit), the TONAL BODY (the steady notes), the RESONANT TAIL (the ring after it),
//  the MODULATION (the wobble and movement) and the MICROSTRUCTURE (the grainy, noisy texture, with how often it repeats).
//  It is a spectral guess (harmonic / percussive split, a decay detector and a movement detector), not a perfect separation: the five
//  parts add back up to the sound. The parts stay linked to the source: change the source audio or the three settings and they are
//  made again, and every track made with USE IT gets the new audio too. The first 20 seconds of the clip are taken apart.
// =====================================================================================================
partial class StudioForm
{
    static readonly string[] AutNames = { "TRANSIENT", "TONAL BODY", "RESONANT TAIL", "MODULATION", "MICROSTRUCTURE" };
    static readonly string[] AutAbout = { "The attack: the hit at the start of each sound", "The steady notes and their harmonics", "The ring that is left as each sound dies away", "The movement: wobble, tremolo and anything that pulses", "The grain: breath, noise and texture" };
    sealed class Autopsy
    {
        public Track Src; public AudioClip Clip; public long Start, Frames; public float Sharp = 0.5f, Tail = 0.5f, Move = 0.5f;   // what the parts were made from
        public float ASharp = -1, ATail = -1, AMove = -1;
        public float[][] L, R; public float[][] Peaks; public string Repeat = ""; public volatile bool Running; public volatile float Prog; public int Gen; public string Error = "";
    }
    sealed class AutLink { public Autopsy A; public int Part; }
    readonly Dictionary<Track, Autopsy> autopsies = new Dictionary<Track, Autopsy>(); readonly Dictionary<Track, AutLink> autLinks = new Dictionary<Track, AutLink>();
    string autPlaying = "";

    Autopsy AutFor(Track t) { Autopsy a; if (!autopsies.TryGetValue(t, out a)) autopsies[t] = a = new Autopsy { Src = t }; return a; }
    static bool AutStale(Autopsy a) { var t = a.Src; return !ReferenceEquals(a.Clip, t.Clip) || a.Start != t.ClipStart || a.Frames != t.ClipFrames || a.ASharp != a.Sharp || a.ATail != a.Tail || a.AMove != a.Move; }
    // called every frame: a stale autopsy (the window open on it, or tracks made from it) is made again
    void AutTick()
    {
        if (autopsies.Count == 0) return; var open = toolWins.Any(w => w.Kind == 17); var sel = selTrack >= 0 && selTrack < eng.Tracks.Count ? eng.Tracks[selTrack] : null;
        foreach (var a in autopsies.Values.ToList())
        {
            if (!eng.Tracks.Contains(a.Src) || a.Src.Clip == null || !a.Src.Clip.Loaded || a.Running || !AutStale(a)) continue;
            if ((open && a.Src == sel) || autLinks.Values.Any(k => k.A == a)) AutRun(a);
        }
    }
    void AutRun(Autopsy a)
    {
        var t = a.Src; var clip = t.Clip; if (clip == null || !clip.Loaded) return;
        long s0 = Math.Max(0, t.ClipStart), n = Math.Min(t.UsedLength, StudioEngine.SR * 20L); if (n < 4096) { a.Error = "That clip is too short to take apart."; a.Clip = clip; a.Start = t.ClipStart; a.Frames = t.ClipFrames; a.ASharp = a.Sharp; a.ATail = a.Tail; a.AMove = a.Move; return; }
        a.Clip = clip; a.Start = t.ClipStart; a.Frames = t.ClipFrames; a.ASharp = a.Sharp; a.ATail = a.Tail; a.AMove = a.Move; a.Running = true; a.Prog = 0; a.Error = ""; int gen = ++a.Gen;
        var srcL = new float[n]; var srcR = new float[n]; Array.Copy(clip.L, s0, srcL, 0, n); Array.Copy(clip.R ?? clip.L, s0, srcR, 0, n); float sharp = a.Sharp, tail = a.Tail, move = a.Move;
        new Thread(() =>
        {
            try
            {
                float[][] ol, or; string rep; AutDecompose(srcL, srcR, sharp, tail, move, p => a.Prog = p, () => a.Gen != gen, out ol, out or, out rep);
                if (a.Gen != gen) return; var pk = new float[5][]; for (int c = 0; c < 5; c++) pk[c] = AutPeaks(ol[c], or[c], 300);
                BeginInvoke(new Action(() => { if (a.Gen != gen) return; a.L = ol; a.R = or; a.Peaks = pk; a.Repeat = rep; a.Running = false; AutRelink(a); Invalidate(); }));
            }
            catch (Exception ex) { a.Error = ex.Message; a.Running = false; }
        }) { IsBackground = true, Priority = ThreadPriority.Lowest }.Start();
    }
    // the tracks made from this autopsy get the new parts
    void AutRelink(Autopsy a)
    {
        foreach (var kv in autLinks.ToList())
        {
            if (kv.Value.A != a || !eng.Tracks.Contains(kv.Key)) continue; var c = AutClip(a, kv.Value.Part); if (c == null) continue;
            lock (eng.Sync) { kv.Key.Clip = c; kv.Key.ClipStart = 0; kv.Key.ClipFrames = -1; kv.Key.Offset = a.Src.Offset; kv.Key.Source = c.Path; } clipSeen[kv.Key] = c;
        }
    }
    AudioClip AutClip(Autopsy a, int part)
    {
        if (a.L == null) return null; string path = Path.Combine(PatternAudioDir(), "Autopsy " + AutSafe(a.Src.Name) + " " + AutNames[part].ToLowerInvariant() + " " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss-fff") + ".wav");
        WavIO.Write(path, a.L[part], a.R[part], StudioEngine.SR, 24); var c = AudioClip.FromArrays(a.L[part], a.R[part], a.Src.Name + " · " + AutNames[part].ToLowerInvariant()); c.Path = path; return c;
    }
    static string AutSafe(string s) { foreach (var ch in Path.GetInvalidFileNameChars()) s = s.Replace(ch, '-'); return s.Length > 40 ? s.Substring(0, 40) : s; }
    void AutUse(Autopsy a, int part)
    {
        var c = AutClip(a, part); if (c == null) return; PushUndo();
        var nt = StudioEngine.NewTrack(a.Src.Name + " · " + AutNames[part].ToLowerInvariant(), a.Src.Role, c); nt.Color = a.Src.Color; nt.Offset = a.Src.Offset; nt.Out = a.Src.Out;
        AddTrack(nt); autLinks[nt] = new AutLink { A = a, Part = part }; clipSeen[nt] = c;
        Toast(nt.Name + " is a new track. It stays linked: change " + a.Src.Name + " or the autopsy settings and it is made again.");
    }
    void AutHear(Autopsy a, int part)
    {
        if (a.L == null) return; int n = Math.Min(a.L[part].Length, StudioEngine.SR * 8); var l = new float[n]; var r = new float[n]; Array.Copy(a.L[part], l, n); Array.Copy(a.R[part], r, n);
        float pk = 0; for (int i = 0; i < n; i++) pk = Math.Max(pk, Math.Max(Math.Abs(l[i]), Math.Abs(r[i]))); if (pk > 0.0001f && pk < 0.5f) { float k = Math.Min(8f, 0.5f / pk); for (int i = 0; i < n; i++) { l[i] *= k; r[i] *= k; } }   // a quiet part is brought up so you can hear it
        string path = Path.Combine(Path.GetTempPath(), "purphex-autopsy-" + part + ".wav"); try { WavIO.Write(path, l, r, StudioEngine.SR, 24); } catch { return; }
        if (eng.Playing) eng.Pause(); Auditioner.Stop(); Auditioner.Play(new SoundItem { Name = AutNames[part], Kind = "one-shot", Path = path }, null); autPlaying = AutNames[part];
    }
    static float[] AutPeaks(float[] l, float[] r, int bins)
    {
        var p = new float[bins]; int n = l.Length; if (n == 0) return p;
        for (int b = 0; b < bins; b++) { int s = (int)((long)n * b / bins), e = (int)((long)n * (b + 1) / bins); float m = 0; for (int i = s; i < e; i += 4) m = Math.Max(m, Math.Max(Math.Abs(l[i]), Math.Abs(r[i]))); p[b] = m; }
        return p;
    }

    // ---- the decomposition: STFT (2048, hop 512), harmonic / percussive medians, a noise share where neither wins, then the harmonic
    //      share split into steady body, decaying tail and movement. The five masks add up to 1, so the parts add back up to the sound.
    static void AutDecompose(float[] L, float[] R, float sharp, float tailSet, float moveSet, Action<float> prog, Func<bool> cancel, out float[][] outL, out float[][] outR, out string repeat)
    {
        const int N = 2048, H = 512, B = N / 2 + 1; int n = L.Length, F = (n + N) / H + 1;
        var win = new double[N]; for (int i = 0; i < N; i++) win[i] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / N);
        var mag = new float[F * B]; var re = new double[N]; var im = new double[N];
        Func<float[], int, int, double> at = (x, i, f) => { int j = f * H - N / 2 + i; return j >= 0 && j < x.Length ? x[j] : 0; };
        for (int f = 0; f < F; f++)
        {
            for (int i = 0; i < N; i++) { re[i] = (at(L, i, f) + at(R, i, f)) * 0.5 * win[i]; im[i] = 0; } Fft.Transform(re, im, false);
            for (int k = 0; k < B; k++) mag[f * B + k] = (float)Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
            if ((f & 63) == 0) { prog(0.2f * f / F); if (cancel()) throw new OperationCanceledException(); }
        }
        // medians: across time (harmonic) and across frequency (percussive)
        const int MW = 8; var hMed = new float[F * B]; var pMed = new float[F * B]; var buf = new float[2 * MW + 1];
        for (int k = 0; k < B; k++) { for (int f = 0; f < F; f++) { int c = 0; for (int d = -MW; d <= MW; d++) { int ff = f + d; if (ff >= 0 && ff < F) buf[c++] = mag[ff * B + k]; } hMed[f * B + k] = Median(buf, c); } if ((k & 63) == 0) { prog(0.2f + 0.2f * k / B); if (cancel()) throw new OperationCanceledException(); } }
        for (int f = 0; f < F; f++) { for (int k = 0; k < B; k++) { int c = 0; for (int d = -MW; d <= MW; d++) { int kk = k + d; if (kk >= 0 && kk < B) buf[c++] = mag[f * B + kk]; } pMed[f * B + k] = Median(buf, c); } if ((f & 63) == 0) { prog(0.4f + 0.2f * f / F); if (cancel()) throw new OperationCanceledException(); } }
        // the shares: pm (transient) into pMed, nm (texture) into hMed; the harmonic magnitude into mag
        double pw = 1 + 3 * sharp; const double gam = 0.6;
        for (int i = 0; i < F * B; i++)
        {
            double h = hMed[i] + 1e-9, p = pMed[i] + 1e-9, wh = Math.Pow(h, pw), wp = Math.Pow(p, pw), wn = Math.Pow(gam * Math.Sqrt(h * p), pw), s = wh + wp + wn;
            float pm = (float)(wp / s), nm = (float)(wn / s); pMed[i] = pm; hMed[i] = nm; mag[i] = mag[i] * (1 - pm - nm);
        }
        // the tail: frames where the harmonic energy is falling well below its recent peak
        var tau = new float[F]; double peak = 0, prev = 0, thr = 0.15 + 0.55 * tailSet, fall = Math.Exp(-1.0 / (0.8 * StudioEngine.SR / H));
        var en = new double[F]; for (int f = 0; f < F; f++) { double e = 0; for (int k = 0; k < B; k++) { double v = mag[f * B + k]; e += v * v; } en[f] = e; }   // energy, so the quiet noise floor across every bin does not hide the fade
        for (int f = 0; f < F; f++) { double e = en[f]; peak = Math.Max(e, peak * fall); double ratio = peak > 1e-9 ? Math.Sqrt(e / peak) : 1; bool falling = e <= prev * 1.02; prev = e * 0.3 + prev * 0.7; tau[f] = falling ? (float)Math.Max(0, Math.Min(1, (thr - ratio) / thr * 2 + 0.5 * (1 - ratio))) : 0; }
        for (int f = 1; f < F; f++) tau[f] = tau[f] * 0.5f + tau[f - 1] * 0.5f;
        // the movement: how far each harmonic bin strays from its own slow average (smoothing 60 to 400 ms)
        int sw = Math.Max(2, (int)((0.06 + 0.34 * (1 - moveSet)) * StudioEngine.SR / H)); var lg = new double[F + 1];   // in log terms, so a steady fade (a straight line) is not counted as movement
        var gate = new float[F]; for (int f = 0; f < F; f++) gate[f] = 1; double tiny = 1e-6 * en.Max();   // a new hit is a jump, not movement: the frames round each hit are left out
        for (int f = 2; f < F; f++) if (en[f] > 3 * en[f - 2] + tiny) for (int d = -sw / 2 - 1; d <= sw / 2 + 1; d++) { int ff = f + d; if (ff >= 0 && ff < F) gate[ff] = 0; }
        for (int k = 0; k < B; k++)
        {
            for (int f = 0; f < F; f++) lg[f + 1] = lg[f] + Math.Log(mag[f * B + k] + 1e-6);
            for (int f = 0; f < F; f++) { float h = mag[f * B + k]; int f0 = Math.Max(0, f - sw / 2), f1 = Math.Min(F, f + sw / 2 + 1); double avg = (lg[f1] - lg[f0]) / (f1 - f0); mag[f * B + k] = h > 1e-6f ? (float)Math.Min(1, Math.Abs(Math.Log(h + 1e-6) - avg) * (0.2 + 0.5 * moveSet)) * gate[f] : 0; }   // mag now holds the movement share of the harmonic part
            if ((k & 63) == 0) { prog(0.6f + 0.1f * k / B); if (cancel()) throw new OperationCanceledException(); }
        }
        // the repeat: the period of the texture's loudness (autocorrelation of its frame energy), 20 ms to 2 s
        var tx = new double[F]; double mean = 0; for (int f = 0; f < F; f++) { double e = 0; for (int k = 0; k < B; k += 2) e += hMed[f * B + k]; tx[f] = e; mean += e; } mean /= F; for (int f = 0; f < F; f++) tx[f] -= mean;
        double z = 0; for (int f = 0; f < F; f++) z += tx[f] * tx[f]; int bestLag = 0; double best = 0;
        for (int lag = 2; lag < Math.Min(F / 2, (int)(2.0 * StudioEngine.SR / H)); lag++) { double s = 0; for (int f = 0; f + lag < F; f++) s += tx[f] * tx[f + lag]; s /= Math.Max(1e-9, z); if (s > best) { best = s; bestLag = lag; } }
        repeat = best > 0.3 && bestLag > 0 ? "repeats about every " + Math.Round(bestLag * H * 1000.0 / StudioEngine.SR) + " ms (" + Math.Round(best * 100) + "% alike)" : "no clear repeat";
        // put each part back together (the same window for both, overlap-add, divided by the window overlap)
        outL = new float[5][]; outR = new float[5][]; for (int c = 0; c < 5; c++) { outL[c] = new float[n]; outR[c] = new float[n]; }
        var lr = new double[N]; var li = new double[N]; var rr = new double[N]; var ri = new double[N]; var tr = new double[N]; var ti = new double[N]; var m = new double[5]; const double norm = 1.0 / 1.5;
        for (int f = 0; f < F; f++)
        {
            for (int i = 0; i < N; i++) { lr[i] = at(L, i, f) * win[i]; rr[i] = at(R, i, f) * win[i]; li[i] = ri[i] = 0; } Fft.Transform(lr, li, false); Fft.Transform(rr, ri, false);
            for (int c = 0; c < 5; c++)
                for (int side = 0; side < 2; side++)
                {
                    var sre = side == 0 ? lr : rr; var sim = side == 0 ? li : ri;
                    for (int k = 0; k < B; k++)
                    {
                        int idx = f * B + k; double pm = pMed[idx], nm = hMed[idx], hm = Math.Max(0, 1 - pm - nm), mu = mag[idx], tt = tau[f];
                        double w = c == 0 ? pm : c == 1 ? hm * (1 - mu) * (1 - tt) : c == 2 ? hm * (1 - mu) * tt : c == 3 ? hm * mu : nm;
                        tr[k] = sre[k] * w; ti[k] = sim[k] * w; if (k > 0 && k < N / 2) { tr[N - k] = tr[k]; ti[N - k] = -ti[k]; }
                    }
                    Fft.Transform(tr, ti, true); var o = side == 0 ? outL[c] : outR[c];
                    for (int i = 0; i < N; i++) { int j = f * H - N / 2 + i; if (j >= 0 && j < n) o[j] += (float)(tr[i] * win[i] * norm); }
                }
            if ((f & 31) == 0) { prog(0.7f + 0.3f * f / F); if (cancel()) throw new OperationCanceledException(); }
        }
    }
    static float Median(float[] a, int n) { for (int i = 1; i < n; i++) { float v = a[i]; int j = i - 1; while (j >= 0 && a[j] > v) { a[j + 1] = a[j]; j--; } a[j + 1] = v; } return n == 0 ? 0 : a[n / 2]; }

    static readonly Color[] AutCols = { Color.FromArgb(255, 255, 90, 170), Color.FromArgb(255, 120, 220, 255), Color.FromArgb(255, 170, 120, 255), Color.FromArgb(255, 255, 200, 90), Color.FromArgb(255, 120, 255, 170) };
    void PaintAutopsy(Graphics g, RectangleF a)
    {
        var t = selTrack >= 0 && selTrack < eng.Tracks.Count ? eng.Tracks[selTrack] : null; AutLink lk; if (t != null && autLinks.TryGetValue(t, out lk)) t = lk.A.Src;   // a part's track shows its source
        Txt(g, "INSTRUMENT AUTOPSY" + (t != null ? "  ·  " + t.Name.ToUpperInvariant() : ""), 15, Ink, Rc(a.X + 6, a.Y + 4, a.Width - 12, 24), StringAlignment.Near, true);
        if (t == null || t.IsBus || t.Clip == null) { Txt(g, "Click an audio clip on the timeline to take its sound apart.", 12, DimC, Rc(a.X + 6, a.Y + 40, a.Width - 12, 18), StringAlignment.Near, false); return; }
        var au = AutFor(t);
        Txt(g, "A spectral guess, not a perfect separation: the five parts add back up to the sound. They stay linked to " + t.Name + " (the first 20 s): change it or the settings and they are made again.", 10.5f, DimC, Rc(a.X + 6, a.Y + 30, a.Width - 12, 18), StringAlignment.Near, false);
        // the three settings
        float sx = a.X + 6, sw = (a.Width - 12 - 40) / 3; string[] sn = { "TRANSIENT SHARPNESS", "TAIL POINT", "MOVEMENT" }; string[] sh = { "Higher: only the hardest hits count as the transient", "Higher: the tail starts sooner after each hit", "Higher: faster, smaller movement counts as modulation" };
        for (int i = 0; i < 3; i++)
        {
            var r = Rc(sx + i * (sw + 20), a.Y + 56, sw, 40); float v = i == 0 ? au.Sharp : i == 1 ? au.Tail : au.Move; int ii = i;
            Txt(g, sn[i] + "  " + Math.Round(v * 100) + "%", 10, Ink, Rc(r.X, r.Y, r.Width, 14), StringAlignment.Near, true);
            Slider(g, Rc(r.X, r.Y + 16, r.Width, 22), v, x => { x = (float)Math.Round(x * 20) / 20; if (ii == 0) au.Sharp = x; else if (ii == 1) au.Tail = x; else au.Move = x; }); Hint2(r, sh[i]);
        }
        float y = a.Y + 106;
        if (au.Running) { var pr = Rc(a.X + 6, y, a.Width - 12, 8); FillRR(g, TM.C(220, 20, 10, 40), pr, 4); FillRR(g, NeonCyan, Rc(pr.X, pr.Y, pr.Width * Math.Max(0.02f, au.Prog), pr.Height), 4); Txt(g, "Taking it apart... " + Math.Round(au.Prog * 100) + "%", 10, DimC, Rc(a.X + 6, y + 10, 300, 14), StringAlignment.Near, false); }
        else if (au.Error.Length > 0) Txt(g, au.Error, 11, NeonPink, Rc(a.X + 6, y, a.Width - 12, 16), StringAlignment.Near, true);
        y += 30;
        float rowH = Math.Max(54, Math.Min(86, (a.Bottom - y - 6) / 5f - 8));
        for (int c = 0; c < 5; c++)
        {
            var row = Rc(a.X + 6, y, a.Width - 12, rowH); y += rowH + 8; FillRR(g, TM.C(205, 18, 8, 40), row, 10); StrokeRR(g, Color.FromArgb(120, AutCols[c]), row, 10, 1.1f);
            Txt(g, AutNames[c], 12.5f, AutCols[c], Rc(row.X + 12, row.Y + 6, 200, 18), StringAlignment.Near, true);
            string sub = AutAbout[c] + (c == 4 && au.Repeat.Length > 0 ? " · " + au.Repeat : ""); TxtWrap(g, sub, 9.5f, DimC, Rc(row.X + 12, row.Y + 26, 200, row.Height - 28));
            int linked = autLinks.Count(k => k.Value.A == au && k.Value.Part == c && eng.Tracks.Contains(k.Key));
            var wave = Rc(row.X + 222, row.Y + 6, row.Width - 222 - 290, row.Height - 12);
            if (wave.Width > 40)
            {
                using (var b = new SolidBrush(TM.C(230, 6, 2, 16))) g.FillRectangle(b, wave);
                if (au.Peaks != null) { var pk = au.Peaks[c]; float mx = 0.0001f; foreach (var v in pk) mx = Math.Max(mx, v); float cy = wave.Y + wave.Height / 2, bw = wave.Width / pk.Length;
                    using (var b = new SolidBrush(AutCols[c])) for (int i = 0; i < pk.Length; i++) { float hh = Math.Max(1, pk[i] / mx * (wave.Height / 2 - 2)); g.FillRectangle(b, wave.X + i * bw, cy - hh, Math.Max(1, bw - 0.4f), hh * 2); } }
                else Txt(g, au.Running ? "..." : "", 10, DimC, wave, StringAlignment.Center, false);
            }
            int cc = c; bool ready = au.L != null && !au.Running;
            var hb = Rc(row.Right - 280, row.Y + (row.Height - 30) / 2, 130, 30); Btn(g, hb, autPlaying == AutNames[c] && Auditioner.IsPlaying ? "PLAYING" : "HEAR IT", ready ? 1 : 0, () => { if (au.L != null) AutHear(au, cc); }, 10); Hint2(hb, "Hear the first 8 seconds of the " + AutNames[c].ToLowerInvariant() + " on its own");
            var ub = Rc(row.Right - 142, row.Y + (row.Height - 30) / 2, 132, 30); Btn(g, ub, linked > 0 ? "USE IT (" + linked + ")" : "USE IT", ready ? 2 : 0, () => { if (au.L != null) AutUse(au, cc); }, 10); Hint2(ub, "Put the " + AutNames[c].ToLowerInvariant() + " on a new track, linked to " + t.Name);
        }
    }
}
