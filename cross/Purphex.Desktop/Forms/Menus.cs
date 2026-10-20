// Purphex Forms: right-click and drop-down menus. The app builds ContextMenuStrip / ToolStripMenuItem trees exactly as on Windows;
// when one is shown it becomes an Avalonia context menu (sub-menus, ticks, disabled items, colours, separators).
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using Avalonia.Threading;
using AC = Avalonia.Controls;

namespace System.Windows.Forms
{
    public class ToolStripItemCollection : IEnumerable<ToolStripItem>, IList
    {
        readonly List<ToolStripItem> list = new List<ToolStripItem>(); internal ToolStripItem Owner;
        public ToolStripItem Add(string text) { var i = new ToolStripMenuItem(text); Add(i); return i; }
        public ToolStripItem Add(string text, Image img) { var i = new ToolStripMenuItem(text, img); Add(i); return i; }
        public ToolStripItem Add(string text, Image img, EventHandler click) { var i = new ToolStripMenuItem(text, img, click); Add(i); return i; }
        public int Add(ToolStripItem item) { list.Add(item); item.OwnerItem = Owner; return list.Count - 1; }
        public void AddRange(ToolStripItem[] items) { foreach (var i in items) Add(i); }
        public void Insert(int index, ToolStripItem item) { list.Insert(Math.Max(0, Math.Min(list.Count, index)), item); item.OwnerItem = Owner; }
        public void Remove(ToolStripItem item) { list.Remove(item); } public void RemoveAt(int i) { list.RemoveAt(i); } public void Clear() { list.Clear(); }
        public int Count { get { return list.Count; } } public ToolStripItem this[int i] { get { return list[i]; } } public bool Contains(ToolStripItem i) { return list.Contains(i); } public int IndexOf(ToolStripItem i) { return list.IndexOf(i); }
        public IEnumerator<ToolStripItem> GetEnumerator() { return list.GetEnumerator(); } IEnumerator IEnumerable.GetEnumerator() { return list.GetEnumerator(); }
        int IList.Add(object v) { return Add((ToolStripItem)v); } bool IList.Contains(object v) { return list.Contains(v as ToolStripItem); } int IList.IndexOf(object v) { return list.IndexOf(v as ToolStripItem); } void IList.Insert(int i, object v) { Insert(i, (ToolStripItem)v); } void IList.Remove(object v) { list.Remove(v as ToolStripItem); }
        object IList.this[int i] { get { return list[i]; } set { list[i] = (ToolStripItem)value; } } bool IList.IsFixedSize { get { return false; } } bool IList.IsReadOnly { get { return false; } } void ICollection.CopyTo(Array a, int i) { ((ICollection)list).CopyTo(a, i); } bool ICollection.IsSynchronized { get { return false; } } object ICollection.SyncRoot { get { return list; } }
    }

    public abstract class ToolStripItem : Component
    {
        public virtual string Text { get; set; } = ""; public bool Enabled { get; set; } = true; public bool Visible { get; set; } = true; public bool Available { get { return Visible; } set { Visible = value; } } public Image Image { get; set; } public object Tag { get; set; } public string Name { get; set; } = ""; public string ToolTipText { get; set; }
        public Color ForeColor { get; set; } = Color.Empty; public Color BackColor { get; set; } = Color.Empty; public Font Font { get; set; } public ToolStripItemDisplayStyle DisplayStyle { get; set; } = ToolStripItemDisplayStyle.ImageAndText; public Padding Padding { get; set; } public Size Size { get; set; } public bool AutoSize { get; set; } = true; public bool AutoToolTip { get; set; }
        public ContentAlignment TextAlign { get; set; } public ContentAlignment ImageAlign { get; set; } public ToolStripItemImageScaling ImageScaling { get; set; } public bool Selected { get { return false; } } public Rectangle Bounds { get { return Rectangle.Empty; } } public int Width { get { return Size.Width; } set { Size = new Size(value, Size.Height); } } public int Height { get { return Size.Height; } set { Size = new Size(Size.Width, value); } }
        public ToolStripItem OwnerItem { get; internal set; } public ToolStrip Owner { get; internal set; } public ToolStrip GetCurrentParent() { return Owner; }
        public event EventHandler Click, MouseEnter, MouseLeave, MouseHover; public event MouseEventHandler MouseDown, MouseUp;
        public void PerformClick() { if (!Enabled) return; OnClick(EventArgs.Empty); }
        protected virtual void OnClick(EventArgs e) { Click?.Invoke(this, e); }
        internal void RaiseHover() { MouseEnter?.Invoke(this, EventArgs.Empty); }
        public void Select() { } public void Invalidate() { }
    }
    public enum ToolStripItemImageScaling { None, SizeToFit }
    public class ToolStripSeparator : ToolStripItem { }
    public class ToolStripLabel : ToolStripItem { public ToolStripLabel() { } public ToolStripLabel(string t) { Text = t; } public bool IsLink { get; set; } }
    public class ToolStripButton : ToolStripItem { public bool Checked { get; set; } public bool CheckOnClick { get; set; } public ToolStripButton() { } public ToolStripButton(string t) { Text = t; } }
    public class ToolStripControlHost : ToolStripItem { public Control Control { get; } public ToolStripControlHost(Control c) { Control = c; } }
    public class ToolStripDropDownItem : ToolStripItem
    {
        public ToolStripDropDownItem() { DropDownItems = new ToolStripItemCollection { Owner = this }; }
        public ToolStripItemCollection DropDownItems { get; } public bool HasDropDownItems { get { return DropDownItems.Count > 0; } } public ToolStripDropDown DropDown { get { return null; } }
        public event EventHandler DropDownOpening, DropDownOpened, DropDownClosed; public void ShowDropDown() { } public void HideDropDown() { }
        internal void RaiseOpening() { DropDownOpening?.Invoke(this, EventArgs.Empty); DropDownOpened?.Invoke(this, EventArgs.Empty); }
    }
    public class ToolStripMenuItem : ToolStripDropDownItem
    {
        public bool Checked { get; set; } public bool CheckOnClick { get; set; } public CheckState CheckState { get { return Checked ? CheckState.Checked : CheckState.Unchecked; } set { Checked = value != CheckState.Unchecked; } } public string ShortcutKeyDisplayString { get; set; } public Keys ShortcutKeys { get; set; } public bool ShowShortcutKeys { get; set; } = true;
        public event EventHandler CheckedChanged;
        public ToolStripMenuItem() { } public ToolStripMenuItem(string text) { Text = text; } public ToolStripMenuItem(string text, Image img) { Text = text; Image = img; } public ToolStripMenuItem(string text, Image img, EventHandler click) { Text = text; Image = img; if (click != null) Click += click; }
        public ToolStripMenuItem(string text, Image img, params ToolStripItem[] items) { Text = text; Image = img; DropDownItems.AddRange(items); }
        protected override void OnClick(EventArgs e) { if (CheckOnClick) { Checked = !Checked; CheckedChanged?.Invoke(this, EventArgs.Empty); } base.OnClick(e); }
    }
    public enum CheckState { Unchecked, Checked, Indeterminate }

    public class ToolStrip : Control
    {
        public ToolStrip() { Items = new ToolStripItemCollection(); } public ToolStripItemCollection Items { get; }
        public ToolStripRenderer Renderer { get; set; } public ToolStripRenderMode RenderMode { get; set; } public bool ShowItemToolTips { get; set; } public ToolStripGripStyle GripStyle { get; set; } public bool ShowImageMargin { get; set; } = true; public bool ShowCheckMargin { get; set; } public Size ImageScalingSize { get; set; } public bool DropShadowEnabled { get; set; } = true; public bool AutoClose { get; set; } = true;
    }
    public enum ToolStripRenderMode { Custom, System, Professional, ManagerRenderMode }
    public enum ToolStripGripStyle { Hidden, Visible }
    public class ToolStripDropDown : ToolStrip { public event ToolStripDropDownClosedEventHandler Closed; public event CancelEventHandler Opening; public event EventHandler Opened; public bool IsDropDown { get { return true; } }
        internal void RaiseClosed() { Closed?.Invoke(this, new ToolStripDropDownClosedEventArgs(ToolStripDropDownCloseReason.ItemClicked)); } internal bool RaiseOpening() { var e = new CancelEventArgs(); OnOpening(e); return !e.Cancel; } internal void RaiseOpened() { Opened?.Invoke(this, EventArgs.Empty); }
        protected virtual void OnOpening(CancelEventArgs e) { Opening?.Invoke(this, e); } public void Close() { if (menu != null) menu.Close(); } public void Close(ToolStripDropDownCloseReason r) { Close(); } internal AC.ContextMenu menu; }
    public delegate void ToolStripDropDownClosedEventHandler(object sender, ToolStripDropDownClosedEventArgs e);
    public class ToolStripDropDownClosedEventArgs : EventArgs { public ToolStripDropDownCloseReason CloseReason { get; } public ToolStripDropDownClosedEventArgs(ToolStripDropDownCloseReason r) { CloseReason = r; } }
    public enum ToolStripDropDownCloseReason { AppFocusChange, AppClicked, ItemClicked, Keyboard, CloseCalled }
    public class ToolStripDropDownMenu : ToolStripDropDown { }
    public class ContextMenuStrip : ToolStripDropDownMenu
    {
        public ContextMenuStrip() { } public ContextMenuStrip(IContainer c) { }
        public Control SourceControl { get; private set; }
        public void Show(Control c, Point p) { SourceControl = c; ShowAt(c, p); } public void Show(Control c, int x, int y) { Show(c, new Point(x, y)); }
        public void Show(Point screen) { var f = Form.ActiveForm ?? (Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null); if (f != null) ShowAt(f, f.PointToClient(screen)); }
        public void Show(int x, int y) { Show(new Point(x, y)); } public void Show(Control c, Point p, ToolStripDropDownDirection d) { Show(c, p); }
        void ShowAt(Control c, Point p)
        {
            if (!RaiseOpening()) return; var target = c.Native; menu = new AC.ContextMenu { PlacementTarget = target, Placement = Avalonia.Controls.PlacementMode.Pointer };
            foreach (var it in Build(Items)) menu.Items.Add(it);
            menu.Closed += (s, e) => Dispatcher.UIThread.Post(RaiseClosed);
            menu.Open(target); RaiseOpened();
        }
        static List<object> Build(ToolStripItemCollection items)
        {
            var res = new List<object>();
            foreach (var item in items)
            {
                if (!item.Visible) continue;
                if (item is ToolStripSeparator) { res.Add(new AC.Separator()); continue; }
                var mi = new AC.MenuItem { Header = item.Text ?? "", IsEnabled = item.Enabled };
                if (!item.ForeColor.IsEmpty) mi.Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(item.ForeColor.A, item.ForeColor.R, item.ForeColor.G, item.ForeColor.B));
                var tmi = item as ToolStripMenuItem;
                if (tmi != null && tmi.Checked) mi.Icon = new AC.TextBlock { Text = "✓" };
                if (tmi != null && !string.IsNullOrEmpty(tmi.ShortcutKeyDisplayString)) mi.InputGesture = null;
                var dd = item as ToolStripDropDownItem;
                if (dd != null && dd.DropDownItems.Count > 0) { var d0 = dd; mi.SubmenuOpened += (s, e) => d0.RaiseOpening(); foreach (var sub in Build(dd.DropDownItems)) mi.Items.Add(sub); }
                else { var it0 = item; mi.Click += (s, e) => Dispatcher.UIThread.Post(() => it0.PerformClick()); }
                res.Add(mi);
            }
            return res;
        }
    }
    public enum ToolStripDropDownDirection { AboveLeft, AboveRight, BelowLeft, BelowRight, Left, Right, Default }
    public class MenuStrip : ToolStrip { }
    public class StatusStrip : ToolStrip { }

    // renderers: the Windows app paints its menus itself; here Avalonia draws them, so these only need to exist
    public abstract class ToolStripRenderer { protected virtual void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e) { } protected virtual void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { } protected virtual void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e) { } protected virtual void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { } protected virtual void OnRenderToolStripBackground(ToolStripRenderEventArgs e) { } protected virtual void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) { } protected virtual void OnRenderArrow(ToolStripArrowRenderEventArgs e) { } protected virtual void OnRenderImageMargin(ToolStripRenderEventArgs e) { } protected virtual void OnRenderItemImage(ToolStripItemImageRenderEventArgs e) { } }
    public class ToolStripProfessionalRenderer : ToolStripRenderer { public ToolStripProfessionalRenderer() { } public ToolStripProfessionalRenderer(ProfessionalColorTable t) { ColorTable = t; } public ProfessionalColorTable ColorTable { get; } public bool RoundedEdges { get; set; } }
    public class ToolStripSystemRenderer : ToolStripRenderer { }
    public class ProfessionalColorTable { public virtual Color MenuItemSelected { get { return Color.Purple; } } public virtual Color MenuBorder { get { return Color.Purple; } } public virtual Color ToolStripDropDownBackground { get { return Color.Black; } } public virtual Color ImageMarginGradientBegin { get { return Color.Black; } } public virtual Color ImageMarginGradientMiddle { get { return Color.Black; } } public virtual Color ImageMarginGradientEnd { get { return Color.Black; } } public virtual Color MenuItemBorder { get { return Color.Purple; } } public virtual Color SeparatorDark { get { return Color.Gray; } } public virtual Color SeparatorLight { get { return Color.Gray; } } public virtual Color MenuItemSelectedGradientBegin { get { return Color.Purple; } } public virtual Color MenuItemSelectedGradientEnd { get { return Color.Purple; } } public virtual Color MenuItemPressedGradientBegin { get { return Color.Purple; } } public virtual Color MenuItemPressedGradientEnd { get { return Color.Purple; } } public virtual Color CheckBackground { get { return Color.Purple; } } public virtual Color CheckSelectedBackground { get { return Color.Purple; } } public bool UseSystemColors { get; set; } }
    public class ToolStripRenderEventArgs : EventArgs { public Graphics Graphics { get; } public ToolStrip ToolStrip { get; } public Rectangle AffectedBounds { get; } public Color BackColor { get; } public ToolStripRenderEventArgs(Graphics g, ToolStrip t) { Graphics = g; ToolStrip = t; } }
    public class ToolStripItemRenderEventArgs : EventArgs { public Graphics Graphics { get; } public ToolStripItem Item { get; } public ToolStrip ToolStrip { get; } public ToolStripItemRenderEventArgs(Graphics g, ToolStripItem i) { Graphics = g; Item = i; } }
    public class ToolStripItemTextRenderEventArgs : ToolStripItemRenderEventArgs { public string Text { get; set; } public Color TextColor { get; set; } public Font TextFont { get; set; } public Rectangle TextRectangle { get; set; } public TextFormatFlags TextFormat { get; set; } public ToolStripItemTextRenderEventArgs(Graphics g, ToolStripItem i, string t, Rectangle r, Color c, Font f, TextFormatFlags fl) : base(g, i) { Text = t; TextRectangle = r; TextColor = c; TextFont = f; TextFormat = fl; } }
    public class ToolStripSeparatorRenderEventArgs : ToolStripItemRenderEventArgs { public bool Vertical { get; } public ToolStripSeparatorRenderEventArgs(Graphics g, ToolStripSeparator s, bool v) : base(g, s) { Vertical = v; } }
    public class ToolStripItemImageRenderEventArgs : ToolStripItemRenderEventArgs { public Image Image { get; } public Rectangle ImageRectangle { get; } public ToolStripItemImageRenderEventArgs(Graphics g, ToolStripItem i, Rectangle r) : base(g, i) { ImageRectangle = r; } }
    public class ToolStripArrowRenderEventArgs : EventArgs { public Graphics Graphics { get; } public ToolStripItem Item { get; } public Color ArrowColor { get; set; } public Rectangle ArrowRectangle { get; set; } public ArrowDirection Direction { get; set; } public ToolStripArrowRenderEventArgs(Graphics g, ToolStripItem i, Rectangle r, Color c, ArrowDirection d) { Graphics = g; Item = i; ArrowRectangle = r; ArrowColor = c; Direction = d; } }
    public enum ArrowDirection { Left = 0, Up = 1, Right = 16, Down = 17 }
    [Flags] public enum TextFormatFlags { Default = 0, Left = 0, Top = 0, HorizontalCenter = 1, Right = 2, VerticalCenter = 4, Bottom = 8, WordBreak = 16, SingleLine = 32, ExpandTabs = 64, NoClipping = 256, ExternalLeading = 512, NoPrefix = 2048, Internal = 4096, TextBoxControl = 8192, PathEllipsis = 16384, EndEllipsis = 32768, ModifyString = 65536, RightToLeft = 131072, WordEllipsis = 262144, NoFullWidthCharacterBreak = 524288, HidePrefix = 1048576, PrefixOnly = 2097152, PreserveGraphicsClipping = 16777216, PreserveGraphicsTranslateTransform = 4194304, NoPadding = 268435456, LeftAndRightPadding = 536870912 }
    public static class TextRenderer
    {
        public static Size MeasureText(string text, Font f) { return MeasureText(text, f, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.Default); }
        public static Size MeasureText(string text, Font f, Size proposed) { return MeasureText(text, f, proposed, TextFormatFlags.Default); }
        public static Size MeasureText(string text, Font f, Size proposed, TextFormatFlags flags) { using (var b = new Bitmap(1, 1)) using (var g = Graphics.FromImage(b)) { var s = g.MeasureString(text ?? "", f, (flags & TextFormatFlags.WordBreak) != 0 && proposed.Width > 0 && proposed.Width < int.MaxValue ? proposed.Width : 0); return new Size((int)Math.Ceiling(s.Width), (int)Math.Ceiling(s.Height)); } }
        public static Size MeasureText(IDeviceContext dc, string text, Font f, Size proposed, TextFormatFlags flags) { return MeasureText(text, f, proposed, flags); }
        public static void DrawText(IDeviceContext dc, string text, Font f, Rectangle r, Color c, TextFormatFlags flags) { var g = dc as Graphics; if (g == null) return; using (var b = new SolidBrush(c)) using (var sf = new StringFormat { Alignment = (flags & TextFormatFlags.HorizontalCenter) != 0 ? StringAlignment.Center : ((flags & TextFormatFlags.Right) != 0 ? StringAlignment.Far : StringAlignment.Near), LineAlignment = (flags & TextFormatFlags.VerticalCenter) != 0 ? StringAlignment.Center : ((flags & TextFormatFlags.Bottom) != 0 ? StringAlignment.Far : StringAlignment.Near), FormatFlags = (flags & TextFormatFlags.WordBreak) != 0 ? 0 : StringFormatFlags.NoWrap, Trimming = (flags & TextFormatFlags.EndEllipsis) != 0 ? StringTrimming.EllipsisCharacter : StringTrimming.None }) g.DrawString(text, f, b, r, sf); }
        public static void DrawText(IDeviceContext dc, string text, Font f, Rectangle r, Color c, Color back, TextFormatFlags flags) { DrawText(dc, text, f, r, c, flags); }
        public static void DrawText(IDeviceContext dc, string text, Font f, Point p, Color c) { DrawText(dc, text, f, new Rectangle(p, new Size(4000, 400)), c, TextFormatFlags.Default); }
        public static void DrawText(IDeviceContext dc, string text, Font f, Point p, Color c, TextFormatFlags flags) { DrawText(dc, text, f, new Rectangle(p, new Size(4000, 400)), c, flags); }
    }
}

