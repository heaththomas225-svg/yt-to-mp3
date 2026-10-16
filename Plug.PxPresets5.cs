using System;
using System.Collections.Generic;

// Factory presets for Purphex VST batch 5 (Clipper, Smack, Vinyl Stop, Radio).
static class PxPlugins5
{
    public static readonly string[] Clipper = {
        "Init|",
        "Drums: Smash|mode=0;thr=-6.7;soft=100;pre=3",
        "Drums: Hard Snare Clip|mode=1;thr=-4;pre=4;link=1",
        "Bass: 808 Warmth|mode=3;thr=-8;pre=6;link=1",
        "Master: Gentle Peaks|mode=0;thr=-1.5;soft=80",
        "Master: Loud Clip|mode=0;thr=-3;soft=60;pre=4;post=3",
        "Tape: Rounded Glue|mode=2;thr=-6;soft=70;pre=3;link=1",
        "Vocal: Edge|mode=3;thr=-10;pre=8;link=1",
        "FX: Wavefold Fuzz|mode=4;thr=-12;pre=12;link=1",
        "FX: Brick Wall|mode=1;thr=-12;pre=10;link=1",
        "Subtle: Catch the Spikes|mode=0;thr=-0.5;soft=50",
        "Drums: Tape Crunch|mode=2;thr=-9;soft=40;pre=6;link=1",
    };
    public static readonly string[] Smack = {
        "Init|",
        "Drums: Snappy Snare|att=60;sus=-20;adur=20",
        "Drums: Punchy Kick|att=45;sus=10;adur=30;sdur=300",
        "Drums: Tight Room|att=20;sus=-60;sdur=400",
        "Drums: Big Room|att=0;sus=70;sdur=900",
        "Drums: Soft Hats|att=-50;sus=0",
        "Bass: Pluck Bite|att=40;sus=-15",
        "Guitar: Strum Attack|att=35;sus=20",
        "Vocal: Less Plosive|att=-40;sus=10;asens=70",
        "Piano: Gentle|att=-20;sus=25",
        "Loop: Crunchy Punch|att=80;sus=-30;guard=1;out=-2",
        "Drums: Short Tails|att=30;sus=-70;sdur=150",
    };
    public static readonly string[] Vinyl = {
        "Init|",
        "Stop: Classic Turntable|len=4;curve=60;pitch=100;wash=40",
        "Stop: Quick Brake|len=1;curve=20;pitch=100;wash=20",
        "Stop: Long Power-Down|len=5;curve=80;pitch=100;wash=60",
        "Stop: Tape Stop|len=3;curve=50;wash=70;vinyl=0",
        "Stop: Stepped Motor|len=4;step=70;curve=40",
        "Stop: Digital Crash|len=2;digi=80;wash=10",
        "Stop: Dusty Record|len=4;vinyl=1;wash=50",
        "Stop: Half Drop|len=3;pitch=50;wash=30",
        "Stop: Slow Spin-Up|len=2;start=100",
        "Stop: Fast Spin-Up|len=4;start=0",
        "Stop: Dark Wash Out|len=5;wash=100;curve=70",
    };
    public static readonly string[] Radio = {
        "Init|",
        "Radio: Clear Station|tune=50;static=10;bw=50;drive=20;noise=5",
        "Radio: Weak Signal|tune=62;static=40;bw=35;noise=30",
        "Radio: Lost Signal|tune=88;static=70;bw=30;skip=30",
        "Radio: Walkie-Talkie|tune=50;bw=10;drive=70;squelch=40;static=20",
        "Radio: Police Scanner|tune=55;bw=15;drive=60;static=35;crush=20;squelch=30",
        "Radio: Old AM Broadcast|tune=50;bw=25;am=30;noise=40;static=15",
        "Radio: Howling Feedback|tune=45;fb=60;bw=30;drive=40",
        "Radio: Robot Transmission|rm=60;bw=40;crush=40",
        "Radio: Blend in Raw|tune=50;raw=50;bw=30",
        "Radio: Total Breakdown|tune=75;static=80;skip=60;crush=60;fb=40;drive=80",
        "Radio: Crackly Oldies|tune=52;bw=30;noise=50;am=15;drive=25",
    };
    public static void RegisterAll()
    {
        FxRegistry.Register(new FxRegistry.Info("pxclip", "Purphex Clipper", "Purphex VST", "Multi-mode clipper: threshold, softness, pre and post gain"), () => new ClipperFx());
        FxRegistry.Register(new FxRegistry.Info("pxsmack", "Purphex Smack", "Purphex VST", "Transient shaper: attack and sustain, sensitivity, shape, duration, guard"), () => new SmackFx());
        FxRegistry.Register(new FxRegistry.Info("pxvinyl", "Purphex Vinyl Stop", "Purphex VST", "Vinyl and tape stop: length in beats, curve, pitch, step, digi, wash"), () => new VinylStopFx());
        FxRegistry.Register(new FxRegistry.Info("pxradio", "Purphex Radio", "Purphex VST", "Broken radio: tune, static, squelch, bandwidth, drive, AM / RM, bitcrush, skip"), () => new RadioFx());
        PresetsExtra.Data["pxclip"] = Clipper; PresetsExtra.Data["pxsmack"] = Smack; PresetsExtra.Data["pxvinyl"] = Vinyl; PresetsExtra.Data["pxradio"] = Radio;
    }
}
