using System;
using System.Collections.Generic;
using System.IO;

static class PackBuilder
{
    // makes a pack's files on disk and returns the finished pack (null if cancelled)
    // the sound design version: packs made by an older one are offered an update (2 = band-limited hats, cymbals and saws, in-tune plucks, even loop levels)
    public const string SoundVersion = "4";   // 3: the big packs (260+ sounds each); 4: clean sounds (no clicks, crackle or distortion); installed packs offer an update
    public static bool Outdated(SoundPack p) { return p != null && p.Items.Count > 0 && p.Id != SoundLibrary.UserPackId && SoundCatalog.Find(p.Id) != null && p.Version != SoundVersion; }
    public static SoundPack Build(Recipe rec, Action<string, float> progress, Func<bool> cancel)
    {
        var pack = new SoundPack { Id = rec.Id, Name = rec.Name, Section = rec.Section, Genre = rec.Genre, Sub = rec.Sub, Description = rec.Desc, License = SoundLibrary.LicenceText, Hue = rec.Hue, Version = SoundVersion };
        try { if (Directory.Exists(pack.Dir)) Directory.Delete(pack.Dir, true); } catch { }
        var w = new PackWriter(pack, rec.Seed, progress, cancel);
        try
        {
            var st = StyleFor(rec); var kit = DrumKit.Make(st, rec.Seed + 1); var ctx = new Ctx { Rec = rec, W = w, Style = st, Kit = kit, R = new Rnd(rec.Seed + 7) };
            if (rec.Has("drums")) OneShots(ctx);
            if (rec.Has("sub808")) Subs(ctx);
            if (rec.Has("bass")) BassShots(ctx);
            if (rec.Has("loops")) { DrumLoops(ctx); BassLoops(ctx); Fills(ctx); }
            if (rec.Has("music")) MusicLoops(ctx);
            if (rec.Has("fx")) Effects(ctx);
            if (rec.Has("vocal")) Vocals(ctx);
            if (rec.Has("midi")) MidiSet(ctx);
            if (rec.Has("presets")) Presets(ctx);
            if (rec.Has("kit")) Songstarter(ctx);
            if (rec.Has("loops") || rec.Has("music")) Extra(ctx);
        }
        catch (OperationCanceledException) { try { Directory.Delete(pack.Dir, true); } catch { } return null; }
        pack.Installed = true; SoundLibrary.WritePack(pack); return pack;
    }

    class Ctx { public Recipe Rec; public PackWriter W; public DrumStyle Style; public DrumKit.Kit Kit; public Rnd R; }
    static string Tg(Ctx c, string extra) { return (c.Rec.Genre + "," + c.Rec.Sub + "," + extra + "," + c.Rec.Tags).ToLowerInvariant(); }
    static string KeyOf(Ctx c, int i) { return c.Rec.Keys[i % c.Rec.Keys.Length]; }
    static double BpmOf(Ctx c, int i) { return c.Rec.Bpms[i % c.Rec.Bpms.Length]; }
    static string N(Ctx c, string what, int i) { return c.Rec.Name + " - " + what + " " + (i + 1).ToString("00"); }
    static string Kn(string key) { int r; bool m; SoundLibrary.ParseKey(key, out r, out m); return SoundLibrary.KeyName(r, m); }

    // ---- the drum character of a pack ----
    public static DrumStyle StyleFor(Recipe r)
    {
        var s = new DrumStyle { Pattern = r.Drums }; var j = new Rnd(r.Seed + 3); string d = r.Drums;
        if (d.StartsWith("dnb_jumpup")) { s.KickF0 = 58; s.KickStart = 290; s.KickDecay = 0.27; s.KickDrive = 3.2; s.SnareBody = 215; s.SnareNoise = 4200; s.SnareDrive = 2.4; s.HatBright = 11; }
        else if (d.StartsWith("dnb_liquid")) { s.KickF0 = 52; s.KickStart = 200; s.KickDecay = 0.3; s.KickDrive = 1.4; s.KickClick = 0.25; s.SnareBody = 185; s.SnareAmt = 0.75; s.SnareDrive = 1.3; s.SnareDecay = 0.17; s.HatBright = 9; }
        else if (d.StartsWith("dnb_dark") || d.StartsWith("dnb_neuro")) { s.KickF0 = 49; s.KickStart = 210; s.KickDecay = 0.32; s.KickDrive = 2.9; s.KickClick = 0.6; s.SnareBody = 178; s.SnareNoise = 2800; s.SnareDrive = 2.6; s.HatBright = 9.5; }
        else if (d.StartsWith("dnb_roller")) { s.KickF0 = 54; s.KickStart = 235; s.KickDecay = 0.23; s.KickDrive = 2.5; s.SnareBody = 200; s.SnareNoise = 3600; }
        else if (d.StartsWith("dnb_halftime")) { s.KickF0 = 50; s.KickStart = 200; s.KickDecay = 0.4; s.KickDrive = 2.3; s.SnareBody = 170; s.SnareDecay = 0.2; }
        else if (d.StartsWith("dnb_rave")) { s.KickF0 = 60; s.KickStart = 260; s.KickDecay = 0.2; s.KickDrive = 2.2; s.SnareBody = 230; s.SnareNoise = 4500; }
        else if (d == "jungle") { s.KickF0 = 57; s.KickStart = 180; s.KickDecay = 0.2; s.KickDrive = 1.8; s.SnareBody = 235; s.SnareNoise = 3300; s.SnareDecay = 0.17; s.Swing = 0.52; }
        else if (d.StartsWith("ukg")) { s.KickF0 = 58; s.KickStart = 210; s.KickDecay = 0.2; s.KickDrive = 1.8; s.SnareBody = 205; s.SnareAmt = 0.8; s.Swing = 0.58; s.ClapCentre = 1100; }
        else if (d.StartsWith("bassline") || d == "house") { s.KickF0 = 56; s.KickStart = 200; s.KickDecay = 0.26; s.KickDrive = 2.7; s.ClapCentre = 1300; s.Swing = d == "house" ? 0.52 : 0.56; s.HatBright = 10.5; }
        else if (d == "trap") { s.KickF0 = 48; s.KickStart = 160; s.KickDecay = 0.34; s.KickDrive = 2.2; s.SnareBody = 195; s.SnareNoise = 4000; s.HatC = 0.035; s.HatBright = 11.5; s.ClapCentre = 1400; }
        else if (d == "drill") { s.KickF0 = 46; s.KickStart = 150; s.KickDecay = 0.4; s.KickDrive = 2.6; s.SnareBody = 175; s.SnareNoise = 3000; s.SnareDrive = 2.2; s.HatC = 0.03; s.HatBright = 10; }
        else if (d == "boombap") { s.KickF0 = 58; s.KickStart = 170; s.KickDecay = 0.3; s.KickDrive = 1.6; s.SnareBody = 175; s.SnareAmt = 0.8; s.SnareDecay = 0.19; s.SnareDrive = 1.5; s.Swing = 0.6; s.HatBright = 8.5; }
        else if (d == "dubstep" || d == "riddim") { s.KickF0 = 50; s.KickStart = 190; s.KickDecay = 0.32; s.KickDrive = 3; s.SnareBody = 180; s.SnareNoise = 3000; s.SnareDrive = 2.8; s.SnareLen = 0.5; }
        else if (d == "afro") { s.KickF0 = 55; s.KickStart = 170; s.KickDecay = 0.24; s.KickDrive = 1.5; s.ClapCentre = 1250; s.Swing = 0.56; s.HatBright = 9.5; }
        else if (d == "amapiano") { s.KickF0 = 52; s.KickStart = 150; s.KickDecay = 0.3; s.KickDrive = 1.3; s.KickClick = 0.25; s.ClapCentre = 1150; s.Swing = 0.55; s.HatBright = 9; }
        else if (d == "dembow" || d == "dancehall") { s.KickF0 = 56; s.KickStart = 190; s.KickDecay = 0.24; s.KickDrive = 2.0; s.SnareBody = 215; s.SnareNoise = 4200; s.SnareDecay = 0.12; s.HatBright = 10; }
        else if (d == "techno") { s.KickF0 = 50; s.KickStart = 230; s.KickDecay = 0.33; s.KickDrive = 3.0; s.KickClick = 0.7; s.ClapCentre = 1500; s.HatC = 0.03; s.HatBright = 11; }
        else if (d == "techhouse" || d == "deephouse") { s.KickF0 = 53; s.KickStart = 210; s.KickDecay = 0.28; s.KickDrive = 2.4; s.ClapCentre = 1300; s.Swing = d == "deephouse" ? 0.55 : 0.53; s.HatBright = 10; }
        else if (d == "lofi") { s.KickF0 = 56; s.KickStart = 150; s.KickDecay = 0.26; s.KickDrive = 1.2; s.KickClick = 0.15; s.SnareBody = 180; s.SnareAmt = 0.7; s.SnareDecay = 0.2; s.SnareDrive = 1.1; s.Swing = 0.62; s.HatBright = 7.5; }
        else if (d == "rnb") { s.KickF0 = 50; s.KickStart = 160; s.KickDecay = 0.36; s.KickDrive = 1.6; s.SnareBody = 190; s.SnareAmt = 0.8; s.Swing = 0.57; s.ClapCentre = 1200; s.HatBright = 9; }
        else if (d == "pop" || d == "disco") { s.KickF0 = 55; s.KickStart = 200; s.KickDecay = 0.26; s.KickDrive = 2.0; s.ClapCentre = 1350; s.SnareBody = 205; s.HatBright = 10.5; s.Swing = d == "disco" ? 0.53 : 0.5; }
        else if (d == "phonk") { s.KickF0 = 46; s.KickStart = 170; s.KickDecay = 0.42; s.KickDrive = 3.4; s.SnareBody = 185; s.SnareNoise = 3500; s.SnareDrive = 2.6; s.HatC = 0.03; s.HatBright = 10.5; }
        else if (d == "jersey") { s.KickF0 = 54; s.KickStart = 220; s.KickDecay = 0.22; s.KickDrive = 2.8; s.SnareBody = 220; s.SnareNoise = 4300; s.HatBright = 10.5; }
        else if (d == "hyperpop") { s.KickF0 = 52; s.KickStart = 260; s.KickDecay = 0.3; s.KickDrive = 3.6; s.SnareBody = 230; s.SnareNoise = 5000; s.SnareDrive = 3.0; s.HatC = 0.028; s.HatBright = 12; }
        else if (d == "grime") { s.KickF0 = 50; s.KickStart = 200; s.KickDecay = 0.3; s.KickDrive = 2.4; s.SnareBody = 200; s.SnareNoise = 3800; s.SnareDrive = 2.2; s.HatBright = 10; }
        else if (d == "baile") { s.KickF0 = 54; s.KickStart = 200; s.KickDecay = 0.26; s.KickDrive = 2.6; s.SnareBody = 210; s.SnareNoise = 4000; s.ClapCentre = 1400; s.HatBright = 10; }
        else if (d == "trance") { s.KickF0 = 52; s.KickStart = 250; s.KickDecay = 0.25; s.KickDrive = 2.6; s.ClapCentre = 1400; s.HatC = 0.03; s.HatBright = 11; }
        else if (d == "futurebass") { s.KickF0 = 50; s.KickStart = 220; s.KickDecay = 0.34; s.KickDrive = 2.4; s.SnareBody = 210; s.SnareNoise = 4200; s.SnareLen = 0.5; s.HatBright = 10.5; }
        else if (d == "breaks") { s.KickF0 = 57; s.KickStart = 180; s.KickDecay = 0.22; s.KickDrive = 1.9; s.SnareBody = 225; s.SnareNoise = 3500; s.Swing = 0.54; s.HatBright = 9.5; }
        s.KickF0 *= 0.97 + j.Next() * 0.06; s.SnareBody *= 0.96 + j.Next() * 0.08; s.HatBright *= 0.96 + j.Next() * 0.08; return s;
    }

    // ---- one-shots ----
    static void OneShots(Ctx c)
    {
        var k = c.Kit; var w = c.W; string key = KeyOf(c, 0);
        for (int i = 0; i < k.Kicks.Count; i++) w.Audio(k.Kicks[i], "Drums/Kicks", N(c, "Kick " + k.KickNames[i], i), "Kick", "Drums", "one-shot", 0, "", Tg(c, "kick,drums,one-shot"), c.Rec.Mood, -1.0);
        for (int i = 0; i < k.Snares.Count; i++) w.Audio(k.Snares[i], "Drums/Snares", N(c, "Snare " + k.SnareNames[i], i), "Snare", "Drums", "one-shot", 0, "", Tg(c, "snare,drums,one-shot"), c.Rec.Mood, -1.0);
        for (int i = 0; i < k.Claps.Count; i++) w.Audio(k.Claps[i], "Drums/Claps", N(c, "Clap", i), "Clap", "Drums", "one-shot", 0, "", Tg(c, "clap,drums,one-shot"), c.Rec.Mood, -1.0);
        for (int i = 0; i < 3; i++) w.Audio(k.HatsC[i], "Drums/Hats", N(c, "Hat Closed", i), "Hi-Hat", "Drums", "one-shot", 0, "", Tg(c, "hat,hihat,closed,drums,one-shot"), c.Rec.Mood, -1.5);
        for (int i = 0; i < k.HatsO.Count; i++) w.Audio(k.HatsO[i], "Drums/Hats", N(c, "Hat Open", i), "Open Hat", "Drums", "one-shot", 0, "", Tg(c, "hat,hihat,open,drums,one-shot"), c.Rec.Mood, -1.5);
        for (int i = 0; i < k.Perc.Count; i++) w.Audio(k.Perc[i], "Drums/Percussion", N(c, "Perc " + k.PercNames[i], i), "Percussion", "Drums", "one-shot", 0, "", Tg(c, "percussion,perc," + k.PercNames[i].ToLowerInvariant() + ",drums,one-shot"), c.Rec.Mood, -2.0);
        w.Audio(k.Rides[0], "Drums/Cymbals", N(c, "Ride", 0), "Ride", "Drums", "one-shot", 0, "", Tg(c, "ride,cymbal,drums"), c.Rec.Mood, -2.0); w.Audio(k.Rides[1], "Drums/Cymbals", N(c, "Crash", 0), "Cymbal", "Drums", "one-shot", 0, "", Tg(c, "crash,cymbal,drums"), c.Rec.Mood, -2.0);
    }

    // ---- 808 and sub hits tuned to the pack's key ----
    static void Subs(Ctx c)
    {
        var w = c.W; var r = new Rnd(c.Rec.Seed + 11);
        for (int i = 0; i < 4; i++)
        {
            string key = KeyOf(c, i); int pc = Music.Root(key), midi = 24 + pc; double dec = 0.5 + i * 0.35, drv = 1.1 + i * 0.2;   // clean 808s: a little warmth, no distortion
            var b = SoundGen.Eight08(r, midi, dec, drv, 0.15 + i * 0.1, i == 3 ? 5 : 2, 0.9 + dec * 1.3); string kn = Kn(key);
            w.Audio(b, "Basses/808 and Subs", N(c, (i == 0 ? "808 Clean" : i == 1 ? "808 Long" : i == 2 ? "808 Warm" : "808 Punchy") + " " + Music.NoteName(midi), i), i < 3 ? "808" : "808", "Bass", "one-shot", 0, kn, Tg(c, "808,sub,bass,one-shot," + Music.NoteName(midi).ToLowerInvariant()), c.Rec.Mood, -1.0);
        }
        for (int i = 0; i < 2; i++)
        {
            string key = KeyOf(c, i); int midi = 24 + Music.Root(key); var notes = new List<double[]> { new[] { 0, 2.5, (double)midi + 12, 110 } };
            var b = SoundGen.RenderSynth(Music.BassPatch("sub", r), notes, BpmOf(c, 0), 3.0, 0.4); w.Audio(b, "Basses/808 and Subs", N(c, "Sub Bass " + Music.NoteName(midi + 12), i), "Sub Bass", "Bass", "one-shot", 0, Kn(key), Tg(c, "sub,bass,one-shot"), c.Rec.Mood, -1.0);
        }
    }

    // ---- bass one-shots: each flavour, dry and with processing ----
    static void BassShots(Ctx c)
    {
        var w = c.W; var r = new Rnd(c.Rec.Seed + 13); int n = 0;
        foreach (var fl in c.Rec.Flavours)
        {
            string key = KeyOf(c, n); int midi = 36 + Music.Root(key); double bpm = BpmOf(c, 0);
            for (int v = 0; v < 2; v++)
            {
                int p = midi + (v == 0 ? 0 : (Music.IsMinor(key) ? 7 : 7)); var notes = new List<double[]> { new[] { 0, 1.3 * bpm / 60, (double)p, 105 } };
                var b = SoundGen.RenderSynth(Music.BassPatch(fl, r), notes, bpm, 1.5 * bpm / 60, 0.45); string nm = Cap(fl) + " Bass " + Music.NoteName(p);
                w.Audio(b, "Basses/One-Shots", N(c, nm + (v == 0 ? " dry" : " dry B"), n * 2 + v), BassType(fl), "Bass", "one-shot", 0, Kn(key), Tg(c, fl + ",bass,one-shot,dry"), c.Rec.Mood, -1.0);
                if (v == 0) { var pr = SoundGen.Wet(b.Copy(), 40, 55, 0.22f, b.Length + 20000); SoundGen.Glue(pr, 1.6f); w.Audio(pr, "Basses/One-Shots", N(c, nm + " processed", n), BassType(fl), "Bass", "one-shot", 0, Kn(key), Tg(c, fl + ",bass,one-shot,processed"), c.Rec.Mood, -1.0); }
            }
            n++;
        }
    }
    static string Cap(string s) { return s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1); }
    public static string BassType(string fl)
    {
        switch (fl) { case "reese": return "Reese Bass"; case "wobble": return "Wobble Bass"; case "growl": case "dirty": return "Growl Bass"; case "talking": return "Talking Bass"; case "rubber": case "elastic": return "Rubber Bass"; case "stab": case "organ": return "Bass Stab"; case "sub": return "Sub Bass"; default: return "Bass One-Shot"; }
    }

    // ---- loops ----
    static void DrumLoops(Ctx c)
    {
        var w = c.W; var st = c.Style; var p = st.Pat; int n = 0;
        for (int i = 0; i < 6; i++)
        {
            double bpm = BpmOf(c, i); int bars = (i % 3 == 2) ? 4 : 2; double sw = st.Swing + (i % 2 == 0 ? 0 : 0.015); var lp = DrumKit.Render(c.Kit, st, p, bpm, bars, sw, c.Rec.Seed + 100 + i, false);
            string nm = N(c, "Drum Loop " + bpm.ToString("0") + " BPM", i); w.Audio(lp.Mix, "Loops/Drum Loops", nm, "Drum Loop", "Loops", "loop", bpm, "", Tg(c, "drum loop,drums,loop," + bpm.ToString("0") + "bpm"), c.Rec.Mood, -1.5);
            if (i == 0 || i == 3) { string sn = c.Rec.Name + " - Drum Loop " + bpm.ToString("0") + " BPM"; w.Audio(lp.Kick, "Loops/Drum Stems", sn + " (kick stem)", "Stem", "Loops", "stem", bpm, "", Tg(c, "stem,kick,drums,loop"), c.Rec.Mood, -1.5); w.Audio(lp.Snare, "Loops/Drum Stems", sn + " (snare stem)", "Stem", "Loops", "stem", bpm, "", Tg(c, "stem,snare,drums,loop"), c.Rec.Mood, -1.5); w.Audio(lp.Hats, "Loops/Drum Stems", sn + " (hats stem)", "Stem", "Loops", "stem", bpm, "", Tg(c, "stem,hats,drums,loop"), c.Rec.Mood, -1.5); }
            if (i == 1 || i == 4) { var top = new Buf(lp.Mix.Length); top.Add(lp.Hats, 0, 1f); top.Add(lp.Perc, 0, 0.9f); w.Audio(top, "Loops/Top Loops", N(c, "Top Loop " + bpm.ToString("0") + " BPM", n), "Top Loop", "Loops", "loop", bpm, "", Tg(c, "top loop,hats,percussion,loop"), c.Rec.Mood, -2.0); var ks = new Buf(lp.Mix.Length); ks.Add(lp.Kick, 0, 1f); ks.Add(lp.Snare, 0, 0.95f); w.Audio(ks, "Loops/Drum Loops", N(c, "Kick and Snare Loop " + bpm.ToString("0") + " BPM", n), "Drum Loop", "Loops", "loop", bpm, "", Tg(c, "kick snare loop,drums,loop"), c.Rec.Mood, -1.5); n++; }
        }
    }
    static void Fills(Ctx c)
    {
        var w = c.W; for (int i = 0; i < 3; i++) { double bpm = BpmOf(c, i); var lp = DrumKit.Render(c.Kit, c.Style, c.Style.Pat, bpm, 2, c.Style.Swing, c.Rec.Seed + 200 + i, true); w.Audio(lp.Mix, "Loops/Drum Fills", N(c, "Drum Fill " + bpm.ToString("0") + " BPM", i), "Drum Fill", "Loops", "loop", bpm, "", Tg(c, "fill,drum fill,drums,transition"), c.Rec.Mood, -1.5); }
    }
    static void BassLoops(Ctx c)
    {
        var w = c.W; var r = new Rnd(c.Rec.Seed + 17); int i = 0; string rh = Music.RhythmFor(c.Rec.Drums);
        foreach (var fl in c.Rec.Flavours)
        {
            if (i >= 6) break; double bpm = BpmOf(c, i); string key = KeyOf(c, i); int root = 36 + Music.Root(key); bool minor = Music.IsMinor(key);
            var notes = Music.BassLine(r, rh, 2, root, minor); var b = SoundGen.RenderSynth(Music.BassPatch(fl, r), notes, bpm, 8, 0.25);
            string nm = N(c, Cap(fl) + " Bass Loop " + bpm.ToString("0") + " BPM " + Kn(key), i); b = LoopLen(b, 8, bpm);
            w.Audio(b, "Basses/Bass Loops", nm, "Bass Loop", "Bass", "loop", bpm, Kn(key), Tg(c, fl + ",bass loop,bass,loop," + Kn(key).ToLowerInvariant()), c.Rec.Mood, -1.5);
            if (i < 3 && c.Rec.Has("midi")) w.Midi("MIDI/Basslines", N(c, "MIDI " + Cap(fl) + " Bassline " + bpm.ToString("0") + " BPM " + Kn(key), i), "MIDI Bassline", bpm, Kn(key), Tg(c, "midi,bassline," + fl), c.Rec.Mood, notes);
            i++;
        }
    }
    // loops are cut to exactly their length in beats so they repeat without a gap
    internal static Buf LoopLen(Buf b, double beats, double bpm) { int n = (int)Math.Round(beats * 60.0 / bpm * SoundGen.SR); var o = b.Trim(n); int tail = Math.Min(Math.Max(0, b.Length - n), SoundGen.SR * 2); for (int k = 0; k < tail && k < n; k++) { o.L[k] += b.L[n + k]; o.R[k] += b.R[n + k]; } return o; }

    static void MusicLoops(Ctx c)
    {
        var w = c.W; var r = new Rnd(c.Rec.Seed + 19); bool dark = c.Rec.Mood.IndexOf("dark", StringComparison.OrdinalIgnoreCase) >= 0 || c.Rec.Mood.IndexOf("aggress", StringComparison.OrdinalIgnoreCase) >= 0;
        string[] padNames = dark ? new[] { "Dark Pad", "Dark Pad", "Soft Pad" } : new[] { "Soft Pad", "Shimmer Pad", "Dark Pad" };
        for (int i = 0; i < 3; i++)
        {
            double bpm = BpmOf(c, i); string key = KeyOf(c, i); int pc = Music.Root(key); bool minor = Music.IsMinor(key); var notes = Music.Chords(r, 4, pc, minor, 3, "pad");
            var b = SoundGen.RenderSynth(Music.LeadPatch(padNames[i]), notes, bpm, 16, 1.0); b = LoopLen(b, 16, bpm); w.Audio(b, "Synths/Chord Loops", N(c, "Chord Loop " + bpm.ToString("0") + " BPM " + Kn(key), i), "Chord Loop", "Synth", "loop", bpm, Kn(key), Tg(c, "chords,pad,chord loop,loop,synth"), c.Rec.Mood, -2.0);
            var stabs = Music.Chords(r, 4, pc, minor, 3, "stab"); var sb = SoundGen.RenderSynth(Music.LeadPatch(i == 1 ? "Supersaw Lead" : "Organ", "voices=16;e1a=9;e1d=180;e1s=0;e1r=100"), stabs, bpm, 16, 0.4); sb = LoopLen(sb, 16, bpm);
            w.Audio(sb, "Synths/Stab Loops", N(c, (i == 1 ? "Rave Stab Loop " : "Organ Stab Loop ") + bpm.ToString("0") + " BPM " + Kn(key), i), i == 1 ? "Rave Stab" : "Organ Stab", "Synth", "loop", bpm, Kn(key), Tg(c, (i == 1 ? "rave stab" : "organ stab") + ",stab,loop,synth"), c.Rec.Mood, -2.0);
        }
        for (int i = 0; i < 2; i++)
        {
            double bpm = BpmOf(c, i); string key = KeyOf(c, i); int pc = Music.Root(key); bool minor = Music.IsMinor(key);
            var arp = SoundGen.RenderSynth(Music.LeadPatch(i == 0 ? "Square Lead" : "FM Bell Lead"), Music.Arpeggio(r, 4, pc, minor, 4), bpm, 16, 0.8); arp = LoopLen(arp, 16, bpm); w.Audio(arp, "Synths/Synth Loops", N(c, "Arp Loop " + bpm.ToString("0") + " BPM " + Kn(key), i), "Synth Loop", "Synth", "loop", bpm, Kn(key), Tg(c, "arp,synth loop,loop,synth"), c.Rec.Mood, -2.0);
            var mel = SoundGen.RenderSynth(Music.LeadPatch(i == 0 ? "Mono Saw Lead" : "Electric Keys"), Music.Melody(r, 4, pc, minor, 4), bpm, 16, 0.8); mel = LoopLen(mel, 16, bpm); w.Audio(mel, "Synths/Melody Loops", N(c, "Melody Loop " + bpm.ToString("0") + " BPM " + Kn(key), i), "Melody Loop", "Synth", "loop", bpm, Kn(key), Tg(c, "melody,synth loop,loop,synth"), c.Rec.Mood, -2.0);
            var pl = new Buf((int)(16 * 60.0 / bpm * SoundGen.SR + 2 * SoundGen.SR)); var gr = new Rnd(c.Rec.Seed + 31 + i); int[] sc = minor ? Music.MinPent : new[] { 0, 2, 4, 7, 9 }; double stepS = 60.0 / bpm * SoundGen.SR / 2;
            for (int st = 0; st < 32; st++) { if (gr.Next() < 0.3f) continue; int midi = 12 * 4 + pc + sc[gr.Int(sc.Length)] + (gr.Next() < 0.3f ? 12 : 0); pl.Add(SoundGen.Pluck(gr, SoundGen.NoteHz(midi), 1.4, 0.7), (int)(st * stepS), 0.5f); }
            pl = SoundGen.Wet(pl, 45, 50, 0.2f, pl.Length); pl = LoopLen(pl, 16, bpm); w.Audio(pl, "Synths/Guitar and Pluck Loops", N(c, "Pluck Loop " + bpm.ToString("0") + " BPM " + Kn(key), i), "Guitar Loop", "Synth", "loop", bpm, Kn(key), Tg(c, "guitar,pluck,string,loop,melody"), c.Rec.Mood, -2.0);
        }
        for (int i = 0; i < 2; i++) { string key = KeyOf(c, i); int pc = Music.Root(key); var at = SoundGen.Atmosphere(r, 12, 48 + pc, Music.IsMinor(key), 0.6 + i * 0.3); w.Audio(at, "Synths/Atmospheres", N(c, "Atmosphere " + Kn(key), i), "Atmosphere", "Synth", "one-shot", 0, Kn(key), Tg(c, "atmosphere,pad,texture,ambient"), c.Rec.Mood, -3.0); }
        SoundGen.Crackle(r, 10, 0.5);   // (made and thrown away so the sounds after it stay the same; packs have no crackle)
    }

    static void Effects(Ctx c)
    {
        var w = c.W; var r = new Rnd(c.Rec.Seed + 23); double bpm = BpmOf(c, 0); double bar = 4 * 60.0 / bpm;
        for (int i = 0; i < 2; i++) w.Audio(SoundGen.Riser(r, bar * (i == 0 ? 4 : 2), 300 + i * 150, 9000, i == 0), "FX/Risers", N(c, "Riser " + (i == 0 ? "4 bars" : "2 bars"), i), "Riser", "FX", "one-shot", bpm, "", Tg(c, "riser,build,fx,transition"), c.Rec.Mood, -1.5);
        var dn = SoundGen.Riser(r, bar * 2, 300, 9000, true); dn.Reverse(); w.Audio(dn, "FX/Downlifters", N(c, "Downlifter 2 bars", 0), "Downlifter", "FX", "one-shot", bpm, "", Tg(c, "downlifter,fx,transition"), c.Rec.Mood, -1.5);
        for (int i = 0; i < 2; i++) w.Audio(SoundGen.Impact(r, 3.2 + i, 52 + i * 12, 1.8 + i), "FX/Impacts", N(c, "Impact", i), "Impact", "FX", "one-shot", 0, "", Tg(c, "impact,hit,boom,fx,drop"), c.Rec.Mood, -1.0);
        w.Audio(SoundGen.Sweep(r, bar, 400, 12000, 2.5), "FX/Sweeps", N(c, "Sweep Up", 0), "Sweep", "FX", "one-shot", bpm, "", Tg(c, "sweep,fx,transition"), c.Rec.Mood, -2.0);
        var sd = SoundGen.Sweep(r, bar, 12000, 400, 2.5); w.Audio(sd, "FX/Sweeps", N(c, "Sweep Down", 1), "Sweep", "FX", "one-shot", bpm, "", Tg(c, "sweep,fx,transition"), c.Rec.Mood, -2.0);
        var rv = SoundGen.Impact(r, 3.0, 60, 1.6); rv.Reverse(); w.Audio(rv, "FX/Reverses", N(c, "Reverse Impact", 0), "Reverse", "FX", "one-shot", 0, "", Tg(c, "reverse,fx,transition,build"), c.Rec.Mood, -1.5);
        var cr = SoundGen.Wet(SoundGen.Cymbal(r, 1.6, 3.0, false), 80, 40, 0.5f, (int)(4.5 * SoundGen.SR)); cr.Reverse(); w.Audio(cr, "FX/Reverses", N(c, "Reverse Crash", 1), "Reverse", "FX", "one-shot", 0, "", Tg(c, "reverse,crash,fx,transition"), c.Rec.Mood, -2.0);
        w.Audio(SoundGen.Siren(2.4, 700, 1500, 0.8), "FX/Sirens", N(c, "Siren", 0), "Siren", "FX", "one-shot", 0, "", Tg(c, "siren,alarm,fx,rave"), c.Rec.Mood, -3.0); w.Audio(SoundGen.Siren(3.2, 400, 2200, 0.5), "FX/Sirens", N(c, "Siren Wide", 1), "Siren", "FX", "one-shot", 0, "", Tg(c, "siren,alarm,fx,rave"), c.Rec.Mood, -3.0);
        var tr = new Buf((int)((bar * 2 + 3.5) * SoundGen.SR)); tr.Add(SoundGen.Riser(r, bar * 2, 250, 10000, true), 0, 1f); tr.Add(SoundGen.Impact(r, 3.5, 55, 2.2), (int)(bar * 2 * SoundGen.SR), 1f); w.Audio(tr, "FX/Transitions", N(c, "Drop Transition (build into impact)", 0), "Transition", "FX", "one-shot", bpm, "", Tg(c, "transition,drop,build,impact,fx"), c.Rec.Mood, -1.0);
        w.Audio(SoundGen.Sweep(r, 1.2, 800, 6000, 1.5), "FX/Drop FX", N(c, "Whoosh", 0), "Drop FX", "FX", "one-shot", 0, "", Tg(c, "whoosh,drop,fx"), c.Rec.Mood, -2.0);
    }

    static void Vocals(Ctx c)
    {
        var w = c.W; var r = new Rnd(c.Rec.Seed + 29); double bpm = BpmOf(c, 0); string tags = ",synth voice,formant,vocal";
        Func<string, double, int, int, double, double, string, Buf> syl = (nm, f0, v1, v2, len, bend, cons) => SoundGen.VocalSyllable(r, f0 * r.Range(0.93f, 1.08f), v1, v2, len, bend, 1, cons);
        var shouts = new[] { new object[] { "HEY", 230.0, 1, 2, 0.34, 0.15, "h" }, new object[] { "YO", 190.0, 3, 4, 0.42, -0.1, "" }, new object[] { "OH", 210.0, 3, 4, 0.5, 0.05, "" }, new object[] { "UH", 150.0, 0, 0, 0.26, -0.15, "" }, new object[] { "AYY", 240.0, 0, 2, 0.45, 0.1, "" }, new object[] { "YEAH", 200.0, 1, 0, 0.5, -0.05, "" } };
        int i = 0; foreach (var s in shouts) { var b = syl((string)s[0], (double)s[1], (int)s[2], (int)s[3], (double)s[4], (double)s[5], (string)s[6]); b = SoundGen.Wet(b, 35, 50, 0.18f, b.Length + 12000); w.Audio(b, "Vocals/Shouts", N(c, "Synth Voice Shout " + (string)s[0], i), "Vocal Shout", "Vocal", "one-shot", 0, "", Tg(c, "shout,vocal shout,ad-lib" + tags), c.Rec.Mood, -1.5); i++; }
        // chant: a group of four detuned voices
        var ch = new Buf((int)(2 * 4 * 60.0 / bpm * SoundGen.SR)); double beat = 60.0 / bpm * SoundGen.SR; for (int k = 0; k < 8; k++) ch.Add(syl("HEY", 220 + (k % 2) * 30, 1, 2, 0.28, 0.1, "h"), (int)(k * beat), 0.8f); w.Audio(LoopLen(ch, 8, bpm), "Vocals/Chants", N(c, "Synth Voice Chant " + bpm.ToString("0") + " BPM", 0), "Vocal Chant", "Vocal", "loop", bpm, "", Tg(c, "chant,crowd,vocal chant" + tags), c.Rec.Mood, -2.0);
        // chop loops: syllables on a melody in the key
        for (int v = 0; v < 2; v++)
        {
            string key = KeyOf(c, v); int pc = Music.Root(key); bool minor = Music.IsMinor(key); var mel = Music.Melody(r, 2, pc, minor, 4); var lp = new Buf((int)(8 * 60.0 / bpm * SoundGen.SR + 20000));
            int vi = 0; foreach (var n in mel) { double hz = SoundGen.NoteHz((int)n[2]); lp.Add(SoundGen.VocalSyllable(r, hz, (vi++) % 5, -1, Math.Min(0.6, n[1] * 60.0 / bpm * 0.95), 0, 0, ""), (int)(n[0] * beat), 0.6f); }
            lp = SoundGen.Wet(lp, 50, 50, 0.2f, lp.Length); lp = LoopLen(lp, 8, bpm); w.Audio(lp, "Vocals/Chops", N(c, "Synth Voice Chop Loop " + bpm.ToString("0") + " BPM " + Kn(key), v), "Vocal Chop", "Vocal", "loop", bpm, Kn(key), Tg(c, "vocal chop,chop,loop" + tags), c.Rec.Mood, -2.0);
        }
        var morph = SoundGen.VocalSyllable(r, 140, 0, 4, 2.4, 0.9, 1, ""); w.Audio(SoundGen.Wet(morph, 60, 40, 0.3f, morph.Length + 30000), "Vocals/Vocal FX", N(c, "Synth Voice Rise", 0), "Vocal FX", "Vocal", "one-shot", 0, "", Tg(c, "vocal fx,rise,transition" + tags), c.Rec.Mood, -2.0);
    }

    static void MidiSet(Ctx c)
    {
        var w = c.W; var r = new Rnd(c.Rec.Seed + 37); string rh = Music.RhythmFor(c.Rec.Drums);
        for (int i = 0; i < 3; i++) { string key = KeyOf(c, i); double bpm = BpmOf(c, i); w.Midi("MIDI/Basslines", N(c, "MIDI Bassline " + bpm.ToString("0") + " BPM " + Kn(key), i), "MIDI Bassline", bpm, Kn(key), Tg(c, "midi,bassline"), c.Rec.Mood, Music.BassLine(r, rh, 4, 36 + Music.Root(key), Music.IsMinor(key))); }
        for (int i = 0; i < 2; i++) { string key = KeyOf(c, i); double bpm = BpmOf(c, i); w.Midi("MIDI/Chords", N(c, "MIDI Chords " + bpm.ToString("0") + " BPM " + Kn(key), i), "MIDI Chords", bpm, Kn(key), Tg(c, "midi,chords"), c.Rec.Mood, Music.Chords(r, 4, Music.Root(key), Music.IsMinor(key), 3, i == 0 ? "pad" : "stab")); }
        { string key = KeyOf(c, 0); double bpm = BpmOf(c, 0); w.Midi("MIDI/Melodies", N(c, "MIDI Melody " + bpm.ToString("0") + " BPM " + Kn(key), 0), "MIDI Melody", bpm, Kn(key), Tg(c, "midi,melody"), c.Rec.Mood, Music.Melody(r, 4, Music.Root(key), Music.IsMinor(key), 4)); }
        // the drum pattern as a General MIDI drum file (kick 36, snare 38, clap 39, closed hat 42, open hat 46)
        { var p = c.Style.Pat; var nt = new List<double[]>(); for (int st = 0; st < 16 * Math.Max(2, p.Bars * 2); st++) { int pi = st % (16 * p.Bars); Action<string, int, double> add = (s2, note, vel) => { if (pi < s2.Length && s2[pi] != '.') nt.Add(new[] { st * 0.25, 0.2, (double)note, vel }); }; add(p.K, 36, 110); add(p.S, 38, 105); add(p.C, 39, 100); add(p.H, 42, 80); add(p.O, 46, 85); add(p.G, 38, 40); }
          w.Midi("MIDI/Drums", N(c, "MIDI Drums " + BpmOf(c, 0).ToString("0") + " BPM", 0), "MIDI Drums", BpmOf(c, 0), "", Tg(c, "midi,drums,pattern"), c.Rec.Mood, nt); }
        if (c.Rec.Has("sub808"))
        {
            bool drill = c.Rec.Drums == "drill" || c.Rec.Tags.Contains("drill"); string key = KeyOf(c, 0); double bpm = BpmOf(c, 0); var line = Music.Sub808Line(r, 4, 24 + Music.Root(key), drill); var nt = new List<double[]>();
            foreach (var n in line) { nt.Add(new[] { n[0], n[1], n[2], n[3] }); }
            w.Midi("MIDI/Basslines", N(c, (drill ? "MIDI Sliding 808 " : "MIDI 808 Line ") + bpm.ToString("0") + " BPM " + Kn(key), 0), "MIDI Bassline", bpm, Kn(key), Tg(c, "midi,808,bassline," + (drill ? "slide" : "sub")), c.Rec.Mood, nt);
        }
    }

    static void Presets(Ctx c)
    {
        var w = c.W; var r = new Rnd(c.Rec.Seed + 41); int i = 0;
        foreach (var fl in c.Rec.Flavours) { if (i >= 6) break; w.Preset("Presets", N(c, Cap(fl) + " Preset", i), Music.BassPatch(fl, r), fl + ",bass,synth preset", c.Rec.Mood); i++; }
        w.Preset("Presets", N(c, "Pad Preset", i), Music.LeadPatch("Dark Pad"), "pad,synth preset", c.Rec.Mood); w.Preset("Presets", N(c, "Stab Preset", i + 1), Music.LeadPatch("Supersaw Lead", "voices=16;e1a=9;e1d=180;e1s=0;e1r=100"), "stab,synth preset", c.Rec.Mood);
    }

    // a songstarter kit: every part rendered on its own at the same tempo and key, so each can be edited alone
    static void Songstarter(Ctx c)
    {
        var w = c.W; var r = new Rnd(c.Rec.Seed + 43); double bpm = BpmOf(c, 0); string key = KeyOf(c, 0); int pc = Music.Root(key); bool minor = Music.IsMinor(key); string rh = Music.RhythmFor(c.Rec.Drums); string kit = c.Rec.Name + " Songstarter " + bpm.ToString("0") + " BPM " + Kn(key); string fol = "Kits/" + WriterName(kit);
        var lp = DrumKit.Render(c.Kit, c.Style, c.Style.Pat, bpm, 4, c.Style.Swing, c.Rec.Seed + 300, true); var mix = new Buf(lp.Mix.Length); mix.Add(lp.Mix, 0, 0.9f);
        string fl = c.Rec.Flavours[0]; var bass = SoundGen.RenderSynth(Music.BassPatch(fl, r), Music.BassLine(r, rh, 4, 36 + pc, minor), bpm, 16, 0.3); bass = LoopLen(bass, 16, bpm);
        var chords = SoundGen.RenderSynth(Music.LeadPatch(c.Rec.Mood.IndexOf("dark", StringComparison.OrdinalIgnoreCase) >= 0 ? "Dark Pad" : "Soft Pad"), Music.Chords(r, 4, pc, minor, 3, "pad"), bpm, 16, 0.8); chords = LoopLen(chords, 16, bpm);
        var lead = SoundGen.RenderSynth(Music.LeadPatch("Square Lead"), Music.Melody(r, 4, pc, minor, 4), bpm, 16, 0.6); lead = LoopLen(lead, 16, bpm);
        double bar = 4 * 60.0 / bpm; var fx = new Buf(mix.Length); fx.Add(SoundGen.Riser(r, bar, 300, 9000, true), (int)(3 * bar * SoundGen.SR), 0.8f);
        w.Audio(lp.Mix, fol + "/Stems", kit + " - Drums", "Stem", "Kits", "stem", bpm, "", Tg(c, "songstarter,stem,drums"), c.Rec.Mood, -1.5); w.Audio(bass, fol + "/Stems", kit + " - Bass", "Stem", "Kits", "stem", bpm, Kn(key), Tg(c, "songstarter,stem,bass"), c.Rec.Mood, -1.5);
        w.Audio(chords, fol + "/Stems", kit + " - Chords", "Stem", "Kits", "stem", bpm, Kn(key), Tg(c, "songstarter,stem,chords"), c.Rec.Mood, -2.0); w.Audio(lead, fol + "/Stems", kit + " - Lead", "Stem", "Kits", "stem", bpm, Kn(key), Tg(c, "songstarter,stem,lead"), c.Rec.Mood, -2.0); w.Audio(fx, fol + "/Stems", kit + " - FX", "Stem", "Kits", "stem", bpm, "", Tg(c, "songstarter,stem,fx"), c.Rec.Mood, -2.0);
        mix.Add(bass, 0, 0.8f); mix.Add(chords, 0, 0.5f); mix.Add(lead, 0, 0.35f); mix.Add(fx, 0, 0.5f); SoundGen.Glue(mix, 1.2f); w.Audio(mix, fol, kit + " - Full Mix", "Songstarter", "Kits", "loop", bpm, Kn(key), Tg(c, "songstarter,kit,full mix"), c.Rec.Mood, -1.0);
        w.Midi(fol + "/MIDI", kit + " - Bass", "MIDI Bassline", bpm, Kn(key), Tg(c, "songstarter,midi"), c.Rec.Mood, Music.BassLine(new Rnd(c.Rec.Seed + 43), rh, 4, 36 + pc, minor)); w.Midi(fol + "/MIDI", kit + " - Chords", "MIDI Chords", bpm, Kn(key), Tg(c, "songstarter,midi"), c.Rec.Mood, Music.Chords(new Rnd(c.Rec.Seed + 44), 4, pc, minor, 3, "pad"));
    }
    static string WriterName(string s) { return PackWriter.Clean(s); }

    // ---- the big pack: many more takes of every kind of loop, so each pack has well over two hundred sounds ----
    static readonly string[] ChordPatches = { "Electric Keys", "Organ", "Bell Keys", "Clav", "Choir Pad", "Shimmer Pad", "Soft Pad", "Dark Pad" };
    static readonly string[] LeadPatches = { "Supersaw Lead", "Square Lead", "Mono Saw Lead", "FM Bell Lead", "Short Pluck", "Soft Pluck", "Bell Pluck", "Electric Keys" };
    static void Extra(Ctx c)
    {
        var w = c.W; var r = new Rnd(c.Rec.Seed + 53); string rh = Music.RhythmFor(c.Rec.Drums);
        if (c.Rec.Has("loops"))
        {
            // drum loops: eight more grooves, each as the full loop, without the kick, and kick and snare only
            for (int i = 0; i < 8; i++)
            {
                double bpm = BpmOf(c, i + 1); int bars = i % 2 == 0 ? 2 : 4; var lp = DrumKit.Render(c.Kit, c.Style, c.Style.Pat, bpm, bars, c.Style.Swing + (i % 3) * 0.01, c.Rec.Seed + 500 + i * 7, i % 4 == 3); string b = bpm.ToString("0");
                w.Audio(lp.Mix, "Loops/Drum Loops", N(c, "Drum Groove " + (i + 1) + " " + b + " BPM", 10 + i), "Drum Loop", "Loops", "loop", bpm, "", Tg(c, "drum loop,drums,loop,groove," + b + "bpm"), c.Rec.Mood, -1.5);
                var nk = new Buf(lp.Mix.Length); nk.Add(lp.Snare, 0, 1f); nk.Add(lp.Hats, 0, 1f); nk.Add(lp.Perc, 0, 0.9f);
                w.Audio(nk, "Loops/Drum Loops No Kick", N(c, "Drum Groove " + (i + 1) + " " + b + " BPM (no kick)", 10 + i), "Drum Loop", "Loops", "loop", bpm, "", Tg(c, "drum loop,no kick,drums,loop," + b + "bpm"), c.Rec.Mood, -1.5);
                var ks = new Buf(lp.Mix.Length); ks.Add(lp.Kick, 0, 1f); ks.Add(lp.Snare, 0, 1f);
                w.Audio(ks, "Loops/Kick and Snare Loops", N(c, "Kick and Snare " + (i + 1) + " " + b + " BPM", 10 + i), "Drum Loop", "Loops", "loop", bpm, "", Tg(c, "kick,snare,drum loop,loop," + b + "bpm"), c.Rec.Mood, -1.5);
                if (i < 4) { var pc = new Buf(lp.Mix.Length); pc.Add(lp.Perc, 0, 1f); pc.Add(lp.Hats, 0, 0.5f); w.Audio(pc, "Loops/Percussion Loops", N(c, "Percussion Loop " + (i + 1) + " " + b + " BPM", 10 + i), "Percussion Loop", "Loops", "loop", bpm, "", Tg(c, "percussion,perc,loop,shaker," + b + "bpm"), c.Rec.Mood, -2.0); }
            }
            // bass loops: three more lines for each bass sound, in other keys
            int fi = 0; foreach (var fl in c.Rec.Flavours)
            {
                if (fi >= 6) break;
                for (int v = 0; v < 3; v++)
                {
                    double bpm = BpmOf(c, fi + v + 1); string key = KeyOf(c, fi + v + 1); int root = 36 + Music.Root(key); bool minor = Music.IsMinor(key);
                    var notes = Music.BassLine(r, rh, 2, root, minor); var b = LoopLen(SoundGen.RenderSynth(Music.BassPatch(fl, r), notes, bpm, 8, 0.25), 8, bpm);
                    w.Audio(b, "Basses/Bass Loops", N(c, Cap(fl) + " Bass Line " + (v + 2) + " " + bpm.ToString("0") + " BPM " + Kn(key), 20 + fi * 3 + v), "Bass Loop", "Bass", "loop", bpm, Kn(key), Tg(c, fl + ",bass loop,bass,loop," + Kn(key).ToLowerInvariant()), c.Rec.Mood, -1.5);
                }
                fi++;
            }
        }
        if (c.Rec.Has("music"))
        {
            bool dark = c.Rec.Mood.IndexOf("dark", StringComparison.OrdinalIgnoreCase) >= 0;
            // chord loops on every keys and pad sound, in two keys each
            for (int i = 0; i < ChordPatches.Length; i++) for (int v = 0; v < 2; v++)
            {
                double bpm = BpmOf(c, i + v); string key = KeyOf(c, i + v + 2); int pc = Music.Root(key); bool minor = Music.IsMinor(key); string style = i < 4 && v == 1 ? "stab" : "pad";
                var b = LoopLen(SoundGen.RenderSynth(Music.LeadPatch(ChordPatches[i], style == "stab" ? "voices=16;e1a=9;e1d=200;e1s=10;e1r=120" : "e1a=3"), Music.Chords(r, 4, pc, minor, i % 2 == 0 ? 3 : 4, style), bpm, 16, 1.0), 16, bpm);
                w.Audio(b, "Synths/Chord Loops", N(c, ChordPatches[i] + (style == "stab" ? " Stabs " : " Chords ") + bpm.ToString("0") + " BPM " + Kn(key), 30 + i * 2 + v), "Chord Loop", "Synth", "loop", bpm, Kn(key), Tg(c, "chords,chord loop,loop,synth," + ChordPatches[i].ToLowerInvariant() + "," + Kn(key).ToLowerInvariant()), c.Rec.Mood, -2.5);
            }
            // melody and arp loops on every lead and pluck sound
            for (int i = 0; i < LeadPatches.Length; i++) for (int v = 0; v < 3; v++)
            {
                double bpm = BpmOf(c, i + v); string key = KeyOf(c, i + v + 1); int pc = Music.Root(key); bool minor = Music.IsMinor(key); bool arp = v == 2;
                var notes = arp ? Music.Arpeggio(r, 4, pc, minor, i % 2 == 0 ? 4 : 5) : Music.Melody(r, 4, pc, minor, v == 0 ? 4 : 5);
                var b = LoopLen(SoundGen.RenderSynth(Music.LeadPatch(LeadPatches[i]), notes, bpm, 16, 0.8), 16, bpm);
                w.Audio(b, arp ? "Synths/Arp Loops" : "Synths/Melody Loops", N(c, LeadPatches[i] + (arp ? " Arp " : " Melody " + (v + 1) + " ") + bpm.ToString("0") + " BPM " + Kn(key), 50 + i * 3 + v), arp ? "Synth Loop" : "Melody Loop", "Synth", "loop", bpm, Kn(key), Tg(c, (arp ? "arp,arpeggio," : "melody,lead,") + "loop,synth," + LeadPatches[i].ToLowerInvariant() + "," + Kn(key).ToLowerInvariant()), c.Rec.Mood, -2.5);
            }
            // plucked guitar and harp loops
            for (int i = 0; i < 4; i++)
            {
                double bpm = BpmOf(c, i); string key = KeyOf(c, i + 3); int pc = Music.Root(key); bool minor = Music.IsMinor(key); var gr = new Rnd(c.Rec.Seed + 600 + i); int[] sc = minor ? Music.MinPent : new[] { 0, 2, 4, 7, 9 }; double stepS = 60.0 / bpm * SoundGen.SR / (i % 2 == 0 ? 2 : 4);
                var pl = new Buf((int)(16 * 60.0 / bpm * SoundGen.SR + 2 * SoundGen.SR)); int steps = i % 2 == 0 ? 32 : 64;
                for (int st = 0; st < steps; st++) { if (gr.Next() < (i % 2 == 0 ? 0.3f : 0.5f)) continue; int midi = 12 * 4 + pc + sc[gr.Int(sc.Length)] + (gr.Next() < 0.3f ? 12 : 0); pl.Add(SoundGen.Pluck(gr, SoundGen.NoteHz(midi), i < 2 ? 1.4 : 0.6, i < 2 ? 0.7 : 0.9), (int)(st * stepS), 0.5f); }
                pl = LoopLen(SoundGen.Wet(pl, 45, 50, 0.2f, pl.Length), 16, bpm);
                w.Audio(pl, "Synths/Guitar and Pluck Loops", N(c, (i < 2 ? "Guitar Loop " : "Harp Loop ") + (i % 2 + 2) + " " + bpm.ToString("0") + " BPM " + Kn(key), 80 + i), "Guitar Loop", "Synth", "loop", bpm, Kn(key), Tg(c, (i < 2 ? "guitar" : "harp") + ",pluck,string,loop,melody"), c.Rec.Mood, -2.5);
            }
            // synth one-shots: a chord hit and a single note on every sound
            for (int i = 0; i < ChordPatches.Length; i++)
            {
                string key = KeyOf(c, i); int pc = Music.Root(key); bool minor = Music.IsMinor(key); double bpm = BpmOf(c, 0); var ch = new List<double[]>(); foreach (int iv in minor ? new[] { 0, 3, 7, 10 } : new[] { 0, 4, 7, 11 }) ch.Add(new double[] { 0, 1.5, 48 + pc + iv, 100 });
                w.Audio(SoundGen.RenderSynth(Music.LeadPatch(ChordPatches[i]), ch, bpm, 3, 1.2), "Synths/One-Shots", N(c, ChordPatches[i] + " Chord Hit " + Kn(key), 90 + i), "Chord Hit", "Synth", "one-shot", 0, Kn(key), Tg(c, "chord,hit,one-shot,synth," + ChordPatches[i].ToLowerInvariant()), c.Rec.Mood, -2.0);
            }
            for (int i = 0; i < LeadPatches.Length; i++)
            {
                string key = KeyOf(c, i); int midi = 60 + Music.Root(key); var nt = new List<double[]> { new double[] { 0, 1.0, midi, 105 } };
                w.Audio(SoundGen.RenderSynth(Music.LeadPatch(LeadPatches[i]), nt, 120, 2.5, 0.9), "Synths/One-Shots", N(c, LeadPatches[i] + " " + Music.NoteName(midi), 100 + i), "Synth One-Shot", "Synth", "one-shot", 0, Kn(key), Tg(c, "lead,note,one-shot,synth," + LeadPatches[i].ToLowerInvariant()), c.Rec.Mood, -2.0);
            }
        }
        if (c.Rec.Has("vocal"))
        {
            // more vocal chop loops: different vowels, melodies and speeds in each key
            double beat0 = 0; for (int v = 0; v < 10; v++)
            {
                double bpm = BpmOf(c, v); double beat = 60.0 / bpm * SoundGen.SR; beat0 = beat; string key = KeyOf(c, v + 1); int pc = Music.Root(key); bool minor = Music.IsMinor(key); var mel = Music.Melody(r, 2, pc, minor, v % 3 == 0 ? 5 : 4); var lp = new Buf((int)(8 * 60.0 / bpm * SoundGen.SR + 20000));
                int vi = v; foreach (var n in mel) { double hz = SoundGen.NoteHz((int)n[2]); lp.Add(SoundGen.VocalSyllable(r, hz, (vi++) % 5, v % 2 == 0 ? -1 : (vi + 2) % 5, Math.Min(v % 3 == 2 ? 0.25 : 0.6, n[1] * 60.0 / bpm * 0.95), v % 4 == 1 ? 0.2 : 0, 0, ""), (int)(n[0] * beat), 0.6f); }
                lp = LoopLen(SoundGen.Wet(lp, 50, 50, 0.2f, lp.Length), 8, bpm);
                w.Audio(lp, "Vocals/Chops", N(c, "Vocal Chop Loop " + (v + 3) + " " + bpm.ToString("0") + " BPM " + Kn(key), 120 + v), "Vocal Chop", "Vocal", "loop", bpm, Kn(key), Tg(c, "vocal chop,chop,loop,synth voice,vocal," + Kn(key).ToLowerInvariant()), c.Rec.Mood, -2.0);
            }
            // vocal one-shot notes in the key (play them as an instrument)
            for (int v = 0; v < 6; v++) { string key = KeyOf(c, v); int midi = 57 + Music.Root(key) + (v % 2) * 7; var b = SoundGen.Wet(SoundGen.VocalSyllable(r, SoundGen.NoteHz(midi), v % 5, -1, 0.9, 0, 1, ""), 45, 50, 0.2f, (int)(1.6 * SoundGen.SR)); w.Audio(b, "Vocals/Vocal Notes", N(c, "Vocal Note " + "AEIOU"[v % 5] + " " + Music.NoteName(midi), 130 + v), "Vocal Chop", "Vocal", "one-shot", 0, Kn(key), Tg(c, "vocal,note,one-shot,synth voice"), c.Rec.Mood, -2.0); }
        }
    }
}
