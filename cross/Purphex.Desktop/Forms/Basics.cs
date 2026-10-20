// Purphex Forms (macOS / Linux): the part of System.Windows.Forms the app uses, on Avalonia. Names and behaviour follow WinForms,
// so the studio's own code runs unchanged. Enums, event data and small helpers live here.
using System;
using System.Collections.Generic;
using System.Drawing;

namespace System.Windows.Forms
{
    [Flags] public enum Keys
    {
        None = 0, KeyCode = 0xFFFF, Modifiers = unchecked((int)0xFFFF0000), Shift = 0x10000, Control = 0x20000, Alt = 0x40000,
        LButton = 1, RButton = 2, Cancel = 3, MButton = 4, Back = 8, Tab = 9, Enter = 13, Return = 13, ShiftKey = 16, ControlKey = 17, Menu = 18, Pause = 19, CapsLock = 20, Escape = 27, Space = 32, PageUp = 33, Prior = 33, PageDown = 34, Next = 34, End = 35, Home = 36, Left = 37, Up = 38, Right = 39, Down = 40, Insert = 45, Delete = 46,
        D0 = 48, D1, D2, D3, D4, D5, D6, D7, D8, D9, A = 65, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z, LWin = 91, RWin = 92, Apps = 93,
        NumPad0 = 96, NumPad1, NumPad2, NumPad3, NumPad4, NumPad5, NumPad6, NumPad7, NumPad8, NumPad9, Multiply = 106, Add = 107, Separator = 108, Subtract = 109, Decimal = 110, Divide = 111,
        F1 = 112, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12, F13, F14, F15, F16, F17, F18, F19, F20, F21, F22, F23, F24, NumLock = 144, Scroll = 145, LShiftKey = 160, RShiftKey = 161, LControlKey = 162, RControlKey = 163, LMenu = 164, RMenu = 165,
        OemSemicolon = 186, Oemplus = 187, Oemcomma = 188, OemMinus = 189, OemPeriod = 190, OemQuestion = 191, Oemtilde = 192, OemOpenBrackets = 219, OemPipe = 220, OemCloseBrackets = 221, OemQuotes = 222
    }
    [Flags] public enum MouseButtons { None = 0, Left = 0x100000, Right = 0x200000, Middle = 0x400000, XButton1 = 0x800000, XButton2 = 0x1000000 }
    public enum DialogResult { None, OK, Cancel, Abort, Retry, Ignore, Yes, No }
    public enum FormBorderStyle { None, FixedSingle, Fixed3D, FixedDialog, Sizable, FixedToolWindow, SizableToolWindow }
    public enum FormStartPosition { Manual, CenterScreen, WindowsDefaultLocation, WindowsDefaultBounds, CenterParent }
    public enum FormWindowState { Normal, Minimized, Maximized }
    public enum CloseReason { None, WindowsShutDown, MdiFormClosing, UserClosing, TaskManagerClosing, FormOwnerClosing, ApplicationExitCall }
    [Flags] public enum AnchorStyles { None = 0, Top = 1, Bottom = 2, Left = 4, Right = 8 }
    public enum DockStyle { None, Top, Bottom, Left, Right, Fill }
    public enum ScrollBars { None, Horizontal, Vertical, Both }
    public enum BorderStyle { None, FixedSingle, Fixed3D }
    public enum ComboBoxStyle { Simple, DropDown, DropDownList }
    public enum MessageBoxButtons { OK, OKCancel, AbortRetryIgnore, YesNoCancel, YesNo, RetryCancel }
    public enum MessageBoxIcon { None = 0, Hand = 16, Error = 16, Stop = 16, Question = 32, Exclamation = 48, Warning = 48, Asterisk = 64, Information = 64 }
    public enum MessageBoxDefaultButton { Button1 = 0, Button2 = 256, Button3 = 512 }
    public enum DragDropEffects { None = 0, Copy = 1, Move = 2, Link = 4, Scroll = unchecked((int)0x80000000), All = -2147483645 }
    [Flags] public enum BoundsSpecified { None = 0, X = 1, Y = 2, Location = 3, Width = 4, Height = 8, Size = 12, All = 15 }
    public enum FlatStyle { Flat, Popup, Standard, System }
    public enum HorizontalAlignment { Left, Right, Center }
    public enum ToolStripItemDisplayStyle { None, Text, Image, ImageAndText }
    public enum TickStyle { None, TopLeft, BottomRight, Both }
    public enum Orientation { Horizontal, Vertical }
    public enum DrawMode { Normal, OwnerDrawFixed, OwnerDrawVariable }
    [Flags] public enum DrawItemState { None = 0, Selected = 1, Focus = 16 }
    public enum ImeMode { NoControl, On, Off, Disable }
    public enum RightToLeft { No, Yes, Inherit }

    public class EventArgs2 : EventArgs { }
    public class MouseEventArgs : EventArgs
    {
        public MouseButtons Button { get; } public int Clicks { get; } public int X { get; } public int Y { get; } public int Delta { get; }
        public Point Location { get { return new Point(X, Y); } }
        public MouseEventArgs(MouseButtons b, int clicks, int x, int y, int delta) { Button = b; Clicks = clicks; X = x; Y = y; Delta = delta; }
    }
    public class HandledMouseEventArgs : MouseEventArgs { public bool Handled { get; set; } public HandledMouseEventArgs(MouseButtons b, int c, int x, int y, int d) : base(b, c, x, y, d) { } }
    public class KeyEventArgs : EventArgs
    {
        public Keys KeyData { get; } public Keys KeyCode { get { return KeyData & Keys.KeyCode; } } public Keys Modifiers { get { return KeyData & Keys.Modifiers; } }
        public bool Control { get { return (KeyData & Keys.Control) != 0; } } public bool Shift { get { return (KeyData & Keys.Shift) != 0; } } public bool Alt { get { return (KeyData & Keys.Alt) != 0; } }
        public int KeyValue { get { return (int)KeyCode; } } public bool Handled { get; set; } public bool SuppressKeyPress { get; set; }
        public KeyEventArgs(Keys k) { KeyData = k; }
    }
    public class KeyPressEventArgs : EventArgs { public char KeyChar { get; set; } public bool Handled { get; set; } public KeyPressEventArgs(char c) { KeyChar = c; } }
    public class PaintEventArgs : EventArgs, IDisposable { public Graphics Graphics { get; } public Rectangle ClipRectangle { get; } public PaintEventArgs(Graphics g, Rectangle clip) { Graphics = g; ClipRectangle = clip; } public void Dispose() { } }
    public class FormClosingEventArgs : System.ComponentModel.CancelEventArgs { public CloseReason CloseReason { get; } public FormClosingEventArgs(CloseReason r, bool cancel) : base(cancel) { CloseReason = r; } }
    public class FormClosedEventArgs : EventArgs { public CloseReason CloseReason { get; } public FormClosedEventArgs(CloseReason r) { CloseReason = r; } }
    public class DragEventArgs : EventArgs { public IDataObject Data { get; } public int X { get; } public int Y { get; } public DragDropEffects Effect { get; set; } public DragDropEffects AllowedEffect { get; } public int KeyState { get; } public DragEventArgs(IDataObject d, int ks, int x, int y, DragDropEffects allowed, DragDropEffects eff) { Data = d; KeyState = ks; X = x; Y = y; AllowedEffect = allowed; Effect = eff; } }
    public class DrawItemEventArgs : EventArgs { public Graphics Graphics { get; } public Font Font { get; } public Rectangle Bounds { get; } public int Index { get; } public DrawItemState State { get; } public Color ForeColor { get; } public Color BackColor { get; } public DrawItemEventArgs(Graphics g, Font f, Rectangle r, int i, DrawItemState s) { Graphics = g; Font = f; Bounds = r; Index = i; State = s; ForeColor = Color.White; BackColor = Color.Black; } public void DrawBackground() { } public void DrawFocusRectangle() { } }
    public class MeasureItemEventArgs : EventArgs { public int ItemHeight { get; set; } public int ItemWidth { get; set; } public int Index { get; } public MeasureItemEventArgs(int i) { Index = i; } }
    public class PreviewKeyDownEventArgs : EventArgs { public Keys KeyData { get; } public bool IsInputKey { get; set; } public PreviewKeyDownEventArgs(Keys k) { KeyData = k; } }
    public delegate void MouseEventHandler(object sender, MouseEventArgs e);
    public delegate void KeyEventHandler(object sender, KeyEventArgs e);
    public delegate void KeyPressEventHandler(object sender, KeyPressEventArgs e);
    public delegate void PaintEventHandler(object sender, PaintEventArgs e);
    public delegate void FormClosingEventHandler(object sender, FormClosingEventArgs e);
    public delegate void FormClosedEventHandler(object sender, FormClosedEventArgs e);
    public delegate void DragEventHandler(object sender, DragEventArgs e);
    public delegate void DrawItemEventHandler(object sender, DrawItemEventArgs e);
    public delegate void MeasureItemEventHandler(object sender, MeasureItemEventArgs e);
    public delegate void PreviewKeyDownEventHandler(object sender, PreviewKeyDownEventArgs e);

    public struct Message { public IntPtr HWnd; public int Msg; public IntPtr WParam; public IntPtr LParam; public IntPtr Result; public static Message Create(IntPtr h, int m, IntPtr w, IntPtr l) { return new Message { HWnd = h, Msg = m, WParam = w, LParam = l }; } }
    public sealed class CreateParams { public int Style, ExStyle, ClassStyle; public string Caption; public IntPtr Parent; public int X, Y, Width, Height; }
    public interface IMessageFilter { bool PreFilterMessage(ref Message m); }
    public class NativeWindow { public IntPtr Handle { get { return IntPtr.Zero; } } public void AssignHandle(IntPtr h) { } public void ReleaseHandle() { } protected virtual void WndProc(ref Message m) { } public void DefWndProc(ref Message m) { } }
    public struct Padding { public int Left, Top, Right, Bottom; public Padding(int all) { Left = Top = Right = Bottom = all; } public Padding(int l, int t, int r, int b) { Left = l; Top = t; Right = r; Bottom = b; } public int All { get { return Left; } set { Left = Top = Right = Bottom = value; } } public int Horizontal { get { return Left + Right; } } public int Vertical { get { return Top + Bottom; } } public static readonly Padding Empty = new Padding(0); }

    public interface IDataObject { object GetData(string format); object GetData(Type t); bool GetDataPresent(string format); bool GetDataPresent(Type t); string[] GetFormats(); void SetData(string format, object data); }
    public sealed class DataObject : IDataObject
    {
        readonly Dictionary<string, object> d = new Dictionary<string, object>(); public DataObject() { } public DataObject(string format, object data) { d[format] = data; } public DataObject(object data) { if (data != null) d[data.GetType().FullName] = data; }
        public object GetData(string f) { object v; return d.TryGetValue(f, out v) ? v : null; } public object GetData(Type t) { return GetData(t.FullName); } public bool GetDataPresent(string f) { return d.ContainsKey(f); } public bool GetDataPresent(Type t) { return d.ContainsKey(t.FullName); } public string[] GetFormats() { return new List<string>(d.Keys).ToArray(); } public void SetData(string f, object data) { d[f] = data; }
    }
    public static class DataFormats { public const string FileDrop = "FileDrop", Text = "Text", UnicodeText = "UnicodeText", Html = "HTML Format"; }

    public sealed class Cursor : IDisposable { internal readonly string Kind; internal Cursor(string k) { Kind = k; } public static Point Position { get { return Control.MousePosition; } set { } } public static Cursor Current { get; set; } public void Dispose() { } }
    public static class Cursors { public static readonly Cursor Default = new Cursor("arrow"), Arrow = Default, Hand = new Cursor("hand"), IBeam = new Cursor("ibeam"), WaitCursor = new Cursor("wait"), SizeWE = new Cursor("sizewe"), SizeNS = new Cursor("sizens"), SizeAll = new Cursor("sizeall"), SizeNWSE = new Cursor("sizenwse"), SizeNESW = new Cursor("sizenesw"), Cross = new Cursor("cross"), No = new Cursor("no"), AppStarting = WaitCursor, HSplit = SizeNS, VSplit = SizeWE; }
    public static class SystemInformation
    {
        public static bool UIEffectsEnabled { get { return true; } } public static int DoubleClickTime { get { return 500; } } public static Size DoubleClickSize { get { return new Size(4, 4); } }
        public static Rectangle VirtualScreen { get { return Screen.PrimaryScreen.Bounds; } } public static Rectangle WorkingArea { get { return Screen.PrimaryScreen.WorkingArea; } }
        public static int MouseWheelScrollLines { get { return 3; } } public static Size PrimaryMonitorSize { get { return Screen.PrimaryScreen.Bounds.Size; } } public static int VerticalScrollBarWidth { get { return 14; } } public static bool TerminalServerSession { get { return false; } }
    }
    public sealed class Screen
    {
        public Rectangle Bounds { get; internal set; } public Rectangle WorkingArea { get; internal set; } public bool Primary { get; internal set; } public string DeviceName { get { return "screen"; } }
        public static Func<Screen[]> Provider;
        public static Screen[] AllScreens { get { var s = Provider != null ? Provider() : null; return s != null && s.Length > 0 ? s : new[] { new Screen { Bounds = new Rectangle(0, 0, 1920, 1080), WorkingArea = new Rectangle(0, 0, 1920, 1040), Primary = true } }; } }
        public static Screen PrimaryScreen { get { foreach (var s in AllScreens) if (s.Primary) return s; return AllScreens[0]; } }
        public static Screen FromControl(Control c) { return FromPoint(c == null ? Point.Empty : c.Location); }
        public static Screen FromPoint(Point p) { foreach (var s in AllScreens) if (s.Bounds.Contains(p)) return s; return PrimaryScreen; }
        public static Screen FromRectangle(Rectangle r) { return FromPoint(new Point(r.X + r.Width / 2, r.Y + r.Height / 2)); }
        public static Screen FromHandle(IntPtr h) { return PrimaryScreen; }
    }
    public static class Clipboard
    {
        public static Func<string> GetTextImpl; public static Action<string> SetTextImpl;
        public static string GetText() { return GetTextImpl != null ? GetTextImpl() ?? "" : ""; } public static bool ContainsText() { return GetText().Length > 0; }
        public static void SetText(string s) { if (SetTextImpl != null) SetTextImpl(s); } public static void Clear() { SetText(""); }
        public static void SetDataObject(object d) { SetText(d == null ? "" : d.ToString()); } public static void SetDataObject(object d, bool copy) { SetDataObject(d); }
        public static System.Collections.Specialized.StringCollection GetFileDropList() { return new System.Collections.Specialized.StringCollection(); } public static bool ContainsFileDropList() { return false; }
        public static bool ContainsImage() { return false; } public static Image GetImage() { return null; } public static void SetImage(Image i) { }
    }
    public sealed class ToolTip : IDisposable
    {
        public int InitialDelay { get; set; } public int AutoPopDelay { get; set; } public int ReshowDelay { get; set; } public bool ShowAlways { get; set; } public bool Active { get; set; } = true; public bool IsBalloon { get; set; } public Color BackColor { get; set; } public Color ForeColor { get; set; } public bool OwnerDraw { get; set; } public string ToolTipTitle { get; set; }
        public event PopupEventHandler Popup; public event DrawToolTipEventHandler Draw; public bool UseAnimation { get; set; } public bool UseFading { get; set; }
        readonly Dictionary<Control, string> tips = new Dictionary<Control, string>();
        public void SetToolTip(Control c, string text) { tips[c] = text; if (c != null) c.TipText = text; } public string GetToolTip(Control c) { string t; return tips.TryGetValue(c, out t) ? t : ""; }
        public void Show(string text, IWin32Window w) { } public void Show(string text, IWin32Window w, int x, int y) { } public void Show(string text, IWin32Window w, int x, int y, int dur) { } public void Show(string text, IWin32Window w, Point p) { } public void Show(string text, IWin32Window w, Point p, int dur) { } public void Hide(IWin32Window w) { } public void RemoveAll() { tips.Clear(); }
        public void Dispose() { }
    }
}
