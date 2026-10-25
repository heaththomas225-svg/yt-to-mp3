using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;

// =====================================================================================================
//  PRESET KNOBS: every preset list (vocal presets, bus presets, mastering presets, a plugin's presets) shows small knobs on each preset,
//  turned to what that preset really does, read from its own settings (not decoration):
//    vocal / bus presets: SPACE (reverb and echo), PUNCH (compression), BRIGHT (top end lifted)
//    mastering presets:   LOUD (its loudness target), PUNCH (the master compressor), BRIGHT (top end and exciter)
//    a plugin's presets:  its first three settings, where each one sits in its range
//  They keep the Purphex look like the rest of the preset windows (themes do not change them).
// =====================================================================================================
partial class StudioForm
{
    static readonly Dictionary<string, float[]> knobCache = new Dictionary<string, float[]>();
    static readonly CultureInfo KCI = CultureInfo.InvariantCulture;
    static float KNum(string body, string key) { int i = 0; while ((i = body.IndexOf(key + "=", i, StringComparison.Ordinal)) >= 0) { bool start = i == 0 || body[i - 1] == '{' || body[i - 1] == ';'; int e = body.IndexOf(';', i); string v = body.Substring(i + key.Length + 1, (e < 0 ? body.Length : e) - i - key.Length - 1).TrimEnd('}', ' '); float f; if (start && float.TryParse(v, NumberStyles.Float, KCI, out f)) return f; i += key.Length; } return float.NaN; }
    static float K01(float v) { return float.IsNaN(v) ? 0 : Math.Max(0, Math.Min(1, v)); }
    // the top end an EQ setting lifts: the gains of its bands at 3 kHz and above (dB)
    static float KHighs(string eq) { float sum = 0; for (int b = 0; b < 8; b++) { float f = KNum(eq, "b" + b + "_f"), g = KNum(eq, "b" + b + "_g"); if (!float.IsNaN(f) && !float.IsNaN(g) && f >= 3000) sum += g; } return sum; }
    static float KPunch(string comp) { float ratio = KNum(comp, "ratio"), thr = KNum(comp, "thr"); if (float.IsNaN(ratio)) return 0; return K01((ratio - 1) / 6f * 0.6f + (float.IsNaN(thr) ? 0 : (-thr - 8) / 28f * 0.4f)); }

    // SPACE, PUNCH, BRIGHT of a vocal or bus preset's chain ("eq{...} > comp{...} > reverb{mix=20}")
    internal static float[] ChainKnobs(string chain)
    {
        chain = chain ?? ""; lock (knobCache) { float[] c; if (knobCache.TryGetValue(chain, out c)) return c; }
        float space = 0, punch = 0, highs = 0;
        foreach (var part in chain.Split('>'))
        {
            string p = part.Trim(); int b = p.IndexOf('{'); if (b < 0) continue; string name = p.Substring(0, b).Trim().ToLowerInvariant(), body = p.Substring(b);
            float mix = KNum(body, "mix");
            if (name == "reverb" || name.Contains("verb") || name.Contains("space") || name.Contains("plate") || name.Contains("room")) space += float.IsNaN(mix) ? 15 : mix;
            else if (name == "delay" || name.Contains("echo")) space += (float.IsNaN(mix) ? 15 : mix) * 0.8f;
            else if (name == "chorus" || name == "widener") space += (float.IsNaN(mix) ? 10 : mix) * 0.15f;
            if (name == "comp" || name.Contains("compress") || name.Contains("dynamics")) punch = Math.Max(punch, KPunch(body));
            else if (name == "limiter" || name.Contains("clipper")) punch = Math.Max(punch, 0.55f);
            if (name == "eq") highs += KHighs(body); else if (name == "exciter" || name.Contains("air")) highs += 2.5f;
        }
        var r = new[] { K01(space / 45f), punch, K01(highs / 8f) }; lock (knobCache) knobCache[chain] = r; return r;
    }
    // LOUD, PUNCH, BRIGHT of a mastering preset (its slots: eq, comp, multiband, exciter, saturation, width, limiter)
    internal static float[] MasterKnobs(MasterPreset p)
    {
        string eq = p.Slots != null && p.Slots.Length > 0 ? p.Slots[0] ?? "" : "", comp = p.Slots != null && p.Slots.Length > 1 ? p.Slots[1] ?? "" : "", ex = p.Slots != null && p.Slots.Length > 3 ? p.Slots[3] ?? "" : "";
        float exAmt = KNum(ex, "amt"); if (float.IsNaN(exAmt)) exAmt = KNum(ex, "mix"); float bright = KHighs(eq) + (float.IsNaN(exAmt) ? 0 : exAmt / 25f);
        return new[] { K01((p.Target + 18f) / 11f), KPunch(comp), K01(bright / 6f) };
    }
    // a plugin preset ("thr=-24;ratio=3;..."): its first three settings, each where it sits in that setting's range
    internal static float[] PluginKnobs(Fx f, string data, out string[] labels)
    {
        var vals = new List<float>(); var labs = new List<string>(); data = data ?? "";
        foreach (var kv in data.Split(';'))
        {
            int e = kv.IndexOf('='); if (e <= 0) continue; string id = kv.Substring(0, e).Trim(); float v; if (!float.TryParse(kv.Substring(e + 1), NumberStyles.Float, KCI, out v)) continue;
            var prm = f.P.FirstOrDefault(x => x.Id == id); if (prm == null || prm.Choices != null || prm.Max <= prm.Min) continue;
            vals.Add(K01((v - prm.Min) / (prm.Max - prm.Min))); string nm = (prm.Name ?? id).ToUpperInvariant(); labs.Add(nm.Length > 6 ? nm.Substring(0, 6) : nm); if (vals.Count == 3) break;
        }
        labels = labs.ToArray(); return vals.ToArray();
    }

    static readonly string[] ChainKnobNames = { "SPACE", "PUNCH", "BRIGHT" }, MasterKnobNames = { "LOUD", "PUNCH", "BRIGHT" };
    // the knobs in a row: right edge at `right`, centred on cy; returns the left edge they take up
    float PresetKnobs(Graphics g, float right, float cy, float[] v, string[] labels, string hintWhat)
    {
        if (v == null || v.Length == 0) return right; const float d = 26, pitch = 46; float x0 = right - pitch * v.Length;
        Color[] cols = { Color.FromArgb(255, 61, 217, 255), Color.FromArgb(255, 255, 92, 200), Color.FromArgb(255, 176, 92, 255) };   // Purphex cyan, pink and violet: the preset windows keep the brand look
        var oldSm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        for (int i = 0; i < v.Length; i++)
        {
            float cx = x0 + pitch * i + pitch / 2, ky = cy - 7; var r = new RectangleF(cx - d / 2, ky - d / 2, d, d); Color c = cols[i % cols.Length];
            using (var br = new LinearGradientBrush(r, Color.FromArgb(255, 46, 30, 78), Color.FromArgb(255, 12, 6, 26), 90f)) g.FillEllipse(br, r);
            using (var pen = new Pen(Color.FromArgb(70, 255, 255, 255), 2.4f)) g.DrawArc(pen, r.X + 2, r.Y + 2, d - 4, d - 4, 135, 270);   // the track
            float sweep = Math.Max(1f, 270f * v[i]); using (var pen = new Pen(c, 2.4f)) g.DrawArc(pen, r.X + 2, r.Y + 2, d - 4, d - 4, 135, sweep);   // how far it is turned
            double a = (135 + 270 * v[i]) * Math.PI / 180; using (var pen = new Pen(Color.White, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawLine(pen, cx + (float)Math.Cos(a) * 3, ky + (float)Math.Sin(a) * 3, cx + (float)Math.Cos(a) * (d / 2 - 5), ky + (float)Math.Sin(a) * (d / 2 - 5));
            Txt(g, i < labels.Length ? labels[i] : "", 8, Color.FromArgb(225, 215, 200, 245), Rc(cx - pitch / 2, ky + d / 2 + 1, pitch, 12), StringAlignment.Center, true);
            Hint2(Rc(cx - pitch / 2, ky - d / 2, pitch, d + 14), (i < labels.Length ? labels[i] : "") + " " + Math.Round(v[i] * 100) + "%: how much " + hintWhat);
        }
        g.SmoothingMode = oldSm; return x0;
    }
}
