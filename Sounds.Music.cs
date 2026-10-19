using System;
using System.Collections.Generic;

// Notes, bass flavours and patterns: what the Bass Lab synthesizer plays when a pack is made.
static class Music
{
    public static readonly int[] Minor = { 0, 2, 3, 5, 7, 8, 10 }, Major = { 0, 2, 4, 5, 7, 9, 11 }, MinPent = { 0, 3, 5, 7, 10 };
    public static int Root(string key) { int r; bool m; if (!SoundLibrary.ParseKey(key, out r, out m)) return 5; return r; }
    public static bool IsMinor(string key) { int r; bool m; return SoundLibrary.ParseKey(key, out r, out m) && m; }
    public static string NoteName(int midi) { return SoundLibrary.NoteNames[((midi % 12) + 12) % 12] + (midi / 12 - 1); }

    // each bass flavour starts from a Bass Lab preset; the numbers are nudged by the seed so every pack sounds a little different
    // a drawn LFO curve that jumps (two points at the same place) clicks every time it jumps: each jump becomes a ramp of 1% of the cycle
    public static string SmoothCurves(string data)
    {
        var parts = data.Split(';'); for (int pi = 0; pi < parts.Length; pi++)
        {
            string p = parts[pi]; if (!(p.StartsWith("lc1=") || p.StartsWith("lc2=") || p.StartsWith("lc3=") || p.StartsWith("lc4="))) continue;
            var pts = p.Substring(4).Split(','); var xs = new double[pts.Length]; var rest = new string[pts.Length]; var ci = System.Globalization.CultureInfo.InvariantCulture;
            for (int i = 0; i < pts.Length; i++) { int c = pts[i].IndexOf(':'); if (c < 0) { xs = null; break; } xs[i] = double.Parse(pts[i].Substring(0, c), ci); rest[i] = pts[i].Substring(c); }
            if (xs == null) continue;
            for (int i = 1; i < xs.Length; i++) if (Math.Abs(xs[i] - xs[i - 1]) < 0.004) { double lo = i >= 2 ? xs[i - 2] : 0, hi = i + 1 < xs.Length ? xs[i + 1] : 1, w = Math.Min(0.006, Math.Min(xs[i - 1] - lo, hi - xs[i]) / 2); if (w <= 0) continue; xs[i - 1] -= w; xs[i] += w; }
            var sb = new System.Text.StringBuilder(p.Substring(0, 4)); for (int i = 0; i < xs.Length; i++) { if (i > 0) sb.Append(','); sb.Append(Math.Max(0, Math.Min(1, xs[i])).ToString("0.####", ci)).Append(rest[i]); } parts[pi] = sb.ToString();
        }
        return string.Join(";", parts);
    }
    public static string BassPatch(string flavour, Rnd r)
    {
        string baseName; string extra = "";
        switch (flavour)
        {
            case "reese": baseName = "Reese Bass"; extra = "adet=" + (int)r.Range(20, 40) + ";bdet=" + (int)r.Range(16, 34) + ";f1cut=" + (int)r.Range(420, 900) + ";l1sync=" + r.Pick(new[] { 2, 3, 5 }); break;
            case "dirty": case "growl": baseName = "Growl Bass"; extra = "f1cut=" + (int)r.Range(600, 1400) + ";f1res=" + (int)r.Range(40, 62) + ";dstdrive=" + (int)r.Range(10, 18) + ";l1sync=" + r.Pick(new[] { 8, 11, 13 }); break;
            case "talking": baseName = "Formant Growl"; extra = "f1cut=" + (int)r.Range(700, 1300) + ";f1res=" + (int)r.Range(50, 68) + ";l1sync=" + r.Pick(new[] { 5, 8, 11 }); break;
            case "wobble": baseName = "Wobble Bass"; extra = "f1cut=" + (int)r.Range(380, 700) + ";f1res=" + (int)r.Range(40, 58) + ";l1sync=" + r.Pick(new[] { 3, 5, 7, 8 }); break;
            case "neuro": baseName = "Neuro Bite"; extra = "f1cut=" + (int)r.Range(500, 900) + ";l1sync=" + r.Pick(new[] { 8, 10, 11 }); break;
            case "metallic": baseName = "Metallic Neuro"; extra = "f1cut=" + (int)r.Range(1200, 2200) + ";flmix=" + (int)r.Range(30, 55); break;
            case "sub": baseName = "Deep Sub"; extra = "f1cut=" + (int)r.Range(260, 480) + ";bbgain=" + (int)r.Range(6, 11); break;
            case "stab": baseName = "Reese Stab"; extra = "f1cut=" + (int)r.Range(700, 1500); break;
            case "rubber": case "elastic": baseName = "Acid Squelch"; extra = "mode=2;glide=" + (int)r.Range(70, 160) + ";f1res=" + (int)r.Range(55, 78) + ";f1cut=" + (int)r.Range(320, 620); break;
            case "riddim": baseName = "Riddim Chug"; extra = "f1cut=" + (int)r.Range(380, 520); return SmoothCurves((SynthPresets.DataOf(baseName) ?? "") + ";" + extra) + ";e1a=4;f1drive=0;l1smooth=18;l2smooth=12;dstmode=2;dsttone=5000;dston=0;dstdrive=0;saton=0;satdrive=0" + Skip(r, 1);   // the chug, with soft edges, no distortion
            case "organ": baseName = "Organ"; extra = "e1d=" + (int)r.Range(150, 300) + ";e1s=" + (int)r.Range(0, 30) + ";e1r=90;aoct=-1"; break;
            case "pluck": baseName = "Pluck Bass"; extra = "f1cut=" + (int)r.Range(260, 420); break;
            default: baseName = "Wobble Bass"; break;
        }
        bool gritty = flavour == "growl" || flavour == "dirty" || flavour == "talking" || flavour == "neuro" || flavour == "metallic";
        string d = SynthPresets.DataOf(baseName) ?? ""; return SmoothCurves(d + ";" + extra) + ";e1a=4;f1drive=0;l1smooth=8;l2smooth=8;l3smooth=8;l4smooth=8;dstmode=2;dsttone=" + (int)r.Range(4500, 6500) + ";dston=0;dstdrive=0;saton=0;satdrive=0" + Skip(r, gritty ? 1 : 0);   /* no distortion on any pack bass */   // packs: growl without crackle (an overdriven filter and instant jumps are what crackle); the clean basses have no distortion at all
    }
    static string Skip(Rnd r, int n) { for (int i = 0; i < n; i++) r.Next(); return ""; }   // uses up the random numbers a removed setting used, so everything after it stays the same
    public static string LeadPatch(string name, string extra = "") { return (SynthPresets.DataOf(name) ?? "") + ";" + extra; }

    // ---- patterns: lists of {start in beats, length in beats, midi note, velocity} ----
    static readonly Dictionary<string, string[]> Rhythms = new Dictionary<string, string[]> {
        { "roller", new[] { "x..xx.x.x..xx.x.", "x.x.x..xx.x..x.x", "x..x..x.x..x.xx.", "xx.x.x..x.x.x.x." } },
        { "jumpup", new[] { "x.x...x.x.x...x.", "x..x.x..x.x.x...", "..x.x...x.x.x.x.", "x.x.x.x...x.x..." } },
        { "liquid", new[] { "x....x..x..x....", "x..x....x...x.x.", "x.....x...x.x..." } },
        { "neuro", new[] { "x.xxx.xxx.x.xx.x", "xx.xx.x.xx.xx.x." } },
        { "halftime", new[] { "x.......x.......", "x.....x.x......." } },
        { "garage", new[] { "x..x..x...x..x..", "x.x..x.x..x..x.x" } },
        { "bassline", new[] { "x.x.x..x.x.x..x.", "x..x.x.x..x.x.x." } },
        { "minimal", new[] { "x.......x.....x.", "x.....x........." } },
        { "house", new[] { "x.x.x.x.x.x.x.x.", ".x.x.x.x.x.x.x.x" } },
        { "afro", new[] { "x..x...x..x.x...", "x....x..x..x..x.", "x..x..x...x....." } },
        { "amapiano", new[] { "x..x..x...x.x...", "x.x...x.x..x....", "...x..x...x..x.x" } },
        { "dembow", new[] { "x..x..x.x..x..x.", "x..x...xx..x...x" } },
        { "techno", new[] { "..x...x...x...x.", "x.xxx.xxx.xxx.xx", ".xx..xx..xx..xx." } },
        { "pop", new[] { "x...x...x...x...", "x.....x.x.......", "x.x...x.x.x...x." } },
        { "rnb", new[] { "x......x.x......", "x....x....x.x...", "x..........x.x.." } },
        { "disco", new[] { "x.x.x.x.x.x.x.x.", "x..xx..xx..xx..x" } },
        { "trance", new[] { "..x...x...x...x.", ".xxx.xxx.xxx.xxx" } },
        { "dub", new[] { "x.......x.......", "x...x...x.x....." } },
    };
    public static string RhythmFor(string drums)
    {
        if (drums == "afro") return "afro"; if (drums == "amapiano") return "amapiano"; if (drums == "dembow" || drums == "dancehall" || drums == "baile") return "dembow"; if (drums == "techno" || drums == "techhouse") return "techno";
        if (drums == "pop" || drums == "jersey" || drums == "hyperpop") return "pop"; if (drums == "rnb" || drums == "lofi") return "rnb"; if (drums == "disco" || drums == "deephouse") return "disco"; if (drums == "trance") return "trance"; if (drums == "futurebass" || drums == "phonk" || drums == "grime") return "dub"; if (drums == "breaks") return "roller";
        if (drums.Contains("roller")) return "roller"; if (drums.Contains("jumpup")) return "jumpup"; if (drums.Contains("liquid")) return "liquid"; if (drums.Contains("neuro")) return "neuro"; if (drums.Contains("halftime")) return "halftime"; if (drums.Contains("minimal")) return "minimal";
        if (drums.Contains("ukg")) return "garage"; if (drums.Contains("bassline")) return "bassline"; if (drums.Contains("house")) return "house"; if (drums.Contains("dubstep") || drums.Contains("riddim") || drums.Contains("trap") || drums.Contains("drill")) return "dub"; if (drums.Contains("dark")) return "neuro"; if (drums.Contains("jungle")) return "roller"; return "roller";
    }

    public static List<double[]> BassLine(Rnd r, string rhythm, int bars, int rootMidi, bool minor, int vel = 100)
    {
        var notes = new List<double[]>(); string[] opts; if (!Rhythms.TryGetValue(rhythm, out opts)) opts = Rhythms["roller"]; string a = opts[r.Int(opts.Length)], b = opts[r.Int(opts.Length)];
        int[] third = minor ? new[] { 0, 0, 0, 3, 5, 7, 10, 12 } : new[] { 0, 0, 0, 4, 5, 7, 11, 12 };
        for (int bar = 0; bar < bars; bar++)
        {
            string pat = (bar % 2 == 0) ? a : b; if (bar == bars - 1 && bars >= 2) pat = b;
            for (int st = 0; st < 16; st++)
            {
                if (pat[st] != 'x') continue; int next = 16; for (int k = st + 1; k < 16; k++) if (pat[k] == 'x') { next = k; break; }
                int pitch = rootMidi + (st == 0 ? 0 : third[r.Int(third.Length)]); double len = Math.Min(next - st, rhythm == "liquid" || rhythm == "halftime" ? 6 : 3) * 0.25 * 0.92; notes.Add(new[] { (bar * 16 + st) * 0.25, len, pitch, st % 4 == 0 ? vel : vel - 12 });
            }
        }
        return notes;
    }
    // chord progressions (roots as semitones from the key): many, so two packs in one key do not play the same chords
    static readonly int[][] MinProg = { new[] { 0, 8, 3, 10 }, new[] { 0, 5, 8, 7 }, new[] { 0, 3, 8, 10 }, new[] { 0, 10, 8, 7 }, new[] { 0, 8, 10, 0 }, new[] { 0, 5, 10, 3 }, new[] { 0, 7, 8, 5 }, new[] { 0, 3, 10, 8 }, new[] { 0, 8, 5, 7 }, new[] { 0, 10, 5, 8 }, new[] { 0, 0, 8, 10 }, new[] { 0, 5, 3, 7 }, new[] { 8, 10, 0, 0 }, new[] { 0, 3, 5, 8 } };
    static readonly int[][] MajProg = { new[] { 0, 7, 9, 5 }, new[] { 0, 5, 9, 7 }, new[] { 0, 9, 5, 7 }, new[] { 0, 5, 7, 5 }, new[] { 0, 2, 5, 7 }, new[] { 0, 9, 2, 7 }, new[] { 0, 4, 5, 7 }, new[] { 0, 5, 0, 7 }, new[] { 5, 7, 0, 0 }, new[] { 0, 4, 9, 5 } };
    public static int[] ChordRoots(Rnd r, bool minor) { return minor ? MinProg[r.Int(MinProg.Length)] : MajProg[r.Int(MajProg.Length)]; }
    // the chord on a degree of the key, as it really is in that key (in a minor key the sixth and seventh are major, the second diminished)
    static int[] Quality(int deg, bool minor, int ext)
    {
        bool maj, dim = false; deg = ((deg % 12) + 12) % 12;
        if (minor) { maj = deg == 3 || deg == 8 || deg == 10; dim = deg == 2; } else { maj = deg == 0 || deg == 5 || deg == 7; dim = deg == 11; }
        int third = maj ? 4 : 3, fifth = dim ? 6 : 7; var t = new List<int> { 0, third, fifth };
        if (ext == 1) t.Add(maj && !(deg == 7 && !minor) ? 11 : 10); else if (ext == 2) t.Add(14); else if (ext == 3) { t.Add(maj ? 11 : 10); t.Add(14); }
        return t.ToArray();
    }
    static readonly string[] StabRhythms = { "x..x..x.x.......", "x.....x...x.....", "..x...x...x...x.", "x..x...x..x..x..", "x.x...x.x.x...x.", "x.......x..x...." };
    public static List<double[]> Chords(Rnd r, int bars, int rootPc, bool minor, int octave, string style)
    {
        var notes = new List<double[]>(); int[] roots = ChordRoots(r, minor); int ext = r.Int(4); string stab = StabRhythms[r.Int(StabRhythms.Length)]; double prevAvg = 12 * (octave + 1) + rootPc + 5;
        for (int bar = 0; bar < bars; bar++)
        {
            int deg = roots[bar % roots.Length], cr = 12 * (octave + 1) + rootPc + deg; var q = Quality(deg, minor, ext);
            // the inversion closest to the chord before (smooth voice leading, no jumping block chords)
            int[] best = null; double bd = 1e9; for (int inv = 0; inv < q.Length; inv++) { var v = new int[q.Length]; for (int k = 0; k < q.Length; k++) v[k] = cr + q[k] + (k < inv ? 12 : 0); foreach (int sh in new[] { -12, 0 }) { double avg = 0; foreach (int x in v) avg += x + sh; avg /= v.Length; if (Math.Abs(avg - prevAvg) < bd) { bd = Math.Abs(avg - prevAvg); best = Array.ConvertAll(v, x => x + sh); } } }
            double av = 0; foreach (int x in best) av += x; prevAvg = av / best.Length;
            if (style == "pad") foreach (int t in best) notes.Add(new[] { bar * 4.0, 3.9, (double)t, 80 });
            else for (int st = 0; st < 16; st++) if (stab[st] == 'x') foreach (int t in best) notes.Add(new[] { (bar * 16 + st) * 0.25, 0.22, (double)t, st == 0 ? 105 : 90 });
        }
        return notes;
    }
    public static List<double[]> Arpeggio(Rnd r, int bars, int rootPc, bool minor, int octave)
    {
        var notes = new List<double[]>(); int[] roots = ChordRoots(r, minor); int[] tones = minor ? new[] { 0, 3, 7, 10, 12, 15, 19 } : new[] { 0, 4, 7, 11, 12, 16, 19 }; int dir = 1, idx = 0;
        for (int bar = 0; bar < bars; bar++) { int cr = 12 * (octave + 1) + rootPc + roots[bar % roots.Length]; for (int st = 0; st < 16; st++) { if (r.Next() < 0.2f) continue; notes.Add(new[] { (bar * 16 + st) * 0.25, 0.2, cr + tones[idx], 85 + r.Int(25) }); idx += dir; if (idx >= tones.Length - 1 || idx <= 0) dir = -dir; idx = Math.Max(0, Math.Min(tones.Length - 1, idx)); } }
        return notes;
    }
    public static List<double[]> Melody(Rnd r, int bars, int rootPc, bool minor, int octave)
    {
        var notes = new List<double[]>(); int[] sc = minor ? MinPent : new[] { 0, 2, 4, 7, 9 }; int deg = 2; double t = 0;
        for (int bar = 0; bar < bars; bar++) { double barT = bar * 4.0; t = barT; while (t < barT + 3.99) { double len = r.Pick(new[] { 0.5, 0.5, 1.0, 0.75, 1.5 }); if (t + len > barT + 4) len = barT + 4 - t; if (r.Next() > 0.2f) { deg = Math.Max(0, Math.Min(sc.Length * 2 - 1, deg + r.Pick(new[] { -2, -1, -1, 0, 1, 1, 2 }))); int semi = sc[deg % sc.Length] + 12 * (deg / sc.Length); notes.Add(new[] { t, len * 0.9, 12 * (octave + 1) + rootPc + semi, 90 + r.Int(25) }); } t += len; } }
        return notes;
    }
    // a slide-y 808 line for drill / trap: {start, length, midi, vel, slideToMidi}
    public static List<double[]> Sub808Line(Rnd r, int bars, int rootMidi, bool drill)
    {
        var notes = new List<double[]>(); string[] pats = drill ? new[] { "x.....x.x.....x.", "x.......x.x.....", "x..x....x.....x." } : new[] { "x.......x..x....", "x.....x...x.....", "x..x....x......." };
        for (int bar = 0; bar < bars; bar++)
        {
            string p = pats[(bar + r.Int(2)) % pats.Length];
            for (int st = 0; st < 16; st++) { if (p[st] != 'x') continue; int next = 16; for (int k = st + 1; k < 16; k++) if (p[k] == 'x') { next = k; break; } int pitch = rootMidi + (st == 0 ? 0 : r.Pick(minorOffsets)); double slide = (drill && r.Next() < 0.6f) ? pitch + r.Pick(new[] { -5, -2, 2, 5, 7 }) : 0; notes.Add(new[] { (bar * 16 + st) * 0.25, (next - st) * 0.25 * 0.95, pitch, 105, slide }); }
        }
        return notes;
    }
    static readonly int[] minorOffsets = { 0, 0, 3, 5, 7, 10, 12 };
}
