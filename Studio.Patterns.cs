using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

// =====================================================================================================
//  GENERATE PATTERN: a starting beat in a style (trap, hip-hop, lo-fi, UK drill, drum & bass, house, afrobeats, reggaeton, UK garage,
//  phonk), written as real notes onto YOUR tracks, each by its job: kick, snare or clap, hi-hats, open hat, percussion, 808 or bass.
//  A track's job comes from its sound (a sampler's sample name, the track name: "kick", "808", "hat"...) or from you (JOBS in the menu).
//  A drum kit on one track gets every drum part on its keys. It works with whatever you have: only an 808 and a kick still get their
//  parts (and it tells you which parts would make it fuller). It writes over the bars it fills, in one undo step; ghost notes then
//  show every part while you change them. The 808 / bass follows the kick, in the key picked in the piano roll (minor by default).
// =====================================================================================================
partial class StudioForm
{
    internal class PatStyle { public string Name; public double Bpm, Swing; public string Kick, Snare, Hat, Open, Perc, Family = "", Pack = ""; public int[] Bass; public bool Slide, Held; }   // Family: the group it is listed under; Held: held chords even at a fast tempo (half-time styles)
    // 16 steps per bar, 2 bars: x = a hit, g = a soft (ghost) hit, r = a quick double (two 32nds), . = rest
    internal static readonly PatStyle[] PatStyles = {
        new PatStyle { Name = "Trap", Family = "Hip-Hop & Trap", Bpm = 140, Kick = "x.......x.....x.x.x.......x.....", Snare = "........x...............x.......", Hat = "x.x.x.x.x.x.rrx.x.x.x.x.x.r.rrx.", Open = "..............x...............x.", Perc = "", Bass = new[] { 0, 0, 8, 10 }, Slide = true },
        new PatStyle { Name = "Hip-Hop (boom bap)", Family = "Hip-Hop & Trap", Bpm = 90, Swing = 0.58, Kick = "x......x.x......x......x..x.....", Snare = "....x.......x.......x.......x..g", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.xxx.", Open = "", Perc = "", Bass = new[] { 0, 0, 5, 3 } },
        new PatStyle { Name = "Lo-Fi", Family = "Chill & Lo-Fi", Bpm = 80, Swing = 0.6, Kick = "x.....x...x.....x.....x..x......", Snare = "....x.......x..g....x.......x...", Hat = "x.xgx.xgx.xgx.xgx.xgx.xgx.xgx.xg", Open = "", Perc = "......x.......x.......x.......x.", Bass = new[] { 0, 3, 5, 7 } },
        new PatStyle { Name = "UK Drill", Family = "UK & Drill", Bpm = 142, Kick = "x......x..x.......x....x...x....", Snare = "........x..x............x.......", Hat = "x..x..x..x..x.x.x..x..x..x.rx.x.", Open = "", Perc = "...x...........x...x............", Bass = new[] { 0, 10, 8, 7 }, Slide = true },
        new PatStyle { Name = "Drum & Bass", Family = "Bass & Breaks", Bpm = 174, Kick = "x.........x.....x.....x...x.....", Snare = "....x.......x.......x.......x..g", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.xxx.x.", Open = "......x...............x.........", Perc = "", Bass = new[] { 0, 0, 10, 8 } },
        new PatStyle { Name = "House", Family = "House & Techno", Bpm = 124, Kick = "x...x...x...x...x...x...x...x...", Snare = "....x.......x.......x.......x...", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "..x...x...x...x...x...x...x...x.", Perc = "", Bass = new[] { 0, 0, 5, 7 } },
        new PatStyle { Name = "Afrobeats", Family = "Afro & Latin", Bpm = 104, Swing = 0.55, Kick = "x..x..x...x..x..x..x..x...x..x..", Snare = "...x..x....x..x....x..x....x..x.", Hat = "x.xxx.xxx.xxx.xxx.xxx.xxx.xxx.xx", Open = "", Perc = "..x...x...x.x.x...x...x...x.x.x.", Bass = new[] { 0, 5, 7, 5 } },
        new PatStyle { Name = "Reggaeton", Family = "Afro & Latin", Bpm = 95, Kick = "x...x...x...x...x...x...x...x...", Snare = "...x..x....x..x....x..x....x..x.", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "", Perc = "", Bass = new[] { 0, 0, 7, 5 } },
        new PatStyle { Name = "UK Garage", Family = "UK & Drill", Bpm = 132, Swing = 0.58, Kick = "x.........x..x..x.........x.....", Snare = "....x.......x.......x.......x...", Hat = "..x...x...x...x...x...x...x...x.", Open = "......x...............x.........", Perc = "...x.......x.......x.......x....", Bass = new[] { 0, 0, 3, 5 } },
        new PatStyle { Name = "Phonk", Family = "Hip-Hop & Trap", Bpm = 130, Kick = "x.......x.x.....x.......x.x..x..", Snare = "....x.......x.......x.......x...", Hat = "x.x.x.x.xxx.x.x.x.x.x.x.xxx.x.x.", Open = "", Perc = "x..x..x...x..x..x..x..x...x..x..", Bass = new[] { 0, 0, 8, 7 } },
        new PatStyle { Name = "Melodic Trap", Family = "Hip-Hop & Trap", Pack = "melodic-trap", Bpm = 145, Kick = "x.......x.....x...x.......x.....", Snare = "........x...............x.......", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.rrx.x.", Open = "", Perc = "", Bass = new[] { 0, 8, 3, 7 }, Slide = true, Held = true },
        new PatStyle { Name = "Rage", Family = "Hip-Hop & Trap", Pack = "rage-trap", Bpm = 160, Kick = "x.....x...x.x...x.....x...x...x.", Snare = "........x...............x.......", Hat = "x.x.x.x.x.x.x.x.rrrrx.x.x.x.rrrr", Open = "", Perc = "", Bass = new[] { 0, 0, 10, 7 }, Slide = true, Held = true },
        new PatStyle { Name = "Plugg", Family = "Hip-Hop & Trap", Pack = "plugg", Bpm = 150, Kick = "x.......x.........x.......x.....", Snare = "........x...............x.......", Hat = "x...x...x...x.x.x...x...x...x.x.", Open = "", Perc = "......x...............x.........", Bass = new[] { 0, 5, 3, 7 }, Slide = true, Held = true },
        new PatStyle { Name = "Pluggnb", Family = "R&B & Soul", Pack = "plugg", Bpm = 150, Kick = "x.......x.x.......x.......x.....", Snare = "........x...............x.......", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "..............x...............x.", Perc = "", Bass = new[] { 0, 9, 5, 7 }, Slide = true, Held = true },
        new PatStyle { Name = "Dark Trap", Family = "Hip-Hop & Trap", Pack = "dark-trap", Bpm = 135, Kick = "x.........x.x...x.........x.....", Snare = "........x...............x.......", Hat = "x...x.x.x...x.rrx...x.x.x...x.rr", Open = "", Perc = "...............x...............x", Bass = new[] { 0, 1, 0, 8 }, Slide = true, Held = true },
        new PatStyle { Name = "Detroit", Family = "Hip-Hop & Trap", Pack = "street-trap", Bpm = 160, Kick = "x..x..x...x..x..x..x..x...x.x...", Snare = "....x.......x.......x.......x...", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "", Perc = "", Bass = new[] { 0, 0, 3, 5 } },
        new PatStyle { Name = "Memphis", Family = "Hip-Hop & Trap", Pack = "memphis-phonk", Bpm = 140, Kick = "x.....x...x.....x.....x...x.x...", Snare = "....x.......x.......x.......x...", Hat = "xxx.xxx.xxx.xxx.xxx.xxx.xxx.xxx.", Open = "", Perc = "x...............x...............", Bass = new[] { 0, 0, 3, 1 }, Held = true },
        new PatStyle { Name = "Crunk", Family = "Hip-Hop & Trap", Pack = "atl-trap", Bpm = 80, Kick = "x.......x.x.....x.......x.x.x...", Snare = "....x.......x.......x.......x...", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "", Perc = "", Bass = new[] { 0, 0, 5, 3 }, Held = true },
        new PatStyle { Name = "West Coast (G-Funk)", Family = "Hip-Hop & Trap", Pack = "golden-era", Bpm = 94, Swing = 0.56, Kick = "x.....x...x.....x.....x.x.......", Snare = "....x.......x.......x.......x...", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "..............x...............x.", Perc = "", Bass = new[] { 0, 5, 3, 7 }, Slide = true },
        new PatStyle { Name = "Old School Hip-Hop", Family = "Hip-Hop & Trap", Pack = "golden-era", Bpm = 98, Swing = 0.55, Kick = "x......x..x.....x......x..x.....", Snare = "....x.......x.......x.......x...", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "", Perc = "", Bass = new[] { 0, 0, 3, 5 } },
        new PatStyle { Name = "Jersey Club", Family = "Hip-Hop & Trap", Pack = "jersey-club", Bpm = 140, Kick = "x...x...x.x.x...x...x...x.x.x...", Snare = "....x.......x.......x.......x...", Hat = "..x...x...x...x...x...x...x...x.", Open = "", Perc = "x.......x.......x.......x.......", Bass = new[] { 0, 0, 5, 7 } },
        new PatStyle { Name = "Emo Rap", Family = "Hip-Hop & Trap", Pack = "trap-guitars", Bpm = 140, Kick = "x.......x.....x.x.........x.....", Snare = "........x...............x.......", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "..............x...............x.", Perc = "", Bass = new[] { 0, 8, 3, 10 }, Slide = true, Held = true },
        new PatStyle { Name = "Cloud Rap", Family = "Chill & Lo-Fi", Pack = "cloud-trap", Bpm = 70, Kick = "x.........x.....x.........x.x...", Snare = "........x...............x.......", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "", Perc = "", Bass = new[] { 0, 8, 5, 3 }, Slide = true, Held = true },
        new PatStyle { Name = "Brooklyn Drill", Family = "UK & Drill", Pack = "ny-drill", Bpm = 144, Kick = "x.....x...x.......x.x.....x.....", Snare = "........x..x............x..x....", Hat = "x..x..x..x..x.x.x..x..x..x.rx.x.", Open = "", Perc = "...x...........x...x............", Bass = new[] { 0, 8, 10, 7 }, Slide = true, Held = true },
        new PatStyle { Name = "Chicago Drill", Family = "UK & Drill", Pack = "dark-drill", Bpm = 140, Kick = "x.......x.x.....x.......x...x...", Snare = "........x...............x.......", Hat = "x.x.x.x.rrx.x.x.x.x.x.x.rrx.x.x.", Open = "", Perc = "", Bass = new[] { 0, 0, 8, 7 }, Slide = true, Held = true },
        new PatStyle { Name = "Grime", Family = "UK & Drill", Pack = "grime-essentials", Bpm = 140, Kick = "x.........x.....x.....x...x.....", Snare = "........x...............x.......", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "", Perc = "....x.......x.......x.......x...", Bass = new[] { 0, 0, 1, 0 }, Held = true },
        new PatStyle { Name = "UK Rap", Family = "UK & Drill", Pack = "boom-bap-classics", Bpm = 95, Swing = 0.55, Kick = "x......x..x.....x......x..x.x...", Snare = "....x.......x.......x.......x...", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "", Perc = "", Bass = new[] { 0, 3, 5, 3 }, Held = true },
        new PatStyle { Name = "Afroswing", Family = "UK & Drill", Pack = "afro-swing", Bpm = 102, Swing = 0.55, Kick = "x..x..x...x.....x..x..x...x.....", Snare = "....x.......x.......x.......x...", Hat = "x.xxx.xxx.xxx.xxx.xxx.xxx.xxx.xx", Open = "", Perc = "..x...x.......x...x...x.......x.", Bass = new[] { 0, 5, 3, 5 }, Held = true },
        new PatStyle { Name = "Bassline", Family = "UK & Drill", Pack = "bass-house-club", Bpm = 136, Kick = "x...x...x...x...x...x...x...x...", Snare = "....x.......x.......x.......x...", Hat = "..x...x...x...x...x...x...x...x.", Open = "", Perc = "x..x..x.x..x..x.x..x..x.x..x..x.", Bass = new[] { 0, 0, 3, 5 } },
        new PatStyle { Name = "R&B", Family = "R&B & Soul", Pack = "rnb-essentials", Bpm = 95, Swing = 0.56, Kick = "x......x..x.....x.........x.....", Snare = "....x.......x.......x.......x..g", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.xxx.x.", Open = "", Perc = "", Bass = new[] { 0, 5, 9, 7 }, Slide = true, Held = true },
        new PatStyle { Name = "Trap Soul", Family = "R&B & Soul", Pack = "trap-soul", Bpm = 68, Kick = "x.........x.x...x.........x.....", Snare = "........x...............x.......", Hat = "x.x.x.x.x.x.rrx.x.x.x.x.x.x.rrx.", Open = "", Perc = "", Bass = new[] { 0, 9, 5, 7 }, Slide = true, Held = true },
        new PatStyle { Name = "Neo Soul", Family = "R&B & Soul", Pack = "neo-soul", Bpm = 88, Swing = 0.6, Kick = "x.....x...x.....x.....x..x......", Snare = "....x.......x..g....x.......x..g", Hat = "x.xgx.xgx.xgx.xgx.xgx.xgx.xgx.xg", Open = "", Perc = "", Bass = new[] { 0, 5, 2, 7 }, Held = true },
        new PatStyle { Name = "Funk", Family = "R&B & Soul", Pack = "breakbeat-funk", Bpm = 108, Swing = 0.54, Kick = "x.....x.x.x.....x.....x.x...x...", Snare = "....x..g....x.g.....x..g....x...", Hat = "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", Open = "..............x...............x.", Perc = "", Bass = new[] { 0, 0, 5, 7 } },
        new PatStyle { Name = "Gospel", Family = "R&B & Soul", Pack = "slow-jams", Bpm = 76, Swing = 0.6, Kick = "x.......x.x.....x.......x.....x.", Snare = "....x.......x.......x.......x...", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "", Perc = "", Bass = new[] { 0, 5, 7, 5 }, Held = true },
        new PatStyle { Name = "Pop", Family = "Pop & Rock", Pack = "pop-essentials", Bpm = 118, Kick = "x.......x.x.....x.......x.x.....", Snare = "....x.......x.......x.......x...", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "", Perc = "", Bass = new[] { 0, 7, 9, 5 }, Held = true },
        new PatStyle { Name = "Dance Pop", Family = "Pop & Rock", Pack = "dance-pop", Bpm = 124, Kick = "x...x...x...x...x...x...x...x...", Snare = "....x.......x.......x.......x...", Hat = "..x...x...x...x...x...x...x...x.", Open = "..x...x...x...x...x...x...x...x.", Perc = "", Bass = new[] { 0, 7, 9, 5 } },
        new PatStyle { Name = "Synthwave", Family = "Pop & Rock", Pack = "80s-pop", Bpm = 100, Kick = "x.......x.......x.......x.......", Snare = "....x.......x.......x.......x...", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "", Perc = "", Bass = new[] { 0, 0, 8, 10 } },
        new PatStyle { Name = "Disco", Family = "Pop & Rock", Pack = "disco-pop", Bpm = 118, Kick = "x...x...x...x...x...x...x...x...", Snare = "....x.......x.......x.......x...", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "..x...x...x...x...x...x...x...x.", Perc = "", Bass = new[] { 0, 0, 5, 7 } },
        new PatStyle { Name = "Rock", Family = "Pop & Rock", Pack = "indie-pop", Bpm = 120, Kick = "x.......x.x.....x.......x.x.....", Snare = "....x.......x.......x.......x...", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "", Perc = "", Bass = new[] { 0, 0, 5, 7 }, Held = true },
        new PatStyle { Name = "Pop Punk", Family = "Pop & Rock", Pack = "indie-pop", Bpm = 172, Kick = "x.....x.x.....x.x.....x.x.....x.", Snare = "....x.......x.......x.......x...", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "", Perc = "", Bass = new[] { 0, 7, 9, 5 }, Held = true },
        new PatStyle { Name = "Hyperpop", Family = "Pop & Rock", Pack = "hyperpop", Bpm = 160, Kick = "x.....x...x.x...x.....x...x.x.x.", Snare = "....x.......x.......x.......x...", Hat = "xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx", Open = "", Perc = "", Bass = new[] { 0, 7, 4, 9 }, Slide = true },
        new PatStyle { Name = "Dancehall", Family = "Afro & Latin", Pack = "dancehall-essentials", Bpm = 100, Kick = "x..x..x...x.....x..x..x...x.....", Snare = "...x..x.......x....x..x.......x.", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "", Perc = "", Bass = new[] { 0, 0, 5, 7 }, Held = true },
        new PatStyle { Name = "Amapiano", Family = "Afro & Latin", Pack = "amapiano-essentials", Bpm = 112, Swing = 0.55, Kick = "x...x...x...x...x...x...x...x...", Snare = "............x...............x...", Hat = "..x...x...x...x...x...x...x...x.", Open = "", Perc = "x..x..x...x...x.x..x..x...x...x.", Bass = new[] { 0, 0, 5, 3 }, Slide = true, Held = true },
        new PatStyle { Name = "Afro House", Family = "Afro & Latin", Pack = "afro-house", Bpm = 122, Kick = "x...x...x...x...x...x...x...x...", Snare = "....x.......x.......x.......x...", Hat = "x.xxx.xxx.xxx.xxx.xxx.xxx.xxx.xx", Open = "..x...x...x...x...x...x...x...x.", Perc = "..x..x....x..x....x..x....x..x..", Bass = new[] { 0, 0, 3, 5 } },
        new PatStyle { Name = "Baile Funk", Family = "Afro & Latin", Pack = "baile-funk", Bpm = 130, Kick = "x..x..x.x..x..x.x..x..x.x..x..x.", Snare = "....x.......x.......x.......x...", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "", Perc = "...x..x....x..x....x..x....x..x.", Bass = new[] { 0, 0, 1, 0 } },
        new PatStyle { Name = "Moombahton", Family = "Afro & Latin", Pack = "moombahton", Bpm = 108, Kick = "x...x...x...x...x...x...x...x...", Snare = "...x..x....x..x....x..x....x..x.", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "", Perc = "", Bass = new[] { 0, 0, 8, 7 } },
        new PatStyle { Name = "Dembow", Family = "Afro & Latin", Pack = "perreo", Bpm = 115, Kick = "x...x...x...x...x...x...x...x...", Snare = "...x..x....x..x....x..x....x..x.", Hat = "..x...x...x...x...x...x...x...x.", Open = "", Perc = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Bass = new[] { 0, 0, 5, 7 } },
        new PatStyle { Name = "Techno", Family = "House & Techno", Pack = "techno-essentials", Bpm = 130, Kick = "x...x...x...x...x...x...x...x...", Snare = "....x.......x.......x.......x...", Hat = "..x...x...x...x...x...x...x...x.", Open = "..x...x...x...x...x...x...x...x.", Perc = "...x.......x.......x.......x....", Bass = new[] { 0, 0, 0, 1 } },
        new PatStyle { Name = "Tech House", Family = "House & Techno", Pack = "tech-house-grooves", Bpm = 126, Swing = 0.54, Kick = "x...x...x...x...x...x...x...x...", Snare = "....x.......x.......x.......x...", Hat = "..x...x...x...x...x...x...x...x.", Open = "..x...x...x...x...x...x...x...x.", Perc = ".x...x.x.x...x...x...x.x.x...x..", Bass = new[] { 0, 0, 0, 3 } },
        new PatStyle { Name = "Deep House", Family = "House & Techno", Pack = "deep-house", Bpm = 120, Swing = 0.55, Kick = "x...x...x...x...x...x...x...x...", Snare = "....x.......x.......x.......x...", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "..x...x...x...x...x...x...x...x.", Perc = "", Bass = new[] { 0, 0, 5, 7 } },
        new PatStyle { Name = "Trance", Family = "House & Techno", Pack = "trance-essentials", Bpm = 138, Kick = "x...x...x...x...x...x...x...x...", Snare = "....x.......x.......x.......x...", Hat = "..x...x...x...x...x...x...x...x.", Open = "..x...x...x...x...x...x...x...x.", Perc = "", Bass = new[] { 0, 8, 3, 10 } },
        new PatStyle { Name = "Hardstyle", Family = "House & Techno", Pack = "hardstyle-style", Bpm = 150, Kick = "x...x...x...x...x...x...x...x...", Snare = "....x.......x.......x.......x...", Hat = "..x...x...x...x...x...x...x...x.", Open = "", Perc = "", Bass = new[] { 0, 0, 0, 0 } },
        new PatStyle { Name = "Dubstep", Family = "Bass & Breaks", Pack = "melodic-dubstep", Bpm = 140, Kick = "x.........x.........x...........", Snare = "........x...............x.......", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "", Perc = "", Bass = new[] { 0, 0, 1, 0 }, Slide = true, Held = true },
        new PatStyle { Name = "Riddim", Family = "Bass & Breaks", Pack = "colour-bass", Bpm = 150, Kick = "x.....x.......x.x.....x.......x.", Snare = "........x...............x.......", Hat = "x...x...x...x...x...x...x...x...", Open = "", Perc = "", Bass = new[] { 0, 0, 0, 0 }, Held = true },
        new PatStyle { Name = "Future Bass", Family = "Bass & Breaks", Pack = "future-bass", Bpm = 150, Kick = "x.........x.x...x.........x.....", Snare = "........x...............x.......", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "", Perc = "", Bass = new[] { 0, 9, 5, 7 }, Slide = true, Held = true },
        new PatStyle { Name = "Jungle", Family = "Bass & Breaks", Pack = "dnb-rollers", Bpm = 170, Kick = "x.........x.x...x.....x.........", Snare = "....x..g.x..x..g....x..g.x..x..g", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "", Perc = "", Bass = new[] { 0, 0, 5, 3 }, Held = true },
        new PatStyle { Name = "Liquid DnB", Family = "Bass & Breaks", Pack = "dnb-rollers", Bpm = 174, Kick = "x.........x.....x.........x.....", Snare = "....x.......x.......x.......x...", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "..............x...............x.", Perc = "", Bass = new[] { 0, 9, 5, 7 }, Held = true },
        new PatStyle { Name = "Breakbeat", Family = "Bass & Breaks", Pack = "breakbeat-funk", Bpm = 130, Kick = "x.........x.x.....x.......x.....", Snare = "....x.......x.......x..x....x...", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "", Perc = "", Bass = new[] { 0, 0, 3, 5 } },
        new PatStyle { Name = "Footwork", Family = "Bass & Breaks", Pack = "club-edits", Bpm = 160, Kick = "x..x..x...x..x..x..x..x...x..x..", Snare = "........x...............x.......", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "", Perc = "x.....x.....x...x.....x.....x...", Bass = new[] { 0, 0, 3, 0 }, Held = true },
        new PatStyle { Name = "Drift Phonk", Family = "Bass & Breaks", Pack = "drift-phonk", Bpm = 120, Kick = "x.....x...x.....x.....x...x.x...", Snare = "....x.......x.......x.......x...", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "", Perc = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Bass = new[] { 0, 0, 1, 0 }, Slide = true, Held = true },
        new PatStyle { Name = "Jazz Hop", Family = "Chill & Lo-Fi", Pack = "jazz-hop", Bpm = 86, Swing = 0.62, Kick = "x.....x...x.....x.....x..x......", Snare = "....x.......x..g....x.......x...", Hat = "x.xgx.xgx.xgx.xgx.xgx.xgx.xgx.xg", Open = "", Perc = "", Bass = new[] { 0, 5, 2, 7 }, Held = true },
        new PatStyle { Name = "Chillhop", Family = "Chill & Lo-Fi", Pack = "chillhop", Bpm = 84, Swing = 0.58, Kick = "x.......x.x.....x.....x...x.....", Snare = "....x.......x.......x.......x...", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "", Perc = "......x.......x.......x.......x.", Bass = new[] { 0, 5, 3, 7 }, Held = true },
        new PatStyle { Name = "Ambient", Family = "Chill & Lo-Fi", Pack = "ambient-chill", Bpm = 70, Kick = "x...............x...............", Snare = "........................x.......", Hat = "x.......x.......x.......x.......", Open = "", Perc = "", Bass = new[] { 0, 5, 0, 7 }, Held = true },
        new PatStyle { Name = "Bossa Nova", Family = "Chill & Lo-Fi", Pack = "jazzy-lofi", Bpm = 130, Kick = "x..x..x.x..x..x.x..x..x.x..x..x.", Snare = "x..x..x...x..x..x..x..x...x..x..", Hat = "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", Open = "", Perc = "", Bass = new[] { 0, 5, 2, 7 }, Held = true } };
    internal static readonly string[] Jobs = { "Kick", "Snare / clap", "Hi-hats", "Open hat", "Percussion", "808 / bass" };
    internal readonly Dictionary<Track, int> prJobs = new Dictionary<Track, int>();   // jobs you set yourself (they win over the guess)

    // the job of a track: what you set, else a guess from its sound and name (-1: not a drum or bass part)
    internal int JobOf(Track t)
    {
        int j; if (prJobs.TryGetValue(t, out j)) return j; if (!PrCanEdit(t)) return -1; string h = t.Name.ToLowerInvariant();
        foreach (var f in t.Fx) { var s = f as SamplerFx; if (s != null) { if (s.IsKit) return 100; foreach (var z in s.Zones) h += " " + z.Name.ToLowerInvariant() + " " + System.IO.Path.GetFileNameWithoutExtension(z.Path).ToLowerInvariant(); } }
        // whole words only ("custom" is not a tom, "subtle" is not a sub), and the drums win over the bass ("Sub Kick" is a kick)
        return JobFromWords(h);
    }
    static int JobFromWords(string h)
    {
        h = System.Text.RegularExpressions.Regex.Replace(h.ToLowerInvariant(), "[^a-z0-9]+", " "); Func<string, bool> w = x => (" " + h + " ").Contains(" " + x + " ");
        if (w("kick") || w("kicks") || w("bd")) return 0; if (w("snare") || w("clap") || w("snap") || w("rim crack")) return 1;
        if (w("open hat") || w("openhat") || w("hat open")) return 3; if (w("hat") || w("hats") || w("hihat") || w("hh") || w("hi hat")) return 2;
        if (w("808") || w("bass") || w("sub")) return 5;
        if (w("perc") || w("percussion") || w("shaker") || w("conga") || w("tom") || w("rim") || w("bell") || w("cowbell")) return 4;
        return -1;
    }
    static readonly int[] KitKeys = { 36, 38, 42, 46, 54 };   // a kit track: kick, snare, closed hat, open hat, percussion (General MIDI keys)

    // write the pattern; returns what it did (or why it could not)
    // only: just that track gets its part (nothing is written on the others); a track that is not a drum or bass gets chords in the key
    internal string GeneratePattern(int styleIndex, int bars, int fromBar, bool setTempo, Track only = null)
    {
        var st = PatStyles[styleIndex]; var parts = new Dictionary<int, List<Track>>(); var kits = new List<Track>();
        if (only != null && JobOf(only) == -1) return GenerateChords(st, only, bars, fromBar, setTempo);
        foreach (var t in eng.Tracks) { if (only != null && t != only) continue; int j = JobOf(t); if (j == 100) kits.Add(t); else if (j >= 0) { List<Track> l; if (!parts.TryGetValue(j, out l)) parts[j] = l = new List<Track>(); l.Add(t); } }
        if (parts.Count == 0 && kits.Count == 0) return "Add your sounds first: put a kick, a snare or clap, hi-hats and an 808 on tracks 1, 2, 3, 4... (drag a sound from SOUNDS onto the piano roll, then drag your pattern onto the song), then generate again. Or use the GENERATE box: it brings its own sounds. A track's job comes from its sound or name (JOBS sets it yourself).";
        PushUndo(); if (setTempo) { Fx.ProjectBpm = st.Bpm; bpmUserSet = true; }
        double beat = StudioEngine.SR * 60.0 / Fx.ProjectBpm, step = beat / 4; long a = (long)Math.Round(fromBar * 4 * beat), len = (long)Math.Round(bars * 4 * beat);
        Func<string, string> pat = j => j == "" ? "" : j;
        Action<Track, string, int, int, bool> write = (t, p, note, vel, swingOk) =>
        {
            if (string.IsNullOrEmpty(p)) return; var notes = NotesOf(t); notes.RemoveAll(n => n.Start >= a && n.Start < a + len && (JobOf(t) != 100 || n.Note == note));
            int steps = bars * 16;
            for (int s = 0; s < steps; s++)
            {
                char c = p[s % p.Length]; if (c == '.') continue; double sw = swingOk && st.Swing > 0.5 && s % 2 == 1 ? (st.Swing - 0.5) * 2 * step : 0; long at = a + (long)Math.Round(s * step + sw);
                int v = c == 'g' ? vel * 55 / 100 : (c == 'r' ? vel * 80 / 100 : vel); if (p == st.Hat && c == 'x') v = s % 2 == 0 ? vel : vel * 75 / 100;
                notes.Add(new PrNote { Start = at, Len = (long)(step * 0.9), Note = note, Vel = Math.Max(1, Math.Min(127, v)) });
                if (c == 'r') notes.Add(new PrNote { Start = at + (long)(step / 2), Len = (long)(step * 0.45), Note = note, Vel = Math.Max(1, v * 85 / 100) });
            }
            WriteNotes(t, notes);
        };
        var done = new List<string>(); string[] P = { st.Kick, st.Snare, st.Hat, st.Open, st.Perc };
        for (int j = 0; j < 5; j++) { List<Track> l; if (!parts.TryGetValue(j, out l)) continue; if (string.IsNullOrEmpty(P[j])) continue; foreach (var t in l) write(t, P[j], DrumNote(t), j == 2 ? 100 : 112, j >= 2); done.Add(Jobs[j]); }
        foreach (var k in kits) { int kj = only != null ? JobFromWords(k.Name) : -1; for (int j = 0; j < 5; j++) if (!string.IsNullOrEmpty(P[j]) && (kj < 0 || kj > 4 || j == kj)) write(k, P[j], KitKeys[j], j == 2 ? 100 : 112, j >= 2); done.Add("the kit on " + k.Name); }
        // the 808 / bass: on the kick's rhythm, notes from the key, each held until the next
        List<Track> bassT; if (parts.TryGetValue(5, out bassT))
        {
            foreach (var t in bassT)
            {
                var notes = NotesOf(t); notes.RemoveAll(n => n.Start >= a && n.Start < a + len); var hits = new List<int>(); for (int s = 0; s < bars * 16; s++) if (st.Kick[s % st.Kick.Length] == 'x') hits.Add(s);
                int baseNote = BassRoot(t); bool minor = prScale != 2 && prScale != 7;
                for (int i = 0; i < hits.Count; i++)
                {
                    int s = hits[i], next = i + 1 < hits.Count ? hits[i + 1] : bars * 16; int barIx = s / 16; int deg = st.Bass[barIx % st.Bass.Length]; if (!minor && (deg == 3 || deg == 8 || deg == 10)) deg++;
                    int n = baseNote + deg; if (st.Slide && i % 4 == 3) n += 12;   // (trap and drill: an octave jump now and then)
                    notes.Add(new PrNote { Start = a + (long)Math.Round(s * step), Len = (long)Math.Round((next - s) * step * 0.95), Note = n, Vel = 110 });
                }
                WriteNotes(t, notes);
            }
            done.Add(Jobs[5]);
        }
        prStamp = -1; if (only != null) return st.Name + " " + (JobOf(only) == 100 ? "beat" : Jobs[JobOf(only)].ToLowerInvariant()) + " written on " + only.Name + " only (bars " + (fromBar + 1) + "-" + (fromBar + bars) + (setTempo ? ", " + st.Bpm + " BPM" : "") + "). Open another track and do the same to build it up.";
        var missing = new List<string>(); if (!parts.ContainsKey(0) && kits.Count == 0) missing.Add("a kick"); if (!parts.ContainsKey(1) && kits.Count == 0) missing.Add("a snare or clap"); if (!parts.ContainsKey(2) && kits.Count == 0) missing.Add("hi-hats"); if (!parts.ContainsKey(5)) missing.Add("an 808");
        return st.Name + " pattern written for " + string.Join(", ", done) + " (bars " + (fromBar + 1) + "-" + (fromBar + bars) + (setTempo ? ", " + st.Bpm + " BPM" : "") + ")." + (missing.Count > 0 ? " Add " + string.Join(", ", missing) + " to make it fuller." : "");
    }
    // a piano, lead or pad: the style's chord movement (the same roots its 808 plays) as chords in the piano roll's key, in the style's rhythm
    string GenerateChords(PatStyle st, Track t, int bars, int fromBar, bool setTempo)
    {
        PushUndo(); if (setTempo) { Fx.ProjectBpm = st.Bpm; bpmUserSet = true; }
        double beat = StudioEngine.SR * 60.0 / Fx.ProjectBpm, step = beat / 4; long a = (long)Math.Round(fromBar * 4 * beat), len = (long)Math.Round(bars * 4 * beat);
        var notes = NotesOf(t); notes.RemoveAll(n => n.Start >= a && n.Start < a + len); bool minor = prScale != 2 && prScale != 7;
        int[] sc = minor ? new[] { 0, 2, 3, 5, 7, 8, 10 } : new[] { 0, 2, 4, 5, 7, 9, 11 }; int root = 48 + prKeyRoot; if (root > 54) root -= 12;
        bool stabs = st.Bpm >= 120 && !st.Held && st.Name != "Drum & Bass" && st.Name != "Liquid DnB" && st.Name != "Jungle";   // fast dance styles: off-beat stabs; the rest: held chords
        for (int bar = 0; bar < bars; bar++)
        {
            int deg = st.Bass[bar % st.Bass.Length]; if (!minor && (deg == 3 || deg == 8 || deg == 10)) deg++;
            int di = 0; for (int i = 0; i < 7; i++) if (sc[i] == deg) di = i;   // the scale step of the root
            var chord = new List<int>(); for (int k = 0; k < 3; k++) { int ii = di + k * 2; chord.Add(root + sc[ii % 7] + 12 * (ii / 7)); }
            long b0 = a + (long)Math.Round(bar * 16 * step);
            if (stabs) { foreach (int s0 in new[] { 2, 6, 10, 14 }) foreach (int n in chord) notes.Add(new PrNote { Start = b0 + (long)Math.Round(s0 * step), Len = (long)(step * 1.5), Note = n, Vel = 96 }); }
            else foreach (int n in chord) notes.Add(new PrNote { Start = b0, Len = (long)Math.Round(16 * step * 0.97), Note = n, Vel = 92 });
        }
        WriteNotes(t, notes); prStamp = -1;
        return st.Name + " chords written on " + t.Name + " only (bars " + (fromBar + 1) + "-" + (fromBar + bars) + (setTempo ? ", " + st.Bpm + " BPM" : "") + ", in the key picked in the piano roll). Open another track and do the same to build it up.";
    }
    // the note a drum track's hits use (a fixed-pitch sample plays the same on any key: its root, so it shows on one row)
    int DrumNote(Track t) { foreach (var f in t.Fx) { var s = f as SamplerFx; if (s != null && s.Zones.Count > 0) return s.Zones[0].Follow ? s.Zones[0].Root : (s.Zones[0].Lo == s.Zones[0].Hi ? s.Zones[0].Lo : 60); } return 60; }
    // the 808 / bass key: the sampler's root (an 808 sample tuned to its key) moved to the piano roll's key, low
    int BassRoot(Track t) { int r = 24 + prKeyRoot; foreach (var f in t.Fx) { var s = f as SamplerFx; if (s != null && s.Zones.Count > 0 && s.Zones[0].Follow) { r = s.Zones[0].Root - ((s.Zones[0].Root - 24) % 12) + prKeyRoot; while (r > 40) r -= 12; } } return r; }
    internal static void WriteNotes(Track t, List<PrNote> notes)
    {
        NoOverlap(notes); var ev = new List<MidiEv>(); foreach (var n in notes) { int note = Math.Max(0, Math.Min(127, n.Note)); ev.Add(new MidiEv { Frame = Math.Max(0, n.Start), S = 0x90, D1 = (byte)note, D2 = (byte)Math.Max(1, Math.Min(127, n.Vel)) }); ev.Add(new MidiEv { Frame = Math.Max(0, n.Start) + Math.Max(1, n.Len), S = 0x80, D1 = (byte)note, D2 = 0 }); }
        ev.Sort((x, y) => { int c = x.Frame.CompareTo(y.Frame); return c != 0 ? c : x.S.CompareTo(y.S); }); lock (t.Midi) { t.Midi.Clear(); t.Midi.AddRange(ev); }
    }
    // the styles are listed in their families (Hip-Hop & Trap, UK & Drill, R&B & Soul...): a sub-menu each, in the order they first come
    static ToolStripMenuItem Fam(List<ToolStripMenuItem> fams, string name) { var f = fams.Find(x => x.Name == name); if (f == null) { f = new ToolStripMenuItem(name) { Name = name }; fams.Add(f); } return f; }
    // the GENERATE menu: styles, how many bars, the song's tempo, and each track's job
    void PrGenerateMenu()
    {
        var m = Win.NewMenu(); int from = (int)Math.Floor(prScrollBeats / 4);
        m.Items.Add(new ToolStripMenuItem("ONE SOUND AT A TIME") { Enabled = false });
        if (prTrack != null && PrCanEdit(prTrack))
        {   // just the track you are on: its own part with its own sound (no claps or other sounds added)
            var me = prTrack; int jm = JobOf(me); string what = jm == 100 ? "its drum kit" : (jm >= 0 ? Jobs[jm].ToLowerInvariant() : "chords");
            var just = new ToolStripMenuItem("JUST THIS TRACK: " + me.Name + " (" + what + ")") { Font = new Font(m.Font, FontStyle.Bold) }; m.Items.Add(just);
            var fj = new List<ToolStripMenuItem>(); for (int i = 0; i < PatStyles.Length; i++)
            {
                int k = i; var it = new ToolStripMenuItem(PatStyles[i].Name + "   (" + PatStyles[i].Bpm + " BPM)");
                foreach (int bars0 in new[] { 4, 8, 16 }) { int bars = bars0; it.DropDownItems.Add(bars + " bars from bar " + (from + 1)).Click += (a, b) => { bool tempo = Math.Abs(Fx.ProjectBpm - PatStyles[k].Bpm) > 0.5 && AskYes("Set the song to " + PatStyles[k].Bpm + " BPM (the usual " + PatStyles[k].Name + " tempo)? No keeps " + Fx.ProjectBpm.ToString("0.##") + " BPM.", "Generate"); Toast(GeneratePattern(k, bars, from, tempo, me)); }; }
                Fam(fj, PatStyles[k].Family).DropDownItems.Add(it);
            }
            foreach (var f in fj) just.DropDownItems.Add(f);
        }
        // one new sound in the box: only that part (a kick is only a kick, no hats come with it)
        for (int j0 = 0; j0 < Jobs.Length; j0++)
        {
            int jj = j0; var one = new ToolStripMenuItem("A new " + Jobs[j0].ToLowerInvariant() + " on its own (in the box, with its own sound)"); var fo = new List<ToolStripMenuItem>();
            for (int i = 0; i < PatStyles.Length; i++)
            {
                int k = i; if (jj < 5 && string.IsNullOrEmpty(new[] { PatStyles[i].Kick, PatStyles[i].Snare, PatStyles[i].Hat, PatStyles[i].Open, PatStyles[i].Perc }[jj])) continue;
                var it = new ToolStripMenuItem(PatStyles[i].Name + "   (" + PatStyles[i].Bpm + " BPM)");
                foreach (int bars0 in new[] { 4, 8, 16 }) { int bars = bars0; it.DropDownItems.Add(bars + " bars").Click += (a, b) => { prBox = BuildPattern(k, bars, jj); Toast(PatStyles[k].Name + " " + Jobs[jj].ToLowerInvariant() + " on its own, ready in the box at the top right of the piano roll: drag it onto the song, or press ADD TO SONG."); }; }
                Fam(fo, PatStyles[k].Family).DropDownItems.Add(it);
            }
            foreach (var f in fo) one.DropDownItems.Add(f); m.Items.Add(one);
        }
        m.Items.Add(new ToolStripSeparator());
        var whole = new ToolStripMenuItem("A whole beat in the box: kick, snare, hats and 808 together"); m.Items.Add(whole);
        var fb = new List<ToolStripMenuItem>(); for (int i = 0; i < PatStyles.Length; i++)
        {
            int k = i; var it = new ToolStripMenuItem(PatStyles[i].Name + "   (" + PatStyles[i].Bpm + " BPM)");
            foreach (int bars0 in new[] { 4, 8, 16 }) { int bars = bars0; it.DropDownItems.Add(bars + " bars").Click += (a, b) => { prBox = BuildPattern(k, bars); Toast(PatStyles[k].Name + " beat ready in the box at the top right of the piano roll: drag it onto the song, or press ADD TO SONG."); }; }
            Fam(fb, PatStyles[k].Family).DropDownItems.Add(it);
        }
        foreach (var f in fb) whole.DropDownItems.Add(f);
        var onTracks = new ToolStripMenuItem("A whole beat written onto your own tracks (kick, snare, hats, 808...)"); m.Items.Add(onTracks);
        var ft = new List<ToolStripMenuItem>(); for (int i = 0; i < PatStyles.Length; i++)
        {
            int k = i; var it = new ToolStripMenuItem(PatStyles[i].Name + "   (" + PatStyles[i].Bpm + " BPM)");
            foreach (int bars0 in new[] { 4, 8, 16 }) { int bars = bars0; it.DropDownItems.Add(bars + " bars from bar " + (from + 1)).Click += (a, b) => { bool tempo = Math.Abs(Fx.ProjectBpm - PatStyles[k].Bpm) > 0.5 && AskYes("Set the song to " + PatStyles[k].Bpm + " BPM (the usual " + PatStyles[k].Name + " tempo)? No keeps " + Fx.ProjectBpm.ToString("0.##") + " BPM.", "Generate pattern"); Toast(GeneratePattern(k, bars, from, tempo)); }; }
            Fam(ft, PatStyles[k].Family).DropDownItems.Add(it);
        }
        foreach (var f in ft) onTracks.DropDownItems.Add(f);
        m.Items.Add(new ToolStripSeparator()); var jobs = new ToolStripMenuItem("Jobs of your tracks");
        foreach (var t0 in eng.Tracks) { var t = t0; if (!PrCanEdit(t)) continue; int j = JobOf(t); var it = new ToolStripMenuItem(t.Name + ":  " + (j == 100 ? "drum kit" : (j >= 0 ? Jobs[j] : "not a drum or bass part")), null) { ForeColor = Neon(t.Color) };
            for (int k = 0; k < Jobs.Length; k++) { int kk = k; var ji = new ToolStripMenuItem(Jobs[k]) { Checked = j == k }; ji.Click += (a, b) => prJobs[t] = kk; it.DropDownItems.Add(ji); }
            var none = new ToolStripMenuItem("Not part of the beat") { Checked = j == -1 && prJobs.ContainsKey(t) }; none.Click += (a, b) => prJobs[t] = -1; it.DropDownItems.Add(none); jobs.DropDownItems.Add(it); }
        if (jobs.DropDownItems.Count == 0) jobs.DropDownItems.Add(new ToolStripMenuItem("No note tracks yet") { Enabled = false }); m.Items.Add(jobs);
        m.Show(this, new Point((int)MousePos.X, (int)MousePos.Y));
    }
}
