using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

// ======================================================================
//  MUSIC HUB: the browser inside Purphex. The streaming services' own web players run in Microsoft's WebView2 (the Edge engine that is
//  part of Windows): no address bar, no tabs, no borders, Purphex's own buttons around it. You sign in on each service's own page
//  (Purphex never sees a password); the sign-ins stay in a folder of your own (hubweb) and SIGN OUT clears that service's data.
//  Protected music (Spotify, Apple Music, Tidal, Amazon, Deezer) plays through the engine's own Widevine / PlayReady support: nothing is
//  bypassed, downloaded or recorded.
// ======================================================================

// The WebView2 parts (two .NET files and the small native loader) travel inside the exe and are unpacked once into the app's data folder.
static class Wv2Loader
{
    public const string Ver = "1.0.3912.50"; static bool hooked; static string dir;
    public static string Dir { get { Ensure(); return dir; } }
    public static void Ensure()
    {
        if (hooked) return; hooked = true;
        try
        {
            string d = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YtToMp3", "wv2", Ver); Directory.CreateDirectory(d); var asm = typeof(Wv2Loader).Assembly;
            bool ok = Extract(asm, "wv2.Core", Path.Combine(d, "Microsoft.Web.WebView2.Core.dll")) & Extract(asm, "wv2.WinForms", Path.Combine(d, "Microsoft.Web.WebView2.WinForms.dll")) & Extract(asm, Environment.Is64BitProcess ? "wv2.Loader64" : "wv2.Loader86", Path.Combine(d, "WebView2Loader.dll"));
            if (ok) dir = d;
        }
        catch { }
        if (dir == null)
        {   // a build without the files inside (the tests): the copies beside the program
            string dev = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wv2"); if (File.Exists(Path.Combine(dev, "Microsoft.Web.WebView2.Core.dll"))) dir = dev;
        }
        AppDomain.CurrentDomain.AssemblyResolve += (s, e) =>
        {
            string n = new AssemblyName(e.Name).Name; if (dir == null || !n.StartsWith("Microsoft.Web.WebView2", StringComparison.Ordinal)) return null;
            string p = Path.Combine(dir, n + ".dll"); return File.Exists(p) ? Assembly.LoadFrom(p) : null;
        };
    }
    static bool Extract(Assembly asm, string res, string path)
    {
        using (var st = asm.GetManifestResourceStream(res))
        {
            if (st == null) return false; if (File.Exists(path) && new FileInfo(path).Length == st.Length) return true;
            try { using (var fs = File.Create(path)) st.CopyTo(fs); return true; } catch { return File.Exists(path); }   // in use by another copy of Purphex: it is already there
        }
    }
}

// One embedded browser for the hub. Everything that touches WebView2 types lives in here, so nothing loads them until the hub is opened.
sealed class HubBrowser
{
    readonly WebView2 wv; string pending; readonly bool muted;
    public bool Ready, Loading, NoRuntime; public string Error = "", Url = "", Title = "";
    public Action Changed = delegate { };
    public Control View { get { return wv; } }
    public bool CanBack { get { return Ready && wv.CoreWebView2.CanGoBack; } }
    public bool CanForward { get { return Ready && wv.CoreWebView2.CanGoForward; } }

    // true when the WebView2 runtime is on this PC (it comes with Windows 11 and recent Windows 10)
    static bool loaderSet;   // the loader's folder can be given only once (a second time throws)
    static void LoaderPath() { if (loaderSet) return; loaderSet = true; Wv2Loader.Ensure(); if (Wv2Loader.Dir != null) CoreWebView2Environment.SetLoaderDllFolderPath(Wv2Loader.Dir); }
    public static string RuntimeVersion() { try { LoaderPath(); return CoreWebView2Environment.GetAvailableBrowserVersionString(); } catch { return null; } }

    public HubBrowser(Control parent, string userDataDir, bool mute)
    {
        muted = mute; wv = new WebView2 { Visible = false, DefaultBackgroundColor = Color.FromArgb(255, 10, 5, 24) }; parent.Controls.Add(wv); wv.BringToFront(); var hw = wv.Handle; Init(userDataDir);   // the engine starts only once the view has a window, even a hidden one
    }
    async void Init(string ud)
    {
        try
        {
            LoaderPath(); Directory.CreateDirectory(ud);
            var env = await CoreWebView2Environment.CreateAsync(null, ud, new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required"));
            await wv.EnsureCoreWebView2Async(env);
            var c = wv.CoreWebView2; c.IsMuted = muted; c.Settings.AreDevToolsEnabled = false; c.Settings.IsStatusBarEnabled = false; c.Settings.IsZoomControlEnabled = true; c.Settings.IsGeneralAutofillEnabled = false; c.Settings.IsPasswordAutosaveEnabled = false;
            c.NavigationStarting += (s, e) => { Loading = true; Error = ""; Changed(); };
            c.NavigationCompleted += (s, e) => { Loading = false; if (!e.IsSuccess && e.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled) Error = "The page did not load (" + e.WebErrorStatus + "). Check the internet connection and press RELOAD."; Changed(); };
            c.SourceChanged += (s, e) => { Url = c.Source; Changed(); };
            c.DocumentTitleChanged += (s, e) => { Title = c.DocumentTitle; Changed(); };
            // a link that opens a new tab stays in the hub; a sign-in window (a pop-up with a size) opens as its own small window so the sign-in works
            c.NewWindowRequested += (s, e) => { if (!e.WindowFeatures.HasSize) { e.Handled = true; c.Navigate(e.Uri); } };
            Ready = true; Url = c.Source ?? ""; if (pending != null) { c.Navigate(pending); pending = null; }
        }
        catch (WebView2RuntimeNotFoundException) { NoRuntime = true; Error = "Microsoft Edge WebView2 Runtime is not installed."; }
        catch (Exception ex) { Error = "The music browser could not start: " + ex.Message; }
        Changed();
    }
    public void Navigate(string url) { if (string.IsNullOrEmpty(url)) return; if (Ready) { try { wv.CoreWebView2.Navigate(url); } catch (Exception ex) { Error = ex.Message; } } else pending = url; }
    public void Back() { if (CanBack) wv.CoreWebView2.GoBack(); }
    public void Forward() { if (CanForward) wv.CoreWebView2.GoForward(); }
    public void Reload() { if (Ready) wv.CoreWebView2.Reload(); }
    public void SetBounds(Rectangle r, bool show) { if (wv.Bounds != r) wv.Bounds = r; if (wv.Visible != show) { wv.Visible = show; if (show) wv.BringToFront(); } }
    public bool Visible { get { return wv.Visible; } }
    // runs a script in the page; the answer comes back as JSON (null when there is no page yet)
    public async Task<string> Js(string script) { if (!Ready) return null; try { return await wv.CoreWebView2.ExecuteScriptAsync(script); } catch { return null; } }
    // the names of the cookies of a site (to show whether you are signed in there; the values are never read out)
    public async Task<List<string>> CookieNames(string url)
    {
        var l = new List<string>(); if (!Ready) return l;
        try { var cs = await wv.CoreWebView2.CookieManager.GetCookiesAsync(url); foreach (var k in cs) l.Add(k.Name); } catch { }
        return l;
    }
    // SIGN OUT: every cookie and every stored page data of the service's sites goes
    public async Task ClearSites(IEnumerable<string> origins)
    {
        if (!Ready) return;
        foreach (var o in origins)
        {
            try { var cs = await wv.CoreWebView2.CookieManager.GetCookiesAsync(o); foreach (var k in cs) wv.CoreWebView2.CookieManager.DeleteCookie(k); } catch { }
            try { await wv.CoreWebView2.CallDevToolsProtocolMethodAsync("Storage.clearDataForOrigin", "{\"origin\":\"" + o.TrimEnd('/') + "\",\"storageTypes\":\"all\"}"); } catch { }
        }
    }
    // a picture of the page (for the screen checks)
    public async Task<bool> Capture(string pngPath) { if (!Ready) return false; try { using (var fs = File.Create(pngPath)) await wv.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, fs); return true; } catch { return false; } }
    public void Dispose() { try { wv.Dispose(); } catch { } }
}
