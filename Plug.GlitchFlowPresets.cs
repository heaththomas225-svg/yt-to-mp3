using System;
using System.Collections.Generic;

// GlitchFlow factory presets: original settings made for this engine ("Category: Name|settings"); anything not written is the default.
static class GlitchFlowPresets
{
    public static readonly string[] Factory = {
        "Init (clean start)|",
        // subtle
        "Subtle: Gentle Last-Beat Echo|rsync=11;fsync=3;pattern=2;intensity=60;decay=40;mix=45",
        "Subtle: Ghost Repeats|rsync=14;fsync=5;pattern=4;intensity=35;decay=50;mix=35;fade=6",
        "Subtle: Soft Triplet Flutter|rsync=15;fsync=8;pattern=8;intensity=70;length=60;mix=40",
        "Subtle: Last-Beat Lift|rsync=11;fsync=3;pattern=2;mix=55;hp_on=1;hp_cut=300",
        "Subtle: Tape Hiccup|rsync=16;fsync=3;pattern=4;intensity=25;mix=60;drv_on=1;drv_amt=20;fade=4",
        // vocals
        "Vocal: Classic Stutter Hook|rsync=14;fsync=5;fade=2",
        "Vocal: Every-Other Chop|rsync=11;fsync=5;pattern=1;length=70;fade=3",
        "Vocal: Ad-Lib Throw|rsync=11;fsync=3;pattern=2;decay=35;dly_on=1;dly_sync=9;dly_fb=45;dly_mix=35;rev_on=1;rev_mix=25",
        "Vocal: Telephone Stammer|rsync=14;fsync=5;pattern=7;tel_on=1;tel_amt=50",
        "Vocal: Pitch-Down Stutter|rsync=14;fsync=5;pstep=-1;decay=10",
        "Vocal: Reverse Tail|rsync=8;fsync=3;pattern=3;reverse=1;rev_on=1;rev_size=70;rev_mix=35",
        "Vocal: Robot Syllables|rsync=16;fsync=8;length=50;shape=0;mod_on=1;mod_tgt=0;mod_rate=6;mod_depth=60",
        "Vocal: Freeze Pad|rsync=8;fsync=3;freeze=1;rev_on=1;rev_size=90;rev_mix=50;lp_on=1;lp_cut=3500;fade=20",
        // drum fills
        "Drum Fill: 1/16 Roll|rsync=14;fsync=3;pattern=2;fade=1;shape=0",
        "Drum Fill: 1/32 Snare Roll|rsync=16;fsync=3;pattern=2;shape=0;fade=0.5",
        "Drum Fill: Triplet Fill|rsync=15;fsync=3;pattern=2;shape=0",
        "Drum Fill: Half-Bar Machine Gun|rsync=16;fsync=3;pattern=3;drv_on=1;drv_amt=30",
        "Drum Fill: Rising Roll|rsync=14;fsync=3;pattern=3;pstep=1;shape=0",
        // build-ups
        "Build-Up: 4-Bar Riser|rsync=8;fsync=1;pattern=5;pstep=0.5;hp_on=1;hp_cut=200;rev_on=1;rev_mix=20",
        "Build-Up: 2-Bar Snare Rush|rsync=11;fsync=2;pattern=5;shape=0",
        "Build-Up: Filter Climb|rsync=11;fsync=2;pattern=5;lp_on=1;lp_cut=1200;lp_res=55;mod_on=1;mod_tgt=1;mod_rate=0.25;mod_depth=60",
        "Build-Up: Pitch Rocket|rsync=14;fsync=2;pattern=5;pstep=1",
        // transitions
        "Transition: Tape Stop Glide|rsync=8;fsync=3;pitch=-5;pstep=-3;decay=20;lp_on=1;lp_cut=4000",
        "Transition: Slow-Down Drop|rsync=14;fsync=2;pattern=6;pstep=-1",
        "Transition: Reverse Swell|rsync=8;fsync=3;reverse=1;shape=2;rev_on=1;rev_mix=40",
        "Transition: Freeze and Fade|rsync=5;fsync=3;freeze=1;shape=3;fade=40;rev_on=1;rev_mix=45",
        "Transition: Wash Out|rsync=11;fsync=3;pattern=3;dly_on=1;dly_sync=9;dly_fb=70;dly_mix=50;rev_on=1;rev_size=85;rev_mix=45;hp_on=1;hp_cut=600",
        // EDM
        "EDM: Sidechain Stutter|rsync=11;fsync=5;gate_on=1;gate_sync=10;gate_duty=55;gate_depth=90",
        "EDM: Festival Vocal Chop|rsync=14;fsync=5;pattern=9;wid_on=1;width=170;rev_on=1;rev_mix=20",
        "EDM: Big Room Roll|rsync=14;fsync=1;pattern=5;hp_on=1;hp_cut=250;wid_on=1;width=160",
        "EDM: Pumping 1/8 Gate|intensity=0;gate_on=1;gate_sync=10;gate_duty=45;gate_depth=100",
        // drum and bass
        "DnB: Amen Chopper|rsync=16;fsync=8;pattern=4;intensity=80;seed=12;shape=0",
        "DnB: Reese Stutter|rsync=14;fsync=5;pattern=7;drv_on=1;drv_amt=45;lp_on=1;lp_cut=2500;lp_res=40",
        "DnB: Roller Edit|rsync=14;fsync=3;pattern=2;decay=15;dly_on=1;dly_sync=12;dly_fb=35;dly_mix=25",
        "DnB: Jungle Reverse Fill|rsync=11;fsync=3;pattern=2;reverse=1;shape=0",
        // UK garage
        "UKG: Shuffle Skip|rsync=13;fsync=5;pattern=7;length=75;fade=2",
        "UKG: 2-Step Vox Chop|rsync=15;fsync=8;pattern=1;pitch=3;length=60",
        "UKG: Organ Gate|rsync=14;fsync=3;pattern=2;gate_on=1;gate_sync=12;gate_duty=60;gate_depth=85",
        "UKG: Speed Garage Pitch Drop|rsync=14;fsync=5;pattern=2;pstep=-2",
        // trap
        "Trap: Hi-Hat Roll 1/32|rsync=16;fsync=8;pattern=8;shape=0;fade=0.5",
        "Trap: Triplet Flam|rsync=15;fsync=8;pattern=1;shape=0",
        "Trap: 808 Stutter Slide|rsync=14;fsync=5;pattern=2;pstep=-1;lp_on=1;lp_cut=900",
        "Trap: Ad-Lib Echo Stab|rsync=12;fsync=5;pattern=2;decay=45;dly_on=1;dly_sync=12;dly_fb=40;dly_mix=30",
        "Trap: Dark Vocal Glitch|rsync=16;fsync=5;pattern=4;intensity=60;lp_on=1;lp_cut=1800;rev_on=1;rev_mix=25",
        // experimental
        "Experimental: Granular Drift|rsync=16;fsync=11;pattern=4;intensity=70;pitch=7;mod_on=1;mod_tgt=0;mod_rate=0.4;mod_depth=80;rev_on=1;rev_mix=40",
        "Experimental: Broken Radio|rsync=16;fsync=8;pattern=4;tel_on=1;tel_amt=80;drv_on=1;drv_mode=2;drv_amt=50",
        "Experimental: Spiral Pitch|rsync=14;fsync=2;pstep=2;dly_on=1;dly_sync=10;dly_fb=60;dly_mix=40",
        "Experimental: Backwards World|rsync=8;fsync=5;reverse=1;pattern=1;rev_on=1;rev_size=95;rev_mix=50",
        "Experimental: Auto-Pan Shatter|rsync=16;fsync=8;pattern=4;mod_on=1;mod_tgt=2;mod_rate=6;mod_depth=90",
        "Experimental: Frozen Choir|rsync=11;fsync=3;freeze=1;pitch=12;rev_on=1;rev_size=95;rev_mix=60;fade=25;mix=60",
        // aggressive
        "Aggressive: Machine Gun|rsync=16;fsync=5;shape=0;fade=0.3;drv_on=1;drv_mode=1;drv_amt=55",
        "Aggressive: Crushed Riddim|rsync=14;fsync=5;pattern=9;drv_on=1;drv_mode=2;drv_amt=65;gate_on=1;gate_sync=13;gate_depth=70",
        "Aggressive: Shred 1/64|rsync=18;fsync=8;pattern=4;intensity=90;shape=0;drv_on=1;drv_amt=40",
        "Aggressive: Hard Clip Chop|rsync=14;fsync=5;pattern=1;drv_on=1;drv_mode=1;drv_amt=70;hp_on=1;hp_cut=150",
        "Aggressive: Destroyer|rsync=17;fsync=8;pattern=4;pitch=-7;pstep=-2;drv_on=1;drv_mode=2;drv_amt=80;tel_on=1;tel_amt=70",
    };
}
