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
        string ff = App.FfmpegExe();
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
        string ff = App.FfmpegExe(), fp = App.FfprobeExe();
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

// ======================================================================
//  The window. Designed in a 1400x880 space and scaled by the View zoom.
// ======================================================================
class MasterForm : Form
{
    App app;
    MasterSettings ms { get { return app.Master; } }
    float z = 0.75f;
    System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    float t = 0;
    Random rnd = new Random();
    List<Leaf> leaves = new List<Leaf>();
    List<Spore> spores = new List<Spore>();

    // history / A-B
    List<string> undo = new List<string>(), redo = new List<string>();
    bool comparing = false; string cmpHold = "";
    string presetName = "Manual";
    int selNode = 2;

    // drag state
    int drag = 0; float dragY, dragVal; string dragBefore = "";
    volatile bool analyzing = false; string analyzeText = "";

    // ---- purple theme (from the main window) ----
    static Color Accent = Color.FromArgb(190, 110, 255);
    static Color Accent2 = Color.FromArgb(235, 90, 230);
    static Color AccentHi = Color.FromArgb(232, 190, 255);
    static Color Violet = Color.FromArgb(124, 52, 214);
    static Color PanelC = Color.FromArgb(205, 28, 11, 54);
    static Color PanelEdge = Color.FromArgb(120, 150, 80, 220);
    internal static void ApplyTheme(ThemeDef t) { Accent = t.Accent; Accent2 = t.Accent2; AccentHi = Color.FromArgb(Math.Min(255, t.Accent.R + 54), Math.Min(255, t.Accent.G + 54), Math.Min(255, t.Accent.B + 54)); Violet = t.Btn; PanelC = Color.FromArgb(205, t.Panel.R + 8, t.Panel.G + 4, t.Panel.B + 14); PanelEdge = Color.FromArgb(120, t.EdgeC); }
    static readonly Color Dim = Color.FromArgb(190, 175, 150, 215);
    static readonly Color White = Color.FromArgb(244, 236, 255);
    static readonly Color Chrome = Color.FromArgb(24, 10, 44);
    static readonly Color Pink = Color.FromArgb(255, 120, 200);

    // ---- layout (design space) ----
    static readonly RectangleF PresetR = new RectangleF(92, 44, 368, 28);
    static readonly RectangleF NavL = new RectangleF(92, 84, 45, 28), NavR = new RectangleF(137, 84, 45, 28);
    static readonly RectangleF CompareR = new RectangleF(197, 84, 94, 28);
    static readonly RectangleF CopyR = new RectangleF(304, 84, 76, 28), PasteR = new RectangleF(380, 84, 80, 28);
    static readonly RectangleF UndoR = new RectangleF(474, 84, 77, 28), RedoR = new RectangleF(551, 84, 78, 28);
    static readonly RectangleF ViewR = new RectangleF(1230, 82, 88, 30);
    static readonly RectangleF CharR = new RectangleF(16, 138, 186, 78);
    static readonly RectangleF Plot = new RectangleF(147, 272, 601, 400);
    static readonly RectangleF CustomR = new RectangleF(16, 775, 97, 43);
    static readonly RectangleF DynR = new RectangleF(826, 138, 558, 382);
    static readonly RectangleF ExciteR = new RectangleF(891, 366, 60, 42);
    static readonly RectangleF StartR = new RectangleF(1244, 449, 60, 42), ResetR = new RectangleF(1310, 449, 60, 42);
    static readonly RectangleF SpreadR = new RectangleF(826, 531, 558, 230);
    static readonly RectangleF ReanR = new RectangleF(826, 775, 130, 43);
    static readonly RectangleF BypassR = new RectangleF(1106, 775, 135, 43), LoudR = new RectangleF(1248, 775, 136, 43);
    const float SlX = 70, SlY1 = 262, SlY2 = 680;
    static readonly PointF KnobL = new PointF(920, 285), KnobW = new PointF(920, 678);
    const float KnobR = 52;

    // presets
    static readonly string[] PresetNames = {
        "Streaming  (-14 LUFS)", "Loud / Club  (-9 LUFS)", "Gentle glue  (-16 LUFS)", "Warm tape",
        "Bright & airy", "Vocal forward", "Wide & punchy" };
    static readonly string[] PresetData = {
        "on=1;char=3;auto=100;custom=0;loud=-14;excite=0;width=100;comp=0",
        "on=1;char=2;auto=70;custom=0;loud=-9;excite=1;width=100;comp=0",
        "on=1;char=0;auto=60;custom=0;loud=-16;excite=0;width=100;comp=0",
        "on=1;char=1;auto=80;custom=1;loud=-14;excite=0;width=100;comp=0;nodes=80:2,250:1,1000:0,4000:-1,10000:-2",
        "on=1;char=3;auto=100;custom=1;loud=-14;excite=1;width=100;comp=0;nodes=80:0,250:0,1000:0,4000:2,10000:4",
        "on=1;char=0;auto=80;custom=1;loud=-14;excite=0;width=100;comp=0;nodes=80:-1,250:-1,1000:1,4000:3,10000:1",
        "on=1;char=2;auto=80;custom=0;loud=-12;excite=0;width=130;comp=0" };

    public MasterForm(App owner)
    {
        app = owner;
        z = Math.Max(0.4f, Math.Min(1.0f, app.MZoomPct / 100f));
        Text = "Purphex Mastering"; Win.Dark(this);
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        ApplyZoom();
        for (int i = 0; i < 10; i++) leaves.Add(NewLeaf(true));
        for (int i = 0; i < 30; i++) spores.Add(NewSpore(true));
        timer.Interval = 33; timer.Tick += (s, e) => { if (moving || Win.AnyMoving) return; if (ActiveForm != this && (++idleTick & 3) != 0) return; t += 0.033f; StepAnim(); Invalidate(); };
        timer.Start();
        FormClosed += (s, e) => { timer.Stop(); timer.Dispose(); if (ms.On) { ms.On = false; app.SaveSettings(); } };   // closing the Mastering window turns mastering off
    }

    int idleTick; volatile bool moving;
    protected override void WndProc(ref Message m)
    {
        Win.TrackMove(ref m); if (m.Msg == 0x231) moving = true; else if (m.Msg == 0x232) moving = false;
        base.WndProc(ref m);
    }

    void ApplyZoom() { ClientSize = new Size((int)(1400 * z), (int)(880 * z)); }

    Leaf NewLeaf(bool any)
    {
        Color[] pal = { Color.FromArgb(170, 90, 255), Color.FromArgb(140, 60, 230), Color.FromArgb(195, 125, 255), Color.FromArgb(120, 50, 200) };
        var l = new Leaf();
        l.S = 0.5f + (float)rnd.NextDouble() * 1.2f;
        l.X = (float)rnd.NextDouble() * 1400;
        l.Y = any ? (float)rnd.NextDouble() * 880 : -200;
        l.Rot = (float)rnd.NextDouble() * 360; l.Spin = ((float)rnd.NextDouble() - 0.5f) * 0.8f;
        l.VY = 0.4f + l.S * 0.7f; l.Phase = (float)rnd.NextDouble() * 6.28f; l.Sway = 0.3f + (float)rnd.NextDouble() * 0.6f;
        l.A = 22 + (int)(l.S * 22); l.C = pal[rnd.Next(pal.Length)];
        return l;
    }

    Spore NewSpore(bool any)
    {
        var s = new Spore();
        s.X = (float)rnd.NextDouble() * 1400; s.Y = any ? (float)rnd.NextDouble() * 880 : 885;
        s.R = 1.5f + (float)rnd.NextDouble() * 3f; s.VY = 0.3f + (float)rnd.NextDouble();
        s.Phase = (float)rnd.NextDouble() * 6.28f;
        return s;
    }

    void StepAnim()
    {
        foreach (var l in leaves)
        {
            l.Y += l.VY; l.X += (float)Math.Sin(t * 0.8f + l.Phase) * l.Sway; l.Rot += l.Spin;
            if (l.Y > 1100) { var n = NewLeaf(false); l.X = n.X; l.Y = n.Y; l.S = n.S; l.VY = n.VY; l.A = n.A; l.C = n.C; }
        }
        foreach (var s in spores)
        {
            s.Y -= s.VY; s.X += (float)Math.Sin(t + s.Phase) * 0.5f;
            if (s.Y < -5) { var n = NewSpore(false); s.X = n.X; s.Y = n.Y; }
        }
    }

    // ---------- coordinate helpers ----------
    static float FX(double f) { return Plot.X + Plot.Width * (float)(Math.Log10(f / 20.0) / 3.0); }
    static float GY(double db) { return Plot.Y + Plot.Height * (float)((12 - db) / 24.0); }
    static double FromX(float x) { return 20.0 * Math.Pow(10, (x - Plot.X) / Plot.Width * 3.0); }
    static double FromY(float y) { return 12 - (y - Plot.Y) / Plot.Height * 24.0; }

    // ---------- drawing helpers ----------
    static GraphicsPath RR(RectangleF r, float rad)
    {
        var p = new GraphicsPath(); float d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }

    static void FillRR(Graphics g, Color c, RectangleF r, float rad)
    {
        using (var p = RR(r, rad)) using (var b = new SolidBrush(c)) g.FillPath(b, p);
    }

    static void StrokeRR(Graphics g, Color c, RectangleF r, float rad, float w)
    {
        using (var p = RR(r, rad)) using (var pen = new Pen(c, w)) g.DrawPath(pen, p);
    }

    static void Txt(Graphics g, string s, float px, Color c, RectangleF r, StringAlignment ha, bool bold)
    {
        using (var b = new SolidBrush(c)) g.DrawString(s, GfxCache.Font(px, bold), b, r, GfxCache.Fmt(ha, false));
    }

    static RectangleF At(float cx, float cy, float w, float h) { return new RectangleF(cx - w / 2, cy - h / 2, w, h); }

    void Btn(Graphics g, RectangleF r, string text, bool on, Color onColor, Color textColor)
    {
        text = Win.Up(text);
        FillRR(g, on ? onColor : Color.FromArgb(150, 52, 28, 92), r, 5);
        Txt(g, text, 17, textColor, r, StringAlignment.Center, false);
    }

    void Knob(Graphics g, PointF c, float val01, bool centerRef)
    {
        var ring = new RectangleF(c.X - KnobR - 9, c.Y - KnobR - 9, (KnobR + 9) * 2, (KnobR + 9) * 2);
        using (var pen = new Pen(Color.FromArgb(120, 60, 30, 100), 7))
            g.DrawArc(pen, ring, 135, 270);
        float a0 = centerRef ? 270 : 135, a1 = 135 + 270 * val01;
        float sweep = a1 - a0;
        if (Math.Abs(sweep) > 0.5f)
        {
            using (var glow = new Pen(Color.FromArgb(70, Accent), 13)) g.DrawArc(glow, ring, a0, sweep);
            using (var pen = new Pen(Accent, 7)) g.DrawArc(pen, ring, a0, sweep);
        }
        var body = new RectangleF(c.X - KnobR, c.Y - KnobR, KnobR * 2, KnobR * 2);
        using (var br = new LinearGradientBrush(body, Color.FromArgb(62, 30, 104), Color.FromArgb(30, 12, 58), 90f))
            g.FillEllipse(br, body);
        using (var pen = new Pen(Color.FromArgb(150, 130, 80, 200), 2)) g.DrawEllipse(pen, body);
        double ang = (135 + 270 * val01) * Math.PI / 180.0;
        using (var pen = new Pen(AccentHi, 4) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(pen, c.X + (float)Math.Cos(ang) * KnobR * 0.35f, c.Y + (float)Math.Sin(ang) * KnobR * 0.35f,
                c.X + (float)Math.Cos(ang) * KnobR * 0.88f, c.Y + (float)Math.Sin(ang) * KnobR * 0.88f);
    }

    void BarMeter(Graphics g, float x, float y, float w, float h, double v, double lo, double hi)
    {
        FillRR(g, Color.FromArgb(150, 12, 4, 30), new RectangleF(x, y, w, h), h / 2);
        if (double.IsNaN(v)) return;
        float fw = (float)(w * Math.Max(0, Math.Min(1, (v - lo) / (hi - lo))));
        if (fw < 2) return;
        var r = new RectangleF(x, y, fw, h);
        using (var br = new LinearGradientBrush(new RectangleF(x, y, w, h), Violet, Accent2, 0f))
        using (var p = RR(r, h / 2)) g.FillPath(br, p);
    }

    static string Num(double v, string fmtStr) { return double.IsNaN(v) ? "-" : v.ToString(fmtStr, CultureInfo.InvariantCulture); }

    // ---------- main paint ----------
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.ScaleTransform(z, z);

        // background: the main window's deep-purple gradient, glows, leaves and spores
        var full = new RectangleF(0, 0, 1400, 880);
        using (var br = new LinearGradientBrush(full, Color.FromArgb(14, 5, 30), Color.FromArgb(48, 12, 86), 70f))
            g.FillRectangle(br, full);
        for (int i = 0; i < 2; i++)
        {
            float cx = 1400 * (0.3f + 0.4f * i) + (float)Math.Sin(t * 0.4f + i * 2) * 90;
            float cy = 880 * (0.35f + 0.3f * i) + (float)Math.Cos(t * 0.3f + i) * 70;
            float rad = 330 + (float)Math.Sin(t * 0.9f + i) * 40;
            var gb = Glow(); var o1 = g.CompositingQuality; var o2 = g.PixelOffsetMode; var o3 = g.SmoothingMode; var o4 = g.InterpolationMode;
            g.CompositingQuality = CompositingQuality.HighSpeed; g.PixelOffsetMode = PixelOffsetMode.HighSpeed; g.SmoothingMode = SmoothingMode.HighSpeed; g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.DrawImage(gb, new RectangleF(cx - rad, cy - rad, rad * 2, rad * 2), new RectangleF(0, 0, gb.Width, gb.Height), GraphicsUnit.Pixel);
            g.CompositingQuality = o1; g.PixelOffsetMode = o2; g.SmoothingMode = o3; g.InterpolationMode = o4;
        }
        leaves.Sort((a, b) => a.S.CompareTo(b.S));
        foreach (var l in leaves) App.DrawLeaf(g, l);
        foreach (var s in spores)
        {
            int a = (int)(60 + 90 * (Math.Sin(t * 2 + s.Phase) * 0.5 + 0.5));
            using (var b = new SolidBrush(Color.FromArgb(a, 215, 170, 255))) g.FillEllipse(b, s.X, s.Y, s.R * 2, s.R * 2);
        }

        PaintTopBar(g);
        PaintLeft(g);
        PaintEq(g);
        PaintDynamics(g);
        PaintSpread(g);
        PaintBottom(g);
    }

    static Bitmap glowBmp;
    static Bitmap Glow()
    {
        if (glowBmp != null) return glowBmp;
        var bmp = new Bitmap(256, 256, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using (var bg = Graphics.FromImage(bmp)) using (var gp = new GraphicsPath())
        {
            bg.SmoothingMode = SmoothingMode.AntiAlias; gp.AddEllipse(0, 0, 256, 256);
            using (var pg = new PathGradientBrush(gp)) { pg.CenterColor = Color.FromArgb(70, 150, 60, 240); pg.SurroundColors = new[] { Color.FromArgb(0, 150, 60, 240) }; bg.FillPath(pg, gp); }
        }
        glowBmp = bmp; return bmp;
    }

    void PaintTopBar(Graphics g)
    {
        using (var br = new SolidBrush(Color.FromArgb(215, Chrome))) g.FillRectangle(br, 0, 0, 1400, 125);
        using (var pen = new Pen(Color.FromArgb(140, 150, 80, 220))) g.DrawLine(pen, 0, 125, 1400, 125);
        Txt(g, "PURPHEX MASTERING", 22, White, new RectangleF(400, 6, 600, 30), StringAlignment.Center, true);

        // power button (pulses while on)
        var pc = At(49, 78, 52, 52);
        if (ms.On)
        {
            float pulse = (float)(Math.Sin(t * 3) * 0.5 + 0.5);
            using (var gl = new SolidBrush(Color.FromArgb((int)(40 + 50 * pulse), Accent))) g.FillEllipse(gl, At(49, 78, 70, 70));
        }
        using (var br = new SolidBrush(ms.On ? Violet : Color.FromArgb(70, 55, 90))) g.FillEllipse(br, pc);
        using (var pen = new Pen(Color.White, 3.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
        {
            g.DrawArc(pen, 49 - 12, 78 - 12, 24, 24, -55, 290);
            g.DrawLine(pen, 49, 78 - 16, 49, 78);
        }

        FillRR(g, Color.FromArgb(160, 52, 28, 92), PresetR, 5);
        Txt(g, presetName, 18, Dim, new RectangleF(PresetR.X + 12, PresetR.Y, 300, PresetR.Height), StringAlignment.Near, false);
        using (var pen = new Pen(Dim, 2)) { g.DrawLine(pen, PresetR.Right - 24, 55, PresetR.Right - 18, 61); g.DrawLine(pen, PresetR.Right - 18, 61, PresetR.Right - 12, 55); }

        FillRR(g, Color.FromArgb(150, 52, 28, 92), new RectangleF(NavL.X, NavL.Y, 90, 28), 5);
        using (var pen = new Pen(White, 2.2f))
        {
            g.DrawLine(pen, NavL.X + 26, NavL.Y + 8, NavL.X + 18, NavL.Y + 14); g.DrawLine(pen, NavL.X + 18, NavL.Y + 14, NavL.X + 26, NavL.Y + 20);
            g.DrawLine(pen, NavR.X + 19, NavR.Y + 8, NavR.X + 27, NavR.Y + 14); g.DrawLine(pen, NavR.X + 27, NavR.Y + 14, NavR.X + 19, NavR.Y + 20);
        }
        Btn(g, CompareR, "Compare", comparing, Violet, White);
        Btn(g, CopyR, "Copy", false, Violet, White); Btn(g, PasteR, "Paste", false, Violet, Dim);
        Btn(g, UndoR, "Undo", false, Violet, undo.Count > 0 ? White : Dim);
        Btn(g, RedoR, "Redo", false, Violet, redo.Count > 0 ? White : Dim);

        Txt(g, "View:", 18, Dim, new RectangleF(1150, 82, 74, 30), StringAlignment.Far, false);
        FillRR(g, Color.FromArgb(150, 52, 28, 92), ViewR, 5);
        Txt(g, app.MZoomPct + "%", 18, White, ViewR, StringAlignment.Center, false);
    }

    void Panel(Graphics g, RectangleF r)
    {
        FillRR(g, PanelC, r, 10);
        StrokeRR(g, PanelEdge, r, 10, 1.2f);
    }

    void PaintLeft(Graphics g)
    {
        Panel(g, CharR);
        Txt(g, "CHARACTER", 16, Dim, new RectangleF(CharR.X + 22, CharR.Y + 14, 150, 22), StringAlignment.Near, false);
        Txt(g, MasterSettings.CharNames[ms.Character], 21, Accent, new RectangleF(CharR.X + 22, CharR.Y + 38, 140, 32), StringAlignment.Near, true);
        using (var pen = new Pen(Accent, 2))
        {
            g.DrawLine(pen, CharR.Right - 26, CharR.Y + 44, CharR.Right - 20, CharR.Y + 38); g.DrawLine(pen, CharR.Right - 20, CharR.Y + 38, CharR.Right - 14, CharR.Y + 44);
            g.DrawLine(pen, CharR.Right - 26, CharR.Y + 52, CharR.Right - 20, CharR.Y + 58); g.DrawLine(pen, CharR.Right - 20, CharR.Y + 58, CharR.Right - 14, CharR.Y + 52);
        }

        // Auto EQ vertical slider
        FillRR(g, Color.FromArgb(150, 12, 4, 30), new RectangleF(SlX - 3, SlY1, 6, SlY2 - SlY1), 3);
        float hy = SlY2 - (SlY2 - SlY1) * ms.AutoEq / 100f;
        FillRR(g, Accent, new RectangleF(SlX - 3, hy, 6, SlY2 - hy), 3);
        using (var pen = new Pen(Color.FromArgb(90, Dim), 1.5f))
            for (int i = 0; i < 5; i++) { float ty = SlY1 + (SlY2 - SlY1) * i / 4f; g.DrawLine(pen, SlX + 14, ty, SlX + 24, ty); }
        var hr = new RectangleF(SlX - 19, hy - 7, 38, 14);
        using (var gl = new SolidBrush(Color.FromArgb(60, Accent))) g.FillEllipse(gl, At(SlX, hy, 56, 30));
        FillRR(g, AccentHi, hr, 6);
        Txt(g, "AUTO EQ", 17, Dim, At(SlX, 711, 110, 24), StringAlignment.Center, false);
        Txt(g, ((int)ms.AutoEq) + " %", 22, Accent, At(SlX + 2, 735, 110, 28), StringAlignment.Center, false);

        // Custom EQ toggle
        Btn(g, CustomR, "Custom EQ", ms.Custom, Violet, White);
    }

    void PaintEq(Graphics g)
    {
        // grid
        double[] decades = { 20, 30, 40, 50, 60, 70, 80, 90, 100, 200, 300, 400, 500, 600, 700, 800, 900, 1000,
            2000, 3000, 4000, 5000, 6000, 7000, 8000, 9000, 10000, 20000 };
        using (var pen = new Pen(Color.FromArgb(34, 190, 140, 255), 1))
            foreach (var f in decades) { float x = FX(f); g.DrawLine(pen, x, Plot.Y - 16, x, Plot.Bottom + 4); }
        using (var pen = new Pen(Color.FromArgb(120, Dim), 1.4f) { DashStyle = DashStyle.Dot })
            g.DrawLine(pen, Plot.X, GY(0), Plot.Right + 10, GY(0));
        double[] lab = { 20, 50, 100, 300, 700, 2000, 5000, 10000 };
        string[] labT = { "20", "50", "100", "300", "700", "2k", "5k", "10k" };
        for (int i = 0; i < lab.Length; i++)
            Txt(g, labT[i], 17, Dim, At(FX(lab[i]), GY(0) + 20, 50, 22), StringAlignment.Center, false);
        int[] dbl = { 12, 6, 0, -6, -12 };
        foreach (var d in dbl) Txt(g, d.ToString(), 17, Color.FromArgb(150, Dim), new RectangleF(Plot.Right + 20, GY(d) - 11, 50, 22), StringAlignment.Near, false);

        // curves
        int n = 260;
        var total = new PointF[n]; var auto = new PointF[n];
        for (int i = 0; i < n; i++)
        {
            double f = 20 * Math.Pow(1000, i / (double)(n - 1));
            double a = MasterDsp.AutoResp(ms, app.LastAn, f), c = MasterDsp.CustomResp(ms, f);
            total[i] = new PointF(FX(f), GY(Math.Max(-12, Math.Min(12, a + c))));
            auto[i] = new PointF(FX(f), GY(Math.Max(-12, Math.Min(12, a))));
        }
        float pulse = (float)(Math.Sin(t * 2.2) * 0.5 + 0.5);
        using (var pen = new Pen(Color.FromArgb((int)(40 + 40 * pulse), Accent), 11) { LineJoin = LineJoin.Round }) g.DrawLines(pen, total);
        using (var pen = new Pen(Accent, 3.2f) { LineJoin = LineJoin.Round }) g.DrawLines(pen, total);
        if (app.LastAn != null && app.LastAn.HasBands)
            using (var pen = new Pen(Color.FromArgb(190, AccentHi), 2) { DashStyle = DashStyle.Dot }) g.DrawLines(pen, auto);

        // nodes
        for (int i = 0; i < 5; i++)
        {
            float x = FX(ms.NF[i]), y = GY(ms.NG[i]);
            bool sel = i == selNode;
            if (sel) using (var gl = new SolidBrush(Color.FromArgb(70, Accent2))) g.FillEllipse(gl, At(x, y, 46, 46));
            using (var br = new SolidBrush(sel ? Accent2 : (ms.Custom ? Color.FromArgb(150, 90, 200) : Color.FromArgb(120, 70, 50, 110))))
                g.FillEllipse(br, At(x, y, sel ? 24 : 18, sel ? 24 : 18));
            using (var pen = new Pen(Color.FromArgb(200, AccentHi), 1.6f)) g.DrawEllipse(pen, At(x, y, sel ? 24 : 18, sel ? 24 : 18));
        }

        string nm = MasterSettings.NodeNames[selNode];
        Txt(g, nm + " Freq", 17, Dim, At(413, 785, 140, 24), StringAlignment.Center, false);
        Txt(g, ms.NF[selNode].ToString("0.0", CultureInfo.InvariantCulture) + " Hz", 23, Accent, At(413, 807, 160, 28), StringAlignment.Center, false);
        Txt(g, nm + " Gain", 17, Dim, At(552, 785, 140, 24), StringAlignment.Center, false);
        Txt(g, ms.NG[selNode].ToString("0.0", CultureInfo.InvariantCulture) + " dB", 23, Accent, At(552, 807, 160, 28), StringAlignment.Center, false);
    }

    void PaintDynamics(Graphics g)
    {
        Panel(g, DynR);
        Txt(g, "DYNAMICS", 24, White, new RectangleF(853, 156, 200, 32), StringAlignment.Near, true);
        Txt(g, ms.Comp ? "Matched" : "Loudness", 17, Dim, At(920, 212, 120, 24), StringAlignment.Center, false);
        Knob(g, KnobL, (ms.Loud + 20f) / 14f, false);
        Txt(g, ms.Comp ? "= input" : Num(ms.Loud, "0.0") + " LUFS", 16, Accent, At(920, 346, 130, 22), StringAlignment.Center, false);
        Btn(g, ExciteR, "Excite", ms.Excite, Violet, ms.Excite ? White : AccentHi);

        var an = app.LastAn;
        // true peak
        Txt(g, "TRUE PEAK", 17, Dim, new RectangleF(1068, 200, 150, 24), StringAlignment.Near, false);
        double tp = an == null ? double.NaN : an.TP;
        BarMeter(g, 1068, 231, 232, 7, tp, -60, 6); BarMeter(g, 1068, 243, 232, 7, tp, -60, 6);
        Txt(g, "-60", 15, Dim, At(1068, 273, 40, 20), StringAlignment.Near, false);
        Txt(g, "0", 15, White, At(1068 + 232 * 60f / 66f, 273, 30, 20), StringAlignment.Center, false);
        Txt(g, "6", 15, Dim, At(1300, 273, 24, 20), StringAlignment.Far, false);
        FillRR(g, Color.FromArgb(150, 12, 4, 30), new RectangleF(1315, 222, 55, 32), 5);
        Txt(g, Num(tp, "0.0"), 15, !double.IsNaN(tp) && tp > -1 ? Pink : White, new RectangleF(1315, 222, 55, 32), StringAlignment.Center, false);

        // LUFS meters
        Txt(g, "LUFS", 17, Dim, new RectangleF(1068, 308, 100, 24), StringAlignment.Near, false);
        string[] ml = { "M", "S", "I" };
        double[] mv = { an == null ? double.NaN : an.M, an == null ? double.NaN : an.S, an == null ? double.NaN : an.I };
        for (int i = 0; i < 3; i++)
        {
            Txt(g, ml[i], 16, Dim, At(1046, 342 + i * 20, 20, 18), StringAlignment.Center, false);
            BarMeter(g, 1068, 338 + i * 20, 232, 9, mv[i], -60, 0);
            Txt(g, Num(mv[i], "0.0"), 14, AccentHi, new RectangleF(1306, 334 + i * 20, 60, 18), StringAlignment.Near, false);
        }
        Txt(g, "-60", 15, Dim, At(1068, 410, 40, 20), StringAlignment.Near, false);
        Txt(g, "0", 15, White, At(1300, 410, 24, 20), StringAlignment.Far, false);

        Txt(g, "LU RANGE", 17, Dim, At(1180, 460, 110, 24), StringAlignment.Center, false);
        Txt(g, an == null ? "-" : Num(an.LRA, "0.0"), 18, Accent, At(1180, 484, 110, 24), StringAlignment.Center, false);
        Btn(g, StartR, "Start", false, Violet, Accent); Btn(g, ResetR, "Reset", false, Violet, Accent);
    }

    void PaintSpread(Graphics g)
    {
        Panel(g, SpreadR);
        Txt(g, "SPREAD", 24, White, new RectangleF(853, 550, 200, 32), StringAlignment.Near, true);
        Txt(g, "WIDTH", 17, Dim, At(920, 606, 120, 24), StringAlignment.Center, false);
        Knob(g, KnobW, ms.Width / 200f, true);
        Txt(g, ((int)ms.Width) + " %", 16, Accent, At(920, 742, 100, 22), StringAlignment.Center, false);

        Txt(g, "CORRELATION", 17, Dim, new RectangleF(1068, 594, 150, 24), StringAlignment.Near, false);
        var bar = new RectangleF(1068, 639, 232, 8);
        using (var br = new LinearGradientBrush(bar, Color.FromArgb(235, 70, 130), Color.FromArgb(120, 230, 150), 0f))
        {
            var cb = new ColorBlend(3);
            cb.Colors = new[] { Color.FromArgb(235, 70, 130), Color.FromArgb(240, 210, 90), Color.FromArgb(120, 230, 150) };
            cb.Positions = new[] { 0f, 0.5f, 1f };
            br.InterpolationColors = cb;
            using (var p = RR(bar, 4)) g.FillPath(br, p);
        }
        double cv = app.LastAn == null ? double.NaN : app.LastAn.Corr;
        float mx = 1068 + 232 * (float)((double.IsNaN(cv) ? 0 : cv) + 1) / 2f;
        using (var pen = new Pen(White, 2.5f)) g.DrawLine(pen, mx, 626, mx, 654);
        Txt(g, "-1", 15, Dim, At(1068, 673, 30, 20), StringAlignment.Near, false);
        Txt(g, "0", 15, White, At(1184, 673, 24, 20), StringAlignment.Center, false);
        Txt(g, "+1", 15, Dim, At(1300, 673, 30, 20), StringAlignment.Far, false);
        Txt(g, Num(cv, "0.0"), 18, White, new RectangleF(1312, 628, 60, 28), StringAlignment.Near, false);
    }

    void PaintBottom(Graphics g)
    {
        using (var br = new SolidBrush(Color.FromArgb(215, Chrome))) g.FillRectangle(br, 0, 832, 1400, 48);
        using (var pen = new Pen(Color.FromArgb(140, 150, 80, 220))) g.DrawLine(pen, 0, 832, 1400, 832);
        string cap = analyzing ? analyzeText : "Purphex Mastering";
        Txt(g, cap, 24, analyzing ? Accent : White, new RectangleF(300, 836, 800, 40), StringAlignment.Center, false);

        FillRR(g, Color.FromArgb(150, 52, 28, 92), ReanR, 6);
        // sparkle icon
        using (var pen = new Pen(Accent2, 2.2f))
        {
            g.DrawLine(pen, ReanR.X + 20, ReanR.Y + 12, ReanR.X + 20, ReanR.Y + 30); g.DrawLine(pen, ReanR.X + 11, ReanR.Y + 21, ReanR.X + 29, ReanR.Y + 21);
            g.DrawLine(pen, ReanR.X + 28, ReanR.Y + 9, ReanR.X + 28, ReanR.Y + 15); g.DrawLine(pen, ReanR.X + 25, ReanR.Y + 12, ReanR.X + 31, ReanR.Y + 12);
        }
        Txt(g, "REANALYZE", 18, Accent, new RectangleF(ReanR.X + 38, ReanR.Y, 90, ReanR.Height), StringAlignment.Near, false);
        Btn(g, BypassR, "Bypass", !ms.On, Violet, !ms.On ? White : Accent);
        FillRR(g, ms.Comp ? Color.FromArgb(200, 200, 70, 150) : Color.FromArgb(150, 90, 30, 90), LoudR, 6);
        Txt(g, "Loudness\nCompensation", 15, ms.Comp ? White : Pink, LoudR, StringAlignment.Center, false);
    }

    // ---------- state changes ----------
    void Commit(Action change)
    {
        string before = ms.Serialize();
        change();
        if (ms.Serialize() != before)
        {
            undo.Add(before); redo.Clear(); presetName = "Manual"; app.SaveSettings();
        }
        Invalidate();
    }

    void ApplyPreset(int i)
    {
        Commit(() => { ms.Parse(PresetData[i]); });
        presetName = PresetNames[i];
    }

    int PresetIndex() { for (int i = 0; i < PresetNames.Length; i++) if (PresetNames[i] == presetName) return i; return -1; }

    static bool In(RectangleF r, PointF p) { return r.Contains(p); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left) return;
        var p = new PointF(e.X / z, e.Y / z);

        for (int i = 0; i < 5; i++)
        {
            float dx = FX(ms.NF[i]) - p.X, dy = GY(ms.NG[i]) - p.Y;
            if (dx * dx + dy * dy < 22 * 22)
            {
                selNode = i; drag = 1; dragBefore = ms.Serialize(); ms.Custom = true; Invalidate(); return;
            }
        }
        if (p.X > SlX - 28 && p.X < SlX + 28 && p.Y > SlY1 - 16 && p.Y < SlY2 + 16)
        { drag = 2; dragBefore = ms.Serialize(); MoveDrag(p); return; }
        if (Dist(p, KnobL) < KnobR + 14)
        {
            if (e.Clicks == 2) { Commit(() => ms.Loud = -14); return; }
            drag = 3; dragY = p.Y; dragVal = ms.Loud; dragBefore = ms.Serialize(); return;
        }
        if (Dist(p, KnobW) < KnobR + 14)
        {
            if (e.Clicks == 2) { Commit(() => ms.Width = 100); return; }
            drag = 4; dragY = p.Y; dragVal = ms.Width; dragBefore = ms.Serialize(); return;
        }

        if (Dist(p, new PointF(49, 78)) < 28 || In(BypassR, p)) { Commit(() => ms.On = !ms.On); return; }
        if (In(CharR, p)) { CharMenu(e.Location); return; }
        if (In(CustomR, p)) { Commit(() => ms.Custom = !ms.Custom); return; }
        if (In(ExciteR, p)) { Commit(() => ms.Excite = !ms.Excite); return; }
        if (In(LoudR, p)) { Commit(() => ms.Comp = !ms.Comp); return; }
        if (In(PresetR, p)) { PresetMenu(e.Location); return; }
        if (In(NavL, p)) { int i = PresetIndex(); ApplyPreset(i <= 0 ? PresetNames.Length - 1 : i - 1); return; }
        if (In(NavR, p)) { int i = PresetIndex(); ApplyPreset(i < 0 || i >= PresetNames.Length - 1 ? 0 : i + 1); return; }
        if (In(UndoR, p) && undo.Count > 0) { redo.Add(ms.Serialize()); ms.Parse(undo[undo.Count - 1]); undo.RemoveAt(undo.Count - 1); presetName = "Manual"; app.SaveSettings(); Invalidate(); return; }
        if (In(RedoR, p) && redo.Count > 0) { undo.Add(ms.Serialize()); ms.Parse(redo[redo.Count - 1]); redo.RemoveAt(redo.Count - 1); presetName = "Manual"; app.SaveSettings(); Invalidate(); return; }
        if (In(CompareR, p)) { ToggleCompare(); return; }
        if (In(CopyR, p)) { try { Clipboard.SetText(ms.Serialize()); } catch { } return; }
        if (In(PasteR, p))
        {
            try
            {
                if (Clipboard.ContainsText() && Clipboard.GetText().StartsWith("on="))
                { string txt = Clipboard.GetText(); Commit(() => ms.Parse(txt)); }
            }
            catch { }
            return;
        }
        if (In(ViewR, p)) { ViewMenu(e.Location); return; }
        if (In(ReanR, p)) { Analyze(true, (ModifierKeys & Keys.Control) != 0); return; }
        if (In(StartR, p)) { Analyze(false, (ModifierKeys & Keys.Control) != 0); return; }
        if (In(ResetR, p)) { app.LastAn = null; Invalidate(); return; }
    }

    static float Dist(PointF a, PointF b) { float dx = a.X - b.X, dy = a.Y - b.Y; return (float)Math.Sqrt(dx * dx + dy * dy); }

    void MoveDrag(PointF p)
    {
        if (drag == 1)
        {
            float[] lo = { 20, 60, 200, 800, 2000 }, hi = { 500, 2000, 8000, 16000, 20000 };
            double f = Math.Max(lo[selNode], Math.Min(hi[selNode], FromX(p.X)));
            double gdb = Math.Max(-12, Math.Min(12, FromY(p.Y)));
            ms.NF[selNode] = (float)Math.Round(f, 1);
            ms.NG[selNode] = (float)Math.Round(gdb * 10) / 10f;
        }
        else if (drag == 2) ms.AutoEq = (float)Math.Round(Math.Max(0, Math.Min(100, (SlY2 - p.Y) / (SlY2 - SlY1) * 100)));
        else if (drag == 3) ms.Loud = (float)Math.Round(Math.Max(-20, Math.Min(-6, dragVal + (dragY - p.Y) * 0.08)) * 10) / 10f;
        else if (drag == 4) ms.Width = (float)Math.Round(Math.Max(0, Math.Min(200, dragVal + (dragY - p.Y))));
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (drag != 0) MoveDrag(new PointF(e.X / z, e.Y / z));
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (drag != 0)
        {
            drag = 0;
            if (ms.Serialize() != dragBefore) { undo.Add(dragBefore); redo.Clear(); presetName = "Manual"; app.SaveSettings(); }
            Invalidate();
        }
    }

    void ToggleCompare()
    {
        if (!comparing)
        {
            if (undo.Count == 0) return;
            cmpHold = ms.Serialize(); ms.Parse(undo[undo.Count - 1]); comparing = true;
        }
        else { ms.Parse(cmpHold); comparing = false; }
        Invalidate();
    }

    void CharMenu(Point at)
    {
        var m = Win.NewMenu();
        for (int i = 0; i < 4; i++)
        {
            int k = i; var it = new ToolStripMenuItem(MasterSettings.CharNames[i]) { Checked = ms.Character == i };
            it.Click += (s, e) => Commit(() => ms.Character = k);
            m.Items.Add(it);
        }
        m.Show(this, at);
    }

    void PresetMenu(Point at)
    {
        var m = Win.NewMenu();
        for (int i = 0; i < PresetNames.Length; i++)
        {
            int k = i; var it = new ToolStripMenuItem(PresetNames[i]) { Checked = presetName == PresetNames[i] };
            it.Click += (s, e) => ApplyPreset(k);
            m.Items.Add(it);
        }
        m.Show(this, at);
    }

    void ViewMenu(Point at)
    {
        var m = Win.NewMenu();
        foreach (int pct in new[] { 50, 60, 70, 75, 85, 100 })
        {
            int k = pct; var it = new ToolStripMenuItem(pct + "%") { Checked = app.MZoomPct == pct };
            it.Click += (s, e) => { app.MZoomPct = k; z = k / 100f; ApplyZoom(); app.SaveSettings(); Invalidate(); };
            m.Items.Add(it);
        }
        m.Show(this, at);
    }

    // Analyze the last converted file (or pick one) in the background.
    void Analyze(bool full, bool pick)
    {
        if (analyzing) return;
        string file = app.LastFile;
        if (pick || file == null || !File.Exists(file))
        {
            using (var d = new OpenFileDialog())
            {
                d.Title = "Choose an audio file to analyze";
                d.Filter = "Audio|*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg;*.opus|All files|*.*";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                file = d.FileName; app.LastFile = file;
            }
        }
        if (App.FfmpegExe() == null) { MessageBox.Show(this, "Convert something once first. That installs ffmpeg, which the analysis needs."); return; }
        analyzing = true; analyzeText = "Analyzing " + Path.GetFileName(file) + "...";
        string f = file;
        new Thread(() =>
        {
            MasterAnalysis an = null;
            try { an = MasterDsp.Analyze(f, full, true, null); } catch { }
            BeginInvoke(new Action(() =>
            {
                analyzing = false;
                if (an != null && an.Ok)
                {
                    if (!full && app.LastAn != null) { an.Auto = app.LastAn.Auto; an.HasBands = app.LastAn.HasBands; }
                    app.LastAn = an;
                }
                else analyzeText = "";
                Invalidate();
            }));
        }) { IsBackground = true }.Start();
    }
}
