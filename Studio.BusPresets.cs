using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

// ======================================================================
//  BUS PRESETS: 1,440 ready-made chains for buses: 12 kinds of bus (vocal, ad-lib, backing vocals, drums, 808 and bass, beat, reverb,
//  echo, FX, crush, glue, mix) x 15 genres x 8 flavours (clean, warm, punchy, wide, dark, bright, vintage, heavy).
//  Each is built from this app's own plugins with the genre's traits (how much space, how bright, how gritty, how wide, how punchy and
//  which delay note) and the flavour's changes, so they are all different and all editable. They are made when the app starts.
//  The AI BUS GENERATOR (BusPrompt) reads what you type, starts from the closest one and changes it with your words.
// ======================================================================
static class BusPresets
{
    public static readonly string[] Kinds = { "Vocal Bus", "Ad-Lib Bus", "Backing Vocal Bus", "Drum Bus", "808 & Bass Bus", "Beat Bus", "Reverb Bus", "Echo Bus", "FX Bus", "Crush Bus", "Glue Bus", "Mix Bus" };
    public static readonly string[] Genres = { "Trap", "Drill", "UK Rap", "R&B", "Melodic Rap", "Pop", "Boom Bap", "Rage", "Afrobeats", "Emo Rap", "Lo-Fi", "Hyperpop", "Dancehall", "House", "Rock" };
    public static readonly string[] Flavours = { "Clean", "Warm", "Punchy", "Wide", "Dark", "Bright", "Vintage", "Heavy" };
    // per genre: space, brightness (-1..1), grit, width, punch (0..1) and the delay note (Delay plugin: 1 = 1/4, 2 = 1/8, 3 = dotted 1/8, 4 = triplet, 5 = 1/16)
    static readonly double[][] Traits = {
        new[] { .5, .5, .3, .6, .7, 3 }, new[] { .35, .3, .4, .5, .8, 4 }, new[] { .3, .4, .3, .5, .7, 3 }, new[] { .8, .4, .1, .8, .4, 3 }, new[] { .75, .5, .2, .8, .5, 3 },
        new[] { .6, .7, .1, .7, .6, 2 }, new[] { .3, -.2, .5, .3, .7, 1 }, new[] { .5, .6, .8, .9, .9, 5 }, new[] { .6, .5, .1, .7, .6, 2 }, new[] { .7, .2, .5, .7, .5, 3 },
        new[] { .5, -.6, .6, .5, .3, 1 }, new[] { .6, .9, .7, .9, .8, 5 }, new[] { .5, .4, .3, .6, .7, 2 }, new[] { .6, .6, .2, .8, .8, 1 }, new[] { .5, .3, .7, .6, .8, 1 } };
    static readonly string[] KindBlurb = { "glue, polish and a shared space for every vocal you route in", "thin, wide and thrown into echoes, for the ad-libs", "soft, wide and tucked behind the lead", "punch, weight and snap for the whole kit",
        "weight, warmth and control for the 808 and bass", "a gentle glue for the whole instrumental", "one shared reverb for everything routed in", "one shared echo in time with the song", "a creative effect bus", "squashed hard and blended under the dry sound",
        "slow, gentle compression that holds the mix together", "a finishing chain for a group of tracks: tone, glue, width and level" };
    public static readonly List<VocalStyles.Style> All = new List<VocalStyles.Style>();
    static string F(double v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }
    static double C(double v, double lo, double hi) { return Math.Max(lo, Math.Min(hi, v)); }

    static BusPresets()
    {
        for (int k = 0; k < Kinds.Length; k++) for (int g = 0; g < Genres.Length; g++) for (int f = 0; f < Flavours.Length; f++)
            All.Add(new VocalStyles.Style { Name = Kinds[k] + ": " + Genres[g] + " (" + Flavours[f] + ")", Genre = Kinds[k], Part = Genres[g], Blurb = Flavours[f] + " " + Genres[g] + " " + Kinds[k].ToLowerInvariant() + ": " + KindBlurb[k] + ".", Chain = Chain(k, g, f) });
    }

    // the chain for one kind, genre and flavour
    public static string Chain(int kind, int genre, int flav)
    {
        var t = Traits[genre]; double space = t[0], bright = t[1], grit = t[2], width = t[3], punch = t[4]; int sync = (int)t[5]; bool tape = false, dark = false;
        switch (Flavours[flav])
        {
            case "Clean": grit *= 0.3; break;
            case "Warm": bright -= 0.4; tape = true; break;
            case "Punchy": punch = Math.Min(1, punch + 0.3); break;
            case "Wide": width = Math.Min(1, width + 0.4); break;
            case "Dark": bright -= 0.8; dark = true; break;
            case "Bright": bright += 0.5; break;
            case "Vintage": tape = true; dark = true; grit = Math.Max(grit, 0.4); break;
            case "Heavy": punch = Math.Min(1, punch + 0.4); grit = Math.Min(1, grit + 0.4); break;
        }
        bright = C(bright, -1, 1.2); string lp = dark ? ";lp_on=1;lp_f=" + F(tape ? 11000 : 8500) : "";
        Func<double, double, double, double, double, string> eq = (hp, low, mud, pres, air) => "eq{hp_on=1;hp_f=" + F(hp) + lp + ";b0_t=1;b0_f=120;b0_g=" + F(C(low, -12, 12)) + ";b2_f=350;b2_g=" + F(C(mud, -12, 12)) + ";b3_f=3500;b3_g=" + F(C(pres, -12, 12)) + ";b5_t=2;b5_f=12000;b5_g=" + F(C(air, -12, 12)) + "}";
        Func<double, double, double, double, double, string> comp = (thr, ratio, att, rel, mk) => "comp{thr=" + F(thr) + ";ratio=" + F(ratio) + ";att=" + F(C(att, 0.1, 100)) + ";rel=" + F(rel) + ";knee=6;makeup=" + F(mk) + "}";
        string sat = "sat{mode=" + (tape ? 0 : grit > 0.6 ? 2 : 1) + ";drive=" + F(2 + grit * 10) + ";mix=" + F(25 + grit * 30) + "}";
        Func<double, string> rev = mix => "reverb{size=" + F(25 + space * 60) + ";damp=" + F(dark ? 70 : 45) + ";pre=" + F(10 + space * 30) + ";width=100;lc=250;hc=" + F(dark ? 6500 : 10000) + ";mix=" + F(C(mix, 0, 100)) + "}";
        Func<int, double, double, string> dly = (mode, fb, mix) => "delay{mode=" + mode + ";sync=" + sync + ";time=375;fb=" + F(fb) + ";lc=250;hc=" + F(dark ? 4000 : 7000) + ";mix=" + F(mix) + "}";
        string wide = "widener{width=" + F(100 + width * 50) + ";bass=120}", lim = "limiter{ceil=-1;gain=" + F(1 + punch * 2) + ";rel=80;tp=1}";
        var s = new List<string>();
        switch (kind)
        {
            case 0: s.Add(eq(90, 0, -2, 1 + bright * 2, 1 + bright * 3)); s.Add(comp(-18, 2 + punch * 2, 12 - punch * 8, 120, 2)); s.Add(sat); s.Add("deess{freq=6500;thr=-30;range=6}"); s.Add(wide); s.Add(rev(6 + space * 12)); s.Add(lim); break;
            case 1: s.Add(eq(220, -3, -2, 2 + bright * 2, 2 + bright * 3)); s.Add(comp(-22, 4, 4, 80, 3)); s.Add("widener{width=" + F(130 + width * 60) + ";bass=150}"); s.Add(dly(2, 30 + space * 20, 16 + space * 12)); s.Add(rev(10 + space * 15)); break;
            case 2: s.Add(eq(160, -2, -2, -1, 2 + bright * 2)); s.Add(comp(-20, 3, 15, 160, 2)); s.Add("chorus{rate=0.8;depth=2;delay=12;spread=100;mix=" + F(15 + width * 15) + "}"); s.Add("widener{width=" + F(140 + width * 50) + ";bass=150}"); s.Add(rev(14 + space * 16)); break;
            case 3: s.Add(eq(30, 1 + punch * 3, -2, bright * 2, 1 + bright * 2)); s.Add(comp(-16, 4, 8 + (1 - punch) * 20, 80, 2)); s.Add(sat); s.Add(rev(3 + space * 5)); s.Add(lim); break;
            case 4: s.Add(eq(25, 2 + punch * 2, -2 - grit, bright, 0)); s.Add("sat{mode=" + (tape ? 0 : 1) + ";drive=" + F(4 + grit * 12) + ";mix=" + F(35 + grit * 35) + "}"); s.Add(comp(-20, 4, 20, 150, 2)); s.Add("widener{width=" + F(70 + width * 20) + ";bass=200}"); s.Add(lim); break;
            case 5: s.Add(eq(25, punch, -1, bright, 1 + bright)); s.Add(comp(-14, 2, 30, 200, 1)); s.Add("sat{mode=0;drive=" + F(2 + grit * 5) + ";mix=30}"); s.Add("widener{width=" + F(100 + width * 25) + ";bass=150}"); s.Add(lim); break;
            case 6: s.Add("eq{hp_on=1;hp_f=" + F(200 + (1 - space) * 150) + ";lp_on=1;lp_f=" + F(dark ? 6000 : 10000 + bright * 3000) + "}"); s.Add(rev(25 + space * 25)); s.Add("widener{width=" + F(120 + width * 60) + ";bass=200}"); break;
            case 7: s.Add("eq{hp_on=1;hp_f=250;lp_on=1;lp_f=" + F(dark ? 4500 : 8000 + bright * 2000) + "}"); s.Add(dly(2, 35 + space * 25, 22 + space * 15)); s.Add(rev(8 + space * 10)); break;
            case 8:
                switch (Flavours[flav])
                {
                    case "Clean": s.Add("eq{hp_on=1;hp_f=400;lp_on=1;lp_f=4000}"); break;
                    case "Warm": s.Add("sat{mode=0;drive=12;mix=60}"); break;
                    case "Punchy": s.Add("dist{mode=0;drive=14;tone=6000;mix=40;level=-6}"); break;
                    case "Wide": s.Add("chorus{rate=0.6;depth=3;delay=14;spread=100;mix=40}"); s.Add("widener{width=180;bass=150}"); break;
                    case "Dark": s.Add("eq{hp_on=1;hp_f=150;lp_on=1;lp_f=3000}"); break;
                    case "Bright": s.Add("exciter{drive=10;harm=60;flo=4000;fhi=16000;mix=40}"); break;
                    case "Vintage": s.Add("@Lo-Fi Vocal"); break;
                    default: s.Add("dist{mode=1;drive=20;tone=5000;mix=55;level=-8}"); break;
                }
                s.Add(dly(2, 40 + space * 20, 20 + space * 10)); s.Add(rev(15 + space * 20)); break;
            case 9: s.Add(comp(-34, 12, 1, 60, 10)); s.Add("sat{mode=2;drive=" + F(6 + grit * 10) + ";mix=60}"); s.Add(eq(40, 1, -2, 2 + bright, 2 + bright)); s.Add("gain{gain=-6}"); break;
            case 10: s.Add(comp(-12, 2, 30, 300, 1)); s.Add("sat{mode=0;drive=" + F(2 + grit * 4) + ";mix=30}"); s.Add(eq(25, punch, -1, bright, bright)); s.Add(lim); break;
            default: s.Add(eq(25, punch, -1, bright, 1 + bright * 2)); s.Add(comp(-10, 1.5 + punch, 30, 200, 1)); s.Add("sat{mode=0;drive=" + F(2 + grit * 4) + ";mix=25}"); s.Add("widener{width=" + F(100 + width * 20) + ";bass=150}"); s.Add("limiter{ceil=-1;gain=" + F(3 + punch * 3) + ";rel=80;tp=1}"); break;
        }
        return string.Join(" > ", s.ToArray());
    }
}

// ======================================================================
//  AI BUS GENERATOR: "drill drum bus, punchy and dark", "wide reverb bus for R&B", "Juice WRLD vocal bus with more glue and a 1/4 echo"
// ======================================================================
static class BusPrompt
{
    static readonly string[][] KindWords = {
        new[] { "ad lib|adlib|ad libs|adlibs", "Ad-Lib Bus" }, new[] { "backing|bgv|bgvs|harmony|harmonies|choir|stack|stacks|doubles", "Backing Vocal Bus" }, new[] { "808|808s|bass|sub|low end", "808 & Bass Bus" },
        new[] { "drum|drums|kit|percussion|perc", "Drum Bus" }, new[] { "beat|instrumental|music", "Beat Bus" }, new[] { "mix|mixbus|master|2|stereo|mastering", "Mix Bus" }, new[] { "fx|effect|effects|telephone|radio|lofi|lo fi|creative", "FX Bus" },
        new[] { "vocal|vocals|vox|voice|lead", "Vocal Bus" }, new[] { "crush|parallel|new york|squash", "Crush Bus" }, new[] { "glue", "Glue Bus" }, new[] { "echo|delay|delays|echoes", "Echo Bus" }, new[] { "reverb|verb|space|room|hall", "Reverb Bus" } };
    static readonly string[][] GenreWords = {
        new[] { "uk drill|ny drill|drill", "Drill" }, new[] { "uk rap|uk|road rap|grime|british", "UK Rap" }, new[] { "r&b|rnb|r and b|soul|trapsoul", "R&B" }, new[] { "melodic|singing|singer", "Melodic Rap" },
        new[] { "hyperpop|hyper pop", "Hyperpop" }, new[] { "pop", "Pop" }, new[] { "boom bap|boombap|90s|old school|oldschool", "Boom Bap" }, new[] { "rage", "Rage" }, new[] { "afro|afrobeat|afrobeats|amapiano", "Afrobeats" },
        new[] { "emo", "Emo Rap" }, new[] { "lofi|lo fi|chill", "Lo-Fi" }, new[] { "dancehall|reggae", "Dancehall" }, new[] { "house|edm|dance|techno", "House" }, new[] { "rock|punk|metal", "Rock" }, new[] { "trap", "Trap" } };
    static readonly string[][] FlavourWords = {
        new[] { "vintage|tape|old|analog|analogue", "Vintage" }, new[] { "heavy|aggressive|hard|crushing", "Heavy" }, new[] { "punchy|punch|snappy|tight", "Punchy" }, new[] { "wide|wider|stereo|huge", "Wide" },
        new[] { "dark|darker|moody|muffled", "Dark" }, new[] { "bright|brighter|airy|crisp|shiny", "Bright" }, new[] { "warm|warmer|smooth|soft", "Warm" }, new[] { "clean|transparent|natural", "Clean" } };
    // the genre of an artist's own vocal bus, from the vocal presets ("Vocal Bus (Melodic)" -> Melodic Rap)
    static readonly string[][] BusGenre = { new[] { "trap", "Trap" }, new[] { "melodic", "Melodic Rap" }, new[] { "r&b", "R&B" }, new[] { "uk", "UK Rap" }, new[] { "drill", "Drill" }, new[] { "boom", "Boom Bap" }, new[] { "rage", "Rage" }, new[] { "singer", "Pop" }, new[] { "afro", "Afrobeats" }, new[] { "emo", "Emo Rap" } };

    static string Norm(string s) { var c = (s ?? "").ToLowerInvariant().ToCharArray(); for (int i = 0; i < c.Length; i++) if (!(char.IsLetterOrDigit(c[i]) || c[i] == '&' || c[i] == '/')) c[i] = ' '; return " " + string.Join(" ", new string(c).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) + " "; }
    static int Find(string p, string words) { foreach (var w in words.Split('|')) { int i = p.IndexOf(" " + w + " ", StringComparison.Ordinal); if (i >= 0) return i + 1; } return -1; }
    static bool Has(string p, string words) { return Find(p, words) >= 0; }
    static bool Neg(string p, int at) { if (at < 0) return false; string b = p.Substring(Math.Max(0, at - 12), Math.Min(12, at)); return b.Contains(" no ") || b.Contains(" without ") || b.Contains(" not ") || b.Contains(" less "); }
    static string F(double v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }

    public static PresetPrompt.Result Make(string prompt, int seed = 0)
    {
        var r = new PresetPrompt.Result(); string p = Norm(prompt); var rnd = seed == 0 ? null : new Random(seed);
        string kind = null; int kAt = int.MaxValue;
        foreach (var k in KindWords) foreach (var w in k[0].Split('|')) { int at = p.IndexOf(" " + w + " bus ", StringComparison.Ordinal); if (at >= 0 && at < kAt) { kAt = at; kind = k[1]; } }   // "drum bus", "vocal bus": the word right before bus
        if (kind == null) foreach (var k in KindWords) { var ws = k[0].Split('|').Where(w => w != "mix" && w != "2" && w != "stereo" && w != "music").ToArray(); if (ws.Length > 0 && Has(p, string.Join("|", ws))) { kind = k[1]; break; } }   // short words only count right before "bus"
        if (kind == null) { kind = "Vocal Bus"; r.Understood.Add("No kind of bus named: a vocal bus"); } else r.Understood.Add("Kind: " + kind);
        string genre = null; foreach (var g in GenreWords) if (Has(p, g[0])) { genre = g[1]; break; }
        if (genre == null)
        {
            string artist = PresetPrompt.ArtistIn(prompt);
            if (artist != null) { var st = VocalStyles.All.FirstOrDefault(s => VocalStyles.ArtistOf(s) == artist && !string.IsNullOrEmpty(s.BusName)); string bn = st == null ? "" : st.BusName.ToLowerInvariant(); foreach (var bg in BusGenre) if (bn.Contains(bg[0])) { genre = bg[1]; break; } if (genre != null) r.Understood.Add("Artist: " + artist + " (a " + genre + " sound)"); }
        }
        if (genre == null) { genre = rnd != null ? BusPresets.Genres[rnd.Next(BusPresets.Genres.Length)] : "Trap"; r.Understood.Add("No genre named: " + genre); } else if (!r.Understood.Any(u => u.StartsWith("Artist"))) r.Understood.Add("Genre: " + genre);
        string flav = null; int fAt = int.MaxValue; foreach (var f in FlavourWords) { int at = Find(p, f[0]); if (at >= 0 && at < fAt) { fAt = at; flav = f[1]; } }   // the first flavour named
        if (flav == null) flav = rnd != null ? BusPresets.Flavours[rnd.Next(BusPresets.Flavours.Length)] : "Clean"; r.Understood.Add("Flavour: " + flav);
        var bs = BusPresets.All.First(s => s.Name == kind + ": " + genre + " (" + flav + ")"); r.Base = bs.Name;
        var st2 = bs.Chain.Split(new[] { " > " }, StringSplitOptions.RemoveEmptyEntries).ToList(); var tail = new List<string>(); int i;
        Predicate<string> isRev = s => s.StartsWith("reverb{"); Predicate<string> isDly = s => s.StartsWith("delay{");
        if (Has(p, "dry|no reverb|no space|no effects")) { st2.RemoveAll(isRev); st2.RemoveAll(isDly); r.Understood.Add("Dry (no reverb or echo)"); }
        else
        {
            if ((i = Find(p, "more reverb|lots of reverb|spacey|spacy|wet|big reverb|huge reverb|hall|cathedral")) >= 0 && !Neg(p, i)) { st2.RemoveAll(isRev); tail.Add("reverb{size=88;damp=45;pre=35;width=100;lc=250;hc=10000;mix=30}"); r.Understood.Add("Lots of reverb"); }
            if ((i = Find(p, "echo|delay|delays|echoes")) >= 0 && !Neg(p, i) && kind != "Echo Bus")
            {
                int sync = 3; string sn = "dotted 1/8"; if (Has(p, "1/4|quarter")) { sync = 1; sn = "1/4"; } else if (Has(p, "triplet")) { sync = 4; sn = "1/8 triplet"; } else if (Has(p, "1/16|sixteenth")) { sync = 5; sn = "1/16"; } else if (Has(p, "1/8|eighth")) { sync = 2; sn = "1/8"; }
                st2.RemoveAll(isDly); tail.Insert(0, "delay{mode=" + (Has(p, "mono") ? 0 : 2) + ";sync=" + sync + ";time=375;fb=35;lc=250;hc=7000;mix=22}"); r.Understood.Add(sn + " echo");
            }
        }
        if (Has(p, "more glue|glue|more compression|compressed|squash|squashed|squashy")) { st2.Insert(Math.Min(1, st2.Count), "comp{thr=-24;ratio=4;att=10;rel=120;knee=6;makeup=4}"); r.Understood.Add("More compression and glue"); }
        if ((i = Find(p, "saturation|saturated|grit|gritty|warmth|tape")) >= 0 && !Neg(p, i)) { st2.Add("sat{mode=0;drive=10;mix=45}"); r.Understood.Add("Saturation"); }
        if ((i = Find(p, "distortion|distorted|dirty|crunchy|fuzz")) >= 0 && !Neg(p, i)) { st2.Add("dist{mode=0;drive=14;tone=6000;mix=40;level=-6}"); r.Understood.Add("Distortion"); }
        if (Has(p, "telephone|phone")) { st2.Add("@Telephone Vocal"); r.Understood.Add("Telephone sound"); }
        if (Has(p, "radio")) { st2.Add("@Radio Voice"); r.Understood.Add("Old radio sound"); }
        if (Has(p, "mono|narrow")) { st2.RemoveAll(s => s.StartsWith("widener{")); st2.Add("widener{width=60;bass=150}"); r.Understood.Add("Narrower (more mono)"); }
        else if (Has(p, "wider|super wide|very wide|huge")) { st2.RemoveAll(s => s.StartsWith("widener{")); st2.Add("widener{width=190;bass=150}"); r.Understood.Add("Very wide"); }
        if (Has(p, "de ess|deess|sibilance|harsh")) { st2.Add("deess{freq=6800;thr=-32;range=10}"); r.Understood.Add("De-essing"); }
        if (Has(p, "loud|louder|limit|limiter|slam|slammed")) { st2.RemoveAll(s => s.StartsWith("limiter{")); st2.Add("limiter{ceil=-1;gain=6;rel=80;tp=1}"); r.Understood.Add("Louder (limiter pushed)"); }
        if (Has(p, "chorus|doubled")) { st2.Add("chorus{rate=0.9;depth=2;delay=12;spread=100;mix=30}"); r.Understood.Add("Chorus"); }
        // anything that limits the level stays last
        var lims = st2.Where(s => s.StartsWith("limiter{")).ToList(); st2.RemoveAll(s => s.StartsWith("limiter{")); st2.AddRange(tail); st2.AddRange(lims);
        r.Chain = AiVary.Jitter(string.Join(" > ", st2.ToArray()), rnd); r.BusName = kind; r.BusChain = null;
        string title = (prompt ?? "").Trim(); if (title.Length > 42) title = title.Substring(0, 42).TrimEnd() + "..."; r.Name = "AI Bus: " + (title.Length > 0 ? char.ToUpper(title[0]) + title.Substring(1) : bs.Name);
        return r;
    }
}

partial class StudioForm
{
    // a bus preset goes on: the bus you picked, or the bus the picked track goes into, or a new bus of that kind (the picked track is routed into it)
    internal void ApplyBusPreset(VocalStyles.Style s)
    {
        if (eng.Tracks.Count == 0) { Toast("Add a track first: the bus preset goes on its bus."); return; }
        var sel = busEdit != null && eng.Tracks.Contains(busEdit) ? busEdit : eng.Tracks[Math.Max(0, Math.Min(selTrack, eng.Tracks.Count - 1))];
        var chain = VocalStyles.BuildChain(s.Chain); if (chain.Count == 0) { Toast("That bus preset could not be built."); return; }
        var snapS = TakeSnapshot(); PushUndo(); Track bus; string note;
        if (sel.IsBus) { bus = sel; note = " on " + bus.Name + "."; }
        else
        {
            var row = sel.Host ?? sel;
            if (row.Out != null && row.Out.IsBus && eng.Tracks.Contains(row.Out)) { bus = row.Out; note = " on " + bus.Name + " (the bus " + row.Name + " goes into)."; }
            else
            {
                string nm = string.IsNullOrEmpty(s.Genre) || s.Genre == "AI" ? "Bus" : s.Genre; bus = eng.Tracks.Find(x => x.IsBus && x.Name == nm);
                if (bus == null) { int keep = selTrack; bus = StudioEngine.NewTrack(nm, "bus", null); bus.IsBus = true; bus.Color = unchecked((int)0xFFFFD04D); nextInsert = -1; AddTrack(bus); selTrack = Math.Max(0, Math.Min(eng.Tracks.Count - 1, keep)); note = ": " + nm + " was made and " + row.Name + " goes into it (route more tracks into it from their Bus drop-down)."; }
                else note = " on " + nm + ", and " + row.Name + " now goes into it.";
                row.Out = bus;
            }
        }
        PutPreset(bus, s.Name, chain);
        OfferRevert(s.Name + " on " + bus.Name, () => Restore(snapS));
        selFx = -1; stylesOpen = false; Toast(s.Name + " added" + note + " Every plugin is editable (Ctrl+Z undoes it).");
    }
}
