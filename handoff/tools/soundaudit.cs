using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

// The library audit. Build with -main:SA (see handoff/tools/README.md); runs on Windows or under mono. Never touches the real data folder.
//   soundaudit.exe grooves                    every hand-written groove line is 32 steps
//   soundaudit.exe bass [packId|section]      every instant bass of the packs: renders it and runs BassDesign.Check (LFO'd bassline packs must pass)
//   soundaudit.exe drums [packId]             the percussion each genre uses, and the notes per bar of its loops
//   soundaudit.exe empty [all|instant|installed]   every filter option (genre, style, pack, section, toolkit, tempo range) gives at least one sound
//   soundaudit.exe catalog                    pack counts, bassline pack list, archetypes per pack, instant item counts per code
static class SA
{
    static string Tmp()
    {
        string t = Path.Combine(Path.GetTempPath(), "purphex_audit"); try { if (Directory.Exists(t)) Directory.Delete(t, true); } catch { } Directory.CreateDirectory(t); return t;
    }
    static void Main(string[] a)
    {
        string mode = a.Length > 0 ? a[0] : "catalog"; string arg = a.Length > 1 ? a[1] : ""; StudioEnv.DataDir = mode == "audit" ? arg : Tmp(); SoundLibrary.Load();
        switch (mode)
        {
            case "grooves": { string c = Grooves.Check(); Console.WriteLine(c.Length == 0 ? "grooves OK" : "GROOVE ERRORS: " + c); Environment.Exit(c.Length == 0 ? 0 : 1); break; }
            case "bass": Bass(arg); break;
            case "drums": Drums(arg); break;
            case "control": Control(); break;
            case "exp4": {
                // how many of the library's hats / open hats / rides / crashes are distinct (greedy, in pack order, the baker's rule)
                foreach (var what in new[] { "HatsC", "HatsO", "Rides" }) { var kept = new List<KeyValuePair<double, float[]>>(); int total = 0, dropped = 0;
                    foreach (var rec in SoundCatalog.Recipes.OrderBy(r => r.Id)) { if (!(rec.Has("drums") || rec.Has("loops"))) continue; for (int kv = 0; kv < 2; kv++) { var kit = DrumKit.Make(InstantLib.KitStyle(rec, kv), rec.Seed + 1 + kv * 977); var list = what == "HatsC" ? kit.HatsC : what == "HatsO" ? kit.HatsO : kit.Rides; for (int j = 0; j < list.Count; j++) { total++; var b = list[j]; var fp = LibraryBaker.Fingerprint(b, false); double d = b.Length / 44100.0; bool dup = false; foreach (var k in kept) if (Math.Abs(k.Key - d) <= Math.Max(0.05, d * 0.02) && LibraryBaker.Similarity(k.Value, fp) >= LibraryBaker.CrossLimit) { dup = true; break; } if (dup) dropped++; else kept.Add(new KeyValuePair<double, float[]>(d, fp)); } } }
                    Console.WriteLine(what + ": " + total + " candidates, " + kept.Count + " distinct, " + dropped + " too alike"); }
                break; }
            case "audit": Audit(); break;
            case "exp3": { foreach (var id in arg.Split(',')) { var st = PackBuilder.StyleFor(SoundCatalog.Find(id)); var p = Grooves.Vary(st, 0, 0); Console.WriteLine(id + " variant " + st.Variant + " pattern " + st.Pattern + " K " + p.K + " P " + p.P + " H " + p.H); } break; }
            case "exp2": {
                Func<double, double, double, Buf> H = (dec, br, pit) => SoundGen.Hat(new Rnd(5), dec, br, 0.2, pit);
                var baseH = H(0.045, 10, 1.0); var baseF = LibraryBaker.Fingerprint(baseH, false);
                foreach (var t in new[] { new[] { 0.045, 10, 1.0 }, new[] { 0.032, 10, 1.0 }, new[] { 0.064, 10, 1.0 }, new[] { 0.045, 7.5, 1.0 }, new[] { 0.045, 13, 1.0 }, new[] { 0.045, 10, 0.8 }, new[] { 0.045, 10, 1.25 }, new[] { 0.06, 12, 1.1 } }) { var h = H(t[0], t[1], t[2]); Console.WriteLine("decay " + t[0] + " bright " + t[1] + " pitch " + t[2] + "  sim to base " + LibraryBaker.Similarity(baseF, LibraryBaker.Fingerprint(h, false)).ToString("0.0000") + "  len " + (h.Length / 44100.0).ToString("0.000")); }
                break; }
            case "bake": {
                foreach (var id in arg.Split(',')) { var rec = SoundCatalog.Find(id); var r = LibraryBaker.BakePack(rec, null, null); SoundLibrary.AddOrReplace(r.Pack); Console.WriteLine(id + ": " + r.Made + " made, " + r.Failed + " left out (" + r.Duplicates + " exact, " + r.Similar + " alike)");
                    var codes = new SortedSet<string>(r.MadeBy.Keys); foreach (var k in r.FailedBy.Keys) codes.Add(k); if (Environment.GetEnvironmentVariable("PURPHEX_FAILS") != null) foreach (var f in r.Failures) Console.WriteLine("      " + f); Console.WriteLine("   " + string.Join(" ", codes.Select(c => { int m, f; r.MadeBy.TryGetValue(c, out m); r.FailedBy.TryGetValue(c, out f); return f > 0 ? c + "=" + m + "(-" + f + ")" : c + "=" + m; }))); }
                break; }
            case "exp": {
                var variants = new Dictionary<string, Action<WobblePatch>> { { "base", q => { } }, { "fold0", q => q.Fold = 0 }, { "drive.2", q => q.Drive = 0.2 }, { "fm0", q => q.Fm = 0 }, { "rate8", q => q.Rates = "8" }, { "shape0", q => q.Shape = 0 }, { "retrigOff", q => q.Retrig = false }, { "res.8", q => q.Res = 0.8 }, { "depth5", q => q.Depth = 5 } };
                foreach (var fl in new[] { "growl", "neuro", "fm", "metallic" }) foreach (var v in variants) { double sum = 0; int n = 0; for (int sd = 0; sd < 6; sd++) { var p = BassDesign.Patch(fl, new Rnd(70 + sd)); v.Value(p); var notes = BassDesign.Phrase(new Rnd(900 + sd), BassDesign.StyleOf(fl), 2, 41, true, 0.58); var b = PackBuilder.LoopLen(BassDesign.Render(p, notes, 138, 8, 0.3), 8, 138); var m = BassDesign.Check(b, notes, 138, p); sum += m.Motion; n++; } Console.WriteLine(fl.PadRight(9) + v.Key.PadRight(10) + " motion " + (sum / n).ToString("0.0")); }
                break; }
            case "one": { var rec = SoundCatalog.Find(arg); string code = a[2]; int idx = int.Parse(a[3]); var it = InstantLib.ItemsOf(rec.Id).First(x => LibraryBaker.CodeOf(x) == code && int.Parse(x.Spec.Split('|')[2]) == idx); string err; var b = InstantLib.MakeBuf(it, out err); Console.WriteLine(it.Name + " " + (b == null ? err : b.Length + " samples")); break; }
            case "empty": { var bad = LibraryCheck.EmptyOptions(arg.Length > 0 ? (arg == "installed" ? "" : arg) : "all"); Console.WriteLine(bad.Count == 0 ? "no empty filter option" : bad.Count + " EMPTY OPTIONS:"); foreach (var b in bad) Console.WriteLine("  " + b); Environment.Exit(bad.Count == 0 ? 0 : 1); break; }
            default: Catalog(); break;
        }
    }
    static List<Recipe> Pick(string arg)
    {
        if (arg.Length == 0) return SoundCatalog.Recipes.ToList(); var l = SoundCatalog.Recipes.Where(r => r.Id == arg || r.Genre == arg || r.Section == arg).ToList(); return l;
    }
    static void Catalog()
    {
        Console.WriteLine("packs: " + SoundCatalog.Recipes.Count); foreach (var g in SoundCatalog.Recipes.GroupBy(r => r.Genre)) Console.WriteLine("  " + g.Key + ": " + g.Count());
        Console.WriteLine("UK Bassline packs:"); foreach (var r in SoundCatalog.Recipes.Where(x => x.Genre == "UK Bassline")) Console.WriteLine("  " + r.Id + " | " + r.Name + " | " + r.Drums + " | " + string.Join(",", r.Flavours.Select(f => BassDesign.FlavourFor(r, f)).Distinct()));
        var codes = new Dictionary<string, int>(); int total = 0; foreach (var r in SoundCatalog.Recipes) foreach (var s in InstantLib.ItemsOf(r.Id)) { string c = LibraryBaker.CodeOf(s); int n; codes.TryGetValue(c, out n); codes[c] = n + 1; total++; }
        Console.WriteLine("instant items: " + total + "; per pack: " + (total / SoundCatalog.Recipes.Count)); foreach (var kv in codes.OrderBy(k => k.Key)) Console.Write(kv.Key + "=" + kv.Value + " "); Console.WriteLine();
        int plan = 0; foreach (var r in SoundCatalog.Recipes) plan += LibraryBaker.Plan(r).Count; Console.WriteLine("planned (baked) sounds: " + plan);
    }
    static void Bass(string arg)
    {
        var sw2 = new Dictionary<string, List<double>>(); var rg2 = new Dictionary<string, List<double>>(); var recs = Pick(arg); int bad = 0, n = 0; var arche = new Dictionary<string, int>(); var rates = new Dictionary<string, int>(); var sw = System.Diagnostics.Stopwatch.StartNew();
        foreach (var rec in recs)
        {
            if (rec.Flavours == null || rec.Flavours.Length == 0 || rec.Flavours[0].Length == 0) continue; bool lfo = rec.Genre == "UK Bassline"; int pb = 0, pn = 0;
            foreach (var s in InstantLib.ItemsOf(rec.Id))
            {
                string code = LibraryBaker.CodeOf(s); if (code != "b" && code != "ob") continue; if (code == "ob" && !lfo) continue; if (code == "b" && !lfo && s.BaseBpm > 0 && int.Parse(s.Spec.Split('|')[2]) >= 14) continue;   // (outside bassline: the 14 baked loops)
                if (lfo && code == "b" && int.Parse(s.Spec.Split('|')[2]) >= 36) continue;   // (36 of the 120 loops of each bassline pack)
                string err; var b = InstantLib.MakeBuf(s, out err); n++; pn++;
                if (b == null) { bad++; pb++; Console.WriteLine("  MAKE FAIL " + s.Name + ": " + err); continue; }
                var m = InstantLib.LastNotes != null ? BassDesign.Check(b, InstantLib.LastNotes, InstantLib.LastBpm, InstantLib.LastPatch) : BassDesign.Check(b); if (lfo) { string ak = s.Tags.Split(',')[0]; List<double> sl; if (!sw2.TryGetValue(ak, out sl)) sw2[ak] = sl = new List<double>(); sl.Add(m.Motion >= 0 ? m.Motion : m.Lsd + 100); List<double> rl; if (!rg2.TryGetValue(ak, out rl)) rg2[ak] = rl = new List<double>(); rl.Add(m.Tilt); } string tg = s.Tags.Split(',')[0]; int c0; arche.TryGetValue(tg, out c0); arche[tg] = c0 + 1; string rate = m.Rate > 0 ? Math.Round(m.Rate).ToString() : "-"; int r0; rates.TryGetValue(rate, out r0); rates[rate] = r0 + 1;
                if (lfo && !m.Ok) { bad++; pb++; if (pb <= 4) Console.WriteLine("  NOT LFO'd: " + s.Name + " -> " + m.Why + " (motion " + m.Motion.ToString("0.0") + " dB, mid " + (m.Mid * 100).ToString("0") + "%)"); }
            }
            Console.WriteLine(rec.Id + ": " + pn + " basses, " + pb + " bad (" + sw.Elapsed.TotalSeconds.ToString("0") + " s)");
        }
        Console.WriteLine("bass sounds " + n + ", bad " + bad); Console.WriteLine("archetypes: " + string.Join(" ", arche.OrderBy(k => k.Key).Select(k => k.Key + "=" + k.Value))); Console.WriteLine("measured LFO rates (Hz): " + string.Join(" ", rates.OrderBy(k => k.Key).Select(k => k.Key + "=" + k.Value)));
        foreach (var kv in sw2.OrderBy(k => k.Key)) { var l1 = kv.Value.OrderBy(x => x).ToList(); var l2 = rg2[kv.Key].OrderBy(x => x).ToList(); Console.WriteLine("  " + kv.Key.PadRight(13) + " n " + l1.Count.ToString().PadLeft(3) + "  motion dB min/med " + l1[0].ToString("0.0") + "/" + l1[l1.Count / 2].ToString("0.0") + "  colour tilt min/med " + l2[0].ToString("0.0") + "/" + l2[l2.Count / 2].ToString("0.0") + " dB"); }
        Environment.Exit(bad == 0 ? 0 : 1);
    }
    // control: the same bass with every modulation switched off (a held filter): this is what "not LFO'd" measures like
    static void Control()
    {
        var swing = new List<double>(); var tilt = new List<double>(); var rec = SoundCatalog.Find("filthy-wobblers"); int k = 0;
        foreach (var fl in new[] { "wobble", "filthy", "rubber", "elastic", "growl", "talking", "fm", "square", "reese", "neuro", "metallic", "riddim", "speedgarage", "subwob", "organwob", "stabwob", "warped", "classic", "laser", "experimental" })
            for (int v = 0; v < 6; v++)
            {
                var r = new Rnd(500 + k++); var p = BassDesign.Patch(fl, r); p.Depth = 0; p.PwMod = 0; p.FmMod = 0; p.Fold = 0; p.EnvAmt = 0; p.Pump = 0; p.Retrig = false;
                var notes = BassDesign.Phrase(new Rnd(900 + k), BassDesign.StyleOf(fl), 2, 41, true, 0.58); var b = PackBuilder.LoopLen(BassDesign.Render(p, notes, 138, 8, 0.3), 8, 138); var m = BassDesign.Check(b, notes, 138, p); swing.Add(m.Motion >= 0 ? m.Motion : m.Lsd + 100); tilt.Add(m.Swing);
            }
        swing.Sort(); tilt.Sort(); Console.WriteLine("STATIC controls (" + swing.Count + "): MOTION dB p50/p90/max " + swing[swing.Count / 2].ToString("0.0") + "/" + swing[(int)(swing.Count * 0.9)].ToString("0.0") + "/" + swing[swing.Count - 1].ToString("0.0") + "; old swing dB p50/p90/max " + tilt[tilt.Count / 2].ToString("0.0") + "/" + tilt[(int)(tilt.Count * 0.9)].ToString("0.0") + "/" + tilt[tilt.Count - 1].ToString("0.0"));
    }
    // the baked library on disk: real counts, copies across packs, empty filters
    static void Audit()
    {
        var packs = SoundLibrary.Packs.Where(p => p.Id != SoundLibrary.UserPackId && p.Items.Count > 0).ToList(); var all = packs.SelectMany(p => p.Items).ToList(); long bytes = 0; foreach (var s in all) { try { bytes += new FileInfo(s.Path).Length; } catch { } }
        Console.WriteLine("packs baked: " + packs.Count + " of " + SoundCatalog.Recipes.Count + "; sounds: " + all.Count + " (audio " + all.Count(s => s.IsAudio) + ", MIDI " + all.Count(s => s.Kind == "midi") + ", presets " + all.Count(s => s.Kind == "preset") + "); size " + (bytes / 1073741824.0).ToString("0.0") + " GB");
        Console.WriteLine("per pack: min " + packs.Min(p => p.Items.Count) + ", median " + packs.Select(p => p.Items.Count).OrderBy(x => x).ElementAt(packs.Count / 2) + ", max " + packs.Max(p => p.Items.Count));
        foreach (var g in all.GroupBy(s => SoundCatalog.SectionOf(s.Genre)).OrderByDescending(g => g.Count())) Console.WriteLine("  " + g.Key.PadRight(18) + g.Count());
        var dupHash = all.Where(s => s.Hash.Length > 0).GroupBy(s => s.Hash).Count(g => g.Count() > 1); Console.WriteLine("exact copies (same hash): " + dupHash);
        FpIndex.Refresh("none"); string ex; int cross = FpIndex.CountCrossCopies(LibraryBaker.CrossLimit, out ex); Console.WriteLine("fingerprints read: " + FpIndex.Count + "; sounds with a near-copy in another pack: " + cross + " " + ex);
        var bad = LibraryCheck.EmptyOptions(""); Console.WriteLine(bad.Count == 0 ? "no empty filter option in the installed library" : bad.Count + " EMPTY OPTIONS: " + string.Join("; ", bad.Take(20)));
        var types = all.GroupBy(s => s.Type).OrderByDescending(g => g.Count()).Take(40); Console.WriteLine("types: " + string.Join(", ", types.Select(g => g.Key + " " + g.Count())));
        var weak = packs.Where(p => p.Items.Count < 190).Select(p => p.Id + "=" + p.Items.Count).ToList(); Console.WriteLine("packs under 190 sounds: " + weak.Count + " " + string.Join(" ", weak.Take(30)));
    }
    static void Drums(string arg)
    {
        var recs = Pick(arg); int bad = 0;
        foreach (var g in recs.GroupBy(r => r.Drums))
        {
            var kinds = Grooves.PercKinds(g.Key); var pr = new HashSet<string>(); int packs = 0;
            foreach (var rec in g) { packs++; foreach (var s in InstantLib.ItemsOf(rec.Id)) if (LibraryBaker.CodeOf(s) == "op") pr.Add(s.Core.Split(' ')[1] + (s.Core.Split(' ')[1] == "Tom" ? " " + s.Core.Split(' ')[2] : "")); }
            bool ok = pr.All(x => kinds.Contains(x)); if (!ok) bad++;
            var st = PackBuilder.StyleFor(g.First()); var p = st.Pat;
            Console.WriteLine((ok ? "ok   " : "BAD  ") + g.Key.PadRight(14) + " packs " + packs.ToString().PadLeft(2) + "  perc " + string.Join("/", kinds) + "  swing " + st.Swing.ToString("0.00") + "  one-shot percs: " + string.Join(",", pr));
        }
        Environment.Exit(bad == 0 ? 0 : 1);
    }
}
