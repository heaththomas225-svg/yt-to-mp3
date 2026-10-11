using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
static class DupT
{
    static double Lum(Color c) { Func<int, double> f = v => { double x = v / 255.0; return x <= 0.03928 ? x / 12.92 : Math.Pow((x + 0.055) / 1.055, 2.4); }; return 0.2126 * f(c.R) + 0.7152 * f(c.G) + 0.0722 * f(c.B); }
    static double Con(Color a, Color b) { double l1 = Lum(a), l2 = Lum(b); if (l1 < l2) { var t = l1; l1 = l2; l2 = t; } return (l1 + 0.05) / (l2 + 0.05); }
    static void Main()
    {
        Console.WriteLine("theme            whiteOnBtn whiteOnBtnC inkOnPanel inkOnLane cyanOnPanel pinkOnPanel violetOnLane accentOnPanel hueV hueC hueP");
        foreach (var t in Themes.All)
        {
            var white = Color.White; var ink = Color.FromArgb(240, 235, 255);
            Console.WriteLine(string.Format("{0,-16} {1,8:0.0} {2,10:0.0} {3,10:0.0} {4,9:0.0} {5,10:0.0} {6,10:0.0} {7,12:0.0} {8,12:0.0}  {9,4:0} {10,4:0} {11,4:0}", t.Name, Con(white, t.Btn), Con(white, t.BtnC), Con(ink, t.Panel), Con(ink, t.Lane), Con(t.Cyan, t.Panel), Con(t.Pink, t.Panel), Con(t.Violet, t.Lane), Con(t.Accent, t.Panel), t.Violet.GetHue(), t.Cyan.GetHue(), t.Pink.GetHue()));
        }
    }
}
