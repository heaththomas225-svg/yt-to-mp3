using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

// Community presets: effect settings, plugin chains and Bass Lab sounds that people share, so anyone can use them.
// The app only ever asks the website for plain text (name=number pairs) and checks it again before using it.
class CommPreset { public string Id = "", Kind = "", Name = "", Author = "", Data = ""; public int Uses; }

// A very small JSON reader (the website's answers are simple), so the app needs nothing extra.
static class MiniJson
{
    public static object Parse(string s) { int i = 0; if (s == null || s.Length > 6 * 1024 * 1024) throw new FormatException("too big"); var v = Val(s, ref i, 0); return v; }
    static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }
    static object Val(string s, ref int i, int depth)
    {
        if (depth > 24) throw new FormatException("too deep"); Ws(s, ref i); if (i >= s.Length) throw new FormatException("end"); char c = s[i];
        if (c == '{')
        {
            var d = new Dictionary<string, object>(); i++; Ws(s, ref i); if (i < s.Length && s[i] == '}') { i++; return d; }
            while (true) { Ws(s, ref i); string k = Str(s, ref i); Ws(s, ref i); if (i >= s.Length || s[i++] != ':') throw new FormatException("colon"); d[k] = Val(s, ref i, depth + 1); Ws(s, ref i); if (i < s.Length && s[i] == ',') { i++; continue; } if (i < s.Length && s[i] == '}') { i++; return d; } throw new FormatException("object"); }
        }
        if (c == '[')
        {
            var l = new List<object>(); i++; Ws(s, ref i); if (i < s.Length && s[i] == ']') { i++; return l; }
            while (true) { l.Add(Val(s, ref i, depth + 1)); Ws(s, ref i); if (i < s.Length && s[i] == ',') { i++; continue; } if (i < s.Length && s[i] == ']') { i++; return l; } throw new FormatException("array"); }
        }
        if (c == '"') return Str(s, ref i);
        if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
        if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
        if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
        int st = i; while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++; double n; if (i == st || !double.TryParse(s.Substring(st, i - st), NumberStyles.Float, CultureInfo.InvariantCulture, out n)) throw new FormatException("value"); return n;
    }
    static string Str(string s, ref int i)
    {
        if (i >= s.Length || s[i] != '"') throw new FormatException("string"); i++; var sb = new StringBuilder();
        while (i < s.Length)
        {
            char c = s[i++]; if (c == '"') return sb.ToString();
            if (c != '\\') { sb.Append(c); continue; }
            if (i >= s.Length) break; char e = s[i++];
            switch (e) { case 'n': sb.Append('\n'); break; case 'r': sb.Append('\r'); break; case 't': sb.Append('\t'); break; case 'b': sb.Append('\b'); break; case 'f': sb.Append('\f'); break; case 'u': { int cp; if (i + 4 > s.Length || !int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out cp)) throw new FormatException("escape"); sb.Append((char)cp); i += 4; break; } default: sb.Append(e); break; }
        }
        throw new FormatException("unterminated");
    }
}

static class CommunityPresets
{
    public static string BaseUrl = "https://purphexmusic.com";
    static readonly Regex Safe0 = new Regex("^[A-Za-z0-9_.;=,:|\\-+ \\n]*$"), Kind0 = new Regex("^(fx:[a-z0-9_]{1,24}|chain|synth)$"), Key0 = new Regex("^[a-z0-9_]{1,24}$"), Num0 = new Regex("^-?[0-9]{1,12}(\\.[0-9]{1,12})?([eE][+-]?[0-9]{1,3})?$");

    // the same rules the website applies: only name=number pairs (a chain is lines of  plugin|bypass|pairs)
    public static bool Safe(string kind, string data)
    {
        if (kind == null || data == null || !Kind0.IsMatch(kind)) return false; data = data.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
        int max = kind.StartsWith("fx:") ? 3000 : (kind == "chain" ? 14000 : 30000); if (data.Length == 0 || data.Length > max || !Safe0.IsMatch(data)) return false;
        if (kind == "chain") { var lines = data.Split('\n'); if (lines.Length > 24) return false; foreach (var ln in lines) { var m = Regex.Match(ln, "^([a-z0-9_]{1,24})\\|([01])\\|(.*)$"); if (!m.Success) return false; if (m.Groups[3].Value.Length > 0 && !Pairs(m.Groups[3].Value, false)) return false; } return true; }
        return Pairs(kind == "synth" ? data.Replace("\n", "") : data, kind == "synth");
    }
    static bool Pairs(string data, bool anyValue)
    {
        var parts = data.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries); if (parts.Length == 0 || parts.Length > 400) return false;
        foreach (var p in parts) { int i = p.IndexOf('='); if (i < 1 || !Key0.IsMatch(p.Substring(0, i))) return false; string v = p.Substring(i + 1); if (anyValue ? v.Length > 6000 : !Num0.IsMatch(v)) return false; }
        return true;
    }

    static string Http(string method, string path, string body)
    {
        var rq = (HttpWebRequest)WebRequest.Create(BaseUrl + path); rq.Method = method; rq.UserAgent = "PurphexMusic"; rq.Timeout = 12000; rq.ReadWriteTimeout = 12000;
        if (body != null) { var b = Encoding.UTF8.GetBytes(body); rq.ContentType = "application/json"; rq.ContentLength = b.Length; using (var s = rq.GetRequestStream()) s.Write(b, 0, b.Length); }
        try { using (var r = (HttpWebResponse)rq.GetResponse()) using (var sr = new StreamReader(r.GetResponseStream(), Encoding.UTF8)) return sr.ReadToEnd(); }
        catch (WebException ex) { if (ex.Response != null) { using (var sr = new StreamReader(ex.Response.GetResponseStream(), Encoding.UTF8)) return sr.ReadToEnd(); } throw; }
    }
    static string Q(string s) { return Uri.EscapeDataString(s ?? ""); }
    static string J(string s) { var sb = new StringBuilder("\""); foreach (char c in s ?? "") { if (c == '"' || c == '\\') sb.Append('\\').Append(c); else if (c == '\n') sb.Append("\\n"); else if (c < 32) sb.Append(' '); else sb.Append(c); } return sb.Append('"').ToString(); }
    static string S(Dictionary<string, object> d, string k) { object v; return d.TryGetValue(k, out v) && v is string ? (string)v : ""; }

    // the presets shared for one kind (blocks: call it from a background thread)
    public static List<CommPreset> Fetch(string kind, string sort)
    {
        var o = MiniJson.Parse(Http("GET", "/api/presets?limit=200&kind=" + Q(kind) + "&sort=" + Q(sort), null)) as Dictionary<string, object>; var res = new List<CommPreset>();
        object arr; if (o == null || !o.TryGetValue("presets", out arr) || !(arr is List<object>)) throw new FormatException("answer");
        foreach (var it in (List<object>)arr)
        {
            var d = it as Dictionary<string, object>; if (d == null) continue; var p = new CommPreset { Id = S(d, "id"), Kind = S(d, "kind"), Name = S(d, "name"), Author = S(d, "author"), Data = S(d, "data") };
            object u; if (d.TryGetValue("uses", out u) && u is double) p.Uses = (int)Math.Max(0, Math.Min(int.MaxValue, (double)u));
            if (p.Id.Length == 0 || p.Name.Length == 0 || p.Kind != kind || !Safe(p.Kind, p.Data)) continue; res.Add(p);
        }
        return res;
    }
    // returns null when it worked, otherwise a sentence for the person
    public static string Share(string kind, string name, string author, string data)
    {
        if (!Safe(kind, data)) return "That cannot be shared: it has to be plain settings.";
        try
        {
            var o = MiniJson.Parse(Http("POST", "/api/presets/share", "{\"kind\":" + J(kind) + ",\"name\":" + J(name) + ",\"author\":" + J(author) + ",\"data\":" + J(data) + "}")) as Dictionary<string, object>;
            object ok; if (o != null && o.TryGetValue("ok", out ok) && ok is bool && (bool)ok) return null;
            string err = o == null ? "" : S(o, "err");
            if (err == "rate") return "You have shared a lot of presets in the last hour. Try again a bit later.";
            if (err == "name") return "Give it a name with at least 2 letters.";
            return "The community could not take that preset.";
        }
        catch { return "Could not reach the Purphex community. Check your internet connection."; }
    }
    // Every preset you save is also given to the community (unless Settings has it turned off): saved in the background, and any trouble is quiet.
    // done gets one sentence for the screen when it worked, or null when it could not be shared right now.
    public static void AutoShare(string kind, string name, string data, string author, Action<string> done)
    {
        if (!Prefs.ShareSaved || !Safe(kind, data)) return; if (string.IsNullOrWhiteSpace(author)) author = "Producer";
        ThreadPool.QueueUserWorkItem(o => { string err = Share(kind, name, author, data); if (done != null) { try { done(err == null ? "Saved, and shared with the community so everyone can use it." : (err.Contains("a lot of presets") ? "Saved. The community share limit for this hour is used up, so it was not shared." : null)); } catch { } } });
    }
    public static void Tell(string what, string id) { ThreadPool.QueueUserWorkItem(o => { try { Http("POST", "/api/presets/" + what + "/" + Q(id), "{}"); } catch { } }); }
}

partial class StudioForm
{
    bool cpOpen, cpLoading; string cpKind = "", cpTitle = "", cpSort = "popular", cpQuery = "", cpStatus = "", cpDefName = ""; List<CommPreset> cpList = new List<CommPreset>(); float cpScroll; int cpGen;
    Action<CommPreset> cpApply; Func<string> cpData;

    // one sheet for every kind of preset: what to ask the community for, what to do with the one you pick, and where your own settings come from
    internal void OpenCommunityPresets(string kind, string title, Action<CommPreset> apply, Func<string> getData, string defName)
    {
        cpOpen = true; cpKind = kind; cpTitle = title; cpApply = apply; cpData = getData; cpDefName = defName; cpQuery = ""; cpScroll = 0; cpSort = "popular"; stylesOpen = false; autoOpen = false; gainOpen = false; pickOpen = false; browserOpen = false; ntOpen = false; busOpen = false; CpFetch();
    }
    void CpFetch()
    {
        int gen = ++cpGen; cpLoading = true; cpStatus = ""; string kind = cpKind, sort = cpSort;
        ThreadPool.QueueUserWorkItem(o =>
        {
            List<CommPreset> l = null; string err = null;
            try { l = CommunityPresets.Fetch(kind, sort); } catch { err = "Could not reach the Purphex community. Check your internet connection and try again."; }
            try { BeginInvoke(new Action(() => { if (gen != cpGen) return; cpLoading = false; cpList = l ?? new List<CommPreset>(); cpStatus = err ?? ""; cpScroll = 0; Invalidate(); })); } catch { }
        });
    }
    void CpShare()
    {
        string data = null; try { data = cpData == null ? null : cpData(); } catch { }
        if (string.IsNullOrEmpty(data) || !CommunityPresets.Safe(cpKind, data)) { Toast("There is nothing to share yet. Set the sound up first."); return; }
        string nm = Ask("Name your preset (everyone can see and use it):", cpDefName); if (nm == null) return;
        string author = ProjectLibrary.ProfileName; if (string.IsNullOrWhiteSpace(author)) author = "Producer"; string kind = cpKind; Toast("Sharing...");
        ThreadPool.QueueUserWorkItem(o =>
        {
            string err = CommunityPresets.Share(kind, nm, author, data);
            try { BeginInvoke(new Action(() => { if (err == null) { Toast("Shared! Everyone can find \"" + nm + "\" in Community presets now."); if (cpOpen && cpKind == kind) { cpSort = "new"; CpFetch(); } } else Toast(err); })); } catch { }
        });
    }
    void CpUse(CommPreset p)
    {
        if (!CommunityPresets.Safe(p.Kind, p.Data)) { Toast("That preset is not valid."); return; }
        try { if (cpApply != null) cpApply(p); } catch (Exception ex) { Toast("Could not use that preset: " + ex.Message); return; }
        CommunityPresets.Tell("use", p.Id); cpOpen = false; Toast("Using \"" + p.Name + "\" by " + p.Author + ". Ctrl+Z undoes it.");
    }
    void CpKey(char c)
    {
        if (c == '\b') { if (cpQuery.Length > 0) cpQuery = cpQuery.Substring(0, cpQuery.Length - 1); } else if (!char.IsControl(c) && cpQuery.Length < 30) cpQuery += c; cpScroll = 0;
    }
    void PaintCommPresets(Graphics g)
    {
        if (!cpOpen) return; float W = ClientSize.Width, H = ClientSize.Height; var all = Rc(0, 0, W, H);
        using (var b = new SolidBrush(Color.FromArgb(165, 3, 1, 10))) g.FillRectangle(b, all); Reg(all, () => cpOpen = false);
        float sw = Math.Min(720, W - 24), sh = Math.Min(H - 24, 600); var sheet = PopMove("comm", Rc((W - sw) / 2, (H - sh) / 2, sw, sh), 54, 70);
        FillRR(g, Color.FromArgb(252, 16, 10, 34), sheet, 20); Glow(g, NeonViolet, sheet, 20, 0.8f, 4); StrokeRR(g, Color.FromArgb(180, NeonViolet), sheet, 20, 1.5f); Reg(sheet, () => { });
        Txt(g, "COMMUNITY PRESETS", 18, Ink, Rc(sheet.X + 22, sheet.Y + 14, sheet.Width - 90, 28), StringAlignment.Near, true);
        Txt(g, cpTitle + "  -  made and shared by other producers. Click USE to try one.", 12, DimC, Rc(sheet.X + 22, sheet.Y + 44, sheet.Width - 90, 20), StringAlignment.Near, false);
        var close = Rc(sheet.Right - 50, sheet.Y + 14, 34, 34); FillRR(g, Hot(close) ? Color.FromArgb(200, 150, 50, 110) : Color.FromArgb(120, 70, 30, 90), close, 17); Txt(g, "X", 17, Ink, close, StringAlignment.Center, true); Reg(close, () => cpOpen = false);
        // sort tabs and search
        float y = sheet.Y + 74; Btn(g, Rc(sheet.X + 22, y, 104, 32), "POPULAR", cpSort == "popular" ? 4 : 0, () => { if (cpSort != "popular") { cpSort = "popular"; CpFetch(); } }, 12);
        Btn(g, Rc(sheet.X + 132, y, 96, 32), "NEWEST", cpSort == "new" ? 4 : 0, () => { if (cpSort != "new") { cpSort = "new"; CpFetch(); } }, 12);
        var sb = Rc(sheet.X + 240, y, sheet.Right - sheet.X - 240 - 22, 32); FillRR(g, Color.FromArgb(235, 14, 7, 32), sb, 8); StrokeRR(g, Color.FromArgb(150, NeonViolet), sb, 8, 1);
        if (cpQuery.Length > 0) Btn(g, Rc(sb.Right - 30, sb.Y + 4, 24, 24), "x", 3, () => { cpQuery = ""; cpScroll = 0; }, 12);
        double tm = Environment.TickCount / 1000.0; Txt(g, cpQuery.Length == 0 ? (((int)(tm * 2) % 2 == 0 ? "|" : " ") + "  Type to search by name or producer") : cpQuery + ((int)(tm * 2) % 2 == 0 ? "|" : " "), 13, cpQuery.Length == 0 ? DimC : Ink, Rc(sb.X + 12, sb.Y, sb.Width - 44, sb.Height), StringAlignment.Near, false);
        // the list
        var shown = new List<CommPreset>(); foreach (var p in cpList) if (cpQuery.Length == 0 || (p.Name + " " + p.Author).IndexOf(cpQuery, StringComparison.OrdinalIgnoreCase) >= 0) shown.Add(p);
        const float rowH = 66; float top = y + 44, bottom = sheet.Bottom - 74; var area = Rc(sheet.X + 10, top, sheet.Width - 20, bottom - top); float total = shown.Count * rowH; cpScroll = Math.Max(0, Math.Min(Math.Max(0, total - area.Height), cpScroll)); float vsw = total > area.Height ? 54 : 0;
        regs.Add(new HitReg { R = area, OnWheel = d => cpScroll = Math.Max(0, Math.Min(Math.Max(0, total - area.Height), cpScroll - d * 50)) });
        if (cpLoading) Txt(g, "LOADING THE COMMUNITY...", 14, DimC, Rc(area.X + 14, area.Y + 14, area.Width - 28, 26), StringAlignment.Near, false);
        else if (cpStatus.Length > 0) { Txt(g, cpStatus, 13, Color.FromArgb(255, 150, 150), Rc(area.X + 14, area.Y + 14, area.Width - 28, 40), StringAlignment.Near, false); Btn(g, Rc(area.X + 14, area.Y + 58, 120, 32), "TRY AGAIN", 1, () => CpFetch(), 12); }
        else if (shown.Count == 0) Txt(g, cpQuery.Length > 0 ? "Nothing matches that search." : "Nobody has shared one for this yet. Be the first: set the sound up and press SHARE below.", 13, DimC, Rc(area.X + 14, area.Y + 14, area.Width - 28, 44), StringAlignment.Near, false);
        var gs = g.Save(); g.SetClip(area, System.Drawing.Drawing2D.CombineMode.Intersect);
        for (int i = 0; i < shown.Count; i++)
        {
            var p = shown[i]; var row = Rc(area.X + 2, area.Y + i * rowH - cpScroll, area.Width - 4 - vsw, rowH - 6); if (row.Bottom < area.Y || row.Y > area.Bottom) continue;
            bool hot = Hot(RectangleF.Intersect(row, area)) && area.Contains(MousePos); FillRR(g, hot ? Color.FromArgb(200, 70, 40, 130) : Color.FromArgb(150, 26, 15, 52), row, 12); StrokeRR(g, Color.FromArgb(hot ? 180 : 80, NeonViolet), row, 12, 1);
            float tw = row.Width - 200; Txt(g, p.Name, 15, Ink, Rc(row.X + 16, row.Y + 8, tw, 24), StringAlignment.Near, true);
            Txt(g, "BY " + p.Author + "     " + p.Uses + (p.Uses == 1 ? " use" : " uses"), 12, DimC, Rc(row.X + 16, row.Y + 33, tw, 20), StringAlignment.Near, false);
            var pp = p; var useR = Rc(row.Right - 96, row.Y + 11, 84, 36); var repR = Rc(row.Right - 176, row.Y + 17, 70, 24);
            var ua = RectangleF.Intersect(useR, area); if (!ua.IsEmpty) Btn(g, useR, "USE", 2, () => CpUse(pp), 13);
            var ra = RectangleF.Intersect(repR, area); if (!ra.IsEmpty) Btn(g, repR, "Report", 0, () => { if (MessageBox.Show(this, "Report \"" + pp.Name + "\" as spam or something that should not be here?\n\nPresets that are reported by several people are hidden.", "Report preset", MessageBoxButtons.YesNo) == DialogResult.Yes) { CommunityPresets.Tell("report", pp.Id); Toast("Thanks, reported."); } }, 11);
        }
        g.Restore(gs); VScroll(g, Rc(area.Right - 46, area.Y, 42, area.Height), total, area.Height, () => cpScroll, v => cpScroll = v);
        Btn(g, Rc(sheet.X + 22, sheet.Bottom - 58, 330, 40), "SHARE MY CURRENT SETTINGS", 1, () => CpShare(), 13);
        Txt(g, "Shared presets are public. Only share sounds you made.", 11, DimC, Rc(sheet.X + 366, sheet.Bottom - 58, sheet.Right - sheet.X - 366 - 22, 40), StringAlignment.Far, false);
    }
}
