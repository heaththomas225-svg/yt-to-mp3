using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Forms;

// =====================================================================================================
//  BASS DESIGN: what each bass flavour of the library really is, as a patch of the Purphex Wobble engine, and the phrases it plays.
//  Every flavour is an ARCHETYPE with its own sound source, filter, LFO rate (or a rhythm of rates), LFO shape, grit and envelope; the
//  pack's seed picks the exact numbers inside the archetype's range, so two packs with "wobble" are two different wobbles. In the UK
//  Bassline packs EVERY bass moves with a tempo-synced LFO (a sub, organ or stab flavour there becomes its LFO'd version).
//  The phrases are written for the sound: wobbles hold their notes long enough to be heard (a 1/4 wobble needs at least a beat), with
//  octave jumps, offbeat bounces and call-and-response, like a producer writes a bassline; UK Garage and Speed Garage lines swing.
// =====================================================================================================
static class BassDesign
{
    // the flavours the wobble engine plays (the others - sub, pluck, organ, stab outside bassline - stay on the Bass Lab synthesizer)
    static readonly HashSet<string> EngineFl = new HashSet<string> { "wobble", "filthy", "rubber", "elastic", "growl", "dirty", "talking", "fm", "square", "reese", "neuro", "metallic", "riddim", "speedgarage", "subwob", "organwob", "stabwob", "warped", "classic", "experimental", "laser" };
    public static bool UsesEngine(string fl) { return EngineFl.Contains(fl); }
    static readonly HashSet<string> NoWobble = new HashSet<string> { "Trap", "UK Drill", "Hip-Hop", "Boom Bap", "Grime", "Vocals", "Lo-Fi", "R&B", "Pop", "Jersey Club", "Afrobeats", "Amapiano", "Reggaeton", "Dancehall", "Baile Funk", "Phonk" };
    public static bool IsBassline(Recipe r) { return r != null && r.Genre == "UK Bassline"; }
    // the flavour a pack really plays: in a UK Bassline pack every bass is LFO'd
    public static string FlavourFor(Recipe r, string fl)
    {
        if (!IsBassline(r)) { if (r != null && NoWobble.Contains(r.Genre) && (fl == "dirty" || fl == "growl" || fl == "rubber" || fl == "elastic")) return "bl_" + fl; return fl; }   // rap, trap, drill, afro, pop... keep their 808 and synth basses (a wobble does not belong there)
        switch (fl) { case "sub": return "subwob"; case "organ": return "organwob"; case "stab": return "stabwob"; case "pluck": return "rubber"; case "dirty": return "square"; default: return UsesEngine(fl) ? fl : "wobble"; }
    }
    // a readable name for each archetype (sound names and types)
    public static string Title(string fl)
    {
        switch (fl)
        {
            case "wobble": return "Wobble"; case "filthy": return "Filthy Wobble"; case "rubber": return "Rubber"; case "elastic": return "Elastic"; case "growl": return "Growl"; case "dirty": return "Dirty";
            case "talking": return "Talking"; case "fm": return "FM Wobble"; case "square": return "Square Wobble"; case "reese": return "Reese"; case "neuro": return "Neuro"; case "metallic": return "Metallic FM";
            case "riddim": return "Riddim"; case "speedgarage": return "Speed Garage"; case "subwob": return "Sub Wobble"; case "organwob": return "Organ Wobble"; case "stabwob": return "Stab Wobble";
            case "warped": return "Warped Formant"; case "classic": return "Classic Bassline"; case "experimental": return "Experimental"; case "laser": return "Laser";
        }
        return fl.Length == 0 ? fl : char.ToUpperInvariant(fl[0]) + fl.Substring(1);
    }
    public static string TypeOf(string fl)
    {
        switch (fl)
        {
            case "reese": case "neuro": return "Reese Bass"; case "growl": case "dirty": case "metallic": case "fm": return "Growl Bass"; case "talking": case "warped": return "Talking Bass";
            case "rubber": case "elastic": return "Rubber Bass"; case "stabwob": case "organwob": return "Bass Stab"; case "subwob": return "Sub Bass";
            default: return "Wobble Bass";
        }
    }
    // the LFO division words for names and search ("1/8", "1/16 triplet"...)
    public static string RateWords(string rates)
    {
        var p = rates.Split(','); if (p.Length > 1) return "rate sequence";
        switch (p[0]) { case "1": return "1/1"; case "2": return "1/2"; case "4": return "1/4"; case "8": return "1/8"; case "16": return "1/16"; case "32": return "1/32"; case "6": return "1/4 triplet"; case "12": return "1/8 triplet"; case "24": return "1/16 triplet"; }
        return "1/" + p[0];
    }

    static double R(Rnd r, double a, double b) { return a + (b - a) * r.Next(); }
    static T Pick<T>(Rnd r, T[] a) { return a[r.Int(a.Length)]; }
    // the patch of an archetype; the seed picks the numbers inside its range
    public static WobblePatch Patch(string fl, Rnd r)
    {
        var p = new WobblePatch { Retrig = true };
        switch (fl)
        {
            case "wobble":
                p.Saw = 1; p.Square = R(r, 0, 0.5); p.Cutoff = R(r, 110, 220); p.Depth = R(r, 4.0, 5.2); p.Res = R(r, 0.45, 0.72); p.Rates = Pick(r, new[] { "8", "4", "16", "8,8,16,16", "4,8,16,8", "12", "8,4,8,16" }); p.Shape = Pick(r, new[] { 0, 0, 1, 5 }); p.Drive = R(r, 0.3, 0.6); p.Sub = R(r, 0.5, 0.65); p.Glide = Pick(r, new[] { 0.0, 0.0, 0.03 }); break;
            case "filthy":
                p.Saw = 1; p.Square = R(r, 0.3, 0.8); p.Fold = R(r, 0.2, 0.5); p.Drive = R(r, 0.6, 0.9); p.Rates = Pick(r, new[] { "8,16,8,24", "16,8,12,16", "4,16,8,32", "8,8,16,12", "16,16,8,24" }); p.Shape = Pick(r, new[] { 0, 2, 4 }); p.Res = R(r, 0.5, 0.75); p.Cutoff = R(r, 150, 250); p.Depth = R(r, 4.0, 5.2); p.Sub = R(r, 0.45, 0.6); break;
            case "rubber":
                p.Saw = 0; p.Square = 1; p.Pw = R(r, 0.3, 0.45); p.PwMod = R(r, 0.3, 0.5); p.EnvAmt = R(r, 1.2, 2.4); p.EnvDecay = R(r, 0.08, 0.2); p.Res = R(r, 0.65, 0.85); p.Rates = Pick(r, new[] { "4", "2", "6", "4,4,8,4" }); p.Shape = Pick(r, new[] { 1, 5 }); p.Glide = R(r, 0.03, 0.09); p.Cutoff = R(r, 95, 160); p.Depth = R(r, 3.6, 4.6); p.PitchEnv = R(r, 3, 7); p.PitchDecay = R(r, 0.03, 0.06); p.Drive = R(r, 0.25, 0.45); break;
            case "elastic":
                p.Saw = 0.7; p.Square = 0.7; p.Glide = R(r, 0.06, 0.14); p.PitchEnv = Pick(r, new[] { -5.0, 7.0, 12.0 }); p.PitchDecay = R(r, 0.05, 0.12); p.Rates = Pick(r, new[] { "6", "12", "4,6,4,12" }); p.Shape = 5; p.Res = R(r, 0.6, 0.8); p.Cutoff = R(r, 110, 190); p.Depth = R(r, 3.8, 4.8); p.Drive = R(r, 0.3, 0.5); break;
            case "growl": case "dirty":
                p.Saw = 0.3; p.Fm = 1; p.FmRatio = Pick(r, new[] { 1.0, 2.0, 0.5 }); p.FmIndex = R(r, 2, 4); p.FmMod = R(r, 0.5, 1.0); p.Rates = Pick(r, new[] { "16", "16,8,16,32", "12,16", "16,16,8,8" }); p.Shape = Pick(r, new[] { 2, 0 }); p.Drive = R(r, 0.6, 0.85); p.Cutoff = R(r, 250, 450); p.Depth = R(r, 2.8, 3.8); p.Fold = R(r, 0.1, 0.3); p.Res = R(r, 0.45, 0.65); p.Cutoff = Math.Min(p.Cutoff, 330); p.Depth = Math.Max(p.Depth, 3.4); break;
            case "talking":
                p.Saw = 1; p.Square = R(r, 0.3, 0.7); p.Formant = true; p.Vowels = Pick(r, new[] { "aoui", "oaei", "uoa", "aeo", "iaou", "ouai" }); p.Rates = Pick(r, new[] { "8", "4", "8,4,8,16", "12" }); p.Shape = Pick(r, new[] { 5, 1 }); p.Res = R(r, 0.55, 0.78); p.Cutoff = R(r, 300, 520); p.Depth = R(r, 2.6, 3.4); p.Drive = R(r, 0.3, 0.5); break;
            case "fm":
                p.Saw = 0; p.Fm = 1; p.FmRatio = Pick(r, new[] { 1.5, 2.0, 3.0, 0.5 }); p.FmIndex = R(r, 3, 6); p.FmMod = R(r, 0.8, 1.4); p.Rates = Pick(r, new[] { "8", "16", "8,16", "4,8" }); p.Shape = Pick(r, new[] { 0, 1, 2 }); p.Cutoff = R(r, 180, 320); p.Depth = R(r, 3.6, 4.6); p.Res = R(r, 0.4, 0.6); p.Drive = R(r, 0.4, 0.6); break;
            case "square":
                p.Saw = 0; p.Square = 1; p.Pw = 0.5; p.PwMod = R(r, 0.2, 0.4); p.Drive = R(r, 0.7, 0.9); p.Rates = Pick(r, new[] { "8", "4", "16", "8,16" }); p.Shape = Pick(r, new[] { 0, 1, 4 }); p.Res = R(r, 0.4, 0.6); p.Cutoff = R(r, 110, 200); p.Depth = R(r, 4.0, 5.0); p.Drive = R(r, 0.5, 0.7); break;
            case "reese":
                p.Saw = 0; p.Reese = 1; p.Detune = R(r, 14, 30); p.Cutoff = R(r, 200, 400); p.Depth = R(r, 1.5, 2.5); p.Rates = Pick(r, new[] { "1", "2", "4" }); p.Shape = 1; p.Res = R(r, 0.3, 0.5); p.Drive = R(r, 0.4, 0.6); p.Retrig = false; break;
            case "neuro":
                p.Saw = 0; p.Reese = 1; p.Fm = 0.4; p.Fold = R(r, 0.3, 0.6); p.Detune = R(r, 18, 30); p.Rates = Pick(r, new[] { "16,8,32,16", "16", "8,24,16,12", "32,16,8,16" }); p.Shape = Pick(r, new[] { 2, 4 }); p.Drive = R(r, 0.6, 0.9); p.Res = R(r, 0.55, 0.8); p.Cutoff = R(r, 150, 260); p.Depth = R(r, 3.8, 4.8); break;
            case "metallic":
                p.Saw = 0; p.Fm = 1; p.FmRatio = Pick(r, new[] { 1.41, 2.73, 3.5 }); p.FmIndex = R(r, 3, 5); p.Fold = R(r, 0.3, 0.5); p.Rates = Pick(r, new[] { "16", "8,16", "16,32" }); p.Res = R(r, 0.55, 0.72); p.Shape = 2; p.Cutoff = R(r, 180, 300); p.Depth = R(r, 3.6, 4.6); p.Drive = R(r, 0.45, 0.65); break;
            case "riddim":
                p.Saw = 0; p.Square = 1; p.Shape = 4; p.Rates = Pick(r, new[] { "8", "16", "8,8,16,8" }); p.Drive = R(r, 0.75, 0.9); p.Cutoff = R(r, 160, 220); p.Depth = R(r, 4.0, 4.8); p.Res = R(r, 0.35, 0.5); break;
            case "speedgarage":
                p.Saw = 0.5; p.Square = 0.7; p.EnvAmt = R(r, 1.6, 2.4); p.EnvDecay = R(r, 0.1, 0.18); p.Rates = Pick(r, new[] { "8", "4", "8,4" }); p.Shape = 1; p.Glide = 0.02; p.PitchEnv = Pick(r, new[] { 12.0, 7.0, -12.0 }); p.PitchDecay = R(r, 0.05, 0.1); p.Res = R(r, 0.5, 0.7); p.Cutoff = R(r, 180, 300); p.Depth = R(r, 2.5, 3.5); p.Drive = R(r, 0.35, 0.55); break;
            case "subwob":
                p.Saw = 1; p.Sub = R(r, 0.6, 0.7); p.Mid = R(r, 1.25, 1.45); p.Cutoff = R(r, 110, 170); p.Depth = R(r, 3.6, 4.4); p.Res = R(r, 0.3, 0.45); p.Rates = Pick(r, new[] { "4", "8", "2" }); p.Shape = 1; p.Drive = R(r, 0.15, 0.3); break;
            case "organwob":
                p.Saw = 0; p.Square = 1; p.Fm = 0.35; p.FmRatio = 2; p.FmIndex = R(r, 0.6, 1.2); p.Rates = Pick(r, new[] { "8", "16", "12" }); p.Shape = Pick(r, new[] { 4, 1 }); p.EnvAmt = R(r, 0.8, 1.4); p.EnvDecay = 0.12; p.Res = R(r, 0.4, 0.6); p.Cutoff = R(r, 150, 260); p.Depth = R(r, 3.4, 4.2); p.Drive = R(r, 0.3, 0.5); break;
            case "stabwob":
                p.Saw = 1; p.Square = R(r, 0.4, 0.8); p.Rates = Pick(r, new[] { "16", "8", "12" }); p.Shape = 0; p.EnvAmt = R(r, 1.2, 2.0); p.EnvDecay = R(r, 0.06, 0.12); p.Res = R(r, 0.5, 0.7); p.Cutoff = R(r, 110, 200); p.Depth = R(r, 4.0, 5.0); p.Drive = R(r, 0.4, 0.6); p.Release = 0.04; break;
            case "warped":
                p.Saw = 1; p.Formant = true; p.Vowels = Pick(r, new[] { "eao", "iou", "aie" }); p.Fold = R(r, 0.25, 0.45); p.Rates = Pick(r, new[] { "6", "12,6", "4,12" }); p.Shape = 3; p.PitchEnv = R(r, -4, 4); p.Res = R(r, 0.55, 0.78); p.Cutoff = R(r, 280, 480); p.Depth = R(r, 2.8, 3.6); p.Drive = R(r, 0.45, 0.65); break;
            case "classic":
                p.Saw = 1; p.Square = R(r, 0.5, 1.0); p.Rates = Pick(r, new[] { "8", "4", "8,8,4,4" }); p.Shape = 1; p.Glide = R(r, 0.03, 0.06); p.Res = R(r, 0.5, 0.65); p.Cutoff = R(r, 120, 200); p.Depth = R(r, 4.0, 5.0); p.Drive = R(r, 0.35, 0.55); break;
            case "laser":
                p.Saw = 1; p.Square = 0.5; p.Rates = Pick(r, new[] { "16", "32" }); p.Shape = 3; p.Res = R(r, 0.75, 0.9); p.Cutoff = R(r, 300, 500); p.Depth = R(r, 3.0, 3.8); p.Drive = R(r, 0.5, 0.7); break;
            default: /* experimental */
                p.Saw = R(r, 0, 1); p.Square = R(r, 0, 1); p.Fm = R(r, 0, 1); p.Reese = R(r, 0, 0.6); p.FmRatio = Pick(r, new[] { 0.5, 1.5, 2.5, 3.0 }); p.FmIndex = R(r, 1, 5); p.Fold = R(r, 0.2, 0.6); p.Rates = Pick(r, new[] { "6,24,12,32", "24,8,6,16", "12,12,24,6" }); p.Shape = r.Int(6); p.Formant = r.Next() < 0.3f; p.Vowels = "iaeu"; p.Res = R(r, 0.5, 0.85); p.Cutoff = R(r, 200, 500); p.Depth = R(r, 2.5, 4.5); p.Drive = R(r, 0.5, 0.9); p.Pump = R(r, 0, 0.4); break;
        }
        if (p.Saw + p.Square + p.Reese + p.Fm < 0.2) p.Saw = 1;
        p.Width = R(r, 0.15, 0.4); p.Attack = R(r, 0.003, 0.008); if (p.Release < 0.05) p.Release = Math.Max(p.Release, 0.03); else p.Release = R(r, 0.05, 0.12);
        return p;
    }

    // ---- phrases {start beat, length in beats, midi, velocity} ----
    // letter = a new note (its pitch: a root, b octave, c fifth, d minor seventh / sixth, e third, f the octave below the fifth), '-' = it holds, '.' = rest
    static readonly string[] HoldBars = {
        "a-------b---c---", "a-----a-b-------", "a---a-b---a-c-d-", "..a-..a-..a-..a-", "a-b-a-b-c---d---", "a-------a---b---", "..a---..a---b---", "a---------c-b---", "a-------d-------", "a-----b-----c---" };
    static readonly string[] RiffBars = {
        "a.a.b.a.c.a.d.b.", "a-.ab-.ac-.ab-.d", "a.b.a.c.a.b.d.c.", "aa.a.b.aa.a.c.b.", "a-a-.a-a-b-.c-d-" };
    static readonly string[] StabBars = { "a..a..a.a..a..a.", ".a.a.a.a.a.a.a.a", "a..a.a..a..a.a..", "..a...a...a...a." };
    static readonly string[] SgBars = { "..a-..a-..a-..b-", "..a-..a-..c-..a-", "a-..a-..b-..a-..", "..a-..a-..a-..a-" };
    // the phrase style of a flavour: held wobbles, riffs, stabs or speed garage offbeats
    public static string StyleOf(string fl) { switch (fl) { case "stabwob": case "organwob": return "stab"; case "speedgarage": return "sg"; case "growl": case "dirty": case "neuro": case "metallic": case "laser": return "riff"; default: return "hold"; } }
    public static List<double[]> Phrase(Rnd r, string style, int bars, int root, bool minor, double swing)
    {
        string[] bank = style == "riff" ? RiffBars : style == "stab" ? StabBars : style == "sg" ? SgBars : HoldBars;
        int third = minor ? 3 : 4, sev = minor ? 10 : 9; var deg = new Dictionary<char, int> { { 'a', 0 }, { 'b', 12 }, { 'c', 7 }, { 'd', sev }, { 'e', third }, { 'f', -5 } };
        string call = bank[r.Int(bank.Length)], answer = bank[r.Int(bank.Length)]; int shift = r.Next() < 0.5f ? 0 : (minor ? (r.Next() < 0.5f ? 3 : 5) : 5);   // the answer bar moves to another chord tone
        var notes = new List<double[]>();
        for (int bar = 0; bar < bars; bar++)
        {
            string p = bar % 2 == 0 ? call : answer; int rootNow = root + (bar % 2 == 1 ? (bar % 4 == 3 ? shift : 0) : 0);
            for (int st = 0; st < 16; st++)
            {
                char c = p[st]; if (c == '-' || c == '.') continue; int len = 1; while (st + len < 16 && p[st + len] == '-') len++;
                double start = (bar * 16 + st) * 0.25; if (st % 2 == 1 && swing > 0.5) start += (swing - 0.5) * 2 * 0.25;   // swung 16ths (UK garage, speed garage)
                int pitch = rootNow + (deg.ContainsKey(c) ? deg[c] : 0); double l = len * 0.25 * (style == "stab" ? 0.55 : 0.94);
                notes.Add(new[] { start, l, (double)pitch, st % 4 == 0 ? 112.0 : 100.0 });
            }
        }
        return notes;
    }
    // a bassline in the pack's rhythm (the old generator) with the swing of the pack applied to its off 16ths
    public static List<double[]> Swung(List<double[]> notes, double swing)
    {
        if (swing <= 0.5) return notes; foreach (var n in notes) { double st = n[0] * 4; if (Math.Abs(st - Math.Round(st)) < 1e-6 && ((int)Math.Round(st)) % 2 == 1) n[0] += (swing - 0.5) * 2 * 0.25; } return notes;
    }

    // the bass of a pack: notes and the finished sound (one loop or one-shot)
    public static Buf Render(WobblePatch p, List<double[]> notes, double bpm, double beats, double tail) { return WobbleEngine.Render(p, notes, bpm, beats, tail); }

    // ---- the check of an LFO'd bass: it must really move, with real mid (not only sub) ----
    public class Moves { public double Swing, Mid, Rate, Bright; public bool Ok; public string Why = ""; }
    public static Moves Check(Buf b)
    {
        var m = new Moves(); int N = 1024, hop = 44100 * 12 / 1000; if (b.Length < N * 4) { m.Why = "too short to measure"; return m; }
        var re = new double[N]; var im = new double[N]; var band = new List<double>(); var lvl = new List<double>(); var cent = new List<double>(); double midE = 0, all = 0;
        for (int a = 0; a + N < b.Length; a += hop)
        {
            for (int k = 0; k < N; k++) { double w = 0.5 - 0.5 * Math.Cos(2 * Math.PI * k / (N - 1)); re[k] = (b.L[a + k] + b.R[a + k]) * 0.5 * w; im[k] = 0; }
            Fft.Transform(re, im, false); double bE = 0, tE = 0, num = 0, den = 0;
            for (int k = 1; k < N / 2; k++) { double hz = k * 44100.0 / N, e = re[k] * re[k] + im[k] * im[k]; if (hz > 300 && hz < 3000) bE += e; if (hz > 140) midE += e; if (hz > 140 && hz < 12000) { num += hz * e; den += e; } tE += e; all += e; }
            if (tE > 1e-9) { band.Add(10 * Math.Log10(bE + 1e-12)); lvl.Add(10 * Math.Log10(tE)); cent.Add(den > 1e-12 ? num / den : 0); }
        }
        if (lvl.Count < 12) { m.Why = "too quiet to measure"; return m; }
        double top = double.MinValue; foreach (var x in lvl) top = Math.Max(top, x); var bs = new List<double>(); for (int i = 0; i < band.Count; i++) if (lvl[i] > top - 30) bs.Add(band[i]); bs.Sort();
        if (bs.Count > 10) m.Swing = bs[(int)(bs.Count * 0.9)] - bs[(int)(bs.Count * 0.1)]; m.Mid = midE / Math.Max(1e-12, all);
        // the rate: the first strong repeat of the brightness between 1 and 20 Hz
        double mean = 0; foreach (var x in cent) mean += x; mean /= cent.Count; double fps = 1000.0 / 12; int lo = (int)(fps / 20), hi = (int)fps; var ac = new double[hi + 2]; double best = 0;
        for (int lag = lo; lag <= hi + 1 && lag < cent.Count; lag++) { double acc = 0, n0 = 0; for (int i = 0; i + lag < cent.Count; i++) { acc += (cent[i] - mean) * (cent[i + lag] - mean); n0 += (cent[i] - mean) * (cent[i] - mean); } ac[lag] = acc / Math.Max(1e-12, n0); if (lag <= hi) best = Math.Max(best, ac[lag]); }
        for (int lag = lo + 1; lag <= hi && lag + 1 < ac.Length; lag++) if (ac[lag] >= ac[lag - 1] && ac[lag] >= ac[lag + 1] && ac[lag] > 0.3 && ac[lag] >= best * 0.8) { m.Rate = fps / lag; break; }
        { var cs = new List<double>(); for (int i = 0; i < cent.Count; i++) if (lvl[i] > top - 30 && cent[i] > 0) cs.Add(cent[i]); cs.Sort(); if (cs.Count > 10) m.Bright = cs[(int)(cs.Count * 0.9)] / Math.Max(1, cs[(int)(cs.Count * 0.1)]); }   // how far the brightness moves (a vowel filter moves inside the band)
        if (m.Mid < 0.15) m.Why = "almost all sub (" + (m.Mid * 100).ToString("0") + "% above 140 Hz): no wobble to hear";
        else if (m.Swing < 6 && m.Bright < 1.35) m.Why = "the filter hardly moves (" + m.Swing.ToString("0.0") + " dB, brightness x" + m.Bright.ToString("0.00") + ")";
        m.Ok = m.Why.Length == 0; return m;
    }
}

// =====================================================================================================
//  THE PURPHEX WOBBLE: the engine as an instrument on a track. Every library wobble preset is the same sound here on any note, and every
//  setting is a real control: the sound source, LFO rate (a note division or a rhythm of divisions), LFO shape, depth, cutoff, resonance,
//  vowel filter, grit, fold, FM, detune, sub and mid levels, envelope, glide, width. It plays one note at a time (a bass), with glide.
// =====================================================================================================
sealed class WobbleFx : InstrumentFx
{
    public static readonly string[] Srcs = { "Saw", "Square", "Reese (2 saws)", "FM", "Saw + Square", "FM + Saw", "Reese + FM" };
    public static readonly string[] RateNames = { "1/1", "1/2", "1/4", "1/8", "1/16", "1/32", "1/4 triplet", "1/8 triplet", "1/16 triplet", "Rhythm A (1/4 1/8 1/16 1/8)", "Rhythm B (1/8 1/8T 1/16 1/16T)", "Rhythm C (1/16 1/8 1/4 1/8)", "Rhythm D (1/8 1/16 1/8 1/32)" };
    static readonly string[] RateCodes = { "1", "2", "4", "8", "16", "32", "6", "12", "24", "4,8,16,8", "8,12,16,24", "16,8,4,8", "8,16,8,32" };
    public static readonly string[] Vowels = { "aoui", "oaei", "uoa", "aeo", "iaou", "ouai", "eao", "iou", "aie", "iaeu" }, Ratios = { "0.5", "1", "1.41", "1.5", "2", "2.73", "3", "3.5" };
    public static readonly string[] Shapes = { "Wub", "Sine", "Yoy (falling)", "Rising", "Chop", "Triangle" };
    readonly WobblePatch patch = new WobblePatch(); WobbleVoice voice; List<KeyValuePair<int, MidiEv>> pending; readonly List<int> held = new List<int>(); string sig = "";
    public WobbleFx()
    {
        Type = "wobble"; Title = "Purphex Wobble"; Category = "Instruments"; Blurb = "The wobble bass engine: tempo-synced LFO filter, vowels, FM, grit, sub and mid layers";
        AddC("src", "Sound", Srcs, 0); AddC("rate", "LFO rate", RateNames, 3); AddC("shape", "LFO shape", Shapes, 0); AddC("filter", "Filter", new[] { "Low-pass", "Vowels (talking)" }, 0);
        AddP("depth", "Wobble depth", 0, 6, 4, "oct"); AddP("cutoff", "Cutoff", 40, 2000, 180, "Hz"); AddP("res", "Resonance", 0, 95, 55, "%"); AddP("grit", "Grit", 0, 100, 45, "%"); AddP("fold", "Distortion (fold)", 0, 100, 0, "%");
        AddP("fm", "FM amount", 0, 8, 2, ""); AddP("detune", "Detune", 0, 50, 18, "ct"); AddP("sub", "Sub", 0, 100, 55, "%"); AddP("mid", "Mid", 0, 150, 130, "%");
        AddP("attack", "Attack", 1, 200, 4, "ms"); AddP("release", "Release", 5, 1000, 60, "ms"); AddP("glide", "Glide", 0, 300, 0, "ms"); AddP("width", "Width", 0, 100, 25, "%");
        AddC("vowels", "Vowels", Vowels, 0); AddC("ratio", "FM ratio", Ratios, 4); AddP("pw", "Pulse width", 8, 92, 50, "%");
        AddP("env", "Filter envelope", 0, 4, 0, "oct"); AddP("pitchenv", "Pitch drop", -12, 12, 0, "st"); AddP("level", "Level", -24, 12, 0, "dB");
    }
    public override bool IsInstrument { get { return true; } }
    public override bool HasWindow { get { return false; } }
    public override string WindowLabel { get { return "Open the piano roll"; } }
    public override void ShowWindow(IWin32Window owner) { var sf = owner as StudioForm; if (sf != null && Owner != null) sf.OpenPianoRoll(Owner); }
    public override void CloseWindow() { }
    public override void Reset() { base.Reset(); held.Clear(); voice = null; }
    protected override void OnEvents(List<KeyValuePair<int, MidiEv>> ev) { pending = ev; }
    double songBeat; bool rolling;
    protected override void OnBlock(long pos, int n, bool transport) { rolling = transport; songBeat = pos / (SR * 60.0 / Math.Max(20, Bpm)); }
    // the controls into the engine's patch
    void Sync()
    {
        int src = (int)Get("src"); patch.Saw = src == 0 || src == 4 || src == 5 ? 1 : 0; patch.Square = src == 1 || src == 4 ? 1 : 0; patch.Reese = src == 2 || src == 6 ? 1 : 0; patch.Fm = src == 3 || src == 5 || src == 6 ? 1 : 0;
        patch.Rates = RateCodes[Math.Max(0, Math.Min(RateCodes.Length - 1, (int)Get("rate")))]; patch.Shape = (int)Get("shape"); patch.Formant = Get("filter") >= 0.5f;
        patch.Depth = Get("depth"); patch.Cutoff = Get("cutoff"); patch.Res = Get("res") / 100.0; patch.Drive = Get("grit") / 100.0; patch.Fold = Get("fold") / 100.0; patch.FmIndex = Get("fm"); patch.FmMod = 0.8; patch.Detune = Get("detune");
        patch.Sub = Get("sub") / 100.0; patch.Mid = Get("mid") / 100.0; patch.Attack = Get("attack") / 1000.0; patch.Release = Get("release") / 1000.0; patch.Glide = Get("glide") / 1000.0; patch.Width = Get("width") / 100.0;
        patch.EnvAmt = Get("env"); patch.PitchEnv = Get("pitchenv"); patch.Vowels = Vowels[Math.Max(0, Math.Min(Vowels.Length - 1, (int)Get("vowels")))]; patch.FmRatio = double.Parse(Ratios[Math.Max(0, Math.Min(Ratios.Length - 1, (int)Get("ratio")))], CultureInfo.InvariantCulture); patch.Pw = Get("pw") / 100.0;
        string s = patch.Rates + "|" + patch.Vowels; if (voice != null && s != sig) voice.Refresh(); sig = s;
    }
    // a library preset (the patch text) onto the controls
    public void LoadPatch(WobblePatch p)
    {
        int src = p.Reese > 0.5 && p.Fm > 0.2 ? 6 : p.Reese > 0.5 ? 2 : p.Fm > 0.5 && p.Saw > 0.2 ? 5 : p.Fm > 0.5 ? 3 : p.Square > 0.5 && p.Saw > 0.5 ? 4 : p.Square > 0.5 ? 1 : 0;
        Set("src", src); int ri = Array.IndexOf(RateCodes, p.Rates); Set("rate", ri >= 0 ? ri : 3); Set("shape", p.Shape); Set("filter", p.Formant ? 1 : 0);
        Set("depth", (float)p.Depth); Set("cutoff", (float)p.Cutoff); Set("res", (float)(p.Res * 100)); Set("grit", (float)(p.Drive * 100)); Set("fold", (float)(p.Fold * 100)); Set("fm", (float)p.FmIndex); Set("detune", (float)p.Detune);
        Set("sub", (float)(p.Sub * 100)); Set("mid", (float)(p.Mid * 100)); Set("attack", (float)(p.Attack * 1000)); Set("release", (float)(p.Release * 1000)); Set("glide", (float)(p.Glide * 1000)); Set("width", (float)(p.Width * 100));
        Set("env", (float)p.EnvAmt); Set("pitchenv", (float)p.PitchEnv); int vi = Array.IndexOf(Vowels, p.Vowels); Set("vowels", vi >= 0 ? vi : 0); int fi = 0; double bd = 99; for (int k = 0; k < Ratios.Length; k++) { double d = Math.Abs(double.Parse(Ratios[k], CultureInfo.InvariantCulture) - p.FmRatio); if (d < bd) { bd = d; fi = k; } } Set("ratio", fi); Set("pw", (float)(p.Pw * 100)); patch.Vowels = p.Vowels; patch.Pw = p.Pw; patch.PwMod = p.PwMod; patch.FmRatio = p.FmRatio; patch.EnvDecay = p.EnvDecay; patch.PitchDecay = p.PitchDecay; patch.Retrig = p.Retrig; patch.Pump = p.Pump; patch.Tone = p.Tone;
    }
    void Set(string id, float v) { var p = Find(id); if (p != null) p.Val = Math.Max(p.Min, Math.Min(p.Max, v)); }
    public override void Process(float[] l, float[] r, int n)
    {
        Sync(); if (voice == null) voice = new WobbleVoice(patch, SR, Bpm); voice.Tempo = Bpm; if (rolling) voice.SetBeat(songBeat);
        var ev = pending; pending = null; int ei = 0; double g = Math.Pow(10, Get("level") / 20.0);
        for (int i = 0; i < n; i++)
        {
            while (ev != null && ei < ev.Count && ev[ei].Key <= i)
            {
                var m = ev[ei].Value; int kind = m.S & 0xF0; ei++;
                if (kind == 0x90 && m.D2 > 0) { held.Remove(m.D1); held.Add(m.D1); voice.NoteOn(m.D1, m.D2, 0); }
                else if (kind == 0x80 || kind == 0x90) { held.Remove(m.D1); if (voice.Note == m.D1) { if (held.Count > 0) voice.NoteOn(held[held.Count - 1], 100, 0); else voice.NoteOff(m.D1); } }
                else if (kind == 0xB0 && (m.D1 == 123 || m.D1 == 120)) { held.Clear(); if (voice.Note >= 0) voice.NoteOff(voice.Note); }
            }
            if (!voice.Active) continue; double a, b; voice.Next(out a, out b); l[i] += (float)(a * g); r[i] += (float)(b * g);
        }
    }
    // a patch as the instrument's saved settings (the library's .purphexwobble preset files)
    public static string PresetText(WobblePatch p) { var fx = new WobbleFx(); fx.LoadPatch(p); return fx.Serialize(); }
    // the factory presets: one of every archetype (the plugin's preset menu)
    public static string[] FactoryPresets()
    {
        var l = new List<string>(); var fls = new[] { "wobble", "filthy", "rubber", "elastic", "growl", "talking", "fm", "square", "reese", "neuro", "metallic", "riddim", "speedgarage", "subwob", "organwob", "stabwob", "warped", "classic", "laser", "experimental" };
        foreach (var fl in fls) for (int v = 0; v < 2; v++) { var p = BassDesign.Patch(fl, new Rnd(1000 + v * 77 + fl.Length * 13)); var fx = new WobbleFx(); fx.LoadPatch(p); var sb = new System.Text.StringBuilder(); foreach (var q in fx.P) sb.Append(q.Id).Append('=').Append(q.Val.ToString("0.###", CultureInfo.InvariantCulture)).Append(';'); l.Add(BassDesign.Title(fl) + " " + (v == 0 ? "A" : "B") + " (" + BassDesign.RateWords(p.Rates) + ")|" + sb.ToString().TrimEnd(';')); }
        return l.ToArray();
    }
}
