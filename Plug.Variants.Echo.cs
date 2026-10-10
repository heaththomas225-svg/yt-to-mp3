using System;

// Echo, delay and reverb plugins
static partial class PluginTables
{
    const string ER = "Echo & Reverb";
    const string HE = "decay", HEM = "mod,modr,wow,diff", HEX = "duck,duckr", HEV = "rev,revsize";
    const string RG = "gate,gthr,ghold,grel", RR = "rev,rtime", RD = "duck,duckr", RF = "freeze", RS = "shim,shimst", RSP = "spring";

    static void FillEcho()
    {
        FillReverb();
        A(ER, "Vocal Echo", "echo", "mode=1;sync=4;fb=30;lc=300;hc=5500;duck=45;duckr=300;mix=28;rev=15", "Repeats that stay out of the way: they duck while the singer is singing and fill the gaps between phrases.", HE);
        A(ER, "Stereo Delay", "echo", "mode=1;sync=4;offset=40;fb=35;mix=30", "Separate left and right repeats, the right a little later for width.", HE + "," + HEM);
        A(ER, "Mono Delay", "echo", "mode=0;sync=0;time=380;fb=35;mix=30", "A single centred repeat, with time set in milliseconds.", HE + ",offset," + HEM);
        A(ER, "Ping-Pong Delay", "echo", "mode=2;sync=4;fb=45;mix=32", "Repeats bounce from left to right and back.", HE + ",offset," + HEM);
        A(ER, "Tempo-Synced Delay", "echo", "mode=1;sync=7;fb=35;mix=30", "Locks to your project tempo so the repeats stay in time (change Time for other note lengths).", HE + ",offset," + HEM);
        A(ER, "Slapback Echo", "echo", "mode=0;sync=0;time=95;fb=10;lc=200;hc=5000;mix=35", "One very short repeat: the classic rock-and-roll vocal thickener.", HE + ",offset,wow," + HEX + "," + HEV + ",diff");
        A(ER, "Tape Echo", "echo", "mode=0;sync=0;time=320;fb=45;lc=180;hc=4200;sat=40;mod=0.8;modr=0.6;wow=45;mix=32", "Warm, dark repeats that wobble like an old tape machine.", HE + ",offset");
        A(ER, "Analog Echo", "echo", "mode=0;sync=0;time=420;fb=50;lc=250;hc=3200;sat=30;mod=1.5;modr=0.35;wow=20;mix=30", "Murky bucket-brigade style repeats that get darker each time.", HE + ",offset");
        A(ER, "Digital Delay", "echo", "mode=1;sync=4;fb=35;lc=20;hc=20000;mix=30", "Perfectly clean repeats with no colour.", HE + ",offset," + HEM);
        A(ER, "Multi-Tap Delay", "echo", "mode=3;sync=7;fb=25;decay=70;mix=34", "Four evenly spaced taps that fade away, alternating left and right.", "offset");
        A(ER, "Filtered Delay", "echo", "mode=2;sync=8;fb=55;lc=500;hc=2500;mix=34", "Repeats limited to the middle frequencies so they sit behind the vocal.", HE + ",offset," + HEM);
        A(ER, "Telephone Echo", "echo", "mode=0;sync=4;fb=50;lc=500;hc=2800;sat=35;mix=34", "Repeats that sound like they are coming down a phone line.", HE + ",offset," + HEM);
        A(ER, "Reverse Delay", "echo", "mode=4;sync=4;fb=20;lc=150;hc=8000;mix=38", "Backwards swells that build into every phrase.", HE + ",offset," + HEM);
        A(ER, "Dotted-Eighth Delay", "echo", "mode=2;sync=8;fb=40;mix=32", "Three-sixteenth repeats: the rhythmic delay used in so much modern music.", HE + ",offset," + HEM);
        A(ER, "Quarter-Note Delay", "echo", "mode=1;sync=4;fb=35;mix=30", "One repeat per beat.", HE + ",offset," + HEM);
        A(ER, "Eighth-Note Delay", "echo", "mode=1;sync=7;fb=35;mix=30", "Two repeats per beat.", HE + ",offset," + HEM);
        A(ER, "Ping-Pong Vocal Throw", "echo", "mode=2;sync=4;fb=55;lc=350;hc=6000;duck=75;duckr=350;mix=40;rev=18", "Throws the end of a phrase across the stereo field. It ducks, so only the last word echoes.", HE + ",offset," + HEM);
        A(ER, "Feedback Delay", "echo", "mode=1;sync=7;fb=88;sat=35;hc=4500;lc=150;mix=35", "Long, near-endless repeats that build up and glow.", HE + ",offset");
        A(ER, "Duck Delay", "echo", "mode=1;sync=5;fb=45;duck=85;duckr=320;mix=45", "Repeats that drop out of the way while the sound plays and swell back after.", HE + ",offset," + HEM);
        A(ER, "Modulated Delay", "echo", "mode=1;sync=0;time=300;fb=40;mod=4;modr=0.5;mix=32", "Repeats whose time slowly drifts for a swirling, detuned echo.", HE + ",offset");
        A(ER, "Chorus Delay", "echo", "mode=1;sync=0;time=45;fb=22;mod=6;modr=0.9;width=150;offset=30;mix=40", "A short delay with chorus-style wobble: wide, shimmery thickening.", HE);
    }

    static void FillReverb()
    {
        A(ER, "Stereo Reverb", "reverb2", "mode=1;size=50;decay=2.0;mix=25", "A smooth, wide, all-round reverb.", RG + "," + RR + "," + RD + "," + RF + "," + RS + "," + RSP);
        A(ER, "Room Reverb", "reverb2", "mode=0;size=45;decay=0.7;er=45;damp=8000;mix=22", "A small, natural room: adds closeness and body.", RG + "," + RR + "," + RD + "," + RF + "," + RS + "," + RSP);
        A(ER, "Hall Reverb", "reverb2", "mode=1;size=65;decay=2.8;pre=25;mix=26", "A concert hall: big, smooth and graceful.", RG + "," + RR + "," + RD + "," + RF + "," + RS + "," + RSP);
        A(ER, "Plate Reverb", "reverb2", "mode=2;size=50;decay=1.8;er=0;diff=90;hc=14000;mix=24", "A bright, dense studio plate: the classic vocal reverb.", RG + "," + RR + "," + RD + "," + RF + "," + RS + "," + RSP);
        A(ER, "Chamber Reverb", "reverb2", "mode=3;size=55;decay=1.3;damp=5500;mix=24", "A warm echo chamber with a soft top end.", RG + "," + RR + "," + RD + "," + RF + "," + RS + "," + RSP);
        A(ER, "Spring Reverb", "reverb2", "mode=4;size=50;decay=1.6;spring=65;damp=6000;mix=26", "The boingy drip of a guitar-amp spring tank.", "er,diff,mod," + RG + "," + RR + "," + RD + "," + RF + "," + RS);
        A(ER, "Shimmer Reverb", "reverb2", "mode=6;size=75;decay=6;shim=45;shimst=12;mix=35", "A huge reverb with an octave-up glow that floats above the sound.", RG + "," + RR + "," + RD + "," + RF + "," + RSP);
        A(ER, "Gated Reverb", "reverb2", "mode=2;size=60;decay=2.5;gate=1;gthr=-30;ghold=160;grel=30;mix=40", "Big reverb that is chopped off short: the 80s snare sound.", RR + "," + RD + "," + RF + "," + RS + "," + RSP);
        A(ER, "Reverse Reverb", "reverb2", "mode=2;size=60;decay=2.2;rev=1;rtime=1100;mix=40", "The reverb plays backwards, swelling and sucking in.", RG + "," + RD + "," + RF + "," + RS + "," + RSP);
        A(ER, "Ambient Reverb", "reverb2", "mode=6;size=80;decay=4.5;mod=45;pre=35;damp=6500;mix=35", "A slow, drifting wash for pads and atmosphere.", RG + "," + RR + "," + RD + "," + RS + "," + RSP);
        A(ER, "Dark Reverb", "reverb2", "mode=1;size=65;decay=3.5;damp=2500;hc=5000;mix=28", "A dark, moody hall with the brightness removed.", RG + "," + RR + "," + RD + "," + RF + "," + RS + "," + RSP);
        A(ER, "Vocal Space", "reverb2", "mode=2;size=45;decay=1.4;pre=30;er=25;lc=250;hc=10000;duck=35;duckr=300;mix=22", "A tasteful space for a lead vocal: pre-delayed, ducked and kept out of the mud.", RG + "," + RR + "," + RF + "," + RS + "," + RSP);
        A(ER, "Early Reflections", "reverb2", "mode=0;size=50;tail=0;er=100;pre=0;decay=0.5;mix=40", "Just the first bounces off the walls: a sense of room without a tail.", RG + "," + RR + "," + RD + "," + RF + "," + RS + "," + RSP + ",tail,decay,damp,diff,mod");
        A(ER, "Convolution Reverb", "conv", "ir=2;mix=30", "Uses a recorded-style impulse of a real space: hall, cathedral, plate, spring and more (or load your own).", "");
        A(ER, "Reverb Ducking", "reverb2", "mode=1;size=60;decay=3;duck=75;duckr=400;mix=35", "A big reverb that gets out of the way while the sound plays and blooms back in the gaps.", RG + "," + RR + "," + RF + "," + RS + "," + RSP);
    }
}
