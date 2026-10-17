using System;
using System.Collections.Generic;

// Factory presets for Purphex VST batch 9 (Reference, Pathfinder, Modular, Dreamscape, Morph).
static class PxPlugins9
{
    public static readonly string[] Reference = {
        "Init|",
        "Compare: Level Matched|listen=1;match=1",
        "Compare: Real Levels|listen=1;match=0",
        "Check: Mono Mix|mono=1",
        "Check: Mono Reference|listen=1;mono=1",
        "Focus: Low End (Mine)|focus=1",
        "Focus: Low End (Reference)|listen=1;focus=1",
        "Focus: Vocal Range (Mine)|focus=2",
        "Focus: Vocal Range (Reference)|listen=1;focus=2",
        "Focus: Air (Mine)|focus=3",
        "Focus: Air (Reference)|listen=1;focus=3",
        "Loop: Reference From Start|listen=1;sync=1",
    };
    public static readonly string[] Pathfinder = {
        "Init|",
        "Vocal: Classic Reverse Wash|time=2;fb=40;diff=40;mix=35",
        "Vocal: Reverse Lead-In|time=3;fb=20;mix=30;duck=50",
        "Vocal: Ghost Octave Up|time=2;pitch=12;fb=30;diff=30;mix=30",
        "Vocal: Dark Reverse Throw|time=2;lp=3000;fb=45;mix=35",
        "Guitar: Reverse Ping-Pong|dir=2;time=1;fb=45;width=100;mix=40",
        "Synth: Rising Octave Down|time=3;pitch=-12;fb=50;mix=35",
        "Drums: Reverse Snare Pre|time=1;fb=15;hp=400;mix=40",
        "FX: Endless Reverse Cloud|time=4;fb=85;diff=80;mix=45",
        "FX: Forward Tape Echo|dir=1;time=2;fb=45;lp=5000;mix=35",
        "Subtle: Backward Air|time=1;fb=20;hp=1500;mix=20;width=100",
        "Ducked: Reverse Under Vocal|time=2;fb=50;duck=80;mix=45",
    };
    public static readonly string[] Modular = {
        "Init|",
        "Drums: Crunchy Bus|s0_type=1;s0_amt=55;s1_type=6;s1_amt=20;s2_type=3;s2_amt=25",
        "Drums: Parallel Smash|s0_type=1;s0_amt=85;s1_type=5;s1_amt=40;mix=40",
        "Bass: Folded Growl|s0_type=2;s0_amt=60;s1_type=5;s1_amt=55;s1_mod=60;s1_src=0;lrate=2",
        "Bass: Rectified Grit|s0_type=4;s0_amt=50;s1_type=1;s1_amt=40",
        "Vocal: Radio Fold|s0_type=7;s0_amt=45;s1_type=2;s1_amt=35;mix=60",
        "Vocal: Robot Ring|s0_type=9;s0_amt=50;s1_type=7;s1_amt=50;mix=55",
        "Synth: Wobble Filter|s0_type=1;s0_amt=30;s1_type=5;s1_amt=45;s1_mod=90;s1_src=0;lrate=3",
        "Synth: Envelope Wah|s0_type=7;s0_amt=30;s0_mod=80;s0_src=1;sens=70",
        "FX: Bitcrushed Comb|s0_type=8;s0_amt=60;s1_type=3;s1_amt=55;s2_type=11;s2_amt=30",
        "FX: Tremolo Drive|s0_type=1;s0_amt=50;s1_type=10;s1_amt=70",
        "Lofi: Downsampled Tape|s0_type=11;s0_amt=40;s1_type=5;s1_amt=35;s2_type=3;s2_amt=20",
    };
    public static readonly string[] Dreamscape = {
        "Init|",
        "Vocal: Heavenly Shimmer|shimmer=45;decay=10;size=80;mix=35",
        "Vocal: Soft Halo|shimmer=10;decay=4;size=55;bloom=50;mix=25",
        "Pad: Endless Cathedral|decay=25;size=100;shimmer=30;mod=50;mix=50",
        "Pad: Fifths Rising|shift=1;shimmer=50;decay=14;mix=45",
        "Keys: Dream Room|decay=5;size=60;damp=55;shimmer=15;mix=30",
        "Guitar: Ambient Swell|bloom=80;decay=12;shimmer=35;mix=45",
        "FX: Frozen Drone|freeze=1;mix=60",
        "FX: Octave Down Abyss|shift=3;shimmer=50;decay=18;damp=60;mix=45",
        "FX: Sparkle Tail|shift=2;shimmer=40;highcut=18000;decay=9;mix=35",
        "Dark: Deep Space|damp=80;highcut=4000;decay=20;size=100;mix=40",
        "Subtle: Wide Air|decay=2.5;size=40;width=100;mix=18",
    };
    public static readonly string[] Morph = {
        "Init|",
        "Vocoder: Classic Robot|mode=1;amt=100;smooth=20",
        "Vocoder: Smooth Choir|mode=1;amt=90;smooth=60;rel=250",
        "Vocoder: Formant Up|mode=1;amt=100;formant=5",
        "Vocoder: Formant Down|mode=1;amt=100;formant=-5",
        "Morph: Half and Half|mode=0;amt=50",
        "Morph: Mostly Source|mode=0;amt=80",
        "Morph: A Touch of Source|mode=0;amt=25",
        "Gate: Rhythm From Drums|mode=2;amt=100;sens=-40",
        "Gate: Soft Duck Pattern|mode=2;amt=60;sens=-45",
        "Talkbox: Synth Talks|mode=1;amt=100;smooth=10;att=5;rel=60",
        "Pad: Breathing Morph|mode=0;amt=60;att=80;rel=600",
    };
    public static void RegisterAll()
    {
        FxRegistry.Register(new FxRegistry.Info("pxref", "Purphex Reference", "Purphex VST", "Compare your mix with a reference song: level matched, spectrum, loudness and width"), () => new ReferenceFx());
        FxRegistry.Register(new FxRegistry.Info("pxpath", "Purphex Pathfinder", "Purphex VST", "Reverse delay in time with the song: pitch, feedback, filters, diffusion and ducking"), () => new PathfinderFx());
        FxRegistry.Register(new FxRegistry.Info("pxmodular", "Purphex Modular", "Purphex VST", "Four slots of drive, fold, crush, filters, comb, ring and more, each moved by an LFO or envelope"), () => new ModularFx());
        FxRegistry.Register(new FxRegistry.Info("pxdream", "Purphex Dreamscape", "Purphex VST", "A huge ambient space with shimmer, freeze, bloom and drift"), () => new DreamscapeFx());
        FxRegistry.Register(new FxRegistry.Info("pxmorph", "Purphex Morph", "Purphex VST", "Morph or vocode this track with another one (pick any track as the source)"), () => new MorphFx());
        PresetsExtra.Data["pxref"] = Reference; PresetsExtra.Data["pxpath"] = Pathfinder; PresetsExtra.Data["pxmodular"] = Modular; PresetsExtra.Data["pxdream"] = Dreamscape; PresetsExtra.Data["pxmorph"] = Morph;
    }
}
