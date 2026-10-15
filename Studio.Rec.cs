using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

// Purphex Music: record a microphone onto a new track, open the piano roll, start a new project.
partial class StudioForm
{
    AudioRecorder mic; long micLead; long micStart; int micCount; DateTime micT0; bool micArmed;

    // MIC arms the microphone (nothing is recorded yet); PLAY then starts the recording, after the Count In if it is on. Press MIC again to disarm, or while recording to finish.
    void MicClick()
    {
        if (mic != null) { StopMic(); return; }
        if (micArmed) { micArmed = false; Toast("Microphone is no longer armed."); return; }
        if (AudioRecorder.Devices().Count == 0) { StartMic(); return; }   // shows the "no microphone" message
        if (!AskYes("Would you like to record now?\n\nPress PLAY (or the space bar) to start recording. If not, press No.", "Record with the microphone")) return;
        micArmed = true; var row = RecordTarget();
        Toast("The microphone is ready" + (row != null ? " for " + row.Name : "") + " (the MIC button pulses red). Nothing is recorded yet: press PLAY (or the space bar) when you are ready" + (eng.CountIn ? ". Count In gives you one bar first." : ". Switch on COUNT IN if you want a bar of clicks first."));
    }
    // PLAY / PAUSE and the space bar: with the microphone armed, PLAY is what starts the recording
    void PlayToggle()
    {
        if (micArmed && mic == null) { StartMic(); return; }
        if (eng.Playing) PauseHere(); else PlayGuard();
    }
    // PAUSE (the buttons, the space bar): the song stops exactly where the needle is drawn, so the needle does not hop and PLAY carries on from that very spot (the view never moves)
    internal void PauseHere()
    {
        if (!eng.Playing) return; long at = DispPos; eng.Pause();
        if (at >= 0 && Math.Abs(eng.Pos - at) < StudioEngine.SR / 4) { lock (eng.Sync) eng.Pos = at; }   // (what you saw and what you heard are a few ms apart: what you saw wins)
        dispPos = eng.Pos;
    }
    // the track a new recording goes onto: the selected audio track (its own row), never a new track made behind your back
    Track RecordTarget() { var s = Selected; if (s == null || s.IsBus || s.Instrument) return null; var h = RowHost(s); return h != null && !h.IsBus && !h.Instrument && !h.Locked ? h : null; }

    internal Func<string, DialogResult> ask3Hook;   // tests answer the three-way question themselves
    // the track you are about to record onto cannot be heard (muted, switched off, or another track is soloed): you are asked first.
    // returns false to cancel; true to go on (unmuted / switched on / solo taken off when you chose that)
    internal bool CheckRecordTargetAudible()
    {
        var t = RecordTarget(); if (t == null) return true; var row = RowHost(t) ?? t; bool soloElse = !row.Solo && eng.Tracks.Exists(x => x != row && x.Solo);
        string why = row.Off ? "is switched OFF (its power button)" : row.Mute ? "is MUTED" : soloElse ? "cannot be heard: another track is soloed" : ""; if (why.Length == 0) return true;
        string q = "\"" + row.Name + "\" " + why + ", so you will not hear what you record onto it.\n\nYes: " + (row.Off ? "switch it on" : row.Mute ? "unmute it" : "take the solo off") + " and record\nNo: record anyway\nCancel: do not record";
        var a = ask3Hook != null ? ask3Hook(q) : MessageBox.Show(this, q, "This track is " + (row.Off ? "off" : row.Mute ? "muted" : "not heard"), MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1);
        if (a == DialogResult.Cancel) return false;
        if (a == DialogResult.Yes) { PushUndo(); if (row.Off) row.Off = false; else if (row.Mute) row.Mute = false; else foreach (var x in eng.Tracks) x.Solo = false; }
        return true;
    }
    bool micSilentWarned;
    void StartMic()
    {
        micArmed = false; micSilentWarned = false; if (!CheckRecordTargetAudible()) return;
        var devs = AudioRecorder.Devices();
        if (devs.Count == 0)
        {
            MessageBox.Show(this, "No microphone or audio input was found.\n\nPlug one in (or switch it on in Windows: Settings > System > Sound > Input), then press MIC again.", "Record", MessageBoxButtons.OK, MessageBoxIcon.Information); return;
        }
        int idx = Prefs.InDev.Length > 0 ? devs.IndexOf(Prefs.InDev) : -1; var r = new AudioRecorder(); r.SetMode((MicMode)Prefs.MicMode);
        if (!r.Start(idx)) { MessageBox.Show(this, r.Error, "Record", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        mic = r; micT0 = DateTime.Now; micStart = eng.Playing ? (long)dispPos : eng.Pos; micTarget = RecordTarget();
        micLead = 0; if (!eng.Playing) { PlayGuard(); micLead = eng.LastCountFrames; }   // with Count In the recording starts one bar before the song
        Toast("Recording" + (micTarget != null ? " onto " + micTarget.Name : "") + ". Press MIC again (or STOP) when you are done. Use headphones so the beat is not recorded too.");
    }

    // the microphone sends pure silence: what to check (a headset is named, so the hint fits it)
    string SilentMicText()
    {
        string dev = Prefs.InDev.Length > 0 ? Prefs.InDev : "your microphone"; bool headset = dev.IndexOf("G3", StringComparison.OrdinalIgnoreCase) >= 0 || dev.IndexOf("LIGHTSPEED", StringComparison.OrdinalIgnoreCase) >= 0 || dev.IndexOf("headset", StringComparison.OrdinalIgnoreCase) >= 0 || dev.IndexOf("Hands-Free", StringComparison.OrdinalIgnoreCase) >= 0;
        return "The microphone (" + dev + ") is sending no sound at all, not even background hiss, so it is muted or switched off before the sound reaches Purphex Music.\n\n" +
            (headset ? "On a headset: flip the mic boom down (flipped up mutes it on many headsets) or press its mic mute button, and check it is switched on and charged.\n" : "Check the mic is switched on and not muted.\n") +
            "In Windows: Settings > System > Sound > Input: the right microphone, and its level up.\nOr pick another input in Purphex Music: Settings > Recording input.";
    }
    Track micTarget;   // the track chosen when recording started
    void StopMic()
    {
        micArmed = false; var r = mic; mic = null; if (r == null) return; bool silent = r.AllSilent; int frames; float[] s = r.Stop(out frames); if (eng.Playing) eng.Pause();
        if (silent) { MessageBox.Show(this, SilentMicText() + "\n\nNothing was added (the take was silence).", "The microphone sent no sound", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        if (frames < 4410) { Toast("That recording was too short, so nothing was added."); return; }
        try { var t = AddRecording(s, frames, micStart - micLead); Toast("Added " + t.Name + " (" + (frames / (double)StudioEngine.SR).ToString("0.0") + " s). It is saved in Music / yt2mp3 / Recordings."); }
        catch (Exception ex) { MessageBox.Show(this, "Couldn't save the recording: " + ex.Message, "Record"); }
    }

    // Saves the take as a WAV file (so projects can reopen it) and adds it as a vocal track, shifted earlier to cancel the sound-card delay.
    internal Track AddRecording(float[] mono, int frames, long startPos)
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "yt2mp3", "Recordings"); Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "Recording " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") + ".wav");
        using (var w = new WavStream(path, StudioEngine.SR, 24)) w.Write(mono, mono, 0, frames);
        var l = new float[frames]; Array.Copy(mono, l, frames); var rr = (float[])l.Clone(); var clip = AudioClip.FromArrays(l, rr, "Recording"); clip.Path = path;
                Track t = null, host = micTarget != null && eng.Tracks.Contains(micTarget) ? micTarget : RecordTarget(); micTarget = null; bool existing = false, layer = false;
        if (host != null && host.Clip == null) t = host;                    // an empty audio track: the take goes straight onto it
        else if (host != null) layer = true;                                // a track that already has audio: the take is another piece on the SAME track
        else foreach (var x in eng.Tracks.ToArray()) if (x.Clip == null && !x.IsBus && !x.Instrument && x.Host == null) { t = x; break; }
        if (t != null) { existing = true; PushUndo(); lock (eng.Sync) { t.Clip = clip; t.Source = path; t.Armed = false; t.ClipStart = 0; t.ClipFrames = -1; } }
        else if (layer) { t = StudioEngine.NewTrack("Recording " + (++micCount), host.Role, clip); t.Source = path; t.Host = host; t.Out = host.Out; }
        else { t = StudioEngine.NewTrack("Recording " + (++micCount), "vocal", clip); t.Source = path; }
        long comp = WaveOutPlayer.QueuedFrames + AudioRecorder.InputBufferFrames;   // what you heard was already this late, and the input adds some too
        if (startPos >= comp) t.Offset = startPos - comp; else { t.Offset = 0; t.ClipStart = Math.Min(frames - 1, comp - startPos); }
        if (layer) { PushUndo(); lock (eng.Sync) eng.Tracks.Insert(FirstBusIndex(), t); selTrack = Math.Max(0, eng.Tracks.IndexOf(host)); toolsTrack = t; }
        else if (!existing) AddTrack(t); return t;
    }

    void OpenRoll(Track t)
    {
        if (t == null || !t.Instrument) { Toast("Select an instrument track first (add one with + ADD TRACK, then MIDI Track)."); return; }
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
        bpmUserSet = false; bpmAutoSet = false; ProjectIO.Lyrics = ""; ProjectIO.LyricsTitle = ""; ProjectIO.CoverBytes = null; undo.Clear(); redo.Clear(); projPath = ""; projName = "Untitled project"; selTrack = 0; selFx = -1; scroll = 0; FitView(); Toast("New project. Add music, or press + INSTRUMENT to make something.");
    }
}
