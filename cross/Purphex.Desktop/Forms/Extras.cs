// Purphex Forms: smaller pieces the app uses here and there.
using System;
using System.Drawing;
using System.ComponentModel;

namespace System.Windows.Forms
{
    public enum AutoSizeMode { GrowAndShrink, GrowOnly }
    public class ScrollProperties { public bool Visible { get; set; } public bool Enabled { get; set; } = true; public int Value { get; set; } public int Minimum { get; set; } public int Maximum { get; set; } = 100; public int SmallChange { get; set; } = 1; public int LargeChange { get; set; } = 10; }
    public sealed class VScrollProperties : ScrollProperties { }
    public sealed class HScrollProperties : ScrollProperties { }
    public delegate void PopupEventHandler(object sender, PopupEventArgs e);
    public class PopupEventArgs : CancelEventArgs { public Control AssociatedControl { get; } public IWin32Window AssociatedWindow { get; } public bool IsBalloon { get; } public Size ToolTipSize { get; set; } public PopupEventArgs(IWin32Window w, Control c, bool balloon, Size size) { AssociatedWindow = w; AssociatedControl = c; IsBalloon = balloon; ToolTipSize = size; } }
    public delegate void DrawToolTipEventHandler(object sender, DrawToolTipEventArgs e);
    public class DrawToolTipEventArgs : EventArgs { public Graphics Graphics { get; } public Control AssociatedControl { get; } public IWin32Window AssociatedWindow { get; } public Rectangle Bounds { get; } public string ToolTipText { get; } public Color BackColor { get; } public Color ForeColor { get; } public Font Font { get; } public DrawToolTipEventArgs(Graphics g, IWin32Window w, Control c, Rectangle b, string text, Color back, Color fore, Font f) { Graphics = g; AssociatedWindow = w; AssociatedControl = c; Bounds = b; ToolTipText = text; BackColor = back; ForeColor = fore; Font = f; } public void DrawBackground() { } public void DrawBorder() { } public void DrawText() { } public void DrawText(TextFormatFlags f) { } }
    public delegate void ToolStripItemEventHandler(object sender, ToolStripItemEventArgs e);
    public class ToolStripItemEventArgs : EventArgs { public ToolStripItem Item { get; } public ToolStripItemEventArgs(ToolStripItem i) { Item = i; } }
    public class DomainUpDown : Control { public System.Collections.ArrayList Items { get; } = new System.Collections.ArrayList(); public int SelectedIndex { get; set; } public object SelectedItem { get; set; } public bool ReadOnly { get; set; } public event EventHandler SelectedItemChanged; }
    public sealed class BufferedGraphicsManager { public static BufferedGraphicsContext Current { get; } = new BufferedGraphicsContext(); }
    public sealed class BufferedGraphicsContext { public Size MaximumBuffer { get; set; } }
}
namespace System.Windows.Forms
{
    public delegate void LayoutEventHandler(object sender, LayoutEventArgs e);
    public sealed class LayoutEventArgs : EventArgs { public Control AffectedControl { get; } public string AffectedProperty { get; } public LayoutEventArgs(Control c, string p) { AffectedControl = c; AffectedProperty = p; } }
}
