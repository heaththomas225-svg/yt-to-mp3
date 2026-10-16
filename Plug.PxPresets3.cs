using System;
using System.Collections.Generic;

// Factory presets for Purphex VST batch 3 (Vortex, MultiDyn, Prism).
static class PxPlugins3
{
    public static readonly string[] Vortex = {
        "Init|",
        "Spin: Slow Orbit|sync=2;shape=0;space=25;pan=100;f_mix=60",
        "Spin: 8D Vocal|sync=3;shape=0;space=30;pan=100;w_mix=100;f_mix=80;color=60",
        "Spin: Fast Whirl|sync=6;pan=90;f_mix=40;b_mix=30;range=40",
        "Spin: Figure of Eight|sync=3;shape=100;space=20;pan=100",
        "Move: Car Passing|sync=0;rate=0.15;b_mix=80;range=70;f_mix=90;color=80",
        "Move: Behind You|sync=2;f_mix=100;freq=900;color=90;space=40",
        "Rhythm: Sliced Orbit|sync=4;s_mix=80;time=60;pan=100",
        "Rhythm: Chopped 1/8|sync=5;s_mix=100;time=100;pan=70",
        "Space: Echo Galaxy|sync=2;echo=45;esync=1;space=60;pan=100",
        "Space: Ambient Drift|sync=1;space=80;echo=25;esync=0;f_mix=50;mix=70",
        "Subtle: Gentle Motion|sync=2;pan=35;f_mix=20;color=20;mix=60",
        "Wild: Tornado|sync=7;shape=60;b_mix=60;range=80;s_mix=40;echo=30;space=40",
    };
    public static readonly string[] MultiDyn = {
        "Init|",
        "Mix: Pop Polish|b0_ratio=2;b0_thr=-22;b1_ratio=2;b1_thr=-24;b2_ratio=2.5;b2_thr=-26;b3_ratio=2;b3_thr=-26;b4_ratio=1.5;b4_thr=-28;b4_gain=1.5",
        "Mix: Vocal Pulls Through|b1_ratio=3;b1_thr=-28;b2_ratio=1.5;b2_thr=-20;b3_ratio=3;b3_thr=-30",
        "Mix: Glue All Bands|b0_ratio=1.8;b1_ratio=1.8;b2_ratio=1.8;b3_ratio=1.8;b4_ratio=1.8;knee=12",
        "Master: Gentle Master|b0_ratio=1.6;b1_ratio=1.4;b2_ratio=1.3;b3_ratio=1.4;b4_ratio=1.6;knee=12;limit=1",
        "Master: Loud and Tight|b0_ratio=4;b0_thr=-20;b1_ratio=3;b2_ratio=3;b3_ratio=3;b4_ratio=3;limit=1;out=2",
        "Bass: Even Low End|b0_ratio=4;b0_thr=-26;b0_att=30;b0_rel=200;b1_ratio=2",
        "Vocal: De-Ess Band|b4_ratio=6;b4_thr=-34;b4_att=1;b4_rel=60;x3=6000",
        "Vocal: Warm Vintage|mode=1;b1_ratio=2;b2_ratio=2;b3_ratio=2",
        "Drums: Punchy Bus|b0_ratio=3;b0_att=30;b1_ratio=2;b3_ratio=2;b3_att=10;b4_gain=2",
        "Upward: Lift the Detail|b2_ratio=0.6;b2_thr=-40;b3_ratio=0.6;b3_thr=-40;b4_ratio=0.7;b4_thr=-42",
        "OTT Style: Squashed|b0_ratio=8;b0_thr=-34;b2_ratio=8;b2_thr=-34;b4_ratio=8;b4_thr=-34;b1_ratio=0.5;b1_thr=-50;b3_ratio=0.5;b3_thr=-50;mix=70",
    };
    public static readonly string[] Prism = {
        "Init|",
        "Chord: Resonant Choir|note=35;i1=3;i2=7;i3=10;i3_on=1;unison=5;detune=18;decay=628;mix=33",
        "Chord: Major Bells|note=60;i1=4;i2=7;decay=2000;hfd=16000;mix=40",
        "Chord: Minor Pad|note=57;i1=3;i2=7;i3=12;i3_on=1;decay=4000;blur=60;mix=50",
        "Drums: Tuned Kit|note=40;i1_on=0;i2_on=0;decay=300;mix=35",
        "Drums: Metal Percussion|note=67;i1=6;i2=11;harm=2;decay=900;mix=45",
        "Vocal: Robot Harmony|note=48;i1=7;i2=12;unison=1;decay=200;mix=40",
        "Vocal: Shimmer Ring|note=72;i1=7;i2=12;decay=1500;hfd=18000;blur=40;mix=25",
        "Bass: Sub Ring|note=28;i1=12;i2_on=0;harm=1;decay=800;hfd=3000;mix=30",
        "Texture: Frozen Cloud|note=50;i1=5;i2=10;freeze=1;blur=80;unison=5;detune=40;mix=60",
        "Texture: Odd Glass|note=69;i1=4;i2=9;harm=2;decay=2500;mix=40",
        "Texture: Dark Drone|note=31;i1=7;i2=12;i3=19;i3_on=1;decay=6000;hfd=2000;mix=55",
    };
    public static void RegisterAll()
    {
        FxRegistry.Register(new FxRegistry.Info("pxvortex", "Purphex Vortex", "Purphex VST", "3D auto-panner: rotate, shape, space and echo with five movement sections"), () => new VortexFx());
        FxRegistry.Register(new FxRegistry.Info("pxmultidyn", "Purphex MultiDyn", "Purphex VST", "Five-band compressor with learn, solo, vintage mode and a limiter"), () => new MultiDynFx());
        FxRegistry.Register(new FxRegistry.Info("pxprism", "Purphex Prism", "Purphex VST", "Spectral resonator: any sound rings at notes and chords"), () => new PrismFx());
        PresetsExtra.Data["pxvortex"] = Vortex; PresetsExtra.Data["pxmultidyn"] = MultiDyn; PresetsExtra.Data["pxprism"] = Prism;
    }
}
