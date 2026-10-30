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
    static void BanChecks()
    {
        string r; var a = PcBan.Parse("{\"ok\":true,\"banned\":true,\"reason\":\"spam \\\"links\\\"\"}", out r); Check("a closed PC is read as closed, with the reason", a == true && r == "spam \"links\"", r);
        var b = PcBan.Parse("{\"ok\":true,\"banned\":false,\"reason\":\"\"}", out r); Check("a PC that is fine is read as fine", b == false);
        Check("no answer or an error is neither (a closed PC then stays closed from what it remembered)", PcBan.Parse(null, out r) == null && PcBan.Parse("{\"ok\":false,\"err\":\"slow\"}", out r) == null && PcBan.Parse("<html>", out r) == null);
    }
    static void Check(string n, bool ok, string d = "") { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + (d.Length > 0 ? "   " + d : "")); Console.Out.Flush(); }
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static T F<T>(string n) { return (T)typeof(StudioForm).GetField(n, BF).GetValue(sf); }
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access); [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool CloseDesktop(IntPtr h);
    // a check that needs the real screen (a picture of it): it is skipped, and says so, when the PC is locked (a locked screen cannot be captured)
    static int screenOk = -1;
    // can this PC capture its screen right now? (a small window of a known colour is shown and looked at)
    static void SafeCopy(Graphics g, Point src, Point dst, Size sz) { try { g.CopyFromScreen(src, dst, sz); } catch { } }   // on the hidden test desktop there is no screen to copy: the picture stays empty and those checks are skipped
    static bool ScreenWorks() { if (screenOk >= 0) return screenOk == 1; try { using (var t1 = new Bitmap(1, 1)) using (var g1 = Graphics.FromImage(t1)) g1.CopyFromScreen(0, 0, 0, 0, new Size(1, 1)); } catch { screenOk = 0; return false; } bool ok = false; try { IntPtr dk = OpenInputDesktop(0, false, 0x0100); if (dk != IntPtr.Zero) { CloseDesktop(dk); using (var pf = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual, Location = new Point(20, 20), Size = new Size(60, 60), TopMost = true, BackColor = Color.FromArgb(1, 2, 3) }) { pf.Show(); for (int i = 0; i < 10; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(40); } using (var bm = new Bitmap(10, 10)) using (var gg = Graphics.FromImage(bm)) { gg.CopyFromScreen(new Point(40, 40), Point.Empty, new Size(10, 10)); var px = bm.GetPixel(5, 5); ok = px.R == 1 && px.G == 2 && px.B == 3; } } } } catch { ok = false; } screenOk = ok ? 1 : 0; return ok; }
    // a check that needs the real screen (a picture of it): it is skipped, and says so, when the PC is locked or cannot be captured
    static void CheckScreen(string n, bool ok, string d = "") { if (!ScreenWorks()) { Console.WriteLine("SKIP  " + n + "   (the screen cannot be captured right now: locked?)"); return; } Check(n, ok, d); }
    static bool Known0(string[] ks) { EmojiSprites.ForgetForTest(); foreach (var k in ks) if (EmojiSprites.Get(k) == null) return false; return true; }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static void Shot(string path) { sf.Invalidate(); sf.Update(); Application.DoEvents(); var bmp = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height); using (var g = Graphics.FromImage(bmp)) { var pe = new PaintEventArgs(g, new Rectangle(Point.Empty, sf.ClientSize)); for (int i = 0; i < 3; i++) typeof(StudioForm).GetMethod("PaintAll", BF).Invoke(sf, new object[] { pe }); } bmp.Save(path, ImageFormat.Png); bmp.Dispose(); }
    static void FormShot(Form f, string path) { Pump(300); var bmp = new Bitmap(f.ClientSize.Width, f.ClientSize.Height); f.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height)); bmp.Save(path, ImageFormat.Png); bmp.Dispose(); }

    [STAThread]
    static void Main(string[] args)
    {
        string outDir = args.Length > 0 ? args[0] : null; if (outDir != null) Directory.CreateDirectory(outDir);
        StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_nf_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(StudioEnv.DataDir); Application.EnableVisualStyles();
        Edition.Override = Tier.Fantasy; BanChecks();

        // ---- Spotify links in the converter: the songs are read from Spotify's embed page and each is found on YouTube ----
        {
            Check("Spotify song, album, playlist and artist links are recognised (also with intl-gb/ and ?si=), other links are not", SpotifyLinks.Is("https://open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT?si=x") && SpotifyLinks.Is("https://open.spotify.com/intl-gb/album/6N9PS4QXF1D0OWPk0Sxtb4") && SpotifyLinks.Is("open.spotify.com/playlist/37i9dQZF1DXcBWIGoYBM5M") && SpotifyLinks.Is("spotify:track:4cOdK2wGLETKBW3PvgPWqT") && SpotifyLinks.Is("https://open.spotify.com/artist/0gxyHStUsqpMadRV0Di1Qt") && !SpotifyLinks.Is("https://www.youtube.com/watch?v=abc") && !SpotifyLinks.Is("https://open.spotify.com/episode/abc"));
            SpotifyLinks.FetchHook = u => u.Contains("/track/") ? "<script id=\"__NEXT_DATA__\" type=\"application/json\">{\"props\":{\"pageProps\":{\"state\":{\"data\":{\"entity\":{\"name\":\"Dark \\\"Night\\\"\",\"artists\":[{\"name\":\"Ana Bee\"},{\"name\":\"Cy Dee\"}]}}}}}}</script>"
                : "<script id=\"__NEXT_DATA__\" type=\"application/json\">{\"props\":{\"pageProps\":{\"state\":{\"data\":{\"entity\":{\"trackList\":[{\"title\":\"One\",\"subtitle\":\"Ana Bee\"},{\"title\":\"Two\",\"subtitle\":\"Ana Bee, Cy Dee\"}]}}}}}}</script>";
            string se; var tr = SpotifyLinks.Resolve("https://open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT", out se); var al = SpotifyLinks.Resolve("https://open.spotify.com/album/6N9PS4QXF1D0OWPk0Sxtb4", out se);
            Check("a Spotify song becomes a YouTube search for its artists and title (nothing in it can break the search)", tr.Count == 1 && tr[0].Key == "Ana Bee, Cy Dee - Dark \"Night\"" && tr[0].Value.StartsWith("ytsearch1:Ana Bee, Cy Dee - Dark  Night") && !tr[0].Value.Contains("\""), tr.Count > 0 ? tr[0].Value : se);
            Check("a Spotify album or playlist becomes every one of its songs, in order", al.Count == 2 && al[0].Key == "Ana Bee - One" && al[1].Key == "Ana Bee, Cy Dee - Two", al.Count.ToString());
            SpotifyLinks.FetchHook = u => "<html>nothing</html>"; var none = SpotifyLinks.Resolve("https://open.spotify.com/playlist/37i9dQZF1DXcBWIGoYBM5M", out se); Check("a Spotify page with no song list says so (nothing is converted)", none.Count == 0 && se != null && se.Length > 10, se ?? "");
            SpotifyLinks.FetchHook = null;
            {   // finding the RIGHT song on YouTube: same title, the artist in the title or channel, the same length; covers, slowed versions and other songs are passed over
                var song = new SpotifyLinks.Song { Key = "aheath - WANTED", Artist = "aheath", Title = "WANTED", Secs = 158 }; string why;
                var res = new List<string[]> { new[] { "aaaaaaaa1", "212", "Some Channel", "Random Hit Song (Official Video)" }, new[] { "aaaaaaaa2", "158", "Covers R Us", "aheath - WANTED (cover)" }, new[] { "aaaaaaaa3", "160", "aheath - Topic", "WANTED" }, new[] { "aaaaaaaa4", "120", "aheath", "aheath - WANTED (slowed + reverb)" } };
                var pk = SpotifyLinks.Pick(song, res, out why); Check("a Spotify song picks the YouTube video that really is it (title, artist, length), not a cover, a slowed version or a random song", pk != null && pk[0] == "aaaaaaaa3", pk == null ? why : pk[1]);
                var noPick = SpotifyLinks.Pick(song, new List<string[]> { new[] { "bbbbbbbb1", "200", "Pop Hits", "Another Song - Someone Else" }, new[] { "bbbbbbbb2", "158", "x", "Wanted Dead Or Alive - Bon Jovi" } }, out why);
                { var lz = new SpotifyLinks.Song { Key = "Busygettinlizzy - Next Up", Artist = "Busygettinlizzy", Title = "Next Up", Secs = 135 }; string wl;
                  var lres = new List<string[]> { new[] { "egDndW3cigU", "136", "P110: Music", "BUSYGETTINLIZZY15 - Next Up [Music Video] | P110" }, new[] { "yxw0AknleLk", "130", "P110: Music", "BUSYGETTINLIZZY15 - Hoods Hottest | P110" } };
                  var lp = SpotifyLinks.Pick(lz, lres, out wl); Check("the artist written differently on YouTube (BUSYGETTINLIZZY15 for Busygettinlizzy) still finds the right song", lp != null && lp[0] == "egDndW3cigU", lp == null ? wl : lp[1]);
                  var lz2 = new SpotifyLinks.Song { Key = "Somebody - Next Up", Artist = "Somebody Else", Title = "Next Up", Secs = 135 }; Check("but another artist's song with the same name is still never taken", SpotifyLinks.Pick(lz2, lres, out wl) == null, ""); }
                Check("an edition that ends counts down live (days, then hours:minutes:seconds), and says when it has ended", Edition.CountdownText(new TimeSpan(2, 5, 6, 59)) == "2d 05:06:59" && Edition.CountdownText(TimeSpan.FromSeconds(3725)) == "01:02:05" && Edition.CountdownText(TimeSpan.FromSeconds(-5)) == "ended", Edition.CountdownText(new TimeSpan(2, 5, 6, 59)));
                {   // the Sub-Bass Generator: loud 808s never push past full scale, quiet tails never click, in both methods
                    bool okAll = true; string det = "";
                    foreach (int md in new[] { 0, 1 }) foreach (double am in new[] { 0.006, 0.7 })
                    {
                        int sr = 44100, nn = sr * 3; var l = new float[nn]; var r = new float[nn]; var rq = new Random(5);
                        for (int i = 0; i < nn; i++) { double t = (i % (sr / 2)) / (double)sr; l[i] = r[i] = (float)(am * Math.Exp(-t * 3) * Math.Sin(2 * Math.PI * 55 * i / sr + 3 * Math.Exp(-t * 30)) + (rq.NextDouble() - 0.5) * 0.002); }
                        var o0 = (float[])l.Clone(); var sx = new SubFx(); sx.Prepare(sr); sx.Set("mode", md); for (int p = 0; p < nn; p += 512) { int mm = Math.Min(512, nn - p); var bl = new float[mm]; var br = new float[mm]; Array.Copy(l, p, bl, 0, mm); Array.Copy(r, p, br, 0, mm); sx.Process(bl, br, mm); Array.Copy(bl, 0, l, p, mm); }
                        double sPk = 0, sAp = 0, sMj = 0, sPa = 0; for (int i = 1; i < nn; i++) { double ad = l[i] - o0[i]; sPk = Math.Max(sPk, Math.Abs(l[i])); sAp = Math.Max(sAp, Math.Abs(ad)); sMj = Math.Max(sMj, Math.Abs(ad - sPa)); sPa = ad; }
                        bool ok = sPk <= 1.0001 && sAp > am * 0.2 && sMj <= sAp * 0.05; if (!ok) okAll = false; det += " m" + md + "/" + am + ": peak " + sPk.ToString("0.000") + " sub " + sAp.ToString("0.000") + " jump " + (sMj / Math.Max(sAp, 1e-9) * 100).ToString("0.0") + "%";
                    }
                    Check("the Sub-Bass Generator never crackles: loud 808s stay under full scale and quiet tails do not click (both methods)", okAll, det);
                }
                {   // previews (My Projects, Community) always get the biggest buffer, and the stutter watch fixes a preview too (it used to watch only the mix)
                    var wp = new WaveOutPlayer((x1, x2, x3) => { }, 2); var wf = typeof(WaveOutPlayer).GetField("Frames", BF); var wb = typeof(WaveOutPlayer).GetField("Buffers", BF);
                    var wn = new WaveOutPlayer((x1, x2, x3) => { }); int bigF = (int)wf.GetValue(wp), bigB = (int)wb.GetValue(wp), setF = (int)wf.GetValue(wn);
                    Check("a project preview or a community song plays with the biggest buffer (no stutter); the mix keeps the one from Settings", bigF * bigB == 1024 * 6 && setF == (Prefs.BufEff == 2 ? 1024 : 512), bigF + "x" + bigB + " / " + setF);
                }
                {   // mute one SECTION of a track (right-click): only that piece is silent, the rest of the track plays; it is saved in the project and undo puts it back
                    int sr = 44100, n2 = sr * 4; var x2 = new float[n2]; for (int i = 0; i < n2; i++) x2[i] = (float)(0.5 * Math.Sin(2 * Math.PI * 440 * i / sr)); var cl2 = AudioClip.FromArrays(x2, (float[])x2.Clone(), "m");
                    var me = new StudioEngine(); foreach (var mf in me.MasterFx) mf.Bypass = true; var p1 = StudioEngine.NewTrack("Vox", "vocal", cl2); p1.ClipFrames = sr * 2; var p2 = StudioEngine.NewTrack("Vox", "vocal", cl2); p2.Host = p1; p2.ClipStart = sr * 2; p2.Offset = sr * 2; p2.ClipFrames = sr * 2; me.Tracks.Add(p1); me.Tracks.Add(p2);
                    Func<float[], double, double> rm = (a2, sec) => { int s0 = (int)(sec * sr), s1 = s0 + 4000; double e2 = 0; for (int i = s0; i < s1; i++) e2 += a2[i] * a2[i]; return Math.Sqrt(e2 / 4000); };
                    float[] aL, aR; p2.PieceMuted = true; me.Render(0, n2, new RenderOpts(), null, out aL, out aR); double on1 = rm(aL, 1.0), off2 = rm(aL, 3.0);
                    p2.PieceMuted = false; p1.PieceMuted = true; me.Render(0, n2, new RenderOpts(), null, out aL, out aR); double off1 = rm(aL, 1.0), on2 = rm(aL, 3.0);
                    string mpf = Path.Combine(Path.GetTempPath(), "px_mutesec_" + Guid.NewGuid().ToString("N") + ".ytmproj"); ProjectIO.Save(me, mpf); var me2 = new StudioEngine(); ProjectIO.Load(me2, mpf, z => { }); bool kept = me2.Tracks.Count == 2 && me2.Tracks[0].PieceMuted && !me2.Tracks[1].PieceMuted;   /* (the test has no audio file behind the clip: only the flags are compared) */ try { File.Delete(mpf); } catch { }
                    Check("a muted section is kept in the project file", kept, me2.Tracks.Count + " tracks");
                    Check("a muted section is silent and the rest of its track still plays (either piece, playback and export alike)", on1 > 0.2 && off2 < 0.001 && off1 < 0.001 && on2 > 0.2, on1.ToString("0.000") + " " + off2.ToString("0.000") + " | " + off1.ToString("0.000") + " " + on2.ToString("0.000"));
                }
                Check("Bass Boost starts with its output at -12 dB (big bass, room for the vocals)", Math.Abs(new BassBoost().Get("level") + 12) < 0.001, new BassBoost().Get("level").ToString());
                Check("when YouTube does not have the song, nothing is converted (no random song) and it says why", noPick == null && why != null && why.Contains("not found"), why ?? "");
                SpotifyLinks.SearchHook = q => "PXR|cccccccc1|158|aheath - Topic|WANTED\nPXR|cccccccc2|158|x|Totally Different"; string w2, found; var url = SpotifyLinks.FindOnYouTube("yt-dlp", song, out w2, out found); SpotifyLinks.SearchHook = null;
                Check("the converter gets the address of that very video", url == "https://www.youtube.com/watch?v=cccccccc1" && found == "WANTED", url ?? w2);
                SpotifyLinks.FetchHook = u => u.Contains("itunes.apple.com") ? "{\"resultCount\":2,\"results\":[{\"wrapperType\":\"collection\",\"collectionName\":\"Album\"},{\"wrapperType\":\"track\",\"trackName\":\"Lost\",\"artistName\":\"aheath\",\"trackTimeMillis\":143500}]}"
                    : "{\"data\":[{\"title\":\"Drunk\",\"duration\":140,\"artist\":{\"name\":\"aheath\"}},{\"title\":\"Wanted\",\"duration\":158,\"artist\":{\"name\":\"aheath\"}}]}";
                string ae; var ap = SpotifyLinks.Resolve("https://music.apple.com/gb/album/lost/123456789?i=987654321", out ae); var dz = SpotifyLinks.Resolve("https://www.deezer.com/en/album/302127", out ae); SpotifyLinks.FetchHook = null;
                Check("Apple Music and Deezer links are read the same way (the song, its artist and its length) and found on YouTube", ap.Count == 1 && ap[0].Key == "aheath - Lost" && ap[0].Secs == 144 && dz.Count == 2 && dz[1].Key == "aheath - Wanted" && dz[1].Secs == 158 && SpotifyLinks.ServiceOf("https://music.apple.com/gb/song/x/1") == "Apple Music" && SpotifyLinks.ServiceOf("https://deezer.page.link/abc") == "Deezer" && !SpotifyLinks.Is("https://www.youtube.com/watch?v=abc") && !SpotifyLinks.Is("https://soundcloud.com/a/b"), ap.Count + " / " + dz.Count);
                Check("short Spotify share links (spotify.link) are recognised too", SpotifyLinks.Is("https://spotify.link/AbCdEf123") && SpotifyLinks.Is("spotify.app.link/xyz9"));
            }
        }
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
        { // the X while music plays: the music stops at once and the leave question comes (No keeps the DAW open)
          var asked = new List<string>(); sf.exitAsk = t => { asked.Add(t); return DialogResult.No; };
          sf.eng.Tracks.Clear(); sf.eng.Tracks.Add(StudioEngine.NewTrack("Quiet", "beat", AudioClip.FromArrays(new float[44100 * 20], new float[44100 * 20], "q"))); sf.eng.Seek(0); sf.eng.Play(); Application.DoEvents(); bool xWas = sf.eng.Playing;
          var msg = Message.Create(sf.Handle, 0x00A1, (IntPtr)20, IntPtr.Zero); var wargs = new object[] { msg }; typeof(StudioForm).GetMethod("WndProc", BF).Invoke(sf, wargs);
          for (int i = 0; i < 10; i++) { Application.DoEvents(); System.Threading.Thread.Sleep(10); }
          Check("pressing the X while music plays stops it straight away and asks to leave (No keeps the DAW open)", xWas && !sf.eng.Playing && asked.Count == 1 && asked[0].Contains("LEAVING THE DAW") && !sf.IsDisposed, "was=" + xWas + " playing=" + sf.eng.Playing + " asked=" + asked.Count);
          sf.eng.Stop(); sf.eng.Tracks.Clear(); sf.exitAsk = null; }
        { // the MIDI keyboard plays the selected track's own instrument (not only an instrument whose window is in front), and MIDI REC records it
          sf.CloseInspector();   /* (with the piano roll open the keyboard plays the track it shows: this is about the picked track) */
          sf.eng.Stop(); sf.eng.Shutdown(); StudioEngine.LiveUntil = Environment.TickCount - 1; sf.eng.Tracks.Clear(); var it = StudioEngine.NewTrack("Chords", "inst", null); /* (the sound card is closed first: only this check plays the instrument, no race with the engine) */ it.Instrument = true; var cp = (InstrumentFx)FxRegistry.Create("pxchord"); cp.Prepare(StudioEngine.SR); cp.Owner = it; it.Fx.Add(cp); sf.eng.Tracks.Add(it);
          typeof(StudioForm).GetField("selTrack", BF).SetValue(sf, 0); typeof(StudioForm).GetField("recOn", BF).SetValue(sf, true); MidiInput.Target = null; var keep = StudioEngine.Current; StudioEngine.Current = null;
          MidiInput.Handle(0x90u | (60u << 8) | (100u << 16)); var bl = new float[4096]; var br = new float[4096]; cp.BeginBlock(it, 0, 4096, false); cp.Process(bl, br, 4096); float pk = 0; foreach (var v in bl) pk = Math.Max(pk, Math.Abs(v));
          bool got = pk > 0.001f || ((ChordPadFx)cp).ShowPad >= 0; for (int w8 = 0; w8 < 60 && !got; w8++) { System.Threading.Thread.Sleep(10); cp.BeginBlock(it, 0, 4096, false); cp.Process(bl, br, 4096); foreach (var v in bl) pk = Math.Max(pk, Math.Abs(v)); got = pk > 0.001f || ((ChordPadFx)cp).ShowPad >= 0; }   /* (on a busy PC the note can land a block or two later: keep listening for a moment) */   /* (if the sound card is still running from an earlier check, the engine may play the note before this block does: the chord starting counts) */
          MidiInput.Handle(0x80u | (60u << 8)); StudioEngine.Current = keep; int rec; lock (it.Midi) rec = it.Midi.Count(e => (e.S & 0xF0) == 0x90 && e.D1 == 60);
          Check("a MIDI keyboard note plays the instrument on the selected track (no window open) and MIDI REC records it", got && rec == 1, "peak " + pk.ToString("0.000") + ", recorded " + rec);
          typeof(StudioForm).GetField("recOn", BF).SetValue(sf, false); sf.eng.Stop(); sf.eng.Tracks.Clear(); }
        { // MIDI mapping across the studio: learn, knobs on a track and a plugin, two pads at once, a key on PLAY, remove, profiles, the activity light
          sf.eng.Stop(); sf.eng.Tracks.Clear(); MidiInput.Target = null; var keepE = StudioEngine.Current;
          var drums = StudioEngine.NewTrack("Drums", "beat", AudioClip.FromArrays(new float[44100 * 4], new float[44100 * 4], "d")); sf.eng.Tracks.Add(drums);
          var gainFx = FxRegistry.Create("gain"); gainFx.Prepare(44100); drums.Fx.Add(gainFx); var gp = gainFx.P.First(q => !q.IsChoice);
          var padsT = StudioEngine.NewTrack("Pads", "inst", null); padsT.Instrument = true; var pads = (PadsFx)FxRegistry.Create("pxpads"); pads.Prepare(44100); pads.Owner = padsT; padsT.Fx.Add(pads); sf.eng.Tracks.Add(padsT);
          string wv = Path.Combine(StudioEnv.DataDir, "hit.wav"); { var x = new float[22050]; for (int i = 0; i < x.Length; i++) x[i] = (float)(0.5 * Math.Sin(i * 0.07)); WavIO.Write(wv, x, x, 44100, 24); } pads.SetPad(0, wv, "A"); pads.SetPad(1, wv, "B");
          MidiMap.Load("Test controller"); MidiMap.Clear(); int act0 = MidiMap.ActivityAt;
          MidiMap.StartLearn("vol|Drums", "Drums volume"); MidiInput.Handle(0xB0u | (21u << 8) | (64u << 16)); bool learned = MidiMap.Learning == null && MidiMap.ForTarget("vol|Drums") != null && MidiMap.ForTarget("vol|Drums").Src == "cc:1:21";
          MidiInput.Handle(0xB0u | (21u << 8) | (127u << 16)); Pump(150);
          Check("MIDI Learn: right-click a control, turn a knob on the controller, and that knob now drives it (track volume to the top)", learned && Math.Abs(drums.VolDb - 6) < 0.2, "learned " + learned + ", volume " + drums.VolDb);
          Check("the MIDI light sees every message, and says what it was", MidiMap.ActivityAt != act0 && MidiMap.LastIn.Contains("CC 21"), MidiMap.LastIn);
          MidiMap.StartLearn(MidiMap.FxTarget(gainFx, gp.Id), "Gain: " + gp.Name); MidiInput.Handle(0xB0u | (22u << 8) | (10u << 16)); MidiInput.Handle(0xB0u | (22u << 8) | (0u << 16)); Pump(150);
          Check("a plugin's knob takes a controller knob (all the way down here)", Math.Abs(gp.Val - gp.Min) < 0.01f * Math.Max(1, gp.Max - gp.Min), gp.Name + " = " + gp.Val);
          MidiMap.StartLearn("pad|Pads|0", "Pad 1"); MidiInput.Handle(0x99u | (60u << 8) | (100u << 16)); MidiInput.Handle(0x89u | (60u << 8));
          MidiMap.StartLearn("pad|Pads|1", "Pad 2"); MidiInput.Handle(0x99u | (62u << 8) | (100u << 16)); MidiInput.Handle(0x89u | (62u << 8));
          StudioEngine.Current = null; pads.Reset(); pads.LastIn = -1; MidiInput.Handle(0x99u | (60u << 8) | (110u << 16)); MidiInput.Handle(0x99u | (62u << 8) | (110u << 16));
          var pl = new float[2048]; var pr = new float[2048]; pads.BeginBlock(padsT, 0, 2048, false); pads.Process(pl, pr, 2048); double ppk = 0; foreach (var v in pl) ppk = Math.Max(ppk, Math.Abs(v)); int both = (unchecked(Environment.TickCount - pads.HitAt[pads.PadNote(0)]) < 2000 ? 1 : 0) + (unchecked(Environment.TickCount - pads.HitAt[pads.PadNote(1)]) < 2000 ? 1 : 0);
          StudioEngine.Current = keepE;
          Check("two pads mapped to two keys play their own sounds at the same time", both == 2 && ppk > 0.5 * 0.251 * 1.5, both + " pads hit, peak " + ppk.ToString("0.00"));
          sf.eng.Stop(); for (int w8 = 0; w8 < 20 && sf.eng.Playing; w8++) Pump(50); bool playWas = sf.eng.Playing; MidiMap.StartLearn("transport|play", "PLAY"); MidiInput.Handle(0x90u | (48u << 8) | (100u << 16)); MidiInput.Handle(0x80u | (48u << 8)); MidiInput.Handle(0x90u | (48u << 8) | (100u << 16)); for (int w9 = 0; w9 < 30 && !sf.eng.Playing; w9++) Pump(50);   /* (the song counts as playing once the sound card has started) */
          Check("a key mapped to PLAY starts the song (and plays no note)", !playWas && sf.eng.Playing, "was playing " + playWas + ", playing " + sf.eng.Playing + ", mapped " + (MidiMap.ForTarget("transport|play") != null ? MidiMap.ForTarget("transport|play").Src : "no") + ", mic armed " + F<bool>("micArmed") + ", tracks " + sf.eng.Tracks.Count + ", end " + sf.eng.EndFrame); sf.eng.Stop(); Pump(50);
          MidiMap.Remove("transport|play"); Check("a mapping can be taken off (that key is free again)", MidiMap.ForTarget("transport|play") == null && !MidiMap.Handle(0x90, 48, 100), "");
          int n0 = MidiMap.All.Count; MidiMap.NewProfile("Second pad set"); bool empty = MidiMap.All.Count == 0; MidiMap.Assign("cc:1:30", "pan|Drums", "Drums pan"); MidiMap.Load("Test controller"); bool back = MidiMap.All.Count == n0 && MidiMap.ForTarget("pan|Drums") == null; MidiMap.Load("Second pad set"); bool second = MidiMap.ForTarget("pan|Drums") != null;
          Check("controller profiles: a new one starts empty, each keeps its own maps, and they are saved (switching loads them back)", empty && back && second && MidiMap.Profiles().Contains("Test controller") && MidiMap.Profiles().Contains("Second pad set"), "profiles " + string.Join(", ", MidiMap.Profiles()));
          MidiMap.MakeMpkMini(); Check("an Akai MPK Mini profile: its 8 knobs (CC 70 to 77) turn the plugin in front", MidiMap.All.Count == 8 && MidiMap.All.All(m => m.Target.StartsWith("selfx|")), MidiMap.All.Count + " maps");
          MidiMap.Clear(); sf.eng.Tracks.Clear(); }
        { // AUTO TRIM: the silence before the sound starts is taken off the start of a clip, the clip stays where it is
          var a0 = new float[44100 * 2]; for (int i = 22050; i < a0.Length; i++) a0[i] = (float)(0.4 * Math.Sin(i * 0.05)); var tt = StudioEngine.NewTrack("Late start", "audio", AudioClip.FromArrays(a0, a0, "late")); tt.Offset = 44100;
          long cut = (long)typeof(StudioForm).GetMethod("TrimLeadingSilence", BF).Invoke(sf, new object[] { tt });
          var quiet = StudioEngine.NewTrack("Silent", "audio", AudioClip.FromArrays(new float[44100], new float[44100], "s")); long cut2 = (long)typeof(StudioForm).GetMethod("TrimLeadingSilence", BF).Invoke(sf, new object[] { quiet });
          Check("auto trim: a clip that starts with half a second of silence starts where the sound starts (5 ms kept before it), and the sound stays where it was in the song", Math.Abs(cut - (22050 - 220)) < 40 && tt.ClipStart == cut && tt.Offset == 44100 + cut && cut2 == 0, "trimmed " + cut + " frames, silent clip " + cut2); }
        { // the sound output refreshed while an instrument is being played live comes straight back (it went away until something woke it: hits came out seconds late)
          sf.eng.Stop(); sf.eng.EnsureAudio(); StudioEngine.LiveUntil = Environment.TickCount + 3000; sf.eng.ReopenOutput(); bool liveBack = sf.eng.OutputOpen;
          StudioEngine.LiveUntil = Environment.TickCount - 100000; sf.eng.ReopenOutput(); bool idleOff = !sf.eng.OutputOpen; sf.eng.EnsureAudio();
          Check("a refreshed sound output comes straight back while you play an instrument live (and stays off when nothing plays)", liveBack && idleOff, "live " + liveBack + ", idle off " + idleOff); }
        { // an open Purphex Pads window keeps the MIDI keyboard when you click in the studio (the keys went to the Beat Lab's kit or the picked track's instrument), and gives it back when it closes
          var pt = StudioEngine.NewTrack("Pads", "inst", null); pt.Instrument = true; var pf = (PadsFx)FxRegistry.Create("pxpads"); pf.Prepare(44100); pf.Owner = pt; pt.Fx.Add(pf); sf.eng.Tracks.Add(pt);
          var pw = new PadsWindow(pf); pw.Show(sf); Pump(150); bool held = MidiInput.Target == pf; sf.StudioActivatedMidi(); bool kept = MidiInput.Target == pf;
          pw.Close(); Pump(100); sf.StudioActivatedMidi(); bool freed = MidiInput.Target == null; sf.eng.Tracks.Remove(pt);
          Check("an open Pads window keeps the MIDI keyboard when you click in the studio, and lets go when it closes", held && kept && freed, "held " + held + ", kept " + kept + ", freed " + freed); }
        { // the keyboard always plays something: an audio track picked, it plays the first MIDI track; a song with no MIDI track gets a Keys track on the first key
          sf.eng.Stop(); sf.eng.Tracks.Clear(); MidiInput.Target = null; var keepC = StudioEngine.Current; StudioEngine.Current = null;
          var aud = StudioEngine.NewTrack("Beat", "beat", AudioClip.FromArrays(new float[44100], new float[44100], "b")); var mt = StudioEngine.NewTrack("Lead", "inst", null); mt.Instrument = true; var cp2 = (InstrumentFx)FxRegistry.Create("pxchord"); cp2.Prepare(44100); cp2.Owner = mt; mt.Fx.Add(cp2);
          sf.eng.Tracks.Add(aud); sf.eng.Tracks.Add(mt); typeof(StudioForm).GetField("selTrack", BF).SetValue(sf, 0);
          MidiInput.Handle(0x90u | (60u << 8) | (100u << 16)); var fl = new float[4096]; var fr = new float[4096]; cp2.BeginBlock(mt, 0, 4096, false); cp2.Process(fl, fr, 4096); MidiInput.Handle(0x80u | (60u << 8)); float fpk = 0; foreach (var v in fl) fpk = Math.Max(fpk, Math.Abs(v));
          bool fb = fpk > 0.001f || ((ChordPadFx)cp2).ShowPad >= 0;
          sf.eng.Tracks.Clear(); sf.eng.Tracks.Add(aud); typeof(StudioForm).GetField("selTrack", BF).SetValue(sf, 0); MidiInput.Handle(0x90u | (62u << 8) | (100u << 16)); Pump(200); MidiInput.Handle(0x80u | (62u << 8));
          bool made = sf.eng.Tracks.Any(q => q.Instrument && q.Name == "Keys"); StudioEngine.Current = keepC;
          Check("the MIDI keyboard always plays: with an audio track picked it plays the first MIDI track, and a song with no MIDI track gets a Keys track", fb && made, "fallback " + fb + ", keys track " + made);
          sf.eng.Tracks.Clear(); }
        { // the piano roll's REC: with the piano roll open on a track, the MIDI keyboard plays and records into that track (not the picked one)
          sf.eng.Stop(); sf.eng.Tracks.Clear(); MidiInput.Target = null; var keepR = StudioEngine.Current; StudioEngine.Current = null;
          var other = StudioEngine.NewTrack("Other", "inst", null); other.Instrument = true; var shown = StudioEngine.NewTrack("Shown", "inst", null); shown.Instrument = true; sf.eng.Tracks.Add(other); sf.eng.Tracks.Add(shown);
          typeof(StudioForm).GetField("selTrack", BF).SetValue(sf, 0); sf.OpenPianoRoll(shown); Pump(100); typeof(StudioForm).GetField("recOn", BF).SetValue(sf, true);
          MidiInput.Handle(0x90u | (64u << 8) | (100u << 16)); MidiInput.Handle(0x80u | (64u << 8)); Pump(100); typeof(StudioForm).GetField("recOn", BF).SetValue(sf, false); sf.eng.Stop(); StudioEngine.Current = keepR;
          int inShown, inOther; lock (shown.Midi) inShown = shown.Midi.Count(e => (e.S & 0xF0) == 0x90 && e.D1 == 64); lock (other.Midi) inOther = other.Midi.Count(e => (e.S & 0xF0) == 0x90 && e.D1 == 64);
          Check("REC in the piano roll records the MIDI keyboard into the track the piano roll shows (not the picked one)", sf.PrShown && inShown == 1 && inOther == 0, "shown " + inShown + ", other " + inOther);
          sf.CloseInspector(); sf.eng.Tracks.Clear(); }
        { // previewing a sound in SOUNDS pauses the song
          sf.eng.Stop(); sf.eng.Tracks.Clear(); sf.eng.Tracks.Add(StudioEngine.NewTrack("Song", "beat", AudioClip.FromArrays(new float[44100 * 20], new float[44100 * 20], "s"))); sf.eng.Play(); for (int w = 0; w < 30 && !sf.eng.Playing; w++) Pump(50); bool wasP = sf.eng.Playing;
          string wv2 = Path.Combine(StudioEnv.DataDir, "prev.wav"); { var x = new float[22050]; WavIO.Write(wv2, x, x, 44100, 24); }
          sf.PreviewSound(new SoundItem { Name = "Prev", Kind = "one-shot", Path = wv2, Rel = "prev.wav" }); Pump(100); bool paused = !sf.eng.Playing; Auditioner.Stop(); sf.eng.Stop();
          Check("previewing a sound in SOUNDS or the packs pauses the song", wasP && paused, "was playing " + wasP + ", paused " + paused); sf.eng.Tracks.Clear(); }
        { // dragging a clip or a sound: plugin windows step aside (fade), except Pads when a sound is dragged (it is a place to drop it); back when it ends
          var eqf = FxRegistry.Create("eq"); eqf.Prepare(44100); var gw = new GenericFxWindow(eqf); gw.Show(sf); var pdf = (PadsFx)FxRegistry.Create("pxpads"); pdf.Prepare(44100); var pw2 = new PadsWindow(pdf); pw2.Show(sf); Pump(100);
          typeof(StudioForm).GetField("sndDrag", BF).SetValue(sf, new SoundItem { Name = "x" }); typeof(StudioForm).GetField("sndDragging", BF).SetValue(sf, true); sf.FadePluginWindows();
          double gOp = gw.Opacity, pOp = pw2.Opacity; typeof(StudioForm).GetField("sndDragging", BF).SetValue(sf, false); typeof(StudioForm).GetField("sndDrag", BF).SetValue(sf, null); sf.FadePluginWindows(); double gBack = gw.Opacity;
          gw.Close(); pw2.Close(); Pump(50);
          Check("while a sound is dragged plugin windows fade out of the way (Pads stays, you can drop on it) and come back after", gOp < 0.3 && pOp > 0.9 && gBack > 0.9, "plugin " + gOp.ToString("0.00") + ", pads " + pOp.ToString("0.00") + ", after " + gBack.ToString("0.00")); }
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
          Check("another preset is added in a box of its own: the first preset, your plugins and Bass Lab all stay", vt.Fx.Count(f => f.Preset == "Melodic Rap") == pc && p1 > 0 && vt.Fx.Contains(eq1) && vt.Fx.Contains(rv1) && vt.Fx.Contains(bl) && vt.Fx.First(f => f.Preset == "Melodic Rap").PresetId != vt.Fx.First(f => f.Preset == "Trap Vocal").PresetId);
          string mix = ProjectIO.SerializeMix(sf.eng); Check("the preset name is saved in the project", mix.Contains("fxp|0|") && mix.Contains("Trap Vocal"));
          ProjectIO.RestoreMix(sf.eng, mix); var vt2 = sf.eng.Tracks[0]; Check("and comes back when the project is opened (both presets, each in its own box)", vt2.Fx.Count(f => f.Preset == "Trap Vocal") == p1 && vt2.Fx.Count(f => f.Preset == "Melodic Rap") == pc && vt2.Fx.Count(f => f.Preset == "") == 3 && vt2.Fx.First(f => f.Preset == "Melodic Rap").PresetId.Length > 0 && vt2.Fx.First(f => f.Preset == "Melodic Rap").PresetId != vt2.Fx.First(f => f.Preset == "Trap Vocal").PresetId, vt2.Fx.Count(f => f.Preset == "Trap Vocal") + " preset plugins");
          if (outDir != null) { SetF("selFx", -1); Shot(Path.Combine(outDir, "plugin-panel-boxes.png")); }
          { string tid = vt2.Fx.First(f => f.Preset == "Trap Vocal").PresetId; rem.Invoke(sf, new object[] { vt2, tid }); Check("REMOVE on one preset takes away only that preset (the other preset and your plugins stay)", vt2.Fx.Count(f => f.Preset == "Trap Vocal") == 0 && vt2.Fx.Count(f => f.Preset == "Melodic Rap") == pc && vt2.Fx.Count(f => f.Preset == "") == 3); }
          rem.Invoke(sf, new object[] { vt2, null }); Check("removing every preset leaves only your own plugins", vt2.Fx.Count == 3 && vt2.Fx.Any(f => f.Type == "eq"));
          { put.Invoke(sf, new object[] { vt2, "Melodic Rap", Presets.BuildChain("Melodic Rap") }); typeof(StudioForm).GetMethod("RemoveAllPlugins", BF).Invoke(sf, new object[] { vt2 }); Check("REMOVE ALL takes off every plugin and preset (RAW); only the instrument stays", vt2.Fx.All(f => f is InstrumentFx), vt2.Fx.Count + " left"); }
          if (outDir != null) { var cmp = FxRegistry.Create("comp"); cmp.Prepare(44100); vt2.Fx.Add(cmp); SetF("selFx", vt2.Fx.Count - 1); Shot(Path.Combine(outDir, "plugin-editor-rows.png")); vt2.Fx.Remove(cmp); SetF("selFx", -1); }
          if (outDir != null) { typeof(StudioForm).GetMethod("OpenBrowser", BF).Invoke(sf, new object[] { false, 0 }); Shot(Path.Combine(outDir, "add-effect-boxes.png")); SetF("browserOpen", false); }
          // an instrument's panel is only the OPEN button
          Check("Bass Lab's panel is as short as the button", (float)typeof(StudioForm).GetMethod("EditorHeight", BF | BindingFlags.Static).Invoke(null, new object[] { bl }) < 120);
          sf.eng.Tracks.Clear();
          // PERFORMANCE MODE
          { var st = new SettingsForm(app); st.Show(); Pump(400); var pcb = (CheckBox)typeof(SettingsForm).GetField("perfChk", BF).GetValue(st); Check("Settings has the PERFORMANCE MODE switch", pcb != null && pcb.Text.StartsWith("PERFORMANCE MODE")); for (int pi = 0; pi < 7 && outDir != null; pi++) { typeof(SettingsForm).GetMethod("ShowPage", BF).Invoke(st, new object[] { pi }); Pump(150); if (pcb.Visible) FormShot(st, Path.Combine(outDir, "settings-animation.png")); } pcb.Checked = true; Check("ticking it turns PERFORMANCE MODE on", Prefs.PerfMode); pcb.Checked = false; Check("and unticking turns it off", !Prefs.PerfMode); st.Close(); }
          Prefs.PerfMode = true; Check("PERFORMANCE MODE does not limit the frame rate: 240", Prefs.FpsEff == 240); string pl = string.Join("\n", Prefs.Lines()); Check("and is saved", pl.Contains("perfmode=True")); Prefs.PerfMode = false; Prefs.Parse("perfmode", "True"); Check("and read back", Prefs.PerfMode); Prefs.PerfMode = false; Check("normal mode uses your frame rate", Prefs.FpsEff == Prefs.FpsCap);
        }
        { // the export window: the two complete mixes exclude each other, no dry options, a trim
          var res = new List<string>(); var tm = new System.Windows.Forms.Timer { Interval = 500 };
          tm.Tick += (o, e) => { tm.Stop(); var ef = Application.OpenForms.Cast<Form>().FirstOrDefault(x => x.Text == "Export"); if (ef == null) { res.Add("noform"); return; }
            var cks = ef.Controls.OfType<CheckBox>().ToList(); res.Add("n=" + cks.Count); res.Add("dry=" + cks.Any(c => c.Text.IndexOf("dry", StringComparison.OrdinalIgnoreCase) >= 0));
            res.Add("a=" + cks[0].Checked + cks[1].Checked); cks[1].Checked = true; res.Add("b=" + cks[0].Checked + cks[1].Checked); cks[0].Checked = true; res.Add("c=" + cks[0].Checked + cks[1].Checked); for (int q = 0; q < cks.Count; q++) { cks[q].Checked = true; int on = 0; foreach (var cc in cks) if (cc.Checked) on++; res.Add("only" + q + "=" + on); } cks[cks.Count - 1].Checked = false; res.Add("last=" + cks[cks.Count - 1].Checked);
            var boxes = ef.Controls.OfType<TextBox>().ToList(); res.Add("t=" + boxes[0].Text + "," + boxes[1].Text);
            if (outDir != null) { res.Add("cs=" + ef.ClientSize.Height + "/" + ef.Height); var bm = new Bitmap(ef.Width, ef.Height); ef.DrawToBitmap(bm, new Rectangle(0, 0, bm.Width, bm.Height)); bm.Save(Path.Combine(outDir, "export-dialog.png"), ImageFormat.Png); bm.Dispose(); }
            ef.Close(); };
          tm.Start(); StudioDialogs.ExportOptions(sf, "My song", 95.0); string rj = string.Join(" ", res);
          Check("the export window has five choices and no dry vocals or dry instrumental", rj.Contains("n=5") && rj.Contains("dry=False"), rj);
          Check("only ONE export box can be ticked at a time, and the last one cannot be un-ticked", rj.Contains("only0=1") && rj.Contains("only1=1") && rj.Contains("only2=1") && rj.Contains("only3=1") && rj.Contains("only4=1") && rj.Contains("last=True"), rj);
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
          // Purphex Music opens on the mix with a neon welcome box on top (not on My Projects)
          sf.ShowWelcome(); Pump(200); Check("opening Purphex Music stays on the mix (tab 0) with the welcome box on top, not on My Projects", F<int>("tab") == 0 && F<bool>("welcomeOn") && sf.WelcomeShowing); Pump(100); { var bm0 = typeof(StudioForm).GetField("btnMap", BF); var bmap = (Dictionary<string, RectangleF>)bm0.GetValue(sf); Check("the welcome box has START A NEW PROJECT and OPEN A PROJECT FILE buttons", bmap.ContainsKey("START A NEW PROJECT") && bmap.ContainsKey("OPEN A PROJECT FILE...")); } if (outDir != null) Shot(Path.Combine(outDir, "welcome-box.png"));
          SetF("welcomeOn", false); SetF("tab", 0);
          { var cn = (string[])typeof(StudioForm).GetField("MenuNames", BF | BindingFlags.Static).GetValue(null); int ci = Array.IndexOf(cn, "Community"); var its = (System.Collections.IList)typeof(StudioForm).GetMethod("MenuItems", BF).Invoke(sf, new object[] { ci }); string cT0 = (string)its[0].GetType().GetField("Text").GetValue(its[0]), cT1 = (string)its[1].GetType().GetField("Text").GetValue(its[1]); Check("Community is a list in the side panel with two separate choices (community tab, artists page) and no numbers in front", its.Count == 2 && cT0.StartsWith("Community tab") && cT1.StartsWith("Artists page") && !char.IsDigit(cT0[0]) && !char.IsDigit(cT1[0]), cT0 + " | " + cT1); ((Action)its[0].GetType().GetField("Do").GetValue(its[0]))(); Check("the first choice opens the Community page", F<int>("tab") == 4, "tab " + F<int>("tab")); SetF("tab", 0); }
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
          for (int i = 0; i < 5; i++) { string p = Path.Combine(d0, "Song " + (i + 1) + ".ytmproj"); File.WriteAllText(p, "x"); ProjectLibrary.Entries.Add(new ProjectEntry { Path = p, Name = "Song " + (i + 1), Tracks = 3, Bpm = 140, Seconds = 150, OnProfile = i == 0, Updated = DateTime.Now, Opened = DateTime.Now }); }
          VideoRecs.Add(CommunitySongs.PidOf(ProjectLibrary.Entries[0].Path)); SetF("projectsLoaded", true); SetF("tab", 3); sf.eng.Tracks.Clear(); Pump(250); if (outDir != null) Shot(Path.Combine(outDir, "projects-cards.png"));
          // an EP or an album from the projects: a neon box in the studio (a name, EP or ALBUM, as many projects as you like)
          sf.releaseHook = null; SetF("askHook", (Func<string, bool>)(t2 => false)); typeof(StudioForm).GetMethod("NewReleaseClick", BF).Invoke(sf, null); Pump(500); Check("the EP / ALBUM neon box opens inside the studio (not a plain window)", F<bool>("relOpen") && !Application.OpenForms.Cast<Form>().Any(x => x.Text == "New EP or album"));
          if (outDir != null) Shot(Path.Combine(outDir, "release-neon.png"));
          { var rpick = F<HashSet<string>>("relPick"); var rname = F<TextBox>("relNameBox"); var rmk = typeof(StudioForm).GetMethod("ReleaseCreate", BF); var ents = ProjectLibrary.Entries.ToList();
            rpick.Add(ents[0].Path); rname.Text = "Late Hours"; rmk.Invoke(sf, null); string rerr1 = F<string>("relErr"); rpick.Add(ents[1].Path); rpick.Add(ents[2].Path); rname.Text = "L"; rmk.Invoke(sf, null); string rerr2 = F<string>("relErr"); rname.Text = "Late Hours EP"; rmk.Invoke(sf, null); string rerr3 = F<string>("relErr"); rpick.Add(ents[3].Path);
            if (outDir != null) { Pump(300); Shot(Path.Combine(outDir, "release-neon-picked.png")); }
            rname.Text = "Late Hours EP"; rmk.Invoke(sf, null); Pump(200);
            Check("an EP needs at least 4 songs: one or three are refused (with a sentence), so is a too short name; with 4 it is made and the box closes", !F<bool>("relOpen") && ReleaseStore.All.Count == 1 && ReleaseStore.All[0].Title == "Late Hours EP" && ReleaseStore.All[0].Kind == "ep" && ReleaseStore.All[0].Pids.Count == 4 && rerr1.ToUpper().Contains("AT LEAST 4") && rerr2.ToUpper().Contains("GIVE IT A NAME") && rerr3.ToUpper().Contains("1 MORE"), rerr1 + " | " + rerr2 + " | " + rerr3 + " | " + ReleaseStore.All.Count);
            Check("an album needs at least 5 songs (4 are refused), an EP is fine with 4", ReleaseStore.Check("album", "Big One", 4) != null && ReleaseStore.Check("album", "Big One", 5) == null && ReleaseStore.Check("ep", "Short One", 4) == null && ReleaseStore.Check("ep", "Short One", 3) != null, "");
            ReleaseStore.ResetForTest(); SetF("askHook", null); }
          sf.releaseHook = list => new Release { Kind = "album", Title = "City Lights", Pids = list.Select(x => CommunitySongs.PidOf(x.Path)).ToList() }; SetF("askHook", (Func<string, bool>)(t => false)); ProfileStore.Published = false; typeof(StudioForm).GetMethod("NewReleaseClick", BF).Invoke(sf, null); SetF("askHook", null); sf.releaseHook = null;
          Check("making a release keeps it (and asks before it publishes the projects that are private)", ReleaseStore.All.Count == 1 && ReleaseStore.All[0].Title == "City Lights" && ReleaseStore.All[0].Kind == "album" && !ProjectLibrary.Entries[1].OnProfile);
          Check("the profile gets the release only with its published projects", ReleaseStore.Json(ProjectLibrary.Entries) == "[]"); for (int q = 1; q < 4; q++) ProjectLibrary.Entries[q].OnProfile = true; Check("an album with only 4 of its songs published does not show yet", !ReleaseStore.Json(ProjectLibrary.Entries).Contains("City Lights")); ProjectLibrary.Entries[4].OnProfile = true; Check("with all 5 published it shows on the profile", ReleaseStore.Json(ProjectLibrary.Entries).Contains("City Lights"));
          {   // My Projects: an EP is ONE card (its projects are inside it), opening it lists them; while you search they show one by one
            var vis = ProjectLibrary.Entries.ToList(); var rels = sf.GroupProjectReleases(vis); Check("on My Projects the album is one card and its songs are not separate cards", rels.Count == 1 && vis.Count == 0, rels.Count + " releases, " + vis.Count + " loose");
            { var loose1 = new ProjectEntry { Path = Path.Combine(Path.GetTempPath(), "px-loose-one.pxp"), Name = "Loose one" }; var loose2 = new ProjectEntry { Path = Path.Combine(Path.GetTempPath(), "px-loose-two.pxp"), Name = "Loose two" };
              var mix = new List<ProjectEntry> { loose1, ProjectLibrary.Entries[2], loose2, ProjectLibrary.Entries[0] }; sf.GroupProjectReleases(mix); var ord = sf.projCardOrder;
              Check("an EP or album sits where its newest song is in the list (like any project), not first just because it is an EP", ord.Count == 3 && ord[0] == loose1 && ord[1] is Release && ord[2] == loose2, string.Join(", ", ord.Select(o => o is Release ? "EP" : ((ProjectEntry)o).Name))); sf.GroupProjectReleases(ProjectLibrary.Entries.ToList()); }
            sf.OpenProjEp(rels[0]); Check("opening it shows its songs in order (click one to open it, PLAY to hear it)", sf.projEpOpen, ""); var firstPid = rels[0].Pids[0]; sf.MoveInRelease(0, 1); Check("moving a song down in an EP makes it the next track (the order is kept)", rels[0].Pids[1] == firstPid && ReleaseStore.All[0].Pids[1] == firstPid, ""); SetF("projEpOpen", false);
            SetF("projSearch", "Song"); var vis2 = ProjectLibrary.Entries.ToList(); Check("while you search the songs show one by one", sf.GroupProjectReleases(vis2).Count == 0 && vis2.Count == 5, ""); SetF("projSearch", "");
            var fd = new List<FeedSong>(); for (int q = 0; q < 4; q++) fd.Add(new FeedSong { Id = "e" + q, Title = "T" + q, ArtistId = "a1", Artist = "A", Kind = "ep", Coll = "Night EP", Cid = "r9", Ci = 3 - q, Cn = 4 }); fd.Add(new FeedSong { Id = "x1", Title = "Solo", ArtistId = "a1", Artist = "A", Kind = "single" });
            SetF("feed", fd); SetF("commFilter", 3); var shown = (List<FeedSong>)typeof(StudioForm).GetMethod("FeedList", BF).Invoke(sf, null); Check("Community EPS: one card per EP", shown.Count(x => x.Cid == "r9") == 1, shown.Count + " cards");
            sf.OpenRelease(fd[0]); Check("clicking the EP shows its songs in order, with PLAY EP", sf.epOpen && sf.epSongs.Count == 4 && sf.epSongs[0].Ci == 0 && sf.epSongs[3].Ci == 3, sf.epSongs.Count + " songs"); SetF("epOpen", false);
            sf.epQueue = new List<FeedSong>(sf.epSongs); sf.epQueueAt = 0; SetF("previewPath", "artist:a1:somethingelse"); Check("PLAY EP stops going on when you play something else", !sf.EpQueueNext() && sf.epQueue == null, "");
            sf.epQueue = new List<FeedSong>(sf.epSongs); sf.epQueueAt = 0; sf.PlayReleaseNow(fd[0], true); Check("PLAY NOW on a release card pressed again stops it", sf.epQueue == null, "");
            {   // My Projects: PLAY NOW plays the songs of the EP one after another
                var t1 = Path.Combine(Path.GetTempPath(), "px-epq-1.pxp"); var t2 = Path.Combine(Path.GetTempPath(), "px-epq-2.pxp"); File.WriteAllText(t1, "x"); File.WriteAllText(t2, "x");
                var q1 = new ProjectEntry { Path = t1, Name = "Q one" }; var q2 = new ProjectEntry { Path = t2, Name = "Q two" }; var gone = new ProjectEntry { Path = Path.Combine(Path.GetTempPath(), "px-epq-gone.pxp"), Name = "Gone" };
                sf.projQueue = new List<ProjectEntry> { q1, gone, q2 }; sf.projQueueAt = 0; SetF("previewPath", t1); bool next = sf.ProjQueueNext();
                Check("when a song of the EP ends the next one starts (a song whose file is gone is skipped)", next && sf.projQueueAt == 2 && string.Equals(F<string>("previewPath"), t2, StringComparison.OrdinalIgnoreCase), sf.projQueueAt + " " + F<string>("previewPath"));
                SetF("previewPath", t2); Check("after the last song it stops", !sf.ProjQueueNext() && sf.projQueue == null, "");
                sf.projQueue = new List<ProjectEntry> { q1, q2 }; sf.projQueueAt = 0; SetF("previewPath", "somewhere-else.pxp"); Check("playing something else ends the EP queue", !sf.ProjQueueNext() && sf.projQueue == null, "");
                typeof(StudioForm).GetMethod("StopPreview", BF).Invoke(sf, null); try { File.Delete(t1); File.Delete(t2); } catch { }
            }
            SetF("feed", new List<FeedSong>()); SetF("commFilter", 0); }
        {   // MUSIC HUB: in the side panel right under Community; the official sign-in (OAuth with PKCE), encrypted keys, the APIs and the public catalogues
            var names = (string[])typeof(StudioForm).GetField("MenuNames", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null); int ci = Array.IndexOf(names, "Community");
            Check("Music Hub is in the side panel, right under Community", ci >= 0 && ci + 1 < names.Length && names[ci + 1] == "Music Hub", string.Join(",", names));
            var md = (Action)typeof(StudioForm).GetMethod("MenuDirect", BF).Invoke(sf, new object[] { ci + 1 }); md(); using (var bmH = new Bitmap(1500, 800)) using (var gH = Graphics.FromImage(bmH)) typeof(StudioForm).GetMethod("PaintAll", BF).Invoke(sf, new object[] { new PaintEventArgs(gH, new Rectangle(0, 0, 1500, 800)) });
            Check("clicking Music Hub opens the hub page in the studio (no extra window, no browser started yet)", F<int>("tab") == 9 && F<object>("hubBObj") == null, "tab " + F<int>("tab"));
            Check("the hub page lists every service and shows CONNECT ACCOUNTS and BACK TO DAW", F<List<HitReg>>("regs").Count > 20 && HubServices.All.Length >= 10, F<List<HitReg>>("regs").Count + " regions");
            Check("every service is an official https site with its own search", HubServices.All.All(s => s.Home.StartsWith("https://") && s.SearchUrl.Contains("{q}") && s.Origins.All(o => o.StartsWith("https://"))) && HubServices.Get("spotify").Search("lo fi") == "https://open.spotify.com/search/lo%20fi");
            Check("PKCE: the code challenge is SHA-256 of the verifier in base64url (checked against another implementation)", HubApi.Challenge("dBjftJeZ4CVP-mJ92K27uhbUJU1p1r_wW1gFWFOEjXk") == "ngF5GsXcbwljx6u133FFr3Xht9xooA_DuaX_3QwODtc", HubApi.Challenge("dBjftJeZ4CVP-mJ92K27uhbUJU1p1r_wW1gFWFOEjXk"));
            HubVault.Set("spotify.id", "abc123"); var sp = HubApi.Get("spotify"); string au = sp.AuthorizeUrl(HubApi.Challenge("v"), "st");
            Check("the sign-in goes to Spotify's own page with S256, the 127.0.0.1 redirect and read-only scopes", au.StartsWith("https://accounts.spotify.com/authorize?") && au.Contains("code_challenge_method=S256") && au.Contains("redirect_uri=http%3A%2F%2F127.0.0.1%3A43821%2Fcallback") && au.Contains("user-library-read") && !au.Contains("modify"), au);
            HubApi.PostHook = (u, f) => u.Contains("spotify") && f.Contains("grant_type=authorization_code") && f.Contains("code_verifier=") ? "{\"access_token\":\"AAA\",\"refresh_token\":\"RRR\",\"expires_in\":3600}" : f.Contains("grant_type=refresh_token") ? "{\"access_token\":\"BBB\",\"expires_in\":3600}" : "{}";
            sp.Exchange("code1", "verifier1"); bool enc = !File.ReadAllText(Path.Combine(StudioEnv.DataDir, "hubvault.dat")).Contains("RRR");
            Check("the tokens are kept, encrypted for this Windows user (not readable in the file)", sp.Connected && sp.Token() == "AAA" && enc, "");
            HubVault.Set("spotify.expires", DateTime.UtcNow.AddMinutes(-1).Ticks.ToString()); Check("a token that ran out is refreshed by itself", sp.Token() == "BBB", "");
            HubApi.GetHook = u => u.Contains("/me/tracks") ? "{\"items\":[{\"track\":{\"id\":\"t1\",\"name\":\"Song A\",\"artists\":[{\"name\":\"Artist A\"}],\"album\":{\"images\":[{\"url\":\"https://i.scdn.co/a.jpg\"}]},\"external_urls\":{\"spotify\":\"https://open.spotify.com/track/t1\"}}}]}" : "{\"items\":[]}";
            var liked = sp.Load("Liked"); Check("Spotify liked songs are listed with title, artist, cover and the page to play", liked.Count == 1 && liked[0].Title == "Song A" && liked[0].Sub == "Artist A" && liked[0].Open == "https://open.spotify.com/track/t1" && liked[0].Art.StartsWith("https://"), liked.Count.ToString());
            sp.Disconnect(); Check("DISCONNECT deletes the sign-in", !sp.Connected && HubVault.Get("spotify.refresh") == "", "");
            HubCatalog.GetHook = u => u.Contains("deezer") ? "{\"data\":[{\"id\":5,\"title\":\"Track D\",\"link\":\"https://www.deezer.com/track/5\",\"artist\":{\"name\":\"Art D\"},\"album\":{\"cover_medium\":\"https://e-cdns-images.dzcdn.net/x.jpg\"}}]}" : "{\"results\":[{\"trackId\":7,\"trackName\":\"Track I\",\"artistName\":\"Art I\",\"artworkUrl100\":\"https://is1.mzstatic.com/100x100bb.jpg\",\"trackViewUrl\":\"https://music.apple.com/us/song/7\"}]}";
            var found = HubCatalog.Search("night"); Check("search with no account finds songs in Deezer's and Apple's public catalogues", found.Count == 2 && found[0].Service == "deezer" && found[1].Service == "apple" && found[1].Art.Contains("300x300"), found.Count.ToString());
            Check("the player script reads the page's own media session and audio element, and presses the service's own buttons", HubPlayerJs.Poll.Contains("mediaSession") && HubPlayerJs.Toggle(HubServices.Get("spotify")).Contains("control-button-playpause") && HubPlayerJs.Next(HubServices.Get("soundcloud")).Contains("skipControl__next"), "");
            Check("tests never start the music browser (and it would be muted under test)", F<object>("hubBObj") == null && StudioForm.HubMuted, "");
            Check("SOCIALS: Instagram, TikTok, Facebook, Snapchat, X and more, each its own official https site", HubServices.Socials.Length >= 12 && new[] { "instagram", "tiktok", "facebook", "snapchat", "x", "discord" }.All(id => HubServices.Get(id) != null && HubServices.Get(id).Social) && HubServices.Socials.All(s => s.Home.StartsWith("https://") && s.Origins.All(o => o.StartsWith("https://")) && s.SearchUrl != null), HubServices.Socials.Length + " socials");
            Check("DISTRIBUTION: DistroKid, TuneCore, CD Baby, UnitedMasters, Amuse and more, each its own official https site", HubServices.Distributors.Length >= 15 && new[] { "distrokid", "tunecore", "cdbaby", "unitedmasters", "amuse", "ditto", "routenote" }.All(id => HubServices.Get(id) != null && HubServices.Get(id).Distro) && HubServices.Distributors.All(s => s.Home.StartsWith("https://") && s.Origins.All(o => o.StartsWith("https://"))), HubServices.Distributors.Length + " distributors");
            {   var td = HubServices.All.First(x => x.Id == "tidal"); string js = td.SignedJs();
                Check("Tidal's SIGNED IN comes from its page (its home page or a Log in button means not signed in)", js.Contains("'tidal.com'") && js.Contains("'listen.tidal.com'") && js.Contains("log ?in"), js.Length + " chars"); }
            HubApi.PostHook = null; HubApi.GetHook = null; HubCatalog.GetHook = null; HubVault.Forget("spotify."); SetF("tab", 0);
        }
        {   // CREATIVE STUDIO: side panel entries, safe project files, autosave recovery, export settings
            var names = (string[])typeof(StudioForm).GetField("MenuNames", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null); int hi = Array.IndexOf(names, "Music Hub");
            Check("Purphex Video Editor and Purphex Photo Editor are in the side panel, under Music Hub", hi >= 0 && names[hi + 1] == "Purphex Video Editor" && names[hi + 2] == "Purphex Photo Editor", string.Join(",", names));
            var proj = new Dictionary<string, object> { { "id", "ptest1" }, { "name", "Test video" }, { "clips", new List<object> { new Dictionary<string, object> { { "id", "c1" }, { "start", 1.5 }, { "_err", true } } } } };
            CreativeStore.Save("video", proj, false); bool rec; var back = CreativeStore.Load("video", "ptest1", out rec);
            Check("a video project saves and opens again (runtime-only fields are not written)", HubApi.S(back, "name") == "Test video" && !rec && !File.ReadAllText(CreativeStore.ProjPath("video", "ptest1")).Contains("_err"), "");
            bool refused = false; try { CreativeStore.SafeWrite(CreativeStore.ProjPath("video", "ptest1"), "{ broken"); } catch { refused = true; }
            Check("a broken save never replaces a good project", refused && HubApi.S(CreativeStore.Load("video", "ptest1", out rec), "name") == "Test video", "");
            System.Threading.Thread.Sleep(30); proj["name"] = "Unsaved change"; CreativeStore.Save("video", proj, true); var rc = CreativeStore.Load("video", "ptest1", out rec);
            Check("after a crash the newer autosave is offered back (and the saved project stays as it was)", rec && HubApi.S(rc, "name") == "Unsaved change" && File.ReadAllText(CreativeStore.ProjPath("video", "ptest1")).Contains("Test video"), "");
            CreativeStore.Rename("video", "ptest1", "Renamed"); string dup = CreativeStore.Duplicate("video", "ptest1"); var lst = CreativeStore.List("video");
            Check("projects can be renamed, duplicated and listed", lst.Count == 2 && lst.Any(x => HubApi.S(x, "name") == "Renamed copy"), lst.Count.ToString());
            CreativeStore.Delete("video", dup); CreativeStore.Delete("video", "ptest1"); Check("projects can be deleted", CreativeStore.List("video").Count == 0, "");
            Check("media kinds are recognised (and unknown files refused)", CreativeStore.KindOf("a.MOV") == "video" && CreativeStore.KindOf("b.flac") == "audio" && CreativeStore.KindOf("c.png") == "image" && CreativeStore.KindOf("d.exe") == null, "");
            Check("export settings: H.264, HEVC, WebM and audio-only use the right encoders", CreativeStore.ExportArgs("mp4-h264", "high", 192, 30, "a.wav", "o.mp4").Contains("libx264") && CreativeStore.ExportArgs("mp4-h265", "good", 192, 30, "a.wav", "o.mp4").Contains("libx265") && CreativeStore.ExportArgs("webm", "good", 160, 30, "a.wav", "o.webm").Contains("libvpx-vp9") && CreativeStore.ExportArgs("webm", "good", 160, 30, "a.wav", "o.webm").Contains("libopus") && CreativeStore.ExportArgs("m4a", "", 256, 30, "a.wav", "o.m4a").Contains("aac -b:a 256k") && !CreativeStore.ExportArgs("m4a", "", 256, 30, "a.wav", "o.m4a").Contains("image2pipe"), "");
        }
        {   // BUTTONS WIN: a button on a card or row takes the click even when the card was added after it; a pop-up still blocks what is behind it
            var regs = F<List<HitReg>>("regs"); var keep = new List<HitReg>(regs); regs.Clear(); var top = typeof(StudioForm).GetMethod("TopRegion", BF); var reg = typeof(StudioForm).GetMethod("Reg", BF);
            SetF("regLayer", 0); var btn = (HitReg)reg.Invoke(sf, new object[] { new RectangleF(100, 100, 80, 30), (Action)(() => { }) }); btn.Button = true;
            var card = (HitReg)reg.Invoke(sf, new object[] { new RectangleF(50, 50, 230, 400), (Action)(() => { }) });
            Check("a button on a project card takes the click (not the card under it)", top.Invoke(sf, new object[] { new PointF(140, 115) }) == btn, "");
            Check("the rest of the card still opens the project", top.Invoke(sf, new object[] { new PointF(60, 300) }) == card, "");
            var bar = (HitReg)reg.Invoke(sf, new object[] { new RectangleF(40, 90, 400, 50), (Action)(() => { }) }); bar.Overlay = true;
            Check("the audio bar (and every bar drawn over the page) takes clicks over a card and over the card's buttons", top.Invoke(sf, new object[] { new PointF(140, 115) }) == bar && top.Invoke(sf, new object[] { new PointF(300, 100) }) == bar, "");
            regs.Remove(bar);
            reg.Invoke(sf, new object[] { new RectangleF(0, 0, sf.ClientSize.Width, sf.ClientSize.Height), (Action)(() => { }) }); var box = (HitReg)reg.Invoke(sf, new object[] { new RectangleF(20, 20, 600, 500), (Action)(() => { }) });
            Check("a button behind an open pop-up does not take its clicks", top.Invoke(sf, new object[] { new PointF(140, 115) }) == box, "");
            regs.Clear(); regs.AddRange(keep);
        }
        {   // SOUND: pack sounds are converted with a windowed sinc (no grit), and the sound pack player never clicks
            int n0 = 48000; var src = new float[n0]; for (int i = 0; i < n0; i++) src[i] = (float)(0.5 * Math.Sin(2 * Math.PI * 10000 * i / 48000.0));
            var y = HqResample.Run(src, 48000, 44100); double err = 0; int cnt = 0; for (int i = 2000; i < y.Length - 2000; i++) { double ideal = 0.5 * Math.Sin(2 * Math.PI * 10000 * i / 44100.0); err += (y[i] - ideal) * (y[i] - ideal); cnt++; }
            err = Math.Sqrt(err / cnt); Check("48 kHz sounds become 44.1 kHz cleanly (a 10 kHz tone stays a clean tone)", y.Length == 44100 && err < 0.004, "error " + err.ToString("0.00000"));
            var AT = typeof(Auditioner); var SB = BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public; Action<string, object> sa = (nm, v) => AT.GetField(nm, SB).SetValue(null, v);
            var tone = new float[44100]; for (int i = 0; i < tone.Length; i++) tone[i] = (float)(0.8 * Math.Cos(2 * Math.PI * 100 * i / 44100.0));
            sa("player", new WaveOutPlayer((a, b, c) => { })); sa("pl", tone); sa("pr", tone); sa("pos", 0); sa("env", 0f); sa("envTarget", 1f); sa("atk", 1f / 66); sa("ol", null); sa("playing", true); sa("resetOnStop", false);
            float vol = SoundLibrary.AuditionVolume; SoundLibrary.AuditionVolume = 1f; bool loopWas = SoundLibrary.LoopPreview; SoundLibrary.LoopPreview = false;
            var render = AT.GetMethod("Render", SB); Func<float[]> blk = () => { var l = new float[1024]; var r = new float[1024]; render.Invoke(null, new object[] { l, r, 1024 }); return l; };
            Func<float[], float, float> jump = (x, prev) => { float m = Math.Abs(x[0] - prev); for (int i = 1; i < x.Length; i++) m = Math.Max(m, Math.Abs(x[i] - x[i - 1])); return m; };
            var b1 = blk(); float j1 = jump(b1, 0); Check("a sound starts without a click (it fades in over 1.5 ms)", j1 < 0.03, j1.ToString("0.000"));
            Auditioner.Seek(0.5); var b2 = blk(); float j2 = jump(b2, b1[1023]); Check("jumping to another place in the sound crossfades (no click)", j2 < 0.03, j2.ToString("0.000"));
            Auditioner.Stop(); var b3 = blk(); float j3 = jump(b3, b2[1023]); Check("stopping fades out (no click) and really stops", j3 < 0.03 && Math.Abs(b3[1023]) < 1e-6 && !Auditioner.IsPlaying, j3.ToString("0.000"));
            sa("pos", 44100 - 300); sa("env", 1f); sa("envTarget", 1f); sa("playing", true); var b4 = blk(); float j4 = jump(b4, b4[0]); Check("the end of a sound always fades to silence (no click when it ends)", j4 < 0.03 && Math.Abs(b4[1023]) < 1e-6, j4.ToString("0.000"));
            SoundLibrary.LoopPreview = true; sa("pos", 44100 - 600); sa("env", 1f); sa("playing", true); var b5 = blk(); float j5 = jump(b5, b5[0]); Check("a looping sound crossfades at the loop point (no click)", j5 < 0.03 && Auditioner.IsPlaying, j5.ToString("0.000"));
            sa("playing", false); sa("player", null); SoundLibrary.AuditionVolume = vol; SoundLibrary.LoopPreview = loopWas;
        }
          ReleaseStore.ResetForTest(); ProjectLibrary.Entries.Clear(); VideoRecs.ResetForTest(); SetF("tab", 0); }
        { // COLOUR EMOJI in writing: pictures made from the emoji font of this PC (a browser that is never shown), drawn over the outlines of a text box and in the writing the program draws itself
          EmojiSprites.Disabled = false; Func<Bitmap, Func<Color, bool>, int> countPx = (bm, pred) => { int n = 0; for (int y = 0; y < bm.Height; y++) for (int x = 0; x < bm.Width; x++) if (pred(bm.GetPixel(x, y))) n++; return n; };
          Func<Color, bool> orange = c => c.R > 200 && c.G > 90 && c.G < 200 && c.B < 80, yellow = c => c.R > 220 && c.G > 170 && c.B < 90, red = c => c.R > 200 && c.G < 70 && c.B < 80, green = c => c.G > 150 && c.R < 120 && c.B < 120;
          string[] ks = { "\U0001F525", "\U0001F600", "\U0001F308", "\u2764\uFE0F", "\U0001F3B8" }; foreach (var k in ks) EmojiSprites.Get(k); for (int i = 0; i < 400 && ks.Any(k => !EmojiSprites.Known(k)); i++) Pump(25);
          var fire = EmojiSprites.Get("\U0001F525"); Check("the colour picture of an emoji is made from the font of this PC (not shipped, nothing downloaded)", fire != null && fire.Width == 64 && countPx(fire, orange) > 150, fire == null ? "none" : "orange " + countPx(fire, orange));
          var grin = EmojiSprites.Get("\U0001F600"); var rain = EmojiSprites.Get("\U0001F308"); var heart = EmojiSprites.Get("\u2764\uFE0F"); Check("a face is yellow, a rainbow has red and green, a heart is red", grin != null && countPx(grin, yellow) > 300 && rain != null && countPx(rain, red) > 40 && countPx(rain, green) > 40 && heart != null && countPx(heart, red) > 300, (grin == null ? "-" : countPx(grin, yellow).ToString()) + " " + (rain == null ? "-" : countPx(rain, red) + "/" + countPx(rain, green)) + " " + (heart == null ? "-" : countPx(heart, red).ToString()));
          Check("the pictures are kept in a folder, and after a restart an emoji is in colour at once (no waiting)", Directory.Exists(Path.Combine(StudioEnv.DataDir, "emoji")) && Directory.GetFiles(Path.Combine(StudioEnv.DataDir, "emoji"), "*.png").Length >= 4 && Known0(ks), "");
          Check("the background around an emoji is see-through", fire != null && fire.GetPixel(1, 1).A < 10 && fire.GetPixel(32, 40).A > 200);
          Check("a heart is found with or without the picture mark, and a pair of surrogates is one emoji", EmojiSprites.KeyOf("\u2764") == "\u2764\uFE0F" && EmojiSprites.KeyOf("\u2764\uFE0F") == "\u2764\uFE0F" && EmojiSprites.ClusterAt("a\U0001F525b", 1) == 2 && EmojiSprites.ClusterAt("\u2764\uFE0F", 0) == 2 && EmojiSprites.ClusterAt("abc", 1) == 0);
          // a text box
          var ef = new Form { StartPosition = FormStartPosition.Manual, Location = new Point(60, 60), Size = new Size(640, 220), TopMost = true, BackColor = Color.FromArgb(10, 5, 25) };
          var etb = new TextBox { Multiline = true, Left = 12, Top = 12, Width = 600, Height = 150, Font = new Font("Segoe UI", 26f), ForeColor = Color.White, BackColor = Color.FromArgb(10, 5, 25), BorderStyle = BorderStyle.None, Text = "\U0001F3B8\U0001F525\u2764\uFE0F\U0001F600\U0001F308 hi" };
          ef.Controls.Add(etb); Win.NeonBox(etb); ef.Show(); ef.Activate(); Pump(900); etb.Invalidate(); Pump(500);
          Func<Bitmap> grab = () => { var p = etb.PointToScreen(Point.Empty); var bm = new Bitmap(etb.Width, etb.Height); using (var gg = Graphics.FromImage(bm)) SafeCopy(gg, p, Point.Empty, etb.Size); return bm; };
          using (var shot = grab()) { if (outDir != null) shot.Save(Path.Combine(outDir, "emoji-colour-textbox.png"), ImageFormat.Png); CheckScreen("the emoji in a text box are in colour (orange fire, yellow face, red heart), not white outlines", countPx(shot, orange) > 80 && countPx(shot, yellow) > 200 && countPx(shot, red) > 100, "orange " + countPx(shot, orange) + " yellow " + countPx(shot, yellow) + " red " + countPx(shot, red)); }
          etb.Text = "ab " + etb.Text; Pump(300); etb.Invalidate(); Pump(400); using (var shot2 = grab()) CheckScreen("and still after the text is changed", countPx(shot2, orange) > 80);
          etb.SelectAll(); etb.Focus(); Pump(300); etb.Invalidate(); Pump(300); using (var shot3 = grab()) { if (outDir != null) shot3.Save(Path.Combine(outDir, "emoji-colour-selected.png"), ImageFormat.Png); CheckScreen("selected text still shows the colour pictures", countPx(shot3, orange) > 40); }
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
              Func<int> coloured = () => { pf.Activate(); pf.BringToFront(); Pump(300); var bmp = new Bitmap(wb.Width, wb.Height); using (var gg = Graphics.FromImage(bmp)) SafeCopy(gg, wb.PointToScreen(Point.Empty), Point.Empty, wb.Size); int n = 0;   /* a picture of the real screen: the browser control does not draw into DrawToBitmap when other windows are open */ for (int y = 120; y < bmp.Height - 10; y += 2) for (int x = 20; x < bmp.Width - 40; x += 2) { var c = bmp.GetPixel(x, y); int mx = Math.Max(c.R, Math.Max(c.G, c.B)), mn = Math.Min(c.R, Math.Min(c.G, c.B)); if (mx - mn > 110 && mx > 190 && c.R > c.B + 40) n++; } if (outDir != null) bmp.Save(Path.Combine(outDir, "emoji-last.png"), ImageFormat.Png); bmp.Dispose(); return n; };
              wb.Document.InvokeScript("setQuery", new object[] { "" }); wb.Document.InvokeScript("eval", new object[] { "cur=0;show();" }); Pump(500); if (outDir != null) coloured();
              CheckScreen("the emojis are in COLOUR (orange and red faces, not white outlines)", coloured() > 150, coloured() + " ready " + wb.ReadyState + " body " + (wb.Document.Body == null ? -1 : wb.Document.Body.InnerHtml.Length) + " url " + wb.Url);
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
            m.Show(sf, new Point(200, 300)); Pump(400); var menuBmp = new Bitmap(520, 220); using (var gg = Graphics.FromImage(menuBmp)) SafeCopy(gg, sf.PointToScreen(new Point(190, 290)), Point.Empty, new Size(520, 220)); menuBmp.Save(Path.Combine(outDir, "menu.png"), ImageFormat.Png); menuBmp.Dispose(); m.Close();
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
        { // ---- 3.36: centred glyph buttons, the welcome box, AUTO EQ, the leave questions, the nickname, the name check, stems, format hints ----
          var gm = typeof(StudioForm).GetMethod("GlyphCentred", BF); string badGl = "";
          foreach (var gl in new[] { "\u2261", "X", "+", "<", ">>", "|<", "-", "M" })
            using (var bm = new Bitmap(60, 40, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                using (var gg = Graphics.FromImage(bm)) { gg.Clear(Color.Black); gg.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; gm.Invoke(sf, new object[] { gg, gl, 24f, Color.White, new RectangleF(6, 4, 48, 32), true }); }
                int x0 = 999, x1 = -1, y0 = 999, y1 = -1; for (int y = 0; y < 40; y++) for (int x = 0; x < 60; x++) if (bm.GetPixel(x, y).R > 90) { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y); }
                float cx = (x0 + x1 + 1) / 2f, cy = (y0 + y1 + 1) / 2f; if (x1 < 0 || Math.Abs(cx - 30) > 1.6 || Math.Abs(cy - 20) > 1.6) badGl += " " + gl + "(" + cx + "," + cy + ")";
            }
          Check("the one and two character buttons (the menu, X, +, <, >>, |<...) have their mark exactly in the middle", badGl.Length == 0, badGl);
          // AUTO EQ: a bass-heavy track makes the EQ cut the bass, and REVERT puts it back
          { sf.eng.Tracks.Clear(); int nS = 44100 * 3; var bl = new float[nS]; for (int i = 0; i < nS; i++) bl[i] = (float)(0.6 * Math.Sin(2 * Math.PI * 90 * i / 44100.0) + 0.02 * Math.Sin(2 * Math.PI * 3000 * i / 44100.0)); var br2 = (float[])bl.Clone();
            sf.eng.Tracks.Add(StudioEngine.NewTrack("Boom", "audio", AudioClip.FromArrays(bl, br2, "boom"))); var meq = (ParametricEq)typeof(StudioForm).GetMethod("MasterFind", BF).Invoke(sf, new object[] { "eq" });
            SetF("askHook", (Func<string, bool>)(t2 => true)); float b0 = meq.Get("b0_g"); typeof(StudioForm).GetMethod("AutoEqClick", BF).Invoke(sf, new object[] { meq, null }); for (int i = 0; i < 1200 && (F<bool>("busy") || i < 5); i++) Pump(25); Pump(300);
            Check("AUTO EQ listens to the mix and sets the EQ (too much bass: the bass band is cut)", meq.Get("b0_g") < -1f && !meq.Bypass, "b0 " + b0 + " -> " + meq.Get("b0_g"));
            typeof(StudioForm).GetMethod("DoRevert", BF).Invoke(sf, null); Check("REVERT puts the EQ back as it was", Math.Abs(meq.Get("b0_g") - b0) < 0.01f, meq.Get("b0_g") + "");
            // on a track it listens to that track alone (before its own effects)
            var teq = FxRegistry.Create("eq"); teq.Prepare(44100); sf.eng.Tracks[0].Fx.Add(teq); typeof(StudioForm).GetMethod("AutoEqClick", BF).Invoke(sf, new object[] { teq, sf.eng.Tracks[0] }); for (int i = 0; i < 1200 && (F<bool>("busy") || i < 5); i++) Pump(25); Pump(300);
            Check("AUTO EQ on a track listens to that track and cuts its bass too", teq.Get("b0_g") < -1f, teq.Get("b0_g") + ""); SetF("askHook", null);
            if (outDir != null) { SetF("tab", 1); Pump(300); Shot(Path.Combine(outDir, "mastering-auto-eq.png")); SetF("tab", 0); }
            sf.eng.Tracks.Clear(); }
          // leaving the DAW with lyrics that were never saved
          { var asked = new List<string>(); sf.eng.Tracks.Clear(); ProjectIO.Lyrics = "First line\nSecond line"; ProjectIO.LyricsKept = ""; var al = typeof(StudioForm).GetMethod("AskLeave", BF);
            sf.exitAsk = t2 => { asked.Add(t2); return t2.StartsWith("YOU ARE LEAVING") ? DialogResult.Yes : DialogResult.No; }; bool r1 = (bool)al.Invoke(sf, null); Check("leaving with lyrics that were never saved asks about them (No leaves anyway)", r1 && asked.Any(t2 => t2.ToUpper().Contains("UNSAVED LYRICS") && t2.Contains(".txt")), string.Join(" || ", asked)); SetF("leaving", false);
            sf.exitAsk = t2 => t2.StartsWith("YOU ARE LEAVING") ? DialogResult.Yes : (t2.ToUpper().Contains("UNSAVED LYRICS") ? DialogResult.Cancel : DialogResult.No); bool r2 = (bool)al.Invoke(sf, null); Check("Cancel keeps you in the DAW", !r2); SetF("leaving", false);
            string lyf = Path.Combine(StudioEnv.DataDir, "lyrics-out.txt"); sf.lyricsPathHook = nme => lyf; sf.exitAsk = t2 => t2.StartsWith("YOU ARE LEAVING") ? DialogResult.Yes : DialogResult.Yes; bool r3 = (bool)al.Invoke(sf, null);
            Check("Yes saves the lyrics as a .txt file first", r3 && File.Exists(lyf) && File.ReadAllText(lyf).Contains("Second line") && ProjectIO.LyricsKept == ProjectIO.Lyrics); SetF("leaving", false);
            asked.Clear(); sf.exitAsk = t2 => { asked.Add(t2); return DialogResult.Yes; }; al.Invoke(sf, null); Check("saved lyrics are not asked about again", !asked.Any(t2 => t2.ToUpper().Contains("UNSAVED LYRICS"))); SetF("leaving", false);
            ProjectIO.Lyrics = ""; ProjectIO.LyricsKept = ""; sf.exitAsk = null; sf.lyricsPathHook = null; }
          // nickname + the name check
          { ProfileStore.Nick = "Wolfie"; ProfileStore.Save(); ProfileStore.Nick = ""; ProfileStore.Load(); Check("the nickname is kept", ProfileStore.Nick == "Wolfie");
            Check("the artists tabs show the nickname in brackets after the name", new ArtistInfo { Name = "Ana", Nick = "Wolfie" }.Shown == "Ana (Wolfie)" && new ArtistInfo { Name = "Ana" }.Shown == "Ana");
            string pj = (string)typeof(StudioForm).GetMethod("BuildProfileJson", BF).Invoke(sf, new object[] { false }); Check("the nickname goes up with the profile", pj.Contains("\"nick\":\"Wolfie\""));
            int asks = 0; sf.nameTakenHook = (nme, sug) => { asks++; return null; }; var nit = typeof(StudioForm).GetMethod("NameIsTaken", BF); SetF("nameEdited", false); nit.Invoke(sf, null);
            Check("a name that is already yours is never asked about (no window)", asks == 0 && F<string>("profStatus").Contains("held by another profile"), F<string>("profStatus")); SetF("nameEdited", true); nit.Invoke(sf, null);
            Check("changing the name on the Profile tab does run the check (the window opens)", asks == 1); sf.nameTakenHook = null; SetF("nameEdited", false); ProfileStore.Nick = ""; ProfileStore.Save(); }
          // the Export window: tracks to pick for the stems, and plain-words help for the formats
          { var resX = new List<string>(); ExportSettings got2 = null; var tmx = new System.Windows.Forms.Timer { Interval = 500 };
            tmx.Tick += (o, e) => { tmx.Stop(); var ef = Application.OpenForms.Cast<Form>().FirstOrDefault(x => x.Text == "Export"); if (ef == null) { resX.Add("noform"); return; } try {
              var pnl = ef.Controls.OfType<Panel>().FirstOrDefault(); var cks = ef.Controls.OfType<CheckBox>().ToList(); var tck = pnl == null ? new List<CheckBox>() : pnl.Controls.OfType<CheckBox>().ToList(); resX.Add("n=" + tck.Count); resX.Add("en0=" + (pnl != null && pnl.Enabled));
              var five = cks.First(c => c.Text.StartsWith("Individual stems", StringComparison.OrdinalIgnoreCase)); five.Checked = true; resX.Add("en1=" + (pnl != null && pnl.Enabled)); tck[1].Checked = false;
              var cmb = ef.Controls.OfType<ComboBox>().ToList(); var desc = ef.Controls.OfType<Label>().First(l => l.Text.IndexOf("WAV 24-bit:", StringComparison.OrdinalIgnoreCase) >= 0); resX.Add("d0=" + (desc.Text.IndexOf("studio quality", StringComparison.OrdinalIgnoreCase) >= 0)); cmb[0].SelectedIndex = 2; resX.Add("d2=" + ef.Controls.OfType<Label>().Any(l => l.Text.IndexOf("HIGHEST QUALITY", StringComparison.OrdinalIgnoreCase) >= 0)); cmb[0].SelectedIndex = 4; resX.Add("d4=" + ef.Controls.OfType<Label>().Any(l => l.Text.IndexOf("MP3 320", StringComparison.OrdinalIgnoreCase) >= 0 && l.Text.IndexOf("smallest", StringComparison.OrdinalIgnoreCase) >= 0));
              cmb[0].SelectedIndex = 0; if (outDir != null) { var bm = new Bitmap(ef.Width, ef.Height); ef.DrawToBitmap(bm, new Rectangle(0, 0, bm.Width, bm.Height)); bm.Save(Path.Combine(outDir, "export-stems.png"), ImageFormat.Png); bm.Dispose(); }
              ef.Controls.OfType<Button>().First(b => string.Equals(b.Text, "Export", StringComparison.OrdinalIgnoreCase)).PerformClick(); } catch (Exception ex7) { resX.Add("ERR " + ex7.Message); try { ef.Close(); } catch { } } };
            tmx.Start(); got2 = StudioDialogs.ExportOptions(sf, "My song", 95.0, new List<string> { "Vocals", "Beat", "Bass" }); string rx = string.Join(" ", resX);
            Check("the Export window lists the project's tracks for the stems (live only when INDIVIDUAL STEMS is ticked)", rx.Contains("n=3") && rx.Contains("en0=False") && rx.Contains("en1=True"), rx);
            Check("the picked tracks are what is exported (the one that was un-ticked is left out)", got2 != null && got2.Stems && got2.StemTracks != null && got2.StemTracks.SequenceEqual(new[] { 0, 2 }), got2 == null ? "null" : (got2.StemTracks == null ? "all" : string.Join(",", got2.StemTracks.Select(i => i.ToString()))));
            Check("every format has a plain-words description (what it is for, how big, the highest quality marked)", rx.Contains("d0=True") && rx.Contains("d2=True") && rx.Contains("d4=True"), rx);
            var engP = new StudioEngine(); for (int i = 0; i < 3; i++) { var cl = new float[4410]; engP.Tracks.Add(StudioEngine.NewTrack("T" + i, "audio", AudioClip.FromArrays(cl, (float[])cl.Clone(), "t" + i))); } engP.Tracks[1].Mute = true;
            var all3 = Exporter.Plan(engP, false, false, false, false, false, false, true, "P", 0); var one = Exporter.Plan(engP, false, false, false, false, false, false, true, "P", 0, new HashSet<Track> { engP.Tracks[1] }); var two = Exporter.Plan(engP, false, false, false, false, false, false, true, "P", 0, new HashSet<Track> { engP.Tracks[0], engP.Tracks[2] });
            Check("stems: no choice = every track that is not muted; a choice = exactly those tracks (even a muted one)", all3.Count == 2 && one.Count == 1 && one[0].Label == "Stem: T1" && two.Count == 2); }
          // OPEN A PROJECT FILE in the welcome box: cancelling the window brings the welcome box back
          { sf.eng.Tracks.Clear(); typeof(StudioForm).GetMethod("ShowWelcome", BF).Invoke(sf, null); Pump(300); var bmw = (Dictionary<string, RectangleF>)typeof(StudioForm).GetField("btnMap", BF).GetValue(sf); RectangleF ob = bmw["OPEN A PROJECT FILE..."]; var rgs = (System.Collections.IList)typeof(StudioForm).GetField("regs", BF).GetValue(sf); object hit = null; foreach (var rg in rgs) { var rr = (RectangleF)rg.GetType().GetField("R").GetValue(rg); if (Math.Abs(rr.X - ob.X) < 0.6f && Math.Abs(rr.Y - ob.Y) < 0.6f && Math.Abs(rr.Width - ob.Width) < 0.6f) hit = rg; }
            sf.openPickHook = () => null; ((Action)hit.GetType().GetField("OnDown").GetValue(hit))(); Pump(200); Check("cancelling OPEN A PROJECT FILE takes you back to the welcome box", F<bool>("welcomeOn") && sf.WelcomeShowing); sf.openPickHook = null; SetF("welcomeOn", false); }
          // the box of an empty track is clickable: it is the top-most thing under the mouse there (not hidden behind the lane)
          { sf.eng.Tracks.Clear(); sf.eng.Tracks.Add(StudioEngine.NewTrack("Empty one", "audio", null)); SetF("tab", 0); SetF("selTrack", 0); Pump(300); Shot(Path.Combine(outDir ?? Path.GetTempPath(), "empty-track-box.png")); var elD = (System.Collections.IDictionary)typeof(StudioForm).GetField("emptyLanes", BF).GetValue(sf); RectangleF eb = RectangleF.Empty; foreach (System.Collections.DictionaryEntry de in elD) eb = (RectangleF)de.Value; var rgl = (System.Collections.IList)typeof(StudioForm).GetField("regs", BF).GetValue(sf); object top = null; float cxm = eb.X + eb.Width / 2, cym = eb.Y + eb.Height / 2; foreach (var rg in rgl) { var rr = (RectangleF)rg.GetType().GetField("R").GetValue(rg); if (rr.Contains(cxm, cym) && rg.GetType().GetField("OnDown").GetValue(rg) != null) top = rg; }
            Check("an empty track has no add-music box to click (pressing the lane does not open the file window)", top != null && Math.Abs(((RectangleF)top.GetType().GetField("R").GetValue(top)).Width - eb.Width) >= 0.6f && !eb.IsEmpty, eb + ""); sf.eng.Tracks.Clear(); }
          // LOCK keeps its tab open: several tracks can be locked one after another, and the locked ones show
          { sf.eng.Tracks.Clear(); for (int i = 0; i < 3; i++) { var cl = new float[44100]; sf.eng.Tracks.Add(StudioEngine.NewTrack("L" + i, "audio", AudioClip.FromArrays(cl, (float[])cl.Clone(), "l" + i))); } typeof(StudioForm).GetMethod("OpenTrackPicker", BF).Invoke(sf, new object[] { 0 }); var pk = typeof(StudioForm).GetMethod("PickTrack", BF); pk.Invoke(sf, new object[] { 0 }); pk.Invoke(sf, new object[] { 2 }); Pump(250); Check("LOCK keeps the tab open so several tracks can be locked in a row", F<bool>("tpOpen") && sf.eng.Tracks[0].Locked && !sf.eng.Tracks[1].Locked && sf.eng.Tracks[2].Locked); if (outDir != null) Shot(Path.Combine(outDir, "lock-picker.png")); pk.Invoke(sf, new object[] { 0 }); Check("pressing a locked track unlocks it (the tab stays open)", F<bool>("tpOpen") && !sf.eng.Tracks[0].Locked); SetF("tpOpen", false);
            { for (int i = 0; i < 3; i++) { sf.eng.Tracks[i].Locked = false; } sf.eng.Tracks[1].ClipGainDb = 2.5f; SetF("gainClip", sf.eng.Tracks[1]); SetF("gainOpen", true); Pump(300); if (outDir != null) Shot(Path.Combine(outDir, "gain-picker.png")); Check("the GAIN box lists the tracks like the track picker and the picked one is the one being set", F<bool>("gainOpen") && ReferenceEquals(F<object>("gainClip"), sf.eng.Tracks[1]));
              { sf.Invalidate(); sf.Update(); Application.DoEvents(); var rl = F<List<HitReg>>("regs"); var vb = rl.LastOrDefault(r => r.Button && Math.Abs(r.R.Width - 86) < 0.5f && Math.Abs(r.R.Height - 30) < 0.5f); bool found = vb != null && vb.OnDown != null; if (found) vb.OnDown(); var eb = F<TextBox>("editBox"); bool opened = eb.Visible; var commit = typeof(StudioForm).GetMethod("CommitEdit", BF);
                eb.Text = "-4.2"; commit.Invoke(sf, new object[] { true }); float g1 = sf.eng.Tracks[1].ClipGainDb; if (found) vb.OnDown(); eb.Text = "50 dB"; commit.Invoke(sf, new object[] { true }); float g2 = sf.eng.Tracks[1].ClipGainDb;
                Check("the gain has a box you can type in: click it, type -4.2, Enter sets exactly -4.2 dB (and 50 is held to +12)", found && opened && Math.Abs(g1 + 4.2f) < 0.01f && Math.Abs(g2 - 12f) < 0.01f, "box " + found + ", typed " + g1 + ", too big " + g2); if (outDir != null) Shot(Path.Combine(outDir, "gain-typed.png")); }
              SetF("gainOpen", false); } sf.eng.Tracks.Clear(); }
          // the pale-rimmed button of the converter is the Fantasy edition's look only: Supporter never gets it
          { var look = typeof(StudioForm).GetMethod("BtnLook", BF); Func<Tier, int> rimBright = tier => { Edition.Override = tier; using (var bm = new Bitmap(120, 40)) { using (var gg = Graphics.FromImage(bm)) { gg.Clear(Color.Black); gg.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; look.Invoke(sf, new object[] { gg, new RectangleF(10, 6, 100, 28), "TEST", 1, 0f, 13f }); } var px = bm.GetPixel(11, 20); return px.R + px.G + px.B; } };
            int fantasyRim = rimBright(Tier.Fantasy), supporterRim = rimBright(Tier.Supporter); Edition.Override = Tier.Fantasy;
            Check("the Fantasy edition has the pale rim all round the buttons, and the Supporter edition does not get it", fantasyRim > 520 && supporterRim < 520 && fantasyRim > supporterRim + 90, "fantasy " + fantasyRim + ", supporter " + supporterRim); }
          // the Edit menu: no CUT, and LOCK / DUPLICATE / DELETE show a tab of the tracks to pick from
          { sf.eng.Tracks.Clear(); for (int i = 0; i < 3; i++) { var cl = new float[4410]; sf.eng.Tracks.Add(StudioEngine.NewTrack("Tr" + i, "audio", AudioClip.FromArrays(cl, (float[])cl.Clone(), "x" + i))); }
            var its = (System.Collections.IList)typeof(StudioForm).GetMethod("MenuItems", BF).Invoke(sf, new object[] { 1 }); var txt = new List<string>(); foreach (var it in its) txt.Add((string)it.GetType().GetField("Text").GetValue(it));
            Check("the Edit menu has no 'Cut a section out' any more, and has Lock, Duplicate and Delete track", !txt.Any(t2 => t2.StartsWith("Cut a section")) && txt.Contains("Lock or unlock a track") && txt.Contains("Duplicate track") && txt.Contains("Delete track"), string.Join(" | ", txt));
            typeof(StudioForm).GetMethod("OpenTrackPicker", BF).Invoke(sf, new object[] { 1 }); Pump(300); Check("pressing Duplicate track shows the tab of your tracks", F<bool>("tpOpen")); if (outDir != null) Shot(Path.Combine(outDir, "track-picker.png"));
            typeof(StudioForm).GetMethod("PickTrack", BF).Invoke(sf, new object[] { 0 }); Check("picking TRACK 1 duplicates that track and closes the tab", sf.eng.Tracks.Count == 4 && !F<bool>("tpOpen"), sf.eng.Tracks.Count + " tracks");
            SetF("tpMode", 0); typeof(StudioForm).GetMethod("PickTrack", BF).Invoke(sf, new object[] { 2 }); Check("Lock picks the track you choose (not only the selected one)", sf.eng.Tracks[2].Locked, ""); sf.eng.Tracks.Clear(); }
          // the sort list, the EP / Album type, the buttons that never touch
          { var sn = (string[])typeof(StudioForm).GetField("SortNames", BF | BindingFlags.Static).GetValue(null); Check("the Sort list has no 'Most tracks' and no 'Longest', and has 'First / oldest track you have'", !sn.Any(t2 => t2.StartsWith("Most")) && !sn.Any(t2 => t2 == "Longest") && sn.Contains("First / oldest track you have") && !sn.Contains("Oldest edited"), string.Join(", ", sn));
            var tn = (string[])typeof(StudioForm).GetField("TypeNames", BF | BindingFlags.Static).GetValue(null); Check("the Type list has EP / Album", tn.Contains("EP / Album"), string.Join(", ", tn));
            string dpj = Path.Combine(StudioEnv.DataDir, "pj"); Directory.CreateDirectory(dpj); ProjectLibrary.Entries.Clear(); for (int i = 0; i < 3; i++) { string p = Path.Combine(dpj, "P" + i + ".ytmproj"); File.WriteAllText(p, "x"); ProjectLibrary.Entries.Add(new ProjectEntry { Path = p, Name = "P" + i, Tracks = 2, Updated = DateTime.Now.AddHours(-i), Opened = DateTime.Now, Kind = "Song" }); }
            ReleaseStore.ResetForTest(); ReleaseStore.Add("ep", "My EP", new List<string> { CommunitySongs.PidOf(ProjectLibrary.Entries[0].Path), CommunitySongs.PidOf(ProjectLibrary.Entries[1].Path) }); SetF("projType", "EP / Album"); var vis0 = (System.Collections.IList)typeof(StudioForm).GetMethod("VisibleProjects", BF).Invoke(sf, null);
            Check("Type: EP / Album shows only the projects that are in an EP or an album", vis0.Count == 2, vis0.Count + " shown"); SetF("projType", ""); ReleaseStore.ResetForTest(); ProjectLibrary.Entries.Clear();
            SetF("tab", 3); SetF("projectsLoaded", true); Pump(200); if (outDir != null) Shot(Path.Combine(outDir, "projects-bar.png")); }
          // Colour A / colour B switches
          { var mA = typeof(StudioForm).GetField("mixA", BF); var mxOn = typeof(StudioForm).GetMethod("MixSetOn", BF); var mxUse = typeof(StudioForm).GetMethod("MixUseOnly", BF); typeof(StudioForm).GetMethod("MixEnsure", BF).Invoke(sf, null);
            var aCol = (Color)mA.GetValue(sf); var bCol = (Color)typeof(StudioForm).GetField("mixB", BF).GetValue(sf); mxOn.Invoke(sf, new object[] { 0, false }); Check("with colour A off the mix is colour B only", sf.MixedNow.ToArgb() == bCol.ToArgb(), "");
            mxOn.Invoke(sf, new object[] { 1, false }); Check("the last colour cannot be switched off", F<bool>("mixBOn") && !F<bool>("mixAOn"), ""); mxOn.Invoke(sf, new object[] { 0, true }); Check("A back on: both again", F<bool>("mixAOn") && F<bool>("mixBOn"), "");
            mxUse.Invoke(sf, new object[] { 0 }); Check("USE A ONLY makes the profile colour A and nothing else", sf.MixedNow.ToArgb() == aCol.ToArgb() && !F<bool>("mixBOn"), ""); mxUse.Invoke(sf, new object[] { 1 }); Check("USE B ONLY makes it B", sf.MixedNow.ToArgb() == bCol.ToArgb() && !F<bool>("mixAOn"), ""); mxOn.Invoke(sf, new object[] { 0, true }); }
          // the Friends page (the layout of Messages) and the FRIENDS button in Messages
          { ProfileStore.Published = true; var fl = F<List<ArtistInfo>>("profFriends"); fl.Clear(); fl.Add(new ArtistInfo { Id = "af1", Name = "Mia Waves", Nick = "Mimi", Bio = "Garage and lofi from the coast. New EP out now.", Followers = 12, Tracks = 3, TimeMade = 7200, Tags = new[] { "garage", "lofi" } }); fl.Add(new ArtistInfo { Id = "af2", Name = "Kali", Bio = "Dubstep.", Followers = 5, Tracks = 1 });
            F<List<ArtistInfo>>("profReqs").Clear(); typeof(StudioForm).GetMethod("OpenFriends", BF).Invoke(sf, null); Pump(300); Check("FRIENDS opens its own page (like Messages) with the first friend picked", F<int>("tab") == 7 && F<int>("artView") == 3 && F<string>("friWithId") == "af2", F<string>("friWithId")); if (outDir != null) Shot(Path.Combine(outDir, "friends-page.png"));
            SetF("artView", 1); Pump(300); Shot(Path.Combine(outDir ?? Path.GetTempPath(), "messages-with-friends-button.png")); var bmap = (Dictionary<string, RectangleF>)typeof(StudioForm).GetField("btnMap", BF).GetValue(sf); Check("the Messages page has a FRIENDS button", bmap.Keys.Any(k => k.StartsWith("FRIENDS")), string.Join(",", bmap.Keys.Take(8)));
            SetF("artView", 0); SetF("tab", 0); fl.Clear(); ProfileStore.Published = false; }
          // the Add Effect box: the instruments (Bass Lab and VSTs) are together, in one box that fits them
          { FxRegistry.Register(new FxRegistry.Info("mock_inst", "Mock Instrument", "Instruments", "a test instrument"), () => FxRegistry.Create("gain")); FxRegistry.Register(new FxRegistry.Info("mock_inst2", "Mock Instrument Two", "Instruments", "a second test instrument"), () => FxRegistry.Create("gain")); typeof(StudioForm).GetMethod("OpenBrowser", BF).Invoke(sf, new object[] { false, 0 }); { var blE = (System.Collections.IList)typeof(StudioForm).GetMethod("BrowserList", BF).Invoke(sf, null); bool noVst = true; foreach (var it in blE) { string c0 = (string)it.GetType().GetField("Category").GetValue(it); if (c0 == "Instruments" || c0 == "Purphex VST") noVst = false; } Check("ADD EFFECT lists effects only: the VSTs are under VSTS in the right panel", noVst && blE.Count > 50, blE.Count + ""); }
            sf.OpenVsts(); var bl2 = (System.Collections.IList)typeof(StudioForm).GetMethod("BrowserList", BF).Invoke(sf, null);
            Check("Bass Lab is the very first plugin in the VSTS box (it makes all your sounds)", bl2.Count > 0 && (string)bl2[0].GetType().GetField("Type").GetValue(bl2[0]) == "synth");
            var cats9 = new List<string>(); foreach (var it in bl2) cats9.Add((string)it.GetType().GetField("Category").GetValue(it)); int in9 = 0; while (in9 < cats9.Count && cats9[in9] == "Instruments") in9++; int pv9 = in9; while (pv9 < cats9.Count && cats9[pv9] == "Purphex VST") pv9++; string third = pv9 < cats9.Count ? cats9[pv9] : "";
            Check("the instruments (Bass Lab first, then the VSTs) are the first group, together, then the Purphex VST effects", in9 >= 3 && pv9 > in9 && !cats9.Skip(pv9).Contains("Purphex VST") && !cats9.Skip(pv9).Contains("Instruments"), in9 + " instruments, then " + (pv9 - in9) + " Purphex VST, then " + third);
            {   // PINNED: Purphex Pads and Key Map sit right under Bass Lab; PIN puts any VST up there too (kept after a restart), UNPIN takes it off
                Func<List<string>> types = () => { SetF("blCache", null); var l = new List<string>(); foreach (var it in (System.Collections.IList)typeof(StudioForm).GetMethod("BrowserList", BF).Invoke(sf, null)) l.Add(((FxRegistry.Info)it).Type); return l; };
                var t1 = types(); Check("Purphex Pads and Key Map are pinned right under Bass Lab", t1.Count > 3 && t1[0] == "synth" && t1[1] == "pxpads" && t1[2] == "pxkeymap", string.Join(",", t1.Take(4)));
                sf.TogglePin("sampler"); SetF("pinnedVsts", null); var t2 = types(); Check("PIN puts a VST at the top too, and it stays pinned after a restart", t2.IndexOf("sampler") == 3, "at " + t2.IndexOf("sampler"));
                sf.TogglePin("sampler"); sf.TogglePin("synth"); var t3 = types(); Check("UNPIN takes it off, and Bass Lab can't be unpinned", t3.IndexOf("sampler") > 3 && t3[0] == "synth", "at " + t3.IndexOf("sampler"));
                sf.TogglePin("sampler"); { string sd = Environment.GetEnvironmentVariable("PADS_SHOT"); if (!string.IsNullOrEmpty(sd)) try { Shot(Path.Combine(sd, "vsts-pinned.png")); } catch { } } sf.TogglePin("sampler");
                try { File.Delete(Path.Combine(StudioEnv.DataDir, "pinned_vsts.txt")); } catch { } SetF("pinnedVsts", null); SetF("blCache", null);
            } if (outDir != null) Shot(Path.Combine(outDir, "add-effect-boxes2.png")); SetF("browserOpen", false); }
          { var fam0 = FontKit.Family; bool hc0 = FontKit.HardCurrent; FontKit.Family = new FontFamily("Georgia"); FontKit.HardCurrent = true; using (var small = FontKit.Make(14, false)) using (var big = FontKit.Make(30, false)) Check("a thin or hollow font writes big titles only: the small writing stays in the clear original font", small.FontFamily.Name == Win.UiFamily && big.FontFamily.Name == "Georgia", small.FontFamily.Name + " / " + big.FontFamily.Name); FontKit.Family = fam0; FontKit.HardCurrent = hc0; Check("a normal font (Segoe UI) is not counted as thin or hollow", !FontKit.IsHard(new FontFamily("Segoe UI"))); }
          // LOCK, FLEX and DEL ask which track (the tab of TRACK 1, 2, 3...) when there is more than one
          { sf.eng.Tracks.Clear(); for (int i = 0; i < 3; i++) { var cl = new float[44100]; sf.eng.Tracks.Add(StudioEngine.NewTrack("Q" + i, "audio", AudioClip.FromArrays(cl, (float[])cl.Clone(), "q" + i))); } SetF("tpOpen", false); typeof(StudioForm).GetMethod("TrackAction", BF).Invoke(sf, new object[] { 3, (Action)(() => { }) }); Check("FLEX with several tracks asks which track", F<bool>("tpOpen") && F<int>("tpMode") == 3); SetF("tpOpen", false);
            bool didIt = false; sf.eng.Tracks.RemoveRange(1, 2); typeof(StudioForm).GetMethod("TrackAction", BF).Invoke(sf, new object[] { 0, (Action)(() => didIt = true) }); Check("with one track LOCK just does it (no question)", didIt && !F<bool>("tpOpen")); sf.eng.Tracks.Clear(); }
          // the EMOJIS button opens and closes the panel
          { EmojiPanel.Toggle(sf, e2 => { }); Pump(300); bool o1 = EmojiPanel.IsOpen; EmojiPanel.Toggle(sf, e2 => { }); Pump(300); Check("the EMOJIS button opens the panel and a second press closes it", o1 && !EmojiPanel.IsOpen); }
        }
        // the mouse wheel never changes a value: no slider, knob or EQ dot answers it (only big pages and lists scroll), and a drop-down, number box or slider control swallows it
        {
            sf.eng.Tracks.Clear(); var wt = StudioEngine.NewTrack("Wheel", "audio", AudioClip.FromArrays(new float[44100 * 3], new float[44100 * 3], "t")); sf.eng.Tracks.Add(wt); SetF("selTrack", 0); SetF("tab", 0);
            var regsW = (List<HitReg>)typeof(StudioForm).GetField("regs", BF).GetValue(sf); int smallWheel = 0; string which = "";
            foreach (var typ in new[] { "comp", "eq" })
            {
                var fxw = FxRegistry.Create(typ); fxw.Prepare(44100); wt.Fx.Add(fxw); SetF("selFx", wt.Fx.Count - 1); Pump(150); sf.Invalidate(); sf.Update(); Pump(100);
                lock (regsW) foreach (var rg in regsW.ToArray()) if (rg.OnWheel != null && !rg.ScrollBar && (rg.R.Height < 90 || rg.R.Width < 90)) { smallWheel++; which += typ + ":" + rg.R + " "; }
                wt.Fx.Remove(fxw); SetF("selFx", -1);
            }
            if (outDir != null) { var osz = sf.ClientSize; sf.ClientSize = new Size(1240, 680); Pump(300); var fq = FxRegistry.Create("eq"); fq.Prepare(44100); wt.Fx.Add(fq); SetF("selFx", wt.Fx.Count - 1); Pump(200); Shot(Path.Combine(outDir, "small-window-eq.png")); SetF("selFx", -1); wt.Fx.Remove(fq); sf.ClientSize = osz; Pump(200); }
            { var osz2 = sf.ClientSize; sf.ClientSize = new Size(1240, 680); Pump(300); SetF("tab", 0); sf.OfferRevert("Technical Rap added as a preset on Hurricane Wisdom", () => { }); sf.Toast("Technical Rap added as a preset: every plugin is editable, your own plugins stay (REVERT puts it back)"); Pump(200); sf.Invalidate(); sf.Update(); Pump(150);
              var tr0 = F<RectangleF>("toastRect"); var rv0 = F<RectangleF>("revertRect"); if (outDir != null) Shot(Path.Combine(outDir, "toast-over-revert.png"));
              Check("the message box never covers the REVERT bar", !tr0.IsEmpty && !rv0.IsEmpty && !tr0.IntersectsWith(rv0) && tr0.Left >= 0 && tr0.Right <= sf.ClientSize.Width && tr0.Top >= 0, tr0 + " vs " + rv0);
              SetF("revertAction", null); SetF("toast", ""); sf.ClientSize = osz2; Pump(200); }
            { sf.eng.Seek(44100); foreach (int tb in new[] { 0, 1, 2 }) { SetF("tab", tb); Pump(150); sf.Invalidate(); sf.Update(); Pump(100); var bm = (Dictionary<string, RectangleF>)typeof(StudioForm).GetField("btnMap", BF).GetValue(sf);
                RectangleF br0 = RectangleF.Empty; RectangleF pl0 = RectangleF.Empty; foreach (var kv in bm) { if (kv.Key == "|<") br0 = kv.Value; if (kv.Key.ToUpper() == "PLAY") pl0 = kv.Value; } Check("the back-to-start button is on tab " + tb + ", left of PLAY", !br0.IsEmpty && !pl0.IsEmpty && br0.Right <= pl0.X, br0 + " " + pl0); }
              SetF("tab", 0); }
            { sf.eng.Tracks.Clear(); var bt = StudioEngine.NewTrack("Keys", "audio", AudioClip.FromArrays(new float[44100 * 3], new float[44100 * 3], "t")); sf.eng.Tracks.Add(bt); SetF("selTrack", 0); SetF("selFx", -1); SetF("tab", 0); Prefs.PanelHidden = false; /* (an earlier check tucked the right panel away) */ Pump(150); sf.Invalidate(); sf.Update(); Pump(100);
              var bmB = (Dictionary<string, RectangleF>)typeof(StudioForm).GetField("btnMap", BF).GetValue(sf); bool has = bmB.ContainsKey("Bass Lab") || bmB.ContainsKey("BASS LAB"); string pk9 = bmB.Keys.FirstOrDefault(k9 => k9.StartsWith("Presets (", StringComparison.OrdinalIgnoreCase)); Check("no separate BASS LAB button any more (Bass Lab is the first VST in + ADD EFFECT); PRESETS is on the mixer strips, not in the right panel", !has && pk9 == null && File.ReadAllText("Studio.Inspector.cs").Contains("Btn(g, prb, \"PRESETS\""), (has ? "still there" : "gone") + ", " + (pk9 ?? "no PRESETS"));
              if (outDir != null) Shot(Path.Combine(outDir, "bass-lab-button.png"));
              sf.OpenVsts(); var bl9 = (System.Collections.IList)typeof(StudioForm).GetMethod("BrowserList", BF).Invoke(sf, null); var first9 = bl9.Count > 0 ? (FxRegistry.Info)bl9[0] : null;
              Check("Bass Lab is at the very top of the VSTS list", first9 != null && first9.Type == "synth", first9 == null ? "empty" : first9.Type);
              typeof(StudioForm).GetMethod("AddFx", BF).Invoke(sf, new object[] { "synth" }); Pump(300); Check("adding Bass Lab from the list puts the synthesizer on the track and selects it", bt.Fx.Count == 1 && bt.Fx[0].Type == "synth" && F<int>("selFx") == 0, bt.Fx.Count + "");
              foreach (var fxb in bt.Fx.ToArray()) { var inb = fxb as InstrumentFx; if (inb != null) inb.CloseWindow(); } sf.eng.Tracks.Clear(); SetF("selFx", -1); }
            { PlaceApi.Fetch = q => new List<PlaceHit> { new PlaceHit { Label = "Poole, Dorset, England, United Kingdom", Value = "Poole, Dorset" } };
              var t3 = new System.Windows.Forms.Timer { Interval = 700 }; t3.Tick += (s3, e3) => { t3.Stop(); var pf = Application.OpenForms.Cast<Form>().FirstOrDefault(x => x.Text == "Where you are"); if (pf == null) return; pf.Controls.OfType<TextBox>().First().Text = "poole"; Pump(1500); var lb = pf.Controls.OfType<ListBox>().First(); typeof(Control).GetMethod("OnMouseClick", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(lb, new object[] { new MouseEventArgs(MouseButtons.Left, 1, 20, 10, 0) }); }; t3.Start();
              string got = PlacePicker.Show(sf, ""); Check("one click on a place picks it and closes the box", got == "Poole, Dorset", got ?? "null"); }
            { var wa0 = new Rectangle(0, 0, 1920, 1040); var psz = new Size(520, 640); bool never = true; string where = "";
              foreach (var dlg in new[] { new Rectangle(200, 300, 420, 180), new Rectangle(1300, 300, 420, 180), new Rectangle(700, 300, 900, 600), new Rectangle(20, 20, 1880, 1000) })
              { var pt = EmojiPanel.BesideDialog(dlg, psz, wa0); var pr = new Rectangle(pt, psz); where += pt + " "; if (dlg.Width < 1000 && pr.IntersectsWith(dlg) && dlg.Width < 1500) never = false; }
              Check("the emoji panel opens BESIDE the box you type in, never on top of it", never, where); }
            { // a link out of the DAW and every delete button ask first; SHARE PROFILE is on the profile
              var asked = new List<string>(); SetF("askHook", (Func<string, bool>)(tx => { asked.Add(tx); return false; }));
              typeof(StudioForm).GetMethod("OpenLink", BF).Invoke(sf, new object[] { "https://www.youtube.com/watch?v=abc" }); typeof(StudioForm).GetMethod("OpenCommunitySite", BF).Invoke(sf, new object[] { "/community" });
              Check("a link asks first: you are leaving this DAW and being redirected to YOUTUBE", asked.Count == 2 && asked[0].Contains("leaving this DAW") && asked[0].Contains("YOUTUBE") && asked[0].Contains("youtube.com"), asked.Count > 0 ? asked[0].Replace((char)10, (char)32) : "no question");
              MixStore.Presets.Add(new MixPreset { Name = "Keep me", A = Color.Red, B = Color.Blue, T = 0.5f }); SetF("mixPresetSel", MixStore.Presets.Count - 1); asked.Clear(); typeof(StudioForm).GetMethod("MixPresetDelete", BF).Invoke(sf, null);
              Check("deleting a colour preset asks, and No keeps it", asked.Count == 1 && MixStore.Presets.Exists(p => p.Name == "Keep me"));
              SetF("askHook", (Func<string, bool>)(tx => true)); typeof(StudioForm).GetMethod("MixPresetDelete", BF).Invoke(sf, null); Check("Yes deletes it", !MixStore.Presets.Exists(p => p.Name == "Keep me"));
              SetF("askHook", null); sf.eng.Tracks.Clear(); SetF("tab", 6); Pump(200); sf.Invalidate(); sf.Update(); Pump(150); var bmS = (Dictionary<string, RectangleF>)typeof(StudioForm).GetField("btnMap", BF).GetValue(sf);
              Check("the profile has a SHARE PROFILE button", bmS.ContainsKey("SHARE PROFILE")); SetF("tab", 0); }
            { // the links window: a box for the title and a box for the address on every row; only streaming and social websites
              bool stayed = false, hadStatus = false; var tl = new System.Windows.Forms.Timer { Interval = 800 };
              tl.Tick += (s4, e4) => { tl.Stop(); try { var pf = Application.OpenForms.Cast<Form>().FirstOrDefault(x => x.Text == "Your links"); if (pf == null) return; var tbs = pf.Controls.OfType<TextBox>().ToList(); var save = pf.Controls.OfType<Button>().First(b => b.Text.ToUpper() == "SAVE LINKS");
                tbs[0].Text = "My YouTube"; tbs[1].Text = "http://www.youtube.com/@official_aheath"; tbs[2].Text = "Evil"; tbs[3].Text = "https://example.com/x"; Pump(150);
                hadStatus = pf.Controls.OfType<Label>().Any(l => l.Text.ToLower() == "not allowed") && pf.Controls.OfType<Label>().Any(l => l.Text.ToUpper().StartsWith("OK")); if (outDir != null) FormShot(pf, Path.Combine(outDir, "links-dialog.png")); save.PerformClick(); Pump(100); stayed = !pf.IsDisposed && pf.Visible;
                tbs[3].Text = "https://www.tiktok.com/@aheath"; Pump(150); save.PerformClick(); } catch (Exception ex4) { Console.WriteLine("FAIL  links window test threw " + ex4.Message); } };
              tl.Start(); string lk = null; try { lk = StudioDialogs.Links(sf, ""); } catch (Exception exL) { Console.WriteLine("FAIL  Links threw " + exL); }
              Check("a website that is not a streaming or social one is refused and the window stays open", hadStatus && stayed);
              Check("with both boxes right on every row the links are saved as title and address (YouTube and TikTok work)", lk != null && lk.Contains("My YouTube | https://www.youtube.com/@official_aheath") && lk.Contains("Evil | https://www.tiktok.com/@aheath"), lk ?? "null"); }
            if (outDir != null) { var ai = new ArtistInfo { Id = "a9", Name = "aheath", Nick = "poo", Bio = "aheath on all platforms!", Tracks = 0, Followers = 0, Fantasy = true }; var al = F<List<ArtistInfo>>("artList"); al.Clear(); al.Add(ai); SetF("artSel", null); SetF("tab", 7); Pump(300); sf.Invalidate(); sf.Update(); Pump(200); Shot(Path.Combine(outDir, "artist-cards.png")); SetF("tab", 6); SetF("profScroll", 0f); ProfileStore.Nick = "poo"; Pump(200); Shot(Path.Combine(outDir, "profile-hero.png")); ProfileStore.Nick = ""; al.Clear(); SetF("tab", 0); }
            { bool pubKeep = ProfileStore.Published, privKeep = ProfileStore.Private; string keyKeep = ProfileStore.Key; ProfileStore.EnsureKey(); ProfileStore.Published = false; string u0 = sf.ProfilePageUrl(); ProfileStore.Published = true; ProfileStore.Private = false; string u1 = sf.ProfilePageUrl(); ProfileStore.Private = true; string u2 = sf.ProfilePageUrl();
              Check("SHARE PROFILE: your own page on the website once the profile is public (not before, not when private)", u0 == "" && u2 == "" && System.Text.RegularExpressions.Regex.IsMatch(u1, "/artist/a[0-9a-f]{12}$"), u1);
              ProfileStore.Published = pubKeep; ProfileStore.Private = privKeep; ProfileStore.Key = keyKeep; }
            Check("no plugin slider or EQ dot answers the mouse wheel", smallWheel == 0, which);
            using (var cbw = new ComboBox()) using (var nuw = new NumericUpDown()) using (var tbw = new TrackBar()) using (var btw = new Button())
                Check("drop-downs, number boxes and slider bars are the controls the wheel is blocked on", NoValueWheel.IsValueControl(cbw) && NoValueWheel.IsValueControl(nuw) && NoValueWheel.IsValueControl(tbw) && !NoValueWheel.IsValueControl(btw));
            {   // leaving the DAW resets it: no tracks, no project, and the welcome box is back for the next time
                sf.eng.Tracks.Add(StudioEngine.NewTrack("Left behind", "vocal", AudioClip.FromArrays(new float[4410], new float[4410], "t"))); SetF("projPath", "C:\\x\\left.ytmp"); SetF("welcomeOn", false);
                sf.ResetAfterLeave(); Check("leaving the DAW clears the tracks and the project, so it opens empty next time", sf.eng.Tracks.Count == 0 && F<string>("projPath") == "" && !sf.HasProject);
                Check("and the welcome box with your projects is back", F<bool>("welcomeOn") && sf.WelcomeShowing);
                SetF("welcomeOn", false);
            }
            {   // any part of a track box picks that track
                for (int q = 0; q < 2; q++) sf.eng.Tracks.Add(StudioEngine.NewTrack("Pick " + q, "vocal", AudioClip.FromArrays(new float[4410], new float[4410], "t")));
                sf.ClientSize = new Size(1500, 800); Application.DoEvents(); SetF("tab", 0); SetF("welcomeOn", false); SetF("selTrack", 0); SetF("projBin", false);
                var pmh = typeof(StudioForm).GetMethod("PaintAll", BF); using (var bmh = new Bitmap(1500, 800)) using (var gh = Graphics.FromImage(bmh)) { var peh = new PaintEventArgs(gh, new Rectangle(Point.Empty, bmh.Size)); for (int i = 0; i < 3; i++) pmh.Invoke(sf, new object[] { peh }); }
                var hdr = new List<HitReg>(); foreach (object o in (System.Collections.IEnumerable)typeof(StudioForm).GetField("regs", BF).GetValue(sf)) { var rg = (HitReg)o; if (rg.R.X <= 1 && rg.R.Width > 300 && rg.R.Width < 420 && rg.R.Height > 90 && rg.R.Height < 220 && rg.OnDown != null) hdr.Add(rg); }
                bool picked = hdr.Count >= 2; if (picked) { hdr.Sort((p, r) => p.R.Y.CompareTo(r.R.Y)); hdr[1].OnDown(); picked = F<int>("selTrack") == 1; hdr[0].OnDown(); picked = picked && F<int>("selTrack") == 0; }
                Check("pressing anywhere on a track's box (not only its name) picks that track", picked, hdr.Count + " boxes");
                sf.eng.Tracks.Clear();
            }
            {   // old project pictures (a blurred, stretched copy fills the square) are found and kept as the picture itself; nothing is stretched
                string old = Path.Combine(StudioEnv.DataDir, "oldcover.ytmp"); File.WriteAllText(old, "PURPHEX\nname|Old\n"); ProjectLibrary.Touch(old, "Old", 2, 120, 60, true, null);
                string oldImg = Path.Combine(StudioEnv.DataDir, "old420.jpg");
                using (var b0 = new Bitmap(420, 420)) { using (var g0 = Graphics.FromImage(b0)) { using (var br = new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0, 0, 420, 420), Color.FromArgb(40, 10, 20), Color.FromArgb(60, 14, 28), 90f)) g0.FillRectangle(br, 0, 0, 420, 420); var rn = new Random(5); for (int i = 0; i < 420; i += 6) for (int j = 140; j < 280; j += 6) using (var br = new SolidBrush(Color.FromArgb(rn.Next(256), rn.Next(256), rn.Next(256)))) g0.FillRectangle(br, i, j, 6, 6); } b0.Save(oldImg, System.Drawing.Imaging.ImageFormat.Jpeg); }
                Directory.CreateDirectory(ProjectLibrary.CoversDir); string cvp = Path.Combine(ProjectLibrary.CoversDir, "oldcover-test.jpg"); File.Copy(oldImg, cvp, true); var oe = ProjectLibrary.Find(old); oe.Cover = cvp;
                using (var im = Image.FromFile(oldImg)) { var box = ProjectLibrary.FindContent(im); Check("the picture itself is found in the middle of an old blurred square", box.HasValue && box.Value.Height < 260 && box.Value.Height > 120 && box.Value.Width > 330, box.HasValue ? box.Value.ToString() : "none"); }
                typeof(ProjectLibrary).GetField("fitDone", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, false); int fixedN = ProjectLibrary.FitOldCovers();
                int oz = 100, ox2, oy2; ProjectLibrary.LoadAdj(old, out oz, out ox2, out oy2); int sw2 = 0, sh2 = 0; if (ProjectLibrary.HasCoverSrc(old)) using (var im2 = Image.FromFile(ProjectLibrary.CoverSrc(old))) { sw2 = im2.Width; sh2 = im2.Height; }
                Check("going through the projects: the old picture is kept on its own (not the stretched background) and shown whole, undistorted", fixedN >= 1 && sh2 > 0 && sh2 < 300 && sw2 > 300 && oz == 0, fixedN + " " + sw2 + "x" + sh2 + " zoom " + oz);
                using (var rb = ProfileStore.RenderFramed(new Bitmap(200, 100), 100, 100, 0, 50, 50, Color.FromArgb(255, 10, 5, 22), false)) { var top = rb.GetPixel(50, 5); Check("a picture that is not square sits on the plain background: no stretched bands", top.R == 10 && top.G == 5 && top.B == 22, top.ToString()); }
                ProjectLibrary.ClearCover(old);
            }
            {   // the Export box remembers the folder you used last (only that)
                string keepF = StudioDialogs.LastExportDir; string tmpF = Path.Combine(StudioEnv.DataDir, "exp-last"); Directory.CreateDirectory(tmpF); StudioDialogs.LastExportDir = tmpF;
                Check("the folder of the last export comes back in the Export box", StudioDialogs.LastExportDir == tmpF, StudioDialogs.LastExportDir);
                Directory.Delete(tmpF, true); Check("a folder that is gone falls back to the Purphex Music folder in Downloads", StudioDialogs.LastExportDir.EndsWith("Purphex Music"), StudioDialogs.LastExportDir);
                try { File.Delete(Path.Combine(StudioEnv.DataDir, "export-folder.txt")); } catch { }
            }
            {   // SPLIT cuts exactly at the needle, also when zoomed in (the grid lines are far apart there)
                sf.eng.Tracks.Clear(); sf.eng.Tracks.Add(StudioEngine.NewTrack("Cut me", "vocal", AudioClip.FromArrays(new float[441000], new float[441000], "t"))); SetF("selTrack", 0); SetF("snapOn", true); SetF("spp", 4.0); SetF("dispPos", 100777.0); SetF("toolsTrack", null);
                var sm = typeof(StudioForm).GetMethod("SplitSelected", BF); sm.Invoke(sf, null); long cutAt = sf.eng.Tracks.Count == 2 ? sf.eng.Tracks[1].Offset : -1;
                Check("zoomed in, SPLIT cuts exactly where the needle is (not at the nearest far-away grid line)", cutAt == 100777, cutAt + " of 100777");
                sf.eng.Tracks.Clear();
                {   // zoomed out with the grid on, the needle just beside a grid line: the split still goes exactly at the needle (it used to jump onto the grid line); and right next to the edge of a clip
                    sf.eng.Tracks.Clear(); sf.eng.Tracks.Add(StudioEngine.NewTrack("Cut me", "vocal", AudioClip.FromArrays(new float[441000], new float[441000], "t"))); SetF("selTrack", 0); SetF("snapOn", true); SetF("spp", 2000.0);
                    long grid = (long)Convert.ToDouble(typeof(StudioForm).GetProperty("GridFrames", BF).GetValue(sf, null)); long near = grid + 3 * 2000; SetF("dispPos", (double)near); sm.Invoke(sf, null); long cut2 = sf.eng.Tracks.Count == 2 ? sf.eng.Tracks[1].Offset : -1;
                    Check("a split goes exactly at the needle, even right beside a grid line (no pull to the grid: as free as moving)", cut2 == near, cut2 + " of " + near);
                    sf.eng.Tracks.Clear(); sf.eng.Tracks.Add(StudioEngine.NewTrack("Edge", "vocal", AudioClip.FromArrays(new float[441000], new float[441000], "t"))); SetF("dispPos", 441.0); sm.Invoke(sf, null);
                    Check("a split 10 ms from the start of a clip works (it used to be refused that close to the edge)", sf.eng.Tracks.Count == 2 && sf.eng.Tracks[1].Offset == 441, sf.eng.Tracks.Count + " pieces"); sf.eng.Tracks.Clear();
                }
            }
            {   // the whole ruler moves the needle, down to its bottom border, also when the tracks are scrolled up under it
                sf.eng.Tracks.Clear(); for (int q = 0; q < 6; q++) sf.eng.Tracks.Add(StudioEngine.NewTrack("Scroll " + q, "vocal", AudioClip.FromArrays(new float[441000], new float[441000], "t")));
                sf.ClientSize = new Size(1500, 800); Application.DoEvents(); SetF("tab", 0); SetF("welcomeOn", false); SetF("vscroll", 120f);
                var pmr = typeof(StudioForm).GetMethod("PaintAll", BF); using (var bmr = new Bitmap(1500, 800)) using (var gr = Graphics.FromImage(bmr)) { var per = new PaintEventArgs(gr, new Rectangle(Point.Empty, bmr.Size)); for (int i = 0; i < 3; i++) pmr.Invoke(sf, new object[] { per }); }
                HitReg rul = null; foreach (object o in (System.Collections.IEnumerable)typeof(StudioForm).GetField("regs", BF).GetValue(sf)) { var rg = (HitReg)o; if (rg.R.Height < 40 && rg.R.Width > 500 && rg.OnDrag != null && (rul == null || rg.R.Y < rul.R.Y)) rul = rg; }   /* the highest one is the ruler (the overview strip is at the bottom) */
                HitReg hit = rul == null ? null : (HitReg)typeof(StudioForm).GetMethod("TopRegion", BF).Invoke(sf, new object[] { new PointF(rul.R.X + 300, rul.R.Bottom - 2) });
                Check("a click on the bottom edge of the ruler still moves the needle (a row scrolled up under it does not take the click)", rul != null && hit != null && hit.R.Height < 40 && hit.OnDrag != null, rul == null ? "no ruler" : (hit == null ? "nothing" : hit.R.ToString()));
                SetF("vscroll", 0f); sf.eng.Tracks.Clear();
            }
            {   // STOP stops where you are (it used to go back to the start); hold and drag a box from ANY empty space, also the empty part of a row and an empty row
                sf.eng.Tracks.Clear(); var ra = StudioEngine.NewTrack("Clip row", "vocal", AudioClip.FromArrays(new float[44100], new float[44100], "t")); var rb = StudioEngine.NewTrack("Empty row", "audio", null); sf.eng.Tracks.Add(ra); sf.eng.Tracks.Add(rb);
                sf.ClientSize = new Size(1500, 800); Application.DoEvents(); SetF("tab", 0); SetF("welcomeOn", false); SetF("vscroll", 0f); SetF("autoFit", false); SetF("spp", 100.0); SetF("scroll", 0L);
                var pmr = typeof(StudioForm).GetMethod("PaintAll", BF); Action paintS = () => { using (var bmr = new Bitmap(1500, 800)) using (var gr = Graphics.FromImage(bmr)) { var per = new PaintEventArgs(gr, new Rectangle(Point.Empty, bmr.Size)); for (int i = 0; i < 2; i++) pmr.Invoke(sf, new object[] { per }); } };
                paintS(); sf.eng.Pos = 44100 * 5; SetF("scroll", 44100L * 3); var bm = (Dictionary<string, RectangleF>)typeof(StudioForm).GetField("btnMap", BF).GetValue(sf); var stopR = bm["STOP"];
                var stopReg = (HitReg)typeof(StudioForm).GetMethod("TopRegion", BF).Invoke(sf, new object[] { new PointF(stopR.X + stopR.Width / 2, stopR.Y + stopR.Height / 2) }); stopReg.OnDown();
                Check("STOP stops where you are: the needle and the view stay put", sf.eng.Pos == 44100 * 5 && F<long>("scroll") == 44100L * 3 && !sf.eng.Playing, sf.eng.Pos / 44100 + " s, view " + F<long>("scroll") / 44100 + " s");
                SetF("scroll", 0L); paintS(); float laneH = F<int>("LaneH");
                float top = (float)typeof(StudioForm).GetProperty("ArrTop", BF).GetValue(sf, null) + 28; var ld = typeof(StudioForm).GetMethod("LaneDown", BF); var ldr = typeof(StudioForm).GetMethod("LaneDrag", BF); var lup = typeof(StudioForm).GetMethod("MarqueeEnd", BF);
                foreach (var rowT in new[] { ra, rb })
                {
                    int ri = sf.eng.Tracks.IndexOf(rowT); var lane = new RectangleF(388, top + ri * laneH, 1000, laneH); var p0 = new PointF(388 + 900, top + ri * laneH + laneH / 2);   // far right of the row: no audio there
                    SetF("MousePos", p0); ld.Invoke(sf, new object[] { rowT, lane }); ldr.Invoke(sf, new object[] { rowT, new PointF(388 + 5, top + 4) }); bool on = F<bool>("marqueeOn"); var ms = (List<Track>)typeof(StudioForm).GetField("multiSel", BF).GetValue(sf); bool got = ms.Contains(ra); lup.Invoke(sf, null);
                    Check("hold and drag on empty space in " + (rowT == ra ? "the empty part of a row" : "an empty row") + " draws a box that selects the clips", on && got, "box " + on + ", clip picked " + got);
                }
                {   // the box stays on the same part of the song when you zoom (it grew huge on zooming out), and dragging it to the right edge scrolls the view
                    int ri = sf.eng.Tracks.IndexOf(ra); var lane = new RectangleF(388, top + ri * laneH, 1000, laneH); var mb = typeof(StudioForm).GetMethod("MarqueeBox", BF); var mt = typeof(StudioForm).GetMethod("MarqueeTo", BF);
                    SetF("MousePos", new PointF(388 + 900, top + ri * laneH + 4)); ld.Invoke(sf, new object[] { ra, lane }); var pB = new PointF(388 + 500, top + ri * laneH + laneH - 4); SetF("MousePos", pB); mt.Invoke(sf, new object[] { pB });
                    float w1 = ((RectangleF)mb.Invoke(sf, null)).Width; SetF("spp", 200.0); float w2 = ((RectangleF)mb.Invoke(sf, null)).Width; SetF("spp", 100.0);
                    Check("zooming out while a box is drawn: the box shrinks with the song instead of growing", w1 > 300 && Math.Abs(w2 - w1 / 2) < 3, w1.ToString("0") + " px, zoomed out " + w2.ToString("0") + " px");
                    var tl = (RectangleF)typeof(StudioForm).GetMethod("TimelineRect", BF).Invoke(sf, null); long sc0 = F<long>("scroll"); var pE = new PointF(tl.Right - 2, pB.Y); for (int i = 0; i < 5; i++) mt.Invoke(sf, new object[] { pE });
                    Check("a box dragged to the right edge scrolls the view along", F<long>("scroll") > sc0, sc0 + " to " + F<long>("scroll")); lup.Invoke(sf, null); SetF("scroll", 0L);
                }
                {   // a clip dragged to another row holds its time (IN SYNC) unless it is pulled clearly sideways
                    int ri = sf.eng.Tracks.IndexOf(ra); var lane = new RectangleF(388, top + ri * laneH, 1000, laneH); long o0 = ra.Offset; double sp = F<double>("spp"); long sc = F<long>("scroll");
                    float cx0 = 388 + (float)((ra.Offset - sc) / sp) + (float)(ra.UsedLength / sp) / 2, cy0 = top + ri * laneH + laneH / 2;
                    SetF("MousePos", new PointF(cx0, cy0)); ld.Invoke(sf, new object[] { ra, lane }); int lm = F<int>("laneMode");
                    ldr.Invoke(sf, new object[] { ra, new PointF(cx0 + 12, cy0 + laneH) }); bool held = ra.Offset == o0 && F<bool>("syncHeld");
                    ldr.Invoke(sf, new object[] { ra, new PointF(cx0 + 80, cy0 + laneH) }); bool freed = ra.Offset != o0 && !F<bool>("syncHeld");
                    Check("moving a clip down a row keeps it in time (a small sideways wobble does not nudge it)", lm == 1 && held, "mode " + lm + ", offset " + o0 + " -> " + ra.Offset);
                    Check("pulling it clearly sideways moves it freely again", freed, ra.Offset.ToString());
                    ra.Offset = o0; SetF("laneMode", 0); SetF("laneMoved", false); typeof(StudioForm).GetMethod("GhostEnd", BF).Invoke(sf, null); SetF("syncHeld", false);
                    // zoomed out: a short clip is easy to pick (it is at least 8 px wide, and a click a few pixels off still finds it)
                    var pl = typeof(StudioForm).GetMethod("PickLayer", BF); var tiny = StudioEngine.NewTrack("Tiny", "beat", AudioClip.FromArrays(new float[2000], new float[2000], "t")); tiny.Host = ra; tiny.Offset = ra.Offset + 44100; lock (sf.eng.Sync) sf.eng.Tracks.Add(tiny);
                    SetF("spp", 2000.0); SetF("scroll", 0L); float tx = 388 + (float)(tiny.Offset / 2000.0); var got1 = (Track)pl.Invoke(sf, new object[] { ra, new PointF(tx + 5, cy0) }); var got2 = (Track)pl.Invoke(sf, new object[] { ra, new PointF(tx + 18, cy0) });
                    Check("zoomed out, a tiny clip on a row is picked when you click it, even a little off", got1 == tiny && got2 == tiny, (got1 == tiny) + " / " + (got2 == tiny));
                    lock (sf.eng.Sync) sf.eng.Tracks.Remove(tiny); SetF("spp", sp); SetF("scroll", sc);
                }
                var keepTracks = sf.eng.Tracks.ToList(); var keepState = keepTracks.Select(t => new long[] { t.Offset, t.ClipStart, t.ClipFrames }).ToList(); var keepClips = keepTracks.Select(t => t.Clip).ToList(); var keepHosts = keepTracks.Select(t => t.Host).ToList();   // (the sound checks below add clips: the project is put back as it was afterwards)
                {   // a clip of a sound you own, trimmed, then right-click Add to MY SOUNDS: No keeps both (the edit right after the original as v2), Yes puts the edit in its place
                    string vsrc = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "nf_vers_kick.wav"); var vl = new float[44100]; for (int i = 4410; i < vl.Length; i++) vl[i] = 0.5f * (float)Math.Sin(i * 0.05); WavIO.Write(vsrc, vl, vl, 44100, 24);
                    { var t0w = DateTime.Now; while (!LibLoader.Ready && DateTime.Now - t0w < TimeSpan.FromSeconds(60)) Application.DoEvents(); }   // (the library loads on its own thread; it would replace the list under the test)
                    SoundLibrary.ImportFiles(new[] { vsrc }, null); var up = SoundLibrary.Pack(SoundLibrary.UserPackId); var orig = SoundLibrary.ItemAtPath(System.IO.Path.Combine(up.Dir, "imports", "nf_vers_kick.wav"));
                    Check("a sound in MY SOUNDS is found from its file", orig != null, "");
                    if (orig != null)
                    {
                        var vt = StudioEngine.NewTrack("V", "other", AudioClip.FromArrays(vl, (float[])vl.Clone(), "v")); vt.Source = orig.Path; vt.ClipStart = 4400; vt.ClipFrames = 44100 - 4400; int cnt = up.Items.Count;
                        sf.clipAsk = q => DialogResult.No; sf.AddClipToMySounds(vt); string toastNo = F<string>("toast"); up = SoundLibrary.Pack(SoundLibrary.UserPackId); orig = up.Items.Find(x => x.Id == orig.Id) ?? orig; int oi = up.Items.IndexOf(orig);
                        var v2 = oi >= 0 && oi + 1 < up.Items.Count ? up.Items[oi + 1] : null;
                        Check("No keeps both: the trimmed edit is a new version right after the original, and the original is untouched", v2 != null && v2.Name == "nf_vers_kick v2" && up.Items.Count == cnt + 1 && Math.Abs(v2.Dur - (44100 - 4400) / 44100.0) < 0.01 && Math.Abs(orig.Dur - 1) < 0.01, v2 == null ? "none after it (at " + oi + " of " + up.Items.Count + ", was " + cnt + "): " + toastNo : v2.Name + " " + v2.Dur.ToString("0.000") + " s");
                        sf.clipAsk = q => DialogResult.Yes; sf.AddClipToMySounds(vt); up = SoundLibrary.Pack(SoundLibrary.UserPackId); orig = up.Items.Find(x => x.Id == orig.Id) ?? orig;
                        Check("Yes replaces the original with the edit (same name, same file, no new sound)", up.Items.Count == cnt + 1 && up.Items.Contains(orig) && orig.Name == "nf_vers_kick" && Math.Abs(orig.Dur - (44100 - 4400) / 44100.0) < 0.01, orig.Name + " " + orig.Dur.ToString("0.000") + " s, " + up.Items.Count + " sounds");
                        sf.clipAsk = null; typeof(StudioForm).GetMethod("CloseSounds", BF).Invoke(sf, null);
                    }
                    if (orig != null)
                    {   // TRIM in MY SOUNDS: the part between the handles goes into the song (and to a pad as its own file); the sound file is not changed
                        orig = SoundLibrary.Pack(SoundLibrary.UserPackId).Items.Find(x => x.Id == orig.Id) ?? orig; long flen = new FileInfo(orig.Path).Length; sf.SetSoundTrim(orig, 0.2f, 0.6f); int tc = sf.eng.Tracks.Count;
                        typeof(StudioForm).GetMethod("AddSample", BF).Invoke(sf, new object[] { orig, 0L, sf.eng.Tracks.Count }); var nt = sf.eng.Tracks.Count > tc ? sf.eng.Tracks.Find(x => x.Clip != null && x.Clip.Path == orig.Path) : null; long cl = nt != null ? nt.Clip.Length : 0;
                        Check("a trimmed sound goes into the song as just the part you kept", nt != null && Math.Abs(nt.ClipStart - cl * 0.2) < 50 && Math.Abs(nt.UsedLength - cl * 0.4) < 50, nt == null ? "no track" : nt.ClipStart + " / " + nt.UsedLength + " of " + cl);
                        string tf = (string)typeof(StudioForm).GetMethod("TrimmedFile", BF).Invoke(sf, new object[] { orig, orig.Path }); float[] tl, tr2; int tsr; string tfm; bool rd = SoundIO.Read(tf, out tl, out tr2, out tsr, out tfm);
                        Check("a pad gets the trimmed part as its own file, and the sound file itself is not changed", tf != orig.Path && rd && Math.Abs(tl.Length - cl * 0.4) < 100 && new FileInfo(orig.Path).Length == flen, (rd ? tl.Length.ToString() : "unreadable") + " frames");
                        sf.SetSoundTrim(orig, 0, 1); Check("RESET uses the whole sound again", sf.SoundTrim(orig) == null, ""); if (nt != null) lock (sf.eng.Sync) sf.eng.Tracks.Remove(nt);
                        // duplicates: the same sound under another name is not imported again; a different sound with the same name is kept as (2)
                        string dupe = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "nf_dupe_copy.wav"); File.Copy(orig.Path, dupe, true); int nd = SoundLibrary.ImportFiles(new[] { dupe }, null);
                        Check("importing the same sound again (even with another name) skips it as a duplicate", nd == 0 && SoundLibrary.LastDupes >= 1, nd + " imported, " + SoundLibrary.LastDupes + " duplicates");
                        sf.NoteDupes(); sf.LocateDupe(); var selD = (SoundItem)typeof(StudioForm).GetField("sndSel", BF).GetValue(sf);
                        Check("LOCATE shows the sound the duplicate was a copy of", selD != null && selD.Id == orig.Id, selD == null ? "none" : selD.Name); typeof(StudioForm).GetMethod("CloseSounds", BF).Invoke(sf, null);
                        string other = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sub_nf", "nf_vers_kick.wav"); Directory.CreateDirectory(System.IO.Path.GetDirectoryName(other)); var ol = new float[22050]; for (int i = 0; i < ol.Length; i++) ol[i] = 0.3f * (float)Math.Sin(i * 0.11); WavIO.Write(other, ol, ol, 44100, 24);
                        int no = SoundLibrary.ImportFiles(new[] { other }, null); Check("a different sound with the same name is imported too (as \"(2)\")", no == 1 && SoundLibrary.Pack(SoundLibrary.UserPackId).Items.Exists(x => x.Name == "nf_vers_kick (2)"), no + "");
                        { sf.ShowMySounds(); { var flt = typeof(StudioForm).GetField("sndFilter", BF).GetValue(sf); flt.GetType().GetField("Pack").SetValue(flt, ""); flt.GetType().GetField("Text").SetValue(flt, ""); } typeof(StudioForm).GetMethod("RefreshSounds", BF).Invoke(sf, null); var res = (List<SoundItem>)typeof(StudioForm).GetField("sndResults", BF).GetValue(sf);
                          Check("the sound list has the sounds to step through", res.Count >= 2, res.Count + " in the list"); if (res.Count >= 2) { typeof(StudioForm).GetField("sndSel", BF).SetValue(sf, res[0]); SetF("keysInSounds", true); var pckS = typeof(StudioForm).GetMethod("ProcessCmdKey", BF | BindingFlags.DeclaredOnly); var msgS = new Message(); pckS.Invoke(sf, new object[] { msgS, Keys.Down }); var s1 = (SoundItem)typeof(StudioForm).GetField("sndSel", BF).GetValue(sf);
                            Check("in the sound list, Down picks the next sound (the tracks do not scroll)", s1 == res[1], s1 == null ? "none" : s1.Name); SetF("keysInSounds", false); Auditioner.Stop(); } }
                    }
                    { var rm = Win.NewReadableMenu(); var it = rm.Items.Add("Save this clip as a WAV on my PC..."); Check("the right-click menu of a track keeps clear writing (not all capitals) in a bigger font", it.Text == "Save this clip as a WAV on my PC..." && rm.Font.Size >= 11, it.Text + " / " + rm.Font.Size); rm.Dispose(); }
                    int lead = SamplerFx.LeadOf(vl, vl, 44100);
                    Check("a pad skips the silence at the front of a sound, so a hit is not late", lead > 4300 && lead <= 4410, lead + " frames skipped");
                    lock (sf.eng.Sync) { sf.eng.Tracks.Clear(); sf.eng.Tracks.AddRange(keepTracks); for (int k = 0; k < keepTracks.Count; k++) { keepTracks[k].Offset = keepState[k][0]; keepTracks[k].ClipStart = keepState[k][1]; keepTracks[k].ClipFrames = keepState[k][2]; keepTracks[k].Clip = keepClips[k]; keepTracks[k].Host = keepHosts[k]; } }
                    typeof(StudioForm).GetMethod("CloseSounds", BF).Invoke(sf, null); paintS();
                }
                {   // LOCK lists the tracks on the left only (a piece sharing a row is not a track of its own), numbered the same
                    var lp = StudioEngine.NewTrack("Clip row (2)", "vocal", AudioClip.FromArrays(new float[44100], new float[44100], "t")); lp.Host = ra; lp.Offset = 44100 * 3; lock (sf.eng.Sync) sf.eng.Tracks.Add(lp);
                    sf.OpenTrackPicker(0); paintS(); var rgsL = (List<HitReg>)typeof(StudioForm).GetField("regs", BF).GetValue(sf); int entries = rgsL.FindAll(r => Math.Abs(r.R.Height - 40) < 0.5f && r.R.Width > 300 && r.OnDown != null).Count;
                    Check("LOCK lists the 2 tracks on the left, not the piece that shares a row", entries == 2, entries + " entries");
                    SetF("tpOpen", false);
                    // right-click a piece: lock just that section
                    var isL = typeof(StudioForm).GetMethod("IsLocked", BF); sf.TogglePieceLock(lp);
                    Check("Lock this section locks only that piece (the rest of the track stays editable)", (bool)isL.Invoke(sf, new object[] { lp }) && !(bool)isL.Invoke(sf, new object[] { ra }) && !ra.Locked, "");
                    SetF("toast", ""); typeof(StudioForm).GetMethod("SplitTrack", BF).Invoke(sf, new object[] { lp, lp.Offset + 10000 });
                    Check("a locked section says so when you try to change it, and how to unlock it (right-click)", F<string>("toast").Contains("section is locked") && F<string>("toast").Contains("right-click"), F<string>("toast"));
                    string pfL = Path.Combine(Path.GetTempPath(), "nf-piecelock.ytmproj"); ra.Source = "x"; lp.Source = "x"; ProjectIO.Save(sf.eng, pfL); var eL = new StudioEngine(); ProjectIO.Load(eL, pfL, s => { }); var lpL = eL.Tracks.Find(t => t.Name == "Clip row (2)"); var raL = eL.Tracks.Find(t => t.Name == "Clip row");
                    Check("a locked section stays locked when the project is saved and opened again", lpL != null && lpL.PieceLocked && raL != null && !raL.PieceLocked && !raL.Locked, lpL == null ? "missing" : lpL.PieceLocked.ToString()); try { File.Delete(pfL); } catch { }
                    sf.TogglePieceLock(lp); Check("Unlock this section frees it again", !(bool)isL.Invoke(sf, new object[] { lp }), "");
                    {   // several clips picked: "Lock these sections" locks every one of them, one Ctrl+Z puts them all back
                        var msl = (List<Track>)typeof(StudioForm).GetField("multiSel", BF).GetValue(sf); msl.Clear(); msl.Add(ra); msl.Add(lp); ra.PieceLocked = false; lp.PieceLocked = false;
                        bool grp = (bool)typeof(StudioForm).GetMethod("InGroup", BF).Invoke(sf, new object[] { lp }); sf.SetPieceLocks(new List<Track>(msl), true);
                        Check("with several clips picked, Lock these sections locks every one of them", grp && ra.PieceLocked && lp.PieceLocked && (bool)isL.Invoke(sf, new object[] { ra }) && (bool)isL.Invoke(sf, new object[] { lp }), "");
                        typeof(StudioForm).GetMethod("DoUndo", BF, null, Type.EmptyTypes, null).Invoke(sf, null); Check("one Ctrl+Z unlocks them all again", !ra.PieceLocked && !lp.PieceLocked, ""); msl.Clear();
                    }
                    { var mm = Win.NewMenu(); mm.Items.Add("Split here"); Check("menus are compact (10 pt, 25% smaller than before) with each option in its own box", Math.Abs(mm.Font.SizeInPoints - 10f) < 0.3f && mm.Items[0].Padding.Top == 3, mm.Font.SizeInPoints + " pt"); mm.Dispose(); }
                    { var ppd = (Dictionary<Track, int>)typeof(StudioForm).GetField("powerPulse", BF).GetValue(sf); typeof(StudioForm).GetMethod("ToggleOff", BF).Invoke(sf, new object[] { rb }); bool pulsed = ppd.ContainsKey(rb) && rb.Off; typeof(StudioForm).GetMethod("ToggleOff", BF).Invoke(sf, new object[] { rb }); Check("pressing the power button turns the track off and sends pulse rings out of it (and on again)", pulsed && !rb.Off, ""); }
                    lock (sf.eng.Sync) sf.eng.Tracks.Remove(lp);
                }
                {   // LOOP > Loop a section: drag across the timeline, it loops that part (and plays it straight away)
                    int ov = Prefs.OutVol; Prefs.OutVol = 0; SetF("scroll", 0L); SetF("spp", 100.0); sf.LoopPickStart(); paintS();
                    float y0 = top + laneH / 2; var pr = (HitReg)typeof(StudioForm).GetMethod("TopRegion", BF).Invoke(sf, new object[] { new PointF(388 + 100, y0) });
                    SetF("MousePos", new PointF(388 + 100, y0)); pr.OnDown(); pr.OnDrag(new PointF(388 + 400, y0)); pr.OnUp();
                    bool snapOff = !F<bool>("snapOn"); long wa = 100 * 100, wb = 400 * 100;
                    Check("LOOP a section: dragging across the tracks sets the section and it plays round from its start", sf.eng.LoopSection && !sf.loopPick && (snapOff ? sf.eng.LoopA == wa && sf.eng.LoopB == wb : sf.eng.LoopA <= wa && sf.eng.LoopB >= wb) && sf.eng.Playing, sf.eng.LoopA + " to " + sf.eng.LoopB + " playing " + sf.eng.Playing);
                    sf.eng.Pause(); sf.LoopWhole(); Check("LOOP the whole song clears the section", sf.eng.Loop && !sf.eng.LoopSection, "");
                    sf.LoopOff(); Check("turning the loop off stops it looping", !sf.eng.Loop, ""); Prefs.OutVol = ov; sf.eng.Pos = 0;
                }
                {   // a finished song from the community plays as it is: no mastering chain over it (it squashed every published song a second time)
                    var loud = new float[44100 * 3]; for (int k = 0; k < loud.Length; k++) loud[k] = (float)(0.9 * Math.Sin(k * 0.06)); var sp = sf.SongPlayer(AudioClip.FromArrays(loud, (float[])loud.Clone(), "s"), "Song");
                    var bl = new float[4096]; var br2 = new float[4096]; sp.Playing = true; float pk = 0; for (int q = 0; q < 20; q++) { sp.RenderRealtime(bl, br2, 4096); if (q > 4) foreach (var v in bl) pk = Math.Max(pk, Math.Abs(v)); } sp.Playing = false;
                    Check("a community song plays exactly as it was published (no second mastering squashing it)", sp.MasterFx.TrueForAll(m => m.Bypass) && Math.Abs(pk - 0.9f) < 0.05f, "peak " + pk.ToString("0.000") + " of 0.900"); sp.Shutdown();
                }
                {   // the whole picture is in the video, never cropped: a black border inside it is trimmed off; a square video is filled edge to edge, wide and tall ones keep every corner of the picture
                    byte[] keepCover = ProjectIO.CoverBytes; string keepPath = F<string>("projPath");
                    using (var pic = new Bitmap(600, 600)) { using (var pg = Graphics.FromImage(pic)) { pg.Clear(Color.Black); using (var gb = new SolidBrush(Color.FromArgb(255, 150, 60, 200))) pg.FillRectangle(gb, 100, 100, 400, 400); using (var yb = new SolidBrush(Color.Yellow)) foreach (var c0 in new[] { new Point(100, 100), new Point(460, 100), new Point(100, 460), new Point(460, 460) }) pg.FillRectangle(yb, c0.X, c0.Y, 40, 40); } using (var ms = new MemoryStream()) { pic.Save(ms, System.Drawing.Imaging.ImageFormat.Png); ProjectIO.CoverBytes = ms.ToArray(); } }
                    SetF("projPath", ""); string why = ""; Func<Color, bool> yellow = px => px.R > 200 && px.G > 200 && px.B < 80;
                    string fr = sf.MakeVideoFrame(1080, 1080); using (var vb = new Bitmap(fr)) foreach (var pt in new[] { new Point(4, 4), new Point(1075, 4), new Point(4, 1075), new Point(1075, 1075) }) if (!yellow(vb.GetPixel(pt.X, pt.Y))) why += "square corner " + pt + " is not the picture; "; try { File.Delete(fr); } catch { }
                    fr = sf.MakeVideoFrame(1920, 1080); using (var vb = new Bitmap(fr)) foreach (var pt in new[] { new Point(430, 10), new Point(1490, 10), new Point(430, 1070), new Point(1490, 1070) }) if (!yellow(vb.GetPixel(pt.X, pt.Y))) why += "wide: picture corner " + pt + " cut off; "; try { File.Delete(fr); } catch { }
                    fr = sf.MakeVideoFrame(1080, 1920); using (var vb = new Bitmap(fr)) foreach (var pt in new[] { new Point(10, 430), new Point(1070, 430), new Point(10, 1490), new Point(1070, 1490) }) if (!yellow(vb.GetPixel(pt.X, pt.Y))) why += "tall: picture corner " + pt + " cut off; "; try { File.Delete(fr); } catch { }
                    Check("the whole picture is in the video, never cropped (its black border trimmed off): it fills a square video edge to edge", why.Length == 0, why); ProjectIO.CoverBytes = keepCover; SetF("projPath", keepPath);
                }
                {   // EXPORT does not lock the program: its window is open and everything else can still be clicked
                    var et = StudioEngine.NewTrack("Exp", "beat", AudioClip.FromArrays(new float[44100], new float[44100], "e")); lock (sf.eng.Sync) sf.eng.Tracks.Add(et);
                    typeof(StudioForm).GetMethod("ExportClick", BF).Invoke(sf, null); Pump(300); var ew = F<Form>("exportWin");
                    Check("the EXPORT window does not lock the program (the tabs and tracks stay clickable while it is open)", ew != null && ew.Visible && !ew.Modal && sf.Enabled, ew == null ? "no window" : "modal " + ew.Modal);
                    if (ew != null) ew.Close(); Pump(150); Check("closing it without exporting does nothing", F<Form>("exportWin") == null, ""); lock (sf.eng.Sync) sf.eng.Tracks.Remove(et);
                }
                {   // EXPORT as MP4: the picture of the project for exactly as long as the song
                    string ffx = StudioEnv.Ffmpeg(); if (ffx == null) Console.WriteLine("SKIP  MP4 export (ffmpeg is not installed here)"); else
                    {
                        var em = new StudioEngine(); var vt = StudioEngine.NewTrack("Tone", "beat", AudioClip.FromArrays(Enumerable.Range(0, 44100 * 5).Select(k => (float)(0.3 * Math.Sin(k * 0.05))).ToArray(), Enumerable.Range(0, 44100 * 5).Select(k => (float)(0.3 * Math.Sin(k * 0.05))).ToArray(), "t")); em.Tracks.Add(vt);
                        string vdir = Path.Combine(Path.GetTempPath(), "nf_mp4_" + Guid.NewGuid().ToString("N").Substring(0, 6)); Exporter.VideoFrame = sf.MakeVideoFrame(1920, 1080); Exporter.SoundRate = 128; var plan = Exporter.Plan(em, true, false, false, false, false, false, false, "Clip", Exporter.Mp4);
                        string outv = null, verr = ""; try { outv = Exporter.Write(em, plan[0], vdir, Exporter.Mp4, null, null, Exporter.RateFor(Exporter.Mp4, 0)); } catch (Exception ex) { verr = ex.Message; }
                        int vc; string info = outv != null ? MasterDsp.RunCapture(ffx, "-hide_banner -i \"" + outv + "\"", null, out vc) : ""; var dm = System.Text.RegularExpressions.Regex.Match(info, @"Duration: ([0-9]+):([0-9]+):([0-9.]+)"); double dur = dm.Success ? int.Parse(dm.Groups[1].Value) * 3600 + int.Parse(dm.Groups[2].Value) * 60 + double.Parse(dm.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture) : -1;
                        long vsz = outv != null && File.Exists(outv) ? new FileInfo(outv).Length : 0; Check("EXPORT as MP4 makes a video (the project picture) with the song, as long as the song, at the size you picked (1920 x 1080), and small (" + vsz / 1024 + " KB for 7 s)", outv != null && outv.EndsWith(".mp4") && info.Contains("1920x1080") && vsz < 600 * 1024 && info.Contains("Video:") && info.Contains("Audio: aac") && Math.Abs(dur - 7.0) < 0.6, verr + " dur " + dur + " " + info.Replace((char)13, ' ').Replace((char)10, '|').Substring(0, Math.Min(900, info.Length)));
                        try { File.Delete(Exporter.VideoFrame); Directory.Delete(vdir, true); } catch { }
                    }
                }
                sf.eng.Tracks.Clear();
            }
            {   // USE on a mastering preset: it is marked IN USE (and REVERT takes the mark back off)
                sf.eng.Tracks.Clear(); sf.masterPresetUsed = -1; typeof(StudioForm).GetMethod("PresetUse", BF).Invoke(sf, new object[] { 2 });
                Check("USE on a mastering preset marks it IN USE (a border and a tag)", sf.masterPresetUsed == 2, sf.masterPresetUsed.ToString()); var rv = F<Action>("revertAction"); if (rv != null) rv(); Check("REVERT takes the IN USE mark back off", sf.masterPresetUsed == -1, sf.masterPresetUsed.ToString()); SetF("revertAction", null);
                {   // the Mastering page shows the preset that is on the master: (edited) once the chain is changed, and it is kept in the project
                    typeof(StudioForm).GetMethod("PresetUse", BF).Invoke(sf, new object[] { 3 }); bool fresh = !ProjectIO.MasterPresetEdited(sf.eng);
                    var mf = sf.eng.MasterFx[0]; float mpWas = mf.Get(mf.P[0].Id); mf.Set(mf.P[0].Id, mf.P[0].Max); bool edited = ProjectIO.MasterPresetEdited(sf.eng);
                    string mixTxt = ProjectIO.SerializeMix(sf.eng); sf.masterPresetUsed = -1; ProjectIO.RestoreMix(sf.eng, mixTxt);
                    Check("the Mastering page shows the preset in use, marks it EDITED after a change, and the project keeps both", fresh && edited && sf.masterPresetUsed == 3 && ProjectIO.MasterPresetEdited(sf.eng) && mixTxt.Contains("mpreset|" + MasterPresets.All[3].Name), sf.masterPresetUsed + " " + fresh + " " + edited);
                    ProjectIO.RestoreMix(sf.eng, mixTxt.Replace("mpreset|", "xx|")); Check("a project without a preset shows none", sf.masterPresetUsed == -1, sf.masterPresetUsed.ToString()); mf = sf.eng.MasterFx[0]; mf.Set(mf.P[0].Id, mpWas);
                {   // VOCAL PRESETS: over a thousand, every one valid; artist presets in brackets; a preset with a vocal bus makes it once and routes the track into it; the AI PRESET GENERATOR
                    int badV = 0; foreach (var vs in VocalStyles.All) if (VocalStyles.Check(vs).Count > 0) badV++; int nArt = VocalStyles.All.Select(VocalStyles.ArtistOf).Where(a2 => a2.Length > 0).Distinct().Count();
                    Check("VOCAL PRESETS has over 1,000 presets, every plugin and setting in them is real, and dozens of artists are in brackets", VocalStyles.All.Count >= 1000 && badV == 0 && nArt >= 80 && VocalStyles.Find("juice wrld", "", false).Count >= 10, VocalStyles.All.Count + " presets, " + badV + " bad, " + nArt + " artists");
                    var keepV = new List<Track>(sf.eng.Tracks); sf.eng.Tracks.Clear(); var v1 = StudioEngine.NewTrack("Lead", "vocal", null); var v2 = StudioEngine.NewTrack("Adlibs", "vocal", null); sf.eng.Tracks.Add(v1); sf.eng.Tracks.Add(v2);
                    var bst = VocalStyles.All.First(s9 => s9.Name == "Emo Melodic Sing (Juice WRLD style)"); SetF("selTrack", 0); typeof(StudioForm).GetMethod("ApplyStyle", BF).Invoke(sf, new object[] { bst, true }); SetF("selTrack", sf.eng.Tracks.IndexOf(v2)); typeof(StudioForm).GetMethod("ApplyStyle", BF).Invoke(sf, new object[] { VocalStyles.All.First(s9 => s9.Name == bst.Name + " - Ad-Libs"), true });
                    var buses = sf.eng.Tracks.FindAll(t9 => t9.IsBus && t9.Name == bst.BusName); bool busOk = buses.Count == 1 && v1.Out == buses[0] && v2.Out == buses[0] && buses[0].Fx.Count >= 3 && v1.Fx.Count >= 8;
                    Check("a +BUS preset makes its vocal bus once, and the lead and its ad-libs both go into it", busOk, buses.Count + " buses, lead out " + (v1.Out == null ? "master" : v1.Out.Name) + ", adlibs out " + (v2.Out == null ? "master" : v2.Out.Name));
                    sf.aiPrompt = "Juice WRLD with a stutter delay and lots of reverb"; sf.AiGenerate(); var ar = sf.aiResult; bool aiOk = ar != null && ar.Base.Contains("Juice WRLD") && ar.Chain.Contains("Stutter Edit") && ar.Chain.Contains("delay{") && ar.Understood.Exists(u9 => u9.Contains("Lots of reverb")) && VocalStyles.CheckChain("ai", ar.Chain).Count == 0;
                    var dry = PresetPrompt.Make("eminem dry and loud, no autotune"); bool dryOk = dry.Base.Contains("Eminem") && !dry.Chain.Contains("@Hard Tune") && dry.Chain.Contains("limiter{") && dry.Understood.Exists(u9 => u9.StartsWith("Dry"));
                    SetF("selTrack", 0); int fxBefore = v1.Fx.Count; sf.AiUse(); bool used = v1.Fx.Count > fxBefore; sf.aiSaveAskHook = n9 => "Test AI " + Guid.NewGuid().ToString("N").Substring(0, 5); bool saved = sf.AiSave(); sf.aiSaveAskHook = null;
                    Check("the AI PRESET GENERATOR turns words into plugins: the artist, a stutter echo, lots of reverb; dry, loud and no autotune; USE puts it on the track, SAVE keeps it", aiOk && dryOk && used && saved, aiOk + " " + dryOk + " " + used + " " + saved);
                    sf.eng.Tracks.Clear(); sf.eng.Tracks.AddRange(keepV);
                }
                {   // BUS PRESETS and the AI BUS GENERATOR; a bus preset goes on the bus a track goes into or makes one; the EFFECT LIBRARY; Censor this section
                    int badB = 0; foreach (var bs9 in BusPresets.All) if (VocalStyles.CheckChain(bs9.Name, bs9.Chain).Count > 0) badB++;
                    Check("BUS PRESETS has over 1,000 presets in 12 kinds of bus and 15 genres, every plugin and setting in them is real", BusPresets.All.Count >= 1000 && badB == 0 && BusPresets.All.Select(b9 => b9.Genre).Distinct().Count() == 12 && BusPresets.All.Select(b9 => b9.Name).Distinct().Count() == BusPresets.All.Count, BusPresets.All.Count + " presets, " + badB + " bad");
                    var keepB = new List<Track>(sf.eng.Tracks); sf.eng.Tracks.Clear(); var d1 = StudioEngine.NewTrack("Drums", "beat", null); sf.eng.Tracks.Add(d1); SetF("selTrack", 0); SetF("busEdit", null);
                    sf.ApplyBusPreset(BusPresets.All.First(b9 => b9.Name == "Drum Bus: Drill (Punchy)")); var db = sf.eng.Tracks.Find(t9 => t9.IsBus && t9.Name == "Drum Bus"); bool routed = db != null && d1.Out == db && db.Fx.Count >= 4;
                    int nb0 = sf.eng.Tracks.Count(t9 => t9.IsBus); SetF("selTrack", sf.eng.Tracks.IndexOf(d1)); sf.ApplyBusPreset(BusPresets.All.First(b9 => b9.Name == "Glue Bus: Trap (Clean)")); bool same = sf.eng.Tracks.Count(t9 => t9.IsBus) == nb0 && db != null && db.Fx.Count >= 8;
                    Check("a bus preset makes a bus of its kind and routes the track into it; the next one goes on the bus that track already goes into", routed && same, (db == null ? "no bus" : db.Fx.Count + " plugins") + ", out " + (d1.Out == null ? "master" : d1.Out.Name));
                    sf.OpenAiBus(); sf.aiPrompt = "drill drum bus, punchy and dark with more glue"; sf.AiGenerate(); var br9 = sf.aiResult; bool kindOk = br9 != null && br9.BusName == "Drum Bus" && br9.Base == "Drum Bus: Drill (Punchy)" && br9.Understood.Exists(u9 => u9.Contains("glue")) && VocalStyles.CheckChain("aibus", br9.Chain).Count == 0;
                    var ar9 = BusPrompt.Make("Juice WRLD vocal bus with more glue, dry"); bool artOk = ar9.BusName == "Vocal Bus" && ar9.Understood.Exists(u9 => u9.StartsWith("Artist: Juice WRLD")) && !ar9.Chain.Contains("reverb{");
                    var mx9 = BusPrompt.Make("clean mix bus for pop, loud"); bool mixOk = mx9.Base == "Mix Bus: Pop (Clean)" && mx9.Chain.Contains("gain=6");
                    int busFx0 = db == null ? 0 : db.Fx.Count; SetF("selTrack", sf.eng.Tracks.IndexOf(d1)); sf.AiUse(); bool aiUsed = db != null && db.Fx.Count > busFx0 && !sf.aiOpen;
                    Check("the AI BUS GENERATOR reads the kind of bus, the genre, the flavour and changes (and an artist's genre); USE ON BUS puts it on the bus", kindOk && artOk && mixOk && aiUsed, kindOk + " " + artOk + " " + mixOk + " " + aiUsed + " " + (br9 == null ? "" : br9.Base));
                    var ids9 = new HashSet<string>(); bool uniq = PluginLibrary.All.TrueForAll(v9 => ids9.Add(v9.Id)); int nGen = PluginLibrary.GeneratedCount;
                    int listedLib = PluginLibrary.All.Count(PluginLibrary.IsListed); bool noGenListed = !FxRegistry.All.Any(i9 => PluginLibrary.IsGenerated(i9.Type)); string[] mustList = { "Room Reverb", "Hall Reverb", "Plate Reverb", "Spring Reverb", "Shimmer Reverb", "Gated Reverb", "Reverse Reverb", "Cathedral Reverb", "Ping-Pong Delay", "Tape Echo", "Censor", "Croaky Demon Voice", "Vibrato" };
                    bool typesListed = mustList.All(n9 => FxRegistry.All.Any(i9 => i9.Name == n9)); bool dupGone = !FxRegistry.All.Any(i9 => i9.Name == "Dotted-Eighth Delay" || i9.Name == "Hard Tune" || i9.Name == "Censor (Mute)" || i9.Name.StartsWith("Hall Reverb: "));
                    bool oldLoads = FxRegistry.Create(PluginLibrary.IdFor("Dotted-Eighth Delay")) != null && FxRegistry.Create(PluginLibrary.IdFor("Hall Reverb: Large, Dark, Shimmer, Wet")) != null && uniq;
                    Check("ADD EFFECT lists one plugin of each type (versions that were only other settings are gone from the list), and a project that used one of those still opens with it", noGenListed && typesListed && dupGone && oldLoads && listedLib < 150, listedLib + " listed library plugins, " + noGenListed + " " + typesListed + " " + dupGone + " " + oldLoads);
                                                            // Censor this section: the censor plugin is added once (switched off) and switched on only over the section
                    int nS9 = 44100 * 3; var cl9 = new float[nS9]; for (int q = 0; q < nS9; q++) cl9[q] = (float)(0.3 * Math.Sin(q * 2 * Math.PI * 220 / 44100.0));
                    var vt9 = StudioEngine.NewTrack("Clean edit", "vocal", AudioClip.FromArrays(cl9, (float[])cl9.Clone(), "c")); vt9.Offset = 44100; sf.eng.Tracks.Clear(); sf.eng.Tracks.Add(vt9);
                    sf.CensorSections(new List<Track> { vt9 }, 1); var cf9 = vt9.Fx.Find(f9 => f9 is CensorFx); var ln9 = vt9.Auto.Find(l9 => l9.Fx == cf9 && l9.Param == "censor");
                    bool laneOk = cf9 != null && cf9.Get("censor") < 0.5f && (int)cf9.Get("mode") == 1 && ln9 != null && ln9.ValueAt(44100 + 1000, true) > 0.5f && ln9.ValueAt(44100 - 100, true) < 0.5f && ln9.ValueAt(44100 + nS9 + 10, true) < 0.5f;
                    sf.CensorSections(new List<Track> { vt9 }, 0); bool once = vt9.Fx.Count(f9 => f9 is CensorFx) == 1 && (int)cf9.Get("mode") == 0;
                    float[] L9, R9; sf.eng.Render(0, 44100 * 5, new RenderOpts { Master = false }, null, out L9, out R9); double in9 = 0, zc = 0; for (int q = 44100 + 22050; q < 44100 + 22050 + 44100; q++) { in9 += L9[q] * L9[q]; if (L9[q - 1] <= 0 && L9[q] > 0) zc++; }
                    bool bleeped = Math.Sqrt(in9 / 44100) > 0.05 && zc > 800 && zc < 1200;   // the 1 kHz bleep, not the 220 Hz voice
                    sf.UncensorSections(new List<Track> { vt9 }); bool un = ln9 == null || ln9.ValueAt(44100 + 1000, true) < 0.5f;
                    Check("Censor this section adds one Censor plugin (switched off) and switches it on only over the section; the render is bleeped there; Uncensor takes it off", laneOk && once && bleeped && un, laneOk + " " + once + " " + bleeped + " (" + zc + " crossings) " + un);
                    var dm9 = FxRegistry.Create(PluginLibrary.IdFor("Croaky Demon Voice")); var gw9 = FxRegistry.Create(PluginLibrary.IdFor("Satan Voice")); Check("the demon voices are there (Croaky Demon Voice, Satan Voice: pitch and formant down, the Growl croak, a dark reverb)", dm9 != null && gw9 != null && dm9.Find("s2.rate") != null && dm9.Find("s1.p1") != null, dm9 == null ? "missing" : dm9.P.Count + " settings");
                    // the AI EFFECT GENERATOR, and REGENERATE / RANDOMISE on every generator
                    var e1 = EffectPrompt.Make("huge dark cathedral reverb with a shimmer"); var e2 = EffectPrompt.Make("hard autotune in F# minor"); var e3 = EffectPrompt.Make("1/8 dotted ping pong delay, tape, ducked");
                    var e2f = VocalStyles.BuildChain(e2.Chain); bool efxOk = VocalStyles.CheckChain("e1", e1.Chain).Count == 0 && e1.Chain.Contains("mode=5") && e1.Chain.Contains("shim=") && e2f.Count == 1 && (int)e2f[0].Get("key") == 6 && (int)e2f[0].Get("scale") == 2 && !e2f[0].AutoKeyPending && e3.Chain.Contains("mode=2;sync=8") && e3.Chain.Contains("duck=60");
                    sf.eng.Tracks.Clear(); var ef9 = StudioEngine.NewTrack("FX me", "vocal", null); sf.eng.Tracks.Add(ef9); SetF("selTrack", 0); sf.OpenAiEffect(false); sf.aiPrompt = "telephone with a bit of distortion"; sf.AiGenerate(); int fx0 = ef9.Fx.Count; sf.AiUse(); bool efxUsed = ef9.Fx.Count == fx0 + 2 && !sf.aiOpen;
                    Check("the AI EFFECT GENERATOR turns words into the effect with its settings (a huge dark shimmer cathedral, hard autotune kept in F# minor, a ducked 1/8 dotted ping-pong) and adds it to YOUR PLUGINS", efxOk && efxUsed, efxOk + " " + efxUsed + " " + ef9.Fx.Count);
                    sf.OpenAiPreset(); sf.aiPrompt = "Juice WRLD with lots of reverb"; sf.AiGenerate(); string c0 = sf.aiResult.Chain; sf.AiRegenerate(4242); string c1 = sf.aiResult.Chain; bool regenV = c1 != c0 && VocalStyles.CheckChain("rv", c1).Count == 0 && sf.aiPrompt == "Juice WRLD with lots of reverb";
                    sf.OpenAiBus(); sf.aiPrompt = "drum bus"; sf.AiGenerate(); string b0 = sf.aiResult.Base; sf.AiRegenerate(77); bool regenB = sf.aiResult.Chain != "" && VocalStyles.CheckChain("rb", sf.aiResult.Chain).Count == 0 && sf.aiResult.BusName == "Drum Bus";
                    sf.OpenAiEffect(false); sf.aiPrompt = ""; sf.AiRandomise(31337); bool randE = sf.aiPrompt.Length > 0 && sf.aiResult != null && VocalStyles.CheckChain("re", sf.aiResult.Chain).Count == 0; sf.OpenAiBus(); sf.AiRandomise(5); bool randB = sf.aiPrompt.Contains("bus") && VocalStyles.CheckChain("rb2", sf.aiResult.Chain).Count == 0; sf.OpenAiPreset(); sf.AiRandomise(9); bool randV = sf.aiPrompt.Length > 0 && VocalStyles.CheckChain("rv2", sf.aiResult.Chain).Count == 0;
                    Check("REGENERATE gives another take on the same words (vocal, bus) and RANDOMISE makes a surprise (effect, bus, vocal), all valid", regenV && regenB && randE && randB && randV, regenV + " " + regenB + " " + randE + " " + randB + " " + randV);
                    sf.aiOpen = false;
                    sf.eng.Tracks.Clear(); sf.eng.Tracks.AddRange(keepB); SetF("selTrack", 0);
                }
                {   // ALL BUSES next to ALL PLUGINS opens the sheet on its buses view
                    SetF("tab", 0); var keepAB = sf.eng.Tracks.ToList(); sf.eng.Tracks.Clear(); sf.eng.Tracks.Add(StudioEngine.NewTrack("Vox", "vocal", null)); SetF("selTrack", 0); SetF("busEdit", null); Prefs.PanelHidden = false; SetF("panelAt", DateTime.MinValue);
                    using (var bmA = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height)) using (var gA = Graphics.FromImage(bmA)) typeof(StudioForm).GetMethod("PaintAll", BF).Invoke(sf, new object[] { new PaintEventArgs(gA, new Rectangle(Point.Empty, sf.ClientSize)) });
                    typeof(StudioForm).GetMethod("OpenAllBuses", BF).Invoke(sf, null); bool okA = (bool)typeof(StudioForm).GetField("allFxOpen", BF).GetValue(sf) && (bool)typeof(StudioForm).GetField("allBusMode", BF).GetValue(sf);
                    Check("ALL BUSES (next to ALL PLUGINS) opens the sheet on its buses view", okA, ""); SetF("allFxOpen", false); SetF("allBusMode", false); sf.eng.Tracks.Clear(); sf.eng.Tracks.AddRange(keepAB);
                }
                {   // the track list folds away to a thin strip with an arrow (more room to edit), comes back, and is remembered; THIN TRACKS is remembered too
                    SetF("tab", 0); var keepH = sf.eng.Tracks.ToList(); sf.eng.Tracks.Clear(); sf.eng.Tracks.Add(StudioEngine.NewTrack("Fold me", "vocal", null)); var trmH = typeof(StudioForm).GetMethod("TimelineRect", BF); float hw0 = ((RectangleF)trmH.Invoke(sf, null)).X;
                    sf.ToggleHead(); SetF("headAt", DateTime.MinValue); float hw1 = ((RectangleF)trmH.Invoke(sf, null)).X; bool savedH = Prefs.Lines().Any(l9 => l9 == "headhid=1");
                    bool miniOk = false, arrowOk = false; using (var bmH = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height)) using (var gH = Graphics.FromImage(bmH)) { typeof(StudioForm).GetMethod("PaintAll", BF).Invoke(sf, new object[] { new PaintEventArgs(gH, new Rectangle(0, 0, bmH.Width, bmH.Height)) }); bmH.Save(@"C:\Users\thoma\nfout\head-hidden.png"); }
                    var regsH = (System.Collections.IList)typeof(StudioForm).GetField("regs", BF).GetValue(sf); foreach (var rg in regsH) { var rr = (RectangleF)rg.GetType().GetField("R").GetValue(rg); if (Math.Abs(rr.Width - StudioForm.HeadTabW) < 1 && rr.X == 0) miniOk = true; if (Math.Abs(rr.Width - (StudioForm.HeadTabW - 8)) < 1 && Math.Abs(rr.X - 4) < 1 && rr.Height < 30) arrowOk = true; }
                    sf.ToggleHead(); SetF("headAt", DateTime.MinValue); float hw2 = ((RectangleF)trmH.Invoke(sf, null)).X;
                    Check("the track list tucks away to a thin strip (the timeline gets wider), its arrow brings it back, and it is remembered", hw1 < hw0 - 300 && Math.Abs(hw2 - hw0) < 1 && savedH && miniOk && arrowOk, hw0 + " -> " + hw1 + " -> " + hw2 + " saved " + savedH + " strip " + miniOk + " arrow " + arrowOk);
                    typeof(StudioForm).GetMethod("ToggleThinTracks", BF).Invoke(sf, null); bool thinSaved = Prefs.ThinTracks && Prefs.Lines().Any(l9 => l9 == "thin=1"); typeof(StudioForm).GetMethod("ToggleThinTracks", BF).Invoke(sf, null);
                    sf.eng.Tracks.Clear(); sf.eng.Tracks.AddRange(keepH);
                    Check("THIN TRACKS is a saved setting", thinSaved && !Prefs.ThinTracks && Prefs.Lines().Any(l9 => l9 == "thin=0"), "saved " + thinSaved);
                    Prefs.Changed();
                }
                {   // the plugin panel tucks away into a thin tab (a bigger timeline) and comes back; it is remembered; adding an effect brings it out
                    SetF("tab", 0); bool keepHid = Prefs.PanelHidden; Prefs.PanelHidden = false; SetF("panelAt", DateTime.MinValue); var trm = typeof(StudioForm).GetMethod("TimelineRect", BF); float w0 = ((RectangleF)trm.Invoke(sf, null)).Width;
                    sf.TogglePanel(); SetF("panelAt", DateTime.MinValue); float w1 = ((RectangleF)trm.Invoke(sf, null)).Width; bool saved = Prefs.Lines().Any(l9 => l9 == "panelhid=1");
                    bool tabOk = false; using (var bmP = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height)) using (var gP = Graphics.FromImage(bmP)) { typeof(StudioForm).GetMethod("PaintAll", BF).Invoke(sf, new object[] { new PaintEventArgs(gP, new Rectangle(Point.Empty, sf.ClientSize)) }); }
                    var regsP = (System.Collections.IList)typeof(StudioForm).GetField("regs", BF).GetValue(sf); foreach (var rg in regsP) { var rr = (RectangleF)rg.GetType().GetField("R").GetValue(rg); if (Math.Abs(rr.Width - StudioForm.PanelTabW) < 1 && rr.X >= sf.ClientSize.Width - StudioForm.PanelTabW - 1) tabOk = true; }
                    sf.TogglePanel(); SetF("panelAt", DateTime.MinValue); float w2 = ((RectangleF)trm.Invoke(sf, null)).Width;
                    Check("the plugin panel tucks away into a thin tab (the timeline gets wider), the tab brings it back, and it is remembered", w1 > w0 + 300 && Math.Abs(w2 - w0) < 1 && saved && tabOk, w0 + " -> " + w1 + " -> " + w2 + " saved " + saved + " tab " + tabOk);
                    Prefs.PanelHidden = keepHid; Prefs.Changed();
                }
                {   // the LYRICS page: the mouse wheel over the logo scroll bar (and around the box) scrolls the lyrics; the arrows and the bar still work
                    SetF("tab", 2); SetF("welcomeOn", false); Action pnt = () => { Application.DoEvents(); using (var bmL = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height)) using (var gL = Graphics.FromImage(bmL)) typeof(StudioForm).GetMethod("PaintAll", BF).Invoke(sf, new object[] { new PaintEventArgs(gL, new Rectangle(Point.Empty, sf.ClientSize)) }); Application.DoEvents(); };
                    pnt(); var lbox = F<TextBoxBase>("lyricBox"); string keepLy = lbox.Text; var sbL = new System.Text.StringBuilder(); for (int q = 1; q <= 200; q++) sbL.Append("Line " + q + " of the lyrics\r\n"); lbox.Text = sbL.ToString(); lbox.SelectionStart = 0; lbox.ScrollToCaret(); pnt(); pnt();
                    Func<int> firstL = () => (int)typeof(StudioForm).GetMethod("SendMessage", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { lbox.Handle, 0xCE, 0, 0 }); var wm = typeof(StudioForm).GetMethod("OnMouseWheel", BF | BindingFlags.DeclaredOnly);
                    int bx = lbox.Right + 29, byy = lbox.Top + lbox.Height / 2; Action waitG = () => { var swG = System.Diagnostics.Stopwatch.StartNew(); while (swG.ElapsedMilliseconds < 700) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } pnt(); };
                    int l0 = firstL(); wm.Invoke(sf, new object[] { new MouseEventArgs(MouseButtons.None, 0, bx, byy, -120) }); waitG(); int l1 = firstL(); wm.Invoke(sf, new object[] { new MouseEventArgs(MouseButtons.None, 0, 40, byy, -120) }); waitG(); int l2 = firstL();
                    Check("on the LYRICS page the mouse wheel over the scroll bar scrolls the lyrics (it did nothing), and so does the wheel beside the box", l1 > l0 && l2 > l1, l0 + " -> " + l1 + " -> " + l2);
                    // a click on the bar glides there one line at a time (you can keep reading), the logo dragged follows straight away
                    lbox.SelectionStart = 0; lbox.ScrollToCaret(); pnt(); HitReg trk = null; var barR = new RectangleF(lbox.Right + 8, lbox.Top, 42, lbox.Height); foreach (HitReg hr in (System.Collections.IList)typeof(StudioForm).GetField("regs", BF).GetValue(sf)) if (barR.Contains(new PointF(hr.R.X + hr.R.Width / 2, hr.R.Y + hr.R.Height / 2)) && hr.R.Height > 100 && hr.OnDown != null) trk = hr;
                    int g0 = firstL(), gSoon = -1, gEnd = -1; if (trk != null) { typeof(StudioForm).GetField("MousePos", BF).SetValue(sf, new PointF(trk.R.X + 10, trk.R.Bottom - 1)); trk.OnDown(); var swS = System.Diagnostics.Stopwatch.StartNew(); while (swS.ElapsedMilliseconds < 250) { Application.DoEvents(); System.Threading.Thread.Sleep(5); } gSoon = firstL(); var swE = System.Diagnostics.Stopwatch.StartNew(); while (swE.ElapsedMilliseconds < 20000 && sf.lyricGlideTo >= 0) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } gEnd = firstL(); }
                    Check("clicking the lyrics scroll bar glides there slowly, one line at a time (readable), and gets to the end", trk != null && gSoon > g0 && gSoon < g0 + 8 && gEnd > gSoon + 20, g0 + " -> " + gSoon + " after 0.25 s -> " + gEnd);
                    { lbox.SelectionStart = 0; lbox.ScrollToCaret(); pnt(); int w0l = firstL(); var scr = lbox.PointToScreen(new Point(lbox.Width / 2, lbox.Height / 2)); var smP = typeof(StudioForm).GetMethod("SendMessageP", BindingFlags.NonPublic | BindingFlags.Static);
                      smP.Invoke(null, new object[] { lbox.Handle, 0x020A, (IntPtr)((-120) << 16), (IntPtr)((scr.Y << 16) | (scr.X & 0xFFFF)) }); { var swW = System.Diagnostics.Stopwatch.StartNew(); while (swW.ElapsedMilliseconds < 600) { Application.DoEvents(); System.Threading.Thread.Sleep(5); } } int w1l = firstL();
                      Check("the mouse wheel with the pointer over the lyrics themselves scrolls them, 3 lines a notch", w1l == w0l + 3, w0l + " -> " + w1l); }
                    float sz0 = Prefs.LyricSize; int w0 = lbox.Width; sf.LyricSizeBy(1.5f); pnt(); bool bigger = Math.Abs(Prefs.LyricSize - sz0 - 1.5f) < 0.01f && Math.Abs(lbox.Font.Size - Prefs.LyricSize) < 0.1f; sf.LyricSizeBy(-1.5f); sf.LyricFillToggle(); pnt(); bool wide = lbox.Width > w0 + 100 && !F<TextBox>("lyricTitle").Visible; sf.LyricFillToggle(); pnt(); bool back = lbox.Width == w0;
                    Check("lyrics A+ / A- change the text size (kept in Settings), FILL SCREEN gives the lyrics the whole window and back", bigger && wide && back && Math.Abs(Prefs.LyricSize - sz0) < 0.01f, bigger + " " + wide + " " + back);
                    lbox.Text = keepLy; SetF("tab", 0);
                }
                {   // SAVE PRESET under the effects: the track's plugins (with their settings) become your own preset, found in PRESETS... on any track
                    var st = StudioEngine.NewTrack("Vox", "vocal", null); st.Fx.Add(Presets.Make("eq", "")); st.Fx.Add(Presets.Make("comp", "")); var empty = StudioEngine.NewTrack("Empty", "vocal", null);
                    string nmS = "Test Vox Chain " + Guid.NewGuid().ToString("N").Substring(0, 6); sf.savePresetAskHook = q => nmS; bool okS = sf.SaveTrackPreset(st); string pf = Path.Combine(Path.Combine(StudioEnv.DataDir, "chains"), nmS + ".chain"); bool fileS = File.Exists(pf); string txtS = fileS ? File.ReadAllText(pf) : "";
                    bool noFx = !sf.SaveTrackPreset(empty); sf.savePresetAskHook = q => null; bool cancelled = !sf.SaveTrackPreset(st); sf.savePresetAskHook = null; try { File.Delete(pf); } catch { }
                    Check("SAVE PRESET saves the track's plugins with their settings as your own preset (nothing to save, or cancel, saves nothing)", okS && fileS && txtS.Contains("eq") && txtS.Contains("comp") && noFx && cancelled, okS + " " + fileS + " " + noFx + " " + cancelled);
                }
                {   // whatever put it there (USE, TRY then KEEP, an AUTO MASTERING version) the name of what is on the master is shown, and kept in the project
                    sf.masterPresetUsed = -1; SetF("mpBackup", (object)new string[0]); SetF("mpPreviewIdx", 5); typeof(StudioForm).GetMethod("PresetKeep", BF).Invoke(sf, null); bool kept5 = sf.masterPresetUsed == 5 && sf.MasterPresetName == MasterPresets.All[5].Name;
                    sf.MasterPresetName = "Auto Mastering: Vocal Forward"; string mx = ProjectIO.SerializeMix(sf.eng); sf.MasterPresetName = ""; ProjectIO.RestoreMix(sf.eng, mx); bool amName = sf.MasterPresetName == "Auto Mastering: Vocal Forward" && sf.masterPresetUsed == -1 && !ProjectIO.MasterPresetEdited(sf.eng);
                    Check("TRY then KEEP marks the preset as in use, and an AUTO MASTERING version shows by its name (kept in the project)", kept5 && amName, kept5 + " " + amName + " [" + sf.MasterPresetName + "]"); sf.MasterPresetName = "";
                }
                {   // the gain of one section: a tag on the section you drag up / down (double-click for 0 dB), and quick values on right-click
                    int sr = 44100, n3 = sr * 20; var x3 = new float[n3]; for (int i = 0; i < n3; i++) x3[i] = (float)(0.3 * Math.Sin(i * 0.05)); var cl3 = AudioClip.FromArrays(x3, (float[])x3.Clone(), "g");
                    var keep3 = new List<Track>(sf.eng.Tracks); sf.eng.Tracks.Clear(); var ga = StudioEngine.NewTrack("Vox", "vocal", cl3); ga.ClipFrames = sr * 10; var gb = StudioEngine.NewTrack("Vox", "vocal", cl3); gb.Host = ga; gb.Offset = sr * 10; gb.ClipStart = sr * 10; gb.ClipFrames = sr * 10; gb.ClipGainDb = 3f; sf.eng.Tracks.Add(ga); sf.eng.Tracks.Add(gb);
                    SetF("tab", 0); SetF("welcomeOn", false); typeof(StudioForm).GetMethod("FitView", BF).Invoke(sf, null); var regsF = typeof(StudioForm).GetField("regs", BF);
                    using (var bm3 = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height)) using (var g3 = Graphics.FromImage(bm3)) typeof(StudioForm).GetMethod("PaintAll", BF).Invoke(sf, new object[] { new PaintEventArgs(g3, new Rectangle(Point.Empty, sf.ClientSize)) });
                    HitReg tag = null; foreach (HitReg hr in (System.Collections.IList)regsF.GetValue(sf)) if (hr.OnDrag != null && hr.OnDouble != null && Math.Abs(hr.R.Height - 17) < 0.5f) tag = hr;
                    bool dragged = false, reset = false; if (tag != null) { var c0 = new PointF(tag.R.X + tag.R.Width / 2, tag.R.Y + tag.R.Height / 2); typeof(StudioForm).GetField("MousePos", BF).SetValue(sf, c0); tag.OnDown(); tag.OnDrag(new PointF(c0.X, c0.Y - 20)); tag.OnUp(); dragged = Math.Abs(gb.ClipGainDb - 7f) < 0.01f && Math.Abs(ga.ClipGainDb) < 0.01f; tag.OnDouble(); reset = Math.Abs(gb.ClipGainDb) < 0.01f; }
                    sf.NudgeSectionGain(new List<Track> { ga, gb }, -3f); bool nudged = Math.Abs(ga.ClipGainDb + 3f) < 0.01f && Math.Abs(gb.ClipGainDb + 3f) < 0.01f; sf.SetSectionGain(new List<Track> { gb }, 40f); bool capped = Math.Abs(gb.ClipGainDb - 12f) < 0.01f;
                    Check("each section has its own gain: drag its tag up (+4 dB for 20 px, only that section), double-click for 0 dB, quick +/- on several at once, never past +12 dB", tag != null && dragged && reset && nudged && capped, (tag == null ? "no tag" : tag.R.ToString()) + " " + dragged + " " + reset + " " + nudged + " " + capped);
                    sf.eng.Tracks.Clear(); sf.eng.Tracks.AddRange(keep3);
                }
                {   // name a section (Yeahs, Adlibs...): only that piece shows the name; saved in the project, undo takes it back. REMOVE ALL PLUGINS clears every track and bus (instruments stay), undo puts them back
                    var ra = StudioEngine.NewTrack("Lead vocal", "vocal", null); var rb2 = StudioEngine.NewTrack("Lead vocal", "vocal", null); rb2.Host = ra; sf.eng.Tracks.Add(ra); sf.eng.Tracks.Add(rb2);
                    sf.labelAskHook = q => "  Adlibs "; sf.RenameSections(new List<Track> { rb2 }); bool named = rb2.SectionLabel == "Adlibs" && ra.SectionLabel == "" && ra.Name == "Lead vocal";
                    string lpf = Path.Combine(Path.GetTempPath(), "px_label_" + Guid.NewGuid().ToString("N") + ".ytmproj"); var le = new StudioEngine(); le.Tracks.Add(StudioEngine.NewTrack("Lead vocal", "vocal", null)); var lb = StudioEngine.NewTrack("Lead vocal", "vocal", null); lb.Host = le.Tracks[0]; lb.SectionLabel = "Yeahs | x"; le.Tracks.Add(lb); ProjectIO.Save(le, lpf); var le2 = new StudioEngine(); ProjectIO.Load(le2, lpf, z => { }); try { File.Delete(lpf); } catch { }
                    bool savedL = le2.Tracks.Count == 2 && le2.Tracks[1].SectionLabel == "Yeahs | x" && le2.Tracks[0].SectionLabel == "";
                    typeof(StudioForm).GetMethod("DoUndo", BF, null, Type.EmptyTypes, null).Invoke(sf, null); bool undone = rb2.SectionLabel == "";
                    Check("a section can have its own name (Yeahs, Adlibs): only that piece, the track keeps its name; saved in the project (even with a | in it), and undo takes it back", named && savedL && undone, named + " " + savedL + " " + undone);
                    sf.labelAskHook = null; sf.eng.Tracks.Remove(rb2); sf.eng.Tracks.Remove(ra);
                    var t1 = StudioEngine.NewTrack("Drums", "beat", null); t1.Fx.Add(Presets.Make("eq", "")); t1.Fx.Add(Presets.Make("comp", "")); var bus1 = StudioEngine.NewTrack("Bus 1", "bus", null); bus1.IsBus = true; bus1.Fx.Add(Presets.Make("reverb", "")); var ins = new SynthFx(); var t2 = StudioEngine.NewTrack("Bass", "inst", null); t2.Instrument = true; t2.Fx.Add(ins); t2.Fx.Add(Presets.Make("eq", ""));
                    var keepT = new List<Track>(sf.eng.Tracks); sf.eng.Tracks.Clear(); sf.eng.Tracks.Add(t1); sf.eng.Tracks.Add(t2); sf.eng.Tracks.Add(bus1); string rq = ""; sf.removeAllAskHook = q => { rq = q; return true; }; int gone = sf.RemoveEveryPlugin();
                    bool cleared = gone == 4 && t1.Fx.Count == 0 && bus1.Fx.Count == 0 && t2.Fx.Count == 1 && t2.Fx[0] == ins && rq.Contains("4 plugins"); typeof(StudioForm).GetMethod("DoUndo", BF, null, Type.EmptyTypes, null).Invoke(sf, null); bool back = t1.Fx.Count == 2 && bus1.Fx.Count == 1 && t2.Fx.Count == 2;
                    sf.removeAllAskHook = q => false; t1.Fx.Add(Presets.Make("eq", "")); int n0 = t1.Fx.Count; sf.RemoveEveryPlugin(); bool kept = t1.Fx.Count == n0;
                    Check("REMOVE ALL PLUGINS asks first, clears every track and bus (instruments stay), and one undo puts them all back", cleared && back && kept, gone + " " + cleared + " " + back + " " + kept);
                    sf.removeAllAskHook = null; sf.eng.Tracks.Clear(); sf.eng.Tracks.AddRange(keepT);
                }
                {   // recording: a microphone that sends pure silence (a muted headset) is noticed; a muted / off / not-soloed track asks before you record onto it
                    var rec0 = new AudioRecorder(); rec0.SetMode(MicMode.Standard); rec0.Feed(new short[50000], 50000); var rec1 = new AudioRecorder(); rec1.SetMode(MicMode.Standard); var hiss = new short[50000]; var rq2 = new Random(9); for (int i = 0; i < hiss.Length; i++) hiss[i] = (short)rq2.Next(-60, 60); rec1.Feed(hiss, hiss.Length);
                    Check("a microphone sending pure silence (muted on the headset, switched off) is noticed, a live one (even a quiet room) is not", rec0.AllSilent && !rec1.AllSilent, rec0.AllSilent + " " + rec1.AllSilent);
                    var tq = StudioEngine.NewTrack("Vox take", "vocal", null); sf.eng.Tracks.Add(tq); SetF("selTrack", sf.eng.Tracks.IndexOf(tq)); string askedQ = "";
                    tq.Mute = true; sf.ask3Hook = q => { askedQ = q; return DialogResult.Yes; }; bool goYes = sf.CheckRecordTargetAudible();
                    tq.Mute = true; sf.ask3Hook = q => DialogResult.No; bool goNo = sf.CheckRecordTargetAudible(); bool stillMuted = tq.Mute; sf.ask3Hook = q => DialogResult.Cancel; bool goCancel = sf.CheckRecordTargetAudible();
                    tq.Mute = false; askedQ = ""; sf.ask3Hook = q => { askedQ = q; return DialogResult.Yes; }; bool goPlain = sf.CheckRecordTargetAudible(); bool notAsked = askedQ.Length == 0;
                    Check("recording onto a muted track asks first: Yes unmutes it and records, No records anyway, Cancel does not record; an audible track is not asked", goYes && goNo && stillMuted && !goCancel && goPlain && notAsked, goYes + " " + goNo + " " + stillMuted + " " + goCancel + " " + goPlain + " " + notAsked);
                    sf.ask3Hook = null; sf.eng.Tracks.Remove(tq);
                }
                {
                    var pv = new StudioEngine { ListenOnly = true }; bool mixWas = sf.eng.Playing; sf.eng.Playing = false; typeof(StudioForm).GetField("previewEng", BF).SetValue(sf, pv); pv.Playing = true; int boost0 = Prefs.BufBoost;
                    typeof(StudioForm).GetField("lastRefresh", BF).SetValue(sf, DateTime.MinValue); sf.AudioGlitchCheck(DateTime.Now); WaveOutPlayer.Underruns += 3; bool fixedIt = sf.AudioGlitchCheck(DateTime.Now.AddMilliseconds(200));
                    Check("when a preview stutters it is fixed straight away (refreshed, buffer made bigger): it no longer waits for you to play the mix", fixedIt && pv.Reopens == 1, fixedIt + " reopens " + pv.Reopens);
                    pv.Playing = false; pv.Shutdown(); typeof(StudioForm).GetField("previewEng", BF).SetValue(sf, null); Prefs.BufBoost = boost0; sf.eng.Playing = mixWas;
                }
                }
            }
            {   // NEW takes you to the welcome page (a fresh start)
                sf.eng.Tracks.Clear(); SetF("tab", 0); SetF("welcomeOn", false); var pmn = typeof(StudioForm).GetMethod("PaintAll", BF); using (var bmn = new Bitmap(1500, 800)) using (var gn = Graphics.FromImage(bmn)) pmn.Invoke(sf, new object[] { new PaintEventArgs(gn, new Rectangle(0, 0, 1500, 800)) });
                var bmN = (Dictionary<string, RectangleF>)typeof(StudioForm).GetField("btnMap", BF).GetValue(sf); RectangleF nr; HitReg nreg = bmN.TryGetValue("NEW", out nr) ? (HitReg)typeof(StudioForm).GetMethod("TopRegion", BF).Invoke(sf, new object[] { new PointF(nr.X + nr.Width / 2, nr.Y + nr.Height / 2) }) : null;
                if (nreg != null) nreg.OnDown(); Check("NEW starts a new project on the welcome page", nreg != null && F<bool>("welcomeOn") && sf.eng.Tracks.Count == 0, nreg == null ? "no NEW button" : ""); SetF("welcomeOn", false);
            }
            {   // copy / paste of plugins and of pieces, the preset shown on the plugin, REMOVE PRESET
                sf.eng.Tracks.Clear(); var ta = StudioEngine.NewTrack("Row A", "vocal", AudioClip.FromArrays(new float[441000], new float[441000], "t")); sf.eng.Tracks.Add(ta);
                var d1 = FxRegistry.Create("delay"); d1.Prepare(44100); d1.Set("fb", 61f); var d2 = FxRegistry.Create("delay"); d2.Prepare(44100); var cmp = FxRegistry.Create("comp"); cmp.Prepare(44100); ta.Fx.Add(d2); ta.Fx.Add(cmp);
                SetF("fxCopyType", d1.Type); SetF("fxCopyData", d1.Serialize()); SetF("fxCopyTitle", d1.Title);
                sf.PasteFx(d2, ta); Check("PASTE puts the copied settings on a plugin of the same kind", Math.Abs(d2.Get("fb") - 61f) < 0.01f, d2.Get("fb").ToString());
                int before = ta.Fx.Count; sf.PasteFx(cmp, ta); Check("PASTE on a different kind adds the copied plugin as a new one on the track", ta.Fx.Count == before + 1 && ta.Fx[ta.Fx.Count - 1].Type == "delay" && Math.Abs(ta.Fx[ta.Fx.Count - 1].Get("fb") - 61f) < 0.01f, ta.Fx.Count.ToString());
                {   // an auto-tune whose key you set keeps that key when it is pasted (the copy used to wait to detect a key and overwrite the one you copied)
                    var pc = FxRegistry.Create("pitch"); pc.Prepare(44100); pc.Set("key", 7f); pc.AutoKeyPending = false; sf.CopyFx(pc); int n0 = ta.Fx.Count; sf.PasteFxAsNew(ta); var np = ta.Fx.Count > n0 ? ta.Fx[ta.Fx.Count - 1] : null;
                    Check("a pasted auto-tune keeps the copied key and does not detect a new one over it", np != null && np.Type == "pitch" && Math.Abs(np.Get("key") - 7f) < 0.01f && !np.AutoKeyPending, np == null ? "none" : np.Get("key") + " pending=" + np.AutoKeyPending);
                    var pc2 = FxRegistry.Create("pitch"); pc2.Prepare(44100); sf.PasteFx(pc2, ta); Check("pasting onto an auto-tune gives it the copied key for good", Math.Abs(pc2.Get("key") - 7f) < 0.01f && !pc2.AutoKeyPending, pc2.Get("key") + " pending=" + pc2.AutoKeyPending);
                    var bl = FxRegistry.Create("synth"); bl.Prepare(44100); sf.CopyFx(bl); n0 = ta.Fx.Count; sf.PasteFxAsNew(ta); var nb = ta.Fx.Count > n0 ? ta.Fx[ta.Fx.Count - 1] as InstrumentFx : null;
                    Check("a pasted Bass Lab belongs to the track it was pasted on (it plays that track's notes)", nb != null && nb.Owner == ta, nb == null ? "none" : "owner " + (nb.Owner == null ? "none" : nb.Owner.Name));
                    lock (sf.eng.Sync) { ta.Fx.RemoveAll(x => x.Type == "pitch" || x.Type == "synth"); }
                    var keyed = new List<string>(); foreach (var inf in FxRegistry.All) { if (PluginLibrary.IsGenerated(inf.Type)) continue; var kf = FxRegistry.Create(inf.Type); if (kf != null && StudioForm.HasKeyParam(kf)) keyed.Add(inf.Type); }
                    Check("every plugin with a key gets AUTO DETECT (Pitch Correction, Melody Tune and the tuners made from them), and only those", keyed.Contains("pitch") && keyed.Contains("lib_melody_tune") && !keyed.Contains("comp") && !keyed.Contains("delay"), keyed.Count + ": " + string.Join(", ", keyed.ToArray()));
                    var mt = FxRegistry.Create("lib_melody_tune"); mt.Prepare(44100); var silent = StudioEngine.NewTrack("Empty", "vocal", null); sf.AutoDetectKey(mt, silent);
                    Check("AUTO DETECT on a track without audio says so and leaves the key alone", !mt.AutoKeyPending && Math.Abs(mt.Get("key")) < 0.01f && F<string>("toast").Contains("needs audio"), F<string>("toast"));
                }
                d2.CurPreset = ""; string none = StudioForm.PresetShown(d2); d2.CurPreset = "Slapback"; d2.CurPresetData = d2.Serialize(); string withP = StudioForm.PresetShown(d2); d2.Set("mix", 7f); string changed = StudioForm.PresetShown(d2);
                Check("the plugin shows which preset is on it (or NONE, or that it was changed by hand)", none == "PRESET: NONE" && withP == "PRESET: SLAPBACK" && changed.StartsWith("PRESET: SLAPBACK") && changed.Contains("CHANGED"), none + " | " + withP + " | " + changed);
                sf.RemoveFxPreset(d2); Check("REMOVE PRESET takes it off and puts the plugin back to its own defaults", d2.CurPreset == "" && Math.Abs(d2.Get("fb") - d2.Find("fb").Def) < 0.01f);
                sf.CopyClip(ta); sf.PasteClip(ta, 441000); var pasted = sf.eng.Tracks.Count == 2 ? sf.eng.Tracks[1] : null;
                Check("right-click Copy then Paste here puts a second piece on the same track, where you clicked", pasted != null && pasted.Host == ta && pasted.Offset == 441000 && pasted.Clip == ta.Clip, sf.eng.Tracks.Count + " tracks");
                {   // Ctrl+C copies every highlighted section, Ctrl+V pastes them at the needle on the picked track, keeping their spacing in time and between tracks
                    sf.eng.Tracks.Clear(); var ka = StudioEngine.NewTrack("A", "vocal", AudioClip.FromArrays(new float[88200], new float[88200], "a")); ka.Offset = 44100; var kb = StudioEngine.NewTrack("B", "vocal", AudioClip.FromArrays(new float[88200], new float[88200], "b")); kb.Offset = 44100 * 3; sf.eng.Tracks.Add(ka); sf.eng.Tracks.Add(kb);
                    var msel = F<List<Track>>("multiSel"); msel.Clear(); msel.Add(ka); msel.Add(kb); SetF("tab", 0); var pk = typeof(StudioForm).GetMethod("ProcessCmdKey", BF); Func<Keys, bool> key = kk => { var m = new Message(); return (bool)pk.Invoke(sf, new object[] { m, kk }); };
                    bool cOk = key(Keys.Control | Keys.C); string afterC = F<string>("toast"); msel.Clear(); SetF("selTrack", 0); sf.eng.Seek(44100 * 10); Application.DoEvents(); bool vOk = key(Keys.Control | Keys.V);
                    var newA = sf.eng.Tracks.Find(t => t.Host == ka); var newB = sf.eng.Tracks.Find(t => t.Host == kb);
                    Check("Ctrl+C copies all the highlighted sections and Ctrl+V pastes them at the needle, same spacing, same tracks", cOk && vOk && newA != null && newB != null && newA.Offset == 44100 * 10 && newB.Offset == 44100 * 12 && newA.Clip == ka.Clip && newB.Clip == kb.Clip, (newA == null ? "no A" : newA.Offset.ToString()) + " / " + (newB == null ? "no B" : newB.Offset.ToString()) + "  copy said: " + afterC);
                    typeof(StudioForm).GetMethod("DoUndo", BF, null, Type.EmptyTypes, null).Invoke(sf, null); Check("one Ctrl+Z takes the whole paste away", sf.eng.Tracks.Count == 2, sf.eng.Tracks.Count + " tracks");
                }
                {   // Ctrl+X cuts the highlighted audio: copied, then taken off the timeline; Ctrl+V puts it back at the needle; Ctrl+Z undoes the cut
                    sf.eng.Tracks.Clear(); var xa = StudioEngine.NewTrack("A", "vocal", AudioClip.FromArrays(new float[88200], new float[88200], "xa")); xa.Offset = 44100; var xb = StudioEngine.NewTrack("B", "vocal", AudioClip.FromArrays(new float[88200], new float[88200], "xb")); xb.Offset = 44100 * 3; sf.eng.Tracks.Add(xa); sf.eng.Tracks.Add(xb);
                    var xclipA = xa.Clip; var xclipB = xb.Clip; var xsel = F<List<Track>>("multiSel"); xsel.Clear(); xsel.Add(xa); xsel.Add(xb); SetF("tab", 0); var xpk = typeof(StudioForm).GetMethod("ProcessCmdKey", BF); Func<Keys, bool> xkey = kk => { var m = new Message(); return (bool)xpk.Invoke(sf, new object[] { m, kk }); };
                    bool xOk = xkey(Keys.Control | Keys.X); string xToast = F<string>("toast"); bool gone = !sf.eng.Tracks.Exists(t => t.Clip == xclipA) && !sf.eng.Tracks.Exists(t => t.Clip == xclipB);
                    Check("Ctrl+X cuts every highlighted section (they leave the timeline)", xOk && gone && xToast.StartsWith("Cut"), xToast);
                    typeof(StudioForm).GetMethod("DoUndo", BF, null, Type.EmptyTypes, null).Invoke(sf, null); Check("Ctrl+Z brings the cut audio back", sf.eng.Tracks.Exists(t => t.Clip == xclipA) && sf.eng.Tracks.Exists(t => t.Clip == xclipB), sf.eng.Tracks.Count + " tracks");
                    xsel.Clear(); xsel.Add(sf.eng.Tracks.Find(t => t.Clip == xclipA)); xsel.Add(sf.eng.Tracks.Find(t => t.Clip == xclipB)); xkey(Keys.Control | Keys.X); xsel.Clear(); SetF("selTrack", 0); sf.eng.Seek(44100 * 10); Application.DoEvents(); xkey(Keys.Control | Keys.V);
                    var pa = sf.eng.Tracks.Find(t => t.Clip == xclipA); var pb2 = sf.eng.Tracks.Find(t => t.Clip == xclipB);
                    Check("after Ctrl+X, Ctrl+V pastes the cut sections at the needle, same spacing", pa != null && pb2 != null && pa.Offset == 44100 * 10 && pb2.Offset == 44100 * 12, (pa == null ? "no A" : pa.Offset.ToString()) + " / " + (pb2 == null ? "no B" : pb2.Offset.ToString()));
                    xsel.Clear(); string t0 = F<string>("toast"); typeof(StudioForm).GetField("multiSel", BF); SetF("selTrack", -1); bool nothing = xkey(Keys.Control | Keys.X); Check("Ctrl+X with nothing picked deletes nothing", sf.eng.Tracks.Exists(t => t.Clip == xclipA) && sf.eng.Tracks.Exists(t => t.Clip == xclipB));
                }
                sf.eng.Tracks.Clear();
            }
            {   // clicking on the empty part of a track's row (past its audio) does not switch to that track
                sf.eng.Tracks.Clear(); sf.eng.Tracks.Add(StudioEngine.NewTrack("Long", "vocal", AudioClip.FromArrays(new float[441000], new float[441000], "t"))); sf.eng.Tracks.Add(StudioEngine.NewTrack("Short", "beat", AudioClip.FromArrays(new float[44100], new float[44100], "t")));
                sf.ClientSize = new Size(1500, 800); Application.DoEvents(); SetF("tab", 0); SetF("welcomeOn", false); SetF("selTrack", 0); SetF("vscroll", 0f); SetF("projBin", false);
                var pme = typeof(StudioForm).GetMethod("PaintAll", BF); Action paintE = () => { using (var bme = new Bitmap(1500, 800)) using (var ge = Graphics.FromImage(bme)) { var pee = new PaintEventArgs(ge, new Rectangle(Point.Empty, bme.Size)); for (int i = 0; i < 3; i++) pme.Invoke(sf, new object[] { pee }); } }; paintE();
                var lanes = new List<HitReg>(); foreach (object o in (System.Collections.IEnumerable)typeof(StudioForm).GetField("regs", BF).GetValue(sf)) { var rg = (HitReg)o; if (rg.R.Width > 600 && rg.R.Height > 100 && rg.R.Height < 160 && rg.R.X > 300 && rg.OnDrag != null) lanes.Add(rg); } lanes.Sort((p, r) => p.R.Y.CompareTo(r.R.Y));
                Func<string, MouseButtons, int, int, object> evt = (nm, b, x, y) => typeof(StudioForm).GetMethod(nm, BF | BindingFlags.DeclaredOnly).Invoke(sf, new object[] { new MouseEventArgs(b, 1, x, y, 0) });
                bool stayed = false, pickedOn = false, onAudioStayed = false; if (lanes.Count >= 2)
                {
                    var l2 = lanes[1].R; int ex = (int)(l2.Right - 120), ey = (int)(l2.Y + l2.Height / 2); evt("OnMouseMove", MouseButtons.None, ex, ey); evt("OnMouseDown", MouseButtons.Left, ex, ey); evt("OnMouseUp", MouseButtons.Left, ex, ey); paintE(); stayed = F<int>("selTrack") == 0;
                    int cx = (int)(l2.X + 20), cy = ey; evt("OnMouseMove", MouseButtons.None, cx, cy); evt("OnMouseDown", MouseButtons.Left, cx, cy); evt("OnMouseUp", MouseButtons.Left, cx, cy); paintE(); onAudioStayed = F<int>("selTrack") == 0;
                    int hx = -1, hy = -1; { var topR2 = typeof(StudioForm).GetMethod("TopRegion", BF); float hw = Convert.ToSingle(typeof(StudioForm).GetField("HeadFullW", BF | BindingFlags.Static).GetValue(null)); for (int yy2 = (int)l2.Y + 4; yy2 < l2.Bottom - 4 && hx < 0; yy2 += 3) for (int xx2 = 4; xx2 < hw - 4 && hx < 0; xx2 += 3) { var hr = (HitReg)topR2.Invoke(sf, new object[] { new PointF(xx2, yy2) }); if (hr != null && (int)hr.R.Width == (int)hw && (int)hr.R.X == 0) { hx = xx2; hy = yy2; } } } evt("OnMouseMove", MouseButtons.None, hx, hy); evt("OnMouseDown", MouseButtons.Left, hx, hy); evt("OnMouseUp", MouseButtons.Left, hx, hy); paintE(); pickedOn = F<int>("selTrack") == 1;
                }
                Check("clicking past the audio of a track does not change the selected track", stayed, lanes.Count + " lanes");
                Check("clicking on the audio itself does not change the selected track either", onAudioStayed);
                Check("the box of the track on the left (the header) is what picks the track", pickedOn);
                sf.eng.Tracks.Clear();
            }
            {   // the side menu: pointing at COMMUNITY (or any menu) shows its tabs at once, no click
                sf.eng.Tracks.Clear(); sf.ClientSize = new Size(1500, 800); Application.DoEvents(); SetF("tab", 0); SetF("welcomeOn", false); typeof(StudioForm).GetMethod("OpenMenu", BF).Invoke(sf, null);
                var pmm = typeof(StudioForm).GetMethod("PaintAll", BF); Action paintM = () => { using (var bmm = new Bitmap(1500, 800)) using (var gm = Graphics.FromImage(bmm)) { var pem = new PaintEventArgs(gm, new Rectangle(Point.Empty, bmm.Size)); for (int i = 0; i < 3; i++) pmm.Invoke(sf, new object[] { pem }); } }; paintM();
                float[] ys = (float[])typeof(StudioForm).GetField("menuYs", BF).GetValue(sf); Action<int, int> mv = (x, y) => typeof(StudioForm).GetMethod("OnMouseMove", BF | BindingFlags.DeclaredOnly).Invoke(sf, new object[] { new MouseEventArgs(MouseButtons.None, 0, x, y, 0) });
                SetF("menuSec", -1); mv(60, (int)ys[5] + 10); paintM(); int hov = F<int>("menuSec");
                mv(60, (int)ys[0] + 10); paintM(); int hov0 = F<int>("menuSec");
                int lastI = ((string[])typeof(StudioForm).GetField("MenuNames", BF | BindingFlags.Static).GetValue(null)).Length - 1; mv(60, (int)ys[lastI] + 10); paintM(); int hovX = F<int>("menuSec");
                Check("pointing at COMMUNITY opens its tabs (no click), pointing at another menu switches, pointing at a single button closes them", hov == 5 && hov0 == 0 && hovX == -1, hov + " " + hov0 + " " + hovX);
                SetF("menuOpen", false); SetF("menuSec", -1);
            }
            {   // the space bar starts and stops the mix on the Mix page only
                sf.eng.Tracks.Clear(); sf.eng.Tracks.Add(StudioEngine.NewTrack("Space", "vocal", AudioClip.FromArrays(new float[441000], new float[441000], "t"))); SetF("welcomeOn", false); SetF("menuOpen", false);
                var pck = typeof(StudioForm).GetMethod("ProcessCmdKey", BF); sf.eng.Stop(); SetF("tab", 1); pck.Invoke(sf, new object[] { new Message(), Keys.Space }); bool otherPage = sf.eng.Playing; sf.eng.Stop();
                SetF("tab", 0); pck.Invoke(sf, new object[] { new Message(), Keys.Space }); bool mixPage = sf.eng.Playing; sf.eng.Stop(); sf.eng.Tracks.Clear();
                Check("the space bar does not start the mix on another page, and still does on the Mix page", !otherPage && mixPage, otherPage + " / " + mixPage);
            }
            {   // PERFORMANCE MODE: the highest frame rate the screen can show (up to 240), the effects off, and the frame loop never starves the mouse
                bool pk = Prefs.PerfMode; int capK = Prefs.FpsCap; Prefs.FpsCap = 60; Prefs.PerfMode = false; int offFps = Prefs.FpsEff; Prefs.PerfMode = true; int onFps = Prefs.FpsEff; sf.ApplyFps(); var lp = (FrameLoop)typeof(StudioForm).GetField("studioLoop", BF).GetValue(sf); int loopFps = lp.TargetFps;
                Check("performance mode asks for 240 frames (the screen decides how many it shows), off it follows the chosen limit", onFps == 240 && offFps == 60 && loopFps == Math.Max(30, Math.Min(240, FrameLoop.DisplayHz())), offFps + " / " + onFps + " / loop " + loopFps);
                Prefs.PerfMode = false; sf.ApplyFps(); Check("turning it off puts the frame rate back at once", lp.TargetFps == Math.Max(30, Math.Min(60, FrameLoop.DisplayHz())), lp.TargetFps.ToString());
                var tickF = typeof(FrameLoop).GetField("lastTickMs", BindingFlags.NonPublic | BindingFlags.Instance); Check("the frame loop measures how long a frame takes, so it can leave time for the mouse", tickF != null);
                Prefs.PerfMode = pk; Prefs.FpsCap = capK; sf.ApplyFps();
            }
            {   // MESSAGES: type straight into the conversation, press a message to reply to it
                bool pubK = ProfileStore.Published; ProfileStore.Published = true; var th = new MsgThread { With = new ArtistInfo { Id = "a0123456789ab", Name = "Neo" } }; th.Msgs.Add(new MsgItem { Mine = false, At = DateTime.UtcNow.Ticks, Text = "Want to make a track together?" });
                var thr = (System.Collections.Generic.List<MsgThread>)typeof(StudioForm).GetField("msgThreads", BF).GetValue(sf); thr.Clear(); thr.Add(th); SetF("msgWithId", "a0123456789ab"); SetF("tab", 7); SetF("artView", 1); SetF("artSel", null); SetF("welcomeOn", false); SetF("menuOpen", false);
                sf.ClientSize = new Size(1500, 860); Application.DoEvents(); var pmg = typeof(StudioForm).GetMethod("PaintAll", BF); Action paintG = () => { using (var bm = new Bitmap(1500, 860)) using (var gg = Graphics.FromImage(bm)) { var pe = new PaintEventArgs(gg, new Rectangle(Point.Empty, bm.Size)); for (int i = 0; i < 2; i++) pmg.Invoke(sf, new object[] { pe }); } }; paintG();
                HitReg bubble = null, box = null; foreach (object o in (System.Collections.IEnumerable)typeof(StudioForm).GetField("regs", BF).GetValue(sf)) { var rg = (HitReg)o; if (rg.R.Width > 300 && rg.R.Height > 30 && rg.R.Height < 60 && rg.R.Y > 600 && rg.OnDown != null) box = rg; if (rg.R.Width > 200 && rg.R.Height >= 40 && rg.R.Height < 120 && rg.R.Y < 600 && rg.R.X > 400 && rg.OnDown != null) bubble = rg; }
                Check("the conversation has a reply box under it and the messages can be pressed", box != null && bubble != null);
                if (bubble != null) bubble.OnDown(); Check("pressing a message starts a reply to it, with the cursor in the box", F<string>("msgReplyTo").Contains("Want to make") && F<bool>("msgTyping"), F<string>("msgReplyTo"));
                var kp = typeof(StudioForm).GetMethod("OnKeyPress", BF); foreach (char ch in "yes! x") kp.Invoke(sf, new object[] { new KeyPressEventArgs(ch) }); kp.Invoke(sf, new object[] { new KeyPressEventArgs('\b') });
                Check("letters go into the message (also space and the letters that are shortcuts elsewhere), backspace deletes", F<string>("msgDraft") == "yes! ", "[" + F<string>("msgDraft") + "]");
                var pck2 = typeof(StudioForm).GetMethod("ProcessCmdKey", BF); bool took = (bool)pck2.Invoke(sf, new object[] { new Message(), Keys.Escape }); Check("Esc first stops the reply, then stops typing", took && F<string>("msgReplyTo") == "" && F<bool>("msgTyping")); pck2.Invoke(sf, new object[] { new Message(), Keys.Escape }); Check("and a second Esc leaves the box", !F<bool>("msgTyping"));
                SetF("msgDraft", ""); thr.Clear(); SetF("msgWithId", ""); SetF("tab", 0); SetF("artView", 0); ProfileStore.Published = pubK;
            }
            {   // closing the DAW in any way (not only the X) pauses the mix: the engine belongs to the main window and used to keep playing
                var eng2 = (StudioEngine)typeof(StudioForm).GetField("eng", BF).GetValue(sf); var sf2 = new StudioForm(app); sf2.Show(); Pump(150); eng2.Tracks.Add(StudioEngine.NewTrack("Loop", "beat", AudioClip.FromArrays(new float[441000], new float[441000], "x"))); eng2.Play(); Check("before: the mix plays", eng2.Playing);
                sf2.Close(); Pump(100); Check("after the DAW window closes the mix is paused (nothing keeps playing)", !eng2.Playing); lock (eng2.Sync) eng2.Tracks.Clear();
            }
            {   // MESSAGES and FRIENDS: ACCEPT, DECLINE and the conversations take the click (the scroll area of the list was on top of them and ate every click)
                bool pubK = ProfileStore.Published; ProfileStore.Published = true; var reqs = (System.Collections.Generic.List<ArtistInfo>)typeof(StudioForm).GetField("profReqs", BF).GetValue(sf); reqs.Clear(); reqs.Add(new ArtistInfo { Id = "a0000000000aa", Name = "Rowan" });
                var thr = (System.Collections.Generic.List<MsgThread>)typeof(StudioForm).GetField("msgThreads", BF).GetValue(sf); thr.Clear(); var th = new MsgThread { With = new ArtistInfo { Id = "a0000000000bb", Name = "Neo" } }; th.Msgs.Add(new MsgItem { Mine = false, At = DateTime.UtcNow.Ticks, Text = "yo" }); thr.Add(th);
                var topR = typeof(StudioForm).GetMethod("TopRegion", BF); var pmg = typeof(StudioForm).GetMethod("PaintAll", BF);
                foreach (int view in new[] { 1, 3 })
                {
                    SetF("tab", 7); SetF("artView", view); SetF("artSel", null); SetF("welcomeOn", false); SetF("menuOpen", false); SetF("msgWithId", ""); sf.ClientSize = new Size(1500, 860); Application.DoEvents();
                    using (var bm = new Bitmap(1500, 860)) using (var gg = Graphics.FromImage(bm)) { var pe = new PaintEventArgs(gg, new Rectangle(Point.Empty, bm.Size)); for (int i = 0; i < 2; i++) pmg.Invoke(sf, new object[] { pe }); }
                    HitReg acc = null, dec = null; foreach (object o in (System.Collections.IEnumerable)typeof(StudioForm).GetField("regs", BF).GetValue(sf)) { var rg = (HitReg)o; if (rg.OnDown != null && (int)rg.R.Width == 92 && (int)rg.R.Height == 24) acc = rg; if (rg.OnDown != null && (int)rg.R.Width == 82 && (int)rg.R.Height == 24) dec = rg; }
                    string nm = view == 1 ? "Messages" : "Friends"; Check(nm + ": the request has ACCEPT and DECLINE", acc != null && dec != null);
                    if (acc != null) { var hit = (HitReg)topR.Invoke(sf, new object[] { new PointF(acc.R.X + acc.R.Width / 2, acc.R.Y + acc.R.Height / 2) }); Check(nm + ": a click on ACCEPT reaches ACCEPT (not the scroll area of the list)", hit == acc); }
                    if (dec != null) { var hit = (HitReg)topR.Invoke(sf, new object[] { new PointF(dec.R.X + dec.R.Width / 2, dec.R.Y + dec.R.Height / 2) }); Check(nm + ": a click on DECLINE reaches DECLINE", hit == dec); }
                }
                SetF("tab", 0); SetF("artView", 0); reqs.Clear(); thr.Clear(); ProfileStore.Published = pubK;
            }
            {   // a row whose own clip was moved to another row (only pieces are left on it): its plugins still play every piece on the row
                Func<bool, bool, float> peakOf = (withFx, hostHasClip) => { var e = new StudioEngine(); int nS = 44100; var one = new float[nS]; for (int i = 0; i < nS; i++) one[i] = 0.5f;
                    var host = StudioEngine.NewTrack("Row", "vocal", hostHasClip ? AudioClip.FromArrays(new float[nS], new float[nS], "h") : null); if (withFx) { var gfx = FxRegistry.Create("gain"); gfx.Prepare(44100); gfx.Set("gain", -24); host.Fx.Add(gfx); }
                    var p1 = StudioEngine.NewTrack("Piece 1", "vocal", AudioClip.FromArrays(one, (float[])one.Clone(), "p")); p1.Host = host; var p2 = StudioEngine.NewTrack("Piece 2", "vocal", AudioClip.FromArrays(one, (float[])one.Clone(), "p")); p2.Host = host; p2.Offset = nS / 2;
                    e.Tracks.Add(host); e.Tracks.Add(p1); e.Tracks.Add(p2); float[] L, R; e.Render(0, nS, new RenderOpts { Master = false }, null, out L, out R); float pk = 0; for (int i = 0; i < L.Length; i++) pk = Math.Max(pk, Math.Abs(L[i])); e.Shutdown(); return pk; };
                float dry = peakOf(false, false), wet = peakOf(true, false), wetClip = peakOf(true, true);
                Check("pieces on a row whose own track has no audio any more play through that row's plugins (-24 dB gain on the row)", dry > 0.1f && wet < dry * 0.1f, dry + " -> " + wet);
                Check("and the same when the row still has its own clip", wetClip < dry * 0.1f, dry + " -> " + wetClip);
            }
            {   // COPY PRESET / PASTE PRESET and the ALL PLUGINS panel
                sf.eng.Tracks.Clear(); var tA = StudioEngine.NewTrack("Vox A", "vocal", AudioClip.FromArrays(new float[44100], new float[44100], "a")); var tB = StudioEngine.NewTrack("Vox B", "vocal", AudioClip.FromArrays(new float[44100], new float[44100], "b")); sf.eng.Tracks.Add(tA); sf.eng.Tracks.Add(tB);
                var own = FxRegistry.Create("delay"); own.Prepare(44100); tB.Fx.Add(own);
                var pc = new List<Fx>(); var e1 = FxRegistry.Create("eq"); var c1 = FxRegistry.Create("comp"); c1.Set("ratio", 6); pc.Add(e1); pc.Add(c1); foreach (var f in pc) f.Prepare(44100);
                typeof(StudioForm).GetMethod("PutPreset", BF).Invoke(sf, new object[] { tA, "Technical Rap", pc });
                string key = tA.Fx[0].PresetId; sf.CopyPreset(tA, key); Check("COPY on a preset copies all its plugins", sf.presetCopy.Count == 2 && sf.presetCopyName == "Technical Rap", sf.presetCopy.Count + " " + sf.presetCopyName);
                sf.PastePreset(tB); var pres = tB.Fx.FindAll(f => f.Preset == "Technical Rap");
                Check("PASTE PRESET puts the whole preset on the other track, with its settings, in a box of its own (your own plugins stay)", pres.Count == 2 && tB.Fx.Contains(own) && Math.Abs(pres[1].Get("ratio") - 6) < 0.01 && pres[0].PresetId != key, pres.Count + " / " + tB.Fx.Count);
                Check("the pasted plugins are new ones (changing them leaves the first track alone)", !tA.Fx.Contains(pres[0]) && !tA.Fx.Contains(pres[1]));
                sf.OpenAllFx(); SetF("tab", 0); SetF("welcomeOn", false); sf.ClientSize = new Size(1500, 860); Application.DoEvents(); var pmA = typeof(StudioForm).GetMethod("PaintAll", BF);
                using (var bm = new Bitmap(1500, 860)) using (var gg = Graphics.FromImage(bm)) { var pe = new PaintEventArgs(gg, new Rectangle(Point.Empty, bm.Size)); for (int i = 0; i < 2; i++) pmA.Invoke(sf, new object[] { pe }); if (outDir != null) bm.Save(Path.Combine(outDir, "all-plugins.png")); }
                var bmA = (Dictionary<string, RectangleF>)typeof(StudioForm).GetField("btnMap", BF).GetValue(sf); int adds = 0, pps = 0; foreach (var kv in bmA) { if (kv.Key.StartsWith("+ ADD")) adds++; if (kv.Key.StartsWith("PASTE PRESET")) pps++; }
                Check("ALL PLUGINS shows the tracks with + ADD and PASTE PRESET", adds >= 1 && pps >= 1, adds + " " + pps);
                HitReg openRow = null; foreach (object o in (System.Collections.IEnumerable)typeof(StudioForm).GetField("regs", BF).GetValue(sf)) { var rg = (HitReg)o; if (rg.OnDown != null && (int)rg.R.Width == 224 && (int)rg.R.Height == 26) openRow = rg; }
                if (openRow != null) openRow.OnDown(); Check("pressing a plugin in the panel opens it on its track (the panel closes)", openRow != null && !sf.allFxOpen && F<int>("selFx") >= 0);
                sf.allFxOpen = false; sf.eng.Tracks.Clear(); SetF("selFx", -1); SetF("revertAction", null);
            }
            {   // AUTO KEY: a pitch correction plugin that is added finds the key of its track by itself; one from a saved project keeps its key
                sf.eng.Tracks.Clear(); int sr = 44100, nS = sr * 12; var mel = new float[nS]; int[] notes = { 57, 60, 64, 62, 60, 59, 57, 64, 65, 64, 62, 60 };   // an A minor tune
                for (int i = 0; i < nS; i++) { int ni = (i / sr) % notes.Length; double hz = 440 * Math.Pow(2, (notes[ni] - 69) / 12.0); double env = Math.Min(1, (i % sr) / 2000.0) * Math.Min(1, (sr - i % sr) / 2000.0); mel[i] = (float)(0.4 * env * (Math.Sin(2 * Math.PI * hz * i / sr) + 0.3 * Math.Sin(4 * Math.PI * hz * i / sr))); }
                var vt = StudioEngine.NewTrack("Tune", "vocal", AudioClip.FromArrays(mel, (float[])mel.Clone(), "tune")); sf.eng.Tracks.Add(vt);
                var pcA = (PitchCorrector)FxRegistry.Create("pitch"); pcA.Prepare(sr); vt.Fx.Add(pcA); var mt = FxRegistry.Create("lib_melody_tune") ?? FxRegistry.Create("melody"); mt.Prepare(sr); vt.Fx.Add(mt);
                var kept = (PitchCorrector)FxRegistry.Create("pitch"); kept.Prepare(sr); kept.Set("key", 4); kept.AutoKeyPending = false; vt.Fx.Add(kept);
                Check("a new pitch correction plugin is waiting to find the key by itself", pcA.AutoKeyPending && mt.AutoKeyPending);
                SetF("tab", 0); SetF("welcomeOn", false); var end = DateTime.Now.AddSeconds(25); while (DateTime.Now < end && (pcA.AutoKeyPending || mt.AutoKeyPending)) { sf.AutoKeyTick(); Pump(150); }
                int k1 = (int)pcA.Get("key"), s1 = (int)pcA.Get("scale"); bool am = (k1 == 9 && s1 == 2) || (k1 == 0 && s1 == 1);
                Check("AUTO KEY sets the key it finds in the audio (an A minor tune: A minor, or its twin C major)", !pcA.AutoKeyPending && am, PitchCorrector.Keys[k1] + " " + PitchCorrector.Scales[s1]);
                Check("Melody Tune gets the same key", !mt.AutoKeyPending && (int)mt.Get("key") == k1 && (int)mt.Get("scale") == s1, mt.Get("key") + " " + mt.Get("scale"));
                Check("the notes of the scale follow the key", ((int)pcA.Get("mask") & 4095) == PitchCorrector.MaskFor(k1, s1));
                Check("a plugin from a saved project keeps the key it was saved with", (int)kept.Get("key") == 4);
                var pcB = (PitchCorrector)FxRegistry.Create("pitch"); pcB.Prepare(sr); pcB.AutoKeyPending = false; Check("a key picked by hand is never changed by AUTO KEY", !pcB.AutoKeyPending);
                sf.eng.Tracks.Clear(); SetF("toast", "");
            }
            {   // the welcome box lists every project in a row that scrolls, with the website's scroll bar (and no "tracks" on the cards)
                for (int i = 0; i < 7; i++) { string pp = Path.Combine(StudioEnv.DataDir, "wel" + i + ".ytmproj"); File.WriteAllText(pp, "YTMPROJ\nname|Song " + i + "\n"); ProjectLibrary.Touch(pp, "Song " + i, 3, 120, 100, true, null); }
                sf.eng.Tracks.Clear(); SetF("tab", 0); SetF("welcomeOn", true); SetF("welScroll", 0f); sf.ClientSize = new Size(1500, 860); Application.DoEvents(); var pmW = typeof(StudioForm).GetMethod("PaintAll", BF);
                Action paintW = () => { using (var bm = new Bitmap(1500, 860)) using (var gg = Graphics.FromImage(bm)) { var pe = new PaintEventArgs(gg, new Rectangle(Point.Empty, bm.Size)); StudioForm.TxtAudit = new List<StudioForm.AuditText>(); for (int i = 0; i < 2; i++) pmW.Invoke(sf, new object[] { pe }); if (outDir != null) bm.Save(Path.Combine(outDir, "welcome-scroll.png")); } };
                paintW(); var auW = StudioForm.TxtAudit; StudioForm.TxtAudit = null; bool tracksWord = auW.Any(t => t.S.Contains("tracks   EDITED")); bool seven = auW.Any(t => t.S.StartsWith("EDITED "));
                Check("the welcome cards do not say how many tracks", !tracksWord && seven);
                HitReg wheel = null, bar = null; foreach (object o in (System.Collections.IEnumerable)typeof(StudioForm).GetField("regs", BF).GetValue(sf)) { var rg = (HitReg)o; if (rg.OnWheel != null && rg.OnDown == null && rg.R.Height > 150 && rg.R.Height < 300) wheel = rg; if (rg.OnDrag != null && (int)rg.R.Height == 28 && rg.R.Width > 300) bar = rg; }
                Check("with more than 4 projects there is a scroll bar under them", wheel != null && bar != null);
                if (wheel != null) wheel.OnWheel(-1); float s1 = F<float>("welScroll"); Check("the mouse wheel moves the row to the next project", s1 > 50, s1.ToString());
                if (bar != null) { typeof(StudioForm).GetField("MousePos", BF).SetValue(sf, new PointF(bar.R.Right - 2, bar.R.Y + 5)); bar.OnDown(); bar.OnDrag(new PointF(bar.R.Right + 200, bar.R.Y + 5)); if (bar.OnUp != null) bar.OnUp(); }
                paintW(); float s2 = F<float>("welScroll"); Check("dragging the bar to the end shows the last projects", s2 > s1, s1 + " -> " + s2);
                SetF("welcomeOn", false); SetF("welScroll", 0f);
            }
            {   // dragging the needle to the left edge scrolls the timeline back with it (it never hides behind the track boxes)
                sf.eng.Tracks.Clear(); sf.eng.Tracks.Add(StudioEngine.NewTrack("Long", "beat", AudioClip.FromArrays(new float[44100 * 60], new float[44100 * 60], "l"))); SetF("tab", 0); SetF("welcomeOn", false); SetF("autoFit", false); SetF("spp", 300.0); SetF("scroll", (long)(44100 * 30)); sf.ClientSize = new Size(1500, 860); Application.DoEvents();
                var pmN = typeof(StudioForm).GetMethod("PaintAll", BF); Action paintN = () => { using (var bm = new Bitmap(1500, 860)) using (var gg = Graphics.FromImage(bm)) { var pe = new PaintEventArgs(gg, new Rectangle(Point.Empty, bm.Size)); pmN.Invoke(sf, new object[] { pe }); } };
                paintN(); int hw = Convert.ToInt32(typeof(StudioForm).GetField("HeadFullW", BF | BindingFlags.Static).GetValue(null)) + StudioForm.VBarW, ry = 70 + 86 + 26 + 14;   // the ruler sits under the hint strip
                Action<string, MouseButtons, int, int> ev = (m, b, x, y) => typeof(StudioForm).GetMethod(m, BF | BindingFlags.DeclaredOnly).Invoke(sf, new object[] { new MouseEventArgs(b, b == MouseButtons.None ? 0 : 1, x, y, 0) });
                ev("OnMouseMove", MouseButtons.None, hw + 300, ry); ev("OnMouseDown", MouseButtons.Left, hw + 300, ry); ev("OnMouseMove", MouseButtons.Left, hw - 120, ry);
                long sc0 = 44100 * 30; for (int i = 0; i < 25; i++) { paintN(); Pump(20); }
                long sc1 = F<long>("scroll"), pos = sf.eng.Pos; ev("OnMouseUp", MouseButtons.Left, hw - 120, ry);
                Check("holding the needle past the left edge scrolls the timeline back", sc1 < sc0, sc0 + " -> " + sc1);
                Check("and the needle stays in view, at the left edge (not behind the track boxes)", pos >= sc1 && pos <= sc1 + (long)(60 * 300.0), pos + " vs view from " + sc1);
                sf.eng.Tracks.Clear(); SetF("autoFit", true);
            }
            {   // moving a clip is free: it goes exactly as far as you drag, between the grid lines too (the grid is on)
                sf.eng.Tracks.Clear(); var mv = StudioEngine.NewTrack("Move me", "vocal", AudioClip.FromArrays(new float[44100 * 4], new float[44100 * 4], "m")); mv.Offset = 44100 * 2; sf.eng.Tracks.Add(mv); sf.eng.Tracks.Add(StudioEngine.NewTrack("Other", "beat", AudioClip.FromArrays(new float[44100 * 20], new float[44100 * 20], "o")));
                SetF("tab", 0); SetF("welcomeOn", false); SetF("autoFit", false); SetF("spp", 400.0); SetF("scroll", 0L); SetF("snapOn", true); SetF("selTrack", 0); sf.ClientSize = new Size(1500, 860); Application.DoEvents();
                var pmM = typeof(StudioForm).GetMethod("PaintAll", BF); using (var bm = new Bitmap(1500, 860)) using (var gg = Graphics.FromImage(bm)) { var pe = new PaintEventArgs(gg, new Rectangle(Point.Empty, bm.Size)); pmM.Invoke(sf, new object[] { pe }); }
                int hwM = Convert.ToInt32(typeof(StudioForm).GetField("HeadFullW", BF | BindingFlags.Static).GetValue(null)) + StudioForm.VBarW; float topM = (float)typeof(StudioForm).GetProperty("ArrTop", BF).GetValue(sf, null) + 28; float laneH = Convert.ToSingle(typeof(StudioForm).GetProperty("LaneH", BF) != null ? typeof(StudioForm).GetProperty("LaneH", BF).GetValue(sf, null) : typeof(StudioForm).GetField("LaneH", BF).GetValue(sf));
                int cx = hwM + (int)(44100 * 4 / 400.0), cyM = (int)(topM + laneH / 2);
                Action<string, MouseButtons, int, int> evM = (m, b, x, y) => typeof(StudioForm).GetMethod(m, BF | BindingFlags.DeclaredOnly).Invoke(sf, new object[] { new MouseEventArgs(b, b == MouseButtons.None ? 0 : 1, x, y, 0) });
                long o0 = mv.Offset; evM("OnMouseMove", MouseButtons.None, cx, cyM); evM("OnMouseDown", MouseButtons.Left, cx, cyM); evM("OnMouseMove", MouseButtons.Left, cx + 7, cyM); if (outDir != null) { evM("OnMouseMove", MouseButtons.Left, cx + 60, cyM); using (var bm2 = new Bitmap(1500, 860)) using (var g2 = Graphics.FromImage(bm2)) { var pe2 = new PaintEventArgs(g2, new Rectangle(Point.Empty, bm2.Size)); for (int q = 0; q < 2; q++) pmM.Invoke(sf, new object[] { pe2 }); bm2.Save(Path.Combine(outDir, "drag-clip.png")); } evM("OnMouseMove", MouseButtons.Left, cx + 7, cyM); } evM("OnMouseUp", MouseButtons.Left, cx + 7, cyM);
                Check("a clip dragged 7 pixels moves exactly 7 pixels (it does not jump to a grid line)", mv.Offset - o0 == (long)(7 * 400.0), (mv.Offset - o0) + " frames (7 px = " + (long)(7 * 400.0) + ")");
                sf.eng.Tracks.Clear(); SetF("autoFit", true);
            }
            {   // DUPLICATE on a piece copies that piece only, onto a new track right under its track (same time, the track's plugins)
                sf.eng.Tracks.Clear(); var big = StudioEngine.NewTrack("Vocals", "vocal", AudioClip.FromArrays(new float[44100 * 30], new float[44100 * 30], "v")); big.ClipFrames = 44100 * 10; var eq0 = FxRegistry.Create("eq"); eq0.Prepare(44100); big.Fx.Add(eq0);
                var small = StudioEngine.NewTrack("Vocals (2)", "vocal", big.Clip); small.Host = big; small.Offset = 44100 * 12; small.ClipStart = 44100 * 12; small.ClipFrames = 44100 * 2;
                var beat = StudioEngine.NewTrack("Beat", "beat", AudioClip.FromArrays(new float[44100 * 30], new float[44100 * 30], "b"));
                sf.eng.Tracks.Add(big); sf.eng.Tracks.Add(beat); sf.eng.Tracks.Add(small);
                sf.DuplicatePiece(small); var dup = sf.eng.Tracks[1];
                Check("the copy is a new track right under the track of the piece", dup != beat && dup.Host == null && sf.eng.Tracks[2] == beat, dup.Name);
                Check("it holds exactly that piece (same audio, same time, same length), not the whole track", dup.Clip == small.Clip && dup.Offset == small.Offset && dup.ClipStart == small.ClipStart && dup.UsedLength == small.UsedLength, dup.Offset + " / " + dup.UsedLength);
                Check("it has the track's plugins", dup.Fx.Count == 1 && dup.Fx[0].Type == "eq");
                Check("the original track and its pieces are untouched", big.UsedLength == 44100 * 10 && small.Host == big && sf.eng.Tracks.Contains(small));
                sf.DuplicatePiece(small); Check("pressing it again makes one more copy of that same piece", sf.eng.Tracks[1].UsedLength == small.UsedLength && sf.eng.Tracks[2].UsedLength == small.UsedLength);
                sf.eng.Tracks.Clear(); SetF("revertAction", null);
            }
            {   // ADD MUSIC can add an empty vocal track; an empty track is never deleted by itself
                sf.eng.Tracks.Clear(); sf.eng.Tracks.Add(StudioEngine.NewTrack("Beat", "beat", AudioClip.FromArrays(new float[44100 * 5], new float[44100 * 5], "b"))); SetF("tab", 0); SetF("welcomeOn", false);
                var ev2 = sf.AddEmptyTrack("vocal"); var fxE = FxRegistry.Create("eq"); fxE.Prepare(44100); ev2.Fx.Add(fxE); ev2.EverHadClip = true;
                for (int i = 0; i < 4; i++) { sf.Invalidate(); sf.Update(); Pump(400); }
                Check("ADD MUSIC can add an empty vocal track (no music)", ev2.Role == "vocal" && ev2.Clip == null && ev2.Name.StartsWith("Vocal"), ev2.Name);
                Check("an empty track stays (it is never deleted by itself, even with plugins on)", sf.eng.Tracks.Contains(ev2));
                if (outDir != null) Shot(Path.Combine(outDir, "empty-vocal-track.png"));
                sf.eng.Tracks.Clear(); SetF("revertAction", null);
            }
            {   // the visualiser hits on every kick (one BANG per kick) and the bars fall right down between hits, also on a loud steady mix
                int srV = 44100; double secsV = 6; int nV = (int)(srV * secsV); var sigV = new float[nV]; var rndV = new Random(3);
                for (int i = 0; i < nV; i++) { double t = i / (double)srV, kt = t % 0.5; double kick = kt < 0.25 ? Math.Sin(2 * Math.PI * (55 + 90 * Math.Exp(-kt * 30)) * kt) * Math.Exp(-kt * 14) : 0; double pad = 0.35 * Math.Sin(2 * Math.PI * 220 * t) + 0.25 * Math.Sin(2 * Math.PI * 330 * t) + 0.15 * (rndV.NextDouble() - 0.5); sigV[i] = (float)(0.55 * kick + 0.5 * pad); }
                sf.eng.Tracks.Clear(); sf.visTestTap = new float[8192]; var paintV = typeof(StudioForm).GetMethod("PaintVisStage", BF); var hdtV = typeof(StudioForm).GetField("hDt", BF); var visBV = (float[])typeof(StudioForm).GetField("visB", BF).GetValue(sf); var beatAtV = typeof(StudioForm).GetField("visBeatAt", BF);
                int beatsV = 0; double lastV = (double)beatAtV.GetValue(sf); float bMinV = 1, bMaxV = 0;
                using (var bmV = new Bitmap(900, 600)) using (var gV = Graphics.FromImage(bmV)) for (int f = 0; f < (int)(secsV * 60); f++)
                {
                    int endV = (int)((f + 1) / 60.0 * srV); for (int k = 0; k < 8192; k++) { int si = endV - 8192 + k; sf.visTestTap[k] = si >= 0 && si < nV ? sigV[si] : 0; }
                    hdtV.SetValue(sf, 1f / 60f); paintV.Invoke(sf, new object[] { gV, new RectangleF(0, 0, 900, 600), true });
                    double ba = (double)beatAtV.GetValue(sf); if (ba != lastV) { if (f > 30) beatsV++; lastV = ba; }
                    if (f > 60) { float bs = 0; for (int i = 0; i < 14; i++) bs += visBV[i]; bs /= 14; bMinV = Math.Min(bMinV, bs); bMaxV = Math.Max(bMaxV, bs); }
                }
                sf.visTestTap = null;
                Check("the visualiser goes BANG on every kick, once per kick (a loud steady pad under it)", beatsV >= 9 && beatsV <= 11, beatsV + " BANGs for 10 kicks");
                // MOTION: CALM moves far less from frame to frame than WILD, shakes nothing, and still hits on the beat; hi-hats between the kicks never add a BANG
                Func<int, bool, float[]> runV = (mode, hats) => { int keepM = Prefs.VisMotion; Prefs.VisMotion = mode; var sig2 = (float[])sigV.Clone(); if (hats) for (int i = 0; i < nV; i++) { double t = i / (double)srV, ht = (t + 0.25) % 0.5; if (ht < 0.03) sig2[i] += (float)(0.5 * (rndV.NextDouble() - 0.5) * Math.Exp(-ht * 120)); double st = (t + 0.25) % 1.0; if (st < 0.12) sig2[i] += (float)(0.45 * (rndV.NextDouble() - 0.5) * Math.Exp(-st * 30) + 0.3 * Math.Sin(2 * Math.PI * 190 * t) * Math.Exp(-st * 25)); }
                    sf.visTestTap = new float[8192]; typeof(StudioForm).GetField("visKicks", BF).GetValue(sf).GetType().GetMethod("Clear").Invoke(typeof(StudioForm).GetField("visKicks", BF).GetValue(sf), null); float jit = 0, shakeMax = 0; int bangs = 0; double last = (double)beatAtV.GetValue(sf); var prev = new float[visBV.Length];
                    using (var bm2 = new Bitmap(900, 600)) using (var g2 = Graphics.FromImage(bm2)) for (int f = 0; f < (int)(secsV * 60); f++) { int endV = (int)((f + 1) / 60.0 * srV); for (int k = 0; k < 8192; k++) { int si = endV - 8192 + k; sf.visTestTap[k] = si >= 0 && si < nV ? sig2[si] : 0; } hdtV.SetValue(sf, 1f / 60f); paintV.Invoke(sf, new object[] { g2, new RectangleF(0, 0, 900, 600), true });
                        double ba = (double)beatAtV.GetValue(sf); if (ba != last) { if (f > 30) bangs++; last = ba; } if (f > 60) { for (int i = 0; i < visBV.Length; i++) jit += Math.Abs(visBV[i] - prev[i]); shakeMax = Math.Max(shakeMax, (float)typeof(StudioForm).GetField("visShake", BF).GetValue(sf)); } Array.Copy(visBV, prev, prev.Length); }
                    sf.visTestTap = null; Prefs.VisMotion = keepM; return new[] { jit, shakeMax, bangs }; };
                var calmV = runV(0, false); var wildV = runV(3, false); var smoothHats = runV(1, true);
                Check("MOTION CALM moves far less than WILD (smooth bars, no shaking)", calmV[0] < wildV[0] * 0.6f && calmV[1] < 0.01f && wildV[1] > 0.5f, "movement " + calmV[0].ToString("0") + " vs " + wildV[0].ToString("0") + ", shake " + calmV[1].ToString("0.00") + " vs " + wildV[1].ToString("0.00"));
                Check("CALM still hits on the beat", calmV[2] >= 8 && calmV[2] <= 11, calmV[2] + " BANGs for 10 kicks");
                Check("SMOOTH goes BANG on the beat only: hi-hats and snares between the kicks add none", smoothHats[2] >= 8 && smoothHats[2] <= 11, smoothHats[2] + " BANGs for 10 kicks (with hats and snares)");
                Check("MOTION is a saved setting", Prefs.Lines().Any(l9 => l9.StartsWith("vismotion=")), "");
                Check("between the kicks the bass bars fall right down, and on a kick they hit the top", bMinV < 0.2f && bMaxV > 0.9f, bMinV.ToString("0.00") + " .. " + bMaxV.ToString("0.00"));
            }
            {   // HOME on My Projects; AUTO MASTERING / LOUDNESS / EQ listen to the mix once and keep it until a track changes
                SetF("tab", 3); sf.Invalidate(); sf.Update(); Pump(200); var bmH = (Dictionary<string, RectangleF>)typeof(StudioForm).GetField("btnMap", BF).GetValue(sf); Check("My Projects has a HOME button", bmH.ContainsKey("HOME")); SetF("tab", 0);
                sf.eng.Tracks.Clear(); var toneF = new float[44100 * 100]; for (int i = 0; i < toneF.Length; i++) toneF[i] = (float)(0.3 * Math.Sin(i * 0.03)); var tF = StudioEngine.NewTrack("Tone", "vocal", AudioClip.FromArrays(toneF, (float[])toneF.Clone(), "f")); sf.eng.Tracks.Add(tF);
                float[] l1, r1, l2, r2; var swF = System.Diagnostics.Stopwatch.StartNew(); sf.SampleMix(out l1, out r1); long first = swF.ElapsedMilliseconds; swF.Restart(); sf.SampleMix(out l2, out r2); long again = swF.ElapsedMilliseconds;
                Check("the mix is listened to in slices (a 100 s song: 60 s of it) and kept: the second time is instant", l1.Length == 44100 * 60 && ReferenceEquals(l1, l2) && again < 20, l1.Length / 44100 + " s, " + first + " ms then " + again + " ms");
                tF.VolDb = -3; sf.SampleMix(out l2, out r2); Check("changing a track listens again", !ReferenceEquals(l1, l2));
                sf.eng.Tracks.Clear();
            }
            {   // a row whose own clip moved away but that still has pieces on it is never drawn as EMPTY TRACK (the label lay over the pieces)
                sf.eng.Tracks.Clear(); var hostE = StudioEngine.NewTrack("Vocals", "vocal", null); var pE = StudioEngine.NewTrack("Vocals (2)", "vocal", AudioClip.FromArrays(new float[44100 * 3], new float[44100 * 3], "p")); pE.Host = hostE; pE.Offset = 44100;
                var emptyE = StudioEngine.NewTrack("Nothing", "audio", null); sf.eng.Tracks.Add(hostE); sf.eng.Tracks.Add(emptyE); sf.eng.Tracks.Add(pE); SetF("tab", 0); SetF("welcomeOn", false);
                var elE = (System.Collections.IDictionary)typeof(StudioForm).GetField("emptyLanes", BF).GetValue(sf); elE.Clear(); sf.Invalidate(); sf.Update(); Pump(200);
                if (outDir != null) Shot(Path.Combine(outDir, "pieces-on-empty-host.png"));
                Check("a row with pieces on it is not drawn as EMPTY TRACK, even when its own clip is gone", !elE.Contains(hostE));
                Check("a row with nothing on it still says EMPTY TRACK", elE.Contains(emptyE));
                sf.eng.Tracks.Clear();
            }
            {   // SPLIT VOCALS + BEAT: the vocals and the instrumental take the song's place and the original track is removed (Ctrl+Z brings it back)
                sf.eng.Tracks.Clear(); Func<AudioClip> clipS = () => AudioClip.FromArrays(new float[44100 * 4], new float[44100 * 4], "s");
                var song = StudioEngine.NewTrack("My Song", "song", clipS()); song.Offset = 44100; var other = StudioEngine.NewTrack("Other", "beat", clipS()); sf.eng.Tracks.Add(other); sf.eng.Tracks.Add(song);
                sf.FinishSeparation(song, clipS(), clipS(), song.Offset);
                Check("after the split the original track is gone", !sf.eng.Tracks.Contains(song), string.Join(", ", sf.eng.Tracks.Select(t => t.Name).ToArray()));
                Check("the beat is always the first track and the vocals come right under it, in sync", sf.eng.Tracks.Count == 3 && sf.eng.Tracks[0].Name == "My Song - Instrumental" && sf.eng.Tracks[1].Name == "My Song - Vocals" && sf.eng.Tracks[2] == other && sf.eng.Tracks[0].Offset == 44100 && sf.eng.Tracks[1].Offset == 44100, string.Join(", ", sf.eng.Tracks.Select(t => t.Name).ToArray()));
                typeof(StudioForm).GetMethod("DoUndo", BF, null, Type.EmptyTypes, null).Invoke(sf, null); Check("Ctrl+Z brings the original back", sf.eng.Tracks.Exists(t => t.Name == "My Song"));
                sf.eng.Tracks.Clear(); var host2 = StudioEngine.NewTrack("Row", "song", clipS()); var piece2 = StudioEngine.NewTrack("Row (2)", "song", clipS()); piece2.Host = host2; piece2.Offset = 44100 * 6; sf.eng.Tracks.Add(host2); sf.eng.Tracks.Add(piece2);
                sf.FinishSeparation(host2, clipS(), clipS(), 0); Check("splitting a track that has other pieces removes only its own audio (the pieces stay)", sf.eng.Tracks.Contains(piece2) && host2.Clip == null);
                sf.eng.Tracks.Clear(); SetF("revertAction", null);
                {   // SPLIT INTO 4 STEMS: drums, bass and other first, the vocals under them, in sync, the original gone (Ctrl+Z brings it back)
                    var s4 = StudioEngine.NewTrack("Tune", "song", clipS()); s4.Offset = 44100 * 2; var keep = StudioEngine.NewTrack("Keep", "beat", clipS()); sf.eng.Tracks.Add(keep); sf.eng.Tracks.Add(s4);
                    sf.FinishSeparation4(s4, new[] { clipS(), clipS(), clipS(), clipS() }, s4.Offset); var nm = sf.eng.Tracks.Select(t => t.Name).ToArray();
                    Check("after a 4-stem split: Drums, Bass, Other, Vocals take the song's place, in sync, the original is gone", !sf.eng.Tracks.Contains(s4) && sf.eng.Tracks.Count == 5 && nm[0] == "Tune - Drums" && nm[1] == "Tune - Bass" && nm[2] == "Tune - Other" && nm[3] == "Tune - Vocals" && sf.eng.Tracks[4] == keep && sf.eng.Tracks.Take(4).All(t => t.Offset == 44100 * 2) && sf.eng.Tracks[3].Role == "vocal", string.Join(", ", nm));
                    typeof(StudioForm).GetMethod("DoUndo", BF, null, Type.EmptyTypes, null).Invoke(sf, null); Check("Ctrl+Z brings the original back after a 4-stem split", sf.eng.Tracks.Exists(t => t.Name == "Tune"));
                    sf.eng.Tracks.Clear(); SetF("revertAction", null);
                }
            }
            {   // a vocal track's plugins never touch the beat on its own track; the plugin panel says clearly whose plugins they are
                Func<bool, float> beatPeak = (vocFx) => { var e = new StudioEngine(); int nS = 44100; var one = new float[nS]; for (int i = 0; i < nS; i++) one[i] = 0.5f;
                    var voc = StudioEngine.NewTrack("aheath - Song - Vocals", "vocal", AudioClip.FromArrays(new float[nS], new float[nS], "v")); if (vocFx) { var gx = FxRegistry.Create("gain"); gx.Prepare(44100); gx.Set("gain", -24); voc.Fx.Add(gx); }
                    var bt = StudioEngine.NewTrack("aheath - Song - Instrumental", "beat", AudioClip.FromArrays(one, (float[])one.Clone(), "b")); e.Tracks.Add(voc); e.Tracks.Add(bt);
                    float[] L, R; e.Render(0, nS, new RenderOpts { Master = false }, null, out L, out R); float pk = 0; for (int i = 0; i < L.Length; i++) pk = Math.Max(pk, Math.Abs(L[i])); e.Shutdown(); return pk; };
                float without = beatPeak(false), with = beatPeak(true); Check("a plugin on the vocal track does not change the beat on its own track", Math.Abs(without - with) < 1e-4f, without + " / " + with);
                sf.eng.Tracks.Clear(); sf.eng.Tracks.Add(StudioEngine.NewTrack("aheath - Song - Vocals", "vocal", AudioClip.FromArrays(new float[44100], new float[44100], "v"))); sf.eng.Tracks.Add(StudioEngine.NewTrack("aheath - Song - Instrumental", "beat", AudioClip.FromArrays(new float[44100], new float[44100], "b")));
                SetF("tab", 0); SetF("welcomeOn", false); SetF("selTrack", 1); StudioForm.TxtAudit = new List<StudioForm.AuditText>(); sf.Invalidate(); sf.Update(); Pump(150); var auT = StudioForm.TxtAudit; StudioForm.TxtAudit = null;
                if (outDir != null) Shot(Path.Combine(outDir, "plugins-title.png"));
                Check("the plugin panel says which track: TRACK 2 BEAT and the track's own name", auT.Any(t => t.S == "TRACK 2  BEAT") && auT.Any(t => t.S == "aheath - Song - Instrumental"), string.Join(" | ", auT.Where(t => t.S.StartsWith("TRACK")).Select(t => t.S).ToArray()));
                sf.eng.Tracks.Clear();
            }
            {   // the theme you chose is never lost when the edition is briefly not known (a licence being renewed): it comes back by itself; and it is what the website gets
                string keepT = Prefs.Theme; int gold = Themes.IndexOf("Royal Gold"); Themes.Apply(gold); Check("(Royal Gold is on, on Fantasy)", Themes.Current.Name == "Royal Gold");
                Edition.Override = Tier.Free; Themes.EnforceEdition(); bool showsOriginal = Themes.Current.Name == "Purphex"; bool kept = Prefs.Theme == "Royal Gold";
                Edition.Override = Tier.Fantasy; Themes.EnforceEdition(); bool back = Themes.Current.Name == "Royal Gold";
                Check("while the edition reads Free the original theme is shown, but your choice is kept", showsOriginal && kept, Themes.Current.Name + " / " + Prefs.Theme);
                Check("and when Fantasy is known again your theme is back by itself", back, Themes.Current.Name);
                string body = SiteTheme.Body(Themes.Current); Check("the website is sent the theme's name, its edition and its colours", body.Contains("\"name\":\"Royal Gold\"") && body.Contains("\"req\":\"fantasy\"") && System.Text.RegularExpressions.Regex.IsMatch(body, "\"violet\":\"#[0-9a-f]{6}\""), body);
                Edition.Override = Tier.Free; Themes.EnforceEdition(false); bool noFlash = Themes.Current.Name == "Royal Gold"; Edition.Override = Tier.Fantasy; Check("at start on a linked PC (edition still being checked) the theme stays as it is: no flash to the original", noFlash, Themes.Current.Name);
                Themes.Apply(Themes.IndexOf(keepT));
            }
            {   // EDIT on a bus: its effects show in the plugin panel (open, switch off, remove), BACK TO TRACKS goes back; REMOVE ALL is not in the YOUR PLUGINS box any more
                sf.eng.Tracks.Clear(); var vB = StudioEngine.NewTrack("Vox", "vocal", AudioClip.FromArrays(new float[44100], new float[44100], "v")); var fxV = FxRegistry.Create("eq"); fxV.Prepare(44100); vB.Fx.Add(fxV); sf.eng.Tracks.Add(vB);
                var bus = StudioEngine.NewTrack("Bus 1", "bus", null); bus.IsBus = true; var r1 = FxRegistry.Create("reverb"); r1.Prepare(44100); var d1 = FxRegistry.Create("delay"); d1.Prepare(44100); bus.Fx.Add(r1); bus.Fx.Add(d1); sf.eng.Tracks.Add(bus);
                SetF("tab", 0); SetF("welcomeOn", false); SetF("selTrack", 0); SetF("selFx", -1); sf.ClientSize = new Size(1500, 860); Application.DoEvents(); var pmB = typeof(StudioForm).GetMethod("PaintAll", BF);
                Func<List<StudioForm.AuditText>> paintB = () => { StudioForm.TxtAudit = new List<StudioForm.AuditText>(); using (var bm = new Bitmap(1500, 860)) using (var gg = Graphics.FromImage(bm)) { var pe = new PaintEventArgs(gg, new Rectangle(Point.Empty, bm.Size)); for (int i = 0; i < 2; i++) pmB.Invoke(sf, new object[] { pe }); } var res = StudioForm.TxtAudit; StudioForm.TxtAudit = null; return res; };
                paintB(); var bmB = (Dictionary<string, RectangleF>)typeof(StudioForm).GetField("btnMap", BF).GetValue(sf); Check("REMOVE ALL is not in the YOUR PLUGINS box any more (it is in PRESETS...)", !bmB.ContainsKey("REMOVE ALL"));
                sf.EditBus(bus); var auB = paintB(); if (outDir != null) Shot(Path.Combine(outDir, "edit-bus.png"));
                Check("EDIT on a bus shows its effects in the plugin panel", auB.Any(t => t.S == "EDITING BUS") && auB.Any(t => t.S == "Reverb") && auB.Any(t => t.S == "Delay"), string.Join(" | ", auB.Where(t => t.S.StartsWith("EDIT") || t.S == "Reverb").Select(t => t.S).ToArray()));
                bmB = (Dictionary<string, RectangleF>)typeof(StudioForm).GetField("btnMap", BF).GetValue(sf); Check("with a BACK TO TRACKS button (and no BASS LAB on a bus)", bmB.ContainsKey("BACK TO TRACKS") && !bmB.ContainsKey("Bass Lab"));
                HitReg xB = null; foreach (object o in (System.Collections.IEnumerable)typeof(StudioForm).GetField("regs", BF).GetValue(sf)) { var rg = (HitReg)o; if (rg.OnDown != null && (int)rg.R.Width == 22 && (int)rg.R.Height == 22 && rg.R.X > 1440 && rg.R.Y < 500 && (xB == null || rg.R.Y < xB.R.Y)) xB = rg; }   /* the x of the bus's first effect (the rightmost small button of its row) */
                if (xB != null) xB.OnDown(); Check("an effect of the bus can be removed there", bus.Fx.Count == 1, bus.Fx.Count + " left");
                bmB["BACK TO TRACKS"].ToString(); typeof(StudioForm).GetField("busEdit", BF).SetValue(sf, null); var auC = paintB(); Check("BACK TO TRACKS shows the picked track again", auC.Any(t => t.S.StartsWith("TRACK 1")));
                sf.eng.Tracks.Clear(); SetF("selFx", -1);
            }
            {   // several picked clips can be dragged to other tracks together (they all move the same number of rows, keeping their times, fades and gain)
                sf.eng.Tracks.Clear(); Func<string, long, Track> mkG = (nm, off) => { var t = StudioEngine.NewTrack(nm, "vocal", AudioClip.FromArrays(new float[44100 * 2], new float[44100 * 2], nm)); t.Offset = off; return t; };
                var gA = mkG("A", 0); var gB = mkG("B", 44100 * 3); gB.FadeIn = 4410; gB.ClipGainDb = -3; var gOther = mkG("Other", 0); sf.eng.Tracks.Add(gA); sf.eng.Tracks.Add(gB); sf.eng.Tracks.Add(gOther);
                var ms = (List<Track>)typeof(StudioForm).GetField("multiSel", BF).GetValue(sf); ms.Clear(); ms.Add(gA); ms.Add(gB);
                sf.GroupToRows(2); int lanes = (int)typeof(StudioForm).GetProperty("LaneCount", BF).GetValue(sf, null);
                var onRow2 = sf.eng.Tracks.FindAll(t => t.Clip != null && t.Offset == 0 && (t.Host ?? t) == sf.eng.Tracks[2] && t.Name == "A"); var row3 = lanes > 3 ? sf.eng.Tracks[3] : null;
                Check("two picked clips dragged 2 tracks down both move 2 tracks down (one onto a track that has audio, one onto a new track)", onRow2.Count == 1 && row3 != null && row3.Offset == 44100 * 3 && row3.UsedLength == 44100 * 2, "lanes " + lanes + ", A on row 3: " + onRow2.Count + ", row 4 " + (row3 == null ? "none" : row3.Name + " @" + row3.Offset));
                Check("their old rows are empty now, and the clips keep their fades and gain", gA.Clip == null && gB.Clip == null && row3 != null && row3.FadeIn == 4410 && row3.ClipGainDb == -3);
                Check("they stay picked as a group after the move", ms.Count == 2);
                ms.Clear(); ms.Add(sf.eng.Tracks[2]); ms.Add(row3); int before = sf.eng.Tracks.Count; sf.GroupToRows(-5); Check("a group cannot go above the first track (nothing moves)", sf.eng.Tracks.Count == before && row3.Clip != null);
                ms.Clear(); sf.eng.Tracks.Clear(); SetF("revertAction", null);
            }
            {   // ALL PLUGINS edits the buses too: picking a bus column edits it in the plugin panel, TRACKS INTO THIS BUS routes a track (with its pieces)
                sf.eng.Tracks.Clear(); var bt1 = StudioEngine.NewTrack("Vox", "vocal", AudioClip.FromArrays(new float[44100], new float[44100], "a")); var bp1 = StudioEngine.NewTrack("Vox (2)", "vocal", AudioClip.FromArrays(new float[44100], new float[44100], "a")); bp1.Host = bt1; sf.eng.Tracks.Add(bt1); sf.eng.Tracks.Add(bp1);
                var bb = (Track)typeof(StudioForm).GetMethod("AddBus", BF).Invoke(sf, null); sf.PickAllFxColumn(bb, sf.eng.Tracks.IndexOf(bb)); Check("picking a bus in ALL PLUGINS edits that bus in the plugin panel", sf.busEdit == bb, "");
                sf.SetTrackBus(bt1, bb); Check("TRACKS INTO THIS BUS sends the track and its pieces through the bus", bt1.Out == bb && bp1.Out == bb, ""); sf.SetTrackBus(bt1, null); Check("and back to the master", bt1.Out == null && bp1.Out == null, "");
                sf.PickAllFxColumn(bt1, 0); Check("picking a track goes back to tracks", sf.busEdit == null, ""); sf.eng.Tracks.Clear(); SetF("revertAction", null);
            }
            {   // COPY ALL PLUGINS / PASTE ALL PLUGINS: every plugin of one track (settings and preset boxes) onto another; the plugins already there stay
                sf.eng.Tracks.Clear(); var cA = StudioEngine.NewTrack("Vox A", "vocal", AudioClip.FromArrays(new float[44100], new float[44100], "a")); var cB = StudioEngine.NewTrack("Vox B", "vocal", AudioClip.FromArrays(new float[44100], new float[44100], "b")); sf.eng.Tracks.Add(cA); sf.eng.Tracks.Add(cB);
                var pcA = new List<Fx>(); var eqA = FxRegistry.Create("eq"); var cmA = FxRegistry.Create("comp"); cmA.Set("ratio", 7); pcA.Add(eqA); pcA.Add(cmA); foreach (var f in pcA) f.Prepare(44100); typeof(StudioForm).GetMethod("PutPreset", BF).Invoke(sf, new object[] { cA, "Cloud Rap Haze", pcA });
                var dlA = FxRegistry.Create("delay"); dlA.Prepare(44100); dlA.Bypass = true; cA.Fx.Add(dlA); var gB = FxRegistry.Create("gain"); gB.Prepare(44100); cB.Fx.Add(gB);
                sf.CopyAllFx(cA); sf.PasteAllFx(cB);
                Check("PASTE ALL PLUGINS puts every plugin of the other track on this one, and the plugin it had stays", cB.Fx.Count == 4 && cB.Fx.Contains(gB), cB.Fx.Count + ": " + string.Join(", ", cB.Fx.Select(f => f.Type).ToArray()));
                var prB = cB.Fx.FindAll(f => f.Preset == "Cloud Rap Haze"); Check("the preset comes over in a box of its own, with its settings", prB.Count == 2 && Math.Abs(prB[1].Get("ratio") - 7) < 0.01 && prB[0].PresetId != eqA.PresetId && prB[0].PresetId == prB[1].PresetId);
                var dlB = cB.Fx.Find(f => f.Type == "delay"); Check("a switched-off plugin stays switched off, and the copies are new plugins (changing them leaves the first track alone)", dlB != null && dlB.Bypass && !cA.Fx.Contains(dlB) && !cA.Fx.Contains(prB[0]));
                sf.eng.Tracks.Clear(); SetF("revertAction", null);
            }
            {   // the REVERT box stays at the bottom left and is no wider than the left column of the Mastering page (it never reaches into the next box)
                var pm = typeof(StudioForm).GetMethod("PaintAll", BF); var rrF = typeof(StudioForm).GetField("revertRect", BF); bool narrow = true; string bad = "";
                foreach (var szz in new[] { new Size(1366, 768), new Size(1240, 680), new Size(1700, 1000) })
                {
                    sf.ClientSize = szz; Application.DoEvents(); typeof(StudioForm).GetField("tab", BF).SetValue(sf, 1); typeof(StudioForm).GetMethod("OfferRevert", BF).Invoke(sf, new object[] { "Vocal Forward is on your master", (Action)(() => { }) });
                    using (var bm = new Bitmap(szz.Width, szz.Height)) using (var gg = Graphics.FromImage(bm)) { var pe = new PaintEventArgs(gg, new Rectangle(Point.Empty, szz)); for (int i = 0; i < 3; i++) pm.Invoke(sf, new object[] { pe }); }
                    var rr = (RectangleF)rrF.GetValue(sf); if (rr.IsEmpty || rr.Width > 262 || rr.Right > 290 || rr.Bottom > szz.Height || rr.X > 20) { narrow = false; bad += " " + szz.Width + ":" + rr; }
                    typeof(StudioForm).GetField("revertAction", BF).SetValue(sf, null);
                }
                Check("the REVERT box is narrow: it stays inside the left column at the bottom left", narrow, bad);
            }
            {   // the picture of a project is cut to its content, fitted by itself and can be zoomed and moved like the profile picture
                string pw = Path.Combine(StudioEnv.DataDir, "pictest.ytmp"); File.WriteAllText(pw, "PURPHEX\nname|Pic\n"); ProjectLibrary.Touch(pw, "Pic", 2, 120, 60, true, null);
                string wide = Path.Combine(StudioEnv.DataDir, "wide.png"); using (var b0 = new Bitmap(1200, 600)) { using (var g0 = Graphics.FromImage(b0)) { g0.Clear(Color.Black); using (var br = new SolidBrush(Color.FromArgb(230, 255, 40, 60))) g0.FillEllipse(br, 480, 220, 240, 160); } b0.Save(wide, System.Drawing.Imaging.ImageFormat.Png); }
                string perr; bool pok = ProjectLibrary.SetCover(pw, wide, out perr); int pz, px, py; ProjectLibrary.LoadAdj(pw, out pz, out px, out py); int kw = 0, kh = 0; using (var im = Image.FromFile(ProjectLibrary.CoverSrc(pw))) { kw = im.Width; kh = im.Height; }
                Check("a picture with black bars round it is cut to the picture itself and kept whole, with a zoom and position of its own", pok && kw < 600 && kh < 400 && kw > 150 && ProjectLibrary.HasCoverSrc(pw) && px == 50 && py == 50, pok + " " + kw + "x" + kh + " zoom " + pz);
                var ce = ProjectLibrary.Find(pw); byte[] c0 = File.ReadAllBytes(ce.Cover);
                ProjectLibrary.SaveAdj(pw, 250, 10, 90); string perr2; bool pk2 = ProjectLibrary.BakeCover(pw, out perr2); byte[] c1 = File.ReadAllBytes(ProjectLibrary.Find(pw).Cover);
                Check("zooming and moving makes a new framed picture, written into the list and into the project file", pk2 && !c0.SequenceEqual(c1) && File.ReadAllText(pw).Contains("cover|"), pk2 + " " + perr2);
                int qz, qx, qy; ProjectLibrary.LoadAdj(pw, out qz, out qx, out qy); Check("the zoom and position are remembered", qz == 250 && qx == 10 && qy == 90);
                string photo = Path.Combine(StudioEnv.DataDir, "photo.png"); using (var b1 = new Bitmap(400, 400)) { using (var g1 = Graphics.FromImage(b1)) { var rn = new Random(3); for (int i = 0; i < 400; i += 4) for (int j = 0; j < 400; j += 4) using (var br = new SolidBrush(Color.FromArgb(rn.Next(256), rn.Next(256), rn.Next(256)))) g1.FillRectangle(br, i, j, 4, 4); } b1.Save(photo, System.Drawing.Imaging.ImageFormat.Png); }
                using (var im2 = Image.FromFile(photo)) Check("a picture with no border is never cut", !ProjectLibrary.AutoTrim(im2).HasValue);
                sf.OpenCoverAdjust(pw); bool opened = (bool)typeof(StudioForm).GetField("caOpen", BF).GetValue(sf); typeof(StudioForm).GetField("caOpen", BF).SetValue(sf, false);
                Check("the picture button opens the resize sheet when the project already has a picture", opened);
                ProjectLibrary.ClearCover(pw); Check("removing the picture also removes the kept whole picture", !ProjectLibrary.HasCoverSrc(pw));
            }
            sf.eng.Tracks.Clear();
        }
        {   // a sound file dragged in from outside (Bass Lab's DRAG TO SONG): it shows where it lands and goes onto the track under the mouse, not a new track
            sf.eng.Tracks.Clear(); int n0 = 44100 * 10; var bx = new float[n0]; for (int i = 0; i < n0; i++) bx[i] = (float)(0.3 * Math.Sin(i * 0.02)); var beat = StudioEngine.NewTrack("Beat", "beat", AudioClip.FromArrays(bx, (float[])bx.Clone(), "b")); sf.eng.Tracks.Add(beat);
            SetF("tab", 0); sf.Refresh(); Pump(100);
            string wv = Path.Combine(StudioEnv.DataDir, "bass lab note.wav"); var nx = new float[44100]; for (int i = 0; i < nx.Length; i++) nx[i] = (float)(0.5 * Math.Sin(i * 0.01)); SoundIO.WriteWav(wv, nx, nx, 44100, 24);
            Func<string, float> V = nm => { var fi = typeof(StudioForm).GetField(nm, BF | BindingFlags.Static); if (fi != null) return Convert.ToSingle(fi.GetValue(fi.IsStatic ? null : sf)); var pi = typeof(StudioForm).GetProperty(nm, BF | BindingFlags.Static); return Convert.ToSingle(pi.GetValue(pi.GetGetMethod(true).IsStatic ? null : sf, null)); };
            var pt = new Point((int)V("HeadW") + 200, (int)(V("ArrTop") + V("RulerH") + V("LaneH") / 2 - V("vscroll")));
            typeof(StudioForm).GetMethod("ExtDragStart", BF).Invoke(sf, new object[] { new[] { wv } }); SetF("MousePos", new PointF(pt.X, pt.Y)); sf.Refresh();
            var sd = (SoundItem)typeof(StudioForm).GetField("sndDrag", BF).GetValue(sf); Check("a sound file dragged in shows its preview while it is dragged", sd != null && (bool)typeof(StudioForm).GetField("sndDragging", BF).GetValue(sf) && sd.Peaks.Length == 200 && Math.Abs(sd.Dur - 1) < 0.05, sd == null ? "none" : sd.Dur.ToString("0.00"));
            long want = (long)typeof(StudioForm).GetMethod("FrameAtX", BF).Invoke(sf, new object[] { (float)pt.X });
            typeof(StudioForm).GetField("extDrag", BF).SetValue(sf, false); typeof(StudioForm).GetField("sndDragging", BF).SetValue(sf, false); typeof(StudioForm).GetMethod("DropSampleAt", BF).Invoke(sf, new object[] { pt }); typeof(StudioForm).GetField("sndDrag", BF).SetValue(sf, null);
            var piece = sf.eng.Tracks.FirstOrDefault(t => t != beat);
            Check("dropped on a track that has audio, it goes on that track at that time (no new track)", sf.eng.Tracks.Count == 2 && piece != null && piece.Host == beat && Math.Abs(piece.Offset - want) < 44100 / 10, sf.eng.Tracks.Count + " tracks, offset " + (piece == null ? -1 : piece.Offset) + " want " + want);
            sf.eng.Tracks.Clear();
        }
        Console.WriteLine(fails == 0 ? "=== new feature checks passed ===" : "=== " + fails + " FAILED ===");
        sf.Close(); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
