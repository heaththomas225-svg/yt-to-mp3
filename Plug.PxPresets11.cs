using System;
using System.Collections.Generic;

// Factory presets for Purphex Drum2MIDI.
static class PxPlugins11
{
    public static readonly string[] Drum2Midi = {
        "Init|",
        "Listen: Clean Loop|sens_k=55;sens_s=55;sens_h=55;quant=2",
        "Listen: Busy Live Kit|sens_k=70;sens_s=70;sens_h=75;quant=0",
        "Listen: Only the Big Hits|sens_k=35;sens_s=35;sens_h=25;quant=2",
        "Listen: Trap Hats|sens_h=85;quant=3",
        "Listen: As Played (no grid)|quant=0;velmode=0",
        "Kit: 808 Boom|k_tune=42;k_punch=40;k_decay=1300;s_tone=200;s_snap=80",
        "Kit: Tight Punch|k_tune=58;k_punch=85;k_decay=250;s_decay=120;h_decay=35",
        "Kit: Lofi Dusty|k_tune=48;k_decay=380;s_tone=170;s_snap=45;h_tone=5500;h_decay=70;level=-5",
        "Kit: Rock Snare|s_tone=230;s_snap=55;s_decay=320;k_tune=55;k_punch=70",
        "Kit: Crisp Hats|h_tone=12000;h_decay=40;h_lvl=-2",
        "Kit: Deep and Soft|k_tune=38;k_punch=20;k_decay=900;s_snap=35;h_lvl=-10;velmode=0",
        "Kit: Even Hits|velmode=1;quant=2",
    };
    public static void RegisterAll()
    {
        FxRegistry.Register(new FxRegistry.Info("pxdrum2midi", "Purphex Drum2MIDI", "Instruments", "Turns a drum track into notes (kick, snare, hats) and plays them with its own kit"), () => new Drum2MidiFx());
        PresetsExtra.Data["pxdrum2midi"] = Drum2Midi;
    }
}
