// Stand-in for the Windows web view on macOS / Linux, so the Music Hub and the editors compile. Starting it reports "no web runtime",
// which the hub already handles. A real cross-platform web view replaces this in Phase 4.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.Web.WebView2.Core
{
    public class WebView2RuntimeNotFoundException : Exception { public WebView2RuntimeNotFoundException(string m) : base(m) { } }
    public enum CoreWebView2CapturePreviewImageFormat { Png, Jpeg }
    public enum CoreWebView2HostResourceAccessKind { Deny, Allow, DenyCors }
    public enum CoreWebView2WebErrorStatus { Unknown, OperationCanceled, ConnectionAborted }
    public enum CoreWebView2WebResourceContext { All, Document, Media, Image, Fetch, XmlHttpRequest }
    public enum CoreWebView2PermissionState { Default, Allow, Deny }
    public enum CoreWebView2PermissionKind { UnknownPermission, Microphone, Camera, Geolocation, Notifications, OtherSensors, ClipboardRead }
    public class CoreWebView2EnvironmentOptions { public CoreWebView2EnvironmentOptions(string args = null) { AdditionalBrowserArguments = args; } public string AdditionalBrowserArguments { get; set; } public string Language { get; set; } }
    public class CoreWebView2HttpRequestHeaders : IEnumerable<KeyValuePair<string, string>> { readonly Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); public bool Contains(string n) { return d.ContainsKey(n); } public string GetHeader(string n) { string v; return d.TryGetValue(n, out v) ? v : null; } public void SetHeader(string n, string v) { d[n] = v; } public IEnumerator<KeyValuePair<string, string>> GetEnumerator() { return d.GetEnumerator(); } System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return d.GetEnumerator(); } }
    public class CoreWebView2WebResourceRequest { public string Uri { get; set; } public string Method { get; set; } public Stream Content { get; set; } public CoreWebView2HttpRequestHeaders Headers { get; } = new CoreWebView2HttpRequestHeaders(); }
    public class CoreWebView2WebResourceResponse { public Stream Content { get; set; } public int StatusCode { get; set; } public string ReasonPhrase { get; set; } public CoreWebView2HttpRequestHeaders Headers { get; } = new CoreWebView2HttpRequestHeaders(); }
    public class CoreWebView2Deferral { public void Complete() { } }
    public class CoreWebView2WebResourceRequestedEventArgs : EventArgs { public CoreWebView2WebResourceRequest Request { get; set; } public CoreWebView2WebResourceResponse Response { get; set; } public CoreWebView2WebResourceContext ResourceContext { get; set; } public CoreWebView2Deferral GetDeferral() { return new CoreWebView2Deferral(); } }
    public class CoreWebView2NavigationCompletedEventArgs : EventArgs { public bool IsSuccess { get; set; } public CoreWebView2WebErrorStatus WebErrorStatus { get; set; } public int HttpStatusCode { get; set; } public ulong NavigationId { get; set; } }
    public class CoreWebView2NavigationStartingEventArgs : EventArgs { public string Uri { get; set; } public bool Cancel { get; set; } public bool IsUserInitiated { get; set; } public bool IsRedirected { get; set; } public ulong NavigationId { get; set; } }
    public class CoreWebView2WindowFeatures { public bool HasSize { get; set; } public bool HasPosition { get; set; } public uint Width { get; set; } public uint Height { get; set; } public uint Left { get; set; } public uint Top { get; set; } }
    public class CoreWebView2NewWindowRequestedEventArgs : EventArgs { public string Uri { get; set; } public bool Handled { get; set; } public CoreWebView2 NewWindow { get; set; } public bool IsUserInitiated { get; set; } public CoreWebView2WindowFeatures WindowFeatures { get; } = new CoreWebView2WindowFeatures(); public CoreWebView2Deferral GetDeferral() { return new CoreWebView2Deferral(); } }
    public class CoreWebView2File { public string Path { get; set; } }
    public class CoreWebView2ObjectCollectionView : List<object> { }
    public class CoreWebView2WebMessageReceivedEventArgs : EventArgs { public string Source { get; set; } public string WebMessageAsJson { get; set; } public string TryGetWebMessageAsString() { return null; } public CoreWebView2ObjectCollectionView AdditionalObjects { get; } = new CoreWebView2ObjectCollectionView(); }
    public class CoreWebView2PermissionRequestedEventArgs : EventArgs { public string Uri { get; set; } public CoreWebView2PermissionKind PermissionKind { get; set; } public CoreWebView2PermissionState State { get; set; } public bool Handled { get; set; } }
    public class CoreWebView2ProcessFailedEventArgs : EventArgs { }
    public class CoreWebView2DownloadStartingEventArgs : EventArgs { public bool Cancel { get; set; } public bool Handled { get; set; } public string ResultFilePath { get; set; } }
    public class CoreWebView2ContextMenuRequestedEventArgs : EventArgs { public bool Handled { get; set; } }
    public class CoreWebView2Settings { public bool AreDevToolsEnabled { get; set; } public bool AreDefaultContextMenusEnabled { get; set; } public bool IsStatusBarEnabled { get; set; } public bool IsZoomControlEnabled { get; set; } public bool AreBrowserAcceleratorKeysEnabled { get; set; } public bool IsScriptEnabled { get; set; } = true; public bool IsWebMessageEnabled { get; set; } = true; public bool AreDefaultScriptDialogsEnabled { get; set; } = true; public bool IsGeneralAutofillEnabled { get; set; } public bool IsPasswordAutosaveEnabled { get; set; } public bool IsPinchZoomEnabled { get; set; } public bool IsSwipeNavigationEnabled { get; set; } public bool AreHostObjectsAllowed { get; set; } public bool IsBuiltInErrorPageEnabled { get; set; } public string UserAgent { get; set; } }
    public class CoreWebView2Cookie { }
    public class CoreWebView2CookieManager { public void DeleteAllCookies() { } public void DeleteCookies(string name, string uri) { } public Task<List<CoreWebView2Cookie>> GetCookiesAsync(string uri) { return Task.FromResult(new List<CoreWebView2Cookie>()); } }
#pragma warning disable 67
    public class CoreWebView2
    {
        public CoreWebView2Settings Settings { get; } = new CoreWebView2Settings(); public CoreWebView2CookieManager CookieManager { get; } = new CoreWebView2CookieManager();
        public bool CanGoBack { get { return false; } } public bool CanGoForward { get { return false; } } public bool IsMuted { get; set; } public string Source { get { return ""; } } public string DocumentTitle { get { return ""; } }
        public event EventHandler<CoreWebView2NavigationCompletedEventArgs> NavigationCompleted; public event EventHandler<CoreWebView2NavigationStartingEventArgs> NavigationStarting; public event EventHandler<CoreWebView2NewWindowRequestedEventArgs> NewWindowRequested; public event EventHandler<CoreWebView2WebMessageReceivedEventArgs> WebMessageReceived; public event EventHandler<CoreWebView2WebResourceRequestedEventArgs> WebResourceRequested; public event EventHandler<CoreWebView2PermissionRequestedEventArgs> PermissionRequested; public event EventHandler<CoreWebView2ProcessFailedEventArgs> ProcessFailed; public event EventHandler<CoreWebView2DownloadStartingEventArgs> DownloadStarting; public event EventHandler<CoreWebView2ContextMenuRequestedEventArgs> ContextMenuRequested; public event EventHandler<object> DocumentTitleChanged, SourceChanged, HistoryChanged, ContentLoading, DOMContentLoaded, IsMutedChanged, IsDocumentPlayingAudioChanged, ContainsFullScreenElementChanged;
        public void Navigate(string u) { } public void NavigateToString(string html) { } public void GoBack() { } public void GoForward() { } public void Reload() { } public void Stop() { } public void PostWebMessageAsString(string m) { } public void PostWebMessageAsJson(string m) { } public void OpenDevToolsWindow() { }
        public Task<string> ExecuteScriptAsync(string s) { return Task.FromResult("null"); } public Task<string> AddScriptToExecuteOnDocumentCreatedAsync(string s) { return Task.FromResult(""); } public Task<string> CallDevToolsProtocolMethodAsync(string m, string p) { return Task.FromResult("{}"); } public Task CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat f, Stream s) { return Task.FromResult(0); }
        public void AddWebResourceRequestedFilter(string uri, CoreWebView2WebResourceContext c) { } public void SetVirtualHostNameToFolderMapping(string host, string folder, CoreWebView2HostResourceAccessKind k) { } public void ClearVirtualHostNameToFolderMapping(string host) { }
    }
    public class CoreWebView2Environment
    {
        internal const string Soon = "The built-in web view is coming to macOS and Linux soon.";
        public static void SetLoaderDllFolderPath(string p) { }
        public static string GetAvailableBrowserVersionString(string folder = null) { throw new WebView2RuntimeNotFoundException(Soon); }
        public static Task<CoreWebView2Environment> CreateAsync(string browserFolder = null, string userDataFolder = null, CoreWebView2EnvironmentOptions options = null) { throw new WebView2RuntimeNotFoundException(Soon); }
        public CoreWebView2WebResourceResponse CreateWebResourceResponse(Stream content, int status, string reason, string headers) { return new CoreWebView2WebResourceResponse { Content = content, StatusCode = status, ReasonPhrase = reason }; }
        public string BrowserVersionString { get { return ""; } }
    }
    public class CoreWebView2InitializationCompletedEventArgs : EventArgs { public bool IsSuccess { get; set; } public Exception InitializationException { get; set; } }
}
namespace Microsoft.Web.WebView2.WinForms
{
    using Microsoft.Web.WebView2.Core;
    public class WebView2 : System.Windows.Forms.Control
    {
        public CoreWebView2 CoreWebView2 { get; private set; } public Uri Source { get; set; } public double ZoomFactor { get; set; } = 1; public System.Drawing.Color DefaultBackgroundColor { get; set; } public CoreWebView2CreationProperties CreationProperties { get; set; }
        public event EventHandler<CoreWebView2InitializationCompletedEventArgs> CoreWebView2InitializationCompleted; public event EventHandler<CoreWebView2NavigationCompletedEventArgs> NavigationCompleted;
        public Task EnsureCoreWebView2Async(CoreWebView2Environment env = null) { throw new WebView2RuntimeNotFoundException(CoreWebView2Environment.Soon); }
        public Task<string> ExecuteScriptAsync(string s) { return Task.FromResult("null"); }
        public void Reload() { } public void GoBack() { } public void GoForward() { } public void NavigateToString(string h) { }
    }
#pragma warning restore 67
    public class CoreWebView2CreationProperties { public string UserDataFolder { get; set; } public string BrowserExecutableFolder { get; set; } public string AdditionalBrowserArguments { get; set; } }
}
