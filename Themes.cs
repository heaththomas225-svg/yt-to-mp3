using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

// Colour themes for Purphex Music (the studio, Mastering and Bass Lab): one choice changes every glow, border, button and highlight.
// A theme is just a handful of colours, so anyone can make one: it is a short text (violet=8a5cff;cyan=00e5ff;...) that can be saved to a file, imported,
// shared in Community themes and downloaded. Every theme goes through the same clean-up, so a bad pick (a white background, a colour too dull to read) is tamed.
sealed class ThemeDef
{
    public string Name = "", Blurb = "", Author = ""; public bool Custom, Rainbow;
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
        Btn = Tame(Btn, 0.30, 0.55, 0.25); BtnC = Tame(BtnC, 0.20, 0.36, 0.15); EdgeC = Tame(EdgeC, 0.38, 0.68, 0.20); return this;
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
        error = null; if (string.IsNullOrWhiteSpace(text) || text.Length > 4000) { error = "That is not a theme."; return null; }
        var vals = new Dictionary<string, Color>(); string name = fallbackName ?? "", author = "";
        foreach (var raw in text.Replace("\r", "").Split(new[] { '\n', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int i = raw.IndexOf('='); if (i < 1) continue; string k = raw.Substring(0, i).Trim().ToLowerInvariant(), v = raw.Substring(i + 1).Trim();
            if (k == "name") { name = Clean(v, 28); continue; } if (k == "author") { author = Clean(v, 24); continue; }
            if (Array.IndexOf(Keys, k) < 0 && Array.IndexOf(ShortKeys, k) < 0) continue; Color c; if (!ParseHex(v, out c)) { error = "The colour for " + k + " is not a colour (use something like 8a5cff)."; return null; } vals[k] = c;
        }
        if (vals.Count < 3) { error = "A theme needs at least three colours (for example primary, secondary and background)."; return null; }
        ThemeDef t; Color p0, s0, t0, b0;
        bool shortForm = vals.TryGetValue("primary", out p0); vals.TryGetValue("secondary", out s0); vals.TryGetValue("tertiary", out t0); bool hasBg = vals.TryGetValue("background", out b0);
        if (shortForm) t = FromBase(p0, vals.ContainsKey("secondary") ? s0 : ThemeDef.Hsl(Hue(p0) + 165, 0.9, 0.56), vals.ContainsKey("tertiary") ? t0 : ThemeDef.Hsl(Hue(p0) - 45, 0.9, 0.62), hasBg ? (Color?)b0 : null);
        else { Color v0; double h = vals.TryGetValue("violet", out v0) ? Hue(v0) : (vals.TryGetValue("accent", out v0) ? Hue(v0) : 270); t = FromHue("", "", h); }
        foreach (var k in Keys) { Color c; if (vals.TryGetValue(k, out c)) t.SetKey(k, c); }
        t.Name = name.Length > 0 ? name : "My theme"; t.Author = author; t.Custom = true; t.Blurb = author.Length > 0 ? "By " + author + "." : "A custom theme."; return t.Cleaned();
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
        list.Add(new ThemeDef("Purphex", "The original neon purple, cyan and pink.", new[] { 150,90,255, 0,229,255, 255,43,214, 120,255,120, 178,108,255, 236,92,226, 118,52,212, 70,40,120, 140,85,210, 18,10,34, 11,6,22, 15,8,29, 7,4,16, 18,8,36 }));
        list.Add(new ThemeDef("Ocean", "Cool blues and teal, like a night sea.", new[] { 70,140,255, 0,229,255, 0,200,170, 120,255,200, 100,170,255, 40,210,200, 28,84,190, 24,56,110, 60,120,210, 8,14,32, 5,9,22, 8,13,29, 3,7,18, 7,16,38 }));
        list.Add(new ThemeDef("Sunset", "Hot orange, amber and pink.", new[] { 255,130,70, 255,205,70, 255,70,130, 255,230,120, 255,160,100, 255,90,140, 200,80,40, 112,50,40, 215,115,80, 32,13,13, 22,8,9, 29,11,11, 16,6,6, 38,14,14 }));
        list.Add(new ThemeDef("Toxic", "Acid green and lime on black.", new[] { 80,225,110, 190,255,60, 0,255,200, 190,255,60, 120,240,130, 200,255,80, 40,150,60, 30,84,44, 80,200,110, 8,24,12, 5,16,8, 8,21,11, 3,12,6, 8,30,14 }));
        list.Add(new ThemeDef("Crimson", "Deep red with a hot pink edge.", new[] { 255,70,90, 255,170,180, 255,40,160, 255,150,150, 255,105,125, 255,70,160, 190,40,62, 104,30,46, 210,70,92, 28,8,12, 19,5,8, 25,8,11, 14,4,6, 34,9,14 }));
        list.Add(new ThemeDef("Mono", "Clean white and grey, no colour at all.", new[] { 205,205,220, 255,255,255, 170,170,190, 220,220,230, 215,215,230, 165,165,185, 92,92,112, 52,52,66, 140,140,160, 16,16,20, 10,10,13, 14,14,18, 6,6,9, 20,20,26 }));
        // one theme for every colour of the rainbow, and a rainbow that moves
        list.Add(ThemeDef.FromHue("Red", "A clean, strong red.", 2)); list.Add(ThemeDef.FromHue("Orange", "Warm orange.", 26)); list.Add(ThemeDef.FromHue("Yellow", "Bright golden yellow.", 48)); list.Add(ThemeDef.FromHue("Green", "Fresh green.", 128));
        list.Add(ThemeDef.FromHue("Teal", "Cool sea teal.", 178)); list.Add(ThemeDef.FromHue("Blue", "Deep electric blue.", 214)); list.Add(ThemeDef.FromHue("Indigo", "Midnight indigo.", 244)); list.Add(ThemeDef.FromHue("Violet", "Rich violet.", 275)); list.Add(ThemeDef.FromHue("Pink", "Soft rose pink.", 328));
        var rb = ThemeDef.FromHue("Rainbow", "Every colour of the rainbow, slowly moving.", 0); rb.Rainbow = true; list.Add(rb);
        LoadCustom();
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
    static int IndexOfName(string name) { for (int i = 0; i < list.Count; i++) if (string.Equals(list[i].Name, name, StringComparison.OrdinalIgnoreCase)) return i; return -1; }
    public static int IndexOf(string name) { Ensure(); int i = IndexOfName(name); return i < 0 ? 0 : i; }

    // choose a theme: every window of the program repaints in the new colours at once
    public static void Apply(int i)
    {
        Ensure(); Index = Math.Max(0, Math.Min(list.Count - 1, i)); var t = Current; rainbowT0 = Environment.TickCount / 1000.0; Push(t); Prefs.Theme = t.Name; UserBackground.Version++;
        foreach (Form f in Application.OpenForms) { try { f.Invalidate(true); } catch { } }
    }
    public static void ApplyByName(string name) { Ensure(); Index = IndexOf(name); Push(Current); }
    static void Push(ThemeDef t) { StudioForm.ApplyTheme(t); MasterForm.ApplyTheme(t); SynthCanvas.ApplyTheme(t); }
    // the Rainbow theme slowly turns through every colour (called once per frame by the studio)
    public static bool Tick()
    {
        if (!loaded || Index < 0 || Index >= list.Count || !list[Index].Rainbow) return false;
        double h = ((Environment.TickCount / 1000.0 - rainbowT0) * 22) % 360; var t = ThemeDef.FromHue("Rainbow", list[Index].Blurb, h).Cleaned(); t.Rainbow = true; Push(t); return true;
    }

    // ---- custom themes: import, export, delete, share ----
    public static string Import(string text, string fallbackName, out ThemeDef theme)
    {
        Ensure(); string err; theme = ThemeDef.FromText(text, fallbackName, out err); if (theme == null) return err;
        string name = theme.Name; if (name.Length == 0) name = "My theme"; int n = 2; string baseName = name; while (IndexOfName(name) >= 0 && !list[IndexOfName(name)].Custom) name = baseName + " " + n++;
        theme.Name = name; int existing = IndexOfName(name);
        try { Directory.CreateDirectory(Dir); File.WriteAllText(Path.Combine(Dir, SafeFile(name) + ".purphextheme"), ToFile(theme), Encoding.UTF8); } catch (Exception ex) { return "Could not save the theme: " + ex.Message; }
        if (existing >= 0) list[existing] = theme; else list.Add(theme); return null;
    }
    static string SafeFile(string n) { var sb = new StringBuilder(); foreach (char c in n) sb.Append(char.IsLetterOrDigit(c) || c == ' ' || c == '-' ? c : '_'); string s = sb.ToString().Trim(); return s.Length == 0 ? "theme" : s; }
    public static string ToFile(ThemeDef t) { return "# Purphex Music theme: one colour per line (rrggbb). Share it, edit it, import it.\nname=" + t.Name + "\n" + (t.Author.Length > 0 ? "author=" + t.Author + "\n" : "") + t.ToText().Replace(";", "\n") + "\n"; }
    public static bool Delete(ThemeDef t)
    {
        Ensure(); if (!t.Custom) return false; int i = list.IndexOf(t); if (i < 0) return false; bool wasCurrent = i == Index;
        try { string f = Path.Combine(Dir, SafeFile(t.Name) + ".purphextheme"); if (File.Exists(f)) File.Delete(f); } catch { }
        list.RemoveAt(i); if (wasCurrent) Apply(0); else if (Index > i) Index--; return true;
    }
}
