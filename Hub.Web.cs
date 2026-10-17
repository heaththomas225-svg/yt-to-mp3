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
    public Action<string> Message;   // a page of our own (the video editor) talking to Purphex
    public Action<string, List<string>> MessageFiles;   // files dropped on the page: Purphex is handed their real paths (the page never sees them)
    public Func<string, byte[], string> Upload;   // https://export.purphex/...: the page sends rendered frames, audio and pictures (null = OK, else the error)
    public Func<string, string> Serve;   // https://media.creative/<id>/<name>: the media files you imported (only those), with byte ranges so videos can seek
    CoreWebView2Environment env;
    readonly List<string[]> hosts = new List<string[]>();
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
            await wv.EnsureCoreWebView2Async(env); this.env = env;
            var c = wv.CoreWebView2; c.IsMuted = muted; c.Settings.AreDevToolsEnabled = false; c.Settings.IsStatusBarEnabled = false; c.Settings.IsZoomControlEnabled = true; c.Settings.IsGeneralAutofillEnabled = false; c.Settings.IsPasswordAutosaveEnabled = false;
            c.NavigationStarting += (s, e) => { Loading = true; Error = ""; Changed(); };
            c.NavigationCompleted += (s, e) => { Loading = false; if (!e.IsSuccess && e.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled) Error = "The page did not load (" + e.WebErrorStatus + "). Check the internet connection and press RELOAD."; Changed(); };
            c.SourceChanged += (s, e) => { Url = c.Source; Changed(); };
            c.DocumentTitleChanged += (s, e) => { Title = c.DocumentTitle; Changed(); };
            // a link that opens a new tab stays in the hub; a sign-in window (a pop-up with a size) opens as its own small window so the sign-in works
            c.NewWindowRequested += (s, e) => { if (!e.WindowFeatures.HasSize) { e.Handled = true; c.Navigate(e.Uri); } };
            c.WebMessageReceived += (s, e) =>
            {
                string m = null; try { m = e.TryGetWebMessageAsString(); } catch { }
                List<string> files = null; try { if (e.AdditionalObjects != null && e.AdditionalObjects.Count > 0) { files = new List<string>(); foreach (var o in e.AdditionalObjects) { var cf = o as CoreWebView2File; if (cf != null) files.Add(cf.Path); } } } catch { }
                if (files != null && MessageFiles != null) MessageFiles(m, files); else if (m != null && Message != null) Message(m);
            };
            c.AddWebResourceRequestedFilter("https://export.purphex/*", CoreWebView2WebResourceContext.All); c.AddWebResourceRequestedFilter("https://media.creative/*", CoreWebView2WebResourceContext.All);
            c.WebResourceRequested += (s, e) =>
            {
                const string cors = "Access-Control-Allow-Origin: *\r\nAccess-Control-Allow-Methods: POST, OPTIONS\r\nAccess-Control-Allow-Headers: *\r\nContent-Type: text/plain";
                if (e.Request.Method == "OPTIONS") { e.Response = env.CreateWebResourceResponse(null, 204, "No Content", cors); return; }
                if (e.Request.Uri.StartsWith("https://media.creative/", StringComparison.OrdinalIgnoreCase)) { e.Response = ServeFile(e.Request); return; }
                string err; try { byte[] body = new byte[0]; var st = e.Request.Content; if (st != null) using (var ms = new MemoryStream()) { st.CopyTo(ms); body = ms.ToArray(); } err = Upload == null ? "Nothing takes uploads here." : Upload(e.Request.Uri, body); } catch (Exception ex) { err = ex.Message; }
                var resp = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(err ?? "OK")); e.Response = env.CreateWebResourceResponse(resp, err == null ? 200 : 500, err == null ? "OK" : "Error", cors);
            };
            foreach (var h in hosts) c.SetVirtualHostNameToFolderMapping(h[0], h[1], CoreWebView2HostResourceAccessKind.Allow);
            Ready = true; Url = c.Source ?? ""; if (pending != null) { c.Navigate(pending); pending = null; }
        }
        catch (WebView2RuntimeNotFoundException) { NoRuntime = true; Error = "Microsoft Edge WebView2 Runtime is not installed."; }
        catch (Exception ex) { Error = "The music browser could not start: " + ex.Message; }
        Changed();
    }
    public void Navigate(string url) { if (string.IsNullOrEmpty(url)) return; if (Ready) { try { wv.CoreWebView2.Navigate(url); } catch (Exception ex) { Error = ex.Message; } } else pending = url; }
    static readonly Dictionary<string, string> Mime = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { ".mp4", "video/mp4" }, { ".m4v", "video/mp4" }, { ".mov", "video/quicktime" }, { ".webm", "video/webm" }, { ".mkv", "video/x-matroska" }, { ".mp3", "audio/mpeg" }, { ".wav", "audio/wav" }, { ".m4a", "audio/mp4" }, { ".aac", "audio/aac" }, { ".ogg", "audio/ogg" }, { ".opus", "audio/ogg" }, { ".flac", "audio/flac" }, { ".png", "image/png" }, { ".jpg", "image/jpeg" }, { ".jpeg", "image/jpeg" }, { ".webp", "image/webp" }, { ".gif", "image/gif" }, { ".bmp", "image/bmp" } };
    CoreWebView2WebResourceResponse ServeFile(CoreWebView2WebResourceRequest req)
    {
        const string h0 = "Access-Control-Allow-Origin: *\r\nAccept-Ranges: bytes\r\nCache-Control: no-cache";
        string path = Serve == null ? null : Serve(new Uri(req.Uri).AbsolutePath); if (path == null || !File.Exists(path)) return env.CreateWebResourceResponse(null, 404, "Not Found", h0);
        string type; if (!Mime.TryGetValue(Path.GetExtension(path), out type)) type = "application/octet-stream";
        var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 65536); long len = fs.Length, from = 0, to = len - 1; bool partial = false;
        try { string range = req.Headers.Contains("Range") ? req.Headers.GetHeader("Range") : null; if (range != null && range.StartsWith("bytes=")) { var p = range.Substring(6).Split('-'); if (p[0].Length > 0) from = long.Parse(p[0]); if (p.Length > 1 && p[1].Length > 0) to = Math.Min(len - 1, long.Parse(p[1])); else if (p[0].Length == 0 && p.Length > 1) { from = Math.Max(0, len - long.Parse(p[1])); } partial = true; } } catch { from = 0; to = len - 1; partial = false; }
        if (from >= len && len > 0) { fs.Dispose(); return env.CreateWebResourceResponse(null, 416, "Range Not Satisfiable", h0 + "\r\nContent-Range: bytes */" + len); }
        fs.Position = from; var body = new RangeStream(fs, to - from + 1);
        string h = h0 + "\r\nContent-Type: " + type + "\r\nContent-Length: " + (to - from + 1) + (partial ? "\r\nContent-Range: bytes " + from + "-" + to + "/" + len : "");
        return env.CreateWebResourceResponse(body, partial ? 206 : 200, partial ? "Partial Content" : "OK", h);
    }
    // a folder on this PC seen by the page as https://<host>/ (the video editor's own files and your media folders; nothing else is reachable)
    public void MapHost(string host, string folder)
    {
        foreach (var h in hosts) if (h[0] == host) { if (h[1] == folder) return; h[1] = folder; if (Ready) wv.CoreWebView2.SetVirtualHostNameToFolderMapping(host, folder, CoreWebView2HostResourceAccessKind.Allow); return; }
        hosts.Add(new[] { host, folder }); if (Ready) wv.CoreWebView2.SetVirtualHostNameToFolderMapping(host, folder, CoreWebView2HostResourceAccessKind.Allow);
    }
    public void Post(string json) { if (Ready) { try { wv.CoreWebView2.PostWebMessageAsString(json); } catch { } } }
    public void Focus() { try { wv.Focus(); } catch { } }
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

// a part of a file, read as a stream (the bytes a video asked for)
sealed class RangeStream : Stream
{
    readonly Stream s; long left;
    public RangeStream(Stream s, long count) { this.s = s; left = count; }
    public override int Read(byte[] buffer, int offset, int count) { if (left <= 0) return 0; int n = s.Read(buffer, offset, (int)Math.Min(count, left)); left -= n; return n; }
    public override bool CanRead { get { return true; } } public override bool CanSeek { get { return false; } } public override bool CanWrite { get { return false; } }
    public override long Length { get { return left; } } public override long Position { get { return 0; } set { throw new NotSupportedException(); } }
    public override void Flush() { } public override long Seek(long o, SeekOrigin so) { throw new NotSupportedException(); } public override void SetLength(long v) { throw new NotSupportedException(); } public override void Write(byte[] b, int o, int c) { throw new NotSupportedException(); }
    protected override void Dispose(bool d) { if (d) s.Dispose(); base.Dispose(d); }
}
