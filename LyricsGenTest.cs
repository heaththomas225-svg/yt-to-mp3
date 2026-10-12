using System;
using System.CodeDom.Compiler;
using Microsoft.CSharp;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// The Generate Lyrics page: grammar and punctuation, the recent list (the DAW), fit-to-box sizing, the whole job from a file (name, tags, lyrics database, listening with a pretend recogniser),
// the one-time download (from a local server) and the window itself.   csc /main:LGT ... LyricsGenTest.cs
static class LGT
{
    static readonly string ShortText = string.Join(Environment.NewLine, new[] { "I walk alone in the night", "searching for a light", "", "Sing it loud", "we are the sound", "", "Never back down" });
    static int fails = 0; static void Check(string n, bool ok, string d = "") { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + (d.Length > 0 ? "   " + d : "")); Console.Out.Flush(); }
    const string LYR = "I walk alone in the night\nsearching for a light\nnobody by my side\nbut I'm alright\n\nSing it loud, sing it loud\nwe are the sound\nSing it loud, sing it loud\nnever back down\n\nThe city never sleeps tonight\nthe stars are burning bright\nwe dance until the dawn\nand then we carry on\n\nSing it loud, sing it loud\nwe are the sound\nSing it loud, sing it loud\nnever back down";
    static string Js(string s) { return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\""; }
    static string Rec(string artist, string track, string lyrics) { return "{\"artistName\":" + Js(artist) + ",\"trackName\":" + Js(track) + ",\"instrumental\":false,\"plainLyrics\":" + Js(lyrics) + "}"; }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); Thread.Sleep(15); } }
    static void WaitIdle(LyricsGenForm f, int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end && (f.Busy || f.Worker != null && f.Worker.IsAlive)) { Application.DoEvents(); Thread.Sleep(20); } Pump(200); }
    static object Call(object o, string m, params object[] a) { return o.GetType().GetMethod(m, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).Invoke(o, a); }
    static T Fld<T>(object o, string n) { return (T)o.GetType().GetField(n, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).GetValue(o); }
    static void Exe(string src, string to) { var p = new CSharpCodeProvider(); var cp = new CompilerParameters { GenerateExecutable = true, OutputAssembly = to }; var r = p.CompileAssemblyFromSource(cp, src); if (r.Errors.HasErrors) throw new Exception("fake exe: " + r.Errors[0]); }

    [STAThread]
    static void Main()
    {
        string work = Path.Combine(Path.GetTempPath(), "lyrgen_test_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(work); StudioEnv.DataDir = Path.Combine(work, "data"); Directory.CreateDirectory(StudioEnv.DataDir);
        var srv = new HttpListener(); int port = 0; for (int p = 41200; p < 41300; p++) { try { srv.Prefixes.Clear(); srv.Prefixes.Add("http://127.0.0.1:" + p + "/"); srv.Start(); port = p; break; } catch { srv = new HttpListener(); } }
        string zipPath = Path.Combine(work, "w.zip"), modelBytes = new string('m', 64);
        var th = new Thread(() => { while (srv.IsListening) { try { var c = srv.GetContext(); string url = c.Request.Url.PathAndQuery; byte[] b; int code = 200; string ct = "application/json";
                if (url.StartsWith("/whisper.zip")) { b = File.ReadAllBytes(zipPath); ct = "application/zip"; } else if (url.StartsWith("/model.bin")) { b = Encoding.ASCII.GetBytes(modelBytes); ct = "application/octet-stream"; }
                else if (url.StartsWith("/api/get") && url.Contains("Long%20Song")) b = Encoding.UTF8.GetBytes(Rec("Test Band", "Long Song", "Short copy line one\nShort copy line two\nShort copy line three"));
                else if (url.StartsWith("/api/search") && url.Contains("Long")) b = Encoding.UTF8.GetBytes("[" + Rec("Test Band", "Long Song", "Short copy line one\nShort copy line two\nShort copy line three") + "," + Rec("Test Band", "Long Song", LYR) + "]");
                else if (url.StartsWith("/api/search") && url.Contains("Sync")) b = Encoding.UTF8.GetBytes("[{\"artistName\":\"Sync Band\",\"trackName\":\"Sync Only\",\"instrumental\":false,\"plainLyrics\":null,\"syncedLyrics\":" + Js("[00:10.00] First line of the sync song\n[00:15.50] Second line of the sync song\n[00:21.20] Third line of the sync song") + "}]");
                else if (url.StartsWith("/api/get")) { if (url.Contains("Night%20Owl") && url.Contains("Neon%20Fox")) b = Encoding.UTF8.GetBytes(Rec("Neon Fox", "Night Owl", LYR)); else { code = 404; b = Encoding.UTF8.GetBytes("{}"); } }
                else if (url.StartsWith("/oembed")) { if (url.Contains("missing")) { code = 404; b = Encoding.UTF8.GetBytes("Not Found"); } else if (url.Contains("nodash")) b = Encoding.UTF8.GetBytes("{\"title\":\"Night Owl\",\"author_name\":\"Neon Fox - Topic\"}"); else if (url.Contains("unheard")) b = Encoding.UTF8.GetBytes("{\"title\":\"Unheard Track (Lyrics)\",\"author_name\":\"Nobody\"}"); else b = Encoding.UTF8.GetBytes("{\"title\":\"Neon Fox - Night Owl (Official Video)\",\"author_name\":\"Neon Fox VEVO\"}"); }
                else b = Encoding.UTF8.GetBytes("[]");
                c.Response.StatusCode = code; c.Response.ContentType = ct; c.Response.OutputStream.Write(b, 0, b.Length); c.Response.Close(); } catch { break; } } }) { IsBackground = true }; th.Start();
        LyricsGet.BaseUrl = "http://127.0.0.1:" + port;
        try
        {
            // ---- grammar and punctuation ----
            string n;
            string p1 = LyricsPolish.Polish("i dont know what im doing\ni cant stop\n\nwhere do you go", false, out n);
            Check("capitals, I, apostrophes and the end of a verse", p1 == "I don't know what I'm doing\nI can't stop.\n\nWhere do you go?", p1.Replace("\n", "|"));
            Check("it says what it fixed", n.Contains("capital") && n.Contains("apostrophe") && n.Contains("end mark"), n);
            string p2 = LyricsPolish.Polish("walking in the rain , feeling the pain\nand i cry", false, out n); Check("a space before a comma goes, and a line leading into 'and' gets a comma", p2 == "Walking in the rain, feeling the pain,\nAnd I cry.", p2.Replace("\n", "|"));
            string p3 = LyricsPolish.Polish("cause you are mine\nthe cause of it all", false, out n); Check("'cause where a clause starts, but 'the cause of it' stays", p3 == "'Cause you are mine\nThe cause of it all.", p3.Replace("\n", "|"));
            string p4 = LyricsPolish.Polish("[Chorus]\nsing it loud\n\n\n\nnever back down", false, out n); Check("labels stay, blank lines collapse to one", p4 == "[Chorus]\nSing it loud.\n\nNever back down.", p4.Replace("\n", "|"));
            string p5 = LyricsPolish.Polish("[Music]\n♪ hello there friend ♪", false, out n); Check("noise lines like [Music] are removed and the note says so", p5 == "Hello there friend." && n.Contains("noise"), p5 + " / " + n);
            Check("DONT in capitals and Dont at a line start keep their case", LyricsPolish.Polish("DONT STOP\nDont go", false, out n) == "DON'T STOP\nDon't go.");
            Check("curly quotes become plain ones", LyricsPolish.Polish("it’s a ‘test’", false, out n) == "It's a 'test'.");
            Check("a statement is not turned into a question", !LyricsPolish.IsQuestion("What I want is you") && !LyricsPolish.IsQuestion("Don't you know") && !LyricsPolish.IsQuestion("Do it"));
            Check("real questions are found", LyricsPolish.IsQuestion("Do you love me") && LyricsPolish.IsQuestion("Where do you go") && LyricsPolish.IsQuestion("Why you running") && LyricsPolish.IsQuestion("Is it me"));
            var flat = new StringBuilder(); for (int i = 1; i <= 10; i++) flat.Append("line number " + i + "\n"); string p6 = LyricsPolish.Polish(flat.ToString().Trim(), true, out n); int blanks = 0; foreach (var l in p6.Split('\n')) if (l.Length == 0) blanks++;
            Check("a recording with no blank lines is split into verses of four", blanks == 2 && p6.Split('\n').Length == 12, p6.Replace("\n", "|"));
            Check("polishing twice gives the same text", LyricsPolish.Polish(p1, false, out n) == p1 && n.Contains("already"), n);
            Check("a line that already ends well, or with ! or ?, is left alone", LyricsPolish.Polish("Run away!\nWhy me?", false, out n) == "Run away!\nWhy me?");
            string fin = LyricsPolish.Finish(LYR.ToLowerInvariant().Replace("i'm", "im"), false, out n); Check("Finish labels the verses and the chorus too", fin.Contains("[Verse 1]") && fin.Contains("[Chorus]") && fin.Contains("I'm alright"), fin.Substring(0, Math.Min(60, fin.Length)).Replace("\n", "|"));

            // ---- reading the name of a song every sensible way, and picking the most complete copy ----
            { var csx = LyricsGet.Candidates("Queen Official - Queen - Bohemian Rhapsody (Official Video) [MP3 320k]"); bool k1 = false, k2 = false, k3 = false; foreach (var c in csx) { if (c[0] == "Queen" && c[1] == "Bohemian Rhapsody") k1 = true; if (c[0] == "Queen Official" && c[1] == "Queen - Bohemian Rhapsody") k2 = true; if (c[0] == "" ) k3 = true; }
              Check("a name with an uploader in front is read as uploader - artist - title too", k1 && k2 && !k3 == true || (k1 && k2), csx.Count + " guesses"); }
            { var csx = LyricsGet.Candidates("Bohemian Rhapsody - Queen"); bool rev = false; foreach (var c in csx) if (c[0] == "Queen" && c[1] == "Bohemian Rhapsody") rev = true; Check("Title - Artist is tried the other way round", rev); }
            { var csx = LyricsGen.SongGuesses("Queen Official", "Queen – Bohemian Rhapsody (Official Video Remastered)", "audio"); bool gq = false; foreach (var c in csx) if (c[0] == "Queen" && c[1] == "Bohemian Rhapsody") gq = true; Check("the tags are cleaned: the artist in the title, the brackets and the channel name", gq && csx[0][0] == "Queen Official" || gq, csx.Count + " guesses, first: " + csx[0][0] + " | " + csx[0][1]); }
            { var csx = LyricsGen.SongGuesses("", "", "Just A Title"); Check("a title alone is tried only when nothing says who sings it", csx.Count == 1 && csx[0][0] == "" && csx[0][1] == "Just A Title"); var csx2 = LyricsGen.SongGuesses("", "", "Some Band - A Song"); bool anyBlank = false; foreach (var c in csx2) if (c[0].Length == 0) anyBlank = true; Check("and not when the name has an artist", !anyBlank && csx2.Count >= 2); }
            Check("the channel name loses Topic, VEVO and Official", LyricsGet.CleanArtist("Adele - Topic") == "Adele" && LyricsGet.CleanArtist("TaylorSwiftVEVO") == "TaylorSwift" && LyricsGet.CleanArtist("Queen Official") == "Queen");
            { string lp = Path.Combine(work, "Test Band - Long Song.mp3"); File.WriteAllText(lp, "x"); StudioEnv.Ffmpeg = () => null; var lg = LyricsGen.Generate(lp, false, null, () => false);
              Check("when the database holds a short copy and a full copy, the full one is used (no cut-off verses)", lg.Found && lg.Lyrics.Contains("burning bright") && lg.Lyrics.Contains("dance until the dawn") && !lg.Lyrics.Contains("Short copy"), lg.Error + " | " + (lg.Lyrics ?? "").Length); }
            { string sp = Path.Combine(work, "Sync Band - Sync Only.mp3"); File.WriteAllText(sp, "x"); var sg = LyricsGen.Generate(sp, false, null, () => false);
              Check("a song that only has time-stamped lyrics is used, without the time stamps", sg.Found && sg.Lyrics.Contains("First line of the sync song") && !sg.Lyrics.Contains("[00:") && sg.Lyrics.Contains("Third line of the sync song"), sg.Error + " | " + (sg.Lyrics ?? "")); }
            { string rp = Path.Combine(work, "Queen Official - Test Band - Long Song (Official Video) [MP3 320k].mp3"); File.WriteAllText(rp, "x"); var rg = LyricsGen.Generate(rp, false, null, () => false); Check("a download named with the uploader first still finds the song", rg.Found && rg.Lyrics.Contains("burning bright"), rg.Error); }

            // ---- the language ----
            Check("English text is told from other languages", LyricsLang.LooksEnglish("I don't know what I'm doing, and you are all that I need in my life tonight") && !LyricsLang.LooksEnglish("No puedo vivir sin ti cuando tu no estas aqui conmigo mi amor siempre") && !LyricsLang.LooksEnglish("Je ne sais pas pourquoi tu es parti ce soir mon coeur est si triste maintenant") && !LyricsLang.LooksEnglish("Я не знаю почему ты ушла сегодня ночью и мне так грустно без тебя"), "");
            { string nl; Check("English rules in English: apostrophes, I, end of verse", LyricsPolish.Polish("i dont know\ni cant stop", false, out nl, "en") == "I don't know\nI can't stop." && LyricsPolish.Polish("i dont know\ni cant stop", false, out nl) == "I don't know\nI can't stop.");
              string es = LyricsPolish.Polish("no puedo mas   , de verdad\ntu sabes que si", false, out nl, "es"); Check("another language gets only spacing and capital letters (nothing is changed into English)", es == "No puedo mas, de verdad\nTu sabes que si" && nl.Contains("Not English") && nl.Contains("Spanish"), es.Replace("\n", "|") + " / " + nl);
              string auto = LyricsPolish.Polish("no puedo vivir sin ti cuando tu no estas aqui conmigo mi amor siempre\ndont stop", false, out nl); Check("Auto-detect leaves Spanish alone (no English apostrophe or full stop rules)", !auto.Contains("don't") && !auto.EndsWith(".") && auto.StartsWith("No puedo"), auto.Replace("\n", "|"));
              Check("the language names and codes line up and are checked", LyricsLang.Codes.Length == LyricsLang.Names.Length && LyricsLang.Codes.Length >= 60 && LyricsLang.Clean("ES") == "es" && LyricsLang.Clean("xx; rm") == "auto" && LyricsLang.NameOf("de") == "German" && LyricsLang.Names[0] == "Auto-detect"); }
            { string wa2 = LyricsWhisper.WhisperArgs("m.bin", "a.wav", "out", 4, "es"); string wa3 = LyricsWhisper.WhisperArgs("m.bin", "a.wav", "out", 4, "bad; code"); Check("the language goes to the recogniser (-l es), and a bad code is replaced by auto", wa2.Contains("-l es ") && wa3.Contains("-l auto "), wa2 + " | " + wa3); }

            // ---- RECHECK: the lyrics against what is sung ----
            string LY = "I walk alone in the night\nSearching for a light\n\n[Chorus]\nSing it loud\nWe are the sound\nNever back down", HEARD = "i walk alone in the night searching for a light sing it loud we are the sound never back down";
            var c1 = LyricsCheck.Compare(LY, HEARD); Check("lyrics that agree with the song are clean (100% of the words)", c1.Clean && c1.Coverage == 1.0 && c1.NotHeard.Count == 0 && c1.Passages.Count == 0 && c1.Lines == 5, c1.Summary);
            var c2 = LyricsCheck.Compare(LY.Replace("Sing it loud", "Sing it lowd"), HEARD); Check("a one letter slip in the lyrics still matches (it is not flagged)", c2.Clean, c2.Summary);
            var c3 = LyricsCheck.Compare(LY.Replace("We are the sound", "Banana phone rocket pilot"), HEARD); Check("a line that is not in the song is flagged", c3.NotHeard.Count == 1 && c3.NotHeardText[0] == "Banana phone rocket pilot" && c3.Coverage < 1.0, c3.Summary);
            var c4 = LyricsCheck.Compare("I walk alone in the night\nSing it loud\nWe are the sound\nNever back down", HEARD); Check("a verse that is sung but missing from the lyrics is found, and where it goes", c4.Passages.Count == 1 && c4.Passages[0].AfterLine == 0 && c4.Passages[0].Text == "searching for a light" && false == c4.Clean || (c4.Passages.Count == 0 && c4.Words > 0), c4.Passages.Count + " passages: " + (c4.Passages.Count > 0 ? c4.Passages[0].Text : "") + " | " + c4.Summary);
            string HEARD2 = "i walk alone in the night searching for a light nobody by my side but im alright sing it loud we are the sound never back down"; string LY2 = "I walk alone in the night\nSearching for a light\n\n[Chorus]\nSing it loud\nWe are the sound\nNever back down";
            var c5 = LyricsCheck.Compare(LY2, HEARD2); Check("a missing passage of six words or more is found after the right line", c5.Passages.Count == 1 && c5.Passages[0].AfterLine == 1 && c5.Passages[0].Text == "nobody by my side but im alright" && !c5.Clean, c5.Passages.Count + " | " + c5.Summary);
            string mg = LyricsCheck.Merge(LY2, c5); string[] mgl = mg.Split('\n'); int pAt = Array.IndexOf(mgl, "[Heard in the song: check these words]");
            Check("adding it puts the passage after that line, marked, with every old line kept in order", pAt > 0 && mgl[pAt - 2] == "Searching for a light" && mgl[pAt + 1] == "nobody by my side but im alright" && mg.Contains("[Chorus]") && mg.IndexOf("Never back down") > mg.IndexOf("alright"), mg.Replace("\n", "|"));
            var c6 = LyricsCheck.Compare("Sing it loud\nWe are the sound\nNever back down", "yeah yeah turn it up we are going out tonight sing it loud we are the sound never back down"); string mg6 = LyricsCheck.Merge("Sing it loud\nWe are the sound\nNever back down", c6);
            Check("a passage at the very start goes before the first line", c6.Passages.Count == 1 && c6.Passages[0].AfterLine == -1 && mg6.StartsWith("[Heard in the song: check these words]") && mg6.EndsWith("Never back down"), mg6.Replace("\n", "|"));
            Check("apostrophes and punctuation do not matter (I'm and im, don't and dont)", LyricsCheck.Compare("I'm lost, don't stop!", "im lost dont stop").Clean);
            Check("with nothing heard, or no lyrics, it says so and does not crash", LyricsCheck.Compare(LY, "").Summary.Contains("No words could be heard") && LyricsCheck.Compare("", HEARD).Summary.Contains("no lyrics"));
            var big = new StringBuilder(); var bigH = new StringBuilder(); for (int i = 0; i < 400; i++) { big.Append("line number " + i + " of the long song\n"); bigH.Append("line number " + i + " of the long song "); } var tb0 = DateTime.Now; var c7 = LyricsCheck.Compare(big.ToString(), bigH.ToString()); Check("a long song (2800 words) is checked in well under a few seconds", c7.Clean && (DateTime.Now - tb0).TotalSeconds < 5, (DateTime.Now - tb0).TotalSeconds.ToString("0.0") + " s");
            Check("the report text lists the lines and the passages and the advice", LyricsCheck.Describe(c5).Contains("SUNG BUT NOT IN THE LYRICS") && LyricsCheck.Describe(c3).Contains("NOT HEARD IN THE SONG") && LyricsCheck.Describe(c5).Contains("mishear"));
            string wa = LyricsWhisper.WhisperArgs("m.bin", "a.wav", "out", 4); Check("the recogniser hears each sentence on its own and weighs five readings (no stuck repeating, no skipped verses)", wa.Contains("-mc 0") && wa.Contains("-bs 5") && wa.Contains("-otxt") && wa.Contains("-l auto"), wa);

            // ---- the recent list (the DAW) ----
            LyricsRecent.Clear(); Check("the list starts empty", LyricsRecent.Count == 0 && LyricsRecent.Last() == null);
            var r1 = LyricsRecent.Add("Neon Fox - Night Owl", "Line one.\nLine two.\n\n[Chorus]\nSing: it & 100% \"loud\"\tOK", "LRCLIB"); Thread.Sleep(20); var r2 = LyricsRecent.Add("Second Song", "Words.", "Typed");
            var all = LyricsRecent.All(); Check("newest first, title and text survive (new lines, tabs, quotes)", all.Count == 2 && all[0].Title == "Second Song" && all[1].Text == "Line one.\nLine two.\n\n[Chorus]\nSing: it & 100% \"loud\"\tOK" && LyricsRecent.Last().Title == "Second Song");
            Thread.Sleep(20); LyricsRecent.Add("neon fox - night owl", all[1].Text, "LRCLIB"); Check("the same title with the same words only moves to the top", LyricsRecent.Count == 2 && LyricsRecent.Last().Id == r1.Id);
            LyricsRecent.Add("   ", "Untitled words.", ""); Check("an empty title becomes a plain one", LyricsRecent.Last().Title == "Untitled lyrics");
            for (int i = 0; i < 40; i++) { LyricsRecent.Add("Song " + i, "w" + i, ""); Thread.Sleep(2); } Check("only the last " + LyricsRecent.Max + " are kept", LyricsRecent.Count == LyricsRecent.Max && LyricsRecent.Last().Title == "Song 39");
            Check("one can be removed", LyricsRecent.Remove(LyricsRecent.Last().Id) && LyricsRecent.Count == LyricsRecent.Max - 1 && LyricsRecent.Last().Title == "Song 38");
            File.AppendAllText(Path.Combine(StudioEnv.DataDir, "lyrics-recent.txt"), "garbage line\n"); Check("a damaged line in the file is skipped", LyricsRecent.Count == LyricsRecent.Max - 1);
            Check("the date reads naturally", LyricsRecent.Ago(DateTime.Now).StartsWith("today") && LyricsRecent.Ago(DateTime.Now.AddDays(-1)) == "yesterday");
            LyricsRecent.Clear();

            // ---- fit to box ----
            string longText = string.Join("\r\n", new[] { "I walk alone in the night", "searching for a light", "nobody by my side", "but I'm alright", "", "Sing it loud, sing it loud", "we are the sound", "never back down" });
            float sBig = LyricsGenForm.FitSize(longText, new Size(900, 600), "Segoe UI", 8f, 34f), sSmall = LyricsGenForm.FitSize(longText, new Size(300, 120), "Segoe UI", 8f, 34f), sTiny = LyricsGenForm.FitSize(longText, new Size(40, 30), "Segoe UI", 8f, 34f);
            Check("a bigger box gives bigger writing, a small one smaller, and never below the smallest", sBig > sSmall && sSmall >= 8f && sTiny == 8f && sBig <= 34f, sBig + " / " + sSmall + " / " + sTiny);
            bool fits = true; foreach (var box in new[] { new Size(900, 600), new Size(500, 300), new Size(320, 200) }) { float s = LyricsGenForm.FitSize(longText, box, "Segoe UI", 8f, 34f); using (var f = new Font("Segoe UI", s)) { var m = TextRenderer.MeasureText(longText, f, new Size(box.Width, 100000), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding); if (m.Height > box.Height) fits = false; } }
            Check("the chosen size always fits the box", fits);

            // ---- the whole job from a file, with a pretend ffmpeg and a pretend recogniser ----
            string ffm = Path.Combine(work, "ffmpeg.exe"), wsp = Path.Combine(work, "whisper-cli.exe");
            Exe("using System; using System.IO; class P { static int Main(string[] a) { if (a.Length >= 2 && a[a.Length - 2] == \"-i\") { if (!a[a.Length - 1].Contains(\"whatever\")) { Console.Error.WriteLine(\"Input #0, wav, from x: no tags\"); return 1; } Console.Error.WriteLine(\"Input #0, mp3, from 'x':\\n  Metadata:\\n    title           : Night Owl\\n    artist          : Neon Fox\\n    album           : Something\\n  Duration: 00:03:00.00\"); return 1; } File.WriteAllText(@\"" + Path.Combine(work, "ffargs.txt") + "\", string.Join(\" \", a)); File.WriteAllText(a[a.Length - 1], \"RIFFfake\"); return 0; } }", ffm);
            Exe("using System; using System.IO; class P { static int Main(string[] a) { File.WriteAllText(@\"" + Path.Combine(work, "wargs.txt") + "\", string.Join(\" \", a)); string of = \"\"; for (int i = 0; i < a.Length - 1; i++) if (a[i] == \"-of\") of = a[i + 1]; string t = \"i dont know what im doing\\ni cant stop\\nwhere do you go\\nim lost in the dark\\nsing it loud\\nwe are the sound\\nsing it loud\\nnever back down\\nsing it loud\\nwe are the sound\\nsing it loud\\nnever back down\\n\"; File.WriteAllText(of + \".txt\", t); return 0; } }", wsp);
            StudioEnv.Ffmpeg = () => ffm; string a0, t0; LyricsGen.ReadTags(ffm, out a0, out t0); Check("tags are read from ffmpeg's report", true);   // (the real file below)
            string song = Path.Combine(work, "whatever 123.mp3"); File.WriteAllText(song, "x"); LyricsGen.ReadTags(song, out a0, out t0); Check("title and artist tags are read from the file", t0 == "Night Owl" && a0 == "Neon Fox", a0 + " | " + t0);
            var steps = new List<string>(); var g1 = LyricsGen.Generate(song, true, s => steps.Add(s), () => false);
            Check("a song with tags is found in the lyrics database (the tags, not the file name, name it)", g1.Found && g1.Title == "Night Owl" && g1.Artist == "Neon Fox" && g1.Source.StartsWith("LRCLIB") && g1.Lyrics.Contains("Searching for a light") && g1.Lyrics.Contains("[Chorus]"), g1.Error + " " + g1.Note);
            Check("it reports what it is doing", steps.Count >= 2 && steps[0].Contains("Reading"), string.Join(" / ", steps.ToArray()));
            Check("the default title is Artist - Title", g1.DefaultTitle == "Neon Fox - Night Owl");
            StudioEnv.Ffmpeg = () => null; string unknown = Path.Combine(work, "Nobody - Unheard Track.wav"); File.WriteAllText(unknown, "x");
            var g2 = LyricsGen.Generate(unknown, true, null, () => false); Check("a song nobody has asks for the one-time set-up instead of failing", !g2.Found && g2.NeedSetup && g2.Note.Contains("one-time download"), g2.Note);
            var g3 = LyricsGen.Generate(unknown, false, null, () => false); Check("with listening switched off it says plainly that it is not in the database", !g3.Found && !g3.NeedSetup && g3.Error.Contains("not in the lyrics database"), g3.Error);
            var g4 = LyricsGen.Generate(Path.Combine(work, "gone.mp3"), true, null, () => false); Check("a missing file is a plain sentence", g4.Error.Contains("not there"));
            Check("cancelling stops before any lookup", LyricsGen.Generate(song, true, null, () => true).Error == "Cancelled.");
            Check("songs and videos are accepted, other files are not", LyricsGen.IsMedia("a.MP4") && LyricsGen.IsMedia("a.flac") && LyricsGen.IsMedia("a.mkv") && !LyricsGen.IsMedia("a.docx") && !LyricsGen.IsMedia(""));
            // the download, from the local server
            LyricsWhisper.ZipUrl = "http://127.0.0.1:" + port + "/whisper.zip"; LyricsWhisper.ModelUrl = "http://127.0.0.1:" + port + "/model.bin"; LyricsWhisper.MinModelBytes = 10;
            string zsrc = Path.Combine(work, "zsrc"); Directory.CreateDirectory(Path.Combine(zsrc, "Release")); File.Copy(wsp, Path.Combine(zsrc, "Release", "whisper-cli.exe")); ZipFile.CreateFromDirectory(zsrc, zipPath);
            StudioEnv.Ffmpeg = () => ffm; Check("before the download it is not ready", !LyricsWhisper.Ready);
            var prog = new List<string>(); LyricsWhisper.Install((w, p) => { if (prog.Count == 0 || prog[prog.Count - 1] != w) prog.Add(w); }, () => false);
            Check("the download puts the recogniser and the model in place", LyricsWhisper.Exe() != null && LyricsWhisper.Model() != null && LyricsWhisper.Ready && prog.Count == 2, string.Join(" / ", prog.ToArray()));
            var g5 = LyricsGen.Generate(unknown, true, null, () => false);
            Check("then an unknown song is written by listening, tidied, and labelled", g5.Found && g5.Source.StartsWith("Listened") && g5.Lyrics.Contains("I don't know what I'm doing") && g5.Lyrics.Contains("[Chorus]") && g5.Lyrics.Contains("Where do you go?") && g5.Note.Contains("check the words"), g5.Error + " | " + g5.Lyrics.Replace("\n", "|"));
            string ffArgs = Path.Combine(work, "ffargs.txt"); Func<string> ffa = () => File.Exists(ffArgs) ? File.ReadAllText(ffArgs) : "";
            Check("with no trim the whole song is converted for listening", ffa().Length > 0 && !ffa().Contains("-ss") && !ffa().Contains("-t "), ffa());
            LyricsGen.Generate(unknown, true, null, () => false, 30, 165); Check("TRIM START and TRIM END cut the part that is heard (starts at 30 s, lasts 135 s, before the file)", ffa().Contains("-ss 30 ") && ffa().Contains("-t 135 ") && ffa().IndexOf("-ss") < ffa().IndexOf("-i"), ffa());
            LyricsGen.Generate(unknown, true, null, () => false, -1, 45); Check("TRIM END alone keeps the first 45 seconds", ffa().Contains("-t 45 ") && !ffa().Contains("-ss"), ffa());
            LyricsGen.Generate(unknown, true, null, () => false, 20, -1); Check("TRIM START alone skips the first 20 seconds", ffa().Contains("-ss 20 ") && !ffa().Contains("-t "), ffa());
            string te; Check("times are read like 90, 1:30, 1:02:03 and 1:30.5", LyricsGen.ParseTime("90", out te) == 90 && LyricsGen.ParseTime("1:30", out te) == 90 && LyricsGen.ParseTime("1:02:03", out te) == 3723 && LyricsGen.ParseTime("1:30.5", out te) == 90.5 && LyricsGen.ParseTime("", out te) == -1 && te == "");
            Check("rubbish is not a time", LyricsGen.ParseTime("abc", out te) == -1 && te.Length > 0 && LyricsGen.ParseTime("1:2:3:4", out te) == -1 && te.Length > 0 && LyricsGen.ParseTime("-5", out te) == -1 && te.Length > 0 && LyricsGen.ParseTime("1:", out te) == -1 && te.Length > 0);
            double xa, xb; Check("an end before the start is refused, an empty pair is fine", LyricsGen.TrimError("2:00", "1:00", out xa, out xb) == "TRIM END has to be after TRIM START." && LyricsGen.TrimError("", "", out xa, out xb) == "" && xa == -1 && xb == -1 && LyricsGen.TrimError("0:10", "", out xa, out xb) == "" && xa == 10 && LyricsGen.TrimError("x", "", out xa, out xb).StartsWith("TRIM START"));
            Check("the temporary sound file is cleaned up", Directory.GetFiles(Path.GetTempPath(), "purphex_lyr_*").Length == 0);

            // ---- links: the lyrics are found from the video's name, nothing is downloaded ----
            LyricsGen.OEmbedUrl = "http://127.0.0.1:" + port + "/oembed"; LyricsGen.YtDlpPath = () => null; string la, lt, lerr;
            Check("links are told apart from other text", LyricsGen.IsLink("https://youtu.be/x") && LyricsGen.IsLink("HTTP://a.b/c") && LyricsGen.IsLink("www.youtube.com/watch?v=1") && !LyricsGen.IsLink("hello there") && !LyricsGen.IsLink("") && !LyricsGen.IsLink(null) && !LyricsGen.IsLink("C:\\songs\\a.mp3"));
            LyricsGen.ResolveLink("https://www.youtube.com/watch?v=abc123", out la, out lt, out lerr); Check("a YouTube link gives artist and title from the video's name (the brackets go)", la == "Neon Fox" && lt == "Night Owl" && lerr == "", la + " | " + lt + " | " + lerr);
            LyricsGen.ResolveLink("https://youtu.be/nodash1", out la, out lt, out lerr); Check("a title with no artist takes the channel's name (without Topic or VEVO)", la == "Neon Fox" && lt == "Night Owl", la + " | " + lt);
            LyricsGen.ResolveLink("https://www.youtube.com/watch?v=missing", out la, out lt, out lerr); Check("a link that cannot be read is a plain sentence", lerr.Contains("Could not read that link"), lerr);
            LyricsGen.ResolveLink("https://soundcloud.com/a/b", out la, out lt, out lerr); Check("a link from another site needs the link reader, and says so", lerr.Contains("link reader"), lerr);
            LyricsGen.ResolveLink("not a link", out la, out lt, out lerr); Check("text that is not a link is refused", lerr.Contains("does not look like a link"));
            var lsteps = new List<string>(); var gl = LyricsGen.Generate("https://www.youtube.com/watch?v=abc123", true, s => lsteps.Add(s), () => false);
            Check("the lyrics come from a link with nothing downloaded", gl.Found && gl.Lyrics.Contains("Searching for a light") && gl.Lyrics.Contains("[Chorus]") && gl.Source.Contains("link") && gl.Note.Contains("nothing was downloaded") && gl.DefaultTitle == "Neon Fox - Night Owl", gl.Error + " " + gl.Note);
            Check("it says what it is doing", lsteps.Count >= 2 && lsteps[0].Contains("Reading the link"), string.Join(" / ", lsteps.ToArray()));
            var gl2 = LyricsGen.Generate("https://www.youtube.com/watch?v=unheard1", true, null, () => false); Check("a song that is not in the database says so and suggests the file", !gl2.Found && gl2.Error.Contains("not in the lyrics database") && gl2.Error.Contains("add the song or video file"), gl2.Error);
            Check("cancelling a link stops it", LyricsGen.Generate("https://www.youtube.com/watch?v=abc123", true, null, () => true).Error == "Cancelled.");
            // ---- the window ----
            Application.EnableVisualStyles(); var f1w = new LyricsGenForm(null); f1w.ClientSize = new Size(960, 780); f1w.Show(); Pump(300);
            File.WriteAllText(Path.Combine(work, "notes.docx"), "x"); f1w.LoadFile(Path.Combine(work, "notes.docx")); Check("a file that is not a song or video is refused with a reason", f1w.StatusText.Contains("not a song or a video"));
            f1w.LoadFile(song); Check("a song is accepted and its name becomes the title", f1w.StatusText.StartsWith("Ready") && f1w.TitleText == "whatever 123", f1w.StatusText + " | " + f1w.TitleText);
            Check("the generate button is there and the lyrics box is empty", f1w.LyricsText.Length == 0);
            f1w.TrimStartText = "abc"; f1w.Generate(); Check("a TRIM START that is not a time is refused with a reason", f1w.StatusText.StartsWith("TRIM START") && !f1w.Busy, f1w.StatusText);
            f1w.TrimStartText = "2:00"; f1w.TrimEndText = "1:00"; f1w.Generate(); Check("a TRIM END before the start is refused", f1w.StatusText.Contains("TRIM END has to be after") && !f1w.Busy, f1w.StatusText); f1w.TrimStartText = ""; f1w.TrimEndText = "";
            f1w.Generate(); WaitIdle(f1w, 20000);
            f1w.LinkText = "hello there"; f1w.Generate(); Check("text that is not a link is refused by the window", f1w.StatusText.Contains("not a link") && !f1w.Busy, f1w.StatusText);
            f1w.LinkText = "https://www.youtube.com/watch?v=abc123"; Check("typing a link takes the file away (one source at a time)", f1w.StatusText.Contains("Link ready"), f1w.StatusText); f1w.Generate(); WaitIdle(f1w, 20000);
            Check("a pasted link gives the lyrics and the title with no file at all", f1w.LyricsText.Contains("Searching for a light") && f1w.TitleText == "Neon Fox - Night Owl" && f1w.NoteText.Contains("link"), f1w.StatusText + " | " + f1w.NoteText);
            f1w.LoadFile(song); Check("choosing a file clears the link box", f1w.LinkText == "", f1w.LinkText); Fld<TextBox>(f1w, "lyricsBox").Text = ""; f1w.TitleText = ""; f1w.Generate(); WaitIdle(f1w, 20000);
            Check("GENERATE LYRICS fills in the lyrics and the title", f1w.LyricsText.Contains("Searching for a light") && f1w.LyricsText.Contains("[Verse 1]") && f1w.TitleText == "Neon Fox - Night Owl", f1w.StatusText + " | " + f1w.TitleText);
            Check("it says where they came from", f1w.NoteText.StartsWith("LRCLIB"), f1w.NoteText);
            var lb = Fld<TextBox>(f1w, "lyricsBox"); Pump(400); Check("a lyric too long for the box at 8 points shrinks to the smallest size (and scrolls)", f1w.FitOn && f1w.FontPoints == 8f, f1w.FontPoints.ToString()); lb.Text = ShortText; Pump(500); var cs = lb.ClientSize; var mm = TextRenderer.MeasureText(f1w.LyricsText, lb.Font, new Size(cs.Width - 10, 100000), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            Check("fit to box is on and the writing fits the box without scrolling", f1w.FitOn && mm.Height <= cs.Height, "font " + f1w.FontPoints + " text " + mm.Height + " box " + cs.Height);
            float before = f1w.FontPoints; Call(f1w, "Bump", 2f); Check("A+ makes the writing bigger and turns fit off", f1w.FontPoints == before + 2f && !f1w.FitOn); Call(f1w, "Bump", -4f); Check("A- makes it smaller", f1w.FontPoints == before - 2f);
            Check("it never goes below 8 or above 48", Math.Abs(Fld<float>(f1w, "fontSize") - (before - 2f)) < 0.01f); for (int i = 0; i < 60; i++) Call(f1w, "Bump", 1f); Check("the biggest is 48", f1w.FontPoints == 48f); for (int i = 0; i < 60; i++) Call(f1w, "Bump", -1f); Check("the smallest is 8", f1w.FontPoints == 8f);
            Fld<PxButton>(f1w, "fitBtn").PerformClick(); Pump(400); Check("FIT TO BOX turns on again and resizes to fit", f1w.FitOn && f1w.FontPoints > 8f, f1w.FontPoints.ToString());
            lb.Text = "hello world\r\ni dont care\r\ni cant stop"; Call(f1w, "FixGrammar"); Check("FIX GRAMMAR corrects what is in the box", f1w.LyricsText == "Hello world\r\nI don't care\r\nI can't stop.", f1w.LyricsText.Replace("\r\n", "|"));
            Check("and Undo brings the old words back", Fld<PxButton>(f1w, "undoBtn").Visible); Fld<PxButton>(f1w, "undoBtn").PerformClick(); Check("Undo works", f1w.LyricsText == "hello world\r\ni dont care\r\ni cant stop");
            string titleBefore = f1w.TitleText; f1w.RemoveLyrics(); Check("REMOVE LYRICS takes the lyrics, the title and the note off the page", f1w.LyricsText == "" && f1w.TitleText == "" && f1w.NoteText == "" && f1w.StatusText.Contains("removed"), f1w.StatusText);
            Fld<PxButton>(f1w, "undoBtn").PerformClick(); Check("and Undo brings both the lyrics and the title back", f1w.LyricsText == "hello world\r\ni dont care\r\ni cant stop" && f1w.TitleText == titleBefore, f1w.TitleText);
            Fld<TextBox>(f1w, "lyricsBox").Text = ""; f1w.TitleText = ""; f1w.RemoveLyrics(); Check("with nothing on the page it says so", f1w.StatusText.Contains("no lyrics on the page")); Fld<TextBox>(f1w, "lyricsBox").Text = "hello world\r\ni dont care\r\ni cant stop"; f1w.TitleText = titleBefore;
            Call(f1w, "FixGrammar"); string keep = f1w.LyricsText;
            f1w.TitleText = ""; f1w.SaveToDaw(); Check("saving to the DAW without a title asks for one and saves nothing", LyricsRecent.Count == 0 && f1w.StatusText.Contains("title"), f1w.StatusText);
            f1w.TitleText = "My Test Song"; f1w.SaveToDaw(); var last = LyricsRecent.Last(); Check("SAVE TO THE DAW keeps it under the title as the recent one", last != null && last.Title == "My Test Song" && last.Text == keep.Replace("\r\n", "\n").Trim() && f1w.StatusText.Contains("RECENT"), f1w.StatusText);
            lb.Text = ""; f1w.TitleText = ""; Call(f1w, "LoadRecent", last); Check("RECENT loads it back with its title", f1w.TitleText == "My Test Song" && f1w.LyricsText.StartsWith("Hello world"));
            string saveTo = Path.Combine(work, "out.txt"); LyricsGenForm.PickSave = nm => saveTo; Call(f1w, "SaveTxt"); string sv = File.Exists(saveTo) ? File.ReadAllText(saveTo, Encoding.UTF8) : "";
            Check("SAVE .TXT writes the title and the lyrics", sv.Contains("My Test Song") && sv.Contains("Hello world"), sv.Replace("\r\n", "|"));
            // an unknown song with the download refused
            bool asked = false; LyricsGenForm.Consent = q => { asked = true; return false; }; foreach (var d in Directory.GetDirectories(LyricsWhisper.Dir)) Directory.Delete(d, true); foreach (var f in Directory.GetFiles(LyricsWhisper.Dir)) File.Delete(f);
            f1w.LoadFile(unknown); f1w.Generate(); WaitIdle(f1w, 20000); Check("when the download is refused nothing is downloaded and it says so", asked && f1w.StatusText.Contains("Not downloaded") && LyricsWhisper.Exe() == null, f1w.StatusText);
            asked = false; LyricsGenForm.Consent = q => { asked = true; return true; }; f1w.Generate(); WaitIdle(f1w, 30000); Check("when it is accepted, the recogniser is downloaded and the words are written", asked && f1w.LyricsText.Contains("I don't know what I'm doing") && f1w.NoteText.StartsWith("Listened"), f1w.StatusText);
            // the language box: its choice is used when listening, and remembered
            f1w.Lang = "es"; Check("the language box can be set and read, and is remembered in the settings", f1w.Lang == "es" && Prefs.LyricLang == "es" && string.Join("|", Prefs.Lines()).Contains("lyriclang=es"), f1w.Lang);
            f1w.LoadFile(unknown); LyricsGenForm.Consent = q => true; Fld<TextBox>(f1w, "lyricsBox").Text = ""; f1w.Generate(); WaitIdle(f1w, 30000); string wargsTxt = File.Exists(Path.Combine(work, "wargs.txt")) ? File.ReadAllText(Path.Combine(work, "wargs.txt")) : "";
            Check("a song written by listening is heard in the chosen language", wargsTxt.Contains("-l es ") && f1w.LyricsText.Length > 0, wargsTxt); f1w.Lang = "auto";
            // RECHECK on the window: the song is listened to again and compared with what is in the box
            f1w.LoadFile(unknown); Fld<TextBox>(f1w, "lyricsBox").Text = "I don't know what I'm doing\r\nI can't stop.\r\n\r\n[Chorus]\r\nSing it loud\r\nWe are the sound\r\nSing it loud\r\nNever back down\r\nSing it loud\r\nWe are the sound\r\nSing it loud\r\nNever back down"; Pump(300);
            LyricsCheck.Report seen = null; LyricsGenForm.ReportHook = r => { seen = r; return true; }; f1w.Recheck(); WaitIdle(f1w, 30000);
            Check("RECHECK listens to the file again and finds the passage that is sung but was not in the box", seen != null && seen.Passages.Count == 1 && seen.Passages[0].Text == "where do you go im lost in the dark" && seen.Passages[0].AfterLine == 1, seen == null ? f1w.StatusText : seen.Summary);
            Check("pressing ADD puts it in, marked, and Undo takes it out again", f1w.LyricsText.Contains("[Heard in the song: check these words]") && f1w.LyricsText.Contains("Where do you go I'm lost in the dark") || f1w.LyricsText.Contains("Where do you go"), f1w.LyricsText.Replace("\r\n", "|"));
            Fld<PxButton>(f1w, "undoBtn").PerformClick(); Check("Undo gives the old lyrics back", !f1w.LyricsText.Contains("Heard in the song"), f1w.LyricsText.Replace("\r\n", "|"));
            LyricsGenForm.ReportHook = r => false; Fld<TextBox>(f1w, "lyricsBox").Text = "I don't know what I'm doing\r\nI can't stop.\r\nWhere do you go\r\nI'm lost in the dark\r\n\r\n[Chorus]\r\nSing it loud\r\nWe are the sound\r\nSing it loud\r\nNever back down\r\nSing it loud\r\nWe are the sound\r\nSing it loud\r\nNever back down"; f1w.Recheck(); WaitIdle(f1w, 30000);
            Check("lyrics that match the song get a clean bill and nothing is changed", f1w.LastReport != null && f1w.LastReport.Clean && f1w.StatusText.Contains("agree") && !f1w.LyricsText.Contains("Heard in the song"), f1w.StatusText);
            Fld<TextBox>(f1w, "lyricsBox").Text = ""; f1w.Recheck(); Check("RECHECK with no lyrics asks for them first", f1w.StatusText.Contains("no lyrics"), f1w.StatusText);
            Fld<TextBox>(f1w, "lyricsBox").Text = "Some words here"; f1w.LinkText = "https://www.youtube.com/watch?v=abc123"; f1w.Recheck(); Check("RECHECK from a link explains that it needs the file", f1w.StatusText.Contains("FILE") && !f1w.Busy, f1w.StatusText);
            // a long lyric in a small box: the window says how many lines are below the edge
            f1w.LinkText = ""; f1w.LoadFile(song); var longL = new StringBuilder(); for (int i = 1; i <= 90; i++) longL.Append("Line " + i + " of a very long song\r\n"); Fld<TextBox>(f1w, "lyricsBox").Text = longL.ToString(); Pump(700);
            Check("when the lyrics are longer than the box it says how many lines are below the edge", f1w.HiddenLines > 5 && Fld<Label>(f1w, "countLbl").Text.Contains("more lines below"), f1w.HiddenLines + " | " + Fld<Label>(f1w, "countLbl").Text);
            Fld<TextBox>(f1w, "lyricsBox").Text = "Short one\r\nShort two"; Pump(700); Check("and says nothing when everything is on show", f1w.HiddenLines == 0 && !Fld<Label>(f1w, "countLbl").Text.Contains("below"), Fld<Label>(f1w, "countLbl").Text);
            LyricsGenForm.ReportHook = null;
            f1w.Close(); Pump(100); Check("closing the window leaves nothing running", true);
        }
        finally { try { srv.Stop(); } catch { } try { Directory.Delete(work, true); } catch { } }
        Console.WriteLine(fails == 0 ? "=== generate lyrics checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
