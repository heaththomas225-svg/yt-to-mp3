using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

// The COLOUR MIXER on the Profile page: the palettes (every shade and every finish), the two sides, the mixing, the profile following the mix, the presets, and that every style paints at every window size.   csc /main:CM ... ColourMixTest.cs
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
    static List<HitReg> Swatches() { var l = new List<HitReg>(); foreach (HitReg r in Regs()) if (r.OnDown != null && Math.Abs(r.R.Width - 36) < 0.5f && Math.Abs(r.R.Height - 36) < 0.5f && r.R.Y > 100 && r.R.Bottom < 880) l.Add(r); return l; }

    [STAThread]
    static void Main(string[] args)
    {
        string outDir = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "renders"); Directory.CreateDirectory(outDir);
        StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_cm_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(StudioEnv.DataDir); Application.EnableVisualStyles();
        // ---- the colours ----
        var all = MixColours.All; string[] want = { "Every shade", "Basic colours", "Neon and glow", "Pastels", "Metals", "Pearlescent", "Matte", "Jewel tones", "Earth and nature", "Sky and sea", "Fire and sunset", "Greys and blacks" }; bool have = true; foreach (var w in want) if (all.Find(p => p.Name == w) == null) have = false;
        Check("the styles are there: every shade, basic, neon, pastels, metals, pearlescent, matte, jewel tones, earth, sky and sea, fire, greys", have, string.Join(", ", all.ConvertAll(p => p.Name).ToArray()));
        int total = MixColours.Total; Check("more than a thousand colours (" + total + ")", total >= 1000, total.ToString());
        bool unique = true, sized = true; foreach (var p in all) { if (p.Kind == 4) continue; var seen = new HashSet<string>(); if (p.Names.Length != p.Cols.Length || p.Cols.Length < 10) sized = false; foreach (var n in p.Names) if (!seen.Add(n)) unique = false; }
        Check("every style has its names and colours (at least 10) and no name twice", unique && sized);
        var met = all.Find(p => p.Name == "Metals"); var pea = all.Find(p => p.Name == "Pearlescent"); var mat = all.Find(p => p.Name == "Matte");
        Func<MixPalette, string[], bool> hasAll = (p, ns) => { foreach (var n in ns) if (Array.IndexOf(p.Names, n) < 0) return false; return true; };
        Check("the metals: gold, silver, bronze, copper, rose gold, platinum, titanium, steel...", met.Kind == 1 && hasAll(met, new[] { "Gold", "Silver", "Bronze", "Copper", "Rose Gold", "Platinum", "Chrome Metal", "Titanium", "Steel", "Gunmetal", "Brass" }) && met.Cols.Length >= 24);
        Check("the pearlescent colours: pearl white, opal, abalone, iridescent pink and blue, mother of pearl...", pea.Kind == 2 && hasAll(pea, new[] { "Pearl White", "Opal", "Abalone", "Iridescent Pink", "Iridescent Blue", "Mother of Pearl" }) && pea.Cols.Length >= 20);
        Check("the matte colours: matte black, sage, terracotta, dusty rose...", mat.Kind == 3 && hasAll(mat, new[] { "Matte Black", "Sage", "Terracotta", "Dusty Rose", "Charcoal" }) && mat.Cols.Length >= 20);
        { var kinds = new HashSet<int>(); foreach (var p in all) kinds.Add(p.Kind); int[] need = { MixColours.Metal, MixColours.Pearl, MixColours.Matte, MixColours.Chameleon, MixColours.Candy, MixColours.Chrome, MixColours.Brushed, MixColours.Holo, MixColours.Glitter, MixColours.Carbon, MixColours.Satin, MixColours.Frost, MixColours.Worn, MixColours.Glow }; bool allK = true; foreach (int k in need) if (!kinds.Contains(k)) allK = false;
          Check("every finish is there: metallic, brushed, chrome, chameleon, candy, pearlescent, holographic, glitter, carbon, satin, matte, frosted glass, worn, neon glow", allK, string.Join(",", kinds)); }
        { var ch = all.Find(p => p.Kind == MixColours.Chameleon); bool two = ch.Cols2 != null && ch.Cols2.Length == ch.Cols.Length; foreach (var c2 in ch.Cols2 ?? new Color[0]) if (c2.IsEmpty) two = false; Check("the chameleon colours each have two colours that shift (" + ch.Cols.Length + " of them)", two && ch.Cols.Length >= 30); }
        Check("a hex code is read and written back", MixColours.HexOf(MixColours.Hex("#FF6B6B")) == "#FF6B6B" && MixColours.HexOf(MixColours.Hex("4ECDC4")) == "#4ECDC4");
        Check("the shade grid goes from light to dark, and the last row is greys from white to black", MixColours.Shade(0, 0).GetBrightness() > MixColours.Shade(0, 8).GetBrightness() && MixColours.Shade(0, MixColours.ShadeRows - 1).R > 240 && MixColours.Shade(MixColours.ShadeCols - 1, MixColours.ShadeRows - 1).R < 15 && MixColours.ShadeName(3, 2).StartsWith("Hue 30"), MixColours.ShadeName(3, 2));
        // ---- mixing ----
        Color a = MixColours.Hex("#FF6B6B"), b = MixColours.Hex("#4ECDC4"); Check("mixing 0% and 100% give back the two colours", MixColours.Mix(a, b, 0) == a && MixColours.Mix(a, b, 1) == b);
        var m50 = MixColours.Mix(a, b, 0.5f); Check("50% is the middle of the two (within one step)", Math.Abs(m50.R - (a.R + b.R) / 2) <= 1 && Math.Abs(m50.G - (a.G + b.G) / 2) <= 1 && Math.Abs(m50.B - (a.B + b.B) / 2) <= 1, MixColours.HexOf(m50));
        Check("a mix beyond the ends is held at the ends", MixColours.Mix(a, b, -3) == a && MixColours.Mix(a, b, 9) == b);
        // ---- the page ----
        var app = (App)Activator.CreateInstance(typeof(App), true); Edition.Override = Tier.Free; sf = new StudioForm(app); sf.ClientSize = new Size(1500, 900); sf.Show(); Pump(300); SetF("tab", 6); Pump(200);
        Paint(); Paint(); Check("the mixer is on the Profile page, in a box of its own (no tab), and measured its own height", F<float>("mixH") > 800, F<float>("mixH").ToString());
        Check("it starts with the profile following the mix and the name fading", F<bool>("mixFollow") && F<bool>("mixFade"));
        Color gold = MixColours.Hex("#D4AF37"), purple = MixColours.Hex("#7B2D9E");
        sf.MixSet(0, gold, 1, "Gold"); sf.MixSet(1, purple, 0, "Purple"); Check("choosing colour A and colour B sets the two sides", F<Color>("mixA") == gold && F<Color>("mixB") == purple && F<int>("mixKA") == 1);
        Check("the merged colour is the middle of A and B", sf.MixedNow == MixColours.Mix(gold, purple, 0.5f));
        Check("and it IS the profile colour at once (no button to press)", ProfileStore.Accent == ProfileStore.Pack(sf.MixedNow), ProfileStore.Accent.ToString("X6"));
        Check("the name fades from A to B (both ends readable on the dark page)", ProfileStore.NameRgb >= 0 && ProfileStore.NameRgb2 >= 0 && MixColours.Luma(ProfileStore.ColorOf(ProfileStore.NameRgb)) >= 0.38 && MixColours.Luma(ProfileStore.ColorOf(ProfileStore.NameRgb2)) >= 0.38);
        sf.MixSetT(0.25f); Check("the MIX slider moves the merged colour and the profile colour with it", sf.MixedNow == MixColours.Mix(gold, purple, 0.25f) && ProfileStore.Accent == ProfileStore.Pack(sf.MixedNow));
        sf.MixSetFade(false); Check("FADE MY NAME OFF: the name takes the merged colour", ProfileStore.NameRgb >= 0 && ProfileStore.NameRgb2 == -1); sf.MixSetFade(true); Check("and ON again fades it", ProfileStore.NameRgb2 >= 0);
        sf.MixSetFollow(false); Check("PROFILE COLOUR = MY MIX OFF: the profile goes back to the colour of the theme", ProfileStore.Accent == -1 && ProfileStore.NameRgb == -1 && ProfileStore.NameRgb2 == -1);
        sf.MixSet(1, MixColours.Hex("#4ECDC4"), 0, "Turquoise"); Check("while it is off a change does not touch the profile", ProfileStore.Accent == -1);
        sf.MixSetFollow(true); Check("and ON again, the profile is the mix again", ProfileStore.Accent == ProfileStore.Pack(sf.MixedNow));
        sf.MixSet(1, purple, 0, "Purple"); sf.MixSetT(0.5f); sf.MixSwap(); Check("SWAP flips A and B", F<Color>("mixA") == purple && F<Color>("mixB") == gold && F<int>("mixKB") == 1 && F<string>("mixNameB") == "Gold"); sf.MixSwap();
        // saved and remembered
        sf.MixFlush(); { var st = MixStore.State; Check("the mixer remembers where you left it (colours, mix, styles, switches)", st.A == gold && st.B == purple && st.NA == "Gold" && st.KA == 1 && Math.Abs(st.T - 0.5f) < 0.001f && MixStore.Follow && MixStore.Fade); MixStore.State = new MixPreset(); MixStore.Load(); Check("and reads it back from its file", MixStore.State.A == gold && MixStore.State.B == purple && MixStore.State.NB == "Purple"); }
        // ---- the presets ----
        MixStore.Presets.Clear(); var p1 = sf.MixPresetSave(); Check("SAVE COLOUR PRESET keeps the two colours, finishes and mix under a name", MixStore.Presets.Count == 1 && p1.Name == "Gold + Purple" && p1.A == gold && p1.B == purple && p1.KA == 1);
        sf.MixPresetSave(); Check("saving the same mix again does not make a copy", MixStore.Presets.Count == 1);
        sf.MixSet(0, MixColours.Hex("#00E5FF"), 15, "Neon Cyan"); sf.MixSetT(0.7f); var p2 = sf.MixPresetSave(); Check("a different mix is a second preset", MixStore.Presets.Count == 2 && p2.Name == "Neon Cyan + Purple" && Math.Abs(p2.T - 0.7f) < 0.001f);
        sf.MixPresetStep(1); Check("NEXT goes through the saved presets and loads each one", F<int>("mixPresetSel") == 0 && F<Color>("mixA") == gold && F<Color>("mixB") == purple);
        sf.MixPresetStep(1); Check("the next one", F<int>("mixPresetSel") == 1 && F<int>("mixKA") == 15 && Math.Abs(F<float>("mixT") - 0.7f) < 0.001f); sf.MixPresetStep(1); Check("it wraps round to the first", F<int>("mixPresetSel") == 0); sf.MixPresetStep(-1); Check("PREV goes back (round to the last)", F<int>("mixPresetSel") == MixStore.Presets.Count - 1);
        Check("loading a preset makes it the profile colour too", ProfileStore.Accent == ProfileStore.Pack(sf.MixedNow));
        { int n0 = MixStore.Presets.Count; MixStore.Presets.Clear(); MixStore.Load(); Check("the presets are saved in a file and come back", MixStore.Presets.Count == n0 && MixStore.Presets[0].Name == "Gold + Purple" && MixStore.Presets[1].KA == 15, MixStore.Presets.Count + " of " + n0); }
        sf.MixPresetLoad(0); int before = MixStore.Presets.Count; sf.MixPresetDelete(); Check("DELETE THIS PRESET removes the picked one", MixStore.Presets.Count == before - 1 && F<int>("mixPresetSel") == -1 && MixStore.Presets.Find(p => p.Name == "Gold + Purple") == null);
        sf.MixPresetDelete(); Check("with none picked it only says what to do", MixStore.Presets.Count == before - 1 && F<string>("mixStatus").Contains("Pick a saved preset"));
        // ---- theme and code ----
        int prevT = Themes.Index; string terr = sf.MixUseAsTheme(); Check("USE AS A THEME makes a theme from the mix and uses it", terr == null && Themes.Current.Custom && Themes.Current.Name.StartsWith("Mix "), terr + " " + Themes.Current.Name);
        double h1, s1, l1, h2, s2, l2; ThemeDef.ToHsl(sf.MixedNow, out h1, out s1, out l1); ThemeDef.ToHsl(Themes.Current.Violet, out h2, out s2, out l2); Check("its main colour is the merged colour (the hue is the same)", Math.Min(Math.Abs(h1 - h2), 360 - Math.Abs(h1 - h2)) < 12, h1.ToString("0") + " vs " + h2.ToString("0"));
        var tmade = Themes.Current; Themes.Apply(prevT); Themes.Delete(tmade);
        sf.MixCopyCode(); Check("COPY THE HEX CODE copies (or says why it could not)", F<string>("mixStatus").EndsWith("copied.") || F<string>("mixStatus").StartsWith("Could not copy"), F<string>("mixStatus"));
        Check("the old buttons are gone: there is no separate profile colour or name colour choice", typeof(StudioForm).GetMethod("MixUseAsProfile", BF) == null && typeof(StudioForm).GetMethod("MixUseAsName", BF) == null);
        // two chameleons make a chameleon; different finishes make a plain colour
        { var pch = all.Find(p => p.Kind == MixColours.Chameleon); sf.MixSet(0, pch.Cols[0], MixColours.Chameleon, pch.Names[0], pch.Cols2[0]); sf.MixSet(1, pch.Cols[1], MixColours.Chameleon, pch.Names[1], pch.Cols2[1]); var mk = typeof(StudioForm).GetProperty("MixedKind", BF); Check("two chameleons make a chameleon", (int)mk.GetValue(sf, null) == MixColours.Chameleon); sf.MixSet(1, pch.Cols[1], MixColours.Metal, "x"); Check("a chameleon and a metal make a plain colour", (int)mk.GetValue(sf, null) == 0); }
        sf.MixSet(0, gold, 1, "Gold"); sf.MixSet(1, purple, 0, "Purple");
        // ---- both sides are buttons: the left side sets A only, the right side sets B only ----
        sf.ClientSize = new Size(1500, 900); Pump(250); SetF("tab", 6); SetF("mixCatA", all.IndexOf(met)); SetF("mixCatB", all.IndexOf(pea)); List<HitReg> sw = null;
        for (float sc = 0; sc <= 3600; sc += 100) { SetF("profScroll", sc); Paint(); sw = Swatches(); if (sw.Count >= 8) break; }
        Check("the swatches of both sides are buttons on the page", sw != null && sw.Count >= 8, sw == null ? "none" : sw.Count.ToString());
        if (sw != null && sw.Count >= 8)
        {
            float mid = 750; HitReg left = null, right = null; foreach (var r in sw) { if (r.R.X < mid && (left == null || r.R.Y > left.R.Y)) left = r; if (r.R.X > mid && (right == null || r.R.Y > right.R.Y)) right = r; }
            Check("there is a palette on the left and one on the right", left != null && right != null);
            if (left != null && right != null)
            {
                var bA = F<Color>("mixA"); var bB = F<Color>("mixB"); left.OnDown(); Check("a colour from the LEFT palette sets colour A and leaves B alone", F<Color>("mixA") != bA && F<Color>("mixB") == bB && F<int>("mixKA") == 1);
                var aNow = F<Color>("mixA"); right.OnDown(); Check("a colour from the RIGHT palette sets colour B and leaves A alone", F<Color>("mixB") != bB && F<Color>("mixA") == aNow && F<int>("mixKB") == 2);
                var bB2 = F<Color>("mixB"); left.OnRight(); Check("right-click on a colour puts it on the other side", F<Color>("mixB") != bB2);
            }
        }
        // the wheel turns
        SetF("profScroll", 1700f); Paint(); float r0 = F<float>("mixWheelRot"); bool moving = false; for (int i = 0; i < 6 && !moving; i++) { Pump(250); for (float sc = 1200; sc <= 2600 && !moving; sc += 200) { SetF("profScroll", sc); Paint(); if (Math.Abs(F<float>("mixWheelRot") - r0) > 0.5f) moving = true; } }
        Check("the colour wheel turns by itself", moving, r0 + " -> " + F<float>("mixWheelRot"));
        // every style on both sides at every window size and scroll position
        bool boom = false; string where = ""; int styles = all.Count;
        foreach (var sz in new[] { new Size(1500, 900), new Size(1240, 680), new Size(1000, 700), new Size(1920, 1080) })
        {
            sf.ClientSize = sz; Pump(250); SetF("tab", 6);
            for (int cat = 0; cat < styles; cat++) { SetF("mixCatA", cat); SetF("mixCatB", (cat * 7 + 3) % styles); for (float sc = 0; sc <= 4400; sc += 550) { SetF("profScroll", sc); try { Paint(); } catch (Exception ex) { boom = true; where = sz + " style " + cat + " scroll " + sc + ": " + ex.Message; } } }
        }
        Check("every style on both sides paints at every window size and scroll position without a problem", !boom, where);
        // pictures
        sf.ClientSize = new Size(1500, 900); Pump(250); SetF("tab", 6); sf.MixSet(0, gold, 1, "Gold"); sf.MixSet(1, purple, 0, "Purple"); sf.MixPresetSave(); sf.MixSet(0, MixColours.Hex("#7A2EC8"), 5, "Cosmic", MixColours.Hex("#1FB868")); sf.MixSet(1, MixColours.Hex("#2A4FD8"), 5, "Twilight", MixColours.Hex("#9A3EC8")); sf.MixPresetSave();
        string[][] combos = { new[] { "Metals", "Pearlescent" }, new[] { "Chameleon (colour shifting)", "Candy" }, new[] { "Every shade", "Every shade" }, new[] { "Chrome and mirror", "Glitter and sparkle" } };
        for (int i = 0; i < combos.Length; i++)
        {
            SetF("mixCatA", MixColours.IndexOf(combos[i][0])); SetF("mixCatB", MixColours.IndexOf(combos[i][1]));
            foreach (float sc in new[] { 1000f, 1700f, 2400f, 3100f }) { SetF("profScroll", sc); Shot(Path.Combine(outDir, "mixer2_" + i + "_" + (int)sc + ".png")); }
        }
        sf.Close();
        Console.WriteLine(fails == 0 ? "=== colour mixer checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
