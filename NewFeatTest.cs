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
          Check("another artist's page has a LINKS button and a MUSIC VIDEO button on the track that has one", btnMap.Keys.Cast<string>().Any(k => k.ToUpper().StartsWith("LINKS")) && btnMap.Keys.Cast<string>().Any(k => k.ToUpper() == "MUSIC VIDEO")); }
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
