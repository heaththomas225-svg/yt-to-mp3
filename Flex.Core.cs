using System;
using System.Collections.Generic;

// ======================================================================
//  FLEX PITCH engine: finds every note in a (monophonic) recording, and re-tunes the audio from an edited list of notes.
//  Analysis: YIN pitch tracking every 256 samples -> a pitch curve -> notes.
//  Rendering: a pitch-synchronous overlap-add (PSOLA) resynthesis driven by a per-frame pitch-ratio curve built from each note's edits.
// ======================================================================

class FlexNote
{
    public int Start, End;             // analysis frames: [Start, End)
    public double OrigMidi;            // the note's analysed pitch (a fractional MIDI note number)
    public double Shift;               // semitones added to the whole note
    public double Fine;                // cents added on top
    public double Drift = 0;           // 0..100 %: how much of the slow pitch wandering is removed
    public double Vib = 100;           // 0..200 %: size of the fast pitch movement (vibrato)
    public double GainDb;              // volume of the note
    public double Formant;             // semitones
    public bool Deleted;               // not processed any more (its audio is left as it was)
    public int Move;                   // analysis frames the note was moved in time (+ later, - earlier); the audio around it stretches to fit
    public double Target { get { return OrigMidi + Shift + Fine / 100.0; } }
    public bool Edited { get { return Math.Abs(Shift) > 1e-6 || Math.Abs(Fine) > 1e-6 || Drift > 0.01 || Math.Abs(Vib - 100) > 0.01 || Math.Abs(GainDb) > 1e-6 || Math.Abs(Formant) > 1e-6 || Move != 0; } }
    public FlexNote Clone() { return (FlexNote)MemberwiseClone(); }
    public void ResetEdits() { Shift = 0; Fine = 0; Drift = 0; Vib = 100; GainDb = 0; Formant = 0; Move = 0; }
    public static string Name(double midi) { string[] n = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" }; int m = (int)Math.Round(midi); return n[((m % 12) + 12) % 12] + (m / 12 - 1); }
}

class FlexAnalysis
{
    public int Hop = 256, SR = 44100, Frames;
    public float[] F0;        // Hz per frame, 0 = not pitched
    public float[] Midi;      // fractional MIDI per frame (0 where unpitched)
    public float[] Level;     // frame level in dB
    public List<FlexNote> Notes = new List<FlexNote>();
}

static class FlexPitch
{
    public static double Hz(double midi) { return 440.0 * Math.Pow(2, (midi - 69) / 12.0); }
    public static double ToMidi(double hz) { return 69 + 12 * Math.Log(hz / 440.0) / Math.Log(2); }

    // ---------- analysis ----------
    public static FlexAnalysis Analyse(float[] l, float[] r, int length, int sr, Action<float> progress, Func<bool> cancelled)
    {
        var a = new FlexAnalysis { SR = sr }; int hop = a.Hop; int frames = Math.Max(1, length / hop); a.Frames = frames; a.F0 = new float[frames]; a.Midi = new float[frames]; a.Level = new float[frames];
        // mono, then 2:1 decimated (22 kHz is plenty for pitch and four times cheaper)
        int dn = length / 2; var d = new float[dn]; for (int i = 0; i < dn; i++) d[i] = ((l[2 * i] + (r != null ? r[2 * i] : l[2 * i])) + (l[2 * i + 1] + (r != null ? r[2 * i + 1] : l[2 * i + 1]))) * 0.25f;
        double sr2 = sr / 2.0; int tauMin = Math.Max(2, (int)(sr2 / 1100)), tauMax = Math.Min(560, (int)(sr2 / 65)); int W = 512; var dd = new double[tauMax + 2]; var raw = new double[tauMax + 2];
        for (int f = 0; f < frames; f++)
        {
            if (cancelled != null && (f & 255) == 0 && cancelled()) return null; if (progress != null && (f & 127) == 0) progress(f / (float)frames);
            int centre = (f * hop + hop / 2) / 2; int s0 = centre - W / 2; if (s0 < 0) s0 = 0; if (s0 + W + tauMax + 2 >= dn) { if (dn - W - tauMax - 3 < 0) continue; s0 = dn - W - tauMax - 3; }
            double e = 0; for (int i = 0; i < W; i++) e += d[s0 + i] * d[s0 + i]; double rms = Math.Sqrt(e / W); a.Level[f] = (float)(20 * Math.Log10(rms + 1e-9)); if (rms < 0.003) continue;
            double run = 0; dd[0] = 1;
            for (int tau = 1; tau <= tauMax + 1; tau++) { double sum = 0; for (int j = 0; j < W; j++) { double df = d[s0 + j] - d[s0 + j + tau]; sum += df * df; } run += sum; dd[tau] = run > 0 ? sum * tau / run : 1; }
            int best = -1; for (int tau = tauMin; tau <= tauMax; tau++) { if (dd[tau] < 0.15) { while (tau + 1 <= tauMax && dd[tau + 1] < dd[tau]) tau++; best = tau; break; } }
            if (best < 0) { double m = 1e9; int bi = -1; for (int tau = tauMin; tau <= tauMax; tau++) if (dd[tau] < m) { m = dd[tau]; bi = tau; } if (bi < 0 || m > 0.3) continue; best = bi; }
            double t = best; if (best > 1 && best < tauMax) { double p = dd[best - 1], q = dd[best], s = dd[best + 1], den = p - 2 * q + s; if (Math.Abs(den) > 1e-12) t = best + 0.5 * (p - s) / den; }
            double hz = sr2 / t; if (hz < 60 || hz > 1200) continue; a.F0[f] = (float)hz; a.Midi[f] = (float)ToMidi(hz);
        }
        Clean(a); Segment(a); if (progress != null) progress(1f); return a;
    }

    // remove single-frame octave jumps (median of 5) and bridge tiny gaps
    static void Clean(FlexAnalysis a)
    {
        int n = a.Frames; var m = (float[])a.Midi.Clone();
        for (int i = 2; i < n - 2; i++)
        {
            if (a.Midi[i] <= 0) continue; var w = new List<float>(5); for (int k = -2; k <= 2; k++) if (a.Midi[i + k] > 0) w.Add(a.Midi[i + k]); if (w.Count < 3) { m[i] = 0; continue; } w.Sort(); float med = w[w.Count / 2];
            if (Math.Abs(a.Midi[i] - med) > 2.5f) m[i] = med; else m[i] = a.Midi[i];
        }
        for (int i = 1; i < n - 1; i++) { if (m[i] <= 0) { int j = i; while (j < n && m[j] <= 0) j++; if (j - i <= 3 && i > 0 && j < n && m[i - 1] > 0 && m[j] > 0 && Math.Abs(m[i - 1] - m[j]) < 1.2f) for (int k = i; k < j; k++) m[k] = m[i - 1] + (m[j] - m[i - 1]) * (k - i + 1) / (j - i + 1); i = j; } }
        a.Midi = m; for (int i = 0; i < n; i++) a.F0[i] = m[i] > 0 ? (float)Hz(m[i]) : 0;
    }

    static double Median(List<double> v) { if (v.Count == 0) return 0; v.Sort(); return v[v.Count / 2]; }

    // pitched runs become notes: a new note starts where the pitch settles on a different note, or the level dips and returns (a new syllable)
    static void Segment(FlexAnalysis a)
    {
        int n = a.Frames, i = 0; var notes = a.Notes; notes.Clear();
        while (i < n)
        {
            if (a.Midi[i] <= 0) { i++; continue; }
            int j = i; while (j < n && a.Midi[j] > 0) j++;   // [i, j) is one pitched run
            int s = i; var cur = new List<double>(); int off = 0; int lastEnd = i;
            for (int k = i; k < j; k++)
            {
                double mid = a.Midi[k]; double med = cur.Count >= 6 ? Median(new List<double>(cur.GetRange(Math.Max(0, cur.Count - 24), Math.Min(24, cur.Count)))) : (cur.Count > 0 ? Median(new List<double>(cur)) : mid);
                bool far = cur.Count >= 6 && Math.Abs(mid - med) > 0.75; if (far) off++; else off = 0;
                bool dip = k - s > 12 && k + 3 < j && a.Level[k] < a.Level[k - 1] - 9 && a.Level[k + 2] > a.Level[k] + 8;
                if (off >= 5 || dip)
                {
                    int cut = dip ? k : k - off + 1; if (cut - s >= 8) Add(a, s, cut); else if (notes.Count > 0 && s == notes[notes.Count - 1].End) notes[notes.Count - 1].End = cut;
                    s = cut; cur.Clear(); off = 0; for (int q = cut; q <= k; q++) cur.Add(a.Midi[q]); continue;
                }
                cur.Add(mid);
            }
            if (j - s >= 8) Add(a, s, j); else if (notes.Count > 0 && s == notes[notes.Count - 1].End) notes[notes.Count - 1].End = j;
            i = j;
        }
    }
    static void Add(FlexAnalysis a, int s, int e)
    {
        var v = new List<double>(); for (int k = s; k < e; k++) if (a.Midi[k] > 0) v.Add(a.Midi[k]); if (v.Count < 4) return;
        a.Notes.Add(new FlexNote { Start = s, End = e, OrigMidi = Median(v) });
    }

    public static FlexNote Split(FlexAnalysis a, FlexNote n, int frame)
    {
        if (frame - n.Start < 6 || n.End - frame < 6) return null;
        var v1 = new List<double>(); var v2 = new List<double>(); for (int k = n.Start; k < n.End; k++) if (a.Midi[k] > 0) (k < frame ? v1 : v2).Add(a.Midi[k]);
        if (v1.Count < 3 || v2.Count < 3) return null;
        var second = n.Clone(); second.Start = frame; second.OrigMidi = Median(v2); n.End = frame; n.OrigMidi = Median(v1); a.Notes.Insert(a.Notes.IndexOf(n) + 1, second); return second;
    }
    public static bool Join(FlexAnalysis a, FlexNote n)
    {
        int i = a.Notes.IndexOf(n); if (i < 0 || i + 1 >= a.Notes.Count) return false; var nx = a.Notes[i + 1]; if (nx.Start - n.End > 12) return false;
        var v = new List<double>(); for (int k = n.Start; k < nx.End; k++) if (a.Midi[k] > 0) v.Add(a.Midi[k]); n.End = nx.End; n.OrigMidi = Median(v); a.Notes.RemoveAt(i + 1); return true;
    }

    // ---------- the pitch-ratio curve ----------
    // per frame: how much to scale the original pitch (1 = leave it alone)
    public static float[] RatioCurve(FlexAnalysis a, float[] gainDb, float[] formant)
    {
        int n = a.Frames; var ratio = new float[n]; for (int i = 0; i < n; i++) ratio[i] = 1f; if (gainDb != null) Array.Clear(gainDb, 0, n); if (formant != null) Array.Clear(formant, 0, n);
        foreach (var note in a.Notes)
        {
            if (note.Deleted) continue; int s = Math.Max(0, note.Start), e = Math.Min(n, note.End); int len = e - s; if (len <= 0) continue;
            if (gainDb != null) for (int i = s; i < e; i++) gainDb[i] = (float)note.GainDb; if (formant != null) for (int i = s; i < e; i++) formant[i] = (float)note.Formant;
            bool pitchEdit = Math.Abs(note.Shift) > 1e-6 || Math.Abs(note.Fine) > 1e-6 || note.Drift > 0.01 || Math.Abs(note.Vib - 100) > 0.01; if (!pitchEdit) continue;
            // slow part = moving average of the pitch over about a quarter of a second; fast part (vibrato) = what is left
            int win = 21; var mid = new double[len]; for (int i = 0; i < len; i++) { double m = a.Midi[s + i]; mid[i] = m > 0 ? m : note.OrigMidi; }
            var slow = new double[len]; double acc = 0; int cnt = 0; int lo = 0, hi = -1;
            for (int i = 0; i < len; i++) { int want = Math.Min(len - 1, i + win / 2); while (hi < want) { hi++; acc += mid[hi]; cnt++; } int wl = Math.Max(0, i - win / 2); while (lo < wl) { acc -= mid[lo]; cnt--; lo++; } slow[i] = acc / Math.Max(1, cnt); }
            double drift = note.Drift / 100.0, vib = note.Vib / 100.0, add = note.Shift + note.Fine / 100.0;
            for (int i = 0; i < len; i++)
            {
                double slow2 = slow[i] + (note.OrigMidi - slow[i]) * drift, fast = (mid[i] - slow[i]) * vib; double target = slow2 + fast + add; ratio[s + i] = (float)Math.Pow(2, (target - mid[i]) / 12.0);
            }
        }
        // smooth the ratio so notes join without a click or a pitch jump (about 25 ms)
        var o = new float[n]; int w2 = 2; for (int i = 0; i < n; i++) { double sum = 0; int c = 0; for (int k = -w2; k <= w2; k++) { int j = i + k; if (j >= 0 && j < n) { sum += Math.Log(ratio[j]); c++; } } o[i] = (float)Math.Exp(sum / c); }
        return o;
    }

    // ---------- resynthesis ----------
    // Re-tunes the audio. 'l' and 'r' are the ORIGINAL samples; returns new arrays of the same length.
    public static void Render(float[] l, float[] r, int length, FlexAnalysis a, out float[] outL, out float[] outR)
    {
        outL = new float[length]; outR = new float[length]; Array.Copy(l, outL, length); Array.Copy(r ?? l, outR, length);
        int n = a.Frames, hop = a.Hop; var gain = new float[n]; var form = new float[n]; var ratio = RatioCurve(a, gain, form);
        // pitch regions to process: runs of frames whose ratio is not 1, joined across small gaps, with a margin
        var regions = new List<int[]>(); int i = 0;
        while (i < n) { if (Math.Abs(ratio[i] - 1) < 5e-4) { i++; continue; } int j = i; int gap = 0; int lastBad = i; while (j < n && gap <= 6) { if (Math.Abs(ratio[j] - 1) >= 5e-4) { lastBad = j; gap = 0; } else gap++; j++; } regions.Add(new[] { i, lastBad + 1 }); i = lastBad + 1; }
        foreach (var rg in regions)
        {
            int sA = Math.Max(0, rg[0] * hop - 2400), eA = Math.Min(length, rg[1] * hop + 2400); if (eA - sA < 600) continue;
            float[] pl = Psola(l, sA, eA, a, ratio), pr = r == null ? pl : Psola(r, sA, eA, a, ratio, pl != null);
            if (pl == null) continue; int fade = Math.Min(960, (eA - sA) / 4);
            for (int k = 0; k < eA - sA; k++)
            {
                double w = k < fade ? 0.5 - 0.5 * Math.Cos(Math.PI * k / fade) : (k > eA - sA - fade ? 0.5 - 0.5 * Math.Cos(Math.PI * (eA - sA - k) / fade) : 1.0); int idx = sA + k;
                outL[idx] = (float)(outL[idx] * (1 - w) + pl[k] * w); outR[idx] = (float)(outR[idx] * (1 - w) + (pr != null ? pr[k] : pl[k]) * w);
            }
        }
        // formant changes, note by note
        foreach (var note in a.Notes)
        {
            if (note.Deleted || Math.Abs(note.Formant) < 1e-6) continue; int sA = Math.Max(0, note.Start * hop - 3000), eA = Math.Min(length, note.End * hop + 3000); if (eA - sA < 4096) continue;
            var sl = new float[eA - sA]; var sr2 = new float[eA - sA]; Array.Copy(outL, sA, sl, 0, sl.Length); Array.Copy(outR, sA, sr2, 0, sl.Length); FormantShift(sl, sr2, (float)note.Formant, a.SR);
            int ns = note.Start * hop - sA, ne = note.End * hop - sA, fade = 1400; for (int k = Math.Max(0, ns - fade); k < Math.Min(sl.Length, ne + fade); k++) { double w = k < ns ? (k - (ns - fade)) / (double)fade : (k > ne ? 1 - (k - ne) / (double)fade : 1.0); w = Math.Max(0, Math.Min(1, w)); w = 0.5 - 0.5 * Math.Cos(Math.PI * w); outL[sA + k] = (float)(outL[sA + k] * (1 - w) + sl[k] * w); outR[sA + k] = (float)(outR[sA + k] * (1 - w) + sr2[k] * w); }
        }
        // gain, smoothed so it never clicks
        bool any = false; for (int f = 0; f < n; f++) if (Math.Abs(gain[f]) > 1e-6) { any = true; break; }
        if (any)
        {
            double g = 1; for (int s = 0; s < length; s++)
            {
                int f = Math.Min(n - 1, s / hop); double tg = Math.Pow(10, gain[f] / 20.0); g += (tg - g) * 0.004; if (Math.Abs(g - 1) > 1e-5) { outL[s] = (float)(outL[s] * g); outR[s] = (float)(outR[s] * g); }
            }
        }
        FlexTime.Apply(ref outL, ref outR, length, a);   // notes moved in time, last
    }

    static void FormantShift(float[] l, float[] r, float semis, int sr)
    {
        var fx = new FormantFx(); fx.Prepare(sr); fx.SetPreset("shift=" + semis.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ";mix=100"); int lat = fx.Latency; int n = l.Length; var pl = new float[n + lat]; var pr = new float[n + lat]; Array.Copy(l, pl, n); Array.Copy(r, pr, n);
        for (int p = 0; p < pl.Length; p += 512) { int m = Math.Min(512, pl.Length - p); var bl = new float[512]; var br = new float[512]; Array.Copy(pl, p, bl, 0, m); Array.Copy(pr, p, br, 0, m); fx.Process(bl, br, 512); Array.Copy(bl, 0, pl, p, m); Array.Copy(br, 0, pr, p, m); }
        Array.Copy(pl, lat, l, 0, n); Array.Copy(pr, lat, r, 0, n);
    }

    static double PeriodAt(FlexAnalysis a, double sample, ref double lastT)
    {
        int f = (int)(sample / a.Hop); if (f < 0) f = 0; if (f >= a.Frames) f = a.Frames - 1; float hz = a.F0[f]; if (hz > 0) { lastT = a.SR / hz; return lastT; } return lastT > 0 ? lastT : a.SR * 0.0100;
    }
    static double RatioAt(float[] ratio, FlexAnalysis a, double sample) { int f = (int)(sample / a.Hop); if (f < 0) f = 0; if (f >= ratio.Length) f = ratio.Length - 1; return ratio[f]; }

    // pitch-synchronous overlap-add of x[s0..e0): analysis marks follow the pitch curve; synthesis marks are spaced period/ratio apart
    static float[] Psola(float[] x, int s0, int e0, FlexAnalysis a, float[] ratio, bool dummy = true)
    {
        int len = e0 - s0; var outp = new double[len + 4096]; var wsum = new double[len + 4096];
        var marks = new List<double>(); double lastT = a.SR * 0.0100; double m = s0; marks.Add(m); while (m < e0 + 2000) { m += PeriodAt(a, m, ref lastT); marks.Add(m); }
        double ts = s0; int mp = 0; double lt2 = lastT;
        while (ts < e0)
        {
            double tin = ts; while (mp + 1 < marks.Count && Math.Abs(marks[mp + 1] - tin) <= Math.Abs(marks[mp] - tin)) mp++; while (mp > 0 && Math.Abs(marks[mp - 1] - tin) < Math.Abs(marks[mp] - tin)) mp--;
            double mk = marks[mp]; double T = PeriodAt(a, mk, ref lt2); int Tg = (int)Math.Max(24, Math.Min(1400, Math.Round(T))); double rt = RatioAt(ratio, a, tin); if (rt < 0.2) rt = 0.2; if (rt > 5) rt = 5;
            int tsi = (int)Math.Round(ts), mi = (int)Math.Round(mk);
            for (int c = -Tg; c < Tg; c++)
            {
                int io = tsi - s0 + c, ii = mi + c; if (io < 0 || io >= len + 4096 - 1 || ii < 0 || ii >= x.Length) continue; double w = 0.5 * (1 + Math.Cos(Math.PI * c / Tg));
                outp[io] += x[ii] * w; wsum[io] += w;
            }
            ts += T / rt;
        }
        var res = new float[len]; for (int k = 0; k < len; k++) res[k] = wsum[k] > 0.08 ? (float)(outp[k] / wsum[k]) : x[s0 + k]; return res;
    }

    // snap a pitch (fractional MIDI) to the nearest allowed note of a scale; mask bit n = pitch class n allowed
    public static double Snap(double midi, int mask)
    {
        int c = (int)Math.Round(midi); double best = c, bd = 1e9; for (int n = c - 6; n <= c + 6; n++) { int pc = ((n % 12) + 12) % 12; if ((mask & (1 << pc)) == 0) continue; double d = Math.Abs(n - midi); if (d < bd) { bd = d; best = n; } }
        return best;
    }
}

// ---------- moving notes in time ----------
// Each note keeps its length and sound; the audio between the notes is stretched or squeezed to fill (WSOLA: pieces of the original are laid
// end to end where they line up best, so nothing is pitched or smeared). Where nothing moved, the audio comes back as it was.
static class FlexTime
{
    public static bool AnyMoved(FlexAnalysis a) { foreach (var n in a.Notes) if (!n.Deleted && n.Move != 0) return true; return false; }
    // the time map as points (output sample, input sample): every note is an anchor at its new place
    public static List<double[]> Anchors(FlexAnalysis a, int length)
    {
        var notes = new List<FlexNote>(a.Notes); notes.Sort((p, q) => p.Start.CompareTo(q.Start)); var pts = new List<double[]> { new double[] { 0, 0 } };
        foreach (var n in notes)
        {
            int mv = n.Deleted ? 0 : n.Move; double s = (double)n.Start * a.Hop, e = (double)n.End * a.Hop, d = (double)mv * a.Hop;
            foreach (var p in new[] { new[] { s + d, s }, new[] { e + d, e } }) { var last = pts[pts.Count - 1]; if (p[0] > last[0] && p[1] > last[1] && p[0] < length && p[1] < length) pts.Add(p); }
        }
        pts.Add(new double[] { length, length }); return pts;
    }
    public static double Map(List<double[]> an, double dst)
    {
        int lo = 0, hi = an.Count - 1; if (dst <= an[0][0]) return an[0][1] + (dst - an[0][0]); if (dst >= an[hi][0]) return an[hi][1] + (dst - an[hi][0]);
        while (hi - lo > 1) { int m = (lo + hi) / 2; if (an[m][0] <= dst) lo = m; else hi = m; }
        var a = an[lo]; var b = an[hi]; return a[1] + (dst - a[0]) * (b[1] - a[1]) / (b[0] - a[0]);
    }
    const int N = 1024, H = 512, S = 384;
    public static void Apply(ref float[] L, ref float[] R, int length, FlexAnalysis a)
    {
        if (!AnyMoved(a) || length < N * 2) return; var an = Anchors(a, length);
        var win = new float[N]; for (int i = 0; i < N; i++) win[i] = (float)(0.5 - 0.5 * Math.Cos(2 * Math.PI * i / N));
        var oL = new float[length]; var oR = new float[length]; var norm = new float[length]; var mono = new float[length]; for (int i = 0; i < length; i++) mono[i] = (L[i] + R[i]) * 0.5f;
        int prev = int.MinValue;
        for (int outPos = -H; outPos < length; outPos += H)
        {
            int nominal = (int)Math.Round(Map(an, outPos + N / 2) - N / 2), pick;
            if (prev == int.MinValue) pick = nominal;
            else { int natural = prev + H; pick = Math.Abs(natural - nominal) <= 1 ? natural : Search(mono, length, natural, nominal); }   // where the map runs straight, the next piece simply follows on
            for (int i = 0; i < N; i++)
            {
                int o = outPos + i; if (o < 0) continue; if (o >= length) break; int s = pick + i; float w = win[i];
                if (s >= 0 && s < length) { oL[o] += L[s] * w; oR[o] += R[s] * w; } norm[o] += w;
            }
            prev = pick;
        }
        for (int i = 0; i < length; i++) { if (norm[i] > 1e-3) { oL[i] /= norm[i]; oR[i] /= norm[i]; } else { int s = (int)Math.Round(Map(an, i)); if (s >= 0 && s < length) { oL[i] = L[s]; oR[i] = R[s]; } } }
        L = oL; R = oR;
    }
    // the start near "nominal" whose sound best continues the piece that would naturally follow the last one (coarse search, then fine)
    static int Search(float[] x, int length, int natural, int nominal)
    {
        int best = nominal; double bs = double.MinValue;
        Func<int, double> score = c =>
        {
            double dot = 0, e1 = 0, e2 = 0; for (int i = 0; i < N / 2; i += 4) { int p = natural + i, q = c + i; float u = p >= 0 && p < length ? x[p] : 0, v = q >= 0 && q < length ? x[q] : 0; dot += u * v; e1 += u * u; e2 += v * v; }
            return e1 < 1e-12 || e2 < 1e-12 ? (c == nominal ? 0 : -1) : dot / Math.Sqrt(e1 * e2) - Math.Abs(c - nominal) * 1e-6;   // silence: stay on the map
        };
        for (int k = 0; k <= S; k += 8) foreach (int c in k == 0 ? new[] { nominal } : new[] { nominal - k, nominal + k }) { double sc = score(c); if (sc > bs + 1e-9) { bs = sc; best = c; } }
        int coarse = best; for (int k = 1; k < 8; k++) foreach (int c in new[] { coarse - k, coarse + k }) { double sc = score(c); if (sc > bs + 1e-9) { bs = sc; best = c; } }
        return best;
    }
}
