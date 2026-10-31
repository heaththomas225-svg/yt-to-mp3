using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

// =====================================================================================================
//  THE PURPHEX SAMPLER: real samples played by the notes of a track (the piano roll, a MIDI keyboard, recorded MIDI). One track can hold
//  one sample (a kick, a snare, an 808...) or a whole kit: each sample is a zone with the keys that play it. A zone either keeps its
//  pitch (drums: any note plays the sample as it is) or follows the note (808s, basses, melodic samples: the root note plays it as it
//  is, a note an octave up plays it an octave up). One-shot zones always play to the end (a drum hit); others stop when the note ends
//  (with a short release, so nothing clicks). It runs inside the studio's own engine like every instrument: mute, solo, the track's
//  plugins, buses and the mixer all apply. The samples are referenced by file (the sound library's files, or your own).
// =====================================================================================================
class SamplerFx : InstrumentFx   // (Purphex Pads is a sampler with a window of 16 pads)
{
    public class Zone
    {
        public string Path = "", Name = ""; public int Lo, Hi = 127, Root = 60; public bool Follow, OneShot = true; public float GainDb;
        internal float[] L, R; internal int Rate = 44100; public bool Missing; internal int Lead;   // Lead: the silence at the front of the file, skipped when a one-shot is hit
        public bool Plays(int note) { return note >= Lo && note <= Hi; }
        public Zone Copy() { return new Zone { Path = Path, Name = Name, Lo = Lo, Hi = Hi, Root = Root, Follow = Follow, OneShot = OneShot, GainDb = GainDb, L = L, R = R, Rate = Rate, Missing = Missing, Lead = Lead }; }
    }
    class Voice { public bool On, Releasing, Choked; public Zone Z; public int Note; public double Pos, Step, Gain, Env, Rel, LastL, LastR; }
    double tailL, tailR, lfoPh, flL, flR;   // flL / flR: the LFO filter   // what a voice taken for a new note was playing, fading away in 3 ms (cut in one sample it clicked)
    public readonly List<Zone> Zones = new List<Zone>(); readonly Voice[] voices = new Voice[48]; List<KeyValuePair<int, MidiEv>> pending; protected readonly object zl = new object();   // (the audio thread reads the zones under it)
    public SamplerFx()
    {
        Type = "sampler"; Title = "Purphex Sampler"; Category = "Instruments"; Blurb = "Plays your samples from notes: one sample (kick, snare, 808...) or a kit mapped to keys, fixed pitch or following the notes";
        AddP("gain", "Level", -24, 12, 0, "dB"); AddP("rel", "Release", 5, 2000, 60, "ms"); AddP("vel", "Velocity", 0, 100, 70, "%");
        // the LFO: it moves the level (tremolo), the pitch (vibrato) or a filter (wobble) at RATE, as much as DEPTH (0 = off, as before)
        AddP("lfor", "LFO rate", 0.1f, 16, 3, "Hz"); AddP("lfod", "LFO depth", 0, 100, 0, "%"); AddC("lfot", "LFO moves", new[] { "Volume", "Pitch", "Filter" }, 2);
        for (int i = 0; i < voices.Length; i++) voices[i] = new Voice();
    }
    public override bool IsInstrument { get { return true; } }
    public override bool HasWindow { get { return false; } }
    public override string WindowLabel { get { return "Open the piano roll"; } }
#if !PURPHEX_CORE
    public override void ShowWindow(IWin32Window owner) { var sf = owner as StudioForm; if (sf != null && Owner != null) sf.OpenPianoRoll(Owner); }
#else
    public override void ShowWindow(IWin32Window owner) { }
#endif
    public override void CloseWindow() { }
    public override void Reset() { base.Reset(); foreach (var v in voices) v.On = false; }
    protected override void OnEvents(List<KeyValuePair<int, MidiEv>> ev) { pending = ev; }

    // ---- zones ----
    // the zone a note plays (the last matching one: a later zone covers an earlier one on the same key)
    public Zone ZoneFor(int note) { lock (zl) { for (int i = Zones.Count - 1; i >= 0; i--) if (Zones[i].Plays(note)) return Zones[i]; } return null; }
    public static string LoadInto(Zone z)
    {
        float[] l, r; int sr; string fmt; z.Missing = false;
        if (string.IsNullOrEmpty(z.Path) || !File.Exists(z.Path)) { z.Missing = true; z.L = z.R = null; return "The sample is missing: " + z.Path; }
        if (!SoundIO.Read(z.Path, out l, out r, out sr, out fmt) || l.Length == 0) { z.Missing = true; z.L = z.R = null; return "The sample could not be read: " + z.Path; }
        z.L = l; z.R = r; z.Rate = sr > 0 ? sr : 44100; z.Lead = LeadOf(l, r, z.Rate); return null;
    }
    // the silence before the sound starts (under -48 dB), keeping 1 ms before the hit so its attack is whole; at most 2 s
    public static int LeadOf(float[] l, float[] r, int sr)
    {
        int n = Math.Min(l.Length, sr * 2), i = 0; for (; i < n; i++) if (Math.Abs(l[i]) > 0.004f || (r != null && i < r.Length && Math.Abs(r[i]) > 0.004f)) break;
        return i >= n ? 0 : Math.Max(0, i - sr / 1000);
    }
    public List<Zone> ZonesCopy() { lock (zl) return new List<Zone>(Zones); }
    // one sample on every key (a drum hit keeps its pitch; an 808 or melodic sample follows the notes from its root note)
    public string SetSingle(string path, string name, bool follow, int root)
    {
        var z = new Zone { Path = path, Name = name ?? System.IO.Path.GetFileNameWithoutExtension(path), Lo = 0, Hi = 127, Root = root, Follow = follow, OneShot = !follow };
        string err = LoadInto(z); lock (zl) { Zones.Clear(); Zones.Add(z); } return err;
    }
    // one more sample in the kit, on its own key
    public string AddToKit(string path, string name, int key)
    {
        var z = new Zone { Path = path, Name = name ?? System.IO.Path.GetFileNameWithoutExtension(path), Lo = key, Hi = key, Root = key, Follow = false, OneShot = true };
        string err = LoadInto(z); lock (zl) { Zones.RemoveAll(q => q.Lo == key && q.Hi == key); Zones.Add(z); } return err;
    }
    // the sound on one key, taking a key's sound off, and two keys swapping their sounds (Purphex Pads moves a pad to another key)
    public Zone ZoneAt(int key) { lock (zl) { foreach (var z in Zones) if (z.Lo == key && z.Hi == key) return z; } return null; }
    public void RemoveKey(int key) { lock (zl) Zones.RemoveAll(q => q.Lo == key && q.Hi == key); }
    public void SwapKeys(int a, int b) { lock (zl) foreach (var z in Zones) if (z.Lo == z.Hi) { if (z.Lo == a) { z.Lo = z.Hi = z.Root = b; } else if (z.Lo == b) { z.Lo = z.Hi = z.Root = a; } } }
    // every sound copied into a folder (a saved kit keeps its own copies); the zones then play the copies
    public void PackSounds(string folder)
    {
        List<Zone> zs; lock (zl) zs = new List<Zone>(Zones); if (zs.Count == 0) return;
        try
        {
            Directory.CreateDirectory(folder); string full = Path.GetFullPath(folder).TrimEnd('\\') + "\\"; var done = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); int n = 0;
            foreach (var z in zs)
            {
                if (string.IsNullOrEmpty(z.Path) || !File.Exists(z.Path)) continue; if (Path.GetFullPath(z.Path).StartsWith(full, StringComparison.OrdinalIgnoreCase)) continue;
                string to; if (!done.TryGetValue(z.Path, out to)) { to = Path.Combine(folder, (++n).ToString("00") + " " + Path.GetFileName(z.Path)); File.Copy(z.Path, to, true); done[z.Path] = to; }
                z.Path = to;
            }
        }
        catch { }
    }
    public bool IsKit { get { lock (zl) return Zones.Count > 1 || (Zones.Count == 1 && Zones[0].Lo == Zones[0].Hi); } }
    public bool AnyFollow { get { lock (zl) { foreach (var z in Zones) if (z.Follow) return true; } return false; } }
    // the name shown on a key of the piano roll (a kit's sample on that key)
    public string KeyName(int note) { lock (zl) { foreach (var z in Zones) if (z.Lo == z.Hi && z.Lo == note) return z.Name; } return null; }

    // ---- saving ----
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    public override string Serialize()
    {
        var sb = new StringBuilder(); lock (zl) foreach (var z in Zones) sb.Append(z.Lo).Append('|').Append(z.Hi).Append('|').Append(z.Root).Append('|').Append(z.Follow ? 1 : 0).Append('|').Append(z.OneShot ? 1 : 0).Append('|').Append(z.GainDb.ToString("0.##", CI)).Append('|').Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(z.Name))).Append('|').Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(z.Path))).Append('\n');
        return base.Serialize() + ";zones=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(sb.ToString()));
    }
    public override void Deserialize(string s)
    {
        base.Deserialize(s); if (s == null) return; int k = s.IndexOf(";zones="); if (k < 0) return; string b64 = s.Substring(k + 7); int e = b64.IndexOf(';'); if (e >= 0) b64 = b64.Substring(0, e);
        string text; try { text = Encoding.UTF8.GetString(Convert.FromBase64String(b64)); } catch { return; }
        var list = new List<Zone>();
        foreach (var line in text.Split('\n'))
        {
            var f = line.Split('|'); if (f.Length < 8) continue; var z = new Zone(); int iv; float fv;
            if (int.TryParse(f[0], out iv)) z.Lo = iv; if (int.TryParse(f[1], out iv)) z.Hi = iv; if (int.TryParse(f[2], out iv)) z.Root = iv; z.Follow = f[3] == "1"; z.OneShot = f[4] == "1"; if (float.TryParse(f[5], NumberStyles.Float, CI, out fv)) z.GainDb = fv;
            try { z.Name = Encoding.UTF8.GetString(Convert.FromBase64String(f[6])); z.Path = Encoding.UTF8.GetString(Convert.FromBase64String(f[7])); } catch { }
            LoadInto(z); list.Add(z);
        }
        lock (zl) { Zones.Clear(); Zones.AddRange(list); }
    }

    // ---- playing ----
    void Start(int note, int vel)
    {
        var z = ZoneFor(note); if (z == null || z.L == null) return;
        // a pad or key hit again plays over its last hits (they overlap, like hitting a drum again); past 6 copies of the same pad the oldest fades out in 10 ms, so they never pile up into a mess
        { int same = 0; Voice oldest = null; foreach (var x in voices) if (x.On && x.Note == note && x.Z == z && !x.Releasing) { same++; if (oldest == null || x.Pos > oldest.Pos) oldest = x; } if (same >= 6 && oldest != null) { oldest.Releasing = true; oldest.Choked = true; } }
        Voice v = null; foreach (var x in voices) if (!x.On) { v = x; break; }
        if (v == null) { foreach (var x in voices) if (x.Choked || x.Releasing) { if (v == null || x.Env < v.Env) v = x; } if (v == null) { v = voices[0]; foreach (var x in voices) if (x.Pos > v.Pos) v = x; } tailL += v.LastL; tailR += v.LastR; }
        double rate = z.Rate / (double)SR, pitch = z.Follow ? Math.Pow(2, (note - z.Root) / 12.0) : 1.0; double vs = Get("vel") / 100.0;
        v.On = true; v.Releasing = false; v.Choked = false; v.LastL = v.LastR = 0; v.Z = z; v.Note = note; v.Pos = z.OneShot ? z.Lead : 0; v.Step = rate * pitch; v.Gain = ((1 - vs) + vs * vel / 127.0) * Math.Pow(10, z.GainDb / 20.0); v.Env = 1; v.Rel = 0;
    }
    static double Soft(double x) { double a = Math.Abs(x); return Math.Sign(x) * (0.95 + 0.05 * Math.Tanh((a - 0.95) / 0.05)); }
    void Stop(int note) { foreach (var v in voices) if (v.On && v.Note == note && !v.Releasing && !v.Z.OneShot) v.Releasing = true; }
    public override void Process(float[] l, float[] r, int n)
    {
        var ev = pending; pending = null; int ei = 0; double relK = Math.Exp(-1.0 / (Math.Max(5, Get("rel")) / 1000.0 * SR)), master = Math.Pow(10, Get("gain") / 20.0); double fadeIn = SR * 0.0015, chokeK = Math.Exp(-1.0 / (0.010 * SR));
        double lfoD = Get("lfod") / 100.0, lfoInc = Get("lfor") / SR; int lfoT = (int)Math.Round(Get("lfot")); bool lfoOn = lfoD > 0.001;
        for (int i = 0; i < n; i++)
        {
            double lfoS = 0, pm = 1; if (lfoOn) { lfoPh += lfoInc; if (lfoPh >= 1) lfoPh -= 1; lfoS = Math.Sin(2 * Math.PI * lfoPh); if (lfoT == 1) pm = Math.Pow(2, lfoD * lfoS / 12.0); }   // (vibrato: up to a semitone each way)
            while (ev != null && ei < ev.Count && ev[ei].Key <= i)
            {
                var m = ev[ei].Value; int kind = m.S & 0xF0; ei++;
                if (kind == 0x90 && m.D2 > 0) Start(m.D1, m.D2);
                else if (kind == 0x80 || kind == 0x90) Stop(m.D1);
                else if (kind == 0xB0 && (m.D1 == 123 || m.D1 == 120)) foreach (var v in voices) if (v.On) { v.Releasing = true; if (m.D1 == 120) v.Choked = true; }
            }
            double sl = tailL, sr = tailR; if (tailL != 0 || tailR != 0) { tailL *= chokeK; tailR *= chokeK; if (Math.Abs(tailL) + Math.Abs(tailR) < 1e-7) tailL = tailR = 0; }
            for (int k = 0; k < voices.Length; k++)
            {
                var v = voices[k]; if (!v.On) continue; var z = v.Z; var L = z.L; var R = z.R; if (L == null) { v.On = false; continue; }
                int p = (int)v.Pos; if (p >= L.Length - 1) { v.On = false; continue; } double fr = v.Pos - p;
                double a = L[p] + (L[p + 1] - L[p]) * fr, b = R[p] + (R[p + 1] - R[p]) * fr;
                if (v.Releasing) { v.Env *= v.Choked ? chokeK : relK; if (v.Env < 1e-4) { v.On = false; continue; } }
                double att = !z.Follow || v.Pos * (1 / v.Step) > fadeIn ? 1 : v.Pos / v.Step / fadeIn;   // (a pitched sample starts with a tiny fade: no click when it starts mid-wave)
                double g = v.Gain * v.Env * att; sl += a * g; sr += b * g; v.LastL = a * g; v.LastR = b * g; v.Pos += v.Step * pm;
                int left = L.Length - 1 - (int)v.Pos; if (left < 64 && left >= 0) { double t = left / 64.0; sl -= a * g * (1 - t); sr -= b * g * (1 - t); }   // (the last 64 samples fade out: no click at the end)
            }
            if (lfoOn)
            {
                if (lfoT == 0) { double vg = 1 - lfoD * (0.5 - 0.5 * lfoS); sl *= vg; sr *= vg; }   // volume
                else if (lfoT == 2) { double fc = 120 * Math.Pow(150, 1 - lfoD * (0.5 - 0.5 * lfoS)), fa = 1 - Math.Exp(-2 * Math.PI * Math.Min(fc, SR * 0.45) / SR); flL += (sl - flL) * fa; flR += (sr - flR) * fa; sl = flL; sr = flR; }   // filter (18 kHz open down to about 120 Hz)
            }
            double ol = sl * master, or = sr * master;
            if (ol > 0.95 || ol < -0.95) ol = Soft(ol); if (or > 0.95 || or < -0.95) or = Soft(or);   // a smooth ceiling: several loud hits together round off instead of crackling
            l[i] += (float)ol; r[i] += (float)or;
        }
    }
}
