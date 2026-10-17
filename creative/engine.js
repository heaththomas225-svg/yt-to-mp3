'use strict';
// =====================================================================
//  PURPHEX CREATIVE STUDIO: the engine.
//  One compositor draws a frame of the project at any time t. The preview monitor and the export use the same code, so what you see is
//  what is rendered. Audio is mixed with Web Audio from the real decoded audio (preview: live; export: OfflineAudioContext), and the
//  visualisers read the same decoded samples (real FFT, never random motion).
// =====================================================================
const PX = window.PX = {};
// ---------------- the bridge to Purphex (C#) ----------------
PX.handlers = {};
PX.send = (msg) => { try { window.chrome.webview.postMessage(JSON.stringify(msg)); } catch (e) { console.log('no host', msg); } };
PX.on = (t, fn) => { (PX.handlers[t] = PX.handlers[t] || []).push(fn); };
if (window.chrome && window.chrome.webview) window.chrome.webview.addEventListener('message', ev => { let m; try { m = JSON.parse(ev.data); } catch (e) { return; } (PX.handlers[m.t] || []).forEach(fn => { try { fn(m); } catch (e) { console.error(e); PX.toast('Error: ' + e.message, true); } }); });
PX.uid = (p) => (p || 'x') + Math.random().toString(36).slice(2, 10) + Date.now().toString(36).slice(-4);
PX.clamp = (v, a, b) => Math.max(a, Math.min(b, v));
PX.toast = (msg, err) => { const t = document.getElementById('toast'); t.textContent = msg; t.className = err ? 'err' : ''; clearTimeout(PX._tt); PX._tt = setTimeout(() => t.className = 'hidden', err ? 7000 : 3500); };
PX.fmtTC = (t, fps) => { t = Math.max(0, t || 0); const f = Math.floor((t % 1) * fps + 1e-6), s = Math.floor(t) % 60, m = Math.floor(t / 60) % 60, h = Math.floor(t / 3600); const p = n => String(n).padStart(2, '0'); return p(h) + ':' + p(m) + ':' + p(s) + ':' + p(f); };
PX.fmtT = (t) => { t = Math.max(0, t || 0); return Math.floor(t / 60) + ':' + String(Math.floor(t % 60)).padStart(2, '0') + '.' + Math.floor((t % 1) * 10); };

// ---------------- the project ----------------
PX.SCHEMA = 2;
PX.defaultFx = () => ({ brightness: 0, contrast: 0, saturation: 0, exposure: 0, temperature: 0, tint: 0, highlights: 0, shadows: 0, blur: 0, sharpen: 0, vignette: 0, fade: 0, bw: false, sepia: 0 });
PX.newProject = (name, w, h, fps, preset) => ({
  v: PX.SCHEMA, id: PX.uid('p'), name: name || 'Untitled video', preset: preset || 'custom', settings: { w: w || 1920, h: h || 1080, fps: fps || 30, bg: '#000000' },
  media: [], tracks: [
    { id: PX.uid('t'), kind: 'text', name: 'Text', h: 46, hidden: false, locked: false },
    { id: PX.uid('t'), kind: 'video', name: 'Video 2', h: 56, hidden: false, locked: false },
    { id: PX.uid('t'), kind: 'video', name: 'Video 1', h: 64, hidden: false, locked: false },
    { id: PX.uid('t'), kind: 'audio', name: 'Audio 1', h: 56, mute: false, solo: false, locked: false, vol: 1 },
    { id: PX.uid('t'), kind: 'audio', name: 'Music', h: 56, mute: false, solo: false, locked: false, vol: 1 }],
  clips: [], markers: [], lyrics: { text: '' }, exportSettings: { format: 'mp4-h264', res: 'project', fps: 0, quality: 'high', abr: 192 } });
// older saves are brought up to date (never break an old project)
PX.migrate = (p) => {
  if (!p || typeof p !== 'object') throw new Error('That project file is damaged.');
  p.v = p.v || 1; p.settings = Object.assign({ w: 1920, h: 1080, fps: 30, bg: '#000000' }, p.settings || {});
  p.media = p.media || []; p.tracks = p.tracks || []; p.clips = p.clips || []; p.markers = p.markers || []; p.lyrics = p.lyrics || { text: '' };
  p.exportSettings = Object.assign({ format: 'mp4-h264', res: 'project', fps: 0, quality: 'high', abr: 192 }, p.exportSettings || {});
  for (const c of p.clips) { c.fx = Object.assign(PX.defaultFx(), c.fx || {}); c.crop = Object.assign({ l: 0, t: 0, r: 0, b: 0 }, c.crop || {}); if (c.opacity == null) c.opacity = 1; if (c.scale == null) c.scale = 1; if (c.x == null) c.x = 0; if (c.y == null) c.y = 0; if (c.rot == null) c.rot = 0; if (c.speed == null) c.speed = 1; if (c.vol == null) c.vol = 1; if (!c.fit) c.fit = 'fit'; if (c.fadeIn == null) c.fadeIn = 0; if (c.fadeOut == null) c.fadeOut = 0; if (c.in == null) c.in = 0; }
  p.v = PX.SCHEMA; return p;
};
PX.P = PX.newProject();
PX.mediaById = (id) => PX.P.media.find(m => m.id === id);
PX.trackById = (id) => PX.P.tracks.find(t => t.id === id);
PX.clipById = (id) => PX.P.clips.find(c => c.id === id);
PX.projectDur = () => { let e = 0; for (const c of PX.P.clips) e = Math.max(e, c.start + c.dur); return e; };
PX.clipsOn = (tid) => PX.P.clips.filter(c => c.track === tid).sort((a, b) => a.start - b.start);
PX.isVisualKind = (c) => c.type === 'video' || c.type === 'image' || c.type === 'text' || c.type === 'visual';

// ---------------- undo / redo (whole-project snapshots, only on finished edits) ----------------
PX.hist = { undo: [], redo: [], max: 200 };
PX.dirty = false;
PX.commit = (label) => { PX.hist.undo.push(PX._before || JSON.stringify(PX.P)); if (PX.hist.undo.length > PX.hist.max) PX.hist.undo.shift(); PX.hist.redo = []; PX._before = null; PX.setDirty(true); PX.changed(label); };
PX.begin = () => { if (!PX._before) PX._before = JSON.stringify(PX.P); };   // call before a drag; commit() when it ends
PX.cancelBegin = () => { PX._before = null; };
PX.undo = () => { if (!PX.hist.undo.length) return PX.toast('Nothing to undo.'); PX.hist.redo.push(JSON.stringify(PX.P)); PX.P = JSON.parse(PX.hist.undo.pop()); PX.setDirty(true); PX.changed('undo'); };
PX.redo = () => { if (!PX.hist.redo.length) return PX.toast('Nothing to redo.'); PX.hist.undo.push(JSON.stringify(PX.P)); PX.P = JSON.parse(PX.hist.redo.pop()); PX.setDirty(true); PX.changed('redo'); };
PX.setDirty = (d) => { PX.dirty = d; const e = document.getElementById('dirty'); if (e) e.className = d ? 'on' : ''; };
PX.listeners = [];
PX.changed = (why) => { PX.listeners.forEach(f => f(why)); PX.scheduleAutosave(); };
PX.scheduleAutosave = () => { clearTimeout(PX._as); PX._as = setTimeout(() => { if (PX.dirty) PX.send({ t: 'autosave', project: PX.P }); }, 4000); };

// ---------------- media: elements, decoded audio, thumbnails, waveforms ----------------
PX.vids = {};        // clip id -> <video> (each clip its own element, so two clips of one file never fight)
PX.imgs = {};        // media id -> <img>
PX.audio = {};       // media id -> AudioBuffer (decoded from the compact audio Purphex extracts)
PX.peaks = {};       // media id -> Float32Array of peaks (100 per second)
PX.thumbs = {};      // media id -> data URL
PX.strips = {};      // media id -> [{t, url}]
PX.actx = null;
PX.ac = () => { if (!PX.actx) PX.actx = new AudioContext({ sampleRate: 48000 }); return PX.actx; };
PX.mediaUrl = (m) => m.proxyUrl || m.url;
PX.videoFor = (c) => {
  let v = PX.vids[c.id]; const m = PX.mediaById(c.media); if (!m) return null;
  if (v && v._src !== PX.mediaUrl(m)) { v.src = ''; v = null; }
  if (!v) { v = document.createElement('video'); v.muted = true; v.preload = 'auto'; v.playsInline = true; v.crossOrigin = 'anonymous'; v._src = PX.mediaUrl(m); v.src = v._src; v.onerror = () => PX.mediaError(m, v); PX.vids[c.id] = v; }
  return v;
};
PX.imgFor = (m) => { let i = PX.imgs[m.id]; if (!i || i._src !== PX.mediaUrl(m)) { i = new Image(); i.crossOrigin = 'anonymous'; i._src = PX.mediaUrl(m); i.src = i._src; i.addEventListener('load', () => { m.w = i.naturalWidth; m.h = i.naturalHeight; PX.redraw(); }); i.addEventListener('error', () => { if ((i._tries = (i._tries || 0) + 1) <= 3) setTimeout(() => { i.src = i._src + (i._src.indexOf('?') < 0 ? '?' : '&') + 'try=' + i._tries; }, 300 * i._tries); else PX.mediaError(m); }); PX.imgs[m.id] = i; } return i; };   // a picture that fails right after its folder is opened is tried again (up to 3 times)
PX.mediaError = (m, el) => {
  if (m._err) return; m._err = true;
  if (m.kind === 'video' && !m.proxyUrl && !m.missing) { PX.toast('"' + m.name + '" uses a codec the preview cannot play: making a preview copy (the original is used for the export).'); PX.send({ t: 'proxy', id: m.id }); }
  else if (!m.missing) PX.toast('Could not open "' + m.name + '". The file may be damaged or in a format that is not supported.', true);
};
// releasing clips far from the playhead keeps memory flat on long timelines
PX.releaseFar = (t) => { for (const id in PX.vids) { const c = PX.clipById(id); if (!c || (t < c.start - 30 || t > c.start + c.dur + 30)) { const v = PX.vids[id]; v.pause(); v.removeAttribute('src'); v.load(); delete PX.vids[id]; } } };
PX.loadAudio = async (m) => {
  if (!m.audioUrl || PX.audio[m.id] || m._aload) return; m._aload = true;
  try { const buf = await (await fetch(m.audioUrl)).arrayBuffer(); const ab = await PX.ac().decodeAudioData(buf); PX.audio[m.id] = ab; PX.peaks[m.id] = PX.makePeaks(ab); PX.changed('audio'); }
  catch (e) { PX.toast('Could not read the audio of "' + m.name + '": ' + e.message, true); }
  m._aload = false;
};
PX.makePeaks = (ab) => { const per = Math.max(1, Math.floor(ab.sampleRate / 100)), n = Math.ceil(ab.length / per), out = new Float32Array(n), a = ab.getChannelData(0), b = ab.numberOfChannels > 1 ? ab.getChannelData(1) : a; for (let i = 0; i < n; i++) { let mx = 0; const s = i * per, e = Math.min(ab.length, s + per); for (let k = s; k < e; k += 4) { const v = Math.max(Math.abs(a[k]), Math.abs(b[k])); if (v > mx) mx = v; } out[i] = mx; } return out; };
PX.makeThumbs = (m) => new Promise(res => {
  if (m.kind === 'image') { PX.thumbs[m.id] = PX.mediaUrl(m); PX.strips[m.id] = [{ t: 0, url: PX.mediaUrl(m) }]; return res(); }
  if (m.kind !== 'video') return res();
  const v = document.createElement('video'); v.muted = true; v.crossOrigin = 'anonymous'; v.preload = 'auto'; v.src = PX.mediaUrl(m); const strip = []; const cv = document.createElement('canvas'); cv.width = 160; cv.height = 90; const g = cv.getContext('2d');
  let times = [], i = 0; const done = () => { PX.strips[m.id] = strip; v.removeAttribute('src'); v.load(); PX.changed('thumbs'); res(); };
  v.onloadedmetadata = () => { m.dur = v.duration; m.w = v.videoWidth; m.h = v.videoHeight; const n = PX.clamp(Math.ceil(v.duration / 3), 1, 40); for (let k = 0; k < n; k++) times.push(Math.min(v.duration - 0.05, (k + 0.5) * v.duration / n)); v.currentTime = times[0]; };
  v.onseeked = () => { g.drawImage(v, 0, 0, 160, 90); const u = cv.toDataURL('image/jpeg', 0.7); strip.push({ t: times[i], url: u }); if (i === 0 || i === Math.floor(times.length / 3)) PX.thumbs[m.id] = u; i++; if (i < times.length) v.currentTime = times[i]; else done(); };
  v.onerror = () => { PX.mediaError(m, v); res(); };
});
PX.addMedia = (items) => {
  for (const it of items) {
    let m = PX.P.media.find(x => x.path === it.path && it.path); if (m) { Object.assign(m, it, { id: m.id }); } else { m = Object.assign({ id: it.id || PX.uid('m') }, it); PX.P.media.push(m); }
    if (m.kind === 'image') { const i = PX.imgFor(m); i.addEventListener('load', () => PX.changed('media')); PX.makeThumbs(m); }
    else if (m.kind === 'video') PX.makeThumbs(m);
    else if (m.kind === 'audio') { const a = new Audio(); a.preload = 'metadata'; a.src = PX.mediaUrl(m); a.onloadedmetadata = () => { m.dur = a.duration; PX.changed('media'); }; a.onerror = () => PX.mediaError(m); }
    if (m.audioUrl) PX.loadAudio(m);
  }
  PX.setDirty(true); PX.changed('media');
};
PX.on('audioReady', m => { const md = PX.mediaById(m.id); if (!md) return; md.audioUrl = m.url; md.hasAudio = true; PX.loadAudio(md); });
PX.on('noAudio', m => { const md = PX.mediaById(m.id); if (md) { md.hasAudio = false; PX.changed('media'); } });
PX.on('probe', m => { const md = PX.mediaById(m.id); if (!md) return; Object.assign(md, m.info); PX.changed('media'); });
PX.on('proxyDone', m => { const md = PX.mediaById(m.id); if (!md) return; md.proxyUrl = m.url; md._err = false; for (const id in PX.vids) { const c = PX.clipById(id); if (c && c.media === md.id) { PX.vids[id].src = ''; delete PX.vids[id]; } } PX.makeThumbs(md); PX.toast('Preview copy of "' + md.name + '" is ready.'); PX.redraw(); });
PX.on('proxyError', m => PX.toast('Could not make a preview copy: ' + m.msg, true));
PX.on('mediaError', m => PX.toast(m.msg, true));

// ---------------- effects: one SVG filter per clip (the same in preview and export) ----------------
PX.fxDefs = () => document.querySelector('#fxdefs defs');
PX.fxKey = (fx) => JSON.stringify(fx);
PX.fxIsNeutral = (fx) => { const d = PX.defaultFx(); for (const k in d) if (k !== 'vignette' && fx[k] !== d[k]) return false; return true; };
PX.filterFor = (c, scale) => {
  const fx = c.fx; if (!fx || PX.fxIsNeutral(fx)) return 'none';
  const key = PX.fxKey(fx) + '|' + scale.toFixed(2), id = 'f' + c.id; PX._fcache = PX._fcache || {};
  if (PX._fcache[id] !== key) {
    PX._fcache[id] = key; let el = document.getElementById(id); if (el) el.remove(); el = document.createElementNS('http://www.w3.org/2000/svg', 'filter'); el.id = id; el.setAttribute('color-interpolation-filters', 'sRGB'); el.setAttribute('x', '-10%'); el.setAttribute('y', '-10%'); el.setAttribute('width', '120%'); el.setAttribute('height', '120%');
    // tone: exposure, brightness, contrast, highlights, shadows, fade (one lookup table per channel)
    const N = 32, tab = []; const ex = Math.pow(2, fx.exposure || 0), br = (fx.brightness || 0) / 200, ct = 1 + (fx.contrast || 0) / 100, hi = (fx.highlights || 0) / 100, sh = (fx.shadows || 0) / 100, fd = (fx.fade || 0) / 100;
    for (let i = 0; i <= N; i++) { let x = i / N; x = x * ex + br; x = (x - 0.5) * ct + 0.5; x += sh * 0.35 * Math.pow(1 - PX.clamp(x, 0, 1), 2); x += hi * 0.35 * Math.pow(PX.clamp(x, 0, 1), 2); x = fd * 0.22 + x * (1 - fd * 0.3); tab.push(PX.clamp(x, 0, 1).toFixed(4)); }
    const ct2 = document.createElementNS(el.namespaceURI, 'feComponentTransfer'); for (const ch of ['R', 'G', 'B']) { const fn = document.createElementNS(el.namespaceURI, 'feFunc' + ch); fn.setAttribute('type', 'table'); fn.setAttribute('tableValues', tab.join(' ')); ct2.appendChild(fn); } el.appendChild(ct2);
    // colour: saturation, black and white, sepia, temperature, tint (a colour matrix)
    let s = fx.bw ? 0 : 1 + (fx.saturation || 0) / 100; const lr = 0.2126, lg = 0.7152, lb = 0.0722;
    let M = [lr + (1 - lr) * s, lg - lg * s, lb - lb * s, 0, 0, lr - lr * s, lg + (1 - lg) * s, lb - lb * s, 0, 0, lr - lr * s, lg - lg * s, lb + (1 - lb) * s, 0, 0, 0, 0, 0, 1, 0];
    const sp = (fx.sepia || 0) / 100; if (sp > 0) { const S = [0.393, 0.769, 0.189, 0.349, 0.686, 0.168, 0.272, 0.534, 0.131]; for (let r = 0; r < 3; r++) for (let k = 0; k < 3; k++) M[r * 5 + k] = M[r * 5 + k] * (1 - sp) + S[r * 3 + k] * sp; }
    const tp = (fx.temperature || 0) / 100, tn = (fx.tint || 0) / 100; M[4] += tp * 0.08 + tn * 0.03; M[9] += -tn * 0.06; M[14] += -tp * 0.08 + tn * 0.03;
    const cm = document.createElementNS(el.namespaceURI, 'feColorMatrix'); cm.setAttribute('type', 'matrix'); cm.setAttribute('values', M.map(v => v.toFixed(4)).join(' ')); el.appendChild(cm);
    if (fx.sharpen > 0) { const k = (fx.sharpen / 100) * 1.2, cv = document.createElementNS(el.namespaceURI, 'feConvolveMatrix'); cv.setAttribute('order', '3'); cv.setAttribute('kernelMatrix', [0, -k, 0, -k, 1 + 4 * k, -k, 0, -k, 0].map(v => v.toFixed(3)).join(' ')); cv.setAttribute('preserveAlpha', 'true'); el.appendChild(cv); }
    if (fx.blur > 0) { const gb = document.createElementNS(el.namespaceURI, 'feGaussianBlur'); gb.setAttribute('stdDeviation', (fx.blur * scale).toFixed(2)); el.appendChild(gb); }
    PX.fxDefs().appendChild(el);
  }
  return 'url(#' + id + ')';
};
PX.PRESETS = [
  { name: 'Cinematic', fx: { contrast: 18, saturation: -12, temperature: 12, shadows: -10, vignette: 35, fade: 8 } },
  { name: 'Dark', fx: { exposure: -0.5, contrast: 25, saturation: -20, shadows: -25, vignette: 50 } },
  { name: 'Vibrant', fx: { saturation: 45, contrast: 12, brightness: 4 } },
  { name: 'Monochrome', fx: { bw: true, contrast: 22 } },
  { name: 'Music video neon', fx: { saturation: 35, temperature: -20, tint: 25, contrast: 15, vignette: 30, highlights: 10 } },
  { name: 'Vintage film', fx: { sepia: 35, fade: 30, contrast: -8, vignette: 40, saturation: -15 } },
  { name: 'Warm sunset', fx: { temperature: 40, saturation: 15, highlights: 12 } },
  { name: 'Cold night', fx: { temperature: -45, exposure: -0.3, saturation: -10, vignette: 30 } },
  { name: 'Dreamy', fx: { blur: 1.2, fade: 25, brightness: 8, saturation: -5 } },
  { name: 'Sharp clarity', fx: { sharpen: 60, contrast: 12 } }];

// ---------------- transitions ----------------
PX.TRANSITIONS = [['none', 'No transition'], ['dissolve', 'Cross dissolve'], ['fadeblack', 'Fade through black'], ['fadewhite', 'Fade through white'], ['wipe', 'Wipe'], ['slide', 'Slide'], ['zoom', 'Zoom'], ['blur', 'Blur']];
// the clip before c on its track that ends where c starts (within 2 frames): the partner of a transition
PX.partnerBefore = (c) => { const fps = PX.P.settings.fps; let best = null; for (const o of PX.P.clips) if (o.track === c.track && o.id !== c.id && Math.abs(o.start + o.dur - c.start) < 2 / fps) best = o; return best; };
PX.partnerAfter = (c) => { const fps = PX.P.settings.fps; for (const o of PX.P.clips) if (o.track === c.track && o.id !== c.id && Math.abs(c.start + c.dur - o.start) < 2 / fps && o.transIn && o.transIn.type !== 'none') return o; return null; };
// where a clip is drawn at t: its own time span widened by half of each transition with a partner, and how it takes part
PX.roleAt = (c, t) => {
  const ti = c.transIn && c.transIn.type !== 'none' ? c.transIn : null, pb = ti ? PX.partnerBefore(c) : null, half = ti ? ti.dur / 2 : 0;
  const nx = PX.partnerAfter(c), nh = nx ? nx.transIn.dur / 2 : 0;
  let from = c.start - (pb ? half : 0), to = c.start + c.dur + nh;
  if (t < from || t >= to) return null;
  if (ti && pb && t < c.start + half) return { role: 'B', type: ti.type, p: (t - (c.start - half)) / ti.dur };
  if (ti && !pb && t < c.start + ti.dur) return { role: 'B0', type: ti.type, p: (t - c.start) / ti.dur };   // from black / from nothing
  if (nx && t >= c.start + c.dur - nh) return { role: 'A', type: nx.transIn.type, p: (t - (nx.start - nh)) / nx.transIn.dur };
  if (c.transOut && c.transOut.type !== 'none' && t >= c.start + c.dur - c.transOut.dur) return { role: 'A0', type: c.transOut.type, p: (t - (c.start + c.dur - c.transOut.dur)) / c.transOut.dur };
  return { role: '', p: 0 };
};
PX.mediaTime = (c, t) => { const m = PX.mediaById(c.media); const md = m && m.dur ? m.dur : 1e9; return PX.clamp(c.in + (t - c.start) * c.speed, 0, Math.max(0, md - 0.04)); };

// ---------------- text ----------------
PX.defaultText = () => ({ content: 'Your title', font: 'Segoe UI', size: 96, weight: 800, color: '#ffffff', align: 'center', spacing: 0, outline: 0, outlineColor: '#000000', shadow: 12, shadowColor: '#000000', bg: 'none', bgColor: '#a85cff', bgPad: 24, animIn: 'fade', animOut: 'fade', animDur: 0.5, karaoke: false, hl: '#ffd23c', lineH: 1.15, upper: false });
PX.drawText = (g, c, t, W, H) => {
  const x = c.text, local = t - c.start; let a = c.opacity, dx = 0, dy = 0, sc = 1, shown = 1, blur = 0;
  const ad = Math.min(x.animDur || 0.5, c.dur / 2);
  const ease = (p) => 1 - Math.pow(1 - PX.clamp(p, 0, 1), 3);
  const anim = (type, p) => { const e = ease(p); if (type === 'fade') a *= e; else if (type === 'up') { a *= e; dy += (1 - e) * H * 0.06; } else if (type === 'down') { a *= e; dy -= (1 - e) * H * 0.06; } else if (type === 'left') { a *= e; dx += (1 - e) * W * 0.08; } else if (type === 'pop') { a *= e; sc *= 0.6 + 0.4 * e + Math.sin(e * Math.PI) * 0.08; } else if (type === 'type') shown = Math.min(shown, PX.clamp(p, 0, 1)); else if (type === 'blur') { a *= e; blur = (1 - e) * 18; } };
  if (x.animIn && x.animIn !== 'none' && local < ad) anim(x.animIn, local / ad);
  if (x.animOut && x.animOut !== 'none' && local > c.dur - ad) anim(x.animOut, (c.dur - local) / ad);
  if (a <= 0.002) return;
  const k = W / 1920, size = x.size * k * (c.scale || 1); let text = x.upper ? x.content.toUpperCase() : x.content; const lines = text.split('\n');
  g.save(); g.globalAlpha = PX.clamp(a, 0, 1); g.translate(W / 2 + c.x * W / 2 + dx, H / 2 + c.y * H / 2 + dy); g.rotate((c.rot || 0) * Math.PI / 180); g.scale(sc, sc); if (blur > 0) g.filter = 'blur(' + (blur * k).toFixed(1) + 'px)';
  g.font = (x.italic ? 'italic ' : '') + x.weight + ' ' + size + 'px "' + x.font + '"'; g.textBaseline = 'middle'; try { g.letterSpacing = (x.spacing * k) + 'px'; } catch (e) { }
  const lh = size * (x.lineH || 1.15), widths = lines.map(l => g.measureText(l).width), bw = Math.max(1, ...widths), bh = lh * lines.length;
  const ax = x.align === 'left' ? -bw / 2 : x.align === 'right' ? bw / 2 : 0; g.textAlign = x.align || 'center';
  if (x.bg && x.bg !== 'none') { const pad = (x.bgPad || 20) * k, rx = -bw / 2 - pad, ry = -bh / 2 - pad * 0.6, rw = bw + pad * 2, rh = bh + pad * 1.2; g.fillStyle = x.bgColor; g.beginPath(); if (g.roundRect) g.roundRect(rx, ry, rw, rh, x.bg === 'pill' ? rh / 2 : x.bg === 'round' ? 14 * k : 0); else g.rect(rx, ry, rw, rh); g.fill(); }
  const total = text.replace(/\n/g, '').length; let budget = Math.floor(total * shown);
  for (let i = 0; i < lines.length; i++) {
    let line = lines[i]; if (shown < 1) { line = line.slice(0, Math.max(0, budget)); budget -= lines[i].length; }
    const ly = -bh / 2 + lh * (i + 0.5);
    if (x.shadow > 0) { g.shadowColor = x.shadowColor; g.shadowBlur = x.shadow * k; g.shadowOffsetY = x.shadow * k * 0.25; }
    if (x.outline > 0) { g.lineWidth = x.outline * k * 2; g.strokeStyle = x.outlineColor; g.lineJoin = 'round'; g.strokeText(line, ax, ly); }
    g.fillStyle = x.color; g.fillText(line, ax, ly); g.shadowColor = 'transparent';
    if (x.karaoke && line.length) {   // karaoke: the line fills with the highlight colour as it is sung (evenly over the clip, or by the word times when set)
      const p = PX.clamp(local / c.dur, 0, 1), lw = g.measureText(line).width, left = x.align === 'left' ? ax : x.align === 'right' ? ax - lw : ax - lw / 2;
      g.save(); g.beginPath(); g.rect(left, ly - lh / 2, lw * p, lh); g.clip(); g.fillStyle = x.hl; g.fillText(line, ax, ly); g.restore();
    }
  }
  g.restore();
};

// ---------------- audio analysis for the visualisers (real samples) ----------------
PX.fft = (re, im) => { const n = re.length; for (let i = 1, j = 0; i < n; i++) { let bit = n >> 1; for (; j & bit; bit >>= 1) j ^= bit; j ^= bit; if (i < j) { let t = re[i]; re[i] = re[j]; re[j] = t; t = im[i]; im[i] = im[j]; im[j] = t; } } for (let len = 2; len <= n; len <<= 1) { const a = -2 * Math.PI / len, wr = Math.cos(a), wi = Math.sin(a); for (let i = 0; i < n; i += len) { let cr = 1, ci = 0; for (let k = 0; k < len / 2; k++) { const ur = re[i + k], ui = im[i + k], vr = re[i + k + len / 2] * cr - im[i + k + len / 2] * ci, vi = re[i + k + len / 2] * ci + im[i + k + len / 2] * cr; re[i + k] = ur + vr; im[i + k] = ui + vi; re[i + k + len / 2] = ur - vr; im[i + k + len / 2] = ui - vi; const nr = cr * wr - ci * wi; ci = cr * wi + ci * wr; cr = nr; } } } };
// the mono mix of the timeline's audio around time t (n samples at 48 kHz), from the decoded buffers, with each clip's level
PX.mixWindow = (t, n, onlyClip) => {
  const out = new Float32Array(n); const solo = PX.P.tracks.some(tr => tr.kind === 'audio' && tr.solo);
  for (const c of PX.P.clips) {
    if (onlyClip && c.id !== onlyClip) continue; const m = PX.mediaById(c.media); if (!m) continue; const ab = PX.audio[m.id]; if (!ab) continue;
    const tr = PX.trackById(c.track); if (!tr || (tr.kind === 'audio' && (tr.mute || (solo && !tr.solo))) || (tr.kind === 'video' && tr.hidden)) continue;
    if (t < c.start || t > c.start + c.dur) continue; const mt = c.in + (t - c.start) * c.speed, s0 = Math.floor((mt - n / 2 / 48000) * ab.sampleRate);
    const a = ab.getChannelData(0), b = ab.numberOfChannels > 1 ? ab.getChannelData(1) : a, g = c.vol * (tr.vol == null ? 1 : tr.vol), step = ab.sampleRate / 48000 * c.speed;
    for (let i = 0; i < n; i++) { const k = Math.floor(s0 + i * step); if (k >= 0 && k < ab.length) out[i] += (a[k] + b[k]) * 0.5 * g; }
  }
  return out;
};
PX.spectrum = (t, bands, src) => {
  const N = 2048, w = PX.mixWindow(t, N, src), re = new Float64Array(N), im = new Float64Array(N);
  for (let i = 0; i < N; i++) re[i] = w[i] * (0.5 - 0.5 * Math.cos(2 * Math.PI * i / (N - 1)));
  PX.fft(re, im); const out = new Float32Array(bands);
  for (let b = 0; b < bands; b++) { const f0 = 30 * Math.pow(16000 / 30, b / bands), f1 = 30 * Math.pow(16000 / 30, (b + 1) / bands); const k0 = Math.max(1, Math.floor(f0 / 48000 * N)), k1 = Math.max(k0 + 1, Math.floor(f1 / 48000 * N)); let mx = 0; for (let k = k0; k < k1 && k < N / 2; k++) mx = Math.max(mx, Math.hypot(re[k], im[k])); out[b] = PX.clamp((20 * Math.log10(mx / (N / 4) + 1e-9) + 70) / 70, 0, 1); }
  return { bands: out, wave: w };
};
PX.spectrumSmooth = (t, bands, src, smooth) => {
  smooth = smooth == null ? 0.6 : PX.clamp(smooth, 0, 1); const now = PX.spectrum(t, bands, src); if (smooth < 0.02) return now;
  const steps = 2 + Math.round(smooth * 7), gap = 0.022, keep = 0.6 + smooth * 0.3, acc = new Float32Array(bands); let wsum = 0;
  for (let k = 0; k <= steps; k++) { const sp = k ? PX.spectrum(t - k * gap, bands, src).bands : now.bands, w = Math.pow(keep, k); wsum += w; for (let b = 0; b < bands; b++) acc[b] += sp[b] * w; }
  const out = new Float32Array(bands); for (let b = 0; b < bands; b++) out[b] = Math.min(1, acc[b] / wsum * (1 + smooth * 0.15));
  // and the bars next to each other move together a little (no single bar flickering on its own)
  if (smooth > 0.3) { const o2 = Float32Array.from(out); for (let b = 1; b < bands - 1; b++) out[b] = o2[b] * 0.6 + (o2[b - 1] + o2[b + 1]) * 0.2; }
  return { bands: out, wave: now.wave };
};
// a smoothed waveform: the fast wobble taken out, so the line flows instead of shaking
PX.smoothWave = (w, smooth) => { const n = Math.round((smooth == null ? 0.6 : smooth) * 28); if (n < 2) return w; const out = new Float32Array(w.length); let acc = 0; for (let i = 0; i < w.length; i++) { acc += w[i]; if (i >= n) acc -= w[i - n]; out[i] = acc / Math.min(i + 1, n) * (1 + n / 14); } return out; };
// bass energy now compared with the last second: a pulse on every kick
PX.bassPulse = (t, src) => { const e = (tt) => { const w = PX.mixWindow(tt, 1024, src); let lp = 0, s = 0; for (let i = 0; i < w.length; i++) { lp += (w[i] - lp) * 0.02; s += lp * lp; } return Math.sqrt(s / w.length); }; const now = e(t); let avg = 0; for (let k = 1; k <= 6; k++) avg += e(t - k * 0.15); avg /= 6; return PX.clamp((now - avg * 1.1) / (avg + 0.01) * 1.5, 0, 1); };
PX.VISUALS = [['bars', 'Spectrum bars'], ['cspec', 'Circular spectrum'], ['wave', 'Horizontal waveform'], ['cwave', 'Circular waveform'], ['minwave', 'Minimal waveform'], ['pulse', 'Bass pulse'], ['gradient', 'Animated gradient (reacts to bass)'], ['geo', 'Geometric shapes (react to the music)']];
PX.defaultVis = () => ({ style: 'bars', color: '#a85cff', color2: '#3ce1ff', intensity: 1, smooth: 0.6, size: 1, bands: 64, source: 'mix', thick: 1, glow: 1, mirror: true });
PX.drawVisual = (g, c, t, W, H) => {
  const v = c.vis, k = W / 1920, src = v.source === 'mix' ? null : v.source; const cx = W / 2 + c.x * W / 2, cy = H / 2 + c.y * H / 2, S = Math.min(W, H) * 0.32 * (c.scale || 1) * v.size;
  g.save(); g.globalAlpha = c.opacity; g.translate(cx, cy); g.rotate((c.rot || 0) * Math.PI / 180); if (v.glow > 0) { g.shadowColor = v.color; g.shadowBlur = 18 * k * v.glow; }
  const grad = (a, b) => { const gr = g.createLinearGradient(-S, 0, S, 0); gr.addColorStop(0, v.color); gr.addColorStop(1, v.color2); return gr; };
  if (v.style === 'bars' || v.style === 'cspec') {
    const sp = PX.spectrumSmooth(t, v.bands, src, v.smooth).bands;
    if (v.style === 'bars') { const w = S * 3, bw = w / sp.length; g.fillStyle = grad(); for (let i = 0; i < sp.length; i++) { const h = sp[i] * S * 1.1 * v.intensity; g.fillRect(-w / 2 + i * bw + bw * 0.15, v.mirror ? -h / 2 : -h, bw * 0.7, Math.max(2 * k, h)); } }
    else { const r0 = S * 0.55; g.strokeStyle = grad(); g.lineWidth = Math.max(2, (Math.PI * 2 * r0 / sp.length) * 0.55); g.lineCap = 'round'; for (let i = 0; i < sp.length; i++) { const an = i / sp.length * Math.PI * 2 - Math.PI / 2, h = sp[i] * S * 0.6 * v.intensity; g.beginPath(); g.moveTo(Math.cos(an) * r0, Math.sin(an) * r0); g.lineTo(Math.cos(an) * (r0 + h + 2 * k), Math.sin(an) * (r0 + h + 2 * k)); g.stroke(); } }
  } else if (v.style === 'wave' || v.style === 'minwave' || v.style === 'cwave') {
    const w = PX.smoothWave(PX.mixWindow(t, 1024, src), v.smooth); g.strokeStyle = grad(); g.lineWidth = (v.style === 'minwave' ? 2 : 4) * k * v.thick; g.beginPath();
    if (v.style === 'cwave') { const r0 = S * 0.6; for (let i = 0; i <= w.length; i++) { const an = i / w.length * Math.PI * 2, r = r0 + w[i % w.length] * S * 0.5 * v.intensity; const px = Math.cos(an) * r, py = Math.sin(an) * r; i ? g.lineTo(px, py) : g.moveTo(px, py); } g.closePath(); }
    else { const L = S * 3; for (let i = 0; i < w.length; i++) { const px = -L / 2 + i / (w.length - 1) * L, py = w[i] * S * (v.style === 'minwave' ? 0.35 : 0.8) * v.intensity; i ? g.lineTo(px, py) : g.moveTo(px, py); } }
    g.stroke();
  } else if (v.style === 'pulse') {
    const sm = v.smooth == null ? 0.6 : v.smooth; let p = PX.bassPulse(t, src); if (sm > 0.02) for (let k = 1; k <= 4; k++) p = Math.max(p, PX.bassPulse(t - k * 0.04, src) * Math.pow(0.5 + sm * 0.4, k)); p *= v.intensity; g.fillStyle = v.color; g.globalAlpha *= 0.25 + 0.75 * p; g.beginPath(); g.arc(0, 0, S * (0.5 + p * 0.5), 0, Math.PI * 2); g.fill(); g.globalAlpha = c.opacity * 0.9; g.strokeStyle = v.color2; g.lineWidth = 6 * k; g.beginPath(); g.arc(0, 0, S * (0.7 + p * 0.6), 0, Math.PI * 2); g.stroke();
  } else if (v.style === 'gradient') {
    const p = PX.bassPulse(t, src) * v.intensity; g.shadowBlur = 0; g.setTransform(1, 0, 0, 1, 0, 0); const an = t * 0.4; const gr = g.createLinearGradient(W / 2 + Math.cos(an) * W, H / 2 + Math.sin(an) * H, W / 2 - Math.cos(an) * W, H / 2 - Math.sin(an) * H); gr.addColorStop(0, v.color); gr.addColorStop(1, v.color2); g.fillStyle = gr; g.globalAlpha = c.opacity * (0.55 + 0.45 * p); g.fillRect(0, 0, W, H);
  } else if (v.style === 'geo') {
    const sp = PX.spectrumSmooth(t, 12, src, v.smooth).bands, p = PX.bassPulse(t, src); g.lineWidth = 3 * k; for (let i = 0; i < 6; i++) { const r = S * (0.3 + i * 0.16) * (1 + sp[i * 2] * 0.35 * v.intensity), sides = 3 + (i % 4); g.strokeStyle = i % 2 ? v.color2 : v.color; g.globalAlpha = c.opacity * (0.4 + sp[i * 2] * 0.6); g.beginPath(); for (let s = 0; s <= sides; s++) { const an = s / sides * Math.PI * 2 + t * (0.2 + i * 0.07) * (i % 2 ? 1 : -1) + p * 0.3; const px = Math.cos(an) * r, py = Math.sin(an) * r; s ? g.lineTo(px, py) : g.moveTo(px, py); } g.stroke(); }
  }
  g.restore();
};

// ---------------- the compositor ----------------
// draws a visual clip with its transform, crop, fit, effects and the transition it is in
PX.drawClip = (g, c, t, W, H, exporting) => {
  const role = PX.roleAt(c, t); if (!role) return;
  if (c.type === 'text') { if (t >= c.start && t < c.start + c.dur) PX.drawText(g, c, t, W, H); return; }
  if (c.type === 'visual') { if (t >= c.start && t < c.start + c.dur) PX.drawVisual(g, c, t, W, H); return; }
  const m = PX.mediaById(c.media); if (!m || m.missing) return; let src, sw, sh;
  if (c.type === 'image') { src = PX.imgFor(m); if (!src.complete || !src.naturalWidth) return; sw = src.naturalWidth; sh = src.naturalHeight; }
  else { src = PX.videoFor(c); if (!src || src.readyState < 2) return; sw = src.videoWidth; sh = src.videoHeight; }
  const cl = c.crop, cx0 = sw * cl.l / 100, cy0 = sh * cl.t / 100, cw = Math.max(1, sw * (1 - (cl.l + cl.r) / 100)), ch = Math.max(1, sh * (1 - (cl.t + cl.b) / 100));
  let dw, dh; const ar = cw / ch, car = W / H;
  if (c.fit === 'fill') { if (ar > car) { dh = H; dw = H * ar; } else { dw = W; dh = W / ar; } } else if (c.fit === 'stretch') { dw = W; dh = H; } else { if (ar > car) { dw = W; dh = W / ar; } else { dh = H; dw = H * ar; } }
  let alpha = c.opacity, sc = c.scale, ox = 0, blurAdd = 0, clipRect = null, white = 0, black = 0;
  const p = role.p;
  if (c.ken && c.type === 'image') { const kp = PX.clamp((t - c.start) / c.dur, 0, 1); sc *= 1 + (c.ken.zoom || 0.12) * kp; ox += (c.ken.pan || 0) * kp * W * 0.05; }
  if (role.role === 'B' || role.role === 'B0') {
    const q = p; switch (role.type) {
      case 'dissolve': if (role.role === 'B') alpha *= q; else alpha *= q; break;
      case 'fadeblack': if (role.role === 'B') { if (q < 0.5) alpha = 0; else black = 1 - (q - 0.5) * 2; } else black = 1 - q; break;
      case 'fadewhite': if (role.role === 'B') { if (q < 0.5) alpha = 0; else white = 1 - (q - 0.5) * 2; } else white = 1 - q; break;
      case 'wipe': clipRect = [0, 0, W * q, H]; break;
      case 'slide': ox += (1 - q) * W; break;
      case 'zoom': alpha *= q; sc *= 0.8 + 0.2 * q; break;
      case 'blur': alpha *= Math.min(1, q * 2); blurAdd = (1 - q) * 24; break;
    }
  } else if (role.role === 'A' || role.role === 'A0') {
    const q = p; switch (role.type) {
      case 'fadeblack': if (role.role === 'A') { if (q >= 0.5) alpha = 0; else black = q * 2; } else black = q; break;
      case 'fadewhite': if (role.role === 'A') { if (q >= 0.5) alpha = 0; else white = q * 2; } else white = q; break;
      case 'slide': ox -= q * W; break;
      case 'zoom': sc *= 1 + 0.25 * q; if (role.role === 'A0') alpha *= 1 - q; break;
      case 'blur': blurAdd = q * 24; if (role.role === 'A0') alpha *= 1 - q; break;
      case 'dissolve': if (role.role === 'A0') alpha *= 1 - q; break;
    }
  }
  if (alpha <= 0.002) return;
  g.save(); g.globalAlpha = PX.clamp(alpha, 0, 1);
  if (clipRect) { g.beginPath(); g.rect(clipRect[0], clipRect[1], clipRect[2], clipRect[3]); g.clip(); }
  g.translate(W / 2 + c.x * W / 2 + ox, H / 2 + c.y * H / 2); g.rotate((c.rot || 0) * Math.PI / 180); g.scale(sc, sc);
  const f = PX.filterFor(c, W / 1920); g.filter = blurAdd > 0 ? ((f === 'none' ? '' : f + ' ') + 'blur(' + (blurAdd * W / 1920).toFixed(1) + 'px)') : f;
  try { g.drawImage(src, cx0, cy0, cw, ch, -dw / 2, -dh / 2, dw, dh); } catch (e) { }
  g.filter = 'none';
  if (c.fx && c.fx.vignette > 0) { const vg = g.createRadialGradient(0, 0, Math.min(dw, dh) * 0.25, 0, 0, Math.hypot(dw, dh) * 0.55); vg.addColorStop(0, 'rgba(0,0,0,0)'); vg.addColorStop(1, 'rgba(0,0,0,' + (c.fx.vignette / 100 * 0.85).toFixed(3) + ')'); g.fillStyle = vg; g.fillRect(-dw / 2, -dh / 2, dw, dh); }
  if (black > 0) { g.fillStyle = 'rgba(0,0,0,' + PX.clamp(black, 0, 1) + ')'; g.fillRect(-dw / 2 - 2, -dh / 2 - 2, dw + 4, dh + 4); }
  if (white > 0) { g.fillStyle = 'rgba(255,255,255,' + PX.clamp(white, 0, 1) + ')'; g.fillRect(-dw / 2 - 2, -dh / 2 - 2, dw + 4, dh + 4); }
  g.restore();
};
// the whole frame: background, then the tracks from the bottom of the list up (the top track is drawn last, on top)
PX.drawFrame = (g, t, W, H, exporting) => {
  g.setTransform(1, 0, 0, 1, 0, 0); g.globalAlpha = 1; g.filter = 'none'; g.fillStyle = PX.P.settings.bg || '#000'; g.fillRect(0, 0, W, H);
  const tracks = PX.P.tracks.slice().reverse();
  for (const tr of tracks) { if (tr.kind === 'audio' || tr.hidden) continue; for (const c of PX.clipsOn(tr.id)) if (PX.isVisualKind(c)) PX.drawClip(g, c, t, W, H, exporting); }
};
// video elements at the right place for time t (preview: keep playing; export: exact seek)
PX.visibleVideoClips = (t) => PX.P.clips.filter(c => c.type === 'video' && PX.roleAt(c, t) && !(PX.trackById(c.track) || {}).hidden);
PX.syncVideos = (t, playing) => {
  for (const c of PX.visibleVideoClips(t)) {
    const v = PX.videoFor(c); if (!v) continue; const mt = PX.mediaTime(c, t);
    if (playing) { v.playbackRate = PX.clamp(c.speed, 0.0625, 16); if (Math.abs(v.currentTime - mt) > 0.25) v.currentTime = mt; if (v.paused) v.play().catch(() => { }); }
    else { if (!v.paused) v.pause(); if (Math.abs(v.currentTime - mt) > 0.02) v.currentTime = mt; }
  }
  if (playing) for (const id in PX.vids) { const c = PX.clipById(id); if (!c || !PX.roleAt(c, t)) { if (!PX.vids[id].paused) PX.vids[id].pause(); } }
};
PX.seekExact = (t) => Promise.all(PX.visibleVideoClips(t).map(c => new Promise(res => { const v = PX.videoFor(c); if (!v) return res(); const mt = PX.mediaTime(c, t); const go = () => { if (Math.abs(v.currentTime - mt) < 0.001 && v.readyState >= 2) return res(); const done = () => { v.removeEventListener('seeked', done); res(); }; v.addEventListener('seeked', done); v.currentTime = mt; setTimeout(done, 4000); }; if (v.readyState >= 1) go(); else { v.addEventListener('loadedmetadata', go, { once: true }); setTimeout(res, 6000); } })));

// ---------------- audio: the same schedule live and offline ----------------
// gain envelope of a clip: its volume, the track volume, fade in / out and transitions (a crossfade over the transition window)
PX.scheduleAudio = (ctx, dest, t0, t1, live) => {
  const nodes = []; const solo = PX.P.tracks.some(tr => tr.kind === 'audio' && tr.solo); const base = live ? ctx.currentTime + 0.08 : 0;
  for (const c of PX.P.clips) {
    if (c.type !== 'video' && c.type !== 'audio') continue; const m = PX.mediaById(c.media); if (!m || m.missing) continue; const ab = PX.audio[m.id]; if (!ab) continue;
    const tr = PX.trackById(c.track); if (!tr) continue; if (tr.kind === 'audio' && (tr.mute || (solo && !tr.solo))) continue; if (tr.kind === 'video' && (tr.hidden || c.mute)) continue;
    const role0 = c.transIn && c.transIn.type !== 'none' && PX.partnerBefore(c) ? c.transIn.dur / 2 : 0, nx = PX.partnerAfter(c), role1 = nx ? nx.transIn.dur / 2 : 0;
    const cs = c.start - role0, ce = c.start + c.dur + role1; if (ce <= t0 || cs >= t1) continue;
    const from = Math.max(cs, t0), mt = c.in + (from - c.start) * c.speed; if (mt < 0 || mt >= ab.duration) continue;
    const src = ctx.createBufferSource(); src.buffer = ab; src.playbackRate.value = c.speed; const gn = ctx.createGain(); src.connect(gn); gn.connect(dest);
    const level = c.vol * (tr.vol == null ? 1 : tr.vol) * (c.gainNorm || 1), at = (tt) => base + (tt - t0);
    gn.gain.setValueAtTime(0, at(from)); const env = (tt) => { let gg = level; const lt = tt - c.start; if (c.fadeIn > 0 && lt < c.fadeIn) gg *= PX.clamp(lt / c.fadeIn, 0, 1); if (c.fadeOut > 0 && lt > c.dur - c.fadeOut) gg *= PX.clamp((c.dur - lt) / c.fadeOut, 0, 1); if (role0 > 0 && tt < c.start + role0) gg *= PX.clamp((tt - (c.start - role0)) / (2 * role0), 0, 1); if (role1 > 0 && tt > c.start + c.dur - role1) gg *= PX.clamp((c.start + c.dur + role1 - tt) / (2 * role1), 0, 1); return gg; };
    for (let tt = from; tt <= ce; tt += 0.05) gn.gain.linearRampToValueAtTime(env(tt), at(tt));
    gn.gain.linearRampToValueAtTime(0, at(ce)); src.start(at(from), mt); src.stop(at(ce) + 0.01); nodes.push(src);
  }
  return nodes;
};
PX.renderAudio = async (t0, t1) => {
  const sr = 48000, len = Math.max(1, Math.ceil((t1 - t0) * sr)); const ctx = new OfflineAudioContext(2, len, sr); PX.scheduleAudio(ctx, ctx.destination, t0, t1, false); return await ctx.startRendering();
};
PX.wav = (ab) => { const n = ab.length, ch = 2, buf = new ArrayBuffer(44 + n * ch * 2), d = new DataView(buf); const w = (o, s) => { for (let i = 0; i < s.length; i++) d.setUint8(o + i, s.charCodeAt(i)); }; w(0, 'RIFF'); d.setUint32(4, 36 + n * ch * 2, true); w(8, 'WAVE'); w(12, 'fmt '); d.setUint32(16, 16, true); d.setUint16(20, 1, true); d.setUint16(22, ch, true); d.setUint32(24, ab.sampleRate, true); d.setUint32(28, ab.sampleRate * ch * 2, true); d.setUint16(32, ch * 2, true); d.setUint16(34, 16, true); w(36, 'data'); d.setUint32(40, n * ch * 2, true); const L = ab.getChannelData(0), R = ab.numberOfChannels > 1 ? ab.getChannelData(1) : L; let o = 44; for (let i = 0; i < n; i++) { d.setInt16(o, PX.clamp(L[i], -1, 1) * 32767, true); d.setInt16(o + 2, PX.clamp(R[i], -1, 1) * 32767, true); o += 4; } return new Blob([buf], { type: 'audio/wav' }); };

// ---------------- playback ----------------
PX.play = { on: false, t: 0, t0: 0, c0: 0, nodes: [], startedAt: 0 };
PX.now = () => PX.play.on ? PX.play.t0 + (PX.ac().currentTime - PX.play.c0) : PX.play.t;
PX.startPlay = () => {
  const ctx = PX.ac(); if (ctx.state === 'suspended') ctx.resume(); const dur = PX.projectDur(); if (PX.play.t >= dur - 0.02) PX.play.t = 0;
  PX.play.on = true; PX.play.t0 = PX.play.t; PX.play.startedAt = PX.play.t; PX.play.c0 = ctx.currentTime + 0.08;
  PX.play.nodes = PX.scheduleAudio(ctx, ctx.destination, PX.play.t, Math.max(dur, PX.play.t + 0.1), true);
  const missing = PX.P.clips.some(c => (c.type === 'audio' || c.type === 'video') && PX.mediaById(c.media) && PX.mediaById(c.media).hasAudio && !PX.audio[c.media]);
  if (missing) PX.toast('Some audio is still being read: it will play once it is ready.');
  PX.send({ t: 'playing', on: true }); PX.loop();
};
PX.stopPlay = (back) => {
  if (!PX.play.on) { if (back) PX.seek(0); return; } PX.play.t = PX.now(); PX.play.on = false; for (const n of PX.play.nodes) try { n.stop(); } catch (e) { } PX.play.nodes = [];
  for (const id in PX.vids) PX.vids[id].pause(); if (back) PX.play.t = PX.play.startedAt; PX.send({ t: 'playing', on: false }); PX.seek(PX.play.t);
};
PX.togglePlay = () => PX.play.on ? PX.stopPlay(false) : PX.startPlay();
PX.seek = (t) => { const was = PX.play.on; if (was) { for (const n of PX.play.nodes) try { n.stop(); } catch (e) { } PX.play.nodes = []; PX.play.on = false; } PX.play.t = Math.max(0, t); PX.syncVideos(PX.play.t, false); PX.redraw(); PX.onTime && PX.onTime(PX.play.t); if (was) PX.startPlay(); };
PX.loop = () => {
  if (!PX.play.on) return; const t = PX.now(); const dur = PX.projectDur();
  if (t >= dur) { PX.stopPlay(false); PX.seek(dur); return; }
  PX.syncVideos(t, true); PX.drawMonitor(t); PX.onTime && PX.onTime(t); if (Math.random() < 0.02) PX.releaseFar(t); requestAnimationFrame(PX.loop);
};
PX.redraw = () => { if (!PX.play.on) { cancelAnimationFrame(PX._rd); PX._rd = requestAnimationFrame(() => PX.drawMonitor(PX.play.t)); } };
PX.drawMonitor = (t) => { const cv = document.getElementById('monitor'); if (!cv) return; const g = cv.getContext('2d'); PX.drawFrame(g, t, cv.width, cv.height, false); PX.afterDraw && PX.afterDraw(g, cv.width, cv.height, t); };
// a still frame that is waiting for its video to seek is redrawn when it arrives
setInterval(() => { if (!PX.play.on) { let need = false; for (const id in PX.vids) if (PX.vids[id].readyState >= 2 && !PX.vids[id]._drawn) { PX.vids[id]._drawn = true; need = true; } if (need) PX.redraw(); } }, 250);
document.addEventListener('seeked', () => PX.redraw(), true);
