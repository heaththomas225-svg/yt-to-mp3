using System;
using System.Collections.Generic;

// Factory presets for Purphex VST batch 6 (Doubler, Octave Choir, Vowel, Step Shaper).
static class PxPlugins6
{
    public static readonly string[] Doubler = {
        "Init|",
        "Vocal: Tight Double|v0_on=1;v1_on=1;v0_d=12;v1_d=22;v0_det=-6;v1_det=6;v0_pan=-60;v1_pan=60;v0_g=-8;v1_g=-8",
        "Vocal: Wide Quad|v0_on=1;v1_on=1;v2_on=1;v3_on=1;v0_pan=-90;v1_pan=90;v2_pan=-45;v3_pan=45;v2_d=35;v3_d=45",
        "Vocal: Octave Down Layer|v0_on=1;v1_on=0;v0_oct=0;v0_pan=0;v0_g=-10;v0_d=0;v0_det=0",
        "Vocal: Octave Up Air|v0_on=1;v1_on=1;v0_oct=2;v1_oct=2;v0_g=-16;v1_g=-16;hi_g=-3",
        "Vocal: Chorus Wobble|v0_on=1;v1_on=1;v0_dep=10;v1_dep=12;v0_rate=0.8;v1_rate=1.1",
        "Ad-Lib: Big Stack|v0_on=1;v1_on=1;v2_on=1;v3_on=1;v2_oct=0;v3_oct=2;v2_g=-12;v3_g=-14",
        "Guitar: Fake Double|v0_on=1;v1_on=0;v0_d=18;v0_det=8;v0_pan=80;dpan=-60",
        "Synth: Thick Unison|v0_on=1;v1_on=1;v2_on=1;v3_on=1;v0_det=-15;v1_det=15;v2_det=-8;v3_det=8;v0_d=2;v1_d=3;v2_d=4;v3_d=5",
        "Slap: Echo Doubles|v0_on=1;v1_on=1;v0_d=90;v1_d=120;v0_fb=30;v1_fb=30",
        "Subtle: Gentle Width|v0_on=1;v1_on=1;v0_g=-14;v1_g=-14;v0_det=-3;v1_det=3",
        "Vocal: Dark Double|v0_on=1;v1_on=1;hi_g=-6;hi_f=4000;v0_det=-7;v1_det=7",
    };
    public static readonly string[] Choir = {
        "Init|",
        "Choir: 8 Vox Detune|o0_on=1;o1_on=1;o2_on=1;o3_on=1;o4_on=1;o5_on=1;o6_on=1;o7_on=1",
        "Choir: Major Triad|o0_on=1;o0_iv=0;o1_on=1;o1_iv=4;o2_on=1;o2_iv=7;o3_on=0;scale=1",
        "Choir: Minor Triad|o0_on=1;o0_iv=0;o1_on=1;o1_iv=3;o2_on=1;o2_iv=7;o3_on=0;scale=2",
        "Choir: Octaves|o0_on=1;o0_iv=-12;o1_on=1;o1_iv=12;o2_on=0;o3_on=0;o0_d=20;o1_d=30",
        "Choir: Fifths Up|o0_on=1;o0_iv=7;o1_on=1;o1_iv=19;o2_on=0;o3_on=0",
        "Echo: Harmony Echoes|o0_on=1;o1_on=1;o2_on=1;o3_on=1;o0_fb=40;o1_fb=40;o2_fb=40;o3_fb=40;mix=40",
        "Vocal: Thick Lead|o0_on=1;o0_iv=0;o0_ct=8;o1_on=1;o1_iv=0;o1_ct=-8;o2_on=0;o3_on=0;o0_d=15;o1_d=25",
        "Vocal: Ghost Below|o0_on=1;o0_iv=-12;o0_lvl=-8;o1_on=0;o2_on=0;o3_on=0",
        "Pad: Big Chord Cloud|o0_on=1;o0_iv=0;o1_on=1;o1_iv=4;o2_on=1;o2_iv=7;o3_on=1;o3_iv=11;o4_on=1;o4_iv=12;o5_on=1;o5_iv=16;mix=60;rand=40",
        "FX: Robot Chorus|o0_on=1;o1_on=1;o2_on=1;o3_on=1;rand=0;o0_d=0;o1_d=0;o2_d=0;o3_d=0",
        "Choir: Minor Sixths|o0_on=1;o0_iv=0;o1_on=1;o1_iv=8;o2_on=1;o2_iv=-4;o3_on=0;scale=2",
    };
    public static readonly string[] Vowel = {
        "Init|",
        "Vowel: Talking Bass|x=0.85;y=0.95;move=1;msync=3;mdepth=80;drive=40",
        "Vowel: Wah Sweep|move=2;msync=1;mdepth=90;q=12",
        "Vowel: Robot Speech|move=3;msync=2;mdepth=70;snap=80",
        "Vowel: Ooh Pad|x=0.97;y=0.02;q=10;mix=80",
        "Vowel: Aah Choir|x=0.85;y=0.95;q=14",
        "Vowel: Eee Brightener|x=0.05;y=0.02;mix=50",
        "Vowel: Gritty Yoy|x=0.5;y=0.5;move=1;msync=4;drive=70;mdepth=100",
        "Vowel: Slow Morph|move=3;msync=0;mrate=0.1;mdepth=90",
        "Vowel: Small Mouth|shift=6;q=10",
        "Vowel: Giant Mouth|shift=-8;q=8;drive=30",
        "Vowel: Ah to Oh|x=0.85;y=0.95;move=2;msync=1;mdepth=60",
    };
    public static readonly string[] Step = {
        "Init|",
        "Gate: Funky Synth|grid=2;smooth=15",
        "Gate: Trance 1/16|s0=100;s1=0;s2=100;s3=0;s4=100;s5=0;s6=100;s7=0;s8=100;s9=0;s10=100;s11=0;s12=100;s13=0;s14=100;s15=0;grid=2;smooth=5",
        "Gate: Off-Beat Pump|s0=0;s1=100;s2=0;s3=100;s4=0;s5=100;s6=0;s7=100;s8=0;s9=100;s10=0;s11=100;s12=0;s13=100;s14=0;s15=100;grid=1;smooth=40",
        "Gate: Swung Chops|grid=2;swing=40;smooth=10",
        "Gate: Half-Time|rate=0;grid=1;smooth=20",
        "Filter: Stepped Sweep|s0=10;s1=20;s2=30;s3=40;s4=50;s5=60;s6=70;s7=80;s8=90;s9=100;s10=90;s11=80;s12=60;s13=40;s14=20;s15=10;vol=0;follow=1;cut=8000;res=40",
        "Filter: Acid Steps|vol=20;follow=1;cut=4000;res=70;grid=2",
        "Rhythm: Triplet Feel|s0=100;s1=30;s2=60;s3=100;s4=30;s5=60;s6=100;s7=30;s8=60;s9=100;s10=30;s11=60;s12=100;s13=30;s14=60;s15=100;grid=2",
        "Subtle: Gentle Movement|vol=30;smooth=60",
        "Stutter: 1/32 Roll|grid=3;smooth=0",
        "Gate: Dotted Feel|s0=100;s1=0;s2=0;s3=100;s4=0;s5=0;s6=100;s7=0;s8=100;s9=0;s10=0;s11=100;s12=0;s13=0;s14=100;s15=0;grid=2;smooth=10",
    };
    public static void RegisterAll()
    {
        FxRegistry.Register(new FxRegistry.Info("pxdoubler", "Purphex Doubler", "Purphex VST", "Four-voice doubler: delay, detune, octave, pan and wobble for each voice"), () => new DoublerFx());
        FxRegistry.Register(new FxRegistry.Info("pxchoir", "Purphex Octave Choir", "Purphex VST", "Eight harmony voices with their own interval, delay, pan and level, snapped to a key"), () => new OctaveChoirFx());
        FxRegistry.Register(new FxRegistry.Info("pxvowel", "Purphex Vowel", "Purphex VST", "Vowel formant filter: drag around a vowel pad, snap, shift and move"), () => new PxVowelFx());
        FxRegistry.Register(new FxRegistry.Info("pxstep", "Purphex Step Shaper", "Purphex VST", "Sixteen steps of volume and filter movement in time with the song"), () => new StepShaperFx());
        PresetsExtra.Data["pxdoubler"] = Doubler; PresetsExtra.Data["pxchoir"] = Choir; PresetsExtra.Data["pxvowel"] = Vowel; PresetsExtra.Data["pxstep"] = Step;
    }
}
