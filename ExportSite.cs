using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// Writes the plugin list, sound packs, guides and changelog as JSON for the Purphex website.   csc /main:X1 ... ExportSite.cs  (run: ExportSite.exe <output folder>)
static class X1
{
    static string J(string s) { if (s == null) s = ""; var sb = new StringBuilder("\""); foreach (char c in s) { switch (c) { case '"': sb.Append("\\\""); break; case '\\': sb.Append("\\\\"); break; case '\n': sb.Append("\\n"); break; case '\r': break; case '\t': sb.Append("\\t"); break; default: if (c < 32) sb.Append("\\u" + ((int)c).ToString("x4")); else sb.Append(c); break; } } return sb.Append('"').ToString(); }
    [STAThread]
    static void Main(string[] args)
    {
        string dir = args.Length > 0 ? args[0] : "."; Directory.CreateDirectory(dir);
        var all = FxRegistry.All; var sb = new StringBuilder("["); bool first = true;
        foreach (var i in all) { if (i.Type == "gain") continue; if (!first) sb.Append(",\n"); first = false; sb.Append("{\"name\":" + J(i.Name) + ",\"category\":" + J(i.Category) + ",\"blurb\":" + J(i.Blurb) + "}"); }
        File.WriteAllText(Path.Combine(dir, "plugins.json"), sb.Append("]").ToString(), new UTF8Encoding(false));
        sb = new StringBuilder("["); first = true; foreach (var r in SoundCatalog.Recipes) { if (!first) sb.Append(",\n"); first = false; sb.Append("{\"id\":" + J(r.Id) + ",\"name\":" + J(r.Name) + ",\"section\":" + J(r.Section) + ",\"genre\":" + J(r.Genre) + ",\"sub\":" + J(r.Sub) + ",\"mood\":" + J(r.Mood) + ",\"desc\":" + J(r.Desc) + ",\"hue\":" + r.Hue + "}"); }
        File.WriteAllText(Path.Combine(dir, "packs.json"), sb.Append("]").ToString(), new UTF8Encoding(false));
        sb = new StringBuilder("["); first = true; foreach (var h in HelpContent.All) { if (!first) sb.Append(",\n"); first = false; sb.Append("{\"id\":" + J(h.Id) + ",\"category\":" + J(h.Category) + ",\"title\":" + J(h.Title) + ",\"body\":" + J(h.Body.Trim()) + "}"); }
        File.WriteAllText(Path.Combine(dir, "guides.json"), sb.Append("]").ToString(), new UTF8Encoding(false));
        Console.WriteLine("plugins " + all.Length + ", packs " + SoundCatalog.Recipes.Count + ", guides " + HelpContent.All.Length); Environment.Exit(0);
    }
}
