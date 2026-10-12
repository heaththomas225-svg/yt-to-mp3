using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

// The lyrics getter against a pretend lyrics database (a local server): names are cleaned, the right song is chosen, nothing wrong is saved, the parts are labelled and the .txt is written.
// csc /main:LT ... LyricsTest.cs
static class LT
{
    static int fails = 0; static void Check(string n, bool ok, string d = "") { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + (d.Length > 0 ? "   " + d : "")); }
    const string LYR = "I walk alone in the night\nsearching for a light\nnobody by my side\nbut I'm alright\n\nSing it loud, sing it loud\nwe are the sound\nSing it loud, sing it loud\nnever back down\n\nThe city never sleeps tonight\nthe stars are burning bright\nI hold on to the feeling\nand I'm running through the night\n\nSing it loud, sing it loud\nwe are the sound\nSing it loud, sing it loud\nnever back down\n\nOne more time before we go\nsing it loud, sing it loud\nnever back down";
    static string Js(string s) { return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\""; }
    static string Rec(string artist, string track, string lyrics, bool instr) { return "{\"artistName\":" + Js(artist) + ",\"trackName\":" + Js(track) + ",\"instrumental\":" + (instr ? "true" : "false") + ",\"plainLyrics\":" + Js(lyrics) + "}"; }
    [STAThread]
    static void Main()
    {
        var hits = new List<string>(); bool down = false;
        var srv = new HttpListener(); int port = 0; for (int p = 41000; p < 41100; p++) { try { srv.Prefixes.Clear(); srv.Prefixes.Add("http://127.0.0.1:" + p + "/"); srv.Start(); port = p; break; } catch { srv = new HttpListener(); } }
        var th = new Thread(() => { while (srv.IsListening) { try { var c = srv.GetContext(); string url = c.Request.Url.PathAndQuery; hits.Add(url); string body = "[]"; int code = 200;
                if (down) { code = 503; body = "{}"; }
                else if (url.StartsWith("/api/get")) { if (url.Contains("Night%20Owl") && url.Contains("Neon%20Fox")) body = Rec("Neon Fox", "Night Owl", LYR, false); else { code = 404; body = "{\"statusCode\":404}"; } }
                else if (url.StartsWith("/api/search")) { if (url.Contains("Sleepless")) body = "[" + Rec("Other Band", "Totally Different Song", LYR, false) + "," + Rec("Dream Team", "Sleepless City", LYR, false) + "]"; else if (url.Contains("Quiet")) body = "[" + Rec("Calm", "Quiet Room", "", true) + "]"; }
                var b = Encoding.UTF8.GetBytes(body); c.Response.StatusCode = code; c.Response.ContentType = "application/json"; c.Response.OutputStream.Write(b, 0, b.Length); c.Response.Close(); } catch { break; } } }) { IsBackground = true }; th.Start();
        LyricsGet.BaseUrl = "http://127.0.0.1:" + port;
        string work = Path.Combine(Path.GetTempPath(), "lyr_test_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work);
        try
        {
            string a, t;
            LyricsGet.CleanName("Neon Fox - Night Owl (Official Video) [MP3 320k]", out a, out t); Check("a name is cleaned to artist and title (brackets and the format tag go)", a == "Neon Fox" && t == "Night Owl", a + " | " + t);
            LyricsGet.CleanName("Dream Team – Sleepless City | Lyrics", out a, out t); Check("an en dash and a pipe suffix are handled", a == "Dream Team" && t == "Sleepless City", a + " | " + t);
            LyricsGet.CleanName("Sleepless City", out a, out t); Check("a name with no artist is only a title", a == "" && t == "Sleepless City");
            var f1 = LyricsGet.Fetch("Neon Fox", "Night Owl"); Check("an exact artist and title finds the song", f1 != null && f1.Title == "Night Owl" && f1.Lyrics.Contains("searching for a light"));
            var f2 = LyricsGet.Fetch("Dream Team", "Sleepless City"); Check("otherwise the search picks the matching song, not the first one", f2 != null && f2.Artist == "Dream Team" && f2.Title == "Sleepless City", f2 == null ? "null" : f2.Artist);
            Check("a song nobody has gives nothing (no wrong lyrics)", LyricsGet.Fetch("", "Nothing Like This") == null);
            Check("an instrumental gives nothing", LyricsGet.Fetch("Calm", "Quiet Room") == null);
            // the whole file
            string mp3 = Path.Combine(work, "Neon Fox - Night Owl [MP3 320k].mp3"); File.WriteAllText(mp3, "x"); string msg = LyricsGet.SaveFor(mp3); string txt = Path.ChangeExtension(mp3, ".txt");
            string body2 = File.Exists(txt) ? File.ReadAllText(txt, Encoding.UTF8) : "";
            Check("the .txt is saved next to the song with the same name", File.Exists(txt) && msg.StartsWith("Lyrics saved"), msg);
            Check("it has the title, the artist, labelled parts and the note about whose lyrics they are", body2.StartsWith("Night Owl\r\nNeon Fox") && body2.Contains("[Verse 1]") && body2.Contains("[Chorus]") && body2.Contains("belong to their writers"), body2.Length > 60 ? body2.Substring(0, 60).Replace("\r\n", "/") : body2);
            Check("the chorus is the part that comes back (written once as a label for each time)", System.Text.RegularExpressions.Regex.Matches(body2, @"\[Chorus\]").Count >= 2);
            Check("running it again does not overwrite what is there", LyricsGet.SaveFor(mp3).Contains("already there"));
            // new files in a folder
            var before = LyricsGet.Snapshot(work); string m2 = Path.Combine(work, "Dream Team - Sleepless City [MP3 320k].mp3"); File.WriteAllText(m2, "x"); File.WriteAllText(Path.Combine(work, "notes.txt"), "keep"); var log = new List<string>(); LyricsGet.ForNewFiles(work, before, s => log.Add(s));
            Check("only the songs that are new since the snapshot get lyrics", log.Count == 1 && File.Exists(Path.ChangeExtension(m2, ".txt")) && File.ReadAllText(Path.Combine(work, "notes.txt")) == "keep", string.Join(" / ", log.ToArray()));
            string m3 = Path.Combine(work, "Nobody - Unknown Track [MP3 320k].mp3"); File.WriteAllText(m3, "x"); string none = LyricsGet.SaveFor(m3);
            Check("a song with no match says so and saves nothing", none.StartsWith("No lyrics found") && !File.Exists(Path.ChangeExtension(m3, ".txt")), none);
            down = true; string m4 = Path.Combine(work, "Neon Fox - Night Owl Two [MP3 320k].mp3"); File.WriteAllText(m4, "x"); string off = LyricsGet.SaveFor(m4);
            Check("when the lyrics database is down the converter carries on with a plain sentence", off.StartsWith("Could not look up") && !File.Exists(Path.ChangeExtension(m4, ".txt")), off);
            Check("the setting is on by default and is saved and read back", Prefs.SaveLyrics && string.Join("|", Prefs.Lines()).Contains("savelyrics=True"));
        }
        finally { try { srv.Stop(); } catch { } try { Directory.Delete(work, true); } catch { } }
        Console.WriteLine(fails == 0 ? "=== lyrics getter checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
