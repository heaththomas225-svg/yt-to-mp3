'use strict';
// =====================================================================
//  PHOTO STUDIO PRO: pages (several artboards in one project), pro colour tools (levels, curves, vibrance, white balance,
//  colour balance, selective colour, noise reduction, straighten), layer effects (outlines, glow, drop and inner shadow,
//  colour and gradient overlays, distress), clipping masks, selection masks (rectangle, ellipse, lasso), the fill bucket,
//  the eyedropper, line and arrow tools, soft brushes with smoothing, rulers and guides that snap, a font library, pro text
//  styles and warps, more sizes, quick starts, and safer export (transparency warnings, export every page).
// =====================================================================
(() => {
const C = PX.Cover, clamp = PX.clamp, uid = PX.uid;
const $c = (s) => document.querySelector('#cover ' + s);
const esc = (s) => String(s == null ? '' : s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const sel = () => C.art && C.art.layers.find(l => l.id === C.sel);
const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; };
const mk = (w, h) => { const c = document.createElement('canvas'); c.width = Math.max(1, Math.round(w)); c.height = Math.max(1, Math.round(h)); return c; };
const toast = (t, bad) => PX.toast(t, bad);
const pin = (b, where) => { const st = $c('#cvStage'); if (!st || !b) return; let x = st.scrollLeft + 10; const pop = document.querySelector('#cvPop'); if (pop && !pop.classList.contains('hidden')) { const pr = pop.getBoundingClientRect(), sr = st.getBoundingClientRect(); x = Math.max(x, pr.right - sr.left + st.scrollLeft + 8); } if (where === 'bottom') { b.style.left = x + 'px'; b.style.top = (st.scrollTop + st.clientHeight - b.offsetHeight - 10) + 'px'; } else { b.style.left = x + 'px'; b.style.top = (st.scrollTop + (C.view && C.view.rulers ? 28 : 10) + (where === 'top2' ? 46 : 0)) + 'px'; } };

// ---------------------------------------------------------------- more sizes
const S = C.lib.SIZES; for (const s of [['2560x1440', 'YouTube banner 2560 x 1440'], ['800x800', 'Profile picture 800'], ['1500x500', 'X / Twitter header'], ['1640x624', 'Facebook cover'], ['2480x520', 'SoundCloud banner'], ['1000x1000', 'Logo 1000'], ['2400x3600', 'Poster 2:3'], ['3840x2160', 'Wallpaper 4K'], ['1170x2532', 'Phone wallpaper'], ['1080x566', 'Promo post (landscape)']]) if (!S.some(x => x[0] === s[0])) S.push(s);

// ================================================================= PAGES
// The page you are on lives in art.layers / w / h / global like before; the others wait in art.pages (the current one is a stub).
const pg = {
  ensure() { const a = C.art; if (!a.pages || !a.pages.length) { a.pages = [{ id: uid('p'), name: 'Page 1' }]; a.page = 0; } return a; },
  stash(a) { const p = a.pages[a.page]; p.w = a.w; p.h = a.h; p.layers = a.layers; p.global = a.global; },
  go(i) { const a = pg.ensure(); if (i === a.page || i < 0 || i >= a.pages.length) return; C.begin(); pg.stash(a); const n = a.pages[i]; a.w = n.w; a.h = n.h; a.layers = n.layers; a.global = n.global || { look: 'none' }; a.pages[i] = { id: n.id, name: n.name }; a.page = i; C.sel = null; C.commit(); C.size(); },
  add(dup) { const a = pg.ensure(); C.begin(); pg.stash(a); const cur = a.pages[a.page]; const n = { id: uid('p'), name: 'Page ' + (a.pages.length + 1) };
    if (dup) { const copy = JSON.parse(JSON.stringify({ layers: a.layers, global: a.global })); copy.layers.forEach(l => l.id = uid('l')); a.layers = copy.layers; a.global = copy.global; n.name = cur.name + ' copy'; }
    else { a.layers = []; a.global = { look: 'none' }; C.lib.bg(a, 'color', { color: '#141418' }); }
    a.pages[a.page] = { id: cur.id, name: cur.name, w: cur.w, h: cur.h, layers: cur.layers, global: cur.global }; a.pages.splice(a.page + 1, 0, n); a.page++; C.sel = null; C.commit(); C.size(); },
  del() { const a = pg.ensure(); if (a.pages.length < 2) { toast('A design needs at least one page.'); return; } PX.UI.confirm('Delete "' + a.pages[a.page].name + '"? (UNDO brings it back)', () => { C.begin(); a.pages.splice(a.page, 1); const i = Math.min(a.page, a.pages.length - 1); const n = a.pages[i]; a.w = n.w; a.h = n.h; a.layers = n.layers; a.global = n.global || { look: 'none' }; a.pages[i] = { id: n.id, name: n.name }; a.page = i; C.sel = null; C.commit(); C.size(); }, 'DELETE PAGE'); },
  rename() { const a = pg.ensure(); const p = a.pages[a.page]; PX.UI.modal('<h3>Rename page</h3><div class="f"><input id="pgN" type="text"></div><div class="row"><button id="mNo">CANCEL</button><button id="pgOk" class="accent">RENAME</button></div>', (b) => { const n = b.querySelector('#pgN'); n.value = p.name; n.focus(); b.querySelector('#mNo').onclick = PX.UI.close; b.querySelector('#pgOk').onclick = () => { C.begin(); p.name = n.value.trim() || p.name; C.commit(); PX.UI.close(); }; }); },
  // each page drawn the same way it exports (used by EXPORT ALL PAGES)
  async exportAll(o) { const a = pg.ensure(); const start = a.page; const n = a.pages.length; for (let i = 0; i < n; i++) { if (i !== a.page) { pg.stash(a); const t = a.pages[i]; a.pages[a.page] = { id: a.pages[a.page].id, name: a.pages[a.page].name, w: a.w, h: a.h, layers: a.layers, global: a.global }; a.w = t.w; a.h = t.h; a.layers = t.layers; a.global = t.global || { look: 'none' }; a.page = i; } await C.exportImage(Object.assign({}, o, { name: (o.name || a.name) + ' - ' + (i + 1) + ' ' + (a.pages[i].name || '') })); }
    if (a.page !== start) { const t = a.pages[start]; a.pages[a.page] = { id: a.pages[a.page].id, name: a.pages[a.page].name, w: a.w, h: a.h, layers: a.layers, global: a.global }; a.w = t.w; a.h = t.h; a.layers = t.layers; a.global = t.global || { look: 'none' }; a.pages[start] = { id: t.id, name: t.name }; a.page = start; } C.render(); C.size(); toast('Exported ' + n + ' page' + (n === 1 ? '' : 's') + '.'); },
};
C.pages = pg;
function pageBar() {
  const st = $c('#cvStage'); if (!st) return; let b = document.getElementById('proPages'); if (!b) { b = el('div', 'propages'); b.id = 'proPages'; st.appendChild(b); }
  const a = C.art; const list = a.pages && a.pages.length ? a.pages : [{ name: 'Page 1' }]; const cur = a.page || 0;
  const key = list.map(p => p.name).join('|') + '#' + cur; pin(b, 'bottom'); if (b._k === key) return; b._k = key; b.innerHTML = '';
  pin(b, 'bottom'); list.forEach((p, i) => { const x = el('button', i === cur ? 'on' : '', (i + 1) + '<small>' + esc(p.name || '') + '</small>'); x.title = i === cur ? 'This page (double-click to rename)' : 'Go to ' + p.name; x.onclick = () => pg.go(i); x.ondblclick = () => { if (i === cur) pg.rename(); }; b.appendChild(x); });
  const ad = el('button', 'add', '+'); ad.title = 'Add a blank page (same size)'; ad.onclick = () => pg.add(false); b.appendChild(ad);
  const du = el('button', 'add', '⧉'); du.title = 'Duplicate this page'; du.onclick = () => pg.add(true); b.appendChild(du);
  if (list.length > 1) { const de = el('button', 'add', '🗑'); de.title = 'Delete this page'; de.onclick = () => pg.del(); b.appendChild(de); }
}

// ================================================================= PRO COLOUR (levels, curves, vibrance, white balance, colour balance, HSL, denoise)
const proOn = (P) => !!P && !!(P.levels || P.curve || P.vib || P.temp || P.tint || P.cb || P.hsl || P.denoise || P.straighten);
// a curve through the points (monotone, so it never folds back) as a 256-entry table
function curveLut(pts) { const p = (pts && pts.length >= 2 ? pts : [[0, 0], [1, 1]]).slice().sort((a, b) => a[0] - b[0]); const n = p.length, lut = new Uint8ClampedArray(256); const d = [], m = new Array(n).fill(0);
  for (let i = 0; i < n - 1; i++) d.push((p[i + 1][1] - p[i][1]) / Math.max(1e-6, p[i + 1][0] - p[i][0])); m[0] = d[0]; m[n - 1] = d[n - 2]; for (let i = 1; i < n - 1; i++) m[i] = d[i - 1] * d[i] <= 0 ? 0 : (d[i - 1] + d[i]) / 2;
  for (let i = 0; i < n - 1; i++) { if (d[i] === 0) { m[i] = m[i + 1] = 0; continue; } const a = m[i] / d[i], b = m[i + 1] / d[i], h = a * a + b * b; if (h > 9) { const t = 3 / Math.sqrt(h); m[i] = t * a * d[i]; m[i + 1] = t * b * d[i]; } }
  for (let v = 0; v < 256; v++) { const x = v / 255; let i = 0; while (i < n - 2 && x > p[i + 1][0]) i++; if (x <= p[0][0]) { lut[v] = p[0][1] * 255; continue; } if (x >= p[n - 1][0]) { lut[v] = p[n - 1][1] * 255; continue; } const hh = p[i + 1][0] - p[i][0], t = (x - p[i][0]) / hh, t2 = t * t, t3 = t2 * t; const y = (2 * t3 - 3 * t2 + 1) * p[i][1] + (t3 - 2 * t2 + t) * hh * m[i] + (-2 * t3 + 3 * t2) * p[i + 1][1] + (t3 - t2) * hh * m[i + 1]; lut[v] = clamp(y, 0, 1) * 255; }
  return lut; }
const HSLR = [['red', 'Reds', 0], ['orange', 'Oranges', 30], ['yellow', 'Yellows', 60], ['green', 'Greens', 120], ['aqua', 'Aquas', 180], ['blue', 'Blues', 225], ['purple', 'Purples', 275], ['magenta', 'Magentas', 320]];
function rgb2hsl(r, g, b) { const mx = Math.max(r, g, b), mn = Math.min(r, g, b), l = (mx + mn) / 2; if (mx === mn) return [0, 0, l]; const d = mx - mn, s = l > 0.5 ? d / (2 - mx - mn) : d / (mx + mn); let h = mx === r ? (g - b) / d + (g < b ? 6 : 0) : mx === g ? (b - r) / d + 2 : (r - g) / d + 4; return [h * 60, s, l]; }
function hsl2rgb(h, s, l) { if (!s) return [l, l, l]; const q = l < 0.5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q, f = (t) => { t = ((t % 1) + 1) % 1; return t < 1 / 6 ? p + (q - p) * 6 * t : t < 0.5 ? q : t < 2 / 3 ? p + (q - p) * (2 / 3 - t) * 6 : p; }; return [f(h / 360 + 1 / 3), f(h / 360), f(h / 360 - 1 / 3)]; }
// the tables (levels, curves) are built once per setting
function proTables(P) {
  const lv = P.levels || {}, inB = (lv.inB || 0) / 255, inW = (lv.inW == null ? 255 : lv.inW) / 255, gm = lv.gamma || 1, oB = (lv.outB || 0) / 255, oW = (lv.outW == null ? 255 : lv.outW) / 255;
  const cv = P.curve || {}, cm = curveLut(cv.m), cr = curveLut(cv.r), cg = curveLut(cv.g), cb = curveLut(cv.b); const T = [new Uint8ClampedArray(256), new Uint8ClampedArray(256), new Uint8ClampedArray(256)];
  for (let v = 0; v < 256; v++) { let x = clamp((v / 255 - inB) / Math.max(0.004, inW - inB), 0, 1); x = Math.pow(x, 1 / gm); x = oB + x * (oW - oB); const y = cm[Math.round(x * 255)]; T[0][v] = cr[y]; T[1][v] = cg[y]; T[2][v] = cb[y]; }
  return T;
}
function applyPro(id, P) {
  const d = id.data, T = proTables(P), vib = (P.vib || 0) / 100, temp = (P.temp || 0) / 100, tint = (P.tint || 0) / 100, cb = P.cb, hsl = P.hsl, anyHsl = hsl && HSLR.some(r => hsl[r[0]] && (hsl[r[0]][0] || hsl[r[0]][1] || hsl[r[0]][2]));
  const sh = cb && cb.sh || [0, 0, 0], md = cb && cb.mid || [0, 0, 0], hi = cb && cb.hi || [0, 0, 0];
  for (let i = 0; i < d.length; i += 4) {
    if (!d[i + 3]) continue; let r = T[0][d[i]] / 255, g = T[1][d[i + 1]] / 255, b = T[2][d[i + 2]] / 255;
    if (temp || tint) { r += temp * 0.12; b -= temp * 0.12; g -= tint * 0.1; r += tint * 0.04; b += tint * 0.04; }
    if (cb) { const l = (r + g + b) / 3, ws = clamp(1 - l * 2.2, 0, 1), wh = clamp(l * 2.2 - 1.2, 0, 1), wm = 1 - Math.abs(l - 0.5) * 2; r += (sh[0] * ws + md[0] * wm + hi[0] * wh) / 300; g += (sh[1] * ws + md[1] * wm + hi[1] * wh) / 300; b += (sh[2] * ws + md[2] * wm + hi[2] * wh) / 300; }
    if (vib) { const mx = Math.max(r, g, b), av = (r + g + b) / 3, amt = (1 - (mx - Math.min(r, g, b))) * vib * 1.4; r += (r - av) * amt; g += (g - av) * amt; b += (b - av) * amt; }
    if (anyHsl) { r = clamp(r, 0, 1); g = clamp(g, 0, 1); b = clamp(b, 0, 1); let [h, s, l] = rgb2hsl(r, g, b); if (s > 0.02) { let dh = 0, ds = 0, dl = 0; for (const [k, , c] of HSLR) { const a = hsl[k]; if (!a) continue; let dist = Math.abs(h - c); if (dist > 180) dist = 360 - dist; const w = clamp(1 - dist / 40, 0, 1); if (w) { dh += a[0] * w; ds += a[1] * w; dl += a[2] * w; } } if (dh || ds || dl) { h = (h + dh * 0.6 + 360) % 360; s = clamp(s * (1 + ds / 100), 0, 1); l = clamp(l + dl / 100 * 0.5 * s, 0, 1); [r, g, b] = hsl2rgb(h, s, l); } } }
    d[i] = r * 255; d[i + 1] = g * 255; d[i + 2] = b * 255;
  }
}
// noise reduction: smooth where neighbours are alike, keep edges
function denoise(c, amt) { const W = c.width, H = c.height, g = c.getContext('2d'); const o = g.getImageData(0, 0, W, H); const bl = mk(W, H), bg = bl.getContext('2d'); bg.filter = 'blur(' + (0.6 + amt / 30) + 'px)'; bg.drawImage(c, 0, 0); const b = bg.getImageData(0, 0, W, H).data, d = o.data, thr = 6 + amt * 0.5;
  for (let i = 0; i < d.length; i += 4) { const df = Math.abs(d[i] - b[i]) + Math.abs(d[i + 1] - b[i + 1]) + Math.abs(d[i + 2] - b[i + 2]), w = Math.exp(-df / thr) * Math.min(1, amt / 50); d[i] += (b[i] - d[i]) * w; d[i + 1] += (b[i + 1] - d[i + 1]) * w; d[i + 2] += (b[i + 2] - d[i + 2]) * w; } g.putImageData(o, 0, 0); }
function straighten(c, deg) { const W = c.width, H = c.height, a = Math.abs(deg) * Math.PI / 180, s = Math.max((W * Math.cos(a) + H * Math.sin(a)) / W, (W * Math.sin(a) + H * Math.cos(a)) / H); const t = mk(W, H); t.getContext('2d').drawImage(c, 0, 0); const g = c.getContext('2d'); g.save(); g.setTransform(1, 0, 0, 1, 0, 0); g.clearRect(0, 0, W, H); g.translate(W / 2, H / 2); g.rotate(deg * Math.PI / 180); g.scale(s, s); g.drawImage(t, -W / 2, -H / 2); g.restore(); }
function runPro(c, P) { if (P.straighten) straighten(c, P.straighten); const g = c.getContext('2d'); if (P.levels || P.curve || P.vib || P.temp || P.tint || P.cb || P.hsl) { const id = g.getImageData(0, 0, c.width, c.height); applyPro(id, P); g.putImageData(id, 0, 0); } if (P.denoise) denoise(c, P.denoise); }
C.proLib = { curveLut, applyPro, proOn, runPro };
// photos: done once on a copy of the photo (kept until a setting changes)
const proCache = new Map();
const prevPx = C.pxSource; C.pxSource = (L, im) => {
  const base = prevPx ? prevPx(L, im) : null; if (!proOn(L.pro)) return base; const src = base ? base.c : im, s0 = base ? base.s : 1;
  const key = L.id + '|' + (im.src || '').length + '|' + JSON.stringify(L.pro) + '|' + (base ? 'b' + base.c.width + JSON.stringify(L.px || 0) + JSON.stringify(L.cut || 0).length : ''); let e = proCache.get(L.id); if (e && e.k === key) return e.v;
  const sw = src.naturalWidth || src.width, sh = src.naturalHeight || src.height, f = Math.min(1, 2400 / Math.max(sw, sh)); const c = mk(sw * f, sh * f); c.getContext('2d').drawImage(src, 0, 0, c.width, c.height); runPro(c, L.pro);
  const v = { c, s: s0 * f }; proCache.set(L.id, { k: key, v }); if (proCache.size > 24) proCache.delete(proCache.keys().next().value); return v; };
const prevPost = C.postFx; C.postFx = (g, W, H, G) => { if (prevPost) prevPost(g, W, H, G); if (proOn(G.pro)) runPro(g.canvas, G.pro); };

// ================================================================= LAYER EFFECTS, CLIPPING, SELECTION MASKS (one layer at a time, off screen)
const fxOn = (F) => !!F && !!((F.outlines && F.outlines.some(o => o.size > 0)) || (F.glow && F.glow.size > 0) || (F.shadow && F.shadow.on) || (F.inner && F.inner.size > 0) || (F.overlay && F.overlay.amt > 0) || (F.grad && F.grad.amt > 0) || F.distress > 0);
const needsOff = (L) => fxOn(L.fx) || L.clip || L.smask;
function silhouette(src, color) { const c = mk(src.width, src.height), g = c.getContext('2d'); g.drawImage(src, 0, 0); g.globalCompositeOperation = 'source-in'; g.fillStyle = color; g.fillRect(0, 0, c.width, c.height); return c; }
let speck = null; function distressTile() { if (speck) return speck; speck = mk(256, 256); const g = speck.getContext('2d'); let s = 99; const r = () => { s = (s * 16807) % 2147483647; return s / 2147483647; }; for (let i = 0; i < 1400; i++) { g.globalAlpha = 0.3 + r() * 0.7; g.beginPath(); g.ellipse(r() * 256, r() * 256, 0.5 + r() * 3, 0.5 + r() * 2, r() * 3, 0, 7); g.fill(); } for (let i = 0; i < 40; i++) { g.globalAlpha = 0.5; g.lineWidth = 0.5 + r(); g.beginPath(); const x = r() * 256, y = r() * 256; g.moveTo(x, y); g.lineTo(x + (r() - 0.5) * 60, y + (r() - 0.5) * 20); g.strokeStyle = '#000'; g.stroke(); } return speck; }
function applyFx(O, F, k) {   // O: the layer drawn alone; returns the layer with its effects
  const W = O.width, H = O.height, g = O.getContext('2d');
  if (F.distress > 0) { g.save(); g.globalCompositeOperation = 'destination-out'; g.globalAlpha = clamp(F.distress / 100, 0, 1); const sc = Math.max(1, k * 3); g.scale(sc, sc); g.fillStyle = g.createPattern(distressTile(), 'repeat'); g.fillRect(0, 0, W / sc, H / sc); g.restore(); }
  if (F.overlay && F.overlay.amt > 0) { g.save(); g.globalCompositeOperation = 'source-atop'; g.globalAlpha = F.overlay.amt / 100; g.fillStyle = F.overlay.color || '#ff50c4'; g.fillRect(0, 0, W, H); g.restore(); }
  if (F.grad && F.grad.amt > 0) { g.save(); g.globalCompositeOperation = 'source-atop'; g.globalAlpha = F.grad.amt / 100; const a = (F.grad.angle == null ? 90 : F.grad.angle) * Math.PI / 180, R = Math.hypot(W, H) / 2, gr = g.createLinearGradient(W / 2 - Math.cos(a) * R, H / 2 - Math.sin(a) * R, W / 2 + Math.cos(a) * R, H / 2 + Math.sin(a) * R); gr.addColorStop(0, F.grad.c1 || '#a85cff'); gr.addColorStop(1, F.grad.c2 || '#3ce1ff'); g.fillStyle = gr; g.fillRect(0, 0, W, H); g.restore(); }
  if (F.inner && F.inner.size > 0) { const I = mk(W, H), ig = I.getContext('2d'); ig.fillStyle = F.inner.color || '#000'; ig.fillRect(0, 0, W, H); ig.globalCompositeOperation = 'destination-out'; ig.drawImage(O, 0, 0); const B = mk(W, H), bg = B.getContext('2d'); bg.filter = 'blur(' + F.inner.size * k + 'px)'; bg.drawImage(I, 0, 0); bg.filter = 'none'; bg.globalCompositeOperation = 'destination-in'; bg.drawImage(O, 0, 0); g.save(); g.globalAlpha = clamp((F.inner.amt == null ? 80 : F.inner.amt) / 100, 0, 1); g.globalCompositeOperation = 'source-atop'; g.drawImage(B, 0, 0); g.restore(); }
  const out = mk(W, H), og = out.getContext('2d');
  if (F.shadow && F.shadow.on) { const sCol = silhouette(O, F.shadow.color || '#000'); og.save(); og.globalAlpha = clamp((F.shadow.amt == null ? 70 : F.shadow.amt) / 100, 0, 1); og.filter = 'blur(' + (F.shadow.blur == null ? 20 : F.shadow.blur) * k + 'px)'; og.drawImage(sCol, (F.shadow.x == null ? 20 : F.shadow.x) * k, (F.shadow.y == null ? 30 : F.shadow.y) * k); og.restore(); }
  if (F.glow && F.glow.size > 0) { const sCol = silhouette(O, F.glow.color || '#a85cff'); og.save(); og.filter = 'blur(' + F.glow.size * k + 'px)'; const n = Math.max(1, Math.round((F.glow.amt == null ? 60 : F.glow.amt) / 34)); for (let i = 0; i < n; i++) og.drawImage(sCol, 0, 0); og.restore(); }
  if (F.outlines) for (const o of F.outlines.slice().sort((a, b) => (b.size || 0) - (a.size || 0))) { if (!(o.size > 0)) continue; const sCol = silhouette(O, o.color || '#ffffff'), r = o.size * k, steps = r > 12 ? 32 : 16; for (const rr of r > 8 ? [r, r * 0.66, r * 0.33] : [r]) for (let i = 0; i < steps; i++) { const a = i / steps * Math.PI * 2; og.drawImage(sCol, Math.cos(a) * rr, Math.sin(a) * rr); } }
  og.drawImage(O, 0, 0); return out;
}
function maskCanvas(m, W, H) {   // a selection kept with the layer: white where the layer shows
  const c = mk(W, H), g = c.getContext('2d'); g.fillStyle = '#fff'; selPath(g, m, W, H); g.fill(m.kind === 'lasso' ? 'nonzero' : 'nonzero');
  let out = c; if (m.feather > 0) { const f = mk(W, H), fg = f.getContext('2d'); fg.filter = 'blur(' + m.feather * W / (C.art.w || W) + 'px)'; fg.drawImage(c, 0, 0); out = f; }
  if (m.invert) { const iv = mk(W, H), ig = iv.getContext('2d'); ig.fillStyle = '#fff'; ig.fillRect(0, 0, W, H); ig.globalCompositeOperation = 'destination-out'; ig.drawImage(out, 0, 0); out = iv; }
  return out;
}
function selPath(g, m, W, H) { g.beginPath(); if (m.kind === 'rect') g.rect(m.x * W, m.y * H, m.w * W, m.h * H); else if (m.kind === 'ellipse') g.ellipse((m.x + m.w / 2) * W, (m.y + m.h / 2) * H, Math.abs(m.w) * W / 2, Math.abs(m.h) * H / 2, 0, 0, Math.PI * 2); else if (m.pts && m.pts.length) { m.pts.forEach((p, i) => i ? g.lineTo(p[0] * W, p[1] * H) : g.moveTo(p[0] * W, p[1] * H)); g.closePath(); } }
function drawOff(L, W, H, k) { const O = mk(W, H), g = O.getContext('2d'); try { C.lib.drawLayer(g, L, W, H, k); } catch (e) { } g.setTransform(1, 0, 0, 1, 0, 0);
  let R = fxOn(L.fx) ? applyFx(O, L.fx, k) : O;
  if (L.smask) { const M = maskCanvas(L.smask, W, H), rg = R.getContext('2d'); rg.save(); rg.globalCompositeOperation = 'destination-in'; rg.drawImage(M, 0, 0); rg.restore(); }
  return R; }
const origDrawArt = C.drawArt;
C.drawArt = (g, W, H) => {
  const Ls = C.art.layers; if (!Ls.some(needsOff)) return origDrawArt(g, W, H);
  g.setTransform(1, 0, 0, 1, 0, 0); g.clearRect(0, 0, W, H); const k = W / C.art.w;
  for (let i = 0; i < Ls.length; i++) {
    const L = Ls[i]; if (!L.vis) continue;
    const clipped = []; for (let j = i + 1; j < Ls.length && Ls[j].clip; j++) clipped.push(Ls[j]);
    if (L.clip && i > 0) continue;   // drawn with the layer it is clipped to
    if (!needsOff(L) && !clipped.some(q => q.vis)) { g.save(); g.globalAlpha = clamp(L.opacity, 0, 1); g.globalCompositeOperation = L.blend || 'source-over'; try { C.lib.drawLayer(g, L, W, H, k); } catch (e) { } g.restore(); continue; }
    const B = drawOff(L, W, H, k), bg = B.getContext('2d');
    for (const q of clipped) { if (!q.vis) continue; const Q = drawOff(q, W, H, k), qg = Q.getContext('2d'); qg.globalCompositeOperation = 'destination-in'; qg.drawImage(B, 0, 0); bg.save(); bg.globalAlpha = clamp(q.opacity, 0, 1); bg.globalCompositeOperation = q.blend && q.blend !== 'source-over' ? q.blend : 'source-atop'; bg.drawImage(Q, 0, 0); bg.restore(); }
    g.save(); g.globalAlpha = clamp(L.opacity, 0, 1); g.globalCompositeOperation = L.blend || 'source-over'; g.drawImage(B, 0, 0); g.restore();
  }
  const G = C.art.global || {}; if (G.bright || G.contrast || G.sat || G.hue || G.filter || G.blur || G.warm) { const cv = mk(W, H); cv.getContext('2d').drawImage(g.canvas, 0, 0); g.clearRect(0, 0, W, H); g.filter = C.lib.imageFilter(G, k); g.drawImage(cv, 0, 0); g.filter = 'none'; }
  g.save(); C.lib.finish(g, 0, 0, W, H, Object.assign({}, G, { warm: 0 }), k); g.restore();
  if (C.postFx) C.postFx(g, W, H, G);
};

// ================================================================= LINES AND ARROWS (a layer you can move, turn and resize)
C.boxTypes = (C.boxTypes || []).concat(['pline']);
C.extraDraw = C.extraDraw || {}; C.extraDraw.pline = (g, L, W, H, k) => { const x = (L.dx || 0.3) * W / 2, y = (L.dy || 0) * H / 2, w = (L.width || 16) * k; g.strokeStyle = g.fillStyle = L.color || '#ffffff'; g.lineWidth = w; g.lineCap = L.round === false ? 'butt' : 'round'; if (L.dash) g.setLineDash([w * 2.2, w * 1.6]);
  const head = (L.head || 'none'), hl = w * (L.headSize || 4); const ang = Math.atan2(2 * y, 2 * x), shorten = head !== 'none' ? hl * 0.7 : 0, ex = x - Math.cos(ang) * shorten, ey = y - Math.sin(ang) * shorten, sx = head === 'both' ? -x + Math.cos(ang) * shorten : -x, sy = head === 'both' ? -y + Math.sin(ang) * shorten : -y;
  g.beginPath(); g.moveTo(sx, sy); if (L.curve) g.quadraticCurveTo(-Math.sin(ang) * L.curve * W / 200, Math.cos(ang) * L.curve * W / 200, ex, ey); else g.lineTo(ex, ey); g.stroke(); g.setLineDash([]);
  const tip = (px, py, a) => { g.beginPath(); g.moveTo(px, py); g.lineTo(px - Math.cos(a - 0.45) * hl, py - Math.sin(a - 0.45) * hl); g.lineTo(px - Math.cos(a + 0.45) * hl, py - Math.sin(a + 0.45) * hl); g.closePath(); g.fill(); };
  if (head === 'end' || head === 'both') tip(x, y, ang); if (head === 'both') tip(-x, -y, ang + Math.PI); };
C.extraBox = C.extraBox || {}; C.extraBox.pline = (L, W, H, k, cx, cy, s) => { const w = Math.abs((L.dx || 0.3) * W) * s, h = Math.abs((L.dy || 0) * H) * s, pad = (L.width || 16) * k * s * 3; return { cx, cy, w: w + pad, h: h + pad }; };

// ================================================================= TOOLS: selection, fill bucket, eyedropper, line, arrow
C.selection = null;   // {kind:'rect'|'ellipse'|'lasso', x,y,w,h | pts} in 0..1 of the canvas
const PRO_TOOLS = ['selRect', 'selEllipse', 'selLasso', 'fill', 'eyedrop', 'line', 'arrow'];
C.fillTol = 32; C.fillColor = '#a85cff';
function composite(maxSide) { const f = Math.min(1, maxSide / Math.max(C.art.w, C.art.h)), W = Math.round(C.art.w * f), H = Math.round(C.art.h * f), c = mk(W, H); C._exporting = true; try { C.drawArt(c.getContext('2d'), W, H); } finally { C._exporting = false; } return c; }
C.pickColorAt = (fx, fy) => { const c = composite(1200), d = c.getContext('2d').getImageData(clamp(Math.floor(fx * c.width), 0, c.width - 1), clamp(Math.floor(fy * c.height), 0, c.height - 1), 1, 1).data; return '#' + [d[0], d[1], d[2]].map(v => v.toString(16).padStart(2, '0')).join(''); };
C.applyPicked = (hex) => { C.fillColor = hex; if (C.brush) C.brush.color = hex; const L = sel(); if (L) { C.begin(); if (L.type === 'text' || L.type === 'shape' || L.type === 'color' || L.type === 'pline' || L.type === 'vsticker') L.color = hex; else if (L.type === 'pstk' || L.type === 'pbg') L.c1 = hex; else if (L.type === 'gradient') L.c1 = hex; C.commit(); } toast('Picked colour ' + hex + (L ? ' (put on "' + L.name + '")' : '') + '. It is now the fill and brush colour too.'); };
// flood fill on the picture as you see it: makes a new layer of the filled area
C.floodFill = (fx, fy, hex, tol) => {
  const c = composite(1600), W = c.width, H = c.height, id = c.getContext('2d').getImageData(0, 0, W, H), d = id.data, sx = clamp(Math.floor(fx * W), 0, W - 1), sy = clamp(Math.floor(fy * H), 0, H - 1), si = (sy * W + sx) * 4, t = tol == null ? C.fillTol : tol;
  const r0 = d[si], g0 = d[si + 1], b0 = d[si + 2], a0 = d[si + 3], seen = new Uint8Array(W * H), stack = [sx + sy * W]; const same = (p) => { const i = p * 4; return Math.abs(d[i] - r0) + Math.abs(d[i + 1] - g0) + Math.abs(d[i + 2] - b0) + Math.abs(d[i + 3] - a0) <= t * 3; };
  let count = 0; while (stack.length) { const p = stack.pop(); if (seen[p]) continue; let x = p % W; const y = (p - x) / W; let l = x; while (l > 0 && !seen[y * W + l - 1] && same(y * W + l - 1)) l--; let r = x; while (r < W - 1 && !seen[y * W + r + 1] && same(y * W + r + 1)) r++; for (let i = l; i <= r; i++) { const q = y * W + i; seen[q] = 1; count++; if (y > 0 && !seen[q - W] && same(q - W)) stack.push(q - W); if (y < H - 1 && !seen[q + W] && same(q + W)) stack.push(q + W); } }
  const m = mk(W, H), mg = m.getContext('2d'), od = mg.createImageData(W, H), o = od.data, col = [parseInt(hex.slice(1, 3), 16), parseInt(hex.slice(3, 5), 16), parseInt(hex.slice(5, 7), 16)]; for (let p = 0; p < W * H; p++) if (seen[p]) { o[p * 4] = col[0]; o[p * 4 + 1] = col[1]; o[p * 4 + 2] = col[2]; o[p * 4 + 3] = 255; } mg.putImageData(od, 0, 0);
  if (C.selection) { mg.globalCompositeOperation = 'destination-in'; mg.drawImage(maskCanvas(C.selection, W, H), 0, 0); }
  const n = C.addLayer({ type: 'image', name: 'Fill', url: m.toDataURL('image/png'), fit: 'fill', crop: { l: 0, r: 0, t: 0, b: 0 } }); return { layer: n, pixels: count, of: W * H };
};
C.fillSelection = (hex) => { if (!C.selection) { toast('Make a selection first (rectangle, ellipse or lasso).'); return null; } const W = Math.min(1600, C.art.w), H = Math.round(C.art.h * W / C.art.w), m = mk(W, H), g = m.getContext('2d'); g.drawImage(maskCanvas(C.selection, W, H), 0, 0); g.globalCompositeOperation = 'source-in'; g.fillStyle = hex || C.fillColor; g.fillRect(0, 0, W, H); return C.addLayer({ type: 'image', name: 'Filled selection', url: m.toDataURL('image/png'), fit: 'fill', crop: { l: 0, r: 0, t: 0, b: 0 } }); };
C.copySelection = () => { if (!C.selection) return null; const L = sel(); const W = Math.min(2400, C.art.w), H = Math.round(C.art.h * W / C.art.w), c = mk(W, H), g = c.getContext('2d');
  if (L && L.vis) { g.save(); g.globalAlpha = clamp(L.opacity, 0, 1); try { C.lib.drawLayer(g, L, W, H, W / C.art.w); } catch (e) { } g.restore(); g.setTransform(1, 0, 0, 1, 0, 0); } else g.drawImage(composite(2400), 0, 0, W, H);
  g.globalCompositeOperation = 'destination-in'; g.drawImage(maskCanvas(C.selection, W, H), 0, 0); return C.addLayer({ type: 'image', name: (L ? L.name : 'Picture') + ' (selection)', url: c.toDataURL('image/png'), fit: 'fill', crop: { l: 0, r: 0, t: 0, b: 0 } }); };
C.maskToSelection = (how) => { const L = sel(); if (!L) { toast('Pick the layer to mask first.'); return; } if (!C.selection) return; C.begin(); L.smask = Object.assign(JSON.parse(JSON.stringify(C.selection)), { invert: how === 'cut', feather: C.selFeather || 0 }); C.commit(); toast(how === 'cut' ? 'The selection is cut out of "' + L.name + '". Remove it any time in Properties.' : '"' + L.name + '" now only shows inside the selection. Remove the mask any time in Properties.'); };
function selBar() {
  const st = $c('#cvStage'); if (!st) return; let b = document.getElementById('proSelBar'); const on = !!C.selection;
  if (!on) { if (b) b.classList.add('hidden'); return; } if (!b) { b = el('div', 'proselbar'); b.id = 'proSelBar'; st.appendChild(b); }
  b.classList.remove('hidden'); pin(b, 'top'); if (b._k === JSON.stringify(C.selection) + (C.sel || '')) return; b._k = JSON.stringify(C.selection) + (C.sel || ''); b.innerHTML = '';
  const add = (t, tip, fn) => { const x = el('button', '', t); x.title = tip; x.onclick = fn; b.appendChild(x); return x; };
  b.appendChild(el('b', '', 'SELECTION'));
  add('KEEP INSIDE', 'The picked layer only shows inside the selection (a mask you can remove)', () => C.maskToSelection('keep'));
  add('CUT OUT', 'Hide the selected part of the picked layer (a mask you can remove)', () => C.maskToSelection('cut'));
  add('FILL', 'Fill the selection with the fill colour (as a new layer)', () => C.fillSelection());
  add('COPY TO LAYER', 'Copy the selected part of the picked layer (or the whole picture) to a new layer', () => C.copySelection());
  add('INVERT', 'Select everything except this', () => { const s = C.selection; s.inv = !s.inv; toast(s.inv ? 'Inverted: the actions now use everything outside the shape.' : 'Back to inside the shape.'); });
  const fl = el('label', 'hint', 'Feather <input type="range" min="0" max="120" value="' + (C.selFeather || 0) + '" style="width:80px;vertical-align:middle">'); fl.querySelector('input').oninput = (e) => { C.selFeather = +e.target.value; }; b.appendChild(fl);
  add('DESELECT', 'Drop the selection (Esc)', () => { C.selection = null; C.draw(); });
}
// actions use the selection with its invert / feather settings
const selNow = () => C.selection && Object.assign({}, C.selection, { invert: !!C.selection.inv, feather: C.selFeather || 0 });
const _mask = maskCanvas; // (selection with invert + feather for fills / copies)
C.fillSelection = ((f) => (hex) => { const s = C.selection; if (!s) return f(hex); C.selection = selNow(); try { return f(hex); } finally { C.selection = s; } })(C.fillSelection);
C.copySelection = ((f) => () => { const s = C.selection; if (!s) { toast('Make a selection first.'); return null; } C.selection = selNow(); try { return f(); } finally { C.selection = s; } })(C.copySelection);
C.maskToSelection = ((f) => (how) => { const s = C.selection; if (!s) { toast('Make a selection first.'); return; } C.selection = Object.assign({}, s); if (s.inv) how = how === 'cut' ? 'keep' : 'cut'; try { f(how); } finally { C.selection = s; } })(C.maskToSelection);

function toolDown(e) {
  if (!PRO_TOOLS.includes(C.tool)) return; const cv = $c('#cvCanvas'); if (e.button !== 0 || e.target !== cv) return; e.stopPropagation(); e.preventDefault();
  const r = cv.getBoundingClientRect(), fx = (e.clientX - r.left) / r.width, fy = (e.clientY - r.top) / r.height, at = (ev) => [clamp((ev.clientX - r.left) / r.width, -0.2, 1.2), clamp((ev.clientY - r.top) / r.height, -0.2, 1.2)];
  if (C.tool === 'eyedrop') { C.applyPicked(C.pickColorAt(fx, fy)); return; }
  if (C.tool === 'fill') { const res = C.floodFill(fx, fy, C.fillColor); toast('Filled ' + Math.round(res.pixels / res.of * 100) + '% of the picture on a new layer "Fill". Tolerance: ' + C.fillTol + '.'); return; }
  if (C.tool === 'line' || C.tool === 'arrow') { let tmp = null; const mv = (ev) => { const [qx, qy] = at(ev); C._linePrev = [fx, fy, qx, qy]; C.draw(); }; const up = (ev) => { removeEventListener('mousemove', mv); removeEventListener('mouseup', up); C._linePrev = null; const [qx, qy] = at(ev); if (Math.hypot(qx - fx, (qy - fy) * C.art.h / C.art.w) < 0.01) { C.draw(); return; } let ex = qx, ey = qy; if (ev.shiftKey) { const a = Math.round(Math.atan2((qy - fy) * C.art.h, (qx - fx) * C.art.w) / (Math.PI / 4)) * Math.PI / 4, len = Math.hypot((qx - fx) * C.art.w, (qy - fy) * C.art.h); ex = fx + Math.cos(a) * len / C.art.w; ey = fy + Math.sin(a) * len / C.art.h; }
      tmp = C.addLayer({ type: 'pline', name: C.tool === 'arrow' ? 'Arrow' : 'Line', x: (fx + ex) - 1, y: (fy + ey) - 1, dx: ex - fx, dy: ey - fy, width: C.lineW || 16, color: C.fillColor, head: C.tool === 'arrow' ? 'end' : 'none', headSize: 4 }); }; addEventListener('mousemove', mv); addEventListener('mouseup', up); return; }
  // selections
  const kind = C.tool === 'selRect' ? 'rect' : C.tool === 'selEllipse' ? 'ellipse' : 'lasso'; const s = kind === 'lasso' ? { kind, pts: [[fx, fy]] } : { kind, x: fx, y: fy, w: 0, h: 0 }; C.selection = s;
  const mv = (ev) => { const [qx, qy] = at(ev); if (kind === 'lasso') s.pts.push([qx, qy]); else { let w = qx - fx, h = qy - fy; if (ev.shiftKey) { const m = Math.max(Math.abs(w * C.art.w), Math.abs(h * C.art.h)); w = Math.sign(w || 1) * m / C.art.w; h = Math.sign(h || 1) * m / C.art.h; } s.x = Math.min(fx, fx + w); s.y = Math.min(fy, fy + h); s.w = Math.abs(w); s.h = Math.abs(h); } C.draw(); };
  const up = () => { removeEventListener('mousemove', mv); removeEventListener('mouseup', up); if ((kind === 'lasso' && s.pts.length < 3) || (kind !== 'lasso' && (s.w < 0.004 || s.h < 0.004))) C.selection = null; C.draw(); }; addEventListener('mousemove', mv); addEventListener('mouseup', up);
}

// ================================================================= RULERS AND GUIDES
C.view = C.view || {}; if (C.view.rulers == null) { try { C.view.rulers = localStorage.getItem('pxRulers') === '1'; } catch (e) { C.view.rulers = false; } }
const guides = () => { const a = C.art; a.guides = a.guides || { v: [], h: [] }; return a.guides; };
function rulers() {
  const st = $c('#cvStage'), cv = $c('#cvCanvas'); if (!st || !cv) return; let rh = document.getElementById('proRulerH'), rv = document.getElementById('proRulerV');
  if (!C.view.rulers) { if (rh) rh.classList.add('hidden'); if (rv) rv.classList.add('hidden'); return; }
  if (!rh) { rh = mk(10, 18); rh.id = 'proRulerH'; rh.className = 'proruler'; rv = mk(18, 10); rv.id = 'proRulerV'; rv.className = 'proruler v'; st.appendChild(rh); st.appendChild(rv); for (const [r, dir] of [[rh, 'h'], [rv, 'v']]) r.onmousedown = (e) => newGuide(e, dir); }
  rh.classList.remove('hidden'); rv.classList.remove('hidden');
  const cr = cv.getBoundingClientRect(), sr = st.getBoundingClientRect(), L0 = cr.left - sr.left + st.scrollLeft, T0 = cr.top - sr.top + st.scrollTop;
  rh.style.left = L0 + 'px'; rh.style.top = Math.max(st.scrollTop, T0 - 20) + 'px'; rh.width = Math.round(cr.width); rh.style.width = cr.width + 'px'; rv.style.top = T0 + 'px'; rv.style.left = Math.max(st.scrollLeft, L0 - 20) + 'px'; rv.height = Math.round(cr.height); rv.style.height = cr.height + 'px';
  const tick = (c, len, artLen, horiz) => { const g = c.getContext('2d'); g.clearRect(0, 0, c.width, c.height); g.fillStyle = '#0d0a16'; g.fillRect(0, 0, c.width, c.height); g.fillStyle = '#9a8cc0'; g.strokeStyle = '#6a5c90'; g.font = '9px Segoe UI'; const px = len / artLen, steps = [10, 25, 50, 100, 250, 500, 1000], step = steps.find(s => s * px > 45) || 2000;
    for (let v = 0; v <= artLen; v += step / 5) { const p = Math.round(v * px) + 0.5, big = Math.round(v) % step === 0; g.beginPath(); if (horiz) { g.moveTo(p, 18); g.lineTo(p, big ? 4 : 13); } else { g.moveTo(18, p); g.lineTo(big ? 4 : 13, p); } g.stroke(); if (big) { if (horiz) g.fillText(Math.round(v), p + 2, 10); else { g.save(); g.translate(10, p + 2); g.rotate(Math.PI / 2); g.fillText(Math.round(v), 0, 0); g.restore(); } } } };
  tick(rh, cr.width, C.art.w, true); tick(rv, cr.height, C.art.h, false);
}
function newGuide(e, dir) { e.preventDefault(); const cv = $c('#cvCanvas'), r = cv.getBoundingClientRect(); const gd = guides(); C.begin(); const list = dir === 'h' ? gd.h : gd.v; list.push(0); const i = list.length - 1; dragGuide(list, i, dir, r); }
function dragGuide(list, i, dir, r) { const mv = (ev) => { list[i] = Math.round(dir === 'h' ? (ev.clientY - r.top) / r.height * C.art.h : (ev.clientX - r.left) / r.width * C.art.w); C.draw(); }; const up = (ev) => { removeEventListener('mousemove', mv); removeEventListener('mouseup', up); const out = dir === 'h' ? (ev.clientY < r.top || ev.clientY > r.bottom) : (ev.clientX < r.left || ev.clientX > r.right); if (out) list.splice(i, 1); C.commit(); }; addEventListener('mousemove', mv); addEventListener('mouseup', up); }
// guides can be moved (or dragged off the picture to remove them) with the move tool
function guideDown(e) { if (C.tool !== 'move' || !C.art.guides) return; const cv = $c('#cvCanvas'); if (e.target !== cv || e.button !== 0) return; const r = cv.getBoundingClientRect(), gd = C.art.guides, fx = (e.clientX - r.left) / r.width * C.art.w, fy = (e.clientY - r.top) / r.height * C.art.h, th = 5 * C.art.w / r.width;
  for (const [list, dir, v] of [[gd.v, 'v', fx], [gd.h, 'h', fy]]) { const i = list.findIndex(x => Math.abs(x - v) < th); if (i >= 0) { e.stopPropagation(); e.preventDefault(); C.begin(); dragGuide(list, i, dir, r); return; } } }
const prevSnap = C.snapHook; C.snapHook = (L, ev) => { if (prevSnap) prevSnap(L, ev); if ((ev && ev.altKey) || !C.art.guides) return; const gd = C.art.guides, W = C.art.w, H = C.art.h, b = C.box(L, W, H, true), th = Math.max(W, H) * 0.008, lines = C._snapLines || { xs: [], ys: [] };
  const best = (vals, mine) => { let r = null; for (const m of mine) for (const v of vals) { const d = v - m; if (Math.abs(d) < th && (!r || Math.abs(d) < Math.abs(r[0]))) r = [d, v]; } return r; };
  if (!lines.xs.length) { const sx = best(gd.v, [b.cx, b.cx - b.w / 2, b.cx + b.w / 2]); if (sx) { L.x = (L.x || 0) + sx[0] / (W / 2); lines.xs.push(sx[1]); } }
  if (!lines.ys.length) { const sy = best(gd.h, [b.cy, b.cy - b.h / 2, b.cy + b.h / 2]); if (sy) { L.y = (L.y || 0) + sy[0] / (H / 2); lines.ys.push(sy[1]); } }
  C._snapLines = lines.xs.length || lines.ys.length ? lines : null; };

// ================================================================= drawn over the picture: guides, the selection, the line being drawn
const ants = 0;
const prevAfter = C.afterDraw; C.afterDraw = () => {
  if (prevAfter) prevAfter(); const cv = $c('#cvCanvas'); if (!cv || !C.art) return; const g = cv.getContext('2d'), W = cv.width, H = cv.height, k = W / C.art.w; g.save();
  if (C.art.guides && C.view.guidesShow !== false) { g.strokeStyle = 'rgba(60,225,255,0.9)'; g.lineWidth = 1; for (const x of C.art.guides.v) { g.beginPath(); g.moveTo(x * k + 0.5, 0); g.lineTo(x * k + 0.5, H); g.stroke(); } for (const y of C.art.guides.h) { g.beginPath(); g.moveTo(0, y * k + 0.5); g.lineTo(W, y * k + 0.5); g.stroke(); } }
  if (C.selection) { g.lineWidth = 1.5; const s = C.selection; for (const [col, off] of [['#000', 0], ['#fff', 8]]) { g.strokeStyle = col; g.setLineDash([8, 8]); g.lineDashOffset = -ants - off; selPath(g, s, W, H); g.stroke(); } g.setLineDash([]); if (s.inv) { g.fillStyle = 'rgba(255,80,196,0.10)'; g.beginPath(); g.rect(0, 0, W, H); selPath(g, s, W, H); g.fill('evenodd'); } }
  if (C._linePrev) { const [a, b, c2, d] = C._linePrev; g.strokeStyle = C.fillColor; g.lineWidth = Math.max(2, (C.lineW || 16) * k); g.lineCap = 'round'; g.beginPath(); g.moveTo(a * W, b * H); g.lineTo(c2 * W, d * H); g.stroke(); }
  g.restore(); rulers(); pageBar(); selBar(); proBar();
};

// ================================================================= the PRO tool strip (on the app bar)
const TOOLS = [['move', '↖', 'Move / pick (V)'], ['selRect', '⬚', 'Rectangle selection (M). Shift: square'], ['selEllipse', '◯', 'Ellipse selection (E). Shift: circle'], ['selLasso', '➰', 'Lasso selection (L): draw around what you want'], ['fill', '🪣', 'Fill bucket (G): fills an area of similar colour with the fill colour'], ['eyedrop', '💧', 'Eyedropper (I): pick a colour from the picture'], ['line', '╱', 'Line (U). Shift: straight angles'], ['arrow', '➜', 'Arrow (A). Shift: straight angles']];
function proBar() {
  const app = document.querySelector('#cover .cvapp'); if (!app) return; let b = document.getElementById('proBar');
  if (!b) { b = el('span', 'probar'); b.id = 'proBar'; const anchor = app.querySelector('#cvExport'); app.insertBefore(b, anchor);
    for (const [k, ic, tip] of TOOLS) { const x = el('button', '', ic); x.dataset.t = k; x.title = tip; x.onclick = () => { C.tool = k; if (k !== 'move') C.openPanel && C.openPanel(null); C.render(); }; b.appendChild(x); }
    const fc = el('input'); fc.type = 'color'; fc.id = 'proFillC'; fc.title = 'Fill / line colour (the eyedropper sets it)'; fc.oninput = () => { C.fillColor = fc.value; }; b.appendChild(fc);
    const ru = el('button', '', '📏'); ru.id = 'proRulerBtn'; ru.title = 'Rulers and guides: drag from a ruler to make a guide; layers snap to guides; drag a guide off the picture to remove it'; ru.onclick = () => { C.view.rulers = !C.view.rulers; try { localStorage.setItem('pxRulers', C.view.rulers ? '1' : '0'); } catch (e) { } C.draw(); }; b.appendChild(ru);
  }
  b.querySelectorAll('[data-t]').forEach(x => x.classList.toggle('on', x.dataset.t === C.tool)); const fc = b.querySelector('#proFillC'); if (fc && document.activeElement !== fc) fc.value = C.fillColor; b.querySelector('#proRulerBtn').classList.toggle('on', !!C.view.rulers);
  let opt = document.getElementById('proToolOpt'); const want = C.tool === 'fill' ? 'fill' : (C.tool === 'line' || C.tool === 'arrow') ? 'line' : '';
  if (!want) { if (opt) opt.remove(); return; } if (opt && opt.dataset.k === want) { pin(opt, C.selection ? 'top2' : 'top'); return; } if (opt) opt.remove(); const st = $c('#cvStage'); opt = el('div', 'protoolopt'); opt.id = 'proToolOpt'; opt.dataset.k = want; st.appendChild(opt);
  if (want === 'fill') { opt.innerHTML = '<b>FILL</b> Tolerance <input type="range" min="0" max="160" value="' + C.fillTol + '"><span>' + C.fillTol + '</span><span class="hint">Click an area: it is filled on a new layer (inside the selection if there is one).</span>'; const r = opt.querySelector('input'), v = opt.querySelector('span'); r.oninput = () => { C.fillTol = +r.value; v.textContent = r.value; }; }
  else { opt.innerHTML = '<b>' + (C.tool === 'arrow' ? 'ARROW' : 'LINE') + '</b> Width <input type="range" min="2" max="120" value="' + (C.lineW || 16) + '"><span>' + (C.lineW || 16) + '</span><span class="hint">Drag on the picture. Shift keeps it straight. Change the ends, dashes and curve in Properties.</span>'; const r = opt.querySelector('input'), v = opt.querySelector('span'); r.oninput = () => { C.lineW = +r.value; v.textContent = r.value; }; }
}
addEventListener('keydown', (e) => { if (!PX.UI || PX.UI.tool !== 'cover' || e.ctrlKey || e.altKey || e.metaKey) return; const t = e.target; if (t && (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA' || t.tagName === 'SELECT' || t.isContentEditable)) return; if (document.querySelector('#modal:not(.hidden), #cvStart')) return;
  if (e.key === 'Escape' && C.selection) { C.selection = null; C.draw(); e.preventDefault(); return; }
  const map = { v: 'move', m: 'selRect', e: 'selEllipse', l: 'selLasso', g: 'fill', i: 'eyedrop', u: 'line', a: 'arrow' }; const k = map[e.key.toLowerCase()]; if (k && !e.shiftKey) { C.tool = k; C.render(); e.preventDefault(); } }, true);

// ================================================================= PROPERTIES: layer effects, clipping, masks, line settings, pro text
const prevProps = C.extraProps; C.extraProps = (L, f, num, col, box) => {
  if (prevProps) prevProps(L, f, num, col, box);
  const sec = (title, open) => { const d = el('details', 'prosec'); if (open) d.open = true; d.innerHTML = '<summary>' + title + '</summary>'; box.appendChild(d); return d; };
  const rng = (host, label, get, set, min, max, step) => { const d = el('div', 'f', '<label>' + label + '</label><input type="range" min="' + min + '" max="' + max + '" step="' + (step || 1) + '"><input type="number" min="' + min + '" max="' + max + '" step="' + (step || 1) + '">'); host.appendChild(d); const r = d.querySelector('[type=range]'), n = d.querySelector('[type=number]'); r.value = n.value = get() || 0; r.onpointerdown = C.begin; r.oninput = () => { set(+r.value); n.value = r.value; C.draw(); }; r.onchange = () => C.commit(); n.onchange = () => { C.begin(); set(clamp(+n.value, min, max)); C.commit(); }; return d; };
  const clr = (host, label, get, set) => { const d = el('div', 'f', '<label>' + label + '</label><input type="color">'); host.appendChild(d); const i = d.querySelector('input'); i.value = (get() || '#ffffff').slice(0, 7); i.oninput = () => { C.begin(); set(i.value); C.draw(); }; i.onchange = () => C.commit(); };
  const pick = (host, label, get, set, opts) => { const d = el('div', 'f', '<label>' + label + '</label><select>' + opts.map(o => '<option value="' + o[0] + '">' + o[1] + '</option>').join('') + '</select>'); host.appendChild(d); const s = d.querySelector('select'); s.value = String(get() == null ? opts[0][0] : get()); s.onchange = () => { C.begin(); set(s.value); C.commit(); }; };
  if (L.type === 'pline') { const s = sec('LINE', true); clr(s, 'Colour', () => L.color, v => L.color = v); rng(s, 'Width', () => L.width, v => L.width = v, 1, 200); pick(s, 'Ends', () => L.head, v => L.head = v, [['none', 'Plain line'], ['end', 'Arrow at the end'], ['both', 'Arrows at both ends']]); rng(s, 'Arrow size', () => L.headSize || 4, v => L.headSize = v, 2, 10, 0.5); rng(s, 'Curve', () => L.curve, v => L.curve = v, -100, 100); const d = el('div', 'f', '<label>Dashed</label><input type="checkbox" style="justify-self:start">'); s.appendChild(d); const c = d.querySelector('input'); c.checked = !!L.dash; c.onchange = () => { C.begin(); L.dash = c.checked; C.commit(); }; }
  if (L.type === 'text') { const s = sec('PRO TEXT', false);
    pick(s, 'Fill', () => L.fillKind || (L.grad ? 'grad' : 'solid'), v => { L.fillKind = v; L.grad = v === 'grad'; }, [['solid', 'Solid colour'], ['grad', 'Gradient'], ['chrome', 'Chrome'], ['gold', 'Gold / metallic'], ['gloss', 'Glossy']]);
    pick(s, 'Warp', () => L.warp || 'none', v => L.warp = v, [['none', 'None'], ['wave', 'Wave'], ['rise', 'Rise'], ['bulge', 'Bulge'], ['flag', 'Flag'], ['circle', 'Circle (text on a path)']]); rng(s, 'Warp amount', () => L.warpAmt == null ? 50 : L.warpAmt, v => L.warpAmt = v, 0, 100);
    rng(s, 'Second outline', () => L.outline2, v => L.outline2 = v, 0, 80); clr(s, 'Second outline colour', () => L.outline2Color || '#ff50c4', v => L.outline2Color = v); rng(s, 'Emboss', () => L.emboss, v => L.emboss = v, 0, 100); }
  if (!(C.boxTypes || []).includes(L.type) && !['draw'].includes(L.type)) return;
  // layer effects
  const F = () => { L.fx = L.fx || {}; return L.fx; }; const fx = L.fx || {}; const s = sec('LAYER EFFECTS' + (fxOn(fx) ? ' ●' : ''), fxOn(fx));
  const o1 = () => { const f2 = F(); f2.outlines = f2.outlines || [{ size: 0, color: '#ffffff' }, { size: 0, color: '#a85cff' }]; return f2.outlines; };
  rng(s, 'Outline', () => (fx.outlines && fx.outlines[0] || {}).size, v => o1()[0].size = v, 0, 80); clr(s, 'Outline colour', () => (fx.outlines && fx.outlines[0] || {}).color || '#ffffff', v => o1()[0].color = v);
  rng(s, 'Outer outline', () => (fx.outlines && fx.outlines[1] || {}).size, v => o1()[1].size = v, 0, 120); clr(s, 'Outer outline colour', () => (fx.outlines && fx.outlines[1] || {}).color || '#a85cff', v => o1()[1].color = v);
  rng(s, 'Glow', () => (fx.glow || {}).size, v => { F().glow = Object.assign({ color: '#a85cff', amt: 60 }, F().glow, { size: v }); }, 0, 150); clr(s, 'Glow colour', () => (fx.glow || {}).color || '#a85cff', v => { F().glow = Object.assign({ size: 30, amt: 60 }, F().glow, { color: v }); }); rng(s, 'Glow strength', () => (fx.glow || {}).amt == null ? 60 : fx.glow.amt, v => { F().glow = Object.assign({ size: 30, color: '#a85cff' }, F().glow, { amt: v }); }, 10, 100);
  const sh = () => { F().shadow = Object.assign({ on: true, x: 20, y: 30, blur: 20, amt: 70, color: '#000000' }, F().shadow); return F().shadow; };
  const shd = el('div', 'f', '<label>Drop shadow</label><input type="checkbox" style="justify-self:start">'); s.appendChild(shd); const shc = shd.querySelector('input'); shc.checked = !!(fx.shadow && fx.shadow.on); shc.onchange = () => { C.begin(); sh().on = shc.checked; C.commit(); };
  rng(s, 'Shadow distance X', () => (fx.shadow || {}).x == null ? 20 : fx.shadow.x, v => sh().x = v, -200, 200); rng(s, 'Shadow distance Y', () => (fx.shadow || {}).y == null ? 30 : fx.shadow.y, v => sh().y = v, -200, 200); rng(s, 'Shadow softness', () => (fx.shadow || {}).blur == null ? 20 : fx.shadow.blur, v => sh().blur = v, 0, 150); rng(s, 'Shadow strength', () => (fx.shadow || {}).amt == null ? 70 : fx.shadow.amt, v => sh().amt = v, 0, 100); clr(s, 'Shadow colour', () => (fx.shadow || {}).color || '#000000', v => sh().color = v);
  rng(s, 'Inner shadow', () => (fx.inner || {}).size, v => { F().inner = Object.assign({ color: '#000000', amt: 80 }, F().inner, { size: v }); }, 0, 100); clr(s, 'Inner shadow colour', () => (fx.inner || {}).color || '#000000', v => { F().inner = Object.assign({ size: 10, amt: 80 }, F().inner, { color: v }); });
  rng(s, 'Colour overlay', () => (fx.overlay || {}).amt, v => { F().overlay = Object.assign({ color: '#ff50c4' }, F().overlay, { amt: v }); }, 0, 100); clr(s, 'Overlay colour', () => (fx.overlay || {}).color || '#ff50c4', v => { F().overlay = Object.assign({ amt: 60 }, F().overlay, { color: v }); });
  rng(s, 'Gradient overlay', () => (fx.grad || {}).amt, v => { F().grad = Object.assign({ c1: '#a85cff', c2: '#3ce1ff', angle: 90 }, F().grad, { amt: v }); }, 0, 100); clr(s, 'Gradient from', () => (fx.grad || {}).c1 || '#a85cff', v => { F().grad = Object.assign({ amt: 70, c2: '#3ce1ff', angle: 90 }, F().grad, { c1: v }); }); clr(s, 'Gradient to', () => (fx.grad || {}).c2 || '#3ce1ff', v => { F().grad = Object.assign({ amt: 70, c1: '#a85cff', angle: 90 }, F().grad, { c2: v }); }); rng(s, 'Gradient angle', () => (fx.grad || {}).angle == null ? 90 : fx.grad.angle, v => { F().grad = Object.assign({ amt: 70, c1: '#a85cff', c2: '#3ce1ff' }, F().grad, { angle: v }); }, 0, 360);
  rng(s, 'Distress (worn look)', () => fx.distress, v => F().distress = v, 0, 100);
  const rowB = el('div', 'btnrow'); s.appendChild(rowB); const cl = el('button', '', 'CLEAR EFFECTS'); cl.onclick = () => { C.begin(); delete L.fx; C.commit(); }; rowB.appendChild(cl);
  // clipping and masks
  const m = sec('MASKS AND CLIPPING' + (L.clip || L.smask ? ' ●' : ''), !!(L.clip || L.smask));
  const cd = el('div', 'f', '<label>Clip to the layer below</label><input type="checkbox" style="justify-self:start">'); m.appendChild(cd); const cc = cd.querySelector('input'); cc.checked = !!L.clip; cc.title = 'This layer only shows where the layer under it is (put a photo inside text, for example)'; cc.onchange = () => { C.begin(); L.clip = cc.checked; C.commit(); };
  if (L.smask) { m.appendChild(el('p', 'hint', 'This layer has a ' + L.smask.kind + ' mask' + (L.smask.invert ? ' (the shape is cut out)' : ' (it shows inside the shape)') + '.')); rng(m, 'Mask feather', () => L.smask.feather, v => L.smask.feather = v, 0, 200); const r2 = el('div', 'btnrow'); m.appendChild(r2); const iv = el('button', '', 'INVERT MASK'); iv.onclick = () => { C.begin(); L.smask.invert = !L.smask.invert; C.commit(); }; r2.appendChild(iv); const rm = el('button', '', 'REMOVE MASK'); rm.onclick = () => { C.begin(); delete L.smask; C.commit(); }; r2.appendChild(rm); const ed = el('button', '', 'MASK TO SELECTION'); ed.onclick = () => { C.selection = JSON.parse(JSON.stringify(L.smask)); delete C.selection.invert; delete C.selection.feather; C.draw(); }; r2.appendChild(ed); }
  else m.appendChild(el('p', 'hint', 'Masks: make a selection (rectangle M, ellipse E or lasso L on the toolbar), then press KEEP INSIDE or CUT OUT.'));
};

// ================================================================= PRO ADJUST (in the Adjust panel): levels, curves, vibrance, white balance, colour balance, selective colour, noise, straighten
function proAdjust(b) {
  const L = sel(), tgt = L && L.type === 'image' ? L : (C.art.global = C.art.global || {}), who = L && L.type === 'image' ? 'the picked photo' : 'the whole picture'; const P = () => { tgt.pro = tgt.pro || {}; return tgt.pro; }; const pro = tgt.pro || {};
  const box = el('div', 'proadj'); b.appendChild(box); box.appendChild(el('div', 'hint', '<b>PRO ADJUST</b> for ' + who + (L && L.type === 'image' ? '' : ' (pick a photo layer to adjust only that)')));
  const rng = (label, get, set, min, max, step) => { const d = el('div', 'f', '<label>' + label + '</label><input type="range" min="' + min + '" max="' + max + '" step="' + (step || 1) + '"><input type="number" min="' + min + '" max="' + max + '" step="' + (step || 1) + '">'); box.appendChild(d); const r = d.querySelector('[type=range]'), n = d.querySelector('[type=number]'); r.value = n.value = get(); r.onpointerdown = C.begin; r.oninput = () => { set(+r.value); n.value = r.value; C.draw(); }; r.onchange = () => C.commit(); n.onchange = () => { C.begin(); set(clamp(+n.value, min, max)); C.commit(); }; };
  const lv = () => { P().levels = P().levels || { inB: 0, inW: 255, gamma: 1, outB: 0, outW: 255 }; return P().levels; }, l0 = pro.levels || {};
  box.appendChild(el('div', 'hint', '<b>Levels</b>')); rng('Black point', () => l0.inB || 0, v => lv().inB = v, 0, 250); rng('White point', () => l0.inW == null ? 255 : l0.inW, v => lv().inW = v, 5, 255); rng('Midtones (gamma)', () => l0.gamma || 1, v => lv().gamma = v, 0.2, 3, 0.01); rng('Output black', () => l0.outB || 0, v => lv().outB = v, 0, 200); rng('Output white', () => l0.outW == null ? 255 : l0.outW, v => lv().outW = v, 55, 255);
  box.appendChild(el('div', 'hint', '<b>Curves</b> drag the points; click the line to add one; double-click a point to remove it')); curveEditor(box, P, pro);
  box.appendChild(el('div', 'hint', '<b>Colour</b>')); rng('Vibrance', () => pro.vib || 0, v => P().vib = v, -100, 100); rng('Temperature (cool / warm)', () => pro.temp || 0, v => P().temp = v, -100, 100); rng('Tint (green / magenta)', () => pro.tint || 0, v => P().tint = v, -100, 100);
  const cbv = (zone, i) => (pro.cb && pro.cb[zone] || [0, 0, 0])[i], cbs = (zone, i, v) => { P().cb = P().cb || {}; P().cb[zone] = P().cb[zone] || [0, 0, 0]; P().cb[zone][i] = v; };
  box.appendChild(el('div', 'hint', '<b>Colour balance</b>')); for (const [z, zn] of [['sh', 'Shadows'], ['mid', 'Midtones'], ['hi', 'Highlights']]) { rng(zn + ': cyan / red', () => cbv(z, 0), v => cbs(z, 0, v), -100, 100); rng(zn + ': magenta / green', () => cbv(z, 1), v => cbs(z, 1, v), -100, 100); rng(zn + ': yellow / blue', () => cbv(z, 2), v => cbs(z, 2, v), -100, 100); }
  box.appendChild(el('div', 'hint', '<b>Selective colour (HSL)</b>')); const hs = el('div', 'f', '<label>Colour range</label><select>' + HSLR.map(r => '<option value="' + r[0] + '">' + r[1] + '</option>').join('') + '</select>'); box.appendChild(hs); const hsel = hs.querySelector('select'); hsel.value = C._hslR || 'red'; hsel.onchange = () => { C._hslR = hsel.value; C.renderTab(); };
  const hr = C._hslR || 'red', hv = (i) => (pro.hsl && pro.hsl[hr] || [0, 0, 0])[i], hset = (i, v) => { P().hsl = P().hsl || {}; P().hsl[hr] = P().hsl[hr] || [0, 0, 0]; P().hsl[hr][i] = v; };
  rng('Hue shift', () => hv(0), v => hset(0, v), -100, 100); rng('Saturation', () => hv(1), v => hset(1, v), -100, 100); rng('Lightness', () => hv(2), v => hset(2, v), -100, 100);
  box.appendChild(el('div', 'hint', '<b>Clean up</b>')); rng('Noise reduction', () => pro.denoise || 0, v => P().denoise = v, 0, 100); rng('Straighten (degrees)', () => pro.straighten || 0, v => P().straighten = v, -15, 15, 0.1);
  const r = el('div', 'btnrow'); box.appendChild(r); const rs = el('button', '', 'RESET PRO ADJUST'); rs.onclick = () => { C.begin(); delete tgt.pro; C.commit(); }; r.appendChild(rs);
}
function curveEditor(host, P, pro) {
  const wrap = el('div', 'procurve'); host.appendChild(wrap); const tabs = el('div', 'btnrow'); wrap.appendChild(tabs); let ch = C._curveCh || 'm';
  const cv = mk(220, 220); cv.style.cssText = 'width:220px;height:220px;background:#0b0912;border:1px solid #3a2a5a;border-radius:6px;cursor:crosshair;display:block'; wrap.appendChild(cv);
  for (const [k, n, c] of [['m', 'RGB', '#fff'], ['r', 'RED', '#ff5a5a'], ['g', 'GREEN', '#4cff7a'], ['b', 'BLUE', '#5aa8ff']]) { const x = el('button', ch === k ? 'on' : '', n); x.style.color = c; x.onclick = () => { ch = C._curveCh = k; tabs.querySelectorAll('button').forEach(q => q.classList.toggle('on', q === x)); draw(); }; tabs.appendChild(x); }
  const pts = () => ((pro.curve || {})[ch]) || [[0, 0], [1, 1]];
  const draw = () => { const g = cv.getContext('2d'); g.clearRect(0, 0, 220, 220); g.strokeStyle = '#2a2040'; for (let i = 1; i < 4; i++) { g.beginPath(); g.moveTo(i * 55, 0); g.lineTo(i * 55, 220); g.moveTo(0, i * 55); g.lineTo(220, i * 55); g.stroke(); } g.strokeStyle = '#444'; g.beginPath(); g.moveTo(0, 220); g.lineTo(220, 0); g.stroke(); const lut = curveLut(pts()); g.strokeStyle = { m: '#fff', r: '#ff5a5a', g: '#4cff7a', b: '#5aa8ff' }[ch]; g.lineWidth = 2; g.beginPath(); for (let v = 0; v < 256; v++) { const x = v / 255 * 220, y = 220 - lut[v] / 255 * 220; v ? g.lineTo(x, y) : g.moveTo(x, y); } g.stroke(); g.fillStyle = '#ff50c4'; for (const p of pts()) { g.beginPath(); g.arc(p[0] * 220, 220 - p[1] * 220, 5, 0, 7); g.fill(); } };
  const setPts = (list) => { P().curve = P().curve || {}; P().curve[ch] = list; pro = P(); };
  cv.onmousedown = (e) => { const r = cv.getBoundingClientRect(), at = (ev) => [clamp((ev.clientX - r.left) / r.width, 0, 1), clamp(1 - (ev.clientY - r.top) / r.height, 0, 1)]; const [x, y] = at(e); C.begin(); let list = pts().map(p => p.slice()); let i = list.findIndex(p => Math.hypot(p[0] - x, p[1] - y) < 0.05); if (i < 0) { list.push([x, y]); list.sort((a, b) => a[0] - b[0]); i = list.findIndex(p => p[0] === x && p[1] === y); } setPts(list); draw(); C.draw();
    const mv = (ev) => { const [qx, qy] = at(ev); const l2 = pts(); const lo = i > 0 ? l2[i - 1][0] + 0.01 : 0, hi = i < l2.length - 1 ? l2[i + 1][0] - 0.01 : 1; l2[i] = [clamp(qx, lo, hi), qy]; setPts(l2); draw(); C.draw(); }; const up = () => { removeEventListener('mousemove', mv); removeEventListener('mouseup', up); C.commit(); }; addEventListener('mousemove', mv); addEventListener('mouseup', up); };
  cv.ondblclick = (e) => { const r = cv.getBoundingClientRect(), x = (e.clientX - r.left) / r.width, y = 1 - (e.clientY - r.top) / r.height; const list = pts().slice(); const i = list.findIndex(p => Math.hypot(p[0] - x, p[1] - y) < 0.05); if (i > 0 && i < list.length - 1) { C.begin(); list.splice(i, 1); setPts(list); C.commit(); } };
  const rb = el('button', '', 'RESET CURVE'); rb.onclick = () => { C.begin(); if (P().curve) delete P().curve[ch]; C.commit(); }; wrap.appendChild(rb); draw();
}

// ================================================================= FONT LIBRARY (open-licence Google Fonts, loaded when you use them) + pro text styles + brush hardness
const GF = { 'Display': ['Anton', 'Bebas Neue', 'Oswald', 'Archivo Black', 'Alfa Slab One', 'Bungee', 'Bungee Shade', 'Black Ops One', 'Russo One', 'Righteous', 'Monoton', 'Fugaz One', 'Passion One', 'Titan One', 'Luckiest Guy', 'Bowlby One SC', 'Ultra', 'Abril Fatface', 'Rubik Mono One', 'Staatliches', 'Teko', 'Big Shoulders Display', 'Dela Gothic One', 'Rammetto One', 'Chango', 'Shrikhand', 'Coda', 'Audiowide', 'Faster One', 'Squada One'],
  'Sans serif': ['Inter', 'Montserrat', 'Poppins', 'Roboto', 'Open Sans', 'Lato', 'Raleway', 'Nunito', 'Work Sans', 'Rubik', 'Manrope', 'DM Sans', 'Outfit', 'Space Grotesk', 'Barlow', 'Kanit', 'Exo 2', 'Josefin Sans', 'Quicksand', 'Mulish', 'Urbanist', 'Sora', 'Lexend', 'Archivo', 'Plus Jakarta Sans', 'Red Hat Display'],
  'Serif': ['Playfair Display', 'Merriweather', 'Lora', 'Cormorant Garamond', 'EB Garamond', 'Libre Baskerville', 'Crimson Text', 'DM Serif Display', 'Cinzel', 'Bodoni Moda', 'Prata', 'Spectral', 'Fraunces', 'Marcellus', 'Yeseva One', 'Rozha One'],
  'Script and handwriting': ['Pacifico', 'Lobster', 'Dancing Script', 'Great Vibes', 'Sacramento', 'Satisfy', 'Kaushan Script', 'Yellowtail', 'Caveat', 'Permanent Marker', 'Shadows Into Light', 'Indie Flower', 'Amatic SC', 'Gloria Hallelujah', 'Homemade Apple', 'Mrs Saint Delafield', 'Allura', 'Parisienne', 'Rock Salt', 'Reenie Beanie', 'Covered By Your Grace', 'Nothing You Could Do', 'Marck Script', 'Cookie'],
  'Graffiti and street': ['Sedgwick Ave Display', 'Sedgwick Ave', 'Rubik Spray Paint', 'Rubik Wet Paint', 'Rubik Glitch', 'Rubik Puddles', 'Bangers', 'Knewave', 'Frijole', 'Nosifer', 'Creepster', 'Butcherman', 'Eater', 'Metal Mania', 'New Rocker', 'Rubik Dirt', 'Rubik Distressed'],
  'Retro and tech': ['Press Start 2P', 'VT323', 'Orbitron', 'Silkscreen', 'Major Mono Display', 'Share Tech Mono', 'Space Mono', 'Syncopate', 'Michroma', 'Wallpoet', 'Turret Road', 'Tilt Neon', 'Tilt Prism', 'Bungee Hairline', 'Nova Square', 'Iceland', 'Gruppo', 'Megrim', 'Codystar', 'Libre Barcode 39 Text'],
  'Gothic and blackletter': ['UnifrakturMaguntia', 'UnifrakturCook', 'Pirata One', 'Grenze Gotisch', 'Jacquard 24', 'Germania One', 'MedievalSharp', 'Almendra Display', 'Cinzel Decorative', 'Metamorphous'] };
const loaded = new Set();
C.loadFont = (fam) => { if (!fam || loaded.has(fam)) return; loaded.add(fam); const l = document.createElement('link'); l.rel = 'stylesheet'; l.href = 'https://fonts.googleapis.com/css2?family=' + encodeURIComponent(fam).replace(/%20/g, '+') + ':wght@400;700;900&display=swap'; l.onerror = () => { const l2 = document.createElement('link'); l2.rel = 'stylesheet'; l2.href = 'https://fonts.googleapis.com/css2?family=' + encodeURIComponent(fam).replace(/%20/g, '+') + '&display=swap'; document.head.appendChild(l2); }; document.head.appendChild(l); if (document.fonts) document.fonts.load('700 40px "' + fam + '"').then(() => C.draw && C.draw()).catch(() => { }); };
C.fontLib = GF;
// fonts already used in a design are fetched when it opens
const libFams = new Set(Object.values(GF).flat()); const loadUsed = () => { if (!C.art) return; for (const L of C.art.layers) if (L.type === 'text' && libFams.has(L.font)) C.loadFont(L.font); };
const prevRenderT = C.renderTab; C.renderTab = () => { loadUsed(); prevRenderT();
  const b = document.querySelector('#cvTab'); if (!b) return;
  if (C.tab === 'adjust') proAdjust(b);
  if (C.tab === 'fonts') fontBrowser(b);
  if (C.tab === 'text') textPresets(b);
  if (C.tab === 'draw') brushPro(b);
};
let fq = '', fcat = 'all';
function fontBrowser(b) {
  const box = el('div', 'profonts'); b.insertBefore(box, b.firstChild); const n = Object.values(GF).reduce((a, x) => a + x.length, 0);
  box.innerHTML = '<div class="hint"><b>FONT LIBRARY</b> ' + n + ' free open-licence fonts (downloaded the first time you use one; needs the internet), plus your PC fonts and fonts you import below.</div><input class="stkq" placeholder="Search fonts"><div class="stkcats"></div><div class="fgrid"></div>';
  const qi = box.querySelector('input'), cats = box.querySelector('.stkcats'), grid = box.querySelector('.fgrid'); qi.value = fq;
  for (const c of ['all'].concat(Object.keys(GF))) { const x = el('button', 'chip' + (fcat === c ? ' on' : ''), c === 'all' ? 'All' : c); x.onclick = () => { fcat = c; cats.querySelectorAll('.chip').forEach(q => q.classList.toggle('on', q === x)); fill(); }; cats.appendChild(x); }
  const fill = () => { grid.innerHTML = ''; const w = fq.trim().toLowerCase(); for (const [cat, list] of Object.entries(GF)) { if (fcat !== 'all' && fcat !== cat) continue; for (const fam of list) { if (w && !fam.toLowerCase().includes(w) && !cat.toLowerCase().includes(w)) continue; const x = el('button', 'fcard'); x.title = fam + ' (' + cat + ')'; x.innerHTML = '<span style="font-family:\'' + fam + '\',\'Segoe UI\';font-size:20px">' + esc(fam) + '</span>'; const pv = document.createElement('link'); pv.rel = 'stylesheet'; pv.href = 'https://fonts.googleapis.com/css2?family=' + encodeURIComponent(fam).replace(/%20/g, '+') + '&text=' + encodeURIComponent(fam) + '&display=swap'; if (!loaded.has(fam)) document.head.appendChild(pv);
    x.onclick = () => { C.loadFont(fam); const L = sel(); if (L && L.type === 'text') { C.begin(); L.font = fam; C.commit(); } else C.addLayer(Object.assign(C.lib.textBase(), { text: 'YOUR TEXT', name: fam, font: fam, size: 220 })); setTimeout(() => C.draw(), 600); setTimeout(() => C.draw(), 2000); }; grid.appendChild(x); } } if (!grid.children.length) grid.innerHTML = '<div class="hint">No font matches that.</div>'; };
  qi.oninput = () => { fq = qi.value; fill(); }; fill();
}
const TEXTPRO = [['Chrome', { fillKind: 'chrome', font: 'Anton', outline: 3, outlineColor: '#1a1a22', shadow: 40, shadowColor: '#000000', upper: true, spacing: 4 }], ['Gold', { fillKind: 'gold', font: 'Cinzel', weight: 900, outline: 2, outlineColor: '#4a2e00', shadow: 30, upper: true, spacing: 10 }], ['Glossy', { fillKind: 'gloss', font: 'Titan One', color: '#ff50c4', color2: '#a0005a', outline: 8, outlineColor: '#ffffff' }], ['Metallic', { fillKind: 'chrome', font: 'Russo One', emboss: 40, upper: true }], ['Embossed', { color: '#8a7aa8', emboss: 70, font: 'Archivo Black', upper: true }], ['Distressed', { font: 'Rubik Distressed', color: '#f4efe6', upper: true, spacing: 6, fx: { distress: 55 } }], ['Neon', { neon: true, glowColor: '#ff50c4', color: '#ffe6f8', font: 'Tilt Neon', weight: 400 }], ['Cinematic', { font: 'Cinzel', weight: 400, spacing: 60, color: '#e8e2d4', upper: true, size: 140, shadow: 30 }], ['Minimal', { font: 'Inter', weight: 300, spacing: 30, color: '#ffffff', size: 120 }], ['Double outline', { font: 'Bungee', color: '#ffffff', outline: 10, outlineColor: '#14061f', outline2: 14, outline2Color: '#ff50c4' }], ['Wave', { font: 'Luckiest Guy', color: '#ffd23c', outline: 8, outlineColor: '#14061f', warp: 'wave', warpAmt: 60 }], ['Circle badge', { font: 'Bebas Neue', color: '#ffffff', warp: 'circle', warpAmt: 40, spacing: 20, upper: true }]];
function textPresets(b) { const box = el('div'); b.insertBefore(box, b.firstChild); box.appendChild(el('div', 'hint', '<b>PRO TEXT STYLES</b> click to add (or to restyle the picked text)')); const g = el('div', 'grid2'); box.appendChild(g);
  for (const [n, st] of TEXTPRO) { const c = el('div', 'card', esc(n)); c.style.fontFamily = '"' + st.font + '","Segoe UI"'; if (st.font) C.loadFont(st.font); c.onclick = () => { if (st.font) C.loadFont(st.font); const L = sel(); const s2 = JSON.parse(JSON.stringify(st)); if (L && L.type === 'text') { C.begin(); Object.assign(L, s2); C.commit(); } else C.addLayer(Object.assign(C.lib.textBase(), { text: n.toUpperCase(), name: n }, s2)); setTimeout(() => C.draw(), 800); }; g.appendChild(c); } }
function brushPro(b) { const B = C.brush; if (!B) return; if (B.hard == null) B.hard = 100; if (B.smooth == null) B.smooth = 30; const box = el('div', 'proadj'); b.appendChild(box); box.innerHTML = '<div class="hint"><b>BRUSH PRO</b></div>';
  const rr = (label, key, min, max) => { const d = el('div', 'f', '<label>' + label + '</label><input type="range" min="' + min + '" max="' + max + '"><input type="number" min="' + min + '" max="' + max + '">'); box.appendChild(d); const r = d.querySelector('[type=range]'), n = d.querySelector('[type=number]'); r.value = n.value = B[key]; r.oninput = () => { B[key] = +r.value; n.value = r.value; }; n.onchange = () => { B[key] = clamp(+n.value, min, max); r.value = B[key]; }; };
  rr('Hardness', 'hard', 0, 100); rr('Smoothing', 'smooth', 0, 100); const r = el('div', 'btnrow'); box.appendChild(r); const pc = el('button', '', 'PENCIL'); pc.title = 'A small hard brush'; pc.onclick = () => { B.size = 2; B.hard = 100; B.smooth = 0; B.erase = false; C.tool = 'draw'; C.render(); }; r.appendChild(pc); const sb = el('button', '', 'SOFT AIRBRUSH'); sb.onclick = () => { B.size = 30; B.hard = 0; B.opacity = 0.35; B.erase = false; C.tool = 'draw'; C.render(); }; r.appendChild(sb); }
// strokes keep the brush hardness, and are smoothed when you let go
C.smoothStroke = (pts, amt) => { if (!amt || pts.length < 3) return pts; let p = pts; const passes = Math.round(amt / 25); for (let k = 0; k < passes; k++) { const q = [p[0]]; for (let i = 0; i < p.length - 1; i++) { const a = p[i], b = p[i + 1]; q.push([a[0] * 0.75 + b[0] * 0.25, a[1] * 0.75 + b[1] * 0.25], [a[0] * 0.25 + b[0] * 0.75, a[1] * 0.25 + b[1] * 0.75]); } q.push(p[p.length - 1]); p = q; } return p; };
addEventListener('mouseup', () => { if (C.tool !== 'draw' || !C.brush) return; const L = sel(); if (!L || L.type !== 'draw' || !L.strokes || !L.strokes.length) return; const s = L.strokes[L.strokes.length - 1]; if (s._pro) return; s._pro = 1; if (C.brush.hard != null && C.brush.hard < 100) s.hard = C.brush.hard; if (C.brush.smooth) s.pts = C.smoothStroke(s.pts, C.brush.smooth); setTimeout(() => C.draw(), 0); }, true);

// ================================================================= START SCREEN (quick starts), export warnings, export all pages
const QUICK = [['Single Cover', '3000x3000', '💿', 'Square artwork for a single on Spotify, Apple Music and more'], ['Album Cover', '3000x3000', '📀', 'Album artwork with a title and tracklist page'], ['Artist Banner', '2560x1440', '🎤', 'A wide banner for YouTube and streaming profiles'], ['YouTube Thumbnail', '1280x720', '▶️', '1280 x 720 thumbnail'], ['Promotional Post', '1080x1350', '📣', 'A 4:5 post for Instagram and Facebook'], ['Story / Reel', '1080x1920', '📱', 'A 9:16 story for TikTok, Reels and Shorts']];
function startArt(name, size, album) { const [w, h] = size.split('x').map(Number); C.begin(); const a = C.art; a.w = w; a.h = h; a.layers = []; a.global = { look: 'none' }; a.name = name; a.pages = null; a.page = 0; a.guides = null;
  C.lib.bg(a, 'pbg' in (C.extraDrawAbs || {}) ? 'pbg' : 'gradient', { kind: 'studio', c1: '#a85cff', c2: '#3a1066', seed: 7, angle: 90 });
  C.lib.txt(a, name.toUpperCase().indexOf('THUMBNAIL') >= 0 ? 'BIG TITLE' : 'ARTIST NAME', { y: w > h ? -0.15 : -0.55, size: Math.round(Math.min(w, h) * 0.09), font: 'Anton', spacing: 6, upper: true }); C.lib.txt(a, 'SONG TITLE', { y: w > h ? 0.25 : 0.7, size: Math.round(Math.min(w, h) * 0.06), weight: 400, font: 'Montserrat', spacing: 20, color: '#e6dcff' }); C.loadFont('Anton'); C.loadFont('Montserrat');
  if (album) { pg.ensure(); pg.add(false); const b2 = C.art; b2.layers = []; C.lib.bg(b2, 'color', { color: '#0e0b16' }); C.lib.txt(b2, 'TRACKLIST', { y: -0.75, size: 160, font: 'Anton', spacing: 10 }); C.lib.txt(b2, '1. Intro\n2. Song two\n3. Song three\n4. Song four\n5. Outro', { y: 0.05, size: 110, weight: 400, font: 'Montserrat', align: 'center', lineH: 1.5 }); b2.pages[b2.page].name = 'Tracklist'; b2.pages[0].name = 'Cover'; pg.go(0); }
  C.sel = null; C.commit(); C.size(); }
C.openStart = () => {
  if (document.querySelector('#cvStart')) return; const w = el('div', 'cvstart'); w.id = 'cvStart';
  w.innerHTML = '<div class="cvstartbox pro"><h2>PURPHEX <span>PHOTO STUDIO PRO</span></h2><p class="hint">Pick what you are making, or start from scratch.</p><div class="cvstartgrid q"></div><div class="cvstartrow"><b>START FROM SCRATCH</b> <input id="stW" type="number" min="16" max="10000" value="3000"> x <input id="stH" type="number" min="16" max="10000" value="3000"> <label><input id="stT" type="checkbox"> see-through background</label> <button id="stGo" class="accent">CREATE</button></div><div class="cvstartgrid"><button id="stPhoto" class="big"><span class="ic">🖼️</span>IMPORT A PHOTO</button><button id="stTpl" class="big"><span class="ic">🧩</span>BROWSE TEMPLATES</button><button id="stMine" class="big"><span class="ic">📂</span>OPEN A DESIGN</button><button id="stX" class="big"><span class="ic">✕</span>KEEP CURRENT DESIGN</button></div></div>';
  document.getElementById('cover').appendChild(w); const close = () => w.remove(); const q = w.querySelector('.q');
  for (const [n, sz, ic, tip] of QUICK) { const x = el('button', 'big accent', '<span class="ic">' + ic + '</span>' + esc(n.toUpperCase()) + '<small>' + sz.replace('x', ' x ') + '</small>'); x.title = tip; x.onclick = () => { close(); startArt(n, sz, n === 'Album Cover'); }; q.appendChild(x); }
  w.querySelector('#stGo').onclick = () => { const W = clamp(+w.querySelector('#stW').value || 3000, 16, 10000), H = clamp(+w.querySelector('#stH').value || 3000, 16, 10000), tr = w.querySelector('#stT').checked; close(); C.begin(); const a = C.art; a.w = W; a.h = H; a.layers = []; a.global = { look: 'none' }; a.pages = null; a.page = 0; a.name = 'Untitled design'; if (!tr) C.lib.bg(a, 'color', { color: '#1e1e22' }); C.sel = null; C.commit(); C.size(); };
  w.querySelector('#stPhoto').onclick = () => { close(); C.act('openPhoto'); }; w.querySelector('#stTpl').onclick = () => { close(); C.openWelcome(); }; w.querySelector('#stMine').onclick = () => { close(); C.act('open'); }; w.querySelector('#stX').onclick = close;
};
const hasSeeThrough = () => { const bgL = C.art.layers.find(l => l.name === 'Background' && l.vis); return !bgL || (bgL.opacity < 1); };
const prevExp = C.exportImage; C.exportImage = async (o) => { if (o.fmt === 'jpg' && (o.transparent || hasSeeThrough())) toast('Note: JPEG cannot be see-through, so the empty parts are white. Use PNG or WebP to keep them see-through.'); if (o.transparent && o.fmt !== 'jpg' && !C.art.layers.some(l => l.name === 'Background')) { } if (o.transparent && o.fmt === 'png' && C.art.layers.some(l => l.name === 'Background' && l.vis)) toast('See-through: the layer called "Background" is left out of this export.'); return prevExp(o); };
const prevDlg = C.exportDialog; C.exportDialog = () => { prevDlg(); setTimeout(() => { const go = document.querySelector('#exGo'); if (!go || document.querySelector('#exAll')) return; const n = (C.art.pages || []).length; if (n > 1) { const x = el('button', '', 'EXPORT ALL ' + n + ' PAGES'); x.id = 'exAll'; x.title = 'Each page as its own picture, with the format, size and quality above'; x.onclick = () => { const f = document.querySelector('#exF').value, o = { fmt: f, scale: +document.querySelector('#exS').value, quality: +document.querySelector('#exQ').value / 100, transparent: document.querySelector('#exT').checked && f !== 'jpg', name: document.querySelector('#exN').value.trim() || C.art.name }; PX.UI.close(); pg.exportAll(o); }; go.parentNode.insertBefore(x, go); } const t = document.querySelector('#exT'), f = document.querySelector('#exF'); if (t && f) { const warn = el('p', 'hint'); warn.id = 'exWarn'; t.closest('.f').appendChild(warn); const upd = () => { warn.textContent = f.value === 'jpg' ? 'JPEG has no see-through: empty parts become white.' : t.checked ? 'The layer called "Background" is left out, so it is see-through behind everything else.' : ''; }; f.addEventListener('change', upd); t.addEventListener('change', upd); upd(); } }, 0); };
// the stage listeners (selection tools, guides) go on once the editor is built
const prevShow = C.show; C.show = () => { prevShow(); const st = $c('#cvStage'); if (st && !st._pro) { st._pro = true; st.addEventListener('mousedown', toolDown, true); st.addEventListener('mousedown', guideDown, true); } C.draw(); };
// opening or closing a panel moves the floating bars clear of it
const prevOpen = C.openPanel; if (prevOpen) C.openPanel = (k) => { prevOpen(k); if (C.art && C.draw) C.draw(); };
// a project opened from an older version simply has one page
PX.on('artOpened', () => { C.selection = null; setTimeout(loadUsed, 0); });
})();
