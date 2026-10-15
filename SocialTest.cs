using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// Friends, messages, collabs, reports, songs on the community page, the feed with its filters, EPs and albums, and music videos: all of it against the REAL website
// (purphex-web is started on a free port with an empty data folder), so what the app sends is checked by what the website really does.   csc /main:SO ... SocialTest.cs
static class SO
{
    static int fails = 0; static void Check(string n, bool ok, string d = "") { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + (d.Length > 0 ? "   " + d : "")); Console.Out.Flush(); }
    static string FindTool(string n) { try { string root = Path.Combine(StudioEnv.DataDir, "tools", "ffmpeg"); if (Directory.Exists(root)) foreach (var f in Directory.GetFiles(root, n, SearchOption.AllDirectories)) return f; } catch { } return null; }
    static readonly Random rnd = new Random(); static string Key() { var b = new byte[16]; rnd.NextBytes(b); return BitConverter.ToString(b).Replace("-", "").ToLowerInvariant(); }
    static byte[] FakeMp3() { var r = new Random(7); var b = new byte[6000]; r.NextBytes(b); Array.Copy(Encoding.ASCII.GetBytes("ID3\x03\x00\x00\x00\x00\x00\x00"), b, 10); return b; }
    static byte[] FakeMp4() { var r = new Random(9); var b = new byte[9000]; r.NextBytes(b); Array.Copy(new byte[] { 0, 0, 0, 24 }, 0, b, 0, 4); Array.Copy(Encoding.ASCII.GetBytes("ftypisom"), 0, b, 4, 8); return b; }
    [STAThread]
    static void Main()
    {
        string ffm = FindTool("ffmpeg.exe"), ffp = FindTool("ffprobe.exe"); if (ffm != null) StudioEnv.Ffmpeg = () => ffm; if (ffp != null) StudioEnv.Ffprobe = () => ffp;
        string web = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "purphex-web"); if (!File.Exists(Path.Combine(web, "server.js"))) { Console.WriteLine("=== social checks skipped (purphex-web is not on this PC) ==="); return; }
        string tmp = Path.Combine(Path.GetTempPath(), "purphex_so_" + Guid.NewGuid().ToString("N").Substring(0, 8)); Directory.CreateDirectory(tmp); StudioEnv.DataDir = Path.Combine(tmp, "appdata"); Directory.CreateDirectory(StudioEnv.DataDir); string pa = Path.Combine(tmp, "a.ytmproj"), pb = Path.Combine(tmp, "b.ytmproj"), pc = Path.Combine(tmp, "c.ytmproj"); File.WriteAllText(pa, "x"); File.WriteAllText(pb, "x"); File.WriteAllText(pc, "x");
        int port = 7400 + new Random().Next(1500); var psi = new ProcessStartInfo("node", "server.js") { WorkingDirectory = web, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        psi.EnvironmentVariables["PORT"] = port.ToString(); psi.EnvironmentVariables["DATA_DIR"] = Path.Combine(tmp, "web"); psi.EnvironmentVariables["PUBLIC_URL"] = ""; var srv = Process.Start(psi); srv.OutputDataReceived += (s, e) => { }; srv.ErrorDataReceived += (s, e) => { }; srv.BeginOutputReadLine(); srv.BeginErrorReadLine();
        string was = CommunityPresets.BaseUrl; CommunityPresets.BaseUrl = "http://127.0.0.1:" + port;
        try
        {
            bool up = false; for (int i = 0; i < 80 && !up; i++) { try { using (var wc = new WebClient()) { wc.DownloadString(CommunityPresets.BaseUrl + "/healthz"); up = true; } } catch { Thread.Sleep(150); } } Check("the website is running for the test", up);
            if (!up) return;
            string kA = Key(), kB = Key(), kC = Key(); ArtistInfo me;
            Func<string, string, string, string> prof = (key, name, tracks) => "{\"key\":\"" + key + "\",\"name\":\"" + name + "\",\"timeMade\":4000,\"tracks\":[" + tracks + "],\"collections\":[]}";
            string e1 = ProfApi.Publish(prof(kA, "Ana Bee", "{\"t\":\"Song One\",\"p\":\"" + CommunitySongs.PidOf(pa) + "\"},{\"t\":\"Song Two\",\"p\":\"" + CommunitySongs.PidOf(pb) + "\"}"), out me); string idA = me == null ? "" : me.Id; Check("a profile is published and shows its TIME MADE", e1 == null && idA.Length > 5 && me.TimeMade == 4000, e1 ?? "");
            string e2 = ProfApi.Publish(prof(kB, "Ben Cee", ""), out me); string idB = me == null ? "" : me.Id; string e3 = ProfApi.Publish(prof(kC, "Cy Dee", ""), out me); string idC = me == null ? "" : me.Id; Check("two more", e2 == null && e3 == null, idA + " " + idB + " " + idC);
            // ---- friends ----
            string st; string err = ProfApi.Friend(kA, idB, true, out st); Check("ADD FRIEND sends a request", err == null && st == "requested", err ?? st);
            string fr; bool bl; ProfApi.StateOf(kB, idA, out fr, out bl); Check("the other one sees an incoming request", fr == "incoming");
            var rq = new List<ArtistInfo>(); ProfApi.Me(kB, new List<ArtistInfo>(), null, rq); Check("and it is in their list of requests", rq.Count == 1 && rq[0].Id == idA);
            err = ProfApi.Friend(kB, idA, true, out st); Check("accepting makes them friends", err == null && st == "friends"); var frs = new List<ArtistInfo>(); ProfApi.Me(kA, new List<ArtistInfo>(), frs, null); Check("both have each other as a friend", frs.Count == 1 && frs[0].Id == idB);
            var a0 = ProfApi.Get(idA); Check("the artist page shows the friends and the time made", a0 != null && a0.Friends == 1 && a0.TimeMade == 4000);
            // ---- messages ----
            err = ProfApi.Send(kA, idB, "Love your drop!", "Night Drive"); Check("a message is sent, about a song", err == null, err ?? ""); err = ProfApi.Send(kA, idB, "   ", ""); Check("an empty message is refused with a sentence", err != null && err.StartsWith("Write something"));
            int un; var box = ProfApi.Inbox(kB, out un); Check("the other artist has it, unread, with the song it is about", box != null && un == 1 && box.Count == 1 && box[0].With.Id == idA && box[0].Msgs[0].Text == "Love your drop!" && box[0].Msgs[0].About == "Night Drive" && !box[0].Msgs[0].Mine);
            ProfApi.MarkRead(kB, idA); ProfApi.Inbox(kB, out un); Check("reading clears the number", un == 0); ProfApi.Send(kB, idA, "Thanks!", ""); box = ProfApi.Inbox(kA, out un); Check("the reply comes back in the same conversation", box.Count == 1 && box[0].Msgs.Count == 2 && un == 1);
            Check("BLOCK works", ProfApi.Block(kB, idA, true)); err = ProfApi.Send(kA, idB, "hello?", ""); Check("a blocked artist cannot write and is told", err != null && err.Contains("blocked")); ProfApi.StateOf(kB, idA, out fr, out bl); Check("the block shows on their page", bl && fr == "none"); ProfApi.Block(kB, idA, false); ProfApi.Friend(kA, idB, true, out st); ProfApi.Friend(kB, idA, true, out st); Check("blocking ended the friendship, and they can be friends again", st == "friends");
            // ---- reports ----
            err = ProfApi.ReportWhy(idC, "", kA); Check("a report needs a reason", err != null && err.Contains("why")); err = ProfApi.ReportWhy(idC, "Pretends to be another artist", kA); Check("a report with a reason goes through", err == null, err ?? "");
            // ---- collabs ----
            err = ProfApi.CollabInvite(kA, idC, "Joint"); Check("only friends can collab", err != null && err.Contains("friend")); err = ProfApi.CollabInvite(kA, idB, "Joint Track"); Check("a friend is invited", err == null, err ?? ""); var cl = ProfApi.Collabs(kB); Check("it shows for them as an invite", cl != null && cl.Count == 1 && cl[0].State == "invited" && !cl[0].Inviter && cl[0].With.Id == idA);
            Check("they accept", ProfApi.CollabAnswer(kB, cl[0].Id, true) == null); string mp3 = Path.Combine(tmp, "mine.mp3"); File.WriteAllBytes(mp3, FakeMp3()); err = ProfApi.CollabUpload(kA, cl[0].Id, mp3); Check("a version is sent", err == null, err ?? "");
            string gerr; var got = ProfApi.CollabGet(kB, cl[0].Id, out gerr); Check("and the other artist gets exactly that", got != null && got.Length == 6000 && got[0] == 'I'); got = ProfApi.CollabGet(kA, cl[0].Id, out gerr); Check("your own side has nothing to get until they send theirs", got == null && gerr != null);
            // ---- songs on the community page, the feed and the filters ----
            string sid1, sid2; err = CommunitySongs.Upload(kA, CommunitySongs.PidOf(pa), "Song One", "dnb dark", "first", mp3, null, out sid1); Check("a published song is uploaded to the community page", err == null && sid1.Length > 5, err ?? ""); err = CommunitySongs.Upload(kA, CommunitySongs.PidOf(pb), "Song Two", "dnb", "", mp3, null, out sid2); err = CommunitySongs.Upload(kA, CommunitySongs.PidOf(pc), "Lone Trap", "trap", "", mp3, null, out sid2);
            string pd = Path.Combine(tmp, "Third.ytmproj"), pe = Path.Combine(tmp, "Fourth.ytmproj"); File.WriteAllText(pd, "x"); File.WriteAllText(pe, "x");   // an EP needs 4 songs: two more projects that are published on the profile
            ReleaseStore.ResetForTest(); ReleaseStore.Add("ep", "Four Songs", new List<string> { CommunitySongs.PidOf(pa), CommunitySongs.PidOf(pb), CommunitySongs.PidOf(pd), CommunitySongs.PidOf(pe) });
            var ents = new List<ProjectEntry> { new ProjectEntry { Path = pa, Name = "Song One", OnProfile = true }, new ProjectEntry { Path = pb, Name = "Song Two", OnProfile = true }, new ProjectEntry { Path = pc, Name = "Lone Trap", OnProfile = true }, new ProjectEntry { Path = pd, Name = "Third", OnProfile = true }, new ProjectEntry { Path = pe, Name = "Fourth", OnProfile = true } }; string cjson = ReleaseStore.Json(ents);
            Check("an EP with its published projects becomes the collections of the profile", cjson.Contains("\"k\":\"ep\"") && cjson.Contains("Four Songs") && cjson.Split(new[] { "\"p\":" }, StringSplitOptions.None).Length == 2); ents[1].OnProfile = false; Check("an EP needs four published projects", ReleaseStore.Json(ents) == "[]"); ents[1].OnProfile = true;
            e1 = ProfApi.Publish("{\"key\":\"" + kA + "\",\"name\":\"Ana Bee\",\"tracks\":[{\"t\":\"Song One\",\"p\":\"" + CommunitySongs.PidOf(pa) + "\"},{\"t\":\"Song Two\",\"p\":\"" + CommunitySongs.PidOf(pb) + "\"}],\"collections\":" + cjson + "}", out me); Check("the profile with its EP is published", e1 == null);
            List<string> tags; var feed = ProfApi.Feed("", false, "", "", out tags); Check("the feed lists the published songs with the artist", feed != null && feed.Count == 3 && feed.Exists(s => s.Title == "Song One" && s.Artist == "Ana Bee" && s.Kind == "ep" && s.Coll == "Four Songs" && s.Cn == 4), feed == null ? "null" : feed.Count.ToString());
            Check("and the genres that are used", tags.Contains("dnb") && tags.Contains("trap")); feed = ProfApi.Feed("", false, "trap", "", out tags); Check("genre filter", feed.Count == 1 && feed[0].Title == "Lone Trap"); feed = ProfApi.Feed("ep", false, "", "", out tags); Check("EPS filter (in order)", feed.Count == 2 && feed[0].Title == "Song One" && feed[1].Title == "Song Two"); feed = ProfApi.Feed("album", false, "", "", out tags); Check("ALBUMS filter is empty here", feed.Count == 0); feed = ProfApi.Feed("", true, "", "", out tags); Check("NEW RELEASES", feed.Count == 3);
            string lerr; var heard = ProfApi.Listen(idA, sid1, out lerr); Check("a song can be listened to from the artist page", heard != null && heard.Length == 6000, lerr ?? "");
            var ar = ProfApi.Get(idA); var tr1 = ar.TrackList.Find(t => t.Title == "Song One"); Check("the artist page knows which tracks have a song up", tr1 != null && tr1.Song == sid1);
            // ---- music videos ----
            string mp4 = Path.Combine(tmp, "v.mp4"); File.WriteAllBytes(mp4, FakeMp4()); err = ProfApi.VideoUpload(kA, CommunitySongs.PidOf(pa), mp4); Check("a music video goes up with its song", err == null, err ?? ""); err = ProfApi.VideoUpload(kA, "pnone", mp4); Check("a video for a song that is not up is refused with a sentence", err != null && err.Contains("not on the community page"));
            string txt = Path.Combine(tmp, "x.mp4"); File.WriteAllText(txt, new string('x', 5000)); err = ProfApi.VideoUpload(kA, CommunitySongs.PidOf(pb), txt); Check("something that is not a video is refused", err != null);
            feed = ProfApi.Feed("ep", false, "", "", out tags); Check("the feed says which song has a music video", feed[0].Video && !feed[1].Video); ar = ProfApi.Get(idA); Check("and the artist page does", ar.TrackList.Find(t => t.Title == "Song One").VideoUp);
            Check("the video can be removed again", ProfApi.VideoRemove(kA, CommunitySongs.PidOf(pa))); feed = ProfApi.Feed("ep", false, "", "", out tags); Check("and is gone from the feed", !feed[0].Video);
            // ---- taking the songs down ----
            Check("the artist takes a song off the community page", CommunitySongs.Remove(kA, CommunitySongs.PidOf(pc)) == null); feed = ProfApi.Feed("", false, "", "", out tags); Check("it is gone from the feed", feed.Count == 2);
            // ---- a REAL project: exported to an MP3 by the program, put on the community page, listened to; a real video (and a converted one) played from the website ----
            if (ffm != null)
            {
                var app = (App)Activator.CreateInstance(typeof(App), true); var sf = new StudioForm(app); sf.Show(); string wav = Path.Combine(tmp, "tone.wav");
                { int n = 44100 * 3; var tone = new float[n]; for (int i = 0; i < n; i++) tone[i] = (float)(0.4 * Math.Sin(2 * Math.PI * 440 * i / 44100.0)); using (var ws = new WavStream(wav, 44100, 16)) ws.Write(tone, (float[])tone.Clone(), 0, n); }
                var clip = AudioClip.FromFile(wav); sf.eng.Tracks.Add(StudioEngine.NewTrack("Tone", "beat", clip)); string real = Path.Combine(tmp, "Real Song.ytmproj"); ProjectIO.Save(sf.eng, real);
                var notes = new List<string>(); sf.songNoteHook = x => notes.Add(x); string kR = Key(); string eR = ProfApi.Publish(prof(kR, "Real Rae", ""), out me); string idR = me == null ? "" : me.Id; Check("a profile for the real project", eR == null && idR.Length > 5);
                var ent = new ProjectEntry { Path = real, Name = "Real Song", Tags = "dnb dark", OnProfile = true }; typeof(StudioForm).GetMethod("SongSyncRun", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).Invoke(sf, new object[] { kR, new List<ProjectEntry> { ent } });
                Check("a real project is exported to an MP3 by the program and put on the community page", notes.Exists(x => x.Contains("is on the community page of the website")), string.Join(" / ", notes.ToArray()));
                var feedR = ProfApi.Feed("", false, "", "real", out tags); Check("it shows in the feed under the artist", feedR != null && feedR.Count == 1 && feedR[0].Title == "Real Song" && feedR[0].Artist == "Real Rae");
                string lerr2; var realMp3 = ProfApi.Listen(idR, feedR[0].Id, out lerr2); bool isMp3 = realMp3 != null && realMp3.Length > 20000 && (realMp3[0] == 'I' && realMp3[1] == 'D' && realMp3[2] == '3' || realMp3[0] == 0xFF); Check("the song on the page is a real MP3 of the project", isMp3, realMp3 == null ? lerr2 : realMp3.Length + " bytes");
                if (isMp3 && ffp != null)
                {
                    string tf = Path.Combine(tmp, "heard.realMp3"); File.WriteAllBytes(tf, realMp3); var pr = Process.Start(new ProcessStartInfo(ffp, "-v error -show_entries format=duration -of default=nw=1:nk=1 \"" + tf + "\"") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true }); string dur = pr.StandardOutput.ReadToEnd().Trim(); pr.WaitForExit(); double d; double.TryParse(dur, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out d);
                    Check("and it is as long as the project (3 seconds of tone and the tail)", d > 2.8 && d < 7.5, dur);
                }
                // unpublish takes it down
                ent.OnProfile = false; notes.Clear(); typeof(StudioForm).GetMethod("SongSyncRun", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).Invoke(sf, new object[] { kR, new List<ProjectEntry> { ent } }); feedR = ProfApi.Feed("", false, "", "real", out tags); Check("making the project private takes the song off the community page", feedR != null && feedR.Count == 0);
                ent.OnProfile = true; typeof(StudioForm).GetMethod("SongSyncRun", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).Invoke(sf, new object[] { kR, new List<ProjectEntry> { ent } }); feedR = ProfApi.Feed("", false, "", "real", out tags); string songR = feedR.Count == 1 ? feedR[0].Id : "";
                {   // saving the published project again sends the new version straight away: the same song on the page (its id, plays and likes), the new sound
                    int n5 = 44100 * 6; var t5 = new float[n5]; for (int q = 0; q < n5; q++) t5[q] = (float)(0.4 * Math.Sin(2 * Math.PI * 330 * q / 44100.0)); string wav5 = Path.Combine(tmp, "tone6.wav"); using (var ws5 = new WavStream(wav5, 44100, 16)) ws5.Write(t5, (float[])t5.Clone(), 0, n5);
                    sf.eng.Tracks.Clear(); sf.eng.Tracks.Add(StudioEngine.NewTrack("Tone 6", "beat", AudioClip.FromFile(wav5))); ProjectIO.Save(sf.eng, real); File.SetLastWriteTimeUtc(real, DateTime.UtcNow.AddSeconds(30)); notes.Clear();
                    typeof(StudioForm).GetMethod("SongSyncRun", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).Invoke(sf, new object[] { kR, new List<ProjectEntry> { ent } }); var f5 = ProfApi.Feed("", false, "", "real", out tags); string le5; var mp5 = f5 != null && f5.Count == 1 ? ProfApi.Listen(idR, f5[0].Id, out le5) : null;
                    Check("saving a published project updates its song on the community page right away: same song (plays and likes kept), the new version", notes.Exists(x => x.Contains("is updated on the community page")) && f5 != null && f5.Count == 1 && f5[0].Id == songR && mp5 != null && realMp3 != null && mp5.Length > realMp3.Length * 1.3, string.Join(" / ", notes.ToArray()) + " | " + (mp5 == null ? 0 : mp5.Length) + " vs " + (realMp3 == null ? 0 : realMp3.Length));
                    notes.Clear(); typeof(StudioForm).GetMethod("SongSyncRun", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).Invoke(sf, new object[] { kR, new List<ProjectEntry> { ent } }); Check("and with nothing saved since, nothing is sent again", !notes.Exists(x => x.Contains("updat")), string.Join(" / ", notes.ToArray()));
                }
                // a real video (an H.264 MP4 goes up as it is) and a converted one (a Matroska file with another codec), played from the website
                string v1 = Path.Combine(tmp, "clip.mp4"), v2 = Path.Combine(tmp, "clip.mkv");
                Action<string, string, string> mk = (outp, size, codec) => { var p = Process.Start(new ProcessStartInfo(ffm, "-hide_banner -loglevel error -y -f lavfi -i testsrc2=duration=3:size=" + size + ":rate=24 " + codec + " \"" + outp + "\"") { UseShellExecute = false, CreateNoWindow = true }); p.WaitForExit(40000); };
                mk(v1, "320x240", "-c:v libx264 -pix_fmt yuv420p"); mk(v2, "180x320", "-c:v mpeg4");
                string outp1, outp2; bool made1, made2; string pe1 = VideoPrep.Prepare(v1, out outp1, out made1, null); Check("an H.264 MP4 is sent as it is", pe1 == null && !made1 && outp1 == v1, pe1 ?? "");
                string pe2 = VideoPrep.Prepare(v2, out outp2, out made2, null); Check("a tall MPEG-4 video in an MKV file is converted to a web video of the same shape", pe2 == null && made2 && File.Exists(outp2) && outp2.EndsWith(".mp4"), pe2 ?? "");
                if (made2) { var pr = Process.Start(new ProcessStartInfo(ffp, "-v error -select_streams v:0 -show_entries stream=codec_name,width,height -of default=nw=1 \"" + outp2 + "\"") { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true }); string info = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); Check("the converted video is H.264 and keeps its 9:16 shape (no stretching)", info.Contains("codec_name=h264") && info.Contains("width=180") && info.Contains("height=320"), info.Replace("\r", " ").Replace("\n", " ")); }
                string pidR = CommunitySongs.PidOf(real); string ve = ProfApi.VideoUpload(kR, pidR, outp2); Check("the converted video goes up", ve == null, ve ?? "");
                using (var vf = new VideoFeed(ffm, CommunityPresets.BaseUrl + "/api/profiles/" + idR + "/video/" + songR, 0.5, 320, 180))
                {
                    byte[] frm = null; for (int i = 0; i < 160 && frm == null; i++) { frm = vf.FrameFor(1.5); if (frm == null) Thread.Sleep(50); }
                    bool leftBlack = false, centreLit = false; if (frm != null) { leftBlack = frm[(90 * 320 + 2) * 4] < 12; int ci = (90 * 320 + 160) * 4; centreLit = frm[ci] + frm[ci + 1] + frm[ci + 2] > 40; }
                    Check("the video plays from the website address (seeking with ranges), a tall video in a wide box with bars at the sides", frm != null && leftBlack && centreLit, frm == null ? "no frame" : "t " + vf.CurT);
                }
                try { if (made2) File.Delete(outp2); } catch { } try { sf.Close(); } catch { }
            }
            else Console.WriteLine("SKIP  the real project export (ffmpeg is not installed)");
        }
        finally { try { if (!srv.HasExited) srv.Kill(); } catch { } CommunityPresets.BaseUrl = was; try { Directory.Delete(tmp, true); } catch { } }
        // ---- the pure parts of the video and the releases ----
        Check("the video bitrate leaves the video under 100 MB (3 minutes gets a high one, 10 hours cannot fit)", VideoPrep.VideoKbps(180) == 3000 || VideoPrep.VideoKbps(180) > 2500, VideoPrep.VideoKbps(180).ToString()); Check("a long video gets a lower bitrate", VideoPrep.VideoKbps(600) < VideoPrep.VideoKbps(180) && VideoPrep.VideoKbps(600) >= 300); Check("a video that is far too long says it cannot fit", VideoPrep.VideoKbps(36000) == 0);
        Check("a web video goes up as it is (H.264 mp4, VP9 webm), others are converted", VideoPrep.PlainOk(".mp4", "h264", "yuv420p", 10) && VideoPrep.PlainOk(".webm", "vp9", "yuv420p", 10) && !VideoPrep.PlainOk(".mov", "h264", "yuv420p", 10) && !VideoPrep.PlainOk(".mp4", "hevc", "yuv420p", 10) && !VideoPrep.PlainOk(".mp4", "h264", "yuv444p", 10) && !VideoPrep.PlainOk(".mp4", "h264", "yuv420p", 200L * 1024 * 1024));
        string args = VideoPrep.TranscodeArgs("in.mkv", "out.mp4", 3000); Check("the conversion keeps the shape of the picture (no fixed width and height) and fits any size", args.Contains("force_original_aspect_ratio=decrease") && args.Contains("trunc(iw/2)*2") && !args.Contains("scale=1920:1080") && args.Contains("+faststart"));
        Check("an EP needs 4 or more songs, an album 5 or more, both need a name and hold up to 20 songs", ReleaseStore.Check("ep", "Name", 4) == null && ReleaseStore.Check("ep", "Name", 3) != null && ReleaseStore.Check("album", "Name", 5) == null && ReleaseStore.Check("album", "Name", 4) != null && ReleaseStore.Check("ep", "Name", 20) == null && ReleaseStore.Check("album", "Name", 20) == null && ReleaseStore.Check("ep", "A", 4) != null && ReleaseStore.Check("album", "Name", 21) != null);
        if (ffm != null)
        {   // a real video of any shape is turned into frames of the size of the box, with black bars where the shapes differ
            string dir = Path.Combine(Path.GetTempPath(), "purphex_so_v_" + Guid.NewGuid().ToString("N").Substring(0, 6)); Directory.CreateDirectory(dir); string v43 = Path.Combine(dir, "w.mp4"), vtall = Path.Combine(dir, "t.mp4");
            Action<string, string> make = (outp, size) => { var p = Process.Start(new ProcessStartInfo(ffm, "-hide_banner -loglevel error -y -f lavfi -i testsrc2=duration=3:size=" + size + ":rate=24 -c:v libx264 -pix_fmt yuv420p \"" + outp + "\"") { UseShellExecute = false, CreateNoWindow = true }); p.WaitForExit(30000); };
            make(v43, "320x240"); make(vtall, "180x320");
            foreach (var pair in new[] { new[] { v43, "4:3" }, new[] { vtall, "9:16" } })
            {
                using (var vf = new VideoFeed(ffm, pair[0], 0.5, 320, 180))
                {
                    byte[] fr0 = null; for (int i = 0; i < 100 && fr0 == null; i++) { fr0 = vf.FrameFor(1.2); if (fr0 == null) Thread.Sleep(50); }
                    bool leftBlack = false, centreLit = false; if (fr0 != null) { int bpp = 4; leftBlack = fr0[(90 * 320 + 2) * bpp] < 12 && fr0[(90 * 320 + 2) * bpp + 1] < 12; int ci = (90 * 320 + 160) * bpp; centreLit = fr0[ci] + fr0[ci + 1] + fr0[ci + 2] > 40; }
                    Check("a " + pair[1] + " video is shown in a 16:9 box without stretching (bars on the sides, picture in the middle), frame at the right time", fr0 != null && fr0.Length == 320 * 180 * 4 && leftBlack && centreLit && vf.CurT >= 0.5 && vf.CurT <= 1.2, fr0 == null ? "no frame" : "left black " + leftBlack + ", centre " + centreLit + ", t " + vf.CurT);
                }
            }
            try { Directory.Delete(dir, true); } catch { }
        }
        else Console.WriteLine("SKIP  the frames of a real video (ffmpeg is not installed)");
        Console.WriteLine(fails == 0 ? "=== social checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
