using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;

// Editions: Free (the MP3 / MP4 converter and the vocal remover), Supporter (the whole studio) and Fantasy (everything, for life).
// This PC learns its edition by being linked to a website account (LINK THIS PC in Settings): the website gives a secret token, the token is kept on this PC (encrypted for this Windows user) and
// asked about now and then. Offline it keeps working for a while. Nothing here ever touches a card: payments happen on the website.
enum Tier { Free = 0, Supporter = 1, Fantasy = 2 }

static class Edition
{
    public static Tier? Override;                 // tests: pretend to be this edition
    public static string LastUpsell = "";         // tests: what the last padlock click asked for
    public static Action OnShowAccount;           // set by the main window: opens Settings on the Account page
    public static int Version;                    // goes up whenever the edition changes
    public static event Action Changed;           // on the window thread

    static string tokenEnc = "", licText = "", who = "", billing = ""; static long lastSeen; static bool loaded; static readonly object lk = new object();
    static volatile bool linking; static volatile bool cancelLink; static volatile bool tampered; public static string Note = "";
    // a licence that was checked: the edition, when it ends, and the time it was made and runs out (unix seconds)
    sealed class Lic { public Tier T; public string Until = ""; public long Iat, Exp; }
    static Lic lic;   // null = no valid licence for this PC
    static string FilePath { get { return Path.Combine(StudioEnv.DataDir, "edition.txt"); } }
    static long NowMs() { return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds; }
    static long NowSec() { return NowMs() / 1000; }
    public static string Site { get { return CommunityPresets.BaseUrl; } }

    // ---- the licence ----
    // The website signs "this PC has this edition until this time" with a private key only it has. The matching PUBLIC key is built in below: it can check a licence but never make one,
    // so a licence cannot be edited, invented, or copied to another PC (it names the PC it was made for). It runs out after a few days unless the website renews it.
    static byte[] PubX = Hex("4bf0f47191cb1e0874deb16722303877cc118cfa8faf2dfee24f3d127d29dd1e"), PubY = Hex("0345a4d05d6527f1b11ff9fb5fa94d282573e25ec71c2b528a25e431cd9621dc");
    // tests only: use another public key (so a test can make its own licences); ignored outside a test
    internal static void UseKeyForTests(byte[] x, byte[] y) { if (!StudioEnv.UnderTest) return; PubX = x; PubY = y; }
    internal static void AcceptLicenseForTests(string text, long seen) { lock (lk) { loaded = true; licText = text; lic = CheckLicense(text); lastSeen = seen; } }
    static byte[] Hex(string h) { var r = new byte[h.Length / 2]; for (int i = 0; i < r.Length; i++) r[i] = Convert.ToByte(h.Substring(i * 2, 2), 16); return r; }
    static string fpCache;
    // who this PC is (a hash of the Windows machine id and your Windows user): the licence only works here
    public static string Fingerprint
    {
        get
        {
            if (fpCache == null)
            {
                string g = ""; try { using (var bk = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64)) using (var k = bk.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography")) g = Convert.ToString(k.GetValue("MachineGuid")); } catch { }
                if (g.Length == 0) g = Environment.MachineName; using (var sha = SHA256.Create()) fpCache = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes("purphex-pc|" + g + "|" + Environment.UserName))).Replace("-", "").ToLowerInvariant();
            }
            return fpCache;
        }
    }
    static byte[] B64U(string t) { t = t.Replace('-', '+').Replace('_', '/'); switch (t.Length % 4) { case 2: t += "=="; break; case 3: t += "="; break; } return Convert.FromBase64String(t); }
    static double Num(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) && v is double ? (double)v : 0; }
    static Lic CheckLicense(string text)
    {
        try
        {
            var parts = (text ?? "").Split('.'); if (parts.Length != 3 || parts[0] != "L1") return null;
            var data = Encoding.ASCII.GetBytes("purphex-license-v1." + parts[1]); var sig = B64U(parts[2]); if (sig.Length != 64) return null;
            var blob = new byte[72]; blob[0] = 0x45; blob[1] = 0x43; blob[2] = 0x53; blob[3] = 0x31; blob[4] = 32; Array.Copy(PubX, 0, blob, 8, 32); Array.Copy(PubY, 0, blob, 40, 32);   // "ECS1" + 32 + X + Y
            using (var key = CngKey.Import(blob, CngKeyBlobFormat.EccPublicBlob)) using (var ec = new ECDsaCng(key)) { if (!ec.VerifyData(data, sig)) return null; }
            var o = MiniJson.Parse(Encoding.UTF8.GetString(B64U(parts[1]))) as Dictionary<string, object>; if (o == null || Num(o, "v") != 1) return null;
            if (!string.Equals(S(o, "fp"), Fingerprint, StringComparison.Ordinal)) return null;   // made for another PC
            return new Lic { T = ParseTier(S(o, "tier")), Until = Clean(S(o, "until"), 40), Iat = (long)Num(o, "iat"), Exp = (long)Num(o, "exp") };
        }
        catch { return null; }
    }
    // for the tests: a licence for this PC signed with any key can be offered; only the built-in key is accepted
    internal static bool LicenseValidForThisPc(string text) { return CheckLicense(text) != null; }

    // ---- what the edition is right now ----
    public static Tier Current
    {
        get
        {
            if (Override.HasValue) return Override.Value; if (StudioEnv.UnderTest) return Tier.Fantasy;
            return FromLicense();
        }
    }
    // the rules for a licence: really signed for this PC, not run out, the clock not turned back, a Supporter plan not ended
    internal static Tier FromLicense()
    {
        {
            Load(); lock (lk)
            {
                if (lic == null || tampered) return Tier.Free; long now = NowSec();
                if (now < lastSeen - 86400) return Tier.Free;   // the clock was turned back to make a licence last longer
                if (now > lastSeen) lastSeen = now;
                if (now > lic.Exp || now < lic.Iat - 86400) return Tier.Free;   // it ran out (the app has not been able to renew it)
                if (lic.T == Tier.Supporter && lic.Until.Length > 0) { DateTime u; if (DateTime.TryParse(lic.Until, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out u) && u.AddDays(3) < DateTime.UtcNow) return Tier.Free; }
                return lic.T;
            }
        }
    }
    public static bool Has(Tier t) { return Current >= t; }
    public static string Name(Tier t) { return t == Tier.Fantasy ? "Fantasy Edition" : t == Tier.Supporter ? "Supporter Edition" : "Free Edition"; }
    public static bool Linked { get { Load(); lock (lk) return tokenEnc.Length > 0; } }
    public static string Token { get { if (StudioEnv.UnderTest) return ""; Load(); lock (lk) return tokenEnc; } }   // sent only to purphexmusic.com, when publishing a profile (the Fantasy badge)
    public static string Who { get { Load(); lock (lk) return who; } }
    public static string Until { get { Load(); lock (lk) return lic == null ? "" : lic.Until; } }
    public static string Billing { get { Load(); lock (lk) return billing; } }
    public static bool Linking { get { return linking; } }
    // the highest sample rate this edition can export
    public static int MaxRate { get { var t = Current; return t == Tier.Fantasy ? 384000 : (t == Tier.Supporter ? 192000 : 48000); } }

    // the help guides a Free edition keeps: the converter, the vocal remover and the basics
    static readonly HashSet<string> FreeHelp = new HashSet<string> { "start", "formats", "batch", "trim", "vocal", "settings", "fullscreen", "download-video", "updates", "trouble", "long", "editions", "lyrics-getter" };
    public static bool HelpVisible(string id) { return Has(Tier.Supporter) || FreeHelp.Contains(id); }

    // ---- saved state ----
    static void Load()
    {
        if (loaded) return; lock (lk)
        {
            if (loaded) return; loaded = true;
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (var line in File.ReadAllLines(FilePath, Encoding.UTF8)) { int i = line.IndexOf('='); if (i <= 0) continue; string k = line.Substring(0, i), v = line.Substring(i + 1).Trim();
                    if (k == "token") { try { tokenEnc = Encoding.ASCII.GetString(ProtectedData.Unprotect(Convert.FromBase64String(v), null, DataProtectionScope.CurrentUser)); } catch { tokenEnc = ""; } }
                    else if (k == "license") licText = v; else if (k == "who") who = Clean(v, 40); else if (k == "billing") billing = Clean(v, 20); else if (k == "seen") long.TryParse(v, out lastSeen); }
                lic = CheckLicense(licText); if (lic == null) licText = "";
            }
            catch { }
        }
    }
    static void Save()
    {
        try
        {
            Directory.CreateDirectory(StudioEnv.DataDir); string enc = ""; if (tokenEnc.Length > 0) enc = Convert.ToBase64String(ProtectedData.Protect(Encoding.ASCII.GetBytes(tokenEnc), null, DataProtectionScope.CurrentUser));
            File.WriteAllText(FilePath, "token=" + enc + "\nlicense=" + licText + "\nwho=" + who.Replace('\n', ' ') + "\nbilling=" + billing + "\nseen=" + Math.Max(lastSeen, NowSec()) + "\n", Encoding.UTF8);
        }
        catch { }
    }
    // remember the time the app last ran (so turning the clock back cannot stretch a licence); called when the app closes
    public static void SaveSeen() { if (StudioEnv.UnderTest || !loaded) return; lock (lk) Save(); }
    static Tier ParseTier(string t) { return t == "2" || t == "fantasy" ? Tier.Fantasy : (t == "1" || t == "supporter" ? Tier.Supporter : Tier.Free); }
    static void Raise()
    {
        Version++; try { var fs = Application.OpenForms; Form f = fs.Count > 0 ? fs[0] : null; if (f != null && f.IsHandleCreated && !f.IsDisposed) f.BeginInvoke(new Action(() => { var c = Changed; if (c != null) c(); })); else { var c = Changed; if (c != null) c(); } } catch { }
    }

    // ---- the website ----
    static string Req(string method, string path, string body, string bearer, out int status)
    {
        status = 0; try
        {
            var rq = (HttpWebRequest)WebRequest.Create(Site + path); rq.Method = method; rq.UserAgent = "PurphexMusic"; rq.Timeout = 15000; rq.ReadWriteTimeout = 15000; if (bearer != null) { rq.Headers["Authorization"] = "Bearer " + bearer; rq.Headers["X-Device"] = Fingerprint; }
            if (body != null) { var bb = Encoding.UTF8.GetBytes(body); rq.ContentType = "application/json"; rq.ContentLength = bb.Length; using (var st = rq.GetRequestStream()) st.Write(bb, 0, bb.Length); }
            try { using (var r = (HttpWebResponse)rq.GetResponse()) { status = (int)r.StatusCode; using (var sr = new StreamReader(r.GetResponseStream(), Encoding.UTF8)) return sr.ReadToEnd(); } }
            catch (WebException ex) { if (ex.Response == null) return null; var hr = (HttpWebResponse)ex.Response; status = (int)hr.StatusCode; using (var sr = new StreamReader(hr.GetResponseStream(), Encoding.UTF8)) return sr.ReadToEnd(); }
        }
        catch { return null; }
    }
    static string S(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) && v is string ? (string)v : ""; }
    static Dictionary<string, object> Obj(string t) { try { return t == null ? null : MiniJson.Parse(t) as Dictionary<string, object>; } catch { return null; } }
    static string Clean(string t, int n) { var sb = new StringBuilder(); foreach (char c in t ?? "") { if (c >= 32 && c != '<' && c != '>' && sb.Length < n) sb.Append(c); } return sb.ToString(); }
    // keep a licence the website just gave (only if it is really signed for this PC); true when it was good
    static bool TakeLicense(Dictionary<string, object> o)
    {
        var l = CheckLicense(S(o, "license")); if (l == null) { Note = S(o, "license").Length == 0 ? "This PC is linked, but the website cannot make licences yet (its set-up is not finished). Press CHECK AGAIN in a little while." : "The licence the website sent is not valid for this PC."; return false; }
        lock (lk) { licText = S(o, "license"); lic = l; who = Clean(S(o, "who"), 40); billing = Clean(S(o, "billing"), 20); lastSeen = Math.Max(lastSeen, NowSec()); Save(); } Note = ""; return true;
    }

    // ask the website for a fresh licence (in the background); a PC that was unlinked, or a token used on another PC, is forgotten
    public static void Refresh(Action<bool> done = null)
    {
        if (StudioEnv.UnderTest) { if (done != null) done(false); return; }
        Load(); string tok; lock (lk) tok = tokenEnc; if (tok.Length == 0) { if (done != null) done(false); return; }
        ThreadPool.QueueUserWorkItem(_ =>
        {
            int st; var o = Obj(Req("GET", "/api/license", null, tok, out st)); bool ok = false;
            if (st == 200 && o != null) { ok = TakeLicense(o); Raise(); }
            else if (st == 401 || st == 403) { lock (lk) { tokenEnc = ""; licText = ""; lic = null; who = ""; billing = ""; Save(); } Note = st == 403 ? "This PC's link belonged to another PC and was removed. Press LINK THIS PC again." : "This PC was unlinked from the account."; Raise(); }
            if (done != null) done(ok);
        });
    }
    // connect this PC to a website account: the website opens, the person presses approve, the token and the licence for this PC come back
    public static void StartLink(Action<string> progress, Action<bool, string> done)
    {
        if (linking) return; linking = true; cancelLink = false;
        var t = new Thread(() =>
        {
            string msg = "Could not link this PC."; bool ok = false;
            try
            {
                int st; var o = Obj(Req("POST", "/api/link/start", "{\"device\":" + ProfApi.J(Environment.MachineName) + ",\"fp\":" + ProfApi.J(Fingerprint) + "}", null, out st));
                if (st != 200 || o == null || S(o, "code").Length == 0) { msg = st == 429 ? "Too many tries. Wait a few minutes and try again." : "Could not reach purphexmusic.com. Check your connection."; throw new Exception(msg); }
                string code = S(o, "code"), secret = S(o, "secret"), url = S(o, "url"); if (!url.StartsWith(Site, StringComparison.OrdinalIgnoreCase)) url = Site + "/link?code=" + Uri.EscapeDataString(code);
                if (progress != null) progress("Your code is " + code + ". The website is open: sign in and press Yes, link it.");
                OpenUrl(url); var end = DateTime.UtcNow.AddMinutes(10);
                while (DateTime.UtcNow < end && !cancelLink)
                {
                    Thread.Sleep(2000); var p = Obj(Req("POST", "/api/link/poll", "{\"code\":" + ProfApi.J(code) + ",\"secret\":" + ProfApi.J(secret) + "}", null, out st)); if (p == null) continue; string state = S(p, "state");
                    if (state == "approved") { lock (lk) { tokenEnc = S(p, "token"); } ok = TakeLicense(p); if (!ok) { if (S(p, "license").Length == 0) { lock (lk) { Save(); } msg = Note; ok = true; } else { lock (lk) { tokenEnc = ""; Save(); } msg = Note; } } else { msg = "Linked."; } Raise(); if (ok) Refresh(); break; }   // the website made the link but could not sign a licence yet: the PC stays linked, and CHECK AGAIN asks again later
                    if (state == "expired" || state == "gone") { msg = "That code ran out. Press LINK THIS PC again."; break; }
                }
                if (!ok && cancelLink) msg = "Cancelled."; else if (!ok && msg == "Could not link this PC.") msg = "Nobody approved the code in 10 minutes. Press LINK THIS PC again.";
            }
            catch (Exception ex) { if (msg == "Could not link this PC.") msg = ex.Message; }
            linking = false; if (done != null) done(ok, msg);
        }) { IsBackground = true }; t.Start();
    }
    public static void CancelLink() { cancelLink = true; }
    public static void Unlink() { lock (lk) { tokenEnc = ""; licText = ""; lic = null; who = ""; billing = ""; Save(); } Note = ""; Raise(); }
    // a copy of the program that is not the published one (patched to skip the checks) keeps the paid features locked; any problem reaching the website keeps things as they are
    public static void SelfCheck(string version, string manifestUrl)
    {
        if (StudioEnv.UnderTest || System.Diagnostics.Debugger.IsAttached) return;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                string[] lines; using (var wc = new WebClient()) lines = wc.DownloadString(manifestUrl + "?t=" + DateTime.UtcNow.Ticks).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length < 4 || lines[0].Trim() != version) return;   // a newer or older release is out: nothing to compare
                string want = ""; foreach (var l in lines) if (l.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)) want = l.Substring(7).Trim().ToLowerInvariant(); if (want.Length != 64) return;
                string have; using (var fs = new FileStream(Application.ExecutablePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) using (var sha = SHA256.Create()) have = BitConverter.ToString(sha.ComputeHash(fs)).Replace("-", "").ToLowerInvariant();
                if (have != want) { tampered = true; Note = "This copy of Purphex Music is not the published one, so the paid features are locked. Download it again from purphexmusic.com."; Raise(); }
            }
            catch { }
        });
    }
    public static void OpenUrl(string url) { if (StudioEnv.UnderTest) { LastUpsell = "open " + url; return; } try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { } }
    public static void OpenEditions() { OpenUrl(Site + "/#editions"); }

    // ---- padlocks ----
    // true when the edition allows it; otherwise the prompt (or a note for the tests) and false
    public static bool Gate(Tier need, string what, IWin32Window owner = null) { if (Has(need)) return true; Upsell(need, what, owner); return false; }
    static bool prompting;
    public static void Upsell(Tier need, string what, IWin32Window owner = null)
    {
        LastUpsell = what; if (StudioEnv.UnderTest || prompting) return; prompting = true;
        try { using (var f = new UpsellForm(need, what)) { f.ShowDialog(owner); if (f.Choice == 1) OpenEditions(); else if (f.Choice == 2 && OnShowAccount != null) OnShowAccount(); } }
        finally { prompting = false; }
    }
    static readonly Dictionary<Control, Tier> locked = new Dictionary<Control, Tier>();
    // grey a control out and draw a padlock on it while the edition does not allow it (the click itself must call Gate)
    public static void Lock(Control c, Tier need)
    {
        if (locked.ContainsKey(c)) return; locked[c] = need;
        var dc = c as DarkCheck; if (dc != null) dc.Overlay = g => { if (!Has(need)) PaintLocked(g, dc.ClientRectangle, need); }; else c.Paint += (s, e) => { if (!Has(need)) PaintLocked(e.Graphics, c.ClientRectangle, need); };
        c.Disposed += (s, e) => locked.Remove(c);
    }
    public static void RepaintLocked() { foreach (var c in new List<Control>(locked.Keys)) { try { c.Invalidate(); } catch { } } }
    static void PaintLocked(Graphics g, Rectangle r, Tier need)
    {
        bool narrow = r.Width < 90;   // a small button: the padlock replaces its words
        using (var br = new SolidBrush(Color.FromArgb(narrow ? 215 : 150, 6, 4, 14))) g.FillRectangle(br, r);
        var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias; int s = Math.Min(r.Height - 8, 18); if (s < 10) s = 10;
        var ic = new RectangleF(narrow ? r.X + (r.Width - s) / 2f : r.Right - s - 8, r.Y + (r.Height - s) / 2f, s, s); Padlock(g, ic, need == Tier.Fantasy ? Color.FromArgb(255, 255, 90, 220) : Color.FromArgb(255, 255, 200, 90)); g.SmoothingMode = sm;
    }
    // a small padlock: a body and a shackle
    public static void Padlock(Graphics g, RectangleF r, Color c)
    {
        float bw = r.Width * 0.74f, bh = r.Height * 0.52f, bx = r.X + (r.Width - bw) / 2f, by = r.Bottom - bh; float sw = bw * 0.62f, sx = r.X + (r.Width - sw) / 2f;
        using (var pen = new Pen(c, Math.Max(1.5f, r.Width * 0.12f))) g.DrawArc(pen, sx, r.Y + 0.5f, sw, (by - r.Y) * 1.7f, 180, 180);
        using (var br = new SolidBrush(c)) { using (var p = new GraphicsPath()) { float rr = Math.Min(3f, bh / 3f); p.AddArc(bx, by, rr * 2, rr * 2, 180, 90); p.AddArc(bx + bw - rr * 2, by, rr * 2, rr * 2, 270, 90); p.AddArc(bx + bw - rr * 2, by + bh - rr * 2, rr * 2, rr * 2, 0, 90); p.AddArc(bx, by + bh - rr * 2, rr * 2, rr * 2, 90, 90); p.CloseFigure(); g.FillPath(br, p); } }
        using (var br = new SolidBrush(Color.FromArgb(230, 10, 6, 20))) g.FillEllipse(br, r.X + r.Width / 2f - r.Width * 0.07f, by + bh * 0.28f, r.Width * 0.14f, r.Width * 0.14f);
    }
}

// the studio checks the edition again by itself: a licence that ran out, a PC that was unlinked, a clock turned back or a copy that was changed all close it
partial class StudioForm
{
    int edWatchAt = Environment.TickCount; bool edClosing;
    void EditionWatch()
    {
        if (StudioEnv.UnderTest || edClosing || unchecked(Environment.TickCount - edWatchAt) < 20000) return; edWatchAt = Environment.TickCount;
        if (Edition.Has(Tier.Supporter)) return; edClosing = true;
        BeginInvoke(new Action(() => { try { MessageBox.Show(this, "Your edition no longer includes the studio (the licence ran out, this PC was unlinked, or it could not be checked). Save your work from the converter window, then link this PC again in Settings.", "Purphex editions"); } catch { } Close(); }));
    }
}

// the box that says what a padlock needs and where to get it
sealed class UpsellForm : Form
{
    public int Choice;   // 1 = see the editions, 2 = I have one: link this PC
    readonly Tier need; readonly string what;
    public UpsellForm(Tier need, string what)
    {
        this.need = need; this.what = what; Text = "Purphex editions"; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false; StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 232); BackColor = TM.C(255, 10, 4, 20); ForeColor = TM.C(255, 240, 232, 255); Font = new Font("Segoe UI", 10f); Win.Dark(this); DoubleBuffered = true;
        var b1 = MakeBtn("SEE THE EDITIONS", 20, 176, 190, TM.C(255, 190, 60, 200)); b1.Click += (s, e) => { Choice = 1; Close(); };
        var b2 = MakeBtn("I HAVE ONE: LINK THIS PC", 218, 176, 190, TM.C(255, 70, 40, 150)); b2.Click += (s, e) => { Choice = 2; Close(); };
        var b3 = MakeBtn("NOT NOW", 416, 176, 84, TM.C(255, 60, 30, 90)); b3.Click += (s, e) => Close(); Controls.AddRange(new Control[] { b1, b2, b3 }); AcceptButton = b1; CancelButton = b3;
        Win.CapsAll(this);
    }
    static Button MakeBtn(string t, int x, int y, int w, Color c) { var b = new PxButton { Text = t, Left = x, Top = y, Width = w, Height = 38, FlatStyle = FlatStyle.Flat, BackColor = c, ForeColor = Color.White, Cursor = Cursors.Hand, Font = new Font("Segoe UI Semibold", 8.5f) }; b.FlatAppearance.BorderColor = TM.C(255, 190, 120, 255); return b; }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e); var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        Color c = need == Tier.Fantasy ? Color.FromArgb(255, 255, 90, 220) : Color.FromArgb(255, 255, 200, 90);
        using (var pen = new Pen(Color.FromArgb(150, c), 1.5f)) g.DrawRectangle(pen, 8, 8, ClientSize.Width - 17, ClientSize.Height - 17);
        Edition.Padlock(g, new RectangleF(28, 30, 44, 50), c);
        using (var f = new Font("Segoe UI Semibold", 15f)) using (var br = new SolidBrush(TM.C(255, 245, 238, 255))) g.DrawString(what + " is locked", f, br, new RectangleF(92, 26, 410, 30));
        string line = need == Tier.Fantasy ? "It is part of the Fantasy Edition (one payment, for life)." : "It is part of the Supporter Edition and the Fantasy Edition.";
        using (var f = new Font("Segoe UI", 10f)) using (var br = new SolidBrush(TM.C(255, 190, 170, 240))) g.DrawString(line + "\n\nThe Free Edition has the MP3 and MP4 converter and the vocal remover. Pick an edition on the website, then press LINK THIS PC in Settings and this PC unlocks by itself.", f, br, new RectangleF(92, 62, 410, 110));
    }
}
