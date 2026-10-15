using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

// ======================================================================
//  AI PRESET GENERATOR: describe the sound in your own words ("Juice WRLD with a stutter delay and lots of reverb", "dark drill, dry, bit of distortion")
//  and get a plugin chain. It reads the words with a built-in engine (no internet): the artist or genre you named is the starting sound
//  (from VOCAL PRESETS), then every effect word adds, changes or takes away plugins, and words like "a bit" or "lots" set how strong.
// ======================================================================
static class PresetPrompt
{
    public class Result { public string Name = "", Base = "", Chain = "", BusName, BusChain; public List<string> Understood = new List<string>(); }

    // other ways people write an artist (the names in the library are found by themselves too)
    static readonly string[][] Alias = {
        new[] { "juice world", "Juice WRLD" }, new[] { "juice", "Juice WRLD" }, new[] { "carti", "Playboi Carti" }, new[] { "uzi", "Lil Uzi Vert" }, new[] { "la flame", "Travis Scott" }, new[] { "travis", "Travis Scott" },
        new[] { "thugger", "Young Thug" }, new[] { "durk", "Lil Durk" }, new[] { "polo", "Polo G" }, new[] { "youngboy", "NBA YoungBoy" }, new[] { "nba", "NBA YoungBoy" }, new[] { "tecca", "Lil Tecca" },
        new[] { "a boogie", "A Boogie wit da Hoodie" }, new[] { "tjay", "Lil Tjay" }, new[] { "roddy", "Roddy Ricch" }, new[] { "toliver", "Don Toliver" }, new[] { "trippie", "Trippie Redd" }, new[] { "peep", "Lil Peep" },
        new[] { "xxxtentacion", "XXXTentacion" }, new[] { "xxx", "XXXTentacion" }, new[] { "drizzy", "Drake" }, new[] { "ovo", "Drake" }, new[] { "harlow", "Jack Harlow" }, new[] { "slim shady", "Eminem" }, new[] { "shady", "Eminem" }, new[] { "marshall mathers", "Eminem" },
        new[] { "kendrick", "Kendrick Lamar" }, new[] { "k dot", "Kendrick Lamar" }, new[] { "j cole", "J. Cole" }, new[] { "cole", "J. Cole" }, new[] { "biggie", "Biggie" }, new[] { "notorious", "Biggie" }, new[] { "2pac", "Tupac" }, new[] { "pac", "Tupac" },
        new[] { "jay z", "Jay-Z" }, new[] { "jayz", "Jay-Z" }, new[] { "hov", "Jay-Z" }, new[] { "snoop", "Snoop Dogg" }, new[] { "50 cent", "50 Cent" }, new[] { "fifty", "50 Cent" }, new[] { "dr dre", "Dr. Dre era" }, new[] { "dre", "Dr. Dre era" },
        new[] { "joey bada", "Joey Bada$$" }, new[] { "joey", "Joey Bada$$" }, new[] { "denzel", "Denzel Curry" }, new[] { "mac miller", "Mac Miller" }, new[] { "tyler", "Tyler, The Creator" }, new[] { "asap", "A$AP Rocky" }, new[] { "rocky", "A$AP Rocky" },
        new[] { "cudi", "Kid Cudi" }, new[] { "kanye", "Kanye West" }, new[] { "yeezy", "Kanye West" }, new[] { "weezy", "Lil Wayne" }, new[] { "wayne", "Lil Wayne" }, new[] { "21 savage", "21 Savage" }, new[] { "savage", "21 Savage" },
        new[] { "quavo", "Migos" }, new[] { "offset", "Migos" }, new[] { "takeoff", "Migos" }, new[] { "dababy", "DaBaby" }, new[] { "moneybagg", "Moneybagg Yo" }, new[] { "kodak", "Kodak Black" }, new[] { "ski mask", "Ski Mask" },
        new[] { "nicki", "Nicki Minaj" }, new[] { "cardi", "Cardi B" }, new[] { "megan", "Megan Thee Stallion" }, new[] { "doja", "Doja Cat" }, new[] { "summer walker", "Summer Walker" }, new[] { "weeknd", "The Weeknd" }, new[] { "abel", "The Weeknd" },
        new[] { "bryson", "Bryson Tiller" }, new[] { "partynextdoor", "PARTYNEXTDOOR" }, new[] { "6lack", "6LACK" }, new[] { "brent", "Brent Faiyaz" }, new[] { "chris brown", "Chris Brown" }, new[] { "breezy", "Chris Brown" }, new[] { "frank ocean", "Frank Ocean" },
        new[] { "tory", "Tory Lanez" }, new[] { "post malone", "Post Malone" }, new[] { "posty", "Post Malone" }, new[] { "lil nas", "Lil Nas X" }, new[] { "cench", "Central Cee" }, new[] { "central", "Central Cee" }, new[] { "santan", "Dave" },
        new[] { "headie", "Headie One" }, new[] { "digga", "Digga D" }, new[] { "tion", "Tion Wayne" }, new[] { "huncho", "M Huncho" }, new[] { "hus", "J Hus" }, new[] { "simz", "Little Simz" }, new[] { "fivio", "Fivio Foreign" }, new[] { "flock", "Kay Flock" }, new[] { "burna", "Burna Boy" },
    };
    // a genre or kind of vocal when no artist is named (the first that matches wins)
    static readonly string[][] Genre = {
        new[] { "ultra r&b|ultra rnb", "ULTRA R&B (Glossy)" }, new[] { "dark r&b|moody r&b|dark rnb", "ULTRA R&B (Dark & Moody)" }, new[] { "r&b|rnb|r and b|trapsoul", "ULTIMATE R&B Vocal" }, new[] { "neo soul|soul", "Neo Soul" },
        new[] { "uk drill", "UK Drill Smooth (Central Cee style)" }, new[] { "ny drill|new york drill|brooklyn drill", "NY Drill Lead" }, new[] { "chicago drill", "Chicago Drill" }, new[] { "drill", "ULTIMATE Drill Lead" },
        new[] { "grime", "Grime Pioneer (Skepta style)" }, new[] { "uk rap|uk|road rap|british", "UK Rap Polished" }, new[] { "boom bap|boombap|90s|old school|oldschool|classic hip hop", "ULTIMATE Boom Bap" },
        new[] { "rage|hyper", "ULTIMATE Rage" }, new[] { "emo", "Emo Trap" }, new[] { "metal|scream", "Metal Scream" }, new[] { "rock|punk", "Pop Punk Rap" }, new[] { "afrobeat|afro", "Afro Giant (Burna Boy style)" }, new[] { "dancehall", "Dancehall Toast" },
        new[] { "amapiano", "Amapiano Float" }, new[] { "gospel|church", "Gospel Power" }, new[] { "ballad", "Ballad Singer" }, new[] { "singing|singer|sing|vocalist", "ULTIMATE Melodic Sing" }, new[] { "pop", "Pop Star Polish" },
        new[] { "melodic|hook", "ULTIMATE Trap Hook" }, new[] { "lofi|lo fi|cloud", "Cloud Rap Haze" }, new[] { "phonk", "Florida Phonk (Ski Mask style)" }, new[] { "podcast|talking|speech|interview|skit|voice over|voiceover", "ULTIMATE Podcast Clean" },
        new[] { "storytelling|lyrical|conscious", "Conscious Lead (Kendrick Lamar style)" }, new[] { "loud", "ULTIMATE Loud & Clear" }, new[] { "trap", "Trap Lead" },
    };

    static string Norm(string s) { var c = (s ?? "").ToLowerInvariant().Replace("’", "'").ToCharArray(); for (int i = 0; i < c.Length; i++) if (!(char.IsLetterOrDigit(c[i]) || c[i] == '&' || c[i] == '/' || c[i] == '$' || c[i] == '\'')) c[i] = ' '; return " " + string.Join(" ", new string(c).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) + " "; }
    static string NormName(string s) { return Norm(s.Replace(".", "").Replace("-", " ").Replace(",", " ")).Trim(); }
    static int Find(string p, string words) { foreach (var w in words.Split('|')) { int i = p.IndexOf(" " + w + " ", StringComparison.Ordinal); if (i >= 0) return i + 1; } return -1; }
    static bool Has(string p, string words) { return Find(p, words) >= 0; }
    // how strong: "lots of", "heavy", "super" make it stronger, "a bit", "subtle" softer (the words just before it)
    static float Amt(string p, int at)
    {
        if (at < 0) return 1f; string before = p.Substring(Math.Max(0, at - 22), Math.Min(22, at));
        foreach (var w in new[] { "lots of", "loads of", "a lot of", "tons of", "heavy", "very", "super", "crazy", "massive", "huge", "extreme", "mad", "big", "more", "strong", "hard" }) if (before.Contains(" " + w + " ") || before.EndsWith(" " + w + " ")) return 1.6f;
        foreach (var w in new[] { "a bit", "a little", "bit of", "slight", "subtle", "touch of", "light", "little", "less", "small" }) if (before.Contains(" " + w + " ") || before.EndsWith(" " + w + " ")) return 0.55f;
        return 1f;
    }
    static bool Negated(string p, int at) { if (at < 0) return false; string before = p.Substring(Math.Max(0, at - 12), Math.Min(12, at)); return before.Contains(" no ") || before.Contains(" without ") || before.Contains(" not ") || before.Contains(" remove ") || before.Contains(" zero "); }

    static string F(double v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }
    static string Rv(double size, double damp, double pre, double mix) { return "reverb{size=" + F(size) + ";damp=" + F(damp) + ";pre=" + F(pre) + ";lc=250;hc=11000;mix=" + F(Math.Min(60, mix)) + "}"; }
    static string Dl(int sync, double time, double fb, double mix, int mode) { return "delay{mode=" + mode + ";sync=" + sync + ";time=" + F(time) + ";fb=" + F(Math.Min(85, fb)) + ";lc=250;hc=7000;mix=" + F(Math.Min(50, mix)) + "}"; }
    static bool IsTune(string st) { return st.StartsWith("pitch{") || st.StartsWith("@Hard Tune") || st.StartsWith("@Auto-Tune-Style") || st.StartsWith("@Melody Tune") || st.StartsWith("@Robot Pitch") || st.StartsWith("@Automatic Vocal Tuner"); }
    static bool IsSpace(string st) { return st.StartsWith("reverb{") || st.StartsWith("delay{") || st.StartsWith("@Worldwide Echo") || st.StartsWith("@Stadium Echo") || st.StartsWith("@Rap Ad-Lib Echo") || st.StartsWith("@Dark Echo") || st.StartsWith("@Airy Echo") || st.StartsWith("@Telephone Throw") || st.StartsWith("@Infinite Space"); }

    // the artist named in a prompt (or null): the AI BUS GENERATOR uses it to pick the genre
    public static string ArtistIn(string prompt)
    {
        string p = Norm(prompt), artist = null; int best = -1; var artists = VocalStyles.All.Select(VocalStyles.ArtistOf).Where(a => a.Length > 0).Distinct().ToList();
        foreach (var a in artists) { string n = NormName(a); if (n.Length >= 3 && p.Contains(" " + n + " ") && n.Length > best) { artist = a; best = n.Length; } }
        if (artist == null) foreach (var al in Alias) if (p.Contains(" " + al[0] + " ") && artists.Contains(al[1])) { artist = al[1]; break; }
        return artist;
    }

    public static Result Make(string prompt, int seed = 0)
    {
        var r = new Result(); string p = Norm(prompt); var rnd = seed == 0 ? null : new Random(seed);
        // 1. the starting sound: an artist, otherwise a genre, otherwise the ULTIMATE rap lead
        string artist = null; int best = -1;
        var artists = VocalStyles.All.Select(VocalStyles.ArtistOf).Where(a => a.Length > 0).Distinct().ToList();
        foreach (var a in artists) { string n = NormName(a); if (n.Length >= 3 && p.Contains(" " + n + " ") && n.Length > best) { artist = a; best = n.Length; } }
        if (artist == null) foreach (var al in Alias) if (p.Contains(" " + al[0] + " ") && artists.Contains(al[1])) { artist = al[1]; break; }
        VocalStyles.Style bs = null;
        if (artist != null)
        {
            var mine = VocalStyles.All.Where(s => s.Part == "Lead" && VocalStyles.ArtistOf(s) == artist).ToList();
            Func<string, VocalStyles.Style> pick = w => mine.FirstOrDefault(s => s.Name.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0);
            if (Has(p, "sing|singing|melodic|melody")) bs = pick("Sing") ?? pick("Melod");
            if (bs == null && Has(p, "scream|rock")) bs = pick("Scream") ?? pick("Rock");
            if (bs == null && Has(p, "dark")) bs = pick("Dark");
            if (bs == null && Has(p, "hook")) bs = pick("Hook");
            if (bs == null && Has(p, "ad lib|adlib|ad libs|adlibs")) bs = pick("Ad-Lib");
            if (bs == null && Has(p, "verse|bars|rap|rapping")) bs = mine.FirstOrDefault(s => s.Name.IndexOf("Sing", StringComparison.OrdinalIgnoreCase) < 0);
            if (bs == null) bs = mine.FirstOrDefault();
            if (bs != null) r.Understood.Add("Artist: " + artist + " (starting from " + bs.Name + ")");
        }
        if (bs == null) foreach (var gp in Genre) { if (Has(p, gp[0])) { bs = VocalStyles.All.FirstOrDefault(s => s.Name == gp[1]); if (bs != null) { r.Understood.Add("Sound: " + gp[1]); break; } } }
        if (bs == null) { bs = VocalStyles.All.FirstOrDefault(s => s.Name == "ULTIMATE Rap Lead") ?? VocalStyles.All[0]; r.Understood.Add("No artist or genre named: starting from " + bs.Name); }
        if (rnd != null) { var pool = VocalStyles.All.Where(s => s.Genre == bs.Genre && s.Part == bs.Part && VocalStyles.ArtistOf(s) == VocalStyles.ArtistOf(bs)).ToList(); if (pool.Count > 1) { bs = pool[rnd.Next(pool.Count)]; r.Understood.Add("Another take: starting from " + bs.Name); } }
        // the job: ad-libs / doubles versions of that sound
        if (Has(p, "ad lib|adlib|ad libs|adlibs") && bs.Part == "Lead" && bs.Name.IndexOf("Ad-Lib", StringComparison.OrdinalIgnoreCase) < 0) { var v = VocalStyles.All.FirstOrDefault(s => s.Name == bs.Name + " - Ad-Libs"); if (v != null) { bs = v; r.Understood.Add("For ad-libs (thin, wide, thrown into an echo)"); } }
        else if (Has(p, "doubles|double track|stack track") && bs.Part == "Lead") { var v = VocalStyles.All.FirstOrDefault(s => s.Name == bs.Name + " - Doubles"); if (v != null) { bs = v; r.Understood.Add("For doubles (wide, tucked under the lead)"); } }
        r.Base = bs.Name; r.BusName = bs.BusName; r.BusChain = bs.BusChain;
        var st = bs.Chain.Split(new[] { " > " }, StringSplitOptions.RemoveEmptyEntries).ToList(); var front = new List<string>(); var tail = new List<string>();
        Action<string> said = s => r.Understood.Add(s);
        int i;
        // 2. tuning
        if ((i = Find(p, "autotune|auto tune|tune|tuning|tuned")) >= 0 && Negated(p, i) || Has(p, "natural|untuned|raw vocal")) { st.RemoveAll(IsTune); said("No tuning (natural voice)"); }
        else if (Has(p, "hard tune|t pain|tpain|robotic tune|heavy autotune|heavy auto tune|snapped")) { st.RemoveAll(IsTune); front.Add("@Hard Tune{scale=0;speed=0;strength=100;natural=0}"); said("Hard tune (snapped to the notes)"); }
        else if (Has(p, "autotune|auto tune|tuned|tune") && !st.Any(IsTune)) { front.Add("@Auto-Tune-Style Pitch Corrector{scale=0;speed=10;natural=25}"); said("Autotune"); }
        if (Has(p, "choir|stacked|stack")) { st.RemoveAll(IsTune); front.Add("@Melody Tune: Choir Stack{scale=0}"); said("Choir stack (third, fifth and octave)"); }
        else if (Has(p, "octave")) { st.RemoveAll(IsTune); front.Add("@Melody Tune: Octave Dream{scale=0}"); said("An octave above the voice"); }
        else if (Has(p, "harmony|harmonies|harmonised|harmonized")) { st.RemoveAll(IsTune); front.Add("@Melody Tune: Soft Harmony{scale=0}"); said("A harmony under the voice"); }
        // 3. the voice itself
        if (Has(p, "demon|monster")) { tail.Add("@Demon Voice"); said("Demon voice layer"); }
        else if ((i = Find(p, "deep voice|deeper|low voice|deep")) >= 0 && !Has(p, "deep reverb")) { front.Add("@Formant Shifter{shift=" + F(-2.5 * Amt(p, i)) + ";tilt=-1;mix=100}"); said("Deeper voice (formant down)"); }
        if ((i = Find(p, "baby voice|chipmunk|high voice|squeaky|pitched up|higher")) >= 0) { front.Add("@Formant Shifter{shift=" + F(4 * Amt(p, i)) + ";tilt=2;mix=100}"); said("Higher, baby-like voice (formant up)"); }
        if (Has(p, "whisper|whispered")) { front.Add("@Whisper Enhancer"); said("Whisper enhancer"); }
        if (Has(p, "noisy|noise|hiss|room noise")) { front.Insert(0, "@Vocal De-Noise{amount=14}"); said("Noise reduction first"); }
        if (Has(p, "breaths|no breath|remove breath")) { front.Insert(0, "@Breath Remover"); said("Breaths taken out"); }
        // 4. tone and colour
        if ((i = Find(p, "dark|darker|moody|muffled")) >= 0) { float a = Amt(p, i); st.Add("eq{lp_on=1;lp_f=" + F(a > 1 ? 7500 : 9500) + ";b0_t=1;b0_f=160;b0_g=1.5;b3_f=3200;b3_g=" + F(-1.5 * a) + "}"); st.Add("sat{mode=1;drive=" + F(6 * a) + ";mix=35}"); said((a > 1 ? "Very dark" : a < 1 ? "A bit darker" : "Darker") + " (top rolled off, tube warmth)"); }
        else if ((i = Find(p, "warm|warmer|vintage")) >= 0) { st.Add("eq{lp_on=1;lp_f=12500;b0_t=1;b0_f=170;b0_g=" + F(2 * Amt(p, i)) + "}"); st.Add("sat{mode=0;drive=" + F(6 * Amt(p, i)) + ";mix=40}"); said("Warmer (tape warmth, softer top)"); }
        if ((i = Find(p, "bright|brighter|crisp|airy|shiny|clear|clearer|cutting")) >= 0) { float a = Amt(p, i); st.Add("eq{b3_f=4000;b3_g=" + F(1.5 * a) + ";b5_t=2;b5_f=12500;b5_g=" + F(3 * a) + "}"); st.Add("exciter{drive=8;harm=50;flo=4500;fhi=16000;mix=" + F(22 * a) + "}"); said((a > 1 ? "Very bright" : "Brighter") + " (presence and air)"); }
        if ((i = Find(p, "breathy")) >= 0) { st.Add("eq{b5_t=2;b5_f=13500;b5_g=" + F(4 * Amt(p, i)) + "}"); said("Breathy (extra air)"); }
        if ((i = Find(p, "distorted|distortion|distort|gritty|grit|raspy|dirty|crunchy|crunch|fuzz")) >= 0 && !Negated(p, i)) { float a = Amt(p, i); st.Add("dist{mode=0;drive=" + F(12 * a) + ";tone=6000;mix=" + F(Math.Min(70, 30 * a)) + ";level=-6}"); said((a > 1 ? "Heavy" : a < 1 ? "A bit of" : "Some") + " distortion"); }
        else if ((i = Find(p, "saturated|saturation|tape|tube")) >= 0) { st.Add("sat{mode=" + (Has(p, "tube") ? 1 : 0) + ";drive=" + F(8 * Amt(p, i)) + ";mix=50}"); said("Saturation"); }
        if (Has(p, "telephone|phone call|phone")) { st.Add("@Telephone Vocal"); said("Telephone sound"); }
        if (Has(p, "radio")) { st.Add("@Radio Voice"); said("Old radio sound"); }
        if (Has(p, "megaphone|bullhorn")) { st.Add("@Megaphone"); said("Megaphone"); }
        if (Has(p, "lofi|lo fi")) { st.Add("@Lo-Fi Vocal"); said("Lo-fi"); }
        if (Has(p, "bitcrush|bitcrushed|bit crushed|8 bit|8bit")) { st.Add("@Bitcrusher"); said("Bit crushed"); }
        if (Has(p, "vinyl|old record")) { st.Add("eq{hp_on=1;hp_f=250;lp_on=1;lp_f=6500}"); st.Add("sat{mode=2;drive=8;mix=40}"); said("Old vinyl record sound"); }
        if (Has(p, "robot|vocoder")) { st.Add("@Robot Voice{mix=60}"); said("Robot vocoder"); }
        if (Has(p, "talkbox|talk box")) { st.Add("@Talkbox-Style Effect"); said("Talkbox"); }
        // 5. dynamics
        if (Has(p, "loud|aggressive|punchy|hard hitting|in your face|upfront|up front")) { st.Add("comp{thr=-26;ratio=6;att=2;rel=55;knee=4;makeup=4}"); st.Add("limiter{ceil=-1;gain=3;rel=80;tp=1}"); said("Louder and punchier (hard compression, limiter)"); }
        else if (Has(p, "soft|gentle|smooth|chill|calm|laid back|laidback")) { st.Add("comp{thr=-20;ratio=2.5;att=18;rel=160;knee=10;makeup=2}"); said("Softer, smoother compression"); }
        if (Has(p, "de ess|deess|sibilance|harsh s|esses")) { st.Add("deess{freq=7000;thr=-32;range=12}"); said("Extra de-essing"); }
        // 6. width and modulation
        if ((i = Find(p, "doubled|double tracked|doubling|double")) >= 0 && !Has(p, "doubles")) { st.Add("chorus{rate=1.1;depth=1.6;delay=12;spread=100;mix=" + F(35 * Amt(p, i)) + "}"); st.Add("widener{width=160;bass=120}"); said("Doubled (thick and wide)"); }
        if ((i = Find(p, "chorus")) >= 0) { st.Add("chorus{rate=0.6;depth=3;delay=14;spread=85;mix=" + F(30 * Amt(p, i)) + "}"); said("Chorus"); }
        if (Has(p, "flanger|flange")) { st.Add("@Barberpole Flanger"); said("Flanger"); }
        if (Has(p, "phaser|phase")) { st.Add("@Phaser"); said("Phaser"); }
        if ((i = Find(p, "wide|wider|stereo|huge")) >= 0) { st.Add("widener{width=" + F(Math.Min(200, 145 * Amt(p, i))) + ";bass=120}"); said("Wider"); }
        else if (Has(p, "mono|narrow|centre|center")) { st.Add("widener{width=60;bass=120}"); said("Narrower (more mono)"); }
        if (Has(p, "8d|8 d|spinning|spin|rotating|around your head")) { st.Add("8d{period=10;mix=100}"); said("8D (moves around your head)"); }
        if (Has(p, "reverse|reversed|backwards")) { st.Add("@Reverse Grain Effect"); said("Reverse grains"); }
        // 7. rhythm effects
        bool stutterEcho = Has(p, "stutter echo|stutter delay|stuttering echo|stuttering delay|stutter echoes");
        if (stutterEcho) { tail.Add(Dl(5, 120, 22, 24, 2)); tail.Add("@Stutter Edit"); said("Stutter echo (1/16 repeats plus stutter edits)"); }
        else if (Has(p, "stutter|stutters|stuttering")) { tail.Add("@Stutter Edit"); said("Stutter edits"); }
        if (Has(p, "glitch|glitchy")) { tail.Add("@Glitch Vocal"); said("Glitches"); }
        if (Has(p, "chop|chops|chopped")) { tail.Add("@Vocal Chop Generator"); said("Vocal chops"); }
        if (Has(p, "beat repeat|repeats")) { tail.Add("@Beat Repeat"); said("Beat repeats"); }
        if (Has(p, "tape stop")) { tail.Add("@Tape Stop"); said("Tape stop (press its button where you want it)"); }
        // 8. space: dry, reverb, delays and echoes
        bool dry = Has(p, "dry|no reverb|no space|no effects|bone dry"); if (dry) { st.RemoveAll(IsSpace); tail.Add(Rv(12, 70, 4, 3)); said("Dry (echoes and reverb taken out)"); }
        if (!dry && (i = Find(p, "reverb|wet|spacey|spacy|space|big room|hall|plate|cathedral|church|ambient|ambience|room")) >= 0 && !Negated(p, i))
        {
            float a = Amt(p, i); st.RemoveAll(s => s.StartsWith("reverb{"));
            if (Has(p, "hall|cathedral|church|huge reverb|massive reverb")) { tail.Add(Rv(92, 45, 40, 32 * a)); said("Huge hall reverb"); }
            else if (Has(p, "plate")) { tail.Add(Rv(60, 35, 25, 22 * a)); said("Plate reverb"); }
            else if (Has(p, "small room|room")) { tail.Add(Rv(25, 60, 8, 12 * a)); said("Small room"); }
            else { tail.Add(Rv(72, 45, 30, 22 * a)); said((a > 1 ? "Lots of" : a < 1 ? "A bit of" : "Some") + " reverb"); }
        }
        if (!dry && !stutterEcho && (i = Find(p, "delay|echo|echoes|delays")) >= 0 && !Negated(p, i))
        {
            float a = Amt(p, i); int sync = 3; string syncName = "dotted 1/8";
            if (Has(p, "1/4|quarter")) { sync = 1; syncName = "1/4"; } else if (Has(p, "triplet")) { sync = 4; syncName = "1/8 triplet"; } else if (Has(p, "1/16|sixteenth")) { sync = 5; syncName = "1/16"; } else if (Has(p, "dotted")) { sync = 3; syncName = "dotted 1/8"; } else if (Has(p, "1/8|eighth")) { sync = 2; syncName = "1/8"; }
            int mode = Has(p, "ping pong|pingpong|ping") ? 2 : Has(p, "mono") ? 0 : 1; double fb = Has(p, "long|endless|forever|infinite") ? 55 : 30 * Math.Max(1, a);
            if (Has(p, "stadium")) { tail.Add("@Stadium Echo{mix=" + F(30 * a) + "}"); said("Stadium echo"); }
            else if (Has(p, "infinite|endless|forever")) { tail.Add("@Infinite Space{mix=" + F(30 * a) + "}"); said("Endless echo into space"); }
            else if (Has(p, "dark echo|dark delay")) { tail.Add("@Dark Echo{mix=" + F(25 * a) + "}"); said("Dark echo"); }
            else { tail.Add(Dl(sync, 375, fb, 20 * a, mode)); said((a > 1 ? "Lots of " : a < 1 ? "A bit of " : "") + syncName + " delay" + (mode == 2 ? ", ping-pong" : "")); }
        }
        if (!dry && Has(p, "slapback|slap back|slap")) { tail.Add(Dl(0, 110, 12, 18, 0)); said("Slapback echo"); }
        if (Has(p, "no bus")) { r.BusName = null; r.BusChain = null; said("No vocal bus"); }
        var all = new List<string>(); all.AddRange(front); all.AddRange(st); all.AddRange(tail); r.Chain = AiVary.Jitter(string.Join(" > ", all), rnd);
        string title = (prompt ?? "").Trim(); if (title.Length > 42) title = title.Substring(0, 42).TrimEnd() + "..."; r.Name = "AI: " + (title.Length > 0 ? char.ToUpper(title[0]) + title.Substring(1) : bs.Name);
        return r;
    }
}

partial class StudioForm
{
    internal bool aiOpen; internal string aiPrompt = ""; internal PresetPrompt.Result aiResult;
    static readonly string[] AiExamples = { "Juice WRLD with a stutter delay and lots of reverb", "Drake, dark and smooth, slapback", "Eminem rapid fire, dry and loud", "UK drill, dry, bit of distortion", "R&B singing with harmonies and a big hall", "Travis Scott hard tune, huge echo, wide", "Playboi Carti baby voice, distorted", "Afro singing with a 1/4 ping pong delay" };
    internal bool aiBus;   // the same sheet is the AI BUS GENERATOR
    static readonly string[] AiBusExamples = { "Drill drum bus, punchy and dark", "Wide R&B reverb bus", "Juice WRLD vocal bus with more glue and a 1/4 echo", "Heavy 808 bus for rage", "Vintage beat bus, warm tape", "Ad-lib bus with a triplet echo", "Clean mix bus for pop, loud", "Telephone FX bus for UK rap" };
    internal bool aiFx, aiFxFromBrowser; internal int aiSeed; readonly Random aiRnd = new Random();   // aiFx: the AI EFFECT GENERATOR (opened from ADD EFFECT it adds to that track or the master)
    static readonly string[] AiFxExamples = { "Huge dark cathedral reverb with a shimmer", "1/8 dotted ping pong delay, tape, ducked", "Hard autotune in F# minor", "Telephone with a bit of distortion", "Croaky demon voice 9 semitones down", "Slow tremolo on 1/4 notes", "Cut the mud and add air", "Bleep censor" };
    int aiKindLast = -1;   // the words typed stay while you reopen the same generator; another generator starts empty
    void AiOpenCore(int kind) { aiOpen = true; aiBus = kind == 1; aiFx = kind == 2; aiFxFromBrowser = false; aiSeed = 0; stylesOpen = false; aiResult = null; browserOpen = false; if (kind != aiKindLast) aiPrompt = ""; aiKindLast = kind; }
    internal void OpenAiPreset() { AiOpenCore(0); }
    internal void OpenAiBus() { AiOpenCore(1); }
    internal void OpenAiEffect(bool fromBrowser) { bool m = browserMaster; int bt = browserTrack; AiOpenCore(2); aiFxFromBrowser = fromBrowser; browserMaster = fromBrowser && m; browserTrack = bt; }
    string[] AiExs { get { return aiFx ? AiFxExamples : aiBus ? AiBusExamples : AiExamples; } }
    PresetPrompt.Result AiMake(int seed) { return aiFx ? EffectPrompt.Make(aiPrompt, seed) : aiBus ? BusPrompt.Make(aiPrompt, seed) : PresetPrompt.Make(aiPrompt, seed); }
    internal void AiGenerate() { if (aiPrompt.Trim().Length == 0) { Toast("Type what you want first, for example: " + AiExs[0]); return; } aiSeed = 0; aiResult = AiMake(0); }
    // REGENERATE: another take on the same words (another starting version, other amounts); RANDOMISE: a surprise description and its result
    internal void AiRegenerate(int seed = 0) { if (aiPrompt.Trim().Length == 0) { Toast("Type what you want first (or press RANDOMISE for a surprise)."); return; } aiSeed = seed != 0 ? seed : aiRnd.Next(1, int.MaxValue); aiResult = AiMake(aiSeed); }
    internal void AiRandomise(int seed = 0) { var r = seed != 0 ? new Random(seed) : aiRnd; aiPrompt = aiFx ? AiVary.RandomEffect(r) : aiBus ? AiVary.RandomBus(r) : AiVary.RandomVocal(r); aiSeed = r.Next(1, int.MaxValue); aiResult = AiMake(aiSeed); }
    VocalStyles.Style AiStyle() { return aiResult == null ? null : new VocalStyles.Style { Name = aiResult.Name, Genre = "AI", Blurb = "", Chain = aiResult.Chain, BusName = aiResult.BusName, BusChain = aiResult.BusChain }; }
    internal void AiUse() { var s = AiStyle(); if (s == null) return; aiOpen = false; if (aiFx) { AiAddEffects(); return; } if (aiBus) { s.Genre = aiResult.BusName; s.BusName = null; ApplyBusPreset(s); } else ApplyStyle(s, false); }
    // the AI effect goes into YOUR PLUGINS of the track (or the master, from the mastering ADD EFFECT)
    void AiAddEffects()
    {
        var fx = VocalStyles.BuildChain(aiResult.Chain); if (fx.Count == 0) { Toast("That effect could not be built."); return; }
        bool master = aiFxFromBrowser && browserMaster; int ti = aiFxFromBrowser && browserTrack >= 0 ? browserTrack : selTrack;
        if (!master && (eng.Tracks.Count == 0 || ti < 0 || ti >= eng.Tracks.Count)) { Toast("Add or select a track first."); return; }
        PushUndo();
        if (master) { lock (eng.Sync) eng.MasterFx.AddRange(fx); selMaster = eng.MasterFx.Count - 1; Toast(aiResult.Name + " added to the master (" + fx.Count + " plugin" + (fx.Count == 1 ? "" : "s") + "). Ctrl+Z undoes it."); return; }
        var tr = eng.Tracks[ti]; lock (eng.Sync) tr.Fx.AddRange(fx); selTrack = ti; selFx = tr.Fx.Count - 1;
        Toast(aiResult.Name + " added to " + tr.Name + " (" + string.Join(", ", fx.Select(f => f.Title).ToArray()) + "). Every setting is editable; Ctrl+Z undoes it.");
    }
    internal Func<string, string> aiSaveAskHook;
    internal bool AiSave()
    {
        if (aiResult == null) return false; var fx = VocalStyles.BuildChain(aiResult.Chain); if (fx.Count == 0) return false;
        string nm = aiSaveAskHook != null ? aiSaveAskHook(aiResult.Name) : Ask("Name for this preset (it is also shared with the community):", aiResult.Name.Replace("AI: ", "")); if (nm == null || nm.Trim().Length == 0) return false; nm = nm.Trim();
        Presets.SaveChain(nm, fx); Toast("Saved your preset \"" + nm + "\" (" + fx.Count + " plugins). It is in PRESETS... on any track.");
        if (aiSaveAskHook == null) CommunityPresets.AutoShare("chain", nm, Presets.ChainText(fx), ProjectLibrary.ProfileName, msg => { if (msg != null) BeginInvoke(new Action(() => Toast(msg))); });
        return true;
    }
    internal bool AiKey(char c)
    {
        if (!aiOpen) return false;
        if (c == '\r') AiGenerate(); else if (c == '\b') { if (aiPrompt.Length > 0) aiPrompt = aiPrompt.Substring(0, aiPrompt.Length - 1); } else if (!char.IsControl(c) && aiPrompt.Length < 200) aiPrompt += c;
        return true;
    }
    void PaintAiSheet(Graphics g)
    {
        if (!aiOpen) return; float W = ClientSize.Width, H = ClientSize.Height; var all = Rc(0, 0, W, H); Reg(all, () => aiOpen = false);
        float sw = Math.Min(820, W - 24), sh = Math.Min(H - 30, 640); var sheet = PopMove("aipreset", Rc((W - sw) / 2, (H - sh) / 2, sw, sh), 54, 70);
        FillRR(g, TM.C(252, 16, 10, 34), sheet, 22); Glow(g, NeonCyan, sheet, 22, 0.8f, 4); StrokeRR(g, Color.FromArgb(200, NeonCyan), sheet, 22, 1.5f); Reg(sheet, () => { });
        Txt(g, aiFx ? "AI EFFECT GENERATOR" : aiBus ? "AI BUS GENERATOR" : "AI PRESET GENERATOR", 22, Ink, Rc(sheet.X + 22, sheet.Y + 14, 400, 34), StringAlignment.Near, true); var exs = AiExs;
        var close = Rc(sheet.Right - 52, sheet.Y + 14, 32, 32); FillRR(g, Hot(close) ? TM.C(160, 70, 38, 130) : TM.C(110, 40, 24, 80), close, 16); Txt(g, "X", 15, Ink, close, StringAlignment.Center, true); Reg(close, () => aiOpen = false);
        { var back = Rc(close.X - 12 - 170, sheet.Y + 14, 170, 32); if (aiFx) { Btn(g, back, "ADD EFFECT", 0, () => { aiOpen = false; OpenBrowser(browserMaster && aiFxFromBrowser, aiFxFromBrowser ? browserTrack : selTrack); }, 12); Hint2(back, "Back to the list of " + FxRegistry.All.Length + " effects"); } else if (aiBus) { Btn(g, back, "BUS PRESETS", 0, () => { aiOpen = false; OpenBusPresets(); }, 12); Hint2(back, "Back to the list of " + BusPresets.All.Count + " bus presets"); } else { Btn(g, back, "VOCAL PRESETS", 0, () => { aiOpen = false; OpenStyles(); }, 12); Hint2(back, "Back to the list of " + VocalStyles.All.Count + " presets"); } }
        Txt(g, aiFx ? "Describe the effect in your own words: what it is (reverb, delay, autotune, distortion...) and how it should sound. It sets it up for you." : aiBus ? "Describe the bus in your own words: what goes into it (vocals, drums, 808...), the genre, and how it should sound. It builds the plugins." : "Describe the sound in your own words: an artist or a genre, and the effects you want. It builds the plugins for you.", 12, DimC, Rc(sheet.X + 22, sheet.Y + 50, sheet.Width - 44, 20), StringAlignment.Near, false);
        // the prompt box (type, Enter generates, Ctrl+V pastes)
        var pb = Rc(sheet.X + 22, sheet.Y + 80, sheet.Width - 44 - 150, 44); FillRR(g, TM.C(235, 10, 5, 26), pb, 10); StrokeRR(g, NeonCyan, pb, 10, 1.8f); Reg(pb, () => { });
        bool blink = (int)(t * 2) % 2 == 0; Txt(g, aiPrompt.Length == 0 ? (blink ? "|" : " ") + "  e.g. " + exs[0] : aiPrompt + (blink ? "|" : ""), 14, aiPrompt.Length == 0 ? DimC : Ink, Rc(pb.X + 14, pb.Y, pb.Width - 50, pb.Height), StringAlignment.Near, false);
        if (aiPrompt.Length > 0) Btn(g, Rc(pb.Right - 34, pb.Y + 10, 24, 24), "x", 3, () => { aiPrompt = ""; aiResult = null; }, 12);
        var gb = Rc(pb.Right + 10, pb.Y, 140, 44); Btn(g, gb, "GENERATE", 2, () => AiGenerate(), 14); Hint2(gb, "Build the plugins from what you typed (Enter does the same)");
        // examples to start from
        float ex = sheet.X + 22, ey = pb.Bottom + 12; Txt(g, "TRY", 10, DimC, Rc(ex, ey, 36, 26), StringAlignment.Near, true); ex += 38;
        foreach (var e in exs) { float cw = 18 + e.Length * 6.3f; if (ex + cw > sheet.Right - 22) { ex = sheet.X + 60; ey += 32; } if (ey > sheet.Y + 230) break; string ee = e; Chip(g, Rc(ex, ey, cw, 26), e, aiPrompt == e, () => { aiPrompt = ee; AiGenerate(); }); ex += cw + 6; }
        {   // REGENERATE (another take on the same words) and RANDOMISE (a surprise) on every AI generator
            float by = sheet.Bottom - 62; var rg = Rc(sheet.Right - 22 - 170 - 12 - 170, by, 170, 42); var rz = Rc(sheet.Right - 22 - 170, by, 170, 42);
            Btn(g, rg, "REGENERATE", 0, () => AiRegenerate(), 13); Hint2(rg, "Another take on the same words: another starting point and other amounts (press it again for more)");
            Btn(g, rz, "RANDOMISE", 0, () => AiRandomise(), 13); Hint2(rz, "A surprise: random words and what they make (press USE if you like it)");
        }
        float ry = ey + 42; var res = Rc(sheet.X + 22, ry, sheet.Width - 44, sheet.Bottom - 20 - ry - 52); FillRR(g, TM.C(150, 22, 12, 46), res, 14); StrokeRR(g, Color.FromArgb(90, NeonViolet), res, 14, 1);
        if (aiResult == null && aiFx) { using (var hf = new StringFormat { Trimming = StringTrimming.EllipsisWord, FormatFlags = StringFormatFlags.LineLimit }) using (var hb = new SolidBrush(DimC)) g.DrawString("Type what you want and press GENERATE, pick an example above, or press RANDOMISE for a surprise.\n\nIt understands reverbs (room, hall, plate, chamber, spring, cathedral, ambient; tiny to huge; dark or bright; gated, reverse, shimmer, ducked), delays (1/4, 1/8, dotted, triplet, 1/16, ping pong, slapback, tape, long tail, ducked), autotune and harmonies in any key (F# minor, hard, natural), pitch and formant (up 7, octave down, deeper voice, chipmunk), telephone, radio, megaphone, robot, vocoder, distortion (tube, tape, fuzz, clipper), bitcrush, EQ (bass boost, mud, presence, air), compressors, gates, de-essers, chorus, phaser, flanger, tremolo, auto-pan, filter sweeps, stutter, glitch, tape stop, 8D, wide, demon voices and censor bleeps.", GfxCache.Font(13, false), hb, Rc(res.X + 16, res.Y + 14, res.Width - 32, res.Height - 28), hf); return; }
        if (aiResult == null && aiBus) { Txt(g, "Type what you want and press GENERATE, or pick one of the examples above.\nIt understands the kind of bus (vocal, ad-lib, backing vocals, drums, 808 or bass, beat, reverb, echo, FX, crush, glue, mix), genres and artists (it uses their genre), flavours (clean, warm, punchy, wide, dark, bright, vintage, heavy) and changes: more glue, saturation, distortion, dry, lots of reverb, echo (1/4, 1/8, dotted, triplet), telephone, radio, mono, very wide, de-ess, loud.", 12, DimC, Rc(res.X + 16, res.Y + 14, res.Width - 32, res.Height - 28), StringAlignment.Near, false); return; }
        if (aiResult == null) { Txt(g, "Type what you want and press GENERATE, or pick one of the examples above.\nIt understands artists (Drake, Juice WRLD, Eminem, Central Cee, Mazza...), genres, and effects: stutter, delay (1/4, 1/8, dotted, triplet, ping pong), slapback, reverb, hall, plate, dry, dark, bright, autotune, natural, harmony, octave, distortion, telephone, radio, deep or baby voice, wide, doubled, loud, soft, 8D, robot... Add \"a bit\" or \"lots of\" to set how strong.", 12, DimC, Rc(res.X + 16, res.Y + 14, res.Width - 32, res.Height - 28), StringAlignment.Near, false); return; }
        Txt(g, aiResult.Name, 17, Color.FromArgb(255, 255, 214, 110), Rc(res.X + 16, res.Y + 10, res.Width - 32, 28), StringAlignment.Near, true);
        float ly = res.Y + 42; Txt(g, "WHAT IT UNDERSTOOD", 10, NeonCyan, Rc(res.X + 16, ly, 300, 16), StringAlignment.Near, true); ly += 18;
        foreach (var u in aiResult.Understood) { if (ly > res.Bottom - 74) break; Txt(g, "•  " + u, 12, Ink, Rc(res.X + 24, ly, res.Width - 40, 19), StringAlignment.Near, false); ly += 19; }
        ly += 6; Txt(g, "PLUGINS" + (aiFx ? "   (added to YOUR PLUGINS)" : aiBus ? "   (for a " + aiResult.BusName + ")" : aiResult.BusName != null ? "   (+ " + aiResult.BusName + ")" : ""), 10, NeonCyan, Rc(res.X + 16, ly, res.Width - 32, 16), StringAlignment.Near, true); ly += 18;
        var titles = VocalStyles.BuildChain(aiResult.Chain).Select(f => f.Title).ToList(); using (var pf = new StringFormat { Trimming = StringTrimming.EllipsisWord, FormatFlags = StringFormatFlags.LineLimit }) using (var pbr = new SolidBrush(Ink)) g.DrawString(string.Join("  >  ", titles), GfxCache.Font(12, false), pbr, Rc(res.X + 24, ly, res.Width - 40, Math.Max(20, res.Bottom - ly - 8)), pf);   // the plugins in order
        var ub = Rc(sheet.X + 22, sheet.Bottom - 62, 200, 42); Btn(g, ub, aiFx ? (aiFxFromBrowser && browserMaster ? "ADD TO MASTER" : "ADD TO TRACK") : aiBus ? "USE ON BUS" : "USE ON TRACK", 2, () => AiUse(), 14); Hint2(ub, aiFx ? "Add these plugins to YOUR PLUGINS (Ctrl+Z undoes it)" : aiBus ? "Put these plugins on the picked bus, the bus the picked track goes into, or a new bus (Ctrl+Z undoes it)" : "Put these plugins on the selected track (Ctrl+Z undoes it)");
        var sb = Rc(ub.Right + 12, ub.Y, 200, 42); Btn(g, sb, "SAVE AS MY PRESET", 1, () => AiSave(), 13); Hint2(sb, "Keep it as your own preset (in PRESETS... on any track), shared in Community presets");
    }
}
