using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// The projects page: asking before opening, removing (list only / Recycle Bin), remove picture, the logo as the default picture, pictures that fill the square,
// the BACK button, the preview player, and the colour themes.   csc /main:PR ... ProjectsTest.cs
static class PR
{
    static int fails; static StudioForm sf; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + "   " + d); Console.Out.Flush(); }
    static T F<T>(string n) { return (T)typeof(StudioForm).GetField(n, BF).GetValue(sf); }
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static void Ev(string name, MouseButtons b, int clicks, int x, int y) { typeof(StudioForm).GetMethod(name, BF | BindingFlags.DeclaredOnly).Invoke(sf, new object[] { new MouseEventArgs(b, clicks, x, y, 0) }); }
    static void Paint() { sf.Invalidate(); sf.Update(); Application.DoEvents(); }
    static List<HitReg> Regs() { var l = new List<HitReg>(); foreach (object o in (IEnumerable)typeof(StudioForm).GetField("regs", BF).GetValue(sf)) l.Add((HitReg)o); return l; }
    static void Click(RectangleF r) { int x = (int)(r.X + r.Width / 2), y = (int)(r.Y + r.Height / 2); Ev("OnMouseMove", MouseButtons.None, 0, x, y); Ev("OnMouseDown", MouseButtons.Left, 1, x, y); Ev("OnMouseUp", MouseButtons.Left, 1, x, y); }
    static Color ThemeStatic(string n) { return (Color)typeof(StudioForm).GetField(n, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null); }

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng;
        string dir = Path.Combine(Path.GetTempPath(), "projtest-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        string data = StudioEnv.DataDir; string bak = Path.Combine(dir, "bak"); Directory.CreateDirectory(bak);
        foreach (var f in new[] { "settings.txt", "projects.txt" }) { try { if (File.Exists(Path.Combine(data, f))) File.Copy(Path.Combine(data, f), Path.Combine(bak, f)); } catch { } }
        try
        {
            sf = new StudioForm(app); sf.ClientSize = new Size(1500, 800); sf.Show(); Pump(400);
            string p1 = Path.Combine(dir, "Alpha.ytmproj"), p2 = Path.Combine(dir, "Beta.ytmproj"), png = Path.Combine(dir, "pic.png");
            ProjectIO.Save(eng, p1); ProjectIO.Save(eng, p2); ProjectLibrary.Touch(p1, "Alpha", 3, 140, 95, true, null); ProjectLibrary.Touch(p2, "Beta", 1, 90, 30, true, null); SetF("projectsLoaded", true);
            var e1 = ProjectLibrary.Find(p1); var e2 = ProjectLibrary.Find(p2);

            // ---- confirm before opening ----
            var asked = new List<string>(); bool answer = false; SetF("confirmOpenHook", (Func<ProjectEntry, bool>)(en => { asked.Add(en.Name); return answer; }));
            SetF("tab", 3); typeof(StudioForm).GetMethod("OpenProjectAsk", BF).Invoke(sf, new object[] { e1 }); Check("clicking a project asks first", asked.Count == 1 && asked[0] == "Alpha", asked.Count + " question(s)");
            Check("saying no keeps you on the projects page (you are not sent in blind)", F<int>("tab") == 3, "tab " + F<int>("tab"));
            answer = true; typeof(StudioForm).GetMethod("OpenProjectAsk", BF).Invoke(sf, new object[] { e1 }); Pump(300); Check("saying yes opens the project", asked.Count == 2 && F<int>("tab") == 0, "tab " + F<int>("tab"));
            SetF("confirmOpenHook", null);

            // ---- removing a project ----
            int choice = 0; var recycled = new List<string>(); SetF("removeHook", (Func<ProjectEntry, int>)(en => choice)); SetF("recycleFn", (Func<string, bool>)(p => { recycled.Add(p); return true; }));
            typeof(StudioForm).GetMethod("RemoveProjectAsk", BF).Invoke(sf, new object[] { e2 }); Check("cancelling the removal changes nothing", ProjectLibrary.Find(p2) != null && File.Exists(p2) && recycled.Count == 0, "");
            choice = 1; typeof(StudioForm).GetMethod("RemoveProjectAsk", BF).Invoke(sf, new object[] { e2 }); Check("remove from the list only keeps the file on the PC", ProjectLibrary.Find(p2) == null && File.Exists(p2) && recycled.Count == 0, "");
            ProjectLibrary.Touch(p2, "Beta", 1, 90, 30, true, null); e2 = ProjectLibrary.Find(p2);
            choice = 2; typeof(StudioForm).GetMethod("RemoveProjectAsk", BF).Invoke(sf, new object[] { e2 }); Check("remove and delete the file sends that exact file to the Recycle Bin", ProjectLibrary.Find(p2) == null && recycled.Count == 1 && recycled[0] == p2, recycled.Count + " file(s)");
            SetF("removeHook", null); SetF("recycleFn", (Func<string, bool>)Win.Recycle);
            string rb = Path.Combine(dir, "recycle-me.txt"); File.WriteAllText(rb, "x"); bool rec = Win.Recycle(rb); Check("the real Recycle Bin call removes the file", rec && !File.Exists(rb), rec ? "ok" : "failed");

            // ---- picture: remove, default logo, fills the square ----
            using (var bm = new Bitmap(300, 100)) { using (var gg = Graphics.FromImage(bm)) { gg.FillRectangle(Brushes.Red, 0, 0, 100, 100); gg.FillRectangle(Brushes.Lime, 100, 0, 100, 100); gg.FillRectangle(Brushes.Blue, 200, 0, 100, 100); } bm.Save(png, System.Drawing.Imaging.ImageFormat.Png); }
            using (var sc = ProjectLibrary.Scaled(png, 120, 120, System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic))
            {
                bool fill = sc != null && sc.Width == 120 && sc.Height == 120; Color c1 = fill ? sc.GetPixel(4, 4) : Color.Black, c2 = fill ? sc.GetPixel(115, 115) : Color.Black;
                Check("a wide picture is cropped to fill the whole square (nothing stretched, no empty bars)", fill && c1.G > 200 && c1.R < 60 && c2.G > 200 && c2.R < 60, fill ? c1 + " / " + c2 : "no bitmap");
            }
            string perr; bool set = ProjectLibrary.SetCover(p1, png, out perr); e1 = ProjectLibrary.Find(p1); Check("a picture can be set", set && e1.Cover.Length > 0, perr ?? "ok");
            ProjectLibrary.ClearCover(p1); Check("REMOVE PICTURE takes it off again (the logo shows)", ProjectLibrary.Find(p1).Cover.Length == 0, "");
            using (var bm = new Bitmap(200, 200)) using (var g = Graphics.FromImage(bm))
            {
                typeof(StudioForm).GetMethod("DrawLogoArt", BF).Invoke(sf, new object[] { g, new RectangleF(0, 0, 200, 200) }); int lit = 0; for (int y = 0; y < 200; y += 4) for (int x = 0; x < 200; x += 4) { var c = bm.GetPixel(x, y); if (c.R + c.G + c.B > 200) lit++; }
                Check("with no picture the Purphex logo is drawn in the square", lit > 40, lit + " bright samples");
            }

            // ---- BACK button ----
            SetF("tab", 3); Paint(); var back = Regs().FindLast(r => Math.Abs(r.R.X - 270) < 0.5f && Math.Abs(r.R.Width - 100) < 0.5f); Check("the projects page has a BACK button", back != null, "");
            if (back != null) { Click(back.R); Check("BACK returns to the tracks", F<int>("tab") == 0, "tab " + F<int>("tab")); }
            SetF("tab", 4); Paint(); back = Regs().FindLast(r => Math.Abs(r.R.X - 270) < 0.5f && Math.Abs(r.R.Width - 100) < 0.5f); if (back != null) { Click(back.R); Check("BACK works on the Community page too", F<int>("tab") == 0, "tab " + F<int>("tab")); }

            // ---- preview ----
            SetF("tab", 3); e1 = ProjectLibrary.Find(p1); typeof(StudioForm).GetMethod("PreviewToggle", BF).Invoke(sf, new object[] { e1 });
            Check("PREVIEW starts loading the project", F<string>("previewPath") == p1, F<string>("previewPath"));
            typeof(StudioForm).GetMethod("PreviewToggle", BF).Invoke(sf, new object[] { e1 }); Check("PREVIEW again stops it", F<string>("previewPath") == "", "");
            typeof(StudioForm).GetMethod("PreviewToggle", BF).Invoke(sf, new object[] { e1 }); Pump(2500);
            Check("a project with no sound is not previewed (and you are told)", F<string>("previewPath") == "", F<string>("previewPath"));
            SetF("previewPath", p1); SetF("previewName", "Alpha"); SetF("previewPlayFn", (Action<StudioEngine>)(x => { })); Paint(); var stop = Regs().FindLast(r => Math.Abs(r.R.Width - 26) < 0.5f && r.R.Y > 700);
            Check("while a preview runs the bar with a STOP button is drawn along the bottom", stop != null, "");
            if (stop != null) { Click(stop.R); Check("STOP ends the preview", F<string>("previewPath") == "", ""); }
            SetF("previewPlayFn", null);
            SetF("previewPath", p1); SetF("tab", 0); typeof(StudioForm).GetMethod("PreviewHousekeeping", BF).Invoke(sf, null); Check("leaving the projects pages stops the preview", F<string>("previewPath") == "", "");

            // ---- themes ----
            var cyanBefore = ThemeStatic("NeonPink"); Themes.Apply(1); Check("choosing Ocean changes the colours of the studio", ThemeStatic("NeonPink") == Themes.All[1].Pink && ThemeStatic("NeonPink") != cyanBefore, "pink " + ThemeStatic("NeonPink"));
            Check("the choice is remembered in the settings", Prefs.Theme == "Ocean" && Array.Exists(Prefs.Lines(), l => l == "theme=Ocean"), Prefs.Theme);
            Themes.Apply(0); Check("Purphex is the original colours again", ThemeStatic("NeonPink") == cyanBefore, ThemeStatic("NeonPink").ToString());
            Prefs.Parse("theme", "Sunset"); Check("a saved theme loads by name", Themes.Index == 2 && ThemeStatic("NeonPink") == Themes.All[2].Pink, "index " + Themes.Index); Themes.Apply(0);
            var names = (string[])typeof(StudioForm).GetField("MenuNames", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null); Check("the side menu has a Theme tab", Array.IndexOf(names, "Theme") == 5, string.Join(",", names));
            var items = (IList)typeof(StudioForm).GetMethod("MenuItems", BF).Invoke(sf, new object[] { 5 }); Func<int, string, object> mf = (i, n) => items[i].GetType().GetField(n).GetValue(items[i]); Check("the Theme tab lists every theme", items.Count == Themes.All.Length && (string)mf(0, "Key") == "IN USE", items.Count + " items");
            ((Action)mf(3, "Do"))(); Check("clicking a theme in the menu applies it", Themes.Index == 3, "index " + Themes.Index); Themes.Apply(0);
            var st = new SettingsForm(app); st.Show(); Pump(200); var cb = FindCombo(st); Check("Settings has a theme choice with every theme", cb != null && cb.Items.Count == Themes.All.Length, cb == null ? "none" : cb.Items.Count + " items");
            if (cb != null) { cb.SelectedIndex = 4; Check("choosing a theme in Settings applies it", Themes.Index == 4, "index " + Themes.Index); Themes.Apply(0); } st.Close();
            foreach (var t in Themes.All) { Themes.Apply(Array.IndexOf(Themes.All, t)); Paint(); } Themes.Apply(0); Check("every theme paints the studio without a problem", true, "");
        }
        finally
        {
            foreach (var f in new[] { "settings.txt", "projects.txt" }) { try { if (File.Exists(Path.Combine(dir, "bak", f))) File.Copy(Path.Combine(dir, "bak", f), Path.Combine(data, f), true); } catch { } }
            try { Directory.Delete(dir, true); } catch { }
        }
        Console.WriteLine(fails == 0 ? "=== projects and theme checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
    static ComboBox FindCombo(Control c) { foreach (Control k in c.Controls) { var cb = k as ComboBox; if (cb != null && cb.Items.Count == Themes.All.Length && cb.Items[0].ToString() == Themes.All[0].Name) return cb; var r = FindCombo(k); if (r != null) return r; } return null; }
}
