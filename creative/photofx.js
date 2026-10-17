'use strict';
// =====================================================================
//  PURPHEX MUSIC PHOTO EDITOR, part 2: PicsArt-style tools. Cutout (remove the background, magic eraser, erase / restore brush),
//  photo effects (glitch, RGB split, pixelate, halftone, sketch, cartoon, posterize, emboss, noise), pro adjustments (exposure,
//  highlights, shadows, tint, clarity, sharpen), collages, sticker packs drawn here, a crop tool with aspect ratios, alignment and
//  snapping, zoom, and layers you can drag into order. Everything runs on this PC; every change stays editable and is saved.
// =====================================================================
(() => {
const C = PX.Cover, L0 = C.lib, clamp = PX.clamp, uid = PX.uid;
const esc = (s) => String(s == null ? '' : s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const sel = () => C.art && C.art.layers.find(l => l.id === C.sel);
const rnd = (seed) => { let a = seed >>> 0; return () => { a |= 0; a = a + 0x6D2B79F5 | 0; let t = Math.imul(a ^ a >>> 15, 1 | a); t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t; return ((t ^ t >>> 14) >>> 0) / 4294967296; }; };
const hash = (s) => { let h = 2166136261; for (let i = 0; i < s.length; i++) { h ^= s.charCodeAt(i); h = Math.imul(h, 16777619); } return h >>> 0; };

// ---------------- the pixel work ----------------
function boxBlur(src, w, h, r) {   // one channel, separable box blur
  if (r < 1) return src.slice(); const tmp = new Float32Array(w * h), out = new Float32Array(w * h), d = r * 2 + 1;
  for (let y = 0; y < h; y++) { let acc = 0; const o = y * w; for (let x = -r; x <= r; x++) acc += src[o + clamp(x, 0, w - 1)]; for (let x = 0; x < w; x++) { tmp[o + x] = acc / d; acc += src[o + Math.min(w - 1, x + r + 1)] - src[o + Math.max(0, x - r)]; } }
  for (let x = 0; x < w; x++) { let acc = 0; for (let y = -r; y <= r; y++) acc += tmp[clamp(y, 0, h - 1) * w + x]; for (let y = 0; y < h; y++) { out[y * w + x] = acc / d; acc += tmp[Math.min(h - 1, y + r + 1) * w + x] - tmp[Math.max(0, y - r) * w + x]; } }
  return out;
}
const chans = (d, n) => { const R = new Float32Array(n), G = new Float32Array(n), B = new Float32Array(n); for (let i = 0, j = 0; i < n; i++, j += 4) { R[i] = d[j]; G[i] = d[j + 1]; B[i] = d[j + 2]; } return [R, G, B]; };
const back = (d, R, G, B, n) => { for (let i = 0, j = 0; i < n; i++, j += 4) { d[j] = R[i]; d[j + 1] = G[i]; d[j + 2] = B[i]; } };
const has = (p, k) => p && p[k] != null && p[k] !== 0 && p[k] !== false;
// the colour and look changes on an ImageData (the same for one photo and for the whole picture)
function applyPx(id, p, seed) {
  if (!p) return; const d = id.data, w = id.width, h = id.height, n = w * h; let [R, G, B] = chans(d, n);
  if (has(p, 'exposure') || has(p, 'highlights') || has(p, 'shadows') || has(p, 'tint')) {
    const m = Math.pow(2, (p.exposure || 0) / 50), hl = (p.highlights || 0) / 100, sh = (p.shadows || 0) / 100, ti = (p.tint || 0) / 100;
    for (let i = 0; i < n; i++) { let r = R[i] * m, g = G[i] * m, b = B[i] * m; const l = clamp((0.299 * r + 0.587 * g + 0.114 * b) / 255, 0, 1); const add = sh * (1 - l) * (1 - l) * 90 + hl * l * l * 90; r += add; g += add; b += add; g -= ti * 40; r += ti * 20; b += ti * 20; R[i] = r; G[i] = g; B[i] = b; }
  }
  for (const [key, rad] of [['clarity', Math.max(2, Math.round(Math.min(w, h) / 90))], ['sharpen', 1]]) {
    if (!has(p, key)) continue; const amt = p[key] / 100 * (key === 'sharpen' ? 1.6 : 0.9); const bR = boxBlur(R, w, h, rad), bG = boxBlur(G, w, h, rad), bB = boxBlur(B, w, h, rad);
    for (let i = 0; i < n; i++) { R[i] += amt * (R[i] - bR[i]); G[i] += amt * (G[i] - bG[i]); B[i] += amt * (B[i] - bB[i]); }
  }
  if (has(p, 'sketch')) { const gr = new Float32Array(n); for (let i = 0; i < n; i++) gr[i] = 0.299 * R[i] + 0.587 * G[i] + 0.114 * B[i]; const inv = new Float32Array(n); for (let i = 0; i < n; i++) inv[i] = 255 - gr[i]; const bl = boxBlur(boxBlur(inv, w, h, 3), w, h, 3); const k = p.sketch / 100;
    for (let i = 0; i < n; i++) { const v = Math.min(255, gr[i] * 255 / Math.max(1, 255 - bl[i])); R[i] = R[i] * (1 - k) + v * k; G[i] = G[i] * (1 - k) + v * k; B[i] = B[i] * (1 - k) + v * k; } }
  if (has(p, 'cartoon')) { const lv = 6, st = 255 / (lv - 1), gr = new Float32Array(n); for (let i = 0; i < n; i++) gr[i] = 0.299 * R[i] + 0.587 * G[i] + 0.114 * B[i]; const sm = [boxBlur(R, w, h, 2), boxBlur(G, w, h, 2), boxBlur(B, w, h, 2)]; const k = p.cartoon / 100;
    for (let y = 1; y < h - 1; y++) for (let x = 1; x < w - 1; x++) { const i = y * w + x; const gx = gr[i + 1] - gr[i - 1], gy = gr[i + w] - gr[i - w], e = Math.hypot(gx, gy) > 60 - k * 30 ? 0.15 : 1; R[i] = R[i] * (1 - k) + Math.round(sm[0][i] / st) * st * e * k; G[i] = G[i] * (1 - k) + Math.round(sm[1][i] / st) * st * e * k; B[i] = B[i] * (1 - k) + Math.round(sm[2][i] / st) * st * e * k; } }
  if (has(p, 'emboss')) { const k = p.emboss / 100, gr = new Float32Array(n); for (let i = 0; i < n; i++) gr[i] = 0.299 * R[i] + 0.587 * G[i] + 0.114 * B[i]; for (let y = 1; y < h - 1; y++) for (let x = 1; x < w - 1; x++) { const i = y * w + x, v = 128 + (gr[i + w + 1] - gr[i - w - 1]) * 2; R[i] = R[i] * (1 - k) + v * k; G[i] = G[i] * (1 - k) + v * k; B[i] = B[i] * (1 - k) + v * k; } }
  if (has(p, 'posterize')) { const lv = Math.max(2, Math.round(24 - p.posterize * 0.22)), st = 255 / (lv - 1); for (let i = 0; i < n; i++) { R[i] = Math.round(R[i] / st) * st; G[i] = Math.round(G[i] / st) * st; B[i] = Math.round(B[i] / st) * st; } }
  if (has(p, 'noise')) { const r = rnd(seed + 7), a = p.noise * 0.9; for (let i = 0; i < n; i++) { const v = (r() - 0.5) * a; R[i] += v; G[i] += v; B[i] += v; } }
  if (has(p, 'pixelate')) { const bs = Math.max(2, Math.round(p.pixelate / 100 * Math.max(w, h) / 22)); for (let by = 0; by < h; by += bs) for (let bx = 0; bx < w; bx += bs) { let sr = 0, sg = 0, sb = 0, c = 0; for (let y = by; y < Math.min(h, by + bs); y++) for (let x = bx; x < Math.min(w, bx + bs); x++) { const i = y * w + x; sr += R[i]; sg += G[i]; sb += B[i]; c++; } sr /= c; sg /= c; sb /= c; for (let y = by; y < Math.min(h, by + bs); y++) for (let x = bx; x < Math.min(w, bx + bs); x++) { const i = y * w + x; R[i] = sr; G[i] = sg; B[i] = sb; } } }
  if (has(p, 'rgbsplit') || has(p, 'glitch')) {
    const o = Math.round(((p.rgbsplit || 0) + (p.glitch || 0) * 0.6) / 100 * w / 40), R2 = R.slice(), B2 = B.slice();
    for (let y = 0; y < h; y++) for (let x = 0; x < w; x++) { const i = y * w + x; R[i] = R2[y * w + clamp(x + o, 0, w - 1)]; B[i] = B2[y * w + clamp(x - o, 0, w - 1)]; }
    if (has(p, 'glitch')) { const r = rnd(seed + 3), bands = 6 + Math.round(p.glitch / 8); for (let b = 0; b < bands; b++) { const y0 = Math.floor(r() * h), bh = Math.max(2, Math.floor(r() * h / 18)), sh = Math.round((r() - 0.5) * w * p.glitch / 250); const cR = R.slice(), cG = G.slice(), cB = B.slice(); for (let y = y0; y < Math.min(h, y0 + bh); y++) for (let x = 0; x < w; x++) { const i = y * w + x, j = y * w + clamp(x - sh, 0, w - 1); R[i] = cR[j]; G[i] = cG[j]; B[i] = cB[j]; } } }
  }
  back(d, R, G, B, n); for (let i = 0; i < d.length; i += 4) { d[i] = clamp(d[i], 0, 255); }
}
// halftone dots are drawn, not computed per pixel
function halftone(cv, amt) { const g = cv.getContext('2d'), w = cv.width, h = cv.height, src = g.getImageData(0, 0, w, h).data, s = Math.max(4, Math.round(Math.max(w, h) / (120 - amt * 0.9))); const out = document.createElement('canvas'); out.width = w; out.height = h; const o = out.getContext('2d'); o.fillStyle = '#fff'; o.fillRect(0, 0, w, h); o.fillStyle = '#111';
  for (let y = 0; y < h; y += s) for (let x = 0; x < w; x += s) { const i = (Math.min(h - 1, y + (s >> 1)) * w + Math.min(w - 1, x + (s >> 1))) * 4, l = (0.299 * src[i] + 0.587 * src[i + 1] + 0.114 * src[i + 2]) / 255; const r = (1 - l) * s * 0.62; if (r > 0.3) { o.beginPath(); o.arc(x + s / 2, y + s / 2, r, 0, 7); o.fill(); } }
  g.globalCompositeOperation = 'multiply'; g.drawImage(out, 0, 0); g.globalCompositeOperation = 'source-over'; }
// ---- cutout: a mask (255 keep, 0 gone) from the automatic background removal, the magic eraser points and the brush strokes ----
const cdist = (d, i, r, g, b) => Math.abs(d[i] - r) + Math.abs(d[i + 1] - g) + Math.abs(d[i + 2] - b);
function flood(d, w, h, mask, seeds, near) { const st = []; for (const s of seeds) { if (mask[s] && near(s)) { mask[s] = 0; st.push(s); } } while (st.length) { const i = st.pop(), x = i % w, y = (i / w) | 0; if (x > 0 && mask[i - 1] && near(i - 1)) { mask[i - 1] = 0; st.push(i - 1); } if (x < w - 1 && mask[i + 1] && near(i + 1)) { mask[i + 1] = 0; st.push(i + 1); } if (y > 0 && mask[i - w] && near(i - w)) { mask[i - w] = 0; st.push(i - w); } if (y < h - 1 && mask[i + w] && near(i + w)) { mask[i + w] = 0; st.push(i + w); } } }
function cutMask(id, cut) {
  const d = id.data, w = id.width, h = id.height, n = w * h, mask = new Uint8Array(n).fill(1);
  if (cut.auto) {   // the background: the colours along the edges (up to 4 groups), filled in from the edges while the colour stays close
    const samp = []; for (let x = 0; x < w; x += Math.max(1, (w / 60) | 0)) { samp.push(x, (h - 1) * w + x); } for (let y = 0; y < h; y += Math.max(1, (h / 60) | 0)) { samp.push(y * w, y * w + w - 1); }
    const cents = []; for (const s of samp) { const j = s * 4; if (d[j + 3] < 10) continue; if (!cents.some(c => Math.abs(c[0] - d[j]) + Math.abs(c[1] - d[j + 1]) + Math.abs(c[2] - d[j + 2]) < 60)) cents.push([d[j], d[j + 1], d[j + 2]]); if (cents.length >= 4) break; }
    const tol = (cut.tol == null ? 40 : cut.tol) * 2.2; flood(d, w, h, mask, samp, (i) => { const j = i * 4; if (d[j + 3] < 10) return true; for (const c of cents) if (cdist(d, j, c[0], c[1], c[2]) < tol) return true; return false; });
  }
  for (const pt of cut.pts || []) { const x = clamp(Math.round(pt[0] * w), 0, w - 1), y = clamp(Math.round(pt[1] * h), 0, h - 1), s = y * w + x, j = s * 4, r = d[j], g = d[j + 1], b = d[j + 2], tol = (pt[2] || 40) * 2.2; flood(d, w, h, mask, [s], (i) => cdist(d, i * 4, r, g, b) < tol); }
  const mc = document.createElement('canvas'); mc.width = w; mc.height = h; const mg = mc.getContext('2d'), mi = mg.createImageData(w, h); for (let i = 0; i < n; i++) { mi.data[i * 4 + 3] = mask[i] ? 255 : 0; } mg.putImageData(mi, 0, 0);
  if (cut.feather > 0) { const f = document.createElement('canvas'); f.width = w; f.height = h; const fg = f.getContext('2d'); fg.filter = 'blur(' + (cut.feather * Math.max(w, h) / 1000) + 'px)'; fg.drawImage(mc, 0, 0); mg.clearRect(0, 0, w, h); mg.drawImage(f, 0, 0); }
  mg.lineCap = 'round'; mg.lineJoin = 'round';
  for (const s of cut.strokes || []) { mg.globalCompositeOperation = s.restore ? 'source-over' : 'destination-out'; mg.strokeStyle = '#000'; mg.lineWidth = s.size / 1000 * Math.max(w, h); mg.beginPath(); s.pts.forEach((p, i) => i ? mg.lineTo(p[0] * w, p[1] * h) : mg.moveTo(p[0] * w, p[1] * h)); if (s.pts.length === 1) mg.lineTo(s.pts[0][0] * w + 0.1, s.pts[0][1] * h); mg.stroke(); }
  mg.globalCompositeOperation = 'source-over'; return mc;
}
const cutOn = (c) => c && (c.auto || (c.pts && c.pts.length) || (c.strokes && c.strokes.length));
const pxOn = (p) => p && Object.keys(p).some(k => p[k]);
// the photo after its effects and cutout, made once and kept (the editor redraws many times a second)
const cache = new Map();
C.pxSource = (L, im) => {
  if (!pxOn(L.px) && !cutOn(L.cut)) return null; const key = L.url + '|' + JSON.stringify(L.px || {}) + '|' + JSON.stringify(L.cut || {}); let e = cache.get(key); if (e) return e;
  const s = Math.min(1, 2400 / Math.max(im.naturalWidth, im.naturalHeight)), w = Math.max(1, Math.round(im.naturalWidth * s)), h = Math.max(1, Math.round(im.naturalHeight * s));
  const cv = document.createElement('canvas'); cv.width = w; cv.height = h; const g = cv.getContext('2d'); g.drawImage(im, 0, 0, w, h);
  if (pxOn(L.px)) { const id = g.getImageData(0, 0, w, h); applyPx(id, L.px, hash(L.id)); g.putImageData(id, 0, 0); if (L.px.halftone) halftone(cv, L.px.halftone); }
  if (cutOn(L.cut)) { const id0 = (() => { const t = document.createElement('canvas'); t.width = w; t.height = h; const tg = t.getContext('2d'); tg.drawImage(im, 0, 0, w, h); return tg.getImageData(0, 0, w, h); })(); const m = cutMask(id0, L.cut); g.globalCompositeOperation = 'destination-in'; g.drawImage(m, 0, 0); g.globalCompositeOperation = 'source-over'; }
  e = { c: cv, s }; cache.set(key, e); if (cache.size > 14) cache.delete(cache.keys().next().value); return e;
};
// the whole picture's pro adjustments and effects (after every layer is drawn)
C.postFx = (g, W, H, G) => { if (!pxOn(G.px)) return; const id = g.getImageData(0, 0, W, H); applyPx(id, G.px, 99); g.putImageData(id, 0, 0); if (G.px.halftone) halftone(g.canvas, G.px.halftone); };

// ---------------- sticker packs (drawn here, every one original, colours and words editable) ----------------
const VST = [['label', 'Label', 'NEW'], ['outnow', 'OUT NOW ribbon', 'OUT NOW'], ['banner', 'Banner', 'NEW MUSIC'], ['live', 'LIVE badge', 'LIVE'], ['explicit', 'Explicit tag', 'EXPLICIT'], ['burst', 'Starburst', 'NEW!'], ['speech', 'Speech bubble', 'HEY!'], ['thought', 'Thought bubble', 'hmm...'], ['tape', 'Tape', ''], ['sparkle', 'Sparkle', ''], ['sparkles', 'Sparkles', ''], ['arrow', 'Scribble arrow', ''], ['circle', 'Scribble circle', ''], ['underline', 'Scribble underline', ''], ['heart', 'Heart', ''], ['vinyl', 'Vinyl record', ''], ['cassette', 'Cassette', ''], ['headphones', 'Headphones', ''], ['mic', 'Microphone', ''], ['waveform', 'Waveform', ''], ['eq', 'EQ bars', ''], ['player', 'Music player', 'Song title'], ['play', 'Play button', ''], ['barcode', 'Barcode', ''], ['ticket', 'Ticket', 'ADMIT ONE'], ['pricetag', 'Price tag', 'FREE'], ['pin', 'Location pin', ''], ['verified', 'Verified tick', ''], ['notif', 'Notification', '99+'], ['likes', 'Likes', '1.2M'], ['lightning', 'Lightning', ''], ['smiley', 'Smiley', ''], ['crown2', 'Doodle crown', ''], ['flame', 'Flame', ''], ['stars3', 'Three stars', '']];
const ASPECT = { label: 0.42, outnow: 0.32, banner: 0.3, live: 1, explicit: 0.5, burst: 1, speech: 0.8, thought: 0.8, tape: 0.28, sparkle: 1, sparkles: 1, arrow: 0.6, circle: 0.7, underline: 0.25, heart: 0.9, vinyl: 1, cassette: 0.64, headphones: 0.9, mic: 1.6, waveform: 0.4, eq: 0.7, player: 0.36, play: 1, barcode: 0.5, ticket: 0.45, pricetag: 0.5, pin: 1.3, verified: 1, notif: 0.7, likes: 0.45, lightning: 1.4, smiley: 1, crown2: 0.7, flame: 1.3, stars3: 0.4 };
function rr(g, x, y, w, h, r) { g.beginPath(); if (g.roundRect) g.roundRect(x, y, w, h, r); else g.rect(x, y, w, h); }
function vsDraw(g, L, W, H, k) {
  const S = L.size * k, A = ASPECT[L.kind] || 1, w = S, h = S * A, c = L.color || '#ff50c4', c2 = L.color2 || '#ffffff', t = L.text || '', r = rnd(hash(L.id));
  g.lineCap = 'round'; g.lineJoin = 'round'; const font = (sz, wt) => { g.font = (wt || 900) + ' ' + sz + 'px "' + (L.font || 'Segoe UI') + '"'; g.textAlign = 'center'; g.textBaseline = 'middle'; };
  const text = (sz, col, y, wt) => { if (!t) return; font(sz, wt); let s2 = sz; while (g.measureText(t).width > w * 0.84 && s2 > 6) { s2 *= 0.9; font(s2, wt); } g.fillStyle = col; g.fillText(t, 0, y || 0); };
  switch (L.kind) {
    case 'label': rr(g, -w / 2, -h / 2, w, h, h * 0.22); g.fillStyle = c; g.fill(); text(h * 0.55, c2); break;
    case 'outnow': g.fillStyle = c; g.beginPath(); g.moveTo(-w / 2, -h / 2); g.lineTo(w / 2, -h / 2); g.lineTo(w / 2 - h * 0.35, 0); g.lineTo(w / 2, h / 2); g.lineTo(-w / 2, h / 2); g.lineTo(-w / 2 + h * 0.35, 0); g.closePath(); g.fill(); text(h * 0.5, c2); break;
    case 'banner': g.fillStyle = c; g.beginPath(); g.moveTo(-w / 2, -h / 2); g.quadraticCurveTo(0, -h * 0.9, w / 2, -h / 2); g.lineTo(w / 2, h / 2); g.quadraticCurveTo(0, h * 0.1, -w / 2, h / 2); g.closePath(); g.fill(); text(h * 0.42, c2, -h * 0.08); break;
    case 'live': g.fillStyle = c; g.beginPath(); g.arc(0, 0, w / 2, 0, 7); g.fill(); g.strokeStyle = c2; g.lineWidth = w * 0.04; g.beginPath(); g.arc(0, 0, w * 0.42, 0, 7); g.stroke(); g.fillStyle = c2; g.beginPath(); g.arc(0, -h * 0.22, w * 0.06, 0, 7); g.fill(); text(w * 0.22, c2, h * 0.08); break;
    case 'explicit': g.fillStyle = '#000'; g.fillRect(-w / 2, -h / 2, w, h); g.fillStyle = '#fff'; g.fillRect(-w / 2 + w * 0.04, -h / 2 + h * 0.08, w * 0.92, h * 0.84); g.fillStyle = '#000'; g.fillRect(-w / 2 + w * 0.08, -h / 2 + h * 0.16, w * 0.84, h * 0.68); text(h * 0.42, '#fff'); break;
    case 'burst': { g.fillStyle = c; g.beginPath(); const n = 16; for (let i = 0; i < n * 2; i++) { const a = i / (n * 2) * Math.PI * 2, rr2 = (i % 2 ? 0.38 : 0.5) * w; g.lineTo(Math.cos(a) * rr2, Math.sin(a) * rr2); } g.closePath(); g.fill(); text(w * 0.24, c2); break; }
    case 'speech': rr(g, -w / 2, -h / 2, w, h * 0.78, h * 0.25); g.moveTo(-w * 0.2, h * 0.27); g.lineTo(-w * 0.32, h / 2); g.lineTo(-w * 0.02, h * 0.27); g.fillStyle = c2; g.fill(); g.strokeStyle = c; g.lineWidth = w * 0.03; g.stroke(); text(h * 0.32, c, -h * 0.11); break;
    case 'thought': g.fillStyle = c2; g.strokeStyle = c; g.lineWidth = w * 0.025; for (const [x, y, rad] of [[0, -h * 0.1, h * 0.36], [-w * 0.26, -h * 0.05, h * 0.26], [w * 0.26, -h * 0.05, h * 0.26], [-w * 0.3, h * 0.38, h * 0.06], [-w * 0.38, h * 0.48, h * 0.035]]) { g.beginPath(); g.ellipse(x, y, rad * 1.4, rad, 0, 0, 7); g.fill(); g.stroke(); } text(h * 0.26, c, -h * 0.08, 600); break;
    case 'tape': { g.fillStyle = c; g.globalAlpha *= 0.75; g.beginPath(); g.moveTo(-w / 2, -h / 2); for (let i = 0; i <= 6; i++) g.lineTo(w / 2 + (i % 2 ? -w * 0.02 : 0), -h / 2 + i * h / 6); g.lineTo(-w / 2, h / 2); for (let i = 6; i >= 0; i--) g.lineTo(-w / 2 + (i % 2 ? w * 0.02 : 0), -h / 2 + i * h / 6); g.fill(); break; }
    case 'sparkle': g.fillStyle = c; g.beginPath(); g.moveTo(0, -h / 2); g.quadraticCurveTo(0, 0, w / 2, 0); g.quadraticCurveTo(0, 0, 0, h / 2); g.quadraticCurveTo(0, 0, -w / 2, 0); g.quadraticCurveTo(0, 0, 0, -h / 2); g.fill(); break;
    case 'sparkles': for (const [x, y, s] of [[0, 0, 0.55], [-0.3, -0.3, 0.3], [0.32, 0.28, 0.25]]) { const ww = w * s; g.fillStyle = s > 0.4 ? c : c2; g.beginPath(); g.moveTo(x * w, y * w - ww / 2); g.quadraticCurveTo(x * w, y * w, x * w + ww / 2, y * w); g.quadraticCurveTo(x * w, y * w, x * w, y * w + ww / 2); g.quadraticCurveTo(x * w, y * w, x * w - ww / 2, y * w); g.quadraticCurveTo(x * w, y * w, x * w, y * w - ww / 2); g.fill(); } break;
    case 'arrow': g.strokeStyle = c; g.lineWidth = w * 0.045; g.beginPath(); g.moveTo(-w / 2, h * 0.3); g.bezierCurveTo(-w * 0.2, -h * 0.6, w * 0.2, h * 0.5, w * 0.42, -h * 0.25); g.stroke(); g.beginPath(); g.moveTo(w * 0.3, -h * 0.32); g.lineTo(w * 0.45, -h * 0.28); g.lineTo(w * 0.4, -h * 0.08); g.stroke(); break;
    case 'circle': g.strokeStyle = c; g.lineWidth = w * 0.035; g.beginPath(); for (let i = 0; i <= 70; i++) { const a = i / 60 * Math.PI * 2 - 0.4, j = 1 + Math.sin(i * 0.7) * 0.03; g.lineTo(Math.cos(a) * w / 2 * j * (1 - i / 700), Math.sin(a) * h / 2 * j); } g.stroke(); break;
    case 'underline': g.strokeStyle = c; g.lineWidth = h * 0.22; g.beginPath(); g.moveTo(-w / 2, 0); g.bezierCurveTo(-w * 0.1, -h * 0.4, w * 0.1, h * 0.4, w / 2, -h * 0.1); g.stroke(); break;
    case 'heart': g.fillStyle = c; g.beginPath(); g.moveTo(0, h / 2); g.bezierCurveTo(-w * 0.65, -h * 0.05, -w * 0.3, -h * 0.6, 0, -h * 0.22); g.bezierCurveTo(w * 0.3, -h * 0.6, w * 0.65, -h * 0.05, 0, h / 2); g.fill(); break;
    case 'vinyl': g.fillStyle = '#111'; g.beginPath(); g.arc(0, 0, w / 2, 0, 7); g.fill(); g.strokeStyle = '#2c2c2c'; g.lineWidth = w * 0.006; for (let i = 0.22; i < 0.49; i += 0.03) { g.beginPath(); g.arc(0, 0, w * i, 0, 7); g.stroke(); } g.fillStyle = c; g.beginPath(); g.arc(0, 0, w * 0.18, 0, 7); g.fill(); g.fillStyle = '#000'; g.beginPath(); g.arc(0, 0, w * 0.02, 0, 7); g.fill(); break;
    case 'cassette': rr(g, -w / 2, -h / 2, w, h, w * 0.05); g.fillStyle = c; g.fill(); g.fillStyle = c2; g.fillRect(-w * 0.42, -h * 0.38, w * 0.84, h * 0.3); rr(g, -w * 0.3, -h * 0.02, w * 0.6, h * 0.3, h * 0.1); g.fillStyle = '#222'; g.fill(); g.fillStyle = c2; for (const x of [-0.18, 0.18]) { g.beginPath(); g.arc(x * w, h * 0.13, h * 0.09, 0, 7); g.fill(); } break;
    case 'headphones': g.strokeStyle = c; g.lineWidth = w * 0.08; g.beginPath(); g.arc(0, h * 0.05, w * 0.38, Math.PI, 0); g.stroke(); g.fillStyle = c; rr(g, -w / 2, h * 0.02, w * 0.2, h * 0.42, w * 0.06); g.fill(); rr(g, w * 0.3, h * 0.02, w * 0.2, h * 0.42, w * 0.06); g.fill(); break;
    case 'mic': rr(g, -w * 0.3, -h / 2, w * 0.6, h * 0.5, w * 0.3); g.fillStyle = c; g.fill(); g.strokeStyle = c2; g.lineWidth = w * 0.03; for (let i = 1; i < 5; i++) { g.beginPath(); g.moveTo(-w * 0.28, -h / 2 + i * h * 0.1); g.lineTo(w * 0.28, -h / 2 + i * h * 0.1); g.stroke(); } g.fillStyle = '#333'; g.fillRect(-w * 0.07, 0, w * 0.14, h * 0.42); g.fillRect(-w * 0.25, h * 0.42, w * 0.5, h * 0.08); break;
    case 'waveform': g.fillStyle = c; for (let i = 0; i < 28; i++) { const x = -w / 2 + i * w / 28, a = (0.2 + 0.8 * Math.abs(Math.sin(i * 0.9) * Math.sin(i * 0.33 + 1))) * h; rr(g, x, -a / 2, w / 28 * 0.6, a, w / 120); g.fill(); } break;
    case 'eq': for (let i = 0; i < 7; i++) { const a = (0.3 + 0.7 * r()) * h; g.fillStyle = i % 2 ? c2 : c; rr(g, -w / 2 + i * w / 7 + w * 0.01, h / 2 - a, w / 7 * 0.8, a, w * 0.02); g.fill(); } break;
    case 'player': rr(g, -w / 2, -h / 2, w, h, h * 0.18); g.fillStyle = 'rgba(0,0,0,0.72)'; g.fill(); g.fillStyle = c; rr(g, -w / 2 + h * 0.12, -h * 0.38, h * 0.76, h * 0.76, h * 0.1); g.fill(); font(h * 0.2, 800); g.textAlign = 'left'; g.fillStyle = '#fff'; g.fillText(t || 'Song title', -w / 2 + h * 1.05, -h * 0.12); g.fillStyle = 'rgba(255,255,255,0.3)'; g.fillRect(-w / 2 + h * 1.05, h * 0.18, w * 0.5, h * 0.06); g.fillStyle = c; g.fillRect(-w / 2 + h * 1.05, h * 0.18, w * 0.22, h * 0.06); g.fillStyle = '#fff'; g.beginPath(); g.moveTo(w / 2 - h * 0.55, -h * 0.18); g.lineTo(w / 2 - h * 0.55, h * 0.18); g.lineTo(w / 2 - h * 0.25, 0); g.fill(); break;
    case 'play': g.fillStyle = c; g.beginPath(); g.arc(0, 0, w / 2, 0, 7); g.fill(); g.fillStyle = c2; g.beginPath(); g.moveTo(-w * 0.12, -h * 0.2); g.lineTo(-w * 0.12, h * 0.2); g.lineTo(w * 0.22, 0); g.fill(); break;
    case 'barcode': g.fillStyle = '#fff'; g.fillRect(-w / 2, -h / 2, w, h); g.fillStyle = '#000'; { let x = -w * 0.44; while (x < w * 0.44) { const bw = w * (0.006 + r() * 0.02); g.fillRect(x, -h * 0.4, bw, h * 0.62); x += bw + w * (0.006 + r() * 0.014); } } font(h * 0.14, 600); g.fillText(String(Math.floor(r() * 9e11)).padStart(12, '0'), 0, h * 0.34); break;
    case 'ticket': g.fillStyle = c; g.beginPath(); g.moveTo(-w / 2, -h / 2); g.lineTo(w / 2, -h / 2); g.lineTo(w / 2, -h * 0.1); g.arc(w / 2, 0, h * 0.1, -Math.PI / 2, Math.PI / 2, true); g.lineTo(w / 2, h / 2); g.lineTo(-w / 2, h / 2); g.lineTo(-w / 2, h * 0.1); g.arc(-w / 2, 0, h * 0.1, Math.PI / 2, -Math.PI / 2, true); g.closePath(); g.fill(); g.setLineDash([h * 0.06, h * 0.05]); g.strokeStyle = c2; g.lineWidth = w * 0.006; g.beginPath(); g.moveTo(w * 0.25, -h / 2); g.lineTo(w * 0.25, h / 2); g.stroke(); g.setLineDash([]); g.translate(-w * 0.1, 0); text(h * 0.3, c2); break;
    case 'pricetag': g.fillStyle = c; g.beginPath(); g.moveTo(-w / 2, 0); g.lineTo(-w * 0.28, -h / 2); g.lineTo(w / 2, -h / 2); g.lineTo(w / 2, h / 2); g.lineTo(-w * 0.28, h / 2); g.closePath(); g.fill(); g.fillStyle = c2; g.beginPath(); g.arc(-w * 0.3, 0, h * 0.09, 0, 7); g.fill(); g.translate(w * 0.08, 0); text(h * 0.42, c2); break;
    case 'pin': g.fillStyle = c; g.beginPath(); g.arc(0, -h * 0.15, w * 0.4, Math.PI * 0.85, Math.PI * 0.15); g.lineTo(0, h / 2); g.closePath(); g.fill(); g.fillStyle = c2; g.beginPath(); g.arc(0, -h * 0.15, w * 0.16, 0, 7); g.fill(); break;
    case 'verified': { g.fillStyle = c; g.beginPath(); const n = 12; for (let i = 0; i < n * 2; i++) { const a = i / (n * 2) * Math.PI * 2, rr2 = (i % 2 ? 0.43 : 0.5) * w; g.lineTo(Math.cos(a) * rr2, Math.sin(a) * rr2); } g.closePath(); g.fill(); g.strokeStyle = c2; g.lineWidth = w * 0.09; g.beginPath(); g.moveTo(-w * 0.2, 0); g.lineTo(-w * 0.04, w * 0.16); g.lineTo(w * 0.22, -w * 0.14); g.stroke(); break; }
    case 'notif': rr(g, -w / 2, -h / 2, w, h, h / 2); g.fillStyle = c; g.fill(); text(h * 0.55, c2); break;
    case 'likes': rr(g, -w / 2, -h / 2, w, h, h / 2); g.fillStyle = 'rgba(0,0,0,0.72)'; g.fill(); g.save(); g.translate(-w * 0.3, 0); g.fillStyle = c; g.beginPath(); const hs = h * 0.5; g.moveTo(0, hs * 0.45); g.bezierCurveTo(-hs * 0.65, 0, -hs * 0.3, -hs * 0.55, 0, -hs * 0.2); g.bezierCurveTo(hs * 0.3, -hs * 0.55, hs * 0.65, 0, 0, hs * 0.45); g.fill(); g.restore(); g.translate(w * 0.12, 0); text(h * 0.45, '#fff'); break;
    case 'lightning': g.fillStyle = c; g.beginPath(); for (const [x, y] of [[0.1, -0.5], [-0.35, 0.05], [-0.02, 0.05], [-0.15, 0.5], [0.35, -0.08], [0.03, -0.08]]) g.lineTo(x * w, y * h); g.closePath(); g.fill(); break;
    case 'smiley': g.fillStyle = c; g.beginPath(); g.arc(0, 0, w / 2, 0, 7); g.fill(); g.fillStyle = c2; for (const x of [-0.16, 0.16]) { g.beginPath(); g.ellipse(x * w, -w * 0.1, w * 0.05, w * 0.09, 0, 0, 7); g.fill(); } g.strokeStyle = c2; g.lineWidth = w * 0.05; g.beginPath(); g.arc(0, w * 0.02, w * 0.25, 0.2 * Math.PI, 0.8 * Math.PI); g.stroke(); break;
    case 'crown2': g.strokeStyle = c; g.lineWidth = w * 0.04; g.beginPath(); for (const [x, y] of [[-0.45, 0.35], [-0.45, -0.25], [-0.2, 0.05], [0, -0.45], [0.2, 0.05], [0.45, -0.25], [0.45, 0.35]]) g.lineTo(x * w, y * h * 1.3); g.closePath(); g.stroke(); break;
    case 'flame': g.fillStyle = c; g.beginPath(); g.moveTo(0, -h / 2); g.bezierCurveTo(w * 0.55, -h * 0.05, w * 0.4, h * 0.45, 0, h / 2); g.bezierCurveTo(-w * 0.4, h * 0.45, -w * 0.55, -h * 0.05, 0, -h / 2); g.fill(); g.fillStyle = c2; g.beginPath(); g.moveTo(0, -h * 0.05); g.bezierCurveTo(w * 0.28, h * 0.15, w * 0.2, h * 0.45, 0, h * 0.45); g.bezierCurveTo(-w * 0.2, h * 0.45, -w * 0.28, h * 0.15, 0, -h * 0.05); g.fill(); break;
    case 'stars3': for (let i = 0; i < 3; i++) { g.save(); g.translate((i - 1) * w * 0.33, 0); g.fillStyle = i === 1 ? c : c2; g.beginPath(); for (let j = 0; j < 10; j++) { const a = j / 10 * Math.PI * 2 - Math.PI / 2, r2 = (j % 2 ? 0.4 : 1) * h * 0.5; g.lineTo(Math.cos(a) * r2, Math.sin(a) * r2); } g.closePath(); g.fill(); g.restore(); } break;
  }
}
// ---- collage cells: a photo cropped into a box of the layout ----
function cellDraw(g, L, W, H, k) {
  const gap = (C.art.collageGap == null ? 16 : C.art.collageGap) * k, rad = (C.art.collageRadius || 0) * k; const x = L.rx * W + gap / 2, y = L.ry * H + gap / 2, w = L.rw * W - gap, h = L.rh * H - gap; if (w < 2 || h < 2) return;
  g.save(); rr(g, x, y, w, h, rad); g.clip();
  if (L.url) { const im = C.img(L.url); if (im.complete && im.naturalWidth) { const src = C.pxSource(L, im) || { c: im, s: 1 }; const ar = im.naturalWidth / im.naturalHeight, z = L.zoom || 1; let dw = w, dh = h; if (ar > w / h) dw = h * ar; else dh = w / ar; dw *= z; dh *= z; g.drawImage(src.c, x + (w - dw) / 2 + (L.ox || 0) * w, y + (h - dh) / 2 + (L.oy || 0) * h, dw, dh); } }
  else if (!C._exporting) { g.fillStyle = 'rgba(255,255,255,0.06)'; g.fillRect(x, y, w, h); g.strokeStyle = 'rgba(255,255,255,0.35)'; g.setLineDash([8, 6]); g.lineWidth = 2; g.strokeRect(x + 2, y + 2, w - 4, h - 4); g.setLineDash([]); g.fillStyle = 'rgba(255,255,255,0.6)'; g.font = '600 ' + Math.max(10, Math.min(w, h) * 0.09) + 'px "Segoe UI"'; g.textAlign = 'center'; g.textBaseline = 'middle'; g.fillText('+ PHOTO', x + w / 2, y + h / 2); }
  g.restore();
}
C.extraDrawAbs = { cell: cellDraw };
C.extraDraw = { vsticker: vsDraw };
C.boxTypes = ['text', 'image', 'shape', 'sticker', 'vsticker'];
C.extraBox = { vsticker: (L, W, H, k, cx, cy, s) => ({ cx, cy, w: L.size * k * s, h: L.size * k * s * (ASPECT[L.kind] || 1) }) };
// collage layouts: [name, cells as [x, y, w, h] in parts of the canvas]
const COL = [['Two side by side', [[0, 0, .5, 1], [.5, 0, .5, 1]]], ['Two stacked', [[0, 0, 1, .5], [0, .5, 1, .5]]], ['Three columns', [[0, 0, 1 / 3, 1], [1 / 3, 0, 1 / 3, 1], [2 / 3, 0, 1 / 3, 1]]], ['Three rows', [[0, 0, 1, 1 / 3], [0, 1 / 3, 1, 1 / 3], [0, 2 / 3, 1, 1 / 3]]], ['Big left + two', [[0, 0, .6, 1], [.6, 0, .4, .5], [.6, .5, .4, .5]]], ['Big top + two', [[0, 0, 1, .6], [0, .6, .5, .4], [.5, .6, .5, .4]]], ['Grid of four', [[0, 0, .5, .5], [.5, 0, .5, .5], [0, .5, .5, .5], [.5, .5, .5, .5]]], ['Big top + three', [[0, 0, 1, .6], [0, .6, 1 / 3, .4], [1 / 3, .6, 1 / 3, .4], [2 / 3, .6, 1 / 3, .4]]], ['Big middle', [[.2, .2, .6, .6], [0, 0, .2, .5], [0, .5, .2, .5], [.8, 0, .2, .5], [.8, .5, .2, .5]]], ['Grid of six', [[0, 0, 1 / 3, .5], [1 / 3, 0, 1 / 3, .5], [2 / 3, 0, 1 / 3, .5], [0, .5, 1 / 3, .5], [1 / 3, .5, 1 / 3, .5], [2 / 3, .5, 1 / 3, .5]]], ['Grid of nine', (() => { const a = []; for (let y = 0; y < 3; y++) for (let x = 0; x < 3; x++) a.push([x / 3, y / 3, 1 / 3, 1 / 3]); return a; })()], ['Film strip', [[0, 0, .25, 1], [.25, 0, .25, 1], [.5, 0, .25, 1], [.75, 0, .25, 1]]], ['Mosaic', [[0, 0, .5, .6], [.5, 0, .5, .3], [.5, .3, .5, .3], [0, .6, .3, .4], [.3, .6, .7, .4]]], ['Left column', [[0, 0, .35, 1 / 3], [0, 1 / 3, .35, 1 / 3], [0, 2 / 3, .35, 1 / 3], [.35, 0, .65, 1]]]];
C.makeCollage = (cells) => { C.begin(); C.art.layers = C.art.layers.filter(l => l.type !== 'cell'); const bgi = C.art.layers.findIndex(l => l.name === 'Background'); const made = cells.map((c, i) => ({ id: uid('l'), type: 'cell', name: 'Photo box ' + (i + 1), vis: true, opacity: 1, blend: 'source-over', rx: c[0], ry: c[1], rw: c[2], rh: c[3], url: '', zoom: 1, ox: 0, oy: 0 })); C.art.layers.splice(bgi + 1, 0, ...made); C.sel = made[0].id; C.commit(); };
C.fillCell = (L) => { C._cellFill = L.id; PX.send({ t: 'artImport' }); };
PX.on('artImported', (m) => { if (!C._cellFill) return; const L = C.art.layers.find(l => l.id === C._cellFill); C._cellFill = null; if (!L) return; C.begin(); L.url = m.url; L.name = m.name || L.name; C.commit(); m._handled = true; });
// a click on an empty photo box opens the picker
C.cellAt = (px, py, W, H) => C.art.layers.slice().reverse().find(l => l.type === 'cell' && l.vis && px >= l.rx * W && px <= (l.rx + l.rw) * W && py >= l.ry * H && py <= (l.ry + l.rh) * H);

// ---------------- the tabs ----------------
const head = (b, t) => { const h = document.createElement('div'); h.className = 'hint'; h.style.margin = '10px 2px 6px'; h.innerHTML = '<b>' + t + '</b>'; b.appendChild(h); };
const hint = (b, t) => { const p = document.createElement('p'); p.className = 'hint'; p.textContent = t; b.appendChild(p); };
const btn = (b, t, fn, cls) => { const x = document.createElement('button'); x.textContent = t; if (cls) x.className = cls; x.onclick = fn; b.appendChild(x); return x; };
const row = (b) => { const r = document.createElement('div'); r.className = 'btnrow'; b.appendChild(r); return r; };
const photoSel = () => { const L = sel(); return L && (L.type === 'image' || L.type === 'cell') ? L : null; };
function sliderList(b, T, key, list, after) {
  const box = document.createElement('div'); b.appendChild(box); T[key] = T[key] || {}; const P = T[key];
  for (const [k, label, min, max] of list) { const d = document.createElement('div'); d.className = 'f'; d.innerHTML = '<label>' + label + '</label><input type="range" min="' + min + '" max="' + max + '" step="1"><input type="number" min="' + min + '" max="' + max + '">'; const r = d.querySelector('[type=range]'), n = d.querySelector('[type=number]'); r.value = n.value = P[k] || 0;
    r.onpointerdown = C.begin; r.oninput = () => { n.value = r.value; }; r.onchange = () => { P[k] = +r.value; C.commit(); if (after) after(); }; n.onchange = () => { C.begin(); P[k] = clamp(+n.value, min, max); r.value = P[k]; C.commit(); }; box.appendChild(d); }
}
C.extraTabs = {
  cutout: (b) => {
    const L = photoSel(); if (!L) { hint(b, 'Pick a photo layer (click it on the canvas or in LAYERS), or add one in PHOTOS, then cut it out here.'); return; }
    L.cut = L.cut || {}; const cut = L.cut; hint(b, 'Cutting out "' + L.name + '". Everything here can be undone and changed later.');
    head(b, 'REMOVE THE BACKGROUND'); hint(b, 'Works best when the background is fairly plain (a wall, the sky, a studio). Use the magic eraser and the brush for the rest.');
    const r1 = row(b); btn(r1, cut.auto ? 'BACKGROUND REMOVED (UNDO WITH OFF)' : 'REMOVE BACKGROUND', () => { C.begin(); cut.auto = true; if (cut.tol == null) cut.tol = 40; if (cut.feather == null) cut.feather = 2; C.commit(); }, 'accent'); if (cut.auto) btn(r1, 'OFF', () => { C.begin(); cut.auto = false; C.commit(); });
    sliderList(b, L, 'cut', [['tol', 'Tolerance', 5, 100], ['feather', 'Soft edge', 0, 20]]);
    head(b, 'MAGIC ERASER'); hint(b, 'Press it, then click a colour on the photo: that patch disappears. Click more places to take away more.'); const r2 = row(b); btn(r2, C.tool === 'wand' ? 'CLICK THE PHOTO NOW...' : 'MAGIC ERASER', () => { C.tool = 'wand'; C.render(); }, C.tool === 'wand' ? 'on' : ''); btn(r2, 'UNDO LAST CLICK', () => { if (!(cut.pts || []).length) return; C.begin(); cut.pts.pop(); C.commit(); });
    head(b, 'ERASE / RESTORE BRUSH'); const r3 = row(b); btn(r3, 'ERASE BRUSH', () => { C.tool = 'cutErase'; C.render(); }, C.tool === 'cutErase' ? 'on' : ''); btn(r3, 'RESTORE BRUSH', () => { C.tool = 'cutRestore'; C.render(); }, C.tool === 'cutRestore' ? 'on' : ''); btn(r3, 'DONE', () => { C.tool = 'move'; C.render(); });
    const bs = document.createElement('div'); b.appendChild(bs); C.sliders(bs, C.cutBrush, [['size', 'Brush size', 5, 200]], true);
    head(b, 'START AGAIN'); btn(row(b), 'CLEAR THE CUTOUT', () => { C.begin(); L.cut = {}; C.commit(); }, 'danger');
  },
  effects: (b) => {
    const L = photoSel(), T = L || (C.art.global = C.art.global || {}); hint(b, L ? 'Effects for the photo "' + L.name + '". Click outside it on the canvas for the whole picture.' : 'Effects for the whole picture. Pick a photo layer to put effects on that photo only.');
    head(b, 'ONE CLICK'); const g = document.createElement('div'); g.className = 'grid2'; g.style.gridTemplateColumns = 'repeat(3,1fr)'; b.appendChild(g);
    for (const [name, p] of [['Glitch', { glitch: 60 }], ['RGB split', { rgbsplit: 60 }], ['Pixel art', { pixelate: 45 }], ['Halftone', { halftone: 60 }], ['Pencil sketch', { sketch: 100 }], ['Cartoon', { cartoon: 85 }], ['Posterize', { posterize: 75 }], ['Emboss', { emboss: 80 }], ['Film noise', { noise: 40 }], ['VHS', { glitch: 25, rgbsplit: 35, noise: 30 }], ['Comic', { cartoon: 60, halftone: 30 }], ['Crisp HD', { sharpen: 60, clarity: 50 }], ['Retro game', { pixelate: 30, posterize: 70 }], ['Dreamy', { clarity: -40, exposure: 10, highlights: 30 }], ['Clear all', null]]) {
      const c = document.createElement('div'); c.className = 'card'; c.textContent = name; c.onclick = () => { C.begin(); T.px = p ? Object.assign({}, p) : {}; C.commit(); }; g.appendChild(c); }
    head(b, 'FINE TUNE'); sliderList(b, T, 'px', [['glitch', 'Glitch', 0, 100], ['rgbsplit', 'RGB split', 0, 100], ['pixelate', 'Pixelate', 0, 100], ['halftone', 'Halftone', 0, 100], ['sketch', 'Sketch', 0, 100], ['cartoon', 'Cartoon', 0, 100], ['posterize', 'Posterize', 0, 100], ['emboss', 'Emboss', 0, 100], ['noise', 'Noise', 0, 100]]);
  },
  adjust: (b) => {
    const L = sel(), img = L && (L.type === 'image' || L.type === 'cell'), T = img ? L : (C.art.global = C.art.global || {}); hint(b, img ? 'Adjusting the photo "' + L.name + '".' : 'Adjusting the whole picture.');
    head(b, 'LIGHT'); sliderList(b, T, 'px', [['exposure', 'Exposure', -100, 100], ['highlights', 'Highlights', -100, 100], ['shadows', 'Shadows', -100, 100]]);
    head(b, 'COLOUR AND DETAIL'); const box = document.createElement('div'); b.appendChild(box); C.sliders(box, T, [['bright', 'Brightness', -100, 100], ['contrast', 'Contrast', -100, 100], ['sat', 'Saturation', -100, 100], ['hue', 'Hue', -180, 180], ['warm', 'Warmth', -100, 100]]);
    sliderList(b, T, 'px', [['tint', 'Tint', -100, 100], ['clarity', 'Clarity', -100, 100], ['sharpen', 'Sharpen', 0, 100]]);
    head(b, 'LOOK'); const box2 = document.createElement('div'); b.appendChild(box2); C.sliders(box2, T, [['fade', 'Fade', 0, 100], ['grain', 'Grain', 0, 100], ['vignette', 'Vignette', 0, 100], ['blur', 'Blur', 0, 40]]);
    btn(row(b), 'RESET', () => { C.begin(); ['bright', 'contrast', 'sat', 'hue', 'warm', 'fade', 'grain', 'vignette', 'blur', 'filter', 'duotone'].forEach(k => delete T[k]); T.px = {}; C.commit(); });
  },
  collage: (b) => {
    hint(b, 'Pick a layout, then click each box to put a photo in it. Change the gap, round the corners, and zoom or move each photo inside its box (pick it in LAYERS).');
    const g = document.createElement('div'); g.className = 'grid2'; g.style.gridTemplateColumns = 'repeat(3,1fr)'; b.appendChild(g);
    for (const [name, cells] of COL) { const c = document.createElement('div'); c.className = 'card'; const cv = document.createElement('canvas'); cv.width = 72; cv.height = 72; const x = cv.getContext('2d'); x.fillStyle = '#14061f'; x.fillRect(0, 0, 72, 72); x.fillStyle = '#a85cff'; for (const q of cells) x.fillRect(q[0] * 72 + 2, q[1] * 72 + 2, q[2] * 72 - 4, q[3] * 72 - 4); c.appendChild(cv); const s = document.createElement('small'); s.style.display = 'block'; s.textContent = name; c.appendChild(s); c.onclick = () => C.makeCollage(cells); g.appendChild(c); }
    head(b, 'SPACING'); const T = C.art; if (T.collageGap == null) T.collageGap = 16; const box = document.createElement('div'); b.appendChild(box); C.sliders(box, T, [['collageGap', 'Gap', 0, 120], ['collageRadius', 'Round corners', 0, 200]]);
    const L = sel(); if (L && L.type === 'cell') { head(b, 'THIS BOX'); btn(row(b), L.url ? 'REPLACE PHOTO' : '+ PUT A PHOTO HERE', () => C.fillCell(L), 'accent'); const bx = document.createElement('div'); b.appendChild(bx); C.sliders(bx, L, [['zoom', 'Zoom', 1, 4, 0.01], ['ox', 'Move left / right', -1, 1, 0.01], ['oy', 'Move up / down', -1, 1, 0.01]]); }
    btn(row(b), 'REMOVE THE COLLAGE', () => { C.begin(); C.art.layers = C.art.layers.filter(l => l.type !== 'cell'); C.commit(); }, 'danger');
  },
};
// sticker packs go into the STICKERS tab, under the emoji
C.extraStickers = (b) => {
  head(b, 'STICKER PACK (colours and words editable)'); const g = document.createElement('div'); g.className = 'grid2'; g.style.gridTemplateColumns = 'repeat(3,1fr)'; b.appendChild(g);
  for (const [kind, name, text] of VST) { const c = document.createElement('div'); c.className = 'card'; c.title = name; const cv = document.createElement('canvas'); cv.width = 64; cv.height = 64; const x = cv.getContext('2d'); x.translate(32, 32); try { vsDraw(x, { id: 'p' + kind, kind, size: 54 / Math.max(1, ASPECT[kind] || 1), color: '#ff50c4', color2: '#ffffff', text }, 64, 64, 1); } catch (e) { } c.appendChild(cv); c.onclick = () => C.addLayer({ type: 'vsticker', name: name, kind, text, color: '#ff50c4', color2: '#ffffff', size: 700, font: 'Segoe UI' }); g.appendChild(c); }
};
// the settings of a pack sticker and a photo box, on the right
C.extraProps = (L, f, num, col, box) => {
  if (L.type === 'vsticker') { num('Sticker size', 'size', 60, 4000, 1); col('Colour', 'color'); col('Second colour', 'color2'); const d = f('Words', '<input type="text">'); const i = d.querySelector('input'); i.value = L.text || ''; i.onchange = () => { C.begin(); L.text = i.value; C.commit(); }; }
  if (L.type === 'cell') { const r = document.createElement('div'); r.className = 'btnrow'; const b2 = document.createElement('button'); b2.className = 'accent'; b2.textContent = L.url ? 'REPLACE PHOTO' : '+ PUT A PHOTO HERE'; b2.onclick = () => C.fillCell(L); r.appendChild(b2); box.appendChild(r); num('Zoom', 'zoom', 1, 4, 0.01); num('Move left / right', 'ox', -1, 1, 0.01); num('Move up / down', 'oy', -1, 1, 0.01); }
  if (L.type === 'image') { const r = document.createElement('div'); r.className = 'btnrow'; const cb = document.createElement('button'); cb.textContent = 'CROP...'; cb.onclick = () => C.cropDialog(L); r.appendChild(cb); const ct = document.createElement('button'); ct.textContent = 'CUTOUT'; ct.onclick = () => { C.tab = 'cutout'; C.renderTab(); }; r.appendChild(ct); const fx = document.createElement('button'); fx.textContent = 'EFFECTS'; fx.onclick = () => { C.tab = 'effects'; C.renderTab(); }; r.appendChild(fx); box.appendChild(r); }
  if (C.boxTypes.includes(L.type)) {   // line it up with the canvas
    const r = document.createElement('div'); r.className = 'btnrow'; const W = C.art.w, H = C.art.h;
    const al = (t, fn) => { const b2 = document.createElement('button'); b2.textContent = t; b2.title = 'Line it up: ' + t.toLowerCase(); b2.onclick = () => { const bx = C.box(L, W, H, true); C.begin(); fn(bx.w / W, bx.h / H); C.commit(); }; r.appendChild(b2); };
    al('LEFT', (w) => L.x = -1 + w); al('CENTRE X', () => L.x = 0); al('RIGHT', (w) => L.x = 1 - w); al('TOP', (w, h) => L.y = -1 + h); al('MIDDLE', () => L.y = 0); al('BOTTOM', (w, h) => L.y = 1 - h); box.appendChild(r);
  }
};
// ---------------- the crop tool ----------------
C.cropDialog = (L) => {
  const im = C.img(L.url); if (!im.naturalWidth) return; const cr = Object.assign({ l: 0, r: 0, t: 0, b: 0 }, L.crop || {});
  PX.UI.modal('<h3>Crop</h3><div class="row"><select id="crA"><option value="0">Free</option><option value="1">Square 1:1</option><option value="0.8">Portrait 4:5</option><option value="0.5625">Story 9:16</option><option value="1.7778">Wide 16:9</option><option value="1.5">Photo 3:2</option></select><span class="hint">Drag the box to move it, its corners to resize.</span></div><canvas id="crC" style="max-width:100%;cursor:move;border-radius:8px"></canvas><div class="acts"><button id="crR">RESET</button><button id="crN">CANCEL</button><button id="crY" class="accent">CROP</button></div>', (b) => {
    const cv = b.querySelector('#crC'), mw = 760, mh = 460, s = Math.min(mw / im.naturalWidth, mh / im.naturalHeight); cv.width = Math.round(im.naturalWidth * s); cv.height = Math.round(im.naturalHeight * s); const g = cv.getContext('2d');
    let R = { x: cr.l / 100 * cv.width, y: cr.t / 100 * cv.height, w: cv.width * (1 - (cr.l + cr.r) / 100), h: cv.height * (1 - (cr.t + cr.b) / 100) }, A = 0;
    const draw = () => { g.drawImage(im, 0, 0, cv.width, cv.height); g.fillStyle = 'rgba(0,0,0,0.6)'; g.fillRect(0, 0, cv.width, R.y); g.fillRect(0, R.y + R.h, cv.width, cv.height - R.y - R.h); g.fillRect(0, R.y, R.x, R.h); g.fillRect(R.x + R.w, R.y, cv.width - R.x - R.w, R.h); g.strokeStyle = '#3ce1ff'; g.lineWidth = 2; g.strokeRect(R.x, R.y, R.w, R.h); g.strokeStyle = 'rgba(255,255,255,0.35)'; g.lineWidth = 1; for (let i = 1; i < 3; i++) { g.beginPath(); g.moveTo(R.x + R.w * i / 3, R.y); g.lineTo(R.x + R.w * i / 3, R.y + R.h); g.moveTo(R.x, R.y + R.h * i / 3); g.lineTo(R.x + R.w, R.y + R.h * i / 3); g.stroke(); } g.fillStyle = '#3ce1ff'; for (const [x, y] of [[R.x, R.y], [R.x + R.w, R.y], [R.x, R.y + R.h], [R.x + R.w, R.y + R.h]]) g.fillRect(x - 6, y - 6, 12, 12); };
    const fit = () => { R.w = clamp(R.w, 20, cv.width); R.h = clamp(R.h, 20, cv.height); if (A) { R.h = R.w / A; if (R.h > cv.height) { R.h = cv.height; R.w = R.h * A; } if (R.w > cv.width) { R.w = cv.width; R.h = R.w / A; } } R.x = clamp(R.x, 0, cv.width - R.w); R.y = clamp(R.y, 0, cv.height - R.h); draw(); };
    b.querySelector('#crA').onchange = (e) => { A = +e.target.value; fit(); };
    cv.onmousedown = (e) => { const rc = cv.getBoundingClientRect(), k = cv.width / rc.width, px = (e.clientX - rc.left) * k, py = (e.clientY - rc.top) * k; const corner = [[R.x, R.y, 'tl'], [R.x + R.w, R.y, 'tr'], [R.x, R.y + R.h, 'bl'], [R.x + R.w, R.y + R.h, 'br']].find(c => Math.abs(c[0] - px) < 12 && Math.abs(c[1] - py) < 12); const R0 = Object.assign({}, R);
      const mv = (ev) => { const qx = (ev.clientX - rc.left) * k, qy = (ev.clientY - rc.top) * k, dx = qx - px, dy = qy - py; if (!corner) { R.x = R0.x + dx; R.y = R0.y + dy; } else { const c = corner[2]; if (c.includes('l')) { R.x = R0.x + dx; R.w = R0.w - dx; } if (c.includes('r')) R.w = R0.w + dx; if (c.includes('t')) { R.y = R0.y + dy; R.h = R0.h - dy; } if (c.includes('b')) R.h = R0.h + dy; if (R.w < 20) R.w = 20; if (R.h < 20) R.h = 20; } fit(); };
      const up = () => { removeEventListener('mousemove', mv); removeEventListener('mouseup', up); }; addEventListener('mousemove', mv); addEventListener('mouseup', up); };
    b.querySelector('#crR').onclick = () => { R = { x: 0, y: 0, w: cv.width, h: cv.height }; A = 0; b.querySelector('#crA').value = '0'; draw(); };
    b.querySelector('#crN').onclick = PX.UI.close;
    b.querySelector('#crY').onclick = () => { C.begin(); L.crop = { l: R.x / cv.width * 100, t: R.y / cv.height * 100, r: (cv.width - R.x - R.w) / cv.width * 100, b: (cv.height - R.y - R.h) / cv.height * 100 }; C.commit(); PX.UI.close(); };
    draw();
  });
};
C.cutBrush = { size: 40 };
C.cutToolDown = (L, px, py, W, H, ev, r, k) => {   // the magic eraser and the cutout brushes work in the photo's own coordinates
  const bx = C.box(L, W, H); const a = -(L.rot || 0) * Math.PI / 180; const toLocal = (qx, qy) => { const lx = (qx - bx.cx) * Math.cos(a) - (qy - bx.cy) * Math.sin(a), ly = (qx - bx.cx) * Math.sin(a) + (qy - bx.cy) * Math.cos(a); const cr = L.crop || { l: 0, r: 0, t: 0, b: 0 }; const u = (lx / bx.w + 0.5) * (L.flipH ? -1 : 1) + (L.flipH ? 1 : 0), v = (ly / bx.h + 0.5) * (L.flipV ? -1 : 1) + (L.flipV ? 1 : 0); return [cr.l / 100 + u * (1 - (cr.l + cr.r) / 100), cr.t / 100 + v * (1 - (cr.t + cr.b) / 100)]; };
  L.cut = L.cut || {}; C.begin();
  if (C.tool === 'wand') { L.cut.pts = L.cut.pts || []; const p = toLocal(px, py); L.cut.pts.push([p[0], p[1], L.cut.tol || 40]); C.commit(); return; }
  L.cut.strokes = L.cut.strokes || []; const s = { restore: C.tool === 'cutRestore', size: C.cutBrush.size, pts: [toLocal(px, py)] }; L.cut.strokes.push(s); C.draw();
  const mv = (e2) => { s.pts.push(toLocal((e2.clientX - r.left) * k, (e2.clientY - r.top) * k)); C.draw(); }; const up = () => { removeEventListener('mousemove', mv); removeEventListener('mouseup', up); C.commit(); }; addEventListener('mousemove', mv); addEventListener('mouseup', up);
};
// ---------------- zoom ----------------
C.viewZ = 1;
C.setZoom = (z) => { C.viewZ = clamp(z, 0.25, 4); C.size(); const zl = document.querySelector('#cvZoomL'); if (zl) zl.textContent = Math.round(C.viewZ * 100) + '%'; };
})();
