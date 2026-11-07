using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

// =====================================================================================================
//  VST 3 plugin host (64-bit): effects and instruments in .vst3 files.
//  A VST 3 plugin is a native .dll (renamed .vst3) that exports GetPluginFactory. Everything after that is COM-style objects
//  (a table of function pointers). This file builds the few host-side objects the plugin needs and calls the plugin's own.
//  Written from the public VST 3 binary interface. It is tested against a stand-in plugin built in memory (see Vst3Test.cs);
//  it has not been tried against a plugin from a plugin maker, so treat it as experimental.
// =====================================================================================================

// what the rest of the app needs from any hosted plugin (VST2 or VST3)
interface IVstHosted : IDisposable
{
    string Name { get; } string Vendor { get; } string Path { get; } int NumParams { get; } bool IsSynth { get; } bool HasEditor { get; } bool EditorOpen { get; }
    string ParamName(int i); float GetParam(int i); void SetParam(int i, float v);
    byte[] GetChunk(); void SetChunk(byte[] data); void SendMidi(List<KeyValuePair<int, MidiEv>> ev); void Process(float[] l, float[] r, int n);
    Size GetEditorSize(); void OpenEditor(IntPtr hwnd); void CloseEditor(); void EditorIdle();
    int Latency { get; }
    Action<int, float> Automated { get; set; }   // the plugin changed one of its own parameters (its window, a preset it loaded): index, new value. Can be called on any thread
    bool ParamsDirty { get; set; }   // the plugin said its values changed without saying which: the host re-reads them later on the window thread (never in the plugin's callback)
}

static class VstLoader
{
    public static bool IsVst3(string path) { return path.EndsWith(".vst3", StringComparison.OrdinalIgnoreCase); }
    public static IVstHosted Load(string path, int sr) { return IsVst3(path) ? (IVstHosted)Vst3Plugin.Load(path, sr) : VstPlugin.Load(path, sr); }
}

// ---- COM plumbing ----
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int ComQi(IntPtr self, IntPtr iid, IntPtr obj);
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate uint ComRef(IntPtr self);
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int D_I(IntPtr self);
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int D_IP(IntPtr self, IntPtr a);
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int D_IPP(IntPtr self, IntPtr a, IntPtr b);
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int D_IPPP(IntPtr self, IntPtr a, IntPtr b, IntPtr c);
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int D_II(IntPtr self, int a);
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int D_ISSP(IntPtr self, int bus, short ch, short ctrl, IntPtr id);
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int D_IIP(IntPtr self, int a, IntPtr b);
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int D_IIII(IntPtr self, int a, int b, int c);
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int D_IIIIB(IntPtr self, int a, int b, int c, byte d);
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int D_IB(IntPtr self, byte a);
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int D_IPIPI(IntPtr self, IntPtr a, int b, IntPtr c, int d);
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate uint D_U(IntPtr self);
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int D_IUD(IntPtr self, uint id, double v);
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int D_IU(IntPtr self, uint id);
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate double D_DU(IntPtr self, uint id);
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate IntPtr D_PP(IntPtr self, IntPtr a);
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate IntPtr D_PI(IntPtr self, int a);
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate IntPtr D_PPP(IntPtr self, IntPtr a, IntPtr b);
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int D_IIPP(IntPtr self, int a, IntPtr b, IntPtr c);
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int D_ILIP(IntPtr self, long a, int b, IntPtr c);
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int D_IIPI(IntPtr self, int a, IntPtr b, int c);
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int D_IPIP(IntPtr self, IntPtr a, int b, IntPtr c);
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int D_IUDP(IntPtr self, uint id, double v, IntPtr p);
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate IntPtr FactoryEntry();
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate byte BoolEntry();

class ComObject
{
    public IntPtr Ptr; public Func<byte[], IntPtr> Resolver; readonly List<Delegate> keep = new List<Delegate>(); readonly List<byte[]> iids = new List<byte[]>();
    public ComObject(byte[][] ids, params Delegate[] methods)
    {
        iids.Add(Vst3.IidUnknown); if (ids != null) iids.AddRange(ids);
        ComQi qi = QueryInterface; ComRef add = s => 1; ComRef rel = s => 1; keep.Add(qi); keep.Add(add); keep.Add(rel); foreach (var m in methods) keep.Add(m);
        IntPtr vt = Marshal.AllocHGlobal(IntPtr.Size * keep.Count); for (int i = 0; i < keep.Count; i++) Marshal.WriteIntPtr(vt, i * IntPtr.Size, Marshal.GetFunctionPointerForDelegate(keep[i]));
        Ptr = Marshal.AllocHGlobal(IntPtr.Size); Marshal.WriteIntPtr(Ptr, vt);
    }
    int QueryInterface(IntPtr self, IntPtr iid, IntPtr obj)
    {
        var b = new byte[16]; Marshal.Copy(iid, b, 0, 16); IntPtr r = IntPtr.Zero; if (Resolver != null) r = Resolver(b);
        if (r == IntPtr.Zero) foreach (var id in iids) if (Vst3.Same(id, b)) { r = Ptr; break; }
        Marshal.WriteIntPtr(obj, r); return r != IntPtr.Zero ? 0 : unchecked((int)0x80004002);
    }
}

static class Vst3
{
    // the 16-byte interface ids in the layout Windows uses
    public static byte[] Iid(uint a, uint b, uint c, uint d)
    {
        return new byte[] { (byte)a, (byte)(a >> 8), (byte)(a >> 16), (byte)(a >> 24), (byte)(b >> 16), (byte)(b >> 24), (byte)b, (byte)(b >> 8), (byte)(c >> 24), (byte)(c >> 16), (byte)(c >> 8), (byte)c, (byte)(d >> 24), (byte)(d >> 16), (byte)(d >> 8), (byte)d };
    }
    public static readonly byte[] IidUnknown = Iid(0x00000000, 0x00000000, 0xC0000000, 0x00000046), IidPluginBase = Iid(0x22888DDB, 0x156E45AE, 0x8358B348, 0x08190625), IidComponent = Iid(0xE831FF31, 0xF2D54301, 0x928EBBEE, 0x25697802),
        IidAudioProcessor = Iid(0x42043F99, 0xB7DA453C, 0xA569E79D, 0x9AAEC33D), IidEditController = Iid(0xDCD7BBE3, 0x7742448D, 0xA874AACC, 0x979C759E), IidComponentHandler = Iid(0x93A0BEA3, 0x0BD045DB, 0x8E890B0C, 0xC1E46AC6),
        IidHostApplication = Iid(0x58E595CC, 0xDB2D4969, 0x8B6AAF8C, 0x36A664E5), IidConnectionPoint = Iid(0x70A4156F, 0x6E6E4026, 0x989148BF, 0xAA60D8D1), IidPlugView = Iid(0x5BC32507, 0xD06049EA, 0xA6151B52, 0x2B755B29),
        IidPlugFrame = Iid(0x367FAF01, 0xAFA94693, 0x8D4DA2A0, 0xED0882A3), IidEventList = Iid(0x3A2C4214, 0x346349FE, 0xB2C4F397, 0xB9695A44), IidParameterChanges = Iid(0xA4779663, 0x0BB64A56, 0xB44284A0, 0x1B1AA946),
        IidMidiMapping = Iid(0xDF0FF9F7, 0x49B74669, 0xB63AB733, 0x6A2A8C58), IidParamValueQueue = Iid(0x01263A18, 0xED074F6F, 0x98C9D356, 0x4686F9BA), IidBStream = Iid(0xC3BF6EA2, 0x30994752, 0x9B6BF990, 0x1EE33E9B), IidPluginFactory2 = Iid(0x0007B650, 0xF24B4C0B, 0xA464EDB9, 0xF00B2ABB);
    public static bool Same(byte[] a, byte[] b) { for (int i = 0; i < 16; i++) if (a[i] != b[i]) return false; return true; }
    public static T Fn<T>(IntPtr obj, int slot) where T : class { IntPtr vt = Marshal.ReadIntPtr(obj); return (T)(object)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(vt, slot * IntPtr.Size), typeof(T)); }
    public static void Release(IntPtr obj) { if (obj != IntPtr.Zero) try { Fn<ComRef>(obj, 2)(obj); } catch { } }
    public static IntPtr Query(IntPtr obj, byte[] iid)
    {
        if (obj == IntPtr.Zero) return IntPtr.Zero; IntPtr g = Marshal.AllocHGlobal(16), o = Marshal.AllocHGlobal(IntPtr.Size);
        try { Marshal.Copy(iid, 0, g, 16); Marshal.WriteIntPtr(o, IntPtr.Zero); int r = Fn<ComQi>(obj, 0)(obj, g, o); return r == 0 ? Marshal.ReadIntPtr(o) : IntPtr.Zero; } finally { Marshal.FreeHGlobal(g); Marshal.FreeHGlobal(o); }
    }
    public static string Ansi(IntPtr p, int off, int max) { var b = new byte[max]; Marshal.Copy(p + off, b, 0, max); int n = Array.IndexOf(b, (byte)0); return Encoding.ASCII.GetString(b, 0, n < 0 ? max : n); }
    public static string Wide(IntPtr p, int off, int chars) { var c = new char[chars]; for (int i = 0; i < chars; i++) c[i] = (char)Marshal.ReadInt16(p, off + i * 2); int n = Array.IndexOf(c, '\0'); return new string(c, 0, n < 0 ? chars : n); }
    public static void PutWide(IntPtr p, string s, int chars) { for (int i = 0; i < chars; i++) Marshal.WriteInt16(p, i * 2, i < s.Length && i < chars - 1 ? (short)s[i] : (short)0); }
    public const int Ok = 0, False = 1;
}

// a memory stream the plugin can read its saved state from / write it to
class Vst3Stream
{
    public MemoryStream Mem = new MemoryStream(); public ComObject Obj;
    public Vst3Stream(byte[] data) { if (data != null) { Mem.Write(data, 0, data.Length); Mem.Position = 0; } Obj = new ComObject(new[] { Vst3.IidBStream }, new D_IPIP(Read), new D_IPIP(Write), new D_ILIP(Seek), new D_IP(Tell)); }
    int Read(IntPtr s, IntPtr buf, int n, IntPtr nRead) { var b = new byte[Math.Max(0, n)]; int r = Mem.Read(b, 0, b.Length); if (r > 0) Marshal.Copy(b, 0, buf, r); if (nRead != IntPtr.Zero) Marshal.WriteInt32(nRead, r); return Vst3.Ok; }
    int Write(IntPtr s, IntPtr buf, int n, IntPtr nWritten) { var b = new byte[Math.Max(0, n)]; if (n > 0) Marshal.Copy(buf, b, 0, n); Mem.Write(b, 0, b.Length); if (nWritten != IntPtr.Zero) Marshal.WriteInt32(nWritten, n); return Vst3.Ok; }
    int Seek(IntPtr s, long pos, int mode, IntPtr res) { long np = mode == 0 ? pos : (mode == 1 ? Mem.Position + pos : Mem.Length + pos); if (np < 0) return Vst3.False; Mem.Position = np; if (res != IntPtr.Zero) Marshal.WriteInt64(res, np); return Vst3.Ok; }
    int Tell(IntPtr s, IntPtr pos) { if (pos != IntPtr.Zero) Marshal.WriteInt64(pos, Mem.Position); return Vst3.Ok; }
}

class Vst3Plugin : IVstHosted
{
    public const int MaxBlock = 8192, MaxEvents = 256, MaxChanges = 64;
    IntPtr lib, factory, component, processor, controller, view, connComp, connCtrl; bool disposed, controllerSeparate, exitDll;
    string name = "", vendor = "", path = "", subCats = ""; int numIn, numOut, sampleRate = 44100; bool synth, hasEditor;   /* subCats: what the plugin says it is ("Instrument|Synth", "Fx|Reverb"...) */ Size editorSize = new Size(640, 480);
    readonly List<uint> paramIds = new List<uint>(); readonly List<string> paramNames = new List<string>();
    readonly Dictionary<uint, int> paramIndex = new Dictionary<uint, int>(); float[] known = new float[0];   /* paramIndex: ParamID -> index (filled once at load, only read after). known: the last value of each the host knows of, to tell which changed when the plugin re-reads */
    readonly object callLock = new object();
    D_IP processFn; D_IUD setParamFn; D_DU getParamFn; D_U latFn; volatile int latency;
    public Action<int, float> Automated { get; set; }
    volatile bool paramsDirty; public bool ParamsDirty { get { return paramsDirty; } set { paramsDirty = value; } }
    public int Latency { get { return disposed ? 0 : latency; } }
    void RefreshLatency() { if (latFn == null) return; try { latency = (int)Math.Max(0u, Math.Min(latFn(processor), 1u << 16)); } catch { } }
    IntPtr[] inBuf = new IntPtr[2], outBuf = new IntPtr[2]; IntPtr inArr, outArr, inBus, outBus, procData, procCtx, evBuf, outEvBuf, setup;
    ComObject host, handler, frame, inEvents, outEvents, inChanges, outChanges; ComObject[] queues = new ComObject[MaxChanges]; readonly Dictionary<IntPtr, int> queueIndex = new Dictionary<IntPtr, int>();
    int evCount, changeCount; readonly uint[] chId = new uint[MaxChanges]; readonly double[] chVal = new double[MaxChanges];
    int procCount; readonly uint[] pId = new uint[MaxChanges]; readonly double[] pVal = new double[MaxChanges];   // the changes this block hands the processor: a copy taken under the lock, so an edit made while the plugin processes is kept for the next block instead of being wiped
    readonly List<KeyValuePair<int, MidiEv>> pending = new List<KeyValuePair<int, MidiEv>>(); readonly HashSet<int> active = new HashSet<int>();

    public string Name { get { return name; } } public string Vendor { get { return vendor; } } public string Path { get { return path; } } public int NumParams { get { return paramIds.Count; } }
    public bool IsSynth { get { return synth; } } public bool HasEditor { get { return hasEditor; } } public bool EditorOpen { get { return view != IntPtr.Zero; } }
    public event Action<int, int> SizeRequested;

    // ---- loading ----
    public static Vst3Plugin Load(string file, int sr)
    {
        if (IntPtr.Size != 8) throw new Exception("This app is running as 32-bit, so VST plugins can't be loaded.");
        if (Directory.Exists(file)) { string inner = System.IO.Path.Combine(file, "Contents", "x86_64-win", System.IO.Path.GetFileName(file)); if (!File.Exists(inner)) throw new Exception("That .vst3 folder has no 64-bit Windows plugin inside."); file = inner; }
        string dir = System.IO.Path.GetDirectoryName(file); try { if (!string.IsNullOrEmpty(dir)) VstNative.SetDllDirectory(dir); } catch { }
        IntPtr h = VstNative.LoadLibrary(file);
        if (h == IntPtr.Zero) throw new Exception("Windows couldn't load " + System.IO.Path.GetFileName(file) + " (error " + Marshal.GetLastWin32Error() + "). It may be a 32-bit plugin, or it needs another program's files.");
        IntPtr init = VstNative.GetProcAddress(h, "InitDll"); bool didInit = false;
        if (init != IntPtr.Zero) { try { didInit = ((BoolEntry)Marshal.GetDelegateForFunctionPointer(init, typeof(BoolEntry)))() != 0; } catch { } }
        IntPtr gpf = VstNative.GetProcAddress(h, "GetPluginFactory"); if (gpf == IntPtr.Zero) { VstNative.FreeLibrary(h); throw new Exception(System.IO.Path.GetFileName(file) + " isn't a VST3 plugin."); }
        try { var p = FromFactory(((FactoryEntry)Marshal.GetDelegateForFunctionPointer(gpf, typeof(FactoryEntry)))(), file, sr); p.lib = h; p.exitDll = didInit; return p; }
        catch { VstNative.FreeLibrary(h); throw; }
    }

    internal static Vst3Plugin FromFactory(IntPtr factory, string label, int sr)
    {
        if (factory == IntPtr.Zero) throw new Exception("The plugin didn't give a factory.");
        var p = new Vst3Plugin { factory = factory, path = label, sampleRate = sr }; try { p.Setup(); return p; } catch { try { p.Dispose(); } catch { } throw; }
    }

    void Setup()
    {
        IntPtr info = Marshal.AllocHGlobal(512);
        try
        {
            if (Vst3.Fn<D_IP>(factory, 3)(factory, info) == 0) vendor = Vst3.Ansi(info, 0, 64);
            int n = Vst3.Fn<D_I>(factory, 4)(factory); byte[] cid = null; var getInfo = Vst3.Fn<D_IIP>(factory, 5);
            int ci = -1; for (int i = 0; i < n && cid == null; i++) { if (getInfo(factory, i, info) != 0) continue; if (Vst3.Ansi(info, 20, 32) == "Audio Module Class") { cid = new byte[16]; Marshal.Copy(info, cid, 0, 16); name = Vst3.Ansi(info, 52, 64); ci = i; } }
            // what it is, from its own label (IPluginFactory2): an instrument says "Instrument" even when it has an audio input for a sidechain
            if (ci >= 0) { IntPtr f2 = Vst3.Query(factory, Vst3.IidPluginFactory2); if (f2 != IntPtr.Zero) { try { if (Vst3.Fn<D_IIP>(f2, 7)(f2, ci, info) == 0) subCats = Vst3.Ansi(info, 120, 128); } catch { } Vst3.Release(f2); } }
            if (cid == null) throw new Exception("This file has no audio plugin inside.");
            host = MakeHost(); handler = MakeHandler(); frame = MakeFrame();
            component = CreateInstance(cid, Vst3.IidComponent); if (component == IntPtr.Zero) throw new Exception("The plugin couldn't be created.");
            if (Vst3.Fn<D_IP>(component, 3)(component, host.Ptr) != 0) throw new Exception("The plugin refused to start.");
            processor = Vst3.Query(component, Vst3.IidAudioProcessor); if (processor == IntPtr.Zero) throw new Exception("That plugin has no audio processor.");
            controller = Vst3.Query(component, Vst3.IidEditController);
            if (controller == IntPtr.Zero)
            {
                IntPtr cc = Marshal.AllocHGlobal(16);
                try { if (Vst3.Fn<D_IP>(component, 5)(component, cc) == 0) { var ccid = new byte[16]; Marshal.Copy(cc, ccid, 0, 16); controller = CreateInstance(ccid, Vst3.IidEditController); controllerSeparate = controller != IntPtr.Zero; if (controllerSeparate) Vst3.Fn<D_IP>(controller, 3)(controller, host.Ptr); } }
                finally { Marshal.FreeHGlobal(cc); }
            }
            if (controller != IntPtr.Zero)
            {
                Vst3.Fn<D_IP>(controller, 16)(controller, handler.Ptr);   // setComponentHandler
                if (controllerSeparate) { connComp = Vst3.Query(component, Vst3.IidConnectionPoint); connCtrl = Vst3.Query(controller, Vst3.IidConnectionPoint); if (connComp != IntPtr.Zero && connCtrl != IntPtr.Zero) { Vst3.Fn<D_IP>(connComp, 3)(connComp, connCtrl); Vst3.Fn<D_IP>(connCtrl, 3)(connCtrl, connComp); } }
            }
            var busCount = Vst3.Fn<D_III>(component, 7); numIn = busCount(component, 0, 0); numOut = busCount(component, 0, 1); synth = numIn == 0 || subCats.IndexOf("Instrument", StringComparison.OrdinalIgnoreCase) >= 0;   // (only "no audio input" counted before: a synth with a sidechain input was taken for an effect, so the MIDI keyboard played the built-in piano instead of it)
            // every bus stereo
            IntPtr arr = Marshal.AllocHGlobal(8 * 32); try { for (int i = 0; i < 32; i++) Marshal.WriteInt64(arr, i * 8, 3); IntPtr ia = numIn > 0 ? arr : IntPtr.Zero; Vst3.Fn<D_IPIPI>(processor, 3)(processor, ia, Math.Min(32, numIn), arr, Math.Min(32, numOut)); } finally { Marshal.FreeHGlobal(arr); }
            var act = Vst3.Fn<D_IIIIB>(component, 10);
            if (numIn > 0) act(component, 0, 0, 0, 1); if (numOut > 0) act(component, 0, 1, 0, 1); if (busCount(component, 1, 0) > 0) act(component, 1, 0, 0, 1);
            Vst3.Fn<D_II>(processor, 5)(processor, 0);   // canProcessSampleSize(32 bit)
            setup = Marshal.AllocHGlobal(24); Marshal.WriteInt32(setup, 0, 0); Marshal.WriteInt32(setup, 4, 0); Marshal.WriteInt32(setup, 8, MaxBlock); Marshal.WriteInt32(setup, 12, 0); Marshal.WriteInt64(setup, 16, BitConverter.DoubleToInt64Bits(sampleRate));
            if (Vst3.Fn<D_IP>(processor, 7)(processor, setup) != 0) throw new Exception("The plugin didn't accept this sample rate or block size.");
            Vst3.Fn<D_IB>(component, 11)(component, 1); Vst3.Fn<D_IB>(processor, 8)(processor, 1);
            processFn = Vst3.Fn<D_IP>(processor, 9);
            try { latFn = Vst3.Fn<D_U>(processor, 6); } catch { latFn = null; }
            RefreshLatency();
            BuildBuffers(); ReadParams(); ReadMidiMap(); ProbeEditor();
            if (name.Length == 0) name = System.IO.Path.GetFileNameWithoutExtension(path);
        }
        finally { Marshal.FreeHGlobal(info); }
    }

    IntPtr CreateInstance(byte[] cid, byte[] iid)
    {
        IntPtr c = Marshal.AllocHGlobal(16), i = Marshal.AllocHGlobal(16), o = Marshal.AllocHGlobal(IntPtr.Size);
        try { Marshal.Copy(cid, 0, c, 16); Marshal.Copy(iid, 0, i, 16); Marshal.WriteIntPtr(o, IntPtr.Zero); int r = Vst3.Fn<D_IPPP>(factory, 6)(factory, c, i, o); return r == 0 ? Marshal.ReadIntPtr(o) : IntPtr.Zero; }
        finally { Marshal.FreeHGlobal(c); Marshal.FreeHGlobal(i); Marshal.FreeHGlobal(o); }
    }

    void BuildBuffers()
    {
        for (int i = 0; i < 2; i++) { inBuf[i] = Marshal.AllocHGlobal(MaxBlock * 4); outBuf[i] = Marshal.AllocHGlobal(MaxBlock * 4); }
        inArr = Marshal.AllocHGlobal(16); outArr = Marshal.AllocHGlobal(16); for (int i = 0; i < 2; i++) { Marshal.WriteIntPtr(inArr, i * 8, inBuf[i]); Marshal.WriteIntPtr(outArr, i * 8, outBuf[i]); }
        inBus = Marshal.AllocHGlobal(24); outBus = Marshal.AllocHGlobal(24); Marshal.WriteInt32(inBus, 0, 2); Marshal.WriteInt64(inBus, 8, 0); Marshal.WriteIntPtr(inBus, 16, inArr); Marshal.WriteInt32(outBus, 0, 2); Marshal.WriteInt64(outBus, 8, 0); Marshal.WriteIntPtr(outBus, 16, outArr);
        procData = Marshal.AllocHGlobal(80); procCtx = Marshal.AllocHGlobal(112); evBuf = Marshal.AllocHGlobal(48 * MaxEvents); outEvBuf = IntPtr.Zero;
        for (int i = 0; i < 112; i += 8) Marshal.WriteInt64(procCtx, i, 0); for (int i = 0; i < 80; i += 8) Marshal.WriteInt64(procData, i, 0);
        inEvents = new ComObject(new[] { Vst3.IidEventList }, new D_I(EvCount), new D_IIP(EvGet), new D_IP(EvAdd));
        outEvents = new ComObject(new[] { Vst3.IidEventList }, new D_I(s => 0), new D_IIP((s, i, e) => Vst3.False), new D_IP((s, e) => Vst3.Ok));
        for (int i = 0; i < MaxChanges; i++) { var q = new ComObject(new[] { Vst3.IidParamValueQueue }, new D_U(QId), new D_I(s => 1), new D_IIPP(QPoint), new D_IUDP((s, id, v, pidx) => Vst3.False)); queues[i] = q; queueIndex[q.Ptr] = i; }
        inChanges = new ComObject(new[] { Vst3.IidParameterChanges }, new D_I(s => procCount), new D_PI(ChGet), new D_PPP((s, id, idx) => IntPtr.Zero));
        outChanges = new ComObject(new[] { Vst3.IidParameterChanges }, new D_I(s => 0), new D_PI((s, i) => IntPtr.Zero), new D_PPP((s, id, idx) => IntPtr.Zero));
    }
    int EvCount(IntPtr s) { return evCount; }
    int EvGet(IntPtr s, int i, IntPtr e) { if (i < 0 || i >= evCount) return Vst3.False; for (int o = 0; o < 48; o += 8) Marshal.WriteInt64(e, o, Marshal.ReadInt64(evBuf, i * 48 + o)); return Vst3.Ok; }
    int EvAdd(IntPtr s, IntPtr e) { return Vst3.Ok; }
    IntPtr ChGet(IntPtr s, int i) { return i >= 0 && i < procCount ? queues[i].Ptr : IntPtr.Zero; }
    uint QId(IntPtr s) { int i; return queueIndex.TryGetValue(s, out i) ? pId[i] : 0; }
    int QPoint(IntPtr s, int idx, IntPtr sampleOffset, IntPtr value) { int i; if (!queueIndex.TryGetValue(s, out i) || idx != 0) return Vst3.False; Marshal.WriteInt32(sampleOffset, 0); Marshal.WriteInt64(value, BitConverter.DoubleToInt64Bits(pVal[i])); return Vst3.Ok; }
    // a value for the processor in the next block (latest wins for the same parameter). Short lock, no allocation
    void QueueChange(uint id, double v)
    {
        lock (pending) { for (int k = 0; k < changeCount; k++) if (chId[k] == id) { chVal[k] = v; return; } if (changeCount < MaxChanges) { chId[changeCount] = id; chVal[changeCount] = v; changeCount++; } }
    }

    ComObject MakeHost()
    {
        return new ComObject(new[] { Vst3.IidHostApplication }, new D_IP((s, n) => { Vst3.PutWide(n, "Purphex Music", 128); return Vst3.Ok; }), new D_IPPP((s, cid, iid, obj) => { if (obj != IntPtr.Zero) Marshal.WriteIntPtr(obj, IntPtr.Zero); return unchecked((int)0x80004002); }));
    }
    ComObject MakeHandler()
    {
        return new ComObject(new[] { Vst3.IidComponentHandler }, new D_IU((s, id) => Vst3.Ok), new D_IUD(PerformEdit), new D_IU((s, id) => Vst3.Ok), new D_II(RestartComponent));   // beginEdit, performEdit, endEdit, restartComponent
    }
    // the plugin's own window moved one of its controls: the processor hears it (in VST 3 the host carries it there) and the host's copy follows. Not sent back to the controller, which already has it
    int PerformEdit(IntPtr s, uint id, double v)
    {
        try
        {
            if (disposed || double.IsNaN(v)) return Vst3.Ok; v = Math.Max(0.0, Math.Min(1.0, v));
            QueueChange(id, v);
            int i; if (paramIndex.TryGetValue(id, out i)) { var k = known; if (i < k.Length) k[i] = (float)v; var a = Automated; if (a != null) a(i, (float)v); }
        }
        catch { }
        return Vst3.Ok;
    }
    // RestartFlags (VST 3 SDK ivsteditcontroller.h): kReloadComponent 1<<0, kIoChanged 1<<1, kParamValuesChanged 1<<2, kLatencyChanged 1<<3, kParamTitlesChanged 1<<4, kMidiCCAssignmentChanged 1<<5
    const int kParamValuesChanged = 1 << 2, kLatencyChanged = 1 << 3, kMidiCCAssignmentChanged = 1 << 5;
    int RestartComponent(IntPtr s, int flags)
    {
        try
        {
            if (disposed) return Vst3.Ok;
            if ((flags & kLatencyChanged) != 0) RefreshLatency();
            if ((flags & kMidiCCAssignmentChanged) != 0) ReadMidiMap();
            if ((flags & kParamValuesChanged) != 0) ReReadParams();
        }
        catch { }
        return Vst3.Ok;
    }
    // every value from the controller again (a preset the plugin loaded itself); the ones that changed go to whoever listens. Called from the plugin (normally its window thread), so no host lock is taken here
    void ReReadParams()
    {
        if (controller == IntPtr.Zero || disposed) return;
        if (getParamFn == null) getParamFn = Vst3.Fn<D_DU>(controller, 14);
        var k = known; var a = Automated;
        for (int i = 0; i < paramIds.Count && i < k.Length; i++)
        {
            double d; try { d = getParamFn(controller, paramIds[i]); } catch { continue; }
            if (double.IsNaN(d)) continue; float v = (float)Math.Max(0.0, Math.Min(1.0, d));
            if (v == k[i]) continue; k[i] = v; if (a != null) a(i, v);
        }
    }
    ComObject MakeFrame()
    {
        return new ComObject(new[] { Vst3.IidPlugFrame }, new D_IPP((s, v, rect) =>
        {
            int w = Marshal.ReadInt32(rect, 8) - Marshal.ReadInt32(rect, 0), h = Marshal.ReadInt32(rect, 12) - Marshal.ReadInt32(rect, 4); editorSize = new Size(w, h); var ev = SizeRequested; if (ev != null) ev(w, h); VstPlugin.RaiseSize(this, w, h); return Vst3.Ok;
        }));
    }

    void ReadParams()
    {
        if (controller == IntPtr.Zero) return; int n = Vst3.Fn<D_I>(controller, 8)(controller), cap = Math.Min(n, 4096); var getInfo = Vst3.Fn<D_IIP>(controller, 9); IntPtr info = Marshal.AllocHGlobal(800);
        try
        {
            for (int i = 0; i < cap; i++)
            {
                if (getInfo(controller, i, info) != 0) continue; int flags = Marshal.ReadInt32(info, 788); if ((flags & 16) != 0 || (flags & 2) != 0) continue;   // hidden or read-only
                uint pid = (uint)Marshal.ReadInt32(info, 0); if (paramIndex.ContainsKey(pid)) continue;
                paramIndex[pid] = paramIds.Count; paramIds.Add(pid); paramNames.Add(Vst3.Wide(info, 4, 128));
            }
        }
        finally { Marshal.FreeHGlobal(info); }
        var k = new float[paramIds.Count]; var get = Vst3.Fn<D_DU>(controller, 14);
        for (int i = 0; i < k.Length; i++) { try { double d = get(controller, paramIds[i]); k[i] = double.IsNaN(d) ? 0f : (float)Math.Max(0.0, Math.Min(1.0, d)); } catch { } }
        known = k;
    }

    // which parameter each MIDI controller drives (VST 3 has no CC messages: sustain, mod wheel, pitch bend... are parameters the plugin names here)
    const int MapCtrls = 130; uint[] ccMap; const uint NoParam = 0xFFFFFFFF;
    void ReadMidiMap()
    {
        if (controller == IntPtr.Zero) return; IntPtr mm = Vst3.Query(controller, Vst3.IidMidiMapping); if (mm == IntPtr.Zero) return;
        IntPtr idp = Marshal.AllocHGlobal(4); bool any = false; var map = new uint[16 * MapCtrls];
        try
        {
            var get = Vst3.Fn<D_ISSP>(mm, 3);
            for (int ch = 0; ch < 16; ch++) for (int c = 0; c < MapCtrls; c++) { Marshal.WriteInt32(idp, -1); uint id = NoParam; try { if (get(mm, 0, (short)ch, (short)c, idp) == 0) id = (uint)Marshal.ReadInt32(idp); } catch { } map[ch * MapCtrls + c] = id; if (id != NoParam) any = true; }
        }
        catch { any = false; }
        finally { Marshal.FreeHGlobal(idp); Vst3.Release(mm); }
        if (any) ccMap = map;
    }
    // a controller's value goes to the processor as a parameter change in this block (sound thread, no waiting on the window)
    void CtrlChange(int ch, int ctrl, double v)
    {
        var map = ccMap; if (map == null || ctrl < 0 || ctrl >= MapCtrls) return; uint id = map[(ch & 15) * MapCtrls + ctrl]; if (id == NoParam) return;
        v = Math.Max(0.0, Math.Min(1.0, v));
        QueueChange(id, v);
    }

    void ProbeEditor()
    {
        if (controller == IntPtr.Zero) return;
        IntPtr nm = Marshal.StringToHGlobalAnsi("editor");
        try
        {
            IntPtr v = Vst3.Fn<D_PP>(controller, 17)(controller, nm); if (v == IntPtr.Zero) return;
            IntPtr t = Marshal.StringToHGlobalAnsi("HWND"); try { hasEditor = Vst3.Fn<D_IP>(v, 3)(v, t) == 0; } finally { Marshal.FreeHGlobal(t); }
            if (hasEditor) editorSize = ReadSize(v); Vst3.Release(v);
        }
        catch { hasEditor = false; }
        finally { Marshal.FreeHGlobal(nm); }
    }
    static Size ReadSize(IntPtr v) { IntPtr r = Marshal.AllocHGlobal(16); try { if (Vst3.Fn<D_IP>(v, 9)(v, r) == 0) return new Size(Math.Max(100, Marshal.ReadInt32(r, 8) - Marshal.ReadInt32(r, 0)), Math.Max(60, Marshal.ReadInt32(r, 12) - Marshal.ReadInt32(r, 4))); } finally { Marshal.FreeHGlobal(r); } return new Size(640, 480); }

    // ---- parameters ----
    public string ParamName(int i) { return i >= 0 && i < paramNames.Count ? paramNames[i] : ""; }
    public float GetParam(int i) { try { if (controller == IntPtr.Zero || i < 0 || i >= paramIds.Count) return 0; lock (callLock) return (float)Vst3.Fn<D_DU>(controller, 14)(controller, paramIds[i]); } catch { return 0; } }
    public void SetParam(int i, float v)
    {
        try
        {
            if (controller == IntPtr.Zero || i < 0 || i >= paramIds.Count) return; v = Math.Max(0f, Math.Min(1f, v));
            if (System.Threading.Monitor.TryEnter(callLock)) { try { if (setParamFn == null) setParamFn = Vst3.Fn<D_IUD>(controller, 15); setParamFn(controller, paramIds[i], v); } finally { System.Threading.Monitor.Exit(callLock); } }   // (never waits: the processor below still gets the value)
            var kn = known; if (i < kn.Length) kn[i] = v;
            QueueChange(paramIds[i], v);
        }
        catch { }
    }

    // ---- saved state ----
    public byte[] GetChunk()
    {
        try { var st = new Vst3Stream(null); lock (callLock) { if (Vst3.Fn<D_IP>(component, 13)(component, st.Obj.Ptr) != 0) return null; } return st.Mem.ToArray(); } catch { return null; }
    }
    public void SetChunk(byte[] data)
    {
        if (data == null || data.Length == 0) return;
        try
        {
            lock (callLock)
            {
                var st = new Vst3Stream(data); Vst3.Fn<D_IP>(component, 12)(component, st.Obj.Ptr);
                if (controller != IntPtr.Zero) { var st2 = new Vst3Stream(data); Vst3.Fn<D_IP>(controller, 5)(controller, st2.Obj.Ptr); }
            }
        }
        catch { }
    }

    // ---- audio ----
    public void SendMidi(List<KeyValuePair<int, MidiEv>> ev) { if (ev == null || ev.Count == 0 || disposed) return; lock (pending) pending.AddRange(ev); }

    readonly List<KeyValuePair<int, MidiEv>> evWork = new List<KeyValuePair<int, MidiEv>>(); readonly List<int> evOffs = new List<int>();
    void WriteEvents(int n)
    {
        evCount = 0; lock (pending) { evWork.Clear(); evWork.AddRange(pending); pending.Clear(); }   // (reused lists: no garbage on the sound thread)
        foreach (var kv in evWork)
        {
            if (evCount >= MaxEvents - 16) break; var m = kv.Value; int st = m.S & 0xF0, ch = m.S & 0x0F;
            if (st == 0x90 && m.D2 > 0) { Emit(kv.Key, true, ch, m.D1, m.D2 / 127f); active.Add(m.D1); }
            else if (st == 0x80 || (st == 0x90 && m.D2 == 0)) { Emit(kv.Key, false, ch, m.D1, m.D2 / 127f); active.Remove(m.D1); }
            else if (m.S == 0xB0 && (m.D1 == 123 || m.D1 == 120)) { evOffs.Clear(); evOffs.AddRange(active); foreach (var note in evOffs) if (evCount < MaxEvents) Emit(kv.Key, false, 0, note, 0f); active.Clear(); CtrlChange(0, 64, 0); CtrlChange(0, 129, 0.5); }   // (stop: the pedal is let go and the pitch bend centred too)
            else if (st == 0xB0) CtrlChange(ch, m.D1, m.D2 / 127.0);                        // sustain, mod wheel, any knob on the keyboard
            else if (st == 0xE0) CtrlChange(ch, 129, ((m.D2 << 7) | m.D1) / 16383.0);       // pitch bend
            else if (st == 0xD0) CtrlChange(ch, 128, m.D1 / 127.0);                         // channel pressure
        }
    }
    void Emit(int delta, bool on, int ch, int pitch, float vel)
    {
        IntPtr e = evBuf + evCount * 48; for (int o = 0; o < 48; o += 8) Marshal.WriteInt64(e, o, 0);
        Marshal.WriteInt32(e, 0, 0); Marshal.WriteInt32(e, 4, Math.Max(0, delta)); Marshal.WriteInt16(e, 16, 1); Marshal.WriteInt16(e, 18, (short)(on ? 0 : 1));
        Marshal.WriteInt16(e, 24, (short)ch); Marshal.WriteInt16(e, 26, (short)pitch);
        if (on) { Marshal.WriteInt32(e, 28, 0); Marshal.WriteInt32(e, 32, BitConverter.ToInt32(BitConverter.GetBytes(vel), 0)); Marshal.WriteInt32(e, 36, 0); Marshal.WriteInt32(e, 40, -1); }
        else { Marshal.WriteInt32(e, 28, BitConverter.ToInt32(BitConverter.GetBytes(vel), 0)); Marshal.WriteInt32(e, 32, -1); Marshal.WriteInt32(e, 36, 0); }
        evCount++;
    }

    float[] scrL, scrR;
    public void Process(float[] l, float[] r, int n)
    {
        if (disposed) return;
        for (int off = 0; off < n; off += MaxBlock)
        {
            int c = Math.Min(MaxBlock, n - off);
            if (off == 0) WriteEvents(c); else evCount = 0;
            if (numIn > 0) { Marshal.Copy(l, off, inBuf[0], c); Marshal.Copy(r, off, inBuf[1], c); }
            // time and tempo
            double sr = sampleRate, sp = VstPlugin.HostPos + off, tempo = VstPlugin.HostBpm;
            Marshal.WriteInt32(procCtx, 0, (VstPlugin.HostPlaying ? 2 : 0) | 512 | 1024 | 8192); Marshal.WriteInt64(procCtx, 8, BitConverter.DoubleToInt64Bits(sr)); Marshal.WriteInt64(procCtx, 16, (long)sp);
            Marshal.WriteInt64(procCtx, 40, BitConverter.DoubleToInt64Bits(sp / sr * tempo / 60.0)); Marshal.WriteInt64(procCtx, 72, BitConverter.DoubleToInt64Bits(tempo)); Marshal.WriteInt32(procCtx, 80, 4); Marshal.WriteInt32(procCtx, 84, 4);
            Marshal.WriteInt32(procData, 0, 0); Marshal.WriteInt32(procData, 4, 0); Marshal.WriteInt32(procData, 8, c); Marshal.WriteInt32(procData, 12, numIn > 0 ? 1 : 0); Marshal.WriteInt32(procData, 16, numOut > 0 ? 1 : 0);
            Marshal.WriteIntPtr(procData, 24, numIn > 0 ? inBus : IntPtr.Zero); Marshal.WriteIntPtr(procData, 32, numOut > 0 ? outBus : IntPtr.Zero);
            lock (pending) { procCount = changeCount; Array.Copy(chId, pId, changeCount); Array.Copy(chVal, pVal, changeCount); changeCount = 0; }   /* (taken now: an edit from the plugin's window during process waits for the next block) */
            Marshal.WriteIntPtr(procData, 40, inChanges.Ptr);
            Marshal.WriteIntPtr(procData, 48, outChanges.Ptr); Marshal.WriteIntPtr(procData, 56, inEvents.Ptr); Marshal.WriteIntPtr(procData, 64, outEvents.Ptr); Marshal.WriteIntPtr(procData, 72, procCtx);
            Marshal.WriteInt32(outBus, 0, 2); Marshal.WriteInt64(outBus, 8, 0);
            for (int i = 0; i < 2; i++) ZeroMem(outBuf[i], c);
            processFn(processor, procData);
            RefreshLatency();
            procCount = 0;
            if (scrL == null) { scrL = new float[MaxBlock]; scrR = new float[MaxBlock]; }
            Marshal.Copy(outBuf[0], scrL, 0, c); Marshal.Copy(outBuf[1], scrR, 0, c);
            if (synth) for (int i = 0; i < c; i++) { l[off + i] += scrL[i]; r[off + i] += scrR[i]; }
            else for (int i = 0; i < c; i++) { l[off + i] = scrL[i]; r[off + i] = scrR[i]; }
        }
    }
    static readonly byte[] Zeros = new byte[4096];
    static void ZeroMem(IntPtr p, int frames) { int bytes = frames * 4; var z = Zeros; for (int o = 0; o < bytes; o += z.Length) Marshal.Copy(z, 0, p + o, Math.Min(z.Length, bytes - o)); }

    // ---- editor window ----
    public Size GetEditorSize() { return editorSize; }
    public void OpenEditor(IntPtr hwnd)
    {
        if (view != IntPtr.Zero || controller == IntPtr.Zero) return; IntPtr nm = Marshal.StringToHGlobalAnsi("editor"), t = Marshal.StringToHGlobalAnsi("HWND");
        try
        {
            view = Vst3.Fn<D_PP>(controller, 17)(controller, nm); if (view == IntPtr.Zero) throw new Exception("The plugin has no window.");
            Vst3.Fn<D_IP>(view, 12)(view, frame.Ptr);   // setFrame
            int r = Vst3.Fn<D_IPP>(view, 4)(view, hwnd, t);   // attached
            if (r != 0) { Vst3.Release(view); view = IntPtr.Zero; throw new Exception("The plugin window wouldn't open."); }
            editorSize = ReadSize(view);
        }
        finally { Marshal.FreeHGlobal(nm); Marshal.FreeHGlobal(t); }
    }
    public void CloseEditor()
    {
        if (view == IntPtr.Zero) return;
        try { Vst3.Fn<D_I>(view, 5)(view); Vst3.Fn<D_IP>(view, 12)(view, IntPtr.Zero); } catch { }
        Vst3.Release(view); view = IntPtr.Zero;
    }
    public void EditorIdle() { }   // VST 3 windows run their own timers

    public void Dispose()
    {
        if (disposed) return; disposed = true;
        try { CloseEditor(); } catch { }
        try { if (processor != IntPtr.Zero) Vst3.Fn<D_IB>(processor, 8)(processor, 0); if (component != IntPtr.Zero) Vst3.Fn<D_IB>(component, 11)(component, 0); } catch { }
        try { if (connComp != IntPtr.Zero && connCtrl != IntPtr.Zero) { Vst3.Fn<D_IP>(connComp, 4)(connComp, connCtrl); Vst3.Fn<D_IP>(connCtrl, 4)(connCtrl, connComp); } } catch { }
        try { if (controller != IntPtr.Zero && controllerSeparate) Vst3.Fn<D_I>(controller, 4)(controller); } catch { }
        try { if (component != IntPtr.Zero) Vst3.Fn<D_I>(component, 4)(component); } catch { }
        Vst3.Release(connComp); Vst3.Release(connCtrl); if (controllerSeparate) Vst3.Release(controller); Vst3.Release(processor); Vst3.Release(component); Vst3.Release(factory);
        if (lib != IntPtr.Zero) { try { IntPtr ex = exitDll ? VstNative.GetProcAddress(lib, "ExitDll") : IntPtr.Zero; if (ex != IntPtr.Zero) ((BoolEntry)Marshal.GetDelegateForFunctionPointer(ex, typeof(BoolEntry)))(); } catch { } try { VstNative.FreeLibrary(lib); } catch { } lib = IntPtr.Zero; }
    }
}

[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int D_III(IntPtr self, int a, int b);
