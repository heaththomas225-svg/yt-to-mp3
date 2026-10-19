using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

// ======================================================================
//  PURPHEX DRUM2MIDI: pick a drum track (a beat, a loop, a recorded kit) and it listens for the KICKS, SNARES and HATS and writes them onto
//  its own track as notes (kick 36, snare 38, hat 42), in time with the song if you like (QUANTIZE), with how hard each was hit (VELOCITY).
//  A built-in kit plays them (kick, snare, clap, closed and open hat, each tunable), or move the notes to any drum instrument.
// ======================================================================

// the audio of one row of the project as the song plays it (its own clip and the pieces on its row, with their fades and gain), in mono
partial class StudioEngine
{
    public float[] RowMono(Track row, out long start)
    {
        start = 0; var parts = new List<Track>(); lock (Sync) { foreach (var t in Tracks) if (t == row || (t.Host == row && MergedPiece(t, Tracks))) if (t.Clip != null && t.Clip.Loaded) parts.Add(t); }
        if (parts.Count == 0) return new float[0];
        long a = long.MaxValue, b = 0; foreach (var p in parts) { a = Math.Min(a, p.Offset); b = Math.Max(b, p.Offset + p.UsedLength); }
        if (b <= a) return new float[0]; long len = Math.Min(b - a, (long)SR * 60 * 20); start = a;
        var outp = new float[len]; var bl = new float[4096]; var br = new float[4096];
        for (long pos = 0; pos < len; pos += 4096) { int n = (int)Math.Min(4096, len - pos); Array.Clear(bl, 0, n); Array.Clear(br, 0, n); foreach (var p in parts) AddClipAudio(p, bl, br, n, a + pos); for (int i = 0; i < n; i++) outp[pos + i] = (bl[i] + br[i]) * 0.5f; }
        return outp;
    }
}

class Drum2MidiFx : InstrumentFx
{
    public const int KICK = 0, SNARE = 1, HAT = 2;
    public struct DrumHit { public long Frame; public int Kind; public int Vel; }
    public static readonly string[] Quant = { "Off (as played)", "1/8", "1/16", "1/32" }, VelModes = { "From the hits", "All the same" };
    public static readonly int[] Notes = { 36, 38, 42 };
    class Voice { public bool On; public int Kind; public double T, Ph, Vel, N1, N2, Hp, Lp; }
    readonly Voice[] voices = new Voice[12]; List<KeyValuePair<int, MidiEv>> pending; uint seed = 17;
    public List<DrumHit> LastHits = new List<DrumHit>(); public long LastLength; public string LastSource = "";
    public volatile int Flash = -1;
    public Drum2MidiFx()
    {
        Type = "pxdrum2midi"; Title = "Purphex Drum2MIDI"; Category = "Instruments"; Blurb = "Turns a drum track into notes (kick, snare, hats) and plays them with its own kit";
        AddP("src", "Drum track", 0, 32, 0, ""); AddP("sens_k", "Kick sensitivity", 0, 100, 60, "%"); AddP("sens_s", "Snare sensitivity", 0, 100, 60, "%"); AddP("sens_h", "Hat sensitivity", 0, 100, 60, "%");
        AddC("quant", "Quantize", Quant, 0); AddC("velmode", "Velocity", VelModes, 0);
        AddP("k_tune", "Kick tune", 30, 100, 50, "Hz"); AddP("k_punch", "Kick punch", 0, 100, 60, "%"); AddP("k_decay", "Kick decay", 50, 1500, 420, "ms"); AddP("k_lvl", "Kick level", -24, 6, 0, "dB");
        AddP("s_tone", "Snare tone", 100, 400, 190, "Hz"); AddP("s_snap", "Snare snap", 0, 100, 65, "%"); AddP("s_decay", "Snare decay", 40, 800, 180, "ms"); AddP("s_lvl", "Snare level", -24, 6, 0, "dB");
        AddP("h_tone", "Hat tone", 3000, 14000, 8000, "Hz"); AddP("h_decay", "Hat decay", 10, 400, 55, "ms"); AddP("h_lvl", "Hat level", -24, 6, -4, "dB"); AddP("level", "Level", -24, 12, -3, "dB");
        for (int i = 0; i < voices.Length; i++) voices[i] = new Voice();
    }
    public override bool IsInstrument { get { return true; } }
    public override void ShowWindow(IWin32Window owner) { PxWindow.Show(this, owner, () => new Drum2MidiWindow(this)); }
    public override void CloseWindow() { }
    public override string WindowLabel { get { return "Open Purphex Drum2MIDI"; } }
    public override void Reset() { base.Reset(); foreach (var v in voices) v.On = false; }
    protected override void OnEvents(List<KeyValuePair<int, MidiEv>> ev) { pending = ev; }
    // which kit piece a note plays: 35/36 kick, 37-40 snare and clap, 42/44 closed hat, 46 open hat (anything else: the nearest)
    static int PieceOf(int note) { if (note <= 36) return 0; if (note == 39) return 3; if (note <= 40) return 1; if (note == 46 || note >= 49) return 4; return 2; }
    void Event(MidiEv e)
    {
        int st = e.S & 0xF0; if (!(st == 0x90 && e.D2 > 0)) { if (st == 0xB0 && e.D1 == 120) foreach (var v in voices) v.On = false; return; }
        int k = PieceOf(e.D1); if (k == 2) foreach (var x in voices) if (x.On && x.Kind == 4) x.On = false;   // a closed hat chokes the open one
        Voice vv = null; foreach (var x in voices) if (!x.On) { vv = x; break; } if (vv == null) { vv = voices[0]; foreach (var x in voices) if (x.T > vv.T) vv = x; }
        vv.On = true; vv.Kind = k; vv.T = 0; vv.Ph = 0; vv.Vel = e.D2 / 127.0; vv.N1 = vv.N2 = vv.Hp = vv.Lp = 0; Flash = k;
    }
    double Noise() { seed = seed * 1664525 + 1013904223; return (seed >> 8) / 8388608.0 - 1; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); var ev = pending; pending = null; int ei = 0; double dt = 1.0 / SR;
        double kT = Get("k_tune"), kP = Get("k_punch") / 100, kD = Get("k_decay") * 0.001, kL = Math.Pow(10, Get("k_lvl") / 20), sT = Get("s_tone"), sS = Get("s_snap") / 100, sD = Get("s_decay") * 0.001, sL = Math.Pow(10, Get("s_lvl") / 20);
        double hC = Math.Exp(-2 * Math.PI * Get("h_tone") / SR), hD = Get("h_decay") * 0.001, hL = Math.Pow(10, Get("h_lvl") / 20), lvl = Math.Pow(10, Get("level") / 20), snC = Math.Exp(-2 * Math.PI * 1500.0 / SR), clC = Math.Exp(-2 * Math.PI * 900.0 / SR);
        for (int i = 0; i < n; i++)
        {
            while (ev != null && ei < ev.Count && ev[ei].Key <= i) { Event(ev[ei].Value); ei++; }
            double s = 0;
            foreach (var v in voices)
            {
                if (!v.On) continue; double t = v.T, y, env;
                switch (v.Kind)
                {
                    case 0: { double f = kT * (1 + kP * 4 * Math.Exp(-t / 0.025)); v.Ph += f * dt; env = Math.Exp(-t / (kD * 0.35)); y = Math.Sin(2 * Math.PI * v.Ph) * env * kL + (t < 0.002 ? Noise() * 0.3 * kP * kL : 0); break; }
                    case 1: { v.Ph += sT * dt; double nz = Noise(); v.Hp = snC * (v.Hp + nz - v.N1); v.N1 = nz; env = Math.Exp(-t / (sD * 0.35)); y = (Math.Sin(2 * Math.PI * v.Ph) * Math.Exp(-t / 0.05) * (1 - sS * 0.6) + v.Hp * sS * env) * sL; env = Math.Max(env, Math.Exp(-t / 0.05)); break; }
                    case 3: { double nz = Noise(); v.Hp = clC * (v.Hp + nz - v.N1); v.N1 = nz; double burst = t < 0.03 ? (0.5 + 0.5 * Math.Cos(2 * Math.PI * t / 0.01)) : Math.Exp(-(t - 0.03) / (sD * 0.4)); env = t < 0.03 ? 1 : burst; y = v.Hp * burst * sL * 0.9; break; }
                    default: { double nz = Noise(); v.Hp = hC * (v.Hp + nz - v.N1); v.N1 = nz; env = Math.Exp(-t / ((v.Kind == 4 ? hD * 6 : hD) * 0.35)); y = v.Hp * env * hL * 0.8; break; }
                }
                s += y * (0.4 + 0.6 * v.Vel); v.T += dt; if (env < 1e-4 || v.T > 4) v.On = false;
            }
            s *= lvl * 0.7; if (double.IsNaN(s) || double.IsInfinity(s)) { Reset(); s = 0; }
            l[i] = r[i] = (float)s;
        }
        while (ev != null && ei < ev.Count) { Event(ev[ei].Value); ei++; }
    }

    // ---- listening: three bands (lows for the kick, the snare's body and crack, the hats' highs), a sharp rise in a band is a hit ----
    public static List<DrumHit> Detect(float[] x, int sr, double sensK, double sensS, double sensH)
    {
        // five bands: 0 lows (the kick), 1 body (a snare's tone), 2 crack (a snare's snap), 3 highs (the hats), 4 air (a hat over the tail of a snare stands out most up here)
        const int HOP = 256, B = 5; int frames = x.Length / HOP; var hits = new List<DrumHit>(); if (frames < 8) return hits;
        var L = new double[B][]; for (int b = 0; b < B; b++) L[b] = new double[frames];
        double g120 = Math.Tan(Math.PI * 120.0 / sr), g150 = Math.Tan(Math.PI * 150.0 / sr), g400 = Math.Tan(Math.PI * 400.0 / sr), g1k = Math.Tan(Math.PI * 1000.0 / sr), g5k = Math.Tan(Math.PI * 5000.0 / sr), g8k = Math.Tan(Math.PI * 8000.0 / sr), g12k = Math.Tan(Math.PI * Math.Min(12000.0, sr * 0.4) / sr);
        var st = new double[20]; var P = new double[B];
        for (int f = 0; f < frames; f++)
        {
            Array.Clear(P, 0, B);
            for (int i = f * HOP; i < f * HOP + HOP; i++)
            {
                double v = x[i];
                double lo = Svf(Svf(v, g120, 1.4, ref st[0], ref st[1], 0), g120, 1.4, ref st[2], ref st[3], 0); P[0] += lo * lo;
                double bo = Svf(Svf(v, g150, 1.4, ref st[4], ref st[5], 1), g400, 1.4, ref st[6], ref st[7], 0); P[1] += bo * bo;
                double cr = Svf(Svf(v, g1k, 1.4, ref st[8], ref st[9], 1), g5k, 1.4, ref st[10], ref st[11], 0); P[2] += cr * cr;
                double hi = Svf(Svf(v, g8k, 1.4, ref st[12], ref st[13], 1), g8k, 1.4, ref st[14], ref st[15], 1); P[3] += hi * hi;
                double air = Svf(Svf(v, g12k, 1.4, ref st[16], ref st[17], 1), g12k, 1.4, ref st[18], ref st[19], 1); P[4] += air * air;
            }
            for (int b = 0; b < B; b++) L[b][f] = 10 * Math.Log10(P[b] / HOP + 1e-12);
        }
        { var raw = (double[])L[0].Clone(); for (int f = 0; f < frames; f++) { double e = 0; int n = 0; for (int k = Math.Max(0, f - 3); k <= f; k++) { e += Math.Pow(10, raw[k] / 10); n++; } L[0][f] = 10 * Math.Log10(e / n); } }   // the lows over ~23 ms: a low kick swings a short frame up and down
        var top = new double[B]; for (int b = 0; b < B; b++) { top[b] = -200; foreach (double v in L[b]) top[b] = Math.Max(top[b], v); }
        // how sharp a rise must be, and how close to the loudest moment of that band (more sensitive = smaller rises and quieter hits count)
        double[] sens = { sensK, sensS, sensS, sensH, sensH }; var rise = new double[B]; var floor = new double[B];
        for (int b = 0; b < B; b++) { rise[b] = b >= 3 ? 2.5 + (1 - sens[b]) * 10 : 4 + (1 - sens[b]) * 14; floor[b] = top[b] - (8 + sens[b] * 20); }   // (a hat can sit on the tail of a snare: a smaller rise counts there)
        Func<int, int, double> at = (b, f) => f < 0 ? -120 : L[b][f];   // before the start is silence (a hit on the very first beat counts)
        Func<int, int, double> before = (b, f) => Math.Min(at(b, f - 1), Math.Min(at(b, f - 2), at(b, f - 3)));
        Func<int, int, double> peak = (b, f) => { double m = -200; for (int k = f; k < Math.Min(frames, f + 4); k++) m = Math.Max(m, L[b][k]); return m; };
        int gapK = (int)(0.07 * sr / HOP), gapH = (int)(0.05 * sr / HOP); int lastK = -999, lastS = -999, lastH = -999;
        for (int f = 0; f < frames - 1; f++)
        {
            // a moment where some band starts to rise sharply (the first frame of it)
            bool start = false; for (int b = 0; b < B && !start; b++) { double r0 = L[b][f] - before(b, f), rp = at(b, f - 1) - before(b, f - 1); if (r0 >= rise[b] && L[b][f] >= floor[b] - 6 && rp < rise[b]) start = true; }
            if (!start) continue;
            var pk = new double[B]; var R = new double[B]; for (int b = 0; b < B; b++) { pk[b] = peak(b, f); R[b] = pk[b] - before(b, f); }
            // compared with the loudest moment of each band: a kick fills the lows (its lows build for ~50 ms as its pitch drops), a snare fills the crack, a hat rises in the highs and not in the crack
            double low8 = -200; for (int k = f; k < Math.Min(frames, f + 14); k++) low8 = Math.Max(low8, L[0][k]);   // (a boomy kick with a slow pitch sweep fills its lows up to ~80 ms after its click)
            double crackRel = pk[2] - top[2]; bool kick = (low8 - before(0, f) >= rise[0] * 0.75 || (low8 - before(0, f) >= 4.5 && low8 - top[0] >= -6) || (R[1] >= rise[1] && crackRel < -(3 + sensS * 6)) || (low8 - before(0, f) >= 2.5 && R[1] >= rise[1] * 0.6 && crackRel < -(3 + sensS * 6))) && low8 - top[0] >= -(4 + sensK * 12) && f - lastK >= gapK;   // (kicks in one beat can differ a lot in size, and one can start on the ringing tail of the last)
            bool snare = R[2] >= rise[2] && pk[2] - top[2] >= -(3 + sensS * 6) && f - lastS >= gapK;
            // a hat rings on (still near its peak 2 to 3 frames later) where a kick's click is gone, and it rises in the highs more than in the crack (or the crack stays far from a snare's)
            Func<int, bool> hatIn = b => R[b] >= rise[b] && pk[b] - top[b] >= -(6 + sensH * 10) && Math.Max(at(b, Math.Min(frames - 1, f + 2)), at(b, Math.Min(frames - 1, f + 3))) >= pk[b] - 8 && (R[b] - R[2] > 4 || crackRel < -(7 + sensS * 6));
            bool hat = !snare && (hatIn(3) || hatIn(4)) && f - lastH >= gapH;
            long fr = (long)f * HOP - HOP / 2;
            if (kick) { lastK = f; hits.Add(new DrumHit { Frame = fr, Kind = KICK, Vel = Vel(pk[0] - top[0]) }); }
            if (snare) { lastS = f; hits.Add(new DrumHit { Frame = fr, Kind = SNARE, Vel = Vel(pk[2] - top[2]) }); }
            if (hat) { lastH = f; hits.Add(new DrumHit { Frame = fr, Kind = HAT, Vel = Vel(pk[3] - top[3]) }); }
        }
        return hits;
    }
    static int Vel(double dbBelowTop) { return (int)Math.Max(1, Math.Min(127, Math.Round(127 * (1 + dbBelowTop / 30.0)))); }
    static double Svf(double x, double g, double k, ref double s1, ref double s2, int t) { double hp = (x - (k + g) * s1 - s2) / (1 + g * (g + k)); double v1 = g * hp, bp = v1 + s1; s1 = bp + v1; double v2 = g * bp, lp = v2 + s2; s2 = lp + v2; return t == 1 ? hp : lp; }

    // the hits as notes on a track, from where the drum track starts in the song (replacing the notes there were)
    public int WriteNotes(Track t, List<DrumHit> hits, long start)
    {
        if (t == null) return 0; int q = (int)Get("quant"); bool same = Get("velmode") >= 0.5f; double spb = 60.0 / Math.Max(20, Fx.Bpm) * StudioEngine.SR, grid = q == 1 ? spb / 2 : q == 2 ? spb / 4 : q == 3 ? spb / 8 : 0;
        var evs = new List<MidiEv>(); var used = new HashSet<long>();
        foreach (var h in hits)
        {
            long f = Math.Max(0, start + h.Frame); if (grid > 0) f = (long)(Math.Round(f / grid) * grid); if (!used.Add(f * 4 + h.Kind)) continue;
            byte note = (byte)Notes[h.Kind], vel = (byte)(same ? 100 : h.Vel); long len = (long)(StudioEngine.SR * 0.08);
            evs.Add(new MidiEv { Frame = f, S = 0x90, D1 = note, D2 = vel }); evs.Add(new MidiEv { Frame = f + len, S = 0x80, D1 = note, D2 = 0 });
        }
        evs.Sort((a, b) => a.Frame != b.Frame ? a.Frame.CompareTo(b.Frame) : (a.S == 0x80 ? -1 : 1).CompareTo(b.S == 0x80 ? -1 : 1));
        lock (t.Midi) { t.Midi.Clear(); t.Midi.AddRange(evs); }
        return evs.Count / 2;
    }
    // the audio rows of the project a drum track can come from (rows with audio, not instruments or buses)
    public static List<Track> AudioRows() { var list = new List<Track>(); var eng = StudioEngine.Current; if (eng == null) return list; lock (eng.Sync) foreach (var t in eng.Tracks) if (t.Host == null && !t.IsBus && !t.Instrument) list.Add(t); return list; }
    public string Analyse()
    {
        var rows = AudioRows(); int s = (int)Get("src"); if (s < 1 || s > rows.Count) return "Pick the drum track first (the buttons at the top).";
        var row = rows[s - 1]; long start; var x = StudioEngine.Current.RowMono(row, out start); if (x.Length < StudioEngine.SR / 4) return "\"" + row.Name + "\" has no audio to listen to.";
        var hits = Detect(x, StudioEngine.SR, Get("sens_k") / 100, Get("sens_s") / 100, Get("sens_h") / 100); LastHits = hits; LastLength = x.Length; LastSource = row.Name;
        int k = 0, sn = 0, h = 0; foreach (var d in hits) { if (d.Kind == KICK) k++; else if (d.Kind == SNARE) sn++; else h++; }
        int c = WriteNotes(Owner, hits, start);
        return c == 0 ? "No hits found in \"" + row.Name + "\". Turn the sensitivity up and try again." : c + " notes written from \"" + row.Name + "\": " + k + " kicks, " + sn + " snares, " + h + " hats. They play from where that track starts. Missing some? Turn that sensitivity up and press again.";
    }
}

class Drum2MidiWindow : PxWindow
{
    readonly Drum2MidiFx e; string msg = "Pick the drum track, then press TURN THE DRUMS INTO NOTES."; int held = -1;
    public Drum2MidiWindow(Drum2MidiFx f) : base(f, "Drum2MIDI", new Size(1000, 660)) { e = f; Accent = Lime; }
    protected override string SubTitle { get { return "drums into notes"; } }
    protected override void PaintBody(Graphics g, RectangleF a)
    {
        var top = new RectangleF(a.X, a.Y, a.Width, 96); Panel(g, top, "DRUM TRACK", Lime); var rows = Drum2MidiFx.AudioRows(); int cur = (int)e.Get("src");
        float bx = top.X + 14, by = top.Y + 30;
        if (rows.Count == 0) Txt(g, "No audio tracks in the project yet: add a beat or a drum loop first.", 11, Dim, new RectangleF(bx, by, top.Width - 28, 24), StringAlignment.Near, false);
        for (int i = 0; i < rows.Count && i < 32; i++)
        {
            string nm = (i + 1) + "  " + rows[i].Name; if (nm.Length > 24) nm = nm.Substring(0, 23) + "..."; float w = Math.Min(200, 34 + nm.Length * 7.2f); if (bx + w > top.Right - 14) { bx = top.X + 14; by += 30; if (by > top.Bottom - 28) break; }
            int ii = i + 1; Button(g, new RectangleF(bx, by, w, 26), nm, cur == ii, Lime, () => { Mark(); e.Set("src", ii); }, "Listen to this track", 10); bx += w + 8;
        }
        var go = new RectangleF(a.X, top.Bottom + 10, a.Width, 44); Button(g, go, "TURN THE DRUMS INTO NOTES", true, Lime, () => { Cursor = Cursors.WaitCursor; try { msg = e.Analyse(); } finally { Cursor = Cursors.Default; } Invalidate(); }, "Listen to the drum track and write its kicks, snares and hats onto this track as notes", 13);
        // what it found: three lanes along the drum track
        var lanes = new RectangleF(a.X, go.Bottom + 10, a.Width, 130); Fill(g, Well, lanes, 10); string[] names = { "KICK", "SNARE", "HATS" }; Color[] cols = { Orange, Pink, Cyan };
        for (int k = 0; k < 3; k++)
        {
            float y = lanes.Y + 14 + k * 38; Txt(g, names[k], 10, cols[k], new RectangleF(lanes.X + 12, y, 60, 30), StringAlignment.Near, true);
            var ln = new RectangleF(lanes.X + 78, y + 6, lanes.Width - 92, 18); Fill(g, Color.FromArgb(30, 255, 255, 255), ln, 4);
            if (e.LastLength > 0) foreach (var h in e.LastHits) if (h.Kind == k) { float px = ln.X + ln.Width * (float)(h.Frame / (double)e.LastLength); float hh = ln.Height * (0.35f + 0.65f * h.Vel / 127f); Fill(g, cols[k], new RectangleF(px - 1.5f, ln.Bottom - hh, 3, hh), 1); }
        }
        Txt(g, msg, 10.5f, Ink, new RectangleF(a.X + 10, lanes.Bottom + 6, a.Width - 20, 18), StringAlignment.Near, false);
        var p1 = new RectangleF(a.X, lanes.Bottom + 30, 360, a.Bottom - lanes.Bottom - 30); Panel(g, p1, "LISTENING", Lime);
        Knob(g, p1.X + 60, p1.Y + 70, 20, "sens_k", "KICK", Orange); Knob(g, p1.X + 180, p1.Y + 70, 20, "sens_s", "SNARE", Pink); Knob(g, p1.X + 300, p1.Y + 70, 20, "sens_h", "HATS", Cyan);
        Txt(g, "QUANTIZE", 10, Dim, new RectangleF(p1.X + 14, p1.Y + 128, 160, 14), StringAlignment.Near, true); Choice(g, new RectangleF(p1.X + 14, p1.Y + 144, 160, 24), "quant", Lime, 10);
        Txt(g, "VELOCITY", 10, Dim, new RectangleF(p1.X + 186, p1.Y + 128, 160, 14), StringAlignment.Near, true); Choice(g, new RectangleF(p1.X + 186, p1.Y + 144, 160, 24), "velmode", Lime, 10);
        var p2 = new RectangleF(p1.Right + 10, p1.Y, a.Right - p1.Right - 10, p1.Height); Panel(g, p2, "KIT", Gold); float kw = (p2.Width - 20) / 6;
        string[][] ks = { new[] { "k_tune", "TUNE" }, new[] { "k_punch", "PUNCH" }, new[] { "k_decay", "DECAY" }, new[] { "s_tone", "TONE" }, new[] { "s_snap", "SNAP" }, new[] { "s_decay", "DECAY" } };
        for (int k = 0; k < 6; k++) Knob(g, p2.X + 10 + kw * (k + 0.5f), p2.Y + 58, 15, ks[k][0], ks[k][1], k < 3 ? Orange : Pink);
        string[][] k2 = { new[] { "h_tone", "HAT TONE" }, new[] { "h_decay", "HAT DECAY" }, new[] { "k_lvl", "KICK LVL" }, new[] { "s_lvl", "SNARE LVL" }, new[] { "h_lvl", "HAT LVL" }, new[] { "level", "LEVEL" } };
        for (int k = 0; k < 6; k++) Knob(g, p2.X + 10 + kw * (k + 0.5f), p2.Y + 128, 15, k2[k][0], k2[k][1], k == 5 ? Ink : k == 3 ? Pink : k == 2 ? Orange : Cyan);
        string[] pads = { "KICK", "SNARE", "CLAP", "HAT", "OPEN HAT" }; int[] pn = { 36, 38, 39, 42, 46 }; float pw = (p2.Width - 28 - 4 * 8) / 5, py = p2.Bottom - 40;
        for (int k = 0; k < 5; k++) { int kk = k; var r = new RectangleF(p2.X + 14 + k * (pw + 8), py, pw, 28); bool lit = held == k; Fill(g, lit ? Gold : Ctl, r, 8); Stroke(g, Color.FromArgb(150, Gold), r, 8, 1.2f); Txt(g, pads[k], 10, lit ? Color.Black : Ink, r, StringAlignment.Center, true);
            var h = Reg(r, () => { held = kk; e.NoteOn(pn[kk], 110); }, "Play the " + pads[k].ToLower()); h.DragAt = pt => { }; h.Up = () => { if (held >= 0) e.NoteOff(pn[held]); held = -1; }; }
    }
}
