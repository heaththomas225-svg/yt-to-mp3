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

    public static string LocalIp()
    {
        try
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                var props = ni.GetIPProperties(); if (props.GatewayAddresses.Count == 0) continue;
                foreach (UnicastIPAddressInformation ua in props.UnicastAddresses)
                    if (ua.Address.AddressFamily == AddressFamily.InterNetwork) return ua.Address.ToString();
            }
        }
        catch { }
        return "127.0.0.1";
    }

    public string Url { get { return "http://" + LocalIp() + ":" + Port + "/?c=" + Code; } }

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
            using (c) using (var ns = c.GetStream())
            {
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
                string code; if (!q.TryGetValue("c", out code) || code != Code) { Send(ns, 403, "text/plain", Encoding.UTF8.GetBytes("Wrong or missing code. Open the exact link shown in the app."), null); return; }

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
<meta name='viewport' content='width=device-width,initial-scale=1,viewport-fit=cover'><meta name='theme-color' content='#0e051e'><title>YouTube to MP3</title>
<style>
:root{color-scheme:dark}*{box-sizing:border-box}
body{margin:0;min-height:100vh;color:#efe6ff;font:16px -apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;background:radial-gradient(900px 500px at 20% -10%,#4a1c8a55,transparent),linear-gradient(165deg,#0e051e,#2a0f52)}
.wrap{max-width:560px;margin:0 auto;padding:22px 16px calc(40px + env(safe-area-inset-bottom))}
h1{font-size:28px;margin:6px 0 2px;background:linear-gradient(90deg,#f3e0ff,#b96eff);-webkit-background-clip:text;background-clip:text;color:transparent}
p.sub{margin:0 0 18px;color:#bba6e0;font-size:14px}
label{display:block;font-size:13px;color:#bba6e0;margin:14px 0 6px}
textarea,select{width:100%;background:#1c0b38;color:#efe6ff;border:1px solid #6b3fb3;border-radius:12px;padding:12px;font:inherit}
textarea{min-height:84px;resize:vertical}
.row{display:flex;gap:10px;align-items:center;margin-top:12px;font-size:15px}.row input{width:20px;height:20px}
button{width:100%;margin-top:18px;padding:15px;border:0;border-radius:14px;font:600 17px inherit;color:#fff;background:linear-gradient(90deg,#7c34d6,#c04ad8);box-shadow:0 6px 22px #7c34d655}
button:active{transform:scale(.99)}
.card{margin-top:14px;padding:14px;border-radius:14px;background:#24104a99;border:1px solid #5f3a9e}
.card b{display:block;word-break:break-word;font-size:15px}.card small{color:#bba6e0}
.bar{height:8px;border-radius:5px;background:#12062a;margin:10px 0 6px;overflow:hidden}.bar i{display:block;height:100%;width:0;background:linear-gradient(90deg,#9a52ff,#ee5ee0);transition:width .5s}
a.dl{display:block;margin-top:10px;padding:12px;text-align:center;border-radius:12px;background:#34d399;color:#05261a;font-weight:700;text-decoration:none}
.err{color:#ff8fb8}.note{margin-top:24px;font-size:12px;color:#8f7bb8;line-height:1.5}
</style></head><body><div class='wrap'>
<h1>BandLab + YouTube &rarr; MP3</h1><p class='sub'>Runs on your PC. Paste a link, get the file on your phone.</p>
<label for='links'>Links or search words (one per line)</label><textarea id='links' placeholder='https://youtu.be/... or bandlab.com/post/...'></textarea>
<label for='fmt'>Format</label><select id='fmt'><option value='0'>MP3 (320 kbps)</option><option value='4'>M4A / AAC (256 kbps)</option><option value='3'>FLAC (lossless)</option><option value='2'>WAV (24-bit)</option></select>
<div class='row'><input type='checkbox' id='nv'><label for='nv' style='margin:0;font-size:15px;color:#efe6ff'>Remove vocals (instrumental only)</label></div>
<button id='go'>Convert</button><div id='jobs'></div>
<p class='note'>Files are made on your PC and sent over your Wi-Fi. On iPhone, tap Download, then Save to Files from the share menu. Only download things you have the right to.</p>
</div><script>
var C=new URLSearchParams(location.search).get('c')||'';
function u(p){return p+(p.indexOf('?')<0?'?':'&')+'c='+encodeURIComponent(C)}
function esc(s){return String(s).replace(/[&<>]/g,function(c){return {'&':'&amp;','<':'&lt;','>':'&gt;'}[c]})}
function track(id,label){
  var d=document.createElement('div');d.className='card';d.innerHTML='<b>'+esc(label)+'</b><div class=bar><i></i></div><small>Starting...</small>';
  document.getElementById('jobs').prepend(d);
  var t=setInterval(function(){
    fetch(u('/status?id='+id)).then(function(r){return r.json()}).then(function(s){
      d.querySelector('i').style.width=s.pct+'%';d.querySelector('small').textContent=s.msg||s.state;
      if(s.state==='done'){clearInterval(t);d.querySelector('b').textContent=s.name;d.querySelector('small').textContent='Ready';
        var a=document.createElement('a');a.className='dl';a.textContent='Download';a.href=u('/file?id='+id);a.setAttribute('download',s.name);d.appendChild(a)}
      if(s.state==='error'){clearInterval(t);d.querySelector('small').innerHTML='<span class=err>'+esc(s.msg)+'</span>'}
    }).catch(function(){})
  },1200)}
document.getElementById('go').onclick=function(){
  var lines=document.getElementById('links').value.split(/\r?\n/).map(function(x){return x.trim()}).filter(Boolean);
  if(!lines.length){alert('Paste a link first');return}
  var fmt=document.getElementById('fmt').value,nv=document.getElementById('nv').checked?1:0;
  lines.forEach(function(l){
    fetch(u('/convert'),{method:'POST',headers:{'Content-Type':'application/x-www-form-urlencoded'},body:'link='+encodeURIComponent(l)+'&fmt='+fmt+'&novocal='+nv})
    .then(function(r){return r.json()}).then(function(j){
      if(j.id)track(j.id,l);else{var d=document.createElement('div');d.className='card';d.innerHTML='<b>'+esc(l)+'</b><small class=err>'+esc(j.error||'Failed')+'</small>';document.getElementById('jobs').prepend(d)}
    }).catch(function(){alert('Lost connection to the PC')})
  })};
</script></body></html>";
}
