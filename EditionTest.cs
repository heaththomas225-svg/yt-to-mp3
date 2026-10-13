using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;

// Free / Supporter / Fantasy: what each edition can open, the padlocks, the themes of each edition, the help list, the sample-rate limit and linking this PC to an account (against a pretend website).
// No modal box ever opens: under test a padlock only records what it asked for.   csc /main:ED ... EditionTest.cs
static class ED
{
    static int fails; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void Check(string n, bool ok, string d = "") { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + (d.Length > 0 ? "   " + d : "")); }
    static object F(object o, string n) { return o.GetType().GetField(n, BF).GetValue(o); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); Thread.Sleep(10); } }
    static ECDsaCng tkey; static string lastFp = "";
    static string B64U(byte[] b) { return Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_'); }
    static string MakeLic(string tier, string fp, long iat, long exp, string until, ECDsaCng k = null)
    {
        string body = B64U(Encoding.UTF8.GetBytes("{\"v\":1,\"no\":7,\"tier\":\"" + tier + "\",\"until\":\"" + until + "\",\"fp\":\"" + fp + "\",\"iat\":" + iat + ",\"exp\":" + exp + "}")); var sig = (k ?? tkey).SignData(Encoding.ASCII.GetBytes("purphex-license-v1." + body)); return "L1." + body + "." + B64U(sig);
    }
    static string Dec(string t) { t = t.Replace('-', '+').Replace('_', '/'); switch (t.Length % 4) { case 2: t += "=="; break; case 3: t += "="; break; } return Encoding.UTF8.GetString(Convert.FromBase64String(t)); }
    static long Now() { return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds; }
    static HttpListener lis; static volatile bool serve = true; static int polls;
    static void Serve()
    {
        while (serve)
        {
            HttpListenerContext c; try { c = lis.GetContext(); } catch { return; }
            string path = c.Request.Url.AbsolutePath, body = ""; using (var sr = new StreamReader(c.Request.InputStream, Encoding.UTF8)) body = sr.ReadToEnd(); string json = "{}"; int st = 200;
            if (path == "/api/link/start") { var fm = System.Text.RegularExpressions.Regex.Match(body, "\"fp\":\"([0-9a-f]{64})\""); lastFp = fm.Success ? fm.Groups[1].Value : ""; } if (path == "/api/link/start") json = "{\"ok\":true,\"code\":\"AB12-CD34\",\"secret\":\"s3cr3t\",\"expires\":0,\"url\":\"" + CommunityPresets.BaseUrl + "/link?code=AB12-CD34\"}";
            else if (path == "/api/link/poll") { polls++; json = polls < 2 ? "{\"ok\":true,\"state\":\"pending\"}" : "{\"ok\":true,\"state\":\"approved\",\"token\":\"" + new string('a', 64) + "\",\"tier\":\"supporter\",\"name\":\"Supporter Edition\",\"who\":\"Producer 7\",\"license\":\"" + MakeLic("supporter", lastFp, Now(), Now() + 864000, "") + "\"}"; }
            else st = 404;
            var b = Encoding.UTF8.GetBytes(json); c.Response.StatusCode = st; c.Response.ContentType = "application/json"; c.Response.OutputStream.Write(b, 0, b.Length); c.Response.Close();
        }
    }
    [STAThread]
    static void Main()
    {
        StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_edtest_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(StudioEnv.DataDir);
        Application.EnableVisualStyles(); Prefs.OutVol = 0;
        tkey = new ECDsaCng(256); { var pb = tkey.Key.Export(CngKeyBlobFormat.EccPublicBlob); var px = new byte[32]; var py = new byte[32]; Array.Copy(pb, 8, px, 0, 32); Array.Copy(pb, 40, py, 0, 32); Edition.UseKeyForTests(px, py); }
        // ---- the licence rules (what the paid features depend on) ----
        {
            string fp = Edition.Fingerprint; long n = Now(); Check("this PC has a fingerprint (64 hex letters) that does not change", System.Text.RegularExpressions.Regex.IsMatch(fp, "^[0-9a-f]{64}$") && fp == Edition.Fingerprint, fp.Substring(0, 8));
            Edition.AcceptLicenseForTests(MakeLic("supporter", fp, n - 60, n + 864000, ""), n); Check("a licence signed by the website for this PC gives its edition", Edition.FromLicense() == Tier.Supporter);
            Edition.AcceptLicenseForTests(MakeLic("fantasy", fp, n - 60, n + 864000, ""), n); Check("Fantasy likewise", Edition.FromLicense() == Tier.Fantasy);
            Edition.AcceptLicenseForTests(MakeLic("fantasy", fp, n - 900000, n - 60, ""), n); Check("a licence that has run out is Free (the app could not renew it)", Edition.FromLicense() == Tier.Free);
            Edition.AcceptLicenseForTests(MakeLic("fantasy", new string('a', 64), n - 60, n + 864000, ""), n); Check("a licence made for another PC is refused (copying it is useless)", Edition.FromLicense() == Tier.Free);
            { string ok = MakeLic("supporter", fp, n - 60, n + 864000, ""); var parts = ok.Split('.'); string forged = "L1." + B64U(Encoding.UTF8.GetBytes(Dec(parts[1]).Replace("supporter", "fantasy"))) + "." + parts[2];
              Edition.AcceptLicenseForTests(forged, n); Check("editing the licence (supporter to fantasy) breaks the signature", Edition.FromLicense() == Tier.Free); }
            using (var other = new ECDsaCng(256)) { Edition.AcceptLicenseForTests(MakeLic("fantasy", fp, n - 60, n + 864000, "", other), n); Check("a licence signed with any other key is refused", Edition.FromLicense() == Tier.Free); }
            Edition.AcceptLicenseForTests(MakeLic("fantasy", fp, n - 60, n + 864000, ""), n + 10 * 86400); Check("turning the clock back (it ran 10 days ahead before) switches the paid features off", Edition.FromLicense() == Tier.Free);
            Edition.AcceptLicenseForTests(MakeLic("supporter", fp, n - 900000, n + 864000, DateTime.UtcNow.AddDays(-5).ToString("o")), n); Check("a Supporter plan that ended more than 3 days ago is Free even if the licence has not run out", Edition.FromLicense() == Tier.Free);
            Edition.AcceptLicenseForTests("L1.abc.def", n); Check("rubbish is Free", Edition.FromLicense() == Tier.Free); Edition.AcceptLicenseForTests("", n); Check("no licence is Free", Edition.FromLicense() == Tier.Free);
        }
        // ---- the rules, one edition at a time ----
        Edition.Override = Tier.Free;
        Check("Free: has Free, not Supporter, not Fantasy", Edition.Has(Tier.Free) && !Edition.Has(Tier.Supporter) && !Edition.Has(Tier.Fantasy));
        Check("Free: exports stop at 48 kHz", Edition.MaxRate == 48000 && Exporter.RateFor(1, 7) == 48000 && Exporter.RateFor(1, 0) == 44100);
        Check("Free: the help keeps the converter and the vocal remover and hides the studio", Edition.HelpVisible("vocal") && Edition.HelpVisible("start") && Edition.HelpVisible("trim") && !Edition.HelpVisible("studio-mix") && !Edition.HelpVisible("basslab") && !Edition.HelpVisible("masterassistant"));
        int shown = 0; foreach (var t in HelpContent.All) if (Edition.HelpVisible(t.Id)) shown++; Check("Free: only a part of the help guides are listed", shown > 5 && shown < HelpContent.All.Length - 20, shown + " of " + HelpContent.All.Length);
        Check("Free: only the original theme can be used", Themes.Allowed(Themes.All[0]) && !Themes.Allowed(Themes.All[Themes.IndexOf("Ocean")]) && !Themes.Allowed(Themes.All[Themes.IndexOf("Nebula")]));
        Check("a photo background is gone for every edition", !UserBackground.Active);
        Edition.Override = Tier.Supporter;
        Check("Supporter: the studio edition, up to 192 kHz, every theme but the Fantasy ones", Edition.Has(Tier.Supporter) && !Edition.Has(Tier.Fantasy) && Edition.MaxRate == 192000 && Exporter.RateFor(1, 5) == 192000 && Exporter.RateFor(1, 7) == 192000);
        Check("Supporter: the standard themes and the Supporter themes open, the Fantasy themes do not", Themes.Allowed(Themes.All[Themes.IndexOf("Ocean")]) && Themes.Allowed(Themes.All[Themes.IndexOf("Aurora")]) && Themes.Allowed(Themes.All[Themes.IndexOf("Rainbow")]) && !Themes.Allowed(Themes.All[Themes.IndexOf("Royal Gold")]));
        Check("Supporter: the whole help", Edition.HelpVisible("studio-mix") && Edition.HelpVisible("basslab"));
        Edition.Override = Tier.Fantasy;
        Check("Fantasy: everything, 384 kHz and the Fantasy themes", Edition.Has(Tier.Fantasy) && Edition.MaxRate == 384000 && Exporter.RateFor(1, 7) == 384000 && Themes.Allowed(Themes.All[Themes.IndexOf("Nebula")]) && Themes.Allowed(Themes.All[Themes.IndexOf("Dragon Fire")]));
        int sup = 0, fan = 0, free = 0; foreach (var t in Themes.All) { if (t.Special && t.Req == Tier.Supporter) sup++; if (t.Special && t.Req == Tier.Fantasy) fan++; if (t.Req == Tier.Free) free++; }
        Check("there are themes made for each edition: 3 for Supporter, 5 for Fantasy, 1 for Free", sup == 3 && fan == 5 && free == 1, sup + " / " + fan + " / " + free);
        { var names = new HashSet<string>(); bool dup = false; foreach (var t in Themes.All) if (!names.Add(t.Name)) dup = true; Check("theme names are unique", !dup); }
        // every edition theme is legible (no dark-on-dark): the same check the other themes pass
        { bool ok = true; foreach (var t in Themes.All) { if (!t.Special) continue; double h, s, l; ThemeDef.ToHsl(t.Violet, out h, out s, out l); if (l < 0.35) ok = false; ThemeDef.ToHsl(t.Bg0, out h, out s, out l); if (l > 0.12) ok = false; } Check("the edition themes have bright accents on a dark background", ok); }
        // a theme above the edition is put back
        Edition.Override = Tier.Fantasy; Themes.Apply(Themes.IndexOf("Nebula")); Edition.Override = Tier.Supporter; bool changed = Themes.EnforceEdition();
        Check("a Fantasy theme in use is put back to the original when the edition drops", changed && Themes.Current.Name == "Purphex");
        Edition.Override = Tier.Supporter; Themes.Apply(Themes.IndexOf("Ocean")); Edition.Override = Tier.Free; Check("a standard theme goes back for Free too", Themes.EnforceEdition() && Themes.Current.Name == "Purphex");

        // ---- the converter window ----
        var app = (App)Activator.CreateInstance(typeof(App), true); app.ShowInTaskbar = false; app.Opacity = 0; app.Show(); Pump(300);
        Edition.Override = Tier.Free; Edition.LastUpsell = "";
        var openStudio = typeof(App).GetMethod("OpenStudio", BF); bool opened = (bool)openStudio.Invoke(app, null);
        Check("Free: the studio does not open, and the padlock asks for it", !opened && F(app, "studioForm") == null && Edition.LastUpsell == "The Purphex Music studio", Edition.LastUpsell);
        Edition.LastUpsell = ""; ((Button)F(app, "studioBtn")).PerformClick(); Check("Free: pressing PURPHEX MUSIC does the same", F(app, "studioForm") == null && Edition.LastUpsell == "The Purphex Music studio");
        var fmt = (ComboBox)F(app, "fmt"); var sr = (ComboBox)F(app, "sr"); var norm = (CheckBox)F(app, "norm");
        fmt.SelectedIndex = 1; Check("Free: WAV is refused (stays on MP3)", fmt.SelectedIndex == 0 && Edition.LastUpsell == "That format", fmt.SelectedIndex + " " + Edition.LastUpsell);
        fmt.SelectedIndex = 6; Check("Free: an MP4 quality is allowed", fmt.SelectedIndex == 6); fmt.SelectedIndex = 11; Check("Free: MKV is refused", fmt.SelectedIndex == 6); fmt.SelectedIndex = 0;
        Edition.LastUpsell = ""; sr.SelectedIndex = 4; Check("Free: 96 kHz is refused", sr.SelectedIndex == 0 && Edition.LastUpsell.StartsWith("Exporting at"), sr.SelectedIndex + " " + Edition.LastUpsell); sr.SelectedIndex = 2; Check("Free: 48 kHz is allowed", sr.SelectedIndex == 2); sr.SelectedIndex = 0;
        norm.Checked = true; Check("Free: Normalize loudness will not tick", !norm.Checked);
        Check("Free: the locked formats say which edition they need", ((string)fmt.Items[1]).Contains("(Supporter)") && !((string)fmt.Items[0]).Contains("Supporter") && ((string)sr.Items[8]).Contains("(Fantasy)") && ((string)sr.Items[4]).Contains("(Supporter)"), (string)fmt.Items[1]);
        Edition.Override = Tier.Supporter; Edition.Unlink(); Pump(200);   // Unlink tells every window the edition changed
        Check("Supporter: the lists lose their labels (but 384 kHz says Fantasy)", !((string)fmt.Items[1]).Contains("(") && ((string)sr.Items[8]).Contains("(Fantasy)") && !((string)sr.Items[6]).Contains("("), (string)sr.Items[8] + " | " + (string)sr.Items[6]);
        sr.SelectedIndex = 7; Check("Supporter: 352.8 kHz is refused, it is a Fantasy rate", sr.SelectedIndex != 7); sr.SelectedIndex = 6; Check("Supporter: 192 kHz is allowed", sr.SelectedIndex == 6); sr.SelectedIndex = 0;
        norm.Checked = true; Check("Supporter: Normalize loudness ticks", norm.Checked); norm.Checked = false;
        bool o2 = (bool)openStudio.Invoke(app, null); Check("Supporter: the studio opens", o2 && F(app, "studioForm") != null);
        try { var sfm = (Form)F(app, "studioForm"); sfm.Close(); } catch { }
        Edition.Override = Tier.Fantasy; sr.SelectedIndex = 8; Check("Fantasy: 384 kHz is allowed", sr.SelectedIndex == 8); sr.SelectedIndex = 0;

        // ---- Settings: the Account page and the padlocked pages ----
        Edition.Override = Tier.Free; var set = new SettingsForm(app); set.ShowInTaskbar = false; set.Opacity = 0; set.Show(); Pump(200);
        var pages = (List<Panel>)F(set, "pages"); Panel bgp = null, thp = null, acc = null; foreach (var p in pages) { if ((string)p.Tag == "Background") bgp = p; if ((string)p.Tag == "Theme") thp = p; if ((string)p.Tag == "Account") acc = p; }
        var banners = (Dictionary<Panel, Panel>)F(set, "lockBanners");
        Check("Settings: there is an Account page, last in the list", acc != null && pages[pages.Count - 1] == acc);
        Check("Free: the Background and Theme pages show a padlock banner and their controls are off", BannerShown(set, 0, banners[bgp]) && BannerShown(set, 1, banners[thp]) && AllOff(bgp, banners[bgp]) && AllOff(thp, banners[thp]));
        var accLine = (Label)F(set, "accLine"); var accSub = (Label)F(set, "accSub"); var linkBtn = (Button)F(set, "linkBtn");
        Edition.Override = Tier.Free; set.GetType().GetMethod("RefreshAccount", BF).Invoke(set, null);
        Check("Free: the Account page says Free Edition and offers LINK THIS PC", accLine.Text == "Free Edition" && linkBtn.Text == "LINK THIS PC" && accSub.Text.Contains("not linked"), accLine.Text + " | " + linkBtn.Text);
        Edition.Override = Tier.Supporter; Edition.Unlink(); Pump(200);
        Check("Supporter: the banners go away and the controls come on", !BannerShown(set, 0, banners[bgp]) && !BannerShown(set, 1, banners[thp]) && !AnyOff(bgp, banners[bgp]) && accLine.Text == "Supporter Edition", accLine.Text);
        var themeBox = (ComboBox)F(set, "themeBox"); bool hasTag = false; for (int i = 0; i < themeBox.Items.Count; i++) if (((string)themeBox.Items[i]).Contains("(FANTASY)")) hasTag = true;
        Check("the theme list names the Fantasy and Supporter themes", hasTag && ((string)themeBox.Items[themeBox.FindString("Aurora")]).Contains("(SUPPORTER)"));
        int before = themeBox.SelectedIndex; Edition.LastUpsell = ""; themeBox.SelectedIndex = Themes.IndexOf("Nebula");
        Check("Supporter: picking a Fantasy theme asks for Fantasy and keeps the old theme", Edition.LastUpsell.Contains("Nebula") && Themes.Current.Name != "Nebula" && themeBox.SelectedIndex == before, Edition.LastUpsell);
        set.Close();

        // ---- linking this PC to an account ----
        int port = 19000 + new Random().Next(900); lis = new HttpListener(); lis.Prefixes.Add("http://127.0.0.1:" + port + "/"); lis.Start(); new Thread(Serve) { IsBackground = true }.Start(); CommunityPresets.BaseUrl = "http://127.0.0.1:" + port;
        Edition.Unlink(); Check("before linking nothing is linked", !Edition.Linked);
        string last = ""; bool fin = false, okl = false; Edition.StartLink(m => last = m, (ok, m) => { okl = ok; fin = true; });
        var end = DateTime.Now.AddSeconds(20); while (!fin && DateTime.Now < end) Pump(50);
        Check("LINK THIS PC shows the code, opens the website and waits", last.Contains("AB12-CD34") && Edition.LastUpsell.StartsWith("open " + CommunityPresets.BaseUrl + "/link?code=AB12-CD34"), last + " | " + Edition.LastUpsell);
        Check("the approval comes back as a token: this PC is linked to Producer 7", fin && okl && Edition.Linked && Edition.Who == "Producer 7", "who=" + Edition.Who);
        Check("and the licence that came with it was made for this PC and is good (Supporter)", Edition.FromLicense() == Tier.Supporter && Edition.LicenseValidForThisPc(MakeLic("supporter", Edition.Fingerprint, Now(), Now() + 1000, "")), Edition.FromLicense().ToString());
        string file = File.ReadAllText(Path.Combine(StudioEnv.DataDir, "edition.txt")); Check("the token is saved encrypted, never as plain text", file.Contains("token=") && !file.Contains(new string('a', 64)) && file.Contains("who=Producer 7"));
        Edition.Unlink(); Check("UNLINK THIS PC forgets it (and the licence)", !Edition.Linked && Edition.FromLicense() == Tier.Free && !File.ReadAllText(Path.Combine(StudioEnv.DataDir, "edition.txt")).Contains("token=A"));
        serve = false; try { lis.Stop(); } catch { }
        Console.WriteLine(fails == 0 ? "=== edition checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
    static bool BannerShown(SettingsForm set, int page, Panel banner) { set.GetType().GetMethod("ShowPage", BF).Invoke(set, new object[] { page }); return banner.Visible; }
    static bool AllOff(Panel p, Panel banner) { foreach (Control c in p.Controls) if (c != banner && c.Enabled) { Console.WriteLine("  still on: " + c.GetType().Name + " " + c.Text); return false; } return true; }
    static bool AnyOff(Panel p, Panel banner) { foreach (Control c in p.Controls) if (c != banner && !c.Enabled) return true; return false; }
}
