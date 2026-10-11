using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

// Goes through every Purphex Music screen at small, normal, full-screen and very large window sizes and checks that no writing is cut off,
// runs off the window or lands on other writing, and that the bottom bar's logo is in the middle.   csc /main:AU ... ScreenAudit.cs
static class AU
{
    static StudioForm sf; static StudioEngine eng; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public; static int problems, screens; static string sizeTag = "";
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static T F<T>(string n) { return (T)typeof(StudioForm).GetField(n, BF).GetValue(sf); }
    static object M(string n, params object[] a) { return typeof(StudioForm).GetMethod(n, BF).Invoke(sf, a); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    static void Hover(int x, int y) { typeof(StudioForm).GetMethod("OnMouseMove", BF | BindingFlags.DeclaredOnly).Invoke(sf, new object[] { new MouseEventArgs(MouseButtons.None, 0, x, y, 0) }); }
    static AudioClip Clip(int sec) { int n = 44100 * sec; var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(0.5 * Math.Sin(i * 0.05) * Math.Exp(-(i % 22050) / 6000.0)); return AudioClip.FromArrays(x, (float[])x.Clone(), "test"); }
    static void Report(string what) { problems++; Console.WriteLine("PROBLEM  [" + sizeTag + "] " + what); }

    // paint the screen once, then look at where every string landed
    static void Audit(string name)
    {
        SetF("toast", ""); Hover(3, 3); Bitmap bmp = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height); var args = new object[] { null };
        for (int pass = 0; pass < 2; pass++)
        {
            StudioForm.TxtAudit = new List<StudioForm.AuditText>();
            using (var g = Graphics.FromImage(bmp)) { var pe = new PaintEventArgs(g, new Rectangle(Point.Empty, sf.ClientSize)); typeof(StudioForm).GetMethod("PaintAll", BF).Invoke(sf, new object[] { pe }); }
        }
        var list = StudioForm.TxtAudit; StudioForm.TxtAudit = null; bmp.Dispose(); screens++; float W = sf.ClientSize.Width, H = sf.ClientSize.Height;
        // writing that is cut off by its box
        foreach (var t in list) if (t.Cut) Report(name + ": cut off \"" + Short(t.S) + "\" (needs more than " + (int)t.Box.Width + " px)");
        // writing that runs off the window
        foreach (var t in list) if (t.Ink.Right > W + 3 || t.Ink.X < -3) Report(name + ": off the right or left edge of the window \"" + Short(t.S) + "\"");   // (lists that scroll up and down simply have rows below the edge)
        // writing on top of other writing
        for (int i = 0; i < list.Count; i++)
            for (int j = i + 1; j < list.Count; j++)
            {
                var a = list[i]; var b = list[j]; var x = RectangleF.Intersect(a.Ink, b.Ink); if (x.IsEmpty) continue;
                float area = x.Width * x.Height, min = Math.Min(a.Ink.Width * a.Ink.Height, b.Ink.Width * b.Ink.Height); if (min < 1 || area < min * 0.18f) continue;
                if (a.S == b.S && Math.Abs(a.Ink.X - b.Ink.X) < 3 && Math.Abs(a.Ink.Y - b.Ink.Y) < 3) continue;   // the same word drawn twice on purpose
                Report(name + ": \"" + Short(a.S) + "\" is on top of \"" + Short(b.S) + "\"");
            }
    }
    static string Short(string s) { s = s.Replace('\n', ' '); return s.Length > 38 ? s.Substring(0, 36) + ".." : s; }

    [STAThread]
    static void Main(string[] args)
    {
        string data = StudioEnv.DataDir; string bak = Path.Combine(Path.GetTempPath(), "audit-backup-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(bak);
        foreach (var f in new[] { "settings.txt", "profile.txt", "projects.txt", "synthglobal.txt" }) { try { if (File.Exists(Path.Combine(data, f))) File.Copy(Path.Combine(data, f), Path.Combine(bak, f)); } catch { } }
        try
        {
            Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); eng = app.StudioEng; foreach (var m in eng.MasterFx) m.Bypass = true;
            eng.Tracks.Add(StudioEngine.NewTrack("Lead vocal", "vocal", Clip(60))); eng.Tracks.Add(StudioEngine.NewTrack("Beat", "beat", Clip(60))); eng.Tracks.Add(StudioEngine.NewTrack("Keys", "audio", null));
            sf = new StudioForm(app); CommunityPresets.BaseUrl = "http://127.0.0.1:1"; var sizes = new[] { new Size(1240, 680), new Size(1366, 768), new Size(1600, 900), new Size(1920, 1080), new Size(2560, 1440) };
            foreach (var sz in sizes)
            {
                sf.ClientSize = sz; sf.Show(); Pump(350); sizeTag = sz.Width + "x" + sz.Height; Console.WriteLine("-- " + sizeTag);
                var lead = eng.Tracks[0]; lead.Fx.Clear(); lead.FadeIn = 44100 * 2; lead.ClipGainDb = -3f; SetF("selTrack", 0); SetF("tab", 0);
                var split = typeof(StudioForm).GetMethod("SplitTrack", BF); split.Invoke(sf, new object[] { lead, 44100L * 20 }); SetF("toolsTrack", lead); eng.Seek(44100L * 8);
                Audit("mix: tracks");
                // the logo on the bottom bar is always in the middle, wherever you are in the song
                foreach (var spp in new[] { 4000.0, 600.0 }) { SetF("autoFit", false); SetF("spp", spp); SetF("scroll", (long)(44100L * 50)); SetF("scroll", Math.Max(0L, (long)(44100L * 60 - 2000 * spp))); Pump(50); Audit("mix: scrolled"); CheckLogo(); }
                SetF("autoFit", true); M("FitView");
                // every plugin's own panel
                string[] ids = { "eq", "comp", "mbcomp", "limiter", "gate", "duck", "deess", "exciter", "sat", "dist", "reverb", "delay", "chorus", "widener", "denoise", "pitch", "8d", "bassboost", "lib_worldwide_echo", "lib_melody_tune", "lib_rap_ad_lib_echo" };
                lead.Fx.Clear(); foreach (var id in ids) { var f = FxRegistry.Create(id); if (f == null) continue; f.Prepare(44100); lead.Fx.Add(f); }
                SetF("selTrack", 0); for (int i = 0; i < lead.Fx.Count; i++) { SetF("selFx", i); Audit("plugin panel: " + lead.Fx[i].Type); }
                lead.Fx.Clear(); SetF("selFx", -1);
                // the master chain on the Mastering page, the Lyrics page, My Projects and Community
                SetF("tab", 5); Audit("master visualiser"); SetF("tab", 1); SetF("mpTab", 1); Audit("mastering presets"); SetF("mpBackup", new string[0]); SetF("mpPreviewIdx", 5); Audit("mastering presets while trying one"); SetF("mpBackup", null); SetF("mpPreviewIdx", -1); SetF("mpTab", 0); { var avs = new AmVersion[3]; for (int q = 0; q < 3; q++) avs[q] = new AmVersion { Name = "CLEAN & BALANCED", Blurb = "Natural and clear: your mix with its tone balanced, light glue and a safe level for streaming.", Eq = "bass -1.2 dB, low-mids (mud) +0.5 dB, presence +1.5 dB, air +1.0 dB", Lufs = -14.1, Base = 10 }; SetF("amVersions", avs); SetF("amLines", new[] { "Your mix: -16.3 LUFS, true peak -1.2 dBTP, loudness range 7.4 LU.", "The EQ was made for you: bass -1.2 dB, presence +1.5 dB, air +1.0 dB." }); SetF("amOrigResult", new LoudnessResult { I = -16.3 }); SetF("amApplied", 1); SetF("amOpen", true); Audit("auto mix sheet"); SetF("amOpen", false); SetF("amApplied", -2); } SetF("tab", 0); SetF("themeOpen", true); Audit("theme sheet"); SetF("themeOpen", false); SetF("tab", 1); for (int i = 0; i < eng.MasterFx.Count; i++) { SetF("selMaster", i); Audit("mastering: " + eng.MasterFx[i].Type); } SetF("tab", 2); Audit("lyrics"); SetF("tab", 3); Audit("my projects"); SetF("tab", 4); Audit("community"); SetF("tab", 0); for (int ms = 0; ms < 8; ms++) { SetF("menuOpen", true); SetF("menuSec", ms); Audit("side menu section " + ms); } SetF("menuOpen", false); SetF("menuSec", -1); SetF("tab", 3); typeof(StudioForm).GetField("previewPath", BF).SetValue(sf, "x.ytmproj"); typeof(StudioForm).GetField("previewName", BF).SetValue(sf, "A project with quite a long name to preview"); typeof(StudioForm).GetField("previewPlayFn", BF).SetValue(sf, (Action<StudioEngine>)(x => { })); Audit("my projects with the preview bar"); typeof(StudioForm).GetField("previewPath", BF).SetValue(sf, ""); typeof(StudioForm).GetField("previewPlayFn", BF).SetValue(sf, null); SetF("projSearch", "a long search text to check the box"); SetF("projFav", true); SetF("projType", "Vocals"); SetF("projSort", 5); Audit("my projects with search and filters"); SetF("projSearch", ""); SetF("projFav", false); SetF("projType", ""); SetF("projSort", 0); { var sb0 = new List<ProjectLibrary.BinItem>(ProjectLibrary.Bin); ProjectLibrary.Bin.Clear(); for (int bi = 0; bi < 3; bi++) ProjectLibrary.Bin.Add(new ProjectLibrary.BinItem { Id = "q" + bi, Name = "A project with a quite long name number " + bi, Kind = "Song", Tracks = 4, Bpm = 120, Seconds = 100, Deleted = DateTime.Now.AddDays(-bi * 14) }); SetF("projBin", true); Audit("recycling bin"); ProjectLibrary.Bin.Clear(); Audit("recycling bin (empty)"); SetF("projBin", false); ProjectLibrary.Bin.AddRange(sb0); } SetF("tab", 0);
                // sheets and panels
                M("OpenNewTrackSheet", 0); Audit("new track sheet"); SetF("ntOpen", false); M("OpenBusSheet"); Audit("bus sheet"); SetF("busOpen", false);
                M("OpenStyles"); Audit("vocal styles sheet"); SetF("stylesOpen", false);
                lead.Fx.Clear(); var wf = FxRegistry.Create("lib_worldwide_echo"); wf.Prepare(44100); lead.Fx.Add(wf); M("OpenAuto", lead, wf, wf.Find("fb")); Audit("automation sheet"); SetF("autoOpen", false); lead.Fx.Clear();
                SetF("toolsTrack", lead); M("OpenGain"); Audit("gain panel"); SetF("gainOpen", false);
                M("PickTrackMenu", "Split vocals + beat: pick the track", (Func<Track, bool>)(t => true), (Action<Track>)(t => { })); Audit("track picker"); SetF("pickOpen", false);
                M("OpenCommunityPresets", "fx:bassboost", "Bass Boost", (Action<CommPreset>)(p => { }), (Func<string>)(() => "a=1"), "x"); Pump(600);
                var fake = new List<CommPreset>(); for (int i = 0; i < 6; i++) fake.Add(new CommPreset { Id = "p" + i, Kind = "fx:bassboost", Name = "Deep 808 Sub " + i, Author = "Heath", Uses = 900 - i * 100, Data = "freq=50" }); SetF("cpList", fake); SetF("cpStatus", ""); SetF("cpLoading", false); Audit("community presets sheet"); SetF("cpOpen", false);
                SetF("cutMark", 44100L * 10); Audit("cut mark"); SetF("cutMark", -1L);
                M("OpenSounds"); Pump(300); Audit("sounds panel"); M("CloseSounds");
                M("OpenMenu"); Audit("main menu"); M("CloseMenu");
                // the needle moving and the count in
                int ov = Prefs.OutVol; Prefs.OutVol = 0; eng.LastCountFrames = 88200; typeof(StudioEngine).GetField("countLeft", BF).SetValue(eng, 60000L); eng.Playing = true; Audit("count in"); typeof(StudioEngine).GetField("countLeft", BF).SetValue(eng, 0L); eng.LastCountFrames = 0; eng.Playing = false; Prefs.OutVol = ov;
                SetF("micArmed", true); Audit("mic ready"); SetF("micArmed", false); SetF("LaneH", 84); Audit("thin tracks"); SetF("LaneH", 104); Audit("compact tracks"); SetF("LaneH", 168); Audit("tall tracks"); SetF("LaneH", 132);
                // flex pitch
                SetF("selTrack", 0); SetF("toolsTrack", lead); M("FlexOpen"); var t0 = DateTime.Now; while (F<bool>("flexAnalysing") && (DateTime.Now - t0).TotalSeconds < 40) Pump(100); Pump(300); Audit("flex pitch"); M("FlexClose", false);
                M("TakeSnapshot"); // keep the project tidy for the next size
            }
            sf.Close();
            AuditWindows(app);
        }
        finally { foreach (var f in new[] { "settings.txt", "profile.txt", "projects.txt", "synthglobal.txt" }) { try { if (File.Exists(Path.Combine(bak, f))) File.Copy(Path.Combine(bak, f), Path.Combine(data, f), true); } catch { } } }
        Console.WriteLine(problems == 0 ? "=== screen audit passed (" + screens + " screens) ===" : "=== " + problems + " FAILED (" + screens + " screens) ===");
        Environment.Exit(problems == 0 ? 0 : 1);
    }

    // ---- ordinary windows (the main window, Settings): every button, tick box and label fits and nothing sits on anything else ----
    static void WinAudit(string name, Control root, Func<Control, string> textOf, float labelAbove)
    {
        var kids = new List<Control>(); foreach (Control c in root.Controls) if (c.Visible && c.Width > 0 && c.Height > 0) kids.Add(c);
        foreach (var c in kids)
        {
            string t = c.Text; if (string.IsNullOrEmpty(t) || !(c is Button || c is CheckBox || c is Label)) continue;
            if (c is Label && ((Label)c).AutoEllipsis) continue;   // a label that shortens itself on purpose (a long file path)
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.WordBreak; var sz = TextRenderer.MeasureText(t, c.Font, new Size(c.Width - (c is CheckBox ? 24 : (c is Button ? 8 : 0)), 0), flags);
            if (sz.Height > c.Height + 3) Report(name + ": \"" + Short(t) + "\" does not fit its " + c.GetType().Name + " (" + c.Width + "x" + c.Height + ", needs " + sz.Height + " px of height)");
            else if (c is Button || c is CheckBox) { var one = TextRenderer.MeasureText(t, c.Font, new Size(100000, 100), TextFormatFlags.NoPadding); if (one.Width > c.Width - (c is CheckBox ? 22 : 8)) Report(name + ": \"" + Short(t) + "\" is wider than its button (" + one.Width + " > " + c.Width + ")"); }
        }
        for (int i = 0; i < kids.Count; i++) for (int j = i + 1; j < kids.Count; j++)
        {
            var a = kids[i]; var b = kids[j]; if (a is TrackBar || b is TrackBar) continue; var x = Rectangle.Intersect(a.Bounds, b.Bounds); if (x.IsEmpty) continue; double area = x.Width * (double)x.Height, min = Math.Min(a.Width * (double)a.Height, b.Width * (double)b.Height);
            if (area > min * 0.1 && area > 30) Report(name + ": \"" + Short(a.Text) + "\" (" + a.GetType().Name + ") overlaps \"" + Short(b.Text) + "\" (" + b.GetType().Name + ")");
        }
        foreach (var c in kids) if (c.Right > root.ClientSize.Width + 1 || c.Left < -1) Report(name + ": \"" + Short(c.Text) + "\" runs out of the window");
    }
    static void AuditWindows(App app)
    {
        var bgF = typeof(App).GetField("bg", BF); var lblF = typeof(App).GetField("lbls", BF); var uiS = typeof(App).GetField("uiS", BF); app.Show(); Pump(500);
        foreach (var sz in new[] { new Size(640, 712), new Size(820, 900), new Size(1100, 1000), new Size(1500, 1000), new Size(1920, 1050) })
        {
            app.ClientSize = sz; Pump(500); sizeTag = "main window " + sz.Width + "x" + sz.Height; screens++; var bg = (Control)bgF.GetValue(app); WinAudit("main window", bg, c => c.Text, 21);
            // the writing above each box (painted on the background): it must fit beside its neighbours and not touch the boxes
            float S = (float)uiS.GetValue(app); var lbls = (List<KeyValuePair<Control, string>>)lblF.GetValue(app); var rects = new List<KeyValuePair<string, Rectangle>>();
            using (var f = new Font(Win.UiFamily, 9.5f * S)) foreach (var kv in lbls) { string t = Win.Up(kv.Value); var m = TextRenderer.MeasureText(t, f, new Size(100000, 100), TextFormatFlags.NoPadding); rects.Add(new KeyValuePair<string, Rectangle>(t, new Rectangle(kv.Key.Left, (int)(kv.Key.Top - 21 * S), m.Width, (int)(m.Height * 0.8f)))); }
            for (int i = 0; i < rects.Count; i++)
            {
                if (rects[i].Value.Right > bg.ClientSize.Width) Report("main window label \"" + rects[i].Key + "\" runs out of the window");
                for (int j = i + 1; j < rects.Count; j++) if (rects[i].Value.IntersectsWith(rects[j].Value)) Report("main window labels \"" + rects[i].Key + "\" and \"" + rects[j].Key + "\" run into each other");
                foreach (Control c in bg.Controls) if (c.Visible && rects[i].Value.IntersectsWith(c.Bounds)) Report("main window label \"" + rects[i].Key + "\" is on top of a " + c.GetType().Name + " \"" + Short(c.Text) + "\"");
            }
            try { var bm = new Bitmap(app.ClientSize.Width, app.ClientSize.Height); app.DrawToBitmap(bm, new Rectangle(Point.Empty, bm.Size)); Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "au_forms")); bm.Save(Path.Combine(Path.GetTempPath(), "au_forms", "main-" + sz.Width + "x" + sz.Height + ".png"), System.Drawing.Imaging.ImageFormat.Png); bm.Dispose(); } catch { }
        }
        app.ClientSize = new Size(640, 712); Pump(300); app.Hide();
        var st = new SettingsForm(app); st.Show(); Pump(500); sizeTag = "settings"; for (int pi = 0; pi < 7; pi++) { typeof(SettingsForm).GetMethod("ShowPage", BF).Invoke(st, new object[] { pi }); Pump(100); screens++; WinAudit("settings page " + pi, st, c => c.Text, 0); } st.Close();
    }
    static void CheckLogo()
    {
        var regs = ((IEnumerable)typeof(StudioForm).GetField("regs", BF).GetValue(sf)).Cast<HitReg>().ToList(); float H = sf.ClientSize.Height;
        var bar = regs.FindAll(r => r.R.Y > H - 46 && r.R.Height > 25 && r.R.Height < 40 && r.R.Width > 300).OrderByDescending(r => r.R.Width).FirstOrDefault();   // the strip between the arrows
        var logo = regs.FindAll(r => r.R.Y > H - 46 && r.R.Width > 30 && r.R.Width < 60 && r.R.Height > 24 && r.OnDouble != null).FirstOrDefault();
        if (bar == null || logo == null) { Report("bottom bar: could not find the bar or its logo"); return; }
        float barMid = bar.R.X + bar.R.Width / 2, logoMid = logo.R.X + logo.R.Width / 2; if (logoMid < bar.R.X + 4 || logoMid > bar.R.Right - 4) Report("bottom bar: the logo is at " + (int)logoMid + " which is off the bar (it rides on the bar between the two arrows)");
        if (bar.R.Bottom > H + 1 || bar.R.X < 0 || bar.R.Right > sf.ClientSize.Width + 1) Report("bottom bar: it does not fit the window");
    }
}
