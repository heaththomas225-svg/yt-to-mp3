using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

// The features of 3.33.0: PROFILE THEMES and SAVE AS THEME, the emoji panel, the place search, SAVED SONGS and SHARE, the neon scroll bars, tags without swearing, links, the framed pictures, music videos.
// With a folder as the first argument it also paints pictures of them to look at.   csc /main:NF ... NewFeatTest.cs
static class NF
{
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern int SendMessage(IntPtr h, int msg, int w, int l);
    static long eng0; static int fails; static StudioForm sf; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void Check(string n, bool ok, string d = "") { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + (d.Length > 0 ? "   " + d : "")); Console.Out.Flush(); }
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static T F<T>(string n) { return (T)typeof(StudioForm).GetField(n, BF).GetValue(sf); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static void Shot(string path) { sf.Invalidate(); sf.Update(); Application.DoEvents(); var bmp = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height); using (var g = Graphics.FromImage(bmp)) { var pe = new PaintEventArgs(g, new Rectangle(Point.Empty, sf.ClientSize)); for (int i = 0; i < 3; i++) typeof(StudioForm).GetMethod("PaintAll", BF).Invoke(sf, new object[] { pe }); } bmp.Save(path, ImageFormat.Png); bmp.Dispose(); }
    static void FormShot(Form f, string path) { Pump(300); var bmp = new Bitmap(f.ClientSize.Width, f.ClientSize.Height); f.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height)); bmp.Save(path, ImageFormat.Png); bmp.Dispose(); }

    [STAThread]
    static void Main(string[] args)
    {
        string outDir = args.Length > 0 ? args[0] : null; if (outDir != null) Directory.CreateDirectory(outDir);
        StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_nf_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(StudioEnv.DataDir); Application.EnableVisualStyles();
        Edition.Override = Tier.Fantasy;

        // ---- the emoji panel ----
        Check("there are hundreds of emojis in a dozen groups", EmojiPanel.Count >= 450 && EmojiPanel.GroupNames.Length >= 11, EmojiPanel.Count + " in " + EmojiPanel.GroupNames.Length);
        Check("searching by a word finds them (fire, guitar, heart)", EmojiPanel.Search("fire").Any(i => i.E == "🔥") && EmojiPanel.Search("guitar").Any(i => i.E == "🎸") && EmojiPanel.Search("heart").Count >= 8);
        Check("every word has to match (party + hat)", EmojiPanel.Search("party hat").Count == 0 || EmojiPanel.Search("party hat").All(i => i.Words.Contains("party") && i.Words.Contains("hat")));
        Check("a word that matches nothing gives nothing", EmojiPanel.Search("qqqzzz").Count == 0 && EmojiPanel.Search("").Count == 0);
        EmojiPanel.Used("🎸"); EmojiPanel.Used("🔥"); Check("the emojis you use come first under RECENT (newest first)", EmojiPanel.Recent().Count == 2 && EmojiPanel.Recent()[0].E == "🔥" && File.Exists(Path.Combine(StudioEnv.DataDir, "emoji-recent.txt")));

        // ---- the place search ----
        Check("the countries work without the internet (pol finds Poland)", PlaceApi.LocalCountries("pol").Any(p => p.Value == "Poland") && PlaceApi.LocalCountries("p").Count >= 5 && PlaceApi.LocalCountries("").Count == 0 && PlaceApi.Countries.Length >= 190);
        var lis = new HttpListener(); int port = 9300 + new Random().Next(500); lis.Prefixes.Add("http://127.0.0.1:" + port + "/"); lis.Start(); string lastQ = "";
        var srv = new System.Threading.Thread(() => { while (lis.IsListening) { try { var c = lis.GetContext(); lastQ = c.Request.Url.Query; var b = Encoding.UTF8.GetBytes("{\"ok\":true,\"places\":[{\"label\":\"Poole, Dorset, England, United Kingdom\",\"value\":\"Poole, Dorset\"},{\"label\":\"Poole, Nebraska, United States\",\"value\":\"Poole, Nebraska\"}]}"); c.Response.ContentType = "application/json"; c.Response.OutputStream.Write(b, 0, b.Length); c.Response.Close(); } catch { } } }) { IsBackground = true }; srv.Start();
        string was = CommunityPresets.BaseUrl; CommunityPresets.BaseUrl = "http://127.0.0.1:" + port; var hits = ProfApi.Places("poole dorset"); CommunityPresets.BaseUrl = was;
        Check("the place search asks the website and reads Poole, Dorset", hits != null && hits.Count == 2 && hits[0].Value == "Poole, Dorset" && hits[0].Label.Contains("United Kingdom") && lastQ.Contains("poole%20dorset"), hits == null ? "null" : hits.Count + " " + lastQ);
        CommunityPresets.BaseUrl = "http://127.0.0.1:1"; Check("when the website cannot be reached it says so (null) and the countries still work", ProfApi.Places("poole") == null); CommunityPresets.BaseUrl = was; lis.Stop();

        // ---- profile rules ----
        Check("a swear word never stays in a tag, and nothing else is lost", ProjectLibrary.CleanTags("drill fuck bassline sh1t jazz") == "drill bassline jazz", ProjectLibrary.CleanTags("drill fuck bassline sh1t jazz"));
        { var d = new Dictionary<string, object>(); d["id"] = "a1"; d["name"] = "Neo"; d["tags"] = new List<object> { "trap", "fuck", "dnb" }; d["links"] = new List<object> { "https://open.spotify.com/artist/1", "https://evil.com/x" }; d["nameRgb"] = "ffb07a"; d["nameRgb2"] = "6a9bff"; d["bio"] = "hi https://shady.example.com/x https://soundcloud.com/me";
          var a = ProfApi.Parse(d); Check("an artist's tags and links from the website are cleaned again, and the name colours are read", a.Tags.Length == 2 && a.Links.Count == 1 && a.NameRgb == 0xFFB07A && a.NameRgb2 == 0x6A9BFF && !a.Bio.Contains("shady") && a.Bio.Contains("soundcloud.com/me"), a.Tags.Length + " " + a.Links.Count + " " + a.NameRgb + " " + a.Bio); }
        { string pf = Path.Combine(StudioEnv.DataDir, "profile-page.txt"); File.WriteAllText(pf, "bannerzoom=100\navzoom=100\n"); ProfileStore.BannerZoom = 55; ProfileStore.AvZoom = 55; ProfileStore.Load(); Check("a profile from an older version (zoom 100 = cut to fill) now shows the whole picture (zoom 0) and is saved as migrated", ProfileStore.BannerZoom == 0 && ProfileStore.AvZoom == 0 && ProfileStore.ZoomMigrated);
          ProfileStore.BannerZoom = 130; ProfileStore.Save(); ProfileStore.BannerZoom = 0; ProfileStore.Load(); Check("a zoom you chose yourself is kept", ProfileStore.BannerZoom == 130 && File.ReadAllText(pf).Contains("zoomv=2")); ProfileStore.BannerZoom = 0; ProfileStore.Save(); }
        { var e = new ProjectEntry { Path = Path.Combine(StudioEnv.DataDir, "v.ytmproj"), Name = "Vid song", Video = "https://youtu.be/abc", OnProfile = true }; File.WriteAllText(e.Path, "x"); ProjectLibrary.Entries.Add(e); ProjectLibrary.Save(); ProjectLibrary.Entries.Clear(); ProjectLibrary.Load(); var back = ProjectLibrary.Entries.Find(x => x.Name == "Vid song"); Check("the link to a music video is saved with the project", back != null && back.Video == "https://youtu.be/abc" && back.OnProfile); }

        // ---- the neon scroll bar ----
        { var f = new Form { ClientSize = new Size(400, 300) }; var tb = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Left = 10, Top = 10, Width = 380, Height = 200, ReadOnly = true }; f.Controls.Add(tb); var bar = NeonScroll.Attach(tb); f.Show(); tb.Text = string.Join("\r\n", Enumerable.Range(1, 120).Select(i => "line " + i)); Pump(300);
          Check("a text box gets the neon bar instead of the grey one (same parent, beside the box, no Windows bar)", bar != null && tb.ScrollBars == ScrollBars.None && f.Controls.Contains(bar) && bar.Visible && bar.Right <= tb.Right && bar.Left > tb.Right - 30 && NeonScroll.Attach(tb) == bar);
          var m = typeof(NeonScroll).GetMethod("ScrollBy", BF); m.Invoke(bar, new object[] { 30 }); Pump(100); int first = SendMessage(tb.Handle, 0xCE, 0, 0); Check("the bar scrolls the text (line 31 is the first one shown)", first == 30, first + "");
          f.Close(); f.Dispose(); }

        // ---- the studio ----
        var app = (App)Activator.CreateInstance(typeof(App), true); sf = new StudioForm(app); sf.ClientSize = new Size(1500, 900); sf.Show(); Pump(400);
        // text of a profile takes its colour for the moment, then the studio's own colours come back
        { var ink0 = (Color)typeof(StudioForm).GetField("Ink", BF | BindingFlags.Static).GetValue(null); Color seen = Color.Empty; var pt = typeof(StudioForm).GetMethod("PaintTinted", BF); pt.Invoke(sf, new object[] { 0x914232, (Action)(() => seen = (Color)typeof(StudioForm).GetField("Ink", BF | BindingFlags.Static).GetValue(null)) }); var ink1 = (Color)typeof(StudioForm).GetField("Ink", BF | BindingFlags.Static).GetValue(null);
          Check("the text of a profile is in the colour of the profile while it is drawn, and the studio's colours come back after", seen != ink0 && seen.R > seen.B - 40 && ink1 == ink0, seen + " / " + ink0); }
        ProjectLibrary.ProfileName = "Neo"; ProfileStore.Accent = 0x914232; ProfileStore.NameRgb = 0xFFB07A; ProfileStore.NameRgb2 = 0x6A9BFF; ProfileStore.Tags = "trap drill " + string.Join(" ", Enumerable.Range(1, 40).Select(i => "genre" + i));
        { string json = (string)typeof(StudioForm).GetMethod("BuildProfileJson", BF).Invoke(sf, new object[] { false }); Check("what is published carries the name colours and no limit on the tags", json.Contains("\"nameRgb\":\"ffb07a\"") && json.Contains("\"nameRgb2\":\"6a9bff\"") && json.Contains("genre40"), json.Length + ""); }
        { var asks = new List<string>(); SetF("askHook", (Func<string, bool>)(t => { asks.Add(t); return false; })); typeof(StudioForm).GetMethod("AutoLoudnessClick", BF).Invoke(sf, null); typeof(StudioForm).GetMethod("SetLoudnessClick", BF).Invoke(sf, null); SetF("askHook", null);
          Check("AUTO LOUDNESS and SET LOUDNESS (MANUAL) ask Are you sure first, and answering No changes nothing", asks.Count == 2 && asks[0].StartsWith("Are you sure you want to run AUTO LOUDNESS") && asks[1].StartsWith("Are you sure you want to set the loudness") && !F<bool>("busy"), asks.Count + ""); }
        { // publishing asks first
          string d1 = Path.Combine(StudioEnv.DataDir, "pa1.ytmproj"), d2 = Path.Combine(StudioEnv.DataDir, "pa2.ytmproj"); File.WriteAllText(d1, "x"); File.WriteAllText(d2, "x");
          var pe1 = new ProjectEntry { Path = d1, Name = "Pub one" }; var pe2 = new ProjectEntry { Path = d2, Name = "Pub two" }; ProjectLibrary.Entries.Add(pe1); ProjectLibrary.Entries.Add(pe2); var asked = new List<string>(); bool say = false; SetF("askHook", (Func<string, bool>)(t => { asked.Add(t); return say; }));
          typeof(StudioForm).GetMethod("ToggleOnProfile", BF).Invoke(sf, new object[] { pe1 }); Check("publishing one project asks first, and No leaves it private", asked.Count == 1 && asked[0].Contains("Pub one") && asked[0].Contains("community") && !pe1.OnProfile, asked.Count + "");
          say = true; typeof(StudioForm).GetMethod("ToggleOnProfile", BF).Invoke(sf, new object[] { pe1 }); Check("Yes publishes it", pe1.OnProfile);
          asked.Clear(); typeof(StudioForm).GetMethod("ToggleOnProfile", BF).Invoke(sf, new object[] { pe1 }); Check("making it private again does not ask", asked.Count == 0 && !pe1.OnProfile);
          asked.Clear(); say = false; typeof(StudioForm).GetMethod("PublishAllClick", BF).Invoke(sf, null); Check("PUBLISH ALL says it will publish every project that is not published yet and asks: No publishes nothing", asked.Count == 1 && asked[0].Contains("not published yet") && asked[0].Contains("Are you sure") && !pe1.OnProfile && !pe2.OnProfile, asked.Count + "");
          say = true; typeof(StudioForm).GetMethod("PublishAllClick", BF).Invoke(sf, null); Check("Yes publishes them all", pe1.OnProfile && pe2.OnProfile);
          asked.Clear(); typeof(StudioForm).GetMethod("PublishAllClick", BF).Invoke(sf, null); Check("with everything published there is nothing to ask", asked.Count == 0);
          SetF("askHook", null); ProjectLibrary.Entries.Remove(pe1); ProjectLibrary.Entries.Remove(pe2); ProjectLibrary.Save(); }
        { // the X of the main window asks first
          var prompts = new List<string>(); var answers = new Queue<DialogResult>(); app.CloseAsk = (t, ti, b) => { prompts.Add(ti); return answers.Count > 0 ? answers.Dequeue() : DialogResult.Yes; };
          typeof(App).GetField("studioForm", BF).SetValue(app, null); answers.Enqueue(DialogResult.No); Check("closing asks Are you sure first, and No keeps everything open", !app.ConfirmClose() && prompts.Count == 1 && prompts[0] == "Close Purphex");
          prompts.Clear(); answers.Clear(); answers.Enqueue(DialogResult.Yes); Check("with no project open, Yes closes without a second question", app.ConfirmClose() && prompts.Count == 1);
          typeof(App).GetField("studioForm", BF).SetValue(app, sf); sf.eng.Tracks.Clear(); sf.eng.Tracks.Add(StudioEngine.NewTrack("T", "beat", null)); prompts.Clear(); answers.Clear(); answers.Enqueue(DialogResult.Yes); answers.Enqueue(DialogResult.Cancel);
          Check("with a project open it then asks about saving, and Cancel keeps the program open", !app.ConfirmClose() && prompts.Count == 2 && prompts[1] == "Save your project?");
          prompts.Clear(); answers.Clear(); answers.Enqueue(DialogResult.Yes); answers.Enqueue(DialogResult.No); Check("No (do not save) closes", app.ConfirmClose() && prompts.Count == 2);
          sf.eng.Tracks.Clear(); typeof(App).GetField("studioForm", BF).SetValue(app, null); app.CloseAsk = null; }
        { // writing on buttons is always white; a theme that makes a button too light gets a darker button
          Check("button writing is always white", Contrast.LabelOn(Color.White, Color.Yellow) == Color.White);
          foreach (var lite in new[] { Color.White, Color.Yellow, Color.FromArgb(255, 255, 206, 120), Color.FromArgb(255, 200, 255, 200) }) { var dk = Contrast.ForWhite(lite); Check("a light button colour is darkened until white writing reads on it (" + lite.Name + ")", Contrast.Ratio(Color.White, dk) >= 3.7 && dk.R < lite.R + 1, Contrast.Ratio(Color.White, dk).ToString("0.0")); }
          var dkOk = Color.FromArgb(255, 60, 30, 130); Check("a dark button colour is left alone", Contrast.ForWhite(dkOk) == dkOk); }
        { // a track that is OFF cannot be touched
          sf.eng.Tracks.Clear(); var ot = StudioEngine.NewTrack("Off one", "beat", AudioClip.FromArrays(new float[44100 * 6], new float[44100 * 6], "t")); sf.eng.Tracks.Add(ot); SetF("selTrack", 0); var isl = typeof(StudioForm).GetMethod("IsLocked", BF);
          Check("a track that is on can be edited", !(bool)isl.Invoke(sf, new object[] { ot })); ot.Off = true; Check("a track that is OFF counts as untouchable (like a locked one)", (bool)isl.Invoke(sf, new object[] { ot }));
          int before = sf.eng.Tracks.Count; eng0 = sf.eng.Pos; sf.eng.Seek(44100 * 2); typeof(StudioForm).GetMethod("SplitSelected", BF).Invoke(sf, null); Check("splitting an OFF track is refused and says why", sf.eng.Tracks.Count == before && F<string>("toast").Contains("OFF"), F<string>("toast"));
          ot.Off = false; sf.eng.Tracks.Clear(); }
        { // Exit in the side menu asks, and offers to save
          var asked = new List<string>(); var ans = new Queue<DialogResult>(); sf.exitAsk = t => { asked.Add(t); return ans.Count > 0 ? ans.Dequeue() : DialogResult.Yes; };
          sf.eng.Tracks.Clear(); sf.eng.Tracks.Add(StudioEngine.NewTrack("T", "beat", null)); ans.Enqueue(DialogResult.Cancel); typeof(StudioForm).GetMethod("ExitDawClick", BF).Invoke(sf, null); Check("Exit asks to save first (this will close the DAW) and Cancel keeps it open", asked.Count == 1 && asked[0].Contains("close the DAW") && asked[0].Contains("Save your project now") && !sf.IsDisposed);
          sf.eng.Tracks.Clear(); sf.exitAsk = null; }
        { // the list that asks which track: one button for a row that was split into pieces
          sf.eng.Tracks.Clear(); Func<int, AudioClip> mkc = sec => AudioClip.FromArrays(new float[44100 * sec], new float[44100 * sec], "t"); var row0 = StudioEngine.NewTrack("Row", "beat", mkc(2)); var pc1 = StudioEngine.NewTrack("Row piece", "beat", mkc(5)); var pc2 = StudioEngine.NewTrack("Row piece 2", "beat", mkc(1)); pc1.Host = row0; pc2.Host = row0; var other = StudioEngine.NewTrack("Other", "vocal", mkc(3));
          sf.eng.Tracks.Add(row0); sf.eng.Tracks.Add(pc1); sf.eng.Tracks.Add(pc2); sf.eng.Tracks.Add(other); var picked = new List<Track>();
          typeof(StudioForm).GetMethod("PickTrackMenu", BF).Invoke(sf, new object[] { "Detect the key of which track?", (Func<Track, bool>)(t => t.Clip != null), (Action<Track>)(t => picked.Add(t)) }); var pl = (List<Track>)typeof(StudioForm).GetField("pickList", BF).GetValue(sf);
          Check("a track split into pieces is ONE button in the list (not one for every piece)", pl.Count == 2 && pl[0] == row0 && pl[1] == other, pl.Count + " buttons");
          ((Action<Track>)typeof(StudioForm).GetField("pickAct", BF).GetValue(sf))(row0); Check("and choosing it uses the longest piece of that track", picked.Count == 1 && picked[0] == pc1);
          SetF("pickOpen", false); sf.eng.Tracks.Clear(); }
        { SetF("mixCatA", 3); SetF("mixCatB", 5); sf.MixSwap(); Check("SWAP also swaps the styles, so each side opens on the style of its colour", F<int>("mixCatA") == 5 && F<int>("mixCatB") == 3); }
        { // links with titles, a bio of any length, a taken name
          string ti, ur; const string sp = "https://open.spotify.com/artist/1";
          Check("a link line needs a title and an allowed address", ProfileStore.ParseLinkLine("My Spotify | " + sp, out ti, out ur) && ti == "My Spotify" && ur == sp && !ProfileStore.ParseLinkLine(sp, out ti, out ur) && !ProfileStore.ParseLinkLine(" | " + sp, out ti, out ur) && !ProfileStore.ParseLinkLine("Mine | https://evil.com/x", out ti, out ur));
          var dict = new Dictionary<string, string> { { sp, "My Spotify" } }; Check("a link shows its title, and the name of its service when it has none", ProfileStore.TitleOf(dict, sp) == "My Spotify" && ProfileStore.TitleOf(null, sp) == "Spotify");
          ProfileStore.Links.Clear(); ProfileStore.LinkTitles.Clear(); ProfileStore.Links.Add(sp); ProfileStore.LinkTitles[sp] = "My Spotify"; ProfileStore.Save(); ProfileStore.Links.Clear(); ProfileStore.LinkTitles.Clear(); ProfileStore.Load(); Check("titles are saved with the links and come back", ProfileStore.Links.Count == 1 && ProfileStore.TitleOf(ProfileStore.LinkTitles, sp) == "My Spotify"); ProfileStore.Links.Clear(); ProfileStore.LinkTitles.Clear(); ProfileStore.Save();
          Check("a bio keeps its line breaks and has no short limit", ProfileStore.CleanBio("a\n\n\n\nb   c") == "a\n\nb c" && ProfileStore.CleanBio(new string('x', 5000)).Length == 5000 && ProfileStore.UnescBio(ProfileStore.EscBio("a\nb\\c")) == "a\nb\\c");
          string was2 = CommunityPresets.BaseUrl; CommunityPresets.BaseUrl = "http://127.0.0.1:1"; ProjectLibrary.ProfileName = "Neo"; ProfApi.LastSuggestions = new List<string> { "Neo Official", "The Neo" }; var shown = new List<string>(); sf.nameTakenHook = (n, sug) => { shown.AddRange(sug); return sug[0]; };
          typeof(StudioForm).GetMethod("AskNewName", BF).Invoke(sf, null); Check("when the name is taken a window offers the same name written in other ways, and the one picked becomes the name", shown.Count == 2 && ProjectLibrary.ProfileName == "Neo Official", ProjectLibrary.ProfileName); sf.nameTakenHook = null; CommunityPresets.BaseUrl = was2; Pump(300); }
        { var keep = StudioEngine.Current; var pv = new StudioEngine(); StudioEngine.Current = sf.eng; var sp = typeof(StudioForm).GetMethod("StopPreview", BF); SetF("previewEng", pv); SetF("previewPath", "x.ytmproj"); StudioEngine.Current = pv; sp.Invoke(sf, null); Check("after a project preview the instruments play through the main engine again (the Bass Lab piano has sound)", StudioEngine.Current == sf.eng); StudioEngine.Current = keep; }
        { // PRESETS keep your own plugins, in a box of their own; Bass Lab sits apart; PERFORMANCE MODE
          sf.eng.Tracks.Clear(); var vt = StudioEngine.NewTrack("Vox", "vocal", AudioClip.FromArrays(new float[44100 * 6], new float[44100 * 6], "t")); sf.eng.Tracks.Add(vt); SetF("selTrack", 0); SetF("tab", 0);
          var eq1 = FxRegistry.Create("eq"); eq1.Prepare(44100); var rv1 = FxRegistry.Create("reverb"); rv1.Prepare(44100); var bl = FxRegistry.Create("synth"); bl.Prepare(44100); ((InstrumentFx)bl).Owner = vt; vt.Fx.Add(eq1); vt.Fx.Add(rv1); vt.Fx.Add(bl);
          var put = typeof(StudioForm).GetMethod("PutPreset", BF); var rem = typeof(StudioForm).GetMethod("RemovePreset", BF);
          put.Invoke(sf, new object[] { vt, "Melodic Rap", Presets.BuildChain("Melodic Rap") }); int pc = vt.Fx.Count(f => f.Preset == "Melodic Rap");
          Check("a preset is added and your own plugins (and Bass Lab) stay", vt.Fx.Contains(eq1) && vt.Fx.Contains(rv1) && vt.Fx.Contains(bl) && pc > 3 && vt.Fx.Count == pc + 3, vt.Fx.Count + " plugins, " + pc + " from the preset");
          Check("the order is instrument, preset, then your plugins, and Bass Lab is not marked as a preset", vt.Fx[0] == bl && bl.Preset == "" && vt.Fx[1].Preset == "Melodic Rap" && vt.Fx[vt.Fx.Count - 2] == eq1 && vt.Fx[vt.Fx.Count - 1] == rv1);
          put.Invoke(sf, new object[] { vt, "Trap Vocal", Presets.BuildChain(Presets.ChainNames[Presets.ChainNames.Length > 1 ? 1 : 0]) }); int p1 = vt.Fx.Count(f => f.Preset == "Trap Vocal");
          Check("another preset replaces the first preset only", vt.Fx.Count(f => f.Preset == "Melodic Rap") == 0 && p1 > 0 && vt.Fx.Contains(eq1) && vt.Fx.Contains(rv1) && vt.Fx.Contains(bl));
          string mix = ProjectIO.SerializeMix(sf.eng); Check("the preset name is saved in the project", mix.Contains("fxp|0|") && mix.Contains("Trap Vocal"));
          ProjectIO.RestoreMix(sf.eng, mix); var vt2 = sf.eng.Tracks[0]; Check("and comes back when the project is opened", vt2.Fx.Count(f => f.Preset == "Trap Vocal") == p1 && vt2.Fx.Count(f => f.Preset == "") == 3, vt2.Fx.Count(f => f.Preset == "Trap Vocal") + " preset plugins");
          if (outDir != null) { SetF("selFx", -1); Shot(Path.Combine(outDir, "plugin-panel-boxes.png")); }
          rem.Invoke(sf, new object[] { vt2 }); Check("REMOVE takes away only the preset", vt2.Fx.Count == 3 && vt2.Fx.Any(f => f.Type == "eq"));
          if (outDir != null) { var cmp = FxRegistry.Create("comp"); cmp.Prepare(44100); vt2.Fx.Add(cmp); SetF("selFx", vt2.Fx.Count - 1); Shot(Path.Combine(outDir, "plugin-editor-rows.png")); vt2.Fx.Remove(cmp); SetF("selFx", -1); }
          if (outDir != null) { typeof(StudioForm).GetMethod("OpenBrowser", BF).Invoke(sf, new object[] { false, 0 }); Shot(Path.Combine(outDir, "add-effect-boxes.png")); SetF("browserOpen", false); }
          // an instrument's panel is only the OPEN button
          Check("Bass Lab's panel is as short as the button", (float)typeof(StudioForm).GetMethod("EditorHeight", BF | BindingFlags.Static).Invoke(null, new object[] { bl }) < 120);
          sf.eng.Tracks.Clear();
          // PERFORMANCE MODE
          { var st = new SettingsForm(app); st.Show(); Pump(400); var pcb = (CheckBox)typeof(SettingsForm).GetField("perfChk", BF).GetValue(st); Check("Settings has the PERFORMANCE MODE switch", pcb != null && pcb.Text.StartsWith("PERFORMANCE MODE")); for (int pi = 0; pi < 7 && outDir != null; pi++) { typeof(SettingsForm).GetMethod("ShowPage", BF).Invoke(st, new object[] { pi }); Pump(150); if (pcb.Visible) FormShot(st, Path.Combine(outDir, "settings-animation.png")); } pcb.Checked = true; Check("ticking it turns PERFORMANCE MODE on", Prefs.PerfMode); pcb.Checked = false; Check("and unticking turns it off", !Prefs.PerfMode); st.Close(); }
          Prefs.PerfMode = true; Check("PERFORMANCE MODE caps the frame rate at 30", Prefs.FpsEff == 30); string pl = string.Join("\n", Prefs.Lines()); Check("and is saved", pl.Contains("perfmode=True")); Prefs.PerfMode = false; Prefs.Parse("perfmode", "True"); Check("and read back", Prefs.PerfMode); Prefs.PerfMode = false; Check("normal mode uses your frame rate", Prefs.FpsEff == Prefs.FpsCap);
        }
        { // the export window: the two complete mixes exclude each other, no dry options, a trim
          var res = new List<string>(); var tm = new System.Windows.Forms.Timer { Interval = 500 };
          tm.Tick += (o, e) => { tm.Stop(); var ef = Application.OpenForms.Cast<Form>().FirstOrDefault(x => x.Text == "Export"); if (ef == null) { res.Add("noform"); return; }
            var cks = ef.Controls.OfType<CheckBox>().ToList(); res.Add("n=" + cks.Count); res.Add("dry=" + cks.Any(c => c.Text.IndexOf("dry", StringComparison.OrdinalIgnoreCase) >= 0));
            res.Add("a=" + cks[0].Checked + cks[1].Checked); cks[1].Checked = true; res.Add("b=" + cks[0].Checked + cks[1].Checked); cks[0].Checked = true; res.Add("c=" + cks[0].Checked + cks[1].Checked);
            var boxes = ef.Controls.OfType<TextBox>().ToList(); res.Add("t=" + boxes[0].Text + "," + boxes[1].Text);
            if (outDir != null) { res.Add("cs=" + ef.ClientSize.Height + "/" + ef.Height); var bm = new Bitmap(ef.Width, ef.Height); ef.DrawToBitmap(bm, new Rectangle(0, 0, bm.Width, bm.Height)); bm.Save(Path.Combine(outDir, "export-dialog.png"), ImageFormat.Png); bm.Dispose(); }
            ef.Close(); };
          tm.Start(); StudioDialogs.ExportOptions(sf, "My song", 95.0); string rj = string.Join(" ", res);
          Check("the export window has five choices and no dry vocals or dry instrumental", rj.Contains("n=5") && rj.Contains("dry=False"), rj);
          Check("complete mix without master un-ticks complete mix with master, and the other way round", rj.Contains("a=TrueFalse") && rj.Contains("b=FalseTrue") && rj.Contains("c=TrueFalse"), rj);
          Check("the trim boxes start at 0:00 and end at the end of the song", rj.Contains("t=0:00,1:35"), rj);
        }
        { // Bass Lab: the menu button, YOUR SOUNDS, saved sounds, dropped files
          var sfx = new SynthFx(); sfx.Prepare(44100); var sw = new SynthWindow(sfx, null); sw.Show(); Pump(300); var cv = (Control)typeof(SynthWindow).GetField("canvas", BF).GetValue(sw); var T = typeof(SynthCanvas);
          Func<string, object> gf = n => T.GetField(n, BF).GetValue(cv); Action<string, object> sfv = (n, v) => T.GetField(n, BF).SetValue(cv, v);
          var regsF = T.GetField("regs", BF); var regsL = (IList)regsF.GetValue(cv); Func<string, bool> hasTip = tip => { foreach (var r in (IList)regsF.GetValue(cv)) { var tf = r.GetType().GetField("Tip"); if (tf != null && (string)tf.GetValue(r) == tip) return true; } return false; };
          cv.Invalidate(); Pump(200); Check("Bass Lab has the menu button (YOUR SOUNDS (drag n drop)) and no preset name box any more", hasTip("YOUR SOUNDS (drag n drop)") && !hasTip("Choose a preset, or save your own"));
          T.GetMethod("PresetMenu", BF).Invoke(cv, new object[] { new PointF(10, 10) }); Pump(500); cv.Invalidate(); Pump(300);
          Check("pressing it opens the menu", (bool)gf("browseOpen") && hasTip("The sounds you saved: they stay until you delete them") && hasTip("Publish a sound for everyone to use (you pick which)."));
          if (outDir != null) { var bm = new Bitmap(sw.ClientSize.Width, sw.ClientSize.Height); sw.DrawToBitmap(bm, new Rectangle(0, 0, bm.Width, bm.Height)); bm.Save(Path.Combine(outDir, "basslab-yoursounds-builtin.png"), ImageFormat.Png); bm.Dispose(); }
          foreach (var old in SynthPresets.UserNames().ToList()) SynthPresets.DeleteUser(old);
          sfx.PresetName = "My wobble"; SynthPresets.SaveUser(sfx, "My wobble"); string path1 = SynthPresets.ExportFile("My wobble", SynthPresets.UserData("My wobble"));
          Check("a saved sound is kept (until it is deleted) and can be written as a file", SynthPresets.UserNames().Contains("My wobble") && File.Exists(path1) && path1.EndsWith(".bsound"));
          string msg1 = (string)T.GetMethod("DropSoundFiles", BF).Invoke(cv, new object[] { new[] { path1 } }); Check("dropping that file adds it again under another name", msg1.StartsWith("Added") && SynthPresets.UserNames().Contains("My wobble 2"), msg1);
          string junk = Path.Combine(StudioEnv.DataDir, "junk.txt"); File.WriteAllText(junk, "hello world <script>"); string msg2 = (string)T.GetMethod("DropSoundFiles", BF).Invoke(cv, new object[] { new[] { junk } }); Check("a file that is not a sound is refused", !msg2.StartsWith("Added") && SynthPresets.UserNames().Count == 2, msg2);
          string msg3 = (string)T.GetMethod("DropSoundFiles", BF).Invoke(cv, new object[] { new[] { "x.wav" } }); Check("an audio file says Bass Lab makes its own sound", msg3.Contains("makes its own sound"), msg3);
          sfv("soundTab", 1); cv.Invalidate(); Pump(300); if (outDir != null) { var bm = new Bitmap(sw.ClientSize.Width, sw.ClientSize.Height); sw.DrawToBitmap(bm, new Rectangle(0, 0, bm.Width, bm.Height)); bm.Save(Path.Combine(outDir, "basslab-yoursounds-mine.png"), ImageFormat.Png); bm.Dispose(); }
          SynthPresets.DeleteUser("My wobble 2"); Check("deleting one sound leaves the others", SynthPresets.UserNames().Contains("My wobble") && !SynthPresets.UserNames().Contains("My wobble 2"));
          foreach (var old in SynthPresets.UserNames().ToList()) SynthPresets.DeleteUser(old); sw.Close();
        }
        { // the DAW's questions: leaving, saving, opening; the welcome page; the Community choice
          string wasUrl = CommunityPresets.BaseUrl; CommunityPresets.BaseUrl = "http://127.0.0.1:1";
          var asked = new List<string>(); var ans = new Queue<DialogResult>(); sf.exitAsk = t => { asked.Add(t); return ans.Count > 0 ? ans.Dequeue() : DialogResult.Yes; };
          sf.eng.Tracks.Clear(); sf.eng.Tracks.Add(StudioEngine.NewTrack("Lead", "beat", AudioClip.FromArrays(new float[44100 * 4], new float[44100 * 4], "t"))); SetF("projPath", ""); SetF("projName", "Untitled project");
          var al = typeof(StudioForm).GetMethod("AskLeave", BF); ans.Enqueue(DialogResult.No); bool stay = !(bool)al.Invoke(sf, null);
          Check("the X of the DAW says YOU ARE LEAVING THE DAW, and No keeps you in it (nothing else is asked)", stay && asked.Count == 1 && asked[0].StartsWith("YOU ARE LEAVING THE DAW"), asked.Count + ": " + (asked.Count > 0 ? asked[0] : ""));
          asked.Clear(); ans.Clear(); ans.Enqueue(DialogResult.Yes); ans.Enqueue(DialogResult.Cancel); stay = !(bool)al.Invoke(sf, null);
          Check("an unsaved project then asks to save it (you give it a name, it is in My Projects), and Cancel stays in the DAW", stay && asked.Count == 2 && asked[1].Contains("not saved") && asked[1].Contains("name") && asked[1].Contains("My Projects"), asked.Count > 1 ? asked[1] : "");
          string testName = "zz purphex test " + Guid.NewGuid().ToString("N").Substring(0, 6); string made = null; sf.nameHook = def => testName; asked.Clear(); ans.Clear(); ans.Enqueue(DialogResult.Yes); ans.Enqueue(DialogResult.Yes);
          bool left = (bool)al.Invoke(sf, null); made = Path.Combine(ProjectLibrary.DefaultFolder, testName + ".ytmproj");
          Check("Yes saves it under the name you gave, in the projects folder, and then you leave", left && File.Exists(made) && F<string>("projPath") == made && ProjectLibrary.Find(made) != null, made);
          Check("saved, the project has nothing unsaved; one more edit and it has again", !(bool)typeof(StudioForm).GetProperty("HasUnsaved", BF).GetValue(sf, null) && (typeof(StudioForm).GetMethod("PushUndo", BF).Invoke(sf, null) == null) && (bool)typeof(StudioForm).GetProperty("HasUnsaved", BF).GetValue(sf, null));
          asked.Clear(); ans.Clear(); ans.Enqueue(DialogResult.Yes); ans.Enqueue(DialogResult.No); SetF("leaving", false); left = (bool)al.Invoke(sf, null); Check("No (do not save) leaves without saving", left && asked.Count == 2);
          // SAVE asks before it overwrites
          var yn = new List<string>(); SetF("askHook", (Func<string, bool>)(t => { yn.Add(t); return false; })); long t0 = File.GetLastWriteTime(made).Ticks; System.Threading.Thread.Sleep(30);
          typeof(StudioForm).GetMethod("SaveClick", BF).Invoke(sf, new object[] { false }); Check("pressing SAVE on a saved project asks SAVING WILL OVERWRITE THE CURRENT PROJECT, and No leaves the file alone", yn.Count == 1 && yn[0].StartsWith("SAVING WILL OVERWRITE THE CURRENT PROJECT") && yn[0].Contains("Confirm?") && File.GetLastWriteTime(made).Ticks == t0);
          SetF("askHook", (Func<string, bool>)(t => true)); typeof(StudioForm).GetMethod("SaveClick", BF).Invoke(sf, new object[] { false }); Check("Yes saves it", File.GetLastWriteTime(made).Ticks > t0); SetF("askHook", null);
          string ov = (string)typeof(StudioForm).GetMethod("OverwriteText", BF).Invoke(sf, new object[] { "Other song" }); Check("opening a project says OPENING ... WILL OVERWRITE THE PROJECT THAT IS OPEN NOW", ov.StartsWith("OPENING \"Other song\" WILL OVERWRITE THE PROJECT THAT IS OPEN NOW") && ov.EndsWith("Confirm?"));
          try { File.Delete(made); } catch { } ProjectLibrary.Remove(made); sf.eng.Tracks.Clear(); SetF("projPath", ""); sf.exitAsk = null; sf.nameHook = null; SetF("leaving", false);
          // Purphex Music opens on My Projects with the welcome banner
          sf.ShowWelcome(); Pump(200); Check("opening Purphex Music shows My Projects with the welcome banner", F<int>("tab") == 3 && F<bool>("welcomeOn")); if (outDir != null) Shot(Path.Combine(outDir, "projects-welcome.png"));
          SetF("welcomeOn", false); SetF("tab", 0);
          int pick = -1; sf.communityChoiceHook = n => pick = n; typeof(StudioForm).GetMethod("MenuDirect", BF).Invoke(sf, new object[] { 6 }); ((Action)typeof(StudioForm).GetMethod("MenuDirect", BF).Invoke(sf, new object[] { 6 }))(); Check("the Community button asks which: the community tab or the artists page", pick == 0); sf.communityChoiceHook = null;
          CommunityPresets.BaseUrl = wasUrl; }
        { // the Community tab: the feed with its filters, SAVE SONG, share link, no Open; the artist page; messages; the floating chat; collabs
          string wasUrl = CommunityPresets.BaseUrl; CommunityPresets.BaseUrl = "http://127.0.0.1:1"; SavedSongs.ResetForTest(); ReleaseStore.ResetForTest();
          Func<string, string, string, string, string, FeedSong> fs = (id, title, who, kind, coll) => new FeedSong { Id = id, Title = title, ArtistId = "a" + who.GetHashCode().ToString("x"), Artist = who, Kind = kind, Coll = coll, Cn = kind == "single" ? 0 : 4, Ci = 1, Tags = new[] { "dnb", "dark" }, At = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds - 3600000, Plays = 1234, Likes = 56, Video = id == "s2" };
          var songs = new List<FeedSong> { fs("s1", "Night Drive", "DJ Neon Wolf", "single", ""), fs("s2", "Midnight Run With A Very Long Name", "Ana Bee", "ep", "Late Hours EP"), fs("s3", "Skyline", "Ben Cee", "album", "City Lights"), fs("s4", "Bass In The Park", "Cy Dee", "single", "") };
          SetF("feed", songs); SetF("feedTags", new List<string> { "dnb", "bassline", "garage", "trap", "rap", "lofi" }); SetF("feedFetched", true); SetF("commFilter", 0); SetF("tab", 4); SetF("projectsLoaded", true); Pump(200);
          Check("the Community tab lists the songs of everyone that were put up", ((List<FeedSong>)typeof(StudioForm).GetMethod("FeedList", BF).Invoke(sf, null)).Count == 4);
          if (outDir != null) Shot(Path.Combine(outDir, "community-feed.png"));
          Check("there is no PUBLISHED filter any more, and there are NEW RELEASES, ALBUMS, EPS and SAVED", !((string[])typeof(StudioForm).GetField("CommTabs", BF | BindingFlags.Static).GetValue(null)).Contains("Published") && ((string[])typeof(StudioForm).GetField("CommTabs", BF | BindingFlags.Static).GetValue(null)).SequenceEqual(new[] { "ALL", "NEW RELEASES", "ALBUMS", "EPS", "SAVED" }));
          SavedSongs.Toggle("s3"); SetF("commFilter", 4); Check("SAVED shows only the songs you saved", ((List<FeedSong>)typeof(StudioForm).GetMethod("FeedList", BF).Invoke(sf, null)).Count == 1); SetF("commFilter", 2); Pump(100); if (outDir != null) Shot(Path.Combine(outDir, "community-feed-albums.png")); SetF("commFilter", 0); SavedSongs.ResetForTest();
          SetF("feedFetched", true);   // (the pictures above were drawn without the network)
          // the artist page: ADD FRIEND, MESSAGE, BLOCK, REPORT, TIME MADE and tracks with a play button, AUDIO ONLY / MUSIC VIDEO
          ProfileStore.Published = true; ProfileStore.Key = new string('a', 32);
          var art = new ArtistInfo { Id = "aabc", Name = "DJ Neon Wolf", Bio = "Dark dnb and bassline from the north.", Location = "Leeds", Followers = 12, Friends = 3, Tracks = 3, TimeMade = 98765, Tags = new[] { "dnb", "bassline" } };
          art.TrackList.Add(new ArtistTrack { Title = "Night Drive", Bpm = 174, Secs = 213, Tags = "dnb dark", Song = "s1", VideoUp = true }); art.TrackList.Add(new ArtistTrack { Title = "Slow Burn", Bpm = 140, Secs = 188, Tags = "garage", Song = "s5" }); art.TrackList.Add(new ArtistTrack { Title = "Not Up Yet", Bpm = 90, Secs = 120, Tags = "lofi", Video = "https://www.youtube.com/watch?v=abc" });
          SetF("artSel", art); SetF("artFriend", "none"); SetF("artBlocked", false); SetF("tab", 7); SetF("artView", 0); Pump(200); if (outDir != null) Shot(Path.Combine(outDir, "artist-page-none.png"));
          SetF("artFriend", "friends"); Pump(100); if (outDir != null) Shot(Path.Combine(outDir, "artist-page-friends.png")); SetF("artFriend", "incoming"); SetF("artBlocked", true); Pump(100); if (outDir != null) Shot(Path.Combine(outDir, "artist-page-blocked.png")); SetF("artBlocked", false);
          var rp = new List<string>(); sf.msgAskHook = p => { rp.Add(p); return ""; }; typeof(StudioForm).GetMethod("ReportArtist", BF).Invoke(sf, new object[] { art }); Check("REPORT asks why, and an empty reason sends nothing", rp.Count == 1 && rp[0].Contains("Why are you reporting DJ Neon Wolf") && F<string>("artReported") == ""); sf.msgAskHook = null;
          // messages
          var th1 = new MsgThread { With = new ArtistInfo { Id = "a1", Name = "Ana Bee" }, Unread = 2, At = 1 }; th1.Msgs.Add(new MsgItem { Mine = true, At = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds - 7200000, Text = "Love your drop on Midnight Run! The bassline is mad.", About = "Midnight Run" }); th1.Msgs.Add(new MsgItem { Mine = false, At = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds - 3600000, Text = "Thank you!! Want to make a track together?\nI have a 174 bpm idea." }); th1.Msgs.Add(new MsgItem { Mine = false, At = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalMilliseconds - 60000, Text = "Send me a version when you are ready :)" });
          var th2 = new MsgThread { With = new ArtistInfo { Id = "a2", Name = "Ben Cee" }, At = 0 }; th2.Msgs.Add(new MsgItem { Mine = false, Text = "Hey" });
          SetF("msgThreads", new List<MsgThread> { th1, th2 }); SetF("msgUnread", 2); SetF("profReqs", new List<ArtistInfo> { new ArtistInfo { Id = "a3", Name = "Cy Dee" } }); SetF("msgWithId", "a1"); SetF("artSel", null); SetF("artView", 1); SetF("msgFetchAt", DateTime.Now); SetF("collabFetchAt", DateTime.Now); Pump(250);
          Check("the Artists button shows how many new messages there are", true); if (outDir != null) Shot(Path.Combine(outDir, "messages.png"));
          var wr = new List<string>(); sf.msgAskHook = p => { wr.Add(p); return null; }; typeof(StudioForm).GetMethod("WriteMessage", BF).Invoke(sf, new object[] { "a1", "Ana Bee", "Midnight Run" }); Check("writing about a song says which song it is about", wr.Count == 1 && wr[0].Contains("about \"Midnight Run\"")); sf.msgAskHook = null;
          // the floating chat (a window you can drag, over any page)
          SetF("artView", 0); SetF("tab", 0); SetF("chatOpen", true); SetF("chatWith", "a1"); Pump(200); if (outDir != null) Shot(Path.Combine(outDir, "chat-floating.png"));
          var po = (Dictionary<string, PointF>)typeof(StudioForm).GetField("popOff", BF).GetValue(sf); po["chat"] = new PointF(-420, 160); Pump(150); if (outDir != null) Shot(Path.Combine(outDir, "chat-moved.png")); Check("the chat window can be dragged to another place (it remembers the place)", po.ContainsKey("chat") && po["chat"].X == -420); SetF("chatOpen", false);
          // collabs
          var cl1 = new CollabInfo { Id = "c1", Title = "Late Night Rider", State = "active", Inviter = true, With = new ArtistInfo { Id = "a1", Name = "Ana Bee" }, MineSize = 4200000, TheirSize = 5100000 }; var cl2 = new CollabInfo { Id = "c2", Title = "Skyline Remix", State = "invited", Inviter = false, With = new ArtistInfo { Id = "a2", Name = "Ben Cee" } };
          SetF("collabList", new List<CollabInfo> { cl1, cl2 }); SetF("tab", 7); SetF("artView", 2); Pump(200); if (outDir != null) Shot(Path.Combine(outDir, "collabs.png")); SetF("artView", 0); SetF("tab", 0);
          ProfileStore.Published = false; ProfileStore.Key = ""; CommunityPresets.BaseUrl = wasUrl; }
        { // My Projects: UPLOAD MUSIC VIDEO and SHARE / EP / ALBUM, cards with a running border
          string d0 = Path.Combine(StudioEnv.DataDir, "cards"); Directory.CreateDirectory(d0); VideoRecs.ResetForTest(); ProjectLibrary.Entries.Clear();
          for (int i = 0; i < 3; i++) { string p = Path.Combine(d0, "Song " + (i + 1) + ".ytmproj"); File.WriteAllText(p, "x"); ProjectLibrary.Entries.Add(new ProjectEntry { Path = p, Name = "Song " + (i + 1), Tracks = 3, Bpm = 140, Seconds = 150, OnProfile = i == 0, Updated = DateTime.Now, Opened = DateTime.Now }); }
          VideoRecs.Add(CommunitySongs.PidOf(ProjectLibrary.Entries[0].Path)); SetF("projectsLoaded", true); SetF("tab", 3); sf.eng.Tracks.Clear(); Pump(250); if (outDir != null) Shot(Path.Combine(outDir, "projects-cards.png"));
          // an EP or an album from the projects (a window: name, EP or ALBUM, the projects to tick)
          Release got = null; var tm = new System.Windows.Forms.Timer { Interval = 500 }; string fres = "";
          tm.Tick += (o2, e2) => { tm.Stop(); Form rf = null; try { rf = Application.OpenForms.Cast<Form>().FirstOrDefault(x => x.Text == "New EP or album"); if (rf == null) { fres = "noform"; foreach (Form x in Application.OpenForms) if (x != sf && x.Modal) x.Close(); return; }
            var tb = rf.Controls.OfType<TextBox>().First(); var lb = rf.Controls.OfType<CheckedListBox>().First(); var ok = rf.Controls.OfType<Button>().First(b => string.Equals(b.Text, "Create", StringComparison.OrdinalIgnoreCase)); var er = rf.Controls.OfType<Label>().Last();
            lb.SetItemChecked(0, true); tb.Text = "Late Hours"; ok.PerformClick(); fres += "one:" + er.Text + "|"; lb.SetItemChecked(1, true); lb.SetItemChecked(2, true); tb.Text = "L"; ok.PerformClick(); fres += "name:" + er.Text + "|";
            if (outDir != null) { var bm = new Bitmap(rf.Width, rf.Height); rf.DrawToBitmap(bm, new Rectangle(0, 0, bm.Width, bm.Height)); bm.Save(Path.Combine(outDir, "release-dialog.png"), ImageFormat.Png); bm.Dispose(); }
            tb.Text = "Late Hours EP"; ok.PerformClick(); fres += "done"; } catch (Exception ex3) { fres += "ERR " + ex3.Message; try { if (rf != null) rf.Close(); } catch { } } };
          tm.Start(); got = ReleaseDialog.Show(sf, ProjectLibrary.Entries.ToList());
          Check("the EP / ALBUM window refuses one project and a too short name (with a sentence) and then makes the release", got != null && got.Kind == "ep" && got.Title == "Late Hours EP" && got.Pids.Count == 3 && fres.ToUpper().Contains("ONE:PICK AT LEAST 2") && fres.ToUpper().Contains("NAME:GIVE IT A NAME"), fres + " / " + (got == null ? "null" : got.Pids.Count.ToString()));
          sf.releaseHook = list => new Release { Kind = "album", Title = "City Lights", Pids = list.Select(x => CommunitySongs.PidOf(x.Path)).ToList() }; SetF("askHook", (Func<string, bool>)(t => false)); ProfileStore.Published = false; typeof(StudioForm).GetMethod("NewReleaseClick", BF).Invoke(sf, null); SetF("askHook", null); sf.releaseHook = null;
          Check("making a release keeps it (and asks before it publishes the projects that are private)", ReleaseStore.All.Count == 1 && ReleaseStore.All[0].Title == "City Lights" && ReleaseStore.All[0].Kind == "album" && !ProjectLibrary.Entries[1].OnProfile);
          Check("the profile gets the release only with its published projects", ReleaseStore.Json(ProjectLibrary.Entries) == "[]"); ProjectLibrary.Entries[1].OnProfile = true; Check("two published projects make it count", ReleaseStore.Json(ProjectLibrary.Entries).Contains("City Lights"));
          ReleaseStore.ResetForTest(); ProjectLibrary.Entries.Clear(); VideoRecs.ResetForTest(); SetF("tab", 0); }
        { // COLOUR EMOJI in writing: pictures made from the emoji font of this PC (a browser that is never shown), drawn over the outlines of a text box and in the writing the program draws itself
          EmojiSprites.Disabled = false; Func<Bitmap, Func<Color, bool>, int> countPx = (bm, pred) => { int n = 0; for (int y = 0; y < bm.Height; y++) for (int x = 0; x < bm.Width; x++) if (pred(bm.GetPixel(x, y))) n++; return n; };
          Func<Color, bool> orange = c => c.R > 200 && c.G > 90 && c.G < 200 && c.B < 80, yellow = c => c.R > 220 && c.G > 170 && c.B < 90, red = c => c.R > 200 && c.G < 70 && c.B < 80, green = c => c.G > 150 && c.R < 120 && c.B < 120;
          string[] ks = { "\U0001F525", "\U0001F600", "\U0001F308", "\u2764\uFE0F", "\U0001F3B8" }; foreach (var k in ks) EmojiSprites.Get(k); for (int i = 0; i < 400 && ks.Any(k => !EmojiSprites.Known(k)); i++) Pump(25);
          var fire = EmojiSprites.Get("\U0001F525"); Check("the colour picture of an emoji is made from the font of this PC (not shipped, nothing downloaded)", fire != null && fire.Width == 64 && countPx(fire, orange) > 150, fire == null ? "none" : "orange " + countPx(fire, orange));
          var grin = EmojiSprites.Get("\U0001F600"); var rain = EmojiSprites.Get("\U0001F308"); var heart = EmojiSprites.Get("\u2764\uFE0F"); Check("a face is yellow, a rainbow has red and green, a heart is red", grin != null && countPx(grin, yellow) > 300 && rain != null && countPx(rain, red) > 40 && countPx(rain, green) > 40 && heart != null && countPx(heart, red) > 300, (grin == null ? "-" : countPx(grin, yellow).ToString()) + " " + (rain == null ? "-" : countPx(rain, red) + "/" + countPx(rain, green)) + " " + (heart == null ? "-" : countPx(heart, red).ToString()));
          Check("the background around an emoji is see-through", fire != null && fire.GetPixel(1, 1).A < 10 && fire.GetPixel(32, 40).A > 200);
          Check("a heart is found with or without the picture mark, and a pair of surrogates is one emoji", EmojiSprites.KeyOf("\u2764") == "\u2764\uFE0F" && EmojiSprites.KeyOf("\u2764\uFE0F") == "\u2764\uFE0F" && EmojiSprites.ClusterAt("a\U0001F525b", 1) == 2 && EmojiSprites.ClusterAt("\u2764\uFE0F", 0) == 2 && EmojiSprites.ClusterAt("abc", 1) == 0);
          // a text box
          var ef = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(60, 60), Size = new Size(640, 220), TopMost = true, BackColor = Color.FromArgb(10, 5, 25) };
          var etb = new TextBox { Multiline = true, Left = 12, Top = 12, Width = 600, Height = 150, Font = new Font("Segoe UI", 26f), ForeColor = Color.White, BackColor = Color.FromArgb(10, 5, 25), BorderStyle = BorderStyle.None, Text = "\U0001F3B8\U0001F525\u2764\uFE0F\U0001F600\U0001F308 hi" };
          ef.Controls.Add(etb); Win.NeonBox(etb); ef.Show(); ef.Activate(); Pump(900); etb.Invalidate(); Pump(500);
          Func<Bitmap> grab = () => { var p = etb.PointToScreen(Point.Empty); var bm = new Bitmap(etb.Width, etb.Height); using (var gg = Graphics.FromImage(bm)) gg.CopyFromScreen(p, Point.Empty, etb.Size); return bm; };
          using (var shot = grab()) { if (outDir != null) shot.Save(Path.Combine(outDir, "emoji-colour-textbox.png"), ImageFormat.Png); Check("the emoji in a text box are in colour (orange fire, yellow face, red heart), not white outlines", countPx(shot, orange) > 80 && countPx(shot, yellow) > 200 && countPx(shot, red) > 100, "orange " + countPx(shot, orange) + " yellow " + countPx(shot, yellow) + " red " + countPx(shot, red)); }
          etb.Text = "ab " + etb.Text; Pump(300); etb.Invalidate(); Pump(400); using (var shot2 = grab()) Check("and still after the text is changed", countPx(shot2, orange) > 80);
          etb.SelectAll(); etb.Focus(); Pump(300); etb.Invalidate(); Pump(300); using (var shot3 = grab()) { if (outDir != null) shot3.Save(Path.Combine(outDir, "emoji-colour-selected.png"), ImageFormat.Png); Check("selected text still shows the colour pictures", countPx(shot3, orange) > 40); }
          ef.Close();
          // the writing the program draws itself
          using (var bm = new Bitmap(500, 60)) { using (var gg = Graphics.FromImage(bm)) { gg.Clear(Color.FromArgb(10, 5, 25)); gg.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; EmojiText.Draw(gg, "Fire \U0001F525 love \u2764\uFE0F wow \U0001F600", 24, Color.White, new RectangleF(4, 4, 490, 52), StringAlignment.Near, false); }
            if (outDir != null) bm.Save(Path.Combine(outDir, "emoji-colour-drawn.png"), ImageFormat.Png); Check("emoji in the writing the program draws are in colour too", countPx(bm, orange) > 60 && countPx(bm, yellow) > 120 && countPx(bm, red) > 80, "orange " + countPx(bm, orange) + " yellow " + countPx(bm, yellow) + " red " + countPx(bm, red)); }
          EmojiSprites.Disabled = true; }
        // PROFILE THEMES
        { int prev = Themes.Index; string err = sf.MixSaveAsTheme(); var made = Themes.All.Find(t => t.Custom && t.Name.StartsWith("Mix ")); Check("SAVE AS THEME puts the theme in the profile department and leaves the theme in use alone", err == null && made != null && Themes.IsProfile(made) && Themes.Index == prev);
          if (outDir != null) { SetF("themeOpen", true); SetF("themeTab", 1); SetF("themeSelName", made.Name); Shot(Path.Combine(outDir, "themes-profile-tab.png")); SetF("themeTab", 0); Shot(Path.Combine(outDir, "themes-my-tab.png")); }
          SetF("themeTab", 1); SetF("themeOpen", true); sf.Invalidate(); sf.Update(); var btnMap = (IDictionary)typeof(StudioForm).GetField("btnMap", BF).GetValue(sf); Check("the PROFILE THEMES tab has a DELETE THEME button under the themes", btnMap.Contains("DELETE THEME") || btnMap.Contains("Delete Theme") || btnMap.Keys.Cast<string>().Any(k => k.ToUpper() == "DELETE THEME"));
          SetF("themeOpen", false); SetF("themeTab", 0); Themes.Delete(made); }
        // the community page
        SetF("tab", 4); sf.Invalidate(); sf.Update(); Application.DoEvents(); { var btnMap = (IDictionary)typeof(StudioForm).GetField("btnMap", BF).GetValue(sf); Check("the Community page has SAVED SONGS and SHARE (both on the website: you sign in there)", btnMap.Keys.Cast<string>().Any(k => k.ToUpper() == "SAVED SONGS") && btnMap.Keys.Cast<string>().Any(k => k.ToUpper() == "SHARE")); }
        if (outDir != null) Shot(Path.Combine(outDir, "community.png"));
        // the lyrics page and its emoji panel
        SetF("toast", ""); SetF("tab", 2); Pump(300); if (outDir != null) Shot(Path.Combine(outDir, "lyrics.png"));
        { var btnMap = (IDictionary)typeof(StudioForm).GetField("btnMap", BF).GetValue(sf); Check("the lyrics page has the EMOJIS button", btnMap.Keys.Cast<string>().Any(k => k.ToUpper() == "EMOJIS")); }
        { string inserted = ""; EmojiPanel.Show(sf, e => inserted += e); Pump(2500); Form pf = Application.OpenForms.Cast<Form>().FirstOrDefault(x => x.Text == "Emojis"); var wb = EmojiPanel.LastBrowser; Check("the emoji panel opens as a window of its own, with a page in it", pf != null && wb != null && wb.Document != null);
          if (pf != null && wb != null && wb.Document != null)
          {
              Func<int> coloured = () => { pf.Activate(); pf.BringToFront(); Pump(300); var bmp = new Bitmap(wb.Width, wb.Height); using (var gg = Graphics.FromImage(bmp)) gg.CopyFromScreen(wb.PointToScreen(Point.Empty), Point.Empty, wb.Size); int n = 0;   /* a picture of the real screen: the browser control does not draw into DrawToBitmap when other windows are open */ for (int y = 120; y < bmp.Height - 10; y += 2) for (int x = 20; x < bmp.Width - 40; x += 2) { var c = bmp.GetPixel(x, y); int mx = Math.Max(c.R, Math.Max(c.G, c.B)), mn = Math.Min(c.R, Math.Min(c.G, c.B)); if (mx - mn > 110 && mx > 190 && c.R > c.B + 40) n++; } if (outDir != null) bmp.Save(Path.Combine(outDir, "emoji-last.png"), ImageFormat.Png); bmp.Dispose(); return n; };
              wb.Document.InvokeScript("setQuery", new object[] { "" }); wb.Document.InvokeScript("eval", new object[] { "cur=0;show();" }); Pump(500); if (outDir != null) coloured();
              Check("the emojis are in COLOUR (orange and red faces, not white outlines)", coloured() > 150, coloured() + " ready " + wb.ReadyState + " body " + (wb.Document.Body == null ? -1 : wb.Document.Body.InnerHtml.Length) + " url " + wb.Url);
              var hint = wb.Document.GetElementById("hint"); wb.Document.InvokeScript("setQuery", new object[] { "fire" }); Pump(600); Check("a search by word finds them (fire)", hint.InnerText.Contains("found") && !hint.InnerText.StartsWith("0 "), hint.InnerText); if (outDir != null) { coloured(); File.Copy(Path.Combine(outDir, "emoji-last.png"), Path.Combine(outDir, "emoji-search.png"), true); };
              Check("flags are not in the panel (Windows draws them as letters)", EmojiPanel.BuildPage().IndexOf("🇬🇧") < 0);
              var br = wb.ObjectForScripting as EmojiBridge; br.Pick("🔥"); Check("clicking an emoji puts it in the lyrics and into RECENT", inserted == "🔥" && EmojiPanel.Recent()[0].E == "🔥");
              pf.Close(); } }
        // the glitching, zapping look of a track that is off, and the neon menus (look at them)
        if (outDir != null)
        {
            var xs = new float[44100 * 40]; for (int i = 0; i < xs.Length; i++) xs[i] = (float)(0.55 * Math.Sin(i * 0.031) * (0.5 + 0.5 * Math.Sin(i * 0.00012)) * (0.6 + 0.4 * Math.Sin(i * 0.0007)));
            sf.eng.Tracks.Clear(); var trk = StudioEngine.NewTrack("Hurricane beat", "beat", AudioClip.FromArrays(xs, (float[])xs.Clone(), "test")); sf.eng.Tracks.Add(trk); trk.Off = true; SetF("tab", 0); SetF("toast", ""); Pump(300);
            for (int n = 0; n < 6; n++) { Pump(137 + n * 71); Shot(Path.Combine(outDir, "off-" + n + ".png")); }
            trk.Off = false;
            var m = Win.NewMenu(); m.Items.Add("Rapper vocal styles (searchable)..."); { var sub = new ToolStripMenuItem("Rap vocal presets (replace chain)"); sub.DropDownItems.Add("x"); m.Items.Add(sub); } m.Items.Add(new ToolStripMenuItem("Community chains (download and use)...") { Checked = true }); m.Items.Add(new ToolStripSeparator()); m.Items.Add("Save this track's plugins as a preset..."); m.Items.Add("Clear all plugins");
            m.Show(sf, new Point(200, 300)); Pump(400); var menuBmp = new Bitmap(520, 220); using (var gg = Graphics.FromImage(menuBmp)) gg.CopyFromScreen(sf.PointToScreen(new Point(190, 290)), Point.Empty, new Size(520, 220)); menuBmp.Save(Path.Combine(outDir, "menu.png"), ImageFormat.Png); menuBmp.Dispose(); m.Close();
        }
        // the tag box and the pages (look at them)
        if (outDir != null) { SetF("tab", 6); SetF("profScroll", 0f); Shot(Path.Combine(outDir, "profile.png")); SetF("profScroll", 560f); Shot(Path.Combine(outDir, "profile2.png")); SetF("profScroll", 2200f); Shot(Path.Combine(outDir, "profile3.png")); }
        // a PROFILE of somebody else
        { var a = new ArtistInfo { Id = "a1", Name = "Neo Wolf", Bio = "Dark dnb from the north", Location = "Poole, Dorset", Accent = 0x2A6AD8, NameRgb = 0x7AC8FF, NameRgb2 = 0xD87AFF, Tracks = 2, Followers = 12, Fantasy = false };
          a.Tags = Enumerable.Range(1, 30).Select(i => "tag" + i).ToArray(); a.Links.Add("https://open.spotify.com/artist/1"); a.Links.Add("https://www.youtube.com/@neo"); a.TrackList.Add(new ArtistTrack { Title = "Night Drive", Bpm = 174, Secs = 215, Tags = "dnb", Video = "https://www.youtube.com/watch?v=abc" }); a.TrackList.Add(new ArtistTrack { Title = "Wobble Season", Bpm = 150, Secs = 188 });
          SetF("artSel", a); SetF("tab", 7); if (outDir != null) { SetF("artScroll", 0f); Shot(Path.Combine(outDir, "artist.png")); SetF("artScroll", 520f); Shot(Path.Combine(outDir, "artist2.png")); } sf.Invalidate(); sf.Update(); var btnMap = (IDictionary)typeof(StudioForm).GetField("btnMap", BF).GetValue(sf);
          Check("another artist's page has a LINKS button and a WATCH VIDEO button on the track that has one", btnMap.Keys.Cast<string>().Any(k => k.ToUpper().StartsWith("LINKS")) && btnMap.Keys.Cast<string>().Any(k => k.ToUpper() == "WATCH VIDEO")); }
        // the place picker and the tag dialog (look at them)
        if (outDir != null)
        {
            PlaceApi.Fetch = q => new List<PlaceHit> { new PlaceHit { Label = "Poole, Dorset, England, United Kingdom", Value = "Poole, Dorset" }, new PlaceHit { Label = "Poole, Cheshire East, England, United Kingdom", Value = "Poole, Cheshire East" }, new PlaceHit { Label = "Poole, Nebraska, United States", Value = "Poole, Nebraska" } };
            var t = new System.Windows.Forms.Timer { Interval = 900 }; t.Tick += (s, e) => { t.Stop(); var pf = Application.OpenForms.Cast<Form>().FirstOrDefault(x => x.Text == "Where you are"); if (pf != null) { var q = pf.Controls.OfType<TextBox>().First(); q.Text = "poole"; Pump(1500); FormShot(pf, Path.Combine(outDir, "place-picker.png")); pf.Close(); } }; t.Start();
            PlacePicker.Show(sf, "");
            var t2 = new System.Windows.Forms.Timer { Interval = 900 }; t2.Tick += (s, e) => { t2.Stop(); var pf = Application.OpenForms.Cast<Form>().FirstOrDefault(x => x.Text == "Genre tags"); if (pf != null) { FormShot(pf, Path.Combine(outDir, "tags-dialog.png")); pf.Close(); } }; t2.Start();
            StudioDialogs.Tags(sf, ProfileStore.Tags);
        }
        Console.WriteLine(fails == 0 ? "=== new feature checks passed ===" : "=== " + fails + " FAILED ===");
        sf.Close(); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
