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
        StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_render_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(StudioEnv.DataDir);   // its own data folder (nothing real is touched, and no sound is played)
        string data = StudioEnv.DataDir; string bak = Path.Combine(Path.GetTempPath(), "render-backup-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(bak);
        foreach (var f in new[] { "settings.txt", "profile.txt", "projects.txt", "synthglobal.txt" }) { try { if (File.Exists(Path.Combine(data, f))) File.Copy(Path.Combine(data, f), Path.Combine(bak, f)); } catch { } }
        try
        {
            Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng;
            eng.Tracks.Add(StudioEngine.NewTrack("Lead vocal", "vocal", Clip(60))); eng.Tracks.Add(StudioEngine.NewTrack("Beat", "beat", Clip(60)));
            sf = new StudioForm(app); int[][] sizes = { new[] { 1500, 800 }, new[] { 1366, 768 }, new[] { 1240, 680 }, new[] { 1700, 1000 } };
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
                SetF("LaneH", 84); SetF("toolsTrack", null); Shot(tag + "-thin-tracks"); SetF("LaneH", 104); Shot(tag + "-compact-tracks"); SetF("LaneH", 132);
                // the Mastering tab: its own bars, the EQ picture (before / after), presets, AUTO MIX sheet, themes sheet
                {
                    var spec = (double[])typeof(StudioForm).GetField("spec", BF).GetValue(sf); var specPre = (double[])typeof(StudioForm).GetField("specPre", BF).GetValue(sf);
                    for (int k = 0; k <= 2048; k++) { double f = k * 44100.0 / 4096; double b = -20 - 14 * Math.Log10(Math.Max(1, f / 80)) + 10 * Math.Sin(k * 0.08) * Math.Exp(-f / 4000) + 5 * Math.Sin(k * 0.31); specPre[k] = b; spec[k] = b + (f < 200 ? 4.5 : (f > 8000 ? 3.0 : (f > 2000 && f < 5000 ? 2.0 : -1.2))); }
                    SetF("tab", 1); SetF("mpTab", 0); SetF("selMaster", 0); Shot(tag + "-mastering");
                    SetF("selMaster", 1); Shot(tag + "-mastering-compressor"); SetF("selMaster", 0);
                    SetF("mpTab", 1); Shot(tag + "-mastering-presets"); SetF("mpBackup", new string[0]); SetF("mpPreviewIdx", 5); Shot(tag + "-mastering-presets-trying"); SetF("mpScroll", 300f); Shot(tag + "-mastering-presets-scrolled"); SetF("mpScroll", 0f); SetF("mpBackup", null); SetF("mpPreviewIdx", -1); SetF("mpTab", 0);
                    var vers = new AmVersion[3]; string[] vn = { "CLEAN & BALANCED", "WARM & WIDE", "LOUD & PUNCHY" }; for (int i = 0; i < 3; i++) vers[i] = new AmVersion { Name = vn[i], Blurb = "Natural and clear: your mix with its tone balanced, light glue and a safe level for streaming.", Eq = "bass -1.2 dB, presence +1.5 dB, air +1.0 dB", Lufs = -14.1 + i * 2, Base = 10 };
                    SetF("amVersions", vers); SetF("amLines", new[] { "Your mix: -16.3 LUFS, true peak -1.2 dBTP, loudness range 7.4 LU.", "The EQ was made for you: bass -1.2 dB, presence +1.5 dB, air +1.0 dB." }); SetF("amOrigResult", new LoudnessResult { I = -16.3 }); SetF("amApplied", 1); SetF("amOpen", true); Shot(tag + "-automix-sheet"); SetF("amOpen", false); SetF("amApplied", -2);
                    SetF("tab", 0); Themes.Apply(0); SetF("themeOpen", true); Shot(tag + "-theme-sheet"); Pump(500); Shot(tag + "-theme-hover0"); Hover(720, 262); Shot(tag + "-theme-hover1"); Hover(721, 263); Shot(tag + "-theme-hover"); Hover(5, 5); SetF("themeScroll", 260f); Shot(tag + "-theme-sheet-scrolled"); SetF("themeOpen", false); SetF("themeScroll", 0f);
                    Themes.Apply(Themes.IndexOf("Rainbow")); Shot(tag + "-theme-rainbow"); Themes.Apply(Themes.IndexOf("Emerald") < 0 ? 0 : Themes.IndexOf("Green")); Shot(tag + "-theme-green"); Themes.Apply(0);
                }
                // the Master Visualiser page and the strip along the bottom of Mastering (the levels are faked, nothing is played)
                {
                    int ov3 = Prefs.OutVol; Prefs.OutVol = 0; var tp = eng.MasterTap; var tpp = eng.MasterTapPre; var rnd = new Random(5);
                    for (int i = 0; i < tp.Length; i++) { double bd = Math.Exp(-(i % 5512) / 1200.0); float s0 = (float)(0.55 * bd * Math.Sin(i * 0.012) + 0.25 * Math.Sin(i * 0.11) + 0.15 * Math.Sin(i * 0.43) + 0.08 * (rnd.NextDouble() - 0.5)); tp[i] = s0; tpp[i] = s0 * 0.6f; }
                    eng.MasterTapPos = 4096; eng.Playing = true; SetF("tab", 1);
                    { var vh = (float[])typeof(StudioForm).GetField("vizHist", BF).GetValue(sf); for (int i = 0; i < vh.Length; i++) vh[i] = (float)(0.25 + 0.5 * Math.Abs(Math.Sin(i * 0.11))); }
                    Shot(tag + "-mastering-stage-playing"); eng.Playing = false; Shot(tag + "-mastering-stage-idle");
                    SetF("tab", 5); Shot(tag + "-visualiser-idle"); eng.Playing = true; for (int i = 0; i < 40; i++) { sf.Invalidate(); sf.Update(); Application.DoEvents(); Pump(20); } Shot(tag + "-visualiser-playing"); eng.Playing = false; Prefs.OutVol = ov3; SetF("tab", 1);
                }
                // the profile page, the artist search and an artist's page (made-up artists: the website is not asked)
                {
                    CommunityPresets.BaseUrl = "http://127.0.0.1:1"; string pdir = Path.Combine(Path.GetTempPath(), "render-profile"); Directory.CreateDirectory(pdir); string bpic = Path.Combine(pdir, "banner.png");
                    using (var wb = new Bitmap(1600, 500)) { using (var wg = Graphics.FromImage(wb)) { using (var br = new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0, 0, 1600, 500), Color.FromArgb(255, 90, 40, 160), Color.FromArgb(255, 20, 170, 200), 20f)) wg.FillRectangle(br, 0, 0, 1600, 500); wg.FillEllipse(Brushes.Gold, 1100, 40, 260, 260); } wb.Save(bpic, ImageFormat.Png); }
                    ProfileStore.Bio = "Dark dnb and bassline from the north. New tracks every month, and always looking for singers."; ProfileStore.Location = "Leeds"; ProfileStore.Links.Clear(); ProfileStore.Links.Add("https://example.com/neonwolf"); ProfileStore.Links.Add("https://soundcloud.com/neonwolf"); ProfileStore.Links.Add("spotify://artist/123"); ProfileStore.Tags = "dnb bassline dark"; ProfileStore.Accent = -1; ProfileStore.Followers = 128;
                    SetF("profLoaded", true); SetF("tab", 6); SetF("profScroll", 0f); Shot(tag + "-profile"); typeof(StudioForm).GetMethod("SetBanner", BF).Invoke(sf, new object[] { bpic }); ProfileStore.Accent = 160; ProfileStore.Published = true; Shot(tag + "-profile-banner"); SetF("profScroll", 600f); Shot(tag + "-profile-scrolled"); SetF("profScroll", 0f);
                    var al = new List<ArtistInfo>(); string[] an = { "DJ Neon Wolf", "Mia Waves", "Sub Zero Ghost With A Very Long Name", "Kali", "Bassline Bob", "Lunar Echo", "Pixel Drift" }; for (int i = 0; i < an.Length; i++) al.Add(new ArtistInfo { Id = "a" + i.ToString("x12"), Name = an[i], Bio = i % 2 == 0 ? "Dark dnb and bassline from the north. New tracks every month." : "Pop and synths", Tags = new[] { "dnb", "bassline", "dark" }, Followers = 120 - i * 15, Tracks = 3 + i, Accent = i * 50 });
                    SetF("artList", al); SetF("artFetched", true); SetF("artSel", null); SetF("tab", 7); Shot(tag + "-artists"); SetF("artSearchOn", true); SetF("artQuery", "dnb"); Shot(tag + "-artists-search"); SetF("artSearchOn", false); SetF("artQuery", "");
                    var ai = al[0]; ai.Bio = "Dark dnb and bassline from the north. New tracks every month, and always looking for singers to work with."; ai.Location = "Leeds"; ai.Links.Add("https://example.com/neonwolf"); ai.Links.Add("https://soundcloud.com/neonwolf"); for (int i = 0; i < 6; i++) ai.TrackList.Add(new ArtistTrack { Title = "Night Drive " + (i + 1), Bpm = 174, Secs = 200 + i * 7, Tags = "dnb dark" }); SetF("artSel", ai); Shot(tag + "-artist-page"); SetF("artSel", null);
                    SetF("tab", 4); Shot(tag + "-community"); SetF("tab", 0); ProfileStore.Published = false; ProfileStore.Accent = -1; ProfileStore.BannerPath = "";
                }
                SetF("tab", 1); Shot(tag + "-mastering-back"); SetF("tab", 2); Shot(tag + "-lyrics-back"); SetF("tab", 0);
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
                // the projects page (neon cards, the logo when there is no picture, a wide picture fitted into the square), the preview bar, the theme tab
                {
                    string pd = Path.Combine(Path.GetTempPath(), "render-projects"); Directory.CreateDirectory(pd); string wide = Path.Combine(pd, "wide.png");
                    using (var wb = new Bitmap(900, 300)) { using (var wg = Graphics.FromImage(wb)) { wg.Clear(Color.FromArgb(40, 120, 200)); wg.FillEllipse(Brushes.OrangeRed, 40, 40, 220, 220); wg.FillRectangle(Brushes.Gold, 600, 20, 260, 260); } wb.Save(wide, ImageFormat.Png); }
                    ProjectLibrary.Entries.Clear(); string[] pn = { "Summer drill beat", "Late night vocals with a very long project name", "Untitled", "Wobble bass test" };
                    for (int i = 0; i < pn.Length; i++) { string fp = Path.Combine(pd, "p" + i + ".ypm"); File.WriteAllText(fp, "x"); ProjectLibrary.Entries.Add(new ProjectEntry { Path = fp, Name = pn[i], Cover = i == 1 ? wide : "", Updated = DateTime.Now.AddHours(-i * 30), Opened = DateTime.Now.AddHours(-i * 30), Tracks = 3 + i, Bpm = 120 + i * 7, Seconds = 100 + i * 30, Bytes = 1234567 * (i + 1), SizeAsked = true }); }
                    SetF("projectsLoaded", true); SetF("tab", 3); Shot(tag + "-projects");
                    ProjectLibrary.Entries[0].Fav = true; ProjectLibrary.Entries[0].Kind = "Song"; ProjectLibrary.Entries[1].Kind = "Beat"; ProjectLibrary.Entries[2].Kind = "Vocals";
                    SetF("tab", 3); Shot(tag + "-projects-filters-off"); SetF("projSearch", "bea"); SetF("projFav", true); SetF("projType", "Song"); SetF("projSort", 2); Shot(tag + "-projects-search-filters"); SetF("projSearch", "zzzz"); Shot(tag + "-projects-search-none"); SetF("projSearch", ""); SetF("projFav", false); SetF("projType", ""); SetF("projSort", 0);
                    ProjectLibrary.Bin.Clear(); string[] bnm = { "Old drill idea", "Vocal take with a really really long project name that must be cut", "Beat 7" };
                    for (int bi = 0; bi < bnm.Length; bi++) ProjectLibrary.Bin.Add(new ProjectLibrary.BinItem { Id = "x" + bi, OrigPath = "p" + bi + ".ytmproj", Name = bnm[bi], Kind = bi == 0 ? "Song" : "Beat", Tracks = 3 + bi, Bpm = 120, Seconds = 95, Deleted = DateTime.Now.AddDays(-bi * 14 - 1), Cover = bi == 1 ? wide : "" });
                    SetF("projBin", true); Shot(tag + "-recycling-bin"); ProjectLibrary.Bin.Clear(); Shot(tag + "-recycling-bin-empty"); SetF("projBin", false);
                    SetF("tab", 4); SetF("projScroll", 0f); Shot(tag + "-community-page");
                    SetF("tab", 3); typeof(StudioForm).GetField("previewPath", BF).SetValue(sf, ProjectLibrary.Entries[0].Path); typeof(StudioForm).GetField("previewName", BF).SetValue(sf, "Summer drill beat"); typeof(StudioForm).GetField("previewPlayFn", BF).SetValue(sf, (Action<StudioEngine>)(x => { }));
                    Shot(tag + "-projects-preview-bar"); typeof(StudioForm).GetField("previewPath", BF).SetValue(sf, ""); typeof(StudioForm).GetField("previewPlayFn", BF).SetValue(sf, null);
                    SetF("tab", 0); SetF("menuOpen", true); SetF("menuSec", 3); Shot(tag + "-menu-pages"); SetF("menuOpen", false);
                    Themes.Apply(1); Shot(tag + "-theme-ocean"); Themes.Apply(2); Shot(tag + "-theme-sunset"); Themes.Apply(0);
                    ProjectLibrary.Entries.Clear(); SetF("tab", 0);
                }
                SetF("micArmed", true); Shot(tag + "-mic-ready"); SetF("micArmed", false);
                { SetF("toast", ""); Shot(tag + "-pre-hover"); var micBtn = ((IEnumerable)typeof(StudioForm).GetField("regs", BF).GetValue(sf)).Cast<HitReg>().ToList().Find(r => r.R.Y < 100 && r.R.Y > 70 && Math.Abs(r.R.Width - 60) < 1 && r.R.X > 400);
                  if (micBtn != null) { Hover((int)micBtn.R.X + 10, (int)micBtn.R.Y + 10); Shot(tag + "-hint-long-mic"); }
                  var tools = ((IEnumerable)typeof(StudioForm).GetField("regs", BF).GetValue(sf)).Cast<HitReg>().ToList().Find(r => r.R.Y > 120 && r.R.Y < 160 && r.R.Width > 60 && r.R.Width < 75 && r.R.X > 200);
                  if (tools != null) { Hover((int)tools.R.X + 10, (int)tools.R.Y + 8); Shot(tag + "-hint-tool"); } Hover(5, 600); }
                // the needle: beam, head, sparks (volume 0: nothing is played), the ruler's neon border
                { int ov3 = Prefs.OutVol; Prefs.OutVol = 0; eng.Playing = true; eng.Seek(44100L * 12); SetF("scroll", 0L); for (int i = 0; i < 14; i++) { Pump(70); sf.Invalidate(); sf.Update(); } Shot(tag + "-needle-playing"); eng.Playing = false; Pump(150); Prefs.OutVol = ov3; Shot(tag + "-needle-idle"); }
                // a selection box and clips lit by it
                { var ms = (List<Track>)typeof(StudioForm).GetField("multiSel", BF).GetValue(sf); ms.Clear(); foreach (var t in eng.Tracks) if (!t.IsBus && t.Clip != null) ms.Add(t); SetF("marqueeOn", true); SetF("marqueeMoved", true); SetF("marqueeA", new PointF(420, 260)); SetF("marqueeB", new PointF(760, 420)); Shot(tag + "-selection"); SetF("marqueeOn", false); SetF("marqueeMoved", false); ms.Clear(); }
                // plugin presets sheet with REVERT, lyrics with the logo scroll bar, many tracks
                { var eq0 = FxRegistry.Create("eq"); eq0.Prepare(44100); eng.Tracks[0].Fx.Clear(); eng.Tracks[0].Fx.Add(eq0); SetF("selTrack", 0); SetF("selFx", 0); SetF("tab", 0); sf.OpenPresetSheet(eq0); Shot(tag + "-preset-sheet"); typeof(StudioForm).GetMethod("PsUse", BF).Invoke(sf, new object[] { "Test preset", "b0_g=4" }); Shot(tag + "-preset-sheet-used"); SetF("psOpen", false); Shot(tag + "-revert-bar"); SetF("revertAction", null); eng.Tracks[0].Fx.Clear();
                  ProjectIO.Lyrics = string.Join(Environment.NewLine, Enumerable.Range(1, 120).Select(i => "Line number " + i + " of the song")); ProjectIO.LyricsTitle = "A long song"; SetF("tab", 2); Shot(tag + "-lyrics-bar"); SetF("tab", 0); ProjectIO.Lyrics = ""; ProjectIO.LyricsTitle = "";
                  int before = eng.Tracks.Count; for (int q = 0; q < 12; q++) eng.Tracks.Add(StudioEngine.NewTrack("Extra " + q, "beat", Clip(10))); Shot(tag + "-many-tracks"); while (eng.Tracks.Count > before) eng.Tracks.RemoveAt(eng.Tracks.Count - 1); }
                // the guided tour points at the real button; the Help page with SHOW ME
                { SetF("tab", 0); sf.Activate(); Pump(300); ((StudioForm)sf).ShowOnScreen("ADD MUSIC"); Pump(600); try { var scr = sf.RectangleToScreen(sf.ClientRectangle); using (var bm = new Bitmap(scr.Width, scr.Height)) { using (var gg = Graphics.FromImage(bm)) gg.CopyFromScreen(scr.Location, Point.Empty, scr.Size); bm.Save(Path.Combine(outDir, tag + "-tour-add-music.png"), ImageFormat.Png); } } catch { }
                  foreach (Form of in Application.OpenForms.Cast<Form>().ToList()) if (of.Owner == sf && of.FormBorderStyle == FormBorderStyle.None) of.Close(); Pump(200); }
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
            { var hf = new HelpForm(id => true, id => { }, "studio-start"); hf.Show(); Pump(400); hf.Activate(); Pump(300); var hb = hf.Bounds; using (var bm = new Bitmap(hb.Width, hb.Height)) { using (var gg = Graphics.FromImage(bm)) gg.CopyFromScreen(hb.Location, Point.Empty, hb.Size); bm.Save(Path.Combine(outDir, "help-studio-start.png"), ImageFormat.Png); } hf.Close(); }
            // the main window (phone mode is gone) and Settings (with Scan for updates)
            app.Show(); Pump(400);
            foreach (var msz in new[] { new Size(640, 712), new Size(820, 900), new Size(1100, 1000), new Size(1500, 1000) }) { app.ClientSize = msz; Pump(500); var b0 = new Bitmap(app.ClientSize.Width, app.ClientSize.Height); app.DrawToBitmap(b0, new Rectangle(Point.Empty, b0.Size)); b0.Save(Path.Combine(outDir, "main-window-" + msz.Width + "x" + msz.Height + ".png"), ImageFormat.Png); b0.Dispose(); }
            app.Hide();
            var st = new SettingsForm(app); st.Show(); Pump(400); for (int pi = 0; pi < 7; pi++) { typeof(SettingsForm).GetMethod("ShowPage", BF).Invoke(st, new object[] { pi }); Pump(150); var b = new Bitmap(st.ClientSize.Width, st.ClientSize.Height); st.DrawToBitmap(b, new Rectangle(Point.Empty, b.Size)); b.Save(Path.Combine(outDir, "settings-page" + pi + ".png"), ImageFormat.Png); b.Dispose(); } st.Close();
        }
        finally { foreach (var f in new[] { "settings.txt", "profile.txt", "projects.txt", "synthglobal.txt" }) { try { if (File.Exists(Path.Combine(bak, f))) File.Copy(Path.Combine(bak, f), Path.Combine(data, f), true); } catch { } } }
        Environment.Exit(0);
    }
}
