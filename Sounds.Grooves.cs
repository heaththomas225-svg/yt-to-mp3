using System;
using System.Collections.Generic;
using System.Text;

// =====================================================================================================
//  GROOVES: the drum patterns of the library, written by hand for each genre family. A drum loop is its genre's backbone (the pattern in
//  Patterns) plus one hand-written variation: another kick line, a busier or sparser hat line, an open-hat answer, rolls where the genre
//  rolls, percussion from the genre's own palette, ghost notes where the genre has them. Nothing is sprayed on at random (that was what
//  put toms, cowbells and stray hats into genres that never use them): the same loop number always makes the same groove.
//  Lines are two bars (32 steps) of 16ths: x = a hit, t = a triplet roll, g = a ghost note, '.' = nothing; in a percussion line x, y and z
//  are the genre's first, second and third percussion sound (PercKinds).
// =====================================================================================================
static class Grooves
{
    class Fam
    {
        public string[] Kick = new string[0], Open = new string[0]; public string Busy = "", Sparse = "", Roll = "", Perc = "", Ghost = "";
    }
    static string S(string two) { return two.Replace(" ", ""); }
    static string[] A(params string[] l) { var o = new string[l.Length]; for (int i = 0; i < l.Length; i++) o[i] = S(l[i]); return o; }

    static readonly Dictionary<string, Fam> F = new Dictionary<string, Fam>
    {
        // drum and bass: the two-step backbone stays (kick on 1 and the "and" of 3, snare on 2 and 4); the kick answers move between a few real places
        { "dnb", new Fam { Kick = A("x.........x..... x.......x.x.....", "x.....x...x..... x.........x.x...", "x.x.......x..... x.........x.....", "x.........x...x. x.....x...x....."),
            Busy = S("x.xxx.xxx.xxx.xx x.xxx.xxx.xxx.xx"), Sparse = S("..x...x...x...x. ..x...x...x...x."), Roll = S("x.x.x.x.x.x.x.x. x.x.x.x.x.x.t.t."),
            Perc = S("x...y.....x...y. ..x...y...x.y..."), Ghost = S("...g.....g.g.... ..g....g...g..g."),
            Open = A("..............x. ..............x.", "......x.......x. ......x.......x.", "..x...........x. ..x...x.......x.") } },
        // jungle and breaks: busy chopped kicks and ghosted snares
        { "jungle", new Fam { Kick = A("x.x.......x.x... x.......x.x.x...", "x.....x...x..... x.x.....x.x.....", "x.x.....x.x..... x.........x.x..."),
            Busy = S("xxxxxxxxxxxxxxxx x.xxxxxxx.xxxxxx"), Sparse = S("x.x.x.x.x.x.x.x. x.x.x.x.x.x.x.x."), Roll = S("x.x.x.x.x.x.x.x. x.x.x.x.xxxxxxxx"),
            Perc = S("x...y.....x...y. ..y...x...y.x..."), Ghost = S(".g.g..g...g.g.g. ..g.g..g.g..g.g."),
            Open = A("..x...........x. ..x...........x.", "......x.......x. ..x.......x.....") } },
        // UK garage two-step: swung shuffled hats, a skipping kick, shuffled congas and rim shots
        { "ukg2", new Fam { Kick = A("x.........x..... x.....x...x.....", "x...x.....x..... x.........x.x...", "x.........x...x. x.........x....."),
            Busy = S("x.xxxxxxx.xxxxx. x.xxxxxxx.xxxxxx"), Sparse = S("..x...x...x...x. ..x...x...x...x."), Roll = "",
            Perc = S("...z..x....z..y. ..x....z..y..x.."), Ghost = S("..g..g...g...g.. .g..g...g..g...."),
            Open = A("..x...........x. ..x...........x.", "..x...x.......x. ..x.............") } },
        // speed garage and UK bassline: four on the floor with swung offbeat and 16th hats
        { "bass4", new Fam { Kick = new string[0],
            Busy = S("xxxxxxxxxxxxxxxx xxxxxxxxxxxxxxxx"), Sparse = S("x.x.x.x.x.x.x.x. x.x.x.x.x.x.x.x."), Roll = S("xxxxxxxxxxxxxxxx xxxxxxxxxxxxxtxt"),
            Perc = S("..x...y...x...y. ..x...y...x.y..."), Ghost = S(".......g.......g ......g.......g."),
            Open = A("..x...x...x...x. ..x...x...x...x.", "..x...x...x...x. ..x...x...x.x.x.", "......x.......x. ......x.......x.") } },
        // house, deep house and tech house: four on the floor, offbeat open hats, shakers and congas
        { "house", new Fam { Kick = new string[0],
            Busy = S("x.xxx.xxx.xxx.xx x.xxx.xxx.xxx.xx"), Sparse = S("..x...x...x...x. ..x...x...x...x."), Roll = S("x.xxx.xxx.xxx.xx x.xxx.xxx.xxtxtx"),
            Perc = S("x.y.x.y.x.y.x.y. x.y.x.y.x.y.x.yy"), Ghost = "",
            Open = A("..x...x...x...x. ..x...x...x...x.", "..x...x...x...x. ..x...x...x.x.x.", "..x.......x..... ..x.......x.....") } },
        // pop and disco
        { "pop", new Fam { Kick = new string[0],
            Busy = S("x.x.x.x.x.x.x.xx x.x.x.x.x.x.x.xx"), Sparse = S("x...x...x...x... x...x...x...x..."), Roll = S("x.x.x.x.x.x.x.x. x.x.x.x.x.xxxxxx"),
            Perc = S("x.x.x.x.x.x.x.x. x.x.x.x.x.x.x.y."), Ghost = S("......g.......g. ......g.....g..g"),
            Open = A("..............x. ..............x.", "..x...x...x...x. ..x...x...x...x.", "......x.......x. ..x...........x.") } },
        // techno and trance: four on the floor, offbeat hats, rim and shaker patterns
        { "techno", new Fam { Kick = new string[0],
            Busy = S("xxxxxxxxxxxxxxxx xxxxxxxxxxxxxxxx"), Sparse = S("..x...x...x...x. ..x...x...x...x."), Roll = S("..x...x...x...x. ..x...x...x.xxxx"),
            Perc = S("x..y..x..y..x..y x..y..x..y..x.y."), Ghost = "",
            Open = A("......x.......x. ......x.......x.", "..x...x...x...x. ..x...x...x...x.", "......x.......x. ......x.....x.x.") } },
        // trap, drill, grime and hyperpop: half-time snare, long gaps in the kick, hats that roll
        { "trap", new Fam { Kick = A("x.......x..x.... x.....x.x.......", "x.......x.....x. x.x.....x.x.....", "x..x....x....... x.......x.x...x."),
            Busy = S("xxxxxxxxxxxxxxxx xxxxxxxxxxxxxxxx"), Sparse = S("x.x.x.x.x.x.x.x. x.x.x.x.x.x.x.x."), Roll = S("xxxxxxxxxxxxxtxx xxxxxxxxxxxtxtxx"),
            Perc = S("......x.......x. ...x......x.x..."), Ghost = "",
            Open = A("..............x. ..............x.", "......x.......x. ..............x.") } },
        // boom bap, lo-fi and R&B: lazy swung hats, a loose kick, soft ghost notes
        { "boombap", new Fam { Kick = A("x.....x...x..... ..x...x....x....", "x.....x...x..... x.......x.x.....", "x......x..x..... x.....x...x....."),
            Busy = S("x.x.x.x.x.x.x.xx x.x.x.x.x.x.x.xx"), Sparse = S("x...x...x...x... x...x...x...x..."), Roll = "",
            Perc = S("..x...y...x...y. ..x.....y.x...y."), Ghost = S("......g.......g. .g....g.....g..g"),
            Open = A("..............x. ..............x.", "......x.......x. ..............x.") } },
        // dubstep, riddim and future bass: half-time, snare on three, a few kicks
        { "dub", new Fam { Kick = A("x.........x..... x.....x...x.....", "x.......x.x..... x...........x...", "x......x..x..... x.........x....."),
            Busy = S("x.x.x.x.x.x.x.x. x.x.x.x.x.x.x.xx"), Sparse = S("x...x...x...x... x...x...x...x..."), Roll = S("x.x.x.x.x.x.x.x. x.x.x.x.x.x.xtxt"),
            Perc = S("......x.......x. ...y......y....."), Ghost = "",
            Open = A("..............x. ..............x.", "......x.......x. ..............x.") } },
        // afrobeats, amapiano, dembow, dancehall and baile funk: percussion carries the groove
        { "afro", new Fam { Kick = A("x.....x...x..... x...x.....x.....", "x..x..x...x..... x.....x...x.x...", "x.....x...x.x... x.....x..x......"),
            Busy = S("x.xxx.xxx.xxx.xx x.xxx.xxx.xxx.xx"), Sparse = S("x...x...x...x... x...x...x...x..."), Roll = "",
            Perc = S("x.y..x.y.x.y..x. x..y.x.y..x.y.x."), Ghost = "",
            Open = A("..............x. ..............x.", "..x...x...x...x. ..x...x...x...x.") } },
        // phonk and jersey club: hard stuttering kicks
        { "phonk", new Fam { Kick = A("x.....x...x..... x..x..x.x...x...", "x.....x.x.x..... x.....x...x..x..", "x..x..x...x..... x.....x...x.x..."),
            Busy = S("xxxxxxxxxxxxxxxx xxxxxxxxxxxxxxxx"), Sparse = S("x.x.x.x.x.x.x.x. x.x.x.x.x.x.x.x."), Roll = S("xxxxxxxxxxxxxtxx xxxxxxxxxxxxtxtx"),
            Perc = S("x..y..x...x..y.. x..y..x...x.y..."), Ghost = "",
            Open = A("..............x. ..............x.", "......x.......x. ......x.......x.") } },
    };
    static readonly Dictionary<string, string[]> Alias = new Dictionary<string, string[]> {
        { "dnb", new[] { "dnb_twostep", "dnb_roller", "dnb_jumpup", "dnb_liquid", "dnb_halftime", "dnb_neuro", "dnb_minimal", "dnb_dark", "dnb_rave" } }, { "jungle", new[] { "jungle", "breaks" } }, { "ukg2", new[] { "ukg_2step" } }, { "bass4", new[] { "ukg_speed", "bassline_4x4" } },
        { "house", new[] { "house", "deephouse", "techhouse" } }, { "pop", new[] { "pop", "disco" } }, { "techno", new[] { "techno", "trance" } }, { "trap", new[] { "trap", "drill", "grime", "hyperpop" } }, { "boombap", new[] { "boombap", "lofi", "rnb" } },
        { "dub", new[] { "dubstep", "riddim", "futurebass" } }, { "afro", new[] { "afro", "amapiano", "dembow", "dancehall", "baile" } }, { "phonk", new[] { "phonk", "jersey" } } };
    static readonly Dictionary<string, string> famOf = BuildFamOf();
    static Dictionary<string, string> BuildFamOf() { var d = new Dictionary<string, string>(); foreach (var kv in Alias) foreach (var a in kv.Value) d[a] = kv.Key; return d; }
    public static string FamilyOf(string drums) { string f; return drums != null && famOf.TryGetValue(drums, out f) ? f : "dnb"; }

    // the percussion each genre really uses (the kit has a low tom, a mid tom, a shaker, a rim, a conga and a bell; a genre only gets its own)
    public static string[] PercKinds(string drums)
    {
        switch (drums)
        {
            case "jungle": case "breaks": return new[] { "Shaker", "Rim", "Tom Mid" };
            case "ukg_2step": return new[] { "Shaker", "Rim", "Conga" };
            case "ukg_speed": case "bassline_4x4": return new[] { "Shaker", "Rim" };
            case "house": case "deephouse": case "techhouse": return new[] { "Shaker", "Conga", "Rim" };
            case "disco": return new[] { "Conga", "Shaker", "Bell" };
            case "pop": return new[] { "Shaker", "Rim" };
            case "techno": return new[] { "Rim", "Shaker", "Tom Mid" };
            case "trance": return new[] { "Shaker", "Tom Mid" };
            case "trap": case "drill": case "grime": case "hyperpop": return new[] { "Rim", "Shaker" };
            case "boombap": case "lofi": case "rnb": return new[] { "Shaker", "Rim" };
            case "dubstep": case "riddim": case "futurebass": return new[] { "Rim", "Tom Low" };
            case "afro": return new[] { "Conga", "Shaker", "Rim" };
            case "amapiano": return new[] { "Shaker", "Conga", "Rim" };
            case "dembow": case "dancehall": return new[] { "Rim", "Conga" };
            case "baile": return new[] { "Bell", "Conga", "Rim" };
            case "phonk": return new[] { "Bell", "Rim" };
            case "jersey": return new[] { "Rim", "Shaker" };
        }
        return new[] { "Shaker", "Rim" };   // drum and bass (all sub-styles)
    }
    // the tom a fill rolls on: a genre without toms rolls on its own percussion instead
    public static string FillKind(string drums) { foreach (var k in PercKinds(drums)) if (k.StartsWith("Tom")) return k; return PercKinds(drums)[0]; }

    static readonly int[] SigSteps = { 3, 6, 7, 9, 11, 13, 14, 15, 19, 22, 23, 25, 27, 29, 30, 31 };
    static string Two(string s, int n) { if (string.IsNullOrEmpty(s)) return new string('.', n); var sb = new StringBuilder(); for (int k = 0; k < n; k++) sb.Append(s[k % s.Length]); return sb.ToString(); }
    // a groove of the genre: loop number idx makes the same one every time; feel is idx % 8 (0 base, 1 busy hats, 2 sparse, 3 with fill, 4 open-hat answer, 5 rolls, 6 percussion, 7 ghost notes)
    public static Pat Vary(DrumStyle st, int idx, int feel)
    {
        var p = st.Pat; Fam f = F[FamilyOf(st.Pattern)]; const int n = 32;
        var o = new Pat { K = Two(p.K, n), S = Two(p.S, n), C = Two(p.C, n), H = Two(p.H, n), O = Two(p.O, n), P = Two(p.P, n), G = Two(p.G, n), Bars = 2 };
        if (f.Kick.Length > 0) { int ka = (idx / 8) % (f.Kick.Length + 1); if (ka > 0) o.K = KeepBackbeat(f.Kick[ka - 1], o); }
        if (f.Open.Length > 0 && p.O.Length > 0) { int oa = (idx / 16) % (f.Open.Length + 1); if (oa > 0) o.O = f.Open[oa - 1]; }
        switch (feel)
        {
            case 1: o.H = f.Busy.Length > 0 ? f.Busy : o.H; break;
            case 2: o.H = f.Sparse.Length > 0 ? f.Sparse : o.H; break;
            case 4: if (f.Open.Length > 0 && p.O.Length > 0) o.O = f.Open[(idx / 8 + 1) % f.Open.Length]; break;
            case 5: if (f.Roll.Length > 0) o.H = f.Roll; break;
            case 6: if (f.Perc.Length > 0) o.P = f.Perc; break;
            case 7: if (f.Ghost.Length > 0) o.G = f.Ghost; break;
        }
        // the pack's own touch: one extra percussion hit at a place that suits the pack (so two packs of one genre never write the same loop)
        { for (int t = 0; t < SigSteps.Length; t++) { int sig = SigSteps[(st.Variant + idx / 40 + t) % SigSteps.Length]; if (o.S[sig] == '.' && o.C[sig] == '.' && o.K[sig] == '.' && o.P[sig] == '.') { var pl = o.P.ToCharArray(); pl[sig] = 'x'; o.P = new string(pl); break; } } }
        { var gl = o.G.ToCharArray(); for (int i = 0; i < gl.Length; i++) if (gl[i] != '.' && (o.S[i] != '.' || o.C[i] != '.')) gl[i] = '.'; o.G = new string(gl); }   // (a ghost note is never on top of the backbeat itself)
        return o;
    }
    // what the feel of a loop really is in a genre (a genre without rolls has no "rolls" loops: that feel is the plain groove)
    public static string FeelLabel(string drums, int feel)
    {
        Fam f = F[FamilyOf(drums)]; var p = Patterns.Get(drums);
        switch (feel)
        {
            case 1: return f.Busy.Length > 0 ? "busy hats" : ""; case 2: return f.Sparse.Length > 0 ? "sparse" : ""; case 3: return "with fill"; case 4: return f.Open.Length > 0 && p.O.Length > 0 ? "open hats" : "";
            case 5: return f.Roll.Length > 0 ? "rolls" : ""; case 6: return f.Perc.Length > 0 ? "percussion" : ""; case 7: return f.Ghost.Length > 0 ? "ghost notes" : "";
        }
        return "";
    }
    // a kick line may not sit on the snare or clap (a kick under the backbeat is a different groove, not a variation)
    static string KeepBackbeat(string kick, Pat o)
    {
        var c = kick.ToCharArray(); for (int i = 0; i < c.Length; i++) if ((o.S[i] != '.' || o.C[i] != '.') && c[i] != '.') c[i] = '.'; return new string(c);
    }

    // the lane strings must all be 32 long and use only known letters; returns "" when every family is sound
    public static string Check()
    {
        var bad = new StringBuilder();
        foreach (var kv in F)
        {
            var all = new List<string>(kv.Value.Kick); all.AddRange(kv.Value.Open); all.Add(kv.Value.Busy); all.Add(kv.Value.Sparse); all.Add(kv.Value.Roll); all.Add(kv.Value.Perc); all.Add(kv.Value.Ghost);
            foreach (var s in all) { if (s.Length != 0 && s.Length != 32) bad.Append(kv.Key + ": '" + s + "' is " + s.Length + " steps; "); foreach (char ch in s) if ("xyztg.".IndexOf(ch) < 0) bad.Append(kv.Key + ": letter " + ch + "; "); }
        }
        foreach (var kv in Alias) if (!F.ContainsKey(kv.Key)) bad.Append("no family " + kv.Key + "; ");
        return bad.ToString();
    }
}
