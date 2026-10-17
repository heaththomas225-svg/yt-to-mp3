'use strict';
// =====================================================================
//  PURPHEX CREATIVE STUDIO: the Video Studio interface (timeline, media, inspector, monitor, projects, export).
// =====================================================================
const $ = (s, r) => (r || document).querySelector(s), $$ = (s, r) => Array.from((r || document).querySelectorAll(s));
const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; };
const esc = (s) => String(s == null ? '' : s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const UI = PX.UI = { pps: 60, sel: new Set(), snap: true, tool: 'video', fonts: ['Segoe UI', 'Arial', 'Impact', 'Georgia', 'Bahnschrift', 'Consolas', 'Trebuchet MS', 'Verdana', 'Times New Roman', 'Courier New'] };

// ---------------- dialogs ----------------
UI.modal = (html, onOpen) => { $('#modalBox').innerHTML = html; $('#modal').classList.remove('hidden'); if (onOpen) onOpen($('#modalBox')); };
UI.close = () => $('#modal').classList.add('hidden');
UI.confirm = (text, yes, label) => UI.modal('<h3>Are you sure?</h3><p>' + esc(text) + '</p><div class="acts"><button id="mNo">CANCEL</button><button id="mYes" class="danger">' + (label || 'YES') + '</button></div>', b => { $('#mNo', b).onclick = UI.close; $('#mYes', b).onclick = () => { UI.close(); yes(); }; });
UI.prompt = (title, val, ok) => UI.modal('<h3>' + esc(title) + '</h3><input id="mIn" style="width:100%" maxlength="80"><div class="acts"><button id="mNo">CANCEL</button><button id="mOk" class="accent">OK</button></div>', b => { const i = $('#mIn', b); i.value = val || ''; i.focus(); i.select(); const go = () => { const v = i.value.trim(); if (!v) return; UI.close(); ok(v); }; $('#mOk', b).onclick = go; i.onkeydown = e => { if (e.key === 'Enter') go(); if (e.key === 'Escape') UI.close(); }; $('#mNo', b).onclick = UI.close; });
UI.ctx = (x, y, items) => { $$('.ctx').forEach(e => e.remove()); const m = el('div', 'ctx'); for (const it of items) { if (it === '-') { m.appendChild(el('hr')); continue; } const d = el('div', '', esc(it[0])); d.onclick = () => { m.remove(); it[1](); }; m.appendChild(d); } document.body.appendChild(m); m.style.left = Math.min(x, innerWidth - m.offsetWidth - 8) + 'px'; m.style.top = Math.min(y, innerHeight - m.offsetHeight - 8) + 'px'; setTimeout(() => document.addEventListener('mousedown', function h(e) { if (!m.contains(e.target)) { m.remove(); document.removeEventListener('mousedown', h); } }), 0); };

// ---------------- project presets ----------------
UI.PRESETS = [
  ['mv169', 'Music video 16:9', 1920, 1080, 30], ['mv916', 'Vertical video 9:16', 1080, 1920, 30], ['sq', 'Square video 1:1', 1080, 1080, 30], ['p45', 'Social video 4:5', 1080, 1350, 30],
  ['lyr169', 'Lyric video 16:9', 1920, 1080, 30], ['lyr916', 'Lyric video 9:16', 1080, 1920, 30], ['cover', 'Cover artwork video', 1920, 1080, 30], ['vis', 'Audio visualiser video', 1920, 1080, 30],
  ['teaser', 'Promotional teaser (15 s)', 1080, 1920, 30], ['short', 'Short clip for social', 1080, 1920, 30], ['4k', 'Music video 4K', 3840, 2160, 30], ['custom', 'Custom size...', 0, 0, 0]];
UI.newProjectDialog = (after) => {
  UI.modal('<h3>New video project</h3><p class="hint">Pick what you are making: it sets the real size and frame rate. You can change it later.</p><div class="grid2" id="npg"></div><div class="f" style="margin-top:10px"><label>Name</label><input type="text" id="npName" value="My music video"></div><div id="npCustom" class="hidden"><div class="f"><label>Width</label><input type="number" id="npW" value="1920" min="128" max="7680"><span></span></div><div class="f"><label>Height</label><input type="number" id="npH" value="1080" min="128" max="4320"><span></span></div><div class="f"><label>Frame rate</label><select id="npF"><option>24</option><option>25</option><option selected>30</option><option>50</option><option>60</option></select></div></div><div class="acts"><button id="mNo">CANCEL</button><button id="npOk" class="accent">CREATE</button></div>', b => {
    let pick = UI.PRESETS[0]; const g = $('#npg', b);
    for (const p of UI.PRESETS) { const c = el('div', 'card', esc(p[1]) + '<small>' + (p[2] ? p[2] + ' x ' + p[3] + ', ' + p[4] + ' fps' : 'your own size') + '</small>'); c.onclick = () => { pick = p; $$('.card', g).forEach(x => x.style.borderColor = ''); c.style.borderColor = 'var(--accent)'; $('#npCustom', b).classList.toggle('hidden', p[0] !== 'custom'); }; g.appendChild(c); if (p === pick) c.style.borderColor = 'var(--accent)'; }
    $('#mNo', b).onclick = UI.close;
    $('#npOk', b).onclick = () => {
      let w = pick[2], h = pick[3], f = pick[4]; if (pick[0] === 'custom') { w = PX.clamp(parseInt($('#npW', b).value) || 1920, 128, 7680); h = PX.clamp(parseInt($('#npH', b).value) || 1080, 128, 4320); f = parseInt($('#npF', b).value) || 30; }
      w -= w % 2; h -= h % 2; UI.close(); const name = $('#npName', b).value.trim() || 'My music video';
      UI.loadProject(PX.newProject(name, w, h, f, pick[0])); PX.setDirty(true); if (pick[0] === 'teaser') PX.P.targetDur = 15; if (after) after(pick[0]); else if (['lyr169', 'lyr916', 'cover', 'vis'].includes(pick[0])) UI.songWizard(pick[0]);
    };
  });
};
UI.loadProject = (p) => {
  for (const id in PX.vids) { PX.vids[id].pause(); PX.vids[id].removeAttribute('src'); } PX.vids = {}; PX.imgs = {};
  PX.P = PX.migrate(p); PX.hist = { undo: [], redo: [], max: 200 }; UI.sel.clear(); PX.play.t = 0; $('#projName').value = PX.P.name;
  for (const m of PX.P.media) { m._err = false; if (m.kind === 'image') PX.imgFor(m); if (!m.missing) PX.makeThumbs(m); if (m.audioUrl) PX.loadAudio(m); }
  $('#aspectSel').value = PX.P.settings.w + 'x' + PX.P.settings.h; UI.sizeMonitor(); PX.setDirty(false); PX.changed('load'); UI.fitZoom(); PX.seek(0);
};

// ---------------- media bin ----------------
UI.renderBin = () => {
  const bin = $('#bin'); bin.innerHTML = ''; if (!PX.P.media.length) { bin.appendChild(el('div', 'empty small', 'No media yet. Press IMPORT, drop files here, or bring in MY SONG.')); return; }
  for (const m of PX.P.media) {
    const d = el('div', 'mi' + (m.missing ? ' missing' : '')); d.draggable = !m.missing; d.title = (m.path || m.name) + (m.missing ? '\nMISSING: right-click to find it' : '\nDrag onto the timeline, or double-click to add it at the playhead');
    const th = el('div', 'th'); const tu = PX.thumbs[m.id]; if (tu) th.style.backgroundImage = 'url("' + tu + '")'; else th.textContent = m.kind === 'audio' ? 'AUDIO' : m.kind.toUpperCase();
    if (m.kind === 'audio' && PX.peaks[m.id]) { const cv = el('canvas'); cv.width = 236; cv.height = 66; cv.style.width = '100%'; cv.style.height = '100%'; UI.drawPeaks(cv, PX.peaks[m.id], 0, PX.peaks[m.id].length / 100, '#78f0c8'); th.textContent = ''; th.appendChild(cv); }
    d.appendChild(th); d.appendChild(el('div', 'badge', m.missing ? 'MISSING' : m.kind === 'image' && m.alpha ? 'PNG' : m.kind.toUpperCase()));
    d.appendChild(el('div', 'nm', esc(m.name)));
    const info = []; if (m.dur && m.kind !== 'image') info.push(PX.fmtT(m.dur)); if (m.w) info.push(m.w + 'x' + m.h); if (m.vcodec) info.push(m.vcodec); if (m.kind !== 'image' && m.hasAudio === false) info.push('no audio'); if (m.size) info.push((m.size / 1048576).toFixed(1) + ' MB');
    d.appendChild(el('div', 'mt', esc(info.join('  ')) || '&nbsp;'));
    d.ondragstart = (e) => { e.dataTransfer.setData('text/px-media', m.id); e.dataTransfer.effectAllowed = 'copy'; };
    d.ondblclick = () => UI.addMediaAt(m, null, PX.play.t);
    d.oncontextmenu = (e) => { e.preventDefault(); UI.ctx(e.clientX, e.clientY, [['Add at the playhead', () => UI.addMediaAt(m, null, PX.play.t)], ['Find the file (relink)...', () => PX.send({ t: 'relink', id: m.id })], '-', ['Remove from the project', () => UI.removeMedia(m)]]); };
    bin.appendChild(d);
  }
};
UI.removeMedia = (m) => { const used = PX.P.clips.filter(c => c.media === m.id).length; UI.confirm('Remove "' + m.name + '" from this project' + (used ? ' and its ' + used + ' clip(s) on the timeline' : '') + '? The file on your PC is not deleted.', () => { PX.begin(); PX.P.clips = PX.P.clips.filter(c => c.media !== m.id); PX.P.media = PX.P.media.filter(x => x !== m); PX.commit('remove media'); }, 'REMOVE'); };
UI.drawPeaks = (cv, pk, from, to, col) => { const g = cv.getContext('2d'), w = cv.width, h = cv.height; g.clearRect(0, 0, w, h); g.fillStyle = col; const s0 = Math.floor(from * 100), s1 = Math.max(s0 + 1, Math.floor(to * 100)); for (let x = 0; x < w; x++) { const a = Math.floor(s0 + (s1 - s0) * x / w), b = Math.floor(s0 + (s1 - s0) * (x + 1) / w); let mx = 0; for (let k = a; k <= b && k < pk.length; k++) if (k >= 0) mx = Math.max(mx, pk[k]); const hh = Math.max(1, mx * h * 0.9); g.fillRect(x, (h - hh) / 2, 1, hh); } };
// a clip from a media item: on the given track (or the first free one of the right kind) at time t
UI.addMediaAt = (m, trackId, t) => {
  if (m.missing) return PX.toast('That file is missing: right-click it and press Find the file.', true);
  const kind = m.kind === 'audio' ? 'audio' : 'video'; let tr = trackId ? PX.trackById(trackId) : null; if (tr && (tr.kind !== kind && !(tr.kind === 'text' && m.kind === 'image'))) tr = null;
  const dur = m.kind === 'image' ? 5 : Math.max(0.1, m.dur || 5);
  if (!tr) { const cands = PX.P.tracks.filter(x => x.kind === kind && !x.locked); tr = cands.find(x => !PX.clipsOn(x.id).some(c => c.start < t + dur && c.start + c.dur > t)) || cands[cands.length - 1]; }
  if (!tr) { PX.toast('Add a ' + kind + ' track first.', true); return; } if (tr.locked) return PX.toast('That track is locked.', true);
  PX.begin(); const c = { id: PX.uid('c'), track: tr.id, media: m.id, type: m.kind === 'audio' ? 'audio' : m.kind, start: Math.max(0, t), dur, in: 0, speed: 1, vol: 1, fadeIn: 0, fadeOut: 0, opacity: 1, scale: 1, x: 0, y: 0, rot: 0, crop: { l: 0, t: 0, r: 0, b: 0 }, fit: m.kind === 'image' && m.alpha ? 'fit' : 'fit', fx: PX.defaultFx() };
  if (m.kind === 'image' && !m.alpha) c.ken = { zoom: 0.08, pan: 0 }; PX.P.clips.push(c); UI.sel = new Set([c.id]); PX.commit('add clip'); return c;
};

// ---------------- timeline ----------------
UI.tToX = (t) => t * UI.pps; UI.xToT = (x) => x / UI.pps;
UI.fitZoom = () => { const w = $('#tlscroll').clientWidth - 40, d = Math.max(10, PX.projectDur() + 2); UI.pps = PX.clamp(w / d, 2, 400); $('#zoom').value = Math.round(Math.log(UI.pps / 2) / Math.log(200) * 100); UI.renderTimeline(); };
UI.renderTimeline = () => {
  const heads = $('#heads'), lanes = $('#tracks'); heads.innerHTML = ''; lanes.innerHTML = ''; const dur = Math.max(PX.projectDur() + 30, $('#tlscroll').clientWidth / UI.pps); const W = UI.tToX(dur);
  lanes.style.width = W + 'px';
  for (const tr of PX.P.tracks) {
    const h = el('div', 'th2'); h.style.height = tr.h + 'px'; const kc = tr.kind === 'video' ? 'var(--clipV)' : tr.kind === 'audio' ? 'var(--clipA)' : 'var(--clipT)';
    h.innerHTML = '<div class="tn"><span class="kind" style="background:' + kc + '">' + tr.kind.toUpperCase() + '</span><input value="' + esc(tr.name) + '" title="Click to rename the track"></div><div class="tb"></div><div class="resize" title="Drag to change the track height"></div>';
    const inp = $('input', h); inp.onchange = () => { PX.begin(); tr.name = inp.value.trim() || tr.name; PX.commit('rename track'); };
    const tb = $('.tb', h); const tbtn = (txt, on, tip, fn) => { const b = el('button', on ? 'on' : '', txt); b.title = tip; b.onclick = () => { PX.begin(); fn(); PX.commit('track'); }; tb.appendChild(b); };
    if (tr.kind === 'audio') { tbtn('M', tr.mute, 'Mute this track', () => tr.mute = !tr.mute); tbtn('S', tr.solo, 'Solo: hear only the solo tracks', () => tr.solo = !tr.solo); }
    else tbtn(tr.hidden ? 'HIDDEN' : 'SHOWN', !tr.hidden, 'Show or hide this track in the video', () => tr.hidden = !tr.hidden);
    tbtn(tr.locked ? 'LOCKED' : 'LOCK', tr.locked, 'Lock the track so its clips cannot be changed', () => tr.locked = !tr.locked);
    const db = el('button', '', 'X'); db.title = 'Delete this track'; db.onclick = () => { const n = PX.clipsOn(tr.id).length; const go = () => { PX.begin(); PX.P.clips = PX.P.clips.filter(c => c.track !== tr.id); PX.P.tracks = PX.P.tracks.filter(x => x !== tr); PX.commit('delete track'); }; n ? UI.confirm('Delete the track "' + tr.name + '" and its ' + n + ' clip(s)?', go, 'DELETE') : go(); }; tb.appendChild(db);
    $('.resize', h).onmousedown = (e) => { e.preventDefault(); const y0 = e.clientY, h0 = tr.h; PX.begin(); const mv = (ev) => { tr.h = PX.clamp(h0 + ev.clientY - y0, 34, 180); UI.renderTimeline(); }; const up = () => { removeEventListener('mousemove', mv); removeEventListener('mouseup', up); PX.commit('track height'); }; addEventListener('mousemove', mv); addEventListener('mouseup', up); };
    heads.appendChild(h);
    const lane = el('div', 'lane' + (tr.locked ? ' locked' : '')); lane.style.height = tr.h + 'px'; lane.dataset.track = tr.id;
    lane.ondragover = (e) => { if (e.dataTransfer.types.includes('text/px-media')) { e.preventDefault(); e.dataTransfer.dropEffect = 'copy'; } };
    lane.ondrop = (e) => { const id = e.dataTransfer.getData('text/px-media'); if (!id) return; e.preventDefault(); const m = PX.mediaById(id); const r = lanes.getBoundingClientRect(); let t = UI.xToT(e.clientX - r.left); t = UI.snapT(t, null, []); UI.addMediaAt(m, tr.id, Math.max(0, t)); };
    lane.onmousedown = (e) => { if (e.target === lane) { if (!e.shiftKey) UI.sel.clear(); UI.renderSel(); const r = lanes.getBoundingClientRect(); PX.stopPlay(false); PX.seek(UI.xToT(e.clientX - r.left)); } };
    for (const c of PX.clipsOn(tr.id)) lane.appendChild(UI.clipEl(c, tr));
    lanes.appendChild(lane);
  }
  for (const mk of PX.P.markers) { const d = el('div', 'marker'); d.style.left = UI.tToX(mk) + 'px'; d.style.height = lanes.scrollHeight + 'px'; lanes.appendChild(d); }
  UI.drawRuler(W); UI.placePlayhead(PX.play.t); $('#dur').textContent = ' / ' + PX.fmtTC(PX.projectDur(), PX.P.settings.fps);
  $('#heads').scrollTop = $('#tlscroll').scrollTop;
};
UI.clipName = (c) => c.type === 'text' ? (c.text && c.text.content || 'Text').split('\n')[0] : c.type === 'visual' ? 'Visualiser: ' + (PX.VISUALS.find(v => v[0] === c.vis.style) || ['', ''])[1] : (PX.mediaById(c.media) || { name: '?' }).name;
UI.clipEl = (c, tr) => {
  const m = PX.mediaById(c.media); const cls = c.type === 'video' ? 'v' : c.type === 'audio' ? 'a' : c.type === 'text' ? 't' : c.type === 'visual' ? 'x' : 'i';
  const d = el('div', 'clip ' + cls + (UI.sel.has(c.id) ? ' sel' : '') + (m && m.missing ? ' missing' : '') + (tr.hidden || tr.mute ? ' dim' : '')); d.dataset.id = c.id;
  d.style.left = UI.tToX(c.start) + 'px'; d.style.width = Math.max(4, UI.tToX(c.dur)) + 'px';
  if ((c.type === 'video' || c.type === 'image') && m && PX.strips[m.id] && PX.strips[m.id].length) { const st = PX.strips[m.id], mid = st[Math.min(st.length - 1, Math.floor(st.length * PX.clamp(c.in / (m.dur || 1), 0, 0.99)))]; d.style.backgroundImage = 'url("' + (c.type === 'image' ? st[0].url : mid.url) + '")'; }
  d.appendChild(el('div', 'cl', esc(UI.clipName(c)) + (c.speed !== 1 ? '  ' + c.speed + 'x' : '')));
  if ((c.type === 'audio' || c.type === 'video') && m && PX.peaks[m.id] && tr.h >= 40) { const cv = el('canvas', 'wf'); const w = Math.min(4000, Math.max(4, Math.round(UI.tToX(c.dur)))); cv.width = w; cv.height = c.type === 'audio' ? tr.h - 22 : 18; cv.style.width = w + 'px'; UI.drawPeaks(cv, PX.peaks[m.id], c.in, c.in + c.dur * c.speed, c.type === 'audio' ? '#a8ffe0' : '#ffffffa0'); d.appendChild(cv); }
  if (c.transIn && c.transIn.type !== 'none') { const tw = el('div', 'tr'); tw.style.width = UI.tToX(c.transIn.dur / 2) + 'px'; tw.title = (PX.TRANSITIONS.find(x => x[0] === c.transIn.type) || ['', ''])[1]; d.appendChild(tw); }
  if (c.transOut && c.transOut.type !== 'none') { const tw = el('div', 'tro'); tw.style.width = UI.tToX(c.transOut.dur) + 'px'; d.appendChild(tw); }
  const hl = el('div', 'h l'), hr = el('div', 'h r'); hl.title = 'Drag to trim the start'; hr.title = 'Drag to trim the end'; d.appendChild(hl); d.appendChild(hr);
  d.onmousedown = (e) => UI.clipDown(e, c, e.target === hl ? 'l' : e.target === hr ? 'r' : 'move');
  d.oncontextmenu = (e) => { e.preventDefault(); if (!UI.sel.has(c.id)) { UI.sel = new Set([c.id]); UI.renderSel(); } UI.ctx(e.clientX, e.clientY, [['Split at the playhead (S)', UI.split], ['Duplicate (Ctrl+D)', UI.dup], ['Delete (Delete)', UI.del], '-', ['Remove the transition', () => { PX.begin(); c.transIn = null; c.transOut = null; PX.commit('remove transition'); }], ['Reset effects', () => { PX.begin(); c.fx = PX.defaultFx(); PX.commit('reset fx'); }], ['Reset size and position', () => { PX.begin(); Object.assign(c, { x: 0, y: 0, scale: 1, rot: 0, crop: { l: 0, t: 0, r: 0, b: 0 } }); PX.commit('reset transform'); }], '-', ['Move to the playhead', () => { PX.begin(); c.start = PX.play.t; PX.commit('move'); }]]); };
  return d;
};
UI.renderSel = () => { $$('.clip').forEach(d => d.classList.toggle('sel', UI.sel.has(d.dataset.id))); UI.renderInspector(); PX.redraw(); };
// snapping: to the playhead, markers and the edges of other clips (within 8 pixels)
UI.snapT = (t, dur, excl) => {
  if (!UI.snap) return t; const pts = [0, PX.play.t, ...PX.P.markers]; for (const o of PX.P.clips) if (!excl.includes(o.id)) pts.push(o.start, o.start + o.dur);
  let best = t, bd = 8 / UI.pps; for (const p of pts) { if (Math.abs(p - t) < bd) { bd = Math.abs(p - t); best = p; } if (dur != null && Math.abs(p - (t + dur)) < bd) { bd = Math.abs(p - (t + dur)); best = p - dur; } }
  UI.showSnap(best !== t ? (dur != null && Math.abs(best + dur - t - dur) > 0 ? best : best) : null); return best;
};
UI.showSnap = (t) => { let s = $('#snapline'); if (t == null) { if (s) s.remove(); return; } if (!s) { s = el('div'); s.id = 'snapline'; $('#tracks').appendChild(s); } s.style.left = UI.tToX(t) + 'px'; s.style.height = $('#tracks').scrollHeight + 'px'; };
UI.clipDown = (e, c, mode) => {
  if (e.button !== 0) return; e.stopPropagation(); const tr = PX.trackById(c.track); if (tr.locked) { PX.toast('That track is locked: press LOCKED to unlock it.'); return; }
  if (e.shiftKey || e.ctrlKey) { UI.sel.has(c.id) ? UI.sel.delete(c.id) : UI.sel.add(c.id); UI.renderSel(); return; }
  if (!UI.sel.has(c.id)) { UI.sel = new Set([c.id]); UI.renderSel(); }
  const x0 = e.clientX, y0 = e.clientY, group = PX.P.clips.filter(o => UI.sel.has(o.id)), orig = group.map(o => ({ o, start: o.start, dur: o.dur, in: o.in, track: o.track })); const m = PX.mediaById(c.media); let moved = false;
  const mv = (ev) => {
    const dt = UI.xToT(ev.clientX - x0); if (!moved && Math.abs(ev.clientX - x0) < 3 && Math.abs(ev.clientY - y0) < 3) return; if (!moved) { PX.begin(); moved = true; }
    if (mode === 'move') {
      let ns = Math.max(0, orig.find(q => q.o === c).start + dt); ns = UI.snapT(ns, c.dur, group.map(o => o.id)); const delta = ns - orig.find(q => q.o === c).start;
      for (const q of orig) q.o.start = Math.max(0, q.start + delta);
      if (group.length === 1) { const lane = document.elementFromPoint(ev.clientX, ev.clientY); const ln = lane && lane.closest('.lane'); if (ln) { const nt = PX.trackById(ln.dataset.track); const ok = nt && !nt.locked && ((c.type === 'audio') ? nt.kind === 'audio' : (c.type === 'video' || c.type === 'image') ? nt.kind === 'video' : nt.kind !== 'audio'); if (ok) c.track = nt.id; } }
    } else if (mode === 'l') {
      const q = orig.find(q => q.o === c); let ns = UI.snapT(q.start + dt, null, [c.id]); let d = ns - q.start; const minIn = c.type === 'image' || c.type === 'text' || c.type === 'visual' ? -1e9 : -q.in / c.speed; d = PX.clamp(d, minIn, q.dur - 0.1); c.start = q.start + d; c.dur = q.dur - d; if (c.type === 'video' || c.type === 'audio') c.in = q.in + d * c.speed;
    } else {
      const q = orig.find(q => q.o === c); let ne = UI.snapT(q.start + q.dur + dt, null, [c.id]); let nd = ne - q.start; const maxD = (c.type === 'video' || c.type === 'audio') && m && m.dur ? (m.dur - q.in) / c.speed : 1e9; c.dur = PX.clamp(nd, 0.1, maxD);
    }
    UI.renderTimeline(); PX.redraw(); UI.renderInspector();
  };
  const up = () => { removeEventListener('mousemove', mv); removeEventListener('mouseup', up); UI.showSnap(null); if (moved) PX.commit(mode === 'move' ? 'move clip' : 'trim clip'); };
  addEventListener('mousemove', mv); addEventListener('mouseup', up);
};
UI.drawRuler = (W) => {
  const r = $('#ruler'); r.innerHTML = ''; const cv = el('canvas'); const vis = Math.min(W, 32000); cv.width = vis; cv.height = 26; cv.style.width = vis + 'px'; r.style.width = W + 'px'; r.appendChild(cv); const g = cv.getContext('2d');
  g.fillStyle = '#9d93c2'; g.font = '10px Segoe UI'; const steps = [0.1, 0.5, 1, 2, 5, 10, 15, 30, 60, 120, 300]; let step = steps.find(s => s * UI.pps >= 70) || 600;
  for (let t = 0; UI.tToX(t) < vis; t += step) { const x = Math.round(UI.tToX(t)) + 0.5; g.fillRect(x, 14, 1, 12); g.fillText(PX.fmtT(t), x + 3, 11); for (let k = 1; k < 5; k++) g.fillRect(Math.round(UI.tToX(t + step * k / 5)) + 0.5, 21, 1, 5); }
  r.onmousedown = (e) => { const rr = r.getBoundingClientRect(); const go = (ev) => PX.seek(UI.xToT(ev.clientX - rr.left)); PX.stopPlay(false); go(e); const up = () => { removeEventListener('mousemove', go); removeEventListener('mouseup', up); }; addEventListener('mousemove', go); addEventListener('mouseup', up); };
};
UI.placePlayhead = (t) => { const p = $('#playhead'); p.style.left = UI.tToX(t) + 'px'; p.style.height = ($('#tracks').scrollHeight + 26) + 'px'; $('#tc').textContent = PX.fmtTC(t, PX.P.settings.fps); if (PX.play.on) { const sc = $('#tlscroll'), x = UI.tToX(t); if (x > sc.scrollLeft + sc.clientWidth - 40) sc.scrollLeft = x - 80; } };
PX.onTime = UI.placePlayhead;

// ---------------- editing operations ----------------
UI.selected = () => PX.P.clips.filter(c => UI.sel.has(c.id));
UI.split = () => {
  const t = PX.play.t; let targets = UI.selected().filter(c => t > c.start + 0.03 && t < c.start + c.dur - 0.03); if (!targets.length) targets = PX.P.clips.filter(c => t > c.start + 0.03 && t < c.start + c.dur - 0.03 && !(PX.trackById(c.track) || {}).locked);
  if (!targets.length) return PX.toast('Put the playhead over a clip to split it.'); PX.begin(); const ns = new Set();
  for (const c of targets) { const b = JSON.parse(JSON.stringify(c)); b.id = PX.uid('c'); const cut = t - c.start; b.start = t; b.dur = c.dur - cut; if (c.type === 'video' || c.type === 'audio') b.in = c.in + cut * c.speed; c.dur = cut; b.transIn = null; c.transOut = null; b.fadeIn = 0; c.fadeOut = 0; PX.P.clips.push(b); ns.add(b.id); }
  UI.sel = ns; PX.commit('split');
};
UI.del = () => { const s = UI.selected().filter(c => !(PX.trackById(c.track) || {}).locked); if (!s.length) return PX.toast('Select a clip first.'); PX.begin(); PX.P.clips = PX.P.clips.filter(c => !s.includes(c)); UI.sel.clear(); PX.commit('delete'); };
UI.dup = () => { const s = UI.selected(); if (!s.length) return PX.toast('Select a clip first.'); PX.begin(); const ns = new Set(); const end = Math.max(...s.map(c => c.start + c.dur)), st = Math.min(...s.map(c => c.start)); for (const c of s) { const b = JSON.parse(JSON.stringify(c)); b.id = PX.uid('c'); b.start = c.start + (end - st); PX.P.clips.push(b); ns.add(b.id); } UI.sel = ns; PX.commit('duplicate'); };
UI.addTrack = (kind) => { PX.begin(); const n = PX.P.tracks.filter(t => t.kind === kind).length + 1; const tr = { id: PX.uid('t'), kind, name: (kind === 'video' ? 'Video ' : kind === 'audio' ? 'Audio ' : 'Text ') + n, h: kind === 'text' ? 46 : 56, hidden: false, locked: false, mute: false, solo: false, vol: 1 }; if (kind === 'audio') PX.P.tracks.push(tr); else { const i = PX.P.tracks.findIndex(t => t.kind === kind); PX.P.tracks.splice(i < 0 ? 0 : i, 0, tr); } PX.commit('add track'); };
UI.marker = () => { PX.begin(); const t = PX.play.t; const i = PX.P.markers.findIndex(m => Math.abs(m - t) < 0.05); if (i >= 0) PX.P.markers.splice(i, 1); else PX.P.markers.push(t); PX.commit('marker'); };
UI.textTrack = () => PX.P.tracks.find(t => t.kind === 'text' && !t.locked) || (UI.addTrack('text'), PX.P.tracks.find(t => t.kind === 'text'));
UI.addText = (preset) => { const tr = UI.textTrack(); PX.begin(); const c = { id: PX.uid('c'), track: tr.id, type: 'text', start: PX.play.t, dur: preset.dur || 4, opacity: 1, scale: 1, x: preset.x || 0, y: preset.y || 0, rot: 0, crop: { l: 0, t: 0, r: 0, b: 0 }, fx: PX.defaultFx(), speed: 1, vol: 1, in: 0, fadeIn: 0, fadeOut: 0, text: Object.assign(PX.defaultText(), preset.text) }; PX.P.clips.push(c); UI.sel = new Set([c.id]); PX.commit('add text'); };
UI.addVisual = (style) => { const tr = PX.P.tracks.find(t => t.kind === 'video' && !t.locked); if (!tr) return PX.toast('Add a video track first.', true); PX.begin(); const d = Math.max(5, PX.projectDur() - PX.play.t); const c = { id: PX.uid('c'), track: tr.id, type: 'visual', start: PX.play.t, dur: d, opacity: 1, scale: 1, x: 0, y: style === 'bars' || style === 'wave' || style === 'minwave' ? 0.55 : 0, rot: 0, crop: { l: 0, t: 0, r: 0, b: 0 }, fx: PX.defaultFx(), speed: 1, vol: 1, in: 0, fadeIn: 0, fadeOut: 0, vis: Object.assign(PX.defaultVis(), { style }) }; PX.P.clips.push(c); UI.sel = new Set([c.id]); PX.commit('add visualiser'); if (!PX.P.clips.some(x => x.type === 'audio')) PX.toast('Visualisers move with the audio on the timeline: add a song (MY SONG or IMPORT).'); };

// ---------------- inspector ----------------
UI.renderInspector = () => {
  const box = $('#inspector'); box.innerHTML = ''; const s = UI.selected();
  if (!s.length) { box.appendChild(el('div', 'empty small', 'Select a clip to edit it.<br><br>Project: ' + PX.P.settings.w + ' x ' + PX.P.settings.h + ', ' + PX.P.settings.fps + ' fps')); UI.projSection(box); return; }
  if (s.length > 1) { box.appendChild(el('div', 'empty small', s.length + ' clips selected: drag one to move them all, or press DELETE.')); const al = el('div', 'btnrow'); const b1 = el('button', '', 'ALIGN STARTS'); b1.onclick = () => { PX.begin(); const st = Math.min(...s.map(c => c.start)); s.forEach(c => c.start = st); PX.commit('align'); }; const b2 = el('button', '', 'BUTT TOGETHER'); b2.onclick = () => { PX.begin(); const ss = s.slice().sort((a, b) => a.start - b.start); for (let i = 1; i < ss.length; i++) ss[i].start = ss[i - 1].start + ss[i - 1].dur; PX.commit('butt'); }; al.appendChild(b1); al.appendChild(b2); box.appendChild(al); return; }
  const c = s[0], m = PX.mediaById(c.media);
  const sec = (title, build, closed) => { const d = el('div', 'sec' + (closed ? ' closed' : '')); const h = el('h4', '', esc(title) + '<span class="dim">' + (closed ? '+' : '-') + '</span>'); h.onclick = () => { d.classList.toggle('closed'); }; d.appendChild(h); const b = el('div'); build(b); d.appendChild(b); box.appendChild(d); };
  // a slider + number: live while dragging, one undo step per change
  const num = (b, label, get, set, min, max, step, unit) => { const f = el('div', 'f'); f.innerHTML = '<label>' + esc(label) + '</label><input type="range"><input type="number">'; const r = $('input[type=range]', f), n = $('input[type=number]', f); r.min = n.min = min; r.max = n.max = max; r.step = n.step = step; r.value = n.value = +get().toFixed(3); r.onpointerdown = () => PX.begin(); r.oninput = () => { set(+r.value); n.value = r.value; UI.liveUpdate(); }; r.onchange = () => PX.commit(label); n.onchange = () => { PX.begin(); set(PX.clamp(+n.value, min, max)); r.value = n.value; PX.commit(label); }; if (unit) f.title = unit; b.appendChild(f); return f; };
  const sel = (b, label, opts, get, set) => { const f = el('div', 'f'); f.innerHTML = '<label>' + esc(label) + '</label><select></select>'; const s2 = $('select', f); for (const o of opts) { const op = el('option', '', esc(o[1])); op.value = o[0]; s2.appendChild(op); } s2.value = get(); s2.onchange = () => { PX.begin(); set(s2.value); PX.commit(label); }; b.appendChild(f); };
  const col = (b, label, get, set) => { const f = el('div', 'f'); f.innerHTML = '<label>' + esc(label) + '</label><input type="color">'; const i = $('input', f); i.value = get(); i.oninput = () => { if (!PX._before) PX.begin(); set(i.value); UI.liveUpdate(); }; i.onchange = () => PX.commit(label); b.appendChild(f); };
  const chk = (b, label, get, set) => { const f = el('div', 'f'); f.innerHTML = '<label>' + esc(label) + '</label><input type="checkbox" style="justify-self:start">'; const i = $('input', f); i.checked = get(); i.onchange = () => { PX.begin(); set(i.checked); PX.commit(label); }; b.appendChild(f); };
  box.appendChild(el('div', '', '<b>' + esc(UI.clipName(c)) + '</b>' + (m && m.missing ? ' <span style="color:var(--bad)">(file missing)</span>' : '')));
  sec('CLIP', b => {
    num(b, 'Start (s)', () => c.start, v => c.start = Math.max(0, v), 0, 36000, 0.01);
    num(b, 'Duration (s)', () => c.dur, v => c.dur = Math.max(0.1, v), 0.1, 36000, 0.01);
    if (c.type === 'video' || c.type === 'audio') { num(b, 'In point (s)', () => c.in, v => c.in = PX.clamp(v, 0, m && m.dur ? m.dur - 0.1 : 1e9), 0, m && m.dur ? m.dur : 36000, 0.01); num(b, 'Speed', () => c.speed, v => { const end = c.dur * c.speed; c.speed = PX.clamp(v, 0.25, 4); c.dur = end / c.speed; }, 0.25, 4, 0.05, 'Speed also changes the pitch of the clip audio'); }
  });
  if (c.type !== 'audio') sec('TRANSFORM', b => {
    num(b, 'Position X', () => c.x, v => c.x = v, -2, 2, 0.01); num(b, 'Position Y', () => c.y, v => c.y = v, -2, 2, 0.01); num(b, 'Scale', () => c.scale, v => c.scale = v, 0.05, 5, 0.01); num(b, 'Rotation', () => c.rot, v => c.rot = v, -180, 180, 0.5); num(b, 'Opacity', () => c.opacity, v => c.opacity = v, 0, 1, 0.01);
    if (c.type === 'video' || c.type === 'image') { sel(b, 'Fit', [['fit', 'Fit (whole picture)'], ['fill', 'Fill (crop to fill)'], ['stretch', 'Stretch']], () => c.fit, v => c.fit = v); num(b, 'Crop left %', () => c.crop.l, v => c.crop.l = v, 0, 90, 0.5); num(b, 'Crop right %', () => c.crop.r, v => c.crop.r = v, 0, 90, 0.5); num(b, 'Crop top %', () => c.crop.t, v => c.crop.t = v, 0, 90, 0.5); num(b, 'Crop bottom %', () => c.crop.b, v => c.crop.b = v, 0, 90, 0.5); }
    if (c.type === 'image') { chk(b, 'Slow zoom', () => !!c.ken, v => c.ken = v ? { zoom: 0.08, pan: 0 } : null); if (c.ken) num(b, 'Zoom amount', () => c.ken.zoom, v => c.ken.zoom = v, -0.4, 0.6, 0.01); }
    const rb = el('div', 'btnrow'); const r1 = el('button', '', 'RESET'); r1.onclick = () => { PX.begin(); Object.assign(c, { x: 0, y: 0, scale: 1, rot: 0, opacity: 1, crop: { l: 0, t: 0, r: 0, b: 0 } }); PX.commit('reset transform'); }; rb.appendChild(r1); b.appendChild(rb);
  });
  if (c.type === 'audio' || (c.type === 'video' && m && m.hasAudio !== false)) sec('AUDIO', b => {
    num(b, 'Volume (dB)', () => 20 * Math.log10(Math.max(1e-4, c.vol)), v => c.vol = v <= -59 ? 0 : Math.pow(10, v / 20), -60, 12, 0.5);
    num(b, 'Fade in (s)', () => c.fadeIn, v => c.fadeIn = v, 0, 20, 0.05); num(b, 'Fade out (s)', () => c.fadeOut, v => c.fadeOut = v, 0, 20, 0.05);
    if (c.type === 'video') chk(b, 'Mute clip audio', () => !!c.mute, v => c.mute = v);
    const rb = el('div', 'btnrow'); const nb = el('button', '', 'NORMALISE'); nb.title = 'Set the level so the loudest peak of the clip is at -1 dB'; nb.onclick = () => { const pk = PX.peaks[c.media]; if (!pk) return PX.toast('The audio is still being read.'); const a = Math.floor(c.in * 100), z = Math.floor((c.in + c.dur * c.speed) * 100); let mx = 0; for (let i = a; i < z && i < pk.length; i++) mx = Math.max(mx, pk[i]); if (mx < 1e-4) return PX.toast('That part is silent.'); PX.begin(); c.vol = Math.pow(10, -1 / 20) / mx; PX.commit('normalise'); PX.toast('Normalised: ' + (20 * Math.log10(c.vol)).toFixed(1) + ' dB.'); }; rb.appendChild(nb); b.appendChild(rb);
  });
  if (c.type === 'video' || c.type === 'image') sec('EFFECTS', b => {
    const fx = c.fx; const pr = el('select'); pr.innerHTML = '<option value="">Apply a look...</option>' + PX.PRESETS.map((p, i) => '<option value="' + i + '">' + esc(p.name) + '</option>').join(''); pr.onchange = () => { if (pr.value === '') return; PX.begin(); c.fx = Object.assign(PX.defaultFx(), PX.PRESETS[+pr.value].fx); PX.commit('look'); }; b.appendChild(pr);
    num(b, 'Exposure', () => fx.exposure, v => fx.exposure = v, -2, 2, 0.05); num(b, 'Brightness', () => fx.brightness, v => fx.brightness = v, -100, 100, 1); num(b, 'Contrast', () => fx.contrast, v => fx.contrast = v, -100, 100, 1); num(b, 'Saturation', () => fx.saturation, v => fx.saturation = v, -100, 100, 1);
    num(b, 'Temperature', () => fx.temperature, v => fx.temperature = v, -100, 100, 1); num(b, 'Tint', () => fx.tint, v => fx.tint = v, -100, 100, 1); num(b, 'Highlights', () => fx.highlights, v => fx.highlights = v, -100, 100, 1); num(b, 'Shadows', () => fx.shadows, v => fx.shadows = v, -100, 100, 1);
    num(b, 'Blur', () => fx.blur, v => fx.blur = v, 0, 20, 0.1); num(b, 'Sharpen', () => fx.sharpen, v => fx.sharpen = v, 0, 100, 1); num(b, 'Vignette', () => fx.vignette, v => fx.vignette = v, 0, 100, 1); num(b, 'Fade', () => fx.fade, v => fx.fade = v, 0, 100, 1); num(b, 'Sepia', () => fx.sepia, v => fx.sepia = v, 0, 100, 1); chk(b, 'Black and white', () => fx.bw, v => fx.bw = v);
    const rb = el('div', 'btnrow'); const r1 = el('button', '', 'RESET EFFECTS'); r1.onclick = () => { PX.begin(); c.fx = PX.defaultFx(); PX.commit('reset fx'); }; const cmp = el('button', '', 'HOLD TO COMPARE'); cmp.title = 'Hold to see the clip without effects'; cmp.onpointerdown = () => { UI._cmp = c.fx; c.fx = PX.defaultFx(); PX.redraw(); }; cmp.onpointerup = cmp.onpointerleave = () => { if (UI._cmp) { c.fx = UI._cmp; UI._cmp = null; PX.redraw(); } }; rb.appendChild(r1); rb.appendChild(cmp); b.appendChild(rb);
  }, false);
  if (c.type === 'video' || c.type === 'image' || c.type === 'visual') sec('TRANSITION', b => {
    const pb = PX.partnerBefore(c); b.appendChild(el('div', 'hint', pb ? 'Between "' + esc(UI.clipName(pb)) + '" and this clip.' : 'No clip right before this one on the track: the transition fades this clip in.'));
    sel(b, 'At the start', PX.TRANSITIONS, () => c.transIn ? c.transIn.type : 'none', v => c.transIn = v === 'none' ? null : { type: v, dur: c.transIn ? c.transIn.dur : 1 });
    if (c.transIn) num(b, 'Duration (s)', () => c.transIn.dur, v => c.transIn.dur = PX.clamp(v, 0.1, Math.min(5, c.dur)), 0.1, 5, 0.05);
    sel(b, 'At the end', [['none', 'No transition'], ['dissolve', 'Fade out'], ['fadeblack', 'Fade to black'], ['fadewhite', 'Fade to white'], ['zoom', 'Zoom out'], ['blur', 'Blur out']], () => c.transOut ? c.transOut.type : 'none', v => c.transOut = v === 'none' ? null : { type: v, dur: c.transOut ? c.transOut.dur : 1 });
    if (c.transOut) num(b, 'End duration (s)', () => c.transOut.dur, v => c.transOut.dur = PX.clamp(v, 0.1, Math.min(5, c.dur)), 0.1, 5, 0.05);
  });
  if (c.type === 'text') sec('TEXT', b => {
    const x = c.text; const ta = el('textarea'); ta.rows = 3; ta.value = x.content; ta.onfocus = () => PX.begin(); ta.oninput = () => { x.content = ta.value; UI.liveUpdate(); }; ta.onchange = () => PX.commit('text'); b.appendChild(ta);
    sel(b, 'Font', UI.fonts.map(f => [f, f]), () => x.font, v => x.font = v); num(b, 'Size', () => x.size, v => x.size = v, 8, 400, 1); sel(b, 'Weight', [['300', 'Light'], ['400', 'Regular'], ['600', 'Semibold'], ['700', 'Bold'], ['800', 'Extra bold'], ['900', 'Black']], () => String(x.weight), v => x.weight = +v);
    col(b, 'Colour', () => x.color, v => x.color = v); sel(b, 'Align', [['left', 'Left'], ['center', 'Centre'], ['right', 'Right']], () => x.align, v => x.align = v); num(b, 'Letter spacing', () => x.spacing, v => x.spacing = v, -10, 60, 0.5); chk(b, 'Capitals', () => !!x.upper, v => x.upper = v);
    num(b, 'Outline', () => x.outline, v => x.outline = v, 0, 20, 0.5); col(b, 'Outline colour', () => x.outlineColor, v => x.outlineColor = v); num(b, 'Shadow', () => x.shadow, v => x.shadow = v, 0, 60, 1); col(b, 'Shadow colour', () => x.shadowColor, v => x.shadowColor = v);
    sel(b, 'Background', [['none', 'None'], ['box', 'Box'], ['round', 'Rounded box'], ['pill', 'Pill']], () => x.bg, v => x.bg = v); col(b, 'Background colour', () => x.bgColor, v => x.bgColor = v);
    const an = [['none', 'None'], ['fade', 'Fade'], ['up', 'Slide up'], ['down', 'Slide down'], ['left', 'Slide in'], ['pop', 'Pop'], ['type', 'Typewriter'], ['blur', 'Blur']]; sel(b, 'Comes in', an, () => x.animIn, v => x.animIn = v); sel(b, 'Goes out', an, () => x.animOut, v => x.animOut = v); num(b, 'Animation (s)', () => x.animDur, v => x.animDur = v, 0, 3, 0.05);
    chk(b, 'Karaoke fill', () => !!x.karaoke, v => x.karaoke = v); if (x.karaoke) col(b, 'Fill colour', () => x.hl, v => x.hl = v);
  });
  if (c.type === 'visual') sec('VISUALISER', b => {
    const v = c.vis; sel(b, 'Style', PX.VISUALS, () => v.style, s => v.style = s); col(b, 'Colour', () => v.color, s => v.color = s); col(b, 'Second colour', () => v.color2, s => v.color2 = s);
    num(b, 'Motion', () => v.intensity, s => v.intensity = s, 0.1, 3, 0.05); num(b, 'Size', () => v.size, s => v.size = s, 0.2, 3, 0.05); num(b, 'Glow', () => v.glow, s => v.glow = s, 0, 3, 0.05); if (v.style === 'bars' || v.style === 'cspec') { num(b, 'Bars', () => v.bands, s => v.bands = Math.round(s), 8, 160, 1); chk(b, 'Mirror', () => v.mirror, s => v.mirror = s); }
    const srcs = [['mix', 'All the timeline audio']].concat(PX.P.clips.filter(o => o.type === 'audio' || o.type === 'video').map(o => [o.id, UI.clipName(o)])); sel(b, 'Listens to', srcs, () => v.source, s => v.source = s);
  });
  const rb = el('div', 'btnrow'); const d1 = el('button', 'danger', 'DELETE CLIP'); d1.onclick = UI.del; const d2 = el('button', '', 'DUPLICATE'); d2.onclick = UI.dup; rb.appendChild(d2); rb.appendChild(d1); box.appendChild(rb);
};
UI.projSection = (box) => { const d = el('div', 'sec'); d.innerHTML = '<h4>PROJECT</h4><div><div class="f"><label>Background</label><input type="color" id="bgCol"></div><p class="hint">Size and shape: the menu under the monitor. Export settings: EXPORT.</p></div>'; box.appendChild(d); const i = $('#bgCol', d); i.value = PX.P.settings.bg; i.oninput = () => { if (!PX._before) PX.begin(); PX.P.settings.bg = i.value; PX.redraw(); }; i.onchange = () => PX.commit('background'); };
UI.liveUpdate = () => { clearTimeout(UI._lu); UI._lu = setTimeout(() => UI.renderTimeline(), 60); PX.redraw(); };

// ---------------- monitor: size, transform handles, safe margins ----------------
UI.sizeMonitor = () => { const wrap = $('#monitorWrap'), cv = $('#monitor'); const W = PX.P.settings.w, H = PX.P.settings.h; const aw = wrap.clientWidth - 12, ah = wrap.clientHeight - 12; const k = Math.min(aw / W, ah / H); const dw = Math.max(32, Math.floor(W * k)), dh = Math.max(18, Math.floor(H * k)); const dpr = Math.min(2, window.devicePixelRatio || 1); cv.style.width = dw + 'px'; cv.style.height = dh + 'px'; cv.width = Math.round(dw * dpr); cv.height = Math.round(dh * dpr); PX.redraw(); };
PX.afterDraw = (g, W, H, t) => {
  $('#monEmpty').classList.toggle('hidden', PX.P.clips.length > 0);
  if ($('#safeChk').checked) { g.save(); g.strokeStyle = '#ffffff60'; g.setLineDash([6, 6]); g.strokeRect(W * 0.05, H * 0.05, W * 0.9, H * 0.9); g.strokeRect(W * 0.1, H * 0.1, W * 0.8, H * 0.8); g.restore(); }
  const s = UI.selected(); if (s.length !== 1 || PX.play.on) return; const c = s[0]; if (c.type === 'audio' || t < c.start || t >= c.start + c.dur) return;
  const b = UI.clipBox(c, W, H); if (!b) return; g.save(); g.translate(b.cx, b.cy); g.rotate(c.rot * Math.PI / 180); g.strokeStyle = '#3ce1ff'; g.lineWidth = 1.5; g.strokeRect(-b.w / 2, -b.h / 2, b.w, b.h); g.fillStyle = '#3ce1ff'; for (const [hx, hy] of [[-1, -1], [1, -1], [1, 1], [-1, 1]]) g.fillRect(hx * b.w / 2 - 5, hy * b.h / 2 - 5, 10, 10); g.beginPath(); g.arc(0, -b.h / 2 - 22, 6, 0, Math.PI * 2); g.fill(); g.restore();
};
// the box a clip covers on the canvas (for the handles)
UI.clipBox = (c, W, H) => {
  let w = W, h = H; const m = PX.mediaById(c.media);
  if (c.type === 'video' || c.type === 'image') { const sw = (m && m.w) || 16, sh = (m && m.h) || 9; const cw = sw * (1 - (c.crop.l + c.crop.r) / 100), ch = sh * (1 - (c.crop.t + c.crop.b) / 100), ar = cw / ch; if (c.fit === 'fill') { if (ar > W / H) { h = H; w = H * ar; } else { w = W; h = W / ar; } } else if (c.fit !== 'stretch') { if (ar > W / H) { w = W; h = W / ar; } else { h = H; w = H * ar; } } }
  else if (c.type === 'text') { const g = $('#monitor').getContext('2d'), x = c.text, k = W / 1920; g.font = x.weight + ' ' + (x.size * k) + 'px "' + x.font + '"'; const lines = x.content.split('\n'); w = Math.max(20, ...lines.map(l => g.measureText(x.upper ? l.toUpperCase() : l).width)) + 20 * k; h = x.size * k * 1.15 * lines.length + 10 * k; return { cx: W / 2 + c.x * W / 2, cy: H / 2 + c.y * H / 2, w: w * c.scale, h: h * c.scale }; }
  else if (c.type === 'visual') { const S = Math.min(W, H) * 0.32 * c.vis.size; w = S * 3; h = S * 2; }
  return { cx: W / 2 + c.x * W / 2, cy: H / 2 + c.y * H / 2, w: w * c.scale, h: h * c.scale };
};
UI.monitorMouse = () => {
  const cv = $('#monitor');
  cv.onmousedown = (e) => {
    const s = UI.selected(); if (s.length !== 1) return; const c = s[0]; if (c.type === 'audio' || (PX.trackById(c.track) || {}).locked) return; const r = cv.getBoundingClientRect(), k = cv.width / r.width, W = cv.width, H = cv.height; const px = (e.clientX - r.left) * k, py = (e.clientY - r.top) * k; const b = UI.clipBox(c, W, H); if (!b) return;
    const ang = -c.rot * Math.PI / 180, lx = (px - b.cx) * Math.cos(ang) - (py - b.cy) * Math.sin(ang), ly = (px - b.cx) * Math.sin(ang) + (py - b.cy) * Math.cos(ang);
    let mode = null; if (Math.hypot(lx, ly + b.h / 2 + 22) < 12) mode = 'rot'; else if (Math.abs(Math.abs(lx) - b.w / 2) < 12 && Math.abs(Math.abs(ly) - b.h / 2) < 12) mode = 'scale'; else if (Math.abs(lx) < b.w / 2 && Math.abs(ly) < b.h / 2) mode = 'move'; if (!mode) return;
    PX.begin(); const x0 = c.x, y0 = c.y, s0 = c.scale, r0 = c.rot, d0 = Math.hypot(px - b.cx, py - b.cy), a0 = Math.atan2(py - b.cy, px - b.cx);
    const mv = (ev) => { const qx = (ev.clientX - r.left) * k, qy = (ev.clientY - r.top) * k; if (mode === 'move') { c.x = x0 + (qx - px) / (W / 2); c.y = y0 + (qy - py) / (H / 2); if (ev.shiftKey) { if (Math.abs(c.x) < 0.03) c.x = 0; if (Math.abs(c.y) < 0.03) c.y = 0; } } else if (mode === 'scale') c.scale = PX.clamp(s0 * Math.hypot(qx - b.cx, qy - b.cy) / Math.max(1, d0), 0.05, 6); else { let a = r0 + (Math.atan2(qy - b.cy, qx - b.cx) - a0) * 180 / Math.PI; if (ev.shiftKey) a = Math.round(a / 15) * 15; c.rot = Math.round(a * 10) / 10; } PX.redraw(); };
    const up = () => { removeEventListener('mousemove', mv); removeEventListener('mouseup', up); PX.commit('transform'); }; addEventListener('mousemove', mv); addEventListener('mouseup', up);
  };
};

// ---------------- the left tabs ----------------
UI.TITLES = [
  { n: 'Song title', s: 'Big, centred', text: { content: 'SONG TITLE', size: 140, weight: 900, upper: true, spacing: 6, shadow: 24 } },
  { n: 'Artist name', s: 'Under the title', y: 0.28, text: { content: 'Artist Name', size: 64, weight: 600, spacing: 10, upper: true, color: '#d8c8ff' } },
  { n: 'Lower third', s: 'Name in a box', x: -0.55, y: 0.72, text: { content: 'Artist - Song', size: 48, weight: 700, bg: 'round', bgColor: '#7d3fe0', align: 'left', animIn: 'left' } },
  { n: 'Neon', s: 'Glowing outline', text: { content: 'NEON', size: 160, weight: 900, color: '#ffffff', shadow: 40, shadowColor: '#a85cff', outline: 3, outlineColor: '#ff50c4', animIn: 'pop' } },
  { n: 'Lyric caption', s: 'Bottom, readable', y: 0.75, dur: 3, text: { content: 'Your lyric line here', size: 58, weight: 700, bg: 'round', bgColor: '#00000099', animIn: 'up', animOut: 'fade' } },
  { n: 'Karaoke line', s: 'Fills as it is sung', y: 0.7, dur: 3, text: { content: 'Sing along with me', size: 70, weight: 800, karaoke: true, hl: '#ffd23c', shadow: 16 } },
  { n: 'Credits', s: 'Several lines', dur: 6, text: { content: 'Directed by\nYOUR NAME\n\nProduced by\nYOUR NAME', size: 52, weight: 600, lineH: 1.4, animIn: 'up' } },
  { n: 'Out now', s: 'Promo', dur: 3, text: { content: 'OUT NOW', size: 180, weight: 900, bg: 'box', bgColor: '#ff50c4', color: '#14061f', animIn: 'pop', upper: true } },
  { n: 'Typewriter', s: 'Types itself', text: { content: 'Coming soon...', size: 80, weight: 600, font: 'Consolas', animIn: 'type', animOut: 'fade', animDur: 1.5 } },
  { n: 'Social handle', s: 'Corner tag', x: 0.6, y: -0.82, text: { content: '@yourname', size: 44, weight: 700, bg: 'pill', bgColor: '#ffffff', color: '#14061f' } }];
UI.renderTabs = () => {
  const tl = $('#titleList'); tl.innerHTML = ''; for (const p of UI.TITLES) { const d = el('div', 'card', esc(p.n) + '<small>' + esc(p.s) + '</small>'); d.title = 'Add at the playhead'; d.onclick = () => UI.addText(p); tl.appendChild(d); }
  const tr = $('#transList'); tr.innerHTML = ''; for (const x of PX.TRANSITIONS.slice(1)) { const d = el('div', 'card', esc(x[1])); d.onclick = () => { const c = UI.selected()[0]; if (!c || !(c.type === 'video' || c.type === 'image' || c.type === 'visual')) return PX.toast('Select a video or picture clip first.'); PX.begin(); c.transIn = { type: x[0], dur: c.transIn ? c.transIn.dur : 1 }; PX.commit('transition'); PX.toast(x[1] + ' added at the start of the clip.'); }; tr.appendChild(d); }
  const pl = $('#presetList'); pl.innerHTML = ''; PX.PRESETS.forEach((p, i) => { const d = el('div', 'card', esc(p.name)); d.onclick = () => { const s = UI.selected().filter(c => c.type === 'video' || c.type === 'image'); if (!s.length) return PX.toast('Select a video or picture clip first.'); PX.begin(); s.forEach(c => c.fx = Object.assign(PX.defaultFx(), p.fx)); PX.commit('look'); }; pl.appendChild(d); });
  const vl = $('#visList'); vl.innerHTML = ''; for (const v of PX.VISUALS) { const d = el('div', 'card', esc(v[1])); d.onclick = () => UI.addVisual(v[0]); vl.appendChild(d); }
  UI.renderLyrics();
};
// lyric videos: paste lines, then tap SPACE (or the button) as each line starts while the song plays
UI.lyr = { on: false, i: 0, lines: [] };
UI.renderLyrics = () => {
  const b = $('#tab-lyrics'); b.innerHTML = '<p class="hint">1. Paste the lyrics (one line per line). 2. Press START TAPPING: the song plays from the playhead; press SPACE as each line starts. Each line becomes a caption you can edit. Purphex does not hear the words by itself (no speech recognition).</p><textarea id="lyrText" rows="10" placeholder="Paste lyrics here"></textarea><div class="btnrow"><button id="lyrTap" class="accent">START TAPPING</button><button id="lyrImp">IMPORT .SRT / .LRC</button><button id="lyrExp">EXPORT .SRT</button></div><div class="f"><label>Style</label><select id="lyrStyle"><option value="4">Lyric caption</option><option value="5">Karaoke line</option><option value="3">Neon</option></select></div><div id="lyrState" class="hint"></div>';
  const ta = $('#lyrText', b); ta.value = PX.P.lyrics.text || ''; ta.onchange = () => { PX.P.lyrics.text = ta.value; PX.setDirty(true); };
  $('#lyrTap', b).onclick = () => UI.lyr.on ? UI.lyrStop() : UI.lyrStart();
  $('#lyrImp', b).onclick = () => PX.send({ t: 'subOpen' });
  $('#lyrExp', b).onclick = () => { const cl = PX.P.clips.filter(c => c.type === 'text' && c.lyric).sort((a, z) => a.start - z.start); if (!cl.length) return PX.toast('No lyric captions yet.'); const ts = (t) => { const ms = Math.round(t * 1000); return String(Math.floor(ms / 3600000)).padStart(2, '0') + ':' + String(Math.floor(ms / 60000) % 60).padStart(2, '0') + ':' + String(Math.floor(ms / 1000) % 60).padStart(2, '0') + ',' + String(ms % 1000).padStart(3, '0'); }; PX.send({ t: 'subSave', name: PX.P.name, text: cl.map((c, i) => (i + 1) + '\n' + ts(c.start) + ' --> ' + ts(c.start + c.dur) + '\n' + c.text.content + '\n').join('\n') }); };
};
UI.lyrStart = () => { const lines = ($('#lyrText').value || '').split('\n').map(s => s.trim()).filter(Boolean); if (!lines.length) return PX.toast('Paste the lyrics first.'); PX.P.lyrics.text = $('#lyrText').value; UI.lyr = { on: true, i: 0, lines, made: [] }; PX.begin(); $('#lyrTap').textContent = 'STOP TAPPING'; $('#lyrState').textContent = 'Press SPACE when you hear: "' + lines[0] + '"'; if (!PX.play.on) PX.startPlay(); $('#monitor').focus(); };
UI.lyrTap = () => {
  const L = UI.lyr; if (!L.on) return; const t = PX.now(); const tr = UI.textTrack(); const pre = UI.TITLES[+$('#lyrStyle').value];
  if (L.made.length) { const prev = L.made[L.made.length - 1]; prev.dur = Math.max(0.3, t - prev.start - 0.05); }
  if (L.i >= L.lines.length) { UI.lyrStop(); return; }
  const c = { id: PX.uid('c'), track: tr.id, type: 'text', lyric: true, start: t, dur: 3, opacity: 1, scale: 1, x: pre.x || 0, y: pre.y != null ? pre.y : 0.7, rot: 0, crop: { l: 0, t: 0, r: 0, b: 0 }, fx: PX.defaultFx(), speed: 1, vol: 1, in: 0, fadeIn: 0, fadeOut: 0, text: Object.assign(PX.defaultText(), pre.text, { content: L.lines[L.i] }) };
  PX.P.clips.push(c); L.made.push(c); L.i++; $('#lyrState').textContent = L.i < L.lines.length ? 'Next: "' + L.lines[L.i] + '"' : 'Last line placed: press SPACE when it ends.'; UI.renderTimeline();
};
UI.lyrStop = () => { const L = UI.lyr; if (!L.on) return; L.on = false; $('#lyrTap').textContent = 'START TAPPING'; $('#lyrState').textContent = L.made.length + ' lines placed. Drag their edges on the timeline to fine-tune.'; PX.stopPlay(false); if (L.made.length) PX.commit('lyrics'); else PX.cancelBegin(); };
PX.on('subText', m => {
  const txt = m.text || '', tr = UI.textTrack(), pre = UI.TITLES[4]; const items = [];
  if (/-->/.test(txt)) { for (const blk of txt.replace(/\r/g, '').split(/\n\n+/)) { const ln = blk.split('\n'); const tl = ln.find(l => l.includes('-->')); if (!tl) continue; const [a, b] = tl.split('-->').map(s => { const q = s.trim().replace(',', '.').split(':'); return (+q[0]) * 3600 + (+q[1]) * 60 + parseFloat(q[2]); }); const body = ln.slice(ln.indexOf(tl) + 1).join('\n').replace(/<[^>]+>/g, '').trim(); if (body && isFinite(a) && isFinite(b)) items.push([a, b - a, body]); } }
  else { const rows = []; for (const l of txt.split('\n')) { const mm = l.match(/^\[(\d+):(\d+(?:\.\d+)?)\](.*)$/); if (mm) rows.push([(+mm[1]) * 60 + parseFloat(mm[2]), mm[3].trim()]); } rows.sort((a, b) => a[0] - b[0]); for (let i = 0; i < rows.length; i++) if (rows[i][1]) items.push([rows[i][0], Math.max(0.5, (i + 1 < rows.length ? rows[i + 1][0] : rows[i][0] + 3) - rows[i][0] - 0.05), rows[i][1]]); }
  if (!items.length) return PX.toast('No timed lines found in that file (supported: .srt and .lrc).', true);
  PX.begin(); for (const it of items) PX.P.clips.push({ id: PX.uid('c'), track: tr.id, type: 'text', lyric: true, start: it[0], dur: it[1], opacity: 1, scale: 1, x: 0, y: 0.75, rot: 0, crop: { l: 0, t: 0, r: 0, b: 0 }, fx: PX.defaultFx(), speed: 1, vol: 1, in: 0, fadeIn: 0, fadeOut: 0, text: Object.assign(PX.defaultText(), pre.text, { content: it[2] }) }); PX.commit('import lyrics'); PX.toast(items.length + ' lyric lines imported from ' + m.name + '.');
});

// ---------------- create a video from a song (the first-project workflow) ----------------
UI.songWizard = (kind) => {
  UI.modal('<h3>Make a video from a song</h3><p class="hint">Purphex puts it all on the timeline for you: the song, a background, a visualiser and the artist and title. Change anything afterwards.</p>' +
    '<div class="f"><label>Song</label><select id="wzSong"></select></div><div class="row"><button id="wzMy">USE MY PURPHEX SONG</button><button id="wzImp">IMPORT A SONG FILE...</button></div>' +
    '<div class="f"><label>Background</label><select id="wzBg"></select></div><div class="row"><button id="wzImpBg">IMPORT A PICTURE OR VIDEO...</button></div>' +
    '<div class="f"><label>Visualiser</label><select id="wzVis">' + ['<option value="">None</option>'].concat(PX.VISUALS.map(v => '<option value="' + v[0] + '">' + esc(v[1]) + '</option>')).join('') + '</select></div>' +
    '<div class="f"><label>Artist</label><input type="text" id="wzArtist"></div><div class="f"><label>Title</label><input type="text" id="wzTitle"></div>' +
    '<div class="f"><label>Colours</label><select id="wzCol"><option value="#a85cff|#3ce1ff">Purphex violet and cyan</option><option value="#ff50c4|#ffd23c">Pink and gold</option><option value="#ffffff|#9d93c2">White and grey</option><option value="#3cff9a|#3c8cff">Green and blue</option><option value="#ff5a3c|#ffb03c">Fire</option></select></div>' +
    '<div class="acts"><button id="mNo">CANCEL</button><button id="wzGo" class="accent">PUT IT ON THE TIMELINE</button></div>', b => {
      const fill = () => { const s = $('#wzSong', b), g = $('#wzBg', b); const sv = s.value, gv = g.value; s.innerHTML = PX.P.media.filter(m => m.kind === 'audio' || (m.kind === 'video' && m.hasAudio)).map(m => '<option value="' + m.id + '">' + esc(m.name) + '</option>').join('') || '<option value="">(add a song first)</option>'; g.innerHTML = '<option value="">Plain colour</option>' + PX.P.media.filter(m => m.kind === 'image' || m.kind === 'video').map(m => '<option value="' + m.id + '">' + esc(m.name) + '</option>').join(''); if (sv) s.value = sv; if (gv) g.value = gv; if (!s.value && s.options.length) s.selectedIndex = s.options.length - 1; };
      fill(); UI._wzFill = fill; $('#wzArtist', b).value = UI.artist || ''; $('#wzTitle', b).value = UI.songTitle || PX.P.name;
      $('#wzVis', b).value = kind === 'vis' ? 'cspec' : kind === 'cover' ? 'minwave' : kind && kind.startsWith('lyr') ? '' : 'bars';
      $('#wzMy', b).onclick = () => PX.send({ t: 'song' }); $('#wzImp', b).onclick = () => PX.send({ t: 'import', what: 'audio' }); $('#wzImpBg', b).onclick = () => PX.send({ t: 'import', what: 'visual' });
      $('#mNo', b).onclick = () => { UI._wzFill = null; UI.close(); };
      $('#wzGo', b).onclick = () => {
        const song = PX.mediaById($('#wzSong', b).value); if (!song) return PX.toast('Pick or add a song first.', true); const bg = PX.mediaById($('#wzBg', b).value); const [c1, c2] = $('#wzCol', b).value.split('|');
        UI._wzFill = null; UI.close(); PX.begin(); const D = song.dur || 180; const at = PX.P.tracks.find(t => t.kind === 'audio'), vt = PX.P.tracks.filter(t => t.kind === 'video'), tt = UI.textTrack();
        const base = { opacity: 1, scale: 1, x: 0, y: 0, rot: 0, crop: { l: 0, t: 0, r: 0, b: 0 }, fx: PX.defaultFx(), speed: 1, vol: 1, in: 0, fadeIn: 0, fadeOut: 0 };
        PX.P.clips.push(Object.assign({}, base, { id: PX.uid('c'), track: at.id, media: song.id, type: song.kind === 'audio' ? 'audio' : 'video', start: 0, dur: D, fadeOut: 2 }));
        if (bg) PX.P.clips.push(Object.assign({}, base, { id: PX.uid('c'), track: vt[vt.length - 1].id, media: bg.id, type: bg.kind, start: 0, dur: bg.kind === 'image' ? D : Math.min(D, bg.dur || D), fit: 'fill', ken: bg.kind === 'image' ? { zoom: 0.1, pan: 0 } : null, fx: Object.assign(PX.defaultFx(), { vignette: 35 }), transIn: { type: 'fadeblack', dur: 1.5 }, transOut: { type: 'fadeblack', dur: 2 } }));
        else PX.P.settings.bg = '#0a0518';
        const vs = $('#wzVis', b).value; if (vs) PX.P.clips.push(Object.assign({}, base, { id: PX.uid('c'), track: vt[0].id, type: 'visual', start: 0, dur: D, y: ['bars', 'wave', 'minwave'].includes(vs) ? 0.6 : 0, vis: Object.assign(PX.defaultVis(), { style: vs, color: c1, color2: c2 }) }));
        const ar = $('#wzArtist', b).value.trim(), ti = $('#wzTitle', b).value.trim();
        if (ti) PX.P.clips.push(Object.assign({}, base, { id: PX.uid('c'), track: tt.id, type: 'text', start: 0.5, dur: 6, y: vs && ['cspec', 'cwave', 'pulse'].includes(vs) ? 0 : -0.15, text: Object.assign(PX.defaultText(), UI.TITLES[0].text, { content: ti, color: '#ffffff', shadowColor: c1, animIn: 'pop' }) }));
        if (ar) PX.P.clips.push(Object.assign({}, base, { id: PX.uid('c'), track: tt.id, type: 'text', start: 0.8, dur: 5.7, y: vs && ['cspec', 'cwave', 'pulse'].includes(vs) ? 0.2 : 0.08, text: Object.assign(PX.defaultText(), UI.TITLES[1].text, { content: ar, color: c2 }) }));
        PX.commit('song video'); UI.fitZoom(); PX.seek(0); PX.toast('Done: press PLAY to watch it, then EXPORT.');
      };
    });
};

// ---------------- export ----------------
UI.exportDialog = () => {
  if (!PX.P.clips.length) return PX.toast('Put something on the timeline first.', true); const es = PX.P.exportSettings, S = PX.P.settings; const dur = PX.projectDur();
  UI.modal('<h3>Export</h3><div class="f"><label>Format</label><select id="exF"><option value="mp4-h264">MP4 (H.264 + AAC): works everywhere</option><option value="mp4-h265">MP4 (H.265 / HEVC + AAC): smaller files</option><option value="webm">WebM (VP9 + Opus)</option><option value="m4a">Audio only (M4A / AAC)</option><option value="wav">Audio only (WAV)</option></select></div>' +
    '<div class="f"><label>Resolution</label><select id="exR"><option value="project">Project size (' + S.w + ' x ' + S.h + ')</option><option value="720">720p</option><option value="1080">1080p</option><option value="2160">4K (2160p)</option></select></div>' +
    '<div class="f"><label>Frame rate</label><select id="exFps"><option value="0">Project (' + S.fps + ' fps)</option><option>24</option><option>25</option><option>30</option><option>60</option></select></div>' +
    '<div class="f"><label>Quality</label><select id="exQ"><option value="draft">Draft (fast, smaller)</option><option value="good">Good</option><option value="high">High</option><option value="max">Maximum</option></select></div>' +
    '<div class="f"><label>Audio bitrate</label><select id="exA"><option>128</option><option>192</option><option>256</option><option>320</option></select></div>' +
    '<div class="f"><label>Range</label><select id="exRange"><option value="all">Whole project (0:00 to ' + PX.fmtT(dur) + ')</option><option value="sel">From the playhead to the end of the selection / project</option><option value="io">Between the first two markers</option></select></div>' +
    '<p class="hint" id="exInfo"></p><div class="prog hidden" id="exProg"><div></div></div><p id="exStat" class="hint"></p><div class="acts"><button id="mNo">CLOSE</button><button id="exGo" class="accent">CHOOSE WHERE TO SAVE AND EXPORT</button></div>', b => {
      $('#exF', b).value = es.format; $('#exR', b).value = es.res; $('#exFps', b).value = String(es.fps || 0); $('#exQ', b).value = es.quality; $('#exA', b).value = String(es.abr);
      const info = () => { const sz = UI.exportSize($('#exR', b).value); const fmt = $('#exF', b).value; $('#exInfo', b).textContent = fmt === 'm4a' || fmt === 'wav' ? 'Audio only.' : 'Output: ' + sz.w + ' x ' + sz.h + (sz.w * sz.h > 8294400 ? ' (above 4K: may be slow)' : '') + '. Rendering draws every frame with all effects, transitions and text (a few minutes for a song).'; };
      $$('select', b).forEach(s => s.onchange = info); info();
      $('#mNo', b).onclick = () => { if (UI.exporting) UI.confirm('Stop the export? The unfinished file is deleted.', () => { UI.exportCancel = true; PX.send({ t: 'exportCancel' }); }, 'STOP EXPORT'); else UI.close(); };
      $('#exGo', b).onclick = () => {
        if (UI.exporting) return; es.format = $('#exF', b).value; es.res = $('#exR', b).value; es.fps = +$('#exFps', b).value; es.quality = $('#exQ', b).value; es.abr = +$('#exA', b).value; PX.setDirty(true);
        let t0 = 0, t1 = dur; const rg = $('#exRange', b).value; if (rg === 'sel') { t0 = PX.play.t; const s = UI.selected(); if (s.length) t1 = Math.max(...s.map(c => c.start + c.dur)); } else if (rg === 'io') { const mk = PX.P.markers.slice().sort((a, z) => a - z); if (mk.length < 2) return PX.toast('Put two markers on the timeline first (M).', true); t0 = mk[0]; t1 = mk[1]; }
        if (t1 - t0 < 0.1) return PX.toast('That range is empty.', true); const sz = UI.exportSize(es.res); UI.exportRun = { t0, t1, w: sz.w, h: sz.h, fps: es.fps || S.fps, fmt: es.format };
        PX.send({ t: 'exportStart', name: PX.P.name, fmt: es.format, w: sz.w, h: sz.h, fps: UI.exportRun.fps, quality: es.quality, abr: es.abr, dur: t1 - t0 });
      };
    });
};
UI.exportSize = (r) => { const S = PX.P.settings; if (r === 'project') return { w: S.w, h: S.h }; const target = +r; const k = target / Math.min(S.w, S.h); let w = Math.round(S.w * k), h = Math.round(S.h * k); w -= w % 2; h -= h % 2; return { w, h }; };
UI.setExportStat = (txt, pct) => { const st = $('#exStat'); if (st) st.textContent = txt; const p = $('#exProg'); if (p) { p.classList.remove('hidden'); $('div', p).style.width = (pct * 100).toFixed(1) + '%'; } };
PX.on('exportReady', async m => {
  const R = UI.exportRun; if (!R) return; UI.exporting = true; UI.exportCancel = false; PX.stopPlay(false); $('#exGo') && ($('#exGo').disabled = true);
  try {
    UI.setExportStat('Mixing the audio...', 0);
    const ab = await PX.renderAudio(R.t0, R.t1); const wav = PX.wav(ab); let r = await fetch('https://export.purphex/audio', { method: 'POST', body: wav }); if (!r.ok) throw new Error(await r.text());
    if (R.fmt !== 'm4a' && R.fmt !== 'wav') {
      const cv = document.createElement('canvas'); cv.width = R.w; cv.height = R.h; const g = cv.getContext('2d'); const n = Math.max(1, Math.round((R.t1 - R.t0) * R.fps)); const startT = performance.now();
      for (let i = 0; i < n; i++) {
        if (UI.exportCancel) throw new Error('cancelled'); const t = R.t0 + i / R.fps; await PX.seekExact(t); PX.drawFrame(g, t, R.w, R.h, true);
        const blob = await new Promise(res => cv.toBlob(res, 'image/jpeg', 0.94)); r = await fetch('https://export.purphex/frame', { method: 'POST', body: blob }); if (!r.ok) throw new Error(await r.text());
        if (i % 5 === 0) { const el2 = (performance.now() - startT) / 1000, left = el2 / (i + 1) * (n - i - 1); UI.setExportStat('Rendering frame ' + (i + 1) + ' of ' + n + '  (about ' + Math.ceil(left / 60) + ' min left)', (i + 1) / n * 0.97); }
      }
    }
    UI.setExportStat('Finishing the file...', 0.98); PX.send({ t: 'exportEnd' });
  } catch (e) { UI.exporting = false; if (e.message !== 'cancelled') { PX.send({ t: 'exportCancel' }); UI.setExportStat('Export failed: ' + e.message + '. Your project is safe.', 0); } $('#exGo') && ($('#exGo').disabled = false); }
});
PX.on('exportDone', m => { UI.exporting = false; UI.setExportStat('Exported: ' + m.path + (m.info ? '  (' + m.info + ')' : ''), 1); $('#exGo') && ($('#exGo').disabled = false); PX.toast('Export finished.'); });
PX.on('exportError', m => { UI.exporting = false; UI.setExportStat('Export failed: ' + m.msg + ' Your project is safe.', 0); $('#exGo') && ($('#exGo').disabled = false); PX.toast('Export failed: ' + m.msg, true); });
PX.on('exportCancelled', () => { UI.exporting = false; UI.setExportStat('Export stopped: the unfinished file was deleted.', 0); $('#exGo') && ($('#exGo').disabled = false); });

// ---------------- projects (open / save / rename / copy / delete) ----------------
UI.save = (copy) => { if (copy) return UI.prompt('Name of the copy', PX.P.name + ' copy', n => { const p = JSON.parse(JSON.stringify(PX.P)); p.id = PX.uid('p'); p.name = n; PX.P = p; $('#projName').value = n; PX.send({ t: 'save', project: PX.P }); }); PX.P.name = $('#projName').value.trim() || PX.P.name; PX.send({ t: 'save', project: PX.P }); };
PX.on('saved', m => { if (m.id === PX.P.id) { PX.setDirty(false); PX.toast('Saved "' + m.name + '".'); } });
PX.on('saveError', m => PX.toast('Could not save: ' + m.msg + ' (nothing was overwritten).', true));
UI.openDialog = () => PX.send({ t: 'list' });
PX.on('projects', m => {
  const list = m.list || []; if (UI._wl > 0) { UI._wl--; UI.fillWelcomeProjects(list); return; } UI.modal('<h3>Video projects</h3>' + (list.length ? '<div class="plist" id="pl"></div>' : '<p class="empty small">No saved video projects yet.</p>') + '<div class="acts"><button id="mNo">CLOSE</button><button id="mNew" class="accent">NEW PROJECT</button></div>', b => {
    const pl = $('#pl', b); for (const p of list) { const d = el('div', 'pitem', '<div class="pn">' + esc(p.name) + '</div><div class="pd">' + esc(p.modified) + (p.missing ? '  <span style="color:var(--bad)">' + p.missing + ' missing file(s)</span>' : '') + '</div>'); const bo = el('button', 'accent', 'OPEN'), br = el('button', '', 'RENAME'), bd = el('button', '', 'DUPLICATE'), bx = el('button', 'danger', 'DELETE');
      const openIt = () => { const go = () => { UI.close(); PX.send({ t: 'open', id: p.id }); }; PX.dirty ? UI.confirm('Open "' + p.name + '"? Your unsaved changes to "' + PX.P.name + '" will be lost (save first to keep them).', go, 'OPEN') : go(); };
      bo.onclick = openIt; d.ondblclick = openIt; br.onclick = () => UI.prompt('Rename the project', p.name, n => PX.send({ t: 'rename', id: p.id, name: n })); bd.onclick = () => PX.send({ t: 'duplicate', id: p.id }); bx.onclick = () => UI.confirm('Delete the project "' + p.name + '"? Your media files are not deleted.', () => PX.send({ t: 'delete', id: p.id }), 'DELETE');
      [bo, br, bd, bx].forEach(x => d.appendChild(x)); pl.appendChild(d); }
    $('#mNo', b).onclick = UI.close; $('#mNew', b).onclick = () => { UI.close(); UI.newProjectDialog(); };
  });
});
PX.on('project', m => { UI.loadProject(m.project); if (m.recovered) { PX.setDirty(true); PX.toast('Recovered your unsaved changes from last time. Press SAVE to keep them.'); } const miss = PX.P.media.filter(x => x.missing).length; if (miss) PX.toast(miss + ' media file(s) are missing (moved or deleted): right-click them in MEDIA to find them.', true); });
PX.on('renamed', m => { if (m.id === PX.P.id) { PX.P.name = m.name; $('#projName').value = m.name; } UI.openDialog(); });
PX.on('media', m => { PX.begin(); PX.addMedia(m.items); PX.commit('import'); if (UI._wzFill) UI._wzFill(); if (m.items.length) PX.toast('Imported ' + m.items.length + ' file(s).'); });
PX.on('relinked', m => { const md = PX.mediaById(m.id); if (!md) return; PX.begin(); Object.assign(md, m.item, { id: md.id, missing: false, _err: false }); PX.addMedia([md]); PX.commit('relink'); PX.toast('Found "' + md.name + '".'); });
PX.on('song', m => { if (m.artist) UI.artist = m.artist; if (m.title) UI.songTitle = m.title; });
PX.on('dawPlaying', () => { if (PX.play.on) PX.stopPlay(false); });

// ---------------- layout: resizable panels ----------------
UI.splitters = () => {
  for (const s of $$('.split')) s.onmousedown = (e) => {
    e.preventDefault(); const which = s.dataset.split, x0 = e.clientX, y0 = e.clientY; const L = $('#left'), R = $('#right'), T = $('#timeline'); const w0 = which === 'left' ? L.offsetWidth : R.offsetWidth, h0 = T.offsetHeight;
    const mv = (ev) => { if (which === 'left') L.style.width = PX.clamp(w0 + ev.clientX - x0, 220, innerWidth * 0.4) + 'px'; else if (which === 'right') R.style.width = PX.clamp(w0 - (ev.clientX - x0), 240, innerWidth * 0.4) + 'px'; else T.style.height = PX.clamp(h0 - (ev.clientY - y0), 150, innerHeight * 0.65) + 'px'; UI.sizeMonitor(); };
    const up = () => { removeEventListener('mousemove', mv); removeEventListener('mouseup', up); UI.renderTimeline(); try { localStorage.setItem('pxLayout', JSON.stringify({ l: L.style.width, r: R.style.width, t: T.style.height })); } catch (e2) { } };
    addEventListener('mousemove', mv); addEventListener('mouseup', up);
  };
  try { const l = JSON.parse(localStorage.getItem('pxLayout') || 'null'); if (l) { if (l.l) $('#left').style.width = l.l; if (l.r) $('#right').style.width = l.r; if (l.t) $('#timeline').style.height = l.t; } } catch (e) { }
};

// ---------------- keyboard ----------------
UI.keys = (e) => {
  const tag = (e.target.tagName || '').toLowerCase(); if (tag === 'input' || tag === 'textarea' || tag === 'select') { if (e.key === 'Escape') e.target.blur(); return; } if (!$('#modal').classList.contains('hidden')) { if (e.key === 'Escape' && !UI.exporting) UI.close(); return; }
  if (UI.tool !== 'video') return; const k = e.key.toLowerCase(), ctrl = e.ctrlKey || e.metaKey; const fps = PX.P.settings.fps;
  if (UI.lyr.on && (e.key === ' ' || e.key === 'Enter')) { e.preventDefault(); UI.lyrTap(); return; }
  if (e.key === ' ') { e.preventDefault(); PX.togglePlay(); }
  else if (ctrl && k === 'z') { e.preventDefault(); e.shiftKey ? PX.redo() : PX.undo(); } else if (ctrl && k === 'y') { e.preventDefault(); PX.redo(); }
  else if (ctrl && k === 's') { e.preventDefault(); UI.save(false); } else if (ctrl && k === 'o') { e.preventDefault(); UI.openDialog(); } else if (ctrl && k === 'n') { e.preventDefault(); UI.newProjectDialog(); } else if (ctrl && k === 'e') { e.preventDefault(); UI.exportDialog(); }
  else if (ctrl && k === 'd') { e.preventDefault(); UI.dup(); } else if (ctrl && k === 'a') { e.preventDefault(); UI.sel = new Set(PX.P.clips.map(c => c.id)); UI.renderSel(); }
  else if (k === 's') UI.split(); else if (e.key === 'Delete' || e.key === 'Backspace') UI.del(); else if (k === 'm') UI.marker();
  else if (e.key === ',' || e.key === 'ArrowLeft') { PX.stopPlay(false); PX.seek(PX.play.t - (e.shiftKey ? 1 : 1 / fps)); } else if (e.key === '.' || e.key === 'ArrowRight') { PX.stopPlay(false); PX.seek(PX.play.t + (e.shiftKey ? 1 : 1 / fps)); }
  else if (e.key === 'Home') { PX.stopPlay(false); PX.seek(0); } else if (e.key === 'End') { PX.stopPlay(false); PX.seek(PX.projectDur()); }
  else if (e.key === '+' || e.key === '=') UI.setZoom(UI.pps * 1.25); else if (e.key === '-') UI.setZoom(UI.pps / 1.25);
  else if (e.key === 'Escape') { UI.sel.clear(); UI.renderSel(); }
};
UI.setZoom = (pps) => { const sc = $('#tlscroll'), tc = (sc.scrollLeft + sc.clientWidth / 2) / UI.pps; UI.pps = PX.clamp(pps, 2, 400); $('#zoom').value = Math.round(Math.log(UI.pps / 2) / Math.log(200) * 100); UI.renderTimeline(); sc.scrollLeft = tc * UI.pps - sc.clientWidth / 2; };

// ---------------- start ----------------
UI.act = (a) => {
  const A = { new: () => { PX.dirty ? UI.confirm('Start a new project? Unsaved changes to "' + PX.P.name + '" will be lost (save first to keep them).', () => UI.newProjectDialog(), 'NEW PROJECT') : UI.newProjectDialog(); }, open: UI.openDialog, save: () => UI.save(false), saveCopy: () => UI.save(true), undo: PX.undo, redo: PX.redo, export: () => UI.tool === 'video' ? UI.exportDialog() : (PX.Cover && PX.Cover.exportDialog()), toDaw: () => { PX.stopPlay(false); PX.send({ t: 'toDaw' }); },
    toStart: () => { PX.stopPlay(false); PX.seek(0); }, frameBack: () => { PX.stopPlay(false); PX.seek(PX.play.t - 1 / PX.P.settings.fps); }, frameFwd: () => { PX.stopPlay(false); PX.seek(PX.play.t + 1 / PX.P.settings.fps); }, play: PX.togglePlay, stop: () => PX.stopPlay(true),
    split: UI.split, del: UI.del, dup: UI.dup, addV: () => UI.addTrack('video'), addA: () => UI.addTrack('audio'), addT: () => UI.addTrack('text'), marker: UI.marker, fit: UI.fitZoom };
  if (A[a]) A[a]();
};
// ---- WELCOME TO PURPHEX MUSIC VIDEO EDITOR: shown the first time the video editor opens ----
UI.startPreset = (id) => {
  const p = UI.PRESETS.find(x => x[0] === id); if (!p || id === 'custom') { UI.closeWelcome(); UI.newProjectDialog(); return; }
  const go = () => { UI.closeWelcome(); UI.loadProject(PX.newProject(p[1], p[2], p[3], p[4], id)); PX.setDirty(true); if (id === 'teaser') PX.P.targetDur = 15; if (['lyr169', 'lyr916', 'cover', 'vis'].includes(id)) UI.songWizard(id); };
  PX.dirty && PX.P.clips.length ? UI.confirm('Start a new "' + p[1] + '" project? Unsaved changes to "' + PX.P.name + '" will be lost (save first to keep them).', go, 'NEW PROJECT') : go();
};
UI.welcome = () => {
  UI._vw = true; let w = $('#vidWelcome'); if (w) { w.classList.remove('hidden'); UI._wl = (UI._wl || 0) + 1; PX.send({ t: 'list' }); return; }
  const cards = [['song', 'MAKE A VIDEO FROM MY SONG', 'Your song, a background, a visualiser and the title, put together for you'], ['lyr169', 'LYRIC VIDEO', 'Tap the words in time with the song'], ['vis', 'VISUALISER VIDEO', 'Bars and waves that move with your music'], ['cover', 'COVER ART VIDEO', 'Your artwork, moving, for YouTube and Spotify'], ['teaser', 'PROMO TEASER', 'A 15 second vertical teaser for TikTok and Reels'], ['mv916', 'VERTICAL VIDEO', 'For TikTok, Reels and Shorts'], ['mv169', 'MUSIC VIDEO', 'Edit your clips on a 16:9 timeline'], ['blank', 'BLANK PROJECT', 'Pick any size, start from nothing']];
  w = el('div', 'cvWelcome vidWelcome', '<div class="wtop"><div><h2>WELCOME TO <span>PURPHEX MUSIC</span> VIDEO EDITOR</h2><p class="hint">Make music videos, lyric videos, visualisers and promos for YouTube, Instagram and TikTok. Pick a starting point:</p></div><span class="grow"></span><button id="vwPhoto">PHOTO EDITOR</button><button id="vwGo" class="accent">START EDITING</button></div>' +
    '<div class="vwbody"><div class="vwcards" id="vwCards"></div><div class="vwside"><h4>YOUR PROJECTS</h4><div id="vwProj" class="vwproj"><p class="hint">Loading...</p></div><h4>EVERY SIZE</h4><div class="chips" id="vwSizes"></div><h4>HOW IT WORKS</h4><ol class="vwsteps"><li>IMPORT your clips, photos and music (or drop them in MEDIA).</li><li>Drag them onto the timeline. Trim the edges, press S to split.</li><li>Add TITLES, TRANSITIONS, EFFECTS, LYRICS or a VISUALISER.</li><li>EXPORT for YouTube, Instagram or TikTok.</li></ol></div></div>');
  w.id = 'vidWelcome'; document.body.appendChild(w);
  const cv = $('#vwCards', w); for (const c of cards) { const d = el('div', 'vwcard' + (c[0] === 'song' ? ' big' : ''), '<div class="vt">' + c[1] + '</div><div class="vd">' + esc(c[2]) + '</div>'); d.dataset.k = c[0]; d.onclick = () => { if (c[0] === 'song') { UI.closeWelcome(); UI.songWizard('mv'); } else if (c[0] === 'blank') { UI.closeWelcome(); UI.newProjectDialog(); } else UI.startPreset(c[0]); }; cv.appendChild(d); }
  const sz = $('#vwSizes', w); for (const p of UI.PRESETS) { const b = el('button', '', esc(p[1]) + (p[2] ? ' <small>' + p[2] + ' x ' + p[3] + '</small>' : '')); b.onclick = () => UI.startPreset(p[0]); sz.appendChild(b); }
  $('#vwGo', w).onclick = UI.closeWelcome; $('#vwPhoto', w).onclick = () => { UI.closeWelcome(); UI.setTool('cover'); };
  if (PX.P.clips.length) $('#vwGo', w).textContent = 'CONTINUE "' + PX.P.name.toUpperCase().slice(0, 28) + '"';
  UI._wl = (UI._wl || 0) + 1; PX.send({ t: 'list' });
};
UI.closeWelcome = () => { const w = $('#vidWelcome'); if (w) w.classList.add('hidden'); };
UI.fillWelcomeProjects = (list) => {
  const box = $('#vwProj'); if (!box) return; box.innerHTML = list.length ? '' : '<p class="hint">No saved projects yet: your first one appears here.</p>';
  for (const p of list.slice(0, 8)) { const d = el('div', 'pitem', '<div class="pn">' + esc(p.name) + '</div><div class="pd">' + esc(p.modified) + '</div>'); const b = el('button', 'accent', 'OPEN'); const open = () => { const go = () => { UI.closeWelcome(); PX.send({ t: 'open', id: p.id }); }; PX.dirty && PX.P.clips.length ? UI.confirm('Open "' + p.name + '"? Unsaved changes to "' + PX.P.name + '" will be lost.', go, 'OPEN') : go(); }; b.onclick = open; d.ondblclick = open; d.appendChild(b); box.appendChild(d); }
  if (list.length > 8) { const m = el('button', '', 'ALL ' + list.length + ' PROJECTS...'); m.onclick = () => { UI.closeWelcome(); UI.openDialog(); }; box.appendChild(m); }
};
UI.onboard = () => {
  if (PX.P.clips.length || UI._obShown) return; UI._obShown = true; const ob = el('div', 'onboard', '<div class="ob"><h3>Welcome to Video Studio</h3><ol><li>IMPORT your clips, photos and music (or drop them in MEDIA).</li><li>Drag them onto the timeline. Trim the edges, press S to split.</li><li>Add TITLES, TRANSITIONS, EFFECTS, LYRICS or a VISUALISER.</li><li>EXPORT a video for YouTube, Instagram or TikTok.</li></ol><div class="btnrow"><button id="obSong" class="accent">MAKE A VIDEO FROM MY SONG</button><button id="obNew">NEW PROJECT...</button><button id="obX">START EDITING</button></div></div>');
  $('#monitorWrap').appendChild(ob); $('#obSong', ob).onclick = () => { ob.remove(); UI.songWizard('mv'); }; $('#obNew', ob).onclick = () => { ob.remove(); UI.newProjectDialog(); }; $('#obX', ob).onclick = () => ob.remove(); PX.listeners.push(() => { if (PX.P.clips.length && ob.parentNode) ob.remove(); });
};
UI.init = () => {
  $$('[data-act]').forEach(b => b.onclick = () => UI.act(b.dataset.act));
  $$('#leftTabs button').forEach(b => b.onclick = () => { $$('#leftTabs button').forEach(x => x.classList.toggle('on', x === b)); $$('#left .tabbody').forEach(x => x.classList.toggle('hidden', x.id !== 'tab-' + b.dataset.tab)); });
  $('#toolVideo').onclick = () => UI.setTool('video'); $('#toolCover').onclick = () => UI.setTool('cover');
  $('#importBtn').onclick = () => PX.send({ t: 'import' }); $('#songBtn').onclick = () => PX.send({ t: 'song' });
  $('#projName').onchange = () => { PX.P.name = $('#projName').value.trim() || PX.P.name; PX.setDirty(true); };
  $('#zoom').oninput = () => UI.setZoom(2 * Math.pow(200, +$('#zoom').value / 100)); $('#snapChk').onchange = () => UI.snap = $('#snapChk').checked; $('#safeChk').onchange = PX.redraw;
  $('#aspectSel').onchange = () => { const [w, h] = $('#aspectSel').value.split('x').map(Number); PX.begin(); PX.P.settings.w = w; PX.P.settings.h = h; PX.commit('size'); UI.sizeMonitor(); };
  $('#tlscroll').addEventListener('scroll', () => { $('#heads').scrollTop = $('#tlscroll').scrollTop; });
  $('#tlscroll').addEventListener('wheel', (e) => { if (e.ctrlKey) { e.preventDefault(); UI.setZoom(UI.pps * (e.deltaY < 0 ? 1.2 : 1 / 1.2)); } }, { passive: false });
  // drag and drop files from Windows: the host is handed the real files (WebView2 gives their paths to Purphex only)
  const dz = $('#left'); dz.addEventListener('dragover', e => { if (e.dataTransfer.types.includes('Files')) { e.preventDefault(); $('#dropHint').classList.add('over'); } }); dz.addEventListener('dragleave', () => $('#dropHint').classList.remove('over'));
  document.addEventListener('dragover', e => { if (e.dataTransfer.types.includes('Files')) e.preventDefault(); });
  document.addEventListener('drop', e => { if (!e.dataTransfer.files.length) return; e.preventDefault(); $('#dropHint').classList.remove('over'); try { window.chrome.webview.postMessageWithAdditionalObjects('drop', e.dataTransfer.files); } catch (er) { PX.toast('Drop is not available here: use IMPORT.', true); } });
  addEventListener('keydown', UI.keys); addEventListener('resize', () => { UI.sizeMonitor(); UI.renderTimeline(); });
  new ResizeObserver(() => UI.sizeMonitor()).observe($('#monitorWrap'));
  UI.splitters(); UI.monitorMouse(); UI.renderTabs();
  PX.listeners.push((why) => { if (why === 'thumbs' || why === 'audio' || why === 'media') { UI.renderBin(); UI.renderTimeline(); return; } UI.renderBin(); UI.renderTimeline(); UI.renderInspector(); PX.redraw(); if (why === 'undo' || why === 'redo' || why === 'load') UI.renderLyrics(); });
  UI.loadProject(PX.P); PX.send({ t: 'ready' });
};
UI.setTool = (t) => { UI.tool = t; if (t !== 'video') UI.closeWelcome(); else if (!UI._vw && PX._inited) setTimeout(() => { if (!UI._vw) UI.welcome(); }, 0); $('#toolVideo').classList.toggle('on', t === 'video'); $('#toolCover').classList.toggle('on', t === 'cover'); $('#video').classList.toggle('hidden', t !== 'video'); $('#timeline').classList.toggle('hidden', t !== 'video'); $$('.split.h').forEach(s => s.classList.toggle('hidden', t !== 'video')); $('#cover').classList.toggle('hidden', t !== 'cover'); $('#projBar').classList.toggle('hidden', t !== 'video'); if (t === 'cover' && PX.Cover) PX.Cover.show(); else { PX.stopPlay(false); UI.sizeMonitor(); } PX.send({ t: 'tool', tool: t }); };
PX.on('init', m => { PX._inited = true;
  if (m.theme) { const r = document.documentElement.style; for (const k in m.theme) r.setProperty('--' + k, m.theme[k]); }
  if (m.fonts && m.fonts.length) UI.fonts = m.fonts; UI.artist = m.artist || ''; UI.songTitle = m.title || ''; UI.hasFfmpeg = m.ffmpeg;
  if (m.project) PX.emit('project', { project: m.project, recovered: m.recovered });
  if (m.tool) UI.setTool(m.tool); UI.renderTabs(); if ((m.tool || 'video') === 'video' && !UI._vw) UI.welcome();
});
PX.on('tool', m => UI.setTool(m.tool));
PX.on('theme', m => { const r = document.documentElement.style; for (const k in m.theme) r.setProperty('--' + k, m.theme[k]); });
PX.emit = (t, m) => (PX.handlers[t] || []).forEach(fn => fn(m));
window.addEventListener('DOMContentLoaded', UI.init);
