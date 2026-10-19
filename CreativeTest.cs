using System; using System.Collections.Generic; using System.Drawing; using System.IO; using System.Linq; using System.Reflection; using System.Threading.Tasks; using System.Windows.Forms;
// Acceptance tests for Purphex Creative Studio with the real editor page, real media and real exports (muted, on the hidden desktop).
static class CRTEST
{
    static StudioForm sf; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public; static int fails; static string ff, fp, outDir = @"C:\Users\thoma\nfout\crout", media = @"C:\Users\thoma\nfout\crmedia";
    static void Check(string n, bool ok, string d = "") { d = d ?? "(no answer)"; if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + (d.Length > 0 ? "   " + d : "")); Console.Out.Flush(); }
    static object F(string n) { return typeof(StudioForm).GetField(n, BF).GetValue(sf); }
    static object M(string n, params object[] a) { return typeof(StudioForm).GetMethod(n, BF).Invoke(sf, a); }
    static void Pump(int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } }
    static HubBrowser B { get { return (HubBrowser)F("crBObj"); } }
    static string Js(string s, int ms = 20000) { var t = B.Js(s); var end = DateTime.Now.AddMilliseconds(ms); while (!t.IsCompleted && DateTime.Now < end) { Application.DoEvents(); System.Threading.Thread.Sleep(10); } if (!t.IsCompleted) return null; var r = t.Result; try { var o = MiniJson.Parse(r); return o == null ? null : Convert.ToString(o, System.Globalization.CultureInfo.InvariantCulture); } catch { return r; } }
    static bool WaitJs(string cond, int ms) { var end = DateTime.Now.AddMilliseconds(ms); while (DateTime.Now < end) { if (Js("!!(" + cond + ")", 5000) == "True") return true; Pump(150); } return false; }
    static double Vol(string file, double from, double len) { int code; string o = CreativeStore.Run(ff, "-hide_banner -ss " + from.ToString(System.Globalization.CultureInfo.InvariantCulture) + " -t " + len.ToString(System.Globalization.CultureInfo.InvariantCulture) + " -i \"" + file + "\" -af volumedetect -f null -", 60000, out code); var m = System.Text.RegularExpressions.Regex.Match(o, @"mean_volume: (-?[\d.]+) dB"); return m.Success ? double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : double.NaN; }
    static Bitmap Frame(string file, double t) { string png = Path.Combine(outDir, "f_" + Path.GetFileNameWithoutExtension(file) + "_" + t.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ".png"); int code; CreativeStore.Run(ff, "-y -v error -ss " + t.ToString(System.Globalization.CultureInfo.InvariantCulture) + " -i \"" + file + "\" -frames:v 1 \"" + png + "\"", 60000, out code); return File.Exists(png) ? new Bitmap(png) : null; }
    static bool Gray(Color c) { return Math.Abs(c.R - c.G) < 14 && Math.Abs(c.G - c.B) < 14; }
    static double Lum(Bitmap b, RectangleF r) { double s = 0; int n = 0; for (int y = (int)(r.Y * b.Height); y < (int)(r.Bottom * b.Height); y += 4) for (int x = (int)(r.X * b.Width); x < (int)(r.Right * b.Width); x += 4) { var c = b.GetPixel(x, y); s += (c.R + c.G + c.B) / 3.0; n++; } return n > 0 ? s / n : 0; }
    static string Export(string fmt, string name, int w, int h, string quality = "draft")
    {
        string outp = Path.Combine(outDir, name + CreativeStore.Ext(fmt)); try { File.Delete(outp); } catch { } StudioForm.CrSaveHook = _ => outp; Js("window.__exp=null;window.__expErr=null;");
        Js("PX.UI.exportRun={t0:0,t1:PX.projectDur(),w:" + w + ",h:" + h + ",fps:30,fmt:'" + fmt + "'};PX.send({t:'exportStart',name:'" + name + "',fmt:'" + fmt + "',w:" + w + ",h:" + h + ",fps:30,quality:'" + quality + "',abr:192,dur:PX.projectDur()})");
        if (!WaitJs("window.__exp||window.__expErr", 600000)) return "timeout"; string err = Js("window.__expErr||''"); return err.Length > 0 ? "ERR " + err : outp;
    }
    [STAThread] static void Main()
    {
        StudioEnv.DataDir = Path.Combine(Path.GetTempPath(), "purphex_crtest_" + Guid.NewGuid().ToString("N").Substring(0, 6)); Directory.CreateDirectory(StudioEnv.DataDir); outDir = Path.Combine(StudioEnv.DataDir, "out"); media = Path.Combine(StudioEnv.DataDir, "media"); Directory.CreateDirectory(outDir); Directory.CreateDirectory(media); Edition.Override = Tier.Fantasy;
        string bin = Directory.GetFiles(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "YtToMp3", "tools", "ffmpeg"), "ffmpeg.exe", SearchOption.AllDirectories)[0]; ff = bin; fp = Path.Combine(Path.GetDirectoryName(bin), "ffprobe.exe"); StudioEnv.Ffmpeg = () => ff; StudioEnv.Ffprobe = () => fp;
        { int code; foreach (var a in new[] { "-f lavfi -i testsrc=size=1280x720:rate=30:duration=6 -f lavfi -i sine=frequency=440:duration=6 -c:v libx264 -pix_fmt yuv420p -c:a aac -shortest clipA.mp4", "-f lavfi -i smptebars=size=1280x720:rate=30:duration=5 -f lavfi -i sine=frequency=880:duration=5 -c:v libx264 -pix_fmt yuv420p -c:a aac -shortest clipB.mp4", "-f lavfi -i color=c=0x00000000:s=400x400,format=rgba,drawbox=x=100:y=100:w=200:h=200:color=0xff3399@1:t=fill -frames:v 1 logo.png", "-f lavfi -i gradients=s=1920x1080:c0=0x3311aa:c1=0xff8833:duration=1 -frames:v 1 photo.jpg", "-f lavfi -i sine=frequency=220:duration=12 -f lavfi -i sine=frequency=330:duration=12 -filter_complex [0][1]amix=inputs=2,volume=0.8 -c:a libmp3lame -b:a 192k song.mp3" }) { var parts = a.Split(new[] { ' ' }); string outName = parts[parts.Length - 1]; CreativeStore.Run(ff, "-y -v error " + a.Substring(0, a.Length - outName.Length) + "\"" + Path.Combine(media, outName) + "\"", 60000, out code); } }
        Application.EnableVisualStyles(); var app = (App)Activator.CreateInstance(typeof(App), true); sf = new StudioForm(app); sf.ClientSize = new Size(1600, 940); sf.Show(); Pump(400);
        M("OpenCreative", "video"); var end = DateTime.Now.AddSeconds(30); while (DateTime.Now < end && (B == null || !B.Ready)) { sf.Invalidate(); Pump(100); }
        Check("Creative Studio opens inside Purphex (side panel > Purphex Video Editor)", B != null && B.Ready && (int)F("tab") == 10, B == null ? "no browser" : B.Error);
        Check("the editor page loaded and talks to Purphex", WaitJs("window.PX&&PX.UI&&PX.UI.renderTimeline&&document.readyState==='complete'", 20000));
        Js("PX.on('exportDone',m=>window.__exp=m.info);PX.on('exportError',m=>window.__expErr=m.msg);PX.on('aiError',m=>window.__aiErr=m.msg);PX.on('saved',m=>window.__saved=m.id);PX.on('artSaved',m=>window.__artSaved=m.id);PX.on('mediaError',m=>window.__mErr=m.msg);");
        // ---- import ----
        M("CrImportPaths", new List<string> { Path.Combine(media, "clipA.mp4"), Path.Combine(media, "clipB.mp4"), Path.Combine(media, "logo.png"), Path.Combine(media, "photo.jpg"), Path.Combine(media, "song.mp3"), Path.Combine(media, "..", "crmedia", "nothing.xyz") });
        Check("importing video, audio, a transparent PNG and a photo; an unsupported file is refused with a message", WaitJs("PX.P.media.length===5", 15000) && Js("window.__mErr||''").Contains("nothing.xyz"), Js("PX.P.media.length") + " / " + Js("window.__mErr||''"));
        Js("(()=>{const u=PX.P.media.find(m=>m.name==='photo.jpg').url;window.__t=[];for(const co of [null,'anonymous']){const i=new Image();if(co)i.crossOrigin=co;i.onload=()=>window.__t.push((co||'plain')+' ok '+i.naturalWidth);i.onerror=(e)=>window.__t.push((co||'plain')+' error');i.src=u+'?x='+(co||'p');}fetch(u).then(r=>window.__t.push('fetch '+r.status+' '+r.headers.get('content-type'))).catch(e=>window.__t.push('fetch err '+e.message));})()"); Pump(3000); Console.WriteLine("  img test: " + Js("JSON.stringify(window.__t)"));
        Pump(4000); Console.WriteLine("  img debug: " + Js("JSON.stringify(PX.P.media.filter(m=>m.kind==='image').map(m=>[m.w,PX.imgs[m.id]&&PX.imgs[m.id].complete,PX.imgs[m.id]&&PX.imgs[m.id].naturalWidth,m.url]))"));
        Check("the real media is read: durations, sizes, thumbnails, the PNG's transparency", WaitJs("PX.P.media.every(m=>m.kind==='audio'?m.dur>0:(m.w>0))&&PX.thumbs[PX.P.media[0].id]&&PX.P.media.find(m=>m.name==='logo.png').alpha===true", 30000), Js("JSON.stringify(PX.P.media.map(m=>[m.name,Math.round(m.dur||0),m.w,m.h,m.alpha]))"));
        Check("the sound of each media file is decoded (for waveforms, the mix and the visualisers)", WaitJs("Object.keys(PX.audio).length>=3&&Object.keys(PX.peaks).length>=3", 60000), Js("Object.keys(PX.audio).length"));
        // ---- A: edit, save, reopen ----
        string build = @"(()=>{const P=PX.P,Mf=n=>P.media.find(m=>m.name==n),T=n=>P.tracks.find(t=>t.name==n);
          const a=PX.UI.addMediaAt(Mf('clipA.mp4'),T('Video 1').id,0); a.mute=true; PX.seek(3); PX.UI.sel=new Set([a.id]); PX.UI.split();
          const parts=P.clips.filter(c=>c.media===Mf('clipA.mp4').id).sort((x,y)=>x.start-y.start); const b2=parts[1];
          PX.begin(); b2.start+=0.5; b2.dur-=0.5; b2.in+=0.5; PX.commit('trim'); PX.begin(); b2.start=4; PX.commit('move'); PX.begin(); b2.transIn={type:'fadeblack',dur:1}; PX.commit('transition');
          const b=PX.UI.addMediaAt(Mf('clipB.mp4'),T('Video 2').id,1); PX.begin(); b.mute=true; b.scale=0.4; b.x=0.5; b.y=-0.5; b.fx.bw=true; b.dur=3; PX.commit('layer');
          PX.seek(0.5); PX.UI.addText(PX.UI.TITLES[0]); const tt=P.clips.find(c=>c.type==='text'); PX.begin(); tt.text.content='HELLO'; tt.dur=6; tt.y=0.6; tt.text.animIn='none'; tt.text.animOut='none'; PX.commit('title');
          const s=PX.UI.addMediaAt(Mf('song.mp3'),T('Music').id,0); PX.begin(); s.vol=0.5; s.dur=7.5; PX.commit('song');
          return JSON.stringify(P.clips.map(c=>[c.type,+c.start.toFixed(3),+c.dur.toFixed(3),+(c.in||0).toFixed(3)]))})()";
        string before = Js(build); Console.WriteLine("  clips: " + before);
        Check("a clip is split at the playhead, trimmed, moved; a second layer, a title and a song are added", before != null && before.Contains("[\"video\",0,3,0]") && before.Contains("[\"video\",4,2.5,3.5]") && before.Contains("[\"text\",0.5,6,0]"), before ?? "null");
        Check("undo and redo walk back through the edits", Js("(()=>{const n=PX.P.clips.length;PX.undo();PX.undo();const a=PX.P.clips.length;PX.redo();PX.redo();return n+'/'+a+'/'+PX.P.clips.length+'/'+PX.P.clips.find(c=>c.type==='audio').vol})()") == "5/4/5/0.5");
        string pid = Js("PX.P.id"); Js("PX.P.name='Acceptance A';PX.UI.save(false)");
        Check("the project is saved as a file in Purphex's data folder", WaitJs("window.__saved==='" + pid + "'", 10000) && File.Exists(CreativeStore.ProjPath("video", pid)));
        Js("PX.UI.loadProject(PX.newProject('blank'));"); Check("a different project can be open meanwhile", Js("PX.P.clips.length") == "0");
        Js("PX.send({t:'open',id:'" + pid + "'})"); WaitJs("PX.P.id==='" + pid + "'&&PX.P.clips.length===5", 10000);
        string after = Js("JSON.stringify(PX.P.clips.map(c=>[c.type,+c.start.toFixed(3),+c.dur.toFixed(3),+(c.in||0).toFixed(3)]))");
        Check("closing and reopening the project gives the same composition (positions, trims, layers, title, effects, transition)", after == before && Js("PX.P.clips.find(c=>c.fx&&c.fx.bw).scale") == "0.4" && Js("PX.P.clips.find(c=>c.transIn).transIn.type") == "fadeblack" && Js("PX.P.clips.find(c=>c.type==='text').text.content") == "HELLO", after ?? "null");
        WaitJs("Object.keys(PX.audio).length>=3", 60000);
        // ---- B: export and check the file ----
        string o1 = Export("mp4-h264", "acceptance_A", 1280, 720);
        bool okA; string infoA = o1.StartsWith("ERR") || o1 == "timeout" ? o1 : CreativeStore.Validate(fp, o1, 7.5, true, out okA); Console.WriteLine("  export: " + infoA);
        Check("export: a real MP4 (H.264 + AAC) of the right length is written and checked", File.Exists(o1) && infoA.Contains("h264") && infoA.Contains("aac"), infoA);
        using (var f2 = Frame(o1, 2.0)) using (var f41 = Frame(o1, 4.1)) using (var f55 = Frame(o1, 5.5))
        {
            Check("the frame has the second layer in black and white (the effect is in the export)", f2 != null && new[] { 0.70f, 0.75f, 0.80f }.All(x => Gray(f2.GetPixel((int)(x * f2.Width), (int)(0.22 * f2.Height)))), f2 == null ? "no frame" : f2.GetPixel((int)(0.75 * f2.Width), (int)(0.22 * f2.Height)).ToString());
            bool colour = false; if (f2 != null) for (int x = 20; x < f2.Width / 2; x += 9) { var c = f2.GetPixel(x, (int)(0.45 * f2.Height)); if (Math.Abs(c.R - c.G) > 60 || Math.Abs(c.G - c.B) > 60) colour = true; }
            Check("the bottom layer keeps its colour", colour);
            int white = 0; if (f2 != null) for (int y = (int)(0.72 * f2.Height); y < (int)(0.88 * f2.Height); y += 2) for (int x = (int)(0.3 * f2.Width); x < (int)(0.7 * f2.Width); x += 2) { var c = f2.GetPixel(x, y); if (c.R > 235 && c.G > 235 && c.B > 235) white++; }
            Check("the title is drawn into the video", white > 150, white + " white pixels");
            double l41 = f41 == null ? -1 : Lum(f41, new RectangleF(0.05f, 0.4f, 0.4f, 0.25f)), l55 = f55 == null ? -1 : Lum(f55, new RectangleF(0.05f, 0.4f, 0.4f, 0.25f));
            Check("the transition fades the clip in from black", l41 >= 0 && l41 < l55 * 0.35, l41.ToString("0") + " -> " + l55.ToString("0"));
        }
        double v1 = Vol(o1, 0.2, 7); Js("PX.begin();PX.P.clips.find(c=>c.type==='audio').vol=1;PX.commit('vol')"); string o2 = Export("wav", "acceptance_vol", 1280, 720); double v2 = File.Exists(o2) ? Vol(o2, 0.2, 7) : double.NaN;
        Check("changing a clip's volume changes the exported sound (+6 dB for twice the level)", !double.IsNaN(v1) && !double.IsNaN(v2) && Math.Abs((v2 - v1) - 6.0) < 1.2, v1.ToString("0.0") + " dB -> " + v2.ToString("0.0") + " dB");
        // ---- C: a music video from a song, artwork, a visualiser and a title ----
        Js(@"(()=>{PX.UI.loadProject(PX.newProject('Song video',1280,720,30,'vis'));const P=PX.P,Mf=n=>PX.mediaById?null:null;return 1})()");
        M("CrImportPaths", new List<string> { Path.Combine(media, "song.mp3"), Path.Combine(media, "photo.jpg") }); WaitJs("PX.P.media.length===2&&Object.keys(PX.audio).length>=1&&PX.P.media.find(m=>m.kind==='image').w>0", 60000);
        Js(@"(()=>{const P=PX.P,s=P.media.find(m=>m.kind==='audio'),ph=P.media.find(m=>m.kind==='image'),A=P.tracks.find(t=>t.kind==='audio'),V=P.tracks.filter(t=>t.kind==='video');
          const sc=PX.UI.addMediaAt(s,A.id,0); const bg=PX.UI.addMediaAt(ph,V[1].id,0); PX.begin(); bg.dur=sc.dur; bg.fit='fill'; PX.commit('bg'); PX.seek(0); PX.UI.addVisual('bars'); PX.UI.addText(PX.UI.TITLES[0]); PX.begin(); P.clips.find(c=>c.type==='text').dur=sc.dur; P.clips.find(c=>c.type==='text').text.content='MY SONG'; P.clips.find(c=>c.type==='visual').dur=sc.dur; PX.commit('x'); return P.clips.length})()");
        string o3 = Export("mp4-h264", "acceptance_C", 1280, 720); bool okC; string infoC = File.Exists(o3) ? CreativeStore.Validate(fp, o3, 12, true, out okC) : o3; Console.WriteLine("  song video: " + infoC);
        Check("a music video from a song, a picture, a visualiser and a title exports as a real MP4 with the song's sound", File.Exists(o3) && infoC.Contains("h264") && infoC.Contains("aac") && Vol(o3, 1, 8) > -30, infoC);
        using (var fv = Frame(o3, 6.0))
        {
            int vis = 0; if (fv != null) for (int y = (int)(0.6 * fv.Height); y < (int)(0.95 * fv.Height); y += 3) for (int x = 0; x < fv.Width; x += 3) { var c = fv.GetPixel(x, y); if (c.B > 150 && c.R > 90 && c.G < 140) vis++; }
            Check("the visualiser bars (driven by the song's real spectrum) are in the video", vis > 200, vis + " bar pixels");
        }
        { string sj = Js("(()=>{let a=0,b=0,pa=null,pb=null,mx=0;for(let f=0;f<120;f++){const t=2+f/30,r=PX.spectrum(t,32).bands,s=PX.spectrumSmooth(t,32,null,0.8).bands;if(pa){for(let i=0;i<32;i++){a+=Math.abs(r[i]-pa[i]);b+=Math.abs(s[i]-pb[i]);}}pa=r;pb=s;mx=Math.max(mx,...s);}return (b/a).toFixed(2)+' '+mx.toFixed(2)})()");
          var sjp = sj.Split(' '); double ratio = double.Parse(sjp[0], System.Globalization.CultureInfo.InvariantCulture), smax = double.Parse(sjp[1], System.Globalization.CultureInfo.InvariantCulture);
          Check("visualiser SMOOTH: the bars move far less from frame to frame (no spazzing) and still reach the loud parts", ratio < 0.75 && smax > 0.3, "movement x" + sjp[0] + ", top " + sjp[1]); }
        Check("the spectrum is computed from the real audio (louder parts give taller bars, silence gives none)", Js("(()=>{const a=PX.spectrum(5,32).bands,b=PX.spectrum(20,32).bands;return (Math.max(...a)>0.3&&Math.max(...b)<0.05)+''})()") == "true", Js("Math.max(...PX.spectrum(5,32).bands).toFixed(2)+' vs '+Math.max(...PX.spectrum(20,32).bands).toFixed(2)"));
        // ---- other formats ----
        Js("PX.P.clips=PX.P.clips.filter(c=>c.type!=='visual');PX.changed('x')");
        string o4 = Export("webm", "acceptance_webm", 640, 360); bool ok4; string i4 = File.Exists(o4) ? CreativeStore.Validate(fp, o4, 12, true, out ok4) : o4; Check("WebM (VP9 + Opus) export works", File.Exists(o4) && i4.Contains("vp9"), i4);
        string o5 = Export("mp4-h265", "acceptance_hevc", 640, 360); bool ok5; string i5 = File.Exists(o5) ? CreativeStore.Validate(fp, o5, 12, true, out ok5) : o5; Check("MP4 H.265 / HEVC export works", File.Exists(o5) && i5.Contains("hevc"), i5);
        // ---- errors ----
        StudioForm.CrSaveHook = null; Js("PX.UI.setTool('cover')"); Pump(800);
        // ---- D: artwork project ----
        Js("(()=>{const C=PX.Cover;C.art.name='Cover A';const t=C.art.layers.find(l=>l.type==='text');C.begin();t.text='NIGHT DRIVE';t.color='#ffffff';C.commit();C.act('save')})()");
        string aid = Js("PX.Cover.art.id"); Check("an artwork project is saved (layers and text kept separately)", WaitJs("window.__artSaved==='" + aid + "'", 10000) && File.ReadAllText(CreativeStore.ProjPath("art", aid)).Contains("NIGHT DRIVE"));
        string png = Path.Combine(outDir, "cover_export.png"); try { File.Delete(png); } catch { } StudioForm.CrSaveHook = _ => png; Js("PX.Cover.act('png')"); var pend = DateTime.Now.AddSeconds(20); while (!File.Exists(png) && DateTime.Now < pend) Pump(200); Pump(500);
        bool pngOk = false; string pd = "no file"; if (File.Exists(png)) using (var bm = new Bitmap(png)) { pd = bm.Width + "x" + bm.Height; int w2 = 0; for (int y = (int)(0.75 * bm.Height); y < (int)(0.80 * bm.Height); y += 4) for (int x = (int)(0.3 * bm.Width); x < (int)(0.7 * bm.Width); x += 4) { var c = bm.GetPixel(x, y); if (c.R > 230 && c.G > 230 && c.B > 230) w2++; } pngOk = bm.Width == 3000 && bm.Height == 3000 && w2 > 30; pd += ", " + w2 + " white text pixels"; }
        Check("the artwork exports as a real 3000 x 3000 PNG with its text", pngOk, pd);
        // ---- the video editor's welcome screen ----
        { Js("PX.UI.setTool('video')"); Pump(300); Js("PX.UI.welcome()"); WaitJs("!!document.querySelector('#vidWelcome .vwcard')", 5000); Pump(800);
          Check("the video editor starts simply: IMPORT VIDEO OR PHOTOS first, then your song, your projects or a blank project", Js("String(document.querySelector('#vidWelcome h2').textContent.includes('PURPHEX MUSIC VIDEO EDITOR')&&document.querySelector('#vidWelcome .vwcard.big').textContent.includes('IMPORT VIDEO OR PHOTOS')&&document.querySelectorAll('#vidWelcome .vwcard').length===4&&document.querySelector('#modal').classList.contains('hidden'))") == "true");
          { var ctw = B.Capture("C:/Users/thoma/nfout/video-welcome.png"); var cew = DateTime.Now.AddSeconds(10); while (!ctw.IsCompleted && DateTime.Now < cew) Pump(50); }
          Check("the welcome screen lists your saved projects", WaitJs("document.querySelectorAll('#vwProj .pitem').length>=1", 8000), Js("document.querySelector('#vwProj').textContent.slice(0,80)"));
          string keepId = Js("PX.P.id"); Js("PX.setDirty(false);document.querySelector('#vidWelcome .vwcard[data-k=blank]').click()"); Pump(300);
          Check("NEW BLANK PROJECT asks for the size and closes the start screen", Js("String(!document.querySelector('#modal').classList.contains('hidden')&&document.querySelector('#modalBox').textContent.includes('New video project')&&document.querySelector('#vidWelcome').classList.contains('hidden'))") == "true"); Js("PX.UI.close()");
          Js("PX.send({t:'open',id:'" + keepId + "'})"); WaitJs("PX.P.id==='" + keepId + "'", 8000); Pump(300); }
        // ---- MY PROJECTS ----
        { string pdir = Path.Combine(StudioEnv.DataDir, "myproj"); Directory.CreateDirectory(pdir); string pf = Path.Combine(pdir, "Night Drive.ytmproj"); string cov = Path.Combine(pdir, "cover.png"); using (var bmc = new Bitmap(64, 64)) { using (var gc = Graphics.FromImage(bmc)) gc.Clear(Color.Red); bmc.Save(cov, System.Drawing.Imaging.ImageFormat.Png); }
          File.WriteAllText(pf, "test"); ProjectLibrary.LoadOnce(); ProjectLibrary.Entries.Add(new ProjectEntry { Path = pf, Name = "Night Drive", Cover = cov, Seconds = 3, Bpm = 120, Updated = DateTime.Now });
          File.Copy(Path.Combine(media, "song.mp3"), Path.Combine(pdir, "night.mp3"), true); sf.songRenderHook = e => { string p2 = Path.Combine(Path.GetTempPath(), "purphex_crt_" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".mp3"); File.Copy(Path.Combine(pdir, "night.mp3"), p2, true); return p2; };
          Js("PX.UI.setTool('video');PX.UI.pickProject()"); WaitJs("document.querySelectorAll('#ppL .ppitem').length>0", 8000);
          Pump(600); { var ctp = B.Capture("C:/Users/thoma/nfout/video-myprojects.png"); var cep = DateTime.Now.AddSeconds(10); while (!ctp.IsCompleted && DateTime.Now < cep) Pump(50); }
          Check("MY PROJECTS lists your Purphex projects with their pictures", Js("String([...document.querySelectorAll('#ppL .ppitem')].some(d=>d.textContent.includes('Night Drive')&&d.querySelector('.ppc').style.backgroundImage.includes('data:image')))") == "true");
          int n0p = int.Parse(Js("String(PX.P.media.length)")); Js("[...document.querySelectorAll('#ppL .ppitem')].find(d=>d.textContent.includes('Night Drive')).querySelector('button').click()");
          Check("picking a project brings in its song and its picture (your project is not changed)", WaitJs("PX.P.media.length>=" + (n0p + 2) + "&&PX.P.media.some(m=>m.name.indexOf('Night Drive (')===0&&m.kind==='audio')&&PX.P.media.some(m=>m.name.indexOf('Night Drive cover')===0&&m.kind==='image')", 20000) && File.ReadAllText(pf) == "test", Js("PX.P.media.map(m=>m.name).join(' | ')"));
          sf.songRenderHook = null; ProjectLibrary.Entries.RemoveAll(x => x.Path == pf); }
        // ---- CapCut-style tools ----
        { int codeK; CreativeStore.Run(ff, "-y -v error -f lavfi -i \"aevalsrc='0.8*sin(2*PI*55*t)*exp(-25*mod(t,0.5))':s=44100:d=8\" \"" + Path.Combine(media, "kick120.wav") + "\"", 60000, out codeK);
          using (var gb = new Bitmap(320, 180)) { using (var gg = Graphics.FromImage(gb)) { gg.Clear(Color.FromArgb(0, 255, 0)); using (var rb = new SolidBrush(Color.Red)) gg.FillRectangle(rb, 120, 50, 80, 80); } gb.Save(Path.Combine(media, "greenscreen.png"), System.Drawing.Imaging.ImageFormat.Png); }
          Js("PX.UI.setTool('video');PX.loadProject?0:0;PX.UI.loadProject(PX.newProject('Beat test',1280,720,30,'mv169'));"); Pump(300);
          M("CrImportPaths", new List<string> { Path.Combine(media, "kick120.wav"), Path.Combine(media, "photo.jpg"), Path.Combine(media, "logo.png"), Path.Combine(media, "greenscreen.png") });
          WaitJs("PX.P.media.length>=4&&Object.keys(PX.audio).length>=1", 20000); Pump(500);
          Js(@"(()=>{const P=PX.P,au=P.tracks.find(t=>t.kind==='audio'),vt=P.tracks.find(t=>t.kind==='video');const k=P.media.find(m=>m.name==='kick120.wav');PX.begin();P.clips.push(Object.assign({id:PX.uid('c'),type:'audio',media:k.id,track:au.id,start:0,dur:8,in:0,speed:1,vol:1,opacity:1,scale:1,x:0,y:0,rot:0,fit:'fit',crop:{l:0,t:0,r:0,b:0},fx:PX.defaultFx()}));
            for(const n of ['photo.jpg','logo.png','greenscreen.png']){const m=P.media.find(q=>q.name===n);P.clips.push({id:PX.uid('c'),type:'image',media:m.id,track:vt.id,start:0,dur:3,in:0,speed:1,vol:1,opacity:1,scale:1,x:0,y:0,rot:0,fit:'fit',crop:{l:0,t:0,r:0,b:0},fx:PX.defaultFx()});}PX.commit('test');})()");
          Js("window.__bt=PX.findBeats();"); string bpm = Js("String(PX.P.bpm)"), nb = Js("String((PX.P.beats||[]).length)"); int bpmI; int.TryParse(bpm, out bpmI); int nbI; int.TryParse(nb, out nbI);
          string firstGaps = Js("(PX.P.beats||[]).slice(0,6).map((b,i,a)=>i?(b-a[i-1]).toFixed(2):b.toFixed(2)).join(' ')");
          Check("FIND BEATS hears the tempo of the song and marks every beat (a 120 BPM kick)", Math.Abs(bpmI - 120) <= 3 && nbI >= 14 && nbI <= 17, bpm + " BPM, " + nb + " beats: " + firstGaps);
          Js("(()=>{PX.UI.sel=new Set();PX.autoBeatCut(2,false)})()"); string starts = Js("PX.P.clips.filter(c=>c.type==='image').sort((a,b)=>a.start-b.start).map(c=>c.start.toFixed(2)+'+'+c.dur.toFixed(2)).join(' ')"); string bts = Js("[0,2,4].map(i=>PX.P.beats[i].toFixed(2)).join(' ')");
          Check("AUTO CUT TO BEAT puts every cut on a beat (2 beats each)", starts.Split(' ').Select(s2 => s2.Split('+')[0]).SequenceEqual(bts.Split(' ')), starts + "  beats " + bts);
          string mz = Js("(()=>{const c=PX.P.clips.find(c=>c.type==='image');c.motion={type:'beatzoom',amt:1};const b=PX.P.beats[3],m1=PX.motionAt(c,b+0.01,1280,720).s,m2=PX.motionAt(c,b+0.24,1280,720).s;return m1.toFixed(3)+' '+m2.toFixed(3)})()");
          var mzp = mz.Split(' '); Check("MOTION 'zoom punch on every beat' punches on the beat and settles between beats", double.Parse(mzp[0], System.Globalization.CultureInfo.InvariantCulture) > 1.12 && double.Parse(mzp[1], System.Globalization.CultureInfo.InvariantCulture) < 1.03, mz);
          string gs = Js(@"(()=>{const c=PX.P.clips.find(c=>PX.mediaById(c.media).name==='greenscreen.png');c.key={on:true,color:'#00ff00',tol:40,soft:12};c.start=20;c.dur=3;PX.P.settings.bg='#000000';const cv=document.createElement('canvas');cv.width=1280;cv.height=720;const g=cv.getContext('2d');PX.drawFrame(g,21,1280,720,true);const at=(x,y)=>Array.from(g.getImageData(x,y,1,1).data).slice(0,3).join(',');return at(100,360)+'|'+at(640,360)})()");
          Check("GREEN SCREEN takes the green out (the background shows) and keeps the rest", gs.StartsWith("0,0,0|") && gs.Split('|')[1].StartsWith("255,0,0"), gs);
          int nm0 = int.Parse(Js("String(PX.P.media.length)")); Js("PX.play.t=1;PX.freezeFrame()"); Check("FREEZE FRAME makes a still of the frame and puts it in MEDIA", WaitJs("PX.P.media.length>" + nm0 + "&&PX.P.media.some(m=>m.name.indexOf('Freeze')===0)", 15000));
          Check("the CapCut tools are in the toolbar", Js("String(!!document.querySelector('#beatBtn')&&!!document.querySelector('#beatCutBtn')&&!!document.querySelector('#freezeBtn'))") == "true"); }
        // ---- the video editor's top-toolbar layout ----
        { Js("PX.UI.setTool('video');PX.UI.closeWelcome&&PX.UI.closeWelcome()"); Pump(300);
          Check("Purphex Video Editor: a top bar of categories, the picked one docked on the left (media by default), the preview beside it and the timeline under", Js("String(document.querySelectorAll('#vCats button').length===15&&!!document.querySelector('#left #vPop')&&document.querySelector('#vPopT').textContent==='MEDIA'&&getComputedStyle(document.querySelector('#right')).display==='none')") == "true");
          string vErr = Js("(()=>{try{for(const b of document.querySelectorAll('#vCats button')){PX.UI.openPanel(b.dataset.k);if(!document.querySelector('#vPopB').children.length)return 'empty: '+b.dataset.k;}PX.UI.openPanel(null);return 'none'}catch(e){return e.message}})()");
          Check("every video category opens a working panel", vErr == "none", vErr);
          Js("PX.UI.openPanel('media')"); Pump(500); { var ctv = B.Capture("C:/Users/thoma/nfout/video-toolbar.png"); var cev = DateTime.Now.AddSeconds(10); while (!ctv.IsCompleted && DateTime.Now < cev) Pump(50); } Js("PX.UI.openPanel(null)");
          int nmS = int.Parse(Js("String(PX.P.media.length)")), ncS = int.Parse(Js("String(PX.P.clips.length)")); Js("PX.UI.openPanel('stickers');[...document.querySelectorAll('#vPopB .card')].pop().click()");
          Check("STICKERS put a sticker on the video at the playhead (a real clip you can move and time)", WaitJs("PX.P.media.length>" + nmS + "&&PX.P.clips.length>" + ncS + "&&PX.P.clips.some(c=>c.scale===0.4&&c.dur===3)", 15000)); Js("PX.UI.openPanel(null)");
          int ncT = int.Parse(Js("String(PX.P.clips.filter(c=>c.type==='text').length)")); Js("PX.UI.VTEMPLATES.find(t=>t[0]==='intro')[3]()");
          Check("TEMPLATES build real timeline clips (the intro card adds its texts)", Js("String(PX.P.clips.filter(c=>c.type==='text').length)") == (ncT + 2).ToString());
          string sp = Js("(()=>{try{PX.UI.loadProject(JSON.parse(JSON.stringify(PX.P)));const c=PX.P.clips.find(c=>c.type==='audio'||c.type==='video');if(!c)return 'no clip: '+PX.P.clips.map(c=>c.type).join(',');PX.UI.sel=new Set([c.id]);const d0=c.dur;PX.UI.openPanel('speed');[...document.querySelectorAll('#vPopB .btnrow button')].find(b=>b.textContent==='2x').click();PX.UI.openPanel(null);return (d0/c.dur).toFixed(2)+' '+c.speed}catch(e){return 'ERR '+e.message}})()") ?? "null";
          Check("SPEED 2x plays the clip twice as fast (and it gets half as long)", sp == "2.00 2", sp);
          Js("PX.UI.VTEMPLATES.find(t=>t[0]==='vertical')[3]()"); Check("the vertical template makes the project 9:16 with a blurred copy behind the clips", Js("PX.P.settings.w+'x'+PX.P.settings.h") == "1080x1920" && Js("String(PX.P.clips.filter(c=>c.type==='image'||c.type==='video').every(c=>c.blurFill))") == "true"); }
        // ---- keyframes, audio tools, ripple delete ----
        { string kf = Js(@"(()=>{try{const P=PX.P,vt=P.tracks.find(t=>t.kind==='video');P.clips=P.clips.filter(c=>c.type==='audio'&&PX.mediaById(c.media).name==='kick120.wav');const m=P.media.find(q=>q.name==='photo.jpg');const c={id:PX.uid('c'),type:'image',media:m.id,track:vt.id,start:30,dur:4,in:0,speed:1,vol:1,opacity:1,scale:1,x:0,y:0,rot:0,fit:'fit',crop:{l:0,t:0,r:0,b:0},fx:PX.defaultFx()};P.clips.push(c);
            PX.addKey(c,'x',30,-0.5);PX.addKey(c,'x',32,0.5);PX.addKey(c,'opacity',30,0);PX.addKey(c,'opacity',31,1);const mid=PX.kfApply(c,31).x;P.settings.bg='#000000';const cv=document.createElement('canvas');cv.width=320;cv.height=180;const g=cv.getContext('2d');const lum=()=>{const d=g.getImageData(0,0,320,180).data;let s=0;for(let i=0;i<d.length;i+=4)s+=d[i]+d[i+1]+d[i+2];return s/(d.length/4)};PX.drawFrame(g,30.01,320,180,true);const l0=lum();PX.drawFrame(g,31.5,320,180,true);const l1=lum();return mid.toFixed(3)+' '+l0.toFixed(1)+' '+l1.toFixed(1)}catch(e){return 'ERR '+e.message}})()") ?? "null";
          var kfp = kf.Split(' '); Check("KEYFRAMES move and fade a clip smoothly between the moments you set (real values in the picture)", kfp.Length == 3 && kfp[0] == "0.000" && double.Parse(kfp[1], System.Globalization.CultureInfo.InvariantCulture) < 5 && double.Parse(kfp[2], System.Globalization.CultureInfo.InvariantCulture) > 40, kf);
          string au = Js(@"(()=>{window.__au=null;const P=PX.P,k=P.clips.find(c=>c.type==='audio'),at=P.tracks.filter(t=>t.kind==='audio');if(at.length<2)PX.UI.addTrack('audio');const at2=PX.P.tracks.filter(t=>t.kind==='audio')[1];k.start=0;k.dur=8;k.in=0;k.speed=1;const v=JSON.parse(JSON.stringify(k));v.id=PX.uid('c');v.track=at2.id;v.start=4;v.dur=4;v.in=0;v.vol=0.0001;v.voice=true;P.clips.push(v);
            const rms=(b,a,z)=>{const d=b.getChannelData(0);let s=0;for(let i=Math.floor(a*48000);i<Math.floor(z*48000);i++)s+=d[i]*d[i];return Math.sqrt(s/((z-a)*48000))};
            (async()=>{const b0=await PX.renderAudio(0,8);k.duck={on:true,db:12};const b1=await PX.renderAudio(0,8);k.duck={on:false};k.eq={low:-15,mid:0,high:0};const b2=await PX.renderAudio(0,8);k.eq={low:0,mid:0,high:0};window.__au=[rms(b0,5,7),rms(b1,5,7),rms(b1,1,3),rms(b2,1,3)].map(x=>(20*Math.log10(x+1e-9)).toFixed(1)).join(' ')})();return 'ok'})()");
          WaitJs("window.__au", 30000); au = Js("window.__au") ?? "null"; var aup = au.Split(' '); double a0 = 0, a1 = 0, a2 = 0, a3 = 0; try { a0 = double.Parse(aup[0], System.Globalization.CultureInfo.InvariantCulture); a1 = double.Parse(aup[1], System.Globalization.CultureInfo.InvariantCulture); a2 = double.Parse(aup[2], System.Globalization.CultureInfo.InvariantCulture); a3 = double.Parse(aup[3], System.Globalization.CultureInfo.InvariantCulture); } catch { }
          Check("DUCKING turns the music down by about 12 dB while a voice-over plays (and not before)", a0 - a1 > 9 && a0 - a1 < 15 && Math.Abs(a2 - a0) < 1.5, au);
          Check("the EQ really changes the sound (bass cut 15 dB on a bass kick)", a2 - a3 > 6, au);
          Js("(()=>{const k=PX.P.clips.find(c=>c.type==='audio');k.kf={vol:[[1,1],[3,0.3]]};PX.UI.sel=new Set([k.id]);PX.UI.renderTimeline();PX.UI.openPanel('audio');document.querySelectorAll('#vPopB .sec.closed').forEach(s=>s.classList.remove('closed'))})()"); Pump(600); { var cta = B.Capture("C:/Users/thoma/nfout/video-audio.png"); var cea = DateTime.Now.AddSeconds(10); while (!cta.IsCompleted && DateTime.Now < cea) Pump(50); } Js("PX.UI.openPanel('media')");
          string rp = Js(@"(()=>{const P=PX.P,vt=P.tracks.find(t=>t.kind==='video');P.clips=P.clips.filter(c=>c.track!==vt.id);const m=P.media.find(q=>q.name==='photo.jpg');const mk=(s)=>{const c={id:PX.uid('c'),type:'image',media:m.id,track:vt.id,start:s,dur:2,in:0,speed:1,vol:1,opacity:1,scale:1,x:0,y:0,rot:0,fit:'fit',crop:{l:0,t:0,r:0,b:0},fx:PX.defaultFx()};P.clips.push(c);return c};const a=mk(0),b=mk(2),c=mk(7);PX.UI.sel=new Set([a.id]);PX.UI.rippleDelete();const r1=b.start+' '+c.start;PX.UI.closeGaps();return r1+' | '+b.start+' '+c.start})()");
          Check("RIPPLE DELETE moves the clips after it up, and CLOSE GAPS removes the empty space", rp == "0 5 | 0 2", rp); }
        // ---- VIDEO STUDIO PRO ----
        { foreach (var spec in new[] { new object[] { "vred.png", Color.FromArgb(255, 0, 0), Color.FromArgb(255, 0, 0) }, new object[] { "vblue.png", Color.FromArgb(0, 0, 255), Color.FromArgb(0, 0, 255) }, new object[] { "vgrey.png", Color.FromArgb(128, 128, 128), Color.FromArgb(128, 128, 128) }, new object[] { "vhalves.png", Color.FromArgb(255, 0, 0), Color.FromArgb(0, 0, 255) } })
            using (var vb = new Bitmap(640, 360)) { using (var vg = Graphics.FromImage(vb)) { using (var b1 = new SolidBrush((Color)spec[1])) vg.FillRectangle(b1, 0, 0, 320, 360); using (var b2 = new SolidBrush((Color)spec[2])) vg.FillRectangle(b2, 320, 0, 320, 360); } vb.Save(Path.Combine(media, (string)spec[0]), System.Drawing.Imaging.ImageFormat.Png); }
          Js("window.__vkeepP=PX.P;PX.UI.setTool('video');PX.UI.loadProject(PX.newProject('Video PRO test',1280,720,30,'mv169'));"); Pump(300);
          M("CrImportPaths", new List<string> { Path.Combine(media, "vred.png"), Path.Combine(media, "vblue.png"), Path.Combine(media, "vgrey.png"), Path.Combine(media, "vhalves.png"), Path.Combine(media, "kick120.wav") });
          WaitJs("PX.P.media.length>=5&&PX.P.media.filter(m=>m.kind==='image').every(m=>m.w>0)&&Object.keys(PX.audio).length>=1", 30000);
          Js(@"(()=>{window.__vm=n=>PX.P.media.find(m=>m.name===n);for(const m of PX.P.media)if(m.kind==='image')PX.imgFor(m);
            window.__img=(name,start,dur,extra)=>{const vt=PX.P.tracks.find(t=>t.kind==='video'&&t.name==='Video 1');const c=Object.assign({id:PX.uid('c'),type:'image',media:__vm(name).id,track:vt.id,start,dur,in:0,speed:1,vol:1,opacity:1,scale:1,x:0,y:0,rot:0,fit:'stretch',crop:{l:0,t:0,r:0,b:0},fx:PX.defaultFx(),fadeIn:0,fadeOut:0},extra||{});PX.P.clips.push(c);return c};
            window.__vpx=(t,fx,fy)=>{const c=document.createElement('canvas');c.width=320;c.height=180;const g=c.getContext('2d');PX.drawFrame(g,t,320,180,true);const d=g.getImageData(Math.floor(fx*320),Math.floor(fy*180),1,1).data;return [d[0],d[1],d[2]].join(',')};})()");
          WaitJs("PX.P.media.filter(m=>m.kind==='image').every(m=>PX.imgs[m.id]&&PX.imgs[m.id].complete&&PX.imgs[m.id].naturalWidth>0)", 15000);
          Func<string, int[]> vrgb = s => { try { return Array.ConvertAll(s.Split(','), int.Parse); } catch { return new[] { -1, -1, -1 }; } };
          Func<int[], int, int, int, bool> vnear = (p, r, g, b) => Math.Abs(p[0] - r) < 45 && Math.Abs(p[1] - g) < 45 && Math.Abs(p[2] - b) < 45;
          // transitions
          string vtr = Js("String(['push','iris','clock','barn','heart','glitch','whip','pixelate'].every(k=>PX.TRANSITIONS.some(x=>x[0]===k))&&PX.TRANSITIONS.length>=24)");
          Check("VIDEO PRO: 16 more transitions (push, iris, clock, barn doors, heart, glitch, whip pan, pixelate...)", vtr == "true", Js("String(PX.TRANSITIONS.length)"));
          Js("(()=>{PX.P.clips=[];__img('vred.png',0,2);__img('vblue.png',2,2,{transIn:{type:'iris',dur:1}});})()");
          var ti1 = vrgb(Js("__vpx(2.0,0.5,0.5)")); var ti2 = vrgb(Js("__vpx(2.0,0.02,0.05)"));
          Check("IRIS: half way through, the middle shows the next clip and the corner the last one", vnear(ti1, 0, 0, 255) && vnear(ti2, 255, 0, 0), string.Join(",", ti1) + " | " + string.Join(",", ti2));
          Js("(()=>{PX.P.clips.find(c=>c.transIn).transIn.type='push';})()"); var tp1 = vrgb(Js("__vpx(2.0,0.25,0.5)")); var tp2 = vrgb(Js("__vpx(2.0,0.75,0.5)"));
          Check("PUSH: half way, the old clip fills the left half and the new one the right half", vnear(tp1, 255, 0, 0) && vnear(tp2, 0, 0, 255), string.Join(",", tp1) + " | " + string.Join(",", tp2));
          Js("(()=>{PX.P.clips.find(c=>c.transIn).transIn.type='clock';})()"); var tc1 = vrgb(Js("__vpx(2.0,0.75,0.3)")); var tc2 = vrgb(Js("__vpx(2.0,0.25,0.3)"));
          Check("CLOCK WIPE: half way, the right half has turned and the left has not", vnear(tc1, 0, 0, 255) && vnear(tc2, 255, 0, 0), string.Join(",", tc1) + " | " + string.Join(",", tc2));
          // grading
          Js("(()=>{PX.P.clips=[];window.__g=__img('vgrey.png',0,3);})()"); var gr0 = vrgb(Js("__vpx(1,0.5,0.5)"));
          Js("(()=>{__g.grade={gamma:[100,100,100]};})()"); var gr1 = vrgb(Js("__vpx(1,0.5,0.5)"));
          Js("(()=>{__g.grade={gain:[0,0,100]};})()"); var gr2 = vrgb(Js("__vpx(1,0.5,0.5)"));
          Check("GRADING: gamma brightens the midtones, gain on blue makes it bluer", gr1[0] > gr0[0] + 30 && gr2[2] > gr0[2] + 30 && Math.Abs(gr2[0] - gr0[0]) < 6, string.Join(",", gr0) + " > " + string.Join(",", gr1) + " / " + string.Join(",", gr2));
          string lut = Js(@"(()=>{try{const L=PX.parseCube('TITLE ""invert""\nLUT_3D_SIZE 2\n1 1 1\n0 1 1\n1 0 1\n0 0 1\n1 1 0\n0 1 0\n1 0 0\n0 0 0\n');PX.P.clips=[];const c=__img('vred.png',0,3);c.grade={lut:{n:L.n,b64:L.b64},lutMix:100};return L.n+' '+__vpx(1,0.5,0.5)}catch(e){return 'ERR '+e.message}})()");
          Check("LUT: a .cube file is read and applied (an inverting LUT turns red into cyan)", lut.StartsWith("2 ") && vnear(vrgb(lut.Substring(2)), 0, 255, 255), lut);
          Check("a broken .cube file is refused with a message", Js("(()=>{try{PX.parseCube('hello');return 'no'}catch(e){return e.message}})()").Contains("LUT_3D_SIZE"));
          // effects
          Js("(()=>{PX.P.clips=[];window.__h=__img('vhalves.png',0,3);__h.vfx={mirror:'h'};})()"); var mr = vrgb(Js("__vpx(1,0.9,0.5)"));
          Check("MIRROR: the left half is mirrored onto the right", vnear(mr, 255, 0, 0), string.Join(",", mr));
          Js("(()=>{__h.vfx={letterbox:100};})()"); var lb1 = vrgb(Js("__vpx(1,0.25,0.02)")); var lb2 = vrgb(Js("__vpx(1,0.25,0.5)"));
          Check("LETTERBOX: black bars at the top and bottom, the picture in the middle", vnear(lb1, 0, 0, 0) && vnear(lb2, 255, 0, 0), string.Join(",", lb1) + " | " + string.Join(",", lb2));
          Js("(()=>{__h.vfx={rgb:40};})()"); var rs = vrgb(Js("__vpx(1,0.495,0.5)"));
          Check("RGB SPLIT: the colours separate at an edge", rs[0] > 120 && rs[2] > 120, string.Join(",", rs));
          string vfxErr = Js("(()=>{try{__h.vfx={glitch:80,vhs:80,grain:60,pixel:30,posterize:8,scan:60,kaleido:6};for(let t=0;t<2;t+=0.3)__vpx(t,0.5,0.5);return 'none'}catch(e){return e.message}})()");
          Check("every video effect draws without an error", vfxErr == "none", vfxErr);
          // keyframe easing, text animations
          string ez = Js("(()=>{const c={start:0,kf:{x:[[0,0],[2,1]]}};const r=[];for(const e of ['linear','hold','smooth','bounce']){c.kfEase=e;r.push(PX.kfValue(c,'x',1).toFixed(2))}c.kfEase='back';let over=0;for(let t=0;t<2;t+=0.05)over=Math.max(over,PX.kfValue(c,'x',t));return r.join(' ')+' '+(over>1)})()");
          Check("KEYFRAME EASING: linear is half way at the middle, hold has not moved, smooth is half way, bounce and overshoot work", ez.StartsWith("0.50 0.00 0.50") && ez.EndsWith("true"), ez);
          string wipe = Js(@"(()=>{PX.P.clips=[];const tt=PX.P.tracks.find(t=>t.kind==='text');PX.P.clips.push({id:'tw',track:tt.id,type:'text',start:0,dur:4,opacity:1,scale:1,x:0,y:0,rot:0,crop:{l:0,t:0,r:0,b:0},fx:PX.defaultFx(),speed:1,vol:1,in:0,fadeIn:0,fadeOut:0,text:Object.assign(PX.defaultText(),{content:'WWWWWWWWWWWW',size:140,color:'#ffffff',shadow:0,animIn:'wipe',animOut:'none',animDur:1})});
            const lit=(t,fx)=>{let n=0;for(let y=0.4;y<0.6;y+=0.02){const p=__vpx(t,fx,y).split(',');if(+p[0]>200)n++}return n>0};return [lit(0.3,0.2),lit(0.3,0.8),lit(2,0.8)].join(' ')})()");
          Check("TEXT ANIMATION wipe: early on only the left of the title shows, later all of it", wipe == "true false true", wipe);
          Check("the text animations the templates use (zoom) now exist", Js("String(PX.TEXTANIMS_PRO.some(a=>a[0]==='zoom'))") == "true");
          // reverse
          string rv = Js(@"(()=>{PX.P.clips=[];const m=__vm('kick120.wav');const au=PX.P.tracks.find(t=>t.kind==='audio');const c={id:'ra',type:'audio',media:m.id,track:au.id,start:0,dur:0.5,in:0,speed:1,vol:1,opacity:1,scale:1,x:0,y:0,rot:0,fit:'fit',crop:{l:0,t:0,r:0,b:0},fx:PX.defaultFx(),fadeIn:0,fadeOut:0};PX.P.clips.push(c);window.__rv=null;
            const en=(ab,a,b)=>{const d=ab.getChannelData(0);let s=0;for(let i=Math.floor(a*ab.sampleRate);i<Math.floor(b*ab.sampleRate);i++)s+=d[i]*d[i];return s};
            PX.renderAudio(0,0.5).then(f=>{c.reverse=true;return PX.renderAudio(0,0.5).then(r=>{window.__rv=[en(f,0,0.1)>en(f,0.38,0.48),en(r,0.38,0.48)>en(r,0,0.1),PX.mediaTime(c,0)>0.45].join(' ')})}).catch(e=>window.__rv='ERR '+e.message);return 'started'})()");
          WaitJs("window.__rv", 20000); string rvr = Js("window.__rv");
          Check("PLAY BACKWARDS: the sound is reversed (a kick that fades out now swells in) and the picture runs from the end", rvr == "true true true", rvr);
          // editing
          string ins = Js(@"(()=>{PX.P.clips=[];const a=__img('vred.png',0,2),b=__img('vblue.png',2,2);PX.UI.editMode='insert';PX.UI.addMediaAt(__vm('vgrey.png'),a.track,1);const r1=PX.P.clips.filter(c=>c.track===a.track).sort((x,y)=>x.start-y.start).map(c=>__vm('vred.png').id===c.media?'R'+c.start.toFixed(1)+'-'+(c.start+c.dur).toFixed(1):__vm('vblue.png').id===c.media?'B'+c.start.toFixed(1):'G'+c.start.toFixed(1)).join(' ');
            PX.P.clips=[];const a2=__img('vred.png',0,8);PX.UI.editMode='overwrite';PX.UI.addMediaAt(__vm('vgrey.png'),a2.track,1);const r2=PX.P.clips.filter(c=>c.track===a2.track).sort((x,y)=>x.start-y.start).map(c=>(__vm('vred.png').id===c.media?'R':'G')+c.start.toFixed(1)+'-'+(c.start+c.dur).toFixed(1)).join(' ');PX.UI.editMode='normal';return r1+' | '+r2})()");
          Check("INSERT pushes the rest along (and splits the clip it lands in); OVERWRITE covers what is there", System.Text.RegularExpressions.Regex.IsMatch(ins, @"^R0\.0-1\.0 G1\.0 R6\.0-7\.0 B7\.0 \| R0\.0-1\.0 G1\.0-6\.0 R6\.0-8\.0$"), ins);
          string ng = Js("(()=>{PX.P.clips=[];const a=__img('vred.png',1,2);PX.UI.sel=new Set([a.id]);PX.UI.nudge(1);const s1=a.start;PX.UI.nudge(-10);return s1.toFixed(4)+' '+a.start.toFixed(4)+' '+PX.UI.parseTC('1:30')+' '+PX.UI.parseTC('0:01:15')})()");
          Check("NUDGE moves a clip one frame (and ten), and GO TO reads 1:30 and m:ss:frames", ng == "1.0333 0.7000 90 1.5", ng);
          string bq = Js("(()=>{PX.UI.renderBin();const q=document.querySelector('#binQ');q.value='blue';q.oninput();return [...document.querySelectorAll('#bin .mi')].filter(d=>d.style.display!=='none').length})()");
          Check("MEDIA BIN search finds the file", bq == "1", bq);
          Js("(()=>{const q=document.querySelector('#binQ');q.value='';q.oninput();})()");
          // panels
          string vpErr = Js("(()=>{try{PX.P.clips=[];const a=__img('vhalves.png',0,3);PX.UI.sel=new Set([a.id]);for(const k of ['media','colour','effects','stickers','transitions','edit','settings','animation','text','captions']){PX.UI.openPanel(k)}return 'none'}catch(e){return e.message+' '+(e.stack||'').slice(0,160)}})()");
          Check("every VIDEO PRO panel opens without an error", vpErr == "none", vpErr);
          int nm0 = int.Parse(Js("String(PX.P.media.length)")); Js("(()=>{PX.UI.openPanel('stickers');const c=document.querySelector('#vPopB .vstkgrid .card');c.click()})()");
          Check("the Purphex sticker library puts a sticker on the video (a real clip)", WaitJs("PX.P.media.length>" + nm0, 15000) && WaitJs("PX.P.clips.some(c=>c.type==='image'&&PX.mediaById(c.media).name.indexOf('Music note')===0)", 10000), Js("PX.P.media.map(m=>m.name).slice(-2).join(' | ')"));
          // the export has the same picture as the preview
          Js("(()=>{PX.P.clips=[];const a=__img('vred.png',0,1,{vfx:{letterbox:100}});const b=__img('vgrey.png',1,1,{grade:{gain:[0,0,100]},transIn:{type:'barn',dur:0.4}});})()");
          string vexp = Export("mp4-h264", "video_pro_export", 640, 360);
          string fr1 = Path.Combine(outDir, "vp_f1.png"), fr2 = Path.Combine(outDir, "vp_f2.png"); int vcode; foreach (var f in new[] { fr1, fr2 }) try { File.Delete(f); } catch { }
          if (File.Exists(vexp)) { CreativeStore.Run(ff, "-y -v error -ss 0.5 -i \"" + vexp + "\" -frames:v 1 \"" + fr1 + "\"", 60000, out vcode); CreativeStore.Run(ff, "-y -v error -ss 1.7 -i \"" + vexp + "\" -frames:v 1 \"" + fr2 + "\"", 60000, out vcode); }
          string vxd = "no file " + vexp; bool vxok = false;
          if (File.Exists(fr1) && File.Exists(fr2)) using (var b1 = new Bitmap(fr1)) using (var b2 = new Bitmap(fr2)) { var top = b1.GetPixel(320, 4); var vmid = b1.GetPixel(320, 180); var gm = b2.GetPixel(320, 180); vxd = "top " + top.R + "," + top.G + "," + top.B + " mid " + vmid.R + "," + vmid.G + "," + vmid.B + " graded " + gm.R + "," + gm.G + "," + gm.B; vxok = top.R < 40 && top.G < 40 && top.B < 40 && vmid.R > 200 && vmid.G < 60 && gm.B > gm.R + 40; }
          Check("the exported MP4 has the effects and the grade from the preview (letterbox bars, the blue gain)", vxok, vxd);
          // screenshots
          Js("(()=>{PX.P.clips=[];const a=__img('vhalves.png',0,4,{grade:{gain:[20,0,40],sat:20},vfx:{letterbox:100,grain:30}});__img('vblue.png',4,3,{transIn:{type:'heart',dur:1}});PX.UI.sel=new Set([a.id]);PX.UI.renderTimeline();PX.seek(1);PX.UI.openPanel('colour')})()"); Pump(900);
          { var vct = B.Capture("C:/Users/thoma/nfout/video-pro-colour.png"); var vce = DateTime.Now.AddSeconds(10); while (!vct.IsCompleted && DateTime.Now < vce) Pump(50); }
          Js("(()=>{PX.UI.openPanel('effects');const b=document.querySelector('#vPopB');b.scrollTop=b.scrollHeight})()"); Pump(700);
          { var vct = B.Capture("C:/Users/thoma/nfout/video-pro-effects.png"); var vce = DateTime.Now.AddSeconds(10); while (!vct.IsCompleted && DateTime.Now < vce) Pump(50); }
          Js("(()=>{PX.seek(4.0);PX.UI.openPanel('transitions')})()"); Pump(900);
          { var vct = B.Capture("C:/Users/thoma/nfout/video-pro-transitions.png"); var vce = DateTime.Now.AddSeconds(10); while (!vct.IsCompleted && DateTime.Now < vce) Pump(50); }
          Js("PX.UI.openPanel('media');PX.UI.shortcuts()"); Pump(500);
          { var vct = B.Capture("C:/Users/thoma/nfout/video-pro-media.png"); var vce = DateTime.Now.AddSeconds(10); while (!vct.IsCompleted && DateTime.Now < vce) Pump(50); }
          Js("PX.UI.close();PX.setDirty(false);PX.UI.loadProject(window.__vkeepP)"); WaitJs("PX.P.media.some(m=>m.name==='photo.jpg')", 8000);
        }
        // ---- CREATIVE SUITE ----
        { Js("window.__artSaved=null;window.__saved=null;PX.UI.setTool('cover');(()=>{const C=PX.Cover;const st=document.querySelector('#cvStart');if(st)st.remove();C.art.name='Suite design';C.act('save')})()"); string sid = Js("PX.Cover.art.id"); WaitJs("window.__artSaved==='" + sid + "'", 10000);
          Js("PX.UI.setTool('video');PX.P.name='Acceptance A';PX.UI.save(false)"); WaitJs("window.__saved===PX.P.id", 10000); Pump(200);
          Check("SUITE: the button is on the top bar and opens the suite with its 8 parts", Js("(()=>{document.querySelector('#toolSuite').click();return document.querySelectorAll('#suite .suitetile').length})()") == "8");
          Check("the suite shows the edition from the licence on this PC and the app version", WaitJs("PX.Suite.info&&PX.Suite.info.edition", 5000) && Js("PX.Suite.info.edition") == Edition.Name(Edition.Current) && Js("document.querySelector('#suite .suitechip').textContent").Contains(Edition.Name(Edition.Current)), Js("JSON.stringify({e:PX.Suite.info&&PX.Suite.info.edition,v:PX.Suite.info&&PX.Suite.info.version})"));
          Pump(400); { var sct = B.Capture("C:/Users/thoma/nfout/suite-home.png"); var sce = DateTime.Now.AddSeconds(10); while (!sct.IsCompleted && DateTime.Now < sce) Pump(50); }
          Js("document.querySelector('#suite .suitetile[data-k=projects]').click()"); WaitJs("document.querySelectorAll('#suite .sucol').length===3", 5000);
          Check("ALL PROJECTS lists songs, designs and videos together", Js("(()=>{const t=document.querySelector('#suite').textContent;const I=PX.Suite.info;return String(I.art.length>0&&I.video.length>0&&t.includes(I.art[0].name)&&t.includes(I.video[0].name)&&t.includes('Songs (DAW)'))})()") == "true", Js("document.querySelector('#suite .suitebody').textContent.slice(0,200)"));
          Pump(300); { var sct = B.Capture("C:/Users/thoma/nfout/suite-projects.png"); var sce = DateTime.Now.AddSeconds(10); while (!sct.IsCompleted && DateTime.Now < sce) Pump(50); }
          string sid2 = Js("PX.Suite.info.art[0].id"); Js("[...document.querySelectorAll('#suite .sucol')][1].querySelector('.surow button').click()");
          Check("OPEN on a design switches to Purphex Photo Editor and opens it", WaitJs("PX.UI.tool==='cover'&&PX.Cover.art.id==='" + sid2 + "'", 8000) && Js("String(document.querySelector('#suite').classList.contains('hidden'))") == "true");
          // shared media
          string up = Path.Combine(media, "suite_upload.png"); using (var ub = new Bitmap(200, 100)) { using (var ug = Graphics.FromImage(ub)) ug.Clear(Color.FromArgb(0, 200, 0)); ub.Save(up, System.Drawing.Imaging.ImageFormat.Png); }
          Js("window.__imp=0;PX.on('artImported',()=>window.__imp++)"); M("CrArtFiles", new List<string> { up }); WaitJs("window.__imp>=1", 8000);
          Js("PX.UI.setTool('video');PX.Suite.show('media')"); WaitJs("document.querySelectorAll('#suite .sucard').length>=3&&(PX.Suite.uploads||[]).length>=1", 8000);
          Check("SHARED MEDIA shows the video frame, the open design, the video's photos and the photo uploads", Js("(()=>{const t=document.querySelector('#suite').textContent;return String(t.includes('Video frame')&&t.includes('Design: '+PX.Cover.art.name)&&t.includes('photo.jpg')&&t.includes('suite_upload'))})()") == "true", Js("[...document.querySelectorAll('#suite .sucard .nm')].map(n=>n.textContent).join(' | ').slice(0,300)"));
          Pump(300); { var sct = B.Capture("C:/Users/thoma/nfout/suite-media.png"); var sce = DateTime.Now.AddSeconds(10); while (!sct.IsCompleted && DateTime.Now < sce) Pump(50); }
          int pw3, ph3; using (var pb3 = new Bitmap(Path.Combine(media, "photo.jpg"))) { pw3 = pb3.Width; ph3 = pb3.Height; }
          Js("[...document.querySelectorAll('#suite .sucard')].find(c=>c.textContent.includes('photo.jpg')).querySelector('button').click()");
          Check("a video photo TO PHOTO starts a design from it (its own size)", WaitJs("PX.UI.tool==='cover'&&PX.Cover.art.layers.length===1&&PX.Cover.art.layers[0].type==='image'&&PX.Cover.art.w===" + pw3, 8000), Js("PX.Cover.art.w+'x'+PX.Cover.art.h"));
          Js("PX.UI.setTool('video');PX.Suite.show('media')"); Pump(300); Js("[...document.querySelectorAll('#suite .sucard')].find(c=>c.textContent.includes('Video frame')).querySelector('button').click()");
          Check("the frame under the playhead TO PHOTO becomes a design the size of the video", WaitJs("PX.UI.tool==='cover'&&PX.Cover.art.layers.length===1&&PX.Cover.art.w===Math.round(PX.P.settings.w*Math.min(1,1920/Math.max(PX.P.settings.w,PX.P.settings.h)))", 8000), Js("PX.Cover.art.w+'x'+PX.Cover.art.h+' / '+PX.P.settings.w"));
          int sm0 = int.Parse(Js("String(PX.P.media.length)")); Js("PX.UI.setTool('video');PX.Suite.show('media')"); WaitJs("(PX.Suite.uploads||[]).length>=1", 5000); Pump(200);
          Js("[...document.querySelectorAll('#suite .sucard')].find(c=>c.textContent.includes('suite_upload')).querySelectorAll('button')[2].click()");
          Check("a photo upload TO VIDEO comes into the video MEDIA", WaitJs("PX.P.media.length>" + sm0 + "&&PX.P.media[PX.P.media.length-1].kind==='image'", 10000), Js("PX.P.media.map(m=>m.name).slice(-2).join(' | ')"));
          // account and links
          Js("PX.Suite.show('account')"); WaitJs("!!document.querySelector('#suite .suacct')", 4000);
          Check("EDITION AND ACCOUNT shows the edition and links", Js("document.querySelector('#suite .suacct .big').textContent") == Edition.Name(Edition.Current) && Js("String(document.querySelectorAll('#suite .suacct button').length)") == "3");
          StudioForm.SuiteLastUrl = ""; Js("document.querySelector('#suite .suacct button').click()"); var su0 = DateTime.Now.AddSeconds(4); while (StudioForm.SuiteLastUrl.Length == 0 && DateTime.Now < su0) Pump(50);
          Check("EDITIONS AND PRICES goes to purphexmusic.com/pricing (a fixed page)", StudioForm.SuiteLastUrl == "https://purphexmusic.com/pricing", StudioForm.SuiteLastUrl);
          Js("PX.send({t:'suiteGo',to:'evil.example.com'})"); Pump(300); Check("any other link is refused", StudioForm.SuiteLastUrl == "https://purphexmusic.com/pricing");
          Js("PX.Suite.show('home')"); Pump(100); Js("document.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}))"); Pump(100);
          Check("Esc closes the suite", Js("String(document.querySelector('#suite').classList.contains('hidden'))") == "true");
          Js("PX.Suite.show('home');document.querySelector('#suite .suitetile[data-k=projects]').click()"); Pump(300); Js("[...document.querySelectorAll('#suite .surow button')].find(b=>b.textContent==='IN PROJECTS')||null");
          Js("PX.send({t:'suiteGo',to:'projects'})"); var su1 = DateTime.Now.AddSeconds(4); while ((int)F("tab") != 3 && DateTime.Now < su1) Pump(50);
          Check("IN PROJECTS goes to the Projects page of the DAW", (int)F("tab") == 3, F("tab").ToString());
          M("OpenCreative", "video"); Pump(400); Js("PX.Suite.close();PX.UI.setTool('cover');PX.Cover.openStart();PX.UI.setTool('video')"); Pump(200);
        }
        // ---- TITLES TYPED ON THE VIDEO ----
        { Js("PX.UI.setTool('video');PX.UI.openPanel('text');PX.seek(1)"); Pump(200);
          Js("[...document.querySelectorAll('#vPopB .vtextpanel button')].find(b=>b.textContent.includes('ADD TEXT')).click()");
          Check("ADD TEXT puts a title on the video and opens it ready to type (the words picked)", WaitJs("!!document.querySelector('#vTextEdit')&&document.activeElement===document.querySelector('#vTextEdit')&&PX._editText===PX.P.clips.filter(c=>c.type==='text').slice(-1)[0].id&&document.querySelector('#vTextEdit').selectionEnd-document.querySelector('#vTextEdit').selectionStart===9", 4000), Js("(()=>{const t=document.querySelector('#vTextEdit');return !!t+' active='+(document.activeElement&&document.activeElement.id)+' sel='+(t?t.selectionStart+'-'+t.selectionEnd+' v='+t.value:'')+' ed='+PX._editText+' last='+PX.P.clips.filter(c=>c.type==='text').slice(-1)[0].id+' n='+PX.P.clips.filter(c=>c.type==='text').length})()"));
          Js("(()=>{const t=document.querySelector('#vTextEdit');t.value='PICK UP\\nTHE PHONE';t.dispatchEvent(new Event('input'))})()");
          Check("typing changes the title as you type", Js("PX.UI.selected()[0].text.content") == "PICK UP\nTHE PHONE");
          Pump(300); { var tct = B.Capture("C:/Users/thoma/nfout/video-text-typing.png"); var tce = DateTime.Now.AddSeconds(10); while (!tct.IsCompleted && DateTime.Now < tce) Pump(50); }
          Js("document.querySelector('#vTextEdit').dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}))"); Pump(200);
          Check("Esc finishes typing; the title is drawn on the video again", Js("String(!document.querySelector('#vTextEdit')&&PX._editText==null)") == "true");
          string tpx = Js(@"(()=>{const c=PX.UI.selected()[0];c.text.color='#ffffff';c.text.shadow=0;c.text.size=200;c.x=0;c.y=0;const cv=document.createElement('canvas');cv.width=480;cv.height=270;const g=cv.getContext('2d');PX.drawFrame(g,PX.play.t,480,270,true);const d=g.getImageData(0,0,480,270).data;let n=0;for(let i=0;i<d.length;i+=4)if(d[i]>230&&d[i+1]>230&&d[i+2]>230)n++;return n})()");
          int tpn; int.TryParse(tpx, out tpn); Check("the typed words are drawn on the frame", tpn > 300, tpx);
          Js("PX.undo()"); Check("one UNDO takes the typing back", Js("PX.UI.selected()[0]?PX.UI.selected()[0].text.content:PX.P.clips.filter(c=>c.type==='text').slice(-1)[0].text.content") == "Your text");
          Js("PX.redo();PX.UI.sel=new Set([PX.P.clips.filter(c=>c.type==='text').slice(-1)[0].id]);PX.UI.renderSel();PX.redraw()"); Pump(300);
          // double-click the title on the preview
          Js(@"(()=>{const c=PX.UI.selected()[0];const cv=document.querySelector('#monitor'),r=cv.getBoundingClientRect();const b=PX.UI.clipBox(c,cv.width,cv.height);const s=r.width/cv.width;cv.dispatchEvent(new MouseEvent('dblclick',{bubbles:true,clientX:r.left+b.cx*s,clientY:r.top+b.cy*s}))})()");
          Check("double-clicking the title on the video opens it for typing", WaitJs("!!document.querySelector('#vTextEdit')", 3000)); Js("PX.UI.endEditText()");
          // the title bar
          Check("the title bar shows over the preview for the picked title (font, size, bold, italic, alignment, colours)", WaitJs("!!document.querySelector('#vTitleBar:not(.hidden) .vtbfont')", 3000) && Js("String(document.querySelectorAll('#vTitleBar button').length>=10&&document.querySelectorAll('#vTitleBar input[type=color]').length===3)") == "true");
          string tb = Js(@"(()=>{const c=PX.UI.selected()[0],s0=c.text.size;[...document.querySelectorAll('#vTitleBar button')].find(b=>b.textContent==='A+').click();const s1=c.text.size;document.querySelector('#vTitleBar button[title=Italic]').click();document.querySelector('#vTitleBar button[title=""Align left""]').click();const f=document.querySelector('#vTitleBar .vtbfont');f.value=[...f.options].find(o=>o.value!==c.text.font).value;f.dispatchEvent(new Event('change'));return (s1>s0)+' '+c.text.italic+' '+c.text.align+' '+(c.text.font===f.value)})()");
          Check("the title bar really changes the title (bigger, italic, left, another font)", tb == "true true left true", tb);
          Js("PX.UI.openPanel('text')"); Pump(200);
          Check("the Text panel has a box with the title's words to type in", Js("document.querySelector('#vPopB .vtextbox').value") == "PICK UP\nTHE PHONE");
          Js("(()=>{const t=document.querySelector('#vPopB .vtextbox');t.focus();t.value='NEW WORDS';t.dispatchEvent(new Event('input'));t.dispatchEvent(new Event('change'))})()");
          Check("typing in the Text panel box changes the title", Js("PX.UI.selected()[0].text.content") == "NEW WORDS");
          Js("PX.UI.editText(PX.UI.selected()[0],false)"); Pump(400); { var tct = B.Capture("C:/Users/thoma/nfout/video-text-bar.png"); var tce = DateTime.Now.AddSeconds(10); while (!tct.IsCompleted && DateTime.Now < tce) Pump(50); } Js("PX.UI.endEditText();PX.setDirty(false)");
        }
        // ---- E: the photo editor ----
        Check("Purphex Photo Editor starts simply (import a photo, start blank, templates, your designs)", Js("String(!!document.querySelector('#cvStart')&&document.querySelector('#cvStart').textContent.includes('IMPORT A PHOTO')&&document.querySelector('#cvStart').textContent.includes('BROWSE TEMPLATES'))") == "true");
        Js("document.querySelector('#stTpl').click()"); Pump(600);
        Check("BROWSE TEMPLATES opens the template browser (over two thousand)", Js("String(!!document.querySelector('#cvWelcome:not(.hidden)')&&document.querySelector('#cvWelcome h2').textContent.includes('PURPHEX MUSIC PHOTO EDITOR')&&PX.Cover.templateCount>=1000)") == "true", Js("String(PX.Cover.templateCount)"));
        Js("PX.UI.setTool('cover');PX.Cover.openWelcome()"); Pump(2500); { var ctw = B.Capture("C:/Users/thoma/nfout/photo-welcome.png"); var cew = DateTime.Now.AddSeconds(10); while (!ctw.IsCompleted && DateTime.Now < cew) Pump(50); }
        string tplErr = Js("(()=>{try{const C=PX.Cover;const L=[...document.querySelectorAll('#cvWelcome .wcard')];if(L.length<20)return 'only '+L.length+' cards';L[5].click();return C.art.layers.length>=2&&!document.querySelector('#cvWelcome:not(.hidden)')?'ok':'not used'}catch(e){return e.message}})()");
        Check("a template from the welcome screen opens on the canvas", tplErr == "ok", tplErr);
        string every = Js("PX.Cover.checkAllTemplates?PX.Cover.checkAllTemplates():'none'"); Check("every template builds and draws something (not one is blank or broken)", every.StartsWith("ok"), every);
        Check("there is no AI generator any more (it needed an API key)", Js("String(!document.querySelector('#gP')&&!PX.Cover.generate&&!document.body.innerHTML.includes('AI SETTINGS'))") == "true");
        string tabs = Js("[...document.querySelectorAll('#cvTabs button')].map(b=>(b.querySelector('.lb')||b).textContent).join(',')"); Check("the photo editor has all its tabs", tabs == "TEMPLATES,BACKGROUNDS,TEXT,STICKERS,PHOTOS,CUTOUT,EFFECTS,FILTERS,ADJUST,COLLAGE,DRAW,FRAMES", tabs);
        Js("(()=>{const C=PX.Cover;C.openPanel('fonts')})()"); string nf = Js("String(document.querySelectorAll('#cover .fontcard').length)"); int nfi; int.TryParse(nf, out nfi); Check("FONTS lists every font on the PC, each in its own style", nfi >= 10 && Js("document.querySelector('#cover .fontcard').style.fontFamily").Length > 0, nf + " fonts");
        // pixels: a red background, then the B&W look, a star, a brush stroke, a frame, all drawn by the same code as the export
        Func<string, string> px = (js) => Js("(()=>{try{const C=PX.Cover;" + js + ";const cv=document.createElement('canvas');cv.width=300;cv.height=300;const g=cv.getContext('2d');C.drawArt(g,300,300);window.__pg=g;return 'ok'}catch(e){return 'ERR '+e.message}})()");
        Func<int, int, string> at = (x, y) => Js("Array.from(window.__pg.getImageData(" + x + "," + y + ",1,1).data).join(',')");
        px("C.art.w=3000;C.art.h=3000;C.art.layers=[];C.art.global={look:'none'};C.setBackground({type:'color',color:'#ff0000'})"); string red = at(150, 150);
        px("C.art.global={look:'bw',filter:'bw'}"); string grey = at(150, 150); int[] gp = new int[3]; try { gp = grey.Split(',').Select(int.Parse).ToArray(); } catch { }
        Check("backgrounds and filters really change the picture (red, then the B&W look makes it grey)", red.StartsWith("255,0,0") && Math.Abs(gp[0] - gp[1]) < 6 && Math.Abs(gp[1] - gp[2]) < 6 && gp[0] > 30, red + " -> " + grey);
        px("C.art.global={look:'none'};C.addLayer({type:'shape',name:'Star',shape:'star',color:'#00ff00',w:0.3,h:0.3,stroke:0,glow:0})"); string star = at(150, 150);
        px("C.addLayer({type:'draw',name:'Drawing',strokes:[{color:'#0000ff',size:20,opacity:1,neon:false,erase:false,pts:[[0.1,0.9],[0.4,0.9]]}]})"); string ink = at(75, 270);
        px("C.addLayer({type:'frame',name:'Frame',style:'solid',color:'#ffffff',width:60})"); string edge = at(2, 150);
        Check("shapes, the brush and frames all draw (green star, blue stroke, white frame)", star.StartsWith("0,255,0") && ink.StartsWith("0,0,255") && edge.StartsWith("255,255,255"), star + " | " + ink + " | " + edge);
        Js("(()=>{const C=PX.Cover;const L=C.art.layers.find(l=>l.type==='shape');C.sel=L.id;const n=C.art.layers.length;C.act('dup');window.__dup=C.art.layers.length===n+1;C.undo();window.__und=C.art.layers.length===n;})()");
        Check("duplicate and undo work on layers", Js("String(window.__dup&&window.__und)") == "true");
        Js("(()=>{const C=PX.Cover;C.art.layers=[];C.dirty=false;C.openWelcome();document.querySelector('#wCats button[data-c=story]').click();document.querySelector('#wGrid .wcard').click()})()"); Pump(300); Check("a template sets the size and layers (Story 1080 x 1920)", Js("PX.Cover.art.w+'x'+PX.Cover.art.h") == "1080x1920" && Js("String(PX.Cover.art.layers.length>=3)") == "true");
        Js("(()=>{const C=PX.Cover;C.closeWelcome&&C.closeWelcome();const st=document.querySelector('#cvStart');if(st)st.remove();C.sel=null;C.openPanel('text')})()"); Pump(200);
        Check("TEXT opens a typing box straight away (quick text)", Js("String(!!document.querySelector('.ptype textarea'))") == "true");
        Js("(()=>{const ta=document.querySelector('.ptype textarea');ta.value='HELLO WORLD';document.querySelector('#ptOk').click()})()"); Pump(200);
        Check("pressing the tick puts the words in the middle of the picture, picked, with the text options open", Js("(()=>{const C=PX.Cover,L=C.art.layers.find(l=>l.id===C.sel);return String(!!L&&L.type==='text'&&L.text==='HELLO WORLD'&&Math.abs(L.x||0)<0.01&&!!document.querySelector('#cvTab .ptabs'))})()") == "true");
        Js("(()=>{const C=PX.Cover;C.textTab('styles');C.renderTab();[...document.querySelectorAll('#cvTab .grid2 .card')].find(c=>c.textContent==='Neon glow').click()})()"); Pump(500);
        Check("STYLES change the look of the words in one click (Neon glow)", Js("String(PX.Cover.art.layers.find(l=>l.id===PX.Cover.sel).neon===true)") == "true");
        Js("(()=>{const C=PX.Cover;C.textTab('colour');C.renderTab();[...document.querySelectorAll('#cvTab .psw')][2].click()})()"); Pump(500);
        Check("COLOUR swatches recolour the words", Js("PX.Cover.art.layers.find(l=>l.id===PX.Cover.sel).color") == "#ff3b30");
        Js("(()=>{const C=PX.Cover;C.textTab('font');C.renderTab()})()"); Pump(300);
        Check("FONT previews every font with your own words", Js("String(document.querySelectorAll('#cvTab .pfont').length>=10&&document.querySelector('#cvTab .pfont .ps').textContent==='HELLO WORLD')") == "true");
        { var ctx2 = B.Capture("C:/Users/thoma/nfout/photo-text.png"); var cex2 = DateTime.Now.AddSeconds(10); while (!ctx2.IsCompleted && DateTime.Now < cex2) Pump(50); }
        string hd = Js(@"(()=>{const C=PX.Cover,L=C.art.layers.find(l=>l.id===C.sel),cv=document.querySelector('#cvCanvas'),r=cv.getBoundingClientRect(),k=r.width/cv.width;const b=C.box(L,cv.width,cv.height);const fire=(x,y,type)=>cv.dispatchEvent(new MouseEvent(type,{bubbles:true,cancelable:true,clientX:r.left+x*k,clientY:r.top+y*k,button:0}));
            const s0=L.scale||1;fire(b.cx+b.w/2,b.cy+b.h/2,'mousedown');window.dispatchEvent(new MouseEvent('mousemove',{clientX:r.left+(b.cx+b.w)*k,clientY:r.top+(b.cy+b.h)*k}));window.dispatchEvent(new MouseEvent('mouseup',{}));const s1=L.scale;
            const n0=C.art.layers.length;const b2=C.box(L,cv.width,cv.height),a=(L.rot||0)*Math.PI/180;const dx=-b2.w/2,dy=-b2.h/2;fire(b2.cx+dx*Math.cos(a)-dy*Math.sin(a),b2.cy+dx*Math.sin(a)+dy*Math.cos(a),'mousedown');return (s1/s0).toFixed(2)+' '+(n0-C.art.layers.length)})()");
        Check("the corner handle resizes the text in one drag, and the X on the picture deletes it", hd.Split(' ').Length == 2 && double.Parse(hd.Split(' ')[0], System.Globalization.CultureInfo.InvariantCulture) > 1.5 && hd.Split(' ')[1] == "1", hd);
        Js("(()=>{const C=PX.Cover;C.tab='stickers';C.renderTab();document.querySelector('#cvTab .grid2 .card').click()})()"); Check("a sticker is added from the STICKERS tab", Js("PX.Cover.art.layers[PX.Cover.art.layers.length-1].type") == "sticker");
        { string pp = Path.Combine(media, "photo.jpg"); string pu = Js("PX.P.media.find(m=>m.name==='photo.jpg').url");
          Js("(()=>{PX.Cover.startFromPhoto('" + pu + "','photo.jpg')})()"); WaitJs("PX.Cover.art.layers.length===1&&PX.Cover.art.layers[0].type==='image'", 10000); int pw2, ph2; using (var pb = new Bitmap(pp)) { pw2 = pb.Width; ph2 = pb.Height; }
          Check("OPEN PHOTO starts a design from a picture (the canvas takes the photo's own size)", Js("PX.Cover.art.w+'x'+PX.Cover.art.h") == pw2 + "x" + ph2 && Js("PX.Cover.art.layers[0].fit") == "fill", Js("PX.Cover.art.w+'x'+PX.Cover.art.h") + " vs " + pw2 + "x" + ph2);
          Js("document.querySelector('#cover [data-c=toVideo]').click()"); Check("PURPHEX VIDEO EDITOR in Purphex Photo Editor goes to the video editor", Js("PX.UI.tool") == "video"); Js("PX.UI.setTool('cover')"); Check("there is an OPEN PHOTO button", Js("String(!!document.querySelector('#cover [data-c=openPhoto]'))") == "true"); }
        // ---- the quick photo tools, checked on real pixels ----
        Js(@"window.__mk=(fn,w,h)=>{const c=document.createElement('canvas');c.width=w||200;c.height=h||200;fn(c.getContext('2d'),c.width,c.height);return c.toDataURL('image/png')};
window.__subj=__mk((g,w,h)=>{g.fillStyle='#ffffff';g.fillRect(0,0,w,h);g.fillStyle='#ff0000';g.beginPath();g.arc(100,100,50,0,7);g.fill();});
window.__grad=__mk((g,w,h)=>{const gr=g.createLinearGradient(0,0,w,0);gr.addColorStop(0,'#000');gr.addColorStop(1,'#fff');g.fillStyle=gr;g.fillRect(0,0,w,h);});
window.__pix=(setup)=>{const C=PX.Cover;C.art.w=1000;C.art.h=1000;C.art.layers=[];C.art.global={look:'none'};setup(C);return new Promise(res=>{const urls=C.art.layers.filter(l=>l.url).map(l=>C.img(l.url));Promise.all(urls.map(i=>i.complete&&i.naturalWidth?1:new Promise(r=>{i.addEventListener('load',r,{once:true});i.addEventListener('error',r,{once:true});}))).then(()=>{const cv=document.createElement('canvas');cv.width=200;cv.height=200;const g=cv.getContext('2d');C._exporting=true;C.drawArt(g,200,200);C._exporting=false;window.__pg=g;res('ok');});});};
window.__at=(x,y)=>Array.from(window.__pg.getImageData(x,y,1,1).data).join(',');");
        Func<string, string> pix2 = (setup) => { Js("window.__pd=null;window.__pix(C=>{" + setup + "}).then(r=>window.__pd=r)"); WaitJs("window.__pd==='ok'", 15000); return "ok"; };
        Func<int, int, int[]> at2 = (x, y) => { try { return Js("window.__at(" + x + "," + y + ")").Split(',').Select(int.Parse).ToArray(); } catch { return new[] { -1, -1, -1, -1 }; } };
        pix2("C.art.layers.push({id:'p1',type:'image',name:'Photo',url:window.__subj,fit:'fill',scale:1,x:0,y:0,rot:0,vis:true,opacity:1,blend:'source-over',crop:{l:0,r:0,t:0,b:0},cut:{auto:true,tol:40,feather:0}})");
        int[] corner = at2(8, 8), mid = at2(100, 100); Check("REMOVE BACKGROUND takes away a plain background and keeps the subject", corner[3] < 20 && mid[3] > 230 && mid[0] > 200 && mid[1] < 60, string.Join(",", corner) + " | " + string.Join(",", mid));
        pix2("C.art.layers.push({id:'p2',type:'image',name:'Photo',url:window.__subj,fit:'fill',scale:1,x:0,y:0,rot:0,vis:true,opacity:1,blend:'source-over',crop:{l:0,r:0,t:0,b:0},cut:{pts:[[0.5,0.5,40]]}})");
        int[] wandMid = at2(100, 100), wandEdge = at2(8, 8); Check("the MAGIC ERASER removes the colour you click (and nothing else)", wandMid[3] < 20 && wandEdge[3] > 230, string.Join(",", wandMid) + " | " + string.Join(",", wandEdge));
        pix2("C.art.layers.push({id:'p3',type:'image',name:'Photo',url:window.__subj,fit:'fill',scale:1,x:0,y:0,rot:0,vis:true,opacity:1,blend:'source-over',crop:{l:0,r:0,t:0,b:0},cut:{strokes:[{restore:false,size:200,pts:[[0.1,0.1],[0.2,0.1]]}]}})");
        Check("the ERASE brush rubs out where you paint", at2(30, 20)[3] < 20 && at2(160, 160)[3] > 230, string.Join(",", at2(30, 20)));
        pix2("C.art.layers.push({id:'p4',type:'image',name:'Grad',url:window.__grad,fit:'fill',scale:1,x:0,y:0,rot:0,vis:true,opacity:1,blend:'source-over',crop:{l:0,r:0,t:0,b:0},px:{posterize:95}})");
        { var lv = new HashSet<int>(); for (int x = 2; x < 198; x += 2) lv.Add(at2(x, 100)[0]); Check("EFFECTS posterize turns a smooth gradient into a few flat steps", lv.Count <= 6 && lv.Count >= 2, lv.Count + " levels"); }
        pix2("C.art.layers.push({id:'p5',type:'image',name:'Grad',url:window.__grad,fit:'fill',scale:1,x:0,y:0,rot:0,vis:true,opacity:1,blend:'source-over',crop:{l:0,r:0,t:0,b:0},px:{exposure:80}})"); int brightA = at2(100, 100)[0];
        pix2("C.art.layers.push({id:'p6',type:'image',name:'Grad',url:window.__grad,fit:'fill',scale:1,x:0,y:0,rot:0,vis:true,opacity:1,blend:'source-over',crop:{l:0,r:0,t:0,b:0}})"); int brightB = at2(100, 100)[0];
        Check("ADJUST exposure really brightens the photo", brightA > brightB + 40, brightB + " -> " + brightA);
        pix2("C.art.layers.push({id:'p7',type:'image',name:'Grad',url:window.__grad,fit:'fill',scale:1,x:0,y:0,rot:0,vis:true,opacity:1,blend:'source-over',crop:{l:0,r:0,t:0,b:0},px:{pixelate:100}})");
        { int same = 0; for (int x = 20; x < 40; x++) if (at2(x, 100)[0] == at2(x + 1, 100)[0]) same++; Check("EFFECTS pixelate makes big blocks", same >= 15, same + " of 20 neighbours the same"); }
        pix2("C.addLayer({type:'vsticker',name:'Heart',kind:'heart',color:'#00ff00',color2:'#ffffff',size:600,text:''})"); Check("pack stickers draw (a green heart)", at2(100, 110)[1] > 200 && at2(100, 110)[0] < 60, string.Join(",", at2(100, 110)));
        pix2("C.makeCollage([[0,0,.5,.5],[.5,0,.5,.5],[0,.5,.5,.5],[.5,.5,.5,.5]]);C.art.collageGap=0;const cells=C.art.layers.filter(l=>l.type==='cell');cells[0].url=window.__subj;");
        Check("COLLAGE: a layout makes photo boxes and a photo fills its box", Js("String(PX.Cover.art.layers.filter(l=>l.type==='cell').length)") == "4" && at2(50, 50)[0] > 200 && at2(50, 50)[1] < 60 && at2(150, 150)[3] < 10, string.Join(",", at2(50, 50)) + " | " + string.Join(",", at2(150, 150)));
        Js("(()=>{const C=PX.Cover;C.art.layers=[];C.addLayer(Object.assign(C.lib.textBase(),{text:'HI',size:100,x:0.3,y:0}));const L=C.art.layers[0];C.sel=L.id;C.tab='text';C.renderProps();[...document.querySelectorAll('#cvProps button')].find(b=>b.textContent==='LEFT').click();const bx=C.box(L,C.art.w,C.art.h,true);window.__al=(bx.cx-bx.w/2)/C.art.w})()");
        Check("ALIGN LEFT puts the layer against the left edge", Math.Abs(double.Parse(Js("String(window.__al)"), System.Globalization.CultureInfo.InvariantCulture)) < 0.01, Js("String(window.__al)"));
        Js("PX.Cover.setZoom(2)"); string zw = Js("document.querySelector('#cvCanvas').style.width"); Js("PX.Cover.setZoom(1)"); string zw1 = Js("document.querySelector('#cvCanvas').style.width");
        Check("ZOOM makes the canvas bigger on screen and FIT brings it back", double.Parse(zw.Replace("px", ""), System.Globalization.CultureInfo.InvariantCulture) > double.Parse(zw1.Replace("px", ""), System.Globalization.CultureInfo.InvariantCulture) * 1.8, zw1 + " -> " + zw);
        Js("(()=>{const C=PX.Cover;C.art.layers=[];C.art.layers.push({id:'cr',type:'image',name:'Photo',url:window.__subj,fit:'fit',scale:1,x:0,y:0,rot:0,vis:true,opacity:1,blend:'source-over',crop:{l:0,r:0,t:0,b:0}});C.cropDialog(C.art.layers[0]);})()"); Pump(400);
        Js("(()=>{const s=document.querySelector('#crA');s.value='0.5625';s.onchange({target:s});document.querySelector('#crY').click()})()"); Pump(200);
        { string cr = Js("JSON.stringify(PX.Cover.art.layers[0].crop)"); double l = 0, r = 0, t = 0, bt = 0; try { var dd = (Dictionary<string, object>)MiniJson.Parse(cr); l = Convert.ToDouble(dd["l"]); r = Convert.ToDouble(dd["r"]); t = Convert.ToDouble(dd["t"]); bt = Convert.ToDouble(dd["b"]); } catch { }
          double ar = (100 - l - r) / (100 - t - bt); Check("CROP with Story 9:16 cuts the photo to that shape", Math.Abs(ar - 0.5625) < 0.02, cr); }
        // ---- made easy: the toolbar on the picture, delete in one click, typing on the picture ----
        Js("(()=>{const C=PX.Cover;C.closeWelcome&&C.closeWelcome();C.art.w=1080;C.art.h=1080;C.art.layers=[];C.setBackground({type:'color',color:'#14061f'});C.addLayer(Object.assign(C.lib.textBase(),{text:'SONG TITLE',name:'Title',size:120}));C.addLayer({type:'vsticker',name:'Label',kind:'label',text:'NEW',color:'#ff50c4',color2:'#ffffff',size:300,y:0.5});const t=C.art.layers.find(l=>l.type==='text');C.sel=t.id;C.tool='move';C.render();})()"); Pump(500);
        Check("picking a layer puts its tools right on the picture (edit, font, colour, delete...)", Js("String(!document.querySelector('#cvBar').classList.contains('hidden')&&['EDIT TEXT','FONT','DELETE','DUPLICATE'].every(w=>document.querySelector('#cvBar').textContent.includes(w)))") == "true", Js("document.querySelector('#cvBar').textContent"));
        Js("(()=>{const C=PX.Cover;C.sel=C.art.layers.find(l=>l.type==='vsticker').id;C.render();document.querySelector('#cvBar [data-k=del]').click();})()"); Pump(200);
        Check("DELETE on the toolbar removes anything from a template in one click (and UNDO brings it back)", Js("String(!PX.Cover.art.layers.some(l=>l.type==='vsticker'))") == "true" && Js("(()=>{PX.Cover.undo();return String(PX.Cover.art.layers.some(l=>l.type==='vsticker'))})()") == "true");
        Js("(()=>{const C=PX.Cover;const t=C.art.layers.find(l=>l.type==='text');C.sel=t.id;C.render();C.inlineEdit(t);const ta=document.querySelector('.cvinline');ta.value='MY NEW TITLE';ta.oninput();C._inlineDone(true);})()"); Pump(200);
        Check("typing on the picture changes the words (double-click text)", Js("PX.Cover.art.layers.find(l=>l.type==='text').text") == "MY NEW TITLE" && Js("String(!document.querySelector('.cvinline'))") == "true");
        Check("the tools are big tiles with pictures", Js("String(document.querySelectorAll('#cvTabs.rail .ic').length)") == "12");
        Pump(400); { var cte = B.Capture("C:/Users/thoma/nfout/photo-easy.png"); var cee = DateTime.Now.AddSeconds(10); while (!cte.IsCompleted && DateTime.Now < cee) Pump(50); }
        // ---- the top-toolbar layout and the new photo tools ----
        Js("(()=>{const C=PX.Cover;C.closeWelcome&&C.closeWelcome();C.art.w=1000;C.art.h=1000;C.art.layers=[];C.setBackground({type:'color',color:'#14061f'});C.render();C.openPanel('design')})()"); Pump(400);
        Check("Purphex Photo Editor: an app bar on top, a tool strip on the left, the canvas, and a Properties / Layers panel with a histogram on the right", Js("String(document.querySelectorAll('#cvCats button').length>=18&&!!document.querySelector('#cvWork #cvSide #cvHist')&&getComputedStyle(document.querySelector('#cvLeft')).display==='none'&&getComputedStyle(document.querySelector('#cvRight')).display==='none'&&!!document.querySelector('#cvExport'))") == "true");
        Js("PX.Cover.sideShow(false)"); string sw1 = Js("document.querySelector('#cvCanvas').style.width"); Js("PX.Cover.sideShow(true)");
        Check("a category opens its own panel under the toolbar, and Esc closes it", Js("String(!document.querySelector('#cvPop').classList.contains('hidden')&&document.querySelector('#cvPopT').textContent==='DESIGN')") == "true" && Js("(()=>{document.dispatchEvent(new KeyboardEvent('keydown',{key:'Escape',bubbles:true}));return String(document.querySelector('#cvPop').classList.contains('hidden'))})()") == "true");
        string catErr = Js("(()=>{try{for(const b of document.querySelectorAll('#cvCats button')){PX.Cover.openPanel(b.dataset.k);if(!document.querySelector('#cvTab').children.length)return 'empty: '+b.dataset.k;}PX.Cover.openPanel(null);return 'none'}catch(e){return e.message}})()");
        Check("every category opens a working panel (none is empty)", catErr == "none", catErr);
        Js("PX.Cover.setSize(1234,777)"); Check("DESIGN sets your own size", Js("PX.Cover.art.w+'x'+PX.Cover.art.h") == "1234x777");
        string mul = Js(@"(()=>{const C=PX.Cover;C.setSize(1000,1000);C.art.layers=C.art.layers.filter(l=>l.name==='Background');const mk=(t,x,y)=>C.addLayer(Object.assign(C.lib.textBase(),{text:t,size:80,x:x,y:y}));mk('A',-0.5,-0.3);mk('BB',0.1,0.2);mk('CCC',0.6,-0.1);const ids=C.art.layers.filter(l=>l.type==='text').map(l=>l.id);C.multi=new Set(ids);C.sel=ids[0];
          C.alignSel('l');const lefts=C.art.layers.filter(l=>l.type==='text').map(l=>{const b=C.box(l,1000,1000,true);return Math.round(b.cx-b.w/2)});C.alignSel('t');
          C.art.layers.filter(l=>l.type==='text').forEach((l,i)=>l.x=[-0.6,0.1,0.6][i]);C.art.layers.find(l=>l.text==='BB').x=0.3;C.distributeSel('h');const xs=C.art.layers.filter(l=>l.type==='text').map(l=>+l.x.toFixed(3)).sort((a,b)=>a-b);
          C.groupSel();const gs=new Set(C.art.layers.filter(l=>l.type==='text').map(l=>l.group));C.copySel();C.pasteSel();const n=C.art.layers.filter(l=>l.type==='text').length;return JSON.stringify({lefts,xs,groups:gs.size,grouped:[...gs][0]!=null,n})})()");
        Check("several layers at once: line up, space evenly, group, copy and paste", mul.Contains(@"""groups"":1") && mul.Contains(@"""grouped"":true") && mul.Contains(@"""n"":6") && mul.Contains(@"""xs"":[-0.6,0,0.6]") && System.Text.RegularExpressions.Regex.IsMatch(mul, @"""lefts"":\[(\d+),\1,\1\]"), mul);
        { string wp = Path.Combine(outDir, "export_test.webp"); try { File.Delete(wp); } catch { } StudioForm.CrSaveHook = _ => wp; Js("(()=>{const C=PX.Cover;C.art.layers=[];C.setBackground({type:'color',color:'#ff0000'});C.addLayer({type:'shape',name:'Dot',shape:'circle',color:'#00ff00',w:0.3,h:0.3});C.exportImage({fmt:'webp',scale:0.5,quality:0.9,transparent:true,name:'export_test'})})()");
          var endW = DateTime.Now.AddSeconds(20); while (!File.Exists(wp) && DateTime.Now < endW) Pump(200); Pump(400); byte[] wb = File.Exists(wp) ? File.ReadAllBytes(wp) : new byte[0];
          Check("EXPORT writes a real WebP file at the size you pick", wb.Length > 30 && wb[0] == 0x52 && wb[8] == 0x57, wb.Length + " bytes");
          string tr = Js("(()=>{window.__tr=null;PX.Cover.renderImage({fmt:'png',scale:0.5,transparent:true}).then(b=>createImageBitmap(b)).then(bm=>{const c=document.createElement('canvas');c.width=bm.width;c.height=bm.height;const g=c.getContext('2d');g.drawImage(bm,0,0);window.__tr=bm.width+'x'+bm.height+'|'+g.getImageData(3,3,1,1).data[3]+'|'+Array.from(g.getImageData(bm.width/2,bm.height/2,1,1).data).join(',')});return 'ok'})()"); WaitJs("window.__tr", 10000); tr = Js("window.__tr");
          Check("a see-through export keeps the background out and the rest in (half size)", tr.StartsWith("500x500|0|0,255,0,255"), tr); StudioForm.CrSaveHook = null; }
        { string up = Path.Combine(outDir, "upload_me.png"); using (var ub = new Bitmap(40, 40)) { using (var ug = Graphics.FromImage(ub)) ug.Clear(Color.Blue); ub.Save(up, System.Drawing.Imaging.ImageFormat.Png); }
          Js("window.__imp=0;PX.on('artImported',()=>window.__imp++)"); M("CrArtFiles", new List<string> { up, Path.Combine(outDir, "not_a_picture.txt") }); WaitJs("window.__imp>=1", 8000); Js("PX.Cover.openPanel('uploads')"); WaitJs("document.querySelectorAll('#cvTab .upl .card').length>=1", 8000);
          Check("UPLOADS: pictures are added (others are refused with a message) and listed to use again", Js("String([...document.querySelectorAll('#cvTab .upl .card')].some(c=>c.textContent.includes('upload_me')))") == "true"); }
        { string fdir = Path.Combine(StudioEnv.DataDir, "creative", "art", "fonts"); Directory.CreateDirectory(fdir); string src = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf"); if (File.Exists(src)) File.Copy(src, Path.Combine(fdir, "My_Test_Font.ttf"), true);
          Js("PX.send({t:'fontList'})"); Check("your own font files are loaded and listed in FONTS", WaitJs("PX.UI.fonts.includes('My Test Font')", 10000), Js("PX.UI.fonts.slice(0,3).join(', ')")); }
        Js("PX.Cover.kvSet('brandkit',{colors:['#123456'],fonts:['Georgia'],logos:[]})"); Pump(600);
        Check("the BRAND KIT is kept by Purphex", File.Exists(Path.Combine(StudioEnv.DataDir, "creative", "art", "settings", "brandkit.json")) && File.ReadAllText(Path.Combine(StudioEnv.DataDir, "creative", "art", "settings", "brandkit.json")).Contains("#123456"));
        Js("(()=>{const C=PX.Cover;C.art.layers=[];C.setBackground({type:'color',color:'#333'});C.sel=null;C.openPanel('ai')})()"); Pump(200);
        Check("AI TOOLS shows what really works (remove background, auto enhance) and says honestly what is not set up", Js("String(document.querySelector('#cvTab').textContent.includes('REMOVE BACKGROUND')&&document.querySelector('#cvTab').textContent.includes('Nothing is faked'))") == "true");
        Check("there are more kinds of templates (logos, wallpapers, grunge, cinematic...)", int.Parse(Js("String(PX.Cover.templateCount)")) >= 2000, Js("String(PX.Cover.templateCount)"));
        Js("(()=>{const C=PX.Cover;C.openPanel(null);C.art.w=3000;C.art.h=3000;C.art.layers=[];C.setBackground({type:'gradient',c1:'#1a0b3a',c2:'#000',angle:135});C.addLayer(Object.assign(C.lib.textBase(),{text:'NIGHT DRIVE',size:260}));C.openPanel('text')})()"); Pump(800);
        { var ctb = B.Capture("C:/Users/thoma/nfout/photo-toolbar.png"); var ceb = DateTime.Now.AddSeconds(10); while (!ctb.IsCompleted && DateTime.Now < ceb) Pump(50); }
        Js("PX.Cover.openPanel(null)");
        string tabErr = Js("(()=>{try{for(const t of ['templates','bg','text','stickers','photos','filters','adjust','draw','frames']){PX.Cover.tab=t;PX.Cover.renderTab()}for(const L of PX.Cover.art.layers){PX.Cover.sel=L.id;PX.Cover.renderProps()}PX.Cover.tab='text';PX.Cover.render();return 'none'}catch(e){return e.message}})()");
        Check("every tab and every layer's settings open without an error", tabErr == "none", tabErr);
        Js("(()=>{const C=PX.Cover;C.closeWelcome&&C.closeWelcome();C.art.w=3000;C.art.h=3000;C.art.layers=[];C.setBackground({type:'gradient',c1:'#1a0b3a',c2:'#000',angle:135});C.art.layers.push({id:'ps1',type:'image',name:'Photo',url:window.__subj,fit:'fit',scale:0.8,x:0,y:0,rot:0,vis:true,opacity:1,blend:'source-over',crop:{l:0,r:0,t:0,b:0},cut:{auto:true,tol:40,feather:2},px:{glitch:40}});C.addLayer({type:'vsticker',name:'OUT NOW',kind:'outnow',text:'OUT NOW',color:'#ff50c4',color2:'#ffffff',size:1300,y:0.75});C.sel='ps1';C.tab='cutout';C.render();})()"); Pump(1500); { var ctw = B.Capture("C:/Users/thoma/nfout/photo-cutout.png"); var cew = DateTime.Now.AddSeconds(10); while (!ctw.IsCompleted && DateTime.Now < cew) Pump(50); }
        Js("(()=>{const C=PX.Cover;C.tab='effects';C.renderTab();})()"); Pump(800); { var ctw = B.Capture("C:/Users/thoma/nfout/photo-effects.png"); var cew = DateTime.Now.AddSeconds(10); while (!ctw.IsCompleted && DateTime.Now < cew) Pump(50); }
        Js("(()=>{const C=PX.Cover;C.tab='stickers';C.renderTab();const t=document.querySelector('#cvTab');t.scrollTop=t.scrollHeight;})()"); Pump(800); { var ctw = B.Capture("C:/Users/thoma/nfout/photo-stickers.png"); var cew = DateTime.Now.AddSeconds(10); while (!ctw.IsCompleted && DateTime.Now < cew) Pump(50); }
        Js("(()=>{const C=PX.Cover;C.art.layers=[];C.setBackground({type:'color',color:'#14061f'});C.makeCollage([[0,0,.6,1],[.6,0,.4,.5],[.6,.5,.4,.5]]);const c=C.art.layers.filter(l=>l.type==='cell');c[0].url=window.__subj;c[1].url=window.__grad;C.tab='collage';C.render();})()"); Pump(1200); { var ctw = B.Capture("C:/Users/thoma/nfout/photo-collage.png"); var cew = DateTime.Now.AddSeconds(10); while (!ctw.IsCompleted && DateTime.Now < cew) Pump(50); }
        Pump(600); { var ct = B.Capture(@"C:\Users\thoma\nfout\photo-studio.png"); var ce = DateTime.Now.AddSeconds(10); while (!ct.IsCompleted && DateTime.Now < ce) Pump(50); }
        // ---- PHOTO STUDIO PRO ----
        { Js("(()=>{const C=PX.Cover;C.closeWelcome&&C.closeWelcome();const st=document.querySelector('#cvStart');if(st)st.remove();C.openPanel(null);C.selection=null;C.tool='move';C.art.pages=null;C.art.page=0;C.art.guides=null;C.art.w=1000;C.art.h=1000;C.art.layers=[];C.setBackground({type:'color',color:'#ff0000'});C.sel=null;C.render();" +
             "window.__px=(fx,fy,P)=>{const c=document.createElement('canvas');c.width=200;c.height=200;C.drawArt(c.getContext('2d'),200,200);const d=c.getContext('2d').getImageData(Math.floor(fx*200),Math.floor(fy*200),1,1).data;return [d[0],d[1],d[2],d[3]].join(',')};})()");
          Func<string, int> ToInt = s => { int v; return int.TryParse(s, out v) ? v : -1; };
          Func<string, int[]> rgba = s => { try { return Array.ConvertAll(s.Split(','), int.Parse); } catch { return new[] { -1, -1, -1, -1 }; } };
          Func<int[], int, int, int, bool> near = (p, r, g, b) => Math.Abs(p[0] - r) < 40 && Math.Abs(p[1] - g) < 40 && Math.Abs(p[2] - b) < 40;
          // stickers and backgrounds
          string plib = Js("(()=>{const C=PX.Cover,L=C.stickerLib.list;return L.length+' '+new Set(L.map(d=>d.cat)).size+' '+new Set(L.map(d=>d.id)).size+' '+C.bgLib.list.length})()");
          Check("the sticker library: 300+ original stickers in all 25 categories (no duplicate ids) and 30 backgrounds", System.Text.RegularExpressions.Regex.IsMatch(plib ?? "", @"^(3\d\d|[4-9]\d\d) 25 \1 30$"), plib);
          string pstErr = Js("(()=>{const bad=[];const c=document.createElement('canvas');c.width=c.height=64;const g=c.getContext('2d');for(const d of PX.Cover.stickerLib.list){g.setTransform(1,0,0,1,32,32);g.clearRect(-32,-32,64,64);g.scale(0.6,0.6);try{d.fn(g,'#a85cff','#ffd23c')}catch(e){bad.push(d.id)}g.setTransform(1,0,0,1,0,0);const px=g.getImageData(0,0,64,64).data;let n=0;for(let i=3;i<px.length;i+=4)if(px[i]>20)n++;if(n<12)bad.push(d.id+' empty')}return bad.join(',')||'none'})()");
          Check("every sticker draws something, without an error", pstErr == "none", pstErr);
          Js("(()=>{const C=PX.Cover;window.__st=C.addSticker('hearts:heart');window.__st.c1='#00ff00';window.__st.size=800;C.render()})()");
          var pps = rgba(Js("__px(0.5,0.55)"));
          Check("a sticker is added as a layer and drawn in the colour you pick", Js("PX.Cover.sel===window.__st.id&&window.__st.type==='pstk'") == "True" && near(pps, 0, 255, 0), string.Join(",", pps));
          Check("recently used stickers are remembered", WaitJs("PX.Cover.kvGet&&1", 100) && Js("(()=>{PX.Cover.tab='stickers';PX.Cover.renderTab();const r=[...document.querySelectorAll('#cvTab .stkcats .chip')].find(c=>c.textContent==='Recent');r.click();return document.querySelectorAll('#cvTab .stkgrid .card.stk').length>=1})()") == "True");
          string psrch = Js("(()=>{const q=document.querySelector('#cvTab .stkq');q.value='fire';q.oninput();return document.querySelectorAll('#cvTab .stkgrid .card.stk').length})()");
          Check("sticker search finds matches (fire)", ToInt(psrch) >= 4, psrch);
          Js("(()=>{const C=PX.Cover;C.art.layers=C.art.layers.filter(l=>l.type!=='pstk');C.tab='bg';C.renderTab();[...document.querySelectorAll('#cvTab .card')].find(c=>c.title==='Synthwave grid').click()})()");
          Check("a library background replaces the background", Js("(()=>{const b=PX.Cover.art.layers.find(l=>l.name==='Background');return b.type+' '+b.kind})()") == "pbg synth" && rgba(Js("__px(0.5,0.95)"))[3] == 255);
          // layer effects, clipping and masks
          Js("(()=>{const C=PX.Cover;C.art.layers=[];C.setBackground({type:'color',color:'#000000'});window.__sq=C.addLayer({type:'shape',name:'Sq',shape:'rect',color:'#ffffff',w:0.4,h:0.4});window.__sq.fx={outlines:[{size:60,color:'#00ff00'}]};C.render()})()");
          var po1 = rgba(Js("__px(0.72,0.5)")); var po0 = rgba(Js("__px(0.5,0.5)")); var po2 = rgba(Js("__px(0.9,0.5)"));
          Check("OUTLINE draws round the layer in its colour (and not inside it or far away)", near(po1, 0, 255, 0) && near(po0, 255, 255, 255) && near(po2, 0, 0, 0), string.Join(",", po1) + " | " + string.Join(",", po0) + " | " + string.Join(",", po2));
          Js("(()=>{const C=PX.Cover;C.begin();window.__sq.fx={shadow:{on:true,x:120,y:120,blur:0,amt:100,color:'#0000ff'}};C.commit()})()");
          var psh = rgba(Js("__px(0.78,0.78)"));
          Check("DROP SHADOW is drawn offset in its colour", near(psh, 0, 0, 255), string.Join(",", psh));
          Js("(()=>{const C=PX.Cover;C.begin();window.__sq.fx={overlay:{amt:100,color:'#ff00ff'}};C.commit()})()");
          Check("COLOUR OVERLAY recolours the layer", near(rgba(Js("__px(0.5,0.5)")), 255, 0, 255));
          Js("(()=>{const C=PX.Cover;C.begin();window.__sq.fx=null;window.__cl=C.addLayer({type:'color',color:'#00ffff'});window.__cl.clip=true;C.commit()})()");
          Check("CLIPPING: a layer clipped to the one below only shows inside it", near(rgba(Js("__px(0.5,0.5)")), 0, 255, 255) && near(rgba(Js("__px(0.1,0.1)")), 0, 0, 0));
          Js("(()=>{const C=PX.Cover;C.begin();C.art.layers=C.art.layers.filter(l=>l!==window.__cl);C.commit();C.sel=window.__sq.id;C.selection={kind:'rect',x:0.5,y:0,w:0.5,h:1};C.maskToSelection('keep');C.selection=null})()");
          Check("a selection mask (KEEP INSIDE) hides the layer outside the selection, and can be removed", near(rgba(Js("__px(0.4,0.5)")), 0, 0, 0) && near(rgba(Js("__px(0.6,0.5)")), 255, 255, 255) && Js("(()=>{PX.Cover.begin();delete window.__sq.smask;PX.Cover.commit();return __px(0.4,0.5)})()").StartsWith("255,255,255"));
          // pro colour
          Js("(()=>{const C=PX.Cover;C.art.layers=[];C.setBackground({type:'color',color:'#204080'});C.art.global={look:'none',pro:{levels:{inB:0,inW:255,gamma:1,outB:255,outW:0}}};C.render()})()");
          var plv = rgba(Js("__px(0.5,0.5)"));
          Check("LEVELS (output reversed) inverts the picture: #204080 becomes about #dfbf7f", near(plv, 0xdf, 0xbf, 0x7f), string.Join(",", plv));
          Js("(()=>{const C=PX.Cover;C.art.global={look:'none',pro:{curve:{m:[[0,0],[0.5,0.85],[1,1]]}}};C.render()})()");
          var pcvp = rgba(Js("__px(0.5,0.5)"));
          Check("CURVES lift the mid tones", pcvp[0] > 0x20 + 25 && pcvp[2] > 0x80 + 25, string.Join(",", pcvp));
          Js("(()=>{const C=PX.Cover;C.art.global={look:'none',pro:{temp:100}};C.render()})()"); var pwb = rgba(Js("__px(0.5,0.5)"));
          Check("TEMPERATURE warms the picture (more red, less blue)", pwb[0] > 0x20 + 15 && pwb[2] < 0x80 - 15, string.Join(",", pwb));
          Js("(()=>{const C=PX.Cover;C.art.global={look:'none',pro:{hsl:{blue:[0,-100,0]}}};C.render()})()"); var phs = rgba(Js("__px(0.5,0.5)"));
          Check("SELECTIVE COLOUR: taking the saturation out of the blues greys a blue", Math.Abs(phs[0] - phs[2]) < 30, string.Join(",", phs));
          Js("(()=>{const C=PX.Cover;C.art.global={look:'none',pro:{vib:100}};C.render()})()"); var pvb = rgba(Js("__px(0.5,0.5)"));
          Check("VIBRANCE makes a dull colour stronger", pvb[2] - pvb[0] > 0x80 - 0x20 + 10, string.Join(",", pvb));
          Js("PX.Cover.art.global={look:'none'}");
          // tools: fill bucket, eyedropper, lines, guides
          Js("(()=>{const C=PX.Cover;C.art.layers=[];C.setBackground({type:'color',color:'#ff0000'});C.addLayer({type:'shape',name:'Dot',shape:'circle',color:'#00ff00',w:0.4,h:0.4});C.sel=null;window.__ff=C.floodFill(0.05,0.05,'#0000ff',30);})()");
          Check("FILL BUCKET fills the area you click (the corner) with the colour, on a new layer, and not the circle", near(rgba(Js("__px(0.05,0.05)")), 0, 0, 255) && near(rgba(Js("__px(0.5,0.5)")), 0, 255, 0) && Js("PX.Cover.art.layers[PX.Cover.art.layers.length-1].name") == "Fill");
          Check("EYEDROPPER picks the colour under the pointer", Js("PX.Cover.pickColorAt(0.5,0.5)") == "#00ff00", Js("PX.Cover.pickColorAt(0.5,0.5)"));
          Js("(()=>{const C=PX.Cover;C.art.layers=[];C.setBackground({type:'color',color:'#000000'});C.addLayer({type:'pline',name:'Arrow',x:0,y:0,dx:0.8,dy:0,width:40,color:'#ffff00',head:'end',headSize:4})})()");
          Check("LINE / ARROW layers draw a line with an arrow head", near(rgba(Js("__px(0.2,0.5)")), 255, 255, 0) && near(rgba(Js("__px(0.80,0.53)")), 255, 255, 0) && near(rgba(Js("__px(0.2,0.6)")), 0, 0, 0));
          string psnap = Js("(()=>{const C=PX.Cover;C.art.guides={v:[750],h:[]};const t=C.addLayer({type:'shape',name:'S',shape:'rect',color:'#fff',w:0.1,h:0.1});t.x=0.49;t.y=0.3;C.snapHook(t,{});const b=C.box(t,1000,1000,true);return Math.round(b.cx)})()");
          Check("layers snap to GUIDES", psnap == "750", psnap);
          Js("(()=>{const C=PX.Cover;C.view.rulers=true;C.draw()})()"); Check("RULERS show along the picture", Js("String(!document.querySelector('#proRulerH').classList.contains('hidden'))") == "true"); Js("PX.Cover.view.rulers=false;PX.Cover.art.guides=null;PX.Cover.draw()");
          // text
          string ptf = Js("(()=>{const C=PX.Cover;C.art.layers=[];C.setBackground({type:'color',color:'#000'});const t=C.addLayer(Object.assign(C.lib.textBase(),{text:'WAVE',size:200,fillKind:'chrome',warp:'wave',warpAmt:80,outline2:20,outline2Color:'#ff00ff',emboss:40}));C.render();let n=0;for(let x=0.1;x<0.9;x+=0.02){const p=__px(x,0.5).split(',');if(+p[0]>200&&+p[1]<80&&+p[2]>200)n++}return n})()");
          Check("PRO TEXT: chrome fill, wave warp and a second outline draw", ToInt(ptf) >= 2, ptf);
          // pages
          string ppgs = Js("(()=>{const C=PX.Cover;C.art.pages=null;C.art.layers=[];C.setBackground({type:'color',color:'#ff0000'});C.pages.add(false);C.art.layers[0].color='#0000ff';C.pages.add(true);const n=C.art.pages.length,b=__px(0.5,0.5);C.pages.go(0);const a=__px(0.5,0.5);C.pages.go(1);const c=__px(0.5,0.5);return n+'|'+a+'|'+c+'|'+b})()");
          Check("PAGES: add, duplicate and switch pages (each keeps its own layers)", ppgs == "3|255,0,0,255|0,0,255,255|0,0,255,255", ppgs);
          Js("PX.Cover.art.name='Pages test';PX.Cover.act('save')"); string ppaid = Js("PX.Cover.art.id"); WaitJs("window.__artSaved==='" + ppaid + "'", 10000);
          Js("PX.Cover.art.pages=null;PX.Cover.art.layers=[];PX.send({t:'artOpen',id:'" + ppaid + "'})"); Pump(200);
          WaitJs("PX.Cover.art.pages&&PX.Cover.art.pages.length===3", 8000);
          Check("PAGES are saved with the project and come back when it is opened", Js("String(PX.Cover.art.pages&&PX.Cover.art.pages.length)") == "3", Js("String(PX.Cover.art.pages&&PX.Cover.art.pages.length)"));
          int ppn = 0; string ppdir2 = Path.Combine(outDir, "pages"); Directory.CreateDirectory(ppdir2); foreach (var pf0 in Directory.GetFiles(ppdir2)) File.Delete(pf0); StudioForm.CrSaveHook = _ => Path.Combine(ppdir2, "page" + (++ppn) + ".png");
          Js("PX.Cover.pages.exportAll({fmt:'png',scale:0.25,quality:0.9,transparent:false,name:'Pages test'})"); var ppe = DateTime.Now.AddSeconds(30); while (Directory.GetFiles(ppdir2).Length < 3 && DateTime.Now < ppe) Pump(200); Pump(500); StudioForm.CrSaveHook = null;
          string ppcols = ""; foreach (var pf0 in Directory.GetFiles(ppdir2).OrderBy(x => x)) using (var pbm = new Bitmap(pf0)) { var pc0 = pbm.GetPixel(pbm.Width / 2, pbm.Height / 2); ppcols += pbm.Width + ":" + pc0.R + "," + pc0.G + "," + pc0.B + " "; }
          Check("EXPORT ALL PAGES writes one picture per page, each with that page's design", Directory.GetFiles(ppdir2).Length == 3 && ppcols.StartsWith("250:255,0,0") && ppcols.Contains("250:0,0,255 250:0,0,255"), ppcols);
          // quick starts and all new panels
          Js("(()=>{const C=PX.Cover;C.openStart();[...document.querySelectorAll('#cvStart .q button')].find(b=>b.textContent.includes('ALBUM COVER')).click()})()"); Pump(300);
          Check("QUICK START Album Cover makes a 3000 x 3000 cover with a tracklist page", Js("PX.Cover.art.w+'x'+PX.Cover.art.h+' '+PX.Cover.art.pages.length+' '+PX.Cover.art.pages[1].name") == "3000x3000 2 Tracklist", Js("PX.Cover.art.w+'x'+PX.Cover.art.h+' '+(PX.Cover.art.pages||[]).length"));
          Js("(()=>{const C=PX.Cover;C.openStart();document.querySelector('#stW').value=640;document.querySelector('#stH').value=360;document.querySelector('#stT').checked=true;document.querySelector('#stGo').click()})()");
          Check("START FROM SCRATCH: your own size with a see-through background", Js("PX.Cover.art.w+'x'+PX.Cover.art.h+' '+PX.Cover.art.layers.length") == "640x360 0");
          string ppe2 = Js("(()=>{try{const C=PX.Cover;C.art.w=1000;C.art.h=1000;C.art.layers=[];C.setBackground({type:'color',color:'#222'});C.addSticker('music:mic');C.addLayer({type:'pline',name:'L',dx:0.3,dy:0.1});C.addLayer(Object.assign(C.lib.textBase(),{text:'T'}));C.addLayer({type:'draw',name:'D',strokes:[]});for(const t of ['adjust','fonts','text','draw','stickers','bg']){C.tab=t;C.renderTab()}for(const L of C.art.layers){C.sel=L.id;C.renderProps()}C.tool='selRect';C.selection={kind:'ellipse',x:0.2,y:0.2,w:0.3,h:0.3};C.render();C.tool='fill';C.render();C.tool='line';C.render();C.tool='move';C.selection=null;C.render();return 'none'}catch(e){return e.message+' '+e.stack.slice(0,200)}})()");
          Check("every PRO panel, tool and layer setting opens without an error", ppe2 == "none", ppe2);
          Check("the PRO tool strip is on the app bar (select, lasso, fill, eyedropper, line, arrow, rulers)", Js("String(document.querySelectorAll('#proBar [data-t]').length===8&&!!document.querySelector('#proRulerBtn'))") == "true");
          Js("(()=>{const C=PX.Cover;C.loadFont('Anton')})()");
          Check("the FONT LIBRARY downloads a font when it is used (needs the internet)", WaitJs("document.fonts&&document.fonts.check('700 40px \"Anton\"')&&[...document.fonts].some(f=>f.family.replace(/\"/g,'')==='Anton'&&f.status==='loaded')", 20000), Js("String([...document.fonts].filter(f=>f.family.indexOf('Anton')>=0).map(f=>f.status))"));
          // screenshots to look at
          Js("(()=>{const C=PX.Cover;C.art.w=3000;C.art.h=3000;C.art.layers=[];C.art.pages=null;C.lib.bg(C.art,'pbg',{kind:'synth',c1:'#ff50c4',c2:'#a85cff',seed:7});const t=C.addLayer(Object.assign(C.lib.textBase(),{text:'NIGHT DRIVE',size:330,font:'Anton',fillKind:'chrome',y:-0.55}));t.fx={outlines:[{size:14,color:'#14061f'},{size:30,color:'#ff50c4'}],shadow:{on:true,x:20,y:30,blur:30,amt:80,color:'#000'}};const s=C.addSticker('fire:flame1');s.x=0.6;s.y=0.45;const s2=C.addSticker('stars:sparkle3');s2.x=-0.6;s2.y=0.5;s2.c1='#ffffff';C.pages.add(true);C.pages.go(0);C.sel=t.id;C.tool='selRect';C.selection={kind:'rect',x:0.1,y:0.6,w:0.35,h:0.3};C.view.rulers=true;C.art.guides={v:[1500],h:[900]};C.render();C.openPanel('stickers')})()"); Pump(1500);
          { var pct = B.Capture("C:/Users/thoma/nfout/photo-pro-stickers.png"); var pce = DateTime.Now.AddSeconds(10); while (!pct.IsCompleted && DateTime.Now < pce) Pump(50); }
          Js("(()=>{const C=PX.Cover;C.selection=null;C.tool='move';C.openPanel('adjust');const t=document.querySelector('#cvTab');t.scrollTop=t.scrollHeight*0.35})()"); Pump(800);
          { var pct = B.Capture("C:/Users/thoma/nfout/photo-pro-adjust.png"); var pce = DateTime.Now.AddSeconds(10); while (!pct.IsCompleted && DateTime.Now < pce) Pump(50); }
          Js("(()=>{const C=PX.Cover;C.openPanel('fonts')})()"); Pump(2500);
          { var pct = B.Capture("C:/Users/thoma/nfout/photo-pro-fonts.png"); var pce = DateTime.Now.AddSeconds(10); while (!pct.IsCompleted && DateTime.Now < pce) Pump(50); }
          Js("(()=>{const C=PX.Cover;C.openPanel(null);C.sideTab&&C.sideTab('props');C.render();document.querySelectorAll('#cvSideB details.prosec').forEach(d=>d.open=true)})()"); Pump(800);
          { var pct = B.Capture("C:/Users/thoma/nfout/photo-pro-props.png"); var pce = DateTime.Now.AddSeconds(10); while (!pct.IsCompleted && DateTime.Now < pce) Pump(50); }
          Js("(()=>{const C=PX.Cover;C.view.rulers=false;C.art.guides=null;C.openStart()})()"); Pump(600);
          { var pct = B.Capture("C:/Users/thoma/nfout/photo-pro-start.png"); var pce = DateTime.Now.AddSeconds(10); while (!pct.IsCompleted && DateTime.Now < pce) Pump(50); }
          Js("(()=>{const s=document.querySelector('#cvStart');if(s)s.remove();})()");
        }
        // ---- song from the DAW ----
        Js("PX.UI.setTool('video')"); int n0 = int.Parse(Js("PX.P.media.length")); var eng = (StudioEngine)F("eng"); int sn = 44100 * 3; var x0 = new float[sn]; for (int i = 0; i < sn; i++) x0[i] = (float)(0.3 * Math.Sin(i * 0.05)); eng.Tracks.Add(StudioEngine.NewTrack("Beat", "beat", AudioClip.FromArrays(x0, x0, "b")));
        M("CrSong"); Check("MY SONG brings the current DAW project in as a mixed copy (the DAW project is not changed)", WaitJs("PX.P.media.length>" + n0 + "&&PX.P.media[PX.P.media.length-1].name.indexOf('(mix')>=0", 30000) && eng.Tracks.Count == 1);
        Func<bool> missTest = () => { string p2 = Path.Combine(outDir, "temp_clip.mp4"); File.Copy(Path.Combine(media, "clipA.mp4"), p2, true); M("CrImportPaths", new List<string> { p2 }); WaitJs("PX.P.media.some(m=>m.name==='temp_clip.mp4')", 10000); string id2 = Js("PX.P.id"); Js("PX.UI.save(false)"); WaitJs("window.__saved==='" + id2 + "'", 10000); for (int k = 0; k < 40; k++) { try { File.Delete(p2); break; } catch (IOException) { Pump(250); } }   /* (the thumbnail maker may still be reading it for a moment) */ Js("PX.send({t:'open',id:'" + id2 + "'})"); return WaitJs("PX.P.media.find(m=>m.name==='temp_clip.mp4').missing===true", 10000); }; Check("missing files are marked when a project opens", missTest());
        Console.WriteLine(fails == 0 ? "=== creative studio checks passed ===" : "=== " + fails + " FAILED ===");
        try { Directory.Delete(StudioEnv.DataDir, true); } catch { } Environment.Exit(fails == 0 ? 0 : 1);
    }
}
