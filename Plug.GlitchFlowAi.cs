using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

// ======================================================================
//  GlitchFlow AI GEN. It only uses a real AI provider when one is set up: the user's own Anthropic API key (kept encrypted for this
//  Windows user with DPAPI, never sent anywhere but api.anthropic.com). The request runs on a background thread, never on the audio
//  thread. The answer is checked before anything changes: unknown settings are dropped, numbers are kept inside each setting's range,
//  choices must be one of the plugin's choices. Without a key it says so and changes nothing.
//  QuickMatch is NOT AI: an offline keyword reader, labelled as such in the window.
// ======================================================================
static class GlitchAi
{
    public const string Model = "claude-sonnet-5-5";
    static string KeyPath { get { return Path.Combine(StudioEnv.DataDir, "glitchflow-ai.key"); } }
    public static Func<string, string> SendHook;   // tests: replaces the network call (prompt -> raw reply)

    public static bool Configured { get { return LoadKey().Length > 0 || SendHook != null; } }
    public static string LoadKey()
    {
        try { if (!File.Exists(KeyPath)) return ""; var raw = ProtectedData.Unprotect(File.ReadAllBytes(KeyPath), null, DataProtectionScope.CurrentUser); return Encoding.UTF8.GetString(raw).Trim(); } catch { return ""; }
    }
    public static void SaveKey(string key)
    {
        key = (key ?? "").Trim(); Directory.CreateDirectory(StudioEnv.DataDir);
        if (key.Length == 0) { try { File.Delete(KeyPath); } catch { } return; }
        File.WriteAllBytes(KeyPath, ProtectedData.Protect(Encoding.UTF8.GetBytes(key), null, DataProtectionScope.CurrentUser));
    }

    // what the model is told: every setting with its range or choices
    public static string Describe(Fx f)
    {
        var sb = new StringBuilder();
        foreach (var p in f.P)
        {
            if (p.IsChoice) sb.Append(p.Id + " (" + p.Name + "): one of [" + string.Join(", ", p.Choices) + "]\n");
            else sb.Append(p.Id + " (" + p.Name + "): number " + p.Min.ToString(CultureInfo.InvariantCulture) + " to " + p.Max.ToString(CultureInfo.InvariantCulture) + " " + p.Unit + "\n");
        }
        return sb.ToString();
    }
    static string Esc(string s) { var sb = new StringBuilder(); foreach (char c in s) { if (c == '"' || c == '\\') sb.Append('\\').Append(c); else if (c == '\n') sb.Append("\\n"); else if (c < 32) sb.Append(' '); else sb.Append(c); } return sb.ToString(); }

    // ask the provider (background thread): done(reply text or null, error or null) is called on that thread
    public static void Generate(Fx f, string prompt, bool variation, Action<string, string> done)
    {
        string desc = Describe(f);
        string system = "You set the parameters of Purphex GlitchFlow, a stutter / beat-repeat / glitch audio effect in a DAW. Repeat rate = how fast the captured slice repeats (rsync note value, or rate in ms when rsync is Free). Refresh rate = how often a new slice is captured (fsync, or refresh in ms). Modules (lp, hp, tel, gate, drv, wid, mod, dly, rev) are only heard when their _on setting is On. Reply with ONLY a JSON object: {\"summary\": \"one short sentence\", \"params\": {\"id\": value, ...}}. Use only these ids; numbers inside the ranges; choices exactly as written:\n" + desc;
        string user = prompt + (variation ? "\n(Give a different take from last time: same idea, different settings.)" : "");
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                if (SendHook != null) { done(SendHook(user), null); return; }
                string key = LoadKey(); if (key.Length == 0) { done(null, "no key"); return; }
                string body = "{\"model\":\"" + Model + "\",\"max_tokens\":900,\"temperature\":" + (variation ? "1" : "0.6") + ",\"system\":\"" + Esc(system) + "\",\"messages\":[{\"role\":\"user\",\"content\":\"" + Esc(user) + "\"}]}";
                var req = (HttpWebRequest)WebRequest.Create("https://api.anthropic.com/v1/messages"); req.Method = "POST"; req.ContentType = "application/json"; req.Timeout = 45000; req.ReadWriteTimeout = 45000;
                req.Headers["x-api-key"] = key; req.Headers["anthropic-version"] = "2023-06-01";
                var bytes = Encoding.UTF8.GetBytes(body); using (var s = req.GetRequestStream()) s.Write(bytes, 0, bytes.Length);
                string resp; using (var r = (HttpWebResponse)req.GetResponse()) using (var rd = new StreamReader(r.GetResponseStream(), Encoding.UTF8)) resp = rd.ReadToEnd();
                var m = Regex.Match(resp, "\"text\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\""); if (!m.Success) { done(null, "the AI answered in a way GlitchFlow could not read"); return; }
                done(Regex.Unescape(m.Groups[1].Value), null);
            }
            catch (WebException ex)
            {
                string why = ex.Message; try { var hr = ex.Response as HttpWebResponse; if (hr != null) { int code = (int)hr.StatusCode; why = code == 401 ? "the API key was refused (401): check it in SETUP" : code == 429 ? "too many requests or no credit left (429)" : "the AI service answered " + code; } } catch { }
                done(null, why);
            }
            catch (Exception ex) { done(null, ex.Message); }
        });
    }

    public class Checked { public string Summary = ""; public Dictionary<string, float> Values = new Dictionary<string, float>(); public List<string> Notes = new List<string>(); }
    // read and check a reply: only real settings, numbers kept in range, choices matched by name or number
    public static Checked Validate(Fx f, string reply)
    {
        var c = new Checked(); if (string.IsNullOrEmpty(reply)) { c.Notes.Add("empty reply"); return c; }
        int a = reply.IndexOf('{'), b = reply.LastIndexOf('}'); if (a < 0 || b <= a) { c.Notes.Add("no settings in the reply"); return c; }
        string json = reply.Substring(a, b - a + 1);
        var sm = Regex.Match(json, "\"summary\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\""); if (sm.Success) c.Summary = Regex.Unescape(sm.Groups[1].Value);
        int pi = json.IndexOf("\"params\""); string body = pi >= 0 ? json.Substring(pi + 8) : json;
        foreach (Match m in Regex.Matches(body, "\"([a-z_]+)\"\\s*:\\s*(\"([^\"]*)\"|(-?[0-9]+(?:\\.[0-9]+)?)|true|false)"))
        {
            string id = m.Groups[1].Value; if (id == "summary" || id == "params") continue; var p = f.Find(id);
            if (p == null) { c.Notes.Add("ignored unknown setting " + id); continue; }
            float v;
            if (m.Groups[4].Success) v = float.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture);
            else if (m.Groups[3].Success)
            {
                string t = m.Groups[3].Value.Trim(); int found = -1;
                if (p.IsChoice) for (int k = 0; k < p.Choices.Length; k++) if (string.Equals(p.Choices[k], t, StringComparison.OrdinalIgnoreCase)) found = k;
                if (p.IsChoice && found < 0) for (int k = 0; k < p.Choices.Length; k++) if (p.Choices[k].IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0 || t.IndexOf(p.Choices[k], StringComparison.OrdinalIgnoreCase) >= 0) { found = k; break; }
                if (found < 0 && float.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) found = (int)v;
                if (found < 0) { c.Notes.Add("ignored " + id + " = \"" + t + "\" (not one of its choices)"); continue; }
                v = found;
            }
            else v = m.Value.EndsWith("true") ? 1 : 0;
            float cl = Math.Max(p.Min, Math.Min(p.Max, p.IsChoice ? (float)Math.Round(v) : v)); if (Math.Abs(cl - v) > 1e-4f) c.Notes.Add(id + " kept inside its range (" + v.ToString("0.##", CultureInfo.InvariantCulture) + " -> " + cl.ToString("0.##", CultureInfo.InvariantCulture) + ")");
            c.Values[id] = cl;
        }
        if (c.Values.Count == 0) c.Notes.Add("the reply had no settings GlitchFlow could use");
        return c;
    }

    // QUICK MATCH (offline keywords, not AI): a fixed list of words that pick settings
    public static Checked QuickMatch(Fx f, string prompt)
    {
        var c = new Checked(); string p = " " + (prompt ?? "").ToLowerInvariant() + " "; Action<string, float> set = (id, v) => { if (f.Find(id) != null) c.Values[id] = v; };
        Func<string, bool> has = w => { foreach (var x in w.Split('|')) if (p.Contains(x)) return true; return false; };
        string[][] rate = { new[] { "1/64", "18" }, new[] { "1/32", "16" }, new[] { "1/16", "14" }, new[] { "1/8", "11" }, new[] { "1/4", "8" }, new[] { "1/2", "5" } };
        foreach (var rr in rate) if (p.Contains(rr[0])) { set("rsync", float.Parse(rr[1])); c.Notes.Add("repeat " + rr[0]); break; }
        if (has("triplet")) { set("rsync", 15); c.Notes.Add("triplet repeats"); }
        if (has("roll|fill")) { set("pattern", 2); set("shape", 0); c.Notes.Add("fill on the last beat"); }
        if (has("build|riser|rising")) { set("pattern", 5); set("fsync", 2); c.Notes.Add("build-up"); }
        if (has("random|chaos|chaotic")) { set("pattern", 4); c.Notes.Add("random pattern"); }
        if (has("subtle|gentle|light")) { set("mix", 45); set("intensity", 50); c.Notes.Add("subtle"); }
        if (has("freeze|hold|frozen")) { set("freeze", 1); c.Notes.Add("freeze"); }
        if (has("reverse|backwards")) { set("reverse", 1); c.Notes.Add("reversed repeats"); }
        if (has("pitch down|tape stop|slow down")) { set("pstep", -2); c.Notes.Add("pitch steps down"); }
        if (has("pitch up|rocket")) { set("pstep", 1); c.Notes.Add("pitch steps up"); }
        if (has("telephone|phone|radio")) { set("tel_on", 1); c.Notes.Add("telephone"); }
        if (has("dark|muffled|filter")) { set("lp_on", 1); set("lp_cut", 1500); c.Notes.Add("low-pass"); }
        if (has("thin|high-pass|highpass")) { set("hp_on", 1); set("hp_cut", 500); c.Notes.Add("high-pass"); }
        if (has("distort|dirty|crush|aggressive|hard")) { set("drv_on", 1); set("drv_amt", 55); if (has("crush|8 bit|8-bit")) set("drv_mode", 2); c.Notes.Add("drive"); }
        if (has("gate|pump|sidechain")) { set("gate_on", 1); c.Notes.Add("rhythm gate"); }
        if (has("wide|stereo")) { set("wid_on", 1); set("width", 170); c.Notes.Add("wide"); }
        if (has("wobble|vibrato|warble")) { set("mod_on", 1); set("mod_tgt", 0); c.Notes.Add("pitch wobble"); }
        if (has("echo|delay")) { set("dly_on", 1); c.Notes.Add("delay"); }
        if (has("reverb|space|wash|big")) { set("rev_on", 1); c.Notes.Add("reverb"); }
        c.Summary = c.Values.Count == 0 ? "No keyword matched (try words like 1/16, roll, build, freeze, reverse, telephone, dark, delay, reverb)." : "Quick match (keywords, not AI): " + string.Join(", ", c.Notes.ToArray());
        return c;
    }
}
