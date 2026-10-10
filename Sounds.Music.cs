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
            case "riddim": baseName = "Riddim Chug"; extra = "f1cut=" + (int)r.Range(380, 520); break;
            case "organ": baseName = "Organ"; extra = "e1d=" + (int)r.Range(150, 300) + ";e1s=" + (int)r.Range(0, 30) + ";e1r=90;aoct=-1"; break;
            case "pluck": baseName = "Pluck Bass"; extra = "f1cut=" + (int)r.Range(260, 420); break;
            default: baseName = "Wobble Bass"; break;
        }
        string d = SynthPresets.DataOf(baseName) ?? ""; return d + ";" + extra;
    }
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
        { "dub", new[] { "x.......x.......", "x...x...x.x....." } },
    };
    public static string RhythmFor(string drums)
    {
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
    public static int[] ChordRoots(Rnd r, bool minor) { int[][] m = { new[] { 0, 8, 3, 10 }, new[] { 0, 5, 8, 7 }, new[] { 0, 3, 8, 10 } }, j = { new[] { 0, 7, 9, 5 }, new[] { 0, 5, 9, 7 } }; return minor ? m[r.Int(m.Length)] : j[r.Int(j.Length)]; }
    public static List<double[]> Chords(Rnd r, int bars, int rootPc, bool minor, int octave, string style)
    {
        var notes = new List<double[]>(); int[] roots = ChordRoots(r, minor); int[] tones = minor ? new[] { 0, 3, 7, 10 } : new[] { 0, 4, 7, 11 }; string stab = "x..x..x.x.......";
        for (int bar = 0; bar < bars; bar++)
        {
            int cr = 12 * (octave + 1) + rootPc + roots[bar % roots.Length];
            if (style == "pad") foreach (int t in tones) notes.Add(new[] { bar * 4.0, 3.9, cr + t, 80 });
            else for (int st = 0; st < 16; st++) if (stab[st] == 'x') foreach (int t in tones) notes.Add(new[] { (bar * 16 + st) * 0.25, 0.22, cr + t, st == 0 ? 105 : 90 });
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
