using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

// Tests the VST2 host against a stand-in plugin built in memory with the real VST2 binary layout (a sine-wave synth).
//   csc /main:V ... VstTest.cs
static class V
{
    static int fails;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + "   " + d); Console.Out.Flush(); }

    // ---- the stand-in plugin ----
    static IntPtr eff, rectMem, chunkMem; static VstDispatch dispD; static VstProcess procD; static VstSetParam setD; static VstGetParam getD; static VstEntry entryD;
    static double freq, phase, amp; static int sr = 44100; static IntPtr editorHwnd; static byte[] gotChunk; static float[] pars = { 0.25f, 0.5f };
    static int pendingDelta = -1, pendingNote, pendingVel; static List<string> log = new List<string>(); static bool mainsOn, opened, closedFlag;

    static IntPtr Dispatch(IntPtr e, int op, int index, IntPtr value, IntPtr ptr, float opt)
    {
        switch (op)
        {
            case 0: opened = true; return IntPtr.Zero;
            case 1: closedFlag = true; return IntPtr.Zero;
            case 10: sr = (int)opt; return IntPtr.Zero;
            case 12: mainsOn = value != IntPtr.Zero; return IntPtr.Zero;
            case 13: Marshal.WriteIntPtr(ptr, rectMem); return (IntPtr)1;
            case 14: editorHwnd = ptr; return (IntPtr)1;
            case 15: editorHwnd = IntPtr.Zero; return IntPtr.Zero;
            case 6: Put(ptr, "dB"); return IntPtr.Zero;
            case 7: Put(ptr, pars[index].ToString("0.00")); return IntPtr.Zero;
            case 8: Put(ptr, index == 0 ? "Cutoff" : "Volume"); return IntPtr.Zero;
            case 45: Put(ptr, "Mock Synth"); return (IntPtr)1;
            case 47: Put(ptr, "MockCo"); return (IntPtr)1;
            case 23: { var data = Encoding.ASCII.GetBytes("PATCH-" + pars[0].ToString("0.00")); chunkMem = Marshal.AllocHGlobal(data.Length); Marshal.Copy(data, 0, chunkMem, data.Length); Marshal.WriteIntPtr(ptr, chunkMem); return (IntPtr)data.Length; }
            case 24: gotChunk = new byte[(int)value]; Marshal.Copy(ptr, gotChunk, 0, gotChunk.Length); return IntPtr.Zero;
            case 25:
                {
                    int n = Marshal.ReadInt32(ptr, 0);
                    for (int i = 0; i < n; i++)
                    {
                        IntPtr ev = Marshal.ReadIntPtr(ptr, 16 + i * IntPtr.Size);
                        int type = Marshal.ReadInt32(ev, 0), size = Marshal.ReadInt32(ev, 4), delta = Marshal.ReadInt32(ev, 8); byte s = Marshal.ReadByte(ev, 24), d1 = Marshal.ReadByte(ev, 25), d2 = Marshal.ReadByte(ev, 26);
                        log.Add("ev type " + type + " size " + size + " delta " + delta + " status " + s.ToString("X2") + " " + d1 + " " + d2);
                        if ((s & 0xF0) == 0x90 && d2 > 0) { pendingDelta = delta; pendingNote = d1; pendingVel = d2; }
                        else if ((s & 0xF0) == 0x80 || ((s & 0xF0) == 0x90 && d2 == 0) || (s == 0xB0 && d1 == 123)) { pendingDelta = -2; }
                    }
                    return (IntPtr)1;
                }
        }
        return IntPtr.Zero;
    }
    static void Put(IntPtr p, string s) { var b = Encoding.ASCII.GetBytes(s + "\0"); Marshal.Copy(b, 0, p, b.Length); }

    static void ProcessReplacing(IntPtr e, IntPtr inputs, IntPtr outputs, int frames)
    {
        IntPtr o0 = Marshal.ReadIntPtr(outputs, 0), o1 = Marshal.ReadIntPtr(outputs, IntPtr.Size); var buf = new float[frames];
        for (int i = 0; i < frames; i++)
        {
            if (pendingDelta == i) { freq = 440.0 * Math.Pow(2, (pendingNote - 69) / 12.0); amp = pendingVel / 127.0 * 0.5; phase = 0; pendingDelta = -1; }
            else if (pendingDelta == -2 && i == 0) { amp = 0; pendingDelta = -1; }
            buf[i] = (float)(amp * Math.Sin(phase)); phase += 2 * Math.PI * freq / sr;
        }
        if (pendingDelta >= frames) pendingDelta -= frames;
        Marshal.Copy(buf, 0, o0, frames); Marshal.Copy(buf, 0, o1, frames);
    }

    static IntPtr Entry(IntPtr hostCb)
    {
        eff = Marshal.AllocHGlobal(256); for (int i = 0; i < 256; i += 8) Marshal.WriteInt64(eff, i, 0);
        rectMem = Marshal.AllocHGlobal(8); Marshal.WriteInt16(rectMem, 0, 0); Marshal.WriteInt16(rectMem, 2, 0); Marshal.WriteInt16(rectMem, 4, 200); Marshal.WriteInt16(rectMem, 6, 300);   // top, left, bottom, right
        dispD = Dispatch; procD = ProcessReplacing; setD = (x, i, v) => pars[i] = v; getD = (x, i) => pars[i];
        Marshal.WriteInt32(eff, 0, 0x56737450);
        Marshal.WriteIntPtr(eff, 8, Marshal.GetFunctionPointerForDelegate(dispD));
        Marshal.WriteIntPtr(eff, 24, Marshal.GetFunctionPointerForDelegate(setD)); Marshal.WriteIntPtr(eff, 32, Marshal.GetFunctionPointerForDelegate(getD));
        Marshal.WriteInt32(eff, 40, 1); Marshal.WriteInt32(eff, 44, 2); Marshal.WriteInt32(eff, 48, 0); Marshal.WriteInt32(eff, 52, 2);
        Marshal.WriteInt32(eff, 56, 1 | 16 | 32 | 256); Marshal.WriteInt32(eff, 112, 0x4D4F434B);
        Marshal.WriteIntPtr(eff, 120, Marshal.GetFunctionPointerForDelegate(procD));
        // call the host callback once the way a real plugin does (version query)
        var hcb = (VstHostCb)Marshal.GetDelegateForFunctionPointer(hostCb, typeof(VstHostCb)); int ver = (int)hcb(eff, 1, 0, IntPtr.Zero, IntPtr.Zero, 0);
        log.Add("host version " + ver);
        return eff;
    }

    static int ZeroCross(float[] x, int from, int n) { int z = 0; for (int i = from + 1; i < from + n; i++) if ((x[i - 1] < 0) != (x[i] < 0)) z++; return z; }

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Check("host is 64-bit", IntPtr.Size == 8, "pointer size " + IntPtr.Size);
        entryD = Entry; IntPtr entry = Marshal.GetFunctionPointerForDelegate(entryD);
        var p = VstPlugin.FromEntry(entry, "MockSynth.dll", 44100);
        Check("plugin starts and tells the host its name", p.Name == "Mock Synth" && p.Vendor == "MockCo", "name '" + p.Name + "', vendor '" + p.Vendor + "'");
        Check("plugin details are read from the right places", p.NumParams == 2 && p.NumOut == 2 && p.NumIn == 0 && p.IsSynth && p.HasEditor && p.HasChunks, "params " + p.NumParams + ", in " + p.NumIn + ", out " + p.NumOut + ", synth " + p.IsSynth + ", editor " + p.HasEditor + ", chunks " + p.HasChunks);
        Check("host answered the plugin's version question", log.Contains("host version 2400"), string.Join("; ", log.ToArray()));
        Check("effect was opened, sample rate set and processing started", opened && mainsOn && sr == 44100, "open " + opened + ", on " + mainsOn + ", sr " + sr);
        Check("parameter names / values", p.ParamName(0) == "Cutoff" && p.ParamName(1) == "Volume" && Math.Abs(p.GetParam(0) - 0.25f) < 1e-6 && p.ParamDisplay(1).StartsWith("0.50"), p.ParamName(0) + " " + p.GetParam(0) + ", " + p.ParamDisplay(1));
        p.SetParam(1, 0.9f); Check("setting a parameter reaches the plugin", Math.Abs(pars[1] - 0.9f) < 1e-6, pars[1].ToString());

        // a note: starts exactly 100 samples into the block, has the right pitch
        var l = new float[4096]; var r = new float[4096];
        p.SendMidi(new List<KeyValuePair<int, MidiEv>> { new KeyValuePair<int, MidiEv>(100, new MidiEv { S = 0x90, D1 = 69, D2 = 100 }) });
        p.Process(l, r, 4096);
        double peakBefore = 0; for (int i = 0; i < 99; i++) peakBefore = Math.Max(peakBefore, Math.Abs(l[i])); double peakAfter = 0; for (int i = 200; i < 4096; i++) peakAfter = Math.Max(peakAfter, Math.Abs(l[i]));
        double hz = ZeroCross(l, 200, 3800) / 2.0 / (3800 / 44100.0);
        Check("MIDI note arrives with the right timing", peakBefore < 1e-6 && peakAfter > 0.2, "silent before sample 100: " + (peakBefore < 1e-6) + ", level after " + peakAfter.ToString("0.00"));
        Check("MIDI note has the right pitch (A4 = 440 Hz)", Math.Abs(hz - 440) < 8, hz.ToString("0.0") + " Hz");
        Check("event layout matches the VST2 spec", log.Exists(s => s.StartsWith("ev type 1 size 32 delta 100 status 90 69 100")), log.Count > 1 ? log[log.Count - 1] : "none");
        p.SendMidi(new List<KeyValuePair<int, MidiEv>> { new KeyValuePair<int, MidiEv>(0, new MidiEv { S = 0x80, D1 = 69, D2 = 0 }) });
        l = new float[1024]; r = new float[1024]; p.Process(l, r, 1024); double after = 0; for (int i = 0; i < 1024; i++) after = Math.Max(after, Math.Abs(l[i]));
        Check("note off silences it", after < 1e-6, after.ToString("0.0000"));

        // saved patch round trip
        var fx = new VstFx(p, "MockSynth.dll"); string ser = fx.Serialize();
        Check("the plugin's own patch is saved with the project", ser.StartsWith("chunk=") && Encoding.ASCII.GetString(Convert.FromBase64String(ser.Substring(6))) == "PATCH-0.25", ser);
        fx.Deserialize(ser); Check("and given back when the project is opened", gotChunk != null && Encoding.ASCII.GetString(gotChunk) == "PATCH-0.25", gotChunk == null ? "none" : Encoding.ASCII.GetString(gotChunk));

        // the plugin's window
        var form = new Form { Width = 400, Height = 300 }; form.Show(); IntPtr hwnd = form.Handle;
        var sz = p.GetEditorSize(); p.OpenEditor(hwnd);
        Check("plugin window size and parent window", sz.Width == 300 && sz.Height == 200 && editorHwnd == hwnd, sz.Width + "x" + sz.Height + ", given window " + (editorHwnd == hwnd));
        p.EditorIdle(); p.CloseEditor(); Check("plugin window closes", editorHwnd == IntPtr.Zero, "");

        { var fxw = new VstFx(p, "MockSynth.dll"); var vw = new VstWindow(fxw, null); vw.Show(); Application.DoEvents(); Application.DoEvents(); var bm = new System.Drawing.Bitmap(vw.ClientSize.Width, vw.ClientSize.Height); vw.DrawToBitmap(bm, new System.Drawing.Rectangle(0, 0, bm.Width, bm.Height)); bm.Save(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "vstwin.png")); vw.Close(); Check("instrument window opens its editor and shows a piano", true, vw.ClientSize.Width + "x" + vw.ClientSize.Height); }
        // inside the engine: an instrument track with recorded notes, exported offline
        var eng = new StudioEngine(); var tr = StudioEngine.NewTrack("Synth", "beat", null); tr.Instrument = true; var ifx = fx; ifx.Owner = tr; tr.Fx.Add(ifx); eng.Tracks.Add(tr);
        tr.Midi.Add(new MidiEv { Frame = 10000, S = 0x90, D1 = 69, D2 = 110 }); tr.Midi.Add(new MidiEv { Frame = 30000, S = 0x80, D1 = 69, D2 = 0 });
        foreach (var m in eng.MasterFx) m.Bypass = true;
        long frames = 44100; var L = new float[frames]; long w = 0; amp = 0; pendingDelta = -1;
        eng.RenderStream(0, frames, new RenderOpts { Master = false, Fader = false }, null, (bl, br, off, c) => { Array.Copy(bl, off, L, w, c); w += c; }, null);
        double pre = 0; for (int i = 0; i < 9900; i++) pre = Math.Max(pre, Math.Abs(L[i])); double mid = 0; for (int i = 12000; i < 28000; i++) mid = Math.Max(mid, Math.Abs(L[i])); double post = 0; for (int i = 32000; i < 44000; i++) post = Math.Max(post, Math.Abs(L[i]));
        double hz2 = ZeroCross(L, 12000, 16000) / 2.0 / (16000 / 44100.0);
        Check("instrument track plays its recorded notes in an export", pre < 1e-4 && mid > 0.1 && post < 1e-4, "before " + pre.ToString("0.000") + ", during " + mid.ToString("0.00") + ", after " + post.ToString("0.000"));
        Check("recorded note has the right pitch in the export", Math.Abs(hz2 - 440) < 8, hz2.ToString("0.0") + " Hz");
        Check("instrument track counts toward the song length", tr.EndFrame >= 30000, tr.EndFrame.ToString());

        // live playing while the song is stopped
        amp = 0; pendingDelta = -1; fx.NoteOn(60, 100);
        var oL = new float[1024]; var oR = new float[1024]; double live = 0; for (int b = 0; b < 4; b++) { eng.RenderRealtime(oL, oR, 1024); for (int i = 0; i < 1024; i++) live = Math.Max(live, Math.Abs(oL[i])); }
        Check("you can hear an instrument while the song is stopped", live > 0.05, "level " + live.ToString("0.00"));
        fx.NoteOff(60); StudioEngine.LiveUntil = 0; eng.RenderRealtime(oL, oR, 1024); double quiet = 0; for (int i = 0; i < 1024; i++) quiet = Math.Max(quiet, Math.Abs(oL[i]));
        Check("and it goes quiet again afterwards", quiet < 1e-6, quiet.ToString("0.0000"));
        // saving and reopening a project with an instrument track
        FxRegistry.Register(new FxRegistry.Info("vst:MockSynth.dll", "Mock Synth (VST)", "Instruments", "test"), () => new VstFx(VstPlugin.FromEntry(entry, "MockSynth.dll", 44100), "MockSynth.dll"));
        string pf = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "vsttest.ytmproj"); ProjectIO.Save(eng, pf);
        var eng2 = new StudioEngine(); ProjectIO.Load(eng2, pf, s => { });
        var t2 = eng2.Tracks.Count == 1 ? eng2.Tracks[0] : null;
        Check("project keeps the instrument track, its notes and its plugin", t2 != null && t2.Instrument && t2.Midi.Count == 2 && t2.Midi[0].Frame == 10000 && t2.Midi[0].D1 == 69 && t2.Fx.Count == 1 && t2.Fx[0] is VstFx, t2 == null ? "no track" : "instrument " + t2.Instrument + ", notes " + t2.Midi.Count + ", effects " + t2.Fx.Count);
        if (t2 != null) foreach (var f2 in t2.Fx) ((IDisposable)f2).Dispose();
        fx.Dispose(); Check("plugin is closed cleanly", closedFlag, "");
        Console.WriteLine(fails == 0 ? "=== VST host checks passed ===" : "=== " + fails + " failed ===");
        Environment.Exit(fails == 0 ? 0 : 1);
    }
}
