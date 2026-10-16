using System;
using System.Collections.Generic;

// Factory presets for Purphex VST batch 2 (BreathOff, Phantom Gate, Stereo Lab).
static class PxPlugins2
{
    public static readonly string[] Breath = {
        "Init|",
        "Vocal: Gentle (turn down)|sens=40;gate=-32;red=-12;fout=8;fin=8",
        "Vocal: Clean Rap|sens=35;gate=-30;red=-60;fout=4;fin=4",
        "Vocal: Natural Singer|sens=45;gate=-34;red=-9;fout=12;fin=15",
        "Vocal: Loud Booth|sens=30;gate=-24;red=-60;fout=3;fin=5",
        "Vocal: Quiet Booth|sens=40;gate=-40;red=-60;fout=5;fin=5",
        "Vocal: With Room Tone|sens=35;gate=-30;red=-60;room=1;roomlvl=-60",
        "Podcast: Smooth Talk|sens=38;gate=-34;red=-15;fout=10;fin=10",
        "Podcast: Tight Edit|sens=32;gate=-30;red=-60;fout=3;fin=3;room=1;roomlvl=-64",
        "Check: Hear the Breaths|sens=35;gate=-30;mon=1",
        "Choir: Soft Breaths|sens=50;gate=-36;red=-6;fout=20;fin=20",
        "Voice-Over: Broadcast|sens=33;gate=-28;red=-24;fout=6;fin=6",
    };
    public static readonly string[] Gate = {
        "Init|",
        "Drums: Gated Snare|input=6;spike=73;thr=-16;ret=14;floor=-37;att=0;hold=28;relsync=2;ahead=10",
        "Drums: Tight Kick|thr=-22;ret=6;floor=-60;att=0.2;hold=20;rel=60;ahead=3;sc_on=1;sc_lp=200",
        "Drums: Tom Cleaner|thr=-26;ret=8;floor=-30;hold=60;rel=200;ahead=5;db_on=1;db_f=2500",
        "Drums: Hi-Hat Spill Off|thr=-28;ret=6;floor=-24;att=0.1;hold=10;rel=40;sc_on=1;sc_hp=3000",
        "Drums: 80s Gated Room|thr=-20;ret=4;floor=-80;att=0;hold=180;rel=20;curve=2",
        "Vocal: Clean Pauses|thr=-40;ret=8;floor=-18;att=2;hold=80;rel=200;curve=1",
        "Vocal: Noise Floor Off|thr=-48;ret=6;floor=-40;att=1;hold=120;rel=300;ratio=4",
        "Guitar: Hum Gate|thr=-44;ret=6;floor=-60;att=0.5;hold=40;rel=120",
        "Rhythm: 1/8 Chop|thr=-20;ret=0;floor=-80;hold=0;relsync=2;att=0",
        "Rhythm: 1/16 Trance Gate|thr=-18;ret=0;floor=-70;hold=0;relsync=3;att=0;curve=2",
        "Soft: Gentle Expander|thr=-36;ret=4;floor=-20;ratio=2;att=5;hold=40;rel=250;curve=1",
    };
    public static readonly string[] Stereo = {
        "Init|",
        "Fix: Mono Check|width=0",
        "Fix: Out-of-Phase Right|rph=1",
        "Fix: Tighter Low End|bass=150",
        "Fix: Swap Left and Right|lpan=100;rpan=-100",
        "Wide: Gentle Spread|width=130;bass=120",
        "Wide: Huge Pads|width=180;bass=200;ms=20",
        "Narrow: Focus the Vocal|width=60",
        "Mix: Mid Forward|ms=-30",
        "Mix: Side Air|ms=35;width=120",
        "Move: Lean Left|pan=-35",
        "Morph: Verse to Chorus|ab=1;a_width=80;b_width=170;a_bass=150;b_bass=150;morph=0",
    };
    public static void RegisterAll()
    {
        FxRegistry.Register(new FxRegistry.Info("pxbreath", "Purphex BreathOff", "Purphex VST", "Finds the breaths in a vocal and turns them down, smoothly"), () => new BreathOffFx());
        FxRegistry.Register(new FxRegistry.Info("pxpgate", "Purphex Phantom Gate", "Purphex VST", "Gate for drums and vocals: thresh and return lines, spike, look-ahead, side-chain, de-bleed"), () => new PhantomGateFx());
        FxRegistry.Register(new FxRegistry.Info("pxstereo", "Purphex Stereo Lab", "Purphex VST", "Stereo scope and phase meter with width, pan, mid / side, bass mono and A-B morph"), () => new StereoLabFx());
        PresetsExtra.Data["pxbreath"] = Breath; PresetsExtra.Data["pxpgate"] = Gate; PresetsExtra.Data["pxstereo"] = Stereo;
    }
}
