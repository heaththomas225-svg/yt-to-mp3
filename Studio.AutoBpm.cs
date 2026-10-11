using System;
using System.Threading;

// Automatic tempo: every track that gets audio is listened to in the background (no button to press). Each track shows its own BPM in its header,
// and the first beat or song also sets the project tempo, as long as you have not set one yourself (typed it, pressed DETECT, or opened a project).
partial class StudioForm
{
    bool bpmUserSet, bpmAutoSet; volatile bool bpmBusy; int bpmTick;

    // called on every paint; it does nothing until a track with audio has not been listened to yet, and only one track is analysed at a time
    void AutoBpmTick()
    {
        if (bpmBusy || unchecked(Environment.TickCount - bpmTick) < 600) return; bpmTick = Environment.TickCount;
        Track pick = null; foreach (var t in eng.Tracks.ToArray()) if (t.Clip != null && !t.Instrument && !t.IsBus && !t.BpmChecked && t.Clip.Length >= StudioEngine.SR * 4) { pick = t; break; }
        if (pick == null) return;
        pick.BpmChecked = true; var clip = pick.Clip; Track same = null; foreach (var t in eng.Tracks.ToArray()) if (t != pick && t.BpmChecked && t.Bpm > 0 && ReferenceEquals(t.Clip, clip)) { same = t; break; }   // pieces of one recording share its tempo
        if (same != null) { pick.Bpm = same.Bpm; return; }
        bpmBusy = true; long st = pick.ClipStart + StudioEngine.SR * 8; var who = pick;
        new Thread(() =>
        {
            double b = 0; try { b = Tempo.Detect(clip, st, 70); } catch { }
            try { BeginInvoke(new Action(() => { bpmBusy = false; AutoBpmResult(who, b); })); } catch { bpmBusy = false; }
        }) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "auto-bpm" }.Start();
    }

    void AutoBpmResult(Track t, double b)
    {
        if (b < 40 || b > 220) { t.Bpm = 0; return; }   // no steady beat heard (a plain voice, an ambience...)
        t.Bpm = Math.Round(b * 10) / 10;
        if (!bpmUserSet && !bpmAutoSet && t.Role != "vocal" && eng.Tracks.Contains(t))
        {
            bpmAutoSet = true; Fx.ProjectBpm = t.Bpm;
            Toast("Tempo found automatically: " + t.Bpm.ToString("0.#") + " BPM (from " + t.Name + "). The grid, metronome and Count In follow it. Type another number in the BPM box any time.");
        }
        Invalidate();
    }
}
