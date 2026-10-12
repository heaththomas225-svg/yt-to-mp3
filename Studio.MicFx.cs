using System;

// The microphone modes of the MIC button. They only touch what is being RECORDED (the sound that goes onto the new track); nothing else in the studio changes.
//   Automatic        a gentle clean-up that suits most rooms: rumble out, quiet background noise eased down between words, loud peaks kept from clipping
//   Standard         the microphone exactly as it is, nothing added
//   Voice Isolation  the voice stands alone: rumble and hiss out, the room noise taken down hard (spectral noise reduction and a gate that opens before each word)
//   Wide Spectrum    the full range and the whole room: no gate, no noise reduction, only the DC offset removed and a little air on top
enum MicMode { Automatic = 0, Standard = 1, VoiceIsolation = 2, WideSpectrum = 3 }

sealed class MicProcessor
{
    // ----- the building blocks -----
    sealed class Biq
    {
        double b0, b1, b2, a1, a2, z1, z2;
        void Set(double nb0, double nb1, double nb2, double na0, double na1, double na2) { b0 = nb0 / na0; b1 = nb1 / na0; b2 = nb2 / na0; a1 = na1 / na0; a2 = na2 / na0; }
        public static Biq HighPass(double fc, double q, int sr) { var f = new Biq(); double w = 2 * Math.PI * fc / sr, c = Math.Cos(w), a = Math.Sin(w) / (2 * q); f.Set((1 + c) / 2, -(1 + c), (1 + c) / 2, 1 + a, -2 * c, 1 - a); return f; }
        public static Biq LowPass(double fc, double q, int sr) { var f = new Biq(); double w = 2 * Math.PI * fc / sr, c = Math.Cos(w), a = Math.Sin(w) / (2 * q); f.Set((1 - c) / 2, 1 - c, (1 - c) / 2, 1 + a, -2 * c, 1 - a); return f; }
        public static Biq HighShelf(double fc, double gainDb, int sr)
        {
            var f = new Biq(); double A = Math.Pow(10, gainDb / 40), w = 2 * Math.PI * fc / sr, c = Math.Cos(w), s = Math.Sin(w), al = s / 2 * Math.Sqrt(2), sq = 2 * Math.Sqrt(A) * al;
            f.Set(A * ((A + 1) + (A - 1) * c + sq), -2 * A * ((A - 1) + (A + 1) * c), A * ((A + 1) + (A - 1) * c - sq), (A + 1) - (A - 1) * c + sq, 2 * ((A - 1) - (A + 1) * c), (A + 1) - (A - 1) * c - sq); return f;
        }
        public float Run(float x) { double y = b0 * x + z1; z1 = b1 * x - a1 * y + z2; z2 = b2 * x - a2 * y; return (float)y; }
    }
    // turns the sound down when it is no more than the background: the quiet level is learned (it follows the room, going down fast and up very slowly)
    sealed class Expander
    {
        double env, nf = 1e-3, g = 1, ratio, floor, thrMul, envA, envR, atk, rel, nfUp, nfCap;
        public Expander(int sr, double ratio, double maxCutDb, double thrMul)
        {
            this.ratio = ratio; floor = Math.Pow(10, -maxCutDb / 20); this.thrMul = thrMul; envA = 1 - Math.Exp(-1.0 / (0.002 * sr)); envR = 1 - Math.Exp(-1.0 / (0.060 * sr));
            atk = 1 - Math.Exp(-1.0 / (0.003 * sr)); rel = 1 - Math.Exp(-1.0 / (0.140 * sr)); nfUp = 1 - Math.Exp(-1.0 / (20.0 * sr)); nfCap = 0.012;   // the learned background can never be louder than -38 dB
        }
        public double Gain(float x)
        {
            double a = Math.Abs(x); env += (a - env) * (a > env ? envA : envR);
            if (env < nf) nf += (env - nf) * 0.002; else nf = Math.Min(nfCap, nf + (env - nf) * nfUp); if (nf < 2e-5) nf = 2e-5;
            double thr = nf * thrMul, target = env >= thr ? 1.0 : Math.Max(floor, Math.Pow(env / thr, ratio - 1));
            g += (target - g) * (target > g ? atk : rel); return g;
        }
    }
    // never above 1.0: a smooth curve takes over from -1 dB, so a loud peak is rounded off instead of clipping
    static float Soft(float x) { const float T = 0.89f; float a = Math.Abs(x); if (a <= T) return x; float y = T + (1f - T) * (float)Math.Tanh((a - T) / (1f - T)); return x < 0 ? -y : y; }

    // ----- one processor per recording -----
    readonly MicMode mode; readonly int sr; Biq hp1, hp2, lp, shelf; Expander ex; NoiseReduction nr; double dcX, dcY; float[] tl = new float[0], tr = new float[0], gain = new float[0];
    public int LatencyFrames { get { return nr != null ? nr.Latency : 0; } }   // the noise reduction hears a little ahead: the recording is shifted back so it still lines up
    public MicProcessor(MicMode mode, int sr = 44100)
    {
        this.mode = mode; this.sr = sr;
        switch (mode)
        {
            case MicMode.Automatic: hp1 = Biq.HighPass(80, 0.707, sr); ex = new Expander(sr, 2.5, 10, 2.8); break;
            case MicMode.VoiceIsolation:
                hp1 = Biq.HighPass(100, 0.707, sr); hp2 = Biq.HighPass(100, 0.707, sr); lp = Biq.LowPass(9000, 0.707, sr); ex = new Expander(sr, 4.0, 24, 3.0);
                nr = new NoiseReduction(); nr.Prepare(sr); nr.Set("amount", 18f); nr.Set("sens", 2.2f); nr.Set("mix", 100f); nr.Reset(); break;
            case MicMode.WideSpectrum: shelf = Biq.HighShelf(8000, 2.0, sr); break;
        }
    }

    // processes n samples in place (mono)
    public void Process(float[] x, int n)
    {
        switch (mode)
        {
            case MicMode.Standard: return;
            case MicMode.WideSpectrum:
                for (int i = 0; i < n; i++) { double v = x[i]; double y = v - dcX + 0.9993 * dcY; dcX = v; dcY = y; x[i] = Soft(shelf.Run((float)y)); }   // the DC blocker (about 5 Hz), the air, the safety curve
                return;
            case MicMode.Automatic:
                for (int i = 0; i < n; i++) { float v = hp1.Run(x[i]); x[i] = Soft(v * (float)ex.Gain(v)); }
                return;
            case MicMode.VoiceIsolation:
                if (tl.Length < n) { tl = new float[n]; tr = new float[n]; gain = new float[n]; }
                for (int i = 0; i < n; i++) { float v = lp.Run(hp2.Run(hp1.Run(x[i]))); tl[i] = v; tr[i] = v; gain[i] = (float)ex.Gain(v); }
                nr.Process(tl, tr, n);   // the noise reduction speaks 1024 samples late: the gate was decided on the sound that is just arriving, so it opens before the word comes out
                for (int i = 0; i < n; i++) x[i] = Soft(tl[i] * gain[i]);
                return;
        }
    }
    // the last LatencyFrames of sound are still inside: silence is pushed through to bring them out
    public float[] Finish()
    {
        int k = LatencyFrames; var t = new float[k]; if (k > 0) Process(t, k); return t;
    }

    public static string Name(MicMode m) { switch (m) { case MicMode.Automatic: return "Automatic"; case MicMode.Standard: return "Standard"; case MicMode.VoiceIsolation: return "Voice Isolation"; default: return "Wide Spectrum"; } }
    public static string Blurb(MicMode m)
    {
        switch (m)
        {
            case MicMode.Automatic: return "Rumble out and quiet room noise eased down between words. Best for most rooms.";
            case MicMode.Standard: return "The microphone exactly as it is. Nothing is added.";
            case MicMode.VoiceIsolation: return "Only your voice: hiss and room noise taken down hard.";
            default: return "The full range and the whole room, with a little air on top. No gate, no noise reduction.";
        }
    }
}
