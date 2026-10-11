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
{ public bool Native; public string Path = "";
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
            var files = new List<string>(Directory.GetFiles(Dir, "*.dll")); files.AddRange(Directory.GetFiles(Dir, "*.vst3")); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); foreach (var f0 in files) seen.Add(Path.GetFileName(f0));
            foreach (var vd in VstFolders())
            {
                try
                {
                    var more = new List<string>(Directory.GetFiles(vd, "*.dll")); more.AddRange(Directory.GetFiles(vd, "*.vst3"));
                    foreach (var sd in Directory.GetDirectories(vd))
                    {
                        if (sd.EndsWith(".vst3", StringComparison.OrdinalIgnoreCase)) { string inner = Path.Combine(sd, "Contents", "x86_64-win", Path.GetFileName(sd)); if (System.IO.File.Exists(inner)) more.Add(inner); continue; }
                        more.AddRange(Directory.GetFiles(sd, "*.dll")); more.AddRange(Directory.GetFiles(sd, "*.vst3"));
                    }
                    foreach (var f1 in more) if (seen.Add(Path.GetFileName(f1))) files.Add(f1);
                }
                catch { }
            }
            foreach (var f in files)
            {
                bool inDir = string.Equals(Path.GetDirectoryName(f), Dir, StringComparison.OrdinalIgnoreCase);
                var p = new PluginInfo { Path = f, File = inDir ? Path.GetFileName(f) : f, Name = Path.GetFileNameWithoutExtension(f) };
                try { p.Hash = HashOf(f); } catch (Exception ex) { p.Error = ex.Message; }
                if (f.EndsWith(".vst3", StringComparison.OrdinalIgnoreCase)) p.Native = true; else try { AssemblyName.GetAssemblyName(f); } catch (BadImageFormatException) { p.Native = true; } catch { }   // VST / native audio plugins are not .NET
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

    // Where installed VST2 plugins usually live.
    public static List<string> VstFolders()
    {
        var l = new List<string>();
        string pf = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
        string cf = Environment.GetEnvironmentVariable("CommonProgramFiles") ?? @"C:\Program Files\Common Files";
        foreach (var d in new[] { Path.Combine(cf, "VST3"), Path.Combine(cf, "VST2"), Path.Combine(cf, "VSTPlugins"), Path.Combine(pf, "VSTPlugins"), Path.Combine(pf, "Steinberg", "VSTPlugins"), Path.Combine(pf, "VstPlugins") })
            if (Directory.Exists(d) && !l.Contains(d)) l.Add(d);
        return l;
    }

    static readonly Dictionary<string, IVstHosted> vstProbe = new Dictionary<string, IVstHosted>(StringComparer.OrdinalIgnoreCase);
    static void LoadVst(PluginInfo p, List<string> log)
    {
        string path = p.Path.Length > 0 ? p.Path : Path.Combine(Dir, p.File);
        var probe = VstLoader.Load(path, StudioEngine.SR);   // also tells us its name and whether it is an instrument
        lock (vstProbe) vstProbe[path] = probe;
        string tid = "vst:" + Path.GetFileName(path); bool synth = probe.IsSynth;
        FxRegistry.Register(new FxRegistry.Info(tid, probe.Name + " (VST)", synth ? "Instruments" : "Plugins", (synth ? "VST instrument" : "VST effect") + (probe.Vendor.Length > 0 ? " by " + probe.Vendor : "")),
            () =>
            {
                IVstHosted inst; lock (vstProbe) { vstProbe.TryGetValue(path, out inst); vstProbe.Remove(path); }
                if (inst == null) inst = VstLoader.Load(path, StudioEngine.SR);
                var f = new VstFx(inst, path); f.Prepare(StudioEngine.SR); return f;
            });
        p.Types.Add(tid); p.Loaded = true; loaded[p.File] = p;
        log.Add("Loaded " + probe.Name + (synth ? " (instrument: use + ADD TRACK, then Virtual Instruments, in Purphex Music)" : " (effect: use + Add Effect > PLUGINS)"));
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
                if (p.Native) { LoadVst(p, log); continue; }
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
        BackColor = TM.C(255, 10, 4, 20); ForeColor = TM.C(255, 240, 232, 255); ClientSize = new Size(700, 600); Font = new Font("Segoe UI", 10f); Win.Dark(this);
        Controls.Add(new Label { Text = "Studio effect plugins", Left = 18, Top = 14, Width = 400, Height = 26, Font = new Font("Segoe UI Semibold", 13f), ForeColor = TM.C(255, 225, 190, 255) });
        Controls.Add(new Label { Text = "Your installed VST2 and VST3 plugins are listed here automatically. Tick one and it loads at once: instruments appear under + ADD TRACK > Virtual Instruments in Purphex Music, effects under + Add Effect > PLUGINS. You can also copy a .dll into the plugins folder. Plugins are programs: only tick ones you trust.", Left = 18, Top = 44, Width = 664, Height = 50, ForeColor = TM.C(255, 170, 150, 200) });
        list = new Panel { Left = 18, Top = 100, Width = 664, Height = 200, AutoScroll = true, BackColor = TM.C(255, 8, 4, 16), BorderStyle = BorderStyle.FixedSingle }; Controls.Add(list); Win.DarkControl(list);
        Mk("Open plugins folder", 18, 310, 170, () => { Directory.CreateDirectory(PluginHost.Dir); Process.Start("explorer.exe", "\"" + PluginHost.Dir + "\""); });
        Mk("Reload", 196, 310, 110, () => { Reload(true); });
        Mk("Create sample plugin (for developers)", 314, 310, 290, MakeSample);
        msg = new Label { Left = 18, Top = 352, Width = 664, Height = 40, ForeColor = Color.FromArgb(120, 230, 160) }; Controls.Add(msg);

        Controls.Add(new Label { Text = "Custom ffmpeg filter for conversions (advanced)", Left = 18, Top = 400, Width = 500, Height = 26, Font = new Font("Segoe UI Semibold", 13f), ForeColor = TM.C(255, 225, 190, 255) });
        Controls.Add(new Label { Text = "Any ffmpeg audio filter chain is applied to each download (before loudness normalization). Examples: aecho=0.8:0.88:60:0.4   or   flanger   or   acrusher=bits=8   or   lv2=p=<plugin URI> to host an LV2 plugin you installed.", Left = 18, Top = 430, Width = 664, Height = 52, ForeColor = TM.C(255, 170, 150, 200) });
        filterBox = new TextBox { Left = 18, Top = 486, Width = 664, Text = getFilter(), BackColor = TM.C(255, 8, 4, 16), ForeColor = TM.C(255, 240, 232, 255), BorderStyle = BorderStyle.FixedSingle }; Win.NeonBox(filterBox); Controls.Add(filterBox);
        Mk("Test and save filter", 18, 520, 190, () => TestFilter());
        Mk("Clear", 216, 520, 90, () => { filterBox.Text = ""; setFilter(""); msg.Text = "Filter cleared."; });
        Mk("Close", 582, 548, 100, () => Close());
        Reload(false);
    }

    Button Mk(string text, int x, int y, int w, Action click)
    {
        var b = new Button { Text = text, Left = x, Top = y, Width = w, Height = 32, FlatStyle = FlatStyle.Flat, BackColor = TM.C(255, 118, 52, 212), ForeColor = Color.White, Cursor = Cursors.Hand };
        b.FlatAppearance.BorderColor = TM.C(255, 190, 120, 255); b.Click += (s, e) => click(); Controls.Add(b); return b;
    }

    void MakeSample()
    {
        string d = PluginHost.WriteSdk(Application.ExecutablePath);
        string ask = "This creates a TEMPLATE for people who write their own effects. It is not something you can use straight away.\n\n"
            + "It writes the source code of a simple effect (Sample Tremolo: a volume wobble) plus a build.bat that turns it into a plugin.\n\n"
            + "Files are in:\n" + d + "\n\nBuild it now so you can try it in Purphex Music?";
        var a = MessageBox.Show(this, ask, "Sample plugin", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
        if (a != DialogResult.Yes) { msg.Text = "Sample source written to " + d + " (open build.bat there to build it)."; try { Process.Start("explorer.exe", "\"" + d + "\""); } catch { } return; }
        try
        {
            string csc = Path.Combine(Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows","Microsoft.NET", "Framework64", "v4.0.30319", "csc.exe");
            string args = "/nologo /target:library /out:\"" + Path.Combine(PluginHost.Dir, "SampleTremolo.dll") + "\" /r:\"" + Application.ExecutablePath + "\" SamplePlugin.cs";
            var psi = new ProcessStartInfo(csc, args) { WorkingDirectory = d, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var p = Process.Start(psi))
            {
                string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd(); p.WaitForExit(30000);
                if (p.ExitCode != 0) { msg.Text = "Build failed: " + o.Trim(); return; }
            }
            Reload(true); msg.Text = "Built. Tick Sample Tremolo in the list to approve it, then in Purphex Music use Add Effect > Plugins > Sample Tremolo.";
        }
        catch (Exception ex) { msg.Text = "Couldn't build: " + ex.Message; }
    }

    void Reload(bool load)
    {
        if (load) { var log = PluginHost.LoadEnabled(); msg.Text = log.Count == 0 ? "Nothing new to load." : string.Join("   ", log.ToArray()); }
        list.Controls.Clear(); int y = 6; var ps = PluginHost.Scan();
        if (ps.Count == 0) list.Controls.Add(new Label { Text = "No plugins found yet. Folder: " + PluginHost.Dir, Left = 8, Top = 8, Width = 620, Height = 40, ForeColor = TM.C(255, 170, 150, 200) });
        foreach (var p in ps)
        {
            var cb = new DarkCheck { Left = 6, Top = y, Width = 640, Height = 30, Checked = p.Enabled, Text = p.Name + (p.Native ? "   [VST]" : "") + (p.Loaded ? "   (" + ReadyText(p) + ")" : (p.Enabled ? "   (approved: press Reload)" : "   (not approved)")) + (p.Error.Length > 0 ? "   ERROR: " + p.Error : "") };
            var pi = p; cb.CheckedChanged += (s, e) =>
            {
                bool on = cb.Checked; PluginHost.SetEnabled(pi, on);
                if (!on) { msg.Text = "Disabled. It stays in the list until you restart the app."; return; }
                // load it straight away so it is ready in Purphex Music without pressing anything else
                BeginInvoke(new Action(() => { msg.Text = "Loading... (big instruments can take a few seconds)"; Reload(true); if (pi.Loaded) msg.Text = "Ready: " + ReadyText(pi) + "."; }));
            };
            list.Controls.Add(cb); y += 34;
        }
    }

    static string ReadyText(PluginInfo p)
    {
        bool inst = false; foreach (var i in FxRegistry.All) if (p.Types.Contains(i.Type) && i.Category == "Instruments") inst = true;
        return inst ? "ready: in Purphex Music press + ADD TRACK > Virtual Instruments" : "ready: Purphex Music > + Add Effect > PLUGINS";
    }

    void TestFilter()
    {
        string f = filterBox.Text.Trim();
        if (f.Length == 0) { setFilter(""); msg.Text = "Filter cleared."; return; }
        if (f.IndexOf('"') >= 0 || f.StartsWith("-") || f.IndexOf('\n') >= 0) { msg.ForeColor = TM.C(255, 255, 140, 170); msg.Text = "Don't use double quotes or a leading dash in the filter."; return; }
        string ff = StudioEnv.Ffmpeg(); if (ff == null) { msg.Text = "Convert something once first so ffmpeg is installed."; return; }
        int code; string o = MasterDsp.RunCapture(ff, "-hide_banner -f lavfi -i anullsrc=r=44100:cl=stereo -t 0.2 -af \"" + f + "\" -f null -", null, out code);
        if (code == 0) { setFilter(f); msg.ForeColor = Color.FromArgb(120, 230, 160); msg.Text = "Filter works and is saved. It will be applied to your conversions."; }
        else { msg.ForeColor = TM.C(255, 255, 140, 170); var ls = o.Trim().Split('\n'); msg.Text = "ffmpeg rejected it: " + ls[ls.Length - 1].Trim(); }
    }
}
