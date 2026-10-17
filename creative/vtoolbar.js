'use strict';
// =====================================================================
//  PURPHEX MUSIC VIDEO EDITOR: the top-toolbar layout. Under the top bar, a bar of categories (Media, Audio, Text, Captions,
//  Stickers, Effects, Filters, Transitions, Speed, Colour, Animation, Overlays, Templates, Settings, Edit); each opens its own
//  drop-down panel. No side panels: the preview gets the width and the timeline stays under it. Also stickers on video and
//  timeline templates that build real, editable compositions.
// =====================================================================
(() => {
const UI = PX.UI, $ = (s, r) => (r || document).querySelector(s), $$ = (s, r) => Array.from((r || document).querySelectorAll(s));
const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; };
const esc = (s) => String(s == null ? '' : s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const hint = (b, t) => b.appendChild(el('p', 'hint', esc(t)));
const btn = (b, t, fn, cls) => { const x = el('button', cls || '', t); x.onclick = fn; b.appendChild(x); return x; };
const row = (b) => b.appendChild(el('div', 'btnrow'));
const CATS = [['media', 'Media', '🎬'], ['audio', 'Audio', '🎵'], ['text', 'Text', '🔤'], ['captions', 'Captions', '💬'], ['stickers', 'Stickers', '⭐'], ['effects', 'Effects', '✨'], ['filters', 'Filters', '🎞️'], ['transitions', 'Transitions', '⇄'], ['speed', 'Speed', '⏩'], ['colour', 'Colour', '🎨'], ['animation', 'Animation', '🌀'], ['overlays', 'Overlays', '🌈'], ['templates', 'Templates', '🧩'], ['settings', 'Settings', '⚙️'], ['edit', 'Edit clip', '✎']];
let pop, body, park, open = null;
function build() {
  if ($('#vCats')) return; const app = $('#app'), top = $('#top'), video = $('#video');
  const cats = el('nav', 'cvcats vcats'); cats.id = 'vCats'; for (const [k, lab, ic] of CATS) { const b = el('button', '', '<span class="ic">' + ic + '</span>' + esc(lab)); b.dataset.k = k; b.title = lab; b.onclick = () => UI.openPanel(k); cats.appendChild(b); }
  top.after(cats);
  const left = $('#left'); left.querySelector('#leftTabs').style.display = 'none'; pop = el('div', 'vdock'); pop.id = 'vPop'; pop.innerHTML = '<div class="cvpophead"><b id="vPopT"></b></div><div class="vpopbody" id="vPopB"></div>'; left.appendChild(pop); body = $('#vPopB', pop);
  park = el('div', 'hidden'); park.id = 'vPark'; app.appendChild(park);
  for (const id of ['tab-media', 'tab-titles', 'tab-transitions', 'tab-effects', 'tab-lyrics', 'tab-visuals']) { const t = $('#' + id); if (t) park.appendChild(t); }
  park.appendChild($('#inspector'));
  $('#right').style.display = 'none'; const sr = $('.split.v[data-split="right"]'); if (sr) sr.style.display = 'none'; document.body.classList.add('vtb');
  UI.openPanel('media'); UI.sizeMonitor && UI.sizeMonitor();
}
// the inspector, showing only the sections a category is about
const insFilter = { audio: ['AUDIO'], speed: ['CLIP'], colour: ['EFFECTS'], animation: ['MOTION', 'TEXT'], effects: ['MOTION', 'GREEN SCREEN'], transitions: ['TRANSITION'], edit: null };
const origIns = UI.renderInspector; UI.renderInspector = () => { origIns(); applyFilter(); };
function applyFilter() { const ins = $('#inspector'); if (!ins) return; const f = open && open in insFilter ? insFilter[open] : null; $$('.sec', ins).forEach(s => { const h = ($('h4', s) || {}).textContent || ''; s.style.display = !f || f.some(k => h.toUpperCase().startsWith(k)) ? '' : 'none'; if (f && f.some(k => h.toUpperCase().startsWith(k))) s.classList.remove('closed'); }); }
UI.openPanel = (k) => {
  build(); open = k; $$('#vCats button').forEach(b => b.classList.toggle('on', b.dataset.k === k));
  for (const c of Array.from(body.children)) park.appendChild(c); body.innerHTML = '';
  if (!k) { k = open = 'media'; $$('#vCats button').forEach(b => b.classList.toggle('on', b.dataset.k === k)); } const cat = CATS.find(c => c[0] === k); $('#vPopT').textContent = cat[1].toUpperCase();
  (P[k] || (() => hint(body, 'Nothing here yet.')))(body); applyFilter();
};
const show = (b, id) => { const t = $('#' + id); if (t) { t.classList.remove('hidden'); b.appendChild(t); } };
const inspector = (b, msg) => { const sel = UI.selected(); if (!sel.length) hint(b, msg || 'Select a clip on the timeline first.'); b.appendChild($('#inspector')); UI.renderInspector(); };
const P = {
  media: (b) => show(b, 'tab-media'),
  audio: (b) => { hint(b, 'Bring in a song, a voice-over or sound effects; select an audio clip for its volume, fades and gain.'); const r = row(b); btn(r, 'MY SONG', () => PX.send({ t: 'song' })); btn(r, 'MY PROJECTS', () => UI.pickProject()); btn(r, '+ IMPORT AUDIO', () => PX.send({ t: 'import', what: 'audio' }), 'accent'); btn(r, '+ AUDIO TRACK', () => UI.addTrack('audio')); inspector(b, 'Select an audio clip (or a video clip with sound) for its volume and fades.'); },
  text: (b) => show(b, 'tab-titles'),
  captions: (b) => { hint(b, 'Captions and lyrics as real text clips: paste the words and tap them in time, or import / export an .srt or .lrc file.'); show(b, 'tab-lyrics'); },
  stickers: (b) => stickers(b),
  effects: (b) => { hint(b, 'Motion (zoom, spin, shake, pulse on the beat...), green screen and blur fill for the selected clip.'); inspector(b); },
  filters: (b) => show(b, 'tab-effects'),
  transitions: (b) => { show(b, 'tab-transitions'); inspector(b, 'Select a clip to set the length of its transition.'); },
  speed: (b) => { hint(b, 'Slow motion and fast motion. The clip gets longer or shorter so it plays the same part of the video.'); const r = row(b); for (const s of [0.25, 0.5, 0.75, 1, 1.5, 2, 4]) btn(r, s + 'x', () => setSpeed(s), s === 1 ? 'accent' : ''); inspector(b, 'Select a video or audio clip first.'); },
  colour: (b) => { hint(b, 'Brightness, contrast, saturation, temperature, tint, highlights, shadows and more for the selected clip (FILTERS has ready-made looks).'); inspector(b); },
  animation: (b) => { hint(b, 'How a clip or text comes in and moves: pick an animation and its strength; text also has its own in and out animations.'); inspector(b); },
  overlays: (b) => { hint(b, 'Visualisers that move with the music, and pictures or PNG graphics on top of your video (put them on a track above the main one).'); show(b, 'tab-visuals'); const r = row(b); btn(r, '+ IMPORT A PNG / PICTURE', () => PX.send({ t: 'import', what: 'visual' })); btn(r, '+ VIDEO TRACK (FOR OVERLAYS)', () => UI.addTrack('video')); },
  templates: (b) => templates(b),
  settings: (b) => settings(b),
  edit: (b) => inspector(b),
};
function setSpeed(s) { const sel = UI.selected().filter(c => c.type === 'video' || c.type === 'audio'); if (!sel.length) return PX.toast('Select a video or audio clip first.', true); PX.begin(); for (const c of sel) { const old = c.speed || 1; c.dur = Math.max(0.1, c.dur * old / s); c.speed = s; } PX.commit('speed'); PX.toast('Speed ' + s + 'x.'); }
// ---------------- stickers on video ----------------
// a sticker becomes a transparent PNG in MEDIA and is put on the top video track at the playhead (move, resize and time it like any clip)
UI.addOverlayCanvas = async (cv, name) => {
  const blob = await new Promise(r => cv.toBlob(r, 'image/png')); const tag = name + ' ' + Date.now().toString(36); const n0 = PX.P.media.length;
  const res = await fetch('https://export.purphex/art?what=video&name=' + encodeURIComponent(tag), { method: 'POST', body: blob }); if (!res.ok) return PX.toast('Could not add the sticker: ' + (await res.text()), true);
  const t0 = PX.play.t, end = Date.now() + 8000; const look = () => { const m = PX.P.media.find(q => q.name.indexOf(tag) === 0); if (m) { let tr = PX.P.tracks.find(t => t.kind === 'video' && !t.locked && !PX.clipsOn(t.id).some(c => c.start < t0 + 3 && c.start + c.dur > t0)); if (!tr) { UI.addTrack('video'); tr = PX.P.tracks.filter(t => t.kind === 'video').slice(-1)[0]; } UI.addMediaAt(m, tr.id, t0); const c = PX.P.clips[PX.P.clips.length - 1]; if (c && c.media === m.id) { PX.begin(); c.dur = 3; c.scale = 0.4; c.fit = 'fit'; PX.commit('sticker'); } return; } if (Date.now() < end) setTimeout(look, 150); }; look();
};
function stickers(b) {
  hint(b, 'Click a sticker: it goes on top of your video at the playhead for 3 seconds. Drag it on the preview, and time it on the timeline.');
  const C = PX.Cover; if (C && C.VST) { b.appendChild(el('div', 'hint', '<b>STICKER PACK</b>')); const g = el('div', 'grid2'); g.style.gridTemplateColumns = 'repeat(4,1fr)'; b.appendChild(g);
    for (const [kind, name, text] of C.VST) { const cv = el('canvas'); cv.width = 56; cv.height = 56; const x = cv.getContext('2d'); x.translate(28, 28); try { C.vsDraw(x, { id: 'v' + kind, kind, size: 48 / Math.max(1, C.ASPECT[kind] || 1), color: '#ff50c4', color2: '#ffffff', text }, 56, 56, 1); } catch (e) { } const c = el('div', 'card'); c.title = name; c.appendChild(cv);
      c.onclick = () => { const big = el('canvas'); const A = C.ASPECT[kind] || 1; big.width = 800; big.height = Math.round(800 * Math.max(A, 0.2)) + 40; const g2 = big.getContext('2d'); g2.translate(big.width / 2, big.height / 2); C.vsDraw(g2, { id: 'v' + kind + Date.now(), kind, size: 760, color: '#ff50c4', color2: '#ffffff', text }, big.width, big.height, 1); UI.addOverlayCanvas(big, name); }; g.appendChild(c); } }
  if (C && C.lib && C.lib.EMOJI) { b.appendChild(el('div', 'hint', '<b>EMOJI</b>')); const g = el('div', 'grid2'); g.style.gridTemplateColumns = 'repeat(7,1fr)'; b.appendChild(g);
    for (const e of C.lib.EMOJI) { const c = el('div', 'card', '<span style="font-size:22px;font-family:Segoe UI Emoji">' + e + '</span>'); c.onclick = () => { const big = el('canvas'); big.width = big.height = 512; const g2 = big.getContext('2d'); g2.font = '420px "Segoe UI Emoji","Segoe UI Symbol"'; g2.textAlign = 'center'; g2.textBaseline = 'middle'; g2.fillText(e, 256, 280); UI.addOverlayCanvas(big, 'Sticker'); }; g.appendChild(c); } }
}
// ---------------- templates: real timelines ----------------
const textClip = (track, start, dur, text, extra) => Object.assign({ id: PX.uid('c'), track, type: 'text', start, dur, opacity: 1, scale: 1, x: 0, y: 0, rot: 0, crop: { l: 0, t: 0, r: 0, b: 0 }, fx: PX.defaultFx(), speed: 1, vol: 1, in: 0, fadeIn: 0, fadeOut: 0, text: Object.assign(PX.defaultText(), text) }, extra || {});
const song = () => PX.P.clips.filter(c => c.type === 'audio').sort((a, b) => b.dur - a.dur)[0];
const VT = [
  ['slideshow', 'Photo slideshow cut to the beat', 'All the photos in MEDIA, one after another, every cut on the beat, with a title', () => { const imgs = PX.P.media.filter(m => m.kind === 'image' && !m.missing); if (!imgs.length) return PX.toast('Import some photos first (MEDIA).', true); const vt = PX.P.tracks.find(t => t.kind === 'video'); PX.begin(); let t = 0; for (const m of imgs) { UI.addMediaAt(m, vt.id, t); t += 3; } PX.commit('slideshow'); if (song()) PX.autoBeatCut(2, true); PX.begin(); PX.P.clips.filter(c => c.type === 'image').forEach((c, i) => c.motion = { type: i % 2 ? 'zoomout' : 'zoomin', amt: 1 }); PX.P.clips.push(textClip(UI.textTrack().id, 0, 3, { content: UI.songTitle || 'MY PHOTOS', size: 130, weight: 900, upper: true, shadow: 30, animIn: 'pop', animOut: 'fade' })); PX.commit('slideshow'); }],
  ['promo', 'Promo advert', 'NEW SINGLE / the title / OUT FRIDAY / LINK IN BIO, pulsing on the beat, over your first picture or video', () => { const tt = UI.textTrack().id, title = (UI.songTitle || 'SONG TITLE').toUpperCase(); PX.begin(); [['NEW SINGLE', 0, 'pop'], [title, 2, 'left'], ['OUT FRIDAY', 4, 'up'], ['LINK IN BIO', 6, 'pop']].forEach(([w, s, a]) => PX.P.clips.push(textClip(tt, s, 2, { content: w, size: 150, weight: 900, upper: true, shadow: 30, animIn: a, animOut: 'fade' }, { motion: { type: 'beatpulse', amt: 1 } }))); const m = PX.P.media.find(q => q.kind !== 'audio' && !q.missing); PX.commit('promo'); if (m) { UI.addMediaAt(m, PX.P.tracks.find(t => t.kind === 'video').id, 0); const c = PX.P.clips[PX.P.clips.length - 1]; PX.begin(); c.dur = 8; c.blurFill = true; c.motion = { type: 'kenburns', amt: 1 }; PX.commit('promo'); } if (song()) PX.findBeats(); }],
  ['intro', 'Intro card', 'Black, your name zooming in, the song title fading in', () => { const tt = UI.textTrack().id; PX.begin(); PX.P.clips.push(textClip(tt, 0, 4, { content: (UI.artist || 'ARTIST NAME').toUpperCase(), size: 160, weight: 900, spacing: 12, animIn: 'zoom', animOut: 'fade' }, { motion: { type: 'zoomin', amt: 1 } })); PX.P.clips.push(textClip(tt, 1, 3, { content: UI.songTitle || 'Song title', size: 64, weight: 500, spacing: 18, color: '#d8c8ff', animIn: 'fade', animOut: 'fade' }, { y: 0.3 })); PX.commit('intro'); }],
  ['outro', 'Outro card', 'Thanks for watching, subscribe, and your handle', () => { const tt = UI.textTrack().id, s = PX.projectDur(); PX.begin(); PX.P.clips.push(textClip(tt, s, 5, { content: 'THANKS FOR WATCHING', size: 110, weight: 900, animIn: 'up', animOut: 'fade' }, { y: -0.2 })); PX.P.clips.push(textClip(tt, s + 0.6, 4.4, { content: 'SUBSCRIBE', size: 90, weight: 900, bg: 'pill', bgColor: '#ff3c3c', color: '#ffffff', animIn: 'pop' }, { y: 0.2, motion: { type: 'beatpulse', amt: 1 } })); PX.P.clips.push(textClip(tt, s + 1, 4, { content: '@yourname', size: 50, weight: 700, color: '#d8c8ff', animIn: 'fade' }, { y: 0.55 })); PX.commit('outro'); }],
  ['montage', 'Gaming montage', 'Your clips cut on every beat, a flash on each beat, a glitchy title', () => { const vids = PX.P.media.filter(m => m.kind === 'video' && !m.missing); if (!vids.length) return PX.toast('Import some video clips first (MEDIA).', true); const vt = PX.P.tracks.find(t => t.kind === 'video'); let t = 0; for (const m of vids) { UI.addMediaAt(m, vt.id, t); t += 2; } if (song()) PX.autoBeatCut(1, true); PX.begin(); PX.P.clips.filter(c => c.type === 'video').forEach(c => c.motion = { type: 'beatflash', amt: 0.8 }); PX.P.clips.push(textClip(UI.textTrack().id, 0, 2.5, { content: 'MONTAGE', size: 170, weight: 900, italic: true, upper: true, outline: 4, outlineColor: '#00e5ff', animIn: 'pop', animOut: 'fade' }, { motion: { type: 'beatshake', amt: 1 } })); PX.commit('montage'); }],
  ['vertical', 'Vertical social clip', 'Makes the project 9:16 and puts a blurred copy behind every clip', () => { PX.begin(); PX.P.settings.w = 1080; PX.P.settings.h = 1920; PX.P.clips.forEach(c => { if (c.type === 'video' || c.type === 'image') { c.blurFill = true; c.fit = 'fit'; } }); PX.commit('vertical'); UI.sizeMonitor && UI.sizeMonitor(); }],
  ['musicvideo', 'Music video from my song', 'The song, a background, a visualiser and the title, put together for you', () => UI.songWizard('mv')],
  ['lyric', 'Lyric video', 'Your song and its words, tapped in time', () => UI.startPreset('lyr169')],
  ['visualiser', 'Visualiser video', 'Bars that move with your music over your artwork', () => UI.startPreset('vis')],
  ['artist', 'Artist promotion', 'Your name, photo and song with a lower third and a social handle', () => { const tt = UI.textTrack().id, d = Math.max(8, PX.projectDur()); PX.begin(); PX.P.clips.push(textClip(tt, 0.5, d - 1, { content: (UI.artist || 'Artist Name') + '  -  ' + (UI.songTitle || 'Song'), size: 48, weight: 700, bg: 'round', bgColor: '#7d3fe0', align: 'left', animIn: 'left' }, { x: -0.55, y: 0.72 })); PX.P.clips.push(textClip(tt, 0.5, d - 1, { content: '@yourname', size: 44, weight: 700, bg: 'pill', bgColor: '#ffffff', color: '#14061f' }, { x: 0.6, y: -0.82 })); PX.commit('artist'); const m = PX.P.media.find(q => q.kind === 'image' && !q.missing); if (m) { UI.addMediaAt(m, PX.P.tracks.find(t => t.kind === 'video').id, 0); const c = PX.P.clips[PX.P.clips.length - 1]; PX.begin(); c.dur = d; c.motion = { type: 'kenburns', amt: 1 }; c.blurFill = true; PX.commit('artist'); } }],
];
UI.VTEMPLATES = VT;
function templates(b) { hint(b, 'These build a real timeline from your media and song: change anything afterwards (clips, words, timing, effects).'); const g = el('div', 'grid2'); b.appendChild(g); for (const [id, n, d, fn] of VT) { const c = el('div', 'card', esc(n) + '<small>' + esc(d) + '</small>'); c.dataset.t = id; c.onclick = () => { UI.openPanel(null); fn(); }; g.appendChild(c); } }
// ---------------- settings ----------------
function settings(b) {
  const S = PX.P.settings, f = (label, html) => { const d = el('div', 'f', '<label>' + label + '</label>' + html); b.appendChild(d); return d; };
  const nm = f('Project name', '<input type="text">').querySelector('input'); nm.value = PX.P.name; nm.onchange = () => { PX.begin(); PX.P.name = nm.value.trim() || PX.P.name; PX.commit('name'); const pn = $('#projName'); if (pn) pn.value = PX.P.name; };
  const sz = f('Size and shape', '<select>' + UI.PRESETS.filter(p => p[2]).map(p => '<option value="' + p[2] + 'x' + p[3] + '">' + esc(p[1]) + ' (' + p[2] + ' x ' + p[3] + ')</option>').join('') + '<option value="custom">Your own size...</option></select>').querySelector('select'); sz.value = S.w + 'x' + S.h;
  const cw = f('Width x height', '<input type="number" min="16" max="7680" style="width:80px"> x <input type="number" min="16" max="4320" style="width:80px">'); const [wi, hi] = cw.querySelectorAll('input'); wi.value = S.w; hi.value = S.h;
  const apply = (w, h) => { PX.begin(); S.w = Math.round(w / 2) * 2; S.h = Math.round(h / 2) * 2; PX.commit('size'); UI.sizeMonitor && UI.sizeMonitor(); UI.openPanel('settings'); };
  sz.onchange = () => { if (sz.value !== 'custom') { const [w, h] = sz.value.split('x').map(Number); apply(w, h); } }; wi.onchange = hi.onchange = () => apply(PX.clamp(+wi.value, 16, 7680), PX.clamp(+hi.value, 16, 4320));
  const fp = f('Frame rate', '<select><option>24</option><option>25</option><option>30</option><option>50</option><option>60</option></select>').querySelector('select'); fp.value = String(S.fps || 30); fp.onchange = () => { PX.begin(); S.fps = +fp.value; PX.commit('fps'); };
  const bg = f('Background colour', '<input type="color">').querySelector('input'); bg.value = S.bg || '#000000'; bg.oninput = () => { S.bg = bg.value; PX.redraw(); }; bg.onchange = () => { PX.begin(); S.bg = bg.value; PX.commit('bg'); };
  hint(b, 'Changing the shape never stretches your footage: each clip keeps fit, fill or crop (set it per clip in EDIT CLIP, or BLUR FILL behind it in EFFECTS).');
  btn(row(b), 'EXPORT SETTINGS...', () => { UI.openPanel(null); UI.act('export'); }, 'accent');
}
// show the layout as soon as the editor starts
const prevSet = UI.setTool; UI.setTool = (t) => { prevSet(t); const vc = $("#vCats"); if (vc) vc.style.display = t === "video" ? "" : "none"; };
const prevInit = UI.init; UI.init = () => { prevInit(); build(); };
if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', () => setTimeout(build, 0)); else setTimeout(build, 0);
})();
