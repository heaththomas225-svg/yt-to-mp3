using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

// =====================================================================================================
//  PRODUCER TAG GENERATOR (the sound side): the words you type are spoken by a voice on this PC (the speech voices Windows has built in,
//  no account or key, nothing goes online) or you record your own voice; then a character (deep, giant, monster, robot...), the effects you
//  pick (reverb, echo, stutter, a reverse swell, tape stop...) and, if you like, one of the studio's vocal presets are put on it, and it is
//  levelled to a clean peak. The window that drives it is in Studio.ProducerTag.cs.
// =====================================================================================================
sealed class TagSettings
{
    public string Text = "";
    public int Voice;             // index into TagMaker.Voices(); -1 = your recorded voice
    public int Character;         // index into TagMaker.Characters
    public int Speed;             // -5 .. 5 (the voice speaks slower / faster)
    public float Pitch;           // semitones on top of the character
    public bool[] Fx = new bool[TagMaker.Effects.Length];
    public string VocalPreset = "";   // a vocal preset's name ("" = none)
    public string Sig() { return Text + "|" + Voice + "|" + Character + "|" + Speed + "|" + Pitch.ToString("0.0") + "|" + string.Join(",", Array.ConvertAll(Fx, b => b ? "1" : "0")) + "|" + VocalPreset; }
}

static class TagMaker
{
    public const int SR = 44100;
    // name, what it does, pitch (semitones), formant shift (semitones)
    public sealed class Character { public string Name, Says; public float Pitch, Formant; public Character(string n, string s, float p, float f) { Name = n; Says = s; Pitch = p; Formant = f; } }
    public static readonly Character[] Characters = {
        new Character("Natural", "The voice as it is.", 0, 0),
        new Character("Deep", "Lower and fuller, still clearly a person.", -3, -1.5f),
        new Character("Giant", "Much lower with a big chest: a huge voice.", -7, -4),
        new Character("Monster", "Very low and growly, with grit.", -11, -6),
        new Character("High", "Higher and brighter.", 4, 2),
        new Character("Chipmunk", "Very high and quick-sounding.", 9, 6),
        new Character("Robot", "Metallic and buzzy, like a machine talking.", 0, 0),
        new Character("Radio", "Thin and boxy, like an old radio or a phone call.", 0, 0),
        new Character("Alien", "The pitch wobbles up and down: strange and spacey.", 2, 3) };
    // name, what it does
    public static readonly string[][] Effects = {
        new[] { "REVERB", "Space: the tag sounds like it is in a big hall." },
        new[] { "ECHO", "Repeats that fade out after the words: the classic tag tail." },
        new[] { "STUTTER", "The start repeats quickly before the whole tag (Pur-Pur-Purphex)." },
        new[] { "REVERSE SWELL", "A backwards reverb that rises up into the words." },
        new[] { "TAPE STOP", "The end slows down and drops in pitch, like a tape being stopped." },
        new[] { "DISTORTION", "Gritty and loud: cuts through a busy beat." },
        new[] { "WIDE", "Doubled a little left and right so it sounds wider." },
        new[] { "LOW CUT", "Takes away the rumble under the voice so it never fights the 808." } };

    // ---- the voices on this PC ----
    sealed class VoiceTok { public string Name; public string Category; public int Index; }
    static List<VoiceTok> voices;
    public static List<string> Voices()
    {
        if (voices == null)
        {
            voices = new List<VoiceTok>();
#if !PURPHEX_DESKTOP
            var seen = new HashSet<string>();
            foreach (var cat in new[] { @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Speech_OneCore\Voices", @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Speech\Voices" })
            {
                try
                {
                    var toks = Tokens(cat); int n = (int)Get(toks, "Count");
                    for (int i = 0; i < n; i++) { var t = Call(toks, "Item", i); string d = (string)Call(t, "GetDescription", 0); string nm = Nice(d); if (seen.Add(nm)) voices.Add(new VoiceTok { Name = nm, Category = cat, Index = i }); }
                }
                catch { }
            }
#endif
        }
        var l = new List<string>(); foreach (var v in voices) l.Add(v.Name); return l;
    }
    // "Microsoft George - English (United Kingdom)" -> "George (UK)"
    static string Nice(string d)
    {
        string s = d.Replace("Microsoft ", "").Replace(" Desktop", ""); int dash = s.IndexOf(" - ", StringComparison.Ordinal); string who = dash > 0 ? s.Substring(0, dash) : s, lang = dash > 0 ? s.Substring(dash + 3) : "";
        lang = lang.Replace("English (United Kingdom)", "UK").Replace("English (Great Britain)", "UK").Replace("English (United States)", "US").Replace("English (Australia)", "AU").Replace("English (Canada)", "CA").Replace("English (India)", "IN").Replace("English (Ireland)", "IE");
        return lang.Length > 0 ? who + " (" + lang + ")" : who;
    }
#if !PURPHEX_DESKTOP
    static object Make(string progId) { return Activator.CreateInstance(Type.GetTypeFromProgID(progId, true)); }
    static object Call(object o, string m, params object[] a) { return o.GetType().InvokeMember(m, BindingFlags.InvokeMethod, null, o, a); }
    static object Get(object o, string p) { return o.GetType().InvokeMember(p, BindingFlags.GetProperty, null, o, null); }
    static void Set(object o, string p, object v) { bool plain = v == null || v is string || v.GetType().IsPrimitive; o.GetType().InvokeMember(p, plain ? BindingFlags.SetProperty : BindingFlags.PutRefDispProperty, null, o, new[] { v }); }   // (an object is set by reference)
    static object Tokens(string cat) { var c = Make("SAPI.SpObjectTokenCategory"); Call(c, "SetId", cat, false); return Call(c, "EnumerateTokens", "", ""); }
#endif

    // the words spoken by voice i (speed -5..5), as mono samples at 44.1 kHz; null and why on failure
    public static float[] Speak(string text, int i, int speed, out string err)
    {
        err = null; Voices();
        if (string.IsNullOrEmpty(text.Trim())) { err = "Type your tag first."; return null; }
        if (i < 0 || i >= voices.Count) { err = "No speech voice on this PC: record your own voice (MY VOICE)."; return null; }
#if PURPHEX_DESKTOP
        err = "Speech voices are on Windows: record your own voice (MY VOICE)."; return null;
#else
        string tmp = Path.Combine(Path.GetTempPath(), "purphex_tag_" + Guid.NewGuid().ToString("N") + ".wav");
        float[] res = null; string e2 = null;
        var th = new System.Threading.Thread(() =>
        {   // (the speech engine wants a thread of its own)
            object fs = null;
            try
            {
                var v = voices[i]; var sp = Make("SAPI.SpVoice"); var tok = Call(Tokens(v.Category), "Item", v.Index); Set(sp, "Voice", tok);
                Set(sp, "Rate", (int)Math.Round(Math.Max(-8, Math.Min(8, speed * 1.4))));
                var fmt = Make("SAPI.SpAudioFormat"); Set(fmt, "Type", 38);   // 44.1 kHz, 16-bit, mono
                fs = Make("SAPI.SpFileStream"); Set(fs, "Format", fmt); Call(fs, "Open", tmp, 3, false); Set(sp, "AudioOutputStream", fs);
                Call(sp, "Speak", text, 0); Call(fs, "Close"); fs = null;
                float[] l, r; int sr; string f; if (!SoundIO.Read(tmp, out l, out r, out sr, out f)) { e2 = "The voice made no sound."; return; }
                res = sr == SR ? l : HqResample.Run(l, sr, SR);
            }
            catch (Exception ex) { e2 = "The voice could not speak: " + ex.Message; }
            finally { if (fs != null) try { Call(fs, "Close"); } catch { } try { File.Delete(tmp); } catch { } }
        });
        th.SetApartmentState(System.Threading.ApartmentState.STA); th.IsBackground = true; th.Start(); th.Join(30000);
        if (res == null) err = e2 ?? "The voice took too long."; return res;
#endif
    }

    // a recording of your voice: the silence before and after is cut off
    public static float[] Trim(float[] x)
    {
        if (x == null || x.Length == 0) return x; double pk = 0; foreach (var v in x) pk = Math.Max(pk, Math.Abs(v)); if (pk < 1e-4) return new float[0];
        double th = pk * 0.06; int a = 0, b = x.Length - 1; while (a < b && Math.Abs(x[a]) < th) a++; while (b > a && Math.Abs(x[b]) < th) b--;
        a = Math.Max(0, a - SR / 50); b = Math.Min(x.Length - 1, b + SR / 8); var o = new float[b - a + 1]; Array.Copy(x, a, o, 0, o.Length); return o;
    }

    // ---- the sound: character, vocal preset, effects, level ----
    public static float[][] Process(float[] dry, TagSettings s)
    {
        if (dry == null || dry.Length == 0) return null;
        var ch = Characters[Math.Max(0, Math.Min(Characters.Length - 1, s.Character))]; bool[] fx = s.Fx;
        float[] l = (float[])dry.Clone();
        if (fx[2]) l = Stutter(l);
        int tail = SR * ((fx[0] || fx[1]) ? 3 : 1) / 2; l = Pad(l, SR / 20, tail);   // room for the reverb and echo to ring out
        float[] r = (float[])l.Clone();
        // the character
        float pitch = ch.Pitch + s.Pitch;
        if (Math.Abs(pitch) > 0.01f || Math.Abs(ch.Formant) > 0.01f || ch.Name == "Alien")
        {
            var ps = new PitchShiftFx(); ps.Prepare(SR); ps.Set("p1", pitch); ps.Set("fshift", ch.Formant); ps.Set("keep", 0);
            if (ch.Name == "Alien") { ps.Set("mshape", 1); ps.Set("mrate", 3f); ps.Set("mdepth", 2f); }
            RunFx(ps, l, r); int lat = ps.Latency; if (lat > 0) { l = Shift(l, lat); r = Shift(r, lat); }
        }
        if (ch.Name == "Robot") { double ph = 0, inc = 2 * Math.PI * 70 / SR; for (int i = 0; i < l.Length; i++) { float m = (float)(0.35 + 0.65 * Math.Abs(Math.Sin(ph))); l[i] *= m * 1.6f; r[i] *= m * 1.6f; ph += inc; } Comb(l, r, SR / 180, 0.6f); }
        if (ch.Name == "Monster") Drive(l, r, 2.2f);
        if (ch.Name == "Radio") { Band(l, r, 450, 2800); Drive(l, r, 1.6f); }
        // a vocal preset from the studio
        if (!string.IsNullOrEmpty(s.VocalPreset))
        {
            var st = VocalStyles.All.Find(x => x.Name == s.VocalPreset);
            if (st != null) foreach (var f in VocalStyles.Build(st)) { try { f.Prepare(SR); RunFx(f, l, r); int lat = f.Latency; if (lat > 0) { l = Shift(l, lat); r = Shift(r, lat); } } catch { } }
        }
        if (fx[7]) HighPass(l, r, 140);
        if (fx[5]) Drive(l, r, 3f);
        if (fx[6]) Widen(l, r);
        if (fx[1]) { var d = FxRegistry.Create("delay"); d.Prepare(SR); d.Set("time", 330); d.Set("fb", 45); d.Set("mix", 32); d.Set("lc", 200); d.Set("hc", 6000); RunFx(d, l, r); }
        if (fx[0]) { var rv = FxRegistry.Create("reverb"); rv.Prepare(SR); rv.Set("size", 78); rv.Set("mix", 28); rv.Set("pre", 20); rv.Set("lc", 200); RunFx(rv, l, r); }
        if (fx[3]) { var sw = Swell(l, r); l = sw[0]; r = sw[1]; }
        if (fx[4]) TapeStop(l, r);
        // levelled to a clean peak, with soft ends
        int end = l.Length; while (end > SR / 10 && Math.Abs(l[end - 1]) < 1e-4 && Math.Abs(r[end - 1]) < 1e-4) end--; if (end < l.Length) { Array.Resize(ref l, end); Array.Resize(ref r, end); }
        double pk = 0; for (int i = 0; i < l.Length; i++) pk = Math.Max(pk, Math.Max(Math.Abs(l[i]), Math.Abs(r[i]))); float g = pk > 1e-6 ? (float)(0.89 / pk) : 1;
        int fi = Math.Min(l.Length / 2, SR / 200), fo = Math.Min(l.Length / 2, SR / 25);
        for (int i = 0; i < l.Length; i++) { float e = 1; if (i < fi) e = i / (float)fi; if (i > l.Length - fo) e = (l.Length - i) / (float)fo; l[i] *= g * e; r[i] *= g * e; }
        return new[] { l, r };
    }

    static void RunFx(Fx f, float[] l, float[] r)
    {
        if (f == null) return;
        const int B = 512; var bl = new float[B]; var br = new float[B];
        for (int p = 0; p < l.Length; p += B) { int n = Math.Min(B, l.Length - p); Array.Copy(l, p, bl, 0, n); Array.Copy(r, p, br, 0, n); f.BeginBlock(null, p, n, true); f.Process(bl, br, n); Array.Copy(bl, 0, l, p, n); Array.Copy(br, 0, r, p, n); }
    }
    static float[] Pad(float[] x, int before, int after) { var o = new float[x.Length + before + after]; Array.Copy(x, 0, o, before, x.Length); return o; }
    static float[] Shift(float[] x, int lat) { var o = new float[x.Length]; Array.Copy(x, Math.Min(lat, x.Length), o, 0, Math.Max(0, x.Length - lat)); return o; }
    // the first syllable three times, then the whole tag
    static float[] Stutter(float[] x)
    {
        int seg = Math.Min(x.Length, SR * 14 / 100), gap = SR * 3 / 100, fade = SR / 200; var o = new List<float>();
        for (int k = 0; k < 3; k++) { for (int i = 0; i < seg; i++) { float e = i < fade ? i / (float)fade : (i > seg - fade ? (seg - i) / (float)fade : 1); o.Add(x[i] * e * (0.75f + 0.08f * k)); } for (int i = 0; i < gap; i++) o.Add(0); }
        o.AddRange(x); return o.ToArray();
    }
    static void Drive(float[] l, float[] r, float d) { float n = (float)Math.Tanh(d); for (int i = 0; i < l.Length; i++) { l[i] = (float)Math.Tanh(l[i] * d) / n; r[i] = (float)Math.Tanh(r[i] * d) / n; } }
    static void Band(float[] l, float[] r, double lo, double hi) { HighPass(l, r, lo); LowPass(l, r, hi); }
    static void HighPass(float[] l, float[] r, double f) { var c = Biquad.Coeffs(Biquad.HighPass, SR, f, 0.707, 0); Filt(c, l); Filt(c, r); Filt(c, l); Filt(c, r); }
    static void LowPass(float[] l, float[] r, double f) { var c = Biquad.Coeffs(Biquad.LowPass, SR, f, 0.707, 0); Filt(c, l); Filt(c, r); Filt(c, l); Filt(c, r); }
    static void Filt(double[] c, float[] x) { double x1 = 0, x2 = 0, y1 = 0, y2 = 0; for (int i = 0; i < x.Length; i++) { double y = c[0] * x[i] + c[1] * x1 + c[2] * x2 - c[3] * y1 - c[4] * y2; x2 = x1; x1 = x[i]; y2 = y1; y1 = y; x[i] = (float)y; } }
    static void Comb(float[] l, float[] r, int d, float g) { var bl = new float[d]; var br = new float[d]; int w = 0; for (int i = 0; i < l.Length; i++) { float a = l[i] + bl[w] * g, b = r[i] + br[w] * g; bl[w] = a; br[w] = b; w = (w + 1) % d; l[i] = a * 0.6f; r[i] = b * 0.6f; } }
    static void Widen(float[] l, float[] r)
    {   // a copy a little later and a touch higher on the left, lower on the right
        var ps = new PitchShiftFx(); ps.Prepare(SR); ps.Set("c1", 12); var a = (float[])l.Clone(); var b = (float[])r.Clone(); RunFx(ps, a, b);
        int dl = SR * 14 / 1000; for (int i = l.Length - 1; i >= 0; i--) { float wl = i >= dl ? a[i - dl] : 0, wr = i >= dl ? b[i - dl] : 0; l[i] = l[i] * 0.8f + wl * 0.45f; r[i] = r[i] * 0.8f - wr * 0.2f + wl * 0.25f; }
    }
    // a reverb of the start, played backwards, rising into the words
    static float[][] Swell(float[] l, float[] r)
    {
        int len = Math.Min(l.Length, SR * 6 / 10), pre = SR * 9 / 10; var a = new float[len + pre]; var b = new float[len + pre]; Array.Copy(l, a, len); Array.Copy(r, b, len);
        var rv = FxRegistry.Create("reverb"); rv.Prepare(SR); rv.Set("size", 90); rv.Set("mix", 100); rv.Set("lc", 250); RunFx(rv, a, b); Array.Reverse(a); Array.Reverse(b);
        var ol = new float[pre + l.Length]; var or = new float[pre + r.Length];
        for (int i = 0; i < ol.Length; i++) { float s1 = i < a.Length ? a[i] * 0.7f : 0, s2 = i < b.Length ? b[i] * 0.7f : 0; int j = i - pre; ol[i] = s1 + (j >= 0 ? l[j] : 0); or[i] = s2 + (j >= 0 ? r[j] : 0); }
        return new[] { ol, or };
    }
    // the last half second slows to a stop
    static void TapeStop(float[] l, float[] r)
    {
        int n = Math.Min(l.Length / 2, SR / 2), st = l.Length - n; if (n < 100) return; var sl = new float[n]; var sr = new float[n]; double pos = st;
        for (int i = 0; i < n; i++) { double sp = 1 - i / (double)n; int k = (int)pos; double t = pos - k; if (k + 1 >= l.Length) break; sl[i] = (float)(l[k] * (1 - t) + l[k + 1] * t) * (float)sp; sr[i] = (float)(r[k] * (1 - t) + r[k + 1] * t) * (float)sp; pos += sp; }
        Array.Copy(sl, 0, l, st, n); Array.Copy(sr, 0, r, st, n);
    }
}
