using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

// ======================================================================
//  AI EFFECT GENERATOR: describe one effect (or a few) in your own words and get it with the settings you asked for:
//  "huge dark cathedral reverb with a shimmer", "1/8 dotted ping pong delay, tape, ducked", "hard autotune in F# minor",
//  "telephone with a bit of distortion", "croaky demon voice 9 semitones down", "slow tremolo on 1/4 notes", "bleep the swear".
//  It works inside the app (no internet): the words pick the engines and their settings; anything it does not know as an effect
//  word it looks for among the 10,000+ names of the EFFECT LIBRARY. REGENERATE gives another take on the same words, RANDOMISE a surprise.
// ======================================================================
static class AiVary
{
    // settings that are a smooth amount (a different take nudges them); choices like mode, sync or key are never touched
    static readonly HashSet<string> Mul = new HashSet<string> { "mix", "size", "decay", "damp", "fb", "drive", "ratio", "att", "rel", "width", "depth", "rate", "time", "speed", "natural", "hlevel", "spread", "lc", "hc", "hp_f", "lp_f", "b0_f", "b1_f", "b2_f", "b3_f", "b4_f", "b5_f", "jitter", "sub", "rasp", "dark", "knee", "diff", "mod", "er", "shim", "duck", "sat", "wow", "dens", "len", "q", "hp", "lp", "bump", "noise" };
    static readonly HashSet<string> Add = new HashSet<string> { "b0_g", "b1_g", "b2_g", "b3_g", "b4_g", "b5_g", "g0", "g1", "g2", "g3", "g4", "g5", "g6", "g7", "g8", "g9", "tilt", "makeup", "gain", "level", "thr", "out" };
    static readonly Regex Tok = new Regex(@"([a-z][a-z0-9_]*(?:\.[a-z0-9_]+)?)=(-?[0-9]+(?:\.[0-9]+)?)", RegexOptions.Compiled);
    // another take (REGENERATE): every plugin in the result is made, and each of its smooth settings is nudged a little (amounts by up to about 15%,
    // settings that go below zero by a few % of their range); choices, keys, notes and semitone steps stay exactly as they were. The sound stays similar but is never the same.
    static readonly HashSet<string> Fixed = new HashSet<string> { "mask", "p1", "p2", "p3", "p4", "key", "scale", "note", "every", "shimst", "hold" };
    public static string Jitter(string chain, Random rnd)
    {
        if (rnd == null || string.IsNullOrEmpty(chain)) return chain;
        var outp = new List<string>();
        foreach (var part in chain.Split(new[] { " > " }, StringSplitOptions.RemoveEmptyEntries))
        {
            string st = part.Trim(), type = st, data = ""; int b = st.IndexOf('{'); if (b >= 0) { type = st.Substring(0, b).Trim(); int e = st.LastIndexOf('}'); data = st.Substring(b + 1, (e > b ? e : st.Length) - b - 1); }
            string id = type.StartsWith("@") ? PluginLibrary.IdFor(type.Substring(1)) : type; Fx fx = null; try { fx = Presets.Make(id, data); } catch { }
            if (fx == null) { outp.Add(st); continue; }
            var sb = new StringBuilder();
            foreach (var p in fx.P)
            {
                float v = p.Val; string last = p.Id.Contains('.') ? p.Id.Substring(p.Id.LastIndexOf('.') + 1) : p.Id;
                if (!p.IsChoice && !Fixed.Contains(last))
                {
                    double span = p.Max - p.Min;
                    if (p.Min < 0) v += (float)((rnd.NextDouble() - 0.5) * span * 0.06); else if (v > 0) v *= (float)(1 + (rnd.NextDouble() - 0.5) * 0.3);
                    v = Math.Max(p.Min, Math.Min(p.Max, v));
                }
                sb.Append(p.Id + "=" + v.ToString("0.###", CultureInfo.InvariantCulture) + ";");
            }
            outp.Add(type + "{" + sb.ToString().TrimEnd(';') + "}");
        }
        return string.Join(" > ", outp.ToArray());
    }
    public static T Pick<T>(Random rnd, T[] options, T fallback) { return rnd == null ? fallback : options[rnd.Next(options.Length)]; }
    // RANDOMISE: a surprise description in the words each generator understands
    public static string RandomVocal(Random r)
    {
        var artists = VocalStyles.All.Select(VocalStyles.ArtistOf).Where(a => a.Length > 0).Distinct().ToArray(); string[] genres = { "UK drill", "R&B", "trap", "emo", "rage", "afro", "boom bap", "dark R&B", "pop", "melodic", "cloud rap", "phonk" };
        string[] fx = { "with a stutter delay", "lots of reverb", "dry", "dark", "bright", "hard tune", "with harmonies", "a bit distorted", "wide", "telephone", "with a 1/4 ping pong delay", "slapback", "deep voice", "doubled", "lofi", "chorus", "huge hall", "octave", "baby voice", "glitchy" };
        string start = r.Next(3) == 0 ? genres[r.Next(genres.Length)] : artists.Length > 0 ? artists[r.Next(artists.Length)] : "trap"; int n = 1 + r.Next(3); var used = new List<string>();
        while (used.Count < n) { var w = fx[r.Next(fx.Length)]; if (!used.Contains(w)) used.Add(w); } return start + ", " + string.Join(", ", used.ToArray());
    }
    public static string RandomBus(Random r)
    {
        string[] mods = { "", "", " with more glue", ", dry", " with lots of reverb", " with a triplet echo", ", saturated", ", very wide", ", loud" };
        return BusPresets.Flavours[r.Next(BusPresets.Flavours.Length)].ToLowerInvariant() + " " + BusPresets.Genres[r.Next(BusPresets.Genres.Length)] + " " + BusPresets.Kinds[r.Next(BusPresets.Kinds.Length)].ToLowerInvariant() + mods[r.Next(mods.Length)];
    }
    public static string RandomEffect(Random r)
    {
        string[] a = { "huge dark cathedral reverb with a shimmer", "small bright room reverb", "gated plate reverb", "reverse reverb, wet", "1/8 dotted ping pong delay, tape, ducked", "dark 1/4 echo with a long tail", "slapback echo", "1/16 stutter repeat",
            "hard autotune in " + PitchCorrector.Keys[r.Next(12)] + " minor", "natural tuning in " + PitchCorrector.Keys[r.Next(12)] + " major", "harmony a third above", "pitch up 7 semitones", "octave down", "deeper voice", "chipmunk voice", "croaky demon voice", "satan voice 9 semitones down",
            "telephone with a bit of distortion", "old radio", "megaphone", "walkie talkie", "robot voice", "vocoder", "tube saturation", "heavy fuzz", "8 bit bitcrush", "slow tremolo on 1/4 notes", "auto pan 1/8", "filter sweep", "lush chorus", "jet flanger",
            "phaser", "vibrato", "bass boost", "cut the mud and add air", "presence boost", "4:1 compressor, fast attack", "parallel compression", "glue compression", "noise gate", "de-esser", "limiter, loud", "8D", "wider", "glitch chops", "tape stop", "bleep censor", "reverse censor",
            "granular cloud", "talkbox", "ring mod 60 Hz", "vinyl lo-fi", "spring reverb, warm", "ambient space, airy, wet" };
        string s = a[r.Next(a.Length)]; if (r.Next(3) == 0) { var b = a[r.Next(a.Length)]; if (b != s) s += " and " + b; } return s;
    }
}

static class EffectPrompt
{
    static string Norm(string s) { var c = (s ?? "").ToLowerInvariant().Replace("’", "'").ToCharArray(); for (int i = 0; i < c.Length; i++) if (!(char.IsLetterOrDigit(c[i]) || c[i] == '#' || c[i] == '/' || c[i] == ':' || c[i] == '.' || c[i] == '%')) c[i] = ' '; return " " + string.Join(" ", new string(c).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) + " "; }
    static int Find(string p, string words) { foreach (var w in words.Split('|')) { int i = p.IndexOf(" " + w + " ", StringComparison.Ordinal); if (i >= 0) return i + 1; } return -1; }
    static bool Has(string p, string words) { return Find(p, words) >= 0; }
    static string F(double v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }
    // the hand-made plugin of an engine (so the effect saves and loads like any other plugin); the settings go on top of it
    static string Lib(string engine) { if (FxRegistry.All.Length == 0) return null; var v = PluginLibrary.All.FirstOrDefault(x => x.Engine == engine && PluginLibrary.IsListed(x) && x.Hide.Length == 0) ?? PluginLibrary.All.FirstOrDefault(x => x.Engine == engine && PluginLibrary.IsListed(x)) ?? PluginLibrary.All.FirstOrDefault(x => x.Engine == engine); return v == null ? null : "@" + v.Name; }
    static string Lib(string engine, string name) { if (FxRegistry.All.Length == 0) return null; return PluginLibrary.All.Any(x => x.Name == name && x.Engine == engine && PluginLibrary.IsListed(x)) ? "@" + name : Lib(engine); }   // the hand-made one that fits (its name shows in the list)
    static double Amt(string p) { if (Has(p, "huge|massive|giant|lots|loads|heavy|very|extreme|crazy|big|strong|hard|super")) return 1.6; if (Has(p, "subtle|slight|light|little|bit|touch|gentle|soft|small")) return 0.55; return 1; }
    static int Sync(string p, int def, Random rnd)
    {
        if (Has(p, "1/32|32nd|thirty second")) return 13; if (Has(p, "1/16 triplet|16th triplet")) return 12; if (Has(p, "1/16 dotted|dotted 1/16|dotted 16th")) return 11; if (Has(p, "1/16|16th|sixteenth")) return 10;
        if (Has(p, "1/8 triplet|triplet|triplets")) return 9; if (Has(p, "1/8 dotted|dotted 1/8|dotted eighth|dotted")) return 8; if (Has(p, "1/8|eighth|8th")) return 7; if (Has(p, "1/4 dotted|dotted 1/4|dotted quarter")) return 5; if (Has(p, "1/4|quarter|4th")) return 4;
        if (Has(p, "1/2|half note|half")) return 2; if (Has(p, "1/1|whole note|1 bar|one bar|bar")) return 1; return def;
    }
    static readonly string[] SyncName = { "free time", "1/1", "1/2", "dotted 1/2", "1/4", "dotted 1/4", "1/4 triplet", "1/8", "dotted 1/8", "1/8 triplet", "1/16", "dotted 1/16", "1/16 triplet", "1/32" };
    static int Num(string p, string after, int def) { var m = Regex.Match(p, @" (?:" + after + @") (-?\d+) "); if (m.Success) return int.Parse(m.Groups[1].Value); m = Regex.Match(p, @" (-?\d+) (?:semitones?|st|steps?) (?:" + after + ") "); return m.Success ? int.Parse(m.Groups[1].Value) : def; }

    public static PresetPrompt.Result Make(string prompt, int seed = 0)
    {
        var r = new PresetPrompt.Result(); string p = Norm(prompt); var rnd = seed == 0 ? null : new Random(seed); var st = new List<string>(); Action<string> said = s => r.Understood.Add(s);
        double a = Amt(p); bool dark = Has(p, "dark|darker|muffled|moody|warm|warmer"), bright = Has(p, "bright|brighter|airy|crisp|shiny|sparkly"), wet = Has(p, "wet|lots|loads|drenched|huge|massive"), subtle = Has(p, "subtle|slight|light|touch|little bit|a bit");
        bool fx = false;
        // 1. censor
        if (Has(p, "censor|bleep|beep|clean version|swear|curse|cuss"))
        {
            int m = Has(p, "mute|silence|silent") ? 1 : Has(p, "reverse|reversed|backwards") ? 2 : Has(p, "muffle|muffled|underwater") ? 3 : Has(p, "scramble|scrambled|garbled") ? 4 : Has(p, "static|radio noise") ? 5 : Has(p, "duck|quieter") ? 6 : 0;
            st.Add(Lib("censor") + "{mode=" + m + ";censor=1" + (Has(p, "follow|tight|only the word") ? ";follow=1" : "") + "}"); said("Censor: " + CensorFx.Modes[m] + " (right-click a section and press Censor this section to bleep just there)"); fx = true;
        }
        // 2. demon / monster voices
        if (Has(p, "demon|demonic|satan|devil|monster|beast|possessed|hell|evil|growl|croak|croaky|lucifer|exorcist"))
        {
            int down = Num(p, "down", 0); if (down == 0) down = Has(p, "satan|devil|hell|lucifer|huge|deep|deepest") ? 9 : Has(p, "subtle|slight|bit") ? 3 : 5; down = Math.Abs(down);
            double croak = Has(p, "croak|croaky|fry|creaky") ? 90 : 70, rasp = Has(p, "rasp|raspy|whisper|breathy") ? 75 : 35;
            string verb = Has(p, "dry|no reverb") ? "" : " > reverb2{mode=5;size=80;decay=4.5;damp=3200;hc=6000;mix=" + F(wet ? 28 : 18) + "}";
            st.Add("@Croaky Demon Voice{s1.p1=-" + down + ";s1.fshift=-" + Math.Min(12, down / 2 + 2) + ";s2.depth=" + F(croak) + ";s2.rasp=" + F(rasp) + ";s2.sub=" + F(down >= 8 ? 60 : 40) + "}");
            said("Demon voice " + down + " semitones down, croak " + F(croak) + "%, rasp " + F(rasp) + "%" + (verb.Length == 0 ? ", dry" : ", dark cathedral")); fx = true;
        }
        // 3. tuning and harmonies
        var km = Regex.Match(p, @" ([a-g])(#| sharp| flat|b)? ?(major|minor|maj|min|m) "); int key = -1, scale = -1;
        if (km.Success) { key = new[] { 0, 2, 4, 5, 7, 9, 11 }["cdefgab".IndexOf(km.Groups[1].Value[0])]; string acc = km.Groups[2].Value.Trim(); if (acc == "#" || acc == "sharp") key = (key + 1) % 12; else if (acc == "flat" || acc == "b") key = (key + 11) % 12; scale = km.Groups[3].Value.StartsWith("maj") ? 1 : 2; }
        bool harmony = Has(p, "harmony|harmonies|harmonise|harmonize|choir|third above|third below|fifth above|fifth below|octave above|octave below|power chord");
        if (Has(p, "autotune|auto tune|tune|tuned|tuning|t pain|tpain|pitch correction|pitch correct|correct the pitch") || harmony)
        {
            if (key < 0 && Has(p, "pentatonic")) scale = 4; if (Has(p, "minor pentatonic")) scale = 5; if (Has(p, "blues")) scale = 6; if (Has(p, "dorian")) scale = 7; if (Has(p, "harmonic minor")) scale = 3;
            if (harmony)
            {
                int h = Has(p, "choir") ? 8 : Has(p, "third below") ? 2 : Has(p, "fifth above") ? 3 : Has(p, "fifth below") ? 4 : Has(p, "octave above") ? 5 : Has(p, "octave below") ? 6 : Has(p, "power") ? 7 : Has(p, "thick|double") ? 9 : 1;
                st.Add(Lib("melody") + "{" + (key >= 0 ? "key=" + key + ";scale=" + scale + ";" : "") + "speed=15;strength=100;natural=30;harm=" + h + ";hlevel=" + F(subtle ? 35 : 55) + "}");
                said("Melody Tune with a harmony" + (key >= 0 ? " in " + PitchCorrector.Keys[key] + " " + PitchCorrector.Scales[scale].ToLowerInvariant() : " (it finds the key of your track)"));
            }
            else
            {
                double speed = Has(p, "hard|robotic|robot|t pain|tpain|snap|snapped|heavy") ? 0 : Has(p, "fast|tight") ? 8 : Has(p, "natural|subtle|light|gentle|soft") ? 45 : 14; 
                int rg = Has(p, "low voice|deep voice|bass voice|male") ? 0 : Has(p, "high voice|female|soprano") ? 2 : 1;
                st.Add("pitch{" + (key >= 0 ? "key=" + key + ";scale=" + scale + ";mask=" + PitchCorrector.MaskFor(key, scale) + ";" : scale > 0 ? "scale=" + scale + ";" : "") + "speed=" + F(speed) + ";strength=100;natural=" + F(speed == 0 ? 0 : speed > 30 ? 55 : 20) + ";range=" + rg + ";mix=100}");
                said((speed == 0 ? "Hard autotune" : speed > 30 ? "Natural tuning" : "Autotune") + (key >= 0 ? " in " + PitchCorrector.Keys[key] + " " + PitchCorrector.Scales[scale].ToLowerInvariant() + " (kept)" : " (it finds the key of your track)"));
            }
            fx = true;
        }
        // 4. pitch and formant
        if (!st.Any(s => s.Contains("Croaky Demon")))
        {
            int up = Num(p, "up|pitch up|higher", 0), dn = Num(p, "down|pitch down|lower", 0);
            if (Has(p, "octave up")) up = 12; if (Has(p, "octave down|octave lower")) dn = 12; if (Has(p, "chipmunk")) up = Math.Max(up, 7);
            if (up != 0 || dn != 0 || Has(p, "pitch shift|pitch up|pitch down|pitched up|pitched down"))
            {
                int s = up != 0 ? Math.Abs(up) : -Math.Abs(dn != 0 ? dn : 5); if (s == 0) s = Has(p, "down|lower") ? -5 : 5; s = Math.Max(-24, Math.Min(24, s));
                st.Add(Lib("pshift", s == 12 ? "Octave Up" : s == -12 ? "Octave Down" : "Pitch Shifter") + "{p1=" + s + ";l1=100;keep=" + (Has(p, "chipmunk|cartoon|monster") ? 0 : 100) + ";mix=" + (Has(p, "layer|layered|blend|under") ? 50 : 100) + "}"); said("Pitch " + (s > 0 ? "up " : "down ") + Math.Abs(s) + " semitones"); fx = true;
            }
            if (Has(p, "deeper voice|deep voice|bigger voice|deeper|formant down|giant voice")) { st.Add(Lib("formant") + "{shift=" + F(-3 * a) + ";tilt=-1;mix=100}"); said("Deeper voice (formants down, same pitch)"); fx = true; }
            else if (Has(p, "baby voice|smaller voice|chipmunk|formant up|tiny voice|kid voice")) { st.Add(Lib("formant") + "{shift=" + F(4 * a) + ";tilt=2;mix=100}"); said("Smaller voice (formants up)"); fx = true; }
            if (Has(p, "doubler|double track|double tracked|doubled|thicken|thicker")) { st.Add(Lib("pshift", "Vocal Doubler") + "{p1=0;c1=12;l1=75;pan1=-75;d1=18;p2=0;c2=-11;l2=75;pan2=75;d2=28;p3=0;l3=0;p4=0;l4=0;keep=100;mix=60}"); said("Doubled (two detuned copies left and right)"); fx = true; }
        }
        // 5. telephone, radio, robots
        if (Has(p, "telephone|phone|phone call")) { st.Add(Lib("band", "Telephone Vocal") + "{hp=300;lp=3400;drive=" + F(Has(p, "distort|distortion|dirty|crunchy") ? 18 : 6) + ";noise=0;bump=4}"); said("Telephone band (300 to 3,400 Hz)"); fx = true; }
        else if (Has(p, "walkie talkie|walkie|cb radio")) { st.Add(Lib("band", "Vintage Telephone") + "{hp=450;lp=3000;drive=18;noise=30;bump=6}"); said("Walkie-talkie (narrow, driven, noisy)"); fx = true; }
        else if (Has(p, "megaphone|bullhorn|loudhailer")) { st.Add(Lib("band", "Megaphone") + "{hp=550;lp=4200;drive=24;bump=8}"); said("Megaphone"); fx = true; }
        else if (Has(p, "radio|old radio|am radio|transistor")) { st.Add(Lib("band", "Radio Voice") + "{hp=220;lp=5000;drive=10;noise=20;crackle=20}"); said("Old radio"); fx = true; }
        if (Has(p, "vocoder|talking synth")) { st.Add(Lib("vocoder") + "{carrier=0;note=48;chord=" + (Has(p, "chord|chords") ? 3 : 0) + ";mix=100}"); said("Vocoder"); fx = true; }
        else if (Has(p, "robot|robotic voice|dalek|metallic|ring mod|ring modulator")) { var hz = Regex.Match(p, @" (\d+) ?hz "); st.Add(Lib("ring", "Ring Modulator") + "{mode=0;wave=0;freq=" + (hz.Success ? hz.Groups[1].Value : Has(p, "dalek") ? "30" : "110") + ";depth=100;mix=70}"); said("Robot (ring modulator)"); fx = true; }
        if (Has(p, "talkbox|talk box|vowel|wah")) { st.Add(Lib("vowel") + "{vowel=0;shape=1;rate=" + F(Has(p, "fast") ? 4 : 1) + ";depth=2;mix=100}"); said("Talkbox vowels"); fx = true; }
        // 6. drive
        int sm = -1; string[] smn = { "soft clip", "tube", "tape", "overdrive", "hard clip", "", "fuzz", "wavefolder", "", "", "octave fuzz", "diode" };
        if (Has(p, "fuzz|fuzzy")) sm = Has(p, "octave fuzz") ? 10 : 6; else if (Has(p, "tube|valve")) sm = 1; else if (Has(p, "tape saturation|tape") && !Has(p, "tape stop|tapestop")) sm = Has(p, "delay|echo") ? -1 : 2; else if (Has(p, "hard clip|clipper|clipping|clip")) sm = 4; else if (Has(p, "wavefold|wavefolder|fold")) sm = 7; else if (Has(p, "diode")) sm = 11; else if (Has(p, "overdrive|distortion|distorted|distort|dirty|grit|gritty|crunch|crunchy|saturation|saturated|saturate|warmth") && !st.Any(s => s.Contains("band"))) sm = Has(p, "saturation|saturated|saturate|warmth") ? 1 : 3;
        if (sm >= 0) { double d = Has(p, "heavy|destroyed|crazy|extreme|lots") ? 24 : subtle ? 5 : 12; string[] shN = { "Soft Saturation", "Tube Saturation", "Tape Saturation", "Overdrive", "Hard Clipper", "Soft Clipper", "Fuzz", "Wavefolder", "Waveshaper", "Waveshaper", "Fuzz", "Waveshaper" }; st.Add(Lib("shaper", shN[sm]) + "{mode=" + sm + ";drive=" + F(d) + ";tone=" + (dark ? 6000 : bright ? 18000 : 12000) + ";auto=1;mix=100}"); said(char.ToUpper(smn[sm][0]) + smn[sm].Substring(1) + " drive (" + F(d) + " dB)"); fx = true; }
        if (Has(p, "amp|guitar amp|amplifier|cabinet")) { st.Add(Lib("amp") + "{pre=" + (Has(p, "clean") ? 0 : Has(p, "high gain|metal") ? 3 : 1) + ";cab=" + (Has(p, "bass") ? 4 : 3) + ";gain=" + F(18 * a) + ";level=-6}"); said("Guitar amp and cabinet"); fx = true; }
        if (Has(p, "bitcrush|bitcrusher|bit crush|bitcrushed|8 bit|8bit|crushed|lofi|lo fi|retro game")) { int bits = Has(p, "8 bit|8bit") ? 8 : Has(p, "lofi|lo fi") ? 10 : 6; st.Add(Lib("crush") + "{bits=" + bits + ";rate=" + (Has(p, "lofi|lo fi") ? 16000 : 11025) + ";mix=" + (subtle ? 50 : 100) + "}"); said(bits + "-bit crush"); fx = true; }
        if (Has(p, "vinyl|record crackle")) { st.Add(Lib("band", "Vintage Speaker") + "{hp=120;lp=9000;drive=4;crackle=45;noise=15}"); said("Vinyl (crackle, softer top)"); fx = true; }
        // 7. EQ and dynamics
        var g = new double[10]; bool eqAny = false; Action<int, double> eqB = (b, v) => { g[b] += v * a; eqAny = true; };
        if (Has(p, "bass boost|more bass|boost the bass|bassier|bass up|low end|808 boost")) { eqB(0, 5); eqB(1, 4); eqB(2, 1); said("More bass"); }
        if (Has(p, "less bass|cut bass|cut the bass|thinner|thin out|no rumble|rumble")) { eqB(0, -8); eqB(1, -5); eqB(2, -1); said("Less bass"); }
        if (Has(p, "mud|muddy|cut the mud|clean up")) { eqB(3, -4); eqB(4, -1); said("Mud cut (250 Hz)"); }
        if (Has(p, "boxy")) { eqB(4, -4); said("Boxiness cut (500 Hz)"); }
        if (Has(p, "nasal|honky")) { eqB(5, -4); said("Nasal cut (1 kHz)"); }
        if (Has(p, "presence|clarity|clear|clearer|cut through|upfront|forward")) { eqB(6, 3); eqB(7, 3); said("Presence (2 to 4 kHz)"); }
        if (Has(p, "harsh|harshness|piercing")) { eqB(7, -4); eqB(8, -1); said("Harshness cut (4 kHz)"); }
        if (Has(p, "air|airy|sparkle|sparkly|shine|shiny")) { eqB(8, 3); eqB(9, 5); said("Air (8 to 16 kHz)"); }
        bool hasSpace = Has(p, "reverb|verb|hall|plate|room|cathedral|church|chamber|spring|ambient|space|delay|echo|echoes|slapback");
        if (Has(p, "eq|equalizer|equaliser|tone") || (!hasSpace && !fx && !eqAny && (dark || bright)))
        {
            if (dark) { eqB(7, -2); eqB(8, -4); eqB(9, -6); eqB(2, 1); said("Darker"); } else if (bright) { eqB(7, 2); eqB(8, 3); eqB(9, 4); said("Brighter"); } else if (!eqAny) { eqB(1, 1); eqB(6, 1); eqB(9, 2); said("A gentle smile EQ"); }
        }
        if (eqAny) { var sb = new StringBuilder(Lib("geq") + "{"); for (int b = 0; b < 10; b++) sb.Append("g" + b + "=" + F(Math.Max(-15, Math.Min(15, Math.Round(g[b] * 2) / 2))) + ";"); sb.Append("out=0}"); st.Add(sb.ToString()); fx = true; }
        if (Has(p, "compress|compressor|compression|squash|squashed|glue|level out|even out|punchy|punch|parallel|new york"))
        {
            var rm = Regex.Match(p, @" (\d+(?:\.\d+)?) ?(?::|to) ?1 "); double ratio = rm.Success ? double.Parse(rm.Groups[1].Value, CultureInfo.InvariantCulture) : Has(p, "glue|gentle") ? 2 : Has(p, "squash|heavy|hard") ? 8 : 4;
            double att = Has(p, "fast attack|fast|snappy") ? 1 : Has(p, "slow attack|slow|punchy|punch") ? 30 : 10, rel = Has(p, "slow release") ? 300 : Has(p, "fast release") ? 50 : 120; bool par = Has(p, "parallel|new york");
            st.Add("comp{thr=" + F(par ? -34 : Has(p, "glue|gentle|light") ? -14 : -22) + ";ratio=" + F(ratio) + ";att=" + F(att) + ";rel=" + F(rel) + ";knee=6;makeup=" + F(par ? 8 : 3) + (par ? ";mix=40" : "") + "}");
            said((par ? "Parallel compression" : "Compressor") + " " + F(ratio) + ":1, attack " + F(att) + " ms, release " + F(rel) + " ms"); fx = true;
        }
        if (Has(p, "ott|multiband squash|upward")) { st.Add(Lib("ott") + "{depth=" + F(Math.Min(100, 60 * a)) + "}"); said("OTT squash"); fx = true; }
        if (Has(p, "gate|noise gate")) { st.Add("gate{thr=" + (Has(p, "hard|tight") ? -32 : -45) + ";range=60;att=1;hold=40;rel=100}"); said("Noise gate"); fx = true; }
        if (Has(p, "de ess|deess|de esser|deesser|sibilance|esses|harsh s")) { st.Add("deess{freq=6500;thr=-30;range=" + F(10 * a) + "}"); said("De-esser"); fx = true; }
        if (Has(p, "transient|snap|snappier|more attack")) { st.Add(Lib("transient") + "{attack=" + F(6 * a) + ";sustain=0}"); said("Transient shaper (more attack)"); fx = true; }
        // 8. modulation
        if (Has(p, "chorus|lush")) { st.Add("chorus{rate=" + F(Has(p, "fast") ? 2 : 0.7) + ";depth=" + F(2.5 * a) + ";delay=" + (Has(p, "lush") ? 20 : 12) + ";spread=90;mix=" + F(subtle ? 25 : 45) + "}"); said("Chorus"); fx = true; }
        if (Has(p, "phaser|phase|phasing")) { st.Add(Lib("phaser") + "{stages=" + (Has(p, "deep|heavy") ? 3 : 2) + ";rate=" + F(Has(p, "fast") ? 2 : Has(p, "slow") ? 0.15 : 0.4) + ";fb=" + F(Has(p, "resonant|heavy") ? 80 : 45) + ";mix=50}"); said("Phaser"); fx = true; }
        if (Has(p, "flanger|flange|jet")) { st.Add(Lib("flanger") + "{rate=" + F(Has(p, "fast") ? 1.5 : 0.25) + ";fb=" + F(Has(p, "jet|heavy") ? 75 : 55) + ";barber=" + (Has(p, "endless|barber|rising") ? 1 : 0) + ";mix=45}"); said("Flanger"); fx = true; }
        int modT = Has(p, "tremolo|chop the volume|volume chop|gated") && !Has(p, "reverb") ? 0 : Has(p, "auto pan|autopan|panning|pan around|ping pong pan") ? 1 : Has(p, "filter sweep|auto filter|autofilter|sweep|filter") ? 2 : Has(p, "vibrato|wobble the pitch|wobbly") ? 4 : -1;
        if (modT >= 0) { int sy = Sync(p, modT == 4 ? 0 : 7, rnd); string[] tn = { "Tremolo", "Auto-pan", "Auto-filter", "", "Vibrato" }; string[] mdN = { "Auto-Tremolo", "Auto-Pan", "Auto Filter", "", "LFO Modulator" }; st.Add(Lib("mod", mdN[modT]) + "{src=0;tgt=" + modT + ";shape=" + (Has(p, "square|choppy|chop") ? 4 : Has(p, "random") ? 5 : Has(p, "saw") ? 2 : 0) + ";sync=" + sy + ";rate=" + F(Has(p, "fast") ? 6 : Has(p, "slow") ? 0.5 : 2) + ";depth=" + F(modT == 4 ? 25 * a : Math.Min(100, 80 * a)) + "}"); said(tn[modT] + (sy > 0 ? " on " + SyncName[sy] + " notes" : "")); fx = true; }
        // 9. rhythm
        if (Has(p, "stutter|repeat|beat repeat|repeats")) { int sy = Sync(p, 10, rnd); st.Add(Lib("repeat") + "{sync=" + sy + ";mode=" + (Has(p, "always|constant") ? 0 : Has(p, "end|last beat") ? 2 : 1) + ";chance=" + F(Math.Min(100, 50 * a)) + "}"); said("Stutter repeats on " + SyncName[sy] + " notes"); fx = true; }
        if (Has(p, "glitch|glitchy|chop|chops|chopped")) { int sy = Sync(p, 10, rnd); st.Add(Lib("chop") + "{sync=" + sy + ";pmute=" + F(20 * a) + ";prep=" + F(30 * a) + ";prev=" + F(15 * a) + ";ppit=" + F(15 * a) + "}"); said("Glitch chops on " + SyncName[sy] + " notes"); fx = true; }
        if (Has(p, "tape stop|tapestop|slow down|power down")) { st.Add(Lib("tapestop") + "{auto=1;every=" + (Has(p, "every bar") ? 4 : 8) + ";stop=" + F(Has(p, "slow") ? 1500 : 700) + "}"); said("Tape stop every " + (Has(p, "every bar") ? 4 : 8) + " beats"); fx = true; }
        if (Has(p, "granular|grains|grain|cloud")) { st.Add(Lib("grain") + "{size=" + (Has(p, "small|tiny") ? 40 : Has(p, "big|long") ? 400 : 120) + ";pitch=" + (Has(p, "octave up") ? 12 : 0) + ";rev=" + (Has(p, "reverse|backwards") ? 100 : 0) + ";dry=30;mix=100}"); said("Granular cloud"); fx = true; }
        // 10. width
        if (Has(p, "8d|8 d|around your head|spinning|orbit")) { st.Add("8d{period=" + (Has(p, "fast") ? 4 : Has(p, "slow") ? 16 : 8) + ";mix=100}"); said("8D (circles your head)"); fx = true; }
        if (Has(p, "mono|narrow|narrower")) { st.Add("widener{width=55;bass=120}"); said("Narrower"); fx = true; } else if (Has(p, "wide|wider|widen|stereo|widener")) { st.Add("widener{width=" + F(Math.Min(200, 150 * a)) + ";bass=120}"); said("Wider stereo"); fx = true; }
        // 11. delay and echo
        if (Has(p, "delay|echo|echoes|delays|slapback|slap|throw"))
        {
            if (Has(p, "slapback|slap")) { st.Add(Lib("echo", "Slapback Echo") + "{mode=0;sync=0;time=" + (Has(p, "long") ? 160 : 100) + ";fb=8;hc=" + (dark ? 3500 : 6500) + ";sat=" + (Has(p, "tape|vintage") ? 35 : 0) + ";mix=26}"); said("Slapback echo"); }
            else if (Has(p, "throw|throws|end of the bar|last word")) { int sy = Sync(p, 7, rnd); st.Add(Lib("worldecho") + "{style=0;sync=" + sy + ";send=1;fb=45}"); said("Throw echo on the last beat of each bar (" + SyncName[sy] + ")"); }
            else
            {
                int mode = Has(p, "ping pong|pingpong|ping") ? 2 : Has(p, "multi tap|multitap") ? 3 : Has(p, "reverse|reversed|backwards") ? 4 : Has(p, "mono") ? 0 : Has(p, "stereo") ? 1 : 2;
                int sy = Sync(p, 8, rnd); double fbk = Has(p, "endless|infinite|forever") ? 85 : Has(p, "long|long tail") ? 62 : Has(p, "short") ? 15 : 35;
                string tone = Has(p, "lofi|lo fi") ? ";lc=400;hc=3000;sat=50;wow=50" : Has(p, "tape|vintage|analog|analogue") ? ";lc=200;hc=4500;sat=30;wow=25" : dark ? ";lc=150;hc=2500" : bright ? ";lc=400;hc=14000" : ";lc=150;hc=7000";
                string[] dlN = { "Mono Delay", "Stereo Delay", "Ping-Pong Delay", "Multi-Tap Delay", "Reverse Delay" }; string dlB = Has(p, "duck|ducked|ducking") && mode != 2 ? "Duck Delay" : Has(p, "tape|vintage|analog|analogue") && mode < 2 ? "Tape Echo" : dlN[mode];
                st.Add(Lib("echo", dlB) + "{mode=" + mode + ";sync=" + sy + ";fb=" + F(fbk) + tone + (Has(p, "duck|ducked|ducking") ? ";duck=60" : "") + (Has(p, "spacey|spacy|reverb") ? ";rev=30;diff=40" : "") + ";mix=" + F(wet ? 42 : subtle ? 18 : 28) + "}");
                string[] mn = { "Mono", "Stereo", "Ping-pong", "Multi-tap", "Reverse" }; said(mn[mode] + " delay on " + SyncName[sy] + " notes, feedback " + F(fbk) + "%" + (Has(p, "duck|ducked") ? ", ducked under the vocal" : ""));
            }
            fx = true;
        }
        // 12. reverb and real spaces
        if (Has(p, "bathroom|tiled|metal tank|tank|cardboard|tube|phone booth|booth|guitar cabinet"))
        {
            int ir = Has(p, "bathroom|tiled") ? 6 : Has(p, "tank") ? 7 : Has(p, "booth") ? 8 : Has(p, "cabinet") ? 9 : Has(p, "cardboard|tube") ? 11 : 1; st.Add(Lib("conv") + "{ir=" + ir + ";len=100;mix=" + F(wet ? 50 : 30) + "}"); said("A real recorded space (convolution)"); fx = true;
        }
        else if (Has(p, "reverb|verb|hall|plate|room|cathedral|church|chamber|spring|ambient|ambience|space|spacey|atmosphere|shimmer"))
        {
            int mode = Has(p, "cathedral|church") ? 5 : Has(p, "hall|concert") ? 1 : Has(p, "plate") ? 2 : Has(p, "chamber") ? 3 : Has(p, "spring") ? 4 : Has(p, "ambient|ambience|space|spacey|atmosphere") ? 6 : Has(p, "room") ? 0 : 1;
            double[] baseD = { 1.0, 2.6, 1.8, 1.6, 1.5, 4.5, 3.2 }; double size = Has(p, "tiny") ? 15 : Has(p, "small|short|tight") ? 30 : Has(p, "huge|massive|giant|enormous|infinite") ? 92 : Has(p, "big|large|long") ? 72 : 50; double dm = size / 50.0; dm = dm * dm;
            string ch = Has(p, "gated|gate") ? ";gate=1;gthr=-30;ghold=180;grel=40" : Has(p, "reverse|reversed|backwards") ? ";rev=1;rtime=900" : Has(p, "shimmer|heavenly|angelic") ? ";shim=45;shimst=12" : Has(p, "freeze|frozen") ? ";freeze=1" : "";
            if (Has(p, "duck|ducked|ducking")) ch += ";duck=60"; if (Has(p, "modulated|lush|chorused")) ch += ";mod=70";
            double mix = Has(p, "100%|fully wet|send|all wet") ? 100 : wet ? 36 : subtle ? 14 : 24;
            string[] rvN = { "Room Reverb", "Hall Reverb", "Plate Reverb", "Chamber Reverb", "Spring Reverb", "Hall Reverb", "Ambient Reverb" }; string rvB = ch.Contains("gate=1") ? "Gated Reverb" : ch.Contains("rev=1") ? "Reverse Reverb" : ch.Contains("shim=") ? "Shimmer Reverb" : ch.Contains("duck=") ? "Reverb Ducking" : dark && mode != 5 ? "Dark Reverb" : rvN[mode];
            st.Add(Lib("reverb2", rvB) + "{mode=" + mode + ";size=" + F(size) + ";decay=" + F(Math.Min(30, baseD[mode] * dm)) + ";pre=" + F(size / 3) + ";damp=" + (dark ? 3800 : bright ? 14000 : 7500) + ";hc=" + (dark ? 6000 : bright ? 17000 : 11000) + ch + ";mix=" + F(mix) + "}");
            string[] mn = { "Room", "Hall", "Plate", "Chamber", "Spring", "Cathedral", "Ambient" };
            said((size >= 90 ? "Huge " : size >= 70 ? "Big " : size <= 30 ? "Small " : "") + mn[mode].ToLowerInvariant() + " reverb" + (dark ? ", dark" : bright ? ", bright" : "") + (ch.Contains("gate=1") ? ", gated" : ch.Contains("rev=1") ? ", reversed" : ch.Contains("shim") ? ", with a shimmer" : "") + ", " + F(mix) + "% wet"); fx = true;
        }
        // 13. nothing recognised: the closest names in the EFFECT LIBRARY
        if (!fx)
        {
            var words = p.Trim().Split(' ').Where(w => w.Length >= 3).ToArray(); PluginLibrary.Variant best = null; int bs = 0;
            foreach (var v in PluginLibrary.All) { if (!PluginLibrary.IsListed(v)) continue; string n = v.Name.ToLowerInvariant(); int sc = words.Count(w => n.Contains(w)); if (sc > bs) { bs = sc; best = v; } }
            if (best != null) { st.Add("@" + best.Name); said("Closest effect in the library: " + best.Name); }
            else { st.Add(Lib("reverb2") + "{mode=1;size=50;mix=24}"); said("No effect word found: a medium hall reverb to start from (try words like reverb, delay, autotune, distortion, telephone)"); }
        }
        if (rnd != null) said("Another take: the same effect with its amounts nudged"); r.Chain = AiVary.Jitter(string.Join(" > ", st.ToArray()), rnd); r.BusName = null;
        string title = (prompt ?? "").Trim(); if (title.Length > 42) title = title.Substring(0, 42).TrimEnd() + "..."; r.Name = "AI FX: " + (title.Length > 0 ? char.ToUpper(title[0]) + title.Substring(1) : "Effect");
        return r;
    }
}
