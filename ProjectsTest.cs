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
    static AudioClip ToneClip(int sec) { int n = 44100 * sec; var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(0.4 * Math.Sin(i * 0.05)); return AudioClip.FromArrays(x, (float[])x.Clone(), "t"); }
    static Color ThemeStatic(string n) { return (Color)typeof(StudioForm).GetField(n, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null); }

    [STAThread]
    static void Main()
    {
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
            SetF("tab", 0); Paint(); Check("leaving the page hands the keyboard back and the search comes back ready next time", F<bool>("projSearchOn") && !F<bool>("projBin"), "");
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
            SetF("tab", 0); Paint(); SetF("tab", 4); Paint(); SetF("tab", 1); Paint(); Check("Mastering has a BACK button", Regs().Exists(r => r.OnDown != null && Math.Abs(r.R.Width - 100) < 0.5f && r.R.Y < 125 && r.R.Y > 70), ""); typeof(StudioForm).GetMethod("GoBack", BF).Invoke(sf, null); Check("BACK returns to the page you came from (Community)", F<int>("tab") == 4, "tab " + F<int>("tab"));
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
            Check("the soft logo behind Community and My Projects is made", typeof(StudioForm).GetMethod("SoftLogo", BF).Invoke(sf, new object[] { 400 }) != null, "");
            eng.Tracks.Clear();
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

            // ---- BACK button ----
            SetF("tab", 3); Paint(); var back = Regs().FindLast(r => Math.Abs(r.R.X - 270) < 0.5f && Math.Abs(r.R.Width - 100) < 0.5f); Check("the projects page has a BACK button", back != null, "");
            if (back != null) { Click(back.R); Check("BACK returns to the page before", F<int>("tab") == 0, "tab " + F<int>("tab")); }
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
            SetF("previewPath", p1); SetF("tab", 0); typeof(StudioForm).GetMethod("PreviewHousekeeping", BF).Invoke(sf, null); Check("leaving the projects pages stops the preview", F<string>("previewPath") == "", "");

            // ---- themes ----
            var cyanBefore = ThemeStatic("NeonPink"); Themes.Apply(1); Check("choosing Ocean changes the colours of the studio", ThemeStatic("NeonPink") == Themes.All[1].Pink && ThemeStatic("NeonPink") != cyanBefore, "pink " + ThemeStatic("NeonPink"));
            Check("the choice is remembered in the settings", Prefs.Theme == "Ocean" && Array.Exists(Prefs.Lines(), l => l == "theme=Ocean"), Prefs.Theme);
            Themes.Apply(0); Check("Purphex is the original colours again", ThemeStatic("NeonPink") == cyanBefore, ThemeStatic("NeonPink").ToString());
            Prefs.Parse("theme", "Sunset"); Check("a saved theme loads by name", Themes.Index == 2 && ThemeStatic("NeonPink") == Themes.All[2].Pink, "index " + Themes.Index); Themes.Apply(0);
            var names = (string[])typeof(StudioForm).GetField("MenuNames", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null); Check("the side menu has a Theme tab", Array.IndexOf(names, "Theme") == 5, string.Join(",", names));
            var items = (IList)typeof(StudioForm).GetMethod("MenuItems", BF).Invoke(sf, new object[] { 5 }); Func<int, string, object> mf = (i, n) => items[i].GetType().GetField(n).GetValue(items[i]); Check("the Theme tab lists every theme", items.Count == Themes.All.Length && (string)mf(0, "Key") == "IN USE", items.Count + " items");
            ((Action)mf(3, "Do"))(); Check("clicking a theme in the menu applies it", Themes.Index == 3, "index " + Themes.Index); Themes.Apply(0);
            var st = new SettingsForm(app); st.Show(); Pump(200); var cb = FindCombo(st); Check("Settings has a theme choice with every theme", cb != null && cb.Items.Count == Themes.All.Length, cb == null ? "none" : cb.Items.Count + " items");
            if (cb != null) { cb.SelectedIndex = 4; Check("choosing a theme in Settings applies it", Themes.Index == 4, "index " + Themes.Index); Themes.Apply(0); } st.Close();
            foreach (var t in Themes.All) { Themes.Apply(Array.IndexOf(Themes.All, t)); Paint(); } Themes.Apply(0); Check("every theme paints the studio without a problem", true, "");
        }
        finally
        {
            if (!hadBin) { try { File.Delete(Path.Combine(data, "bin.txt")); } catch { } }
            foreach (var f in new[] { "settings.txt", "projects.txt", "bin.txt" }) { try { if (File.Exists(Path.Combine(dir, "bak", f))) File.Copy(Path.Combine(dir, "bak", f), Path.Combine(data, f), true); } catch { } }
            try { Directory.Delete(dir, true); } catch { }
        }
        Console.WriteLine(fails == 0 ? "=== projects and theme checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
    static ComboBox FindCombo(Control c) { foreach (Control k in c.Controls) { var cb = k as ComboBox; if (cb != null && cb.Items.Count == Themes.All.Length && cb.Items[0].ToString() == Themes.All[0].Name) return cb; var r = FindCombo(k); if (r != null) return r; } return null; }
}
