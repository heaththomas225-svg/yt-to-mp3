// Purphex Forms: standard controls backed by Avalonia's own (typing, buttons, lists, sliders).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using Avalonia.Media;
using AC = Avalonia.Controls;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;
using Color = System.Drawing.Color;
using PointF = System.Drawing.PointF;
using Rectangle = System.Drawing.Rectangle;

namespace System.Windows.Forms
{
    static class Bridge
    {
        public static IBrush Brush(Color c) { return new SolidColorBrush(Avalonia.Media.Color.FromArgb(c.A, c.R, c.G, c.B)); }
        public static void FontOn(AC.Primitives.TemplatedControl n, System.Drawing.Font f) { if (n == null || f == null) return; n.FontFamily = new Avalonia.Media.FontFamily(f.Name); n.FontSize = f.SizeInPoints * 96f / 72f; n.FontWeight = f.Bold ? FontWeight.Bold : FontWeight.Normal; n.FontStyle = f.Italic ? Avalonia.Media.FontStyle.Italic : Avalonia.Media.FontStyle.Normal; }
        public static void FontOn(AC.TextBlock n, System.Drawing.Font f) { if (n == null || f == null) return; n.FontFamily = new Avalonia.Media.FontFamily(f.Name); n.FontSize = f.SizeInPoints * 96f / 72f; n.FontWeight = f.Bold ? FontWeight.Bold : FontWeight.Normal; }
    }

    public abstract class TextBoxBase : Control
    {
        internal AC.TextBox Box; bool ro, multi, wrap = true; int maxLen = 32767; char pw; ScrollBars sb;
        public bool ReadOnly { get { return ro; } set { ro = value; if (Box != null) Box.IsReadOnly = value; } }
        public virtual bool Multiline { get { return multi; } set { multi = value; if (Box != null) { Box.AcceptsReturn = value; Box.TextWrapping = value && wrap ? TextWrapping.Wrap : TextWrapping.NoWrap; } } }
        public bool WordWrap { get { return wrap; } set { wrap = value; Multiline = multi; } }
        public int MaxLength { get { return maxLen; } set { maxLen = value; if (Box != null) Box.MaxLength = value; } }
        public bool AcceptsTab { get; set; } public bool AcceptsReturn { get; set; } public bool HideSelection { get; set; } public BorderStyle BorderStyle { get; set; } public bool ShortcutsEnabled { get; set; } = true; public bool Modified { get; set; }
        public ScrollBars ScrollBars { get { return sb; } set { sb = value; } }
        public char PasswordChar { get { return pw; } set { pw = value; if (Box != null) Box.PasswordChar = value; } }
        public int SelectionStart { get { return Box != null ? Math.Min(Box.SelectionStart, Box.SelectionEnd) : 0; } set { if (Box != null) { Box.SelectionStart = value; Box.SelectionEnd = value; Box.CaretIndex = value; } } }
        public int SelectionLength { get { return Box != null ? Math.Abs(Box.SelectionEnd - Box.SelectionStart) : 0; } set { if (Box != null) Box.SelectionEnd = Math.Min(Box.SelectionStart + value, (Box.Text ?? "").Length); } }
        public string SelectedText { get { var t = Text; int s = SelectionStart, l = SelectionLength; return s + l <= t.Length ? t.Substring(s, l) : ""; } set { var t = Text; int s = SelectionStart, l = SelectionLength; Text = t.Substring(0, s) + (value ?? "") + t.Substring(Math.Min(t.Length, s + l)); SelectionStart = s + (value ?? "").Length; } }
        public int TextLength { get { return Text.Length; } } public string[] Lines { get { return Text.Replace("\r\n", "\n").Split('\n'); } set { Text = string.Join(Environment.NewLine, value); } }
        public void Select(int start, int len) { SelectionStart = start; SelectionLength = len; } public void SelectAll() { if (Box != null) Box.SelectAll(); } public void AppendText(string t) { Text += t; if (Box != null) Box.CaretIndex = Text.Length; } public void Clear() { Text = ""; } public void ScrollToCaret() { } public void Copy() { if (Box != null) Box.Copy(); } public void Paste() { if (Box != null) Box.Paste(); } public void Cut() { if (Box != null) Box.Cut(); } public void Undo() { if (Box != null) Box.Undo(); } public void DeselectAll() { SelectionLength = 0; }
        public int GetLineFromCharIndex(int i) { int n = 0; var t = Text; for (int k = 0; k < Math.Min(i, t.Length); k++) if (t[k] == '\n') n++; return n; }
        public int GetFirstCharIndexFromLine(int line) { int n = 0; var t = Text; if (line <= 0) return 0; for (int k = 0; k < t.Length; k++) if (t[k] == '\n' && ++n == line) return k + 1; return -1; }
        public Point GetPositionFromCharIndex(int i) { return Point.Empty; } public int GetCharIndexFromPosition(Point p) { return 0; }
        protected override AC.Control CreateNative()
        {
            Box = new AC.TextBox { IsReadOnly = ro, AcceptsReturn = multi, TextWrapping = multi && wrap ? TextWrapping.Wrap : TextWrapping.NoWrap, MaxLength = maxLen, Padding = new Avalonia.Thickness(4, 2), MinHeight = 0 };
            if (pw != '\0') Box.PasswordChar = pw;
            Box.TextChanged += (s, e) => { var t = Box.Text ?? ""; if (t != base.Text) { inSync = true; base.Text = t; inSync = false; Modified = true; } };
            return Box;
        }
        bool inSync;
        protected override void NativeText(string t) { if (Box != null && !inSync && Box.Text != t) Box.Text = t; }
        protected override void NativeFont() { Bridge.FontOn(Box, Font); }
        protected override void NativeColors() { if (Box == null) return; Box.Background = Bridge.Brush(BackColor); Box.Foreground = Bridge.Brush(ForeColor); }
    }
    public class TextBox : TextBoxBase { public int PreferredHeight { get { return Font != null ? (int)Math.Ceiling(Font.GetHeight()) + 8 : 24; } } public HorizontalAlignment TextAlign { get; set; } public CharacterCasing CharacterCasing { get; set; } public bool UseSystemPasswordChar { get { return PasswordChar != '\0'; } set { PasswordChar = value ? '•' : '\0'; } } public AutoCompleteMode AutoCompleteMode { get; set; } public AutoCompleteSource AutoCompleteSource { get; set; } public string PlaceholderText { get { return Box != null ? Box.Watermark : ""; } set { CreateHandleIfNeeded(); Box.Watermark = value; } } }
    public enum CharacterCasing { Normal, Upper, Lower }
    public enum AutoCompleteMode { None, Suggest, Append, SuggestAppend }
    public enum AutoCompleteSource { None, CustomSource, FileSystem, HistoryList, RecentlyUsedList, AllUrl, AllSystemSources, FileSystemDirectories, ListItems }
    public class RichTextBox : TextBoxBase
    {
        public new RichTextBoxScrollBars ScrollBars { get; set; } public int SelectionIndent { get; set; } public int SelectionRightIndent { get; set; } public int SelectionHangingIndent { get; set; } public bool SelectionBullet { get; set; } public int BulletIndent { get; set; } public int RightMargin { get; set; }
        public RichTextBox() { Multiline = true; }
        public string Rtf { get { return Text; } set { Text = value; } } public Color SelectionColor { get; set; } public Font SelectionFont { get; set; } public Color SelectionBackColor { get; set; } public HorizontalAlignment SelectionAlignment { get; set; } public bool DetectUrls { get; set; } public float ZoomFactor { get; set; } = 1f; public RichTextBoxScrollBars ScrollBarsRich { get; set; } public bool EnableAutoDragDrop { get; set; }
        public event ContentsResizedEventHandler ContentsResized; public event EventHandler SelectionChanged, VScroll;
        public int Find(string s, int start, RichTextBoxFinds f) { return Text.IndexOf(s, Math.Max(0, start), StringComparison.OrdinalIgnoreCase); }
    }
    public enum RichTextBoxScrollBars { None, Horizontal, Vertical, Both, ForcedHorizontal, ForcedVertical, ForcedBoth }
    [Flags] public enum RichTextBoxFinds { None = 0, WholeWord = 2, MatchCase = 4, NoHighlight = 8, Reverse = 16 }
    public delegate void ContentsResizedEventHandler(object sender, ContentsResizedEventArgs e);
    public class ContentsResizedEventArgs : EventArgs { public Rectangle NewRectangle { get; } public ContentsResizedEventArgs(Rectangle r) { NewRectangle = r; } }

    public class ButtonBase : Control
    {
        public FlatStyle FlatStyle { get; set; } public FlatButtonAppearance FlatAppearance { get; } = new FlatButtonAppearance(); public Image Image { get; set; } public ContentAlignment TextAlign { get; set; } public ContentAlignment ImageAlign { get; set; } public bool UseVisualStyleBackColor { get; set; } public bool UseMnemonic { get; set; } = true; public bool UseCompatibleTextRendering { get; set; } public TextImageRelation TextImageRelation { get; set; }
    }
    public sealed class FlatButtonAppearance { public int BorderSize { get; set; } public Color BorderColor { get; set; } public Color MouseOverBackColor { get; set; } public Color MouseDownBackColor { get; set; } public Color CheckedBackColor { get; set; } }
    public enum TextImageRelation { Overlay, ImageAboveText, TextAboveImage, ImageBeforeText, TextBeforeImage }
    public class Button : ButtonBase, IButtonControl
    {
        AC.Button b; public DialogResult DialogResult { get; set; }
        public void PerformClick() { OnClick(EventArgs.Empty); } public void NotifyDefault(bool v) { }
        protected override void OnClick(EventArgs e) { base.OnClick(e); if (DialogResult != DialogResult.None) { var f = FindForm(); if (f != null) { f.DialogResult = DialogResult; f.Close(); } } }
        protected override AC.Control CreateNative() { b = new AC.Button { HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center, Padding = new Avalonia.Thickness(6, 2) }; b.Click += (s, e) => PerformClick(); return b; }
        protected override void NativeText(string t) { if (b != null) b.Content = t.Replace("&&", "\u0001").Replace("&", "").Replace("\u0001", "&"); }
        protected override void NativeFont() { Bridge.FontOn(b, Font); } protected override void NativeColors() { if (b == null) return; b.Background = Bridge.Brush(BackColor); b.Foreground = Bridge.Brush(ForeColor); }
    }
    public class Label : Control
    {
        AC.TextBlock t; public ContentAlignment TextAlign { get; set; } public BorderStyle BorderStyle { get; set; } public bool UseMnemonic { get; set; } = true; public Image Image { get; set; } public bool AutoEllipsis { get; set; } public FlatStyle FlatStyle { get; set; } public bool UseCompatibleTextRendering { get; set; }
        protected override AC.Control CreateNative() { t = new AC.TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }; return t; }
        protected override void NativeText(string s) { if (t != null) t.Text = s.Replace("&&", "&"); } protected override void NativeFont() { Bridge.FontOn(t, Font); } protected override void NativeColors() { if (t == null) return; t.Foreground = Bridge.Brush(ForeColor); if (BackColor.A > 0) t.Background = Bridge.Brush(BackColor); }
    }
    public class LinkLabel : Label { public event LinkLabelLinkClickedEventHandler LinkClicked; public Color LinkColor { get; set; } public Color ActiveLinkColor { get; set; } public Color VisitedLinkColor { get; set; } public LinkBehavior LinkBehavior { get; set; } protected override void OnClick(EventArgs e) { base.OnClick(e); LinkClicked?.Invoke(this, new LinkLabelLinkClickedEventArgs(null)); } }
    public delegate void LinkLabelLinkClickedEventHandler(object sender, LinkLabelLinkClickedEventArgs e);
    public class LinkLabelLinkClickedEventArgs : EventArgs { public object Link { get; } public LinkLabelLinkClickedEventArgs(object l) { Link = l; } }
    public enum LinkBehavior { SystemDefault, AlwaysUnderline, HoverUnderline, NeverUnderline }
    public class CheckBox : ButtonBase
    {
        AC.CheckBox c; bool chk; public event EventHandler CheckedChanged, CheckStateChanged; public Appearance Appearance { get; set; } public bool AutoCheck { get; set; } = true; public ContentAlignment CheckAlign { get; set; }
        public bool Checked { get { return chk; } set { if (chk == value) return; chk = value; if (c != null) c.IsChecked = value; CheckedChanged?.Invoke(this, EventArgs.Empty); CheckStateChanged?.Invoke(this, EventArgs.Empty); } }
        public CheckState CheckState { get { return chk ? CheckState.Checked : CheckState.Unchecked; } set { Checked = value != CheckState.Unchecked; } }
        protected override AC.Control CreateNative() { c = new AC.CheckBox { IsChecked = chk }; c.IsCheckedChanged += (s, e) => { Checked = c.IsChecked == true; }; return c; }
        protected override void NativeText(string t) { if (c != null) c.Content = t.Replace("&", ""); } protected override void NativeFont() { Bridge.FontOn(c, Font); } protected override void NativeColors() { if (c != null) c.Foreground = Bridge.Brush(ForeColor); }
    }
    public class RadioButton : CheckBox { }
    public enum Appearance { Normal, Button }

    public class ListControlItems : IList, IEnumerable<object>
    {
        readonly List<object> l = new List<object>(); internal Action Changed;
        public int Add(object o) { l.Add(o); Changed?.Invoke(); return l.Count - 1; } public void AddRange(object[] os) { l.AddRange(os); Changed?.Invoke(); } public void Clear() { l.Clear(); Changed?.Invoke(); } public void Remove(object o) { l.Remove(o); Changed?.Invoke(); } public void RemoveAt(int i) { l.RemoveAt(i); Changed?.Invoke(); } public void Insert(int i, object o) { l.Insert(i, o); Changed?.Invoke(); }
        public bool Contains(object o) { return l.Contains(o); } public int IndexOf(object o) { return l.IndexOf(o); } public int Count { get { return l.Count; } } public object this[int i] { get { return l[i]; } set { l[i] = value; Changed?.Invoke(); } }
        public bool IsFixedSize { get { return false; } } public bool IsReadOnly { get { return false; } } public bool IsSynchronized { get { return false; } } public object SyncRoot { get { return l; } } public void CopyTo(Array a, int i) { ((ICollection)l).CopyTo(a, i); }
        IEnumerator<object> IEnumerable<object>.GetEnumerator() { return l.GetEnumerator(); } public IEnumerator GetEnumerator() { return l.GetEnumerator(); } internal List<object> List { get { return l; } }
    }
    public class ComboBox : Control
    {
        AC.ComboBox cb; int sel = -1; public ListControlItems Items { get; } = new ListControlItems(); public ComboBoxStyle DropDownStyle { get; set; } = ComboBoxStyle.DropDown; public int MaxDropDownItems { get; set; } public int DropDownWidth { get; set; } public int DropDownHeight { get; set; } public FlatStyle FlatStyle { get; set; } public DrawMode DrawMode { get; set; } public int ItemHeight { get; set; } public bool IntegralHeight { get; set; } public bool Sorted { get; set; } public string DisplayMember { get; set; } public string ValueMember { get; set; } public object DataSource { get; set; }
        public event EventHandler SelectedIndexChanged, SelectionChangeCommitted, DropDown, DropDownClosed; public event DrawItemEventHandler DrawItem; public event MeasureItemEventHandler MeasureItem;
        public ComboBox() { Items.Changed = Sync; }
        protected virtual void OnDrawItem(DrawItemEventArgs e) { if (DrawItem != null) DrawItem(this, e); } protected virtual void OnMeasureItem(MeasureItemEventArgs e) { if (MeasureItem != null) MeasureItem(this, e); } protected virtual void OnSelectedIndexChanged(EventArgs e) { }
        public int SelectedIndex { get { return sel; } set { if (value == sel) return; sel = Math.Max(-1, Math.Min(Items.Count - 1, value)); if (cb != null && cb.SelectedIndex != sel) cb.SelectedIndex = sel; SelectedIndexChanged?.Invoke(this, EventArgs.Empty); } }
        public object SelectedItem { get { return sel >= 0 && sel < Items.Count ? Items[sel] : null; } set { SelectedIndex = Items.IndexOf(value); } }
        public override string Text { get { return SelectedItem != null ? SelectedItem.ToString() : base.Text; } set { int i = -1; for (int k = 0; k < Items.Count; k++) if (Items[k].ToString() == value) i = k; if (i >= 0) SelectedIndex = i; else base.Text = value; } }
        public bool DroppedDown { get { return cb != null && cb.IsDropDownOpen; } set { if (cb != null) cb.IsDropDownOpen = value; } }
        public void BeginUpdate() { } public void EndUpdate() { Sync(); } public int FindStringExact(string s) { for (int i = 0; i < Items.Count; i++) if (Items[i].ToString() == s) return i; return -1; }
        void Sync() { if (cb == null) return; cb.ItemsSource = null; cb.ItemsSource = new List<string>(Items.List.ConvertAll(o => o?.ToString() ?? "")); cb.SelectedIndex = sel; }
        protected override AC.Control CreateNative() { cb = new AC.ComboBox { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch }; Sync(); cb.SelectionChanged += (s, e) => { if (cb.SelectedIndex != sel) { SelectedIndex = cb.SelectedIndex; SelectionChangeCommitted?.Invoke(this, EventArgs.Empty); } }; cb.DropDownOpened += (s, e) => DropDown?.Invoke(this, EventArgs.Empty); cb.DropDownClosed += (s, e) => DropDownClosed?.Invoke(this, EventArgs.Empty); return cb; }
        protected override void NativeFont() { Bridge.FontOn(cb, Font); } protected override void NativeColors() { if (cb == null) return; cb.Background = Bridge.Brush(BackColor); cb.Foreground = Bridge.Brush(ForeColor); }
    }
    public class ListBox : Control
    {
        AC.ListBox lb; int sel = -1; public ListControlItems Items { get; } = new ListControlItems(); public DrawMode DrawMode { get; set; } public int ItemHeight { get; set; } = 18; public bool IntegralHeight { get; set; } public BorderStyle BorderStyle { get; set; } public SelectionMode SelectionMode { get; set; } = SelectionMode.One; public int TopIndex { get; set; } public bool Sorted { get; set; } public bool HorizontalScrollbar { get; set; } public bool ScrollAlwaysVisible { get; set; }
        public event EventHandler SelectedIndexChanged; public event DrawItemEventHandler DrawItem; public event MeasureItemEventHandler MeasureItem;
        public ListBox() { Items.Changed = Sync; }
        protected virtual void OnDrawItem(DrawItemEventArgs e) { if (DrawItem != null) DrawItem(this, e); } protected virtual void OnMeasureItem(MeasureItemEventArgs e) { if (MeasureItem != null) MeasureItem(this, e); }
        public int SelectedIndex { get { return sel; } set { if (value == sel) return; sel = Math.Max(-1, Math.Min(Items.Count - 1, value)); if (lb != null && lb.SelectedIndex != sel) lb.SelectedIndex = sel; SelectedIndexChanged?.Invoke(this, EventArgs.Empty); } }
        public object SelectedItem { get { return sel >= 0 && sel < Items.Count ? Items[sel] : null; } set { SelectedIndex = Items.IndexOf(value); } }
        public int IndexFromPoint(Point p) { return Math.Max(-1, Math.Min(Items.Count - 1, (p.Y / Math.Max(1, ItemHeight)) + TopIndex)); } public int IndexFromPoint(int x, int y) { return IndexFromPoint(new Point(x, y)); } public Rectangle GetItemRectangle(int i) { return new Rectangle(0, (i - TopIndex) * ItemHeight, Width, ItemHeight); }
        public void BeginUpdate() { } public void EndUpdate() { Sync(); } public void ClearSelected() { SelectedIndex = -1; }
        void Sync() { if (lb == null) return; lb.ItemsSource = null; lb.ItemsSource = new List<string>(Items.List.ConvertAll(o => o?.ToString() ?? "")); lb.SelectedIndex = sel; }
        protected override AC.Control CreateNative() { lb = new AC.ListBox(); Sync(); lb.SelectionChanged += (s, e) => { if (lb.SelectedIndex != sel) SelectedIndex = lb.SelectedIndex; }; return lb; }
        protected override void NativeFont() { Bridge.FontOn(lb, Font); } protected override void NativeColors() { if (lb == null) return; lb.Background = Bridge.Brush(BackColor); lb.Foreground = Bridge.Brush(ForeColor); }
    }
    public enum SelectionMode { None, One, MultiSimple, MultiExtended }
    public class NumericUpDown : Control
    {
        AC.NumericUpDown n; decimal val, min, max = 100, inc = 1; public int DecimalPlaces { get; set; } public bool Hexadecimal { get; set; } public bool ThousandsSeparator { get; set; } public HorizontalAlignment TextAlign { get; set; } public BorderStyle BorderStyle { get; set; } public bool ReadOnly { get; set; }
        public event EventHandler ValueChanged;
        public decimal Value { get { return val; } set { var v = Math.Max(min, Math.Min(max, value)); if (v == val) return; val = v; if (n != null) n.Value = v; ValueChanged?.Invoke(this, EventArgs.Empty); } }
        public decimal Minimum { get { return min; } set { min = value; if (n != null) n.Minimum = value; } } public decimal Maximum { get { return max; } set { max = value; if (n != null) n.Maximum = value; } } public decimal Increment { get { return inc; } set { inc = value; if (n != null) n.Increment = value; } }
        protected override AC.Control CreateNative() { n = new AC.NumericUpDown { Minimum = min, Maximum = max, Increment = inc, Value = val, FormatString = "F" + DecimalPlaces }; n.ValueChanged += (s, e) => { if (n.Value.HasValue) Value = n.Value.Value; }; return n; }
    }
    public class TrackBar : Control
    {
        AC.Slider s; int val, min, max = 10; public int TickFrequency { get; set; } = 1; public TickStyle TickStyle { get; set; } public Orientation Orientation { get; set; } public int SmallChange { get; set; } = 1; public int LargeChange { get; set; } = 5; public event EventHandler ValueChanged, Scroll;
        public int Value { get { return val; } set { var v = Math.Max(min, Math.Min(max, value)); if (v == val) return; val = v; if (s != null) s.Value = v; ValueChanged?.Invoke(this, EventArgs.Empty); } }
        public int Minimum { get { return min; } set { min = value; if (s != null) s.Minimum = value; } } public int Maximum { get { return max; } set { max = value; if (s != null) s.Maximum = value; } }
        protected override AC.Control CreateNative() { s = new AC.Slider { Minimum = min, Maximum = max, Value = val, IsSnapToTickEnabled = true, TickFrequency = 1 }; s.ValueChanged += (o, e) => { int v = (int)Math.Round(s.Value); if (v != val) { Value = v; Scroll?.Invoke(this, EventArgs.Empty); } }; return s; }
    }
    public class ProgressBar : Control { public int Value { get; set; } public int Minimum { get; set; } public int Maximum { get; set; } = 100; public ProgressBarStyle Style { get; set; } public int MarqueeAnimationSpeed { get; set; } }
    public enum ProgressBarStyle { Blocks, Continuous, Marquee }
    public class GroupBox : Control { }
    public class SplitContainer : Control { public Panel Panel1 { get; } = new Panel(); public Panel Panel2 { get; } = new Panel(); public int SplitterDistance { get; set; } }
    public class TableLayoutPanel : Panel { public int ColumnCount { get; set; } public int RowCount { get; set; } }

    // the Windows web view (Music Hub, the Video and Photo Editors): on macOS / Linux these open in the system browser until the
    // cross-platform web view lands (Phase 4). Present so the app compiles and the rest runs.
    public class WebBrowser : Control { public bool ScrollBarsEnabled { get; set; } public object ObjectForScripting { get; set; } public object Document { get { return null; } } public Uri Url { get; set; } public string DocumentText { get; set; } public bool ScriptErrorsSuppressed { get; set; } public bool IsWebBrowserContextMenuEnabled { get; set; } public bool WebBrowserShortcutsEnabled { get; set; } public bool AllowWebBrowserDrop { get; set; } public event EventHandler DocumentCompleted; public void Navigate(string url) { Url = new Uri(url); } public void Navigate(Uri u) { Url = u; } }
}
