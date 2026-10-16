using System;
using System.Collections.Generic;

// Factory presets for Purphex VST batch 7 (Tri-Drive, Shift, Maximizer, Spectral Gate).
static class PxPlugins7
{
    public static readonly string[] TriDrive = {
        "Init|",
        "Bass: Grit on Top|lo_drive=10;mid_drive=45;hi_drive=55;hi_type=2;xlo=150",
        "Bass: 808 Warmth|lo_drive=40;lo_type=0;mid_drive=20;hi_on=0;xlo=120",
        "Drums: Crushed Hats|lo_drive=0;mid_drive=15;hi_drive=70;hi_type=4;xhi=5000",
        "Drums: Room Smash|lo_drive=35;mid_drive=50;hi_drive=40;comp=60;mix=70",
        "Vocal: Edge|lo_on=0;mid_drive=35;mid_type=1;hi_drive=25;filt=3;cut=150;mix=60",
        "Vocal: Megaphone|lo_on=0;mid_drive=60;mid_type=2;hi_on=0;filt=2;cut=1500;res=40",
        "Synth: Octave Fuzz|lo_drive=20;mid_drive=60;mid_type=3;hi_drive=50;hi_type=3",
        "FX: Metal Ring|mid_drive=50;fb=70;fbt=6;mix=60",
        "FX: Bit Wreck|lo_type=4;lo_drive=60;mid_type=4;mid_drive=70;hi_type=4;hi_drive=80",
        "Guitar: Stack|lo_drive=40;mid_drive=65;mid_type=0;hi_drive=40;filt=1;cut=6000;comp=40",
        "Master: Gentle Glue|lo_drive=10;mid_drive=10;hi_drive=8;comp=25;mix=50",
    };
    public static readonly string[] Shift = {
        "Init|",
        "Subtle: Slow Phaser|a_shift=1;a_dep=3;a_rate=0.2;a_mix=50",
        "Subtle: Stereo Drift|a_shift=0;spread=6;a_mix=60",
        "Vocal: Robot Choir|a_shift=60;b_on=1;b_shift=-60;route=1;mix=70",
        "Vocal: Alien|a_shift=-180;a_dir=0;fb=30;fbt=40",
        "FX: Barber Pole Up|a_shift=4;fb=80;fbt=150;mix=70",
        "FX: Barber Pole Down|a_shift=-4;fb=80;fbt=150;mix=70",
        "FX: Bell Metal|a_shift=470;a_dir=2;a_mix=60",
        "FX: Ring Mod Tremolo|a_shift=8;a_dir=2",
        "Drums: Tuned Down Hits|a_shift=-90;a_mix=100;mix=60",
        "Synth: Detuned Spread|a_shift=2;b_on=1;b_shift=-2;route=1;spread=10",
        "FX: Spiral Sweep|a_shift=0;a_dep=400;a_rate=0.1;fb=50;fbt=80",
    };
    public static readonly string[] Maximizer = {
        "Init|",
        "Master: Streaming Ready|loud=40;ceil=-1;target=1",
        "Master: Loud and Clean|loud=65;ceil=-0.3;rel=150",
        "Master: Club Banger|loud=80;char=2;low=1.5;mono=2;target=3",
        "Master: Warm Glue|loud=45;char=1;rel=250",
        "Master: Wide and Bright|loud=50;high=2;width=130;mono=1",
        "Master: Max Volume|loud=95;char=2;rel=60;target=4",
        "Beat: Punchy|loud=55;char=2;rel=80;low=1",
        "Vocal: Upfront|loud=40;high=1.5;rel=100",
        "Bass: Solid Mono|loud=45;mono=3;low=2",
        "Subtle: Safety Limiter|loud=0;ceil=-1;rel=200",
        "Lo-fi: Squashed|loud=90;char=1;rel=500;high=-4",
    };
    public static readonly string[] SpectralGate = {
        "Init|",
        "Drums: Kick Mic Spill|thr=-40;red=25;att=2;rel=80;hi=4000",
        "Drums: Snare Bleed|thr=-38;red=20;att=1;rel=60;lo=150",
        "Drums: Tom Rings|thr=-35;red=30;att=3;rel=200",
        "Vocal: Room Noise|thr=-55;red=18;att=5;rel=150;smooth=50",
        "Vocal: Hiss Clean|thr=-60;red=15;lo=2000;smooth=60",
        "Guitar: Amp Hum|thr=-55;red=24;hi=1000;smooth=40",
        "Podcast: Fan Noise|thr=-58;red=20;rel=250;smooth=70",
        "FX: Spectral Chop|thr=-25;red=60;att=1;rel=15;smooth=0",
        "FX: Ghost (Removed Only)|thr=-40;red=40;listen=1",
        "Subtle: Light Clean|thr=-65;red=8;smooth=50",
        "Heavy: Tight Gate|thr=-30;red=50;att=1;rel=40",
    };
    public static void RegisterAll()
    {
        FxRegistry.Register(new FxRegistry.Info("pxtri", "Purphex Tri-Drive", "Purphex VST", "Three-band distortion with a filter, a compressor and feedback"), () => new TriDriveFx());
        FxRegistry.Register(new FxRegistry.Info("pxshift", "Purphex Shift", "Purphex VST", "Two frequency shifters with LFOs, feedback and stereo spread"), () => new ShiftFx());
        FxRegistry.Register(new FxRegistry.Info("pxmax", "Purphex Maximizer", "Purphex VST", "One-knob loudness: limiter, character, tone, mono bass, width and LUFS targets"), () => new MaximizerFx());
        FxRegistry.Register(new FxRegistry.Info("pxsgate", "Purphex Spectral Gate", "Purphex VST", "Gates every frequency on its own: learns the spill and the wanted sound"), () => new SpectralGateFx());
        PresetsExtra.Data["pxtri"] = TriDrive; PresetsExtra.Data["pxshift"] = Shift; PresetsExtra.Data["pxmax"] = Maximizer; PresetsExtra.Data["pxsgate"] = SpectralGate;
    }
}
