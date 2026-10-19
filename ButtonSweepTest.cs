using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// BUTTON SWEEP: goes through every page and sheet of the DAW, step by step, and for each one
//  1. checks that every button can be reached by the mouse (nothing invisible lies on top of it and takes the click), and
//  2. presses every button for real (mouse down and up where it is drawn) and checks that nothing throws.
// Questions are answered No, file and save windows are closed by themselves, links never open, nothing is sent to the website.   csc /main:BS ... ButtonSweepTest.cs
static class BS
{
    static StudioForm sf; static App app; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static int fails, pressed, reached; static readonly List<string> errs = new List<string>();
    static void SetF(string n, object v) { var f = typeof(StudioForm).GetField(n, BF); if (f != null) f.SetValue(sf, v); }
    static T F<T>(string n) { return (T)typeof(StudioForm).GetField(n, BF).GetValue(sf); }
    static void Fail(string s) { fails++; Console.WriteLine("FAIL  " + s); Console.Out.Flush(); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(5); } }
    static void Mouse(string m, MouseButtons b, int x, int y) { typeof(StudioForm).GetMethod(m, BF | BindingFlags.DeclaredOnly).Invoke(sf, new object[] { new MouseEventArgs(b, b == MouseButtons.None ? 0 : 1, x, y, 0) }); }
    static void Paint() { var pm = typeof(StudioForm).GetMethod("PaintAll", BF); using (var bm = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height)) using (var g = Graphics.FromImage(bm)) { var pe = new PaintEventArgs(g, new Rectangle(Point.Empty, bm.Size)); for (int i = 0; i < 2; i++) pm.Invoke(sf, new object[] { pe }); } }
    static AudioClip Clip(int sec) { int n = 44100 * sec; var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(0.3 * Math.Sin(i * 0.05) * Math.Exp(-(i % 22050) / 6000.0)); return AudioClip.FromArrays(x, (float[])x.Clone(), "sweep"); }

    // ---- windows that open while a button is tested are closed again by themselves (a file window, a question box, the Export window, a menu) ----
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, System.Text.StringBuilder sb, int n);
    [DllImport("user32.dll")] static extern IntPtr GetDlgItem(IntPtr h, int id);
    static IntPtr keepA, keepB; static volatile int closedWins;
    static void Closer(object o)
    {
        uint me = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        EnumWindows((h, l) => { uint pid; GetWindowThreadProcessId(h, out pid); if (pid == me && h != keepA && h != keepB && IsWindowVisible(h)) { var cn = new System.Text.StringBuilder(64); GetClassName(h, cn, 64); if (cn.ToString() == "#32770") { /* a question box: press No if it has one, else Cancel (a Yes/No box ignores Close) */ int id = GetDlgItem(h, 7) != IntPtr.Zero ? 7 : 2; PostMessage(h, 0x0111, (IntPtr)id, IntPtr.Zero); } else PostMessage(h, 0x0010, IntPtr.Zero, IntPtr.Zero); closedWins++; } return true; }, IntPtr.Zero);
    }

    // labels of buttons that are never pressed by the sweep: they start the microphone, a long download or open another program
    static readonly string[] Skip = { "SPLIT VOCALS", "MIC", "MIDI REC", "FOLDER", "JOIN THE DISCORD", "TRANSLATE", "RECORD", "UPDATE", "DOWNLOAD" };
    static bool Skipped(string label) { string u = label.ToUpperInvariant(); foreach (var s in Skip) if (u.StartsWith(s)) return true; return false; }

    static void Reset()
    {
        foreach (var n in new[] { "menuOpen", "browserOpen", "ntOpen", "busOpen", "stylesOpen", "themeOpen", "caOpen", "psOpen", "amOpen", "autoOpen", "gainOpen", "pickOpen", "cpOpen", "relOpen", "tpOpen", "allFxOpen", "welcomeOn", "keyOn", "packsOpen", "projBin", "msgTyping", "artSearchOn", "projSearchOn", "fontSearchOn", "soundsOpen" }) { var f = typeof(StudioForm).GetField(n, BF); if (f != null && f.FieldType == typeof(bool)) f.SetValue(sf, false); }
        try { if (F<bool>("flexOpen")) typeof(StudioForm).GetMethod("FlexClose", BF).Invoke(sf, new object[] { false }); } catch { }   /* a FLEX panel opened by an earlier press is closed */
        SetF("toast", ""); SetF("revertAction", null); SetF("artSel", null);
        Tracks(); lock (sf.eng.Sync) { sf.eng.MasterFx.Clear(); sf.eng.InitMasterChain(); } SetF("selFx", -1); SetF("selMaster", 0); SetF("vscroll", 0f);   // every press starts from the same project
        foreach (Form f in Application.OpenForms.Cast<Form>().ToArray()) if (f != sf && f != app && !f.IsDisposed && f.Visible) { try { f.Close(); } catch { } }
    }

    // one page: set it up, paint it, check that every button can be reached, then press each one
    static string only = null, dumpOnly = null; static bool dump;
    static void Page(string name, Action setup)
    {
        if (only != null && name.IndexOf(only, StringComparison.OrdinalIgnoreCase) < 0) return;
        try { Reset(); setup(); Paint(); } catch (Exception ex) { Fail(name + ": the page could not be drawn: " + (ex.InnerException ?? ex).Message); return; }
        var regs = ((List<HitReg>)typeof(StudioForm).GetField("regs", BF).GetValue(sf)).ToList(); var btns = ((List<RectangleF>)typeof(StudioForm).GetField("btnRects", BF).GetValue(sf)).ToList();
        var map = (Dictionary<string, RectangleF>)typeof(StudioForm).GetField("btnMap", BF).GetValue(sf); var names = new Dictionary<RectangleF, string>(); foreach (var kv in map) if (!names.ContainsKey(kv.Value)) names[kv.Value] = kv.Key;
        float W = sf.ClientSize.Width, H = sf.ClientSize.Height; int start = 0;
        for (int i = 0; i < regs.Count; i++) if (regs[i].OnDown != null && regs[i].R.Width >= W * 0.95f && regs[i].R.Height >= H * 0.9f) start = i + 1;   // a sheet's background: only what is on top of it counts
        if (dump && (dumpOnly == null || name.IndexOf(dumpOnly, StringComparison.OrdinalIgnoreCase) >= 0)) for (int i = 0; i < regs.Count; i++) Console.WriteLine("REG " + i + " " + regs[i].R + (regs[i].OnDown != null ? " down" : "") + (regs[i].OnWheel != null ? " wheel" : "") + (btns.Contains(regs[i].R) ? " BTN " + (names.ContainsKey(regs[i].R) ? names[regs[i].R] : "") : "") + (i == start ? "   <== start" : ""));
        var topR = typeof(StudioForm).GetMethod("TopRegion", BF); var todo = new List<KeyValuePair<string, PointF>>();
        for (int i = start; i < regs.Count; i++)
        {
            var r = regs[i]; if (r.OnDown == null || r.R.Width < 6 || r.R.Height < 6) continue; var vis = RectangleF.Intersect(r.R, new RectangleF(0, 0, W, H)); if (vis.Width < 4 || vis.Height < 4) continue;
            var c = new PointF(vis.X + vis.Width / 2, vis.Y + vis.Height / 2); var top = (HitReg)topR.Invoke(sf, new object[] { c });
            bool isBtn = btns.Contains(r.R);
            // a sheet or menu (it lies on a full-window background) draws its own items (menu entries, track-type cards): they count as buttons too, except the panels that only hold other items
            if (!isBtn && start > 0 && r.R.Width * r.R.Height < W * H * 0.25f) { int inside = 0; for (int q = i + 1; q < regs.Count; q++) if (regs[q].OnDown != null && r.R.Contains(regs[q].R) && regs[q].R != r.R) inside++; if (inside < 2) isBtn = true; }
            string label = names.ContainsKey(r.R) ? names[r.R] : (isBtn ? "item at " + (int)r.R.X + "," + (int)r.R.Y : null);
            bool ok = top == r || (top != null && r.R.Contains(top.R) && top.R != r.R) || (top != null && top.R == r.R);
            if (!ok && isBtn)
            {
                // a button partly under another one is fine if some part of it still takes the click
                bool any = false; for (float fx = 0.15f; fx < 0.9f && !any; fx += 0.2f) for (float fy = 0.2f; fy < 0.9f && !any; fy += 0.3f) { var p = new PointF(vis.X + vis.Width * fx, vis.Y + vis.Height * fy); var t2 = (HitReg)topR.Invoke(sf, new object[] { p }); if (t2 == r) { any = true; c = p; } }
                if (!any) Fail(name + ": \"" + label + "\" cannot be clicked: " + (top == null ? "nothing takes the click" : "something else lies on top of it (" + (int)top.R.X + "," + (int)top.R.Y + " " + (int)top.R.Width + "x" + (int)top.R.Height + ")"));
                else { reached++; todo.Add(new KeyValuePair<string, PointF>(label, c)); }
            }
            else if (ok && isBtn) { reached++; todo.Add(new KeyValuePair<string, PointF>(label, c)); }
        }
        int pagePressed = 0;
        foreach (var t in todo)
        {
            if (Skipped(t.Key)) continue;
            try { Reset(); setup(); Paint(); } catch { continue; }
            // the button may have moved when the page was drawn again: find the one with the same label
            var map2 = (Dictionary<string, RectangleF>)typeof(StudioForm).GetField("btnMap", BF).GetValue(sf); PointF at = t.Value; RectangleF rr; if (map2.TryGetValue(t.Key, out rr)) at = new PointF(rr.X + rr.Width / 2, rr.Y + rr.Height / 2);
            try
            {
                Mouse("OnMouseMove", MouseButtons.None, (int)at.X, (int)at.Y); Mouse("OnMouseDown", MouseButtons.Left, (int)at.X, (int)at.Y); Mouse("OnMouseUp", MouseButtons.Left, (int)at.X, (int)at.Y);
                Pump(60); var end = DateTime.Now.AddSeconds(25); while (F<bool>("busy") && DateTime.Now < end) Pump(100);
                if (F<bool>("busy")) Fail(name + ": \"" + t.Key + "\" started a job that never finished (25 s)");
                Paint(); pressed++; pagePressed++;
            }
            catch (Exception ex) { var ie = ex.InnerException ?? ex; Fail(name + ": pressing \"" + t.Key + "\" crashed: " + ie.GetType().Name + ": " + ie.Message + "  " + (ie.StackTrace ?? "").Split('\n').FirstOrDefault()); }
            if (sf.IsDisposed || !sf.Visible) { Fail(name + ": pressing \"" + t.Key + "\" closed the DAW"); Recreate(); }
            if (sf.WindowState != FormWindowState.Normal) sf.WindowState = FormWindowState.Normal; if (sf.ClientSize != new Size(1500, 860)) sf.ClientSize = new Size(1500, 860);
        }
        Console.WriteLine("PAGE  " + name + ": " + todo.Count + " buttons can be clicked, " + pagePressed + " pressed"); Console.Out.Flush();
    }

    static void Recreate()
    {
        try { if (!sf.IsDisposed) sf.Dispose(); } catch { }
        sf = new StudioForm(app); sf.StartPosition = FormStartPosition.Manual; sf.Location = new Point(10, 10); sf.ClientSize = new Size(1500, 860); sf.Show(); Pump(300); keepA = sf.Handle; Hooks();
    }
    static void Hooks()
    {
        SetF("askHook", (Func<string, bool>)(t => false)); SetF("exitAsk", (Func<string, DialogResult>)(t => DialogResult.Cancel)); SetF("nameHook", (Func<string, string>)(t => null));
    }
    static void Tracks()
    {
        var eng = sf.eng; lock (eng.Sync) eng.Tracks.Clear();
        var a = StudioEngine.NewTrack("Lead vocal", "vocal", Clip(8)); var b = StudioEngine.NewTrack("Beat", "beat", Clip(10)); var p = StudioEngine.NewTrack("Lead vocal (2)", "vocal", Clip(3)); p.Host = a; p.Offset = 44100 * 9;
        foreach (var ty in new[] { "eq", "comp", "pitch" }) { var f = FxRegistry.Create(ty); f.Prepare(44100); f.AutoKeyPending = false; a.Fx.Add(f); }
        var d = FxRegistry.Create("delay"); d.Prepare(44100); b.Fx.Add(d);
        var keys = StudioEngine.NewTrack("Keys", "inst", null); keys.Instrument = true; var empty = StudioEngine.NewTrack("Audio 5", "audio", null);
        lock (eng.Sync) { eng.Tracks.Add(a); eng.Tracks.Add(b); eng.Tracks.Add(p); eng.Tracks.Add(keys); eng.Tracks.Add(empty); }
    }

    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] != "*") only = args[0]; dump = args.Length > 1; if (args.Length > 1 && args[0] == "*") dumpOnly = args[1];   /* "*" PAGE: run every page, dump only PAGE */
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (s, e) => { var frames = (e.Exception.StackTrace ?? "").Split('\n').Select(x => x.Trim()).Where(x => x.StartsWith("at ")).ToArray(); if (frames.Length > 0 && frames.All(x => x.StartsWith("at System."))) { Console.WriteLine("NOTE  Windows itself hit " + e.Exception.GetType().Name + " while a window the sweep closed by force was painting (no Purphex code involved): " + frames.FirstOrDefault()); return; } Fail("an error happened later (in a timer or after a button): " + e.Exception.GetType().Name + ": " + e.Exception.Message + "  " + string.Join(" <- ", (e.Exception.StackTrace ?? "").Split('\n').Take(10).Select(x => x.Trim()).ToArray())); };
        StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_sweep_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(StudioEnv.DataDir);
        Edition.Override = Tier.Fantasy; CommunityPresets.BaseUrl = "http://127.0.0.1:1";   // nothing reaches the real website
        Application.EnableVisualStyles();
        app = (App)Activator.CreateInstance(typeof(App), true); Recreate(); try { keepB = app.Handle; } catch { }
        var closer = new System.Threading.Timer(Closer, null, 400, 250);
        Tracks();
        // ---- every page and sheet, one by one ----
        Page("MIX", () => { SetF("tab", 0); SetF("selTrack", 0); SetF("selFx", -1); });
        Page("MIX with a plugin open (EQ)", () => { SetF("tab", 0); SetF("selTrack", 0); SetF("selFx", 0); });
        Page("MIX with a plugin open (Compressor)", () => { SetF("tab", 0); SetF("selTrack", 0); SetF("selFx", 1); });
        Page("MIX with Pitch Correction open", () => { SetF("tab", 0); SetF("selTrack", 0); SetF("selFx", 2); });
        Page("MIX second track picked", () => { SetF("tab", 0); SetF("selTrack", 1); SetF("selFx", 0); });
        Page("ALL PLUGINS panel", () => { SetF("tab", 0); sf.OpenAllFx(); });
        Page("ADD EFFECT box", () => { SetF("tab", 0); sf.OpenBrowser(false, 0); });
        Page("SIDE MENU", () => { SetF("tab", 0); SetF("menuOpen", true); });
        for (int ms = 0; ms < 6; ms++) { int sec = ms; Page("SIDE MENU section " + sec, () => { SetF("tab", 0); SetF("menuOpen", true); Paint(); Mouse("OnMouseMove", MouseButtons.None, 60, 104 + sec * 46 + 20); Paint(); }); }   // the lists that open from File, Edit, Tools, Pages, View and Community
        Page("RAPPER VOCAL STYLES", () => { SetF("tab", 0); typeof(StudioForm).GetMethod("OpenStyles", BF).Invoke(sf, null); });
        Page("NEW TRACK sheet", () => { SetF("tab", 0); SetF("ntOpen", true); });
        Page("BUSES sheet", () => { SetF("tab", 0); SetF("busOpen", true); });
        Page("THEMES sheet", () => { SetF("tab", 0); SetF("themeOpen", true); });
        Page("WELCOME box", () => { SetF("tab", 0); SetF("welcomeOn", true); });
        Page("MASTERING", () => { SetF("tab", 1); SetF("mpTab", 0); SetF("selMaster", 0); });
        for (int i = 1; i < sf.eng.MasterFx.Count; i++) { int k = i; Page("MASTERING plugin " + sf.eng.MasterFx[i].Title, () => { SetF("tab", 1); SetF("mpTab", 0); SetF("selMaster", k); }); }
        Page("MASTERING PRESETS", () => { SetF("tab", 1); SetF("mpTab", 1); });
        Page("LYRICS", () => { SetF("tab", 2); });
        Page("MY PROJECTS", () => { SetF("tab", 3); });
        Page("COMMUNITY", () => { SetF("tab", 4); });
        Page("VISUALISER", () => { SetF("tab", 5); });
        Page("PROFILE", () => { SetF("tab", 6); });
        Page("ARTISTS", () => { SetF("tab", 7); SetF("artView", 0); });
        Page("MESSAGES", () => { SetF("tab", 7); SetF("artView", 1); var reqs = F<List<ArtistInfo>>("profReqs"); reqs.Clear(); reqs.Add(new ArtistInfo { Id = "a0000000000aa", Name = "Rowan" }); });
        Page("COLLABS", () => { SetF("tab", 7); SetF("artView", 2); });
        Page("FRIENDS", () => { SetF("tab", 7); SetF("artView", 3); });
        Page("FONTS", () => { SetF("tab", 8); });
        Func<string, int> ti = nm => sf.eng.Tracks.FindIndex(x => x.Name == nm);
        Page("PIANO ROLL on a note track", () => { SetF("tab", 0); SetF("selTrack", ti("Keys")); sf.OpenPianoRoll(null); });
        Page("PIANO ROLL choose a sound", () => { SetF("tab", 0); int e = ti("Audio 5"); if (e < 0) e = ti("Piano"); SetF("selTrack", e); sf.OpenPianoRoll(null); });
        Page("MIXER panel: ALL PLUGINS", () => { SetF("tab", 0); SetF("selTrack", 0); typeof(StudioForm).GetMethod("OpenInspector", BF).Invoke(sf, new object[] { 1 }); });
        Page("MIXER panel: CHANNEL", () => { SetF("tab", 0); SetF("selTrack", 1); typeof(StudioForm).GetMethod("OpenInspector", BF).Invoke(sf, new object[] { 0 }); });
        Page("SOUND PACKS", () => { SetF("tab", 0); typeof(StudioForm).GetMethod("OpenPacks", BF).Invoke(sf, null); });
        Page("SOUNDS", () => { SetF("tab", 0); typeof(StudioForm).GetMethod("OpenSounds", BF).Invoke(sf, null); });
        closer.Dispose(); Pump(200);
        Console.WriteLine("=== " + reached + " buttons checked on every page, " + pressed + " pressed, " + closedWins + " windows that opened were closed again ===");
        Console.WriteLine(fails == 0 ? "=== button sweep passed ===" : "=== " + fails + " FAILED ===");
        try { sf.Close(); } catch { } Environment.Exit(fails == 0 ? 0 : 1);
    }
}
