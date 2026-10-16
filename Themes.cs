using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

// Colour themes for Purphex Music (the studio, Mastering and Bass Lab): one choice changes every glow, border, button and highlight.
// A theme is just a handful of colours, so anyone can make one: it is a short text (violet=8a5cff;cyan=00e5ff;...) that can be saved to a file, imported,
// shared in Community themes and downloaded. Every theme goes through the same clean-up, so a bad pick (a white background, a colour too dull to read) is tamed.
sealed class ThemeDef
{
    public string Name = "", Blurb = "", Author = "", Photo = "", ImageHint = ""; public bool Custom, Rainbow, Special; public Tier Req = Tier.Supporter;   // Photo: the background picture of this theme (a file on this PC), ImageHint: the picture named inside a theme file
    public ThemeDef Copy() { return (ThemeDef)MemberwiseClone(); }
    public Color Violet, Cyan, Pink, Lime, Accent, Accent2, Btn, BtnC, EdgeC, Panel, Lane, Lane2, Bg0, Bg1;
    public static readonly string[] Keys = { "violet", "cyan", "pink", "lime", "accent", "accent2", "btn", "btnc", "edge", "panel", "lane", "lane2", "bg0", "bg1" };
    public static readonly string[] ShortKeys = { "primary", "secondary", "tertiary", "background" };
    public ThemeDef() { }
    public ThemeDef(string name, string blurb, int[] c) { Name = name; Blurb = blurb; Violet = C(c, 0); Cyan = C(c, 3); Pink = C(c, 6); Lime = C(c, 9); Accent = C(c, 12); Accent2 = C(c, 15); Btn = C(c, 18); BtnC = C(c, 21); EdgeC = C(c, 24); Panel = C(c, 27); Lane = C(c, 30); Lane2 = C(c, 33); Bg0 = C(c, 36); Bg1 = C(c, 39); }
    static Color C(int[] a, int i) { return Color.FromArgb(a[i], a[i + 1], a[i + 2]); }

    // ---- colour maths ----
    public static Color Hsl(double h, double s, double l)
    {
        h = ((h % 360) + 360) % 360; s = Math.Max(0, Math.Min(1, s)); l = Math.Max(0, Math.Min(1, l)); double c = (1 - Math.Abs(2 * l - 1)) * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = l - c / 2, r = 0, g = 0, b = 0;
        if (h < 60) { r = c; g = x; } else if (h < 120) { r = x; g = c; } else if (h < 180) { g = c; b = x; } else if (h < 240) { g = x; b = c; } else if (h < 300) { r = x; b = c; } else { r = c; b = x; }
        return Color.FromArgb((int)Math.Round((r + m) * 255), (int)Math.Round((g + m) * 255), (int)Math.Round((b + m) * 255));
    }
    public static void ToHsl(Color c, out double h, out double s, out double l)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0, mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b)); l = (mx + mn) / 2; double d = mx - mn;
        if (d < 1e-9) { h = 0; s = 0; return; } s = d / (1 - Math.Abs(2 * l - 1)); if (mx == r) h = 60 * (((g - b) / d) % 6); else if (mx == g) h = 60 * ((b - r) / d + 2); else h = 60 * ((r - g) / d + 4); if (h < 0) h += 360;
    }
    static Color Tame(Color c, double minL, double maxL, double minS)
    {
        double h, s, l; ToHsl(c, out h, out s, out l); if (s > 0.12) s = Math.Max(s, minS); l = Math.Max(minL, Math.Min(maxL, l)); return Hsl(h, s, l);
    }
    // a whole palette from one hue (the same recipe for every colour theme, so they all look like they belong together)
    public static ThemeDef FromHue(string name, string blurb, double h)
    {
        return new ThemeDef { Name = name, Blurb = blurb, Violet = Hsl(h, 0.82, 0.64), Cyan = Hsl(h + 165, 0.95, 0.55), Pink = Hsl(h - 45, 0.92, 0.62), Lime = Hsl(h + 105, 0.85, 0.68), Accent = Hsl(h, 0.85, 0.70), Accent2 = Hsl(h - 35, 0.85, 0.66),
            Btn = Hsl(h, 0.62, 0.46), BtnC = Hsl(h, 0.40, 0.30), EdgeC = Hsl(h, 0.55, 0.56), Panel = Hsl(h, 0.45, 0.09), Lane = Hsl(h, 0.45, 0.05), Lane2 = Hsl(h, 0.45, 0.07), Bg0 = Hsl(h, 0.50, 0.035), Bg1 = Hsl(h, 0.50, 0.09) };
    }
    // a palette with its own four neon colours (h1 = the main one) and a background tint of its own
    public static ThemeDef FromSpec(string name, string blurb, double h1, double h2, double h3, double h4, double bgHue, double bgSat)
    {
        return new ThemeDef { Name = name, Blurb = blurb, Violet = Hsl(h1, 0.86, 0.62), Cyan = Hsl(h2, 0.92, 0.56), Pink = Hsl(h3, 0.90, 0.62), Lime = Hsl(h4, 0.85, 0.66), Accent = Hsl(h1, 0.86, 0.72), Accent2 = Hsl(h3, 0.85, 0.66),
            Btn = Hsl(h1, 0.62, 0.44), BtnC = Hsl(h1, 0.42, 0.28), EdgeC = Hsl(h1, 0.56, 0.56), Panel = Hsl(bgHue, bgSat, 0.095), Lane = Hsl(bgHue, bgSat, 0.05), Lane2 = Hsl(bgHue, bgSat, 0.07), Bg0 = Hsl(bgHue, bgSat, 0.035), Bg1 = Hsl(bgHue, bgSat, 0.11) };
    }
    // from a primary colour, an optional second and third accent and a background tint
    static ThemeDef FromBase(Color primary, Color secondary, Color tertiary, Color? background)
    {
        double h, s, l; ToHsl(primary, out h, out s, out l); var t = FromHue("", "", h); t.Violet = primary; t.Accent = Tame(primary, 0.6, 0.78, 0.5); t.Btn = Hsl(h, Math.Max(0.4, Math.Min(0.75, s)), 0.46); t.EdgeC = Hsl(h, 0.5, 0.56);
        t.Cyan = secondary; t.Accent2 = secondary; t.Pink = tertiary;
        if (background.HasValue) { double bh, bs, bl; ToHsl(background.Value, out bh, out bs, out bl); bs = Math.Min(bs, 0.6); t.Bg0 = Hsl(bh, bs, 0.035); t.Bg1 = Hsl(bh, bs, 0.09); t.Panel = Hsl(bh, bs, 0.09); t.Lane = Hsl(bh, bs, 0.05); t.Lane2 = Hsl(bh, bs, 0.07); }
        return t;
    }
    // keep a theme readable and tasteful whatever was asked for: dark surfaces, bright accents, buttons you can see
    public ThemeDef Cleaned()
    {
        Bg0 = Tame(Bg0, 0.015, 0.06, 0); Bg1 = Tame(Bg1, 0.04, 0.12, 0); Panel = Tame(Panel, 0.05, 0.13, 0); Lane = Tame(Lane, 0.02, 0.08, 0); Lane2 = Tame(Lane2, 0.03, 0.10, 0);
        Violet = Tame(Violet, 0.50, 0.80, 0.30); Cyan = Tame(Cyan, 0.50, 0.82, 0.30); Pink = Tame(Pink, 0.50, 0.82, 0.30); Lime = Tame(Lime, 0.50, 0.84, 0.30); Accent = Tame(Accent, 0.55, 0.82, 0.30); Accent2 = Tame(Accent2, 0.55, 0.82, 0.30);
        Btn = Tame(Btn, 0.30, 0.55, 0.25); BtnC = Tame(BtnC, 0.20, 0.36, 0.15); EdgeC = Tame(EdgeC, 0.38, 0.68, 0.20); return Legible();
    }
    // every writing in a theme can be read: white button text on its buttons, and its bright colours against its dark panels (a yellow theme used to have white on yellow, a blue one dark blue on navy)
    static double Lum(Color c) { Func<int, double> f = v => { double x = v / 255.0; return x <= 0.03928 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); }; return 0.2126 * f(c.R) + 0.7152 * f(c.G) + 0.0722 * f(c.B); }
    static double Con(Color a, Color b) { double l1 = Lum(a), l2 = Lum(b); if (l1 < l2) { var t = l1; l1 = l2; l2 = t; } return (l1 + 0.05) / (l2 + 0.05); }
    static Color Brighter(Color c, Color against, double min) { double h, s, l; ToHsl(c, out h, out s, out l); int n = 0; while (Con(c, against) < min && l < 0.9 && n++ < 60) { l += 0.015; c = Hsl(h, s, l); } return c; }
    static Color Darker(Color c, double min) { double h, s, l; ToHsl(c, out h, out s, out l); int n = 0; while (Con(Color.White, c) < min && l > 0.1 && n++ < 60) { l -= 0.015; c = Hsl(h, s, l); } return c; }
    public ThemeDef Legible()
    {
        Btn = Darker(Btn, 4.6); BtnC = Darker(BtnC, 7.0); Color pl = Panel.R + Panel.G + Panel.B > 0 ? Panel : Color.Black;
        Violet = Brighter(Violet, pl, 4.6); Cyan = Brighter(Cyan, pl, 4.6); Pink = Brighter(Pink, pl, 4.6); Lime = Brighter(Lime, pl, 4.6); Accent = Brighter(Accent, pl, 5.0); Accent2 = Brighter(Accent2, pl, 4.6); EdgeC = Brighter(EdgeC, pl, 3.0); return this;
    }
    public Color Get(string key)
    {
        switch (key) { case "violet": return Violet; case "cyan": return Cyan; case "pink": return Pink; case "lime": return Lime; case "accent": return Accent; case "accent2": return Accent2; case "btn": return Btn; case "btnc": return BtnC; case "edge": return EdgeC; case "panel": return Panel; case "lane": return Lane; case "lane2": return Lane2; case "bg0": return Bg0; default: return Bg1; }
    }
    void SetKey(string key, Color c)
    {
        switch (key) { case "violet": Violet = c; break; case "cyan": Cyan = c; break; case "pink": Pink = c; break; case "lime": Lime = c; break; case "accent": Accent = c; break; case "accent2": Accent2 = c; break; case "btn": Btn = c; break; case "btnc": BtnC = c; break; case "edge": EdgeC = c; break; case "panel": Panel = c; break; case "lane": Lane = c; break; case "lane2": Lane2 = c; break; case "bg0": Bg0 = c; break; default: Bg1 = c; break; }
    }
    static string Hex(Color c) { return c.R.ToString("x2") + c.G.ToString("x2") + c.B.ToString("x2"); }
    public string ToText() { var sb = new StringBuilder(); foreach (var k in Keys) { if (sb.Length > 0) sb.Append(';'); sb.Append(k).Append('=').Append(Hex(Get(k))); } return sb.ToString(); }
    static bool ParseHex(string v, out Color c)
    {
        c = Color.Black; v = (v ?? "").Trim().TrimStart('#'); if (v.Length == 3) v = "" + v[0] + v[0] + v[1] + v[1] + v[2] + v[2]; if (v.Length != 6) return false; int n;
        if (!int.TryParse(v, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out n)) return false; c = Color.FromArgb((n >> 16) & 255, (n >> 8) & 255, n & 255); return true;
    }
    // a theme from its text (key=hex pairs split by ; or new lines; name= and author= lines are allowed). null + a reason when it is not a theme.
    public static ThemeDef FromText(string text, string fallbackName, out string error)
    {
        error = null; string imageHint = ""; if (string.IsNullOrWhiteSpace(text) || text.Length > 4000) { error = "That is not a theme."; return null; }
        var vals = new Dictionary<string, Color>(); string name = fallbackName ?? "", author = "";
        foreach (var raw in text.Replace("\r", "").Split(new[] { '\n', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int i = raw.IndexOf('='); if (i < 1) continue; string k = raw.Substring(0, i).Trim().ToLowerInvariant(), v = raw.Substring(i + 1).Trim();
            if (k == "name") { name = Clean(v, 28); continue; } if (k == "author") { author = Clean(v, 24); continue; } if (k == "image") { imageHint = Path.GetFileName(v.Replace('/', Path.DirectorySeparatorChar)); continue; }
            if (Array.IndexOf(Keys, k) < 0 && Array.IndexOf(ShortKeys, k) < 0) continue; Color c; if (!ParseHex(v, out c)) { error = "The colour for " + k + " is not a colour (use something like 8a5cff)."; return null; } vals[k] = c;
        }
        if (vals.Count < 3) { error = "A theme needs at least three colours (for example primary, secondary and background)."; return null; }
        ThemeDef t; Color p0, s0, t0, b0;
        bool shortForm = vals.TryGetValue("primary", out p0); vals.TryGetValue("secondary", out s0); vals.TryGetValue("tertiary", out t0); bool hasBg = vals.TryGetValue("background", out b0);
        if (shortForm) t = FromBase(p0, vals.ContainsKey("secondary") ? s0 : ThemeDef.Hsl(Hue(p0) + 165, 0.9, 0.56), vals.ContainsKey("tertiary") ? t0 : ThemeDef.Hsl(Hue(p0) - 45, 0.9, 0.62), hasBg ? (Color?)b0 : null);
        else { Color v0; double h = vals.TryGetValue("violet", out v0) ? Hue(v0) : (vals.TryGetValue("accent", out v0) ? Hue(v0) : 270); t = FromHue("", "", h); }
        foreach (var k in Keys) { Color c; if (vals.TryGetValue(k, out c)) t.SetKey(k, c); }
        t.Name = name.Length > 0 ? name : "My theme"; t.Author = author; t.Custom = true; t.ImageHint = imageHint; t.Blurb = author.Length > 0 ? "By " + author + "." : "A custom theme."; return t.Cleaned();
    }
    static double Hue(Color c) { double h, s, l; ToHsl(c, out h, out s, out l); return h; }
    public static string Clean(string s, int max)
    {
        var sb = new StringBuilder(); foreach (char ch in s ?? "") { if (char.IsLetterOrDigit(ch) || ch == ' ' || ch == '-' || ch == '_' || ch == '.' || ch == '\'') sb.Append(ch); } string r = sb.ToString().Trim(); return r.Length > max ? r.Substring(0, max).Trim() : r;
    }
}
static class Themes
{
    static readonly List<ThemeDef> list = new List<ThemeDef>();
    public static int Index; static bool loaded; static double rainbowT0;
    public static List<ThemeDef> All { get { Ensure(); return list; } }
    public static ThemeDef Current { get { Ensure(); return list[Math.Max(0, Math.Min(list.Count - 1, Index))]; } }
    static string Dir { get { return Path.Combine(StudioEnv.DataDir, "themes"); } }

    static void Ensure()
    {
        if (loaded) return; loaded = true;
        list.Add(new ThemeDef("Purphex", "The original neon purple, cyan and pink.", new[] { 150,90,255, 0,229,255, 255,43,214, 120,255,120, 178,108,255, 236,92,226, 118,52,212, 70,40,120, 140,85,210, 18,10,34, 11,6,22, 15,8,29, 7,4,16, 18,8,36 })); list[0].Req = Tier.Free;   // the Free Edition has this one
        list.Add(new ThemeDef("Ocean", "Cool blues and teal, like a night sea.", new[] { 70,140,255, 0,229,255, 0,200,170, 120,255,200, 100,170,255, 40,210,200, 28,84,190, 24,56,110, 60,120,210, 8,14,32, 5,9,22, 8,13,29, 3,7,18, 7,16,38 }));
        list.Add(new ThemeDef("Sunset", "Hot orange, amber and pink.", new[] { 255,130,70, 255,205,70, 255,70,130, 255,230,120, 255,160,100, 255,90,140, 200,80,40, 112,50,40, 215,115,80, 32,13,13, 22,8,9, 29,11,11, 16,6,6, 38,14,14 }));
        list.Add(new ThemeDef("Mono", "Clean white and grey, no colour at all.", new[] { 205,205,220, 255,255,255, 170,170,190, 220,220,230, 215,215,230, 165,165,185, 92,92,112, 52,52,66, 140,140,160, 16,16,20, 10,10,13, 14,14,18, 6,6,9, 20,20,26 }));
        // every colour of the rainbow, each with its own four neon colours and its own tint of background (and a Rainbow that moves)
        list.Add(ThemeDef.FromSpec("Toxic", "Acid lime and hazard yellow with a purple warning glow.", 88, 62, 300, 130, 90, 0.6));
        list.Add(ThemeDef.FromSpec("Crimson", "Wine red with gold and magenta.", 345, 45, 315, 15, 340, 0.6));
        list.Add(ThemeDef.FromSpec("Red", "Fire red against electric blue.", 358, 215, 32, 55, 0, 0.65));
        list.Add(ThemeDef.FromSpec("Orange", "Orange with teal and pink sparks.", 27, 168, 340, 70, 22, 0.55));
        list.Add(ThemeDef.FromSpec("Yellow", "Golden yellow with violet.", 51, 262, 24, 100, 48, 0.5));
        list.Add(ThemeDef.FromSpec("Green", "Emerald and mint on deep forest.", 146, 172, 42, 105, 152, 0.55));
        list.Add(ThemeDef.FromSpec("Teal", "Sea teal with a pink flash.", 178, 205, 332, 140, 186, 0.55));
        list.Add(ThemeDef.FromSpec("Blue", "Electric blue on deep navy.", 218, 248, 190, 165, 226, 0.65));
        list.Add(ThemeDef.FromSpec("Indigo", "Midnight indigo with magenta.", 248, 292, 330, 205, 252, 0.6));
        list.Add(ThemeDef.FromSpec("Violet", "Rich violet and magenta.", 283, 322, 205, 140, 288, 0.55));
        list.Add(ThemeDef.FromSpec("Pink", "Rose pink with lilac and aqua.", 328, 275, 195, 155, 332, 0.5));
        var rb = ThemeDef.FromHue("Rainbow", "Every colour of the rainbow, slowly moving.", 0); rb.Rainbow = true; list.Add(rb);
        // themes of their own for each paid edition: three for Supporter, five for Fantasy (the lifetime one)
        foreach (var sp in new[] {
            ThemeDef.FromSpec("Neon Tokyo", "Hot magenta and electric cyan on midnight blue.", 322, 188, 262, 60, 236, 0.62),
            ThemeDef.FromSpec("Aurora", "Northern-light green with violet and ice.", 150, 270, 190, 95, 200, 0.5),
            ThemeDef.FromSpec("Bubblegum", "Soft pink and baby blue on deep plum.", 340, 200, 300, 170, 325, 0.6) }) { sp.Special = true; sp.Req = Tier.Supporter; list.Add(sp); }
        foreach (var fa in new[] {
            ThemeDef.FromSpec("Royal Gold", "Black and 24-carat gold with a ruby glow.", 44, 352, 38, 50, 30, 0.35),
            ThemeDef.FromSpec("Nebula", "Deep-space magenta, ice blue and starlight.", 292, 196, 330, 270, 268, 0.7),
            ThemeDef.FromSpec("Obsidian", "Glass black with steel blue and red sparks.", 215, 2, 40, 100, 220, 0.12),
            ThemeDef.FromSpec("Splatter", "The sparks of the pointer: pink, cyan, yellow and lime on black.", 330, 188, 52, 100, 280, 0.3),
            ThemeDef.FromSpec("Dragon Fire", "Molten orange with magenta and gold sparks.", 14, 300, 44, 40, 10, 0.7) }) { fa.Special = true; fa.Req = Tier.Fantasy; list.Add(fa); }
        foreach (var bt in list) bt.Legible();
        LoadCustom(); LoadPhotoMap();
    }
    static void LoadCustom()
    {
        try
        {
            if (!Directory.Exists(Dir)) return;
            foreach (var f in Directory.GetFiles(Dir, "*.purphextheme")) { try { string err; var t = ThemeDef.FromText(File.ReadAllText(f, Encoding.UTF8), Path.GetFileNameWithoutExtension(f), out err); if (t != null && IndexOfName(t.Name) < 0) list.Add(t); } catch { } }
        }
        catch { }
    }
    // the themes saved from the colour mixer of the profile (SAVE AS THEME) live in a department of their own: PROFILE THEMES
    static HashSet<string> profileSet;
    static string ProfileFile { get { return Path.Combine(StudioEnv.DataDir, "profile-themes.txt"); } }
    static HashSet<string> ProfileNames() { if (profileSet == null) { profileSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase); try { if (File.Exists(ProfileFile)) foreach (var l in File.ReadAllLines(ProfileFile, Encoding.UTF8)) if (l.Trim().Length > 0) profileSet.Add(l.Trim()); } catch { } } return profileSet; }
    public static bool IsProfile(ThemeDef t) { return t != null && t.Custom && (ProfileNames().Contains(t.Name) || t.Name.StartsWith("Mix ", StringComparison.Ordinal)); }
    public static void MarkProfile(string name) { ProfileNames().Add(name); SaveProfileNames(); }
    static void SaveProfileNames() { try { Directory.CreateDirectory(StudioEnv.DataDir); File.WriteAllText(ProfileFile, string.Join("\n", System.Linq.Enumerable.ToArray(ProfileNames())), Encoding.UTF8); } catch { } }
    static int IndexOfName(string name) { for (int i = 0; i < list.Count; i++) if (string.Equals(list[i].Name, name, StringComparison.OrdinalIgnoreCase)) return i; return -1; }
    public static int IndexOf(string name) { Ensure(); int i = IndexOfName(name); return i < 0 ? 0 : i; }

    // choose a theme: every window of the program repaints in the new colours at once
    public static void Apply(int i)
    {
        Ensure(); Index = Math.Max(0, Math.Min(list.Count - 1, i)); var t = Current; rainbowT0 = Environment.TickCount / 1000.0; Push(t); Prefs.Theme = t.Name; UserBackground.Version++;
        foreach (Form f in Application.OpenForms) { try { f.Invalidate(true); } catch { } }
        SiteTheme.Send(t);   // the website gets your colours too
    }
    public static void ApplyByName(string name) { Ensure(); Index = IndexOf(name); Push(Current); }
    // the edition decides which themes can be used: a theme above the edition is put back to the original one
    public static bool Allowed(ThemeDef t) { return Edition.Has(t.Req); }
    // the theme you chose is never forgotten: while the edition is not known yet (a licence being renewed, no internet) or lower, the original theme is shown for now,
    // and your own theme comes back by itself as soon as your edition is known again (it used to be reset to the original for good)
    static void Show(int i) { Index = Math.Max(0, Math.Min(list.Count - 1, i)); rainbowT0 = Environment.TickCount / 1000.0; Push(Current); UserBackground.Version++; foreach (Form f in Application.OpenForms) { try { f.Invalidate(true); } catch { } } }
    public static bool EnforceEdition(bool allowDowngrade = true)
    {
        Ensure(); int want = IndexOfName(Prefs.Theme);
        if (want >= 0 && want != Index && Allowed(list[want])) { Show(want); SiteTheme.Send(Current); return true; }   // your theme is back
        if (!Allowed(Current)) { if (!allowDowngrade) return false; Show(0); return true; }   // at start on a linked PC the licence may still be renewing: no flash to the original (that left the converter half in the old colours)   // shown as the original for now; your choice is kept in Prefs.Theme
        SiteTheme.Send(Current);
        // the Fantasy edition comes with the Fantasy look: the first time it is on this PC the gold Fantasy theme goes on every button and outline of the program (choose any theme you like afterwards)
        if (!StudioEnv.UnderTest && Prefs.FantasyThemeV < 2 && Edition.Current == Tier.Fantasy) { Prefs.FantasyThemed = true; Prefs.FantasyThemeV = 2; int gi = IndexOfName("Royal Gold"); if (gi >= 0 && !list[Index].Special) { Apply(gi); Prefs.Changed(); return true; } Prefs.Changed(); }
        return false;
    }
    public static string Tag(ThemeDef t) { return t.Special ? (t.Req == Tier.Fantasy ? "FANTASY" : "SUPPORTER") : ""; }
    // fromTick: the Rainbow theme moving a little; the other windows are not recoloured then (that would close an open drop-down and make every control flash), only when a theme is really chosen
    static void Push(ThemeDef t, bool fromTick = false) { if (!fromTick) Sprites.Clear(); StudioForm.ApplyTheme(t); MasterForm.ApplyTheme(t); SynthCanvas.ApplyTheme(t); PxWindow.ApplyTheme(t); if (!fromTick) { try { Win.RethemeAll(); } catch { } } }
    // the Rainbow theme slowly turns through every colour (called once per frame by the studio)
    static double lastRainbowH = -1; static int lastRainbowIdx = -2;
    public static bool Tick()
    {
        if (!loaded || Index < 0 || Index >= list.Count || !list[Index].Rainbow) return false;
        double h = Math.Floor(((Environment.TickCount / 1000.0 - rainbowT0) * 22) % 360 / 15) * 15;   // a new colour every 15 degrees (about every 0.7 s): each step rebuilds every button picture and waveform, so 3-degree steps (7 a second) made frame spikes and piles of bitmaps
        if (h == lastRainbowH && lastRainbowIdx == Index) return false; lastRainbowH = h; lastRainbowIdx = Index; var t = ThemeDef.FromHue("Rainbow", list[Index].Blurb, h).Cleaned(); t.Rainbow = true; Push(t, true); return true;
    }

    // ---- custom themes: import, export, delete, share ----
    public static string Import(string text, string fallbackName, out ThemeDef theme, string sourceFolder = null)
    {
        Ensure(); string err; theme = ThemeDef.FromText(text, fallbackName, out err); if (theme == null) return err;
        string name = theme.Name; if (name.Length == 0) name = "My theme"; int n = 2; string baseName = name; while (IndexOfName(name) >= 0 && !list[IndexOfName(name)].Custom) name = baseName + " " + n++;
        theme.Name = name; int existing = IndexOfName(name); string hint = theme.ImageHint; theme.ImageHint = "";
        try { Directory.CreateDirectory(Dir); File.WriteAllText(Path.Combine(Dir, SafeFile(name) + ".purphextheme"), ToFile(theme), Encoding.UTF8); } catch (Exception ex) { return "Could not save the theme: " + ex.Message; }
        if (existing >= 0) { theme.Photo = list[existing].Photo; list[existing] = theme; } else list.Add(theme);
        if (hint.Length > 0 && sourceFolder != null) { string pf = Path.Combine(sourceFolder, hint); if (File.Exists(pf)) { string perr = SetPhoto(theme, pf); if (perr != null) return null; } }
        try { File.WriteAllText(Path.Combine(Dir, SafeFile(theme.Name) + ".purphextheme"), ToFile(theme), Encoding.UTF8); } catch { }
        return null;
    }
    static string SafeFile(string n) { var sb = new StringBuilder(); foreach (char c in n) sb.Append(char.IsLetterOrDigit(c) || c == ' ' || c == '-' ? c : '_'); string s = sb.ToString().Trim(); return s.Length == 0 ? "theme" : s; }
    public static string ToFile(ThemeDef t) { return "# Purphex Music theme: one colour per line (rrggbb). Share it, edit it, import it.\nname=" + t.Name + "\n" + (t.Author.Length > 0 ? "author=" + t.Author + "\n" : "") + (t.Photo.Length > 0 ? "image=" + Path.GetFileName(t.Photo) + "\n" : "") + t.ToText().Replace(";", "\n") + "\n"; }
    public static bool Delete(ThemeDef t)
    {
        Ensure(); if (!t.Custom) return false; int i = list.IndexOf(t); if (i < 0) return false; bool wasCurrent = i == Index; if (ProfileNames().Remove(t.Name)) SaveProfileNames();
        try { string f = Path.Combine(Dir, SafeFile(t.Name) + ".purphextheme"); if (File.Exists(f)) File.Delete(f); } catch { }
        if (t.Photo.Length > 0) { try { File.Delete(t.Photo); } catch { } t.Photo = ""; }
        list.RemoveAt(i); if (wasCurrent) Apply(0); else if (Index > i) Index--; return true;
    }

    // ---- pictures: any photo, any size, becomes the background of the whole studio ----
    static string PhotoDir { get { return Path.Combine(Dir, "photos"); } } static string PhotoMapFile { get { return Path.Combine(Dir, "photos.txt"); } }
    static void LoadPhotoMap() { }   // (photo themes are gone: a theme is its colours only)
    static void SavePhotoMap() { try { Directory.CreateDirectory(Dir); var sb = new StringBuilder(); foreach (var t in list) if (t.Photo.Length > 0) sb.Append(t.Name).Append('|').Append(Path.GetFileName(t.Photo)).Append('\n'); File.WriteAllText(PhotoMapFile, sb.ToString(), Encoding.UTF8); } catch { } }
    // reads any picture the PC can read (JPG, PNG, BMP, GIF, TIFF), scales a huge one down to at most 3840 pixels (a small one is kept: the studio stretches it to fill), saves it as a JPEG in the theme folder
    static string SavePhoto(string imageFile, string baseName, out string error, out Color dominant, out double dominantSat)
    {
        error = null; dominant = Color.Gray; dominantSat = 0;
        try
        {
            using (var fs = new FileStream(imageFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var src = Image.FromStream(fs, false, false))
            {
                int w = src.Width, h = src.Height; if (w < 1 || h < 1) { error = "That picture is empty."; return null; }
                double k = Math.Min(1.0, 3840.0 / Math.Max(w, h)); int nw = Math.Max(1, (int)Math.Round(w * k)), nh = Math.Max(1, (int)Math.Round(h * k));
                Directory.CreateDirectory(PhotoDir); string path = Path.Combine(PhotoDir, SafeFile(baseName) + "_" + (DateTime.Now.Ticks % 100000000) + ".jpg");
                using (var bmp = new Bitmap(nw, nh, PixelFormat.Format24bppRgb))
                {
                    using (var g = Graphics.FromImage(bmp)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.Clear(Color.Black); g.DrawImage(src, 0, 0, nw, nh); }
                    DominantOf(bmp, out dominant, out dominantSat);
                    var enc = Array.Find(ImageCodecInfo.GetImageEncoders(), e => e.MimeType == "image/jpeg"); var ep = new EncoderParameters(1); ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 90L);
                    if (enc != null) bmp.Save(path, enc, ep); else bmp.Save(path, ImageFormat.Jpeg);
                }
                return path;
            }
        }
        catch (OutOfMemoryException) { error = "That file is not a picture this program can read (use JPG, PNG, BMP, GIF or TIFF)."; return null; }
        catch (ArgumentException) { error = "That file is not a picture this program can read (use JPG, PNG, BMP, GIF or TIFF)."; return null; }
        catch (Exception ex) { error = "Could not use that picture: " + ex.Message; return null; }
    }
    // the colour the picture is mostly about (the average of its colourful pixels), and how colourful it is
    static void DominantOf(Bitmap bmp, out Color c, out double sat)
    {
        c = Color.Gray; sat = 0; using (var small = new Bitmap(48, 48, PixelFormat.Format24bppRgb))
        {
            using (var g = Graphics.FromImage(small)) { g.InterpolationMode = InterpolationMode.HighQualityBilinear; g.DrawImage(bmp, 0, 0, 48, 48); }
            double sx = 0, sy = 0, wsum = 0, ssum = 0, n = 0; var hist = new double[36];
            for (int y = 0; y < 48; y++) for (int x = 0; x < 48; x++) { var p = small.GetPixel(x, y); double h, s, l; ThemeDef.ToHsl(p, out h, out s, out l); double wgt = s * (1 - Math.Abs(2 * l - 1)); sx += Math.Cos(h * Math.PI / 180) * wgt; sy += Math.Sin(h * Math.PI / 180) * wgt; wsum += wgt; ssum += s; n++; hist[((int)(h / 10) % 36 + 36) % 36] += wgt; }
            lastHues = PickHues(hist);
            sat = ssum / Math.Max(1, n); if (wsum < 1e-6) return; double ang = Math.Atan2(sy, sx) * 180 / Math.PI; if (ang < 0) ang += 360; c = ThemeDef.Hsl(ang, 0.8, 0.55);
        }
    }
    // the colours the picture is really made of: up to four hues that are at least 50 degrees apart, the strongest first (a photo of a sunset gives orange, pink and blue, not one colour)
    static double[] lastHues = new double[0];
    static double[] PickHues(double[] hist)
    {
        var sc = new double[36]; for (int i = 0; i < 36; i++) sc[i] = hist[(i + 35) % 36] * 0.5 + hist[i] + hist[(i + 1) % 36] * 0.5; var picked = new List<int>(); double top = 0; foreach (double v in sc) top = Math.Max(top, v);
        for (int k = 0; k < 4; k++)
        {
            int best = -1; double bv = 0; for (int i = 0; i < 36; i++) { bool near = false; foreach (int q in picked) { int d = Math.Abs(i - q); d = Math.Min(d, 36 - d); if (d < 5) near = true; } if (!near && sc[i] > bv) { bv = sc[i]; best = i; } }
            if (best < 0 || bv < top * 0.18) break; picked.Add(best);
        }
        var hs = new double[picked.Count]; for (int i = 0; i < hs.Length; i++) hs[i] = picked[i] * 10 + 5; return hs;
    }
    // a picture for the theme in use (or any theme): it fills the whole studio window
    public static string SetPhoto(ThemeDef t, string imageFile)
    {
        Ensure(); string err; Color dc; double ds; string saved = SavePhoto(imageFile, t.Name, out err, out dc, out ds); if (saved == null) return err;
        string old = t.Photo; t.Photo = saved; SavePhotoMap(); if (old.Length > 0 && old != saved) { try { File.Delete(old); } catch { } }
        UserBackground.Version++; return null;
    }
    public static void ClearPhoto(ThemeDef t)
    {
        Ensure(); if (t.Photo.Length == 0) return; try { File.Delete(t.Photo); } catch { } t.Photo = ""; SavePhotoMap(); UserBackground.Version++;
    }
    // import a photo as a whole new theme: its colours come from the picture, and the picture is the background
    public static string ImportPhoto(string imageFile, out ThemeDef theme)
    {
        Ensure(); theme = null; string baseName = ThemeDef.Clean(Path.GetFileNameWithoutExtension(imageFile), 28); if (baseName.Length == 0) baseName = "My photo";
        string err; Color dc; double ds; string saved = SavePhoto(imageFile, baseName, out err, out dc, out ds); if (saved == null) return err;
        string name = baseName; int n = 2; while (IndexOfName(name) >= 0) name = baseName + " " + n++;
        ThemeDef t; if (ds < 0.10) { var mono = list[IndexOfName("Mono")]; t = mono.Copy(); } else { double h, s, l; ThemeDef.ToHsl(dc, out h, out s, out l); var hs = lastHues; double h1 = hs.Length > 0 ? hs[0] : h, h2 = hs.Length > 1 ? hs[1] : h1 + 155, h3 = hs.Length > 2 ? hs[2] : h1 - 45, h4 = hs.Length > 3 ? hs[3] : h1 + 95; t = ThemeDef.FromSpec(name, "", h1, h2, h3, h4, h1, 0.5).Cleaned(); }   // the colours of the program come from the colours of the picture
        t.Name = name; t.Custom = true; t.Rainbow = false; t.Blurb = "Made from your photo."; t.Photo = saved; t.Author = "";
        try { Directory.CreateDirectory(Dir); File.WriteAllText(Path.Combine(Dir, SafeFile(name) + ".purphextheme"), ToFile(t), Encoding.UTF8); } catch (Exception ex) { return "Could not save the theme: " + ex.Message; }
        list.Add(t); SavePhotoMap(); theme = t; return null;
    }
    // the photo of the theme in use, scaled to cover a window of this size (cropped in the middle, never stretched, no bars); null when the theme has none
    static Bitmap photoSrc; static string photoSrcPath = "";
    public static Bitmap PhotoFit(int w, int h)
    {
        var t = Current; if (t == null || t.Photo.Length == 0 || !File.Exists(t.Photo) || w < 2 || h < 2) return null;
        try
        {
            if (photoSrc == null || photoSrcPath != t.Photo) { if (photoSrc != null) photoSrc.Dispose(); using (var fs = new FileStream(t.Photo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) using (var im = Image.FromStream(fs, false, false)) photoSrc = new Bitmap(im); photoSrcPath = t.Photo; }
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb); double k = Math.Max(w / (double)photoSrc.Width, h / (double)photoSrc.Height); float dw = (float)(photoSrc.Width * k), dh = (float)(photoSrc.Height * k);
            using (var g = Graphics.FromImage(bmp)) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.DrawImage(photoSrc, (w - dw) / 2f, (h - dh) / 2f, dw, dh); }
            return bmp;
        }
        catch { return null; }
    }
}
