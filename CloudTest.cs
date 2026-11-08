using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;

// Cloud backup: PCs on one account against a stand-in for the website that follows the same rules (each PC writes only its own part; the real
// website has its own checks in purphex-web/test-backup.js). Songs, their audio and imported sounds go up from one PC and come down on the other,
// nothing is sent twice, a wiped vault or a fresh install gets everything back, one PC never overwrites the other's sounds, and a path from the
// server can never write outside Purphex's folders.
static class CB
{
    static int fails;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + "   " + d); Console.Out.Flush(); }

    // ---- the stand-in server ----
    sealed class Ent { public string Sha; public long Size, Mtime; }
    static readonly Dictionary<string, Ent> files = new Dictionary<string, Ent>(); static readonly Dictionary<string, byte[]> blobs = new Dictionary<string, byte[]>();
    static readonly object slk = new object(); static string good = new string('a', 64); public static int FullPuts, Removes, NotYours; static string extraBad; static long fakeRoom = -1;
    static string Sha1(byte[] b) { using (var s = SHA1.Create()) return BitConverter.ToString(s.ComputeHash(b)).Replace("-", "").ToLowerInvariant(); }
    static string PcOf(string fp) { return "pc-" + Sha1(Encoding.UTF8.GetBytes("pc|" + fp)).Substring(0, 12); }
    static void Serve(HttpListener l)
    {
        while (l.IsListening)
        {
            HttpListenerContext c; try { c = l.GetContext(); } catch { return; }
            try
            {
                var rq = c.Request; var rs = c.Response; string path = rq.Url.AbsolutePath; int st = 200; byte[] outb = null; string pc = PcOf(rq.Headers["X-Device"] ?? "");
                if (rq.Headers["Authorization"] != "Bearer " + good) { st = 401; outb = Encoding.UTF8.GetBytes("{\"ok\":false,\"err\":\"link\"}"); }
                else lock (slk)
                {
                    if (path == "/api/backup/list") { var sb = new StringBuilder("{\"ok\":true,\"pc\":\"" + pc + "\",\"used\":123,\"quota\":10737418240,\"maxFile\":1073741824," + (fakeRoom >= 0 ? "\"room\":" + fakeRoom + "," : "") + "\"files\":["); bool first = true; foreach (var kv in files) { if (!first) sb.Append(','); first = false; sb.Append("{\"p\":\"").Append(kv.Key).Append("\",\"sha\":\"").Append(kv.Value.Sha).Append("\",\"size\":").Append(kv.Value.Size).Append(",\"mtime\":").Append(kv.Value.Mtime).Append('}'); } if (extraBad != null) sb.Append(first ? "" : ",").Append("{\"p\":\"" + extraBad + "\",\"sha\":\"" + new string('0', 40) + "\",\"size\":1,\"mtime\":1}"); sb.Append("]}"); outb = Encoding.UTF8.GetBytes(sb.ToString()); }
                    else if (path == "/api/backup/file" && rq.HttpMethod == "PUT")
                    {
                        string p = rq.QueryString["p"], sha = rq.QueryString["sha"]; long size = long.Parse(rq.QueryString["size"]), mt = long.Parse(rq.QueryString["mtime"]);
                        var ms = new MemoryStream(); rq.InputStream.CopyTo(ms); var body = ms.ToArray();
                        if (!p.StartsWith(pc + "/")) { NotYours++; st = 403; outb = Encoding.UTF8.GetBytes("{\"ok\":false,\"err\":\"notyours\"}"); }
                        else if (blobs.ContainsKey(sha) && body.Length == 0) { files[p] = new Ent { Sha = sha, Size = size, Mtime = mt }; outb = Encoding.UTF8.GetBytes("{\"ok\":true}"); }
                        else if (body.Length == 0 && size > 0) { st = 409; outb = Encoding.UTF8.GetBytes("{\"ok\":false,\"err\":\"need\"}"); }
                        else if (Sha1(body) != sha) { st = 400; outb = Encoding.UTF8.GetBytes("{\"ok\":false,\"err\":\"sha\"}"); }
                        else { FullPuts++; blobs[sha] = body; files[p] = new Ent { Sha = sha, Size = size, Mtime = mt }; outb = Encoding.UTF8.GetBytes("{\"ok\":true}"); }
                    }
                    else if (path == "/api/backup/file") { string p = rq.QueryString["p"]; Ent e; if (p != null && files.TryGetValue(p, out e)) outb = blobs[e.Sha]; else if (p == extraBad) outb = new byte[] { 1 }; else st = 404; }
                    else if (path == "/api/backup/remove") { var body = new StreamReader(rq.InputStream).ReadToEnd(); var o = MiniJson.Parse(body) as Dictionary<string, object>; foreach (var x in (List<object>)o["p"]) { string p = Convert.ToString(x); if (p.StartsWith(pc + "/") && files.Remove(p)) Removes++; } outb = Encoding.UTF8.GetBytes("{\"ok\":true}"); }
                    else st = 404;
                }
                rs.StatusCode = st; if (outb == null) outb = new byte[0]; rs.ContentLength64 = outb.Length; rs.OutputStream.Write(outb, 0, outb.Length); rs.Close();
            }
            catch { try { c.Response.Abort(); } catch { } }
        }
    }
    static int Count(string prefix) { lock (slk) { int n = 0; foreach (var k in files.Keys) if (k.StartsWith(prefix)) n++; return n; } }

    static string root;
    static void Pc(string d, string fp) { StudioEnv.DataDir = d; Directory.CreateDirectory(d); CloudBackup.FpOverride = fp; SoundLibrary.Load(); ProjectLibrary.Load(); }
    static string Wav(string file, int seed)
    {
        int n = 4410; var ms = new MemoryStream(); var w = new BinaryWriter(ms);
        w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + n * 2); w.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(44100); w.Write(88200); w.Write((short)2); w.Write((short)16); w.Write(Encoding.ASCII.GetBytes("data")); w.Write(n * 2);
        for (int i = 0; i < n; i++) w.Write((short)(Math.Sin(i * 0.05 * (seed + 1)) * 8000)); w.Flush(); File.WriteAllBytes(file, ms.ToArray()); return file;
    }
    static SoundPack PackNamed(string start) { lock (SoundLibrary.Lock) return SoundLibrary.Packs.Find(p => p.Name.StartsWith(start)); }
    static void Main()
    {
        root = Path.Combine(Path.GetTempPath(), "purphex_cloudtest_" + Guid.NewGuid().ToString("N")); string PcA = Path.Combine(root, "pcA"), PcB = Path.Combine(root, "pcB"); Directory.CreateDirectory(root);
        int port = 18000 + new Random().Next(1500); var l = new HttpListener(); l.Prefixes.Add("http://localhost:" + port + "/"); l.Start(); var th = new System.Threading.Thread(() => Serve(l)); th.IsBackground = true; th.Start();
        CloudBackup.BaseOverride = "http://localhost:" + port; CloudBackup.TokenOverride = good; string A = PcOf("fpA") + "/", B = PcOf("fpB") + "/";
        try
        {
            Check("backup paths: only pc-<id>/vault/ and pc-<id>/samples/, nothing that climbs out or that Windows cannot hold", CloudBackup.CleanPath(A + "vault/projects/a.ytmproj") != null && CloudBackup.CleanPath(A + "samples/imports/k (2).wav") != null && CloudBackup.CleanPath(A + "vault/../x") == null && CloudBackup.CleanPath("vault/x") == null && CloudBackup.CleanPath(A + "vault/a:b") == null && CloudBackup.CleanPath(A + "vault/x.") == null, "");
            // ---- PC A: a saved song with a recording, and two imported sounds ----
            Pc(PcA, "fpA"); string src = Path.Combine(root, "music"); Directory.CreateDirectory(src);
            string rec = Wav(Path.Combine(src, "vocal take.wav"), 1); string proj = Path.Combine(src, "Bassline.ytmproj");
            File.WriteAllText(proj, "YTMPROJ|1\ntrack|Vocal|0|0|" + rec.Replace("%", "%25").Replace("|", "%7C") + "\n", Encoding.UTF8);
            Check("the song is kept in the vault first", ProjectVault.Keep(proj, "Bassline"), "");
            int imp = SoundLibrary.ImportFiles(new[] { Wav(Path.Combine(src, "kick.wav"), 2), Wav(Path.Combine(src, "snare A.wav"), 3) }, null); Check("two sounds imported on PC A", imp == 2, imp + "");
            bool ok = CloudBackup.Sync(); int onServer = Count(A);
            Check("PC A backs up its song, the song's audio and its imported sounds, into its own part", ok && onServer >= 6 && files.ContainsKey(A + "samples/imports/kick.wav") && files.ContainsKey(A + "samples/pack.txt") && CloudBackup.MyPc + "/" == A, ok + " " + CloudBackup.Sent + " sent, " + onServer + " on the server: " + CloudBackup.Status);
            Check("the Projects line says it is backed up", CloudBackup.Status.StartsWith("Backed up to your account"), CloudBackup.Status);
            int fp0 = FullPuts; ok = CloudBackup.Sync(); Check("a second pass sends nothing (nothing changed)", ok && CloudBackup.Sent == 0 && FullPuts == fp0, CloudBackup.Sent + " sent");
            // ---- PC B: a new PC on the same account, and the originals are gone ----
            File.Delete(rec); File.Delete(proj);
            Pc(PcB, "fpB"); SoundLibrary.ImportFiles(new[] { Wav(Path.Combine(root, "kick.wav"), 9) }, null);   // B has a different sound with the same name
            Wav(rec, 7); File.WriteAllText(proj, "YTMPROJ|1\nbpm|99\ntrack|Mine|0|0|" + rec.Replace("%", "%25").Replace("|", "%7C") + "\n", Encoding.UTF8); ProjectVault.Keep(proj, "Bassline"); string bOwn = File.ReadAllText(ProjectVault.CopyFor(proj));   // and B has its own, different song at the very same path (same Windows user name)
            ok = CloudBackup.Sync();
            var adopted = ProjectLibrary.Entries.Find(e => e.Name.StartsWith("Bassline (from "));
            Check("PC B gets PC A's song as a song of its own, \"Bassline (from <PC>)\"", ok && adopted != null && File.Exists(adopted.Path), ProjectLibrary.Entries.Count + " projects, " + CloudBackup.Got + " brought down, " + CloudBackup.Status);
            string aSrc = null; if (adopted != null) foreach (var line in File.ReadAllLines(adopted.Path)) if (line.StartsWith("track|")) aSrc = line.Split('|')[4].Replace("%7C", "|").Replace("%25", "%");
            Check("its recording is PC A's take, byte for byte (not PC B's file at the same path)", aSrc != null && File.Exists(aSrc) && Sha1(File.ReadAllBytes(aSrc)) == files[A + "vault/audio/" + Path.GetFileName(aSrc)].Sha && aSrc != rec, aSrc ?? "none");
            Check("and PC B's own song at that path is untouched", File.ReadAllText(ProjectVault.CopyFor(proj)) == bOwn && File.ReadAllText(proj).Contains("Mine"), "");
            File.Delete(rec); File.Delete(proj);
            var other = PackNamed("My samples (from "); Check("PC A's sounds are a pack of their own on PC B, named after PC A", other != null && other.Items.Exists(s => s.Name == "snare A") && other.Items.Exists(s => s.Name == "kick") && File.Exists(Path.Combine(other.Dir, "imports", "snare A.wav")), other == null ? "no pack" : other.Name + ": " + other.Items.Count);
            var mineB = SoundLibrary.Pack(SoundLibrary.UserPackId); string bKick = Path.Combine(mineB.Dir, "imports", "kick.wav");
            Check("PC B's own kick with the same name is untouched", File.Exists(bKick) && Sha1(File.ReadAllBytes(bKick)) == Sha1(File.ReadAllBytes(Path.Combine(root, "kick.wav"))) && mineB.Items.Count == 1, mineB.Items.Count + "");
            Check("PC B never writes into PC A's part", NotYours == 0 && files[A + "samples/imports/kick.wav"].Sha != files[B + "samples/imports/kick.wav"].Sha, NotYours + "");
            Pc(PcA, "fpA"); CloudBackup.Sync(); var mineA = SoundLibrary.Pack(SoundLibrary.UserPackId); var fromB = PackNamed("My samples (from ");
            Check("and PC A gets PC B's kick as PC B's, keeping its own", mineA.Items.Count == 2 && fromB != null && fromB.Items.Exists(s => s.Name == "kick") && Sha1(File.ReadAllBytes(Path.Combine(mineA.Dir, "imports", "kick.wav"))) == files[A + "samples/imports/kick.wav"].Sha, fromB == null ? "none" : fromB.Items.Count + "");
            // ---- a change on A reaches B ----
            string copyA = ProjectVault.CopyFor(proj); System.Threading.Thread.Sleep(1100); File.AppendAllText(copyA, "note|changed on A\n"); CloudBackup.Sync();
            var fromOnA = ProjectLibrary.Entries.FindAll(e => e.Name.Contains("(from ")); var fromNames = new List<string>(); foreach (var e in fromOnA) fromNames.Add(e.Name + (File.ReadAllText(e.Path).Contains("Mine") ? " [B's own]" : " [A's song back]"));
            Check("PC A gets PC B's own song, but never PC B's copy of A's song back (no songs bouncing between PCs)", fromOnA.Count == 1 && File.ReadAllText(fromOnA[0].Path).Contains("Mine"), string.Join("; ", fromNames.ToArray()));
            Pc(PcB, "fpB"); CloudBackup.Sync(); CloudBackup.Sync(); string theirCopy = Path.Combine(CloudBackup.OthersDir, A.TrimEnd('/'), "projects", Path.GetFileName(ProjectVault.CopyFor(proj)));
            int fromCount = ProjectLibrary.Entries.FindAll(e => e.Name.StartsWith("Bassline (from ")).Count;
            Check("a song changed on one PC is updated in the other PC's copy, and it is not brought over a second time", File.ReadAllText(theirCopy).Contains("changed on A") && fromCount == 1, fromCount + " copies");
            // ---- PC A's vault is wiped (a cleaner, a bad restore): everything comes back and nothing is removed from the cloud ----
            Pc(PcA, "fpA"); int before = Count(A), rm0 = Removes; Directory.Delete(ProjectVault.Dir, true); Directory.Delete(Path.Combine(SoundLibrary.Root, SoundLibrary.UserPackId), true);
            CloudBackup.Sync(); SoundLibrary.Load(); mineA = SoundLibrary.Pack(SoundLibrary.UserPackId);
            Check("a wiped vault and My samples are fetched back and nothing is removed from the cloud", Count(A) == before && Removes == rm0 && File.Exists(copyA) && File.ReadAllText(copyA).Contains("changed on A") && mineA != null && mineA.Items.Count == 2, Count(A) + "/" + before + ", " + (Removes - rm0) + " removed, " + CloudBackup.Status);
            // ---- a fresh install on PC A (no data at all) ----
            string PcA2 = Path.Combine(root, "pcA2"); Pc(PcA2, "fpA"); string oldKick = files[A + "samples/imports/kick.wav"].Sha;
            SoundLibrary.ImportFiles(new[] { Wav(Path.Combine(Directory.CreateDirectory(Path.Combine(root, "new kick")).FullName, "kick.wav"), 11) }, null);   // before it syncs, a new sound with an old name
            CloudBackup.Sync(); SoundLibrary.Load(); var a2 = SoundLibrary.Pack(SoundLibrary.UserPackId); var a2n = new List<string>(); foreach (var x in a2.Items) a2n.Add(x.Name); a2n.Sort();
            Check("a fresh install on the same PC gets its own songs back as its own", ProjectVault.All().Exists(k => k.Name == "Bassline" && k.FromPc == null) && File.Exists(ProjectVault.CopyFor(proj)), CloudBackup.Status);
            string bk = Path.Combine(a2.Dir, "imports", "kick (backup).wav");
            Check("and keeps every sound: its old kick comes back as \"kick (backup)\" next to the new one", string.Join(",", a2n.ToArray()) == "kick,kick (backup),snare A" && File.Exists(bk) && Sha1(File.ReadAllBytes(bk)) == oldKick, string.Join(",", a2n.ToArray()));
            // ---- taken off the list and put back on one PC ----
            ProjectVault.MarkRemoved(proj); CloudBackup.Sync(); Check("a song taken off the list stays off after a sync", ProjectVault.IsRemoved(proj), "");
            string mk = A + "vault/projects/" + Path.GetFileName(Path.ChangeExtension(ProjectVault.CopyFor(proj), ".removed"));
            ProjectVault.Unmark(proj); CloudBackup.Sync(); CloudBackup.Sync(); Check("and putting it back stays back (the cloud marker goes)", !ProjectVault.IsRemoved(proj) && !files.ContainsKey(mk), "");
            ProjectVault.MarkRemoved(proj); CloudBackup.Sync(); ProjectVault.Unmark(proj); File.Delete(Path.Combine(StudioEnv.DataDir, "cloud", "synced.txt"));   // put back, but the sync was cut off and forgot it
            if (ProjectLibrary.Find(proj) == null) ProjectLibrary.AddIfNew(new ProjectEntry { Path = proj, Name = "Bassline" }); CloudBackup.Sync();
            Check("an old marker in the cloud never hides a song that is on the list", !ProjectVault.IsRemoved(proj) && !files.ContainsKey(mk), "");
            // ---- the server is full: nothing is sent for nothing, and it goes up once there is room ----
            fakeRoom = 10; SoundLibrary.ImportFiles(new[] { Wav(Path.Combine(root, "perc full.wav"), 6) }, null); int fp1 = FullPuts; CloudBackup.Sync();
            Check("when the server has no room nothing big is uploaded and the Projects line says files are waiting", CloudBackup.Waiting >= 1 && FullPuts == fp1 && CloudBackup.Status.Contains("waiting for space"), CloudBackup.Waiting + " waiting, " + (FullPuts - fp1) + " uploads: " + CloudBackup.Status);
            fakeRoom = -1; CloudBackup.Sync(); Check("and they go up as soon as there is room", CloudBackup.Waiting == 0 && files.ContainsKey(A + "samples/imports/perc full.wav"), CloudBackup.Status);
            // ---- the server can never make it write outside its folders ----
            extraBad = B + "vault/../../evil.txt"; Pc(PcB, "fpB"); CloudBackup.Sync(); extraBad = null;
            Check("a bad path from the server writes nothing outside Purphex's folders", !File.Exists(Path.Combine(PcB, "evil.txt")) && !File.Exists(Path.Combine(root, "evil.txt")), "");
            // ---- not linked, or linked to nothing ----
            CloudBackup.TokenOverride = new string('b', 64); ok = CloudBackup.Sync(); Check("a token the account does not know stops the backup and says so", !ok && CloudBackup.Status.Contains("Link this PC"), CloudBackup.Status);
            CloudBackup.TokenOverride = ""; ok = CloudBackup.Sync(); Check("a PC that is not linked asks to be linked", !ok && !CloudBackup.Linked && CloudBackup.Status.Contains("Link this PC"), CloudBackup.Status);
        }
        catch (Exception ex) { Check("no crash", false, ex.ToString()); }
        finally { try { l.Stop(); } catch { } }
        try { Directory.Delete(root, true); } catch { }
        Console.WriteLine(fails == 0 ? "=== cloud backup checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
