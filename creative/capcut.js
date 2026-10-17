'use strict';
// =====================================================================
//  PURPHEX MUSIC VIDEO EDITOR: CapCut-style tools. FIND BEATS hears the song and marks every beat; AUTO CUT TO BEAT lays your
//  clips and photos out so every cut lands on the beat; MOTION gives a clip an animation (zoom, spin, slide, bounce, shake, or a
//  pulse / flash / shake on every beat); GREEN SCREEN takes a colour out; BLUR FILL puts a blurred copy behind a clip that does not
//  fill the frame (a wide clip in a vertical video); FREEZE FRAME makes a still of the frame under the playhead.
// =====================================================================
(() => {
const UI = PX.UI, clamp = PX.clamp, $ = (s, r) => (r || document).querySelector(s);
// ---------------- the beat ----------------
// the song: the longest audio clip on the timeline (or a video clip with sound)
function songClip() { let best = null; for (const c of PX.P.clips) { if (c.type !== 'audio' && c.type !== 'video') continue; const m = PX.mediaById(c.media); if (!m || !PX.audio[m.id]) continue; if (!best || c.dur > best.dur) best = c; } return best; }
// the beats in an audio buffer (seconds): kicks and hits from the low end, the tempo from how they repeat, then an even grid that sits on them
PX.beatsOf = (ab, from, to) => {
  const sr = ab.sampleRate, a = ab.getChannelData(0), b = ab.numberOfChannels > 1 ? ab.getChannelData(1) : a, hop = Math.round(sr / 100), s0 = Math.max(0, Math.floor(from * sr)), s1 = Math.min(a.length, Math.floor(to * sr));
  const n = Math.max(0, Math.floor((s1 - s0) / hop)), env = new Float32Array(n); let lp = 0, k = 1 - Math.exp(-2 * Math.PI * 160 / sr);
  for (let i = 0; i < n; i++) { let e = 0; for (let j = 0; j < hop; j++) { const x = (a[s0 + i * hop + j] + b[s0 + i * hop + j]) * 0.5; lp += (x - lp) * k; e += lp * lp + x * x * 0.15; } env[i] = Math.sqrt(e / hop); }
  const on = new Float32Array(n); for (let i = 1; i < n; i++) on[i] = Math.max(0, env[i] - env[i - 1]);
  // the tempo: which gap between hits repeats most (between 60 and 190 BPM)
  let bestLag = 50, bestScore = -1; for (let lag = 32; lag <= 100; lag++) { let s = 0; for (let i = lag; i < n; i++) s += on[i] * on[i - lag]; s *= 1 + (lag > 40 && lag < 75 ? 0.06 : 0); if (s > bestScore) { bestScore = s; bestLag = lag; } }
  // the grid: the starting point where the beats land on the most hits, nudged to each real hit nearby
  let bestPh = 0, bestSum = -1; for (let ph = 0; ph < bestLag; ph++) { let s = 0; for (let i = ph; i < n; i += bestLag) s += on[i] + (on[i - 1] || 0) * 0.5 + (on[i + 1] || 0) * 0.5; if (s > bestSum) { bestSum = s; bestPh = ph; } }
  const out = []; for (let i = bestPh; i < n; i += bestLag) { let bi = i, bv = on[i] || 0; for (let d = -3; d <= 3; d++) if ((on[i + d] || 0) > bv) { bv = on[i + d]; bi = i + d; } out.push(from + bi / 100); }
  return { beats: out, bpm: 6000 / bestLag };
};
PX.findBeats = () => {
  const c = songClip(); if (!c) { PX.toast('Put a song on the timeline first (MY SONG, MY PROJECTS or IMPORT).', true); return null; }
  const m = PX.mediaById(c.media), ab = PX.audio[m.id], r = PX.beatsOf(ab, c.in, c.in + c.dur * c.speed);
  const tl = r.beats.map(t => c.start + (t - c.in) / c.speed).filter(t => t >= c.start - 0.001 && t <= c.start + c.dur + 0.001);
  PX.begin(); PX.P.beats = tl; PX.P.bpm = Math.round(r.bpm); PX.P.markers = (PX.P.markers || []).filter(t => !(PX.P._beatMarks || []).includes(t)); PX.P._beatMarks = tl.slice(); PX.P.markers = PX.P.markers.concat(tl); PX.commit('beats');
  PX.toast('Found ' + tl.length + ' beats (about ' + Math.round(r.bpm) + ' BPM): they are marked on the timeline, and clips snap to them.'); return tl;
};
// lay the clips out so every cut is on a beat: each one lasts "every" beats, from the first beat
PX.autoBeatCut = (every, repeat) => {
  let beats = PX.P.beats; if (!beats || !beats.length) beats = PX.findBeats(); if (!beats || beats.length < 2) return;
  every = Math.max(1, every || 2); let clips = UI.selected().filter(c => c.type === 'video' || c.type === 'image');
  if (clips.length < 2) { const tr = PX.P.tracks.find(t => t.kind === 'video' && PX.P.clips.some(c => c.track === t.id && (c.type === 'video' || c.type === 'image'))); clips = tr ? PX.P.clips.filter(c => c.track === tr.id && (c.type === 'video' || c.type === 'image')) : []; }
  if (!clips.length) { PX.toast('Add some clips or photos to a video track first (or select the ones to cut).', true); return; }
  clips.sort((a, b) => a.start - b.start); const track = clips[0].track; PX.begin();
  let bi = 0, ci = 0; const made = []; const pool = clips.slice();
  while (bi + every < beats.length + (repeat ? 0 : 0)) {
    if (ci >= pool.length) { if (!repeat) break; const src = pool[ci % pool.length]; const cp = JSON.parse(JSON.stringify(src)); cp.id = PX.uid('c'); PX.P.clips.push(cp); pool.push(cp); }
    const c = pool[ci], t0 = beats[bi], t1 = beats[Math.min(beats.length - 1, bi + every)]; let dur = t1 - t0;
    if (c.type === 'video') { const m = PX.mediaById(c.media), left = m && m.dur ? (m.dur - c.in) / c.speed : dur; if (left < dur) dur = Math.max(0.2, left); }
    c.track = track; c.start = t0; c.dur = dur; c.transIn = null; made.push(c); bi += every; ci++;
  }
  for (const c of pool.slice(ci)) if (!made.includes(c) && repeat) PX.P.clips = PX.P.clips.filter(x => x !== c);
  PX.commit('auto cut to beat'); PX.toast('Cut ' + made.length + ' clips to the beat (' + every + ' beat' + (every > 1 ? 's' : '') + ' each).');
};
// ---------------- motion ----------------
PX.MOTIONS = [['none', 'None'], ['zoomin', 'Zoom in (slow push)'], ['zoomout', 'Zoom out (slow pull)'], ['kenburns', 'Ken Burns (zoom and drift)'], ['popin', 'Pop in'], ['spinin', 'Spin in'], ['slidel', 'Slide in from the left'], ['slider', 'Slide in from the right'], ['bounce', 'Bounce in'], ['shake', 'Shake (all the time)'], ['beatpulse', 'Pulse on every beat'], ['beatshake', 'Shake on every beat'], ['beatflash', 'Flash on every beat'], ['beatzoom', 'Zoom punch on every beat'], ['swing', 'Swing'], ['float', 'Float']];
const lastBeat = (t) => { const b = PX.P.beats; if (!b || !b.length) return -1; let lo = 0, hi = b.length - 1, r = -1; while (lo <= hi) { const mid = (lo + hi) >> 1; if (b[mid] <= t) { r = b[mid]; lo = mid + 1; } else hi = mid - 1; } return r; };
PX.motionAt = (c, t, W, H) => {
  const mo = c.motion, out = { s: 1, dx: 0, dy: 0, r: 0, a: 1, white: 0 }; if (!mo || !mo.type || mo.type === 'none') return out; const amt = mo.amt == null ? 1 : mo.amt, loc = t - c.start, p = clamp(loc / Math.max(0.01, c.dur), 0, 1);
  const inP = clamp(loc / 0.45, 0, 1), easeOut = 1 - Math.pow(1 - inP, 3); const sh = (f) => Math.sin(t * f * 6.283) * Math.sin(t * f * 4.1 + 1.3);
  const beatE = () => { const lb = lastBeat(t); if (lb < 0) { const bp = PX.bassPulse ? PX.bassPulse(t, null) : 0; return bp; } return Math.exp(-(t - lb) * 9); };
  switch (mo.type) {
    case 'zoomin': out.s = 1 + 0.18 * amt * p; break;
    case 'zoomout': out.s = 1 + 0.18 * amt * (1 - p); break;
    case 'kenburns': out.s = 1.05 + 0.14 * amt * p; out.dx = (p - 0.5) * W * 0.04 * amt; out.dy = (0.5 - p) * H * 0.025 * amt; break;
    case 'popin': out.s = inP < 1 ? 0.6 + 0.4 * easeOut + Math.sin(inP * Math.PI) * 0.12 * amt : 1; out.a = Math.min(1, inP * 2); break;
    case 'spinin': out.r = (1 - easeOut) * -180 * amt; out.s = 0.5 + 0.5 * easeOut; out.a = Math.min(1, inP * 2); break;
    case 'slidel': out.dx = -(1 - easeOut) * W * amt; break;
    case 'slider': out.dx = (1 - easeOut) * W * amt; break;
    case 'bounce': out.dy = inP < 1 ? -Math.abs(Math.cos(inP * Math.PI * 2.5)) * (1 - inP) * H * 0.25 * amt : 0; break;
    case 'shake': out.dx = sh(7) * W * 0.008 * amt; out.dy = sh(5.3) * H * 0.008 * amt; break;
    case 'beatpulse': out.s = 1 + 0.07 * amt * beatE(); break;
    case 'beatshake': { const e = beatE(); out.dx = sh(13) * W * 0.02 * amt * e; out.dy = sh(11) * H * 0.02 * amt * e; break; }
    case 'beatflash': out.white = 0.55 * amt * beatE(); break;
    case 'beatzoom': out.s = 1 + 0.16 * amt * beatE(); break;
    case 'swing': out.r = Math.sin(loc * 2.2) * 4 * amt; break;
    case 'float': out.dy = Math.sin(loc * 1.6) * H * 0.012 * amt; out.s = 1.02; break;
  }
  return out;
};
// ---------------- green screen ----------------
const keyCache = { cv: null };
PX.keyed = (c, src, sx, sy, sw, sh, exporting) => {
  const k = c.key; if (!k || !k.on) return null; const maxW = exporting ? 1920 : 900, sc = Math.min(1, maxW / sw), w = Math.max(1, Math.round(sw * sc)), h = Math.max(1, Math.round(sh * sc));
  const cv = keyCache.cv || (keyCache.cv = document.createElement('canvas')); if (cv.width !== w) cv.width = w; if (cv.height !== h) cv.height = h; const g = cv.getContext('2d', { willReadFrequently: true }); g.clearRect(0, 0, w, h); g.drawImage(src, sx, sy, sw, sh, 0, 0, w, h);
  const id = g.getImageData(0, 0, w, h), d = id.data, hex = (k.color || '#00ff00').replace('#', ''), kr = parseInt(hex.slice(0, 2), 16), kg = parseInt(hex.slice(2, 4), 16), kb = parseInt(hex.slice(4, 6), 16);
  const toCb = (r, g2, b) => -0.169 * r - 0.331 * g2 + 0.5 * b, toCr = (r, g2, b) => 0.5 * r - 0.419 * g2 - 0.081 * b, kcb = toCb(kr, kg, kb), kcr = toCr(kr, kg, kb), tol = (k.tol == null ? 40 : k.tol) * 1.6, soft = Math.max(1, (k.soft == null ? 12 : k.soft) * 1.6);
  for (let i = 0; i < d.length; i += 4) { const dist = Math.hypot(toCb(d[i], d[i + 1], d[i + 2]) - kcb, toCr(d[i], d[i + 1], d[i + 2]) - kcr); if (dist < tol) d[i + 3] = 0; else if (dist < tol + soft) { d[i + 3] = Math.round(d[i + 3] * (dist - tol) / soft); if (kg > kr && kg > kb) d[i + 1] = Math.min(d[i + 1], Math.max(d[i], d[i + 2])); } }   // (and the green spill on the edges is taken out)
  g.putImageData(id, 0, 0); return cv;
};
// ---------------- blur fill ----------------
PX.blurFill = (g, c, src, sx, sy, sw, sh, W, H, alpha) => { if (!c.blurFill) return; const s2 = Math.max(W / sw, H / sh) * 1.08; g.save(); g.globalAlpha = clamp(alpha, 0, 1); g.filter = 'blur(' + (32 * W / 1920).toFixed(1) + 'px) brightness(0.7)'; try { g.drawImage(src, sx, sy, sw, sh, (W - sw * s2) / 2, (H - sh * s2) / 2, sw * s2, sh * s2); } catch (e) { } g.restore(); };
// ---------------- freeze frame ----------------
PX.freezeFrame = async () => {
  const W = PX.P.settings.w, H = PX.P.settings.h, cv = document.createElement('canvas'); cv.width = W; cv.height = H; const g = cv.getContext('2d'); PX.drawFrame(g, PX.play.t, W, H, true);
  const blob = await new Promise(r => cv.toBlob(r, 'image/png')); const res = await fetch('https://export.purphex/art?what=video&name=' + encodeURIComponent('Freeze ' + PX.play.t.toFixed(2) + 's'), { method: 'POST', body: blob });
  PX.toast(res.ok ? 'Freeze frame made: it is in MEDIA (drag it onto the timeline).' : 'Could not make the freeze frame: ' + (await res.text()), !res.ok);
};
// ---------------- the inspector and the toolbar ----------------
UI.extraInspector = (c, m, sec, num, sel, col, chk) => {
  if (c.type !== 'video' && c.type !== 'image' && c.type !== 'text' && c.type !== 'visual') return;
  sec('MOTION (CAPCUT STYLE)', b => {
    c.motion = c.motion || { type: 'none', amt: 1 }; sel(b, 'Animation', PX.MOTIONS, () => c.motion.type, s => c.motion.type = s); num(b, 'Strength', () => c.motion.amt == null ? 1 : c.motion.amt, s => c.motion.amt = s, 0.1, 3, 0.05);
    const h = document.createElement('p'); h.className = 'hint'; h.textContent = PX.P.beats && PX.P.beats.length ? 'The "on every beat" ones follow the ' + PX.P.beats.length + ' beats found in your song.' : 'For the "on every beat" ones press FIND BEATS under the monitor (they follow the bass until then).'; b.appendChild(h);
  });
  if (c.type === 'video' || c.type === 'image') sec('GREEN SCREEN AND BACKGROUND', b => {
    c.key = c.key || { on: false, color: '#00ff00', tol: 40, soft: 12 }; chk(b, 'Green screen', () => !!c.key.on, v => c.key.on = v); col(b, 'Colour to remove', () => c.key.color || '#00ff00', v => c.key.color = v); num(b, 'Tolerance', () => c.key.tol == null ? 40 : c.key.tol, v => c.key.tol = v, 1, 100, 1); num(b, 'Soft edge', () => c.key.soft == null ? 12 : c.key.soft, v => c.key.soft = v, 0, 60, 1);
    chk(b, 'Blur fill behind', () => !!c.blurFill, v => c.blurFill = v);
    const fb = document.createElement('button'); fb.textContent = 'FREEZE FRAME (at the playhead)'; fb.onclick = PX.freezeFrame; b.appendChild(fb);
  }, true);
};
function bar() {
  const mk = document.querySelector('[data-act="marker"]'); if (!mk || document.querySelector('#beatBtn')) return;
  const add = (id, label, title, fn) => { const b = document.createElement('button'); b.id = id; b.textContent = label; b.title = title; b.onclick = fn; mk.after(b); return b; };
  add('freezeBtn', 'FREEZE', 'Make a still of the frame under the playhead (it goes into MEDIA)', PX.freezeFrame);
  add('beatCutBtn', 'AUTO CUT TO BEAT', 'Lay your clips and photos out so every cut lands on a beat (select the ones to use, or it takes the first video track)', () => {
    UI.modal('<h3>Auto cut to the beat</h3><p class="hint">Your clips and photos are put one after another, each lasting the same number of beats, so every cut lands on the beat. Select the clips first to use only those.</p><div class="f"><label>Each clip lasts</label><select id="acE"><option value="1">1 beat (fast)</option><option value="2" selected>2 beats</option><option value="4">4 beats (one bar)</option><option value="8">8 beats (two bars)</option></select></div><div class="f"><label>Fill the whole song</label><input type="checkbox" id="acR" style="justify-self:start"></div><div class="acts"><button id="mNo">CANCEL</button><button id="acGo" class="accent">CUT TO THE BEAT</button></div>', b2 => { $('#mNo', b2).onclick = UI.close; $('#acGo', b2).onclick = () => { const e = +$('#acE', b2).value, r = $('#acR', b2).checked; UI.close(); PX.autoBeatCut(e, r); }; });
  });
  add('beatBtn', 'FIND BEATS', 'Hear the song and mark every beat on the timeline (clips snap to them)', () => PX.findBeats());
}
bar();
})();
