using System;
using System.Collections.Generic;
using System.Windows.Forms;

// =====================================================================================================
//  THE BUILT-IN KEYS: the sound of a MIDI track that has no instrument on it. Whatever you play on a MIDI keyboard (or a MIDI guitar) is
//  heard straight away and the recorded notes play back with it, so a MIDI track works on its own. Bass Lab and the VSTs are extras: put
//  one on the track and it plays the notes instead. A warm piano-like voice: a few harmonics that ring out (the high ones fade first, like
//  a struck string), a soft hammer at the start, louder notes are brighter, the sustain pedal holds notes. 32 notes at once.
// =====================================================================================================
sealed class KeysFx : InstrumentFx
{
    class Voice { public bool On, Held, Pedal; public int Note; public double Vel, Ph, Inc, T, Env, Rel, Pan; public double[] Amp = new double[7], Dec = new double[7]; public uint Seed; }
    readonly Voice[] voices = new Voice[32]; List<KeyValuePair<int, MidiEv>> pending; bool pedal; double lp1L, lp1R;
    static readonly double[] Ratio = { 1, 2, 3, 4, 5, 6, 8 };
    public KeysFx() { Type = "keys"; Title = "Built-in keys"; Category = "Instruments"; for (int i = 0; i < voices.Length; i++) voices[i] = new Voice(); }
    public override bool IsInstrument { get { return true; } }
    public override bool HasWindow { get { return false; } }
    public override void ShowWindow(IWin32Window owner) { }
    public override void CloseWindow() { }
    public override void Reset() { base.Reset(); foreach (var v in voices) { v.On = false; v.Env = 0; } pedal = false; lp1L = lp1R = 0; }
    protected override void OnEvents(List<KeyValuePair<int, MidiEv>> ev) { pending = ev; }

    void Start(int note, int vel)
    {
        Voice v = null; foreach (var x in voices) if (x.On && x.Note == note) { v = x; break; }   // the same key again: it is struck again
        if (v == null) foreach (var x in voices) if (!x.On) { v = x; break; }
        if (v == null) { v = voices[0]; foreach (var x in voices) if (x.T > v.T) v = x; }   // all busy: the oldest note makes room
        double f = 440.0 * Math.Pow(2, (note - 69) / 12.0); v.On = true; v.Held = true; v.Pedal = false; v.Note = note; v.Vel = vel / 127.0; v.Inc = f / SR; v.Ph = 0; v.T = 0; v.Env = 1; v.Rel = 0; v.Pan = Math.Max(-0.5, Math.Min(0.5, (note - 60) / 60.0)); v.Seed = (uint)(note * 7919 + 1);
        double bright = 0.35 + 0.65 * v.Vel, low = Math.Max(0, (60 - note) / 48.0);
        for (int h = 0; h < Ratio.Length; h++)
        {
            double fh = f * Ratio[h]; v.Amp[h] = fh > SR * 0.45 ? 0 : Math.Pow(0.55 + 0.25 * bright, h) / (1 + h * 0.35) * (h == 0 ? 1 : bright);
            double secs = (2.6 + 2.5 * low) / (1 + h * 0.9) / (1 + Math.Max(0, note - 72) / 24.0); v.Dec[h] = Math.Exp(-1.0 / (secs * SR));   // high harmonics and high notes fade sooner
        }
    }
    void Stop(int note) { foreach (var v in voices) if (v.On && v.Note == note && v.Held) { v.Held = false; if (pedal) v.Pedal = true; else v.Rel = 1; } }

    public override void Process(float[] l, float[] r, int n)
    {
        var ev = pending; pending = null; int ei = 0; double relK = Math.Exp(-1.0 / (0.16 * SR)), lpK = 0.35;
        for (int i = 0; i < n; i++)
        {
            while (ev != null && ei < ev.Count && ev[ei].Key <= i)
            {
                var m = ev[ei].Value; int kind = m.S & 0xF0; ei++;
                if (kind == 0x90 && m.D2 > 0) Start(m.D1, m.D2);
                else if (kind == 0x80 || kind == 0x90) Stop(m.D1);
                else if (kind == 0xB0 && m.D1 == 64) { pedal = m.D2 >= 64; if (!pedal) foreach (var v in voices) if (v.On && v.Pedal) { v.Pedal = false; v.Rel = 1; } }
                else if (kind == 0xB0 && (m.D1 == 123 || m.D1 == 120)) { foreach (var v in voices) if (v.On) { v.Held = false; v.Pedal = false; v.Rel = 1; } pedal = false; }
            }
            double sl = 0, sr = 0;
            for (int k = 0; k < voices.Length; k++)
            {
                var v = voices[k]; if (!v.On) continue;
                double s = 0, ph = v.Ph * 2 * Math.PI;
                for (int h = 0; h < Ratio.Length; h++) { if (v.Amp[h] < 1e-5) continue; s += v.Amp[h] * Math.Sin(ph * Ratio[h] * (1 + h * h * 0.0004)); v.Amp[h] *= v.Dec[h]; }   // (a slight stretch of the high harmonics, like a real string)
                if (v.T < SR * 0.012) { v.Seed = v.Seed * 1664525u + 1013904223u; s += ((v.Seed >> 9) / 4194304.0 - 1.0) * 0.18 * v.Vel * (1 - v.T / (SR * 0.012)); }   // the hammer
                double att = Math.Min(1, v.T / (SR * 0.002)); if (v.Rel > 0) { v.Env *= relK; if (v.Env < 1e-4) { v.On = false; continue; } }
                s *= att * v.Env * (0.25 + 0.75 * v.Vel) * 0.22; sl += s * (1 - v.Pan); sr += s * (1 + v.Pan);
                v.Ph += v.Inc; if (v.Ph >= 1) v.Ph -= 1; v.T++;
                if (v.Amp[0] < 2e-4 && v.Rel == 0) { v.Rel = 1; }   // rung out
            }
            lp1L += (sl - lp1L) * lpK; lp1R += (sr - lp1R) * lpK;   // a gentle top-end rounding
            l[i] += (float)Math.Max(-1.2, Math.Min(1.2, lp1L)); r[i] += (float)Math.Max(-1.2, Math.Min(1.2, lp1R));
        }
    }
}
