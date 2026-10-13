using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// The Artists page is where you talk to other artists: ADD FRIEND, MESSAGES (and replies to their songs), COLLABS (make one track together), BLOCK and REPORT.
// The website keeps it all; the secret key of your artist profile proves who you are (there is no sign-in in the app).
sealed class MsgItem { public bool Mine; public long At; public string Text = "", About = ""; }
sealed class MsgThread { public ArtistInfo With; public int Unread; public long At; public bool Blocked; public List<MsgItem> Msgs = new List<MsgItem>(); }
sealed class CollabInfo { public string Id = "", Title = "", State = ""; public bool Inviter; public ArtistInfo With; public long MineAt, MineSize, TheirAt, TheirSize; }

static partial class ProfApi
{
    static Dictionary<string, object> PostJ(string path, string body) { return MiniJson.Parse(Http("POST", path, body, Edition.Token)) as Dictionary<string, object>; }   /* the link of this PC goes along: a linked PC can write, add friends and collab before it publishes a profile */
    static string KeyId(string key, string id, bool? on = null) { return "{\"key\":" + J(key) + ",\"id\":" + J(id) + (on.HasValue ? ",\"on\":" + (on.Value ? "true" : "false") : "") + "}"; }
    public static string Say(string err)
    {
        switch (err)
        {
            case "profile": return "Publish your profile first (Profile tab): people need to see who you are.";
            case "key": return "Your profile key is missing. Open the Profile tab once.";
            case "gone": return "That artist or item is not there any more.";
            case "blocked": return "You cannot write to this artist (one of you has blocked the other).";
            case "empty": return "Write something first.";
            case "rate": return "That is a lot of messages in a short time. Try again a little later.";
            case "full": return "That is the most there can be. Remove some first.";
            case "friends": return "Only friends can be invited to a collab. Add them as a friend first.";
            case "title": return "The track needs a name with at least 2 letters.";
            case "why": return "Please say why you are reporting them (at least a few words).";
            case "big": return "The file is bigger than 30 MB (about 12 minutes as an MP3).";
            case "type": return "The exported song could not be read. Try again.";
            default: return "The community website said no (" + err + ").";
        }
    }
    // null = it worked, otherwise a sentence
    static string Fail(Dictionary<string, object> o) { if (o == null) return "The community website gave an answer that could not be read."; if (B(o, "ok")) return null; return Say(S(o, "err")); }
    // state: none, requested, incoming or friends
    public static string Friend(string key, string id, bool on, out string state)
    {
        state = "none"; try { var o = PostJ("/api/profiles/friend", KeyId(key, id, on)); string e = Fail(o); if (e == null) state = S(o, "state"); return e; } catch { return "Could not reach the Purphex community. Check your internet connection."; }
    }
    public static bool StateOf(string key, string id, out string friend, out bool blocked)
    {
        friend = "none"; blocked = false; try { var o = PostJ("/api/profiles/state", KeyId(key, id)); if (o == null) return false; friend = S(o, "friend"); if (friend.Length == 0) friend = "none"; blocked = B(o, "blocked"); return B(o, "following"); } catch { return false; }
    }
    public static string Send(string key, string to, string text, string about)
    {
        try { return Fail(PostJ("/api/profiles/msg-send", "{\"key\":" + J(key) + ",\"to\":" + J(to) + ",\"text\":" + J(text) + ",\"about\":" + J(about ?? "") + "}")); } catch { return "Could not reach the Purphex community. Check your internet connection."; }
    }
    public static List<MsgThread> Inbox(string key, out int unread)
    {
        unread = 0; var l = new List<MsgThread>();
        try
        {
            var o = PostJ("/api/profiles/msg-inbox", "{\"key\":" + J(key) + "}"); if (o == null || !B(o, "ok")) return null; unread = N(o, "unread"); object arr;
            if (o.TryGetValue("threads", out arr) && arr is List<object>)
                foreach (var x in (List<object>)arr)
                {
                    var td = x as Dictionary<string, object>; if (td == null) continue; object w; var with = td.TryGetValue("with", out w) ? Parse(w as Dictionary<string, object>) : null; if (with == null) continue;
                    var t = new MsgThread { With = with, Unread = N(td, "unread"), Blocked = B(td, "blocked") }; object at; if (td.TryGetValue("at", out at) && at is double) t.At = (long)(double)at;
                    object ms; if (td.TryGetValue("msgs", out ms) && ms is List<object>) foreach (var y in (List<object>)ms) { var md = y as Dictionary<string, object>; if (md == null) continue; var m = new MsgItem { Mine = B(md, "mine"), Text = S(md, "text"), About = S(md, "about") }; object ma; if (md.TryGetValue("at", out ma) && ma is double) m.At = (long)(double)ma; t.Msgs.Add(m); }
                    l.Add(t);
                }
        }
        catch { return null; }
        return l;
    }
    public static void MarkRead(string key, string with) { try { PostJ("/api/profiles/msg-read", "{\"key\":" + J(key) + ",\"with\":" + J(with) + "}"); } catch { } }
    public static bool Block(string key, string id, bool on) { try { var o = PostJ("/api/profiles/block", KeyId(key, id, on)); return B(o, "ok"); } catch { return false; } }
    public static string ReportWhy(string id, string why, string key)
    {
        try { return Fail(PostJ("/api/profiles/report", "{\"id\":" + J(id) + ",\"why\":" + J(why) + ",\"key\":" + J(key ?? "") + "}")); } catch { return "Could not reach the Purphex community. Check your internet connection."; }
    }
    // ---- collabs ----
    public static List<CollabInfo> Collabs(string key)
    {
        var l = new List<CollabInfo>();
        try
        {
            var o = PostJ("/api/profiles/collab-list", "{\"key\":" + J(key) + "}"); if (o == null || !B(o, "ok")) return null; object arr;
            if (o.TryGetValue("collabs", out arr) && arr is List<object>)
                foreach (var x in (List<object>)arr)
                {
                    var d = x as Dictionary<string, object>; if (d == null) continue; object w; var with = d.TryGetValue("with", out w) ? Parse(w as Dictionary<string, object>) : null; if (with == null) continue;
                    var c = new CollabInfo { Id = S(d, "id"), Title = S(d, "title"), State = S(d, "state"), Inviter = B(d, "inviter"), With = with }; object v; Dictionary<string, object> vd;
                    if (d.TryGetValue("mine", out v) && (vd = v as Dictionary<string, object>) != null) { c.MineAt = (long)N(vd, "size") > 0 ? 1 : 0; c.MineSize = N(vd, "size"); }
                    if (d.TryGetValue("theirs", out v) && (vd = v as Dictionary<string, object>) != null) { c.TheirAt = 1; c.TheirSize = N(vd, "size"); }
                    l.Add(c);
                }
        }
        catch { return null; }
        return l;
    }
    public static string CollabInvite(string key, string to, string title)
    {
        try { return Fail(PostJ("/api/profiles/collab-invite", "{\"key\":" + J(key) + ",\"to\":" + J(to) + ",\"title\":" + J(title) + "}")); } catch { return "Could not reach the Purphex community. Check your internet connection."; }
    }
    public static string CollabAnswer(string key, string id, bool on) { try { return Fail(PostJ("/api/profiles/collab-answer", KeyId(key, id, on))); } catch { return "Could not reach the Purphex community. Check your internet connection."; } }
    public static string CollabUpload(string key, string id, string audioPath)
    {
        try
        {
            byte[] audio = File.ReadAllBytes(audioPath); if (audio.Length > 30 * 1024 * 1024) return Say("big"); string boundary = "----purphex" + Guid.NewGuid().ToString("N"); var ms = new MemoryStream();
            Action<string, string> part = (n, v) => { var b = Encoding.UTF8.GetBytes("--" + boundary + "\r\nContent-Disposition: form-data; name=\"" + n + "\"\r\n\r\n" + v + "\r\n"); ms.Write(b, 0, b.Length); };
            part("key", key); part("id", id); var h = Encoding.UTF8.GetBytes("--" + boundary + "\r\nContent-Disposition: form-data; name=\"audio\"; filename=\"collab.mp3\"\r\nContent-Type: audio/mpeg\r\n\r\n"); ms.Write(h, 0, h.Length); ms.Write(audio, 0, audio.Length); ms.WriteByte(13); ms.WriteByte(10);
            var end = Encoding.UTF8.GetBytes("--" + boundary + "--\r\n"); ms.Write(end, 0, end.Length);
            var rq = (HttpWebRequest)WebRequest.Create(CommunityPresets.BaseUrl + "/api/profiles/collab-upload"); rq.Method = "POST"; rq.UserAgent = "PurphexMusic"; rq.Timeout = 600000; rq.ReadWriteTimeout = 180000; rq.ContentType = "multipart/form-data; boundary=" + boundary; rq.ContentLength = ms.Length; rq.AllowWriteStreamBuffering = false; { string bt = Edition.Token; if (bt.Length > 0) rq.Headers["Authorization"] = "Bearer " + bt; }
            using (var s = rq.GetRequestStream()) { ms.Position = 0; ms.CopyTo(s); }
            string body; try { using (var r = (HttpWebResponse)rq.GetResponse()) using (var sr = new StreamReader(r.GetResponseStream(), Encoding.UTF8)) body = sr.ReadToEnd(); }
            catch (WebException ex) { if (ex.Response == null) throw; using (var sr = new StreamReader(ex.Response.GetResponseStream(), Encoding.UTF8)) body = sr.ReadToEnd(); }
            return Fail(MiniJson.Parse(body) as Dictionary<string, object>);
        }
        catch (Exception ex) { return "Could not reach the Purphex community (" + ex.Message + ")."; }
    }
    static byte[] Download(string method, string path, string body, out string err)
    {
        err = null;
        try
        {
            var rq = (HttpWebRequest)WebRequest.Create(CommunityPresets.BaseUrl + path); rq.Method = method; rq.UserAgent = "PurphexMusic"; rq.Timeout = 120000; rq.ReadWriteTimeout = 120000; { string bt = Edition.Token; if (bt.Length > 0) rq.Headers["Authorization"] = "Bearer " + bt; }
            if (body != null) { var b = Encoding.UTF8.GetBytes(body); rq.ContentType = "application/json"; rq.ContentLength = b.Length; using (var s = rq.GetRequestStream()) s.Write(b, 0, b.Length); }
            using (var r = (HttpWebResponse)rq.GetResponse()) using (var s = r.GetResponseStream()) using (var ms = new MemoryStream()) { var buf = new byte[65536]; int n; while ((n = s.Read(buf, 0, buf.Length)) > 0) { ms.Write(buf, 0, n); if (ms.Length > 40 * 1024 * 1024) { err = "The file is too big."; return null; } } return ms.ToArray(); }
        }
        catch (WebException ex) { err = ex.Response != null && ((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.NotFound ? "There is nothing there yet." : "Could not reach the Purphex community. Check your internet connection."; return null; }
        catch (Exception ex) { err = ex.Message; return null; }
    }
    public static byte[] CollabGet(string key, string id, out string err) { return Download("POST", "/api/profiles/collab-get", KeyId(key, id), out err); }
    public static byte[] Listen(string artistId, string songId, out string err) { return Download("GET", "/api/profiles/" + artistId + "/listen/" + songId, null, out err); }
}

partial class StudioForm
{
    // ================================================================= state =================================================================
    int artView;   // 0 the artists, 1 messages, 2 collabs, 3 friends
    List<MsgThread> msgThreads = new List<MsgThread>(); int msgUnread; string msgWithId = ""; float msgScroll, msgListScroll; DateTime msgFetchAt = DateTime.MinValue; volatile bool msgFetching;
    List<ArtistInfo> profFriends = new List<ArtistInfo>(), profReqs = new List<ArtistInfo>();
    string artFriend = "none"; bool artBlocked;
    List<CollabInfo> collabList = new List<CollabInfo>(); DateTime collabFetchAt = DateTime.MinValue; volatile bool collabFetching; string socialNote = "";
    internal Func<string, string> msgAskHook;   // a test answers for itself (the text of a message, a reason)

    static string AgoMs(long ms) { try { return ProjectLibrary.Ago(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(ms).ToLocalTime()); } catch { return ""; } }
    string AskText(string prompt, string def) { return msgAskHook != null ? msgAskHook(prompt) : StudioDialogs.AskMulti(this, prompt, def); }
    // messages, friends and collabs: a published profile, or a PC linked to a purphexmusic.com account (the same as the website, where anybody with an account can write)
    bool CanSocial { get { if (ProfileStore.Published && ProfileStore.Key.Length >= 32) return true; if (!Edition.Linked) return false; ProfileStore.EnsureKey(); return ProfileStore.Key.Length >= 32; } }
    bool HaveProfile(string what) { if (CanSocial) return true; Toast("Link this PC to your purphexmusic.com account (Settings, Account, LINK THIS PC) or publish your profile (Profile tab) to " + what + "."); return false; }

    // the unread number (shown on the ARTISTS button) and the conversations; asked again at most every 45 seconds
    void FetchInbox(bool force)
    {
        if (msgFetching || !CanSocial) return; if (!force && (DateTime.Now - msgFetchAt).TotalSeconds < 45) return; msgFetchAt = DateTime.Now; msgFetching = true; string key = ProfileStore.Key;
        ThreadPool.QueueUserWorkItem(o => { int un; var l = ProfApi.Inbox(key, out un); try { BeginInvoke(new Action(() => { msgFetching = false; if (l != null) { msgThreads = l; msgUnread = un; lastInput = DateTime.Now; } })); } catch { msgFetching = false; } });
    }
    void FetchCollabs(bool force)
    {
        if (collabFetching || !CanSocial) return; if (!force && (DateTime.Now - collabFetchAt).TotalSeconds < 30) return; collabFetchAt = DateTime.Now; collabFetching = true; string key = ProfileStore.Key;
        ThreadPool.QueueUserWorkItem(o => { var l = ProfApi.Collabs(key); try { BeginInvoke(new Action(() => { collabFetching = false; if (l != null) { collabList = l; lastInput = DateTime.Now; } })); } catch { collabFetching = false; } });
    }
    internal void OpenMessages(string withId) { tab = 7; artSel = null; artView = 1; msgWithId = withId ?? ""; msgScroll = 0; FetchInbox(true); }

    // ================================================================= actions =================================================================
    // write to an artist (about = the name of the song it is about)
    void WriteMessage(string toId, string toName, string about)
    {
        if (!HaveProfile("send messages")) return;
        string text = AskText(about.Length > 0 ? "Message to " + toName + " about \"" + about + "\":" : "Message to " + toName + ":", ""); if (text == null || text.Trim().Length == 0) return; string key = ProfileStore.Key; text = text.Trim();
        ThreadPool.QueueUserWorkItem(o => { string err = ProfApi.Send(key, toId, text, about); try { BeginInvoke(new Action(() => { if (err == null) { Toast("Message sent to " + toName + "."); OpenMessages(toId); } else Toast(err); })); } catch { } });
    }
    void FriendToggle()
    {
        var a = artSel; if (a == null || artSelLoading || !HaveProfile("add friends")) return; string st = artFriend; bool on = st == "none" || st == "incoming";
        if (st == "friends" && !AskYes("Remove " + a.Name + " from your friends?", "Remove friend")) return; string key = ProfileStore.Key; string id = a.Id, name = a.Name; artSelLoading = true;
        ThreadPool.QueueUserWorkItem(o => { string ns; string err = ProfApi.Friend(key, id, on, out ns); try { BeginInvoke(new Action(() => { artSelLoading = false; if (err != null) { Toast(err); return; } artFriend = ns; Toast(ns == "friends" ? "You and " + name + " are friends now." : (ns == "requested" ? "Friend request sent to " + name + "." : (st == "friends" ? name + " is not your friend any more." : "Done."))); ProfileRefresh(); })); } catch { } });
    }
    void BlockToggle()
    {
        var a = artSel; if (a == null || artSelLoading || !HaveProfile("block someone")) return; bool on = !artBlocked;
        if (on && !AskYes("Block " + a.Name + "?\n\nThey cannot write to you any more, and if you are friends it ends. You can unblock them here whenever you like.", "Block artist")) return; string key = ProfileStore.Key, id = a.Id, name = a.Name; artSelLoading = true;
        ThreadPool.QueueUserWorkItem(o => { bool ok = ProfApi.Block(key, id, on); try { BeginInvoke(new Action(() => { artSelLoading = false; if (ok) { artBlocked = on; if (on) artFriend = "none"; Toast(on ? name + " is blocked." : name + " is unblocked."); ProfileRefresh(); FetchInbox(true); } else Toast("Could not reach the Purphex community. Check your internet connection."); })); } catch { } });
    }
    // REPORT: the reason is required, and it goes to the staff (the member reports channel on Discord)
    void ReportArtist(ArtistInfo a)
    {
        if (a == null || artReported == a.Id) return;
        string why = AskText("Why are you reporting " + a.Name + "? (the staff read this)", ""); if (why == null) return; why = why.Trim();
        if (why.Length < 4) { Toast("Please say why you are reporting them (at least a few words)."); return; }
        string id = a.Id, key = ProfileStore.Key.Length >= 32 ? ProfileStore.Key : "";
        ThreadPool.QueueUserWorkItem(o => { string err = ProfApi.ReportWhy(id, why, key); try { BeginInvoke(new Action(() => { if (err == null) { artReported = id; Toast("Reported. Thank you: the staff will look at it."); } else Toast(err); })); } catch { } });
    }
    // INVITE TO COLLAB: pick one of your projects (its name becomes the name of the collab)
    void InviteCollab(ArtistInfo a)
    {
        if (a == null || !HaveProfile("start a collab")) return; var all = ProjectLibrary.AllByEdited().FindAll(x => !x.Missing); var m = Win.NewMenu();
        m.Items.Add("Name of the collab track: pick one of your projects").Enabled = false; m.Items.Add(new ToolStripSeparator());
        foreach (var e in all) { var ee = e; m.Items.Add(e.Name).Click += (s0, e0) => SendCollabInvite(a, ee.Name); }
        m.Items.Add("A new track name...").Click += (s0, e0) => { string n = Ask("Name of the track you will make together:", ""); if (n != null && n.Trim().Length >= 2) SendCollabInvite(a, n.Trim()); };
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }
    void SendCollabInvite(ArtistInfo a, string title)
    {
        string key = ProfileStore.Key, id = a.Id, name = a.Name; ThreadPool.QueueUserWorkItem(o => { string err = ProfApi.CollabInvite(key, id, title); try { BeginInvoke(new Action(() => { if (err == null) { Toast("Collab invite for \"" + title + "\" sent to " + name + "."); artView = 2; tab = 7; artSel = null; FetchCollabs(true); FetchInbox(true); } else Toast(err); })); } catch { } });
    }
    void CollabAnswerClick(CollabInfo c, bool on)
    {
        if (!on && !AskYes("End the collab \"" + c.Title + "\" with " + c.With.Name + "? The versions that were sent are deleted.", "End collab")) return; string key = ProfileStore.Key, id = c.Id;
        ThreadPool.QueueUserWorkItem(o => { string err = ProfApi.CollabAnswer(key, id, on); try { BeginInvoke(new Action(() => { if (err != null) Toast(err); FetchCollabs(true); })); } catch { } });
    }
    // SEND MY VERSION: one of your projects is exported (mixed and mastered, like EXPORT) and sent, the other artist is told
    void CollabSendClick(CollabInfo c)
    {
        var all = ProjectLibrary.AllByEdited().FindAll(x => !x.Missing); if (all.Count == 0) { Toast("Save a project first: its song is what you send."); return; } var m = Win.NewMenu();
        m.Items.Add("Which project is your part of \"" + c.Title + "\"?").Enabled = false; m.Items.Add(new ToolStripSeparator());
        foreach (var e in all) { var ee = e; m.Items.Add(e.Name).Click += (s0, e0) => CollabSendProject(c, ee); }
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }
    void CollabSendProject(CollabInfo c, ProjectEntry e)
    {
        string key = ProfileStore.Key, cid = c.Id; Toast("Exporting \"" + e.Name + "\" for the collab...");
        new Thread(() =>
        {
            string mp3 = null, err = null; try { mp3 = RenderSong(e); } catch (Exception ex) { err = ex.Message; }
            if (mp3 != null) { try { BeginInvoke(new Action(() => Toast("Sending \"" + e.Name + "\" to " + c.With.Name + "..."))); } catch { } err = ProfApi.CollabUpload(key, cid, mp3); try { File.Delete(mp3); string d = Path.GetDirectoryName(mp3); if (d != null && d.IndexOf("purphex_comm_", StringComparison.Ordinal) >= 0) Directory.Delete(d); } catch { } }
            try { BeginInvoke(new Action(() => { Toast(err == null ? "Your version was sent to " + c.With.Name + "." : "Could not send it: " + err); FetchCollabs(true); FetchInbox(true); })); } catch { }
        }) { IsBackground = true, Name = "collab-send" }.Start();
    }
    // GET THEIR VERSION: saved in Documents > Purphex Music > Collabs, and shown in its folder (add it to a project with ADD MUSIC)
    void CollabGetClick(CollabInfo c)
    {
        string key = ProfileStore.Key, cid = c.Id, who = c.With.Name, title = c.Title; Toast("Getting " + who + "'s version...");
        ThreadPool.QueueUserWorkItem(o =>
        {
            string err; var data = ProfApi.CollabGet(key, cid, out err); string path = null;
            if (data != null)
            {
                try
                {
                    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Purphex Music", "Collabs"); Directory.CreateDirectory(dir); string nm = (title + " - " + who + " " + DateTime.Now.ToString("yyyy-MM-dd HH-mm")); foreach (char ch in Path.GetInvalidFileNameChars()) nm = nm.Replace(ch, ' ');
                    string ext = data.Length > 4 && data[0] == 'f' && data[1] == 'L' ? ".flac" : (data.Length > 12 && data[0] == 'R' && data[1] == 'I' ? ".wav" : ".mp3"); path = Path.Combine(dir, nm + ext); File.WriteAllBytes(path, data);
                }
                catch (Exception ex) { err = ex.Message; path = null; }
            }
            try { BeginInvoke(new Action(() => { if (path != null) { Toast(who + "'s version is saved. Use ADD MUSIC in the DAW to put it in your project."); try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\""); } catch { } } else Toast(err ?? "Could not get it."); })); } catch { }
        });
    }

    // ================================================================= playing an artist's track =================================================================
    // the audio comes from the community page (the song the artist published) and plays in the same strip along the bottom as a project preview
    void ArtistTrackClick(ArtistInfo a, ArtistTrack t)
    {
        string pkey = "artist:" + a.Id + ":" + t.Song; if (t.Song.Length > 0 && string.Equals(previewPath, pkey, StringComparison.Ordinal)) { if (previewEng != null) PreviewPauseToggle(); return; }
        bool hasLink = (t.Video ?? "").Length > 0, hasFile = t.VideoUp && t.Song.Length > 0, video = hasLink || hasFile, watch = false;
        if (video) watch = AskYes("\"" + t.Title + "\" has a music video.\n\nWatch the video too?\n\nYes: " + (hasFile ? "the video plays in a player above the strip at the bottom (it has a FULL SCREEN button), with the song." : "open the video and play the song.") + "\nNo: play the song only.", "Music video");
        if (watch && !hasFile) OpenLink(t.Video);
        if (t.Song.Length == 0) { if (!video || !watch) Toast("This track has no song on the community page yet" + (video ? " (the music video was opened)." : ".")); return; }
        PlayArtistSong(a, t, watch && hasFile);
    }
    // WATCH VIDEO on a track row: the video file plays in the player (the song starts if it is not playing), a video link opens in the browser
    void ArtistWatch(ArtistInfo a, ArtistTrack t)
    {
        if (!t.VideoUp || t.Song.Length == 0) { if ((t.Video ?? "").Length > 0) OpenLink(t.Video); return; }
        string pkey = "artist:" + a.Id + ":" + t.Song; if (string.Equals(previewPath, pkey, StringComparison.Ordinal) && previewEng != null) { StartVideoOverlay(a.Id, t.Song, pkey); return; }
        PlayArtistSong(a, t, true);
    }
    void PlayArtistSong(ArtistInfo a, ArtistTrack t, bool withVideo = false)
    {
        StopPreview(); if (eng.Playing) eng.Pause(); string pkey = "artist:" + a.Id + ":" + t.Song; previewPath = pkey; previewName = a.Name + " - " + t.Title; previewLoading = true; previewPaused = false; string aid = a.Id, sid = t.Song, title = t.Title;
        new Thread(() =>
        {
            StudioEngine pe = null; string err = null, tmp = null;
            try
            {
                var data = ProfApi.Listen(aid, sid, out err); if (data != null)
                {
                    tmp = Path.Combine(Path.GetTempPath(), "purphex_listen_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".mp3"); File.WriteAllBytes(tmp, data); var clip = AudioClip.FromFile(tmp);
                    pe = new StudioEngine(); StudioEngine.Current = eng; pe.Tracks.Add(StudioEngine.NewTrack(title, "beat", clip));
                }
            }
            catch (Exception ex) { err = ex.Message; }
            finally { try { if (tmp != null) File.Delete(tmp); } catch { } }   // the song is never kept as a file
            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (previewPath != pkey) { if (pe != null) { try { pe.Shutdown(); } catch { } } return; }
                    if (pe == null || pe.EndFrame < 1000) { if (pe != null) { try { pe.Shutdown(); } catch { } } Toast("Could not play it: " + (err ?? "the song is empty.")); StopPreview(); return; }
                    previewEng = pe; previewLoading = false; try { pe.Play(); if (withVideo) StartVideoOverlay(aid, sid, pkey); } catch (Exception ex2) { Toast("Could not play it: " + ex2.Message); StopPreview(); }
                }));
            }
            catch { }
        }) { IsBackground = true, Name = "artist-listen" }.Start();
    }

    // one track of an artist: a round play button, the name, "AUDIO ONLY" or "MUSIC VIDEO", the progress while it plays, and a button to write to the artist about it
    void ArtistTrackRow(Graphics g, RectangleF r, ArtistInfo a, ArtistTrack t, Color acc)
    {
        bool hot = Hot(r); FillRR(g, TM.C(hot ? 255 : 240, 16, 9, 36), r, 16); Win.DrawNeonBox(g, r, hot);
        string pkey = "artist:" + a.Id + ":" + t.Song; bool can = t.Song.Length > 0, mine = can && string.Equals(previewPath, pkey, StringComparison.Ordinal), playing = mine && previewEng != null && previewEng.Playing && !previewPaused, loading = mine && previewEng == null;
        var rowReg = Reg(r, () => ArtistTrackClick(a, t)); Hint2(r, can ? (playing ? "Pause" : "Play this track") : "This track has no song on the community page yet");
        // the round play button
        var pb = Rc(r.X + 16, r.Y + (r.Height - 52) / 2, 52, 52); Color pc = can ? NeonCyan : TM.C(255, 90, 80, 120);
        if (can) { Glow(g, pc, pb, 26, hot || playing ? 0.8f : 0.4f, 4); using (var br = new LinearGradientBrush(pb, playing ? TM.C(255, 255, 43, 214) : TM.C(255, 120, 70, 235), playing ? TM.C(255, 150, 20, 160) : TM.C(255, 70, 40, 180), 90f)) g.FillEllipse(br, pb); using (var pen = new Pen(Color.FromArgb(230, Color.White), 1.6f)) g.DrawEllipse(pen, pb); }
        else { using (var br = new SolidBrush(TM.C(255, 28, 18, 50))) g.FillEllipse(br, pb); using (var pen = new Pen(TM.C(255, 80, 70, 110), 1.4f)) g.DrawEllipse(pen, pb); }
        using (var b = new SolidBrush(can ? Color.White : TM.C(255, 120, 110, 150)))
        {
            if (loading) { for (int k = 0; k < 3; k++) { float ph = (float)((Environment.TickCount / 250.0 + k) % 3) / 3f; g.FillEllipse(b, pb.X + 14 + k * 9, pb.Y + 24 + (float)Math.Sin(ph * Math.PI * 2) * 3, 6, 6); } lastInput = DateTime.Now; }
            else if (playing) { g.FillRectangle(b, pb.X + 17, pb.Y + 15, 7, 22); g.FillRectangle(b, pb.X + 29, pb.Y + 15, 7, 22); }
            else g.FillPolygon(b, new[] { new PointF(pb.X + 20, pb.Y + 14), new PointF(pb.X + 20, pb.Y + 38), new PointF(pb.X + 39, pb.Y + 26) });
        }
        // buttons on the right
        float rx = r.Right - 14; bool hasV = (t.Video ?? "").Length > 0 || (t.VideoUp && can);
        var wb = Rc(rx - 150, r.Y + 10, 150, 28); Btn(g, wb, "MESSAGE ABOUT IT", 1, () => WriteMessage(a.Id, a.Name, t.Title), 10); Hint2(wb, "Write to " + a.Name + " about this song (a reply to the track)"); rx -= 158;
        if (hasV) { var vb = Rc(rx - 130, r.Y + 10, 130, 28); Btn(g, vb, "WATCH VIDEO", 2, () => ArtistWatch(a, t), 10); Hint2(vb, t.VideoUp ? "Watch the music video in the player (it has a FULL SCREEN button)" : "Watch the music video of this track (opens " + ProfileStore.LinkLabel(t.Video) + ")"); rx -= 138; }
        // the name, then the line with the details and the label
        float tx = pb.Right + 16, tw = Math.Max(60, rx - tx - 6); Txt(g, t.Title, 16, Ink, Rc(tx, r.Y + 8, tw, 24), StringAlignment.Near, true);
        string meta = (t.Bpm > 0 ? t.Bpm + " BPM" : "") + (t.Bpm > 0 && t.Secs > 0 ? "   -   " : "") + (t.Secs > 0 ? Dur(t.Secs) : "") + (t.Tags.Length > 0 ? "   -   #" + t.Tags.Replace(" ", " #") : ""); Txt(g, meta, 11, DimC, Rc(tx, r.Y + 34, tw, 18), StringAlignment.Near, false);
        string lab = hasV ? "MUSIC VIDEO" : "AUDIO ONLY"; float lw = 18 + lab.Length * 7.2f; var pill = Rc(tx, r.Y + r.Height - 26, lw, 18); FillRR(g, hasV ? TM.C(200, 255, 43, 214) : TM.C(160, 60, 40, 110), pill, 9); Txt(g, lab, 9, Color.White, pill, StringAlignment.Center, true);
        if (!can) { var np = Rc(pill.Right + 8, pill.Y, 150, 18); Txt(g, "NO SONG UP YET", 9, DimC, np, StringAlignment.Near, true); }
        // while it plays: a progress line (click it to jump) and the time
        if (mine && previewEng != null)
        {
            double dur = Math.Max(1, previewEng.EndFrame), pos = previewEng.Pos; float bx = pill.Right + (can ? 14 : 170), bw = Math.Max(40, rx - bx - 80); var bar = Rc(bx, pill.Y + 6, bw, 6); FillRR(g, TM.C(255, 30, 18, 56), bar, 3); float f = (float)Math.Min(1.0, pos / dur); if (f > 0.002f) FillRR(g, NeonCyan, Rc(bar.X, bar.Y, bar.Width * f, bar.Height), 3);
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, bar.X + bar.Width * f - 6, bar.Y - 3, 12, 12);
            string tm = ((int)(pos / StudioEngine.SR / 60)) + ":" + ((int)(pos / StudioEngine.SR % 60)).ToString("00") + " / " + ((int)(dur / StudioEngine.SR / 60)) + ":" + ((int)(dur / StudioEngine.SR % 60)).ToString("00"); Txt(g, tm, 10, Ink, Rc(bar.Right + 8, pill.Y, 80, 18), StringAlignment.Near, false);
            var hit = Rc(bar.X - 6, bar.Y - 8, bar.Width + 12, 22); var pe2 = previewEng; Action<float> seek = mx => { double ff = Math.Max(0, Math.Min(1, (mx - bar.X) / bar.Width)); try { pe2.Seek((long)(ff * dur)); } catch { } }; var rg = Reg(hit, () => seek(MousePos.X)); rg.OnDrag = pt => seek(pt.X); lastInput = DateTime.Now;
        }
    }

    // the buttons on an artist's page: ADD FRIEND, MESSAGE, INVITE TO COLLAB (friends only), BLOCK
    void SocialButtons(Graphics g, ArtistInfo a, float cx, float cw, float ty)
    {
        string fl = artFriend == "friends" ? "FRIENDS (REMOVE)" : (artFriend == "requested" ? "REQUEST SENT (CANCEL)" : (artFriend == "incoming" ? "ACCEPT FRIEND REQUEST" : "ADD FRIEND"));
        float y2 = ty + 64, x = cx + cw; var bl = Rc(x - 110, y2, 110, 32); Btn(g, bl, artBlocked ? "UNBLOCK" : "BLOCK", artBlocked ? 4 : 3, () => BlockToggle(), 11); Hint2(bl, artBlocked ? "Let " + a.Name + " write to you again" : "Stop " + a.Name + " writing to you (it also ends a friendship)"); x -= 118;
        if (artFriend == "friends") { var co = Rc(x - 190, y2, 190, 32); Btn(g, co, "INVITE TO COLLAB", 1, () => InviteCollab(a), 11); Hint2(co, "Make one track together with " + a.Name); x -= 198; }
        var ms = Rc(x - 130, y2, 130, 32); Btn(g, ms, "MESSAGE", 1, () => WriteMessage(a.Id, a.Name, ""), 11); Hint2(ms, "Write to " + a.Name); x -= 138;
        var fb = Rc(x - 230, y2, 230, 32); Btn(g, fb, artSelLoading ? "WORKING..." : fl, artFriend == "friends" ? 4 : (artFriend == "none" || artFriend == "incoming" ? 2 : 0), () => FriendToggle(), 11);
        Hint2(fb, artFriend == "friends" ? "You are friends: press to remove " + a.Name : (artFriend == "requested" ? "You asked to be friends: press to cancel the request" : (artFriend == "incoming" ? a.Name + " asked to be your friend: press to accept" : "Ask " + a.Name + " to be your friend")));
    }

    // ================================================================= the Messages page =================================================================
    void PaintMessages(Graphics g, float W, float top, float bottom)
    {
        FetchInbox(false); FetchCollabs(false); float y = top + 12, h = 36;
        var back = Rc(24, y, 150, h); Btn(g, back, "< ALL ARTISTS", 1, () => { artView = 0; }, 12); Hint2(back, "Back to the list of artists");
        Txt(g, "MESSAGES", 22, Ink, Rc(190, y - 2, 240, h + 4), StringAlignment.Near, true);
        var cb = Rc(W - 56 - 170, y, 170, h); Btn(g, cb, "COLLABS", 2, () => { artView = 2; FetchCollabs(true); }, 12); Hint2(cb, "The tracks you make together with other artists");
        var fb0 = Rc(cb.X - 8 - 170, y, 170, h); Btn(g, fb0, "FRIENDS" + (profReqs.Count > 0 ? " (" + profReqs.Count + ")" : ""), 2, () => OpenFriends(), 12); Hint2(fb0, "Your friends and the friend requests, in the same layout as messages");
        y += h + 14; float listW = Math.Min(360, Math.Max(250, W * 0.3f)), px = 24, ph = bottom - y - 12; var left = Rc(px, y, listW, ph); FillRR(g, TM.C(235, 14, 7, 32), left, 14); Win.DrawNeonBox(g, left, false);
        var right = Rc(left.Right + 14, y, W - 56 - left.Right - 14 - 6, ph); FillRR(g, TM.C(235, 14, 7, 32), right, 14); Win.DrawNeonBox(g, right, false);
        if (!CanSocial) { TxtWrap(g, "Link this PC to your purphexmusic.com account (Settings, Account, LINK THIS PC) or publish your profile (Profile tab) to send and get messages, add friends and start collabs.", 14, DimC, Rc(right.X + 24, right.Y + 24, right.Width - 48, 80)); return; }
        // ---- the left side: friend requests, then the conversations ----
        float lh = 0; { float contentTop = left.Y + 8; var gs = g.Save(); g.SetClip(Rc(left.X + 2, left.Y + 2, left.Width - 4, left.Height - 4), CombineMode.Intersect); float ly = contentTop - msgListScroll;
            if (profReqs.Count > 0)
            {
                Txt(g, "FRIEND REQUESTS (" + profReqs.Count + ")", 11, NeonPink, Rc(left.X + 14, ly, left.Width - 28, 18), StringAlignment.Near, true); ly += 22;
                foreach (var rq in profReqs)
                {
                    var row = Rc(left.X + 8, ly, left.Width - 16, 54); FillRR(g, TM.C(170, 60, 24, 90), row, 10); NameTextC(g, rq.Shown, 13, Rc(row.X + 12, row.Y + 4, row.Width - 24, 20), Ink, true, rq.NameRgb, rq.NameRgb2);
                    var ok = Rc(row.X + 10, row.Y + 26, 92, 24); var no = Rc(ok.Right + 8, row.Y + 26, 82, 24); var rr = rq; Btn(g, ok, "ACCEPT", 2, () => AnswerRequest(rr, true), 10); Btn(g, no, "DECLINE", 3, () => AnswerRequest(rr, false), 10); var vp = Rc(no.Right + 8, row.Y + 26, 70, 24); Btn(g, vp, "PROFILE", 0, () => { OpenArtist(rr.Id); artView = 0; }, 10); ly += 60;
                }
                ly += 6;
            }
            Txt(g, "CONVERSATIONS", 11, DimC, Rc(left.X + 14, ly, left.Width - 28, 18), StringAlignment.Near, true); ly += 22;
            if (msgThreads.Count == 0) { TxtWrap(g, msgFetching ? "LOADING..." : "NO MESSAGES YET. OPEN AN ARTIST AND PRESS MESSAGE.", 12, DimC, Rc(left.X + 14, ly, left.Width - 28, 50)); ly += 56; }
            foreach (var t in msgThreads)
            {
                var row = Rc(left.X + 8, ly, left.Width - 16, 56); bool sel = t.With.Id == msgWithId, hotr = Hot(Rc(row.X, Math.Max(row.Y, left.Y), row.Width, Math.Min(row.Height, left.Bottom - row.Y)));
                FillRR(g, sel ? TM.C(220, 80, 40, 150) : (hotr ? TM.C(170, 60, 34, 110) : TM.C(120, 44, 24, 80)), row, 10); if (sel) StrokeRR(g, NeonCyan, row, 10, 1.4f);
                var av = Rc(row.X + 8, row.Y + 8, 40, 40); Bitmap ab = t.With.Avatar ? ArtistImage(t.With.Id, "avatar", t.With.Av, 60, 60) : null; DrawFrameImage(g, av, ab, ProfAccent(t.With.Accent));
                NameTextC(g, t.With.Shown, 13, Rc(av.Right + 10, row.Y + 6, row.Width - av.Width - 60, 20), Ink, true, t.With.NameRgb, t.With.NameRgb2); string last = t.Msgs.Count > 0 ? (t.Msgs[t.Msgs.Count - 1].Mine ? "You: " : "") + t.Msgs[t.Msgs.Count - 1].Text.Replace('\n', ' ') : ""; Txt(g, last, 11, DimC, Rc(av.Right + 10, row.Y + 28, row.Width - av.Width - 26, 20), StringAlignment.Near, false);
                if (t.Unread > 0) { var bd = Rc(row.Right - 30, row.Y + 8, 22, 18); FillRR(g, NeonPink, bd, 9); Txt(g, t.Unread.ToString(), 10, Color.White, bd, StringAlignment.Center, true); }
                var tt = t; var vis = RectangleF.Intersect(row, Rc(left.X, left.Y, left.Width, left.Height)); if (vis.Height > 6) Reg(vis, () => { msgWithId = tt.With.Id; msgScroll = 0; msgTyping = true; msgReplyTo = ""; if (tt.Unread > 0) { tt.Unread = 0; msgUnread = Math.Max(0, msgUnread - 1); string key = ProfileStore.Key, wid = tt.With.Id; ThreadPool.QueueUserWorkItem(o => ProfApi.MarkRead(key, wid)); } });
                ly += 62;
            }
            lh = ly - contentTop + msgListScroll + 8; g.Restore(gs); }
        float maxL = Math.Max(0, lh - left.Height); msgListScroll = Math.Max(0, Math.Min(maxL, msgListScroll)); regs.Add(new HitReg { R = left, OnWheel = d => msgListScroll = Math.Max(0, Math.Min(maxL, msgListScroll - d * 50)) });
        // ---- the right side: the conversation ----
        MsgThread cur = msgThreads.Find(t => t.With.Id == msgWithId);
        if (cur == null && msgWithId.Length > 0) { var stub = artList.Find(x => x.Id == msgWithId) ?? profFriends.Find(x => x.Id == msgWithId); if (stub == null) stub = artSel != null && artSel.Id == msgWithId ? artSel : null; if (stub != null) cur = new MsgThread { With = stub }; }
        if (cur == null) { TxtWrap(g, msgThreads.Count > 0 ? "CHOOSE A CONVERSATION ON THE LEFT." : "WHEN YOU WRITE TO AN ARTIST (OR THEY WRITE TO YOU) THE CONVERSATION SHOWS HERE.", 14, DimC, Rc(right.X + 24, right.Y + 24, right.Width - 48, 60)); return; }
        { var hd = Rc(right.X + 14, right.Y + 10, right.Width - 28, 44); NameTextC(g, cur.With.Shown, 18, Rc(hd.X, hd.Y, hd.Width - 330, 28), Ink, true, cur.With.NameRgb, cur.With.NameRgb2);
          var wr = Rc(hd.Right - 320, hd.Y + 2, 150, 36); var cu = cur; Btn(g, wr, "WRITE A MESSAGE", 2, () => WriteMessage(cu.With.Id, cu.With.Name, ""), 11); var pr = Rc(wr.Right + 8, hd.Y + 2, 150, 36); Btn(g, pr, "OPEN PROFILE", 0, () => { OpenArtist(cu.With.Id); artView = 0; }, 11); }
        float boxH = 50 + (msgReplyTo.Length > 0 ? 24 : 0); var conv = Rc(right.X + 14, right.Y + 62, right.Width - 28, right.Height - 76 - boxH - 8); float bw = Math.Min(conv.Width * 0.7f, 520), cy = 0;
        var fnt = GfxCache.Exact(GfxCache.Grow(13), false); var hs = new List<float>(); foreach (var m in cur.Msgs) { float th = g.MeasureString(m.Text, fnt, (int)(bw - 28)).Height + 16 + (m.About.Length > 0 ? 18 : 0) + 18; hs.Add(th); cy += th + 8; }
        float maxS = Math.Max(0, cy - conv.Height); msgScroll = Math.Max(0, Math.Min(maxS, msgScroll)); regs.Add(new HitReg { R = right, OnWheel = d => msgScroll = Math.Max(0, Math.Min(maxS, msgScroll + d * 50)) });
        { var gs = g.Save(); g.SetClip(conv, CombineMode.Intersect); float yy = conv.Bottom - cy + msgScroll; if (cy < conv.Height) yy = conv.Y;
          for (int i = 0; i < cur.Msgs.Count; i++)
          {
              var m = cur.Msgs[i]; float th = hs[i]; float bx = m.Mine ? conv.Right - bw - 6 : conv.X + 6; var bub = Rc(bx, yy, bw, th); yy += th + 8; if (bub.Bottom < conv.Y || bub.Y > conv.Bottom) continue;
              bool hb = Hot(RectangleF.Intersect(bub, conv)); FillRR(g, m.Mine ? TM.C(235, 70, 40, 170) : (hb ? TM.C(235, 46, 28, 90) : TM.C(235, 34, 20, 66)), bub, 14); if (!m.Mine) StrokeRR(g, Color.FromArgb(hb ? 220 : 120, NeonCyan), bub, 14, hb ? 1.6f : 1f); float ty = bub.Y + 8;
              { var vis = RectangleF.Intersect(bub, conv); var mm = m; if (vis.Height > 6) { Reg(vis, () => { msgReplyTo = Snip(mm.Text, 50); msgTyping = true; }); Hint2(vis, "Press to reply to this message"); } }
              if (m.About.Length > 0) { Txt(g, (m.About.StartsWith("Reply: ") ? "REPLYING TO: " + m.About.Substring(7) : "ABOUT: " + m.About).ToUpper(), 10, m.Mine ? Color.FromArgb(230, 255, 224, 150) : NeonCyan, Rc(bub.X + 14, ty, bub.Width - 28, 16), StringAlignment.Near, true); ty += 18; }
              TxtWrap(g, m.Text, 13, Ink, Rc(bub.X + 14, ty, bub.Width - 28, th - (ty - bub.Y) - 18)); Txt(g, AgoMs(m.At), 9, Color.FromArgb(170, Ink), Rc(bub.X + 14, bub.Bottom - 20, bub.Width - 28, 14), StringAlignment.Far, false);
          }
          g.Restore(gs); }
        if (cur.Msgs.Count == 0) Txt(g, "NO MESSAGES YET. PRESS WRITE A MESSAGE.", 13, DimC, Rc(conv.X + 10, conv.Y + 10, conv.Width - 20, 24), StringAlignment.Near, false);
        if (maxS > 0) { var sb = Rc(right.Right - 52, conv.Y, 42, conv.Height); VScroll(g, sb, cy, conv.Height, () => maxS - msgScroll, v => msgScroll = maxS - v); }
        PaintReplyBox(g, Rc(right.X + 14, right.Bottom - 14 - boxH, right.Width - 28, boxH), cur.With);
    }
    // ---- the reply box: type straight into the conversation (Enter sends, Esc stops typing), press a message to reply to it, emoji from the smiley ----
    internal string msgDraft = "", msgReplyTo = ""; internal bool msgTyping;
    static string Snip(string t, int n) { t = (t ?? "").Replace('\n', ' ').Trim(); return t.Length > n ? t.Substring(0, n - 1) + "…" : t; }
    bool MsgBoxActive { get { return msgTyping && tab == 7 && artView == 1 && !OverlayOpen && (editBox == null || !editBox.Visible); } }
    void PaintReplyBox(Graphics g, RectangleF r, ArtistInfo to)
    {
        float y = r.Y;
        if (msgReplyTo.Length > 0) { var rb = Rc(r.X, y, r.Width, 20); Txt(g, "REPLYING TO: " + msgReplyTo, 10, NeonCyan, Rc(rb.X + 4, rb.Y, rb.Width - 40, 20), StringAlignment.Near, true); var xb = Rc(rb.Right - 24, rb.Y, 22, 20); Btn(g, xb, "x", 3, () => msgReplyTo = "", 10); Hint2(xb, "Stop replying to that message"); y += 24; }
        var send = Rc(r.Right - 96, y + 4, 96, 40); var emo = Rc(send.X - 50, y + 4, 44, 40); var box = Rc(r.X, y + 4, emo.X - r.X - 8, 40);
        FillRR(g, TM.C(240, 8, 4, 20), box, 12); StrokeRR(g, MsgBoxActive ? NeonCyan : Color.FromArgb(150, NeonViolet), box, 12, MsgBoxActive ? 1.8f : 1.2f);
        bool blink = ((Environment.TickCount / 500) % 2) == 0; string shown = msgDraft.Length == 0 ? (MsgBoxActive ? (blink ? "|" : "") : "Write to " + to.Name + "...  (press here, then type: Enter sends)") : msgDraft.Replace('\n', ' ') + (MsgBoxActive && blink ? "|" : "");
        if (MsgBoxActive) lastInput = DateTime.Now;
        var gs = g.Save(); g.SetClip(box, System.Drawing.Drawing2D.CombineMode.Intersect); float tw = g.MeasureString(shown, GfxCache.Font(13, false)).Width; float tx = Math.Min(box.X + 12, box.Right - 12 - tw);
        Txt(g, shown, 13, msgDraft.Length == 0 && !MsgBoxActive ? DimC : Ink, Rc(tx, box.Y, Math.Max(box.Width - 24, tw + 8), box.Height), StringAlignment.Near, false); g.Restore(gs);
        Reg(box, () => { msgTyping = true; }); Hint2(box, "Press here and type your message (Enter sends, Esc stops typing, Ctrl+V pastes)");
        var too = to; Btn(g, emo, ":)", 1, () => EmojiMenu(emo), 12); Hint2(emo, "Add an emoji");
        Btn(g, send, "SEND", msgDraft.Trim().Length > 0 ? 2 : 0, () => SendDraft(too), 13); Hint2(send, "Send the message (Enter does the same)");
    }
    static readonly string[] QuickEmoji = { "\U0001F525", "\U0001F60D", "\U0001F602", "\U0001F64C", "\U0001F44D", "\U0001F3B5", "\U0001F3A7", "\U0001F3B9", "\U0001F4AF", "\u2764\uFE0F", "\U0001F91D", "\U0001F680" };
    void EmojiMenu(RectangleF at) { var m = Win.NewMenu(); foreach (var e in QuickEmoji) { var em = e; m.Items.Add(em).Click += (a, b) => { msgDraft += em; msgTyping = true; }; } m.Show(this, new Point((int)at.X, (int)at.Bottom)); }
    void SendDraft(ArtistInfo to)
    {
        string text = msgDraft.Trim(); if (text.Length == 0) { msgTyping = true; return; } if (!HaveProfile("send messages")) return; if (text.Length > 600) text = text.Substring(0, 600);
        string key = ProfileStore.Key, id = to.Id, about = msgReplyTo.Length > 0 ? Snip("Reply: " + msgReplyTo, 60) : ""; msgDraft = ""; msgReplyTo = "";
        ThreadPool.QueueUserWorkItem(o => { string err = ProfApi.Send(key, id, text, about); try { BeginInvoke(new Action(() => { if (err == null) { msgWithId = id; msgScroll = 0; FetchInbox(true); } else { msgDraft = text; Toast(err); } })); } catch { } });
    }
    // keys while the reply box has the cursor: letters go into the message, not to the shortcuts of the DAW
    internal bool MsgKey(char c)
    {
        if (!MsgBoxActive) return false;
        if (c == '\b') { if (msgDraft.Length > 0) msgDraft = msgDraft.Substring(0, msgDraft.Length - 1); return true; }
        if (c == '\r') { var cur = msgThreads.Find(t => t.With.Id == msgWithId); ArtistInfo to = cur != null ? cur.With : (artList.Find(x => x.Id == msgWithId) ?? profFriends.Find(x => x.Id == msgWithId)); if (to != null) SendDraft(to); return true; }
        if (c == 27) { msgTyping = false; return true; }
        if (!char.IsControl(c) && msgDraft.Length < 600) msgDraft += c; return true;
    }
    internal bool MsgCmd(Keys k)
    {
        if (!MsgBoxActive) return false;
        if (k == (Keys.Control | Keys.V)) { try { string t = Clipboard.GetText(); if (!string.IsNullOrEmpty(t)) msgDraft = (msgDraft + t.Replace("\r", "")).Substring(0, Math.Min(600, (msgDraft + t.Replace("\r", "")).Length)); } catch { } return true; }
        if (k == Keys.Escape) { if (msgReplyTo.Length > 0) msgReplyTo = ""; else msgTyping = false; return true; }
        return false;
    }
    internal bool MsgTakesKey(Keys k) { return MsgBoxActive && (k & Keys.Control) == 0 && (k & Keys.Alt) == 0 && !((k & Keys.KeyCode) >= Keys.F1 && (k & Keys.KeyCode) <= Keys.F24); }

    // ================================================================= the Friends page (the same layout as Messages) =================================================================
    string friWithId = ""; float friListScroll;
    internal void OpenFriends() { tab = 7; artSel = null; artView = 3; ProfileRefresh(); }
    void RemoveFriend(ArtistInfo a)
    {
        if (!AskYes("Remove " + a.Name + " from your friends?", "Remove friend")) return; string key = ProfileStore.Key, id = a.Id, name = a.Name;
        ThreadPool.QueueUserWorkItem(o => { string ns; string err = ProfApi.Friend(key, id, false, out ns); try { BeginInvoke(new Action(() => { Toast(err != null ? err : name + " is not your friend any more."); friWithId = ""; ProfileRefresh(); })); } catch { } });
    }
    void PaintFriends(Graphics g, float W, float top, float bottom)
    {
        float y = top + 12, h = 36;
        var back = Rc(24, y, 150, h); Btn(g, back, "< ALL ARTISTS", 1, () => { artView = 0; }, 12); Hint2(back, "Back to the list of artists");
        Txt(g, "FRIENDS", 22, Ink, Rc(190, y - 2, 240, h + 4), StringAlignment.Near, true);
        var cb = Rc(W - 56 - 170, y, 170, h); Btn(g, cb, "COLLABS", 2, () => { artView = 2; FetchCollabs(true); }, 12); Hint2(cb, "The tracks you make together with your friends");
        var mb = Rc(cb.X - 8 - 170, y, 170, h); Btn(g, mb, "MESSAGES" + (msgUnread > 0 ? " (" + msgUnread + ")" : ""), 2, () => { artView = 1; FetchInbox(true); }, 12); Hint2(mb, "Write to artists and read what they wrote");
        y += h + 14; float listW = Math.Min(360, Math.Max(250, W * 0.3f)), px = 24, ph = bottom - y - 12; var left = Rc(px, y, listW, ph); FillRR(g, TM.C(235, 14, 7, 32), left, 14); Win.DrawNeonBox(g, left, false);
        var right = Rc(left.Right + 14, y, W - 56 - left.Right - 14 - 6, ph); FillRR(g, TM.C(235, 14, 7, 32), right, 14); Win.DrawNeonBox(g, right, false);
        if (!CanSocial) { TxtWrap(g, "Link this PC to your purphexmusic.com account (Settings, Account, LINK THIS PC) or publish your profile (Profile tab) to add friends.", 14, DimC, Rc(right.X + 24, right.Y + 24, right.Width - 48, 80)); return; }
        var friends = new List<ArtistInfo>(profFriends); friends.Sort((p, q) => string.Compare(p.Name, q.Name, StringComparison.CurrentCultureIgnoreCase));
        float lh = 0; { float contentTop = left.Y + 8; var gs = g.Save(); g.SetClip(Rc(left.X + 2, left.Y + 2, left.Width - 4, left.Height - 4), CombineMode.Intersect); float ly = contentTop - friListScroll;
            if (profReqs.Count > 0)
            {
                Txt(g, "FRIEND REQUESTS (" + profReqs.Count + ")", 11, NeonPink, Rc(left.X + 14, ly, left.Width - 28, 18), StringAlignment.Near, true); ly += 22;
                foreach (var rq in profReqs)
                {
                    var row = Rc(left.X + 8, ly, left.Width - 16, 54); FillRR(g, TM.C(170, 60, 24, 90), row, 10); NameTextC(g, rq.Shown, 13, Rc(row.X + 12, row.Y + 4, row.Width - 24, 20), Ink, true, rq.NameRgb, rq.NameRgb2);
                    var ok = Rc(row.X + 10, row.Y + 26, 92, 24); var no = Rc(ok.Right + 8, row.Y + 26, 82, 24); var rr = rq; Btn(g, ok, "ACCEPT", 2, () => AnswerRequest(rr, true), 10); Btn(g, no, "DECLINE", 3, () => AnswerRequest(rr, false), 10); var vp = Rc(no.Right + 8, row.Y + 26, 70, 24); Btn(g, vp, "PROFILE", 0, () => { OpenArtist(rr.Id); artView = 0; }, 10); ly += 60;
                }
                ly += 6;
            }
            Txt(g, "YOUR FRIENDS (" + friends.Count + ")", 11, DimC, Rc(left.X + 14, ly, left.Width - 28, 18), StringAlignment.Near, true); ly += 22;
            if (friends.Count == 0) { TxtWrap(g, "NO FRIENDS YET. OPEN AN ARTIST AND PRESS ADD FRIEND.", 12, DimC, Rc(left.X + 14, ly, left.Width - 28, 50)); ly += 56; }
            foreach (var f in friends)
            {
                var row = Rc(left.X + 8, ly, left.Width - 16, 56); bool sel = f.Id == friWithId, hotr = Hot(Rc(row.X, Math.Max(row.Y, left.Y), row.Width, Math.Min(row.Height, left.Bottom - row.Y)));
                FillRR(g, sel ? TM.C(220, 80, 40, 150) : (hotr ? TM.C(170, 60, 34, 110) : TM.C(120, 44, 24, 80)), row, 10); if (sel) StrokeRR(g, NeonCyan, row, 10, 1.4f);
                var av = Rc(row.X + 8, row.Y + 8, 40, 40); Bitmap ab = f.Avatar ? ArtistImage(f.Id, "avatar", f.Av, 60, 60) : null; DrawFrameImage(g, av, ab, ProfAccent(f.Accent));
                NameTextC(g, f.Shown, 13, Rc(av.Right + 10, row.Y + 6, row.Width - av.Width - 26, 20), Ink, true, f.NameRgb, f.NameRgb2); Txt(g, (f.FollowersHidden ? "" : f.Followers + " FOLLOWERS   ") + f.Tracks + (f.Tracks == 1 ? " TRACK" : " TRACKS"), 10, NeonCyan, Rc(av.Right + 10, row.Y + 28, row.Width - av.Width - 26, 18), StringAlignment.Near, true);
                var ff = f; var vis = RectangleF.Intersect(row, Rc(left.X, left.Y, left.Width, left.Height)); if (vis.Height > 6) Reg(vis, () => { friWithId = ff.Id; });
                ly += 62;
            }
            lh = ly - contentTop + friListScroll + 8; g.Restore(gs); }
        float maxL = Math.Max(0, lh - left.Height); friListScroll = Math.Max(0, Math.Min(maxL, friListScroll)); regs.Add(new HitReg { R = left, OnWheel = d => friListScroll = Math.Max(0, Math.Min(maxL, friListScroll - d * 50)) });
        // the right side: the friend that is picked, with everything you can do with them
        ArtistInfo cur = friends.Find(x => x.Id == friWithId); if (cur == null && friends.Count > 0) { cur = friends[0]; friWithId = cur.Id; }
        if (cur == null) { TxtWrap(g, friends.Count > 0 ? "CHOOSE A FRIEND ON THE LEFT." : "YOUR FRIENDS SHOW HERE: THEIR PROFILE, AND BUTTONS TO MESSAGE THEM OR START A COLLAB.", 14, DimC, Rc(right.X + 24, right.Y + 24, right.Width - 48, 60)); return; }
        { var hd = Rc(right.X + 14, right.Y + 10, right.Width - 28, 44); NameTextC(g, cur.Shown, 20, Rc(hd.X, hd.Y, hd.Width, 30), Ink, true, cur.NameRgb, cur.NameRgb2);
          float bwx = Math.Min(170, (right.Width - 28 - 3 * 8) / 4f); float by = right.Y + 58; var cu = cur;
          var b1 = Rc(right.X + 14, by, bwx, 36); Btn(g, b1, "MESSAGE", 2, () => WriteMessage(cu.Id, cu.Name, ""), 11); var b2 = Rc(b1.Right + 8, by, bwx, 36); Btn(g, b2, "INVITE TO COLLAB", 1, () => InviteCollab(cu), 11);
          var b3 = Rc(b2.Right + 8, by, bwx, 36); Btn(g, b3, "OPEN PROFILE", 0, () => { OpenArtist(cu.Id); artView = 0; }, 11); var b4 = Rc(b3.Right + 8, by, bwx, 36); Btn(g, b4, "REMOVE FRIEND", 3, () => RemoveFriend(cu), 11);
          float sy = by + 50, sw = (right.Width - 28 - 2 * 12) / 3f; StatCard(g, Rc(right.X + 14, sy, sw, 64), "FOLLOWERS", cur.FollowersHidden ? "-" : cur.Followers.ToString(), NeonCyan); StatCard(g, Rc(right.X + 14 + sw + 12, sy, sw, 64), "TRACKS", cur.Tracks.ToString(), NeonPink); StatCard(g, Rc(right.X + 14 + 2 * (sw + 12), sy, sw, 64), "TIME MADE", cur.TimeMade > 0 ? DurMade(cur.TimeMade) : "-", Ink);
          float yy = sy + 78; Txt(g, "BIO", 11, DimC, Rc(right.X + 16, yy, 100, 16), StringAlignment.Near, true); yy += 20; var bb = Rc(right.X + 14, yy, right.Width - 28, 96); FillRR(g, TM.C(190, 8, 4, 20), bb, 10); StrokeRR(g, Color.FromArgb(130, NeonViolet), bb, 10, 1f);
          if (cur.Bio.Length > 0) TxtWrap(g, cur.Bio, 13, PlainInk, Rc(bb.X + 12, bb.Y + 8, bb.Width - 24, bb.Height - 16)); else Txt(g, "No bio yet.", 12, DimC, Rc(bb.X + 12, bb.Y + 8, bb.Width - 24, 20), StringAlignment.Near, false);
          yy = bb.Bottom + 14; if (cur.Tags.Length > 0) TagChips(g, right.X + 16, yy, right.Right - 16, cur.Tags, ProfAccent(cur.Accent), true); }
    }
    internal ArtistInfo answeredReq;   // the request that was answered last (a test reads it)
    internal void AnswerRequest(ArtistInfo a, bool accept)
    {
        if (!HaveProfile("answer friend requests")) return; answeredReq = a; profReqs.RemoveAll(x => x.Id == a.Id); if (accept && !profFriends.Exists(x => x.Id == a.Id)) profFriends.Add(a);   // the list changes at once (the website is asked in the background)
        string key = ProfileStore.Key, id = a.Id, name = a.Name; ThreadPool.QueueUserWorkItem(o => { string ns; string err = ProfApi.Friend(key, id, accept, out ns); try { BeginInvoke(new Action(() => { if (err != null) { Toast(err); if (!profReqs.Exists(x => x.Id == id)) profReqs.Add(a); profFriends.RemoveAll(x => x.Id == id && accept); } else Toast(accept ? "You and " + name + " are friends now." : "Request declined."); ProfileRefresh(); })); } catch { } });
    }

    // ================================================================= the floating chat =================================================================
    // a small chat window you can drag anywhere over the DAW (any page), so you can talk to an artist while you make music
    bool chatOpen; string chatWith = ""; float chatScroll;
    internal void OpenChat(string withId) { chatOpen = true; if (withId != null && withId.Length > 0) chatWith = withId; chatScroll = 0; FetchInbox(true); lastInput = DateTime.Now; }
    void PaintChatWindow(Graphics g)
    {
        if (!chatOpen) return; FetchInbox(false); float W = ClientSize.Width, H = ClientSize.Height, cw = Math.Min(400, W - 40), ch = Math.Min(540, H - 110);
        var box = PopMove("chat", Rc(W - cw - 24, 86, cw, ch), 40, 44); FillRR(g, TM.C(250, 16, 9, 36), box, 14); Glow(g, NeonCyan, box, 14, 0.55f, 3); StrokeRR(g, Color.FromArgb(200, NeonCyan), box, 14, 1.4f); Reg(box, () => { });
        Txt(g, "CHAT" + (msgUnread > 0 ? "  (" + msgUnread + " NEW)" : ""), 14, Ink, Rc(box.X + 16, box.Y + 10, box.Width - 130, 24), StringAlignment.Near, true);
        var xb = Rc(box.Right - 38, box.Y + 8, 28, 26); Btn(g, xb, "x", 3, () => chatOpen = false, 12); Hint2(xb, "Close the chat (drag the top of it to move it)");
        var big = Rc(box.Right - 38 - 34, box.Y + 8, 28, 26); Btn(g, big, "[]", 1, () => { chatOpen = false; OpenMessages(chatWith); }, 10); Hint2(big, "Open the big Messages page");
        if (!CanSocial) { TxtWrap(g, "Link this PC to your account (Settings, Account, LINK THIS PC) or publish your profile (Profile tab) to chat.", 12, DimC, Rc(box.X + 16, box.Y + 54, box.Width - 32, 80)); return; }
        // who: a row of the conversations
        float y = box.Y + 44; var show = msgThreads.Count > 6 ? msgThreads.GetRange(0, 6) : msgThreads; float cwid = (box.Width - 24) / Math.Max(1, show.Count > 0 ? show.Count : 1); if (cwid > 70) cwid = 70; float x = box.X + 12;
        foreach (var t in show) { var r = Rc(x, y, cwid - 4, 34); bool sel = t.With.Id == chatWith; FillRR(g, sel ? TM.C(220, 80, 40, 150) : TM.C(130, 44, 24, 80), r, 8); if (sel) StrokeRR(g, NeonCyan, r, 8, 1.2f); string nm = t.With.Name.Length > 8 ? t.With.Name.Substring(0, 8) : t.With.Name; Txt(g, nm, 10, Ink, r, StringAlignment.Center, sel); if (t.Unread > 0) { var bd = Rc(r.Right - 14, r.Y - 3, 16, 14); FillRR(g, NeonPink, bd, 7); Txt(g, t.Unread.ToString(), 8, Color.White, bd, StringAlignment.Center, true); } var tt = t; Reg(r, () => { chatWith = tt.With.Id; chatScroll = 0; if (tt.Unread > 0) { tt.Unread = 0; string key = ProfileStore.Key, wid = tt.With.Id; ThreadPool.QueueUserWorkItem(o2 => ProfApi.MarkRead(key, wid)); FetchInbox(true); } }); Hint2(r, t.With.Name); x += cwid; }
        y += 42; MsgThread cur = msgThreads.Find(t => t.With.Id == chatWith); if (cur == null && chatWith.Length > 0) { var stub = artList.Find(a => a.Id == chatWith) ?? profFriends.Find(a => a.Id == chatWith); if (stub != null) cur = new MsgThread { With = stub }; }
        var conv = Rc(box.X + 10, y, box.Width - 20, box.Bottom - y - 52);
        if (cur == null) { TxtWrap(g, msgThreads.Count > 0 ? "CHOOSE SOMEONE ABOVE." : "NO CHATS YET. OPEN AN ARTIST (ARTISTS TAB) AND PRESS MESSAGE.", 12, DimC, Rc(conv.X + 8, conv.Y + 8, conv.Width - 16, 50)); return; }
        float bw = conv.Width * 0.8f; var fnt = GfxCache.Exact(GfxCache.Grow(12), false); float tot = 0; var hs = new List<float>(); foreach (var m in cur.Msgs) { float th = g.MeasureString(m.Text, fnt, (int)(bw - 22)).Height + 14 + (m.About.Length > 0 ? 16 : 0) + 4; hs.Add(th); tot += th + 6; }
        float maxS = Math.Max(0, tot - conv.Height); chatScroll = Math.Max(0, Math.Min(maxS, chatScroll)); regs.Add(new HitReg { R = box, OnWheel = d => chatScroll = Math.Max(0, Math.Min(maxS, chatScroll + d * 40)) });
        { var gs = g.Save(); g.SetClip(conv, CombineMode.Intersect); float yy = tot < conv.Height ? conv.Y : conv.Bottom - tot + chatScroll;
          for (int i = 0; i < cur.Msgs.Count; i++) { var m = cur.Msgs[i]; float th = hs[i]; var bub = Rc(m.Mine ? conv.Right - bw - 2 : conv.X + 2, yy, bw, th); yy += th + 6; if (bub.Bottom < conv.Y || bub.Y > conv.Bottom) continue; { var vb = RectangleF.Intersect(bub, conv); var mm = m; var cw0 = cur.With; if (vb.Height > 6) { Reg(vb, () => WriteChat(cw0, Snip("Reply: " + mm.Text, 60))); Hint2(vb, "Press to reply to this message"); } } FillRR(g, m.Mine ? TM.C(235, 70, 40, 170) : TM.C(235, 34, 20, 66), bub, 12); float ty = bub.Y + 6; if (m.About.Length > 0) { Txt(g, (m.About.StartsWith("Reply: ") ? "REPLYING TO: " + m.About.Substring(7) : "ABOUT: " + m.About).ToUpper(), 9, m.Mine ? Color.FromArgb(230, 255, 224, 150) : NeonCyan, Rc(bub.X + 11, ty, bub.Width - 22, 14), StringAlignment.Near, true); ty += 16; } TxtWrap(g, m.Text, 12, Ink, Rc(bub.X + 11, ty, bub.Width - 22, th - (ty - bub.Y) - 2)); }
          g.Restore(gs); }
        if (cur.Msgs.Count == 0) Txt(g, "NO MESSAGES YET.", 12, DimC, Rc(conv.X + 8, conv.Y + 8, conv.Width - 16, 20), StringAlignment.Near, false);
        var wr = Rc(box.X + 12, box.Bottom - 44, box.Width - 24, 34); var cu = cur; Btn(g, wr, "WRITE TO " + cur.With.Name.ToUpper(), 2, () => WriteChat(cu.With), 11); Hint2(wr, "Write a message: it appears here and on the Messages page");
    }
    // a message from the floating chat (the chat stays where it is)
    void WriteChat(ArtistInfo a) { WriteChat(a, ""); }
    void WriteChat(ArtistInfo a, string about)
    {
        if (!HaveProfile("send messages")) return; string text = AskText(about.Length > 0 ? "Reply to " + a.Name + " (" + about + "):" : "Message to " + a.Name + ":", ""); if (text == null || text.Trim().Length == 0) return; string key = ProfileStore.Key, id = a.Id, name = a.Name; text = text.Trim();
        string ab = about; ThreadPool.QueueUserWorkItem(o => { string err = ProfApi.Send(key, id, text, ab); try { BeginInvoke(new Action(() => { if (err == null) { chatWith = id; chatScroll = 0; FetchInbox(true); } else Toast(err); })); } catch { } });
    }

    // ================================================================= the Collabs page =================================================================
    void PaintCollabs(Graphics g, float W, float top, float bottom)
    {
        FetchCollabs(false); float y = top + 12, h = 36;
        var back = Rc(24, y, 150, h); Btn(g, back, "< ALL ARTISTS", 1, () => { artView = 0; }, 12); Hint2(back, "Back to the list of artists");
        Txt(g, "COLLABS", 22, Ink, Rc(190, y - 2, 240, h + 4), StringAlignment.Near, true); var mb = Rc(W - 56 - 170, y, 170, h); Btn(g, mb, "MESSAGES" + (msgUnread > 0 ? " (" + msgUnread + ")" : ""), 2, () => { artView = 1; FetchInbox(true); }, 12); var fb1 = Rc(mb.X - 8 - 170, y, 170, h); Btn(g, fb1, "FRIENDS" + (profReqs.Count > 0 ? " (" + profReqs.Count + ")" : ""), 2, () => OpenFriends(), 12);
        y += h + 12; TxtWrap(g, "A collab is one track made by two friends. Invite a friend from their artist page. When they accept, each of you sends your own version (your project, mixed and mastered) and gets the other one to put in your own project.", 12, DimC, Rc(24, y, W - 120, 36)); y += 44;
        if (!CanSocial) { TxtWrap(g, "Link this PC to your account (Settings, Account, LINK THIS PC) or publish your profile (Profile tab) to start collabs.", 14, DimC, Rc(24, y, W - 120, 40)); return; }
        var area = Rc(0, y, W - 56, bottom - y); float cy = y - collabScroll; float contentH = collabList.Count * 96 + 10; float viewH = bottom - y; collabScroll = Math.Max(0, Math.Min(Math.Max(0, contentH - viewH), collabScroll)); regs.Add(new HitReg { R = area, OnWheel = d => collabScroll = Math.Max(0, Math.Min(Math.Max(0, contentH - viewH), collabScroll - d * 60)) });
        var gs = g.Save(); g.SetClip(area, CombineMode.Intersect); float cw = Math.Min(1000, W - 110), cx = (W - 56 - cw) / 2f;
        foreach (var c in collabList)
        {
            var row = Rc(cx, cy, cw, 88); cy += 96; if (row.Bottom < y || row.Y > bottom) continue; var cc = c; bool active = c.State == "active", incoming = c.State == "invited" && !c.Inviter;
            FillRR(g, TM.C(240, 16, 9, 36), row, 16); Win.DrawNeonBox(g, row, false); var av = Rc(row.X + 14, row.Y + 14, 60, 60); Bitmap ab = c.With.Avatar ? ArtistImage(c.With.Id, "avatar", c.With.Av, 60, 60) : null; DrawFrameImage(g, av, ab, ProfAccent(c.With.Accent));
            Txt(g, c.Title, 16, Ink, Rc(av.Right + 14, row.Y + 10, cw - 560, 24), StringAlignment.Near, true);
            Txt(g, "WITH " + c.With.Name.ToUpper() + "   -   " + (active ? "ACTIVE" : (c.Inviter ? "WAITING FOR THEM TO ACCEPT" : "THEY INVITED YOU")), 11, active ? NeonCyan : NeonPink, Rc(av.Right + 14, row.Y + 36, cw - 560, 18), StringAlignment.Near, true);
            if (active) Txt(g, "YOUR VERSION: " + (c.MineSize > 0 ? "SENT (" + (c.MineSize / 1048576.0).ToString("0.0") + " MB)" : "NOT SENT YET") + "     THEIR VERSION: " + (c.TheirSize > 0 ? "READY (" + (c.TheirSize / 1048576.0).ToString("0.0") + " MB)" : "NOT SENT YET"), 10, DimC, Rc(av.Right + 14, row.Y + 58, cw - 560, 18), StringAlignment.Near, false);
            float bx = row.Right - 14;
            if (incoming) { var no = Rc(bx - 120, row.Y + 26, 120, 36); Btn(g, no, "DECLINE", 3, () => CollabAnswerClick(cc, false), 11); var ok = Rc(no.X - 128, row.Y + 26, 120, 36); Btn(g, ok, "ACCEPT", 2, () => CollabAnswerClick(cc, true), 11); }
            else
            {
                var end = Rc(bx - 90, row.Y + 26, 90, 36); Btn(g, end, active ? "END" : "CANCEL", 3, () => CollabAnswerClick(cc, false), 11); bx = end.X - 8;
                if (active) { var get = Rc(bx - 190, row.Y + 26, 190, 36); Btn(g, get, "GET THEIR VERSION", c.TheirSize > 0 ? 2 : 0, () => { if (cc.TheirSize > 0) CollabGetClick(cc); else Toast(cc.With.Name + " has not sent a version yet."); }, 11); var snd = Rc(get.X - 190, row.Y + 26, 180, 36); Btn(g, snd, "SEND MY VERSION", 1, () => CollabSendClick(cc), 11); }
            }
        }
        g.Restore(gs); if (collabList.Count == 0) TxtWrap(g, collabFetching ? "LOADING..." : "NO COLLABS YET. OPEN A FRIEND'S ARTIST PAGE AND PRESS INVITE TO COLLAB.", 14, DimC, Rc(40, y + 20, W - 140, 40));
        VScroll(g, Rc(W - 52, y, 42, viewH - 6), contentH + 6, viewH, () => collabScroll, v => collabScroll = v);
    }
    float collabScroll;
}
