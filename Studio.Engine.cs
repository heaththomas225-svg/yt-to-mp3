using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

// ======================================================================
//  Studio audio engine: clips, tracks, mixer with plugin-delay compensation,
//  loudness / true-peak meters, real-time playback (winmm) and offline render.
// ======================================================================

static class StudioEnv
{
    public static Func<string> Ffmpeg = delegate { return null; };
    public static Func<string> Ffprobe = delegate { return null; };
    public static string DataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YtToMp3");
}

// ---------------- audio clip (always 44.1 kHz stereo float in memory) ----------------
class AudioClip
{
    public float[] L, R; public string Path = "", Name = "";
    public int Length { get { return L == null ? 0 : L.Length; } }
    const int Bin = 256; float[] pmin, pmax;

    public static AudioClip FromFile(string path)
    {
        string ff = StudioEnv.Ffmpeg();
        if (ff == null) throw new Exception("ffmpeg isn't installed yet. Convert something once in the main window first (it installs ffmpeg).");
        var psi = new ProcessStartInfo(ff, "-v error -i \"" + path + "\" -vn -ac 2 -ar 44100 -f f32le -")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        var ms = new MemoryStream();
        string err = "";
        using (var p = Process.Start(psi))
        {
            var et = new Thread(() => { try { err = p.StandardError.ReadToEnd(); } catch { } }) { IsBackground = true };
            et.Start();
            p.StandardOutput.BaseStream.CopyTo(ms);
            p.WaitForExit(); et.Join(2000);
            if (p.ExitCode != 0 || ms.Length < 8) throw new Exception("Couldn't read that audio file." + (err.Length > 0 ? "\n" + err.Trim() : ""));
        }
        byte[] b = ms.GetBuffer(); int frames = (int)(ms.Length / 8);
        var c = new AudioClip { L = new float[frames], R = new float[frames], Path = path, Name = System.IO.Path.GetFileNameWithoutExtension(path) };
        for (int i = 0; i < frames; i++) { c.L[i] = BitConverter.ToSingle(b, i * 8); c.R[i] = BitConverter.ToSingle(b, i * 8 + 4); }
        c.BuildPeaks();
        return c;
    }

    public static AudioClip FromArrays(float[] l, float[] r, string name)
    {
        var c = new AudioClip { L = l, R = r, Name = name }; c.BuildPeaks(); return c;
    }

    public void BuildPeaks()
    {
        int bins = (Length + Bin - 1) / Bin; pmin = new float[bins]; pmax = new float[bins];
        for (int b = 0; b < bins; b++)
        {
            float mn = 1e9f, mx = -1e9f; int s = b * Bin, e = Math.Min(Length, s + Bin);
            for (int i = s; i < e; i++)
            {
                float a = L[i], c = R[i];
                if (a < mn) mn = a; if (c < mn) mn = c; if (a > mx) mx = a; if (c > mx) mx = c;
            }
            pmin[b] = mn; pmax[b] = mx;
        }
    }

    // min / max per pixel for waveform drawing. start/spp in frames (start may be negative).
    public void GetPeaks(long start, double spp, int pixels, float[] mn, float[] mx)
    {
        for (int p = 0; p < pixels; p++)
        {
            long a = start + (long)(p * spp), b = start + (long)((p + 1) * spp); if (b <= a) b = a + 1;
            if (b <= 0 || a >= Length) { mn[p] = 0; mx[p] = 0; continue; }
            if (a < 0) a = 0; if (b > Length) b = Length;
            float lo = 1e9f, hi = -1e9f;
            if (b - a >= Bin * 2)
            {
                int ba = (int)(a / Bin), bb = (int)((b + Bin - 1) / Bin);
                for (int k = ba; k < bb && k < pmin.Length; k++) { if (pmin[k] < lo) lo = pmin[k]; if (pmax[k] > hi) hi = pmax[k]; }
            }
            else
                for (long i = a; i < b; i++)
                {
                    float x = L[i], y = R[i];
                    if (x < lo) lo = x; if (y < lo) lo = y; if (x > hi) hi = x; if (y > hi) hi = y;
                }
            mn[p] = lo > hi ? 0 : lo; mx[p] = lo > hi ? 0 : hi;
        }
    }
}

// ---------------- WAV writer (16 / 24 / 32-bit float) ----------------
static class WavIO
{
    public static void Write(string path, float[] l, float[] r, int sr, int bits)
    {
        int n = l.Length, bytes = bits / 8, dataLen = n * 2 * bytes;
        using (var f = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (var w = new BinaryWriter(f))
        {
            w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + dataLen); w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16); w.Write((short)(bits == 32 ? 3 : 1)); w.Write((short)2); w.Write(sr); w.Write(sr * 2 * bytes); w.Write((short)(2 * bytes)); w.Write((short)bits);
            w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(dataLen);
            var rnd = new Random(1); var buf = new byte[4096 * 2 * bytes]; int bi = 0;
            for (int i = 0; i < n; i++)
            {
                for (int c = 0; c < 2; c++)
                {
                    float x = c == 0 ? l[i] : r[i];
                    if (bits == 32) { var bb = BitConverter.GetBytes(x); Buffer.BlockCopy(bb, 0, buf, bi, 4); bi += 4; }
                    else if (bits == 24)
                    {
                        double d = (rnd.NextDouble() - rnd.NextDouble()) / 8388608.0;   // TPDF dither (~1 LSB)
                        int v = (int)Math.Round(Math.Max(-1, Math.Min(0.9999999, x + d)) * 8388607.0);
                        buf[bi++] = (byte)v; buf[bi++] = (byte)(v >> 8); buf[bi++] = (byte)(v >> 16);
                    }
                    else
                    {
                        double d = (rnd.NextDouble() - rnd.NextDouble()) / 32768.0;
                        int v = (int)Math.Round(Math.Max(-1, Math.Min(0.99997, x + d)) * 32767.0);
                        buf[bi++] = (byte)v; buf[bi++] = (byte)(v >> 8);
                    }
                }
                if (bi >= buf.Length - 8) { w.Write(buf, 0, bi); bi = 0; }
            }
            if (bi > 0) w.Write(buf, 0, bi);
        }
    }
}

// ---------------- loudness (ITU-R BS.1770 / EBU R128) ----------------
class KWeight
{
    Biquad pre = new Biquad(), hp = new Biquad();
    // K-weighting designed for any sample rate exactly as in libebur128 (matches the BS.1770 48 kHz coefficients).
    public KWeight(int sr)
    {
        double f0 = 1681.974450955533, G = 3.999843853973347, Q = 0.7071752369554196;
        double K = Math.Tan(Math.PI * f0 / sr), Vh = Math.Pow(10.0, G / 20.0), Vb = Math.Pow(Vh, 0.4996667741545416);
        double a0 = 1.0 + K / Q + K * K;
        pre.SetRaw((Vh + Vb * K / Q + K * K) / a0, 2.0 * (K * K - Vh) / a0, (Vh - Vb * K / Q + K * K) / a0,
                   2.0 * (K * K - 1.0) / a0, (1.0 - K / Q + K * K) / a0);
        f0 = 38.13547087613982; Q = 0.5003270373253953; K = Math.Tan(Math.PI * f0 / sr);
        hp.SetRaw(1.0, -2.0, 1.0, 2.0 * (K * K - 1.0) / (1.0 + K / Q + K * K), (1.0 - K / Q + K * K) / (1.0 + K / Q + K * K));
    }
    public void Reset() { pre.Reset(); hp.Reset(); }
    public void Tick(float l, float r, out float yl, out float yr)
    {
        yl = hp.TickL(pre.TickL(l)); yr = hp.TickR(pre.TickR(r));
    }
}

class LoudnessResult
{
    public double I = double.NegativeInfinity, LRA, TruePeakDb = double.NegativeInfinity, PeakDb = double.NegativeInfinity, MaxShortTerm = double.NegativeInfinity;
    public int Clips;
}

static class TruePeak
{
    static float[][] ph;
    static TruePeak()
    {
        ph = new float[3][];
        for (int p = 1; p <= 3; p++)
        {
            ph[p - 1] = new float[12];
            for (int k = 0; k < 12; k++)
            {
                double x = (k - 5) - p / 4.0, s = Math.Abs(x) < 1e-9 ? 1 : Math.Sin(Math.PI * x) / (Math.PI * x), w = 0.5 * (1 + Math.Cos(Math.PI * x / 6.5));
                ph[p - 1][k] = (float)(s * w);
            }
        }
    }
    // peak among the 4x interpolated points between the newest sample (index 11) and the 11 before it
    public static float Oversampled(float[] hist12)
    {
        float pk = Math.Abs(hist12[11]);
        for (int p = 0; p < 3; p++)
        {
            float s = 0; var f = ph[p]; for (int k = 0; k < 12; k++) s += hist12[k] * f[k];
            if (Math.Abs(s) > pk) pk = Math.Abs(s);
        }
        return pk;
    }
}

class LufsMeter
{
    int sr, sub, subCnt; double subSum; KWeight k; List<double> sp = new List<double>(), blocks = new List<double>();
    float[] hl = new float[12], hr = new float[12];
    public double MaxTruePeak = 0; public volatile float Momentary = -120, ShortTerm = -120, Integrated = -120, LraV; public int Clips; public volatile float PeakNow;
    public LufsMeter(int sr) { this.sr = sr; sub = sr / 10; k = new KWeight(sr); }
    public void Reset() { k.Reset(); sp.Clear(); blocks.Clear(); subCnt = 0; subSum = 0; MaxTruePeak = 0; Momentary = ShortTerm = Integrated = -120; Clips = 0; Array.Clear(hl, 0, 12); Array.Clear(hr, 0, 12); }
    static double Lufs(double z) { return z <= 1e-12 ? -120 : -0.691 + 10 * Math.Log10(z); }

    public void Process(float[] l, float[] r, int n)
    {
        float pk = 0;
        for (int i = 0; i < n; i++)
        {
            float yl, yr; k.Tick(l[i], r[i], out yl, out yr);
            subSum += yl * yl + yr * yr; subCnt++;
            if (subCnt >= sub)
            {
                sp.Add(subSum / sub); subSum = 0; subCnt = 0;
                int c = sp.Count;
                if (c >= 4)
                {
                    double z = (sp[c - 1] + sp[c - 2] + sp[c - 3] + sp[c - 4]) / 4; blocks.Add(z); Momentary = (float)Lufs(z);
                    if (c >= 30) { double s = 0; for (int j = c - 30; j < c; j++) s += sp[j]; ShortTerm = (float)Lufs(s / 30); }
                }
            }
            Array.Copy(hl, 1, hl, 0, 11); hl[11] = l[i]; Array.Copy(hr, 1, hr, 0, 11); hr[11] = r[i];
            float t = Math.Max(TruePeak.Oversampled(hl), TruePeak.Oversampled(hr));
            if (t > MaxTruePeak) MaxTruePeak = t; if (t > pk) pk = t;
            if (Math.Abs(l[i]) >= 0.9999f || Math.Abs(r[i]) >= 0.9999f) Clips++;
        }
        PeakNow = pk;
    }

    public double TruePeakDb { get { return MaxTruePeak > 0 ? 20 * Math.Log10(MaxTruePeak) : -120; } }

    public void UpdateIntegrated()
    {
        int n = blocks.Count; if (n == 0) { Integrated = -120; return; }
        double sum = 0; int cnt = 0; double absThr = Math.Pow(10, (-70 + 0.691) / 10);
        for (int i = 0; i < n; i++) if (blocks[i] > absThr) { sum += blocks[i]; cnt++; }
        if (cnt == 0) { Integrated = -120; return; }
        double rel = Lufs(sum / cnt) - 10, relThr = Math.Pow(10, (rel + 0.691) / 10); sum = 0; cnt = 0;
        for (int i = 0; i < n; i++) if (blocks[i] > relThr) { sum += blocks[i]; cnt++; }
        Integrated = cnt == 0 ? -120 : (float)Lufs(sum / cnt);
    }
}

static class Loudness
{
    public static LoudnessResult Measure(float[] l, float[] r, int sr, int from, int to)
    {
        var res = new LoudnessResult(); if (to > l.Length) to = l.Length; if (to - from < sr / 2) return res;
        var k = new KWeight(sr); int sub = sr / 10, nSub = (to - from) / sub; var sp = new double[nSub];
        double pkL = 0, tp = 0; float[] hl = new float[12], hr = new float[12]; int clips = 0;
        for (int b = 0; b < nSub; b++)
        {
            double s = 0;
            for (int i = from + b * sub; i < from + (b + 1) * sub; i++)
            {
                float yl, yr; k.Tick(l[i], r[i], out yl, out yr); s += yl * yl + yr * yr;
                float a = Math.Abs(l[i]), c = Math.Abs(r[i]); if (a > pkL) pkL = a; if (c > pkL) pkL = c;
                if (a >= 0.9999f || c >= 0.9999f) clips++;
                Array.Copy(hl, 1, hl, 0, 11); hl[11] = l[i]; Array.Copy(hr, 1, hr, 0, 11); hr[11] = r[i];
                double t = Math.Max(TruePeak.Oversampled(hl), TruePeak.Oversampled(hr)); if (t > tp) tp = t;
            }
            sp[b] = s / sub;
        }
        res.PeakDb = pkL > 0 ? 20 * Math.Log10(pkL) : -120; res.TruePeakDb = tp > 0 ? 20 * Math.Log10(tp) : -120; res.Clips = clips;
        if (nSub < 4) return res;
        var blocks = new List<double>(); for (int i = 3; i < nSub; i++) blocks.Add((sp[i] + sp[i - 1] + sp[i - 2] + sp[i - 3]) / 4);
        double absThr = Math.Pow(10, (-70 + 0.691) / 10), sum = 0; int cnt = 0;
        foreach (var z in blocks) if (z > absThr) { sum += z; cnt++; }
        if (cnt == 0) return res;
        double relThr = Math.Pow(10, ((-0.691 + 10 * Math.Log10(sum / cnt) - 10) + 0.691) / 10); sum = 0; cnt = 0;
        foreach (var z in blocks) if (z > relThr) { sum += z; cnt++; }
        res.I = cnt == 0 ? double.NegativeInfinity : -0.691 + 10 * Math.Log10(sum / cnt);
        // loudness range from 3 s short-term values
        var st = new List<double>();
        for (int i = 29; i < nSub; i += 5) { double s = 0; for (int j = i - 29; j <= i; j++) s += sp[j]; st.Add(s / 30); }
        var gated = new List<double>(); double sa = 0; int ca = 0; double absT = Math.Pow(10, (-70 + 0.691) / 10);
        foreach (var z in st) if (z > absT) { sa += z; ca++; }
        if (ca > 3)
        {
            double relT = Math.Pow(10, ((-0.691 + 10 * Math.Log10(sa / ca) - 20) + 0.691) / 10);
            foreach (var z in st) if (z > relT) gated.Add(-0.691 + 10 * Math.Log10(z));
            if (gated.Count > 3) { gated.Sort(); res.LRA = gated[(int)(gated.Count * 0.95)] - gated[(int)(gated.Count * 0.10)]; res.MaxShortTerm = gated[gated.Count - 1]; }
        }
        return res;
    }
}

// ---------------- tracks ----------------
class Track
{
    public string Name = "Track", Role = "other", Source = "";
    public AudioClip Clip; public long Offset;
    public float VolDb, Pan; public bool Mute, Solo; public int Color;
    public List<Fx> Fx = new List<Fx>();
    public volatile float PeakL, PeakR, RmsL, RmsR; public volatile bool Clipped;
    public float[] TapPre = new float[8192], TapPost = new float[8192]; public volatile int TapPos;
    internal float[] tl = new float[8192], tr = new float[8192];
    internal float[] dlyL = new float[1 << 17], dlyR = new float[1 << 17]; internal long dlyW;

    public int Latency() { int s = 0; for (int i = 0; i < Fx.Count; i++) if (!Fx[i].Bypass) s += Fx[i].Latency; return s; }
    public long EndFrame { get { return Clip == null ? 0 : Offset + Clip.Length; } }
}

class RenderOpts
{
    public bool Master = true, Dry = false, Fader = true, MuteSolo = true;
    public List<Track> Only;    // null = all tracks
}

// ---------------- the engine ----------------
class StudioEngine
{
    public const int SR = 44100;
    public readonly object Sync = new object();
    public List<Track> Tracks = new List<Track>();
    public List<Fx> MasterFx = new List<Fx>();
    public float MasterVolDb; public bool MasterBypass; public float AbMatchDb;
    public volatile bool Playing; public long Pos; public bool Loop;
    public LufsMeter Lufs = new LufsMeter(SR);
    public volatile float MasterPeakL, MasterPeakR; public float[] MasterTap = new float[8192]; public volatile int MasterTapPos;
    public float[] MasterTapPre = new float[8192];
    public volatile float MasterGr;
    float[] mixL = new float[8192], mixR = new float[8192], scRL = new float[1 << 16], scRR = new float[1 << 16];
    float[] scL = new float[8192], scR = new float[8192];
    WaveOutPlayer player;

    public StudioEngine() { InitMasterChain(); }

    public static Track NewTrack(string name, string role, AudioClip clip)
    {
        var t = new Track { Name = name, Role = role, Clip = clip, Source = clip == null ? "" : clip.Path };
        t.Color = role == "vocal" ? unchecked((int)0xFFE05CC8) : (role == "beat" ? unchecked((int)0xFF7C5CFF) : unchecked((int)0xFF5CC8E0));
        return t;
    }

    public long EndFrame { get { long e = 0; foreach (var t in Tracks) e = Math.Max(e, t.EndFrame); return e; } }

    // Default mastering chain (all modules present, gentle defaults; the user toggles / edits them).
    public void InitMasterChain()
    {
        MasterFx.Clear();
        var eq = new ParametricEq(); eq.Prepare(SR);
        var comp = new Compressor(); comp.Prepare(SR); comp.Set("thr", -14); comp.Set("ratio", 2); comp.Set("att", 30); comp.Set("rel", 200); comp.Set("knee", 8);
        var mb = new MultibandComp(); mb.Prepare(SR); mb.Bypass = true;
        var ex = new Exciter(); ex.Prepare(SR); ex.Set("mix", 25); ex.Bypass = true;
        var sat = new Saturation(); sat.Prepare(SR); sat.Set("drive", 3); sat.Set("mix", 60); sat.Bypass = true;
        var wid = new Widener(); wid.Prepare(SR); wid.Set("width", 105); wid.Bypass = true;
        var lim = new Limiter(); lim.Prepare(SR); lim.Set("gain", 3);
        MasterFx.AddRange(new Fx[] { eq, comp, mb, ex, sat, wid, lim });
    }

    // ---------------- mixing ----------------
    int PanGains(float pan, out float gl, out float gr)
    {
        gl = pan <= 0 ? 1f : (float)Math.Cos(pan * Math.PI / 2); gr = pan >= 0 ? 1f : (float)Math.Cos(-pan * Math.PI / 2); return 0;
    }

    public Track VocalTrack() { foreach (var t in Tracks) if (t.Role == "vocal") return t; return null; }

    void Mix(long pos, int n, RenderOpts o, float[] outL, float[] outR, bool meters, int pad)
    {
        if (n > mixL.Length) { mixL = new float[n]; mixR = new float[n]; scL = new float[n]; scR = new float[n]; foreach (var t in Tracks) { t.tl = new float[n]; t.tr = new float[n]; } }
        Array.Clear(mixL, 0, n); Array.Clear(mixR, 0, n);
        var list = o.Only ?? Tracks;
        bool soloActive = false; if (o.MuteSolo) foreach (var t in list) if (t.Solo) soloActive = true;
        int maxLat = 0; foreach (var t in list) maxLat = Math.Max(maxLat, o.Dry ? 0 : t.Latency());
        var voc = VocalTrack();

        // vocal dry signal -> sidechain ring (time aligned on the timeline)
        if (voc != null && voc.Clip != null)
            for (int i = 0; i < n; i++)
            {
                long idx = pos + i - voc.Offset; int w = (int)((pos + i) & (scRL.Length - 1));
                scRL[w] = (idx >= 0 && idx < voc.Clip.Length) ? voc.Clip.L[idx] : 0; scRR[w] = (idx >= 0 && idx < voc.Clip.Length) ? voc.Clip.R[idx] : 0;
            }

        foreach (var t in list)
        {
            if (t.Clip == null) continue;
            if (n > t.tl.Length) { t.tl = new float[n]; t.tr = new float[n]; }
            for (int i = 0; i < n; i++)
            {
                long idx = pos + i - t.Offset;
                if (idx >= 0 && idx < t.Clip.Length) { t.tl[i] = t.Clip.L[idx]; t.tr[i] = t.Clip.R[idx]; } else { t.tl[i] = 0; t.tr[i] = 0; }
            }
            if (meters) PushTap(t.TapPre, t, false, t.tl, t.tr, n);
            int latSoFar = 0;
            if (!o.Dry)
                for (int f = 0; f < t.Fx.Count; f++)
                {
                    var fx = t.Fx[f]; if (fx.Bypass) continue;
                    bool needsSc = fx.Type == "duck" || (fx.Type == "comp" && fx.Get("ext") > 0.5f);
                    if (needsSc && voc != null && voc != t)
                    {
                        for (int i = 0; i < n; i++) { int w = (int)((pos + i - latSoFar) & (scRL.Length - 1)); scL[i] = scRL[w]; scR[i] = scRR[w]; }
                        fx.ScL = scL; fx.ScR = scR;
                    }
                    else { fx.ScL = null; fx.ScR = null; }
                    try { fx.Process(t.tl, t.tr, n); } catch (Exception ex) { fx.Error = ex.Message; fx.Bypass = true; }
                    latSoFar += fx.Latency;
                }
            // plugin delay compensation
            int d = o.Dry ? 0 : maxLat - t.Latency(); int mask = t.dlyL.Length - 1;
            if (maxLat > 0)
                for (int i = 0; i < n; i++)
                {
                    long w = t.dlyW + i; t.dlyL[w & mask] = t.tl[i]; t.dlyR[w & mask] = t.tr[i];
                    if (d > 0) { t.tl[i] = t.dlyL[(w - d) & mask]; t.tr[i] = t.dlyR[(w - d) & mask]; }
                }
            t.dlyW += n;
            if (meters) PushTap(t.TapPost, t, true, t.tl, t.tr, n);

            bool audible = !o.MuteSolo || (!t.Mute && (!soloActive || t.Solo));
            float g = o.Fader ? DspMath.DbToLin(t.VolDb) : 1f, gl, gr; PanGains(o.Fader ? t.Pan : 0, out gl, out gr);
            double sl = 0, sr = 0; float pl = 0, pr = 0; bool clip = false;
            for (int i = 0; i < n; i++)
            {
                float yl = t.tl[i] * g * gl, yr = t.tr[i] * g * gr;
                float a = Math.Abs(yl), b = Math.Abs(yr); if (a > pl) pl = a; if (b > pr) pr = b; sl += yl * yl; sr += yr * yr;
                if (a > 1f || b > 1f) clip = true;
                if (audible) { mixL[i] += yl; mixR[i] += yr; }
            }
            if (meters)
            {
                t.PeakL = audible ? pl : 0; t.PeakR = audible ? pr : 0; t.RmsL = audible ? (float)Math.Sqrt(sl / n) : 0; t.RmsR = audible ? (float)Math.Sqrt(sr / n) : 0;
                if (clip && audible) t.Clipped = true;
            }
        }

        // master section
        if (meters) { PushTapRaw(MasterTapPre, mixL, mixR, n, false); }
        float mg = DspMath.DbToLin(MasterVolDb);
        bool useMaster = o.Master && !MasterBypass;
        float grMax = 0;
        if (o.Master)
        {
            if (useMaster)
                for (int f = 0; f < MasterFx.Count; f++)
                {
                    var fx = MasterFx[f]; if (fx.Bypass) continue; fx.ScL = null; fx.ScR = null;
                    try { fx.Process(mixL, mixR, n); } catch (Exception ex) { fx.Error = ex.Message; fx.Bypass = true; }
                    if (fx.GR > grMax) grMax = fx.GR;
                }
            else if (meters && AbMatchDb != 0) { float am = DspMath.DbToLin(AbMatchDb); for (int i = 0; i < n; i++) { mixL[i] *= am; mixR[i] *= am; } }
        }
        if (o.Master) for (int i = 0; i < n; i++) { mixL[i] *= mg; mixR[i] *= mg; }
        Array.Copy(mixL, outL, n); Array.Copy(mixR, outR, n);
        if (meters)
        {
            MasterGr = grMax; float pkL = 0, pkR = 0;
            for (int i = 0; i < n; i++) { float a = Math.Abs(mixL[i]), b = Math.Abs(mixR[i]); if (a > pkL) pkL = a; if (b > pkR) pkR = b; }
            MasterPeakL = pkL; MasterPeakR = pkR; PushTapRaw(MasterTap, mixL, mixR, n, true); Lufs.Process(mixL, mixR, n);
        }
    }

    void PushTap(float[] tap, Track t, bool post, float[] l, float[] r, int n)
    {
        int p = t.TapPos;
        for (int i = 0; i < n; i++) { tap[(p + i) & 8191] = (l[i] + r[i]) * 0.5f; }
        if (post) t.TapPos = (p + n) & 8191;
    }
    void PushTapRaw(float[] tap, float[] l, float[] r, int n, bool advance)
    {
        int p = MasterTapPos;
        for (int i = 0; i < n; i++) tap[(p + i) & 8191] = (l[i] + r[i]) * 0.5f;
        if (advance) MasterTapPos = (p + n) & 8191;
    }

    static readonly RenderOpts Live = new RenderOpts();

    // Real-time callback used by the audio device
    public void RenderRealtime(float[] oL, float[] oR, int n)
    {
        lock (Sync)
        {
            if (!Playing) { Array.Clear(oL, 0, n); Array.Clear(oR, 0, n); MasterPeakL = MasterPeakR = 0; foreach (var t in Tracks) { t.PeakL = t.PeakR = 0; } return; }
            Mix(Pos, n, Live, oL, oR, true, 0);
            Pos += n;
            long end = EndFrame + SR * 2;   // allow reverb / delay tails
            if (Pos >= end) { if (Loop) { Pos = 0; ResetStates(); } else { Playing = false; Pos = end; } }
        }
    }

    public void ResetStates()
    {
        foreach (var t in Tracks) { foreach (var f in t.Fx) f.Reset(); Array.Clear(t.dlyL, 0, t.dlyL.Length); Array.Clear(t.dlyR, 0, t.dlyR.Length); t.dlyW = 0; }
        foreach (var f in MasterFx) f.Reset();
        Array.Clear(scRL, 0, scRL.Length); Array.Clear(scRR, 0, scRR.Length); Lufs.Reset();
    }

    public void Seek(long frame) { lock (Sync) { Pos = Math.Max(0, frame); ResetStates(); } }

    public void Play()
    {
        EnsurePlayer();
        lock (Sync) { if (Pos >= EndFrame + SR * 2) Pos = 0; Playing = true; }
    }
    public void Pause() { Playing = false; }
    public void Stop() { Playing = false; lock (Sync) { Pos = 0; ResetStates(); } }

    void EnsurePlayer()
    {
        if (player != null) return;
        player = new WaveOutPlayer(RenderRealtime); player.Start();
    }

    public void Shutdown() { Playing = false; if (player != null) { player.Dispose(); player = null; } }

    // ---------------- offline render (export, analysis) ----------------
    public int RenderLatency(RenderOpts o)
    {
        var list = o.Only ?? Tracks; int maxLat = 0; foreach (var t in list) maxLat = Math.Max(maxLat, o.Dry ? 0 : t.Latency());
        int ml = 0; if (o.Master && !MasterBypass) foreach (var f in MasterFx) if (!f.Bypass) ml += f.Latency;
        return maxLat + ml;
    }

    public void Render(long start, long frames, RenderOpts o, Action<float> prog, out float[] oL, out float[] oR)
    {
        bool wasPlaying = Playing; Playing = false;
        lock (Sync)
        {
            ResetStates();
            int lat = RenderLatency(o); long total = frames + lat;
            oL = new float[frames]; oR = new float[frames];
            float[] bl = new float[2048], br = new float[2048]; long done = 0;
            // state of the plugins starts clean; the first `lat` frames are plugin delay and are discarded
            while (done < total)
            {
                int n = (int)Math.Min(2048, total - done);
                Mix(start + done, n, o, bl, br, false, 0);
                for (int i = 0; i < n; i++)
                {
                    long dst = done + i - lat; if (dst >= 0 && dst < frames) { oL[dst] = bl[i]; oR[dst] = br[i]; }
                }
                done += n; if (prog != null && (done & 0x3FFFF) < 2048) prog((float)done / total);
            }
            ResetStates(); Lufs.Reset();
        }
        Playing = false;   // exporting / analysing always leaves the transport stopped
    }
}

// ---------------- winmm output ----------------
class WaveOutPlayer : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] struct WAVEFORMATEX { public ushort wFormatTag, nChannels; public uint nSamplesPerSec, nAvgBytesPerSec; public ushort nBlockAlign, wBitsPerSample, cbSize; }
    [StructLayout(LayoutKind.Sequential)] struct WAVEHDR { public IntPtr lpData; public uint dwBufferLength, dwBytesRecorded; public IntPtr dwUser; public uint dwFlags, dwLoops; public IntPtr lpNext, reserved; }
    [DllImport("winmm.dll")] static extern int waveOutOpen(out IntPtr h, int dev, ref WAVEFORMATEX f, IntPtr cb, IntPtr inst, int flags);
    [DllImport("winmm.dll")] static extern int waveOutPrepareHeader(IntPtr h, IntPtr hdr, int size);
    [DllImport("winmm.dll")] static extern int waveOutUnprepareHeader(IntPtr h, IntPtr hdr, int size);
    [DllImport("winmm.dll")] static extern int waveOutWrite(IntPtr h, IntPtr hdr, int size);
    [DllImport("winmm.dll")] static extern int waveOutReset(IntPtr h);
    [DllImport("winmm.dll")] static extern int waveOutClose(IntPtr h);
    const int CALLBACK_EVENT = 0x50000, WHDR_DONE = 1, Frames = 1024, Buffers = 4;

    Action<float[], float[], int> render; IntPtr hwo; IntPtr[] hdrs = new IntPtr[Buffers], data = new IntPtr[Buffers];
    ManualResetEvent ev = new ManualResetEvent(false); Thread th; volatile bool run; float[] fl = new float[Frames], fr = new float[Frames]; short[] tmp = new short[Frames * 2];

    public WaveOutPlayer(Action<float[], float[], int> render) { this.render = render; }

    public void Start()
    {
        var f = new WAVEFORMATEX { wFormatTag = 1, nChannels = 2, nSamplesPerSec = 44100, wBitsPerSample = 16, nBlockAlign = 4, nAvgBytesPerSec = 44100 * 4 };
        int r = waveOutOpen(out hwo, -1, ref f, ev.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, CALLBACK_EVENT);
        if (r != 0) throw new Exception("Couldn't open the audio output device (winmm error " + r + ").");
        for (int i = 0; i < Buffers; i++)
        {
            data[i] = Marshal.AllocHGlobal(Frames * 4); hdrs[i] = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WAVEHDR)));
            var h = new WAVEHDR { lpData = data[i], dwBufferLength = (uint)(Frames * 4), dwFlags = 0 };
            Marshal.StructureToPtr(h, hdrs[i], false); waveOutPrepareHeader(hwo, hdrs[i], Marshal.SizeOf(typeof(WAVEHDR)));
            Fill(i);
        }
        run = true; th = new Thread(Loop) { IsBackground = true, Priority = ThreadPriority.AboveNormal }; th.Start();
    }

    void Fill(int i)
    {
        render(fl, fr, Frames);
        for (int k = 0; k < Frames; k++)
        {
            float a = fl[k], b = fr[k]; if (a > 1) a = 1; else if (a < -1) a = -1; if (b > 1) b = 1; else if (b < -1) b = -1;
            tmp[k * 2] = (short)(a * 32767f); tmp[k * 2 + 1] = (short)(b * 32767f);
        }
        Marshal.Copy(tmp, 0, data[i], Frames * 2);
        var h = (WAVEHDR)Marshal.PtrToStructure(hdrs[i], typeof(WAVEHDR)); h.dwFlags &= ~(uint)WHDR_DONE; Marshal.StructureToPtr(h, hdrs[i], false);
        waveOutWrite(hwo, hdrs[i], Marshal.SizeOf(typeof(WAVEHDR)));
    }

    void Loop()
    {
        while (run)
        {
            ev.WaitOne(30); ev.Reset();
            for (int i = 0; i < Buffers && run; i++)
            {
                var h = (WAVEHDR)Marshal.PtrToStructure(hdrs[i], typeof(WAVEHDR));
                if ((h.dwFlags & WHDR_DONE) != 0) Fill(i);
            }
        }
    }

    public void Dispose()
    {
        run = false; ev.Set(); if (th != null) th.Join(500);
        if (hwo != IntPtr.Zero)
        {
            waveOutReset(hwo);
            for (int i = 0; i < Buffers; i++) { waveOutUnprepareHeader(hwo, hdrs[i], Marshal.SizeOf(typeof(WAVEHDR))); Marshal.FreeHGlobal(data[i]); Marshal.FreeHGlobal(hdrs[i]); }
            waveOutClose(hwo); hwo = IntPtr.Zero;
        }
    }
}
