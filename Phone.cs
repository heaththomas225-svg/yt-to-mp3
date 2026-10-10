using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;

// Setup dialog for phone mode: shows the address(es), explains the firewall, and can add the firewall rule.
static class PhoneUi
{
    public static void Show(IWin32Window owner, PhoneServer ps)
    {
        var f = new Form { Text = "Phone mode", FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false, BackColor = Color.FromArgb(24, 12, 44), ForeColor = Color.FromArgb(240, 232, 255), ClientSize = new Size(600, 470), Font = new Font("Segoe UI", 10f) };
        Win.Dark(f); var cands = PhoneServer.Candidates(); string best = cands.Count > 0 ? cands[0].Ip : "127.0.0.1";
        var sb = new StringBuilder();
        sb.Append("Phone mode is ON.\r\n\r\nOn your phone, connected to the SAME Wi-Fi / router as this PC (not mobile data), open this address in the browser:\r\n\r\n");
        sb.Append("    http://" + best + ":" + ps.Port + "/?c=" + ps.Code + "\r\n\r\n");
        if (cands.Count > 1)
        {
            sb.Append("If that doesn't open, this PC has other network addresses. Try the one that starts like your phone's Wi-Fi address (for example 192.168.x.x):\r\n");
            foreach (var c in cands) sb.Append("    " + c.Ip + "   (" + c.Alias + ")\r\n");
            sb.Append("   (VPN adapters such as Radmin, Tailscale or VMware are not reachable from a phone.)\r\n\r\n");
        }
        sb.Append("Still not loading? Windows Firewall blocks other devices by default, especially when your network is set to \"Public\". Press the green button below to allow it (Windows will ask for administrator permission). Also make sure your router isn't using \"guest network\" or \"AP/client isolation\".");
        var tb = new TextBox { Left = 16, Top = 14, Width = 568, Height = 330, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(34, 16, 62), ForeColor = Color.FromArgb(240, 232, 255), BorderStyle = BorderStyle.FixedSingle, Text = sb.ToString() };
        f.Controls.Add(tb);
        Func<string, int, int, int, Color, Button> mk = (text, x, y, w, col) => { var b = new Button { Text = text, Left = x, Top = y, Width = w, Height = 38, FlatStyle = FlatStyle.Flat, BackColor = col, ForeColor = Color.White, Cursor = Cursors.Hand }; b.FlatAppearance.BorderColor = Color.FromArgb(190, 120, 255); return b; };
        var fw = mk(ps.FirewallRuleExists() ? "Firewall: already allowed (click to remove)" : "Allow in Windows Firewall", 16, 356, 568, Color.FromArgb(28, 130, 90));
        var copy = mk("Copy address", 16, 404, 270, Color.FromArgb(118, 52, 212)); var ok = mk("OK", 314, 404, 270, Color.FromArgb(70, 40, 120));
        string status = "";
        fw.Click += (s, e) =>
        {
            bool exists = ps.FirewallRuleExists(); string err = ps.SetFirewallRule(!exists);
            bool now = ps.FirewallRuleExists();
            if (err != null) MessageBox.Show(f, err, "Phone mode");
            else if (!exists && now) { fw.Text = "Firewall: allowed (click to remove)"; MessageBox.Show(f, "Done. Windows Firewall now allows connections to port " + ps.Port + ". Try the address on your phone again.", "Phone mode"); }
            else if (exists && !now) { fw.Text = "Allow in Windows Firewall"; }
            else MessageBox.Show(f, "Windows didn't apply the change. You can add it yourself: open Windows Defender Firewall with Advanced Security > Inbound Rules > New Rule > Port > TCP " + ps.Port + " > Allow.", "Phone mode");
        };
        copy.Click += (s, e) => { try { Clipboard.SetText("http://" + best + ":" + ps.Port + "/?c=" + ps.Code); copy.Text = "Copied"; } catch { } };
        ok.Click += (s, e) => f.Close(); f.AcceptButton = ok; f.Controls.AddRange(new Control[] { fw, copy, ok });
        try { Clipboard.SetText("http://" + best + ":" + ps.Port + "/?c=" + ps.Code); } catch { }
        f.Show(owner);
    }
}

// ======================================================================
//  Phone mode: a tiny web server inside the app. A phone on the same Wi-Fi opens
//  http://<PC address>:8765/?c=<code>, pastes a link, and downloads the result.
//  The PC does the converting (yt-dlp + ffmpeg); the phone just uses a web page.
//  Off by default; every request needs the random code shown in the app.
// ======================================================================
class PJob
{
    public string Id = "", State = "queued", Msg = "", File = "", Name = "", Dir = ""; public int Pct; public DateTime Created = DateTime.Now;
}

class PhoneServer
{
    public int Port = 8765; public string Code = "";
    TcpListener listener; Thread th; volatile bool run;
    readonly Dictionary<string, PJob> jobs = new Dictionary<string, PJob>();
    readonly object lk = new object(); int running;
    public Action<string> Log = delegate { };

    public class NetCand { public string Alias, Ip; public int Score; }

    // Addresses this PC has, best first. VPN / virtual adapters (Radmin, VMware, Hyper-V, WSL, Tailscale...) are
    // pushed to the bottom because a phone on your Wi-Fi can't reach them.
    public static List<NetCand> Candidates()
    {
        var l = new List<NetCand>();
        try
        {
            var bad = new Regex("vpn|virtual|vmware|vbox|hyper-v|vethernet|tap-|tun|wireguard|tailscale|zerotier|radmin|bluetooth|loopback|pseudo|docker|wsl|npcap|hamachi|nordlynx", RegexOptions.IgnoreCase);
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback || ni.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                var props = ni.GetIPProperties();
                foreach (UnicastIPAddressInformation ua in props.UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue; string ip = ua.Address.ToString(); if (ip.StartsWith("169.254") || ip.StartsWith("127.")) continue;
                    int s = 0; var b = ua.Address.GetAddressBytes();
                    if (b[0] == 192 && b[1] == 168) s = 100; else if (b[0] == 10) s = 80; else if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) s = 70; else s = 5;
                    if (bad.IsMatch(ni.Name) || bad.IsMatch(ni.Description)) s = Math.Min(s, 3);
                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet) s += 5;
                    if (props.GatewayAddresses.Count > 0) s += 10;
                    l.Add(new NetCand { Alias = ni.Name, Ip = ip, Score = s });
                }
            }
        }
        catch { }
        l.Sort((a, c) => c.Score.CompareTo(a.Score));
        return l;
    }

    public static string LocalIp() { var c = Candidates(); return c.Count > 0 ? c[0].Ip : "127.0.0.1"; }

    public string Url { get { return "http://" + LocalIp() + ":" + Port + "/?c=" + Code; } }

    public const string FwRule = "YouTube to MP3 phone mode";
    public bool FirewallRuleExists()
    {
        try
        {
            var psi = new ProcessStartInfo("netsh", "advfirewall firewall show rule name=\"" + FwRule + "\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
            using (var p = Process.Start(psi)) { p.StandardOutput.ReadToEnd(); p.WaitForExit(); return p.ExitCode == 0; }
        }
        catch { return false; }
    }

    // Asks Windows for administrator permission (UAC) and adds an inbound allow rule for this port.
    public string SetFirewallRule(bool add)
    {
        try
        {
            string args = add ? "advfirewall firewall add rule name=\"" + FwRule + "\" dir=in action=allow protocol=TCP localport=" + Port + " profile=any"
                              : "advfirewall firewall delete rule name=\"" + FwRule + "\"";
            var psi = new ProcessStartInfo("netsh", args) { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
            using (var p = Process.Start(psi)) { p.WaitForExit(); }
            return null;
        }
        catch (System.ComponentModel.Win32Exception) { return "Permission was declined, so the firewall wasn't changed."; }
        catch (Exception ex) { return ex.Message; }
    }

    // wrong-code throttle per remote address
    readonly Dictionary<string, int> fails = new Dictionary<string, int>(); readonly Dictionary<string, DateTime> blocked = new Dictionary<string, DateTime>();
    bool IsBlocked(string ip) { lock (lk) { DateTime t; if (blocked.TryGetValue(ip, out t)) { if (DateTime.Now < t) return true; blocked.Remove(ip); fails.Remove(ip); } return false; } }
    void Fail(string ip) { lock (lk) { int n; fails.TryGetValue(ip, out n); n++; fails[ip] = n; if (n >= 15) blocked[ip] = DateTime.Now.AddMinutes(10); } }

    public bool Start(out string error)
    {
        error = null;
        try
        {
            var rnd = new Random(); Code = rnd.Next(100000, 999999).ToString();
            listener = new TcpListener(IPAddress.Any, Port); listener.Start(); run = true;
            th = new Thread(AcceptLoop) { IsBackground = true }; th.Start(); return true;
        }
        catch (Exception ex) { error = ex.Message; return false; }
    }

    public void Stop() { run = false; try { if (listener != null) listener.Stop(); } catch { } }

    void AcceptLoop()
    {
        while (run)
        {
            try { var c = listener.AcceptTcpClient(); ThreadPool.QueueUserWorkItem(delegate { Handle(c); }); }
            catch { if (!run) break; }
        }
    }

    // ---------- tiny HTTP ----------
    static void Send(NetworkStream ns, int status, string type, byte[] body, string extra)
    {
        string head = "HTTP/1.1 " + status + " " + (status == 200 ? "OK" : (status == 403 ? "Forbidden" : (status == 404 ? "Not Found" : "Bad Request")))
            + "\r\nContent-Type: " + type + "\r\nContent-Length: " + body.Length + "\r\nCache-Control: no-store\r\nConnection: close\r\n" + (extra ?? "") + "\r\n";
        var h = Encoding.ASCII.GetBytes(head); ns.Write(h, 0, h.Length); ns.Write(body, 0, body.Length);
    }
    static void Json(NetworkStream ns, int status, string json) { Send(ns, status, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(json), null); }
    static string Esc(string s) { return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " "); }

    static Dictionary<string, string> ParseQuery(string q)
    {
        var d = new Dictionary<string, string>();
        foreach (var p in (q ?? "").Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int i = p.IndexOf('='); string k = i < 0 ? p : p.Substring(0, i), v = i < 0 ? "" : p.Substring(i + 1);
            d[Uri.UnescapeDataString(k.Replace('+', ' '))] = Uri.UnescapeDataString(v.Replace('+', ' '));
        }
        return d;
    }

    void Handle(TcpClient c)
    {
        try
        {
            c.ReceiveTimeout = 8000; c.SendTimeout = 60000;
            string remote = ""; try { remote = ((IPEndPoint)c.Client.RemoteEndPoint).Address.ToString(); } catch { }
            using (c) using (var ns = c.GetStream())
            {
                if (IsBlocked(remote)) return;
                var buf = new List<byte>(); var one = new byte[1]; int headEnd = -1;
                while (buf.Count < 16384 && headEnd < 0)
                {
                    if (ns.Read(one, 0, 1) <= 0) return; buf.Add(one[0]);
                    int n = buf.Count; if (n >= 4 && buf[n - 1] == '\n' && buf[n - 2] == '\r' && buf[n - 3] == '\n' && buf[n - 4] == '\r') headEnd = n;
                }
                if (headEnd < 0) return;
                string head = Encoding.ASCII.GetString(buf.ToArray());
                var lines = head.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                var rl = lines[0].Split(' '); if (rl.Length < 2) return;
                string method = rl[0], target = rl[1], path = target, query = "";
                int qi = target.IndexOf('?'); if (qi >= 0) { path = target.Substring(0, qi); query = target.Substring(qi + 1); }
                int clen = 0;
                foreach (var l in lines) if (l.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) int.TryParse(l.Substring(15).Trim(), out clen);
                string body = "";
                if (clen > 0 && clen < 20000)
                {
                    var bb = new byte[clen]; int got = 0; while (got < clen) { int r = ns.Read(bb, got, clen - got); if (r <= 0) break; got += r; }
                    body = Encoding.UTF8.GetString(bb, 0, got);
                }
                var q = ParseQuery(query);
                string code; if (!q.TryGetValue("c", out code) || code != Code) { Fail(remote); Send(ns, 403, "text/plain", Encoding.UTF8.GetBytes("Wrong or missing code. Open the exact link shown in the app."), null); return; }

                if (method == "GET" && (path == "/" || path == "/index.html")) { Send(ns, 200, "text/html; charset=utf-8", Encoding.UTF8.GetBytes(Page), null); return; }
                if (method == "POST" && path == "/convert") { Convert(ns, ParseQuery(body)); return; }
                if (method == "GET" && path == "/status")
                {
                    string id; PJob j = null; if (q.TryGetValue("id", out id)) lock (lk) jobs.TryGetValue(id, out j);
                    if (j == null) { Json(ns, 404, "{\"state\":\"error\",\"msg\":\"Unknown job\"}"); return; }
                    Json(ns, 200, "{\"state\":\"" + j.State + "\",\"pct\":" + j.Pct + ",\"msg\":\"" + Esc(j.Msg) + "\",\"name\":\"" + Esc(j.Name) + "\"}"); return;
                }
                if (method == "GET" && path == "/file")
                {
                    string id; PJob j = null; if (q.TryGetValue("id", out id)) lock (lk) jobs.TryGetValue(id, out j);
                    if (j == null || j.State != "done" || !System.IO.File.Exists(j.File)) { Send(ns, 404, "text/plain", Encoding.UTF8.GetBytes("Not ready"), null); return; }
                    string fn = Path.GetFileName(j.File), ext = Path.GetExtension(fn).ToLowerInvariant();
                    string ct = ext == ".mp3" ? "audio/mpeg" : (ext == ".m4a" ? "audio/mp4" : (ext == ".flac" ? "audio/flac" : (ext == ".wav" ? "audio/wav" : "application/octet-stream")));
                    string ascii = Regex.Replace(fn, @"[^\x20-\x7E]", "_").Replace("\"", "'");
                    var fi = new FileInfo(j.File);
                    string h = "HTTP/1.1 200 OK\r\nContent-Type: " + ct + "\r\nContent-Length: " + fi.Length + "\r\nContent-Disposition: attachment; filename=\"" + ascii + "\"; filename*=UTF-8''" + Uri.EscapeDataString(fn) + "\r\nConnection: close\r\n\r\n";
                    var hb = Encoding.ASCII.GetBytes(h); ns.Write(hb, 0, hb.Length);
                    using (var fs = new FileStream(j.File, FileMode.Open, FileAccess.Read, FileShare.Read)) fs.CopyTo(ns);
                    return;
                }
                Send(ns, 404, "text/plain", Encoding.UTF8.GetBytes("Not found"), null);
            }
        }
        catch { }
    }

    // ---------- jobs ----------
    static readonly Regex UrlOk = new Regex(@"^https?://[^\s""'<>|^`]+$", RegexOptions.IgnoreCase);

    void Convert(NetworkStream ns, Dictionary<string, string> f)
    {
        string link = (f.ContainsKey("link") ? f["link"] : "").Trim(); int fmt = 0; int.TryParse(f.ContainsKey("fmt") ? f["fmt"] : "0", out fmt);
        bool novocal = f.ContainsKey("novocal") && f["novocal"] == "1";
        if (fmt != 0 && fmt != 2 && fmt != 3 && fmt != 4) fmt = 0;
        if (link.Length == 0 || link.Length > 600) { Json(ns, 400, "{\"error\":\"Paste a link first.\"}"); return; }
        string target;
        if (UrlOk.IsMatch(link)) target = link;
        else if (link.IndexOfAny(new[] { '"', '\r', '\n', '|', '&', '<', '>', '^', '`' }) < 0 && !link.StartsWith("-") && link.IndexOf("://") < 0) target = "ytsearch1:" + link;
        else { Json(ns, 400, "{\"error\":\"That doesn't look like a valid link.\"}"); return; }
        if (novocal && !Separator.Installed) { Json(ns, 400, "{\"error\":\"Vocal remover isn't set up on the PC yet. Open it once on the PC first.\"}"); return; }
        lock (lk) { if (running >= 2) { Json(ns, 400, "{\"error\":\"The PC is busy with other jobs. Try again in a minute.\"}"); return; } running++; }
        var j = new PJob { Id = Guid.NewGuid().ToString("N").Substring(0, 12), State = "running", Msg = "Starting..." };
        j.Dir = Path.Combine(Path.GetTempPath(), "YtToMp3-phone", j.Id); Directory.CreateDirectory(j.Dir);
        lock (lk)
        {
            foreach (var old in new List<PJob>(jobs.Values)) if ((DateTime.Now - old.Created).TotalMinutes > 90) { try { Directory.Delete(old.Dir, true); } catch { } jobs.Remove(old.Id); }
            jobs[j.Id] = j;
        }
        Json(ns, 200, "{\"id\":\"" + j.Id + "\"}");
        string tgt = target; int fm = fmt;
        new Thread(delegate () { try { RunJob(j, tgt, fm, novocal); } catch (Exception ex) { j.State = "error"; j.Msg = ex.Message; } finally { lock (lk) running--; } }) { IsBackground = true }.Start();
    }

    void RunJob(PJob j, string target, int fmt, bool novocal)
    {
        string ff = App.FfmpegExe();
        if (!File.Exists(App.YtDlp) || ff == null) { j.State = "error"; j.Msg = "The PC app needs to convert something once first (it installs its tools)."; return; }
        string deno = App.FindFilePublic(App.DenoDirPath, "deno.exe");
        string[] ext = { "mp3", "mp3", "wav", "flac", "m4a", "opus" };
        string[] qual = { "320K", "0", null, null, "256K", null }; bool[] art = { true, true, false, true, true, false };
        string dlExt = novocal ? "wav" : ext[fmt];
        string args = "-x --audio-format " + dlExt + (!novocal && qual[fmt] != null ? " --audio-quality " + qual[fmt] : "")
            + " --newline --windows-filenames --embed-metadata " + (!novocal && art[fmt] ? "--embed-thumbnail --convert-thumbnails jpg " : "")
            + "--ffmpeg-location \"" + Path.GetDirectoryName(ff) + "\" " + (deno != null ? "--js-runtimes \"deno:" + deno + "\" " : "")
            + "--no-playlist -o \"" + Path.Combine(j.Dir, "%(title).120B." + "%(ext)s") + "\" -- \"" + target + "\"";
        j.Msg = "Downloading...";
        var psi = new ProcessStartInfo(App.YtDlp, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        string lastErr = "";
        using (var p = new Process { StartInfo = psi })
        {
            DataReceivedEventHandler h = delegate (object s, DataReceivedEventArgs e)
            {
                if (e.Data == null) return;
                var m = Regex.Match(e.Data, @"\[download\]\s+([\d.]+)%");
                if (m.Success) { double v; if (double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v)) j.Pct = (int)(v * (novocal ? 0.35 : 0.9)); }
                else if (e.Data.StartsWith("ERROR")) lastErr = e.Data;
                else if (e.Data.Contains("[ExtractAudio]") || e.Data.Contains("[Metadata]")) j.Msg = "Converting...";
            };
            p.OutputDataReceived += h; p.ErrorDataReceived += h; p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine(); p.WaitForExit();
            if (p.ExitCode != 0) { j.State = "error"; j.Msg = lastErr.Length > 0 ? lastErr : "Download failed."; return; }
        }
        string got = null; DateTime bt = DateTime.MinValue;
        foreach (var f in Directory.GetFiles(j.Dir))
        {
            string x = Path.GetExtension(f).ToLowerInvariant();
            if (x != "." + dlExt) continue; var w = File.GetLastWriteTime(f); if (w > bt) { bt = w; got = f; }
        }
        if (got == null) { j.State = "error"; j.Msg = "No audio was produced."; return; }

        if (novocal)
        {
            j.Msg = "Removing vocals (AI, this takes a while)...";
            string v, inst; string baseName = Path.GetFileNameWithoutExtension(got);
            bool ok = Separator.Separate(got, j.Dir, baseName, "htdemucs", delegate (string s) { }, delegate (float pr) { j.Pct = 35 + (int)(pr * 55); }, () => false, out v, out inst);
            if (!ok) { j.State = "error"; j.Msg = "Vocal removal failed."; return; }
            string outp = Path.Combine(j.Dir, baseName + " [instrumental].") + ext[fmt];
            j.Msg = "Encoding..."; int code;
            MasterDsp.RunCapture(ff, "-hide_banner -y -i \"" + inst + "\" -vn " + MasterDsp.FmtEnc[fmt] + " \"" + outp + "\"", null, out code);
            if (code != 0 || !File.Exists(outp)) { j.State = "error"; j.Msg = "Encoding failed."; return; }
            got = outp;
        }
        j.File = got; j.Name = Path.GetFileName(got); j.Pct = 100; j.Msg = "Ready"; j.State = "done";
        Log("Phone: finished " + j.Name);
    }

    // ---------- the mobile page ----------
    const string Page = @"<!doctype html><html lang='en'><head><meta charset='utf-8'>
<meta name='viewport' content='width=device-width,initial-scale=1,viewport-fit=cover,user-scalable=no'>
<meta name='theme-color' content='#05020c'><meta name='color-scheme' content='dark'>
<meta name='apple-mobile-web-app-capable' content='yes'><meta name='mobile-web-app-capable' content='yes'>
<meta name='apple-mobile-web-app-status-bar-style' content='black-translucent'><meta name='apple-mobile-web-app-title' content='MP3'>
<title>BandLab + YouTube to MP3</title>
<style>
:root{--ink:#f3eaff;--dim:#a893d6;--acc:#b66cff;--acc2:#ee5ee0;--edge:rgba(170,110,255,.38)}
*{box-sizing:border-box;-webkit-tap-highlight-color:transparent}
html,body{margin:0;background:#05020c;color:var(--ink);font:16px/1.4 -apple-system,BlinkMacSystemFont,Segoe UI,Roboto,sans-serif;-webkit-text-size-adjust:100%}
body{min-height:100vh;min-height:100dvh;overflow-x:hidden;touch-action:manipulation}
.fx{position:fixed;left:0;top:0;width:100%;height:100%;z-index:-1;overflow:hidden;background:radial-gradient(120% 80% at 50% 0%,#1d0a3d 0%,#05020c 62%)}
.orb{position:absolute;width:80vmax;height:80vmax;border-radius:50%;opacity:.5;will-change:transform;transform:translate3d(0,0,0)}
.o1{background:radial-gradient(circle,#8a3cff 0%,rgba(138,60,255,0) 62%);left:-35vmax;top:-30vmax;animation:d1 19s ease-in-out infinite alternate}
.o2{background:radial-gradient(circle,#ee4ed8 0%,rgba(238,78,216,0) 62%);right:-40vmax;top:15vh;animation:d2 24s ease-in-out infinite alternate}
.o3{background:radial-gradient(circle,#4a3cff 0%,rgba(74,60,255,0) 62%);left:-15vmax;bottom:-45vmax;animation:d3 28s ease-in-out infinite alternate}
@keyframes d1{to{transform:translate3d(32vmax,20vmax,0) scale(1.15)}}
@keyframes d2{to{transform:translate3d(-30vmax,-14vh,0) scale(.9)}}
@keyframes d3{to{transform:translate3d(28vmax,-16vmax,0) scale(1.2)}}
.lf{position:absolute;top:-130px;width:90px;height:75px;opacity:.55;will-change:transform;animation:fall 22s linear infinite}
@keyframes fall{0%{transform:translate3d(0,0,0) rotate(0deg)}50%{transform:translate3d(28px,60vh,0) rotate(180deg)}100%{transform:translate3d(-10px,125vh,0) rotate(360deg)}}
.wrap{max-width:560px;margin:0 auto;padding:calc(18px + env(safe-area-inset-top)) 16px calc(118px + env(safe-area-inset-bottom))}
h1{margin:6px 0 4px;font-size:30px;line-height:1.1;letter-spacing:-.3px;font-weight:800;background:linear-gradient(90deg,#f6e6ff,#b66cff 55%,#ee5ee0);-webkit-background-clip:text;background-clip:text;color:transparent}
.sub{margin:0 0 18px;color:var(--dim);font-size:14px}
.card{margin-top:14px;padding:16px;border-radius:20px;background:rgba(16,7,32,.72);border:1px solid var(--edge);-webkit-backdrop-filter:blur(14px);backdrop-filter:blur(14px);box-shadow:0 0 0 1px rgba(255,255,255,.02) inset,0 10px 40px rgba(110,40,220,.18)}
label,.lab{display:block;font-size:12px;letter-spacing:.9px;text-transform:uppercase;color:var(--dim);margin:0 0 8px;font-weight:600}
textarea{width:100%;min-height:92px;resize:none;background:#0a0416;color:var(--ink);border:1px solid rgba(150,90,230,.55);border-radius:14px;padding:13px 14px;font:16px/1.4 inherit;font-family:inherit;outline:none;-webkit-appearance:none}
textarea:focus{border-color:var(--acc2);box-shadow:0 0 0 3px rgba(238,94,224,.2)}
textarea::placeholder{color:#7e6aa8}
.row{display:flex;gap:10px;margin-top:10px}
.ghost{flex:1;padding:12px;border-radius:12px;border:1px solid var(--edge);background:rgba(40,18,80,.55);color:var(--ink);font-size:15px;font-weight:600;font-family:inherit}
.ghost:active{background:rgba(110,50,200,.5)}
.seg{display:grid;grid-template-columns:repeat(4,1fr);gap:8px}
.seg button{padding:13px 0;border-radius:13px;border:1px solid var(--edge);background:rgba(30,12,60,.7);color:var(--dim);font-size:15px;font-weight:700;font-family:inherit;transition:transform .12s,background .2s,color .2s}
.seg button.on{background:linear-gradient(135deg,#7c34d6,#c04ad8);color:#fff;border-color:transparent;box-shadow:0 6px 20px rgba(150,60,230,.45)}
.seg button:active{transform:scale(.96)}
small{color:var(--dim);font-size:12.5px}
#fmtnote{display:block;margin-top:9px}
.sw{display:flex;align-items:center;justify-content:space-between;gap:12px;margin-top:16px;padding-top:14px;border-top:1px solid rgba(150,90,230,.2)}
.sw span{font-size:15.5px;font-weight:600}.sw small{display:block;font-weight:400}
.toggle{flex:none;width:56px;height:32px;border-radius:16px;border:1px solid var(--edge);background:#150a2c;position:relative;padding:0;transition:background .25s}
.toggle i{position:absolute;left:3px;top:3px;width:24px;height:24px;border-radius:50%;background:#c9b5f2;transition:transform .25s cubic-bezier(.3,1.4,.5,1),background .25s;will-change:transform}
.toggle.on{background:linear-gradient(135deg,#7c34d6,#c04ad8);border-color:transparent}
.toggle.on i{transform:translate3d(24px,0,0);background:#fff}
.bar{position:fixed;left:0;right:0;bottom:0;padding:26px 16px calc(14px + env(safe-area-inset-bottom));background:linear-gradient(to top,#05020c 55%,rgba(5,2,12,0));z-index:5}
.cta{display:block;width:100%;max-width:560px;margin:0 auto;padding:18px;border:0;border-radius:18px;font-size:19px;font-weight:800;letter-spacing:.3px;color:#fff;font-family:inherit;background:linear-gradient(100deg,#7c34d6,#c04ad8 60%,#ee5ee0);box-shadow:0 10px 34px rgba(170,60,230,.5);position:relative;overflow:hidden}
.cta:active{transform:scale(.985)}
.cta:after{content:'';position:absolute;top:0;left:0;width:40%;height:100%;background:linear-gradient(100deg,rgba(255,255,255,0),rgba(255,255,255,.28),rgba(255,255,255,0));transform:translate3d(-150%,0,0) skewX(-18deg);animation:shine 3.2s ease-in-out infinite;will-change:transform}
@keyframes shine{0%,55%{transform:translate3d(-150%,0,0) skewX(-18deg)}100%{transform:translate3d(380%,0,0) skewX(-18deg)}}
.job{margin-top:12px;padding:15px;border-radius:18px;background:rgba(22,9,46,.8);border:1px solid var(--edge);animation:pop .35s cubic-bezier(.2,1.2,.4,1)}
@keyframes pop{from{opacity:0;transform:translate3d(0,14px,0) scale(.97)}to{opacity:1;transform:none}}
.jt{display:flex;justify-content:space-between;align-items:flex-start;gap:10px}
.jt b{font-size:15px;word-break:break-word;font-weight:700}
.pill{flex:none;font-size:11px;font-weight:700;text-transform:uppercase;letter-spacing:.7px;padding:4px 9px;border-radius:999px;background:rgba(150,90,230,.3);color:#e4d2ff}
.pill.ok{background:rgba(60,210,140,.2);color:#7df0b8}.pill.bad{background:rgba(255,90,140,.2);color:#ff9bb8}
.pb{height:8px;border-radius:5px;background:#0d0620;margin:12px 0 8px;overflow:hidden}
.pb i{display:block;height:100%;width:100%;transform-origin:0 50%;transform:scaleX(0);background:linear-gradient(90deg,#8a3cff,#ee5ee0);transition:transform .7s linear;will-change:transform}
.pb.ind i{width:40%;transform:none;animation:ind 1.3s ease-in-out infinite}
@keyframes ind{0%{transform:translate3d(-110%,0,0)}100%{transform:translate3d(260%,0,0)}}
a.dl{display:block;margin-top:12px;padding:14px;border-radius:14px;text-align:center;text-decoration:none;font-weight:800;font-size:16px;color:#032a1a;background:linear-gradient(100deg,#3ee0a0,#7df0b8)}
.err{color:#ff9bb8}.note{margin:26px 4px 0;font-size:12px;color:#8a76b4;line-height:1.55}
.hist{margin-top:18px}.hist h2{font-size:12px;letter-spacing:.9px;text-transform:uppercase;color:var(--dim);margin:0 0 4px;font-weight:600}
@media (prefers-reduced-motion:reduce){.orb,.lf,.cta:after{animation:none}}
</style></head><body>
<div class='fx'><i class='orb o1'></i><i class='orb o2'></i><i class='orb o3'></i>
<svg width='0' height='0' style='position:absolute'><defs><symbol id='lf' viewBox='-110 -110 220 170'><g fill='rgba(170,100,255,.55)' stroke='rgba(235,200,255,.8)' stroke-width='2'>
<path d='M0 0C-17-30-12-80 0-100C12-80 17-30 0 0Z'/><path d='M0 0C-15-26-11-70 0-88C11-70 15-26 0 0Z' transform='rotate(-38)'/><path d='M0 0C-15-26-11-70 0-88C11-70 15-26 0 0Z' transform='rotate(38)'/>
<path d='M0 0C-12-20-8-52 0-66C8-52 12-20 0 0Z' transform='rotate(-72)'/><path d='M0 0C-12-20-8-52 0-66C8-52 12-20 0 0Z' transform='rotate(72)'/>
<path d='M0 0C-8-12-6-32 0-42C6-32 8-12 0 0Z' transform='rotate(-104)'/><path d='M0 0C-8-12-6-32 0-42C6-32 8-12 0 0Z' transform='rotate(104)'/></g></symbol></defs></svg>
<svg class='lf' style='left:6%;animation-duration:21s;animation-delay:-3s'><use href='#lf'/></svg>
<svg class='lf' style='left:28%;width:60px;height:50px;animation-duration:27s;animation-delay:-14s;opacity:.4'><use href='#lf'/></svg>
<svg class='lf' style='left:52%;width:110px;height:90px;animation-duration:24s;animation-delay:-9s'><use href='#lf'/></svg>
<svg class='lf' style='left:74%;width:70px;height:58px;animation-duration:30s;animation-delay:-20s;opacity:.45'><use href='#lf'/></svg>
<svg class='lf' style='left:90%;animation-duration:23s;animation-delay:-6s'><use href='#lf'/></svg></div>
<main class='wrap'>
<h1>BandLab + YouTube &rarr; MP3</h1><p class='sub'>Runs on your PC. Paste a link, get the file on your phone.</p>
<section class='card'><label for='links'>Links or search words</label>
<textarea id='links' rows='3' placeholder='https://youtu.be/...   or   bandlab.com/post/...' autocapitalize='off' autocorrect='off' spellcheck='false'></textarea>
<div class='row'><button class='ghost' id='paste' type='button'>Paste</button><button class='ghost' id='clear' type='button'>Clear</button></div></section>
<section class='card'><span class='lab'>Format</span>
<div class='seg' id='seg'><button type='button' data-v='0' class='on'>MP3</button><button type='button' data-v='4'>M4A</button><button type='button' data-v='3'>FLAC</button><button type='button' data-v='2'>WAV</button></div>
<small id='fmtnote'>MP3 320 kbps, plays everywhere</small>
<div class='sw'><span>Remove vocals<small>Instrumental only (AI, takes a few minutes)</small></span><button type='button' class='toggle' id='nv' role='switch' aria-checked='false'><i></i></button></div></section>
<section id='jobs'></section><section class='hist' id='hist'></section>
<p class='note'>Files are made on your PC and sent over your Wi-Fi. On iPhone: tap Download, then use the share button to Save to Files. Only download things you have the right to.</p>
</main>
<div class='bar'><button class='cta' id='go' type='button'>Convert</button></div>
<script>
var C=new URLSearchParams(location.search).get('c')||'',fmt='0',nv=0,notes={'0':'MP3 320 kbps, plays everywhere','4':'AAC 256 kbps, best for iPhone Music','3':'FLAC, lossless and large','2':'WAV 24-bit, biggest files'};
function $(i){return document.getElementById(i)}
function u(p){return p+(p.indexOf('?')<0?'?':'&')+'c='+encodeURIComponent(C)}
function esc(s){return String(s).replace(/[&<>]/g,function(c){return {'&':'&amp;','<':'&lt;','>':'&gt;'}[c]})}
function buzz(n){try{if(navigator.vibrate)navigator.vibrate(n)}catch(e){}}
try{var sv=JSON.parse(localStorage.getItem('ytm')||'{}');if(sv.fmt){fmt=sv.fmt}if(sv.nv){nv=1}}catch(e){}
function paintOpts(){var bs=$('seg').children;for(var i=0;i<bs.length;i++)bs[i].className=bs[i].getAttribute('data-v')===fmt?'on':'';$('fmtnote').textContent=notes[fmt];$('nv').className='toggle'+(nv?' on':'');$('nv').setAttribute('aria-checked',nv?'true':'false')}
function saveOpts(){try{localStorage.setItem('ytm',JSON.stringify({fmt:fmt,nv:nv}))}catch(e){}}
$('seg').onclick=function(e){var v=e.target.getAttribute&&e.target.getAttribute('data-v');if(v===null||v===undefined)return;fmt=v;paintOpts();saveOpts();buzz(8)};
$('nv').onclick=function(){nv=nv?0:1;paintOpts();saveOpts();buzz(8)};
$('clear').onclick=function(){$('links').value='';$('links').focus()};
$('paste').onclick=function(){
  if(navigator.clipboard&&navigator.clipboard.readText){navigator.clipboard.readText().then(function(t){if(t)$('links').value=t;else $('links').focus()}).catch(function(){$('links').focus()})}
  else{$('links').focus();$('links').select()}};
paintOpts();
function hist(){var h=[];try{h=JSON.parse(localStorage.getItem('ytmh')||'[]')}catch(e){}
  if(!h.length){$('hist').innerHTML='';return}
  var s='<h2>Recent</h2>';for(var i=0;i<h.length;i++){s+='<div class=job><div class=jt><b>'+esc(h[i].name)+'</b><span class=pill>saved</span></div><a class=dl href=\''+u('/file?id='+h[i].id)+'\' download>Download again</a></div>'}
  $('hist').innerHTML=s}
function remember(id,name){try{var h=JSON.parse(localStorage.getItem('ytmh')||'[]');h.unshift({id:id,name:name});h=h.slice(0,5);localStorage.setItem('ytmh',JSON.stringify(h))}catch(e){}hist()}
hist();
function track(id,label){
  var d=document.createElement('div');d.className='job';
  d.innerHTML='<div class=jt><b>'+esc(label)+'</b><span class=pill>starting</span></div><div class=\'pb ind\'><i></i></div><small>Waiting for your PC...</small>';
  $('jobs').insertBefore(d,$('jobs').firstChild);
  var pill=d.querySelector('.pill'),pb=d.querySelector('.pb'),bar=d.querySelector('.pb i'),msg=d.querySelector('small');
  function poll(){
    fetch(u('/status?id='+id)).then(function(r){return r.json()}).then(function(s){
      if(s.pct>0){pb.className='pb';bar.style.transform='scaleX('+Math.min(1,s.pct/100)+')'}
      msg.textContent=(s.msg||s.state)+(s.pct>0&&s.state==='running'?'  '+s.pct+'%':'');pill.textContent=s.state;
      if(s.state==='done'){pb.className='pb';bar.style.transform='scaleX(1)';pill.className='pill ok';pill.textContent='ready';d.querySelector('b').textContent=s.name;msg.textContent='Ready to download';
        var a=document.createElement('a');a.className='dl';a.textContent='Download';a.href=u('/file?id='+id);a.setAttribute('download',s.name);d.appendChild(a);remember(id,s.name);buzz([20,40,20]);return}
      if(s.state==='error'){pb.style.display='none';pill.className='pill bad';pill.textContent='failed';msg.innerHTML='<span class=err>'+esc(s.msg)+'</span>';return}
      setTimeout(poll,800)
    }).catch(function(){msg.textContent='Reconnecting...';setTimeout(poll,1500)})}
  poll()}
$('go').onclick=function(){
  var lines=$('links').value.split(/\r?\n/).map(function(x){return x.trim()}).filter(Boolean);
  if(!lines.length){$('links').focus();buzz(30);return}
  buzz(12);
  lines.forEach(function(l){
    fetch(u('/convert'),{method:'POST',headers:{'Content-Type':'application/x-www-form-urlencoded'},body:'link='+encodeURIComponent(l)+'&fmt='+fmt+'&novocal='+nv})
    .then(function(r){return r.json()}).then(function(j){
      if(j.id)track(j.id,l);else{var d=document.createElement('div');d.className='job';d.innerHTML='<div class=jt><b>'+esc(l)+'</b><span class=\'pill bad\'>failed</span></div><small class=err>'+esc(j.error||'Failed')+'</small>';$('jobs').insertBefore(d,$('jobs').firstChild)}
    }).catch(function(){alert('Lost connection to the PC')})});
  $('links').value='';window.scrollTo({top:0,behavior:'smooth'})};
</script></body></html>";
}
