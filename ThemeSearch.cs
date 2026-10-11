using System;
using System.Collections.Generic;
using System.Drawing;
static class TSrch
{
    static int Dist(Color c1, Color c2) { return Math.Abs(c1.R - c2.R) + Math.Abs(c1.G - c2.G) + Math.Abs(c1.B - c2.B); }
    static int D(ThemeDef a, ThemeDef b) { return Dist(a.Violet, b.Violet) + Dist(a.Cyan, b.Cyan) + Dist(a.Pink, b.Pink) + Dist(a.Btn, b.Btn) + Dist(a.Bg1, b.Bg1) * 2; }
    [STAThread]
    static void Main(string[] args)
    {
        StudioEnv.DataDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "purphex_ts_" + Guid.NewGuid().ToString("N")); System.IO.Directory.CreateDirectory(StudioEnv.DataDir);
        var all = new List<ThemeDef>(); foreach (var t in Themes.All) if (!t.Rainbow && !t.Custom) all.Add(t);
        foreach (var line in System.IO.File.ReadAllLines(args[0]))
        {
            var p = line.Split(','); if (p.Length < 7) continue; var c = ThemeDef.FromSpec(p[0], "", double.Parse(p[1]), double.Parse(p[2]), double.Parse(p[3]), double.Parse(p[4]), double.Parse(p[5]), double.Parse(p[6])); c.Legible();
            int mn = int.MaxValue; string nn = ""; foreach (var e in all) { if (e.Name == c.Name) continue; int d = D(c, e); if (d < mn) { mn = d; nn = e.Name; } }
            Console.WriteLine(p[0] + " min " + mn + " nearest " + nn);
        }
    }
}
