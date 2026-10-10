using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

// ======================================================================
//  Extensions: drop plugin DLLs into  %LocalAppData%\YtToMp3\plugins  and they appear in the studio's
//  effect browser. Plugins are real programs, so each one must be approved by you first (and is
//  disabled again automatically if the file changes). A crashing plugin is switched off, not the app.
// ======================================================================
public class PluginInfo
{
    public string File = "", Hash = "", Name = "", Error = ""; public bool Enabled, Loaded; public List<string> Types = new List<string>();
}

public static class PluginHost
{
    public static string Dir { get { return Path.Combine(StudioEnv.DataDir, "plugins"); } }
    static string Approvals { get { return Path.Combine(StudioEnv.DataDir, "plugins.txt"); } }
    static readonly Dictionary<string, PluginInfo> loaded = new Dictionary<string, PluginInfo>();

    static string HashOf(string path) { using (var sha = SHA256.Create()) using (var f = System.IO.File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(f)).Replace("-", "").ToLowerInvariant(); }

    static Dictionary<string, string> ReadApprovals()
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try { if (System.IO.File.Exists(Approvals)) foreach (var l in System.IO.File.ReadAllLines(Approvals)) { var p = l.Split('|'); if (p.Length == 3 && p[0] == "allow") d[p[1]] = p[2]; } } catch { }
        return d;
    }

    static void WriteApprovals(Dictionary<string, string> d)
    {
        var sb = new StringBuilder(); foreach (var kv in d) sb.Append("allow|").Append(kv.Key).Append('|').Append(kv.Value).Append('\n');
        Directory.CreateDirectory(StudioEnv.DataDir); System.IO.File.WriteAllText(Approvals, sb.ToString());
    }

    // Lists the plugin files and whether you've approved them.
    public static List<PluginInfo> Scan()
    {
        var list = new List<PluginInfo>(); var ap = ReadApprovals();
        try
        {
            Directory.CreateDirectory(Dir);
            foreach (var f in Directory.GetFiles(Dir, "*.dll"))
            {
                var p = new PluginInfo { File = Path.GetFileName(f), Name = Path.GetFileNameWithoutExtension(f) };
                try { p.Hash = HashOf(f); } catch (Exception ex) { p.Error = ex.Message; }
                string h; p.Enabled = ap.TryGetValue(p.File, out h) && h == p.Hash;
                PluginInfo l; if (loaded.TryGetValue(p.File, out l) && l.Hash == p.Hash) { p.Loaded = true; p.Types = l.Types; }
                list.Add(p);
            }
        }
        catch { }
        return list;
    }

    public static void SetEnabled(PluginInfo p, bool on)
    {
        var ap = ReadApprovals(); if (on) ap[p.File] = p.Hash; else ap.Remove(p.File); WriteApprovals(ap); p.Enabled = on;
    }

    // Loads every approved plugin that isn't loaded yet. Returns a short log of what happened.
    public static List<string> LoadEnabled()
    {
        var log = new List<string>();
        foreach (var p in Scan())
        {
            if (!p.Enabled || p.Loaded) continue;
            try
            {
                var asm = Assembly.LoadFrom(Path.Combine(Dir, p.File)); Type[] types;
                try { types = asm.GetTypes(); } catch (ReflectionTypeLoadException ex) { types = Array.FindAll(ex.Types, t => t != null); }
                foreach (var t in types)
                {
                    if (!t.IsClass || t.IsAbstract || !typeof(Fx).IsAssignableFrom(t) || t.GetConstructor(Type.EmptyTypes) == null) continue;
                    var probe = (Fx)Activator.CreateInstance(t); string id = string.IsNullOrEmpty(probe.Type) ? "plug_" + t.Name.ToLowerInvariant() : probe.Type;
                    if (FxRegistry.IsBuiltin(id)) id = "x_" + id; Type tt = t; string tid = id;
                    FxRegistry.Register(new FxRegistry.Info(tid, string.IsNullOrEmpty(probe.Title) ? t.Name : probe.Title, string.IsNullOrEmpty(probe.Category) ? "Plugins" : probe.Category, probe.Blurb ?? ""),
                        () => { var f = (Fx)Activator.CreateInstance(tt); f.Type = tid; f.Prepare(StudioEngine.SR); return f; });
                    p.Types.Add(tid); log.Add("Loaded " + probe.Title + "  (" + p.File + ")");
                }
                p.Loaded = true; loaded[p.File] = p;
                if (p.Types.Count == 0) log.Add(p.File + ": no effect classes found (see PLUGINS.md)");
            }
            catch (Exception ex) { p.Error = ex.Message; log.Add(p.File + ": failed to load: " + ex.Message); }
        }
        return log;
    }

    // Writes the SDK sample (a working tremolo effect), a build script, and instructions next to the plugins.
    public static string WriteSdk(string exePath)
    {
        string d = Path.Combine(Dir, "sdk"); Directory.CreateDirectory(d);
        System.IO.File.WriteAllText(Path.Combine(d, "SamplePlugin.cs"), SampleSource);
        System.IO.File.WriteAllText(Path.Combine(d, "build.bat"),
            "@echo off\r\nrem Builds SamplePlugin.cs into ..\\SampleTremolo.dll (needs the .NET Framework that ships with Windows)\r\n" +
            "\"%WINDIR%\\Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe\" /nologo /target:library /out:..\\SampleTremolo.dll /r:\"" + exePath + "\" SamplePlugin.cs\r\n" +
            "if errorlevel 1 (echo Build failed) else (echo Built ..\\SampleTremolo.dll. Open Plugins in the app and enable it.)\r\npause\r\n");
        System.IO.File.WriteAllText(Path.Combine(d, "README.txt"),
            "Writing a plugin\r\n----------------\r\n1. Copy SamplePlugin.cs and change it (class name, parameters, the Process method).\r\n2. Run build.bat. It creates a .dll in the plugins folder.\r\n" +
            "3. In the app: menu > Plugins, then tick the plugin to approve it. It appears in the studio under Add Effect > Plugins.\r\n\r\n" +
            "Your plugin derives from Fx. Add parameters with AddP / AddC in the constructor, read them with Get(\"id\"), and process audio in Process(l, r, n)\r\n" +
            "(left and right sample arrays, n samples). Use Update() to cache values when a parameter changes.\r\n");
        return d;
    }

    public const string SampleSource = @"// Sample plugin: a stereo tremolo. Build with build.bat, then approve it in the app (menu > Plugins).
using System;

public class SampleTremolo : Fx
{
    double phase, rate, depth;

    public SampleTremolo()
    {
        Type = ""sample_tremolo""; Title = ""Sample Tremolo""; Category = ""Plugins""; Blurb = ""Example plugin: volume wobble"";
        AddP(""rate"", ""Rate"", 0.1f, 20f, 5f, ""Hz"");        // id, name, min, max, default, unit
        AddP(""depth"", ""Depth"", 0f, 100f, 60f, ""%"");
    }

    protected override void Update() { rate = Get(""rate""); depth = Get(""depth"") / 100.0; }   // called when a parameter changes

    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        for (int i = 0; i < n; i++)
        {
            double g = 1.0 - depth * (0.5 - 0.5 * Math.Cos(phase)); phase += 2 * Math.PI * rate / SR; if (phase > 2 * Math.PI) phase -= 2 * Math.PI;
            l[i] = (float)(l[i] * g); r[i] = (float)(r[i] * g);
        }
    }
}
";
}

// ----------------------------------------------------------------------
class PluginManagerForm : Form
{
    readonly Func<string> getFilter; readonly Action<string> setFilter; Panel list; TextBox filterBox; Label msg;

    public PluginManagerForm(Func<string> getFilter, Action<string> setFilter)
    {
        this.getFilter = getFilter; this.setFilter = setFilter;
        Text = "Plugins and extensions"; FormBorderStyle = FormBorderStyle.FixedDialog; StartPosition = FormStartPosition.CenterParent; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        BackColor = Color.FromArgb(10, 4, 20); ForeColor = Color.FromArgb(240, 232, 255); ClientSize = new Size(700, 600); Font = new Font("Segoe UI", 10f); Win.Dark(this);
        Controls.Add(new Label { Text = "Studio effect plugins", Left = 18, Top = 14, Width = 400, Height = 26, Font = new Font("Segoe UI Semibold", 13f), ForeColor = Color.FromArgb(225, 190, 255) });
        Controls.Add(new Label { Text = "Drop plugin .dll files into the plugins folder, press Reload, then tick a plugin to approve it. Plugins are programs: only enable ones you trust. They show up in the studio under Add Effect > Plugins.", Left = 18, Top = 44, Width = 664, Height = 50, ForeColor = Color.FromArgb(170, 150, 200) });
        list = new Panel { Left = 18, Top = 100, Width = 664, Height = 200, AutoScroll = true, BackColor = Color.FromArgb(8, 4, 16), BorderStyle = BorderStyle.FixedSingle }; Controls.Add(list); Win.DarkControl(list);
        Mk("Open plugins folder", 18, 310, 170, () => { Directory.CreateDirectory(PluginHost.Dir); Process.Start("explorer.exe", "\"" + PluginHost.Dir + "\""); });
        Mk("Reload", 196, 310, 110, () => { Reload(true); });
        Mk("Create sample plugin (SDK)", 314, 310, 210, () => { string d = PluginHost.WriteSdk(Application.ExecutablePath); msg.Text = "SDK written to " + d + ". Run build.bat there."; Process.Start("explorer.exe", "\"" + d + "\""); });
        msg = new Label { Left = 18, Top = 352, Width = 664, Height = 40, ForeColor = Color.FromArgb(120, 230, 160) }; Controls.Add(msg);

        Controls.Add(new Label { Text = "Custom ffmpeg filter for conversions (advanced)", Left = 18, Top = 400, Width = 500, Height = 26, Font = new Font("Segoe UI Semibold", 13f), ForeColor = Color.FromArgb(225, 190, 255) });
        Controls.Add(new Label { Text = "Any ffmpeg audio filter chain is applied to each download (before loudness normalization). Examples: aecho=0.8:0.88:60:0.4   or   flanger   or   acrusher=bits=8   or   lv2=p=<plugin URI> to host an LV2 plugin you installed.", Left = 18, Top = 430, Width = 664, Height = 52, ForeColor = Color.FromArgb(170, 150, 200) });
        filterBox = new TextBox { Left = 18, Top = 486, Width = 664, Text = getFilter(), BackColor = Color.FromArgb(8, 4, 16), ForeColor = Color.FromArgb(240, 232, 255), BorderStyle = BorderStyle.FixedSingle }; Controls.Add(filterBox);
        Mk("Test and save filter", 18, 520, 190, () => TestFilter());
        Mk("Clear", 216, 520, 90, () => { filterBox.Text = ""; setFilter(""); msg.Text = "Filter cleared."; });
        Mk("Close", 582, 548, 100, () => Close());
        Reload(false);
    }

    Button Mk(string text, int x, int y, int w, Action click)
    {
        var b = new Button { Text = text, Left = x, Top = y, Width = w, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(118, 52, 212), ForeColor = Color.White, Cursor = Cursors.Hand };
        b.FlatAppearance.BorderColor = Color.FromArgb(190, 120, 255); b.Click += (s, e) => click(); Controls.Add(b); return b;
    }

    void Reload(bool load)
    {
        if (load) { var log = PluginHost.LoadEnabled(); msg.Text = log.Count == 0 ? "Nothing new to load." : string.Join("   ", log.ToArray()); }
        list.Controls.Clear(); int y = 6; var ps = PluginHost.Scan();
        if (ps.Count == 0) list.Controls.Add(new Label { Text = "No plugins found yet. Folder: " + PluginHost.Dir, Left = 8, Top = 8, Width = 620, Height = 40, ForeColor = Color.FromArgb(170, 150, 200) });
        foreach (var p in ps)
        {
            var cb = new DarkCheck { Left = 6, Top = y, Width = 640, Height = 30, Checked = p.Enabled, Text = p.Name + (p.Loaded ? "   (loaded: " + string.Join(", ", p.Types.ToArray()) + ")" : (p.Enabled ? "   (approved: press Reload)" : "   (not approved)")) + (p.Error.Length > 0 ? "   ERROR: " + p.Error : "") };
            var pi = p; cb.CheckedChanged += (s, e) => { PluginHost.SetEnabled(pi, cb.Checked); msg.Text = cb.Checked ? "Approved. Press Reload to load it." : "Disabled. It stays loaded until you restart the app."; };
            list.Controls.Add(cb); y += 34;
        }
    }

    void TestFilter()
    {
        string f = filterBox.Text.Trim();
        if (f.Length == 0) { setFilter(""); msg.Text = "Filter cleared."; return; }
        if (f.IndexOf('"') >= 0 || f.StartsWith("-") || f.IndexOf('\n') >= 0) { msg.ForeColor = Color.FromArgb(255, 140, 170); msg.Text = "Don't use double quotes or a leading dash in the filter."; return; }
        string ff = StudioEnv.Ffmpeg(); if (ff == null) { msg.Text = "Convert something once first so ffmpeg is installed."; return; }
        int code; string o = MasterDsp.RunCapture(ff, "-hide_banner -f lavfi -i anullsrc=r=44100:cl=stereo -t 0.2 -af \"" + f + "\" -f null -", null, out code);
        if (code == 0) { setFilter(f); msg.ForeColor = Color.FromArgb(120, 230, 160); msg.Text = "Filter works and is saved. It will be applied to your conversions."; }
        else { msg.ForeColor = Color.FromArgb(255, 140, 170); var ls = o.Trim().Split('\n'); msg.Text = "ffmpeg rejected it: " + ls[ls.Length - 1].Trim(); }
    }
}
