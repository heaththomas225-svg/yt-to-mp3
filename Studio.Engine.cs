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

// ---------------- audio clip (44.1 kHz stereo float in memory; decoded as a stream so very long files work) ----------------
class AudioClip
{
    public float[] L, R; public string Path = "", Name = ""; int len;
    public int Length { get { return len; } }
    public bool Loaded { get { return L != null; } }
    const int Bin = 256; float[] pmin, pmax;

    public static double ProbeSeconds(string path)
    {
        try
        {
            string fp = StudioEnv.Ffprobe == null ? null : StudioEnv.Ffprobe(); if (fp == null) return 0;
            var psi = new ProcessStartInfo(fp, "-v error -show_entries format=duration -of csv=p=0 \"" + path + "\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
            using (var p = Process.Start(psi))
            {
                string s = p.StandardOutput.ReadToEnd().Trim(); p.WaitForExit(); double d;
                return double.TryParse(s.Split(new[] { ',', '\r', '\n' })[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d) ? d : 0;
            }
        }
        catch { return 0; }
    }

    // Streams ffmpeg's output straight into the sample arrays: no 2 GB buffer limit, progress and cancel supported.
    public static AudioClip FromFile(string path, Action<float> progress = null, Func<bool> cancelled = null)
    {
        string ff = StudioEnv.Ffmpeg();
        if (ff == null) throw new Exception("ffmpeg isn't installed yet. Convert something once in the main window first (it installs ffmpeg).");
        double dur = ProbeSeconds(path); long expect = dur > 0 ? (long)(dur * 44100.0) + 8820 : 0;
        int cap = (int)Math.Max(44100L * 5, Math.Min(expect, int.MaxValue - 8192));
        var l = new float[cap]; var r = new float[cap]; int count = 0;
        var psi = new ProcessStartInfo(ff, "-v error -i \"" + path + "\" -vn -ac 2 -ar 44100 -f f32le -")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        string err = "";
        using (var p = Process.Start(psi))
        {
            var et = new Thread(() => { try { err = p.StandardError.ReadToEnd(); } catch { } }) { IsBackground = true };
            et.Start();
            var stream = p.StandardOutput.BaseStream; var buf = new byte[1 << 18]; var fb = new float[buf.Length / 4]; int carry = 0, read;
            while ((read = stream.Read(buf, carry, buf.Length - carry)) > 0)
            {
                if (cancelled != null && cancelled()) { try { p.Kill(); } catch { } throw new Exception("Cancelled."); }
                int total = carry + read, frames = total / 8;
                if (count + frames > l.Length)
                {
                    long nc = Math.Min((long)int.MaxValue - 8192, Math.Max((long)l.Length * 3 / 2, (long)count + frames));
                    if (nc < (long)count + frames) throw new Exception("That file is too long to load (limit about 13 hours).");
                    Array.Resize(ref l, (int)nc); Array.Resize(ref r, (int)nc);
                }
                Buffer.BlockCopy(buf, 0, fb, 0, frames * 8);
                for (int i = 0; i < frames; i++) { l[count + i] = fb[i * 2]; r[count + i] = fb[i * 2 + 1]; }
                count += frames; carry = total - frames * 8; if (carry > 0) Buffer.BlockCopy(buf, frames * 8, buf, 0, carry);
                if (progress != null && expect > 0) progress(Math.Min(1f, count / (float)expect));
            }
            p.WaitForExit(); et.Join(2000);
            if (p.ExitCode != 0 || count < 1) throw new Exception("Couldn't read that audio file." + (err.Length > 0 ? "\n" + err.Trim() : ""));
        }
        var c = new AudioClip { L = l, R = r, len = count, Path = path, Name = System.IO.Path.GetFileNameWithoutExtension(path) };
        c.BuildPeaks();
        return c;
    }

    public static AudioClip FromArrays(float[] l, float[] r, string name)
    {
        var c = new AudioClip { L = l, R = r, len = l.Length, Name = name }; c.BuildPeaks(); return c;
    }

    // Frees the sample memory but keeps the waveform overview, so long songs don't hog RAM once they've been split.
    public void Unload() { if (pmin != null) { L = null; R = null; } }
    public void Reload(Action<float> progress, Func<bool> cancelled) { var c = FromFile(Path, progress, cancelled); L = c.L; R = c.R; len = c.len; }

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
        bool direct = L != null;
        for (int p = 0; p < pixels; p++)
        {
            long a = start + (long)(p * spp), b = start + (long)((p + 1) * spp); if (b <= a) b = a + 1;
            if (b <= 0 || a >= Length) { mn[p] = 0; mx[p] = 0; continue; }
            if (a < 0) a = 0; if (b > Length) b = Length;
            float lo = 1e9f, hi = -1e9f;
            if (b - a >= Bin * 2 || !direct)
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
// ---------------- WAV writer (16 / 24 / 32-bit float), streaming so any length works up to the 4 GB WAV limit ----------------
class WavStream : IDisposable
{
    FileStream f; BinaryWriter w; readonly int bits, bytes; long dataLen; readonly Random rnd = new Random(1); readonly byte[] buf; int bi;

    public WavStream(string path, int sr, int bits)
    {
        this.bits = bits; bytes = bits / 8; buf = new byte[4096 * 2 * bytes + 16];
        f = new FileStream(path, FileMode.Create, FileAccess.Write); w = new BinaryWriter(f);
        w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(0); w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
        w.Write(16); w.Write((short)(bits == 32 ? 3 : 1)); w.Write((short)2); w.Write(sr); w.Write(sr * 2 * bytes); w.Write((short)(2 * bytes)); w.Write((short)bits);
        w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(0);
    }

    public void Write(float[] l, float[] r, int off, int n)
    {
        for (int i = off; i < off + n; i++)
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
            if (bi >= buf.Length - 16) { w.Write(buf, 0, bi); dataLen += bi; bi = 0; }
        }
    }

    public void Dispose()
    {
        if (f == null) return;
        if (bi > 0) { w.Write(buf, 0, bi); dataLen += bi; bi = 0; }
        long d = Math.Min(dataLen, 0xFFFFFFF0L - 36);
        f.Seek(4, SeekOrigin.Begin); w.Write((uint)(36 + d)); f.Seek(40, SeekOrigin.Begin); w.Write((uint)d); w.Close(); f = null;
    }
}

static class WavIO
{
    public static void Write(string path, float[] l, float[] r, int sr, int bits) { using (var s = new WavStream(path, sr, bits)) s.Write(l, r, 0, l.Length); }
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
    public bool Instrument; public readonly List<MidiEv> Midi = new List<MidiEv>();   // an instrument track has no audio file: a VST instrument plays recorded notes
    public long ClipStart; public long ClipFrames = -1;   // the part of the audio file that is used (after splitting / trimming); -1 = to the end
    public long UsedLength { get { if (Clip == null) return 0; long avail = Math.Max(0, Clip.Length - ClipStart); return ClipFrames < 0 ? avail : Math.Min(ClipFrames, avail); } }
    public AudioClip Clip; public long Offset;
    public float VolDb, Pan; public bool Mute, Solo; public int Color;
    public Track Host;   // set = this track shares the row of its Host (an audio clip, a MIDI instrument and more can live on one row)
    public bool IsBus, Armed; public Track Out;   // a bus has no audio of its own: it mixes the tracks sent to it (Out), runs its own plugins, then goes to the master. Armed = an empty audio track waiting for a recording
    public List<Fx> Fx = new List<Fx>();
    public double Bpm; public bool BpmChecked;   // the tempo heard in this track (0 = none heard yet) and whether it has been listened to
    public long FadeIn, FadeOut;   // frames: this clip fades in over its first FadeIn frames and out over its last FadeOut frames
    public float ClipGainDb;       // the volume of this clip (piece) on its own, on top of the track fader
    public bool Locked;            // locked in place (set on a row's own track: it covers every piece on the row)
    public List<AutoLane> Auto = new List<AutoLane>();   // automation: settings of the plugins above that follow a curve
    public volatile float PeakL, PeakR, RmsL, RmsR; public volatile bool Clipped;
    public float[] TapPre = new float[8192], TapPost = new float[8192]; public volatile int TapPos;
    internal float[] tl = new float[8192], tr = new float[8192];
    internal float[] dlyL = new float[1 << 17], dlyR = new float[1 << 17]; internal long dlyW; internal bool fxIdle;

    public int Latency() { int s = 0; for (int i = 0; i < Fx.Count; i++) if (!Fx[i].Bypass) s += Fx[i].Latency; return s; }
    public long EndFrame { get { if (Clip != null) return Offset + UsedLength; if (Instrument && Midi.Count > 0) { lock (Midi) return Midi[Midi.Count - 1].Frame + 44100L * 3; } return 0; } }
}

class RenderOpts
{
    public bool Master = true, Dry = false, Fader = true, MuteSolo = true;
    public List<Track> Only;    // null = all tracks
    public bool NoClips;        // live playing while stopped: instruments only, the audio files stay silent
}

// ---------------- the engine ----------------
partial class StudioEngine
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

    public static StudioEngine Current;   // the engine the instrument windows play through
    public string AudioStatus = "Audio: not started yet";
    public StudioEngine() { InitMasterChain(); Current = this; }
    // Opens the sound device. The piano needs this too, not only PLAY.
    public bool EnsureAudio()
    {
        try { EnsurePlayer(); AudioStatus = "Audio ready"; return true; }
        catch (Exception ex) { AudioStatus = "Audio unavailable: " + ex.Message; return false; }
    }

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

    static int EffLat(Track t) { return t.IsBus ? 0 : t.Latency() + (t.Out != null && t.Out.IsBus ? t.Out.Latency() : 0); }   // a track sent to a bus is as late as its own plugins plus the bus plugins

    public Track VocalTrack() { foreach (var t in Tracks) if (t.Role == "vocal") return t; return null; }

    void Mix(long pos, int n, RenderOpts o, float[] outL, float[] outR, bool meters, int pad)
    {
        if (n > mixL.Length) { mixL = new float[n]; mixR = new float[n]; scL = new float[n]; scR = new float[n]; foreach (var t in Tracks) { t.tl = new float[n]; t.tr = new float[n]; } }
        Array.Clear(mixL, 0, n); Array.Clear(mixR, 0, n);
        var list = o.Only ?? Tracks;
        bool soloActive = false; if (o.MuteSolo) foreach (var t in list) if (t.Solo) soloActive = true;
        int maxLat = 0; foreach (var t in list) maxLat = Math.Max(maxLat, o.Dry ? 0 : EffLat(t));
        var voc = VocalTrack();

        // vocal dry signal -> sidechain ring (time aligned on the timeline)
        if (voc != null && voc.Clip != null)
        {
            var vc = voc.Clip; long voff = voc.Offset, vcs = voc.ClipStart, vcf = voc.ClipFrames; float[] vL = vc == null ? null : vc.L, vR = vc == null ? null : (vc.R ?? vc.L);
            long vavail = vL == null ? 0 : Math.Max(0, vL.Length - vcs), vlen = vcf < 0 ? vavail : Math.Min(vcf, vavail);
            for (int i = 0; i < n; i++)
            {
                long idx = pos + i - voff; int w = (int)((pos + i) & (scRL.Length - 1));
                bool inside = vL != null && idx >= 0 && idx < vlen && idx + vcs < vL.Length && idx + vcs < vR.Length;
                scRL[w] = inside ? vL[idx + vcs] : 0; scRR[w] = inside ? vR[idx + vcs] : 0;
            }
        }

        for (int bi = 0; bi < list.Count; bi++) { var bt = list[bi]; if (bt.IsBus) { if (n > bt.tl.Length) { bt.tl = new float[n]; bt.tr = new float[n]; } Array.Clear(bt.tl, 0, n); Array.Clear(bt.tr, 0, n); } }
        for (int pass = 0; pass < 2; pass++)   // pass 0: the tracks (some feed a bus), pass 1: the buses
        foreach (var t in list)
        {
            if (t.IsBus != (pass == 1)) continue;
            if (t.Clip == null && !t.Instrument && !t.IsBus) continue;
            if (n > t.tl.Length) { t.tl = new float[n]; t.tr = new float[n]; }
            bool audible = t.IsBus ? (!o.MuteSolo || !t.Mute) : (!o.MuteSolo || (!t.Mute && (!soloActive || t.Solo)));
            bool idle = !audible && !o.Dry;   // muted / not soloed: skip the plugin chain (the output is discarded anyway)
            var tclip = o.NoClips ? null : t.Clip; long toff = t.Offset, tcs = t.ClipStart, tcf = t.ClipFrames; float[] cl = tclip == null ? null : tclip.L, cr = tclip == null ? null : (tclip.R ?? tclip.L);
            long tavail = cl == null ? 0 : Math.Max(0, cl.Length - tcs), ulen = tcf < 0 ? tavail : Math.Min(tcf, tavail);
            if (!t.IsBus) for (int i = 0; i < n; i++)
            {
                long idx = pos + i - toff, k = idx + tcs;
                if (cl != null && idx >= 0 && idx < ulen && k < cl.Length && k < cr.Length) { t.tl[i] = cl[k]; t.tr[i] = cr[k]; } else { t.tl[i] = 0; t.tr[i] = 0; }
            }
            if (!t.IsBus && !t.Instrument && cl != null && (t.ClipGainDb != 0f || t.FadeIn > 0 || t.FadeOut > 0))
            {
                float cg = DspMath.DbToLin(t.ClipGainDb); long fi = t.FadeIn, fo = t.FadeOut;
                for (int i = 0; i < n; i++)
                {
                    long idx = pos + i - toff; if (idx < 0 || idx >= ulen) continue; float gg = cg;
                    if (fi > 0 && idx < fi) gg *= (float)Math.Sin(idx / (double)fi * Math.PI / 2);
                    if (fo > 0 && idx >= ulen - fo) gg *= (float)Math.Sin((ulen - idx) / (double)fo * Math.PI / 2);
                    t.tl[i] *= gg; t.tr[i] *= gg;
                }
            }
            if (!idle && t.fxIdle) { for (int f = 0; f < t.Fx.Count; f++) t.Fx[f].Reset(); Array.Clear(t.dlyL, 0, t.dlyL.Length); Array.Clear(t.dlyR, 0, t.dlyR.Length); t.fxIdle = false; }
            if (meters) PushTap(t.TapPre, t, false, t.tl, t.tr, n);
            int latSoFar = 0;
            if (idle) { if (!t.fxIdle) { t.fxIdle = true; for (int f = 0; f < t.Fx.Count; f++) t.Fx[f].GR = 0; } }
            else if (!o.Dry)
                for (int f = 0; f < t.Fx.Count; f++)
                {
                    var fx = t.Fx[f]; if (fx.Bypass) continue;
                    bool needsSc = fx is Ducker || fx.WantsSidechain || (fx is Compressor && fx.Get("ext") > 0.5f);
                    if (needsSc && voc != null && voc != t)
                    {
                        for (int i = 0; i < n; i++) { int w = (int)((pos + i - latSoFar) & (scRL.Length - 1)); scL[i] = scRL[w]; scR[i] = scRR[w]; }
                        fx.ScL = scL; fx.ScR = scR;
                    }
                    else { fx.ScL = null; fx.ScR = null; }
                    try { var lanes = t.Auto.Count > 0 ? AutoLanes(t, fx) : null; if (lanes == null) { fx.BeginBlock(t, pos, n, !meters || Playing); fx.Process(t.tl, t.tr, n); } else ProcessAuto(t, fx, lanes, pos, n, !meters || Playing, needsSc && voc != null && voc != t); } catch (Exception ex) { fx.Error = ex.Message; fx.Bypass = true; }
                    latSoFar += fx.Latency;
                }
            // plugin delay compensation
            int d = o.Dry ? 0 : maxLat - EffLat(t); int mask = t.dlyL.Length - 1;
            if (maxLat > 0 && !idle && !t.IsBus)
                for (int i = 0; i < n; i++)
                {
                    long w = t.dlyW + i; t.dlyL[w & mask] = t.tl[i]; t.dlyR[w & mask] = t.tr[i];
                    if (d > 0) { t.tl[i] = t.dlyL[(w - d) & mask]; t.tr[i] = t.dlyR[(w - d) & mask]; }
                }
            t.dlyW += n;
            if (meters) PushTap(t.TapPost, t, true, t.tl, t.tr, n);

            Track dest = !t.IsBus && t.Out != null && t.Out.IsBus && t.Out != t && (o.Only == null || o.Only.Contains(t.Out)) ? t.Out : null;
            float g = o.Fader ? DspMath.DbToLin(t.VolDb) : 1f, gl, gr; PanGains(o.Fader ? t.Pan : 0, out gl, out gr);
            double sl = 0, sr = 0; float pl = 0, pr = 0; bool clip = false;
            for (int i = 0; i < n; i++)
            {
                float yl = t.tl[i] * g * gl, yr = t.tr[i] * g * gr;
                float a = Math.Abs(yl), b = Math.Abs(yr); if (a > pl) pl = a; if (b > pr) pr = b; sl += yl * yl; sr += yr * yr;
                if (a > 1f || b > 1f) clip = true;
                if (audible) { if (dest != null) { dest.tl[i] += yl; dest.tr[i] += yr; } else { mixL[i] += yl; mixR[i] += yr; } }
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

    public volatile bool CountIn; public long LastCountFrames; long countLeft;
    public int CountBeat { get { long cl = countLeft, tot = LastCountFrames; if (cl <= 0 || tot <= 0 || !Playing) return 0; int b = 1 + (int)((tot - cl) * 4 / tot); return Math.Max(1, Math.Min(4, b)); } }   // which beat of the count-in (1 to 4) is sounding, 0 = not counting   // Count In: one bar of clicks before playing or recording starts
    public volatile bool Metronome;   // click on every beat while the song plays
    public static volatile int LiveUntil;   // Environment.TickCount until which instruments keep sounding even though the song is stopped
    static readonly RenderOpts Live = new RenderOpts();

    // Real-time callback used by the audio device
    public volatile bool Rendering; readonly object renderGate = new object();
    public void RenderRealtime(float[] oL, float[] oR, int n)
    {
        if (Rendering) { Array.Clear(oL, 0, n); Array.Clear(oR, 0, n); return; }
        lock (Sync)
        {
            if (Rendering) { Array.Clear(oL, 0, n); Array.Clear(oR, 0, n); return; }
            if (!Playing)
            {
                if (unchecked(Environment.TickCount - LiveUntil) < 0)   // a note is being played on an instrument: only the instrument tracks sound
                {
                    var inst = new List<Track>();
                    foreach (var t in Tracks) { bool has = t.Instrument; if (!has) foreach (var f in t.Fx) { var ifx = f as InstrumentFx; if (ifx != null && ifx.IsInstrument && !f.Bypass) has = true; } if (has) inst.Add(t); }
                    if (inst.Count > 0) { Mix(Pos, n, new RenderOpts { Only = inst, NoClips = true }, oL, oR, true, 0); return; }
                }
                Array.Clear(oL, 0, n); Array.Clear(oR, 0, n); MasterPeakL = MasterPeakR = 0; foreach (var t in Tracks) { t.PeakL = t.PeakR = 0; } return;
            }
            if (countLeft > 0) { Array.Clear(oL, 0, n); Array.Clear(oR, 0, n); Click(LastCountFrames - countLeft, n, oL, oR, LastCountFrames); countLeft = Math.Max(0, countLeft - n); MasterPeakL = MasterPeakR = 0; return; }   // the count-in: only the clicks, the song waits
            Mix(Pos, n, Live, oL, oR, true, 0);
            if (Metronome) Click(Pos, n, oL, oR);
            Pos += n;
            long end = EndFrame + SR * 2;   // allow reverb / delay tails
            if (Pos >= end) { if (Loop) { Pos = 0; ResetStates(); } else { Playing = false; Pos = end; } }
        }
    }

    void Click(long pos, int n, float[] oL, float[] oR, long limit = long.MaxValue)
    {
        double beat = SR * 60.0 / Math.Max(20, Fx.ProjectBpm); long bar = (long)(beat * 4);
        for (int i = 0; i < n; i++)
        {
            long p = pos + i; if (p >= limit) break;   // the count-in is exactly four beats: nothing may start a fifth click when the last block runs past the end
            long inBeat = (long)(p - Math.Floor(p / beat) * beat); if (inBeat > 2400) continue;
            bool accent = (long)(p - Math.Floor(p / (double)bar) * bar) < 2400; double f = accent ? 1900 : 1250; float v = (float)(Math.Sin(2 * Math.PI * f * inBeat / SR) * Math.Exp(-inBeat / 380.0) * (accent ? 0.35 : 0.22)); oL[i] += v; oR[i] += v;
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
        lock (Sync) { if (Pos >= EndFrame + SR * 2) Pos = 0; if (!Playing) { if (CountIn) { LastCountFrames = (long)(SR * 60.0 / Math.Max(20, Fx.ProjectBpm) * 4); countLeft = LastCountFrames; } else { LastCountFrames = 0; countLeft = 0; } } Playing = true; }
    }
    public void Pause() { Playing = false; countLeft = 0; }
    public void Stop() { Playing = false; lock (Sync) { Pos = 0; countLeft = 0; ResetStates(); } }

    void EnsurePlayer()
    {
        if (player != null) return;
        player = new WaveOutPlayer(RenderRealtime); player.Start();
    }

    public void ReopenOutput() { bool was = Playing; if (player != null) { player.Dispose(); player = null; } if (was) { try { EnsurePlayer(); } catch { Playing = false; } } }
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
        var L = new float[frames]; var R = new float[frames]; long w = 0;
        RenderStream(start, frames, o, prog, (bl, br, off, n) => { Array.Copy(bl, off, L, w, n); Array.Copy(br, off, R, w, n); w += n; }, null);
        oL = L; oR = R;
    }

    // Renders in small blocks and hands each one to `sink` (so exports of very long songs use almost no memory).
    // The first `latency` frames are plugin delay and are dropped, which keeps every stem sample-aligned with the timeline.
    public void RenderStream(long start, long frames, RenderOpts o, Action<float> prog, Action<float[], float[], int, int> sink, Func<bool> cancelled)
    {
        Playing = false;
        lock (renderGate)
        {
            Rendering = true;   // the live callback outputs silence meanwhile; Sync is only held per block so the UI and Cancel stay responsive
            try
            {
                int lat; lock (Sync) { ResetStates(); lat = RenderLatency(o); }
                foreach (var t in Tracks.ToArray()) foreach (var f in t.Fx.ToArray()) { try { f.PrepareOffline(); } catch { } }   // e.g. wait for a convolution reverb's impulse response
                foreach (var f in MasterFx.ToArray()) { try { f.PrepareOffline(); } catch { } }
                long total = frames + lat, done = 0, emitted = 0; float[] bl = new float[4096], br = new float[4096];
                while (done < total && emitted < frames)
                {
                    if (cancelled != null && cancelled()) break;
                    int n = (int)Math.Min(4096, total - done);
                    lock (Sync) Mix(start + done, n, o, bl, br, false, 0);
                    int skip = (int)Math.Max(0, Math.Min(n, lat - done)), cnt = (int)Math.Min(n - skip, frames - emitted);
                    if (cnt > 0) { sink(bl, br, skip, cnt); emitted += cnt; }
                    done += n; if (prog != null) prog((float)Math.Min(1.0, (double)done / total));
                }
                lock (Sync) { ResetStates(); Lufs.Reset(); }
            }
            finally { Rendering = false; }
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
    const int CALLBACK_EVENT = 0x50000, WHDR_DONE = 1;
    public static volatile int Underruns;       // times the sound card ran out of audio (heard as stutter)
    public static volatile int QueuedFrames = 4096;   // audio waiting in the buffers: how far the sound lags behind the render position
    readonly int Buffers = Prefs.Buf == 0 ? 4 : 8;   // more, smaller buffers: the same 23 ms steps but much more slack when the computer is busy
    public static volatile float Gain = 1f;   // playback volume from Settings (exports are not affected)
    public static int Device { get { return Prefs.OutDev; } }
    readonly int Frames = Prefs.Buf == 0 ? 512 : (Prefs.Buf == 2 ? 2048 : 1024);
    [DllImport("winmm.dll", CharSet = CharSet.Auto)] static extern int waveOutGetNumDevs();
    [DllImport("winmm.dll", CharSet = CharSet.Auto)] static extern int waveOutGetDevCaps(IntPtr id, ref WAVEOUTCAPS caps, int size);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)] struct WAVEOUTCAPS { public ushort mid, pid; public uint ver; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string name; public uint formats; public ushort ch, res; public uint support; }
    public static System.Collections.Generic.List<string> DeviceNames()
    {
        var l = new System.Collections.Generic.List<string>(); int n = waveOutGetNumDevs();
        for (int i = 0; i < n; i++) { var c = new WAVEOUTCAPS(); if (waveOutGetDevCaps((IntPtr)i, ref c, Marshal.SizeOf(typeof(WAVEOUTCAPS))) == 0) l.Add(c.name); else l.Add("Device " + i); }
        return l;
    }

    Action<float[], float[], int> render; IntPtr hwo; IntPtr[] hdrs, data;
    ManualResetEvent ev = new ManualResetEvent(false); Thread th; volatile bool run; float[] fl, fr; short[] tmp;

    public WaveOutPlayer(Action<float[], float[], int> render) { this.render = render; hdrs = new IntPtr[Buffers]; data = new IntPtr[Buffers]; QueuedFrames = Buffers * Frames; fl = new float[Frames]; fr = new float[Frames]; tmp = new short[Frames * 2]; }

    public void Start()
    {
        var f = new WAVEFORMATEX { wFormatTag = 1, nChannels = 2, nSamplesPerSec = 44100, wBitsPerSample = 16, nBlockAlign = 4, nAvgBytesPerSec = 44100 * 4 };
        int r = waveOutOpen(out hwo, Device, ref f, ev.SafeWaitHandle.DangerousGetHandle(), IntPtr.Zero, CALLBACK_EVENT);
        if (r != 0) throw new Exception("Couldn't open the audio output device (winmm error " + r + ").");
        for (int i = 0; i < Buffers; i++)
        {
            data[i] = Marshal.AllocHGlobal(Frames * 4); hdrs[i] = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WAVEHDR)));
            var h = new WAVEHDR { lpData = data[i], dwBufferLength = (uint)(Frames * 4), dwFlags = 0 };
            Marshal.StructureToPtr(h, hdrs[i], false); waveOutPrepareHeader(hwo, hdrs[i], Marshal.SizeOf(typeof(WAVEHDR)));
            Fill(i);
        }
        run = true; th = new Thread(Loop) { IsBackground = true, Priority = ThreadPriority.Highest, Name = "audio" }; th.Start();
    }

    void Fill(int i)
    {
        try { render(fl, fr, Frames); } catch { Array.Clear(fl, 0, Frames); Array.Clear(fr, 0, Frames); }
        float gn = Gain; if (gn != 1f) for (int k = 0; k < Frames; k++) { fl[k] *= gn; fr[k] *= gn; }
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
            int ready = 0; for (int i = 0; i < Buffers; i++) { var hh = (WAVEHDR)Marshal.PtrToStructure(hdrs[i], typeof(WAVEHDR)); if ((hh.dwFlags & WHDR_DONE) != 0) ready++; }
            if (ready >= Buffers) Underruns++;   // every buffer had already been played: the sound card was starved
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
