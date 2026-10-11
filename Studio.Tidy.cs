using System;
using System.Collections.Generic;

// Tidy up: a track that once held audio, has been emptied (its clip moved to another track or deleted), has nothing layered on it
// and still has plugins switched on, is removed by itself, so no useless processing rows are left behind. Ctrl+Z brings it back.
// Tracks you made empty on purpose (an armed audio track waiting for a recording, an instrument, a bus) and tracks that never held audio
// (for example a project whose audio file is missing) are never touched.
partial class StudioForm
{
    int tidyTick;

    void TidyEmptyTracks()
    {
        if (unchecked(Environment.TickCount - tidyTick) < 500) return; tidyTick = Environment.TickCount;
        var all = eng.Tracks.ToArray();
        foreach (var t in all) if (t.Clip != null && !t.Instrument && !t.IsBus) t.EverHadClip = true;
        if (busy || dragReg != null || ghostOn || mic != null || micArmed || flexOpen || marqueeOn || undoHold > 0) return;
        Track dead = null;
        foreach (var t in all)
        {
            if (!t.EverHadClip || t.Clip != null || t.Instrument || t.IsBus || t.Armed || t.Host != null) continue;
            bool hasLayer = false, pluginsOn = false; foreach (var x in all) if (x.Host == t && (x.Clip != null || x.Instrument)) { hasLayer = true; break; }
            foreach (var f in t.Fx) if (!f.Bypass) { pluginsOn = true; break; }
            if (hasLayer || !pluginsOn) continue; dead = t; break;
        }
        if (dead == null) return;
        string name = dead.Name; int n = dead.Fx.Count; DeleteTrackQuiet(dead);
        if (selTrack >= LaneCount) selTrack = Math.Max(0, LaneCount - 1);
        Toast("Removed the empty track " + name + ": nothing was on it any more except " + n + (n == 1 ? " plugin" : " plugins") + ". Ctrl+Z brings it back.");
    }
}
