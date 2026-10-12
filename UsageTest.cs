using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// TIMES USED: numbers of any size are shown in full and short, conversions are counted (audio and video apart) and sent, offline ones wait and go later, the setting turns it off, and the window.
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
        // ---- a pretend website ----
        long mp3 = 1234567, mp4 = 345678; var posts = new List<string>(); bool down = false; int port = 0; var srv = new HttpListener();
        for (int p = 41400; p < 41500; p++) { try { srv.Prefixes.Clear(); srv.Prefixes.Add("http://127.0.0.1:" + p + "/"); srv.Start(); port = p; break; } catch { srv = new HttpListener(); } }
        var th = new Thread(() => { while (srv.IsListening) { try { var c = srv.GetContext(); string body = "{}"; int code = 200;
                if (down) { code = 503; }
                else if (c.Request.HttpMethod == "POST") { string txt = new StreamReader(c.Request.InputStream).ReadToEnd(); lock (posts) posts.Add(txt); int n = int.Parse(System.Text.RegularExpressions.Regex.Match(txt, "\"n\":(\\d+)").Groups[1].Value); if (txt.Contains("mp3")) mp3 += n; else mp4 += n; body = "{\"ok\":true}"; }
                else body = "{\"ok\":true,\"mp3\":\"" + mp3 + "\",\"mp4\":\"" + mp4 + "\",\"total\":\"" + (mp3 + mp4) + "\"}";
                var b = Encoding.UTF8.GetBytes(body); c.Response.StatusCode = code; c.Response.ContentType = "application/json"; c.Response.OutputStream.Write(b, 0, b.Length); c.Response.Close(); } catch { break; } } }) { IsBackground = true }; th.Start();
        UsageStat.BaseUrl = "http://127.0.0.1:" + port;
        try
        {
            var t = UsageStat.Fetch(); Check("the worldwide numbers are read", t.Ok && t.Mp3 == "1234567" && t.Mp4 == "345678" && t.Total == "1580245", t.Error);
            down = true; var t2 = UsageStat.Fetch(); Check("when the website is down it says so plainly", !t2.Ok && t2.Error.Contains("Could not reach")); down = false;
            // ---- counting ----
            UsageStat.Reset(); Prefs.CountUses = true; UsageStat.Count(1, 0); Until(() => UsageStat.Pending == 0, 5000); lock (posts) Check("a finished MP3 is sent as one audio", posts.Count == 1 && posts[0].Contains("\"mp3\"") && posts[0].Contains("\"n\":1"), string.Join(" ", posts.ToArray())); Check("and counted on this PC", UsageStat.LocalAudio == 1 && UsageStat.LocalVideo == 0);
            UsageStat.Count(0, 2); Until(() => UsageStat.Pending == 0, 5000); lock (posts) Check("two videos are sent as MP4", posts.Count == 2 && posts[1].Contains("\"mp4\"") && posts[1].Contains("\"n\":2"));
            UsageStat.Count(120, 0); Until(() => UsageStat.Pending == 0, 8000); int sum = 0; lock (posts) foreach (var p in posts) if (p.Contains("mp3")) sum += int.Parse(System.Text.RegularExpressions.Regex.Match(p, "\"n\":(\\d+)").Groups[1].Value); Check("a big playlist is sent in pieces of at most 50", sum == 121 && UsageStat.Pending == 0, sum + " sent");
            Check("the totals went up by what was counted", UsageStat.Fetch().Mp3 == (1234567 + 121).ToString());
            Prefs.CountUses = false; int before; lock (posts) before = posts.Count; UsageStat.Count(5, 5); Pump(400); lock (posts) Check("with the setting off nothing is sent, but it is still counted on this PC", posts.Count == before && UsageStat.LocalAudio == 126 && UsageStat.LocalVideo == 7 && UsageStat.Pending == 0);
            Prefs.CountUses = true; down = true; UsageStat.Count(3, 0); Pump(600); Check("offline conversions wait to be sent", UsageStat.Pending == 3, UsageStat.Pending + " waiting");
            down = false; UsageStat.Flush(); Check("and go when the website is back", Until(() => UsageStat.Pending == 0, 6000)); string saved = File.ReadAllText(Path.Combine(StudioEnv.DataDir, "usage-local.txt")); Check("the counts are saved in a file", saved.Contains("audio=129") && saved.Contains("pendaudio=0"), saved.Replace("\n", " "));
            // counting files after a conversion
            string d = Path.Combine(work, "dl"); Directory.CreateDirectory(d); File.WriteAllText(Path.Combine(d, "old.mp3"), "x"); var snap = LyricsGet.Snapshot(d); File.WriteAllText(Path.Combine(d, "a.mp3"), "x"); File.WriteAllText(Path.Combine(d, "b.flac"), "x"); File.WriteAllText(Path.Combine(d, "c.mp4"), "x"); File.WriteAllText(Path.Combine(d, "notes.txt"), "x");
            long la = UsageStat.LocalAudio, lv = UsageStat.LocalVideo; UsageStat.CountNew(d, snap, 0, 1); Check("only the new songs and videos are counted (audio and video apart, notes ignored)", UsageStat.LocalAudio == la + 2 && UsageStat.LocalVideo == lv + 1, (UsageStat.LocalAudio - la) + " audio " + (UsageStat.LocalVideo - lv) + " video");
            string empty = Path.Combine(work, "none"); Directory.CreateDirectory(empty); la = UsageStat.LocalAudio; lv = UsageStat.LocalVideo; UsageStat.CountNew(empty, LyricsGet.Snapshot(empty), 7, 1); Check("a conversion with no file found still counts as one of its kind", UsageStat.LocalVideo == lv + 1 && UsageStat.LocalAudio == la);
            Check("counting is on by default and is saved with the settings", new Func<bool>(() => { bool keep = Prefs.CountUses; Prefs.CountUses = true; bool r = string.Join("|", Prefs.Lines()).Contains("countuses=True"); Prefs.CountUses = keep; return r; })());
            // ---- the window ----
            UsageForm.FetchHook = () => new UsageStat.Totals { Ok = true, Mp3 = "123456789012345678901234567890", Mp4 = "1234567", Total = "123456789012345678901236002457" };
            var f = new UsageForm(); f.Show(); Until(() => f.TotalText != "0", 4000); Pump(300);
            Check("the window shows the full numbers, however long", f.TotalText == "123,456,789,012,345,678,901,236,002,457" && f.AudioText == "123,456,789,012,345,678,901,234,567,890" && f.VideoText == "1,234,567", f.TotalText + " | " + f.AudioText);
            Check("and says when it was updated", f.StatusText.StartsWith("Updated"), f.StatusText);
            var bmp = new System.Drawing.Bitmap(f.ClientSize.Width, f.ClientSize.Height); f.DrawToBitmap(bmp, new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height)); bmp.Save(Path.Combine(Path.GetTempPath(), "usage_window.png")); bmp.Dispose();
            UsageForm.FetchHook = () => new UsageStat.Totals { Ok = false, Error = "Could not reach purphexmusic.com (test)." }; f.Load(); Until(() => f.StatusText.Contains("Could not reach"), 4000); Check("when the website cannot be reached the window says so", f.StatusText.Contains("Could not reach"), f.StatusText);
            f.Close(); UsageForm.FetchHook = null;
        }
        finally { try { srv.Stop(); } catch { } try { Directory.Delete(work, true); } catch { } }
        Console.WriteLine(fails == 0 ? "=== times used checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
