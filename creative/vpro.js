'use strict';
// =====================================================================
//  PURPHEX MUSIC VIDEO EDITOR: keyframes, audio tools and timeline tools.
//  KEYFRAMES: position, size, rotation, opacity and volume change over a clip (real time / value pairs, smoothly in between,
//  the same in the preview and the export). AUDIO: an EQ (low, mid, high), a compressor, and ducking (music goes down while a
//  voice-over plays), all in the Web Audio mix that both the preview and the export use. TIMELINE: ripple delete and close gaps.
// =====================================================================
(() => {
const UI = PX.UI, clamp = PX.clamp, $ = (s, r) => (r || document).querySelector(s);
const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; };
// ---------------- keyframes ----------------
PX.KF_PROPS = [['x', 'Position X'], ['y', 'Position Y'], ['scale', 'Size'], ['rot', 'Rotation'], ['opacity', 'Opacity'], ['vol', 'Volume']];
const ease = (p) => p < 0.5 ? 2 * p * p : 1 - Math.pow(-2 * p + 2, 2) / 2;
PX.kfValue = (c, prop, t) => {   // t: time on the timeline
  const ks = c.kf && c.kf[prop]; if (!ks || !ks.length) return null; const lt = t - c.start;
  if (lt <= ks[0][0]) return ks[0][1]; const last = ks[ks.length - 1]; if (lt >= last[0]) return last[1];
  for (let i = 1; i < ks.length; i++) if (lt < ks[i][0]) { const a = ks[i - 1], b = ks[i], p = (lt - a[0]) / Math.max(1e-6, b[0] - a[0]); return a[1] + (b[1] - a[1]) * ease(p); }
  return last[1];
};
// a clip as it is at time t (its keyframed values on top of its own)
PX.kfApply = (c, t) => { if (!c.kf) return c; let o = null; for (const [p] of PX.KF_PROPS) { if (p === 'vol') continue; const v = PX.kfValue(c, p, t); if (v != null) { if (!o) o = Object.create(c); o[p] = v; } } return o || c; };
PX.addKey = (c, prop, t, v) => { c.kf = c.kf || {}; const ks = c.kf[prop] = c.kf[prop] || []; const lt = clamp(t - c.start, 0, c.dur); const i = ks.findIndex(k => Math.abs(k[0] - lt) < 0.04); if (i >= 0) ks[i][1] = v; else { ks.push([lt, v]); ks.sort((a, b) => a[0] - b[0]); } };
// ---------------- audio: volume keyframes and ducking ----------------
const voiceAt = (c, t) => PX.P.clips.some(o => o !== c && o.voice && (o.type === 'audio' || o.type === 'video') && t >= o.start - 0.15 && t <= o.start + o.dur + 0.15);
PX.audioMul = (c, t) => {
  let m = 1; const kv = PX.kfValue(c, 'vol', t); if (kv != null) m *= kv / Math.max(1e-6, c.vol || 1);
  if (c.duck && c.duck.on && !c.voice) { const dB = c.duck.db == null ? 12 : c.duck.db; let near = 0; for (const d of [0, -0.1, 0.1, -0.2, 0.2]) if (voiceAt(c, t + d)) near++; m *= 1 - (1 - Math.pow(10, -dB / 20)) * (near / 5); }
  return m;
};
// the EQ and the compressor go between the sound and its volume
PX.audioChain = (ctx, c, src, gain) => {
  const eq = c.eq, cp = c.comp; let node = src;
  if (eq && (eq.low || eq.mid || eq.high)) { const lo = ctx.createBiquadFilter(); lo.type = 'lowshelf'; lo.frequency.value = 200; lo.gain.value = eq.low || 0; const mi = ctx.createBiquadFilter(); mi.type = 'peaking'; mi.frequency.value = 1500; mi.Q.value = 0.9; mi.gain.value = eq.mid || 0; const hi = ctx.createBiquadFilter(); hi.type = 'highshelf'; hi.frequency.value = 6000; hi.gain.value = eq.high || 0; node.connect(lo); lo.connect(mi); mi.connect(hi); node = hi; }
  if (cp && cp.on) { const k = ctx.createDynamicsCompressor(); k.threshold.value = cp.threshold == null ? -24 : cp.threshold; k.ratio.value = cp.ratio || 4; k.attack.value = 0.005; k.release.value = 0.2; k.knee.value = 6; const mk = ctx.createGain(); mk.gain.value = Math.pow(10, (cp.makeup || 0) / 20); node.connect(k); k.connect(mk); node = mk; }
  node.connect(gain);
};
// ---------------- the timeline: ripple delete and close gaps ----------------
UI.rippleDelete = () => {
  const s = UI.selected().filter(c => !(PX.trackById(c.track) || {}).locked); if (!s.length) return PX.toast('Select a clip first.');
  PX.begin(); for (const c of s.sort((a, b) => b.start - a.start)) { const end = c.start + c.dur; PX.P.clips = PX.P.clips.filter(x => x !== c); for (const o of PX.P.clips) if (o.track === c.track && o.start >= end - 0.001) o.start -= c.dur; }
  UI.sel = new Set(); PX.commit('ripple delete'); PX.toast('Deleted, and the clips after it moved up to close the gap.');
};
UI.closeGaps = () => {
  PX.begin(); let n = 0; for (const tr of PX.P.tracks) { const cl = PX.P.clips.filter(c => c.track === tr.id).sort((a, b) => a.start - b.start); let t = cl.length ? Math.min(cl[0].start, 0) : 0; t = 0; for (const c of cl) { if (c.start > t + 0.01) { c.start = t; n++; } t = Math.max(t, c.start + c.dur); } }
  PX.commit('close gaps'); PX.toast(n ? 'Closed ' + n + ' gap' + (n > 1 ? 's' : '') + '.' : 'There were no gaps.');
};
// ---------------- the inspector: keyframes and audio tools ----------------
const prevExtra = UI.extraInspector; UI.extraInspector = (c, m, sec, num, sel, col, chk) => {
  if (prevExtra) prevExtra(c, m, sec, num, sel, col, chk);
  const hasAudio = c.type === 'audio' || (c.type === 'video' && m && m.hasAudio !== false);
  if (hasAudio) sec('AUDIO TOOLS (EQ, COMPRESSOR, DUCKING)', b => {
    c.eq = c.eq || { low: 0, mid: 0, high: 0 }; num(b, 'Low (bass) dB', () => c.eq.low || 0, v => c.eq.low = v, -15, 15, 0.5); num(b, 'Mid dB', () => c.eq.mid || 0, v => c.eq.mid = v, -15, 15, 0.5); num(b, 'High (treble) dB', () => c.eq.high || 0, v => c.eq.high = v, -15, 15, 0.5);
    c.comp = c.comp || { on: false, threshold: -24, ratio: 4, makeup: 0 }; chk(b, 'Compressor', () => !!c.comp.on, v => c.comp.on = v); num(b, 'Threshold dB', () => c.comp.threshold == null ? -24 : c.comp.threshold, v => c.comp.threshold = v, -60, 0, 1); num(b, 'Ratio', () => c.comp.ratio || 4, v => c.comp.ratio = v, 1, 20, 0.5); num(b, 'Make-up gain dB', () => c.comp.makeup || 0, v => c.comp.makeup = v, 0, 24, 0.5);
    chk(b, 'This is a voice-over', () => !!c.voice, v => c.voice = v); c.duck = c.duck || { on: false, db: 12 }; chk(b, 'Go quieter under voice-overs', () => !!c.duck.on, v => c.duck.on = v); num(b, 'How much quieter (dB)', () => c.duck.db == null ? 12 : c.duck.db, v => c.duck.db = v, 3, 30, 1);
    b.appendChild(el('p', 'hint', 'These are heard live while you play, and the export sounds the same.'));
  }, true);
  if (c.type === 'video' || c.type === 'image' || c.type === 'text' || c.type === 'visual' || c.type === 'audio') sec('KEYFRAMES', b => {
    b.appendChild(el('p', 'hint', 'Move the playhead, set a value (in TRANSFORM, AUDIO or on the preview), then press ◆ to keep it at that moment. Between keyframes it changes smoothly. Press ◆ again on the same spot to change a keyframe.'));
    const props = PX.KF_PROPS.filter(([p]) => p === 'vol' ? hasAudio : c.type !== 'audio');
    for (const [p, label] of props) {
      const ks = (c.kf && c.kf[p]) || []; const r = el('div', 'kfrow'); r.appendChild(el('span', 'kfl', label));
      const add = el('button', 'kfadd', '◆'); add.title = 'Keep the ' + label.toLowerCase() + ' at the playhead (' + PX.fmtTC(PX.play.t, PX.P.settings.fps) + ')'; add.onclick = () => { if (PX.play.t < c.start || PX.play.t > c.start + c.dur) return PX.toast('Put the playhead inside the clip first.', true); PX.begin(); PX.addKey(c, p, PX.play.t, c[p] == null ? (p === 'scale' || p === 'opacity' || p === 'vol' ? 1 : 0) : c[p]); PX.commit('keyframe'); };
      r.appendChild(add); for (const k of ks) { const d = el('button', 'kfdot', (c.start + k[0]).toFixed(2) + 's'); d.title = label + ' ' + (+k[1]).toFixed(2) + ' at ' + (c.start + k[0]).toFixed(2) + ' s (click to go there, right-click to remove)'; d.onclick = () => { PX.seek ? PX.seek(c.start + k[0]) : (PX.play.t = c.start + k[0]); UI.placePlayhead && UI.placePlayhead(PX.play.t); PX.redraw(); }; d.oncontextmenu = (e) => { e.preventDefault(); PX.begin(); c.kf[p] = c.kf[p].filter(q => q !== k); if (!c.kf[p].length) delete c.kf[p]; PX.commit('remove keyframe'); }; r.appendChild(d); }
      b.appendChild(r);
    }
    const cl = el('button', '', 'CLEAR ALL KEYFRAMES'); cl.onclick = () => { PX.begin(); delete c.kf; PX.commit('clear keyframes'); }; b.appendChild(cl);
  }, !(c.kf && Object.keys(c.kf).length));
};
// keyframe diamonds on the clips in the timeline
const prevClipEl = UI.clipEl; UI.clipEl = (c, tr) => { const d = prevClipEl(c, tr); if (c.kf) { const times = new Set(); for (const p in c.kf) for (const k of c.kf[p]) times.add(Math.round(k[0] * 100) / 100); for (const t of times) { const k = el('span', 'kfmark'); k.style.left = UI.tToX(t) - 4 + 'px'; d.appendChild(k); } } return d; };
// the toolbar: ripple delete and close gaps next to DELETE
function bar() { const del = $('[data-act="del"]'); if (!del || $('#rippleBtn')) return; const add = (id, t, tip, fn) => { const b = el('button', '', t); b.id = id; b.title = tip; b.onclick = fn; del.after(b); }; add('gapsBtn', 'CLOSE GAPS', 'Move the clips on every track up so there are no empty gaps', UI.closeGaps); add('rippleBtn', 'RIPPLE DELETE', 'Delete the selected clips and move the clips after them up (Shift + Delete)', UI.rippleDelete); }
bar();
document.addEventListener('keydown', (e) => { if (UI.tool !== 'video' || e.key !== 'Delete' || !e.shiftKey) return; const tag = (e.target.tagName || '').toLowerCase(); if (tag === 'input' || tag === 'textarea') return; e.preventDefault(); e.stopImmediatePropagation(); UI.rippleDelete(); }, true);
})();
