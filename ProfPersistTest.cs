using System;
using System.IO;
using System.Linq;

// Nothing is ever written over a saved file before it has been read: the profile (bio, location, links, banner), the project list with your name and picture,
// and the saved colour mixes all survive a Save() that happens first (what used to wipe them after an update).   csc /main:PP ... ProfPersistTest.cs
static class PP
{
    static int fails;
    static void Check(string n, bool ok, string d = "") { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + (d.Length > 0 ? "   " + d : "")); }
    static void Main()
    {
        string dir = Path.Combine(Path.GetTempPath(), "purphex_pp_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir); StudioEnv.DataDir = dir;
        File.WriteAllText(Path.Combine(dir, "profile-page.txt"), "nick=poo\nbio=Dark dnb from the north\nlocation=Leeds, United Kingdom\nlink=https://soundcloud.com/aheath\n");
        File.WriteAllText(Path.Combine(dir, "projects.txt"), "P|C:/music/a.ytmproj|My song|" + DateTime.Now.Ticks + "|" + DateTime.Now.Ticks + "|3|120|60|cover.png|desc\n");
        File.WriteAllText(Path.Combine(dir, "profile.txt"), "name=aheath\n");
        // a Save() that comes before anything has been read (the colour mix or the place picker at start-up)
        ProfileStore.Save(); ProjectLibrary.Save(); MixStore.Save(); ReleaseStore.Save();
        string p = File.ReadAllText(Path.Combine(dir, "profile-page.txt")); string pl = File.ReadAllText(Path.Combine(dir, "projects.txt")); string pr = File.ReadAllText(Path.Combine(dir, "profile.txt"));
        Check("the bio survives a Save() that came first", p.Contains("Dark dnb from the north"), p.Replace("\n", " | "));
        Check("so do the nickname, the place and the links", p.Contains("nick=poo") && p.Contains("Leeds, United Kingdom") && p.Contains("soundcloud.com/aheath"));
        Check("the project list survives", pl.Contains("My song"), pl.Replace("\n", " | "));
        Check("your name survives", pr.Contains("aheath"), pr.Replace("\n", " | "));
        Check("in memory too", ProfileStore.Bio == "Dark dnb from the north" && ProfileStore.Location.StartsWith("Leeds") && ProjectLibrary.Entries.Count == 1);
        try { Directory.Delete(dir, true); } catch { }
        Console.WriteLine(fails == 0 ? "=== profile persistence checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
