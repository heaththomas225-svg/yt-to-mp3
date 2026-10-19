'use strict';
// =====================================================================
//  VIDEO STUDIO PRO: more transitions (push, iris, clock, barn doors, heart, glitch, whip pan, pixelate...), colour grading
//  (lift / gamma / gain wheels, curves and .cube LUTs), video effects (RGB split, glitch, VHS, grain, pixelate, mirror,
//  kaleidoscope, posterize, letterbox, scan lines), keyframe easing, more text animations, reversed clips, insert and overwrite
//  editing, nudging clips by a frame, go to a timecode, a searchable media bin, the Purphex sticker library on video, and a
//  keyboard shortcut sheet. Everything is drawn the same way in the preview and the export.
// =====================================================================
(() => {
const UI = PX.UI, clamp = PX.clamp, $ = (s, r) => (r || document).querySelector(s), $$ = (s, r) => Array.from((r || document).querySelectorAll(s));
const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; };
const esc = (s) => String(s == null ? '' : s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const mk = (w, h) => { const c = document.createElement('canvas'); c.width = Math.max(1, Math.round(w)); c.height = Math.max(1, Math.round(h)); return c; };
const pool = {}; const scratch = (k, W, H) => { let c = pool[k]; if (!c || c.width !== W || c.height !== H) { c = pool[k] = mk(W, H); } const g = c.getContext('2d'); g.setTransform(1, 0, 0, 1, 0, 0); g.globalAlpha = 1; g.globalCompositeOperation = 'source-over'; g.filter = 'none'; g.clearRect(0, 0, W, H); return c; };

// ================================================================= TRANSITIONS
const NEWTR = [['push', 'Push'], ['slideup', 'Slide up'], ['slidedown', 'Slide down'], ['wipeup', 'Wipe up'], ['wipeleft', 'Wipe left'], ['barn', 'Barn doors'], ['iris', 'Iris (circle)'], ['diamond', 'Diamond'], ['heart', 'Heart'], ['clock', 'Clock wipe'], ['spin', 'Spin'], ['zoomin', 'Zoom through'], ['flash', 'Flash'], ['whip', 'Whip pan'], ['glitch', 'Glitch'], ['pixelate', 'Pixelate']];
const NEWSET = new Set(NEWTR.map(x => x[0]));
for (const x of NEWTR) if (!PX.TRANSITIONS.some(y => y[0] === x[0])) PX.TRANSITIONS.push(x);
const origRole = PX.roleAt; PX.roleAt = (c, t) => { const r = origRole(c, t); if (r && NEWSET.has(r.type)) return Object.assign({}, r, { type: '_pro', pro: r.type }); return r; };
function heartPath(g, cx, cy, s) { g.moveTo(cx, cy + s * 0.42); g.bezierCurveTo(cx - s * 0.9, cy - s * 0.1, cx - s * 0.45, cy - s * 0.75, cx, cy - s * 0.3); g.bezierCurveTo(cx + s * 0.45, cy - s * 0.75, cx + s * 0.9, cy - s * 0.1, cx, cy + s * 0.42); g.closePath(); }
// draws the clip (already drawn alone on O) onto g with the transition: incoming = B / B0, outgoing = A / A0, p = 0..1
function composeTransition(g, O, type, incoming, p, W, H, t) {
  const R = Math.hypot(W, H) / 2; p = clamp(p, 0, 1); g.save();
  const draw = () => g.drawImage(O, 0, 0);
  if (type === 'push') { g.translate(incoming ? (1 - p) * W : -p * W, 0); draw(); }
  else if (type === 'slideup') { if (incoming) g.translate(0, (1 - p) * H); draw(); }
  else if (type === 'slidedown') { if (incoming) g.translate(0, -(1 - p) * H); draw(); }
  else if (type === 'whip') { const k = incoming ? 1 - p : p, dx = incoming ? (1 - p) * W : -p * W; g.filter = 'blur(' + (Math.sin(k * Math.PI) * 26 * W / 1920 + k * 10 * W / 1920).toFixed(1) + 'px)'; g.translate(dx, 0); draw(); }
  else if (type === 'spin') { if (incoming) { g.globalAlpha = p; g.translate(W / 2, H / 2); g.rotate((1 - p) * -Math.PI); g.scale(Math.max(0.01, p), Math.max(0.01, p)); g.translate(-W / 2, -H / 2); } draw(); }
  else if (type === 'zoomin') { if (incoming) { g.globalAlpha = p; g.translate(W / 2, H / 2); g.scale(0.6 + 0.4 * p, 0.6 + 0.4 * p); g.translate(-W / 2, -H / 2); } else { g.globalAlpha = 1 - p; g.translate(W / 2, H / 2); g.scale(1 + p * 1.2, 1 + p * 1.2); g.translate(-W / 2, -H / 2); g.filter = 'blur(' + (p * 14 * W / 1920).toFixed(1) + 'px)'; } draw(); }
  else if (type === 'flash') { if (incoming ? p >= 0.5 : p < 0.5) draw(); const f = 1 - Math.abs(p - 0.5) * 2; g.globalAlpha = clamp(f * 1.1, 0, 1); g.fillStyle = '#fff'; g.fillRect(0, 0, W, H); }
  else if (type === 'glitch') { const show = incoming ? p >= 0.5 : p < 0.5; if (show) { const k = 1 - Math.abs(p - 0.5) * 2, off = k * 30 * W / 1920; g.globalAlpha = 1; draw(); if (k > 0.02) { g.globalCompositeOperation = 'lighter'; g.globalAlpha = 0.5 * k; g.drawImage(O, off, 0); g.drawImage(O, -off, 0); g.globalCompositeOperation = 'source-over'; g.globalAlpha = 1; let s = Math.floor(t * 30) * 9301 + 49297; const rnd = () => { s = (s * 233280 + 49297) % 233280; return s / 233280; }; for (let i = 0; i < 6; i++) { const y = rnd() * H, h = (0.01 + rnd() * 0.05) * H, dx = (rnd() - 0.5) * 120 * k * W / 1920; g.drawImage(O, 0, y, W, h, dx, y, W, h); } } } }
  else if (type === 'pixelate') { const show = incoming ? p >= 0.5 : p < 0.5; if (show) { const k = 1 - Math.abs(p - 0.5) * 2, ps = Math.max(1, Math.round(k * 48 * W / 1920)); if (ps <= 1) draw(); else { const sw = Math.max(1, Math.round(W / ps)), sh = Math.max(1, Math.round(H / ps)), s2 = scratch('pix', sw, sh); s2.getContext('2d').drawImage(O, 0, 0, sw, sh); g.imageSmoothingEnabled = false; g.drawImage(s2, 0, 0, sw, sh, 0, 0, W, H); g.imageSmoothingEnabled = true; } } }
  else { // shaped reveals: only the incoming clip is masked
    if (!incoming) { draw(); g.restore(); return; }
    g.beginPath();
    if (type === 'wipeup') g.rect(0, H * (1 - p), W, H * p + 1);
    else if (type === 'wipeleft') g.rect(W * (1 - p), 0, W * p + 1, H);
    else if (type === 'barn') g.rect(W / 2 - p * W / 2, 0, p * W, H);
    else if (type === 'iris') g.arc(W / 2, H / 2, Math.max(0.1, p * R), 0, Math.PI * 2);
    else if (type === 'diamond') { const r = p * (W + H) / 2; g.moveTo(W / 2, H / 2 - r); g.lineTo(W / 2 + r, H / 2); g.lineTo(W / 2, H / 2 + r); g.lineTo(W / 2 - r, H / 2); g.closePath(); }
    else if (type === 'heart') heartPath(g, W / 2, H / 2 + p * H * 0.1, Math.max(1, p * Math.max(W, H) * 2.2));
    else if (type === 'clock') { g.moveTo(W / 2, H / 2); g.arc(W / 2, H / 2, R * 1.5, -Math.PI / 2, -Math.PI / 2 + p * Math.PI * 2); g.closePath(); }
    g.clip(); draw();
  }
  g.restore();
}

// ================================================================= COLOUR GRADING (lift / gamma / gain, curves, saturation, .cube LUT)
const gradeOn = (G) => !!G && !!((G.lift && G.lift.some(v => v)) || (G.gamma && G.gamma.some(v => v)) || (G.gain && G.gain.some(v => v)) || G.sat || G.curve || (G.lut && G.lut.n && G.lutMix !== 0) || G.temp);
const curveLut = (pts) => PX.Cover && PX.Cover.proLib ? PX.Cover.proLib.curveLut(pts) : (() => { const l = new Uint8ClampedArray(256); for (let i = 0; i < 256; i++) l[i] = i; return l; })();
const gradeCache = new Map();
function gradeTables(G) {
  const key = JSON.stringify([G.lift, G.gamma, G.gain, G.curve, G.temp]); let T = gradeCache.get(key); if (T) return T;
  T = [new Uint8ClampedArray(256), new Uint8ClampedArray(256), new Uint8ClampedArray(256)]; const cv = G.curve || {}, cm = curveLut(cv.m), cc = [curveLut(cv.r), curveLut(cv.g), curveLut(cv.b)];
  for (let ch = 0; ch < 3; ch++) { const lift = ((G.lift || [0, 0, 0])[ch] || 0) / 100 * 0.25 + (ch === 0 ? 1 : ch === 2 ? -1 : 0) * (G.temp || 0) / 100 * 0.05, gam = Math.pow(2, -((G.gamma || [0, 0, 0])[ch] || 0) / 100), gain = 1 + ((G.gain || [0, 0, 0])[ch] || 0) / 100 * 0.6 + (ch === 0 ? 1 : ch === 2 ? -1 : 0) * (G.temp || 0) / 100 * 0.08;
    for (let v = 0; v < 256; v++) { let x = v / 255; x = gain * (x + lift * (1 - x)); x = Math.pow(clamp(x, 0, 1), gam); T[ch][v] = cc[ch][cm[Math.round(x * 255)]]; } }
  gradeCache.set(key, T); if (gradeCache.size > 40) gradeCache.delete(gradeCache.keys().next().value); return T;
}
// a .cube LUT file (3D, up to 64 points a side) as a small array of bytes kept with the clip
PX.parseCube = (text) => { let n = 0; const vals = []; let dmin = [0, 0, 0], dmax = [1, 1, 1];
  for (const raw of String(text).split(/\r?\n/)) { const line = raw.trim(); if (!line || line[0] === '#') continue; const p = line.split(/\s+/); if (p[0] === 'LUT_3D_SIZE') n = parseInt(p[1], 10); else if (p[0] === 'DOMAIN_MIN') dmin = p.slice(1, 4).map(Number); else if (p[0] === 'DOMAIN_MAX') dmax = p.slice(1, 4).map(Number); else if (/^[-0-9.]/.test(p[0]) && p.length >= 3) vals.push(+p[0], +p[1], +p[2]); else if (p[0] === 'LUT_1D_SIZE') throw new Error('1D LUTs are not supported: use a 3D .cube file.'); }
  if (!(n >= 2 && n <= 64)) throw new Error('That is not a 3D .cube LUT (no LUT_3D_SIZE between 2 and 64).'); if (vals.length < n * n * n * 3) throw new Error('The LUT is cut short: it needs ' + n * n * n + ' colour lines.');
  const bytes = new Uint8Array(n * n * n * 3); for (let i = 0; i < bytes.length; i++) { const ch = i % 3; bytes[i] = clamp(Math.round((vals[i] - dmin[ch]) / ((dmax[ch] - dmin[ch]) || 1) * 255), 0, 255); }
  let bin = ''; for (let i = 0; i < bytes.length; i++) bin += String.fromCharCode(bytes[i]); return { n, b64: btoa(bin) }; };
const lutBytes = new Map(); const lutOf = (L) => { let b = lutBytes.get(L.b64); if (!b) { const s = atob(L.b64); b = new Uint8Array(s.length); for (let i = 0; i < s.length; i++) b[i] = s.charCodeAt(i); lutBytes.set(L.b64, b); if (lutBytes.size > 8) lutBytes.delete(lutBytes.keys().next().value); } return b; };
function applyGrade(id, G) {
  const d = id.data, T = gradeTables(G), sat = 1 + (G.sat || 0) / 100, L = G.lut && G.lut.n ? G.lut : null, lb = L ? lutOf(L) : null, n = L ? L.n : 0, mix = L ? clamp(G.lutMix == null ? 1 : G.lutMix / 100, 0, 1) : 0;
  for (let i = 0; i < d.length; i += 4) {
    if (!d[i + 3]) continue; let r = T[0][d[i]], g = T[1][d[i + 1]], b = T[2][d[i + 2]];
    if (sat !== 1) { const l = 0.2126 * r + 0.7152 * g + 0.0722 * b; r = l + (r - l) * sat; g = l + (g - l) * sat; b = l + (b - l) * sat; }
    if (lb && mix > 0) { const fr = clamp(r, 0, 255) / 255 * (n - 1), fg = clamp(g, 0, 255) / 255 * (n - 1), fb = clamp(b, 0, 255) / 255 * (n - 1), r0 = Math.floor(fr), g0 = Math.floor(fg), b0 = Math.floor(fb), r1 = Math.min(n - 1, r0 + 1), g1 = Math.min(n - 1, g0 + 1), b1 = Math.min(n - 1, b0 + 1), dr = fr - r0, dg = fg - g0, db = fb - b0;
      const at = (ri, gi, bi, c) => lb[((bi * n + gi) * n + ri) * 3 + c]; const out = [0, 0, 0];
      for (let c = 0; c < 3; c++) { const c00 = at(r0, g0, b0, c) * (1 - dr) + at(r1, g0, b0, c) * dr, c10 = at(r0, g1, b0, c) * (1 - dr) + at(r1, g1, b0, c) * dr, c01 = at(r0, g0, b1, c) * (1 - dr) + at(r1, g0, b1, c) * dr, c11 = at(r0, g1, b1, c) * (1 - dr) + at(r1, g1, b1, c) * dr; out[c] = (c00 * (1 - dg) + c10 * dg) * (1 - db) + (c01 * (1 - dg) + c11 * dg) * db; }
      r = r + (out[0] - r) * mix; g = g + (out[1] - g) * mix; b = b + (out[2] - b) * mix; }
    d[i] = r; d[i + 1] = g; d[i + 2] = b;
  }
}
PX.gradeLib = { gradeOn, applyGrade, gradeTables };

// ================================================================= VIDEO EFFECTS
const VFX = [['rgb', 'RGB split', 0, 60], ['glitch', 'Glitch', 0, 100], ['vhs', 'VHS', 0, 100], ['grain', 'Film grain', 0, 100], ['pixel', 'Pixelate', 0, 80], ['posterize', 'Posterize', 0, 12], ['scan', 'Scan lines', 0, 100], ['letterbox', 'Letterbox (cinema bars)', 0, 100]];
const vfxOn = (V) => !!V && (VFX.some(([k]) => V[k] > 0) || (V.mirror && V.mirror !== 'none') || V.kaleido > 0);
let grainTile = null; const grain = () => { if (grainTile) return grainTile; grainTile = mk(256, 256); const g = grainTile.getContext('2d'), id = g.createImageData(256, 256); let s = 7; for (let i = 0; i < id.data.length; i += 4) { s = (s * 16807) % 2147483647; const v = s % 256; id.data[i] = id.data[i + 1] = id.data[i + 2] = v; id.data[i + 3] = 255; } g.putImageData(id, 0, 0); return grainTile; };
function applyVfx(O, V, t, W, H) {
  let g = O.getContext('2d'); const s = W / 1920;
  if (V.mirror && V.mirror !== 'none') { const c = scratch('mir', W, H), cg = c.getContext('2d'); cg.drawImage(O, 0, 0); g.clearRect(0, 0, W, H);
    if (V.mirror === 'h' || V.mirror === 'quad') { g.drawImage(c, 0, 0, W / 2, H, 0, 0, W / 2, H); g.save(); g.translate(W, 0); g.scale(-1, 1); g.drawImage(c, 0, 0, W / 2, H, 0, 0, W / 2, H); g.restore(); }
    if (V.mirror === 'v') { g.drawImage(c, 0, 0, W, H / 2, 0, 0, W, H / 2); g.save(); g.translate(0, H); g.scale(1, -1); g.drawImage(c, 0, 0, W, H / 2, 0, 0, W, H / 2); g.restore(); }
    if (V.mirror === 'quad') { const top = scratch('mir2', W, H); top.getContext('2d').drawImage(O, 0, 0); g.clearRect(0, 0, W, H); g.drawImage(top, 0, 0, W, H / 2, 0, 0, W, H / 2); g.save(); g.translate(0, H); g.scale(1, -1); g.drawImage(top, 0, 0, W, H / 2, 0, 0, W, H / 2); g.restore(); } }
  if (V.kaleido > 0) { const n = Math.max(3, Math.round(V.kaleido)), c = scratch('kal', W, H); c.getContext('2d').drawImage(O, 0, 0); g.clearRect(0, 0, W, H); const R = Math.hypot(W, H); for (let i = 0; i < n * 2; i++) { g.save(); g.translate(W / 2, H / 2); g.rotate(i * Math.PI / n); if (i % 2) g.scale(1, -1); g.beginPath(); g.moveTo(0, 0); g.arc(0, 0, R, 0, Math.PI / n + 0.01); g.closePath(); g.clip(); g.drawImage(c, -W / 2, -H / 2); g.restore(); } }
  if (V.pixel > 0) { const ps = Math.max(2, Math.round(V.pixel * s)), sw = Math.max(1, Math.round(W / ps)), sh = Math.max(1, Math.round(H / ps)), c = scratch('vpx', sw, sh); c.getContext('2d').drawImage(O, 0, 0, sw, sh); g.clearRect(0, 0, W, H); g.imageSmoothingEnabled = false; g.drawImage(c, 0, 0, sw, sh, 0, 0, W, H); g.imageSmoothingEnabled = true; }
  if (V.posterize > 0 || V.rgb > 0 || V.vhs > 0) { const id = g.getImageData(0, 0, W, H), d = id.data;
    if (V.posterize > 0) { const lv = Math.max(2, 14 - Math.round(V.posterize)), st = 255 / (lv - 1); for (let i = 0; i < d.length; i += 4) { d[i] = Math.round(d[i] / st) * st; d[i + 1] = Math.round(d[i + 1] / st) * st; d[i + 2] = Math.round(d[i + 2] / st) * st; } }
    const off = Math.round(((V.rgb || 0) + (V.vhs || 0) * 0.08) * s);
    if (off > 0) { const src = new Uint8ClampedArray(d); for (let y = 0; y < H; y++) { const row = y * W * 4; for (let x = 0; x < W; x++) { const i = row + x * 4, xr = Math.max(0, x - off), xb = Math.min(W - 1, x + off); d[i] = src[row + xr * 4]; d[i + 2] = src[row + xb * 4 + 2]; const a = Math.max(src[i + 3], src[row + xr * 4 + 3], src[row + xb * 4 + 3]); d[i + 3] = a; } } }
    if (V.vhs > 0) { const k = V.vhs / 100; for (let i = 0; i < d.length; i += 4) { const l = (d[i] + d[i + 1] + d[i + 2]) / 3; d[i] = d[i] * (1 - 0.15 * k) + l * 0.15 * k + 10 * k; d[i + 1] = d[i + 1] * (1 - 0.1 * k) + l * 0.1 * k; d[i + 2] = d[i + 2] * (1 - 0.25 * k) + l * 0.25 * k + 18 * k; } }
    g.putImageData(id, 0, 0); }
  if (V.glitch > 0) { const k = V.glitch / 100; let sd = Math.floor(t * 24) * 7919 + 13; const rnd = () => { sd = (sd * 48271) % 2147483647; return sd / 2147483647; }; if (rnd() < 0.35 + k * 0.6) { const c = scratch('gl', W, H); c.getContext('2d').drawImage(O, 0, 0); for (let i = 0; i < 3 + Math.round(k * 10); i++) { const y = rnd() * H, h = (0.005 + rnd() * 0.06) * H, dx = (rnd() - 0.5) * 160 * k * s; g.clearRect(0, y, W, h); g.drawImage(c, 0, y, W, h, dx, y, W, h); } } }
  if (V.vhs > 0) { const k = V.vhs / 100; g.save(); g.globalAlpha = 0.12 * k; g.fillStyle = '#fff'; const y = ((t * 0.35) % 1) * H; g.fillRect(0, y, W, H * 0.04); g.restore(); }
  if (V.scan > 0) { g.save(); g.globalCompositeOperation = 'source-atop'; g.globalAlpha = V.scan / 100 * 0.45; g.fillStyle = '#000'; const step = Math.max(2, Math.round(4 * s)); for (let y = 0; y < H; y += step) g.fillRect(0, y, W, Math.max(1, step / 2)); g.restore(); }
  if (V.grain > 0) { g.save(); g.globalCompositeOperation = 'source-atop'; g.globalAlpha = V.grain / 100 * 0.35; const pt = g.createPattern(grain(), 'repeat'); const ox = Math.floor(t * 97 % 256), oy = Math.floor(t * 53 % 256); g.translate(-ox, -oy); g.fillStyle = pt; g.globalCompositeOperation = 'overlay'; g.fillRect(ox, oy, W + 256, H + 256); g.restore(); }
  if (V.letterbox > 0) { const bar = H * (V.letterbox / 100) * 0.14; g.save(); g.fillStyle = '#000'; g.fillRect(0, 0, W, bar); g.fillRect(0, H - bar, W, bar); g.restore(); }
}
PX.vfxLib = { VFX, vfxOn, applyVfx };

// ================================================================= the drawing hook: a clip with a PRO transition, a grade or effects is drawn alone first
const origDraw = PX.drawClip;
PX.drawClip = (g, c, t, W, H, exporting) => {
  const role = PX.roleAt(c, t); if (!role) return; const tr = role.type === '_pro' ? role.pro : null, G = gradeOn(c.grade) ? c.grade : null, V = vfxOn(c.vfx) ? c.vfx : null;
  if (!tr && !G && !V) return origDraw(g, c, t, W, H, exporting);
  if ((c.type === 'text' || c.type === 'visual') && !(t >= c.start && t < c.start + c.dur)) return;
  const O = scratch('clip' + (exporting ? 'E' : 'P'), W, H), og = O.getContext('2d'); origDraw(og, c, t, W, H, exporting); og.setTransform(1, 0, 0, 1, 0, 0);
  if (G) { const id = og.getImageData(0, 0, W, H); applyGrade(id, G); og.putImageData(id, 0, 0); }
  if (V) applyVfx(O, V, t, W, H);
  if (tr) composeTransition(g, O, tr, role.role === 'B' || role.role === 'B0', role.p, W, H, t); else { g.save(); g.setTransform(1, 0, 0, 1, 0, 0); g.drawImage(O, 0, 0); g.restore(); }
};

// ================================================================= KEYFRAME EASING (per clip, or per key)
PX.EASES = [['smooth', 'Smooth (ease in and out)'], ['linear', 'Linear'], ['in', 'Ease in (slow start)'], ['out', 'Ease out (slow end)'], ['hold', 'Hold (jump)'], ['back', 'Overshoot'], ['bounce', 'Bounce'], ['elastic', 'Elastic']];
PX.easeFn = (name, p) => { p = clamp(p, 0, 1); switch (name) {
  case 'linear': return p; case 'in': return p * p * p; case 'out': return 1 - Math.pow(1 - p, 3); case 'hold': return p < 1 ? 0 : 1;
  case 'back': { const c1 = 1.70158, c3 = c1 + 1; return 1 + c3 * Math.pow(p - 1, 3) + c1 * Math.pow(p - 1, 2); }
  case 'bounce': { const n1 = 7.5625, d1 = 2.75; let x = p; if (x < 1 / d1) return n1 * x * x; if (x < 2 / d1) return n1 * (x -= 1.5 / d1) * x + 0.75; if (x < 2.5 / d1) return n1 * (x -= 2.25 / d1) * x + 0.9375; return n1 * (x -= 2.625 / d1) * x + 0.984375; }
  case 'elastic': return p === 0 || p === 1 ? p : Math.pow(2, -10 * p) * Math.sin((p * 10 - 0.75) * (2 * Math.PI) / 3) + 1;
  default: return p < 0.5 ? 2 * p * p : 1 - Math.pow(-2 * p + 2, 2) / 2; } };
PX.kfValue = (c, prop, t) => {
  const ks = c.kf && c.kf[prop]; if (!ks || !ks.length) return null; const lt = t - c.start;
  if (lt <= ks[0][0]) return ks[0][1]; const last = ks[ks.length - 1]; if (lt >= last[0]) return last[1];
  for (let i = 1; i < ks.length; i++) if (lt < ks[i][0]) { const a = ks[i - 1], b = ks[i], p = (lt - a[0]) / Math.max(1e-6, b[0] - a[0]); return a[1] + (b[1] - a[1]) * PX.easeFn(b[2] || c.kfEase || 'smooth', p); }
  return last[1];
};

// ================================================================= TEXT ANIMATIONS (more ways in and out)
const TEXTANIMS = [['zoom', 'Zoom in'], ['right', 'Slide from the right'], ['drop', 'Drop and bounce'], ['spin', 'Spin'], ['flicker', 'Flicker'], ['glitch', 'Glitch'], ['wipe', 'Wipe reveal'], ['stretch', 'Stretch'], ['rise', 'Rise and blur']];
PX.TEXTANIMS_PRO = TEXTANIMS; const TA = new Set(TEXTANIMS.map(x => x[0]));
const origText = PX.drawText; PX.drawText = (g, c, t, W, H) => {
  const x = c.text || {}; const inP = TA.has(x.animIn), outP = TA.has(x.animOut); if (!inP && !outP) return origText(g, c, t, W, H);
  const local = t - c.start, ad = Math.min(x.animDur || 0.5, c.dur / 2); let type = null, p = 1;
  if (inP && local < ad) { type = x.animIn; p = local / ad; } else if (outP && local > c.dur - ad) { type = x.animOut; p = (c.dur - local) / ad; }
  const c2 = Object.create(c); c2.text = Object.assign({}, x, { animIn: inP ? 'none' : x.animIn, animOut: outP ? 'none' : x.animOut });
  if (!type) return origText(g, c2, t, W, H); p = clamp(p, 0, 1);
  const cx = W / 2 + (c.x || 0) * W / 2, cy = H / 2 + (c.y || 0) * H / 2; g.save();
  const around = (fn) => { g.translate(cx, cy); fn(); g.translate(-cx, -cy); };
  if (type === 'zoom') { g.globalAlpha *= p; around(() => g.scale(0.3 + 0.7 * PX.easeFn('out', p), 0.3 + 0.7 * PX.easeFn('out', p))); }
  else if (type === 'right') { g.globalAlpha *= p; g.translate((1 - PX.easeFn('out', p)) * -W * 0.08 * -1, 0); }
  else if (type === 'drop') { const e = PX.easeFn('bounce', p); g.translate(0, -(1 - e) * H * 0.35); g.globalAlpha *= Math.min(1, p * 3); }
  else if (type === 'spin') { g.globalAlpha *= p; around(() => { g.rotate((1 - PX.easeFn('out', p)) * Math.PI * 2); const s = 0.4 + 0.6 * p; g.scale(s, s); }); }
  else if (type === 'flicker') { const on = p > 0.95 || (Math.sin(p * 47) > 0.2 && Math.sin(p * 13 + 1) > -0.4); g.globalAlpha *= on ? 1 : 0.08; }
  else if (type === 'glitch') { const k = 1 - p; g.translate(Math.sin(t * 90) * 24 * k * W / 1920, 0); g.globalAlpha *= p < 0.15 ? p / 0.15 : 1; }
  else if (type === 'wipe') { g.beginPath(); g.rect(0, 0, W * PX.easeFn('out', p), H); g.clip(); }
  else if (type === 'stretch') { around(() => g.scale(1 + (1 - PX.easeFn('back', p)) * 1.2, Math.max(0.05, PX.easeFn('back', p)))); g.globalAlpha *= Math.min(1, p * 2); }
  else if (type === 'rise') { g.translate(0, (1 - PX.easeFn('out', p)) * H * 0.12); g.globalAlpha *= p; g.filter = 'blur(' + ((1 - p) * 10 * W / 1920).toFixed(1) + 'px)'; }
  origText(g, c2, t, W, H); g.restore();
};

// ================================================================= REVERSED CLIPS (the picture and the sound play backwards)
const revAudio = {};
const revBuf = (ab) => { const ctx = PX.ac(), r = ctx.createBuffer(ab.numberOfChannels, ab.length, ab.sampleRate); for (let ch = 0; ch < ab.numberOfChannels; ch++) { const s = ab.getChannelData(ch), d = r.getChannelData(ch), n = s.length; for (let i = 0; i < n; i++) d[i] = s[n - 1 - i]; } return r; };
const origMediaTime = PX.mediaTime; PX.mediaTime = (c, t) => { if (!c.reverse) return origMediaTime(c, t); const m = PX.mediaById(c.media); const md = m && m.dur ? m.dur : 1e9; return clamp(c.in + (c.start + c.dur - t) * c.speed, 0, Math.max(0, md - 0.04)); };
// reversed clips do not play the video element forwards: each frame is looked up (the export does this anyway)
const origSync = PX.syncVideos; PX.syncVideos = (t, playing) => { origSync(t, playing); for (const c of PX.visibleVideoClips(t)) { if (!c.reverse) continue; const v = PX.videoFor(c); if (!v) continue; if (!v.paused) v.pause(); const mt = PX.mediaTime(c, t); if (Math.abs(v.currentTime - mt) > (playing ? 0.08 : 0.02) && !v.seeking) v.currentTime = mt; } };
const origMediaById = PX.mediaById; PX.mediaById = (id) => { if (typeof id === 'string' && id.indexOf('rev:') === 0) { const m = origMediaById(id.slice(4)); return m ? Object.assign({}, m, { id }) : null; } return origMediaById(id); };
const origSched = PX.scheduleAudio; PX.scheduleAudio = (ctx, dest, t0, t1, live) => {
  const rev = PX.P.clips.filter(c => c.reverse && (c.type === 'video' || c.type === 'audio')); if (!rev.length) return origSched(ctx, dest, t0, t1, live);
  const keep = PX.P.clips; try {
    PX.P.clips = keep.map(c => { if (!c.reverse || !(c.type === 'video' || c.type === 'audio')) return c; const m = origMediaById(c.media); const ab = m && PX.audio[m.id]; if (!ab) return c; const rid = 'rev:' + m.id; if (!PX.audio[rid] || revAudio[rid] !== ab) { PX.audio[rid] = revBuf(ab); revAudio[rid] = ab; } const n = Object.create(c); n.media = rid; n.in = ab.duration - (c.in + c.dur * c.speed); return n; });
    return origSched(ctx, dest, t0, t1, live);
  } finally { PX.P.clips = keep; }
};

// ================================================================= EDITING: insert / overwrite, nudge by a frame, go to a timecode
UI.editMode = 'normal';   // 'normal' (put it where there is room), 'insert' (push the rest along), 'overwrite' (cover what is there)
const origAdd = UI.addMediaAt; UI.addMediaAt = (m, trackId, t) => {
  const before = new Set(PX.P.clips.map(c => c.id)); const r = origAdd(m, trackId, t); const n = PX.P.clips.find(c => !before.has(c.id)); if (!n || UI.editMode === 'normal') return r;
  PX.begin(); const others = PX.P.clips.filter(c => c !== n && c.track === n.track);
  if (UI.editMode === 'insert') { n.start = t; for (const o of others) { if (o.start >= t - 1e-6) o.start += n.dur; else if (o.start + o.dur > t + 1e-6) { const cut = t - o.start; const tail = JSON.parse(JSON.stringify(o)); tail.id = PX.uid('c'); tail.start = t + n.dur; tail.in = (o.in || 0) + cut * (o.speed || 1); tail.dur = o.dur - cut; o.dur = cut; tail.transIn = null; PX.P.clips.push(tail); } } }
  else { n.start = t; const a = t, b = t + n.dur; for (const o of others) { const s = o.start, e = o.start + o.dur; if (e <= a + 1e-6 || s >= b - 1e-6) continue; if (s >= a && e <= b) PX.P.clips = PX.P.clips.filter(x => x !== o); else if (s < a && e > b) { const tail = JSON.parse(JSON.stringify(o)); tail.id = PX.uid('c'); tail.start = b; tail.in = (o.in || 0) + (b - s) * (o.speed || 1); tail.dur = e - b; tail.transIn = null; o.dur = a - s; PX.P.clips.push(tail); } else if (s < a) o.dur = a - s; else { o.in = (o.in || 0) + (b - s) * (o.speed || 1); o.dur = e - b; o.start = b; } } }
  PX.commit(UI.editMode); return r; };
UI.nudge = (frames) => { const s = UI.selected().filter(c => !(PX.trackById(c.track) || {}).locked); if (!s.length) return PX.toast('Select a clip first.'); const d = frames / PX.P.settings.fps; PX.begin(); for (const c of s) c.start = Math.max(0, Math.round((c.start + d) * PX.P.settings.fps) / PX.P.settings.fps); PX.commit('nudge'); };
UI.parseTC = (s) => { s = String(s || '').trim(); if (!s) return null; const fps = PX.P.settings.fps; const p = s.split(':').map(x => x.trim()); if (p.some(x => x === '' || isNaN(+x))) return null; let t = 0; if (p.length === 4) t = (+p[0]) * 3600 + (+p[1]) * 60 + (+p[2]) + (+p[3]) / fps; else if (p.length === 3) t = (+p[0]) * 60 + (+p[1]) + (+p[2]) / fps; else if (p.length === 2) t = (+p[0]) * 60 + (+p[1]); else t = +p[0]; return t >= 0 ? t : null; };
UI.gotoTC = () => UI.prompt ? UI.prompt('Go to a time (seconds, m:ss, or m:ss:frames)', PX.fmtT(PX.play.t), v => { const t = UI.parseTC(v); if (t == null) return PX.toast('That is not a time: try 1:30 or 1:30:12.', true); PX.stopPlay(false); PX.seek(t); }) : null;
addEventListener('keydown', (e) => { if (!UI || UI.tool !== 'video') return; const tag = (e.target.tagName || '').toLowerCase(); if (tag === 'input' || tag === 'textarea' || tag === 'select') return; if (!$('#modal').classList.contains('hidden')) return;
  if (e.altKey && (e.key === 'ArrowLeft' || e.key === 'ArrowRight')) { e.preventDefault(); e.stopImmediatePropagation(); UI.nudge((e.key === 'ArrowLeft' ? -1 : 1) * (e.shiftKey ? 10 : 1)); }
  else if (!e.ctrlKey && !e.altKey && e.key.toLowerCase() === 'g') { e.preventDefault(); e.stopImmediatePropagation(); UI.gotoTC(); }
  else if (!e.ctrlKey && !e.altKey && e.key.toLowerCase() === 'i') { e.preventDefault(); e.stopImmediatePropagation(); UI.setEditMode(UI.editMode === 'insert' ? 'normal' : 'insert'); }
  else if (!e.ctrlKey && !e.altKey && e.key.toLowerCase() === 'o') { e.preventDefault(); e.stopImmediatePropagation(); UI.setEditMode(UI.editMode === 'overwrite' ? 'normal' : 'overwrite'); }
  else if (e.key === '?' || (e.shiftKey && e.key === '/')) { e.preventDefault(); e.stopImmediatePropagation(); UI.shortcuts(); } }, true);
UI.setEditMode = (m) => { UI.editMode = m; const b = $('#vEditMode'); if (b) b.querySelectorAll('button').forEach(x => x.classList.toggle('on', x.dataset.m === m)); PX.toast({ normal: 'Normal: new clips go where there is room.', insert: 'Insert: new clips push the rest of the track along (I).', overwrite: 'Overwrite: new clips cover what is there (O).' }[m]); };
UI.shortcuts = () => UI.modal('<h3>Purphex Video Editor shortcuts</h3><div class="kbd">' + [['Space', 'Play / pause'], ['S', 'Split at the playhead'], ['Delete', 'Delete the selected clips'], ['Ctrl+D', 'Duplicate'], ['Ctrl+Z / Ctrl+Y', 'Undo / redo'], ['Ctrl+S', 'Save'], ['Ctrl+E', 'Export'], ['Left / Right (or , .)', 'One frame back / forward'], ['Shift + Left / Right', 'One second back / forward'], ['Alt + Left / Right', 'Nudge the selected clips one frame (Shift: 10)'], ['Home / End', 'Start / end'], ['G', 'Go to a time'], ['M', 'Add a marker'], ['I', 'Insert mode on / off'], ['O', 'Overwrite mode on / off'], ['+ / -', 'Zoom the timeline'], ['Ctrl+A', 'Select everything'], ['Esc', 'Clear the selection'], ['?', 'This sheet']].map(([k, d]) => '<div><b>' + esc(k) + '</b><span>' + esc(d) + '</span></div>').join('') + '</div><div class="acts"><button id="mNo" class="accent">CLOSE</button></div>', b => { $('#mNo', b).onclick = UI.close; });

// ================================================================= the media bin: search, sort and "not used yet"
let binQ = '', binSort = 'added', binUnused = false;
const origBin = UI.renderBin; UI.renderBin = () => { origBin(); const bin = $('#bin'); if (!bin || !PX.P.media.length) return; const host = bin.parentNode; let bar = $('#binBar'); if (!bar) { bar = el('div', 'binbar'); bar.id = 'binBar'; bar.innerHTML = '<input id="binQ" placeholder="Search media"><select id="binSort"><option value="added">Order added</option><option value="name">Name</option><option value="kind">Kind</option><option value="dur">Length</option></select><label class="hint"><input type="checkbox" id="binUn"> not used yet</label>'; host.insertBefore(bar, bin); const q = $('#binQ', bar), so = $('#binSort', bar), un = $('#binUn', bar); q.oninput = () => { binQ = q.value; filt(); }; so.onchange = () => { binSort = so.value; UI.renderBin(); }; un.onchange = () => { binUnused = un.checked; filt(); }; }
  const items = Array.from(bin.children).filter(d => d.classList.contains('mi')); const media = PX.P.media.slice(); const idx = new Map(media.map((m, i) => [m, i]));
  if (binSort !== 'added') { const order = media.slice().sort((a, b) => binSort === 'name' ? a.name.localeCompare(b.name) : binSort === 'kind' ? (a.kind.localeCompare(b.kind) || a.name.localeCompare(b.name)) : (b.dur || 0) - (a.dur || 0)); for (const m of order) { const d = items[idx.get(m)]; if (d) bin.appendChild(d); } }
  items.forEach((d, i) => { d._m = media[i]; }); filt(); };
function filt() { const bin = $('#bin'); if (!bin) return; const w = binQ.trim().toLowerCase(); let shown = 0; for (const d of bin.children) { if (!d._m) continue; const m = d._m, used = PX.P.clips.some(c => c.media === m.id); const ok = (!w || m.name.toLowerCase().includes(w) || m.kind.includes(w)) && (!binUnused || !used); d.style.display = ok ? '' : 'none'; if (ok) shown++; } let none = $('#binNone'); if (!shown) { if (!none) { none = el('div', 'empty small', 'Nothing matches.'); none.id = 'binNone'; bin.appendChild(none); } } else if (none) none.remove(); }

// ================================================================= the panels: grading, effects, transitions list, stickers, edit modes
const sec = (b, title) => { const d = el('div', 'vprosec'); d.innerHTML = '<h4>' + title + '</h4>'; b.appendChild(d); return d; };
const slider = (b, label, get, set, min, max, step) => { const d = el('div', 'f', '<label>' + label + '</label><input type="range" min="' + min + '" max="' + max + '" step="' + (step || 1) + '"><input type="number" min="' + min + '" max="' + max + '" step="' + (step || 1) + '">'); b.appendChild(d); const r = $('[type=range]', d), n = $('[type=number]', d); r.value = n.value = get() || 0; r.onpointerdown = () => PX.begin(); r.oninput = () => { set(+r.value); n.value = r.value; PX.redraw(); }; r.onchange = () => PX.commit('grade'); n.onchange = () => { PX.begin(); set(clamp(+n.value, min, max)); PX.commit('grade'); }; return d; };
function gradePanel(b) {
  const c = UI.selected().find(x => x.type === 'video' || x.type === 'image'); const s = sec(b, 'COLOUR GRADING'); b.insertBefore(s, b.children[1] || null);   /* grading first, the basic sliders under it */
  if (!c) { s.appendChild(el('p', 'hint', 'Select a video or picture clip to grade it.')); return; }
  const G = () => { c.grade = c.grade || {}; return c.grade; }, g0 = c.grade || {};
  const wheel = (key, name) => { s.appendChild(el('div', 'hint', '<b>' + name + '</b>')); ['Red', 'Green', 'Blue'].forEach((cn, i) => slider(s, cn, () => ((g0[key] || [])[i]) || 0, v => { const a = G()[key] = G()[key] || [0, 0, 0]; a[i] = v; }, -100, 100)); slider(s, 'All three', () => 0, v => { G()[key] = [v, v, v]; }, -100, 100); };
  wheel('lift', 'Lift (shadows)'); wheel('gamma', 'Gamma (midtones)'); wheel('gain', 'Gain (highlights)');
  slider(s, 'Saturation', () => g0.sat || 0, v => G().sat = v, -100, 100); slider(s, 'Temperature', () => g0.temp || 0, v => G().temp = v, -100, 100);
  s.appendChild(el('div', 'hint', '<b>Curves</b> (all colours) drag the dots')); curve(s, c);
  s.appendChild(el('div', 'hint', '<b>LUT</b> load a 3D .cube file (from your camera maker or a colour pack)')); const lr = el('div', 'btnrow'); s.appendChild(lr);
  const fi = el('input'); fi.type = 'file'; fi.accept = '.cube'; fi.style.display = 'none'; lr.appendChild(fi); const lb = el('button', '', g0.lut && g0.lut.n ? 'CHANGE LUT (' + esc(g0.lut.name || 'loaded') + ')' : 'LOAD .CUBE LUT'); lb.onclick = () => fi.click(); lr.appendChild(lb);
  fi.onchange = () => { const f = fi.files[0]; if (!f) return; const rd = new FileReader(); rd.onload = () => { try { const L = PX.parseCube(rd.result); PX.begin(); G().lut = { n: L.n, b64: L.b64, name: f.name.replace(/\.cube$/i, '') }; if (G().lutMix == null) G().lutMix = 100; PX.commit('lut'); PX.toast('LUT "' + f.name + '" loaded (' + L.n + ' points a side).'); UI.openPanel('colour'); } catch (e) { PX.toast(e.message, true); } }; rd.readAsText(f); };
  if (g0.lut && g0.lut.n) { slider(s, 'LUT strength', () => g0.lutMix == null ? 100 : g0.lutMix, v => G().lutMix = v, 0, 100); const rm = el('button', '', 'REMOVE LUT'); rm.onclick = () => { PX.begin(); delete G().lut; PX.commit('lut'); UI.openPanel('colour'); }; lr.appendChild(rm); }
  const r2 = el('div', 'btnrow'); s.appendChild(r2); const cp = el('button', '', 'COPY GRADE TO ALL CLIPS'); cp.title = 'Every video and picture clip gets this grade'; cp.onclick = () => { PX.begin(); const gr = JSON.stringify(c.grade || {}); for (const o of PX.P.clips) if (o !== c && (o.type === 'video' || o.type === 'image')) o.grade = JSON.parse(gr); PX.commit('grade all'); PX.toast('The grade is on every clip.'); }; r2.appendChild(cp); const rs = el('button', '', 'RESET GRADE'); rs.onclick = () => { PX.begin(); delete c.grade; PX.commit('grade'); UI.openPanel('colour'); }; r2.appendChild(rs);
}
function curve(host, c) { const cv = mk(200, 200); cv.className = 'vcurve'; host.appendChild(cv); const pts = () => ((c.grade && c.grade.curve && c.grade.curve.m) || [[0, 0], [1, 1]]);
  const draw = () => { const g = cv.getContext('2d'); g.clearRect(0, 0, 200, 200); g.fillStyle = '#0b0912'; g.fillRect(0, 0, 200, 200); g.strokeStyle = '#2a2040'; for (let i = 1; i < 4; i++) { g.beginPath(); g.moveTo(i * 50, 0); g.lineTo(i * 50, 200); g.moveTo(0, i * 50); g.lineTo(200, i * 50); g.stroke(); } const l = curveLut(pts()); g.strokeStyle = '#fff'; g.lineWidth = 2; g.beginPath(); for (let v = 0; v < 256; v++) { const x = v / 255 * 200, y = 200 - l[v] / 255 * 200; v ? g.lineTo(x, y) : g.moveTo(x, y); } g.stroke(); g.fillStyle = '#ff50c4'; for (const p of pts()) { g.beginPath(); g.arc(p[0] * 200, 200 - p[1] * 200, 5, 0, 7); g.fill(); } };
  cv.onmousedown = (e) => { const r = cv.getBoundingClientRect(), at = (ev) => [clamp((ev.clientX - r.left) / r.width, 0, 1), clamp(1 - (ev.clientY - r.top) / r.height, 0, 1)]; const [x, y] = at(e); PX.begin(); c.grade = c.grade || {}; c.grade.curve = c.grade.curve || {}; let list = pts().map(p => p.slice()); let i = list.findIndex(p => Math.hypot(p[0] - x, p[1] - y) < 0.06); if (i < 0) { list.push([x, y]); list.sort((a, b) => a[0] - b[0]); i = list.findIndex(p => p[0] === x); } c.grade.curve.m = list; draw(); PX.redraw();
    const mv = (ev) => { const [qx, qy] = at(ev); const l2 = c.grade.curve.m; const lo = i > 0 ? l2[i - 1][0] + 0.01 : 0, hi = i < l2.length - 1 ? l2[i + 1][0] - 0.01 : 1; l2[i] = [clamp(qx, lo, hi), qy]; draw(); PX.redraw(); }; const up = () => { removeEventListener('mousemove', mv); removeEventListener('mouseup', up); PX.commit('curve'); }; addEventListener('mousemove', mv); addEventListener('mouseup', up); };
  cv.ondblclick = (e) => { const r = cv.getBoundingClientRect(), x = (e.clientX - r.left) / r.width, y = 1 - (e.clientY - r.top) / r.height; const l = pts().slice(); const i = l.findIndex(p => Math.hypot(p[0] - x, p[1] - y) < 0.06); if (i > 0 && i < l.length - 1) { PX.begin(); l.splice(i, 1); c.grade.curve.m = l; PX.commit('curve'); draw(); } }; draw(); }
function vfxPanel(b) {
  const c = UI.selected().find(x => x.type === 'video' || x.type === 'image' || x.type === 'text' || x.type === 'visual'); const s = sec(b, 'VIDEO EFFECTS');
  if (!c) { s.appendChild(el('p', 'hint', 'Select a clip to add effects to it.')); return; }
  const V = () => { c.vfx = c.vfx || {}; return c.vfx; }, v0 = c.vfx || {};
  for (const [k, n, mn, mx] of VFX) slider(s, n, () => v0[k] || 0, v => V()[k] = v, mn, mx);
  const md = el('div', 'f', '<label>Mirror</label><select><option value="none">None</option><option value="h">Left to right</option><option value="v">Top to bottom</option><option value="quad">Four ways</option></select>'); s.appendChild(md); const ms = $('select', md); ms.value = v0.mirror || 'none'; ms.onchange = () => { PX.begin(); V().mirror = ms.value; PX.commit('vfx'); };
  slider(s, 'Kaleidoscope (segments)', () => v0.kaleido || 0, v => V().kaleido = v, 0, 12);
  const r = el('div', 'btnrow'); s.appendChild(r); const rs = el('button', '', 'CLEAR EFFECTS'); rs.onclick = () => { PX.begin(); delete c.vfx; PX.commit('vfx'); UI.openPanel('effects'); }; r.appendChild(rs);
  // keyframe easing and reverse live here too
  const k = sec(b, 'KEYFRAMES AND PLAYBACK'); const ed = el('div', 'f', '<label>Keyframe easing</label><select>' + PX.EASES.map(e => '<option value="' + e[0] + '">' + e[1] + '</option>').join('') + '</select>'); k.appendChild(ed); const es = $('select', ed); es.value = c.kfEase || 'smooth'; es.onchange = () => { PX.begin(); c.kfEase = es.value; PX.commit('ease'); };
  if (c.type === 'video' || c.type === 'audio') { const rv = el('div', 'f', '<label>Play backwards</label><input type="checkbox" style="justify-self:start">'); k.appendChild(rv); const cb = $('input', rv); cb.checked = !!c.reverse; cb.onchange = () => { PX.begin(); c.reverse = cb.checked; PX.commit('reverse'); PX.seek(PX.play.t); }; }
}
const origOpen = UI.openPanel; UI.openPanel = (k) => { origOpen(k); const body = $('#vPopB'); if (!body) return;
  if (k === 'colour') gradePanel(body);
  if (k === 'effects') vfxPanel(body);
  if (k === 'stickers') stickerLib(body);
  if (k === 'edit' || k === 'media') editModes(body);
  if (k === 'settings') { const r = el('div', 'btnrow'); body.appendChild(r); const b2 = el('button', '', 'KEYBOARD SHORTCUTS (?)'); b2.onclick = UI.shortcuts; r.appendChild(b2); }
};
function editModes(b) { const box = el('div', 'veditmode'); box.id = 'vEditMode'; box.innerHTML = '<span class="hint">New clips:</span>'; for (const [m, n, tip] of [['normal', 'NORMAL', 'Put new clips where there is room'], ['insert', 'INSERT (I)', 'Push the rest of the track along to make room'], ['overwrite', 'OVERWRITE (O)', 'Cover whatever is there']]) { const x = el('button', UI.editMode === m ? 'on' : '', n); x.dataset.m = m; x.title = tip; x.onclick = () => UI.setEditMode(m); box.appendChild(x); } b.insertBefore(box, b.firstChild); }
function stickerLib(b) { const C = PX.Cover; if (!C || !C.stickerLib) return; const box = el('div'); b.insertBefore(box, b.firstChild); box.innerHTML = '<div class="hint"><b>STICKER LIBRARY</b> ' + C.stickerLib.list.length + ' Purphex stickers: they go on top of your video at the playhead (add an animation in ANIMATION)</div><input class="stkq" placeholder="Search stickers"><div class="vstkgrid"></div>';
  const q = $('input', box), g = $('.vstkgrid', box); const fill = () => { g.innerHTML = ''; const w = q.value.trim().toLowerCase(); const list = C.stickerLib.list.filter(d => !w || w.split(/\s+/).every(t => d.tags.includes(t))).slice(0, 120);
    for (const d of list) { const cv = mk(52, 52), x = cv.getContext('2d'); x.translate(26, 26); x.scale(0.46, 0.46); try { d.fn(x, '#b46cff', '#ffd23c'); } catch (e) { } const c = el('div', 'card'); c.title = d.name; c.appendChild(cv); c.onclick = () => { const big = mk(720, Math.round(720 * Math.min(1, Math.max(0.3, d.aspect)))); const g2 = big.getContext('2d'); g2.translate(big.width / 2, big.height / 2); g2.scale(7, 7); d.fn(g2, '#b46cff', '#ffd23c'); UI.addOverlayCanvas(big, d.name); }; g.appendChild(c); } if (!list.length) g.innerHTML = '<div class="hint">Nothing matches.</div>'; };
  q.oninput = fill; fill(); }
})();
