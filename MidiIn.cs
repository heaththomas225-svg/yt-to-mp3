using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

// External MIDI keyboards / controllers (Windows winmm). Notes go to whichever instrument window is in front.
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

    static IntPtr dev; static readonly MidiInProc cb = OnMsg;   // kept alive
    public static volatile InstrumentFx Target;                  // the instrument that receives the notes
    public static string Status = "MIDI: not started"; public static string OpenName = ""; public static int Received;

    public static List<string> Devices()
    {
        var l = new List<string>();
        try { int n = midiInGetNumDevs(); for (int i = 0; i < n; i++) { var c = new MIDIINCAPS(); if (midiInGetDevCaps((IntPtr)i, ref c, Marshal.SizeOf(typeof(MIDIINCAPS))) == 0) l.Add(c.name); else l.Add("MIDI device " + i); } } catch { }
        return l;
    }

    // Opens the device with this name ("" = close). Returns true when it is listening.
    public static bool Open(string name)
    {
        Close(); if (string.IsNullOrEmpty(name)) { Status = "MIDI input is off"; return false; }
        var d = Devices(); int idx = d.IndexOf(name);
        if (d.Count == 0) { Status = "No MIDI device found. Plug in a MIDI keyboard, then pick it again."; return false; }
        if (idx < 0) { Status = "MIDI device '" + name + "' is not connected."; return false; }
        try
        {
            int r = midiInOpen(out dev, idx, cb, IntPtr.Zero, CALLBACK_FUNCTION);
            if (r != 0) { dev = IntPtr.Zero; Status = r == 4 ? "MIDI device '" + name + "' is in use by another program." : "Couldn't open '" + name + "' (error " + r + ")."; return false; }
            midiInStart(dev); OpenName = name; Status = "MIDI: listening to " + name; return true;
        }
        catch (Exception ex) { Status = "MIDI is unavailable: " + ex.Message; return false; }
    }
    public static void Close()
    {
        if (dev != IntPtr.Zero) { try { midiInStop(dev); midiInReset(dev); midiInClose(dev); } catch { } dev = IntPtr.Zero; }
        OpenName = "";
    }

    static void OnMsg(IntPtr h, uint msg, IntPtr inst, IntPtr p1, IntPtr p2) { if (msg == MIM_DATA) Handle((uint)(long)p1); }

    // One packed MIDI message: status | data1 << 8 | data2 << 16.
    public static void Handle(uint m)
    {
        byte s = (byte)(m & 0xFF), d1 = (byte)((m >> 8) & 0xFF), d2 = (byte)((m >> 16) & 0xFF); var t = Target; if (t == null || s >= 0xF0) return;
        Received++; t.Midi(s, d1, d2);
        int kind = s & 0xF0; var rec = InstrumentFx.Recorder;
        if (rec != null && (kind == 0x90 || kind == 0x80)) { bool on = kind == 0x90 && d2 > 0; rec(t.Owner, on, d1, on ? d2 : 0); }
    }
}
