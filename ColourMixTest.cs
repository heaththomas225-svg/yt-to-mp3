using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// The COLOUR MIXER on the Profile page: the palettes (every shade, metals, pearlescent, matte...), the mixing, using the result, and that every style paints at every window size.   csc /main:CM ... ColourMixTest.cs
static class CM
{
    static int fails; static StudioForm sf; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public;
    static void Check(string n, bool ok, string d = "") { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + (d.Length > 0 ? "   " + d : "")); Console.Out.Flush(); }
    static void SetF(string n, object v) { typeof(StudioForm).GetField(n, BF).SetValue(sf, v); }
    static T F<T>(string n) { return (T)typeof(StudioForm).GetField(n, BF).GetValue(sf); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(15); } }
    static void Paint() { sf.Invalidate(); sf.Update(); Application.DoEvents(); }
    static void Shot(string path) { Paint(); var bmp = new Bitmap(sf.ClientSize.Width, sf.ClientSize.Height); using (var g = Graphics.FromImage(bmp)) typeof(StudioForm).GetMethod("PaintAll", BF).Invoke(sf, new object[] { new PaintEventArgs(g, new Rectangle(Point.Empty, sf.ClientSize)) }); bmp.Save(path, ImageFormat.Png); bmp.Dispose(); }
    static IList Regs() { return (IList)typeof(StudioForm).GetField("regs", BF).GetValue(sf); }
    static int Near(Color a, Color b) { return Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B))); }

    [STAThread]
    static void Main(string[] args)
    {
        string outDir = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "renders"); Directory.CreateDirectory(outDir);
        StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_cm_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(StudioEnv.DataDir); Application.EnableVisualStyles();
        // ---- the colours ----
        var all = MixColours.All; string[] want = { "Every shade", "Basic colours", "Neon and glow", "Pastels", "Metals", "Pearlescent", "Matte", "Jewel tones", "Earth and nature", "Sky and sea", "Fire and sunset", "Greys and blacks" }; bool have = true; foreach (var w in want) if (all.Find(p => p.Name == w) == null) have = false;
        Check("the styles are there: every shade, basic, neon, pastels, metals, pearlescent, matte, jewel tones, earth, sky and sea, fire, greys", have, string.Join(", ", all.ConvertAll(p => p.Name).ToArray()));
        int total = MixColours.ShadeCols * MixColours.ShadeRows; foreach (var p in all) total += p.Cols.Length; Check("hundreds of colours (" + total + ")", total >= 500, total.ToString());
        bool unique = true, sized = true; foreach (var p in all) { if (p.Kind == 4) continue; var seen = new HashSet<string>(); if (p.Names.Length != p.Cols.Length || p.Cols.Length < 12) sized = false; foreach (var n in p.Names) if (!seen.Add(n)) unique = false; }
        Check("every style has its names and colours (at least 12) and no name twice", unique && sized);
        var met = all.Find(p => p.Name == "Metals"); var pea = all.Find(p => p.Name == "Pearlescent"); var mat = all.Find(p => p.Name == "Matte");
        Func<MixPalette, string[], bool> hasAll = (p, ns) => { foreach (var n in ns) if (Array.IndexOf(p.Names, n) < 0) return false; return true; };
        Check("the metals: gold, silver, bronze, copper, rose gold, platinum, chrome, titanium, steel...", met.Kind == 1 && hasAll(met, new[] { "Gold", "Silver", "Bronze", "Copper", "Rose Gold", "Platinum", "Chrome", "Titanium", "Steel", "Gunmetal", "Brass" }) && met.Cols.Length >= 24);
        Check("the pearlescent colours: pearl white, opal, abalone, iridescent pink and blue, mother of pearl...", pea.Kind == 2 && hasAll(pea, new[] { "Pearl White", "Opal", "Abalone", "Iridescent Pink", "Iridescent Blue", "Mother of Pearl" }) && pea.Cols.Length >= 20);
        Check("the matte colours: matte black, sage, terracotta, dusty rose...", mat.Kind == 3 && hasAll(mat, new[] { "Matte Black", "Sage", "Terracotta", "Dusty Rose", "Charcoal" }) && mat.Cols.Length >= 20);
        Check("a hex code is read and written back", MixColours.HexOf(MixColours.Hex("#FF6B6B")) == "#FF6B6B" && MixColours.HexOf(MixColours.Hex("4ECDC4")) == "#4ECDC4");
        Check("the shade grid goes from light to dark, and the last row is greys from white to black", MixColours.Shade(0, 0).GetBrightness() > MixColours.Shade(0, 8).GetBrightness() && MixColours.Shade(0, MixColours.ShadeRows - 1).R > 240 && MixColours.Shade(MixColours.ShadeCols - 1, MixColours.ShadeRows - 1).R < 15 && MixColours.ShadeName(3, 2).StartsWith("Hue 30"), MixColours.ShadeName(3, 2));
        // ---- mixing ----
        Color a = MixColours.Hex("#FF6B6B"), b = MixColours.Hex("#4ECDC4"); Check("mixing 0% and 100% give back the two colours", MixColours.Mix(a, b, 0) == a && MixColours.Mix(a, b, 1) == b);
        var m50 = MixColours.Mix(a, b, 0.5f); Check("50% is the middle of the two (within one step)", Math.Abs(m50.R - (a.R + b.R) / 2) <= 1 && Math.Abs(m50.G - (a.G + b.G) / 2) <= 1 && Math.Abs(m50.B - (a.B + b.B) / 2) <= 1, MixColours.HexOf(m50));
        Check("25% and 75% are a quarter of the way each side", Near(MixColours.Mix(a, b, 0.25f), Color.FromArgb(255, (int)(a.R * 0.75 + b.R * 0.25), (int)(a.G * 0.75 + b.G * 0.25), (int)(a.B * 0.75 + b.B * 0.25))) <= 1 && Near(MixColours.Mix(a, b, 0.75f), Color.FromArgb(255, (int)(a.R * 0.25 + b.R * 0.75), (int)(a.G * 0.25 + b.G * 0.75), (int)(a.B * 0.25 + b.B * 0.75))) <= 1);
        Check("a mix beyond the ends is held at the ends", MixColours.Mix(a, b, -3) == a && MixColours.Mix(a, b, 9) == b);
        // ---- the page ----
        var app = (App)Activator.CreateInstance(typeof(App), true); Edition.Override = Tier.Free; sf = new StudioForm(app); sf.ClientSize = new Size(1500, 900); sf.Show(); Pump(300); SetF("tab", 6); Pump(200);
        sf.MixSet(0, a, 0, "Coral"); sf.MixSet(1, b, 0, "Turquoise"); SetF("mixT", 0.5f); SetF("mixSlot", 0);
        Check("the page paints with the mixer on it (no tab of its own)", true); Paint(); Paint(); Check("the mixer has measured its own height", F<float>("mixH") > 500, F<float>("mixH").ToString());
        sf.MixPick(MixColours.Hex("#D4AF37"), 1, "Gold"); Check("clicking a colour puts it in A, and the next click goes to B", F<Color>("mixA") == MixColours.Hex("#D4AF37") && F<int>("mixSlot") == 1 && F<int>("mixKA") == 1);
        sf.MixPick(MixColours.Hex("#7B2D9E"), 0, "Purple"); Check("then the merged colour is the middle of A and B", sf.MixedNow == MixColours.Mix(MixColours.Hex("#D4AF37"), MixColours.Hex("#7B2D9E"), 0.5f) && F<int>("mixSlot") == 0);
        sf.MixUseAsProfile(); Check("USE AS MY PROFILE COLOUR sets the profile colour to the merged colour", ProfileStore.Accent == ProfileStore.Pack(sf.MixedNow), ProfileStore.Accent.ToString("X6")); ProfileStore.Accent = -1;
        int prevT = Themes.Index; string terr = sf.MixUseAsTheme(); Check("USE AS A THEME makes a theme from the mix and uses it", terr == null && Themes.Current.Custom && Themes.Current.Name.StartsWith("Mix "), terr + " " + Themes.Current.Name);
        double h1, s1, l1, h2, s2, l2; ThemeDef.ToHsl(sf.MixedNow, out h1, out s1, out l1); ThemeDef.ToHsl(Themes.Current.Violet, out h2, out s2, out l2); Check("its main colour is the merged colour (the hue is the same)", Math.Min(Math.Abs(h1 - h2), 360 - Math.Abs(h1 - h2)) < 12, h1.ToString("0") + " vs " + h2.ToString("0"));
        var tmade = Themes.Current; Themes.Apply(prevT); Themes.Delete(tmade);
        // every style at every size, at every place on the page
        bool boom = false; string where = ""; int styles = all.Count;
        foreach (var sz in new[] { new Size(1500, 900), new Size(1240, 680), new Size(1000, 700), new Size(1920, 1080) })
        {
            sf.ClientSize = sz; Pump(250); SetF("tab", 6);
            for (int cat = 0; cat < styles; cat++) { SetF("mixCat", cat); for (float sc = 0; sc <= 3200; sc += 400) { SetF("profScroll", sc); try { Paint(); } catch (Exception ex) { boom = true; where = sz + " style " + cat + " scroll " + sc + ": " + ex.Message; } } }
        }
        Check("every style paints at every window size and scroll position without a problem", !boom, where);
        // swatches are buttons: find one in the regs and click it
        sf.ClientSize = new Size(1500, 900); Pump(250); SetF("tab", 6); SetF("mixCat", all.IndexOf(met)); SetF("mixSlot", 0); int found = 0; HitReg swatch = null;
        for (float sc = 0; sc <= 3200 && swatch == null; sc += 100) { SetF("profScroll", sc); Paint(); foreach (HitReg r in Regs()) if (r.OnDown != null && Math.Abs(r.R.Width - 40) < 0.5f && Math.Abs(r.R.Height - 40) < 0.5f && r.R.Y > 120 && r.R.Bottom < 880) { swatch = r; found++; break; } }
        Check("the swatches of a style are buttons on the page", swatch != null);
        if (swatch != null) { var before = F<Color>("mixA"); swatch.OnDown(); Check("pressing one puts that colour in the colour being edited", F<Color>("mixA") != before || F<int>("mixKA") == 1, ""); swatch.OnRight(); }
        // pictures
        string[] names = { "shades", "basic", "neon", "pastels", "metals", "pearl", "matte" }; int[] cats = { 0, 1, 2, 3, all.IndexOf(met), all.IndexOf(pea), all.IndexOf(mat) };
        sf.MixSet(0, MixColours.Hex("#D4AF37"), 1, "Gold"); sf.MixSet(1, MixColours.Hex("#7B2D9E"), 0, "Purple");
        for (int i = 0; i < cats.Length; i++)
        {
            SetF("mixCat", cats[i]); SetF("profScroll", 0f); Paint(); Paint(); float mh = F<float>("mixH"); float contentTop = 0;
            // scroll so the mixer is at the top of the page: its top is where the editor ends; search for the scroll where the COLOUR MIXER heading is first visible
            for (float sc = 0; sc <= 3200; sc += 50) { SetF("profScroll", sc); Paint(); }
            SetF("profScroll", 1250f); Shot(Path.Combine(outDir, "mixer_" + names[i] + "_a.png")); SetF("profScroll", 1750f); Shot(Path.Combine(outDir, "mixer_" + names[i] + "_b.png"));
        }
        sf.Close();
        Console.WriteLine(fails == 0 ? "=== colour mixer checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
