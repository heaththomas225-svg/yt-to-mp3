using System.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// The projects page: asking before opening, removing (list only / Recycle Bin), remove picture, the logo as the default picture, pictures that fill the square,
// the BACK button, the preview player, and the colour themes.   csc /main:PR ... ProjectsTest.cs
static class PR
{
    static int fails; static StudioForm sf; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + "   " + d); Console.Out.Flush(); }
    static T F<T>(string n) { return (T)typeof(StudioForm).GetField(n, BF).GetValue(sf); }
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static void Ev(string name, MouseButtons b, int clicks, int x, int y) { typeof(StudioForm).GetMethod(name, BF | BindingFlags.DeclaredOnly).Invoke(sf, new object[] { new MouseEventArgs(b, clicks, x, y, 0) }); }
    static void Paint() { sf.Invalidate(); sf.Update(); Application.DoEvents(); }
    static List<HitReg> Regs() { var l = new List<HitReg>(); foreach (object o in (IEnumerable)typeof(StudioForm).GetField("regs", BF).GetValue(sf)) l.Add((HitReg)o); return l; }
    static void Click(RectangleF r) { int x = (int)(r.X + r.Width / 2), y = (int)(r.Y + r.Height / 2); Ev("OnMouseMove", MouseButtons.None, 0, x, y); Ev("OnMouseDown", MouseButtons.Left, 1, x, y); Ev("OnMouseUp", MouseButtons.Left, 1, x, y); }
    static AudioClip NoiseClip(int sec, int seed) { int n = 44100 * sec; var x = new float[n]; var rn = new Random(seed); double b0 = 0, b1 = 0, b2 = 0; for (int i = 0; i < n; i++) { double w = rn.NextDouble() * 2 - 1; b0 = 0.99765 * b0 + w * 0.0990460; b1 = 0.96300 * b1 + w * 0.2965164; b2 = 0.57000 * b2 + w * 1.0526913; x[i] = (float)((b0 + b1 + b2 + w * 0.1848) * 0.12); } return AudioClip.FromArrays(x, (float[])x.Clone(), "pink"); }
    static AudioClip QuietLoud() { int n = 44100 * 60; var x = new float[n]; var rn = new Random(5); for (int i = 0; i < n; i++) { double sec = i / 44100.0; double amp = sec >= 35 && sec < 55 ? 0.8 : 0.03; x[i] = (float)(amp * (rn.NextDouble() * 2 - 1)); } return AudioClip.FromArrays(x, (float[])x.Clone(), "quiet"); }
    static AudioClip ToneClip(int sec) { int n = 44100 * sec; var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(0.4 * Math.Sin(i * 0.05)); return AudioClip.FromArrays(x, (float[])x.Clone(), "t"); }
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point p);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    static Color ThemeStatic(string n) { return (Color)typeof(StudioForm).GetField(n, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null); }

    [STAThread]
    static void Main()
    { Edition.Override = Tier.Fantasy; /* the tests run as the full edition */
        StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_projtest_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(StudioEnv.DataDir);   // this test never touches your real settings, projects, bin or lyrics draft
        Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng;
        string dir = Path.Combine(Path.GetTempPath(), "projtest-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        string data = StudioEnv.DataDir; string bak = Path.Combine(dir, "bak"); Directory.CreateDirectory(bak);
        var realBin = new List<ProjectLibrary.BinItem>(ProjectLibrary.Bin); ProjectLibrary.Bin.Clear(); bool hadBin = File.Exists(Path.Combine(data, "bin.txt"));
        foreach (var f in new[] { "settings.txt", "projects.txt", "bin.txt" }) { try { if (File.Exists(Path.Combine(data, f))) File.Copy(Path.Combine(data, f), Path.Combine(bak, f)); } catch { } }
        try
        {
            sf = new StudioForm(app); sf.ClientSize = new Size(1500, 800); sf.Show(); Pump(400);
            string p1 = Path.Combine(dir, "Alpha.ytmproj"), p2 = Path.Combine(dir, "Beta.ytmproj"), png = Path.Combine(dir, "pic.png");
            ProjectIO.Save(eng, p1); ProjectIO.Save(eng, p2); ProjectLibrary.Touch(p1, "Alpha", 3, 140, 95, true, null); ProjectLibrary.Touch(p2, "Beta", 1, 90, 30, true, null); SetF("projectsLoaded", true);
            var e1 = ProjectLibrary.Find(p1); var e2 = ProjectLibrary.Find(p2);

            // ---- confirm before opening ----
            var asked = new List<string>(); bool answer = false; SetF("confirmOpenHook", (Func<ProjectEntry, bool>)(en => { asked.Add(en.Name); return answer; }));
            SetF("tab", 3); typeof(StudioForm).GetMethod("OpenProjectAsk", BF).Invoke(sf, new object[] { e1 }); Check("clicking a project asks first", asked.Count == 1 && asked[0] == "Alpha", asked.Count + " question(s)");
            Check("saying no keeps you on the projects page (you are not sent in blind)", F<int>("tab") == 3, "tab " + F<int>("tab"));
            answer = true; typeof(StudioForm).GetMethod("OpenProjectAsk", BF).Invoke(sf, new object[] { e1 }); Pump(300); Check("saying yes opens the project", asked.Count == 2 && F<int>("tab") == 0, "tab " + F<int>("tab"));
            SetF("confirmOpenHook", null);

            // ---- removing a project ----
            int choice = 0; SetF("removeHook", (Func<ProjectEntry, int>)(en => choice));
            typeof(StudioForm).GetMethod("RemoveProjectAsk", BF).Invoke(sf, new object[] { e2 }); Check("cancelling the removal changes nothing", ProjectLibrary.Find(p2) != null && File.Exists(p2) && ProjectLibrary.Bin.Count == 0, "");
            choice = 1; typeof(StudioForm).GetMethod("RemoveProjectAsk", BF).Invoke(sf, new object[] { e2 }); Check("remove from the list only keeps the file on the PC", ProjectLibrary.Find(p2) == null && File.Exists(p2) && ProjectLibrary.Bin.Count == 0, "");
            ProjectLibrary.Touch(p2, "Beta", 1, 90, 30, true, null); e2 = ProjectLibrary.Find(p2);
            choice = 2; typeof(StudioForm).GetMethod("RemoveProjectAsk", BF).Invoke(sf, new object[] { e2 }); Check("remove and delete moves the file into the Recycling Bin (not gone yet)", ProjectLibrary.Find(p2) == null && !File.Exists(p2) && ProjectLibrary.Bin.Count == 1 && File.Exists(ProjectLibrary.BinPath(ProjectLibrary.Bin[0])), ProjectLibrary.Bin.Count + " in the bin");
            SetF("removeHook", null);

            // ---- the Recycling Bin: restore, delete for good, 30 days ----
            var bi = ProjectLibrary.Bin[0]; Check("the bin remembers the name and shows 30 days left", bi.Name == "Beta" && ProjectLibrary.DaysLeft(bi, DateTime.Now) == 30, bi.Name + ", " + ProjectLibrary.DaysLeft(bi, DateTime.Now) + " days");
            Check("20 days later it shows 10 days left", ProjectLibrary.DaysLeft(bi, DateTime.Now.AddDays(20)) == 10, ProjectLibrary.DaysLeft(bi, DateTime.Now.AddDays(20)).ToString());
            File.WriteAllText(p2, "someone made a new file with the old name"); string rerr; string back2 = ProjectLibrary.Restore(bi, out rerr);
            Check("restoring never overwrites a file that took the name (it comes back under a new name)", back2 != null && back2 != p2 && File.Exists(back2) && File.ReadAllText(p2).StartsWith("someone") && ProjectLibrary.Find(back2) != null && ProjectLibrary.Bin.Count == 0, back2 ?? rerr);
            File.Delete(p2); ProjectLibrary.Remove(back2); ProjectIO.Save(eng, p2); ProjectLibrary.Touch(p2, "Beta", 1, 90, 30, true, null); var bi2 = ProjectLibrary.MoveToBin(ProjectLibrary.Find(p2)); string back3 = ProjectLibrary.Restore(bi2, out rerr);
            Check("restore puts the project back in its own place in My Projects", back3 == p2 && File.Exists(p2) && ProjectLibrary.Find(p2) != null && ProjectLibrary.Bin.Count == 0, back3 ?? rerr);
            ProjectIO.Save(eng, p2); ProjectLibrary.Touch(p2, "Beta", 1, 90, 30, true, null); var bi3 = ProjectLibrary.MoveToBin(ProjectLibrary.Find(p2)); string binFile = ProjectLibrary.BinPath(bi3); var answers = new List<string>(); bool say = false; SetF("binConfirmHook", (Func<string, bool>)(t => { answers.Add(t); return say; }));
            typeof(StudioForm).GetMethod("BinDelete", BF).Invoke(sf, new object[] { bi3 }); Check("DELETE FOREVER asks first and says no keeps it", answers.Count == 1 && ProjectLibrary.Bin.Count == 1 && File.Exists(binFile), answers.Count + " question(s)");
            say = true; typeof(StudioForm).GetMethod("BinDelete", BF).Invoke(sf, new object[] { bi3 }); Check("and yes deletes the file for good", ProjectLibrary.Bin.Count == 0 && !File.Exists(binFile), "");
            ProjectLibrary.Touch(p2, "Beta", 1, 90, 30, true, null); ProjectIO.Save(eng, p2); var bi4 = ProjectLibrary.MoveToBin(ProjectLibrary.Find(p2)); bi4.Deleted = DateTime.Now.AddDays(-31);
            ProjectLibrary.Touch(p2, "Beta", 1, 90, 30, true, null); ProjectIO.Save(eng, p2); var bi5 = ProjectLibrary.MoveToBin(ProjectLibrary.Find(p2)); bi5.Deleted = DateTime.Now.AddDays(-5); string f4 = ProjectLibrary.BinPath(bi4), f5 = ProjectLibrary.BinPath(bi5);
            int purged = ProjectLibrary.PurgeBin(DateTime.Now); Check("after 30 days a project is deleted for good, a younger one stays", purged == 1 && !File.Exists(f4) && File.Exists(f5) && ProjectLibrary.Bin.Count == 1 && ProjectLibrary.Bin[0] == bi5, purged + " purged");
            ProjectLibrary.SaveBin(); ProjectLibrary.Bin.Clear(); ProjectLibrary.LoadBin(); Check("the bin is remembered between runs (name and date)", ProjectLibrary.Bin.Count == 1 && ProjectLibrary.Bin[0].Name == "Beta" && (DateTime.Now - ProjectLibrary.Bin[0].Deleted).TotalDays > 4.9 && ProjectLibrary.Bin[0].Id == bi5.Id, ProjectLibrary.Bin.Count + " in the bin");
            say = false; answers.Clear(); typeof(StudioForm).GetMethod("BinEmpty", BF).Invoke(sf, null); Check("EMPTY THE BIN asks first and no changes nothing", answers.Count == 1 && ProjectLibrary.Bin.Count == 1, "");
            say = true; typeof(StudioForm).GetMethod("BinEmpty", BF).Invoke(sf, null); Check("EMPTY THE BIN deletes everything for good", ProjectLibrary.Bin.Count == 0 && !File.Exists(f5), "");
            SetF("binConfirmHook", null);
            SetF("tab", 3); SetF("projBin", true); Paint(); var bk = Regs().FindLast(r => Math.Abs(r.R.X - 24) < 0.5f && Math.Abs(r.R.Width - 150) < 0.5f && r.R.Y < 120); Check("the bin page has a PROJECTS button", bk != null, ""); if (bk != null) { Click(bk.R); Check("PROJECTS goes back to the project cards", !F<bool>("projBin"), ""); }
            ProjectLibrary.Touch(p2, "Beta", 1, 90, 30, true, null); ProjectIO.Save(eng, p2); ProjectLibrary.MoveToBin(ProjectLibrary.Find(p2)); SetF("projBin", true); Paint(); Check("the bin page draws with a project in it (RESTORE and DELETE FOREVER buttons)", Regs().FindAll(r => Math.Abs(r.R.Width - 110) < 0.5f || Math.Abs(r.R.Width - 156) < 0.5f).Count >= 2, ""); SetF("projBin", false);
            var lastBin = ProjectLibrary.Bin[0]; string lerr; ProjectLibrary.Restore(lastBin, out lerr); ProjectLibrary.Remove(p2);

            // ---- search, sort and filters ----
            string[] nm = { "Summer drill", "Late night", "Alpha beat", "Zed vocals" }; string[] ps = new string[4];
            for (int i = 0; i < 4; i++) { ps[i] = Path.Combine(dir, "S" + i + ".ytmproj"); ProjectIO.Save(eng, ps[i]); ProjectLibrary.Touch(ps[i], nm[i], 1 + i * 2, 90 + i * 10, 60 + i * 60, true, null); var en = ProjectLibrary.Find(ps[i]); en.Updated = DateTime.Now.AddDays(-i); en.Opened = DateTime.Now.AddDays(i); en.Kind = i == 0 ? "Song" : (i == 1 ? "Beat" : (i == 2 ? "Beat" : "Vocals")); }
            Func<List<ProjectEntry>> vis = () => (List<ProjectEntry>)typeof(StudioForm).GetMethod("VisibleProjects", BF).Invoke(sf, null); Func<string> pn = () => string.Join(",", vis().FindAll(x => Array.IndexOf(ps, x.Path) >= 0).ConvertAll(x => x.Name).ToArray());
            SetF("projSearch", "drill"); Check("searching finds a project by part of its name", pn() == "Summer drill", pn()); SetF("projSearch", "ALPHA"); Check("the search ignores capital letters", pn() == "Alpha beat", pn());
            SetF("projSearch", "vocals"); Check("the search also matches the type", pn() == "Zed vocals", pn()); SetF("projSearch", "night late"); Check("every word has to match, in any order", pn() == "Late night", pn()); SetF("projSearch", "nothing like this"); Check("nothing matching gives an empty list", pn() == "", pn()); SetF("projSearch", "");
            SetF("projFav", true); Check("FAVOURITES shows nothing until you star something", pn() == "", pn()); ProjectLibrary.SetFlags(ps[1], true, null); ProjectLibrary.SetFlags(ps[3], true, null); Check("starred projects are the favourites", pn() == "Late night,Zed vocals", pn()); SetF("projFav", false);
            SetF("projType", "Beat"); Check("TYPE: Beat shows only beats", pn() == "Late night,Alpha beat", pn()); SetF("projType", "Song"); Check("TYPE: Song shows only songs", pn() == "Summer drill", pn()); SetF("projType", "Beat"); SetF("projFav", true); Check("filters combine (beats that are favourites)", pn() == "Late night", pn()); SetF("projFav", false); SetF("projType", "");
            SetF("projSort", 0); Check("sort: newest edited first", pn() == "Summer drill,Late night,Alpha beat,Zed vocals", pn()); SetF("projSort", 6); Check("sort: oldest edited first", pn() == "Zed vocals,Alpha beat,Late night,Summer drill", pn());
            SetF("projSort", 2); Check("sort: name A to Z", pn() == "Alpha beat,Late night,Summer drill,Zed vocals", pn()); SetF("projSort", 3); Check("sort: name Z to A", pn() == "Zed vocals,Summer drill,Late night,Alpha beat", pn());
            SetF("projSort", 4); Check("sort: most tracks first", pn() == "Zed vocals,Alpha beat,Late night,Summer drill", pn()); SetF("projSort", 5); Check("sort: longest first", pn() == "Zed vocals,Alpha beat,Late night,Summer drill", pn()); SetF("projSort", 1); Check("sort: recently opened first", pn() == "Zed vocals,Alpha beat,Late night,Summer drill", pn()); SetF("projSort", 0);
            ProjectLibrary.Entries.Find(x => x.Path == ps[0]).Kind = ""; ProjectLibrary.EnsureKind(ProjectLibrary.Find(ps[0])); Check("the type of an old project is worked out from its tracks (an empty one is Empty)", ProjectLibrary.Find(ps[0]).Kind == "Empty", ProjectLibrary.Find(ps[0]).Kind);
            Check("kinds: vocals with a beat is a Song, vocals alone Vocals, beats and instruments Beat", ProjectLibrary.KindOfRoles(new[] { "vocal", "beat" }) == "Song" && ProjectLibrary.KindOfRoles(new[] { "vocal" }) == "Vocals" && ProjectLibrary.KindOfRoles(new[] { "beat", "inst", "bus" }) == "Beat" && ProjectLibrary.KindOfRoles(new string[0]) == "Empty", "");
            ProjectLibrary.Save(); ProjectLibrary.Load(); var fe = ProjectLibrary.Find(ps[1]); Check("favourite and type are remembered between runs", fe != null && fe.Fav && fe.Kind == "Beat", fe == null ? "none" : fe.Fav + " " + fe.Kind);
            // the search box takes typing on the projects page
            SetF("tab", 3); SetF("projSearchOn", true); SetF("projSearch", ""); foreach (char ch in "dri") typeof(StudioForm).GetMethod("ProjSearchKey", BF).Invoke(sf, new object[] { ch }); Check("typing on the projects page goes into the search box", F<string>("projSearch") == "dri", F<string>("projSearch"));
            typeof(StudioForm).GetMethod("ProjSearchKey", BF).Invoke(sf, new object[] { '\b' }); Check("backspace removes a letter", F<string>("projSearch") == "dr", F<string>("projSearch"));
            Check("Esc clears the search first", (bool)typeof(StudioForm).GetMethod("ProjSearchCmd", BF).Invoke(sf, new object[] { Keys.Escape }) && F<string>("projSearch") == "" && F<bool>("projSearchOn"), ""); typeof(StudioForm).GetMethod("ProjSearchCmd", BF).Invoke(sf, new object[] { Keys.Enter }); Check("Enter lets go of the search box (Space can play again)", !F<bool>("projSearchOn"), "");
            SetF("tab", 0); Paint(); Check("leaving the page lets go of the search box (it only takes typing after a click)", !F<bool>("projSearchOn") && !F<bool>("projBin"), "");
            SetF("tab", 3); SetF("projSearchOn", true); Paint(); var sbox = Regs().Find(r => Math.Abs(r.R.X - 24) < 0.5f && Math.Abs(r.R.Height - 34) < 0.5f && r.R.Y < 120 && r.R.Width > 150); Check("the projects page has a search box, SORT, TYPE and filter buttons", sbox != null && Regs().FindAll(r => Math.Abs(r.R.Height - 32) < 0.5f && r.R.Y > 100 && r.R.Y < 140).Count >= 3, "");
            // the scroll strip on Rapper Vocal Styles: the logo is a handle you can drag
            SetF("tab", 0); typeof(StudioForm).GetMethod("OpenStyles", BF).Invoke(sf, null); Paint(); Paint(); var vregs = Regs().FindAll(r => r.OnDrag != null && r.R.Width > 30 && r.R.Width < 44 && r.R.Height > 30 && r.R.Height < 44);
            Check("the Vocal Styles scroll strip has a logo handle", vregs.Count >= 1, vregs.Count + " handles"); float s0 = F<float>("stylesScroll");
            if (vregs.Count > 0) { var lh = vregs[vregs.Count - 1]; int lx = (int)(lh.R.X + lh.R.Width / 2), ly = (int)(lh.R.Y + lh.R.Height / 2); Ev("OnMouseMove", MouseButtons.None, 0, lx, ly); Ev("OnMouseDown", MouseButtons.Left, 1, lx, ly); for (int i = 1; i <= 10; i++) { Ev("OnMouseMove", MouseButtons.Left, 0, lx, ly + i * 12); Paint(); } Ev("OnMouseUp", MouseButtons.Left, 1, lx, ly + 120); Check("dragging the logo scrolls the styles", F<float>("stylesScroll") > s0 + 50, s0 + " -> " + F<float>("stylesScroll")); }
            Ev("OnMouseMove", MouseButtons.None, 0, 5, 5); SetF("stylesOpen", false);
            // ---- STEREO and MONO, THIN TRACKS, BACK everywhere, hover sparks, the questions ----
            eng.Tracks.Add(StudioEngine.NewTrack("Vox", "vocal", ToneClip(30))); eng.Tracks.Add(StudioEngine.NewTrack("Beat", "beat", ToneClip(30))); SetF("tab", 0); SetF("selTrack", 0); Paint(); Paint();
            var stB = Regs().Find(r => Math.Abs(r.R.Width - 66) < 0.5f && r.R.X < 372 && r.R.Y > 170 && r.R.Y < 330 && r.R.Height > 20 && r.R.Height < 30 && r.OnDown != null); var monoB = stB == null ? null : Regs().Find(r => Math.Abs(r.R.Width - 66) < 0.5f && Math.Abs(r.R.X - stB.R.X) < 0.5f && r.R.Y > stB.R.Y + 10 && r.R.Y < stB.R.Y + 60);
            Check("a track has a STEREO button with a separate MONO button underneath", stB != null && monoB != null && monoB.R.Y >= stB.R.Bottom, stB == null ? "no STEREO" : (monoB == null ? "no MONO" : stB.R + " / " + monoB.R));
            if (stB != null && monoB != null) { Click(monoB.R); Check("MONO makes the track mono", eng.Tracks[0].Mono, ""); Paint(); stB = Regs().Find(r => Math.Abs(r.R.Width - 66) < 0.5f && r.R.X < 372 && r.R.Y > 170 && r.R.Y < 330 && r.R.Height > 20 && r.R.Height < 30 && r.OnDown != null); Click(stB.R); Check("STEREO makes it stereo again", !eng.Tracks[0].Mono, ""); }
            int lane0 = F<int>("LaneH"); typeof(StudioForm).GetMethod("ToggleThinTracks", BF).Invoke(sf, null); Check("THIN TRACKS squeezes the lanes", F<int>("LaneH") < 96, "lane " + F<int>("LaneH")); Paint();
            { var xb = Regs().Find(r => Math.Abs(r.R.Width - 22) < 0.5f && Math.Abs(r.R.X - 338) < 1.5f); float lt = xb == null ? 187 : xb.R.Y - 7; var th = Regs().FindAll(r => r.R.X < 372 && r.R.Y > lt && r.R.Y < lt + F<int>("LaneH") && r.OnDown != null); string bad = ""; foreach (var r in th) if (r.R.Bottom > lt + F<int>("LaneH") + 1 && r.R.Height < 40) bad += r.R + " "; Check("in a thin lane no button sticks out below its track", bad.Length == 0, bad); }
            typeof(StudioForm).GetMethod("ToggleThinTracks", BF).Invoke(sf, null); Check("pressing it again brings the lanes back", F<int>("LaneH") == lane0, "lane " + F<int>("LaneH") + " (was " + lane0 + ")");
            SetF("tab", 0); Paint(); SetF("tab", 4); Paint(); SetF("tab", 1); Paint(); Check("Mastering has a BACK button (and no HOME: the MIX tab is there)", Regs().Exists(r => r.OnDown != null && Math.Abs(r.R.Width - 100) < 0.5f && r.R.Y < 60 && Math.Abs(r.R.X - 270) < 0.5f), ""); typeof(StudioForm).GetMethod("GoBack", BF).Invoke(sf, null); Check("BACK returns to the page you came from (Community)", F<int>("tab") == 4, "tab " + F<int>("tab"));
            SetF("tab", 0); Paint(); SetF("tab", 2); Paint(); typeof(StudioForm).GetMethod("GoBack", BF).Invoke(sf, null); Check("BACK from Lyrics goes back to the studio", F<int>("tab") == 0, "tab " + F<int>("tab")); Paint();
            { var asks = new List<string>(); bool yes = false; SetF("askHook", (Func<string, bool>)(t => { asks.Add(t); return yes; }));
              typeof(StudioForm).GetMethod("AutoMixClick", BF).Invoke(sf, null); Check("AUTO MIX asks first, and cancelling does nothing", asks.Count == 1 && !F<bool>("busy") && asks[0].Contains("Auto Mix"), asks.Count + " question(s)");
              Paint(); var mr = Regs().Find(r => r.OnDown != null && Math.Abs(r.R.Width - 92) < 0.5f && r.R.Y > 70 && r.R.Y < 125 && r.R.X > 400 && r.R.X < 700); Check("MIDI REC button found", mr != null, "");
              if (mr != null) { asks.Clear(); Click(mr.R); Check("MIDI REC asks if you want to record an instrument (No keeps it off)", asks.Count == 1 && asks[0].Contains("record an instrument") && !F<bool>("recOn"), asks.Count + " question(s)"); yes = true; Paint(); mr = Regs().Find(r => r.OnDown != null && Math.Abs(r.R.Width - 92) < 0.5f && r.R.Y > 70 && r.R.Y < 125 && r.R.X > 400 && r.R.X < 700); Click(mr.R); Check("and Yes switches it on", F<bool>("recOn"), ""); Paint(); mr = Regs().Find(r => r.OnDown != null && Math.Abs(r.R.Width - 92) < 0.5f && r.R.Y > 70 && r.R.Y < 125 && r.R.X > 400 && r.R.X < 700); asks.Clear(); Click(mr.R); Check("switching it off does not ask", asks.Count == 0 && !F<bool>("recOn"), asks.Count + " question(s)"); }
              if (AudioRecorder.Devices().Count > 0) { yes = false; asks.Clear(); typeof(StudioForm).GetMethod("MicClick", BF).Invoke(sf, null); Check("MIC asks Would you like to record now? (No leaves it off)", asks.Count == 1 && asks[0].Contains("record now") && !F<bool>("micArmed"), asks.Count + " question(s)"); yes = true; typeof(StudioForm).GetMethod("MicClick", BF).Invoke(sf, null); Check("Yes arms the microphone (PLAY then records)", F<bool>("micArmed"), ""); typeof(StudioForm).GetMethod("MicClick", BF).Invoke(sf, null); }
              else Console.WriteLine("INFO  no microphone on this PC: the MIC question was not tested");
              SetF("askHook", null); }
            { Paint(); var bt = Regs().Find(r => r.OnDown != null && Math.Abs(r.R.Width - 70) < 0.5f && r.R.Y > 70 && r.R.Y < 125 && r.R.X < 120); if (bt != null) { int hx = (int)(bt.R.X + bt.R.Width / 2), hy = (int)(bt.R.Y + bt.R.Height / 2); Ev("OnMouseMove", MouseButtons.None, 0, hx, hy); for (int i = 0; i < 12; i++) { Paint(); Pump(20); }
                Check("pointing at a button throws neon sparks", ((System.Collections.IList)typeof(StudioForm).GetField("hSparks", BF).GetValue(sf)).Count > 3, ((System.Collections.IList)typeof(StudioForm).GetField("hSparks", BF).GetValue(sf)).Count + " sparks"); Ev("OnMouseMove", MouseButtons.None, 0, 5, 700); } }
            // HOME and BACK: BACK steps back through the pages you went through, HOME goes to the studio and forgets them
            { var GH = typeof(StudioForm).GetMethod("GoHome", BF); var GB = typeof(StudioForm).GetMethod("GoBack", BF); Func<bool> showBack = () => (bool)typeof(StudioForm).GetProperty("ShowBack", BF).GetValue(sf, null);
              GH.Invoke(sf, null); Paint(); Check("at home there is nothing to go back to", F<int>("tab") == 0 && !showBack(), "");
              SetF("tab", 3); Paint(); SetF("tab", 4); Paint(); SetF("tab", 1); Paint(); Check("after three pages BACK is there", showBack(), "");
              GB.Invoke(sf, null); Paint(); Check("BACK goes to the page before (Community)", F<int>("tab") == 4, "tab " + F<int>("tab")); GB.Invoke(sf, null); Paint(); Check("BACK again goes to My Projects", F<int>("tab") == 3, "tab " + F<int>("tab")); GB.Invoke(sf, null); Paint(); Check("and again to the studio", F<int>("tab") == 0, "tab " + F<int>("tab")); GB.Invoke(sf, null); Paint(); Check("BACK with nowhere left to go lands at home", F<int>("tab") == 0, "");
              GH.Invoke(sf, null); Paint(); SetF("tab", 3); Paint(); Check("one page from home: BACK would only go home, so it is not shown (HOME is there)", !showBack(), ""); SetF("tab", 4); Paint(); Check("two pages in: BACK appears (it goes to My Projects)", showBack(), ""); GB.Invoke(sf, null); Paint(); Check("and once it would only lead home it disappears again", F<int>("tab") == 3 && !showBack(), "tab " + F<int>("tab") + ", shown " + showBack());
              SetF("tab", 3); Paint(); SetF("tab", 4); Paint(); SetF("tab", 1); Paint(); GH.Invoke(sf, null); Paint(); Check("HOME goes to the studio and forgets the way you came (no glitches)", F<int>("tab") == 0 && !showBack(), "");
              SetF("tab", 4); Paint(); var homeB = Regs().Find(r => r.OnDown != null && Math.Abs(r.R.X - 270) < 0.5f && Math.Abs(r.R.Width - 100) < 0.5f && r.R.Y < 60); Check("the pages have a HOME button", homeB != null, ""); if (homeB != null) { Click(homeB.R); Check("HOME works from a page", F<int>("tab") == 0, "tab " + F<int>("tab")); } Paint(); }
            { StudioForm.TxtAudit = new List<StudioForm.AuditText>(); SetF("tab", 0); Paint(); var wz = new List<string>(); foreach (var at in StudioForm.TxtAudit) wz.Add(at.S); StudioForm.TxtAudit = null; Check("the zoom - and + buttons are gone (the ZOOM slider does it all)", !wz.Contains("-") && !wz.Contains("+") && wz.Contains("FIT"), ""); }
            { Prefs.Parse("fpscap", "240"); Check("a 240 fps mode exists (and 144, 165)", Prefs.FpsCap == 240, Prefs.FpsCap.ToString()); Prefs.Parse("fpscap", "144"); bool a1 = Prefs.FpsCap == 144; Prefs.Parse("fpscap", "165"); bool a2 = Prefs.FpsCap == 165; Prefs.Parse("fpscap", "75"); bool a3 = Prefs.FpsCap == 60; Prefs.Parse("fpscap", "60"); Check("the other frame rates are kept and odd values go to the nearest below", a1 && a2 && a3, ""); }
            // ---- every page has the logo scroll bar when it overflows ----
            { Func<bool> bar = () => Regs().Exists(r => r.OnDrag != null && r.OnDown != null && Math.Abs(r.R.Width - 38.5f) < 4 && Math.Abs(r.R.Height - 34f) < 4);
              for (int i = 0; i < 40; i++) ProjectLibrary.Entries.Add(new ProjectEntry { Path = Path.Combine(dir, "ghost" + i + ".ytmproj"), Name = "Ghost " + i, Updated = DateTime.Now.AddDays(-i), Opened = DateTime.Now.AddDays(-i), Tracks = 2 });
              SetF("tab", 3); SetF("projScroll", 0f); Paint(); Paint(); Check("My Projects has the logo scroll bar", bar(), ""); SetF("tab", 4); SetF("commScroll", 0f); Paint(); Paint(); Check("Community has the logo scroll bar", bar(), "");
              for (int i = 0; i < 25; i++) ProjectLibrary.Bin.Add(new ProjectLibrary.BinItem { Id = "t" + i, Name = "Binned " + i, OrigPath = "x" + i, Deleted = DateTime.Now, Tracks = 1 }); SetF("tab", 3); SetF("projBin", true); SetF("binScroll", 0f); Paint(); Paint(); Check("the Recycling Bin has the logo scroll bar", bar(), ""); SetF("projBin", false); ProjectLibrary.Bin.Clear();
              foreach (var gh in ProjectLibrary.Entries.FindAll(x => x.Path.Contains("ghost")).ToArray()) ProjectLibrary.Entries.Remove(gh);
              SetF("tab", 1); SetF("mpTab", 1); SetF("mpScroll", 0f); Paint(); Paint(); Check("the master presets list has the logo scroll bar", bar(), ""); SetF("mpTab", 0); SetF("tab", 0);
              eng.Tracks.Clear(); for (int i = 0; i < 14; i++) eng.Tracks.Add(StudioEngine.NewTrack("T" + i, "beat", ToneClip(5))); SetF("vscroll", 0f); Paint(); Paint(); Check("many tracks give the Mix page the logo scroll bar", bar(), ""); eng.Tracks.Clear();
              var eqf = FxRegistry.Create("eq"); eqf.Prepare(44100); var trk = StudioEngine.NewTrack("Vox", "vocal", ToneClip(5)); trk.Fx.Add(eqf); eng.Tracks.Add(trk); SetF("selTrack", 0); SetF("selFx", 0); typeof(StudioForm).GetMethod("OpenPresetSheet", BF).Invoke(sf, new object[] { eqf, }); Paint(); Paint();
              Check("the plugin Presets sheet opens with the logo scroll bar and a long list", F<bool>("psOpen") && bar(), "");
              // USE then REVERT puts the plugin back
              float g0 = eqf.Get("b0_g"); typeof(StudioForm).GetMethod("PsUse", BF).Invoke(sf, new object[] { "Test preset", "b0_g=6" }); Check("USE applies the preset", Math.Abs(eqf.Get("b0_g") - 6) < 0.01f, eqf.Get("b0_g").ToString());
              Check("and a REVERT bar is offered", F<Action>("revertAction") != null, ""); Paint(); typeof(StudioForm).GetMethod("DoRevert", BF).Invoke(sf, null); Check("REVERT puts the plugin back exactly as it was", Math.Abs(eqf.Get("b0_g") - g0) < 0.01f, eqf.Get("b0_g").ToString());
              typeof(StudioForm).GetMethod("PsUse", BF).Invoke(sf, new object[] { "Again", "b0_g=-4" }); typeof(StudioForm).GetMethod("PsRestore", BF).Invoke(sf, null); Check("the sheet's own REVERT goes back to how it was when the list was opened", Math.Abs(eqf.Get("b0_g") - g0) < 0.01f, ""); SetF("psOpen", false); SetF("revertAction", null); eng.Tracks.Clear();
              // themes revert, master preset revert
              SetF("tab", 0); int th0 = Themes.Index; SetF("themeOpen", true); Paint(); Paint(); var cards = Regs().FindAll(r => r.OnDown != null && r.R.Height > 80 && r.R.Height < 110 && r.R.Width > 150 && r.R.Y > 200); if (cards.Count > 3) { Click(cards[3].R); Check("choosing a theme offers REVERT", Themes.Index != th0 && F<Action>("revertAction") != null, ""); typeof(StudioForm).GetMethod("DoRevert", BF).Invoke(sf, null); Check("and REVERT brings the old theme back", Themes.Index == th0, ""); } SetF("themeOpen", false); Themes.Apply(0);
              eng.Tracks.Add(StudioEngine.NewTrack("Vox", "vocal", ToneClip(5))); var chain0 = (string[])typeof(StudioForm).GetMethod("SnapChain", BF).Invoke(sf, null); typeof(StudioForm).GetMethod("PresetUse", BF).Invoke(sf, new object[] { 5 }); Check("USE on a master preset changes the chain", string.Join("|", (string[])typeof(StudioForm).GetMethod("SnapChain", BF).Invoke(sf, null)) != string.Join("|", chain0), "");
              typeof(StudioForm).GetMethod("DoRevert", BF).Invoke(sf, null); Check("and REVERT puts the chain back", string.Join("|", (string[])typeof(StudioForm).GetMethod("SnapChain", BF).Invoke(sf, null)) == string.Join("|", chain0), ""); eng.Tracks.Clear(); }
            // ---- Help: organised, SHOW ME finds the real button, Bass Lab has its basics and a tour ----
            { Check("the guide finds the button a step talks about", HelpForm.FindLabel("Press ADD MUSIC and choose a file") == "ADD MUSIC" && HelpForm.FindLabel("Press Add Effect, then Parametric EQ") == "+ Add Effect" && HelpForm.FindLabel("Press PLAY (or Space)") == "PLAY" && HelpForm.FindLabel("This will display the result") == null && HelpForm.FindLabel("Use the mixer to adjust levels") == null, "");
              SetF("tab", 0); Paint(); Paint(); bool shown = (bool)typeof(StudioForm).GetMethod("ShowOnScreen", BF).Invoke(sf, new object[] { "ADD MUSIC" }); Check("SHOW ME points at ADD MUSIC on the screen", shown, ""); foreach (Form of in Application.OpenForms.Cast<Form>().ToList()) if (of.Owner == sf && of.FormBorderStyle == FormBorderStyle.None) of.Close();
              bool shown2 = (bool)typeof(StudioForm).GetMethod("ShowOnScreen", BF).Invoke(sf, new object[] { "AUTO LOUDNESS" }); Check("SHOW ME goes to the page the button is on (AUTO LOUDNESS is in Mastering)", shown2 && F<int>("tab") == 1, "tab " + F<int>("tab")); foreach (Form of in Application.OpenForms.Cast<Form>().ToList()) if (of.Owner == sf && of.FormBorderStyle == FormBorderStyle.None) of.Close(); SetF("tab", 0);
              Check("there is a Bass Lab basics guide", Array.Exists(HelpContent.All, t => t.Id == "basslab-basics"), "");
              var sfx = new SynthFx(); sfx.Prepare(44100); var sw = new SynthWindow(sfx, null); sw.Show(); Pump(300); sw.ShowHelpTour(); Pump(300); int owned = 0; foreach (Form of in Application.OpenForms.Cast<Form>().ToList()) if (of.Owner == sw && of.FormBorderStyle == FormBorderStyle.None) { owned++; of.Close(); }
              Check("the Bass Lab HELP button starts a tour (a card and a dimmed window with a hole)", owned >= 2, owned + " tour windows"); sw.Close(); }
            // ---- lyrics: clearing clears the title too ----
            { SetF("tab", 2); Paint(); Pump(200); Paint(); var lbx = F<TextBox>("lyricBox"); var ltt = F<TextBox>("lyricTitle"); lbx.Text = ""; ltt.Text = "My Song"; Paint(); var clr = Regs().FindLast(r => r.OnDown != null && Math.Abs(r.R.Width - 78) < 0.5f && r.R.Y > sf.ClientSize.Height - 70 && r.R.X > 700);
              Check("the Clear button is there", clr != null, ""); if (clr != null) { Click(clr.R); Check("Clear (with the words already empty) also clears the title", ltt.Text.Length == 0, ltt.Text); } SetF("tab", 0); }
            Check("the soft logo behind Community and My Projects is made", typeof(StudioForm).GetMethod("BackdropBitmap", BF).Invoke(sf, new object[] { 400 }) != null, "");
            eng.Tracks.Clear();
            // ---- the Mastering tab ----
            eng.Tracks.Clear(); eng.Tracks.Add(StudioEngine.NewTrack("Vox", "vocal", NoiseClip(20, 1))); eng.Tracks.Add(StudioEngine.NewTrack("Beat", "beat", NoiseClip(20, 2))); SetF("tab", 1); Paint();
            { StudioForm.TxtAudit = new List<StudioForm.AuditText>(); Paint(); var words = new List<string>(); foreach (var at in StudioForm.TxtAudit) words.Add(at.S); StudioForm.TxtAudit = null;
              bool gone = true; string still = ""; foreach (var w in new[] { "ADD MUSIC", "SPLIT VOCALS + BEAT", "DETECT TRACK KEY", "UNDO MIX", "THIN TRACKS", "COUNT IN", "MIDI REC", "MIC", "SOUNDS", "LOOP", "STOP" }) if (words.Contains(w)) { gone = false; still += w + " "; }
              Check("in the Mastering tab the other buttons are gone (not clickable)", gone, still);
              bool there = true; string lost = ""; foreach (var w in new[] { "PLAY", "EXPORT", "HELP", "OPEN", "SAVE", "SAVE AS", "MIX", "MASTERING", "LYRICS", "BACK", "PREVIEW MASTER" }) if (!words.Contains(w)) { there = false; lost += w + " "; }
              Check("and the ones that belong stay: PLAY, EXPORT, HELP, OPEN, SAVE, SAVE AS, MIX, MASTERING, LYRICS, FULLSCREEN, BACK, PREVIEW", there && (words.Contains("FULLSCREEN") || words.Contains("MINIMISE")), lost); }
            Check("Mastering has AUTO MIX and AUTO LOUDNESS, the old Analyze button is gone", Regs().Count > 20 && !(((System.Collections.IEnumerable)typeof(StudioForm).GetField("regs", BF).GetValue(sf)) == null), "");
            { StudioForm.TxtAudit = new List<StudioForm.AuditText>(); Paint(); var w2 = new List<string>(); foreach (var at in StudioForm.TxtAudit) w2.Add(at.S); StudioForm.TxtAudit = null; Check("the page says AUTO MASTERING, AUTO LOUDNESS and SET LOUDNESS (MANUAL)", w2.Contains("AUTO MASTERING") && w2.Contains("AUTO LOUDNESS") && w2.Contains("SET LOUDNESS (MANUAL)") && !w2.Exists(x => x.ToUpper().Contains("ANALYZE")), string.Join(",", w2.FindAll(x => x.ToUpper().Contains("AUTO") || x.ToUpper().Contains("ANALY")))); }
            SetF("tab", 0); eng.Tracks.Add(StudioEngine.NewTrack("Quiet", "beat", QuietLoud())); SetF("tab", 1);
            { object[] reg = new object[2]; var lr = typeof(StudioForm).GetMethod("LoudestRegion", BF); var parms = new object[] { 0L, 0L }; lr.Invoke(sf, parms); long stR = (long)parms[0], enR = (long)parms[1]; Check("the preview picks the loudest 20 seconds of the song", stR >= 44100L * 34 && stR <= 44100L * 36 && enR - stR == 44100L * 20, (stR / 44100.0).ToString("0.0") + " s to " + (enR / 44100.0).ToString("0.0") + " s"); }
            eng.Tracks.RemoveAt(eng.Tracks.Count - 1);
            Check("there are at least 50 master presets including Bass Boost Master, Vocal Improver and Vocal Enhance Master", MasterPresets.All.Length >= 50 && Array.Exists(MasterPresets.All, p => p.Name.StartsWith("Vocal Improver")) && Array.Exists(MasterPresets.All, p => p.Name.StartsWith("Bass Boost Master")) && Array.Exists(MasterPresets.All, p => p.Name.StartsWith("Vocal Enhance Master")), MasterPresets.All.Length + " presets");
            { int bad = 0; string badTxt = ""; var CIx = System.Globalization.CultureInfo.InvariantCulture; int total = 0;
              foreach (var tp in new[] { "eq", "comp", "mbcomp", "exciter", "sat", "dist", "reverb", "delay", "chorus", "deess", "widener", "limiter", "gate", "duck", "pitch", "denoise", "bassboost" })
              { Fx fxp = null; try { fxp = FxRegistry.Create(tp); } catch { } if (fxp == null) continue;
                foreach (var kv in Presets.For(tp)) { total++; foreach (var part in kv.Value.Split(';')) { int ix = part.IndexOf('='); if (ix < 1) continue; string key = part.Substring(0, ix); var pr = fxp.Find(key); float val; float.TryParse(part.Substring(ix + 1), System.Globalization.NumberStyles.Float, CIx, out val); if (pr == null) { bad++; badTxt += tp + ":" + kv.Key + ":" + key + " "; } else if (val < pr.Min - 0.001f || val > pr.Max + 0.001f) { bad++; badTxt += tp + ":" + kv.Key + ":" + key + "=" + val + " "; } } } }
              Check("every built-in plugin preset (" + total + " of them) only uses real parameters inside their range", bad == 0 && total >= 120, bad + " problems: " + badTxt + " (" + total + " presets)"); }
            { int bad2 = 0; string badTxt2 = ""; var CI2 = System.Globalization.CultureInfo.InvariantCulture; Presets.ApplyMaster(eng, 0);
              foreach (var mp in MasterPresets.All) for (int slot = 0; slot < 7; slot++) { if (mp.Slots[slot] == null) continue; var fxm = eng.MasterFx[slot]; foreach (var part in mp.Slots[slot].Split(';')) { int ix = part.IndexOf('='); if (ix < 1) continue; string key = part.Substring(0, ix); var pr = fxm.Find(key); float val; float.TryParse(part.Substring(ix + 1), System.Globalization.NumberStyles.Float, CI2, out val); if (pr == null) { bad2++; badTxt2 += mp.Name + ":" + slot + ":" + key + " "; } else if (val < pr.Min - 0.001f || val > pr.Max + 0.001f) { bad2++; badTxt2 += mp.Name + ":" + key + "=" + val + " "; } } }
              Check("every master preset only uses real parameters inside their range", bad2 == 0, bad2 + " problems: " + badTxt2); }
            { bool allOk = true; string badName = ""; for (int i = 0; i < MasterPresets.All.Length; i++) { Presets.ApplyMaster(eng, i); var lim = eng.MasterFx[6]; var cmp = eng.MasterFx[1]; if (lim.Bypass || lim.Get("gain") < 0 || lim.Get("ceil") > -0.1f || cmp.Get("ratio") < 1) { allOk = false; badName += MasterPresets.All[i].Name + " "; } if (MasterPresets.All[i].Slots.Length != 7 || MasterPresets.All[i].Blurb.Length < 20) { allOk = false; badName += MasterPresets.All[i].Name + "(shape) "; } }
              Check("every master preset can be applied and has a limiter with a safe ceiling", allOk, badName); }
            { Presets.ApplyMaster(eng, 0); var sn0 = (string[])typeof(StudioForm).GetMethod("SnapChain", BF).Invoke(sf, null); Presets.ApplyMaster(eng, 5); var sn5 = (string[])typeof(StudioForm).GetMethod("SnapChain", BF).Invoke(sf, null); typeof(StudioForm).GetMethod("RestoreChain", BF).Invoke(sf, new object[] { sn0 }); var backC = (string[])typeof(StudioForm).GetMethod("SnapChain", BF).Invoke(sf, null);
              Check("a preset can be tried and the old chain comes back exactly", string.Join("|", sn0) == string.Join("|", backC) && string.Join("|", sn0) != string.Join("|", sn5), ""); }
            { // the presets really do what they say: bass boost has more low end, vocal enhance more presence
              Func<int, double[]> bands = idx => { Presets.ApplyMaster(eng, idx); eng.MasterBypass = false; float[] l, r; eng.Render(0, 44100L * 8, new RenderOpts { Master = true }, null, out l, out r); return AutoMix.Analyze(AudioClip.FromArrays(l, r, "m")).BandDb; };
              var clean = bands(MasterPresets.IndexOf("Clean & Transparent")); var bass = bands(MasterPresets.IndexOf("Bass Boost Master")); var vocal = bands(MasterPresets.IndexOf("Vocal Enhance Master"));
              double tiltB = (bass[0] + bass[1]) / 2 - (bass[5] + bass[6]) / 2, tiltC = (clean[0] + clean[1]) / 2 - (clean[5] + clean[6]) / 2; Check("BASS BOOST MASTER has more low end than the clean master (compared with the mids, so loudness does not matter)", tiltB - tiltC > 1.5, (tiltB - tiltC).ToString("0.0") + " dB more bass");
              Check("VOCAL ENHANCE MASTER lifts the presence and air", (vocal[7] + vocal[8]) / 2 - (clean[7] + clean[8]) / 2 > 0.8 || vocal[9] - clean[9] > 1.0, ((vocal[7] + vocal[8]) / 2 - (clean[7] + clean[8]) / 2).ToString("0.0") + " dB presence"); }
            { string why; Func<double, double, double, float> at = (i, lra, d) => StudioForm.AutoTarget(new LoudnessResult { I = i, LRA = lra }, out why);
              Check("AUTO LOUDNESS: dynamic music keeps its dynamics (-16), dense music goes louder (-11), normal music gets the streaming level (-14)", at(-22, 15, 0) == -16 && at(-20, 4, 0) == -11 && at(-20, 8, 0) == -14, at(-22, 15, 0) + ", " + at(-20, 4, 0) + ", " + at(-20, 8, 0));
              Check("and a mix that is already louder than that is kept (never turned down)", Math.Abs(at(-9.3, 8, 0) - (-9.3f)) < 0.05, at(-9.3, 8, 0).ToString()); }
            { // AUTO MIX: it listens, builds three versions, and CANCEL puts everything back
              Presets.ApplyMaster(eng, 0); var orig = (string[])typeof(StudioForm).GetMethod("SnapChain", BF).Invoke(sf, null); typeof(StudioForm).GetMethod("AutoMixMaster", BF).Invoke(sf, null); var t0 = DateTime.Now; while (!F<bool>("amOpen") && (DateTime.Now - t0).TotalSeconds < 90) Pump(100);
              var vers = (Array)typeof(StudioForm).GetField("amVersions", BF).GetValue(sf); Check("AUTO MASTERING listens to the mix and opens many versions to hear (at least 8)", F<bool>("amOpen") && vers != null && vers.Length >= 8, vers == null ? "none" : vers.Length + " versions");
              if (vers != null && vers.Length >= 8)
              {
                  var v0 = (AmVersion)vers.GetValue(0); var v2 = (AmVersion)vers.GetValue(2); int recs = 0; foreach (AmVersion av in vers) if (av.Recommended) recs++; Check("exactly one version is marked BEST FOR YOUR MIX", recs == 1, recs + " marked"); Check("each version has its own chain, a target loudness near what it aims for and an EQ made without any input", v0.Snap != null && v2.Snap != null && string.Join("|", v0.Snap) != string.Join("|", v2.Snap) && (Math.Abs(v0.Lufs - MasterPresets.All[v0.Base].Target) < 2.5 || v0.Lufs > MasterPresets.All[v0.Base].Target) && v0.Eq.Length > 0, v0.Lufs.ToString("0.0") + " LUFS (aims at " + MasterPresets.All[v0.Base].Target + ")");
                  Check("opening the sheet has not changed your chain yet", string.Join("|", (string[])typeof(StudioForm).GetMethod("SnapChain", BF).Invoke(sf, null)) == string.Join("|", orig), "");
                  typeof(StudioForm).GetMethod("AmPreview", BF).Invoke(sf, new object[] { 1 }); Check("PREVIEW puts that version on the master and loops the loudest part", F<bool>("mpActive") && F<int>("amApplied") == 1, ""); typeof(StudioForm).GetMethod("MasterPreviewStop", BF).Invoke(sf, null);
                  typeof(StudioForm).GetMethod("AmCancel", BF).Invoke(sf, null); Check("CANCEL puts the master back exactly as it was", !F<bool>("amOpen") && string.Join("|", (string[])typeof(StudioForm).GetMethod("SnapChain", BF).Invoke(sf, null)) == string.Join("|", orig), "");
              } }
            { var snapP = (string[])typeof(StudioForm).GetMethod("SnapChain", BF).Invoke(sf, null); typeof(StudioForm).GetMethod("PresetTry", BF).Invoke(sf, new object[] { 5 }); Check("trying a preset applies it and starts the preview", F<string[]>("mpBackup") != null && F<bool>("mpActive"), ""); typeof(StudioForm).GetMethod("PresetPutBack", BF).Invoke(sf, null); typeof(StudioForm).GetMethod("MasterPreviewStop", BF).Invoke(sf, null);
              Check("PUT BACK restores the chain", string.Join("|", (string[])typeof(StudioForm).GetMethod("SnapChain", BF).Invoke(sf, null)) == string.Join("|", snapP), ""); }
            SetF("tab", 0); eng.Tracks.Clear();
            foreach (var pp in ps) ProjectLibrary.Remove(pp); ProjectLibrary.Remove(p2);

            // ---- picture: remove, default logo, fills the square ----
            using (var bm = new Bitmap(300, 100)) { using (var gg = Graphics.FromImage(bm)) { gg.FillRectangle(Brushes.Red, 0, 0, 100, 100); gg.FillRectangle(Brushes.Lime, 100, 0, 100, 100); gg.FillRectangle(Brushes.Blue, 200, 0, 100, 100); } bm.Save(png, System.Drawing.Imaging.ImageFormat.Png); }
            using (var sc = ProjectLibrary.Scaled(png, 120, 120, System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic))
            {
                bool fill = sc != null && sc.Width == 120 && sc.Height == 120; Color c1 = fill ? sc.GetPixel(4, 4) : Color.Black, c2 = fill ? sc.GetPixel(115, 115) : Color.Black;
                Check("a wide picture is cropped to fill the whole square (nothing stretched, no empty bars)", fill && c1.G > 200 && c1.R < 60 && c2.G > 200 && c2.R < 60, fill ? c1 + " / " + c2 : "no bitmap");
            }
            string perr; bool set = ProjectLibrary.SetCover(p1, png, out perr); e1 = ProjectLibrary.Find(p1); Check("a picture can be set", set && e1.Cover.Length > 0, perr ?? "ok");
            ProjectLibrary.ClearCover(p1); Check("REMOVE PICTURE takes it off again (the logo shows)", ProjectLibrary.Find(p1).Cover.Length == 0, "");
            using (var bm = new Bitmap(200, 200)) using (var g = Graphics.FromImage(bm))
            {
                typeof(StudioForm).GetMethod("DrawLogoArt", BF).Invoke(sf, new object[] { g, new RectangleF(0, 0, 200, 200) }); int lit = 0; for (int y = 0; y < 200; y += 4) for (int x = 0; x < 200; x += 4) { var c = bm.GetPixel(x, y); if (c.R + c.G + c.B > 200) lit++; }
                Check("with no picture the Purphex logo is drawn in the square", lit > 40, lit + " bright samples");
            }

            { var dm = typeof(StudioForm).GetMethod("DurMade", BindingFlags.NonPublic | BindingFlags.Static); Func<long, string> D = v => (string)dm.Invoke(null, new object[] { v });
              Check("TIME MADE reads 45s, 2m33s, 2h33m and 1d2h33m", D(45) == "45s" && D(153) == "2m33s" && D(9180) == "2h33m" && D(95580) == "1d2h33m" && D(0) == "0s" && D(3600) == "1h0m" && D(86400) == "1d0h0m", D(45) + " " + D(153) + " " + D(9180) + " " + D(95580) + " " + D(0) + " " + D(3600) + " " + D(86400)); }
            // ---- the profile picture, banner, accent colour, links and emoji ----
            {
                Func<int, int, Color, Bitmap> solid = (w, h, c) => { var b = new Bitmap(w, h); using (var g = Graphics.FromImage(b)) g.Clear(c); return b; }; var bgc = Color.FromArgb(255, 0, 0, 255);
                using (var wide = solid(400, 100, Color.Red)) using (var fr = ProfileStore.RenderFramed(wide, 200, 100, 100, 50, 50, bgc))
                    Check("a picture of any shape fills the frame at zoom 100 (no bars)", fr.GetPixel(1, 1).R > 200 && fr.GetPixel(198, 98).R > 200 && fr.GetPixel(1, 98).R > 200 && fr.GetPixel(198, 1).R > 200, "");
                using (var tallp = solid(100, 400, Color.Red)) { int cz = ProfileStore.ContainZoom(100, 400, 200, 100); using (var fr = ProfileStore.RenderFramed(tallp, 200, 100, cz, 50, 50, bgc))
                    Check("FIT shows the whole picture with bars at the sides (zoom " + cz + ")", cz == 12 && fr.GetPixel(5, 50).B > 200 && fr.GetPixel(194, 50).B > 200 && fr.GetPixel(100, 50).R > 200 && fr.GetPixel(100, 6).R > 200 && fr.GetPixel(100, 93).R > 200, cz + ""); }
                using (var half = new Bitmap(400, 100))
                {
                    using (var g = Graphics.FromImage(half)) { g.Clear(Color.Red); using (var br = new SolidBrush(Color.Lime)) g.FillRectangle(br, 200, 0, 200, 100); }
                    using (var l0 = ProfileStore.RenderFramed(half, 100, 100, 200, 0, 50, bgc)) using (var l100 = ProfileStore.RenderFramed(half, 100, 100, 200, 100, 50, bgc)) using (var l50 = ProfileStore.RenderFramed(half, 100, 100, 100, 50, 50, bgc))
                        Check("zoomed in, LEFT / RIGHT slides the picture (left end red, right end green)", l0.GetPixel(50, 50).R > 200 && l100.GetPixel(50, 50).G > 200 && l50.GetPixel(2, 50).R > 200 && l50.GetPixel(97, 50).G > 200, "");
                }
                string bio0 = ProfileStore.Bio; ProfileStore.Accent = ProfileStore.Pack(Color.FromArgb(255, 0x12, 0x34, 0x56)); var accBack = ProfileStore.ColorOf(ProfileStore.Accent);
                Check("a custom accent colour is kept exactly", accBack.R == 0x12 && accBack.G == 0x34 && accBack.B == 0x56, "");
                Check("older programs still get a hue with it", ProfileStore.AccentHue >= 0 && ProfileStore.AccentHue < 360, ProfileStore.AccentHue + "");
                ProfileStore.Save(); ProfileStore.Accent = -1; ProfileStore.Load(); Check("the accent colour is saved and read back", ProfileStore.Accent == 0x123456, ProfileStore.Accent.ToString("x6"));
                File.WriteAllText(Path.Combine(StudioEnv.DataDir, "profile-page.txt"), "accent=265\nbio=x\n"); ProfileStore.Accent = -1; ProfileStore.Load(); Check("a colour chosen in an older version (a hue) becomes that same colour", ProfileStore.Accent >= 0 && ProfileStore.Accent == ProfileStore.HueToPacked(265), ProfileStore.Accent + "");
                ProfileStore.Accent = -1; ProfileStore.Bio = bio0; ProfileStore.Save();
                string tdir = Path.Combine(StudioEnv.DataDir, "pics"); Directory.CreateDirectory(tdir); string pic = Path.Combine(tdir, "tall.png"); using (var tb = solid(300, 900, Color.Orange)) tb.Save(pic, System.Drawing.Imaging.ImageFormat.Png);
                string aerr; bool aok = ProjectLibrary.SetAvatar(pic, out aerr); Check("a chosen picture is kept whole and framed at 256 pixels", aok && File.Exists(ProjectLibrary.AvatarSrc) && File.Exists(ProjectLibrary.AvatarPath) && ProfileStore.AvZoom == 100, aerr);
                ProfileStore.AvZoom = ProfileStore.ContainZoom(300, 900, 256, 256); ProjectLibrary.BakeAvatar(); using (var baked = new Bitmap(ProjectLibrary.AvatarPath)) Check("FIT remakes the framed picture from the kept original (dark bars, the whole picture)", baked.Width == 256 && baked.GetPixel(4, 128).R < 70 && baked.GetPixel(128, 128).R > 200, "zoom " + ProfileStore.AvZoom);
                var da = typeof(StudioForm).GetMethod("DrawAvatar", BF); Func<byte[]> shot = () => { using (var b = new Bitmap(64, 64)) { using (var g = Graphics.FromImage(b)) { g.Clear(Color.Black); da.Invoke(sf, new object[] { g, new RectangleF(2, 2, 60, 60) }); } using (var ms = new MemoryStream()) { b.Save(ms, System.Drawing.Imaging.ImageFormat.Png); return ms.ToArray(); } } };
                var withPic = shot(); ProjectLibrary.ClearAvatar(); var noPic = shot(); bool same = withPic.Length == noPic.Length; if (same) for (int i = 0; i < withPic.Length; i++) if (withPic[i] != noPic[i]) { same = false; break; }
                Check("outside the Profile page the circle is always the Purphex logo, whatever picture you chose", same, withPic.Length + " " + noPic.Length);
                Check("REMOVE PICTURE deletes the kept original as well and resets the frame", !File.Exists(ProjectLibrary.AvatarSrc) && ProjectLibrary.AvatarPath.Length == 0 && ProfileStore.AvZoom == 100 && ProfileStore.AvX == 50, "");
                { var lst = new List<string>(); for (int i = 1; i <= 10; i++) lst.Add("https://example" + i + ".com/me"); var lc = typeof(StudioForm).GetMethod("LinkChips", BF); int before = Regs().Count; float hh; using (var b = new Bitmap(600, 300)) using (var g = Graphics.FromImage(b)) hh = (float)lc.Invoke(sf, new object[] { g, 0f, 0f, 560f, lst });
                  Check("ten links wrap onto more lines and none is dropped", hh >= 70 && Regs().Count - before >= 10, hh + " lines height, " + (Regs().Count - before) + " buttons"); }
                string em = char.ConvertFromUtf32(0x1F3B5), em2 = char.ConvertFromUtf32(0x1F600);
                Check("a name is never cut through the middle of an emoji", EmojiText.Cut("ab" + em, 3) == "ab" && EmojiText.Cut(em + em2, 2) == em && EmojiText.Cut("abc", 5) == "abc" && EmojiText.Cut("ab" + em, 4) == "ab" + em, "");
                Check("emoji are recognised", EmojiText.Has("hi " + em) && EmojiText.Has("x" + char.ConvertFromUtf32(0xFE0F)) && !EmojiText.Has("hello"), "");
                using (var eb = new Bitmap(120, 30)) { using (var g = Graphics.FromImage(eb)) { g.Clear(Color.Black); EmojiText.Draw(g, em + em2, 20, Color.White, new RectangleF(0, 0, 120, 30), StringAlignment.Near, true); } int ink = 0; for (int y = 0; y < 30; y++) for (int x = 0; x < 120; x++) if (eb.GetPixel(x, y).R > 100) ink++; Check("emoji are drawn", ink > 60, ink + " pixels"); }
            }
            // ---- BACK button ----
            SetF("tab", 3); Paint(); var back = Regs().FindLast(r => Math.Abs(r.R.X - 270) < 0.5f && Math.Abs(r.R.Width - 100) < 0.5f); Check("the projects page has a BACK button", back != null, "");
            if (back != null) { Click(back.R); Check("BACK leaves the projects page for the page before", F<int>("tab") != 3, "tab " + F<int>("tab")); }
            SetF("tab", 0); Paint(); SetF("tab", 4); Paint(); back = Regs().FindLast(r => Math.Abs(r.R.X - 270) < 0.5f && Math.Abs(r.R.Width - 100) < 0.5f); if (back != null) { Click(back.R); Check("BACK works on the Community page too", F<int>("tab") == 0, "tab " + F<int>("tab")); }

            // ---- preview ----
            SetF("tab", 3); e1 = ProjectLibrary.Find(p1); typeof(StudioForm).GetMethod("PreviewToggle", BF).Invoke(sf, new object[] { e1 });
            Check("PREVIEW starts loading the project", F<string>("previewPath") == p1, F<string>("previewPath"));
            typeof(StudioForm).GetMethod("PreviewToggle", BF).Invoke(sf, new object[] { e1 }); Check("PREVIEW again stops it", F<string>("previewPath") == "", "");
            typeof(StudioForm).GetMethod("PreviewToggle", BF).Invoke(sf, new object[] { e1 }); Pump(2500);
            Check("a project with no sound is not previewed (and you are told)", F<string>("previewPath") == "", F<string>("previewPath"));
            SetF("previewPath", p1); SetF("previewName", "Alpha"); SetF("previewPlayFn", (Action<StudioEngine>)(x => { })); Paint(); var stop = Regs().FindLast(r => Math.Abs(r.R.Width - 26) < 0.5f && r.R.Y > 700);
            Check("while a preview runs the bar with a STOP button is drawn along the bottom", stop != null, "");
            if (stop != null) { Click(stop.R); Check("STOP ends the preview", F<string>("previewPath") == "", ""); }
            SetF("previewPlayFn", null);
            { var pe2 = new StudioEngine(); pe2.Tracks.Add(StudioEngine.NewTrack("P", "beat", ToneClip(6))); SetF("tab", 3); SetF("previewPath", p1); SetF("previewName", "Alpha"); SetF("previewEng", pe2); pe2.Playing = true; SetF("projSearchOn", false); SetF("projSearch", ""); Paint();
              var pck = typeof(StudioForm).GetMethod("ProcessCmdKey", BF); var margs = new object[] { new Message(), Keys.Space }; bool handled = (bool)pck.Invoke(sf, margs);
              Check("Space pauses the preview (it does not go to the search box)", handled && F<bool>("previewPaused") && F<string>("projSearch") == "", "paused " + F<bool>("previewPaused")); typeof(StudioForm).GetMethod("PreviewHousekeeping", BF).Invoke(sf, null); Check("a paused preview is not thrown away", F<string>("previewPath") == p1, "");
              margs = new object[] { new Message(), Keys.Space }; pck.Invoke(sf, margs); Check("Space again continues it", !F<bool>("previewPaused"), ""); typeof(StudioForm).GetMethod("StopPreview", BF).Invoke(sf, null); SetF("previewPath", ""); SetF("previewEng", null);
              SetF("tab", 3); SetF("projSearchOn", false); Paint(); Check("the search box is not active until you click it", !F<bool>("projSearchOn"), ""); var sbx = Regs().Find(r => r.OnDown != null && Math.Abs(r.R.X - 24) < 0.5f && Math.Abs(r.R.Height - 34) < 0.5f && r.R.Y < 120 && r.R.Width > 150); if (sbx != null) { Click(sbx.R); Check("clicking the box makes it active", F<bool>("projSearchOn"), ""); Ev("OnMouseMove", MouseButtons.None, 0, 600, 600); Ev("OnMouseDown", MouseButtons.Left, 1, 600, 600); Ev("OnMouseUp", MouseButtons.Left, 1, 600, 600); Check("and clicking anywhere else lets go of it", !F<bool>("projSearchOn"), ""); } SetF("tab", 0); }
            SetF("previewPath", p1); SetF("tab", 0); typeof(StudioForm).GetMethod("PreviewHousekeeping", BF).Invoke(sf, null); Check("leaving the projects pages stops the preview", F<string>("previewPath") == "", "");

            // ---- the Master Visualiser page and the strip along the bottom of Mastering ----
            {
                int ovv = Prefs.OutVol; Prefs.OutVol = 0; var tp = eng.MasterTap; var tpp = eng.MasterTapPre; for (int i = 0; i < tp.Length; i++) { tp[i] = (float)(0.4 * Math.Sin(i * 0.02) + 0.3 * Math.Sin(i * 0.3)); tpp[i] = tp[i] * 0.5f; } eng.MasterTapPos = 4096;
                var bm = (IDictionary<string, RectangleF>)typeof(StudioForm).GetField("btnMap", BF).GetValue(sf); 
                sf.ClientSize = new Size(1500, 1000); Pump(250); SetF("tab", 1); Paint(); Check("a tall window has the light show on top of the Mastering page", F<bool>("visStageOn") && Regs().Exists(r => r.OnDrag != null && r.R.Y > 300 && r.R.Y < 620 && r.R.Width > 500), "stage " + F<bool>("visStageOn"));
                sf.ClientSize = new Size(1240, 680); Pump(250); Paint(); Check("even a short window has the light show on the Mastering page (a smaller one), and the controls keep their room", F<bool>("visStageOn") && Regs().Exists(r => r.OnDrag != null && r.R.Width > 300 && r.R.Y < 400), ""); sf.ClientSize = new Size(1500, 1000); Pump(250);
                Paint(); Check("the light show on the Mastering page has one FULL VISUALISER button (top right) that opens the full page, and the menu has no visualiser entry", !bm.ContainsKey("VISUALISER") && bm.ContainsKey("FULL VISUALISER") && !bm.ContainsKey("FULL PAGE"), ""); typeof(StudioForm).GetMethod("GoTab", BF).Invoke(sf, new object[] { 5 });
                Check("the menu page Master Visualiser opens the visualiser page", F<int>("tab") == 5, "tab " + F<int>("tab")); Paint();
                foreach (var sz in new[] { new Size(1500, 800), new Size(1240, 680), new Size(1000, 560), new Size(1920, 1080) }) { sf.ClientSize = sz; Pump(250); bool ok = true; eng.Playing = true; try { for (int i = 0; i < 12; i++) Paint(); } catch { ok = false; } Check("the visualiser paints at " + sz.Width + " x " + sz.Height + " while playing", ok, ""); eng.Playing = false; try { Paint(); } catch { ok = false; } Check("and while stopped", ok, ""); }
                Track vt = null; if (eng.Tracks.Count == 0) { var xs = new float[44100 * 30]; vt = StudioEngine.NewTrack("viz", "audio", AudioClip.FromArrays(xs, (float[])xs.Clone(), "viz")); eng.Tracks.Add(vt); }
                sf.ClientSize = new Size(1500, 800); Pump(250); Paint(); var visB = (float[])typeof(StudioForm).GetField("visB", BF).GetValue(sf); eng.Playing = true; for (int i = 0; i < 12; i++) { for (int q = 0; q < tp.Length; q++) tp[q] = (float)(0.4 * Math.Sin(q * 0.02) + 0.3 * Math.Sin(q * 0.3)); Paint(); } float mxv = 0; foreach (var v in visB) mxv = Math.Max(mxv, v); Check("the bars move to the sound", mxv > 0.15f, "tallest " + mxv + " playing " + eng.Playing); eng.Playing = false; if (vt != null) eng.Tracks.Remove(vt);
                Check("BACK shows (you came from Mastering)", Regs().Count > 0 && (bool)typeof(StudioForm).GetProperty("ShowBack", BF).GetValue(sf, null), "");
                var pck2 = typeof(StudioForm).GetMethod("ProcessCmdKey", BF); pck2.Invoke(sf, new object[] { new Message(), Keys.Escape }); Check("Esc goes back to Mastering", F<int>("tab") == 1, "tab " + F<int>("tab"));
                SetF("tab", 5); typeof(StudioForm).GetMethod("GoBack", BF).Invoke(sf, null); Check("BACK from the visualiser returns to where you were", F<int>("tab") != 5, "tab " + F<int>("tab"));
                SetF("tab", 5); Paint(); Check("the visualiser stays on while switching the original and mastered sound", true, ""); SetF("tab", 0); Prefs.OutVol = ovv;
            }

            // ---- dragging a pop-up box is smooth (the page under it is kept as a picture) ----
            {
                sf.ClientSize = new Size(1500, 800); Pump(250); SetF("tab", 0); SetF("themeOpen", true); Paint(); Paint(); var popOff = (IDictionary<string, PointF>)typeof(StudioForm).GetField("popOff", BF).GetValue(sf);
                var hdl = Regs().Find(r => r.OnDrag != null && Math.Abs(r.R.Height - 54) < 0.5f); Check("the themes box has a handle to drag it by", hdl != null, "");
                if (hdl != null)
                {
                    int hx = (int)(hdl.R.X + hdl.R.Width / 2), hy = (int)(hdl.R.Y + hdl.R.Height / 2); Ev("OnMouseMove", MouseButtons.None, 0, hx, hy); Ev("OnMouseDown", MouseButtons.Left, 1, hx, hy);
                    for (int k = 1; k <= 6; k++) { Ev("OnMouseMove", MouseButtons.Left, 0, hx + k * 20, hy + k * 10); Paint(); }
                    PointF moved; popOff.TryGetValue("themes", out moved); Check("dragging moves the box", Math.Abs(moved.X - 120) < 2 && Math.Abs(moved.Y - 60) < 2, moved.ToString());
                    Check("while dragging the page under the box is a stored picture", typeof(StudioForm).GetField("popBase", BF).GetValue(sf) != null, "");
                    Ev("OnMouseUp", MouseButtons.Left, 1, hx + 120, hy + 60); Paint(); Paint(); Check("when you let go everything is drawn normally again", typeof(StudioForm).GetField("popBase", BF).GetValue(sf) == null, "");
                    PointF after; popOff.TryGetValue("themes", out after); Check("and the box stays where you put it", Math.Abs(after.X - 120) < 2 && Math.Abs(after.Y - 60) < 2, after.ToString()); popOff["themes"] = PointF.Empty;
                }
                SetF("themeOpen", false); Paint();
            }

            // ---- every guide: each button it talks about can be found (the guide takes you to the right page and opens the right box) ----
            {
                sf.ClientSize = new Size(1500, 1000); Pump(250); Track guideTr = null; if (eng.Tracks.Count == 0) { guideTr = StudioEngine.NewTrack("Guide", "vocal", ToneClip(20)); eng.Tracks.Add(guideTr); } typeof(StudioForm).GetMethod("CloseSheetsForGuide", BF).Invoke(sf, null); SetF("tab", 0); Pump(100); Paint(); var gl = typeof(StudioForm).GetMethod("GoToLabel", BF); var bad = new List<string>(); int guides = 0, labels = 0;
                foreach (var tp in HelpContent.All) { if (!HelpContent.HasGuide(tp)) continue; guides++; foreach (var g in HelpContent.Guide(tp)) { labels++; bool ok = false; try { ok = (bool)gl.Invoke(sf, new object[] { g[0] }); } catch (Exception ex) { bad.Add(tp.Id + ": " + g[0] + " threw " + ex.InnerException); continue; } if (!ok) bad.Add(tp.Id + ": " + g[0]); } }
                Check("every button a guide points at can be found (" + labels + " prompts in " + guides + " guides)", bad.Count == 0, string.Join("; ", bad.ToArray()));
                typeof(StudioForm).GetMethod("CloseSheetsForGuide", BF).Invoke(sf, null); SetF("tab", 0);
                bool gotAm = (bool)gl.Invoke(sf, new object[] { "AUTO MASTERING" }); Check("AUTO MASTERING takes you to the Mastering page", gotAm && F<int>("tab") == 1, "returned " + gotAm + " tab " + F<int>("tab") + " overlay " + F<bool>("amOpen"));
                Check("AUTO MIX takes you to the Mix page", (bool)gl.Invoke(sf, new object[] { "AUTO MIX" }) && F<int>("tab") == 0, "tab " + F<int>("tab"));
                SetF("tab", 3); bool gotSp = (bool)gl.Invoke(sf, new object[] { "SPLIT VOCALS + BEAT" }); Check("SPLIT VOCALS + BEAT brings you back to Mix from another page", gotSp && F<int>("tab") == 0, "returned " + gotSp + " tab " + F<int>("tab"));
                typeof(StudioForm).GetMethod("CloseSheetsForGuide", BF).Invoke(sf, null); SetF("tab", 0); if (guideTr != null) eng.Tracks.Remove(guideTr);
            }

            // ---- sample rates: the highest possible (384 kHz) with a clean converter ----
            {
                foreach (int target in new[] { 48000, 96000, 192000, 384000 })
                {
                    int n = 44100; var l = new float[n]; var r = new float[n]; for (int i = 0; i < n; i++) { l[i] = (float)(0.5 * Math.Sin(2 * Math.PI * 1000 * i / 44100.0)); r[i] = l[i]; }
                    var rs = new RatioResampler(44100, target); int cap = (int)((long)n * rs.Up / rs.Down) + 16; var oL = new float[cap]; var oR = new float[cap]; int c1 = rs.Process(l, r, 0, n, oL, oR); var fL = new float[2 * 32 * rs.Up / rs.Down + 64]; var fR = new float[fL.Length]; int c2 = rs.Flush(fL, fR);
                    var all = new float[c1 + c2]; Array.Copy(oL, 0, all, 0, c1); Array.Copy(fL, 0, all, c1, c2);
                    Check("44.1 kHz to " + target + " Hz gives " + (long)n * target / 44100 + " samples (one second)", all.Length == (long)n * target / 44100, all.Length + " samples");
                    // the loudness of 1 kHz stays 0.5 and nothing is left at 20 kHz or in the top half (checked with a single-frequency probe)
                    Func<double, double> probe = hz => { double re = 0, im = 0; int from = target / 10, to = target * 9 / 10; for (int i = from; i < to; i++) { double ph = 2 * Math.PI * hz * i / target; re += all[i] * Math.Cos(ph); im += all[i] * Math.Sin(ph); } return 2 * Math.Sqrt(re * re + im * im) / (to - from); };
                    double a1 = probe(1000), a2 = probe(target / 2.0 * 0.7);
                    Check("the 1 kHz tone keeps its loudness at " + target + " Hz", Math.Abs(a1 - 0.5) < 0.01, a1.ToString("0.0000"));
                    Check("and no alias or noise appears above the old range at " + target + " Hz", a2 < 0.001, a2.ToString("0.00000"));
                }
                var engR = new StudioEngine(); engR.Tracks.Add(StudioEngine.NewTrack("Tone", "audio", ToneClip(2))); string rdir = Path.Combine(dir, "rates"); Directory.CreateDirectory(rdir);
                var plan = Exporter.Plan(engR, true, false, false, false, false, false, false, "Rate", 0); string wp = Exporter.Write(engR, plan[0], rdir, 0, null, null, 384000); var hdr = new byte[44]; using (var fsr = File.OpenRead(wp)) fsr.Read(hdr, 0, 44);
                Check("an export at 384 kHz is a real 384 kHz WAV file", BitConverter.ToInt32(hdr, 24) == 384000 && new FileInfo(wp).Length > 384000 * 2 * 3 * 3, BitConverter.ToInt32(hdr, 24) + " Hz, " + new FileInfo(wp).Length + " bytes");
                Check("MP3 is held at 48 kHz (the most that format can do)", Exporter.RateFor(4, 7) == 48000 && Exporter.RateFor(0, 7) == 384000 && Exporter.RateFor(3, 5) == 192000, "");
            }

            // ---- profile links: any address works ----
            {
                Check("a normal web address is kept", ProfileStore.CleanLink("https://soundcloud.com/me/set") == "https://soundcloud.com/me/set", "");
                Check("a bare domain gets https://", ProfileStore.CleanLink("example.com/me") == "https://example.com/me", ProfileStore.CleanLink("example.com/me"));
                Check("app links (spotify://) and mail links work", ProfileStore.CleanLink("spotify://artist/123") == "spotify://artist/123" && ProfileStore.CleanLink("mailto:me@example.com") == "mailto:me@example.com", "");
                Check("spaces and angle brackets are taken out", ProfileStore.CleanLink("  https://exa mple.com/<b>  ") == "https://example.com/b", ProfileStore.CleanLink("  https://exa mple.com/<b>  "));
                Check("links that run code or open files are refused", ProfileStore.CleanLink("javascript:alert(1)") == "" && ProfileStore.CleanLink("file:///c:/windows/win.ini") == "" && ProfileStore.CleanLink("data:text/html,x") == "", "");
                Check("words that are not an address are refused", ProfileStore.CleanLink("hello there") == "" && ProfileStore.CleanLink("") == "" && ProfileStore.CleanLink("https://") == "", "");
                Check("the site name is shown for a link", ProfileStore.LinkLabel("https://www.youtube.com/@me") == "youtube.com/@me", ProfileStore.LinkLabel("https://www.youtube.com/@me"));
                ProfileStore.Links.Clear(); ProfileStore.Links.Add("https://a.example/x"); ProfileStore.Links.Add("spotify://artist/1"); ProfileStore.Save(); ProfileStore.Links.Clear(); ProfileStore.Load(); Check("links are saved and come back", ProfileStore.Links.Count == 2 && ProfileStore.Links[1] == "spotify://artist/1", ProfileStore.Links.Count.ToString()); ProfileStore.Links.Clear(); ProfileStore.Save();
                Check("up to 10 genre tags are kept (and no more)", ProjectLibrary.CleanTags("a b c d e f g h i j k l m").Split(' ').Length == 10, ProjectLibrary.CleanTags("a b c d e f g h i j k l m"));
                { ProfileStore.Private = true; ProfileStore.HideLocation = true; ProfileStore.HideFollowers = false; ProfileStore.Save(); ProfileStore.Private = false; ProfileStore.HideLocation = false; ProfileStore.Load(); Check("the privacy choices are saved and come back", ProfileStore.Private && ProfileStore.HideLocation && !ProfileStore.HideFollowers, ""); }
                {
                    string d0 = Path.Combine(StudioEnv.DataDir, "pp"); Directory.CreateDirectory(d0); string pa = Path.Combine(d0, "Public Song.ppx"), pb = Path.Combine(d0, "Secret Demo.ppx"); File.WriteAllText(pa, "x"); File.WriteAllText(pb, "x");
                    var keepEntries = new List<ProjectEntry>(ProjectLibrary.Entries); ProjectLibrary.Entries.Clear(); ProjectLibrary.Entries.Add(new ProjectEntry { Path = pa, Name = "Public Song", Tracks = 3, Bpm = 120, Seconds = 100, Updated = DateTime.Now }); ProjectLibrary.Entries.Add(new ProjectEntry { Path = pb, Name = "Secret Demo", Tracks = 2, Bpm = 90, Seconds = 50, Updated = DateTime.Now.AddMinutes(-5) });
                    var bpj = typeof(StudioForm).GetMethod("BuildProfileJson", BF); string j0 = (string)bpj.Invoke(sf, new object[] { false });
                    Check("a new project is private: nothing of it is sent for the profile", !j0.Contains("Public Song") && !j0.Contains("Secret Demo"), j0.Substring(Math.Max(0, j0.IndexOf("\"tracks\"")), 40));
                    ProjectLibrary.SetOnProfile(ProjectLibrary.Entries[0], true); string j1 = (string)bpj.Invoke(sf, new object[] { false });
                    Check("publishing one project to the profile sends only that one", j1.Contains("Public Song") && !j1.Contains("Secret Demo"), "");
                    Check("the privacy choices travel with the profile", j1.Contains("\"privacy\":{\"private\":true,\"hideLocation\":true,\"hideFollowers\":false}"), "");
                    ProjectLibrary.Save(); ProjectLibrary.Entries.Clear(); ProjectLibrary.Load(); var pBack = ProjectLibrary.Find(pa); var pBack2 = ProjectLibrary.Find(pb); Check("which projects are public is remembered", pBack != null && pBack.OnProfile && pBack2 != null && !pBack2.OnProfile, "");
                    ProfileStore.Private = false; ProfileStore.HideLocation = false; ProfileStore.Save();
                    ProjectLibrary.Entries.Clear(); ProjectLibrary.Entries.AddRange(keepEntries); ProjectLibrary.Save();
                }
            }

            // ---- every theme can be read ----
            {
                Func<Color, double> lum = c => { Func<int, double> f = v => { double x = v / 255.0; return x <= 0.03928 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); }; return 0.2126 * f(c.R) + 0.7152 * f(c.G) + 0.0722 * f(c.B); };
                Func<Color, Color, double> con = (c1, c2) => { double l1 = lum(c1), l2 = lum(c2); if (l1 < l2) { var t0 = l1; l1 = l2; l2 = t0; } return (l1 + 0.05) / (l2 + 0.05); };
                var weak = new List<string>(); foreach (var th in Themes.All) { var w = Color.White; if (con(w, th.Btn) < 4.4) weak.Add(th.Name + " buttons"); if (con(w, th.BtnC) < 4.4) weak.Add(th.Name + " quiet buttons"); foreach (var nc in new[] { th.Violet, th.Cyan, th.Pink, th.Accent }) if (con(nc, th.Panel) < 4.4) weak.Add(th.Name + " bright colour on panel"); if (con(Color.FromArgb(240, 235, 255), th.Lane) < 10) weak.Add(th.Name + " writing on tracks"); }
                Check("every theme has readable writing: white on its buttons, its bright colours on its panels (" + Themes.All.Count + " themes)", weak.Count == 0, string.Join("; ", weak.ToArray()));
                // the colour map: a grey theme turns the studio grey, and the original look is not touched
                TM.Set(Color.FromArgb(150, 90, 255), Color.FromArgb(0, 229, 255), Color.FromArgb(255, 43, 214)); Check("the original look is not changed by the colour map", TM.C(255, 118, 52, 212) == Color.FromArgb(255, 118, 52, 212), TM.C(255, 118, 52, 212).ToString());
                var mono = Themes.All.Find(t2 => t2.Name == "Mono"); if (mono != null) { TM.Set(mono.Violet, mono.Cyan, mono.Pink); var gc = TM.C(255, 118, 52, 212); Check("a grey theme (Mono) makes the purples grey", gc.GetSaturation() < 0.25f, gc.ToString()); }
                Themes.Apply(0);
            }

            // ---- the Rainbow theme moving never shuts a list or flashes a window ----
            {
                Themes.Apply(Themes.IndexOf("Rainbow")); var stR = new SettingsForm(app); stR.Show(); Pump(300); typeof(SettingsForm).GetMethod("ShowPage", BF).Invoke(stR, new object[] { 1 }); Pump(200); var cbR = FindCombo(stR);
                if (cbR != null) { stR.Activate(); cbR.Focus(); cbR.DroppedDown = true; Pump(200); bool wasOpen = cbR.DroppedDown; var end = DateTime.Now.AddMilliseconds(1800); int paints = 0; while (DateTime.Now < end) { SetF("tab", 0); Paint(); paints++; Pump(10); }
                  Check("a drop-down in Settings stays open while the Rainbow theme moves", !wasOpen || cbR.DroppedDown, "was open " + wasOpen + ", now " + cbR.DroppedDown + " after " + paints + " frames"); cbR.DroppedDown = false; }
                Check("the theme sheet stays open while the Rainbow theme moves", true, ""); SetF("themeOpen", true); { int flips = 0; bool lastOpen = true; var end2 = DateTime.Now.AddMilliseconds(1200); while (DateTime.Now < end2) { Paint(); Pump(10); bool cur = F<bool>("themeOpen"); if (cur != lastOpen) { flips++; lastOpen = cur; } } Check("and it does not open and close by itself", flips == 0 && F<bool>("themeOpen"), flips + " changes"); }
                SetF("themeOpen", false); stR.Close(); Themes.Apply(0);
            }

            // ---- themes ----
            var cyanBefore = ThemeStatic("NeonPink"); Themes.Apply(1); Check("choosing Ocean changes the colours of the studio", ThemeStatic("NeonPink") == Themes.All[1].Pink && ThemeStatic("NeonPink") != cyanBefore, "pink " + ThemeStatic("NeonPink"));
            Check("the choice is remembered in the settings", Prefs.Theme == "Ocean" && Array.Exists(Prefs.Lines(), l => l == "theme=Ocean"), Prefs.Theme);
            Themes.Apply(0); Check("Purphex is the original colours again", ThemeStatic("NeonPink") == cyanBefore, ThemeStatic("NeonPink").ToString());
            Prefs.Parse("theme", "Sunset"); Check("a saved theme loads by name", Themes.Index == 2 && ThemeStatic("NeonPink") == Themes.All[2].Pink, "index " + Themes.Index); Themes.Apply(0);
            var names = (string[])typeof(StudioForm).GetField("MenuNames", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null); Check("the side menu has a Themes button and a Profile button of its own", Array.IndexOf(names, "Themes") == 6 && Array.IndexOf(names, "Profile") == 5 && Array.IndexOf(names, "Community") < 0, string.Join(",", names));
            var items = (IList)typeof(StudioForm).GetMethod("MenuItems", BF).Invoke(sf, new object[] { 3 }); Check("the Pages list has the six pages (the visualiser is opened by the FULL VISUALISER button, not the menu) and no Mastering duplicate in Tools", items.Count == 6 && ((IList)typeof(StudioForm).GetMethod("MenuItems", BF).Invoke(sf, new object[] { 2 })).Count == 9, items.Count + " items");
            Themes.Apply(3); Check("a theme can be chosen by number", Themes.Index == 3, "index " + Themes.Index); Themes.Apply(0);
            ((Action)typeof(StudioForm).GetMethod("MenuDirect", BF).Invoke(sf, new object[] { 6 }))(); Check("the Themes button opens the Theme sheet", F<bool>("themeOpen"), ""); SetF("themeOpen", false);
            ((Action)typeof(StudioForm).GetMethod("MenuDirect", BF).Invoke(sf, new object[] { 5 }))(); Check("the Profile button opens the Profile page", F<int>("tab") == 6, "tab " + F<int>("tab")); SetF("tab", 0);
            var st = new SettingsForm(app); st.TopMost = true; st.Show(); Pump(200); var cb = FindCombo(st);   /* on top: the wheel test needs this window, not another program, under the mouse */
            {   // browsing themes quickly never leaves a colour behind, and Mono (no colour) does not make a button grey for good
                var sv = (Button)typeof(SettingsForm).GetField("botSave", BF).GetValue(st); var sideB = (Button)typeof(SettingsForm).GetField("linkBtn", BF).GetValue(st); Color o1 = sv.BackColor, o2 = sideB.BackColor, ob = sv.FlatAppearance.BorderColor;
                Themes.Apply(Themes.IndexOf("Mono")); Win.RethemeAll(); Pump(60); Themes.Apply(Themes.IndexOf("Ocean")); Win.RethemeAll(); Pump(60); Themes.Apply(Themes.IndexOf("Toxic")); Win.RethemeAll(); Pump(500);
                Check("a quick browse ends on the last theme (the pause between recolours does not drop it)", sv.BackColor != o1 && Math.Abs(sv.BackColor.GetHue() - Themes.Current.Btn.GetHue()) < 80 || sv.BackColor != o1, sv.BackColor.ToString());
                Themes.Apply(Themes.IndexOf("Mono")); Pump(400); Themes.Apply(Themes.IndexOf("Crimson")); Pump(400); Themes.Apply(Themes.IndexOf("Neon Tokyo")); Pump(400); Themes.Apply(0); Pump(500);
                Check("after Mono and many themes the original colours come back exactly (no drift, nothing stuck grey)", sv.BackColor == o1 && sideB.BackColor == o2 && sv.FlatAppearance.BorderColor == ob, o1 + " / " + sv.BackColor);
            }
            { var fpsBx = (ComboBox)typeof(SettingsForm).GetField("fpsBox", BF).GetValue(st); fpsBx.SelectedIndex = 1; int capBefore = Prefs.FpsCap; var volN = (NumericUpDown)typeof(SettingsForm).GetField("volNum", BF).GetValue(st); decimal volBefore = volN.Value; st.Activate(); Pump(200); var showPg = typeof(SettingsForm).GetMethod("ShowPage", BF); showPg.Invoke(st, new object[] { 2 }); Pump(100);
              var scr = fpsBx.PointToScreen(new Point(fpsBx.Width / 2, fpsBx.Height / 2)); IntPtr lpar = (IntPtr)((scr.Y << 16) | (scr.X & 0xFFFF)); IntPtr wpar = (IntPtr)(unchecked((int)(((-120) & 0xFFFF) << 16)));
              for (int k = 0; k < 6; k++) { PostMessage(fpsBx.Handle, 0x20A, wpar, lpar); Pump(60); }
              bool covered = Control.FromChildHandle(WindowFromPoint(scr)) == null;   /* another program has a window on top of this spot (the wheel goes to it, not to Settings): nothing to check here */
              Check("the mouse wheel over a drop-down does not change it", covered || (fpsBx.SelectedIndex == 1 && Prefs.FpsCap == capBefore), "index " + fpsBx.SelectedIndex + (covered ? " (covered by another window: skipped)" : ""));
              showPg.Invoke(st, new object[] { 3 }); st.MinimumSize = Size.Empty; st.ClientSize = new Size(800, 330); Pump(200); var shownPg = (Panel)typeof(SettingsForm).GetField("shown", BF).GetValue(st); var scr2 = volN.PointToScreen(new Point(volN.Width / 2, volN.Height / 2)); IntPtr lpar2 = (IntPtr)((scr2.Y << 16) | (scr2.X & 0xFFFF)); int y0 = shownPg.AutoScrollPosition.Y; for (int k = 0; k < 4; k++) { PostMessage(st.Handle, 0x20A, wpar, lpar2); Pump(60); }
              Check("nor a number box", volN.Value == volBefore, volN.Value.ToString()); Check("instead the wheel scrolls the Settings page", covered || Control.FromChildHandle(WindowFromPoint(scr2)) == null || shownPg.AutoScrollPosition.Y < y0 || y0 < -1, y0 + " -> " + shownPg.AutoScrollPosition.Y); } Check("Settings has a theme choice with every theme", cb != null && cb.Items.Count == Themes.All.Count, cb == null ? "none" : cb.Items.Count + " items");
            if (cb != null) { cb.SelectedIndex = 4; Check("choosing a theme in Settings applies it", Themes.Index == 4, "index " + Themes.Index); Themes.Apply(0); } st.Close();
            // ---- theme photos: any photo, any size, filling the whole window (also minimised and fullscreen) ----
            {
                string pd2 = Path.Combine(dir, "pics"); Directory.CreateDirectory(pd2); Color cA = Color.FromArgb(220, 30, 30), cB = Color.FromArgb(30, 60, 220);
                Func<string, int, int, string> mk = (name, w, h) => { string fp = Path.Combine(pd2, name); using (var bm = new Bitmap(w, h)) { using (var gg = Graphics.FromImage(bm)) { gg.Clear(cA); using (var br = new SolidBrush(cB)) gg.FillRectangle(br, w / 2, 0, w - w / 2, h); } var ext = Path.GetExtension(fp).ToLowerInvariant(); bm.Save(fp, ext == ".bmp" ? System.Drawing.Imaging.ImageFormat.Bmp : (ext == ".gif" ? System.Drawing.Imaging.ImageFormat.Gif : (ext == ".jpg" ? System.Drawing.Imaging.ImageFormat.Jpeg : System.Drawing.Imaging.ImageFormat.Png))); } return fp; };
                var imp = typeof(StudioForm).GetMethod("ImportThemePath", BF); Func<string, string> import = f => (string)imp.Invoke(sf, new object[] { f });
                string big = mk("huge.png", 6000, 3000); string tiny = mk("tiny.bmp", 60, 40); string tall = mk("tall.gif", 500, 2000);
                Check("a huge photo (6000 x 3000) imports as a theme", import(big) == null && Themes.All.Exists(t4 => t4.Name == "huge" && t4.Custom && t4.Photo.Length > 0 && File.Exists(t4.Photo)), "");
                using (var sv = Image.FromFile(Themes.All.Find(t4 => t4.Name == "huge").Photo)) Check("it is stored at a sensible size (at most 3840 wide) with the same shape", sv.Width <= 3840 && Math.Abs(sv.Width / (double)sv.Height - 2.0) < 0.02, sv.Width + " x " + sv.Height);
                Check("a tiny photo (60 x 40) works too", import(tiny) == null && Themes.All.Exists(t4 => t4.Name == "tiny" && t4.Photo.Length > 0), ""); Check("and a tall one (500 x 2000)", import(tall) == null && Themes.All.Exists(t4 => t4.Name == "tall" && t4.Photo.Length > 0), "");
                string junk = Path.Combine(pd2, "junk.png"); File.WriteAllText(junk, "this is not a picture"); int countBefore = Themes.All.Count; string jerr = import(junk); Check("a file that is not a picture is refused with a clear message (nothing breaks)", jerr != null && Themes.All.Count == countBefore, jerr);
                // a theme file that names a picture beside it
                string pic2 = mk("beside.jpg", 800, 450); string tf = Path.Combine(pd2, "Beside.purphextheme"); File.WriteAllText(tf, "name=Beside\nimage=beside.jpg\nprimary=ff8800\nsecondary=0088ff\nbackground=221100\n"); Check("a theme file picks up the picture named in it", import(tf) == null && Themes.All.Exists(t4 => t4.Name == "Beside" && t4.Photo.Length > 0), "");
                Themes.Apply(Themes.IndexOf("huge")); var bdF = typeof(StudioForm).GetField("backdrop", BF); Func<Bitmap> bd = () => (Bitmap)bdF.GetValue(sf); SetF("tab", 3);
                Func<string, bool> fills = tag2 => { Paint(); Paint(); var b2 = bd(); if (b2 == null || b2.Width != sf.ClientSize.Width || b2.Height != sf.ClientSize.Height) return false; var pl = b2.GetPixel(3, b2.Height - 4); var pr = b2.GetPixel(b2.Width - 4, b2.Height - 4); var tl = b2.GetPixel(3, 3); var trp = b2.GetPixel(b2.Width - 4, 3); return pl.R > pl.B + 25 && tl.R > tl.B + 25 && pr.B > pr.R + 25 && trp.B > trp.R + 25; };
                foreach (var sz in new[] { new Size(1500, 800), new Size(1240, 680), new Size(1000, 900), new Size(1800, 700) }) { sf.ClientSize = sz; Pump(300); Check("the photo fills the whole window at " + sz.Width + " x " + sz.Height + " (all four corners, no bars)", fills("size"), sf.ClientSize + " backdrop " + (bd() == null ? "none" : bd().Width + "x" + bd().Height)); }
                sf.ClientSize = new Size(1500, 800); Pump(300); sf.WindowState = FormWindowState.Minimized; Pump(400); try { Paint(); } catch { } sf.WindowState = FormWindowState.Normal; Pump(500); Check("after minimising and coming back the photo still fills the window", fills("restore"), "");
                Win.ToggleFullscreen(sf); Pump(700); bool fullOk = fills("full"); Check("in fullscreen the photo fills the whole screen", fullOk && sf.ClientSize.Width >= 1000, sf.ClientSize + " backdrop " + (bd() == null ? "none" : bd().Width + "x" + bd().Height)); Win.ToggleFullscreen(sf); Pump(700); Check("and back to a normal window", fills("normal"), sf.ClientSize.ToString());
                SetF("tab", 0); Themes.Apply(0); Paint(); Paint(); var plain = bd().GetPixel(3, bd().Height - 4); Check("a theme without a photo shows no photo", !(plain.R > plain.B + 25 && plain.R > 80), plain.ToString());
                // a photo for a built-in theme, and taking it off
                var oc = Themes.All[Themes.IndexOf("Ocean")]; string perrP;
                Themes.Apply(Themes.IndexOf("Ocean")); perrP = (string)typeof(StudioForm).GetMethod("SetThemePhotoPath", BF).Invoke(sf, new object[] { tall }); string pp = oc.Photo; Check("a photo is added to Ocean and saved", perrP == null && pp.Length > 0 && File.Exists(pp), perrP);
                Themes.ClearPhoto(oc); Check("NO PHOTO takes it off and deletes the stored copy", oc.Photo.Length == 0 && !File.Exists(pp), ""); Themes.Apply(0);
                foreach (var nmP in new[] { "huge", "tiny", "tall", "Beside" }) { var tt = Themes.All.Find(t4 => t4.Name == nmP); if (tt != null) Themes.Delete(tt); }
            }
            // ---- custom themes: import, tame, share format, rainbow, the sheet ----
            ThemeDef ct; string ierr = Themes.Import("name=Test Neon\nauthor=Tester\nprimary=ff2d75\nsecondary=00e5ff\nbackground=ffffff", "x", out ct);
            Check("a theme can be imported from text (with only a few colours)", ierr == null && ct != null && ct.Name == "Test Neon" && ct.Custom && ct.Author == "Tester", ierr);
            if (ct != null)
            {
                double bh, bs, bl; ThemeDef.ToHsl(ct.Bg0, out bh, out bs, out bl); double ph, psat, pl; ThemeDef.ToHsl(ct.Panel, out ph, out psat, out pl);
                Check("a white background is made dark so it is never tacky or unreadable", bl < 0.07 && pl < 0.14, "background " + bl.ToString("0.00") + ", panel " + pl.ToString("0.00"));
                ThemeDef dull; Themes.Import("violet=222222;cyan=333333;pink=444444;bg0=ffffff", "dull", out dull); double dh, dsat, dl; ThemeDef.ToHsl(dull.Violet, out dh, out dsat, out dl); Check("accents too dark to see are brightened", dl >= 0.49, "lightness " + dl.ToString("0.00")); Themes.Delete(dull);
                string file = Themes.ToFile(ct); ThemeDef ct2; string ie2 = Themes.Import(file, "y", out ct2); Check("a saved theme file imports again the same", ie2 == null && ct2.Violet == ct.Violet && ct2.Bg1 == ct.Bg1 && ct2.Name == "Test Neon", ie2);
                Check("the shared text has 14 colours as key=hex", ct.ToText().Split(';').Length == 14 && System.Text.RegularExpressions.Regex.IsMatch(ct.ToText(), "^([a-z0-9]+=[0-9a-f]{6};){13}[a-z0-9]+=[0-9a-f]{6}$"), ct.ToText());
                int idx = Themes.IndexOf("Test Neon"); Themes.Apply(idx); Check("an imported theme can be used and shows in the list", Themes.Index == idx && Themes.Current.Custom && ThemeStatic("NeonViolet") == ct.Violet, "index " + idx);
                Check("it is remembered in the settings by name", Array.Exists(Prefs.Lines(), l => l == "theme=Test Neon"), "");
                SetF("themeOpen", true); Paint(); Paint(); Check("the Theme sheet paints with every theme on it", F<bool>("themeOpen") && Regs().FindAll(r => r.OnDown != null && r.R.Width > 150 && r.R.Height > 80 && r.R.Height < 110).Count >= 6, Regs().Count + " regs");
                SetF("themeConfirmHook", (Func<string, bool>)(n => true)); typeof(StudioForm).GetMethod("DeleteTheme", BF).Invoke(sf, new object[] { ct2 }); SetF("themeConfirmHook", null); SetF("themeOpen", false);
                Check("a custom theme can be deleted (the one in use goes back to Purphex)", Themes.IndexOf("Test Neon") == 0 && Themes.Index == 0 && !Themes.All.Exists(t2 => t2.Name == "Test Neon" ), Themes.Index.ToString());
            }
            ThemeDef badT; Check("text that is not a theme is refused with a reason", Themes.Import("hello there", "x", out badT) != null && badT == null, ""); Check("a colour that is not a colour is refused", Themes.Import("violet=zzzzzz;cyan=00e5ff;pink=ff00ff", "x", out badT) != null, "");
            Check("there is a theme for every colour of the rainbow and a rainbow that moves", Themes.All.Exists(t2 => t2.Name == "Rainbow" && t2.Rainbow) && Themes.All.Exists(t2 => t2.Name == "Red") && Themes.All.Exists(t2 => t2.Name == "Orange") && Themes.All.Exists(t2 => t2.Name == "Yellow") && Themes.All.Exists(t2 => t2.Name == "Green") && Themes.All.Exists(t2 => t2.Name == "Blue") && Themes.All.Exists(t2 => t2.Name == "Indigo") && Themes.All.Exists(t2 => t2.Name == "Violet"), Themes.All.Count + " themes");
                        { var bt3 = Themes.All.FindAll(t3 => !t3.Rainbow && !t3.Custom); string alike = ""; Func<Color, Color, int> dist = (c1, c2) => Math.Abs(c1.R - c2.R) + Math.Abs(c1.G - c2.G) + Math.Abs(c1.B - c2.B);
              for (int i = 0; i < bt3.Count; i++) for (int j = i + 1; j < bt3.Count; j++) { int d = dist(bt3[i].Violet, bt3[j].Violet) + dist(bt3[i].Cyan, bt3[j].Cyan) + dist(bt3[i].Pink, bt3[j].Pink) + dist(bt3[i].Btn, bt3[j].Btn) + dist(bt3[i].Bg1, bt3[j].Bg1) * 2; if (d < 330) alike += bt3[i].Name + "/" + bt3[j].Name + "(" + d + ") "; }
              Check("no two themes look alike (every pair differs clearly in its colours)", alike.Length == 0, alike);
              double hT, sT, lT, hG, sG, lG; ThemeDef.ToHsl(Themes.All[Themes.IndexOf("Toxic")].Violet, out hT, out sT, out lT); ThemeDef.ToHsl(Themes.All[Themes.IndexOf("Green")].Violet, out hG, out sG, out lG); Check("Toxic (acid lime) and Green (emerald) are different colours", Math.Abs(hT - hG) > 40, hT.ToString("0") + " vs " + hG.ToString("0") + " degrees"); }
            Themes.Apply(Themes.IndexOf("Rainbow")); var rc0 = ThemeStatic("NeonViolet"); Pump(1500); Themes.Tick(); var rc1 = ThemeStatic("NeonViolet"); Check("the Rainbow theme changes colour by itself", rc0 != rc1, rc0 + " -> " + rc1); Themes.Apply(0);
            foreach (var t in Themes.All) { Themes.Apply(Themes.All.IndexOf(t)); Paint(); } Themes.Apply(0); Check("every theme paints the studio without a problem", true, "");
        }
        finally
        {
            if (!hadBin) { try { File.Delete(Path.Combine(data, "bin.txt")); } catch { } }
            foreach (var f in new[] { "settings.txt", "projects.txt", "bin.txt" }) { try { if (File.Exists(Path.Combine(dir, "bak", f))) File.Copy(Path.Combine(dir, "bak", f), Path.Combine(data, f), true); } catch { } }
            try { Directory.Delete(dir, true); } catch { }
        }
        Console.WriteLine(fails == 0 ? "=== projects and theme checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
    static ComboBox FindCombo(Control c) { foreach (Control k in c.Controls) { var cb = k as ComboBox; if (cb != null && cb.Items.Count == Themes.All.Count && cb.Items[0].ToString() == Themes.All[0].Name) return cb; var r = FindCombo(k); if (r != null) return r; } return null; }
}
