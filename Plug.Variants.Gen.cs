using System;
using System.Collections.Generic;
using System.Globalization;

// ======================================================================
//  The EFFECT LIBRARY: over ten thousand ready-made versions of the real engines, made from grids of good settings
//  (every reverb type in every size, tone and character; every delay note value with every feedback and tone; Auto-Tune in every key and scale
//  at every speed; EQ curves, compressors, saturation, amps, modulation, robots, stutters, censors and the demon voices...).
//  They are built when the app starts (nothing to download) and their names never change, so a saved project always finds them again.
//  The hand-made plugins in the other files come first in the list; these follow in categories of their own ("... Library").
// ======================================================================
static partial class PluginTables
{
    const string GR = "Reverb Library", GD = "Delay & Echo Library", GT = "Auto-Tune Library", GP = "Pitch & Formant Library", GE = "EQ Library", GC = "Compressor & Dynamics Library",
        GS = "Saturation & Distortion Library", GA = "Amp & Cab Library", GL = "Lo-Fi & Bitcrush Library", GM = "Modulation Library", GB = "Telephone & Radio Library", GRB = "Robot & Vocoder Library",
        GG = "Stutter & Glitch Library", GV = "Convolution Space Library", GZ = "Resonator Library", GW = "Talkbox & Vowel Library", GN = "Granular Library", GX = "Demon & Monster Voices", GK = "Censor & Bleep";
    static void A(string cat, string name, string chain, string blurb) { A(cat, name, chain, "", blurb); }   // a chain: its settings are written inside it
    static string N(double v) { return v.ToString("0.##", CultureInfo.InvariantCulture); }
    static string Updn(int st) { return st < 0 ? "Down " + (-st) : "Up " + st; }
    static readonly string[] SyncNames = { "Free", "1/1", "1/2", "1/2 dotted", "1/4", "1/4 dotted", "1/4 triplet", "1/8", "1/8 dotted", "1/8 triplet", "1/16", "1/16 dotted", "1/16 triplet", "1/32" };

    // the hand-made censor and demon plugins (in the normal categories), then the library
    static void FillVoice2()
    {
        A(VT, "Censor (Bleep)", "censor", "mode=0", "Bleeps a word like TV. Right-click a section: Censor this section, or automate CENSOR.");
        A(VT, "Censor (Mute)", "censor", "mode=1", "Silences a word cleanly (radio edit). Use Censor this section on a split clip.");
        A(VT, "Censor (Reverse)", "censor", "mode=2", "Plays the word backwards, the classic clean-version trick.");
        A(VT, "Censor (Muffle)", "censor", "mode=3", "Muffles the word as if under water, so it can't be made out.");
        A(VT, "Censor (Scramble)", "censor", "mode=4", "Garbles the word: short reversed pieces with a wobble on top.");
        A(VT, "Censor (Radio Static)", "censor", "mode=5", "Covers the word with radio static that follows the voice.");
        A(VT, "Censor (Duck)", "censor", "mode=6", "Turns the word right down instead of removing it.");
        A(VT, "Clean Version Bleep", "censor", "mode=0;follow=1;tone=1000;level=-10", "A bleep that only sounds while the word does: tight, like a pro clean edit.");
        A(PV, "Croaky Demon Voice", "pshift{p1=-5;keep=0;fshift=-4;mix=100} > growl{rate=38;jitter=60;depth=85;sub=40;rasp=40;drive=14;dark=5500} > reverb2{mode=5;size=70;decay=3.5;damp=3500;hc=6500;mix=16}", "The pro demon: pitched and formant down, a croaking vocal fry, rasp, drive and a dark cathedral.");
        A(PV, "Satan Voice", "pshift{p1=-9;keep=0;fshift=-6;p2=-12;l2=60;mix=100} > growl{rate=32;jitter=55;depth=80;sub=60;rasp=35;drive=18;dark=4200} > reverb2{mode=5;size=85;decay=5;damp=3000;hc=5500;mix=22}", "Huge and evil: a deep shifted voice with an octave below, croak, grit and a cavern reverb.");
        A(PV, "Lucifer Layer", "pshift{p1=0;l1=75;p2=-12;l2=85;keep=0;fshift=-5;mix=100} > growl{rate=40;jitter=50;depth=60;sub=55;rasp=30;drive=12;dark=6000}", "Your real voice with a growling octave-down demon under it, how pros layer it on hooks.");
        A(PV, "Possessed Whisper", "pshift{p1=-3;keep=0;fshift=-3;mix=100} > growl{rate=55;jitter=80;depth=55;sub=0;rasp=85;drive=8;dark=7000} > reverb2{mode=6;size=75;decay=4;damp=4000;mix=25}", "A creepy breathing whisper with rasp and a cold, wide space.");
        A(PV, "Hell Lord", "pshift{p1=-12;keep=0;fshift=-8;mix=100} > growl{rate=28;jitter=50;depth=80;sub=70;rasp=30;drive=20;dark=3800} > reverb2{mode=5;size=95;decay=7;damp=2800;hc=5000;mix=24}", "An octave down with the formants dropped hard: the biggest, darkest demon in the list.");
        A(PV, "Underworld Growl", "pshift{p1=-7;keep=0;fshift=-5;mix=100} > growl{rate=34;jitter=65;depth=90;sub=45;rasp=50;drive=16;dark=5000}", "A dry, close, croaking growl with lots of fry and rasp.");
        A(PV, "Beast Mode Voice", "pshift{p1=-6;keep=0;fshift=-7;mix=100} > growl{rate=44;jitter=40;depth=70;sub=50;rasp=25;drive=22;dark=6500} > shaper{mode=1;drive=8;mix=50}", "A monster with a big chest: formants way down, tube grit and drive.");
        A(PV, "Exorcist Voice", "pshift{p1=-4;keep=0;fshift=-2;p2=-11;l2=55;mix=100} > growl{rate=50;jitter=85;depth=75;sub=30;rasp=60;drive=10;dark=5500} > reverb2{mode=0;size=40;decay=1.4;damp=4500;mix=18}", "Two broken voices at once with an uneven croak, in a small dark room.");
        A(PV, "Venom Voice", "pshift{p1=-8;keep=0;fshift=-4;mix=100} > growl{rate=36;jitter=45;depth=65;sub=40;rasp=45;drive=14;dark=7000} > widener{width=150;bass=120}", "Thick, slimy and wide: a villain voice for intros and skits.");
        A(PV, "Hellhound Snarl", "pshift{p1=-5;keep=0;fshift=-3;mix=100} > growl{rate=62;jitter=70;depth=70;sub=25;rasp=70;drive=24;dark=8000}", "A fast, snarling croak with heavy rasp and drive.");
        A(PV, "Abyss Choir", "pshift{p1=-12;l1=70;pan1=-50;p2=-7;l2=60;pan2=50;p3=-5;l3=50;keep=0;fshift=-4;mix=100} > growl{rate=40;jitter=40;depth=40;sub=40;rasp=20;drive=8;dark=5000} > reverb2{mode=5;size=90;decay=6;damp=3200;mix=30}", "A choir of demons a fifth, a fourth and an octave below you, in a cathedral.");
        A(PV, "Dark Lord Rap", "pshift{p1=-3;keep=0;fshift=-2;mix=100} > growl{rate=42;jitter=40;depth=35;sub=25;rasp=15;drive=6;dark=9000;mix=70}", "A subtle demon for rap verses: a bit lower and croakier, still clear to understand.");
        A(PV, "Vocal Fry / Croak", "growl", "rate=45;jitter=50;depth=70;sub=0;rasp=20;drive=6;dark=10000", "Just the croak: the uneven, creaky vocal fry pulses on any voice.");
        A(PV, "Monster Under the Bed", "pshift{p1=-10;keep=0;fshift=-9;mix=100} > growl{rate=25;jitter=60;depth=85;sub=55;rasp=40;drive=16;dark=3500} > reverb2{mode=0;size=20;decay=0.8;damp=3000;mix=14}", "Very low and close, like something breathing in the dark.");
    }

    static void FillLibrary()
    {
        PluginLibrary.Generating = true;
        try
        {
            GenReverb(); GenDelay(); GenTune(); GenMore(); GenPitch(); GenEq(); GenDynamics(); GenSaturation(); GenModulation(); GenTelephone(); GenRobot(); GenStutter(); GenSpace(); GenDemon(); GenCensor();
        }
        finally { PluginLibrary.Generating = false; }
    }

    // ---- reverbs: 7 types x 5 sizes x 5 tones x 6 characters x 3 amounts ----
    static void GenReverb()
    {
        string[] types = { "Room", "Hall", "Plate", "Chamber", "Spring", "Cathedral", "Ambient" }; double[] baseDecay = { 1.0, 2.6, 1.8, 1.6, 1.5, 4.5, 3.2 };
        string[] sizes = { "Tiny", "Small", "Medium", "Large", "Huge" }; double[] sz = { 15, 30, 50, 72, 92 }, dm = { 0.35, 0.6, 1, 1.6, 2.6 }, pre = { 5, 10, 20, 30, 45 };
        string[] tones = { "Dark", "Warm", "Natural", "Bright", "Airy" }; double[] damp = { 3500, 5500, 7500, 12000, 16000 }, hc = { 5500, 8000, 11000, 16000, 19000 }, lc = { 150, 120, 100, 180, 250 };
        string[] chars = { "Clean", "Ducked", "Gated", "Reverse", "Shimmer", "Modulated" };
        string[] charSet = { "", ";duck=60;duckr=350", ";gate=1;gthr=-30;ghold=180;grel=40", ";rev=1;rtime=900", ";shim=45;shimst=12", ";mod=70" };
        string[] charBlurb = { "", " It ducks while you sing and blooms in the gaps.", " Cut short by a gate, the 80s drum sound.", " Swells in backwards before each sound.", " With a shimmer an octave up.", " With a slow chorus-like movement in the tail." };
        string[] amts = { "Subtle", "Wet", "Send" }; double[] mix = { 14, 32, 100 };
        for (int t = 0; t < types.Length; t++) for (int s = 0; s < sizes.Length; s++) for (int o = 0; o < tones.Length; o++) for (int c = 0; c < chars.Length; c++) for (int a = 0; a < amts.Length; a++)
        {
            string name = types[t] + " Reverb: " + sizes[s] + ", " + tones[o] + ", " + chars[c] + ", " + amts[a];
            string p = "mode=" + t + ";size=" + N(sz[s]) + ";decay=" + N(Math.Min(30, baseDecay[t] * dm[s])) + ";pre=" + N(pre[s]) + ";damp=" + N(damp[o]) + ";hc=" + N(hc[o]) + ";lc=" + N(lc[o]) + charSet[c] + ";mix=" + N(mix[a]);
            string b = sizes[s] + " " + types[t].ToLowerInvariant() + ", " + tones[o].ToLowerInvariant() + " tone, " + (a == 2 ? "100% wet for a bus send." : a == 0 ? "a light touch." : "clearly wet.") + charBlurb[c];
            A(GR, name, "reverb2", p, b);
        }
    }

    // ---- delays: 5 modes x 12 note values x 3 feedbacks x 5 tones x 3 characters, throws and slapbacks ----
    static void GenDelay()
    {
        string[] modes = { "Mono", "Stereo", "Ping-pong", "Multi-tap", "Reverse" };
        string[] fbs = { "Short", "Medium", "Long" }; double[] fb = { 15, 40, 70 };
        string[] tones = { "Dark", "Tape", "Lo-Fi", "Clean", "Bright" }; string[] toneSet = { ";lc=150;hc=2500", ";lc=200;hc=4500;sat=30;wow=25", ";lc=400;hc=3000;sat=50;wow=50", ";lc=150;hc=8000", ";lc=400;hc=14000" };
        string[] chars = { "Plain", "Ducked", "Spacey" }; string[] charSet = { ";mix=28", ";duck=60;mix=38", ";rev=35;diff=40;mix=28" };
        for (int m = 0; m < modes.Length; m++) for (int sy = 1; sy <= 13; sy++) for (int f = 0; f < fbs.Length; f++) for (int t = 0; t < tones.Length; t++) for (int c = 0; c < chars.Length; c++)
        {
            string name = modes[m] + " Delay: " + SyncNames[sy] + ", " + fbs[f] + ", " + tones[t] + ", " + chars[c];
            string b = modes[m] + " echoes on " + SyncNames[sy] + " notes, " + fbs[f].ToLowerInvariant() + " tail, " + tones[t].ToLowerInvariant() + (c == 1 ? ", ducked under the vocal." : c == 2 ? ", melting into reverb." : ".");
            A(GD, name, "echo", "mode=" + m + ";sync=" + sy + ";fb=" + N(fb[f]) + toneSet[t] + charSet[c], b);
        }
        string[] tapeT = { "Tape", "Clean", "Dark" }; string[] tapeS = { ";sat=35;wow=30;hc=5000", ";hc=9000", ";hc=2800" };
        foreach (int ms in new[] { 40, 60, 80, 100, 120, 150, 180, 220 }) for (int t = 0; t < 3; t++)
            A(GD, "Slapback Echo: " + ms + " ms, " + tapeT[t], "echo", "mode=0;sync=0;time=" + ms + ";fb=8" + tapeS[t] + ";mix=26", "One quick " + ms + " ms repeat, the 50s and old-school rap slap, " + tapeT[t].ToLowerInvariant() + ".");
        string[] styles = { "Ping-pong", "Stereo", "Mono", "Multi-tap" }; string[] sends = { "All the time", "Last beat of each bar", "Second half of each bar", "Off-beats", "Last beat of every 2nd bar", "First beat of each bar" };
        foreach (int sy in new[] { 4, 5, 7, 8, 9, 10 }) for (int st = 0; st < styles.Length; st++) for (int se = 0; se < sends.Length; se++)
            A(GD, "Throw Echo: " + styles[st] + ", " + SyncNames[sy] + ", " + sends[se], "worldecho", "style=" + st + ";sync=" + sy + ";send=" + se, "Echo thrown only " + sends[se].ToLowerInvariant() + " (" + styles[st].ToLowerInvariant() + ", " + SyncNames[sy] + ").");
    }

    // ---- Auto-Tune in every key and scale at five speeds, and Melody Tune with every harmony ----
    static void GenTune()
    {
        // how fast it snaps x how much of your own slides it keeps x how hard it pulls x the voice it listens for; the key comes from your track (or set it in the plugin)
        string[] spN = { "Instant", "Robotic", "Snappy", "Fast", "Pop", "Smooth", "Natural", "Gentle" }; double[] sp = { 0, 3, 7, 12, 18, 30, 45, 70 };
        string[] ntN = { "Stiff", "Some Slides", "Loose" }; double[] nt = { 0, 25, 55 }; string[] stN = { "Full", "Partial" }; double[] stv = { 100, 75 };
        string[] rgN = { "Low Voice", "Standard Voice", "High Voice", "Wide Range" };
        string[] spB = { "snaps instantly: the hard T-Pain / Travis effect", "almost instant, robotic steps", "snappy modern rap tuning", "fast and tight", "polished pop speed", "smooth, soft corrections", "natural, you hardly hear it", "gentle, barely there" };
        for (int a = 0; a < spN.Length; a++) for (int b = 0; b < 3; b++) for (int c = 0; c < 2; c++) for (int d = 0; d < 4; d++)
            A(GT, "Auto-Tune: " + spN[a] + ", " + ntN[b] + ", " + stN[c] + ", " + rgN[d], "pitch", "speed=" + N(sp[a]) + ";natural=" + N(nt[b]) + ";strength=" + N(stv[c]) + ";range=" + d + ";mix=100", "Tuning that " + spB[a] + (b == 0 ? ", every note held straight" : b == 1 ? ", keeps some slides" : ", keeps your slides and vibrato") + (c == 1 ? ", pulled part of the way" : "") + ". Key from your track.");
        // Auto-Tune with something on top: the sounds people actually use
        string[] tsN = { "Hard", "Snappy", "Pop", "Natural" }; double[] ts = { 0, 7, 18, 45 };
        string[] xN = { "Doubled", "Octave Down Layer", "Octave Up Layer", "Formant Up", "Formant Down", "Chorused", "Telephone", "Distorted", "Wide", "Lo-Fi" };
        string[] xS = { " > pshift{p1=0;c1=12;l1=75;pan1=-75;d1=18;p2=0;c2=-11;l2=75;pan2=75;d2=28;keep=100;mix=60}", " > pshift{p1=0;l1=100;p2=-12;l2=55;keep=100;mix=100}", " > pshift{p1=0;l1=100;p2=12;l2=40;keep=100;mix=100}", " > formant{shift=4;tilt=2;mix=100}", " > formant{shift=-4;tilt=-1;mix=100}",
            " > chorus{rate=0.8;depth=2.5;delay=14;spread=100;mix=40}", " > band{hp=300;lp=3400;drive=8;bump=4}", " > shaper{mode=3;drive=14;tone=10000;auto=1;mix=100}", " > widener{width=170;bass=120}", " > crush{bits=10;rate=16000;mix=100}" };
        for (int a = 0; a < 4; a++) for (int x = 0; x < xN.Length; x++)
            A(GT, "Auto-Tune FX: " + tsN[a] + ", " + xN[x], "pitch{speed=" + N(ts[a]) + ";natural=" + N(ts[a] > 30 ? 55 : ts[a] > 0 ? 20 : 0) + ";strength=100;mix=100}" + xS[x], tsN[a] + " tuning, then " + xN[x].ToLowerInvariant() + ". Key from your track.");
        // Melody Tune: every harmony at four speeds, quiet or loud
        string[] harm = { "No harmony", "Third above", "Third below", "Fifth above", "Fifth below", "Octave above", "Octave below", "Power (fifth + octave)", "Choir (third, fifth, octave)", "Thick double" };
        string[] mS = { "Hard", "Snappy", "Pop", "Natural" }; double[] ms = { 0, 8, 18, 45 };
        for (int h = 0; h < harm.Length; h++) for (int a = 0; a < 4; a++) for (int lv = 0; lv < 2; lv++) { if (h == 0 && lv == 1) continue;
            A(GT, "Melody Tune: " + harm[h] + ", " + mS[a] + (h == 0 ? "" : lv == 0 ? ", Quiet harmony" : ", Loud harmony"), "melody", "speed=" + N(ms[a]) + ";strength=100;natural=" + N(ms[a] > 30 ? 55 : 25) + ";harm=" + h + ";hlevel=" + (lv == 0 ? 35 : 70) + ";mix=100", mS[a] + " tuning" + (h == 0 ? "." : " with a harmony: " + harm[h].ToLowerInvariant() + (lv == 0 ? ", tucked under." : ", loud.")) + " Key from your track."); }
    }

    // ---- versions that change the sound: filter types, amp tone stacks, vocoder bands, blended drive, shapes, densities, crackle ----
    static void GenMore()
    {
        string[] ft = { "Low-pass", "Band-pass", "High-pass", "Notch" }; string[] sh = { "Sine", "Triangle", "Saw Up", "Saw Down", "Square", "Random Steps", "Random Smooth" };
        for (int f = 1; f < 4; f++) for (int s2 = 0; s2 < sh.Length; s2++) foreach (int sy in new[] { 2, 4, 7, 9, 10, 13 })
            A(GM, "Auto-Filter: " + ft[f] + ", " + sh[s2] + ", " + SyncNames[sy], "mod", "src=0;tgt=2;ftype=" + f + ";shape=" + s2 + ";sync=" + sy + ";depth=80;res=6", "A " + ft[f].ToLowerInvariant() + " filter sweeping on " + SyncNames[sy] + " notes with a " + sh[s2].ToLowerInvariant() + " shape.");
        string[] pre = { "Clean", "Crunch", "Overdrive", "High Gain", "Fuzz", "Bass Tube" }; string[] cab = { "Direct", "1x12 Combo", "2x12 Open Back", "4x12 Stack", "Bass 8x10", "Bass 1x15", "Small Radio", "Megaphone Horn" };
        string[] tsk = { "Scooped", "Mid Push", "Dark" }; string[] tss = { ";bass=5;mid=-6;treble=5", ";bass=-2;mid=6;treble=1", ";bass=3;mid=0;treble=-7" };
        for (int p = 0; p < pre.Length; p++) for (int c = 0; c < cab.Length; c++) for (int t = 0; t < 3; t++)
            A(GA, "Amp: " + pre[p] + " into " + cab[c] + ", " + tsk[t], "amp", "pre=" + p + ";cab=" + c + ";gain=22" + tss[t] + ";level=-6", "A " + pre[p].ToLowerInvariant() + " amp with a " + tsk[t].ToLowerInvariant() + " tone stack, through a " + cab[c].ToLowerInvariant() + ".");
        string[] car = { "Saw", "Pulse", "Whisper", "Saw + Noise" }; string[] ch = { "Single note", "Octave", "Octave + fifth", "Major chord", "Minor chord", "Major 7th" };
        for (int c = 0; c < 4; c++) foreach (int b in new[] { 0, 4 }) foreach (int nt in new[] { 36, 48, 60 }) for (int h = 0; h < ch.Length; h++)
            A(GRB, "Vocoder: " + car[c] + ", " + (nt == 36 ? "Low" : nt == 48 ? "Mid" : "High") + ", " + ch[h] + (b == 0 ? ", 8 bands (rough)" : ", 32 bands (clear)"), "vocoder", "carrier=" + c + ";note=" + nt + ";chord=" + h + ";bands=" + b + ";mix=100", "Talking synth with " + (b == 0 ? "8 bands: rough and robotic." : "32 bands: clear and wordy."));
        string[] modes = { "Soft Clip", "Tube", "Tape", "Overdrive", "Hard Clip", "Cubic Clipper", "Fuzz", "Wavefolder", "Sine Shaper", "Harmonics", "Octave Rectifier", "Diode" }; string[] dn = { "Touch", "Warm", "Crunchy", "Hot", "Destroyed" }; double[] dr = { 3, 8, 14, 22, 32 }; string[] tn = { "Dark", "Even", "Bright" }; double[] tf = { 4000, 12000, 20000 };
        for (int m = 0; m < modes.Length; m++) for (int d = 0; d < 5; d++) for (int t = 0; t < 3; t++)
            A(GS, modes[m] + " Saturation: " + dn[d] + ", " + tn[t] + ", Blended", "shaper", "mode=" + m + ";drive=" + N(dr[d]) + ";tone=" + N(tf[t]) + ";auto=1;mix=45", modes[m] + " drive blended under the clean sound (parallel), " + tn[t].ToLowerInvariant() + " top.");
        string[] stg = { "4", "6", "8", "12", "24" };
        for (int s3 = 0; s3 < 5; s3++) foreach (double r in new[] { 0.1, 0.35, 1, 3 }) foreach (int fb in new[] { 0, 45, 85 })
            A(GM, "Phaser: " + stg[s3] + " stages, " + N(r) + " Hz, Feedback " + fb + ", Triangle", "phaser", "stages=" + s3 + ";rate=" + N(r) + ";fb=" + fb + ";shape=1;mix=50", "A " + stg[s3] + "-stage phaser with a triangle sweep (more even than a sine).");
        foreach (double r in new[] { 0.1, 0.3, 0.8, 2 }) foreach (int fb in new[] { -60, 40, 85 }) for (int b = 0; b < 3; b++)
            A(GM, "Flanger: " + N(r) + " Hz, Feedback " + (fb < 0 ? "minus " + (-fb) : fb.ToString()) + ", " + new[] { "Classic", "Rising", "Falling" }[b] + ", Sine", "flanger", "rate=" + N(r) + ";fb=" + fb + ";barber=" + b + ";shape=0;mix=50", "A flanger with a smooth sine sweep at " + N(r) + " Hz.");
        foreach (int sz in new[] { 40, 120, 300, 600 }) foreach (int p in new[] { -12, 0, 7, 12 }) foreach (int rv in new[] { 0, 100 }) foreach (int dn2 in new[] { 6, 60 })
            A(GN, "Granular Cloud: " + sz + " ms grains, " + (p == 0 ? "Same pitch" : Updn(p)) + (rv > 0 ? ", Reversed" : "") + (dn2 < 20 ? ", Sparse" : ", Dense"), "grain", "size=" + sz + ";pitch=" + p + ";rev=" + rv + ";dens=" + dn2 + ";dry=30;mix=100", (dn2 < 20 ? "Sparse, scattered" : "Dense, smeared") + " grains of " + sz + " ms.");
        foreach (int hp in new[] { 200, 300, 500, 800 }) foreach (int lp in new[] { 2500, 3400, 5000, 8000 }) foreach (int d in new[] { 0, 12, 24 })
            A(GB, "Band Filter: " + hp + " to " + lp + " Hz" + (d > 0 ? (d > 12 ? ", Overdriven" : ", Driven") : ", Clean") + ", Crackle & Flutter", "band", "hp=" + hp + ";lp=" + lp + ";drive=" + d + ";crackle=40;flutter=40;bump=4", "An old worn speaker from " + hp + " to " + lp + " Hz with crackle and flutter.");
    }

    // ---- pitch, formant, harmonies and doubles ----
    static void GenPitch()
    {
        string[] fm = { "Natural", "Moving formants", "Layered" }; string[] fs = { ";keep=100;mix=100", ";keep=0;mix=100", ";keep=100;mix=50" };
        for (int st = -24; st <= 24; st++) { if (st == 0) continue; for (int f = 0; f < 3; f++) A(GP, "Pitch Shift: " + Updn(st) + " semitones, " + fm[f], "pshift", "p1=" + st + ";l1=100" + fs[f], (st < 0 ? "Lower by " : "Higher by ") + Math.Abs(st) + " semitones" + (f == 0 ? ", same voice character." : f == 1 ? ", the voice character moves too." : ", blended with your dry voice."), HV1 + ",d1,pan1"); }
        string[] tilt = { "Dark", "Even", "Bright" };
        for (int s = -12; s <= 12; s++) { if (s == 0) continue; for (int t = 0; t < 3; t++) A(GP, "Formant Shift: " + Updn(s) + ", " + tilt[t], "formant", "shift=" + s + ";tilt=" + (t - 1) * 6 + ";mix=100", (s < 0 ? "A bigger, deeper voice" : "A smaller, younger voice") + " at the same pitch, " + tilt[t].ToLowerInvariant() + " tone."); }
        int[][] iv = { new[] { 3, 7 }, new[] { 4, 7 }, new[] { -5, 7 }, new[] { -12, 12 }, new[] { 5, 12 }, new[] { 3, 10 }, new[] { 4, 11 }, new[] { 7, 12 }, new[] { -3, -8 }, new[] { -4, -9 }, new[] { 2, 5 }, new[] { -7, 5 }, new[] { 12, 19 }, new[] { -12, -5 }, new[] { 3, -9 }, new[] { 4, -8 } };
        string[] spr = { "Tight", "Wide", "Stacked" }; int[] pan = { 30, 80, 60 }; int[] dly = { 0, 6, 22 };
        foreach (var v in iv) for (int s = 0; s < 3; s++)
            A(GP, "Harmonizer: " + Updn(v[0]) + " & " + Updn(v[1]) + ", " + spr[s], "pshift", "p1=" + v[0] + ";l1=55;pan1=-" + pan[s] + ";d1=" + dly[s] + ";p2=" + v[1] + ";l2=50;pan2=" + pan[s] + ";d2=" + (dly[s] * 2) + ";keep=100;mix=52", "Two harmony voices (" + Updn(v[0]).ToLowerInvariant() + " and " + Updn(v[1]).ToLowerInvariant() + " semitones), " + spr[s].ToLowerInvariant() + ".", HV2);
        foreach (int c in new[] { 6, 10, 16, 24 }) foreach (int d in new[] { 10, 18, 28, 40 }) foreach (int wide in new[] { 0, 1 })
            A(GP, "Vocal Doubler: " + c + " cents, " + d + " ms, " + (wide == 1 ? "Wide" : "Close"), "pshift", "p1=0;c1=" + c + ";l1=75;pan1=-" + (wide == 1 ? 85 : 35) + ";d1=" + d + ";p2=0;c2=-" + c + ";l2=75;pan2=" + (wide == 1 ? 85 : 35) + ";d2=" + (d + 9) + ";mix=60", "A double-tracked vocal: two copies " + c + " cents apart, " + d + " ms late, " + (wide == 1 ? "wide." : "close."), HV2);
    }

    // ---- EQ: 30 curves x 3 strengths, vocal EQ grid, tilts ----
    static void GenEq()
    {
        string[] cn = { "Smile", "Air Lift", "Presence", "Mud Cut", "Boxy Cut", "Warmth", "Bass Boost", "Sub Cut", "Vocal Clarity", "Radio Ready", "Dark", "Bright", "Scoop", "Mid Push", "Nasal Cut", "Harsh Cut", "Body", "Low Shelf Lift", "High Shelf Cut", "Loudness", "Podcast", "De-Mud & Air", "Kick Punch", "808 Weight", "Snare Crack", "Hi-Hat Sparkle", "Guitar Bite", "Piano Clear", "Strings Silk", "Phone Thin" };
        int[][] cv = {
            new[] { 4, 3, 1, 0, -1, -1, 0, 2, 3, 4 }, new[] { 0, 0, 0, 0, 0, 0, 1, 2, 4, 6 }, new[] { 0, 0, 0, 0, 0, 1, 3, 4, 1, 0 }, new[] { 0, 0, -1, -4, -2, 0, 0, 0, 0, 0 }, new[] { 0, 0, 0, -1, -4, -2, 0, 0, 0, 0 },
            new[] { 1, 2, 3, 2, 0, 0, 0, -1, -1, -2 }, new[] { 5, 5, 3, 1, 0, 0, 0, 0, 0, 0 }, new[] { -12, -6, -1, 0, 0, 0, 0, 0, 0, 0 }, new[] { -6, -3, -1, -2, -1, 1, 2, 3, 3, 2 }, new[] { -8, -4, 0, -1, 0, 2, 3, 3, 2, 1 },
            new[] { 1, 1, 1, 0, 0, 0, -2, -4, -6, -8 }, new[] { 0, 0, 0, 0, 0, 1, 2, 3, 4, 5 }, new[] { 3, 2, 0, -2, -4, -3, -1, 1, 2, 3 }, new[] { -1, -1, 0, 1, 3, 4, 3, 1, 0, -1 }, new[] { 0, 0, 0, 0, -1, -4, -1, 0, 0, 0 },
            new[] { 0, 0, 0, 0, 0, 0, -1, -4, -2, 0 }, new[] { 0, 1, 3, 3, 1, 0, 0, 0, 0, 0 }, new[] { 3, 3, 2, 1, 0, 0, 0, 0, 0, 0 }, new[] { 0, 0, 0, 0, 0, 0, 0, -1, -3, -5 }, new[] { 4, 3, 1, 0, -1, -1, 0, 1, 3, 4 },
            new[] { -12, -5, 0, 1, 0, 0, 2, 2, 0, -2 }, new[] { -4, -2, -1, -3, -1, 0, 1, 2, 3, 5 }, new[] { 2, 4, 1, -2, -2, 0, 2, 3, 0, 0 }, new[] { 6, 4, 0, -2, -1, 0, 0, 0, 0, 0 }, new[] { 0, 0, 2, 3, 0, -1, 2, 4, 2, 0 },
            new[] { -6, -4, -2, 0, 0, 0, 0, 1, 4, 5 }, new[] { -4, -2, 0, -1, 0, 2, 4, 2, 0, -1 }, new[] { -2, -1, 0, -1, -1, 0, 1, 2, 2, 1 }, new[] { 0, 0, 1, 0, -1, -1, 0, -1, 2, 3 }, new[] { -15, -12, -8, -2, 2, 4, 2, -4, -10, -15 } };
        string[] st = { "Subtle", "Gentle", "Medium", "Strong", "Extreme" }; double[] k = { 0.3, 0.5, 1, 1.6, 2.2 };
        for (int c = 0; c < cn.Length; c++) for (int s = 0; s < 5; s++)
        {
            var sb = new System.Text.StringBuilder(); for (int b = 0; b < 10; b++) sb.Append("g" + b + "=" + N(Math.Max(-15, Math.Min(15, Math.Round(cv[c][b] * k[s] * 2) / 2))) + ";"); sb.Append("out=0");
            A(GE, "EQ Curve: " + cn[c] + ", " + st[s], "geq", sb.ToString(), "A " + st[s].ToLowerInvariant() + " " + cn[c].ToLowerInvariant() + " curve on a 10-band graphic EQ (every band editable).");
        }
        foreach (int hp in new[] { 60, 80, 100, 120, 150, 200 }) foreach (int pr in new[] { 0, 2, 4 }) foreach (int air in new[] { 0, 3, 6 }) foreach (int mud in new[] { 0, -3 })
            A(GE, "Vocal EQ: High-pass " + hp + " Hz, Presence " + pr + ", Air " + air + (mud < 0 ? ", Mud cut" : ""), "eq", "hp_on=1;hp_f=" + hp + ";b2_f=350;b2_g=" + mud + ";b2_q=1;b3_f=3500;b3_g=" + pr + ";b3_q=0.8;b5_f=12000;b5_g=" + air, "Low rumble off below " + hp + " Hz" + (pr > 0 ? ", +" + pr + " dB presence" : "") + (air > 0 ? ", +" + air + " dB air" : "") + (mud < 0 ? ", mud cut at 350 Hz." : "."));
        foreach (double t in new[] { -9, -7.5, -6, -4.5, -3, -1.5, 1.5, 3, 4.5, 6, 7.5, 9 }) foreach (int pv in new[] { 250, 500, 800, 1500, 3000 })
            A(GE, "Tilt EQ: " + (t < 0 ? "Darker " + N(-t) : "Brighter " + N(t)) + " dB at " + pv + " Hz", "tilt", "tilt=" + N(t) + ";pivot=" + pv, "One knob tone: " + (t < 0 ? "darker" : "brighter") + " around " + pv + " Hz.");
    }

    // ---- compressors, OTT, gates, de-essers, limiters, transients ----
    static void GenDynamics()
    {
        double[] ratio = { 1.5, 2, 3, 4, 6, 8, 12, 20 }; string[] an = { "Fast attack", "Quick attack", "Medium attack", "Slow attack" }; double[] att = { 0.3, 3, 10, 30 };
        string[] rn = { "Fast release", "Medium release", "Slow release" }; double[] rel = { 40, 120, 300 }; string[] amt = { "Light", "Medium", "Heavy" }; double[] thr = { -12, -20, -30 }, mk = { 1, 3, 6 };
        foreach (double ra in ratio) for (int a = 0; a < 4; a++) for (int r = 0; r < 3; r++) for (int m = 0; m < 3; m++)
            A(GC, "Compressor: " + N(ra) + " to 1, " + an[a] + ", " + rn[r] + ", " + amt[m], "comp", "thr=" + N(thr[m]) + ";ratio=" + N(ra) + ";att=" + N(att[a]) + ";rel=" + N(rel[r]) + ";knee=6;makeup=" + N(mk[m]), amt[m] + " " + N(ra) + ":1 compression, " + an[a].ToLowerInvariant() + ", " + rn[r].ToLowerInvariant() + ".");
        foreach (double ra in ratio) for (int m = 0; m < 3; m++)
            A(GC, "Parallel Compressor: " + N(ra) + " to 1, " + amt[m], "comp", "thr=" + N(thr[m] - 8) + ";ratio=" + N(ra) + ";att=2;rel=90;knee=4;makeup=" + N(mk[m] + 4) + ";mix=40", "New York style: squashed hard and blended under the dry sound.");
        string[] tl = { "Even", "Bright", "Dark", "Bass" }; string[] ts = { "", ";highg=3", ";highg=-3;lowg=2", ";lowg=4" };
        foreach (int d in new[] { 20, 40, 60, 80, 100 }) foreach (int tm in new[] { 30, 100, 250 }) for (int t = 0; t < 4; t++)
            A(GC, "OTT Squash: " + d + "%, " + (tm == 30 ? "Fast" : tm == 100 ? "Medium" : "Slow") + ", " + tl[t], "ott", "depth=" + d + ";time=" + tm + ts[t], "Upward and downward multiband squash at " + d + "% depth, " + tl[t].ToLowerInvariant() + ".");
        foreach (int th in new[] { -60, -50, -40, -32, -24 }) foreach (int r in new[] { 30, 100, 300 })
            A(GC, "Noise Gate: " + th + " dB, " + (r == 30 ? "Fast" : r == 100 ? "Medium" : "Slow") + " close", "gate", "thr=" + th + ";range=60;att=1;hold=40;rel=" + r, "Silences anything quieter than " + th + " dB between phrases.");
        foreach (int f in new[] { 4500, 5500, 6500, 7500, 9000 }) foreach (int rg in new[] { 4, 8, 12, 18 })
            A(GC, "De-esser: " + N(f / 1000.0) + " kHz, " + rg + " dB", "deess", "freq=" + f + ";thr=-30;range=" + rg, "Tames harsh S sounds around " + N(f / 1000.0) + " kHz, up to " + rg + " dB.");
        foreach (double c in new[] { -0.3, -1, -2 }) foreach (int gn in new[] { 0, 3, 6, 9 })
            A(GC, "Limiter: Ceiling " + N(-c) + " below, +" + gn + " dB", "limiter", "ceil=" + N(c) + ";gain=" + gn + ";rel=80;tp=1", "A brick wall at " + N(c) + " dB, pushed " + gn + " dB louder.");
        foreach (int a in new[] { -10, -5, 0, 5, 10 }) foreach (int s in new[] { -10, -5, 0, 5, 10 }) { if (a == 0 && s == 0) continue; A(GC, "Transient Shaper: Attack " + (a < 0 ? "less " + (-a) : "more " + a) + ", Sustain " + (s < 0 ? "less " + (-s) : "more " + s), "transient", "attack=" + a + ";sustain=" + s, (a > 0 ? "Punchier hits" : a < 0 ? "Softer hits" : "Same hits") + (s > 0 ? ", longer ring." : s < 0 ? ", tighter tails." : ".")); }
    }

    // ---- saturation, distortion, exciters, amps, lo-fi ----
    static void GenSaturation()
    {
        string[] modes = { "Soft Clip", "Tube", "Tape", "Overdrive", "Hard Clip", "Cubic Clipper", "Fuzz", "Wavefolder", "Sine Shaper", "Harmonics", "Octave Rectifier", "Diode" };
        string[] dn = { "Touch", "Warm", "Crunchy", "Hot", "Destroyed" }; double[] dr = { 3, 8, 14, 22, 32 }; string[] tn = { "Dark", "Even", "Bright" }; double[] tf = { 4000, 12000, 20000 };
        for (int m = 0; m < modes.Length; m++) for (int d = 0; d < 5; d++) for (int t = 0; t < 3; t++)
            A(GS, modes[m] + " Saturation: " + dn[d] + ", " + tn[t], "shaper", "mode=" + m + ";drive=" + N(dr[d]) + ";tone=" + N(tf[t]) + ";auto=1", modes[m] + " drive (" + N(dr[d]) + " dB), " + tn[t].ToLowerInvariant() + " top, level kept even.");
        string[] sm = { "Tape", "Tube", "Soft Clip" };
        for (int m = 0; m < 3; m++) foreach (int d in new[] { 2, 4, 7, 10, 14, 18 }) foreach (int mx in new[] { 50, 100 })
            A(GS, "Analog Saturation: " + sm[m] + ", Drive " + d + ", " + (mx == 100 ? "Full" : "Blended"), "sat", "mode=" + m + ";drive=" + d + ";tone=0;mix=" + mx, sm[m] + " warmth at drive " + d + (mx < 100 ? ", blended with the clean sound." : "."));
        foreach (int d in new[] { 4, 8, 12 }) foreach (int r in new[] { 0, 1, 2 }) foreach (int mx in new[] { 15, 30, 50 })
            A(GS, "Exciter: " + (r == 0 ? "Presence" : r == 1 ? "Top" : "Air") + ", Drive " + d + ", " + mx + "%", "exciter", "drive=" + d + ";harm=50;flo=" + (r == 0 ? 2500 : r == 1 ? 4500 : 8000) + ";fhi=" + (r == 0 ? 8000 : 16000) + ";mix=" + mx, "Adds shine with new harmonics in the " + (r == 0 ? "presence range." : r == 1 ? "top end." : "air band."));
        string[] pre = { "Clean", "Crunch", "Overdrive", "High Gain", "Fuzz", "Bass Tube" }; string[] cab = { "Direct", "1x12 Combo", "2x12 Open Back", "4x12 Stack", "Bass 8x10", "Bass 1x15", "Small Radio", "Megaphone Horn" }; double[] gn = { 10, 22, 34 }; string[] gnn = { "Low gain", "Mid gain", "Cranked" };
        for (int p = 0; p < pre.Length; p++) for (int c = 0; c < cab.Length; c++) for (int g = 0; g < 3; g++)
            A(GA, "Amp: " + pre[p] + " into " + cab[c] + ", " + gnn[g], "amp", "pre=" + p + ";cab=" + c + ";gain=" + N(gn[g]) + ";level=-6", "A " + pre[p].ToLowerInvariant() + " amp, " + gnn[g].ToLowerInvariant() + ", through a " + cab[c].ToLowerInvariant() + " speaker.");
        foreach (int b in new[] { 2, 4, 6, 8, 10, 12 }) foreach (int r in new[] { 4000, 8000, 11025, 16000, 22050 }) foreach (int mx in new[] { 50, 100 })
            A(GL, "Bitcrusher: " + b + "-bit, " + N(r / 1000.0) + " kHz, " + (mx == 100 ? "Full" : "Blended"), "crush", "bits=" + b + ";rate=" + r + ";mix=" + mx, b + "-bit at " + N(r / 1000.0) + " kHz: retro game and lo-fi grit" + (mx < 100 ? ", blended." : "."));
    }

    // ---- chorus, phaser, flanger, tremolo / auto-pan / auto-filter / vibrato, width, 8D ----
    static void GenModulation()
    {
        foreach (double r in new[] { 0.3, 0.8, 1.5, 3 }) foreach (double d in new[] { 1, 2.5, 5 }) foreach (int mx in new[] { 20, 40, 60 }) foreach (int dl in new[] { 8, 20 })
            A(GM, "Chorus: Rate " + N(r) + " Hz, Depth " + N(d) + ", " + mx + "%" + (dl > 10 ? ", Lush" : ""), "chorus", "rate=" + N(r) + ";depth=" + N(d) + ";delay=" + dl + ";spread=90;mix=" + mx, "Stereo chorus, " + (r < 1 ? "slow" : "quick") + " and " + (d > 2 ? "deep" : "light") + ".");
        string[] stg = { "4", "6", "8", "12", "24" };
        for (int s = 0; s < 5; s++) foreach (double r in new[] { 0.1, 0.35, 1, 3 }) foreach (int fb in new[] { 0, 45, 85 })
            A(GM, "Phaser: " + stg[s] + " stages, " + N(r) + " Hz, Feedback " + fb, "phaser", "stages=" + s + ";rate=" + N(r) + ";fb=" + fb + ";mix=50", "A " + stg[s] + "-stage phaser sweeping at " + N(r) + " Hz" + (fb > 60 ? ", strongly resonant." : "."));
        string[] bb = { "Classic", "Rising", "Falling" };
        foreach (double r in new[] { 0.1, 0.3, 0.8, 2 }) foreach (int fb in new[] { -60, 40, 85 }) for (int b = 0; b < 3; b++)
            A(GM, "Flanger: " + N(r) + " Hz, Feedback " + (fb < 0 ? "minus " + (-fb) : fb.ToString()) + ", " + bb[b], "flanger", "rate=" + N(r) + ";fb=" + fb + ";barber=" + b + ";mix=50", (b == 0 ? "A jet-plane flanger" : "An endless " + bb[b].ToLowerInvariant() + " barber-pole flanger") + " at " + N(r) + " Hz.");
        string[] tg = { "Tremolo", "Auto-Pan", "Auto-Filter", "Auto-Drive", "Vibrato", "Width Wobble" }; string[] sh = { "Sine", "Triangle", "Saw Up", "Saw Down", "Square", "Random Steps", "Random Smooth" };
        int[] syncs = { 2, 4, 7, 9, 10, 13 };
        for (int t = 0; t < tg.Length; t++) for (int s = 0; s < sh.Length; s++) foreach (int sy in syncs)
            A(GM, tg[t] + ": " + sh[s] + ", " + SyncNames[sy], "mod", "src=0;tgt=" + t + ";shape=" + s + ";sync=" + sy + ";depth=" + (t == 4 ? 25 : 80), tg[t] + " moving in time on " + SyncNames[sy] + " notes with a " + sh[s].ToLowerInvariant() + " shape.");
        foreach (int w in new[] { 50, 80, 120, 140, 170, 200 }) A(GM, "Stereo Width: " + w + "%", "widener", "width=" + w + ";bass=120", w < 100 ? "Narrower, closer to mono (the bass stays centred)." : "Wider stereo (the bass stays centred).");
        foreach (int p in new[] { 4, 8, 12, 20 }) foreach (int mx in new[] { 60, 100 }) A(GM, "8D Orbit: " + p + " s per turn, " + (mx == 100 ? "Full" : "Blended"), "8d", "period=" + p + ";mix=" + mx, "The sound circles your head every " + p + " seconds (best on headphones).");
    }

    // ---- telephone, radio, megaphone band filters ----
    static void GenTelephone()
    {
        foreach (int hp in new[] { 200, 300, 500, 800 }) foreach (int lp in new[] { 2500, 3400, 5000, 8000 }) foreach (int d in new[] { 0, 12, 24 }) foreach (int nz in new[] { 0, 30 })
            A(GB, "Band Filter: " + hp + " to " + lp + " Hz" + (d > 0 ? (d > 12 ? ", Overdriven" : ", Driven") : ", Clean") + (nz > 0 ? ", Noisy" : ""), "band", "hp=" + hp + ";lp=" + lp + ";drive=" + d + ";noise=" + nz + ";bump=4", (lp <= 3400 ? "Telephone" : lp <= 5000 ? "Old radio" : "Thin speaker") + " band from " + hp + " to " + lp + " Hz" + (d > 0 ? ", driven" : "") + (nz > 0 ? " with line noise." : "."));
    }

    // ---- ring modulators, vocoders, frequency shifters ----
    static void GenRobot()
    {
        string[] wv = { "Sine", "Square", "Saw", "Triangle" };
        for (int m = 0; m < 2; m++) for (int w = 0; w < 4; w++) foreach (int f in new[] { 30, 60, 110, 220, 440, 880, 1500 })
            A(GRB, (m == 0 ? "Ring Mod: " : "Tremolo Mod: ") + wv[w] + ", " + f + " Hz", "ring", "mode=" + m + ";wave=" + w + ";freq=" + f + ";depth=100;mix=" + (m == 0 ? 70 : 100), m == 0 ? "Metallic robot tones from a " + f + " Hz " + wv[w].ToLowerInvariant() + " carrier." : "Fast " + wv[w].ToLowerInvariant() + " volume chop at " + f + " Hz.");
        string[] car = { "Saw", "Pulse", "Whisper", "Saw + Noise" }; string[] ch = { "Single note", "Octave", "Octave + fifth", "Major chord", "Minor chord", "Major 7th" };
        for (int c = 0; c < 4; c++) foreach (int nt in new[] { 36, 48, 60 }) for (int h = 0; h < ch.Length; h++)
            A(GRB, "Vocoder: " + car[c] + ", " + (nt == 36 ? "Low" : nt == 48 ? "Mid" : "High") + ", " + ch[h], "vocoder", "carrier=" + c + ";note=" + nt + ";chord=" + h + ";mix=100", "Talking synth: your words played on a " + car[c].ToLowerInvariant() + " " + ch[h].ToLowerInvariant() + ".");
        foreach (int s in new[] { -400, -100, -30, 30, 100, 400 }) foreach (int fb in new[] { 0, 50 })
            A(GRB, "Frequency Shift: " + (s < 0 ? "Down " + (-s) : "Up " + s) + " Hz" + (fb > 0 ? ", Spiral" : ""), "fshift", "shift=" + s + ";fb=" + fb + ";mix=100", "Every frequency moved " + Math.Abs(s) + " Hz " + (s < 0 ? "down" : "up") + ": metallic, inharmonic" + (fb > 0 ? ", spiralling." : "."));
    }

    // ---- beat repeats, chops, tape stops ----
    static void GenStutter()
    {
        string[] md = { "Always", "Random bars", "Last beat", "End of 4 bars" };
        foreach (int sy in new[] { 4, 5, 7, 8, 10, 12, 13 }) for (int m = 0; m < 4; m++) foreach (int dk in new[] { 0, 60 })
            A(GG, "Stutter Repeat: " + SyncNames[sy] + ", " + md[m] + (dk > 0 ? ", Dark" : ""), "repeat", "sync=" + sy + ";mode=" + m + ";dark=" + dk + ";chance=50", "Repeats " + SyncNames[sy] + " slices " + (m == 0 ? "all the time" : m == 1 ? "on random bars" : m == 2 ? "on the last beat of each bar" : "at the end of every 4 bars") + (dk > 0 ? ", getting darker." : "."));
        string[] cs = { "Gentle", "Busy", "Wild" }; string[] cset = { ";pmute=10;prep=15;prev=5;ppit=5", ";pmute=20;prep=30;prev=15;ppit=15", ";pmute=30;prep=45;prev=30;ppit=35" };
        foreach (int sy in new[] { 4, 5, 7, 8, 10, 12, 13 }) for (int c = 0; c < 3; c++)
            A(GG, "Glitch Chop: " + SyncNames[sy] + ", " + cs[c], "chop", "sync=" + sy + cset[c], cs[c] + " glitch edits on " + SyncNames[sy] + " notes: mutes, repeats, reverses and pitch jumps.");
        foreach (int st in new[] { 250, 500, 900, 1600 }) foreach (int sp in new[] { 150, 400, 900 })
            A(GG, "Tape Stop: " + st + " ms stop, " + sp + " ms start", "tapestop", "auto=1;every=8;stop=" + st + ";start=" + sp, "The tape slows to a stop over " + st + " ms every 8 beats, then spins back up.");
    }

    // ---- convolution spaces, resonators, vowels, grains ----
    static void GenSpace()
    {
        string[] ir = { "Small Room", "Live Room", "Large Hall", "Cathedral", "Plate", "Spring", "Tiled Bathroom", "Metal Tank", "Telephone Booth", "Guitar Cabinet", "Small Speaker", "Cardboard Tube" };
        for (int i = 0; i < ir.Length; i++) foreach (int ln in new[] { 30, 60, 100 }) foreach (int mx in new[] { 15, 30, 60 })
            A(GV, "Real Space: " + ir[i] + ", " + (ln == 30 ? "Short" : ln == 60 ? "Medium" : "Full") + ", " + mx + "%", "conv", "ir=" + i + ";len=" + ln + ";mix=" + mx, "A recorded " + ir[i].ToLowerInvariant() + " (convolution), " + (ln == 100 ? "full length" : ln + "% length") + ", " + mx + "% wet.");
        string[] notes = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" }; string[] ch = { "Single note", "Octave", "Fifth", "Octave + fifth", "Major chord", "Minor chord", "Sus4", "Major 7th" };
        foreach (int fb in new[] { 50, 80, 95 }) foreach (int dm in new[] { 3000, 12000 }) for (int c = 0; c < ch.Length; c++)
            A(GZ, "Resonator: " + ch[c] + ", " + (fb == 50 ? "Short ring" : fb == 80 ? "Ringing" : "Long ring") + ", " + (dm < 5000 ? "Dark" : "Bright"), "comb", "mode=0;freq=130.81;snap=1;chord=" + c + ";fb=" + fb + ";damp=" + dm + ";mix=45", "Makes any sound ring as a " + ch[c].ToLowerInvariant() + " (set the note with Frequency): drums and noise turn musical.");
        string[] vw = { "A", "E", "I", "O", "U" }; string[] sh = { "Sine", "Triangle", "Saw Up", "Saw Down", "Random" };
        for (int v = 0; v < 5; v++) { A(GW, "Talkbox Vowel: " + vw[v] + ", Still", "vowel", "vowel=" + v + ";shape=0;mix=100", "The sound says '" + vw[v] + "' like a talkbox."); for (int s = 0; s < 5; s++) foreach (double r in new[] { 0.3, 1, 4 }) A(GW, "Talkbox Vowel: " + vw[v] + ", " + sh[s] + ", " + N(r) + " Hz", "vowel", "vowel=" + v + ";shape=" + (s + 1) + ";rate=" + N(r) + ";depth=2;mix=100", "Talkbox vowels moving from '" + vw[v] + "' with a " + sh[s].ToLowerInvariant() + " at " + N(r) + " Hz."); }
        foreach (int sz in new[] { 40, 120, 300, 600 }) foreach (int p in new[] { -12, 0, 7, 12 }) foreach (int rv in new[] { 0, 100 })
            A(GN, "Granular Cloud: " + sz + " ms grains, " + (p == 0 ? "Same pitch" : Updn(p)) + (rv > 0 ? ", Reversed" : ""), "grain", "size=" + sz + ";pitch=" + p + ";rev=" + rv + ";dry=30;mix=100", "Clouds of " + sz + " ms grains" + (p != 0 ? ", shifted " + Updn(p).ToLowerInvariant() : "") + (rv > 0 ? ", playing backwards." : "."));
    }

    // ---- demon voices: 9 depths x 4 growls x 4 spaces ----
    static void GenDemon()
    {
        string[] gn = { "Smooth", "Croaky", "Raspy", "Brutal" };
        string[] gs = { "rate=45;jitter=30;depth=35;sub=35;rasp=10;drive=6;dark=7000", "rate=38;jitter=60;depth=85;sub=40;rasp=35;drive=12;dark=5500", "rate=55;jitter=75;depth=60;sub=25;rasp=80;drive=14;dark=6500", "rate=30;jitter=55;depth=90;sub=70;rasp=50;drive=24;dark=4000" };
        string[] sp = { "Dry", "Room", "Cathedral", "Hell" };
        string[] ss = { "", " > reverb2{mode=0;size=30;decay=1;damp=4000;mix=14}", " > reverb2{mode=5;size=85;decay=5;damp=3200;hc=6000;mix=22}", " > reverb2{mode=5;size=95;decay=8;damp=2600;hc=5000;shim=30;shimst=-12;mix=28}" };
        foreach (int st in new[] { 3, 4, 5, 6, 7, 8, 9, 10, 12 }) for (int g = 0; g < 4; g++) for (int s = 0; s < 4; s++)
            A(GX, "Demon Voice Lab: Down " + st + ", " + gn[g] + ", " + sp[s], "pshift{p1=-" + st + ";keep=0;fshift=-" + Math.Min(12, (st + 2) / 2 + 2) + ";mix=100} > growl{" + gs[g] + "}" + ss[s], "Demon voice " + st + " semitones down, " + gn[g].ToLowerInvariant() + " growl, " + sp[s].ToLowerInvariant() + (s == 0 ? " (no reverb)." : "."));
    }

    // ---- censors: 7 sounds x 3 fades, and bleep pitches ----
    static void GenCensor()
    {
        string[] fd = { "Hard", "Smooth", "Soft" }; int[] fms = { 1, 8, 30 };
        for (int m = 0; m < CensorFx.Modes.Length; m++) for (int f = 0; f < 3; f++)
        {
            string mn = CensorFx.Modes[m]; mn = mn.Substring(0, mn.IndexOf(' '));
            A(GK, "Censor Library: " + mn + ", " + fd[f] + " edges", "censor", "mode=" + m + ";fade=" + fms[f], CensorFx.Modes[m] + " with " + fd[f].ToLowerInvariant() + " edges (" + fms[f] + " ms). Automate CENSOR or use Censor this section.");
        }
        foreach (int hz in new[] { 500, 750, 1000, 1500, 2000 }) foreach (int lv in new[] { -18, -12, -6 })
            A(GK, "Bleep: " + hz + " Hz, " + (lv == -18 ? "Quiet" : lv == -12 ? "Medium" : "Loud"), "censor", "mode=0;tone=" + hz + ";level=" + lv, "A " + hz + " Hz bleep over the word, " + (lv == -18 ? "quiet." : lv == -12 ? "medium level." : "loud."));
    }
}
