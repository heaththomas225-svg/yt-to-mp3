using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

// Paints the screens into picture files so they can be looked at (text running out of boxes, overlaps...).   csc /main:RenderT ... RenderCheck.cs     run: RenderT.exe <output folder>
static class RenderT
{
    static StudioForm sf; static string outDir; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static void Shot(string name)
    {
        sf.Invalidate(); sf.Update(); Application.DoEvents();
        var bmp = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height);
        using (var g = Graphics.FromImage(bmp)) typeof(StudioForm).GetMethod("PaintAll", BF).Invoke(sf, new object[] { new PaintEventArgs(g, new Rectangle(Point.Empty, sf.ClientSize)) });
        string p = Path.Combine(outDir, name + ".png"); bmp.Save(p, ImageFormat.Png); bmp.Dispose(); Console.WriteLine("saved " + p);
    }
    static void Hover(int x, int y) { typeof(StudioForm).GetMethod("OnMouseMove", BF | BindingFlags.DeclaredOnly).Invoke(sf, new object[] { new MouseEventArgs(MouseButtons.None, 0, x, y, 0) }); }
    static AudioClip Clip(int sec) { int n = 44100 * sec; var x = new float[n]; for (int i = 0; i < n; i++) x[i] = (float)(0.5 * Math.Sin(i * 0.05) * Math.Exp(-(i % 22050) / 6000.0)); return AudioClip.FromArrays(x, (float[])x.Clone(), "test"); }

    [STAThread]
    static void Main(string[] args)
    {
        outDir = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "renders"); Directory.CreateDirectory(outDir);
        string data = StudioEnv.DataDir; string bak = Path.Combine(Path.GetTempPath(), "render-backup-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(bak);
        foreach (var f in new[] { "settings.txt", "profile.txt", "projects.txt", "synthglobal.txt" }) { try { if (File.Exists(Path.Combine(data, f))) File.Copy(Path.Combine(data, f), Path.Combine(bak, f)); } catch { } }
        try
        {
            Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng;
            eng.Tracks.Add(StudioEngine.NewTrack("Lead vocal", "vocal", Clip(60))); eng.Tracks.Add(StudioEngine.NewTrack("Beat", "beat", Clip(60)));
            sf = new StudioForm(app); int[][] sizes = { new[] { 1500, 800 }, new[] { 1366, 768 }, new[] { 1240, 680 } };
            foreach (var sz in sizes)
            {
                sf.ClientSize = new Size(sz[0], sz[1]); sf.Show(); Pump(400); string tag = sz[0] + "x" + sz[1];
                var tr = eng.Tracks[0]; tr.Fx.Clear(); foreach (var id in new[] { "lib_worldwide_echo", "lib_melody_tune", "lib_rap_ad_lib_echo" }) { var f = FxRegistry.Create(id); f.Prepare(44100); tr.Fx.Add(f); }
                SetF("selTrack", 0); SetF("selFx", 0); Shot(tag + "-panel-worldwide-echo");
                SetF("selFx", 1); Shot(tag + "-panel-melody-tune");
                // the busy box
                SetF("busy", true); SetF("busyText", "Separating vocals and beat with AI...\n(under a minute with a good graphics card)"); SetF("busyPct", 0.3f); SetF("busyCancelable", true); SetF("busyStart", DateTime.Now.AddSeconds(-20)); Shot(tag + "-busy-separating");
                SetF("busyText", "Setting up the separation engine (one time)..."); Shot(tag + "-busy-setup"); SetF("busy", false); SetF("busyPct", -1f);
                // the vocal styles sheet
                typeof(StudioForm).GetMethod("OpenStyles", BF).Invoke(sf, null); Shot(tag + "-styles"); SetF("stylesScroll", 640f); Shot(tag + "-styles-scroll1"); SetF("stylesScroll", 1500f); Shot(tag + "-styles-scroll2"); SetF("stylesScroll", 0f); SetF("stylesGenre", "Cloud & Lo-Fi"); Shot(tag + "-styles-cloud"); SetF("stylesGenre", ""); SetF("stylesQuery", "zzz"); Shot(tag + "-styles-none"); SetF("stylesQuery", ""); SetF("stylesFav", true); Shot(tag + "-styles-favourites-empty"); SetF("stylesOpen", false);
                // the automation sheet
                var wfx = tr.Fx[0]; typeof(StudioForm).GetMethod("OpenAuto", BF).Invoke(sf, new object[] { tr, wfx, wfx.Find("fb") }); Shot(tag + "-automation");
                var cfx = tr.Fx[1]; typeof(StudioForm).GetMethod("OpenAuto", BF).Invoke(sf, new object[] { tr, cfx, cfx.Find("harm") }); Shot(tag + "-automation-choice"); SetF("autoOpen", false);
                tr.Auto.Clear(); tr.Fx.Clear();
                // clips: split twice, fades, gain, lock; then the panels and sheets that belong to them
                object snap = typeof(StudioForm).GetMethod("TakeSnapshot", BF).Invoke(sf, null);
                var split = typeof(StudioForm).GetMethod("SplitTrack", BF); long sr = 44100;
                split.Invoke(sf, new object[] { tr, sr * 20 });
                Track mid = null; foreach (var t in eng.Tracks.ToArray()) if (t.Host == tr && t.Offset >= sr * 19) mid = t;
                if (mid != null) split.Invoke(sf, new object[] { mid, sr * 40 });
                Track last = null; foreach (var t in eng.Tracks.ToArray()) if (t.Host == tr && t.Offset >= sr * 39) last = t;
                tr.FadeIn = sr * 3; if (mid != null) { mid.ClipGainDb = -6; mid.FadeIn = sr * 2; } if (last != null) { last.FadeOut = sr * 5; last.Locked = true; }
                eng.Seek(sr * 8); SetF("scroll", 0L); SetF("autoFit", true); SetF("selTrack", 0); SetF("toolsTrack", mid); Shot(tag + "-clips-fades-gain-lock");
                SetF("toolsTrack", last); Shot(tag + "-clips-locked-selected");
                SetF("toolsTrack", mid); typeof(StudioForm).GetMethod("OpenGain", BF).Invoke(sf, null); Shot(tag + "-gain-panel"); SetF("gainOpen", false);
                SetF("cutMark", sr * 10); Shot(tag + "-cut-mark"); SetF("cutMark", -1L);
                typeof(StudioForm).GetMethod("PickTrackMenu", BF).Invoke(sf, new object[] { "Split vocals + beat: pick the track", (Func<Track, bool>)(t => true), (Action<Track>)(t => { }) }); Shot(tag + "-pick-sheet"); SetF("pickOpen", false);
                typeof(StudioForm).GetMethod("OpenNewTrackSheet", BF).Invoke(sf, new object[] { 0 }); Shot(tag + "-new-track"); SetF("ntScroll", 600f); Shot(tag + "-new-track-scrolled"); SetF("ntOpen", false);
                SetF("gridFlashAt", Environment.TickCount); Shot(tag + "-grid-size-flash"); SetF("gridFlashAt", -100000);
                // the bottom bar while music plays (nothing is actually played: the volume is 0 and the levels are faked)
                Shot(tag + "-bar-idle");
                { var vh = (float[])typeof(StudioForm).GetField("vizHist", BF).GetValue(sf); var vb = (float[])typeof(StudioForm).GetField("vizBand", BF).GetValue(sf); for (int i = 0; i < vh.Length; i++) vh[i] = (float)(0.25 + 0.55 * Math.Abs(Math.Sin(i * 0.11)) * Math.Abs(Math.Cos(i * 0.037))); for (int i = 0; i < vb.Length; i++) vb[i] = (float)(0.3 + 0.6 * Math.Abs(Math.Sin(i * 0.5))); }
                int ov = Prefs.OutVol; Prefs.OutVol = 0; eng.Playing = true; Shot(tag + "-bar-playing"); eng.Playing = false; Prefs.OutVol = ov;
                // community presets sheet (a made-up list, the website is not asked)
                CommunityPresets.BaseUrl = "http://127.0.0.1:1";
                typeof(StudioForm).GetMethod("OpenCommunityPresets", BF).Invoke(sf, new object[] { "fx:bassboost", "Bass Boost", (Action<CommPreset>)(p => { }), (Func<string>)(() => "a=1"), "Bass Boost custom" }); Pump(900);
                var fake = new List<CommPreset>(); string[] fn = { "Deep 808 Sub", "Club Thump", "Warm & Round", "Punchy Kick for fast drill drums with a really long name", "Heavy Trap", "Subtle Lift", "Bass Guitar Body", "Small Speakers" }; string[] fa = { "Heath", "Producer", "DJ Neon Wolf With A Very Long Name", "Ann", "Bob", "Cy", "Dee", "Eve" };
                for (int i = 0; i < fn.Length; i++) fake.Add(new CommPreset { Id = "p" + i, Kind = "fx:bassboost", Name = fn[i], Author = fa[i], Uses = 1200 - i * 150, Data = "freq=50" });
                SetF("cpList", fake); SetF("cpStatus", ""); SetF("cpLoading", false); Shot(tag + "-community-presets");
                SetF("cpQuery", "zzz"); Shot(tag + "-community-presets-nosearch"); SetF("cpQuery", ""); SetF("cpList", new List<CommPreset>()); Shot(tag + "-community-presets-empty");
                SetF("cpStatus", "Could not reach the Purphex community. Check your internet connection and try again."); Shot(tag + "-community-presets-error"); SetF("cpOpen", false);
                // the count in (with the logo) and the armed microphone; nothing is played: the volume is 0
                { int ov2 = Prefs.OutVol; Prefs.OutVol = 0; eng.LastCountFrames = 88200; typeof(StudioEngine).GetField("countLeft", BF).SetValue(eng, 60000L); eng.Playing = true; Shot(tag + "-count-in");
                  typeof(StudioEngine).GetField("countLeft", BF).SetValue(eng, 20000L); Shot(tag + "-count-in-4"); eng.Playing = false; typeof(StudioEngine).GetField("countLeft", BF).SetValue(eng, 0L); eng.LastCountFrames = 0; Prefs.OutVol = ov2; }
                SetF("micArmed", true); Shot(tag + "-mic-ready"); SetF("micArmed", false);
                { SetF("toast", ""); Shot(tag + "-pre-hover"); var micBtn = ((IEnumerable)typeof(StudioForm).GetField("regs", BF).GetValue(sf)).Cast<HitReg>().ToList().Find(r => r.R.Y < 100 && r.R.Y > 70 && Math.Abs(r.R.Width - 60) < 1 && r.R.X > 400);
                  if (micBtn != null) { Hover((int)micBtn.R.X + 10, (int)micBtn.R.Y + 10); Shot(tag + "-hint-long-mic"); }
                  var tools = ((IEnumerable)typeof(StudioForm).GetField("regs", BF).GetValue(sf)).Cast<HitReg>().ToList().Find(r => r.R.Y > 120 && r.R.Y < 160 && r.R.Width > 60 && r.R.Width < 75 && r.R.X > 200);
                  if (tools != null) { Hover((int)tools.R.X + 10, (int)tools.R.Y + 8); Shot(tag + "-hint-tool"); } Hover(5, 600); }
                // the needle: beam, head, sparks (volume 0: nothing is played), the ruler's neon border
                { int ov3 = Prefs.OutVol; Prefs.OutVol = 0; eng.Playing = true; eng.Seek(44100L * 12); SetF("scroll", 0L); for (int i = 0; i < 14; i++) { Pump(70); sf.Invalidate(); sf.Update(); } Shot(tag + "-needle-playing"); eng.Playing = false; Pump(150); Prefs.OutVol = ov3; Shot(tag + "-needle-idle"); }
                // a selection box and clips lit by it
                { var ms = (List<Track>)typeof(StudioForm).GetField("multiSel", BF).GetValue(sf); ms.Clear(); foreach (var t in eng.Tracks) if (!t.IsBus && t.Clip != null) ms.Add(t); SetF("marqueeOn", true); SetF("marqueeMoved", true); SetF("marqueeA", new PointF(420, 260)); SetF("marqueeB", new PointF(760, 420)); Shot(tag + "-selection"); SetF("marqueeOn", false); SetF("marqueeMoved", false); ms.Clear(); }
                // Flex Pitch with its new scroll bar
                SetF("toolsTrack", tr); SetF("selTrack", 0); typeof(StudioForm).GetMethod("FlexOpen", BF).Invoke(sf, null);
                var t0 = DateTime.Now; while ((bool)typeof(StudioForm).GetField("flexAnalysing", BF).GetValue(sf) && (DateTime.Now - t0).TotalSeconds < 40) Pump(100); Pump(300); Shot(tag + "-flex-pitch");
                typeof(StudioForm).GetMethod("FlexClose", BF).Invoke(sf, new object[] { false });
                typeof(StudioForm).GetMethod("Restore", BF).Invoke(sf, new object[] { snap });
            }
            sf.Close();
            { var sfx = new SynthFx(); sfx.Prepare(44100); var sw = new SynthWindow(sfx, null); sw.Show(); Pump(300); var cv = (Control)typeof(SynthWindow).GetField("canvas", BF).GetValue(sw); var T = typeof(SynthCanvas);
              var fk = new List<CommPreset>(); for (int i = 0; i < 6; i++) fk.Add(new CommPreset { Id = "p" + i, Kind = "synth", Name = "Wobble sound number " + (i + 1) + (i == 2 ? " with a really really long name that must not run out of the row" : ""), Author = i == 1 ? "DJ Neon Wolf With A Very Long Name" : "Heath", Uses = 900 - i * 100, Data = "osca=1" });
              T.GetField("tab", BF).SetValue(cv, 0); T.GetField("browseOpen", BF).SetValue(cv, true); T.GetField("browseCat", BF).SetValue(cv, "Community"); T.GetField("commList", BF).SetValue(cv, fk); T.GetField("browseMsg", BF).SetValue(cv, "Loaded Wobble sound number 1 by Heath. Press SAVE AS to keep your own copy."); cv.Invalidate(); Pump(200);
              var bmp = new Bitmap(sw.ClientSize.Width, sw.ClientSize.Height); sw.DrawToBitmap(bmp, new Rectangle(0, 0, sw.Width, sw.Height)); bmp.Save(Path.Combine(outDir, "synth-community.png"), ImageFormat.Png); bmp.Dispose(); sw.Close(); }
            // dialogs with their text boxes: a name box and the Bass Lab quick value box (shown without waiting, so the render never blocks)
            { var host = new Form { Size = new Size(500, 300), StartPosition = FormStartPosition.CenterScreen }; host.Show(); Pump(200);
              var qd = StudioDialogs.Make("Studio", 440, 140); qd.Controls.Add(new Label { Text = "Name your preset (everyone can see and use it):", Left = 16, Top = 14, Width = 400, Height = 22 }); var qb = new TextBox { Left = 16, Top = 42, Width = 404, Text = "Bass Boost custom" }; qd.Controls.Add(qb); Win.NeonBox(qb); qd.Controls.Add(StudioDialogs.B("OK", 230, 86, 90, 32, Color.FromArgb(118, 52, 212))); qd.Show(); Pump(400);
              var b1 = new Bitmap(qd.ClientSize.Width, qd.ClientSize.Height); qd.DrawToBitmap(b1, new Rectangle(0, 0, qd.Width, qd.Height)); b1.Save(Path.Combine(outDir, "dialog-ask.png"), ImageFormat.Png); b1.Dispose(); qd.Close();
              StudioDialogs.QuickValue(host, "Frequency (Hz):", "70", x => { }, () => { }); Pump(500); var qf = Form.ActiveForm; if (qf != null && qf != host) { var b2 = new Bitmap(qf.ClientSize.Width, qf.ClientSize.Height); qf.DrawToBitmap(b2, new Rectangle(0, 0, qf.Width, qf.Height)); b2.Save(Path.Combine(outDir, "dialog-quickvalue.png"), ImageFormat.Png); b2.Dispose(); qf.Close(); } host.Close(); }
            // the main window (phone mode is gone) and Settings (with Scan for updates)
            app.Show(); Pump(400); { var b = new Bitmap(app.ClientSize.Width, app.ClientSize.Height); app.DrawToBitmap(b, new Rectangle(Point.Empty, b.Size)); b.Save(Path.Combine(outDir, "main-window.png"), ImageFormat.Png); b.Dispose(); } app.Hide();
            var st = new SettingsForm(app); st.Show(); Pump(400); { var b = new Bitmap(st.ClientSize.Width, st.ClientSize.Height); st.DrawToBitmap(b, new Rectangle(Point.Empty, b.Size)); b.Save(Path.Combine(outDir, "settings-top.png"), ImageFormat.Png); b.Dispose(); st.AutoScrollPosition = new Point(0, 2000); Pump(200); b = new Bitmap(st.ClientSize.Width, st.ClientSize.Height); st.DrawToBitmap(b, new Rectangle(Point.Empty, b.Size)); b.Save(Path.Combine(outDir, "settings-bottom.png"), ImageFormat.Png); b.Dispose(); } st.Close();
        }
        finally { foreach (var f in new[] { "settings.txt", "profile.txt", "projects.txt", "synthglobal.txt" }) { try { if (File.Exists(Path.Combine(bak, f))) File.Copy(Path.Combine(bak, f), Path.Combine(data, f), true); } catch { } } }
        Environment.Exit(0);
    }
}
