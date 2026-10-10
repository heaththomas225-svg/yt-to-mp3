using System;
using System.Collections.Generic;
using System.Windows.Forms;

// Anything that makes sound from notes (a VST synth, or the built-in Bass Lab): live keys, recorded notes, a window.
interface IKeySink { int BaseNote { get; } int Octave { get; set; } bool IsDown(int note); void KeyOn(int note, int vel); void KeyOff(int note); }

abstract class InstrumentFx : Fx
{
    public Track Owner;
    public static Action<Track, bool, int, int> Recorder;   // the studio records notes while REC is armed
    readonly object q = new object(); readonly List<MidiEv> live = new List<MidiEv>(); bool wasTransport;

    public abstract bool IsInstrument { get; }
    public abstract void ShowWindow(IWin32Window owner);
    public abstract void CloseWindow();
    public virtual string WindowLabel { get { return "Open " + Title + " window"; } }
    public virtual bool HasWindow { get { return true; } }

    // ---- live MIDI (keyboard / piano) ----
    public void Midi(byte status, byte d1, byte d2, bool keepAlive = true)
    {
        lock (q) { if (live.Count < 256) live.Add(new MidiEv { S = status, D1 = d1, D2 = d2 }); }
        if (keepAlive) StudioEngine.LiveUntil = Environment.TickCount + (status == 0x80 ? 4000 : 8000);   // keep rendering while it rings out
    }
    public void NoteOn(int note, int vel) { var e = StudioEngine.Current; if (e != null) e.EnsureAudio(); Midi(0x90, (byte)note, (byte)vel); }
    public void NoteOff(int note) { Midi(0x80, (byte)note, 0); }
    public void AllNotesOff() { Midi(0xB0, 123, 0, false); Midi(0xB0, 120, 0, false); }
    // Panic: every note and every ringing tail stops at once.
    public void Panic() { Midi(0xB0, 123, 0, false); Midi(0xB0, 120, 0, false); Midi(0xB0, 64, 0, false); StudioEngine.LiveUntil = Environment.TickCount + 600; }
    public override void Reset() { base.Reset(); try { AllNotesOff(); } catch { } }

    protected virtual void OnBlock(long pos, int n, bool transport) { }
    protected abstract void OnEvents(List<KeyValuePair<int, MidiEv>> events);

    // Called by the engine just before Process for this block.
    public override void BeginBlock(object track, long pos, int n, bool transport)
    {
        var t = track as Track; OnBlock(pos, n, transport);
        var ev = new List<KeyValuePair<int, MidiEv>>();
        if (!transport && wasTransport) ev.Add(new KeyValuePair<int, MidiEv>(0, new MidiEv { S = 0xB0, D1 = 123, D2 = 0 }));   // stopped: let go of every held note (first, so a note played right now still sounds)
        lock (q) { foreach (var m in live) ev.Add(new KeyValuePair<int, MidiEv>(0, m)); live.Clear(); }
        wasTransport = transport;
        if (transport && t != null && t.Midi.Count > 0)
            lock (t.Midi)
            {
                int lo = 0, hi = t.Midi.Count;   // first event at or after pos
                while (lo < hi) { int mid = (lo + hi) / 2; if (t.Midi[mid].Frame < pos) lo = mid + 1; else hi = mid; }
                for (int i = lo; i < t.Midi.Count && t.Midi[i].Frame < pos + n; i++) ev.Add(new KeyValuePair<int, MidiEv>((int)(t.Midi[i].Frame - pos), t.Midi[i]));
            }
        if (ev.Count > 0)
        {
            var idx = new Dictionary<MidiEv, int>(); for (int i = 0; i < ev.Count; i++) idx[ev[i].Value] = i;
            ev.Sort((a, b) => { int c = a.Key.CompareTo(b.Key); return c != 0 ? c : idx[a.Value].CompareTo(idx[b.Value]); });   // in time order, same-time events keep their order
        }
        OnEvents(ev);
    }
}
