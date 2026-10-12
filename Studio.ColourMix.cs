using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

// The COLOUR MIXER on the Profile page (a box of its own, not a tab): pick two colours from every shade, neon, pastel, metals, pearlescent, matte, jewel tones and more
// (a drop bar chooses the style), see the colour they make together (a gradient, five steps, a colour wheel, swatch pairs), and use the result as your profile colour or as a theme.
sealed class MixPalette { public string Name; public int Kind; public string[] Names; public Color[] Cols; }   // Kind: 0 plain, 1 metal, 2 pearlescent, 3 matte, 4 every shade (a grid)

static class MixColours
{
    public static Color Hex(string h) { h = h.TrimStart('#'); return Color.FromArgb(255, Convert.ToInt32(h.Substring(0, 2), 16), Convert.ToInt32(h.Substring(2, 2), 16), Convert.ToInt32(h.Substring(4, 2), 16)); }
    public static string HexOf(Color c) { return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2"); }
    public static Color Mix(Color a, Color b, float t) { t = Math.Max(0f, Math.Min(1f, t)); return Color.FromArgb(255, (int)Math.Round(a.R + (b.R - a.R) * t), (int)Math.Round(a.G + (b.G - a.G) * t), (int)Math.Round(a.B + (b.B - a.B) * t)); }
    static MixPalette P(string name, int kind, string spec)
    {
        var names = new List<string>(); var cols = new List<Color>(); foreach (string it in spec.Split('|')) { int i = it.LastIndexOf('#'); if (i < 1) continue; names.Add(it.Substring(0, i).Trim()); cols.Add(Hex(it.Substring(i + 1))); }
        return new MixPalette { Name = name, Kind = kind, Names = names.ToArray(), Cols = cols.ToArray() };
    }
    // the grid of every shade: 36 hues (every 10 degrees) from the lightest to the darkest, then a row of greys
    public const int ShadeCols = 36, ShadeRows = 10;
    static readonly double[] ShadeL = { 0.93, 0.84, 0.74, 0.64, 0.54, 0.45, 0.36, 0.27, 0.18 };
    public static Color Shade(int col, int row) { if (row >= ShadeL.Length) { double g = 1.0 - col / (double)(ShadeCols - 1); return ThemeDef.Hsl(0, 0, g); } return ThemeDef.Hsl(col * 10, 0.85, ShadeL[row]); }
    public static string ShadeName(int col, int row) { if (row >= ShadeL.Length) return "Grey " + (int)Math.Round((1.0 - col / (double)(ShadeCols - 1)) * 100) + "%"; return "Hue " + (col * 10) + ", " + (int)Math.Round(ShadeL[row] * 100) + "% light"; }

    public static readonly List<MixPalette> All = new List<MixPalette>
    {
        new MixPalette { Name = "Every shade", Kind = 4, Names = new string[0], Cols = new Color[0] },
        P("Basic colours", 0, "Red#E53935|Crimson#DC143C|Scarlet#FF2400|Coral#FF6B6B|Orange#FB8C00|Amber#FFC107|Yellow#FFEB3B|Lime#A4DE02|Green#43A047|Emerald#50C878|Mint#98FF98|Teal#009688|Turquoise#4ECDC4|Cyan#00BCD4|Sky Blue#4FC3F7|Blue#1E88E5|Royal Blue#4169E1|Navy#1A237E|Indigo#3F51B5|Violet#8E24AA|Purple#6A1B9A|Magenta#FF00FF|Pink#EC407A|Hot Pink#FF69B4|Rose#FF007F|Brown#6D4C41|Black#111111|Grey#9E9E9E|White#FAFAFA"),
        P("Neon and glow", 0, "Neon Pink#FF2BD6|Neon Cyan#00E5FF|Neon Green#39FF14|Neon Yellow#F5FF00|Neon Orange#FF6700|Neon Purple#BC13FE|Neon Blue#1F51FF|Neon Red#FF073A|Laser Lime#BFFF00|Electric Violet#8F00FF|Hot Magenta#FF1DCE|Plasma#E100FF|Ultra Blue#3D5AFE|Acid Green#B0FF00|Radioactive#CCFF00|Cyber Teal#00FFC6|Flamingo#FF6EC7|Solar#FFD400|Ice#B9F2FF|Lightning#FFF200"),
        P("Pastels", 0, "Baby Pink#F8BBD0|Baby Blue#BBDEFB|Mint Cream#C8F7DC|Lavender#D1C4E9|Peach#FFCCBC|Lemon#FFF9C4|Lilac#E1BEE7|Powder Blue#B3E5FC|Sage Mist#C5E1A5|Blush#FFCDD2|Apricot#FFE0B2|Seafoam#B2DFDB|Periwinkle#C5CAE9|Cotton Candy#FFBCF2|Buttercream#FFF3C4|Sky Mist#D6F1FF|Rose Quartz#F7CAC9|Serenity#92A8D1|Pistachio#C1E1C1|Orchid Mist#EBD3F8"),
        P("Metals", 1, "Gold#D4AF37|Antique Gold#CFB53B|Champagne Gold#F7E7CE|White Gold#EFE4B0|Rose Gold#B76E79|Silver#C0C0C0|Platinum#E5E4E2|Chrome#DBE4EB|Bronze#CD7F32|Copper#B87333|Brass#B5A642|Steel#71797E|Titanium#878681|Gunmetal#2A3439|Iron#48494B|Pewter#8E9196|Nickel#727472|Zinc#B6B8B9|Aluminium#A9ACB0|Tin#C9CCD1|Cobalt#3D59AB|Tungsten#5A5D63|Carbon#2B2B2B|Mercury#D8DADC|Lead#6C6F73|Palladium#CED0DD|Iridium#9E9FA6|Black Chrome#3A3B3F"),
        P("Pearlescent", 2, "Pearl White#F8F6F0|Opal#A8C3BC|Moonstone#D9E2E6|Abalone#7FB5B5|Mother of Pearl#EAE0C8|Iridescent Pink#F4C2E7|Iridescent Blue#A9D6F5|Iridescent Lilac#D1B3F2|Aurora Green#A7F0D1|Peach Pearl#FFD9C2|Champagne Pearl#F2DFC4|Pearl Black#2E2A33|Sea Pearl#9DD6D0|Rose Pearl#F2C9D3|Violet Pearl#B8A9E0|Gold Pearl#F0D98C|Silver Pearl#D6D9E0|Midnight Pearl#1F2748|Candy Red Pearl#E0415B|Candy Blue Pearl#3D7BE8|Ice Pearl#DCEFF7|Sunset Pearl#F6B28B|Mint Pearl#BFEBD8|Plum Pearl#8E5C9B"),
        P("Matte", 3, "Matte Black#1C1C1E|Charcoal#36393F|Graphite#3B3C3E|Slate#5B6670|Stone#8A8D8F|Sand#CBB994|Sage#8DA290|Olive#6B7048|Terracotta#C0674A|Clay#B07A5F|Dusty Rose#C9908F|Mauve#A98AA3|Matte Navy#1F2A44|Matte Forest#2F4A3A|Matte Burgundy#5E1F2B|Matte Mustard#C49A2C|Matte Teal#2F6F73|Cream#EFE7D6|Bone#E3DAC9|Cocoa#5A3E36|Matte White#EDEDED|Matte Grey#6E7074|Matte Blue#3E5C76|Matte Green#4F6F52|Matte Red#8C2F39|Matte Orange#C8702A|Matte Plum#5B3A5E|Putty#B8AFA3"),
        P("Jewel tones", 0, "Ruby#9B111E|Emerald#046307|Sapphire#0F52BA|Amethyst#9966CC|Topaz#FFC87C|Garnet#733635|Jade#00A86B|Onyx#353839|Opal Blue#2B5F75|Peridot#E6E200|Aquamarine#7FFFD4|Citrine#E4D00A|Tanzanite#4A5FA5|Rhodolite#B03060|Turquoise Stone#30D5C8|Lapis Lazuli#26619C|Tourmaline#B8336A|Moonstone Blue#7A9BB5"),
        P("Earth and nature", 0, "Moss#8A9A5B|Fern#4F7942|Pine#01796F|Bark#5C4033|Mud#70543E|Rust#B7410E|Ochre#CC7722|Sandstone#C2B280|Dune#E0C9A6|Clay Red#A5522E|Slate Rock#708090|River Stone#8B8C89|Lichen#A9BA9D|Heather#B7A6C3|Wheat#F5DEB3|Autumn#C0562F|Forest#228B22|Deep Moss#3B4A2F"),
        P("Sky and sea", 0, "Sky#87CEEB|Deep Sky#00BFFF|Ocean#006994|Deep Sea#003B5C|Lagoon#1FB5B5|Surf#7ADBE3|Cloud#E8EEF2|Storm#4B5A66|Dusk#5B4B8A|Twilight#3E3768|Midnight#101A3A|Dawn#F2B6A0|Tide#5BA3B0|Glacier#CFEFF5"),
        P("Fire and sunset", 0, "Flame#FF4500|Ember#E25822|Lava#CF1020|Sunset Orange#FF7A3D|Sunset Pink#FF8FA3|Sunset Purple#9B5DE5|Golden Hour#FFB347|Molten#FF6A00|Magma#B22222|Ash#4A4A4A|Cinder#7A3E2B|Blaze#FF5E00|Spark#FFD166|Phoenix#E8590C"),
        P("Greys and blacks", 0, "Pure White#FFFFFF|Snow#F5F5F5|Mist#E0E0E0|Fog#CFCFCF|Silver Grey#BDBDBD|Ash Grey#9E9E9E|Smoke#757575|Steel Grey#616161|Charcoal Grey#424242|Coal#2D2D2D|Ink#1A1A1A|Jet#0D0D0D|Pure Black#000000|Blue Grey#607D8B")
    };
    // the pairs of the reference sheet
    public static readonly string[][] Pairs = { new[] { "Orange + Blue", "#FF9F43", "#0ABDE3" }, new[] { "Yellow + Purple", "#FFE600", "#7B2D9E" }, new[] { "Green + Red", "#43A047", "#E53935" } };
    public static double Luma(Color c) { return (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0; }
}

partial class StudioForm
{
    Color mixA = Color.FromArgb(255, 255, 107, 107), mixB = Color.FromArgb(255, 78, 205, 196); int mixKA = 0, mixKB = 0, mixSlot = 0, mixCat = 1; float mixT = 0.5f, mixH = 900f; string mixNameA = "Coral", mixNameB = "Turquoise", mixHover = "", mixStatus = "";
    public Color MixedNow { get { return MixColours.Mix(mixA, mixB, mixT); } }
    internal void MixSet(int slot, Color c, int kind, string name) { if (slot == 0) { mixA = c; mixKA = kind; mixNameA = name; } else { mixB = c; mixKB = kind; mixNameB = name; } }
    internal void MixPick(Color c, int kind, string name) { MixSet(mixSlot, c, kind, name); mixSlot = 1 - mixSlot; mixStatus = ""; }   // the next click goes to the other colour

    // one swatch in the style of its family: plain, shiny metal, pearlescent (a soft rainbow sheen) or matte
    void MixSwatch(Graphics g, RectangleF r, Color c, int kind, float rad)
    {
        if (r.Width < 2 || r.Height < 2) return;
        using (var p = RR(r, rad))
        {
            using (var br = new SolidBrush(c)) g.FillPath(br, p);
            var gs = g.Save(); g.SetClip(p, CombineMode.Intersect);
            if (kind == 1)
            {   // metal: bands of light and dark across the surface
                Func<float, Color> sh = t => t >= 0 ? MixC(c, Color.White, t) : MixC(c, Color.Black, -t);
                var cb = new ColorBlend(6) { Colors = new[] { sh(0.55f), sh(0.10f), sh(-0.30f), sh(0.30f), sh(-0.12f), sh(0.22f) }, Positions = new[] { 0f, 0.22f, 0.45f, 0.62f, 0.82f, 1f } };
                using (var br = new LinearGradientBrush(r, c, c, 35f)) { br.InterpolationColors = cb; g.FillPath(br, p); }
            }
            else if (kind == 2)
            {   // pearlescent: a pale base with a soft sheen of pink, blue and mint moving across it
                var cb = new ColorBlend(5) { Colors = new[] { Color.FromArgb(120, 255, 190, 240), Color.FromArgb(0, 255, 255, 255), Color.FromArgb(110, 170, 220, 255), Color.FromArgb(0, 255, 255, 255), Color.FromArgb(120, 190, 255, 220) }, Positions = new[] { 0f, 0.28f, 0.5f, 0.72f, 1f } };
                using (var br = new LinearGradientBrush(r, c, c, 40f)) { br.InterpolationColors = cb; g.FillPath(br, p); }
                using (var br = new LinearGradientBrush(r, Color.FromArgb(120, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f)) g.FillPath(br, p);
            }
            else if (kind == 3)
            {   // matte: flat and soft, no shine
                using (var br = new LinearGradientBrush(r, Color.FromArgb(0, 0, 0, 0), Color.FromArgb(46, 0, 0, 0), 90f)) g.FillPath(br, p);
            }
            g.Restore(gs);
            using (var pen = new Pen(Color.FromArgb(kind == 3 ? 70 : 130, 255, 255, 255), 1f)) g.DrawPath(pen, p);
        }
    }
    static string MixTextName(Color c) { return MixColours.Luma(c) > 0.6 ? "dark" : "light"; }
    void MixText(Graphics g, string s, float px, Color on, RectangleF r, bool bold) { Txt(g, s, px, MixColours.Luma(on) > 0.6 ? Color.FromArgb(255, 20, 12, 36) : Color.FromArgb(255, 250, 244, 255), r, StringAlignment.Center, bold); }

    // one coloured circle with its label and code (A, B or the merged colour)
    void MixCircle(Graphics g, RectangleF r, Color c, int kind, string title, string name, bool selected, Action click)
    {
        var gs = g.Save(); using (var p = new GraphicsPath()) { p.AddEllipse(r); g.SetClip(p, CombineMode.Intersect); }
        MixSwatch(g, r, c, kind, r.Width / 2f); g.Restore(gs);
        using (var pen = new Pen(selected ? NeonCyan : Color.FromArgb(230, 255, 255, 255), selected ? 3.4f : 2.2f)) g.DrawEllipse(pen, r); if (selected) using (var pen = new Pen(Color.FromArgb(80, NeonCyan), 9f)) g.DrawEllipse(pen, RectangleF.Inflate(r, 4, 4));
        float fs = Math.Max(10, r.Width * 0.115f); MixText(g, title, fs + 2, c, Rc(r.X, r.Y + r.Height * 0.22f, r.Width, fs + 8), true); MixText(g, MixColours.HexOf(c), fs, c, Rc(r.X, r.Y + r.Height * 0.22f + fs + 8, r.Width, fs + 6), false);
        MixText(g, name.Length > 18 ? name.Substring(0, 17) + "." : name, Math.Max(9, fs - 2), c, Rc(r.X, r.Y + r.Height * 0.22f + 2 * fs + 12, r.Width, fs + 4), false);
        if (click != null) { Reg(r, click); }
    }
    void MixArrow(Graphics g, RectangleF r, Color from, Color to, bool pointRight, string label)
    {
        float ah = Math.Min(r.Height, 54f), cy = r.Y + r.Height / 2, head = ah * 0.7f;
        var pts = pointRight ? new[] { new PointF(r.X, cy - ah * 0.28f), new PointF(r.Right - head, cy - ah * 0.28f), new PointF(r.Right - head, cy - ah * 0.5f), new PointF(r.Right, cy), new PointF(r.Right - head, cy + ah * 0.5f), new PointF(r.Right - head, cy + ah * 0.28f), new PointF(r.X, cy + ah * 0.28f) }
                              : new[] { new PointF(r.Right, cy - ah * 0.28f), new PointF(r.X + head, cy - ah * 0.28f), new PointF(r.X + head, cy - ah * 0.5f), new PointF(r.X, cy), new PointF(r.X + head, cy + ah * 0.5f), new PointF(r.X + head, cy + ah * 0.28f), new PointF(r.Right, cy + ah * 0.28f) };
        using (var br = new LinearGradientBrush(r, pointRight ? from : to, pointRight ? to : from, 0f)) g.FillPolygon(br, pts);
        using (var pen = new Pen(Color.FromArgb(120, 255, 255, 255), 1f)) g.DrawPolygon(pen, pts);
        Txt(g, label, 11, Color.White, Rc(r.X, cy - 9, r.Width - head * 0.4f, 18), StringAlignment.Center, true);
    }
    // the colour wheel: the hues in a ring, with arrows from the middle to colour A, colour B and the colour they make together
    void MixWheel(Graphics g, RectangleF area)
    {
        float R = Math.Min(area.Width, area.Height) / 2f - 4, cx = area.X + area.Width / 2f, cy = area.Y + area.Height / 2f; float th = Math.Max(10, R * 0.28f); var ring = new RectangleF(cx - R + th / 2, cy - R + th / 2, 2 * R - th, 2 * R - th);
        var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        for (int i = 0; i < 36; i++) using (var pen = new Pen(ThemeDef.Hsl(i * 10, 0.85, 0.52), th)) g.DrawArc(pen, ring, i * 10 - 90 - 0.6f, 10.8f);
        Func<Color, float> ang = c => (float)((c.GetHue() - 90) * Math.PI / 180);
        Action<Color, string, bool> marker = (c, letter, arrow) =>
        {
            float a = ang(c), ir = R - th / 2; var mp = new PointF(cx + (float)Math.Cos(a) * ir, cy + (float)Math.Sin(a) * ir);
            if (arrow) using (var pen = new Pen(Color.FromArgb(240, 255, 255, 255), 2.4f) { EndCap = LineCap.ArrowAnchor }) g.DrawLine(pen, cx, cy, cx + (float)Math.Cos(a) * (R - th - 6), cy + (float)Math.Sin(a) * (R - th - 6));
            var dot = new RectangleF(mp.X - 11, mp.Y - 11, 22, 22); using (var br = new SolidBrush(c)) g.FillEllipse(br, dot); using (var pen = new Pen(Color.White, 2f)) g.DrawEllipse(pen, dot); Txt(g, letter, 11, MixColours.Luma(c) > 0.6 ? Color.Black : Color.White, dot, StringAlignment.Center, true);
        };
        marker(MixedNow, "M", true); marker(mixA, "A", true); marker(mixB, "B", true);
        using (var br = new SolidBrush(Color.FromArgb(255, 14, 8, 30))) g.FillEllipse(br, cx - 5, cy - 5, 10, 10); g.SmoothingMode = sm;
    }

    void ShowMixMenu(RectangleF at)
    {
        var m = Win.NewMenu();
        for (int i = 0; i < MixColours.All.Count; i++) { int k = i; var p = MixColours.All[i]; var it = new ToolStripMenuItem(p.Name + (p.Kind == 4 ? "  (" + MixColours.ShadeCols * MixColours.ShadeRows + ")" : "  (" + p.Cols.Length + ")")) { Checked = mixCat == i }; it.Click += (a, b) => { mixCat = k; }; m.Items.Add(it); }
        m.Show(this, new Point((int)at.X, (int)at.Bottom));
    }
    internal void MixUseAsProfile() { var c = MixedNow; ProfileStore.Accent = ProfileStore.Pack(c); palSeen = ProfileStore.Accent; PalCommit(); mixStatus = "Your profile colour is now " + MixColours.HexOf(c) + "."; Toast(mixStatus); }
    internal string MixUseAsTheme()
    {
        Color m = MixedNow; double h, s, l; ThemeDef.ToHsl(m, out h, out s, out l); string bg = MixColours.HexOf(ThemeDef.Hsl(h, 0.5, 0.05)).Substring(1);
        string text = "name=Mix " + MixColours.HexOf(m).Substring(1) + "\nviolet=" + MixColours.HexOf(m).Substring(1) + "\ncyan=" + MixColours.HexOf(mixB).Substring(1) + "\npink=" + MixColours.HexOf(mixA).Substring(1) + "\nbg0=" + bg;
        ThemeDef t; string err = Themes.Import(text, "Mix", out t); if (err != null) { Toast(err); return err; } int prev = Themes.Index; Themes.Apply(Themes.IndexOf(t.Name)); Prefs.Changed();
        mixStatus = "Theme made from your mix and in use: " + t.Name + "."; Toast(mixStatus); OfferRevert("Theme " + t.Name + " is on", () => { Themes.Apply(prev); Prefs.Changed(); }); return null;
    }

    // the box itself; returns the y under it. vt / vb: the part of the page that is on screen (the rest is not drawn and has no buttons)
    float PaintColourMixer(Graphics g, float x, float y, float w, float vt, float vb)
    {
        var box = Rc(x, y, w, mixH); using (var p = RR(box, 18)) { using (var br = new SolidBrush(TM.C(255, 12, 6, 28))) g.FillPath(br, p); } Win.DrawNeonBox(g, box, false);
        float px = x + 18, pw = w - 36, yy = y + 14; Func<float, float, bool> vis = (a, b) => b > vt - 6 && a < vb + 6;
        Txt(g, "COLOUR MIXER", 18, Ink, Rc(px, yy, 400, 28), StringAlignment.Near, true); yy += 32;
        TxtWrap(g, "Pick two colours (click one in the list below), see the colour they make together, and use it as your profile colour or as a theme for the whole program.", 12, DimC, Rc(px, yy, pw, 36)); yy += 44;
        // ---- A + B = merged, like the reference sheet ----
        var merged = MixedNow; float dA = Math.Max(72, Math.Min(124, pw * 0.11f)), dM = dA * 1.22f, gapA = Math.Max(50, Math.Min(170, pw * 0.12f)), rowW = dA + gapA + dM + gapA + dA, rx = px + (pw - rowW) / 2, rowH = dM, cyC = yy + rowH / 2;
        if (vis(yy, yy + rowH))
        {
            MixCircle(g, Rc(rx, cyC - dA / 2, dA, dA), mixA, mixKA, "COLOUR A", mixNameA, mixSlot == 0, () => mixSlot = 0);
            MixArrow(g, Rc(rx + dA + 6, cyC - 27, gapA - 12, 54), mixA, merged, true, (int)Math.Round((1 - mixT) * 100) + "% A");
            MixCircle(g, Rc(rx + dA + gapA, yy, dM, dM), merged, 0, "MERGED", "A + B", false, null);
            MixArrow(g, Rc(rx + dA + gapA + dM + 6, cyC - 27, gapA - 12, 54), merged, mixB, false, (int)Math.Round(mixT * 100) + "% B");
            MixCircle(g, Rc(rx + dA + gapA + dM + gapA, cyC - dA / 2, dA, dA), mixB, mixKB, "COLOUR B", mixNameB, mixSlot == 1, () => mixSlot = 1);
        }
        yy += rowH + 14;
        // ---- which colour the next click changes, the mix, swap ----
        if (vis(yy, yy + 34))
        {
            float cw0 = 128; var ca = Rc(px, yy, cw0, 32); var cb = Rc(ca.Right + 8, yy, cw0, 32); Chip(g, ca, "EDIT COLOUR A", mixSlot == 0, () => mixSlot = 0, 1); Chip(g, cb, "EDIT COLOUR B", mixSlot == 1, () => mixSlot = 1, 1); Hint2(ca, "The next colour you click goes in A"); Hint2(cb, "The next colour you click goes in B");
            var sw = Rc(cb.Right + 8, yy, 88, 32); Btn(g, sw, "SWAP", 0, () => { var tc = mixA; int tk = mixKA; string tn = mixNameA; mixA = mixB; mixKA = mixKB; mixNameA = mixNameB; mixB = tc; mixKB = tk; mixNameB = tn; }, 12); Hint2(sw, "Swap colour A and colour B");
            float lx = sw.Right + 18; Txt(g, "MIX", 12, DimC, Rc(lx, yy, 40, 32), StringAlignment.Near, true); var sr = Rc(lx + 44, yy + 4, Math.Max(80, px + pw - lx - 44 - 150), 24); Slider(g, sr, mixT, f => mixT = (float)Math.Round(f * 100) / 100f, merged); Hint2(sr, "How much of colour B is in the mix (left is all A, right is all B)");
            Txt(g, (int)Math.Round((1 - mixT) * 100) + "% A / " + (int)Math.Round(mixT * 100) + "% B", 12, Ink, Rc(sr.Right + 10, yy, 140, 32), StringAlignment.Near, true);
        }
        yy += 46;
        // ---- the style drop bar and the colours ----
        using (var pen = new Pen(Color.FromArgb(70, NeonViolet))) g.DrawLine(pen, px, yy, px + pw, yy); yy += 10;
        var pal = MixColours.All[Math.Max(0, Math.Min(MixColours.All.Count - 1, mixCat))];
        if (vis(yy, yy + 34))
        {
            Txt(g, "STYLE", 12, DimC, Rc(px, yy, 60, 32), StringAlignment.Near, true); var dd = Rc(px + 62, yy, 310, 32); Btn(g, dd, pal.Name.ToUpper() + "   v", 1, () => ShowMixMenu(dd), 13); Hint2(dd, "Choose the style of colours: every shade, neon, pastel, metals, pearlescent, matte, jewel tones and more");
            Txt(g, pal.Kind == 4 ? MixColours.ShadeCols * MixColours.ShadeRows + " shades: across is the colour, down goes from light to dark, the last row is grey" : pal.Cols.Length + " colours. Click one: it goes in the colour you are editing, then the other.", 12, DimC, Rc(dd.Right + 14, yy, Math.Max(100, px + pw - dd.Right - 14), 32), StringAlignment.Near, false);
        }
        yy += 42; mixHover = "";
        if (pal.Kind == 4)
        {
            float cell = Math.Max(10, (pw - (MixColours.ShadeCols - 1) * 2f) / MixColours.ShadeCols), step = cell + 2;
            for (int r = 0; r < MixColours.ShadeRows; r++) { float ry = yy + r * step; if (!vis(ry, ry + cell)) continue; for (int c = 0; c < MixColours.ShadeCols; c++) { var cr = Rc(px + c * step, ry, cell, cell); Color col = MixColours.Shade(c, r); string nm = MixColours.ShadeName(c, r); using (var br = new SolidBrush(col)) g.FillRectangle(br, cr); if (Hot(cr)) { mixHover = nm + "  " + MixColours.HexOf(col); using (var pen = new Pen(Color.White, 2f)) g.DrawRectangle(pen, cr.X, cr.Y, cr.Width, cr.Height); } int cc = c, rr2 = r; var h = Reg(cr, () => MixPick(MixColours.Shade(cc, rr2), 0, MixColours.ShadeName(cc, rr2))); h.OnRight = () => { MixSet(1 - mixSlot, MixColours.Shade(cc, rr2), 0, MixColours.ShadeName(cc, rr2)); }; } }
            yy += MixColours.ShadeRows * step + 6;
        }
        else
        {
            float cell = 40, gap = 8; int cols = Math.Max(4, (int)((pw + gap) / (cell + gap))); int rows = (pal.Cols.Length + cols - 1) / cols;
            for (int i = 0; i < pal.Cols.Length; i++)
            {
                float ry = yy + (i / cols) * (cell + gap); if (!vis(ry, ry + cell)) continue; var cr = Rc(px + (i % cols) * (cell + gap), ry, cell, cell); int k = i; MixSwatch(g, cr, pal.Cols[i], pal.Kind, 9);
                if (Hot(cr)) { mixHover = pal.Names[i] + "  " + MixColours.HexOf(pal.Cols[i]); using (var p = RR(RectangleF.Inflate(cr, 2, 2), 10)) using (var pen = new Pen(Color.White, 2.2f)) g.DrawPath(pen, p); }
                var h = Reg(cr, () => MixPick(pal.Cols[k], pal.Kind, pal.Names[k])); h.OnRight = () => MixSet(1 - mixSlot, pal.Cols[k], pal.Kind, pal.Names[k]);
            }
            yy += rows * (cell + gap) + 2;
        }
        Txt(g, mixHover.Length > 0 ? mixHover : "Point at a colour to see its name and code. Right-click puts it in the other colour.", 12, mixHover.Length > 0 ? Ink : DimC, Rc(px, yy, pw, 22), StringAlignment.Near, mixHover.Length > 0); yy += 32;
        using (var pen = new Pen(Color.FromArgb(70, NeonViolet))) g.DrawLine(pen, px, yy, px + pw, yy); yy += 12;
        // ---- the mixing guide: gradient and steps, the wheel, the pairs ----
        bool three = pw >= 900; float colW = three ? (pw - 2 * 24) / 3f : pw; float y1 = yy, y2 = yy, y3 = yy; float c1x = px, c2x = three ? px + colW + 24 : px, c3x = three ? px + 2 * (colW + 24) : px; float blockTop = yy;
        // gradient + steps
        {
            float by = blockTop; Txt(g, "HORIZONTAL GRADIENT MIX", 12, DimC, Rc(c1x, by, colW, 18), StringAlignment.Near, true); by += 22; var gr = Rc(c1x, by, colW, 36);
            if (vis(gr.Y, gr.Bottom)) { using (var p = RR(gr, 6)) { using (var br = new LinearGradientBrush(gr, mixA, mixB, 0f)) g.FillPath(br, p); using (var pen = new Pen(Color.FromArgb(150, 255, 255, 255), 1f)) g.DrawPath(pen, p); } }
            by += 42; Txt(g, MixColours.HexOf(mixA), 10, DimC, Rc(c1x, by, 90, 16), StringAlignment.Near, false); Txt(g, "LINEAR GRADIENT BLEND (A TO B)", 10, DimC, Rc(c1x + 90, by, colW - 180, 16), StringAlignment.Center, false); Txt(g, MixColours.HexOf(mixB), 10, DimC, Rc(c1x + colW - 90, by, 90, 16), StringAlignment.Far, false); by += 28;
            Txt(g, "STEPPED COLOUR BLEND", 12, DimC, Rc(c1x, by, colW, 18), StringAlignment.Near, true); by += 24; float sw = (colW - 4 * 8) / 5f; string[] lb = { "100% A", "75% A / 25% B", "50% A / 50% B", "25% A / 75% B", "100% B" };
            for (int i = 0; i < 5; i++) { var sr = Rc(c1x + i * (sw + 8), by + 18, sw, 44); Color sc = MixColours.Mix(mixA, mixB, i / 4f); if (vis(sr.Y, sr.Bottom)) { Txt(g, lb[i], 9, DimC, Rc(sr.X - 6, by, sr.Width + 12, 16), StringAlignment.Center, false); using (var p = RR(sr, 6)) { using (var br = new SolidBrush(sc)) g.FillPath(br, p); using (var pen = new Pen(Color.FromArgb(150, 255, 255, 255), 1f)) g.DrawPath(pen, p); } Txt(g, MixColours.HexOf(sc), 9, DimC, Rc(sr.X - 6, sr.Bottom + 3, sr.Width + 12, 14), StringAlignment.Center, false); var hh = Reg(sr, () => { }); } }
            y1 = by + 18 + 44 + 22;
        }
        // wheel
        { float by = three ? blockTop : y1 + 6; Txt(g, "COLOUR WHEEL APPLICATION", 12, DimC, Rc(c2x, by, colW, 18), StringAlignment.Near, true); by += 24; float ws = Math.Min(colW, 200f); var wr = Rc(c2x + (colW - ws) / 2, by, ws, ws); if (vis(wr.Y, wr.Bottom)) MixWheel(g, wr); y2 = by + ws + 8; }
        // pairs
        {
            float by = three ? blockTop : y2 + 6; Txt(g, "COLOUR SWATCH VARIATIONS", 12, DimC, Rc(c3x, by, colW, 18), StringAlignment.Near, true); by += 24;
            Action<string, Color, Color, Action> pair = (title, a, b, click) =>
            {
                var row = Rc(c3x, by, colW, 58); if (vis(row.Y, row.Bottom))
                {
                    Color mm = MixColours.Mix(a, b, 0.5f); float s = 46, gx = (colW - 3 * s - 2 * 22) / 2f; Txt(g, title, 11, Ink, Rc(c3x, by - 2, colW, 14), StringAlignment.Near, true); float sy = by + 14;
                    Func<float, Color, string, RectangleF> sq = (ox, col, nm) => { var r0 = Rc(c3x + ox, sy, s, s - 14); using (var p = RR(r0, 5)) { using (var br = new SolidBrush(col)) g.FillPath(br, p); using (var pen = new Pen(Color.FromArgb(150, 255, 255, 255), 1f)) g.DrawPath(pen, p); } Txt(g, MixColours.HexOf(col), 8, DimC, Rc(c3x + ox - 8, sy + s - 13, s + 16, 12), StringAlignment.Center, false); return r0; };
                    sq(0, a, ""); Txt(g, "+", 14, Ink, Rc(c3x + s, sy, 22, s - 14), StringAlignment.Center, true); sq(s + 22, b, ""); Txt(g, "=", 14, Ink, Rc(c3x + 2 * s + 22, sy, 22, s - 14), StringAlignment.Center, true); sq(2 * s + 44, mm, "");
                    if (click != null) { var h = Reg(row, click); Hint2(row, "Use this pair as colour A and B"); }
                }
                by += 62;
            };
            pair("Your mix (A + B)", mixA, mixB, null);
            foreach (var pr in MixColours.Pairs) { var p0 = pr; pair(p0[0], MixColours.Hex(p0[1]), MixColours.Hex(p0[2]), () => { MixSet(0, MixColours.Hex(p0[1]), 0, p0[0].Split('+')[0].Trim()); MixSet(1, MixColours.Hex(p0[2]), 0, p0[0].Split('+')[1].Trim()); mixT = 0.5f; }); }
            y3 = by;
        }
        yy = Math.Max(three ? Math.Max(y1, Math.Max(y2, y3)) : y3, yy) + 8;
        // ---- use it ----
        using (var pen = new Pen(Color.FromArgb(70, NeonViolet))) g.DrawLine(pen, px, yy, px + pw, yy); yy += 12;
        if (vis(yy, yy + 40))
        {
            float bw = Math.Min(280, (pw - 2 * 10) / 3f); var u1 = Rc(px, yy, bw, 38); var u2 = Rc(u1.Right + 10, yy, bw, 38); var u3 = Rc(u2.Right + 10, yy, bw, 38);
            Btn(g, u1, "USE AS MY PROFILE COLOUR", 2, () => MixUseAsProfile(), 12); Hint2(u1, "Your profile (banner, rings, accents) takes the merged colour");
            Btn(g, u2, "USE AS A THEME", 1, () => MixUseAsTheme(), 12); Hint2(u2, "Make a theme for the whole program from your mix (the merged colour, A and B). REVERT puts the old one back");
            Btn(g, u3, "COPY THE CODE", 0, () => { try { Clipboard.SetText(MixColours.HexOf(MixedNow)); mixStatus = MixColours.HexOf(MixedNow) + " copied."; Toast(mixStatus); } catch { } }, 12); Hint2(u3, "Copy the code of the merged colour (like #A79CC4)");
        }
        yy += 46; if (mixStatus.Length > 0) { Txt(g, mixStatus, 12, Ink, Rc(px, yy, pw, 20), StringAlignment.Near, false); yy += 24; }
        mixH = Math.Max(300, yy + 10 - y); return y + mixH;
    }
}
