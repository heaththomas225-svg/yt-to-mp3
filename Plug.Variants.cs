using System;
using System.Collections.Generic;

// The named built-in plugins. Each line is a real plugin: an engine plus its starting values (and, for chains, several engines in a row).
static partial class PluginTables
{
    const string PV = "Pitch & Voice", TD = "Telephone & Distortion";
    const string HV1 = "p2,c2,l2,pan2,d2,p3,c3,l3,pan3,d3,p4,c4,l4,pan4,d4,mshape,mrate,mdepth", HV2 = "p3,c3,l3,pan3,d3,p4,c4,l4,pan4,d4,mshape,mrate,mdepth", HV4 = "mshape,mrate,mdepth";
    static void A(string cat, string name, string engine, string preset, string blurb, string hide = "") { PluginLibrary.Add(cat, name, engine, preset, blurb, hide); }

    public static void Fill() { FillPitch(); FillTone(); FillEcho(); FillWorld(); FillMelody(); FillEq(); FillVocal(); FillCreative(); FillBass(); FillVoice2(); FillLibrary(); }

    static void FillPitch()
    {
        A(PV, "Pitch Shifter", "pshift", "p1=5;l1=100;keep=70;mix=100", "Shift the pitch up or down by semitones and cents. Keeps the voice natural.", HV1 + ",d1,pan1");
        A(PV, "Pitch Shifter ±12 Semitones", "pshift", "p1[-12,12]=4;keep=70;fshift[-12,12]=0", "Pitch shifter limited to one octave either way, for fine control.", HV1 + ",d1,pan1");
        A(PV, "Pitch Shifter ±24 Semitones", "pshift", "p1[-24,24]=7;keep=50", "Pitch shifter with a two-octave range up or down.", HV1 + ",d1,pan1");
        A(PV, "Formant Shifter", "formant", "shift=-4", "Changes the character of a voice (deeper / smaller) without changing its pitch.");
        A(PV, "Pitch + Formant Shifter", "pshift", "p1=-3;fshift=-3;keep=0;mix=100", "Move the pitch and the voice colour together or apart. Keep original formants at 0 lets them follow the pitch.", HV1 + ",d1,pan1");
        A(PV, "Octave Down", "pshift", "p1=-12;keep=0;mix=100", "One octave lower, for deep doubles and demon vocals.", HV1 + ",d1,pan1");
        A(PV, "Octave Up", "pshift", "p1=12;keep=0;mix=100", "One octave higher.", HV1 + ",d1,pan1");
        A(PV, "Octaver", "cshift", "p1=-12;l1=85;p2=12;l2=40;win=30;dry=100;mix=100", "Adds a note an octave below and a quieter one an octave above your sound, like a pedal.", "mshape,mrate,mdepth,fb");
        A(PV, "Vocal Doubler", "pshift", "p1=0;c1=12;l1=75;pan1=-70;d1=18;p2=0;c2=-11;l2=75;pan2=70;d2=28;mix=62", "Two quietly detuned, delayed copies left and right: a natural double-tracked vocal.", HV2);
        A(PV, "Vocal Harmonizer", "pshift", "p1=3;l1=55;pan1=-45;d1=9;p2=7;l2=50;pan2=45;d2=15;keep=100;mix=52", "Adds a third and a fifth above your vocal (fixed intervals). Change the pitches to fit your song.", HV2);
        A(PV, "4-Voice Harmonizer", "pshift", "p1=3;l1=45;pan1=-70;d1=8;p2=7;l2=45;pan2=70;d2=14;p3=-5;l3=40;pan3=-25;d3=20;p4=12;l4=38;pan4=25;d4=26;keep=100;mix=55", "Four harmony voices, each with its own pitch, level, pan and delay.", HV4);
        A(PV, "Vocal Transposer", "pshift", "p1[-12,12]=2;keep=100;mix=100", "Transpose a vocal up or down a few semitones while keeping it sounding like the same singer.", HV1 + ",d1,pan1,fshift");
        A(PV, "Micro Pitch Shift", "pshift", "p1=0;c1=8;l1=100;pan1=-80;d1=12;p2=0;c2=-8;l2=100;pan2=80;d2=16;mix=45", "A tiny pitch difference left and right that widens and thickens a sound without a chorus sound.", HV2);
        A(PV, "Stereo Detune", "pshift", "p1=0;c1=-14;l1=100;pan1=-100;d1=0;p2=0;c2=14;l2=100;pan2=100;d2=0;mix=70", "Detunes the left and right sides in opposite directions for a huge stereo spread.", HV2);
        A(PV, "Vocal Thickener", "pshift", "p1=0;c1=9;l1=70;pan1=-60;d1=10;p2=0;c2=-9;l2=70;pan2=60;d2=14;p3=-12;c3=0;l3=22;pan3=0;d3=0;mix=55", "Two small detunes and a quiet octave below to make a thin vocal fuller.", "p4,c4,l4,pan4,d4,mshape,mrate,mdepth");
        A(PV, "Unison Voice", "pshift", "p1=0;c1=-18;l1=60;pan1=-85;d1=8;p2=0;c2=-6;l2=60;pan2=-30;d2=15;p3=0;c3=6;l3=60;pan3=30;d3=22;p4=0;c4=18;l4=60;pan4=85;d4=29;mix=65", "Four detuned copies spread across the stereo field like a stack of singers.", HV4);
        A(PV, "Vocal Character Transformer", "pshift", "p1=-2;fshift=-4;keep=0;mix=100", "Turns your voice into another person: lower the formants for a bigger voice, raise them for a smaller one.", HV1 + ",d1,pan1");
        A(PV, "Granular Pitch Shifter", "cshift", "win=8;p1=7;l1=100;dry=0;mix=100", "A rough, grainy pitch shifter with a digital, glitchy texture.", "p2,l2,fb,mshape,mrate,mdepth");
        A(PV, "Real-Time Pitch Shifter", "cshift", "win=14;p1=5;l1=100;dry=0;mix=100", "Very low delay (about 7 ms): good for playing live while you hear yourself.", "p2,l2,mshape,mrate,mdepth,fb");
        A(PV, "Pitch Glide", "cshift", "p1=0;l1=100;win=30;mshape=4;mrate=0.25;mdepth=12;dry=0;mix=100", "The pitch slides up or down again and again. Movement depth is how far, rate is how fast.", "p2,l2,fb");
        A(PV, "Vocal Chorus Pitch", "pshift", "p1=0;c1=14;l1=70;pan1=-55;d1=14;p2=0;c2=-14;l2=70;pan2=55;d2=22;mshape=1;mrate=0.6;mdepth=0.12;mix=55", "A chorus made of gently moving pitch instead of a moving delay.", "p3,c3,l3,pan3,d3,p4,c4,l4,pan4,d4");
        A(PV, "Auto-Tune-Style Pitch Corrector", "pitch", "speed=18;strength=100;natural=20;mix=100", "Snaps the voice to the nearest note of your key at a fast, polished speed.");
        A(PV, "Hard Tune", "pitch", "speed=0;strength=100;natural=0;mix=100", "Instant note snapping: the classic stepped, robotic vocal effect.");
        A(PV, "Robot Pitch", "pitch", "speed=0;strength=100;natural=0;scale=0;mask=4095;mix=100", "Hard tunes every note to the chromatic scale for a flat, robotic voice.");
        A(PV, "Automatic Vocal Tuner", "pitch", "speed=45;strength=85;natural=55;mix=100", "Gentle automatic tuning that keeps the singer's natural movement.");
    }

    static void FillTone()
    {
        A(TD, "Telephone Vocal", "band", "hp=350;lp=3200;slope=1;mono=1;bump=5;bumpf=1900;drive=6;noise=5", "The classic phone call sound: narrow, mid-heavy, slightly crunchy.");
        A(TD, "Telephone EQ", "band", "hp=300;lp=3400;slope=2;mono=0;bump=2;bumpf=1800", "Just the telephone frequency range (300 Hz - 3.4 kHz), clean, without noise.", "drive,noise,crackle,flutter");
        A(TD, "Vintage Telephone", "band", "hp=500;lp=2800;slope=1;bump=8;bumpf=1500;drive=12;noise=18;crackle=22;flutter=15;mono=1", "An old rotary phone line with crackle, hiss and wobble.");
        A(TD, "Radio Voice", "band", "hp=250;lp=5500;slope=1;bump=4;bumpf=2800;drive=8;noise=8;flutter=10;mono=1", "An AM radio announcer: tight bandwidth with a little hiss and fade.");
        A(TD, "Walkie-Talkie", "band{hp=500;lp=2800;slope=1;drive=18;noise=18;bump=6;bumpf=1700;mono=1;crackle=10} > crush{bits=10;rate=9000;mix=70}", "", "Squashed, noisy and digital, like a two-way radio.");
        A(TD, "Megaphone", "band", "hp=600;lp=3600;slope=1;bump=10;bumpf=2200;drive=24;mono=1;noise=4", "A loud-hailer: boxy mids and hard drive.");
        A(TD, "Police Radio", "band{hp=450;lp=3000;slope=2;drive=15;noise=22;crackle=35;flutter=25;bump=8;bumpf=1600;mono=1} > crush{bits=12;rate=11025;mix=60}", "", "Dispatch radio: harsh, crackly, with signal fade.");
        A(TD, "Lo-Fi Vocal", "crush{bits=11;rate=14000;tone=7000;dither=10} > band{hp=120;lp=7500;slope=0;noise=8;crackle=10;drive=4;flutter=8;mono=0}", "", "Dusty, crushed and warm: a sampled-from-cassette vocal.");
        A(TD, "Vintage Speaker", "band", "hp=120;lp=5200;slope=2;bump=6;bumpf=900;drive=10;mono=0", "A small old speaker: no bass or treble, boxy middle.");
        A(TD, "Band-Pass Filter", "band", "hp=500;lp=2500;slope=1;mono=0", "Only lets one band of frequencies through. Move the low and high cut to choose it.", "drive,noise,crackle,flutter,bump,bumpf,mono");
        A(TD, "Narrow-Band Filter", "band", "hp=800;lp=1600;slope=2;mono=0", "A very narrow slice of sound for a tiny, tinny effect.", "drive,noise,crackle,flutter,bump,bumpf,mono");
        A(TD, "Vocal Exciter", "exciter", "drive=8;harm=45;flo=3500;fhi=16000;mix=35", "Adds sparkle and presence by creating new high harmonics.");
        A(TD, "Vocal Saturator", "shaper", "mode=1;drive=9;tone=11000;mix=70;auto=1;pre=60", "Warm tube-style saturation that thickens a vocal.");
        A(TD, "Soft Saturation", "shaper", "mode=0;drive=6;mix=100;out=-1;auto=1", "A gentle tanh curve: adds warmth and glue.");
        A(TD, "Tube Saturation", "shaper", "mode=1;drive=11;bias=25;tone=10000;auto=1", "Even-harmonic tube warmth.");
        A(TD, "Tape Saturation", "shaper", "mode=2;drive=10;tone=9500;auto=1;pre=30", "Smooth tape compression and soft high end.");
        A(TD, "Overdrive", "shaper", "mode=3;drive=18;pre=120;tone=7000;auto=1", "Guitar-pedal style overdrive.");
        A(TD, "Hard Clipper", "shaper", "mode=4;drive=14;tone=14000;auto=0;out=-6", "Chops the tops of the waveform off: loud and aggressive.");
        A(TD, "Soft Clipper", "shaper", "mode=5;drive=10;tone=16000;auto=1", "Rounded clipping that adds loudness without harshness.");
        A(TD, "Fuzz", "shaper", "mode=6;drive=22;pre=100;tone=5500;auto=1;bias=30", "A thick, buzzy fuzz pedal sound.");
        A(TD, "Bitcrusher", "crush", "bits=8;rate=12000;mix=100", "Reduces the quality of the sound in steps: retro and gritty.");
        A(TD, "Sample-Rate Reducer", "crush", "bits=24;rate=6000;mix=100", "Lowers the sample rate for aliasing and a metallic, digital texture.", "bits,dither");
        A(TD, "Ring Modulator", "ring", "freq=440;wave=0;depth=100;mix=100", "Multiplies your sound by a tone: bell-like, metallic and robotic.");
        A(TD, "Frequency Shifter", "fshift", "shift=60;fb=0;mix=100", "Moves every frequency by the same number of Hz, which makes sounds inharmonic and strange. Feedback creates a rising spiral.");
        A(TD, "Comb Filter", "comb", "mode=0;freq=220;fb=75;mix=60", "A series of resonant peaks, like a metal tube. Tune it with Frequency.", "chord,spread,dyn");
        A(TD, "Vocoder", "vocoder", "carrier=0;note=48;chord=3;bands=2;sib=40;mix=100", "Makes a synth voice talk: your voice shapes a built-in synth sound.");
        A(TD, "Talkbox-Style Effect", "vocoder", "carrier=3;note=45;chord=0;bands=1;lo=200;hi=5000;sharp=1.8;att=3;rel=22;sib=50;dry=0", "A talkbox-style talking synth made with the vocoder.");
        A(TD, "Robot Voice", "vocoder", "carrier=1;note=48;chord=0;bands=1;sharp=1.0;sib=30;dry=0", "A flat-pitch robot voice.");
        A(TD, "Demon Voice", "pshift{p1=-8;l1=100;p2=-15;l2=65;keep=0;fshift=-4;mix=100} > shaper{mode=3;drive=11;tone=5500;mix=60;out=-3;auto=1}", "", "Two voices an octave or more below, a heavy formant drop and growl.");
        A(TD, "Monster Voice", "pshift{p1=-12;l1=100;p2=-19;l2=60;keep=0;fshift=-6;mix=100} > ring{freq=55;wave=0;depth=35;mix=45} > shaper{mode=6;drive=14;tone=4500;mix=50;auto=1}", "", "A huge, rough creature voice.");
        A(TD, "Dark Vocal", "pshift{p1=-2;keep=0;fshift=-3;mix=100} > band{hp=60;lp=3800;slope=0;bump=3;bumpf=400}", "", "Lowers and darkens a vocal without making it muddy.");
        A(TD, "Whisper Enhancer", "gate{thr=-52;range=18;att=1;rel=120} > exciter{drive=10;harm=60;flo=3500;fhi=16000;mix=60}", "", "Lifts a whisper: gates the room away and adds breathy high harmonics.");
    }
}
