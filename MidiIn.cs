using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

// External MIDI keyboards / controllers (Windows winmm). Every MIDI input is listened to at once (a keyboard often has two ports, e.g. its keys
// and a "DAW" or control port: listening to only the first one, the keys could be on the port nobody listened to). One port can be picked instead.
static class MidiInput
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)] struct MIDIINCAPS { public ushort mid, pid; public uint ver; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string name; public uint support; }
    delegate void MidiInProc(IntPtr h, uint msg, IntPtr inst, IntPtr p1, IntPtr p2);
    [DllImport("winmm.dll")] static extern int midiInGetNumDevs();
    [DllImport("winmm.dll", CharSet = CharSet.Auto)] static extern int midiInGetDevCaps(IntPtr id, ref MIDIINCAPS caps, int size);
    [DllImport("winmm.dll")] static extern int midiInOpen(out IntPtr h, int id, MidiInProc cb, IntPtr inst, int flags);
    [DllImport("winmm.dll")] static extern int midiInStart(IntPtr h);
    [DllImport("winmm.dll")] static extern int midiInStop(IntPtr h);
    [DllImport("winmm.dll")] static extern int midiInReset(IntPtr h);
    [DllImport("winmm.dll")] static extern int midiInClose(IntPtr h);
    const int CALLBACK_FUNCTION = 0x30000, MIM_DATA = 0x3C3;

    static readonly MidiInProc cb = OnMsg;   // kept alive
    static readonly object gate = new object();
    static readonly Dictionary<string, IntPtr> open = new Dictionary<string, IntPtr>();      // port name -> handle
    static readonly Dictionary<IntPtr, string> nameOf = new Dictionary<IntPtr, string>();
    static readonly Dictionary<string, string> problem = new Dictionary<string, string>();   // a port that could not be opened, and why
    static readonly Dictionary<string, int> count = new Dictionary<string, int>();          // messages from each port
    public static volatile InstrumentFx Target;                  // the instrument that receives the notes
    public static Func<byte, byte, byte, bool> BareTrack;         // the studio: plays (and records) the notes on a track (true = taken)
    public static string Status = "MIDI: not started"; public static int Received; public const string OffName = "-";   // Prefs.MidiDev: "" = every MIDI input, "-" = off, else that one
    public static string OpenName { get { lock (gate) return string.Join(", ", open.Keys); } }

    public static List<string> Devices()
    {
        var l = new List<string>();
        try { int n = midiInGetNumDevs(); for (int i = 0; i < n; i++) { var c = new MIDIINCAPS(); string nm = midiInGetDevCaps((IntPtr)i, ref c, Marshal.SizeOf(typeof(MIDIINCAPS))) == 0 ? c.name : "MIDI device " + i; while (l.Contains(nm)) nm += " (2)"; l.Add(nm); } } catch { }
        return l;
    }
    // what each port is doing, for the check in MIDI MAPPING
    public static List<string> Report()
    {
        var l = new List<string>(); var d = Devices();
        lock (gate) foreach (var n in d) { int c; count.TryGetValue(n, out c); string p; l.Add(n + ": " + (open.ContainsKey(n) ? "LISTENING, " + c + " message" + (c == 1 ? "" : "s") + " so far" : (problem.TryGetValue(n, out p) ? p : "not listened to"))); }
        if (d.Count == 0) l.Add("Windows reports no MIDI input at all: is the keyboard plugged in (and its driver installed)?");
        return l;
    }
    static bool OpenOne(List<string> d, string name)
    {
        int idx = d.IndexOf(name); if (idx < 0) return false; IntPtr h;
        try
        {
            int r = midiInOpen(out h, idx, cb, IntPtr.Zero, CALLBACK_FUNCTION);
            if (r != 0) { lock (gate) problem[name] = r == 4 ? "IN USE BY ANOTHER PROGRAM (close it and Purphex picks it up)" : "could not be opened (error " + r + ")"; return false; }
            midiInStart(h); lock (gate) { open[name] = h; nameOf[h] = name; problem.Remove(name); } return true;
        }
        catch (Exception ex) { lock (gate) problem[name] = "unavailable: " + ex.Message; return false; }
    }
    // every MIDI input: opens the ones not open yet, closes the ones that were unplugged. Returns the names newly opened.
    public static List<string> OpenAll()
    {
        var d = Devices(); var added = new List<string>(); List<string> gone; lock (gate) gone = open.Keys.Where(k => !d.Contains(k)).ToList();
        foreach (var g in gone) CloseOne(g);
        foreach (var n in d) { bool has; lock (gate) has = open.ContainsKey(n); if (!has && OpenOne(d, n)) added.Add(n); }
        Status = OpenName.Length > 0 ? "MIDI: listening to " + OpenName : (d.Count == 0 ? "No MIDI device found." : "MIDI: no input could be opened");
        return added;
    }
    // just this port ("" or off: close them all). Returns true when it is listening.
    public static bool Open(string name)
    {
        Close(); if (string.IsNullOrEmpty(name) || name == OffName) { Status = "MIDI input is off"; return false; }
        var d = Devices();
        if (d.Count == 0) { Status = "No MIDI device found. Plug in a MIDI keyboard, then pick it again."; return false; }
        if (!d.Contains(name)) { Status = "MIDI device '" + name + "' is not connected."; return false; }
        bool ok = OpenOne(d, name); string p; lock (gate) problem.TryGetValue(name, out p);
        Status = ok ? "MIDI: listening to " + name : "MIDI device '" + name + "' " + (p ?? "could not be opened") + "."; return ok;
    }
    static void CloseOne(string name)
    {
        IntPtr h; lock (gate) { if (!open.TryGetValue(name, out h)) return; open.Remove(name); nameOf.Remove(h); }
        try { midiInStop(h); midiInReset(h); midiInClose(h); } catch { }
    }
    public static void Close() { List<string> all; lock (gate) all = open.Keys.ToList(); foreach (var n in all) CloseOne(n); }

    static void OnMsg(IntPtr h, uint msg, IntPtr inst, IntPtr p1, IntPtr p2)
    {
        if (msg != MIM_DATA) return;
        lock (gate) { string n; if (nameOf.TryGetValue(h, out n)) { int c; count.TryGetValue(n, out c); count[n] = c + 1; } }
        Handle((uint)(long)p1);
    }

    // One packed MIDI message: status | data1 << 8 | data2 << 16.
    public static void Handle(uint m)
    {
        byte s = (byte)(m & 0xFF), d1 = (byte)((m >> 8) & 0xFF), d2 = (byte)((m >> 16) & 0xFF); if (s >= 0xF0) return;
        if (MidiMap.Handle(s, d1, d2)) { Received++; return; }   // a mapped knob, key or pad (or a MIDI Learn) does its job and plays nothing else
        var bt = BareTrack; if (bt != null && bt(s, d1, d2)) { Received++; return; }   // the studio plays it on a track (the picked one, else the first MIDI track)
        var t = Target; if (t == null) return;
        if ((s & 0xF0) == 0x90 && d2 > 0) { var se = StudioEngine.Current; if (se != null) se.EnsureAudio(); }   // a key or pad hit wakes the sound card if it is off (it stays open once it is)
        Received++; t.Midi(s, d1, d2);
        int kind = s & 0xF0; var rec = InstrumentFx.Recorder;
        if (rec != null && (kind == 0x90 || kind == 0x80)) { bool on = kind == 0x90 && d2 > 0; rec(t.Owner, on, d1, on ? d2 : 0); }
    }
}
