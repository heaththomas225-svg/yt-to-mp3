using System; using System.Collections.Generic; using System.Drawing; using System.IO; using System.Linq; using System.Reflection; using System.Threading.Tasks; using System.Windows.Forms;
// Acceptance tests for Purphex Creative Studio with the real editor page, real media and real exports (muted, on the hidden desktop).
static class CRTEST
{
    static StudioForm sf; static readonly BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public; static int fails; static string ff, fp, outDir = @"C:\Users\thoma\nfout\crout", media = @"C:\Users\thoma\nfout\crmedia";
    static void Check(string n, bool ok, string d = "") { if (!ok) fails++; Console.WriteLine((ok ? "PASS  " : "FAIL  ") + n + (d.Length > 0 ? "   " + d : "")); Console.Out.Flush(); }
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
        Check("Creative Studio opens inside Purphex (side panel > Video Studio)", B != null && B.Ready && (int)F("tab") == 10, B == null ? "no browser" : B.Error);
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
        Check("the spectrum is computed from the real audio (louder parts give taller bars, silence gives none)", Js("(()=>{const a=PX.spectrum(5,32).bands,b=PX.spectrum(20,32).bands;return (Math.max(...a)>0.3&&Math.max(...b)<0.05)+''})()") == "true", Js("Math.max(...PX.spectrum(5,32).bands).toFixed(2)+' vs '+Math.max(...PX.spectrum(20,32).bands).toFixed(2)"));
        // ---- other formats ----
        Js("PX.P.clips=PX.P.clips.filter(c=>c.type!=='visual');PX.changed('x')");
        string o4 = Export("webm", "acceptance_webm", 640, 360); bool ok4; string i4 = File.Exists(o4) ? CreativeStore.Validate(fp, o4, 12, true, out ok4) : o4; Check("WebM (VP9 + Opus) export works", File.Exists(o4) && i4.Contains("vp9"), i4);
        string o5 = Export("mp4-h265", "acceptance_hevc", 640, 360); bool ok5; string i5 = File.Exists(o5) ? CreativeStore.Validate(fp, o5, 12, true, out ok5) : o5; Check("MP4 H.265 / HEVC export works", File.Exists(o5) && i5.Contains("hevc"), i5);
        // ---- errors ----
        StudioForm.CrSaveHook = null; Js("window.__aiErr=null;PX.UI.setTool('cover');document.querySelector('#gP').value='test';PX.Cover.generate()");
        Check("AI generation without a configured provider says what is missing (nothing fake is generated)", WaitJs("window.__aiErr", 10000) && Js("window.__aiErr").Contains("API key") && Js("PX.Cover.art.history.length") == "0", Js("window.__aiErr||''"));
        // ---- D: artwork project ----
        Js("(()=>{const C=PX.Cover;C.art.name='Cover A';const t=C.art.layers.find(l=>l.type==='text');C.begin();t.text='NIGHT DRIVE';t.color='#ffffff';C.commit();C.act('save')})()");
        string aid = Js("PX.Cover.art.id"); Check("an artwork project is saved (layers and text kept separately)", WaitJs("window.__artSaved==='" + aid + "'", 10000) && File.ReadAllText(CreativeStore.ProjPath("art", aid)).Contains("NIGHT DRIVE"));
        string png = Path.Combine(outDir, "cover_export.png"); try { File.Delete(png); } catch { } StudioForm.CrSaveHook = _ => png; Js("PX.Cover.act('png')"); var pend = DateTime.Now.AddSeconds(20); while (!File.Exists(png) && DateTime.Now < pend) Pump(200); Pump(500);
        bool pngOk = false; string pd = "no file"; if (File.Exists(png)) using (var bm = new Bitmap(png)) { pd = bm.Width + "x" + bm.Height; int w2 = 0; for (int y = (int)(0.75 * bm.Height); y < (int)(0.80 * bm.Height); y += 4) for (int x = (int)(0.3 * bm.Width); x < (int)(0.7 * bm.Width); x += 4) { var c = bm.GetPixel(x, y); if (c.R > 230 && c.G > 230 && c.B > 230) w2++; } pngOk = bm.Width == 3000 && bm.Height == 3000 && w2 > 30; pd += ", " + w2 + " white text pixels"; }
        Check("the artwork exports as a real 3000 x 3000 PNG with its text", pngOk, pd);
        // ---- song from the DAW ----
        Js("PX.UI.setTool('video')"); int n0 = int.Parse(Js("PX.P.media.length")); var eng = (StudioEngine)F("eng"); int sn = 44100 * 3; var x0 = new float[sn]; for (int i = 0; i < sn; i++) x0[i] = (float)(0.3 * Math.Sin(i * 0.05)); eng.Tracks.Add(StudioEngine.NewTrack("Beat", "beat", AudioClip.FromArrays(x0, x0, "b")));
        M("CrSong"); Check("MY SONG brings the current DAW project in as a mixed copy (the DAW project is not changed)", WaitJs("PX.P.media.length>" + n0 + "&&PX.P.media[PX.P.media.length-1].name.indexOf('(mix')>=0", 30000) && eng.Tracks.Count == 1);
        Func<bool> missTest = () => { string p2 = Path.Combine(outDir, "temp_clip.mp4"); File.Copy(Path.Combine(media, "clipA.mp4"), p2, true); M("CrImportPaths", new List<string> { p2 }); WaitJs("PX.P.media.some(m=>m.name==='temp_clip.mp4')", 10000); string id2 = Js("PX.P.id"); Js("PX.UI.save(false)"); WaitJs("window.__saved==='" + id2 + "'", 10000); File.Delete(p2); Js("PX.send({t:'open',id:'" + id2 + "'})"); return WaitJs("PX.P.media.find(m=>m.name==='temp_clip.mp4').missing===true", 10000); }; Check("missing files are marked when a project opens", missTest());
        Console.WriteLine(fails == 0 ? "=== creative studio checks passed ===" : "=== " + fails + " FAILED ===");
        try { Directory.Delete(StudioEnv.DataDir, true); } catch { } Environment.Exit(fails == 0 ? 0 : 1);
    }
}
