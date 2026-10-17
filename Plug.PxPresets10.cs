using System;
using System.Collections.Generic;

// Factory presets for Purphex VST batch 10, the instruments (Current, Inferno, ChordPad).
static class PxPlugins10
{
    public static readonly string[] Current = {
        "Init|",
        "Lead: Supersaw Anthem|a_tab=0;a_pos=50;a_uni=7;a_det=45;a_spr=100;b_lvl=0;cut=9000;res=15;fenv=20",
        "Lead: Vowel Talker|a_tab=2;a_pos=30;a_uni=1;b_lvl=0;m0_src=2;m0_dst=0;m0_amt=60;lrate=3",
        "Lead: Glide Pulse|a_tab=1;a_pos=60;a_uni=1;b_lvl=0;mono=1;glide=120;cut=5000",
        "Pad: Evolving Sweep|a_tab=3;a_pos=10;b_tab=7;b_pos=40;a=900;d=3000;s=80;r=2500;m0_src=1;m0_dst=0;m0_amt=70;a2=3000;d2=6000;s2=60",
        "Pad: Warm Organ|a_tab=6;a_pos=70;b_lvl=0;a=60;r=600;cut=4000;a_uni=3;a_det=10",
        "Pad: Hollow Choir|a_tab=2;a_pos=50;b_tab=7;b_pos=60;b_lvl=50;a=600;r=1800;a_uni=5;a_det=20;m1_amt=25",
        "Bass: Reese Table|a_tab=0;a_pos=50;a_oct=-1;a_uni=3;a_det=30;b_lvl=0;sub=50;cut=700;res=35;fenv=40;d=400",
        "Bass: Digital Growl|a_tab=4;a_pos=40;a_oct=-1;b_lvl=0;cut=900;res=60;m0_src=2;m0_dst=2;m0_amt=50;lrate=4;drive=50",
        "Pluck: Metallic Bell|a_tab=5;a_pos=40;b_lvl=0;a=1;d=700;s=0;r=500;fenv=60;cut=1500",
        "Pluck: Bright Keys|a_tab=0;a_pos=60;b_tab=1;b_lvl=40;a=1;d=450;s=10;r=300;fenv=50;cut=2000",
        "FX: Rising Table|a_tab=3;a_pos=0;m0_src=1;m0_dst=0;m0_amt=100;a2=4000;s2=100;noise=15;r=1500",
        "Keys: Soft Sine|a_tab=0;a_pos=0;b_lvl=0;a_uni=1;a=5;d=1200;s=40;r=500;cut=8000",
    };
    public static readonly string[] Inferno = {
        "Init|",
        "Dubstep: Classic Wobble|wave=0;ftype=0;rate=3;wob=90;res=60;drive=40",
        "Dubstep: Talking Growl|wave=0;ftype=2;rate=4;wob=85;res=70;drive=55",
        "Dubstep: Slow Yoi|wave=2;ftype=2;rate=2;wob=100;res=65;drive=50",
        "DnB: Reese Roller|wave=0;osc2=90;det=60;ftype=0;cut=600;wob=20;rate=1;drive=30;width=70",
        "DnB: Neuro Fold|wave=3;fm=70;heat=2;drive=70;ftype=1;cut=1200;wob=50;rate=5",
        "Riddim: Square Stab|wave=1;ftype=1;rate=4;wob=60;heat=1;drive=80;r=60",
        "Trap: Gritty Sub Wobble|oct=-2;wave=0;osc2=20;ftype=0;cut=400;wob=50;rate=3;heat=0;drive=30",
        "Hard: Crushed Screech|wave=2;heat=3;drive=75;ftype=2;cut=2500;rate=5;wob=70",
        "Tearout: FM Monster|wave=3;fm=100;heat=1;drive=90;ftype=2;wob=90;rate=6",
        "Smooth: Deep Reese|wave=0;osc2=100;det=40;ftype=0;cut=350;wob=0;drive=15;width=60",
        "Half Time: Big Growl|wave=2;ftype=2;rate=1;wob=95;res=75;drive=60;glide=80",
        "Clean: Filter Bass|wave=0;osc2=0;ftype=0;cut=800;res=40;wob=0;fenv=50;hmix=0",
    };
    public static readonly string[] ChordPad = {
        "Init|",
        "Pop: Bright Keys (C major)|key=0;scale=0;sound=1;prog=0",
        "Emotional: Minor Pad (A minor)|key=9;scale=1;sound=0;prog=1;kind=1;att=400;rel=1500",
        "Trap: Dark Pad (F minor)|key=5;scale=1;sound=0;prog=2;voicing=1;oct=-1;att=200;rel=1200",
        "Soul: 7ths and 9ths (Eb major)|key=3;scale=0;kind=2;voicing=3;sound=1;prog=3",
        "RnB: Smooth 7ths (D dorian)|key=2;scale=2;kind=1;voicing=1;sound=1;prog=3;bright=35",
        "Lofi: Dusty Keys (G major)|key=7;scale=0;kind=1;sound=1;bright=25;strum=40;prog=4",
        "Guitar: Strummed Pluck (E minor)|key=4;scale=1;sound=2;strum=60;prog=1",
        "Rock: Power Chords (E)|key=4;scale=1;kind=4;sound=2;strum=15;prog=5",
        "Cinematic: Wide Spread (C minor)|key=0;scale=1;voicing=2;sound=0;att=900;rel=3000;prog=5",
        "Dreamy: Sus Pad (F major)|key=5;scale=0;kind=3;sound=0;att=600;rel=2000;voicing=1",
        "Drill: Harmonic Minor (G)|key=7;scale=4;sound=1;bright=60;prog=2",
        "Gospel: 9ths Keys (Ab major)|key=8;scale=0;kind=2;voicing=1;sound=1;inv=1;prog=3",
    };
    public static void RegisterAll()
    {
        FxRegistry.Register(new FxRegistry.Info("pxcurrent", "Purphex Current", "Instruments", "Wavetable synth: two morphing oscillators, unison, filter, LFO and mod slots"), () => new CurrentFx());
        FxRegistry.Register(new FxRegistry.Info("pxinferno", "Purphex Inferno", "Instruments", "Growl and wobble bass: reese, growl filter wobbled in time, and HEAT distortion"), () => new InfernoFx());
        FxRegistry.Register(new FxRegistry.Info("pxchord", "Purphex ChordPad", "Instruments", "Chords of any key on eight pads: one finger plays a chord, voicings, strum, write to track"), () => new ChordPadFx());
        PresetsExtra.Data["pxcurrent"] = Current; PresetsExtra.Data["pxinferno"] = Inferno; PresetsExtra.Data["pxchord"] = ChordPad;
    }
}
