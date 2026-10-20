using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// Pictures of buttons and glows that are drawn once and then just copied onto the screen (drawing a button from scratch 100 times a frame was most of the lag).
static class Sprites
{
    static readonly Dictionary<long, Bitmap> cache = new Dictionary<long, Bitmap>();
    public static long H(long h, long v) { unchecked { return (h ^ v) * 1099511628211L; } }
    public static long H(long h, string s) { unchecked { for (int i = 0; i < s.Length; i++) h = (h ^ s[i]) * 1099511628211L; return h; } }
    public static long Seed { get { return unchecked((long)14695981039346656037UL); } }
    public static Bitmap Get(long key) { Bitmap b; return cache.TryGetValue(key, out b) ? b : null; }
    public static void Put(long key, Bitmap b) { if (cache.Count > 700) Clear(); cache[key] = b; }
    // a cleared picture may still be about to be drawn, so it is disposed one Clear later (long after); before, they were left to the finalizer and native memory kept climbing
    static List<Bitmap> retired = new List<Bitmap>();
    public static int Count { get { return cache.Count; } } public static void Clear() { foreach (var b in retired) { try { b.Dispose(); } catch { } } retired = new List<Bitmap>(cache.Values); cache.Clear(); }
    public static Bitmap Make(int w, int h) { return new Bitmap(Math.Max(1, w), Math.Max(1, h), System.Drawing.Imaging.PixelFormat.Format32bppPArgb); }
}

partial class StudioForm
{
    const int SpritePad = 10;
    // pictures (the 512 pixel logo shown at 30 to 160 pixels, covers, photos) are shrunk with the best filter: no shimmering or jagged picture edges (CompositingQuality.HighQuality was tried: 7 times slower)
    static void Sharp(Graphics g) { g.InterpolationMode = InterpolationMode.HighQualityBicubic; }
    long ThemeKey() { long h = Sprites.H(Sprites.H(Sprites.Seed, UiTokens.Version), UiTokens.Finish); foreach (var c in new[] { Violet, BtnC, Accent, Accent2, NeonCyan, NeonViolet, NeonPink }) h = Sprites.H(h, c.ToArgb()); return Sprites.H(h, Edition.Has(Tier.Fantasy) ? 7 : 3); }   // (a button picture is kept per edition look)
    long themeKeyFrame = 0; int themeKeyAt = -1;
    long ThemeKeyNow() { int f = Environment.TickCount; if (themeKeyAt != f) { themeKeyAt = f; themeKeyFrame = ThemeKey(); } return themeKeyFrame; }

    // a button picture: fill, border, glow and label; drawn once for every look the button can have
    internal static bool DirectDraw;   // draw buttons straight onto the screen instead of from a cached picture (used when the studio is drawn scaled up, e.g. a 4K recording)
    void BtnDrawSprite(Graphics g, RectangleF r, string shown, int style, float ht, float px)
    {
        if (DirectDraw) { var gs0 = g.Save(); g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit; BtnLook(g, r, shown, style, (int)(ht * 4 + 0.5f) / 4f, px); g.Restore(gs0); return; }
        long key = Sprites.H(Sprites.H(Sprites.H(Sprites.H(Sprites.H(Sprites.H(ThemeKeyNow(), (long)Math.Round(r.Width)), (long)Math.Round(r.Height)), style), (int)(ht * 4 + 0.5f)), (long)(px * 4)), shown);
        long pt0 = Prof.T(); var sp = Sprites.Get(key);
        if (sp == null)
        {
            Prof.A("sprite.miss", 0); if (Prof.On) Prof.Note(key + "|" + ThemeKeyNow() + "|" + shown + "|" + r.Width + "x" + r.Height + "|s" + style + "|ht" + ht + "|px" + px); int w = (int)Math.Round(r.Width), h = (int)Math.Round(r.Height); sp = Sprites.Make(w + SpritePad * 2, h + SpritePad * 2);
            using (var sg = Graphics.FromImage(sp))
            {
                sg.SmoothingMode = SmoothingMode.AntiAlias; sg.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit; Sharp(sg); var q = Rc(SpritePad, SpritePad, w, h); float hq = (int)(ht * 4 + 0.5f) / 4f;
                BtnLook(sg, q, shown, style, hq, px);
            }
            Sprites.Put(key, sp);
        }
        long pt1 = Prof.T(); g.DrawImageUnscaled(sp, (int)Math.Round(r.X) - SpritePad, (int)Math.Round(r.Y) - SpritePad); Prof.A("sprite.blit", pt1);
    }
}
