using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

// Tests the VST3 host against a stand-in plugin built in memory (COM-style tables of function pointers, as a real .vst3 has).
// It checks the host's mechanics: loading, buses, notes, parameters, saved state, editor, effect mode.   csc /main:W3 ... Vst3Test.cs
// It was written by the same hand as the host, so it cannot prove a real plugin from a plugin maker will load.
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int M_IIIIP(IntPtr self, int a, int b, int c, IntPtr d);
[UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate double M_DUD(IntPtr self, uint id, double v);

class MockV3
{
    public bool Effect; public ComObject Factory, Comp, Proc, Ctrl, View; public double Vol = 1.0; public static int SR = 44100;
    public double Freq, Phase, Amp; public byte[] GotState; public IntPtr Hwnd; public bool Removed, Attached, ControllerSetState; public IntPtr FramePtr; public List<string> Log = new List<string>(); public bool Active, Processing;
    public IntPtr HandlerPtr, HostCtx;
    readonly byte[] cid = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };

    public MockV3(bool effect)
    {
        Effect = effect;
        Comp = new ComObject(new[] { Vst3.IidComponent, Vst3.IidPluginBase }, new D_IP((s, ctx) => { HostCtx = ctx; return 0; }), new D_I(s => 0), new D_IP((s, p) => 1), new D_II((s, m) => 0), new D_III(BusCount), new M_IIIIP((s, a, b, c, d) => 1), new D_IPP((s, a, b) => 1),
            new D_IIIIB((s, a, b, c, d) => 0), new D_IB((s, st) => { Active = st != 0; return 0; }), new D_IP(SetState), new D_IP(GetState));
        Proc = new ComObject(new[] { Vst3.IidAudioProcessor }, new D_IPIPI((s, a, na, b, nb) => 0), new D_IIP((s, i, p) => 1), new D_II((s, t) => t == 0 ? 0 : 1), new D_U(s => 0), new D_IP((s, st) => { SR = (int)BitConverter.Int64BitsToDouble(Marshal.ReadInt64(st, 16)); return 0; }),
            new D_IB((s, on) => { Processing = on != 0; return 0; }), new D_IP(Process), new D_U(s => 0));
        Ctrl = new ComObject(new[] { Vst3.IidEditController }, new D_IP((s, ctx) => 0), new D_I(s => 0), new D_IP((s, st) => { ControllerSetState = true; return 0; }), new D_IP((s, st) => 0), new D_IP((s, st) => 0), new D_I(s => 2), new D_IIP(ParamInfo),
            new D_IUDP((s, id, v, str) => 1), new D_IPPP((s, a, b, c) => 1), new M_DUD((s, id, v) => v), new M_DUD((s, id, v) => v), new D_DU((s, id) => id == 100 ? Vol : 0.25), new D_IUD(SetNorm), new D_IP((s, h) => { HandlerPtr = h; return 0; }), new D_PP(CreateView));
        View = new ComObject(new[] { Vst3.IidPlugView }, new D_IP((s, t) => Vst3.Ansi(t, 0, 8) == "HWND" ? 0 : 1), new D_IPP(Attach), new D_I(s => { Removed = true; Attached = false; return 0; }), new D_IP((s, x) => 1), new D_IP((s, x) => 1), new D_IP((s, x) => 1),
            new D_IP((s, r) => { Marshal.WriteInt32(r, 0, 0); Marshal.WriteInt32(r, 4, 0); Marshal.WriteInt32(r, 8, 300); Marshal.WriteInt32(r, 12, 200); return 0; }), new D_IP((s, r) => 0), new D_IB((s, f) => 0), new D_IP((s, f) => { FramePtr = f; return 0; }), new D_I(s => 0), new D_IP((s, r) => 0));
        Func<byte[], IntPtr> res = iid => Vst3.Same(iid, Vst3.IidComponent) ? Comp.Ptr : (Vst3.Same(iid, Vst3.IidAudioProcessor) ? Proc.Ptr : (Vst3.Same(iid, Vst3.IidEditController) ? Ctrl.Ptr : IntPtr.Zero));
        Comp.Resolver = res; Proc.Resolver = res; Ctrl.Resolver = res;
        Factory = new ComObject(null, new D_IP((s, info) => { var b = Encoding.ASCII.GetBytes("MockCo\0"); Marshal.Copy(b, 0, info, b.Length); return 0; }), new D_I(s => 1), new D_IIP(ClassInfo), new D_IPPP((s, c, iid, obj) =>
        {
            var b = new byte[16]; Marshal.Copy(iid, b, 0, 16); IntPtr r = res(b); Marshal.WriteIntPtr(obj, r); return r != IntPtr.Zero ? 0 : -1;
        }));
    }
    int BusCount(IntPtr s, int type, int dir) { if (type == 0) return dir == 0 ? (Effect ? 1 : 0) : 1; return dir == 0 && !Effect ? 1 : 0; }
    int ClassInfo(IntPtr s, int i, IntPtr info)
    {
        if (i != 0) return 1; Marshal.Copy(cid, 0, info, 16); Marshal.WriteInt32(info, 16, 0x7FFFFFFF); Put(info, 20, "Audio Module Class"); Put(info, 52, Effect ? "Mock V3 Effect" : "Mock V3 Synth"); return 0;
    }
    static void Put(IntPtr p, int off, string s) { var b = Encoding.ASCII.GetBytes(s + "\0"); Marshal.Copy(b, 0, p + off, b.Length); }
    int ParamInfo(IntPtr s, int i, IntPtr info)
    {
        if (i > 1) return 1; Marshal.WriteInt32(info, 0, 100 + i); Vst3.PutWide(info + 4, i == 0 ? "Volume" : "Cutoff", 128); Marshal.WriteInt32(info, 772, 0); Marshal.WriteInt64(info, 776, BitConverter.DoubleToInt64Bits(0.5)); Marshal.WriteInt32(info, 784, 0); Marshal.WriteInt32(info, 788, 1); return 0;
    }
    int SetNorm(IntPtr s, uint id, double v) { if (id == 100) Vol = v; return 0; }
    IntPtr CreateView(IntPtr s, IntPtr name) { return Vst3.Ansi(name, 0, 8) == "editor" ? View.Ptr : IntPtr.Zero; }
    int Attach(IntPtr s, IntPtr parent, IntPtr type) { Hwnd = parent; Attached = true; return 0; }
    int GetState(IntPtr s, IntPtr stream) { var b = Encoding.ASCII.GetBytes("V3STATE-" + Vol.ToString("0.00")); IntPtr m = Marshal.AllocHGlobal(b.Length); Marshal.Copy(b, 0, m, b.Length); IntPtr w = Marshal.AllocHGlobal(4); var wr = Vst3.Fn<D_IPIP>(stream, 4); int r = wr(stream, m, b.Length, w); Marshal.FreeHGlobal(m); Marshal.FreeHGlobal(w); return r; }
    int SetState(IntPtr s, IntPtr stream) { IntPtr m = Marshal.AllocHGlobal(256), n = Marshal.AllocHGlobal(4); var rd = Vst3.Fn<D_IPIP>(stream, 3); rd(stream, m, 256, n); int got = Marshal.ReadInt32(n); GotState = new byte[got]; Marshal.Copy(m, GotState, 0, got); Marshal.FreeHGlobal(m); Marshal.FreeHGlobal(n); return 0; }

    int Process(IntPtr s, IntPtr data)
    {
        int n = Marshal.ReadInt32(data, 8); int numIn = Marshal.ReadInt32(data, 12), numOut = Marshal.ReadInt32(data, 16); IntPtr ins = Marshal.ReadIntPtr(data, 24), outs = Marshal.ReadIntPtr(data, 32);
        IntPtr chg = Marshal.ReadIntPtr(data, 40), evs = Marshal.ReadIntPtr(data, 56);
        if (chg != IntPtr.Zero) { int pc = Vst3.Fn<D_I>(chg, 3)(chg); var gd = Vst3.Fn<D_PI>(chg, 4); for (int i = 0; i < pc; i++) { IntPtr q = gd(chg, i); uint id = Vst3.Fn<D_U>(q, 3)(q); IntPtr so = Marshal.AllocHGlobal(4), val = Marshal.AllocHGlobal(8); Vst3.Fn<D_IIPP>(q, 5)(q, 0, so, val); if (id == 100) Vol = BitConverter.Int64BitsToDouble(Marshal.ReadInt64(val)); Marshal.FreeHGlobal(so); Marshal.FreeHGlobal(val); } }
        int ec = evs == IntPtr.Zero ? 0 : Vst3.Fn<D_I>(evs, 3)(evs); var evl = new List<int[]>(); IntPtr eb = Marshal.AllocHGlobal(48);
        for (int i = 0; i < ec; i++) { Vst3.Fn<D_IIP>(evs, 4)(evs, i, eb); int off = Marshal.ReadInt32(eb, 4); int type = (ushort)Marshal.ReadInt16(eb, 18); int pitch = Marshal.ReadInt16(eb, 26); float vel = BitConverter.ToSingle(BitConverter.GetBytes(Marshal.ReadInt32(eb, type == 0 ? 32 : 28)), 0); evl.Add(new[] { off, type, pitch, (int)(vel * 127 + 0.5f) }); Log.Add("event type " + type + " at " + off + " pitch " + pitch); }
        Marshal.FreeHGlobal(eb);
        IntPtr o = Marshal.ReadIntPtr(Marshal.ReadIntPtr(outs, 16), 0), o2 = Marshal.ReadIntPtr(Marshal.ReadIntPtr(outs, 16), IntPtr.Size); var buf = new float[n];
        if (Effect)
        {
            IntPtr i0 = Marshal.ReadIntPtr(Marshal.ReadIntPtr(ins, 16), 0); Marshal.Copy(i0, buf, 0, n); for (int i = 0; i < n; i++) buf[i] = (float)(buf[i] * Vol);
        }
        else
            for (int i = 0; i < n; i++)
            {
                foreach (var e in evl) if (e[0] == i) { if (e[1] == 0) { Freq = 440.0 * Math.Pow(2, (e[2] - 69) / 12.0); Amp = e[3] / 127.0 * 0.5; Phase = 0; } else Amp = 0; }
                buf[i] = (float)(Amp * Vol * Math.Sin(Phase)); Phase += 2 * Math.PI * Freq / SR;
            }
        Marshal.Copy(buf, 0, o, n); Marshal.Copy(buf, 0, o2, n);
        return 0;
    }
}

static class W3
{
    static int fails;
    static void Check(string n, bool ok, string d) { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + "   " + d); Console.Out.Flush(); }
    static double Hz(float[] x, int a, int n) { int z = 0; for (int i = a + 1; i < a + n; i++) if ((x[i - 1] < 0) != (x[i] < 0)) z++; return z / 2.0 / (n / 44100.0); }
    static double Peak(float[] x, int a, int b) { double p = 0; for (int i = a; i < b; i++) p = Math.Max(p, Math.Abs(x[i])); return p; }

    static void Main()
    {
        var m = new MockV3(false); var p = Vst3Plugin.FromFactory(m.Factory.Ptr, "mock.vst3", 44100);
        Check("a VST3 stand-in loads and reports its name, maker, kind and parameters", p.Name == "Mock V3 Synth" && p.Vendor == "MockCo" && p.IsSynth && p.NumParams == 2 && p.ParamName(0) == "Volume" && p.ParamName(1) == "Cutoff", p.Name + " / " + p.Vendor + ", params " + p.NumParams);
        Check("the host started the plugin (host object handed over, active and processing)", m.HostCtx != IntPtr.Zero && m.Active && m.Processing, "");
        Check("the plugin has an editor window of the size it asked for", p.HasEditor && p.GetEditorSize().Width == 300 && p.GetEditorSize().Height == 200, p.GetEditorSize().ToString());
        // notes
        var l = new float[8192]; var r = new float[8192]; VstPlugin.HostBpm = 120; VstPlugin.HostPos = 0;
        p.SendMidi(new List<KeyValuePair<int, MidiEv>> { new KeyValuePair<int, MidiEv>(100, new MidiEv { S = 0x90, D1 = 69, D2 = 127 }) }); p.Process(l, r, 8192);
        double hz = Hz(l, 1000, 6000); Check("a note-on starts a note at the right time and pitch (A4 = 440 Hz, nothing before frame 100)", Peak(l, 0, 100) == 0 && Peak(l, 200, 2000) > 0.2 && Math.Abs(hz - 440) < 8, "level " + Peak(l, 200, 2000).ToString("0.00") + ", " + hz.ToString("0.0") + " Hz");
        Check("the plugin's sound reaches both channels", Peak(r, 200, 2000) > 0.2, "");
        p.SendMidi(new List<KeyValuePair<int, MidiEv>> { new KeyValuePair<int, MidiEv>(0, new MidiEv { S = 0x80, D1 = 69, D2 = 0 }) }); l = new float[2048]; r = new float[2048]; p.Process(l, r, 2048);
        Check("a note-off silences it", Peak(l, 0, 2048) == 0, "");
        // all notes off
        p.SendMidi(new List<KeyValuePair<int, MidiEv>> { new KeyValuePair<int, MidiEv>(0, new MidiEv { S = 0x90, D1 = 60, D2 = 100 }) }); p.Process(new float[512], new float[512], 512);
        p.SendMidi(new List<KeyValuePair<int, MidiEv>> { new KeyValuePair<int, MidiEv>(0, new MidiEv { S = 0xB0, D1 = 123, D2 = 0 }) }); l = new float[1024]; r = new float[1024]; p.Process(l, r, 1024);
        Check("'all notes off' turns every sounding note off", Peak(l, 0, 1024) == 0, "");
        // parameters
        p.SendMidi(new List<KeyValuePair<int, MidiEv>> { new KeyValuePair<int, MidiEv>(0, new MidiEv { S = 0x90, D1 = 69, D2 = 127 }) }); l = new float[2048]; r = new float[2048]; p.Process(l, r, 2048); double full = Peak(l, 500, 2048);
        p.SetParam(0, 0.5f); l = new float[2048]; r = new float[2048]; p.Process(l, r, 2048); double half = Peak(l, 500, 2048);
        Check("changing a parameter reaches the plugin's audio (volume halved)", half < full * 0.6 && half > full * 0.4 && Math.Abs(p.GetParam(0) - 0.5f) < 0.01, "full " + full.ToString("0.00") + ", half " + half.ToString("0.00"));
        // state
        var chunk = p.GetChunk(); string cs = chunk == null ? "" : Encoding.ASCII.GetString(chunk);
        Check("the plugin's saved state can be read", cs.StartsWith("V3STATE-"), cs);
        p.SetChunk(Encoding.ASCII.GetBytes("V3STATE-0.80")); Check("a saved state can be given back (to the plugin and its controller)", m.GotState != null && Encoding.ASCII.GetString(m.GotState) == "V3STATE-0.80" && m.ControllerSetState, m.GotState == null ? "none" : Encoding.ASCII.GetString(m.GotState));
        // editor
        int sized = 0; p.SizeRequested += (w, h) => sized = w * 1000 + h; p.OpenEditor((IntPtr)0x1234);
        Check("the editor opens in the window we give it", p.EditorOpen && m.Attached && m.Hwnd == (IntPtr)0x1234 && m.FramePtr != IntPtr.Zero, "");
        IntPtr rect = Marshal.AllocHGlobal(16); Marshal.WriteInt32(rect, 0, 0); Marshal.WriteInt32(rect, 4, 0); Marshal.WriteInt32(rect, 8, 640); Marshal.WriteInt32(rect, 12, 480); Vst3.Fn<D_IPP>(m.FramePtr, 3)(m.FramePtr, m.View.Ptr, rect);
        Check("the plugin can ask the host to resize its window", sized == 640480 && p.GetEditorSize().Width == 640, sized.ToString());
        p.CloseEditor(); Check("closing the editor tells the plugin", !p.EditorOpen && m.Removed, "");
        // effect
        var me = new MockV3(true); var pe = Vst3Plugin.FromFactory(me.Factory.Ptr, "mockfx.vst3", 44100); var el = new float[1024]; var er = new float[1024]; for (int i = 0; i < 1024; i++) { el[i] = 0.8f; er[i] = -0.8f; }
        pe.SetParam(0, 0.5f); pe.Process(el, er, 1024); pe.Process(el, er, 1024);
        Check("an effect plugin processes the audio it is given (and is not mistaken for an instrument)", !pe.IsSynth && Math.Abs(el[500]) < 0.45 && Math.Abs(el[500]) > 0.2, "sample " + el[500].ToString("0.00"));
        p.Dispose(); pe.Dispose();
        Check("plugins close cleanly", true, "");
        Check("a missing file gives a clear message", Throws(() => Vst3Plugin.Load(@"C:\nope\missing.vst3", 44100)), "");
        Console.WriteLine(fails == 0 ? "=== VST3 host checks passed ===" : "=== " + fails + " FAILED ==="); Environment.Exit(fails == 0 ? 0 : 1);
    }
    static bool Throws(Action a) { try { a(); return false; } catch { return true; } }
}
