using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

// Factory presets for the Purphex VSTs ("Category: Name|settings"; anything not written is the default) and how each one is registered and opened.
static class PxPlugins
{
    public static readonly string[] Glow = {
        "Init|",
        "Bass: 808 Weight|l_amt=70;l_chr=0;m_amt=10;h_amt=5;a_amt=0;inject=60",
        "Bass: Punchy Kick|l_amt=65;l_chr=1;m_amt=20;h_amt=15;a_amt=5",
        "Bass: Reese Grit|l_amt=50;m_amt=45;m_chr=1;sat=35;satmode=2",
        "Vocal: Radio Sheen|l_amt=0;m_amt=25;h_amt=55;a_amt=50;a_chr=1;filt=1;hp=90",
        "Vocal: Warm Lead|l_amt=15;m_amt=50;m_chr=0;h_amt=30;a_amt=25;lvl=1",
        "Vocal: Airy Hook|m_amt=10;h_amt=40;h_chr=1;a_amt=70;a_chr=0;ms=1",
        "Vocal: Whisper Detail|m_amt=60;m_chr=1;h_amt=35;a_amt=40",
        "Drums: Crack and Air|l_amt=40;l_chr=1;h_amt=60;h_chr=1;a_amt=45",
        "Drums: Fat Bus|l_amt=55;m_amt=40;sat=25;satmode=1;lvl=1",
        "Mix: Gentle Polish|l_amt=20;m_amt=15;h_amt=20;a_amt=25;inject=40;lvl=1",
        "Mix: Wide Shine|l_amt=15;h_amt=45;a_amt=55;ms=1;lvl=1",
        "Lo-Fi: Crushed Tape|l_amt=40;m_amt=50;h_amt=0;a_amt=0;sat=45;satmode=3;filt=1;lp=6000",
        "Synth: Bright Pluck|m_amt=30;h_amt=60;h_chr=1;a_amt=35",
        "FX: Only the Sparkle|fxonly=1;h_amt=60;a_amt=80;a_chr=1",
    };
    public static readonly string[] Curve = {
        "Init|",
        "Vocal: Clean Lead|b0_on=1;b0_t=3;b0_f=90;b2_on=1;b2_f=300;b2_g=-3;b2_q=1.2;b5_on=1;b5_f=4000;b5_g=2;b7_on=1;b7_t=2;b7_f=12000;b7_g=3",
        "Vocal: Dynamic De-Harsh|b0_on=1;b0_t=3;b0_f=80;b5_on=1;b5_f=3500;b5_q=3;b5_dyn=-6;b5_thr=-30",
        "Vocal: Tame the Esses|b6_on=1;b6_f=7000;b6_q=2.5;b6_dyn=-9;b6_thr=-34",
        "Vocal: Telephone|b0_on=1;b0_t=4;b0_f=400;b7_on=1;b7_t=6;b7_f=3200;b4_on=1;b4_f=1500;b4_g=5",
        "Drums: Kick Click|b0_on=1;b0_t=3;b0_f=30;b1_on=1;b1_f=60;b1_g=3;b3_on=1;b3_f=400;b3_g=-4;b5_on=1;b5_f=4000;b5_g=4;b5_q=1.5",
        "Drums: Snare Body|b2_on=1;b2_f=200;b2_g=3;b4_on=1;b4_f=900;b4_g=-3;b6_on=1;b6_f=6000;b6_g=3",
        "Bass: Tight 808|b0_on=1;b0_t=4;b0_f=28;b1_on=1;b1_f=55;b1_g=2;b3_on=1;b3_f=250;b3_g=-4;b3_q=1.5",
        "Bass: Boom Control|b1_on=1;b1_f=110;b1_q=2;b1_dyn=-6;b1_thr=-20",
        "Mix: Smile|b1_on=1;b1_t=1;b1_f=100;b1_g=3;b7_on=1;b7_t=2;b7_f=10000;b7_g=3;auto=1",
        "Mix: Mud Cut|b2_on=1;b2_f=280;b2_g=-4;b2_q=1;auto=1",
        "Mix: Air Lift|b7_on=1;b7_t=2;b7_f=14000;b7_g=5",
        "Master: Gentle Tilt|b1_on=1;b1_t=1;b1_f=200;b1_g=1.5;b6_on=1;b6_t=2;b6_f=6000;b6_g=1.5;auto=1;scale=0",
        "FX: Lo-Fi Radio|b0_on=1;b0_t=4;b0_f=300;b7_on=1;b7_t=6;b7_f=4500;b3_on=1;b3_f=1200;b3_g=6;b3_q=0.8",
    };
    public static readonly string[] Shape = {
        "Init|",
        "Vocal: Smooth Leveler|ratio=3;thr=-24;att=8;rel=150;knee=10;auto=60",
        "Vocal: Upfront Rap|ratio=6;thr=-28;att=2;rel=80;auto=70;eq_on=1;eq_f=3000;eq_slope=25",
        "Vocal: Pumped Ad-Libs|ratio=10;thr=-34;att=0.5;rel=60;auto=80;cl_on=1;cl_gain=4",
        "Drums: Snappy Snare|ratio=4;thr=-20;att=20;rel=90;tr_on=1;tr_gain=6;tr_time=40",
        "Drums: Smashed Room|ratio=20;thr=-40;fatt=1;rel=50;mix=40;auto=100",
        "Drums: Soft Hats|ratio=2;thr=-18;tr_on=1;tr_gain=-6;tr_time=30",
        "Drums: Kick Punch|ratio=4;thr=-18;att=30;rel=120;tr_on=1;tr_gain=5;cl_on=1;cl_gain=3;cl_soft=60",
        "Bass: 808 Glue|ratio=4;thr=-22;att=10;rel=200;sc_on=1;sc_lc=40;cl_on=1;cl_gain=2",
        "Bass: Even Notes|ratio=6;thr=-30;att=5;rel=250;frel=1;auto=70",
        "Mix: Bus Glue|ratio=2;thr=-16;att=30;rel=300;knee=12;auto=50",
        "Mix: Parallel Squash|ratio=12;thr=-38;att=1;rel=80;mix=35;auto=100",
        "Master: Loud and Clipped|ratio=2;thr=-12;att=20;rel=200;cl_on=1;cl_gain=6;cl_soft=70",
        "FX: De-Ess Listener|ratio=6;thr=-30;sc_on=1;sc_f=7000;sc_g=12;sc_q=2;sc_lc=200",
    };
    public static readonly string[] SubForge = {
        "Init|",
        "808: Classic Long|wave=0;punch=7;ptime=40;d=2500;s=20;r=400;glide=60",
        "808: Short Knock|wave=0;punch=12;ptime=25;d=500;s=0;r=120;glide=0",
        "808: Gritty Slide|wave=0;punch=8;d=2000;s=30;glide=140;d_on=1;d_mode=0;d_drive=45",
        "808: Hard Drill|wave=1;punch=14;ptime=30;d=1500;s=25;glide=90;d_on=1;d_mode=1;d_drive=55;c_on=1",
        "808: Distorted Trap|wave=0;sub_lvl=50;punch=9;d=3000;s=35;d_on=1;d_mode=3;d_drive=60;d_hc=6000;c_on=1;c_thr=-24",
        "808: Deep Sub|wave=0;oct=-1;sub_on=0;punch=4;d=4000;s=60",
        "Bass: Reese|wave=2;cut=900;res=35;fenv=10;d=8000;s=80;r=200;mono=0;d_on=1;d_mode=0;d_drive=25",
        "Bass: Square Sub|wave=3;cut=500;res=10;sub_lvl=40;d=6000;s=70",
        "Bass: Pluck|wave=2;cut=600;res=40;fenv=60;d=350;s=0;r=150;punch=0",
        "Bass: Fold Growl|wave=1;d_on=1;d_mode=2;d_drive=70;cut=2500;res=30;s=80",
        "Bass: UK Garage Organ|wave=3;oct=0;cut=1800;res=20;fenv=30;d=400;s=40;punch=0;sub_on=0",
        "Lead: Sine Whistle|wave=0;oct=1;sub_on=0;punch=0;a=20;d=2000;s=80;glide=120",
        "Lead: Saw Brass|wave=2;oct=1;mono=1;cut=1500;fenv=50;res=20;a=30;d=800;s=60;sub_on=0",
    };

    // every Purphex VST: registered at start, its presets put in the preset system, and its window
    public static void RegisterAll()
    {
        FxRegistry.Register(new FxRegistry.Info("pxglow", "Purphex Glow", "Purphex VST", "Four-band enhancer: low, mid, high and air, wet inject, saturation"), () => new GlowFx());
        FxRegistry.Register(new FxRegistry.Info("pxcurve", "Purphex Curve EQ", "Purphex VST", "8-band dynamic EQ with draggable nodes on a live spectrum"), () => new CurveEqFx());
        FxRegistry.Register(new FxRegistry.Info("pxshape", "Purphex Shape", "Purphex VST", "Compressor and transient shaper with side-chain EQ, EQ and clipper"), () => new ShapeFx());
        FxRegistry.Register(new FxRegistry.Info("pxsubforge", "Purphex SubForge", "Instruments", "808 and bass instrument: punch, sub, glide, filter, distortion, compressor"), () => new SubForgeFx());
        PresetsExtra.Data["pxglow"] = Glow; PresetsExtra.Data["pxcurve"] = Curve; PresetsExtra.Data["pxshape"] = Shape; PresetsExtra.Data["pxsubforge"] = SubForge;
    }
    public static readonly HashSet<string> Types = new HashSet<string> { "glitchflow", "pxglow", "pxcurve", "pxshape", "pxsubforge" };
    // the window of a Purphex VST effect (instruments open their own through ShowWindow)
    public static bool HasWindow(Fx f) { return f is GlitchFlowFx || f is GlowFx || f is CurveEqFx || f is ShapeFx; }
    public static void Open(Fx f, IWin32Window owner)
    {
        if (f is GlitchFlowFx) GlitchFlowWindow.Show((GlitchFlowFx)f, owner);
        else if (f is GlowFx) PxWindow.Show(f, owner, () => new GlowWindow((GlowFx)f));
        else if (f is CurveEqFx) PxWindow.Show(f, owner, () => new CurveEqWindow((CurveEqFx)f));
        else if (f is ShapeFx) PxWindow.Show(f, owner, () => new ShapeWindow((ShapeFx)f));
    }
}
