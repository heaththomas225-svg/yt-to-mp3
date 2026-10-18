using System;
using System.Collections.Generic;

// =====================================================================================================
//  The acoustic and electric instruments of the sound library: piano, electric piano, strings, brass, guitars, mallets, bells, log drum
//  and flute. Each note is synthesised here (additive partials, FM, plucked-string and band-limited oscillators), so nothing is sampled
//  from anyone else's recording. Everything stays below 18 kHz, starts and ends without a click and never clips, so the sounds are clean.
//  The synth sounds (leads, pads, plucks, organ) come from Bass Lab presets and are played through the same RenderPart call.
// =====================================================================================================
class Instrument
{
    public string Name, Group, Preset; public int Low = 36, High = 96; public bool Chords = true, Melody = true;
    public bool Native { get { return Preset == null; } }
}

static class Instruments
{
    const int SR = SoundGen.SR;
    public static readonly List<Instrument> All = new List<Instrument>
    {
        new Instrument { Name = "Grand Piano", Group = "Keys", Low = 28, High = 100 },
        new Instrument { Name = "Electric Piano", Group = "Keys", Low = 33, High = 96 },
        new Instrument { Name = "Organ", Group = "Keys", Preset = "Organ" },
        new Instrument { Name = "Clav", Group = "Keys", Preset = "Clav Keys" },
        new Instrument { Name = "Bell Keys", Group = "Keys", Preset = "Bell Keys" },
        new Instrument { Name = "Strings", Group = "Strings & Brass", Low = 36, High = 96 },
        new Instrument { Name = "Brass", Group = "Strings & Brass", Low = 40, High = 84 },
        new Instrument { Name = "Acoustic Guitar", Group = "Guitar", Low = 40, High = 88 },
        new Instrument { Name = "Nylon Guitar", Group = "Guitar", Low = 40, High = 84 },
        new Instrument { Name = "Marimba", Group = "Mallets & Bells", Low = 45, High = 96, Chords = false },
        new Instrument { Name = "Kalimba", Group = "Mallets & Bells", Low = 55, High = 96, Chords = false },
        new Instrument { Name = "Bells", Group = "Mallets & Bells", Low = 60, High = 100, Chords = false },
        new Instrument { Name = "Log Drum", Group = "Percussion", Low = 33, High = 60, Chords = false },
        new Instrument { Name = "Flute", Group = "Woodwind", Low = 60, High = 96, Chords = false },
        new Instrument { Name = "Supersaw", Group = "Synth", Preset = "Supersaw Lead" },
        new Instrument { Name = "Square Lead", Group = "Synth", Preset = "Square Lead", Chords = false },
        new Instrument { Name = "Saw Lead", Group = "Synth", Preset = "Mono Saw Lead", Chords = false },
        new Instrument { Name = "FM Bell", Group = "Synth", Preset = "FM Bell Lead" },
        new Instrument { Name = "Pluck", Group = "Synth", Preset = "Short Pluck" },
        new Instrument { Name = "Soft Pluck", Group = "Synth", Preset = "Soft Pluck" },
        new Instrument { Name = "Soft Pad", Group = "Pads", Preset = "Soft Pad", Melody = false },
        new Instrument { Name = "Dark Pad", Group = "Pads", Preset = "Dark Pad", Melody = false },
        new Instrument { Name = "Shimmer Pad", Group = "Pads", Preset = "Shimmer Pad", Melody = false },
        new Instrument { Name = "Choir", Group = "Pads", Preset = "Choir Pad", Melody = false },
    };
    public static Instrument Get(string name) { return All.Find(i => i.Name == name); }

    // the instruments that suit a pack's genre (a trap pack gets piano, bells and strings; amapiano gets log drum, marimba and keys...)
    public static List<Instrument> Palette(Recipe r)
    {
        string g = (r.Genre + " " + r.Sub + " " + r.Section).ToLowerInvariant(); string[] names;
        if (g.Contains("amapiano")) names = new[] { "Log Drum", "Electric Piano", "Grand Piano", "Marimba", "Kalimba", "Soft Pad", "Flute", "Bells" };
        else if (g.Contains("afro") || g.Contains("dancehall") || g.Contains("reggaeton") || g.Contains("baile")) names = new[] { "Marimba", "Nylon Guitar", "Electric Piano", "Kalimba", "Grand Piano", "Flute", "Pluck", "Soft Pad" };
        else if (g.Contains("lo-fi") || g.Contains("r&b") || g.Contains("soul") || g.Contains("jazz")) names = new[] { "Electric Piano", "Grand Piano", "Nylon Guitar", "Strings", "Kalimba", "Flute", "Soft Pad", "Bells" };
        else if (g.Contains("trap") || g.Contains("drill") || g.Contains("hip-hop") || g.Contains("boom bap") || g.Contains("grime") || g.Contains("phonk") || g.Contains("vocals")) names = new[] { "Grand Piano", "Bells", "Strings", "Flute", "Choir", "Electric Piano", "Brass", "Dark Pad" };
        else if (g.Contains("house") || g.Contains("garage") || g.Contains("disco")) names = new[] { "Grand Piano", "Organ", "Electric Piano", "Strings", "Pluck", "Soft Pad", "Brass", "Bell Keys" };
        else if (g.Contains("techno") || g.Contains("trance")) names = new[] { "Supersaw", "Pluck", "Shimmer Pad", "Saw Lead", "Bells", "Strings", "Dark Pad", "FM Bell" };
        else if (g.Contains("pop") || g.Contains("jersey")) names = new[] { "Grand Piano", "Acoustic Guitar", "Electric Piano", "Strings", "Supersaw", "Pluck", "Brass", "Bells" };
        else if (g.Contains("drum & bass") || g.Contains("bassline") || g.Contains("bass music") || g.Contains("dubstep") || g.Contains("breaks") || g.Contains("rave")) names = new[] { "Soft Pad", "Electric Piano", "Grand Piano", "Strings", "Supersaw", "Organ", "Bells", "Pluck" };
        else names = new[] { "Supersaw", "Pluck", "Grand Piano", "Strings", "Bells", "Soft Pad", "Electric Piano", "Brass" };
        var l = new List<Instrument>(); foreach (var n in names) { var i = Get(n); if (i != null) l.Add(i); } return l;
    }

    // ---- playing a part: notes are {start in beats, length in beats, midi, velocity} ----
    public static Buf RenderPart(Instrument inst, List<double[]> notes, double bpm, double beats, double tailSec)
    {
        if (!inst.Native) return SoundGen.RenderSynth(Music.LeadPatch(inst.Preset), notes, bpm, beats, tailSec);
        double spb = 60.0 / bpm; int total = (int)((beats * spb + tailSec) * SR); var o = new Buf(total);
        // a guitar strums: notes that start together are spread 12 ms apart, low string first
        var starts = new Dictionary<double, int>(); var sorted = new List<double[]>(notes); sorted.Sort((a, b) => a[0] != b[0] ? a[0].CompareTo(b[0]) : a[2].CompareTo(b[2]));
        int seed = 1; foreach (var n in sorted)
        {
            int k; starts.TryGetValue(n[0], out k); starts[n[0]] = k + 1; double strum = inst.Group == "Guitar" ? k * 0.012 : 0;
            int at = (int)((n[0] * spb + strum) * SR); var b = Note(inst, (int)n[2], n[3] / 127.0, n[1] * spb, seed++); o.Add(b, at, 1f);
        }
        return o;
    }

    // one note of a native instrument, with its release (stereo)
    public static Buf Note(Instrument inst, int midi, double vel, double dur, int seed)
    {
        while (midi < inst.Low) midi += 12; while (midi > inst.High) midi -= 12; double f0 = SoundGen.NoteHz(midi); var r = new Rnd(seed * 7919 + midi);
        switch (inst.Name)
        {
            case "Grand Piano": return Piano(f0, vel, dur, r);
            case "Electric Piano": return EPiano(f0, vel, dur);
            case "Strings": return Ensemble(f0, vel, dur, 5, 9, 0.18, 0.45, true, r);
            case "Brass": return BrassNote(f0, vel, dur, r);
            case "Acoustic Guitar": return Guitar(f0, vel, dur, 0.8, r);
            case "Nylon Guitar": return Guitar(f0, vel, dur, 0.45, r);
            case "Marimba": return Modal(f0, vel, new[] { 1.0, 3.93, 9.25 }, new[] { 1.0, 0.32, 0.1 }, new[] { 0.55 * Math.Pow(330 / f0, 0.3), 0.12, 0.04 }, 0.035, r);
            case "Kalimba": return Modal(f0, vel, new[] { 1.0, 5.4, 11.7 }, new[] { 1.0, 0.22, 0.05 }, new[] { 1.3 * Math.Pow(440 / f0, 0.3), 0.18, 0.05 }, 0.025, r);
            case "Bells": return Modal(f0, vel, new[] { 1.0, 2.0, 2.76, 4.07, 5.4, 6.8 }, new[] { 1.0, 0.5, 0.45, 0.25, 0.18, 0.08 }, new[] { 3.2, 2.2, 1.6, 1.0, 0.6, 0.4 }, 0.01, r);
            case "Log Drum": return LogDrum(f0, vel, dur, r);
            case "Flute": return Flute(f0, vel, dur, r);
        }
        return Piano(f0, vel, dur, r);
    }

    static int Len(double dur, double rel) { return (int)((dur + rel) * SR) + 64; }
    // the release after the note ends: a smooth fall to silence (a damper), and a soft start so nothing clicks
    static void Shape(Buf b, double dur, double attack, double rel)
    {
        int on = (int)(dur * SR), n = b.Length; int a = Math.Max(1, (int)(attack * SR));
        for (int i = 0; i < n; i++) { double g = i < a ? 0.5 - 0.5 * Math.Cos(Math.PI * i / a) : 1; if (i > on) { double t = (i - on) / (double)SR; g *= Math.Exp(-t / (rel / 4.6)); } b.L[i] *= (float)g; b.R[i] *= (float)g; }
        SoundGen.EndFade(b, 0.1, (int)(0.02 * SR));
    }

    // piano: stiff-string partials (slightly sharp as they go up), two strings a hair apart per side, a hammer thump; brighter when played harder
    static Buf Piano(double f0, double vel, double dur, Rnd r)
    {
        double rel = 0.35, B = 0.00025 * Math.Pow(f0 / 261.6, 0.5); int n = Len(dur, rel); var o = new Buf(n);
        double T0 = Math.Max(1.2, Math.Min(9, 7 * Math.Pow(220 / f0, 0.55))), bright = 0.35 + 0.65 * vel;
        for (int k = 1; k <= 28; k++)
        {
            double fk = k * f0 * Math.Sqrt(1 + B * k * k); if (fk > 16000) break;
            double amp = Math.Pow(k, -1.05) * Math.Exp(-(k - 1) * (1.1 - bright) * 0.28) * (k == 1 ? 1 : 0.9); double tau = T0 / (1 + 0.18 * k + fk / 3000.0);
            for (int s = 0; s < 2; s++)   // two strings: one a little flat, one a little sharp; left and right hear them differently
            {
                double det = (s == 0 ? -1 : 1) * (0.35 + 0.15 * r.Next()) / 1200.0 * Math.Log(2); double w = 2 * Math.PI * fk * Math.Exp(det) / SR; double c = Math.Cos(w), sn = Math.Sin(w), x = 0, y = 1, d = Math.Exp(-1.0 / (tau * SR)), e = amp * 0.5;
                float pl = s == 0 ? 0.62f : 0.38f, pr = 1 - pl;
                for (int i = 0; i < n; i++) { double nx = x * c + y * sn, ny = y * c - x * sn; x = nx; y = ny; float v = (float)(x * e); o.L[i] += v * pl; o.R[i] += v * pr; e *= d; if (e < 1e-6) break; }
            }
        }
        // the hammer: a short low thump of filtered noise
        var lp = new Svf(); double cut = Math.Min(4000, 1200 + f0 * 2);
        for (int i = 0; i < Math.Min(n, SR / 20); i++) { double t = i / (double)SR; float h = lp.Process(r.Bi(), (float)cut, 0.7f, 0) * (float)(0.06 * vel * Math.Exp(-t / 0.006)); o.L[i] += h; o.R[i] += h; }
        o.Gain((float)(0.35 + 0.65 * vel)); Shape(o, dur, 0.002, rel); return o;
    }
    // electric piano: a bell-like tine (FM with a falling brightness) and a gentle stereo tremolo
    static Buf EPiano(double f0, double vel, double dur)
    {
        double rel = 0.25; int n = Len(dur, rel); var o = new Buf(n); double tau = 2.2 * Math.Pow(220 / f0, 0.35), ph = 0, w = 2 * Math.PI * f0 / SR, tine = 14 * f0 < 15000 ? 0.18 * vel : 0;
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SR, I = (0.8 + 1.8 * vel) * Math.Exp(-t / 0.3) + 0.2; ph += w; double y = Math.Sin(ph + I * Math.Sin(ph)) * Math.Exp(-t / tau) + tine * Math.Sin(14 * ph) * Math.Exp(-t / 0.04);
            double trem = 0.12 * Math.Sin(2 * Math.PI * 4.5 * t); o.L[i] = (float)(y * (0.5 + trem) * 0.9); o.R[i] = (float)(y * (0.5 - trem) * 0.9);
        }
        o.Gain((float)(0.4 + 0.6 * vel)); Shape(o, dur, 0.003, rel); return o;
    }
    // strings: an ensemble of band-limited saws, slow bow attack, vibrato that grows in, a warm low-pass
    static Buf Ensemble(double f0, double vel, double dur, int voices, double spreadCents, double attack, double rel, bool vibrato, Rnd r)
    {
        int n = Len(dur, rel); var o = new Buf(n); var lpL = new Svf(); var lpR = new Svf(); var ph = new double[voices * 2]; var det = new double[voices * 2];
        for (int v = 0; v < voices * 2; v++) { det[v] = Math.Pow(2, ((v % voices) - (voices - 1) / 2.0) / ((voices - 1) / 2.0) * spreadCents / 1200.0 + (r.Next() - 0.5) * 0.002); ph[v] = r.Next(); }
        double cut = Math.Min(9000, 1800 + f0 * 3.5);
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SR, vib = vibrato ? Math.Pow(2, 0.12 / 12 * Math.Sin(2 * Math.PI * 5.2 * t) * Math.Min(1, Math.Max(0, (t - 0.25) / 0.4))) : 1; double sl = 0, sr = 0;
            for (int v = 0; v < voices * 2; v++) { double dt = f0 * det[v] * vib / SR; ph[v] += dt; if (ph[v] >= 1) ph[v] -= 1; double s = SoundGen.Saw(ph[v], dt); if (v < voices) sl += s; else sr += s; }
            o.L[i] = lpL.Process((float)(sl / voices), (float)cut, 0.6f, 0) * 0.55f; o.R[i] = lpR.Process((float)(sr / voices), (float)cut, 0.6f, 0) * 0.55f;
        }
        o.Gain((float)(0.5 + 0.5 * vel)); Shape(o, dur, attack, rel); return o;
    }
    // brass: three saws with a filter that opens on the attack and a small pitch scoop
    static Buf BrassNote(double f0, double vel, double dur, Rnd r)
    {
        double rel = 0.15; int n = Len(dur, rel); var o = new Buf(n); var lpL = new Svf(); var lpR = new Svf(); var ph = new double[3]; double[] det = { 0.997, 1.0, 1.003 };
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SR, scoop = Math.Pow(2, -0.3 / 12 * Math.Exp(-t / 0.03)), env = Math.Min(1, t / 0.07); double cut = Math.Min(8000, f0 * (1.5 + (3 + 4 * vel) * env)); double s = 0;
            for (int v = 0; v < 3; v++) { double dt = f0 * det[v] * scoop / SR; ph[v] += dt; if (ph[v] >= 1) ph[v] -= 1; s += SoundGen.Saw(ph[v], dt); }
            float x = (float)(s / 3); o.L[i] = lpL.Process(x, (float)cut, 0.8f, 0) * 0.6f; o.R[i] = lpR.Process(x, (float)(cut * 0.97), 0.8f, 0) * 0.6f;
        }
        o.Gain((float)(0.5 + 0.5 * vel)); Shape(o, dur, 0.025, rel); return o;
    }
    // guitar: a plucked string and the body of the guitar under it
    static Buf Guitar(double f0, double vel, double dur, double bright, Rnd r)
    {
        double len = Math.Min(4.0, dur + 0.6); var s = SoundGen.Pluck(r, f0, len, bright * (0.7 + 0.3 * vel)); var body1 = new Svf(); var body2 = new Svf(); var o = new Buf(s.Length);
        for (int i = 0; i < s.Length; i++) { float x = s.L[i]; float b = body1.Process(x, 110f, 3f, 1) * 0.5f + body2.Process(x, 230f, 2.5f, 1) * 0.35f; float y = x * 0.8f + b; o.L[i] = y * 0.62f; o.R[i] = y * 0.55f; }
        o.Gain((float)(0.45 + 0.55 * vel)); Shape(o, Math.Min(dur + 0.3, len - 0.1), 0.001, 0.12); return o;
    }
    // mallets and bells: a few tuned modes, each fading at its own speed, and a soft mallet tap
    static Buf Modal(double f0, double vel, double[] ratio, double[] amp, double[] tau, double tap, Rnd r)
    {
        double longest = 0; foreach (var t in tau) longest = Math.Max(longest, t); int n = (int)(Math.Min(6, longest * 5) * SR) + 64; var o = new Buf(n);
        for (int k = 0; k < ratio.Length; k++)
        {
            double fk = f0 * ratio[k]; if (fk > 16000) continue; double w = 2 * Math.PI * fk / SR, c = Math.Cos(w), sn = Math.Sin(w), x = 0, y = 1, d = Math.Exp(-1.0 / (tau[k] * SR)), e = amp[k] * (k == 0 ? 1 : 0.6 + 0.4 * vel);
            for (int i = 0; i < n; i++) { double nx = x * c + y * sn, ny = y * c - x * sn; x = nx; y = ny; float v = (float)(x * e * 0.5); o.L[i] += v; o.R[i] += v; e *= d; if (e < 1e-6) break; }
        }
        var lp = new Svf(); for (int i = 0; i < Math.Min(n, SR / 40); i++) { double t = i / (double)SR; float h = lp.Process(r.Bi(), 3000f, 0.7f, 0) * (float)(tap * vel * Math.Exp(-t / 0.002)); o.L[i] += h; o.R[i] += h; }
        o.Gain((float)(0.4 + 0.6 * vel)); Shape(o, n / (double)SR, 0.001, 0.05); return o;
    }
    // log drum (amapiano): a deep tuned knock that drops into its note, with a wooden overtone
    static Buf LogDrum(double f0, double vel, double dur, Rnd r)
    {
        double len = Math.Max(0.35, Math.Min(1.2, dur + 0.25)); int n = (int)(len * SR); var o = new Buf(n); double ph = 0, ph2 = 0;
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SR, f = f0 * Math.Pow(2, 5.0 / 12 * Math.Exp(-t / 0.018)); ph += 2 * Math.PI * f / SR; ph2 += 2 * Math.PI * f * 2.02 / SR;
            double y = Math.Sin(ph) * Math.Exp(-t / 0.32) + 0.3 * Math.Sin(ph2) * Math.Exp(-t / 0.06); o.L[i] = (float)(y * 0.8); o.R[i] = (float)(y * 0.8);
        }
        o.Gain((float)(0.5 + 0.5 * vel)); Shape(o, len, 0.0015, 0.05); return o;
    }
    // flute: a soft tone with a little breath and a vibrato that comes in after the start
    static Buf Flute(double f0, double vel, double dur, Rnd r)
    {
        double rel = 0.12; int n = Len(dur, rel); var o = new Buf(n); var bp = new Svf(); double ph = 0;
        for (int i = 0; i < n; i++)
        {
            double t = i / (double)SR, vib = Math.Pow(2, 0.15 / 12 * Math.Sin(2 * Math.PI * 5 * t) * Math.Min(1, Math.Max(0, (t - 0.3) / 0.3))); ph += 2 * Math.PI * f0 * vib / SR;
            double y = Math.Sin(ph) + 0.18 * Math.Sin(2 * ph) + 0.06 * Math.Sin(3 * ph); float br = bp.Process(r.Bi(), (float)Math.Min(6000, f0 * 2), 4f, 1) * 0.035f * (float)(0.4 + 0.6 * Math.Exp(-t / 0.15)); float v = (float)(y * 0.45) + br; o.L[i] = v; o.R[i] = v;
        }
        o.Gain((float)(0.5 + 0.5 * vel)); Shape(o, dur, 0.07, rel); return o;
    }

    // ---- a General MIDI drum part played with a pack's kit (for MIDI previews and drum MIDI files) ----
    public static Buf RenderDrums(DrumKit.Kit kit, List<double[]> notes, double bpm, double beats)
    {
        double spb = 60.0 / bpm; var o = new Buf((int)((beats * spb + 1.0) * SR));
        foreach (var n in notes)
        {
            int at = (int)(n[0] * spb * SR); float g = (float)(n[3] / 127.0); Buf b = null;
            switch ((int)n[2]) { case 35: case 36: b = kit.Kicks[0]; break; case 38: case 40: b = kit.Snares[0]; g *= 0.95f; break; case 37: b = kit.Perc[3]; break; case 39: b = kit.Claps[0]; g *= 0.8f; break; case 42: case 44: b = kit.HatsC[0]; g *= 0.55f; break; case 46: b = kit.HatsO[0]; g *= 0.5f; break; case 49: case 57: b = kit.Rides[1]; g *= 0.5f; break; case 51: case 59: b = kit.Rides[0]; g *= 0.45f; break; default: b = kit.Perc[((int)n[2]) % kit.Perc.Count]; g *= 0.5f; break; }
            if (b != null) o.Add(b, at, g);
        }
        return o;
    }
}
