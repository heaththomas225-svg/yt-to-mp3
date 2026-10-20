using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

// Shared data types used by the engine and the studio (moved out of the UI files so the cross-platform core can use them)
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
        if (rnd != null) r.Understood.Add("Another take: the same sound with its amounts nudged");
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

class AutoLane
{
    public Fx Fx; public string Param = ""; public bool On = true; public readonly List<AutoPoint> Pts = new List<AutoPoint>();
    public void SortPts() { Pts.Sort((a, b) => a.Frame.CompareTo(b.Frame)); }
    // the value at a song position (frames). Before the first point and after the last one it holds that point's value. NaN when there are no points.
    public float ValueAt(long frame, bool steps)
    {
        int n = Pts.Count; if (n == 0) return float.NaN; if (n == 1 || frame <= Pts[0].Frame) return Pts[0].Value; if (frame >= Pts[n - 1].Frame) return Pts[n - 1].Value;
        int lo = 0, hi = n - 1; while (hi - lo > 1) { int m = (lo + hi) / 2; if (Pts[m].Frame <= frame) lo = m; else hi = m; }
        var a = Pts[lo]; var b = Pts[hi]; if (steps) return a.Value;
        double t = (frame - a.Frame) / (double)Math.Max(1, b.Frame - a.Frame); return (float)(a.Value + (b.Value - a.Value) * t);
    }
    public AutoLane CloneFor(Fx fx) { var c = new AutoLane { Fx = fx, Param = Param, On = On }; foreach (var p in Pts) c.Pts.Add(new AutoPoint { Frame = p.Frame, Value = p.Value }); return c; }

    // "frame:value,frame:value" for the project file
    public string PointsText()
    {
        var sb = new StringBuilder(); foreach (var p in Pts) { if (sb.Length > 0) sb.Append(','); sb.Append(p.Frame.ToString(CultureInfo.InvariantCulture)).Append(':').Append(p.Value.ToString("R", CultureInfo.InvariantCulture)); }
        return sb.ToString();
    }
    public static void ParsePoints(AutoLane lane, string s)
    {
        foreach (var part in (s ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var q = part.Split(':'); long fr; float v; if (q.Length == 2 && long.TryParse(q[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out fr) && float.TryParse(q[1], NumberStyles.Float, CultureInfo.InvariantCulture, out v) && fr >= 0) lane.Pts.Add(new AutoPoint { Frame = fr, Value = v });
        }
        lane.SortPts();
    }
}

sealed class MasterPreset { public string Name, Blurb; public float Target; public string[] Slots; public string Group; }
