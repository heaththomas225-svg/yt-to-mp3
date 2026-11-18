// Pictures of the website for the video (no window opens: Chrome runs headless): the real download page of purphexmusic.com, and the
// "Link this PC?" page drawn by the website's own code for a made-up member (Neo Wolf), served from a tiny local server.
//   node video/web.js <out folder>        writes download.png (+ download.btn: where its Download button is) and link.png (1600 x 804)
const http = require('http'); const fs = require('fs'); const path = require('path'); const { execFile } = require('child_process');
const out = process.argv[2] || 'D:/PurphexTests/video/web'; fs.mkdirSync(out, { recursive: true });
const WEB = 'C:/Users/thoma/purphex-web'; const CHROME = 'C:/Program Files/Google/Chrome/Application/chrome.exe';
const shot = (url, file) => new Promise((ok, bad) => execFile(CHROME, ['--headless=new', '--disable-gpu', '--hide-scrollbars', '--mute-audio', '--no-first-run', '--window-size=1600,804', '--virtual-time-budget=4000', '--screenshot=' + file, url], { timeout: 60000 }, (e) => (e ? bad(e) : ok())));   // (async: the little server below has to answer Chrome meanwhile)
const editions = require(path.join(WEB, 'lib/editions.js')); const site = require(path.join(WEB, 'lib/site.js'));
const dump = (url) => new Promise((ok, bad) => execFile(CHROME, ['--headless=new', '--disable-gpu', '--hide-scrollbars', '--no-first-run', '--window-size=1600,804', '--virtual-time-budget=4000', '--dump-dom', url], { timeout: 60000, maxBuffer: 1 << 24 }, (e, so) => (e ? bad(e) : ok(String(so)))));
const user = { id: 'v1', name: 'Neo Wolf', display: 'Neo Wolf', memberNo: 7, identities: [], passkeys: [], plan: { tier: 'fantasy', billing: 'lifetime' } };
// the download page as a signed-in member sees it; a little script writes where the Windows Download button is into the title (read with --dump-dom)
const where = "<script>addEventListener('load',function(){setTimeout(function(){var b=document.querySelector('.plat[data-os=win] .go');if(!b)return;var r=b.getBoundingClientRect();document.title='BTN:'+[r.x,r.y,r.width,r.height].map(Math.round).join(',');},300)})</script>";
const dlHtml = site.download({ user, release: { version: '3.80.12', size: '64 MB', date: '18 November 2026' } }).replace('</body>', where + '</body>');
const html = editions.linkPage({ user, link: { code: 'AB12-CD34', device: 'MY-PC' } });
const PUB = path.join(WEB, 'public'); const TYPES = { '.css': 'text/css', '.js': 'application/javascript', '.png': 'image/png', '.webp': 'image/webp', '.woff2': 'font/woff2', '.svg': 'image/svg+xml' };
const srv = http.createServer((req, res) => {
    const p = decodeURIComponent(req.url.split('?')[0]);
    if (p === '/download') { res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' }); return res.end(dlHtml); }
    if (p === '/link') { res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' }); return res.end(html); }
    const f = path.join(PUB, path.normalize(p)); if (!f.startsWith(PUB) || !fs.existsSync(f) || fs.statSync(f).isDirectory()) { res.writeHead(404); return res.end(); }
    res.writeHead(200, { 'Content-Type': TYPES[path.extname(f)] || 'application/octet-stream' }); fs.createReadStream(f).pipe(res);
});
srv.listen(0, '127.0.0.1', async () => {
    const port = srv.address().port;
    try { await shot('http://127.0.0.1:' + port + '/link', path.join(out, 'link.png')); console.log('link.png'); } catch (e) { console.log('link page failed: ' + e.message); }
    try { await shot('http://127.0.0.1:' + port + '/download', path.join(out, 'download.png')); const d = await dump('http://127.0.0.1:' + port + '/download'); const m = /BTN:([0-9,.-]+)/.exec(d); if (m) fs.writeFileSync(path.join(out, 'download.btn'), m[1]); console.log('download.png, button ' + (m ? m[1] : 'not found')); } catch (e) { console.log('download page failed: ' + e.message); }
    srv.close();
});
