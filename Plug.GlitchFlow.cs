using System;
using System.Collections.Generic;

// ======================================================================
//  PURPHEX GLITCHFLOW: a real-time stutter / beat-repeat / rhythmic glitch engine with its own window.
//
//  How it works (all in this file, no allocation on the audio thread: every buffer is made when the plugin is created):
//  - REFRESH RATE sets a "window" on the song grid (for example every 1/2 note). At the start of each window the incoming audio is
//    recorded into a slice buffer (two buffers take turns, so the old slice can fade out while the new one records).
//  - REPEAT RATE sets the length of the slice. The first slice-length of a window plays live (it is being recorded); after that the
//    slice repeats until the next window. The PATTERN decides which repeats sound (all, every other, last beat, random, build-up...).
//  - FREEZE holds the current slice: recording stops, the slice repeats until FREEZE is released, then the audio is live again and
//    the next window records a fresh slice.
//  - Timing comes from the song position while the song plays (so it locks to the bars, follows BPM changes, loops and jumps), and
//    from a free-running counter when it is stopped. A jump (loop, seek, new tempo) never plays half-recorded audio: the rest of that
//    window plays live and the next window records again.
//  - Every repeat has a short fade at its edges (shape: Hard, Smooth, Swell or Fade-out) and the switch between live and repeated
//    audio glides, so there are no clicks. Repeats can be reversed, pitched, pitch-stepped and wobbled.
//  - Then the optional modules: low-pass (with resonance), high-pass, telephone band, rhythmic gate, drive, width, modulation,
//    delay and reverb, each with its own ON switch. DRY/WET mixes the result with the input; IN and OUT gain set the levels.
// ======================================================================
class GlitchFlowFx : SyncFx
{
    // the note values (quarter note = 1 beat)
    public static readonly string[] Div = { "Free (ms)", "4 bars", "2 bars", "1/1", "1/2 dotted", "1/2", "1/2 triplet", "1/4 dotted", "1/4", "1/4 triplet", "1/8 dotted", "1/8", "1/8 triplet", "1/16 dotted", "1/16", "1/16 triplet", "1/32", "1/32 triplet", "1/64" };
    public static readonly double[] DivBeats = { 0, 16, 8, 4, 3, 2, 4 / 3.0, 1.5, 1, 2 / 3.0, 0.75, 0.5, 1 / 3.0, 0.375, 0.25, 1 / 6.0, 0.125, 1 / 12.0, 0.0625 };
    public static readonly string[] DivS = { "4 bars", "2 bars", "1/1", "1/2 dotted", "1/2", "1/2 triplet", "1/4 dotted", "1/4", "1/4 triplet", "1/8 dotted", "1/8", "1/8 triplet", "1/16 dotted", "1/16", "1/16 triplet", "1/32", "1/32 triplet", "1/64" };
    static readonly int[] MultUp = { 1, 2, 4, 8 }, MultDown = { 8, 4, 2, 1 }; static readonly double[] BndUp = { 0, 0.5, 0.75, 0.875 }, BndDown = { 0, 0.25, 0.5, 0.75 };
    public static readonly string[] Patterns = { "Constant", "Every other", "Last beat (fill)", "Last half bar", "Random", "Build-up (faster)", "Slow-down", "Triplet skip", "First half only", "3-3-2 gate" };
    public static readonly string[] Shapes = { "Hard", "Smooth", "Swell", "Fade-out" };
    public static readonly string[] ModTargets = { "Pitch wobble", "Filter sweep", "Auto-pan" };
    public static readonly string[] DriveModes = { "Soft", "Hard clip", "Bitcrush" };

    const int MaxSR = 96000, SliceSec = 4, DelaySec = 3;
    readonly float[][] sl = new float[2][], sr = new float[2][];   // two slice buffers (L and R) that take turns
    readonly float[] dlL, dlR; int dlW;
    // reverb (4 combs + 2 all-passes per side, sizes scaled to the sample rate)
    static readonly int[] CombT = { 1116, 1188, 1277, 1356 }, ApT = { 556, 441 };
    readonly float[][] cb = new float[8][], ab = new float[4][]; readonly int[] ci = new int[8], ai = new int[4]; readonly float[] cf = new float[8];
    int recBuf, oldBuf = 1, recPos, recLen;          // recording slice: which buffer, where, how much is recorded
    long winIdx = long.MinValue, lastPos = long.MinValue; bool armed, frozen, wasFreeze; long frozenAnchor;
    double readPos, a, gGate = 1, panPh, modPh; int lastCycle = -1, lastSeg = -1; double oldPh, oldEnv, lastEnv, lastRep, lastRef; int oldLen;   // oldPh/oldLen: the previous slice keeps playing while it fades out
    double inG = 1, outG = 1, mixS = 1;               // smoothed gains
    // filters (TPT state-variable), per side
    double lpS1L, lpS2L, lpS1R, lpS2R, hpS1L, hpS2L, hpS1R, hpS2R, t1L, t2L, t1R, t2R, u1L, u2L, u1R, u2R, lpCut = 20000, crushHold, crushL, crushR;
    // what the window shows (written by the audio thread, read by the window: a torn read only draws one odd frame)
    public readonly float[] View = new float[256]; public volatile float ViewPh, InPk, OutPk; public volatile bool ViewActive, ViewFrozen; public volatile int ViewCycle, ViewCycles; public volatile float ViewWinPh;
    public volatile bool[] ViewSteps = new bool[32];

    public GlitchFlowFx()
    {
        Type = "glitchflow"; Title = "Purphex GlitchFlow"; Category = "Purphex VST"; Blurb = "Stutter, beat-repeat and glitch engine with freeze, patterns and effect modules";
        // core
        AddC("rsync", "Repeat rate", Div, 14); AddP("rate", "Repeat rate (free)", 10, 2000, 125, "ms");
        AddC("fsync", "Refresh rate", Div, 5); AddP("refresh", "Refresh rate (free)", 20, 8000, 1000, "ms");
        AddC("freeze", "Freeze", new[] { "Off", "On" }, 0);
        AddC("pattern", "Pattern", Patterns, 0); AddP("intensity", "Repeat intensity", 0, 100, 100, "%"); AddP("length", "Repeat length", 5, 100, 100, "%");
        AddP("decay", "Repeat decay", 0, 90, 0, "%"); AddP("fade", "Fade", 0.3f, 50, 3, "ms"); AddC("shape", "Fade shape", Shapes, 1); AddP("seed", "Random seed", 0, 99, 7, "");
        AddC("reverse", "Reverse repeats", new[] { "Off", "On" }, 0); AddP("pitch", "Repeat pitch", -24, 24, 0, "st"); AddP("pstep", "Pitch step per repeat", -12, 12, 0, "st");
        AddP("in", "Input gain", -24, 24, 0, "dB"); AddP("out", "Output gain", -24, 24, 0, "dB"); AddP("mix", "Dry/Wet", 0, 100, 100, "%");
        // modules
        AddC("lp_on", "Low-pass", new[] { "Off", "On" }, 0); AddP("lp_cut", "Low-pass cutoff", 80, 20000, 2500, "Hz"); AddP("lp_res", "Low-pass resonance", 0, 100, 30, "%");
        AddC("hp_on", "High-pass", new[] { "Off", "On" }, 0); AddP("hp_cut", "High-pass cutoff", 20, 8000, 400, "Hz");
        AddC("tel_on", "Telephone", new[] { "Off", "On" }, 0); AddP("tel_amt", "Telephone drive", 0, 100, 40, "%");
        AddC("gate_on", "Rhythm gate", new[] { "Off", "On" }, 0); AddC("gate_sync", "Gate rate", DivS, 13); AddP("gate_duty", "Gate length", 10, 90, 50, "%"); AddP("gate_depth", "Gate depth", 0, 100, 100, "%");
        AddC("drv_on", "Drive", new[] { "Off", "On" }, 0); AddC("drv_mode", "Drive type", DriveModes, 0); AddP("drv_amt", "Drive amount", 0, 100, 40, "%");
        AddC("wid_on", "Width", new[] { "Off", "On" }, 0); AddP("width", "Stereo width", 0, 200, 150, "%");
        AddC("mod_on", "Modulation", new[] { "Off", "On" }, 0); AddC("mod_tgt", "Modulation target", ModTargets, 0); AddP("mod_rate", "Modulation rate", 0.05f, 12, 2, "Hz"); AddP("mod_depth", "Modulation depth", 0, 100, 40, "%");
        AddC("dly_on", "Delay", new[] { "Off", "On" }, 0); AddC("dly_sync", "Delay time", DivS, 9); AddP("dly_fb", "Delay feedback", 0, 90, 40, "%"); AddP("dly_mix", "Delay mix", 0, 100, 30, "%");
        AddC("rev_on", "Reverb", new[] { "Off", "On" }, 0); AddP("rev_size", "Reverb size", 0, 100, 60, "%"); AddP("rev_mix", "Reverb mix", 0, 100, 25, "%");
        int sn = MaxSR * SliceSec; for (int b = 0; b < 2; b++) { sl[b] = new float[sn]; sr[b] = new float[sn]; }
        dlL = new float[MaxSR * DelaySec]; dlR = new float[MaxSR * DelaySec];
        for (int i = 0; i < 8; i++) cb[i] = new float[(int)(CombT[i % 4] * (MaxSR / 44100.0)) + 40]; for (int i = 0; i < 4; i++) ab[i] = new float[(int)(ApT[i % 2] * (MaxSR / 44100.0)) + 40];
    }

    public override void Reset()
    {
        base.Reset(); winIdx = long.MinValue; lastPos = long.MinValue; armed = false; frozen = false; recLen = 0; recPos = 0; a = 0; gGate = 1; lastCycle = -1; lastSeg = -1; oldLen = 0;
        lpS1L = lpS2L = lpS1R = lpS2R = hpS1L = hpS2L = hpS1R = hpS2R = t1L = t2L = t1R = t2R = u1L = u2L = u1R = u2R = 0; crushHold = 0;
        Array.Clear(dlL, 0, dlL.Length); Array.Clear(dlR, 0, dlR.Length); for (int i = 0; i < 8; i++) { Array.Clear(cb[i], 0, cb[i].Length); cf[i] = 0; ci[i] = 0; } for (int i = 0; i < 4; i++) { Array.Clear(ab[i], 0, ab[i].Length); ai[i] = 0; }
    }

    // the length in samples of a note value (or the free time)
    double Len(int div, double ms, double bpm) { return div <= 0 ? ms * 0.001 * SR : DivBeats[div] * 60.0 / bpm * SR; }
    static uint H(uint x) { x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16; return x; }
    bool On(string id) { return Get(id) >= 0.5f; }

    // a TPT state-variable filter step: returns low-pass (lp=true) or high-pass
    static double Svf(double x, double g, double k, ref double s1, ref double s2, bool lp)
    {
        double hp = (x - (2 * k + g) * s1 - s2) / (1 + g * (g + 2 * k)) * 1; double v1 = g * hp, bp = v1 + s1; s1 = bp + v1; double v2 = g * bp, low = v2 + s2; s2 = low + v2;
        return lp ? low : hp;
    }

    public override void Process(float[] l, float[] r, int n)
    {
        CheckDirty();
        double bpm = Math.Max(20, Fx.Bpm);
        int rs = (int)Get("rsync"), fs = (int)Get("fsync"), pat = (int)Get("pattern"), shape = (int)Get("shape");
        int maxLen = Math.Min(sl[0].Length, SR * SliceSec) - 4;
        double repLen = Math.Max(16, Math.Min(maxLen, Len(rs, Get("rate"), bpm)));
        double refLen = Math.Max(repLen, Len(fs, Get("refresh"), bpm));          // a window is never shorter than one repeat
        double intensity = Get("intensity") / 100.0, lenFrac = Get("length") / 100.0, decay = Get("decay") / 100.0;
        double fadeS = Math.Max(SR * 0.0003, Get("fade") * 0.001 * SR); if (shape == 0) fadeS = SR * 0.0003;
        uint seed = (uint)Get("seed"); bool rev = On("reverse"); double pitch = Get("pitch"), pstep = Get("pstep");
        bool freeze = On("freeze");
        double inT = Math.Pow(10, Get("in") / 20), outT = Math.Pow(10, Get("out") / 20), mixT = Get("mix") / 100.0;
        double gs = 1 - Math.Exp(-1.0 / (0.01 * SR)), as_ = 1 - Math.Exp(-1.0 / Math.Max(1, fadeS));
        // modules
        bool lpOn = On("lp_on"), hpOn = On("hp_on"), telOn = On("tel_on"), gateOn = On("gate_on"), drvOn = On("drv_on"), widOn = On("wid_on"), modOn = On("mod_on"), dlyOn = On("dly_on"), revOn = On("rev_on");
        double lpTarget = Get("lp_cut"), lpK = 1 - 0.97 * Get("lp_res") / 100.0, hpG = Math.Tan(Math.PI * Math.Min(Get("hp_cut"), SR * 0.45) / SR);
        double telG1 = Math.Tan(Math.PI * 350.0 / SR), telG2 = Math.Tan(Math.PI * 3200.0 / SR), telDrive = 1 + Get("tel_amt") / 100.0 * 6;
        double gateLen = Len((int)Get("gate_sync") + 1, 0, bpm), gateDuty = Get("gate_duty") / 100.0, gateDepth = Get("gate_depth") / 100.0;
        int drvMode = (int)Get("drv_mode"); double drv = Get("drv_amt") / 100.0, drvG = 1 + drv * 15;
        double width = Get("width") / 100.0; int modT = (int)Get("mod_tgt"); double modRate = Get("mod_rate"), modDepth = Get("mod_depth") / 100.0;
        double dlyLen = Math.Max(1, Math.Min(dlL.Length - 2, Len((int)Get("dly_sync") + 1, 0, bpm))), dlyFb = Get("dly_fb") / 100.0, dlyMix = Get("dly_mix") / 100.0;
        double revSize = Get("rev_size") / 100.0, revMix = Get("rev_mix") / 100.0, revFb = 0.7 + revSize * 0.28, revDamp = 0.2 + (1 - revSize) * 0.3; double srK = SR / 44100.0;
        float inPk = 0, outPk = 0;
        bool retime = lastRep > 0 && (Math.Abs(repLen - lastRep) > 0.5 || Math.Abs(refLen - lastRef) > 0.5); lastRep = repLen; lastRef = refLen;   // a new tempo or a new note value: hand over to live until the next window

        for (int i = 0; i < n; i++)
        {
            long pos = tr ? bp + i : freeCnt + i;
            // ---- timing: which window, which repeat, where in it ----
            bool jump = (lastPos != long.MinValue && pos != lastPos + 1) || (retime && i == 0); lastPos = pos;
            long wi = (long)Math.Floor(pos / refLen); double offset = pos - wi * refLen;
            if (!freeze && frozen)
            {   // FREEZE released: the held slice fades out, the sound is live, and the next window records a fresh slice
                frozen = false; armed = false; winIdx = wi; oldPh = readPos; oldLen = Math.Max(1, Math.Min(recLen, (int)repLen)); oldBuf = recBuf; oldEnv = lastEnv;
            }
            if (frozen) { offset = pos - frozenAnchor; if (offset < 0 || jump) { frozenAnchor = pos; offset = 0; } }   // held: the repeats run on from the moment it was held (a loop or a jump restarts them)
            else if (wi != winIdx || jump)
            {   // a new window: the old slice keeps playing while it fades out; the other buffer records the new one
                if (a > 0.0005 && lastCycle >= 1) { oldPh = readPos; oldLen = Math.Max(1, Math.Min(recLen, (int)repLen)); oldBuf = recBuf; oldEnv = lastEnv; } else oldLen = 0;
                winIdx = wi; recBuf ^= 1; recPos = 0; recLen = 0; lastCycle = -1; lastSeg = -1;
                armed = offset < 2;   // it starts at the window's first sample: it can repeat. It landed in the middle (a jump, a new tempo): live until the next window
            }
            if (freeze && !frozen && armed) { frozen = true; frozenAnchor = (long)Math.Round(pos - offset); }   // FREEZE holds this window's slice (it finishes recording it first)
            wasFreeze = freeze;
            // ---- input ----
            inG += (inT - inG) * gs; outG += (outT - outG) * gs; mixS += (mixT - mixS) * gs;
            double xl = l[i] * inG, xr = r[i] * inG; inPk = (float)Math.Max(inPk, Math.Max(Math.Abs(xl), Math.Abs(xr)));
            // record the slice (only the first repeat-length of a window; frozen: nothing is recorded, the slice stays exactly as it is)
            if (armed && recPos < repLen && recPos < maxLen) { sl[recBuf][recPos] = (float)xl; sr[recBuf][recPos] = (float)xr; recPos++; recLen = recPos; int vb = (int)(recPos / repLen * 255); if (vb >= 0 && vb < 256) View[vb] = Math.Max(View[vb] * 0.5f, (float)Math.Max(Math.Abs(xl), Math.Abs(xr))); }

            // ---- which repeat this is (patterns that speed up or slow down use segments of the window) ----
            int seg = 0; double segStart = 0, cycLen = repLen;
            if (!frozen && (pat == 5 || pat == 6))
            {
                double f = offset / refLen; int[] mult = pat == 5 ? MultUp : MultDown; double[] bnd = pat == 5 ? BndUp : BndDown;
                for (int s = 3; s >= 0; s--) if (f >= bnd[s]) { seg = s; segStart = bnd[s] * refLen; cycLen = Math.Max(16, repLen / mult[s]); break; }
            }
            double inSeg = offset - segStart; int cycle = (int)Math.Floor(inSeg / cycLen); double ph = inSeg - cycle * cycLen;
            int gCycle = seg == 0 ? cycle : cycle + 1000 * seg;   // a number for this repeat (for random patterns)
            bool live = !frozen && (seg == 0 && cycle == 0);
            bool active = false;
            if (frozen) active = !(recLen < 2);
            else if (armed && !live)
            {
                double beat = pos / (double)SR * bpm / 60.0, inBar = beat - Math.Floor(beat / 4) * 4; int cycInWin = (int)Math.Floor(offset / repLen);
                switch (pat)
                {
                    case 1: active = cycle % 2 == 1; break;
                    case 2: active = inBar >= 3; break;
                    case 3: active = inBar >= 2; break;
                    case 4: active = (H((uint)winIdx * 31337u + (uint)gCycle * 7u + seed) & 1) == 0; break;
                    case 7: active = cycle % 3 != 2; break;
                    case 8: active = offset < refLen / 2; break;
                    case 9: { int step = cycInWin % 8; active = step != 3 && step != 6; break; }
                    default: active = true; break;
                }
                if (active && intensity < 1) active = (H((uint)winIdx * 7919u + (uint)gCycle * 104729u + seed * 31u) & 0xFFFF) / 65536.0 < intensity;
            }
            if (cycle != lastCycle || seg != lastSeg) { readPos = 0; lastCycle = cycle; lastSeg = seg; }
            // ---- the repeated sound ----
            double audible = Math.Max(fadeS * 2, cycLen * lenFrac), env = 0, wl = 0, wr = 0;
            if (active)
            {
                int rLen = Math.Max(1, Math.Min(recLen, (int)cycLen)); int buf = recBuf;   // the slice recorded in this window
                double sp = Math.Pow(2, (pitch + pstep * Math.Max(0, cycle - 1)) / 12.0); sp = Math.Max(0.25, Math.Min(4, sp));
                if (modOn && modT == 0) sp *= Math.Pow(2, modDepth * 0.5 * Math.Sin(modPh) / 12.0);
                double rp = readPos % rLen; double idx = rev ? rLen - 1 - rp : rp; int i0 = (int)idx; double fr = idx - i0; int i1 = Math.Min(rLen - 1, i0 + 1); if (i0 < 0) i0 = 0;
                wl = sl[buf][i0] + (sl[buf][i1] - sl[buf][i0]) * fr; wr = sr[buf][i0] + (sr[buf][i1] - sr[buf][i0]) * fr;
                readPos += sp;
                if (ph < audible)
                {
                    env = Math.Min(1, Math.Min(ph / fadeS, (audible - ph) / fadeS));
                    if (shape == 2) env *= ph / audible; else if (shape == 3) env *= 1 - ph / audible;
                }
                env *= Math.Pow(1 - decay, Math.Max(0, cycle - 1));
                ViewPh = (float)(ph / cycLen);
            }
            else if (oldLen > 0)
            {   // the previous slice, still playing while it fades out
                double rp = oldPh % oldLen; int i0 = (int)(rev ? oldLen - 1 - rp : rp); if (i0 < 0) i0 = 0; if (i0 >= oldLen) i0 = oldLen - 1; wl = sl[oldBuf][i0]; wr = sr[oldBuf][i0]; oldPh += 1; env = oldEnv;
                if (a < 0.0005) oldLen = 0;
            }
            if (active) lastEnv = env;
            a += ((active ? 1.0 : 0.0) - a) * as_;
            double yl = xl * (1 - a) + wl * env * a, yr = xr * (1 - a) + wr * env * a;

            // ---- modules (on the wet sound) ----
            if (modOn) { modPh += 2 * Math.PI * modRate / SR; if (modPh > 2 * Math.PI) modPh -= 2 * Math.PI; }
            if (lpOn)
            {
                double cut = lpTarget; if (modOn && modT == 1) cut *= Math.Pow(2, modDepth * 3 * Math.Sin(modPh));
                lpCut += (Math.Min(SR * 0.45, Math.Max(40, cut)) - lpCut) * 0.002; double g = Math.Tan(Math.PI * lpCut / SR), k = Math.Max(0.03, lpK);
                yl = Svf(yl, g, k, ref lpS1L, ref lpS2L, true); yr = Svf(yr, g, k, ref lpS1R, ref lpS2R, true);
            }
            if (hpOn) { yl = Svf(yl, hpG, 0.7, ref hpS1L, ref hpS2L, false); yr = Svf(yr, hpG, 0.7, ref hpS1R, ref hpS2R, false); }
            if (telOn)
            {
                yl = Svf(yl, telG1, 0.7, ref t1L, ref t2L, false); yr = Svf(yr, telG1, 0.7, ref t1R, ref t2R, false);
                yl = Svf(yl, telG2, 0.7, ref u1L, ref u2L, true); yr = Svf(yr, telG2, 0.7, ref u1R, ref u2R, true);
                yl = Math.Tanh(yl * telDrive) / Math.Sqrt(telDrive) * 1.6; yr = Math.Tanh(yr * telDrive) / Math.Sqrt(telDrive) * 1.6;
            }
            if (drvOn)
            {
                if (drvMode == 0) { yl = Math.Tanh(yl * drvG) / Math.Sqrt(drvG); yr = Math.Tanh(yr * drvG) / Math.Sqrt(drvG); }
                else if (drvMode == 1) { double c = 1 / drvG * 0.9 + 0.1; yl = Math.Max(-c, Math.Min(c, yl)) / c * 0.7; yr = Math.Max(-c, Math.Min(c, yr)) / c * 0.7; }
                else { double bits = 16 - drv * 13, q = Math.Pow(2, bits - 1), hold = 1 + drv * 23; crushHold += 1; if (crushHold >= hold) { crushHold -= hold; crushL = Math.Round(yl * q) / q; crushR = Math.Round(yr * q) / q; } yl = crushL; yr = crushR; }
            }
            if (gateOn)
            {
                double gp = (pos % gateLen) / gateLen; double tgt = gp < gateDuty ? 1 : 1 - gateDepth; gGate += (tgt - gGate) * (1 - Math.Exp(-1.0 / (0.002 * SR)));
                yl *= gGate; yr *= gGate;
            }
            if (modOn && modT == 2) { double p = 0.5 + 0.5 * modDepth * Math.Sin(modPh); double gl = Math.Cos(p * Math.PI / 2) * 1.414, gr = Math.Sin(p * Math.PI / 2) * 1.414; yl *= gl; yr *= gr; }
            if (widOn) { double m = (yl + yr) * 0.5, s = (yl - yr) * 0.5 * width; yl = m + s; yr = m - s; }
            if (dlyOn)
            {
                int dl = dlL.Length, rd = (int)(dlW - dlyLen); if (rd < 0) rd += dl; double dL = dlL[rd], dR = dlR[rd];
                dlL[dlW] = (float)((yl + yr) * 0.5 + dR * dlyFb); dlR[dlW] = (float)(dL * dlyFb); dlW = (dlW + 1) % dl;   // ping-pong: each echo crosses to the other side
                yl += dL * dlyMix; yr += dR * dlyMix;
            }
            if (revOn)
            {
                double inv = (yl + yr) * 0.25, ol = 0, or = 0;
                for (int c = 0; c < 8; c++)
                {
                    var buf = cb[c]; int len = Math.Min(buf.Length - 1, (int)(CombT[c % 4] * srK) + (c >= 4 ? (int)(23 * srK) : 0)); int k2 = ci[c]; if (k2 >= len) k2 = 0;
                    double o = buf[k2]; cf[c] = (float)(o * (1 - revDamp) + cf[c] * revDamp); buf[k2] = (float)(inv + cf[c] * revFb); ci[c] = k2 + 1; if (c < 4) ol += o; else or += o;
                }
                for (int c = 0; c < 4; c++)
                {
                    var buf = ab[c]; int len = Math.Min(buf.Length - 1, (int)(ApT[c % 2] * srK) + (c >= 2 ? (int)(23 * srK) : 0)); int k2 = ai[c]; if (k2 >= len) k2 = 0;
                    double x = c < 2 ? ol : or, bo = buf[k2], y = -x + bo; buf[k2] = (float)(x + bo * 0.5); ai[c] = k2 + 1; if (c < 2) ol = y; else or = y;
                }
                yl += ol * revMix; yr += or * revMix;
            }
            // ---- dry / wet and output ----
            double ol2 = (xl * (1 - mixS) + yl * mixS) * outG, or2 = (xr * (1 - mixS) + yr * mixS) * outG;
            if (double.IsNaN(ol2) || double.IsInfinity(ol2) || double.IsNaN(or2) || double.IsInfinity(or2)) { Reset(); ol2 = xl; or2 = xr; }
            l[i] = (float)ol2; r[i] = (float)or2; outPk = (float)Math.Max(outPk, Math.Max(Math.Abs(ol2), Math.Abs(or2)));
            if (i == n - 1)
            {
                ViewActive = active; ViewFrozen = frozen; ViewCycle = cycle; ViewWinPh = (float)(offset / refLen); int cyc = (int)Math.Min(32, Math.Max(1, Math.Round(refLen / repLen))); ViewCycles = cyc;
                for (int s = 0; s < 32; s++) ViewSteps[s] = false;
                for (int s = 1; s < cyc; s++) { bool act = frozen || (armed && pat != 4); if (pat == 1) act = s % 2 == 1; else if (pat == 7) act = s % 3 != 2; else if (pat == 8) act = s < cyc / 2.0; else if (pat == 9) { int st2 = s % 8; act = st2 != 3 && st2 != 6; } ViewSteps[s] = act || frozen; }
            }
        }
        InPk = Math.Max(InPk * 0.85f, inPk); OutPk = Math.Max(OutPk * 0.85f, outPk);
        EndBlock(n);
    }
}
