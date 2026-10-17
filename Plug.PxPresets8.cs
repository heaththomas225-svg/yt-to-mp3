using System;
using System.Collections.Generic;

// Factory presets for Purphex VST batch 8 (EchoGrid, Modal Filter).
static class PxPlugins8
{
    public static readonly string[] EchoGrid = {
        "Init|",
        "Vocal: Classic Ping Pong|e0_step=3;e1_step=6;e2_on=0;e0_pan=-80;e1_pan=80;fb=30;mix=35",
        "Vocal: Dark Throws|e0_step=4;e1_step=8;e0_tone=-60;e1_tone=-75;e2_on=0;fb=25;mix=40",
        "Vocal: Octave Ghosts|e0_step=4;e0_pitch=12;e1_step=8;e1_pitch=-12;e2_on=0;mix=35",
        "Drums: Rolling Hats|grid=2;e0_step=1;e1_step=2;e2_step=3;e3_on=1;e3_step=4;fb=10;mix=40",
        "Drums: Trap Stutter|grid=2;e0_step=2;e1_step=4;e2_step=6;e3_on=1;e3_step=8;e0_crush=40;mix=50",
        "Synth: Rhythmic Gate Echo|e0_step=3;e1_step=5;e2_step=7;e3_on=1;e3_step=11;fb=40;mix=45",
        "FX: Lo-Fi Tape Bounce|e0_tone=-50;e1_tone=-50;e2_tone=-50;e0_drive=40;e1_drive=40;e2_drive=40;e0_crush=25;fb=45;mix=40",
        "FX: Pitch Staircase|e0_pitch=3;e1_on=1;e1_pitch=7;e2_pitch=12;e3_on=1;e3_pitch=-12;e3_step=11;mix=45",
        "FX: Bitcrushed Cloud|e0_crush=70;e1_crush=60;e2_crush=80;e3_on=1;e4_on=1;fb=55;mix=50",
        "Triplet: Swing Echoes|grid=3;e0_step=1;e1_step=2;e2_step=3;fb=30;mix=35",
        "Subtle: Thin Slap|e0_step=1;e1_on=0;e2_on=0;e0_tone=40;e0_lvl=-12;fb=0;mix=25",
    };
    public static readonly string[] ModalFilter = {
        "Init|",
        "Sweep: Slow LFO Low-Pass|a_cut=600;a_res=55;ldepth=60;lrate=0.25",
        "Funk: Auto-Wah|a_type=2;a_cut=500;a_res=70;env=80;att=5;rel=120",
        "Vocal: Talk Box|a_type=6;a_cut=600;a_res=60;b_type=6;b_cut=1400;morph=50;ltarget=3;ldepth=70;lrate=1.5",
        "Bass: Acid Squelch|a_cut=400;a_res=85;a_drive=40;env=70;att=2;rel=180",
        "Pad: Formant Morph|a_type=6;a_cut=500;b_type=6;b_cut=1200;ltarget=3;ldepth=90;lrate=0.2",
        "Metal: Comb Ring|a_type=5;a_cut=220;a_res=85;mix=60",
        "Drums: Band Focus|a_type=2;a_cut=1800;a_res=40;b_type=0;b_cut=6000;route=1",
        "FX: Notch Phaser|a_type=3;a_cut=800;a_res=60;b_type=3;b_cut=2400;route=1;ldepth=50;lrate=0.4",
        "FX: Telephone Twin|a_type=1;a_cut=500;b_type=0;b_cut=3000;route=1;a_res=30;b_res=30",
        "Bright: Peak Sparkle|a_type=4;a_cut=8000;a_res=40",
        "Lofi: Random Steps|a_cut=1500;a_res=50;lshape=3;ldepth=70;lrate=4",
    };
    public static void RegisterAll()
    {
        FxRegistry.Register(new FxRegistry.Info("pxechogrid", "Purphex EchoGrid", "Purphex VST", "Eight echoes on a step grid, each with its own pitch, tone, drive and crush"), () => new EchoGridFx());
        FxRegistry.Register(new FxRegistry.Info("pxmodal", "Purphex Modal Filter", "Purphex VST", "Two morphable filters (seven types) moved by an envelope and an LFO"), () => new ModalFilterFx());
        PresetsExtra.Data["pxechogrid"] = EchoGrid; PresetsExtra.Data["pxmodal"] = ModalFilter;
    }
}
