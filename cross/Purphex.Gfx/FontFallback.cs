// Fonts the Windows app names (Segoe UI and its weights, Consolas) are not on macOS / Linux: they map to Inter (shipped inside the app)
// or a close system font, at the right weight, instead of whatever the system picks (often a much wider font that cuts the labels off).
using System;
using System.Collections.Generic;
using SkiaSharp;

namespace System.Drawing
{
    public static class FontFallback
    {
        static readonly Dictionary<string, SortedDictionary<int, SKTypeface>> reg = new Dictionary<string, SortedDictionary<int, SKTypeface>>(StringComparer.OrdinalIgnoreCase);
        // a font shipped with the app (the desktop app adds Inter at start)
        public static void Register(string family, int weight, SKTypeface face) { if (face == null) return; lock (reg) { SortedDictionary<int, SKTypeface> w; if (!reg.TryGetValue(family, out w)) reg[family] = w = new SortedDictionary<int, SKTypeface>(); w[weight] = face; } }
        static SKTypeface Registered(string family, int weight)
        {
            lock (reg)
            {
                SortedDictionary<int, SKTypeface> w; if (!reg.TryGetValue(family, out w) || w.Count == 0) return null;
                SKTypeface best = null; int bd = int.MaxValue; foreach (var kv in w) { int d = Math.Abs(kv.Key - weight); if (d < bd) { bd = d; best = kv.Value; } } return best;
            }
        }
        static readonly string[] Sans = { "Inter", "Helvetica Neue", "Noto Sans", "Liberation Sans", "Arimo", "Arial", "Ubuntu", "Cantarell", "DejaVu Sans" };
        static readonly string[] Condensed = { "DIN Alternate", "Barlow", "Roboto Condensed", "Inter", "Noto Sans", "Liberation Sans Narrow", "Arial", "DejaVu Sans" };   // Bahnschrift (the buttons)
        static readonly string[] Mono = { "Menlo", "SF Mono", "Noto Sans Mono", "Liberation Mono", "Ubuntu Mono", "DejaVu Sans Mono" };
        static readonly Dictionary<string, SKTypeface> cache = new Dictionary<string, SKTypeface>();

        // name as the app writes it ("Segoe UI Semibold"), bold / italic from the Font
        public static SKTypeface Resolve(string name, bool bold, bool italic)
        {
            name = name ?? ""; string key = name + "|" + bold + "|" + italic;
            lock (cache) { SKTypeface c; if (cache.TryGetValue(key, out c)) return c; }
            var r = Find(name, bold, italic); lock (cache) cache[key] = r; return r;
        }
        static SKTypeface Find(string name, bool bold, bool italic)
        {
            int weight = bold ? 700 : 400; string family = name;
            var slant = italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
            var reg0 = Registered(name, weight); if (reg0 != null) return reg0;
            var sys = SKTypeface.FromFamilyName(name, new SKFontStyle(weight, (int)SKFontStyleWidth.Normal, slant));
            if (sys != null && string.Equals(sys.FamilyName, name, StringComparison.OrdinalIgnoreCase)) return sys;   // it is on this computer
            // "Segoe UI Semibold" = Segoe UI at weight 600, and so on
            string[] list = Sans; string lower = name.ToLowerInvariant();
            bool segoe = lower.StartsWith("segoe ui"), bahn = lower.StartsWith("bahnschrift");
            if (segoe || bahn || lower == "tahoma" || lower == "verdana" || lower == "calibri" || lower == "microsoft sans serif")
            {
                if (lower.Contains("semibold")) weight = bold ? 800 : 600; else if (lower.Contains("black")) weight = 900; else if (lower.Contains("semilight")) weight = bold ? 600 : 350; else if (lower.Contains("light")) weight = bold ? 600 : 300; else if (lower.Contains("bold")) weight = 700;
                // Windows names the weights as their own families ("Segoe UI Semibold") but reports them as the base family: try that first
                string baseFam = segoe ? "Segoe UI" : bahn ? "Bahnschrift" : null;
                if (baseFam != null) { var t0 = SKTypeface.FromFamilyName(baseFam, new SKFontStyle(weight, (int)SKFontStyleWidth.Normal, slant)); if (t0 != null && string.Equals(t0.FamilyName, baseFam, StringComparison.OrdinalIgnoreCase)) return t0; }
                if (bahn) list = Condensed;
            }
            else if (lower.Contains("consolas") || lower.Contains("cascadia") || lower.Contains("courier") || lower.Contains("mono")) list = Mono;
            else return sys ?? SKTypeface.Default;   // a font the user picked (Fonts page) or a named display font: the system match
            foreach (var f in list)
            {
                var r = Registered(f, weight); if (r != null) return r;
                var t = SKTypeface.FromFamilyName(f, new SKFontStyle(weight, (int)SKFontStyleWidth.Normal, slant));
                if (t != null && string.Equals(t.FamilyName, f, StringComparison.OrdinalIgnoreCase)) return t;
            }
            return sys ?? SKTypeface.Default;
        }
    }
}
