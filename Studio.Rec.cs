using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

// Purphex Music: record a microphone onto a new track, open the piano roll, start a new project.
partial class StudioForm
{
    AudioRecorder mic; long micStart; int micCount; DateTime micT0;

    void MicClick() { if (mic != null) StopMic(); else StartMic(); }

    void StartMic()
    {
        var devs = AudioRecorder.Devices();
        if (devs.Count == 0)
        {
            MessageBox.Show(this, "No microphone or audio input was found.\n\nPlug one in (or switch it on in Windows: Settings > System > Sound > Input), then press MIC again.", "Record", MessageBoxButtons.OK, MessageBoxIcon.Information); return;
        }
        int idx = Prefs.InDev.Length > 0 ? devs.IndexOf(Prefs.InDev) : -1; var r = new AudioRecorder();
        if (!r.Start(idx)) { MessageBox.Show(this, r.Error, "Record", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        mic = r; micT0 = DateTime.Now; micStart = eng.Playing ? (long)dispPos : eng.Pos;
        if (!eng.Playing) PlayGuard();
        Toast("Recording from the microphone. Press MIC again (or STOP) when you are done. Use headphones so the beat is not recorded too.");
    }

    void StopMic()
    {
        var r = mic; mic = null; if (r == null) return; int frames; float[] s = r.Stop(out frames); if (eng.Playing) eng.Pause();
        if (frames < 4410) { Toast("That recording was too short, so nothing was added."); return; }
        try { var t = AddRecording(s, frames, micStart); Toast("Added " + t.Name + " (" + (frames / (double)StudioEngine.SR).ToString("0.0") + " s). It is saved in Music / yt2mp3 / Recordings."); }
        catch (Exception ex) { MessageBox.Show(this, "Couldn't save the recording: " + ex.Message, "Record"); }
    }

    // Saves the take as a WAV file (so projects can reopen it) and adds it as a vocal track, shifted earlier to cancel the sound-card delay.
    internal Track AddRecording(float[] mono, int frames, long startPos)
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "yt2mp3", "Recordings"); Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "Recording " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") + ".wav");
        using (var w = new WavStream(path, StudioEngine.SR, 24)) w.Write(mono, mono, 0, frames);
        var l = new float[frames]; Array.Copy(mono, l, frames); var rr = (float[])l.Clone(); var clip = AudioClip.FromArrays(l, rr, "Recording"); clip.Path = path;
                Track t = null, sel = Selected; bool existing = false;
        if (sel != null && sel.Armed && sel.Clip == null && !sel.IsBus && !sel.Instrument) t = sel; else foreach (var x in eng.Tracks.ToArray()) if (x.Armed && x.Clip == null && !x.IsBus && !x.Instrument) { t = x; break; }
        if (t != null) { existing = true; lock (eng.Sync) { t.Clip = clip; t.Source = path; t.Armed = false; t.ClipStart = 0; t.ClipFrames = -1; } }
        else { t = StudioEngine.NewTrack("Recording " + (++micCount), "vocal", clip); t.Source = path; }
        long comp = WaveOutPlayer.QueuedFrames + AudioRecorder.InputBufferFrames;   // what you heard was already this late, and the input adds some too
        if (startPos >= comp) t.Offset = startPos - comp; else { t.Offset = 0; t.ClipStart = Math.Min(frames - 1, comp - startPos); }
        if (!existing) AddTrack(t); return t;
    }

    void OpenRoll(Track t)
    {
        if (t == null || !t.Instrument) { Toast("Select an instrument track first (add one with + INSTRUMENT)."); return; }
        InstrumentFx ins = null; foreach (var f in t.Fx) { var i = f as InstrumentFx; if (i != null && i.IsInstrument) { ins = i; break; } }
        if (ins == null) { Toast("This track has no instrument."); return; }
        ins.Owner = t; var w = new PianoRollForm(t, ins, eng, () => { if (eng.Playing) eng.Pause(); else PlayGuard(); }); w.Show(this);
    }

    void NewProject()
    {
        if (eng.Tracks.Count > 0 && MessageBox.Show(this, "Start a new empty project? Anything not saved will be lost.", "New project", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        if (mic != null) StopMic(); eng.Stop();
        lock (eng.Sync) { foreach (var t in eng.Tracks) retired.AddRange(t.Fx); eng.Tracks.Clear(); }
        foreach (var f in retired) { var ic = f as InstrumentFx; if (ic != null) ic.CloseWindow(); }
        ProjectIO.Lyrics = ""; ProjectIO.LyricsTitle = ""; ProjectIO.CoverBytes = null; undo.Clear(); redo.Clear(); projPath = ""; projName = "Untitled project"; selTrack = 0; selFx = -1; scroll = 0; FitView(); Toast("New project. Add music, or press + INSTRUMENT to make something.");
    }
}
