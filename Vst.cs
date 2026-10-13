using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

// =====================================================================================================
//  VST 2 plugin host (64-bit): effects and instruments.
//  Written against the public VST 2.4 binary interface. A plugin is a native .dll that exports
//  VSTPluginMain (or main); the host hands it a callback and then talks to it through one "dispatcher".
//  VST 3 (.vst3) and 32-bit plugins are not supported.
// =====================================================================================================
static class VstNative
{
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)] public static extern IntPtr LoadLibrary(string path);
    [DllImport("kernel32", CharSet = CharSet.Ansi)] public static extern IntPtr GetProcAddress(IntPtr h, string name);
    [DllImport("kernel32")] public static extern bool FreeLibrary(IntPtr h);
    [DllImport("kernel32", CharSet = CharSet.Unicode)] public static extern bool SetDllDirectory(string dir);
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr VstEntry(IntPtr hostCallback);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr VstDispatch(IntPtr effect, int opcode, int index, IntPtr value, IntPtr ptr, float opt);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void VstProcess(IntPtr effect, IntPtr inputs, IntPtr outputs, int frames);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate void VstSetParam(IntPtr effect, int index, float value);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate float VstGetParam(IntPtr effect, int index);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate IntPtr VstHostCb(IntPtr effect, int opcode, int index, IntPtr value, IntPtr ptr, float opt);

class MidiEv { public long Frame; public byte S, D1, D2; }

class VstPlugin : IVstHosted
{
    string IVstHosted.Name { get { return Name; } } string IVstHosted.Vendor { get { return Vendor; } } string IVstHosted.Path { get { return Path; } } int IVstHosted.NumParams { get { return NumParams; } } bool IVstHosted.EditorOpen { get { return EditorOpen; } }
    public static void RaiseSize(object p, int w, int h) { var e = SizeWindowRequested; if (e != null) e(p, w, h); }
    // AEffect (x64) field offsets
    const int OffMagic = 0, OffDispatcher = 8, OffSetParam = 24, OffGetParam = 32, OffNumPrograms = 40, OffNumParams = 44, OffNumIn = 48, OffNumOut = 52, OffFlags = 56, OffUniqueId = 112, OffProcessReplacing = 120;
    const int effOpen = 0, effClose = 1, effSetSampleRate = 10, effSetBlockSize = 11, effMainsChanged = 12, effEditGetRect = 13, effEditOpen = 14, effEditClose = 15, effEditIdle = 19,
              effGetChunk = 23, effSetChunk = 24, effProcessEvents = 25, effGetParamLabel = 6, effGetParamDisplay = 7, effGetParamName = 8, effGetEffectName = 45, effGetVendorString = 47, effStartProcess = 71, effStopProcess = 72;
    public const int MaxBlock = 8192;
    public const int FlagHasEditor = 1, FlagCanReplacing = 16, FlagProgramChunks = 32, FlagIsSynth = 256;

    IntPtr lib, eff; VstDispatch disp; VstProcess proc; VstSetParam setP; VstGetParam getP; bool disposed; volatile bool freed;   // freed: effClose has run and the DLL may be unloaded, so nothing may call into it again
    public string Name = "", Vendor = "", Path = ""; public int NumParams, NumIn, NumOut, Flags, UniqueId; public int SampleRate = 44100;
    public bool IsSynth { get { return (Flags & FlagIsSynth) != 0 || NumIn == 0; } }
    public int Latency { get { return disposed ? 0 : Math.Max(0, Math.Min(1 << 16, Marshal.ReadInt32(eff, 80))); } }   // AEffect.initialDelay, read live (audioMasterIOChanged can change it)
    public bool HasEditor { get { return (Flags & FlagHasEditor) != 0; } }
    public bool HasChunks { get { return (Flags & FlagProgramChunks) != 0; } }
    public bool EditorOpen;

    IntPtr[] inBuf, outBuf; IntPtr inArr, outArr, evBlock, evArr; const int MaxEvents = 512;
    float[] scrL, scrR;
    readonly object callLock = new object();
    static readonly VstHostCb hostCb = HostCallback;   // kept alive for the life of the process
    static IntPtr timeInfo = Marshal.AllocHGlobal(256);
    public static volatile int HostSampleRate = 44100; public static long HostPos; public static double HostBpm = 90; public static bool HostPlaying;
    public static event Action<object, int, int> SizeWindowRequested;
    static readonly Dictionary<IntPtr, VstPlugin> byEffect = new Dictionary<IntPtr, VstPlugin>();

    // ---- loading ----
    public static VstPlugin Load(string path, int sr)
    {
        if (IntPtr.Size != 8) throw new Exception("This app is running as 32-bit, so VST plugins can't be loaded.");
        string dir = System.IO.Path.GetDirectoryName(path); try { if (!string.IsNullOrEmpty(dir)) VstNative.SetDllDirectory(dir); } catch { }
        IntPtr h = VstNative.LoadLibrary(path);
        if (h == IntPtr.Zero) throw new Exception("Windows couldn't load " + System.IO.Path.GetFileName(path) + " (error " + Marshal.GetLastWin32Error() + "). It may be a 32-bit plugin, or it needs another program's files.");
        IntPtr entry = VstNative.GetProcAddress(h, "VSTPluginMain"); if (entry == IntPtr.Zero) entry = VstNative.GetProcAddress(h, "main");
        if (entry == IntPtr.Zero) { VstNative.FreeLibrary(h); throw new Exception(System.IO.Path.GetFileName(path) + " isn't a VST2 plugin."); }
        try { var p = FromEntry(entry, path, sr); p.lib = h; return p; }
        catch { VstNative.FreeLibrary(h); throw; }
    }

    internal static VstPlugin FromEntry(IntPtr entry, string label, int sr)
    {
        var fn = (VstEntry)Marshal.GetDelegateForFunctionPointer(entry, typeof(VstEntry));
        IntPtr e = fn(Marshal.GetFunctionPointerForDelegate(hostCb));
        if (e == IntPtr.Zero) throw new Exception("The plugin didn't start.");
        if (Marshal.ReadInt32(e, OffMagic) != 0x56737450) throw new Exception("That file isn't a VST2 plugin.");
        var p = new VstPlugin { eff = e, Path = label, SampleRate = sr };
        p.disp = (VstDispatch)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(e, OffDispatcher), typeof(VstDispatch));
        IntPtr sp = Marshal.ReadIntPtr(e, OffSetParam), gp = Marshal.ReadIntPtr(e, OffGetParam), pr = Marshal.ReadIntPtr(e, OffProcessReplacing);
        if (sp != IntPtr.Zero) p.setP = (VstSetParam)Marshal.GetDelegateForFunctionPointer(sp, typeof(VstSetParam));
        if (gp != IntPtr.Zero) p.getP = (VstGetParam)Marshal.GetDelegateForFunctionPointer(gp, typeof(VstGetParam));
        if (pr != IntPtr.Zero) p.proc = (VstProcess)Marshal.GetDelegateForFunctionPointer(pr, typeof(VstProcess));
        p.NumParams = Marshal.ReadInt32(e, OffNumParams); p.NumIn = Marshal.ReadInt32(e, OffNumIn); p.NumOut = Marshal.ReadInt32(e, OffNumOut);
        p.Flags = Marshal.ReadInt32(e, OffFlags); p.UniqueId = Marshal.ReadInt32(e, OffUniqueId);
        if (p.proc == null) throw new Exception("This plugin is too old (it can't process audio the modern way).");
        lock (byEffect) byEffect[e] = p;
        p.Call(effOpen, 0, IntPtr.Zero, IntPtr.Zero, 0);
        p.Call(effSetSampleRate, 0, IntPtr.Zero, IntPtr.Zero, sr);
        p.Call(effSetBlockSize, 0, (IntPtr)MaxBlock, IntPtr.Zero, 0);
        p.Name = p.GetString(effGetEffectName, 0, 256); if (p.Name.Length == 0) p.Name = System.IO.Path.GetFileNameWithoutExtension(label);
        p.Vendor = p.GetString(effGetVendorString, 0, 256);
        int nin = Math.Max(2, p.NumIn), nout = Math.Max(2, p.NumOut);
        p.inBuf = new IntPtr[nin]; p.outBuf = new IntPtr[nout];
        for (int i = 0; i < nin; i++) { p.inBuf[i] = Marshal.AllocHGlobal(MaxBlock * 4); ZeroMem(p.inBuf[i], MaxBlock * 4); }
        for (int i = 0; i < nout; i++) { p.outBuf[i] = Marshal.AllocHGlobal(MaxBlock * 4); ZeroMem(p.outBuf[i], MaxBlock * 4); }
        p.inArr = Marshal.AllocHGlobal(nin * IntPtr.Size); p.outArr = Marshal.AllocHGlobal(nout * IntPtr.Size);
        for (int i = 0; i < nin; i++) Marshal.WriteIntPtr(p.inArr, i * IntPtr.Size, p.inBuf[i]);
        for (int i = 0; i < nout; i++) Marshal.WriteIntPtr(p.outArr, i * IntPtr.Size, p.outBuf[i]);
        p.evBlock = Marshal.AllocHGlobal(MaxEvents * 32); p.evArr = Marshal.AllocHGlobal(16 + MaxEvents * IntPtr.Size);
        p.Call(effMainsChanged, 0, (IntPtr)1, IntPtr.Zero, 0);
        p.Call(effStartProcess, 0, IntPtr.Zero, IntPtr.Zero, 0);
        return p;
    }

    static void ZeroMem(IntPtr p, int bytes) { var z = new byte[Math.Min(bytes, 4096)]; for (int o = 0; o < bytes; o += z.Length) Marshal.Copy(z, 0, p + o, Math.Min(z.Length, bytes - o)); }

    IntPtr Call(int op, int index, IntPtr value, IntPtr ptr, float opt) { if (freed) return IntPtr.Zero; lock (callLock) { return disp(eff, op, index, value, ptr, opt); } }

    string GetString(int op, int index, int size)
    {
        IntPtr b = Marshal.AllocHGlobal(size); try { ZeroMem(b, size); Call(op, index, IntPtr.Zero, b, 0); string s = Marshal.PtrToStringAnsi(b); return (s ?? "").Trim(); } catch { return ""; } finally { Marshal.FreeHGlobal(b); }
    }

    // ---- parameters ----
    public string ParamName(int i) { return GetString(effGetParamName, i, 256); }
    public string ParamDisplay(int i) { string d = GetString(effGetParamDisplay, i, 256), u = GetString(effGetParamLabel, i, 256); return u.Length > 0 ? d + " " + u : d; }
    public float GetParam(int i) { if (freed) return 0; try { return getP == null ? 0 : getP(eff, i); } catch { return 0; } }
    public void SetParam(int i, float v) { if (freed) return; try { if (setP != null) setP(eff, i, v); } catch { } }

    // ---- saved state (the whole patch) ----
    public byte[] GetChunk()
    {
        if (!HasChunks || freed) return null;
        IntPtr pp = Marshal.AllocHGlobal(IntPtr.Size);
        try { Marshal.WriteIntPtr(pp, IntPtr.Zero); int size = (int)Call(effGetChunk, 0, IntPtr.Zero, pp, 0); IntPtr d = Marshal.ReadIntPtr(pp); if (size <= 0 || d == IntPtr.Zero) return null; var b = new byte[size]; Marshal.Copy(d, b, 0, size); return b; }
        catch { return null; }
        finally { Marshal.FreeHGlobal(pp); }
    }
    public void SetChunk(byte[] data)
    {
        if (data == null || data.Length == 0 || !HasChunks) return;
        IntPtr b = Marshal.AllocHGlobal(data.Length); try { Marshal.Copy(data, 0, b, data.Length); Call(effSetChunk, 0, (IntPtr)data.Length, b, 0); } catch { } finally { Marshal.FreeHGlobal(b); }
    }

    // ---- audio ----
    // MIDI: events are (deltaFrames, status, data1, data2). Must be called before Process for the same block.
    public void SendMidi(List<KeyValuePair<int, MidiEv>> ev)
    {
        if (ev == null || ev.Count == 0 || disposed) return;
        int n = Math.Min(ev.Count, MaxEvents);
        for (int i = 0; i < n; i++)
        {
            IntPtr e = evBlock + i * 32; var m = ev[i].Value;
            Marshal.WriteInt32(e, 0, 1);                 // type: MIDI
            Marshal.WriteInt32(e, 4, 32);                // byteSize
            Marshal.WriteInt32(e, 8, Math.Max(0, ev[i].Key));   // deltaFrames
            Marshal.WriteInt32(e, 12, 0); Marshal.WriteInt32(e, 16, 0); Marshal.WriteInt32(e, 20, 0);   // flags, noteLength, noteOffset
            Marshal.WriteByte(e, 24, m.S); Marshal.WriteByte(e, 25, m.D1); Marshal.WriteByte(e, 26, m.D2); Marshal.WriteByte(e, 27, 0);   // midiData[4]
            Marshal.WriteByte(e, 28, 0); Marshal.WriteByte(e, 29, 0); Marshal.WriteByte(e, 30, 0); Marshal.WriteByte(e, 31, 0);
            Marshal.WriteIntPtr(evArr, 16 + i * IntPtr.Size, e);
        }
        Marshal.WriteInt32(evArr, 0, n); Marshal.WriteInt32(evArr, 4, 0); Marshal.WriteIntPtr(evArr, 8, IntPtr.Zero);
        Call(effProcessEvents, 0, IntPtr.Zero, evArr, 0);
    }

    // Processes n frames. Effects: output replaces l / r. Instruments: output is added to l / r.
    public void Process(float[] l, float[] r, int n)
    {
        if (disposed) return;
        for (int off = 0; off < n; off += MaxBlock)
        {
            int c = Math.Min(MaxBlock, n - off);
            if (NumIn >= 1) Marshal.Copy(l, off, inBuf[0], c);
            if (NumIn >= 2) Marshal.Copy(r, off, inBuf[1], c); else if (NumIn == 1) { }
            for (int o = 0; o < outBuf.Length; o++) ZeroSome(outBuf[o], c);
            UpdateTime(off);
            proc(eff, inArr, outArr, c);
            bool add = IsSynth; if (scrL == null) { scrL = new float[MaxBlock]; scrR = new float[MaxBlock]; }
            var tl = scrL; var tr = scrL; Marshal.Copy(outBuf[0], tl, 0, c);
            if (NumOut >= 2) { tr = scrR; Marshal.Copy(outBuf[1], tr, 0, c); }
            if (add) for (int i = 0; i < c; i++) { l[off + i] += tl[i]; r[off + i] += tr[i]; }
            else for (int i = 0; i < c; i++) { l[off + i] = tl[i]; r[off + i] = tr[i]; }
        }
    }

    static void ZeroSome(IntPtr p, int frames) { int bytes = frames * 4; var z = new byte[Math.Min(bytes, 4096)]; for (int o = 0; o < bytes; o += z.Length) Marshal.Copy(z, 0, p + o, Math.Min(z.Length, bytes - o)); }

    void UpdateTime(int off)
    {
        double sr = SampleRate, sp = HostPos + off, tempo = HostBpm;
        Marshal.WriteInt64(timeInfo, 0, BitConverter.DoubleToInt64Bits(sp));
        Marshal.WriteInt64(timeInfo, 8, BitConverter.DoubleToInt64Bits(sr));
        Marshal.WriteInt64(timeInfo, 16, BitConverter.DoubleToInt64Bits(0.0));
        Marshal.WriteInt64(timeInfo, 24, BitConverter.DoubleToInt64Bits(sp / sr * tempo / 60.0));
        Marshal.WriteInt64(timeInfo, 32, BitConverter.DoubleToInt64Bits(tempo));
        Marshal.WriteInt64(timeInfo, 40, BitConverter.DoubleToInt64Bits(0.0)); Marshal.WriteInt64(timeInfo, 48, 0); Marshal.WriteInt64(timeInfo, 56, 0);
        Marshal.WriteInt32(timeInfo, 64, 4); Marshal.WriteInt32(timeInfo, 68, 4); Marshal.WriteInt32(timeInfo, 72, 0); Marshal.WriteInt32(timeInfo, 76, 0); Marshal.WriteInt32(timeInfo, 80, 0);
        Marshal.WriteInt32(timeInfo, 84, (HostPlaying ? 2 : 0) | 512 | 1024 | 8192);
    }

    // ---- editor window ----
    public Size GetEditorSize()
    {
        IntPtr pp = Marshal.AllocHGlobal(IntPtr.Size);
        try
        {
            Marshal.WriteIntPtr(pp, IntPtr.Zero); Call(effEditGetRect, 0, IntPtr.Zero, pp, 0); IntPtr rp = Marshal.ReadIntPtr(pp);
            if (rp == IntPtr.Zero) return new Size(640, 480);
            short top = Marshal.ReadInt16(rp, 0), left = Marshal.ReadInt16(rp, 2), bottom = Marshal.ReadInt16(rp, 4), right = Marshal.ReadInt16(rp, 6);
            return new Size(Math.Max(100, right - left), Math.Max(60, bottom - top));
        }
        catch { return new Size(640, 480); }
        finally { Marshal.FreeHGlobal(pp); }
    }
    public void OpenEditor(IntPtr hwnd) { if (EditorOpen) return; Call(effEditOpen, 0, IntPtr.Zero, hwnd, 0); EditorOpen = true; }
    public void CloseEditor() { if (!EditorOpen) return; try { Call(effEditClose, 0, IntPtr.Zero, IntPtr.Zero, 0); } catch { } EditorOpen = false; }
    public void EditorIdle() { if (EditorOpen && !disposed) { try { Call(effEditIdle, 0, IntPtr.Zero, IntPtr.Zero, 0); } catch { } } }

    // ---- host callback ----
    static IntPtr HostCallback(IntPtr effect, int op, int index, IntPtr value, IntPtr ptr, float opt)
    {
        try
        {
            switch (op)
            {
                case 1: return (IntPtr)2400;                      // audioMasterVersion: we speak VST 2.4
                case 6: return (IntPtr)1;                         // audioMasterWantMidi
                case 7: UpdateTimeStatic(); return timeInfo;      // audioMasterGetTime
                case 15:                                          // audioMasterSizeWindow
                    { VstPlugin p; lock (byEffect) byEffect.TryGetValue(effect, out p); var h = SizeWindowRequested; if (h != null && p != null) h(p, index, (int)value); return (IntPtr)1; }
                case 16: return (IntPtr)HostSampleRate;           // audioMasterGetSampleRate
                case 17: return (IntPtr)MaxBlock;                 // audioMasterGetBlockSize
                case 23: return IntPtr.Zero;                      // audioMasterGetCurrentProcessLevel: unsupported
                case 32: PutString(ptr, "YtToMp3"); return (IntPtr)1;
                case 33: PutString(ptr, "Purphex Music"); return (IntPtr)1;
                case 34: return (IntPtr)1000;
                case 37:                                          // audioMasterCanDo
                    { string s = Marshal.PtrToStringAnsi(ptr) ?? ""; return (s == "sendVstEvents" || s == "sendVstMidiEvent" || s == "sizeWindow" || s == "supplyIdle" || s == "receiveVstTimeInfo") ? (IntPtr)1 : (IntPtr)(-1); }
                case 38: return (IntPtr)1;                        // audioMasterGetLanguage: English
                case 43: case 44: return (IntPtr)1;               // begin / end edit
            }
        }
        catch { }
        return IntPtr.Zero;
    }
    static void UpdateTimeStatic()
    {
        double sr = HostSampleRate, sp = HostPos, tempo = HostBpm;
        Marshal.WriteInt64(timeInfo, 0, BitConverter.DoubleToInt64Bits(sp)); Marshal.WriteInt64(timeInfo, 8, BitConverter.DoubleToInt64Bits(sr));
        Marshal.WriteInt64(timeInfo, 24, BitConverter.DoubleToInt64Bits(sp / sr * tempo / 60.0)); Marshal.WriteInt64(timeInfo, 32, BitConverter.DoubleToInt64Bits(tempo));
        Marshal.WriteInt32(timeInfo, 64, 4); Marshal.WriteInt32(timeInfo, 68, 4); Marshal.WriteInt32(timeInfo, 84, (HostPlaying ? 2 : 0) | 512 | 1024 | 8192);
    }
    static void PutString(IntPtr dst, string s) { if (dst == IntPtr.Zero) return; var b = Encoding.ASCII.GetBytes(s + "\0"); Marshal.Copy(b, 0, dst, b.Length); }

    public void Dispose()
    {
        if (disposed) return; disposed = true;
        try { CloseEditor(); } catch { }
        try { Call(effStopProcess, 0, IntPtr.Zero, IntPtr.Zero, 0); Call(effMainsChanged, 0, IntPtr.Zero, IntPtr.Zero, 0); Call(effClose, 0, IntPtr.Zero, IntPtr.Zero, 0); } catch { }
        lock (byEffect) byEffect.Remove(eff); freed = true;
        // the plugin may still have a thread that touches its buffers for a moment, so the buffers are left allocated (small) and the library is released last
        if (lib != IntPtr.Zero) { try { VstNative.FreeLibrary(lib); } catch { } lib = IntPtr.Zero; }
    }
}

// An instrument or effect VST as an ordinary effect in a track's chain.
class VstFx : InstrumentFx, IDisposable
{
    public IVstHosted Plugin; public string File = "";
    public override int Latency { get { return Plugin == null ? 0 : Plugin.Latency; } }
    VstWindow window;
    const int MaxParams = 128;

    public VstFx(IVstHosted p, string file)
    {
        Plugin = p; File = file; Type = "vst:" + System.IO.Path.GetFileName(file); Title = p.Name; Category = p.IsSynth ? "Instruments" : "Plugins"; Blurb = (p.IsSynth ? "VST instrument" : "VST effect") + (p.Vendor.Length > 0 ? " by " + p.Vendor : "");
        int n = Math.Min(p.NumParams, MaxParams);
        for (int i = 0; i < n; i++) { string nm = p.ParamName(i); if (nm.Length == 0) nm = "Parameter " + (i + 1); AddP("p" + i, nm, 0f, 1f, p.GetParam(i), ""); }
        dirty = false;
    }

    public override bool IsInstrument { get { return Plugin.IsSynth; } }
    public override bool HasWindow { get { return Plugin.HasEditor; } }
    public override string WindowLabel { get { return Plugin.HasEditor ? "Open " + Title + " window" : "(this plugin has no window)"; } }
    protected override void OnBlock(long pos, int n, bool transport) { VstPlugin.HostPos = pos; VstPlugin.HostPlaying = transport; VstPlugin.HostSampleRate = SR; }
    protected override void OnEvents(List<KeyValuePair<int, MidiEv>> events) { if (events.Count > 0) Plugin.SendMidi(events); }

    public override void Process(float[] l, float[] r, int n)
    {
        if (dirty) { dirty = false; for (int i = 0; i < P.Count; i++) { int idx; if (P[i].Id.Length > 1 && int.TryParse(P[i].Id.Substring(1), out idx)) Plugin.SetParam(idx, P[i].Val); } }
        Plugin.Process(l, r, n);
    }

    // The saved state is the plugin's own patch data (so a plugin preset survives saving the project).
    public override string Serialize()
    {
        var c = Plugin.GetChunk(); if (c != null) return "chunk=" + Convert.ToBase64String(c);
        return base.Serialize();
    }
    public override void Deserialize(string s)
    {
        if (s != null && s.StartsWith("chunk=")) { try { Plugin.SetChunk(Convert.FromBase64String(s.Substring(6))); } catch { } return; }
        base.Deserialize(s);
    }

    // ---- editor window ----
    public override void ShowWindow(IWin32Window owner)
    {
        if (window != null && !window.IsDisposed) { window.Activate(); window.WindowState = FormWindowState.Normal; return; }
        window = new VstWindow(this, Owner); window.Show(owner);
    }
    public override void CloseWindow() { try { if (window != null && !window.IsDisposed) window.Close(); } catch { } window = null; }

    public void Dispose() { CloseWindow(); try { Plugin.Dispose(); } catch { } }
}

// The plugin's own window (its real interface) plus a keyboard for playing instruments.
class VstWindow : Form, IKeySink
{
    readonly VstFx fx; readonly Track track; Panel host; PianoPanel piano; Timer idle; int octave = 4; readonly HashSet<int> down = new HashSet<int>();
    static readonly Keys[] KeyMap = { Keys.A, Keys.W, Keys.S, Keys.E, Keys.D, Keys.F, Keys.T, Keys.G, Keys.Y, Keys.H, Keys.U, Keys.J, Keys.K, Keys.O, Keys.L, Keys.P, Keys.Oemtilde };

    public VstWindow(VstFx fx, Track t)
    {
        this.fx = fx; track = t; Text = fx.Title + "  (VST)"; BackColor = Color.FromArgb(10, 4, 20); ForeColor = Color.FromArgb(240, 232, 255); KeyPreview = true; StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; Win.Dark(this);
        var sz = fx.Plugin.HasEditor ? fx.Plugin.GetEditorSize() : new Size(520, 100);
        host = new Panel { Left = 0, Top = 0, Width = sz.Width, Height = sz.Height, BackColor = Color.Black };
        bool keys = fx.IsInstrument; int ph = keys ? 128 : 0;
        piano = new PianoPanel(this) { Left = 0, Top = sz.Height, Width = sz.Width, Height = ph, Visible = keys };
        Controls.Add(host); Controls.Add(piano); ClientSize = new Size(Math.Max(520, sz.Width), sz.Height + ph);
        host.Width = ClientSize.Width; piano.Width = ClientSize.Width;
        if (!fx.Plugin.HasEditor) host.Controls.Add(new Label { Text = "This plugin has no window of its own. Use its sliders in the effect panel.", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(200, 180, 230), TextAlign = ContentAlignment.MiddleCenter });
        Shown += (s, e) =>
        {
            if (fx.Plugin.HasEditor)
                try
                {
                    fx.Plugin.OpenEditor(host.Handle);
                    var s2 = fx.Plugin.GetEditorSize(); Resize2(s2.Width, s2.Height);
                }
                catch (Exception ex) { host.Controls.Add(new Label { Text = "Couldn't open the plugin window: " + ex.Message, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(255, 160, 160), TextAlign = ContentAlignment.MiddleCenter }); }
            idle = new Timer { Interval = 40 }; idle.Tick += (s3, e3) => fx.Plugin.EditorIdle(); idle.Start();
        };
        FormClosing += (s, e) => { if (idle != null) idle.Stop(); ReleaseAll(); try { fx.Plugin.CloseEditor(); } catch { } };
        VstPlugin.SizeWindowRequested += OnSizeRequest;
        FormClosed += (s, e) => VstPlugin.SizeWindowRequested -= OnSizeRequest;
        Activated += (s, e) => { MidiInput.Target = fx; };
        FormClosed += (s, e) => { if (MidiInput.Target == fx) MidiInput.Target = null; };
        Deactivate += (s, e) => ReleaseAll();
    }

    void OnSizeRequest(object p, int w, int h) { if (p != (object)fx.Plugin || IsDisposed) return; try { BeginInvoke(new Action(() => Resize2(w, h))); } catch { } }
    void Resize2(int w, int h)
    {
        host.SetBounds(0, 0, Math.Max(100, w), Math.Max(60, h)); int ph = piano.Visible ? piano.Height : 0;
        piano.SetBounds(0, host.Height, Math.Max(520, w), ph); ClientSize = new Size(Math.Max(520, w), host.Height + ph); host.Width = ClientSize.Width;
    }

    // ---- playing notes ----
    public void KeyOn(int note, int vel) { if (!down.Add(note)) return; fx.NoteOn(note, vel); var f = InstrumentFx.Recorder; if (f != null) f(track, true, note, vel); }
    public void KeyOff(int note) { if (!down.Remove(note)) return; fx.NoteOff(note); var f = InstrumentFx.Recorder; if (f != null) f(track, false, note, 0); }
    void ReleaseAll() { foreach (var n in new List<int>(down)) KeyOff(n); piano.Invalidate(); }

    protected override bool ProcessCmdKey(ref Message msg, Keys k)
    {
        if (fx.IsInstrument && (k == Keys.Z || k == Keys.X) && (msg.Msg == 0x100)) { ReleaseAll(); octave = Math.Max(0, Math.Min(8, octave + (k == Keys.Z ? -1 : 1))); piano.Invalidate(); return true; }
        return base.ProcessCmdKey(ref msg, k);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!fx.IsInstrument) return;
        int i = Array.IndexOf(KeyMap, e.KeyCode); if (i >= 0 && !e.Control && !e.Alt) { KeyOn(12 * (octave + 1) + i, 100); piano.Invalidate(); e.Handled = true; }
    }
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        int i = Array.IndexOf(KeyMap, e.KeyCode); if (i >= 0) { KeyOff(12 * (octave + 1) + i); piano.Invalidate(); }
    }
    public int BaseNote { get { return 12 * (octave + 1); } }
    public bool IsDown(int note) { return down.Contains(note); }
    public int Octave { get { return octave; } set { octave = Math.Max(0, Math.Min(8, value)); } }
}

class PianoPanel : Panel
{
    readonly IKeySink w; int mouseNote = -1; int Keys3 = 36;   // three octaves of keys
    public PianoPanel(IKeySink w, int keys = 36) { this.w = w; Keys3 = keys; DoubleBuffered = true; BackColor = Color.FromArgb(14, 7, 30); }
    static bool Black(int semi) { int s = semi % 12; return s == 1 || s == 3 || s == 6 || s == 8 || s == 10; }

    RectangleF KeyRect(int i, out bool black)
    {
        int whites = 0; for (int k = 0; k < Keys3; k++) if (!Black(k)) whites++;
        float ww = Width / (float)whites, top = 22, h = Height - top - 6; int wi = 0; for (int k = 0; k < i; k++) if (!Black(k)) wi++;
        black = Black(i);
        if (!black) return new RectangleF(wi * ww, top, ww, h);
        return new RectangleF(wi * ww - ww * 0.3f, top, ww * 0.6f, h * 0.62f);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.Clear(BackColor); g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var f = new Font("Segoe UI", 9f)) using (var b = new SolidBrush(Color.FromArgb(190, 170, 230)))
            g.DrawString("Keyboard: A S D F G H J = white keys, W E T Y U = black.  Z / X = octave (now " + w.Octave + ")", f, b, 6, 3);
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < Keys3; i++)
            {
                bool black; var r = KeyRect(i, out black); if ((pass == 0) == black) continue; int note = w.BaseNote + i; bool on = w.IsDown(note);
                Color c = black ? (on ? Color.FromArgb(200, 90, 240) : Color.FromArgb(20, 12, 36)) : (on ? Color.FromArgb(220, 150, 255) : Color.FromArgb(235, 228, 250));
                using (var br = new SolidBrush(c)) g.FillRectangle(br, r); using (var pen = new Pen(Color.FromArgb(90, 60, 140))) g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
            }
    }
    int Hit(Point p) { for (int pass = 0; pass < 2; pass++) for (int i = 0; i < Keys3; i++) { bool black; var r = KeyRect(i, out black); if ((pass == 0) != black && r.Contains(p)) return i; } return -1; }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e); int i = Hit(e.Location);
        if (i >= 0) { bool bk; var kr = KeyRect(i, out bk); int vel = (int)Math.Max(30, Math.Min(127, 35 + 92 * (e.Y - kr.Y) / Math.Max(1, kr.Height))); mouseNote = w.BaseNote + i; w.KeyOn(mouseNote, vel); Invalidate(); }   // lower on the key = louder
    }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); if (mouseNote >= 0) { w.KeyOff(mouseNote); mouseNote = -1; Invalidate(); } }
}
