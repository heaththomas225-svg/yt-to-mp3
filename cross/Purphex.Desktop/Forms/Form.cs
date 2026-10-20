// Purphex Forms: Form (an Avalonia window), Application, Timer, MessageBox and the file dialogs, on Avalonia.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AC = Avalonia.Controls;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;
using Color = System.Drawing.Color;
using PointF = System.Drawing.PointF;
using Rectangle = System.Drawing.Rectangle;

namespace System.Windows.Forms
{
    public class Form : ContainerControl
    {
        protected virtual bool ShowWithoutActivation { get { return false; } }
        internal AC.Window win; internal volatile bool active; bool shown, closing; FormWindowState state; bool topMost, showInTaskbar = true; FormBorderStyle border = FormBorderStyle.Sizable; double opacity = 1; Icon icon;
        public Form() { Width = 800; Height = 600; }
        public FormStartPosition StartPosition { get; set; } = FormStartPosition.WindowsDefaultLocation; public bool KeyPreview { get; set; } public IButtonControl AcceptButton { get; set; } public IButtonControl CancelButton { get; set; }
        public DialogResult DialogResult { get; set; } public bool ControlBox { get; set; } = true; public bool MaximizeBox { get; set; } = true; public bool MinimizeBox { get; set; } = true; public bool ShowIcon { get; set; } = true; public Color TransparencyKey { get; set; } public bool Modal { get; private set; } public bool IsMdiContainer { get; set; } public SizeGripStyle SizeGripStyle { get; set; } public AutoScaleMode AutoScaleMode { get; set; } public SizeF AutoScaleDimensions { get; set; }
        public Form Owner { get; set; } public Form[] OwnedForms { get { return Application.OpenForms.Cast<Form>().Where(f => f.Owner == this).ToArray(); } }
        public static Form ActiveForm { get { foreach (Form f in Application.OpenForms) if (f.active) return f; return null; } }
        public Icon Icon { get { return icon; } set { icon = value; } }
        public bool TopMost { get { return topMost; } set { topMost = value; if (win != null) win.Topmost = value; } }
        public bool ShowInTaskbar { get { return showInTaskbar; } set { showInTaskbar = value; if (win != null) win.ShowInTaskbar = value; } }
        public double Opacity { get { return opacity; } set { opacity = value; if (win != null) win.Opacity = value; } }
        public FormBorderStyle FormBorderStyle { get { return border; } set { border = value; ApplyBorder(); } }
        public FormWindowState WindowState { get { return state; } set { state = value; if (win != null) win.WindowState = value == FormWindowState.Maximized ? AC.WindowState.Maximized : (value == FormWindowState.Minimized ? AC.WindowState.Minimized : AC.WindowState.Normal); } }
        public Rectangle RestoreBounds { get { return Bounds; } } public Rectangle DesktopBounds { get { return Bounds; } set { Bounds = value; } }
        public event EventHandler Load, Shown, Activated, Deactivate, ResizeBegin, ResizeEnd; public event FormClosingEventHandler FormClosing; public event FormClosedEventHandler FormClosed;
        protected virtual void OnLoad(EventArgs e) { Load?.Invoke(this, e); } protected virtual void OnShown(EventArgs e) { Shown?.Invoke(this, e); } protected virtual void OnActivated(EventArgs e) { Activated?.Invoke(this, e); } protected virtual void OnDeactivate(EventArgs e) { Deactivate?.Invoke(this, e); }
        protected virtual void OnFormClosing(FormClosingEventArgs e) { FormClosing?.Invoke(this, e); } protected virtual void OnFormClosed(FormClosedEventArgs e) { FormClosed?.Invoke(this, e); } protected virtual void OnResizeBegin(EventArgs e) { ResizeBegin?.Invoke(this, e); } protected virtual void OnResizeEnd(EventArgs e) { ResizeEnd?.Invoke(this, e); }
        void ApplyBorder() { if (win == null) return; win.SystemDecorations = border == FormBorderStyle.None ? AC.SystemDecorations.None : AC.SystemDecorations.Full; win.CanResize = border == FormBorderStyle.Sizable || border == FormBorderStyle.SizableToolWindow; }
        void EnsureWindow()
        {
            if (win != null) return;
            win = new AC.Window { Title = Text, Width = Width, Height = Height, Topmost = topMost, ShowInTaskbar = showInTaskbar, Opacity = opacity, Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(BackColor.A, BackColor.R, BackColor.G, BackColor.B)) };
            if (MinimumSize.Width > 0) win.MinWidth = MinimumSize.Width; if (MinimumSize.Height > 0) win.MinHeight = MinimumSize.Height; if (MaximumSize.Width > 0) win.MaxWidth = MaximumSize.Width; if (MaximumSize.Height > 0) win.MaxHeight = MaximumSize.Height;
            ApplyBorder(); win.Content = Native;
            if (StartPosition == FormStartPosition.CenterScreen || (StartPosition == FormStartPosition.CenterParent && Owner == null)) win.WindowStartupLocation = AC.WindowStartupLocation.CenterScreen;
            else if (StartPosition == FormStartPosition.CenterParent) win.WindowStartupLocation = AC.WindowStartupLocation.CenterOwner;
            else if (StartPosition == FormStartPosition.Manual) win.Position = new PixelPoint(Left, Top);
            if (state != FormWindowState.Normal) WindowState = state;
            win.SizeChanged += (s, e) => Guard(() => { if (win.ClientSize.Width > 0) base.SetBoundsFromWindow((int)win.ClientSize.Width, (int)win.ClientSize.Height); });
            win.PositionChanged += (s, e) => Guard(() => base.MoveFromWindow(win.Position.X, win.Position.Y));
            win.Activated += (s, e) => Guard(() => { active = true; OnActivated(EventArgs.Empty); }); win.Deactivated += (s, e) => Guard(() => { active = false; OnDeactivate(EventArgs.Empty); });
            win.PropertyChanged += (s, e) => Guard(() => { if (e.Property == AC.Window.WindowStateProperty) { var ws = win.WindowState; state = ws == AC.WindowState.Maximized || ws == AC.WindowState.FullScreen ? FormWindowState.Maximized : ws == AC.WindowState.Minimized ? FormWindowState.Minimized : FormWindowState.Normal; } });
            win.Closing += (s, e) => Guard(() => { if (closing) return; var fc = new FormClosingEventArgs(CloseReason.UserClosing, false); OnFormClosing(fc); if (fc.Cancel) e.Cancel = true; });
            win.Closed += (s, e) => Guard(() => { Application.forms.Remove(this); OnFormClosed(new FormClosedEventArgs(CloseReason.UserClosing)); if (Application.mainForm == this) Application.Exit(); });
            win.KeyDown += (s, e) => Guard(() => { if (e.Handled) return; var k = Map.Key(e.Key) | Map.ModKeys(e.KeyModifiers); if (RouteCmdKey(k)) { e.Handled = true; return; } if (k == Keys.Enter && AcceptButton != null) { AcceptButton.PerformClick(); e.Handled = true; } else if (k == Keys.Escape && CancelButton != null) { CancelButton.PerformClick(); e.Handled = true; } });
        }
        public override string Text { get { return base.Text; } set { base.Text = value; if (win != null) win.Title = value; } }
        public override Size ClientSize { get { return base.ClientSize; } set { base.ClientSize = value; if (win != null) { win.Width = value.Width; win.Height = value.Height; } } }
        public new void Show() { Show(null); }
        public void Show(IWin32Window owner)
        {
            if (owner is Form) Owner = (Form)owner; EnsureWindow(); if (!Application.forms.Contains(this)) Application.forms.Add(this);
            if (!shown) { shown = true; OnLoad(EventArgs.Empty); }
            if (Owner != null && Owner.win != null) win.Show(Owner.win); else win.Show(); Visible = true; OnShown(EventArgs.Empty);
        }
        public DialogResult ShowDialog() { return ShowDialog(null); }
        public DialogResult ShowDialog(IWin32Window owner)
        {
            if (owner is Form) Owner = (Form)owner; else if (owner is Control) Owner = ((Control)owner).FindForm(); EnsureWindow(); if (!Application.forms.Contains(this)) Application.forms.Add(this);
            DialogResult = DialogResult.None; Modal = true; if (!shown) { shown = true; OnLoad(EventArgs.Empty); }
            var cts = new CancellationTokenSource(); win.Closed += (s, e) => cts.Cancel();
            var parentWin = Owner != null ? Owner.win : Application.MainWindow; if (parentWin != null) win.ShowDialog(parentWin); else win.Show(); OnShown(EventArgs.Empty);
            Dispatcher.UIThread.MainLoop(cts.Token); Modal = false; if (DialogResult == DialogResult.None) DialogResult = DialogResult.Cancel; return DialogResult;
        }
        public void Close() { if (win == null) return; var fc = new FormClosingEventArgs(CloseReason.UserClosing, false); OnFormClosing(fc); if (fc.Cancel) return; closing = true; win.Close(); }
        public new void Hide() { if (win != null) win.Hide(); Visible = false; }
        public void Activate() { if (win != null) win.Activate(); }
        public new void BringToFront() { Activate(); }
        public void CenterToScreen() { StartPosition = FormStartPosition.CenterScreen; }
        public override bool Equals(object o) { return ReferenceEquals(this, o); } public override int GetHashCode() { return base.GetHashCode(); }
        protected override void Dispose(bool disposing) { if (win != null) { try { closing = true; win.Close(); } catch { } } base.Dispose(disposing); }
    }
    public interface IButtonControl { DialogResult DialogResult { get; set; } void PerformClick(); void NotifyDefault(bool v); }
    public enum SizeGripStyle { Auto, Show, Hide }
    public enum AutoScaleMode { None, Font, Dpi, Inherit }

    public static class Application
    {
        internal static readonly List<Form> forms = new List<Form>(); internal static Form mainForm; static Avalonia.Application app;
        public static FormCollection OpenForms { get { return new FormCollection(forms); } }
        public static event ThreadExceptionEventHandler ThreadException; public static event EventHandler ApplicationExit, Idle;
        public static string ExecutablePath { get { return Environment.ProcessPath ?? ""; } } public static string StartupPath { get { return AppContext.BaseDirectory.TrimEnd('/', '\\'); } }
        public static string ProductName { get { return "Purphex Music"; } } public static string ProductVersion { get { return typeof(Application).Assembly.GetName().Version.ToString(); } } public static string UserAppDataPath { get { return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); } } public static string CompanyName { get { return "Purphex"; } }
        internal static AC.Window MainWindow { get { return mainForm != null ? mainForm.win : null; } }
        public static void EnableVisualStyles() { } public static void SetCompatibleTextRenderingDefault(bool b) { } public static void SetHighDpiMode(int m) { } public static void SetUnhandledExceptionMode(UnhandledExceptionMode m) { }
        public static void AddMessageFilter(IMessageFilter f) { } public static void RemoveMessageFilter(IMessageFilter f) { }
        internal static void ReportError(Exception e) { if (Control.WinOnly(e)) { Control.NoteWinOnly(e); return; } var h = ThreadException; if (h != null) h(null, new ThreadExceptionEventArgs(e)); else Console.Error.WriteLine(e); }
        // the Avalonia app runs on this thread; Run(form) shows the first window and returns when it closes
        static bool looping;
        public static void Run(Form main)
        {
            mainForm = main;
            if (!looping)
            {
                looping = true; Boot.Up(); Started(); Boot.Life.Start(Environment.GetCommandLineArgs());
                return;
            }
            main.Show(); var cts = new CancellationTokenSource(); main.FormClosed += (s, e) => cts.Cancel(); Dispatcher.UIThread.MainLoop(cts.Token);
        }
        // PURPHEX_SNAP=<file.png>[;seconds]: saves a picture of the main window after it has been up a while, then quits (used by the build checks)
        static void Snap()
        {
            var spec = Environment.GetEnvironmentVariable("PURPHEX_SNAP"); if (string.IsNullOrEmpty(spec)) return;
            var parts = spec.Split(';'); double secs = parts.Length > 1 ? double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) : 6;
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(secs) };
            var click = Environment.GetEnvironmentVariable("PURPHEX_SNAP_CLICK");   // presses the button with this text first (e.g. PURPHEX MUSIC opens the studio)
            if (!string.IsNullOrEmpty(click)) { var c0 = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) }; c0.Tick += (s, e) => { c0.Stop(); var b = FindByText(mainForm, click); Console.WriteLine(b == null ? "snap: no button " + click : "snap: pressing " + click); if (b != null) b.PerformClickInternal(); }; c0.Start(); }
            t.Tick += (s, e) => { t.Stop(); try { AC.Window w = null; foreach (Form f in OpenForms) if (f.win != null && f.win.IsVisible) w = f.win; w = w ?? MainWindow; var sz = new PixelSize(Math.Max(1, (int)w.Bounds.Width), Math.Max(1, (int)w.Bounds.Height)); using (var bmp = new Avalonia.Media.Imaging.RenderTargetBitmap(sz)) { bmp.Render(w); bmp.Save(parts[0]); } Console.WriteLine("snap saved " + sz); } catch (Exception ex) { Console.WriteLine("snap failed: " + ex); } Environment.Exit(0); };
            t.Start();
        }
        static Control FindByText(Control root, string text) { if (root == null) return null; foreach (var c in root.Controls) { if (c.Text == text) return c; var r = FindByText(c, text); if (r != null) return r; } return null; }
        internal static void Started() { Snap(); if (mainForm != null) { mainForm.Show(); if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d) d.MainWindow = mainForm.win; } Dispatcher.UIThread.Post(() => Idle?.Invoke(null, EventArgs.Empty), DispatcherPriority.Background); }
        public static void Run() { if (mainForm != null) Run(mainForm); }
        public static void DoEvents() { Dispatcher.UIThread.RunJobs(); }
        public static void Exit() { ApplicationExit?.Invoke(null, EventArgs.Empty); if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d) d.Shutdown(); else Environment.Exit(0); }
        public static void Restart() { System.Diagnostics.Process.Start(ExecutablePath); Exit(); }
        internal static void Init(Avalonia.Application a) { app = a; }
    }
    public enum UnhandledExceptionMode { Automatic, ThrowException, CatchException }
    public sealed class FormCollection : ReadOnlyCollectionBase { public FormCollection(List<Form> l) { foreach (var f in l) InnerList.Add(f); } public Form this[int i] { get { return (Form)InnerList[i]; } } public Form this[string name] { get { foreach (Form f in InnerList) if (f.Name == name) return f; return null; } } }
    public delegate void ThreadExceptionEventHandler(object sender, ThreadExceptionEventArgs e);
    public class ThreadExceptionEventArgs : EventArgs { public Exception Exception { get; } public ThreadExceptionEventArgs(Exception e) { Exception = e; } }

    // Avalonia is set up before Main runs (the app makes timers and windows in its constructor, and those need the platform ready)
    static class Boot
    {
        internal static ClassicDesktopStyleApplicationLifetime Life; static bool up;
        [System.Runtime.CompilerServices.ModuleInitializer] internal static void Init() { try { Up(); } catch (Exception e) { Console.Error.WriteLine("Avalonia setup: " + e); } }
        internal static void Up()
        {
            if (up) return; up = true;
            var life = new ClassicDesktopStyleApplicationLifetime { ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose }; Life = life;
            AppBuilder.Configure<PurphexAvaloniaApp>().UsePlatformDetect().WithInterFont().LogToTrace().SetupWithLifetime(life);
            // Inter (shipped with Avalonia) stands in for Segoe UI where Windows fonts are missing
            foreach (var w in new[] { new { F = "Thin", W = 100 }, new { F = "ExtraLight", W = 200 }, new { F = "Light", W = 300 }, new { F = "Regular", W = 400 }, new { F = "Medium", W = 500 }, new { F = "SemiBold", W = 600 }, new { F = "Bold", W = 700 } })
            {
                try { using (var st = Avalonia.Platform.AssetLoader.Open(new Uri("avares://Avalonia.Fonts.Inter/Assets/Inter-" + w.F + ".ttf"))) { var ms = new System.IO.MemoryStream(); st.CopyTo(ms); ms.Position = 0; System.Drawing.FontFallback.Register("Inter", w.W, SkiaSharp.SKTypeface.FromStream(ms)); } } catch { }
            }
            if (Environment.GetEnvironmentVariable("PURPHEX_FONTTEST") == "1")   // prints what the Windows font names become here, then quits
            {
                foreach (var n in new[] { "Segoe UI", "Segoe UI Semibold", "Segoe UI Black", "Segoe UI Light", "Segoe UI NotHere", "Segoe UI NotHere Semibold", "Consolas", "Bahnschrift", "Bahnschrift SemiBold", "Bahnschrift NotHere SemiBold" })
                    foreach (var b in new[] { false, true }) { var t = System.Drawing.FontFallback.Resolve(n, b, false); Console.WriteLine(n + (b ? " +bold" : "") + " -> " + t.FamilyName + " " + t.FontStyle.Weight); }
                Environment.Exit(0);
            }
            Dispatcher.UIThread.UnhandledException += (s, e) => { e.Handled = true; if (Control.WinOnly(e.Exception)) Control.NoteWinOnly(e.Exception); else Application.ReportError(e.Exception); };
        }
    }
    sealed class PurphexAvaloniaApp : Avalonia.Application
    {
        public override void Initialize() { Styles.Add(new Avalonia.Themes.Fluent.FluentTheme()); RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark; Application.Init(this); }
        public override void OnFrameworkInitializationCompleted() { base.OnFrameworkInitializationCompleted(); Clipboard.GetTextImpl = () => { var top = Application.MainWindow; return top?.Clipboard == null ? null : CommonDialog.Wait(top.Clipboard.GetTextAsync()); }; Clipboard.SetTextImpl = t => { var top = Application.MainWindow; if (top?.Clipboard != null) CommonDialog.Wait(top.Clipboard.SetTextAsync(t ?? "").ContinueWith(_ => true)); }; }
    }

    public class Timer : Component
    {
        readonly DispatcherTimer t = new DispatcherTimer(); bool enabled;
        public Timer() { t.Interval = TimeSpan.FromMilliseconds(100); t.Tick += (s, e) => { var h = Tick; if (h != null) try { h(this, EventArgs.Empty); } catch (Exception ex) { Application.ReportError(ex); } }; }
        public Timer(IContainer c) : this() { }
        public int Interval { get { return (int)t.Interval.TotalMilliseconds; } set { t.Interval = TimeSpan.FromMilliseconds(Math.Max(1, value)); } }
        public bool Enabled { get { return enabled; } set { enabled = value; if (value) t.Start(); else t.Stop(); } }
        public event EventHandler Tick; public object Tag { get; set; }
        public void Start() { Enabled = true; } public void Stop() { Enabled = false; }
        protected override void Dispose(bool disposing) { t.Stop(); base.Dispose(disposing); }
    }

    // ---------------- message boxes (a small Avalonia dialog, shown modally) ----------------
    public static class MessageBox
    {
        public static DialogResult Show(string text) { return Show(null, text, "", MessageBoxButtons.OK, MessageBoxIcon.None); }
        public static DialogResult Show(string text, string caption) { return Show(null, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None); }
        public static DialogResult Show(string text, string caption, MessageBoxButtons b) { return Show(null, text, caption, b, MessageBoxIcon.None); }
        public static DialogResult Show(string text, string caption, MessageBoxButtons b, MessageBoxIcon i) { return Show(null, text, caption, b, i); }
        public static DialogResult Show(string text, string caption, MessageBoxButtons b, MessageBoxIcon i, MessageBoxDefaultButton d) { return Show(null, text, caption, b, i); }
        public static DialogResult Show(IWin32Window owner, string text) { return Show(owner, text, "", MessageBoxButtons.OK, MessageBoxIcon.None); }
        public static DialogResult Show(IWin32Window owner, string text, string caption) { return Show(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None); }
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons b) { return Show(owner, text, caption, b, MessageBoxIcon.None); }
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons b, MessageBoxIcon i, MessageBoxDefaultButton d) { return Show(owner, text, caption, b, i); }
        public static DialogResult Show(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            var result = DialogResult.Cancel; var w = new AC.Window { Title = string.IsNullOrEmpty(caption) ? "Purphex Music" : caption, SizeToContent = AC.SizeToContent.WidthAndHeight, CanResize = false, WindowStartupLocation = AC.WindowStartupLocation.CenterOwner, MaxWidth = 560 };
            var panel = new AC.StackPanel { Margin = new Thickness(22), Spacing = 18 }; panel.Children.Add(new AC.TextBlock { Text = text ?? "", TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 500 });
            var row = new AC.StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 10, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
            Action<string, DialogResult> add = (label, r) => { var bt = new AC.Button { Content = label, MinWidth = 90 }; bt.Click += (s, e) => { result = r; w.Close(); }; row.Children.Add(bt); };
            switch (buttons) { case MessageBoxButtons.OKCancel: add("OK", DialogResult.OK); add("Cancel", DialogResult.Cancel); break; case MessageBoxButtons.YesNo: add("Yes", DialogResult.Yes); add("No", DialogResult.No); break; case MessageBoxButtons.YesNoCancel: add("Yes", DialogResult.Yes); add("No", DialogResult.No); add("Cancel", DialogResult.Cancel); break; case MessageBoxButtons.RetryCancel: add("Retry", DialogResult.Retry); add("Cancel", DialogResult.Cancel); break; case MessageBoxButtons.AbortRetryIgnore: add("Abort", DialogResult.Abort); add("Retry", DialogResult.Retry); add("Ignore", DialogResult.Ignore); break; default: add("OK", DialogResult.OK); result = DialogResult.OK; break; }
            panel.Children.Add(row); w.Content = panel;
            var cts = new CancellationTokenSource(); w.Closed += (s, e) => cts.Cancel(); var ow = owner is Control ? ((Control)owner).FindForm()?.win : Application.MainWindow;
            if (ow != null) w.ShowDialog(ow); else w.Show(); Dispatcher.UIThread.MainLoop(cts.Token); return result;
        }
    }

    // ---------------- file dialogs (the system's own pickers) ----------------
    public abstract class CommonDialog : Component { public abstract DialogResult ShowDialog(); public virtual DialogResult ShowDialog(IWin32Window owner) { return ShowDialog(); } public object Tag { get; set; }
        internal static T Wait<T>(System.Threading.Tasks.Task<T> task) { var cts = new CancellationTokenSource(); task.ContinueWith(_ => cts.Cancel()); if (!task.IsCompleted) Dispatcher.UIThread.MainLoop(cts.Token); return task.Result; }
        internal static IStorageProvider Storage { get { var w = Application.MainWindow; return w != null ? w.StorageProvider : null; } } }
    public abstract class FileDialog : CommonDialog
    {
        public string FileName { get; set; } = ""; public string[] FileNames { get; protected set; } = new string[0]; public string Filter { get; set; } = ""; public int FilterIndex { get; set; } = 1; public string Title { get; set; } = ""; public string InitialDirectory { get; set; } = ""; public string DefaultExt { get; set; } = ""; public bool AddExtension { get; set; } = true; public bool CheckFileExists { get; set; } public bool CheckPathExists { get; set; } public bool RestoreDirectory { get; set; } public bool OverwritePrompt { get; set; } = true; public bool ValidateNames { get; set; } = true; public bool SupportMultiDottedExtensions { get; set; } public bool AutoUpgradeEnabled { get; set; } = true;
        internal List<FilePickerFileType> Types() { var l = new List<FilePickerFileType>(); var parts = (Filter ?? "").Split('|'); for (int i = 0; i + 1 < parts.Length; i += 2) l.Add(new FilePickerFileType(parts[i]) { Patterns = parts[i + 1].Split(';').Select(p => p.Trim()).ToList() }); return l; }
        internal async System.Threading.Tasks.Task<IStorageFolder> Start() { var sp = Storage; if (sp == null || string.IsNullOrEmpty(InitialDirectory) || !System.IO.Directory.Exists(InitialDirectory)) return null; try { return await sp.TryGetFolderFromPathAsync(InitialDirectory); } catch { return null; } }
    }
    public sealed class OpenFileDialog : FileDialog
    {
        public bool Multiselect { get; set; } public bool ReadOnlyChecked { get; set; } public bool ShowReadOnly { get; set; }
        public override DialogResult ShowDialog()
        {
            var sp = Storage; if (sp == null) return DialogResult.Cancel;
            var start = Wait(Start()); var files = Wait(sp.OpenFilePickerAsync(new FilePickerOpenOptions { Title = Title, AllowMultiple = Multiselect, FileTypeFilter = Types(), SuggestedStartLocation = start }));
            if (files == null || files.Count == 0) return DialogResult.Cancel; FileNames = files.Select(f => ((IStorageItem)f).TryGetLocalPath()).Where(p => p != null).ToArray(); if (FileNames.Length == 0) return DialogResult.Cancel; FileName = FileNames[0]; return DialogResult.OK;
        }
        public System.IO.Stream OpenFile() { return System.IO.File.OpenRead(FileName); }
    }
    public sealed class SaveFileDialog : FileDialog
    {
        public bool CreatePrompt { get; set; }
        public override DialogResult ShowDialog()
        {
            var sp = Storage; if (sp == null) return DialogResult.Cancel;
            var start = Wait(Start()); var file = Wait(sp.SaveFilePickerAsync(new FilePickerSaveOptions { Title = Title, SuggestedFileName = System.IO.Path.GetFileName(FileName), DefaultExtension = DefaultExt, FileTypeChoices = Types(), ShowOverwritePrompt = OverwritePrompt, SuggestedStartLocation = start }));
            var p = file == null ? null : ((IStorageItem)file).TryGetLocalPath(); if (p == null) return DialogResult.Cancel; FileName = p; FileNames = new[] { p }; return DialogResult.OK;
        }
    }
    public sealed class FolderBrowserDialog : CommonDialog
    {
        public string SelectedPath { get; set; } = ""; public string Description { get; set; } = ""; public bool ShowNewFolderButton { get; set; } = true; public Environment.SpecialFolder RootFolder { get; set; } public bool UseDescriptionForTitle { get; set; }
        public override DialogResult ShowDialog()
        {
            var sp = Storage; if (sp == null) return DialogResult.Cancel;
            var folders = Wait(sp.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = Description, AllowMultiple = false }));
            var p = folders != null && folders.Count > 0 ? ((IStorageItem)folders[0]).TryGetLocalPath() : null; if (p == null) return DialogResult.Cancel; SelectedPath = p; return DialogResult.OK;
        }
    }
    public sealed class ColorDialog : CommonDialog { public Color Color { get; set; } public bool FullOpen { get; set; } public bool AnyColor { get; set; } public int[] CustomColors { get; set; } public override DialogResult ShowDialog() { return DialogResult.Cancel; } }
}
