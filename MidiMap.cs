using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

// MIDI mapping for the whole studio: a knob, fader, key or pad of your controller (a source: "cc:1:21" or "note:10:36") drives a control
// (a target: a plugin's knob, a track's volume / pan / mute / solo / trim, PLAY / STOP / MIDI REC, a Beat Lab row, a pad of Purphex Pads...).
// MIDI Learn: right-click a control, pick MIDI Learn, then move a knob or hit a key or pad. The maps are kept per controller profile
// (as many as you like), saved as you go and loaded when the studio opens. Notes that are not mapped still play the instruments.
static class MidiMap
{
    public class Map { public string Src = "", Target = "", Label = ""; }
    static readonly object gate = new object();
    static List<Map> maps = new List<Map>();
    public static string Profile = "My controller";
    public static volatile string Learning; static string learnLabel = ""; public static string LearningLabel { get { return learnLabel; } }
    public static volatile int ActivityAt; public static volatile string LastIn = "";   // the light next to MIDI REC, and what came in last
    public static Func<string, float, bool, bool> Apply;   // the studio: drive a target with a value 0..1 (cc: a knob or fader; else a key or pad), true when it was done
    public static Action<string, string> Learned;          // the studio: a learn finished (target label, source)
    static readonly Dictionary<string, float> lastVal = new Dictionary<string, float>();

    public static List<Map> All { get { lock (gate) return maps.Select(m => new Map { Src = m.Src, Target = m.Target, Label = m.Label }).ToList(); } }
    public static Map ForTarget(string target) { lock (gate) return maps.FirstOrDefault(m => m.Target == target); }
    public static string SrcText(string src)
    {
        var f = src.Split(':'); if (f.Length < 3) return src; int n; int.TryParse(f[2], out n);
        return f[0] == "cc" ? "Knob / slider CC " + n + " (channel " + f[1] + ")" : "Key / pad " + PadsFx.NoteName(n) + " (channel " + f[1] + ")";
    }

    // ---- from the MIDI thread, before the notes go to an instrument ----
    public static bool Handle(byte s, byte d1, byte d2)
    {
        int kind = s & 0xF0, ch = (s & 0x0F) + 1; if (kind != 0xB0 && kind != 0x90 && kind != 0x80) return false;
        bool cc = kind == 0xB0, on = kind == 0x90 && d2 > 0; string src = (cc ? "cc:" : "note:") + ch + ":" + d1;
        ActivityAt = Environment.TickCount; LastIn = cc ? "CC " + d1 + " = " + d2 + "  (channel " + ch + ")" : (on ? "Note on " : "Note off ") + PadsFx.NoteName(d1) + "  velocity " + d2 + "  (channel " + ch + ")";
        string lt = Learning;
        if (lt != null)
        {
            if (!cc && !on) return true;   // (the key coming back up after a learn)
            Assign(src, lt, learnLabel); Learning = null; var cb = Learned; if (cb != null) cb(learnLabel, src); return true;
        }
        Map m; lock (gate) m = maps.FirstOrDefault(x => x.Src == src); if (m == null) return false;
        var ap = Apply; if (ap == null) return true;
        float v = cc ? d2 / 127f : (on ? d2 / 127f : 0f);
        try { ap(m.Target, v, cc); } catch { }
        return true;   // a mapped key or knob does its job and plays nothing else
    }
    // a button driven by a knob or key: it acts when it goes over half way (a CC button sends 127 then 0; a key sends a note on)
    public static bool Pressed(string target, float v, bool cc)
    {
        if (!cc) return v > 0; lock (gate) { float was; lastVal.TryGetValue(target, out was); lastVal[target] = v; return v >= 0.5f && was < 0.5f; }
    }

    // ---- learning, removing ----
    public static void StartLearn(string target, string label) { learnLabel = label; Learning = target; }
    public static void CancelLearn() { Learning = null; }
    public static void Assign(string src, string target, string label)
    {
        lock (gate) { maps.RemoveAll(m => m.Src == src || m.Target == target); maps.Add(new Map { Src = src, Target = target, Label = label }); }
        Save();
    }
    public static void Remove(string target) { lock (gate) maps.RemoveAll(m => m.Target == target); Save(); }
    public static void Clear() { lock (gate) maps.Clear(); Save(); }

    // ---- profiles: a file each in the MIDI folder; the one in use is remembered ----
    static string Dir { get { return Path.Combine(StudioEnv.DataDir, "midi"); } }
    static string FileOf(string profile) { return Path.Combine(Dir, Presets.SafeName(profile) + ".txt"); }
    public static List<string> Profiles() { try { Directory.CreateDirectory(Dir); return Directory.GetFiles(Dir, "*.txt").Select(Path.GetFileNameWithoutExtension).Where(n => n != "_current").OrderBy(n => n).ToList(); } catch { return new List<string>(); } }
    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir); List<Map> copy; lock (gate) copy = maps.ToList();
            File.WriteAllLines(FileOf(Profile), copy.Select(m => m.Src + "\t" + m.Target + "\t" + m.Label.Replace("\t", " ")));
            File.WriteAllText(Path.Combine(Dir, "_current.txt"), Profile);
        }
        catch { }
    }
    public static void Load(string profile = null)
    {
        try
        {
            Directory.CreateDirectory(Dir); string cur = Path.Combine(Dir, "_current.txt");
            if (profile == null) profile = File.Exists(cur) ? File.ReadAllText(cur).Trim() : "My controller"; if (profile.Length == 0) profile = "My controller";
            var list = new List<Map>(); string f = FileOf(profile);
            if (File.Exists(f)) foreach (var line in File.ReadAllLines(f)) { var p = line.Split('\t'); if (p.Length >= 2 && p[0].Length > 0) list.Add(new Map { Src = p[0], Target = p[1], Label = p.Length > 2 ? p[2] : p[1] }); }
            lock (gate) { maps = list; lastVal.Clear(); } Profile = profile; File.WriteAllText(cur, Profile); if (!File.Exists(f)) Save();
        }
        catch { }
    }
    public static void NewProfile(string name) { Save(); lock (gate) maps = new List<Map>(); Profile = Presets.SafeName(name); Save(); }
    public static void CopyProfile(string name) { Save(); Profile = Presets.SafeName(name); Save(); }
    public static void RenameProfile(string name) { string old = FileOf(Profile); Profile = Presets.SafeName(name); Save(); try { if (old != FileOf(Profile)) File.Delete(old); } catch { } }
    public static void DeleteProfile() { try { File.Delete(FileOf(Profile)); } catch { } var left = Profiles(); Load(left.Count > 0 ? left[0] : "My controller"); }
    // a profile for the Akai MPK Mini: its 8 knobs (CC 70 to 77) turn the first 8 knobs of the plugin in front; its 8 pads (notes 36 to 43) already play pads 1 to 8 of Purphex Pads and the Beat Lab
    public static void MakeMpkMini()
    {
        Save(); var list = new List<Map>(); for (int k = 0; k < 8; k++) list.Add(new Map { Src = "cc:1:" + (70 + k), Target = "selfx|" + (k + 1), Label = "Knob " + (k + 1) + " of the plugin in front" });
        lock (gate) maps = list; Profile = "Akai MPK Mini"; Save();
    }

    // ---- the menu a control gets on right-click (the studio and the plugin windows use it) ----
    public static void Menu(Control owner, Point at, string target, string label)
    {
        var m = Win.NewMenu(); m.Items.Add(new ToolStripMenuItem("MIDI: " + label) { Enabled = false });
        if (target == null) { m.Items.Add(new ToolStripMenuItem("Put this plugin on a track first, then it can be MIDI mapped") { Enabled = false }); m.Show(owner, at); return; }
        var ex = ForTarget(target);
        m.Items.Add(ex == null ? "MIDI Learn (then move a knob or press a key or pad on your controller)" : "MIDI Learn again (assign something else)").Click += (a, b) => StartLearn(target, label);
        if (ex != null) { m.Items.Add(new ToolStripMenuItem("Mapped to: " + SrcText(ex.Src)) { Enabled = false }); m.Items.Add("Remove this MIDI mapping").Click += (a, b) => Remove(target); }
        m.Show(owner, at);
    }
    // the target of a plugin's knob: its track (by name; "" is the master) and its place in the chain
    public static string FxTarget(Fx f, string paramId)
    {
        var e = StudioEngine.Current; if (e == null || f == null) return null;
        lock (e.Sync)
        {
            int mi = e.MasterFx.IndexOf(f); if (mi >= 0) return "fx||" + mi + "|" + f.Type + "|" + paramId;
            foreach (var t in e.Tracks) { int i = t.Fx.IndexOf(f); if (i >= 0) return "fx|" + t.Name + "|" + i + "|" + f.Type + "|" + paramId; }
        }
        return null;
    }
}
