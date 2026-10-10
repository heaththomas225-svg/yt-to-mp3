using System;
using System.Collections.Generic;

// The packs Purphex can make. Each row is a Recipe: the genre, drum character, bass flavours, tempos and keys. New packs are one more row.
static class SoundCatalog
{
    public static readonly List<Recipe> Recipes = new List<Recipe>();
    static int Seed(string id) { uint h = 2166136261; foreach (char c in id) { h ^= c; h *= 16777619; } return (int)(h & 0x7FFFFFFF); }
    static void A(string id, string name, string section, string genre, string sub, string drums, string flavours, string bpms, string keys, string mood, string tags, string desc, string parts = "all")
    {
        var bp = new List<double>(); foreach (var s in bpms.Split(',')) bp.Add(double.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture));
        var ks = new List<string>(); foreach (var s in keys.Split(',')) ks.Add(s.Trim());
        int seed = Seed(id);
        Recipes.Add(new Recipe { Id = id, Name = name, Section = section, Genre = genre, Sub = sub, Drums = drums, Flavours = flavours.Split(','), Bpms = bp.ToArray(), Keys = ks.ToArray(), Mood = mood, Tags = tags, Desc = desc, Parts = parts, Family = section, Seed = seed, Hue = seed % 360 });
    }
    public static Recipe Find(string id) { return Recipes.Find(r => r.Id == id); }

    const string RollerBass = "reese,dirty,growl,sub,stab,talking", JumpBass = "wobble,growl,talking,dirty,stab,rubber", NeuroBass = "neuro,metallic,growl,reese,sub,stab", LiquidBass = "sub,pluck,organ,reese,stab,pluck", BasslineBass = "wobble,talking,rubber,elastic,organ,stab", UkgBass = "sub,organ,stab,rubber,pluck,reese", RapBass = "sub,reese,pluck,stab", DubBass = "wobble,growl,riddim,metallic,talking,sub", HouseBass = "pluck,rubber,stab,organ,sub,reese";
    const string DnbKeys = "F min,G min,E min,D min,A min,C min";

    static SoundCatalog()
    {
        // ---- Drum & Bass ----
        const string D = "Drum & Bass";
        A("dnb-rollers", "DnB Rollers", "DnB", D, "Rollers", "dnb_roller", RollerBass, "172,174,175", DnbKeys, "Rolling, dark, club", "rollers,dnb", "Rolling breakbeats, punchy kicks, crisp snares, ghost-note percussion, reese and sub basses, pads and transitions.");
        A("jumpup-dnb", "Jump-Up DnB", "DnB", D, "Jump-Up", "dnb_jumpup", JumpBass, "172,174,176", DnbKeys, "Aggressive, bouncy", "jump-up,dnb", "Bass stabs, wobbles, bouncy drum loops, fills, impacts and drop sounds.");
        A("liquid-dnb", "Liquid DnB", "DnB", D, "Liquid", "dnb_liquid", LiquidBass, "170,172,174", "A min,D min,F min,G min,E min", "Warm, smooth, soulful", "liquid,dnb,melodic", "Musical basslines, smooth pads, soulful chords, atmospheres and rolling drums.");
        A("naughty-dnb-rollers", "Naughty DnB Rollers", "DnB", D, "Naughty Rollers", "dnb_roller", RollerBass, "172,174,175,176", DnbKeys, "Gritty, dark, club", "naughty,rollers,dnb,club", "Rolling basslines, gritty mids, heavy subs, tight drums and dark club sounds.");
        A("jumpup-reactor", "Jump-Up Reactor", "DnB", D, "Jump-Up", "dnb_jumpup", JumpBass, "172,174,176", DnbKeys, "Aggressive, energetic", "jump-up,dnb,reactor", "Punchy jump-up kits with bouncing basses, stabs and big drops.");
        A("filthy-jumpup", "Filthy Jump-Up", "DnB", D, "Jump-Up", "dnb_jumpup", "growl,dirty,talking,wobble,stab,rubber", "174,175,176", "F min,E min,G min,D min", "Dirty, aggressive", "jump-up,filthy,dnb,growl", "Dirty growls, talking basses and hard-hitting drums.");
        A("jumpup-rollers", "Jump-Up Rollers", "DnB", D, "Jump-Up", "dnb_roller", "wobble,reese,dirty,stab,growl,sub", "172,174,175", DnbKeys, "Bouncy, rolling", "jump-up,rollers,dnb", "Where jump-up bounce meets rolling drums.");
        A("wobble-vault", "The Wobble Vault", "DnB", D, "Wobble", "dnb_jumpup", "wobble,wobble,growl,talking,rubber,dirty", "172,174,176", DnbKeys, "Wobbly, heavy", "wobble,dnb,bass", "Wobble basses at every speed, with the drums to match.");
        A("bass-face", "Bass Face", "DnB", D, "Wobble", "dnb_neuro", "growl,dirty,talking,metallic,wobble,sub", "172,174,176", "F min,E min,G min", "Nasty, heavy", "bass face,growl,dnb", "Faces-pulling growls and nasty mids.");
        A("dirty-bassline-rollers", "Dirty Bassline Rollers", "DnB", D, "Rollers", "dnb_roller", "dirty,reese,growl,talking,sub,stab", "172,174,175", DnbKeys, "Dirty, rolling", "dirty,rollers,bassline,dnb", "Dirty rolling basslines on tight breaks.");
        A("minimal-dnb", "Minimal DnB", "DnB", D, "Minimal", "dnb_minimal", "sub,reese,pluck,stab,dirty,sub", "172,174", "F min,G min,A min", "Sparse, deep", "minimal,dnb,deep", "Space, subs and tight drums for stripped-back tunes.");
        A("dark-rollers", "Dark Rollers", "DnB", D, "Dark Rollers", "dnb_dark", "reese,dirty,sub,growl,stab,metallic", "172,174,175", "F min,E min,D min,C min", "Dark, menacing", "dark,rollers,dnb", "Dark atmospheres, heavy kicks and brooding rollers.");
        A("heavy-rollers", "Heavy Rollers", "DnB", D, "Heavy Rollers", "dnb_roller", "reese,growl,dirty,sub,stab,talking", "174,175,176", DnbKeys, "Heavy, powerful", "heavy,rollers,dnb", "Big subs and weighty drums.");
        A("dancefloor-dnb", "Dancefloor DnB", "DnB", D, "Dancefloor", "dnb_twostep", "rubber,reese,stab,talking,wobble,sub", "172,174,176", "G min,A min,F min,D min", "Uplifting, club", "dancefloor,dnb,club", "Sounds that move a crowd.");
        A("neurofunk", "Neurofunk", "DnB", D, "Neurofunk", "dnb_neuro", NeuroBass, "172,174,175", "F min,E min,G min,D min", "Technical, dark", "neuro,neurofunk,dnb", "Twisting neuro basses and tight, technical drums.");
        A("techstep", "Techstep", "DnB", D, "Techstep", "dnb_neuro", NeuroBass, "172,174,176", "F min,E min,D min", "Cold, mechanical", "techstep,dnb", "Cold mechanical basses and hard drums.");
        A("darkstep", "Darkstep", "DnB", D, "Darkstep", "dnb_dark", "metallic,reese,sub,growl,stab,neuro", "172,174,176", "F min,D min,C min", "Dark, horror", "darkstep,dnb,dark", "Doom-laden basses and dark cinematic touches.");
        A("liquid-funk", "Liquid Funk", "DnB", D, "Liquid Funk", "dnb_liquid", LiquidBass, "172,174", "A min,D min,G min,E min", "Funky, warm", "liquid,funk,dnb", "Funky liquid grooves and rolling bass.");
        A("soulful-rollers", "Soulful Rollers", "DnB", D, "Soulful Rollers", "dnb_roller", "sub,organ,pluck,reese,stab,pluck", "172,174", "A min,F min,D min", "Soulful, rolling", "soulful,rollers,dnb", "Rolling drums under warm, soulful chords.");
        A("atmospheric-dnb", "Atmospheric DnB", "DnB", D, "Atmospheric", "dnb_liquid", "sub,pluck,organ,reese,stab,sub", "170,172,174", "A min,D min,F min,E min", "Dreamy, spacious", "atmospheric,dnb,ambient", "Wide pads, space and soft drums.");
        A("deep-dnb", "Deep DnB", "DnB", D, "Deep", "dnb_minimal", "sub,reese,pluck,stab,dirty,organ", "172,174", "F min,G min,E min", "Deep, moody", "deep,dnb", "Deep subs and moody textures.");
        A("melodic-dnb", "Melodic DnB", "DnB", D, "Melodic", "dnb_liquid", LiquidBass, "172,174,176", "A min,C min,D min,F min", "Emotional, uplifting", "melodic,dnb,emotional", "Chord and melody loops for big, emotional tunes.");
        A("vocal-dnb", "Vocal DnB", "DnB", D, "Vocal", "dnb_twostep", LiquidBass, "172,174", "A min,D min,F min", "Vocal-led, warm", "vocal,dnb", "Voice-style chops, chants and shouts with matching drums and bass.");
        A("jungle-breakbeat", "Jungle and Breakbeat", "DnB", D, "Jungle", "jungle", "sub,reese,stab,wobble,pluck,dirty", "160,165,168", "F min,G min,D min", "Raw, chopped", "jungle,breakbeat,dnb", "Chopped breakbeats and deep jungle subs.");
        A("ragga-jungle", "Ragga Jungle", "DnB", D, "Ragga Jungle", "jungle", "sub,wobble,stab,reese,rubber,dirty", "160,164,168", "F min,A min,G min", "Raw, sound-system", "ragga,jungle,dnb", "Sound-system bass and rolling jungle breaks.");
        A("halftime-dnb", "Half-Time DnB", "DnB", D, "Half-Time", "dnb_halftime", "wobble,growl,sub,reese,talking,metallic", "85,87,88", "F min,G min,E min", "Heavy, slow", "halftime,dnb", "Half-time drums at 85 to 88 BPM with heavy basses.");
        A("industrial-dnb", "Industrial DnB", "DnB", D, "Industrial", "dnb_neuro", "metallic,neuro,growl,dirty,sub,stab", "172,174,176", "F min,D min,E min", "Harsh, mechanical", "industrial,dnb", "Metallic, harsh textures and clanking drums.");
        A("old-school-rave-dnb", "Old-School Rave DnB", "DnB", D, "Old-School Rave", "dnb_rave", "stab,reese,sub,rubber,organ,wobble", "168,170,172", "A min,G min,F min", "Nostalgic, raving", "old school,rave,dnb", "Rave stabs, breakbeats and hoover-style basses.");
        A("festival-dnb", "Festival DnB", "DnB", D, "Festival", "dnb_jumpup", "wobble,growl,stab,talking,reese,sub", "174,175,176", "F min,G min,E min", "Huge, energetic", "festival,dnb,drop", "Big-room drops and crowd-pleasing basses.");
        A("dnb-drop-toolkit", "DnB Drop Toolkit", "DnB", D, "Drop Toolkit", "dnb_roller", "growl,reese,wobble,sub,stab,talking", "172,174,176", DnbKeys, "Hard-hitting", "drop,toolkit,dnb,fx", "Everything for a drop: impacts, risers, downlifters, sweeps, fills and bass hits.", "drums,bass,loops,fx,midi");
        A("dnb-transitions-fills", "DnB Transitions and Fills", "DnB", D, "Transitions and Fills", "dnb_roller", "reese,sub", "172,174,176", "F min,G min", "Building, dramatic", "transition,fill,dnb,fx", "Drum fills, risers, reverses, sweeps and impacts.", "drums,loops,fx");

        // ---- UK Bassline ----
        const string B = "UK Bassline";
        A("ultimate-bassline-vault", "Ultimate Bassline Vault", "UK Bassline", B, "Bassline", "bassline_4x4", BasslineBass, "134,136,138", "F min,G min,A min,E min", "Heavy, club", "bassline,uk bassline,4x4", "The big all-round bassline collection.");
        A("naughty-bassline", "Naughty Bassline", "UK Bassline", B, "Naughty", "bassline_4x4", "wobble,talking,dirty,rubber,stab,growl", "134,136,138", "F min,G min,E min", "Naughty, gritty", "naughty,bassline", "Naughty, gritty basslines and bouncing drums.");
        A("filthy-wobblers", "Filthy Wobblers", "UK Bassline", B, "Wobblers", "bassline_4x4", "wobble,wobble,growl,talking,dirty,rubber", "134,136,138", "F min,E min,G min", "Filthy, wobbly", "wobble,bassline", "Heavy wobble basses.");
        A("bassline-after-dark", "Bassline After Dark", "UK Bassline", B, "After Dark", "bassline_4x4", "reese,dirty,sub,growl,stab,talking", "134,136", "F min,D min,C min", "Dark, late night", "dark,bassline", "Late-night, darker bassline sounds.");
        A("wobble-bass-nation", "Wobble Bass Nation", "UK Bassline", B, "Wobblers", "bassline_4x4", "wobble,wobble,talking,rubber,growl,sub", "134,136,138", DnbKeys, "Wobbly, big", "wobble,bassline", "Wobble basses with every flavour.");
        A("talking-bassline", "Talking Bassline", "UK Bassline", B, "Talking", "bassline_4x4", "talking,talking,rubber,wobble,dirty,stab", "134,136,138", "F min,G min,A min", "Vocal-like, bouncy", "talking,bassline,formant", "Vowel-style talking basses.");
        A("elastic-basslines", "Elastic Basslines", "UK Bassline", B, "Elastic", "bassline_4x4", "elastic,rubber,elastic,wobble,stab,organ", "134,136,138", "F min,G min,E min", "Stretchy, bouncy", "elastic,rubber,bassline", "Stretchy, gliding rubber basses.");
        A("speed-garage-revival", "Speed Garage Revival", "UK Bassline", B, "Speed Garage", "ukg_speed", "rubber,organ,stab,wobble,sub,talking", "130,132,134", "F min,G min,A min", "Swinging, fun", "speed garage,bassline", "A fresh take on speed garage.");
        A("bassline-rollers", "Bassline Rollers", "UK Bassline", B, "Rollers", "bassline_4x4", "wobble,rubber,dirty,talking,sub,stab", "136,138", "F min,G min,E min", "Rolling, driving", "rollers,bassline", "Rolling 4x4 basslines.");
        A("rubberband-bass", "Rubberband Bass", "UK Bassline", B, "Rubber", "bassline_4x4", "rubber,elastic,rubber,wobble,stab,talking", "134,136,138", "F min,G min", "Bouncy, stretchy", "rubber,bassline", "Rubbery, bouncing basses.");
        A("dark-bassline", "Dark Bassline", "UK Bassline", B, "Dark", "bassline_4x4", "reese,dirty,growl,sub,talking,stab", "134,136", "F min,D min,E min", "Dark, heavy", "dark,bassline", "Dark, heavy bass.");
        A("bouncy-bassline", "Bouncy Bassline", "UK Bassline", B, "Bouncy", "bassline_4x4", "rubber,wobble,elastic,organ,stab,pluck", "134,136,138", "G min,A min,F min", "Bouncy, fun", "bouncy,bassline", "Fun, bouncing basses and shuffling drums.");
        A("bassline-anthems", "Bassline Anthems", "UK Bassline", B, "Anthems", "bassline_4x4", "wobble,stab,organ,talking,rubber,sub", "134,136,138", "F min,G min,A min", "Big, crowd-pleasing", "anthem,bassline", "Big hooks and anthem-sized basses.");
        A("uk-bass-underground", "UK Bass Underground", "UK Bassline", B, "Underground", "ukg_2step", "reese,dirty,sub,growl,stab,rubber", "132,134,136", "F min,E min,C min", "Underground, raw", "uk bass,underground", "Raw underground bass music.");
        A("rave-bassline", "Rave Bassline", "UK Bassline", B, "Rave", "dnb_rave", "stab,rubber,reese,wobble,organ,sub", "136,138,140", "A min,G min,F min", "Raving, energetic", "rave,bassline", "Rave stabs, organ bass and club shouts.");
        A("bassline-drop-toolkit", "Bassline Drop Toolkit", "UK Bassline", B, "Drop Toolkit", "bassline_4x4", "wobble,talking,rubber,growl,stab,sub", "134,136,138", "F min,G min,A min", "Hard-hitting", "drop,toolkit,bassline,fx", "Builds, drops, impacts, sweeps and transitions for bassline.", "drums,bass,loops,fx,midi");

        // ---- UK Garage ----
        const string G = "UK Garage";
        A("uk-garage", "UK Garage", "UK Garage", G, "UKG", "ukg_2step", UkgBass, "130,132,134", "C min,F min,G min,A min", "Shuffled, soulful", "garage,ukg", "Shuffled drum loops, syncopated percussion, bass stabs, chord loops, organ sounds and vocal-style chops.");
        A("ukg-essentials", "UKG Essentials", "UK Garage", G, "UKG", "ukg_2step", UkgBass, "130,132,134", "C min,F min,G min,A min", "Swinging, classic", "garage,ukg,essentials", "Everything you need to start a garage tune.");
        A("naughty-ukg", "Naughty UKG", "UK Garage", G, "Naughty", "ukg_2step", "rubber,sub,stab,organ,wobble,talking", "130,132,134", "F min,G min,C min", "Naughty, skippy", "naughty,ukg,garage", "Naughty garage grooves and warped basses.");
        A("speed-garage", "Speed Garage", "UK Garage", G, "Speed Garage", "ukg_speed", "rubber,organ,stab,wobble,sub,talking", "130,132,134", "F min,G min,A min", "Fast, swinging", "speed garage,ukg", "Four-to-the-floor garage with swinging hats and warped bass.");
        A("two-step-essentials", "2-Step Essentials", "UK Garage", G, "2-Step", "ukg_2step", "sub,organ,stab,rubber,pluck,reese", "130,132,134", "C min,F min,A min", "Skippy, smooth", "2-step,garage", "Classic 2-step drums and deep bass.");
        A("dark-garage", "Dark Garage", "UK Garage", G, "Dark", "ukg_2step", "reese,sub,dirty,stab,growl,organ", "130,132", "C min,F min,D min", "Dark, moody", "dark,garage", "Dark, brooding garage.");
        A("bass-heavy-garage", "Bass-Heavy Garage", "UK Garage", G, "Bass-Heavy", "ukg_2step", "sub,reese,wobble,dirty,rubber,growl", "130,132,134", "F min,G min,E min", "Heavy, deep", "bass,garage", "Garage with a weighty low end.");
        A("garage-rollers", "Garage Rollers", "UK Garage", G, "Rollers", "ukg_2step", "sub,rubber,reese,stab,pluck,organ", "132,134", "F min,G min", "Rolling, driving", "rollers,garage", "Rolling garage drums and bass.");
        A("future-garage", "Future Garage", "UK Garage", G, "Future Garage", "ukg_2step", "sub,pluck,reese,organ,stab,pluck", "128,130,132", "A min,D min,F min,E min", "Hazy, emotional", "future garage,ukg", "Hazy pads, soft drums and deep bass.");
        A("deep-ukg", "Deep UKG", "UK Garage", G, "Deep", "ukg_2step", "sub,pluck,organ,reese,stab,pluck", "130,132", "C min,F min,A min", "Deep, warm", "deep,ukg,garage", "Deep, warm garage.");
        A("garage-vocal-chops", "Garage Vocal Chops", "UK Garage", G, "Vocal Chops", "ukg_2step", "sub,organ", "130,132,134", "C min,F min,G min,A min", "Soulful, chopped", "vocal,chops,garage", "Synth-voice chops, shouts and chants for garage.", "drums,loops,vocal,midi,fx");
        A("garage-organ-stabs", "Garage Organ Stabs", "UK Garage", G, "Organ Stabs", "ukg_2step", "organ,organ,stab,rubber,sub,pluck", "130,132,134", "C min,F min,G min", "Classic, stabby", "organ,stabs,garage", "Organ stabs and chord hits.", "drums,music,midi,presets,loops");
        A("garage-piano-chords", "Garage Piano and Chords", "UK Garage", G, "Piano and Chords", "ukg_2step", "organ,pluck", "130,132", "C min,F min,A min,G min", "Soulful, jazzy", "piano,chords,garage", "Keys-style chords and loops in many keys.", "music,midi,presets,loops,drums");
        A("shuffled-drum-grooves", "Shuffled Drum Grooves", "UK Garage", G, "Drum Grooves", "ukg_2step", "sub,rubber", "130,132,134,136", "C min,F min", "Shuffled, swinging", "shuffled,drums,garage", "Swinging and shuffled drum patterns, hits and fills.", "drums,loops,midi");
        A("afterhours-garage", "Afterhours Garage", "UK Garage", G, "Afterhours", "ukg_2step", "sub,reese,organ,stab,pluck,rubber", "130,132", "C min,F min,D min", "Late night, hazy", "afterhours,garage", "Late-night garage sounds.");
        A("garage-drop-toolkit", "Garage Drop Toolkit", "UK Garage", G, "Drop Toolkit", "ukg_speed", "rubber,wobble,stab,sub,talking,organ", "130,132,134", "F min,G min,C min", "Hard-hitting", "drop,toolkit,garage,fx", "Builds, drops, impacts and transitions for garage.", "drums,bass,loops,fx,midi");

        // ---- Other electronic bass ----
        const string E = "Electronic Bass";
        A("dubstep-riddim", "Dubstep and Riddim", "Electronic Bass", E, "Dubstep and Riddim", "riddim", DubBass, "140,150", "F min,E min,G min,D min", "Heavy, minimal", "dubstep,riddim", "Wobbles, chugs and heavy half-time drums.");
        A("heavy-dubstep", "Heavy Dubstep", "Electronic Bass", E, "Heavy Dubstep", "dubstep", DubBass, "140,145", "F min,E min,D min", "Heavy, aggressive", "dubstep,heavy", "Heavy dubstep growls and drums.");
        A("deep-dubstep", "Deep Dubstep", "Electronic Bass", E, "Deep Dubstep", "dubstep", "sub,reese,wobble,pluck,stab,metallic", "138,140", "F min,A min,E min", "Deep, dark", "dubstep,deep", "Deep, spacious dubstep.");
        A("tearout-bass-design", "Tearout-Style Bass Design", "Electronic Bass", E, "Tearout", "dubstep", "growl,metallic,dirty,talking,neuro,wobble", "150,170", "F min,E min,G min", "Brutal, harsh", "tearout,bass design", "Harsh, brutal bass patches and drums.");
        A("bass-house", "Bass House", "Electronic Bass", E, "Bass House", "house", HouseBass, "126,128", "F min,G min,A min", "Bouncy, club", "bass house,house", "Bouncing bass house sounds.");
        A("uk-bass", "UK Bass", "Electronic Bass", E, "UK Bass", "ukg_2step", "reese,rubber,sub,stab,wobble,dirty", "132,134,136", "F min,G min,C min", "Skippy, heavy", "uk bass", "Skippy drums with heavy bass.");
        A("electro-house", "Electro House", "Electronic Bass", E, "Electro House", "house", "reese,stab,wobble,sub,dirty,pluck", "126,128,130", "F min,G min,A min", "Big, filtered", "electro house", "Big leads, saws and four-to-the-floor drums.");
        A("tech-house", "Tech House", "Electronic Bass", E, "Tech House", "house", "pluck,rubber,sub,stab,organ,reese", "124,126,128", "F min,G min,A min,C min", "Groovy, driving", "tech house,house", "Groovy tech house drums and bass.");
        A("minimal-house", "Minimal House", "Electronic Bass", E, "Minimal House", "house", "pluck,sub,rubber,stab,organ,pluck", "122,124,126", "F min,A min,G min", "Sparse, hypnotic", "minimal,house", "Sparse, hypnotic grooves.");
        A("rave-hardcore", "Rave and Hardcore", "Electronic Bass", E, "Rave and Hardcore", "dnb_rave", "stab,reese,wobble,sub,organ,rubber", "160,165,170", "A min,G min,F min", "Fast, raving", "rave,hardcore", "Rave stabs and fast breakbeats.");
        A("jungle-breaks", "Jungle Breaks", "Electronic Bass", E, "Jungle Breaks", "jungle", "sub,reese,stab,wobble,dirty,pluck", "160,165,168", "F min,G min,D min", "Raw, chopped", "jungle,breaks", "Chopped breaks and deep subs.");
        A("breakbeat", "Breakbeat", "Electronic Bass", E, "Breakbeat", "jungle", "reese,sub,stab,wobble,rubber,pluck", "130,135,140", "F min,A min,G min", "Funky, cut-up", "breakbeat,breaks", "Funky cut-up breaks.");
        A("halftime", "Halftime", "Electronic Bass", E, "Halftime", "dnb_halftime", "wobble,growl,sub,reese,metallic,talking", "85,87,140", "F min,G min,E min", "Slow, heavy", "halftime,bass", "Half-time drums and heavy bass.");
        A("glitch-bass", "Glitch Bass", "Electronic Bass", E, "Glitch Bass", "dnb_neuro", "metallic,neuro,rubber,growl,stab,talking", "110,128,140", "F min,E min,G min", "Glitchy, twisted", "glitch,bass", "Twisted, glitchy basses.");
        A("experimental-bass-design", "Experimental Bass Design", "Electronic Bass", E, "Experimental", "dnb_neuro", "metallic,neuro,talking,growl,elastic,wobble", "100,128,140,160", "F min,E min,A min,G min", "Strange, experimental", "experimental,bass design", "Strange new bass sounds to build from.");

        // ---- Rap and vocal production ----
        const string R = "Rap and Vocals";
        A("trap-essentials", "Trap Essentials", "Rap", R, "Trap", "trap", RapBass, "140,145,150", "F min,G min,C# min,D min", "Hard, dark", "trap,rap,808", "Hard kicks, claps, snares, rolling hats, 808s, dark melodies and transitions.");
        A("trap-drums-808s", "Trap Drums and 808s", "Rap", R, "Trap", "trap", "sub,reese", "140,145,150", "F min,G min,C# min,D min,A min", "Hard, heavy", "trap,808,drums", "Trap drum kits and tuned 808s.", "drums,sub808,loops,midi,fx");
        A("uk-drill", "UK Drill", "Rap", R, "UK Drill", "drill", RapBass, "140,142,144", "F min,E min,G min,C# min", "Dark, sliding", "drill,uk drill,808", "Sliding 808 basslines, punchy drums, dark melodic loops, percussion and atmospheres.");
        A("uk-drill-drums-808-midi", "UK Drill Drums and Sliding 808 MIDI", "Rap", R, "UK Drill", "drill", "sub,reese", "140,142,144", "F min,E min,G min,C# min", "Dark, sliding", "drill,808,midi", "Drill drums and sliding 808 MIDI lines.", "drums,sub808,loops,midi");
        A("grime-kits", "Grime Kits", "Rap", R, "Grime", "dubstep", "reese,stab,dirty,metallic,sub,wobble", "140", "F min,G min,E min", "Cold, gritty", "grime,rap", "Grime drum kits and synth stabs.");
        A("uk-rap-production", "UK Rap Production", "Rap", R, "UK Rap", "boombap", RapBass, "130,140,145", "F min,A min,D min,G min", "Gritty, British", "uk rap,rap", "Production sounds for UK rap.");
        A("melodic-rap", "Melodic Rap", "Rap", R, "Melodic Rap", "trap", "sub,pluck,reese", "130,140,150", "A min,F min,D min,C min", "Emotional, melodic", "melodic rap,rap", "Melodic loops, soft 808s and airy pads.");
        A("cloud-rap", "Cloud Rap", "Rap", R, "Cloud Rap", "trap", "sub,pluck,organ", "120,130,140", "F min,A min,E min", "Dreamy, hazy", "cloud rap,rap,ambient", "Hazy pads, dreamy keys and soft drums.");
        A("dark-rap", "Dark Rap", "Rap", R, "Dark Rap", "drill", "sub,reese,metallic,pluck", "130,140,145", "F min,D min,C# min,E min", "Dark, menacing", "dark rap,rap", "Dark piano-style loops, deep 808s and cold drums.");
        A("boom-bap", "Boom Bap", "Rap", R, "Boom Bap", "boombap", "sub,pluck,organ,stab", "85,90,95", "A min,D min,G min,F min", "Dusty, classic", "boom bap,hip-hop", "Dusty drums, jazzy chords and deep bass.");
        A("rnb-melodic-vocal-textures", "R&B and Melodic Vocal Textures", "Rap", R, "R&B", "ukg_2step", "sub,pluck,organ", "70,80,90", "C min,F min,A min,D min", "Smooth, soulful", "r&b,vocal textures", "Soft keys, pads and voice-style textures.", "music,vocal,midi,presets,loops,drums");
        A("rap-ad-lib-fx", "Rap Ad-Lib FX", "Rap", R, "Ad-Libs", "trap", "sub", "130,140,150", "F min,G min,A min", "Hype, punchy", "ad-lib,vocal fx,rap", "Synth-voice shouts, hype calls and vocal effects.", "vocal,fx");
        A("vocal-chop-kits", "Vocal Chop Kits", "Rap", R, "Vocal Chops", "trap", "sub,pluck", "130,140,150", "F min,G min,A min,D min", "Chopped, melodic", "vocal chops,rap", "Synth-voice chop loops in many keys.", "vocal,midi,loops,drums");
        A("vocal-transition-fx", "Vocal Transition FX", "Rap", R, "Vocal Transitions", "trap", "sub", "130,140", "F min,A min", "Building, dramatic", "vocal fx,transition", "Voice-style risers, swells and transitions.", "vocal,fx");
        A("dark-piano-loops", "Dark Piano Loops", "Rap", R, "Dark Piano", "drill", "sub,pluck", "130,140,145", "F min,D min,C# min,E min", "Dark, eerie", "piano,dark,rap,loops", "Dark keys-style loops and melodies.", "music,midi,presets,loops,drums");
        A("guitar-synth-loops", "Guitar and Synth Loops", "Rap", R, "Guitar and Synth", "trap", "pluck,sub", "130,140,150", "A min,F min,E min,G min", "Melodic, emotional", "guitar,synth,loops,rap", "Plucked string and synth loops that suit rap beats.", "music,midi,presets,loops,drums");
        A("rap-vocals", "Rap Vocals", "Rap", R, "Vocals", "trap", "sub", "130,140,150", "F min,A min,G min", "Hype, hooky", "rap vocals,ad-lib", "Original synth-voice ad-libs, textures, one-shots and chop loops. There is no recorded voice in this pack.", "vocal,fx,loops,drums");

        // ---- Cinematic FX and signature sounds ----
        A("cinematic-fx", "Cinematic FX", "FX", "Cinematic", "FX", "dnb_roller", "sub,reese", "100,120,140", "F min,D min,A min", "Epic, dramatic", "cinematic,fx,risers", "Risers, downlifters, impacts, sweeps, reverses and transitions.", "fx,music");
        A("purphex-signature", "Purphex Signature Sounds", "Purphex", "Signature", "Signature", "dnb_roller", "reese,growl,wobble,sub,rubber,stab", "140,172,174", "F min,G min,A min", "Purphex, purple, powerful", "signature,purphex", "A hand-picked starter: drums, basses, loops and effects in the Purphex style.");
    }
}

// the shared toolkit: views over every installed pack (the same files appear in them, nothing is copied)
static class SoundToolkit
{
    public class Set { public string Name; public SoundLibrary.Filter Filter; }
    public static readonly List<Set> Sets = new List<Set>();
    static void A(string name, SoundLibrary.Filter f) { Sets.Add(new Set { Name = name, Filter = f }); }
    static SoundLibrary.Filter T(params string[] types) { return new SoundLibrary.Filter { AnyType = types }; }
    static SoundLibrary.Filter G(string group, string kind = "") { return new SoundLibrary.Filter { Group = group, Kind = kind }; }
    static SoundToolkit()
    {
        A("Ultimate Drum Arsenal", G("Drums")); A("Kick Collection", T("Kick")); A("Snare Collection", T("Snare", "Clap")); A("Hi-Hat Collection", T("Hi-Hat", "Open Hat")); A("Percussion Collection", T("Percussion", "Ride", "Cymbal"));
        A("Bass One-Shots", G("Bass", "one-shot")); A("Bass Loops", T("Bass Loop")); A("Synth One-Shots", G("Synth", "one-shot")); A("Synth Loops", new SoundLibrary.Filter { Group = "Synth", Kind = "loop" });
        A("Reese Bass Laboratory", T("Reese Bass")); A("Wobble Bass Laboratory", T("Wobble Bass")); A("Sub-Bass Collection", T("Sub Bass", "808")); A("Rave Stabs", T("Rave Stab", "Organ Stab", "Bass Stab"));
        A("Vocal FX and Ad-Libs", G("Vocal")); A("Impacts and Transitions", T("Impact", "Transition", "Drop FX", "Sweep", "Reverse")); A("Risers and Downlifters", T("Riser", "Downlifter")); A("Atmospheres and Textures", T("Atmosphere", "Texture"));
        A("MIDI Basslines", T("MIDI Bassline")); A("MIDI Melodies and Chords", T("MIDI Melody", "MIDI Chords")); A("Drum Fills", T("Drum Fill")); A("Songstarter Kits", G("Kits")); A("Purphex Signature Sounds", new SoundLibrary.Filter { Pack = "purphex-signature" });
    }
}
