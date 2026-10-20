// Purphex Forms: Control and Form on Avalonia. A Control is a WinForms-style object; on screen it is an Avalonia element:
// custom-drawn controls (the studio, plugin windows...) paint through Purphex.Gfx on Skia inside PaintSurface, and standard controls
// (TextBox, Button...) use Avalonia's own. A Form is an Avalonia window holding its controls.
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using AC = Avalonia.Controls;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;
using Color = System.Drawing.Color;
using PointF = System.Drawing.PointF;
using Rectangle = System.Drawing.Rectangle;

namespace System.Windows.Forms
{
    // the Skia drawing surface of a custom-drawn control: Avalonia asks it to render, it hands a Purphex.Gfx Graphics to the control's OnPaint
    sealed class PaintSurface : AC.Control
    {
        readonly Control owner; public PaintSurface(Control o) { owner = o; ClipToBounds = true; Focusable = true; }
        sealed class Op : ICustomDrawOperation
        {
            readonly Control owner; public Rect Bounds { get; } public Op(Control o, Rect b) { owner = o; Bounds = b; }
            public void Dispose() { } public bool HitTest(Avalonia.Point p) { return Bounds.Contains(p); } public bool Equals(ICustomDrawOperation other) { return false; }
            public void Render(ImmediateDrawingContext context)
            {
                var lf = context.TryGetFeature<ISkiaSharpApiLeaseFeature>(); if (lf == null) return;
                using (var lease = lf.Lease()) { var canvas = lease.SkCanvas; int save = canvas.Save(); try { using (var g = new Graphics(canvas)) owner.RaisePaint(g); } catch (Exception e) { Application.ReportError(e); } finally { canvas.RestoreToCount(save); } }
            }
        }
        public override void Render(DrawingContext context) { context.Custom(new Op(owner, new Rect(0, 0, Bounds.Width, Bounds.Height))); }
    }

    public class ControlCollection : IEnumerable<Control>
    {
        readonly Control owner; readonly List<Control> list = new List<Control>(); internal ControlCollection(Control o) { owner = o; }
        public void Add(Control c) { if (c == null || list.Contains(c)) return; if (c.Parent != null) c.Parent.Controls.Remove(c); list.Add(c); c.parent = owner; owner.AttachChild(c); c.RaiseParentChanged(); c.NoteAnchor(); owner.DoLayout(); }
        public void AddRange(Control[] cs) { foreach (var c in cs) Add(c); }
        public void Remove(Control c) { if (c == null || !list.Remove(c)) return; owner.DetachChild(c); c.parent = null; c.RaiseParentChanged(); owner.DoLayout(); }
        public void Clear() { foreach (var c in list.ToArray()) Remove(c); } public bool Contains(Control c) { return list.Contains(c); }
        public int Count { get { return list.Count; } } public Control this[int i] { get { return list[i]; } } public int IndexOf(Control c) { return list.IndexOf(c); } public void SetChildIndex(Control c, int i) { if (!list.Remove(c)) return; list.Insert(Math.Max(0, Math.Min(list.Count, i)), c); owner.ReorderKids(); owner.DoLayout(); } public int GetChildIndex(Control c) { return list.IndexOf(c); }
        internal List<Control> List { get { return list; } }
        public IEnumerator<Control> GetEnumerator() { return list.GetEnumerator(); } IEnumerator IEnumerable.GetEnumerator() { return list.GetEnumerator(); }
    }

    public class Control : Component, IWin32Window
    {
        internal Control parent; internal AC.Control native; AC.Canvas kids; AC.Panel root; internal string TipText;
        int x, y, w = 100, h = 30; bool visible = true, enabled = true; string text = ""; Font font; Color back = Color.Empty, fore = Color.Empty; Cursor cursor;
        public Control() { Controls = new ControlCollection(this); }
        public ControlCollection Controls { get; }
        public event EventHandler ParentChanged; protected virtual void OnParentChanged(EventArgs e) { if (ParentChanged != null) ParentChanged(this, e); }
        public bool HasChildren { get { return Controls.Count > 0; } }
        public static Control FromChildHandle(IntPtr h) { return null; } public static Control FromHandle(IntPtr h) { return null; } internal void RaiseParentChanged() { OnParentChanged(EventArgs.Empty); }
        public bool Contains(Control c) { for (var p = c == null ? null : c.Parent; p != null; p = p.Parent) if (p == this) return true; return false; }
        protected bool ShowFocusCues { get { return true; } }
        protected Size SizeFromClientSize(Size s) { return s; }
        public DragDropEffects DoDragDrop(object data, DragDropEffects allowed) { return DragDropEffects.None; }
        public Control Parent { get { return parent; } set { if (value == null) { if (parent != null) parent.Controls.Remove(this); } else value.Controls.Add(this); } }
        public string Name { get; set; } = ""; public object Tag { get; set; } public bool TabStop { get; set; } public int TabIndex { get; set; } public bool CausesValidation { get; set; } public AnchorStyles Anchor { get { return anchor; } set { anchor = value; NoteAnchor(); } } AnchorStyles anchor = AnchorStyles.Top | AnchorStyles.Left;
        public DockStyle Dock { get { return dock; } set { if (dock == value) return; dock = value; if (parent != null) parent.DoLayout(); } } DockStyle dock; public Padding Padding { get { return padding; } set { padding = value; DoLayout(); } } Padding padding; public Padding Margin { get; set; } public bool AllowDrop { get; set; } public bool AutoSize { get; set; } public Size MinimumSize { get; set; } public Size MaximumSize { get; set; } public ContextMenuStrip ContextMenuStrip { get; set; } public RightToLeft RightToLeft { get; set; } public ImeMode ImeMode { get; set; } public Region Region { get; set; } public string AccessibleName { get; set; } public string AccessibleDescription { get; set; }
        protected bool DoubleBuffered { get; set; } protected bool ResizeRedraw { get; set; }
        protected void SetStyle(ControlStyles s, bool on) { } protected void UpdateStyles() { }
        public virtual string Text { get { return text; } set { text = value ?? ""; OnTextChanged(EventArgs.Empty); NativeText(text); } }
        public virtual Font Font { get { return font ?? (parent != null ? parent.Font : Defaults.Font); } set { font = value; NativeFont(); Invalidate(); } }
        public virtual Color BackColor { get { return back.IsEmpty ? (parent != null ? parent.BackColor : Color.FromArgb(255, 18, 10, 34)) : back; } set { back = value; NativeColors(); Invalidate(); } }
        public virtual Color ForeColor { get { return fore.IsEmpty ? (parent != null ? parent.ForeColor : Color.White) : fore; } set { fore = value; NativeColors(); Invalidate(); } }
        public virtual Cursor Cursor { get { return cursor ?? Cursors.Default; } set { cursor = value; if (native != null) native.Cursor = Map.Cur(value); } }
        public bool Visible { get { return visible; } set { if (visible == value) return; visible = value; if (native != null) native.IsVisible = value; OnVisibleChanged(EventArgs.Empty); } }
        public bool Enabled { get { return enabled; } set { if (enabled == value) return; enabled = value; if (native != null) native.IsEnabled = value; OnEnabledChanged(EventArgs.Empty); } }
        public int Left { get { return x; } set { SetBounds(value, y, w, h); } } public int Top { get { return y; } set { SetBounds(x, value, w, h); } }
        public int Width { get { return w; } set { SetBounds(x, y, value, h); } } public int Height { get { return h; } set { SetBounds(x, y, w, value); } }
        public int Right { get { return x + w; } } public int Bottom { get { return y + h; } }
        public Point Location { get { return new Point(x, y); } set { SetBounds(value.X, value.Y, w, h); } }
        public Size Size { get { return new Size(w, h); } set { SetBounds(x, y, value.Width, value.Height); } }
        public Rectangle Bounds { get { return new Rectangle(x, y, w, h); } set { SetBounds(value.X, value.Y, value.Width, value.Height); } }
        public virtual Size ClientSize { get { return new Size(w, h); } set { SetBounds(x, y, value.Width, value.Height); } }
        public Rectangle ClientRectangle { get { var s = ClientSize; return new Rectangle(0, 0, s.Width, s.Height); } } public Rectangle DisplayRectangle { get { return ClientRectangle; } }
        public void SetBounds(int nx, int ny, int nw, int nh) { SetBoundsCore(nx, ny, nw, nh, BoundsSpecified.All); }
        protected virtual void SetBoundsCore(int nx, int ny, int nw, int nh, BoundsSpecified s) { bool moved = nx != x || ny != y, sized = nw != w || nh != h; x = nx; y = ny; w = Math.Max(0, nw); h = Math.Max(0, nh); Place(); if (!laying) NoteAnchor(); if (sized) DoLayout(); if (moved) OnMove(EventArgs.Empty); if (moved) OnLocationChanged(EventArgs.Empty); if (sized) { OnSizeChanged(EventArgs.Empty); OnResize(EventArgs.Empty); OnClientSizeChanged(EventArgs.Empty); } }
        public void SetBounds(int nx, int ny, int nw, int nh, BoundsSpecified s) { SetBoundsCore(nx, ny, nw, nh, s); }
        // the window was resized or moved by the user: take its size without pushing it back
        internal void SetBoundsFromWindow(int nw, int nh) { if (nw == w && nh == h) return; w = nw; h = nh; if (root != null) { native.Width = w; native.Height = h; } DoLayout(); OnSizeChanged(EventArgs.Empty); OnResize(EventArgs.Empty); OnClientSizeChanged(EventArgs.Empty); Invalidate(); }
        internal void MoveFromWindow(int nx, int ny) { if (nx == x && ny == y) return; x = nx; y = ny; OnMove(EventArgs.Empty); OnLocationChanged(EventArgs.Empty); }
        public IntPtr Handle { get { CreateHandleIfNeeded(); return new IntPtr(GetHashCode() | 1); } } public bool IsHandleCreated { get { return native != null; } } public bool IsDisposed { get; private set; } public bool Disposing { get; private set; }
        public bool Created { get { return native != null; } } public bool Focused { get { return native != null && native.IsFocused; } } public bool ContainsFocus { get { return Focused; } } public bool CanFocus { get { return visible && enabled; } } public bool Capture { get; set; }
        public static Point MousePosition { get; internal set; } public static MouseButtons MouseButtons { get; internal set; } public static Keys ModifierKeys { get; internal set; }
        public bool InvokeRequired { get { return !Dispatcher.UIThread.CheckAccess(); } }
        public IAsyncResult BeginInvoke(Delegate d, params object[] args) { Dispatcher.UIThread.Post(() => { try { d.DynamicInvoke(args); } catch (Exception e) { Application.ReportError(e.InnerException ?? e); } }); return null; }
        public IAsyncResult BeginInvoke(Action a) { Dispatcher.UIThread.Post(() => { try { a(); } catch (Exception e) { Application.ReportError(e); } }); return null; }
        public object Invoke(Delegate d, params object[] args) { if (Dispatcher.UIThread.CheckAccess()) return d.DynamicInvoke(args); object r = null; Dispatcher.UIThread.InvokeAsync(() => { r = d.DynamicInvoke(args); }).Wait(); return r; }
        public object Invoke(Action a) { return Invoke((Delegate)a); }
        public void EndInvoke(IAsyncResult r) { }

        // events
        public event EventHandler Click, DoubleClick, Resize, SizeChanged, ClientSizeChanged, Move, LocationChanged, TextChanged, VisibleChanged, EnabledChanged, GotFocus, LostFocus, Enter, Leave, MouseEnter, MouseLeave, MouseHover, Disposed, HandleCreated, HandleDestroyed, FontChanged, BackColorChanged, Validated;
        public event MouseEventHandler MouseDown, MouseUp, MouseMove, MouseWheel, MouseClick, MouseDoubleClick; public event KeyEventHandler KeyDown, KeyUp; public event KeyPressEventHandler KeyPress; public event PaintEventHandler Paint;
        public event DragEventHandler DragEnter, DragOver, DragDrop; public event EventHandler DragLeave; public event PreviewKeyDownEventHandler PreviewKeyDown; public event CancelEventHandler Validating;
        protected virtual void OnClick(EventArgs e) { Click?.Invoke(this, e); } protected virtual void OnDoubleClick(EventArgs e) { DoubleClick?.Invoke(this, e); }
        protected virtual void OnResize(EventArgs e) { Resize?.Invoke(this, e); if (ResizeRedraw) Invalidate(); } protected virtual void OnSizeChanged(EventArgs e) { SizeChanged?.Invoke(this, e); } protected virtual void OnClientSizeChanged(EventArgs e) { ClientSizeChanged?.Invoke(this, e); }
        protected virtual void OnMove(EventArgs e) { Move?.Invoke(this, e); } protected virtual void OnLocationChanged(EventArgs e) { LocationChanged?.Invoke(this, e); } protected virtual void OnTextChanged(EventArgs e) { TextChanged?.Invoke(this, e); }
        protected virtual void OnVisibleChanged(EventArgs e) { VisibleChanged?.Invoke(this, e); } protected virtual void OnEnabledChanged(EventArgs e) { EnabledChanged?.Invoke(this, e); }
        protected virtual void OnGotFocus(EventArgs e) { GotFocus?.Invoke(this, e); } protected virtual void OnLostFocus(EventArgs e) { LostFocus?.Invoke(this, e); } protected virtual void OnEnter(EventArgs e) { Enter?.Invoke(this, e); } protected virtual void OnLeave(EventArgs e) { Leave?.Invoke(this, e); }
        protected virtual void OnMouseEnter(EventArgs e) { MouseEnter?.Invoke(this, e); } protected virtual void OnMouseLeave(EventArgs e) { MouseLeave?.Invoke(this, e); } protected virtual void OnMouseHover(EventArgs e) { MouseHover?.Invoke(this, e); }
        protected virtual void OnMouseDown(MouseEventArgs e) { MouseDown?.Invoke(this, e); } protected virtual void OnMouseUp(MouseEventArgs e) { MouseUp?.Invoke(this, e); } protected virtual void OnMouseMove(MouseEventArgs e) { MouseMove?.Invoke(this, e); }
        protected virtual void OnMouseWheel(MouseEventArgs e) { MouseWheel?.Invoke(this, e); } protected virtual void OnMouseClick(MouseEventArgs e) { MouseClick?.Invoke(this, e); } protected virtual void OnMouseDoubleClick(MouseEventArgs e) { MouseDoubleClick?.Invoke(this, e); }
        protected virtual void OnKeyDown(KeyEventArgs e) { KeyDown?.Invoke(this, e); } protected virtual void OnKeyUp(KeyEventArgs e) { KeyUp?.Invoke(this, e); } protected virtual void OnKeyPress(KeyPressEventArgs e) { KeyPress?.Invoke(this, e); }
        protected virtual void OnPaint(PaintEventArgs e) { Paint?.Invoke(this, e); } protected virtual void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(BackColor); }
        protected virtual void OnHandleCreated(EventArgs e) { HandleCreated?.Invoke(this, e); } protected virtual void OnHandleDestroyed(EventArgs e) { HandleDestroyed?.Invoke(this, e); }
        protected virtual void OnFontChanged(EventArgs e) { FontChanged?.Invoke(this, e); } protected virtual void OnBackColorChanged(EventArgs e) { BackColorChanged?.Invoke(this, e); }
        protected virtual void OnDragEnter(DragEventArgs e) { DragEnter?.Invoke(this, e); } protected virtual void OnDragOver(DragEventArgs e) { DragOver?.Invoke(this, e); } protected virtual void OnDragDrop(DragEventArgs e) { DragDrop?.Invoke(this, e); } protected virtual void OnDragLeave(EventArgs e) { DragLeave?.Invoke(this, e); }
        protected virtual void OnPreviewKeyDown(PreviewKeyDownEventArgs e) { PreviewKeyDown?.Invoke(this, e); } protected virtual void OnValidating(CancelEventArgs e) { Validating?.Invoke(this, e); }
        protected virtual void WndProc(ref Message m) { } protected void DefWndProc(ref Message m) { }
        protected virtual bool ProcessCmdKey(ref Message msg, Keys keyData) { return false; } protected virtual bool ProcessDialogKey(Keys k) { return false; } protected virtual bool IsInputKey(Keys k) { return true; }
        protected virtual CreateParams CreateParams { get { return new CreateParams(); } }
        internal bool RouteCmdKey(Keys k) { var m = new Message(); return ProcessCmdKey(ref m, k); }

        // painting
        // Windows-only calls (user32, winmm, ...) inside the app fail on macOS / Linux: they are skipped there (noted once in the log) instead of stopping the app
        internal static bool WinOnly(Exception e) { for (var x = e; x != null; x = x.InnerException) if (x is DllNotFoundException || x is EntryPointNotFoundException) return true; return false; }
        static readonly HashSet<string> noted = new HashSet<string>();
        internal static void NoteWinOnly(Exception e) { var key = e.Message.Length > 80 ? e.Message.Substring(0, 80) : e.Message; lock (noted) if (!noted.Add(key)) return; Console.Error.WriteLine("Windows-only call skipped: " + e.Message + Environment.NewLine + e.StackTrace); }
        internal static void Guard(Action a) { try { a(); } catch (Exception e) when (WinOnly(e)) { NoteWinOnly(e); } }
        internal void RaisePaint(Graphics g) { var e = new PaintEventArgs(g, ClientRectangle); if (!PaintsItself) OnPaintBackground(e); OnPaint(e); }
        protected virtual bool PaintsItself { get { return false; } }
        public void Invalidate() { if (native != null) Dispatcher.UIThread.Post(() => { var s = native as PaintSurface ?? Surface; if (s != null) s.InvalidateVisual(); }, DispatcherPriority.Render); }
        public void Invalidate(bool children) { Invalidate(); } public void Invalidate(Rectangle r) { Invalidate(); } public void Invalidate(Region r) { Invalidate(); }
        public void Update() { } public void Refresh() { Invalidate(); }
        public Graphics CreateGraphics() { return Graphics.FromImage(new Bitmap(Math.Max(1, w), Math.Max(1, h))); }
        public void DrawToBitmap(Bitmap bmp, Rectangle r) { using (var g = Graphics.FromImage(bmp)) { g.TranslateTransform(-r.X, -r.Y); RaisePaint(g); } }
        internal PaintSurface Surface;

        // the Avalonia element
        protected virtual AC.Control CreateNative() { return Surface = new PaintSurface(this); }
        internal AC.Control Native { get { CreateHandleIfNeeded(); return root ?? native; } }
        public void CreateControl() { CreateHandleIfNeeded(); }
        internal void CreateHandleIfNeeded()
        {
            if (native != null) return; native = CreateNative(); native.IsVisible = visible; native.IsEnabled = enabled; if (cursor != null) native.Cursor = Map.Cur(cursor);
            if (TipText != null) AC.ToolTip.SetTip(native, TipText);
            Hook(native); NativeText(text); NativeFont(); NativeColors();
            if (Controls.Count > 0) EnsureKids();
            Place(); Guard(() => OnHandleCreated(EventArgs.Empty));
        }
        void EnsureKids()
        {
            if (kids != null) return; kids = new AC.Canvas { ClipToBounds = true }; root = new AC.Panel(); root.Children.Add(native); root.Children.Add(kids);
            var cl = Controls.List; for (int i = cl.Count - 1; i >= 0; i--) { var n = cl[i].Native; if (n.Parent == null) kids.Children.Add(n); }
        }
        internal void AttachChild(Control c) { if (native == null) return; EnsureKids(); var n = c.Native; if (n.Parent == null) kids.Children.Insert(0, n); c.Place(); }
        internal void DetachChild(Control c) { if (kids != null && c.root != null) kids.Children.Remove(c.root); else if (kids != null && c.native != null) kids.Children.Remove(c.native); }
        // Windows Forms layout: docked children take the edges in z-order from the back (the last in Controls first), Fill takes what is left;
        // anchored children keep their distance to the right / bottom edges when this control is resized.
        bool laying; int anchorR, anchorB; internal bool anchorKnown;
        internal void NoteAnchor() { if (parent == null || dock != DockStyle.None) return; var cs = parent.ClientSize; if (cs.Width <= 0 && cs.Height <= 0) return; anchorR = cs.Width - (x + w); anchorB = cs.Height - (y + h); anchorKnown = true; }
        internal void DoLayout()
        {
            if (laying || Controls.Count == 0) return; laying = true;
            try
            {
                var cs = ClientSize; int l = padding.Left, t = padding.Top, r = cs.Width - padding.Right, b = cs.Height - padding.Bottom;
                var list = Controls.List;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var c = list[i]; if (!c.visible) continue;
                    switch (c.dock)
                    {
                        case DockStyle.Top: c.LayBounds(l, t, r - l, c.h); t += c.h; break;
                        case DockStyle.Bottom: c.LayBounds(l, b - c.h, r - l, c.h); b -= c.h; break;
                        case DockStyle.Left: c.LayBounds(l, t, c.w, b - t); l += c.w; break;
                        case DockStyle.Right: c.LayBounds(r - c.w, t, c.w, b - t); r -= c.w; break;
                    }
                }
                for (int i = list.Count - 1; i >= 0; i--) { var c = list[i]; if (c.visible && c.dock == DockStyle.Fill) c.LayBounds(l, t, Math.Max(0, r - l), Math.Max(0, b - t)); }
                foreach (var c in list)
                {
                    if (c.dock != DockStyle.None || !c.anchorKnown) continue;
                    int nx = c.x, ny = c.y, nw = c.w, nh = c.h; var a = c.anchor;
                    if ((a & AnchorStyles.Right) != 0) { if ((a & AnchorStyles.Left) != 0) nw = Math.Max(0, cs.Width - c.anchorR - c.x); else nx = cs.Width - c.anchorR - c.w; }
                    if ((a & AnchorStyles.Bottom) != 0) { if ((a & AnchorStyles.Top) != 0) nh = Math.Max(0, cs.Height - c.anchorB - c.y); else ny = cs.Height - c.anchorB - c.h; }
                    if (nx != c.x || ny != c.y || nw != c.w || nh != c.h) c.LayBounds(nx, ny, nw, nh);
                }
            }
            finally { laying = false; }
            OnLayout(new LayoutEventArgs(this, ""));
        }
        public event LayoutEventHandler Layout; protected virtual void OnLayout(LayoutEventArgs e) { if (Layout != null) Layout(this, e); }
        void LayBounds(int nx, int ny, int nw, int nh) { laying = true; try { SetBounds(nx, ny, nw, nh); } finally { laying = false; } }
        internal void ReorderKids() { if (kids == null) return; kids.Children.Clear(); var l = Controls.List; for (int i = l.Count - 1; i >= 0; i--) { var n = l[i].root ?? l[i].native; if (n != null && n.Parent == null) kids.Children.Add(n); } }
        internal virtual void Place()
        {
            var n = root ?? native; if (n == null) return; n.Width = Math.Max(0, w); n.Height = Math.Max(0, h);
            if (parent != null && !(this is Form)) { AC.Canvas.SetLeft(n, x); AC.Canvas.SetTop(n, y); }
            if (root != null) { native.Width = w; native.Height = h; }
        }
        protected virtual void NativeText(string t) { } protected virtual void NativeFont() { } protected virtual void NativeColors() { }

        // input from Avalonia
        int clicks; DateTime lastClick; Point lastClickAt;
        void Hook(AC.Control n)
        {
            n.PointerPressed += (s, e) => Guard(() => { var p = e.GetCurrentPoint(n); var pt = new Point((int)p.Position.X, (int)p.Position.Y); var b = Map.Btn(p.Properties); MouseButtons |= b; Mods(e.KeyModifiers); MousePosition = pt;
                var now = DateTime.Now; clicks = (now - lastClick).TotalMilliseconds < SystemInformation.DoubleClickTime && Math.Abs(pt.X - lastClickAt.X) < 5 && Math.Abs(pt.Y - lastClickAt.Y) < 5 ? clicks + 1 : 1; lastClick = now; lastClickAt = pt;
                if (n is PaintSurface) { n.Focus(); e.Pointer.Capture(n); }
                var me = new MouseEventArgs(b, clicks, pt.X, pt.Y, 0); OnMouseDown(me); if (clicks == 2) { OnMouseDoubleClick(me); OnDoubleClick(EventArgs.Empty); } if (n is PaintSurface) e.Handled = true; });
            n.PointerReleased += (s, e) => Guard(() => { var p = e.GetCurrentPoint(n); var pt = new Point((int)p.Position.X, (int)p.Position.Y); var b = e.InitialPressMouseButton == Avalonia.Input.MouseButton.Right ? MouseButtons.Right : (e.InitialPressMouseButton == Avalonia.Input.MouseButton.Middle ? MouseButtons.Middle : MouseButtons.Left); MouseButtons &= ~b; Mods(e.KeyModifiers);
                var me = new MouseEventArgs(b, clicks, pt.X, pt.Y, 0); OnMouseUp(me); if (new Rectangle(0, 0, w, h).Contains(pt)) { OnMouseClick(me); OnClick(EventArgs.Empty); }
                if (b == MouseButtons.Right && ContextMenuStrip != null) ContextMenuStrip.Show(this, pt); if (n is PaintSurface) e.Handled = true; });
            n.PointerMoved += (s, e) => Guard(() => { var p = e.GetCurrentPoint(n); var pt = new Point((int)p.Position.X, (int)p.Position.Y); MousePosition = pt; Mods(e.KeyModifiers); OnMouseMove(new MouseEventArgs(MouseButtons, 0, pt.X, pt.Y, 0)); });
            n.PointerWheelChanged += (s, e) => Guard(() => { var p = e.GetCurrentPoint(n); Mods(e.KeyModifiers); OnMouseWheel(new HandledMouseEventArgs(MouseButtons.None, 0, (int)p.Position.X, (int)p.Position.Y, (int)Math.Round(e.Delta.Y * 120))); if (n is PaintSurface) e.Handled = true; });
            n.PointerEntered += (s, e) => Guard(() => OnMouseEnter(EventArgs.Empty)); n.PointerExited += (s, e) => Guard(() => OnMouseLeave(EventArgs.Empty));
            n.GotFocus += (s, e) => Guard(() => { OnGotFocus(EventArgs.Empty); OnEnter(EventArgs.Empty); }); n.LostFocus += (s, e) => Guard(() => { OnLostFocus(EventArgs.Empty); OnLeave(EventArgs.Empty); });
            n.KeyDown += (s, e) => Guard(() => { Mods(e.KeyModifiers); var k = Map.Key(e.Key) | Map.ModKeys(e.KeyModifiers); var f = FindForm(); if (f != null && f.RouteCmdKey(k)) { e.Handled = true; return; } var ke = new KeyEventArgs(k); OnKeyDown(ke); if (ke.Handled || ke.SuppressKeyPress) e.Handled = true; });
            n.KeyUp += (s, e) => Guard(() => { Mods(e.KeyModifiers); var ke = new KeyEventArgs(Map.Key(e.Key) | Map.ModKeys(e.KeyModifiers)); OnKeyUp(ke); });
            n.TextInput += (s, e) => Guard(() => { if (string.IsNullOrEmpty(e.Text)) return; foreach (char ch in e.Text) { var kp = new KeyPressEventArgs(ch); OnKeyPress(kp); if (kp.Handled) e.Handled = true; } });
            Avalonia.Input.DragDrop.SetAllowDrop(n, true);
            n.AddHandler(Avalonia.Input.DragDrop.DropEvent, (s, e) => { if (!AllowDrop) return; var files = e.Data.GetFiles(); var list = new List<string>(); if (files != null) foreach (var f in files) { var p = Avalonia.Platform.Storage.StorageProviderExtensions.TryGetLocalPath(f); if (p != null) list.Add(p); } var d = new DataObject(); if (list.Count > 0) d.SetData(DataFormats.FileDrop, list.ToArray()); var t = e.Data.GetText(); if (t != null) d.SetData(DataFormats.Text, t); var pos = e.GetPosition(n); var de = new DragEventArgs(d, 0, (int)pos.X, (int)pos.Y, DragDropEffects.Copy, DragDropEffects.Copy); OnDragEnter(de); OnDragDrop(de); });
            n.AddHandler(Avalonia.Input.DragDrop.DragOverEvent, (s, e) => { if (AllowDrop) e.DragEffects = Avalonia.Input.DragDropEffects.Copy; });
        }
        static void Mods(KeyModifiers m) { ModifierKeys = Map.ModKeys(m); }
        internal void PerformClickInternal() { OnClick(EventArgs.Empty); OnMouseClick(new MouseEventArgs(MouseButtons.Left, 1, 1, 1, 0)); }
        public Form FindForm() { Control c = this; while (c != null && !(c is Form)) c = c.parent; return c as Form; }
        public Control TopLevelControl { get { return FindForm(); } }
        public bool Focus() { CreateHandleIfNeeded(); return native.Focus(); } public void Select() { Focus(); }
        public void BringToFront() { if (parent == null) return; var l = parent.Controls.List; l.Remove(this); l.Insert(0, this); parent.ReorderKids(); parent.DoLayout(); } public void SendToBack() { if (parent == null) return; var l = parent.Controls.List; l.Remove(this); l.Add(this); parent.ReorderKids(); parent.DoLayout(); } public void Show() { Visible = true; } public void Hide() { Visible = false; } public void PerformLayout() { DoLayout(); } public void SuspendLayout() { } public void ResumeLayout() { } public void ResumeLayout(bool b) { }
        public Point PointToScreen(Point p) { var n = Native; var top = AC.TopLevel.GetTopLevel(n); if (top == null) return p; var tp = n.TranslatePoint(new Avalonia.Point(p.X, p.Y), top); if (!tp.HasValue) return p; var s = top.PointToScreen(tp.Value); return new Point(s.X, s.Y); }
        public Point PointToClient(Point p) { var n = Native; var top = AC.TopLevel.GetTopLevel(n); if (top == null) return p; var c = top.PointToClient(new PixelPoint(p.X, p.Y)); var tp = top.TranslatePoint(c, n); return tp.HasValue ? new Point((int)tp.Value.X, (int)tp.Value.Y) : p; }
        public Rectangle RectangleToScreen(Rectangle r) { var p = PointToScreen(r.Location); return new Rectangle(p, r.Size); } public Rectangle RectangleToClient(Rectangle r) { var p = PointToClient(r.Location); return new Rectangle(p, r.Size); }
        public Control GetChildAtPoint(Point p) { foreach (var c in Controls) if (c.Visible && c.Bounds.Contains(p)) return c; return null; }
        public Size GetPreferredSize(Size proposed) { return new Size(w, h); }
        protected override void Dispose(bool disposing) { if (IsDisposed) return; Disposing = true; foreach (var c in Controls) c.Dispose(); if (parent != null) parent.Controls.Remove(this); IsDisposed = true; OnHandleDestroyed(EventArgs.Empty); Disposed?.Invoke(this, EventArgs.Empty); base.Dispose(disposing); }
    }
    public enum ControlStyles { UserPaint = 2, Opaque = 4, ResizeRedraw = 16, Selectable = 512, AllPaintingInWmPaint = 8192, OptimizedDoubleBuffer = 131072, DoubleBuffer = 65536, SupportsTransparentBackColor = 2048, StandardClick = 256, StandardDoubleClick = 4096, UserMouse = 1024 }
    public abstract class ScrollableControl : Control { public VScrollProperties VerticalScroll { get; } = new VScrollProperties(); public HScrollProperties HorizontalScroll { get; } = new HScrollProperties(); public bool AutoScroll { get; set; } public Point AutoScrollPosition { get; set; } public Size AutoScrollMinSize { get; set; } }
    public class ContainerControl : ScrollableControl { public Control ActiveControl { get; set; } }
    public class UserControl : ContainerControl { }
    public class Panel : ScrollableControl { public BorderStyle BorderStyle { get; set; } }
    public class FlowLayoutPanel : Panel { public AutoSizeMode AutoSizeMode { get; set; } public bool WrapContents { get; set; } public FlowDirection FlowDirection { get; set; } }
    public enum FlowDirection { LeftToRight, TopDown, RightToLeft, BottomUp }
    public class PictureBox : Control { public Image Image { get; set; } public PictureBoxSizeMode SizeMode { get; set; } protected override void OnPaint(PaintEventArgs e) { if (Image != null) e.Graphics.DrawImage(Image, ClientRectangle); base.OnPaint(e); } }
    public enum PictureBoxSizeMode { Normal, StretchImage, AutoSize, CenterImage, Zoom }

    static class Defaults { public static readonly Font Font = new Font("Segoe UI", 9f); }

    static class Map
    {
        public static MouseButtons Btn(PointerPointProperties p) { return p.IsRightButtonPressed ? MouseButtons.Right : (p.IsMiddleButtonPressed ? MouseButtons.Middle : (p.IsXButton1Pressed ? MouseButtons.XButton1 : (p.IsXButton2Pressed ? MouseButtons.XButton2 : MouseButtons.Left))); }
        public static Keys ModKeys(KeyModifiers m) { var k = Keys.None; if ((m & KeyModifiers.Control) != 0 || (m & KeyModifiers.Meta) != 0) k |= Keys.Control; if ((m & KeyModifiers.Shift) != 0) k |= Keys.Shift; if ((m & KeyModifiers.Alt) != 0) k |= Keys.Alt; return k; }   // Cmd on a Mac works as Ctrl (Ctrl+Z, Ctrl+S...)
        public static Avalonia.Input.Cursor Cur(Cursor c)
        {
            switch (c == null ? "arrow" : c.Kind) { case "hand": return new Avalonia.Input.Cursor(StandardCursorType.Hand); case "ibeam": return new Avalonia.Input.Cursor(StandardCursorType.Ibeam); case "wait": return new Avalonia.Input.Cursor(StandardCursorType.Wait); case "sizewe": return new Avalonia.Input.Cursor(StandardCursorType.SizeWestEast); case "sizens": return new Avalonia.Input.Cursor(StandardCursorType.SizeNorthSouth); case "sizeall": return new Avalonia.Input.Cursor(StandardCursorType.SizeAll); case "cross": return new Avalonia.Input.Cursor(StandardCursorType.Cross); case "no": return new Avalonia.Input.Cursor(StandardCursorType.No); case "sizenwse": return new Avalonia.Input.Cursor(StandardCursorType.TopLeftCorner); case "sizenesw": return new Avalonia.Input.Cursor(StandardCursorType.TopRightCorner); default: return Avalonia.Input.Cursor.Default; }
        }
        public static Keys Key(Avalonia.Input.Key k)
        {
            if (k >= Avalonia.Input.Key.A && k <= Avalonia.Input.Key.Z) return Keys.A + (k - Avalonia.Input.Key.A);
            if (k >= Avalonia.Input.Key.D0 && k <= Avalonia.Input.Key.D9) return Keys.D0 + (k - Avalonia.Input.Key.D0);
            if (k >= Avalonia.Input.Key.NumPad0 && k <= Avalonia.Input.Key.NumPad9) return Keys.NumPad0 + (k - Avalonia.Input.Key.NumPad0);
            if (k >= Avalonia.Input.Key.F1 && k <= Avalonia.Input.Key.F12) return Keys.F1 + (k - Avalonia.Input.Key.F1);
            switch (k)
            {
                case Avalonia.Input.Key.Space: return Keys.Space; case Avalonia.Input.Key.Enter: return Keys.Enter; case Avalonia.Input.Key.Escape: return Keys.Escape; case Avalonia.Input.Key.Back: return Keys.Back; case Avalonia.Input.Key.Tab: return Keys.Tab; case Avalonia.Input.Key.Delete: return Keys.Delete; case Avalonia.Input.Key.Insert: return Keys.Insert;
                case Avalonia.Input.Key.Left: return Keys.Left; case Avalonia.Input.Key.Right: return Keys.Right; case Avalonia.Input.Key.Up: return Keys.Up; case Avalonia.Input.Key.Down: return Keys.Down; case Avalonia.Input.Key.Home: return Keys.Home; case Avalonia.Input.Key.End: return Keys.End; case Avalonia.Input.Key.PageUp: return Keys.PageUp; case Avalonia.Input.Key.PageDown: return Keys.PageDown;
                case Avalonia.Input.Key.LeftShift: case Avalonia.Input.Key.RightShift: return Keys.ShiftKey; case Avalonia.Input.Key.LeftCtrl: case Avalonia.Input.Key.RightCtrl: return Keys.ControlKey; case Avalonia.Input.Key.LeftAlt: case Avalonia.Input.Key.RightAlt: return Keys.Menu;
                case Avalonia.Input.Key.OemPlus: return Keys.Oemplus; case Avalonia.Input.Key.OemMinus: return Keys.OemMinus; case Avalonia.Input.Key.OemComma: return Keys.Oemcomma; case Avalonia.Input.Key.OemPeriod: return Keys.OemPeriod; case Avalonia.Input.Key.Add: return Keys.Add; case Avalonia.Input.Key.Subtract: return Keys.Subtract; case Avalonia.Input.Key.Multiply: return Keys.Multiply; case Avalonia.Input.Key.Divide: return Keys.Divide;
                case Avalonia.Input.Key.OemOpenBrackets: return Keys.OemOpenBrackets; case Avalonia.Input.Key.OemCloseBrackets: return Keys.OemCloseBrackets; case Avalonia.Input.Key.OemQuestion: return Keys.OemQuestion; case Avalonia.Input.Key.OemSemicolon: return Keys.OemSemicolon; case Avalonia.Input.Key.OemQuotes: return Keys.OemQuotes; case Avalonia.Input.Key.OemTilde: return Keys.Oemtilde; case Avalonia.Input.Key.OemPipe: return Keys.OemPipe;
                default: return Keys.None;
            }
        }
    }
}
