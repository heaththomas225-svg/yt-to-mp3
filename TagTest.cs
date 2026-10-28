using System;
using System.IO;
using System.Linq;

// The producer tag generator: the voices on this PC speak, every character and effect makes a clean, levelled tag, and a vocal preset can go on it.
static class TagTest
{
    static int fails;
    static void Check(string n, bool ok, string d = "") { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + "   " + d); Console.Out.Flush(); }
    static string Stat(float[][] o, out bool good)
    {
        good = false; if (o == null) return "none"; double pk = 0, rms = 0; bool bad = false; foreach (var ch in o) foreach (var v in ch) { if (float.IsNaN(v) || float.IsInfinity(v)) bad = true; pk = Math.Max(pk, Math.Abs(v)); rms += v * v; }
        rms = Math.Sqrt(rms / Math.Max(1, o[0].Length * 2)); good = !bad && pk > 0.85 && pk < 0.92 && rms > 0.02 && o[0].Length > 44100 / 2 && o[0].Length < 44100 * 12;
        return (o[0].Length / 44100.0).ToString("0.00") + "s peak " + pk.ToString("0.00") + " rms " + rms.ToString("0.000") + (bad ? " NaN" : "");
    }
    // EXPORT puts the tag where you choose (WAV, or MP3 when ffmpeg is there); IMPORT TAG puts a tag file on the selected track at the needle
    static void StudioPart(float[] dry)
    {
        const System.Reflection.BindingFlags BF = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public;
        var app = (App)Activator.CreateInstance(typeof(App), true); var eng = app.StudioEng; var sf = new StudioForm(app); sf.ClientSize = new System.Drawing.Size(1500, 900); sf.Show(); System.Windows.Forms.Application.DoEvents();
        var o = TagMaker.Process(dry, new TagSettings()); string tmp = Path.Combine(StudioEnv.DataDir, "tagtmp.wav"); SoundIO.WriteWav(tmp, o[0], o[1], 44100, 24);
        string outW = Path.Combine(StudioEnv.DataDir, "out", "My tag.wav"); string e1 = sf.TagExportTo(tmp, outW); Check("EXPORT saves the tag as a WAV where you choose", e1 == null && File.Exists(outW) && new FileInfo(outW).Length > 10000, e1 ?? "");
        if (StudioEnv.Ffmpeg() != null) { string outM = Path.Combine(StudioEnv.DataDir, "out", "My tag.mp3"); string e2 = sf.TagExportTo(tmp, outM); Check("...or as an MP3", e2 == null && File.Exists(outM) && new FileInfo(outM).Length > 2000, e2 ?? ""); }
        else Console.WriteLine("(no ffmpeg here: the MP3 export check is skipped)");
        Check("exporting before there is a tag says to press PREVIEW", sf.TagExportTo(Path.Combine(StudioEnv.DataDir, "none.wav"), outW) != null);
        var x = new float[44100 * 8]; var beat = StudioEngine.NewTrack("Beat", "beat", AudioClip.FromArrays(x, (float[])x.Clone(), "beat")); eng.Tracks.Add(beat); typeof(StudioForm).GetField("selTrack", BF).SetValue(sf, 0);
        int n0 = eng.Tracks.Count; string e3 = sf.TagImportFrom(outW, 44100 * 2); var added = eng.Tracks.Skip(n0).ToList();
        Check("IMPORT TAG puts the tag on the selected track at the needle", e3 == null && added.Count == 1 && added[0].Host == beat && Math.Abs(added[0].Offset - 44100 * 2) < 2, e3 ?? added.Count + " added");
        var midi = StudioEngine.NewTrack("Keys", "inst", null); midi.Instrument = true; eng.Tracks.Insert(0, midi); typeof(StudioForm).GetField("selTrack", BF).SetValue(sf, 0); var before = eng.Tracks.ToList(); sf.TagImportFrom(outW, 0); added = eng.Tracks.Where(q => !before.Contains(q)).ToList();
        Check("on a note track (no audio there) it goes on a track of its own", added.Count == 1 && added[0].Host == null && added[0].Clip != null, added.Count + " added; " + string.Join(",", eng.Tracks.Select(q => q.Name + (q.Host != null ? "@" + q.Host.Name : ""))));
        sf.Close();
    }
    [STAThread] static void Main()
    {
        StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_tagtest_" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(StudioEnv.DataDir);
        var voices = TagMaker.Voices(); Console.WriteLine("voices on this PC: " + string.Join(", ", voices));
        float[] dry = null;
        if (voices.Count == 0) Console.WriteLine("(no speech voices on this PC: the voice checks are skipped)");
        else
        {
            var sw = System.Diagnostics.Stopwatch.StartNew(); string err; dry = TagMaker.Speak("Produced by Purphex", 0, 0, out err);
            Check("a voice on this PC speaks the tag (no key, nothing online)", dry != null && dry.Length > 44100 / 2 && dry.Max(v => Math.Abs(v)) > 0.05, dry == null ? err : (dry.Length / 44100.0).ToString("0.00") + "s in " + sw.ElapsedMilliseconds + " ms");
            for (int i = 1; i < voices.Count; i++) { var d2 = TagMaker.Speak("Produced by Purphex", i, 0, out err); Check("voice " + voices[i] + " speaks too", d2 != null && d2.Length > 44100 / 2, d2 == null ? err : (d2.Length / 44100.0).ToString("0.00") + "s"); }
            if (voices.Count > 0) { var slow = TagMaker.Speak("Produced by Purphex", 0, -4, out err); var fast = TagMaker.Speak("Produced by Purphex", 0, 4, out err); Check("SPEED changes how fast it speaks", slow != null && fast != null && slow.Length > fast.Length * 1.3, slow == null || fast == null ? err : slow.Length + " vs " + fast.Length); }
            string e3; var none = TagMaker.Speak("   ", 0, 0, out e3); Check("an empty tag says to type one", none == null && e3 == "Type your tag first.", e3 ?? "");
        }
        if (dry == null) { var syn = new float[44100]; for (int i = 0; i < syn.Length; i++) syn[i] = (float)(0.4 * Math.Sin(i * 0.03) * Math.Sin(i * 0.0007)); dry = syn; }   // a stand-in voice so the sound checks still run
        for (int c = 0; c < TagMaker.Characters.Length; c++) { bool ok; var o = TagMaker.Process(dry, new TagSettings { Character = c }); string st = Stat(o, out ok); Check("character " + TagMaker.Characters[c].Name + ": a clean tag, levelled to a safe peak", ok, st); }
        for (int f = 0; f < TagMaker.Effects.Length; f++) { var s = new TagSettings(); s.Fx[f] = true; bool ok; var o = TagMaker.Process(dry, s); string st = Stat(o, out ok); Check("effect " + TagMaker.Effects[f][0] + ": a clean tag", ok, st); }
        { var plain = TagMaker.Process(dry, new TagSettings()); var s = new TagSettings(); s.Fx[2] = true; var stut = TagMaker.Process(dry, s); s = new TagSettings(); s.Fx[1] = true; var echo = TagMaker.Process(dry, s);
          Check("STUTTER and ECHO make the tag longer (the repeats and the tail)", stut[0].Length > plain[0].Length + 44100 / 4 && echo[0].Length > plain[0].Length, plain[0].Length + " / " + stut[0].Length + " / " + echo[0].Length); }
        { var s = new TagSettings { Character = 3, Pitch = -2 }; for (int f = 0; f < s.Fx.Length; f++) s.Fx[f] = true; s.VocalPreset = VocalStyles.All[0].Name; bool ok; var o = TagMaker.Process(dry, s); string st = Stat(o, out ok); Check("everything at once with a vocal preset (" + s.VocalPreset + ")", ok, st); }
        { var x = new float[44100 * 3]; for (int i = 44100; i < 44100 * 2; i++) x[i] = (float)(0.5 * Math.Sin(i * 0.05)); var t = TagMaker.Trim(x); Check("your own recording: the silence before and after is cut off", t.Length > 44100 && t.Length < 44100 * 1.25, (t.Length / 44100.0).ToString("0.00") + "s"); }
        try { StudioPart(dry); } catch (Exception ex) { Check("export and import run without errors", false, ex.ToString()); }
        Console.WriteLine(fails == 0 ? "=== producer tag checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
}
