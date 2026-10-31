using System;
using System.Collections.Generic;
using System.Windows.Forms;

// Anything that makes sound from notes (a VST synth, or the built-in Bass Lab): live keys, recorded notes, a window.
interface IKeySink { int BaseNote { get; } int Octave { get; set; } bool IsDown(int note); void KeyOn(int note, int vel); void KeyOff(int note); }

// Keeps a sound under full scale without clipping (clipping crackles): when it gets too loud the level comes down smoothly and goes back up
// as the peak fades; below the threshold it does nothing. With look-ahead the sound comes out that many samples later, so the level is already
// down when a peak comes out (no overshoot); without it the odd fast peak goes into a smooth ceiling just under 1.
sealed class Declip
{
    readonly int look; readonly double thr; readonly double[] dl, dr; int di, sr; double gain = 1, peak, att, rel;
    public Declip(int lookAhead, double threshold) { look = lookAhead; thr = threshold; dl = new double[Math.Max(1, look)]; dr = new double[Math.Max(1, look)]; }
    public void Reset() { gain = 1; peak = 0; di = 0; Array.Clear(dl, 0, dl.Length); Array.Clear(dr, 0, dr.Length); }
    public void Run(float[] l, float[] r, int n)
    {
        int s = StudioEngine.SR; if (s != sr) { sr = s; att = look > 0 ? 1 - Math.Exp(-5.0 / look) : 1 - Math.Exp(-1.0 / (0.0005 * s)); rel = Math.Exp(-1.0 / (0.3 * s)); }
        for (int i = 0; i < n; i++)
        {
            double a = l[i], b = r[i], pk = Math.Max(Math.Abs(a), Math.Abs(b));
            if (pk < 1e-9 && peak < thr && gain >= 0.9999 && look == 0) continue;   // quiet: nothing to do
            peak = Math.Max(pk, peak * rel); double want = peak > thr ? thr / peak : 1; gain += (want - gain) * att;
            if (look > 0) { double oa = dl[di], ob = dr[di]; dl[di] = a; dr[di] = b; di = (di + 1) % look; a = oa; b = ob; }
            l[i] = (float)Soft(a * gain); r[i] = (float)Soft(b * gain);
        }
    }
    public static double Soft(double x) { double a = Math.Abs(x); return a <= 0.9 ? x : Math.Sign(x) * (0.9 + 0.1 * Math.Tanh((a - 0.9) / 0.1)); }
}

abstract class InstrumentFx : Fx
{
    public Track Owner;
    public readonly Declip Guard = new Declip(0, 0.88);   // run by the engine on what the instrument plays (any instrument, VSTs too): never clipped
    public static Action<Track, bool, int, int> Recorder;   // the studio records notes while REC is armed
    static readonly MidiEv AllOff = new MidiEv { S = 0xB0, D1 = 123, D2 = 0 };
    readonly object q = new object(); readonly List<MidiEv> live = new List<MidiEv>(); readonly List<int> liveAt = new List<int>(); bool wasTransport;
    // the events handed over each block: two lists used in turn (a new list every block for every instrument was garbage on the sound thread, the kind that makes crackle)
    readonly List<KeyValuePair<int, MidiEv>>[] evBuf = { new List<KeyValuePair<int, MidiEv>>(), new List<KeyValuePair<int, MidiEv>>() }; int evTurn;

    public abstract bool IsInstrument { get; }
    public abstract void ShowWindow(IWin32Window owner);
    public abstract void CloseWindow();
    public virtual string WindowLabel { get { return "Open " + Title + " window"; } }
    public virtual bool HasWindow { get { return true; } }

    // ---- live MIDI (keyboard / piano) ----
    public void Midi(byte status, byte d1, byte d2, bool keepAlive = true)
    {
        lock (q) { if (live.Count < 256) { live.Add(new MidiEv { S = status, D1 = d1, D2 = d2 }); liveAt.Add(Environment.TickCount); } }
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
    internal void FeedEvents(List<KeyValuePair<int, MidiEv>> events) { OnEvents(events); }   // another instrument hands this one its notes for the block (Key Map plays its empty keys on the built-in piano)

    // Called by the engine just before Process for this block.
    public override void BeginBlock(object track, long pos, int n, bool transport)
    {
        var t = track as Track; OnBlock(pos, n, transport);
        evTurn ^= 1; var ev = evBuf[evTurn]; ev.Clear();
        if (!transport && wasTransport) ev.Add(new KeyValuePair<int, MidiEv>(0, AllOff));   // stopped: let go of every held note (first, so a note played right now still sounds)
        lock (q)
        {   // a hit that waited more than half a second (the sound card was off) is dropped: it would come out late, out of nowhere. Note-offs always pass.
            int now = Environment.TickCount;
            for (int i = 0; i < live.Count; i++) { var m = live[i]; if ((m.S & 0xF0) == 0x90 && m.D2 > 0 && unchecked(now - liveAt[i]) > 500) continue; ev.Add(new KeyValuePair<int, MidiEv>(0, m)); }
            live.Clear(); liveAt.Clear();
        }
        wasTransport = transport;
        if (transport && t != null && t.Midi.Count > 0)
            lock (t.Midi)
            {
                int lo = 0, hi = t.Midi.Count;   // first event at or after pos
                while (lo < hi) { int mid = (lo + hi) / 2; if (t.Midi[mid].Frame < pos) lo = mid + 1; else hi = mid; }
                for (int i = lo; i < t.Midi.Count && t.Midi[i].Frame < pos + n; i++) ev.Add(new KeyValuePair<int, MidiEv>((int)(t.Midi[i].Frame - pos), t.Midi[i]));
            }
        for (int i = 1; i < ev.Count; i++) { var x = ev[i]; int j = i - 1; while (j >= 0 && ev[j].Key > x.Key) { ev[j + 1] = ev[j]; j--; } ev[j + 1] = x; }   // in time order, same-time events keep their order (an insertion sort: no garbage)
        OnEvents(ev);
    }
}
