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
    static int fails; static StudioForm sf; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
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
        Check("the countries work without the internet (pol finds Poland)", PlaceApi.LocalCountries("pol").Any(p => p.Value == "Poland") && PlaceApi.LocalCountries("p").Count == 0 && PlaceApi.Countries.Length >= 190);
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
        { string inserted = ""; EmojiPanel.Show(sf, e => inserted += e); Pump(500); Form pf = Application.OpenForms.Cast<Form>().FirstOrDefault(x => x.Text == "Emojis"); Check("the emoji panel opens as a window of its own", pf != null);
          if (pf != null) { if (outDir != null) FormShot(pf, Path.Combine(outDir, "emoji-panel.png")); var q = pf.Controls.OfType<TextBox>().First(); q.Text = "fire"; Pump(300); if (outDir != null) FormShot(pf, Path.Combine(outDir, "emoji-search.png")); pf.Close(); } }
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
