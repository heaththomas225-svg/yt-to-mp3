using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

// FAST AUTO: AUTO MASTERING, AUTO LOUDNESS, SET LOUDNESS and AUTO EQ listen to the song in a few seconds instead of minutes.
//  - the tracks are rendered once, as slices spread over the whole song (a short song is rendered whole), and kept until a track changes;
//  - the loudness passes run the master chain alone over that audio (on copies of the master plugins), not every track again;
//  - the ten AUTO MASTERING versions are worked out side by side, one per processor core.
partial class StudioForm
{
    const int SliceSec = 10, Slices = 6, PreRollSec = 2;
    float[] smpL, smpR; string smpKey = "";

    // what decides the sound before the master: every track's audio, place, level and plugins (the master chain is not in it)
    string PremixKey()
    {
        var sb = new StringBuilder(); sb.Append(eng.EndFrame).Append('|');
        lock (eng.Sync) foreach (var t in eng.Tracks)
        {
            sb.Append(t.Clip == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(t.Clip)).Append(',').Append(t.Offset).Append(',').Append(t.ClipStart).Append(',').Append(t.ClipFrames).Append(',').Append(t.VolDb).Append(',').Append(t.Pan).Append(',')
              .Append(t.Mute).Append(t.Solo).Append(t.Off).Append(t.Mono).Append(',').Append(t.ClipGainDb).Append(',').Append(t.FadeIn).Append(',').Append(t.FadeOut).Append(',').Append(t.FadeInDelay).Append(',').Append(t.FadeOutTail).Append(',')
              .Append(eng.Tracks.IndexOf(t.Host)).Append(',').Append(eng.Tracks.IndexOf(t.Out)).Append(',').Append(t.Midi.Count).Append(':');
            foreach (var f in t.Fx) sb.Append(f.Type).Append(f.Bypass ? "!" : "=").Append(f.Serialize()).Append(';');
            sb.Append('\n');
        }
        return sb.ToString();
    }
    // the mix before the master: slices spread over the song (or the whole song when it is short), kept until something on a track changes
    internal void SampleMix(out float[] l, out float[] r)
    {
        string key = PremixKey(); if (smpL != null && key == smpKey) { l = smpL; r = smpR; return; }
        int sr = StudioEngine.SR; long end = Math.Min(eng.EndFrame, (long)sr * 600); var L = new List<float>(); var R = new List<float>();
        if (end <= (long)sr * SliceSec * Slices + sr * 10) { float[] a, b; eng.Render(0, Math.Max(sr, end), new RenderOpts { Master = false }, null, out a, out b); L.AddRange(a); R.AddRange(b); }
        else
        {
            for (int i = 0; i < Slices; i++)
            {
                long mid = (long)(end * (i + 0.5) / Slices), s = Math.Max(0, mid - (long)sr * SliceSec / 2), pre = Math.Min(s, (long)sr * PreRollSec);
                float[] a, b; eng.Render(s - pre, pre + (long)sr * SliceSec, new RenderOpts { Master = false }, null, out a, out b);   // a little before each slice, so reverbs and compressors are already running
                for (long k = pre; k < a.Length; k++) { L.Add(a[k]); R.Add(b[k]); }
            }
        }
        smpL = L.ToArray(); smpR = R.ToArray(); smpKey = key; l = smpL; r = smpR;
    }
    // the master chain alone over a piece of audio, on fresh copies of the master plugins (so it can run beside the live sound and beside other copies)
    internal List<Fx> MasterCopies(string[] snap)
    {
        var list = new List<Fx>(); Fx[] live; lock (eng.Sync) live = eng.MasterFx.ToArray();
        for (int i = 0; i < live.Length && i < snap.Length; i++) { var f = FxRegistry.Create(live[i].Type); if (f == null) continue; f.Prepare(StudioEngine.SR); f.ResetDefaults(); f.Bypass = snap[i].Length > 0 && snap[i][0] == '1'; f.Deserialize(snap[i].Substring(Math.Min(1, snap[i].Length))); f.Reset(); list.Add(f); }
        return list;
    }
    internal static LoudnessResult RunMaster(List<Fx> chain, float[] inL, float[] inR, float volDb)
    {
        foreach (var f in chain) { try { f.PrepareOffline(); } catch { } f.Reset(); }
        int n = inL.Length; var oL = new float[n]; var oR = new float[n]; float mg = DspMath.DbToLin(volDb); var bl = new float[4096]; var br = new float[4096];
        for (int p = 0; p < n; p += 4096)
        {
            int c = Math.Min(4096, n - p); Array.Copy(inL, p, bl, 0, c); Array.Copy(inR, p, br, 0, c); if (c < 4096) { Array.Clear(bl, c, 4096 - c); Array.Clear(br, c, 4096 - c); }
            foreach (var f in chain) { if (f.Bypass) continue; f.ScL = null; f.ScR = null; try { f.Process(bl, br, c); } catch { } }
            for (int i = 0; i < c; i++) { oL[p + i] = bl[i] * mg; oR[p + i] = br[i] * mg; }
        }
        return Loudness.Measure(oL, oR, StudioEngine.SR, 0, n);
    }
    static string[] SnapOf(List<Fx> chain) { var s = new string[chain.Count]; for (int i = 0; i < chain.Count; i++) s[i] = (chain[i].Bypass ? "1" : "0") + chain[i].Serialize(); return s; }
    // moves the limiter of a copied chain until the audio lands on the target (2 or 3 passes of the master chain alone)
    internal static LoudnessResult FitCopy(List<Fx> chain, float target, float[] l, float[] r, float volDb)
    {
        Fx lim = chain.Find(f => f.Type == "limiter"); LoudnessResult m = RunMaster(chain, l, r, volDb); if (lim == null) return m;
        for (int it = 0; it < 4; it++)
        {
            if (double.IsInfinity(m.I) || Math.Abs(target - m.I) < 0.3) break;
            float g0 = lim.Get("gain"), g1 = (float)DspMath.Clamp(g0 + (target - m.I), 0, 24); if (Math.Abs(g1 - g0) < 0.01f) break; lim.Set("gain", g1); m = RunMaster(chain, l, r, volDb);
        }
        return m;
    }
    // the ten AUTO MASTERING versions at once: each one is fitted to its loudness on its own processor core
    internal LoudnessResult[] FitVersions(List<string[]> snaps, float[] targets, float[] l, float[] r, out string[][] fitted)
    {
        var res = new LoudnessResult[snaps.Count]; var outS = new string[snaps.Count][]; float vol = eng.MasterVolDb;
        var copies = new List<Fx>[snaps.Count]; for (int i = 0; i < snaps.Count; i++) copies[i] = MasterCopies(snaps[i]);   // made here: they read the live chain for the plugin types
        Parallel.For(0, snaps.Count, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) }, i => { res[i] = FitCopy(copies[i], targets[i], l, r, vol); outS[i] = SnapOf(copies[i]); });
        fitted = outS; return res;
    }
}
