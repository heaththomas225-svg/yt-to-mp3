using System;
using System.Drawing;
using System.Windows.Forms;

// Colour themes for Purphex Music (the studio, Mastering and Bass Lab): one choice changes every glow, border, button and highlight.
sealed class ThemeDef
{
    public string Name, Blurb; public Color Violet, Cyan, Pink, Lime, Accent, Accent2, Btn, BtnC, EdgeC, Panel, Lane, Lane2, Bg0, Bg1;
    public ThemeDef(string name, string blurb, int[] c) { Name = name; Blurb = blurb; Violet = C(c, 0); Cyan = C(c, 3); Pink = C(c, 6); Lime = C(c, 9); Accent = C(c, 12); Accent2 = C(c, 15); Btn = C(c, 18); BtnC = C(c, 21); EdgeC = C(c, 24); Panel = C(c, 27); Lane = C(c, 30); Lane2 = C(c, 33); Bg0 = C(c, 36); Bg1 = C(c, 39); }
    static Color C(int[] a, int i) { return Color.FromArgb(a[i], a[i + 1], a[i + 2]); }
}
static class Themes
{
    public static readonly ThemeDef[] All = {
        new ThemeDef("Purphex", "The original neon purple, cyan and pink.", new[] { 150,90,255, 0,229,255, 255,43,214, 120,255,120, 178,108,255, 236,92,226, 118,52,212, 70,40,120, 140,85,210, 18,10,34, 11,6,22, 15,8,29, 7,4,16, 18,8,36 }),
        new ThemeDef("Ocean", "Cool blues and teal, like a night sea.", new[] { 70,140,255, 0,229,255, 0,200,170, 120,255,200, 100,170,255, 40,210,200, 28,84,190, 24,56,110, 60,120,210, 8,14,32, 5,9,22, 8,13,29, 3,7,18, 7,16,38 }),
        new ThemeDef("Sunset", "Hot orange, amber and pink.", new[] { 255,130,70, 255,205,70, 255,70,130, 255,230,120, 255,160,100, 255,90,140, 200,80,40, 112,50,40, 215,115,80, 32,13,13, 22,8,9, 29,11,11, 16,6,6, 38,14,14 }),
        new ThemeDef("Toxic", "Acid green and lime on black.", new[] { 80,225,110, 190,255,60, 0,255,200, 190,255,60, 120,240,130, 200,255,80, 40,150,60, 30,84,44, 80,200,110, 8,24,12, 5,16,8, 8,21,11, 3,12,6, 8,30,14 }),
        new ThemeDef("Crimson", "Deep red with a hot pink edge.", new[] { 255,70,90, 255,170,180, 255,40,160, 255,150,150, 255,105,125, 255,70,160, 190,40,62, 104,30,46, 210,70,92, 28,8,12, 19,5,8, 25,8,11, 14,4,6, 34,9,14 }),
        new ThemeDef("Mono", "Clean white and grey, no colour at all.", new[] { 205,205,220, 255,255,255, 170,170,190, 220,220,230, 215,215,230, 165,165,185, 92,92,112, 52,52,66, 140,140,160, 16,16,20, 10,10,13, 14,14,18, 6,6,9, 20,20,26 }),
    };
    public static int Index;
    public static ThemeDef Current { get { return All[Math.Max(0, Math.Min(All.Length - 1, Index))]; } }
    public static int IndexOf(string name) { for (int i = 0; i < All.Length; i++) if (string.Equals(All[i].Name, name, StringComparison.OrdinalIgnoreCase)) return i; return 0; }
    // choose a theme: every window of the program repaints in the new colours at once
    public static void Apply(int i)
    {
        Index = Math.Max(0, Math.Min(All.Length - 1, i)); var t = Current; StudioForm.ApplyTheme(t); MasterForm.ApplyTheme(t); SynthCanvas.ApplyTheme(t); Prefs.Theme = t.Name; UserBackground.Version++;
        foreach (Form f in Application.OpenForms) { try { f.Invalidate(true); } catch { } }
    }
    public static void ApplyByName(string name) { int i = IndexOf(name); Index = i; var t = Current; StudioForm.ApplyTheme(t); MasterForm.ApplyTheme(t); SynthCanvas.ApplyTheme(t); }
}
