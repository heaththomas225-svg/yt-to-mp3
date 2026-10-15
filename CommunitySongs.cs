using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

// Publishing a project puts the song itself on the community page of the website (members listen, like and comment there).
// The app has no sign-in: the secret key of your artist profile proves whose song it is, the same key that publishes your profile.
// communitysongs.txt remembers which projects are up there: pid | path | song id ("-" = published before songs went up: not uploaded unless you publish it again).
static class CommunitySongs
{
    public class Rec { public string Pid = "", Path = "", SongId = ""; public long Stamp; }   // Stamp: the save of the project that is up there (its file time)
    public static long StampOf(string path) { try { return File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks : 0; } catch { return 0; } }
    static readonly object lk = new object();
    static string FileName { get { return System.IO.Path.Combine(StudioEnv.DataDir, "communitysongs.txt"); } }
    public static bool Exists { get { return File.Exists(FileName); } }

    // a short name for a project that stays the same as long as the project file stays where it is
    public static string PidOf(string path)
    {
        unchecked { uint h = 2166136261; foreach (char c in (path ?? "").ToLowerInvariant()) { h ^= c; h *= 16777619; } return "p" + h.ToString("x8"); }
    }
    public static List<Rec> Load()
    {
        var l = new List<Rec>();
        lock (lk) { try { if (File.Exists(FileName)) foreach (var line in File.ReadAllLines(FileName)) { var f = line.Split('|'); if (f.Length >= 3 && f[0].Length > 0) { long st; if (f.Length < 4 || !long.TryParse(f[3], out st)) st = StampOf(f[1]); l.Add(new Rec { Pid = f[0], Path = f[1], SongId = f[2], Stamp = st }); }   /* (a song uploaded before this counts as the current save: nothing is sent again by itself) */ } } catch { } }
        return l;
    }
    public static void Save(List<Rec> l)
    {
        lock (lk)
        {
            try
            {
                Directory.CreateDirectory(StudioEnv.DataDir); var sb = new StringBuilder(); foreach (var r in l) sb.Append(r.Pid).Append('|').Append((r.Path ?? "").Replace("|", "/")).Append('|').Append(r.SongId).Append('|').Append(r.Stamp).Append('\n');
                string tmp = FileName + ".tmp"; File.WriteAllText(tmp, sb.ToString(), Encoding.UTF8); if (File.Exists(FileName)) File.Delete(FileName); File.Move(tmp, FileName);
            }
            catch { }
        }
    }
    // the first time: the projects that were published before this existed are marked, so nothing is uploaded that was published under the old promise (never the audio)
    public static void MarkOld(IEnumerable<string> paths)
    {
        if (Exists) return; var l = new List<Rec>(); foreach (var p in paths) l.Add(new Rec { Pid = PidOf(p), Path = p, SongId = "-" }); Save(l);
    }

    public static string Say(string err)
    {
        switch (err)
        {
            case "profile": return "Publish your profile first (Profile tab), so people know who made the song.";
            case "private": return "Your profile is private, so its songs cannot go on the community page. Make your profile public in the Profile tab.";
            case "rate": return "You have put a lot of songs up this hour. Try again a little later.";
            case "big": return "The song is bigger than 30 MB (about 12 minutes as an MP3), which is the most the community takes.";
            case "quota": return "You have used all the room you have on the community page. Delete one of your songs there first.";
            case "type": return "The exported song could not be read. Try again.";
            case "title": return "The project needs a name with at least 2 letters.";
            case "key": return "Your profile key is missing. Open the Profile tab once.";
            default: return "The community website said no (" + err + ").";
        }
    }

    static void Part(MemoryStream ms, string boundary, string name, string value) { var b = Encoding.UTF8.GetBytes("--" + boundary + "\r\nContent-Disposition: form-data; name=\"" + name + "\"\r\n\r\n" + value + "\r\n"); ms.Write(b, 0, b.Length); }
    static void FilePart(MemoryStream ms, string boundary, string name, string fileName, string type, byte[] data)
    {
        var h = Encoding.UTF8.GetBytes("--" + boundary + "\r\nContent-Disposition: form-data; name=\"" + name + "\"; filename=\"" + fileName + "\"\r\nContent-Type: " + type + "\r\n\r\n"); ms.Write(h, 0, h.Length); ms.Write(data, 0, data.Length); ms.WriteByte(13); ms.WriteByte(10);
    }
    // null = it worked (songId is set), otherwise a sentence for the person
    public static string Upload(string key, string pid, string title, string tags, string desc, string audioPath, string coverPath, out string songId)
    {
        songId = "";
        try
        {
            byte[] audio = File.ReadAllBytes(audioPath); if (audio.Length > 30 * 1024 * 1024) return Say("big");
            string boundary = "----purphex" + Guid.NewGuid().ToString("N"); var ms = new MemoryStream();
            Part(ms, boundary, "key", key); Part(ms, boundary, "pid", pid); Part(ms, boundary, "title", title ?? ""); Part(ms, boundary, "tags", tags ?? ""); Part(ms, boundary, "desc", desc ?? ""); Part(ms, boundary, "rights", "1");
            FilePart(ms, boundary, "audio", "song.mp3", "audio/mpeg", audio);
            if (!string.IsNullOrEmpty(coverPath) && File.Exists(coverPath)) { var fi = new FileInfo(coverPath); string ext = fi.Extension.ToLowerInvariant(); if (fi.Length < 3 * 1024 * 1024 && (ext == ".jpg" || ext == ".jpeg" || ext == ".png")) FilePart(ms, boundary, "cover", "cover" + ext, ext == ".png" ? "image/png" : "image/jpeg", File.ReadAllBytes(coverPath)); }
            var end = Encoding.UTF8.GetBytes("--" + boundary + "--\r\n"); ms.Write(end, 0, end.Length);
            var rq = (HttpWebRequest)WebRequest.Create(CommunityPresets.BaseUrl + "/api/profiles/song"); rq.Method = "POST"; rq.UserAgent = "PurphexMusic"; rq.Timeout = 600000; rq.ReadWriteTimeout = 180000; rq.ContentType = "multipart/form-data; boundary=" + boundary; rq.ContentLength = ms.Length; rq.AllowWriteStreamBuffering = false;
            using (var s = rq.GetRequestStream()) { ms.Position = 0; ms.CopyTo(s); }
            string body; try { using (var r = (HttpWebResponse)rq.GetResponse()) using (var sr = new StreamReader(r.GetResponseStream(), Encoding.UTF8)) body = sr.ReadToEnd(); }
            catch (WebException ex) { if (ex.Response == null) throw; using (var sr = new StreamReader(ex.Response.GetResponseStream(), Encoding.UTF8)) body = sr.ReadToEnd(); }
            var o = MiniJson.Parse(body) as Dictionary<string, object>; if (o == null) return "The community website gave an answer that could not be read.";
            object ok, id, er; if (o.TryGetValue("ok", out ok) && ok is bool && (bool)ok) { songId = o.TryGetValue("id", out id) && id is string ? (string)id : ""; return songId.Length > 0 ? null : "The community website gave an answer that could not be read."; }
            return Say(o.TryGetValue("err", out er) && er is string ? (string)er : "unknown");
        }
        catch (Exception ex) { return "Could not reach the Purphex community (" + ex.Message + "). Check your internet connection."; }
    }
    // a published project was renamed: the song on the community takes the new name straight away (null = it worked, otherwise a sentence)
    public static string Rename(string key, string pid, string title)
    {
        try
        {
            var rq = (HttpWebRequest)WebRequest.Create(CommunityPresets.BaseUrl + "/api/profiles/song/rename"); rq.Method = "POST"; rq.UserAgent = "PurphexMusic"; rq.Timeout = 20000; rq.ContentType = "application/json";
            var b = Encoding.UTF8.GetBytes("{\"key\":" + ProfApi.J(key) + ",\"pid\":" + ProfApi.J(pid) + ",\"title\":" + ProfApi.J(title) + "}"); rq.ContentLength = b.Length; using (var s = rq.GetRequestStream()) s.Write(b, 0, b.Length);
            using (var r = (HttpWebResponse)rq.GetResponse()) using (var sr = new StreamReader(r.GetResponseStream(), Encoding.UTF8)) { var o = MiniJson.Parse(sr.ReadToEnd()) as Dictionary<string, object>; object ok; return o != null && o.TryGetValue("ok", out ok) && ok is bool && (bool)ok ? null : "The community did not take the new name."; }
        }
        catch (Exception ex) { return "Could not reach the Purphex community (" + ex.Message + ")."; }
    }
    // null = it worked, otherwise a sentence
    public static string Remove(string key, string pid)
    {
        try
        {
            var rq = (HttpWebRequest)WebRequest.Create(CommunityPresets.BaseUrl + "/api/profiles/song/remove"); rq.Method = "POST"; rq.UserAgent = "PurphexMusic"; rq.Timeout = 20000; rq.ContentType = "application/json";
            var b = Encoding.UTF8.GetBytes("{\"key\":" + ProfApi.J(key) + ",\"pid\":" + ProfApi.J(pid) + "}"); rq.ContentLength = b.Length; using (var s = rq.GetRequestStream()) s.Write(b, 0, b.Length);
            using (var r = (HttpWebResponse)rq.GetResponse()) using (var sr = new StreamReader(r.GetResponseStream(), Encoding.UTF8)) { var o = MiniJson.Parse(sr.ReadToEnd()) as Dictionary<string, object>; object ok; return o != null && o.TryGetValue("ok", out ok) && ok is bool && (bool)ok ? null : "The community website said no."; }
        }
        catch (Exception ex) { return "Could not reach the Purphex community (" + ex.Message + ")."; }
    }
}

partial class StudioForm
{
    volatile bool songSyncing, songSyncAgain;
    internal Func<ProjectEntry, string> songRenderHook;   // a test answers for itself: it returns the path of a finished mp3
    internal Action<string> songNoteHook;
    // the projects published before this existed are marked once, so nothing is uploaded that was published under the old promise (never the audio)
    void EnsureSongMarks()
    {
        if (CommunitySongs.Exists) return; var paths = new List<string>(); foreach (var x in ProjectLibrary.AllByEdited()) if (x.OnProfile) paths.Add(x.Path); CommunitySongs.MarkOld(paths);
    }
    void SongNote(string s) { if (songNoteHook != null) { songNoteHook(s); return; } try { BeginInvoke(new Action(() => Toast(s))); } catch { } }
    // keeps the community page in step with the projects you published: a project that is published and not up there yet is exported and uploaded, one that is private again (or gone) is taken down
    internal void SyncCommunitySongs()
    {
        if (!ProfileStore.Published || ProfileStore.Key.Length < 32) return; EnsureSongMarks();
        if (songSyncing) { songSyncAgain = true; return; }
        songSyncing = true; string key = ProfileStore.Key; var ents = new List<ProjectEntry>(ProjectLibrary.AllByEdited());
        new System.Threading.Thread(() =>
        {
            try { SongSyncRun(key, ents); }
            catch (Exception ex) { SongNote("The community page could not be updated: " + ex.Message); }
            finally { songSyncing = false; if (songSyncAgain) { songSyncAgain = false; try { BeginInvoke(new Action(SyncCommunitySongs)); } catch { } } }
        }) { IsBackground = true, Name = "community-songs" }.Start();
    }
    internal void SongSyncRun(string key, List<ProjectEntry> ents)
    {
        var recs = CommunitySongs.Load();
        foreach (var r in recs.ToArray())
        {
            if (ents.Exists(x => x.OnProfile && string.Equals(x.Path, r.Path, StringComparison.OrdinalIgnoreCase))) continue;
            if (r.SongId == "-") { recs.Remove(r); continue; }
            string err = CommunitySongs.Remove(key, r.Pid); if (err == null) { recs.Remove(r); SongNote("A song was taken off the community page."); } else SongNote(err);
        }
        CommunitySongs.Save(recs);
        foreach (var e in ents)
        {
            if (!e.OnProfile || e.Missing) continue; string pid = CommunitySongs.PidOf(e.Path); var had = recs.Find(x => x.Pid == pid); long stamp = CommunitySongs.StampOf(e.Path);
            if (had != null && (had.SongId == "-" || stamp <= had.Stamp + TimeSpan.TicksPerSecond * 2)) continue;   // up there already, and not saved since
            bool update = had != null; SongNote(update ? "You saved \"" + e.Name + "\": updating it on your profile and the community page (exporting)..." : "Putting \"" + e.Name + "\" on the community page: exporting the song...");
            string mp3 = null, err = null, id = ""; try { mp3 = RenderSong(e); } catch (Exception ex) { err = ex.Message; }
            if (mp3 != null)
            {
                SongNote(update ? "Updating \"" + e.Name + "\" on the community page: uploading..." : "Putting \"" + e.Name + "\" on the community page: uploading...");
                err = CommunitySongs.Upload(key, pid, e.Name, e.Tags, e.Desc, mp3, e.Cover.Length > 0 ? e.Cover : null, out id);
                try { File.Delete(mp3); string d = Path.GetDirectoryName(mp3); if (d != null && d.IndexOf("purphex_comm_", StringComparison.Ordinal) >= 0) Directory.Delete(d); } catch { }
            }
            if (err == null) { if (had != null) recs.Remove(had); recs.Add(new CommunitySongs.Rec { Pid = pid, Path = e.Path, SongId = id, Stamp = stamp }); CommunitySongs.Save(recs); SongNote(update ? "\"" + e.Name + "\" is updated on the community page: everyone hears the new version (plays, likes and comments are kept)." : "\"" + e.Name + "\" is on the community page of the website."); }
            else SongNote(update ? "\"" + e.Name + "\" could not be updated on the community page (it tries again next time you save): " + err : "\"" + e.Name + "\" is on your profile, but not on the community page: " + err);
        }
    }
    // the song as a finished mp3 (mixed and mastered, exactly like EXPORT), made from the project file without opening it
    string RenderSong(ProjectEntry e)
    {
        if (songRenderHook != null) return songRenderHook(e);
        if (StudioEnv.Ffmpeg() == null) throw new Exception("ffmpeg is not installed yet: convert something once in the main window, then publish again.");
        // the load fills its own ProjMeta and never touches the open project's tempo, lyrics or picture (saving and restoring them raced with the UI and with other background loads)
        StudioEngine pe = null; double loaded; var meta = new ProjectIO.ProjMeta();
        try { pe = new StudioEngine(); StudioEngine.Current = eng; ProjectIO.Load(pe, e.Path, s => { }, meta); }
        catch { if (pe != null) { try { pe.Shutdown(); } catch { } } throw; }
        loaded = double.IsNaN(meta.Bpm) ? Fx.ProjectBpm : meta.Bpm;
        try
        {
            if (pe.EndFrame < 1000) throw new Exception("the project is empty.");
            double keepR = Fx.RenderBpm; Fx.RenderBpm = loaded;
            try { var plan = Exporter.Plan(pe, true, false, false, false, false, false, false, "song", 4); string dir = Path.Combine(Path.GetTempPath(), "purphex_comm_" + Guid.NewGuid().ToString("N").Substring(0, 8)); return Exporter.Write(pe, plan[0], dir, 4, null, null, 44100); }
            finally { Fx.RenderBpm = keepR; }
        }
        finally { try { pe.Shutdown(); } catch { } }
    }
}
