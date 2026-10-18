'use strict';
// =====================================================================
//  VIDEO TITLES THE iMOVIE WAY: type straight onto the video. A new title opens ready to type; double-click any title on
//  the preview (or on the timeline) to change its words; a title bar over the preview has the font, size, bold, italic,
//  alignment and colours of the picked title; the Text panel has a box to type in and ADD TEXT.
// =====================================================================
(() => {
const UI = PX.UI, clamp = PX.clamp, $ = (s, r) => (r || document).querySelector(s);
const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; };
const esc = (s) => String(s == null ? '' : s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const textSel = () => { const s = UI.selected(); return s.length === 1 && s[0].type === 'text' ? s[0] : null; };
const libFonts = () => (PX.Cover && PX.Cover.fontLib) ? Object.values(PX.Cover.fontLib).flat() : [];
const useFont = (f) => { if (PX.Cover && PX.Cover.loadFont && libFonts().includes(f)) PX.Cover.loadFont(f); if (document.fonts) document.fonts.load('700 40px "' + f + '"').then(() => PX.redraw()).catch(() => { }); };

// while a title is being typed, the box on the preview shows it (the canvas leaves it out so it is not drawn twice)
const prevText = PX.drawText; PX.drawText = (g, c, t, W, H) => { if (PX._editText === c.id && !UI.exporting) return; return prevText(g, c, t, W, H); };

// ---------------- typing on the preview ----------------
UI.editText = (c, selectAll) => {
  if (!c || c.type !== 'text') return; UI.endEditText(); PX.stopPlay(false);
  const ad = Math.min(c.text.animDur || 0.5, c.dur / 2); if (PX.play.t < c.start + ad || PX.play.t > c.start + c.dur - ad) PX.seek(c.start + Math.min(ad + 0.05, c.dur / 2));   // a moment where the title is fully in
  UI.sel = new Set([c.id]); UI.renderSel && UI.renderSel(); PX.begin(); PX._editText = c.id;
  const ta = el('textarea', 'vtxtedit'); ta.id = 'vTextEdit'; ta.spellcheck = false; ta.value = c.text.content; document.body.appendChild(ta);
  const place = () => { const cv = $('#monitor'); if (!cv) return; const r = cv.getBoundingClientRect(), W = cv.width, H = cv.height, s = r.width / W, x = c.text, k = W / 1920, size = x.size * k * (c.scale || 1) * s;
    ta.style.font = (x.italic ? 'italic ' : '') + x.weight + ' ' + size + 'px "' + x.font + '"'; ta.style.lineHeight = (x.lineH || 1.15); ta.style.letterSpacing = (x.spacing * k * s) + 'px'; ta.style.color = x.color; ta.style.textAlign = x.align || 'center'; ta.style.textTransform = x.upper ? 'uppercase' : 'none';
    ta.style.webkitTextStroke = x.outline > 0 ? (x.outline * k * s) + 'px ' + x.outlineColor : '0'; ta.style.textShadow = x.shadow > 0 ? '0 ' + (x.shadow * k * s * 0.25) + 'px ' + (x.shadow * k * s) + 'px ' + x.shadowColor : 'none';
    ta.style.background = x.bg && x.bg !== 'none' ? x.bgColor : 'rgba(0,0,0,0.15)';
    const lines = ta.value.split('\n'); const meas = document.createElement('canvas').getContext('2d'); meas.font = ta.style.font; const w = Math.max(size * 2, ...lines.map(l => meas.measureText(x.upper ? l.toUpperCase() : l).width + x.spacing * k * s * l.length)) + size * 0.8, h = size * (x.lineH || 1.15) * lines.length + size * 0.4;
    const cx = r.left + (W / 2 + c.x * W / 2) * s, cy = r.top + (H / 2 + c.y * H / 2) * s; ta.style.width = Math.min(r.width, w) + 'px'; ta.style.height = h + 'px'; ta.style.left = (cx - Math.min(r.width, w) / 2) + 'px'; ta.style.top = (cy - h / 2) + 'px'; ta.style.transform = 'rotate(' + (c.rot || 0) + 'deg)'; };
  place(); ta.oninput = () => { c.text.content = ta.value; place(); PX.redraw(); UI.syncTitleBar(); const pb = document.querySelector('#vPopB .vtextbox'); if (pb) pb.value = ta.value; };
  ta.onkeydown = (e) => { e.stopPropagation(); if (e.key === 'Escape' || (e.key === 'Enter' && (e.ctrlKey || e.metaKey))) { e.preventDefault(); UI.endEditText(); } };
  ta.onblur = () => setTimeout(() => { if (document.activeElement && document.activeElement.closest && document.activeElement.closest('#vTitleBar')) return; UI.endEditText(); }, 120);
  UI._place = place; addEventListener('resize', place); PX.redraw(); UI.syncTitleBar(); setTimeout(() => { ta.focus(); if (selectAll) ta.select(); else ta.setSelectionRange(ta.value.length, ta.value.length); }, 0);
};
UI.endEditText = () => { const ta = $('#vTextEdit'); if (!ta) return; const c = PX.clipById(PX._editText); if (c && !c.text.content.trim()) c.text.content = 'Title'; PX._editText = null; ta.remove(); removeEventListener('resize', UI._place); PX.commit('text'); PX.redraw(); UI.syncTitleBar(); };
// a new title opens ready to type, with its words picked
const prevAdd = UI.addText; UI.addText = (preset) => { prevAdd(preset); const c = textSel(); if (c) setTimeout(() => UI.editText(c, true), 30); return c; };
UI.addPlainText = () => UI.addText({ n: 'Text', text: { content: 'Your text', size: 110, weight: 800, animIn: 'fade', animOut: 'fade' } });
// double-click a title on the preview or on the timeline
function textAt(e) { const cv = $('#monitor'), r = cv.getBoundingClientRect(), k = cv.width / r.width, px = (e.clientX - r.left) * k, py = (e.clientY - r.top) * k, t = PX.play.t;
  const tracks = PX.P.tracks.filter(tr => tr.kind !== 'audio' && !tr.hidden); for (const tr of tracks) for (const c of PX.clipsOn(tr.id).slice().reverse()) { if (c.type !== 'text' || t < c.start || t >= c.start + c.dur) continue; const b = UI.clipBox(c, cv.width, cv.height); if (b && Math.abs(px - b.cx) < b.w / 2 + 8 && Math.abs(py - b.cy) < b.h / 2 + 8) return c; } return null; }
addEventListener('dblclick', (e) => { if (!UI || UI.tool !== 'video') return; if (e.target && e.target.id === 'monitor') { const c = textAt(e); if (c) { e.preventDefault(); UI.editText(c, true); } return; } const d = e.target.closest && e.target.closest('.clip'); if (d) { const c = PX.clipById(d.dataset.id); if (c && c.type === 'text') { e.preventDefault(); e.stopPropagation(); UI.editText(c, true); } } }, true);
// a single click on a title that is not picked picks it (so the title bar shows)
addEventListener('mousedown', (e) => { if (!UI || UI.tool !== 'video' || !e.target || e.target.id !== 'monitor' || e.button !== 0) return; const cur = textSel(); const c = textAt(e); if (c && (!cur || cur.id !== c.id) && UI.selected().length <= 1) { const any = UI.selected()[0]; if (!any || any.type === 'text' || !UI.clipBox(any, 1, 1)) { UI.sel = new Set([c.id]); UI.renderSel && UI.renderSel(); } } }, true);

// ---------------- the title bar over the preview ----------------
UI.syncTitleBar = () => {
  const wrap = $('#monitorWrap'); if (!wrap) return; let bar = $('#vTitleBar'); const c = textSel(); const show = c && !PX.play.on && UI.tool === 'video';
  if (!show) { if (bar) bar.classList.add('hidden'); return; } if (!bar) { bar = el('div', 'vtitlebar'); bar.id = 'vTitleBar'; wrap.appendChild(bar); }
  bar.classList.remove('hidden'); if (bar._id === c.id && bar._v === JSON.stringify(c.text)) return; bar._id = c.id; bar._v = JSON.stringify(c.text); const x = c.text;
  const fonts = Array.from(new Set([x.font].concat(UI.fonts || [], libFonts()))); bar.innerHTML = '';
  const add = (n) => { bar.appendChild(n); return n; }; const set = (fn) => { PX.begin(); fn(); PX.commit('title'); PX.redraw(); if (UI._place && $('#vTextEdit')) UI._place(); UI.syncTitleBar(); UI.renderInspector && UI.renderInspector(); };
  const ed = add(el('button', 'accent', '✎ TYPE')); ed.title = 'Type on the video (or double-click the title)'; ed.onclick = () => $('#vTextEdit') ? UI.endEditText() : UI.editText(c, true); if ($('#vTextEdit')) ed.textContent = '✓ DONE';
  const fs = add(el('select', 'vtbfont')); fs.title = 'Font'; for (const f of fonts) { const o = el('option', '', esc(f)); o.value = f; o.style.fontFamily = '"' + f + '"'; fs.appendChild(o); } fs.value = x.font; fs.onchange = () => { useFont(fs.value); set(() => x.font = fs.value); };
  const dn = add(el('button', '', 'A-')); dn.title = 'Smaller'; dn.onclick = () => set(() => x.size = clamp(Math.round(x.size / 1.12), 8, 400));
  const sz = add(el('input', 'vtbsize')); sz.type = 'number'; sz.min = 8; sz.max = 400; sz.value = Math.round(x.size); sz.title = 'Size'; sz.onchange = () => set(() => x.size = clamp(+sz.value || x.size, 8, 400));
  const up = add(el('button', '', 'A+')); up.title = 'Bigger'; up.onclick = () => set(() => x.size = clamp(Math.round(x.size * 1.12), 8, 400));
  const bo = add(el('button', (+x.weight >= 700 ? 'on' : ''), '<b>B</b>')); bo.title = 'Bold'; bo.onclick = () => set(() => x.weight = +x.weight >= 700 ? 400 : 800);
  const it = add(el('button', (x.italic ? 'on' : ''), '<i>I</i>')); it.title = 'Italic'; it.onclick = () => set(() => x.italic = !x.italic);
  const cap = add(el('button', (x.upper ? 'on' : ''), 'AA')); cap.title = 'Capitals'; cap.onclick = () => set(() => x.upper = !x.upper);
  for (const [a, ic, tip] of [['left', '⇤', 'Align left'], ['center', '↔', 'Centre'], ['right', '⇥', 'Align right']]) { const b = add(el('button', (x.align || 'center') === a ? 'on' : '', ic)); b.title = tip; b.onclick = () => set(() => x.align = a); }
  const col = add(el('input')); col.type = 'color'; col.value = (x.color || '#ffffff').slice(0, 7); col.title = 'Text colour'; col.oninput = () => { x.color = col.value; PX.redraw(); if (UI._place && $('#vTextEdit')) UI._place(); }; col.onchange = () => set(() => x.color = col.value);
  const ol = add(el('button', x.outline > 0 ? 'on' : '', 'OUTLINE')); ol.title = 'An outline round the letters'; ol.onclick = () => set(() => x.outline = x.outline > 0 ? 0 : 4);
  const oc = add(el('input')); oc.type = 'color'; oc.value = (x.outlineColor || '#000000').slice(0, 7); oc.title = 'Outline colour'; oc.onchange = () => set(() => { x.outlineColor = oc.value; if (!(x.outline > 0)) x.outline = 4; });
  const bg = add(el('select')); bg.title = 'Background behind the text'; for (const [v, n] of [['none', 'No box'], ['box', 'Box'], ['round', 'Rounded box'], ['pill', 'Pill']]) { const o = el('option', '', n); o.value = v; bg.appendChild(o); } bg.value = x.bg || 'none'; bg.onchange = () => set(() => x.bg = bg.value);
  const bc = add(el('input')); bc.type = 'color'; bc.value = (x.bgColor || '#a85cff').slice(0, 7); bc.title = 'Box colour'; bc.onchange = () => set(() => { x.bgColor = bc.value; if (!x.bg || x.bg === 'none') x.bg = 'round'; });
};
const prevAfter = PX.afterDraw; PX.afterDraw = (g, W, H, t) => { if (prevAfter) prevAfter(g, W, H, t); UI.syncTitleBar(); if ($('#vTextEdit') && UI._place) UI._place(); };

// ---------------- the Text panel: a box to type in, and ADD TEXT ----------------
const prevOpen = UI.openPanel; UI.openPanel = (k) => { prevOpen(k); if (k !== 'text') return; const b = $('#vPopB'); if (!b) return; const box = el('div', 'vtextpanel'); b.insertBefore(box, b.firstChild); const c = textSel();
  const r = el('div', 'btnrow'); box.appendChild(r); const ad = el('button', 'accent', '+ ADD TEXT'); ad.title = 'A plain title at the playhead, ready to type'; ad.onclick = () => UI.addPlainText(); r.appendChild(ad);
  if (c) { box.appendChild(el('div', 'hint', '<b>YOUR TEXT</b> type here, or double-click the title on the video')); const ta = el('textarea', 'vtextbox'); ta.rows = 3; ta.value = c.text.content; box.appendChild(ta); ta.onfocus = () => PX.begin(); ta.oninput = () => { c.text.content = ta.value; PX.redraw(); }; ta.onchange = () => PX.commit('text'); ta.onblur = () => PX.commit('text'); }
  else box.appendChild(el('p', 'hint', 'Press ADD TEXT (or a style below) and type straight onto the video. Pick a title on the timeline to change its words.'));
  box.appendChild(el('div', 'hint', '<b>STYLES</b>')); };
// picking another clip refreshes the panel's text box
const prevSel = UI.renderSel; UI.renderSel = () => { prevSel(); UI.syncTitleBar(); const p = $('#vPopT'); if (p && p.textContent === 'TEXT' && !$('#vTextEdit') && !(document.activeElement && document.activeElement.classList.contains('vtextbox'))) UI.openPanel('text'); };
})();
