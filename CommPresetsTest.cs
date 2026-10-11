using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// Tests community presets: the JSON reader, the safety rules, fetching, sharing, using one from the sheet, and (when the website folder is here) the real website.   csc /main:CP ... CommPresetsTest.cs
static class CP
{
    static int fails; static StudioForm sf; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + "   " + d); Console.Out.Flush(); }
    static T F<T>(string n) { return (T)typeof(StudioForm).GetField(n, BF).GetValue(sf); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); Thread.Sleep(15); } }
    static void Paint() { sf.Invalidate(); sf.Update(); Application.DoEvents(); }
    static List<HitReg> Regs() { var l = new List<HitReg>(); foreach (object o in (IEnumerable)typeof(StudioForm).GetField("regs", BF).GetValue(sf)) l.Add((HitReg)o); return l; }
    static void Ev(string name, MouseButtons b, int clicks, int x, int y) { typeof(StudioForm).GetMethod(name, BF | BindingFlags.DeclaredOnly).Invoke(sf, new object[] { new MouseEventArgs(b, clicks, x, y, 0) }); }
    static void Click(HitReg r) { int cx = (int)(r.R.X + r.R.Width / 2), cy = (int)(r.R.Y + r.R.Height / 2); Ev("OnMouseMove", MouseButtons.None, 0, cx, cy); Ev("OnMouseDown", MouseButtons.Left, 1, cx, cy); Ev("OnMouseUp", MouseButtons.Left, 1, cx, cy); Paint(); }

    static string lastBody = "", lastPath = ""; static volatile bool serve = true; static HttpListener lis;
    static void Serve()
    {
        while (serve)
        {
            HttpListenerContext c; try { c = lis.GetContext(); } catch { return; }
            string path = c.Request.Url.PathAndQuery, body = ""; using (var sr = new StreamReader(c.Request.InputStream, Encoding.UTF8)) body = sr.ReadToEnd(); lastBody = body; lastPath = path; string json; int status = 200;
            if (path.StartsWith("/api/presets/share")) { if (body.Contains("RATELIMIT")) { json = "{\"ok\":false,\"err\":\"rate\"}"; status = 429; } else json = "{\"ok\":true,\"id\":\"p0123456789abcd\"}"; }
            else if (path.StartsWith("/api/presets/use/")) json = "{\"ok\":true}";
            else if (path.StartsWith("/api/presets?")) json = "{\"presets\":[" +
                "{\"id\":\"p1\",\"kind\":\"fx:bassboost\",\"name\":\"Deep \\u0038\\u0030\\u0038 \\\"Thump\\\"\",\"author\":\"Ann\",\"data\":\"freq=50;gain=9;q=0.8;harm=20;tight=18;mix=100;level=-4\",\"uses\":12,\"at\":1}," +
                "{\"id\":\"p2\",\"kind\":\"fx:bassboost\",\"name\":\"Evil\",\"author\":\"Bob\",\"data\":\"freq=<script>\",\"uses\":99,\"at\":2}," +
                "{\"id\":\"p3\",\"kind\":\"fx:bassboost\",\"name\":\"Soft low\",\"author\":\"Cy\",\"data\":\"freq=90;gain=3;level=-1\",\"uses\":1,\"at\":3}],\"counts\":{}}";
            else { json = "{}"; status = 404; }
            var b = Encoding.UTF8.GetBytes(json); c.Response.StatusCode = status; c.Response.ContentType = "application/json"; c.Response.OutputStream.Write(b, 0, b.Length); c.Response.Close();
        }
    }

    [STAThread]
    static void Main()
    { Edition.Override = Tier.Fantasy; /* the tests run as the full edition */
        string tmp = Path.Combine(Path.GetTempPath(), "purphex_cptest"); try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); } catch { } Directory.CreateDirectory(tmp); StudioEnv.DataDir = tmp;
        // ---- the JSON reader ----
        var o = MiniJson.Parse("{\"a\":[1,2.5,-3e2,true,false,null,\"x\\ny\\u0041\\\"\"],\"b\":{\"c\":\"d\"}}") as Dictionary<string, object>; var arr = o["a"] as List<object>;
        Check("the JSON reader handles numbers, words, escapes and nesting", arr != null && arr.Count == 7 && (double)arr[2] == -300 && (bool)arr[3] && arr[5] == null && (string)arr[6] == "x\nyA\"" && ((Dictionary<string, object>)o["b"])["c"].Equals("d"), "");
        bool threw = false; try { MiniJson.Parse("{\"a\":[1,2"); } catch (FormatException) { threw = true; } Check("broken JSON is refused cleanly", threw, "");
        // ---- safety rules ----
        Check("a normal plugin preset is safe", CommunityPresets.Safe("fx:bassboost", "freq=50;gain=9;q=0.8;level=-4.5"), "");
        Check("scientific numbers are fine (values the app itself saves)", CommunityPresets.Safe("fx:eq", "b1_g=1E-05;b2_f=2.5e+3"), "");
        Check("words instead of numbers are not safe", !CommunityPresets.Safe("fx:eq", "freq=abc") && !CommunityPresets.Safe("fx:eq", "hello"), "");
        Check("markup and quotes are not safe", !CommunityPresets.Safe("fx:eq", "freq=1;x=<b>") && !CommunityPresets.Safe("fx:eq", "freq=1\"; DROP"), "");
        Check("a strange kind is not safe", !CommunityPresets.Safe("fx:../x", "a=1") && !CommunityPresets.Safe("other", "a=1") && !CommunityPresets.Safe(null, "a=1"), "");
        Check("a chain needs plugin|bypass|settings lines", CommunityPresets.Safe("chain", "eq|0|hp_on=1;hp_f=90\ncomp|1|thr=-20") && !CommunityPresets.Safe("chain", "eq|0|hp_on=1\nnonsense"), "");
        Check("a huge preset is not safe", !CommunityPresets.Safe("fx:eq", new string('a', 4000) + "=1"), "");
        Check("a Bass Lab sound may hold drawn waves and curves", CommunityPresets.Safe("synth", "osca=1;wtc1=0.1,0.2,0.3;lc1=0:0,0.5:1"), "");
        var eqf = Presets.Make("eq", ""); eqf.Set("b1_g", 3.5f); Check("what the app itself saves is always accepted", CommunityPresets.Safe("fx:eq", eqf.Serialize()), eqf.Serialize().Length + " characters");
        var ch = Presets.BuildChain("Melodic Rap"); Check("the built-in vocal chains are accepted too", CommunityPresets.Safe("chain", Presets.ChainText(ch)) && Presets.ChainFromText(Presets.ChainText(ch)).Count == ch.Count, ch.Count + " plugins");

        // ---- against a pretend website ----
        int port = 18000 + new Random().Next(1000); lis = new HttpListener(); lis.Prefixes.Add("http://127.0.0.1:" + port + "/"); lis.Start(); var th = new Thread(Serve) { IsBackground = true }; th.Start(); CommunityPresets.BaseUrl = "http://127.0.0.1:" + port;
        var list = CommunityPresets.Fetch("fx:bassboost", "popular");
        Check("fetching reads the presets and skips the unsafe one", list.Count == 2 && list[0].Name == "Deep 808 \"Thump\"" && list[0].Author == "Ann" && list[0].Uses == 12 && list[1].Name == "Soft low", list.Count + " presets: " + (list.Count > 0 ? list[0].Name : ""));
        Check("it asked for the right kind and sort order", lastPath.Contains("kind=fx%3Abassboost") && lastPath.Contains("sort=popular"), lastPath);
        string err = CommunityPresets.Share("fx:bassboost", "My \"boom\"", "Zed", "freq=60;gain=6"); Check("sharing sends the preset and returns no error", err == null && lastBody.Contains("\"kind\":\"fx:bassboost\"") && lastBody.Contains("\"name\":\"My \\\"boom\\\"\"") && lastBody.Contains("\"author\":\"Zed\"") && lastBody.Contains("freq=60;gain=6"), lastBody);
        err = CommunityPresets.Share("fx:bassboost", "RATELIMIT", "Zed", "freq=60"); Check("a rate limit is explained in plain words", err != null && err.Contains("a lot of presets"), err);
        err = CommunityPresets.Share("fx:bassboost", "Bad", "Zed", "freq=<x>"); Check("an unsafe preset is never even sent", err != null && !lastBody.Contains("Bad"), err);

        // ---- a saved preset is shared automatically (unless Settings has it off) ----
        { Prefs.ShareSaved = true; lastBody = ""; string said = null; CommunityPresets.AutoShare("fx:bassboost", "Auto saved one", "freq=61;gain=6", "Zed", m => said = m); var w0 = DateTime.Now; while (said == null && (DateTime.Now - w0).TotalSeconds < 5) Pump(50);
          Check("saving a preset also shares it with the community", lastBody.Contains("Auto saved one") && lastBody.Contains("freq=61;gain=6") && said != null && said.Contains("shared"), lastBody + " / " + said);
          Prefs.ShareSaved = false; lastBody = ""; CommunityPresets.AutoShare("fx:bassboost", "Not shared", "freq=62;gain=6", "Zed", m => { }); Pump(700); Check("with the Settings switch off nothing is shared", lastBody == "", lastBody);
          Prefs.ShareSaved = true; lastBody = ""; CommunityPresets.AutoShare("fx:bassboost", "Bad one", "freq=<x>", "Zed", m => { }); Pump(500); Check("an unsafe preset is never sent automatically either", lastBody == "", lastBody); }

        // ---- the sheet ----
        var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng; foreach (var m in eng.MasterFx) m.Bypass = true;
        sf = new StudioForm(app); sf.ClientSize = new Size(1500, 800); sf.Show(); Pump(300); Paint();
        var bb = Presets.Make("bassboost", ""); string applied = null;
        sf.GetType().GetMethod("OpenCommunityPresets", BF).Invoke(sf, new object[] { "fx:bassboost", "Bass Boost", (Action<CommPreset>)(p => { applied = p.Data; bb.Deserialize(p.Data); }), (Func<string>)(() => bb.Serialize()), "Bass Boost custom" });
        var t0 = DateTime.Now; while (F<bool>("cpLoading") && (DateTime.Now - t0).TotalSeconds < 10) Pump(50); Pump(100); Paint();
        Check("the sheet opens and lists what the community shared", F<bool>("cpOpen") && F<IList>("cpList").Count == 2, F<IList>("cpList").Count + " listed");
        var sheetRegs = Regs(); var use = sheetRegs.FindAll(r => Math.Abs(r.R.Width - 84) < 0.5f && Math.Abs(r.R.Height - 36) < 0.5f && r.OnDown != null); Check("each preset has a USE button", use.Count == 2, use.Count + " buttons");
        var close = sheetRegs.FindLast(r => Math.Abs(r.R.Width - 34) < 0.5f && Math.Abs(r.R.Height - 34) < 0.5f && r.OnDown != null); Check("the sheet has a close x", close != null, "");
        if (use.Count == 2) { use.Sort((a, b) => a.R.Y.CompareTo(b.R.Y)); Click(use[0]); Pump(100); }
        Check("USE applies that preset's settings to the plugin", applied != null && Math.Abs(bb.Get("freq") - 50) < 0.01 && Math.Abs(bb.Get("gain") - 9) < 0.01 && Math.Abs(bb.Get("level") + 4) < 0.01, applied);
        Check("the sheet closes after using one", !F<bool>("cpOpen"), "");
        Pump(300); Check("the website is told it was used", lastPath.StartsWith("/api/presets/use/p1"), lastPath);
        sf.GetType().GetMethod("OpenCommunityPresets", BF).Invoke(sf, new object[] { "fx:bassboost", "Bass Boost", (Action<CommPreset>)(p => { }), (Func<string>)(() => bb.Serialize()), "x" }); t0 = DateTime.Now; while (F<bool>("cpLoading") && (DateTime.Now - t0).TotalSeconds < 10) Pump(50); Paint();
        foreach (char ch2 in "soft") typeof(StudioForm).GetMethod("CpKey", BF).Invoke(sf, new object[] { ch2 }); Paint(); use = Regs().FindAll(r => Math.Abs(r.R.Width - 84) < 0.5f && Math.Abs(r.R.Height - 36) < 0.5f && r.OnDown != null); Check("typing in the search box narrows the list", use.Count == 1, use.Count + " shown");
        sf.GetType().GetMethod("ProcessCmdKey", BF).Invoke(sf, new object[] { null, Keys.Escape }); Check("Esc closes the sheet", !F<bool>("cpOpen"), "");
        // an unreachable website is explained, not a crash
        CommunityPresets.BaseUrl = "http://127.0.0.1:1"; sf.GetType().GetMethod("OpenCommunityPresets", BF).Invoke(sf, new object[] { "fx:bassboost", "Bass Boost", (Action<CommPreset>)(p => { }), (Func<string>)(() => "a=1"), "x" }); t0 = DateTime.Now; while (F<bool>("cpLoading") && (DateTime.Now - t0).TotalSeconds < 20) Pump(50); Paint();
        Check("when the website cannot be reached the sheet says so", F<string>("cpStatus").Contains("Could not reach"), F<string>("cpStatus")); sf.GetType().GetField("cpOpen", BF).SetValue(sf, false);
        // a chain applies and a chain can be shared as text
        var back = Presets.ChainFromText(Presets.ChainText(ch)); Check("a shared chain rebuilds the same plugins in the same order", back.Count == ch.Count && back[0].Type == ch[0].Type && back[back.Count - 1].Type == ch[ch.Count - 1].Type, "");
        serve = false; try { lis.Stop(); } catch { }

        // ---- the real website, if it is here ----
        string web = @"C:\Users\thoma\purphex-web"; string node = FindNode();
        if (Directory.Exists(web) && node != null)
        {
            int p2 = 19000 + new Random().Next(900); var psi = new ProcessStartInfo(node, "server.js") { WorkingDirectory = web, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }; psi.EnvironmentVariables["PORT"] = p2.ToString(); psi.EnvironmentVariables["DATA_DIR"] = Path.Combine(tmp, "webdata"); psi.EnvironmentVariables["PUBLIC_URL"] = "";
            var pr = Process.Start(psi); pr.OutputDataReceived += (s, e) => { }; pr.ErrorDataReceived += (s, e) => { }; pr.BeginOutputReadLine(); pr.BeginErrorReadLine(); CommunityPresets.BaseUrl = "http://127.0.0.1:" + p2; bool up = false;
            try
            {
                for (int i = 0; i < 80 && !up; i++) { try { using (var wc = new WebClient()) { wc.DownloadString(CommunityPresets.BaseUrl + "/healthz"); up = true; } } catch { Thread.Sleep(150); } }
                Check("the real website started for the test", up, "");
                if (up)
                {
                    string e1 = CommunityPresets.Share("fx:bassboost", "Real test boom", "Tester", bb.Serialize()); Check("the real website accepts what the app shares", e1 == null, e1);
                    var got = CommunityPresets.Fetch("fx:bassboost", "popular"); Check("and gives it back, ready to use", got.Count == 1 && got[0].Name == "Real test boom" && got[0].Author == "Tester", got.Count + " found");
                    var fx2 = Presets.Make("bassboost", ""); if (got.Count == 1) fx2.Deserialize(got[0].Data); Check("with exactly the same settings", Math.Abs(fx2.Get("freq") - bb.Get("freq")) < 0.001 && Math.Abs(fx2.Get("gain") - bb.Get("gain")) < 0.001 && Math.Abs(fx2.Get("level") - bb.Get("level")) < 0.001, "");
                    string e2 = CommunityPresets.Share("chain", "Real test chain", "Tester", Presets.ChainText(ch)); var gc = CommunityPresets.Fetch("chain", "popular"); Check("a whole plugin chain round-trips through the real website", e2 == null && gc.Count == 1 && Presets.ChainFromText(gc[0].Data).Count == ch.Count, e2);
                    var syn = new SynthFx(); string e3 = CommunityPresets.Share("synth", "Real test wobble", "Tester", syn.Serialize()); var gs = CommunityPresets.Fetch("synth", "popular"); Check("a Bass Lab sound round-trips through the real website", e3 == null && gs.Count == 1, e3 + " " + syn.Serialize().Length + " characters");
                }
            }
            finally { try { pr.Kill(); } catch { } }
        }
        else Console.WriteLine("SKIP  the real website is not on this PC");

        sf.Close(); try { Directory.Delete(tmp, true); } catch { } Console.WriteLine(fails == 0 ? "=== community preset checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
    static string FindNode() { foreach (var d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';')) { try { string f = Path.Combine(d.Trim(), "node.exe"); if (File.Exists(f)) return f; } catch { } } return null; }
}
