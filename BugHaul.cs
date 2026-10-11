using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

// The bug haul: every page and pop-up of the studio, in a normal window, a small window, full screen and after minimising, with every button pressed, every box dragged, the wheel turned and the keys pressed.
// Anything that throws, hangs, opens an unexpected window or leaves the screen broken is listed.   csc /main:BH ... BugHaul.cs     run: BH.exe [quick]
static class BH
{
    static StudioForm sf; static App app; static StudioEngine eng; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static readonly List<string> problems = new List<string>(); static readonly Dictionary<string, string> dialogs = new Dictionary<string, string>(); static int clicks, drags, wheels, keys, scenes;
    static T F<T>(string n) { return (T)typeof(StudioForm).GetField(n, BF).GetValue(sf); }
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static object M(string n, params object[] a) { return typeof(StudioForm).GetMethod(n, BF).Invoke(sf, a); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); Thread.Sleep(8); } }
    static void Ev(string name, MouseButtons b, int clicksN, int x, int y) { typeof(StudioForm).GetMethod(name, BF | BindingFlags.DeclaredOnly).Invoke(sf, new object[] { new MouseEventArgs(b, clicksN, x, y, 0) }); }
    static void Paint() { sf.Invalidate(); sf.Update(); Application.DoEvents(); }
    static List<HitReg> Regs() { var l = new List<HitReg>(); foreach (object o in (IEnumerable)typeof(StudioForm).GetField("regs", BF).GetValue(sf)) l.Add((HitReg)o); return l; }
    static AudioClip Tone(int sec) { int n = 44100 * sec; var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(0.4 * Math.Sin(i * 0.05) * Math.Exp(-(i % 22050) / 9000.0)); return AudioClip.FromArrays(x, (float[])x.Clone(), "tone"); }

    // ---- windows that are not ours (a dialog the click opened) are closed with a Cancel so the test never hangs ----
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc p, IntPtr l); delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid); [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n); [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int n);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i); [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
    // the test window is invisible and lets every click through, so running it never gets in the way of whoever is using the PC
    static void Stealth(Form f) { try { f.ShowInTaskbar = false; f.Opacity = 0.0; int ex = GetWindowLong(f.Handle, -20); SetWindowLong(f.Handle, -20, ex | 0x80000 | 0x20 | 0x80 | 0x08000000); } catch { } }
    static IntPtr mainH, appH; static volatile bool inClick; static volatile string curLabel = ""; static DateTime clickStart; static volatile bool stop;
    static Thread mainThread; static long beat; static DateTime lastBeat = DateTime.Now; static bool dumped;
    // if the test stops making progress, say where the studio thread is stuck
    static void DumpStuck()
    {
        try { mainThread.Suspend(); var ctor = typeof(StackTrace).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(Thread), typeof(bool) }, null); var st = (StackTrace)ctor.Invoke(new object[] { mainThread, false }); Console.WriteLine("STUCK at " + curLabel + " " + st.ToString()); Console.Out.Flush(); } catch (Exception ex) { Console.WriteLine("could not read the stack: " + ex.Message); } finally { try { mainThread.Resume(); } catch { } }
    }
    static void Watchdog()
    {
        while (!stop)
        {
            Thread.Sleep(250); if ((DateTime.Now - lastBeat).TotalSeconds > 25 && !dumped) { dumped = true; DumpStuck(); }
            if (!inClick || (DateTime.Now - clickStart).TotalMilliseconds < 1200) continue;
            uint me = (uint)Process.GetCurrentProcess().Id; var mine = new List<IntPtr>();
            EnumWindows((h, l) => { uint pid; GetWindowThreadProcessId(h, out pid); if (pid == me && IsWindowVisible(h)) mine.Add(h); return true; }, IntPtr.Zero);
            foreach (var h in mine)
            {
                if (h == mainH || h == appH) continue; var txt = new System.Text.StringBuilder(128); GetWindowText(h, txt, 128); var cls = new System.Text.StringBuilder(64); GetClassName(h, cls, 64);
                lock (dialogs) { if (!dialogs.ContainsKey(curLabel)) dialogs[curLabel] = txt.ToString(); }
                foreach (int id in new[] { 2, 7, 3, 1 }) PostMessage(h, 0x111, (IntPtr)id, IntPtr.Zero); PostMessage(h, 0x10, IntPtr.Zero, IntPtr.Zero);
            }
        }
    }
    static void CloseExtraWindows()
    {
        var extra = new List<Form>(); foreach (Form f in Application.OpenForms) if (f != sf && f != app) extra.Add(f); foreach (var f in extra) { try { f.Close(); } catch { } }
    }
    static void Beat() { lastBeat = DateTime.Now; }
    static void Problem(string where, string what) { string p = where + ": " + what; if (!problems.Contains(p)) { problems.Add(p); Console.WriteLine("PROBLEM  " + p); Console.Out.Flush(); } }

    static string Only = Environment.GetEnvironmentVariable("BH_ONLY"); static bool allowClose; static int closeAttempts; static string Mode = "normal"; static bool Verbose = Environment.GetEnvironmentVariable("BH_V") == "1";
    static void Reset(Action setup)
    {
        try { M("CloseSheetsForGuide"); } catch { } CloseExtraWindows(); try { M("StopPreview"); } catch { } SetF("popDragId", ""); SetF("revertAction", null); SetF("tab", 0); try { eng.Stop(); } catch { } eng.Playing = false;
        { eng.Tracks.Clear(); eng.Tracks.Add(StudioEngine.NewTrack("Lead vocal", "vocal", Tone(30))); eng.Tracks.Add(StudioEngine.NewTrack("Beat", "beat", Tone(30))); }
        SetF("selTrack", 0); SetF("selFx", -1); SetF("menuOpen", false); SetF("flexOpen", false); try { if (setup != null) setup(); } catch (Exception ex) { Problem("setup", ex.ToString().Split('\n')[0]); }
        Paint(); Paint();
    }
    static int Errors() { return F<int>("paintErrors"); }
    // a cheap picture of the state of the studio: when a press changes it, the scene is set up again
    static string StateHash() { try { return F<int>("tab") + "|" + eng.Tracks.Count + "|" + F<int>("selTrack") + "|" + F<int>("selFx") + "|" + F<bool>("menuOpen") + F<bool>("themeOpen") + F<bool>("ntOpen") + F<bool>("busOpen") + F<bool>("stylesOpen") + F<bool>("browserOpen") + F<bool>("gainOpen") + F<bool>("psOpen") + F<bool>("cpOpen") + F<bool>("amOpen") + F<bool>("autoOpen") + F<bool>("pickOpen") + F<bool>("soundsOpen") + F<bool>("flexOpen") + "|" + F<int>("mpTab") + "|" + (int)F<long>("scroll") + "|" + F<double>("spp") + "|" + F<bool>("projBin"); } catch { return ""; } }
    static string Key(HitReg r) { return (int)r.R.X + "," + (int)r.R.Y + "," + (int)r.R.Width + "," + (int)r.R.Height; }
    static string NameOf(HitReg r)
    {
        var bm = (IDictionary)typeof(StudioForm).GetField("btnMap", BF).GetValue(sf); foreach (DictionaryEntry d in bm) { var rr = (RectangleF)d.Value; if (Math.Abs(rr.X - r.R.X) < 1.5f && Math.Abs(rr.Y - r.R.Y) < 1.5f && Math.Abs(rr.Width - r.R.Width) < 1.5f) return (string)d.Key; }
        return "area " + Key(r);
    }

    static readonly HashSet<string> Skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "SPLIT VOCALS + BEAT", "x" /* removes a track: done in its own check */ };

    static void Scene(string name, int tabNum, Action setup, bool pressAll = true)
    {
        if (Only != null && !name.Contains(Only)) return; scenes++; Reset(setup); if (tabNum >= 0) { SetF("tab", tabNum); Paint(); Paint(); } string where = Mode + " / " + name; var swatch = Stopwatch.StartNew();
        int e0 = Errors();
        var snap = Regs(); var keys0 = new List<string>(); var all = new List<HitReg>(); foreach (var r in snap) { if (r.R.Width < 3 || r.R.Height < 3) continue; if (r.R.Width >= sf.ClientSize.Width - 2 && r.R.Height >= sf.ClientSize.Height - 2) continue; if (keys0.Contains(Key(r))) continue; keys0.Add(Key(r)); all.Add(r); }
        string baseHash = StateHash(); bool dirty = false;
        if (pressAll)
            for (int i = 0; i < all.Count; i++)
            {
                if (i > 0 && (dirty || StateHash() != baseHash)) { if (Verbose) { Console.WriteLine("      (set up again)"); Console.Out.Flush(); } Reset(setup); if (tabNum >= 0) { SetF("tab", tabNum); Paint(); Paint(); } baseHash = StateHash(); dirty = false; }
                var cur = Regs(); HitReg r = null; foreach (var c in cur) if (Key(c) == keys0[i]) { r = c; break; } if (r == null) continue; string label = NameOf(r); if (Skip.Contains(label)) continue;
                if (r.R.Bottom < 0 || r.R.Y > sf.ClientSize.Height || r.R.Right < 0 || r.R.X > sf.ClientSize.Width) continue;
                int x = (int)Math.Max(1, Math.Min(sf.ClientSize.Width - 2, r.R.X + r.R.Width / 2)), y = (int)Math.Max(1, Math.Min(sf.ClientSize.Height - 2, r.R.Y + r.R.Height / 2)); curLabel = where + " / " + label; clickStart = DateTime.Now; inClick = true; Beat(); if (Verbose) { Console.WriteLine("      press " + label); Console.Out.Flush(); }
                try
                {
                    Ev("OnMouseMove", MouseButtons.None, 0, x, y); if (r.OnDown != null || r.OnUp != null || r.OnDrag != null) { Ev("OnMouseDown", MouseButtons.Left, 1, x, y); clicks++; if (r.OnDrag != null) { drags++; for (int k = 1; k <= 3; k++) Ev("OnMouseMove", MouseButtons.Left, 0, Math.Min(sf.ClientSize.Width - 2, x + k * 12), Math.Min(sf.ClientSize.Height - 2, y + k * 6)); } Ev("OnMouseUp", MouseButtons.Left, 1, x, y); }
                    if (r.OnDouble != null) { Ev("OnMouseDown", MouseButtons.Left, 2, x, y); Ev("OnMouseUp", MouseButtons.Left, 2, x, y); }
                    if (r.OnRight != null) { Ev("OnMouseDown", MouseButtons.Right, 1, x, y); Ev("OnMouseUp", MouseButtons.Right, 1, x, y); }
                    if (r.OnWheel != null) { wheels++; typeof(StudioForm).GetMethod("OnMouseWheel", BF | BindingFlags.DeclaredOnly).Invoke(sf, new object[] { new MouseEventArgs(MouseButtons.None, 0, x, y, 120) }); typeof(StudioForm).GetMethod("OnMouseWheel", BF | BindingFlags.DeclaredOnly).Invoke(sf, new object[] { new MouseEventArgs(MouseButtons.None, 0, x, y, -120) }); }
                    Pump(20); if (Verbose) { Console.WriteLine("      (paint)"); Console.Out.Flush(); } Paint(); Paint();
                }
                catch (TargetInvocationException ex) { Problem(where + " / " + label, "threw " + (ex.InnerException == null ? ex.Message : ex.InnerException.GetType().Name + ": " + ex.InnerException.Message + " at " + (ex.InnerException.StackTrace ?? "").Split('\n')[0].Trim())); }
                catch (Exception ex) { Problem(where + " / " + label, "threw " + ex.GetType().Name + ": " + ex.Message); }
                finally { inClick = false; }
                if (StateHash() != baseHash) dirty = true;
                if (Errors() > e0) { Problem(where + " / " + label, "the screen could not be drawn after this press (a hidden drawing error)"); e0 = Errors(); }
                if (F<int>("tab") < 0 || F<int>("tab") > 7) Problem(where + " / " + label, "went to a page that does not exist: " + F<int>("tab"));
            }
        Console.WriteLine("   " + where + ": " + all.Count + " areas, " + (int)swatch.Elapsed.TotalSeconds + " s"); Console.Out.Flush();
        // the keyboard: every key goes through the same path the real keyboard uses
        Reset(setup); if (tabNum >= 0) { SetF("tab", tabNum); Paint(); }
        var pck = typeof(StudioForm).GetMethod("ProcessCmdKey", BF);
        foreach (var k in new[] { Keys.Space, Keys.Escape, Keys.Delete, Keys.Left, Keys.Right, Keys.Up, Keys.Down, Keys.Home, Keys.End, Keys.B, Keys.F, Keys.M, Keys.S, Keys.Enter, Keys.Control | Keys.Z, Keys.Control | Keys.Y, Keys.Control | Keys.S, Keys.Control | Keys.D })
        {
            if (k == (Keys.Control | Keys.S)) continue; keys++; curLabel = where + " / key " + k; clickStart = DateTime.Now; inClick = true;
            try { var m = new Message(); pck.Invoke(sf, new object[] { m, k }); Pump(10); Paint(); } catch (TargetInvocationException ex) { Problem(where + " / key " + k, "threw " + (ex.InnerException == null ? ex.Message : ex.InnerException.GetType().Name + ": " + ex.InnerException.Message + " at " + (ex.InnerException.StackTrace ?? "").Split('\n')[0].Trim())); } finally { inClick = false; }
            if (Errors() > e0) { Problem(where + " / key " + k, "a hidden drawing error after this key"); e0 = Errors(); }
            if (k == Keys.Escape || k == Keys.Delete || k == Keys.Control) { Reset(setup); if (tabNum >= 0) { SetF("tab", tabNum); Paint(); } }
        }
        // typing
        foreach (char ch in "abc 1\b") { try { typeof(StudioForm).GetMethod("OnKeyPress", BF | BindingFlags.DeclaredOnly).Invoke(sf, new object[] { new KeyPressEventArgs(ch) }); } catch (TargetInvocationException ex) { Problem(where + " / typing", "threw " + ex.InnerException.GetType().Name + ": " + ex.InnerException.Message); } }
        Paint(); if (Errors() > e0) Problem(where, "a hidden drawing error while typing");
    }

    static void RunAllScenes(bool quick)
    {
        var fake = new List<CommPreset>(); for (int i = 0; i < 5; i++) fake.Add(new CommPreset { Id = "p" + i, Kind = "fx:bassboost", Name = "Preset " + i, Author = "Tester", Uses = 10, Data = "freq=50" });
        Scene("mix", 0, null); Scene("mix with effects", 0, () => { foreach (var id in new[] { "eq", "comp", "reverb" }) { var f = FxRegistry.Create(id); f.Prepare(44100); eng.Tracks[0].Fx.Add(f); } SetF("selFx", 0); });
        Scene("mix with an effect panel (compressor)", 0, () => { var f = FxRegistry.Create("comp"); f.Prepare(44100); eng.Tracks[0].Fx.Add(f); SetF("selFx", 0); });
        Scene("mix, sounds browser", 0, () => { M("OpenSounds"); });
        Scene("mix, new track sheet", 0, () => { M("OpenNewTrackSheet", 0); }); Scene("mix, bus sheet", 0, () => { M("OpenBusSheet"); }); Scene("mix, styles sheet", 0, () => { M("OpenStyles"); });
        Scene("mix, add effect list", 0, () => { M("OpenBrowser", false, -1); }); Scene("mix, gain panel", 0, () => { SetF("toolsTrack", eng.Tracks[0]); M("OpenGain"); });
        Scene("mix, preset sheet", 0, () => { var f = FxRegistry.Create("eq"); f.Prepare(44100); eng.Tracks[0].Fx.Add(f); M("OpenPresetSheet", f); });
        Scene("mix, community presets sheet", 0, () => { M("OpenCommunityPresets", "fx:bassboost", "Bass Boost", (Action<CommPreset>)(p => { }), (Func<string>)(() => "freq=50"), "My preset"); SetF("cpList", fake); SetF("cpLoading", false); SetF("cpStatus", ""); });
        Scene("mix, theme sheet", 0, () => { M("OpenThemeSheet", false); }); Scene("mix, community themes", 0, () => { M("OpenThemeSheet", true); });
        Scene("mix, side menu", 0, () => { M("OpenMenu"); }); for (int sec = 0; sec <= 4 && !quick; sec++) { int s2 = sec; Scene("mix, side menu section " + sec, 0, () => { M("OpenMenu"); SetF("menuSec", s2); }); }
        Scene("mastering", 1, () => { SetF("mpTab", 0); }); Scene("mastering presets", 1, () => { SetF("mpTab", 1); });
        Scene("mastering with the visualiser stage", 1, () => { sf.ClientSize = new Size(Math.Max(sf.ClientSize.Width, 1500), Math.Max(sf.ClientSize.Height, 1000)); SetF("mpTab", 0); });
        Scene("visualiser", 5, null); Scene("lyrics", 2, null); Scene("my projects", 3, () => { SetF("projectsLoaded", true); }); Scene("recycling bin", 3, () => { SetF("projectsLoaded", true); SetF("projBin", true); });
        Scene("community", 4, () => { SetF("projectsLoaded", true); }); Scene("profile", 6, () => { SetF("profLoaded", true); });
        Scene("artists", 7, () => { SetF("artFetched", true); var al = new List<ArtistInfo>(); for (int i = 0; i < 5; i++) al.Add(new ArtistInfo { Id = "a" + i.ToString("x12"), Name = "Artist " + i, Bio = "x", Tags = new[] { "dnb" }, Followers = i, Tracks = i }); SetF("artList", al); SetF("artSel", null); });
        Scene("an artist's page", 7, () => { SetF("artFetched", true); var a = new ArtistInfo { Id = "a000000000001", Name = "Artist", Bio = "x", Tags = new[] { "dnb" }, Followers = 3, Tracks = 1 }; a.TrackList.Add(new ArtistTrack { Title = "Song", Bpm = 120, Secs = 100 }); SetF("artList", new List<ArtistInfo> { a }); SetF("artSel", a); });
    }

    [STAThread]
    static void Main(string[] args)
    {
        bool quick = args.Length > 0 && args[0] == "quick"; mainThread = Thread.CurrentThread; Edition.Override = Tier.Fantasy;
        StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_bughaul_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(StudioEnv.DataDir); Prefs.OutVol = 0;
        Application.EnableVisualStyles(); Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException); Application.ThreadException += (s, e) => Problem("unhandled (" + curLabel + ")", e.Exception.GetType().Name + ": " + e.Exception.Message + " at " + (e.Exception.StackTrace ?? "").Split('\n')[0].Trim());
        app = (App)Activator.CreateInstance(typeof(App), true); eng = app.StudioEng; CommunityPresets.BaseUrl = "http://127.0.0.1:1";
        sf = new StudioForm(app); sf.ClientSize = new Size(1500, 900); sf.Show(); Stealth(sf); Pump(500); SetF("askHook", (Func<string, bool>)(t => false)); SetF("confirmOpenHook", (Func<ProjectEntry, bool>)(en => false)); SetF("removeHook", (Func<ProjectEntry, int>)(en => 0));
        string dir = Path.Combine(Path.GetTempPath(), "bh-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); string p1 = Path.Combine(dir, "Alpha.ytmproj"); ProjectIO.Save(eng, p1); ProjectLibrary.Touch(p1, "Alpha", 3, 140, 95, true, null);
        sf.FormClosing += (s0, e0) => { if (!allowClose) { e0.Cancel = true; closeAttempts++; } }; mainH = sf.Handle; appH = app.IsHandleCreated ? app.Handle : IntPtr.Zero; var wd = new Thread(Watchdog) { IsBackground = true }; wd.Start(); var clock = Stopwatch.StartNew();
        var modes = new List<KeyValuePair<string, Action>>
        {
            new KeyValuePair<string, Action>("normal 1500x900", () => { if (Win.IsFull(sf)) Win.ToggleFullscreen(sf); sf.WindowState = FormWindowState.Normal; sf.ClientSize = new Size(1500, 900); }),
            new KeyValuePair<string, Action>("small 1000x600", () => { sf.ClientSize = new Size(1000, 600); }),
            new KeyValuePair<string, Action>("fullscreen", () => { if (!Win.IsFull(sf)) Win.ToggleFullscreen(sf); }),
            new KeyValuePair<string, Action>("after minimising", () => { if (Win.IsFull(sf)) Win.ToggleFullscreen(sf); sf.ClientSize = new Size(1300, 800); Pump(200); sf.WindowState = FormWindowState.Minimized; Pump(400); try { Paint(); } catch { } sf.WindowState = FormWindowState.Normal; Pump(400); }),
        };
        foreach (var md in modes)
        {
            if (quick && md.Key != "normal 1500x900" && md.Key != "fullscreen") continue; Mode = md.Key; Console.WriteLine("=== " + md.Key); md.Value(); Pump(500); Paint();
            if (sf.ClientSize.Width < 100 || sf.ClientSize.Height < 100) Problem(md.Key, "the window is too small to use: " + sf.ClientSize);
            RunAllScenes(quick);
            if (md.Key == "fullscreen" || md.Key == "after minimising") { Reset(null); Paint(); var bd = typeof(StudioForm).GetField("backdrop", BF).GetValue(sf) as Bitmap; if (bd != null && (bd.Width != sf.ClientSize.Width || bd.Height != sf.ClientSize.Height)) Problem(md.Key, "the background picture does not match the window: " + bd.Size + " vs " + sf.ClientSize); }
        }
        allowClose = true; stop = true; Console.WriteLine("the studio was asked to close " + closeAttempts + " times by the Exit items (blocked here)"); Console.WriteLine("scenes " + scenes + ", presses " + clicks + ", drags " + drags + ", wheels " + wheels + ", keys " + keys + " in " + (int)clock.Elapsed.TotalSeconds + " s");
        Console.WriteLine("buttons that opened a window of their own (closed with Cancel):"); foreach (var kv in dialogs.OrderBy(k => k.Key)) Console.WriteLine("   " + kv.Key + "  ->  " + kv.Value);
        Console.WriteLine(problems.Count == 0 ? "=== bug haul clean ===" : "=== " + problems.Count + " PROBLEMS ==="); Environment.Exit(problems.Count == 0 ? 0 : 1);
    }
}
