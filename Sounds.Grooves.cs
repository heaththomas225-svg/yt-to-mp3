using System;
using System.Collections.Generic;

// =====================================================================================================
//  THE GROOVE BOOK: hand-written drum grooves for every drum style of the library (several each), and which percussion sound belongs in
//  each style. Every loop of a pack is one of these grooves (with its fills, ghost notes and hat feel), never random hits: a DnB loop has
//  a shaker or rim, not a cowbell; an afro loop has congas and shakers; a trap loop has no percussion lane at all.
//  The P (percussion) lane uses letters: s shaker, r rim, c conga, b bell (cowbell), l low tom, m mid tom, x the style's own percussion.
//  The H lane: x a hat, t a triplet roll (trap and drill), the hat's level follows its place in the beat.
// =====================================================================================================
static class Grooves
{
    static Pat G(string k, string s, string c, string h, string o, string g, string p, int bars = 1) { return new Pat { K = k, S = s, C = c, H = h, O = o, G = g, P = p, Bars = bars }; }
    static readonly Dictionary<string, Pat[]> Book = new Dictionary<string, Pat[]>
    {
        { "dnb_twostep", new[] {
            G("x.........x.....", "....x.......x...", "", "x.x.x.x.x.x.x.x.", ".............x..", ".......g.g.....g", "..s...s...s...s."),
            G("x.........x..x..", "....x.......x...", "", "x.xxx.x.x.xxx.x.", "", "......g.......g.", "s.s.s.s.s.s.s.s."),
            G("x.......x.x.....", "....x.......x...", "", "x.x.x.x.x.x.x.x.", "..x...........x.", ".g.....g........", ""),
            G("x.....x...x.....", "....x.......x...", "", "xxxxxxxxxxxxxxxx", "", "...g......g.....", "...r.......r....") } },
        { "dnb_roller", new[] {
            G("x.........x.x...", "....x.......x...", "", "xxxxxxxxxxxxxxxx", "..............x.", "..g...g.g..g..g.", "s.ss.ss.s.ss.ss."),
            G("x......x..x.....", "....x.......x...", "", "x.xxx.xxx.xxx.xx", "", "..g..g.g...g..g.", "s.s.s.s.s.s.s.s."),
            G("x.........xx....", "....x.......x...", "", "xxxxxxxxxxxxxxxx", "", ".g....g..g....g.", "...r.......r..r."),
            G("x..x......x.....", "....x.......x...", "", "x.xxx.xxx.xxx.xx", "......x.........", "..g.....g..g....", "ss.ss.ss.ss.ss.s") } },
        { "dnb_jumpup", new[] {
            G("x.....x...x.....", "....x.......x...", "", "x.x.x.x.x.x.x.x.", "..x...x...x...x.", "...g.......g....", ""),
            G("x.........x.x...", "....x.......x...", "", "x.x.x.x.x.x.x.x.", "..x.......x.....", "", "s...s...s...s..."),
            G("x.....x.x.......", "....x.......x...", "", "x.xxx.x.x.xxx.x.", "..x...........x.", ".......g........", ""),
            G("x..x......x.....", "....x.......x...", "", "x.x.x.x.x.x.x.x.", "......x.......x.", "...g...g...g....", "") } },
        { "dnb_liquid", new[] {
            G("x.........x.....", "....x.......x...", "", "x.x.x.x.x.x.x.x.", "", "......g......g..", "..s...s...s...s."),
            G("x.........x..x..", "....x.......x...", "", "x.xxx.x.x.xxx.x.", "", "..g.....g.....g.", "s.s.s.s.s.s.s.s."),
            G("x.......x.x.....", "....x.......x...", "", "x.x.x.x.x.x.x.x.", "..............x.", ".......g........", "..s.....s......."),
            G("x.........x.....", "....x.......x...", "", "..x...x...x...x.", "", "...g......g.....", "s...s...s...s...") } },
        { "dnb_halftime", new[] {
            G("x.........x.....", "........x.......", "", "x.x.x.x.x.x.x.x.", "............x...", "...........g....", "..s.....s...s..."),
            G("x.....x...x.....", "........x.......", "", "x.xxx.x.x.xxx.x.", "", "......g.....g...", ""),
            G("x.........x..x..", "........x.......", "", "xxxxxxxxxxxxxxxx", "..............x.", "", "...r.......r...."),
            G("x.......x.x.....", "........x.......", "", "x.x.x.x.x.x.x.x.", "", ".....g.....g....", "s.......s.......") } },
        { "dnb_neuro", new[] {
            G("x.....x.x.......", "....x.......x...", "", "x.xxx.x.x.xxx.x.", "", "..g.g..g..g.g...", "x.......x......."),
            G("x.........x.x...", "....x.......x...", "", "x.x.x.x.x.x.x.x.", "", ".g..g..g.g..g...", "...r.......r...."),
            G("x......x..x.....", "....x.......x...", "", "xxxxxxxxxxxxxxxx", "", "..g...g...g...g.", ""),
            G("x.x.......x.....", "....x.......x...", "", "x.xxx.xxx.xxx.xx", "", "......g.g.....g.", "r.......r.......") } },
        { "dnb_minimal", new[] {
            G("x.........x.....", "....x.......x...", "", "..x...x...x...x.", "", ".........g......", ""),
            G("x.........x.....", "....x.......x...", "", "x...x...x...x...", "", "......g.........", "...r.......r...."),
            G("x.......x.......", "....x.......x...", "", "..x...x...x...x.", "", "", "..s...s...s...s.") } },
        { "dnb_dark", new[] {
            G("x.......x.x.....", "....x.......x...", "", "x.x.x.x.x.x.x.x.", "..............x.", "...g.....g......", "......x......x.."),
            G("x.........x.....", "....x.......x...", "", "x.xxx.x.x.xxx.x.", "", "..g.....g......g", ""),
            G("x.....x...x.....", "....x.......x...", "", "xxxxxxxxxxxxxxxx", "", ".g.....g..g.....", "...r.......r....") } },
        { "dnb_rave", new[] {
            G("x...x...x...x...", "....x.......x...", "", "x.x.x.x.x.x.x.x.", "..x...x...x...x.", "", "s.s.s.s.s.s.s.s."),
            G("x.........x.x...", "....x.......x...", "", "x.x.x.x.x.x.x.x.", "..x...x...x...x.", ".......g.g......", ""),
            G("x...x...x...x...", "....x.......x...", "", "xxxxxxxxxxxxxxxx", "..x...x...x...x.", "", "") } },
        { "jungle", new[] {
            G("x.x.......x.x...x.......x.x.....", "....x..x.x..x..x....x..x.x..x.x.", "", "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", "", ".......g.g..........g..g........", "", 2),
            G("x.x.......x.....x.x.....x.......", "....x....x..x.......x..x....x.x.", "", "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", "..............x...............x.", "..g....g..g........g...g.g......", "", 2),
            G("x.........x.x...x.x.......x.....", "....x..x....x..x....x.....x..x..", "", "x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.x.", "", ".g.....g...g...g.g.....g...g....", "s.s.s.s.s.s.s.s.s.s.s.s.s.s.s.s.", 2) } },
        { "ukg_2step", new[] {
            G("x.........x.....", "....x.......x...", "", "x.xx.xx.xx.xx.x.", "..x...........x.", "..g..g...g...g..", "...r..r....r..r."),
            G("x......x..x.....", "....x.......x...", "", "..x...x...x...x.", "..x...x...x...x.", ".g...g....g..g..", "s.s.s.s.s.s.s.s."),
            G("x.........x..x..", "....x.......x...", "", "x.xx.xx.xx.xx.x.", "", "...g..g....g..g.", "...r.......r...."),
            G("x...x.....x.....", "....x.......x...", "", "x.x.x.x.x.x.x.x.", "..............x.", "..g.....g..g....", "...r..r....r..r.") } },
        { "ukg_speed", new[] {
            G("x...x...x...x...", "....x.......x...", "....x.......x...", "..x...x...x...x.", "..x...x...x...x.", "", "s.s.s.s.s.s.s.s."),
            G("x...x...x...x...", "....x.......x...", "", "x.xx.xx.xx.xx.x.", "..x...x...x...x.", "......g.......g.", "...r.......r...."),
            G("x...x...x...x...", "", "....x.......x...", "..x...x...x...x.", "..x...x...x...x.", "", "s.ss.ss.s.ss.ss.") } },
        { "bassline_4x4", new[] {
            G("x...x...x...x...", "", "....x.......x...", "xxxxxxxxxxxxxxxx", "..x...x...x...x.", "", "..s...s...s...s."),
            G("x...x...x...x...", "....x.......x...", "....x.......x...", "x.xx.xx.xx.xx.x.", "..x...x...x...x.", "", "s.s.s.s.s.s.s.s."),
            G("x...x...x...x...", "", "....x.......x...", "..x...x...x...x.", "..x...x...x...x.", "", "...r.......r...."),
            G("x...x...x...x..x", "", "....x.......x...", "xxxxxxxxxxxxxxxx", "..x...x...x...x.", "......g.......g.", "") } },
        { "house", new[] {
            G("x...x...x...x...", "", "....x.......x...", "x.xxx.xxx.xxx.xx", "..x...x...x...x.", "", "s.s.s.s.s.s.s.s."),
            G("x...x...x...x...", "", "....x.......x...", "..x...x...x...x.", "..x...x...x...x.", "", "x.x.x.x.x.x.x.x."),
            G("x...x...x...x...", "", "....x.......x...", "xxxxxxxxxxxxxxxx", "..x.......x.....", "", ".s.s.s.s.s.s.s.s") } },
        { "techhouse", new[] {
            G("x...x...x...x...", "", "....x.......x...", "x.xxx.xxx.xxx.xx", "..x...x...x...x.", "", ".r.r.r.r.r.r.r.r"),
            G("x...x...x...x...", "", "....x.......x...", "..x...x...x...x.", "..x...x...x...x.", "", "s.ss.ss.s.ss.ss."),
            G("x...x...x...x...", "", "....x.......x...", "xxxxxxxxxxxxxxxx", "", "", "...r..r....r..r.") } },
        { "deephouse", new[] {
            G("x...x...x...x...", "", "....x.......x...", "..x.x.x...x.x.x.", "..x.......x.....", "", "...s.......s...."),
            G("x...x...x...x...", "", "....x.......x...", "..x...x...x...x.", "..x...x...x...x.", "", "s.s.s.s.s.s.s.s."),
            G("x...x...x...x...", "", "....x.......x...", "x.x.x.x.x.x.x.x.", "......x.......x.", "", "...r.......r..r.") } },
        { "techno", new[] {
            G("x...x...x...x...", "", "....x.......x...", "..x...x...x...x.", "......x.......x.", "", "x..x..x..x..x..x"),
            G("x...x...x...x...", "", "", "xxxxxxxxxxxxxxxx", "..x...x...x...x.", "", "..r...r...r...r."),
            G("x...x...x...x...", "", "....x.......x...", "x.xxx.xxx.xxx.xx", "..x...x...x...x.", "", ""),
            G("x...x...x...x...", "", "....x.......x...", "..x...x...x...x.", "", "", "r.r.r.r.r.r.r.r.") } },
        { "trap", new[] {
            G("x.......x..x....", "........x.......", "........x.......", "xxxxxxxxxxxxxtxx", "..............x.", "", ""),
            G("x.........x.....", "........x.......", "........x.......", "x.x.x.x.x.x.xtx.", "", "", ""),
            G("x..x......x..x..", "........x.......", "........x.......", "xxxxxxxtxxxxxxxx", "", "", ""),
            G("x.......x.x.....", "........x.......", "........x.......", "x.xtx.x.x.xtx.x.", "", "", "") } },
        { "drill", new[] {
            G("x.......x.....x.", "........x.......", "........x.......", "x.x.x.xtx.x.xtx.", "", "", ""),
            G("x.........x.x...", "....x.......x...", "....x.......x...", "x..x..x.x..x..x.", "", "", ""),
            G("x.....x.......x.", "........x.......", "........x.......", "x.x.xtx.x.x.xtx.", "", "", ""),
            G("x.......x.x...x.", "........x.......", "", "x..x.tx.x..x.tx.", "", "", "") } },
        { "boombap", new[] {
            G("x.....x...x.....", "....x.......x...", "", "x.x.x.x.x.x.x.x.", "..............x.", "......g.........", ""),
            G("x......xx.......", "....x.......x...", "", "x.x.x.x.x.x.x.x.", "", ".g..........g...", ""),
            G("x.........xx....", "....x.......x...", "", "x.x.x.x.x.x.x.xx", "..x.............", "", "...r.......r....") } },
        { "dubstep", new[] {
            G("x.........x.....", "........x.......", "........x.......", "x.x.x.x.x.x.x.x.", "..............x.", "", ""),
            G("x.....x.......x.", "........x.......", "", "x.xxx.x.x.xxx.x.", "", "", ""),
            G("x.........x..x..", "........x.......", "........x.......", "xxxxxxxxxxxxxxxx", "", "", "") } },
        { "riddim", new[] {
            G("x.....x......x..", "........x.......", "........x.......", "x.x.x.x.x.x.x.x.", "", "", ""),
            G("x.......x.x.....", "........x.......", "........x.......", "x.xxx.x.x.xxx.x.", "", "", ""),
            G("x..x......x.....", "........x.......", "", "xxxxxxxxxxxxxxxx", "", "", "") } },
        { "afro", new[] {
            G("x.....x...x.....", "", "......x.......x.", "x.x.x.x.x.x.x.x.", "", "", "..c..c....c..c.."),
            G("x..x....x.......", "", "...x......x.....", "x.xxx.x.x.xxx.x.", "", "", "s.s.s.s.s.s.s.s."),
            G("x.....x.x.......", "", "......x.......x.", "..x...x...x...x.", "", "", "c..c..c.c..c.c.."),
            G("x.......x..x....", "", "....x.......x...", "x.x.x.x.x.x.x.x.", "", "", "..s...s...s..cc.") } },
        { "amapiano", new[] {
            G("x...x...x...x...", "", ".......x.......x", "..x...x...x...x.", "", "", "s.ss.s.ss.s.s.ss"),
            G("x...x...x...x...", "", "....x.......x...", "x.x.x.x.x.x.x.x.", "", "", "..r...r...r.r..."),
            G("x...x...x...x...", "", ".......x.......x", "..x...x...x...x.", "", "", "s.s.s.s.s.s.s.s.") } },
        { "dembow", new[] {
            G("x...x...x...x...", "...x..x....x..x.", "", "x.x.x.x.x.x.x.x.", "", "", ""),
            G("x...x...x...x...", "...x..x....x..x.", "", "..x...x...x...x.", "", "", "s.s.s.s.s.s.s.s."),
            G("x...x...x...x...", "...x..x.x..x..x.", "", "x.x.x.x.x.x.x.x.", "", "", "...r.......r....") } },
        { "dancehall", new[] {
            G("x..x..x.x..x..x.", "...x..x....x..x.", "", "x.x.x.x.x.x.x.x.", "", "", ""),
            G("x..x...x..x.....", "....x.......x...", "", "x.x.x.x.x.x.x.x.", "", "", "..r..r....r..r.."),
            G("x.....x.x.......", "...x..x....x..x.", "", "..x...x...x...x.", "", "", "s.s.s.s.s.s.s.s.") } },
        { "baile", new[] {
            G("x..x...x..x.x...", "....x.......x..x", "", "x.x.x.x.x.x.x.x.", "", "", "c.c..c.c.c..c.c."),
            G("x..x..x...x..x..", "....x.......x...", "", "x.x.x.x.x.x.x.x.", "", "", "..c..c....c..c.."),
            G("x..x...x..x.x...", "....x.......x...", "", "..x...x...x...x.", "", "", "c..c..c.c..c..c.") } },
        { "lofi", new[] {
            G("x......x..x.....", "....x.......x...", "", "x.x.x.x.x.x.x.x.", "", "......g.......g.", ""),
            G("x.........x.x...", "....x.......x...", "", "x.x.x.x.x.x.x.x.", "", "..g.......g.....", "...s.......s...."),
            G("x......xx.......", "....x.......x...", "", "x.xxx.x.x.xxx.x.", "", ".......g........", "") } },
        { "rnb", new[] {
            G("x......x.x......", "....x.......x...", "....x.......x...", "x.x.x.x.x.x.x.xx", "", "..........g.....", ""),
            G("x.........x..x..", "....x.......x...", "", "x.xx.x.xx.x.x.xx", "", ".g......g.......", "...r.......r...."),
            G("x......x..x.....", "....x.......x...", "....x.......x...", "x.x.x.x.x.x.x.x.", "", "", "") } },
        { "pop", new[] {
            G("x...x...x...x...", "", "....x.......x...", "x.x.x.x.x.x.x.x.", "..............x.", "", ""),
            G("x.....x.x.......", "....x.......x...", "", "x.x.x.x.x.x.x.x.", "", "", "s.s.s.s.s.s.s.s."),
            G("x...x...x...x...", "", "....x.......x...", "xxxxxxxxxxxxxxxx", "..x...x...x...x.", "", "") } },
        { "disco", new[] {
            G("x...x...x...x...", "", "....x.......x...", "x.x.x.x.x.x.x.x.", "..x...x...x...x.", "", "s.s.s.s.s.s.s.s."),
            G("x...x...x...x...", "", "....x.......x...", "xxxxxxxxxxxxxxxx", "..x...x...x...x.", "", "..c...c...c...c."),
            G("x...x...x...x...", "....x.......x...", "", "x.x.x.x.x.x.x.x.", "..x...x...x...x.", "", "") } },
        { "phonk", new[] {
            G("x.....x...x.....", "....x.......x...", "", "xxxxxxxxxxxxxxxx", "", "", "b..b..b...b..b.."),
            G("x.......x..x....", "....x.......x...", "", "x.x.x.x.x.x.x.x.", "", "", "b.b..b.b..b.b..."),
            G("x.....x.x.......", "....x.......x...", "", "xxxxxxxtxxxxxxxx", "", "", "b..b..b.b..b..b.") } },
        { "jersey", new[] {
            G("x..x..x.x...x...", "....x.......x...", "", "x.x.x.x.x.x.x.x.", "", "", ""),
            G("x..x..x...x.x...", "....x.......x...", "", "..x...x...x...x.", "", "", ""),
            G("x..x..x.x..x..x.", "....x.......x...", "", "x.x.x.x.x.x.x.x.", "", "", "") } },
        { "hyperpop", new[] {
            G("x.....x.x.......", "....x.......x...", "", "xxxxxxxxxxxxtxxx", "", "", ""),
            G("x.x.....x.x.....", "....x.......x...", "", "x.xtx.x.x.xtx.x.", "", "", ""),
            G("x.....xx..x.....", "....x.......x...", "", "xxxxxxxxxxxxxxxx", "", "", "") } },
        { "grime", new[] {
            G("x.......x.x.....", "....x.......x...", "", "x.x.x.x.x.x.x.x.", "", "", "......x......x.."),
            G("x.........x.....", "....x.......x...", "", "x.xxx.x.x.xxx.x.", "", "", ""),
            G("x.....x...x.x...", "....x.......x...", "", "x.x.x.x.x.x.x.x.", "", "", "...r.......r....") } },
        { "trance", new[] {
            G("x...x...x...x...", "", "....x.......x...", "xxxxxxxxxxxxxxxx", "..x...x...x...x.", "", ""),
            G("x...x...x...x...", "", "....x.......x...", "..x...x...x...x.", "..x...x...x...x.", "", "s.s.s.s.s.s.s.s."),
            G("x...x...x...x...", "", "", "x.xxx.xxx.xxx.xx", "..x...x...x...x.", "", "") } },
        { "futurebass", new[] {
            G("x.........x.....", "........x.......", "........x.......", "x.x.x.x.x.x.x.x.", "..............x.", "", ""),
            G("x......x..x.....", "........x.......", "........x.......", "x.xxx.x.x.xxx.x.", "", "", ""),
            G("x.........x..x..", "........x.......", "", "x.x.x.x.x.x.xtx.", "", "", "") } },
        { "breaks", new[] {
            G("x.........x.x...", "....x.......x...", "", "x.x.x.x.x.x.x.x.", "", ".......g.g......", ""),
            G("x.x.......x.....", "....x..x.x..x...", "", "x.x.x.x.x.x.x.x.", "..............x.", "", ""),
            G("x......x..x.x...", "....x.......x...", "", "x.xxx.x.x.xxx.x.", "", "..g.......g.....", "s.s.s.s.s.s.s.s.") } },
    };
    // the style's own percussion sound (the 'x' of a P lane)
    public static char DefaultPerc(string style)
    {
        if (style.StartsWith("dnb") || style == "jungle" || style == "trance" || style == "house" || style == "deephouse" || style == "pop" || style == "disco" || style == "amapiano" || style == "lofi" || style == "breaks") return 's';
        if (style.StartsWith("ukg") || style.StartsWith("bassline")) return 's';
        if (style == "techno" || style == "techhouse" || style == "grime" || style == "dembow" || style == "dancehall" || style == "rnb" || style == "boombap" || style == "dubstep") return 'r';
        if (style == "afro" || style == "baile") return 'c'; if (style == "phonk") return 'b'; return 's';
    }
    // the kit's percussion sound for a letter of the P lane (the kit lists: Tom Low, Tom Mid, Shaker, Rim, Conga, Bell)
    public static int PercIndex(char c, string style)
    {
        if (c == 'x' || c == 'X') c = DefaultPerc(style);
        switch (c) { case 'l': return 0; case 'm': return 1; case 's': return 2; case 'r': return 3; case 'c': return 4; case 'b': return 5; default: return 2; }
    }
    // styles whose drums have no percussion lane (trap, drill and the like: a shaker there is a stray noise)
    public static bool HasPerc(string style) { switch (style) { case "trap": case "drill": case "hyperpop": case "jersey": case "dubstep": case "riddim": case "futurebass": return false; } return true; }
    public static int Count(string style) { Pat[] b; return Book.TryGetValue(style, out b) ? b.Length : 0; }
    // groove number i of a style (the style's own pattern when the book has none)
    public static Pat Get(string style, int i)
    {
        Pat[] b; if (!Book.TryGetValue(style, out b) || b.Length == 0) return Patterns.Get(style); return b[((i % b.Length) + b.Length) % b.Length];
    }
    public static IEnumerable<string> Styles { get { return Book.Keys; } }
}
