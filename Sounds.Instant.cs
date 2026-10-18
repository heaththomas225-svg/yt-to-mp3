using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// =====================================================================================================
//  THE INSTANT LIBRARY: every pack in the catalog lists 120 drum loops and 120 bass loops straight away, without installing it,
//  so every genre, style, tempo and key in the sound browser has hundreds of loops to pick from. A loop is made the first time it
//  is played or dragged (a fraction of a second, on this PC, with the same sound design as the packs) and kept in sounds\_instant,
//  so the next time it is there at once. Each loop is always the same sound: its name stands for a fixed recipe.
// =====================================================================================================
static class InstantLib
{
    public const int DrumPer = 120, BassPer = 120;
    static List<SoundItem> items; static string builtFor = ""; static readonly object lk = new object();
    static readonly Dictionary<string, byte[]> peaks = new Dictionary<string, byte[]>();
    public static string Dir { get { return Path.Combine(SoundLibrary.Root, "_instant" + PackBuilder.SoundVersion); } }   // one folder per sound version: loops made by older sound design are made again
    static string PeaksFile { get { return Path.Combine(Dir, "peaks.txt"); } }

    // the list of every instant loop (made once per data folder; it is only names and numbers until a loop is played)
    public static List<SoundItem> Items
    {
        get
        {
            lock (lk)
            {
                if (items != null && builtFor == SoundLibrary.Root) return items;
                builtFor = SoundLibrary.Root; peaks.Clear(); ReadPeaks(); try { foreach (var d in Directory.GetDirectories(SoundLibrary.Root, "_instant*")) if (!string.Equals(Path.GetFileName(d), Path.GetFileName(Dir), StringComparison.OrdinalIgnoreCase)) Directory.Delete(d, true); } catch { } var l = new List<SoundItem>(); var inGenre = new Dictionary<string, int>(); foreach (var rec in SoundCatalog.Recipes) { int c; inGenre.TryGetValue(rec.Genre, out c); inGenre[rec.Genre] = c + 1; }
                foreach (var rec in SoundCatalog.Recipes)
                {
                    int per = inGenre[rec.Genre] == 1 ? 2 : 1;   // a genre with one pack gets twice as many, so every genre has hundreds
                    bool drums = rec.Has("drums") || rec.Has("loops"), bass = (rec.Has("bass") || rec.Has("loops")) && rec.Flavours != null && rec.Flavours.Length > 0 && rec.Flavours[0].Length > 0;
                    if (drums) for (int i = 0; i < DrumPer * per; i++) l.Add(DrumItem(rec, i));
                    if (bass) for (int i = 0; i < BassPer * per; i++) l.Add(BassItem(rec, i));
                }
                items = l; return items;
            }
        }
    }
    public static SoundItem Find(string id) { foreach (var s in Items) if (s.Id == id) return s; return null; }

    static double Bpm(Recipe r, int i) { return r.Bpms[i % r.Bpms.Length]; }
    static int Bars(int i) { return (i / 3) % 3 == 2 ? 4 : 2; }
    static string Tg(Recipe r, string extra) { return (r.Genre + "," + r.Sub + "," + extra + "," + r.Tags).ToLowerInvariant(); }
    static SoundItem Make(Recipe r, string file, string name, string type, string group, double bpm, string key, double beats, string tags, string spec)
    {
        string rel = "instant/" + file; var s = new SoundItem { PackId = r.Id, PackName = r.Name, Rel = rel, Id = r.Id + "/" + rel, Name = r.Name + " - " + name, Type = type, Group = group, Kind = "loop", Genre = r.Genre, Sub = r.Sub, Tags = tags + ",instant", Mood = r.Mood, Key = key, Bpm = bpm, Dur = beats * 60.0 / bpm, Rate = 44100, Format = "WAV 24-bit", Spec = spec };
        s.Path = Path.Combine(Dir, r.Id, file); byte[] pk; if (peaks.TryGetValue(s.Id, out pk)) s.Peaks = pk; return s;
    }
    static SoundItem DrumItem(Recipe r, int i)
    {
        double bpm = Bpm(r, i); int bars = Bars(i); string b = bpm.ToString("0"); string feel = Feel(i);
        return Make(r, "drum-" + (i + 1).ToString("000") + ".wav", "Drum Loop " + (i + 1).ToString("000") + " " + b + " BPM" + (feel.Length > 0 ? " (" + feel + ")" : ""), "Drum Loop", "Loops", bpm, "", bars * 4, Tg(r, "drum loop,drums,loop,beat," + b + "bpm," + feel), "d|" + r.Id + "|" + i);
    }
    static string Feel(int i) { switch (i % 8) { case 1: return "busy hats"; case 2: return "sparse"; case 3: return "with fill"; case 5: return "rolls"; case 6: return "percussion"; case 7: return "ghost notes"; default: return ""; } }
    static SoundItem BassItem(Recipe r, int i)
    {
        int nf = r.Flavours.Length; string fl = r.Flavours[i % nf]; string key = r.Keys[(i / nf) % r.Keys.Length]; double bpm = Bpm(r, i / 2); string kn = KeyName(key); string b = bpm.ToString("0");
        return Make(r, "bass-" + (i + 1).ToString("000") + ".wav", Cap(fl) + " Bass Loop " + (i + 1).ToString("000") + " " + b + " BPM " + kn, "Bass Loop", "Bass", bpm, kn, 8, Tg(r, fl + ",bass loop,bass,loop," + kn.ToLowerInvariant() + "," + b + "bpm"), "b|" + r.Id + "|" + i);
    }
    static string Cap(string s) { return s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1); }
    static string KeyName(string key) { int rt; bool m; SoundLibrary.ParseKey(key, out rt, out m); return SoundLibrary.KeyName(rt, m); }

    // ---- making a loop ----
    static readonly Dictionary<string, DrumKit.Kit> kits = new Dictionary<string, DrumKit.Kit>(); static readonly object makeLock = new object();
    // makes the file for an instant loop if it is not there yet; returns null if it worked, or a message
    public static string Ensure(SoundItem s)
    {
        if (s == null || string.IsNullOrEmpty(s.Spec)) return null; if (File.Exists(s.Path)) return null;
        lock (makeLock) try   // one at a time: the preview and a drop can ask for the same loop at once
        {
            if (File.Exists(s.Path)) return null;
            var f = s.Spec.Split('|'); var rec = SoundCatalog.Find(f[1]); int i = int.Parse(f[2]); if (rec == null) return "This loop's pack is no longer in the library.";
            Buf b = f[0] == "d" ? RenderDrum(rec, i) : RenderBass(rec, i);
            b = PackWriter.Finish(b, s.Type, "loop", f[0] == "d" ? -1.5 : -1.5);
            Directory.CreateDirectory(Path.GetDirectoryName(s.Path)); string tmp = s.Path + ".tmp"; SoundIO.WriteWav(tmp, b.L, b.R, SoundGen.SR, 24); if (File.Exists(s.Path)) File.Delete(s.Path); File.Move(tmp, s.Path);
            s.Peaks = SoundIO.Peaks(b.L, b.R, 96); s.Dur = b.Length / (double)SoundGen.SR;
            lock (lk) { peaks[s.Id] = s.Peaks; try { File.AppendAllText(PeaksFile, s.Id + "|" + Convert.ToBase64String(s.Peaks) + "\n", Encoding.UTF8); } catch { } }
            return null;
        }
        catch (Exception ex) { return "Couldn't make this loop: " + ex.Message; }
    }
    static void ReadPeaks()
    {
        try { if (!File.Exists(PeaksFile)) return; foreach (var line in File.ReadAllLines(PeaksFile, Encoding.UTF8)) { int k = line.IndexOf('|'); if (k <= 0) continue; try { peaks[line.Substring(0, k)] = Convert.FromBase64String(line.Substring(k + 1)); } catch { } } } catch { }
    }

    public static Buf RenderDrum(Recipe rec, int i)
    {
        var st = PackBuilder.StyleFor(rec); int kv = i % 6; DrumKit.Kit kit; string kk = rec.Id + "/" + kv;
        lock (kits) { if (!kits.TryGetValue(kk, out kit)) { if (kits.Count > 24) kits.Clear(); kit = DrumKit.Make(st, rec.Seed + 1 + kv * 977); kits[kk] = kit; } }
        var r = new Rnd(rec.Seed * 7 + i * 131 + 5); var pat = Vary(st.Pat, r, i % 8); double sw = st.Swing + (r.Next() - 0.5) * 0.03;
        var lp = DrumKit.Render(kit, st, pat, Bpm(rec, i), Bars(i), sw, rec.Seed + 900 + i * 13, i % 8 == 3);
        return lp.Mix;
    }
    // a groove from the pack's own pattern: the backbeat stays, the kicks, hats, ghosts and percussion move, and bar two answers bar one
    public static Pat Vary(Pat p, Rnd r, int feel)
    {
        int n = 16 * p.Bars; Func<string, string> norm = s => { s = s ?? ""; if (s.Length == 0) return ""; var sb = new StringBuilder(); for (int k = 0; k < n; k++) sb.Append(s[k % s.Length]); return sb.ToString(); };
        var o = new Pat { K = norm(p.K), S = norm(p.S), C = norm(p.C), H = norm(p.H), O = norm(p.O), P = norm(p.P), G = norm(p.G), Bars = p.Bars };
        Func<string, int, char> at = (s, k) => s.Length == 0 ? '.' : s[k];
        Func<int, bool> backbeat = k => at(o.S, k) != '.' || at(o.C, k) != '.';
        var K = (o.K.Length == 0 ? new string('.', n) : o.K).ToCharArray(); var H = (o.H.Length == 0 ? new string('.', n) : o.H).ToCharArray(); var O = (o.O.Length == 0 ? new string('.', n) : o.O).ToCharArray(); var G = (o.G.Length == 0 ? new string('.', n) : o.G).ToCharArray(); var P = (o.P.Length == 0 ? new string('.', n) : o.P).ToCharArray();
        bool fourFloor = o.K.StartsWith("x...x...x...x..."); bool hasRoll = o.H.IndexOf('t') >= 0;
        // kicks: one or two moved or added (never on the snare, and a four-to-the-floor keeps its four)
        int moves = 1 + r.Int(2); for (int m = 0; m < moves; m++) { int k = r.Int(n); if (k % 4 == 0 && fourFloor) continue; if (backbeat(k)) continue; if (K[k] == '.') K[k] = 'x'; else if (k % 8 != 0 && !fourFloor) K[k] = '.'; }
        // hats by feel
        if (feel == 1) { for (int k = 0; k < n; k++) if (H[k] == '.' && r.Next() < 0.5f) H[k] = 'x'; }
        else if (feel == 2) { for (int k = 0; k < n; k++) if (H[k] != '.' && k % 4 != 0 && r.Next() < 0.45f) H[k] = '.'; for (int k = 0; k < n; k++) if (P[k] != '.' && r.Next() < 0.5f) P[k] = '.'; }
        else if (feel == 5) { int rolls = 1 + r.Int(3); for (int m = 0; m < rolls; m++) { int k = r.Int(n); if (!hasRoll && r.Next() < 0.5f) k = k - k % 2 + 1; H[k] = 't'; } }
        else if (feel == 6) { for (int k = 0; k < n; k++) if (P[k] == '.' && !backbeat(k) && r.Next() < 0.28f) P[k] = 'x'; }
        else if (feel == 7) { for (int k = 0; k < n; k++) if (G[k] == '.' && !backbeat(k) && K[k] == '.' && r.Next() < 0.22f) G[k] = 'g'; }
        else { for (int k = 0; k < n; k++) if (r.Next() < 0.08f && k % 4 != 0) H[k] = H[k] == '.' ? 'x' : '.'; }
        // an open hat moved to another off-beat now and then
        if (r.Next() < 0.5f) { for (int k = 0; k < n; k++) if (O[k] != '.' && r.Next() < 0.5f) O[k] = '.'; int k2 = (r.Int(n / 2) * 2 + 2) % n; if (!backbeat(k2)) O[k2] = 'x'; }
        o.K = new string(K); o.H = new string(H); o.O = new string(O); o.G = new string(G); o.P = new string(P);
        if (o.Bars == 1)
        {
            // bar two: the same groove with one kick and one hat changed near its end, so the loop answers itself
            var K2 = o.K.ToCharArray(); var H2 = o.H.ToCharArray(); int a = 10 + r.Int(6); if (!backbeat(a) && !(fourFloor && a % 4 == 0)) K2[a] = K2[a] == '.' ? 'x' : '.'; int h2 = 12 + r.Int(4); H2[h2] = H2[h2] == '.' ? 'x' : (hasRoll ? 't' : H2[h2]);
            o = new Pat { K = o.K + new string(K2), S = o.S + o.S, C = o.C + o.C, H = o.H + new string(H2), O = o.O + o.O, P = o.P + o.P, G = o.G + o.G, Bars = 2 };
        }
        return o;
    }

    public static Buf RenderBass(Recipe rec, int i)
    {
        int nf = rec.Flavours.Length; string fl = rec.Flavours[i % nf]; string key = rec.Keys[(i / nf) % rec.Keys.Length]; double bpm = Bpm(rec, i / 2);
        var r = new Rnd(rec.Seed * 13 + i * 197 + 3); int root = 36 + Music.Root(key); bool minor = Music.IsMinor(key);
        var notes = Music.BassLine(r, Music.RhythmFor(rec.Drums), 2, root, minor); var patch = Music.BassPatch(fl, r);
        return PackBuilder.LoopLen(SoundGen.RenderSynth(patch, notes, bpm, 8, 0.3), 8, bpm);
    }
}
