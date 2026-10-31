using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

// =====================================================================================================
//  PURPHEX SOUNDS: the library behind the sound browser.
//  A pack is a folder in the data folder (sounds\<pack id>\) holding real audio / MIDI / preset files and a pack.txt that lists
//  them with their tempo, key, type, tags and licence. Packs are made by Sounds.Packs (original sounds synthesised on this PC)
//  or imported (a .zip in the same layout, or your own samples). The browser only ever reads the index, so new packs appear
//  without any change to the interface.
// =====================================================================================================
class SoundItem
{
    public string PackId = "", Name = "", Type = "", Group = "", Kind = "one-shot", Genre = "", Sub = "", Tags = "", Mood = "", Key = "", Format = "", Rel = "", Instrument = "";
    string id, path;   // worked out when first asked for (the library lists 100,000+ sounds; most are never opened)
    public string Id { get { return id ?? (id = PackId + "/" + Rel); } set { id = value; } }
    public string Path { get { if (path == null) path = Spec.Length > 0 ? InstantLib.PathOf(this) : ""; return path; } set { path = value; } }
    public double Bpm, Dur; public int Rate = 44100; public byte[] Peaks = Empty; static readonly byte[] Empty = new byte[0];
    public string Core = "", BaseKey = ""; public double BaseBpm, Beats;   // instant sounds: the name without tempo and key, the pack's own tempo and key, and the length in beats
    public bool IsAudio { get { string e = System.IO.Path.GetExtension(Rel).ToLowerInvariant(); return e == ".wav" || e == ".mp3" || e == ".flac" || e == ".ogg" || e == ".aiff" || e == ".m4a"; } }
    public bool Exists { get { return File.Exists(Path); } }
    // a search word is in one of the sound's labels (nothing is built per sound: the browser searches every sound on each key press)
    public bool Matches(string w) { if (Spec.Length > 0) return Tags.IndexOf(w, StringComparison.Ordinal) >= 0 || In(Name, w) || In(Key, w);   // an instant sound carries every label in its (shared, lower-case) tags
        return (Tags != null && Tags.IndexOf(w, StringComparison.Ordinal) >= 0) || In(Name, w) || Lo(Type, w) || Lo(Instrument, w) || Lo(Key, w) || Lo(PackName, w) || Lo(Genre, w) || Lo(Sub, w) || Lo(Mood, w) || Lo(Group, w) || Lo(Kind, w); }   // (w is lower case)
    static bool In(string s, string w) { return s != null && s.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0; }
    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> lower = new System.Collections.Concurrent.ConcurrentDictionary<string, string>();
    static bool Lo(string s, string w) { if (string.IsNullOrEmpty(s)) return false; string l; if (!lower.TryGetValue(s, out l)) { l = s.ToLowerInvariant(); if (lower.Count < 20000) lower[s] = l; } return l.IndexOf(w, StringComparison.Ordinal) >= 0; }
    public string PackName = "";
    public string Spec = "";   // an instant loop: the recipe it is made from the first time it is played (see InstantLib)
    public bool Instant { get { return Spec.Length > 0; } }
    // the details of an installed sound (checked when it was made; see LibraryBaker): bars, channels, bit depth, peak and loudness, licence, where it came from, how it was made,
    // when, its check result, a fingerprint and a hash of its audio (for spotting copies), and the sound it is a version of
    public int Bars, Channels, Bits; public double PeakDb, RmsDb; public string Licence = "", Source = "", Origin = "", Method = "", Created = "", Valid = "", Hash = "", VarOf = ""; public float[] Fp;
    public SoundItem Copy() { return (SoundItem)MemberwiseClone(); }
    // a sound with a musical key (drums, percussion and most FX have none: a key filter never hides them)
    public bool Tonal { get { return Key.Length > 0; } }
}

class SoundPack
{
    public string Id = "", Name = "", Section = "", Genre = "", Sub = "", Description = "", License = "", Version = "1"; public int Hue = 280; public List<SoundItem> Items = new List<SoundItem>(); public bool Installed;
    public string Dir { get { return System.IO.Path.Combine(SoundLibrary.Root, Id); } }
    public long Bytes { get { long b = 0; foreach (var i in Items) { try { if (i.Exists) b += new FileInfo(i.Path).Length; } catch { } } return b; } }
}

// loads the library on its own thread when the studio starts (a test loads it at once)
static class LibLoader
{
    public static volatile bool Ready; static volatile bool started; public static Action Done;
    public static void Start()
    {
        if (started) return; started = true;
        if (StudioEnv.UnderTest) { SoundLibrary.Load(); int n0 = InstantLib.Items.Count; Ready = true; return; }
        new System.Threading.Thread(() => { try { SoundLibrary.Load(); int n = InstantLib.Items.Count; } catch { } Ready = true; var d = Done; if (d != null) try { d(); } catch { } }) { IsBackground = true, Priority = System.Threading.ThreadPriority.BelowNormal, Name = "library" }.Start();
    }
}

static class SoundLibrary
{
    public static string Root { get { return Path.Combine(StudioEnv.DataDir, "sounds"); } }
    public static readonly List<SoundPack> Packs = new List<SoundPack>(); public static readonly object Lock = new object();
    public static readonly HashSet<string> Favourites = new HashSet<string>(); public static readonly List<string> Recent = new List<string>(); public static readonly Dictionary<string, List<string>> Collections = new Dictionary<string, List<string>>();
    public static float AuditionVolume = 0.8f; public static bool AutoPlay = true, LoopPreview;
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    public const string UserPackId = "my-samples";

    public const string LicenceText = "Purphex Sounds licence: every sound in a Purphex pack was generated by Purphex's own sound-design code on your computer. It contains no recordings or samples made by anyone else. You may use the sounds in your own music, including commercial releases, without paying or crediting anyone. You may not sell, share or upload the sounds on their own (as samples, loops or a pack) or train a sound-generating model on them.";

    // ---- index ----
    public static void Load()
    {
        lock (Lock)
        {
            Packs.Clear(); try { Directory.CreateDirectory(Root); } catch { }
            try { foreach (var d in Directory.GetDirectories(Root)) { var p = ReadPack(Path.Combine(d, "pack.txt")); if (p != null) Packs.Add(p); } } catch { }
            foreach (var p in Packs) { foreach (var s in p.Items) if (s.Instrument.Length == 0) s.Instrument = InstrumentOf(s); var rec = SoundCatalog.Find(p.Id); if (rec == null) continue; p.Section = rec.Section; p.Genre = rec.Genre; p.Sub = rec.Sub; foreach (var s in p.Items) { s.Genre = rec.Genre; s.Sub = rec.Sub; } }   // packs installed before a relabel show the right genre and style
            if (!Packs.Exists(p => p.Id == UserPackId)) Packs.Add(new SoundPack { Id = UserPackId, Name = "My samples", Section = "Yours", Genre = "Any", Description = "Samples you imported yourself.", License = "Yours: you told Purphex you own or have the rights to these files.", Hue = 200, Installed = true });
            Packs.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            ReadUser();
        }
    }

    // a pack list read from any file (pack.txt, or pack.part while a pack is being installed)
    internal static SoundPack ReadPackFile(string file, string dir) { var p = ReadPack(file); if (p == null) return null; foreach (var s in p.Items) s.Path = Path.Combine(dir, s.Rel.Replace('/', Path.DirectorySeparatorChar)); return p; }
    static SoundPack ReadPack(string file)
    {
        if (!File.Exists(file)) return null; SoundPack p = null; string dir = Path.GetDirectoryName(file);
        foreach (var line in File.ReadAllLines(file, Encoding.UTF8))
        {
            var f = line.Split('|');
            if (f[0] == "PACK" && f.Length >= 10) { p = new SoundPack { Id = f[1], Name = U(f[2]), Section = U(f[3]), Genre = U(f[4]), Sub = U(f[5]), Version = f[6], License = U(f[7]), Description = U(f[8]), Installed = true }; int h; if (int.TryParse(f[9], out h)) p.Hue = h; }
            else if (f[0] == "M" && p != null && p.Items.Count > 0 && f.Length >= 16)
            {
                var s = p.Items[p.Items.Count - 1]; int iv; double dv;
                if (int.TryParse(f[1], out iv)) s.Bars = iv; if (int.TryParse(f[2], out iv)) s.Channels = iv; if (int.TryParse(f[3], out iv)) s.Bits = iv;
                if (double.TryParse(f[4], NumberStyles.Float, CI, out dv)) s.PeakDb = dv; if (double.TryParse(f[5], NumberStyles.Float, CI, out dv)) s.RmsDb = dv;
                s.Licence = f[6]; s.Source = f[7]; s.Origin = f[8]; s.Method = f[9]; s.Created = f[10]; s.Valid = f[11]; s.Hash = f[12]; s.VarOf = f[13]; s.Fp = LibraryBaker.FpParse(f[14]); if (double.TryParse(f[15], NumberStyles.Float, CI, out dv)) s.Beats = dv;
            }
            else if (f[0] == "S" && p != null && f.Length >= 17)
            {
                var s = new SoundItem { PackId = p.Id, PackName = p.Name, Rel = f[1], Name = U(f[2]), Type = f[3], Group = f[4], Kind = f[5], Genre = U(f[6]), Sub = U(f[7]), Tags = U(f[8]), Key = f[10], Format = U(f[13]), Instrument = U(f[14]), Mood = U(f[15]) };
                double b, d; int r; double.TryParse(f[9], NumberStyles.Float, CI, out b); double.TryParse(f[11], NumberStyles.Float, CI, out d); int.TryParse(f[12], out r); s.Bpm = b; s.Dur = d; s.Rate = r > 0 ? r : 44100;
                try { s.Peaks = Convert.FromBase64String(f[16]); } catch { s.Peaks = new byte[0]; }
                s.Path = Path.Combine(dir, f[1].Replace('/', Path.DirectorySeparatorChar)); s.Id = p.Id + "/" + f[1]; p.Items.Add(s);
            }
        }
        return p;
    }

    public static void WritePack(SoundPack p)
    {
        Directory.CreateDirectory(p.Dir); string text = PackText(p);
        string tmp = Path.Combine(p.Dir, "pack.txt.tmp"); File.WriteAllText(tmp, text, Encoding.UTF8); string fin = Path.Combine(p.Dir, "pack.txt"); if (File.Exists(fin)) File.Delete(fin); File.Move(tmp, fin);
        File.WriteAllText(Path.Combine(p.Dir, "LICENCE.txt"), p.Name + "\r\n" + new string('-', p.Name.Length) + "\r\n" + p.License + "\r\n", Encoding.UTF8);
    }
    // the pack list: a PACK line, then an S line per sound (and an M line with its checked details for sounds made by the library baker)
    internal static string PackText(SoundPack p)
    {
        var sb = new StringBuilder();
        sb.Append("PACK|").Append(p.Id).Append('|').Append(E(p.Name)).Append('|').Append(E(p.Section)).Append('|').Append(E(p.Genre)).Append('|').Append(E(p.Sub)).Append('|').Append(p.Version).Append('|').Append(E(p.License)).Append('|').Append(E(p.Description)).Append('|').Append(p.Hue).Append('\n');
        foreach (var s in p.Items)
        {
            sb.Append("S|").Append(s.Rel).Append('|').Append(E(s.Name)).Append('|').Append(s.Type).Append('|').Append(s.Group).Append('|').Append(s.Kind).Append('|').Append(E(s.Genre)).Append('|').Append(E(s.Sub)).Append('|').Append(E(s.Tags)).Append('|')
              .Append(s.Bpm.ToString("0.##", CI)).Append('|').Append(s.Key).Append('|').Append(s.Dur.ToString("0.###", CI)).Append('|').Append(s.Rate).Append('|').Append(E(s.Format)).Append('|').Append(E(s.Instrument)).Append('|').Append(E(s.Mood)).Append('|').Append(Convert.ToBase64String(s.Peaks ?? new byte[0])).Append('\n');
            if (s.Valid.Length > 0 || s.Origin.Length > 0)
                sb.Append("M|").Append(s.Bars).Append('|').Append(s.Channels).Append('|').Append(s.Bits).Append('|').Append(s.PeakDb.ToString("0.##", CI)).Append('|').Append(s.RmsDb.ToString("0.##", CI)).Append('|').Append(E(s.Licence)).Append('|').Append(E(s.Source)).Append('|').Append(E(s.Origin))
                  .Append('|').Append(E(s.Method)).Append('|').Append(s.Created).Append('|').Append(s.Valid).Append('|').Append(s.Hash).Append('|').Append(E(s.VarOf)).Append('|').Append(LibraryBaker.FpText(s.Fp)).Append('|').Append(s.Beats.ToString("0.##", CI)).Append('\n');
        }
        return sb.ToString();
    }
    static string E(string s) { return (s ?? "").Replace("|", "/").Replace("\r", " ").Replace("\n", " "); }
    static string U(string s) { return s; }

    public static void AddOrReplace(SoundPack p) { lock (Lock) { Packs.RemoveAll(x => x.Id == p.Id); Packs.Add(p); Packs.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)); } }
    public static void RemovePack(string id) { lock (Lock) { Packs.RemoveAll(x => x.Id == id && id != UserPackId); } try { string d = Path.Combine(Root, id); if (id != UserPackId && Directory.Exists(d)) Directory.Delete(d, true); } catch { } }
    public static SoundPack Pack(string id) { lock (Lock) return Packs.Find(p => p.Id == id); }

    public static List<SoundItem> All() { var l = new List<SoundItem>(); lock (Lock) foreach (var p in Packs) l.AddRange(p.Items); l.AddRange(InstantLib.Items); return l; }   // the installed packs, then every instant loop
    public static SoundItem Find(string id) { lock (Lock) foreach (var p in Packs) foreach (var s in p.Items) if (s.Id == id) return s; return InstantLib.Find(id); }

    // ---- search ----
    public class Filter
    {
        public string Text = "", Genre = "", Sub = "", Type = "", Group = "", Key = "", Kind = "", Pack = "", Mood = "", Instrument = ""; public double BpmMin, BpmMax; public bool HalfDouble, OnlyFav, OnlyRecent; public string Collection = ""; public string[] AnyType;
        // Avail: "" = installed sounds only (real files), "instant" = sounds made when first played, "all" = both
        public string Avail = ""; public double DurMin, DurMax; public int Bars, Channels; public string Source = "", Family = ""; public int AddedDays; public bool OnlyKeyed;
        public bool IsEmpty { get { return Text.Length == 0 && Genre.Length == 0 && Sub.Length == 0 && Type.Length == 0 && Group.Length == 0 && Key.Length == 0 && Kind.Length == 0 && Pack.Length == 0 && Mood.Length == 0 && Instrument.Length == 0 && BpmMax <= 0 && !OnlyFav && !OnlyRecent && Collection.Length == 0 && AnyType == null; } }
        public Filter Copy() { return (Filter)MemberwiseClone(); }
    }
    // where a sound came from (the Source filter)
    public static string SourceOf(SoundItem s) { if (s.PackId == UserPackId) return "My samples"; if (s.Instant) return "Purphex (made on play)"; return "Purphex original"; }
    public static string FamilyOfItem(SoundItem s) { switch (s.Group) { case "Drums": case "Loops": return s.Type.IndexOf("Bass", StringComparison.OrdinalIgnoreCase) >= 0 ? "Bass" : "Drums"; case "Bass": return "Bass"; case "FX": return "FX"; case "Vocal": return "Vocals"; case "MIDI": return "MIDI"; } return s.Group.Length > 0 ? s.Group : "Other"; }
    static bool AddedWithin(SoundItem s, int days) { DateTime d; return s.Created.Length > 0 && DateTime.TryParse(s.Created, CI, DateTimeStyles.AssumeUniversal, out d) && (DateTime.UtcNow - d).TotalDays <= days; }
    public static int InstalledCount() { int n = 0; lock (Lock) foreach (var p in Packs) n += p.Items.Count; return n; }
    // the installed (real) sounds only
    public static List<SoundItem> Installed() { var l = new List<SoundItem>(); lock (Lock) foreach (var p in Packs) l.AddRange(p.Items); return l; }
    public static List<SoundItem> Search(Filter f)
    {
        var res = new List<SoundItem>(); var words = (f.Text ?? "").ToLowerInvariant().Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries); List<string> coll = null; if (!string.IsNullOrEmpty(f.Collection)) lock (Lock) Collections.TryGetValue(f.Collection, out coll);
        foreach (var s in All())
        {
            if (s.Instant ? f.Avail.Length == 0 : f.Avail == "instant") continue;   // installed (real files) or made on play
            if (f.DurMax > 0 && (s.Dur < f.DurMin || s.Dur > f.DurMax)) continue; if (f.Bars > 0 && (s.Beats > 0 ? (int)Math.Round(s.Beats / 4) : s.Bars) != f.Bars) continue; if (f.Channels > 0 && s.Channels != f.Channels) continue;
            if (f.Source.Length > 0 && SourceOf(s) != f.Source) continue; if (f.Family.Length > 0 && FamilyOfItem(s) != f.Family) continue; if (f.AddedDays > 0 && !AddedWithin(s, f.AddedDays)) continue;
            if (f.OnlyFav && !Favourites.Contains(s.Id)) continue; if (f.OnlyRecent && !Recent.Contains(s.Id)) continue; if (coll != null && !coll.Contains(s.Id)) continue; if (!string.IsNullOrEmpty(f.Collection) && coll == null) continue;
            if (f.Instrument.Length > 0 && s.Instrument != f.Instrument) continue;
            if (f.Genre.Length > 0 && s.Genre != f.Genre) continue; if (f.Sub.Length > 0 && s.Sub != f.Sub) continue; if (f.Type.Length > 0 && s.Type != f.Type) continue; if (f.Group.Length > 0 && s.Group != f.Group) continue; if (f.Kind.Length > 0 && s.Kind != f.Kind) continue;
            if (f.AnyType != null && Array.IndexOf(f.AnyType, s.Type) < 0) continue; if (f.Pack.Length > 0 && s.PackId != f.Pack) continue; if (f.Mood.Length > 0 && s.Mood.IndexOf(f.Mood, StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (f.Key.Length > 0 && (s.Tonal || f.OnlyKeyed) && !KeyMatches(s.Key, f.Key)) continue;
            if (f.BpmMax > 0) { if (s.Bpm <= 0) continue; bool ok = s.Bpm >= f.BpmMin && s.Bpm <= f.BpmMax; if (!ok && f.HalfDouble) ok = (s.Bpm * 2 >= f.BpmMin && s.Bpm * 2 <= f.BpmMax) || (s.Bpm / 2 >= f.BpmMin && s.Bpm / 2 <= f.BpmMax); if (!ok) continue; }
            bool all = true; foreach (var w in words) if (!s.Matches(w)) { all = false; break; } if (!all) continue;
            res.Add(s);
        }
        if (f.OnlyRecent) { res.Sort((a, b) => Recent.IndexOf(b.Id).CompareTo(Recent.IndexOf(a.Id))); return res; }
        // pack by pack: the installed sounds are sorted by name, the instant ones are already in pack order; the two lists are merged (sorting 100,000 names on every key press was slow)
        var mine = res.FindAll(s => !s.Instant); var inst = res.FindAll(s => s.Instant); mine.Sort((a, b) => { int c = string.Compare(a.PackName, b.PackName, StringComparison.OrdinalIgnoreCase); return c != 0 ? c : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); });
        var outl = new List<SoundItem>(res.Count); int ia = 0, ib = 0; while (ia < mine.Count || ib < inst.Count) { if (ib >= inst.Count || (ia < mine.Count && string.Compare(mine[ia].PackName, inst[ib].PackName, StringComparison.OrdinalIgnoreCase) <= 0)) outl.Add(mine[ia++]); else outl.Add(inst[ib++]); }
        return outl;
    }
    // "F minor" matches "F min", "Fm" and its relative major "Ab major" is NOT folded in: only the same key (sharps and flats are treated as the same note)
    public static string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
    public static bool KeyMatches(string have, string want)
    {
        if (string.IsNullOrEmpty(have)) return false; int a, b; bool am, bm; if (!ParseKey(have, out a, out am) || !ParseKey(want, out b, out bm)) return string.Equals(have, want, StringComparison.OrdinalIgnoreCase); return a == b && am == bm;
    }
    public static bool ParseKey(string k, out int root, out bool minor)
    {
        root = 0; minor = false; if (string.IsNullOrWhiteSpace(k)) return false; k = k.Trim(); char c = char.ToUpperInvariant(k[0]); int[] semis = { 9, 11, 0, 2, 4, 5, 7 }; if (c < 'A' || c > 'G') return false; root = semis[c - 'A'];
        int i = 1; if (i < k.Length && (k[i] == '#' || k[i] == '♯')) { root++; i++; } else if (i < k.Length && (k[i] == 'b' || k[i] == '♭')) { root--; i++; } root = ((root % 12) + 12) % 12;
        string rest = k.Substring(i).Trim().ToLowerInvariant(); minor = rest.StartsWith("m") && !rest.StartsWith("maj"); return true;
    }
    public static string KeyName(int root, bool minor) { return NoteNames[((root % 12) + 12) % 12] + (minor ? " min" : " maj"); }

    // the instrument a sound is (for the Instrument filter): from its type, name and tags
    public static string InstrumentOf(SoundItem s)
    {
        string t = s.Type ?? "", h = ((s.Name ?? "") + " " + (s.Tags ?? "")).ToLowerInvariant();
        switch (t) { case "Kick": return "Kick"; case "Snare": return "Snare"; case "Clap": return "Clap"; case "Hi-Hat": case "Open Hat": return "Hi-Hat"; case "Percussion": case "Percussion Loop": case "Top Loop": return "Percussion"; case "Ride": case "Cymbal": return "Cymbal"; case "808": return "808"; case "Drum Loop": case "Drum Fill": case "MIDI Drums": return "Drums"; }
        if (t.Contains("Bass") || h.Contains(" bass")) return "Bass Synth"; if (t.Contains("Vocal")) return "Voice"; if (s.Group == "FX" || t == "Riser" || t == "Impact" || t == "Sweep" || t == "Downlifter" || t == "Reverse" || t == "Transition") return "FX";
        foreach (var ins in Instruments.All) if (h.Contains(ins.Name.ToLowerInvariant()) || (ins.Preset != null && h.Contains(ins.Preset.ToLowerInvariant()))) return ins.Name;
        if (h.Contains("guitar") || h.Contains("harp")) return "Acoustic Guitar"; if (h.Contains("organ")) return "Organ"; if (h.Contains("pad")) return "Soft Pad"; if (h.Contains("arp") || h.Contains("lead") || h.Contains("synth")) return "Supersaw";
        return "";
    }
    public static List<string> Distinct(Func<SoundItem, string> sel) { var set = new SortedDictionary<string, int>(StringComparer.OrdinalIgnoreCase); foreach (var s in All()) { string v = sel(s); if (!string.IsNullOrEmpty(v)) set[v] = 1; } return new List<string>(set.Keys); }

    // ---- favourites, recents, collections ----
    static string UserFile { get { return Path.Combine(Root, "user.txt"); } }
    static void ReadUser()
    {
        Favourites.Clear(); Recent.Clear(); Collections.Clear();
        try
        {
            if (!File.Exists(UserFile)) return;
            foreach (var line in File.ReadAllLines(UserFile, Encoding.UTF8))
            {
                var f = line.Split('|');
                if (f[0] == "fav" && f.Length > 1) Favourites.Add(f[1]); else if (f[0] == "recent" && f.Length > 1) Recent.Add(f[1]);
                else if (f[0] == "coll" && f.Length > 2) Collections[f[1]] = new List<string>(f[2].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
                else if (f[0] == "vol" && f.Length > 1) { float v; if (float.TryParse(f[1], NumberStyles.Float, CI, out v)) AuditionVolume = Math.Max(0, Math.Min(1, v)); }
                else if (f[0] == "auto" && f.Length > 1) AutoPlay = f[1] == "1";
            }
        }
        catch { }
    }
    public static void SaveUser()
    {
        try
        {
            Directory.CreateDirectory(Root); var sb = new StringBuilder(); foreach (var f in Favourites) sb.Append("fav|").Append(f).Append('\n'); foreach (var r in Recent) sb.Append("recent|").Append(r).Append('\n');
            foreach (var kv in Collections) sb.Append("coll|").Append(kv.Key.Replace("|", "/")).Append('|').Append(string.Join(";", kv.Value.ToArray())).Append('\n');
            sb.Append("vol|").Append(AuditionVolume.ToString("0.##", CI)).Append('\n').Append("auto|").Append(AutoPlay ? "1" : "0").Append('\n'); File.WriteAllText(UserFile, sb.ToString(), Encoding.UTF8);
        }
        catch { }
    }
    public static void ToggleFav(SoundItem s) { if (!Favourites.Remove(s.Id)) Favourites.Add(s.Id); SaveUser(); }
    public static void MarkUsed(SoundItem s) { Recent.Remove(s.Id); Recent.Add(s.Id); while (Recent.Count > 60) Recent.RemoveAt(0); SaveUser(); }
    public static void AddToCollection(string name, SoundItem s) { List<string> l; if (!Collections.TryGetValue(name, out l)) { l = new List<string>(); Collections[name] = l; } if (!l.Contains(s.Id)) l.Add(s.Id); SaveUser(); }
    public static void RemoveFromCollection(string name, SoundItem s) { List<string> l; if (Collections.TryGetValue(name, out l)) { l.Remove(s.Id); if (l.Count == 0) Collections.Remove(name); SaveUser(); } }

    // ---- importing your own samples (files you own) ----
    // duplicates: a sound that is already in My samples (the same file, whatever its name) is not imported again, and copies already there are removed
    public static int LastDupes;   // how many the last import skipped or removed as duplicates
    public static readonly List<SoundItem> LastDupeItems = new List<SoundItem>();   // the sounds in My samples those duplicates are copies of (LOCATE finds them)
    static readonly Dictionary<string, string> hashCache = new Dictionary<string, string>();
    static string FileHash(string path)
    {
        try
        {
            var fi = new FileInfo(path); if (!fi.Exists) return null; string key = fi.FullName.ToLowerInvariant() + "|" + fi.Length + "|" + fi.LastWriteTimeUtc.Ticks; string h;
            lock (hashCache) if (hashCache.TryGetValue(key, out h)) return h;
            using (var md5 = System.Security.Cryptography.MD5.Create()) using (var st = File.OpenRead(path)) h = fi.Length + ":" + BitConverter.ToString(md5.ComputeHash(st));
            lock (hashCache) hashCache[key] = h; return h;
        }
        catch { return null; }
    }
    // removes the extra copies of the same sound from My samples (the first one stays); returns how many went
    public static int RemoveDuplicates(SoundPack up)
    {
        if (up == null) return 0; var seen = new Dictionary<string, SoundItem>(); var gone = new List<SoundItem>();
        foreach (var s in up.Items) { string h = FileHash(s.Path); if (h == null) continue; SoundItem keep; if (seen.TryGetValue(h, out keep)) { gone.Add(s); if (!LastDupeItems.Contains(keep)) LastDupeItems.Add(keep); } else seen[h] = s; }
        foreach (var s in gone) up.Items.Remove(s);   // (taken off the list only: the file stays, so a pad or a project that uses it keeps working)
        return gone.Count;
    }
    public static int ImportFiles(string[] files, Action<string> progress)
    {
        var up = Pack(UserPackId) ?? new SoundPack { Id = UserPackId, Name = "My samples" }; Directory.CreateDirectory(up.Dir); int n = 0; LastDupeItems.Clear(); LastDupes = RemoveDuplicates(up);
        var have = new Dictionary<string, SoundItem>(); foreach (var s0 in up.Items) { string h0 = FileHash(s0.Path); if (h0 != null && !have.ContainsKey(h0)) have[h0] = s0; }
        foreach (var f in files)
        {
            try
            {
                string ext = Path.GetExtension(f).ToLowerInvariant(); if (ext != ".wav" && ext != ".mp3" && ext != ".flac" && ext != ".ogg" && ext != ".aiff" && ext != ".m4a" && ext != ".mid") continue;
                string hf = FileHash(f); SoundItem was; if (hf != null && have.TryGetValue(hf, out was)) { LastDupes++; if (was != null && !LastDupeItems.Contains(was)) LastDupeItems.Add(was); continue; }   // the same sound is already in My samples (or twice in this import)
                if (hf != null) have[hf] = null;
                string name = Path.GetFileNameWithoutExtension(f), fn = Path.GetFileName(f); string dest = Path.Combine(up.Dir, "imports"); Directory.CreateDirectory(dest);
                for (int k = 2; File.Exists(Path.Combine(dest, fn)) || up.Items.Exists(x => x.Rel == "imports/" + fn); k++) { name = Path.GetFileNameWithoutExtension(f) + " (" + k + ")"; fn = name + ext; }   // a different sound with the same name keeps both
                string rel = "imports/" + fn, to = Path.Combine(dest, fn);
                File.Copy(f, to); if (progress != null) progress("Importing " + name + "...");
                var s = new SoundItem { PackId = up.Id, PackName = up.Name, Rel = rel, Path = to, Id = up.Id + "/" + rel, Name = name, Genre = "Any", Kind = ext == ".mid" ? "midi" : GuessKind(name), Group = ext == ".mid" ? "MIDI" : "Yours", Type = ext == ".mid" ? "MIDI" : GuessType(name), Tags = "imported" };
                s.Bpm = GuessBpm(name); s.Key = GuessKey(name); s.Instrument = InstrumentOf(s);
                if (s.IsAudio) { float[] l, r; int sr; string fmt; if (SoundIO.Read(to, out l, out r, out sr, out fmt)) { s.Dur = l.Length / (double)sr; s.Rate = sr; s.Format = fmt; s.Peaks = SoundIO.Peaks(l, r, 96); } else { s.Format = ext.Substring(1).ToUpperInvariant(); } }
                up.Items.Add(s); n++; if (hf != null) have[hf] = s;
            }
            catch { }
        }
        up.Installed = true; WritePack(up); AddOrReplace(up); return n;
    }
    static string DupeNote() { return LastDupes > 0 ? " (" + LastDupes + " duplicate" + (LastDupes == 1 ? "" : "s") + " skipped or removed)" : ""; }
    public static string DupeText { get { return DupeNote(); } }
    // a sound of yours deleted from My samples: off the list, out of every folder, and its copy in the library removed (when no other sound uses that file)
    public static bool DeleteMine(SoundItem s)
    {
        var up = Pack(UserPackId); if (up == null) return false; var cur = up.Items.Find(x => x.Id == s.Id) ?? s; if (!up.Items.Remove(cur)) return false;
        lock (Lock) foreach (var kv in Collections) kv.Value.Remove(cur.Id); Favourites.Remove(cur.Id);
        try { if (cur.Path.StartsWith(up.Dir, StringComparison.OrdinalIgnoreCase) && !up.Items.Exists(x => string.Equals(x.Path, cur.Path, StringComparison.OrdinalIgnoreCase)) && File.Exists(cur.Path)) File.Delete(cur.Path); } catch { }
        WritePack(up); AddOrReplace(up); SaveUser(); return true;
    }
    // the library sound a file is (by its path), so an edit of it can go back beside it
    public static SoundItem ItemAtPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return null; string full, fn; try { full = Path.GetFullPath(path); fn = Path.GetFileName(full); } catch { return null; }
        lock (Lock) foreach (var p in Packs) foreach (var s in p.Items) if (s.Rel.EndsWith(fn, StringComparison.OrdinalIgnoreCase) && string.Equals(s.Path, full, StringComparison.OrdinalIgnoreCase)) return s;   // (the name first: a path is worked out only for a sound with the same file name)
        return null;
    }
    // an edited copy of a sound (a WAV): replace puts it in place of the original in My samples (same name; a sound in a pack is never changed),
    // otherwise it is kept as a new version, "Kick v2" (v3...), right after the original, so both are there to compare
    public static SoundItem AddVersion(SoundItem orig, string wav, bool replace)
    {
        float[] l, r; int sr; string fmt; if (orig == null || !SoundIO.Read(wav, out l, out r, out sr, out fmt) || l.Length == 0) return null;
        var up = Pack(UserPackId) ?? new SoundPack { Id = UserPackId, Name = "My samples" }; string dest = Path.Combine(up.Dir, "imports"); Directory.CreateDirectory(dest);
        { var cur = up.Items.Find(x => x.Id == orig.Id); if (cur != null) orig = cur; }   // (the library may have been reloaded since the sound was found: use the one in the list now)
        SoundItem s;
        if (replace && orig.PackId == UserPackId && up.Items.Contains(orig))
        {
            s = orig; string to = Path.GetExtension(orig.Path).ToLowerInvariant() == ".wav" ? orig.Path : Path.Combine(dest, Path.GetFileNameWithoutExtension(orig.Path) + ".wav");
            if (!string.Equals(to, orig.Path, StringComparison.OrdinalIgnoreCase)) { if (File.Exists(to)) to = Path.Combine(dest, Path.GetFileNameWithoutExtension(orig.Path) + " " + Guid.NewGuid().ToString("N").Substring(0, 6) + ".wav"); File.Copy(wav, to); try { File.Delete(orig.Path); } catch { } s.Rel = "imports/" + Path.GetFileName(to); s.Path = to; }
            else File.Copy(wav, to, true);   // (the same file, so favourites and kits that point at it get the edit)
        }
        else
        {
            string bn = System.Text.RegularExpressions.Regex.Replace(orig.Name, @"\s+v\d+$", ""), nm; int v = 2;
            while (up.Items.Exists(x => x.Name.Equals(bn + " v" + v, StringComparison.OrdinalIgnoreCase)) || File.Exists(Path.Combine(dest, bn + " v" + v + ".wav"))) v++;
            nm = bn + " v" + v; string to = Path.Combine(dest, nm + ".wav"); File.Copy(wav, to);
            s = new SoundItem { PackId = up.Id, PackName = up.Name, Rel = "imports/" + nm + ".wav", Path = to, Name = nm, Genre = orig.Genre.Length > 0 ? orig.Genre : "Any", Kind = orig.Kind, Group = "Yours", Type = orig.Type, Tags = (orig.Tags + " edit").Trim(), Key = orig.Key, Bpm = orig.Bpm, Mood = orig.Mood, Sub = orig.Sub };
            s.Id = up.Id + "/" + s.Rel; s.Instrument = orig.Instrument.Length > 0 ? orig.Instrument : InstrumentOf(s);
            var vre = new System.Text.RegularExpressions.Regex("^" + System.Text.RegularExpressions.Regex.Escape(bn) + @"\s+v\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            int at = up.Items.IndexOf(orig); if (at >= 0) { while (at + 1 < up.Items.Count && vre.IsMatch(up.Items[at + 1].Name)) at++; up.Items.Insert(at + 1, s); } else up.Items.Add(s);
        }
        s.Dur = l.Length / (double)sr; s.Rate = sr; s.Format = fmt; s.Peaks = SoundIO.Peaks(l, r, 96);
        up.Installed = true; WritePack(up); AddOrReplace(up); return s;
    }
    // every sample file in a folder and its sub-folders (the import keeps the sub-folder in the name so nothing clashes)
    public static List<string> SamplesIn(string folder) { var l = new List<string>(); try { foreach (var f in Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories)) { string e = Path.GetExtension(f).ToLowerInvariant(); if (e == ".wav" || e == ".mp3" || e == ".flac" || e == ".ogg" || e == ".aiff" || e == ".m4a" || e == ".mid") l.Add(f); } } catch { } l.Sort(StringComparer.OrdinalIgnoreCase); return l; }
    static string GuessKind(string n) { n = n.ToLowerInvariant(); return (n.Contains("loop") || n.Contains("break") || n.Contains("bpm")) ? "loop" : "one-shot"; }
    public static string GuessType(string n)
    {
        n = n.ToLowerInvariant(); string[][] map = { new[] { "kick", "Kick" }, new[] { "snare", "Snare" }, new[] { "clap", "Clap" }, new[] { "hat", "Hi-Hat" }, new[] { "808", "808" }, new[] { "bass", "Bass One-Shot" }, new[] { "vocal", "Vocal Chop" }, new[] { "vox", "Vocal Chop" }, new[] { "riser", "Riser" }, new[] { "impact", "Impact" }, new[] { "perc", "Percussion" }, new[] { "pad", "Pad" }, new[] { "chord", "Chord" }, new[] { "loop", "Drum Loop" } };
        foreach (var m in map) if (n.Contains(m[0])) return m[1]; return "Sample";
    }
    public static double GuessBpm(string n) { var m = System.Text.RegularExpressions.Regex.Match(n, @"(?<![0-9])(\d{2,3})\s*bpm", System.Text.RegularExpressions.RegexOptions.IgnoreCase); double b; return m.Success && double.TryParse(m.Groups[1].Value, out b) && b >= 50 && b <= 220 ? b : 0; }
    public static string GuessKey(string n) { var m = System.Text.RegularExpressions.Regex.Match(n, @"(?<![A-Za-z])([A-G][#b]?)\s?(min|maj|m)(?![a-z])", System.Text.RegularExpressions.RegexOptions.None); return m.Success ? m.Groups[1].Value + (m.Groups[2].Value.StartsWith("m") && !m.Groups[2].Value.StartsWith("maj") ? " min" : " maj") : ""; }

    // ---- packs from a .zip (same layout as an installed pack: a folder with pack.txt and the files) ----
    public static string ImportPackZip(string zip)
    {
        string tmp = Path.Combine(Path.GetTempPath(), "purphexpack_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(tmp);
        try
        {
            System.IO.Compression.ZipFile.ExtractToDirectory(zip, tmp); string packTxt = null; foreach (var f in Directory.GetFiles(tmp, "pack.txt", SearchOption.AllDirectories)) { packTxt = f; break; }
            if (packTxt == null) return "That zip is not a Purphex pack (it has no pack.txt).";
            var p = ReadPack(packTxt); if (p == null || p.Id.Length == 0 || p.Id == UserPackId) return "That pack file is not valid.";
            if (string.IsNullOrWhiteSpace(p.License)) return "That pack has no licence information, so it can't be added.";
            string src = Path.GetDirectoryName(packTxt), dst = Path.Combine(Root, p.Id); if (Directory.Exists(dst)) Directory.Delete(dst, true); CopyDir(src, dst); Load(); return null;
        }
        catch (Exception ex) { return "Couldn't read that pack: " + ex.Message; }
        finally { try { Directory.Delete(tmp, true); } catch { } }
    }
    static void CopyDir(string a, string b) { Directory.CreateDirectory(b); foreach (var f in Directory.GetFiles(a)) File.Copy(f, Path.Combine(b, Path.GetFileName(f)), true); foreach (var d in Directory.GetDirectories(a)) CopyDir(d, Path.Combine(b, Path.GetFileName(d))); }
}

// ---- audio and MIDI files ----
static class SoundIO
{
    public static void WriteWav(string path, float[] l, float[] r, int sr, int bits) { WriteWav(path, l, r, sr, bits, 2); }
    public static void WriteWav(string path, float[] l, float[] r, int sr, int bits, int ch)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)); int n = l.Length, bytes = bits / 8, dataLen = n * ch * bytes;
        using (var fs = new FileStream(path, FileMode.Create)) using (var w = new BinaryWriter(fs))
        {
            w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + dataLen); w.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)ch); w.Write(sr); w.Write(sr * ch * bytes); w.Write((short)(ch * bytes)); w.Write((short)bits);
            w.Write(Encoding.ASCII.GetBytes("data")); w.Write(dataLen);
            for (int i = 0; i < n; i++)
                for (int c = 0; c < ch; c++)
                {
                    float v = c == 0 ? l[i] : r[i]; if (v > 1) v = 1; else if (v < -1) v = -1;
                    if (bits == 16) w.Write((short)Math.Round(v * 32767.0)); else { int q = (int)Math.Round(v * 8388607.0); w.Write((byte)q); w.Write((byte)(q >> 8)); w.Write((byte)(q >> 16)); }
                }
        }
    }

    // reads WAV files directly (fast, used for previews); anything else goes through ffmpeg
    public static bool Read(string path, out float[] l, out float[] r, out int sr, out string format)
    {
        l = r = null; sr = 44100; format = "";
        try
        {
            if (!path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)) { var c = AudioClip.FromFile(path); l = c.L; r = c.R ?? c.L; format = Path.GetExtension(path).Substring(1).ToUpperInvariant(); return true; }
            byte[] b = File.ReadAllBytes(path); if (b.Length < 44 || Encoding.ASCII.GetString(b, 0, 4) != "RIFF") return false;
            int pos = 12, fmtTag = 1, ch = 2, bits = 16, dataAt = -1, dataLen = 0;
            while (pos + 8 <= b.Length)
            {
                string id = Encoding.ASCII.GetString(b, pos, 4); int len = BitConverter.ToInt32(b, pos + 4);
                if (id == "fmt ") { fmtTag = BitConverter.ToInt16(b, pos + 8); ch = BitConverter.ToInt16(b, pos + 10); sr = BitConverter.ToInt32(b, pos + 12); bits = BitConverter.ToInt16(b, pos + 22); }
                else if (id == "data") { dataAt = pos + 8; dataLen = Math.Min(len, b.Length - dataAt); break; }
                pos += 8 + len + (len & 1);
            }
            if (dataAt < 0 || (fmtTag != 1 && fmtTag != 3 && fmtTag != 0xFFFE) || ch < 1 || ch > 2) return false;
            int bytes = bits / 8, frames = dataLen / (bytes * ch); l = new float[frames]; r = new float[frames];
            for (int i = 0; i < frames; i++)
                for (int c = 0; c < ch; c++)
                {
                    int o = dataAt + (i * ch + c) * bytes; float v;
                    if (fmtTag == 3 && bits == 32) v = BitConverter.ToSingle(b, o); else if (bits == 16) v = BitConverter.ToInt16(b, o) / 32768f; else if (bits == 24) v = (((b[o + 2] << 24) | (b[o + 1] << 16) | (b[o] << 8)) >> 8) / 8388608f; else if (bits == 32) v = BitConverter.ToInt32(b, o) / 2147483648f; else if (bits == 8) v = (b[o] - 128) / 128f; else return false;
                    if (c == 0) l[i] = v; else r[i] = v;
                }
            if (ch == 1) Array.Copy(l, r, frames); format = "WAV " + bits + "-bit" + (fmtTag == 3 ? " float" : ""); return true;
        }
        catch { return false; }
    }

    // a small picture of the wave: one byte (0..255) per column, the loudest peak in that stretch
    public static byte[] Peaks(float[] l, float[] r, int cols)
    {
        var p = new byte[cols]; int n = l.Length; if (n == 0) return p; float top = 1e-6f;
        var raw = new float[cols]; for (int c = 0; c < cols; c++) { int a = (int)((long)c * n / cols), b = Math.Max(a + 1, (int)((long)(c + 1) * n / cols)); float m = 0; for (int i = a; i < b && i < n; i++) { float v = Math.Max(Math.Abs(l[i]), Math.Abs(r[i])); if (v > m) m = v; } raw[c] = m; if (m > top) top = m; }
        for (int c = 0; c < cols; c++) p[c] = (byte)Math.Min(255, raw[c] / top * 255f); return p;
    }

    // Standard MIDI file (format 0): notes are (start in beats, length in beats, pitch, velocity)
    public static void WriteMidi(string path, double bpm, string name, List<double[]> notes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)); const int ppq = 480; var ev = new List<long[]>();   // tick, order, status, d1, d2
        foreach (var n in notes) { long s = (long)Math.Round(n[0] * ppq), e = (long)Math.Round((n[0] + n[1]) * ppq); ev.Add(new[] { s, 1L, 0x90L, (long)n[2], (long)n[3] }); ev.Add(new[] { Math.Max(e, s + 1), 0L, 0x80L, (long)n[2], 0L }); }
        ev.Sort((a, b) => a[0] != b[0] ? a[0].CompareTo(b[0]) : a[1].CompareTo(b[1]));
        var tr = new MemoryStream(); Action<long> vlq = v => { var buf = new Stack<byte>(); buf.Push((byte)(v & 0x7F)); v >>= 7; while (v > 0) { buf.Push((byte)((v & 0x7F) | 0x80)); v >>= 7; } foreach (var by in buf) tr.WriteByte(by); };
        vlq(0); tr.WriteByte(0xFF); tr.WriteByte(0x51); tr.WriteByte(3); int mpq = (int)(60000000.0 / bpm); tr.WriteByte((byte)(mpq >> 16)); tr.WriteByte((byte)(mpq >> 8)); tr.WriteByte((byte)mpq);
        var nm = Encoding.ASCII.GetBytes(name ?? ""); vlq(0); tr.WriteByte(0xFF); tr.WriteByte(0x03); vlq(nm.Length); tr.Write(nm, 0, nm.Length);
        long last = 0; foreach (var e in ev) { vlq(e[0] - last); last = e[0]; tr.WriteByte((byte)e[2]); tr.WriteByte((byte)e[3]); tr.WriteByte((byte)e[4]); }
        vlq(0); tr.WriteByte(0xFF); tr.WriteByte(0x2F); tr.WriteByte(0);
        using (var fs = new FileStream(path, FileMode.Create)) using (var w = new BinaryWriter(fs))
        {
            Func<int, byte[]> be32 = v => new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v };
            w.Write(Encoding.ASCII.GetBytes("MThd")); w.Write(be32(6)); w.Write(new byte[] { 0, 0, 0, 1, (byte)((ppq >> 8) & 0xFF), (byte)(ppq & 0xFF) }); w.Write(Encoding.ASCII.GetBytes("MTrk")); w.Write(be32((int)tr.Length)); w.Write(tr.ToArray());
        }
    }
    // reads back the notes of a file written by WriteMidi (and most simple files): used by the tests and the preview
    public static List<double[]> ReadMidi(string path, out double bpm)
    {
        bpm = 120; var notes = new List<double[]>(); byte[] b = File.ReadAllBytes(path); int ppq = (b[12] << 8) | b[13], pos = 14; var open = new Dictionary<int, long[]>();
        while (pos + 8 <= b.Length)
        {
            string id = Encoding.ASCII.GetString(b, pos, 4); int len = (b[pos + 4] << 24) | (b[pos + 5] << 16) | (b[pos + 6] << 8) | b[pos + 7]; pos += 8; if (id != "MTrk") { pos += len; continue; }
            int end = pos + len; long tick = 0; int status = 0;
            while (pos < end)
            {
                long dt = 0; int by; do { by = b[pos++]; dt = (dt << 7) | (long)(by & 0x7F); } while ((by & 0x80) != 0); tick += dt;
                int s = b[pos]; if (s >= 0x80) { status = s; pos++; }
                if (status == 0xFF) { int type = b[pos++]; long ml = 0; do { by = b[pos++]; ml = (ml << 7) | (long)(by & 0x7F); } while ((by & 0x80) != 0); if (type == 0x51 && ml == 3) bpm = 60000000.0 / ((b[pos] << 16) | (b[pos + 1] << 8) | b[pos + 2]); pos += (int)ml; }
                else if ((status & 0xF0) == 0x90 || (status & 0xF0) == 0x80) { int d1 = b[pos++], d2 = b[pos++]; if ((status & 0xF0) == 0x90 && d2 > 0) open[d1] = new[] { tick, d2 }; else { long[] o; if (open.TryGetValue(d1, out o)) { notes.Add(new[] { o[0] / (double)ppq, (tick - o[0]) / (double)ppq, d1, o[1] }); open.Remove(d1); } } }
                else if ((status & 0xF0) == 0xC0 || (status & 0xF0) == 0xD0) pos += 1; else pos += 2;
            }
        }
        return notes;
    }
}

// ---- auditioning: plays one sound at a time on its own audio output, independent of the song ----
static class Auditioner
{
    static AudioOut player; static float[] pl, pr; static int srate = 44100; static volatile int pos; static volatile bool playing; static readonly object lk = new object(); public static SoundItem Current; public static volatile bool Loading; public static string Error = "";
    static DateTime stoppedAt = DateTime.MinValue;
    // fades: env is the level of the sound now (0..1), envTarget where it is going; ol / orr is the sound that was playing before, fading out
    static float env, envTarget = 1, atk = 1f / 66, oenv; static float[] ol, orr; static int opos; static volatile bool resetOnStop;
    const float Rel = 1f / 220, LoopX = 220;   // 5 ms to fade out; the loop crossfades over 5 ms too
    public static bool IsPlaying { get { return playing; } }
    public static double Seconds { get { return srate > 0 ? pos / (double)srate : 0; } }
    public static double Length { get { return pl == null ? 0 : pl.Length / (double)srate; } }
    public static float[] LeftSamples { get { return pl; } }

    public static void Play(SoundItem s, Action<string> onError)
    {
        Error = "";
        if (s == null) return; if (!s.Instant && !s.Exists) { Error = "This file is missing: " + s.Rel + ". Reinstall the pack from the pack manager."; if (onError != null) onError(Error); return; }
        Current = s; Loading = true; playing = false;
        new Thread(() =>
        {
            float[] l, r; int sr; string fmt;
            if (s.Kind == "midi") { var mb = InstantLib.MidiPreview(s); if (mb == null) { Loading = false; Error = "This MIDI file has no notes to play."; if (onError != null) onError(Error); return; } l = mb.L; r = mb.R; sr = 44100; }   // MIDI is heard through an instrument (the pack's kit, bass or keys)
            else
            {
                if (!s.IsAudio) { Loading = false; Error = "This kind of file can't be played here."; if (onError != null) onError(Error); return; }
                if (s.Instant) { string err = InstantLib.Ensure(s); if (err != null) { Loading = false; Error = err; if (onError != null) onError(Error); return; } }   // made now, the first time (a fraction of a second)
                if (!SoundIO.Read(s.Path, out l, out r, out sr, out fmt)) { Loading = false; Error = "Couldn't read " + s.Rel + " (is it a damaged file?)."; if (onError != null) onError(Error); return; }
            }
            if (sr != 44100 && sr > 0) { l = Resample(l, sr); r = Resample(r, sr); }
            if (Current != s) { Loading = false; return; }   // another sound was picked while this one loaded
            lock (lk) { if (playing && pl != null) { ol = pl; orr = pr; opos = pos; oenv = env; } pl = l; pr = r; srate = 44100; pos = 0; env = 0; atk = 1f / 66; envTarget = 1; resetOnStop = false; playing = true; }   // the sound that was playing fades out under the new one
            Loading = false; Ensure();
        }) { IsBackground = true }.Start();
    }
    static float[] Resample(float[] x, int from) { return HqResample.Run(x, from, 44100); }   // windowed sinc: a straight line between samples made pack sounds gritty
    static void Ensure()
    {
        lock (lk)
        {
            if (player != null || StudioEnv.UnderTest) return;   // (a test never plays out of the speakers)
            try { player = AudioOut.Create(Render, 2); player.Start(); }   /* always the biggest buffer (8 x 2048): with the low-latency setting (4 x 512) a busy screen starved the preview and every sound crackled */ catch (Exception ex) { player = null; Error = "No sound output: " + ex.Message; playing = false; }
        }
    }
    static void Render(float[] l, float[] r, int n)
    {
        float g = SoundLibrary.AuditionVolume; var al = pl; var ar = pr; bool loop = SoundLibrary.LoopPreview;
        for (int i = 0; i < n; i++)
        {
            float vl = 0, vr = 0;
            if (playing && al != null && loop && al.Length > LoopX * 8 && pos >= al.Length - LoopX && ol == null) { ol = al; orr = ar; opos = pos; oenv = env; pos = 0; env = 0; atk = Rel; }   // the loop point crossfades instead of jumping (started before the old sound is mixed, so no sample is missed)
            var oa = ol; if (oa != null) { if (opos < oa.Length && oenv > 0) { vl += oa[opos] * oenv; vr += orr[opos] * oenv; opos++; oenv -= Rel; } else ol = null; }
            if (playing && al != null)
            {
                int len = al.Length;
                if (pos >= len) { if (loop) { pos = 0; } else { playing = false; stoppedAt = DateTime.Now; env = 0; } }
                if (playing)
                {
                    if (env < envTarget) env = Math.Min(envTarget, env + atk); else if (env > envTarget) env = Math.Max(envTarget, env - Rel);
                    float tail = loop ? 1f : Math.Min(1f, (len - pos) / 64f);   // the last 1.5 ms of a sound always ends at silence
                    float k = env * tail; vl += al[pos] * k; vr += ar[pos] * k; pos++;
                    if (env <= 0 && envTarget <= 0) { playing = false; stoppedAt = DateTime.Now; if (resetOnStop) { pos = 0; resetOnStop = false; } }
                }
            }
            l[i] = vl * g; r[i] = vr * g;
        }
    }
    public static void Pause() { envTarget = 0; if (player == null) { playing = false; } stoppedAt = DateTime.Now; }
    public static void Resume() { if (pl != null) { if (pos >= pl.Length) pos = 0; env = 0; atk = 1f / 66; envTarget = 1; resetOnStop = false; playing = true; Ensure(); } }
    public static void Toggle() { if (playing && envTarget > 0) Pause(); else Resume(); }
    public static void Stop() { if (playing && player != null) { envTarget = 0; resetOnStop = true; } else { playing = false; pos = 0; } stoppedAt = DateTime.Now; }
    public static void Seek(double frac) { if (pl != null) lock (lk) { if (playing) { ol = pl; orr = pr; opos = pos; oenv = env; env = 0; atk = Rel; } pos = (int)Math.Max(0, Math.Min(pl.Length - 1, frac * pl.Length)); } }
    // closes the output a few seconds after the last sound so nothing keeps the sound card busy
    public static void Tick() { if (!playing && player != null && (DateTime.Now - stoppedAt).TotalSeconds > 4) { lock (lk) { try { player.Dispose(); } catch { } player = null; } } }
    public static void Shutdown() { playing = false; lock (lk) { if (player != null) { try { player.Dispose(); } catch { } player = null; } } }
}
