'use strict';
// =====================================================================
//  PURPHEX CREATIVE SUITE: one home for the music studio, Photo Studio PRO and Video Studio PRO. The SUITE button opens it:
//  every project in one list (songs, designs, videos), a shared media library (photos used in videos, uploads, the design
//  on the canvas, the frame under the playhead) that moves pictures between the editors, the edition and trial on this PC,
//  help and settings. The edition comes from the signed licence the website gives this PC (the app only reads it).
// =====================================================================
(() => {
const UI = PX.UI, $ = (s, r) => (r || document).querySelector(s);
const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; };
const esc = (s) => String(s == null ? '' : s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const S = PX.Suite = { info: null, view: 'home' };
PX.on('suiteInfo', m => { S.info = m; if (S.open) S.render(); });
PX.on('artUploads', m => { S.uploads = m.list || []; if (S.open && S.view === 'media') S.render(); });

// the button on the top bar
function addButton() { const tools = $('#top .tools'); if (!tools || $('#toolSuite')) return; const b = el('button', 'tool suitebtn', '⌂ SUITE'); b.id = 'toolSuite'; b.title = 'Purphex Creative Suite: your songs, designs and videos, shared media, your edition, help'; b.onclick = () => S.show('home'); tools.insertBefore(b, tools.firstChild); }

S.show = (view) => { S.view = view || 'home'; S.open = true; let o = $('#suite'); if (!o) { o = el('div', 'suiteov'); o.id = 'suite'; document.getElementById('app').appendChild(o); } o.classList.remove('hidden'); PX.send({ t: 'suiteInfo' }); if (S.view === 'media') PX.send({ t: 'artUploads' }); S.render(); };
S.close = () => { S.open = false; const o = $('#suite'); if (o) o.classList.add('hidden'); };
addEventListener('keydown', (e) => { if (S.open && e.key === 'Escape') { e.preventDefault(); e.stopImmediatePropagation(); S.close(); } }, true);

const go = { daw: () => { S.close(); PX.send({ t: 'toDaw' }); }, photo: () => { S.close(); UI.setTool('cover'); }, video: () => { S.close(); UI.setTool('video'); } };
const TILES = [['daw', '🎛️', 'MUSIC STUDIO', 'Back to your song in the DAW', () => go.daw()], ['photo', '🖼️', 'PHOTO STUDIO PRO', 'Covers, banners, posts and thumbnails', () => go.photo()], ['video', '🎬', 'VIDEO STUDIO PRO', 'Music videos, lyric videos and promos', () => go.video()],
  ['projects', '📂', 'ALL PROJECTS', 'Songs, designs and videos in one place', () => S.show('projects')], ['media', '🗂️', 'SHARED MEDIA', 'Move pictures between the editors', () => S.show('media')], ['account', '💎', 'EDITION AND ACCOUNT', 'Your edition, trial and account', () => S.show('account')],
  ['help', '❓', 'HELP', 'The Creative Studio guide', () => { S.close(); PX.send({ t: 'suiteGo', to: 'help' }); }], ['settings', '⚙️', 'SETTINGS', 'Purphex settings', () => { S.close(); PX.send({ t: 'suiteGo', to: 'settings' }); }]];

S.render = () => {
  const o = $('#suite'); if (!o) return; const I = S.info || {}; o.innerHTML = '';
  const box = el('div', 'suitebox'); o.appendChild(box);
  const head = el('div', 'suitehead', '<h2>PURPHEX <span>CREATIVE SUITE</span></h2><span class="grow"></span>' + (I.edition ? '<span class="suitechip' + (I.trial ? ' trial' : '') + '" title="From the licence purphexmusic.com gave this PC">' + esc(I.edition) + (I.trial && I.countdown ? ' · trial ' + esc(I.countdown) : '') + '</span>' : '') + (S.view !== 'home' ? '<button id="suBack">‹ HOME</button>' : '') + '<button id="suX" title="Close (Esc)">X</button>');
  box.appendChild(head); $('#suX', head).onclick = S.close; const bk = $('#suBack', head); if (bk) bk.onclick = () => S.show('home');
  const body = el('div', 'suitebody'); box.appendChild(body);
  if (S.view === 'home') { const g = el('div', 'suitegrid'); body.appendChild(g); for (const [k, ic, n, d, fn] of TILES) { const t = el('button', 'suitetile', '<span class="ic">' + ic + '</span><b>' + n + '</b><small>' + esc(d) + '</small>'); t.dataset.k = k; t.onclick = fn; g.appendChild(t); }
    const now = el('p', 'hint'); now.textContent = 'Open now: ' + (UI.tool === 'cover' ? 'Photo Studio PRO, "' + ((PX.Cover.art || {}).name || '') + '"' : 'Video Studio PRO, "' + (PX.P.name || '') + '"') + (I.version ? '   ·   Purphex Music ' + I.version : ''); body.appendChild(now); }
  else if (S.view === 'projects') projects(body, I);
  else if (S.view === 'media') media(body);
  else if (S.view === 'account') account(body, I);
};
function list(host, title, items, empty, row) { const c = el('div', 'sucol'); c.appendChild(el('h3', '', esc(title) + ' <small>' + items.length + '</small>')); host.appendChild(c); if (!items.length) { c.appendChild(el('p', 'hint', esc(empty))); return; } for (const it of items.slice(0, 60)) { const r = el('div', 'surow'); row(r, it); c.appendChild(r); } }
const btn = (host, t, fn, tip) => { const b = el('button', '', t); if (tip) b.title = tip; b.onclick = fn; host.appendChild(b); return b; };
function projects(body, I) {
  if (!S.info) { body.appendChild(el('p', 'hint', 'Loading...')); return; } const cols = el('div', 'sucols'); body.appendChild(cols);
  list(cols, 'Songs (DAW)', I.daw || [], 'No saved songs yet.', (r, p) => { r.appendChild(el('span', 'nm', esc(p.name) + '<small>' + esc(p.updated || '') + '</small>')); btn(r, 'MAKE A VIDEO', () => { S.close(); UI.setTool('video'); PX.send({ t: 'fromProject', path: p.path }); }, 'Bring this song (mixed) and its cover into Video Studio'); btn(r, 'IN PROJECTS', () => { S.close(); PX.send({ t: 'suiteGo', to: 'projects' }); }, 'Go to the Projects page of the DAW'); });
  list(cols, 'Designs (Photo Studio PRO)', I.art || [], 'No saved designs yet.', (r, p) => { r.appendChild(el('span', 'nm', esc(p.name) + '<small>' + esc(p.modified || '') + '</small>')); btn(r, 'OPEN', () => { S.close(); UI.setTool('cover'); PX.send({ t: 'artOpen', id: p.id }); }); });
  list(cols, 'Videos (Video Studio PRO)', I.video || [], 'No saved videos yet.', (r, p) => { r.appendChild(el('span', 'nm', esc(p.name) + '<small>' + esc(p.modified || '') + (p.missing ? ' · ' + p.missing + ' missing file(s)' : '') + '</small>')); btn(r, 'OPEN', () => { S.close(); UI.setTool('video'); PX.send({ t: 'open', id: p.id }); }); });
}
// a picture for the shared library: { name, url, from, toPhoto(), toVideo() }
S.frameUrl = () => { const W = PX.P.settings.w, H = PX.P.settings.h, f = Math.min(1, 1920 / Math.max(W, H)), c = document.createElement('canvas'); c.width = Math.round(W * f); c.height = Math.round(H * f); PX.drawFrame(c.getContext('2d'), PX.play.t, c.width, c.height, true); return c.toDataURL('image/png'); };
S.designUrl = () => { const C = PX.Cover, f = Math.min(1, 1600 / Math.max(C.art.w, C.art.h)), c = document.createElement('canvas'); c.width = Math.round(C.art.w * f); c.height = Math.round(C.art.h * f); C._exporting = true; try { C.drawArt(c.getContext('2d'), c.width, c.height); } finally { C._exporting = false; } return c.toDataURL('image/png'); };
S.toPhotoNew = (url, name) => { UI.setTool('cover'); PX.Cover.startFromPhoto(url, name); };
S.toPhotoLayer = (url, name) => { UI.setTool('cover'); PX.Cover.addLayer({ type: 'image', name, url, fit: 'fit', scale: 0.6, crop: { l: 0, r: 0, t: 0, b: 0 } }); };
S.urlToVideo = async (url, name) => { const im = new Image(); await new Promise((ok, bad) => { im.onload = ok; im.onerror = bad; im.src = url; }); const c = document.createElement('canvas'); c.width = im.naturalWidth; c.height = im.naturalHeight; c.getContext('2d').drawImage(im, 0, 0); UI.setTool('video'); await UI.addOverlayCanvas(c, name); };
S.items = () => { const out = []; const C = PX.Cover;
  if (PX.P.clips.some(c => PX.isVisualKind(c)) || PX.P.media.length) out.push({ name: 'Video frame at ' + PX.fmtT(PX.play.t), from: 'Video Studio PRO', live: 'frame' });
  if (C && C.art && C.art.layers.length) out.push({ name: 'Design: ' + C.art.name, from: 'Photo Studio PRO', live: 'design' });
  for (const m of PX.P.media) if (m.kind === 'image' && !m.missing) out.push({ name: m.name, from: 'In this video', url: m.url, media: m });
  for (const u of S.uploads || []) out.push({ name: u.name, from: 'Photo uploads', url: u.url, upload: u }); return out; };
function media(body) {
  body.appendChild(el('p', 'hint', 'Pictures from both editors. TO PHOTO starts a design from it (or ADD TO DESIGN puts it on the one you have open), TO VIDEO puts it on your video at the playhead.'));
  const g = el('div', 'sumedia'); body.appendChild(g); const items = S.items(); if (!items.length) { g.appendChild(el('p', 'hint', 'Nothing yet: import photos in Video Studio, or upload pictures in Photo Studio.')); return; }
  for (const it of items) { const c = el('div', 'sucard'); const th = el('div', 'suth'); const u = it.live === 'frame' ? S.frameUrl() : it.live === 'design' ? S.designUrl() : it.url; th.style.backgroundImage = 'url("' + u + '")'; c.appendChild(th); c.appendChild(el('div', 'nm', esc(it.name) + '<small>' + esc(it.from) + '</small>')); const r = el('div', 'btnrow'); c.appendChild(r);
    if (it.live !== 'design') btn(r, 'TO PHOTO', () => { S.close(); S.toPhotoNew(it.live ? S.frameUrl() : u, it.name); }, 'Start a new design from this picture');
    if (it.live !== 'design') btn(r, 'ADD TO DESIGN', () => { S.close(); S.toPhotoLayer(it.live ? S.frameUrl() : u, it.name); }, 'Put it on the design that is open');
    if (it.live === 'design') btn(r, 'TO VIDEO', () => { S.close(); PX.Cover.exportImage({ fmt: 'png', scale: 1, what: 'video' }); }, 'Send the design to Video Studio (as a picture in MEDIA)');
    else if (it.upload) btn(r, 'TO VIDEO', () => { S.close(); UI.setTool('video'); PX.send({ t: 'sharedToVideo', file: it.upload.file }); }, 'Bring this upload into the video MEDIA');
    else if (it.live === 'frame') btn(r, 'SAVE AS PICTURE', () => { S.close(); S.urlToVideo(S.frameUrl(), 'Frame ' + PX.fmtT(PX.play.t)); }, 'Keep this frame as a picture clip on the video');
    g.appendChild(c); } }
function account(body, I) {
  if (!S.info) { body.appendChild(el('p', 'hint', 'Loading...')); return; }
  const b = el('div', 'suacct'); body.appendChild(b);
  b.innerHTML = '<div class="big">' + esc(I.edition || '') + '</div>' + (I.trial ? '<p><b>Free trial</b> with every feature: ' + esc(I.countdown || '') + ' left. Get an edition to keep everything after it ends.</p>' : '') + '<p class="hint">' + (I.linked ? 'This PC is linked to ' + esc(I.who || 'your Purphex account') + '. The edition comes from the signed licence purphexmusic.com gives this PC, and is renewed by itself.' : 'This PC is not linked to a Purphex account yet: link it in Settings > Account.') + '</p>';
  const r = el('div', 'btnrow'); b.appendChild(r); btn(r, 'EDITIONS AND PRICES', () => PX.send({ t: 'suiteGo', to: 'pricing' }), 'Opens purphexmusic.com/pricing'); btn(r, 'MY ACCOUNT ONLINE', () => PX.send({ t: 'suiteGo', to: 'accountWeb' }), 'Opens purphexmusic.com/account'); btn(r, 'ACCOUNT ON THIS PC', () => { S.close(); PX.send({ t: 'suiteGo', to: 'account' }); }, 'Settings > Account: link this PC, refresh the edition');
}
if (document.readyState === 'loading') addEventListener('DOMContentLoaded', () => setTimeout(addButton, 0)); else addButton();
})();
