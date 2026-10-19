'use strict';
// =====================================================================
//  PURPHEX MUSIC PHOTO EDITOR: the top-toolbar layout. An app bar (name, new, open, save, undo, redo, zoom, size, export), a bar
//  of categories under it, and a drop-down panel for the category you pick (closed with X, Esc or the same button). No side
//  panels: the picture fills the space. Also: pick several layers (Shift + click, or drag a box), group them, copy and paste,
//  line them up and space them evenly, snap to other layers and to a grid, the Design, Uploads, Fonts, Shapes, Layers, Brand
//  Kit and AI Tools panels, and an export with format, size, quality and a see-through background.
// =====================================================================
(() => {
const C = PX.Cover, clamp = PX.clamp, uid = PX.uid, $c = (s) => document.querySelector('#cover ' + s);
const esc = (s) => String(s == null ? '' : s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const sel = () => C.art && C.art.layers.find(l => l.id === C.sel);
const isBox = (L) => (C.boxTypes || []).includes(L.type);
const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; };
const head = (b, t) => b.appendChild(el('div', 'hint', '<b>' + t + '</b>')).style.margin = '10px 2px 6px';
const hint = (b, t) => b.appendChild(el('p', 'hint', esc(t)));
const btn = (b, t, fn, cls) => { const x = el('button', cls || '', t); x.onclick = fn; b.appendChild(x); return x; };
const row = (b) => b.appendChild(el('div', 'btnrow'));
// a small store of settings kept by Purphex (the brand kit, saved text styles, favourite and recent templates)
const KV = {}; const kvWait = {};
C.kvGet = (k) => new Promise(res => { if (k in KV) return res(KV[k]); (kvWait[k] = kvWait[k] || []).push(res); PX.send({ t: 'kvLoad', key: k }); });
C.kvSet = (k, v) => { KV[k] = v; PX.send({ t: 'kvSave', key: k, value: JSON.stringify(v) }); };
PX.on('kv', m => { let v = null; try { v = m.value ? JSON.parse(m.value) : null; } catch (e) { } KV[m.key] = v; (kvWait[m.key] || []).forEach(f => f(v)); delete kvWait[m.key]; });
// ---------------- the categories ----------------
(C.extraTabs = C.extraTabs || {}).templates = (b) => { hint(b, 'Over 2,000 templates: search them, filter by size and style, star your favourites.'); btn(row(b), 'BROWSE ALL TEMPLATES', () => { C.openPanel(null); C.openWelcome(); }, 'accent'); };
const CATS = [['templates', 'Templates', '🧩'], ['design', 'Design', '📐'], ['photos', 'Images', '🖼️'], ['uploads', 'Uploads', '📁'], ['elements', 'Elements', '🏷️'], ['stickers', 'Stickers', '⭐'], ['text', 'Text', '🔤'], ['fonts', 'Fonts', 'Aa'], ['bg', 'Backgrounds', '🎨'], ['shapes', 'Shapes', '⬟'], ['draw', 'Draw', '✏️'], ['frames', 'Frames', '🟪'], ['adjust', 'Adjust', '🎚️'], ['effects', 'Effects', '✨'], ['filters', 'Filters', '🎞️'], ['cutout', 'Cutout', '✂️'], ['collage', 'Collage', '🔲'], ['layers', 'Layers', '📚'], ['brand', 'Brand Kit', '💼'], ['ai', 'AI Tools', '🤖']];
let pop = null, park = null, open = null, side = null, sideTab = 'props';
// the panel on the right: the settings of the picked layer, or the layers; it can be tucked away
C.sideTab = (k) => { sideTab = k; if (!side) return; side.querySelectorAll('[data-s]').forEach(b => b.classList.toggle('on', b.dataset.s === k)); const body = side.querySelector('#cvSideB'); for (const c of Array.from(body.children)) park.appendChild(c); body.appendChild(document.querySelector(k === 'layers' ? '#cvLayers' : '#cvProps')); if (k === 'layers') C.renderLayers(); else C.renderProps(); };
C.sideShow = (on) => { if (!side) return; side.classList.toggle('hidden', !on); document.querySelector('#cvSideOpen').classList.toggle('hidden', on); try { localStorage.setItem('pxSide', on ? '1' : '0'); } catch (e) { } setTimeout(() => C.size(), 0); };
// the histogram of the picture (redrawn a moment after it changes)
let histT = 0; C.histogram = () => { clearTimeout(histT); histT = setTimeout(() => { const hc = document.querySelector('#cvHist'), cv = document.querySelector('#cvCanvas'); if (!hc || !cv || hc.offsetParent === null) return; const w = Math.min(cv.width, 360), h = Math.round(cv.height * w / cv.width); const t = document.createElement('canvas'); t.width = w; t.height = h; const tg = t.getContext('2d'); tg.drawImage(cv, 0, 0, w, h); const d = tg.getImageData(0, 0, w, h).data, R = new Array(64).fill(0), G = new Array(64).fill(0), B = new Array(64).fill(0); for (let i = 0; i < d.length; i += 4) { R[d[i] >> 2]++; G[d[i + 1] >> 2]++; B[d[i + 2] >> 2]++; } const mx = Math.max(...R, ...G, ...B) || 1, g = hc.getContext('2d'); g.clearRect(0, 0, hc.width, hc.height); g.globalCompositeOperation = 'lighter'; for (const [arr, col] of [[R, 'rgba(255,70,70,0.7)'], [G, 'rgba(70,255,120,0.7)'], [B, 'rgba(80,140,255,0.7)']]) { g.fillStyle = col; g.beginPath(); g.moveTo(0, hc.height); arr.forEach((v, i) => g.lineTo(i / 63 * hc.width, hc.height - Math.sqrt(v / mx) * (hc.height - 4))); g.lineTo(hc.width, hc.height); g.fill(); } g.globalCompositeOperation = 'source-over'; }, 250); };
function build() {
  const root = document.getElementById('cover'); if (!root || root._tb) return; root._tb = true; root.classList.add('tb');
  const center = $c('#cvCenter'), appbar = center.querySelector('.row'), bottom = center.querySelectorAll('.row')[1];
  appbar.classList.add('cvapp');
  // export, song cover and video go into one EXPORT button; zoom moves up into the app bar
  if (bottom) { const z = el('span', 'cvzoom'); for (const id of ['zoomOut', 'cvZoomL', 'zoomIn', 'zoomFit']) { const n = bottom.querySelector('[data-c="' + id + '"]') || bottom.querySelector('#' + id); if (n) z.appendChild(n); } appbar.insertBefore(z, appbar.querySelector('#cvSize')); bottom.style.display = 'none'; }
  const ex = el('button', 'accent', 'EXPORT'); ex.id = 'cvExport'; ex.title = 'Save the picture as PNG, JPEG or WebP, use it as your song cover, or send it to the video editor'; ex.onclick = () => C.exportDialog(); appbar.appendChild(ex);
  const st = el('span', 'cvsave', ''); st.id = 'cvSaveState'; appbar.insertBefore(st, appbar.querySelector('[data-c="undo"]')); C.saveState();
  // the work area: the tool strip | (the options of the tool) | the canvas | the Properties / Layers panel
  const work = el('div', 'cvwork'); work.id = 'cvWork'; center.insertBefore(work, appbar.nextSibling);
  const cats = el('nav', 'cvrail'); cats.id = 'cvCats';
  for (const [k, lab, ic] of CATS) { const b = el('button', '', '<span class="ic">' + ic + '</span><span class="lb">' + esc(lab) + '</span>'); b.dataset.k = k; b.title = lab; b.onclick = () => C.openPanel(open === k ? null : k); cats.appendChild(b); }
  work.appendChild(cats); const stg = $c('#cvStage'); work.appendChild(stg);
  pop = el('div', 'cvpop hidden'); pop.id = 'cvPop'; pop.innerHTML = '<div class="cvpophead"><b id="cvPopT"></b><span class="grow"></span><button id="cvPopX" title="Close (Esc)">X</button></div>'; work.appendChild(pop);
  side = el('aside', 'cvside'); side.id = 'cvSide'; side.innerHTML = '<div class="cvsidehead"><button data-s="props" class="on">PROPERTIES</button><button data-s="layers">LAYERS</button><span class="grow"></span><button id="cvSideX" title="Hide this panel for a bigger canvas">⟩</button></div><canvas id="cvHist" width="260" height="70" title="Histogram: how much of the picture is dark (left) and light (right)"></canvas><div class="cvsidebody" id="cvSideB"></div>'; work.appendChild(side);
  const tab0 = el('button', 'cvsidetab hidden', '⟨'); tab0.id = 'cvSideOpen'; tab0.title = 'Show Properties and Layers'; work.appendChild(tab0);
  side.querySelectorAll('[data-s]').forEach(b => b.onclick = () => C.sideTab(b.dataset.s)); side.querySelector('#cvSideX').onclick = () => C.sideShow(false); tab0.onclick = () => C.sideShow(true);
  const tab = $c('#cvTab'); pop.appendChild(tab); pop.querySelector('#cvPopX').onclick = () => C.openPanel(null);
  park = el('div', 'hidden'); park.id = 'cvPark'; root.appendChild(park); park.appendChild($c('#cvLayers')); park.appendChild($c('#cvProps')); C.sideTab('props');
  $c('#cvLeft').style.display = 'none'; $c('#cvRight').style.display = 'none';
  // the layers and the settings live in the park until a panel shows them (they are drawn all the time)
  const orig = C.renderTab; C.renderTab = () => { const lw = document.querySelector('#cvLayers'), pw = document.querySelector('#cvProps'); if (park && lw && !side.contains(lw)) park.appendChild(lw); if (park && pw && !side.contains(pw)) park.appendChild(pw); orig(); };
  // pictures dropped on the editor are added to the design
  root.addEventListener('dragover', e => { if (PX.UI.tool === 'cover' && e.dataTransfer && [...e.dataTransfer.types].includes('Files')) { e.preventDefault(); e.stopPropagation(); root.classList.add('dropping'); } }, true);
  root.addEventListener('dragleave', () => root.classList.remove('dropping'));
  root.addEventListener('drop', e => { if (PX.UI.tool !== 'cover' || !e.dataTransfer.files.length) return; e.preventDefault(); e.stopPropagation(); root.classList.remove('dropping'); try { window.chrome.webview.postMessageWithAdditionalObjects('artdrop', e.dataTransfer.files); } catch (er) { PX.toast('Drop is not available here: use ADD PHOTOS.', true); } }, true);
  stageMouse(); keys(); PX.send({ t: 'fontList' }); let so = '1'; try { so = localStorage.getItem('pxSide') || '1'; } catch (e) { } C.sideShow(so === '1');
}
C.openPanel = (k) => {
  build(); open = k; document.querySelectorAll('#cvCats button').forEach(b => b.classList.toggle('on', b.dataset.k === k));
  if (!k) { pop.classList.add('hidden'); return; } const cat = CATS.find(c => c[0] === k); pop.querySelector('#cvPopT').textContent = (cat ? cat[1] : k).toUpperCase();
  pop.classList.remove('hidden');
  C.tab = k; C.renderTab();
};
// ---------------- several layers at once ----------------
C.multi = new Set();
const members = (L) => L.group ? C.art.layers.filter(q => q.group === L.group) : [L];
function hit(px, py, W, H) { for (const L of C.art.layers.slice().reverse()) { if (!L.vis || L.locked || !isBox(L)) continue; const b = C.box(L, W, H), a = -(L.rot || 0) * Math.PI / 180, lx = (px - b.cx) * Math.cos(a) - (py - b.cy) * Math.sin(a), ly = (px - b.cx) * Math.sin(a) + (py - b.cy) * Math.cos(a); if (Math.abs(lx) < b.w / 2 && Math.abs(ly) < b.h / 2) return L; } return null; }
function stageMouse() {
  const st = $c('#cvStage'), cv = $c('#cvCanvas');
  st.addEventListener('mousedown', (e) => {
    if (e.button !== 0 || e.target !== cv || C.tool !== 'move' || C._editing) return; const r = cv.getBoundingClientRect(), k = cv.width / r.width, px = (e.clientX - r.left) * k, py = (e.clientY - r.top) * k, W = cv.width, H = cv.height; const L = hit(px, py, W, H);
    if (e.shiftKey && L) { e.stopPropagation(); e.preventDefault(); const ms = members(L); if (ms.every(q => C.multi.has(q.id))) ms.forEach(q => C.multi.delete(q.id)); else { const cur = sel(); if (cur && !C.multi.size && isBox(cur)) members(cur).forEach(q => C.multi.add(q.id)); ms.forEach(q => C.multi.add(q.id)); } C.sel = L.id; C.render(); return; }
    if (L && L.group && !C.multi.has(L.id)) { C.multi = new Set(members(L).map(q => q.id)); }
    if (L && C.multi.size > 1 && C.multi.has(L.id)) {   // move the whole selection together
      e.stopPropagation(); e.preventDefault(); C.sel = L.id; C.begin(); const list = C.art.layers.filter(q => C.multi.has(q.id) && !q.locked), start = list.map(q => [q.x || 0, q.y || 0]);
      const mv = (ev) => { const dx = ((ev.clientX - r.left) * k - px) / (W / 2), dy = ((ev.clientY - r.top) * k - py) / (H / 2); list.forEach((q, i) => { q.x = start[i][0] + dx; q.y = start[i][1] + dy; }); C.draw(); };
      const up = () => { removeEventListener('mousemove', mv); removeEventListener('mouseup', up); C.commit(); }; addEventListener('mousemove', mv); addEventListener('mouseup', up); return;
    }
    if (!L && !e.shiftKey) {   // drag on empty space: a box picks everything inside it
      C.multi.clear(); const x0 = px, y0 = py; let moved = false;
      const mv = (ev) => { const qx = (ev.clientX - r.left) * k, qy = (ev.clientY - r.top) * k; if (Math.abs(qx - x0) + Math.abs(qy - y0) > 6) moved = true; C._band = [Math.min(x0, qx), Math.min(y0, qy), Math.abs(qx - x0), Math.abs(qy - y0)]; C.draw(); };
      const up = () => { removeEventListener('mousemove', mv); removeEventListener('mouseup', up); if (moved && C._band) { const [bx, by, bw, bh] = C._band; for (const q of C.art.layers) { if (!q.vis || q.locked || !isBox(q)) continue; const b = C.box(q, W, H); if (b.cx > bx && b.cx < bx + bw && b.cy > by && b.cy < by + bh) members(q).forEach(m => C.multi.add(m.id)); } const first = C.art.layers.find(q => C.multi.has(q.id)); if (first) C.sel = first.id; } C._band = null; C.render(); };
      addEventListener('mousemove', mv); addEventListener('mouseup', up); return;   // (the editor's own click still runs: it unselects)
    }
    if (L && !C.multi.has(L.id)) C.multi.clear();
  }, true);
}
const selList = () => C.multi.size > 1 ? C.art.layers.filter(q => C.multi.has(q.id)) : (sel() ? members(sel()) : []);
C.groupSel = () => { const l = selList(); if (l.length < 2) return; C.begin(); const g = uid('g'); l.forEach(q => q.group = g); C.commit(); PX.toast('Grouped ' + l.length + ' layers: they move together (UNGROUP splits them).'); };
C.ungroupSel = () => { const l = selList(); C.begin(); l.forEach(q => delete q.group); C.multi.clear(); C.commit(); };
C.deleteSel = () => { const l = selList(); if (!l.length) return; C.begin(); C.art.layers = C.art.layers.filter(q => !l.includes(q)); C.multi.clear(); C.sel = null; C.commit(); };
C.dupSel = () => { const l = selList(); if (!l.length) return; C.begin(); const gmap = {}; const copies = l.map(q => { const n = JSON.parse(JSON.stringify(q)); n.id = uid('l'); n.name = q.name + ' copy'; if (n.group) n.group = gmap[n.group] || (gmap[n.group] = uid('g')); if (isBox(n)) { n.x = (n.x || 0) + 0.05; n.y = (n.y || 0) + 0.05; } return n; }); C.art.layers.push(...copies); C.multi = new Set(copies.map(q => q.id)); C.sel = copies[0].id; C.commit(); };
C.copySel = () => { const l = selList(); if (l.length) { C._clip = JSON.stringify(l); PX.toast('Copied ' + l.length + ' layer' + (l.length > 1 ? 's' : '') + ' (Ctrl+V pastes).'); } };
C.pasteSel = () => { if (!C._clip) return; const l = JSON.parse(C._clip); C.begin(); const gmap = {}; l.forEach(n => { n.id = uid('l'); if (n.group) n.group = gmap[n.group] || (gmap[n.group] = uid('g')); if (isBox(n)) { n.x = (n.x || 0) + 0.04; n.y = (n.y || 0) + 0.04; } }); C.art.layers.push(...l); C.multi = new Set(l.map(q => q.id)); C.sel = l[0].id; C._clip = JSON.stringify(l); C.commit(); };
// line several up (to the edges of the selection) or space them evenly
C.alignSel = (how) => { const l = selList().filter(isBox); if (l.length < 2) return; const W = C.art.w, H = C.art.h, bs = l.map(q => [q, C.box(q, W, H, true)]); const minX = Math.min(...bs.map(([, b]) => b.cx - b.w / 2)), maxX = Math.max(...bs.map(([, b]) => b.cx + b.w / 2)), minY = Math.min(...bs.map(([, b]) => b.cy - b.h / 2)), maxY = Math.max(...bs.map(([, b]) => b.cy + b.h / 2));
  C.begin(); for (const [q, b] of bs) { let cx = b.cx, cy = b.cy; if (how === 'l') cx = minX + b.w / 2; if (how === 'r') cx = maxX - b.w / 2; if (how === 'c') cx = (minX + maxX) / 2; if (how === 't') cy = minY + b.h / 2; if (how === 'b') cy = maxY - b.h / 2; if (how === 'm') cy = (minY + maxY) / 2; q.x = (cx - W / 2) / (W / 2); q.y = (cy - H / 2) / (H / 2); } C.commit(); };
C.distributeSel = (axis) => { const l = selList().filter(isBox); if (l.length < 3) { PX.toast('Pick three or more layers to space them evenly.'); return; } const k = axis === 'h' ? 'x' : 'y'; l.sort((a, b) => (a[k] || 0) - (b[k] || 0)); const a0 = l[0][k] || 0, a1 = l[l.length - 1][k] || 0; C.begin(); l.forEach((q, i) => q[k] = a0 + (a1 - a0) * i / (l.length - 1)); C.commit(); };
// ---------------- snapping: to the middle, to the edges and middles of other layers, and to the grid ----------------
C.view = { grid: false, margins: false, snapGrid: false, snapLayers: true };
C.snapHook = (L, ev) => {
  if (ev && ev.altKey) { C._snapLines = null; return; } const W = C.art.w, H = C.art.h, b = C.box(L, W, H, true), th = Math.max(W, H) * 0.008, xs = [W / 2], ys = [H / 2], lines = { xs: [], ys: [] };
  if (C.view.snapLayers) for (const q of C.art.layers) { if (q === L || !q.vis || !isBox(q) || C.multi.has(q.id)) continue; const o = C.box(q, W, H, true); xs.push(o.cx, o.cx - o.w / 2, o.cx + o.w / 2); ys.push(o.cy, o.cy - o.h / 2, o.cy + o.h / 2); }
  if (C.view.snapGrid) for (let i = 1; i < 12; i++) { xs.push(W * i / 12); ys.push(H * i / 12); }
  const try1 = (vals, ...mine) => { let best = null; for (const m of mine) for (const v of vals) { const d = v - m; if (Math.abs(d) < th && (!best || Math.abs(d) < Math.abs(best[0]))) best = [d, v]; } return best; };
  const sx = try1(xs, b.cx, b.cx - b.w / 2, b.cx + b.w / 2), sy = try1(ys, b.cy, b.cy - b.h / 2, b.cy + b.h / 2);
  if (sx) { L.x = (L.x || 0) + sx[0] / (W / 2); lines.xs.push(sx[1]); } if (sy) { L.y = (L.y || 0) + sy[0] / (H / 2); lines.ys.push(sy[1]); } C._snapLines = (sx || sy) ? lines : null;
};
// drawn on top of the picture: the grid, the safe margins, the snap lines, the box being dragged and the picked layers
const prevAfter = C.afterDraw; C.afterDraw = () => {
  if (C.multi.size) { const ids = new Set(C.art.layers.map(q => q.id)); for (const id of Array.from(C.multi)) if (!ids.has(id)) C.multi.delete(id); if (!C.sel) C.multi.clear(); }
  const L = sel(); const many = C.multi.size > 1; if (prevAfter) { if (many) { const keep = C.sel; C.sel = null; prevAfter(); C.sel = keep; } else prevAfter(); }
  const cv = $c('#cvCanvas'); if (!cv) return; const g = cv.getContext('2d'), W = cv.width, H = cv.height, k = W / C.art.w; g.save();
  if (C.view.grid) { g.strokeStyle = 'rgba(255,255,255,0.18)'; g.lineWidth = 1; for (let i = 1; i < 12; i++) { g.beginPath(); g.moveTo(W * i / 12, 0); g.lineTo(W * i / 12, H); g.moveTo(0, H * i / 12); g.lineTo(W, H * i / 12); g.stroke(); } }
  if (C.view.margins) { g.strokeStyle = 'rgba(60,225,255,0.6)'; g.setLineDash([6, 4]); g.strokeRect(W * 0.05, H * 0.05, W * 0.9, H * 0.9); g.setLineDash([]); }
  if (C._snapLines) { g.strokeStyle = '#ff50c4'; g.lineWidth = 1.5; g.setLineDash([7, 5]); for (const x of C._snapLines.xs) { g.beginPath(); g.moveTo(x * k, 0); g.lineTo(x * k, H); g.stroke(); } for (const y of C._snapLines.ys) { g.beginPath(); g.moveTo(0, y * k); g.lineTo(W, y * k); g.stroke(); } g.setLineDash([]); }
  if (C._band) { const [x, y, w, h] = C._band; g.fillStyle = 'rgba(60,225,255,0.12)'; g.fillRect(x, y, w, h); g.strokeStyle = '#3ce1ff'; g.strokeRect(x, y, w, h); }
  if (many) { g.strokeStyle = '#3ce1ff'; g.lineWidth = 2; g.setLineDash([8, 6]); for (const q of C.art.layers) if (C.multi.has(q.id) && isBox(q)) { const b = C.box(q, W, H); g.save(); g.translate(b.cx, b.cy); g.rotate((q.rot || 0) * Math.PI / 180); g.strokeRect(-b.w / 2, -b.h / 2, b.w, b.h); g.restore(); } g.setLineDash([]); }
  g.restore(); multiBar(many); C.histogram();
  // the toolbars on the picture stay clear of an open options panel
  if (pop && !pop.classList.contains("hidden")) { const st = $c("#cvStage"), pr = pop.getBoundingClientRect(), sr = st.getBoundingClientRect(), minL = pr.right - sr.left + st.scrollLeft + 6; for (const id of ["#cvBar", "#cvMultiBar"]) { const b = document.querySelector(id); if (b && !b.classList.contains("hidden") && parseFloat(b.style.left || 0) < minL) b.style.left = minL + "px"; } }
};
let mbar = null;
function multiBar(show) {
  const st = $c('#cvStage'); if (!st) return; if (!mbar) { mbar = el('div', 'cvbar hidden'); mbar.id = 'cvMultiBar'; st.appendChild(mbar); }
  if (!show) { mbar.classList.add('hidden'); return; } const n = C.multi.size, grouped = selList().every(q => q.group && q.group === selList()[0].group);
  const key = n + '|' + grouped; if (mbar._key !== key) { mbar._key = key; mbar.innerHTML = '<span class="nm">' + n + ' picked</span>'; const add = (t, fn, cls, tip) => { const b = el('button', cls || '', t); b.title = tip || ''; b.onmousedown = e => e.stopPropagation(); b.onclick = e => { e.stopPropagation(); fn(); }; mbar.appendChild(b); };
    add('⇤', () => C.alignSel('l'), '', 'Line up the left edges'); add('↔', () => C.alignSel('c'), '', 'Line up the middles (across)'); add('⇥', () => C.alignSel('r'), '', 'Line up the right edges'); add('⤒', () => C.alignSel('t'), '', 'Line up the tops'); add('↕', () => C.alignSel('m'), '', 'Line up the middles (up and down)'); add('⤓', () => C.alignSel('b'), '', 'Line up the bottoms');
    add('SPACE ACROSS', () => C.distributeSel('h'), '', 'Space them evenly from left to right'); add('SPACE DOWN', () => C.distributeSel('v'), '', 'Space them evenly from top to bottom'); add(grouped ? 'UNGROUP' : 'GROUP', grouped ? C.ungroupSel : C.groupSel); add('⧉ DUPLICATE', C.dupSel); add('🗑 DELETE', C.deleteSel, 'danger'); }
  mbar.classList.remove('hidden'); const cv = $c('#cvCanvas'), sr = st.getBoundingClientRect(), cr = cv.getBoundingClientRect(); mbar.style.left = Math.max(st.scrollLeft + 6, cr.left - sr.left + st.scrollLeft + cr.width / 2 - mbar.offsetWidth / 2) + 'px'; mbar.style.top = (cr.top - sr.top + st.scrollTop + 8) + 'px';
}
// ---------------- keys (only while the photo editor is open, and never while you type) ----------------
function keys() {
  document.addEventListener('keydown', (e) => {
    if (!PX.UI || PX.UI.tool !== 'cover') return; const tag = (e.target.tagName || '').toLowerCase(); if (tag === 'input' || tag === 'textarea' || tag === 'select') return; const ctrl = e.ctrlKey || e.metaKey, k = e.key.toLowerCase();
    if (e.key === 'Escape' && open) { C.openPanel(null); e.preventDefault(); return; }
    if (ctrl && k === 'c') { e.preventDefault(); e.stopImmediatePropagation(); C.copySel(); }
    else if (ctrl && k === 'v') { e.preventDefault(); e.stopImmediatePropagation(); C.pasteSel(); }
    else if (ctrl && k === 'g') { e.preventDefault(); e.stopImmediatePropagation(); e.shiftKey ? C.ungroupSel() : C.groupSel(); }
    else if (ctrl && k === 'a') { e.preventDefault(); e.stopImmediatePropagation(); C.multi = new Set(C.art.layers.filter(q => q.vis && !q.locked && isBox(q)).map(q => q.id)); const f = C.art.layers.find(q => C.multi.has(q.id)); if (f) C.sel = f.id; C.render(); }
    else if (ctrl && k === 'd' && C.multi.size > 1) { e.preventDefault(); e.stopImmediatePropagation(); C.dupSel(); }
    else if ((e.key === 'Delete' || e.key === 'Backspace') && C.multi.size > 1) { e.preventDefault(); e.stopImmediatePropagation(); C.deleteSel(); }
  }, true);
}
// ---------------- the save state in the app bar ----------------
C.saveState = () => { const s = document.querySelector('#cvSaveState'); if (!s) return; s.textContent = C.dirty ? '● Not saved' : '✓ Saved'; s.className = 'cvsave ' + (C.dirty ? 'dirty' : 'ok'); };
setInterval(() => { if (PX.UI && PX.UI.tool === 'cover') C.saveState(); }, 800);
// ---------------- the export ----------------
C.exportDialog = () => {
  const W = C.art.w, H = C.art.h; PX.UI.modal('<h3>Export</h3><div class="f"><label>Format</label><select id="exF"><option value="png">PNG (sharp, see-through possible)</option><option value="jpg">JPEG (small file)</option><option value="webp">WebP (small and sharp)</option></select></div><div class="f"><label>Size</label><select id="exS"><option value="0.5">Half (' + Math.round(W / 2) + ' x ' + Math.round(H / 2) + ')</option><option value="1" selected>Full (' + W + ' x ' + H + ')</option><option value="2">Double (' + W * 2 + ' x ' + H * 2 + ')</option></select></div><div class="f" id="exQf"><label>Quality</label><input type="range" id="exQ" min="50" max="100" value="92"><input type="number" id="exQn" min="50" max="100" value="92"></div><div class="f" id="exTf"><label>See-through background</label><input type="checkbox" id="exT" style="justify-self:start"></div><div class="f"><label>File name</label><input type="text" id="exN"></div><p class="hint">SVG is not offered: effects, photos and blends cannot be kept exactly in SVG, and Purphex only exports what looks the same.</p><div class="acts"><button id="mNo">CANCEL</button><button id="exDaw">USE AS SONG COVER</button><button id="exVid">SEND TO VIDEO EDITOR</button><button id="exGo" class="accent">EXPORT</button></div>', b => {
    const f = b.querySelector('#exF'), q = b.querySelector('#exQ'), qn = b.querySelector('#exQn'), t = b.querySelector('#exT'), n = b.querySelector('#exN'); n.value = C.art.name;
    const upd = () => { b.querySelector('#exQf').style.display = f.value === 'png' ? 'none' : ''; b.querySelector('#exTf').style.display = f.value === 'jpg' ? 'none' : ''; }; f.onchange = upd; upd(); q.oninput = () => qn.value = q.value; qn.onchange = () => q.value = qn.value;
    b.querySelector('#mNo').onclick = PX.UI.close;
    b.querySelector('#exGo').onclick = () => { const o = { fmt: f.value, scale: +b.querySelector('#exS').value, quality: +q.value / 100, transparent: t.checked && f.value !== 'jpg', name: n.value.trim() || C.art.name }; PX.UI.close(); C.exportImage(o); };
    b.querySelector('#exDaw').onclick = () => { PX.UI.close(); C.exportImage({ fmt: 'png', scale: 1, what: 'daw' }); }; b.querySelector('#exVid').onclick = () => { PX.UI.close(); C.exportImage({ fmt: 'png', scale: 1, what: 'video' }); };
  });
};
C.renderImage = (o) => new Promise(res => {
  const s = clamp(o.scale || 1, 0.1, 4), W = Math.min(10000, Math.round(C.art.w * s)), H = Math.min(10000, Math.round(C.art.h * s)); const hidden = [];
  if (o.transparent) for (const L of C.art.layers) if (L.name === 'Background' && L.vis) { L.vis = false; hidden.push(L); }
  const pend = C.art.layers.filter(l => l.vis && l.url).map(l => C.img(l.url)).filter(i => !i.complete);
  Promise.all(pend.map(i => new Promise(r => { i.addEventListener('load', r, { once: true }); i.addEventListener('error', r, { once: true }); }))).then(() => {
    const cv = document.createElement('canvas'); cv.width = W; cv.height = H; const g = cv.getContext('2d'); C._exporting = true; try { C.drawArt(g, W, H); } finally { C._exporting = false; hidden.forEach(L => L.vis = true); }
    let out = cv; if (o.fmt === 'jpg') { out = document.createElement('canvas'); out.width = W; out.height = H; const g2 = out.getContext('2d'); g2.fillStyle = '#ffffff'; g2.fillRect(0, 0, W, H); g2.drawImage(cv, 0, 0); }
    out.toBlob(res, o.fmt === 'jpg' ? 'image/jpeg' : o.fmt === 'webp' ? 'image/webp' : 'image/png', o.quality || 0.92);
  });
});
C.exportImage = async (o) => { const blob = await C.renderImage(o); if (!blob) { PX.toast('The picture could not be made.', true); return; } const what = o.what || o.fmt; const r = await fetch('https://export.purphex/art?what=' + what + '&name=' + encodeURIComponent(o.name || C.art.name), { method: 'POST', body: blob }); if (!r.ok) PX.toast('Could not hand the picture to Purphex: ' + (await r.text()), true); };
// ---------------- the panels ----------------
const T = C.extraTabs = C.extraTabs || {};
const PRESETS = [['3000x3000', 'Album cover'], ['3000x3000', 'Single artwork'], ['3000x3000', 'Mixtape cover'], ['1080x1080', 'Instagram post'], ['1080x1350', 'Portrait post 4:5'], ['1080x1920', 'Story / TikTok'], ['800x800', 'Profile picture'], ['1280x720', 'YouTube thumbnail'], ['2560x1440', 'YouTube channel banner'], ['1500x500', 'X / Twitter header'], ['1640x624', 'Facebook cover'], ['2480x3508', 'Poster A4'], ['1080x1350', 'Flyer (social)'], ['1920x1080', 'Desktop wallpaper'], ['1080x2340', 'Phone wallpaper'], ['1000x1000', 'Logo']];
T.design = (b) => {
  hint(b, 'The size and shape of your picture, the background, and the guides that help you place things.');
  head(b, 'SIZE'); const g = el('div', 'grid2'); g.style.gridTemplateColumns = 'repeat(2,1fr)'; b.appendChild(g);
  for (const [s, n] of PRESETS) { const c = el('div', 'card' + (s === C.art.w + 'x' + C.art.h ? ' on' : ''), esc(n) + '<small>' + s.replace('x', ' x ') + '</small>'); c.onclick = () => { const [w, h] = s.split('x').map(Number); C.setSize(w, h); }; g.appendChild(c); }
  head(b, 'YOUR OWN SIZE'); const f = el('div', 'row', '<input type="number" id="dsW" min="64" max="8000" style="width:90px"> x <input type="number" id="dsH" min="64" max="8000" style="width:90px"> px'); b.appendChild(f); f.querySelector('#dsW').value = C.art.w; f.querySelector('#dsH').value = C.art.h;
  const r1 = row(b); btn(r1, 'APPLY', () => C.setSize(+f.querySelector('#dsW').value, +f.querySelector('#dsH').value), 'accent'); btn(r1, '⟲ TURN (SWAP WIDTH AND HEIGHT)', () => C.setSize(C.art.h, C.art.w));
  head(b, 'BACKGROUND'); const bgl = C.art.layers.find(l => l.name === 'Background'); const r2 = row(b); btn(r2, 'CHOOSE A BACKGROUND', () => C.openPanel('bg')); btn(r2, bgl && bgl.vis ? 'SEE-THROUGH (HIDE BACKGROUND)' : 'SHOW BACKGROUND', () => { if (!bgl) return; C.begin(); bgl.vis = !bgl.vis; C.commit(); C.renderTab(); });
  head(b, 'GUIDES'); const opt = (k, label) => { const d = el('label', 'chk', '<input type="checkbox"> ' + esc(label)); const i = d.querySelector('input'); i.checked = !!C.view[k]; i.onchange = () => { C.view[k] = i.checked; C.draw(); }; b.appendChild(d); };
  opt('grid', 'Show a grid'); opt('margins', 'Show safe margins'); opt('snapLayers', 'Snap to other layers'); opt('snapGrid', 'Snap to the grid');
  hint(b, 'Shift + click picks several layers (or drag a box on an empty part). Ctrl+G groups them, Ctrl+Shift+G ungroups, Ctrl+C / Ctrl+V copy and paste, Ctrl+A picks everything, Alt while dragging moves freely.');
};
C.setSize = (w, h) => { w = clamp(Math.round(w) || 0, 64, 8000); h = clamp(Math.round(h) || 0, 64, 8000); C.begin(); C.art.w = w; C.art.h = h; C.commit(); C.size(); if (open === 'design') C.renderTab(); };
T.uploads = (b) => {
  hint(b, 'Every picture you have added to Purphex (copies: your own files are not touched). Click one to put it on the picture. You can also drop pictures straight onto the editor.');
  const r1 = row(b); btn(r1, '+ ADD PHOTOS', () => PX.send({ t: 'artImport', multi: true }), 'accent');
  const q = el('input'); q.type = 'text'; q.placeholder = 'Search your uploads'; q.style.width = '100%'; q.style.margin = '6px 0'; b.appendChild(q); const g = el('div', 'grid2 upl'); g.style.gridTemplateColumns = 'repeat(3,1fr)'; b.appendChild(g); g.innerHTML = '<p class="hint">Loading...</p>';
  const fill = () => { const list = (C._uploads || []).filter(u => !q.value || u.name.toLowerCase().includes(q.value.toLowerCase())); g.innerHTML = list.length ? '' : '<p class="hint">' + ((C._uploads || []).length ? 'Nothing matches.' : 'No uploads yet: press ADD PHOTOS or drop pictures here.') + '</p>';
    for (const u of list) { const c = el('div', 'card', '<div class="uth" style="background-image:url(' + u.url + ')"></div><small>' + esc(u.name) + '</small>'); c.title = u.name + ' (' + u.kb + ' KB)'; c.onclick = () => C.addLayer({ type: 'image', name: u.name, url: u.url, fit: 'fit', scale: 0.8, crop: { l: 0, r: 0, t: 0, b: 0 } }); const x = el('button', 'tiny', 'X'); x.title = 'Remove from your uploads (designs that use it will show it as missing)'; x.onclick = (e) => { e.stopPropagation(); PX.UI.confirm('Remove "' + u.name + '" from your uploads? Designs that use it will show it as missing.', () => PX.send({ t: 'artUploadDelete', file: u.file }), 'REMOVE'); }; c.appendChild(x); g.appendChild(c); } };
  q.oninput = fill; C._uplFill = fill; PX.send({ t: 'artUploads' });
};
PX.on('artUploads', m => { C._uploads = m.list || []; if (C._uplFill && open === 'uploads') C._uplFill(); });
PX.on('artImported', () => { if (open === 'uploads') PX.send({ t: 'artUploads' }); });
// the emoji alone; the drawn sticker pack alone
T.stickers = (b) => { hint(b, 'Click one to put it on the picture.'); const g = el('div', 'grid2'); g.style.gridTemplateColumns = 'repeat(7,1fr)'; b.appendChild(g); for (const e of C.lib.EMOJI) { const c = el('div', 'card', '<span style="font-size:24px;font-family:Segoe UI Emoji">' + e + '</span>'); c.onclick = () => C.addLayer({ type: 'sticker', name: 'Sticker ' + e, emoji: e, size: 500 }); g.appendChild(c); } };
T.elements = (b) => { if (C.extraStickers) C.extraStickers(b); };
T.shapes = (b) => { hint(b, 'Click a shape to add it. Change its colour, outline, glow and size on the picture (or in its settings).'); const g = el('div', 'grid2'); g.style.gridTemplateColumns = 'repeat(3,1fr)'; b.appendChild(g);
  for (const [s, n] of C.lib.SHAPES) { const cv = el('canvas'); cv.width = 54; cv.height = 40; const c = el('div', 'card'); c.appendChild(cv); c.appendChild(el('small', '', esc(n))); try { const x = cv.getContext('2d'); x.translate(27, 20); C.lib.shapePath(x, s, 44, s === 'line' ? 4 : 32); x.fillStyle = '#ff50c4'; x.strokeStyle = '#ff50c4'; x.lineWidth = 3; if (s === 'ring') x.stroke(); else x.fill(); } catch (e) { } c.onclick = () => C.addLayer({ type: 'shape', name: n, shape: s, color: '#ff50c4', color2: '#3ce1ff', w: s === 'line' ? 0.5 : 0.3, h: s === 'line' ? 0.008 : s === 'rect' || s === 'roundrect' ? 0.2 : 0.3, stroke: 0, glow: 0 }); g.appendChild(c); } };
// fonts: every font on the PC, your own font files, previews
const custom = []; PX.on('fontList', m => { for (const f of m.list || []) { if (custom.some(c => c.name === f.name)) continue; custom.push(f); try { const ff = new FontFace(f.name, 'url(' + f.url + ')'); ff.load().then(x => { document.fonts.add(x); if (!PX.UI.fonts.includes(f.name)) PX.UI.fonts.unshift(f.name); if (open === 'fonts' || open === 'text') C.renderTab(); }).catch(() => PX.toast('The font "' + f.name + '" could not be read.', true)); } catch (e) { } } if (m.added) PX.toast('Font added: it is at the top of FONTS.'); });
T.fonts = (b) => {
  const L = sel(); hint(b, L && L.type === 'text' ? 'Click a font to use it for "' + L.name + '".' : 'Pick a text layer first, or click a font to add new text in it.');
  const r1 = row(b); btn(r1, '+ ADD YOUR OWN FONT FILE', () => PX.send({ t: 'fontImport' })); if (custom.length) hint(b, 'Your own fonts: ' + custom.map(c => c.name).join(', '));
  const q = el('input'); q.type = 'text'; q.placeholder = 'Search fonts'; q.style.width = '100%'; q.style.margin = '6px 0'; b.appendChild(q); const fl = el('div', 'fontlist'); b.appendChild(fl);
  const fill = () => { fl.innerHTML = ''; const cur = sel(); for (const fn of PX.UI.fonts.filter(x => x.toLowerCase().includes(q.value.toLowerCase()))) { const d = el('div', 'fontcard' + (cur && cur.type === 'text' && cur.font === fn ? ' on' : ''), '<b>Aa</b> ' + esc(fn)); d.style.fontFamily = '"' + fn + '"'; d.onclick = () => { const L2 = sel(); if (L2 && L2.type === 'text') { C.begin(); L2.font = fn; C.commit(); } else C.addLayer(Object.assign(C.lib.textBase(), { text: 'YOUR TEXT', name: fn + ' text', font: fn })); fill(); }; fl.appendChild(d); } };
  q.oninput = fill; fill();
};
// layers, with everything they can do
T.layers = (b) => { hint(b, 'The layers are in the panel on the right (top of the list is in front; drag to change the order; Shift + click on the picture picks several).'); const r1 = row(b); btn(r1, 'GROUP PICKED (Ctrl+G)', C.groupSel); btn(r1, 'UNGROUP', C.ungroupSel); C.sideShow(true); C.sideTab('layers'); };
T.props = (b) => { hint(b, 'The settings of the picked layer are in the panel on the right.'); C.sideShow(true); C.sideTab('props'); };
// text: the styles, saved styles, and fonts are in their own panel
const origText = null;
T.textstyles = null;
// the brand kit: your colours, fonts and logo, kept by Purphex
T.brand = (b) => {
  hint(b, 'Your colours, fonts and logo in one place: click a colour to use it on the picked layer, a font for the picked text, the logo to add it.');
  b.appendChild(el('p', 'hint', 'Loading...')); C.kvGet('brandkit').then(k => { k = k || { colors: [], fonts: [], logos: [] }; b.lastChild.remove();
    head(b, 'COLOURS'); const sw = el('div', 'btnrow'); b.appendChild(sw); k.colors.forEach((c, i) => { const s = el('button', 'swatch'); s.style.background = c; s.title = c + ' (right-click to remove)'; s.onclick = () => { const L = sel(); if (!L) return; C.begin(); if ('color' in L || L.type === 'text' || L.type === 'shape' || L.type === 'vsticker') L.color = c; else if (L.type === 'color') L.color = c; C.commit(); }; s.oncontextmenu = (e) => { e.preventDefault(); k.colors.splice(i, 1); C.kvSet('brandkit', k); C.renderTab(); }; sw.appendChild(s); });
    const ci = el('input'); ci.type = 'color'; ci.value = '#a85cff'; sw.appendChild(ci); btn(sw, '+ ADD COLOUR', () => { k.colors.push(ci.value); C.kvSet('brandkit', k); C.renderTab(); });
    head(b, 'FONTS'); const fr = el('div', 'btnrow'); b.appendChild(fr); k.fonts.forEach((f, i) => { const s = el('button', '', esc(f)); s.style.fontFamily = '"' + f + '"'; s.onclick = () => { const L = sel(); if (L && L.type === 'text') { C.begin(); L.font = f; C.commit(); } else C.addLayer(Object.assign(C.lib.textBase(), { text: 'YOUR TEXT', font: f })); }; s.oncontextmenu = (e) => { e.preventDefault(); k.fonts.splice(i, 1); C.kvSet('brandkit', k); C.renderTab(); }; fr.appendChild(s); });
    const L = sel(); if (L && L.type === 'text' && !k.fonts.includes(L.font)) btn(fr, '+ ADD "' + L.font + '"', () => { k.fonts.push(L.font); C.kvSet('brandkit', k); C.renderTab(); });
    head(b, 'LOGOS'); const lg = el('div', 'grid2 upl'); lg.style.gridTemplateColumns = 'repeat(3,1fr)'; b.appendChild(lg); k.logos.forEach((u, i) => { const c = el('div', 'card', '<div class="uth" style="background-image:url(' + u.url + ')"></div><small>' + esc(u.name) + '</small>'); c.onclick = () => C.addLayer({ type: 'image', name: u.name, url: u.url, fit: 'fit', scale: 0.3, crop: { l: 0, r: 0, t: 0, b: 0 } }); c.oncontextmenu = (e) => { e.preventDefault(); k.logos.splice(i, 1); C.kvSet('brandkit', k); C.renderTab(); }; lg.appendChild(c); });
    if (L && L.type === 'image') btn(row(b), '+ USE THE PICKED PHOTO AS A LOGO', () => { k.logos.push({ url: L.url, name: L.name }); C.kvSet('brandkit', k); C.renderTab(); });
    hint(b, 'Right-click a colour, font or logo to take it out of the kit.'); });
};
// AI tools: only what really works here; the rest says what it would need
T.ai = (b) => {
  const L = sel(), img = L && (L.type === 'image' || L.type === 'cell');
  hint(b, 'These run on this PC. Purphex never fakes an AI result.');
  head(b, 'REMOVE BACKGROUND'); hint(b, 'Takes away a fairly plain background around the subject. Fine-tune with the magic eraser and the brushes in CUTOUT.'); btn(row(b), img ? 'REMOVE THE BACKGROUND OF "' + L.name + '"' : 'PICK A PHOTO FIRST', () => { if (!img) return; C.begin(); L.cut = Object.assign({ tol: 40, feather: 2 }, L.cut || {}, { auto: true }); C.commit(); C.openPanel('cutout'); }, img ? 'accent' : '');
  head(b, 'AUTO ENHANCE'); hint(b, 'Brighter, clearer and a little more colourful in one click (every setting stays editable in ADJUST).'); btn(row(b), img ? 'ENHANCE "' + L.name + '"' : 'ENHANCE THE WHOLE PICTURE', () => { const Tt = img ? L : (C.art.global = C.art.global || {}); C.begin(); Tt.px = Object.assign({}, Tt.px || {}, { clarity: 30, sharpen: 30, shadows: 20, highlights: -10 }); Tt.sat = 12; Tt.contrast = 8; C.commit(); }, 'accent');
  head(b, 'NOT SET UP'); for (const [n, d] of [['Image generation', 'making pictures from a description'], ['Image expansion', 'growing a photo past its edges'], ['Object removal', 'taking a thing out of a photo and filling the gap'], ['Variations', 'new versions of a picture']]) b.appendChild(el('div', 'aino', '<b>' + n + '</b>: ' + d + '. This needs an AI image service with an account and a key; you asked for the key-based generator to be taken out, so it is not built in. Nothing is faked.'));
};
// before and after: hold to see the picture without its adjustments and effects
const strip = (o) => { for (const k of ['bright', 'contrast', 'sat', 'hue', 'filter', 'blur', 'warm', 'fade', 'grain', 'vignette', 'duotone', 'px', 'look']) delete o[k]; };
C.compare = (on) => { if (on && !C._cmpKeep) { C._cmpKeep = C.art; const a = JSON.parse(JSON.stringify(C.art)); strip(a.global = a.global || {}); a.layers.forEach(strip); C.art = a; C.draw(); } else if (!on && C._cmpKeep) { C.art = C._cmpKeep; C._cmpKeep = null; C.draw(); } };
for (const k of ['adjust', 'effects']) { const prev = T[k]; const wrap = (b) => { const cb = el('button', '', '◐ HOLD TO SEE BEFORE'); cb.style.marginBottom = '6px'; cb.onpointerdown = () => C.compare(true); cb.onpointerup = cb.onpointerleave = () => C.compare(false); b.appendChild(cb); if (prev) prev(b); else C._filtersBuiltIn(b); }; if (prev) T[k] = wrap; else C._wrapFilters = wrap; }
// saved text styles, added to the TEXT panel
C.afterTextTab = (b) => {
  head(b, 'YOUR SAVED STYLES'); const box = el('div', 'grid2'); b.appendChild(box); const L = sel();
  C.kvGet('textstyles').then(list => { list = list || []; box.innerHTML = list.length ? '' : '<p class="hint">Style some text, pick it, and press SAVE THIS STYLE.</p>';
    list.forEach((s, i) => { const c = el('div', 'card', esc(s.name)); c.style.fontFamily = '"' + s.style.font + '"'; c.style.color = s.style.color; c.onclick = () => { const L2 = sel(); if (L2 && L2.type === 'text') { C.begin(); Object.assign(L2, s.style); C.commit(); } else C.addLayer(Object.assign(C.lib.textBase(), s.style, { text: 'YOUR TEXT', name: s.name })); }; c.oncontextmenu = (e) => { e.preventDefault(); list.splice(i, 1); C.kvSet('textstyles', list); C.renderTab(); }; c.title = 'Click to use, right-click to remove'; box.appendChild(c); });
    if (L && L.type === 'text') btn(row(b), 'SAVE THIS STYLE ("' + L.name + '")', () => { const keys = Object.keys(C.lib.textBase()).filter(k => k !== 'type' && k !== 'text'); const style = {}; keys.forEach(k => style[k] = L[k]); list.push({ name: L.name, style }); C.kvSet('textstyles', list); C.renderTab(); });
  });
};
// show it when Purphex Photo Editor opens
C.openStart = () => {
  if (document.querySelector('#cvStart')) return; const w = el('div', 'cvstart', '<div class="cvstartbox"><h2>PURPHEX MUSIC <span>PHOTO EDITOR</span></h2><p class="hint">Start from a photo of yours, or from nothing.</p><div class="cvstartgrid"><button id="stPhoto" class="big accent"><span class="ic">🖼️</span>IMPORT A PHOTO</button><button id="stBlank" class="big"><span class="ic">⬜</span>START BLANK</button><button id="stTpl" class="big"><span class="ic">🧩</span>BROWSE TEMPLATES</button><button id="stMine" class="big"><span class="ic">📂</span>OPEN A DESIGN</button></div></div>'); w.id = 'cvStart'; document.getElementById('cover').appendChild(w);
  const close = () => w.remove(); w.querySelector('#stPhoto').onclick = () => { close(); C.act('openPhoto'); }; w.querySelector('#stBlank').onclick = () => { close(); C.begin(); C.art.layers = []; C.art.global = { look: 'none' }; C.lib.bg(C.art, 'color', { color: '#1e1e22' }); C.art.name = 'Untitled design'; C.sel = null; C.commit(); C.openPanel('design'); }; w.querySelector('#stTpl').onclick = () => { close(); C.openWelcome(); }; w.querySelector('#stMine').onclick = () => { close(); C.act('open'); };
};
const prevShow = C.show; C.show = () => { const first = !C._welcomed; C._welcomed = true; prevShow(); build(); C.size(); if (first) C.openStart(); };
})();
