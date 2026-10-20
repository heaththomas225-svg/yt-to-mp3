using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

// The live sample buffer a plugin fills for its visualiser (shared: the engine writes it, a window reads it)
class PxScope
{
    public readonly float[] Buf; public volatile int W; public PxScope(int n) { Buf = new float[n]; }
    public void Push(float v) { int w = W; Buf[w] = v; W = (w + 1) % Buf.Length; }
    // the spectrum in dB of the newest n samples (UI thread)
    public double[] Spectrum(int n)
    {
        var re = new double[n]; var im = new double[n]; int w = W, len = Buf.Length;
        for (int i = 0; i < n; i++) { double win = 0.5 - 0.5 * Math.Cos(2 * Math.PI * i / n); re[i] = Buf[(w - n + i + len * 4) % len] * win; }
        Fft.Transform(re, im, false); var db = new double[n / 2]; for (int k = 0; k < n / 2; k++) db[k] = 10 * Math.Log10((re[k] * re[k] + im[k] * im[k]) / (n * n / 16.0) + 1e-12);
        return db;
    }
}
