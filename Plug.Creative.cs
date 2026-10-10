using System;
using System.Collections.Generic;

// ======================================================================
//  Creative engines: granular, spectral freeze / blur / delay, FM, tape stop, scratch, beat repeat,
//  modulator (LFO / steps / envelope / random / pump), sound generators
// ======================================================================

class GranularFx : Fx
{
    struct Grain { public double pos, rate; public int age, len; public float gl, gr; public bool rev; public bool on; }
    float[] bl, br; int len, w; long wc; Grain[] gr = new Grain[96]; double acc; uint rng = 12345; static float[] win; double fbl, fbr;
    public GranularFx()
    {
        Type = "grain"; Title = "Granular Synth Effect"; Category = "Creative";
        AddP("size", "Grain size", 10, 800, 120, "ms"); AddP("dens", "Grains per second", 1, 120, 24, ""); AddP("pos", "Look back", 0, 4000, 350, "ms"); AddP("posj", "Look back scatter", 0, 2000, 150, "ms");
        AddP("pitch", "Pitch", -24, 24, 0, "st"); AddP("pitchj", "Pitch scatter", 0, 12, 0, "st"); AddP("rev", "Backwards grains", 0, 100, 0, "%"); AddP("spread", "Stereo spread", 0, 100, 60, "%");
        AddP("fb", "Feedback", 0, 85, 0, "%"); AddP("lp", "High cut", 500, 20000, 16000, "Hz"); AddP("dry", "Dry", 0, 100, 0, "%"); AddP("mix", "Grains", 0, 100, 100, "%");
    }
    Biquad lpf = new Biquad(); protected override void Update() { len = SR * 6; if (bl == null || bl.Length != len) { bl = new float[len]; br = new float[len]; w = 0; wc = 0; } lpf.Set(Biquad.LowPass, SR, Math.Min(Get("lp"), SR * 0.45), 0.7071, 0); if (win == null) { win = new float[1025]; for (int i = 0; i <= 1024; i++) win[i] = (float)(0.5 - 0.5 * Math.Cos(2 * Math.PI * i / 1024)); } }
    public override void Reset() { base.Reset(); if (bl != null) { Array.Clear(bl, 0, len); Array.Clear(br, 0, len); } for (int i = 0; i < gr.Length; i++) gr[i].on = false; acc = 0; fbl = fbr = 0; lpf.Reset(); }
    double Rnd() { rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5; return (rng & 0xFFFFFF) / 16777216.0; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); int size = (int)Math.Max(64, Get("size") * 0.001 * SR); double dens = Get("dens"), posS = Get("pos") * 0.001 * SR, posJ = Get("posj") * 0.001 * SR, pitch = Get("pitch"), pj = Get("pitchj"), revP = Get("rev") / 100.0, spread = Get("spread") / 100.0, fb = Get("fb") / 100.0, dry = Get("dry") / 100.0, mix = Get("mix") / 100.0;
        double norm = 1.0 / Math.Sqrt(Math.Max(1.0, dens * size / SR * 0.75));
        for (int i = 0; i < n; i++)
        {
            bl[w] = l[i] + (float)fbl * (float)fb; br[w] = r[i] + (float)fbr * (float)fb; wc++;
            acc += dens / SR; while (acc >= 1) { acc -= 1; for (int g = 0; g < gr.Length; g++) if (!gr[g].on) { var G = new Grain(); double st = pitch + (Rnd() * 2 - 1) * pj; G.rate = Math.Pow(2, st / 12.0); G.len = size; G.rev = Rnd() < revP; double back = Math.Max(posS + Rnd() * posJ, size * G.rate + 8); G.pos = G.rev ? w - back + size * G.rate : w - back; double pan = (Rnd() * 2 - 1) * spread; G.gl = (float)Math.Min(1, 1 - pan); G.gr = (float)Math.Min(1, 1 + pan); G.on = true; gr[g] = G; break; } }
            float oL = 0, oR = 0;
            for (int g = 0; g < gr.Length; g++)
            {
                if (!gr[g].on) continue; double ph = gr[g].age / (double)gr[g].len; if (ph >= 1) { gr[g].on = false; continue; }
                double fw = ph * 1024; int fi = (int)fw; float wv = win[fi] + (win[fi + 1] - win[fi]) * (float)(fw - fi);
                double p = gr[g].pos; int ip = (int)Math.Floor(p); float fr2 = (float)(p - ip); int a = ((ip % len) + len) % len, b = a + 1 >= len ? 0 : a + 1;
                oL += (bl[a] + (bl[b] - bl[a]) * fr2) * wv * gr[g].gl; oR += (br[a] + (br[b] - br[a]) * fr2) * wv * gr[g].gr;
                gr[g].pos += gr[g].rev ? -gr[g].rate : gr[g].rate; gr[g].age++;
            }
            oL = lpf.TickL(oL * (float)norm); oR = lpf.TickR(oR * (float)norm); fbl = oL; fbr = oR;
            l[i] = Dsp2.Sanitize((float)(l[i] * dry + oL * mix)); r[i] = Dsp2.Sanitize((float)(r[i] * dry + oR * mix));
            if (++w >= len) w = 0;
        }
    }
}

// spectral effects: freeze a moment of the spectrum, blur it in time, or delay different frequencies by different amounts
class SpecCreateFx : StftFx
{
    public static readonly string[] Modes = { "Freeze", "Blur", "Spectral delay" };
    double[][] fm = new double[2][], fph = new double[2][], dph = new double[2][], last = new double[2][], sm = new double[2][]; double[][][] ringRe = new double[2][][], ringIm = new double[2][][]; int[] rpos = new int[2]; bool wasFrozen; int mode; double blur, depth, fbk, curve; bool frozen; const int RF = 64;
    Random rnd = new Random(4);
    public SpecCreateFx()
    {
        Type = "specfx"; Title = "Spectral Effect"; Category = "Creative";
        AddC("mode", "Effect", Modes, 0); AddC("freeze", "Freeze (when in Freeze mode)", new[] { "Off", "On" }, 0); AddP("blur", "Blur amount", 0, 100, 70, "%"); AddP("depth", "Delay spread", 0, 100, 50, "%"); AddP("curve", "Delay curve", 0.3f, 3, 1, ""); AddP("fb", "Feedback", 0, 90, 30, "%"); AddMix(100);
    }
    protected override void Setup()
    {
        int H = N / 2; for (int c = 0; c < 2; c++) { fm[c] = new double[H + 1]; fph[c] = new double[H + 1]; dph[c] = new double[H + 1]; last[c] = new double[H + 1]; sm[c] = new double[H + 1]; ringRe[c] = new double[RF][]; ringIm[c] = new double[RF][]; for (int f = 0; f < RF; f++) { ringRe[c][f] = new double[N]; ringIm[c][f] = new double[N]; } rpos[c] = 0; }
    }
    protected override void SetupParams() { mode = (int)Get("mode"); blur = Get("blur") / 100.0; depth = Get("depth") / 100.0; fbk = Get("fb") / 100.0; curve = Get("curve"); frozen = Get("freeze") > 0.5f; }
    protected override void Spec(int ch, double[] re, double[] im)
    {
        int H = N / 2; double expct = 2 * Math.PI * st[0].Hop / N;
        if (mode == 0)
        {
            if (!frozen) { for (int k = 0; k <= H; k++) { double m = Math.Sqrt(re[k] * re[k] + im[k] * im[k]), p = Math.Atan2(im[k], re[k]); double d = p - last[ch][k]; last[ch][k] = p; fm[ch][k] = m; dph[ch][k] = d; fph[ch][k] = p; } }
            else { for (int k = 0; k <= H; k++) { fph[ch][k] += dph[ch][k]; re[k] = fm[ch][k] * Math.Cos(fph[ch][k]); im[k] = fm[ch][k] * Math.Sin(fph[ch][k]); } Sym(re, im, N); }
        }
        else if (mode == 1)
        {
            double a = 1 - blur * 0.93; var s = sm[ch]; for (int k = 0; k <= H; k++) { double m = Math.Sqrt(re[k] * re[k] + im[k] * im[k]); s[k] += (m - s[k]) * a; double nm = (k > 0 && k < H) ? (s[k - 1] + 2 * s[k] + s[k + 1]) * 0.25 : s[k]; double g = m > 1e-9 ? nm / m : 0; re[k] *= g; im[k] *= g; }
            Sym(re, im, N);
        }
        else
        {
            var rr = ringRe[ch]; var ri = ringIm[ch]; int p0 = rpos[ch]; int maxD = (int)(depth * (RF - 2));
            // feedback: add the frame that is read back to the new one
            for (int k = 1; k < H; k++)
            {
                int d = (int)(Math.Pow(k / (double)H, curve) * maxD); int rp = (p0 - d + RF * 2) % RF; double or = rr[rp][k], oi = ri[rp][k];
                rr[p0][k] = re[k] + or * fbk; ri[p0][k] = im[k] + oi * fbk; re[k] = or; im[k] = oi; re[N - k] = re[k]; im[N - k] = -im[k];
            }
            rr[p0][0] = re[0]; ri[p0][0] = 0; re[0] = 0; im[0] = 0; im[H] = 0; rpos[ch] = (p0 + 1) % RF;
        }
    }
}

// FM: the input drives a sine wave's phase (mode 1) or an oscillator wobbles the input's timing at audio rate (mode 2)
class FmFx : Fx
{
    float[] bl, br; int len, w; double ph;
    public FmFx()
    {
        Type = "fm"; Title = "FM Effect"; Category = "Creative";
        AddC("mode", "Type", new[] { "Sound drives a sine (FM synthesis)", "Oscillator warps the sound (frequency modulation)" }, 0); AddP("freq", "Oscillator frequency", 1, 4000, 220, "Hz"); AddP("depth", "Depth", 0, 100, 50, "%"); AddP("fine", "Oscillator follows the sound", 0, 100, 0, "%"); AddP("mix", "Mix", 0, 100, 100, "%");
    }
    protected override void Update() { len = SR / 10 + 8; if (bl == null || bl.Length != len) { bl = new float[len]; br = new float[len]; w = 0; } }
    public override void Reset() { base.Reset(); if (bl != null) { Array.Clear(bl, 0, len); Array.Clear(br, 0, len); } ph = 0; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); int mode = (int)Get("mode"); double f = Get("freq"), depth = Get("depth") / 100.0, mix = Get("mix") / 100.0;
        for (int i = 0; i < n; i++)
        {
            ph += f / SR; ph -= Math.Floor(ph); float a, b;
            if (mode == 0) { a = (float)(Math.Sin(2 * Math.PI * ph + depth * 8 * l[i]) * 0.7); b = (float)(Math.Sin(2 * Math.PI * ph + depth * 8 * r[i] + 0.3) * 0.7); }
            else { bl[w] = l[i]; br[w] = r[i]; double d = 3 + depth * 0.004 * SR * (0.5 + 0.5 * Math.Sin(2 * Math.PI * ph)); a = Dsp2.Hermite(bl, len, w - d); b = Dsp2.Hermite(br, len, w - d); if (++w >= len) w = 0; }
            l[i] = Dsp2.Sanitize((float)(l[i] + (a - l[i]) * mix)); r[i] = Dsp2.Sanitize((float)(r[i] + (b - r[i]) * mix));
        }
    }
}

// the tape (or record) slows to a halt, then spins back up. Press Stop it yourself or let it happen on the beat
class TapeStopFx : SyncFx
{
    float[] bl, br; int len, w; long wAbs; double posAbs, prog; int state, xf;   // 0 live, 1 stopping, 2 stopped, 3 spinning up, 4 crossfading back to live
    Biquad lpf = new Biquad();
    public TapeStopFx()
    {
        Type = "tapestop"; Title = "Tape Stop"; Category = "Creative";
        AddC("engage", "Stop it now", new[] { "Off", "On" }, 0); AddC("auto", "Also stop by itself", new[] { "No", "On the beat" }, 0); AddP("every", "Every", 1, 32, 8, "beats"); AddP("hold", "Stay stopped for", 0.25f, 4, 1, "beats");
        AddP("stop", "Time to stop", 30, 3000, 700, "ms"); AddP("start", "Time to start again", 30, 3000, 350, "ms"); AddP("curve", "Curve", 0.4f, 3, 1.5f, ""); AddP("lp", "Darker as it slows", 0, 100, 40, "%");
    }
    protected override void Update() { len = SR * 4; if (bl == null || bl.Length != len) { bl = new float[len]; br = new float[len]; w = 0; wAbs = 0; } }
    public override void Reset() { base.Reset(); if (bl != null) { Array.Clear(bl, 0, len); Array.Clear(br, 0, len); } state = 0; lpf.Reset(); wAbs = 0; w = 0; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); bool eng = Get("engage") > 0.5f, auto = Get("auto") > 0.5f; double every = Get("every"), hold = Get("hold"), stopS = Math.Max(16, Get("stop") * 0.001 * SR), startS = Math.Max(16, Get("start") * 0.001 * SR), curve = Get("curve"), lpA = Get("lp") / 100.0; int XF = (int)(0.02 * SR);
        for (int i = 0; i < n; i++)
        {
            bl[w] = l[i]; br[w] = r[i]; bool want = eng; if (auto && tr) want = want || (BeatAt(i) % every) >= every - hold;
            if (want && (state == 0 || state == 4)) { state = 1; posAbs = wAbs - (state == 4 ? 0 : 0); prog = 0; }
            if (!want && (state == 1 || state == 2)) { state = 3; prog = 0; if (wAbs - posAbs > len - 2000) posAbs = wAbs - (len - 2000); }
            double rate = 1; float oL = l[i], oR = r[i];
            if (state == 1) { prog += 1.0 / stopS; rate = Math.Pow(Math.Max(0, 1 - prog), curve); if (prog >= 1) { state = 2; rate = 0; } }
            else if (state == 2) rate = 0;
            else if (state == 3) { prog += 1.0 / startS; rate = Math.Pow(Math.Min(1, prog), 1.0 / curve); if (prog >= 1) { state = 4; xf = 0; rate = 1; } }
            else if (state == 4) rate = 1;
            if (state != 0)
            {
                posAbs += rate; double rp = posAbs; int idx = (int)(((rp % len) + len) % len); double fr = rp - Math.Floor(rp);
                double q = ((rp % len) + len) % len; oL = Dsp2.Hermite(bl, len, q); oR = Dsp2.Hermite(br, len, q);
                float amp = state == 2 ? 0f : (float)Math.Min(1.0, rate * 10 + (state == 4 ? 1 : 0)); oL *= amp; oR *= amp;
                double fc = 20000 * (1 - lpA) + (700 + 19300 * Math.Pow(rate, 0.7)) * lpA; lpf.Set(Biquad.LowPass, SR, fc, 0.7071, 0); oL = lpf.TickL(oL); oR = lpf.TickR(oR);
                if (state == 4) { xf++; float t = Math.Min(1f, xf / (float)XF); oL = oL * (1 - t) + l[i] * t; oR = oR * (1 - t) + r[i] * t; if (xf >= XF) state = 0; }
            }
            else { lpf.TickL(oL); lpf.TickR(oR); }
            w++; if (w >= len) w = 0; wAbs++; l[i] = Dsp2.Sanitize(oL); r[i] = Dsp2.Sanitize(oR);
        }
        EndBlock(n);
    }
}

// a DJ scratch on the beat: the last slice is dragged back and forth like a record
class ScratchFx : SyncFx
{
    float[] bl, br; int len, w; int curIdx = int.MinValue; int w0; double sliceLen;
    public static readonly string[] Shapes = { "Baby scratch", "Chirp", "Transform (chop)", "Tear", "Random" };
    public ScratchFx()
    {
        Type = "scratch"; Title = "Scratch Effect"; Category = "Creative";
        AddC("sync", "Scratch length", Dsp2.SyncNames, 4); AddC("shape", "Style", Shapes, 0); AddP("rate", "Back and forths per slice", 1, 16, 4, ""); AddP("depth", "How far", 5, 100, 70, "%");
        AddC("when", "When", new[] { "Every slice", "Every 2nd", "Every 4th", "Last slice of the bar" }, 3); AddP("seed", "Pattern number", 0, 99, 3, ""); AddP("mix", "Mix", 0, 100, 100, "%");
    }
    protected override void Update() { len = SR * 4; if (bl == null || bl.Length != len) { bl = new float[len]; br = new float[len]; w = 0; } }
    public override void Reset() { base.Reset(); if (bl != null) { Array.Clear(bl, 0, len); Array.Clear(br, 0, len); } curIdx = int.MinValue; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); int sy = (int)Get("sync"); double gridBeats = sy > 0 ? Dsp2.SyncBeats[sy] : 1; int shape = (int)Get("shape"); double rate = Get("rate"), depth = Get("depth") / 100.0, mix = Get("mix") / 100.0; int when = (int)Get("when"); uint seed = (uint)Get("seed");
        double slice = 60.0 / Math.Max(20, Fx.ProjectBpm) * gridBeats * SR;
        for (int i = 0; i < n; i++)
        {
            float inL = l[i], inR = r[i]; bl[w] = inL; br[w] = inR; float oL = inL, oR = inR;
            if (tr)
            {
                double beat = BeatAt(i); int idx = (int)Math.Floor(beat / gridBeats); double ph = beat / gridBeats - idx; bool active;
                switch (when) { case 0: active = true; break; case 1: active = idx % 2 == 1; break; case 2: active = idx % 4 == 3; break; default: { double per = Math.Max(1, Math.Round(4.0 / gridBeats)); active = (idx % (int)per) == (int)per - 1; break; } }
                if (idx != curIdx) { curIdx = idx; w0 = w; sliceLen = slice; }
                if (active)
                {
                    double x, amp = 1;
                    switch (shape)
                    {
                        case 0: x = 0.5 - 0.5 * Math.Cos(2 * Math.PI * rate * ph); break;
                        case 1: x = (Math.Sin(2 * Math.PI * rate * ph) * 0.5 + 0.5); x = x * x; break;
                        case 2: x = 0.5 - 0.5 * Math.Cos(2 * Math.PI * rate * ph); amp = (Math.Floor(ph * rate * 4) % 2 == 0) ? 1 : 0; break;
                        case 3: x = (ph * rate) % 1.0; break;
                        default: { double u = U01(Hash((uint)(ph * rate * 3), seed + (uint)idx)); x = u; break; }
                    }
                    double p = w0 - sliceLen * depth * (1 - x) - 2; if (p < 0) p += len; double g = Math.Min(1.0, Math.Min(ph * 40.0, (1 - ph) * 40.0)) * amp;
                    oL = Dsp2.Hermite(bl, len, p) * (float)g; oR = Dsp2.Hermite(br, len, p) * (float)g;
                }
            }
            if (++w >= len) w = 0; l[i] = (float)(inL + (oL - inL) * mix); r[i] = (float)(inR + (oR - inR) * mix);
        }
        EndBlock(n);
    }
}

// beat repeat / stutter edit: captures a slice and repeats it, each time quieter, higher and darker if you like
class RepeatFx : SyncFx
{
    float[] bl, br; int len, w; int curBar = int.MinValue; bool active; int capStart, capLen; int repStart; int lastSlice = int.MinValue; double pitchRamp;
    public RepeatFx()
    {
        Type = "repeat"; Title = "Beat Repeat"; Category = "Creative";
        AddC("sync", "Repeat length", Dsp2.SyncNames, 7); AddC("mode", "Happens", new[] { "All the time", "Randomly, bar by bar", "Last beat of every bar", "Last 2 beats of every 4 bars" }, 1); AddP("chance", "Chance per bar", 0, 100, 40, "%"); AddP("grid", "Repeat for", 0.25f, 4, 1, "beats");
        AddP("decay", "Quieter each repeat", 0, 12, 0, "dB"); AddP("ramp", "Pitch rise per repeat", -2, 2, 0, "st"); AddP("dark", "Darker each repeat", 0, 100, 0, "%"); AddP("seed", "Pattern number", 0, 99, 5, ""); AddP("mix", "Mix", 0, 100, 100, "%");
    }
    Biquad lpf = new Biquad();
    protected override void Update() { len = SR * 4; if (bl == null || bl.Length != len) { bl = new float[len]; br = new float[len]; w = 0; } }
    public override void Reset() { base.Reset(); if (bl != null) { Array.Clear(bl, 0, len); Array.Clear(br, 0, len); } curBar = int.MinValue; active = false; lpf.Reset(); }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); int sy = (int)Get("sync"); double rb = sy > 0 ? Dsp2.SyncBeats[sy] : 0.5; int mode = (int)Get("mode"); double chance = Get("chance") / 100.0, span = Get("grid"), decay = Get("decay"), ramp = Get("ramp"), dark = Get("dark") / 100.0, mix = Get("mix") / 100.0; uint seed = (uint)Get("seed");
        int sliceLen = (int)Math.Max(64, 60.0 / Math.Max(20, Fx.ProjectBpm) * rb * SR);
        for (int i = 0; i < n; i++)
        {
            float inL = l[i], inR = r[i]; bl[w] = inL; br[w] = inR; float oL = inL, oR = inR;
            if (tr)
            {
                double beat = BeatAt(i); int bar = (int)Math.Floor(beat / 4.0); double inBar = beat - bar * 4.0; bool on;
                switch (mode)
                {
                    case 0: on = true; break;
                    case 1: on = U01(Hash((uint)bar, seed * 31u + 7)) < chance && inBar >= 4 - span - 0.0001 && inBar < 4; if (U01(Hash((uint)bar, seed * 31u + 7)) < chance && span >= 4) on = true; break;
                    case 2: on = inBar >= 3; break;
                    default: on = (bar % 4) == 3 && inBar >= 2; break;
                }
                if (mode == 1 && on == false && U01(Hash((uint)bar, seed * 31u + 7)) < chance) { /* started earlier in the bar */ }
                if (on && !active) { active = true; capStart = w; capLen = sliceLen; repStart = w; pitchRamp = 1; lpf.Set(Biquad.LowPass, SR, 20000, 0.7071, 0); }
                if (!on && active) active = false;
                if (active)
                {
                    int t = ((w - repStart) % len + len) % len; int k = t / capLen; int tt = t % capLen;
                    if (k == 0) { /* first slice plays live while it is captured */ }
                    else
                    {
                        double rate = Math.Pow(2, ramp * k / 12.0); double g = Math.Pow(10, -decay * k / 20.0); int p0 = (capStart + (int)(tt * rate) % capLen);
                        float x = bl[p0 % len], y = br[p0 % len]; float edge = Math.Min(1f, Math.Min(tt / 48f, (capLen - tt) / 48f)); if (edge < 0) edge = 0;
                        if (dark > 0) { double fc = 20000 * Math.Pow(0.03, dark * Math.Min(1.0, k / 6.0)); lpf.Set(Biquad.LowPass, SR, Math.Max(200, fc), 0.7071, 0); x = lpf.TickL(x); y = lpf.TickR(y); }
                        oL = x * (float)g * edge + inL * (1 - edge) * 0; oR = y * (float)g * edge;
                    }
                }
            }
            if (++w >= len) w = 0; l[i] = (float)(inL + (oL - inL) * mix); r[i] = (float)(inR + (oR - inR) * mix);
        }
        EndBlock(n);
    }
}

// a modulator that can move volume, pan, a resonant filter, drive, pitch or width from an LFO, a step pattern, the sound's own level,
// random values, a beat ramp or a sidechain-style pump curve
class ModFx : SyncFx
{
    public static readonly string[] Sources = { "LFO", "Steps", "Envelope (follows the sound)", "Random", "Beat ramp", "Pump (ducking curve)", "Fixed" };
    public static readonly string[] Targets = { "Volume", "Pan", "Filter", "Drive", "Vibrato (pitch)", "Stereo width" };
    float[] io = new float[2], oo = new float[2]; double[] mv = new double[2]; Svf2[] f = { new Svf2(), new Svf2() }; Lfo2[] lf = { new Lfo2(), new Lfo2() }; double[] sm = new double[2], env = new double[2]; double freePh; float[] dbl = new float[0], dbr = new float[0]; int dlen, dw; int sub; double[] fcache = new double[2]; double rndV = 0; long rndPh = -1; Random rnd = new Random(2);
    public ModFx()
    {
        Type = "mod"; Title = "Modulator"; Category = "Bass & EDM";
        AddC("src", "Moves with", Sources, 0); AddC("tgt", "Controls", Targets, 0); AddC("shape", "LFO shape", Lfo2.Shapes, 0); AddC("sync", "Speed", Dsp2.SyncNames, 4); AddP("rate", "Free speed", 0.05f, 40, 2, "Hz"); AddP("depth", "Depth", 0, 100, 80, "%");
        AddP("phase", "Right side offset", 0, 180, 0, "deg"); AddP("smooth", "Smoothing", 0, 300, 4, "ms"); AddP("nsteps", "Number of steps", 2, 16, 8, "");
        for (int i = 1; i <= 16; i++) AddP("s" + i, "Step " + i, 0, 100, (i % 2 == 1) ? 100 : 25, "%");
        AddC("ftype", "Filter type", new[] { "Low-pass", "Band-pass", "High-pass", "Notch" }, 0); AddP("cut", "Filter centre", 40, 16000, 800, "Hz"); AddP("res", "Resonance", 0.5f, 20, 4, ""); AddP("range", "Filter sweep", 0, 6, 3, "oct");
        AddP("eatt", "Envelope attack", 0.5f, 100, 4, "ms"); AddP("erel", "Envelope release", 5, 800, 120, "ms"); AddP("esens", "Envelope sensitivity", 0, 100, 60, "%"); AddP("pump", "Pump curve", 0, 100, 55, "%"); AddP("drive", "Drive (when it controls drive)", 0, 36, 18, "dB"); AddP("mix", "Mix", 0, 100, 100, "%");
    }
    protected override void Update() { dlen = SR / 20 + 16; if (dbl.Length != dlen) { dbl = new float[dlen]; dbr = new float[dlen]; dw = 0; } }
    public override void Reset() { base.Reset(); foreach (var q in f) q.Reset(); lf[0].Reset(); lf[1].Reset(); sm[0] = sm[1] = 0; env[0] = env[1] = 0; if (dbl.Length > 0) { Array.Clear(dbl, 0, dlen); Array.Clear(dbr, 0, dlen); } fcache[0] = fcache[1] = 0; }
    static readonly int[] ShapeMap = { 0, 1, 2, 3, 4, 5, 6 };
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); int src = (int)Get("src"), tgt = (int)Get("tgt"), shape = (int)Get("shape"), sy = (int)Get("sync"); double rate = Get("rate"), depth = Get("depth") / 100.0, phOff = Get("phase") / 360.0, mix = Get("mix") / 100.0; int ns = (int)Get("nsteps"); double smA = Get("smooth") <= 0 ? 1 : 1 - Math.Exp(-1.0 / (Get("smooth") * 0.001 * SR));
        double cycle = sy > 0 ? Dsp2.SyncBeats[sy] : 0; int ft = (int)Get("ftype"); double cut = Get("cut"), res = Get("res"), range = Get("range"); double ea = DspMath.Tc(Get("eatt"), SR), er = DspMath.Tc(Get("erel"), SR), esens = Get("esens") / 100.0 * 12 + 0.5; double pumpK = 2 + Get("pump") / 100.0 * 14, driveDb = Get("drive");
        float[] stepv = new float[16]; for (int i = 0; i < 16; i++) stepv[i] = Get("s" + (i + 1)) / 100f; bool uni = src == 2 || src == 4 || src == 5 || src == 6 && false;
        for (int i = 0; i < n; i++)
        {
            double beat = BeatAt(i); io[0] = l[i]; io[1] = r[i];
            for (int c = 0; c < 2; c++)
            {
                double ph = cycle > 0 ? (beat / (src == 1 ? cycle * ns : cycle)) : freePh; ph += c * phOff; ph -= Math.Floor(ph); double m01, mb;
                switch (src)
                {
                    case 0: { double dph = cycle > 0 ? 0 : rate / SR; if (cycle > 0) { lf[c].Sync(ph); mb = lf[c].Tick(ShapeMap[shape], 0); } else mb = lf[c].Tick(ShapeMap[shape], dph); m01 = mb * 0.5 + 0.5; break; }
                    case 1: { int si = (int)(ph * ns) % ns; m01 = stepv[si]; mb = m01 * 2 - 1; break; }
                    case 2: { double a = Math.Abs(io[c]); env[c] += (a - env[c]) * (a > env[c] ? 1 - ea : 1 - er); m01 = Math.Min(1.0, env[c] * esens); mb = m01 * 2 - 1; break; }
                    case 3: { long rp = (long)Math.Floor(cycle > 0 ? beat / cycle * 1.0 : freePh * 4); if (rp != rndPh) { rndPh = rp; rndV = rnd.NextDouble(); } m01 = rndV; mb = m01 * 2 - 1; break; }
                    case 4: m01 = ph; mb = ph * 2 - 1; break;
                    case 5: m01 = (1 - Math.Exp(-ph * pumpK)) / (1 - Math.Exp(-pumpK)); mb = m01 * 2 - 1; break;
                    default: m01 = 0.5; mb = 0; break;
                }
                sm[c] += (m01 - sm[c]) * smA; m01 = sm[c]; mv[c] = m01;
            }
            if (cycle <= 0) { freePh += rate / SR / (src == 1 ? ns : 1); freePh -= Math.Floor(freePh); }
            switch (tgt)
            {
                case 0: for (int c = 0; c < 2; c++) oo[c] = (float)(io[c] * (1 - depth * (1 - mv[c]))); break;
                case 1: { double pos = depth * (mv[0] * 2 - 1); double a = (pos + 1) * Math.PI / 4; float gl = (float)(Math.Cos(a) * 1.4142), gr = (float)(Math.Sin(a) * 1.4142); oo[0] = io[0] * gl; oo[1] = io[1] * gr; break; }
                case 2:
                    for (int c = 0; c < 2; c++)
                    {
                        if (((sub + c) & 7) == 0) { double m = uni ? mv[c] : mv[c] * 2 - 1; double fc = cut * Math.Pow(2, range * depth * (src == 2 || src == 4 || src == 5 ? mv[c] : (mv[c] * 2 - 1))); f[c].Set(Math.Max(30, Math.Min(fc, SR * 0.45)), res, SR); }
                        oo[c] = f[c].Tick(io[c], ft);
                    }
                    break;
                case 3: { double pre = 1 + depth * mv[0] * DspMath.DbToLin(driveDb) ; for (int c = 0; c < 2; c++) oo[c] = (float)(Math.Tanh(io[c] * pre) / Math.Sqrt(Math.Max(1, pre))); break; }
                case 4: { dbl[dw] = io[0]; dbr[dw] = io[1]; double d0 = 4 + depth * 0.006 * SR * mv[0]; oo[0] = Dsp2.Hermite(dbl, dlen, dw - d0); oo[1] = Dsp2.Hermite(dbr, dlen, dw - d0); if (++dw >= dlen) dw = 0; break; }
                default: { float m = (io[0] + io[1]) * 0.5f, s = (io[0] - io[1]) * 0.5f; double wdt = 1 + depth * ((mv[0] * 2 - 1) * 1.5); wdt = Math.Max(0, wdt); oo[0] = (float)(m + s * wdt); oo[1] = (float)(m - s * wdt); break; }
            }
            sub++; l[i] = Dsp2.Sanitize((float)(io[0] + (oo[0] - io[0]) * mix)); r[i] = Dsp2.Sanitize((float)(io[1] + (oo[1] - io[1]) * mix));
        }
        EndBlock(n);
    }
}

// risers, downlifters, impacts, sub drops, whooshes, noise bursts and textures that lock to the bars of your song
class GenFx : SyncFx
{
    public static readonly string[] Kinds = { "Riser", "Downlifter", "Impact", "Sub drop", "Whoosh", "Noise burst", "Texture" };
    uint rng = 777; Svf2 f1 = new Svf2(), f2 = new Svf2(); Svf2 g1 = new Svf2(), g2 = new Svf2(); double phase, ph2; int sub; double z1, z2;
    public GenFx()
    {
        Type = "gen"; Title = "Sound Generator"; Category = "Creative";
        AddC("kind", "Sound", Kinds, 0); AddP("every", "Happens every", 1, 32, 8, "bars"); AddP("len", "Length", 0.25f, 16, 4, "bars"); AddP("level", "Level", -40, 6, -12, "dB"); AddP("tone", "Tone / pitch", 0, 100, 50, "%");
        AddP("res", "Whistle (resonance)", 0.5f, 12, 1.2f, ""); AddP("width", "Stereo width", 0, 100, 70, "%"); AddC("grid", "Burst rate (noise burst)", Dsp2.SyncNames, 4); AddP("from", "Sub drop start", 30, 200, 90, "Hz"); AddP("to", "Sub drop end", 20, 100, 28, "Hz");
        AddP("only", "Original sound", 0, 100, 100, "%");
    }
    public override void Reset() { base.Reset(); f1.Reset(); f2.Reset(); g1.Reset(); g2.Reset(); phase = ph2 = 0; z1 = z2 = 0; }
    float Noise() { rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5; return (rng & 0xFFFFFF) / 8388608f - 1f; }
    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty(); int kind = (int)Get("kind"); double every = Get("every") * 4, length = Get("len") * 4, lv = DspMath.DbToLin(Get("level")), tone = Get("tone") / 100.0, res = Get("res"), wid = Get("width") / 100.0, from = Get("from"), to = Get("to"); double keep = Get("only") / 100.0;
        int gi = (int)Get("grid"); double gridB = gi > 0 ? Dsp2.SyncBeats[gi] : 1;
        for (int i = 0; i < n; i++)
        {
            double beat = BeatAt(i); double cyc = beat % every; double u = 0; double outA = 0, outB = 0; bool inside = false;
            switch (kind)
            {
                case 0: case 4: inside = cyc >= every - length; if (inside) u = (cyc - (every - length)) / length; break;
                case 1: case 2: case 3: inside = cyc < length; if (inside) u = cyc / length; break;
                case 5: inside = true; u = (beat % gridB) / gridB; break;
                default: inside = true; u = 0; break;
            }
            if (inside)
            {
                float na = Noise(), nb = Noise(); double amp = 0;
                if ((sub++ & 7) == 0)
                {
                    switch (kind)
                    {
                        case 0: { double fc = 250 * Math.Pow(70, Math.Pow(u, 1.4)); f1.Set(fc, res, SR); f2.Set(fc, res, SR); break; }
                        case 1: { double fc = 14000 * Math.Pow(0.025, Math.Pow(u, 0.7)); f1.Set(fc, res, SR); f2.Set(fc, res, SR); break; }
                        case 2: { double fc = 9000 * Math.Pow(0.03, Math.Min(1.0, u * 3)); f1.Set(fc, 0.8, SR); f2.Set(fc, 0.8, SR); break; }
                        case 4: { double s = u < 0.5 ? u * 2 : 2 - u * 2; double fc = 300 * Math.Pow(40, s); f1.Set(fc, res + 1.5, SR); f2.Set(fc, res + 1.5, SR); break; }
                        case 5: { double fc = 6000 * Math.Pow(0.15, u); f1.Set(fc, 0.7, SR); f2.Set(fc, 0.7, SR); break; }
                        default: { double fc = 600 + 3000 * (0.5 + 0.5 * Math.Sin(beat * 0.37)) ; f1.Set(fc, res, SR); f2.Set(fc * 1.13, res, SR); break; }
                    }
                }
                switch (kind)
                {
                    case 0: { amp = u * u; outA = f1.Tick(na, 0) * amp; outB = f2.Tick(nb, 0) * amp; double fr = (200 + 1800 * u * u) * (0.5 + tone); phase += fr / SR; outA += Math.Sin(2 * Math.PI * phase) * 0.18 * u * tone * 2; outB = outB * wid + outA * (1 - wid); break; }
                    case 1: { amp = Math.Pow(1 - u, 1.5); outA = f1.Tick(na, 0) * amp; outB = f2.Tick(nb, 0) * amp; double fr = (1800 - 1600 * u) * (0.5 + tone); phase += fr / SR; outA += Math.Sin(2 * Math.PI * phase) * 0.15 * (1 - u) * tone * 2; break; }
                    case 2: { double tt = cyc / 4.0 * 60.0 / Math.Max(20, Fx.ProjectBpm) * 4.0; double dec = Math.Exp(-cyc * 60.0 / Fx.ProjectBpm / (0.35 + tone * 0.9)); double fq = 38 + 90 * Math.Exp(-cyc * 60.0 / Fx.ProjectBpm / 0.06); phase += fq / SR; amp = dec; outA = f1.Tick(na, 0) * Math.Exp(-cyc * 60.0 / Fx.ProjectBpm / 0.25) * 0.8 + Math.Sin(2 * Math.PI * phase) * dec * 1.0; outB = f2.Tick(nb, 0) * Math.Exp(-cyc * 60.0 / Fx.ProjectBpm / 0.25) * 0.8 + Math.Sin(2 * Math.PI * phase) * dec; break; }
                    case 3: { double fq = to + (from - to) * Math.Pow(1 - u, 2.2); phase += fq / SR; amp = Math.Pow(1 - u, 0.4) * Math.Min(1.0, (1 - u) * 6); outA = outB = Math.Sin(2 * Math.PI * phase) * amp; break; }
                }
                if (kind == 4) { double s = u < 0.5 ? u * 2 : 2 - u * 2; amp = Math.Sin(Math.PI * u); outA = f1.Tick(na, 1) * amp * 3; outB = f2.Tick(nb, 1) * amp * 3; outB = outB * wid + outA * (1 - wid); }
                if (kind == 5) { amp = Math.Exp(-u * 8); outA = f1.Tick(na, 0) * amp; outB = f2.Tick(nb, 0) * amp; }
                if (kind == 6) { double p = 0.5 + 0.5 * Math.Sin(beat * 0.11); outA = f1.Tick(na, 1) * 1.6 * (0.4 + 0.6 * p); outB = f2.Tick(nb, 1) * 1.6 * (0.4 + 0.6 * (1 - p)); ph2 += (900 + 700 * Math.Sin(beat * 0.05)) / SR; double ping = Math.Max(0, Math.Sin(beat * 2.7)); ping = ping * ping * ping * ping * ping * ping; outA += Math.Sin(2 * Math.PI * ph2) * ping * 0.1 * tone; outB += Math.Sin(2 * Math.PI * ph2 * 1.5) * ping * 0.1 * tone; }
            }
            l[i] = Dsp2.Sanitize((float)(l[i] * keep + outA * lv)); r[i] = Dsp2.Sanitize((float)(r[i] * keep + outB * lv));
        }
        EndBlock(n);
    }
}
