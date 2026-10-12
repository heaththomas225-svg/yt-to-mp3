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

// TIMES USED: how many MP3 (audio) and MP4 (video) conversions have been done with Purphex Music all over the world.
// Every finished conversion adds one to the worldwide number on the website; only that number is sent (never who you are, the link or the song), and Settings can turn it off.
// The numbers are kept as whole-number text, so they go into the millions, billions and beyond with no limit: they are shown in full (1,234,567) and short (1.2M).
static class UsageStat
{
    public static string BaseUrl = null;   // tests point this at a small local server
    static string Base { get { return BaseUrl ?? CommunityPresets.BaseUrl; } }
    static readonly object lk = new object(); static bool loaded; static long localA, localV, pendA, pendV; static bool flushing;
    static string FileP { get { return Path.Combine(StudioEnv.DataDir, "usage-local.txt"); } }
    public sealed class Totals { public string Mp3 = "0", Mp4 = "0", Total = "0"; public bool Ok; public string Error = ""; }
    public static readonly string[] VideoExts = { ".mp4", ".mkv", ".webm", ".mov", ".m4v", ".avi" };
    public static readonly string[] AudioExts = { ".mp3", ".wav", ".flac", ".m4a", ".opus", ".ogg", ".aac" };

    static void Load()
    {
        if (loaded) return; loaded = true;
        try { if (File.Exists(FileP)) foreach (string l in File.ReadAllLines(FileP)) { int i = l.IndexOf('='); if (i < 0) continue; long v; if (!long.TryParse(l.Substring(i + 1), NumberStyles.None, CultureInfo.InvariantCulture, out v)) continue; string k = l.Substring(0, i); if (k == "audio") localA = v; else if (k == "video") localV = v; else if (k == "pendaudio") pendA = v; else if (k == "pendvideo") pendV = v; } } catch { }
    }
    static void Save() { try { Directory.CreateDirectory(StudioEnv.DataDir); Win.WriteAtomic(FileP, "audio=" + localA + "\nvideo=" + localV + "\npendaudio=" + pendA + "\npendvideo=" + pendV + "\n", new UTF8Encoding(false)); } catch { } }
    public static void Reset() { lock (lk) { loaded = false; localA = localV = pendA = pendV = 0; flushing = false; } }
    public static long LocalAudio { get { lock (lk) { Load(); return localA; } } }
    public static long LocalVideo { get { lock (lk) { Load(); return localV; } } }
    public static long Pending { get { lock (lk) { Load(); return pendA + pendV; } } }

    // finished conversions: counted on this PC always, and added to the worldwide number when Settings allows (offline ones are sent later)
    public static void Count(int audio, int video)
    {
        if (audio <= 0 && video <= 0) return;
        lock (lk) { Load(); localA += Math.Max(0, audio); localV += Math.Max(0, video); if (Prefs.CountUses) { pendA += Math.Max(0, audio); pendV += Math.Max(0, video); } Save(); }
        if (Prefs.CountUses) Flush();
    }
    static string Post(string kind, int n)
    {
        try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }
        var rq = (HttpWebRequest)WebRequest.Create(Base + "/api/uses"); rq.Method = "POST"; rq.UserAgent = "PurphexMusic/" + App.CurrentVersion; rq.Timeout = 10000; rq.ReadWriteTimeout = 10000; rq.ContentType = "application/json";
        var b = Encoding.UTF8.GetBytes("{\"kind\":\"" + kind + "\",\"n\":" + n + "}"); rq.ContentLength = b.Length; using (var s = rq.GetRequestStream()) s.Write(b, 0, b.Length);
        using (var r = (HttpWebResponse)rq.GetResponse()) using (var sr = new StreamReader(r.GetResponseStream(), Encoding.UTF8)) return sr.ReadToEnd();
    }
    // sends what is waiting, in the background; what could not be sent stays for next time
    public static void Flush() { lock (lk) { Load(); if (flushing || (pendA <= 0 && pendV <= 0)) return; flushing = true; } new Thread(FlushNow) { IsBackground = true }.Start(); }
    public static void FlushNow()
    {
        try
        {
            for (int round = 0; round < 40; round++)
            {
                string kind; int n; lock (lk) { if (pendA > 0) { kind = "mp3"; n = (int)Math.Min(50, pendA); } else if (pendV > 0) { kind = "mp4"; n = (int)Math.Min(50, pendV); } else return; }
                try { Post(kind, n); } catch { return; }   // offline or refused: keep it and try again later
                lock (lk) { if (kind == "mp3") pendA -= n; else pendV -= n; Save(); }
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
            var d = MiniJson.Parse(body) as Dictionary<string, object>; string a = Digits(d, "mp3"), v = Digits(d, "mp4"), tot = Digits(d, "total"); if (a == null || v == null || tot == null) { t.Error = "The website sent numbers I could not read."; return t; }
            t.Mp3 = a; t.Mp4 = v; t.Total = tot; t.Ok = true;
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
        return idx < Units.Length ? m + Units[idx] : digits.Substring(0, 1) + (dec0(digits) != "0" ? "." + dec0(digits) : "") + "e" + exp;
    }
    static string dec0(string d) { return d.Substring(1, 1); }
    // the files a conversion made since the snapshot: audio and video counted apart
    public static void CountNew(string dir, HashSet<string> before, int fmtIndex, int fallbackIfNone)
    {
        int a = 0, v = 0;
        try { foreach (var f in Directory.GetFiles(dir)) { if (before != null && before.Contains(f)) continue; string e = Path.GetExtension(f).ToLowerInvariant(); if (Array.IndexOf(VideoExts, e) >= 0) v++; else if (Array.IndexOf(AudioExts, e) >= 0) a++; } } catch { }
        if (a == 0 && v == 0 && fallbackIfNone > 0) { if (fmtIndex >= 5) v = fallbackIfNone; else a = fallbackIfNone; }
        Count(a, v);
    }
}

// one big number that always fits its card: it shrinks until the whole number is inside (no matter how many digits)
sealed class NumberCard : Control
{
    public string Title = "", Value = "0", Sub = ""; public bool Gold;
    public NumberCard() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(Parent != null ? Parent.BackColor : BackColor); g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; var rc = new RectangleF(1.5f, 1.5f, Width - 3f, Height - 3f);
        Win.DrawNeonBox(g, rc, Gold); var ink = TM.C(255, 240, 232, 255); var dim = TM.C(255, 198, 182, 232);
        var fl = TextFormatFlags.NoPrefix | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
        using (var f = Win.UiFont(10f, true)) TextRenderer.DrawText(g, Title, f, new Rectangle(8, 6, Width - 16, 20), dim, fl);
        float pt = 30f; Font big = null; try { for (; pt >= 8f; pt -= 1f) { if (big != null) big.Dispose(); big = Win.UiFont(pt, true); if (TextRenderer.MeasureText(Value, big, new Size(10000, 100), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width <= Width - 24) break; } TextRenderer.DrawText(g, Value, big, new Rectangle(8, 26, Width - 16, Height - 56), Gold ? Color.FromArgb(255, 255, 206, 110) : ink, fl); } finally { if (big != null) big.Dispose(); }
        using (var f = Win.UiFont(11f, false)) TextRenderer.DrawText(g, Sub, f, new Rectangle(8, Height - 28, Width - 16, 22), dim, fl);
    }
}

// the TIMES USED window
sealed class UsageForm : Form
{
    NumberCard cAudio, cVideo, cTotal; Label status, mine, note; PxButton refresh, close; volatile bool gone;
    public static Func<UsageStat.Totals> FetchHook;   // a test answers here
    public UsageForm()
    {
        Text = "Times used worldwide"; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = true; ShowInTaskbar = true; StartPosition = FormStartPosition.CenterParent; BackColor = TM.C(255, 10, 4, 20); ForeColor = TM.C(255, 240, 232, 255); Font = new Font("Segoe UI", 10f);
        Win.Dark(this); DoubleBuffered = true; ClientSize = new Size(520, 470);
        var head = new Label { Text = "Times used worldwide", Left = 24, Top = 14, Width = 472, Height = 38, Font = new Font("Segoe UI Semibold", 20f), ForeColor = TM.C(255, 240, 232, 255), Tag = "nocaps" };
        var sub = new Label { Text = "Every MP3 and MP4 made with Purphex Music, all over the world. It keeps counting with no limit.", Left = 24, Top = 54, Width = 472, Height = 40, ForeColor = TM.C(255, 198, 182, 232), Tag = "nocaps" };
        cTotal = new NumberCard { Left = 24, Top = 100, Width = 472, Height = 112, Title = "TOTAL", Gold = true }; cAudio = new NumberCard { Left = 24, Top = 224, Width = 230, Height = 104, Title = "MP3  (AUDIO)" }; cVideo = new NumberCard { Left = 266, Top = 224, Width = 230, Height = 104, Title = "MP4  (VIDEO)" };
        status = new Label { Left = 24, Top = 340, Width = 472, Height = 22, ForeColor = TM.C(255, 198, 182, 232), Tag = "nocaps", Text = "Loading the worldwide numbers..." };
        mine = new Label { Left = 24, Top = 364, Width = 472, Height = 22, ForeColor = TM.C(255, 240, 232, 255), Tag = "nocaps" };
        note = new Label { Left = 24, Top = 390, Width = 472, Height = 38, ForeColor = TM.C(255, 198, 182, 232), Tag = "nocaps", Font = new Font("Segoe UI", 9.5f), Text = "Only a number goes up by one after each conversion: nothing about you, the link or the song is sent. Settings, General turns the counting off." };
        refresh = new PxButton { Text = "REFRESH", Left = 24, Top = 424, Width = 120, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = TM.C(255, 140, 60, 220), ForeColor = Color.White, Cursor = Cursors.Hand }; refresh.Click += (s, e) => Load();
        close = new PxButton { Text = "CLOSE", Left = 376, Top = 424, Width = 120, Height = 34, FlatStyle = FlatStyle.Flat, BackColor = TM.C(255, 90, 50, 170), ForeColor = Color.White, Cursor = Cursors.Hand }; close.Click += (s, e) => Close();
        Controls.AddRange(new Control[] { head, sub, cTotal, cAudio, cVideo, status, mine, note, refresh, close }); Win.CapsAll(this);
        UpdateMine(); FormClosed += (s, e) => gone = true; Shown += (s, e) => Load();
    }
    void UpdateMine() { mine.Text = "On this PC: " + UsageStat.Group(UsageStat.LocalAudio.ToString(CultureInfo.InvariantCulture)) + " audio and " + UsageStat.Group(UsageStat.LocalVideo.ToString(CultureInfo.InvariantCulture)) + " video"; }
    public string TotalText { get { return cTotal.Value; } }
    public string AudioText { get { return cAudio.Value; } }
    public string VideoText { get { return cVideo.Value; } }
    public string StatusText { get { return status.Text; } }
    public void Load()
    {
        refresh.Enabled = false; status.Text = "Loading the worldwide numbers..."; UsageStat.Flush();
        new Thread(() =>
        {
            var t = FetchHook != null ? FetchHook() : UsageStat.Fetch();
            try { if (!gone && IsHandleCreated) BeginInvoke((Action)(() => { refresh.Enabled = true; UpdateMine(); if (!t.Ok) { status.Text = t.Error; return; } Fill(cTotal, t.Total); Fill(cAudio, t.Mp3); Fill(cVideo, t.Mp4); status.Text = "Updated " + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + ". Press REFRESH to count again."; })); } catch { }
        }) { IsBackground = true }.Start();
    }
    static void Fill(NumberCard c, string digits) { c.Value = UsageStat.Group(digits); c.Sub = UsageStat.Short(digits); c.Invalidate(); }
}
