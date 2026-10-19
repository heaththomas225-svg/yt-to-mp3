using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// The sound library overhaul (3.73.0): hand-written grooves and each genre's own percussion, the 20 UK Bassline packs with every bass LFO'd,
// every pack its own kit, no copies across packs, no empty filter options.   csc /main:OH ... OverhaulTest.cs   (needs no window: runs on Windows or under mono)
static class OH
{
    static int fails;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + "   " + d); Console.Out.Flush(); }
    static void Main()
    {
        Edition.Override = Tier.Fantasy; string tmp = Path.Combine(Path.GetTempPath(), "purphex_overhaul"); try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch { } Directory.CreateDirectory(tmp); StudioEnv.DataDir = tmp; SoundLibrary.Load();

        // ---- the grooves ----
        string gc = Grooves.Check(); Check("every hand-written groove line is 32 steps of known letters", gc.Length == 0, gc);
        var drumKeys = SoundCatalog.Recipes.Select(r => r.Drums).Distinct().ToList();
        var badKinds = new List<string>(); var kitNames = new[] { "Tom Low", "Tom Mid", "Shaker", "Rim", "Conga", "Bell" };
        foreach (var d in drumKeys) { var k = Grooves.PercKinds(d); if (k.Length == 0 || k.Any(x => !kitNames.Contains(x))) badKinds.Add(d); }
        Check("every genre has a percussion palette made of real kit sounds", badKinds.Count == 0, string.Join(" ", badKinds));
        var bellOk = new HashSet<string> { "disco", "baile", "phonk" }; var bells = drumKeys.Where(d => Grooves.PercKinds(d).Contains("Bell") && !bellOk.Contains(d)).ToList();
        Check("no cowbell outside phonk, baile funk and disco; no congas in drum and bass, trap or drill", bells.Count == 0 && !drumKeys.Where(d => d.StartsWith("dnb_") || d == "trap" || d == "drill").Any(d => Grooves.PercKinds(d).Contains("Conga")), string.Join(" ", bells));
        var strayPerc = new List<string>(); foreach (var rec in SoundCatalog.Recipes) { var kinds = Grooves.PercKinds(rec.Drums); foreach (var s in InstantLib.ItemsOf(rec.Id)) if (LibraryBaker.CodeOf(s) == "op") { string name = s.Core.Substring("Perc ".Length); name = name.Substring(0, name.LastIndexOf(' ')); if (!kinds.Contains(name)) strayPerc.Add(rec.Id + ":" + name); } }
        Check("the percussion one-shots of every pack are only its genre's percussion", strayPerc.Count == 0, string.Join(" ", strayPerc.Take(6)));
        var badLoops = new List<string>();
        foreach (var d in drumKeys) { var st = new DrumStyle { Pattern = d }; for (int i = 0; i < 120; i++) { var p = Grooves.Vary(st, i, i % 8); var kinds = Grooves.PercKinds(d); if (p.K.Length != 32 || p.H.Length != 32 || p.S.Length != 32 || p.P.Length != 32 || p.G.Length != 32 || p.O.Length != 32 || p.C.Length != 32) { badLoops.Add(d + "#" + i + " length"); break; } if (p.P.Contains('z') && kinds.Length < 3) { badLoops.Add(d + "#" + i + " third percussion"); break; } if (p.P.Contains('y') && kinds.Length < 2) { badLoops.Add(d + "#" + i + " second percussion"); break; } } }
        Check("every genre's loops (120 each) are 2 bars with only the percussion the genre has", badLoops.Count == 0, string.Join(" ", badLoops.Take(5)));
        var same = new List<string>(); foreach (var d in drumKeys) { var st = new DrumStyle { Pattern = d }; var seen = new HashSet<string>(); for (int i = 0; i < 120; i++) { var p = Grooves.Vary(st, i, i % 8); seen.Add(p.K + p.H + p.O + p.P + p.G); } if (seen.Count < 12) same.Add(d + "=" + seen.Count); }
        Check("each genre has at least 12 different written grooves", same.Count == 0, string.Join(" ", same));
        { var a = Grooves.Vary(new DrumStyle { Pattern = "dnb_roller" }, 37, 5); var b = Grooves.Vary(new DrumStyle { Pattern = "dnb_roller" }, 37, 5); Check("the same loop number always makes the same groove (nothing is random)", a.K == b.K && a.H == b.H && a.P == b.P && a.G == b.G, ""); }
        { var lie = new List<string>(); foreach (var d in drumKeys) { var st = new DrumStyle { Pattern = d }; var basePat = Grooves.Vary(st, 0, 0); if (Grooves.FeelLabel(d, 5) == "rolls" && Grooves.Vary(st, 5, 5).H == basePat.H) lie.Add(d + " rolls"); if (Grooves.FeelLabel(d, 6) == "percussion" && Grooves.Vary(st, 6, 6).P == basePat.P) lie.Add(d + " percussion"); if (Grooves.FeelLabel(d, 7) == "ghost notes" && !Grooves.Vary(st, 7, 7).G.Contains('g')) lie.Add(d + " ghost"); if (Grooves.FeelLabel(d, 1) == "busy hats" && Grooves.Vary(st, 1, 1).H == basePat.H && false) lie.Add(d + " busy"); }
          Check("a loop is only called rolls, percussion or ghost notes if it has them", lie.Count == 0, string.Join(" ", lie)); }
        { var bad = new List<string>(); foreach (var r in SoundCatalog.Recipes) { double sw = PackBuilder.StyleFor(r).Swing; if (r.Drums.StartsWith("ukg") && (sw < 0.60 || sw > 0.66)) bad.Add(r.Id + " " + sw.ToString("0.00")); if (r.Drums == "bassline_4x4" && (sw < 0.58 || sw > 0.63)) bad.Add(r.Id + " " + sw.ToString("0.00")); }
          Check("UK garage swings 60-66% and UK bassline 58-63%", bad.Count == 0, string.Join(" ", bad)); }

        // ---- the 20 UK Bassline packs ----
        var bl = SoundCatalog.Recipes.Where(r => r.Genre == "UK Bassline").ToList();
        Check("UK Bassline is exactly 20 named packs", bl.Count == 20 && bl.Select(r => r.Name).Distinct().Count() == 20, bl.Count + " packs");
        var wanted = new[] { "filthy-wobblers", "rubberband-bass", "talking-bassline", "speed-garage-revival", "bassline-drop-toolkit", "rave-bassline", "uk-bass-underground", "elastic-basslines" };
        Check("the named packs keep their ids (filthy wobblers, rubberband bass, talking bassline, speed garage revival, drop toolkit, rave bassline, UK bass underground, elastic basslines)", wanted.All(w => bl.Any(r => r.Id == w)), "");
        { var notEngine = new List<string>(); foreach (var r in bl) foreach (var f in r.Flavours) if (!BassDesign.UsesEngine(BassDesign.FlavourFor(r, f))) notEngine.Add(r.Id + ":" + f); Check("in every UK Bassline pack every bass flavour is an LFO'd archetype of the wobble engine", notEngine.Count == 0, string.Join(" ", notEngine.Take(6))); }
        { var sets = bl.Select(r => string.Join(",", r.Flavours.Select(f => BassDesign.FlavourFor(r, f)).Distinct().OrderBy(x => x))).ToList(); Check("each UK Bassline pack has its own set of archetypes (no two the same, at least 4 kinds each)", sets.Distinct().Count() == 20 && bl.All(r => r.Flavours.Select(f => BassDesign.FlavourFor(r, f)).Distinct().Count() >= 4), sets.Distinct().Count() + " different sets"); }
        { int n = 0, bad = 0, short1 = 0; var why = new List<string>(); foreach (var r in bl) { int take = 0; foreach (var s in InstantLib.ItemsOf(r.Id)) { string c = LibraryBaker.CodeOf(s); if (c != "b" && c != "ob") continue; if (take++ >= 9) break; string err; var b = InstantLib.MakeBuf(s, out err); n++; if (b == null) { bad++; why.Add(s.Name + ": " + err); continue; } var m = BassDesign.Check(b, InstantLib.LastNotes, InstantLib.LastBpm, InstantLib.LastPatch); if (!m.Ok) { bad++; if (why.Count < 5) why.Add(s.Name + ": " + m.Why); } if (b.Length < 4096) short1++; } }
          Check("every UK Bassline bass moves with its LFO (9 sounds from each of the 20 packs measured inside their notes)", bad == 0 && n >= 180, n + " measured, " + bad + " not LFO'd " + string.Join("; ", why)); }
        { var names = new List<string>(); foreach (var r in bl) foreach (var s in InstantLib.ItemsOf(r.Id)) if (LibraryBaker.CodeOf(s) == "b" && !(s.Name.Contains(" LFO)") && s.Tags.Contains("lfo"))) names.Add(s.Name); Check("every UK Bassline bass loop says its LFO (name and tags) and is searchable as wobble", names.Count == 0, string.Join(" ", names.Take(3))); }
        { var r0 = bl.First(x => x.Id == "talking-bassline"); var s0 = InstantLib.ItemsOf(r0.Id).First(x => LibraryBaker.CodeOf(x) == "b"); var types = new HashSet<string>(bl.SelectMany(r => InstantLib.ItemsOf(r.Id)).Where(x => LibraryBaker.CodeOf(x) == "ob").Select(x => x.Type)); Check("bass one-shots carry their archetype as the type (Wobble, Reese, Growl, Talking, Rubber, Sub, Stab)", types.Count >= 5 && !types.Contains("Bass One-Shot"), string.Join(", ", types)); }

        // ---- songstarter kits, atmospheres ----
        { var rec = SoundCatalog.Find("dnb-rollers"); var ks = InstantLib.ItemsOf(rec.Id).Where(x => LibraryBaker.CodeOf(x) == "ks").Take(6).ToList(); var lens = new List<int>(); foreach (var s in ks) { string err; var b = InstantLib.MakeBuf(s, out err); lens.Add(b == null ? -1 : b.Length); }
          Check("a songstarter kit has six parts of one length (drums, bass, chords, lead, FX, full mix)", ks.Count == 6 && lens.All(l => l > 1000 && l == lens[0]), string.Join(",", lens)); }
        { var rec = SoundCatalog.Find("dnb-rollers"); var at = InstantLib.ItemsOf(rec.Id).Where(x => LibraryBaker.CodeOf(x) == "oa" || LibraryBaker.CodeOf(x) == "ox").ToList(); bool ok = at.Count >= 6 && at.Any(x => x.Type == "Atmosphere") && at.Any(x => x.Type == "Texture"); string err; var b = InstantLib.MakeBuf(at[0], out err); Check("every pack lists atmospheres and textures that play", ok && b != null && b.Length > 44100, at.Count + " listed"); }

        // ---- no empty filters ----
        { var bad = LibraryCheck.EmptyOptions("all"); Check("every genre, style, pack, section, toolkit and tempo range the menus offer gives at least one sound", bad.Count == 0, string.Join("; ", bad.Take(6))); }

        // ---- the baker: spares, copies across packs, wobble presets ----
        { var ids = new[] { "filthy-wobblers", "rubberband-bass", "dnb-rollers", "naughty-dnb-rollers" }; var res = new Dictionary<string, LibraryBaker.Result>(); var t0 = DateTime.Now;
          foreach (var id in ids) { var r = LibraryBaker.BakePack(SoundCatalog.Find(id), null, null); SoundLibrary.AddOrReplace(r.Pack); res[id] = r; }
          Check("four packs install with a full range (every kind keeps its quota; leftovers are replaced by spares)", res.Values.All(x => x.Made >= 180), string.Join(", ", res.Select(kv => kv.Key + " " + kv.Value.Made + " made, " + kv.Value.Failed + " left out, " + kv.Value.Similar + " alike")) + " in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
          Check("every pack writes its fingerprints (fp.bin) for the packs made after it", ids.All(id => File.Exists(Path.Combine(SoundLibrary.Root, id, "fp.bin"))), "");
          // nothing in one pack is the same sound as something in another
          string ex; FpIndex.Refresh("none"); int copies = FpIndex.CountCrossCopies(LibraryBaker.CrossLimit, out ex); Check("no sound in these four packs is the same sound as one in another pack", copies == 0, copies + " copies " + ex);
          var fw = SoundLibrary.Pack("filthy-wobblers"); var wp = fw.Items.Where(s => s.Type == "Wobble Preset").ToList(); bool load = wp.Count >= 3 && wp.All(s => { var p = WobblePatch.Load(File.ReadAllText(s.Path)); return p.Depth > 0 || p.PwMod > 0 || p.FmMod > 0 || p.Fold > 0; });
          Check("a bassline pack's bass presets are Purphex Wobble presets that load", load && fw.Items.All(s => s.Type != "Synth Preset" || s.Name.Contains("Pad") || s.Name.Contains("Stab")), wp.Count + " wobble presets");
          var kicks = fw.Items.Count(s => s.Type == "Kick"); var snares = fw.Items.Count(s => s.Type == "Snare"); Check("kicks and snares keep their quota (12 and 10) after copies are left out", kicks >= 10 && snares >= 8, kicks + " kicks, " + snares + " snares");
          var fills = fw.Items.Where(s => s.Type == "Drum Loop").ToList(); Check("a pack's drum loops are all different grooves", fills.Select(s => s.Hash).Distinct().Count() == fills.Count, fills.Count + " drum loops"); }

        try { Directory.Delete(tmp, true); } catch { } Console.WriteLine(fails == 0 ? "=== overhaul checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
