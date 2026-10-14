using System;
using System.Collections.Generic;
using System.Threading;

// AUTO KEY: a pitch correction plugin (Pitch Correction, Melody Tune, a preset or style that has one) finds the key of its track by itself when it is added,
// so it tunes to the right notes straight away. The key of each piece of audio is worked out once (in the background) and kept.
partial class StudioForm
{
    readonly Dictionary<AudioClip, int[]> keyCache = new Dictionary<AudioClip, int[]>();   // { found (0/1), key, scale }
    volatile bool autoKeyBusy; int autoKeyAt;
    // the audio a track's key is taken from: its own clip, else the longest piece on its row
    AudioClip KeySource(Track tr)
    {
        if (tr.Clip != null) return tr.Clip; AudioClip best = null; long bl = 0;
        foreach (var p in eng.Tracks.ToArray()) if (p.Host == tr && p.Clip != null && p.UsedLength > bl) { bl = p.UsedLength; best = p.Clip; }
        return best;
    }
    internal void AutoKeyTick()
    {
        if (autoKeyBusy || Environment.TickCount - autoKeyAt < 400) return; autoKeyAt = Environment.TickCount;
        foreach (var tr in eng.Tracks.ToArray())
        {
            Fx[] fxs; lock (eng.Sync) fxs = tr.Fx.ToArray();
            foreach (var f in fxs)
            {
                if (!f.AutoKeyPending) continue; var clip = KeySource(tr); if (clip == null) continue;   // no audio yet: it waits until there is some
                int[] res; if (keyCache.TryGetValue(clip, out res)) { ApplyAutoKey(f, tr, res); continue; }
                autoKeyBusy = true; var c = clip; var ff = f; var tk = tr;
                ThreadPool.QueueUserWorkItem(o =>
                {
                    int key = 0, scale = 1; double conf; bool ok; try { ok = KeyDetect.Detect(c, out key, out scale, out conf); } catch { ok = false; }
                    var r = new[] { ok ? 1 : 0, key, scale };
                    try { BeginInvoke(new Action(() => { keyCache[c] = r; autoKeyBusy = false; ApplyAutoKey(ff, tk, r); lastInput = DateTime.Now; Invalidate(); })); } catch { autoKeyBusy = false; }
                });
                return;   // one detection at a time
            }
        }
    }
    // every plugin with a key setting (Pitch Correction, Melody Tune and the tuners made from them) has an AUTO DETECT button
    internal static bool HasKeyParam(Fx f) { var p = f == null ? null : f.Find("key"); return p != null && p.Choices != null && p.Choices.Length == 12; }
    // AUTO DETECT: listen to the track (its clip, else the longest piece on its row) and set the key and scale of this plugin
    internal void AutoDetectKey(Fx f, Track tr)
    {
        f.AutoKeyPending = false; var clip = tr == null ? null : KeySource(tr);
        if (clip == null) { Toast("AUTO DETECT needs audio on this track first (sung or rapped notes)."); return; }
        int[] got; if (keyCache.TryGetValue(clip, out got)) { SetKeyFrom(f, tr, got); return; }
        int key = 0, scale = 1; double conf; bool found = false; var c = clip;
        RunBusy("Detecting the key of " + tr.Name + "...", false, p => { found = KeyDetect.Detect(c, out key, out scale, out conf); }, ok =>
        {
            if (!ok) return; var r = new[] { found ? 1 : 0, key, scale }; keyCache[c] = r; SetKeyFrom(f, tr, r);
        });
    }
    void SetKeyFrom(Fx f, Track tr, int[] r)
    {
        if (r[0] == 0) { Toast("No clear key in " + tr.Name + " (it needs sung or rapped notes). Pick the key on " + f.Title + " by hand."); return; }
        lock (eng.Sync) { f.Set("key", r[1]); if (f.Find("scale") != null) f.Set("scale", r[2]); var pc = f as PitchCorrector; if (pc != null) pc.ApplyScale(); }
        Toast("AUTO DETECT: " + f.Title + " is set to " + PitchCorrector.Keys[r[1]] + (r[2] == 2 ? " minor" : " major") + " (found in " + tr.Name + ").");
    }
    void ApplyAutoKey(Fx f, Track tr, int[] r)
    {
        if (!f.AutoKeyPending) return; f.AutoKeyPending = false;   // the key was picked by hand meanwhile: leave it
        if (r[0] == 0) { Toast("AUTO KEY: no clear key in " + tr.Name + " (it needs sung or rapped notes). Pick the key on " + f.Title + " by hand."); return; }
        f.Set("key", r[1]); f.Set("scale", r[2]); var pc = f as PitchCorrector; if (pc != null) pc.ApplyScale();
        Toast("AUTO KEY: " + f.Title + " on " + tr.Name + " is set to " + PitchCorrector.Keys[r[1]] + (r[2] == 2 ? " minor" : " major") + " (found in the audio).");
    }
}
