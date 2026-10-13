using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// ======================================================================
//  Dark theme helpers: title bar, scrollbars, menus, drop-downs, check boxes. No white anywhere.
// ======================================================================
static class Win
{
    // A window painted bigger than WinForms' back-buffer limit gets a fresh buffer that was never cleared, and whatever was in that memory (even another window) showed through. A big limit keeps buffers cached and cleared.
    // The animated background repaints many times a second, and at a big size that can keep the window so busy that the buttons and boxes on top never get their turn to repaint
    // (they showed as grey blocks). These make Windows paint every pending control right now.
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool RedrawWindow(IntPtr h, IntPtr r, IntPtr rgn, uint flags);
    static int lastRepaint;
    public static void PaintPending(Control c) { try { if (c != null && c.IsHandleCreated) RedrawWindow(c.Handle, IntPtr.Zero, IntPtr.Zero, 0x0080 | 0x0100); } catch { } }   // RDW_ALLCHILDREN | RDW_UPDATENOW
    public static void RepaintAll(Control c) { try { if (c != null && c.IsHandleCreated && unchecked(Environment.TickCount - lastRepaint) > 60) { lastRepaint = Environment.TickCount; RedrawWindow(c.Handle, IntPtr.Zero, IntPtr.Zero, 0x0001 | 0x0080 | 0x0100); } } catch { } }   // + RDW_INVALIDATE
    // Saves that cannot half-finish: the text goes to a side file first and replaces the real one only when it is all written (a crash, a full disk or a power cut can no longer leave a broken project, list or settings file behind).
    public static void WriteAtomic(string path, string text, System.Text.Encoding enc)
    {
        string tmp = path + ".tmp"; System.IO.File.WriteAllText(tmp, text, enc);
        if (System.IO.File.Exists(path)) { try { System.IO.File.Replace(tmp, path, null); } catch { System.IO.File.Copy(tmp, path, true); try { System.IO.File.Delete(tmp); } catch { } } } else System.IO.File.Move(tmp, path);
    }
    public static void WidenBuffers() { try { BufferedGraphicsManager.Current.MaximumBuffer = new Size(8192, 4608); } catch { } }
    // set every pixel fully opaque, so a background picture with a hole in it can never let the desktop (or another window) show through
    public static void ForceOpaque(Bitmap b)
    {
        if (b == null) return; var pf = b.PixelFormat; if (pf != System.Drawing.Imaging.PixelFormat.Format32bppPArgb && pf != System.Drawing.Imaging.PixelFormat.Format32bppArgb) return;
        try
        {
            var bd = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, pf); var row = new byte[Math.Abs(bd.Stride)];
            try { for (int y = 0; y < b.Height; y++) { IntPtr p = new IntPtr(bd.Scan0.ToInt64() + (long)y * bd.Stride); System.Runtime.InteropServices.Marshal.Copy(p, row, 0, b.Width * 4); for (int x = 3; x < b.Width * 4; x += 4) row[x] = 255; System.Runtime.InteropServices.Marshal.Copy(row, 0, p, b.Width * 4); } }
            finally { b.UnlockBits(bd); }
        }
        catch { }
    }
    public static volatile bool AnyMoving;   // true while any of our windows is being dragged or resized: every animation pauses so the drag stays smooth
    public static void TrackMove(ref Message m) { if (m.Msg == 0x231) AnyMoving = true; else if (m.Msg == 0x232) AnyMoving = false; }
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr h, string app, string ids);

    // Dark title bar with a near-black purple caption (Windows 10 2004+ / Windows 11; harmlessly ignored elsewhere).
    public static void Dark(Form f)
    {
        try { if (AppLogo.Icon != null) f.Icon = AppLogo.Icon; } catch { }   // every window wears the Purphex emblem
        Action a = delegate ()
        {
            try
            {
                int one = 1; DwmSetWindowAttribute(f.Handle, 20, ref one, 4); DwmSetWindowAttribute(f.Handle, 19, ref one, 4);
                var cc = TM.C(255, 10, 4, 20); var tc = TM.C(255, 235, 225, 255); var bc = TM.C(255, 110, 60, 190); int cap = cc.R | (cc.G << 8) | (cc.B << 16), txt = tc.R | (tc.G << 8) | (tc.B << 16), border = bc.R | (bc.G << 8) | (bc.B << 16);
                DwmSetWindowAttribute(f.Handle, 35, ref cap, 4); DwmSetWindowAttribute(f.Handle, 36, ref txt, 4); DwmSetWindowAttribute(f.Handle, 34, ref border, 4);
                int round = 2; DwmSetWindowAttribute(f.Handle, 33, ref round, 4);   // Windows 11: softly rounded window corners
            }
            catch { }
        };
        if (f.IsHandleCreated) a(); else f.HandleCreated += delegate { a(); };
        f.HandleCreated += delegate { HookFullscreen(f); }; formEpoch.Remove(f); formEpoch.Add(f, new EpochBox { E = TM.Epoch }); darkFns.Remove(f); darkFns.Add(f, new ActionBox { A = a });
    }
    // ---- a theme change reaches the windows that are already open: every colour is moved from the theme it was made under into the new one ----
    sealed class EpochBox { public int E; } sealed class ActionBox { public Action A; } sealed class ColRec { public Color Back, Fore, Border, DBack, DFore, DBorder; public bool HasBorder; }
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Form, EpochBox> formEpoch = new System.Runtime.CompilerServices.ConditionalWeakTable<Form, EpochBox>();
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Form, ActionBox> darkFns = new System.Runtime.CompilerServices.ConditionalWeakTable<Form, ActionBox>();
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, ColRec> colRecs = new System.Runtime.CompilerServices.ConditionalWeakTable<Control, ColRec>();
    // the Downloads folder of this Windows user (wherever they moved it)
    [DllImport("shell32.dll")] static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid rfid, uint dwFlags, IntPtr hToken, out IntPtr ppszPath);
    public static string DownloadsDir { get { try { IntPtr p; if (SHGetKnownFolderPath(new Guid("374DE290-123F-4565-9164-39C4925E467B"), 0, IntPtr.Zero, out p) == 0) { string d = System.Runtime.InteropServices.Marshal.PtrToStringUni(p); System.Runtime.InteropServices.Marshal.FreeCoTaskMem(p); if (!string.IsNullOrEmpty(d)) return d; } } catch { } return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"); } }
    static int lastRetheme = -1000000; static System.Windows.Forms.Timer rethemeLater;
    public static void RethemeAll()
    {
        int now = Environment.TickCount; if (unchecked(now - lastRetheme) < 220) { if (rethemeLater == null) { rethemeLater = new System.Windows.Forms.Timer { Interval = 240 }; rethemeLater.Tick += (s, e) => { rethemeLater.Stop(); lastRetheme = -1000000; RethemeAll(); }; } rethemeLater.Stop(); rethemeLater.Start(); return; } lastRetheme = now;
        var openList = new System.Collections.Generic.List<Form>(); foreach (Form of0 in Application.OpenForms) openList.Add(of0); foreach (Form f in openList) { if (f is StudioForm || f.IsDisposed) continue; try { Retheme(f); } catch { } }
    }
    public static void Retheme(Form f)
    {
        EpochBox eb; int from = formEpoch.TryGetValue(f, out eb) ? eb.E : TM.Epoch; if (from == TM.Epoch) return; RethemeTree(f, from); eb = null; if (formEpoch.TryGetValue(f, out eb)) eb.E = TM.Epoch; ActionBox ab; if (darkFns.TryGetValue(f, out ab) && f.IsHandleCreated) { try { ab.A(); } catch { } } f.Invalidate(true);
    }
    static void RethemeTree(Control c, int from)
    {
        ColRec r; if (!colRecs.TryGetValue(c, out r)) { r = new ColRec { Back = c.BackColor, Fore = c.ForeColor, DBack = TM.ToDesign(c.BackColor, from), DFore = TM.ToDesign(c.ForeColor, from) }; var bt = c as Button; if (bt != null) { r.Border = bt.FlatAppearance.BorderColor; r.DBorder = TM.ToDesign(r.Border, from); r.HasBorder = true; } try { colRecs.Add(c, r); } catch { } }
        var cbx = c as ComboBox; if (cbx != null && cbx.DroppedDown) return;   // a list that is open is left alone (changing it would shut it)
        // every colour is rebuilt from the colour it had in the original look; one the program changed by itself since (a selected button) is noted again first
        if (c.BackColor.A != 0 && c.BackColor != Color.Transparent) { if (c.BackColor != r.Back) r.DBack = TM.ToDesign(c.BackColor, from); var nb = TM.FromDesign(r.DBack); if (nb != c.BackColor) c.BackColor = nb; r.Back = nb; }
        { if (c.ForeColor != r.Fore) r.DFore = TM.ToDesign(c.ForeColor, from); var nf = TM.FromDesign(r.DFore); if (nf != c.ForeColor) c.ForeColor = nf; r.Fore = nf; }
        var b2 = c as Button; if (b2 != null && r.HasBorder && b2.FlatStyle == FlatStyle.Flat && r.Border.A != 0) { if (b2.FlatAppearance.BorderColor != r.Border) r.DBorder = TM.ToDesign(b2.FlatAppearance.BorderColor, from); var nbd = TM.FromDesign(r.DBorder); b2.FlatAppearance.BorderColor = nbd; r.Border = nbd; }
        foreach (Control k in c.Controls) RethemeTree(k, from);
        c.Invalidate();
    }

    // F11 toggles full screen (Esc leaves it) in every resizable window.
    static readonly System.Collections.Generic.Dictionary<Form, Rectangle> fsBounds = new System.Collections.Generic.Dictionary<Form, Rectangle>();
    static readonly System.Collections.Generic.Dictionary<Form, FormWindowState> fsState = new System.Collections.Generic.Dictionary<Form, FormWindowState>();
    static readonly System.Collections.Generic.HashSet<Form> fsHooked = new System.Collections.Generic.HashSet<Form>();
    public static bool IsFullscreen(Form f) { return fsBounds.ContainsKey(f); }
    // true when the window fills the screen in any way (our full screen, or maximised with the title bar button): the button then says MINIMISE
    public static bool IsFull(Form f) { return IsFullscreen(f) || f.WindowState == FormWindowState.Maximized; }
    public static string FullLabel(Form f) { return IsFull(f) ? "MINIMISE" : "FULLSCREEN"; }
    public static void HookFullscreen(Form f)
    {
        if (f.FormBorderStyle != FormBorderStyle.Sizable || !f.MaximizeBox || fsHooked.Contains(f)) return;
        fsHooked.Add(f); f.KeyPreview = true;
        f.KeyDown += (s, e) => { if (e.KeyCode == Keys.F11) { ToggleFullscreen(f); e.Handled = true; } else if (e.KeyCode == Keys.Escape && IsFullscreen(f)) { ToggleFullscreen(f); e.Handled = true; } };
        f.FormClosed += (s, e) => { fsBounds.Remove(f); fsState.Remove(f); fsHooked.Remove(f); };
    }
    public static void ToggleFullscreen(Form f)
    {
        if (IsFullscreen(f))
        {
            // always back to a normal window (never to maximised: that is what made it look like it was still full screen, just without the border)
            Rectangle b = fsBounds[f]; fsBounds.Remove(f); fsState.Remove(f);
            f.WindowState = FormWindowState.Normal; f.FormBorderStyle = FormBorderStyle.Sizable; f.Bounds = SaneBounds(f, b);
        }
        else if (f.WindowState == FormWindowState.Maximized)
        {
            Rectangle b = f.RestoreBounds; f.WindowState = FormWindowState.Normal; f.Bounds = SaneBounds(f, b);   // maximised with the title bar button: MINIMISE puts it back to a normal window
        }
        else
        {
            fsState[f] = f.WindowState; fsBounds[f] = f.WindowState == FormWindowState.Normal ? f.Bounds : f.RestoreBounds;
            var scr = Screen.FromControl(f).Bounds; f.WindowState = FormWindowState.Normal; f.FormBorderStyle = FormBorderStyle.None; f.Bounds = scr;
        }
    }

    // the window size to come back to: the old one, unless that was as big as the screen (then a comfortable size in the middle)
    static Rectangle SaneBounds(Form f, Rectangle b)
    {
        var wa = Screen.FromControl(f).WorkingArea; int w = b.Width, h = b.Height;
        if (w >= wa.Width - 40 || h >= wa.Height - 40 || w < 480 || h < 360) { w = (int)(wa.Width * 0.82); h = (int)(wa.Height * 0.84); b = new Rectangle(wa.X + (wa.Width - w) / 2, wa.Y + (wa.Height - h) / 2, w, h); }
        else if (!wa.IntersectsWith(b)) b = new Rectangle(wa.X + (wa.Width - w) / 2, wa.Y + (wa.Height - h) / 2, w, h);
        return b;
    }

    // Dark scroll bars / list popups for a control.
    public static void DarkControl(Control c)
    {
        Action a = delegate () { try { SetWindowTheme(c.Handle, "DarkMode_Explorer", null); } catch { } };
        if (c.IsHandleCreated) a(); else c.HandleCreated += delegate { a(); };
    }

    // brings a window to the front, restoring it if it was minimised
    public static void Bring(Form f)
    {
        if (f == null || f.IsDisposed) return; if (!f.Visible) f.Show(); if (f.WindowState == FormWindowState.Minimized) f.WindowState = FormWindowState.Normal; f.Activate(); f.BringToFront();
    }

    // ---- the look of the writing: ALL CAPITALS in a bold, techy font (Bahnschrift, which every Windows 10 / 11 PC has; Segoe UI if it is missing) ----
    static string uiFamily, uiBoldFamily;
    public static string UiFamily { get { Fonts0(); return uiFamily; } }
    public static string UiBoldFamily { get { Fonts0(); return uiBoldFamily; } }
    static void Fonts0()
    {
        if (uiFamily != null) return;
        bool ok = false; try { using (var f = new Font("Bahnschrift SemiBold", 10f)) ok = f.Name == "Bahnschrift SemiBold"; } catch { }
        uiFamily = ok ? "Bahnschrift SemiBold" : "Segoe UI Semibold"; uiBoldFamily = ok ? "Bahnschrift" : "Segoe UI";
    }
    public static Font UiFont(float points, bool bold = false) { Fonts0(); return new Font(bold ? uiBoldFamily : uiFamily, points, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Point); }
    // capitals, but units keep their usual spelling after a number (dB, Hz, kHz, ms)
    static readonly System.Collections.Generic.Dictionary<string, string> upCache = new System.Collections.Generic.Dictionary<string, string>();
    public static string Up(string s)
    {   // (the answer is kept: working it out with a pattern for every button on every frame was slow)
        if (string.IsNullOrEmpty(s)) return s; string r; lock (upCache) { if (upCache.TryGetValue(s, out r)) return r; r = UpSlow(s); if (upCache.Count > 6000) upCache.Clear(); upCache[s] = r; } return r;
    }
    static string UpSlow(string s)
    {
        string u = s.ToUpperInvariant();
        return System.Text.RegularExpressions.Regex.Replace(u, @"(?<=[0-9.]\s?)(DB|KHZ|HZ|MS)\b", m => m.Value == "DB" ? "dB" : (m.Value == "KHZ" ? "kHz" : (m.Value == "HZ" ? "Hz" : "ms")));
    }

    // Buttons, tick boxes and short labels of a window: capitals in the bold font (and they stay that way when the program changes their text later).
    // Long sentences keep their normal letters so they stay easy to read.
    public static void CapsAll(Control root)
    {
        foreach (Control c in root.Controls)
        {
            if (c is Button || c is CheckBox || c is RadioButton || c is Label || c is GroupBox)
            {
                if (!(c.Tag is string && (string)c.Tag == "nocaps"))
                {
                    var k = c; Action fix = () => { string t = k.Text; if (t != null && t.Length > 0 && t.Length <= 40 && t.IndexOf('\n') < 0) { string u = Up(t); if (u != t) k.Text = u; } };
                    fix(); k.TextChanged += (s, e) => fix(); float pt = k.Font.SizeInPoints; bool bold = k.Font.Bold || k is Button; try { k.Font = UiFont(pt, bold && !(k is Label && pt < 10 && !k.Font.Bold)); } catch { }
                }
            }
            if (c.HasChildren) CapsAll(c);
        }
    }

    // A one-line box with an exact size (a plain one-line TextBox shrinks to the height of its text, which pulls its frame and the writing above it apart on some PCs).
    // It is a one-line box in a multi-line control whose text is centred up and down.
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, ref RECT r);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int L, T, R, B; }
    public static void FixedBox(TextBox tb)
    {
        tb.Multiline = true; tb.WordWrap = false; tb.AcceptsReturn = false; tb.ScrollBars = ScrollBars.None;
        Action fit = () => { if (!tb.IsHandleCreated) return; int fh = TextRenderer.MeasureText("Ag", tb.Font).Height, top = Math.Max(1, (tb.ClientSize.Height - fh) / 2); var r = new RECT { L = 6, T = top, R = Math.Max(10, tb.ClientSize.Width - 6), B = Math.Max(top + 4, tb.ClientSize.Height - 1) }; SendMessage(tb.Handle, 0xB3, IntPtr.Zero, ref r); };
        tb.HandleCreated += (s, e) => fit(); tb.SizeChanged += (s, e) => fit(); tb.FontChanged += (s, e) => fit();
        tb.KeyPress += (s, e) => { if (e.KeyChar == '\r' || e.KeyChar == '\n') e.Handled = true; };
    }

    // Moves a file to the Recycle Bin (so deleting a project from Purphex can be undone from Windows).
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct ShFile64 { public IntPtr hwnd; public uint wFunc; [MarshalAs(UnmanagedType.LPWStr)] public string pFrom; [MarshalAs(UnmanagedType.LPWStr)] public string pTo; public ushort fFlags; public int fAnyOperationsAborted; public IntPtr hNameMappings; [MarshalAs(UnmanagedType.LPWStr)] public string lpszProgressTitle; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)] struct ShFile32 { public IntPtr hwnd; public uint wFunc; [MarshalAs(UnmanagedType.LPWStr)] public string pFrom; [MarshalAs(UnmanagedType.LPWStr)] public string pTo; public ushort fFlags; public int fAnyOperationsAborted; public IntPtr hNameMappings; [MarshalAs(UnmanagedType.LPWStr)] public string lpszProgressTitle; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHFileOperationW")] static extern int ShOp64(ref ShFile64 f);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHFileOperationW")] static extern int ShOp32(ref ShFile32 f);
    public static bool Recycle(string path)
    {
        try
        {
            if (!System.IO.File.Exists(path)) return true; const ushort flags = 0x0040 | 0x0010 | 0x0004 | 0x0400;   // allow undo (Recycle Bin), no questions, no windows
            int r; if (IntPtr.Size == 8) { var f = new ShFile64 { wFunc = 3, pFrom = path + "\0\0", fFlags = flags }; r = ShOp64(ref f); } else { var f = new ShFile32 { wFunc = 3, pFrom = path + "\0\0", fFlags = flags }; r = ShOp32(ref f); }
            return r == 0 && !System.IO.File.Exists(path);
        }
        catch { return false; }
    }

    // ---- neon boxes: every text and number box in the app gets a thin glowing border (dark inside, bright text) ----
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> neonBoxes = new System.Runtime.CompilerServices.ConditionalWeakTable<Control, object>(), neonParents = new System.Runtime.CompilerServices.ConditionalWeakTable<Control, object>();
    public static void NeonBox(Control c)
    {
        if (c == null) return; var tb = c as TextBoxBase; if (tb != null) tb.BorderStyle = BorderStyle.None; var tbx = c as TextBox; if (tbx != null) EmojiOverlay.Attach(tbx);   // its emoji are drawn in colour var nu = c as NumericUpDown; if (nu != null) nu.BorderStyle = BorderStyle.None;
        c.BackColor = TM.C(255, 10, 5, 24); c.ForeColor = TM.C(255, 226, 250, 255);
        object o; if (neonBoxes.TryGetValue(c, out o)) return; neonBoxes.Add(c, true);
        Action<Control> hook = p => { if (p == null) return; object x; if (neonParents.TryGetValue(p, out x)) return; neonParents.Add(p, true); p.Paint += (s, e) => PaintNeonChildren(p, e.Graphics); p.Invalidate(); };
        c.ParentChanged += (s, e) => hook(c.Parent); hook(c.Parent);
        Action inv = () => { var p = c.Parent; if (p != null && p.IsHandleCreated) p.Invalidate(Rectangle.Inflate(c.Bounds, 14, 14)); };
        c.GotFocus += (s, e) => inv(); c.LostFocus += (s, e) => inv(); c.VisibleChanged += (s, e) => inv(); c.Resize += (s, e) => inv(); c.Move += (s, e) => inv(); c.EnabledChanged += (s, e) => inv();
    }
    public static void PaintNeonChildren(Control parent, Graphics g)
    {
        if (parent == null || parent.IsDisposed || parent.Disposing) return;   // a last paint while a window is being closed: nothing to draw
        try
        {
            var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
            foreach (Control c in parent.Controls) { object o; if (!c.Visible || !neonBoxes.TryGetValue(c, out o)) continue; DrawNeonBox(g, Rectangle.Inflate(c.Bounds, 3, 2), c.ContainsFocus, c.Enabled); }
            g.SmoothingMode = sm;
        }
        catch (ArgumentException) { }   // the drawing surface was already thrown away (the window closed during the paint): skip it instead of crashing
    }
    static GraphicsPath NeonPath(RectangleF r, float rad)
    {
        var p = new GraphicsPath(); float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height)); if (d < 1) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p;
    }
    // a soft glow and a thin bright line; brighter and cyan while it is hot or being typed in
    public static Color NeonHot = TM.C(255, 0, 229, 255), NeonIdle = TM.C(255, 150, 100, 255);   // the neon boxes follow the theme
    public static void DrawNeonBox(Graphics g, RectangleF r, bool hot, bool enabled = true)
    {
        var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = NeonPath(r, 7))
        {
            int a = hot ? 64 : 26; for (int i = 3; i >= 1; i--) using (var pen = new Pen(Color.FromArgb(Math.Max(4, a / i), NeonHot), i * 3f)) g.DrawPath(pen, path);
            using (var pen = new Pen(enabled ? (hot ? Color.FromArgb(255, NeonHot) : Color.FromArgb(200, NeonIdle)) : TM.C(90, 120, 100, 160), hot ? 1.8f : 1.3f)) g.DrawPath(pen, path);
        }
        g.SmoothingMode = sm;
    }

    public static ContextMenuStrip NewMenu()
    {
        var m = new ContextMenuStrip { Renderer = new DarkRenderer(), BackColor = TM.C(255, 16, 8, 32), ForeColor = TM.C(255, 240, 232, 255), ShowImageMargin = false, Font = new Font("Segoe UI", 10f) };
        m.Font = UiFont(9.5f); Action<ToolStripItem> up = null; up = it => { if (!string.IsNullOrEmpty(it.Text) && it.Text.Length <= 70) it.Text = Up(it.Text); var mi = it as ToolStripMenuItem; if (mi != null) { mi.DropDown.ItemAdded += (s2, e2) => up(e2.Item); foreach (ToolStripItem sub in mi.DropDownItems) up(sub); } };
        m.ItemAdded += (s, e) => up(e.Item);
        m.Opened += (s, e) => { MenuShownAt = Control.MousePosition; MenuAway = false; };
        m.Closed += (s, e) => { if (e.CloseReason == ToolStripDropDownCloseReason.AppClicked) { MenuAway = true; MenuClosedAt = Environment.TickCount; } };
        return m;
    }
    // Clicking the same button again while its menu is open must close the menu, not open it a second time. The click that closes a menu
    // also reaches the window, so the window asks here whether a click lands where the menu came from, right after it was closed that way.
    static int MenuClosedAt; static Point MenuShownAt; static bool MenuAway;
    public static bool ClickClosedMenu(Control c, RectangleF region)
    {
        if (!MenuAway || unchecked(Environment.TickCount - MenuClosedAt) > 400) { MenuAway = false; return false; }
        MenuAway = false; return region.Contains(c.PointToClient(MenuShownAt));
    }

    class DarkColors : ProfessionalColorTable
    {
        public override Color MenuItemSelected { get { return TM.C(255, 96, 44, 180); } }
        public override Color MenuItemSelectedGradientBegin { get { return TM.C(255, 96, 44, 180); } }
        public override Color MenuItemSelectedGradientEnd { get { return TM.C(255, 96, 44, 180); } }
        public override Color MenuItemBorder { get { return TM.C(255, 190, 120, 255); } }
        public override Color MenuBorder { get { return TM.C(255, 120, 70, 200); } }
        public override Color ToolStripDropDownBackground { get { return TM.C(255, 16, 8, 32); } }
        public override Color ImageMarginGradientBegin { get { return TM.C(255, 16, 8, 32); } }
        public override Color ImageMarginGradientMiddle { get { return TM.C(255, 16, 8, 32); } }
        public override Color ImageMarginGradientEnd { get { return TM.C(255, 16, 8, 32); } }
        public override Color SeparatorDark { get { return TM.C(255, 80, 50, 130); } }
        public override Color SeparatorLight { get { return TM.C(255, 16, 8, 32); } }
    }
    class DarkRenderer : ToolStripProfessionalRenderer
    {
        public DarkRenderer() : base(new DarkColors()) { }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { e.TextColor = e.Item.Enabled ? TM.C(255, 240, 232, 255) : TM.C(255, 120, 105, 150); var mi = e.Item as ToolStripMenuItem; if (mi != null && mi.Checked && mi.Enabled) { e.TextColor = Color.White; e.TextRectangle = new Rectangle(e.TextRectangle.X, e.TextRectangle.Y, Math.Max(10, e.TextRectangle.Width - 24), e.TextRectangle.Height); } base.OnRenderItemText(e); }
        // the menus have neon borders like the rest of the program: a lit edge in the theme colours with a glow just inside it, a bright bar on the item you point at, and neon lines between the groups
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (!(e.ToolStrip is ToolStripDropDown)) { base.OnRenderToolStripBorder(e); return; }
            var g = e.Graphics; var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias; var r = new Rectangle(0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1); if (r.Width < 6 || r.Height < 6) { g.SmoothingMode = sm; return; }
            Color hot = NeonHot, cold = NeonIdle;
            for (int i = 4; i >= 1; i--) using (var pen = new Pen(Color.FromArgb(14 + 14 * (5 - i), cold), i * 2.2f)) g.DrawRectangle(pen, r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);   // the glow, inside the edge
            using (var br = new LinearGradientBrush(r, hot, cold, 35f)) using (var pen = new Pen(br, 1.8f)) g.DrawRectangle(pen, r);
            using (var pen = new Pen(Color.FromArgb(120, 255, 255, 255), 1f)) g.DrawLine(pen, r.X + 8, r.Y + 1, r.Right - 8, r.Y + 1);
            g.SmoothingMode = sm;
        }
        // the item you point at is a violet bar; the item that is CHOSEN right now (the grid size in use, the microphone mode in use...) keeps a lit neon frame, a cool tint and a tick, so the two are never mixed up
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var it = e.Item; var mi = it as ToolStripMenuItem; bool chosen = mi != null && mi.Checked, hover = (it.Selected && it.Enabled) || it.Pressed;
            if (!hover && !chosen) { base.OnRenderMenuItemBackground(e); return; }
            var g = e.Graphics; var sm = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias; var r = new Rectangle(3, 1, it.Width - 7, it.Height - 3);
            using (var p = RoundRect(r, 6))
            {
                if (hover) using (var br = new LinearGradientBrush(r, Color.FromArgb(235, 112, 52, 205), Color.FromArgb(235, 70, 34, 150), 0f)) g.FillPath(br, p);
                else using (var br = new LinearGradientBrush(r, Color.FromArgb(70, NeonHot), Color.FromArgb(18, NeonHot), 0f)) g.FillPath(br, p);
                using (var pen = new Pen(Color.FromArgb(hover ? 230 : 200, chosen && hover ? Color.White : NeonHot), chosen ? 1.8f : 1.4f)) g.DrawPath(pen, p);
            }
            if (chosen)
            {
                using (var br = new SolidBrush(NeonHot)) g.FillRectangle(br, r.X + 1, r.Y + 5, 3, r.Height - 10);   // a bar on the left edge
                int cx = r.Right - 18, cy = r.Y + r.Height / 2; using (var pen = new Pen(Color.White, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }) g.DrawLines(pen, new[] { new Point(cx - 5, cy), new Point(cx - 1, cy + 4), new Point(cx + 6, cy - 5) });   // the tick
            }
            g.SmoothingMode = sm;
        }
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var g = e.Graphics; int y = e.Item.Height / 2; var r = new Rectangle(8, y, e.Item.Width - 16, 1); if (r.Width < 4) return;
            using (var br = new LinearGradientBrush(r, NeonHot, NeonIdle, 0f)) using (var pen = new Pen(br, 1.2f)) g.DrawLine(pen, r.X, y, r.Right, y);
        }
        static GraphicsPath RoundRect(Rectangle r, int rad) { var p = new GraphicsPath(); int d = rad * 2; p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90); p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90); p.CloseFigure(); return p; }
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) { e.ArrowColor = TM.C(255, 200, 160, 255); base.OnRenderArrow(e); }
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) { using (var b = new SolidBrush(TM.C(255, 190, 120, 255))) e.Graphics.FillEllipse(b, e.ImageRectangle.X + 4, e.ImageRectangle.Y + 4, e.ImageRectangle.Width - 8, e.ImageRectangle.Height - 8); }
    }
}

// Dark, glowing drop-down list (replaces the white system combo box).
class DarkCombo : ComboBox
{
    bool hot;
    public DarkCombo()
    {
        DropDownStyle = ComboBoxStyle.DropDownList; DrawMode = DrawMode.OwnerDrawFixed; FlatStyle = FlatStyle.Flat; ItemHeight = 22;
        BackColor = TM.C(255, 10, 5, 20); ForeColor = TM.C(255, 240, 232, 255); Win.DarkControl(this);
    }
    protected override void OnMouseEnter(EventArgs e) { hot = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hot = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0) return; bool sel = (e.State & DrawItemState.Selected) != 0;
        using (var b = new SolidBrush(sel ? TM.C(255, 104, 48, 196) : TM.C(255, 14, 7, 28))) e.Graphics.FillRectangle(b, e.Bounds);
        TextRenderer.DrawText(e.Graphics, Convert.ToString(Items[e.Index]), Font, new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height), TM.C(255, 240, 232, 255), TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }
    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == 0xF || m.Msg == 0x14)   // WM_PAINT / WM_ERASEBKGND: paint our own look on top
        {
            try
            {
                using (var g = Graphics.FromHwnd(Handle))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias; var r = new Rectangle(0, 0, Width - 1, Height - 1);
                    using (var b = new LinearGradientBrush(r, TM.C(255, 18, 9, 36), TM.C(255, 8, 4, 16), 90f)) g.FillRectangle(b, r);
                    using (var pen = new Pen(hot || Focused ? TM.C(255, 200, 140, 255) : TM.C(255, 120, 70, 200), 1.4f)) g.DrawRectangle(pen, r);
                    TextRenderer.DrawText(g, Text, Font, new Rectangle(8, 0, Width - 30, Height), TM.C(255, 240, 232, 255), TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
                    using (var pen = new Pen(TM.C(255, 210, 160, 255), 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    { int cx = Width - 14, cy = Height / 2; g.DrawLine(pen, cx - 4, cy - 2, cx, cy + 2); g.DrawLine(pen, cx, cy + 2, cx + 4, cy - 2); }
                }
            }
            catch { }
        }
    }
}

// Neon check box on a dark pill.
class DarkCheck : CheckBox
{
    public DarkCheck() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); ForeColor = TM.C(255, 240, 232, 255); BackColor = TM.C(255, 14, 7, 28); }
    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(TM.C(255, 14, 7, 28));
        var box = new Rectangle(4, (Height - 16) / 2, 16, 16);
        bool hot = ClientRectangle.Contains(PointToClient(Cursor.Position));
        if (Checked)
        {
            using (var gl = new SolidBrush(TM.C(60, 190, 110, 255))) g.FillEllipse(gl, box.X - 5, box.Y - 5, box.Width + 10, box.Height + 10);
            using (var b = new LinearGradientBrush(box, TM.C(255, 150, 60, 240), TM.C(255, 236, 92, 226), 45f)) g.FillRectangle(b, box);
            using (var pen = new Pen(Color.White, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) { g.DrawLine(pen, box.X + 3, box.Y + 8, box.X + 7, box.Y + 12); g.DrawLine(pen, box.X + 7, box.Y + 12, box.X + 13, box.Y + 4); }
        }
        else using (var b = new SolidBrush(TM.C(255, 6, 3, 12))) g.FillRectangle(b, box);
        using (var pen = new Pen(hot ? TM.C(255, 230, 170, 255) : TM.C(255, 150, 90, 220), 1.3f)) g.DrawRectangle(pen, box);
        TextRenderer.DrawText(g, Text, Font, new Rectangle(26, 0, Width - 28, Height), ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        if (Overlay != null) Overlay(g);
    }
    public Action<Graphics> Overlay;   // drawn last (the edition padlock)
}

// High-precision frame pacing (about 1 ms timer resolution) instead of the ~15 ms WinForms timer.
// The loop only posts a new frame when the previous one has finished, so a slow PC never queues up work.
class FrameLoop : IDisposable
{
    [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint ms);
    [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint ms);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] static extern int GetDeviceCaps(IntPtr dc, int idx);

    // refresh rate of the primary display (Hz); 60 if Windows doesn't say
    public static int DisplayHz()
    {
        try { IntPtr dc = GetDC(IntPtr.Zero); int hz = GetDeviceCaps(dc, 116); ReleaseDC(IntPtr.Zero, dc); return hz > 30 && hz < 500 ? hz : 60; } catch { return 60; }
    }

    public int TargetFps = 120; public Func<bool> Slow;
    readonly Control owner; readonly Action tick; System.Threading.Thread th; volatile bool run = true, pending; volatile float lastTickMs;   // how long the last frame took

    public FrameLoop(Control owner, Action tick)
    {
        this.owner = owner; this.tick = tick; TargetFps = Math.Max(30, Math.Min(60, DisplayHz()));   // 60 is plenty for this style of animation and leaves the UI thread time to respond
        try { timeBeginPeriod(1); } catch { }
        th = new System.Threading.Thread(Loop) { IsBackground = true, Priority = System.Threading.ThreadPriority.AboveNormal }; th.Start();
    }

    void Loop()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew(); double next = 0;
        while (run)
        {
            double interval = 1000.0 / ((Slow != null && Slow()) ? 30 : TargetFps); interval = Math.Max(interval, lastTickMs * 1.35); next += interval;   // a frame that takes 12 ms is followed by at least 4 ms of rest, whatever FPS was asked for: the mouse is never starved
            if (!pending && owner.IsHandleCreated && !owner.IsDisposed)
            {
                pending = true;
                try { owner.BeginInvoke(new Action(() => { long t0 = System.Diagnostics.Stopwatch.GetTimestamp(); try { tick(); } catch { } finally { lastTickMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency); pending = false; } })); } catch { pending = false; }
            }
            double wait = next - sw.Elapsed.TotalMilliseconds;
            if (wait > 2) System.Threading.Thread.Sleep((int)(wait - 1)); else if (wait < -100) next = sw.Elapsed.TotalMilliseconds;
            if (TargetFps >= 120) { while (next - sw.Elapsed.TotalMilliseconds > 0.05) System.Threading.Thread.SpinWait(60); }   // fast screens: the last part of the wait is exact, so frames arrive evenly
        }
    }

    public void Dispose() { run = false; try { th.Join(300); } catch { } try { timeEndPeriod(1); } catch { } }
}
// Counts painted frames and draws a small badge: used in the converter window and on every Purphex Music screen.
sealed class FpsCounter
{
    int frames; long t0 = System.Diagnostics.Stopwatch.GetTimestamp(); public int Fps; static int hz;
    public void Frame()
    {
        long now = System.Diagnostics.Stopwatch.GetTimestamp(); frames++; double s = (now - t0) / (double)System.Diagnostics.Stopwatch.Frequency;
        if (s >= 0.5) { Fps = (int)(frames / s + 0.5); frames = 0; t0 = now; }
    }
    public void Draw(Graphics g, float right, float bottom, bool idle)
    {
        if (!Prefs.ShowFps) return; if (hz == 0) hz = FrameLoop.DisplayHz(); int target = Math.Min(Prefs.FpsEff, hz); string text = idle ? "idle" : Fps + " FPS";
        Color c = idle ? TM.C(255, 150, 140, 190) : (Fps >= target * 0.85 ? Color.FromArgb(110, 255, 150) : (Fps >= target * 0.45 ? Color.FromArgb(255, 210, 80) : Color.FromArgb(255, 100, 120)));
        var f = GfxCache.Font(12, true); var sz = g.MeasureString(text, f); float w = sz.Width + 30, h = 22; var r = new RectangleF(right - w, bottom - h, w, h);
        var oldS = g.SmoothingMode; g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var b = new SolidBrush(TM.C(210, 8, 4, 20))) g.FillRectangle(b, r); using (var p = new Pen(Color.FromArgb(180, c), 1.2f)) g.DrawRectangle(p, r.X, r.Y, r.Width, r.Height);
        using (var b = new SolidBrush(c)) { g.FillEllipse(b, r.X + 8, r.Y + 7, 8, 8); g.DrawString(text, f, b, r.X + 21, r.Y + 3); }
        g.SmoothingMode = oldS;
    }
}

// Shared drawing resources. Creating a Font / StringFormat for every piece of text on every frame was the main cost of the custom-drawn windows.
static class GfxCache
{
    static readonly System.Collections.Generic.Dictionary<int, Font> fonts = new System.Collections.Generic.Dictionary<int, Font>();
    static readonly System.Collections.Generic.Dictionary<int, StringFormat> fmts = new System.Collections.Generic.Dictionary<int, StringFormat>();
    public static void Clear() { lock (fonts) fonts.Clear(); }   // a new font was chosen (the old ones are left to the garbage collector: a picture being drawn may still hold one)
    // Writing is as big as the boxes allow: the small sizes are made up to about a fifth bigger (the smallest the most, big headings not at all). The places that fit a name into a box measure with this same font, so a longer one still shrinks to fit.
    public static float Grow(float px) { if (px >= 22f) return px; float t = (22f - Math.Max(9f, px)) / 13f; return px * (1f + GrowMax * t); }
    public static float GrowMax = 0.22f;
    // a font of exactly this size (writing that is fitted to its box picks its own size between the grown one and the old one)
    public static Font Exact(float px, bool bold)
    {
        int k = -((int)(px * 10) * 2 + (bold ? 1 : 0)) - 1; Font f;
        lock (fonts) { if (!fonts.TryGetValue(k, out f)) { f = FontKit.Make(px, bold); fonts[k] = f; } }
        return f;
    }
    public static Font Font(float px, bool bold)
    {
        int k = (int)(px * 10) * 2 + (bold ? 1 : 0); Font f;
        lock (fonts) { if (!fonts.TryGetValue(k, out f)) { f = FontKit.Make(px, bold); fonts[k] = f; } }
        return f;
    }
    public static StringFormat Fmt(StringAlignment ha, bool ellipsis)
    {
        int k = (int)ha * 2 + (ellipsis ? 1 : 0); StringFormat s;
        lock (fmts) { if (!fmts.TryGetValue(k, out s)) { s = new StringFormat { Alignment = ha, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap, Trimming = ellipsis ? StringTrimming.EllipsisCharacter : StringTrimming.None }; fmts[k] = s; } }
        return s;
    }
}
