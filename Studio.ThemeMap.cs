using System;
using System.Collections.Generic;
using System.Drawing;

// Every colour written into the studio's drawing code was chosen in the purple, pink and cyan of the original look. TM.C turns such a colour into the same colour in the colours of the theme in use:
// purples follow the theme's violet, cyans its cyan and pinks its pink (the shade and brightness stay as they were). That is how one theme choice changes every button, box, text and glow.
static class TM
{
    // the original look: where its violet, cyan and pink sit on the colour wheel
    const float DV = 262f, DC = 186f, DP = 312f;
    static float hv = DV, hc = DC, hp = DP, satK = 1f; static bool ident = true; static readonly List<float[]> epochs = new List<float[]> { new[] { DC, DV, DP, 1f } }; public static int Epoch; static readonly Dictionary<int, int> cache = new Dictionary<int, int>(); static readonly object lk = new object();

    public static void Set(Color violet, Color cyan, Color pink)
    {
        float v = Hue(violet, 0), c = Hue(cyan, v), p = Hue(pink, v); float sat = (violet.GetSaturation() + cyan.GetSaturation() + pink.GetSaturation()) / 3f, k = Math.Max(0f, Math.Min(1f, sat / 0.6f));   // a grey theme (Mono) turns every colour grey
        lock (lk) { if (Math.Abs(v - hv) < 0.5f && Math.Abs(c - hc) < 0.5f && Math.Abs(p - hp) < 0.5f && Math.Abs(k - satK) < 0.01f) return; hv = v; hc = c; hp = p; satK = k; epochs.Add(new[] { c, v, p, k }); Epoch = epochs.Count - 1; ident = Math.Abs(v - DV) < 2 && Math.Abs(c - DC) < 2 && Math.Abs(p - DP) < 2 && k > 0.99f; cache.Clear(); }
    }
    static float Hue(Color c, float fallback) { return c.GetSaturation() < 0.12f ? fallback : c.GetHue(); }
    static float Arc(float from, float to, float t) { float d = ((to - from) % 360 + 540) % 360 - 180; return ((from + d * t) % 360 + 360) % 360; }   // along the shorter way round the wheel
    static float Warp(float h)
    {
        if (h < 170f || h > 345f) return h;
        if (h < DC) return Arc(170f, hc, (h - 170f) / (DC - 170f));
        if (h < DV) return Arc(hc, hv, (h - DC) / (DV - DC));
        if (h < DP) return Arc(hv, hp, (h - DV) / (DP - DV));
        return Arc(hp, 345f, (h - DP) / (345f - DP));
    }
    // a colour made under an earlier theme (epoch) shown in the theme in use now: first back to the original look, then into the current colours
    static float Unwarp(float h, float[] A)
    {
        if (h < 170f || h > 345f) return h; float[] td = { 170f, A[0], A[1], A[2], 345f }, dd = { 170f, DC, DV, DP, 345f };
        for (int k = 0; k < 4; k++)
        {
            float d = ((td[k + 1] - td[k]) % 360 + 540) % 360 - 180, e = ((h - td[k]) % 360 + 540) % 360 - 180;
            if (Math.Abs(d) < 0.01f) { if (Math.Abs(e) < 0.5f) return dd[k]; continue; }
            if (Math.Sign(d) == Math.Sign(e) || e == 0) { float f = e / d; if (f >= -0.001f && f <= 1.001f) return dd[k] + (dd[k + 1] - dd[k]) * Math.Max(0f, Math.Min(1f, f)); }
        }
        return h;
    }
    // a colour as it was in the original look (before any theme moved it), and back into the theme in use: moving a colour through ToDesign and FromDesign always gives the same answer, however many themes it went through
    public static Color ToDesign(Color c, int fromEpoch)
    {
        if (c.A == 0 || fromEpoch <= 0 || fromEpoch >= epochs.Count) return c; float s = c.GetSaturation(), l = c.GetBrightness(); if (s < 0.015f) return c;
        float design, kFrom; bool same; lock (lk) { var A = epochs[fromEpoch]; same = Math.Abs(A[0] - DC) < 0.5f && Math.Abs(A[1] - DV) < 0.5f && Math.Abs(A[2] - DP) < 0.5f && (A.Length < 4 || A[3] > 0.99f); design = Unwarp(c.GetHue(), A); kFrom = A.Length > 3 ? A[3] : 1f; } if (same) return c;   // that theme was the original look: nothing to undo
        float s0 = kFrom > 0.01f ? Math.Min(1f, s / kFrom) : s;
        var n = ThemeDef.Hsl(design, s0, l); return Color.FromArgb(c.A, n.R, n.G, n.B);
    }
    public static Color FromDesign(Color d)
    {
        if (d.A == 0 || (ident && Math.Abs(satK - 1f) < 0.01f)) return d; float s = d.GetSaturation(), l = d.GetBrightness(); if (s < 0.015f) return d; float nh = ident ? d.GetHue() : Warp(d.GetHue());
        var n = ThemeDef.Hsl(nh, Math.Min(1f, s * satK), l); return Color.FromArgb(d.A, n.R, n.G, n.B);
    }
    public static Color Remap(Color c, int fromEpoch)
    {
        if (c.A == 0 || fromEpoch == Epoch || fromEpoch < 0 || fromEpoch >= epochs.Count) return c; float s = c.GetSaturation(), l = c.GetBrightness(); if (s < 0.015f) return c;
        float design, kFrom; lock (lk) { design = Unwarp(c.GetHue(), epochs[fromEpoch]); kFrom = epochs[fromEpoch].Length > 3 ? epochs[fromEpoch][3] : 1f; } float nh = ident ? design : Warp(design); float s0 = kFrom > 0.01f ? Math.Min(1f, s / kFrom) : s; var n = ThemeDef.Hsl(nh, Math.Min(1f, s0 * satK), l); return Color.FromArgb(c.A, n.R, n.G, n.B);
    }
    public static Color C(int a, int r, int g, int b)
    {
        if (ident) return Color.FromArgb(a, r, g, b);
        int key = (r << 16) | (g << 8) | b, res;
        lock (lk)
        {
            if (!cache.TryGetValue(key, out res))
            {
                var c = Color.FromArgb(r, g, b); float s = c.GetSaturation(), l = c.GetBrightness(), h = c.GetHue();
                if (s < 0.1f) res = key; else { var n = ThemeDef.Hsl(Warp(h), s * satK, l); res = (n.R << 16) | (n.G << 8) | n.B; }
                if (cache.Count > 4000) cache.Clear(); cache[key] = res;
            }
        }
        return Color.FromArgb(a, (res >> 16) & 255, (res >> 8) & 255, res & 255);
    }
    public static Color C(int r, int g, int b) { return C(255, r, g, b); }
}
