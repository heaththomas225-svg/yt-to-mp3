using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

// ======================================================================
//  Mastering: settings, analysis + ffmpeg processing chain, and the
//  custom-drawn "Purphex Mastering" style window (purple theme).
// ======================================================================

class MasterSettings
{
    public bool On;
    public int Character = 3;          // 0 Clean, 1 Valve, 2 Punch, 3 Transparent
    public float AutoEq = 100;         // 0..100 %
    public bool Custom;                // custom EQ nodes active
    public float Loud = -14;           // target LUFS
    public bool Excite;
    public float Width = 100;          // stereo width %
    public bool Comp;                  // loudness compensation: keep original loudness
    public float[] NF = { 80, 250, 1000, 4000, 10000 };
    public float[] NG = new float[5];

    public static readonly string[] CharNames = { "Clean", "Valve", "Punch", "Transparent" };
    public static readonly string[] NodeNames = { "Low", "Low-Mid", "Mid", "High-Mid", "High" };
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;

    public string Serialize()
    {
        var sb = new StringBuilder();
        sb.Append("on=").Append(On ? 1 : 0).Append(";char=").Append(Character)
          .Append(";auto=").Append(AutoEq.ToString("0.#", CI)).Append(";custom=").Append(Custom ? 1 : 0)
          .Append(";loud=").Append(Loud.ToString("0.#", CI)).Append(";excite=").Append(Excite ? 1 : 0)
          .Append(";width=").Append(Width.ToString("0.#", CI)).Append(";comp=").Append(Comp ? 1 : 0)
          .Append(";nodes=");
        for (int i = 0; i < 5; i++)
            sb.Append(i > 0 ? "," : "").Append(NF[i].ToString("0.#", CI)).Append(":").Append(NG[i].ToString("0.#", CI));
        return sb.ToString();
    }

    static float F(string s, float def, float lo, float hi)
    {
        float v;
        if (!float.TryParse(s, NumberStyles.Float, CI, out v)) return def;
        return Math.Max(lo, Math.Min(hi, v));
    }

    public void Parse(string s)
    {
        if (s == null) return;
        foreach (var part in s.Split(';'))
        {
            int i = part.IndexOf('='); if (i < 1) continue;
            string k = part.Substring(0, i).Trim(), v = part.Substring(i + 1).Trim();
            if (k == "on") On = v == "1";
            else if (k == "char") Character = (int)F(v, 3, 0, 3);
            else if (k == "auto") AutoEq = F(v, 100, 0, 100);
            else if (k == "custom") Custom = v == "1";
            else if (k == "loud") Loud = F(v, -14, -20, -6);
            else if (k == "excite") Excite = v == "1";
            else if (k == "width") Width = F(v, 100, 0, 200);
            else if (k == "comp") Comp = v == "1";
            else if (k == "nodes")
            {
                var ns = v.Split(',');
                for (int n = 0; n < 5 && n < ns.Length; n++)
                {
                    var fg = ns[n].Split(':');
                    if (fg.Length == 2) { NF[n] = F(fg[0], NF[n], 20, 20000); NG[n] = F(fg[1], 0, -12, 12); }
                }
            }
        }
    }

    public MasterSettings Clone() { var m = new MasterSettings(); m.Parse(Serialize()); return m; }
}

class MasterAnalysis
{
    public bool Ok;
    public double I = double.NaN, LRA = double.NaN, TP = double.NaN, M = double.NaN, S = double.NaN, Corr = double.NaN;
    public double[] Auto = new double[4];   // auto-EQ corrections (dB): low shelf 120, 350, 6.5k, high shelf 11k
    public bool HasBands;
}

static class MasterDsp
{
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    static string N(double d) { return d.ToString("0.###", CI); }

    public static readonly string[] FmtEnc = {
        "-c:a libmp3lame -b:a 320k", "-c:a pcm_s24le",
        "-c:a flac", "-c:a aac -b:a 256k", "-c:a libopus -b:a 192k" };
    public static readonly string[] FmtExt = { "mp3", "wav", "flac", "m4a", "opus" };

    // ---- biquad magnitude (RBJ cookbook) so the on-screen curve matches the filters ----
    public static double BiquadDb(int type, double f0, double gDb, double q, double f)
    {
        double fs = 48000, A = Math.Pow(10, gDb / 40), w0 = 2 * Math.PI * f0 / fs;
        double cw = Math.Cos(w0), sw = Math.Sin(w0), alpha = sw / (2 * q);
        double b0, b1, b2, a0, a1, a2;
        if (type == 0)
        {
            b0 = 1 + alpha * A; b1 = -2 * cw; b2 = 1 - alpha * A;
            a0 = 1 + alpha / A; a1 = -2 * cw; a2 = 1 - alpha / A;
        }
        else
        {
            double sq = 2 * Math.Sqrt(A) * alpha;
            if (type == 1)
            {
                b0 = A * ((A + 1) - (A - 1) * cw + sq); b1 = 2 * A * ((A - 1) - (A + 1) * cw); b2 = A * ((A + 1) - (A - 1) * cw - sq);
                a0 = (A + 1) + (A - 1) * cw + sq; a1 = -2 * ((A - 1) + (A + 1) * cw); a2 = (A + 1) + (A - 1) * cw - sq;
            }
            else
            {
                b0 = A * ((A + 1) + (A - 1) * cw + sq); b1 = -2 * A * ((A - 1) + (A + 1) * cw); b2 = A * ((A + 1) + (A - 1) * cw - sq);
                a0 = (A + 1) - (A - 1) * cw + sq; a1 = 2 * ((A - 1) - (A + 1) * cw); a2 = (A + 1) - (A - 1) * cw - sq;
            }
        }
        double w = 2 * Math.PI * f / fs;
        double c1 = Math.Cos(w), s1 = Math.Sin(w), c2 = Math.Cos(2 * w), s2 = Math.Sin(2 * w);
        double nr = b0 + b1 * c1 + b2 * c2, ni = -(b1 * s1 + b2 * s2);
        double dr = a0 + a1 * c1 + a2 * c2, di = -(a1 * s1 + a2 * s2);
        return 10 * Math.Log10((nr * nr + ni * ni) / (dr * dr + di * di));
    }

    // node 0 = low shelf, 1..3 = peaks, 4 = high shelf
    static int NodeType(int i) { return i == 0 ? 1 : (i == 4 ? 2 : 0); }
    static double NodeQ(int i) { return (i == 0 || i == 4) ? 0.707 : 1.0; }
    static readonly double[] AutoF = { 120, 350, 6500, 11000 };
    static readonly int[] AutoType = { 1, 0, 0, 2 };
    static readonly double[] AutoQ = { 0.707, 0.7, 0.8, 0.707 };

    public static double AutoResp(MasterSettings s, MasterAnalysis an, double f)
    {
        if (an == null || !an.HasBands) return 0;
        double sum = 0;
        for (int i = 0; i < 4; i++)
            if (Math.Abs(an.Auto[i]) > 0.05) sum += BiquadDb(AutoType[i], AutoF[i], an.Auto[i] * s.AutoEq / 100.0, AutoQ[i], f);
        return sum;
    }

    public static double CustomResp(MasterSettings s, double f)
    {
        if (!s.Custom) return 0;
        double sum = 0;
        for (int i = 0; i < 5; i++)
            if (Math.Abs(s.NG[i]) > 0.05) sum += BiquadDb(NodeType(i), s.NF[i], s.NG[i], NodeQ(i), f);
        return sum;
    }

    // ---- ffmpeg filter pieces ----
    static string Shelf(string kind, double f, double g) { return kind + "=f=" + N(f) + ":t=q:w=0.707:g=" + N(g); }
    static string Peak(double f, double g, double q) { return "equalizer=f=" + N(f) + ":t=q:w=" + N(q) + ":g=" + N(g); }

    public static List<string> EqFilters(MasterSettings s, MasterAnalysis an)
    {
        var l = new List<string>();
        if (s.Custom)
            for (int i = 0; i < 5; i++)
            {
                if (Math.Abs(s.NG[i]) < 0.1) continue;
                if (i == 0) l.Add(Shelf("lowshelf", s.NF[i], s.NG[i]));
                else if (i == 4) l.Add(Shelf("highshelf", s.NF[i], s.NG[i]));
                else l.Add(Peak(s.NF[i], s.NG[i], 1.0));
            }
        if (an != null && an.HasBands && s.AutoEq > 0.5)
            for (int i = 0; i < 4; i++)
            {
                double g = an.Auto[i] * s.AutoEq / 100.0;
                if (Math.Abs(g) < 0.1) continue;
                if (i == 0) l.Add(Shelf("lowshelf", AutoF[i], g));
                else if (i == 3) l.Add(Shelf("highshelf", AutoF[i], g));
                else l.Add(Peak(AutoF[i], g, AutoQ[i]));
            }
        return l;
    }

    static string Comp(double thrDb, double ratio, double att, double rel, double makeup)
    {
        return "acompressor=threshold=" + N(Math.Pow(10, thrDb / 20)) + ":ratio=" + N(ratio)
            + ":attack=" + N(att) + ":release=" + N(rel) + ":makeup=" + N(makeup);
    }

    public static List<string> CharFilters(int c)
    {
        var l = new List<string>();
        if (c == 0) l.Add(Comp(-18, 1.8, 30, 300, 1.2));
        else if (c == 1) { l.Add("volume=2dB"); l.Add("asoftclip=type=tanh:threshold=0.9"); l.Add("volume=-2dB"); l.Add(Comp(-16, 1.5, 30, 250, 1.1)); }
        else if (c == 2) l.Add(Comp(-20, 3.0, 60, 120, 2.0));
        else l.Add(Comp(-16, 1.3, 40, 400, 1.0));
        return l;
    }

    public static string RunCapture(string exe, string args, Action<Process> reg, out int code)
    {
        var sb = new StringBuilder();
        var psi = new ProcessStartInfo(exe, args)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        var p = new Process { StartInfo = psi };
        DataReceivedEventHandler h = (s, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
        p.OutputDataReceived += h; p.ErrorDataReceived += h;
        p.Start();
        if (reg != null) reg(p);
        p.BeginOutputReadLine(); p.BeginErrorReadLine();
        p.WaitForExit();
        p.WaitForExit();
        if (reg != null) reg(null);
        code = p.ExitCode;
        lock (sb) return sb.ToString();
    }

    static double LastNum(string text, string pattern)
    {
        double v = double.NaN;
        foreach (Match m in Regex.Matches(text, pattern))
        {
            double d;
            if (double.TryParse(m.Groups[1].Value, NumberStyles.Float, CI, out d)) v = d;
        }
        return v;
    }

    static double Mean(string ff, string file, string filter, Action<Process> reg)
    {
        int code;
        string o = RunCapture(ff, "-hide_banner -nostats -t 120 -i \"" + file + "\" -vn -af \"" + filter + ",volumedetect\" -f null -", reg, out code);
        return LastNum(o, @"mean_volume:\s*(-?[\d.]+)\s*dB");
    }

    // Loudness numbers, (optionally) band balance for auto-EQ, and (optionally) stereo correlation.
    public static MasterAnalysis Analyze(string file, bool bands, bool corr, Action<Process> reg)
    {
        var an = new MasterAnalysis();
        string ff = StudioEnv.Ffmpeg();
        if (ff == null || !File.Exists(file)) return an;
        int code;
        string o = RunCapture(ff, "-hide_banner -nostats -t 180 -i \"" + file + "\" -vn -af ebur128=peak=true:framelog=info -f null -", reg, out code);
        if (code != 0) return an;
        an.I = LastNum(o, @"\n\s*I:\s+(-?[\d.]+)\s+LUFS");
        an.LRA = LastNum(o, @"LRA:\s+([\d.]+)\s+LU");
        an.TP = LastNum(o, @"Peak:\s+(-?[\d.]+)\s+dBFS");
        double mx = double.NaN, sx = double.NaN;
        foreach (Match m in Regex.Matches(o, @"M:\s*(-?[\d.]+)\s+S:\s*(-?[\d.]+)"))
        {
            double a, b;
            if (double.TryParse(m.Groups[1].Value, NumberStyles.Float, CI, out a) && a > -70 && (double.IsNaN(mx) || a > mx)) mx = a;
            if (double.TryParse(m.Groups[2].Value, NumberStyles.Float, CI, out b) && b > -70 && (double.IsNaN(sx) || b > sx)) sx = b;
        }
        an.M = mx; an.S = sx;
        an.Ok = !double.IsNaN(an.I);

        if (bands && an.Ok)
        {
            double low = Mean(ff, file, "lowpass=f=180", reg);
            double lm = Mean(ff, file, "highpass=f=180,lowpass=f=700", reg);
            double mid = Mean(ff, file, "highpass=f=700,lowpass=f=4000", reg);
            double hi = Mean(ff, file, "highpass=f=4000,lowpass=f=10000", reg);
            double air = Mean(ff, file, "highpass=f=10000", reg);
            if (!double.IsNaN(low + lm + mid + hi + air))
            {
                // Heuristic tonal target relative to the mids; corrections are half-strength,
                // capped at +/-3 dB, with a dead zone so well-balanced tracks are left alone.
                double[] rel = { low - mid, lm - mid, hi - mid, air - mid };
                double[] tgt = { 4, 3, -7, -17 };
                for (int i = 0; i < 4; i++)
                {
                    double d = rel[i] - tgt[i];
                    an.Auto[i] = Math.Abs(d) < 1.5 ? 0 : Math.Max(-3, Math.Min(3, -0.5 * d));
                }
                an.HasBands = true;
            }
        }
        if (corr && an.Ok)
        {
            string c = RunCapture(ff, "-hide_banner -nostats -t 120 -i \"" + file + "\" -vn -af \"aphasemeter=video=0,ametadata=print:key=lavfi.aphasemeter.phase\" -f null -", reg, out code);
            double sum = 0; int n = 0;
            foreach (Match m in Regex.Matches(c, @"lavfi\.aphasemeter\.phase=(-?[\d.]+)"))
            {
                double d;
                if (double.TryParse(m.Groups[1].Value, NumberStyles.Float, CI, out d)) { sum += d; n++; }
            }
            if (n > 0) an.Corr = sum / n;
        }
        return an;
    }

    // Full mastering pass on one file. Returns the output path or null.
    public static string Master(string input, string outDir, MasterSettings s, int fmt, int srIdx, string tag,
        Action<string> log, Action<Process> reg, Func<bool> cancelled, Action<float> prog, out MasterAnalysis result)
    {
        result = null;
        string ff = StudioEnv.Ffmpeg(), fp = StudioEnv.Ffprobe();
        if (ff == null) { log("ffmpeg not found."); return null; }
        int code;

        // source sample rate / channel count
        int srcRate = 44100, ch = 2;
        if (fp != null)
        {
            string pr = RunCapture(fp, "-v error -select_streams a:0 -show_entries stream=sample_rate,channels -of csv=p=0 \"" + input + "\"", reg, out code);
            var m = Regex.Match(pr, @"(\d+),(\d+)");
            if (m.Success) { srcRate = int.Parse(m.Groups[1].Value); ch = int.Parse(m.Groups[2].Value); }
        }
        int outRate = srIdx == 1 ? 44100 : (srIdx == 2 ? 48000 : srcRate);
        if (FmtExt[fmt] == "opus") outRate = 48000;

        prog(0.05f);
        log("Analyzing...");
        var an = Analyze(input, true, false, reg);
        if (cancelled()) return null;
        if (!an.Ok) { log("Analysis failed; mastering skipped."); return null; }
        log("Input: " + N(an.I) + " LUFS, true peak " + N(an.TP) + " dB, LRA " + N(an.LRA) + " LU");
        if (an.HasBands && s.AutoEq > 0.5)
            log("Auto EQ (" + N(s.AutoEq) + "%): low " + N(an.Auto[0]) + " / low-mid " + N(an.Auto[1])
                + " / high " + N(an.Auto[2]) + " / air " + N(an.Auto[3]) + " dB");
        prog(0.25f);

        // chain before loudness: low cut, EQ, character, exciter, width
        var pre = new List<string>();
        pre.Add("highpass=f=25");
        pre.AddRange(EqFilters(s, an));
        pre.AddRange(CharFilters(s.Character));
        if (s.Excite) pre.Add("aexciter=amount=1.5:drive=8.5:freq=7500:ceil=16000:blend=0");
        if (ch == 2 && Math.Abs(s.Width - 100) > 1) pre.Add("extrastereo=m=" + N(s.Width / 100.0));
        string preChain = string.Join(",", pre.ToArray());

        double target = s.Comp && !double.IsNaN(an.I) ? Math.Max(-30, Math.Min(-5, an.I)) : s.Loud;
        string ln = "loudnorm=I=" + N(target) + ":TP=-1:LRA=11";

        // pass 1: measure the processed signal so pass 2 can apply a precise linear gain
        string m1 = RunCapture(ff, "-hide_banner -nostats -i \"" + input + "\" -vn -af \"" + preChain + "," + ln
            + ":print_format=json\" -f null -", reg, out code);
        if (cancelled()) return null;
        if (code != 0) { log("Mastering chain failed:\n" + Tail(m1)); return null; }
        string lnFinal = ln;
        double mI = Json(m1, "input_i"), mTP = Json(m1, "input_tp"), mLRA = Json(m1, "input_lra"),
               mTh = Json(m1, "input_thresh"), mOff = Json(m1, "target_offset");
        if (!double.IsNaN(mI + mTP + mLRA + mTh + mOff))
            lnFinal += ":measured_I=" + N(mI) + ":measured_TP=" + N(mTP) + ":measured_LRA=" + N(mLRA)
                + ":measured_thresh=" + N(mTh) + ":offset=" + N(mOff) + ":linear=true";
        prog(0.55f);

        string name = Path.GetFileNameWithoutExtension(input);
        string output = Path.Combine(outDir, name + " [" + tag + "]." + FmtExt[fmt]);
        string chain = preChain + "," + lnFinal + ",alimiter=limit=0.891:attack=5:release=50:level=0,aresample=" + outRate;
        string m2 = RunCapture(ff, "-hide_banner -nostats -y -i \"" + input + "\" -vn -af \"" + chain + "\" "
            + FmtEnc[fmt] + " -ar " + outRate + " -map_metadata 0 \"" + output + "\"", reg, out code);
        if (cancelled()) return null;
        if (code != 0 || !File.Exists(output)) { log("Encoding failed:\n" + Tail(m2)); return null; }
        prog(0.85f);

        var after = Analyze(output, false, true, reg);
        after.Auto = an.Auto; after.HasBands = an.HasBands;
        if (after.Ok) log("Output: " + N(after.I) + " LUFS, true peak " + N(after.TP) + " dB, LRA " + N(after.LRA) + " LU");
        result = after;
        prog(1f);
        return output;
    }

    static double Json(string text, string key)
    {
        var m = Regex.Match(text, "\"" + key + "\"\\s*:\\s*\"(-?[\\d.]+|-inf|inf)\"");
        double d;
        if (m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CI, out d)) return d;
        return double.NaN;
    }

    static string Tail(string s)
    {
        var lines = s.Split('\n');
        var sb = new StringBuilder();
        for (int i = Math.Max(0, lines.Length - 6); i < lines.Length; i++) sb.AppendLine(lines[i].TrimEnd());
        return sb.ToString();
    }
}

