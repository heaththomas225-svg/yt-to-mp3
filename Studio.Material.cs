using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// =====================================================================================================
//  THE LOOK TOKENS: one place for the accent colour and the material (finish) of the studio. Your profile colour (from the colour mixer)
//  becomes the accent of every selected button, chip, toggle, menu item, focus edge, slider, plugin window and selection outline, with its
//  finish (metallic, chrome, holographic, pearl, chameleon, candy, glitter, matte, translucent...) drawn on the selected and active parts.
//  The theme stays the background and the rest; text stays white on a colour dark enough to read it. Without a profile colour the theme
//  decides everything, as before. Moving sheens are only drawn on big surfaces and never in Performance mode or with Windows animations off.
// =====================================================================================================
static class UiTokens
{
    public static bool Custom; public static Color Accent = Color.FromArgb(255, 150, 70, 230), Second = Color.Empty; public static int Finish; public static int Version = 1;
    public static bool ReducedMotion { get { try { return Prefs.PerfMode || !SystemInformation.UIEffectsEnabled; } catch { return Prefs.PerfMode; } } }
    public static bool Shiny { get { return Custom && Finish != MixColours.Plain; } }
    public static bool Glass { get { return Custom && (Finish == MixColours.Translucent || Finish == MixColours.Frost); } }
    // the text colour on the accent (white unless the accent is very light)
    public static Color AccentText { get { return MixColours.Luma(Accent) > 0.72 ? Color.FromArgb(255, 22, 12, 34) : Color.White; } }
    // the colour a selected / active thing uses: your accent, or the theme's own when you have none
    public static Color Sel(Color themeColour) { return Custom ? Accent : themeColour; }
}

partial class StudioForm
{
    // the profile colour and its finish become the studio's accent (called at start, when the profile colour changes, and after a theme change)
    internal static void ApplyProfileTokens(bool custom, Color accent, int finish, Color second)
    {
        var t = Themes.Current; UiTokens.Custom = custom && !accent.IsEmpty; UiTokens.Accent = UiTokens.Custom ? accent : t.Accent; UiTokens.Finish = UiTokens.Custom ? finish : MixColours.Plain; UiTokens.Second = second; UiTokens.Version++;
        if (UiTokens.Custom) { Accent = MixLegibleAccent(accent); Win.NeonHot = accent; } else { Accent = t.Accent; Win.NeonHot = t.Cyan; }
        Sprites.Clear();
    }
    static Color MixLegibleAccent(Color c) { for (int i = 0; i < 8 && MixColours.Luma(c) < 0.30; i++) c = MixColours.Mix(c, Color.White, 0.15f); return c; }   // an accent that still shows on the dark pages
    internal void ApplyProfileLook()
    {
        MixEnsure(); bool custom = mixFollow && ProfileStore.Accent >= 0;
        ApplyProfileTokens(custom, custom ? ProfileStore.ColorOf(ProfileStore.Accent) : Color.Empty, custom ? MixedKind : MixColours.Plain, custom ? MixedSecond : Color.Empty);
    }
    bool lookApplied;

    // the material drawn over a shape that is already filled with its colour (clipped to the shape)
    internal void PaintMaterial(Graphics g, RectangleF r, float rad, Color c, int finish, bool animate)
    {
        if (r.Width < 3 || r.Height < 3 || finish == MixColours.Plain) return; var gs = g.Save(); var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var p = RR(r, rad))
        {
            g.SetClip(p, CombineMode.Intersect); float ph = animate && !UiTokens.ReducedMotion ? (float)((Environment.TickCount % 4000) / 4000.0) : 0.35f;
            Func<Color, float, Color> sh = (col, k) => k >= 0 ? MixColours.Mix(col, Color.White, k) : MixColours.Mix(col, Color.Black, -k);
            switch (finish)
            {
                case MixColours.Metal: case MixColours.Brushed: case MixColours.Worn:   // bands of light and dark across the surface (brushed: fine lines too)
                    { var cb = new ColorBlend(6) { Colors = new[] { Color.FromArgb(120, 255, 255, 255), Color.FromArgb(20, 255, 255, 255), Color.FromArgb(70, 0, 0, 0), Color.FromArgb(70, 255, 255, 255), Color.FromArgb(40, 0, 0, 0), Color.FromArgb(60, 255, 255, 255) }, Positions = new[] { 0f, 0.22f, 0.45f, 0.62f, 0.82f, 1f } };
                      using (var br = new LinearGradientBrush(r, Color.White, Color.Black, 90f)) { br.InterpolationColors = cb; g.FillPath(br, p); }
                      if (finish != MixColours.Metal) for (float y = r.Y + 1; y < r.Bottom; y += 2f) using (var pen = new Pen(Color.FromArgb(((int)(y * 7) & 1) == 0 ? 18 : 8, 255, 255, 255))) g.DrawLine(pen, r.X, y, r.Right, y); }
                    break;
                case MixColours.Chrome:   // a mirror: bright sky, a dark horizon, light ground
                    { var cb = new ColorBlend(5) { Colors = new[] { Color.FromArgb(150, 255, 255, 255), Color.FromArgb(60, 200, 225, 255), Color.FromArgb(120, 10, 14, 24), Color.FromArgb(110, 255, 255, 255), Color.FromArgb(50, 255, 255, 255) }, Positions = new[] { 0f, 0.42f, 0.5f, 0.56f, 1f } };
                      using (var br = new LinearGradientBrush(r, Color.White, Color.Black, 90f)) { br.InterpolationColors = cb; g.FillPath(br, p); } }
                    break;
                case MixColours.Holo: case MixColours.Pearl:   // a soft rainbow sheen that slides across (pearl: paler)
                    { int a = finish == MixColours.Holo ? 95 : 60; float w = Math.Max(r.Width, 80) * 2; var rr = new RectangleF(r.X - w * ph, r.Y, w, r.Height);
                      var cb = new ColorBlend(7) { Colors = new[] { Color.FromArgb(a, 255, 120, 210), Color.FromArgb(a, 255, 225, 130), Color.FromArgb(a, 140, 255, 170), Color.FromArgb(a, 120, 215, 255), Color.FromArgb(a, 190, 140, 255), Color.FromArgb(a, 255, 120, 210), Color.FromArgb(a, 255, 225, 130) }, Positions = new[] { 0f, 0.17f, 0.33f, 0.5f, 0.67f, 0.83f, 1f } };
                      using (var br = new LinearGradientBrush(rr, Color.White, Color.Black, 25f) { WrapMode = WrapMode.TileFlipX }) { br.InterpolationColors = cb; g.FillPath(br, p); }
                      using (var br = new LinearGradientBrush(r, Color.FromArgb(70, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f)) g.FillPath(br, p); }
                    break;
                case MixColours.Chameleon:   // one colour sliding into the other
                    { var s2 = UiTokens.Second.IsEmpty ? ThemeDef.Hsl((c.GetHue() + 130) % 360, 0.8, 0.5) : UiTokens.Second; using (var br = new LinearGradientBrush(r, Color.FromArgb(0, c), Color.FromArgb(170, s2), 30f)) g.FillPath(br, p); }
                    break;
                case MixColours.Candy: case MixColours.Satin:   // a deep wet colour with a bright highlight
                    using (var br = new SolidBrush(Color.FromArgb(90, 255, 255, 255))) g.FillEllipse(br, r.X + r.Width * 0.1f, r.Y + r.Height * 0.06f, r.Width * 0.6f, r.Height * 0.36f);
                    break;
                case MixColours.Glitter:   // little sparks (fixed, so they never flicker)
                    for (int i = 0; i < Math.Max(6, (int)(r.Width * r.Height / 220)); i++) { int hv = (int)(unchecked((uint)(i + 1) * 2654435761u) ^ (uint)(r.X * 31)); float px = r.X + ((hv >> 3) & 0xFF) / 255f * r.Width, py = r.Y + ((hv >> 11) & 0xFF) / 255f * r.Height; using (var br = new SolidBrush(Color.FromArgb(180, 255, 255, 255))) g.FillEllipse(br, px, py, 1.6f, 1.6f); }
                    break;
                case MixColours.Matte: case MixColours.Carbon: case MixColours.Grid:   // flat and soft
                    using (var br = new LinearGradientBrush(r, Color.FromArgb(0, 0, 0, 0), Color.FromArgb(50, 0, 0, 0), 90f)) g.FillPath(br, p);
                    break;
                case MixColours.Translucent: case MixColours.Frost:   // glass: a bright top edge and a fine light rim (the colour itself is drawn see-through)
                    using (var br = new LinearGradientBrush(r, Color.FromArgb(110, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f)) g.FillRectangle(br, r.X, r.Y, r.Width, r.Height * 0.48f);
                    using (var pen = new Pen(Color.FromArgb(170, 255, 255, 255), 1f)) g.DrawPath(pen, p);
                    break;
                case MixColours.Glow:
                    using (var br = new LinearGradientBrush(r, Color.FromArgb(60, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f)) g.FillPath(br, p);
                    break;
            }
        }
        g.SmoothingMode = sm; g.Restore(gs);
    }
    // a surface in the accent with its finish (selected chips in the inspector, plugin window title bars...)
    internal void PaintAccentSurface(Graphics g, RectangleF r, float rad, int alpha, bool animate)
    {
        Color c = UiTokens.Accent; int a = UiTokens.Glass ? Math.Min(alpha, 110) : alpha; FillRR(g, Color.FromArgb(a, c), r, rad);
        if (UiTokens.Shiny) PaintMaterial(g, r, rad, c, UiTokens.Finish, animate);
    }
}
