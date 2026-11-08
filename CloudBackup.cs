using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;

// Cloud backup under your Purphex account. When this PC is linked to your account, the project vault (every project you saved and the audio it uses)
// and the sounds you imported (My samples) are copied to purphexmusic.com, into this PC's own part of your backup. Every other PC linked to the
// same account brings them down: their songs go on its Projects and their sounds become a "My samples (from <PC>)" pack.
// Safety first: a PC only ever writes its own part, a file lost from this PC is fetched back (never removed from the cloud), files from other PCs
// live in their own folders (they never overwrite anything of yours), and the server keeps replaced and removed files for 30 days.
static class CloudBackup
{
    public static string BaseOverride, TokenOverride, FpOverride;   // a test points it at its own server and account
    static string Base { get { return BaseOverride ?? CommunityPresets.BaseUrl; } }
    static string Token { get { return TokenOverride ?? Edition.Token; } }
    static string Fp { get { return FpOverride ?? Edition.Fingerprint; } }
    static string StateFile { get { return Path.Combine(StudioEnv.DataDir, "cloud", "synced.txt"); } }
    static string SamplesDir { get { return Path.Combine(SoundLibrary.Root, SoundLibrary.UserPackId); } }
    public static string OthersDir { get { return Path.Combine(StudioEnv.DataDir, "vault-others"); } }   // other PCs' vaults (ProjectVault reads them too)
    static readonly char Sep = Path.DirectorySeparatorChar;
    static readonly StringComparison PathCmp = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static volatile string Status = "";   // a short line for the Projects page
    public static long Used, Quota, Room = long.MaxValue, MaxFile = 1024L * 1024 * 1024; public static int Files, Sent, Got, Waiting, TooBig; public static string MyPc = "";
    public static int Failed;
    // this PC's part, remembered (the vault uses it to know which songs are its own)
    public static string SavedPc { get { if (MyPc.Length == 15) return MyPc; try { string f = Path.Combine(StudioEnv.DataDir, "cloud", "pc.txt"); if (File.Exists(f)) { string v = File.ReadAllText(f).Trim(); if (v.Length == 15) return v; } } catch { } return ""; } }
    // runs on the studio's own thread (set by the studio): reloading the sounds or the project list never races the screen
    public static Action<Action> OnUi;
    static void Ui(Action a) { var u = OnUi; if (u != null) { try { u(a); } catch { } } else try { a(); } catch { } }
    static readonly StringComparer Keys = Path.DirectorySeparatorChar == '\\' ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    public static volatile bool Busy; static int loopOn; static readonly System.Threading.AutoResetEvent kick = new System.Threading.AutoResetEvent(false);
    static readonly object lk = new object();

    public static bool Linked { get { return Token.Length > 0; } }

    // starts the background sync once a session (and then every 15 minutes, or soon after a save)
    public static void Start()
    {
        if (StudioEnv.UnderTest || Environment.GetEnvironmentVariable("PURPHEX_TESTRUN") == "1") return;   /* a test run never touches your account (tests call Sync themselves) */
        if (System.Threading.Interlocked.Exchange(ref loopOn, 1) == 1) { kick.Set(); return; }
        var th = new System.Threading.Thread(() =>
        {
            System.Threading.Thread.Sleep(8000);   /* the studio opens first */
            while (true) { try { Sync(); } catch (Exception ex) { Status = "Backup paused: " + ex.Message; } kick.WaitOne(TimeSpan.FromMinutes(15)); System.Threading.Thread.Sleep(20000); }
        });
        th.IsBackground = true; th.Priority = System.Threading.ThreadPriority.Lowest; th.Start();
    }
    // a project was just kept in the vault, or a sound imported: back it up soon
    public static void Soon() { if (System.Threading.Thread.VolatileRead(ref loopOn) == 1) kick.Set(); }

    sealed class Rec { public string Sha = ""; public long Size, Ticks; public bool Up = true; }   // what a file was when it was last in step with the cloud (Up = false: only its hash is remembered, it is not up yet)
    sealed class Remote { public string Sha = ""; public long Size, Mtime; }

    // one full pass: send what is new or changed on this PC, fetch back what this PC lost, and bring down what the other PCs have
    public static bool Sync()
    {
        lock (lk)
        {
            if (!Linked) { Status = "Link this PC to your account to back up your songs and sounds"; return false; }
            Busy = true; Sent = 0; Got = 0; Waiting = 0; TooBig = 0; Failed = 0;
            try
            {
                var remote = List(); if (remote == null) return false; var state = ReadState();
                try { Directory.CreateDirectory(Path.GetDirectoryName(StateFile)); File.WriteAllText(Path.Combine(Path.GetDirectoryName(StateFile), "pc.txt"), MyPc); } catch { }
                string mine = MyPc + "/"; var shas = new HashSet<string>(); foreach (var r in remote.Values) shas.Add(r.Sha);
                try { string nf = Path.Combine(ProjectVault.Dir, "pcname.txt"); Directory.CreateDirectory(ProjectVault.Dir); if (!File.Exists(nf) || File.ReadAllText(nf).Trim() != Environment.MachineName) File.WriteAllText(nf, Environment.MachineName); } catch { }   /* other PCs show where the sounds came from */
                // ---- this PC's own part ----
                bool gotProjects = false; var gotPacks = new HashSet<string>();
                if (ReconcileSamples(remote, state, mine)) gotPacks.Add(SoundLibrary.UserPackId);   // a sound list in the cloud this PC has not seen (after a reinstall): both lists are kept
                var local = new Dictionary<string, string>(Keys); bool listedAll = Collect("vault/", ProjectVault.Dir, local) & Collect("samples/", SamplesDir, local);
                foreach (var kv in local)
                {
                    string rel = kv.Key, file = kv.Value; FileInfo fi; try { fi = new FileInfo(file); if (!fi.Exists) continue; } catch { continue; }
                    if (fi.Length > MaxFile) { TooBig++; continue; }
                    Rec was; state.TryGetValue(rel, out was); string sha = (was != null && was.Size == fi.Length && was.Ticks == fi.LastWriteTimeUtc.Ticks) ? was.Sha : Hash(file); if (sha == null) continue;
                    Remote rm; remote.TryGetValue(mine + rel, out rm);
                    if (rm == null || rm.Sha != sha) { if (!Put(mine + rel, file, sha, fi, shas.Contains(sha))) { state[rel] = new Rec { Sha = sha, Size = fi.Length, Ticks = fi.LastWriteTimeUtc.Ticks, Up = false }; continue; } shas.Add(sha); Sent++; }   // this PC is the only one that writes its part: what is here is what goes up
                    state[rel] = new Rec { Sha = sha, Size = fi.Length, Ticks = fi.LastWriteTimeUtc.Ticks };
                }
                var unmark = new List<string>();
                foreach (var kv in remote)
                {
                    if (!kv.Key.StartsWith(mine, StringComparison.Ordinal)) continue; string rel = kv.Key.Substring(mine.Length); if (local.ContainsKey(rel)) continue; string file = LocalPath(kv.Key); if (file == null) continue;
                    Rec was; state.TryGetValue(rel, out was);
                    bool marker = rel.EndsWith(".removed", StringComparison.OrdinalIgnoreCase);
                    if (marker && was != null && was.Up && was.Sha == kv.Value.Sha) { if (listedAll) unmark.Add(kv.Key); continue; }   // put back on the list here (the record goes once the cloud has let go of it)
                    if (File.Exists(file)) continue;
                    if (marker) { string tmpm = file + ".chk"; if (Fetch(kv.Key, tmpm, kv.Value, true)) { string mp = ""; try { mp = File.ReadAllText(tmpm).Trim(); } catch { } bool listed = mp.Length > 0 && ProjectLibrary.Find(mp) != null; if (listed) { try { File.Delete(tmpm); } catch { } unmark.Add(kv.Key); continue; } try { File.Move(tmpm, file); } catch { try { File.Delete(tmpm); } catch { } continue; } var fm = new FileInfo(file); state[rel] = new Rec { Sha = kv.Value.Sha, Size = fm.Length, Ticks = fm.LastWriteTimeUtc.Ticks }; } continue; }   // a song that is on the list now is never hidden by an old marker
                    if (Fetch(kv.Key, file, kv.Value, false)) { var fi = new FileInfo(file); state[rel] = new Rec { Sha = kv.Value.Sha, Size = fi.Length, Ticks = fi.LastWriteTimeUtc.Ticks }; Got++; if (rel.StartsWith("vault/projects/")) gotProjects = true; if (rel.StartsWith("samples/")) gotPacks.Add(SoundLibrary.UserPackId); }   // lost from this PC: fetched back
                }
                for (int i = 0; i < unmark.Count; i += 200) { var part = unmark.GetRange(i, Math.Min(200, unmark.Count - i)); if (RemoveRemote(part)) foreach (var u in part) state.Remove(u.Substring(mine.Length)); }
                // ---- the other PCs on the account ----
                var packFiles = new Dictionary<string, string>(); var claimed = new HashSet<string>(Keys);
                foreach (var kv in remote)
                {
                    if (kv.Key.StartsWith(mine, StringComparison.Ordinal) || kv.Key.EndsWith(".removed", StringComparison.OrdinalIgnoreCase)) continue; string file = LocalPath(kv.Key); if (file == null) continue;
                    if (!claimed.Add(file)) continue;   /* two cloud names for one file here (different case): only the first */
                    string dev = kv.Key.Substring(0, kv.Key.IndexOf('/')); if (kv.Key.EndsWith("/samples/pack.txt", StringComparison.Ordinal)) packFiles[dev] = file;
                    Rec was; state.TryGetValue(kv.Key, out was); if (was != null && was.Sha == kv.Value.Sha && File.Exists(file)) continue;
                    if (Fetch(kv.Key, file, kv.Value, true)) { state[kv.Key] = new Rec { Sha = kv.Value.Sha }; Got++; if (kv.Key.Contains("/vault/projects/")) gotProjects = true; if (kv.Key.Contains("/samples/")) gotPacks.Add(dev); }
                }
                foreach (var dev in gotPacks) { string cf; if (dev != SoundLibrary.UserPackId && packFiles.TryGetValue(dev, out cf)) MakeOtherPack(dev, cf); }
                WriteState(state);
                if (gotProjects) try { ProjectVault.AdoptOthers(); } catch { }   // the songs from your other PCs become songs of this PC ("... (from <PC>)")
                if (gotPacks.Count > 0) Ui(() => SoundLibrary.Load());
                if (gotProjects) Ui(() => ProjectLibrary.Discover());   // and any this PC lost go back on your Projects
                var after = List(); int n = 0; if (after != null) foreach (var k in after.Keys) if (k.StartsWith(mine, StringComparison.Ordinal)) n++; Files = n;
                string extra = (Failed > 0 ? ". " + Failed + " file" + (Failed == 1 ? "" : "s") + " could not be sent yet (safe on this PC, trying again)" : "") + (Got > 0 ? " (" + Got + " brought down)" : "") + (TooBig > 0 ? ". " + TooBig + " file" + (TooBig == 1 ? " is" : "s are") + " over " + Mb(MaxFile) + " and only kept on this PC" : "");
                if (Waiting > 0) Status = Waiting + " file" + (Waiting == 1 ? "" : "s") + " waiting for space in the cloud backup (" + n + " backed up). They are safe on this PC and go up as soon as there is room." + extra;
                else if (Failed > 0) Status = Failed + " file" + (Failed == 1 ? "" : "s") + " could not be sent yet. They are safe on this PC and Purphex keeps trying (" + n + " backed up).";
                else Status = "Backed up to your account: " + n + " files, " + Mb(Used) + " of " + Mb(Quota) + extra;
                return true;
            }
            catch (Exception ex) { Status = "Backup paused: " + ex.Message; return false; }
            finally { Busy = false; }
        }
    }
    static string Mb(long b) { return b >= 1024L * 1024 * 1024 ? (b / (1024.0 * 1024 * 1024)).ToString("0.0") + " GB" : (b / (1024.0 * 1024)).ToString("0") + " MB"; }
    static long ToMs(DateTime utc) { return (long)(utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds; }

    // ---- the files ----
    // every file under dir, one folder at a time (a folder that cannot be read is skipped, and false says the list is not complete)
    static bool Collect(string prefix, string dir, Dictionary<string, string> into)
    {
        if (!Directory.Exists(dir)) return true; bool all = true; var todo = new Stack<string>(); todo.Push(dir);
        while (todo.Count > 0)
        {
            string d = todo.Pop();
            try { foreach (var s in Directory.GetDirectories(d)) todo.Push(s); } catch { all = false; }
            try
            {
                foreach (var f in Directory.GetFiles(d))
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant(); if (ext == ".tmp" || ext == ".part") continue;
                    string rel = prefix + f.Substring(dir.Length).TrimStart('\\', '/').Replace('\\', '/'); if (CleanRel(rel) == null) continue; into[rel] = f;
                }
            }
            catch { all = false; }
        }
        return all;
    }
    // the same rules as the server, for the part after pc-<id>/: vault/... or samples/..., plain parts, nothing Windows cannot use
    public static string CleanRel(string p)
    {
        if (string.IsNullOrEmpty(p) || p.Length < 3 || p.Length > 400) return null; if (!p.StartsWith("vault/") && !p.StartsWith("samples/")) return null;
        foreach (var s in p.Split('/')) { if (s.Length == 0 || s == "." || s == ".." || s.Length > 200 || s.EndsWith(" ") || s.EndsWith(".")) return null; foreach (char c in s) if (c < 32 || "\\:*?\"<>|".IndexOf(c) >= 0) return null; }
        return p;
    }
    public static string CleanPath(string p)
    {
        if (string.IsNullOrEmpty(p) || p.Length < 19 || !p.StartsWith("pc-") || p[15] != '/') return null;
        for (int i = 3; i < 15; i++) if ("0123456789abcdef".IndexOf(p[i]) < 0) return null;
        return CleanRel(p.Substring(16)) == null ? null : p;
    }
    // where a backup path lives on this PC: this PC's own part is its vault and My samples; another PC's part has folders of its own
    static string LocalPath(string full)
    {
        if (CleanPath(full) == null) return null; string dev = full.Substring(0, 15), rel = full.Substring(16), root, rest;
        bool own = dev == MyPc;
        if (rel.StartsWith("vault/")) { root = own ? ProjectVault.Dir : Path.Combine(OthersDir, dev); rest = rel.Substring(6); }
        else { root = own ? SamplesDir : Path.Combine(SoundLibrary.Root, dev); rest = rel.Substring(8); if (!own && rest == "pack.txt") rest = "pack.cloud.txt"; }   // another PC's sound list is turned into a pack of its own
        string full2 = Path.GetFullPath(Path.Combine(root, rest.Replace('/', Sep))); string rp = Path.GetFullPath(root).TrimEnd(Sep) + Sep;
        return full2.StartsWith(rp, PathCmp) ? full2 : null;
    }
    static string Hash(string file)
    {
        try { using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) using (var sha = SHA1.Create()) return BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant(); }
        catch { return null; }
    }
    // another PC's sounds: the same list with the pack's own id and name, so it shows as "My samples (from <PC>)" and never mixes with yours
    static void MakeOtherPack(string dev, string cloudList)
    {
        try
        {
            var lines = File.ReadAllText(cloudList, Encoding.UTF8).Replace("\r", "").Split('\n'); if (lines.Length == 0 || !lines[0].StartsWith("PACK|")) return;
            var f = lines[0].Split('|'); if (f.Length < 10) return; string pcName = dev; try { string nf = Path.Combine(OthersDir, dev, "pcname.txt"); if (File.Exists(nf)) pcName = File.ReadAllText(nf).Trim().Replace("|", " "); } catch { }
            f[1] = dev; f[2] = "My samples (from " + pcName + ")"; lines[0] = string.Join("|", f);
            string dir = Path.GetDirectoryName(cloudList), tmp = Path.Combine(dir, "pack.txt.tmp"), fin = Path.Combine(dir, "pack.txt");
            File.WriteAllText(tmp, string.Join("\n", lines), Encoding.UTF8); if (File.Exists(fin)) File.Delete(fin); File.Move(tmp, fin);
        }
        catch { }
    }

    // ---- the server ----
    static HttpWebRequest Req(string method, string path, int timeoutMs)
    {
        var rq = (HttpWebRequest)WebRequest.Create(Base + path); rq.Method = method; rq.UserAgent = "PurphexMusic"; rq.Timeout = timeoutMs; rq.ReadWriteTimeout = timeoutMs;
        rq.Headers["Authorization"] = "Bearer " + Token; rq.Headers["X-Device"] = Fp; return rq;
    }
    static string Read(HttpWebRequest rq, out int status)
    {
        try { using (var rs = (HttpWebResponse)rq.GetResponse()) { status = (int)rs.StatusCode; using (var sr = new StreamReader(rs.GetResponseStream(), Encoding.UTF8)) return sr.ReadToEnd(); } }
        catch (WebException ex) { var r = ex.Response as HttpWebResponse; if (r == null) throw; status = (int)r.StatusCode; using (r) using (var sr = new StreamReader(r.GetResponseStream(), Encoding.UTF8)) return sr.ReadToEnd(); }
    }
    static Dictionary<string, Remote> List()
    {
        int st; string body;
        try { body = Read(Req("GET", "/api/backup/list", 30000), out st); } catch (WebException) { Status = "Backup paused: no connection to purphexmusic.com"; return null; }
        if (st == 401 || st == 403) { Status = "Link this PC to your account again to back up (Account)"; return null; }
        if (st != 200) { Status = "Backup paused: the website said " + st; return null; }
        var o = MiniJson.Parse(body) as Dictionary<string, object>; if (o == null) return null;
        string pc = o.ContainsKey("pc") ? Convert.ToString(o["pc"]) : ""; if (pc.Length != 15 || !pc.StartsWith("pc-")) { Status = "Backup paused: the website needs updating"; return null; }
        MyPc = pc; Used = Num(o, "used"); Quota = Num(o, "quota"); Room = o.ContainsKey("room") ? Num(o, "room") : long.MaxValue; if (o.ContainsKey("maxFile")) MaxFile = Num(o, "maxFile");
        var d = new Dictionary<string, Remote>(StringComparer.Ordinal); var arr = (o.ContainsKey("files") ? o["files"] : null) as List<object>;
        if (arr != null) foreach (var x in arr) { var f = x as Dictionary<string, object>; if (f == null || !f.ContainsKey("p")) continue; string p = Convert.ToString(f["p"]); if (CleanPath(p) == null) continue; d[p] = new Remote { Sha = f.ContainsKey("sha") ? Convert.ToString(f["sha"]) : "", Size = Num(f, "size"), Mtime = Num(f, "mtime") }; }
        return d;
    }
    static string JStr(string v) { var sb = new StringBuilder("\""); foreach (char c in v) { if (c == '"' || c == '\\') sb.Append('\\').Append(c); else if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4")); else sb.Append(c); } return sb.Append('"').ToString(); }
    static long Num(Dictionary<string, object> o, string k) { try { return o.ContainsKey(k) && o[k] != null ? Convert.ToInt64(o[k], System.Globalization.CultureInfo.InvariantCulture) : 0; } catch { return 0; } }
    static string Q(string p, string sha, long size, long mtime) { return "/api/backup/file?p=" + Uri.EscapeDataString(p) + "&sha=" + sha + "&size=" + size + "&mtime=" + mtime; }
    // sends one file (first without the bytes when the account already has the same file somewhere)
    static bool Put(string p, string file, string sha, FileInfo fi, bool serverHasIt)
    {
        long mt = ToMs(fi.LastWriteTimeUtc);
        try
        {
            if (serverHasIt) { var r0 = Req("PUT", Q(p, sha, fi.Length, mt), 60000); r0.ContentLength = 0; int s0; Read(r0, out s0); if (s0 == 200) return true; }
            if (fi.Length > MaxFile) { TooBig++; return false; }
            if (fi.Length > Room) { Waiting++; return false; }   /* no room on the server: nothing is sent for nothing */
            int st = SendBody(Q(p, sha, fi.Length, mt), file, fi.Length);
            if (st == 507) { Room = 0; Waiting++; } else if (st == 413) TooBig++;
            if (st == 200) { Room -= fi.Length; return true; }
            if (st != 507 && st != 413) Failed++; return false;
        }
        catch { Failed++; return false; }   // no connection, or the file changed while it was read: it goes next time
    }
#if PURPHEX_DESKTOP
    // (on .NET 8 HttpWebRequest keeps the whole body in memory, so big files go through HttpClient)
    static readonly System.Net.Http.HttpClient http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromHours(6) };
    static int SendBody(string path, string file, long len)
    {
        using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var msg = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Put, Base + path))
        {
            var sc = new System.Net.Http.StreamContent(new LimitStream(fs, len), 65536); sc.Headers.ContentLength = len; sc.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream"); msg.Content = sc;
            msg.Headers.TryAddWithoutValidation("Authorization", "Bearer " + Token); msg.Headers.TryAddWithoutValidation("X-Device", Fp); msg.Headers.TryAddWithoutValidation("User-Agent", "PurphexMusic");
            using (var rs = http.SendAsync(msg).GetAwaiter().GetResult()) return (int)rs.StatusCode;
        }
    }
    sealed class LimitStream : Stream
    {
        readonly Stream s; long left; public LimitStream(Stream s, long n) { this.s = s; left = n; }
        public override int Read(byte[] b, int o, int c) { if (left <= 0) return 0; int n = s.Read(b, o, (int)Math.Min(c, left)); if (n <= 0) throw new IOException("The file got shorter while it was sent."); left -= n; return n; }
        public override bool CanRead { get { return true; } } public override bool CanSeek { get { return false; } } public override bool CanWrite { get { return false; } }
        public override long Length { get { throw new NotSupportedException(); } } public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
        public override void Flush() { } public override long Seek(long a, SeekOrigin b) { throw new NotSupportedException(); } public override void SetLength(long a) { throw new NotSupportedException(); } public override void Write(byte[] b, int o, int c) { throw new NotSupportedException(); }
    }
#else
    static int SendBody(string path, string file, long len)
    {
        var rq = Req("PUT", path, 1800000); rq.ContentType = "application/octet-stream"; rq.ContentLength = len; rq.AllowWriteStreamBuffering = false; rq.SendChunked = false;
        try
        {
            using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) using (var os = rq.GetRequestStream())
            { var buf = new byte[65536]; long left = len; int n; while (left > 0 && (n = fs.Read(buf, 0, (int)Math.Min(buf.Length, left))) > 0) { os.Write(buf, 0, n); left -= n; } if (left != 0) { rq.Abort(); return 0; } }
        }
        catch { try { rq.Abort(); } catch { } return 0; }
        int st; Read(rq, out st); return st;
    }
#endif
    // fetches one file and puts it in place only when it arrived whole and matches its SHA-1. A file of this PC's own is only ever put where
    // nothing is (replace = false: it never writes over anything); another PC's copy (in its own folder) is replaced by its newer version.
    static bool Fetch(string p, string file, Remote rm, bool replace)
    {
        string tmp = file + ".part";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)); var rq = Req("GET", "/api/backup/file?p=" + Uri.EscapeDataString(p), 1800000);
            using (var rs = (HttpWebResponse)rq.GetResponse()) using (var s = rs.GetResponseStream()) using (var o = new FileStream(tmp, FileMode.Create, FileAccess.Write)) s.CopyTo(o);
            if (Hash(tmp) != rm.Sha) { File.Delete(tmp); return false; }
            if (rm.Mtime > 0) try { File.SetLastWriteTimeUtc(tmp, new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(rm.Mtime)); } catch { }
            if (File.Exists(file)) { if (!replace) { File.Delete(tmp); return false; } File.Delete(file); }
            File.Move(tmp, file);   /* (fails if something appeared there meanwhile: then nothing is overwritten) */
            return true;
        }
        catch { try { if (File.Exists(tmp)) File.Delete(tmp); } catch { } return false; }
    }
    static bool RemoveRemote(List<string> paths)
    {
        var sb = new StringBuilder("{\"p\":["); for (int i = 0; i < paths.Count; i++) { if (i > 0) sb.Append(','); sb.Append(JStr(paths[i])); } sb.Append("]}");
        var bytes = Encoding.UTF8.GetBytes(sb.ToString()); var rq = Req("POST", "/api/backup/remove", 30000); rq.ContentType = "application/json"; rq.ContentLength = bytes.Length;
        try { using (var os = rq.GetRequestStream()) os.Write(bytes, 0, bytes.Length); int st; Read(rq, out st); return st == 200; } catch { return false; }
    }

    // the cloud's sound list of this PC holds sounds this PC's list does not (it was reinstalled, or My samples was wiped): every sound of both is
    // kept. A cloud sound whose name is taken here by a different sound comes back next to it as "<name> (backup)". True if the list changed.
    static bool ReconcileSamples(Dictionary<string, Remote> remote, Dictionary<string, Rec> state, string mine)
    {
        Remote rp; if (!remote.TryGetValue(mine + "samples/pack.txt", out rp)) return false;
        Rec wp; state.TryGetValue("samples/pack.txt", out wp); if (wp != null && wp.Up && wp.Sha == rp.Sha) return false;   // it is the list this PC sent
        string lp = Path.Combine(SamplesDir, "pack.txt"); if (File.Exists(lp) && Hash(lp) == rp.Sha) return false;
        string tmp = Path.Combine(SamplesDir, "pack.cloud.tmp"); Directory.CreateDirectory(SamplesDir);
        if (!Fetch(mine + "samples/pack.txt", tmp, rp, true)) return false;
        try
        {
            var theirs = SoundLibrary.ReadPackFile(tmp, SamplesDir); if (theirs == null) return false; bool changed = false;
            lock (SoundLibrary.Lock)
            {
                var up = SoundLibrary.Pack(SoundLibrary.UserPackId); if (up == null) return false;
                foreach (var t in theirs.Items)
                {
                    Remote rf; remote.TryGetValue(mine + "samples/" + t.Rel, out rf); var have = up.Items.Find(x => string.Equals(x.Rel, t.Rel, StringComparison.OrdinalIgnoreCase));
                    string at = Path.Combine(SamplesDir, t.Rel.Replace('/', Sep)); bool clash = (have != null || File.Exists(at)) && rf != null && File.Exists(at) && Hash(at) != rf.Sha;
                    if (have != null && !clash) continue;
                    var c = (SoundItem)typeof(object).GetMethod("MemberwiseClone", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(t, null);
                    if (clash)
                    {
                        string dir = Path.GetDirectoryName(t.Rel.Replace('/', Sep)) ?? "", stem = Path.GetFileNameWithoutExtension(t.Rel), ext = Path.GetExtension(t.Rel), rel2 = null;
                        for (int k = 1; k < 100; k++) { string cand = (dir.Length > 0 ? dir.Replace(Sep, '/') + "/" : "") + stem + " (backup" + (k > 1 ? " " + k : "") + ")" + ext; if (!File.Exists(Path.Combine(SamplesDir, cand.Replace('/', Sep))) && !up.Items.Exists(x => string.Equals(x.Rel, cand, StringComparison.OrdinalIgnoreCase))) { rel2 = cand; break; } }
                        if (rel2 == null) continue; string to = Path.Combine(SamplesDir, rel2.Replace('/', Sep));
                        if (!Fetch(mine + "samples/" + t.Rel, to, rf, false)) continue;
                        c.Rel = rel2; c.Path = to; c.Id = up.Id + "/" + rel2; c.Name = t.Name + " (backup)";
                    }
                    else { c.Path = at; c.Id = up.Id + "/" + c.Rel; }
                    c.PackId = up.Id; c.PackName = up.Name; up.Items.Add(c); changed = true;
                }
                if (changed) SoundLibrary.WritePack(up);
            }
            return changed;
        }
        catch { return false; }
        finally { try { File.Delete(tmp); } catch { } }
    }

    // ---- what was in step last time (this PC's own files by their path in its part, other PCs' files by their full path) ----
    static Dictionary<string, Rec> ReadState()
    {
        var d = new Dictionary<string, Rec>(Keys);
        try { if (File.Exists(StateFile)) foreach (var line in File.ReadAllLines(StateFile, Encoding.UTF8)) { var p = line.Split('\t'); if (p.Length < 4) continue; long sz, tk; if (!long.TryParse(p[2], out sz) || !long.TryParse(p[3], out tk)) continue; d[p[0]] = new Rec { Sha = p[1], Size = sz, Ticks = tk, Up = !(p.Length > 4 && p[4] == "0") }; } }
        catch { }
        return d;
    }
    static void WriteState(Dictionary<string, Rec> d)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StateFile)); var sb = new StringBuilder();
            foreach (var kv in d) sb.Append(kv.Key).Append('\t').Append(kv.Value.Sha).Append('\t').Append(kv.Value.Size).Append('\t').Append(kv.Value.Ticks).Append('\t').Append(kv.Value.Up ? "1" : "0").Append('\n');
            string tmp = StateFile + ".tmp"; File.WriteAllText(tmp, sb.ToString(), Encoding.UTF8); if (File.Exists(StateFile)) File.Delete(StateFile); File.Move(tmp, StateFile);
        }
        catch { }
    }
}
