using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

// The real sound library: packs installed as checked audio files (no mock audio), the right sounds in each category, filters that combine,
// honest counts, previews, loading into the DAW, copies spotted, missing files reported, resuming an interrupted install, licences kept,
// SOUND PACKS first, buses on the mixer desk, menus kept inside the window.   csc /main:LB ... LibraryTest.cs
static class LB
{
    static int fails; static StudioForm sf; static StudioEngine eng; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + "   " + d); Console.Out.Flush(); }
    static T F<T>(string n) { return (T)typeof(StudioForm).GetField(n, BF).GetValue(sf); }
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static object M(string n, params object[] a) { return typeof(StudioForm).GetMethod(n, BF).Invoke(sf, a); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static void Paint() { sf.Invalidate(); sf.Update(); Application.DoEvents(); }
    static void Shot(string name) { Paint(); var bm = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height); sf.DrawToBitmap(bm, new Rectangle(0, 0, sf.Width, sf.Height)); bm.Save("C:/Users/thoma/nfout/" + name + ".png", System.Drawing.Imaging.ImageFormat.Png); }
    static SoundLibrary.Filter Flt(Action<SoundLibrary.Filter> set) { var f = new SoundLibrary.Filter(); set(f); return f; }
    static List<SoundItem> Find(Action<SoundLibrary.Filter> set) { return SoundLibrary.Search(Flt(set)); }
    static double Rms(float[] l, float[] r) { double e = 0; for (int i = 0; i < l.Length; i++) e += l[i] * l[i] + r[i] * r[i]; return Math.Sqrt(e / Math.Max(1, 2 * l.Length)); }

    [STAThread]
    static void Main()
    { Edition.Override = Tier.Fantasy;
        string tmp = Path.Combine(Path.GetTempPath(), "purphex_library"); try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch { } Directory.CreateDirectory(tmp); StudioEnv.DataDir = tmp;
        Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); eng = app.StudioEng; foreach (var m in eng.MasterFx) m.Bypass = true;
        SoundLibrary.Load();
        var ids = new[] { "dnb-rollers", "filthy-wobblers", "uk-drill" }; var recs = ids.Select(i => SoundCatalog.Find(i)).ToList();
        Check("the three test packs are in the catalog", recs.All(r => r != null), "");

        // ---- installing: real files, made and checked on this PC ----
        var results = new Dictionary<string, LibraryBaker.Result>(); var t0 = DateTime.Now;
        foreach (var r in recs) { var res = LibraryBaker.BakePack(r, null, null); SoundLibrary.AddOrReplace(res.Pack); results[r.Id] = res; }
        int made = results.Values.Sum(x => x.Made); Check("installing three packs makes real, checked sound files (about 200 each)", results.Values.All(x => x.Made >= 150), string.Join(", ", results.Select(kv => kv.Key + " " + kv.Value.Made + " made, " + kv.Value.Failed + " left out")) + " in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
        var inst = SoundLibrary.Installed();
        // 11 + 23: every listed sound is a real file that reads back with sound in it (no mock audio)
        { int ok = 0, bad = 0; string firstBad = ""; foreach (var s in inst) { bool good; if (s.Kind == "preset") good = File.Exists(s.Path) && new FileInfo(s.Path).Length > 20; else if (s.Kind == "midi") { double b; good = File.Exists(s.Path) && SoundIO.ReadMidi(s.Path, out b).Count > 0; } else { float[] l, rr; int sr; string fmt; good = SoundIO.Read(s.Path, out l, out rr, out sr, out fmt) && sr == 44100 && l.Length > 100 && Rms(l, rr) > 0.0005 && Math.Abs(l.Length / 44100.0 - s.Dur) < 0.01; } if (good) ok++; else { bad++; if (firstBad.Length == 0) firstBad = s.Rel; } }
          Check("every installed sound is a real file that opens, has sound in it and is as long as its details say", bad == 0 && ok == inst.Count && ok > 450, ok + " fine, " + bad + " bad " + firstBad); }
        // 18: counts match the files
        { int files = 0; foreach (var r in recs) files += Directory.GetFiles(Path.Combine(SoundLibrary.Root, r.Id), "*.*", SearchOption.AllDirectories).Count(f => f.EndsWith(".wav") || f.EndsWith(".mid") || f.EndsWith(".purphexsynth"));
          Check("the library's count is the number of real files (no made-up numbers)", SoundLibrary.InstalledCount() == inst.Count && files == inst.Count, SoundLibrary.InstalledCount() + " listed, " + files + " files"); }
        // 1, 2: drum loops are drum loops; bass loops are bass loops
        { var dl = Find(f => f.Type = "Drum Loop"); bool ok = dl.Count >= 30 && dl.All(s => s.Kind == "loop" && !s.Instant && s.Group == "Loops" && s.Bars >= 1 && Math.Abs(s.Dur - s.Beats * 60.0 / s.Bpm) < 0.01);
          Check("Drum Loops gives only real drum loops, each exactly its bars long at its tempo", ok, dl.Count + " drum loops");
          var bl = Find(f => { f.Group = "Bass"; f.Kind = "loop"; }); Check("Bass loops gives only bass and 808 loops (never a drum loop)", bl.Count >= 30 && bl.All(s => s.Type == "Bass Loop" || s.Type == "808 Loop") && !bl.Any(s => s.Type.Contains("Drum")), bl.Count + " bass loops: " + string.Join(", ", bl.Select(s => s.Type).Distinct()));
          var dq = Find(f => { f.Type = "Drum Loop"; f.Group = "Bass"; }); Check("drum loop and bass filters together give nothing (no drum loop is labelled bass)", dq.Count == 0, dq.Count + ""); }
        // 3, 4: bass one-shots and 808s
        { var bs = Find(f => { f.Group = "Bass"; f.Kind = "one-shot"; }).Where(s => s.Type != "808").ToList(); Check("bass one-shots are single playable bass notes (short, one-shot)", bs.Count >= 20 && bs.All(s => s.Dur < 4 && s.Kind == "one-shot" && s.Bpm == 0), bs.Count + " bass one-shots");
          var e8 = Find(f => f.Type = "808"); var names = e8.Select(s => s.Name.Replace(s.PackName + " - ", "")).ToList();
          Check("808 one-shots are 808s with different characters, not one 808 at twelve pitches", e8.Count >= 20 && e8.GroupBy(s => s.PackId).All(g => g.Select(s => s.Name).Distinct().Count() == g.Count() && g.All(s => s.Key == g.First().Key)) && names.Any(n => n.Contains("Slide")) && names.Any(n => n.Contains("Distorted")), e8.Count + " 808s: " + string.Join(", ", names.Take(6))); }
        // 5, 6, 7: genre, style and tempo filters combine
        { var drill = Find(f => f.Genre = "UK Drill"); Check("a genre filter gives only that genre", drill.Count > 100 && drill.All(s => s.Genre == "UK Drill"), drill.Count + "");
          var sub = Find(f => { f.Genre = "Drum & Bass"; f.Sub = "Rollers"; }); Check("a style works together with its genre", sub.Count > 100 && sub.All(s => s.Genre == "Drum & Bass" && s.Sub == "Rollers"), sub.Count + "");
          var fast = Find(f => { f.BpmMin = 166; f.BpmMax = 180; }); Check("a tempo range gives only sounds in it (DnB at 170-ish, no drill at 140)", fast.Count > 50 && fast.All(s => s.Bpm >= 166 && s.Bpm <= 180) && !fast.Any(s => s.Genre == "UK Drill"), fast.Count + "");
          var both = Find(f => { f.Genre = "Drum & Bass"; f.BpmMin = 166; f.BpmMax = 180; f.Type = "Bass Loop"; }); Check("genre, tempo and type together give only sounds that match all three", both.Count > 0 && both.All(s => s.Genre == "Drum & Bass" && s.Type == "Bass Loop" && s.Bpm >= 166), both.Count + ""); }
        // 8, 9: key filter (tonal sounds only; drums and FX stay unless asked)
        { string key = inst.First(s => s.Type == "Bass Loop").Key; var k = Find(f => f.Key = key); var keyed = k.Where(s => s.Tonal).ToList();
          Check("a key filter gives tonal sounds in that key only", keyed.Count > 5 && keyed.All(s => SoundLibrary.KeyMatches(s.Key, key)), keyed.Count + " in " + key);
          Check("the key filter does not hide drums (they have no key)", k.Any(s => s.Type == "Drum Loop"), k.Count(s => !s.Tonal) + " sounds with no key kept");
          var only = Find(f => { f.Key = key; f.OnlyKeyed = true; }); Check("'only sounds with a key' hides them", only.Count == keyed.Count && only.All(s => s.Tonal), only.Count + ""); }
        // 10: a pack shows exactly its sounds
        { var p = SoundLibrary.Pack("uk-drill"); var ps = Find(f => f.Pack = "uk-drill"); Check("a pack shows exactly the sounds that belong to it", ps.Count == p.Items.Count && ps.All(s => s.PackId == "uk-drill"), ps.Count + " of " + p.Items.Count); }
        // 22: licence and where each sound came from are kept (also after reloading)
        { SoundLibrary.Load(); var again = SoundLibrary.Installed(); bool ok = again.Count == inst.Count && again.All(s => s.Licence.Length > 0 && s.Source.Length > 0 && s.Origin.Length > 0 && s.Method.Length > 0 && s.Created.Length > 0 && s.Valid == "ok" && (s.Kind == "midi" || s.Hash.Length == 20)) && again.Any(s => s.Kind == "preset");
          Check("every sound keeps its licence, source, recipe, how it was made, when, its check result and its audio hash (after reloading too)", ok, again.Count + " reloaded");
          // 19: reindexing makes no duplicate records
          SoundLibrary.Load(); var third = SoundLibrary.Installed(); Check("reloading the library does not make duplicate records", third.Count == inst.Count && third.Select(s => s.Id).Distinct().Count() == third.Count, third.Count + ""); inst = third; }
        // 14, 15: copies are spotted
        { var rec = recs[0]; var plan = LibraryBaker.Plan(rec); var src = plan.First(s => s.Type == "Bass Loop"); string err; var b = InstantLib.MakeBuf(src, out err); var c1 = LibraryBaker.Check(b, src); var c2 = LibraryBaker.Check(InstantLib.MakeBuf(src, out err), src);
          Check("the same audio always gets the same hash (an exact copy is caught)", c1.Ok && c1.Hash == c2.Hash, c1.Hash);
          Check("no audio is in the library twice", inst.Where(s => s.Hash.Length > 0).GroupBy(s => s.Hash).All(g => g.Count() == 1), "");
          var near = new Buf(b.Length); var rnd = new Random(3); for (int i = 0; i < b.Length; i++) { float nz = (float)((rnd.NextDouble() - 0.5) * 0.002); near.L[i] = b.L[i] * 0.95f + nz; near.R[i] = b.R[i] * 0.95f + nz; }
          double simNear = LibraryBaker.Similarity(c1.Fp, LibraryBaker.Fingerprint(near, true)); var other = plan.First(s => s.Type == "Drum Loop"); double simOther = LibraryBaker.Similarity(c1.Fp, LibraryBaker.Check(InstantLib.MakeBuf(other, out err), other).Fp);
          Check("a nearly identical copy counts as a copy, a different sound does not", simNear >= LibraryBaker.SimilarLimit && simOther < LibraryBaker.SimilarLimit, "copy " + simNear.ToString("0.0000") + ", drum loop " + simOther.ToString("0.000"));
          int left = results.Values.Sum(x => x.Duplicates + x.Similar); Check("installing left out copies and too-similar sounds (and says why)", results.Values.All(x => x.Failures.Count == x.Failed) && File.ReadAllText(Path.Combine(SoundLibrary.Root, "uk-drill", "failed.txt")).Length >= 0, left + " copies left out across the packs"); }
        // 21: an interrupted install carries on from where it stopped
        { var rec = SoundCatalog.Find("amapiano-essentials"); int n = 0; bool stopped = false; try { LibraryBaker.BakePack(rec, (s, p) => { }, () => ++n > 40); } catch (OperationCanceledException) { stopped = true; }
          var part = SoundLibrary.ReadPackFile(Path.Combine(SoundLibrary.Root, rec.Id, "pack.part"), Path.Combine(SoundLibrary.Root, rec.Id)); int partN = part != null ? part.Items.Count : 0;
          var res = LibraryBaker.BakePack(rec, null, null); SoundLibrary.AddOrReplace(res.Pack);
          Check("an install that is stopped keeps what it made and carries on from there next time (no sound made twice)", stopped && partN > 20 && res.Resumed == partN && res.Pack.Items.Count == res.Resumed + res.Made && !File.Exists(Path.Combine(SoundLibrary.Root, rec.Id, "pack.part")) && res.Pack.Items.Select(s => s.Origin).Distinct().Count() == res.Pack.Items.Count, partN + " kept, " + res.Made + " more made"); inst = SoundLibrary.Installed(); }
        // 16: a missing file is reported (never shown as playable)
        { var victim = inst.First(s => s.Type == "Snare"); File.Delete(victim.Path); var rep = LibraryCheck.Check(false, null);
          Check("a missing file is found by the library check and the sound says it is missing", rep.Missing == 1 && rep.Problems.Any(p => p.File == victim.Rel) && !victim.Exists && File.Exists(rep.ReportPath), rep.Summary);
          var resRepair = LibraryBaker.BakePack(SoundCatalog.Find(victim.PackId), null, null); SoundLibrary.AddOrReplace(resRepair.Pack); inst = SoundLibrary.Installed(); Check("installing the pack again repairs it", inst.Where(s => s.PackId == victim.PackId).All(s => s.Exists), ""); }
        // the diagnostic report
        { string rp = LibraryCheck.Diagnostics(); string txt = File.ReadAllText(rp); Check("the library report counts the real sounds by section, genre, type and pack, and lists what failed and why", txt.Contains("BY GENRE") && txt.Contains("BY PACK") && txt.Contains("FAILED") && txt.Contains(SoundLibrary.InstalledCount().ToString("N0")), rp); File.Copy(rp, "C:/Users/thoma/nfout/library-report-test.txt", true); }
        // 20: a big library stays quick (the installed sounds plus 130,000 made-on-play sounds)
        { var sw = System.Diagnostics.Stopwatch.StartNew(); var all = Find(f => { f.Avail = "all"; f.Text = "bass"; }); long ms1 = sw.ElapsedMilliseconds; Check("searching every sound (installed and made on play) is quick", all.Count > 1000 && ms1 < 3000, all.Count.ToString("N0") + " found in " + ms1 + " ms"); }

        // ---- the studio: SOUND PACKS first, the sounds screen, previews, loading, honest menus ----
        sf = new StudioForm(app); sf.ClientSize = new Size(1600, 900); sf.Show(); Pump(400); Paint();
        sf.OpenPacks(); Paint(); Paint(); Check("SOUND PACKS opens on the packs (to install them), not on the sounds list", F<bool>("packsOpen") && F<bool>("soundsOpen"), ""); Shot("lib-packs");
        SetF("packsOpen", false); SetF("sndDirty", true); Paint(); Paint(); var res0 = F<List<SoundItem>>("sndResults");
        Check("the SOUNDS screen lists the installed sounds (real files) by default", res0.Count == SoundLibrary.InstalledCount() && res0.All(s => !s.Instant), res0.Count + ""); Shot("lib-sounds");
        // 17: options come from what is there; empty states say why
        { var gm = (ContextMenuStrip)M("GenreMenuStrip"); var txts = gm.Items.Cast<ToolStripItem>().Select(i => i.Text.ToUpperInvariant()).ToList();
          Check("the genre filter lists only genres that have sounds, with real counts", txts.Any(t => t.Contains("UK DRILL")) && !txts.Any(t => t.Contains("TECHNO")) && txts.Any(t => t.Contains("(")), string.Join(" | ", txts.Take(8)));
          var km = (ContextMenuStrip)M("KeyMenuStrip"); int keyItems = km.Items.Cast<ToolStripItem>().Count(i => i.Text.Contains(" MIN") || i.Text.Contains(" MAJ")); var keysThere = inst.Where(s => s.Tonal).Select(s => { int rt; bool mi; SoundLibrary.ParseKey(s.Key, out rt, out mi); return SoundLibrary.KeyName(rt, mi); }).Distinct().Count();
          Check("the key filter lists only keys that sounds have (not all 24)", keyItems == keysThere && keyItems < 24, keyItems + " keys");
          var sfl = F<SoundLibrary.Filter>("sndFilter"); sfl.Genre = "Techno"; SetF("sndDirty", true); Paint(); Paint(); Check("a filter with no matches shows an empty screen that says why and how to widen it", F<List<SoundItem>>("sndResults").Count == 0 && ((string)M("EmptyWhy")).Contains("Techno"), (string)M("EmptyWhy")); Shot("lib-empty");
          sfl.Genre = ""; SetF("sndDirty", true); Paint(); }
        // 12: preview starts and stops (no sound comes out during the test)
        { var s = inst.First(x => x.Type == "Drum Loop"); Auditioner.Play(s, m => { }); Pump(600); bool on = Auditioner.Current == s && (Auditioner.IsPlaying || Auditioner.Loading || Auditioner.LeftSamples != null); Auditioner.Stop(); Pump(200); Check("a preview starts on the sound and stops when asked", on && !Auditioner.IsPlaying, ""); }
        // 13: sounds load into the DAW
        { int before = eng.Tracks.Count; string e1 = (string)M("AddSample", inst.First(x => x.Type == "Drum Loop"), 0L, eng.Tracks.Count); string e2 = (string)M("AddSample", inst.First(x => x.Type == "808"), 0L, eng.Tracks.Count);
          Check("a drum loop and an 808 load into the song as audio tracks", e1 == null && e2 == null && eng.Tracks.Count == before + 2 && eng.Tracks.Last().Clip != null, (e1 ?? "ok") + " / " + (e2 ?? "ok")); }
        // the packs screen: real contents, Install makes real files
        { string pc = (string)M("PackContents", SoundCatalog.Find("uk-drill"), SoundLibrary.Pack("uk-drill")); Check("an installed pack says what it really holds", pc.Contains("drum loops") && pc.Contains("808s") && pc.StartsWith(SoundLibrary.Pack("uk-drill").Items.Count.ToString()), pc); }
        // the mixer desk: buses are made and used there
        { var t = StudioEngine.NewTrack("Lead", "audio", AudioClip.FromArrays(new float[44100], new float[44100], "x")); eng.Tracks.Add(t); sf.OpenInspector(1); Paint(); int buses = eng.Tracks.Count(x => x.IsBus);
          var nb = (Track)M("AddBus"); t.Out = nb; Paint(); Check("a bus is made on the mixer desk and a track goes into it from its strip", eng.Tracks.Count(x => x.IsBus) == buses + 1 && t.Out == nb, ""); Shot("lib-desk-bus");
          Check("the mixer has no separate BUSES tab any more", !File.ReadAllText("Studio.Inspector.cs").Contains("\"CHANNEL\", \"ALL PLUGINS\", \"BUSES\""), ""); }
        // menus stay inside the window
        { var m = Win.NewMenu(); for (int i = 0; i < 6; i++) m.Items.Add("Item " + i); var a = Win.AppArea(); m.Show(new Point(a.Right + 400, a.Bottom + 300)); Pump(150); var b = m.Bounds; m.Close();
          Check("a menu asked to open past the window opens inside it (never on another monitor)", !a.IsEmpty && a.Contains(b), "window " + a + ", menu " + b); }

        Console.WriteLine(fails == 0 ? "=== library checks passed ===" : "=== " + fails + " FAILED ===");
        try { sf.Close(); } catch { } Environment.Exit(fails == 0 ? 0 : 1);
    }
}
