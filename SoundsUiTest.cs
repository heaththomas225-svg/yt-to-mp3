using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// Drives the Purphex Sounds browser with synthetic mouse events: install a pack from the pack manager, search, preview, favourite, drag onto the timeline.   csc /main:Q ... SoundsUiTest.cs
static class Q
{
    static int fails; static StudioForm sf; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + "   " + d); Console.Out.Flush(); }
    static T F<T>(string n) { return (T)typeof(StudioForm).GetField(n, BF).GetValue(sf); }
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static void Ev(string name, MouseButtons b, int clicks, int x, int y) { typeof(StudioForm).GetMethod(name, BF | BindingFlags.DeclaredOnly).Invoke(sf, new object[] { new MouseEventArgs(b, clicks, x, y, 0) }); }
    static void Paint() { sf.Invalidate(); sf.Update(); Application.DoEvents(); }
    static List<HitReg> Regs() { var l = new List<HitReg>(); foreach (object o in (IEnumerable)typeof(StudioForm).GetField("regs", BF).GetValue(sf)) l.Add((HitReg)o); return l; }
    static void Click(float x, float y) { Ev("OnMouseMove", MouseButtons.None, 0, (int)x, (int)y); Ev("OnMouseDown", MouseButtons.Left, 1, (int)x, (int)y); Ev("OnMouseUp", MouseButtons.Left, 1, (int)x, (int)y); Paint(); }
    static void Call(string m) { typeof(StudioForm).GetMethod(m, BF).Invoke(sf, null); }
    static List<SoundItem> Results() { return (List<SoundItem>)typeof(StudioForm).GetField("sndResults", BF).GetValue(sf); }
    static List<HitReg> Rows() { return Regs().FindAll(r => Math.Abs(r.R.Width - 572) < 1f && Math.Abs(r.R.Height - 50) < 0.5f); }

    [STAThread]
    static void Main()
    { Edition.Override = Tier.Fantasy; /* the tests run as the full edition */
        string tmp = Path.Combine(Path.GetTempPath(), "purphex_soundsui"); try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch { } StudioEnv.DataDir = tmp;
        Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng;
        sf = new StudioForm(app); sf.ClientSize = new Size(1500, 820); sf.Show(); Pump(400); Paint();
        var rec = SoundCatalog.Find("naughty-dnb-rollers"); rec.Parts = "drums,bass,loops,midi,presets";
        Call("OpenSounds"); Paint(); Pump(100); Paint();
        Check("the SOUNDS panel opens", F<bool>("soundsOpen"), "");
        Check("with no packs installed the browser already lists the instant drum and bass loops (50,000+)", Results().Count >= 50000 && Rows().Count >= 3, Results().Count + " sounds");
        { // filters: Trap drum loops, House bass loops in F minor: hundreds each, before any pack is installed
          var Fi = F<SoundLibrary.Filter>("sndFilter"); Fi.Genre = "Trap"; Fi.Type = "Drum Loop"; SetF("sndDirty", true); Paint(); int trapDrums = Results().Count; bool allTrap = Results().TrueForAll(s => s.Genre == "Trap" && s.Type == "Drum Loop");
          Fi.Genre = "House"; Fi.Type = "Bass Loop"; Fi.Key = "F min"; SetF("sndDirty", true); Paint(); int houseBass = Results().Count;
          Check("every filter has loops on it: Trap drum loops and House bass loops in F minor are listed straight away", trapDrums >= 500 && allTrap && houseBass >= 20, trapDrums + " trap drum loops, " + houseBass + " house bass loops in F min");
          Fi.Genre = "House"; Fi.Type = "Drum Loop"; Fi.Key = ""; SetF("sndDirty", true); Paint();
          { var bmpF = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height); sf.DrawToBitmap(bmpF, new Rectangle(0, 0, sf.Width, sf.Height)); bmpF.Save("C:/Users/thoma/nfout/sounds-filters.png", System.Drawing.Imaging.ImageFormat.Png); }
          var chips = Regs().FindAll(r => r.R.Y > 300 && r.R.Y < 320 && r.R.X > 880); Check("the filter buttons show what is picked", chips.Count >= 5, chips.Count + " filter buttons");
          var gm = sf.GenreMenuStrip(); var gtexts = new List<string>(); foreach (ToolStripItem it in gm.Items) gtexts.Add((it.Text ?? "").ToUpperInvariant());
          bool secs = gtexts.Contains("DRUM & BASS") && gtexts.Contains("HIP-HOP & RAP") && gtexts.Contains("HOUSE") && gtexts.Contains("AFRO & LATIN"); bool counted = gtexts.Exists(t => t.Trim().StartsWith("TRAP   (") && t.Contains("SOUNDS)")) && gtexts.Exists(t => t.Trim().StartsWith("DUBSTEP   ("));
          Check("the Genre menu lists every genre under its section, each with how many sounds it has", secs && counted && !gtexts.Exists(t => t.Contains("ELECTRONIC BASS") || t.Contains("RAP AND VOCALS")), string.Join(" / ", gtexts.GetRange(0, Math.Min(12, gtexts.Count))));
          Fi.Genre = "House"; var sm = sf.StyleMenuStrip(); var stexts = new List<string>(); foreach (ToolStripItem it in sm.Items) stexts.Add((it.Text ?? "").ToUpperInvariant());
          Check("the Style menu lists the chosen genre's styles with counts (House: Tech House, Deep House, Bass House...)", stexts.Exists(t => t.StartsWith("TECH HOUSE   (")) && stexts.Exists(t => t.StartsWith("DEEP HOUSE   (")) && stexts.Exists(t => t.StartsWith("BASS HOUSE   (")), string.Join(" / ", stexts));
          Fi.Genre = ""; Fi.Type = ""; Fi.Key = ""; Fi.Sub = ""; SetF("sndDirty", true); Paint(); }
        { // an instant loop plays and drags like any sound: made on the spot
          var inst0 = InstantLib.Items.Find(s => s.PackId == "uk-drill" || s.Genre == "UK Drill"); int nT0 = eng.Tracks.Count; string am = (string)typeof(StudioForm).GetMethod("AddSample", BF).Invoke(sf, new object[] { inst0, 0L, eng.Tracks.Count });
          Check("an instant loop added to the song becomes a real clip of the right length", am == null && eng.Tracks.Count == nT0 + 1 && eng.Tracks[eng.Tracks.Count - 1].Clip != null && Math.Abs(eng.Tracks[eng.Tracks.Count - 1].Clip.Length - inst0.Dur * 44100) < 50, (am ?? "ok") + ", " + inst0.Name);
        }
        SetF("packsOpen", true); SetF("packScroll", 0f); Paint();
        Check("Open the packs shows the pack manager", F<bool>("packsOpen"), "");
        var inst = Regs().FindAll(r => Math.Abs(r.R.Width - 160) < 0.5f && Math.Abs(r.R.Height - 32) < 0.5f); Check("every pack has an Install button", inst.Count >= 3, inst.Count + " visible");
        var bmpP = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height); sf.DrawToBitmap(bmpP, new Rectangle(0, 0, sf.Width, sf.Height)); bmpP.Save("_packs.png", System.Drawing.Imaging.ImageFormat.Png);
        // find the Naughty DnB Rollers row's install button: it's the one whose row is first in the DnB section
        SetF("packSection", "All"); Paint(); inst = Regs().FindAll(r => Math.Abs(r.R.Width - 160) < 0.5f && Math.Abs(r.R.Height - 32) < 0.5f);
        string wantId = "naughty-dnb-rollers"; var idx = SoundCatalog.Recipes.FindIndex(r => r.Id == wantId); var rows = new List<Recipe>(SoundCatalog.Recipes);
        // scroll to the pack, then click its Install button (the Nth row)
        SetF("packScroll", (float)(idx * 66)); Paint(); inst = Regs().FindAll(r => Math.Abs(r.R.Width - 160) < 0.5f && Math.Abs(r.R.Height - 32) < 0.5f); Click(inst[0].R.X + 80, inst[0].R.Y + 16);
        var t0 = DateTime.Now; while (SoundLibrary.Pack(wantId) == null || SoundLibrary.Pack(wantId).Items.Count < 20) { Pump(200); Paint(); if ((DateTime.Now - t0).TotalSeconds > 120) break; }
        var pk = SoundLibrary.Pack(wantId); Check("Install makes the pack on this PC (real files)", pk != null && pk.Items.Count > 20 && File.Exists(pk.Items[0].Path), pk == null ? "none" : pk.Items.Count + " sounds in " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
        Pump(300); Paint(); var bmpP2 = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height); sf.DrawToBitmap(bmpP2, new Rectangle(0, 0, sf.Width, sf.Height)); bmpP2.Save("_packs2.png", System.Drawing.Imaging.ImageFormat.Png);
        SetF("packsOpen", false); SetF("sndDirty", true); Paint(); Paint();
        Check("the browser lists the new sounds", Results().Count > 20 && Rows().Count >= 3, Results().Count + " results, " + Rows().Count + " rows");
        // search
        var box = F<TextBox>("soundBox"); Check("the search box is on screen", box.Visible && box.Width > 200, box.Bounds.ToString()); box.Text = "kick"; Paint(); Paint();
        bool allKick = Results().Count > 0; foreach (var s in Results()) if ((s.Name + " " + s.Type + " " + s.Tags).ToLowerInvariant().IndexOf("kick") < 0) allKick = false; Check("searching 'kick' finds only kicks", allKick, Results().Count + " results");
        box.Text = "zzzzqq"; Paint(); Paint(); Check("a search with no match shows nothing", Results().Count == 0, ""); box.Text = ""; Paint(); Paint();
        // key/tempo filters
        var F2 = F<SoundLibrary.Filter>("sndFilter"); F2.BpmMin = 170; F2.BpmMax = 178; SetF("sndDirty", true); Paint(); bool bpmOk = Results().Count > 0; foreach (var s in Results()) if (s.Bpm > 0 && (s.Bpm < 169 || s.Bpm > 179) && !(s.Bpm * 2 >= 170 && s.Bpm * 2 <= 178)) bpmOk = false; Check("the tempo filter keeps only 170-178 BPM loops (plus untimed one-shots)", bpmOk, Results().Count + " results");
        F2.BpmMin = 0; F2.BpmMax = 0; F2.Kind = "loop"; SetF("sndDirty", true); Paint(); Check("the Loops filter shows loops only", Results().Count > 0 && Results().TrueForAll(s => s.Kind == "loop"), Results().Count + " loops"); F2.Kind = ""; SetF("sndDirty", true); Paint();
        // select + preview
        var rowsNow = Rows(); var r0 = rowsNow[0]; Click(r0.R.X + 120, r0.R.Y + 25); var sel = F<SoundItem>("sndSel"); Check("clicking a sound selects it", sel != null && sel == Results()[0], sel == null ? "none" : sel.Name);
        t0 = DateTime.Now; while (Auditioner.Loading && (DateTime.Now - t0).TotalSeconds < 5) Pump(50); Pump(300);
        Check("and starts playing it (instant preview)", Auditioner.Current == sel && (Auditioner.IsPlaying || Auditioner.Seconds > 0 || Auditioner.Length > 0), "length " + Auditioner.Length.ToString("0.00") + " s, error '" + Auditioner.Error + "'");
        Paint(); var bmpA = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height); sf.DrawToBitmap(bmpA, new Rectangle(0, 0, sf.Width, sf.Height)); bmpA.Save("_sounds_ui.png", System.Drawing.Imaging.ImageFormat.Png);
        // favourite
        var star = Regs().Find(r => Math.Abs(r.R.Width - 22) < 0.5f && Math.Abs(r.R.Height - 24) < 0.5f); Click(star.R.X + 11, star.R.Y + 12); Check("the star adds a favourite", SoundLibrary.Favourites.Contains(sel.Id), "");
        F2.OnlyFav = true; SetF("sndDirty", true); Paint(); Check("the Favourites filter then shows it", Results().Count == 1 && Results()[0] == sel, Results().Count + ""); F2.OnlyFav = false; SetF("sndDirty", true); Paint();
        // drag onto the timeline
        Auditioner.Stop(); int tracks0 = eng.Tracks.Count; rowsNow = Rows(); var rd = rowsNow[1]; var drag = Results()[1]; int sx = (int)(rd.R.X + 120), sy = (int)(rd.R.Y + 25);
        Ev("OnMouseMove", MouseButtons.None, 0, sx, sy); Ev("OnMouseDown", MouseButtons.Left, 1, sx, sy); for (int i = 1; i <= 8; i++) Ev("OnMouseMove", MouseButtons.Left, 0, sx - i * 60, sy + i * 15); Paint();
        Check("dragging a sound shows the drop ghost", F<bool>("sndDragging"), ""); Ev("OnMouseUp", MouseButtons.Left, 1, 600, 330); Paint();
        Check("dropping it on the timeline makes a new track with the sample as a real clip", eng.Tracks.Count == tracks0 + 1 && eng.Tracks[eng.Tracks.Count - 1].Clip != null && eng.Tracks[eng.Tracks.Count - 1].Clip.Length > 1000, eng.Tracks.Count + " tracks, " + F<string>("toast") + " | " + drag.Name);
        Check("the clip knows its source file", eng.Tracks.Count > tracks0 && eng.Tracks[eng.Tracks.Count - 1].Clip.Path == drag.Path, "");
        { SetF("sndScroll", 120f); Paint(); float before = F<float>("sndScroll"); var pick = Results()[2]; SetF("sndDrag", pick); SetF("sndDragging", true); int nT = eng.Tracks.Count;
          typeof(StudioForm).GetMethod("DropSampleAt", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(sf, new object[] { new Point(600, 330) }); Paint();
          Check("after dropping a sound the sound packs stay open where you were (same scroll, same list)", F<bool>("soundsOpen") && eng.Tracks.Count == nT + 1 && Math.Abs(F<float>("sndScroll") - before) < 1 && Results()[2] == pick, "open " + F<bool>("soundsOpen") + ", scroll " + before + " -> " + F<float>("sndScroll")); }
        // dropping somewhere that isn't the timeline does nothing
        int t1 = eng.Tracks.Count; Ev("OnMouseMove", MouseButtons.None, 0, sx, sy); Ev("OnMouseDown", MouseButtons.Left, 1, sx, sy); for (int i = 1; i <= 4; i++) Ev("OnMouseMove", MouseButtons.Left, 0, sx + i * 5, sy + i * 5); Ev("OnMouseUp", MouseButtons.Left, 1, sx + 20, sy + 20); Paint(); Check("letting go over the panel does not add anything", eng.Tracks.Count == t1, "");
        // a double click only plays it again (sounds go on the timeline by dragging)
        rowsNow = Rows(); var rdc = rowsNow[2]; Ev("OnMouseMove", MouseButtons.None, 0, (int)rdc.R.X + 120, (int)rdc.R.Y + 25); Ev("OnMouseDown", MouseButtons.Left, 2, (int)rdc.R.X + 120, (int)rdc.R.Y + 25); Ev("OnMouseUp", MouseButtons.Left, 1, (int)rdc.R.X + 120, (int)rdc.R.Y + 25); Paint();
        Check("double-clicking a sound does not add it to the song (drag it to use it)", eng.Tracks.Count == t1, eng.Tracks.Count + " tracks");
        // midi without an instrument track
        SoundItem midi = null; foreach (var s in SoundLibrary.All()) if (s.Kind == "midi") { midi = s; break; } Check("the pack includes MIDI files", midi != null, "");
        if (midi != null)
        {
            var m = typeof(StudioForm).GetMethod("AddSample", BF); string msg = (string)m.Invoke(sf, new object[] { midi, 0L, -1 }); Check("MIDI dropped on an audio track explains what to do instead of failing silently", msg != null && msg.IndexOf("instrument", StringComparison.OrdinalIgnoreCase) >= 0, msg);
            var ins = StudioEngine.NewTrack("Keys", "instrument", null); ins.Instrument = true; eng.Tracks.Add(ins); msg = (string)m.Invoke(sf, new object[] { midi, 0L, eng.Tracks.Count - 1 }); Check("MIDI dropped on an instrument track adds its notes", msg == null && ins.Midi.Count > 4, msg ?? (ins.Midi.Count + " events"));
        }
        // preset
        SoundItem pre = null; foreach (var s in SoundLibrary.All()) if (s.Kind == "preset") { pre = s; break; } Check("the pack includes Bass Lab presets", pre != null, "");
        if (pre != null) { var m = typeof(StudioForm).GetMethod("AddSample", BF); string msg = (string)m.Invoke(sf, new object[] { pre, 0L, 0 }); Check("a synth preset is installed into Bass Lab's My presets", msg == null && File.Exists(Path.Combine(SynthPresets.Dir, PackWriter.Clean(pre.Name) + ".txt")), msg ?? ""); }
        // overlap: menu open hides the search box
        Call("OpenMenu"); Paint(); Check("while the side menu is open the search box steps aside", !box.Visible, ""); Call("CloseMenu"); Paint(); Check("and comes back", box.Visible, "");
        SetF("tab", 3); Paint(); Check("leaving the Mix page hides the search box", !box.Visible, ""); SetF("tab", 0); Paint();
        // the SOUND PACKS search
        { Func<string, List<Recipe>> find = q => SoundCatalog.Recipes.FindAll(r => StudioForm.PackMatches(r, q));
          var dr = find("drill"); var am = find("amapiano"); var both = find("dark trap"); var none = find("zzzz nothing");
          Check("SOUND PACKS search finds packs by genre and words (drill, amapiano, dark trap) and nothing for nonsense", dr.Count >= 6 && dr.TrueForAll(r => (r.Name + r.Genre + r.Sub + r.Tags + r.Desc + r.Mood).ToLowerInvariant().Contains("drill")) && am.Count >= 4 && both.Count >= 1 && none.Count == 0, dr.Count + " drill, " + am.Count + " amapiano, " + both.Count + " dark trap");
          Check("search can find packs by tempo (140 bpm)", find("140 bpm").Count >= 10 && find("140bpm").TrueForAll(r => Array.IndexOf(r.Bpms, 140.0) >= 0), find("140 bpm").Count + " packs");
          SetF("packsOpen", true); Paint(); var pb = typeof(StudioForm).GetField("packBox", BF).GetValue(sf) as TextBox;
          Check("the search box shows in SOUND PACKS", pb != null && pb.Visible, ""); pb.Text = "amapiano"; Paint(); Check("typing filters the pack list", (string)typeof(StudioForm).GetField("packQuery", BF).GetValue(sf) == "amapiano", "");
          { var bmp = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height); using (var gg = Graphics.FromImage(bmp)) typeof(StudioForm).GetMethod("PaintAll", BF).Invoke(sf, new object[] { new PaintEventArgs(gg, new Rectangle(Point.Empty, sf.ClientSize)) }); bmp.Save("C:/Users/thoma/nfout/packs-search.png", System.Drawing.Imaging.ImageFormat.Png); bmp.Dispose(); }
          pb.Text = ""; SetF("packsOpen", false); Paint(); Check("closing SOUND PACKS hides its search box", !pb.Visible, ""); }
        // a double-click on a sound only plays it (dragging is how a sound goes on the timeline)
        { string src = File.Exists("Studio.Sounds.cs") ? File.ReadAllText("Studio.Sounds.cs") : ""; Check("double-clicking a sound does not put it on a track", src.Length == 0 || (src.Contains("rg.OnDouble = () => SelectSound(s, true);") && !src.Contains("rg.OnDouble = () => AddSampleAtNeedle(s);")), ""); }
        // removing a pack
        SoundLibrary.RemovePack(wantId); Check("removing a pack deletes its files", !Directory.Exists(Path.Combine(SoundLibrary.Root, wantId)) && SoundLibrary.Pack(wantId) == null, "");
        Auditioner.Shutdown(); eng.Shutdown(); try { Directory.Delete(tmp, true); } catch { } Console.WriteLine(fails == 0 ? "=== sounds UI checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
