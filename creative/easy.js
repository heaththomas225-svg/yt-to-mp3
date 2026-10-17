'use strict';
// =====================================================================
//  PURPHEX MUSIC PHOTO EDITOR, made easy: click anything on the picture and its tools appear right on it (edit, font, colour,
//  effects, duplicate, front / back, delete), double-click text to type on the picture, right-click for a menu, an outline shows
//  what you are pointing at, and the tools are big tiles with pictures. Every word, sticker and shape of a template can go in one click.
// =====================================================================
(() => {
const C = PX.Cover;
const sel = () => C.art && C.art.layers.find(l => l.id === C.sel);
const isBox = (L) => (C.boxTypes || ['text', 'image', 'shape', 'sticker']).includes(L.type);
const esc = (s) => String(s == null ? '' : s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));
const ICON = { templates: '🧩', bg: '🎨', text: '🔤', stickers: '⭐', photos: '🖼️', cutout: '✂️', effects: '✨', filters: '🎞️', adjust: '🎚️', collage: '🔲', draw: '✏️', frames: '🟪' };
// ---- the things you can do to a layer ----
const A = {
  del: (L) => { C.begin(); C.art.layers = C.art.layers.filter(q => q !== L); if (C.sel === L.id) C.sel = null; C.commit(); PX.toast('Deleted "' + L.name + '" (UNDO brings it back).'); },
  dup: (L) => { C.sel = L.id; C.act('dup'); },
  front: (L) => { const i = C.art.layers.indexOf(L); if (i < C.art.layers.length - 1) { C.begin(); C.art.layers.splice(i, 1); C.art.layers.push(L); C.commit(); } },
  back: (L) => { const i = C.art.layers.indexOf(L), bi = C.art.layers.findIndex(q => q.name === 'Background'); const to = bi >= 0 && bi !== i ? bi + 1 : 0; if (i > to) { C.begin(); C.art.layers.splice(i, 1); C.art.layers.splice(to, 0, L); C.commit(); } },
  lock: (L) => { C.begin(); L.locked = !L.locked; C.commit(); },
  hide: (L) => { C.begin(); L.vis = !L.vis; C.commit(); },
  edit: (L) => C.inlineEdit(L),
  font: (L) => { C.tab = 'text'; C.renderTab(); const fl = document.querySelector('#cvTab .fontlist'); if (fl) fl.scrollIntoView({ block: 'start' }); },
  colour: (L) => { const k = L.type === 'vsticker' || L.type === 'shape' || L.type === 'text' ? 'color' : null; if (!k) return; const i = document.createElement('input'); i.type = 'color'; i.value = (L[k] || '#ffffff').slice(0, 7); i.style.position = 'fixed'; i.style.left = '-50px'; document.body.appendChild(i); i.oninput = () => { C.begin(); L[k] = i.value; C.draw(); }; i.onchange = () => { C.commit(); i.remove(); }; i.click(); },
  effects: (L) => { C.tab = L.type === 'image' || L.type === 'cell' ? 'effects' : 'filters'; C.renderTab(); },
  cutout: (L) => { C.tab = 'cutout'; C.renderTab(); },
  crop: (L) => C.cropDialog && C.cropDialog(L),
  replace: (L) => { if (L.type === 'cell') C.fillCell(L); else { C._replace = L.id; PX.send({ t: 'artImport' }); } },
};
PX.on('artImported', (m) => { if (!C._replace) return; const L = C.art.layers.find(l => l.id === C._replace); C._replace = null; if (!L) return; C.begin(); L.url = m.url; L.px = {}; L.cut = {}; C.commit(); });
// which buttons a layer gets
function tools(L) {
  const t = []; if (L.type === 'text') t.push(['edit', '✎ EDIT TEXT'], ['font', 'Aa FONT'], ['colour', '● COLOUR']);
  if (L.type === 'vsticker' || L.type === 'shape') t.push(['colour', '● COLOUR']); if (L.type === 'vsticker' && L.text != null) t.push(['edit', '✎ WORDS']);
  if (L.type === 'image') t.push(['replace', '⇄ REPLACE'], ['crop', '⌗ CROP'], ['cutout', '✂ CUTOUT'], ['effects', '✨ EFFECTS']);
  if (L.type === 'cell') t.push(['replace', L.url ? '⇄ REPLACE' : '+ PHOTO'], ['effects', '✨ EFFECTS']);
  t.push(['dup', '⧉ DUPLICATE'], ['front', '▲ FRONT'], ['back', '▼ BACK'], ['lock', L.locked ? '🔒 UNLOCK' : '🔓 LOCK'], ['del', '🗑 DELETE']); return t;
}
// ---- the toolbar on the picture ----
let bar = null, hoverId = null, menu = null;
function ensure() {
  const st = document.querySelector('#cvStage'); if (!st || st._easy) return !!st; st._easy = true; st.style.position = 'relative';
  bar = document.createElement('div'); bar.id = 'cvBar'; bar.className = 'cvbar hidden'; st.appendChild(bar);
  const cv = document.querySelector('#cvCanvas');
  cv.addEventListener('dblclick', (e) => { const L = hitAt(e); if (L && (L.type === 'text' || (L.type === 'vsticker' && L.text != null))) { C.sel = L.id; C.render(); C.inlineEdit(L); } else if (L && L.type === 'image') A.replace(L); else if (L && L.type === 'cell') A.replace(L); });
  cv.addEventListener('mousemove', (e) => { if (e.buttons) return; const L = hitAt(e); const id = L ? L.id : null; if (id !== hoverId) { hoverId = id; C.draw(); } cv.style.cursor = L ? (L.locked ? 'not-allowed' : 'move') : 'default'; });
  cv.addEventListener('mouseleave', () => { if (hoverId) { hoverId = null; C.draw(); } });
  cv.addEventListener('contextmenu', (e) => { e.preventDefault(); const L = hitAt(e); if (!L) return; C.sel = L.id; C.render(); showMenu(L, e.clientX, e.clientY); });
  document.addEventListener('mousedown', (e) => { if (menu && !menu.contains(e.target)) { menu.remove(); menu = null; } }, true);
  // the tools become big tiles with pictures
  const tn = document.querySelector('#cvTabs'); if (tn) { tn.classList.add('rail'); tn.querySelectorAll('button').forEach(b => { if (b.querySelector('.ic')) return; const lab = b.textContent; b.innerHTML = '<span class="ic">' + (ICON[b.dataset.t] || '•') + '</span><span class="lb">' + esc(lab) + '</span>'; }); }
  if (!C._tipShown) { C._tipShown = true; setTimeout(() => PX.toast('Tip: click anything on the picture to change it or delete it. Double-click text to type.'), 600); }
  return true;
}
function hitAt(e) {
  const cv = document.querySelector('#cvCanvas'), r = cv.getBoundingClientRect(), k = cv.width / r.width, px = (e.clientX - r.left) * k, py = (e.clientY - r.top) * k, W = cv.width, H = cv.height;
  for (const L of C.art.layers.slice().reverse()) { if (!L.vis) continue; if (isBox(L)) { const b = C.box(L, W, H), a = -(L.rot || 0) * Math.PI / 180, lx = (px - b.cx) * Math.cos(a) - (py - b.cy) * Math.sin(a), ly = (px - b.cx) * Math.sin(a) + (py - b.cy) * Math.cos(a); if (Math.abs(lx) < b.w / 2 && Math.abs(ly) < b.h / 2) return L; } else if (L.type === 'cell' && px >= L.rx * W && px <= (L.rx + L.rw) * W && py >= L.ry * H && py <= (L.ry + L.rh) * H) return L; }
  return null;
}
function showMenu(L, x, y) {
  if (menu) menu.remove(); menu = document.createElement('div'); menu.className = 'cvmenu'; menu.style.left = x + 'px'; menu.style.top = y + 'px';
  for (const [k, lab] of tools(L)) { const b = document.createElement('button'); b.innerHTML = esc(lab); b.onclick = () => { menu.remove(); menu = null; A[k](L); }; menu.appendChild(b); }
  document.body.appendChild(menu); const mr = menu.getBoundingClientRect(); if (mr.right > innerWidth - 8) menu.style.left = (innerWidth - mr.width - 8) + 'px'; if (mr.bottom > innerHeight - 8) menu.style.top = (innerHeight - mr.height - 8) + 'px';
}
// called after every redraw: the hover outline, and the toolbar placed just above the picked layer
C.afterDraw = () => {
  if (!ensure()) return; const cv = document.querySelector('#cvCanvas'), g = cv.getContext('2d'), W = cv.width, H = cv.height, L = sel();
  if (hoverId && (!L || hoverId !== L.id)) { const Hv = C.art.layers.find(l => l.id === hoverId); if (Hv && isBox(Hv)) { const b = C.box(Hv, W, H); g.save(); g.translate(b.cx, b.cy); g.rotate((Hv.rot || 0) * Math.PI / 180); g.strokeStyle = 'rgba(255,255,255,0.75)'; g.lineWidth = 1.5; g.strokeRect(-b.w / 2, -b.h / 2, b.w, b.h); g.restore(); } else if (Hv && Hv.type === 'cell') { g.save(); g.strokeStyle = 'rgba(255,255,255,0.75)'; g.lineWidth = 1.5; g.strokeRect(Hv.rx * W, Hv.ry * H, Hv.rw * W, Hv.rh * H); g.restore(); } }
  if (!L || C.tool !== 'move' || C._editing) { bar.classList.add('hidden'); return; }
  const key = L.id + '|' + L.type + '|' + (L.locked ? 1 : 0) + '|' + (L.url ? 1 : 0); if (bar._key !== key) { bar._key = key; bar.innerHTML = '<span class="nm">' + esc(L.name) + '</span>'; for (const [k, lab] of tools(L)) { const b = document.createElement('button'); b.innerHTML = esc(lab); b.dataset.k = k; if (k === 'del') b.className = 'danger'; b.onmousedown = (e) => e.stopPropagation(); b.onclick = (e) => { e.stopPropagation(); A[k](L); }; bar.appendChild(b); } }
  bar.classList.remove('hidden');
  const st = document.querySelector('#cvStage'), sr = st.getBoundingClientRect(), cr = cv.getBoundingClientRect(), s = cr.width / W; let top, left;
  if (isBox(L)) { const b = C.box(L, W, H); left = cr.left - sr.left + st.scrollLeft + b.cx * s; top = cr.top - sr.top + st.scrollTop + (b.cy - b.h / 2) * s - 64; }
  else if (L.type === 'cell') { left = cr.left - sr.left + st.scrollLeft + (L.rx + L.rw / 2) * W * s; top = cr.top - sr.top + st.scrollTop + L.ry * H * s - 52; }
  else { left = cr.left - sr.left + st.scrollLeft + cr.width / 2; top = cr.top - sr.top + st.scrollTop + 8; }   // a background, frame or texture: at the top of the picture
  const bw = bar.offsetWidth || 400; left = Math.max(st.scrollLeft + 6, Math.min(st.scrollLeft + st.clientWidth - bw - 6, left - bw / 2)); top = Math.max(st.scrollTop + 6, top); bar.style.left = left + 'px'; bar.style.top = top + 'px';
};
// ---- typing on the picture ----
C.inlineEdit = (L) => {
  ensure(); const cv = document.querySelector('#cvCanvas'), st = document.querySelector('#cvStage'), W = cv.width, H = cv.height, b = C.box(L, W, H), sr = st.getBoundingClientRect(), cr = cv.getBoundingClientRect(), s = cr.width / W;
  const ta = document.createElement('textarea'); ta.className = 'cvinline'; ta.value = L.text || ''; const fs = Math.max(14, Math.min(64, (L.type === 'text' ? L.size * W / C.art.w : 40) * s * (L.scale || 1)));
  ta.style.left = (cr.left - sr.left + st.scrollLeft + (b.cx - b.w / 2) * s) + 'px'; ta.style.top = (cr.top - sr.top + st.scrollTop + (b.cy - b.h / 2) * s) + 'px'; ta.style.width = Math.max(160, b.w * s) + 'px'; ta.style.minHeight = Math.max(40, b.h * s) + 'px'; ta.style.fontSize = fs + 'px'; ta.style.fontFamily = '"' + (L.font || 'Segoe UI') + '"';
  st.appendChild(ta); C._editing = true; bar.classList.add('hidden'); C.begin(); const before = L.text; ta.focus(); ta.select();
  const done = (keep) => { if (!ta.parentNode) return; if (!keep) L.text = before; ta.remove(); C._editing = false; if (keep && L.text !== before) { L.name = (L.text || '').split('\n')[0].slice(0, 20) || L.name; C.commit(); } else { C._b = null; C.render(); } };
  C._inlineDone = done; ta.oninput = () => { L.text = ta.value; C.draw(); }; ta.onkeydown = (e) => { e.stopPropagation(); if (e.key === 'Escape') done(false); if (e.key === 'Enter' && (e.ctrlKey || !ta.value.includes('\n') && !e.shiftKey && L.type !== 'text')) { e.preventDefault(); done(true); } }; ta.onblur = () => done(true);
};
C.easyActions = A;
})();
