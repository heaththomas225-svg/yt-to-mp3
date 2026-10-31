using System;
using System.Collections.Generic;

// Factory presets for Purphex VST batch 4 (Frost, Tape Dirt, Constellation).
static class PxPlugins4
{
    public static readonly string[] Frost = {
        "Init|",
        "Freeze: Endless Pad|freeze=1;fade=1500;width=70;fz_mix=100;wet=80",
        "Freeze: Dark Drone|freeze=1;fade=2500;hi=-18;mid=-4;lo=4;width=40;wet=90",
        "Freeze: Airy Shimmer|freeze=1;fade=800;lo=-12;hi=6;width=90;fb_on=1;fb_amt=60;fb_ft=2;fb_cut=1500;wet=70",
        "Ring: Robot Bells|rm_on=1;rm_freq=440;rm_mix=70;rm_ft=1;rm_cut=3000",
        "Ring: Wobbly Metal|rm_on=1;rm_freq=120;rm_wave=1;rm_mix=60;lfo_amt=50;lfo_rate=0.3;lfo_dest=0",
        "Ring: Square Grit|rm_on=1;rm_freq=60;rm_wave=2;rm_mix=45;rm_cut=4000",
        "Echo: Ping Pong Space|fb_on=1;fb_time=375;fb_pp=1;fb_amt=65;fb_cut=4000;fb_mix=45",
        "Echo: Tape Wobble|fb_on=1;fb_time=500;fb_amt=55;fb_cut=2500;lfo_amt=30;lfo_rate=0.6;lfo_dest=1;fb_mix=40",
        "Echo: Filter Sweep Delay|fb_on=1;fb_time=250;fb_amt=70;fb_ft=1;fb_res=60;lfo_amt=70;lfo_rate=0.2;lfo_dest=3",
        "Combo: Frozen Ring Echo|freeze=1;fade=1000;rm_on=1;rm_freq=330;rm_mix=40;fb_on=1;fb_amt=60;wet=75",
        "Combo: Parallel Chaos|serial=1;rm_on=1;rm_freq=800;fb_on=1;fb_amt=80;freeze=1;wet=60",
    };
    public static readonly string[] Tape = {
        "Init|",
        "Warm: Gentle Tape|drive=3;dirt=1",
        "Warm: Hot Tape|drive=8;dirt=2",
        "Lo-Fi: Old Cassette|drive=5;dirt=5;lowcut=60",
        "Lo-Fi: Worn VHS|drive=4;dirt=7;stereo=1",
        "Lo-Fi: Broken Machine|drive=7;dirt=10;stereo=1;lowcut=120",
        "Vocal: Vintage Warmth|drive=4;dirt=1.5;lowcut=80",
        "Drums: Squashed Tape|drive=9;dirt=2;out=-3",
        "Bass: Fat Tape|drive=6;dirt=0.5",
        "Keys: Wobbly Rhodes|drive=3;dirt=5.5",
        "FX: Radio Tape|drive=6;dirt=6;lowcut=400",
        "FX: Drop-Out Glitch|drive=4;dirt=9;stereo=1",
    };
    public static readonly string[] Constellation = {
        "Init|",
        "Cloud: Dense Shimmer|num=83;glen=833;maxd=1000;fb=78;width=80;rev=74",
        "Cloud: Sparse Stars|num=8;glen=180;maxd=3000;fb=20;rev=60",
        "Cloud: Octave Choir|l0_on=0;l4_on=0;num=60;glen=600;rev=50",
        "Cloud: Deep Rumble|l0_p=-12;l1_p=-24;l2_p=-12;l3_p=-24;l4_p=0;num=40;glen=900;rev=40",
        "Reverse: Backwards Halo|dir=1;num=50;glen=700;fb=50;rev=60",
        "Reverse: Both Ways|dir=2;num=45;glen=400;fb=40",
        "Rhythm: Stutter Grains|num=20;glen=60;maxd=400;fb=0;rev=10",
        "Vocal: Ghost Harmony|l0_p=12;l1_p=7;l2_p=0;l3_p=-5;l4_p=-12;num=50;glen=500;wet=-6",
        "Texture: Frozen Field|num=96;glen=2000;maxd=4000;fb=90;rev=80",
        "Texture: Glitter|l0_p=24;l1_p=19;l2_p=24;l3_p=12;l4_p=24;num=70;glen=90;maxd=2000",
        "Subtle: Light Sparkle|num=25;glen=300;fb=15;wet=-14;rev=30",
    };
    public static void RegisterAll()
    {
        FxRegistry.Register(new FxRegistry.Info("pxfrost", "Purphex Frost", "Purphex VST", "Spectral freeze with a ring modulator, a feedback delay and an LFO"), () => new FrostFx());
        FxRegistry.Register(new FxRegistry.Info("pxtape", "Purphex Tape Dirt", "Purphex VST", "Worn-out tape machine: drive, dirt (dull, flutter, drop-outs), VU meters"), () => new TapeDirtFx());
        FxRegistry.Register(new FxRegistry.Info("pxwobblelfo", "Purphex Wobble LFO", "Purphex VST", "Makes any sound wobble (made for bass): a tempo-synced LFO opens and closes a filter, and the volume if you like"), () => new WobbleLfoFx());
        FxRegistry.Register(new FxRegistry.Info("pxconst", "Purphex Constellation", "Purphex VST", "Granular cloud on five pitched lanes with feedback and reverb"), () => new ConstellationFx());
        PresetsExtra.Data["pxfrost"] = Frost; PresetsExtra.Data["pxtape"] = Tape; PresetsExtra.Data["pxconst"] = Constellation;
    }
}
