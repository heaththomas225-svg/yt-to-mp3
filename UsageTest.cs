using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

// TIMES USED: numbers of any size are shown in full and short, conversions are counted for each file type (MP3, MP4, OPUS, WAV, M4A, FLAC, MKV) and sent, offline ones wait and go later, the setting turns it off, and the window.
// csc /main:UT ... UsageTest.cs
static class UT
{
    static int fails = 0; static void Check(string n, bool ok, string d = "") { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + (d.Length > 0 ? "   " + d : "")); Console.Out.Flush(); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); Thread.Sleep(15); } }
    static bool Until(Func<bool> c, int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { if (c()) return true; Application.DoEvents(); Thread.Sleep(20); } return c(); }
    [STAThread]
    static void Main()
    {
        string work = Path.Combine(Path.GetTempPath(), "usage_test_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work); StudioEnv.DataDir = Path.Combine(work, "data"); Directory.CreateDirectory(StudioEnv.DataDir); Application.EnableVisualStyles();
        // ---- the numbers: any size ----
        Check("a number is grouped in threes", UsageStat.Group("1234567") == "1,234,567" && UsageStat.Group("999") == "999" && UsageStat.Group("1000") == "1,000" && UsageStat.Group("0") == "0" && UsageStat.Group("") == "0");
        Check("leading zeros are dropped and rubbish is zero", UsageStat.Group("000123") == "123" && UsageStat.Group("12a") == "0" && UsageStat.Group(null) == "0");
        Check("a number with thirty digits is grouped exactly", UsageStat.Group("123456789012345678901234567890") == "123,456,789,012,345,678,901,234,567,890");
        Check("short form: thousands, millions, billions, trillions", UsageStat.Short("999") == "999" && UsageStat.Short("1000") == "1K" && UsageStat.Short("1234") == "1.2K" && UsageStat.Short("12345") == "12.3K" && UsageStat.Short("123456") == "123.4K" && UsageStat.Short("1234567") == "1.2M" && UsageStat.Short("12345678") == "12.3M" && UsageStat.Short("999999999") == "999.9M" && UsageStat.Short("1000000000") == "1B" && UsageStat.Short("2500000000000") == "2.5T", UsageStat.Short("1234567") + " " + UsageStat.Short("2500000000000"));
        Check("it keeps going past a trillion with no limit", UsageStat.Short("1200000000000000") == "1.2Qa" && UsageStat.Short("9" + new string('0', 33)) == "9Dc" && UsageStat.Short("1" + new string('0', 40)) == "1e40", UsageStat.Short("1" + new string('0', 40)));
        // ---- the file types ----
        Check("the seven types are MP3, MP4, OPUS, WAV, M4A, FLAC and MKV", string.Join(",", UsageStat.Kinds) == "mp3,mp4,opus,wav,m4a,flac,mkv");
        Check("a file's type comes from its extension", UsageStat.KindOfExt(".MP3") == "mp3" && UsageStat.KindOfExt("flac") == "flac" && UsageStat.KindOfExt(".mkv") == "mkv" && UsageStat.KindOfExt(".mp4") == "mp4" && UsageStat.KindOfExt(".opus") == "opus" && UsageStat.KindOfExt(".ogg") == "opus" && UsageStat.KindOfExt(".wav") == "wav" && UsageStat.KindOfExt(".m4a") == "m4a" && UsageStat.KindOfExt(".txt") == null && UsageStat.KindOfExt(null) == null);
        Check("the converter's format list maps to the type (every MP4 quality is MP4)", UsageStat.KindOfFmt(0) == "mp3" && UsageStat.KindOfFmt(1) == "wav" && UsageStat.KindOfFmt(2) == "flac" && UsageStat.KindOfFmt(3) == "m4a" && UsageStat.KindOfFmt(4) == "opus" && UsageStat.KindOfFmt(5) == "mp4" && UsageStat.KindOfFmt(7) == "mp4" && UsageStat.KindOfFmt(10) == "mp4" && UsageStat.KindOfFmt(11) == "mkv");
        // ---- a pretend website ----
        var site = new Dictionary<string, long>(); foreach (var k in UsageStat.Kinds) site[k] = 0; site["mp3"] = 1234567; site["mp4"] = 345678; var posts = new List<string>(); bool down = false; int port = 0; var srv = new HttpListener();
        for (int p = 41400; p < 41500; p++) { try { srv.Prefixes.Clear(); srv.Prefixes.Add("http://127.0.0.1:" + p + "/"); srv.Start(); port = p; break; } catch { srv = new HttpListener(); } }
        var th = new Thread(() => { while (srv.IsListening) { try { var c = srv.GetContext(); string body = "{}"; int code = 200;
                if (down) { code = 503; }
                else if (c.Request.HttpMethod == "POST") { string txt = new StreamReader(c.Request.InputStream).ReadToEnd(); lock (posts) { posts.Add(txt); string kind = Regex.Match(txt, "\"kind\":\"(\\w+)\"").Groups[1].Value; int n = int.Parse(Regex.Match(txt, "\"n\":(\\d+)").Groups[1].Value); site[kind] += n; } body = "{\"ok\":true}"; }
                else { lock (posts) { long tot = 0; var sb = new StringBuilder("{\"ok\":true"); foreach (var kv in site) { sb.Append(",\"" + kv.Key + "\":\"" + kv.Value + "\""); tot += kv.Value; } sb.Append(",\"total\":\"" + tot + "\"}"); body = sb.ToString(); } }
                var b = Encoding.UTF8.GetBytes(body); c.Response.StatusCode = code; c.Response.ContentType = "application/json"; c.Response.OutputStream.Write(b, 0, b.Length); c.Response.Close(); } catch { break; } } }) { IsBackground = true }; th.Start();
        UsageStat.BaseUrl = "http://127.0.0.1:" + port;
        Func<string, int> sent = kind => { int n = 0; lock (posts) foreach (var p in posts) if (p.Contains("\"" + kind + "\"")) n += int.Parse(Regex.Match(p, "\"n\":(\\d+)").Groups[1].Value); return n; };
        try
        {
            var t = UsageStat.Fetch(); Check("the worldwide numbers are read, for every type", t.Ok && t.Get("mp3") == "1234567" && t.Get("mp4") == "345678" && t.Get("flac") == "0" && t.Total == "1580245", t.Error);
            down = true; var t2 = UsageStat.Fetch(); Check("when the website is down it says so plainly", !t2.Ok && t2.Error.Contains("Could not reach")); down = false;
            // ---- counting ----
            UsageStat.Reset(); Prefs.CountUses = true; UsageStat.Count("mp3", 1); Until(() => UsageStat.Pending == 0, 5000); Check("a finished MP3 is sent as one MP3", sent("mp3") == 1 && sent("flac") == 0); Check("and counted on this PC", UsageStat.Local("mp3") == 1 && UsageStat.LocalTotal == 1);
            UsageStat.Count("flac", 2); UsageStat.Count("opus", 1); UsageStat.Count("mkv", 3); Until(() => UsageStat.Pending == 0, 5000); Check("each type is sent as its own type", sent("flac") == 2 && sent("opus") == 1 && sent("mkv") == 3 && sent("mp4") == 0, "flac " + sent("flac") + " opus " + sent("opus") + " mkv " + sent("mkv"));
            UsageStat.Count("wav", 1); UsageStat.Count("m4a", 1); UsageStat.Count("mp4", 1); Until(() => UsageStat.Pending == 0, 5000); Check("WAV, M4A and MP4 too", sent("wav") == 1 && sent("m4a") == 1 && sent("mp4") == 1);
            UsageStat.Count("mp3", 120); Until(() => UsageStat.Pending == 0, 8000); Check("a big playlist is sent in pieces of at most 50", sent("mp3") == 121 && UsageStat.Pending == 0, sent("mp3") + " sent"); lock (posts) foreach (var p in posts) if (int.Parse(Regex.Match(p, "\"n\":(\\d+)").Groups[1].Value) > 50) Check("no piece is bigger than 50", false, p);
            Check("the totals went up by what was counted", UsageStat.Fetch().Get("mp3") == (1234567 + 121).ToString() && UsageStat.Fetch().Get("flac") == "2");
            Check("a type that does not exist is ignored", new Func<bool>(() => { long b = UsageStat.LocalTotal; UsageStat.Count("wma", 5); UsageStat.Count("mp3", 0); UsageStat.Count("mp3", -3); return UsageStat.LocalTotal == b; })());
            Prefs.CountUses = false; int before; lock (posts) before = posts.Count; long lt = UsageStat.LocalTotal; UsageStat.Count("mp3", 5); UsageStat.Count("opus", 5); Pump(400); lock (posts) Check("with the setting off nothing is sent, but it is still counted on this PC", posts.Count == before && UsageStat.LocalTotal == lt + 10 && UsageStat.Pending == 0);
            Prefs.CountUses = true; down = true; UsageStat.Count("wav", 3); Pump(600); Check("offline conversions wait to be sent", UsageStat.Pending == 3, UsageStat.Pending + " waiting");
            int wavBefore = sent("wav"); down = false; UsageStat.Flush(); Check("and go when the website is back", Until(() => UsageStat.Pending == 0, 6000) && sent("wav") == wavBefore + 3); string saved = File.ReadAllText(Path.Combine(StudioEnv.DataDir, "usage-local.txt")); Check("the counts are saved in a file, one line for each type", saved.Contains("l_mp3=") && saved.Contains("l_mkv=") && saved.Contains("p_flac=0"), saved.Replace("\n", " "));
            // the first version of the file only knew audio and video
            File.WriteAllText(Path.Combine(StudioEnv.DataDir, "usage-local.txt"), "audio=5\nvideo=2\npendaudio=1\npendvideo=0\n"); UsageStat.Reset(); Check("a file from the first version is read (audio is MP3, video is MP4)", UsageStat.Local("mp3") == 5 && UsageStat.Local("mp4") == 2 && UsageStat.Pending == 1, UsageStat.Local("mp3") + " " + UsageStat.Local("mp4") + " " + UsageStat.Pending); UsageStat.FlushNow();
            // counting the files after a conversion
            UsageStat.Reset(); File.Delete(Path.Combine(StudioEnv.DataDir, "usage-local.txt"));
            string d = Path.Combine(work, "dl"); Directory.CreateDirectory(d); File.WriteAllText(Path.Combine(d, "old.mp3"), "x"); var snap = LyricsGet.Snapshot(d);
            foreach (var f in new[] { "a.mp3", "b.flac", "c.mp4", "d.opus", "e.wav", "f.mkv", "g.m4a", "notes.txt" }) File.WriteAllText(Path.Combine(d, f), "x");
            UsageStat.CountNew(d, snap, 0, 1); Check("every new file is counted as its own type (and notes are ignored)", UsageStat.Local("mp3") == 1 && UsageStat.Local("flac") == 1 && UsageStat.Local("mp4") == 1 && UsageStat.Local("opus") == 1 && UsageStat.Local("wav") == 1 && UsageStat.Local("mkv") == 1 && UsageStat.Local("m4a") == 1 && UsageStat.LocalTotal == 7, UsageStat.LocalTotal + " counted");
            string empty = Path.Combine(work, "none"); Directory.CreateDirectory(empty); UsageStat.CountNew(empty, LyricsGet.Snapshot(empty), 7, 1); UsageStat.CountNew(empty, LyricsGet.Snapshot(empty), 11, 1); UsageStat.CountNew(empty, LyricsGet.Snapshot(empty), 4, 1);
            Check("a conversion with no file found counts as the type that was asked for", UsageStat.Local("mp4") == 2 && UsageStat.Local("mkv") == 2 && UsageStat.Local("opus") == 2);
            Check("counting is on by default and is saved with the settings", new Func<bool>(() => { bool keep = Prefs.CountUses; Prefs.CountUses = true; bool r = string.Join("|", Prefs.Lines()).Contains("countuses=True"); Prefs.CountUses = keep; return r; })());
            // ---- the window ----
            UsageForm.FetchHook = () => { var tt = new UsageStat.Totals { Ok = true, Total = "123456789012345678901236002457" }; foreach (var k in UsageStat.Kinds) tt.Per[k] = "1234567"; tt.Per["mp3"] = "123456789012345678901234567890"; return tt; };
            var f1 = new UsageForm(); f1.Show(); Until(() => f1.TotalText != "0", 4000); Pump(300);
            Check("the window shows the total in full, however long", f1.TotalText == "123,456,789,012,345,678,901,236,002,457", f1.TotalText);
            bool allKinds = true; foreach (var k in UsageStat.Kinds) if (f1.KindText(k) != (k == "mp3" ? "123,456,789,012,345,678,901,234,567,890" : "1,234,567")) allKinds = false; Check("and a card for each of the seven types", allKinds);
            Check("and what was made on this PC, and says when it was updated", f1.MineText == "10" && f1.StatusText.StartsWith("Updated"), f1.MineText + " | " + f1.StatusText);
            var bmp = new System.Drawing.Bitmap(f1.ClientSize.Width, f1.ClientSize.Height); f1.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height)); bmp.Save(Path.Combine(Path.GetTempPath(), "usage_window.png")); bmp.Dispose();
            UsageForm.FetchHook = () => new UsageStat.Totals { Ok = false, Error = "Could not reach purphexmusic.com (test)." }; f1.Load(); Until(() => f1.StatusText.Contains("Could not reach"), 4000); Check("when the website cannot be reached the window says so", f1.StatusText.Contains("Could not reach"), f1.StatusText);
            f1.Close(); UsageForm.FetchHook = null;
        }
        finally { try { srv.Stop(); } catch { } try { Directory.Delete(work, true); } catch { } }
        Console.WriteLine(fails == 0 ? "=== times used checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
