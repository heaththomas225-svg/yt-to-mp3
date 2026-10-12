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

// TIMES USED: how many conversions have been done with Purphex Music all over the world, for each file type: MP3, MP4, OPUS, WAV, M4A, FLAC and MKV (the quality does not matter, only the type).
// Every finished conversion adds one to the worldwide number on the website; only that number is sent (never who you are, the link or the song), and Settings can turn it off.
// The numbers are kept as whole-number text, so they go into the millions, billions and beyond with no limit: they are shown in full (1,234,567) and short (1.2M).
static class UsageStat
{
    public static string BaseUrl = null;   // tests point this at a small local server
    static string Base { get { return BaseUrl ?? CommunityPresets.BaseUrl; } }
    public static readonly string[] Kinds = { "mp3", "mp4", "opus", "wav", "m4a", "flac", "mkv" };
    static readonly object lk = new object(); static bool loaded, flushing; static readonly Dictionary<string, long> local = new Dictionary<string, long>(), pend = new Dictionary<string, long>();
    static string FileP { get { return Path.Combine(StudioEnv.DataDir, "usage-local.txt"); } }
    public sealed class Totals
    {
        public Dictionary<string, string> Per = new Dictionary<string, string>(); public string Total = "0"; public bool Ok; public string Error = "";
        public string Get(string kind) { string v; return Per.TryGetValue(kind, out v) ? v : "0"; }
    }

    // the type of file a conversion made (the extension decides; the quality does not matter), or null for a file that is not one of the seven
    public static string KindOfExt(string ext)
    {
        switch ((ext ?? "").ToLowerInvariant().TrimStart('.'))
        {
            case "mp3": return "mp3"; case "mp4": case "m4v": return "mp4"; case "opus": case "ogg": return "opus"; case "wav": return "wav"; case "m4a": case "aac": return "m4a"; case "flac": return "flac"; case "mkv": return "mkv"; default: return null;
        }
    }
    // the converter's format list: 0 MP3, 1 WAV, 2 FLAC, 3 M4A, 4 Opus, 5 to 10 MP4 (every video quality), 11 MKV
    public static string KindOfFmt(int fmtIndex) { return fmtIndex == 0 ? "mp3" : fmtIndex == 1 ? "wav" : fmtIndex == 2 ? "flac" : fmtIndex == 3 ? "m4a" : fmtIndex == 4 ? "opus" : fmtIndex == 11 ? "mkv" : "mp4"; }

    static long Get(Dictionary<string, long> d, string k) { long v; return d.TryGetValue(k, out v) ? v : 0; }
    static void Load()
    {
        if (loaded) return; loaded = true; local.Clear(); pend.Clear();
        try
        {
            if (File.Exists(FileP)) foreach (string l in File.ReadAllLines(FileP))
            {
                int i = l.IndexOf('='); if (i < 0) continue; long v; if (!long.TryParse(l.Substring(i + 1), NumberStyles.None, CultureInfo.InvariantCulture, out v)) continue; string k = l.Substring(0, i);
                if (k == "audio") k = "l_mp3"; else if (k == "video") k = "l_mp4"; else if (k == "pendaudio") k = "p_mp3"; else if (k == "pendvideo") k = "p_mp4";   // the first version kept only audio and video
                if (k.StartsWith("l_") && Array.IndexOf(Kinds, k.Substring(2)) >= 0) local[k.Substring(2)] = v; else if (k.StartsWith("p_") && Array.IndexOf(Kinds, k.Substring(2)) >= 0) pend[k.Substring(2)] = v;
            }
        }
        catch { }
    }
    static void Save()
    {
        try { Directory.CreateDirectory(StudioEnv.DataDir); var sb = new StringBuilder(); foreach (var k in Kinds) sb.Append("l_").Append(k).Append('=').Append(Get(local, k)).Append('\n'); foreach (var k in Kinds) sb.Append("p_").Append(k).Append('=').Append(Get(pend, k)).Append('\n'); Win.WriteAtomic(FileP, sb.ToString(), new UTF8Encoding(false)); } catch { }
    }
    public static void Reset() { lock (lk) { loaded = false; local.Clear(); pend.Clear(); flushing = false; } }
    public static long Local(string kind) { lock (lk) { Load(); return Get(local, kind); } }
    public static long LocalTotal { get { lock (lk) { Load(); long t = 0; foreach (var k in Kinds) t += Get(local, k); return t; } } }
    public static long Pending { get { lock (lk) { Load(); long t = 0; foreach (var k in Kinds) t += Get(pend, k); return t; } } }

    // a finished conversion: counted on this PC always, and added to the worldwide number when Settings allows (offline ones are sent later)
    public static void Count(string kind, int n)
    {
        if (n <= 0 || Array.IndexOf(Kinds, kind) < 0) return;
        lock (lk) { Load(); local[kind] = Get(local, kind) + n; if (Prefs.CountUses) pend[kind] = Get(pend, kind) + n; Save(); }
        if (Prefs.CountUses) Flush();
    }
    public static void CountFile(string path) { string k = KindOfExt(Path.GetExtension(path)); if (k != null) Count(k, 1); }
    static void Post(string kind, int n)
    {
        try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }
        var rq = (HttpWebRequest)WebRequest.Create(Base + "/api/uses"); rq.Method = "POST"; rq.UserAgent = "PurphexMusic/" + App.CurrentVersion; rq.Timeout = 10000; rq.ReadWriteTimeout = 10000; rq.ContentType = "application/json";
        var b = Encoding.UTF8.GetBytes("{\"kind\":\"" + kind + "\",\"n\":" + n + "}"); rq.ContentLength = b.Length; using (var s = rq.GetRequestStream()) s.Write(b, 0, b.Length);
        using (var r = (HttpWebResponse)rq.GetResponse()) { }
    }
    // sends what is waiting, in the background; what could not be sent stays for next time
    public static void Flush() { lock (lk) { Load(); if (flushing || Pending <= 0) return; flushing = true; } new Thread(FlushNow) { IsBackground = true }.Start(); }
    public static void FlushNow()
    {
        try
        {
            for (int round = 0; round < 200; round++)
            {
                string kind = null; int n = 0; lock (lk) { foreach (var k in Kinds) { long p = Get(pend, k); if (p > 0) { kind = k; n = (int)Math.Min(50, p); break; } } }
                if (kind == null) return;
                try { Post(kind, n); } catch { return; }   // offline or refused: keep it and try again later
                lock (lk) { pend[kind] = Math.Max(0, Get(pend, kind) - n); Save(); }
            }
        }
        finally { lock (lk) flushing = false; }
    }
    public static Totals Fetch()
    {
        var t = new Totals();
        try
        {
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }
            var rq = (HttpWebRequest)WebRequest.Create(Base + "/api/uses"); rq.UserAgent = "PurphexMusic/" + App.CurrentVersion; rq.Timeout = 10000; rq.ReadWriteTimeout = 10000; string body;
            using (var r = (HttpWebResponse)rq.GetResponse()) using (var sr = new StreamReader(r.GetResponseStream(), Encoding.UTF8)) body = sr.ReadToEnd();
            var d = MiniJson.Parse(body) as Dictionary<string, object>; string tot = Digits(d, "total"); if (tot == null) { t.Error = "The website sent numbers I could not read."; return t; }
            foreach (var k in Kinds) t.Per[k] = Digits(d, k) ?? "0";   // an older site that only knows mp3 and mp4 gives 0 for the rest
            t.Total = tot; t.Ok = true;
        }
        catch (Exception ex) { t.Error = "Could not reach purphexmusic.com (" + ex.Message + ")."; }
        return t;
    }
    // the number as text of digits only (a very long number is never turned into a double)
    static string Digits(Dictionary<string, object> d, string k) { object o; if (d == null || !d.TryGetValue(k, out o) || o == null) return null; string s = o is string ? (string)o : Convert.ToString(o, CultureInfo.InvariantCulture); if (!Regex.IsMatch(s, @"^\d{1,400}$")) return null; string z = s.TrimStart('0'); return z.Length == 0 ? "0" : z; }

    // 1234567 -> "1,234,567" (any length)
    public static string Group(string digits)
    {
        if (digits == null || !Regex.IsMatch(digits, @"^\d+$")) return "0"; digits = digits.TrimStart('0'); if (digits.Length == 0) return "0";
        var sb = new StringBuilder(); int first = digits.Length % 3; if (first > 0) sb.Append(digits, 0, first); for (int i = first; i < digits.Length; i += 3) { if (sb.Length > 0) sb.Append(','); sb.Append(digits, i, 3); }
        return sb.ToString();
    }
    static readonly string[] Units = { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc" };
    // 1234567 -> "1.2M"; past the named units (a trillion trillion trillion...) it is "1.2e45"
    public static string Short(string digits)
    {
        if (digits == null || !Regex.IsMatch(digits, @"^\d+$")) return "0"; digits = digits.TrimStart('0'); if (digits.Length == 0) return "0"; if (digits.Length <= 3) return digits;
        int exp = digits.Length - 1, idx = exp / 3, ip = exp % 3 + 1; string head = digits.Substring(0, ip), dec = digits.Substring(ip, 1); string m = head + (dec != "0" ? "." + dec : "");
        return idx < Units.Length ? m + Units[idx] : digits.Substring(0, 1) + (digits.Substring(1, 1) != "0" ? "." + digits.Substring(1, 1) : "") + "e" + exp;
    }
    // the files a conversion made since the snapshot, each counted as its own type; a conversion with no file found counts as one of the type that was asked for
    public static void CountNew(string dir, HashSet<string> before, int fmtIndex, int fallbackIfNone)
    {
        int found = 0;
        try { foreach (var f in Directory.GetFiles(dir)) { if (before != null && before.Contains(f)) continue; string k = KindOfExt(Path.GetExtension(f)); if (k == null) continue; Count(k, 1); found++; } } catch { }
        if (found == 0 && fallbackIfNone > 0) Count(KindOfFmt(fmtIndex), fallbackIfNone);
    }
}

// one big number that always fits its card: it shrinks until the whole number is inside (no matter how many digits)
sealed class NumberCard : Control
{
    public string Title = "", Value = "0", Sub = "", Raw = "0"; public bool Gold; public float MaxPt = 30f;   // Raw: the digits; Value: the digits in threes
    public NumberCard() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(Parent != null ? Parent.BackColor : BackColor); g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; var rc = new RectangleF(1.5f, 1.5f, Width - 3f, Height - 3f);
        Win.DrawNeonBox(g, rc, Gold); var ink = TM.C(255, 240, 232, 255); var dim = TM.C(255, 198, 182, 232);
        var fl = TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
        using (var f = Win.UiFont(10f, true)) TextRenderer.DrawText(g, Title, f, new Rectangle(6, 6, Width - 12, 20), dim, fl);
        // the whole number at the biggest size that fits (never smaller than 10 points); a number too long even for that is shown short (1.2e29) and the whole number is in the tooltip
        string text = Value, sub = Sub; float pt = MaxPt; Font big = null; bool fits = false;
        try
        {
            for (; pt >= 10f; pt -= 1f) { if (big != null) big.Dispose(); big = Win.UiFont(pt, true); if (TextRenderer.MeasureText(text, big, new Size(10000, 100), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width <= Width - 20) { fits = true; break; } }
            if (!fits) { text = UsageStat.Short(Raw); sub = "hover: full number"; for (pt = MaxPt; pt >= 8f; pt -= 1f) { if (big != null) big.Dispose(); big = Win.UiFont(pt, true); if (TextRenderer.MeasureText(text, big, new Size(10000, 100), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width <= Width - 20) break; } }
            TextRenderer.DrawText(g, text, big, new Rectangle(6, 26, Width - 12, Height - 56), Gold ? Color.FromArgb(255, 255, 206, 110) : ink, fl);
        }
        finally { if (big != null) big.Dispose(); }
        using (var f = Win.UiFont(fits ? 11f : 9f, false)) TextRenderer.DrawText(g, sub, f, new Rectangle(6, Height - 28, Width - 12, 22), dim, fl);
    }
}

// the TIMES USED window: the total, then one card for each file type, then what you made on this PC
sealed class UsageForm : Form
{
    readonly Dictionary<string, NumberCard> cards = new Dictionary<string, NumberCard>(); NumberCard cTotal, cMine; Label status, note; PxButton refresh, close; volatile bool gone;
    public static Func<UsageStat.Totals> FetchHook;   // a test answers here
    public UsageForm()
    {
        Text = "Times used worldwide"; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = true; ShowInTaskbar = true; StartPosition = FormStartPosition.CenterParent; BackColor = TM.C(255, 10, 4, 20); ForeColor = TM.C(255, 240, 232, 255); Font = new Font("Segoe UI", 10f);
        Win.Dark(this); DoubleBuffered = true; ClientSize = new Size(600, 560);
        var head = new Label { Text = "Times used worldwide", Left = 24, Top = 14, Width = 552, Height = 38, Font = new Font("Segoe UI Semibold", 20f), ForeColor = TM.C(255, 240, 232, 255), Tag = "nocaps" };
        var sub = new Label { Text = "Every conversion made with Purphex Music, all over the world, for each file type. It keeps counting with no limit.", Left = 24, Top = 54, Width = 552, Height = 40, ForeColor = TM.C(255, 198, 182, 232), Tag = "nocaps" };
        cTotal = new NumberCard { Left = 24, Top = 100, Width = 552, Height = 104, Title = "ALL CONVERSIONS", Gold = true };
        var all = new List<Control> { head, sub, cTotal };
        string[] names = { "MP3", "MP4", "OPUS", "WAV", "M4A", "FLAC", "MKV" }; int cw = 130, gap = 10;
        for (int i = 0; i < 7; i++) { var c = new NumberCard { Left = 24 + (i % 4) * (cw + gap), Top = 216 + (i / 4) * 108, Width = cw, Height = 98, Title = names[i], MaxPt = 18f }; cards[UsageStat.Kinds[i]] = c; all.Add(c); }
        cMine = new NumberCard { Left = 24 + 3 * (cw + gap), Top = 216 + 108, Width = cw, Height = 98, Title = "ON THIS PC", MaxPt = 18f }; all.Add(cMine);
        status = new Label { Left = 24, Top = 440, Width = 552, Height = 22, ForeColor = TM.C(255, 198, 182, 232), Tag = "nocaps", Text = "Loading the worldwide numbers..." };
        note = new Label { Left = 24, Top = 466, Width = 552, Height = 38, ForeColor = TM.C(255, 198, 182, 232), Tag = "nocaps", Font = new Font("Segoe UI", 9.5f), Text = "Only a number goes up by one after each conversion: nothing about you, the link or the song is sent. Settings, General turns the counting off. Quality does not matter, only the type of file." };
        refresh = new PxButton { Text = "REFRESH", Left = 24, Top = 512, Width = 120, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = TM.C(255, 140, 60, 220), ForeColor = Color.White, Cursor = Cursors.Hand }; refresh.Click += (s, e) => Load();
        close = new PxButton { Text = "CLOSE", Left = 456, Top = 512, Width = 120, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = TM.C(255, 90, 50, 170), ForeColor = Color.White, Cursor = Cursors.Hand }; close.Click += (s, e) => Close();
        all.Add(status); all.Add(note); all.Add(refresh); all.Add(close); Controls.AddRange(all.ToArray()); Win.CapsAll(this);
        UpdateMine(); FormClosed += (s, e) => gone = true; Shown += (s, e) => Load();
    }
    readonly ToolTip tips = new ToolTip();
    void UpdateMine() { Fill(cMine, UsageStat.LocalTotal.ToString(CultureInfo.InvariantCulture)); }
    public string TotalText { get { return cTotal.Value; } }
    public string KindText(string kind) { return cards[kind].Value; }
    public string MineText { get { return cMine.Value; } }
    public string StatusText { get { return status.Text; } }
    public void Load()
    {
        refresh.Enabled = false; status.Text = "Loading the worldwide numbers..."; UsageStat.Flush();
        new Thread(() =>
        {
            var t = FetchHook != null ? FetchHook() : UsageStat.Fetch();
            try { if (!gone && IsHandleCreated) BeginInvoke((Action)(() => { refresh.Enabled = true; UpdateMine(); if (!t.Ok) { status.Text = t.Error; return; } Fill(cTotal, t.Total); foreach (var k in UsageStat.Kinds) Fill(cards[k], t.Get(k)); status.Text = "Updated " + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + ". Press REFRESH to count again."; })); } catch { }
        }) { IsBackground = true }.Start();
    }
    void Fill(NumberCard c, string digits) { c.Raw = digits; c.Value = UsageStat.Group(digits); c.Sub = UsageStat.Short(digits); tips.SetToolTip(c, c.Title + ": " + c.Value); c.Invalidate(); }
}
