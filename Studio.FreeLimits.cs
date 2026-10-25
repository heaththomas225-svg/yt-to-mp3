using System;
using System.Drawing;
using System.Windows.Forms;

// =====================================================================================================
//  THE FREE EDITION STUDIO: the Free Edition opens the studio too, a limited version of it.
//  Free: tracks and the mixer, every effect, the piano roll, sound packs, lyrics, the profile, the Music Hub, the Mastering page with two
//  master presets (Gentle and Clean & Transparent), and export of every kind (mix + master down to stems) as WAV (up to 32-bit) or
//  MP3 320 kbps at 44.1 kHz.
//  Paid only (Supporter Edition and up): themes and fonts, VSTS (Purphex VSTs and your own), vocal presets, bus presets and community
//  presets, the AI generators, Auto Mix, Flex Pitch, Auto EQ, Auto Loudness, AUTO MASTERING, the other master presets, the visualiser,
//  FLAC and MP4 export and rates above 44.1 kHz, and the Video and Photo Editors (hidden from the side panel on the Free Edition).
//  Help shows only the guides for what the Free Edition has (Edition.FreeHelp).
// =====================================================================================================
partial class StudioForm
{
    internal static bool FreeStudio { get { return !Edition.Has(Tier.Supporter); } }
    // true when the edition has it; otherwise the edition prompt opens and false
    internal bool Paid(string what) { return Edition.Gate(Tier.Supporter, what, this); }
    // side panel buttons the Free Edition does not show at all
    static bool FreeHidden(string menuName) { return FreeStudio && (menuName == "Purphex Video Editor" || menuName == "Purphex Photo Editor"); }
    // a paid page reached some other way (an old link, a shortcut) goes back to the Mix page on the Free Edition
    void FreePageGuard() { if (FreeStudio && (tab == 5 || tab == 8 || tab == 10 || tab >= 12)) tab = tab == 5 ? 1 : 0; if (FreeStudio && fontsWin) fontsWin = false; }
    // the master presets the Free Edition can use
    internal static bool FreeMasterPreset(int i) { if (!FreeStudio) return true; if (i < 0 || i >= MasterPresets.All.Length) return false; string n = MasterPresets.All[i].Name; return n.StartsWith("Gentle (") || n.StartsWith("Clean & Transparent ("); }
    // the export formats the Free Edition can use: WAV (16, 24 or 32-bit) and MP3 320 kbps
    internal static bool FreeFormat(int fmt) { return !FreeStudio || fmt == 0 || fmt == 1 || fmt == 2 || fmt == 4; }
    // the visualiser on the Mastering page, locked on the Free Edition: a padlock card in its place
    void PaintVisLocked(Graphics g, RectangleF a)
    {
        FillRR(g, TM.C(235, 14, 7, 30), a, 14); StrokeRR(g, Color.FromArgb(120, NeonViolet), a, 14, 1.2f); float cx = a.X + a.Width / 2;
        Edition.Padlock(g, new RectangleF(cx - 18, a.Y + a.Height / 2 - 62, 36, 42), Color.FromArgb(255, 255, 200, 90));
        Txt(g, "THE VISUALISER", 16, Ink, Rc(a.X, a.Y + a.Height / 2 - 14, a.Width, 24), StringAlignment.Center, true);
        Txt(g, "Watch your music move: part of the Supporter Edition and the Fantasy Edition.", 11, DimC, Rc(a.X + 20, a.Y + a.Height / 2 + 12, a.Width - 40, 20), StringAlignment.Center, false);
        var b = Rc(cx - 80, a.Y + a.Height / 2 + 40, 160, 30); Btn(g, b, "SEE THE EDITIONS", 2, () => Paid("The visualiser"), 11);
    }
}
