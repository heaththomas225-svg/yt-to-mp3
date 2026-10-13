using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

// ======================================================================
//  Rapper Vocal Styles: ready-made plugin chains for rap vocals, grouped by subgenre.
//  Every style is original sound design made from the plugins in this app (no artist's settings are copied).
//  A chain is written  stage > stage > stage ; a stage is  type{id=value;id=value}  or  @Plugin name{id=value}  for a named library plugin.
// ======================================================================
static class VocalStyles
{
    public class Style { public string Name, Genre, Blurb, Chain; }
    public static readonly string[] Genres = { "Trap", "Melodic Trap", "Drill", "Boom Bap", "Old School", "Grime & UK", "Cloud & Lo-Fi", "Rage & Hyper", "Pop Rap", "Phonk", "Storytelling" };
    public static readonly List<Style> All = new List<Style>();
    static void S(string genre, string name, string blurb, string chain) { All.Add(new Style { Genre = genre, Name = name, Blurb = blurb, Chain = chain }); }

    // building blocks used again and again (kept short so the table stays readable)
    const string BrightEq = "eq{hp_on=1;hp_f=110;hp_s=1;b1_f=290;b1_g=-3;b1_q=1.2;b3_f=3400;b3_g=3;b3_q=0.9;b5_f=11000;b5_g=3}";
    const string WarmEq = "eq{hp_on=1;hp_f=85;hp_s=1;b0_f=120;b0_g=2;b1_f=260;b1_g=1;b3_f=3000;b3_g=1.2;b4_f=6000;b4_g=-1;lp_on=1;lp_f=15000}";
    const string ClearEq = "eq{hp_on=1;hp_f=90;hp_s=1;b1_f=320;b1_g=-2.5;b1_q=1.2;b2_f=900;b2_g=-1;b3_f=3200;b3_g=2.5;b3_q=0.9;b4_f=6500;b4_g=1.5;b5_f=11000;b5_g=2.5}";

    static VocalStyles()
    {
        // ---- Trap ----
        S("Trap", "Trap Lead", "Tight tuning, bright top, fast compression and a short ad-lib echo: forward and modern.", "pitch{speed=22;strength=85;natural=30} > " + BrightEq + " > comp{thr=-24;ratio=4;att=6;rel=80;knee=6;makeup=5} > deess{freq=7000;thr=-30;range=8} > sat{mode=0;drive=5;mix=35} > @Rap Ad-Lib Echo{mix=20}");
        S("Trap", "Dark Trap", "A dim, heavy vocal: lows kept, highs rolled off, tube warmth and a dark echo behind it.", "eq{hp_on=1;hp_f=100;hp_s=1;b1_f=250;b1_g=1;b3_f=3000;b3_g=-1;b4_f=6500;b4_g=-3;lp_on=1;lp_f=13000} > comp{thr=-26;ratio=4.5;att=8;rel=100;knee=6;makeup=6} > sat{mode=1;drive=8;mix=55} > reverb{size=60;damp=70;pre=20;mix=14} > @Dark Echo{mix=22}");
        S("Trap", "Trap Ad-Libs", "Bright, squeezed ad-libs that bounce in the second half of every bar.", "eq{hp_on=1;hp_f=260;hp_s=1;b3_f=4200;b3_g=3;b3_q=0.9;b5_f=11000;b5_g=2} > comp{thr=-20;ratio=3;att=6;rel=90;knee=6;makeup=3} > @Rap Ad-Lib Echo{mix=45} > reverb{size=45;damp=50;pre=22;mix=20}");
        S("Trap", "Mumble Trap", "Thick, close and heavily compressed with the top softened: the words melt into the beat.", "eq{hp_on=1;hp_f=120;hp_s=1;b1_f=350;b1_g=1.5;b3_f=3200;b3_g=-1.5;lp_on=1;lp_f=11000} > comp{thr=-28;ratio=6;att=10;rel=70;knee=6;makeup=8} > sat{mode=0;drive=9;mix=60} > reverb{size=30;damp=70;pre=10;mix=10}");
        // ---- Melodic Trap ----
        S("Melodic Trap", "Melodic Trap Hook", "Quick tuning to a minor pentatonic, a quiet octave on top and a wide echo: a modern sing-rap hook.", "@Melody Tune: Trap Melody > " + BrightEq + " > comp{thr=-22;ratio=3.5;att=8;rel=90;knee=6;makeup=4} > exciter{drive=8;harm=40;flo=5000;fhi=16000;mix=25} > @Worldwide Echo{mix=28}");
        S("Melodic Trap", "Autotune Glide", "A softer tune that glides between notes, with a gentle third harmony and a plate.", "@Melody Tune: Soft Harmony > " + ClearEq + " > comp{thr=-22;ratio=2.5;att=15;rel=140;knee=8;makeup=3} > sat{mode=0;drive=4;mix=35} > reverb{size=55;damp=40;pre=20;mix=18}");
        S("Melodic Trap", "Dreamy Melodic", "Floating and wide: tuned, chorused, airy echoes and a long soft reverb.", "@Melody Tune{speed=40;strength=90} > " + ClearEq + " > comp{thr=-22;ratio=3;att=10;rel=110;knee=8;makeup=3} > chorus{rate=0.6;depth=4;delay=14;spread=80;mix=30} > @Airy Echo{mix=30} > reverb{size=75;damp=45;pre=30;mix=22}");
        S("Melodic Trap", "Stacked Hook", "A lead with a small choir behind it (third, fifth and octave), widened and set in a big room.", "@Melody Tune: Choir Stack > " + ClearEq + " > comp{thr=-22;ratio=3;att=10;rel=100;knee=8;makeup=3} > widener{width=125;bass=120} > reverb{size=65;damp=45;pre=25;mix=22}");
        // ---- Drill ----
        S("Drill", "UK Drill Cold", "Cold, dry and close: low cut high, strong presence, hard compression and almost no space.", "eq{hp_on=1;hp_f=140;hp_s=1;b1_f=300;b1_g=-3;b1_q=1.2;b3_f=3500;b3_g=3;b3_q=0.9;b5_f=9000;b5_g=1} > comp{thr=-26;ratio=5;att=4;rel=70;knee=6;makeup=6} > deess{freq=6500;thr=-28;range=8} > sat{mode=2;drive=4;mix=25} > reverb{size=18;damp=70;pre=5;mix=8}");
        S("Drill", "Brooklyn Drill Lead", "Dry and punchy with a touch of grit and a short slap behind the words.", "eq{hp_on=1;hp_f=120;hp_s=1;b2_f=900;b2_g=-1;b3_f=3000;b3_g=2.5;b3_q=0.9} > comp{thr=-24;ratio=4;att=5;rel=75;knee=6;makeup=5} > exciter{drive=7;harm=45;flo=3500;fhi=14000;mix=25} > reverb{size=25;damp=60;pre=8;mix=10} > delay{mode=0;sync=0;time=110;fb=12;mix=14}");
        S("Drill", "Drill Whisper", "Hushed and tight: dark top, very close compression and a quiet dark echo.", "eq{hp_on=1;hp_f=200;hp_s=1;lp_on=1;lp_f=9000} > comp{thr=-30;ratio=6;att=3;rel=60;knee=6;makeup=9} > reverb{size=35;damp=80;pre=15;mix=16} > @Dark Echo{mix=18}");
        S("Drill", "Drill Ad-Libs", "Thin, bright ad-libs thrown on the last beat of each bar through a phone-line echo.", "eq{hp_on=1;hp_f=300;hp_s=1;b3_f=4000;b3_g=3} > comp{thr=-22;ratio=4;att=5;rel=80;knee=6;makeup=4} > @Telephone Throw{mix=45}");
        // ---- Boom Bap ----
        S("Boom Bap", "Boom Bap Classic", "Warm and mid-forward with gentle compression, a little tape and a small room.", WarmEq + " > comp{thr=-20;ratio=3;att=18;rel=140;knee=8;makeup=4} > sat{mode=0;drive=5;mix=45} > reverb{size=22;damp=60;pre=8;mix=8}");
        S("Boom Bap", "Dusty Vinyl Vocal", "Band-limited and crunchy, like a vocal cut to an old record.", "eq{hp_on=1;hp_f=250;hp_s=1;b2_f=1200;b2_g=2;lp_on=1;lp_f=6500} > comp{thr=-22;ratio=3.5;att=12;rel=120;knee=8;makeup=5} > sat{mode=2;drive=8;mix=40} > reverb{size=20;damp=75;pre=6;mix=10}");
        S("Boom Bap", "Punchy Mic", "Direct, punchy and close: fast compression with a bright edge.", "eq{hp_on=1;hp_f=95;hp_s=1;b1_f=280;b1_g=-2;b3_f=3500;b3_g=2.5;b4_f=7000;b4_g=1.5} > comp{thr=-26;ratio=4;att=6;rel=80;knee=6;makeup=6} > deess{freq=6800;thr=-28;range=7} > reverb{size=15;damp=65;pre=5;mix=6}");
        S("Boom Bap", "90s Studio Plate", "A classic studio vocal: even compression and a bright plate reverb.", ClearEq + " > comp{thr=-21;ratio=3;att=14;rel=130;knee=8;makeup=4} > deess{freq=6800;thr=-26;range=6} > reverb{size=55;damp=40;pre=20;mix=18}");
        // ---- Old School ----
        S("Old School", "Old School Party", "Mid-focused and lively with a slap echo, made to carry over a crowd.", "eq{hp_on=1;hp_f=120;hp_s=1;b3_f=2500;b3_g=3;b4_f=5500;b4_g=2} > comp{thr=-20;ratio=3;att=14;rel=120;knee=8;makeup=4} > delay{mode=0;sync=0;time=95;fb=10;mix=22} > reverb{size=30;damp=50;pre=10;mix=12}");
        S("Old School", "Cassette Rap", "Tape-saturated and a little dark, with a wobbly tape echo.", "eq{hp_on=1;hp_f=110;hp_s=1;b2_f=1000;b2_g=1.5;lp_on=1;lp_f=9500} > comp{thr=-20;ratio=3;att=16;rel=140;knee=8;makeup=4} > sat{mode=0;drive=8;mix=70} > @Tape Echo{mix=22} > reverb{size=25;damp=65;pre=8;mix=8}");
        // ---- Grime & UK ----
        S("Grime & UK", "Grime MC", "Aggressive and right in your face: strong presence, hard compression and a gritty edge.", "eq{hp_on=1;hp_f=130;hp_s=1;b1_f=300;b1_g=-2;b3_f=3200;b3_g=3.5;b4_f=6000;b4_g=2} > comp{thr=-26;ratio=5;att=5;rel=70;knee=6;makeup=7} > exciter{drive=16;harm=80;flo=3000;fhi=16000;mix=55} > sat{mode=2;drive=8;mix=40} > reverb{size=15;damp=70;pre=5;mix=6}");
        S("Grime & UK", "UK Rap Clean", "Clear, controlled and polished: gentle tuning, strong de-essing, a little space.", "pitch{speed=70;strength=45;natural=70} > " + ClearEq + " > comp{thr=-23;ratio=3.5;att=8;rel=100;knee=6;makeup=4} > deess{freq=7000;thr=-30;range=12} > reverb{size=25;damp=60;pre=10;mix=8}");
        S("Grime & UK", "Road Rap Dry", "Completely dry and tight: gate, hard compression, no reverb at all.", "gate{thr=-46;range=30;att=1;hold=60;rel=150} > eq{hp_on=1;hp_f=130;hp_s=1;b3_f=3300;b3_g=3} > comp{thr=-27;ratio=6;att=3;rel=60;knee=4;makeup=8}");
        // ---- Cloud & Lo-Fi ----
        S("Cloud & Lo-Fi", "Cloud Rap Haze", "A soft, chorused vocal drowned in long reverb and endless echo.", "eq{hp_on=1;hp_f=120;hp_s=1;lp_on=1;lp_f=10000} > comp{thr=-22;ratio=3;att=14;rel=130;knee=8;makeup=3} > chorus{rate=0.4;depth=3;delay=15;spread=90;mix=40} > reverb{size=85;damp=60;pre=30;mix=40} > @Infinite Space{mix=22}");
        S("Cloud & Lo-Fi", "Lo-Fi Bedroom", "Narrow and warm like a vocal recorded in a bedroom, with tube colour and a tiny room.", "eq{hp_on=1;hp_f=200;hp_s=1;lp_on=1;lp_f=7000} > comp{thr=-22;ratio=3;att=15;rel=130;knee=8;makeup=4} > sat{mode=1;drive=6;mix=60} > reverb{size=25;damp=75;pre=8;mix=12}");
        S("Cloud & Lo-Fi", "Underwater", "Dark and swirling: the top is rolled off, chorus wobbles it and a huge dark reverb surrounds it.", "eq{hp_on=1;hp_f=110;hp_s=1;lp_on=1;lp_f=5000} > chorus{rate=0.5;depth=5;delay=16;spread=100;mix=45} > reverb{size=90;damp=75;pre=25;mix=38}");
        // ---- Rage & Hyper ----
        S("Rage & Hyper", "Rage Vocal", "Distorted, wide and loud: grit first, bright top, hard compression, a short room.", "dist{mode=0;drive=14;mix=45;level=-6} > eq{hp_on=1;hp_f=140;hp_s=1;b3_f=3800;b3_g=3;b5_f=11000;b5_g=4} > comp{thr=-26;ratio=5;att=5;rel=70;knee=6;makeup=7} > widener{width=130;bass=120} > reverb{size=25;damp=55;pre=8;mix=10}");
        S("Rage & Hyper", "Hyper Rap", "Snapped to the notes, very bright and shimmering, with chorus and a quick echo.", "pitch{speed=0;strength=100;natural=0} > eq{hp_on=1;hp_f=140;hp_s=1;b3_f=4500;b3_g=3;b5_f=12000;b5_g=5} > comp{thr=-24;ratio=4;att=5;rel=80;knee=6;makeup=5} > exciter{drive=12;harm=60;flo=4000;fhi=18000;mix=40} > chorus{rate=0.8;depth=3;delay=12;spread=90;mix=30} > delay{mode=2;sync=2;fb=28;hc=7000;mix=18}");
        S("Rage & Hyper", "Glitch Throw", "A bright, tight vocal with only the end of each bar thrown into a thin echo.", "pitch{speed=10;strength=100;natural=10} > " + BrightEq + " > comp{thr=-24;ratio=4;att=5;rel=80;knee=6;makeup=5} > @Telephone Throw{mix=40}");
        // ---- Pop Rap ----
        S("Pop Rap", "Pop Rap Polish", "Clean, glossy and wide: soft tuning, air on top, subtle width and a plate.", "pitch{speed=60;strength=60;natural=60} > " + ClearEq + " > comp{thr=-22;ratio=3;att=10;rel=110;knee=8;makeup=4} > deess{freq=6800;thr=-27;range=8} > exciter{drive=6;harm=40;flo=6000;fhi=18000;mix=30} > widener{width=115;bass=120} > reverb{size=50;damp=40;pre=20;mix=16}");
        S("Pop Rap", "Radio Ready", "Even and controlled with warmth on top: made to sit in a busy pop mix.", WarmEq + " > comp{thr=-24;ratio=3.5;att=8;rel=100;knee=6;makeup=5} > deess{freq=7000;thr=-28;range=8} > sat{mode=0;drive=3;mix=30} > reverb{size=35;damp=45;pre=15;mix=12}");
        S("Pop Rap", "Hook Sheen", "A shiny, lightly tuned hook voice with airy echoes.", "@Melody Tune{speed=35;strength=85} > " + ClearEq + " > comp{thr=-22;ratio=3;att=10;rel=110;knee=8;makeup=3} > exciter{drive=6;harm=40;flo=6000;fhi=18000;mix=30} > @Airy Echo{mix=26}");
        // ---- Phonk ----
        S("Phonk", "Memphis Phonk", "Narrow, distorted and dark like an old tape, with a dark echo.", "eq{hp_on=1;hp_f=300;hp_s=1;lp_on=1;lp_f=5500} > dist{mode=0;drive=12;mix=50;level=-6} > comp{thr=-24;ratio=4;att=8;rel=100;knee=6;makeup=6} > reverb{size=45;damp=75;pre=12;mix=20} > @Dark Echo{mix=22}");
        S("Phonk", "Phonk Chant", "A repeating, ghostly vocal: short slapback plus a wide cave reverb.", "eq{hp_on=1;hp_f=220;hp_s=1;b3_f=2800;b3_g=2;lp_on=1;lp_f=8000} > comp{thr=-25;ratio=5;att=6;rel=90;knee=6;makeup=6} > sat{mode=2;drive=7;mix=40} > delay{mode=2;sync=3;fb=40;hc=3500;mix=30} > reverb{size=70;damp=70;pre=20;mix=26}");
        // ---- Storytelling ----
        S("Storytelling", "Storyteller Close Mic", "Intimate and natural: warm lows, clear words and almost no effect on top.", "eq{hp_on=1;hp_f=80;hp_s=1;b0_f=120;b0_g=2;b3_f=3000;b3_g=1.5} > comp{thr=-22;ratio=2.5;att=15;rel=140;knee=8;makeup=3} > deess{freq=6500;thr=-26;range=6} > reverb{size=20;damp=60;pre=8;mix=7}");
        S("Storytelling", "Intimate Confession", "Soft and close with tube warmth and a small, quiet room: made for low, personal verses.", "eq{hp_on=1;hp_f=85;hp_s=1;b0_f=140;b0_g=2.5;b4_f=6500;b4_g=-1.5;lp_on=1;lp_f=14000} > comp{thr=-24;ratio=3;att=18;rel=150;knee=10;makeup=4} > sat{mode=1;drive=6;mix=50} > reverb{size=30;damp=60;pre=12;mix=10}");
        S("Storytelling", "Spoken Word Clear", "Very clear, un-coloured speech: gentle compression, strong de-essing.", ClearEq + " > comp{thr=-23;ratio=2.5;att=15;rel=140;knee=8;makeup=3} > deess{freq=7000;thr=-30;range=10}");
        S("Storytelling", "Slow Burn", "A reflective, spacious vocal with a long reverb and a slow quarter-note echo.", WarmEq + " > comp{thr=-22;ratio=3;att=15;rel=140;knee=8;makeup=3} > delay{mode=0;sync=4;fb=30;hc=4000;mix=16} > reverb{size=75;damp=45;pre=30;mix=22}");
    }

    // build the plugins of a style (null entries are left out: a plugin that is not installed never breaks the style)
    public static List<Fx> Build(Style s)
    {
        var l = new List<Fx>();
        foreach (var part in s.Chain.Split(new[] { " > " }, StringSplitOptions.RemoveEmptyEntries))
        {
            string st = part.Trim(), type = st, data = ""; int b = st.IndexOf('{'); if (b >= 0) { type = st.Substring(0, b).Trim(); int e = st.LastIndexOf('}'); data = st.Substring(b + 1, (e > b ? e : st.Length) - b - 1); }
            if (type.StartsWith("@")) type = PluginLibrary.IdFor(type.Substring(1));
            var f = Presets.Make(type, data); if (f != null) l.Add(f);
        }
        return l;
    }
    // every id used in a stage must really exist on that plugin (a typo would otherwise do nothing, silently): returns the problems found
    public static List<string> Check(Style s)
    {
        var bad = new List<string>();
        foreach (var part in s.Chain.Split(new[] { " > " }, StringSplitOptions.RemoveEmptyEntries))
        {
            string st = part.Trim(), type = st, data = ""; int b = st.IndexOf('{'); if (b >= 0) { type = st.Substring(0, b).Trim(); int e = st.LastIndexOf('}'); data = st.Substring(b + 1, (e > b ? e : st.Length) - b - 1); }
            if (type.StartsWith("@")) type = PluginLibrary.IdFor(type.Substring(1));
            var f = FxRegistry.Create(type); if (f == null) { bad.Add(s.Name + ": no plugin " + type); continue; }
            foreach (var tok in data.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) { int eq = tok.IndexOf('='); if (eq < 1) { bad.Add(s.Name + ": bad setting " + tok); continue; } string id = tok.Substring(0, eq).Trim(); if (f.Find(id) == null) bad.Add(s.Name + ": " + f.Title + " has no setting " + id); }
        }
        return bad;
    }

    // ---- favourites (kept in a small text file) ----
    public static string FavFile = null;
    static HashSet<string> fav; static string favLoaded;
    static string FavPath { get { return FavFile ?? Path.Combine(StudioEnv.DataDir, "vocal-style-favourites.txt"); } }
    static HashSet<string> Fav()
    {
        string p = FavPath; if (fav != null && favLoaded == p) return fav;
        fav = new HashSet<string>(StringComparer.OrdinalIgnoreCase); favLoaded = p;
        try { if (File.Exists(p)) foreach (var line in File.ReadAllLines(p)) if (line.Trim().Length > 0) fav.Add(line.Trim()); } catch { }
        return fav;
    }
    public static bool IsFav(string name) { return Fav().Contains(name); }
    public static void ToggleFav(string name)
    {
        var f = Fav(); if (!f.Remove(name)) f.Add(name);
        try { Directory.CreateDirectory(Path.GetDirectoryName(FavPath)); File.WriteAllLines(FavPath, new List<string>(f).ToArray()); } catch { }
    }

    // the styles that match a search, a subgenre ("" = all) and the favourites filter
    public static List<Style> Find(string query, string genre, bool onlyFav)
    {
        var res = new List<Style>(); string q = (query ?? "").ToLowerInvariant().Trim();
        foreach (var s in All)
        {
            if (genre.Length > 0 && s.Genre != genre) continue; if (onlyFav && !IsFav(s.Name)) continue;
            if (q.Length > 0 && s.Name.ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) < 0 && s.Genre.ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) < 0 && s.Blurb.ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) < 0) continue;
            res.Add(s);
        }
        return res;
    }
}

// the sheet that shows them
partial class StudioForm
{
    bool stylesOpen; string stylesQuery = "", stylesGenre = ""; bool stylesFav; float stylesScroll;

    void OpenStyles() { stylesOpen = true; stylesQuery = ""; stylesGenre = ""; stylesFav = false; stylesScroll = 0; ntOpen = false; busOpen = false; browserOpen = false; }

    void ApplyStyle(VocalStyles.Style s, bool replace)
    {
        if (eng.Tracks.Count == 0) { Toast("Add or select a track first."); return; }
        var tr = eng.Tracks[Math.Max(0, Math.Min(selTrack, eng.Tracks.Count - 1))];
        var chain = VocalStyles.Build(s); if (chain.Count == 0) { Toast("That style could not be built."); return; }
        var snapS = TakeSnapshot(); PushUndo();
        PutPreset(tr, s.Name, chain);   // every use of a style is a preset of its own: nothing that is on the track is taken away
        OfferRevert(s.Name + " on " + tr.Name, () => Restore(snapS));
        selFx = -1; stylesOpen = false; Toast(s.Name + (replace ? " applied to " : " added to ") + tr.Name + ". Every plugin is editable (Ctrl+Z undoes it).");
    }

    void PaintStylesSheet(Graphics g)
    {
        if (!stylesOpen) return; float W = ClientSize.Width, H = ClientSize.Height; var all = Rc(0, 0, W, H);
        using (var b = new SolidBrush(TM.C(160, 3, 1, 10))) g.FillRectangle(b, all); Reg(all, () => stylesOpen = false);
        float sw = Math.Min(760, W - 24), sh = Math.Min(H - 30, 700); var sheet = PopMove("styles", Rc((W - sw) / 2, (H - sh) / 2, sw, sh), 54, 70);
        FillRR(g, TM.C(252, 16, 10, 34), sheet, 22); Glow(g, NeonViolet, sheet, 22, 0.8f, 4); StrokeRR(g, Color.FromArgb(180, NeonViolet), sheet, 22, 1.5f); Reg(sheet, () => { });
        Txt(g, "RAPPER VOCAL STYLES", 22, Ink, Rc(sheet.X + 22, sheet.Y + 14, 360, 34), StringAlignment.Near, true);
        string tn = eng.Tracks.Count > 0 ? eng.Tracks[Math.Max(0, Math.Min(selTrack, eng.Tracks.Count - 1))].Name : "(no track)";
        Txt(g, "Original sound designs for rap vocals, built from this app's plugins. Choose one for: " + tn, 12, DimC, Rc(sheet.X + 22, sheet.Y + 48, sheet.Width - 44, 20), StringAlignment.Near, false);
        var close = Rc(sheet.Right - 52, sheet.Y + 14, 32, 32); FillRR(g, Hot(close) ? TM.C(160, 70, 38, 130) : TM.C(110, 40, 24, 80), close, 16); Txt(g, "X", 15, Ink, close, StringAlignment.Center, true); Reg(close, () => stylesOpen = false);
        var sb = Rc(sheet.X + 22, sheet.Y + 76, sheet.Width - 44, 34); FillRR(g, TM.C(235, 14, 7, 32), sb, 8); StrokeRR(g, Accent, sb, 8, 1.6f); Reg(sb, () => { });
        if (stylesQuery.Length > 0) Btn(g, Rc(sb.Right - 30, sb.Y + 5, 24, 24), "x", 3, () => { stylesQuery = ""; stylesScroll = 0; }, 12);
        Txt(g, stylesQuery.Length == 0 ? (((int)(t * 2) % 2 == 0 ? "|" : " ") + "  Search styles: trap, drill, dark, warm, echo...") : stylesQuery + ((int)(t * 2) % 2 == 0 ? "|" : ""), 14, stylesQuery.Length == 0 ? DimC : Ink, Rc(sb.X + 12, sb.Y, sb.Width - 50, sb.Height), StringAlignment.Near, false);
        // the subgenre chips (they wrap onto more lines when the sheet is narrow)
        float cx = sheet.X + 22, cy = sheet.Y + 120; const float chH = 28;
        Action<string, bool, Action> chip = (label, on, act) => { float cw = 22 + label.Length * 7.4f; if (cx + cw > sheet.Right - 22) { cx = sheet.X + 22; cy += chH + 6; } Chip(g, Rc(cx, cy, cw, chH), label, on, act); cx += cw + 6; };
        chip("All", stylesGenre.Length == 0 && !stylesFav, () => { stylesGenre = ""; stylesFav = false; stylesScroll = 0; });
        chip("★ Favourites", stylesFav, () => { stylesFav = !stylesFav; stylesGenre = ""; stylesScroll = 0; });
        foreach (var gn in VocalStyles.Genres) { string gg = gn; chip(gn, stylesGenre == gn, () => { stylesGenre = stylesGenre == gg ? "" : gg; stylesFav = false; stylesScroll = 0; }); }
        float top = cy + chH + 12, bottom = sheet.Bottom - 40; var list = VocalStyles.Find(stylesQuery, stylesGenre, stylesFav);
        const float rowH = 70; float areaH = bottom - top; var area = Rc(sheet.X, top, sheet.Width, areaH); float total = list.Count * rowH; stylesScroll = Math.Max(0, Math.Min(Math.Max(0, total - areaH), stylesScroll)); float vsw = total > areaH ? 56 : 0;
        regs.Add(new HitReg { R = area, OnWheel = d => stylesScroll = Math.Max(0, Math.Min(Math.Max(0, total - areaH), stylesScroll - d * 60)) });
        g.SetClip(area);
        if (list.Count == 0) Txt(g, stylesFav ? "No favourites yet. Press the star on a style to keep it here." : "No style matches that.", 14, DimC, Rc(sheet.X + 22, top + 10, sheet.Width - 44, 26), StringAlignment.Near, false);
        for (int i = 0; i < list.Count; i++)
        {
            var s = list[i]; var row = Rc(sheet.X + 14, top + i * rowH - stylesScroll, sheet.Width - 28 - vsw, rowH - 6); if (row.Bottom < top || row.Y > bottom) continue;
            bool hot = Hot(RectangleF.Intersect(row, area)) && area.Contains(MousePos); FillRR(g, hot ? TM.C(200, 60, 34, 112) : TM.C(150, 26, 15, 52), row, 12); StrokeRR(g, Color.FromArgb(hot ? 160 : 80, NeonViolet), row, 12, 1);
            bool fv = VocalStyles.IsFav(s.Name); var star = Rc(row.X + 8, row.Y + 8, 30, 30); Txt(g, fv ? "★" : "☆", 20, fv ? Color.FromArgb(255, 210, 80) : DimC, star, StringAlignment.Center, false); string sn = s.Name; Reg(star, () => VocalStyles.ToggleFav(sn));
            float nameW = g.MeasureString(s.Name, GfxCache.Font(16, true)).Width; Txt(g, s.Name, 16, Ink, Rc(row.X + 44, row.Y + 6, nameW + 12, 26), StringAlignment.Near, true);
            var tag = Rc(row.X + 44 + nameW + 10, row.Y + 10, 14 + s.Genre.Length * 6.6f, 18); FillRR(g, Color.FromArgb(70, NeonViolet), tag, 5); Txt(g, s.Genre, 10, Ink, tag, StringAlignment.Center, false);
            using (var wf = new StringFormat { Trimming = StringTrimming.EllipsisWord }) using (var wb = new SolidBrush(DimC)) g.DrawString(s.Blurb, GfxCache.Font(11, false), wb, Rc(row.X + 46, row.Y + 33, row.Width - 46 - 134, 32), wf);
            var st = s; Btn(g, Rc(row.Right - 112, row.Y + 14, 96, 34), "Use", 1, () => ApplyStyle(st, true), 14);
        }
        g.ResetClip(); VScroll(g, Rc(sheet.Right - 56, top, 42, areaH), total, areaH, () => stylesScroll, v => stylesScroll = v);
        Txt(g, "Use puts the style on the selected track and replaces its plugins (Ctrl+Z undoes it).  " + list.Count + " style" + (list.Count == 1 ? "" : "s") + ".   Esc closes.", 11, DimC, Rc(sheet.X + 22, sheet.Bottom - 30, sheet.Width - 44, 20), StringAlignment.Near, false);
    }
}
