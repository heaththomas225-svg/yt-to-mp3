using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

// Pictures of the padlocks: the converter as Free and as Supporter, Settings (locked Theme page, Account page), the padlock box and the theme sheet with the Fantasy themes.   csc /main:EdShots ... EditionShots.cs
static class EdShots
{
    static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public; static string dir;
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); Thread.Sleep(10); } }
    static object F(object o, string n) { return o.GetType().GetField(n, BF).GetValue(o); }
    static void Snap(Form f, string name) { f.Refresh(); Pump(100); using (var b = new Bitmap(f.Width, f.Height)) { f.DrawToBitmap(b, new Rectangle(0, 0, f.Width, f.Height)); b.Save(Path.Combine(dir, name + ".png")); } Console.WriteLine("shot " + name); }
    [STAThread]
    static void Main(string[] args)
    {
        dir = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "edshots"); Directory.CreateDirectory(dir); StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_edshots_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(StudioEnv.DataDir);
        Application.EnableVisualStyles(); Prefs.OutVol = 0; CommunityPresets.BaseUrl = "http://127.0.0.1:1";
        var app = (App)Activator.CreateInstance(typeof(App), true); app.StartPosition = FormStartPosition.Manual; app.Location = new Point(20, 20); app.Show(); Pump(500);
        Edition.Override = Tier.Free; Edition.Unlink(); Pump(300); Snap(app, "converter-free");
        var norm = (CheckBox)F(app, "norm"); var sr = (ComboBox)F(app, "sr"); sr.DroppedDown = true; Pump(200); Snap(app, "converter-free-rates"); sr.DroppedDown = false;
        Edition.Override = Tier.Supporter; Edition.Unlink(); Pump(300); Snap(app, "converter-supporter");
        Edition.Override = Tier.Free; Edition.Unlink(); Pump(200);
        var set = new SettingsForm(app); set.StartPosition = FormStartPosition.Manual; set.Location = new Point(40, 40); set.Show(); Pump(300);
        var sm = set.GetType().GetMethod("ShowPage", BF); sm.Invoke(set, new object[] { 1 }); Pump(200); Snap(set, "settings-theme-locked");
        set.ShowAccount(); Pump(200); Snap(set, "settings-account-free");
        Edition.Override = Tier.Fantasy; Edition.Unlink(); Pump(300); Snap(set, "settings-account-fantasy"); set.Close();
        Edition.Override = Tier.Free; var up = new UpsellForm(Tier.Supporter, "The Purphex Music studio"); up.StartPosition = FormStartPosition.Manual; up.Location = new Point(60, 60); up.Show(); Pump(200); Snap(up, "upsell-supporter"); up.Close();
        up = new UpsellForm(Tier.Fantasy, "The Nebula theme"); up.StartPosition = FormStartPosition.Manual; up.Location = new Point(60, 60); up.Show(); Pump(200); Snap(up, "upsell-fantasy"); up.Close();
        // the theme sheet in the studio as a Supporter (the Fantasy themes are padlocked)
        Edition.Override = Tier.Supporter; var sf = new StudioForm(app); sf.ClientSize = new Size(1400, 900); sf.StartPosition = FormStartPosition.Manual; sf.Location = new Point(0, 0); sf.Show(); Pump(500);
        typeof(StudioForm).GetMethod("OpenThemeSheet", BF).Invoke(sf, new object[] { false }); Pump(200); var fld = typeof(StudioForm).GetField("themeScroll", BF); fld.SetValue(sf, 330f); sf.Invalidate(); Pump(300); Snap(sf, "themes-supporter");
        typeof(StudioForm).GetField("themeOpen", BF).SetValue(sf, false);
        var al = new System.Collections.Generic.List<ArtistInfo>(); string[] an = { "DJ Neon Wolf", "Mia Waves", "Sub Zero Ghost", "Kali" }; for (int i = 0; i < an.Length; i++) al.Add(new ArtistInfo { Id = "a" + i.ToString("x12"), Name = an[i], Bio = "Dark dnb and bassline from the north.", Tags = new[] { "dnb", "bassline" }, Followers = 120 - i * 15, Tracks = 3 + i, Accent = i * 55, Fantasy = i == 0 || i == 2 });
        typeof(StudioForm).GetField("artList", BF).SetValue(sf, al); typeof(StudioForm).GetField("artFetched", BF).SetValue(sf, true); typeof(StudioForm).GetField("tab", BF).SetValue(sf, 7); sf.Invalidate(); Pump(300); Snap(sf, "artists-fantasy");
        typeof(StudioForm).GetField("tab", BF).SetValue(sf, 0); typeof(StudioForm).GetMethod("OpenMenu", BF).Invoke(sf, null); sf.Invalidate(); Pump(300); Snap(sf, "menu-main");
        foreach (int sec in new[] { 0, 2, 3, 4 }) { typeof(StudioForm).GetField("menuSec", BF).SetValue(sf, sec); sf.Invalidate(); Pump(300); Snap(sf, "menu-sec" + sec); }
        typeof(StudioForm).GetField("menuOpen", BF).SetValue(sf, false);
        foreach (var sz in new[] { new Size(1500, 900), new Size(1280, 720), new Size(1100, 640) }) { sf.ClientSize = sz; typeof(StudioForm).GetField("tab", BF).SetValue(sf, 1); Pump(300); for (int i = 0; i < 8; i++) { sf.Invalidate(); sf.Update(); Pump(40); } Snap(sf, "master-" + sz.Width + "x" + sz.Height); }
        Environment.Exit(0);
    }
}
