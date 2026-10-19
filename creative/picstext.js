'use strict';
// =====================================================================
//  PURPHEX MUSIC PHOTO EDITOR: quick text. Tap TEXT and a typing box opens at once; press the tick and the words go in
//  the middle of the picture, picked. The options are simple tabs (Font, Colour, Stroke, Shadow, Highlight, Bend, Spacing, Opacity,
//  Effects, Styles): a row of choices and one slider each, seen live. On the picture every picked layer has an X to delete it,
//  a pencil to edit the words, and one corner handle that resizes and turns it in one drag.
// =====================================================================
(() => {
const C = PX.Cover, clamp = PX.clamp;
const el = (tag, cls, html) => { const e = document.createElement(tag); if (cls) e.className = cls; if (html != null) e.innerHTML = html; return e; };
const esc = (s) => String(s == null ? '' : s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const sel = () => C.art && C.art.layers.find(l => l.id === C.sel);
const isBox = (L) => (C.boxTypes || []).includes(L.type);
const SW = ['#ffffff', '#000000', '#ff3b30', '#ff9500', '#ffcc00', '#34c759', '#00c7be', '#30b0ff', '#007aff', '#5856d6', '#af52de', '#ff2d55', '#a85cff', '#3ce1ff', '#ff50c4', '#ffd23c', '#78f08c', '#8e8e93', '#3a1066', '#14061f', '#f4f1ea', '#c99a5b', '#7a1d4f', '#0b3d2e'];
// ---------------- 1. typing first ----------------
C.typeText = (L) => {
  const editing = !!L; const box = el('div', 'ptype', '<div class="ptypebox"><textarea id="ptT" placeholder="Type your text"></textarea><div class="ptypebar"><button id="ptX" title="Cancel">✕</button><span class="grow"></span><button id="ptOk" class="accent" title="Done">✓ DONE</button></div></div>');
  document.getElementById('cover').appendChild(box); const ta = box.querySelector('#ptT'); ta.value = editing ? L.text : ''; setTimeout(() => { ta.focus(); ta.select(); }, 30);
  const done = (ok) => { box.remove(); if (!ok) return; const v = ta.value.trim(); if (!v) return; if (editing) { C.begin(); L.text = v; L.name = v.split('\n')[0].slice(0, 20); C.commit(); } else { const s = Math.round(Math.min(C.art.w, C.art.h) / 1080 * 140); const n = C.addLayer(Object.assign(C.lib.textBase(), { text: v, name: v.split('\n')[0].slice(0, 20), size: s, weight: 800 })); C.sel = n.id; C.tool = 'move'; C.render(); } C.openPanel('text'); };
  box.querySelector('#ptOk').onclick = () => done(true); box.querySelector('#ptX').onclick = () => done(false); ta.onkeydown = (e) => { e.stopPropagation(); if (e.key === 'Escape') done(false); if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); done(true); } };
};
// ---------------- 2. the text options ----------------
let tab = 'font';
const TABS = [['font', 'Font'], ['colour', 'Colour'], ['stroke', 'Stroke'], ['shadow', 'Shadow'], ['highlight', 'Highlight'], ['bend', 'Bend'], ['spacing', 'Spacing'], ['opacity', 'Opacity'], ['effects', 'Effects'], ['styles', 'Styles']];
function live(L, fn) { if (!C._b) C.begin(); fn(); C.draw(); clearTimeout(live.t); live.t = setTimeout(() => { if (C._b) C.commit(); }, 350); }
function swatches(b, get, set, withNone) { const r = el('div', 'pswrow'); if (withNone) { const n = el('button', 'psw none' + (!get() ? ' on' : ''), '⦸'); n.title = 'None'; n.onclick = () => { set(null); C.renderTab(); }; r.appendChild(n); } for (const c of SW) { const s = el('button', 'psw' + (get() === c ? ' on' : '')); s.style.background = c; s.title = c; s.onclick = () => { set(c); C.renderTab(); }; r.appendChild(s); } const p = el('input'); p.type = 'color'; p.className = 'pswpick'; p.value = (get() || '#ffffff').slice(0, 7); p.title = 'Any colour'; p.oninput = () => set(p.value); p.onchange = () => C.renderTab(); r.appendChild(p); b.appendChild(r); }
function slider(b, label, min, max, step, get, set) { const d = el('div', 'psl', '<label>' + esc(label) + '</label><input type="range" min="' + min + '" max="' + max + '" step="' + step + '"><span class="pv"></span>'); const r = d.querySelector('input'), v = d.querySelector('.pv'); r.value = get(); v.textContent = r.value; r.oninput = () => { v.textContent = r.value; set(+r.value); }; b.appendChild(d); }
function textOptions(b, L) {
  const ta = el('textarea', 'ptedit'); ta.value = L.text; ta.rows = 2; ta.oninput = () => live(L, () => { L.text = ta.value; L.name = ta.value.split('\n')[0].slice(0, 20) || L.name; }); b.appendChild(ta);
  const tr = el('div', 'ptabs'); for (const [k, lab] of TABS) { const x = el('button', k === tab ? 'on' : '', lab); x.onclick = () => { tab = k; C.renderTab(); }; tr.appendChild(x); } b.appendChild(tr);
  const body = el('div', 'ptbody'); b.appendChild(body); const set = (k) => (v) => live(L, () => L[k] = v);
  if (tab === 'font') { const q = el('input'); q.type = 'text'; q.placeholder = 'Search fonts'; q.className = 'ptq'; body.appendChild(q); const list = el('div', 'pfonts'); body.appendChild(list); const sample = (L.upper ? L.text.toUpperCase() : L.text).split('\n')[0].slice(0, 18) || 'Aa';
    const fill = () => { list.innerHTML = ''; for (const fn of PX.UI.fonts.filter(x => x.toLowerCase().includes(q.value.toLowerCase())).slice(0, 400)) { const d = el('button', 'pfont' + (L.font === fn ? ' on' : ''), '<span class="ps">' + esc(sample) + '</span><span class="pn">' + esc(fn) + '</span>'); d.querySelector('.ps').style.fontFamily = '"' + fn + '"'; d.onclick = () => { live(L, () => L.font = fn); list.querySelectorAll('.pfont').forEach(x => x.classList.toggle('on', x === d)); }; list.appendChild(d); } }; q.oninput = fill; fill();
    const wr = el('div', 'btnrow'); for (const [w, n] of [[400, 'Regular'], [700, 'Bold'], [900, 'Black']]) { const x = el('button', L.weight == w ? 'on' : '', n); x.onclick = () => { live(L, () => L.weight = w); C.renderTab(); }; wr.appendChild(x); } const it = el('button', L.italic ? 'on' : '', '<i>Italic</i>'); it.onclick = () => { live(L, () => L.italic = !L.italic); C.renderTab(); }; wr.appendChild(it); const up = el('button', L.upper ? 'on' : '', 'AA'); up.title = 'Capitals'; up.onclick = () => { live(L, () => L.upper = !L.upper); C.renderTab(); }; wr.appendChild(up); for (const [a, n] of [['left', '⯇ Left'], ['center', 'Centre'], ['right', 'Right ⯈']]) { const x = el('button', L.align === a ? 'on' : '', n); x.onclick = () => { live(L, () => L.align = a); C.renderTab(); }; wr.appendChild(x); } body.insertBefore(wr, q); }
  if (tab === 'colour') { swatches(body, () => L.color, (c) => live(L, () => { L.color = c; L.grad = false; })); const g = el('label', 'chk', '<input type="checkbox"> Gradient (two colours)'); const gi = g.querySelector('input'); gi.checked = !!L.grad; gi.onchange = () => { live(L, () => L.grad = gi.checked); C.renderTab(); }; body.appendChild(g); if (L.grad) swatches(body, () => L.color2, (c) => live(L, () => L.color2 = c)); }
  if (tab === 'stroke') { slider(body, 'Thickness', 0, 40, 1, () => L.outline || 0, set('outline')); swatches(body, () => L.outlineColor, (c) => live(L, () => { L.outlineColor = c; if (!L.outline) L.outline = 6; })); }
  if (tab === 'shadow') { slider(body, 'Softness', 0, 120, 1, () => L.shadow || 0, set('shadow')); swatches(body, () => L.shadowColor, (c) => live(L, () => { L.shadowColor = c; if (!L.shadow) L.shadow = 30; })); }
  if (tab === 'highlight') { const r = el('div', 'btnrow'); for (const [k, n] of [['none', 'None'], ['box', 'Box'], ['round', 'Rounded'], ['pill', 'Pill']]) { const x = el('button', (L.bg || 'none') === k ? 'on' : '', n); x.onclick = () => { live(L, () => L.bg = k); C.renderTab(); }; r.appendChild(x); } body.appendChild(r); swatches(body, () => L.bgColor, (c) => live(L, () => { L.bgColor = c; if (!L.bg || L.bg === 'none') L.bg = 'round'; })); }
  if (tab === 'bend') slider(body, 'Bend', -100, 100, 1, () => L.curve || 0, set('curve'));
  if (tab === 'spacing') { slider(body, 'Letters', -20, 120, 1, () => L.spacing || 0, set('spacing')); slider(body, 'Lines', 0.7, 2.5, 0.05, () => L.lineH || 1.1, set('lineH')); }
  if (tab === 'opacity') slider(body, 'Opacity', 0, 100, 1, () => Math.round((L.opacity == null ? 1 : L.opacity) * 100), (v) => live(L, () => L.opacity = v / 100));
  if (tab === 'effects') { const r = el('div', 'btnrow'); const tog = (n, on, fn) => { const x = el('button', on ? 'on' : '', n); x.onclick = () => { live(L, fn); C.renderTab(); }; r.appendChild(x); }; tog('Neon glow', L.neon, () => L.neon = !L.neon); body.appendChild(r); if (L.neon) swatches(body, () => L.glowColor, (c) => live(L, () => L.glowColor = c)); slider(body, '3D depth', 0, 60, 1, () => L.extrude || 0, set('extrude')); if (L.extrude) swatches(body, () => L.extrudeColor, (c) => live(L, () => L.extrudeColor = c)); slider(body, 'Glitch', 0, 40, 1, () => L.glitch || 0, set('glitch')); }
  if (tab === 'styles') { body.appendChild(el('p', 'hint', 'One click changes the look of these words (the words stay).')); const g = el('div', 'grid2'); body.appendChild(g); const presets = [['Plain', { neon: false, outline: 0, shadow: 0, extrude: 0, glitch: 0, bg: 'none', grad: false }], ['Big bold', { weight: 900, upper: true, spacing: 6 }], ['Neon glow', { neon: true, color: '#ffffff', glowColor: '#ff50c4', weight: 800 }], ['Outline', { color: '#00000000', outline: 6, outlineColor: '#ffffff', weight: 900 }], ['Drop shadow', { shadow: 40, shadowColor: '#000000' }], ['Gradient', { grad: true, color: '#ffd23c', color2: '#ff50c4', weight: 900 }], ['3D', { extrude: 18, extrudeColor: '#5a1d9a', weight: 900 }], ['Glitch', { glitch: 12, weight: 900 }], ['Retro', { grad: true, color: '#ff9f1c', color2: '#ff50c4', extrude: 10, extrudeColor: '#3a1066', italic: true, weight: 900 }], ['Sticker', { outline: 14, outlineColor: '#ffffff', color: '#ff50c4', weight: 900, shadow: 20 }], ['Boxed', { bg: 'box', bgColor: '#ff50c4', color: '#14061f', weight: 900 }], ['Handwritten', { font: 'Segoe Script', weight: 400 }]];
    for (const [n, p] of presets) { const c = el('div', 'card', esc(n)); c.onclick = () => live(L, () => Object.assign(L, p)); g.appendChild(c); } if (C.afterTextTab) C.afterTextTab(body); }
}
(C.extraTabs = C.extraTabs || {}).text = (b) => {
  const L = sel();
  if (L && L.type === 'text') { textOptions(b, L); return; }
  const add = el('button', 'accent ptadd', '＋ ADD TEXT'); add.onclick = () => C.typeText(); b.appendChild(add);
  b.appendChild(el('p', 'hint', 'Press ADD TEXT and type. Then pick a font, colour, stroke and more here, and drag the text on the picture. Tap a text on the picture to change it.'));
};
// tapping TEXT opens the typing box straight away (when no text is picked)
const prevOpen = C.openPanel; C.openPanel = (k) => { if (k !== 'text') { const tb = document.querySelector('.ptype'); if (tb) tb.remove(); } prevOpen(k); if (k === 'text' && !(sel() && sel().type === 'text') && !document.querySelector('.ptype') && !C._noAutoType) C.typeText(); };
// picking a text on the picture shows its options
const prevRender = C.render; C.render = () => { prevRender(); const L = sel(), pop = document.querySelector('#cvPop'); if (L && L.type === 'text' && pop && !pop.classList.contains('hidden') && C.tab !== 'text' && ['bg', 'templates', 'design'].indexOf(C.tab) < 0) { C._noAutoType = true; C.openPanel('text'); C._noAutoType = false; } };
// ---------------- 3. the handles on the picture ----------------
const HR = 13;
function handles(L, W, H) { const b = C.box(L, W, H), a = (L.rot || 0) * Math.PI / 180, ca = Math.cos(a), sa = Math.sin(a); const pt = (lx, ly) => [b.cx + lx * ca - ly * sa, b.cy + lx * sa + ly * ca]; return { del: pt(-b.w / 2, -b.h / 2), edit: pt(b.w / 2, -b.h / 2), turn: pt(b.w / 2, b.h / 2), b }; }
const prevAfter = C.afterDraw; C.afterDraw = () => {
  if (prevAfter) prevAfter(); const L = sel(); if (!L || !isBox(L) || C.tool !== 'move' || C._editing || (C.multi && C.multi.size > 1) || L.locked) return;
  const cv = document.querySelector('#cvCanvas'); if (!cv) return; const g = cv.getContext('2d'), h = handles(L, cv.width, cv.height);
  const dot = (p, fill, ch) => { g.save(); g.beginPath(); g.arc(p[0], p[1], HR, 0, 7); g.fillStyle = fill; g.fill(); g.lineWidth = 2; g.strokeStyle = '#fff'; g.stroke(); g.fillStyle = '#fff'; g.font = '700 14px "Segoe UI Symbol","Segoe UI"'; g.textAlign = 'center'; g.textBaseline = 'middle'; g.fillText(ch, p[0], p[1] + 1); g.restore(); };
  dot(h.del, '#e5383b', '✕'); if (L.type === 'text' || (L.type === 'vsticker' && L.text != null)) dot(h.edit, '#3a7bff', '✎'); dot(h.turn, '#14061f', '⤡');
};
function stage() {
  const st = document.querySelector('#cvStage'), cv = document.querySelector('#cvCanvas'); if (!st || st._ph) return; st._ph = true;
  st.addEventListener('mousedown', (e) => {
    if (e.button !== 0 || e.target !== cv || C.tool !== 'move') return; const L = sel(); if (!L || !isBox(L) || L.locked || (C.multi && C.multi.size > 1)) return;
    const r = cv.getBoundingClientRect(), k = cv.width / r.width, px = (e.clientX - r.left) * k, py = (e.clientY - r.top) * k, h = handles(L, cv.width, cv.height), near = (p) => Math.hypot(px - p[0], py - p[1]) < HR + 4;
    if (near(h.del)) { e.stopPropagation(); e.preventDefault(); C.begin(); C.art.layers = C.art.layers.filter(q => q !== L); C.sel = null; C.commit(); return; }
    if ((L.type === 'text' || (L.type === 'vsticker' && L.text != null)) && near(h.edit)) { e.stopPropagation(); e.preventDefault(); if (L.type === 'text') C.typeText(L); else C.inlineEdit(L); return; }
    if (near(h.turn)) {   // one handle: drag out to make it bigger, round to turn it
      e.stopPropagation(); e.preventDefault(); C.begin(); const cx = h.b.cx, cy = h.b.cy, d0 = Math.hypot(px - cx, py - cy), a0 = Math.atan2(py - cy, px - cx), s0 = L.scale || 1, r0 = L.rot || 0;
      const mv = (ev) => { const qx = (ev.clientX - r.left) * k, qy = (ev.clientY - r.top) * k; L.scale = clamp(s0 * Math.hypot(qx - cx, qy - cy) / Math.max(1, d0), 0.05, 10); let an = r0 + (Math.atan2(qy - cy, qx - cx) - a0) * 180 / Math.PI; if (!ev.altKey) for (const snap of [0, 90, 180, -90, -180, 270]) if (Math.abs(an - snap) < 4) an = snap; L.rot = Math.round(an * 10) / 10; C.draw(); };
      const up = () => { removeEventListener('mousemove', mv); removeEventListener('mouseup', up); C.commit(); }; addEventListener('mousemove', mv); addEventListener('mouseup', up);
    }
  }, true);
}
const prevShow = C.show; C.show = () => { prevShow(); stage(); };
setTimeout(stage, 0);
C.textTab = (k) => { tab = k; };
})();
