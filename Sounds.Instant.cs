using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// =====================================================================================================
//  THE INSTANT LIBRARY: every pack in the catalog lists its whole range straight away, without installing it: drum, top, bass, chord,
//  melody, arp and vocal loops; kick, snare, clap, hat, percussion, cymbal, 808, bass, instrument, chord, FX and vocal one-shots; and
//  drum, bassline, chord and melody MIDI files. A sound is made the first time it is played or dragged (a fraction of a second, on this
//  PC, with the same sound design as the packs) and kept in sounds\_instant<version>, so the next time it is there at once. Each item
//  is a fixed recipe, so it is always the same sound. Loops, notes and MIDI can be made in any key and tempo (SetTarget): they are then
//  made for that key and tempo, and kept separately.
// =====================================================================================================
static class InstantLib
{
    public const int DrumPer = 120, BassPer = 120;
    class Cat { public string Code, Type, Group, Kind, Inst; public bool Timed, Keyed; }
    static readonly Dictionary<string, Cat> Cats = new Dictionary<string, Cat>();
    static void C(string code, string type, string group, string kind, bool timed, bool keyed, string inst) { Cats[code] = new Cat { Code = code, Type = type, Group = group, Kind = kind, Timed = timed, Keyed = keyed, Inst = inst }; }
    static InstantLib()
    {
        C("d", "Drum Loop", "Loops", "loop", true, false, "Drums"); C("t", "Top Loop", "Loops", "loop", true, false, "Percussion"); C("b", "Bass Loop", "Bass", "loop", true, true, "Bass Synth");
        C("c", "Chord Loop", "", "loop", true, true, ""); C("m", "Melody Loop", "", "loop", true, true, ""); C("a", "Arp Loop", "", "loop", true, true, ""); C("v", "Vocal Chop", "Vocal", "loop", true, true, "Voice");
        C("ok", "Kick", "Drums", "one-shot", false, false, "Kick"); C("os", "Snare", "Drums", "one-shot", false, false, "Snare"); C("oc", "Clap", "Drums", "one-shot", false, false, "Clap");
        C("oh", "Hi-Hat", "Drums", "one-shot", false, false, "Hi-Hat"); C("oo", "Open Hat", "Drums", "one-shot", false, false, "Hi-Hat"); C("op", "Percussion", "Drums", "one-shot", false, false, "Percussion"); C("oy", "Cymbal", "Drums", "one-shot", false, false, "Cymbal");
        C("o8", "808", "Bass", "one-shot", false, false, "808"); C("ob", "Bass One-Shot", "Bass", "one-shot", false, true, "Bass Synth"); C("on", "Instrument One-Shot", "", "one-shot", false, true, ""); C("och", "Chord Hit", "", "one-shot", false, true, "");
        C("of", "FX", "FX", "one-shot", false, false, "FX"); C("ov", "Vocal Shout", "Vocal", "one-shot", false, false, "Voice");
        C("md", "MIDI Drums", "MIDI", "midi", true, false, "Drums"); C("mb", "MIDI Bassline", "MIDI", "midi", true, true, "Bass Synth"); C("mc", "MIDI Chords", "MIDI", "midi", true, true, ""); C("mm", "MIDI Melody", "MIDI", "midi", true, true, "");
    }
    static List<SoundItem> items; static string builtFor = ""; static readonly object lk = new object();
    static readonly Dictionary<string, byte[]> peaks = new Dictionary<string, byte[]>();
    public static string Dir { get { return Path.Combine(SoundLibrary.Root, "_instant" + PackBuilder.SoundVersion); } }   // one folder per sound version: sounds made by older sound design are made again
    static string PeaksFile { get { return Path.Combine(Dir, "peaks.txt"); } }
    static readonly System.Globalization.CultureInfo CI = System.Globalization.CultureInfo.InvariantCulture; static readonly byte[] NoPeaks = new byte[0];
    static readonly Dictionary<string, string> shared = new Dictionary<string, string>(); static string Share(string s) { string v; if (shared.TryGetValue(s, out v)) return v; shared[s] = s; return s; }

    // ---- the key and tempo instant sounds are made in ("" / 0 = each pack's own) ----
    public static string TargetKey = ""; public static double TargetBpm = 0;
    public static void SetTarget(string key, double bpm)
    {
        string k = ""; int rt; bool mi; if (!string.IsNullOrEmpty(key) && SoundLibrary.ParseKey(key, out rt, out mi)) k = SoundLibrary.KeyName(rt, mi);
        double b = bpm > 0 ? Math.Max(50, Math.Min(220, Math.Round(bpm, 2))) : 0; if (k == TargetKey && b == TargetBpm) return; TargetKey = k; TargetBpm = b;
        lock (lk) { if (items != null) foreach (var s in items) Retarget(s); }
    }
    static Cat CatOf(SoundItem s) { int b = s.Spec.IndexOf('|'); return Cats[s.Spec.Substring(0, b)]; }
    static string Tag(SoundItem s) { var c = CatOf(s); string t = ""; if (c.Timed && TargetBpm > 0) t += "@" + TargetBpm.ToString("0.##", CI); if (c.Keyed && TargetKey.Length > 0) t += "@" + TargetKey.Replace(" ", "").Replace("#", "s"); return t; }
    public static string PathOf(SoundItem s) { string f = s.Rel.Substring(s.Rel.LastIndexOf('/') + 1); int dot = f.LastIndexOf('.'); return Path.Combine(Dir, s.PackId, f.Substring(0, dot) + Tag(s) + f.Substring(dot)); }
    static void Retarget(SoundItem s)
    {
        var c = CatOf(s);
        s.Bpm = c.Timed && TargetBpm > 0 ? TargetBpm : s.BaseBpm; s.Key = c.Keyed && TargetKey.Length > 0 ? TargetKey : s.BaseKey; if (s.Beats > 0 && s.Bpm > 0) s.Dur = s.Beats * 60.0 / s.Bpm;
        s.Name = s.PackName + " - " + s.Core + (c.Timed && s.Bpm > 0 ? " " + s.Bpm.ToString("0.##", CI) + " BPM" : "") + (c.Keyed && s.Key.Length > 0 ? " " + s.Key : "");
        s.Path = null; byte[] pk; s.Peaks = peaks.Count > 0 && peaks.TryGetValue(s.Id + Tag(s), out pk) ? pk : NoPeaks;
    }

    // the list of every instant sound (made once per data folder; it is only names and numbers until a sound is played)
    public static List<SoundItem> Items
    {
        get
        {
            lock (lk)
            {
                if (items != null && builtFor == SoundLibrary.Root) return items;
                builtFor = SoundLibrary.Root; peaks.Clear(); ReadPeaks(); try { foreach (var d in Directory.GetDirectories(SoundLibrary.Root, "_instant*")) if (!string.Equals(Path.GetFileName(d), Path.GetFileName(Dir), StringComparison.OrdinalIgnoreCase)) Directory.Delete(d, true); } catch { }
                var l = new List<SoundItem>(); var inGenre = new Dictionary<string, int>(); foreach (var rec in SoundCatalog.Recipes) { int c; inGenre.TryGetValue(rec.Genre, out c); inGenre[rec.Genre] = c + 1; }
                var recs = new List<Recipe>(SoundCatalog.Recipes); recs.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));   // in pack-name order, like the installed packs
                foreach (var rec in recs) AddPack(l, rec, inGenre[rec.Genre] == 1 ? 2 : 1);
                var low = new Dictionary<string, string>(); foreach (var s in l) { string k = s.Tags + "," + s.Type + "," + s.Group + "," + s.Instrument + "," + s.Kind + "," + s.Mood + "," + s.PackName; string v; if (!low.TryGetValue(k, out v)) { v = k.ToLowerInvariant(); low[k] = v; } s.Tags = v; }   // every label in the tags, lower case and shared: a search checks one string
                items = l; return items;
            }
        }
    }
    public static SoundItem Find(string id) { foreach (var s in Items) if (s.Id == id) return s; return null; }
    public static int Count(string kind) { int n = 0; foreach (var s in Items) if (kind == null || s.Kind == kind) n++; return n; }

    static double Bpm(Recipe r, int i) { return r.Bpms[i % r.Bpms.Length]; }
    static string KeyOf(Recipe r, int i) { return KeyName(r.Keys[i % r.Keys.Length]); }
    static int Bars(int i) { return (i / 3) % 3 == 2 ? 4 : 2; }
    static string Cap(string s) { return s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1); }
    static string KeyName(string key) { int rt; bool m; SoundLibrary.ParseKey(key, out rt, out m); return SoundLibrary.KeyName(rt, m); }
    static string N3(int i) { return (i + 1).ToString("000"); }
    static int CodeSeed(string code) { int h = 7; foreach (char ch in code) h = h * 31 + ch; return h & 0xFFFF; }

    static void AddPack(List<SoundItem> l, Recipe rec, int per)
    {
        bool loops = rec.Has("loops"), drums = rec.Has("drums") || loops, bass = (rec.Has("bass") || loops) && rec.Flavours != null && rec.Flavours.Length > 0 && rec.Flavours[0].Length > 0;
        bool music = rec.Has("music") || loops, vocal = rec.Has("vocal") || loops, fx = rec.Has("fx") || loops, s808 = rec.Has("sub808") || loops;
        var pal = Instruments.Palette(rec); var chordPal = pal.FindAll(x => x.Chords); var melPal = pal.FindAll(x => x.Melody); if (chordPal.Count == 0) chordPal = pal; if (melPal.Count == 0) melPal = pal;
        string tg = (rec.Genre + "," + rec.Sub + "," + rec.Tags).ToLowerInvariant();
        Func<string, int, string, string, double, string, double, string, SoundItem> mk = (code, i, core, file, bpm, key, beats, extraTags) =>
        {
            var c = Cats[code]; var s = new SoundItem { PackId = rec.Id, PackName = rec.Name, Rel = "instant/" + file, Type = c.Type, Group = c.Group, Kind = c.Kind, Genre = rec.Genre, Sub = rec.Sub, Mood = rec.Mood, Tags = Share(tg + "," + extraTags + ",instant"), Rate = c.Kind == "midi" ? 0 : 44100, Format = c.Kind == "midi" ? "MIDI" : "WAV 24-bit", Spec = code + "|" + rec.Id + "|" + i, Instrument = c.Inst, Core = core, BaseBpm = c.Timed ? bpm : 0, BaseKey = c.Keyed ? key : "", Beats = beats };
            Retarget(s); l.Add(s); return s;
        };
        Action<SoundItem, Instrument> inst = (s, ins) => { s.Group = ins.Group; s.Instrument = ins.Name; };
        if (drums) for (int i = 0; i < DrumPer * per; i++) { string feel = Feel(i); mk("d", i, "Drum Loop " + N3(i) + (feel.Length > 0 ? " (" + feel + ")" : ""), "drum-" + N3(i) + ".wav", Bpm(rec, i), "", Bars(i) * 4, "drum loop,drums,loop,beat," + feel); }
        if (drums && loops) for (int i = 0; i < 24; i++) mk("t", i, "Top Loop " + N3(i), "top-" + N3(i) + ".wav", Bpm(rec, i + 1), "", 8, "top loop,hats,percussion,loop");
        if (bass) for (int i = 0; i < BassPer * per; i++) { int nf = rec.Flavours.Length; string fl = rec.Flavours[i % nf]; mk("b", i, Cap(fl) + " Bass Loop " + N3(i), "bass-" + N3(i) + ".wav", Bpm(rec, i / 2), KeyOf(rec, i / nf), 8, fl + ",bass loop,bass,loop"); }
        if (music)
        {
            for (int i = 0; i < 32; i++) { var ins = chordPal[i % chordPal.Count]; inst(mk("c", i, ins.Name + " Chords " + N3(i), "chords-" + N3(i) + ".wav", Bpm(rec, i), KeyOf(rec, i + 1), 16, "chords,chord loop,loop," + ins.Name.ToLowerInvariant() + "," + ins.Group.ToLowerInvariant()), ins); }
            for (int i = 0; i < 32; i++) { var ins = melPal[i % melPal.Count]; inst(mk("m", i, ins.Name + " Melody " + N3(i), "melody-" + N3(i) + ".wav", Bpm(rec, i + 1), KeyOf(rec, i), 16, "melody,lead,loop," + ins.Name.ToLowerInvariant() + "," + ins.Group.ToLowerInvariant()), ins); }
            for (int i = 0; i < 16; i++) { var ins = melPal[(i * 3 + 1) % melPal.Count]; inst(mk("a", i, ins.Name + " Arp " + N3(i), "arp-" + N3(i) + ".wav", Bpm(rec, i + 2), KeyOf(rec, i + 2), i % 2 == 0 ? 8 : 16, "arp,arpeggio,loop," + ins.Name.ToLowerInvariant() + "," + ins.Group.ToLowerInvariant()), ins); }
        }
        if (vocal) for (int i = 0; i < 16; i++) mk("v", i, "Vocal Chop Loop " + N3(i), "vocal-" + N3(i) + ".wav", Bpm(rec, i), KeyOf(rec, i + 1), 8, "vocal chop,chop,loop,synth voice,vocal");
        if (drums)
        {
            string[] kn = { "Punch", "Tight", "Deep", "Hard", "Thump", "Clean" }, sn = { "Crack", "Snap", "Fat", "Tight", "Rim Crack", "Bright" }, pn = { "Tom Low", "Tom Mid", "Shaker", "Rim", "Conga", "Bell" };
            for (int i = 0; i < 12; i++) mk("ok", i, "Kick " + kn[i % 6] + " " + (i + 1).ToString("00"), "kick-" + N3(i) + ".wav", 0, "", 0, "kick,drums,one-shot");
            for (int i = 0; i < 10; i++) mk("os", i, "Snare " + sn[i % 6] + " " + (i + 1).ToString("00"), "snare-" + N3(i) + ".wav", 0, "", 0, "snare,drums,one-shot");
            for (int i = 0; i < 6; i++) mk("oc", i, "Clap " + (i + 1).ToString("00"), "clap-" + N3(i) + ".wav", 0, "", 0, "clap,drums,one-shot");
            for (int i = 0; i < 8; i++) mk("oh", i, "Hat Closed " + (i + 1).ToString("00"), "hat-" + N3(i) + ".wav", 0, "", 0, "hat,hihat,closed,drums,one-shot");
            for (int i = 0; i < 4; i++) mk("oo", i, "Hat Open " + (i + 1).ToString("00"), "openhat-" + N3(i) + ".wav", 0, "", 0, "hat,hihat,open,drums,one-shot");
            for (int i = 0; i < 12; i++) mk("op", i, "Perc " + pn[i % 6] + " " + (i + 1).ToString("00"), "perc-" + N3(i) + ".wav", 0, "", 0, "percussion,perc," + pn[i % 6].ToLowerInvariant() + ",drums,one-shot");
            for (int i = 0; i < 4; i++) { var s = mk("oy", i, (i % 2 == 0 ? "Ride " : "Crash ") + (i / 2 + 1).ToString("00"), "cymbal-" + N3(i) + ".wav", 0, "", 0, (i % 2 == 0 ? "ride" : "crash") + ",cymbal,drums,one-shot"); s.Type = i % 2 == 0 ? "Ride" : "Cymbal"; }
        }
        if (s808) for (int i = 0; i < 12; i++) mk("o8", i, "808 " + Music.NoteName(24 + i), "808-" + N3(i) + ".wav", 0, "", 0, "808,sub,bass,one-shot," + Music.NoteName(24 + i).ToLowerInvariant());
        if (bass) for (int i = 0; i < 12; i++) { string fl = rec.Flavours[i % rec.Flavours.Length]; var s = mk("ob", i, Cap(fl) + " Bass " + (i + 1).ToString("00"), "bassshot-" + N3(i) + ".wav", 0, KeyOf(rec, i / 2), 0, fl + ",bass,one-shot"); s.Type = PackBuilder.BassType(fl); }
        if (music)
        {
            for (int i = 0; i < 16; i++) { var ins = pal[i % pal.Count]; inst(mk("on", i, ins.Name + " Note " + (i < pal.Count ? "Low" : "High") + " " + (i + 1).ToString("00"), "note-" + N3(i) + ".wav", 0, KeyOf(rec, 0), 0, "note,one-shot,instrument," + ins.Name.ToLowerInvariant() + "," + ins.Group.ToLowerInvariant()), ins); }
            for (int i = 0; i < 8; i++) { var ins = chordPal[i % chordPal.Count]; inst(mk("och", i, ins.Name + " Chord Hit " + (i + 1).ToString("00"), "chordhit-" + N3(i) + ".wav", 0, KeyOf(rec, i), 0, "chord,hit,one-shot," + ins.Name.ToLowerInvariant()), ins); }
        }
        if (fx)
        {
            string[] fn = { "Riser 4 Bars", "Riser 2 Bars", "Riser 1 Bar", "Downlifter 2 Bars", "Downlifter 1 Bar", "Impact Deep", "Impact Hard", "Impact Boom", "Sweep Up", "Sweep Down", "Reverse Crash", "Whoosh" }, ft = { "Riser", "Riser", "Riser", "Downlifter", "Downlifter", "Impact", "Impact", "Impact", "Sweep", "Sweep", "Reverse", "Drop FX" };
            for (int i = 0; i < 12; i++) { var s = mk("of", i, fn[i], "fx-" + N3(i) + ".wav", 0, "", 0, ft[i].ToLowerInvariant() + ",fx,transition"); s.Type = ft[i]; }
        }
        if (vocal) { string[] vn = { "HEY", "YO", "OH", "UH", "AYY", "YEAH", "Note AH", "Note OO" }; for (int i = 0; i < 8; i++) { var s = mk("ov", i, "Vocal " + vn[i], "voice-" + N3(i) + ".wav", 0, "", 0, i < 6 ? "shout,vocal shout,ad-lib,vocal" : "vocal,note,one-shot"); if (i >= 6) s.Type = "Vocal Chop"; } }
        for (int i = 0; i < 24 && drums; i++) mk("md", i, "MIDI Drums " + N3(i), "mididrums-" + N3(i) + ".mid", Bpm(rec, i), "", 16, "midi,drums,pattern");
        for (int i = 0; i < 24 && bass; i++) mk("mb", i, "MIDI Bassline " + N3(i), "midibass-" + N3(i) + ".mid", Bpm(rec, i), KeyOf(rec, i), 16, "midi,bassline");
        for (int i = 0; i < 24 && music; i++) mk("mc", i, "MIDI Chords " + N3(i), "midichords-" + N3(i) + ".mid", Bpm(rec, i), KeyOf(rec, i + 1), 16, "midi,chords,progression");
        for (int i = 0; i < 24 && music; i++) mk("mm", i, "MIDI Melody " + N3(i), "midimelody-" + N3(i) + ".mid", Bpm(rec, i + 1), KeyOf(rec, i), 16, "midi,melody");
    }
    static string Feel(int i) { switch (i % 8) { case 1: return "busy hats"; case 2: return "sparse"; case 3: return "with fill"; case 5: return "rolls"; case 6: return "percussion"; case 7: return "ghost notes"; default: return ""; } }

    // ---- making a sound ----
    static readonly Dictionary<string, DrumKit.Kit> kits = new Dictionary<string, DrumKit.Kit>(); static readonly object makeLock = new object();
    static DrumKit.Kit Kit(Recipe rec, int kv) { DrumKit.Kit kit; string kk = rec.Id + "/" + kv; lock (kits) { if (!kits.TryGetValue(kk, out kit)) { if (kits.Count > 24) kits.Clear(); kit = DrumKit.Make(PackBuilder.StyleFor(rec), rec.Seed + 1 + kv * 977); kits[kk] = kit; } } return kit; }
    // makes the file for an instant sound if it is not there yet; returns null if it worked, or a message
    public static string Ensure(SoundItem s)
    {
        if (s == null || string.IsNullOrEmpty(s.Spec)) return null; if (File.Exists(s.Path)) return null;
        lock (makeLock) try   // one at a time: the preview and a drop can ask for the same sound at once
        {
            string path = s.Path; if (File.Exists(path)) return null;
            var f = s.Spec.Split('|'); var rec = SoundCatalog.Find(f[1]); int i = int.Parse(f[2]); if (rec == null) return "This sound's pack is no longer in the library.";
            Directory.CreateDirectory(Path.GetDirectoryName(path)); string tmp = path + ".tmp";
            if (s.Kind == "midi") { SoundIO.WriteMidi(tmp, s.Bpm, s.Name, MidiNotes(f[0], rec, i, s)); if (File.Exists(path)) File.Delete(path); File.Move(tmp, path); return null; }
            Buf b = Render(f[0], rec, i, s); if (b == null || b.Length < 32) return "Couldn't make this sound.";
            b = PackWriter.Finish(b, s.Type, s.Kind, s.Kind == "loop" ? (s.Group == "Loops" || s.Group == "Bass" ? -1.5 : -2.0) : (s.Group == "Drums" || s.Type == "808" ? -1.0 : -2.0));
            SoundIO.WriteWav(tmp, b.L, b.R, SoundGen.SR, 24); if (File.Exists(path)) File.Delete(path); File.Move(tmp, path);
            s.Peaks = SoundIO.Peaks(b.L, b.R, 96); s.Dur = b.Length / (double)SoundGen.SR;
            lock (lk) { string key = s.Id + Tag(s); peaks[key] = s.Peaks; try { File.AppendAllText(PeaksFile, key + "|" + Convert.ToBase64String(s.Peaks) + "\n", Encoding.UTF8); } catch { } }
            return null;
        }
        catch (Exception ex) { return "Couldn't make this sound: " + ex.Message; }
    }
    static void ReadPeaks()
    {
        try { if (!File.Exists(PeaksFile)) return; foreach (var line in File.ReadAllLines(PeaksFile, Encoding.UTF8)) { int k = line.IndexOf('|'); if (k <= 0) continue; try { peaks[line.Substring(0, k)] = Convert.FromBase64String(line.Substring(k + 1)); } catch { } } } catch { }
    }
    static void KeyParts(SoundItem s, Recipe rec, out int pc, out bool minor) { string k = s.Key.Length > 0 ? s.Key : (rec.Keys.Length > 0 ? rec.Keys[0] : "F min"); pc = Music.Root(k); minor = Music.IsMinor(k); }
    static Instrument InstOf(SoundItem s) { return Instruments.Get(s.Instrument) ?? Instruments.Get("Grand Piano"); }

    static Buf Render(string code, Recipe rec, int i, SoundItem s)
    {
        var r = new Rnd(rec.Seed * 13 + i * 197 + CodeSeed(code)); double bpm = s.Bpm > 0 ? s.Bpm : 120; int pc; bool minor; KeyParts(s, rec, out pc, out minor);
        switch (code)
        {
            case "d": return RenderDrum(rec, i, bpm);
            case "t": { var st = PackBuilder.StyleFor(rec); var lp = DrumKit.Render(Kit(rec, i % 6), st, Vary(st.Pat, new Rnd(rec.Seed * 3 + i * 71), i % 2 == 0 ? 1 : 6), bpm, 2, st.Swing, rec.Seed + 700 + i, false); var top = new Buf(lp.Hats.Length); top.Add(lp.Hats, 0, 1f); top.Add(lp.Perc, 0, 0.9f); return top; }
            case "b": return RenderBass(rec, i, bpm, s.Key);
            case "c": { var ins = InstOf(s); int oct = ins.Low > 50 ? 4 : 3; bool stab = (ins.Group == "Keys" && i % 3 == 2) || (ins.Group == "Guitar" && i % 2 == 1); var notes = Music.Chords(r, 4, pc, minor, oct, stab ? "stab" : "pad"); return PackBuilder.LoopLen(Instruments.RenderPart(ins, notes, bpm, 16, 2.0), 16, bpm); }
            case "m": { var ins = InstOf(s); int oct = ins.Name == "Log Drum" ? 2 : (ins.High < 90 ? 4 : 5); var notes = Music.Melody(r, 4, pc, minor, oct); return PackBuilder.LoopLen(Instruments.RenderPart(ins, notes, bpm, 16, 2.0), 16, bpm); }
            case "a": { var ins = InstOf(s); int bars = (int)(s.Beats / 4); var notes = Music.Arpeggio(r, bars, pc, minor, ins.High < 90 ? 3 : 4); return PackBuilder.LoopLen(Instruments.RenderPart(ins, notes, bpm, s.Beats, 1.5), s.Beats, bpm); }
            case "v":
            {
                double beat = 60.0 / bpm * SoundGen.SR; var mel = Music.Melody(r, 2, pc, minor, i % 3 == 0 ? 5 : 4); var lp = new Buf((int)(8 * 60.0 / bpm * SoundGen.SR + 20000)); int vi = i;
                foreach (var n in mel) { double hz = SoundGen.NoteHz((int)n[2]); lp.Add(SoundGen.VocalSyllable(r, hz, (vi++) % 5, i % 2 == 0 ? -1 : (vi + 2) % 5, Math.Min(i % 3 == 2 ? 0.25 : 0.6, n[1] * 60.0 / bpm * 0.95), i % 4 == 1 ? 0.2 : 0, 0, ""), (int)(n[0] * beat), 0.6f); }
                return PackBuilder.LoopLen(SoundGen.Wet(lp, 50, 50, 0.2f, lp.Length), 8, bpm);
            }
            case "ok": return Kit(rec, i / 6).Kicks[i % 6];
            case "os": return Kit(rec, i / 6).Snares[i % 6];
            case "oc": return Kit(rec, i / 3).Claps[i % 3];
            case "oh": return Kit(rec, i / 4).HatsC[i % 4];
            case "oo": return Kit(rec, i / 2).HatsO[i % 2];
            case "op": { var k = Kit(rec, i / 6); return k.Perc[i % k.Perc.Count]; }
            case "oy": return Kit(rec, i / 2).Rides[i % 2];
            case "o8": { bool longOne = rec.Drums == "drill" || rec.Drums == "trap" || rec.Drums == "phonk" || rec.Tags.Contains("drill"); double dec = (longOne ? 1.0 : 0.5) + (i % 3) * 0.3; return SoundGen.Eight08(r, 24 + i, dec, 1.1 + (i % 2) * 0.15, 0.2, i % 4 == 3 ? 4 : 2, 0.9 + dec * 1.3); }
            case "ob": { string fl = rec.Flavours[i % rec.Flavours.Length]; int p = 36 + pc + (i % 2 == 1 ? 7 : 0); var notes = new List<double[]> { new double[] { 0, 1.3 * 2, p, 105 } }; return SoundGen.RenderSynth(Music.BassPatch(fl, r), notes, 120, 3, 0.45); }
            case "on": { var ins = InstOf(s); int baseOct = ins.Name == "Log Drum" ? 2 : (ins.High < 90 ? 3 : 4); int midi = 12 * (baseOct + 1) + pc + (i >= Instruments.Palette(rec).Count ? 12 : 0); return Instruments.RenderPart(ins, new List<double[]> { new double[] { 0, 2.0, midi, 105 } }, 120, 2.0, 2.0); }
            case "och": { var ins = InstOf(s); int oct = ins.Low > 50 ? 4 : 3; var ch = new List<double[]>(); foreach (int iv in minor ? new[] { 0, 3, 7, 10 } : new[] { 0, 4, 7, 11 }) ch.Add(new double[] { 0, 3.0, 12 * (oct + 1) + pc + iv, 100 }); return Instruments.RenderPart(ins, ch, 120, 3.0, 2.0); }
            case "of":
            {
                double bar = 4 * 60.0 / Bpm(rec, 0);
                switch (i)
                {
                    case 0: return SoundGen.Riser(r, bar * 4, 300, 9000, true); case 1: return SoundGen.Riser(r, bar * 2, 350, 9500, true); case 2: return SoundGen.Riser(r, bar, 400, 10000, false);
                    case 3: { var b = SoundGen.Riser(r, bar * 2, 300, 9000, true); b.Reverse(); return b; } case 4: { var b = SoundGen.Riser(r, bar, 400, 9000, false); b.Reverse(); return b; }
                    case 5: return SoundGen.Impact(r, 3.2, 48, 1.4); case 6: return SoundGen.Impact(r, 2.6, 60, 1.6); case 7: return SoundGen.Impact(r, 4.0, 42, 1.3);
                    case 8: return SoundGen.Sweep(r, bar, 400, 12000, 2.5); case 9: return SoundGen.Sweep(r, bar, 12000, 400, 2.5);
                    case 10: { var cr = SoundGen.Wet(SoundGen.Cymbal(r, 1.6, 3.0, false), 80, 40, 0.5f, (int)(4.5 * SoundGen.SR)); cr.Reverse(); return cr; }
                    default: return SoundGen.Sweep(r, 1.2, 800, 6000, 1.5);
                }
            }
            case "ov":
            {
                if (i >= 6) { int midi = 57 + pc + (i == 7 ? 7 : 0); return SoundGen.Wet(SoundGen.VocalSyllable(r, SoundGen.NoteHz(midi), i == 6 ? 0 : 4, -1, 0.9, 0, 1, ""), 45, 50, 0.2f, (int)(1.6 * SoundGen.SR)); }
                var sh = new[] { new object[] { 230.0, 1, 2, 0.34, 0.15, "h" }, new object[] { 190.0, 3, 4, 0.42, -0.1, "" }, new object[] { 210.0, 3, 4, 0.5, 0.05, "" }, new object[] { 150.0, 0, 0, 0.26, -0.15, "" }, new object[] { 240.0, 0, 2, 0.45, 0.1, "" }, new object[] { 200.0, 1, 0, 0.5, -0.05, "" } }[i];
                var b2 = SoundGen.VocalSyllable(r, (double)sh[0] * r.Range(0.93f, 1.08f), (int)sh[1], (int)sh[2], (double)sh[3], (double)sh[4], 1, (string)sh[5]); return SoundGen.Wet(b2, 35, 50, 0.18f, b2.Length + 12000);
            }
        }
        return null;
    }
    static List<double[]> MidiNotes(string code, Recipe rec, int i, SoundItem s)
    {
        var r = new Rnd(rec.Seed * 17 + i * 233 + 11); int pc; bool minor; KeyParts(s, rec, out pc, out minor);
        switch (code)
        {
            case "md":
            {
                var p = Vary(PackBuilder.StyleFor(rec).Pat, r, i % 8); var nt = new List<double[]>(); int steps = 16 * 4;
                for (int st = 0; st < steps; st++) { int pi = st % (16 * p.Bars); Action<string, int, double> add = (s2, note, vel) => { if (pi < s2.Length && s2[pi] != '.') nt.Add(new[] { st * 0.25, 0.2, (double)note, s2[pi] == 't' ? vel * 0.8 : vel }); }; add(p.K, 36, 110); add(p.S, 38, 105); add(p.C, 39, 100); add(p.H, 42, 80); add(p.O, 46, 85); add(p.G, 38, 40); add(p.P, 37, 70); }
                return nt;
            }
            case "mb": { bool slide = rec.Drums == "drill" || rec.Drums == "trap"; if (slide && i % 2 == 1) { var line = Music.Sub808Line(r, 4, 24 + pc, rec.Drums == "drill"); var nt = new List<double[]>(); foreach (var n in line) nt.Add(new[] { n[0], n[1], n[2], n[3] }); return nt; } return Music.BassLine(r, Music.RhythmFor(rec.Drums), 4, 36 + pc, minor); }
            case "mc": return Music.Chords(r, 4, pc, minor, 3, i % 3 == 2 ? "stab" : "pad");
            default: return Music.Melody(r, 4, pc, minor, i % 2 == 0 ? 5 : 4);
        }
    }

    // a MIDI file heard in the browser: played with the pack's kit (drums), its bass (basslines) or one of its instruments (chords, melodies)
    public static Buf MidiPreview(SoundItem s)
    {
        if (s.Instant) { string e = Ensure(s); if (e != null) return null; }
        double bpm; var notes = SoundIO.ReadMidi(s.Path, out bpm); if (notes.Count == 0) return null; if (bpm <= 0) bpm = s.Bpm > 0 ? s.Bpm : 120;
        double end = 0; foreach (var n in notes) end = Math.Max(end, n[0] + n[1]); end = Math.Max(4, Math.Ceiling(end / 4) * 4);
        var rec = SoundCatalog.Find(s.PackId) ?? SoundCatalog.Recipes[0]; string ty = (s.Type + " " + s.Name).ToLowerInvariant(); Buf b;
        if (ty.Contains("drum")) b = Instruments.RenderDrums(Kit(rec, 0), notes, bpm, end);
        else if (ty.Contains("bass") || ty.Contains("808")) { string fl = rec.Flavours != null && rec.Flavours.Length > 0 && rec.Flavours[0].Length > 0 ? rec.Flavours[0] : "sub"; b = SoundGen.RenderSynth(Music.BassPatch(fl, new Rnd(rec.Seed)), notes, bpm, end, 0.5); }
        else { var pal = Instruments.Palette(rec); bool chords = ty.Contains("chord"); var ins = pal.Find(x => chords ? x.Chords : x.Melody) ?? Instruments.Get("Grand Piano"); b = Instruments.RenderPart(ins, notes, bpm, end, 1.5); }
        return PackWriter.Finish(b, "Melody Loop", "loop", -2.0);
    }

    public static Buf RenderDrum(Recipe rec, int i, double bpm)
    {
        var st = PackBuilder.StyleFor(rec); var kit = Kit(rec, i % 6);
        var r = new Rnd(rec.Seed * 7 + i * 131 + 5); var pat = Vary(st.Pat, r, i % 8); double sw = st.Swing + (r.Next() - 0.5) * 0.03;
        var lp = DrumKit.Render(kit, st, pat, bpm, Bars(i), sw, rec.Seed + 900 + i * 13, i % 8 == 3);
        return lp.Mix;
    }
    public static Buf RenderDrum(Recipe rec, int i) { return RenderDrum(rec, i, Bpm(rec, i)); }
    // a groove from the pack's own pattern: the backbeat stays, the kicks, hats, ghosts and percussion move, and bar two answers bar one
    public static Pat Vary(Pat p, Rnd r, int feel)
    {
        int n = 16 * p.Bars; Func<string, string> norm = s => { s = s ?? ""; if (s.Length == 0) return ""; var sb = new StringBuilder(); for (int k = 0; k < n; k++) sb.Append(s[k % s.Length]); return sb.ToString(); };
        var o = new Pat { K = norm(p.K), S = norm(p.S), C = norm(p.C), H = norm(p.H), O = norm(p.O), P = norm(p.P), G = norm(p.G), Bars = p.Bars };
        Func<string, int, char> at = (s, k) => s.Length == 0 ? '.' : s[k];
        Func<int, bool> backbeat = k => at(o.S, k) != '.' || at(o.C, k) != '.';
        var K = (o.K.Length == 0 ? new string('.', n) : o.K).ToCharArray(); var H = (o.H.Length == 0 ? new string('.', n) : o.H).ToCharArray(); var O = (o.O.Length == 0 ? new string('.', n) : o.O).ToCharArray(); var G = (o.G.Length == 0 ? new string('.', n) : o.G).ToCharArray(); var P = (o.P.Length == 0 ? new string('.', n) : o.P).ToCharArray();
        bool fourFloor = o.K.StartsWith("x...x...x...x..."); bool hasRoll = o.H.IndexOf('t') >= 0;
        int moves = 1 + r.Int(2); for (int m = 0; m < moves; m++) { int k = r.Int(n); if (k % 4 == 0 && fourFloor) continue; if (backbeat(k)) continue; if (K[k] == '.') K[k] = 'x'; else if (k % 8 != 0 && !fourFloor) K[k] = '.'; }
        if (feel == 1) { for (int k = 0; k < n; k++) if (H[k] == '.' && r.Next() < 0.5f) H[k] = 'x'; }
        else if (feel == 2) { for (int k = 0; k < n; k++) if (H[k] != '.' && k % 4 != 0 && r.Next() < 0.45f) H[k] = '.'; for (int k = 0; k < n; k++) if (P[k] != '.' && r.Next() < 0.5f) P[k] = '.'; }
        else if (feel == 5) { int rolls = 1 + r.Int(3); for (int m = 0; m < rolls; m++) { int k = r.Int(n); if (!hasRoll && r.Next() < 0.5f) k = k - k % 2 + 1; H[k % n] = 't'; } }
        else if (feel == 6) { for (int k = 0; k < n; k++) if (P[k] == '.' && !backbeat(k) && r.Next() < 0.28f) P[k] = 'x'; }
        else if (feel == 7) { for (int k = 0; k < n; k++) if (G[k] == '.' && !backbeat(k) && K[k] == '.' && r.Next() < 0.22f) G[k] = 'g'; }
        else { for (int k = 0; k < n; k++) if (r.Next() < 0.08f && k % 4 != 0) H[k] = H[k] == '.' ? 'x' : '.'; }
        if (r.Next() < 0.5f) { for (int k = 0; k < n; k++) if (O[k] != '.' && r.Next() < 0.5f) O[k] = '.'; int k2 = (r.Int(n / 2) * 2 + 2) % n; if (!backbeat(k2)) O[k2] = 'x'; }
        o.K = new string(K); o.H = new string(H); o.O = new string(O); o.G = new string(G); o.P = new string(P);
        if (o.Bars == 1)
        {
            var K2 = o.K.ToCharArray(); var H2 = o.H.ToCharArray(); int a = 10 + r.Int(6); if (!backbeat(a) && !(fourFloor && a % 4 == 0)) K2[a] = K2[a] == '.' ? 'x' : '.'; int h2 = 12 + r.Int(4); H2[h2] = H2[h2] == '.' ? 'x' : (hasRoll ? 't' : H2[h2]);
            o = new Pat { K = o.K + new string(K2), S = o.S + o.S, C = o.C + o.C, H = o.H + new string(H2), O = o.O + o.O, P = o.P + o.P, G = o.G + o.G, Bars = 2 };
        }
        return o;
    }

    public static Buf RenderBass(Recipe rec, int i, double bpm, string key)
    {
        int nf = rec.Flavours.Length; string fl = rec.Flavours[i % nf]; if (string.IsNullOrEmpty(key)) key = rec.Keys[(i / nf) % rec.Keys.Length];
        var r = new Rnd(rec.Seed * 13 + i * 197 + 3); int root = 36 + Music.Root(key); bool minor = Music.IsMinor(key);
        var notes = Music.BassLine(r, Music.RhythmFor(rec.Drums), 2, root, minor); var patch = Music.BassPatch(fl, r);
        return PackBuilder.LoopLen(SoundGen.RenderSynth(patch, notes, bpm, 8, 0.3), 8, bpm);
    }
    public static Buf RenderBass(Recipe rec, int i) { return RenderBass(rec, i, Bpm(rec, i / 2), ""); }
}
